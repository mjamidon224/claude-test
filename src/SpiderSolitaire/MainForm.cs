using System.Diagnostics;
using System.Globalization;
using SpiderSolitaire.Core;
using SpiderSolitaire.Dialogs;
using SpiderSolitaire.Rendering;

namespace SpiderSolitaire;

/// <summary>
/// The game window: menus and shortcuts, the clock, and the game's lifecycle — starting,
/// abandoning, saving, resuming and winning — along with the statistics that go with it.
/// </summary>
internal sealed class MainForm : Form
{
    private const string AppName = "Spider Solitaire";

    private readonly BoardView _board = new() { Dock = DockStyle.Fill };
    private readonly MenuStrip _menu = new();
    private readonly SoundEffects _sounds = new();
    private readonly System.Windows.Forms.Timer _clock = new() { Interval = 250 };
    private readonly Stopwatch _clockWatch = Stopwatch.StartNew();
    private readonly System.Windows.Forms.Timer _winDialogDelay = new() { Interval = 1800 };
    private readonly AppSettings _settings;
    private readonly GameStatistics _statistics;
    private readonly bool _firstRun;

    private ToolStripMenuItem _undoItem = null!;
    private ToolStripMenuItem _hintItem = null!;
    private ToolStripMenuItem _dealItem = null!;

    private Game? _game;
    private bool _winRecorded;
    private int? _winPlace;
    private TimeSpan _lastClockReading;
    private int _lastShownSecond = -1;

    public MainForm()
    {
        _firstRun = !Storage.SettingsExist;
        _settings = Storage.LoadSettings();
        _statistics = Storage.LoadStatistics();

        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        Text = AppName;
        MinimumSize = new Size(640, 480);
        Size = new Size(1100, 780);
        StartPosition = FormStartPosition.CenterScreen;

        try
        {
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        }
        catch (Exception)
        {
            // Keep the default icon.
        }

        BuildMenu();
        Controls.Add(_board);
        Controls.Add(_menu);
        MainMenuStrip = _menu;

        _board.AnimationsEnabled = _settings.Animations;
        _board.CardBack = _settings.CardBack;
        _board.Table = _settings.Table;
        _sounds.Enabled = _settings.Sounds;

        _board.GameChanged += (_, _) => UpdateMenu();
        _board.StagePlayed += (_, kind) => PlaySoundFor(kind);
        _board.DealRefused += (_, _) => OnDealRefused();
        _board.MoveRefused += (_, _) => _sounds.Play(Sound.Invalid);
        _board.AnimationsFinished += (_, _) => CheckForWin();

        _clock.Tick += (_, _) => OnClockTick();
        _winDialogDelay.Tick += (_, _) =>
        {
            _winDialogDelay.Stop();
            ShowWinDialog();
        };

        RestoreWindowBounds();
    }

    private bool InProgress => _game is { HasStarted: true, IsWon: false };

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        if (_settings.WindowMaximized)
        {
            WindowState = FormWindowState.Maximized;
        }
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        _clock.Start();

        if (TryResumeSavedGame())
        {
            return;
        }

        if (_firstRun)
        {
            if (DifficultyDialog.Ask(this, _settings.Difficulty) is { } difficulty)
            {
                _settings.Difficulty = difficulty;
            }

            SaveSettings();
        }

        StartNewGame(_settings.Difficulty);
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        // Single-letter shortcuts, as in the Windows game. Menu items cannot own a shortcut
        // without a modifier key, so these are handled here.
        switch (keyData)
        {
            case Keys.H:
                OnHint();
                return true;
            case Keys.D:
                OnDeal();
                return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _board.FinishAnimations();

        if (_game is not null && InProgress)
        {
            bool save = _settings.AlwaysSaveOnExit
                || e.CloseReason is CloseReason.WindowsShutDown or CloseReason.TaskManagerClosing;

            if (!save)
            {
                int choice = ChoiceDialog.Ask(
                    this,
                    "Exit Game",
                    "Do you want to save this game?",
                    "A saved game carries on where you left off next time. If you don't save it, it counts as a loss.",
                    "Save",
                    "Don't save",
                    "Cancel");

                if (choice is 2 or ChoiceDialog.Dismissed)
                {
                    e.Cancel = true;
                    return;
                }

                save = choice == 0;
            }

            if (save)
            {
                string? error = Storage.SaveGame(_game.ToSavedGame());
                if (error is not null)
                {
                    DialogResult answer = MessageBox.Show(
                        this,
                        $"The game could not be saved:\n\n{error}\n\nExit anyway? The game will count as a loss.",
                        AppName,
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Warning);

                    if (answer != DialogResult.Yes)
                    {
                        e.Cancel = true;
                        return;
                    }

                    RecordLoss();
                }
            }
            else
            {
                RecordLoss();
            }
        }

        RememberWindowBounds();
        SaveSettings();
        base.OnFormClosing(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _clock.Dispose();
            _winDialogDelay.Dispose();
            _sounds.Dispose();
        }

        base.Dispose(disposing);
    }

    private void BuildMenu()
    {
        ToolStripMenuItem game = new("&Game");
        game.DropDownItems.Add(Item("&New Game", Keys.F2, null, (_, _) => OnNewGame()));
        game.DropDownItems.Add(Item("&Restart This Game", Keys.None, null, (_, _) => OnRestart()));
        game.DropDownItems.Add(new ToolStripSeparator());
        game.DropDownItems.Add(_undoItem = Item("&Undo", Keys.Control | Keys.Z, null, (_, _) => _board.Undo()));
        game.DropDownItems.Add(_hintItem = Item("&Hint", Keys.None, "H", (_, _) => OnHint()));
        game.DropDownItems.Add(_dealItem = Item("&Deal Next Row", Keys.None, "D", (_, _) => OnDeal()));
        game.DropDownItems.Add(new ToolStripSeparator());
        game.DropDownItems.Add(Item("&Statistics", Keys.F4, null, (_, _) => OnStatistics()));
        game.DropDownItems.Add(Item("&Options", Keys.F5, null, (_, _) => OnOptions()));
        game.DropDownItems.Add(Item("Change &Appearance", Keys.F7, null, (_, _) => OnAppearance()));
        game.DropDownItems.Add(new ToolStripSeparator());
        game.DropDownItems.Add(Item("E&xit", Keys.None, "Alt+F4", (_, _) => Close()));

        ToolStripMenuItem help = new("&Help");
        help.DropDownItems.Add(Item("&How to Play", Keys.F1, null, (_, _) => HowToPlayDialog.Present(this)));
        help.DropDownItems.Add(new ToolStripSeparator());
        help.DropDownItems.Add(Item("&About Spider Solitaire", Keys.None, null, (_, _) => ShowAbout()));

        _menu.Items.AddRange(new ToolStripItem[] { game, help });
    }

    private static ToolStripMenuItem Item(string text, Keys shortcut, string? shortcutText, EventHandler onClick)
    {
        ToolStripMenuItem item = new(text, null, onClick);
        if (shortcut != Keys.None)
        {
            item.ShortcutKeys = shortcut;
        }

        if (shortcutText is not null)
        {
            item.ShortcutKeyDisplayString = shortcutText;
        }

        return item;
    }

    private void UpdateMenu()
    {
        bool playing = _game is { IsWon: false };
        _undoItem.Enabled = _game?.CanUndo == true;
        _hintItem.Enabled = playing;
        _dealItem.Enabled = playing && _game!.Board.Stock.Length > 0;
    }

    private void StartNewGame(Difficulty difficulty) => Begin(new Game(difficulty, Game.NewSeed()), animate: true);

    private void Begin(Game game, bool animate)
    {
        _winDialogDelay.Stop();
        _game = game;
        _winRecorded = false;
        _winPlace = null;
        _lastShownSecond = -1;
        _board.StartGame(game, animate);
        UpdateMenu();
    }

    /// <summary>Offers a saved game from last time. Returns true if play resumed from it.</summary>
    private bool TryResumeSavedGame()
    {
        SavedGame? saved = Storage.LoadSavedGame();
        if (saved is null)
        {
            return false;
        }

        // A saved game is only ever resumed once.
        Storage.DeleteSavedGame();

        Game resumed;
        try
        {
            resumed = Game.FromSavedGame(saved);
        }
        catch (FormatException)
        {
            return false;
        }

        if (resumed.IsWon)
        {
            return false;
        }

        if (!_settings.AlwaysContinueSavedGame)
        {
            int choice = ChoiceDialog.Ask(
                this,
                "Saved Game",
                "Do you want to continue your saved game?",
                $"{resumed.Difficulty.DisplayName()} ({resumed.Difficulty.SuitsText().ToLowerInvariant()}), score {resumed.Score}, {resumed.Moves} moves, {BoardPainter.FormatTime(resumed.Elapsed)} played.\n\nStarting a new game instead counts the saved one as a loss.",
                "Continue",
                "New game");

            if (choice == 1)
            {
                if (resumed.HasStarted)
                {
                    _statistics.For(resumed.Difficulty).RecordLoss();
                    SaveStatistics();
                }

                return false;
            }
        }

        Begin(resumed, animate: false);
        return true;
    }

    private void OnNewGame()
    {
        _board.FinishAnimations();
        if (!InProgress)
        {
            StartNewGame(_settings.Difficulty);
            return;
        }

        int choice = ChoiceDialog.Ask(
            this,
            "New Game",
            "This game is not finished. What do you want to do?",
            "Quitting or restarting counts this game as a loss.",
            "Quit and start a new game",
            "Restart this game",
            "Keep playing");

        if (choice == 0)
        {
            RecordLoss();
            StartNewGame(_settings.Difficulty);
        }
        else if (choice == 1)
        {
            RecordLoss();
            Begin(_game!.Restart(), animate: true);
        }
    }

    private void OnRestart()
    {
        if (_game is null)
        {
            return;
        }

        _board.FinishAnimations();
        if (InProgress)
        {
            int choice = ChoiceDialog.Ask(
                this,
                "Restart Game",
                "Restart this game from the beginning?",
                "You'll get the same deal again. Restarting counts this game as a loss.",
                "Restart",
                "Keep playing");

            if (choice != 0)
            {
                return;
            }

            RecordLoss();
        }

        Begin(_game.Restart(), animate: true);
    }

    private void OnHint()
    {
        if (_game is null || _game.IsWon)
        {
            return;
        }

        if (_board.ShowHint() != HintOutcome.NoMoves)
        {
            return;
        }

        _sounds.Play(Sound.Invalid);
        List<string> buttons = new();
        if (_game.CanUndo)
        {
            buttons.Add("Undo");
        }

        buttons.Add("Restart this game");
        buttons.Add("New game");
        buttons.Add("Keep playing");

        int choice = ChoiceDialog.Ask(
            this,
            "No More Moves",
            "There are no more moves available.",
            _game.HasStarted
                ? "You can undo, or give up on this game. Restarting or starting a new game counts it as a loss."
                : "You can start again with this deal or a new one.",
            buttons.ToArray());

        string chosen = choice >= 0 ? buttons[choice] : "Keep playing";
        switch (chosen)
        {
            case "Undo":
                _board.Undo();
                break;
            case "Restart this game":
                if (InProgress)
                {
                    RecordLoss();
                }

                Begin(_game.Restart(), animate: true);
                break;
            case "New game":
                if (InProgress)
                {
                    RecordLoss();
                }

                StartNewGame(_settings.Difficulty);
                break;
        }
    }

    private void OnDeal()
    {
        if (_game is { IsWon: false })
        {
            _board.Deal();
        }
    }

    private void OnDealRefused()
    {
        _sounds.Play(Sound.Invalid);
        MessageBox.Show(
            this,
            "You are not allowed to deal a new row while there are any empty slots.",
            AppName,
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private void OnStatistics()
    {
        _board.FinishAnimations();
        StatisticsDialog.Present(this, _statistics, _game?.Difficulty ?? _settings.Difficulty);
        SaveStatistics();
    }

    private void OnOptions()
    {
        _board.FinishAnimations();
        using OptionsDialog dialog = new(_settings);
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        Difficulty previous = _settings.Difficulty;
        dialog.ApplyTo(_settings);
        SaveSettings();

        _board.AnimationsEnabled = _settings.Animations;
        _sounds.Enabled = _settings.Sounds;

        if (_settings.Difficulty == previous || _game is null || _game.Difficulty == _settings.Difficulty)
        {
            return;
        }

        if (!InProgress)
        {
            StartNewGame(_settings.Difficulty);
            return;
        }

        int choice = ChoiceDialog.Ask(
            this,
            "Change Difficulty",
            $"Start a new {_settings.Difficulty.DisplayName()} game now?",
            "Quitting this game counts it as a loss. If you keep playing, the new difficulty starts with your next game.",
            "Start a new game",
            "Keep playing");

        if (choice == 0)
        {
            RecordLoss();
            StartNewGame(_settings.Difficulty);
        }
    }

    private void OnAppearance()
    {
        _board.FinishAnimations();
        if (AppearanceDialog.Ask(this, _settings.CardBack, _settings.Table) is not { } choice)
        {
            return;
        }

        _settings.CardBack = choice.CardBack;
        _settings.Table = choice.Table;
        _board.CardBack = choice.CardBack;
        _board.Table = choice.Table;
        SaveSettings();
    }

    private void ShowAbout()
    {
        string version = typeof(MainForm).Assembly.GetName().Version?.ToString(3) ?? "1.0.0";
        MessageBox.Show(
            this,
            $"{AppName} {version}\n\nThe classic Windows card game: no ads, no accounts, no internet connection needed.\n\nSettings, statistics and saved games are kept in\n{Storage.Folder}",
            $"About {AppName}",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private void PlaySoundFor(StageKind kind)
    {
        switch (kind)
        {
            case StageKind.Move:
            case StageKind.Undo:
                _sounds.Play(Sound.Place);
                break;
            case StageKind.Deal:
                _sounds.Play(Sound.Deal);
                break;
            case StageKind.CompleteSuit:
                _sounds.Play(Sound.CompleteSuit);
                break;
        }
    }

    private void CheckForWin()
    {
        UpdateMenu();
        if (_game is not { IsWon: true } || _winRecorded)
        {
            return;
        }

        _winRecorded = true;
        _winPlace = _statistics.For(_game.Difficulty).RecordWin(_game.Score, _game.Moves, _game.Elapsed, DateTime.Now);
        SaveStatistics();
        Storage.DeleteSavedGame();

        _sounds.Play(Sound.Win);
        if (_settings.Animations)
        {
            _board.Celebrate();
            _winDialogDelay.Start();
        }
        else
        {
            BeginInvoke(ShowWinDialog);
        }
    }

    private void ShowWinDialog()
    {
        if (_game is not { IsWon: true })
        {
            return;
        }

        DifficultyStatistics stats = _statistics.For(_game.Difficulty);
        string record = _winPlace switch
        {
            1 => "That's your best score yet!\n\n",
            { } place => $"That's your #{place} best score.\n\n",
            _ => "",
        };

        string details =
            $"{record}Score: {_game.Score.ToString("N0", CultureInfo.CurrentCulture)}\n" +
            $"Moves: {_game.Moves}\n" +
            $"Time: {BoardPainter.FormatTime(_game.Elapsed)}\n\n" +
            $"{_game.Difficulty.DisplayName()} games won: {stats.GamesWon} of {stats.GamesPlayed} ({stats.WinPercentage}%)\n" +
            $"Current streak: {stats.CurrentStreakText}";

        int choice = ChoiceDialog.Ask(this, "Game Won", "Congratulations, you won!", details, "Play again", "Exit");
        if (choice == 0)
        {
            StartNewGame(_settings.Difficulty);
        }
        else if (choice == 1)
        {
            Close();
        }
    }

    private void OnClockTick()
    {
        TimeSpan now = _clockWatch.Elapsed;
        TimeSpan delta = now - _lastClockReading;
        _lastClockReading = now;

        // The clock runs from the first move until the game is won, and stops while minimised.
        if (_game is null || !InProgress || WindowState == FormWindowState.Minimized)
        {
            return;
        }

        _game.Elapsed += delta;
        int second = (int)_game.Elapsed.TotalSeconds;
        if (second != _lastShownSecond)
        {
            _lastShownSecond = second;
            _board.InvalidateScore();
        }
    }

    private void RecordLoss()
    {
        if (_game is null)
        {
            return;
        }

        _statistics.For(_game.Difficulty).RecordLoss();
        SaveStatistics();
    }

    private void SaveStatistics() => Storage.SaveStatistics(_statistics);

    private void SaveSettings() => Storage.SaveSettings(_settings);

    private void RestoreWindowBounds()
    {
        if (_settings.Window is not { } saved)
        {
            return;
        }

        Rectangle bounds = saved.ToRectangle();
        bool visible = bounds.Width >= MinimumSize.Width
            && bounds.Height >= MinimumSize.Height
            && Screen.AllScreens.Any(screen => screen.WorkingArea.IntersectsWith(Rectangle.Inflate(bounds, -40, -40)));

        if (visible)
        {
            StartPosition = FormStartPosition.Manual;
            Bounds = bounds;
        }
    }

    private void RememberWindowBounds()
    {
        Rectangle normal = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
        _settings.Window = WindowBounds.From(normal);
        _settings.WindowMaximized = WindowState == FormWindowState.Maximized;
    }
}
