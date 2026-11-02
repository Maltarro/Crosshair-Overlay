namespace CrosshairOverlay.Models;

/// <summary>Controls when the crosshair overlay is allowed on screen.</summary>
public enum OverlayVisibilityMode
{
    /// <summary>The crosshair is shown above every application.</summary>
    Always,

    /// <summary>The crosshair is shown only while a fullscreen application owns the screen.</summary>
    FullscreenOnly
}
