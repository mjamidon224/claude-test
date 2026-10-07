namespace SpiderSolitaire.Core.Tests;

public class StatisticsTests
{
    private static readonly DateTime When = new(2026, 10, 7, 12, 0, 0, DateTimeKind.Local);

    [Fact]
    public void Streaks_and_win_percentage()
    {
        DifficultyStatistics stats = new();

        stats.RecordWin(900, 400, TimeSpan.FromMinutes(10), When);
        stats.RecordWin(950, 350, TimeSpan.FromMinutes(8), When);
        stats.RecordLoss();
        stats.RecordLoss();
        stats.RecordLoss();
        stats.RecordWin(800, 500, TimeSpan.FromMinutes(12), When);

        Assert.Equal(6, stats.GamesPlayed);
        Assert.Equal(3, stats.GamesWon);
        Assert.Equal(50, stats.WinPercentage);
        Assert.Equal(2, stats.LongestWinningStreak);
        Assert.Equal(3, stats.LongestLosingStreak);
        Assert.Equal(1, stats.CurrentStreak);
        Assert.Equal("1 win", stats.CurrentStreakText);
        Assert.Equal(480, stats.FastestWinSeconds);
    }

    [Fact]
    public void Win_percentage_rounds_to_nearest()
    {
        DifficultyStatistics stats = new();
        stats.RecordWin(600, 400, TimeSpan.FromMinutes(5), When);
        stats.RecordLoss();
        stats.RecordLoss();

        Assert.Equal(33, stats.WinPercentage);

        stats.RecordWin(600, 400, TimeSpan.FromMinutes(5), When);
        Assert.Equal(50, stats.WinPercentage);
        Assert.Equal(0, new DifficultyStatistics().WinPercentage);
    }

    [Fact]
    public void High_scores_keep_the_best_five_in_order()
    {
        DifficultyStatistics stats = new();
        int[] scores = { 700, 900, 650, 1000, 800, 600, 950 };
        List<int?> places = scores.Select(score => stats.RecordWin(score, 100, TimeSpan.FromMinutes(3), When)).ToList();

        Assert.Equal(new[] { 1000, 950, 900, 800, 700 }, stats.HighScores.Select(entry => entry.Score));
        Assert.Equal(1000, stats.BestScore);
        Assert.Equal(new int?[] { 1, 1, 3, 1, 3, null, 2 }, places);
    }

    [Fact]
    public void Equal_score_ranks_below_the_earlier_one()
    {
        DifficultyStatistics stats = new();
        stats.RecordWin(800, 100, TimeSpan.FromMinutes(3), When);

        int? place = stats.RecordWin(800, 90, TimeSpan.FromMinutes(2), When.AddDays(1));

        Assert.Equal(2, place);
        Assert.Equal(When, stats.HighScores[0].Date);
    }

    [Fact]
    public void Each_difficulty_is_kept_and_reset_separately()
    {
        GameStatistics all = new();
        all.For(Difficulty.OneSuit).RecordLoss();
        all.For(Difficulty.FourSuits).RecordWin(700, 300, TimeSpan.FromMinutes(20), When);

        all.Reset(Difficulty.OneSuit);

        Assert.Equal(0, all.OneSuit.GamesPlayed);
        Assert.Equal(1, all.FourSuits.GamesWon);
        Assert.Equal(0, all.TwoSuits.GamesPlayed);
    }
}
