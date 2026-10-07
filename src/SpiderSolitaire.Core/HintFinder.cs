using System.Collections.Immutable;

namespace SpiderSolitaire.Core;

/// <summary>A suggested move: the cards from <see cref="FromIndex"/> down in one column, onto another.</summary>
public sealed record Hint(int FromColumn, int FromIndex, int ToColumn);

/// <summary>
/// Lists the moves worth suggesting, best first. Legal moves that achieve nothing are left
/// out: moving a whole column into an empty one, splitting a same-suit run onto another
/// card, or shifting a run from one card of the right rank to another of a different suit.
/// </summary>
public static class HintFinder
{
    public static IReadOnlyList<Hint> Find(Board board)
    {
        List<(Hint Hint, int Priority)> found = new();

        // Every empty column is as good as any other, so only the first is suggested.
        int firstEmpty = Enumerable.Range(0, Board.ColumnCount).FirstOrDefault(i => board.Columns[i].IsEmpty, -1);

        for (int from = 0; from < Board.ColumnCount; from++)
        {
            int runStart = board.RunStart(from);
            if (runStart < 0)
            {
                continue;
            }

            ImmutableArray<TableauCard> cards = board.Columns[from];
            for (int index = runStart; index < cards.Length; index++)
            {
                for (int to = 0; to < Board.ColumnCount; to++)
                {
                    if (!board.CanMove(from, index, to) || (board.Columns[to].IsEmpty && to != firstEmpty))
                    {
                        continue;
                    }

                    int priority = Rate(board, from, runStart, index, to);
                    if (priority > 0)
                    {
                        found.Add((new Hint(from, index, to), priority));
                    }
                }
            }
        }

        // OrderBy is stable, so equal priorities keep left-to-right order.
        return found.OrderByDescending(entry => entry.Priority).Select(entry => entry.Hint).ToList();
    }

    /// <summary>Higher is better; zero means the move is legal but pointless.</summary>
    private static int Rate(Board board, int from, int runStart, int index, int to)
    {
        ImmutableArray<TableauCard> cards = board.Columns[from];
        ImmutableArray<TableauCard> target = board.Columns[to];
        Card moving = cards[index].Card;

        if (target.IsEmpty)
        {
            if (index == 0)
            {
                return 0;
            }

            // Into a space: worth it to uncover a card, a last resort otherwise.
            if (index == runStart)
            {
                return cards[index - 1].FaceUp ? 2 : 3;
            }

            return 1;
        }

        bool sameSuit = target[^1].Card.Suit == moving.Suit;

        // Splitting a same-suit run onto another card never improves anything.
        if (index > runStart)
        {
            return 0;
        }

        if (index == 0)
        {
            return sameSuit ? 8 : 7;
        }

        TableauCard above = cards[index - 1];
        if (!above.FaceUp)
        {
            return sameSuit ? 10 : 9;
        }

        // The card above is already one rank higher in another suit, so moving only helps
        // if it joins its own suit.
        if (above.Card.Rank == moving.Rank + 1)
        {
            return sameSuit ? 5 : 0;
        }

        // The card above is not a place this run could sit; moving frees it up.
        return sameSuit ? 6 : 4;
    }
}
