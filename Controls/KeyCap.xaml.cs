using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace AlienFXControlCenter.Controls;

/// <summary>
/// A single keycap-shaped chip used by the key registration table. It shows the
/// key label and, beneath it, the bound action as a small badge.
/// </summary>
public sealed partial class KeyCap : UserControl
{
    private const string MissingGlyph = "\uE783";
    private const string WarningGlyph = "\uE7BA";

    public KeyCap()
    {
        InitializeComponent();
        UpdateVisual();
    }

    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(
        nameof(Label), typeof(string), typeof(KeyCap), new PropertyMetadata(string.Empty, OnVisualChanged));

    public static readonly DependencyProperty BadgeProperty = DependencyProperty.Register(
        nameof(Badge), typeof(string), typeof(KeyCap), new PropertyMetadata(string.Empty, OnVisualChanged));

    public static readonly DependencyProperty IsBoundProperty = DependencyProperty.Register(
        nameof(IsBound), typeof(bool), typeof(KeyCap), new PropertyMetadata(false, OnVisualChanged));

    public static readonly DependencyProperty IsMissingProperty = DependencyProperty.Register(
        nameof(IsMissing), typeof(bool), typeof(KeyCap), new PropertyMetadata(false, OnVisualChanged));

    public static readonly DependencyProperty ShowWarningProperty = DependencyProperty.Register(
        nameof(ShowWarning), typeof(bool), typeof(KeyCap), new PropertyMetadata(false, OnVisualChanged));

    public static readonly DependencyProperty DimmedProperty = DependencyProperty.Register(
        nameof(Dimmed), typeof(bool), typeof(KeyCap), new PropertyMetadata(false, OnVisualChanged));

    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public string Badge
    {
        get => (string)GetValue(BadgeProperty);
        set => SetValue(BadgeProperty, value);
    }

    public bool IsBound
    {
        get => (bool)GetValue(IsBoundProperty);
        set => SetValue(IsBoundProperty, value);
    }

    public bool IsMissing
    {
        get => (bool)GetValue(IsMissingProperty);
        set => SetValue(IsMissingProperty, value);
    }

    public bool ShowWarning
    {
        get => (bool)GetValue(ShowWarningProperty);
        set => SetValue(ShowWarningProperty, value);
    }

    public bool Dimmed
    {
        get => (bool)GetValue(DimmedProperty);
        set => SetValue(DimmedProperty, value);
    }

    private static void OnVisualChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((KeyCap)d).UpdateVisual();

    private void UpdateVisual()
    {
        if (LabelText is null)
            return;

        LabelText.Text = Label;

        var hasBadge = !string.IsNullOrWhiteSpace(Badge);
        BadgeHost.Visibility = hasBadge ? Visibility.Visible : Visibility.Collapsed;
        BadgeText.Text = Badge;

        WarnIcon.Visibility = IsMissing || ShowWarning ? Visibility.Visible : Visibility.Collapsed;
        WarnIcon.Glyph = IsMissing ? MissingGlyph : WarningGlyph;

        Opacity = Dimmed ? 0.45 : 1.0;

        if (IsMissing)
        {
            Cap.BorderBrush = Res("SystemFillColorCautionBrush");
            BadgeHost.Background = Res("SystemFillColorCautionBackgroundBrush");
            BadgeText.Foreground = Res("SystemFillColorCautionBrush");
        }
        else if (IsBound)
        {
            Cap.BorderBrush = Res("AccentFillColorDefaultBrush");
            BadgeHost.Background = Res("AccentFillColorDefaultBrush");
            BadgeText.Foreground = Res("TextOnAccentFillColorPrimaryBrush");
        }
        else
        {
            Cap.BorderBrush = Res("CardStrokeColorDefaultBrush");
            BadgeHost.Background = Res("ControlFillColorSecondaryBrush");
            BadgeText.Foreground = Res("TextFillColorSecondaryBrush");
        }
    }

    private static Brush? Res(string key)
        => Application.Current.Resources.TryGetValue(key, out var value) ? value as Brush : null;
}
