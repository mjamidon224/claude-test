namespace NetIPConfig;

/// <summary>A one-line input dialog, since WinForms has no equivalent of InputBox.</summary>
internal sealed class TextPromptDialog : Form
{
    private readonly TextBox _input;

    private TextPromptDialog(string title, string prompt, string initialValue)
    {
        Label promptLabel = new()
        {
            AutoSize = true,
            Location = new Point(12, 15),
            Text = prompt,
        };

        _input = new TextBox
        {
            Location = new Point(12, 38),
            Size = new Size(356, 23),
            Text = initialValue,
            MaxLength = 60,
        };

        Button ok = new()
        {
            DialogResult = DialogResult.OK,
            Location = new Point(196, 74),
            Size = new Size(84, 28),
            Text = "OK",
        };

        Button cancel = new()
        {
            DialogResult = DialogResult.Cancel,
            Location = new Point(288, 74),
            Size = new Size(84, 28),
            Text = "Cancel",
        };

        AcceptButton = ok;
        CancelButton = cancel;
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(384, 114);
        Controls.AddRange(new Control[] { promptLabel, _input, ok, cancel });
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        Text = title;

        _input.SelectAll();
    }

    /// <summary>Returns the trimmed text, or null if the dialog was cancelled or left blank.</summary>
    public static string? Ask(IWin32Window owner, string title, string prompt, string initialValue, AppTheme theme)
    {
        using TextPromptDialog dialog = new(title, prompt, initialValue);
        Theme.Apply(dialog, theme);

        if (dialog.ShowDialog(owner) != DialogResult.OK)
        {
            return null;
        }

        string value = dialog._input.Text.Trim();
        return value.Length > 0 ? value : null;
    }
}
