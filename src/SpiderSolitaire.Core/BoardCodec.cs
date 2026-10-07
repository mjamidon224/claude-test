using System.Collections.Immutable;
using System.Globalization;
using System.Text;

namespace SpiderSolitaire.Core;

/// <summary>
/// Writes a board as a short string for the saved game, so the undo history stays small.
/// Each card is two hex digits: its id, plus 0x80 if face up. Columns are separated by
/// commas, and the sections (columns / stock / foundations) by slashes:
/// <c>0a8b,...,1c9d/2e3f.../00010203...,0d0e...</c>.
/// </summary>
public static class BoardCodec
{
    private const int FaceUpFlag = 0x80;

    public static string Encode(Board board)
    {
        StringBuilder text = new(400);

        for (int i = 0; i < board.Columns.Length; i++)
        {
            if (i > 0)
            {
                text.Append(',');
            }

            foreach (TableauCard card in board.Columns[i])
            {
                AppendHex(text, card.Card.Id | (card.FaceUp ? FaceUpFlag : 0));
            }
        }

        text.Append('/');
        foreach (Card card in board.Stock)
        {
            AppendHex(text, card.Id);
        }

        text.Append('/');
        for (int i = 0; i < board.Foundations.Length; i++)
        {
            if (i > 0)
            {
                text.Append(',');
            }

            foreach (Card card in board.Foundations[i])
            {
                AppendHex(text, card.Id);
            }
        }

        return text.ToString();
    }

    /// <summary>
    /// Rebuilds a board, checking that it holds each of the 104 cards exactly once, that
    /// the stock is whole rows and that every completed suit really is Ace to King of one
    /// suit. Throws <see cref="FormatException"/> otherwise.
    /// </summary>
    public static Board Decode(string text, Difficulty difficulty)
    {
        string[] sections = text.Split('/');
        if (sections.Length != 3)
        {
            throw new FormatException("A board has three sections.");
        }

        bool[] seen = new bool[Deck.CardCount];

        string[] columnTexts = sections[0].Split(',');
        if (columnTexts.Length != Board.ColumnCount)
        {
            throw new FormatException($"A board has {Board.ColumnCount} columns.");
        }

        ImmutableArray<ImmutableArray<TableauCard>> columns = columnTexts
            .Select(column => ReadValues(column)
                .Select(value => new TableauCard(Take(value & ~FaceUpFlag, difficulty, seen), (value & FaceUpFlag) != 0))
                .ToImmutableArray())
            .ToImmutableArray();

        ImmutableArray<Card> stock = ReadValues(sections[1])
            .Select(value => Take(value, difficulty, seen))
            .ToImmutableArray();
        if (stock.Length % Board.ColumnCount != 0)
        {
            throw new FormatException("The stock must hold whole rows.");
        }

        ImmutableArray<ImmutableArray<Card>> foundations = sections[2].Length == 0
            ? ImmutableArray<ImmutableArray<Card>>.Empty
            : sections[2].Split(',')
                .Select(foundation => ReadValues(foundation).Select(value => Take(value, difficulty, seen)).ToImmutableArray())
                .ToImmutableArray();

        foreach (ImmutableArray<Card> suit in foundations)
        {
            bool complete = suit.Length == Deck.CardsPerSuit
                && suit.Select((card, i) => card.Rank == i + 1 && card.Suit == suit[0].Suit).All(ok => ok);
            if (!complete)
            {
                throw new FormatException("A completed suit must run from Ace to King in one suit.");
            }
        }

        if (seen.Any(present => !present))
        {
            throw new FormatException("Some cards are missing from the board.");
        }

        return new Board(difficulty, columns, stock, foundations);
    }

    private static void AppendHex(StringBuilder text, int value) =>
        text.Append(value.ToString("x2", CultureInfo.InvariantCulture));

    private static IEnumerable<int> ReadValues(string text)
    {
        if (text.Length % 2 != 0)
        {
            throw new FormatException("Cards are written as pairs of hex digits.");
        }

        for (int i = 0; i < text.Length; i += 2)
        {
            if (!int.TryParse(text.AsSpan(i, 2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out int value))
            {
                throw new FormatException($"'{text.Substring(i, 2)}' is not a card.");
            }

            yield return value;
        }
    }

    private static Card Take(int id, Difficulty difficulty, bool[] seen)
    {
        if (id is < 0 or >= Deck.CardCount)
        {
            throw new FormatException($"There is no card {id}.");
        }

        if (seen[id])
        {
            throw new FormatException($"Card {id} appears twice.");
        }

        seen[id] = true;
        return Deck.CardFromId(id, difficulty);
    }
}
