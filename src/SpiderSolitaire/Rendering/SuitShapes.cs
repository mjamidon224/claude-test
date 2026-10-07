using System.Drawing.Drawing2D;
using SpiderSolitaire.Core;

namespace SpiderSolitaire.Rendering;

/// <summary>
/// The four suit symbols as vector shapes in a unit square, so they stay crisp at any
/// card size and do not depend on which symbol fonts are installed.
/// </summary>
/// <remarks>
/// A shape is a list of parts filled one after another rather than one combined path:
/// the parts overlap, and with a single path the overlap would depend on each part's
/// winding direction.
/// </remarks>
internal static class SuitShapes
{
    private static readonly GraphicsPath[] Heart = { HeartPath() };
    private static readonly GraphicsPath[] Diamond = { DiamondPath() };
    private static readonly GraphicsPath[] Spade = { SpadeBody(), Stem(0.55f) };
    private static readonly GraphicsPath[] Club =
    {
        Circle(0.5f, 0.25f, 0.235f),
        Circle(0.255f, 0.58f, 0.235f),
        Circle(0.745f, 0.58f, 0.235f),
        Circle(0.5f, 0.55f, 0.13f),
        Stem(0.6f),
    };

    /// <summary>Fills a suit symbol into <paramref name="bounds"/>, optionally turned upside down.</summary>
    public static void Fill(Graphics graphics, Suit suit, RectangleF bounds, Brush brush, bool upsideDown = false)
    {
        GraphicsState state = graphics.Save();
        graphics.TranslateTransform(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2);
        if (upsideDown)
        {
            graphics.RotateTransform(180);
        }

        graphics.ScaleTransform(bounds.Width, bounds.Height);
        graphics.TranslateTransform(-0.5f, -0.5f);

        foreach (GraphicsPath part in Parts(suit))
        {
            graphics.FillPath(brush, part);
        }

        graphics.Restore(state);
    }

    private static GraphicsPath[] Parts(Suit suit) => suit switch
    {
        Suit.Spades => Spade,
        Suit.Hearts => Heart,
        Suit.Clubs => Club,
        _ => Diamond,
    };

    private static GraphicsPath HeartPath()
    {
        GraphicsPath path = new();
        path.AddBezier(0.5f, 0.2f, 0.5f, 0.12f, 0.43f, 0f, 0.26f, 0f);
        path.AddBezier(0.26f, 0f, 0.08f, 0f, 0f, 0.15f, 0f, 0.32f);
        path.AddBezier(0f, 0.32f, 0f, 0.55f, 0.25f, 0.78f, 0.5f, 1f);
        path.AddBezier(0.5f, 1f, 0.75f, 0.78f, 1f, 0.55f, 1f, 0.32f);
        path.AddBezier(1f, 0.32f, 1f, 0.15f, 0.92f, 0f, 0.74f, 0f);
        path.AddBezier(0.74f, 0f, 0.57f, 0f, 0.5f, 0.12f, 0.5f, 0.2f);
        path.CloseFigure();
        return path;
    }

    private static GraphicsPath DiamondPath()
    {
        GraphicsPath path = new();
        path.AddBezier(0.5f, 0f, 0.62f, 0.18f, 0.74f, 0.34f, 0.9f, 0.5f);
        path.AddBezier(0.9f, 0.5f, 0.74f, 0.66f, 0.62f, 0.82f, 0.5f, 1f);
        path.AddBezier(0.5f, 1f, 0.38f, 0.82f, 0.26f, 0.66f, 0.1f, 0.5f);
        path.AddBezier(0.1f, 0.5f, 0.26f, 0.34f, 0.38f, 0.18f, 0.5f, 0f);
        path.CloseFigure();
        return path;
    }

    /// <summary>An upside-down heart over the top four-fifths of the square.</summary>
    private static GraphicsPath SpadeBody()
    {
        GraphicsPath path = HeartPath();
        using Matrix flip = new(1f, 0f, 0f, -0.8f, 0f, 0.8f);
        path.Transform(flip);
        return path;
    }

    /// <summary>The flared foot shared by spades and clubs, from <paramref name="top"/> down to the bottom edge.</summary>
    private static GraphicsPath Stem(float top)
    {
        GraphicsPath path = new();
        path.AddBezier(0.5f, top, 0.52f, 0.8f, 0.6f, 0.95f, 0.73f, 1f);
        path.AddLine(0.73f, 1f, 0.27f, 1f);
        path.AddBezier(0.27f, 1f, 0.4f, 0.95f, 0.48f, 0.8f, 0.5f, top);
        path.CloseFigure();
        return path;
    }

    private static GraphicsPath Circle(float centerX, float centerY, float radius)
    {
        GraphicsPath path = new();
        path.AddEllipse(centerX - radius, centerY - radius, radius * 2, radius * 2);
        return path;
    }
}
