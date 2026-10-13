using System.Collections.ObjectModel;
using AlienFXControlCenter.Models;
using AlienFXControlCenter.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AlienFXControlCenter.ViewModels;

public sealed partial class TemplateItem : ObservableObject
{
    public required LightingTemplate Template { get; init; }

    public string Name => Template.Name;

    public string Summary { get; init; } = string.Empty;

    public string? Description => Template.Description;
}

public sealed partial class TemplatesViewModel : ObservableObject
{
    public ObservableCollection<TemplateItem> Templates { get; } = new();

    public bool ShowDebug => SettingsService.Current.Value.ShowDebugPanel;

    [ObservableProperty]
    private TemplateItem? selectedTemplate;

    [ObservableProperty]
    private string status = Localizer.Get("Status_Ready");

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private bool hasTemplates;

    [ObservableProperty]
    private string lastCommand = string.Empty;

    public static string SuggestName() => DateTime.Now.ToString("yyyy-MM-dd HH-mm");

    public void Load()
    {
        TemplateStore.Invalidate();

        Templates.Clear();
        foreach (var template in TemplateStore.All())
            Templates.Add(Describe(template));

        HasTemplates = Templates.Count > 0;
        SelectedTemplate = Templates.FirstOrDefault();
    }

    public TemplateItem Describe(LightingTemplate template)
    {
        var parts = new List<string>();
        if (template.HasLights)
            parts.Add(Localizer.Format("Templates_SummaryLights", CountLights(template)));
        if (template.HasFans)
            parts.Add(Localizer.Format("Templates_SummaryFans", template.Fans.Count));

        return new TemplateItem
        {
            Template = template,
            Summary = parts.Count == 0 ? Localizer.Get("Templates_EmptySummary") : string.Join(" · ", parts),
        };
    }

    private static int CountLights(LightingTemplate template)
        => template.Lights.Count + (template.WholeSystemColor is null ? 0 : 1);

    public LightingTemplate? SaveCurrent(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            Status = Localizer.Get("Templates_NeedName");
            return null;
        }

        if (PresetState.IsEmpty)
        {
            Status = Localizer.Get("Templates_NoLiveState");
            return null;
        }

        var template = SaveCurrentState(name);
        Load();

        Status = Localizer.Format("Templates_Saved", template.Name);
        return template;
    }

    public static LightingTemplate SaveCurrentState(string name)
    {
        var template = PresetState.Snapshot(name.Trim());
        template.Name = TemplateStore.UniqueName(template.Name);
        TemplateStore.Save(template);
        return template;
    }

    public bool Rename(TemplateItem item, string newName)
    {
        if (string.IsNullOrWhiteSpace(newName))
            return false;

        newName = newName.Trim();
        if (string.Equals(item.Name, newName, StringComparison.Ordinal))
            return true;

        if (!TemplateStore.Rename(item.Name, newName))
        {
            Status = Localizer.Format("Templates_RenameFailed", newName);
            return false;
        }

        Load();
        Status = Localizer.Format("Templates_Renamed", newName);
        return true;
    }

    public void Duplicate(TemplateItem item)
    {
        var copy = TemplateStore.Duplicate(item.Name);
        Load();
        Status = copy is null
            ? Localizer.Format("Templates_RenameFailed", item.Name)
            : Localizer.Format("Templates_Saved", copy.Name);
    }

    public bool Delete(TemplateItem item)
    {
        var name = item.Name;
        var removed = TemplateStore.Delete(name);
        Load();
        if (removed)
            Status = Localizer.Format("Templates_Deleted", name);
        return removed;
    }

    public void Export(TemplateItem item, string path)
    {
        try
        {
            TemplateStore.Export(item.Template, path);
            Status = Localizer.Format("Templates_Exported", path);
        }
        catch (Exception ex)
        {
            Status = Localizer.Format("Templates_ExportFailed", ex.Message);
        }
    }

    public TemplateItem? Import(string path)
    {
        try
        {
            var template = TemplateStore.Import(path);
            Load();
            Status = Localizer.Format("Templates_Imported", template.Name);
            return SelectedTemplate;
        }
        catch (Exception ex)
        {
            Status = Localizer.Format("Templates_ImportFailed", ex.Message);
            return null;
        }
    }

    public async Task ApplyAsync(TemplateItem item, CancellationToken ct = default)
    {
        IsBusy = true;
        try
        {
            var outcome = await TemplateApplier.ApplyAsync(item.Template, ct);
            LastCommand = outcome.Commands;
            Status = TemplateApplier.FormatStatus(item.Template, outcome);
        }
        catch (Exception ex)
        {
            Status = Localizer.Format("Templates_ApplyFailed", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static string FirstLine(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return Localizer.Get("Status_UnknownError");

        var line = text.Split('\n')[0].Trim();
        return line.Length == 0 ? Localizer.Get("Status_UnknownError") : line;
    }
}
