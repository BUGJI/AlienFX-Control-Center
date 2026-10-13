using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using AlienFXControlCenter.Models;

namespace AlienFXControlCenter.Services;

public static class TemplateStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static List<LightingTemplate>? _cache;

    public static string Folder { get; } = Path.Combine(SettingsService.Current.Folder, "Templates");

    public static IReadOnlyList<LightingTemplate> All()
    {
        if (_cache is not null)
            return _cache;

        var templates = new List<LightingTemplate>();
        if (Directory.Exists(Folder))
        {
            foreach (var file in Directory.EnumerateFiles(Folder, "*.json"))
            {
                var template = LoadFile(file);
                if (template is not null)
                    templates.Add(template);
            }
        }

        _cache = templates
            .OrderBy(t => t.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        return _cache;
    }

    public static void Invalidate() => _cache = null;

    public static LightingTemplate? Get(string name)
        => All().FirstOrDefault(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase));

    public static LightingTemplate? GetById(Guid id)
        => id == Guid.Empty ? null : All().FirstOrDefault(t => t.Id == id);

    public static bool Exists(string name) => Get(name) is not null;

    public static void Save(LightingTemplate template)
    {
        Directory.CreateDirectory(Folder);
        template.Name = template.Name.Trim();
        if (template.Id == Guid.Empty)
            template.Id = Guid.NewGuid();
        template.UpdatedUtc = DateTimeOffset.UtcNow;

        var path = PathFor(template.Name);
        DeleteStaleFiles(template.Name);
        File.WriteAllText(path, JsonSerializer.Serialize(template, Options));
        Invalidate();
    }

    public static bool Delete(string name)
    {
        var path = PathFor(name);
        if (!File.Exists(path))
        {
            Invalidate();
            return false;
        }

        File.Delete(path);
        Invalidate();
        return true;
    }

    public static bool Rename(string oldName, string newName)
    {
        var template = Get(oldName);
        if (template is null)
            return false;

        var newPath = PathFor(newName);
        if (File.Exists(newPath) && !string.Equals(oldName, newName, StringComparison.OrdinalIgnoreCase))
            return false;

        DeleteStaleFiles(oldName);
        template.Name = newName.Trim();
        template.UpdatedUtc = DateTimeOffset.UtcNow;
        File.WriteAllText(newPath, JsonSerializer.Serialize(template, Options));
        Invalidate();
        return true;
    }

    public static LightingTemplate? Duplicate(string name)
    {
        var source = Get(name);
        if (source is null)
            return null;

        var copy = new LightingTemplate
        {
            Name = UniqueName($"{source.Name} (copy)"),
            Description = source.Description,
            WholeSystemColor = source.WholeSystemColor,
            Lights = source.Lights
                .Select(l => new TemplateLightColor
                {
                    VendorId = l.VendorId,
                    ProductId = l.ProductId,
                    LightId = l.LightId,
                    LightName = l.LightName,
                    R = l.R,
                    G = l.G,
                    B = l.B,
                })
                .ToList(),
            Fans = source.Fans
                .Select(f => new TemplateFanSetting { FanId = f.FanId, Name = f.Name, Percent = f.Percent })
                .ToList(),
        };

        Save(copy);
        return copy;
    }

    public static string UniqueName(string desired)
    {
        var baseName = string.IsNullOrWhiteSpace(desired) ? "Template" : desired.Trim();
        if (!Exists(baseName))
            return baseName;

        for (var i = 2; ; i++)
        {
            var candidate = $"{baseName} ({i})";
            if (!Exists(candidate))
                return candidate;
        }
    }

    public static void Export(LightingTemplate template, string path)
        => File.WriteAllText(path, JsonSerializer.Serialize(template, Options));

    public static LightingTemplate Import(string path)
    {
        var template = Deserialize(path) ?? throw new InvalidOperationException("Empty template");
        if (template.Id == Guid.Empty || All().Any(t => t.Id == template.Id))
            template.Id = Guid.NewGuid();
        Save(template);
        return template;
    }

    public static string PathFor(string name)
        => Path.Combine(Folder, $"{SafeFileName(name)}.json");

    private static string SafeFileName(string name)
        => string.Join("_", name.Split(Path.GetInvalidFileNameChars()));

    private static void DeleteStaleFiles(string name)
    {
        if (!Directory.Exists(Folder))
            return;

        var safe = SafeFileName(name);
        foreach (var file in Directory.EnumerateFiles(Folder, "*.json"))
        {
            if (!string.Equals(Path.GetFileNameWithoutExtension(file), safe, StringComparison.OrdinalIgnoreCase))
                continue;

            try
            {
                File.Delete(file);
            }
            catch (Exception)
            {
            }
        }
    }

    private static LightingTemplate? LoadFile(string path)
    {
        var template = Deserialize(path);
        if (template is null)
            return null;

        // Older template files predate the stable Id used by key bindings;
        // assign one and persist it so bindings survive a restart.
        if (template.Id == Guid.Empty)
        {
            template.Id = Guid.NewGuid();
            TryWrite(path, template);
        }

        return template;
    }

    private static LightingTemplate? Deserialize(string path)
    {
        try
        {
            var template = JsonSerializer.Deserialize<LightingTemplate>(File.ReadAllText(path), Options);
            return template is null || string.IsNullOrWhiteSpace(template.Name) ? null : template;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static void TryWrite(string path, LightingTemplate template)
    {
        try
        {
            File.WriteAllText(path, JsonSerializer.Serialize(template, Options));
        }
        catch (Exception)
        {
        }
    }
}
