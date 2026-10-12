namespace AlienFXControlCenter.Models;

public sealed class AppSettings
{
    public string Language { get; set; } = "system";

    public string Theme { get; set; } = "system";

    public string? ToolsFolder { get; set; }

    public string? ProfileKey { get; set; }

    public bool ShowDebugPanel { get; set; }

    public bool AutoApplyOnStartup { get; set; }
}
