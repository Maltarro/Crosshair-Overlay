namespace CrosshairOverlay.Models;

/// <summary>Display item for the visibility-mode combo box.</summary>
public sealed class OverlayModeOption
{
    public OverlayModeOption(string name, OverlayVisibilityMode value)
    {
        Name = name;
        Value = value;
    }

    public string Name { get; }

    public OverlayVisibilityMode Value { get; }

    public override string ToString() => Name;
}
