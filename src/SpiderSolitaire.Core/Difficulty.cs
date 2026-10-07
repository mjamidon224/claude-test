namespace SpiderSolitaire.Core;

/// <summary>The number of suits in the two decks, which is all that difficulty changes.</summary>
public enum Difficulty
{
    OneSuit = 1,
    TwoSuits = 2,
    FourSuits = 4,
}

public static class DifficultyExtensions
{
    public static IReadOnlyList<Difficulty> All { get; } = new[]
    {
        Difficulty.OneSuit,
        Difficulty.TwoSuits,
        Difficulty.FourSuits,
    };

    public static string DisplayName(this Difficulty difficulty) => difficulty switch
    {
        Difficulty.OneSuit => "Beginner",
        Difficulty.TwoSuits => "Intermediate",
        _ => "Advanced",
    };

    public static string SuitsText(this Difficulty difficulty) => difficulty switch
    {
        Difficulty.OneSuit => "One suit",
        Difficulty.TwoSuits => "Two suits",
        _ => "Four suits",
    };

    public static bool IsDefined(this Difficulty difficulty) => Enum.IsDefined(difficulty);
}
