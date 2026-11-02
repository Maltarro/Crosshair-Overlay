using System.ComponentModel;
using System.Runtime.CompilerServices;
using CrosshairOverlay.Utilities;

namespace CrosshairOverlay.Models;

public sealed class AppSettings : INotifyPropertyChanged
{
    private bool _enabled = true;
    private OverlayVisibilityMode _visibilityMode = OverlayVisibilityMode.Always;
    private string _monitorDeviceName = string.Empty;
    private CrosshairStyle _style = CrosshairStyle.Cross;
    private string _color = "#FFFFFF";
    private string _outlineColor = "#000000";
    private string _glowColor = "#C56CFF";
    private double _thickness = 2;
    private double _length = 12;
    private double _gap = 6;
    private double _dotSize = 4;
    private double _circleRadius = 20;
    private double _outlineThickness = 1;
    private double _opacity = 0.9;
    private double _scale = 1.0;
    private double _glowStrength;
    private double _rotation;
    private bool _useOutline = true;
    private bool _glowUsesMainColor = true;
    private bool _startHidden;
    private bool _startWithWindows;
    private string _toggleHotkey = "F8";
    private string _settingsHotkey = "F9";
    private double _offsetX;
    private double _offsetY;

    public bool Enabled
    {
        get => _enabled;
        set => Set(ref _enabled, value);
    }

    /// <summary>
    /// When <see cref="OverlayVisibilityMode.FullscreenOnly"/>, the crosshair is shown only
    /// while a fullscreen application is in the foreground.
    /// </summary>
    public OverlayVisibilityMode VisibilityMode
    {
        get => _visibilityMode;
        set => Set(ref _visibilityMode, value);
    }

    /// <summary>Device name of the monitor whose center hosts the crosshair. Empty means "auto".</summary>
    public string MonitorDeviceName
    {
        get => _monitorDeviceName;
        set => Set(ref _monitorDeviceName, value);
    }

    public CrosshairStyle Style
    {
        get => _style;
        set => Set(ref _style, value);
    }

    public string Color
    {
        get => _color;
        set => Set(ref _color, value);
    }

    public string OutlineColor
    {
        get => _outlineColor;
        set => Set(ref _outlineColor, value);
    }

    /// <summary>Colour of the neon glow when <see cref="GlowUsesMainColor"/> is disabled.</summary>
    public string GlowColor
    {
        get => _glowColor;
        set => Set(ref _glowColor, value);
    }

    public double Thickness
    {
        get => _thickness;
        set => Set(ref _thickness, value);
    }

    public double Length
    {
        get => _length;
        set => Set(ref _length, value);
    }

    public double Gap
    {
        get => _gap;
        set => Set(ref _gap, value);
    }

    public double DotSize
    {
        get => _dotSize;
        set => Set(ref _dotSize, value);
    }

    public double CircleRadius
    {
        get => _circleRadius;
        set => Set(ref _circleRadius, value);
    }

    public double OutlineThickness
    {
        get => _outlineThickness;
        set => Set(ref _outlineThickness, value);
    }

    public double Opacity
    {
        get => _opacity;
        set => Set(ref _opacity, value);
    }

    /// <summary>Uniform crosshair scale, where 1.0 is 100%.</summary>
    public double Scale
    {
        get => _scale;
        set => Set(ref _scale, value);
    }

    /// <summary>Blur radius of the neon glow around the crosshair. Zero disables the glow.</summary>
    public double GlowStrength
    {
        get => _glowStrength;
        set => Set(ref _glowStrength, value);
    }

    /// <summary>Whole-crosshair rotation in degrees.</summary>
    public double Rotation
    {
        get => _rotation;
        set => Set(ref _rotation, value);
    }

    /// <summary>Turns the contrasting outline around the crosshair on or off.</summary>
    public bool UseOutline
    {
        get => _useOutline;
        set => Set(ref _useOutline, value);
    }

    /// <summary>When true, the glow repeats <see cref="Color"/> instead of <see cref="GlowColor"/>.</summary>
    public bool GlowUsesMainColor
    {
        get => _glowUsesMainColor;
        set => Set(ref _glowUsesMainColor, value);
    }

    /// <summary>Starts the application with the settings window hidden in the tray.</summary>
    public bool StartHidden
    {
        get => _startHidden;
        set => Set(ref _startHidden, value);
    }

    /// <summary>Registers the application in the current user's Run key.</summary>
    public bool StartWithWindows
    {
        get => _startWithWindows;
        set => Set(ref _startWithWindows, value);
    }

    /// <summary>Virtual-key name, for example F8, used to show or hide the crosshair.</summary>
    public string ToggleHotkey
    {
        get => _toggleHotkey;
        set => Set(ref _toggleHotkey, value);
    }

    /// <summary>Virtual-key name, for example F9, used to open the settings window.</summary>
    public string SettingsHotkey
    {
        get => _settingsHotkey;
        set => Set(ref _settingsHotkey, value);
    }

    public double OffsetX
    {
        get => _offsetX;
        set => Set(ref _offsetX, value);
    }

    public double OffsetY
    {
        get => _offsetY;
        set => Set(ref _offsetY, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public void Reset()
    {
        var defaults = new AppSettings();
        Enabled = defaults.Enabled;
        VisibilityMode = defaults.VisibilityMode;
        MonitorDeviceName = defaults.MonitorDeviceName;
        Style = defaults.Style;
        Color = defaults.Color;
        OutlineColor = defaults.OutlineColor;
        GlowColor = defaults.GlowColor;
        Thickness = defaults.Thickness;
        Length = defaults.Length;
        Gap = defaults.Gap;
        DotSize = defaults.DotSize;
        CircleRadius = defaults.CircleRadius;
        OutlineThickness = defaults.OutlineThickness;
        Opacity = defaults.Opacity;
        Scale = defaults.Scale;
        GlowStrength = defaults.GlowStrength;
        Rotation = defaults.Rotation;
        UseOutline = defaults.UseOutline;
        GlowUsesMainColor = defaults.GlowUsesMainColor;
        StartHidden = defaults.StartHidden;
        StartWithWindows = defaults.StartWithWindows;
        ToggleHotkey = defaults.ToggleHotkey;
        SettingsHotkey = defaults.SettingsHotkey;
        OffsetX = defaults.OffsetX;
        OffsetY = defaults.OffsetY;
    }

    /// <summary>Clamps values that could have been edited by hand in settings.json.</summary>
    public void Normalize()
    {
        Thickness = Math.Clamp(Thickness, 1, 10);
        Length = Math.Clamp(Length, 1, 60);
        Gap = Math.Clamp(Gap, 0, 40);
        DotSize = Math.Clamp(DotSize, 1, 20);
        CircleRadius = Math.Clamp(CircleRadius, 4, 80);
        OutlineThickness = Math.Clamp(OutlineThickness, 0, 5);
        Opacity = Math.Clamp(Opacity, 0.1, 1);
        Scale = Math.Clamp(Scale, 0.5, 2.0);
        GlowStrength = Math.Clamp(GlowStrength, 0, 20);
        Rotation = Math.Clamp(Rotation, 0, 360);
        OffsetX = Math.Clamp(OffsetX, -200, 200);
        OffsetY = Math.Clamp(OffsetY, -200, 200);

        if (!Enum.IsDefined(VisibilityMode))
        {
            VisibilityMode = OverlayVisibilityMode.Always;
        }

        if (!Enum.IsDefined(Style))
        {
            Style = CrosshairStyle.Cross;
        }

        MonitorDeviceName = MonitorDeviceName?.Trim() ?? string.Empty;
        Color = ColorUtil.TryParse(Color, out var color) ? ColorUtil.ToHex(color) : "#FFFFFF";
        OutlineColor = ColorUtil.TryParse(OutlineColor, out var outline) ? ColorUtil.ToHex(outline) : "#000000";
        GlowColor = ColorUtil.TryParse(GlowColor, out var glow) ? ColorUtil.ToHex(glow) : "#C56CFF";

        ToggleHotkey = "F8";
        SettingsHotkey = "F9";
    }

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}
