using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using AlienFXControlCenter.Models;
using Windows.UI;

namespace AlienFXControlCenter.Services;

/// <summary>
/// Session-scoped record of the most recently applied lighting and fan settings.
/// The Lights and Fans pages push into this as the user works, and the Templates
/// page snapshots it to build a new <see cref="LightingTemplate"/>. The latest
/// snapshot is persisted so it can be re-applied on the next launch when the
/// "Re-apply last color on startup" setting is enabled.
/// </summary>
public static class PresetState
{
    private sealed record LightKey(int VendorId, int ProductId, int LightId);

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static readonly Dictionary<LightKey, (string? Name, byte R, byte G, byte B)> Lights = new();
    private static readonly Dictionary<int, (string? Name, int Percent)> Fans = new();

    private static Color? _wholeSystem;

    public static string FilePath { get; } = Path.Combine(SettingsService.Current.Folder, "lastState.json");

    public static bool IsEmpty =>
        _wholeSystem is null && Lights.Count == 0 && Fans.Count == 0;

    public static void RecordWholeSystem(Color color)
    {
        _wholeSystem = color;
        Persist();
    }

    public static void RecordLights(int vendorId, int productId, IEnumerable<Light> lights, Color color)
    {
        foreach (var light in lights)
        {
            Lights[new LightKey(vendorId, productId, light.Id)] =
                (light.Name, color.R, color.G, color.B);
        }

        Persist();
    }

    public static void RecordFans(IEnumerable<(int Id, string? Name, int Percent)> fans)
    {
        foreach (var (id, name, percent) in fans)
            Fans[id] = (name, percent);

        Persist();
    }

    /// <summary>
    /// Reads the most recently persisted preset from disk. Returns null when no
    /// preset has been stored yet.
    /// </summary>
    public static LightingTemplate? LoadLastApplied()
    {
        try
        {
            if (!File.Exists(FilePath))
                return null;

            return JsonSerializer.Deserialize<LightingTemplate>(File.ReadAllText(FilePath), Options);
        }
        catch (Exception ex)
        {
            Log.Write($"PresetState load failed: {ex.Message}");
            return null;
        }
    }

    private static void Persist()
    {
        try
        {
            Directory.CreateDirectory(SettingsService.Current.Folder);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(Snapshot("last applied"), Options));
        }
        catch (Exception ex)
        {
            Log.Write($"PresetState save failed: {ex.Message}");
        }
    }

    public static void Clear()
    {
        _wholeSystem = null;
        Lights.Clear();
        Fans.Clear();
    }

    public static LightingTemplate Snapshot(string name)
    {
        var template = new LightingTemplate
        {
            Name = name,
            WholeSystemColor = _wholeSystem is { } whole ? $"#{whole.R:X2}{whole.G:X2}{whole.B:X2}" : null,
        };

        foreach (var (key, value) in Lights)
        {
            template.Lights.Add(new TemplateLightColor
            {
                VendorId = key.VendorId,
                ProductId = key.ProductId,
                LightId = key.LightId,
                LightName = value.Name,
                R = value.R,
                G = value.G,
                B = value.B,
            });
        }

        foreach (var (id, value) in Fans)
        {
            template.Fans.Add(new TemplateFanSetting
            {
                FanId = id,
                Name = value.Name,
                Percent = value.Percent,
            });
        }

        return template;
    }
}
