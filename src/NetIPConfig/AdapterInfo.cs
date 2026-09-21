using System.Net.NetworkInformation;

namespace NetIPConfig;

/// <summary>A snapshot of one network adapter's IPv4 configuration.</summary>
internal sealed class AdapterInfo
{
    /// <summary>Connection name as shown in Network Connections, e.g. "Ethernet". This is what netsh takes.</summary>
    public string Name { get; init; } = "";

    /// <summary>Hardware description, e.g. "Intel(R) Ethernet Connection I219-V".</summary>
    public string Description { get; init; } = "";

    /// <summary>Adapter GUID, used to look up the configured settings in the registry.</summary>
    public string Id { get; init; } = "";

    public int? InterfaceIndex { get; init; }

    public OperationalStatus Status { get; init; }

    public NetworkInterfaceType InterfaceType { get; init; }

    public string MacAddress { get; init; } = "";

    /// <summary>True for DHCP, false for a manual address, null when it could not be determined.</summary>
    public bool? IsDhcpEnabled { get; init; }

    public string IPv4Address { get; init; } = "";

    public string SubnetMask { get; init; } = "";

    public string Gateway { get; init; } = "";

    public IReadOnlyList<string> DnsServers { get; init; } = Array.Empty<string>();

    /// <summary>True when DNS servers are entered manually, false when they come from DHCP.</summary>
    public bool? IsDnsStatic { get; init; }

    public string DhcpServer { get; init; } = "";

    public string StatusText => Status switch
    {
        OperationalStatus.Up => "connected",
        OperationalStatus.Down => "disconnected",
        OperationalStatus.Dormant => "dormant",
        OperationalStatus.NotPresent => "not present",
        OperationalStatus.LowerLayerDown => "media disconnected",
        _ => Status.ToString().ToLowerInvariant(),
    };

    public string SourceText => IsDhcpEnabled switch
    {
        true => "DHCP (automatic)",
        false => "Manual (static)",
        _ => "unknown",
    };

    public string DnsText => DnsServers.Count == 0 ? "" : string.Join(", ", DnsServers);

    public string DisplayText => $"{Name}  —  {Description}  ({StatusText})";

    public override string ToString() => DisplayText;
}
