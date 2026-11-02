namespace CrosshairOverlay.Models;

public sealed class StyleOption
{
    public StyleOption(string name, CrosshairStyle value)
    {
        Name = name;
        Value = value;
    }

    public string Name { get; }

    public CrosshairStyle Value { get; }

    public override string ToString() => Name;
}
