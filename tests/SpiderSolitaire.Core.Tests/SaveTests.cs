using System.Text.Json;

namespace SpiderSolitaire.Core.Tests;

public class SaveTests
{
    [Fact]
    public void Board_round_trips_through_the_codec()
    {
        Game game = PlayedGame();

        string text = BoardCodec.Encode(game.Board);
        Board decoded = BoardCodec.Decode(text, game.Difficulty);

        Assert.Equal(text, BoardCodec.Encode(decoded));
        Assert.Equal(game.Board.Columns.Select(column => column.Describe()), decoded.Columns.Select(column => column.Describe()));
        Assert.Equal<Card>(game.Board.Stock, decoded.Stock);
    }

    [Fact]
    public void Saved_game_survives_json_and_undo_still_works()
    {
        Game game = PlayedGame();
        game.Elapsed = TimeSpan.FromSeconds(83.5);
        string json = JsonSerializer.Serialize(game.ToSavedGame());

        Game restored = Game.FromSavedGame(JsonSerializer.Deserialize<SavedGame>(json)!);

        Assert.Equal(game.Seed, restored.Seed);
        Assert.Equal(game.Moves, restored.Moves);
        Assert.Equal(game.Score, restored.Score);
        Assert.Equal(game.Elapsed, restored.Elapsed);
        Assert.Equal(BoardCodec.Encode(game.Board), BoardCodec.Encode(restored.Board));

        game.Undo();
        restored.Undo();
        Assert.Equal(BoardCodec.Encode(game.Board), BoardCodec.Encode(restored.Board));
    }

    [Fact]
    public void Board_with_completed_suits_round_trips()
    {
        Game game = new(Difficulty.OneSuit, 11);
        string text = BoardCodec.Encode(game.Board);

        // Hand-build a valid board with one suit cleared: ids 0-12 are Ace to King.
        Board board = BoardCodec.Decode(text, Difficulty.OneSuit);
        var cleared = board.Columns.Select(column => column.Where(card => card.Card.Id >= 13).ToArray()).ToArray();
        var stock = board.Stock.Where(card => card.Id >= 13).ToList();
        while (stock.Count % 10 != 0)
        {
            cleared[0] = cleared[0].Append(new TableauCard(stock[^1], true)).ToArray();
            stock.RemoveAt(stock.Count - 1);
        }

        string withSuit = string.Join(',', cleared.Select(column => string.Concat(column.Select(card => (card.Card.Id | (card.FaceUp ? 0x80 : 0)).ToString("x2")))))
            + "/" + string.Concat(stock.Select(card => card.Id.ToString("x2")))
            + "/" + string.Concat(Enumerable.Range(0, 13).Select(id => id.ToString("x2")));

        Board decoded = BoardCodec.Decode(withSuit, Difficulty.OneSuit);

        Assert.Single(decoded.Foundations);
        Assert.Equal(withSuit, BoardCodec.Encode(decoded));
    }

    [Theory]
    [InlineData("")]
    [InlineData("garbage")]
    [InlineData("zz/00/")]
    public void Malformed_text_is_rejected(string text)
    {
        Assert.Throws<FormatException>(() => BoardCodec.Decode(text, Difficulty.OneSuit));
    }

    [Fact]
    public void Missing_or_duplicated_cards_are_rejected()
    {
        string text = BoardCodec.Encode(new Game(Difficulty.TwoSuits, 8).Board);

        // Drop the last stock card and repeat the first one in its place.
        string duplicated = text[..^2] + text.Split('/')[1][..2] + "/";
        Assert.Throws<FormatException>(() => BoardCodec.Decode(duplicated, Difficulty.TwoSuits));

        string shortStock = text[..^3] + "/";
        Assert.Throws<FormatException>(() => BoardCodec.Decode(shortStock, Difficulty.TwoSuits));
    }

    [Fact]
    public void A_foundation_that_is_not_a_full_suit_is_rejected()
    {
        // Ace to King of ids 13-25 is a valid suit; 0-11 plus 25 is not.
        Game game = new(Difficulty.FourSuits, 1);
        string[] sections = BoardCodec.Encode(game.Board).Split('/');
        string bogus = string.Concat(Enumerable.Range(0, 12).Append(25).Select(id => id.ToString("x2")));

        Assert.Throws<FormatException>(() => BoardCodec.Decode($"{sections[0]}/{sections[1]}/{bogus}", Difficulty.FourSuits));
    }

    [Fact]
    public void Saved_game_with_an_unknown_difficulty_is_rejected()
    {
        SavedGame saved = PlayedGame().ToSavedGame();
        saved.Difficulty = (Difficulty)3;

        Assert.Throws<FormatException>(() => Game.FromSavedGame(saved));
    }

    private static Game PlayedGame()
    {
        Game game = new(Difficulty.FourSuits, 31337);
        game.TryDeal();
        foreach (Hint hint in game.FindHints().Take(1))
        {
            game.TryMove(hint.FromColumn, hint.FromIndex, hint.ToColumn);
        }

        game.TryDeal();
        return game;
    }
}
