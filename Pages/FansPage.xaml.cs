using AlienFXControlCenter.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AlienFXControlCenter.Pages;

public sealed partial class FansPage : Page
{
    public FansPage()
    {
        InitializeComponent();
        Loaded += FansPage_Loaded;
    }

    public FansViewModel ViewModel { get; } = new();

    private async void FansPage_Loaded(object sender, RoutedEventArgs e) => await ViewModel.LoadAsync();

    private async void Refresh_Click(object sender, RoutedEventArgs e) => await ViewModel.LoadAsync();

    private async void Apply_Click(object sender, RoutedEventArgs e) => await ViewModel.ApplyAsync();

    private async void Unlock_Click(object sender, RoutedEventArgs e) => await ViewModel.UnlockAsync();
}
