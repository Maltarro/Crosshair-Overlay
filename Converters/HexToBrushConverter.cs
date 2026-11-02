using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using CrosshairOverlay.Utilities;

namespace CrosshairOverlay.Converters;

public sealed class HexToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var color = ColorUtil.Parse(value as string, Colors.White);
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
