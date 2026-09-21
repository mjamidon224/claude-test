namespace NetIPConfig;

/// <summary>The settings the user asked for, already validated by the form.</summary>
internal sealed class IPv4ChangeRequest
{
    public required string AdapterName { get; init; }

    public required bool UseDhcp { get; init; }

    /// <summary>Ignored when <see cref="UseDhcp"/> is true.</summary>
    public string Address { get; init; } = "";

    public string SubnetMask { get; init; } = "";

    /// <summary>Empty clears the default gateway.</summary>
    public string Gateway { get; init; } = "";

    public required bool DnsAutomatic { get; init; }

    /// <summary>Empty with <see cref="DnsAutomatic"/> false clears the DNS server list.</summary>
    public string PreferredDns { get; init; } = "";

    public string AlternateDns { get; init; } = "";
}
