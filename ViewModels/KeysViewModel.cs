using System.Collections.ObjectModel;
using AlienFXControlCenter.Services;
using AlienFXControlCenter.Services.Input;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AlienFXControlCenter.ViewModels;

/// <summary>Display wrapper for a <see cref="NamedKey"/> in the registration table.</summary>
public sealed partial class KeyRow : ObservableObject
{
    public required NamedKey Key { get; init; }

    public string Name => Key.Name;

    public string TypeText => Key.Signature.Kind == KeySignatureKind.Keyboard
        ? Localizer.Get("Keys_TypeKeyboard")
        : Localizer.Get("Keys_TypeHid");

    public string SignatureText => Key.Signature.Describe();

    public string SourceText => Key.BuiltIn
        ? Localizer.Get("Keys_SourceBuiltIn")
        : Localizer.Get("Keys_SourceCustom");

    public string ActionText => Key.HasAction
        ? Key.Action?.TemplateName ?? string.Empty
        : Localizer.Get("Keys_ActionNone");

    public bool ActionMissing => Key.HasAction && string.IsNullOrWhiteSpace(Key.Action?.TemplateName);

    public bool ShowTypingWarning => Key.IsTypingKey && Key.HasAction;

    public bool Dimmed => !Key.Enabled;

    public void NotifyEnabledChanged() => OnPropertyChanged(nameof(Dimmed));
}

public sealed partial class KeysViewModel : ObservableObject
{
    private readonly List<NamedKey> _all = new();

    public ObservableCollection<KeyRow> Keys { get; } = new();

    [ObservableProperty]
    private string searchText = string.Empty;

    [ObservableProperty]
    private int filterIndex;

    [ObservableProperty]
    private string status = Localizer.Get("Status_Ready");

    [ObservableProperty]
    private bool hasKeys;

    public void Load()
    {
        _all.Clear();
        _all.AddRange(KeyStore.Keys);
        RefreshActionNames();
        ApplyFilters();
    }

    partial void OnSearchTextChanged(string value) => ApplyFilters();

    partial void OnFilterIndexChanged(int value) => ApplyFilters();

    private void ApplyFilters()
    {
        IEnumerable<NamedKey> query = _all;

        query = FilterIndex switch
        {
            1 => query.Where(k => !k.BuiltIn),
            2 => query.Where(k => k.BuiltIn),
            _ => query,
        };

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var term = SearchText.Trim();
            query = query.Where(k =>
                k.Name.Contains(term, StringComparison.OrdinalIgnoreCase)
                || k.Signature.Describe().Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        Keys.Clear();
        foreach (var key in query)
            Keys.Add(new KeyRow { Key = key });

        HasKeys = Keys.Count > 0;
    }

    public void RefreshEnabled(NamedKey key)
    {
        foreach (var row in Keys)
        {
            if (ReferenceEquals(row.Key, key))
            {
                row.NotifyEnabledChanged();
                return;
            }
        }
    }

    private void RefreshActionNames()
    {
        var healed = false;
        foreach (var key in _all)
        {
            if (key.Action is not { Kind: KeyActionKind.ApplyTemplate } action)
                continue;

            var template = Guid.TryParse(action.TemplateId, out var id)
                ? TemplateStore.GetById(id)
                : null;

            if (template is null && !string.IsNullOrWhiteSpace(action.TemplateName))
            {
                // Heal bindings that point at an id regenerated for a template
                // saved before ids were persisted.
                template = TemplateStore.Get(action.TemplateName);
                if (template is not null)
                {
                    action.TemplateId = template.Id.ToString();
                    healed = true;
                }
            }

            action.TemplateName = template?.Name;
        }

        if (healed)
            KeyStore.Save();
    }

    public void AddKey(NamedKey key)
    {
        KeyStore.Add(key);
        Load();
    }

    public void RenameKey(NamedKey key, string name)
    {
        KeyStore.Rename(key, name);
        Load();
    }

    public void Recapture(NamedKey key, KeySignature signature)
    {
        KeyStore.Replace(key, signature);
        Load();
    }

    public void DeleteKey(NamedKey key)
    {
        KeyStore.Remove(key);
        Load();
    }

    public void ResetDefaults()
    {
        KeyStore.ResetToDefaults();
        Load();
    }

    public void SetAction(NamedKey key, KeyAction? action)
    {
        KeyStore.SetAction(key, action);
        Load();
    }

    public void Save()
    {
        KeyStore.Save();
        Load();
    }
}
