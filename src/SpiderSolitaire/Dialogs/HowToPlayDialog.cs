namespace SpiderSolitaire.Dialogs;

/// <summary>The rules, controls and scoring.</summary>
internal sealed class HowToPlayDialog : Form
{
    private const string Rules =
        "THE GOAL\r\n" +
        "Clear every card from the table by building eight runs from King down to Ace, each in a single suit. A finished run leaves the table on its own.\r\n" +
        "\r\n" +
        "THE LAYOUT\r\n" +
        "Ten columns hold 54 cards, with only the bottom card of each turned up. The other 50 cards wait in the stock, bottom right: one pile for each of five deals.\r\n" +
        "\r\n" +
        "MOVING CARDS\r\n" +
        "• A card can go on any card one rank higher, whatever its suit: a 7 on any 8.\r\n" +
        "• Cards that run down by one in the same suit move together, onto a card one higher than the top of the run.\r\n" +
        "• Any card, or any same-suit run, can go into an empty column.\r\n" +
        "• A face-down card turns over when you uncover it.\r\n" +
        "\r\n" +
        "Drag cards to move them, or click a card to send it (and the cards on it) to the best place available.\r\n" +
        "\r\n" +
        "DEALING\r\n" +
        "Click the stock or press D to deal one card onto every column. You can't deal while a column is empty, unless there are fewer cards left on the table than there are columns.\r\n" +
        "\r\n" +
        "DIFFICULTY\r\n" +
        "Beginner uses one suit, Intermediate two and Advanced all four. The more suits, the harder it is to build same-suit runs. Change it under Game > Options.\r\n" +
        "\r\n" +
        "SCORING\r\n" +
        "You start with 500 points. Every move costs a point (dealing and undoing count as moves) and every finished suit earns 100.\r\n" +
        "\r\n" +
        "STATISTICS\r\n" +
        "A game counts when you win it, or when you give it up after making a move: starting a new game, restarting, or exiting without saving all count as a loss.\r\n" +
        "\r\n" +
        "KEYS\r\n" +
        "F2\tNew game\r\n" +
        "Ctrl+Z\tUndo\r\n" +
        "H\tHint (press again for the next one)\r\n" +
        "D\tDeal a new row\r\n" +
        "F4\tStatistics\r\n" +
        "F5\tOptions\r\n" +
        "F7\tChange appearance\r\n" +
        "F1\tThis help";

    private HowToPlayDialog()
    {
        DialogLayout.Configure(this, "How to Play Spider Solitaire");

        TableLayoutPanel stack = DialogLayout.Stack();

        TextBox text = new()
        {
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            WordWrap = true,
            BackColor = SystemColors.Window,
            Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point),
            Size = new Size(520, 420),
            Text = Rules,
            TabStop = false,
        };

        stack.Controls.Add(text);

        Button close = DialogLayout.Button("Close", DialogResult.Cancel);
        stack.Controls.Add(DialogLayout.ButtonRow(close));
        Controls.Add(stack);

        AcceptButton = close;
        CancelButton = close;
        ActiveControl = close;
    }

    public static void Present(IWin32Window owner)
    {
        using HowToPlayDialog dialog = new();
        dialog.ShowDialog(owner);
    }
}
