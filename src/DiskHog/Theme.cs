using System.Runtime.InteropServices;

namespace DiskHog;

/// <summary>Colours for one theme. The folder list, its bars and the list headers are drawn by hand from these.</summary>
internal sealed record Palette(
    bool IsDark,
    Color Window,
    Color Field,
    Color Text,
    Color Dim,
    Color Border,
    Color Header,
    Color Selection,
    Color SelectionText,
    Color Bar,
    Color BarTrack)
{
    public static readonly Palette Dark = new(
        IsDark: true,
        Window: Color.FromArgb(32, 32, 32),
        Field: Color.FromArgb(40, 40, 42),
        Text: Color.FromArgb(240, 240, 240),
        Dim: Color.FromArgb(160, 160, 160),
        Border: Color.FromArgb(70, 70, 74),
        Header: Color.FromArgb(52, 52, 56),
        Selection: Color.FromArgb(0, 90, 158),
        SelectionText: Color.White,
        Bar: Color.FromArgb(76, 160, 255),
        BarTrack: Color.FromArgb(62, 62, 66));

    public static readonly Palette Light = new(
        IsDark: false,
        Window: SystemColors.Control,
        Field: SystemColors.Window,
        Text: SystemColors.ControlText,
        Dim: SystemColors.GrayText,
        Border: Color.FromArgb(200, 200, 200),
        Header: SystemColors.Control,
        Selection: SystemColors.Highlight,
        SelectionText: SystemColors.HighlightText,
        Bar: Color.FromArgb(0, 120, 215),
        BarTrack: Color.FromArgb(225, 225, 225));
}

/// <summary>
/// Recolours the form and its controls. WinForms has no dark mode of its own, so each control
/// family is handled here, and the native parts (scroll bars, list headers, combo boxes, title
/// bar) are switched with the dark visual styles Windows 10 1809+ and Windows 11 ship.
/// </summary>
internal static class Theme
{
    /// <summary>DWMWA_USE_IMMERSIVE_DARK_MODE — darkens the title bar.</summary>
    private const int UseImmersiveDarkMode = 20;

    private const int ListViewGetHeader = 0x101F;

    private static readonly string SettingsFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DiskHog", "theme.txt");

    /// <summary>Dark unless the user switched to light last time.</summary>
    public static bool LoadDarkSetting()
    {
        try
        {
            return !File.Exists(SettingsFile) || File.ReadAllText(SettingsFile).Trim() != "light";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return true;
        }
    }

    public static void SaveDarkSetting(bool dark)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsFile)!);
            File.WriteAllText(SettingsFile, dark ? "dark" : "light");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Not remembering the choice is no reason to bother anyone.
        }
    }

    public static void Apply(Form form, Palette palette, ContextMenuStrip[] menus)
    {
        form.BackColor = palette.Window;
        form.ForeColor = palette.Text;
        ApplyToChildren(form.Controls, palette);

        var renderer = palette.IsDark
            ? new ToolStripProfessionalRenderer(new MenuColors(palette))
            : new ToolStripProfessionalRenderer();
        foreach (ContextMenuStrip menu in menus)
        {
            menu.Renderer = renderer;
            menu.BackColor = palette.IsDark ? palette.Field : SystemColors.Menu;
            menu.ForeColor = palette.Text;
        }

        UseDarkTitleBar(form, palette.IsDark);
        form.Invalidate(invalidateChildren: true);
    }

    private static void ApplyToChildren(Control.ControlCollection controls, Palette palette)
    {
        bool dark = palette.IsDark;

        foreach (Control control in controls)
        {
            switch (control)
            {
                case ListView list:
                    list.BackColor = palette.Field;
                    list.ForeColor = palette.Text;
                    UseNativeTheme(list, dark ? "DarkMode_Explorer" : "Explorer");
                    if (list.IsHandleCreated)
                    {
                        IntPtr header = SendMessage(list.Handle, ListViewGetHeader, IntPtr.Zero, IntPtr.Zero);
                        TrySetWindowTheme(header, dark ? "DarkMode_ItemsView" : "Explorer");
                    }

                    break;

                case TextBox textBox:
                    textBox.BackColor = palette.Field;
                    textBox.ForeColor = palette.Text;
                    textBox.BorderStyle = dark ? BorderStyle.FixedSingle : BorderStyle.Fixed3D;
                    break;

                case ComboBox comboBox:
                    comboBox.FlatStyle = dark ? FlatStyle.Flat : FlatStyle.Standard;
                    comboBox.BackColor = palette.Field;
                    comboBox.ForeColor = palette.Text;
                    UseNativeTheme(comboBox, dark ? "DarkMode_CFD" : "Explorer");
                    break;

                case ButtonBase button when button is Button || (button is RadioButton { Appearance: Appearance.Button }):
                    button.FlatStyle = dark ? FlatStyle.Flat : FlatStyle.Standard;
                    button.UseVisualStyleBackColor = !dark;
                    button.BackColor = dark ? palette.Header : SystemColors.Control;
                    button.ForeColor = palette.Text;
                    button.FlatAppearance.BorderColor = palette.Border;
                    button.FlatAppearance.CheckedBackColor = palette.Selection;
                    break;

                case Label label:
                    label.ForeColor = palette.Text;
                    break;

                default:
                    control.BackColor = palette.Window;
                    control.ForeColor = palette.Text;
                    break;
            }

            if (control.HasChildren)
            {
                ApplyToChildren(control.Controls, palette);
            }
        }
    }

    private static void UseNativeTheme(Control control, string theme)
    {
        if (control.IsHandleCreated)
        {
            TrySetWindowTheme(control.Handle, theme);
        }
    }

    private static void TrySetWindowTheme(IntPtr handle, string theme)
    {
        if (handle == IntPtr.Zero)
        {
            return;
        }

        try
        {
            SetWindowTheme(handle, theme, null);
        }
        catch (Exception)
        {
            // Without uxtheme's dark styles the native parts simply stay light.
        }
    }

    private static void UseDarkTitleBar(Form form, bool dark)
    {
        try
        {
            int value = dark ? 1 : 0;
            DwmSetWindowAttribute(form.Handle, UseImmersiveDarkMode, ref value, sizeof(int));
        }
        catch (Exception)
        {
            // Older Windows builds do not know the attribute; the client area is still themed.
        }
    }

    /// <summary>Context menu colours; the default renderer ignores BackColor on the dropdown.</summary>
    private sealed class MenuColors(Palette palette) : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground => palette.Field;
        public override Color ImageMarginGradientBegin => palette.Field;
        public override Color ImageMarginGradientMiddle => palette.Field;
        public override Color ImageMarginGradientEnd => palette.Field;
        public override Color MenuBorder => palette.Border;
        public override Color MenuItemBorder => palette.Selection;
        public override Color MenuItemSelected => palette.Selection;
        public override Color MenuItemSelectedGradientBegin => palette.Selection;
        public override Color MenuItemSelectedGradientEnd => palette.Selection;
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);

    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
    private static extern int SetWindowTheme(IntPtr window, string? subAppName, string? subIdList);

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);
}
