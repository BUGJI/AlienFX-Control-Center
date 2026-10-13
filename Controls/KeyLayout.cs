using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using AlienFXControlCenter.Models;
using AlienFXControlCenter.Services;

namespace AlienFXControlCenter.Controls;

public sealed class KeyLayout
{
    public string Device { get; set; } = string.Empty;
    public int Vid { get; set; }
    public int Pid { get; set; }
    public int Columns { get; set; }
    public int Rows { get; set; }
    public List<KeyPlacement> Keys { get; set; } = new();
}

public static class KeyLayoutStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static string Folder { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AlienFX Control Center",
        "KeyLayouts");

    public static string PathFor(int vid, int pid) => Path.Combine(Folder, $"{vid}-{pid}.json");

    public static KeyLayout Load(LightDevice device)
    {
        var path = PathFor(device.VendorId, device.ProductId);
        if (File.Exists(path))
        {
            try
            {
                var layout = JsonSerializer.Deserialize<KeyLayout>(File.ReadAllText(path), Options);
                if (layout is { Keys.Count: > 0 })
                    return Normalize(layout, device);
            }
            catch (Exception)
            {
                // Corrupt override file - fall through to the profile or built-in default.
            }
        }

        var profile = ProfileStore.Match(new[]
        {
            new DeviceIdentity(device.VendorId, device.ProductId, device.Name),
        });

        if (profile is not null)
        {
            var fromProfile = ProfileStore.BuildLayout(profile, device);
            if (fromProfile.Keys.Count > 0)
                return fromProfile;
        }

        return BuildDefault(device);
    }

    public static KeyLayout BuildDefault(LightDevice device) => Normalize(new KeyLayout
    {
        Keys = KeyboardLayout.Keys.ToList(),
    }, device);

    public static KeyLayout BuildExport(LightDevice device)
    {
        var layout = Load(device);
        var placed = layout.Keys.Select(k => k.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var placedIds = layout.Keys.Where(k => k.LightId is not null).Select(k => k.LightId!.Value).ToHashSet();

        var row = layout.Rows;
        var col = 0.0;
        foreach (var light in device.Lights.Where(l => !placed.Contains(l.Name) && !placedIds.Contains(l.Id)).OrderBy(l => l.Id))
        {
            layout.Keys.Add(new KeyPlacement(light.Name, col, row) { LightId = light.Id });
            col += 1;
            if (col >= KeyboardLayout.Columns)
            {
                col = 0;
                row++;
            }
        }

        return Normalize(layout, device);
    }

    public static void Save(LightDevice device, KeyLayout layout)
    {
        Directory.CreateDirectory(Folder);
        layout.Vid = device.VendorId;
        layout.Pid = device.ProductId;
        layout.Device = string.IsNullOrWhiteSpace(layout.Device) ? device.Name : layout.Device;

        var json = JsonSerializer.Serialize(layout, Options);
        File.WriteAllText(PathFor(device.VendorId, device.ProductId), json);
    }

    public static KeyLayout Normalize(KeyLayout layout, LightDevice device)
    {
        if (string.IsNullOrWhiteSpace(layout.Device))
            layout.Device = device.Name;

        layout.Vid = device.VendorId;
        layout.Pid = device.ProductId;
        layout.Columns = layout.Keys.Count == 0
            ? 1
            : Math.Max(1, (int)Math.Ceiling(layout.Keys.Max(k => k.Col + k.Width)));
        layout.Rows = layout.Keys.Count == 0
            ? 1
            : Math.Max(1, (int)Math.Ceiling(layout.Keys.Max(k => k.Row + k.Height)));
        return layout;
    }
}
