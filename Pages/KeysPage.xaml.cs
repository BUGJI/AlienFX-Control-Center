using System.Text;
using AlienFXControlCenter.Models;
using AlienFXControlCenter.Services;
using AlienFXControlCenter.Services.Input;
using AlienFXControlCenter.ViewModels;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace AlienFXControlCenter.Pages;

public sealed partial class KeysPage : Page
{
    private const int TestHotkeyId = 0x4B01;
    private const int MaxLines = 500;

    private readonly List<string> _lines = new();
    private DispatcherQueueTimer? _timer;
    private bool _loading;
    private bool _dirty;
    private int _deviceCount;
    private bool _hotkeyRegistered;

    public KeysPage()
    {
        InitializeComponent();
        Loaded += KeysPage_Loaded;
        Unloaded += KeysPage_Unloaded;
    }

    public KeysViewModel ViewModel { get; } = new();

    private void KeysPage_Loaded(object sender, RoutedEventArgs e)
    {
        var service = InputService.Current;

        service.Captured -= OnCaptured;
        service.Captured += OnCaptured;

        _loading = true;
        RawToggle.IsOn = service.RawInputActive;
        HookToggle.IsOn = service.HookActive;
        _loading = false;

        _timer ??= CreateTimer();
        _timer.Start();

        ViewModel.Load();
        RefreshDevices();
    }

    private void KeysPage_Unloaded(object sender, RoutedEventArgs e)
    {
        _timer?.Stop();
        InputService.Current.Captured -= OnCaptured;
    }

    private DispatcherQueueTimer CreateTimer()
    {
        var timer = DispatcherQueue.CreateTimer();
        timer.Interval = TimeSpan.FromMilliseconds(150);
        timer.IsRepeating = true;
        timer.Tick += (_, _) => Flush();
        return timer;
    }

    private void OnCaptured(InputEventInfo info)
    {
        if (PauseBox.IsChecked == true)
            return;

        if (HideNormalBox.IsChecked == true && IsNormalTypingKey(info))
            return;

        var note = string.Empty;
        var signature = KeyRecognizer.Identify(info);
        if (signature is not null)
        {
            var match = KeyRecognizer.Match(KeyStore.Keys, signature);
            if (match is not null)
                note = $"  =>  {match.Name}";
        }

        _lines.Add(info.ToLogLine() + note);
        if (_lines.Count > MaxLines)
            _lines.RemoveRange(0, _lines.Count - MaxLines);

        _dirty = true;
    }

    private void Flush()
    {
        if (!_dirty)
            return;

        // Never clobber an active selection; wait until the user is done.
        if (LogBox.FocusState != FocusState.Unfocused)
            return;

        _dirty = false;
        LogBox.Text = BuildText(newestFirst: true);
    }

    private string BuildText(bool newestFirst)
    {
        var lines = newestFirst ? Enumerable.Reverse(_lines) : _lines;
        var sb = new StringBuilder();
        foreach (var line in lines)
            sb.Append(line).Append("\r\n");
        return sb.ToString();
    }

    private void RawToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_loading)
            return;

        InputService.Current.SetRawInput(RawToggle.IsOn);
        UpdateStatus();
    }

    private void HookToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_loading)
            return;

        InputService.Current.SetHook(HookToggle.IsOn);
        UpdateStatus();
    }

    private async void Register_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new KeyCaptureDialog(capture: true, initialName: string.Empty) { XamlRoot = XamlRoot };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary || dialog.Signature is null)
            return;

        if (dialog.Duplicate is { } duplicate)
        {
            // Signature already registered — reuse the existing entry with the new name.
            ViewModel.RenameKey(duplicate, dialog.KeyName);
            ViewModel.Status = Localizer.Format("Keys_KeyUpdated", dialog.KeyName);
            return;
        }

        ViewModel.AddKey(new NamedKey { Name = dialog.KeyName, Signature = dialog.Signature });
        ViewModel.Status = Localizer.Format("Keys_KeySaved", dialog.KeyName);
    }

    private async void Rename_Click(object sender, RoutedEventArgs e)
    {
        if (KeyFrom(sender) is not { } key)
            return;

        var dialog = new KeyCaptureDialog(capture: false, initialName: key.Name, ownId: key.Id) { XamlRoot = XamlRoot };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
            return;

        if (KeyStore.NameExists(dialog.KeyName, key.Id))
        {
            ViewModel.Status = Localizer.Get("Keys_NameExists");
            return;
        }

        ViewModel.RenameKey(key, dialog.KeyName);
        ViewModel.Status = Localizer.Format("Keys_Renamed", dialog.KeyName);
    }

    private async void Recapture_Click(object sender, RoutedEventArgs e)
    {
        if (KeyFrom(sender) is not { } key)
            return;

        var dialog = new KeyCaptureDialog(capture: true, initialName: key.Name, ownId: key.Id) { XamlRoot = XamlRoot };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary || dialog.Signature is null)
            return;

        if (dialog.Duplicate is { } duplicate && duplicate.Id != key.Id)
        {
            ViewModel.DeleteKey(key);
            ViewModel.RenameKey(duplicate, dialog.KeyName);
        }
        else
        {
            ViewModel.Recapture(key, dialog.Signature);
            if (!string.Equals(key.Name, dialog.KeyName, StringComparison.Ordinal))
                ViewModel.RenameKey(key, dialog.KeyName);
        }

        ViewModel.Status = Localizer.Format("Keys_Recaptured", dialog.KeyName);
    }

    private async void Bind_Click(object sender, RoutedEventArgs e)
    {
        if (KeyFrom(sender) is not { } key)
            return;

        var templates = TemplateStore.All();
        var combo = new ComboBox { DisplayMemberPath = "Label", MinWidth = 300, HorizontalAlignment = HorizontalAlignment.Stretch };
        combo.Items.Add(new TemplateOption(Localizer.Get("Keys_ActionNone"), null));
        foreach (var template in templates)
            combo.Items.Add(new TemplateOption(template.Name, template));

        var currentIndex = 0;
        if (key.Action is { Kind: KeyActionKind.ApplyTemplate } action
            && Guid.TryParse(action.TemplateId, out var id))
        {
            for (var i = 1; i < combo.Items.Count; i++)
            {
                if (combo.Items[i] is TemplateOption { Template: { } t } && t.Id == id)
                {
                    currentIndex = i;
                    break;
                }
            }
        }

        combo.SelectedIndex = currentIndex;

        var panel = new StackPanel { Spacing = 10, MinWidth = 320 };
        panel.Children.Add(new TextBlock
        {
            Text = Localizer.Format("Keys_BindHeader", key.Name),
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.8,
        });
        panel.Children.Add(combo);

        if (key.IsTypingKey)
        {
            panel.Children.Add(new InfoBar
            {
                IsOpen = true,
                IsClosable = false,
                Severity = InfoBarSeverity.Warning,
                Message = Localizer.Get("Keys_TypingWarn"),
            });
        }

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = Localizer.Get("Keys_BindTitle"),
            Content = panel,
            PrimaryButtonText = Localizer.Get("Dialog_OK"),
            CloseButtonText = Localizer.Get("Dialog_Cancel"),
            DefaultButton = ContentDialogButton.Primary,
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
            return;

        if (combo.SelectedItem is TemplateOption { Template: { } selected })
        {
            ViewModel.SetAction(key, new KeyAction
            {
                Kind = KeyActionKind.ApplyTemplate,
                TemplateId = selected.Id.ToString(),
                TemplateName = selected.Name,
            });
            ViewModel.Status = Localizer.Format("Keys_Bound", key.Name, selected.Name);
        }
        else
        {
            ViewModel.SetAction(key, null);
            ViewModel.Status = Localizer.Format("Keys_Unbound", key.Name);
        }
    }

    private async void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (KeyFrom(sender) is not { } key)
            return;

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = Localizer.Get("Keys_DeleteTitle"),
            Content = Localizer.Format("Keys_DeleteBody", key.Name),
            PrimaryButtonText = Localizer.Get("Keys_DeleteAction.Content"),
            CloseButtonText = Localizer.Get("Dialog_Cancel"),
            DefaultButton = ContentDialogButton.Close,
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
            return;

        ViewModel.DeleteKey(key);
        ViewModel.Status = Localizer.Format("Keys_Deleted", key.Name);
    }

    private void Enabled_Changed(object sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox { Tag: NamedKey key } box)
            return;

        var value = box.IsChecked == true;
        if (key.Enabled == value)
            return;

        key.Enabled = value;
        KeyStore.Save();
        ViewModel.RefreshEnabled(key);
    }

    private async void Reset_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = Localizer.Get("Keys_ResetTitle"),
            Content = Localizer.Get("Keys_ResetBody"),
            PrimaryButtonText = Localizer.Get("Keys_ResetDefaults.Content"),
            CloseButtonText = Localizer.Get("Dialog_Cancel"),
            DefaultButton = ContentDialogButton.Close,
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
            return;

        ViewModel.ResetDefaults();
        ViewModel.Status = Localizer.Get("Keys_ResetDone");
    }

    private void Search_TextChanged(object sender, TextChangedEventArgs e)
        => ViewModel.SearchText = SearchBox.Text;

    private void Filter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        => ViewModel.FilterIndex = FilterBox.SelectedIndex;

    private void TestHotkey_Click(object sender, RoutedEventArgs e)
    {
        if (!_hotkeyRegistered)
        {
            _hotkeyRegistered = InputService.Current.RegisterHotKey(
                TestHotkeyId, NativeInput.MOD_CONTROL | NativeInput.MOD_ALT, 0x4B);
            DiagStatusText.Text = Localizer.Get(_hotkeyRegistered ? "Keys_HotkeyRegistered" : "Keys_HotkeyFailed");
        }
        else
        {
            InputService.Current.UnregisterHotKey(TestHotkeyId);
            _hotkeyRegistered = false;
            DiagStatusText.Text = Localizer.Get("Keys_HotkeyUnregistered");
        }
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        _lines.Clear();
        _dirty = false;
        LogBox.Text = string.Empty;
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var package = new DataPackage();
            package.SetText(BuildText(newestFirst: false));
            Clipboard.SetContent(package);
            DiagStatusText.Text = Localizer.Get("Keys_Copied");
        }
        catch (Exception ex)
        {
            DiagStatusText.Text = ex.Message;
        }
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var picker = new FileSavePicker
            {
                SuggestedFileName = $"alienfx-keys-{DateTime.Now:yyyyMMdd-HHmmss}",
            };
            picker.FileTypeChoices.Add("Log", new List<string> { ".txt" });
            InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(App.MainWindow!));

            var file = await picker.PickSaveFileAsync();
            if (file is null)
                return;

            await File.WriteAllTextAsync(file.Path, BuildText(newestFirst: false));
            DiagStatusText.Text = Localizer.Format("Keys_Saved", file.Path);
        }
        catch (Exception ex)
        {
            DiagStatusText.Text = ex.Message;
        }
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => RefreshDevices();

    private void RefreshDevices()
    {
        _deviceCount = InputSink.EnumerateDevices().Count();
        UpdateStatus();
    }

    private void UpdateStatus()
    {
        DiagStatusText.Text = Localizer.Format(
            "Keys_Status", InputService.Current.RawInputActive, InputService.Current.HookActive, _deviceCount);
    }

    private static NamedKey? KeyFrom(object sender) => (sender as FrameworkElement)?.Tag as NamedKey;

    private static bool IsNormalTypingKey(InputEventInfo info)
    {
        if (info.Channel is not (InputChannel.RawInput or InputChannel.KeyboardHook))
            return false;

        return KeyRecognizer.IsTypingKey(info.VKey);
    }

    private sealed record TemplateOption(string Label, LightingTemplate? Template);
}
