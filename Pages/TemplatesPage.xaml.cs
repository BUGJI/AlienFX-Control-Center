using AlienFXControlCenter.Helpers;
using AlienFXControlCenter.Services;
using AlienFXControlCenter.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace AlienFXControlCenter.Pages;

public sealed partial class TemplatesPage : Page
{
    public TemplatesPage()
    {
        InitializeComponent();
        Loaded += TemplatesPage_Loaded;
    }

    public TemplatesViewModel ViewModel { get; } = new();

    private void TemplatesPage_Loaded(object sender, RoutedEventArgs e) => ViewModel.Load();

    private void Refresh_Click(object sender, RoutedEventArgs e) => ViewModel.Load();

    private async void New_Click(object sender, RoutedEventArgs e)
    {
        var name = await DialogHelper.PromptTextAsync(
            XamlRoot,
            Localizer.Get("Templates_NewTitle"),
            TemplatesViewModel.SuggestName(),
            Localizer.Get("Templates_NamePlaceholder"));

        if (name is null)
            return;

        ViewModel.SaveCurrent(name);
    }

    private async void Apply_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedTemplate is not TemplateItem item)
        {
            ViewModel.Status = Localizer.Get("Templates_SelectFirst");
            return;
        }

        await ViewModel.ApplyAsync(item);
    }

    private async void Rename_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedTemplate is not TemplateItem item)
        {
            ViewModel.Status = Localizer.Get("Templates_SelectFirst");
            return;
        }

        var name = await DialogHelper.PromptTextAsync(
            XamlRoot,
            Localizer.Get("Templates_RenameTitle"),
            item.Name,
            Localizer.Get("Templates_NamePlaceholder"));

        if (name is null)
            return;

        ViewModel.Rename(item, name);
    }

    private void Duplicate_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedTemplate is not TemplateItem item)
        {
            ViewModel.Status = Localizer.Get("Templates_SelectFirst");
            return;
        }

        ViewModel.Duplicate(item);
    }

    private async void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedTemplate is not TemplateItem item)
        {
            ViewModel.Status = Localizer.Get("Templates_SelectFirst");
            return;
        }

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = Localizer.Get("Templates_DeleteTitle"),
            Content = Localizer.Format("Templates_DeleteBody", item.Name),
            PrimaryButtonText = Localizer.Get("Templates_Delete.Content"),
            CloseButtonText = Localizer.Get("Dialog_Cancel"),
            DefaultButton = ContentDialogButton.Close,
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            ViewModel.Delete(item);
    }

    private async void Export_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedTemplate is not TemplateItem item)
        {
            ViewModel.Status = Localizer.Get("Templates_SelectFirst");
            return;
        }

        var picker = new FileSavePicker { SuggestedFileName = item.Name };
        picker.FileTypeChoices.Add("JSON", new List<string> { ".json" });
        Initialize(picker);

        var file = await picker.PickSaveFileAsync();
        if (file is null)
            return;

        ViewModel.Export(item, file.Path);
    }

    private async void Import_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FileOpenPicker();
        picker.FileTypeFilter.Add(".json");
        Initialize(picker);

        var file = await picker.PickSingleFileAsync();
        if (file is null)
            return;

        ViewModel.Import(file.Path);
    }

    private static void Initialize(object picker)
    {
        if (App.MainWindow is Window window)
        {
            var hwnd = WindowNative.GetWindowHandle(window);
            InitializeWithWindow.Initialize(picker, hwnd);
        }
    }
}
