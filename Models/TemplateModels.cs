namespace AlienFXControlCenter.Models;

public sealed class TemplateLightColor
{
    public int VendorId { get; set; }
    public int ProductId { get; set; }
    public int LightId { get; set; }
    public string? LightName { get; set; }
    public byte R { get; set; }
    public byte G { get; set; }
    public byte B { get; set; }

    public string Hex => $"#{R:X2}{G:X2}{B:X2}";
}

public sealed class TemplateFanSetting
{
    public int FanId { get; set; }
    public string? Name { get; set; }
    public int Percent { get; set; }

    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? $"Fan {FanId}" : Name;
}

public sealed class LightingTemplate
{
    // Left empty on purpose: templates saved before ids were persisted have no
    // "id" field, and a Guid.NewGuid() initializer would mask that (making the
    // Guid.Empty migration in TemplateStore.LoadFile a no-op). TemplateStore.Save
    // and LoadFile assign a stable id and persist it.
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTimeOffset UpdatedUtc { get; set; } = DateTimeOffset.UtcNow;

    public string? WholeSystemColor { get; set; }
    public List<TemplateLightColor> Lights { get; set; } = new();
    public List<TemplateFanSetting> Fans { get; set; } = new();

    public bool HasLights => WholeSystemColor is not null || Lights.Count > 0;

    public bool HasFans => Fans.Count > 0;

    public override string ToString() => Name;
}
