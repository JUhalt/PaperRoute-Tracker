using Avalonia.Controls;
using Avalonia.Input;

namespace PaperRoute.Desktop.Tests.Input;

// The keyboard's walk through a shown window (harness design, part 2.5),
// moved out of the accessibility tree's Describe: Tab (or Shift+Tab) is
// pressed up to limit times, and each control it lands on is a stop, until
// nothing has the focus or a stop comes round again. The walk starts where
// the focus is, which is not itself a stop until a key reaches it.
internal static class FocusWalk
{
    public static IReadOnlyList<Control> Tab(TopLevel topLevel, int limit) => Walk(topLevel, limit, RawInputModifiers.None);

    public static IReadOnlyList<Control> ShiftTab(TopLevel topLevel, int limit) => Walk(topLevel, limit, RawInputModifiers.Shift);

    public static object? Focused(TopLevel topLevel)
    {
        ArgumentNullException.ThrowIfNull(topLevel);
        return topLevel.FocusManager?.GetFocusedElement();
    }

    private static List<Control> Walk(TopLevel topLevel, int limit, RawInputModifiers modifiers)
    {
        var stops = new List<Control>();
        var seen = new HashSet<Control>(ReferenceEqualityComparer.Instance);
        for (int step = 0; step < limit; step++)
        {
            Keyboard.Press(topLevel, Key.Tab, modifiers);
            if (Focused(topLevel) is not Control focused || !seen.Add(focused))
            {
                break;
            }

            stops.Add(focused);
        }

        return stops;
    }
}
