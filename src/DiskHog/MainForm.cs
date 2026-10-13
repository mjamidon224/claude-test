using System.ComponentModel;
using System.Diagnostics;
using System.Security.Principal;

namespace DiskHog;

/// <summary>
/// Pick a drive or folder, scan it, then drill down through folders sorted biggest first.
/// The whole UI is built in code; there is no designer file.
/// </summary>
internal sealed class MainForm : Form
{
    private const int BarColumn = 3;
    private const int BiggestFilesShown = 1000;

    private readonly ComboBox _targetBox = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly Button _browseButton = new() { Text = "Browse folder...", AutoSize = true };
    private readonly Button _scanButton = new() { Text = "Scan", AutoSize = true };
    private readonly Button _stopButton = new() { Text = "Stop", AutoSize = true, Enabled = false };
    private readonly Button _adminButton = new() { Text = "Restart as administrator", AutoSize = true };
    private readonly CheckBox _darkToggle = new() { Text = "Dark mode", AutoSize = true, Anchor = AnchorStyles.Left };
    private readonly RadioButton _foldersView = new() { Text = "Folders", AutoSize = true, Appearance = Appearance.Button, Checked = true };
    private readonly RadioButton _filesView = new() { Text = $"Biggest files (≥ {SizeText.Format(DiskScanner.BigFileThreshold)})", AutoSize = true, Appearance = Appearance.Button };
    private readonly Button _upButton = new() { Text = "↑ Up", AutoSize = true, Enabled = false };
    private readonly TextBox _pathBox = new() { ReadOnly = true, Dock = DockStyle.Fill };
    private readonly ListView _folderList = CreateList();
    private readonly ListView _fileList = CreateList();
    private readonly Label _statusLabel = new() { AutoSize = true, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
    private readonly ProgressBar _progress = new() { Style = ProgressBarStyle.Marquee, Visible = false, Dock = DockStyle.Fill };
    private readonly System.Windows.Forms.Timer _progressTimer = new() { Interval = 200 };

    private CancellationTokenSource? _cancellation;
    private DiskScanner? _scanner;
    private FolderNode? _current;
    private SortKey _sortKey = SortKey.Size;
    private Font? _italicFont;
    private Palette _palette = Theme.LoadDarkSetting() ? Palette.Dark : Palette.Light;

    public MainForm(string? initialPath)
    {
        Text = "DiskHog — what's eating my disk";
        StartPosition = FormStartPosition.CenterScreen;
        Size = new Size(LogicalToDeviceUnits(1000), LogicalToDeviceUnits(680));
        MinimumSize = new Size(LogicalToDeviceUnits(640), LogicalToDeviceUnits(400));
        KeyPreview = true;

        BuildLayout();
        WireEvents();
        LoadDrives();

        _darkToggle.Checked = _palette.IsDark;
        Load += (_, _) =>
        {
            // The hidden list has no window yet, and the native dark styles need one.
            _ = _fileList.Handle;
            ApplyTheme();
        };

        if (initialPath is not null)
        {
            SelectCustomTarget(initialPath);
            Shown += (_, _) => StartScan();
        }
    }

    private enum SortKey
    {
        Name,
        Size,
        Files,
        Folders,
    }

    /// <summary>The "files directly in this folder" row in the folder list.</summary>
    private sealed record LooseFiles(FolderNode Folder);

    private sealed record Target(string Path, string Display)
    {
        public override string ToString() => Display;
    }

    private static ListView CreateList() => new()
    {
        View = View.Details,
        FullRowSelect = true,
        HideSelection = false,
        MultiSelect = false,
        Dock = DockStyle.Fill,
    };

    private void BuildLayout()
    {
        _targetBox.Width = LogicalToDeviceUnits(420);
        _adminButton.Visible = !IsElevated();

        var toolbar = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, WrapContents = true };
        toolbar.Controls.Add(new Label { Text = "Scan:", AutoSize = true, Anchor = AnchorStyles.Left, Padding = new Padding(0, 6, 0, 0) });
        toolbar.Controls.AddRange([_targetBox, _browseButton, _scanButton, _stopButton, _adminButton, _darkToggle]);

        var pathBar = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, ColumnCount = 2 };
        pathBar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        pathBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        pathBar.Controls.Add(_upButton, 0, 0);
        pathBar.Controls.Add(_pathBox, 1, 0);
        _pathBox.Margin = new Padding(3, 5, 3, 3);

        _folderList.Columns.Add("Name", LogicalToDeviceUnits(320));
        _folderList.Columns.Add("Size", LogicalToDeviceUnits(90), HorizontalAlignment.Right);
        _folderList.Columns.Add("% of parent", LogicalToDeviceUnits(80), HorizontalAlignment.Right);
        _folderList.Columns.Add("", LogicalToDeviceUnits(200));
        _folderList.Columns.Add("Files", LogicalToDeviceUnits(90), HorizontalAlignment.Right);
        _folderList.Columns.Add("Folders", LogicalToDeviceUnits(80), HorizontalAlignment.Right);
        _folderList.OwnerDraw = true;

        _fileList.Columns.Add("File", LogicalToDeviceUnits(300));
        _fileList.Columns.Add("Size", LogicalToDeviceUnits(90), HorizontalAlignment.Right);
        _fileList.Columns.Add("Folder", LogicalToDeviceUnits(520));
        _fileList.OwnerDraw = true;
        _fileList.Visible = false;

        // Toggle buttons rather than a TabControl, whose tabs can't be darkened.
        var viewBar = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, WrapContents = false };
        viewBar.Controls.AddRange([_foldersView, _filesView]);

        var lists = new Panel { Dock = DockStyle.Fill };
        lists.Controls.Add(_folderList);
        lists.Controls.Add(_fileList);

        var statusBar = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, ColumnCount = 2 };
        statusBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        statusBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, LogicalToDeviceUnits(180)));
        statusBar.Controls.Add(_statusLabel, 0, 0);
        statusBar.Controls.Add(_progress, 1, 0);
        _statusLabel.Text = "Pick a drive and hit Scan.";

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, Padding = new Padding(6) };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.Controls.Add(toolbar, 0, 0);
        root.Controls.Add(pathBar, 0, 1);
        root.Controls.Add(viewBar, 0, 2);
        root.Controls.Add(lists, 0, 3);
        root.Controls.Add(statusBar, 0, 4);
        Controls.Add(root);

        AcceptButton = _scanButton;
    }

    private void WireEvents()
    {
        _scanButton.Click += (_, _) => StartScan();
        _stopButton.Click += (_, _) => _cancellation?.Cancel();
        _browseButton.Click += (_, _) => BrowseForFolder();
        _adminButton.Click += (_, _) => RestartElevated();
        _upButton.Click += (_, _) => GoUp();
        _progressTimer.Tick += (_, _) => ShowProgress();
        _foldersView.CheckedChanged += (_, _) =>
        {
            _folderList.Visible = _foldersView.Checked;
            _fileList.Visible = !_foldersView.Checked;
        };
        _darkToggle.CheckedChanged += (_, _) =>
        {
            _palette = _darkToggle.Checked ? Palette.Dark : Palette.Light;
            Theme.SaveDarkSetting(_palette.IsDark);
            ApplyTheme();
        };

        _folderList.DrawColumnHeader += DrawHeader;
        _folderList.DrawItem += (_, _) => { };
        _folderList.DrawSubItem += DrawFolderCell;
        // Owner-drawn full-row-select lists only repaint the hovered cell otherwise.
        _folderList.MouseMove += (_, e) =>
        {
            if (_folderList.GetItemAt(e.X, e.Y) is { } item)
            {
                _folderList.Invalidate(item.Bounds);
            }
        };
        _folderList.ColumnWidthChanged += (_, _) => _folderList.Invalidate();
        _folderList.ColumnClick += (_, e) => SortBy(e.Column);
        _folderList.ItemActivate += (_, _) => OpenSelectedFolder();
        _folderList.ContextMenuStrip = BuildMenu(_folderList, SelectedFolderPath, selectInExplorer: false);

        _fileList.DrawColumnHeader += DrawHeader;
        _fileList.DrawItem += (_, e) => e.DrawDefault = true;
        _fileList.DrawSubItem += (_, e) => e.DrawDefault = true;
        _fileList.ItemActivate += (_, _) => ShowInExplorer(SelectedFilePath(), select: true);
        _fileList.ContextMenuStrip = BuildMenu(_fileList, SelectedFilePath, selectInExplorer: true);

        KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Back && !_pathBox.Focused && !_targetBox.Focused)
            {
                GoUp();
                e.Handled = true;
            }
        };

        FormClosing += (_, _) => _cancellation?.Cancel();
        FormClosed += (_, _) => _italicFont?.Dispose();
    }

    private ContextMenuStrip BuildMenu(ListView list, Func<string?> selectedPath, bool selectInExplorer)
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Show in Explorer", null, (_, _) => ShowInExplorer(selectedPath(), selectInExplorer));
        menu.Items.Add("Copy path", null, (_, _) =>
        {
            if (selectedPath() is { } path)
            {
                Clipboard.SetText(path);
            }
        });
        menu.Opening += (_, e) => e.Cancel = list.SelectedItems.Count == 0;
        return menu;
    }

    private void LoadDrives()
    {
        _targetBox.Items.Clear();
        foreach (DriveInfo drive in DriveInfo.GetDrives())
        {
            if (!drive.IsReady)
            {
                continue;
            }

            string label = string.IsNullOrWhiteSpace(drive.VolumeLabel) ? drive.DriveType.ToString() : drive.VolumeLabel;
            long used = drive.TotalSize - drive.TotalFreeSpace;
            string display =
                $"{drive.Name}  {label}  —  {SizeText.Format(used)} used of {SizeText.Format(drive.TotalSize)}" +
                $"  ({SizeText.Format(drive.TotalFreeSpace)} free)";
            _targetBox.Items.Add(new Target(drive.Name, display));
        }

        if (_targetBox.Items.Count > 0)
        {
            _targetBox.SelectedIndex = 0;
        }
    }

    private void BrowseForFolder()
    {
        using var dialog = new FolderBrowserDialog { Description = "Pick a folder to scan", UseDescriptionForTitle = true };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            SelectCustomTarget(dialog.SelectedPath);
        }
    }

    private void SelectCustomTarget(string path)
    {
        // "C:" on its own means "current directory on C:", not the drive root.
        if (path.Length == 2 && path[1] == ':')
        {
            path += "\\";
        }

        string full = Path.GetFullPath(path);

        var target = new Target(full, full);
        _targetBox.Items.Insert(0, target);
        _targetBox.SelectedIndex = 0;
    }

    private async void StartScan()
    {
        if (_targetBox.SelectedItem is not Target target || _cancellation is not null)
        {
            return;
        }

        if (!Directory.Exists(target.Path))
        {
            MessageBox.Show(this, $"{target.Path} doesn't exist or isn't reachable.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _cancellation = new CancellationTokenSource();
        _scanner = new DiskScanner();
        SetScanning(true);

        var stopwatch = Stopwatch.StartNew();
        DiskScanner scanner = _scanner;
        CancellationToken token = _cancellation.Token;

        FolderNode result;
        try
        {
            result = await Task.Run(() => scanner.Scan(target.Path, token));
        }
        finally
        {
            stopwatch.Stop();
            SetScanning(false);
            _cancellation.Dispose();
            _cancellation = null;
        }

        bool stopped = token.IsCancellationRequested;
        if (IsDisposed)
        {
            return;
        }

        ShowFolder(result);
        ShowBiggestFiles(scanner);

        string summary =
            $"{(stopped ? "Stopped early — partial results. " : "")}" +
            $"{SizeText.Format(result.Size)} in {scanner.FilesScanned:N0} files and {scanner.FoldersScanned:N0} folders, " +
            $"scanned in {stopwatch.Elapsed.TotalSeconds:0.0} s.";
        if (scanner.UnreadableFolders > 0)
        {
            summary += $"  {scanner.UnreadableFolders:N0} folders couldn't be read" +
                       (IsElevated() ? "." : " — restart as administrator to include them.");
        }

        _statusLabel.Text = summary;
    }

    private void SetScanning(bool scanning)
    {
        _scanButton.Enabled = !scanning;
        _browseButton.Enabled = !scanning;
        _targetBox.Enabled = !scanning;
        _stopButton.Enabled = scanning;
        _progress.Visible = scanning;

        if (scanning)
        {
            _folderList.Items.Clear();
            _fileList.Items.Clear();
            _pathBox.Text = "";
            _upButton.Enabled = false;
            _current = null;
            _progressTimer.Start();
        }
        else
        {
            _progressTimer.Stop();
        }
    }

    private void ShowProgress()
    {
        if (_scanner is not { } scanner)
        {
            return;
        }

        _statusLabel.Text =
            $"Scanning... {scanner.FilesScanned:N0} files, {scanner.FoldersScanned:N0} folders, " +
            $"{SizeText.Format(scanner.BytesScanned)} — {scanner.CurrentFolder}";
    }

    private void ShowFolder(FolderNode folder)
    {
        _current = folder;
        _pathBox.Text = folder.FullPath + (folder.Unreadable ? "   (access denied)" : "");
        _upButton.Enabled = folder.Parent is not null;

        var rows = new List<(object Tag, string Name, long Size, long Files, long Folders)>(folder.Children.Count + 1);
        foreach (FolderNode child in folder.Children)
        {
            string name = child.Unreadable ? child.Name + "  (access denied)" : child.Name;
            rows.Add((child, name, child.Size, child.FileCount, child.FolderCount));
        }

        if (folder.OwnFileCount > 0)
        {
            rows.Add((new LooseFiles(folder), $"[{folder.OwnFileCount:N0} files in this folder]", folder.OwnFilesSize, folder.OwnFileCount, 0));
        }

        rows.Sort((a, b) => _sortKey switch
        {
            SortKey.Name => string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase),
            SortKey.Files => b.Files.CompareTo(a.Files),
            SortKey.Folders => b.Folders.CompareTo(a.Folders),
            _ => b.Size.CompareTo(a.Size),
        });

        _folderList.BeginUpdate();
        try
        {
            _folderList.Items.Clear();
            var items = new ListViewItem[rows.Count];
            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                double share = folder.Size > 0 ? (double)row.Size / folder.Size : 0;
                items[i] = new ListViewItem(new[]
                {
                    row.Name,
                    SizeText.Format(row.Size),
                    $"{share:0.0%}",
                    "",
                    row.Files.ToString("N0"),
                    row.Tag is LooseFiles ? "" : row.Folders.ToString("N0"),
                })
                { Tag = row.Tag };
            }

            _folderList.Items.AddRange(items);
        }
        finally
        {
            _folderList.EndUpdate();
        }

        if (_folderList.Items.Count > 0)
        {
            _folderList.Items[0].Selected = true;
            _folderList.Items[0].Focused = true;
        }
    }

    private void ShowBiggestFiles(DiskScanner scanner)
    {
        var files = scanner.BigFiles.ToList();
        files.Sort((a, b) => b.Size.CompareTo(a.Size));

        _fileList.BeginUpdate();
        try
        {
            _fileList.Items.Clear();
            _fileList.Items.AddRange(files
                .Take(BiggestFilesShown)
                .Select(file => new ListViewItem(new[] { file.Name, SizeText.Format(file.Size), file.Folder }) { Tag = file })
                .ToArray());
        }
        finally
        {
            _fileList.EndUpdate();
        }
    }

    private void ApplyTheme()
    {
        Theme.Apply(this, _palette, [_folderList.ContextMenuStrip!, _fileList.ContextMenuStrip!]);
        _folderList.Invalidate();
        _fileList.Invalidate();
    }

    /// <summary>List headers are drawn by hand because the OS ignores their colours.</summary>
    private void DrawHeader(object? sender, DrawListViewColumnHeaderEventArgs e)
    {
        using (var back = new SolidBrush(_palette.Header))
        {
            e.Graphics.FillRectangle(back, e.Bounds);
        }

        using (var line = new Pen(_palette.Border))
        {
            e.Graphics.DrawLine(line, e.Bounds.Right - 1, e.Bounds.Top + 3, e.Bounds.Right - 1, e.Bounds.Bottom - 4);
            e.Graphics.DrawLine(line, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
        }

        TextFormatFlags flags = TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix;
        flags |= e.Header?.TextAlign == HorizontalAlignment.Right ? TextFormatFlags.Right : TextFormatFlags.Left;

        Rectangle textBounds = Rectangle.Inflate(e.Bounds, -LogicalToDeviceUnits(6), 0);
        TextRenderer.DrawText(e.Graphics, e.Header?.Text ?? "", e.Font ?? Font, textBounds, _palette.Text, flags);
    }

    private void DrawFolderCell(object? sender, DrawListViewSubItemEventArgs e)
    {
        if (e.Item is null || e.SubItem is null)
        {
            return;
        }

        bool selected = e.Item.Selected;
        Color back = selected ? _palette.Selection : _palette.Field;
        Color fore = selected ? _palette.SelectionText : _palette.Text;

        using (var brush = new SolidBrush(back))
        {
            e.Graphics.FillRectangle(brush, e.Bounds);
        }

        if (e.ColumnIndex == BarColumn)
        {
            DrawBar(e.Graphics, e.Bounds, e.Item.Tag, selected);
            return;
        }

        TextFormatFlags flags = TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix;
        flags |= _folderList.Columns[e.ColumnIndex].TextAlign == HorizontalAlignment.Right
            ? TextFormatFlags.Right
            : TextFormatFlags.Left;

        Rectangle textBounds = Rectangle.Inflate(e.Bounds, -LogicalToDeviceUnits(4), 0);
        _italicFont ??= new Font(_folderList.Font, FontStyle.Italic);
        Font font = e.Item.Tag is LooseFiles ? _italicFont : _folderList.Font;
        TextRenderer.DrawText(e.Graphics, e.SubItem.Text, font, textBounds, fore, flags);
    }

    private void DrawBar(Graphics graphics, Rectangle cell, object? tag, bool selected)
    {
        long size = tag switch
        {
            FolderNode node => node.Size,
            LooseFiles loose => loose.Folder.OwnFilesSize,
            _ => 0,
        };
        double share = _current is { Size: > 0 } current ? (double)size / current.Size : 0;

        int padding = LogicalToDeviceUnits(4);
        var track = Rectangle.Inflate(cell, -padding, -padding);
        if (track.Width <= 0 || track.Height <= 0)
        {
            return;
        }

        using (var trackBrush = new SolidBrush(selected ? ControlPaint.Dark(_palette.Selection) : _palette.BarTrack))
        {
            graphics.FillRectangle(trackBrush, track);
        }

        int filled = (int)Math.Round(track.Width * Math.Clamp(share, 0, 1));
        if (filled > 0)
        {
            using var barBrush = new SolidBrush(selected ? _palette.SelectionText : _palette.Bar);
            graphics.FillRectangle(barBrush, new Rectangle(track.X, track.Y, filled, track.Height));
        }
    }

    private void SortBy(int column)
    {
        _sortKey = column switch
        {
            0 => SortKey.Name,
            4 => SortKey.Files,
            5 => SortKey.Folders,
            _ => SortKey.Size,
        };

        if (_current is not null)
        {
            ShowFolder(_current);
        }
    }

    private void OpenSelectedFolder()
    {
        if (_folderList.SelectedItems.Count == 0)
        {
            return;
        }

        switch (_folderList.SelectedItems[0].Tag)
        {
            case FolderNode node when node.Children.Count > 0 || node.OwnFileCount > 0:
                ShowFolder(node);
                break;
            case FolderNode node:
                ShowInExplorer(node.FullPath, select: false);
                break;
            case LooseFiles loose:
                ShowInExplorer(loose.Folder.FullPath, select: false);
                break;
        }
    }

    private void GoUp()
    {
        if (_current?.Parent is not { } parent)
        {
            return;
        }

        FolderNode child = _current;
        ShowFolder(parent);

        // Land back on the folder we just came out of.
        foreach (ListViewItem item in _folderList.Items)
        {
            if (ReferenceEquals(item.Tag, child))
            {
                _folderList.SelectedItems.Clear();
                item.Selected = true;
                item.Focused = true;
                item.EnsureVisible();
                break;
            }
        }
    }

    private string? SelectedFolderPath() =>
        _folderList.SelectedItems.Count == 0
            ? null
            : _folderList.SelectedItems[0].Tag switch
            {
                FolderNode node => node.FullPath,
                LooseFiles loose => loose.Folder.FullPath,
                _ => null,
            };

    private string? SelectedFilePath() =>
        _fileList.SelectedItems.Count == 0 ? null : (_fileList.SelectedItems[0].Tag as BigFile)?.FullPath;

    private void ShowInExplorer(string? path, bool select)
    {
        if (path is null)
        {
            return;
        }

        try
        {
            string arguments = select ? $"/select,\"{path}\"" : $"\"{path}\"";
            Process.Start(new ProcessStartInfo("explorer.exe", arguments) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void RestartElevated()
    {
        if (Environment.ProcessPath is not { } exe)
        {
            return;
        }

        var start = new ProcessStartInfo(exe) { UseShellExecute = true, Verb = "runas" };
        if (_targetBox.SelectedItem is Target target)
        {
            // A trailing backslash would escape the closing quote, so "C:\" goes over as "C:".
            start.Arguments = $"\"{target.Path.TrimEnd('\\')}\"";
        }

        try
        {
            Process.Start(start);
            Close();
        }
        catch (Win32Exception)
        {
            // The UAC prompt was declined; carry on unelevated.
        }
    }

    private static bool IsElevated()
    {
        using WindowsIdentity identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }
}
