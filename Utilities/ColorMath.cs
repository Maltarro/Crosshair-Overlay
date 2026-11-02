using System.Windows.Media;

namespace CrosshairOverlay.Utilities;

/// <summary>RGB/HSV conversions used by the color picker.</summary>
public static class ColorMath
{
    /// <summary>Converts a color to hue (0..360), saturation (0..1) and value (0..1).</summary>
    public static (double Hue, double Saturation, double Value) ToHsv(Color color)
    {
        var r = color.R / 255.0;
        var g = color.G / 255.0;
        var b = color.B / 255.0;

        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var delta = max - min;

        var hue = 0.0;
        if (delta > 0.00001)
        {
            if (max == r)
            {
                hue = 60 * (((g - b) / delta) % 6);
            }
            else if (max == g)
            {
                hue = 60 * (((b - r) / delta) + 2);
            }
            else
            {
                hue = 60 * (((r - g) / delta) + 4);
            }
        }

        if (hue < 0)
        {
            hue += 360;
        }

        var saturation = max <= 0.00001 ? 0 : delta / max;
        return (hue, saturation, max);
    }

    /// <summary>Builds an opaque color from hue (0..360), saturation (0..1) and value (0..1).</summary>
    public static Color FromHsv(double hue, double saturation, double value)
    {
        hue = ((hue % 360) + 360) % 360;
        saturation = Math.Clamp(saturation, 0, 1);
        value = Math.Clamp(value, 0, 1);

        var c = value * saturation;
        var x = c * (1 - Math.Abs((hue / 60.0) % 2 - 1));
        var m = value - c;

        var (r, g, b) = hue switch
        {
            < 60 => (c, x, 0.0),
            < 120 => (x, c, 0.0),
            < 180 => (0.0, c, x),
            < 240 => (0.0, x, c),
            < 300 => (x, 0.0, c),
            _ => (c, 0.0, x)
        };

        return Color.FromRgb(
            (byte)Math.Round((r + m) * 255),
            (byte)Math.Round((g + m) * 255),
            (byte)Math.Round((b + m) * 255));
    }
}
