using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using CrosshairOverlay.Controls;
using CrosshairOverlay.Models;

namespace CrosshairOverlay.Services;

/// <summary>Owns the notification-area icon and its rounded glass context menu.</summary>
public sealed class TrayIconService : IDisposable
{
    private readonly AppSettings _settings;
    private readonly NotifyIcon _notifyIcon;
    private readonly Icon _icon;
    private readonly Action _openSettings;
    private readonly Action _toggleCrosshair;
    private readonly Action<OverlayVisibilityMode> _setVisibilityMode;
    private readonly Action _exitApplication;

    private TrayMenuWindow? _menu;

    public TrayIconService(
        AppSettings settings,
        Action openSettings,
        Action toggleCrosshair,
        Action<OverlayVisibilityMode> setVisibilityMode,
        Action exitApplication)
    {
        _settings = settings;
        _openSettings = openSettings;
        _toggleCrosshair = toggleCrosshair;
        _setVisibilityMode = setVisibilityMode;
        _exitApplication = exitApplication;

        _icon = LoadApplicationIcon();

        _notifyIcon = new NotifyIcon
        {
            Icon = _icon,
            Text = "Crosshair Overlay",
            Visible = true
        };

        _notifyIcon.MouseUp += OnMouseUp;

        _settings.PropertyChanged += OnSettingsChanged;
        UpdateState();
    }

    public void Dispose()
    {
        _settings.PropertyChanged -= OnSettingsChanged;

        _notifyIcon.Visible = false;
        _notifyIcon.MouseUp -= OnMouseUp;
        _notifyIcon.Dispose();

        _menu?.Close();
        _menu = null;

        _icon.Dispose();
    }

    private void OnMouseUp(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Right)
        {
            OpenMenu();
            return;
        }

        if (e.Button == MouseButtons.Left)
        {
            _openSettings();
        }
    }

    private void OpenMenu()
    {
        try
        {
            if (_menu is not null)
            {
                var previous = _menu;
                _menu = null;
                previous.Close();
            }

            var menu = new TrayMenuWindow(
                _settings,
                _openSettings,
                _toggleCrosshair,
                _setVisibilityMode,
                _exitApplication);

            menu.Closed += (_, _) =>
            {
                if (ReferenceEquals(_menu, menu))
                {
                    _menu = null;
                }
            };

            _menu = menu;
            menu.ShowAtCursor();
        }
        catch (Exception ex)
        {
            // A failing context menu must never terminate the whole application.
            AppLog.Write("Could not open the tray menu", ex);
            _menu = null;
        }
    }

    private void OnSettingsChanged(object? sender, PropertyChangedEventArgs e) => UpdateState();

    private void UpdateState()
    {
        var mode = _settings.VisibilityMode == OverlayVisibilityMode.FullscreenOnly
            ? "полный экран"
            : "всегда";

        var state = _settings.Enabled ? "включён" : "выключен";

        _notifyIcon.Text = $"Crosshair Overlay - {state} - {mode} - {_settings.ToggleHotkey}/{_settings.SettingsHotkey}";
    }

    private static Icon LoadApplicationIcon()
    {
        try
        {
            var path = Environment.ProcessPath;
            if (!string.IsNullOrWhiteSpace(path))
            {
                var icon = Icon.ExtractAssociatedIcon(path);
                if (icon is not null)
                {
                    return icon;
                }
            }
        }
        catch
        {
            // Fall back to the generic icon below.
        }

        return (Icon)SystemIcons.Application.Clone();
    }
}
