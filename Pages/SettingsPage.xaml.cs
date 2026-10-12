using System.Reflection;
using System.Text.Json;
using AlienFXControlCenter.Models;
using AlienFXControlCenter.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace AlienFXControlCenter.Pages;

public sealed partial class SettingsPage : Page
{
    private sealed record Choice(string Code, string Label);

    private bool _loading;

    public SettingsPage()
    {
        InitializeComponent();
        Loaded += SettingsPage_Loaded;
    }

    private async void SettingsPage_Loaded(object sender, RoutedEventArgs e)
    {
        PopulateChoices();
        LoadFromSettings();
        await RefreshProfilesAsync();
        await LoadCliVersionAsync();
    }

    private void PopulateChoices()
    {
        _loading = true;

        LanguageBox.ItemsSource = new List<Choice>
        {
            new("system", Localizer.Get("Settings_Language_System")),
            new("zh-CN", Localizer.Get("Settings_Language_Chinese")),
            new("en-US", Localizer.Get("Settings_Language_English")),
        };

        ThemeBox.ItemsSource = new List<Choice>
        {
            new("system", Localizer.Get("Settings_Theme_System")),
            new("light", Localizer.Get("Settings_Theme_Light")),
            new("dark", Localizer.Get("Settings_Theme_Dark")),
        };

        LicenseText.Text = Localizer.Get("Settings_About_LicenseValue");
        _loading = false;
    }

    private void LoadFromSettings()
    {
        _loading = true;

        var settings = SettingsService.Current.Value;
        LanguageBox.SelectedItem = Pick(LanguageBox, settings.Language);
        ThemeBox.SelectedItem = Pick(ThemeBox, settings.Theme);
        AutoApplyBox.IsChecked = settings.AutoApplyOnStartup;
        ShowDebugBox.IsChecked = settings.ShowDebugPanel;
        AppVersionText.Text = AppVersion();
        UpdateToolsFolderText();

        _loading = false;
    }

    private static Choice? Pick(ComboBox box, string code)
        => (box.ItemsSource as IEnumerable<Choice>)?.FirstOrDefault(c => c.Code == code);

    private void LanguageBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || LanguageBox.SelectedItem is not Choice choice)
            return;

        SettingsService.Current.Update(s => s.Language = choice.Code);
        Localizer.ApplyLanguage(choice.Code);
        _ = ShowRestartDialogAsync();
    }

    private void ThemeBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || ThemeBox.SelectedItem is not Choice choice)
            return;

        SettingsService.Current.Update(s => s.Theme = choice.Code);
    }

    private void AutoApply_Click(object sender, RoutedEventArgs e)
        => SettingsService.Current.Update(s => s.AutoApplyOnStartup = AutoApplyBox.IsChecked == true);

    private void ShowDebug_Click(object sender, RoutedEventArgs e)
        => SettingsService.Current.Update(s => s.ShowDebugPanel = ShowDebugBox.IsChecked == true);

    private async void Browse_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FolderPicker();
        picker.FileTypeFilter.Add("*");
        Initialize(picker);

        var folder = await picker.PickSingleFolderAsync();
        if (folder is null)
            return;

        SettingsService.Current.Update(s => s.ToolsFolder = folder.Path);
        AppServices.Refresh();
        ProfileStore.Invalidate();
        UpdateToolsFolderText();
        await RefreshProfilesAsync();
        StatusText.Text = Localizer.Get("Settings_ToolsFolderUpdated");
    }

    private async void Redetect_Click(object sender, RoutedEventArgs e)
    {
        AppServices.Refresh();
        ProfileStore.Invalidate();
        UpdateToolsFolderText();
        await RefreshProfilesAsync();
        StatusText.Text = Localizer.Get("Settings_Rematched");
    }

    private async void Probe_Click(object sender, RoutedEventArgs e)
    {
        var client = AppServices.Fx;
        if (client is null)
        {
            StatusText.Text = Localizer.Format("Status_ToolsMissing", "alienfx-cli");
            return;
        }

        StatusText.Text = Localizer.Get("Settings_ProbeRunning");
        try
        {
            await client.ProbeAsync();
            StatusText.Text = Localizer.Get("Settings_ProbeDone");
        }
        catch (Exception ex)
        {
            StatusText.Text = ex.Message;
        }
    }

    private async void Rematch_Click(object sender, RoutedEventArgs e)
    {
        SettingsService.Current.Update(s => s.ProfileKey = null);
        await RefreshProfilesAsync();
        StatusText.Text = Localizer.Get("Settings_Rematched");
    }

    private async void ResetProfile_Click(object sender, RoutedEventArgs e)
    {
        SettingsService.Current.Update(s => s.ProfileKey = null);
        await RefreshProfilesAsync();
        StatusText.Text = Localizer.Get("Settings_ResetDone");
    }

    private void ProfileBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || ProfileBox.SelectedItem is not DeviceProfile profile)
            return;

        SettingsService.Current.Update(s => s.ProfileKey = profile.Name);
        ProfileNameText.Text = profile.Name;
        ProfileSourceText.Text = $"{profile.Source} · {Localizer.Get("Settings_ProfileManual")}";
    }

    private void ExportProfile_Click(object sender, RoutedEventArgs e)
    {
        if (ProfileBox.SelectedItem is not DeviceProfile profile)
        {
            StatusText.Text = Localizer.Get("Settings_ProfileNone");
            return;
        }

        try
        {
            Directory.CreateDirectory(ProfileStore.UserFolder);
            var safe = string.Join("_", profile.Name.Split(Path.GetInvalidFileNameChars()));
            var path = Path.Combine(ProfileStore.UserFolder, $"{safe}.json");
            ProfileStore.SaveUserProfile(profile, path);
            StatusText.Text = Localizer.Format("Settings_ProfileExported", path);
        }
        catch (Exception ex)
        {
            StatusText.Text = Localizer.Format("Settings_ProfileImportFailed", ex.Message);
        }
    }

    private async void ImportProfile_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FileOpenPicker();
        picker.FileTypeFilter.Add(".json");
        Initialize(picker);

        var file = await picker.PickSingleFileAsync();
        if (file is null)
            return;

        try
        {
            var json = await File.ReadAllTextAsync(file.Path);
            _ = JsonSerializer.Deserialize<DeviceProfile>(json)
                ?? throw new InvalidOperationException("Empty profile");

            Directory.CreateDirectory(ProfileStore.UserFolder);
            File.Copy(file.Path, Path.Combine(ProfileStore.UserFolder, file.Name), true);
            await RefreshProfilesAsync();
            StatusText.Text = Localizer.Get("Settings_ProfileImported");
        }
        catch (Exception ex)
        {
            StatusText.Text = Localizer.Format("Settings_ProfileImportFailed", ex.Message);
        }
    }

    private async Task RefreshProfilesAsync()
    {
        ProfileStore.Invalidate();

        var profiles = ProfileStore.All().ToList();
        ProfileBox.ItemsSource = profiles;

        DeviceProfile? matched = null;
        var client = AppServices.Fx;
        if (client is not null)
        {
            try
            {
                var devices = await client.GetStatusAsync();
                var identities = devices.Select(d => new DeviceIdentity(d.VendorId, d.ProductId, d.Name)).ToList();
                matched = ProfileStore.Match(identities);
                ProfileStore.Current = matched;
            }
            catch (Exception)
            {
            }
        }

        _loading = true;
        ProfileBox.SelectedItem = matched;
        _loading = false;

        ProfileNameText.Text = matched?.Name ?? Localizer.Get("Settings_ProfileNone");
        ProfileSourceText.Text = matched is null
            ? string.Empty
            : $"{matched.Source} · {Localizer.Get(string.IsNullOrWhiteSpace(SettingsService.Current.Value.ProfileKey) ? "Settings_ProfileAuto" : "Settings_ProfileManual")}";
    }

    private void UpdateToolsFolderText()
    {
        var folder = AppServices.ToolsFolder;
        ToolsFolderText.Text = string.IsNullOrEmpty(folder)
            ? Localizer.Get("Settings_ToolsFolderMissing")
            : folder;
    }

    private async Task LoadCliVersionAsync()
    {
        var client = AppServices.Fx;
        if (client is null)
        {
            CliVersionText.Text = Localizer.Get("Settings_ToolsFolderMissing");
            return;
        }

        var version = await client.GetVersionAsync();
        CliVersionText.Text = string.IsNullOrEmpty(version) ? Localizer.Get("Settings_ToolsFolderMissing") : version;
    }

    private static void Initialize(object picker)
    {
        if (App.MainWindow is Window window)
        {
            var hwnd = WindowNative.GetWindowHandle(window);
            InitializeWithWindow.Initialize(picker, hwnd);
        }
    }

    private static string AppVersion()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var info = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        info = info?.Split('+')[0];
        return !string.IsNullOrWhiteSpace(info)
            ? info
            : assembly.GetName().Version?.ToString() ?? "1.0.0";
    }

    private async Task ShowRestartDialogAsync()
    {
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = Localizer.Get("Settings_RestartTitle"),
            Content = Localizer.Get("Settings_RestartBody"),
            PrimaryButtonText = Localizer.Get("Settings_RestartNow"),
            CloseButtonText = Localizer.Get("Settings_RestartLater"),
            DefaultButton = ContentDialogButton.Primary,
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
            RestartApp();
    }

    private static void RestartApp()
    {
        try
        {
            var exe = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(exe))
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(exe) { UseShellExecute = true });
        }
        catch (Exception)
        {
        }

        Application.Current.Exit();
    }
}
