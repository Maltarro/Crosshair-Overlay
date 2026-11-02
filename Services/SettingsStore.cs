using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using CrosshairOverlay.Models;

namespace CrosshairOverlay.Services;

public static class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private static string DirectoryPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "CrosshairOverlay");

    public static string FilePath => Path.Combine(DirectoryPath, "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                return new AppSettings();
            }

            var json = File.ReadAllText(FilePath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
            settings.Normalize();
            return settings;
        }
        catch
        {
            return new AppSettings();
        }
    }

    public static void Save(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(DirectoryPath);
            var json = JsonSerializer.Serialize(settings, JsonOptions);
            File.WriteAllText(FilePath, json);
        }
        catch
        {
            // Saving is best-effort: never crash the overlay because of a locked file.
        }
    }
}
