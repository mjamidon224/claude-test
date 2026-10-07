namespace SpiderSolitaire.Core;

public enum Suit
{
    Spades,
    Hearts,
    Clubs,
    Diamonds,
}

/// <summary>
/// One of the 104 cards in play. Spider uses two decks, and at the easier difficulties
/// eight copies of the same suit, so <see cref="Id"/> is what tells duplicates apart.
/// </summary>
public readonly record struct Card(int Id, Suit Suit, int Rank)
{
    public const int Ace = 1;
    public const int Jack = 11;
    public const int Queen = 12;
    public const int King = 13;

    public bool IsRed => Suit is Suit.Hearts or Suit.Diamonds;

    public string RankText => Rank switch
    {
        Ace => "A",
        Jack => "J",
        Queen => "Q",
        King => "K",
        _ => Rank.ToString(System.Globalization.CultureInfo.InvariantCulture),
    };

    public override string ToString() => RankText + "SHCD"[(int)Suit];
}

/// <summary>A card on the tableau, which is the only place a card can be face down.</summary>
public readonly record struct TableauCard(Card Card, bool FaceUp)
{
    public TableauCard Flipped(bool faceUp) => this with { FaceUp = faceUp };

    public override string ToString() => FaceUp ? Card.ToString() : $"({Card})";
}
