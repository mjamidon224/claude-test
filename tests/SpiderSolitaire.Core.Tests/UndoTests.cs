using static SpiderSolitaire.Core.Tests.TestBoards;

namespace SpiderSolitaire.Core.Tests;

public class UndoTests
{
    [Fact]
    public void Undo_restores_the_board_but_costs_a_move()
    {
        Game game = new(Build(Columns((0, "(KH) 7S"), (1, "8D"))));
        Board start = game.Board;

        game.TryMove(0, 1, 1);
        IReadOnlyList<Stage>? stages = game.Undo();

        Assert.NotNull(stages);
        Assert.Equal(StageKind.Undo, Assert.Single(stages).Kind);
        Assert.Same(start, game.Board);
        Assert.Equal(2, game.Moves);
        Assert.Equal(498, game.Score);
        Assert.False(game.CanUndo);
    }

    [Fact]
    public void Undo_reverses_a_move_and_everything_it_set_off_in_one_step()
    {
        Game game = new(Build(Columns(
            (0, "(9C) KH QH JH TH 9H 8H 7H 6H 5H 4H 3H 2H"),
            (1, "(4D) AH"))));
        Board start = game.Board;

        game.TryMove(1, 1, 0);
        Assert.Equal(599, game.Score);

        game.Undo();

        // The suit bonus goes with the suit, so undoing cannot be used to earn it twice.
        Assert.Same(start, game.Board);
        Assert.Equal(498, game.Score);
    }

    [Fact]
    public void Undo_takes_back_a_deal()
    {
        Game game = new(Difficulty.TwoSuits, 3);
        Board start = game.Board;

        game.TryDeal();
        game.Undo();

        Assert.Same(start, game.Board);
        Assert.Equal(50, game.Board.Stock.Length);
    }

    [Fact]
    public void Several_undos_walk_back_in_order()
    {
        Game game = new(Build(Columns((0, "9S"), (1, "8S"), (2, "7S"), (3, "6S"))));
        Board start = game.Board;

        game.TryMove(1, 0, 0);
        Board afterFirst = game.Board;
        game.TryMove(2, 0, 0);
        game.TryMove(3, 0, 0);

        game.Undo();
        game.Undo();
        Assert.Same(afterFirst, game.Board);

        game.Undo();
        Assert.Same(start, game.Board);
        Assert.Null(game.Undo());
    }

    [Fact]
    public void No_undo_once_the_game_is_won()
    {
        string[] columns = Columns((0, "KS QS JS TS 9S 8S 7S 6S 5S 4S 3S 2S"), (1, "AS"));
        columns = columns.Select((column, i) => i > 1 ? "" : column).ToArray();
        Game game = new(Build(columns, foundations: 7));

        game.TryMove(1, 0, 0);

        Assert.True(game.IsWon);
        Assert.False(game.CanUndo);
        Assert.Null(game.Undo());
        Assert.Empty(game.FindHints());
    }

    [Fact]
    public void Restart_replays_the_same_deal_from_the_beginning()
    {
        Game game = new(Difficulty.FourSuits, 2024);
        string opening = BoardCodec.Encode(game.Board);
        game.TryDeal();

        Game restarted = game.Restart();

        Assert.Equal(opening, BoardCodec.Encode(restarted.Board));
        Assert.Equal(0, restarted.Moves);
        Assert.Equal(game.Seed, restarted.Seed);
    }
}
