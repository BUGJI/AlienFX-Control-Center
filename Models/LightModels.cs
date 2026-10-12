namespace AlienFXControlCenter.Models;

public sealed class Light
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
}

public sealed class LightDevice
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public int VendorId { get; init; }
    public int ProductId { get; init; }
    public int ApiVersion { get; init; }
    public int LightCount { get; init; }
    public List<Light> Lights { get; } = new();

    public string DisplayName => $"#{Id} {Name}";
}
