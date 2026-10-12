namespace AlienFXControlCenter.Models;

public sealed class FanReading
{
    public int Id { get; init; }
    public string Type { get; set; } = string.Empty;
    public int Rpm { get; set; }
    public int MaxRpm { get; set; }
    public int Percent { get; set; }
    public int Boost { get; set; }

    public string DisplayName => string.IsNullOrEmpty(Type) ? $"Fan {Id}" : $"Fan {Id} ({Type})";

    public string RpmText => MaxRpm > 0 ? $"{Rpm} / {MaxRpm} RPM" : $"{Rpm} RPM";
}

public sealed class SensorReading
{
    public int Id { get; init; }
    public string Name { get; set; } = string.Empty;
    public int Temperature { get; set; }

    public string DisplayName => $"{Name}: {Temperature} °C";
}

public sealed class FanSnapshot
{
    public int SystemId { get; set; }
    public int FanCount { get; set; }
    public int SensorCount { get; set; }
    public int PowerCount { get; set; }
    public bool IsGmode { get; set; }
    public bool IsTcc { get; set; }
    public bool IsXmp { get; set; }
    public bool HasLights { get; set; }
    public List<FanReading> Fans { get; } = new();
    public List<SensorReading> Sensors { get; } = new();
    public string Raw { get; set; } = string.Empty;
}
