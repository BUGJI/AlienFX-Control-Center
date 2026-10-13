namespace AlienFXControlCenter.Services.Input;

public sealed class InputService
{
    private readonly HashSet<int> _keyboardDown = new();
    private readonly HashSet<string> _hidDownDevices = new(StringComparer.Ordinal);
    private InputSink? _sink;
    private bool _started;

    public static InputService Current { get; } = new();

    /// <summary>Raised for every raw captured event (diagnostics, learning).</summary>
    public event Action<InputEventInfo>? Captured;

    /// <summary>Raised once per press of a recognized (named) key.</summary>
    public event Action<NamedKey>? Recognized;

    public bool RawInputActive => _sink?.RawInputActive == true;

    public bool HookActive => _sink?.HookActive == true;

    public void Start(IntPtr hwnd)
    {
        if (_started)
            return;

        _started = true;
        _sink = new InputSink(hwnd);
        _sink.Captured += OnCaptured;
        _sink.RegisterRawInput();
        _sink.InstallHook();
        Log.Write("InputService started.");
    }

    public void Stop()
    {
        if (_sink is not null)
        {
            _sink.Captured -= OnCaptured;
            _sink.Dispose();
            _sink = null;
        }

        _started = false;
    }

    public void SetRawInput(bool enabled)
    {
        if (enabled)
            _sink?.RegisterRawInput();
        else
            _sink?.UnregisterRawInput();
    }

    public void SetHook(bool enabled)
    {
        if (enabled)
            _sink?.InstallHook();
        else
            _sink?.UninstallHook();
    }

    public bool RegisterHotKey(int id, uint modifiers, uint vk)
        => _sink?.RegisterHotKey(id, modifiers, vk) ?? false;

    public void UnregisterHotKey(int id) => _sink?.UnregisterHotKey(id);

    private void OnCaptured(InputEventInfo info)
    {
        var edge = UpdateState(info);
        Captured?.Invoke(info);

        if (!edge)
            return;

        var signature = KeyRecognizer.Identify(info);
        if (signature is null)
            return;

        var match = KeyRecognizer.Match(KeyStore.Keys, signature);
        if (match is null)
            return;

        Recognized?.Invoke(match);
    }

    // Returns true only on a fresh press edge (ignores key repeat and release).
    private bool UpdateState(InputEventInfo info)
    {
        if (info.Channel == InputChannel.KeyboardHook && info.VKey >= 0)
        {
            if (info.IsDown)
                return _keyboardDown.Add(info.VKey);

            _keyboardDown.Remove(info.VKey);
            return false;
        }

        if (info.Channel == InputChannel.RawInput && info.VKey < 0)
        {
            // Track the down state per device so concurrent HID devices (e.g. a
            // keyboard and a mouse) don't clobber each other's press edge.
            var pressed = KeyRecognizer.IsHidPress(info.Report);
            if (!pressed)
            {
                _hidDownDevices.Remove(info.DeviceName);
                return false;
            }

            return _hidDownDevices.Add(info.DeviceName);
        }

        return false;
    }
}
