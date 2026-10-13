using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AlienFXControlCenter.Services.Input;

public static class KeyStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private static List<NamedKey>? _keys;

    public static string FilePath { get; } = Path.Combine(SettingsService.Current.Folder, "keys.json");

    public static IReadOnlyList<NamedKey> Keys => _keys ??= Load();

    /// <summary>Raised after any add, update, remove or reset so consumers can refresh.</summary>
    public static event Action? Changed;

    public static void Add(NamedKey key)
    {
        _keys ??= Load();
        if (key.Id == Guid.Empty)
            key.Id = Guid.NewGuid();
        _keys.Add(key);
        Save();
        Changed?.Invoke();
    }

    public static void Remove(NamedKey key)
    {
        _keys ??= Load();
        if (_keys.Remove(key))
        {
            Save();
            Changed?.Invoke();
        }
    }

    public static void Rename(NamedKey key, string name)
    {
        _keys ??= Load();
        key.Name = name;
        Save();
        Changed?.Invoke();
    }

    public static void Replace(NamedKey key, KeySignature signature)
    {
        _keys ??= Load();
        key.Signature = signature;
        Save();
        Changed?.Invoke();
    }

    public static void SetAction(NamedKey key, KeyAction? action)
    {
        _keys ??= Load();
        key.Action = action is { Kind: KeyActionKind.None } ? null : action;
        Save();
        Changed?.Invoke();
    }

    public static void ResetToDefaults()
    {
        _keys = CreateDefaults();
        Save();
        Changed?.Invoke();
    }

    public static NamedKey? FindBySignature(KeySignature signature)
        => Keys.FirstOrDefault(k => k.Signature.Matches(signature));

    public static bool NameExists(string name, Guid exceptId)
        => Keys.Any(k => k.Id != exceptId && string.Equals(k.Name, name, StringComparison.OrdinalIgnoreCase));

    public static void Save()
    {
        try
        {
            Directory.CreateDirectory(SettingsService.Current.Folder);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(_keys ?? new List<NamedKey>(), Options));
        }
        catch (Exception ex)
        {
            Log.Write($"KeyStore save failed: {ex.Message}");
        }
    }

    private static List<NamedKey> Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var keys = JsonSerializer.Deserialize<List<NamedKey>>(File.ReadAllText(FilePath), Options) ?? new List<NamedKey>();
                foreach (var key in keys)
                {
                    if (key.Id == Guid.Empty)
                        key.Id = Guid.NewGuid();
                }

                return keys;
            }
        }
        catch (Exception ex)
        {
            Log.Write($"KeyStore load failed: {ex.Message}");
        }

        return CreateDefaults();
    }

    private static List<NamedKey> CreateDefaults() => new()
    {
        Keyboard("Performance (F17)", 0x80, 0x68),
        Consumer("Custom A", 0x51),
        Consumer("Custom B", 0x52),
        Consumer("Custom C", 0x53),
        Consumer("Custom D", 0x54),
        Consumer("Custom E", 0x55),
    };

    private static NamedKey Keyboard(string name, int vk, int scan) => new()
    {
        Name = name,
        BuiltIn = true,
        Signature = new KeySignature { Kind = KeySignatureKind.Keyboard, VKey = vk, ScanCode = scan },
    };

    private static NamedKey Consumer(string name, int code) => new()
    {
        Name = name,
        BuiltIn = true,
        Signature = new KeySignature
        {
            Kind = KeySignatureKind.Hid,
            VendorId = 0x0D62,
            ProductId = 0xD2B1,
            UsagePage = 0x000C,
            Usage = 0x0001,
            ReportHex = $"02{code:X2}00",
        },
    };
}
