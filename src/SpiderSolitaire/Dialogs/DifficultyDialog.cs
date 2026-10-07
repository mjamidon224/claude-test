using SpiderSolitaire.Core;

namespace SpiderSolitaire.Dialogs;

/// <summary>The three-button difficulty picker shown on the first run.</summary>
internal sealed class DifficultyDialog : Form
{
    private Difficulty? _choice;

    private DifficultyDialog(Difficulty current)
    {
        DialogLayout.Configure(this, "Select Difficulty");

        TableLayoutPanel stack = DialogLayout.Stack();
        stack.Controls.Add(DialogLayout.Heading("How difficult do you want the game to be?"));
        stack.Controls.Add(DialogLayout.Text("Fewer suits make it easier to build runs. You can change this later under Game > Options."));

        FlowLayoutPanel row = new()
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = false,
            Margin = new Padding(0, 10, 0, 0),
        };

        foreach (Difficulty difficulty in DifficultyExtensions.All)
        {
            Button button = new()
            {
                AutoSize = true,
                MinimumSize = new Size(150, 64),
                Margin = new Padding(0, 0, 10, 0),
                Font = new Font("Segoe UI", 10.5F, FontStyle.Regular, GraphicsUnit.Point),
                Text = $"{difficulty.DisplayName()}\n{difficulty.SuitsText()}",
                UseVisualStyleBackColor = true,
            };

            button.Click += (_, _) =>
            {
                _choice = difficulty;
                DialogResult = DialogResult.OK;
            };

            row.Controls.Add(button);
            if (difficulty == current)
            {
                AcceptButton = button;
                ActiveControl = button;
            }
        }

        stack.Controls.Add(row);
        Controls.Add(stack);
    }

    protected override bool ProcessDialogKey(Keys keyData)
    {
        if (keyData == Keys.Escape)
        {
            Close();
            return true;
        }

        return base.ProcessDialogKey(keyData);
    }

    /// <summary>The difficulty chosen, or null if the dialog was closed.</summary>
    public static Difficulty? Ask(IWin32Window owner, Difficulty current)
    {
        using DifficultyDialog dialog = new(current);
        dialog.ShowDialog(owner);
        return dialog._choice;
    }
}
