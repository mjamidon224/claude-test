using System.Runtime.InteropServices;

namespace NetIPConfig;

internal enum AppTheme
{
    Light,
    Dark,
}

/// <summary>
/// Recolours a form and everything on it. WinForms has no built-in dark mode, so each
/// control family is handled explicitly; a control tagged "dim" keeps a muted colour.
/// </summary>
internal static class Theme
{
    /// <summary>DWMWA_USE_IMMERSIVE_DARK_MODE — darkens the title bar on Windows 10 1903+ and 11.</summary>
    private const int UseImmersiveDarkMode = 20;

    private static readonly Color DarkBackground = Color.FromArgb(32, 32, 32);
    private static readonly Color DarkField = Color.FromArgb(45, 45, 48);
    private static readonly Color DarkText = Color.FromArgb(240, 240, 240);
    private static readonly Color DarkDim = Color.FromArgb(155, 155, 155);
    private static readonly Color DarkBorder = Color.FromArgb(70, 70, 74);
    private static readonly Color DarkLink = Color.FromArgb(120, 175, 255);

    public const string DimTag = "dim";

    public static void Apply(Form form, AppTheme theme)
    {
        bool dark = theme == AppTheme.Dark;

        form.BackColor = dark ? DarkBackground : SystemColors.Control;
        form.ForeColor = dark ? DarkText : SystemColors.ControlText;
        ApplyToChildren(form.Controls, dark);
        UseDarkTitleBar(form, dark);
        form.Invalidate(invalidateChildren: true);
    }

    private static void ApplyToChildren(Control.ControlCollection controls, bool dark)
    {
        foreach (Control control in controls)
        {
            switch (control)
            {
                case TextBox textBox:
                    textBox.BackColor = dark ? DarkField : SystemColors.Window;
                    textBox.ForeColor = dark ? DarkText : SystemColors.WindowText;
                    textBox.BorderStyle = dark ? BorderStyle.FixedSingle : BorderStyle.Fixed3D;
                    break;

                case ListView listView:
                    listView.BackColor = dark ? DarkField : SystemColors.Window;
                    listView.ForeColor = dark ? DarkText : SystemColors.WindowText;
                    break;

                case ComboBox comboBox:
                    comboBox.FlatStyle = dark ? FlatStyle.Flat : FlatStyle.Standard;
                    comboBox.BackColor = dark ? DarkField : SystemColors.Window;
                    comboBox.ForeColor = dark ? DarkText : SystemColors.WindowText;
                    break;

                case Button button:
                    button.FlatStyle = dark ? FlatStyle.Flat : FlatStyle.Standard;
                    button.UseVisualStyleBackColor = !dark;
                    button.BackColor = dark ? DarkField : SystemColors.Control;
                    button.ForeColor = dark ? DarkText : SystemColors.ControlText;
                    button.FlatAppearance.BorderColor = DarkBorder;
                    break;

                // LinkLabel derives from Label, so it has to be matched first.
                case LinkLabel linkLabel:
                    linkLabel.ForeColor = dark ? DarkText : SystemColors.ControlText;
                    linkLabel.LinkColor = dark ? DarkLink : SystemColors.HotTrack;
                    linkLabel.ActiveLinkColor = dark ? DarkLink : SystemColors.HotTrack;
                    linkLabel.VisitedLinkColor = dark ? DarkLink : SystemColors.HotTrack;
                    break;

                case Label label:
                    label.ForeColor = IsDim(label)
                        ? (dark ? DarkDim : SystemColors.GrayText)
                        : (dark ? DarkText : SystemColors.ControlText);
                    break;

                default:
                    control.BackColor = dark ? DarkBackground : SystemColors.Control;
                    control.ForeColor = dark ? DarkText : SystemColors.ControlText;
                    break;
            }

            if (control.HasChildren)
            {
                ApplyToChildren(control.Controls, dark);
            }
        }
    }

    /// <summary>Colours for the hand-drawn ListView header (the OS ignores BackColor there).</summary>
    public static Color ListHeaderBackground(bool dark) =>
        dark ? Color.FromArgb(55, 55, 58) : SystemColors.Control;

    public static Color ListHeaderText(bool dark) =>
        dark ? DarkText : SystemColors.ControlText;

    private static bool IsDim(Control control) =>
        control.Tag is string tag && string.Equals(tag, DimTag, StringComparison.Ordinal);

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

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);
}
