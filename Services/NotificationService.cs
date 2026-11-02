using System.Windows;
using System.Windows.Media.Animation;
using CrosshairOverlay.Controls;
using CrosshairOverlay.Models;

namespace CrosshairOverlay.Services;

/// <summary>
/// Bottom-right toast notifications. They are deliberately independent from the
/// crosshair settings so that feedback is always visible, even when the crosshair is hidden.
/// </summary>
public sealed class NotificationService : IDisposable
{
    private const double ScreenMargin = 18;
    private const double ToastGap = 10;
    private const int MaxToasts = 4;

    private readonly List<ToastWindow> _toasts = new();
    private bool _disposed;

    public void Show(
        string title,
        string message,
        NotificationKind kind = NotificationKind.Info,
        TimeSpan? lifetime = null)
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            while (_toasts.Count >= MaxToasts)
            {
                var oldest = _toasts[0];

                // Let the oldest toast fade out instead of closing it abruptly.
                _toasts.RemoveAt(0);
                oldest.Dismiss();
            }

            var toast = new ToastWindow(title, message, kind, lifetime ?? TimeSpan.FromSeconds(5));
            toast.Closed += (_, _) =>
            {
                _toasts.Remove(toast);
                Reposition();
            };

            _toasts.Add(toast);
            Reposition(toast);
            toast.Show();
        }
        catch (Exception ex)
        {
            AppLog.Write("Could not show a notification", ex);
        }
    }

    private void Reposition(ToastWindow? immediate = null)
    {
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

        var top = bottomBound - ScreenMargin;

        for (var index = _toasts.Count - 1; index >= 0; index--)
        {
            var toast = _toasts[index];
            top -= toast.Height;

            toast.Left = Math.Round(rightBound - ScreenMargin - toast.Width);
            MoveTo(toast, Math.Round(Math.Max(topBound + 8, top)), ReferenceEquals(toast, immediate));

            top -= ToastGap;
        }
    }

    private static void MoveTo(ToastWindow toast, double targetTop, bool immediate)
    {
        var currentTop = toast.Top;

        // Clear a running move so that the base value can be updated.
        toast.BeginAnimation(Window.TopProperty, null);

        if (immediate || !toast.IsVisible || Math.Abs(currentTop - targetTop) < 0.5)
        {
            toast.Top = targetTop;
            return;
        }

        var ease = new SineEase { EasingMode = EasingMode.EaseInOut };
        ease.Freeze();

        toast.Top = targetTop;

        // Existing toasts glide to their new slot instead of jumping when
        // another notification appears or disappears.
        toast.BeginAnimation(
            Window.TopProperty,
            new DoubleAnimation(currentTop, targetTop, TimeSpan.FromMilliseconds(300))
            {
                EasingFunction = ease,
                FillBehavior = FillBehavior.Stop
            });
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        foreach (var toast in _toasts.ToArray())
        {
            toast.Close();
        }

        _toasts.Clear();
    }
}
