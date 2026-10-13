using AlienFXControlCenter.Services;
using AlienFXControlCenter.Services.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AlienFXControlCenter.Pages;

public sealed partial class KeyCaptureDialog : ContentDialog
{
    private readonly bool _capture;
    private readonly Guid? _ownId;
    private bool _listening;

    public KeyCaptureDialog(bool capture, string initialName, Guid? ownId = null, bool nameEditable = true)
    {
        InitializeComponent();

        _capture = capture;
        _ownId = ownId;

        Title = Localizer.Get(capture ? "Keys_CaptureTitle" : "Keys_RenameTitle");
        PrimaryButtonText = Localizer.Get("Dialog_OK");
        CloseButtonText = Localizer.Get("Dialog_Cancel");
        DefaultButton = ContentDialogButton.Primary;

        NameBox.Header = Localizer.Get("Keys_KeyNameHeader");
        NameBox.PlaceholderText = Localizer.Get("Keys_KeyNamePlaceholder");
        NameBox.Text = initialName;
        NameBox.IsReadOnly = !nameEditable;

        PrimaryButtonClick += OnPrimaryButtonClick;

        if (capture)
        {
            HintText.Text = Localizer.Get("Keys_CaptureHint");
            CaptureText.Text = Localizer.Get("Keys_CaptureWaiting");
            IsPrimaryButtonEnabled = false;
        }
        else
        {
            HintText.Visibility = Visibility.Collapsed;
            CaptureBorder.Visibility = Visibility.Collapsed;
        }

        Opened += OnOpened;
        Closed += OnClosed;
    }

    public KeySignature? Signature { get; private set; }

    /// <summary>An existing key with the same signature, if any (used to merge duplicates).</summary>
    public NamedKey? Duplicate { get; private set; }

    public string KeyName => NameBox.Text.Trim();

    private void OnOpened(ContentDialog sender, ContentDialogOpenedEventArgs args)
    {
        // Don't let the key being registered fire its own old action.
        KeyActionDispatcher.Current.Suspend();

        if (!_capture)
        {
            NameBox.Focus(FocusState.Programmatic);
            NameBox.SelectAll();
            return;
        }

        InputService.Current.Captured += OnCaptured;
        _listening = true;
        NameBox.Focus(FocusState.Programmatic);
    }

    private void OnClosed(ContentDialog sender, ContentDialogClosedEventArgs args)
    {
        StopListening();
        KeyActionDispatcher.Current.Resume();
    }

    private void StopListening()
    {
        if (!_listening)
            return;

        InputService.Current.Captured -= OnCaptured;
        _listening = false;
    }

    private void OnCaptured(InputEventInfo info)
    {
        var signature = KeyRecognizer.Identify(info);
        if (signature is null)
            return;

        Signature = signature;
        StopListening();
        CaptureText.Text = signature.Describe();

        if (string.IsNullOrWhiteSpace(NameBox.Text))
        {
            NameBox.Text = signature.Kind == KeySignatureKind.Keyboard
                ? NativeInput.VkName((uint)signature.VKey)
                : "Custom";
        }

        Duplicate = KeyStore.FindBySignature(signature);
        if (Duplicate is not null && Duplicate.Id != _ownId)
        {
            WarnBar.Message = Localizer.Format("Keys_DuplicateWarn", Duplicate.Name);
            WarnBar.Severity = InfoBarSeverity.Warning;
            WarnBar.IsOpen = true;
            NameBox.Text = Duplicate.Name;
        }

        IsPrimaryButtonEnabled = true;
        NameBox.Focus(FocusState.Programmatic);
        NameBox.SelectAll();
    }

    private void OnPrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        if (_capture && Signature is null)
        {
            args.Cancel = true;
            return;
        }

        if (string.IsNullOrWhiteSpace(NameBox.Text))
        {
            args.Cancel = true;
            WarnBar.Message = Localizer.Get("Keys_NeedName");
            WarnBar.Severity = InfoBarSeverity.Error;
            WarnBar.IsOpen = true;
        }
    }
}
