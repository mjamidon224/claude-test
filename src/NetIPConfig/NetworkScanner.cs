using System.Buffers.Binary;
using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;

namespace NetIPConfig;

/// <summary>One device that answered during a scan.</summary>
internal sealed record ScanHit(
    IPAddress Address,
    string MacAddress,
    string HostName,
    long? RoundTripMs,
    string Note);

/// <summary>
/// Finds the devices that are live on the selected adapter's own subnet.
///
/// Each address is probed two ways: an ICMP echo, and an ARP request through SendARP.
/// ARP matters because a device on the same layer-2 segment has to answer it to
/// communicate at all, while plenty of hosts (Windows with its default firewall, printers,
/// cameras) silently drop pings. ARP is also what supplies the MAC address.
/// </summary>
internal static class NetworkScanner
{
    /// <summary>Scanning is capped so a wide mask cannot start a run that takes hours.</summary>
    public const int MaxHosts = 1024;

    private const int Concurrency = 64;
    private const int PingTimeoutMs = 600;
    private const int NameTimeoutMs = 1500;

    public static long CountHosts(int prefixLength) => prefixLength switch
    {
        32 => 1,
        31 => 2,
        _ => (1L << (32 - prefixLength)) - 2,
    };

    /// <summary>
    /// The host addresses of the subnet containing <paramref name="address"/>, excluding the
    /// network and broadcast addresses, and never more than <see cref="MaxHosts"/> of them.
    /// </summary>
    public static List<IPAddress> EnumerateHosts(IPAddress address, IPAddress mask)
    {
        List<IPAddress> hosts = new();
        if (!IPv4Text.IsValidMask(mask, out int prefixLength))
        {
            return hosts;
        }

        uint addressValue = IPv4Text.ToUInt32(address);
        uint maskValue = IPv4Text.ToUInt32(mask);

        // A /32 is a single address and a /31 is a point-to-point pair; neither has a
        // network or broadcast address to leave out.
        if (prefixLength == 32)
        {
            hosts.Add(address);
            return hosts;
        }

        if (prefixLength == 31)
        {
            uint first = addressValue & maskValue;
            hosts.Add(IPv4Text.FromUInt32(first));
            hosts.Add(IPv4Text.FromUInt32(first + 1));
            return hosts;
        }

        uint network = addressValue & maskValue;
        uint broadcast = addressValue | ~maskValue;

        for (uint value = network + 1; value < broadcast && hosts.Count < MaxHosts; value++)
        {
            hosts.Add(IPv4Text.FromUInt32(value));
        }

        return hosts;
    }

    /// <summary>
    /// Probes every address, reporting each responder through <paramref name="onHit"/> and
    /// the running total through <paramref name="onProgress"/>. Both are <see cref="IProgress{T}"/>
    /// so a caller that creates them on the UI thread is called back there.
    /// </summary>
    public static async Task ScanAsync(
        IReadOnlyList<IPAddress> hosts,
        AdapterInfo adapter,
        IProgress<ScanHit>? onHit,
        IProgress<int>? onProgress,
        CancellationToken token)
    {
        using SemaphoreSlim gate = new(Concurrency);
        int completed = 0;

        IEnumerable<Task> probes = hosts.Select(async host =>
        {
            await gate.WaitAsync(token).ConfigureAwait(false);

            try
            {
                ScanHit? hit = await ProbeAsync(host, adapter, token).ConfigureAwait(false);
                if (hit is not null)
                {
                    onHit?.Report(hit);
                }
            }
            catch (OperationCanceledException)
            {
                // Stopping the scan is not an error.
            }
            finally
            {
                gate.Release();
                onProgress?.Report(Interlocked.Increment(ref completed));
            }
        });

        await Task.WhenAll(probes).ConfigureAwait(false);
    }

    private static async Task<ScanHit?> ProbeAsync(IPAddress host, AdapterInfo adapter, CancellationToken token)
    {
        long? roundTrip = null;

        try
        {
            using Ping ping = new();
            PingReply reply = await ping.SendPingAsync(host, PingTimeoutMs).ConfigureAwait(false);
            if (reply.Status == IPStatus.Success)
            {
                roundTrip = reply.RoundtripTime;
            }
        }
        catch (Exception)
        {
            // A failed ping is just one of the two signals; carry on to ARP.
        }

        token.ThrowIfCancellationRequested();

        string mac = await Task.Run(() => ResolveMacAddress(host), token).ConfigureAwait(false);

        if (roundTrip is null && mac.Length == 0)
        {
            return null;
        }

        string hostName = await ResolveHostNameAsync(host, token).ConfigureAwait(false);
        return new ScanHit(host, mac, hostName, roundTrip, DescribeRole(host, adapter));
    }

    private static string DescribeRole(IPAddress host, AdapterInfo adapter)
    {
        string address = host.ToString();

        if (string.Equals(address, adapter.IPv4Address, StringComparison.Ordinal))
        {
            return "this computer";
        }

        if (string.Equals(address, adapter.Gateway, StringComparison.Ordinal))
        {
            return "default gateway";
        }

        if (string.Equals(address, adapter.DhcpServer, StringComparison.Ordinal))
        {
            return "DHCP server";
        }

        return adapter.DnsServers.Any(s => string.Equals(s, address, StringComparison.Ordinal))
            ? "DNS server"
            : "";
    }

    /// <summary>ARP-resolves one on-link address to a MAC, or "" if nothing answered.</summary>
    private static string ResolveMacAddress(IPAddress host)
    {
        try
        {
            byte[] buffer = new byte[6];
            uint length = (uint)buffer.Length;

            // SendARP takes the address as a ULONG already in network byte order, which is
            // the little-endian reading of the four address bytes.
            uint destination = BinaryPrimitives.ReadUInt32LittleEndian(host.GetAddressBytes());

            if (SendARP(destination, 0, buffer, ref length) != 0 || length < 6)
            {
                return "";
            }

            return buffer.All(b => b == 0)
                ? ""
                : string.Join("-", buffer.Select(b => b.ToString("X2")));
        }
        catch (Exception)
        {
            return "";
        }
    }

    private static async Task<string> ResolveHostNameAsync(IPAddress host, CancellationToken token)
    {
        try
        {
            Task<IPHostEntry> lookup = Dns.GetHostEntryAsync(host);
            Task finished = await Task.WhenAny(lookup, Task.Delay(NameTimeoutMs, token)).ConfigureAwait(false);

            if (finished != lookup)
            {
                // No reverse record, or the resolver is slow; the address alone will do.
                return "";
            }

            return (await lookup.ConfigureAwait(false)).HostName;
        }
        catch (Exception)
        {
            return "";
        }
    }

    [DllImport("iphlpapi.dll", ExactSpelling = true)]
    private static extern int SendARP(uint destination, uint source, byte[] macAddress, ref uint macAddressLength);
}
