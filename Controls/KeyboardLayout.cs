namespace AlienFXControlCenter.Controls;

public sealed class KeyPlacement
{
    public KeyPlacement()
    {
    }

    public KeyPlacement(string name, double col, int row, double width = 1)
    {
        Name = name;
        Col = col;
        Row = row;
        Width = width;
    }

    public string Name { get; set; } = string.Empty;
    public double Col { get; set; }
    public int Row { get; set; }
    public double Width { get; set; } = 1;
    public double Height { get; set; } = 1;
    public int? LightId { get; set; }
}

public static class KeyboardLayout
{
    public const int Columns = 18;
    public const int Rows = 6;

    public static readonly IReadOnlyList<KeyPlacement> Keys = Build();

    public static string Label(string lightName) => lightName switch
    {
        "Backspace" => "\u232B",
        "Enter" => "\u23CE",
        "Caps Lock" => "Caps",
        "Left Windows" => "Win",
        "Right Alt" => "Alt",
        "Right Ctrl" => "Ctrl",
        "Right shift" => "Shift",
        "Fn (Indicator)" => "Fn",
        "Win lock" => "Lock",
        "Mic Mute" => "Mic",
        "Vol down" => "Vol-",
        "Vol up" => "Vol+",
        "Delete" => "Del",
        _ => lightName,
    };

    private static List<KeyPlacement> Build()
    {
        var keys = new List<KeyPlacement>();

        // Row 0 - function row
        keys.Add(new KeyPlacement("ESC", 0, 0));
        for (var i = 1; i <= 12; i++)
            keys.Add(new KeyPlacement($"F{i}", i, 0));

        // Row 1 - number row
        keys.Add(new KeyPlacement("`", 0, 1));
        for (var i = 1; i <= 9; i++)
            keys.Add(new KeyPlacement($"{i}", i, 1));
        keys.Add(new KeyPlacement("0", 10, 1));
        keys.Add(new KeyPlacement("-", 11, 1));
        keys.Add(new KeyPlacement("=", 12, 1));
        keys.Add(new KeyPlacement("Backspace", 13, 1, 2));

        // Row 2 - QWERTY row
        keys.Add(new KeyPlacement("Tab", 0, 2, 1.5));
        var qwerty = new[] { "Q", "W", "E", "R", "T", "Y", "U", "I", "O", "P" };
        for (var i = 0; i < qwerty.Length; i++)
            keys.Add(new KeyPlacement(qwerty[i], 1.5 + i, 2));
        keys.Add(new KeyPlacement("[", 11.5, 2));
        keys.Add(new KeyPlacement("]", 12.5, 2));
        keys.Add(new KeyPlacement("\\", 13.5, 2, 1.5));

        // Row 3 - home row
        keys.Add(new KeyPlacement("Caps Lock", 0, 3, 1.75));
        var homeRow = new[] { "A", "S", "D", "F", "G", "H", "J", "K", "L" };
        for (var i = 0; i < homeRow.Length; i++)
            keys.Add(new KeyPlacement(homeRow[i], 1.75 + i, 3));
        keys.Add(new KeyPlacement(";", 10.75, 3));
        keys.Add(new KeyPlacement("'", 11.75, 3));
        keys.Add(new KeyPlacement("Enter", 12.75, 3, 2.25));

        // Row 4 - bottom letter row
        keys.Add(new KeyPlacement("Shift", 0, 4, 2.25));
        var bottomRow = new[] { "Z", "X", "C", "V", "B", "N", "M" };
        for (var i = 0; i < bottomRow.Length; i++)
            keys.Add(new KeyPlacement(bottomRow[i], 2.25 + i, 4));
        keys.Add(new KeyPlacement(",", 9.25, 4));
        keys.Add(new KeyPlacement(".", 10.25, 4));
        keys.Add(new KeyPlacement("/", 11.25, 4));
        keys.Add(new KeyPlacement("Right shift", 12.5, 4, 2.5));

        // Row 5 - modifiers
        keys.Add(new KeyPlacement("Ctrl", 0, 5, 1.25));
        keys.Add(new KeyPlacement("Fn (Indicator)", 1.25, 5, 1.25));
        keys.Add(new KeyPlacement("Left Windows", 2.5, 5, 1.25));
        keys.Add(new KeyPlacement("Alt", 3.75, 5, 1.25));
        keys.Add(new KeyPlacement("Space", 5, 5, 6.25));
        keys.Add(new KeyPlacement("Right Alt", 11.25, 5, 1.25));
        keys.Add(new KeyPlacement("Right Ctrl", 12.5, 5, 1.25));

        // Right-side navigation / media cluster
        keys.Add(new KeyPlacement("Delete", 15, 0));
        keys.Add(new KeyPlacement("Home", 16, 0));
        keys.Add(new KeyPlacement("End", 17, 0));
        keys.Add(new KeyPlacement("Mute", 15, 1));
        keys.Add(new KeyPlacement("Vol down", 16, 1));
        keys.Add(new KeyPlacement("Vol up", 17, 1));
        keys.Add(new KeyPlacement("Mic Mute", 15, 2));
        keys.Add(new KeyPlacement("Win lock", 16, 2));
        keys.Add(new KeyPlacement("Up", 16, 4));
        keys.Add(new KeyPlacement("Left", 15, 5));
        keys.Add(new KeyPlacement("Down", 16, 5));
        keys.Add(new KeyPlacement("Right", 17, 5));

        return keys;
    }
}
