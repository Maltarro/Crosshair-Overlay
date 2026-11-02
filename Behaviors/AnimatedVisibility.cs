using System.Windows;
using System.Windows.Media.Animation;

namespace CrosshairOverlay.Behaviors;

/// <summary>
/// Animates an element in and out of the layout instead of toggling Visibility
/// instantly. The element keeps its natural height while it is visible and
/// collapses to zero height when it is hidden, so surrounding rows glide too.
/// </summary>
public static class AnimatedVisibility
{
    private static readonly DependencyProperty InitializedProperty =
        DependencyProperty.RegisterAttached(
            "Initialized",
            typeof(bool),
            typeof(AnimatedVisibility),
            new PropertyMetadata(false));

    private static readonly DependencyProperty IsShownProperty =
        DependencyProperty.RegisterAttached(
            "IsShown",
            typeof(bool),
            typeof(AnimatedVisibility),
            new FrameworkPropertyMetadata(true, OnIsShownChanged));

    public static bool GetIsShown(DependencyObject element) => (bool)element.GetValue(IsShownProperty);

    public static void SetIsShown(DependencyObject element, bool value) => element.SetValue(IsShownProperty, value);

    private static void OnIsShownChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement element)
        {
            return;
        }

        var shown = (bool)e.NewValue;

        if (!(bool)element.GetValue(InitializedProperty))
        {
            // Before the first layout pass just reserve or release the space.
            // The real state is applied once, without animation, on Loaded.
            if (!element.IsLoaded)
            {
                ApplyInstant(element, shown);
                element.Loaded -= OnFirstLoaded;
                element.Loaded += OnFirstLoaded;
                return;
            }

            element.SetValue(InitializedProperty, true);
            ApplyInstant(element, shown);
            return;
        }

        if (!element.IsLoaded)
        {
            ApplyInstant(element, shown);
            return;
        }

        Animate(element, shown);
    }

    private static void OnFirstLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element)
        {
            return;
        }

        element.Loaded -= OnFirstLoaded;
        element.SetValue(InitializedProperty, true);
        ApplyInstant(element, GetIsShown(element));
    }

    private static void ApplyInstant(FrameworkElement element, bool shown)
    {
        element.BeginAnimation(FrameworkElement.MaxHeightProperty, null);
        element.BeginAnimation(UIElement.OpacityProperty, null);

        element.Visibility = shown ? Visibility.Visible : Visibility.Collapsed;
        element.MaxHeight = shown ? double.PositiveInfinity : 0;
        element.Opacity = 1;
    }

    private static void Animate(FrameworkElement element, bool show)
    {
        element.BeginAnimation(FrameworkElement.MaxHeightProperty, null);
        element.BeginAnimation(UIElement.OpacityProperty, null);

        if (show)
        {
            Show(element);
        }
        else
        {
            Hide(element);
        }
    }

    private static void Show(FrameworkElement element)
    {
        element.Visibility = Visibility.Visible;
        element.Opacity = 1;

        var width = GetMeasureWidth(element);
        var previousMaxHeight = element.MaxHeight;

        // Measure the natural height first. The element may have been collapsed,
        // in which case ActualHeight is zero and cannot be used as the target.
        element.MaxHeight = double.PositiveInfinity;
        element.Measure(new Size(width, double.PositiveInfinity));
        var targetHeight = element.DesiredSize.Height;
        element.MaxHeight = previousMaxHeight;

        if (double.IsNaN(targetHeight) || double.IsInfinity(targetHeight) || targetHeight < 1)
        {
            targetHeight = 24;
        }

        var startHeight = 0.0;
        if (element.ActualHeight > 0.5)
        {
            startHeight = Math.Min(element.ActualHeight, targetHeight);
        }

        element.MaxHeight = startHeight;
        element.Opacity = 0;
        element.UpdateLayout();

        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        ease.Freeze();

        var collapse = new DoubleAnimation(startHeight, targetHeight, TimeSpan.FromMilliseconds(220))
        {
            EasingFunction = ease,
            FillBehavior = FillBehavior.HoldEnd
        };
        collapse.Completed += (_, _) =>
        {
            element.BeginAnimation(FrameworkElement.MaxHeightProperty, null);
            element.MaxHeight = double.PositiveInfinity;

            element.BeginAnimation(UIElement.OpacityProperty, null);
            element.Opacity = 1;
        };

        element.BeginAnimation(FrameworkElement.MaxHeightProperty, collapse);
        element.BeginAnimation(
            UIElement.OpacityProperty,
            new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180))
            {
                EasingFunction = ease,
                FillBehavior = FillBehavior.HoldEnd
            });
    }

    private static void Hide(FrameworkElement element)
    {
        var startHeight = element.ActualHeight;

        if (startHeight < 0.5)
        {
            startHeight = element.MaxHeight is > 0 and < double.PositiveInfinity ? element.MaxHeight : 24;
        }

        element.MaxHeight = startHeight;
        element.Opacity = 1;

        var ease = new SineEase { EasingMode = EasingMode.EaseIn };
        ease.Freeze();

        var collapse = new DoubleAnimation(startHeight, 0, TimeSpan.FromMilliseconds(200))
        {
            EasingFunction = ease,
            FillBehavior = FillBehavior.HoldEnd
        };
        collapse.Completed += (_, _) =>
        {
            element.BeginAnimation(FrameworkElement.MaxHeightProperty, null);
            element.BeginAnimation(UIElement.OpacityProperty, null);

            element.Visibility = Visibility.Collapsed;
            element.MaxHeight = double.PositiveInfinity;
            element.Opacity = 1;
        };

        element.BeginAnimation(FrameworkElement.MaxHeightProperty, collapse);
        element.BeginAnimation(
            UIElement.OpacityProperty,
            new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(170))
            {
                EasingFunction = ease,
                FillBehavior = FillBehavior.HoldEnd
            });
    }

    private static double GetMeasureWidth(FrameworkElement element)
    {
        if (element.ActualWidth > 0)
        {
            return element.ActualWidth;
        }

        if (element.Parent is FrameworkElement parent && parent.ActualWidth > 0)
        {
            return parent.ActualWidth;
        }

        return double.PositiveInfinity;
    }
}
