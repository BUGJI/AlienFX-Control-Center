namespace AlienFXControlCenter.Services.Input;

public enum KeySignatureKind
{
    Keyboard,
    Hid,
}

public sealed class KeySignature
{
    public KeySignatureKind Kind { get; set; }

    // Keyboard keys
    public int VKey { get; set; } = -1;
    public int ScanCode { get; set; } = -1;

    // HID / consumer keys
    public int VendorId { get; set; }
    public int ProductId { get; set; }
    public int UsagePage { get; set; }
    public int Usage { get; set; }
    public string ReportHex { get; set; } = string.Empty;

    public bool Matches(KeySignature other)
    {
        if (Kind != other.Kind)
            return false;

        if (Kind == KeySignatureKind.Keyboard)
            return VKey == other.VKey;

        return VendorId == other.VendorId
            && ProductId == other.ProductId
            && UsagePage == other.UsagePage
            && Usage == other.Usage
            && string.Equals(ReportHex, other.ReportHex, StringComparison.OrdinalIgnoreCase);
    }

    public string Describe() => Kind == KeySignatureKind.Keyboard
        ? $"Keyboard VK 0x{VKey:X2} ({NativeInput.VkName((uint)VKey)})"
        : $"HID {VendorId:X4}:{ProductId:X4} {UsagePage:X4}:{Usage:X4} {ReportHex}";
}

public enum KeyActionKind
{
    None,
    ApplyTemplate,
}

public sealed class KeyAction
{
    public KeyActionKind Kind { get; set; } = KeyActionKind.None;

    /// <summary>Target template id (for <see cref="KeyActionKind.ApplyTemplate"/>).</summary>
    public string? TemplateId { get; set; }

    /// <summary>Cached template name for display; refreshed from the store on load.</summary>
    public string? TemplateName { get; set; }
}

public sealed class NamedKey
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public KeySignature Signature { get; set; } = new();
    public bool BuiltIn { get; set; }
    public bool Enabled { get; set; } = true;
    public KeyAction? Action { get; set; }

    public bool HasAction => Action is { Kind: KeyActionKind.ApplyTemplate };

    /// <summary>Returns true when the signature is a plain typing key, which is risky to bind.</summary>
    public bool IsTypingKey => Signature.Kind == KeySignatureKind.Keyboard
        && KeyRecognizer.IsTypingKey(Signature.VKey);

    public string Description => $"{Name}  —  {Signature.Describe()}";

    public override string ToString() => Description;
}
