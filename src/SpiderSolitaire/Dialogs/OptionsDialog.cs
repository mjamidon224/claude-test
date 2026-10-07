using SpiderSolitaire.Core;

namespace SpiderSolitaire.Dialogs;

/// <summary>Difficulty and the play options, as in the Windows game's Options dialog.</summary>
internal sealed class OptionsDialog : Form
{
    private readonly Dictionary<Difficulty, RadioButton> _difficulty = new();
    private readonly CheckBox _animations;
    private readonly CheckBox _sounds;
    private readonly CheckBox _continueSaved;
    private readonly CheckBox _saveOnExit;

    public OptionsDialog(AppSettings settings)
    {
        DialogLayout.Configure(this, "Options");

        TableLayoutPanel stack = DialogLayout.Stack();

        stack.Controls.Add(Section("Difficulty"));

        FlowLayoutPanel difficultyList = new()
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Margin = new Padding(12, 0, 0, 10),
        };

        foreach (Difficulty difficulty in DifficultyExtensions.All)
        {
            RadioButton radio = new()
            {
                AutoSize = true,
                Checked = difficulty == settings.Difficulty,
                Text = $"{difficulty.DisplayName()} ({difficulty.SuitsText().ToLowerInvariant()})",
                Margin = new Padding(3, 2, 3, 2),
            };

            _difficulty[difficulty] = radio;
            difficultyList.Controls.Add(radio);
        }

        stack.Controls.Add(difficultyList);
        stack.Controls.Add(Section("Play"));

        _animations = Check("Play animations", settings.Animations);
        _sounds = Check("Play sounds", settings.Sounds);
        _continueSaved = Check("Always continue saved games", settings.AlwaysContinueSavedGame);
        _saveOnExit = Check("Always save game on exit", settings.AlwaysSaveOnExit);
        stack.Controls.AddRange(new Control[] { _animations, _sounds, _continueSaved, _saveOnExit });

        Button ok = DialogLayout.Button("OK", DialogResult.OK);
        Button cancel = DialogLayout.Button("Cancel", DialogResult.Cancel);
        stack.Controls.Add(DialogLayout.ButtonRow(ok, cancel));
        Controls.Add(stack);

        AcceptButton = ok;
        CancelButton = cancel;
        DialogLayout.Complete(this);
    }

    public void ApplyTo(AppSettings settings)
    {
        settings.Difficulty = _difficulty.First(entry => entry.Value.Checked).Key;
        settings.Animations = _animations.Checked;
        settings.Sounds = _sounds.Checked;
        settings.AlwaysContinueSavedGame = _continueSaved.Checked;
        settings.AlwaysSaveOnExit = _saveOnExit.Checked;
    }

    private static CheckBox Check(string text, bool value) => new()
    {
        AutoSize = true,
        Checked = value,
        Text = text,
        Margin = new Padding(15, 2, 3, 2),
    };

    private static Label Section(string text) => new()
    {
        AutoSize = true,
        Font = DialogLayout.BoldFont,
        Margin = new Padding(0, 0, 0, 4),
        Text = text,
    };
}
