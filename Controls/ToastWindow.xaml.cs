using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using CrosshairOverlay.Interop;
using CrosshairOverlay.Models;

namespace CrosshairOverlay.Controls;

public partial class ToastWindow : Window
{
    private static readonly Color InfoAccent = Color.FromRgb(0xC5, 0x6C, 0xFF);
    private static readonly Color SuccessAccent = Color.FromRgb(0x6E, 0xE7, 0xA8);
    private static readonly Color WarningAccent = Color.FromRgb(0xFF, 0xB8, 0x6B);

    private readonly TimeSpan _lifetime;
    private readonly Stopwatch _clock = new();
    private readonly DispatcherTimer _ticker;
    private bool _started;
    private bool _dismissing;

    public ToastWindow(string title, string message, NotificationKind kind, TimeSpan lifetime)
    {
        InitializeComponent();

        _lifetime = lifetime;
        TitleText.Text = title;
        MessageText.Text = message;
        MessageText.Visibility = string.IsNullOrWhiteSpace(message) ? Visibility.Collapsed : Visibility.Visible;

        ApplyKind(kind);

        // A plain dispatcher timer drives the progress bar and the lifetime.
        // It is independent from WPF storyboards, so the toast always closes
        // exactly when the bar runs out, even if an animation cannot run.
        _ticker = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(16)
        };
        _ticker.Tick += OnTick;

        Loaded += OnLoaded;
        CloseButton.Click += OnCloseClick;
    }

    private void ApplyKind(NotificationKind kind)
    {
        var accent = kind switch
        {
            NotificationKind.Success => SuccessAccent,
            NotificationKind.Warning => WarningAccent,
            _ => InfoAccent
        };

        IconText.Text = kind switch
        {
            NotificationKind.Success => "✓",
            NotificationKind.Warning => "!",
            _ => "i"
        };

        IconText.Foreground = new SolidColorBrush(accent);
        IconBadge.Background = new SolidColorBrush(Color.FromArgb(0x33, accent.R, accent.G, accent.B));
        IconBadge.BorderBrush = new SolidColorBrush(Color.FromArgb(0x66, accent.R, accent.G, accent.B));

        var light = Color.FromArgb(
            0xFF,
            (byte)Math.Min(255, accent.R + 40),
            (byte)Math.Min(255, accent.G + 40),
            (byte)Math.Min(255, accent.B + 40));

        ProgressFill.Background = new LinearGradientBrush(accent, light, 0);
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
        style |= NativeMethods.WS_EX_TOOLWINDOW | NativeMethods.WS_EX_NOACTIVATE;
        NativeMethods.SetWindowLongW(handle, NativeMethods.GWL_EXSTYLE, style);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_started)
        {
            return;
        }

        _started = true;

        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        ease.Freeze();

        // A short, light entrance: only opacity and a small slide are animated.
        // Heavy effects (blur, animated shadows) made the toast stutter.
        BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180))
        {
            EasingFunction = ease,
            FillBehavior = FillBehavior.HoldEnd
        });

        Slide.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(18, 0, TimeSpan.FromMilliseconds(220))
        {
            EasingFunction = ease,
            FillBehavior = FillBehavior.HoldEnd
        });

        ProgressFillScale.ScaleX = 1;
        _clock.Restart();
        _ticker.Start();
    }

    private void OnTick(object? sender, EventArgs e)
    {
        var progress = 1 - _clock.Elapsed.TotalMilliseconds / _lifetime.TotalMilliseconds;

        if (progress <= 0)
        {
            ProgressFillScale.ScaleX = 0;
            Dismiss();
            return;
        }

        ProgressFillScale.ScaleX = progress;
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        Dismiss();
    }

    public void Dismiss()
    {
        if (_dismissing)
        {
            return;
        }

        _dismissing = true;
        _ticker.Stop();
        _clock.Stop();

        if (!_started)
        {
            Close();
            return;
        }

        var ease = new SineEase { EasingMode = EasingMode.EaseIn };
        ease.Freeze();

        // A longer fade with a downward slide makes the dismissal clearly visible
        // instead of looking like the window simply vanishes.
        var fade = new DoubleAnimation(Opacity, 0, TimeSpan.FromMilliseconds(260))
        {
            EasingFunction = ease,
            FillBehavior = FillBehavior.HoldEnd
        };
        fade.Completed += (_, _) => Close();

        BeginAnimation(OpacityProperty, fade);
        Slide.BeginAnimation(
            TranslateTransform.YProperty,
            new DoubleAnimation(Slide.Y, 26, TimeSpan.FromMilliseconds(260))
            {
                EasingFunction = ease
            });
    }
}
