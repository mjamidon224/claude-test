namespace SpiderSolitaire.Rendering;

internal enum CardBackStyle
{
    ClassicBlue,
    ClassicRed,
    Emerald,
    SpiderWeb,
    Midnight,
}

internal enum TableStyle
{
    Green,
    Blue,
    Red,
    Purple,
    Charcoal,
}

internal static class AppearanceNames
{
    public static string DisplayName(this CardBackStyle style) => style switch
    {
        CardBackStyle.ClassicBlue => "Classic blue",
        CardBackStyle.ClassicRed => "Classic red",
        CardBackStyle.Emerald => "Emerald",
        CardBackStyle.SpiderWeb => "Spider web",
        _ => "Midnight",
    };

    public static string DisplayName(this TableStyle style) => style switch
    {
        TableStyle.Green => "Green felt",
        TableStyle.Blue => "Blue felt",
        TableStyle.Red => "Red felt",
        TableStyle.Purple => "Purple felt",
        _ => "Charcoal",
    };

    /// <summary>The middle of the table, before it darkens towards the edges.</summary>
    public static Color BaseColor(this TableStyle style) => style switch
    {
        TableStyle.Green => Color.FromArgb(18, 120, 58),
        TableStyle.Blue => Color.FromArgb(24, 86, 150),
        TableStyle.Red => Color.FromArgb(140, 30, 36),
        TableStyle.Purple => Color.FromArgb(84, 50, 128),
        _ => Color.FromArgb(58, 62, 68),
    };
}
