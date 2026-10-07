namespace SpiderSolitaire.Dialogs;

/// <summary>
/// Shared setup for the game's dialogs. They lay themselves out with auto-sizing panels
/// rather than fixed coordinates, so they fit their text at any DPI and font size.
/// </summary>
internal static class DialogLayout
{
    public static readonly Color HeadingColor = Color.FromArgb(0, 51, 153);

    // Shared rather than created per dialog, since controls never dispose fonts given to them.
    public static readonly Font HeadingFont = new("Segoe UI", 12F, FontStyle.Regular, GraphicsUnit.Point);
    public static readonly Font BoldFont = new("Segoe UI", 9F, FontStyle.Bold, GraphicsUnit.Point);
    public static readonly Font LargeFont = new("Segoe UI", 10.5F, FontStyle.Regular, GraphicsUnit.Point);
    public static readonly Font ReadingFont = new("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point);

    /// <summary>
    /// Sets up a dialog and suspends its layout; call <see cref="Complete"/> once its
    /// controls are added. Scaling for the display's DPI happens when layout resumes, so it
    /// covers every control rather than an empty form.
    /// </summary>
    public static void Configure(Form form, string title)
    {
        form.SuspendLayout();
        form.Text = title;
        form.AutoScaleDimensions = new SizeF(7F, 15F);
        form.AutoScaleMode = AutoScaleMode.Font;
        form.AutoSize = true;
        form.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        form.FormBorderStyle = FormBorderStyle.FixedDialog;
        form.MaximizeBox = false;
        form.MinimizeBox = false;
        form.ShowInTaskbar = false;
        form.StartPosition = FormStartPosition.CenterParent;
    }

    public static void Complete(Form form)
    {
        form.ResumeLayout(false);
        form.PerformLayout();
    }

    /// <summary>
    /// A one-column panel that stacks its children and grows to fit them. It sits at the
    /// form's origin, undocked, and carries the dialog's margin as its own padding, so the
    /// auto-sizing form simply wraps it.
    /// </summary>
    public static TableLayoutPanel Stack()
    {
        TableLayoutPanel panel = new()
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            Location = Point.Empty,
            Margin = Padding.Empty,
            Padding = new Padding(14),
        };

        panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        return panel;
    }

    public static Label Heading(string text) => new()
    {
        AutoSize = true,
        Font = HeadingFont,
        ForeColor = HeadingColor,
        MaximumSize = new Size(460, 0),
        Margin = new Padding(0, 0, 0, 8),
        Text = text,
    };

    public static Label Text(string text, int maxWidth = 460) => new()
    {
        AutoSize = true,
        MaximumSize = new Size(maxWidth, 0),
        Margin = new Padding(0, 0, 0, 6),
        Text = text,
    };

    /// <summary>A right-aligned row of buttons, in the order given.</summary>
    public static FlowLayoutPanel ButtonRow(params Button[] buttons)
    {
        FlowLayoutPanel row = new()
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Anchor = AnchorStyles.Right,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = new Padding(0, 14, 0, 0),
        };

        row.Controls.AddRange(buttons);
        return row;
    }

    public static Button Button(string text, DialogResult result = DialogResult.None) => new()
    {
        AutoSize = true,
        AutoSizeMode = AutoSizeMode.GrowOnly,
        MinimumSize = new Size(88, 28),
        Padding = new Padding(6, 0, 6, 0),
        Margin = new Padding(6, 0, 0, 0),
        DialogResult = result,
        Text = text,
        UseVisualStyleBackColor = true,
    };
}
