using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Microsoft.Win32;

namespace NetIPConfig;

/// <summary>Reads the current IPv4 configuration of the machine's adapters.</summary>
internal static class NetworkQuery
{
    private const string InterfacesKeyPath =
        @"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces";

    /// <summary>
    /// Adapters a user would recognise in Network Connections, most usable first.
    /// Loopback and tunnel pseudo-adapters are filtered out.
    /// </summary>
    public static List<AdapterInfo> GetAdapters()
    {
        List<AdapterInfo> adapters = new();

        foreach (NetworkInterface nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel)
            {
                continue;
            }

            adapters.Add(Describe(nic));
        }

        // Connected adapters first, then by name, so the list opens on something useful.
        return adapters
            .OrderBy(a => a.Status == OperationalStatus.Up ? 0 : 1)
            .ThenBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private static AdapterInfo Describe(NetworkInterface nic)
    {
        int? index = null;
        bool? dhcpEnabled = null;
        string address = "";
        string mask = "";
        string gateway = "";
        string dhcpServer = "";
        List<string> dns = new();

        try
        {
            IPInterfaceProperties props = nic.GetIPProperties();

            // Throws when IPv4 is not bound to the adapter (or it is disabled).
            try
            {
                IPv4InterfaceProperties v4 = props.GetIPv4Properties();
                index = v4.Index;
                dhcpEnabled = v4.IsDhcpEnabled;
            }
            catch (NetworkInformationException)
            {
                // Fall back to the registry below.
            }
            catch (PlatformNotSupportedException)
            {
            }

            UnicastIPAddressInformation? unicast = props.UnicastAddresses
                .FirstOrDefault(a => a.Address.AddressFamily == AddressFamily.InterNetwork
                                     && !a.Address.Equals(IPAddress.Any));
            if (unicast is not null)
            {
                address = unicast.Address.ToString();
                if (unicast.IPv4Mask is not null && !unicast.IPv4Mask.Equals(IPAddress.Any))
                {
                    mask = unicast.IPv4Mask.ToString();
                }
            }

            gateway = props.GatewayAddresses
                .Select(g => g.Address)
                .FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork
                                     && !a.Equals(IPAddress.Any))?.ToString() ?? "";

            dhcpServer = props.DhcpServerAddresses
                .FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork)?.ToString() ?? "";

            dns = props.DnsAddresses
                .Where(a => a.AddressFamily == AddressFamily.InterNetwork)
                .Select(a => a.ToString())
                .ToList();
        }
        catch (NetworkInformationException)
        {
            // Adapter went away or is not queryable; keep whatever we have.
        }

        RegistrySettings reg = ReadRegistrySettings(nic.Id);

        // The registry holds the *configured* values, which are the only ones available
        // while the adapter is disconnected or disabled.
        dhcpEnabled ??= reg.EnableDhcp;
        if (address.Length == 0)
        {
            address = reg.Address;
        }

        if (mask.Length == 0)
        {
            mask = reg.Mask;
        }

        if (gateway.Length == 0)
        {
            gateway = reg.Gateway;
        }

        if (dns.Count == 0 && reg.DnsServers.Count > 0)
        {
            dns = reg.DnsServers.ToList();
        }

        return new AdapterInfo
        {
            Name = nic.Name,
            Description = nic.Description,
            Id = nic.Id,
            InterfaceIndex = index,
            Status = nic.OperationalStatus,
            InterfaceType = nic.NetworkInterfaceType,
            MacAddress = FormatMac(nic.GetPhysicalAddress()),
            IsDhcpEnabled = dhcpEnabled,
            IPv4Address = address,
            SubnetMask = mask,
            Gateway = gateway,
            DnsServers = dns,
            IsDnsStatic = reg.HasStaticDns,
            DhcpServer = dhcpServer,
        };
    }

    private static string FormatMac(PhysicalAddress physical)
    {
        byte[] bytes = physical.GetAddressBytes();
        return bytes.Length == 0 ? "" : string.Join("-", bytes.Select(b => b.ToString("X2")));
    }

    private readonly record struct RegistrySettings(
        bool? EnableDhcp,
        string Address,
        string Mask,
        string Gateway,
        IReadOnlyList<string> DnsServers,
        bool? HasStaticDns);

    private static RegistrySettings ReadRegistrySettings(string adapterId)
    {
        if (string.IsNullOrWhiteSpace(adapterId))
        {
            return new RegistrySettings(null, "", "", "", Array.Empty<string>(), null);
        }

        try
        {
            using RegistryKey? key = Registry.LocalMachine.OpenSubKey($@"{InterfacesKeyPath}\{adapterId}");
            if (key is null)
            {
                return new RegistrySettings(null, "", "", "", Array.Empty<string>(), null);
            }

            bool? dhcp = key.GetValue("EnableDHCP") is int flag ? flag != 0 : null;
            bool isDhcp = dhcp == true;

            string address = FirstAddress(key, isDhcp ? "DhcpIPAddress" : "IPAddress");
            string mask = FirstAddress(key, isDhcp ? "DhcpSubnetMask" : "SubnetMask");
            string gateway = FirstAddress(key, isDhcp ? "DhcpDefaultGateway" : "DefaultGateway");

            // NameServer holds manually entered servers (comma separated); DhcpNameServer
            // holds what the DHCP server handed out (space separated).
            string nameServers = key.GetValue("NameServer") as string ?? "";
            bool hasStaticDns = nameServers.Trim().Length > 0;
            if (!hasStaticDns)
            {
                nameServers = key.GetValue("DhcpNameServer") as string ?? "";
            }

            List<string> dns = nameServers
                .Split(new[] { ',', ' ', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();

            return new RegistrySettings(dhcp, address, mask, gateway, dns, hasStaticDns);
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            return new RegistrySettings(null, "", "", "", Array.Empty<string>(), null);
        }
    }

    private static string FirstAddress(RegistryKey key, string valueName)
    {
        object? value = key.GetValue(valueName);
        IEnumerable<string> candidates = value switch
        {
            string[] many => many,
            string one => new[] { one },
            _ => Array.Empty<string>(),
        };

        return candidates.FirstOrDefault(s => !string.IsNullOrWhiteSpace(s) && s.Trim() != "0.0.0.0")?.Trim() ?? "";
    }
}
