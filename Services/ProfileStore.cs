using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using AlienFXControlCenter.Controls;
using AlienFXControlCenter.Models;

namespace AlienFXControlCenter.Services;

public sealed record DeviceIdentity(int VendorId, int ProductId, string Name);

public static class ProfileStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static List<DeviceProfile>? _cache;

    public static DeviceProfile? Current { get; set; }

    public static string UserFolder { get; } = Path.Combine(SettingsService.Current.Folder, "Profiles");

    public static string BuiltInFolder => Path.Combine(AppContext.BaseDirectory, "Assets", "Profiles");

    public static string MappingsFile => AppServices.ToolsFolder is { } folder
        ? Path.Combine(folder, "Mappings", "devices.csv")
        : string.Empty;

    public static IReadOnlyList<DeviceProfile> All()
    {
        if (_cache is not null)
            return _cache;

        var profiles = new List<DeviceProfile>();
        profiles.AddRange(LoadJsonFolder(BuiltInFolder, "Built-in"));
        profiles.AddRange(LoadMappings());
        profiles.AddRange(LoadJsonFolder(UserFolder, "User"));

        _cache = profiles;
        return profiles;
    }

    public static void Invalidate() => _cache = null;

    public static DeviceProfile? Match(IEnumerable<DeviceIdentity> devices)
    {
        var all = All();
        var manual = SettingsService.Current.Value.ProfileKey;
        if (!string.IsNullOrWhiteSpace(manual))
        {
            var chosen = all.FirstOrDefault(p => string.Equals(p.Name, manual, StringComparison.OrdinalIgnoreCase));
            if (chosen is not null)
                return chosen;
        }

        return MatchAuto(devices);
    }

    public static DeviceProfile? MatchAuto(IEnumerable<DeviceIdentity> devices)
    {
        var list = devices.ToList();
        if (list.Count == 0)
            return null;

        DeviceProfile? best = null;
        var bestScore = 0;

        foreach (var profile in All())
        {
            var score = 0;
            foreach (var device in list)
            {
                foreach (var candidate in profile.Devices)
                {
                    if (candidate.VendorId != device.VendorId || candidate.ProductId != device.ProductId)
                        continue;

                    score++;
                    if (!string.IsNullOrEmpty(device.Name) &&
                        string.Equals(candidate.Name, device.Name, StringComparison.OrdinalIgnoreCase))
                        score += 2;
                }
            }

            if (score > bestScore)
            {
                bestScore = score;
                best = profile;
            }
        }

        return bestScore > 0 ? best : null;
    }

    public static KeyLayout BuildLayout(DeviceProfile profile, LightDevice device)
    {
        var target = profile.FindDevice(device.VendorId, device.ProductId);
        if (target is null || target.Lights.Count == 0)
            return KeyLayoutStore.BuildDefault(device);

        var layout = new KeyLayout { Device = device.Name };
        var placed = new HashSet<int>();

        foreach (var light in target.Lights)
        {
            if (light.Cells.Count == 0 || !placed.Add(light.Id))
                continue;

            var (row, col) = Resolve(profile, light.Cells[0]);
            var width = light.Cells
                .Select(c => Resolve(profile, c))
                .Where(r => r.Row == row)
                .Select(r => r.Col)
                .Distinct()
                .Count();

            layout.Keys.Add(new KeyPlacement(light.Name, col, row, Math.Max(1, width)) { LightId = light.Id });
        }

        Realign(layout);

        return KeyLayoutStore.Normalize(layout, device);
    }

    // Real keyboards are not a uniform grid: reposition keys using standard key
    // widths on rows that contain only "typing" keys, and make the top row
    // (function row) half height. Rows that host media/arrow clusters keep the
    // grid coordinates, which already place those keys correctly.
    private static void Realign(KeyLayout layout)
    {
        if (layout.Keys.Count == 0)
            return;

        var minRow = layout.Keys.Min(k => k.Row);
        foreach (var key in layout.Keys)
            key.Row -= minRow;

        // Function row (topmost) is half height, like a real laptop.
        foreach (var key in layout.Keys.Where(k => k.Row == 0))
            key.Height = 0.5;

        foreach (var row in layout.Keys.GroupBy(k => k.Row))
        {
            var ordered = row.OrderBy(k => k.Col).ToList();
            var firstSpecial = ordered.FirstOrDefault(k => IsSpecialKey(k.Name));

            if (firstSpecial is null)
            {
                var x = 0.0;
                foreach (var key in ordered)
                {
                    key.Width = StandardWidth(key.Name);
                    key.Col = x;
                    x += key.Width;
                }
                continue;
            }

            // Accumulate the leading typing block; if it would run into the
            // media/arrow cluster on the right, keep the grid coordinates.
            var main = ordered.TakeWhile(k => !IsSpecialKey(k.Name)).ToList();
            var original = main.Select(k => (Key: k, k.Col, k.Width)).ToList();
            var cursor = 0.0;
            foreach (var key in main)
            {
                key.Width = StandardWidth(key.Name);
                key.Col = cursor;
                cursor += key.Width;
            }

            if (cursor > firstSpecial.Col + 0.001)
            {
                foreach (var (key, col, width) in original)
                {
                    key.Col = col;
                    key.Width = width;
                }
            }
        }

        // Left-align every row and pull the right-hand cluster (media/arrows)
        // flush against the typing block so the whole board forms a rectangle.
        foreach (var row in layout.Keys.GroupBy(k => k.Row))
        {
            var min = row.Min(k => k.Col);
            if (min > 0.001)
            {
                foreach (var key in row)
                    key.Col -= min;
            }

            var mains = row.Where(k => !IsSpecialKey(k.Name)).ToList();
            var specials = row.Where(k => IsSpecialKey(k.Name)).OrderBy(k => k.Col).ToList();
            if (mains.Count == 0 || specials.Count == 0)
                continue;

            var mainRight = mains.Max(k => k.Col + k.Width);
            var gap = specials[0].Col - mainRight;
            if (gap > 0.001)
            {
                foreach (var special in specials)
                    special.Col -= gap;
            }
        }
    }

    private static readonly HashSet<string> SpecialKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "Up", "Down", "Left", "Right", "Mute", "Vol up", "Vol down", "Mic Mute",
    };

    private static bool IsSpecialKey(string name) => SpecialKeys.Contains(name.Trim());

    private static double StandardWidth(string name)
    {
        return name.Trim().ToLowerInvariant() switch
        {
            "tab" => 1.5,
            "caps" or "caps lock" => 1.75,
            "backspace" => 2.0,
            "\\" => 1.5,
            "enter" or "return" => 2.25,
            "shift" => 2.25,
            "right shift" => 1.75,
            "space" => 6.25,
            "ctrl" or "left ctrl" or "right ctrl" or "alt" or "right alt"
                or "fn" or "left windows" or "windows" or "win" or "win lock" => 1.25,
            _ => 1.0,
        };
    }

    private static (int Row, int Col) Resolve(DeviceProfile profile, GridCell cell)
    {
        var grid = profile.Grids.FirstOrDefault(g => g.Id == cell.GridId);
        var columns = grid is { Columns: > 0 } ? grid.Columns : 1;
        return (cell.Position / columns, cell.Position % columns);
    }

    private static IEnumerable<DeviceProfile> LoadJsonFolder(string folder, string source)
    {
        if (!Directory.Exists(folder))
            yield break;

        foreach (var file in Directory.EnumerateFiles(folder, "*.json"))
        {
            DeviceProfile? profile = null;
            try
            {
                profile = JsonSerializer.Deserialize<DeviceProfile>(File.ReadAllText(file), Options);
            }
            catch (Exception)
            {
            }

            if (profile is null || string.IsNullOrWhiteSpace(profile.Name))
                continue;

            profile.Source = source;
            yield return profile;
        }
    }

    private static IEnumerable<DeviceProfile> LoadMappings()
    {
        var path = MappingsFile;
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
            yield break;

        List<DeviceProfile> profiles;
        try
        {
            profiles = AlienFxMappingParser.Parse(File.ReadAllText(path));
        }
        catch (Exception)
        {
            yield break;
        }

        foreach (var profile in profiles)
        {
            profile.Source = "AlienFX Tools";
            yield return profile;
        }
    }

    public static void SaveUserProfile(DeviceProfile profile, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(profile, Options));
    }
}
