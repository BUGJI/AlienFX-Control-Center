namespace AlienFXControlCenter.Services.Input;

public enum InputChannel
{
    RawInput,
    KeyboardHook,
    HotKey,
    Device,
}

public sealed class InputEventInfo
{
    public DateTimeOffset Time { get; set; } = DateTimeOffset.Now;
    public InputChannel Channel { get; set; }
    public bool IsDown { get; set; }
    public int VKey { get; set; } = -1;
    public int ScanCode { get; set; } = -1;
    public ushort UsagePage { get; set; }
    public ushort Usage { get; set; }
    public int VendorId { get; set; }
    public int ProductId { get; set; }
    public string Report { get; set; } = string.Empty;
    public string DeviceName { get; set; } = string.Empty;
    public string Note { get; set; } = string.Empty;

    public string ChannelText => Channel switch
    {
        InputChannel.RawInput => "RAW",
        InputChannel.KeyboardHook => "HOOK",
        InputChannel.HotKey => "HOTKEY",
        InputChannel.Device => "DEVICE",
        _ => Channel.ToString(),
    };

    public string UsageText => $"0x{UsagePage:X4}:0x{Usage:X4}";

    public string ToLogLine()
    {
        var time = Time.ToString("HH:mm:ss.fff");
        var channel = ChannelText.PadRight(6);
        var state = IsDown ? "DOWN" : "UP";

        return Channel switch
        {
            InputChannel.KeyboardHook =>
                $"{time}  {channel} VK 0x{VKey:X2} ({NativeInput.VkName((uint)VKey)}) scan 0x{ScanCode:X2} {state}",

            InputChannel.RawInput when VKey >= 0 =>
                $"{time}  {channel} VK 0x{VKey:X2} ({NativeInput.VkName((uint)VKey)}) make 0x{ScanCode:X2} {state}",

            InputChannel.RawInput =>
                $"{time}  {channel} {Report,-11} [{UsageText} VID_{VendorId:X4} PID_{ProductId:X4}]  {DeviceName}",

            _ =>
                $"{time}  {channel} {Note}  {DeviceName}",
        };
    }

    public override string ToString() => ToLogLine();
}

public sealed class RawDeviceInfo
{
    public IntPtr Handle { get; set; }
    public uint Type { get; set; }
    public int VendorId { get; set; }
    public int ProductId { get; set; }
    public ushort UsagePage { get; set; }
    public ushort Usage { get; set; }
    public string Name { get; set; } = string.Empty;

    public string TypeText => Type switch
    {
        0 => "Mouse",
        1 => "Keyboard",
        2 => "HID",
        _ => $"Type {Type}",
    };

    public string UsageText => $"0x{UsagePage:X4}:0x{Usage:X4}";

    public override string ToString()
        => $"{TypeText,-8}  VID_{VendorId:X4} PID_{ProductId:X4}  {UsageText}  {Name}";
}
