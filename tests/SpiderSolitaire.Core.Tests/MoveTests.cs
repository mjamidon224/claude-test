using static SpiderSolitaire.Core.Tests.TestBoards;

namespace SpiderSolitaire.Core.Tests;

public class MoveTests
{
    [Fact]
    public void Run_start_is_the_longest_same_suit_sequence_at_the_bottom()
    {
        Board board = Build(Columns(
            (0, "(KS) 9H 8S 7S 6S"),
            (1, "(KS)"),
            (2, ""),
            (3, "5H 4D")));

        Assert.Equal(2, board.RunStart(0));
        Assert.Equal(-1, board.RunStart(1));
        Assert.Equal(-1, board.RunStart(2));
        Assert.Equal(1, board.RunStart(3));
    }

    [Fact]
    public void Can_pick_up_any_card_within_the_run()
    {
        Board board = Build(Columns((0, "(KS) 9H 8S 7S 6S")));

        Assert.False(board.CanPickUp(0, 0));
        Assert.False(board.CanPickUp(0, 1));
        Assert.True(board.CanPickUp(0, 2));
        Assert.True(board.CanPickUp(0, 3));
        Assert.True(board.CanPickUp(0, 4));
        Assert.False(board.CanPickUp(0, 5));
    }

    [Fact]
    public void A_card_goes_on_one_rank_higher_of_any_suit_or_into_a_space()
    {
        Board board = Build(Columns(
            (0, "7S"),
            (1, "8H"),
            (2, "8S"),
            (3, "9S"),
            (4, ""),
            (5, "(8D)")));

        Assert.True(board.CanMove(0, 0, 1));
        Assert.True(board.CanMove(0, 0, 2));
        Assert.False(board.CanMove(0, 0, 3));
        Assert.True(board.CanMove(0, 0, 4));
        Assert.False(board.CanMove(0, 0, 5));
        Assert.False(board.CanMove(0, 0, 0));
    }

    [Fact]
    public void Mixed_suit_sequences_cannot_be_moved_together()
    {
        Board board = Build(Columns((0, "9S 8H 7H"), (1, "TD"), (2, "9C")));

        Assert.False(board.CanPickUp(0, 0));
        Assert.False(board.CanMove(0, 0, 1));
        Assert.True(board.CanMove(0, 1, 2));
    }

    [Fact]
    public void Moving_uncovers_and_turns_over_the_card_beneath()
    {
        Game game = new(Build(Columns((0, "(KH) (5C) 7S"), (1, "8D"))));

        IReadOnlyList<Stage>? stages = game.TryMove(0, 2, 1);

        Assert.NotNull(stages);
        Assert.Equal(new[] { StageKind.Move, StageKind.Flip }, stages.Select(stage => stage.Kind));
        Assert.Equal("(KH) 5C", game.Board.Columns[0].Describe());
        Assert.Equal("8D 7S", game.Board.Columns[1].Describe());
        Assert.Equal(499, game.Score);
        Assert.Equal(1, game.Moves);
    }

    [Fact]
    public void Illegal_move_changes_nothing()
    {
        Game game = new(Build(Columns((0, "7S"), (1, "9D"))));
        Board before = game.Board;

        Assert.Null(game.TryMove(0, 0, 1));
        Assert.Same(before, game.Board);
        Assert.Equal(0, game.Moves);
        Assert.False(game.CanUndo);
    }

    [Fact]
    public void Completing_a_suit_clears_it_and_scores_100()
    {
        // King to Two of hearts, with the Ace arriving from another column.
        Game game = new(Build(Columns(
            (0, "(9C) KH QH JH TH 9H 8H 7H 6H 5H 4H 3H 2H"),
            (1, "(4D) AH"))));

        IReadOnlyList<Stage>? stages = game.TryMove(1, 1, 0);

        Assert.NotNull(stages);
        Assert.Equal(new[] { StageKind.Move, StageKind.Flip, StageKind.CompleteSuit, StageKind.Flip }, stages.Select(stage => stage.Kind));
        Assert.Equal("9C", game.Board.Columns[0].Describe());
        Assert.Equal("4D", game.Board.Columns[1].Describe());

        Card[] suit = Assert.Single(game.Board.Foundations).ToArray();
        Assert.Equal(Enumerable.Range(1, 13), suit.Select(card => card.Rank));
        Assert.All(suit, card => Assert.Equal(Suit.Hearts, card.Suit));

        Assert.Equal(500 - 1 + 100, game.Score);
    }

    [Fact]
    public void A_mixed_suit_king_to_ace_is_not_complete()
    {
        Game game = new(Build(Columns(
            (0, "KH QH JH TH 9H 8H 7H 6H 5H 4H 3H 2H"),
            (1, "AS"))));

        game.TryMove(1, 0, 0);

        Assert.Empty(game.Board.Foundations);
        Assert.Equal(13, game.Board.Columns[0].Length);
    }

    [Fact]
    public void Best_target_prefers_same_suit_then_any_suit_then_a_space()
    {
        Board board = Build(Columns(
            (0, "(3C) 7S"),
            (1, "8H"),
            (2, ""),
            (3, "8S")));

        Assert.Equal(3, board.FindBestTarget(0, 1));

        Board noSameSuit = Build(Columns((0, "(3C) 7S"), (2, ""), (5, "8H")));
        Assert.Equal(5, noSameSuit.FindBestTarget(0, 1));

        Board onlySpace = Build(Columns((0, "(3C) 7S"), (2, "")));
        Assert.Equal(2, onlySpace.FindBestTarget(0, 1));
    }

    [Fact]
    public void Best_target_will_not_move_a_whole_column_into_a_space()
    {
        Board board = Build(Columns((0, "7S 6S"), (2, "")));

        Assert.Null(board.FindBestTarget(0, 0));
        Assert.Equal(2, board.FindBestTarget(0, 1));
    }

    [Fact]
    public void Best_target_ties_go_to_the_nearest_column_on_the_right()
    {
        Board board = Build(Columns((1, "8H"), (6, "(3C) 7S"), (8, "8D")));

        Assert.Equal(8, board.FindBestTarget(6, 1));
    }
}
