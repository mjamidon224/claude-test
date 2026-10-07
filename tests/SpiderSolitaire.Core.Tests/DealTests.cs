namespace SpiderSolitaire.Core.Tests;

public class DealTests
{
    [Theory]
    [InlineData(Difficulty.OneSuit)]
    [InlineData(Difficulty.TwoSuits)]
    [InlineData(Difficulty.FourSuits)]
    public void Opening_layout_matches_the_classic_deal(Difficulty difficulty)
    {
        Board board = new Game(difficulty, seed: 1234).Board;

        Assert.Equal(new[] { 6, 6, 6, 6, 5, 5, 5, 5, 5, 5 }, board.Columns.Select(column => column.Length));
        Assert.Equal(50, board.Stock.Length);
        Assert.Equal(5, board.RowsLeftToDeal);
        Assert.Empty(board.Foundations);

        foreach (var column in board.Columns)
        {
            Assert.True(column[^1].FaceUp);
            Assert.All(column.Take(column.Length - 1), card => Assert.False(card.FaceUp));
        }

        IEnumerable<int> ids = board.Columns.SelectMany(column => column.Select(card => card.Card.Id)).Concat(board.Stock.Select(card => card.Id));
        Assert.Equal(Enumerable.Range(0, Deck.CardCount), ids.Order());
    }

    [Fact]
    public void Same_seed_deals_the_same_cards_and_different_seeds_do_not()
    {
        string first = BoardCodec.Encode(new Game(Difficulty.FourSuits, 42).Board);
        string again = BoardCodec.Encode(new Game(Difficulty.FourSuits, 42).Board);
        string other = BoardCodec.Encode(new Game(Difficulty.FourSuits, 43).Board);

        Assert.Equal(first, again);
        Assert.NotEqual(first, other);
    }

    [Fact]
    public void Shuffle_is_stable_across_versions()
    {
        // Pinned so a saved game's seed keeps meaning the same deal after an update.
        IEnumerable<int> firstTen = Deck.Shuffled(Difficulty.FourSuits, 1).Take(10).Select(card => card.Id);

        Assert.Equal(new[] { 31, 84, 10, 7, 51, 19, 96, 59, 5, 60 }, firstTen);
    }

    [Theory]
    [InlineData(Difficulty.OneSuit, 104, 0, 0, 0)]
    [InlineData(Difficulty.TwoSuits, 52, 52, 0, 0)]
    [InlineData(Difficulty.FourSuits, 26, 26, 26, 26)]
    public void Difficulty_sets_how_many_suits_are_in_play(Difficulty difficulty, int spades, int hearts, int clubs, int diamonds)
    {
        Card[] cards = Enumerable.Range(0, Deck.CardCount).Select(id => Deck.CardFromId(id, difficulty)).ToArray();

        Assert.Equal(spades, cards.Count(card => card.Suit == Suit.Spades));
        Assert.Equal(hearts, cards.Count(card => card.Suit == Suit.Hearts));
        Assert.Equal(clubs, cards.Count(card => card.Suit == Suit.Clubs));
        Assert.Equal(diamonds, cards.Count(card => card.Suit == Suit.Diamonds));

        // Every suit present has whole runs of Ace to King.
        foreach (IGrouping<Suit, Card> suit in cards.GroupBy(card => card.Suit))
        {
            Assert.All(Enumerable.Range(1, 13), rank => Assert.Equal(suit.Count() / 13, suit.Count(card => card.Rank == rank)));
        }
    }

    [Fact]
    public void New_game_scores_500_with_no_moves()
    {
        Game game = new(Difficulty.OneSuit, 7);

        Assert.Equal(500, game.Score);
        Assert.Equal(0, game.Moves);
        Assert.False(game.HasStarted);
        Assert.False(game.CanUndo);
    }
}
