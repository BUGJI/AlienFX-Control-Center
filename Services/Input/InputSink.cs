using System.Runtime.InteropServices;

namespace AlienFXControlCenter.Services.Input;

public sealed class InputSink : IDisposable
{
    private const UIntPtr SubclassId = 1;

    private readonly IntPtr _hwnd;
    private readonly NativeInput.SubclassProc _subclassProc;
    private readonly NativeInput.LowLevelKeyboardProc _hookProc;
    private IntPtr _hook;
    private bool _disposed;

    public InputSink(IntPtr hwnd)
    {
        _hwnd = hwnd;
        _subclassProc = SubclassProc;
        _hookProc = HookProc;
        NativeInput.SetWindowSubclass(_hwnd, _subclassProc, SubclassId, IntPtr.Zero);
    }

    public event Action<InputEventInfo>? Captured;

    public bool RawInputActive { get; private set; }

    public bool HookActive { get; private set; }

    public bool RegisterRawInput()
    {
        var devices = RegisteredUsages(
            NativeInput.RIDEV_INPUTSINK | NativeInput.RIDEV_DEVNOTIFY, _hwnd);

        RawInputActive = NativeInput.RegisterRawInputDevices(
            devices, (uint)devices.Length, (uint)Marshal.SizeOf<NativeInput.RAWINPUTDEVICE>());
        Log.Write($"RawInput register: {RawInputActive}");
        return RawInputActive;
    }

    public void UnregisterRawInput()
    {
        var devices = RegisteredUsages(NativeInput.RIDEV_REMOVE, IntPtr.Zero);
        NativeInput.RegisterRawInputDevices(devices, (uint)devices.Length, (uint)Marshal.SizeOf<NativeInput.RAWINPUTDEVICE>());
        RawInputActive = false;
    }

    public bool InstallHook()
    {
        _hook = NativeInput.SetWindowsHookEx(
            NativeInput.WH_KEYBOARD_LL, _hookProc, NativeInput.GetModuleHandle(null), 0);
        HookActive = _hook != IntPtr.Zero;
        Log.Write($"Keyboard hook install: {HookActive} (handle=0x{_hook.ToInt64():X})");
        return HookActive;
    }

    public void UninstallHook()
    {
        if (_hook != IntPtr.Zero)
        {
            NativeInput.UnhookWindowsHookEx(_hook);
            _hook = IntPtr.Zero;
        }

        HookActive = false;
    }

    public bool RegisterHotKey(int id, uint modifiers, uint vk)
    {
        var ok = NativeInput.RegisterHotKey(_hwnd, id, modifiers | NativeInput.MOD_NOREPEAT, vk);
        Log.Write($"RegisterHotKey id={id} mods=0x{modifiers:X} vk=0x{vk:X}: {ok}");
        return ok;
    }

    public void UnregisterHotKey(int id) => NativeInput.UnregisterHotKey(_hwnd, id);

    private NativeInput.RAWINPUTDEVICE[] RegisteredUsages(uint flags, IntPtr target) =>
    [
        new() { usUsagePage = 0x01, usUsage = 0x06, dwFlags = flags, hwndTarget = target },
        new() { usUsagePage = 0x01, usUsage = 0x07, dwFlags = flags, hwndTarget = target },
        new() { usUsagePage = 0x0C, usUsage = 0x01, dwFlags = flags, hwndTarget = target },
    ];

    private IntPtr SubclassProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam, UIntPtr uIdSubclass, IntPtr dwRefData)
    {
        switch (msg)
        {
            case NativeInput.WM_INPUT:
                HandleRawInput(lParam);
                break;
            case NativeInput.WM_INPUT_DEVICE_CHANGE:
                Captured?.Invoke(new InputEventInfo
                {
                    Channel = InputChannel.Device,
                    Note = wParam == (IntPtr)1 ? "arrived" : "removed",
                    DeviceName = DescribeDevice(lParam),
                });
                break;
            case NativeInput.WM_HOTKEY:
                Captured?.Invoke(new InputEventInfo
                {
                    Channel = InputChannel.HotKey,
                    Note = $"id={wParam.ToInt64()}",
                });
                break;
        }

        return NativeInput.DefSubclassProc(hWnd, msg, wParam, lParam);
    }

    private void HandleRawInput(IntPtr hRawInput)
    {
        try
        {
            var size = 0u;
            var headerSize = (uint)Marshal.SizeOf<NativeInput.RAWINPUTHEADER>();
            NativeInput.GetRawInputData(hRawInput, NativeInput.RID_INPUT, IntPtr.Zero, ref size, headerSize);
            if (size == 0)
                return;

            var buffer = Marshal.AllocHGlobal((int)size);
            try
            {
                if (NativeInput.GetRawInputData(hRawInput, NativeInput.RID_INPUT, buffer, ref size, headerSize) != size)
                    return;

                var header = Marshal.PtrToStructure<NativeInput.RAWINPUTHEADER>(buffer);
                var dataPtr = IntPtr.Add(buffer, (int)headerSize);
                var device = DescribeDevice(header.hDevice);

                if (header.dwType == NativeInput.RIM_TYPEKEYBOARD)
                {
                    var kb = Marshal.PtrToStructure<NativeInput.RAWKEYBOARD>(dataPtr);
                    Captured?.Invoke(new InputEventInfo
                    {
                        Channel = InputChannel.RawInput,
                        DeviceName = device,
                        VKey = kb.VKey,
                        ScanCode = kb.MakeCode,
                        IsDown = kb.Message is NativeInput.WM_KEYDOWN or NativeInput.WM_SYSKEYDOWN,
                    });
                }
                else if (header.dwType == NativeInput.RIM_TYPEHID)
                {
                    var hid = Marshal.PtrToStructure<NativeInput.RAWHID>(dataPtr);
                    var reportPtr = IntPtr.Add(dataPtr, Marshal.SizeOf<NativeInput.RAWHID>());
                    var byteCount = (int)Math.Min(hid.dwSizeHid * hid.dwCount, 24);
                    var bytes = new byte[byteCount];
                    Marshal.Copy(reportPtr, bytes, 0, byteCount);

                    var (usagePage, usage, vendor, product) = GetDeviceInfo(header.hDevice);

                    Captured?.Invoke(new InputEventInfo
                    {
                        Channel = InputChannel.RawInput,
                        DeviceName = device,
                        Report = Convert.ToHexString(bytes),
                        UsagePage = usagePage,
                        Usage = usage,
                        VendorId = vendor,
                        ProductId = product,
                    });
                }
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
        catch (Exception ex)
        {
            Log.Write($"HandleRawInput failed: {ex.Message}");
        }
    }

    private IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var data = Marshal.PtrToStructure<NativeInput.KBDLLHOOKSTRUCT>(lParam);
            var msg = (int)wParam;
            Captured?.Invoke(new InputEventInfo
            {
                Channel = InputChannel.KeyboardHook,
                DeviceName = "keyboard",
                VKey = (int)data.vkCode,
                ScanCode = (int)data.scanCode,
                IsDown = msg is NativeInput.WM_KEYDOWN or NativeInput.WM_SYSKEYDOWN,
                Note = (data.flags & NativeInput.LLKHF_INJECTED) != 0 ? "injected" : string.Empty,
            });
        }

        return NativeInput.CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    public static IReadOnlyList<RawDeviceInfo> EnumerateDevices()
    {
        var list = new List<RawDeviceInfo>();
        var count = 0u;
        var entrySize = (uint)Marshal.SizeOf<NativeInput.RAWINPUTDEVICELIST>();

        if (NativeInput.GetRawInputDeviceList(null, ref count, entrySize) != 0 || count == 0)
            return list;

        var entries = new NativeInput.RAWINPUTDEVICELIST[count];
        if (NativeInput.GetRawInputDeviceList(entries, ref count, entrySize) == uint.MaxValue)
            return list;

        for (var i = 0; i < count; i++)
        {
            var entry = entries[i];
            var (usagePage, usage, vendor, product) = GetDeviceInfo(entry.hDevice);
            list.Add(new RawDeviceInfo
            {
                Handle = entry.hDevice,
                Type = entry.dwType,
                VendorId = vendor,
                ProductId = product,
                UsagePage = usagePage,
                Usage = usage,
                Name = ShortName(GetDeviceName(entry.hDevice)),
            });
        }

        return list;
    }

    private static string DescribeDevice(IntPtr hDevice)
    {
        var name = ShortName(GetDeviceName(hDevice));
        return string.IsNullOrEmpty(name) ? $"0x{hDevice.ToInt64():X}" : name;
    }

    private static string GetDeviceName(IntPtr hDevice)
    {
        var size = 0u;
        NativeInput.GetRawInputDeviceInfo(hDevice, NativeInput.RIDI_DEVICENAME, IntPtr.Zero, ref size);
        if (size == 0)
            return string.Empty;

        var buffer = Marshal.AllocHGlobal((int)(size * sizeof(char)));
        try
        {
            return NativeInput.GetRawInputDeviceInfo(hDevice, NativeInput.RIDI_DEVICENAME, buffer, ref size) > 0
                ? Marshal.PtrToStringUni(buffer) ?? string.Empty
                : string.Empty;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static (ushort UsagePage, ushort Usage, int Vendor, int Product) GetDeviceInfo(IntPtr hDevice)
    {
        var info = new NativeInput.RID_DEVICE_INFO { cbSize = (uint)Marshal.SizeOf<NativeInput.RID_DEVICE_INFO>() };
        var size = info.cbSize;
        var buffer = Marshal.AllocHGlobal((int)size);
        try
        {
            Marshal.StructureToPtr(info, buffer, false);
            if (NativeInput.GetRawInputDeviceInfo(hDevice, NativeInput.RIDI_DEVICEINFO, buffer, ref size) <= 0)
                return (0, 0, 0, 0);

            info = Marshal.PtrToStructure<NativeInput.RID_DEVICE_INFO>(buffer);
            if (info.dwType == NativeInput.RIM_TYPEHID)
                return (info.data.hid.usUsagePage, info.data.hid.usUsage,
                    (int)info.data.hid.dwVendorId, (int)info.data.hid.dwProductId);

            return (0, 0, 0, 0);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static string ShortName(string path)
    {
        if (string.IsNullOrEmpty(path))
            return string.Empty;

        var parts = path.Split('#');
        if (parts.Length >= 3)
            return $"{parts[1]}/{parts[2]}";

        return path;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        UninstallHook();
        NativeInput.RemoveWindowSubclass(_hwnd, _subclassProc, SubclassId);
    }
}
