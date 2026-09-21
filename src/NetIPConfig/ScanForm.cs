using System.Globalization;
using System.Net;
using System.Text;

namespace NetIPConfig;

/// <summary>Runs a subnet scan and shows what answered.</summary>
internal sealed class ScanForm : Form
{
    private readonly AdapterInfo _adapter;
    private readonly List<IPAddress> _hosts;
    private readonly List<ScanHit> _hits = new();
    private readonly bool _dark;

    private readonly Label _range;
    private readonly ListView _results;
    private readonly ProgressBar _progress;
    private readonly Label _status;
    private readonly Button _startStop;
    private readonly Button _export;

    private CancellationTokenSource? _cancellation;
    private bool _scanning;

    public ScanForm(AdapterInfo adapter, IPAddress address, IPAddress mask, AppTheme theme)
    {
        _adapter = adapter;
        _hosts = NetworkScanner.EnumerateHosts(address, mask);
        _dark = theme == AppTheme.Dark;

        _range = new Label
        {
            AutoEllipsis = true,
            Dock = DockStyle.Top,
            Height = 28,
            Padding = new Padding(12, 9, 12, 0),
            Text = DescribeRange(address, mask),
            TextAlign = ContentAlignment.TopLeft,
        };

        Label hint = new()
        {
            AutoEllipsis = true,
            Dock = DockStyle.Top,
            Height = 34,
            Padding = new Padding(12, 3, 12, 12),
            Tag = Theme.DimTag,
            Text = "Each address is pinged and ARP-resolved, so devices that ignore pings still show up.",
            TextAlign = ContentAlignment.TopLeft,
        };

        _results = new ListView
        {
            Dock = DockStyle.Fill,
            FullRowSelect = true,
            GridLines = true,
            HideSelection = false,
            UseCompatibleStateImageBehavior = false,
            View = View.Details,
        };

        _results.Columns.Add("IP address", 115);
        _results.Columns.Add("MAC address", 165);
        _results.Columns.Add("Device name", 215);
        _results.Columns.Add("Ping", 60, HorizontalAlignment.Right);
        _results.Columns.Add("Notes", 110);
        _results.ListViewItemSorter = new AddressComparer();
        ApplyDarkHeader();

        _progress = new ProgressBar
        {
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            Location = new Point(12, 12),
            Size = new Size(736, 18),
        };

        _status = new Label
        {
            // Stretches with the window so it cannot slide under the buttons when narrowed.
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            AutoEllipsis = true,
            Location = new Point(12, 46),
            Size = new Size(400, 20),
            Text = "",
        };

        _startStop = new Button
        {
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(430, 40),
            Size = new Size(104, 28),
            Text = "&Stop",
            UseVisualStyleBackColor = true,
        };

        _export = new Button
        {
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Enabled = false,
            Location = new Point(542, 40),
            Size = new Size(110, 28),
            Text = "&Export CSV...",
            UseVisualStyleBackColor = true,
        };

        Button close = new()
        {
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            DialogResult = DialogResult.Cancel,
            Location = new Point(660, 40),
            Size = new Size(88, 28),
            Text = "&Close",
            UseVisualStyleBackColor = true,
        };

        Panel bottom = new()
        {
            Dock = DockStyle.Bottom,
            // Sized to the client width up front: Anchor offsets are captured relative to the
            // parent's size, so children added to a default 200px panel get placed wrongly
            // once it stretches.
            Size = new Size(760, 80),
        };
        bottom.Controls.Add(_progress);
        bottom.Controls.Add(_status);
        bottom.Controls.Add(_startStop);
        bottom.Controls.Add(_export);
        bottom.Controls.Add(close);

        _startStop.Click += (_, _) => ToggleScan();
        _export.Click += (_, _) => ExportCsv();

        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        CancelButton = close;
        ClientSize = new Size(760, 480);
        MinimumSize = new Size(600, 360);
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        Text = $"Scan network — {adapter.Name}";

        // Docking is applied from the last control backwards, so the filling list goes first.
        Controls.Add(_results);
        Controls.Add(bottom);
        Controls.Add(hint);
        Controls.Add(_range);

        Theme.Apply(this, theme);
    }

    private string DescribeRange(IPAddress address, IPAddress mask)
    {
        if (_hosts.Count == 0)
        {
            return "This adapter has no usable subnet to scan.";
        }

        string first = _hosts[0].ToString();
        string last = _hosts[^1].ToString();
        string capped = NetworkScanner.CountHosts(
            IPv4Text.IsValidMask(mask, out int prefixLength) ? prefixLength : 32) > _hosts.Count
            ? $" (capped at {NetworkScanner.MaxHosts})"
            : "";

        return $"Subnet of {address} / {mask} on \"{_adapter.Name}\": "
               + $"{first} to {last}, {_hosts.Count} addresses{capped}.";
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);

        if (_hosts.Count == 0)
        {
            _startStop.Enabled = false;
            SetStatus("Nothing to scan.");
            return;
        }

        StartScan();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _cancellation?.Cancel();
        base.OnFormClosing(e);
    }

    private void ToggleScan()
    {
        if (_scanning)
        {
            _cancellation?.Cancel();
            SetStatus("Stopping...");
            _startStop.Enabled = false;
            return;
        }

        StartScan();
    }

    private async void StartScan()
    {
        if (_scanning)
        {
            return;
        }

        _scanning = true;
        _hits.Clear();
        _results.Items.Clear();

        CancellationTokenSource cancellation = new();
        _cancellation = cancellation;

        _progress.Maximum = Math.Max(_hosts.Count, 1);
        _progress.Value = 0;
        _startStop.Text = "&Stop";
        _startStop.Enabled = true;
        _export.Enabled = false;
        SetStatus($"Scanning {_hosts.Count} addresses...");

        // Created on the UI thread, so both callbacks come back on the UI thread.
        Progress<ScanHit> onHit = new(AddHit);
        Progress<int> onProgress = new(done =>
        {
            if (IsDisposed || Disposing)
            {
                return;
            }

            _progress.Value = Math.Min(done, _progress.Maximum);
            SetStatus($"Probed {done} of {_hosts.Count} — {_hits.Count} device(s) found.");
        });

        try
        {
            await NetworkScanner.ScanAsync(_hosts, _adapter, onHit, onProgress, cancellation.Token);

            SetStatus(cancellation.IsCancellationRequested
                ? $"Stopped after {_hits.Count} device(s)."
                : $"Done — {_hits.Count} of {_hosts.Count} addresses answered.");
        }
        catch (OperationCanceledException)
        {
            SetStatus($"Stopped after {_hits.Count} device(s).");
        }
        catch (Exception ex)
        {
            SetStatus($"Scan failed: {ex.Message}");
        }
        finally
        {
            _scanning = false;
            _cancellation = null;
            cancellation.Dispose();

            if (!IsDisposed && !Disposing)
            {
                _startStop.Text = "&Rescan";
                _startStop.Enabled = _hosts.Count > 0;
                _export.Enabled = _hits.Count > 0;
                _progress.Value = _progress.Maximum;
                _results.Sort();
            }
        }
    }

    private void AddHit(ScanHit hit)
    {
        if (IsDisposed || Disposing)
        {
            return;
        }

        _hits.Add(hit);

        ListViewItem item = new(hit.Address.ToString())
        {
            Tag = IPv4Text.ToUInt32(hit.Address),
        };

        item.SubItems.Add(hit.MacAddress.Length > 0 ? hit.MacAddress : "—");
        item.SubItems.Add(hit.HostName.Length > 0 ? hit.HostName : "—");
        item.SubItems.Add(hit.RoundTripMs is { } milliseconds ? $"{milliseconds} ms" : "no reply");
        item.SubItems.Add(hit.Note);

        _results.Items.Add(item);
    }

    private void SetStatus(string text)
    {
        if (!IsDisposed && !Disposing)
        {
            _status.Text = text;
        }
    }

    private void ExportCsv()
    {
        using SaveFileDialog dialog = new()
        {
            AddExtension = true,
            DefaultExt = "csv",
            FileName = $"network-scan-{DateTime.Now:yyyy-MM-dd-HHmm}.csv",
            Filter = "CSV file (*.csv)|*.csv|All files (*.*)|*.*",
            Title = "Export scan results",
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        try
        {
            StringBuilder csv = new();
            csv.AppendLine("IP address,MAC address,Device name,Ping (ms),Notes");

            foreach (ScanHit hit in _hits.OrderBy(h => IPv4Text.ToUInt32(h.Address)))
            {
                csv.AppendLine(string.Join(",", new[]
                {
                    hit.Address.ToString(),
                    hit.MacAddress,
                    hit.HostName,
                    hit.RoundTripMs?.ToString(CultureInfo.InvariantCulture) ?? "",
                    hit.Note,
                }.Select(EscapeCsv)));
            }

            File.WriteAllText(dialog.FileName, csv.ToString());
            SetStatus($"Exported {_hits.Count} row(s) to {dialog.FileName}");
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Export failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static string EscapeCsv(string field) =>
        field.Contains(',') || field.Contains('"')
            ? $"\"{field.Replace("\"", "\"\"")}\""
            : field;

    /// <summary>
    /// ListView column headers are drawn by the OS and ignore BackColor, so in dark mode the
    /// header is drawn by hand while the rows keep their default rendering.
    /// </summary>
    private void ApplyDarkHeader()
    {
        if (!_dark)
        {
            return;
        }

        _results.OwnerDraw = true;

        _results.DrawColumnHeader += (_, e) =>
        {
            using SolidBrush background = new(Theme.ListHeaderBackground(dark: true));
            e.Graphics.FillRectangle(background, e.Bounds);

            Rectangle text = new(e.Bounds.X + 4, e.Bounds.Y, e.Bounds.Width - 8, e.Bounds.Height);
            TextRenderer.DrawText(
                e.Graphics,
                e.Header?.Text ?? "",
                _results.Font,
                text,
                Theme.ListHeaderText(dark: true),
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        };

        _results.DrawItem += (_, e) => e.DrawDefault = true;
        _results.DrawSubItem += (_, e) => e.DrawDefault = true;
    }

    /// <summary>Keeps the list in numeric address order rather than string order.</summary>
    private sealed class AddressComparer : System.Collections.IComparer
    {
        public int Compare(object? x, object? y) => Value(x).CompareTo(Value(y));

        private static uint Value(object? item) =>
            item is ListViewItem { Tag: uint value } ? value : 0u;
    }
}
