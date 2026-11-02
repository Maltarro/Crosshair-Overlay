using System.Globalization;
using System.Windows;
using System.Windows.Data;
using CrosshairOverlay.Models;

namespace CrosshairOverlay.Converters;

/// <summary>
/// Shows or hides a settings row depending on the selected crosshair style.
/// When the binding target is a boolean (for example an attached visibility
/// behavior), the converter returns whether the feature applies.
/// </summary>
public sealed class StyleFeatureConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var style = value as CrosshairStyle?;
        var feature = parameter as string;
        var isVisible = style is { } selected
            ? feature switch
            {
                "thickness" => selected is not CrosshairStyle.Dot,
                "cross" => selected is CrosshairStyle.Cross
                    or CrosshairStyle.CrossDot
                    or CrosshairStyle.TShape
                    or CrosshairStyle.XShape,
                "length" => selected is CrosshairStyle.Cross
                    or CrosshairStyle.CrossDot
                    or CrosshairStyle.TShape
                    or CrosshairStyle.XShape
                    or CrosshairStyle.Brackets,
                "gap" => selected is CrosshairStyle.Cross
                    or CrosshairStyle.CrossDot
                    or CrosshairStyle.TShape
                    or CrosshairStyle.XShape,
                "arms" => selected is CrosshairStyle.Cross
                    or CrosshairStyle.CrossDot
                    or CrosshairStyle.TShape
                    or CrosshairStyle.XShape
                    or CrosshairStyle.Brackets,
                "dot" => selected is CrosshairStyle.Dot
                    or CrosshairStyle.CrossDot
                    or CrosshairStyle.CircleDot,
                "circle" => selected is CrosshairStyle.Circle
                    or CrosshairStyle.CircleDot
                    or CrosshairStyle.Brackets,
                "rotation" => selected is CrosshairStyle.Cross
                    or CrosshairStyle.CrossDot
                    or CrosshairStyle.TShape
                    or CrosshairStyle.XShape
                    or CrosshairStyle.Brackets,
                _ => true
            }
            : false;

        if (targetType == typeof(bool))
        {
            return isVisible;
        }

        return isVisible ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
