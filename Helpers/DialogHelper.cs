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
}
