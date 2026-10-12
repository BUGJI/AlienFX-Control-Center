using System.IO;

namespace AlienFXControlCenter.Services;

public static class AppServices
{
    static AppServices()
    {
        SettingsService.Current.Load();
        Refresh();
    }

    public static string? ToolsFolder { get; private set; }

    public static AlienFxClient? Fx { get; private set; }

    public static AlienFanClient? Fan { get; private set; }

    public static bool ToolsAvailable => ToolsFolder is not null;

    public static void Refresh()
    {
        var folder = CliLocator.FindToolsFolder();
        ToolsFolder = folder;

        Fx = null;
        Fan = null;

        if (folder is null)
            return;

        var fxPath = Path.Combine(folder, "alienfx-cli.exe");
        var fanPath = Path.Combine(folder, "alienfan-cli.exe");

        if (File.Exists(fxPath))
            Fx = new AlienFxClient(fxPath);

        if (File.Exists(fanPath))
            Fan = new AlienFanClient(fanPath);
    }
}
