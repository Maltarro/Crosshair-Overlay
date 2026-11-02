using System.ComponentModel;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using CrosshairOverlay.Interop;
using CrosshairOverlay.Models;
using CrosshairOverlay.Services;

namespace CrosshairOverlay;

public partial class OverlayWindow : Window
{
    private const double WindowSize = 600;

    private readonly AppSettings _settings;
    private readonly DispatcherTimer _topmostTimer;

    public OverlayWindow(AppSettings settings)
    {
        _settings = settings;

        InitializeComponent();

        Crosshair.Settings = settings;
        UpdatePlacement();

        _settings.PropertyChanged += OnSettingsChanged;

        _topmostTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(2)
        };
        _topmostTimer.Tick += (_, _) => ReassertTopmost();

        Loaded += OnLoaded;
        IsVisibleChanged += OnIsVisibleChanged;
        Closed += OnClosed;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        UpdatePlacement();
        ApplyOverlayWindowStyles();
        ReassertTopmost();
        _topmostTimer.Start();
    }

    private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (IsVisible)
        {
            UpdatePlacement();
            ReassertTopmost();
        }
    }

    private void OnSettingsChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(AppSettings.MonitorDeviceName) or null)
        {
            UpdatePlacement();
        }
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _topmostTimer.Stop();
        _settings.PropertyChanged -= OnSettingsChanged;
    }

    private void UpdatePlacement()
    {
        var monitor = MonitorService.GetMonitorByDeviceName(_settings.MonitorDeviceName)
                      ?? MonitorService.GetPrimary();

        if (monitor is null)
        {
            Left = Math.Round((SystemParameters.PrimaryScreenWidth - WindowSize) / 2.0);
            Top = Math.Round((SystemParameters.PrimaryScreenHeight - WindowSize) / 2.0);
            return;
        }

        // Window.Left/Top use DIPs; monitor bounds come from Win32 in physical pixels.
        var scale = MonitorService.GetSystemScale();
        var monitorWidth = monitor.Bounds.Width / scale;
        var monitorHeight = monitor.Bounds.Height / scale;

        Left = Math.Round(monitor.Bounds.X / scale + (monitorWidth - WindowSize) / 2.0);
        Top = Math.Round(monitor.Bounds.Y / scale + (monitorHeight - WindowSize) / 2.0);
    }

    private void ApplyOverlayWindowStyles()
    {
        var handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero)
        {
            return;
        }

        var style = NativeMethods.GetWindowLongW(handle, NativeMethods.GWL_EXSTYLE);
        style |= NativeMethods.WS_EX_TRANSPARENT
                 | NativeMethods.WS_EX_TOOLWINDOW
                 | NativeMethods.WS_EX_NOACTIVATE;
        NativeMethods.SetWindowLongW(handle, NativeMethods.GWL_EXSTYLE, style);
    }

    private void ReassertTopmost()
    {
        if (!IsVisible)
        {
            return;
        }

        var handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero)
        {
            return;
        }

        ApplyOverlayWindowStyles();
        NativeMethods.SetWindowPos(
            handle,
            NativeMethods.HWND_TOPMOST,
            0,
            0,
            0,
            0,
            NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);
    }
}
