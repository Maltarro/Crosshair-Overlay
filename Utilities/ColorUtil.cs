using System.Globalization;
using System.Windows.Media;

namespace CrosshairOverlay.Utilities;

public static class ColorUtil
{
    public static bool TryParse(string? value, out Color color)
    {
        color = Colors.White;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        try
        {
            if (ColorConverter.ConvertFromString(value.Trim()) is Color parsed)
            {
                color = Color.FromRgb(parsed.R, parsed.G, parsed.B);
                return true;
            }
        }
        catch (Exception)
        {
            // Invalid user input, fall through.
        }

        return false;
    }

    public static Color Parse(string? value, Color fallback) =>
        TryParse(value, out var color) ? color : fallback;

    public static string ToHex(Color color) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"#{color.R:X2}{color.G:X2}{color.B:X2}");
}
