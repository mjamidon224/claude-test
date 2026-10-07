using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using SpiderSolitaire.Core;

namespace SpiderSolitaire.Rendering;

/// <summary>
/// Draws card faces and backs, and keeps each one as a bitmap at the current card size
/// so that painting the table is a matter of copying bitmaps. Everything is drawn from
/// vectors, so the cards are sharp at any window size or DPI.
/// </summary>
internal sealed class CardRenderer : IDisposable
{
    private static readonly Color Red = Color.FromArgb(196, 22, 34);
    private static readonly Color Black = Color.FromArgb(22, 22, 26);

    private static readonly FontFamily IndexFamily = Shapes.FindFamily("Segoe UI", "DejaVu Sans", "Arial");
    private static readonly FontFamily SymbolFamily = Shapes.FindFamily("Segoe UI Symbol", "DejaVu Sans", "Arial");

    /// <summary>The height of a capital letter at the 100-unit size text paths are built at, used to line up every rank.</summary>
    private static readonly RectangleF CapitalBounds = TextBounds("K", IndexFamily, FontStyle.Bold);

    private readonly Dictionary<(Suit Suit, int Rank), Bitmap> _faces = new();
    private Bitmap? _back;
    private CardBackStyle _backStyle;

    public Size CardSize { get; private set; }

    public CardBackStyle BackStyle
    {
        get => _backStyle;
        set
        {
            if (value != _backStyle)
            {
                _backStyle = value;
                _back?.Dispose();
                _back = null;
            }
        }
    }

    /// <summary>Drops every cached bitmap when the size changes.</summary>
    public void SetSize(Size size)
    {
        if (size == CardSize)
        {
            return;
        }

        Clear();
        CardSize = size;
    }

    public Bitmap Face(Card card)
    {
        if (!_faces.TryGetValue((card.Suit, card.Rank), out Bitmap? bitmap))
        {
            bitmap = Render(CardSize, graphics => DrawFace(graphics, CardSize, card.Suit, card.Rank));
            _faces[(card.Suit, card.Rank)] = bitmap;
        }

        return bitmap;
    }

    public Bitmap Back => _back ??= RenderBack(_backStyle, CardSize);

    public static Bitmap RenderBack(CardBackStyle style, Size size) =>
        Render(size, graphics => DrawBack(graphics, size, style));

    public static Bitmap RenderFace(Suit suit, int rank, Size size) =>
        Render(size, graphics => DrawFace(graphics, size, suit, rank));

    public void Dispose() => Clear();

    private void Clear()
    {
        foreach (Bitmap bitmap in _faces.Values)
        {
            bitmap.Dispose();
        }

        _faces.Clear();
        _back?.Dispose();
        _back = null;
    }

    private static Bitmap Render(Size size, Action<Graphics> draw)
    {
        Bitmap bitmap = new(Math.Max(1, size.Width), Math.Max(1, size.Height), PixelFormat.Format32bppPArgb);
        using Graphics graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.Clear(Color.Transparent);
        draw(graphics);
        return bitmap;
    }

    private static float CornerRadius(Size size) => size.Width * 0.075f;

    private static RectangleF Outline(Size size) => new(0.5f, 0.5f, size.Width - 1f, size.Height - 1f);

    private static void DrawFace(Graphics graphics, Size size, Suit suit, int rank)
    {
        float w = size.Width;
        float h = size.Height;

        using (GraphicsPath outline = Shapes.RoundedRectangle(Outline(size), CornerRadius(size)))
        using (LinearGradientBrush paper = new(new PointF(0, 0), new PointF(0, h), Color.White, Color.FromArgb(240, 240, 236)))
        using (Pen edge = new(Color.FromArgb(120, 120, 128), Math.Max(1f, w / 110f)))
        {
            graphics.FillPath(paper, outline);
            graphics.DrawPath(edge, outline);
        }

        Color ink = suit is Suit.Hearts or Suit.Diamonds ? Red : Black;
        using SolidBrush brush = new(ink);

        DrawIndex(graphics, size, suit, rank, brush);
        GraphicsState state = graphics.Save();
        graphics.TranslateTransform(w, h);
        graphics.RotateTransform(180);
        DrawIndex(graphics, size, suit, rank, brush);
        graphics.Restore(state);

        if (rank >= Card.Jack)
        {
            DrawCourt(graphics, size, suit, rank, ink, brush);
        }
        else if (rank == Card.Ace)
        {
            float pip = w * (suit == Suit.Spades ? 0.48f : 0.4f);
            SuitShapes.Fill(graphics, suit, new RectangleF((w - pip) / 2, (h - pip) / 2, pip, pip), brush);
        }
        else
        {
            DrawPips(graphics, size, suit, rank, brush);
        }
    }

    /// <summary>The rank and a small suit side by side in the corner, so both stay visible when cards overlap tightly.</summary>
    private static void DrawIndex(Graphics graphics, Size size, Suit suit, int rank, Brush brush)
    {
        float w = size.Width;
        float h = size.Height;
        float capHeight = h * 0.118f;
        float left = w * 0.07f;
        float top = h * 0.045f;

        string text = rank switch
        {
            Card.Ace => "A",
            Card.Jack => "J",
            Card.Queen => "Q",
            Card.King => "K",
            _ => rank.ToString(System.Globalization.CultureInfo.InvariantCulture),
        };

        using GraphicsPath path = TextPath(text, IndexFamily, FontStyle.Bold);
        RectangleF bounds = path.GetBounds();
        float scale = capHeight / CapitalBounds.Height;

        // "10" is squeezed so it takes no more room than two narrow letters.
        float scaleX = Math.Min(scale, w * 0.2f / bounds.Width);
        using (Matrix place = new())
        {
            place.Translate(left, top);
            place.Scale(scaleX, scale);
            place.Translate(-bounds.Left, -CapitalBounds.Top);
            path.Transform(place);
        }

        graphics.FillPath(brush, path);

        float pip = h * 0.098f;
        float pipLeft = left + bounds.Width * scaleX + w * 0.03f;
        SuitShapes.Fill(graphics, suit, new RectangleF(pipLeft, top + (capHeight - pip) / 2, pip, pip), brush);
    }

    private static void DrawPips(Graphics graphics, Size size, Suit suit, int rank, Brush brush)
    {
        float w = size.Width;
        float h = size.Height;
        float pip = w * 0.19f;
        float top = h * 0.255f;
        float bottom = h * 0.745f;

        foreach ((float column, float row) in PipLayout(rank))
        {
            float x = w * (0.5f + (column - 0.5f) * 0.42f);
            float y = top + (bottom - top) * row;

            // Pips in the lower half point the other way, as on printed cards.
            bool upsideDown = row > 0.5f;
            SuitShapes.Fill(graphics, suit, new RectangleF(x - pip / 2, y - pip / 2, pip, pip), brush, upsideDown);
        }
    }

    /// <summary>Pip centres for 2-10: column 0 / 0.5 / 1 across, row 0-1 down.</summary>
    private static IEnumerable<(float Column, float Row)> PipLayout(int rank)
    {
        const float L = 0f, M = 0.5f, R = 1f;

        return rank switch
        {
            2 => new[] { (M, 0f), (M, 1f) },
            3 => new[] { (M, 0f), (M, 0.5f), (M, 1f) },
            4 => new[] { (L, 0f), (R, 0f), (L, 1f), (R, 1f) },
            5 => new[] { (L, 0f), (R, 0f), (M, 0.5f), (L, 1f), (R, 1f) },
            6 => new[] { (L, 0f), (R, 0f), (L, 0.5f), (R, 0.5f), (L, 1f), (R, 1f) },
            7 => new[] { (L, 0f), (R, 0f), (M, 0.25f), (L, 0.5f), (R, 0.5f), (L, 1f), (R, 1f) },
            8 => new[] { (L, 0f), (R, 0f), (M, 0.25f), (L, 0.5f), (R, 0.5f), (M, 0.75f), (L, 1f), (R, 1f) },
            9 => new[] { (L, 0f), (R, 0f), (L, 1 / 3f), (R, 1 / 3f), (M, 0.5f), (L, 2 / 3f), (R, 2 / 3f), (L, 1f), (R, 1f) },
            _ => new[] { (L, 0f), (R, 0f), (M, 1 / 6f), (L, 1 / 3f), (R, 1 / 3f), (L, 2 / 3f), (R, 2 / 3f), (M, 5 / 6f), (L, 1f), (R, 1f) },
        };
    }

    /// <summary>
    /// Jack, Queen and King: a framed panel with a chess piece standing in for the
    /// portrait, mirrored top and bottom like a printed court card.
    /// </summary>
    private static void DrawCourt(Graphics graphics, Size size, Suit suit, int rank, Color ink, Brush brush)
    {
        float w = size.Width;
        float h = size.Height;
        RectangleF frame = RectangleF.FromLTRB(w * 0.17f, h * 0.2f, w * 0.83f, h * 0.8f);
        float radius = w * 0.04f;

        Color tint = suit is Suit.Hearts or Suit.Diamonds ? Color.FromArgb(253, 232, 222) : Color.FromArgb(226, 232, 246);
        using (GraphicsPath panel = Shapes.RoundedRectangle(frame, radius))
        using (LinearGradientBrush fill = new(frame, Color.FromArgb(255, 250, 232), tint, LinearGradientMode.Vertical))
        using (Pen border = new(ink, Math.Max(1f, w / 70f)))
        {
            graphics.FillPath(fill, panel);
            graphics.DrawPath(border, panel);
        }

        RectangleF inner = RectangleF.Inflate(frame, -w * 0.025f, -w * 0.025f);
        using (GraphicsPath innerPanel = Shapes.RoundedRectangle(inner, radius * 0.6f))
        using (Pen line = new(Color.FromArgb(150, ink), Math.Max(0.6f, w / 160f)))
        {
            graphics.DrawPath(line, innerPanel);
            graphics.DrawLine(line, inner.Left + w * 0.04f, frame.Top + frame.Height / 2, inner.Right - w * 0.04f, frame.Top + frame.Height / 2);
        }

        string piece = rank switch
        {
            Card.King => "♚",
            Card.Queen => "♛",
            _ => "♞",
        };

        float pip = w * 0.13f;
        // The piece stays clear of the small suit in the frame's corner.
        RectangleF half = RectangleF.FromLTRB(inner.Left + pip * 1.25f, inner.Top + h * 0.025f, inner.Right - pip * 1.25f, frame.Top + frame.Height / 2 - h * 0.02f);

        for (int turn = 0; turn < 2; turn++)
        {
            GraphicsState state = graphics.Save();
            if (turn == 1)
            {
                graphics.TranslateTransform(w, h);
                graphics.RotateTransform(180);
            }

            if (!FillText(graphics, piece, SymbolFamily, FontStyle.Regular, half, brush))
            {
                // No symbol font: fall back to the letter.
                FillText(graphics, rank == Card.King ? "K" : rank == Card.Queen ? "Q" : "J", IndexFamily, FontStyle.Bold, half, brush);
            }

            SuitShapes.Fill(graphics, suit, new RectangleF(inner.Left + w * 0.035f, inner.Top + w * 0.035f, pip, pip), brush);
            graphics.Restore(state);
        }
    }

    private static void DrawBack(Graphics graphics, Size size, CardBackStyle style)
    {
        float w = size.Width;
        float h = size.Height;

        using (GraphicsPath outline = Shapes.RoundedRectangle(Outline(size), CornerRadius(size)))
        using (SolidBrush paper = new(Color.FromArgb(250, 250, 248)))
        using (Pen edge = new(Color.FromArgb(110, 110, 118), Math.Max(1f, w / 110f)))
        {
            graphics.FillPath(paper, outline);
            graphics.DrawPath(edge, outline);
        }

        float margin = w * 0.065f;
        RectangleF inner = RectangleF.FromLTRB(margin, margin, w - margin, h - margin);
        using GraphicsPath panel = Shapes.RoundedRectangle(inner, CornerRadius(size) * 0.6f);

        Color baseColor = style switch
        {
            CardBackStyle.ClassicBlue => Color.FromArgb(28, 70, 160),
            CardBackStyle.ClassicRed => Color.FromArgb(168, 28, 40),
            CardBackStyle.Emerald => Color.FromArgb(14, 112, 74),
            CardBackStyle.SpiderWeb => Color.FromArgb(62, 50, 104),
            _ => Color.FromArgb(18, 24, 52),
        };

        using (LinearGradientBrush fill = new(inner, Shapes.Lighten(baseColor, 0.12f), Shapes.Darken(baseColor, 0.18f), LinearGradientMode.ForwardDiagonal))
        {
            graphics.FillPath(fill, panel);
        }

        GraphicsState state = graphics.Save();
        graphics.SetClip(panel);

        switch (style)
        {
            case CardBackStyle.ClassicBlue:
            case CardBackStyle.ClassicRed:
                DrawLattice(graphics, inner, Shapes.Lighten(baseColor, 0.45f), w);
                break;
            case CardBackStyle.Emerald:
                DrawDiamonds(graphics, inner, Shapes.Lighten(baseColor, 0.3f), w);
                break;
            case CardBackStyle.SpiderWeb:
                DrawWeb(graphics, inner, w);
                break;
            default:
                DrawMidnight(graphics, inner, w);
                break;
        }

        graphics.Restore(state);

        Color trim = style == CardBackStyle.Midnight ? Color.FromArgb(214, 178, 92) : Color.FromArgb(200, 255, 255, 255);
        RectangleF trimRect = RectangleF.Inflate(inner, -w * 0.03f, -w * 0.03f);
        using GraphicsPath trimPath = Shapes.RoundedRectangle(trimRect, CornerRadius(size) * 0.4f);
        using Pen trimPen = new(trim, Math.Max(1f, w / 90f));
        graphics.DrawPath(trimPen, trimPath);
    }

    private static void DrawLattice(Graphics graphics, RectangleF area, Color color, float cardWidth)
    {
        float spacing = cardWidth * 0.09f;
        using Pen pen = new(Color.FromArgb(150, color), Math.Max(1f, cardWidth / 85f));
        float span = area.Width + area.Height;

        for (float offset = -span; offset < span; offset += spacing)
        {
            graphics.DrawLine(pen, area.Left + offset, area.Top, area.Left + offset + area.Height, area.Bottom);
            graphics.DrawLine(pen, area.Left + offset, area.Bottom, area.Left + offset + area.Height, area.Top);
        }

        using SolidBrush dot = new(Color.FromArgb(170, color));
        float size = cardWidth * 0.022f;
        for (float y = area.Top; y < area.Bottom + spacing; y += spacing)
        {
            for (float x = area.Left; x < area.Right + spacing; x += spacing)
            {
                graphics.FillEllipse(dot, x + spacing / 2 - size / 2, y + spacing / 2 - size / 2, size, size);
            }
        }
    }

    private static void DrawDiamonds(Graphics graphics, RectangleF area, Color color, float cardWidth)
    {
        float spacing = cardWidth * 0.14f;
        using SolidBrush fill = new(Color.FromArgb(120, color));
        using Pen pen = new(Color.FromArgb(170, color), Math.Max(1f, cardWidth / 120f));

        int row = 0;
        for (float y = area.Top - spacing; y < area.Bottom + spacing; y += spacing * 0.5f, row++)
        {
            float shift = row % 2 == 0 ? 0 : spacing / 2;
            for (float x = area.Left - spacing + shift; x < area.Right + spacing; x += spacing)
            {
                float r = spacing * 0.3f;
                PointF[] diamond =
                {
                    new(x, y - r),
                    new(x + r * 0.8f, y),
                    new(x, y + r),
                    new(x - r * 0.8f, y),
                };

                graphics.FillPolygon(fill, diamond);
                graphics.DrawPolygon(pen, diamond);
            }
        }
    }

    private static void DrawWeb(Graphics graphics, RectangleF area, float cardWidth)
    {
        PointF centre = new(area.Left + area.Width / 2, area.Top + area.Height * 0.46f);
        float reach = Math.Max(area.Width, area.Height);
        const int spokes = 14;

        using Pen thread = new(Color.FromArgb(150, 225, 225, 240), Math.Max(0.8f, cardWidth / 140f));

        PointF Spoke(int index, float radius)
        {
            double angle = Math.PI * 2 * index / spokes - Math.PI / 2;
            return new PointF(centre.X + (float)Math.Cos(angle) * radius, centre.Y + (float)Math.Sin(angle) * radius);
        }

        for (int i = 0; i < spokes; i++)
        {
            graphics.DrawLine(thread, centre, Spoke(i, reach));
        }

        // Each ring sags slightly towards the centre between spokes, as a real web does.
        for (float radius = reach * 0.1f; radius < reach; radius *= 1.38f)
        {
            for (int i = 0; i < spokes; i++)
            {
                PointF from = Spoke(i, radius);
                PointF to = Spoke(i + 1, radius);
                PointF sag = Spoke(i, radius * 0.86f);
                PointF sagNext = Spoke(i + 1, radius * 0.86f);
                graphics.DrawBezier(
                    thread,
                    from,
                    new PointF(from.X + (sag.X - from.X) * 0.5f + (to.X - from.X) * 0.3f, from.Y + (sag.Y - from.Y) * 0.5f + (to.Y - from.Y) * 0.3f),
                    new PointF(to.X + (sagNext.X - to.X) * 0.5f + (from.X - to.X) * 0.3f, to.Y + (sagNext.Y - to.Y) * 0.5f + (from.Y - to.Y) * 0.3f),
                    to);
            }
        }

        // The spider, hanging just below the hub.
        float body = cardWidth * 0.13f;
        PointF at = new(centre.X, centre.Y + body * 0.6f);
        using Pen leg = new(Color.FromArgb(20, 18, 24), Math.Max(1f, cardWidth / 60f)) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        using Pen silk = new(Color.FromArgb(200, 235, 235, 245), Math.Max(0.8f, cardWidth / 150f));
        graphics.DrawLine(silk, centre, at);

        for (int side = -1; side <= 1; side += 2)
        {
            for (int i = 0; i < 4; i++)
            {
                float spread = (i - 1.5f) * body * 0.42f;
                PointF hip = new(at.X + side * body * 0.2f, at.Y + spread * 0.35f);
                PointF knee = new(at.X + side * body * 0.75f, at.Y + spread - body * 0.35f);
                PointF foot = new(at.X + side * body * 1.05f, at.Y + spread * 1.6f + body * 0.25f);
                graphics.DrawLines(leg, new[] { hip, knee, foot });
            }
        }

        using SolidBrush dark = new(Color.FromArgb(20, 18, 24));
        graphics.FillEllipse(dark, at.X - body * 0.32f, at.Y - body * 0.1f, body * 0.64f, body * 0.78f);
        graphics.FillEllipse(dark, at.X - body * 0.2f, at.Y - body * 0.36f, body * 0.4f, body * 0.36f);
        using SolidBrush mark = new(Color.FromArgb(200, 30, 40));
        graphics.FillEllipse(mark, at.X - body * 0.07f, at.Y + body * 0.18f, body * 0.14f, body * 0.22f);
    }

    private static void DrawMidnight(Graphics graphics, RectangleF area, float cardWidth)
    {
        Random stars = new(7);
        using SolidBrush star = new(Color.FromArgb(150, 230, 225, 255));
        for (int i = 0; i < 40; i++)
        {
            float size = cardWidth * (0.008f + (float)stars.NextDouble() * 0.014f);
            graphics.FillEllipse(star, area.Left + (float)stars.NextDouble() * area.Width, area.Top + (float)stars.NextDouble() * area.Height, size, size);
        }

        Color gold = Color.FromArgb(214, 178, 92);
        float emblem = area.Width * 0.46f;
        using SolidBrush brush = new(gold);
        SuitShapes.Fill(graphics, Suit.Spades, new RectangleF(area.Left + (area.Width - emblem) / 2, area.Top + (area.Height - emblem) / 2, emblem, emblem), brush);

        using Pen ring = new(Color.FromArgb(140, gold), Math.Max(1f, cardWidth / 120f));
        float circle = area.Width * 0.72f;
        graphics.DrawEllipse(ring, area.Left + (area.Width - circle) / 2, area.Top + (area.Height - circle) / 2, circle, circle);
    }

    private static GraphicsPath TextPath(string text, FontFamily family, FontStyle style)
    {
        GraphicsPath path = new();
        path.AddString(text, family, (int)style, 100f, PointF.Empty, StringFormat.GenericTypographic);
        return path;
    }

    private static RectangleF TextBounds(string text, FontFamily family, FontStyle style)
    {
        using GraphicsPath path = TextPath(text, family, style);
        return path.GetBounds();
    }

    /// <summary>Fills text scaled to fit and centred in <paramref name="area"/>. Returns false if the font has no outline for it.</summary>
    private static bool FillText(Graphics graphics, string text, FontFamily family, FontStyle style, RectangleF area, Brush brush)
    {
        using GraphicsPath path = TextPath(text, family, style);
        RectangleF bounds = path.GetBounds();
        if (path.PointCount == 0 || bounds.Width <= 0 || bounds.Height <= 0)
        {
            return false;
        }

        float scale = Math.Min(area.Width / bounds.Width, area.Height / bounds.Height);
        using (Matrix place = new())
        {
            place.Translate(area.Left + (area.Width - bounds.Width * scale) / 2, area.Top + (area.Height - bounds.Height * scale) / 2);
            place.Scale(scale, scale);
            place.Translate(-bounds.Left, -bounds.Top);
            path.Transform(place);
        }

        graphics.FillPath(brush, path);
        return true;
    }
}
