using static SpiderSolitaire.Core.Tests.TestBoards;

namespace SpiderSolitaire.Core.Tests;

public class DealRowTests
{
    [Fact]
    public void Dealing_puts_one_face_up_card_on_every_column_in_stock_order()
    {
        Game game = new(Difficulty.FourSuits, 99);
        Board before = game.Board;

        IReadOnlyList<Stage>? stages = game.TryDeal();

        Assert.NotNull(stages);
        Assert.Equal(StageKind.Deal, stages[0].Kind);
        Assert.Equal(40, game.Board.Stock.Length);
        Assert.Equal(499, game.Score);

        for (int i = 0; i < Board.ColumnCount; i++)
        {
            TableauCard dealt = game.Board.Columns[i][^1];
            Assert.True(dealt.FaceUp);
            Assert.Equal(before.Stock[40 + i], dealt.Card);
            Assert.Equal(before.Columns[i].Length + 1, game.Board.Columns[i].Length);
        }
    }

    [Fact]
    public void Five_deals_empty_the_stock()
    {
        Game game = new(Difficulty.OneSuit, 5);

        for (int i = 0; i < 5; i++)
        {
            Assert.NotNull(game.TryDeal());
        }

        Assert.Equal(DealBlocker.StockEmpty, game.DealBlocker);
        Assert.Null(game.TryDeal());
        Assert.Equal(104, game.Board.TableauCardCount + game.Board.Foundations.Length * 13);
    }

    [Fact]
    public void Cannot_deal_while_a_column_is_empty()
    {
        Game game = new(Build(Columns((3, "")), stockRows: 1));

        Assert.Equal(DealBlocker.EmptyColumn, game.DealBlocker);
        Assert.Null(game.TryDeal());
        Assert.Equal(0, game.Moves);
    }

    [Fact]
    public void Can_deal_into_spaces_when_too_few_cards_are_left_to_fill_them()
    {
        // Seven suits done and nine cards on the table: without this exception the last
        // row could never be dealt and the game could never be won.
        string[] columns = { "KD QD", "TD 9D", "8D", "7D", "6D", "4D", "", "3D", "", "" };
        Game game = new(Build(columns, stockRows: 1, foundations: 7));

        Assert.Equal(DealBlocker.None, game.DealBlocker);
        Assert.NotNull(game.TryDeal());
        Assert.All(game.Board.Columns, column => Assert.NotEmpty(column));
    }

    [Fact]
    public void Dealing_can_complete_a_suit()
    {
        string[] columns = Columns((4, "KS QS JS TS 9S 8S 7S 6S 5S 4S 3S 2S"));
        Board board = Build(columns, stockRows: 1);

        // Make the card dealt onto column 4 the missing Ace of spades.
        var stock = board.Stock.SetItem(4, new Card(board.Stock[4].Id, Suit.Spades, Card.Ace));
        Game game = new(new Board(board.Difficulty, board.Columns, stock, board.Foundations));

        IReadOnlyList<Stage>? stages = game.TryDeal();

        Assert.NotNull(stages);
        Assert.Contains(stages, stage => stage.Kind == StageKind.CompleteSuit);
        Assert.Single(game.Board.Foundations);
        Assert.Empty(game.Board.Columns[4]);
    }
}
