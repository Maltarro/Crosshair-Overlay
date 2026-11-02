using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using CrosshairOverlay.Interop;
using CrosshairOverlay.Models;
using CrosshairOverlay.Services;
using SysForms = System.Windows.Forms;

namespace CrosshairOverlay.Controls;

/// <summary>Rounded glass tray menu, drawn with WPF so that it matches the rest of the app.</summary>
public partial class TrayMenuWindow : Window
{
    private readonly AppSettings _settings;
    private readonly Action _openSettings;
    private readonly Action _toggleCrosshair;
    private readonly Action<OverlayVisibilityMode> _setVisibilityMode;
    private readonly Action _exitApplication;

    private bool _activatedOnce;
    private bool _closing;

    public TrayMenuWindow(
        AppSettings settings,
        Action openSettings,
        Action toggleCrosshair,
        Action<OverlayVisibilityMode> setVisibilityMode,
        Action exitApplication)
    {
        InitializeComponent();

        _settings = settings;
        _openSettings = openSettings;
        _toggleCrosshair = toggleCrosshair;
        _setVisibilityMode = setVisibilityMode;
        _exitApplication = exitApplication;

        _settings.PropertyChanged += OnSettingsChanged;
        RefreshState();

        Loaded += OnLoaded;
        Activated += (_, _) => _activatedOnce = true;
        Deactivated += OnDeactivated;
        PreviewKeyDown += OnPreviewKeyDown;
        Closed += OnClosed;
    }

    public void ShowAtCursor()
    {
        var cursor = SysForms.Cursor.Position;
        var scale = Math.Max(0.5, MonitorService.GetSystemScale());
        var monitor = MonitorService.GetMonitorFromCursor() ?? MonitorService.GetPrimary();

        double leftBound = SystemParameters.WorkArea.Left;
        double topBound = SystemParameters.WorkArea.Top;
        double rightBound = SystemParameters.WorkArea.Right;
        double bottomBound = SystemParameters.WorkArea.Bottom;

        if (monitor is { } target)
        {
            leftBound = target.WorkArea.X / scale;
            topBound = target.WorkArea.Y / scale;
            rightBound = target.WorkArea.Right / scale;
            bottomBound = target.WorkArea.Bottom / scale;
        }

        // The window measures itself (SizeToContent), so use an estimate for the
        // first placement and refine it once the layout is complete.
        var width = Width > 0 ? Width : 292;
        var estimatedHeight = 300;

        Left = Math.Round(Math.Clamp(
            cursor.X / scale - width + 12,
            leftBound + 8,
            Math.Max(leftBound + 8, rightBound - width - 8)));
        Top = Math.Round(Math.Clamp(
            cursor.Y / scale - estimatedHeight - 12,
            topBound + 8,
            Math.Max(topBound + 8, bottomBound - estimatedHeight - 8)));

        Show();
        UpdateLayout();

        var actualWidth = ActualWidth > 0 ? ActualWidth : width;
        var actualHeight = ActualHeight > 0 ? ActualHeight : estimatedHeight;

        Left = Math.Round(Math.Clamp(
            cursor.X / scale - actualWidth + 12,
            leftBound + 8,
            Math.Max(leftBound + 8, rightBound - actualWidth - 8)));
        Top = Math.Round(Math.Clamp(
            cursor.Y / scale - actualHeight - 12,
            topBound + 8,
            Math.Max(topBound + 8, bottomBound - actualHeight - 8)));

        Activate();
    }

    private void OnDeactivated(object? sender, EventArgs e)
    {
        if (!_activatedOnce || _closing)
        {
            return;
        }

        // Closing from inside the Deactivated event can race with activation.
        // Defer it and only close if the menu is still inactive.
        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (!_closing && IsVisible && !IsActive)
            {
                Close();
            }
        }), System.Windows.Threading.DispatcherPriority.Background);
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        _closing = true;
        base.OnClosing(e);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        ease.Freeze();

        Host.Opacity = 0;
        Host.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(160))
        {
            EasingFunction = ease
        });

        MenuScale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.96, 1, TimeSpan.FromMilliseconds(200))
        {
            EasingFunction = ease
        });
        MenuScale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.96, 1, TimeSpan.FromMilliseconds(200))
        {
            EasingFunction = ease
        });
        MenuSlide.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(10, 0, TimeSpan.FromMilliseconds(220))
        {
            EasingFunction = ease
        });
    }

    private void RefreshState()
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(new Action(RefreshState));
            return;
        }

        var enabled = _settings.Enabled;
        var fullscreenOnly = _settings.VisibilityMode == OverlayVisibilityMode.FullscreenOnly;

        ToggleText.Text = enabled ? "Скрыть прицел" : "Показать прицел";
        ToggleKeyText.Text = _settings.ToggleHotkey;
        OpenKeyText.Text = _settings.SettingsHotkey;

        AlwaysDot.Visibility = fullscreenOnly ? Visibility.Collapsed : Visibility.Visible;
        FullscreenDot.Visibility = fullscreenOnly ? Visibility.Visible : Visibility.Collapsed;

        var accent = (Brush)FindResource("AccentBrush");
        var muted = (Brush)FindResource("MutedBrush");

        ToggleIcon.Foreground = enabled ? accent : muted;
        StatusPillText.Text = enabled ? "ВКЛЮЧЁН" : "ВЫКЛЮЧЕН";
        StatusPillText.Foreground = enabled ? accent : muted;
        StatusPill.BorderBrush = enabled ? accent : muted;
        StatusPill.Background = new SolidColorBrush(Color.FromArgb(0x26, 0xC5, 0x6C, 0xFF));
    }

    private void OnSettingsChanged(object? sender, PropertyChangedEventArgs e) => RefreshState();

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Close();
        }
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _settings.PropertyChanged -= OnSettingsChanged;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        var handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero)
        {
            return;
        }

        var style = NativeMethods.GetWindowLongW(handle, NativeMethods.GWL_EXSTYLE);
        style |= NativeMethods.WS_EX_TOOLWINDOW;
        NativeMethods.SetWindowLongW(handle, NativeMethods.GWL_EXSTYLE, style);
    }

    private void OpenSettings_Click(object sender, RoutedEventArgs e)
    {
        Close();
        _openSettings();
    }

    private void Toggle_Click(object sender, RoutedEventArgs e)
    {
        Close();
        _toggleCrosshair();
    }

    private void Always_Click(object sender, RoutedEventArgs e)
    {
        Close();
        _setVisibilityMode(OverlayVisibilityMode.Always);
    }

    private void Fullscreen_Click(object sender, RoutedEventArgs e)
    {
        Close();
        _setVisibilityMode(OverlayVisibilityMode.FullscreenOnly);
    }

    private void Exit_Click(object sender, RoutedEventArgs e)
    {
        Close();
        _exitApplication();
    }
}
