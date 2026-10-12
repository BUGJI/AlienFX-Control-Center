using System.IO;

namespace AlienFXControlCenter.Services;

public static class CliLocator
{
    private const string FolderName = "AlienFX Tools";

    public static string? FindToolsFolder()
    {
        var overrideFolder = SettingsService.Current.Value.ToolsFolder;
        if (!string.IsNullOrWhiteSpace(overrideFolder) && IsToolsFolder(overrideFolder))
            return overrideFolder;

        foreach (var candidate in DefaultCandidates())
        {
            if (IsToolsFolder(candidate))
                return candidate;
        }

        return null;
    }

    public static IEnumerable<string> DefaultCandidates()
    {
        yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), FolderName);
        yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), FolderName);
        yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), FolderName);
    }

    public static bool IsToolsFolder(string folder)
        => !string.IsNullOrWhiteSpace(folder) && File.Exists(Path.Combine(folder, "alienfx-cli.exe"));

    public static string? Find(string exeName)
    {
        var folder = FindToolsFolder();
        return folder is null ? null : Path.Combine(folder, exeName);
    }
}
