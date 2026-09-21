using System.Diagnostics;
using System.Net;
using System.Security.Principal;
using System.Text;

namespace NetIPConfig;

public partial class MainForm : Form
{
    private List<AdapterInfo> _adapters = new();
    private AppSettings _settings = new();

    /// <summary>Set while the code is filling controls, so handlers do not fight the update.</summary>
    private bool _updatingControls;

    public MainForm()
    {
        InitializeComponent();

        cmbAdapters.DisplayMember = nameof(AdapterInfo.DisplayText);
        cmbProfiles.DisplayMember = nameof(IPv4Profile.DisplayText);
        cmbAdapters.SelectedIndexChanged += (_, _) => OnAdapterSelected();
        btnRefresh.Click += (_, _) => LoadAdapters();
        btnLoadCurrent.Click += (_, _) => PopulateFields(logAction: true);
        btnScan.Click += (_, _) => OpenScanDialog();
        btnApply.Click += OnApplyClicked;
        btnClose.Click += (_, _) => Close();

        rbDhcp.CheckedChanged += (_, _) => OnAddressModeChanged();
        rbStatic.CheckedChanged += (_, _) => OnAddressModeChanged();
        rbDnsAutomatic.CheckedChanged += (_, _) => UpdateEnabledState();
        rbDnsManual.CheckedChanged += (_, _) => UpdateEnabledState();

        btnTheme.Click += (_, _) => ToggleTheme();
        cmbProfiles.SelectedIndexChanged += (_, _) => OnProfileSelected();
        btnProfileSave.Click += (_, _) => SaveCurrentAsProfile();
        btnProfileDelete.Click += (_, _) => DeleteSelectedProfile();

        txtAddress.Leave += (_, _) => SuggestMaskForAddress();
        txtMask.Leave += (_, _) => ExpandPrefixLengthShorthand();
        lnkRestartElevated.Click += (_, _) => RestartElevated();
    }

    private AdapterInfo? SelectedAdapter => cmbAdapters.SelectedItem as AdapterInfo;

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        _settings = SettingsStore.Load();
        ApplyTheme();
        RefreshProfileList();
        ShowElevationState();
        LoadAdapters();
    }

    // ---------------------------------------------------------------- adapters

    private void LoadAdapters()
    {
        string? previouslySelected = SelectedAdapter?.Name;

        try
        {
            _adapters = NetworkQuery.GetAdapters();
        }
        catch (Exception ex)
        {
            Log($"Could not read the adapter list: {ex.Message}");
            return;
        }

        _updatingControls = true;
        cmbAdapters.BeginUpdate();
        cmbAdapters.Items.Clear();
        foreach (AdapterInfo adapter in _adapters)
        {
            cmbAdapters.Items.Add(adapter);
        }

        cmbAdapters.EndUpdate();

        if (_adapters.Count > 0)
        {
            int index = previouslySelected is null
                ? 0
                : _adapters.FindIndex(a => string.Equals(a.Name, previouslySelected, StringComparison.Ordinal));
            cmbAdapters.SelectedIndex = index < 0 ? 0 : index;
        }

        _updatingControls = false;

        if (_adapters.Count == 0)
        {
            Log("No configurable network adapters were found.");
            ShowCurrentConfiguration(null);
            UpdateEnabledState();
            return;
        }

        ShowCurrentConfiguration(SelectedAdapter);
        PopulateFields(logAction: false);
        Log($"Found {_adapters.Count} adapter(s).");
    }

    private void OnAdapterSelected()
    {
        if (_updatingControls)
        {
            return;
        }

        ShowCurrentConfiguration(SelectedAdapter);
        PopulateFields(logAction: false);
    }

    private void ShowCurrentConfiguration(AdapterInfo? adapter)
    {
        if (adapter is null)
        {
            foreach (Label label in new[]
                     {
                         lblCurStatusValue, lblCurSourceValue, lblCurMacValue, lblCurAddressValue,
                         lblCurMaskValue, lblCurGatewayValue, lblCurDnsValue,
                     })
            {
                label.Text = "—";
            }

            return;
        }

        lblCurStatusValue.Text = adapter.StatusText;
        lblCurSourceValue.Text = adapter.DhcpServer.Length > 0
            ? $"{adapter.SourceText} — server {adapter.DhcpServer}"
            : adapter.SourceText;
        lblCurMacValue.Text = Or(adapter.MacAddress);
        lblCurAddressValue.Text = Or(adapter.IPv4Address);
        lblCurMaskValue.Text = Or(adapter.SubnetMask);
        lblCurGatewayValue.Text = Or(adapter.Gateway);
        lblCurDnsValue.Text = Or(adapter.DnsText);

        static string Or(string value) => value.Length > 0 ? value : "—";
    }

    /// <summary>Copies the selected adapter's settings into the editable fields.</summary>
    private void PopulateFields(bool logAction)
    {
        AdapterInfo? adapter = SelectedAdapter;
        if (adapter is null)
        {
            return;
        }

        _updatingControls = true;

        // An adapter whose mode cannot be determined is treated as automatic, which is
        // the non-destructive assumption: nothing is pre-filled as a manual change.
        bool automatic = adapter.IsDhcpEnabled != false;
        rbDhcp.Checked = automatic;
        rbStatic.Checked = !automatic;

        txtAddress.Text = adapter.IPv4Address;
        txtMask.Text = adapter.SubnetMask;
        txtGateway.Text = adapter.Gateway;

        bool dnsManual = adapter.IsDnsStatic == true;
        rbDnsAutomatic.Checked = !dnsManual;
        rbDnsManual.Checked = dnsManual;

        txtPreferredDns.Text = adapter.DnsServers.Count > 0 ? adapter.DnsServers[0] : "";
        txtAlternateDns.Text = adapter.DnsServers.Count > 1 ? adapter.DnsServers[1] : "";

        // The fields no longer reflect a profile, so do not leave one looking selected.
        cmbProfiles.SelectedIndex = -1;

        _updatingControls = false;
        btnProfileDelete.Enabled = false;
        UpdateEnabledState();

        if (logAction)
        {
            Log($"Loaded the current settings for \"{adapter.Name}\".");
        }
    }

    // ---------------------------------------------------------------- form state

    private void OnAddressModeChanged()
    {
        if (_updatingControls)
        {
            return;
        }

        UpdateEnabledState();

        // Switching to manual with empty boxes: start from what the adapter has now.
        if (rbStatic.Checked && txtAddress.Text.Trim().Length == 0)
        {
            AdapterInfo? adapter = SelectedAdapter;
            if (adapter is not null)
            {
                txtAddress.Text = adapter.IPv4Address;
                txtMask.Text = adapter.SubnetMask;
                txtGateway.Text = adapter.Gateway;
            }
        }
    }

    private void UpdateEnabledState()
    {
        bool manualAddress = rbStatic.Checked;
        foreach (Control control in new Control[] { lblAddress, txtAddress, lblMask, txtMask, lblGateway, txtGateway, lblGatewayHint })
        {
            control.Enabled = manualAddress;
        }

        bool manualDns = rbDnsManual.Checked;
        foreach (Control control in new Control[] { lblPreferredDns, txtPreferredDns, lblAlternateDns, txtAlternateDns })
        {
            control.Enabled = manualDns;
        }

        bool hasAdapter = SelectedAdapter is not null;
        grpAddress.Enabled = hasAdapter;
        grpDns.Enabled = hasAdapter;
        btnApply.Enabled = hasAdapter;
        btnLoadCurrent.Enabled = hasAdapter;
        btnScan.Enabled = TryGetScanSubnet(out _, out _, out _);
    }

    private void SetBusy(bool busy)
    {
        cmbAdapters.Enabled = !busy;
        btnRefresh.Enabled = !busy;
        btnLoadCurrent.Enabled = !busy;
        btnScan.Enabled = !busy;
        btnApply.Enabled = !busy;
        grpAddress.Enabled = !busy;
        grpDns.Enabled = !busy;
        grpProfiles.Enabled = !busy;
        UseWaitCursor = busy;

        if (!busy)
        {
            UpdateEnabledState();
        }
    }

    /// <summary>Accepts "24" in the mask box and turns it into 255.255.255.0.</summary>
    private void ExpandPrefixLengthShorthand()
    {
        string text = txtMask.Text.Trim();
        if (text.Length is 0 or > 2 || !int.TryParse(text, out int prefixLength))
        {
            return;
        }

        if (prefixLength is >= 1 and <= 32)
        {
            txtMask.Text = IPv4Text.MaskFromPrefixLength(prefixLength).ToString();
        }
    }

    /// <summary>Fills an empty mask box with the address class default once an address is typed.</summary>
    private void SuggestMaskForAddress()
    {
        if (txtMask.Text.Trim().Length > 0 || !IPv4Text.TryParse(txtAddress.Text, out IPAddress address))
        {
            return;
        }

        txtMask.Text = IPv4Text.ClassfulMaskFor(address).ToString();
    }

    // ---------------------------------------------------------------- apply

    private async void OnApplyClicked(object? sender, EventArgs e)
    {
        IPv4ChangeRequest? request = BuildValidatedRequest();
        if (request is null || !Confirm(request))
        {
            return;
        }

        SetBusy(true);
        try
        {
            Log($"Applying IPv4 settings to \"{request.AdapterName}\"...");
            List<CommandResult> results = await Task.Run(() => NetworkConfigurator.Apply(request));

            foreach (CommandResult result in results)
            {
                Log($"> {result.CommandLine}");
                foreach (string line in result.Output.Split('\n'))
                {
                    string trimmed = line.Trim();
                    if (trimmed.Length > 0)
                    {
                        Log($"    {trimmed}");
                    }
                }

                if (result.Tolerated)
                {
                    Log("    (already configured that way; treated as applied)");
                }
                else if (!result.Success)
                {
                    Log($"    netsh exit code {result.ExitCode}");
                }
            }

            CommandResult? failure = results.FirstOrDefault(r => !r.Success);
            if (failure is null)
            {
                Log("Settings applied.");
                MessageBox.Show(
                    this,
                    $"The IPv4 settings for \"{request.AdapterName}\" were applied.",
                    "Settings applied",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            else
            {
                Log("Settings were not fully applied.");
                MessageBox.Show(
                    this,
                    $"netsh could not complete the change.{Environment.NewLine}{Environment.NewLine}"
                    + $"{failure.CommandLine}{Environment.NewLine}{Environment.NewLine}"
                    + (failure.Output.Length > 0 ? failure.Output : $"Exit code {failure.ExitCode}."),
                    "Change failed",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }
        finally
        {
            SetBusy(false);
        }

        LoadAdapters();
    }

    private bool Confirm(IPv4ChangeRequest request)
    {
        StringBuilder summary = new();
        summary.AppendLine($"Apply these IPv4 settings to \"{request.AdapterName}\"?");
        summary.AppendLine();

        if (request.UseDhcp)
        {
            summary.AppendLine("IP address:        obtain automatically (DHCP)");
        }
        else
        {
            summary.AppendLine($"IP address:        {request.Address}");
            summary.AppendLine($"Subnet mask:       {request.SubnetMask}");
            summary.AppendLine($"Default gateway:   {(request.Gateway.Length > 0 ? request.Gateway : "(none)")}");
        }

        if (request.DnsAutomatic)
        {
            summary.AppendLine("DNS servers:       obtain automatically");
        }
        else if (request.PreferredDns.Length == 0)
        {
            summary.AppendLine("DNS servers:       (cleared)");
        }
        else
        {
            summary.AppendLine($"Preferred DNS:     {request.PreferredDns}");
            if (request.AlternateDns.Length > 0)
            {
                summary.AppendLine($"Alternate DNS:     {request.AlternateDns}");
            }
        }

        summary.AppendLine();
        summary.Append("Network connections on this adapter will drop briefly while the change is applied.");

        return MessageBox.Show(
            this,
            summary.ToString(),
            "Confirm IPv4 change",
            MessageBoxButtons.OKCancel,
            MessageBoxIcon.Question,
            MessageBoxDefaultButton.Button1) == DialogResult.OK;
    }

    /// <summary>Validates the form and returns the change to apply, or null if the user must fix something.</summary>
    private IPv4ChangeRequest? BuildValidatedRequest()
    {
        AdapterInfo? adapter = SelectedAdapter;
        if (adapter is null)
        {
            Complain("Select a network adapter first.", null);
            return null;
        }

        // Pressing Enter applies without the boxes ever losing focus, so run the same
        // normalisation the Leave handlers do before reading the values.
        ExpandPrefixLengthShorthand();
        SuggestMaskForAddress();

        bool useDhcp = rbDhcp.Checked;
        string address = txtAddress.Text.Trim();
        string mask = txtMask.Text.Trim();
        string gateway = txtGateway.Text.Trim();
        string preferredDns = txtPreferredDns.Text.Trim();
        string alternateDns = txtAlternateDns.Text.Trim();

        if (!useDhcp)
        {
            if (!IPv4Text.TryParse(address, out IPAddress ip))
            {
                Complain("Enter the IP address as four numbers separated by dots, for example 192.168.1.50.", txtAddress);
                return null;
            }

            if (!IPv4Text.IsAssignableHostAddress(ip, out string reason))
            {
                Complain($"{address} cannot be assigned to an adapter: {reason}.", txtAddress);
                return null;
            }

            if (!IPv4Text.TryParse(mask, out IPAddress maskAddress))
            {
                Complain("Enter the subnet mask as four numbers separated by dots, for example 255.255.255.0."
                         + Environment.NewLine + Environment.NewLine
                         + "A prefix length such as 24 also works.", txtMask);
                return null;
            }

            if (!IPv4Text.IsValidMask(maskAddress, out int prefixLength))
            {
                Complain($"{mask} is not a valid subnet mask. A mask must be an unbroken run of 1 bits, "
                         + "such as 255.255.255.0 or 255.255.240.0.", txtMask);
                return null;
            }

            // /31 and /32 have no network or broadcast address to collide with.
            if (prefixLength <= 30)
            {
                if (IPv4Text.IsNetworkAddress(ip, maskAddress))
                {
                    Complain($"{address} is the network address of {mask} and cannot be used as a host address.", txtAddress);
                    return null;
                }

                if (IPv4Text.IsBroadcastAddress(ip, maskAddress))
                {
                    Complain($"{address} is the broadcast address of {mask} and cannot be used as a host address.", txtAddress);
                    return null;
                }
            }

            if (gateway.Length > 0)
            {
                if (!IPv4Text.TryParse(gateway, out IPAddress gatewayAddress))
                {
                    Complain("Enter the default gateway as four numbers separated by dots, or leave it blank "
                             + "if this adapter should not have one.", txtGateway);
                    return null;
                }

                if (gatewayAddress.Equals(ip))
                {
                    Complain("The default gateway cannot be the same as this adapter's IP address.", txtGateway);
                    return null;
                }

                // Off-subnet gateways are unusual but legal, so ask instead of refusing.
                if (!IPv4Text.SameSubnet(ip, gatewayAddress, maskAddress)
                    && MessageBox.Show(
                        this,
                        $"The gateway {gateway} is outside the subnet defined by {address} / {mask}."
                        + Environment.NewLine + Environment.NewLine
                        + "Windows will accept it, but it will usually be unreachable. Continue?",
                        "Gateway outside the subnet",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Warning,
                        MessageBoxDefaultButton.Button2) != DialogResult.Yes)
                {
                    txtGateway.Focus();
                    return null;
                }
            }
        }

        bool dnsAutomatic = rbDnsAutomatic.Checked;
        if (!dnsAutomatic)
        {
            if (preferredDns.Length == 0 && alternateDns.Length > 0)
            {
                Complain("Fill in the preferred DNS server before the alternate one.", txtPreferredDns);
                return null;
            }

            if (preferredDns.Length > 0 && !IPv4Text.TryParse(preferredDns, out _))
            {
                Complain("Enter the preferred DNS server as four numbers separated by dots, for example 9.9.9.9.", txtPreferredDns);
                return null;
            }

            if (alternateDns.Length > 0 && !IPv4Text.TryParse(alternateDns, out _))
            {
                Complain("Enter the alternate DNS server as four numbers separated by dots, or leave it blank.", txtAlternateDns);
                return null;
            }

            if (alternateDns.Length > 0 && string.Equals(preferredDns, alternateDns, StringComparison.Ordinal))
            {
                Complain("The alternate DNS server must differ from the preferred one.", txtAlternateDns);
                return null;
            }
        }

        return new IPv4ChangeRequest
        {
            AdapterName = adapter.Name,
            UseDhcp = useDhcp,
            Address = address,
            SubnetMask = mask,
            Gateway = gateway,
            DnsAutomatic = dnsAutomatic,
            PreferredDns = preferredDns,
            AlternateDns = alternateDns,
        };
    }

    private void Complain(string message, Control? focus)
    {
        MessageBox.Show(this, message, "Check the settings", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        focus?.Focus();
    }

    // ---------------------------------------------------------------- scanning

    /// <summary>The subnet to scan, taken from the selected adapter's own address and mask.</summary>
    private bool TryGetScanSubnet(out IPAddress address, out IPAddress mask, out int prefixLength)
    {
        address = IPAddress.None;
        mask = IPAddress.None;
        prefixLength = 0;

        AdapterInfo? adapter = SelectedAdapter;
        return adapter is not null
               && IPv4Text.TryParse(adapter.IPv4Address, out address)
               && IPv4Text.TryParse(adapter.SubnetMask, out mask)
               && IPv4Text.IsValidMask(mask, out prefixLength);
    }

    private void OpenScanDialog()
    {
        AdapterInfo? adapter = SelectedAdapter;
        if (adapter is null)
        {
            return;
        }

        if (!TryGetScanSubnet(out IPAddress address, out IPAddress mask, out int prefixLength))
        {
            Complain(
                "This adapter has no usable IPv4 address and subnet mask yet, so there is no subnet "
                + "to scan. Connect it, or give it a manual address first.",
                null);
            return;
        }

        long hostCount = NetworkScanner.CountHosts(prefixLength);
        if (hostCount > NetworkScanner.MaxHosts
            && MessageBox.Show(
                this,
                $"A /{prefixLength} subnet holds {hostCount:N0} addresses. Only the first "
                + $"{NetworkScanner.MaxHosts:N0} will be probed, and that still takes a few minutes."
                + Environment.NewLine + Environment.NewLine
                + "Continue?",
                "Large subnet",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2) != DialogResult.Yes)
        {
            return;
        }

        Log($"Scanning the subnet of {address} / {mask} on \"{adapter.Name}\"...");

        using ScanForm scan = new(adapter, address, mask, CurrentTheme);
        scan.ShowDialog(this);
    }

    // ---------------------------------------------------------------- theme

    private AppTheme CurrentTheme => _settings.DarkMode ? AppTheme.Dark : AppTheme.Light;

    private void ToggleTheme()
    {
        _settings.DarkMode = !_settings.DarkMode;
        ApplyTheme();
        PersistSettings();
        Log(_settings.DarkMode ? "Switched to dark mode." : "Switched to light mode.");
    }

    private void ApplyTheme()
    {
        Theme.Apply(this, CurrentTheme);
        btnTheme.Text = _settings.DarkMode ? "&Light mode" : "Dark &mode";
    }

    // ---------------------------------------------------------------- profiles

    private IPv4Profile? SelectedProfile => cmbProfiles.SelectedItem as IPv4Profile;

    private void RefreshProfileList(string? nameToSelect = null)
    {
        _updatingControls = true;
        cmbProfiles.BeginUpdate();
        cmbProfiles.Items.Clear();

        foreach (IPv4Profile profile in _settings.Profiles
                     .OrderBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            cmbProfiles.Items.Add(profile);
        }

        cmbProfiles.EndUpdate();

        if (nameToSelect is not null)
        {
            for (int i = 0; i < cmbProfiles.Items.Count; i++)
            {
                if (cmbProfiles.Items[i] is IPv4Profile candidate
                    && string.Equals(candidate.Name, nameToSelect, StringComparison.CurrentCultureIgnoreCase))
                {
                    cmbProfiles.SelectedIndex = i;
                    break;
                }
            }
        }

        _updatingControls = false;

        bool hasProfiles = cmbProfiles.Items.Count > 0;
        cmbProfiles.Enabled = hasProfiles;
        btnProfileDelete.Enabled = cmbProfiles.SelectedItem is not null;
    }

    private void OnProfileSelected()
    {
        if (_updatingControls)
        {
            return;
        }

        IPv4Profile? profile = SelectedProfile;
        if (profile is null)
        {
            return;
        }

        LoadProfileIntoFields(profile);
        btnProfileDelete.Enabled = true;
    }

    /// <summary>
    /// Fills the form from a profile. Nothing is sent to the adapter until Apply is pressed,
    /// so recalling a profile is always safe.
    /// </summary>
    private void LoadProfileIntoFields(IPv4Profile profile)
    {
        _updatingControls = true;

        rbDhcp.Checked = profile.UseDhcp;
        rbStatic.Checked = !profile.UseDhcp;
        txtAddress.Text = profile.Address;
        txtMask.Text = profile.SubnetMask;
        txtGateway.Text = profile.Gateway;

        rbDnsAutomatic.Checked = profile.DnsAutomatic;
        rbDnsManual.Checked = !profile.DnsAutomatic;
        txtPreferredDns.Text = profile.PreferredDns;
        txtAlternateDns.Text = profile.AlternateDns;

        _updatingControls = false;
        UpdateEnabledState();

        string target = SelectedAdapter?.Name ?? "the selected adapter";
        Log($"Profile \"{profile.Name}\" loaded — press Apply to set it on \"{target}\".");
    }

    private void SaveCurrentAsProfile()
    {
        string? name = TextPromptDialog.Ask(
            this,
            "Save profile",
            "Name for this set of IPv4 settings:",
            SelectedProfile?.Name ?? "",
            CurrentTheme);

        if (name is null)
        {
            return;
        }

        IPv4Profile? existing = _settings.Profiles
            .FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.CurrentCultureIgnoreCase));

        if (existing is not null)
        {
            if (MessageBox.Show(
                    this,
                    $"A profile named \"{existing.Name}\" already exists. Replace it?",
                    "Replace profile",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question,
                    MessageBoxDefaultButton.Button2) != DialogResult.Yes)
            {
                return;
            }

            _settings.Profiles.Remove(existing);
        }

        IPv4Profile profile = CaptureFieldsAsProfile(name);
        _settings.Profiles.Add(profile);
        PersistSettings();
        RefreshProfileList(profile.Name);
        Log($"Saved profile \"{profile.Name}\".");
    }

    private IPv4Profile CaptureFieldsAsProfile(string name) => new()
    {
        Name = name,
        UseDhcp = rbDhcp.Checked,
        Address = txtAddress.Text.Trim(),
        SubnetMask = txtMask.Text.Trim(),
        Gateway = txtGateway.Text.Trim(),
        DnsAutomatic = rbDnsAutomatic.Checked,
        PreferredDns = txtPreferredDns.Text.Trim(),
        AlternateDns = txtAlternateDns.Text.Trim(),
    };

    private void DeleteSelectedProfile()
    {
        IPv4Profile? profile = SelectedProfile;
        if (profile is null)
        {
            return;
        }

        if (MessageBox.Show(
                this,
                $"Delete the profile \"{profile.Name}\"?",
                "Delete profile",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2) != DialogResult.Yes)
        {
            return;
        }

        _settings.Profiles.Remove(profile);
        PersistSettings();
        RefreshProfileList();
        Log($"Deleted profile \"{profile.Name}\".");
    }

    private void PersistSettings()
    {
        string? error = SettingsStore.Save(_settings);
        if (error is not null)
        {
            Log($"Could not save settings to {SettingsStore.FilePath}: {error}");
        }
    }

    // ---------------------------------------------------------------- elevation and logging

    private void ShowElevationState()
    {
        if (IsElevated())
        {
            lblElevation.Text = "Running as administrator.";
            lnkRestartElevated.Visible = false;
            return;
        }

        lblElevation.Text = "Not running as administrator — applying changes will fail.";
        lnkRestartElevated.Visible = true;
        Log("This process is not elevated. Restart it as administrator before applying changes.");
    }

    private static bool IsElevated()
    {
        try
        {
            using WindowsIdentity identity = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch (Exception)
        {
            return false;
        }
    }

    private void RestartElevated()
    {
        string? executable = Environment.ProcessPath;
        if (executable is null)
        {
            Log("Could not determine this program's path; start it again manually as administrator.");
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(executable)
            {
                UseShellExecute = true,
                Verb = "runas",
            });
            Close();
        }
        catch (Exception ex)
        {
            // Most often the UAC prompt was dismissed.
            Log($"Could not restart elevated: {ex.Message}");
        }
    }

    private void Log(string message)
    {
        txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
    }
}
