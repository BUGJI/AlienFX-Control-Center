using Microsoft.Windows.ApplicationModel.Resources;
using Microsoft.Windows.Globalization;

namespace AlienFXControlCenter.Services;

public static class Localizer
{
    private static ResourceLoader? _loader;

    private static ResourceLoader Loader => _loader ??= new ResourceLoader();

    public static string Get(string key)
    {
        try
        {
            var value = Loader.GetString(key);
            return string.IsNullOrEmpty(value) ? key : value;
        }
        catch (Exception ex)
        {
            Log.Write($"Localizer.Get('{key}') failed: {ex.Message}");
            return key;
        }
    }

    public static string Format(string key, params object?[] args)
    {
        var format = Get(key);
        try
        {
            return string.Format(format, args);
        }
        catch (FormatException)
        {
            return format;
        }
    }

    public static void ApplyLanguage(string language)
    {
        // Assigning an empty string (to request "system language") fails hard in
        // unpackaged WinUI 3, so only override for an explicit language. The
        // override is process-scoped and therefore reset on the next launch.
        if (string.IsNullOrEmpty(language) || language == "system")
            return;

        ApplicationLanguages.PrimaryLanguageOverride = language;
    }
}
