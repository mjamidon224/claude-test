using static SpiderSolitaire.Core.Tests.TestBoards;

namespace SpiderSolitaire.Core.Tests;

public class HintTests
{
    [Fact]
    public void Uncovering_a_face_down_card_comes_first()
    {
        Board board = Build(Columns(
            (0, "TC 7S"),
            (1, "(3H) 7D"),
            (2, "8H")));

        IReadOnlyList<Hint> hints = HintFinder.Find(board);

        Assert.Equal(new Hint(1, 1, 2), hints[0]);
        Assert.Contains(new Hint(0, 1, 2), hints);
    }

    [Fact]
    public void Same_suit_beats_another_suit_when_both_uncover_a_card()
    {
        Board board = Build(Columns(
            (0, "(3H) 7S"),
            (1, "8H"),
            (2, "8S")));

        IReadOnlyList<Hint> hints = HintFinder.Find(board);

        Assert.Equal(new Hint(0, 1, 2), hints[0]);
        Assert.Equal(new Hint(0, 1, 1), hints[1]);
    }

    [Fact]
    public void Pointless_moves_are_not_suggested()
    {
        Board board = Build(new[]
        {
            "9S 8S",        // a whole column: not into the space
            "8D 7D",
            "8C",
            "",
            "9H",
            "(KD) 8D 7H",   // already on an 8 of another suit: not onto another such 8
            "KD",
            "KD",
            "KD",
            "KD",
        });

        IReadOnlyList<Hint> hints = HintFinder.Find(board);

        Assert.DoesNotContain(new Hint(0, 0, 3), hints);
        Assert.DoesNotContain(new Hint(5, 2, 2), hints);
        Assert.DoesNotContain(new Hint(5, 2, 0), hints);

        // Splitting 9S 8S to put the 8 on the 9 of hearts breaks a same-suit run for nothing.
        Assert.DoesNotContain(new Hint(0, 1, 4), hints);

        // Moving 8D 7D onto the 9 of hearts empties a column: worth it.
        Assert.Contains(new Hint(1, 0, 4), hints);
    }

    [Fact]
    public void Joining_its_own_suit_is_worth_suggesting_even_off_a_matching_rank()
    {
        Board board = Build(Columns((0, "8D 7H"), (1, "8H")));

        Assert.Contains(new Hint(0, 1, 1), HintFinder.Find(board));
    }

    [Fact]
    public void Only_the_first_space_is_suggested()
    {
        Board board = Build(new[] { "(4C) 9S", "", "", "KD", "KD", "KD", "KD", "KD", "KD", "KD" });

        IReadOnlyList<Hint> hints = HintFinder.Find(board);

        Assert.Equal(new Hint(0, 1, 1), Assert.Single(hints));
    }

    [Fact]
    public void Every_hint_is_a_legal_move()
    {
        for (int seed = 0; seed < 30; seed++)
        {
            Game game = new(Difficulty.FourSuits, seed);
            foreach (Hint hint in game.FindHints())
            {
                Assert.True(game.Board.CanMove(hint.FromColumn, hint.FromIndex, hint.ToColumn));
            }
        }
    }
}
