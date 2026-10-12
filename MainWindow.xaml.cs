using AlienFXControlCenter.Pages;
using AlienFXControlCenter.Services;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace AlienFXControlCenter;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        try
        {
            InitializeComponent();

            ExtendsContentIntoTitleBar = true;
            SetTitleBar(AppTitleBar);
            AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
            AppWindow.SetIcon("Assets/AppIcon.ico");

            FitToWorkArea();

            var title = Localizer.Get("App_Title");
            Title = title;
            AppTitleBar.Title = title;

            if (Content is FrameworkElement root)
                root.ActualThemeChanged += (_, _) => UpdateTitleBarColors();

            SettingsService.Current.Changed += (_, _) => ApplyTheme();

            ApplyTheme();
            NavView.SelectedItem = LightsItem;
        }
        catch (Exception ex)
        {
            Log.Write($"MainWindow init failed: {ex}");
            throw;
        }
    }

    private void FitToWorkArea()
    {
        var area = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary).WorkArea;
        var width = (int)(area.Width * 0.94);
        var height = (int)(area.Height * 0.94);
        AppWindow.MoveAndResize(new Windows.Graphics.RectInt32(
            area.X + (area.Width - width) / 2,
            area.Y + (area.Height - height) / 2,
            width,
            height));
    }

    private void ApplyTheme()
    {
        if (Content is FrameworkElement root)
        {
            root.RequestedTheme = SettingsService.Current.Value.Theme switch
            {
                "light" => ElementTheme.Light,
                "dark" => ElementTheme.Dark,
                _ => ElementTheme.Default,
            };
        }

        UpdateTitleBarColors();
    }

    private void UpdateTitleBarColors()
    {
        var isDark = (Content as FrameworkElement)?.ActualTheme == ElementTheme.Dark;
        var foreground = isDark ? Colors.White : Colors.Black;

        var titleBar = AppWindow.TitleBar;
        titleBar.ButtonForegroundColor = foreground;
        titleBar.ButtonHoverForegroundColor = foreground;
        titleBar.ButtonPressedForegroundColor = foreground;
    }

    private void TitleBar_PaneToggleRequested(TitleBar sender, object args)
    {
        NavView.IsPaneOpen = !NavView.IsPaneOpen;
    }

    private void TitleBar_BackRequested(TitleBar sender, object args)
    {
        if (NavFrame.CanGoBack)
            NavFrame.GoBack();
    }

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.IsSettingsSelected)
        {
            NavFrame.Navigate(typeof(SettingsPage));
        }
        else if (args.SelectedItem is NavigationViewItem item)
        {
            switch (item.Tag)
            {
                case "lights":
                    NavFrame.Navigate(typeof(LightsPage));
                    break;
                case "fans":
                    NavFrame.Navigate(typeof(FansPage));
                    break;
            }
        }
    }
}
