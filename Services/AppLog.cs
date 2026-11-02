using System.IO;

namespace CrosshairOverlay.Services;

/// <summary>Best-effort diagnostics written next to the settings file.</summary>
public static class AppLog
{
    private static string DirectoryPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "CrosshairOverlay");

    public static string FilePath => Path.Combine(DirectoryPath, "app.log");

    public static void Write(string message, Exception? exception = null)
    {
        try
        {
            Directory.CreateDirectory(DirectoryPath);
            var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}";
            if (exception is not null)
            {
                line += Environment.NewLine + exception;
            }

            File.AppendAllText(FilePath, line + Environment.NewLine);
        }
        catch
        {
            // Logging must never take the application down.
        }
    }
}
