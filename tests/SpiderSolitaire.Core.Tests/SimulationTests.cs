namespace SpiderSolitaire.Core.Tests;

/// <summary>Plays whole games on hint moves and deals, checking that nothing is ever lost or duplicated.</summary>
public class SimulationTests
{
    [Theory]
    [InlineData(Difficulty.OneSuit)]
    [InlineData(Difficulty.TwoSuits)]
    [InlineData(Difficulty.FourSuits)]
    public void Playing_by_hints_keeps_every_card_accounted_for(Difficulty difficulty)
    {
        int wins = 0;

        for (int seed = 0; seed < 40; seed++)
        {
            Game game = new(difficulty, seed);
            HashSet<string> seen = new();

            for (int turn = 0; turn < 2000 && !game.IsWon; turn++)
            {
                // Stop following hints round in circles: if this position has come up
                // before, deal instead, and give up once there is nothing left to deal.
                bool repeated = !seen.Add(BoardCodec.Encode(game.Board));
                IReadOnlyList<Hint> hints = game.FindHints();

                IReadOnlyList<Stage>? stages;
                if (hints.Count > 0 && !repeated)
                {
                    Hint hint = hints[0];
                    stages = game.TryMove(hint.FromColumn, hint.FromIndex, hint.ToColumn);
                    Assert.NotNull(stages);
                }
                else if (game.DealBlocker == DealBlocker.None)
                {
                    stages = game.TryDeal();
                    Assert.NotNull(stages);
                }
                else
                {
                    break;
                }

                Assert.Same(game.Board, stages[^1].Board);
                AssertConsistent(game.Board);
            }

            if (game.IsWon)
            {
                wins++;
                Assert.Equal(Game.StartingScore - game.Moves + 8 * Game.SuitBonus, game.Score);
            }
        }

        // The greedy player is weak, but it should win the occasional one-suit game, which
        // shows suits get collected and the win is detected.
        if (difficulty == Difficulty.OneSuit)
        {
            Assert.True(wins > 0, "Expected at least one one-suit win from hint play.");
        }
    }

    private static void AssertConsistent(Board board)
    {
        List<int> ids = board.Columns.SelectMany(column => column.Select(card => card.Card.Id))
            .Concat(board.Stock.Select(card => card.Id))
            .Concat(board.Foundations.SelectMany(suit => suit.Select(card => card.Id)))
            .ToList();

        Assert.Equal(Enumerable.Range(0, Deck.CardCount), ids.Order());
        Assert.Equal(0, board.Stock.Length % Board.ColumnCount);

        foreach (var column in board.Columns)
        {
            // Nothing face down is left exposed, and face-down cards never sit on face-up ones.
            if (!column.IsEmpty)
            {
                Assert.True(column[^1].FaceUp);
            }

            int firstFaceUp = column.ToList().FindIndex(card => card.FaceUp);
            if (firstFaceUp >= 0)
            {
                Assert.All(column.Skip(firstFaceUp), card => Assert.True(card.FaceUp));
            }
        }
    }
}
