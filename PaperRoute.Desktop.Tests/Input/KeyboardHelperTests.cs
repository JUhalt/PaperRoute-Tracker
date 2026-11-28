using System.Text.RegularExpressions;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using PaperRoute.Desktop.Tests.Board;

namespace PaperRoute.Desktop.Tests.Input;

// The keyboard helpers (harness design, parts 2.5 and 5): the chord parser
// against the key names the User Guide uses, a press and a chord as the
// focused control receives them, an unmapped key, the Tab walk, and no
// modifier carried from one press to the next. Each window is a plain one of
// the test's own, so these tests hold whatever the trial's views do.
[TestClass]
public sealed class KeyboardHelperTests : StorageCheckedTests
{
    // The bold names in the guide's "Keyboard shortcuts" section that name a
    // button, a window, a field or a symbol, not a key. Every other bold name
    // there must parse. A name listed here that the section no longer holds
    // fails, so the list cannot go stale; but a name on it is never parsed, so
    // list only names that are not keys.
    private static readonly string[] s_notKeys =
    [
        "Cancel", "Close", "Fill Blanks from Crossref", "Check for Publications", "Stop",
        "⋯", "?", "View route", "Done", "Route View", "Find", "Look Up Journal Facts",
    ];

    // Issue 3 proves only the parser (design 2.5); issue 5 reads the guide's
    // table against the key bindings.
    [TestMethod]
    public void EveryKeyNameTheGuideUsesParses()
    {
        string guide = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Input", "USER_GUIDE.md")).Replace("\r\n", "\n", StringComparison.Ordinal);
        string section = Section(guide, "### Keyboard shortcuts");
        List<string> bold = Regex.Matches(section, @"\*\*(.+?)\*\*").Select(match => match.Groups[1].Value).Distinct(StringComparer.Ordinal).ToList();
        List<string> keys = bold.Where(name => !s_notKeys.Contains(name, StringComparer.Ordinal)).ToList();

        var failures = new List<string>();
        foreach (string name in keys)
        {
            try
            {
                _ = Keyboard.Parse(name);
            }
            catch (ArgumentException problem)
            {
                failures.Add(problem.Message);
            }
        }

        Assert.IsEmpty(failures, "Key names in the guide the parser does not accept:\n" + string.Join("\n", failures));
        Assert.IsEmpty(s_notKeys.Where(name => !bold.Contains(name, StringComparer.Ordinal)).ToList(), "Names listed as not keys that the section no longer holds.");
        Assert.IsGreaterThanOrEqualTo(13, keys.Count, "The section's table alone names 13 shortcuts; found: " + string.Join(", ", keys));
        Assert.AreEqual((Key.D1, RawInputModifiers.Control), Keyboard.Parse("Ctrl+1"));
        Assert.AreEqual((Key.BrowserBack, RawInputModifiers.None), Keyboard.Parse("Browser Back"));
        Assert.AreEqual((Key.Tab, RawInputModifiers.Shift), Keyboard.Parse("Shift+Tab"));
        // The guide's "?" is a help button's label, listed above beside "⋯";
        // the design's key map still holds the ? key.
        Assert.AreEqual((Key.OemQuestion, RawInputModifiers.None), Keyboard.Parse("?"));
    }

    [TestMethod]
    public void APressReachesTheFocusedControlOnce()
    {
        List<string> events = Headless.Run(() =>
        {
            var first = new Button { Content = "First" };
            var second = new Button { Content = "Second" };
            var panel = new StackPanel();
            panel.Children.Add(first);
            panel.Children.Add(second);
            return InWindow(panel, window =>
            {
                second.Focus(NavigationMethod.Tab);
                var events = new List<string>();
                Record(first, "first", events, RoutingStrategies.Bubble);
                Record(second, "second", events, RoutingStrategies.Bubble);
                Keyboard.Press(window, Key.F);
                return events;
            });
        });

        CollectionAssert.AreEqual(new[] { "second down F F None", "second up F F None" }, events, string.Join(" | ", events));
    }

    [TestMethod]
    public void AChordArrivesWithItsModifiers()
    {
        List<string> events = Headless.Run(() => InWindow(new Button { Content = "Only" }, window =>
        {
            ((Button)window.Content!).Focus(NavigationMethod.Tab);
            var events = new List<string>();
            Record(window, "window", events, RoutingStrategies.Tunnel);
            Keyboard.Chord(window, "Ctrl+Shift+Tab");
            return events;
        }));

        CollectionAssert.AreEqual(new[] { "window down Tab Tab Control, Shift", "window up Tab Tab Control, Shift" }, events, string.Join(" | ", events));
        Assert.AreEqual((Key.Tab, RawInputModifiers.Control | RawInputModifiers.Shift), Keyboard.Parse("Ctrl+Shift+Tab"));
        Assert.AreEqual((Key.Left, RawInputModifiers.Alt), Keyboard.Parse("Alt+Left"));
    }

    [TestMethod]
    public void AnUnmappedKeyNamesItself()
    {
        (string message, List<string> events) = Headless.Run(() => InWindow(new Button { Content = "Only" }, window =>
        {
            ((Button)window.Content!).Focus(NavigationMethod.Tab);
            var events = new List<string>();
            Record(window, "window", events, RoutingStrategies.Tunnel);
            ArgumentOutOfRangeException problem = Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => Keyboard.Press(window, Key.CapsLock));
            return (problem.Message, events);
        }));

        Assert.Contains("CapsLock", message);
        Assert.IsEmpty(events, "An unmapped key sends nothing: " + string.Join(" | ", events));
        Assert.Contains("\"Hyper\"", Assert.ThrowsExactly<ArgumentException>(() => Keyboard.Parse("Ctrl+Hyper")).Message);
        Assert.Contains("\"Win\"", Assert.ThrowsExactly<ArgumentException>(() => Keyboard.Parse("Win+F")).Message);
        Assert.Contains("\"Shift\"", Assert.ThrowsExactly<ArgumentException>(() => Keyboard.Parse("Shift")).Message);
    }

    // From A, Tab reaches B, C, A, then B again, which is a stop already: the
    // walk ends there, short of its limit. Shift+Tab walks back.
    [TestMethod]
    public void TheTabWalkStopsWhenAStopComesRound()
    {
        (List<string> forward, int presses, List<string> limited, List<string> back) = Headless.Run(() =>
        {
            Button[] buttons = new[] { "A", "B", "C" }.Select(name => new Button { Name = name, Content = name }).ToArray();
            var panel = new StackPanel();
            panel.Children.AddRange(buttons);
            return InWindow(panel, window =>
            {
                int tabs = 0;
                window.AddHandler(InputElement.KeyDownEvent, (_, args) => tabs += args.Key == Key.Tab ? 1 : 0, RoutingStrategies.Tunnel, handledEventsToo: true);
                List<string> Names(IReadOnlyList<Control> stops) => stops.Select(stop => stop.Name ?? "?").ToList();

                buttons[0].Focus(NavigationMethod.Tab);
                List<string> forward = Names(FocusWalk.Tab(window, 10));
                int presses = tabs;
                buttons[0].Focus(NavigationMethod.Tab);
                List<string> limited = Names(FocusWalk.Tab(window, 2));
                buttons[0].Focus(NavigationMethod.Tab);
                List<string> back = Names(FocusWalk.ShiftTab(window, 10));
                return (forward, presses, limited, back);
            });
        });

        CollectionAssert.AreEqual(new[] { "B", "C", "A" }, forward, "Tab walk: " + string.Join(" > ", forward));
        Assert.AreEqual(4, presses, "The walk ends on the Tab that comes round to a stop, not at its limit of 10.");
        CollectionAssert.AreEqual(new[] { "B", "C" }, limited, "A walk stops at its limit.");
        CollectionAssert.AreEqual(new[] { "C", "B", "A" }, back, "Shift+Tab walk: " + string.Join(" > ", back));
    }

    // The design's assumption (2.5): headless input carries no key state from
    // one press to the next. A chord's modifier is not held for the next key,
    // and a modifier key pressed alone (as K2 presses Ctrl and Alt) holds its
    // modifier on the way down only.
    [TestMethod]
    public void ModifierStateDoesNotLeakBetweenPresses()
    {
        List<string> events = Headless.Run(() => InWindow(new Button { Content = "Only" }, window =>
        {
            ((Button)window.Content!).Focus(NavigationMethod.Tab);
            var events = new List<string>();
            Record(window, "window", events, RoutingStrategies.Tunnel);
            Keyboard.Press(window, Key.F, RawInputModifiers.Control);
            Keyboard.Press(window, Key.F);
            Keyboard.Press(window, Key.LeftCtrl);
            Keyboard.Press(window, Key.F);
            Keyboard.Chord(window, "Alt+Left");
            Keyboard.Press(window, Key.Left);
            return events;
        }));

        CollectionAssert.AreEqual(
            new[]
            {
                "window down F F Control", "window up F F Control",
                "window down F F None", "window up F F None",
                "window down LeftCtrl ControlLeft Control", "window up LeftCtrl ControlLeft None",
                "window down F F None", "window up F F None",
                "window down Left ArrowLeft Alt", "window up Left ArrowLeft Alt",
                "window down Left ArrowLeft None", "window up Left ArrowLeft None",
            },
            events,
            string.Join(" | ", events));
    }

    // The text from a heading to the next heading of its level or above.
    private static string Section(string text, string heading)
    {
        int start = text.IndexOf("\n" + heading + "\n", StringComparison.Ordinal);
        Assert.IsGreaterThanOrEqualTo(0, start, $"The guide has no \"{heading}\" section.");
        int body = start + heading.Length + 2;
        Match next = Regex.Match(text[body..], @"^#{1,3} ", RegexOptions.Multiline);
        return next.Success ? text.Substring(body, next.Index) : text[body..];
    }

    private static void Record(Interactive target, string name, List<string> events, RoutingStrategies strategy)
    {
        target.AddHandler(InputElement.KeyDownEvent, (_, args) => events.Add($"{name} down {args.Key} {args.PhysicalKey} {args.KeyModifiers}"), strategy, handledEventsToo: true);
        target.AddHandler(InputElement.KeyUpEvent, (_, args) => events.Add($"{name} up {args.Key} {args.PhysicalKey} {args.KeyModifiers}"), strategy, handledEventsToo: true);
    }

    private static T InWindow<T>(Control content, Func<Window, T> body)
    {
        var window = new Window { Width = 400, Height = 300, Content = content };
        window.Show();
        try
        {
            Dispatcher.UIThread.RunJobs();
            return body(window);
        }
        finally
        {
            window.Close();
        }
    }
}
