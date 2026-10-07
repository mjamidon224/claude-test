using SpiderSolitaire.Core;
using SpiderSolitaire.Rendering;

namespace SpiderSolitaire;

/// <summary>Everything the game remembers between runs apart from statistics and the saved game.</summary>
internal sealed class AppSettings
{
    public Difficulty Difficulty { get; set; } = Difficulty.OneSuit;

    public bool Animations { get; set; } = true;

    public bool Sounds { get; set; } = true;

    /// <summary>Resume a saved game at startup without asking.</summary>
    public bool AlwaysContinueSavedGame { get; set; }

    /// <summary>Save an unfinished game on exit without asking.</summary>
    public bool AlwaysSaveOnExit { get; set; }

    public CardBackStyle CardBack { get; set; } = CardBackStyle.ClassicBlue;

    public TableStyle Table { get; set; } = TableStyle.Green;

    /// <summary>The window's normal (not maximised) position and size, or null to centre it.</summary>
    public WindowBounds? Window { get; set; }

    public bool WindowMaximized { get; set; }

    /// <summary>Puts anything a hand-edited or newer file got wrong back to its default.</summary>
    public void Normalize()
    {
        if (!Difficulty.IsDefined())
        {
            Difficulty = Difficulty.OneSuit;
        }

        if (!Enum.IsDefined(CardBack))
        {
            CardBack = CardBackStyle.ClassicBlue;
        }

        if (!Enum.IsDefined(Table))
        {
            Table = TableStyle.Green;
        }
    }
}

/// <summary>A window rectangle as four plain numbers, which JSON round-trips more cleanly than <see cref="Rectangle"/>.</summary>
internal sealed class WindowBounds
{
    public int X { get; set; }

    public int Y { get; set; }

    public int Width { get; set; }

    public int Height { get; set; }

    public Rectangle ToRectangle() => new(X, Y, Width, Height);

    public static WindowBounds From(Rectangle rectangle) => new()
    {
        X = rectangle.X,
        Y = rectangle.Y,
        Width = rectangle.Width,
        Height = rectangle.Height,
    };
}
