using AlienFXControlCenter.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AlienFXControlCenter.Helpers;

public static class DialogHelper
{
    public static async Task ShowErrorAsync(XamlRoot? xamlRoot, string message)
    {
        if (xamlRoot is null)
            return;

        var dialog = new ContentDialog
        {
            XamlRoot = xamlRoot,
            Title = Localizer.Get("Dialog_ErrorTitle"),
            Content = message,
            CloseButtonText = Localizer.Get("Dialog_Close"),
        };

        try
        {
            await dialog.ShowAsync();
        }
        catch (Exception)
        {
        }
    }

    public static async Task<string?> PromptTextAsync(XamlRoot? xamlRoot, string title, string initial, string placeholder)
    {
        if (xamlRoot is null)
            return null;

        var box = new TextBox
        {
            Text = initial,
            PlaceholderText = placeholder,
        };

        var dialog = new ContentDialog
        {
            XamlRoot = xamlRoot,
            Title = title,
            Content = box,
            PrimaryButtonText = Localizer.Get("Dialog_OK"),
            CloseButtonText = Localizer.Get("Dialog_Cancel"),
            DefaultButton = ContentDialogButton.Primary,
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
            return null;

        var text = box.Text?.Trim();
        return string.IsNullOrEmpty(text) ? null : text;
    }
}
