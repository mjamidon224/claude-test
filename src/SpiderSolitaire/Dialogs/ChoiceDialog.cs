namespace SpiderSolitaire.Dialogs;

/// <summary>
/// A question with a heading, an explanation and a row of labelled buttons, in the style
/// of the Windows game's prompts. Shows which button was pressed.
/// </summary>
internal sealed class ChoiceDialog : Form
{
    /// <summary>Returned when the dialog is closed without pressing a button.</summary>
    public const int Dismissed = -1;

    private int _choice = Dismissed;

    private ChoiceDialog(string title, string heading, string? message, string[] choices)
    {
        DialogLayout.Configure(this, title);

        TableLayoutPanel stack = DialogLayout.Stack();
        stack.Controls.Add(DialogLayout.Heading(heading));
        if (!string.IsNullOrEmpty(message))
        {
            stack.Controls.Add(DialogLayout.Text(message));
        }

        Button[] buttons = new Button[choices.Length];
        for (int i = 0; i < choices.Length; i++)
        {
            int index = i;
            buttons[i] = DialogLayout.Button(choices[i], DialogResult.OK);
            buttons[i].Click += (_, _) => _choice = index;
        }

        stack.Controls.Add(DialogLayout.ButtonRow(buttons));
        Controls.Add(stack);

        AcceptButton = buttons[0];
        ActiveControl = buttons[0];
    }

    /// <summary>Escape closes without a choice, which every caller treats as the safe option.</summary>
    protected override bool ProcessDialogKey(Keys keyData)
    {
        if (keyData == Keys.Escape)
        {
            Close();
            return true;
        }

        return base.ProcessDialogKey(keyData);
    }

    public static int Ask(IWin32Window owner, string title, string heading, string? message, params string[] choices)
    {
        using ChoiceDialog dialog = new(title, heading, message, choices);
        dialog.ShowDialog(owner);
        return dialog._choice;
    }
}
