using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;

namespace PaperRoute.Desktop.Tests.Input;

// The keys a headless test sends (harness design, part 2.5), in one place:
// a press of one key, a chord as the User Guide writes it ("Ctrl+Shift+Tab"),
// and typing. A press sends the key down, then up, with the physical key a
// keyboard reports for it, then runs the dispatcher's jobs, as the Board
// tests' own Press did. A key with no physical key here throws with its name.
//
// A modifier key pressed on its own (Ctrl, Alt, Shift) holds its modifier
// while it is down and not on its release, as a keyboard reports it.
internal static class Keyboard
{
    private static readonly Dictionary<Key, PhysicalKey> s_physical = BuildPhysicalKeys();

    private static readonly Dictionary<Key, RawInputModifiers> s_modifierKeys = new()
    {
        [Key.LeftCtrl] = RawInputModifiers.Control,
        [Key.RightCtrl] = RawInputModifiers.Control,
        [Key.LeftAlt] = RawInputModifiers.Alt,
        [Key.RightAlt] = RawInputModifiers.Alt,
        [Key.LeftShift] = RawInputModifiers.Shift,
        [Key.RightShift] = RawInputModifiers.Shift,
    };

    private static readonly Dictionary<string, RawInputModifiers> s_modifierNames = new(StringComparer.Ordinal)
    {
        ["Ctrl"] = RawInputModifiers.Control,
        ["Alt"] = RawInputModifiers.Alt,
        ["Shift"] = RawInputModifiers.Shift,
    };

    private static readonly Dictionary<string, Key> s_keyNames = BuildKeyNames();

    // One key down and up, then the dispatcher's jobs.
    public static void Press(TopLevel topLevel, Key key, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        ArgumentNullException.ThrowIfNull(topLevel);
        if (!s_physical.TryGetValue(key, out PhysicalKey physical))
        {
            throw new ArgumentOutOfRangeException(nameof(key), key, $"No physical key is mapped for {key}.");
        }

        RawInputModifiers held = s_modifierKeys.TryGetValue(key, out RawInputModifiers own) ? own : RawInputModifiers.None;
        topLevel.KeyPress(key, modifiers | held, physical, null);
        topLevel.KeyRelease(key, modifiers, physical, null);
        Dispatcher.UIThread.RunJobs();
    }

    // A chord as the guide writes it, such as "Ctrl+Shift+Tab": its key,
    // pressed once with its modifiers held.
    public static void Chord(TopLevel topLevel, string chord)
    {
        (Key key, RawInputModifiers modifiers) = Parse(chord);
        Press(topLevel, key, modifiers);
    }

    // The key and modifiers of a chord written as the guide writes it:
    // modifiers first (Ctrl, Alt, Shift, each once), then one key name, joined
    // by "+". A name it does not know throws with that name.
    public static (Key Key, RawInputModifiers Modifiers) Parse(string chord)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(chord);
        string[] names = chord.Split('+');
        RawInputModifiers modifiers = RawInputModifiers.None;
        for (int index = 0; index < names.Length - 1; index++)
        {
            if (!s_modifierNames.TryGetValue(names[index], out RawInputModifiers modifier))
            {
                throw new ArgumentException($"\"{names[index]}\" in \"{chord}\" is not a modifier (Ctrl, Alt or Shift).", nameof(chord));
            }

            if ((modifiers & modifier) != 0)
            {
                throw new ArgumentException($"\"{chord}\" names {names[index]} twice.", nameof(chord));
            }

            modifiers |= modifier;
        }

        string last = names[^1];
        if (!s_keyNames.TryGetValue(last, out Key key))
        {
            throw new ArgumentException($"\"{last}\" in \"{chord}\" is not a key name.", nameof(chord));
        }

        return (key, modifiers);
    }

    // One text-input event per character, as a keyboard sends them, each
    // followed by the dispatcher's jobs.
    public static void Type(TopLevel topLevel, string text)
    {
        ArgumentNullException.ThrowIfNull(topLevel);
        ArgumentNullException.ThrowIfNull(text);
        foreach (char character in text)
        {
            topLevel.KeyTextInput(character.ToString());
            Dispatcher.UIThread.RunJobs();
        }
    }

    private static Dictionary<Key, PhysicalKey> BuildPhysicalKeys()
    {
        var map = new Dictionary<Key, PhysicalKey>
        {
            [Key.Left] = PhysicalKey.ArrowLeft,
            [Key.Right] = PhysicalKey.ArrowRight,
            [Key.Up] = PhysicalKey.ArrowUp,
            [Key.Down] = PhysicalKey.ArrowDown,
            [Key.Home] = PhysicalKey.Home,
            [Key.End] = PhysicalKey.End,
            [Key.PageUp] = PhysicalKey.PageUp,
            [Key.PageDown] = PhysicalKey.PageDown,
            [Key.Enter] = PhysicalKey.Enter,
            [Key.Escape] = PhysicalKey.Escape,
            [Key.Space] = PhysicalKey.Space,
            [Key.Tab] = PhysicalKey.Tab,
            [Key.Back] = PhysicalKey.Backspace,
            [Key.Delete] = PhysicalKey.Delete,
            [Key.Apps] = PhysicalKey.ContextMenu,
            [Key.BrowserBack] = PhysicalKey.BrowserBack,
            [Key.BrowserForward] = PhysicalKey.BrowserForward,
            [Key.OemComma] = PhysicalKey.Comma,
            [Key.OemSemicolon] = PhysicalKey.Semicolon,
            [Key.OemQuestion] = PhysicalKey.Slash,
            [Key.LeftCtrl] = PhysicalKey.ControlLeft,
            [Key.RightCtrl] = PhysicalKey.ControlRight,
            [Key.LeftAlt] = PhysicalKey.AltLeft,
            [Key.RightAlt] = PhysicalKey.AltRight,
            [Key.LeftShift] = PhysicalKey.ShiftLeft,
            [Key.RightShift] = PhysicalKey.ShiftRight,
        };

        for (char letter = 'A'; letter <= 'Z'; letter++)
        {
            map[Enum.Parse<Key>(letter.ToString())] = Enum.Parse<PhysicalKey>(letter.ToString());
        }

        for (int digit = 0; digit <= 9; digit++)
        {
            map[Enum.Parse<Key>("D" + digit)] = Enum.Parse<PhysicalKey>("Digit" + digit);
            map[Enum.Parse<Key>("NumPad" + digit)] = Enum.Parse<PhysicalKey>("NumPad" + digit);
        }

        for (int number = 1; number <= 12; number++)
        {
            map[Enum.Parse<Key>("F" + number)] = Enum.Parse<PhysicalKey>("F" + number);
        }

        return map;
    }

    private static Dictionary<string, Key> BuildKeyNames()
    {
        var names = new Dictionary<string, Key>(StringComparer.Ordinal)
        {
            ["Left"] = Key.Left,
            ["Right"] = Key.Right,
            ["Up"] = Key.Up,
            ["Down"] = Key.Down,
            ["Home"] = Key.Home,
            ["End"] = Key.End,
            ["Page Up"] = Key.PageUp,
            ["Page Down"] = Key.PageDown,
            ["Enter"] = Key.Enter,
            ["Esc"] = Key.Escape,
            ["Space"] = Key.Space,
            ["Tab"] = Key.Tab,
            ["Backspace"] = Key.Back,
            ["Delete"] = Key.Delete,
            ["Context Menu"] = Key.Apps,
            ["Browser Back"] = Key.BrowserBack,
            ["Browser Forward"] = Key.BrowserForward,
            [","] = Key.OemComma,
            [";"] = Key.OemSemicolon,
            ["?"] = Key.OemQuestion,
        };

        for (char letter = 'A'; letter <= 'Z'; letter++)
        {
            names[letter.ToString()] = Enum.Parse<Key>(letter.ToString());
        }

        for (int digit = 0; digit <= 9; digit++)
        {
            names[digit.ToString(System.Globalization.CultureInfo.InvariantCulture)] = Enum.Parse<Key>("D" + digit);
        }

        for (int number = 1; number <= 12; number++)
        {
            names["F" + number] = Enum.Parse<Key>("F" + number);
        }

        return names;
    }
}
