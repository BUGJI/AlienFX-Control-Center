using AlienFXControlCenter.Controls;
using AlienFXControlCenter.Helpers;
using AlienFXControlCenter.Models;
using AlienFXControlCenter.Services;
using AlienFXControlCenter.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace AlienFXControlCenter.Pages;

public sealed partial class LightsPage : Page
{
    private static readonly Color[] PaletteColors =
    {
        Color.FromArgb(255, 0xFF, 0x00, 0x00),
        Color.FromArgb(255, 0xFF, 0x7F, 0x00),
        Color.FromArgb(255, 0xFF, 0xD4, 0x00),
        Color.FromArgb(255, 0x00, 0xC8, 0x53),
        Color.FromArgb(255, 0x00, 0xE5, 0xFF),
        Color.FromArgb(255, 0x29, 0x62, 0xFF),
        Color.FromArgb(255, 0x8E, 0x24, 0xAA),
        Color.FromArgb(255, 0xFF, 0x40, 0x81),
        Color.FromArgb(255, 0xFF, 0xFF, 0xFF),
    };

    private CancellationTokenSource? _debounce;
    private bool _ready;
    private bool _loading;
    private bool _userTouched;

    public LightsPage()
    {
        InitializeComponent();
        BuildPalette();

        // The ColorPicker raises ColorChanged once with its default color on
        // load; only react to genuine user interaction so the keys are not
        // tinted until the user actually picks a color.
        Picker.AddHandler(PointerPressedEvent, new PointerEventHandler((_, _) => _userTouched = true), true);
        Picker.AddHandler(KeyDownEvent, new KeyEventHandler((_, _) => _userTouched = true), true);

        Loaded += LightsPage_Loaded;
    }

    public LightsViewModel ViewModel { get; } = new();

    private void BuildPalette()
    {
        var stroke = Application.Current.Resources.TryGetValue("CardStrokeColorDefaultBrush", out var value) && value is Brush brush
            ? brush
            : new SolidColorBrush(Microsoft.UI.Colors.Gray);

        foreach (var color in PaletteColors)
        {
            var hex = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
            var button = new Button
            {
                Width = 30,
                Height = 30,
                Padding = new Thickness(0),
                CornerRadius = new CornerRadius(6),
                Background = new SolidColorBrush(color),
                BorderThickness = new Thickness(1),
                BorderBrush = stroke,
                Tag = color,
            };

            ToolTipService.SetToolTip(button, hex);
            AutomationProperties.SetName(button, hex);
            button.Click += (_, _) =>
            {
                _userTouched = true;
                Picker.Color = (Color)button.Tag;
            };
            Palette.Children.Add(button);
        }
    }

    private async void LightsPage_Loaded(object sender, RoutedEventArgs e)
    {
        await ReloadAsync();
        _ready = true;
    }

    private async Task ReloadAsync()
    {
        _loading = true;
        await ViewModel.LoadAsync();
        TargetBox.ItemsSource = ViewModel.Targets;
        TargetBox.SelectedItem = ViewModel.SelectedTarget;
        _loading = false;
        SyncMatrix();
    }

    private void SyncMatrix()
    {
        var device = ViewModel.SelectedTarget?.Device;
        if (device is not null)
        {
            Matrix.SetDevice(device, KeyLayoutStore.Load(device));
            Matrix.Visibility = Visibility.Visible;
        }
        else
        {
            Matrix.SetDevice(null, null);
            Matrix.Visibility = Visibility.Collapsed;
        }
    }

    private async Task ScheduleApplyAsync()
    {
        if (!_ready)
            return;

        _debounce?.Cancel();
        var cts = new CancellationTokenSource();
        _debounce = cts;

        try
        {
            await Task.Delay(180, cts.Token);
            await ApplyColorAsync(Picker.Color, cts.Token);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task ApplyColorAsync(Color color, CancellationToken ct)
    {
        var device = ViewModel.SelectedTarget?.Device;
        var selection = Matrix.SelectedLights;

        if (device is not null && selection.Count > 0)
        {
            await ViewModel.ApplySelectionAsync(selection, color, ct);
            Matrix.TintSelection(color);
        }
        else
        {
            await ViewModel.ApplyAsync(color, CurrentScope(), ct);
            if (device is not null)
                Matrix.TintAll(color);
        }
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e) => await ReloadAsync();

    private async void Export_Click(object sender, RoutedEventArgs e)
    {
        var device = ViewModel.SelectedTarget?.Device;
        if (device is null)
        {
            ViewModel.Status = Localizer.Get("Status_ExportSelectDevice");
            return;
        }

        try
        {
            var layout = KeyLayoutStore.BuildExport(device);
            KeyLayoutStore.Save(device, layout);
            ViewModel.Status = Localizer.Format("Status_ExportDone", KeyLayoutStore.PathFor(device.VendorId, device.ProductId));
        }
        catch (Exception ex)
        {
            ViewModel.Status = Localizer.Format("Status_ExportFailed", ex.Message);
            await DialogHelper.ShowErrorAsync(XamlRoot, ex.Message);
        }
    }

    private async void SaveTemplate_Click(object sender, RoutedEventArgs e)
    {
        var name = await DialogHelper.PromptTextAsync(
            XamlRoot,
            Localizer.Get("Templates_NewTitle"),
            TemplatesViewModel.SuggestName(),
            Localizer.Get("Templates_NamePlaceholder"));

        if (name is null)
            return;

        if (PresetState.IsEmpty)
        {
            ViewModel.Status = Localizer.Get("Templates_NoLiveState");
            return;
        }

        var template = TemplatesViewModel.SaveCurrentState(name);
        ViewModel.Status = Localizer.Format("Templates_Saved", template.Name);
    }

    private void TargetBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || TargetBox.SelectedItem is not DeviceOption option)
            return;

        ViewModel.SelectedTarget = option;
        SyncMatrix();
        _ = ScheduleApplyAsync();
    }

    private async void Matrix_SelectionChanged(object? sender, EventArgs e) => await ScheduleApplyAsync();

    private async void Scope_Changed(object sender, RoutedEventArgs e) => await ScheduleApplyAsync();

    private async void Picker_ColorChanged(ColorPicker sender, ColorChangedEventArgs args)
    {
        if (!_userTouched)
            return;

        await ScheduleApplyAsync();
    }

    private LightScope CurrentScope()
    {
        if (ScopeWasd.IsChecked == true)
            return LightScope.Wasd;
        if (ScopeArrows.IsChecked == true)
            return LightScope.Arrows;
        if (ScopeFKeys.IsChecked == true)
            return LightScope.FunctionKeys;
        return LightScope.All;
    }
}
