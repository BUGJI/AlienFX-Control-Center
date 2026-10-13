using System.IO;
using AlienFXControlCenter.Services;
using AlienFXControlCenter.Services.Input;
using Microsoft.UI.Xaml;
using WinRT.Interop;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace AlienFXControlCenter;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : Application
{
    private Window? _window;

    public static Window? MainWindow { get; private set; }

    /// <summary>
    /// Initializes the singleton application object.  This is the first line of authored code
    /// executed, and as such is the logical equivalent of main() or WinMain().
    /// </summary>
    public App()
    {
        Log.Reset();
        SettingsService.Current.Load();
        Localizer.ApplyLanguage(SettingsService.Current.Value.Language);

        InitializeComponent();
        UnhandledException += OnUnhandledException;
    }

    /// <summary>
    /// Invoked when the application is launched.
    /// </summary>
    /// <param name="args">Details about the launch request and process.</param>
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            _window = new MainWindow();
            MainWindow = _window;
            _window.Activate();

            InputService.Current.Start(WindowNative.GetWindowHandle(_window!));
            KeyActionDispatcher.Current.Start();

            if (SettingsService.Current.Value.AutoApplyOnStartup)
                _ = ApplyStartupPresetAsync();
        }
        catch (Exception ex)
        {
            Log.Write($"Launch failed: {ex}");
            throw;
        }
    }

    private static async Task ApplyStartupPresetAsync()
    {
        var template = PresetState.LoadLastApplied();
        if (template is null || (!template.HasLights && !template.HasFans))
            return;

        try
        {
            var outcome = await TemplateApplier.ApplyAsync(template);
            Log.Write(
                $"Auto-apply on startup: fxMissing={outcome.FxMissing}, fanMissing={outcome.FanMissing}, " +
                $"lights={outcome.LightCount}, fans={outcome.FanCount}");
        }
        catch (Exception ex)
        {
            Log.Write($"Auto-apply on startup failed: {ex.Message}");
        }
    }

    private static void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
        => Log.Write($"Unhandled exception: {e.Message}{Environment.NewLine}{e.Exception}");
}
