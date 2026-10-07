using System.Collections.Immutable;

namespace SpiderSolitaire.Core.Tests;

/// <summary>
/// Builds boards from a compact notation: <c>"(KS) QH JH"</c> is a face-down King of
/// spades under a face-up Queen and Jack of hearts. Ranks are A 2-9 T J Q K, suits S H C D.
/// Ids are handed out in order, so these boards need not hold all 104 cards.
/// </summary>
internal static class TestBoards
{
    public static Board Build(string[] columns, int stockRows = 0, int foundations = 0)
    {
        if (columns.Length != Board.ColumnCount)
        {
            throw new ArgumentException("Give all ten columns.", nameof(columns));
        }

        int nextId = 0;

        ImmutableArray<ImmutableArray<TableauCard>> tableau = columns
            .Select(column => column
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(token => ParseTableau(token, ref nextId))
                .ToImmutableArray())
            .ToImmutableArray();

        ImmutableArray<Card> stock = Enumerable.Range(0, stockRows * Board.ColumnCount)
            .Select(i => new Card(nextId++, Suit.Clubs, i % Deck.CardsPerSuit + 1))
            .ToImmutableArray();

        ImmutableArray<ImmutableArray<Card>> completed = Enumerable.Range(0, foundations)
            .Select(_ => Enumerable.Range(1, Deck.CardsPerSuit).Select(rank => new Card(nextId++, Suit.Spades, rank)).ToImmutableArray())
            .ToImmutableArray();

        return new Board(Difficulty.FourSuits, tableau, stock, completed);
    }

    /// <summary>Ten columns, the given ones filled in and the rest holding a single unrelated card.</summary>
    public static string[] Columns(params (int Column, string Cards)[] filled)
    {
        string[] columns = Enumerable.Repeat("(2D) 2D", Board.ColumnCount).ToArray();
        foreach ((int column, string cards) in filled)
        {
            columns[column] = cards;
        }

        return columns;
    }

    public static string FullSuit(char suit, bool faceUp = true) =>
        string.Join(' ', "KQJT98765432A".Select(rank => faceUp ? $"{rank}{suit}" : $"({rank}{suit})"));

    private static TableauCard ParseTableau(string token, ref int nextId)
    {
        bool faceDown = token.StartsWith('(');
        string text = token.Trim('(', ')');
        return new TableauCard(Parse(text, nextId++), !faceDown);
    }

    public static Card Parse(string text, int id)
    {
        int rank = "A23456789TJQK".IndexOf(text[0]) + 1;
        int suit = "SHCD".IndexOf(text[1]);
        if (rank == 0 || suit < 0 || text.Length != 2)
        {
            throw new ArgumentException($"'{text}' is not a card.", nameof(text));
        }

        return new Card(id, (Suit)suit, rank);
    }

    public static string Describe(this ImmutableArray<TableauCard> column) => string.Join(' ', column);
}
