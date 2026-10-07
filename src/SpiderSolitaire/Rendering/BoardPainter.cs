using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using SpiderSolitaire.Core;

namespace SpiderSolitaire.Rendering;

/// <summary>
/// Paints the table, cards, score box and highlights. It knows nothing about input or
/// animation timing: the board view decides what goes where and asks this to draw it.
/// </summary>
internal sealed class BoardPainter : IDisposable
{
    private static readonly FontFamily PanelFamily = Shapes.FindFamily("Segoe UI", "DejaVu Sans", "Arial");

    private readonly CardRenderer _cards = new();
    private Bitmap? _table;
    private TableStyle _tableStyle;
    private Font? _panelFont;

    public CardBackStyle CardBack
    {
        get => _cards.BackStyle;
        set => _cards.BackStyle = value;
    }

    public TableStyle Table
    {
        get => _tableStyle;
        set
        {
            if (value != _tableStyle)
            {
                _tableStyle = value;
                _table?.Dispose();
                _table = null;
            }
        }
    }

    public void Dispose()
    {
        _cards.Dispose();
        _table?.Dispose();
        _panelFont?.Dispose();
    }

    /// <summary>Everything that is not moving: table, empty-column outlines and every card not in <paramref name="skip"/>.</summary>
    public void PaintStatic(Graphics graphics, BoardLayout layout, IReadOnlySet<int> skip)
    {
        PrepareFor(layout);
        PaintTable(graphics, layout.ClientSize);

        for (int column = 0; column < Board.ColumnCount; column++)
        {
            if (layout.Board.Columns[column].IsEmpty)
            {
                PaintSlot(graphics, layout.ColumnSlot(column), layout.CardSize.Width);
            }
        }

        foreach (Placement placement in layout.Placements)
        {
            if (!skip.Contains(placement.Card.Id))
            {
                PaintCard(graphics, placement.Card, placement.Rect, placement.FaceUp);
            }
        }
    }

    public void PaintTable(Graphics graphics, Size size)
    {
        if (_table is null || _table.Size != size)
        {
            _table?.Dispose();
            _table = RenderTable(_tableStyle, size);
        }

        graphics.DrawImageUnscaled(_table, 0, 0);
    }

    /// <summary>
    /// Draws a card. <paramref name="squash"/> below 1 narrows it about its centre, which
    /// is how a card turning over is shown.
    /// </summary>
    public void PaintCard(Graphics graphics, Card card, RectangleF rect, bool faceUp, float squash = 1f)
    {
        Bitmap bitmap = faceUp ? _cards.Face(card) : _cards.Back;

        if (squash >= 0.999f)
        {
            graphics.DrawImageUnscaled(bitmap, (int)Math.Round(rect.X), (int)Math.Round(rect.Y));
            return;
        }

        float width = Math.Max(1f, rect.Width * squash);
        graphics.DrawImage(bitmap, new RectangleF(rect.X + (rect.Width - width) / 2, rect.Y, width, rect.Height));
    }

    /// <summary>A soft shadow under a lifted card.</summary>
    public void PaintShadow(Graphics graphics, RectangleF rect, float cardWidth)
    {
        float offset = Math.Max(2f, cardWidth * 0.04f);
        RectangleF shadow = new(rect.X + offset * 0.6f, rect.Y + offset, rect.Width, rect.Height);
        using GraphicsPath path = Shapes.RoundedRectangle(shadow, cardWidth * 0.08f);
        using SolidBrush brush = new(Color.FromArgb(70, 0, 0, 0));
        graphics.FillPath(brush, path);
    }

    public void PaintScore(Graphics graphics, BoardLayout layout, int score, int moves, TimeSpan time)
    {
        RectangleF panel = layout.ScorePanel;
        SmoothingMode smoothing = graphics.SmoothingMode;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;

        using (GraphicsPath path = Shapes.RoundedRectangle(panel, layout.CardSize.Width * 0.08f))
        using (SolidBrush fill = new(Color.FromArgb(80, 0, 0, 0)))
        using (Pen border = new(Color.FromArgb(60, 255, 255, 255)))
        {
            graphics.FillPath(fill, path);
            graphics.DrawPath(border, path);
        }

        graphics.SmoothingMode = smoothing;

        float fontSize = Math.Max(7f, panel.Height * 0.19f);
        if (_panelFont is null || Math.Abs(_panelFont.Size - fontSize) > 0.1f)
        {
            _panelFont?.Dispose();
            _panelFont = new Font(PanelFamily, fontSize, FontStyle.Regular, GraphicsUnit.Pixel);
        }

        string[] lines =
        {
            $"Score: {score.ToString("N0", CultureInfo.CurrentCulture)}",
            $"Moves: {moves.ToString("N0", CultureInfo.CurrentCulture)}",
            $"Time: {FormatTime(time)}",
        };

        using SolidBrush text = new(Color.FromArgb(240, 255, 255, 255));
        using StringFormat centred = new() { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        float lineHeight = panel.Height / (lines.Length + 0.6f);
        float y = panel.Top + lineHeight * 0.3f;
        foreach (string line in lines)
        {
            graphics.DrawString(line, _panelFont, text, new RectangleF(panel.Left, y, panel.Width, lineHeight), centred);
            y += lineHeight;
        }
    }

    /// <summary>A translucent wash with a bright edge over a card or a run, used for hints.</summary>
    public static void PaintHighlight(Graphics graphics, RectangleF rect, float cardWidth, Color color)
    {
        SmoothingMode smoothing = graphics.SmoothingMode;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using GraphicsPath path = Shapes.RoundedRectangle(rect, cardWidth * 0.075f);
        using SolidBrush fill = new(Color.FromArgb(90, color));
        using Pen edge = new(color, Math.Max(2f, cardWidth * 0.035f));
        graphics.FillPath(fill, path);
        graphics.DrawPath(edge, path);
        graphics.SmoothingMode = smoothing;
    }

    public static string FormatTime(TimeSpan time) =>
        time.TotalHours >= 1
            ? $"{(int)time.TotalHours}:{time.Minutes:00}:{time.Seconds:00}"
            : $"{time.Minutes}:{time.Seconds:00}";

    public static Bitmap RenderTable(TableStyle style, Size size)
    {
        Bitmap bitmap = new(Math.Max(1, size.Width), Math.Max(1, size.Height), PixelFormat.Format32bppPArgb);
        using Graphics graphics = Graphics.FromImage(bitmap);
        Color centre = style.BaseColor();
        Color edge = Shapes.Darken(centre, 0.45f);

        graphics.Clear(edge);

        // A wide ellipse so the light falls off gently towards the corners.
        RectangleF glow = RectangleF.Inflate(new RectangleF(0, 0, size.Width, size.Height), size.Width * 0.35f, size.Height * 0.45f);
        using GraphicsPath path = new();
        path.AddEllipse(glow);
        using PathGradientBrush brush = new(path)
        {
            CenterColor = Shapes.Lighten(centre, 0.08f),
            SurroundColors = new[] { edge },
            CenterPoint = new PointF(size.Width / 2f, size.Height * 0.4f),
        };
        graphics.FillPath(brush, path);

        return bitmap;
    }

    private void PrepareFor(BoardLayout layout) => _cards.SetSize(layout.CardSize);

    private static void PaintSlot(Graphics graphics, RectangleF rect, float cardWidth)
    {
        SmoothingMode smoothing = graphics.SmoothingMode;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        RectangleF inset = RectangleF.Inflate(rect, -1, -1);
        using GraphicsPath path = Shapes.RoundedRectangle(inset, cardWidth * 0.075f);
        using SolidBrush fill = new(Color.FromArgb(28, 0, 0, 0));
        using Pen edge = new(Color.FromArgb(90, 255, 255, 255), Math.Max(1f, cardWidth / 70f));
        graphics.FillPath(fill, path);
        graphics.DrawPath(edge, path);
        graphics.SmoothingMode = smoothing;
    }
}
