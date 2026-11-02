using System.Globalization;
using System.Windows.Controls;
using CrosshairOverlay.Utilities;

namespace CrosshairOverlay.Converters;

public sealed class ColorValidationRule : ValidationRule
{
    public override ValidationResult Validate(object value, CultureInfo cultureInfo)
    {
        var text = value as string;
        return ColorUtil.TryParse(text, out _)
            ? new ValidationResult(true, null)
            : new ValidationResult(false, "Введите цвет в формате #RRGGBB или название цвета.");
    }
}
