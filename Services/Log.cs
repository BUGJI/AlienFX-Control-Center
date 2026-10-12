using System.IO;

namespace AlienFXControlCenter.Services;

public static class Log
{
    public static string FilePath { get; } = Path.Combine(SettingsService.Current.Folder, "startup.log");

    public static void Reset()
    {
        try
        {
            Directory.CreateDirectory(SettingsService.Current.Folder);
            File.WriteAllText(FilePath, string.Empty);
        }
        catch (Exception)
        {
        }
    }

    public static void Write(string message)
    {
        try
        {
            Directory.CreateDirectory(SettingsService.Current.Folder);
            File.AppendAllText(FilePath, $"{DateTime.Now:HH:mm:ss.fff} {message}{Environment.NewLine}");
        }
        catch (Exception)
        {
        }
    }
}
