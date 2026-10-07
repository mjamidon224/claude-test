using System.Text.Json;
using System.Text.Json.Serialization;
using SpiderSolitaire.Core;

namespace SpiderSolitaire;

/// <summary>
/// Reads and writes the game's files under <c>%APPDATA%\SpiderSolitaire</c>: settings,
/// statistics and the saved game, each plain JSON. Loading never throws: a missing or
/// damaged file just means starting from defaults.
/// </summary>
internal static class Storage
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static string Folder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "SpiderSolitaire");

    private static string SettingsPath => Path.Combine(Folder, "settings.json");

    private static string StatisticsPath => Path.Combine(Folder, "statistics.json");

    private static string SavedGamePath => Path.Combine(Folder, "saved-game.json");

    /// <summary>False on the very first run, which is when the game asks for a difficulty.</summary>
    public static bool SettingsExist => File.Exists(SettingsPath);

    public static AppSettings LoadSettings()
    {
        AppSettings settings = Read<AppSettings>(SettingsPath) ?? new AppSettings();
        settings.Normalize();
        return settings;
    }

    public static string? SaveSettings(AppSettings settings) => Write(SettingsPath, settings);

    public static GameStatistics LoadStatistics()
    {
        GameStatistics statistics = Read<GameStatistics>(StatisticsPath) ?? new GameStatistics();

        // A hand-edited file can null out parts the code assumes are always there.
        statistics.OneSuit ??= new DifficultyStatistics();
        statistics.TwoSuits ??= new DifficultyStatistics();
        statistics.FourSuits ??= new DifficultyStatistics();
        foreach (Difficulty difficulty in DifficultyExtensions.All)
        {
            statistics.For(difficulty).HighScores ??= new List<HighScore>();
        }

        return statistics;
    }

    public static string? SaveStatistics(GameStatistics statistics) => Write(StatisticsPath, statistics);

    public static SavedGame? LoadSavedGame() => Read<SavedGame>(SavedGamePath);

    public static string? SaveGame(SavedGame game) => Write(SavedGamePath, game);

    public static void DeleteSavedGame()
    {
        try
        {
            File.Delete(SavedGamePath);
        }
        catch (Exception)
        {
            // A saved game that cannot be deleted will be offered again next time; nothing
            // better can be done about it here.
        }
    }

    private static T? Read<T>(string path)
        where T : class
    {
        try
        {
            return File.Exists(path) ? JsonSerializer.Deserialize<T>(File.ReadAllText(path), Options) : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>Returns null on success, or a message describing why nothing was saved.</summary>
    private static string? Write<T>(string path, T value)
    {
        try
        {
            Directory.CreateDirectory(Folder);

            // Write then swap, so a crash mid-write cannot leave a half-written file behind.
            string temporary = path + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(value, Options));
            File.Move(temporary, path, overwrite: true);
            return null;
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }
}
