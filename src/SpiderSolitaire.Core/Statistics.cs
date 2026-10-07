namespace SpiderSolitaire.Core;

/// <summary>Wins and losses for each difficulty, kept between runs.</summary>
public sealed class GameStatistics
{
    public DifficultyStatistics OneSuit { get; set; } = new();

    public DifficultyStatistics TwoSuits { get; set; } = new();

    public DifficultyStatistics FourSuits { get; set; } = new();

    public DifficultyStatistics For(Difficulty difficulty) => difficulty switch
    {
        Difficulty.OneSuit => OneSuit,
        Difficulty.TwoSuits => TwoSuits,
        _ => FourSuits,
    };

    public void Reset(Difficulty difficulty)
    {
        switch (difficulty)
        {
            case Difficulty.OneSuit:
                OneSuit = new DifficultyStatistics();
                break;
            case Difficulty.TwoSuits:
                TwoSuits = new DifficultyStatistics();
                break;
            default:
                FourSuits = new DifficultyStatistics();
                break;
        }
    }
}

/// <summary>
/// Totals and streaks for one difficulty. A game counts once it is won, or abandoned
/// after at least one move (a new game, a restart, or quitting without saving).
/// </summary>
public sealed class DifficultyStatistics
{
    public const int HighScoresKept = 5;

    public int GamesPlayed { get; set; }

    public int GamesWon { get; set; }

    public int LongestWinningStreak { get; set; }

    public int LongestLosingStreak { get; set; }

    /// <summary>Positive for a run of wins, negative for a run of losses.</summary>
    public int CurrentStreak { get; set; }

    public double? FastestWinSeconds { get; set; }

    /// <summary>Best first.</summary>
    public List<HighScore> HighScores { get; set; } = new();

    public int WinPercentage => GamesPlayed == 0 ? 0 : (int)Math.Round(100.0 * GamesWon / GamesPlayed, MidpointRounding.AwayFromZero);

    public int? BestScore => HighScores.Count == 0 ? null : HighScores[0].Score;

    public string CurrentStreakText => CurrentStreak switch
    {
        0 => "None",
        1 => "1 win",
        -1 => "1 loss",
        > 0 => $"{CurrentStreak} wins",
        _ => $"{-CurrentStreak} losses",
    };

    /// <summary>Records a win and returns the place it took in the high scores (1 = best), or null if it did not make the list.</summary>
    public int? RecordWin(int score, int moves, TimeSpan time, DateTime when)
    {
        GamesPlayed++;
        GamesWon++;
        CurrentStreak = CurrentStreak > 0 ? CurrentStreak + 1 : 1;
        LongestWinningStreak = Math.Max(LongestWinningStreak, CurrentStreak);

        if (FastestWinSeconds is null || time.TotalSeconds < FastestWinSeconds)
        {
            FastestWinSeconds = time.TotalSeconds;
        }

        HighScore entry = new() { Score = score, Moves = moves, Seconds = time.TotalSeconds, Date = when };

        // Ties go below existing entries, so an earlier equal score keeps its place.
        int place = HighScores.FindIndex(existing => score > existing.Score);
        if (place < 0)
        {
            place = HighScores.Count;
        }

        HighScores.Insert(place, entry);
        if (HighScores.Count > HighScoresKept)
        {
            HighScores.RemoveRange(HighScoresKept, HighScores.Count - HighScoresKept);
        }

        return place < HighScoresKept ? place + 1 : null;
    }

    public void RecordLoss()
    {
        GamesPlayed++;
        CurrentStreak = CurrentStreak < 0 ? CurrentStreak - 1 : -1;
        LongestLosingStreak = Math.Max(LongestLosingStreak, -CurrentStreak);
    }
}

public sealed class HighScore
{
    public int Score { get; set; }

    public int Moves { get; set; }

    public double Seconds { get; set; }

    public DateTime Date { get; set; }
}
