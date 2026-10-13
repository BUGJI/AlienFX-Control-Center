namespace AlienFXControlCenter.Services.Input;

public static class KeyRecognizer
{
    public static KeySignature? Identify(InputEventInfo info)
    {
        // Keyboard keys: only the raw keyboard-hook 'down' edge (avoids the
        // duplicate Raw Input report and the key-up event).
        if (info.Channel == InputChannel.KeyboardHook && info.VKey >= 0 && info.IsDown)
        {
            return new KeySignature
            {
                Kind = KeySignatureKind.Keyboard,
                VKey = info.VKey,
                ScanCode = info.ScanCode,
            };
        }

        // HID / consumer keys: only a report that carries a non-zero payload.
        if (info.Channel == InputChannel.RawInput && info.VKey < 0 && IsHidPress(info.Report))
        {
            return new KeySignature
            {
                Kind = KeySignatureKind.Hid,
                VendorId = info.VendorId,
                ProductId = info.ProductId,
                UsagePage = info.UsagePage,
                Usage = info.Usage,
                ReportHex = info.Report,
            };
        }

        return null;
    }

    public static NamedKey? Match(IEnumerable<NamedKey> keys, KeySignature signature)
        => keys.FirstOrDefault(key => key.Signature.Matches(signature));

    public static bool IsHidPress(string reportHex)
    {
        if (string.IsNullOrEmpty(reportHex) || reportHex.Length < 4)
            return false;

        // Skip the report id (first byte); a press has at least one non-zero payload byte.
        for (var i = 2; i + 1 < reportHex.Length; i += 2)
        {
            if (reportHex[i] != '0' || reportHex[i + 1] != '0')
                return true;
        }

        return false;
    }

    /// <summary>
    /// True for keys that are normally used for typing. Binding these to an
    /// action is allowed but risks firing while the user types elsewhere.
    /// </summary>
    public static bool IsTypingKey(int vk)
        => vk is (>= 0x41 and <= 0x5A)   // A-Z
            or (>= 0x30 and <= 0x39)      // 0-9
            or (>= 0x70 and <= 0x7B)      // F1-F12
            or 0x20 or 0x0D or 0x09 or 0x08   // Space, Enter, Tab, Backspace
            or (>= 0x10 and <= 0x12)      // Shift, Ctrl, Alt
            or 0x5B or 0x5C or 0x1B;      // Win keys, Esc
}
