using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using SpiderSolitaire.Core;

namespace SpiderSolitaire.Rendering;

internal readonly record struct ScoreInfo(int Score, int Moves, TimeSpan Time);

internal readonly record struct BoardButtonState(BoardButton Button, bool Enabled, bool Hovered, bool Pressed);

/// <summary>
/// Paints the table, cards, score box, buttons and highlights. It knows nothing about
/// input or animation timing: the board view decides what goes where and asks this to draw it.
/// </summary>
internal sealed class BoardPainter : IDisposable
{
    private static readonly FontFamily PanelFamily = Shapes.FindFamily("Segoe UI", "DejaVu Sans", "Arial");
    private static readonly FontFamily ButtonFamily = Shapes.FindFamily("Segoe UI Semibold", "Segoe UI", "DejaVu Sans", "Arial");

    private readonly CardRenderer _cards = new();
    private Bitmap? _table;
    private TableStyle _tableStyle;

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
    }

    /// <summary>
    /// Everything that is not moving: the table, empty-column outlines, score, buttons and
    /// every card not in <paramref name="skip"/>.
    /// </summary>
    public void PaintStatic(Graphics graphics, BoardLayout layout, IReadOnlySet<int> skip, ScoreInfo score, IReadOnlyList<BoardButtonState> buttons)
    {
        PrepareFor(layout);
        PaintTable(graphics, layout.ClientSize);
        graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;

        for (int column = 0; column < Board.ColumnCount; column++)
        {
            if (layout.Board.Columns[column].IsEmpty)
            {
                PaintSlot(graphics, layout.ColumnSlot(column), layout.CardSize.Width);
            }
        }

        // Finished suits and the stock, then the score and buttons, then the columns: a
        // column long enough to reach the bottom row stays on top, where it can be played.
        foreach (Placement placement in layout.Placements)
        {
            if (placement.Pile != Pile.Column && !skip.Contains(placement.Card.Id))
            {
                PaintCard(graphics, placement.Card, placement.Rect, placement.FaceUp);
            }
        }

        PaintScore(graphics, layout, score);
        foreach (BoardButtonState button in buttons)
        {
            PaintButton(graphics, layout.ButtonRect(button.Button), button, layout.CardSize.Width);
        }

        foreach (Placement placement in layout.Placements)
        {
            if (placement.Pile == Pile.Column && !skip.Contains(placement.Card.Id))
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

    /// <summary>Score, moves and time on one line, each centred in a third of the box.</summary>
    private static void PaintScore(Graphics graphics, BoardLayout layout, ScoreInfo score)
    {
        RectangleF panel = layout.ScorePanel;
        PaintPanel(graphics, panel, layout.CardSize.Width, Color.FromArgb(80, 0, 0, 0), Color.FromArgb(60, 255, 255, 255));

        CultureInfo culture = CultureInfo.CurrentCulture;
        string[] parts =
        {
            $"Score: {score.Score.ToString("N0", culture)}",
            $"Moves: {score.Moves.ToString("N0", culture)}",
            $"Time: {FormatTime(score.Time)}",
        };

        // As large as the box allows, shrunk if the widest part would not fit its third.
        float third = panel.Width / parts.Length;
        float size = Math.Max(6f, panel.Height * 0.42f);
        using Font measure = new(PanelFamily, size, FontStyle.Regular, GraphicsUnit.Pixel);
        float widest = parts.Max(part => graphics.MeasureString(part, measure).Width);
        if (widest > third * 0.92f)
        {
            size = Math.Max(6f, size * third * 0.92f / widest);
        }

        using Font font = new(PanelFamily, size, FontStyle.Regular, GraphicsUnit.Pixel);
        using SolidBrush text = new(Color.FromArgb(240, 255, 255, 255));
        using StringFormat centred = new() { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        for (int i = 0; i < parts.Length; i++)
        {
            graphics.DrawString(parts[i], font, text, new RectangleF(panel.Left + third * i, panel.Top, third, panel.Height), centred);
        }
    }

    /// <summary>A button under the score: an icon and a label on a translucent plate.</summary>
    private static void PaintButton(Graphics graphics, RectangleF rect, BoardButtonState state, float cardWidth)
    {
        (Color fill, Color edge, Color ink) = state switch
        {
            { Enabled: false } => (Color.FromArgb(35, 0, 0, 0), Color.FromArgb(45, 255, 255, 255), Color.FromArgb(95, 255, 255, 255)),
            { Pressed: true } => (Color.FromArgb(130, 0, 0, 0), Color.FromArgb(110, 255, 255, 255), Color.White),
            { Hovered: true } => (Color.FromArgb(55, 255, 255, 255), Color.FromArgb(170, 255, 255, 255), Color.White),
            _ => (Color.FromArgb(80, 0, 0, 0), Color.FromArgb(90, 255, 255, 255), Color.FromArgb(240, 255, 255, 255)),
        };

        PaintPanel(graphics, rect, cardWidth, fill, edge);

        string label = state.Button switch
        {
            BoardButton.Hint => "Hint",
            BoardButton.Undo => "Undo",
            _ => "Undo All",
        };

        using Font font = new(ButtonFamily, Math.Max(6f, rect.Height * 0.36f), FontStyle.Regular, GraphicsUnit.Pixel);
        SizeF textSize = graphics.MeasureString(label, font, PointF.Empty, StringFormat.GenericTypographic);
        float icon = rect.Height * 0.44f;
        float gap = icon * 0.3f;
        bool showIcon = icon + gap + textSize.Width <= rect.Width * 0.92f;
        float x = rect.Left + (rect.Width - (showIcon ? icon + gap + textSize.Width : textSize.Width)) / 2;
        float nudge = state.Pressed && state.Enabled ? 1f : 0f;

        if (showIcon)
        {
            PaintIcon(graphics, state.Button, new RectangleF(x, rect.Top + (rect.Height - icon) / 2 + nudge, icon, icon), ink);
            x += icon + gap;
        }

        using SolidBrush brush = new(ink);
        graphics.DrawString(label, font, brush, x, rect.Top + (rect.Height - textSize.Height) / 2 + nudge, StringFormat.GenericTypographic);
    }

    private static void PaintIcon(Graphics graphics, BoardButton button, RectangleF box, Color ink)
    {
        SmoothingMode smoothing = graphics.SmoothingMode;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using SolidBrush brush = new(ink);
        float w = box.Width;
        float h = box.Height;
        float centreX = box.Left + w / 2;

        switch (button)
        {
            case BoardButton.Hint:
            {
                // A light bulb: glass, a tapering neck, then the screw base.
                float bulb = w * 0.7f;
                graphics.FillEllipse(brush, centreX - bulb / 2, box.Top, bulb, bulb);
                graphics.FillPolygon(brush, new[]
                {
                    new PointF(centreX - bulb * 0.3f, box.Top + bulb * 0.75f),
                    new PointF(centreX + bulb * 0.3f, box.Top + bulb * 0.75f),
                    new PointF(centreX + bulb * 0.2f, box.Top + h * 0.8f),
                    new PointF(centreX - bulb * 0.2f, box.Top + h * 0.8f),
                });
                graphics.FillRectangle(brush, centreX - bulb * 0.18f, box.Top + h * 0.86f, bulb * 0.36f, h * 0.14f);
                break;
            }

            case BoardButton.Undo:
            {
                // An arrow curving back over the top, its head on the left pointing down.
                float radius = w * 0.36f;
                PointF centre = new(centreX + w * 0.04f, box.Top + h * 0.56f);
                using Pen pen = new(ink, Math.Max(1.2f, w * 0.14f)) { EndCap = LineCap.Round };
                graphics.DrawArc(pen, centre.X - radius, centre.Y - radius, radius * 2, radius * 2, 165, 210);

                double angle = 165 * Math.PI / 180;
                PointF start = new(centre.X + radius * (float)Math.Cos(angle), centre.Y + radius * (float)Math.Sin(angle));
                PointF back = new((float)Math.Sin(angle), -(float)Math.Cos(angle));
                PointF across = new(-back.Y, back.X);
                float head = w * 0.3f;
                graphics.FillPolygon(brush, new[]
                {
                    new PointF(start.X + back.X * head, start.Y + back.Y * head),
                    new PointF(start.X + across.X * head * 0.75f - back.X * head * 0.2f, start.Y + across.Y * head * 0.75f - back.Y * head * 0.2f),
                    new PointF(start.X - across.X * head * 0.75f - back.X * head * 0.2f, start.Y - across.Y * head * 0.75f - back.Y * head * 0.2f),
                });
                break;
            }

            default:
            {
                // Back to the start: a bar and two arrowheads pointing at it.
                float top = box.Top + h * 0.16f;
                float bottom = box.Bottom - h * 0.16f;
                float middle = box.Top + h / 2;
                graphics.FillRectangle(brush, box.Left + w * 0.04f, top, w * 0.14f, bottom - top);
                graphics.FillPolygon(brush, new[] { new PointF(box.Left + w * 0.22f, middle), new PointF(box.Left + w * 0.6f, top), new PointF(box.Left + w * 0.6f, bottom) });
                graphics.FillPolygon(brush, new[] { new PointF(box.Left + w * 0.58f, middle), new PointF(box.Right - w * 0.02f, top), new PointF(box.Right - w * 0.02f, bottom) });
                break;
            }
        }

        graphics.SmoothingMode = smoothing;
    }

    private static void PaintPanel(Graphics graphics, RectangleF rect, float cardWidth, Color fill, Color edge)
    {
        SmoothingMode smoothing = graphics.SmoothingMode;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using GraphicsPath path = Shapes.RoundedRectangle(rect, Math.Min(rect.Height / 2, cardWidth * 0.08f));
        using SolidBrush brush = new(fill);
        using Pen pen = new(edge);
        graphics.FillPath(brush, path);
        graphics.DrawPath(pen, path);
        graphics.SmoothingMode = smoothing;
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
