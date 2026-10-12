namespace AlienFXControlCenter.Models;

public sealed class DeviceOption
{
    public required string Label { get; init; }
    public LightDevice? Device { get; init; }

    public bool IsWholeSystem => Device is null;

    public override string ToString() => Label;
}

public enum LightScope
{
    All,
    Wasd,
    Arrows,
    FunctionKeys,
}
