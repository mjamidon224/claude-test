namespace NetIPConfig;

/// <summary>Everything the app remembers between runs.</summary>
internal sealed class AppSettings
{
    public bool DarkMode { get; set; }

    public List<IPv4Profile> Profiles { get; set; } = new();
}

/// <summary>
/// A named set of IPv4 settings that can be recalled into the form. Profiles deliberately
/// do not record an adapter, so the same profile can be applied to whichever adapter is
/// selected (a laptop dock and a Wi-Fi card can share one "office static" profile).
/// </summary>
internal sealed class IPv4Profile
{
    public string Name { get; set; } = "";

    public bool UseDhcp { get; set; }

    public string Address { get; set; } = "";

    public string SubnetMask { get; set; } = "";

    public string Gateway { get; set; } = "";

    public bool DnsAutomatic { get; set; } = true;

    public string PreferredDns { get; set; } = "";

    public string AlternateDns { get; set; } = "";

    public string DisplayText
    {
        get
        {
            string address = UseDhcp ? "DHCP" : Address.Length > 0 ? Address : "manual";
            string dns = DnsAutomatic ? "automatic DNS" : PreferredDns.Length > 0 ? PreferredDns : "no DNS";
            return $"{Name}  —  {address}, {dns}";
        }
    }

    public override string ToString() => DisplayText;
}
