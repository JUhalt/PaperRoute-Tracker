using System.Globalization;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PaperRoute.Desktop.Styles;

namespace PaperRoute.Desktop.Tests.Styles;

// Headless checks of the styles as the running app loads them.
[TestClass]
public sealed class StyleFamilyTests
{
    // Tabs.axaml selects Fluent's TabStripItem parts by name and type
    // (inventory :1258 asks for this check against the pinned version). The
    // check is on the built control: a TabStrip is shown and the template
    // children of a TabStripItem are read, so it holds for the Avalonia package
    // the tests are built with (12.1.6), not for what the docs say.
    [TestMethod]
    public void TabStripItemHasTheFluentPartsTabsAxamlTargets()
    {
        IReadOnlyList<string> parts = Headless.Run(() =>
        {
            var strip = new TabStrip();
            strip.Items.Add(new TabStripItem { Content = "Pipeline" });
            strip.Items.Add(new TabStripItem { Content = "Published" });
            strip.SelectedIndex = 0;
            var window = new Window { Width = 400, Height = 120, Content = strip };
            window.Show();
            try
            {
                TabStripItem item = strip.GetVisualDescendants().OfType<TabStripItem>().First();
                return item.GetVisualDescendants()
                    .OfType<StyledElement>()
                    .Where(element => element.TemplatedParent == item && !string.IsNullOrEmpty(element.Name))
                    .Select(element => element.GetType().Name + "#" + element.Name)
                    .ToList();
            }
            finally
            {
                window.Close();
            }
        });

        string found = string.Join(", ", parts);
        Assert.Contains("Border#PART_LayoutRoot", parts, "Found: " + found);
        Assert.Contains("Border#PART_SelectedPipe", parts, "Found: " + found);
        Assert.Contains("ContentPresenter#PART_ContentPresenter", parts, "Found: " + found);
    }

    // The selected tab's underline is the accent, 3 px, and only the selected
    // tab shows one: the selectors of Tabs.axaml reach Fluent's parts.
    [TestMethod]
    public void TheSelectedTabIsUnderlinedInTheAccent()
    {
        (bool selectedVisible, double height, Color pipe, bool otherVisible, Color accent) = Headless.Run(() =>
        {
            var strip = new TabStrip();
            strip.Items.Add(new TabStripItem { Content = "Pipeline" });
            strip.Items.Add(new TabStripItem { Content = "Published" });
            strip.SelectedIndex = 0;
            var window = new Window { Width = 400, Height = 120, Content = strip, RequestedThemeVariant = ThemeVariant.Light };
            window.Show();
            try
            {
                TabStripItem[] items = strip.GetVisualDescendants().OfType<TabStripItem>().ToArray();
                Border selected = PipeOf(items[0]);
                Border other = PipeOf(items[1]);
                Assert.IsTrue(window.TryFindResource("AccentBrush", window.ActualThemeVariant, out object? accent));
                return (selected.IsVisible, selected.Bounds.Height, ((ISolidColorBrush)selected.Background!).Color,
                        other.IsVisible, ((ISolidColorBrush)accent!).Color);
            }
            finally
            {
                window.Close();
            }
        });

        Assert.IsTrue(selectedVisible, "The selected tab shows no underline.");
        Assert.AreEqual(3.0, height, "The underline is 3 px tall today (ShelfTabButton.vb:62).");
        Assert.AreEqual(accent, pipe, "The underline is the accent.");
        Assert.IsFalse(otherVisible, "An unselected tab shows an underline.");
    }

    // The app resolves every token of Tokens.axaml to the value the file
    // holds, in Light and in Dark, and the derived brushes ThemeTokens adds are
    // there too: what R1 (was T1 and T2) reads is what a view gets.
    [TestMethod]
    public void TheAppResolvesEveryTokenInBothThemes()
    {
        var expected = new Dictionary<string, IReadOnlyDictionary<string, Color>>(StringComparer.Ordinal)
        {
            ["Light"] = TokenFile.Colors("Light"),
            ["Dark"] = TokenFile.Colors("Dark"),
        };

        List<string> failures = Headless.Run(() =>
        {
            var problems = new List<string>();
            Application app = Application.Current!;
            foreach ((string name, ThemeVariant variant) in new[] { ("Light", ThemeVariant.Light), ("Dark", ThemeVariant.Dark) })
            {
                foreach (KeyValuePair<string, Color> token in expected[name])
                {
                    if (!app.TryGetResource(token.Key, variant, out object? value) || value is not ISolidColorBrush brush)
                    {
                        problems.Add($"{name}: {token.Key} does not resolve to a solid brush");
                    }
                    else if (brush.Color != token.Value)
                    {
                        problems.Add($"{name}: {token.Key} is {brush.Color}, the file says {token.Value}");
                    }
                }

                if (!app.TryGetResource("OutlineThickness", variant, out object? outline) || outline is not Thickness)
                {
                    problems.Add($"{name}: OutlineThickness does not resolve");
                }
            }

            return problems;
        });

        foreach (string key in ThemeTokens.DerivedKeys)
        {
            Assert.IsTrue(expected["Light"].ContainsKey(key), $"The loader lacks the derived {key}.");
        }

        Assert.IsEmpty(failures, string.Join(Environment.NewLine, failures));
    }

    // A TimeSpan resource feeds a BrushTransition's Duration (harness design
    // 3.1, an assumption settled here: a transition is not a styled element,
    // so a DynamicResource on it might not resolve). The four hover
    // transitions of the styles (a button, a chip, a rail item, an interactive
    // card) take HoverDuration, 100 ms, and take the token's new value when
    // the token changes, so each is driven by the token, not by a literal that
    // happens to agree with it.
    [TestMethod]
    public void ATimeSpanTokenDrivesATransition()
    {
        (object? token, string atStart, string afterChange) = Headless.Run(() =>
        {
            var button = new Button { Content = "Save" };
            var chip = new ToggleButton { Content = "Draft", Classes = { "chip" } };
            var rail = new RadioButton { Content = "Board", Classes = { "rail" } };
            var card = new Border { Width = 120, Height = 40, Classes = { "card", "interactive" } };
            var window = new Window { Width = 400, Height = 300, Content = new StackPanel { Children = { button, chip, rail, card } } };
            window.Show();
            try
            {
                string Durations() => string.Join(", ",
                    $"button {HoverOf(FaceOf(button), ContentPresenter.BackgroundProperty)}",
                    $"chip {HoverOf(FaceOf(chip), ContentPresenter.BackgroundProperty)}",
                    $"rail {HoverOf(FaceOf(rail), ContentPresenter.BackgroundProperty)}",
                    $"card {HoverOf(card, Border.BorderBrushProperty)}");

                window.TryFindResource("HoverDuration", window.ActualThemeVariant, out object? value);
                string start = Durations();
                Application.Current!.Resources["HoverDuration"] = TimeSpan.FromMilliseconds(250);
                Dispatcher.UIThread.RunJobs();
                return (value, start, Durations());
            }
            finally
            {
                window.Close();
            }
        });

        Assert.AreEqual(TimeSpan.FromMilliseconds(100), token, "HoverDuration is a TimeSpan of 100 ms (Tokens.axaml).");
        Assert.AreEqual("button 100 ms, chip 100 ms, rail 100 ms, card 100 ms", atStart, "Each hover transition takes the token's value.");
        Assert.AreEqual("button 250 ms, chip 250 ms, rail 250 ms, card 250 ms", afterChange, "Each hover transition follows the token when it changes.");
    }

    // The template's PART_ContentPresenter of a button, a chip or a rail item.
    private static ContentPresenter FaceOf(TemplatedControl control) =>
        control.GetVisualDescendants().OfType<ContentPresenter>().Single(face => face.Name == "PART_ContentPresenter" && face.TemplatedParent == control);

    // The duration of the brush transition on one property, or "none".
    private static string HoverOf(Animatable element, AvaloniaProperty property) =>
        element.Transitions?.OfType<BrushTransition>().SingleOrDefault(transition => transition.Property == property) is { } hover
            ? $"{hover.Duration.TotalMilliseconds.ToString(CultureInfo.InvariantCulture)} ms"
            : "none";

    private static Border PipeOf(TabStripItem item) =>
        item.GetVisualDescendants().OfType<Border>().Single(border => border.Name == "PART_SelectedPipe");
}
