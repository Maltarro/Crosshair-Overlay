using System.ComponentModel;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using CrosshairOverlay.Interop;
using CrosshairOverlay.Models;
using CrosshairOverlay.Services;

namespace CrosshairOverlay;

public partial class App : System.Windows.Application
{
    private Mutex? _singleInstanceMutex;
    private AppSettings? _settings;
    private OverlayWindow? _overlay;
    private MainWindow? _settingsWindow;
    private TrayIconService? _trayIcon;
    private GlobalKeyboardHook? _keyboardHook;
    private NotificationService? _notifications;
    private DispatcherTimer? _saveTimer;
    private DispatcherTimer? _visibilityTimer;
    private bool _isExiting;
    private bool _notificationsReady;

    public bool IsExiting => _isExiting;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;

        _singleInstanceMutex = new Mutex(true, @"Local\CrosshairOverlay_SingleInstance", out var createdNew);
        if (!createdNew)
        {
            MessageBox.Show(
                "Crosshair Overlay уже запущен. Нажмите F9, чтобы открыть его окно настроек.",
                "Crosshair Overlay",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            Shutdown();
            return;
        }

        _settings = SettingsStore.Load();
        NormalizeMonitorSelection(_settings);
        _settings.PropertyChanged += OnSettingsChanged;
        AutostartService.Apply(_settings.StartWithWindows);
        _notifications = new NotificationService();

        _saveTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(600)
        };
        _saveTimer.Tick += SaveTimerOnTick;

        // Poll the foreground window so the fullscreen-only mode reacts quickly to
        // alt-tabs without relying on low-level window hooks.
        _visibilityTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(400)
        };
        _visibilityTimer.Tick += (_, _) => UpdateOverlayVisibility();

        _overlay = new OverlayWindow(_settings);

        _settingsWindow = new MainWindow(_settings);

        try
        {
            _trayIcon = new TrayIconService(
                _settings,
                ShowSettingsWindow,
                ToggleCrosshair,
                SetVisibilityMode,
                ExitApplication);
        }
        catch
        {
            // The tray icon is a convenience: hotkeys and the overlay keep working without it.
        }

        var startHidden = _settings.StartHidden || e.Args.Any(arg =>
            string.Equals(arg, "--hidden", StringComparison.OrdinalIgnoreCase));

        if (!startHidden)
        {
            _settingsWindow.Show();
        }

        try
        {
            _keyboardHook = new GlobalKeyboardHook();
            _keyboardHook.KeyPressed += OnGlobalKeyPressed;
        }
        catch (Exception ex)
        {
            ShowSettingsWindow();
            MessageBox.Show(
                _settingsWindow,
                "Не удалось установить глобальный хук клавиатуры, поэтому F8/F9 не работают.\n\n" + ex.Message,
                "Crosshair Overlay",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }

        UpdateOverlayVisibility();
        _visibilityTimer.Start();
        _notificationsReady = true;

        if (_settings is not null)
        {
            _notifications?.Show(
                "Приложение запущено",
                $"{_settings.ToggleHotkey} - прицел, {_settings.SettingsHotkey} - настройки.",
                NotificationKind.Info);
        }
    }

    public void ShowSettingsWindow()
    {
        if (_settingsWindow is null)
        {
            return;
        }

        if (!_settingsWindow.IsVisible)
        {
            _settingsWindow.Show();
        }

        if (_settingsWindow.WindowState == WindowState.Minimized)
        {
            _settingsWindow.WindowState = WindowState.Normal;
        }

        // A short topmost pulse makes the window come to the foreground even when
        // the app was hidden while another program owned the foreground.
        _settingsWindow.Topmost = true;
        _settingsWindow.Activate();
        _settingsWindow.Topmost = false;
        _settingsWindow.Focus();
    }

    public void ExitApplication()
    {
        if (_isExiting)
        {
            return;
        }

        _isExiting = true;
        _saveTimer?.Stop();
        _visibilityTimer?.Stop();

        if (_settings is not null)
        {
            SettingsStore.Save(_settings);
        }

        _settingsWindow?.AllowClose();
        Shutdown();
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        if (_isExiting)
        {
            return;
        }

        AppLog.Write("Unhandled UI exception", e.Exception);
        e.Handled = true;

        // Keep the overlay alive and tell the user instead of closing the whole app.
        _notifications?.Show(
            "Ошибка",
            "Что-то пошло не так, но приложение продолжает работать.",
            NotificationKind.Warning);
    }

    private static void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e) =>
        AppLog.Write("Unhandled domain exception", e.ExceptionObject as Exception);

    private void OnGlobalKeyPressed(object? sender, int virtualKey)
    {
        if (_settings is null)
        {
            return;
        }

        if (MatchesHotkey(virtualKey, _settings.ToggleHotkey))
        {
            ToggleCrosshair();
            return;
        }

        if (MatchesHotkey(virtualKey, _settings.SettingsHotkey))
        {
            ShowSettingsWindow();
        }
    }

    private static bool MatchesHotkey(int virtualKey, string hotkey)
    {
        if (!Enum.TryParse<Key>(hotkey, ignoreCase: true, out var key))
        {
            return false;
        }

        return KeyInterop.VirtualKeyFromKey(key) == virtualKey;
    }

    private void ToggleCrosshair()
    {
        if (_settings is not null)
        {
            _settings.Enabled = !_settings.Enabled;
        }
    }

    private void SetVisibilityMode(OverlayVisibilityMode mode)
    {
        if (_settings is not null)
        {
            _settings.VisibilityMode = mode;
        }
    }

    /// <summary>Shows an always-on-top toast notification, independent of the crosshair settings.</summary>
    public void Notify(string title, string message, NotificationKind kind = NotificationKind.Info) =>
        _notifications?.Show(title, message, kind);

    private void OnSettingsChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(AppSettings.Enabled)
            or nameof(AppSettings.VisibilityMode)
            or null)
        {
            UpdateOverlayVisibility();

            if (_notificationsReady && _settings is not null)
            {
                ShowStateNotification(e.PropertyName);
            }
        }

        if (e.PropertyName is nameof(AppSettings.StartWithWindows) && _settings is not null)
        {
            AutostartService.Apply(_settings.StartWithWindows);

            if (_notificationsReady)
            {
                _notifications?.Show(
                    "Автозапуск",
                    _settings.StartWithWindows
                        ? "Приложение будет запускаться вместе с Windows."
                        : "Автозапуск отключён.",
                    NotificationKind.Info);
            }
        }

        _saveTimer?.Stop();
        _saveTimer?.Start();
    }

    private void ShowStateNotification(string? propertyName)
    {
        if (_settings is null || _notifications is null)
        {
            return;
        }

        if (propertyName is nameof(AppSettings.VisibilityMode))
        {
            var fullscreenOnly = _settings.VisibilityMode == OverlayVisibilityMode.FullscreenOnly;
            _notifications.Show(
                "Режим показа",
                fullscreenOnly
                    ? "Прицел виден только в полноэкранных приложениях."
                    : "Прицел виден всегда, поверх любых окон.",
                NotificationKind.Info);
            return;
        }

        if (_settings.Enabled)
        {
            _notifications.Show(
                "Прицел включён",
                $"Нажмите {_settings.ToggleHotkey}, чтобы скрыть прицел.",
                NotificationKind.Success);
        }
        else
        {
            _notifications.Show(
                "Прицел скрыт",
                $"Нажмите {_settings.ToggleHotkey}, чтобы вернуть прицел.",
                NotificationKind.Info);
        }
    }

    private void UpdateOverlayVisibility()
    {
        if (_overlay is null || _settings is null)
        {
            return;
        }

        // The overlay stays visible even while the settings window is open: the window
        // is docked to the edge of the monitor, so both can be seen at the same time.
        // In fullscreen-only mode the crosshair follows the foreground application.
        var shouldShow = _settings.Enabled
                         && !_isExiting
                         && (_settings.VisibilityMode == OverlayVisibilityMode.Always
                             || FullscreenDetector.IsForegroundFullscreen());

        if (shouldShow)
        {
            if (!_overlay.IsVisible)
            {
                _overlay.Show();
            }
        }
        else if (_overlay.IsVisible)
        {
            _overlay.Hide();
        }
    }

    private static void NormalizeMonitorSelection(AppSettings settings)
    {
        var monitors = MonitorService.GetMonitors();
        if (monitors.Count == 0)
        {
            settings.MonitorDeviceName = string.Empty;
            return;
        }

        var isKnown = monitors.Any(m =>
            string.Equals(m.DeviceName, settings.MonitorDeviceName, StringComparison.OrdinalIgnoreCase));

        if (!isKnown)
        {
            var target = MonitorService.GetMonitorFromCursor()
                         ?? monitors.FirstOrDefault(m => m.IsPrimary)
                         ?? monitors[0];
            settings.MonitorDeviceName = target.DeviceName;
        }
    }

    private void SaveTimerOnTick(object? sender, EventArgs e)
    {
        _saveTimer?.Stop();

        if (_settings is not null)
        {
            SettingsStore.Save(_settings);
        }
    }

    protected override void OnSessionEnding(SessionEndingCancelEventArgs e)
    {
        _isExiting = true;
        _settingsWindow?.AllowClose();

        if (_settings is not null)
        {
            SettingsStore.Save(_settings);
        }

        base.OnSessionEnding(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _saveTimer?.Stop();
        _visibilityTimer?.Stop();

        _notifications?.Dispose();
        _notifications = null;

        _trayIcon?.Dispose();
        _trayIcon = null;

        if (_settings is not null)
        {
            SettingsStore.Save(_settings);
        }

        _keyboardHook?.Dispose();

        try
        {
            _singleInstanceMutex?.ReleaseMutex();
        }
        catch (ApplicationException)
        {
            // The mutex was never owned by this process (second instance).
        }

        _singleInstanceMutex?.Dispose();

        base.OnExit(e);
    }
}
