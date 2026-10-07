using System.Globalization;
using SpiderSolitaire.Core;
using SpiderSolitaire.Rendering;

namespace SpiderSolitaire.Dialogs;

/// <summary>Totals, streaks and best scores for each difficulty, with a reset.</summary>
internal sealed class StatisticsDialog : Form
{
    private static readonly string[] RowNames =
    {
        "Games played",
        "Games won",
        "Win rate",
        "Longest winning streak",
        "Longest losing streak",
        "Current streak",
        "Fastest win",
    };

    private readonly GameStatistics _statistics;
    private readonly ComboBox _difficulty;
    private readonly Label[] _values = new Label[RowNames.Length];
    private readonly ListView _scores;

    private StatisticsDialog(GameStatistics statistics, Difficulty selected)
    {
        _statistics = statistics;
        DialogLayout.Configure(this, "Statistics");

        TableLayoutPanel stack = DialogLayout.Stack();

        _difficulty = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 220,
            Margin = new Padding(0, 0, 0, 10),
        };

        foreach (Difficulty difficulty in DifficultyExtensions.All)
        {
            _difficulty.Items.Add($"{difficulty.DisplayName()} ({difficulty.SuitsText().ToLowerInvariant()})");
        }

        _difficulty.SelectedIndex = DifficultyExtensions.All.ToList().IndexOf(selected);
        _difficulty.SelectedIndexChanged += (_, _) => Fill();
        stack.Controls.Add(_difficulty);

        TableLayoutPanel grid = new()
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2,
            Margin = new Padding(0, 0, 0, 10),
        };

        grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        for (int i = 0; i < RowNames.Length; i++)
        {
            grid.Controls.Add(new Label { AutoSize = true, Text = RowNames[i] + ":", Margin = new Padding(0, 2, 24, 2) }, 0, i);
            _values[i] = new Label { AutoSize = true, Margin = new Padding(0, 2, 0, 2), Font = DialogLayout.BoldFont };
            grid.Controls.Add(_values[i], 1, i);
        }

        stack.Controls.Add(grid);
        stack.Controls.Add(DialogLayout.Text("Best scores"));

        _scores = new ListView
        {
            View = View.Details,
            FullRowSelect = true,
            HeaderStyle = ColumnHeaderStyle.Nonclickable,
            MultiSelect = false,
            Size = new Size(400, 130),
        };

        _scores.Columns.Add("", 30);
        _scores.Columns.Add("Score", 70, HorizontalAlignment.Right);
        _scores.Columns.Add("Moves", 64, HorizontalAlignment.Right);
        _scores.Columns.Add("Time", 70, HorizontalAlignment.Right);
        _scores.Columns.Add("Date", -2);
        stack.Controls.Add(_scores);

        Button reset = DialogLayout.Button("Reset...");
        reset.Click += (_, _) => Reset();
        Button close = DialogLayout.Button("Close", DialogResult.Cancel);
        stack.Controls.Add(DialogLayout.ButtonRow(reset, close));

        Controls.Add(stack);
        AcceptButton = close;
        CancelButton = close;

        Fill();
        DialogLayout.Complete(this);
    }

    private Difficulty Selected => DifficultyExtensions.All[Math.Max(0, _difficulty.SelectedIndex)];

    public static void Present(IWin32Window owner, GameStatistics statistics, Difficulty selected)
    {
        using StatisticsDialog dialog = new(statistics, selected);
        dialog.ShowDialog(owner);
    }

    private void Fill()
    {
        DifficultyStatistics stats = _statistics.For(Selected);
        CultureInfo culture = CultureInfo.CurrentCulture;

        string[] values =
        {
            stats.GamesPlayed.ToString("N0", culture),
            stats.GamesWon.ToString("N0", culture),
            $"{stats.WinPercentage}%",
            stats.LongestWinningStreak.ToString("N0", culture),
            stats.LongestLosingStreak.ToString("N0", culture),
            stats.CurrentStreakText,
            stats.FastestWinSeconds is { } seconds ? BoardPainter.FormatTime(TimeSpan.FromSeconds(seconds)) : "—",
        };

        for (int i = 0; i < values.Length; i++)
        {
            _values[i].Text = values[i];
        }

        _scores.BeginUpdate();
        _scores.Items.Clear();
        for (int i = 0; i < stats.HighScores.Count; i++)
        {
            HighScore entry = stats.HighScores[i];
            _scores.Items.Add(new ListViewItem(new[]
            {
                (i + 1).ToString(culture),
                entry.Score.ToString("N0", culture),
                entry.Moves.ToString("N0", culture),
                BoardPainter.FormatTime(TimeSpan.FromSeconds(entry.Seconds)),
                entry.Date.ToString("d", culture),
            }));
        }

        _scores.EndUpdate();
    }

    private void Reset()
    {
        int choice = ChoiceDialog.Ask(
            this,
            "Reset Statistics",
            $"Reset the {Selected.DisplayName()} statistics?",
            "This clears the games played, streaks and best scores for this difficulty. It can't be undone.",
            "Reset",
            "Cancel");

        if (choice == 0)
        {
            _statistics.Reset(Selected);
            Fill();
        }
    }
}
