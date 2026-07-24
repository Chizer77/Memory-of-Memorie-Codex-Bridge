namespace MemoryOfMemorieCodexBridge.Windows;

internal readonly record struct Hotkey(int[] Modifiers, int Key)
{
    internal static Hotkey Parse(string value)
    {
        var parts = value.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2) throw new ArgumentException($"Invalid hotkey: {value}");
        var modifiers = new int[parts.Length - 1];
        for (var index = 0; index < modifiers.Length; index++)
        {
            modifiers[index] = ParseModifier(parts[index]);
            if (Array.IndexOf(modifiers, modifiers[index], 0, index) >= 0) throw new ArgumentException($"Duplicate modifier in hotkey: {value}");
        }
        return new Hotkey(modifiers, ParseKey(parts[^1]));
    }

    internal bool IsPressed()
    {
        foreach (var modifier in Modifiers)
        {
            if ((NativeMethods.GetAsyncKeyState(modifier) & 0x8000) == 0) return false;
        }
        return (NativeMethods.GetAsyncKeyState(Key) & 0x8000) != 0;
    }

    private static int ParseModifier(string value) => value.ToUpperInvariant() switch
    {
        "CTRL" or "CONTROL" => NativeMethods.VK_CONTROL,
        "ALT" => NativeMethods.VK_MENU,
        "SHIFT" => NativeMethods.VK_SHIFT,
        "WIN" or "WINDOWS" => NativeMethods.VK_LWIN,
        _ => throw new ArgumentException($"Unknown hotkey modifier: {value}")
    };

    private static int ParseKey(string value)
    {
        var upper = value.ToUpperInvariant();
        if (upper.Length == 1 && upper[0] <= 127 && char.IsLetterOrDigit(upper[0])) return upper[0];
        if (upper.Length > 1 && upper[0] == 'F' && int.TryParse(upper.AsSpan(1), out var number) && number is >= 1 and <= 24) return NativeMethods.VK_F1 + number - 1;
        return upper switch
        {
            "ENTER" or "RETURN" => NativeMethods.VK_RETURN,
            "ESC" or "ESCAPE" => NativeMethods.VK_ESCAPE,
            "SPACE" => NativeMethods.VK_SPACE,
            "TAB" => NativeMethods.VK_TAB,
            "DELETE" or "DEL" => NativeMethods.VK_DELETE,
            _ => throw new ArgumentException($"Unknown hotkey key: {value}")
        };
    }
}
