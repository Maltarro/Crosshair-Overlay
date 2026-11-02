using Microsoft.Win32;

namespace CrosshairOverlay.Services;

/// <summary>Registers the application in the current user's Run key.</summary>
public static class AutostartService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "CrosshairOverlay";

    public static void Apply(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
                            ?? Registry.CurrentUser.CreateSubKey(RunKeyPath);

            if (key is null)
            {
                return;
            }

            if (!enabled)
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
                return;
            }

            var executable = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(executable))
            {
                return;
            }

            key.SetValue(ValueName, $"\"{executable}\" --hidden");
        }
        catch
        {
            // Autostart is best-effort: a locked registry key must never break the overlay.
        }
    }
}
