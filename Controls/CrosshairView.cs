using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;
using CrosshairOverlay.Models;
using CrosshairOverlay.Utilities;

namespace CrosshairOverlay.Controls;

/// <summary>
/// Draws the crosshair around the centre of its available area with pixel-aligned geometry.
/// </summary>
public sealed class CrosshairView : FrameworkElement
{
    private AppSettings? _settings;

    public CrosshairView()
    {
        Focusable = false;
        IsHitTestVisible = false;
    }

    public AppSettings? Settings
    {
        get => _settings;
        set
        {
            if (ReferenceEquals(_settings, value))
            {
                return;
            }

            if (_settings is not null)
            {
                _settings.PropertyChanged -= OnSettingsChanged;
            }

            _settings = value;

            if (_settings is not null)
            {
                _settings.PropertyChanged += OnSettingsChanged;
            }

            UpdateGlow();
            InvalidateVisual();
        }
    }

    /// <summary>Keeps the crosshair on screen even when it is currently disabled.</summary>
    public bool ForceVisible { get; set; }

    /// <summary>
    /// Preview mode keeps the offset readable and shrinks oversized crosshairs so that the
    /// whole shape always fits inside the preview box.
    /// </summary>
    public bool PreviewMode { get; set; }

    /// <summary>Set to false for the settings-window preview so that offsets do not push it out of the box.</summary>
    public bool ApplyOffset { get; set; } = true;

    private void OnSettingsChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(AppSettings.GlowStrength)
            or nameof(AppSettings.Color)
            or nameof(AppSettings.Opacity)
            or nameof(AppSettings.GlowColor)
            or nameof(AppSettings.GlowUsesMainColor)
            or null)
        {
            UpdateGlow();
        }

        InvalidateVisual();
    }

    private void UpdateGlow()
    {
        if (_settings is not { GlowStrength: > 0 } settings)
        {
            Effect = null;
            return;
        }

        var glowHex = settings.GlowUsesMainColor ? settings.Color : settings.GlowColor;
        if (!ColorUtil.TryParse(glowHex, out var color))
        {
            Effect = null;
            return;
        }

        var glow = new DropShadowEffect
        {
            Color = Color.FromRgb(color.R, color.G, color.B),
            BlurRadius = Math.Clamp(settings.GlowStrength * 2.0, 1, 60),
            ShadowDepth = 0,
            Direction = 0,
            Opacity = Math.Clamp(settings.Opacity, 0.1, 1.0) * 0.9,
            RenderingBias = RenderingBias.Performance
        };
        glow.Freeze();
        Effect = glow;
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        if (_settings is not { } settings || ActualWidth <= 0 || ActualHeight <= 0)
        {
            return;
        }

        if (!settings.Enabled && !ForceVisible)
        {
            return;
        }

        var dpi = VisualTreeHelper.GetDpi(this);
        var scaleX = dpi.DpiScaleX;
        var scaleY = dpi.DpiScaleY;

        var previewHalf = Math.Min(ActualWidth, ActualHeight) / 2.0;
        var extent = GetExtent(settings);
        var scale = Math.Clamp(settings.Scale, 0.5, 2.0);

        if (PreviewMode)
        {
            var available = previewHalf - 16.0;
            if (extent > 0.01 && available > 0)
            {
                var scaledExtent = extent * scale;
                if (scaledExtent > available)
                {
                    scale *= available / scaledExtent;
                }
            }
        }

        var centerX = ActualWidth / 2.0;
        var centerY = ActualHeight / 2.0;

        if (ApplyOffset)
        {
            centerX += settings.OffsetX;
            centerY += settings.OffsetY;
        }
        else if (PreviewMode)
        {
            // Show the offset direction, but keep the whole shape inside the preview.
            var limit = Math.Max(0, previewHalf - extent * scale - 8.0);
            centerX += Math.Clamp(settings.OffsetX, -limit, limit);
            centerY += Math.Clamp(settings.OffsetY, -limit, limit);
        }

        centerX = Snap(centerX, scaleX);
        centerY = Snap(centerY, scaleY);

        var alpha = (byte)Math.Round(Math.Clamp(settings.Opacity, 0.05, 1.0) * 255);
        var mainColor = ColorUtil.Parse(settings.Color, Colors.White);
        mainColor.A = alpha;
        var outlineColor = ColorUtil.Parse(settings.OutlineColor, Colors.Black);
        outlineColor.A = alpha;

        var mainBrush = CreateBrush(mainColor);
        var outlineBrush = CreateBrush(outlineColor);

        var hasOutline = settings.UseOutline && settings.OutlineThickness > 0;
        var outline = hasOutline ? SnapThickness(settings.OutlineThickness, Math.Max(scaleX, scaleY)) : 0;

        var rotation = settings.Rotation + (settings.Style == CrosshairStyle.XShape ? 45 : 0);
        var isRotated = Math.Abs(rotation % 360) > 0.01;
        var isScaled = Math.Abs(scale - 1.0) > 0.001;

        if (isRotated)
        {
            dc.PushTransform(new RotateTransform(rotation, centerX, centerY));
        }

        if (isScaled)
        {
            dc.PushTransform(new ScaleTransform(scale, scale, centerX, centerY));
        }

        switch (settings.Style)
        {
            case CrosshairStyle.Cross:
                DrawCross(dc, settings, mainBrush, outlineBrush, hasOutline, outline, Arms.All, centerX, centerY, scaleX, scaleY);
                break;

            case CrosshairStyle.TShape:
                DrawCross(dc, settings, mainBrush, outlineBrush, hasOutline, outline, Arms.Left | Arms.Right | Arms.Bottom, centerX, centerY, scaleX, scaleY);
                break;

            case CrosshairStyle.XShape:
                DrawCross(dc, settings, mainBrush, outlineBrush, hasOutline, outline, Arms.All, centerX, centerY, scaleX, scaleY);
                break;

            case CrosshairStyle.CrossDot:
                DrawCross(dc, settings, mainBrush, outlineBrush, hasOutline, outline, Arms.All, centerX, centerY, scaleX, scaleY);
                DrawDot(dc, settings, mainBrush, outlineBrush, hasOutline, outline, centerX, centerY);
                break;

            case CrosshairStyle.Dot:
                DrawDot(dc, settings, mainBrush, outlineBrush, hasOutline, outline, centerX, centerY);
                break;

            case CrosshairStyle.Circle:
                DrawCircle(dc, settings, mainBrush, outlineBrush, hasOutline, outline, centerX, centerY, scaleX, scaleY);
                break;

            case CrosshairStyle.CircleDot:
                DrawCircle(dc, settings, mainBrush, outlineBrush, hasOutline, outline, centerX, centerY, scaleX, scaleY);
                DrawDot(dc, settings, mainBrush, outlineBrush, hasOutline, outline, centerX, centerY);
                break;

            case CrosshairStyle.Brackets:
                DrawBrackets(dc, settings, mainBrush, outlineBrush, hasOutline, outline, centerX, centerY, scaleX, scaleY);
                break;
        }

        if (isScaled)
        {
            dc.Pop();
        }

        if (isRotated)
        {
            dc.Pop();
        }
    }

    /// <summary>Half-size of the drawn crosshair in device-independent pixels, used by the preview auto-fit.</summary>
    private static double GetExtent(AppSettings settings)
    {
        var extent = 0.0;

        if (settings.Style is CrosshairStyle.Cross
            or CrosshairStyle.CrossDot
            or CrosshairStyle.TShape
            or CrosshairStyle.XShape)
        {
            extent = Math.Max(extent, settings.Gap / 2.0 + settings.Length + settings.Thickness / 2.0);
        }

        if (settings.Style is CrosshairStyle.Circle or CrosshairStyle.CircleDot or CrosshairStyle.Brackets)
        {
            extent = Math.Max(extent, settings.CircleRadius + settings.Thickness / 2.0);
        }

        if (settings.Style is CrosshairStyle.Dot or CrosshairStyle.CrossDot or CrosshairStyle.CircleDot)
        {
            extent = Math.Max(extent, settings.DotSize / 2.0);
        }

        if (settings.UseOutline)
        {
            extent += settings.OutlineThickness;
        }

        return extent;
    }

    [Flags]
    private enum Arms
    {
        None = 0,
        Left = 1,
        Right = 2,
        Top = 4,
        Bottom = 8,
        All = Left | Right | Top | Bottom
    }

    private static void DrawCross(
        DrawingContext dc,
        AppSettings settings,
        Brush mainBrush,
        Brush outlineBrush,
        bool hasOutline,
        double outline,
        Arms arms,
        double centerX,
        double centerY,
        double scaleX,
        double scaleY)
    {
        var thicknessX = SnapThickness(settings.Thickness, scaleX);
        var thicknessY = SnapThickness(settings.Thickness, scaleY);
        var length = Math.Max(0, settings.Length);
        var gap = Math.Max(0, settings.Gap);

        if (hasOutline)
        {
            var outerGap = Math.Max(0, gap - 2 * outline);
            DrawCrossGeometry(
                dc,
                outlineBrush,
                arms,
                centerX,
                centerY,
                length + outline,
                outerGap,
                thicknessX + 2 * outline,
                thicknessY + 2 * outline,
                scaleX,
                scaleY);
        }

        DrawCrossGeometry(
            dc,
            mainBrush,
            arms,
            centerX,
            centerY,
            length,
            gap,
            thicknessX,
            thicknessY,
            scaleX,
            scaleY);
    }

    private static void DrawCrossGeometry(
        DrawingContext dc,
        Brush brush,
        Arms arms,
        double centerX,
        double centerY,
        double length,
        double gap,
        double thicknessX,
        double thicknessY,
        double scaleX,
        double scaleY)
    {
        var minX = 1.0 / scaleX;
        var minY = 1.0 / scaleY;
        var halfGap = gap / 2.0;

        var gapLeft = Snap(centerX - halfGap, scaleX);
        var gapRight = Snap(centerX + halfGap, scaleX);
        var gapTop = Snap(centerY - halfGap, scaleY);
        var gapBottom = Snap(centerY + halfGap, scaleY);

        // Horizontal arms.
        if ((arms & (Arms.Left | Arms.Right)) != 0)
        {
            var armTop = Snap(centerY - thicknessY / 2.0, scaleY);
            var armBottom = Snap(centerY + thicknessY / 2.0, scaleY);
            if (armBottom - armTop < minY)
            {
                armBottom = armTop + minY;
            }

            var armHeight = armBottom - armTop;

            if ((arms & Arms.Left) != 0)
            {
                var leftOuter = Snap(centerX - halfGap - length, scaleX);
                var leftWidth = gapLeft - leftOuter;
                if (leftWidth >= minX)
                {
                    dc.DrawRectangle(brush, null, new Rect(leftOuter, armTop, leftWidth, armHeight));
                }
            }

            if ((arms & Arms.Right) != 0)
            {
                var rightOuter = Snap(centerX + halfGap + length, scaleX);
                var rightWidth = rightOuter - gapRight;
                if (rightWidth >= minX)
                {
                    dc.DrawRectangle(brush, null, new Rect(gapRight, armTop, rightWidth, armHeight));
                }
            }
        }

        // Vertical arms.
        if ((arms & (Arms.Top | Arms.Bottom)) != 0)
        {
            var armLeft = Snap(centerX - thicknessX / 2.0, scaleX);
            var armRight = Snap(centerX + thicknessX / 2.0, scaleX);
            if (armRight - armLeft < minX)
            {
                armRight = armLeft + minX;
            }

            var armWidth = armRight - armLeft;

            if ((arms & Arms.Top) != 0)
            {
                var topOuter = Snap(centerY - halfGap - length, scaleY);
                var topHeight = gapTop - topOuter;
                if (topHeight >= minY)
                {
                    dc.DrawRectangle(brush, null, new Rect(armLeft, topOuter, armWidth, topHeight));
                }
            }

            if ((arms & Arms.Bottom) != 0)
            {
                var bottomOuter = Snap(centerY + halfGap + length, scaleY);
                var bottomHeight = bottomOuter - gapBottom;
                if (bottomHeight >= minY)
                {
                    dc.DrawRectangle(brush, null, new Rect(armLeft, gapBottom, armWidth, bottomHeight));
                }
            }
        }
    }

    private static void DrawDot(
        DrawingContext dc,
        AppSettings settings,
        Brush mainBrush,
        Brush outlineBrush,
        bool hasOutline,
        double outline,
        double centerX,
        double centerY)
    {
        var radius = Math.Max(0.5, settings.DotSize / 2.0);

        if (hasOutline && outline > 0)
        {
            var outlineRadius = radius + outline;
            dc.DrawEllipse(outlineBrush, null, new Point(centerX, centerY), outlineRadius, outlineRadius);
        }

        dc.DrawEllipse(mainBrush, null, new Point(centerX, centerY), radius, radius);
    }

    private static void DrawCircle(
        DrawingContext dc,
        AppSettings settings,
        Brush mainBrush,
        Brush outlineBrush,
        bool hasOutline,
        double outline,
        double centerX,
        double centerY,
        double scaleX,
        double scaleY)
    {
        var radius = Math.Max(1, settings.CircleRadius);
        var ringThickness = SnapThickness(settings.Thickness, Math.Max(scaleX, scaleY));
        var center = new Point(centerX, centerY);

        if (hasOutline && outline > 0)
        {
            dc.DrawEllipse(
                null,
                CreatePen(outlineBrush, ringThickness + 2 * outline),
                center,
                radius,
                radius);
        }

        dc.DrawEllipse(null, CreatePen(mainBrush, ringThickness), center, radius, radius);
    }

    private static void DrawBrackets(
        DrawingContext dc,
        AppSettings settings,
        Brush mainBrush,
        Brush outlineBrush,
        bool hasOutline,
        double outline,
        double centerX,
        double centerY,
        double scaleX,
        double scaleY)
    {
        var radius = Math.Max(4, settings.CircleRadius);
        var thickness = SnapThickness(settings.Thickness, Math.Max(scaleX, scaleY));

        var left = Snap(centerX - radius, scaleX);
        var right = Snap(centerX + radius, scaleX);
        var top = Snap(centerY - radius, scaleY);
        var bottom = Snap(centerY + radius, scaleY);

        var maxArmX = Math.Max(2, (right - left) / 2.0);
        var maxArmY = Math.Max(2, (bottom - top) / 2.0);
        var armX = Snap(Math.Min(Math.Max(2, settings.Length), maxArmX), scaleX);
        var armY = Snap(Math.Min(Math.Max(2, settings.Length), maxArmY), scaleY);

        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            // Top-left corner.
            context.BeginFigure(new Point(left, top + armY), false, false);
            context.LineTo(new Point(left, top), true, false);
            context.LineTo(new Point(left + armX, top), true, false);

            // Top-right corner.
            context.BeginFigure(new Point(right - armX, top), false, false);
            context.LineTo(new Point(right, top), true, false);
            context.LineTo(new Point(right, top + armY), true, false);

            // Bottom-right corner.
            context.BeginFigure(new Point(right, bottom - armY), false, false);
            context.LineTo(new Point(right, bottom), true, false);
            context.LineTo(new Point(right - armX, bottom), true, false);

            // Bottom-left corner.
            context.BeginFigure(new Point(left + armX, bottom), false, false);
            context.LineTo(new Point(left, bottom), true, false);
            context.LineTo(new Point(left, bottom - armY), true, false);
        }

        geometry.Freeze();

        if (hasOutline && outline > 0)
        {
            dc.DrawGeometry(null, CreatePen(outlineBrush, thickness + 2 * outline), geometry);
        }

        dc.DrawGeometry(null, CreatePen(mainBrush, thickness), geometry);
    }

    private static Pen CreatePen(Brush brush, double thickness)
    {
        var pen = new Pen(brush, thickness)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round
        };
        pen.Freeze();
        return pen;
    }

    private static Brush CreateBrush(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private static double Snap(double value, double scale) => Math.Round(value * scale) / scale;

    private static double SnapThickness(double value, double scale) =>
        Math.Max(1.0 / scale, Math.Round(value * scale) / scale);
}
