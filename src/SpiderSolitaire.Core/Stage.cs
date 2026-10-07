namespace SpiderSolitaire.Core;

public enum StageKind
{
    /// <summary>A run of cards moved to another column.</summary>
    Move,

    /// <summary>Face-down cards left at the bottom of a column were turned over.</summary>
    Flip,

    /// <summary>A row was dealt from the stock.</summary>
    Deal,

    /// <summary>A finished King-to-Ace suit was cleared off the table.</summary>
    CompleteSuit,

    /// <summary>The board went back to how it was before the last action.</summary>
    Undo,
}

/// <summary>
/// One visible step of an action. A single move can play out as several stages (the move,
/// a card turning over, a finished suit leaving the table), and the UI animates each in turn.
/// </summary>
public sealed record Stage(StageKind Kind, Board Board);
