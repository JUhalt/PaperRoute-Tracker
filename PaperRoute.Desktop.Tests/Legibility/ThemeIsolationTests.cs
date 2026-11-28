using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PaperRoute.Desktop.Styles;
using PaperRoute.Desktop.Tests.Styles;

namespace PaperRoute.Desktop.Tests.Legibility;

// The isolation pair (the harness design, parts 5 and 7): the sweeps switch
// themes in test after test, and PerTest isolation must give each test a
// fresh application, with no theme and no static state left from the one
// before (the risk named there: InputTemplates.s_registered). The first
// test puts a contrast case on; the next checks that a fresh application is
// back in Light, with Light's token colours and the placeholder fix still
// working. The second also puts hc2 on itself first, in a Headless.Run of
// its own, so it holds in whatever order the two run.
// Limits of headless: HeadlessLimits.md
[TestClass]
public sealed class ThemeIsolationTests
{
    [TestMethod]
    public void Isolation1_Hc2IsOnInItsOwnTest() => PutHc2On();

    [TestMethod]
    public void Isolation2_TheNextTestHasLightTokens()
    {
        PutHc2On();
        IReadOnlyDictionary<string, Color> light = TokenFile.Colors("Light");
        List<string> problems = Headless.Run(() =>
        {
            var problems = new List<string>();
            Application application = Application.Current!;
            if (ContrastTheme.IsContrast(application.ActualThemeVariant))
            {
                problems.Add($"The application starts in {application.ActualThemeVariant}, a contrast variant.");
            }

            (Window window, TextBox box) = ShowAProbe();
            try
            {
                if (window.ActualThemeVariant != Avalonia.Styling.ThemeVariant.Light)
                {
                    problems.Add($"A new window is in {window.ActualThemeVariant}, not Light.");
                }

                foreach ((string key, Color colour) in light)
                {
                    if (!window.TryFindResource(key, window.ActualThemeVariant, out object? found) || found is not ISolidColorBrush brush || brush.Color != colour)
                    {
                        problems.Add($"{key} is {(found as ISolidColorBrush)?.Color.ToString() ?? "missing"}, not Light's {colour}.");
                    }
                }

                TextBlock placeholder = box.GetVisualDescendants().OfType<TextBlock>().Single(block => block.Name == "PART_Placeholder");
                if (placeholder.Opacity != 1)
                {
                    problems.Add($"The text box's placeholder is drawn at Opacity {placeholder.Opacity}, not 1 (InputTemplates).");
                }
            }
            finally
            {
                window.Close();
            }

            return problems;
        });

        Assert.IsEmpty(problems, "State left from a test before:" + Environment.NewLine + string.Join(Environment.NewLine, problems));
    }

    // Puts hc2 on in a Headless.Run of its own and checks that it took:
    // a card-coloured part shows hc2's window colour.
    private static void PutHc2On()
    {
        (Color card, bool contrast) = Headless.Run(() =>
        {
            ThemeCase.Hc2.Apply(Application.Current!);
            (Window window, _) = ShowAProbe();
            try
            {
                Color card = window.TryFindResource("CardBackgroundBrush", window.ActualThemeVariant, out object? found) && found is ISolidColorBrush brush ? brush.Color : default;
                return (card, ContrastTheme.IsContrast(window.ActualThemeVariant));
            }
            finally
            {
                window.Close();
            }
        });

        Assert.IsTrue(contrast, "hc2 puts the window in a contrast variant.");
        Assert.AreEqual(SystemContrastColors.NightSky.Window, card, "Under hc2 the card is hc2's window colour.");
    }

    // A window holding a text box with a placeholder, shown.
    private static (Window Window, TextBox Box) ShowAProbe()
    {
        var box = new TextBox { Width = 160, PlaceholderText = "Search" };
        var window = new Window { Width = 300, Height = 120, Content = box };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return (window, box);
    }
}
