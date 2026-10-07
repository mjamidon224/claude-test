using System.Collections.Immutable;

namespace SpiderSolitaire.Core;

/// <summary>
/// Where every card is at one moment. Boards are immutable: each change produces a new
/// board, which is what makes undo a stack of boards and lets the UI animate between them.
/// </summary>
public sealed class Board
{
    public const int ColumnCount = 10;
    public const int SuitsToWin = 8;
    public const int InitialTableauCards = 54;

    public Board(
        Difficulty difficulty,
        ImmutableArray<ImmutableArray<TableauCard>> columns,
        ImmutableArray<Card> stock,
        ImmutableArray<ImmutableArray<Card>> foundations)
    {
        if (columns.Length != ColumnCount)
        {
            throw new ArgumentException($"A board has {ColumnCount} columns.", nameof(columns));
        }

        Difficulty = difficulty;
        Columns = columns;
        Stock = stock;
        Foundations = foundations;
    }

    public Difficulty Difficulty { get; }

    public ImmutableArray<ImmutableArray<TableauCard>> Columns { get; }

    /// <summary>
    /// Cards not yet dealt. The last <see cref="ColumnCount"/> are the next row, with
    /// <c>Stock[^10]</c> going to the first column and <c>Stock[^1]</c> to the last.
    /// </summary>
    public ImmutableArray<Card> Stock { get; }

    /// <summary>Completed suits in the order they were finished, each listed Ace first and King last.</summary>
    public ImmutableArray<ImmutableArray<Card>> Foundations { get; }

    public int RowsLeftToDeal => (Stock.Length + ColumnCount - 1) / ColumnCount;

    public bool IsWon => Foundations.Length == SuitsToWin;

    public bool HasEmptyColumn => Columns.Any(column => column.IsEmpty);

    public int TableauCardCount => Columns.Sum(column => column.Length);

    /// <summary>The opening layout: 54 cards in ten columns (four of six, six of five), top cards face up.</summary>
    public static Board Deal(Difficulty difficulty, IReadOnlyList<Card> shuffled)
    {
        if (shuffled.Count != Deck.CardCount)
        {
            throw new ArgumentException($"A game uses {Deck.CardCount} cards.", nameof(shuffled));
        }

        List<TableauCard>[] columns = Enumerable.Range(0, ColumnCount).Select(_ => new List<TableauCard>()).ToArray();

        // Dealt a row at a time, as you would by hand, so the extra four cards land on the left.
        for (int i = 0; i < InitialTableauCards; i++)
        {
            columns[i % ColumnCount].Add(new TableauCard(shuffled[i], FaceUp: false));
        }

        foreach (List<TableauCard> column in columns)
        {
            column[^1] = column[^1].Flipped(true);
        }

        return new Board(
            difficulty,
            columns.Select(column => column.ToImmutableArray()).ToImmutableArray(),
            shuffled.Skip(InitialTableauCards).ToImmutableArray(),
            ImmutableArray<ImmutableArray<Card>>.Empty);
    }

    /// <summary>
    /// The index of the first card in the run that can be picked up from a column: the
    /// longest face-up stretch at the bottom that descends by one in a single suit.
    /// Returns -1 for an empty column.
    /// </summary>
    public int RunStart(int column)
    {
        ImmutableArray<TableauCard> cards = Columns[column];
        if (cards.IsEmpty || !cards[^1].FaceUp)
        {
            return -1;
        }

        int start = cards.Length - 1;
        while (start > 0 && Continues(cards[start - 1], cards[start]))
        {
            start--;
        }

        return start;
    }

    /// <summary>True when <paramref name="lower"/> can sit on <paramref name="upper"/> within a movable run.</summary>
    private static bool Continues(TableauCard upper, TableauCard lower) =>
        upper.FaceUp && upper.Card.Suit == lower.Card.Suit && upper.Card.Rank == lower.Card.Rank + 1;

    public bool CanPickUp(int column, int index)
    {
        if (column is < 0 or >= ColumnCount)
        {
            return false;
        }

        int start = RunStart(column);
        return start >= 0 && index >= start && index < Columns[column].Length;
    }

    /// <summary>Any card can go into an empty column; otherwise it needs a card one rank higher, of any suit.</summary>
    public bool CanPlace(Card card, int column)
    {
        ImmutableArray<TableauCard> cards = Columns[column];
        if (cards.IsEmpty)
        {
            return true;
        }

        TableauCard top = cards[^1];
        return top.FaceUp && top.Card.Rank == card.Rank + 1;
    }

    public bool CanMove(int fromColumn, int index, int toColumn) =>
        fromColumn != toColumn
        && toColumn is >= 0 and < ColumnCount
        && CanPickUp(fromColumn, index)
        && CanPlace(Columns[fromColumn][index].Card, toColumn);

    /// <summary>
    /// Whether a new row can be dealt. The classic rule refuses while any column is empty;
    /// the exception is when there are too few cards left on the table to fill every
    /// column, since the game could otherwise never be finished.
    /// </summary>
    public DealBlocker DealBlocker
    {
        get
        {
            if (Stock.IsEmpty)
            {
                return DealBlocker.StockEmpty;
            }

            return HasEmptyColumn && TableauCardCount >= ColumnCount ? DealBlocker.EmptyColumn : DealBlocker.None;
        }
    }

    /// <summary>
    /// Where a clicked run should go: onto the same suit if possible, then onto any suit,
    /// then into an empty column. Ties go to the nearest column to the right, wrapping
    /// round. Returns null when there is nowhere useful to go, which includes moving a
    /// whole column into an empty one.
    /// </summary>
    public int? FindBestTarget(int fromColumn, int index)
    {
        if (!CanPickUp(fromColumn, index))
        {
            return null;
        }

        Card card = Columns[fromColumn][index].Card;
        int? best = null;
        int bestScore = 0;

        for (int step = 1; step < ColumnCount; step++)
        {
            int target = (fromColumn + step) % ColumnCount;
            if (!CanPlace(card, target))
            {
                continue;
            }

            ImmutableArray<TableauCard> targetCards = Columns[target];
            int score = targetCards.IsEmpty
                ? index == 0 ? 0 : 1
                : targetCards[^1].Card.Suit == card.Suit ? 3 : 2;

            if (score > bestScore)
            {
                best = target;
                bestScore = score;
            }
        }

        return best;
    }

    internal Board MoveRun(int fromColumn, int index, int toColumn)
    {
        ImmutableArray<TableauCard> from = Columns[fromColumn];
        ImmutableArray<TableauCard> moving = from.RemoveRange(0, index);

        ImmutableArray<ImmutableArray<TableauCard>> columns = Columns
            .SetItem(fromColumn, from.RemoveRange(index, from.Length - index))
            .SetItem(toColumn, Columns[toColumn].AddRange(moving));

        return new Board(Difficulty, columns, Stock, Foundations);
    }

    /// <summary>Turns over any face-down card left at the bottom of a column. Returns this board if there was none.</summary>
    internal Board FlipExposedCards()
    {
        ImmutableArray<ImmutableArray<TableauCard>> columns = Columns;
        for (int i = 0; i < ColumnCount; i++)
        {
            ImmutableArray<TableauCard> cards = columns[i];
            if (!cards.IsEmpty && !cards[^1].FaceUp)
            {
                columns = columns.SetItem(i, cards.SetItem(cards.Length - 1, cards[^1].Flipped(true)));
            }
        }

        return columns == Columns ? this : new Board(Difficulty, columns, Stock, Foundations);
    }

    /// <summary>
    /// Moves one finished King-to-Ace run of a single suit off the table, from the
    /// leftmost column that has one. Returns this board if there was none.
    /// </summary>
    internal Board CollectCompletedRun()
    {
        for (int i = 0; i < ColumnCount; i++)
        {
            ImmutableArray<TableauCard> cards = Columns[i];
            if (cards.Length < Deck.CardsPerSuit || cards[^1].Card.Rank != Card.Ace)
            {
                continue;
            }

            int start = RunStart(i);
            int kingIndex = cards.Length - Deck.CardsPerSuit;
            if (start < 0 || start > kingIndex)
            {
                continue;
            }

            // The column reads King down to Ace; the foundation is stored Ace first.
            ImmutableArray<Card> suit = cards.RemoveRange(0, kingIndex).Select(card => card.Card).Reverse().ToImmutableArray();

            return new Board(
                Difficulty,
                Columns.SetItem(i, cards.RemoveRange(kingIndex, Deck.CardsPerSuit)),
                Stock,
                Foundations.Add(suit));
        }

        return this;
    }

    /// <summary>Deals one face-up card onto every column from the stock.</summary>
    internal Board DealRow()
    {
        int rowStart = Stock.Length - ColumnCount;
        ImmutableArray<ImmutableArray<TableauCard>> columns = Columns;
        for (int i = 0; i < ColumnCount; i++)
        {
            columns = columns.SetItem(i, columns[i].Add(new TableauCard(Stock[rowStart + i], FaceUp: true)));
        }

        return new Board(Difficulty, columns, Stock.RemoveRange(rowStart, ColumnCount), Foundations);
    }

    public override string ToString() =>
        string.Join(Environment.NewLine, Columns.Select((column, i) => $"{i}: {string.Join(' ', column)}"))
        + $"{Environment.NewLine}stock {Stock.Length}, completed {Foundations.Length}";
}

public enum DealBlocker
{
    None,
    StockEmpty,
    EmptyColumn,
}
