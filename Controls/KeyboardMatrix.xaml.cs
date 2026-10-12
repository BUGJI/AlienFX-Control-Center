using AlienFXControlCenter.Models;
using AlienFXControlCenter.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace AlienFXControlCenter.Controls;

public sealed partial class KeyboardMatrix : UserControl
{
    private const double FramePadding = 28;
    private const double MaxUnit = 46;
    private const double MinUnit = 22;

    private readonly List<KeyCell> _cells = new();
    private LightDevice? _device;
    private KeyLayout? _layout;
    private double _unit = MaxUnit;
    private bool _suppress;

    public KeyboardMatrix()
    {
        InitializeComponent();
        SizeChanged += OnSizeChanged;
    }

    public event EventHandler? SelectionChanged;

    public IReadOnlyList<Light> SelectedLights =>
        _cells.Where(c => c.Button.IsChecked == true).Select(c => c.Light).ToList();

    public void SetDevice(LightDevice? device, KeyLayout? layout)
    {
        _device = device;
        _layout = layout;
        Rebuild();
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_layout is null || _layout.Columns == 0)
            return;

        var unit = Math.Clamp((e.NewSize.Width - FramePadding) / _layout.Columns, MinUnit, MaxUnit);
        if (Math.Abs(unit - _unit) < 0.5)
            return;

        _unit = unit;
        Rebuild();
    }

    private void Rebuild()
    {
        _suppress = true;
        _cells.Clear();
        CanvasHost.Children.Clear();
        OtherHost.Children.Clear();

        if (_device is not null && _layout is { Keys.Count: > 0 })
        {
            var available = ActualWidth > 0 ? ActualWidth - FramePadding : _layout.Columns * MaxUnit;
            _unit = Math.Clamp(available / _layout.Columns, MinUnit, MaxUnit);

            var gap = Math.Max(2, Math.Round(_unit * 0.09));
            CanvasHost.Width = _layout.Columns * _unit;
            CanvasHost.Height = _layout.Rows * _unit;

            var remainingById = new Dictionary<int, Light>();
            var remainingByName = new Dictionary<string, Light>(StringComparer.OrdinalIgnoreCase);
            foreach (var light in _device.Lights)
            {
                remainingById[light.Id] = light;
                remainingByName[light.Name] = light;
            }

            foreach (var placement in _layout.Keys)
            {
                Light? light = null;
                if (placement.LightId is int id && remainingById.TryGetValue(id, out var byId))
                    light = byId;
                else if (remainingByName.TryGetValue(placement.Name, out var byName))
                    light = byName;

                if (light is null)
                    continue;

                remainingById.Remove(light.Id);
                remainingByName.Remove(light.Name);
                var height = placement.Height * _unit - gap;
                var top = placement.Row * _unit + (_unit * (1 - placement.Height)) / 2;
                AddCell(light, placement.Col * _unit, top,
                    placement.Width * _unit - gap, height, onCanvas: true);
            }

            foreach (var light in remainingById.Values.OrderBy(l => l.Id))
                AddCell(light, 0, 0, Math.Round(_unit * 1.5), _unit - gap, onCanvas: false);

            OtherSection.Visibility = remainingById.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        else
        {
            CanvasHost.Width = 0;
            CanvasHost.Height = 0;
            OtherSection.Visibility = Visibility.Collapsed;
        }

        _suppress = false;
        UpdateInfo();
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    public void TintSelection(Color color)
    {
        var brush = CreateFill(color);
        var foreground = ContrastForeground(color);
        foreach (var cell in _cells)
        {
            var selected = cell.Button.IsChecked == true;
            cell.Button.Background = selected ? brush : null;
            cell.Button.Foreground = selected ? foreground : null;
        }
    }

    public void TintAll(Color color)
    {
        var brush = CreateFill(color);
        var foreground = ContrastForeground(color);
        foreach (var cell in _cells)
        {
            cell.Button.Background = brush;
            cell.Button.Foreground = foreground;
        }
    }

    public void ClearTint()
    {
        foreach (var cell in _cells)
        {
            cell.Button.Background = null;
            cell.Button.Foreground = null;
        }
    }

    private void AddCell(Light light, double left, double top, double width, double height, bool onCanvas)
    {
        var button = new ToggleButton
        {
            Content = KeyboardLayout.Label(light.Name),
            FontSize = Math.Clamp(Math.Round(_unit * 0.22), 8, 13),
            Padding = new Thickness(0),
            Width = width,
            Height = height,
            CornerRadius = new CornerRadius(4),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Tag = light,
        };
        button.Checked += Cell_Toggled;
        button.Unchecked += Cell_Toggled;
        ToolTipService.SetToolTip(button, light.Name);

        if (onCanvas)
        {
            Canvas.SetLeft(button, left);
            Canvas.SetTop(button, top);
        }
        else
        {
            button.HorizontalAlignment = HorizontalAlignment.Stretch;
            button.VerticalAlignment = VerticalAlignment.Stretch;
        }

        (onCanvas ? (Panel)CanvasHost : OtherHost).Children.Add(button);
        _cells.Add(new KeyCell(light, button));
    }

    private void Cell_Toggled(object sender, RoutedEventArgs e)
    {
        if (_suppress)
            return;

        UpdateInfo();
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    private void SelectAll_Click(object sender, RoutedEventArgs e)
    {
        _suppress = true;
        foreach (var cell in _cells)
            cell.Button.IsChecked = true;
        _suppress = false;
        UpdateInfo();
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        _suppress = true;
        foreach (var cell in _cells)
            cell.Button.IsChecked = false;
        _suppress = false;
        UpdateInfo();
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    private void UpdateInfo()
    {
        var count = _cells.Count(c => c.Button.IsChecked == true);
        SelectionInfo.Text = count == 0
            ? Localizer.Get("Matrix_NoSelection")
            : Localizer.Format("Matrix_SelectedCount", count);
    }

    private static SolidColorBrush CreateFill(Color color)
        => new(Color.FromArgb(255, color.R, color.G, color.B));

    private static SolidColorBrush ContrastForeground(Color color)
    {
        var luminance = (0.299 * color.R + 0.587 * color.G + 0.114 * color.B) / 255.0;
        return new SolidColorBrush(luminance > 0.55 ? Microsoft.UI.Colors.Black : Microsoft.UI.Colors.White);
    }

    private readonly record struct KeyCell(Light Light, ToggleButton Button);
}
