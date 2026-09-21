using System.Text.Json;

namespace NetIPConfig;

/// <summary>Reads and writes <see cref="AppSettings"/> as JSON under the user's roaming profile.</summary>
internal static class SettingsStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    public static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "NetIPConfig",
        "settings.json");

    /// <summary>Never throws: a missing or damaged file just means default settings.</summary>
    public static AppSettings Load()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                return new AppSettings();
            }

            return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath), Options) ?? new AppSettings();
        }
        catch (Exception)
        {
            return new AppSettings();
        }
    }

    /// <summary>Returns null on success, or a message describing why nothing was saved.</summary>
    public static string? Save(AppSettings settings)
    {
        try
        {
            string? directory = Path.GetDirectoryName(FilePath);
            if (directory is not null)
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(FilePath, JsonSerializer.Serialize(settings, Options));
            return null;
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }
}
