using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using CrosshairOverlay.Utilities;

namespace CrosshairOverlay.Controls;

/// <summary>
/// Neon-styled HSV color picker used by the settings window.
/// </summary>
public partial class ColorPickerWindow : Window
{
    private readonly Color _originalColor;
    private double _hue;
    private double _saturation;
    private double _value;
    private bool _syncingHex;
    private Color _current;

    public ColorPickerWindow(string target, Color initial)
    {
        InitializeComponent();

        Title = $"Выбор цвета - {target}";
        TargetText.Text = target;
        _originalColor = initial;
        (_hue, _saturation, _value) = ColorMath.ToHsv(initial);
        _current = initial;

        OriginalSwatch.Background = new SolidColorBrush(initial);
        Loaded += OnLoaded;

        UpdateColor(updateHex: true);
    }

    /// <summary>The color confirmed by the user, or <c>null</c> when cancelled.</summary>
    public Color? SelectedColor { get; private set; }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        ease.Freeze();

        var duration = TimeSpan.FromMilliseconds(220);
        Root.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, duration)
        {
            EasingFunction = ease
        });
        RootScale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.97, 1, duration)
        {
            EasingFunction = ease
        });
        RootScale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.97, 1, duration)
        {
            EasingFunction = ease
        });

        UpdateMarkers();
    }

    private void UpdateColor(bool updateHex)
    {
        _current = ColorMath.FromHsv(_hue, _saturation, _value);

        var currentBrush = new SolidColorBrush(_current);
        currentBrush.Freeze();
        CurrentSwatch.Background = currentBrush;
        HueMarker.Background = currentBrush;

        HexPreviewText.Text = ColorUtil.ToHex(_current);
        RgbText.Text = $"R {_current.R}   G {_current.G}   B {_current.B}";

        var hueBrush = new SolidColorBrush(ColorMath.FromHsv(_hue, 1, 1));
        hueBrush.Freeze();
        SvHueLayer.Fill = hueBrush;

        if (updateHex && !_syncingHex)
        {
            _syncingHex = true;
            HexBox.Text = ColorUtil.ToHex(_current);
            HexBox.CaretIndex = HexBox.Text.Length;
            _syncingHex = false;
        }

        UpdateMarkers();
    }

    private void UpdateMarkers()
    {
        var width = SvArea.ActualWidth;
        var height = SvArea.ActualHeight;
        if (width > 0 && height > 0)
        {
            var x = Math.Clamp(_saturation * width, 0, width);
            var y = Math.Clamp((1 - _value) * height, 0, height);
            Canvas.SetLeft(SvMarker, x - SvMarker.Width / 2);
            Canvas.SetTop(SvMarker, y - SvMarker.Height / 2);
        }

        var hueWidth = HueArea.ActualWidth;
        if (hueWidth > 0)
        {
            var x = Math.Clamp(_hue / 360.0 * hueWidth, 0, hueWidth);
            Canvas.SetLeft(HueMarker, x - HueMarker.Width / 2);
            Canvas.SetTop(HueMarker, (HueArea.ActualHeight - HueMarker.Height) / 2);
        }
    }

    private void UpdateSaturationValue(Point point)
    {
        var width = SvArea.ActualWidth;
        var height = SvArea.ActualHeight;
        if (width <= 0 || height <= 0)
        {
            return;
        }

        _saturation = Math.Clamp(point.X / width, 0, 1);
        _value = 1 - Math.Clamp(point.Y / height, 0, 1);
        UpdateColor(updateHex: true);
    }

    private void UpdateHue(Point point)
    {
        var width = HueArea.ActualWidth;
        if (width <= 0)
        {
            return;
        }

        _hue = Math.Clamp(point.X / width, 0, 1) * 360;
        UpdateColor(updateHex: true);
    }

    private void SvArea_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        SvArea.CaptureMouse();
        UpdateSaturationValue(e.GetPosition(SvArea));
        e.Handled = true;
    }

    private void SvArea_MouseMove(object sender, MouseEventArgs e)
    {
        if (SvArea.IsMouseCaptured && e.LeftButton == MouseButtonState.Pressed)
        {
            UpdateSaturationValue(e.GetPosition(SvArea));
        }
    }

    private void SvArea_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (SvArea.IsMouseCaptured)
        {
            SvArea.ReleaseMouseCapture();
        }
    }

    private void SvArea_SizeChanged(object sender, SizeChangedEventArgs e) => UpdateMarkers();

    private void HueArea_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        HueArea.CaptureMouse();
        UpdateHue(e.GetPosition(HueArea));
        e.Handled = true;
    }

    private void HueArea_MouseMove(object sender, MouseEventArgs e)
    {
        if (HueArea.IsMouseCaptured && e.LeftButton == MouseButtonState.Pressed)
        {
            UpdateHue(e.GetPosition(HueArea));
        }
    }

    private void HueArea_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (HueArea.IsMouseCaptured)
        {
            HueArea.ReleaseMouseCapture();
        }
    }

    private void HueArea_SizeChanged(object sender, SizeChangedEventArgs e) => UpdateMarkers();

    private void HexBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_syncingHex)
        {
            return;
        }

        var text = HexBox.Text.Trim();
        if (text.Length is 4 or 7 && text.StartsWith('#') && ColorUtil.TryParse(text, out var color))
        {
            (_hue, _saturation, _value) = ColorMath.ToHsv(color);
            UpdateColor(updateHex: false);
        }
    }

    private void HexBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            Apply_Click(sender, e);
            e.Handled = true;
        }
    }

    private void Preset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string hex } && ColorUtil.TryParse(hex, out var color))
        {
            (_hue, _saturation, _value) = ColorMath.ToHsv(color);
            UpdateColor(updateHex: true);
        }
    }

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        SelectedColor = _current;
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        if (IsLoaded)
        {
            DialogResult = false;
        }
        else
        {
            Close();
        }
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState != MouseButtonState.Pressed || IsInsideButton(e.OriginalSource as DependencyObject))
        {
            return;
        }

        try
        {
            DragMove();
        }
        catch (InvalidOperationException)
        {
            // The mouse button may have been released between the event and DragMove.
        }
    }

    private static bool IsInsideButton(DependencyObject? element)
    {
        while (element is not null)
        {
            if (element is Button)
            {
                return true;
            }

            element = VisualTreeHelper.GetParent(element);
        }

        return false;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
            return;
        }

        base.OnKeyDown(e);
    }
}
