using SpiderSolitaire.Rendering;

namespace SpiderSolitaire.Dialogs;

/// <summary>Picks the card back and table colour from rendered previews.</summary>
internal sealed class AppearanceDialog : Form
{
    private readonly Dictionary<CardBackStyle, RadioButton> _backs = new();
    private readonly Dictionary<TableStyle, RadioButton> _tables = new();
    private readonly List<Image> _previews = new();

    private AppearanceDialog(CardBackStyle back, TableStyle table)
    {
        DialogLayout.Configure(this, "Change Appearance");

        TableLayoutPanel stack = DialogLayout.Stack();

        stack.Controls.Add(DialogLayout.Text("Card back"));
        FlowLayoutPanel backRow = Row();
        foreach (CardBackStyle style in Enum.GetValues<CardBackStyle>())
        {
            Bitmap preview = CardRenderer.RenderBack(style, new Size(72, 101));
            RadioButton option = Option(style.DisplayName(), preview, style == back, new Size(108, 150));
            _backs[style] = option;
            backRow.Controls.Add(option);
        }

        stack.Controls.Add(backRow);

        stack.Controls.Add(DialogLayout.Text("Table"));
        FlowLayoutPanel tableRow = Row();
        foreach (TableStyle style in Enum.GetValues<TableStyle>())
        {
            Bitmap preview = BoardPainter.RenderTable(style, new Size(84, 52));
            RadioButton option = Option(style.DisplayName(), preview, style == table, new Size(108, 96));
            _tables[style] = option;
            tableRow.Controls.Add(option);
        }

        stack.Controls.Add(tableRow);

        Button ok = DialogLayout.Button("OK", DialogResult.OK);
        Button cancel = DialogLayout.Button("Cancel", DialogResult.Cancel);
        stack.Controls.Add(DialogLayout.ButtonRow(ok, cancel));
        Controls.Add(stack);

        AcceptButton = ok;
        CancelButton = cancel;
        DialogLayout.Complete(this);
    }

    /// <summary>The chosen card back and table, or null if cancelled.</summary>
    public static (CardBackStyle CardBack, TableStyle Table)? Ask(IWin32Window owner, CardBackStyle back, TableStyle table)
    {
        using AppearanceDialog dialog = new(back, table);
        if (dialog.ShowDialog(owner) != DialogResult.OK)
        {
            return null;
        }

        return (
            dialog._backs.First(entry => entry.Value.Checked).Key,
            dialog._tables.First(entry => entry.Value.Checked).Key);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            foreach (Image preview in _previews)
            {
                preview.Dispose();
            }
        }

        base.Dispose(disposing);
    }

    private static FlowLayoutPanel Row() => new()
    {
        AutoSize = true,
        AutoSizeMode = AutoSizeMode.GrowAndShrink,
        WrapContents = false,
        Margin = new Padding(0, 0, 0, 12),
    };

    /// <summary>A toggle button showing a preview above its name; options in one row are exclusive.</summary>
    private RadioButton Option(string name, Image preview, bool selected, Size size)
    {
        _previews.Add(preview);
        return new RadioButton
        {
            Appearance = Appearance.Button,
            Checked = selected,
            Image = preview,
            ImageAlign = ContentAlignment.TopCenter,
            TextAlign = ContentAlignment.BottomCenter,
            TextImageRelation = TextImageRelation.ImageAboveText,
            Text = name,
            Size = size,
            Padding = new Padding(4),
            Margin = new Padding(0, 0, 8, 0),
            UseVisualStyleBackColor = true,
        };
    }
}
