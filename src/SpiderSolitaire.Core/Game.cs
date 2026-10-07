namespace SpiderSolitaire.Core;

/// <summary>
/// One game in progress: the board, its undo history, the move count and the score.
/// Every action either returns the stages it played out as, or null if it was not allowed.
/// </summary>
/// <remarks>
/// Scoring follows the Windows game: start on 500, lose a point for every move (dealing
/// and undoing count as moves), gain 100 for every completed suit. Undo restores the
/// cards but not the points, so it cannot be used to farm suit bonuses.
/// </remarks>
public sealed class Game
{
    public const int StartingScore = 500;
    public const int SuitBonus = 100;

    private readonly List<Board> _history;

    public Game(Difficulty difficulty, int seed)
        : this(seed, Board.Deal(difficulty, Deck.Shuffled(difficulty, seed)), 0, TimeSpan.Zero, new List<Board>())
    {
    }

    /// <summary>A game starting from an arbitrary position, for tests.</summary>
    internal Game(Board board)
        : this(0, board, 0, TimeSpan.Zero, new List<Board>())
    {
    }

    private Game(int seed, Board board, int moves, TimeSpan elapsed, List<Board> history)
    {
        Seed = seed;
        Board = board;
        Moves = moves;
        Elapsed = elapsed;
        _history = history;
    }

    /// <summary>A fresh random deal number.</summary>
    public static int NewSeed() => Random.Shared.Next();

    public Difficulty Difficulty => Board.Difficulty;

    /// <summary>The deal number; the same seed and difficulty always deal the same cards.</summary>
    public int Seed { get; }

    public Board Board { get; private set; }

    public int Moves { get; private set; }

    /// <summary>Time spent playing. The UI's clock adds to it; the game only stores it.</summary>
    public TimeSpan Elapsed { get; set; }

    public int Score => StartingScore - Moves + SuitBonus * Board.Foundations.Length;

    public bool IsWon => Board.IsWon;

    /// <summary>Whether anything has been done yet, which is what makes abandoning the game count as a loss.</summary>
    public bool HasStarted => Moves > 0;

    public bool CanUndo => _history.Count > 0 && !IsWon;

    public DealBlocker DealBlocker => Board.DealBlocker;

    public IReadOnlyList<Stage>? TryMove(int fromColumn, int index, int toColumn)
    {
        if (IsWon || !Board.CanMove(fromColumn, index, toColumn))
        {
            return null;
        }

        return Apply(StageKind.Move, Board.MoveRun(fromColumn, index, toColumn));
    }

    public IReadOnlyList<Stage>? TryDeal()
    {
        if (IsWon || DealBlocker != DealBlocker.None)
        {
            return null;
        }

        return Apply(StageKind.Deal, Board.DealRow());
    }

    public IReadOnlyList<Stage>? Undo()
    {
        if (!CanUndo)
        {
            return null;
        }

        Board = _history[^1];
        _history.RemoveAt(_history.Count - 1);
        Moves++;
        return new[] { new Stage(StageKind.Undo, Board) };
    }

    public IReadOnlyList<Hint> FindHints() => IsWon ? Array.Empty<Hint>() : HintFinder.Find(Board);

    /// <summary>The same deal from the start.</summary>
    public Game Restart() => new(Difficulty, Seed);

    private IReadOnlyList<Stage> Apply(StageKind kind, Board next)
    {
        _history.Add(Board);
        Moves++;
        Board = next;

        List<Stage> stages = new() { new Stage(kind, Board) };
        Settle(stages);
        return stages;
    }

    /// <summary>
    /// Turns over exposed cards and clears finished suits until neither applies, recording
    /// each as its own stage. Clearing a suit can expose a card, so this loops.
    /// </summary>
    private void Settle(List<Stage> stages)
    {
        while (true)
        {
            Board flipped = Board.FlipExposedCards();
            if (!ReferenceEquals(flipped, Board))
            {
                Board = flipped;
                stages.Add(new Stage(StageKind.Flip, Board));
                continue;
            }

            Board collected = Board.CollectCompletedRun();
            if (!ReferenceEquals(collected, Board))
            {
                Board = collected;
                stages.Add(new Stage(StageKind.CompleteSuit, Board));
                continue;
            }

            return;
        }
    }

    public SavedGame ToSavedGame() => new()
    {
        Difficulty = Difficulty,
        Seed = Seed,
        Moves = Moves,
        ElapsedSeconds = Elapsed.TotalSeconds,
        Board = BoardCodec.Encode(Board),
        History = _history.Select(BoardCodec.Encode).ToList(),
    };

    /// <summary>Restores a saved game. Throws <see cref="FormatException"/> if it does not describe a valid game.</summary>
    public static Game FromSavedGame(SavedGame saved)
    {
        if (!saved.Difficulty.IsDefined())
        {
            throw new FormatException($"Unknown difficulty {saved.Difficulty}.");
        }

        if (saved.Moves < 0 || double.IsNaN(saved.ElapsedSeconds) || saved.ElapsedSeconds < 0 || saved.ElapsedSeconds > TimeSpan.MaxValue.TotalSeconds / 2)
        {
            throw new FormatException("The move count or time is out of range.");
        }

        List<Board> history = (saved.History ?? new List<string>()).Select(text => BoardCodec.Decode(text, saved.Difficulty)).ToList();
        Game game = new(saved.Seed, BoardCodec.Decode(saved.Board, saved.Difficulty), saved.Moves, TimeSpan.FromSeconds(saved.ElapsedSeconds), history);

        // A file written mid-animation by an older build, or edited by hand, could hold an
        // unflipped card or an uncollected suit; settle it so play carries on normally.
        game.Settle(new List<Stage>());
        return game;
    }
}

/// <summary>A game written to disk on exit, in a form System.Text.Json can round-trip.</summary>
public sealed class SavedGame
{
    public Difficulty Difficulty { get; set; }

    public int Seed { get; set; }

    public int Moves { get; set; }

    public double ElapsedSeconds { get; set; }

    public string Board { get; set; } = "";

    /// <summary>Earlier boards, oldest first, so undo still works after resuming.</summary>
    public List<string>? History { get; set; } = new();
}
