using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using CrosshairOverlay.Controls;
using CrosshairOverlay.Models;
using CrosshairOverlay.Services;
using CrosshairOverlay.Utilities;

namespace CrosshairOverlay;

public partial class MainWindow : Window
{
    private const double ScreenMargin = 16;

    private readonly AppSettings _settings;
    private bool _allowClose;
    private double _workTop;
    private double _workHeight = SystemParameters.WorkArea.Height;

    public MainWindow(AppSettings settings)
    {
        _settings = settings;

        InitializeComponent();

        DataContext = settings;
        PreviewCrosshair.Settings = settings;
        PreviewCrosshair.ApplyOffset = false;
        PreviewCrosshair.PreviewMode = true;
        PreviewCrosshair.ForceVisible = true;

        StyleCombo.ItemsSource = new[]
        {
            new StyleOption("Крест", CrosshairStyle.Cross),
            new StyleOption("Т-образный", CrosshairStyle.TShape),
            new StyleOption("X-образный", CrosshairStyle.XShape),
            new StyleOption("Крест с точкой", CrosshairStyle.CrossDot),
            new StyleOption("Точка", CrosshairStyle.Dot),
            new StyleOption("Круг", CrosshairStyle.Circle),
            new StyleOption("Круг с точкой", CrosshairStyle.CircleDot),
            new StyleOption("Скобки", CrosshairStyle.Brackets)
        };

        VisibilityCombo.ItemsSource = new[]
        {
            new OverlayModeOption("Всегда", OverlayVisibilityMode.Always),
            new OverlayModeOption("Только в полноэкранном режиме", OverlayVisibilityMode.FullscreenOnly)
        };

        RefreshMonitors();
        PositionOnMonitor(GetTargetMonitor());

        IsVisibleChanged += OnIsVisibleChanged;
        SizeChanged += OnWindowSizeChanged;
    }

    /// <summary>
    /// Replays a soft entrance every time the window is shown: the frame fades in,
    /// then the buttons cascade with a gentle lift.
    /// </summary>
    private void PlayIntroAnimation()
    {
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        ease.Freeze();

        var frameDuration = TimeSpan.FromMilliseconds(300);
        WindowRoot.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, frameDuration)
        {
            EasingFunction = ease
        });
        RootScale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.985, 1, frameDuration)
        {
            EasingFunction = ease
        });
        RootScale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.985, 1, frameDuration)
        {
            EasingFunction = ease
        });

        var buttons = FindButtons(this)
            .Select(button => new
            {
                Button = button,
                Position = button.TranslatePoint(new Point(0, 0), this)
            })
            .OrderBy(item => Math.Round(item.Position.Y / 8.0))
            .ThenBy(item => item.Position.X)
            .Select(item => item.Button)
            .ToList();

        if (buttons.Count == 0)
        {
            return;
        }

        var duration = TimeSpan.FromMilliseconds(320);

        for (var index = 0; index < buttons.Count; index++)
        {
            var button = buttons[index];
            var slide = new TranslateTransform(0, 10);
            button.RenderTransform = slide;
            button.Opacity = 0;

            var delay = TimeSpan.FromMilliseconds(index * 14);
            var fade = new DoubleAnimation(0, 1, duration)
            {
                BeginTime = delay,
                EasingFunction = ease
            };
            var move = new DoubleAnimation(10, 0, duration)
            {
                BeginTime = delay,
                EasingFunction = ease
            };

            button.BeginAnimation(OpacityProperty, fade);
            slide.BeginAnimation(TranslateTransform.YProperty, move);
        }
    }

    private static IEnumerable<Button> FindButtons(DependencyObject root)
    {
        var childCount = VisualTreeHelper.GetChildrenCount(root);
        for (var index = 0; index < childCount; index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is Button button)
            {
                yield return button;
            }

            foreach (var nested in FindButtons(child))
            {
                yield return nested;
            }
        }
    }

    private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is not true)
        {
            return;
        }

        RefreshMonitors();
        PositionOnMonitor(GetTargetMonitor());

        // Hide the frame before the first rendered frame so re-showing never flashes.
        WindowRoot.BeginAnimation(OpacityProperty, null);
        WindowRoot.Opacity = 0;
        RootScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        RootScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        RootScale.ScaleX = 0.985;
        RootScale.ScaleY = 0.985;

        // Wait for layout so the button cascade starts from correct positions.
        Dispatcher.BeginInvoke(new Action(PlayIntroAnimation), DispatcherPriority.Loaded);
    }

    private void RefreshMonitors()
    {
        var selectedDevice = _settings.MonitorDeviceName;
        var monitors = MonitorService.GetMonitors();

        MonitorCombo.ItemsSource = monitors;

        if (monitors.Count == 0)
        {
            return;
        }

        var isKnown = monitors.Any(m =>
            string.Equals(m.DeviceName, selectedDevice, StringComparison.OrdinalIgnoreCase));

        if (!isKnown)
        {
            var target = MonitorService.GetMonitorFromCursor()
                         ?? monitors.FirstOrDefault(m => m.IsPrimary)
                         ?? monitors[0];
            selectedDevice = target.DeviceName;
        }

        _settings.MonitorDeviceName = selectedDevice;
        MonitorCombo.SelectedValue = selectedDevice;
    }

    private MonitorInfo? GetTargetMonitor() =>
        MonitorService.GetMonitorByDeviceName(_settings.MonitorDeviceName)
        ?? MonitorService.GetMonitorFromCursor()
        ?? MonitorService.GetPrimary();

    /// <summary>Places the settings window on the left edge of the selected monitor, vertically centred.</summary>
    private void PositionOnMonitor(MonitorInfo? monitor)
    {
        var scale = MonitorService.GetSystemScale();
        var work = monitor?.WorkArea;

        if (work is null)
        {
            _workTop = SystemParameters.WorkArea.Top / scale;
            _workHeight = SystemParameters.WorkArea.Height / scale;
            MaxHeight = Math.Max(MinHeight, _workHeight - 24);
            ApplyVerticalPosition();
            return;
        }

        var workHeight = work.Value.Height / scale;
        var workLeft = work.Value.X / scale;
        var workTop = work.Value.Y / scale;

        _workTop = workTop;
        _workHeight = workHeight;
        MaxHeight = Math.Max(MinHeight, workHeight - 24);
        Left = Math.Round(workLeft + ScreenMargin);
        ApplyVerticalPosition();
    }

    /// <summary>
    /// Keeps the auto-sized window centred after its content changes height.
    /// </summary>
    private void ApplyVerticalPosition()
    {
        var height = ActualHeight > 0 ? ActualHeight : MinHeight;
        Top = Math.Round(_workTop + Math.Max(0, (_workHeight - height) / 2.0));
    }

    private void OnWindowSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (WindowState == WindowState.Normal)
        {
            ApplyVerticalPosition();
        }
    }

    /// <summary>Lets the app close the window during shutdown instead of hiding it.</summary>
    public void AllowClose() => _allowClose = true;

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_allowClose)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        base.OnClosing(e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Hide();
            e.Handled = true;
            return;
        }

        base.OnKeyDown(e);
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
            // DragMove can fail if the mouse button was released in the meantime.
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

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void PickColor_Click(object sender, RoutedEventArgs e)
    {
        var tag = (sender as FrameworkElement)?.Tag as string;
        var isOutline = string.Equals(tag, "outline", StringComparison.Ordinal);
        var isGlow = string.Equals(tag, "glow", StringComparison.Ordinal);

        var currentHex = isOutline
            ? _settings.OutlineColor
            : isGlow
                ? _settings.GlowColor
                : _settings.Color;

        var fallback = isOutline
            ? Colors.Black
            : isGlow
                ? Color.FromRgb(0xC5, 0x6C, 0xFF)
                : Colors.White;

        var title = isOutline
            ? "Цвет обводки"
            : isGlow
                ? "Цвет свечения"
                : "Цвет прицела";

        var initial = ColorUtil.Parse(currentHex, fallback);

        var dialog = new ColorPickerWindow(title, initial)
        {
            Owner = this
        };

        if (dialog.ShowDialog() == true && dialog.SelectedColor is Color chosen)
        {
            var hex = ColorUtil.ToHex(chosen);
            if (isOutline)
            {
                _settings.OutlineColor = hex;
            }
            else if (isGlow)
            {
                _settings.GlowColor = hex;
            }
            else
            {
                _settings.Color = hex;
            }
        }
    }

    private void Telegram_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo("https://t.me/MaltarroChannel")
            {
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                $"Не удалось открыть ссылку: {ex.Message}",
                "Crosshair Overlay",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
    }

    private void Center_Click(object sender, RoutedEventArgs e)
    {
        _settings.OffsetX = 0;
        _settings.OffsetY = 0;
    }

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            this,
            "Сбросить все настройки прицела?",
            "Crosshair Overlay",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            _settings.Reset();
            RefreshMonitors();

            if (System.Windows.Application.Current is App app)
            {
                app.Notify("Настройки сброшены", "Прицел вернулся к значениям по умолчанию.", NotificationKind.Success);
            }
        }
    }

    private void Exit_Click(object sender, RoutedEventArgs e)
    {
        if (System.Windows.Application.Current is App app)
        {
            app.ExitApplication();
        }
    }
}
