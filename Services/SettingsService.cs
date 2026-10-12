using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using AlienFXControlCenter.Models;

namespace AlienFXControlCenter.Services;

public sealed class SettingsService
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static SettingsService Current { get; } = new();

    public string Folder { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AlienFX Control Center");

    public string FilePath => Path.Combine(Folder, "settings.json");

    public AppSettings Value { get; private set; } = new();

    public event EventHandler? Changed;

    public void Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var loaded = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath), Options);
                if (loaded is not null)
                    Value = loaded;
            }
        }
        catch (Exception)
        {
            Value = new AppSettings();
        }
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Folder);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(Value, Options));
        }
        catch (Exception)
        {
        }
    }

    public void Update(Action<AppSettings> mutate)
    {
        mutate(Value);
        Save();
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
