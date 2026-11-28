using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using ManuscriptPipeline.Models;
using PaperRoute.Desktop.Styles;
using PaperRoute.Desktop.Tests.Board;

namespace PaperRoute.Desktop.Tests.Styles;

// AppTheme, the one owner of the theme variant (harness design 3.4, pull
// request S3): the appearance gives its variant, a contrast theme wins over it
// and gives it back, a change needs no restart, and no other code sets a
// variant or reads the system's contrast state. TheWholeWindowFollowsEvery-
// ThemeChange (WholeWindowThemeTests) judges every control of the window.
//
// Every test drives a ContrastPreference of its own: no test reads the
// contrast setting of this PC.
[TestClass]
public sealed class AppThemeTests
{
    private static readonly Color s_lightCard = Color.Parse("#FFFFFF");
    private static readonly Color s_darkCard = Color.Parse("#282C33");

    public TestContext TestContext { get; set; } = null!;

    // System, Light and Dark, with no contrast theme, give Default (Avalonia's
    // own following of the system), Light and Dark: by the rule, and on the
    // running application, which App.Initialize leaves at System.
    [TestMethod]
    public void TheAppThemeGivesTheRightVariant()
    {
        (Appearance Appearance, ThemeVariant Variant)[] expected =
        [
            (Appearance.System, ThemeVariant.Default),
            (Appearance.Light, ThemeVariant.Light),
            (Appearance.Dark, ThemeVariant.Dark),
        ];
        var off = new ContrastPreference(false, SystemContrastColors.Desert);
        foreach ((Appearance appearance, ThemeVariant variant) in expected)
        {
            Assert.AreEqual(variant, AppTheme.VariantFor(appearance, null), $"{appearance} with no contrast preference");
            Assert.AreEqual(variant, AppTheme.VariantFor(appearance, off), $"{appearance} with the contrast theme off");
        }

        List<string> failures = Headless.Run(() =>
        {
            var problems = new List<string>();
            Application app = Application.Current!;
            AppTheme theme = AppTheme.Of(app);
            if (theme.Appearance != Appearance.System || app.RequestedThemeVariant != ThemeVariant.Default)
            {
                problems.Add($"At start: {theme.Appearance} and {app.RequestedThemeVariant}, not System and Default");
            }

            foreach ((Appearance appearance, ThemeVariant variant) in expected.Skip(1).Concat(expected.Take(1)))
            {
                theme.Appearance = appearance;
                Dispatcher.UIThread.RunJobs();
                if (app.RequestedThemeVariant != variant || theme.Variant != variant)
                {
                    problems.Add($"{appearance}: the app requests {app.RequestedThemeVariant} and the theme gives {theme.Variant}, not {variant}");
                }
            }

            return problems;
        });

        Assert.IsEmpty(failures, string.Join(Environment.NewLine, failures));
    }

    // A change of appearance reaches a shown card at once, on the same card
    // view (nothing rebuilt), through the application, not the window.
    [TestMethod]
    public void AnAppearanceChangeNeedsNoRestart()
    {
        var session = new MemorySession([Papers.Paper("An idea with no target journal yet", PaperStage.Idea, journal: string.Empty, stageDaysAgo: 3)], Papers.Today);
        (Color light, Color dark, Color lightAgain, bool same) = Headless.Run(() =>
        {
            AppTheme theme = AppTheme.Of(Application.Current!);
            theme.Appearance = Appearance.Light;
            using BoardWindow board = BoardWindow.Show(session);
            CardView card = board.Cards("PipelineShelf")[0];
            Border face = card.Part<Border>("Card");
            Color first = BackgroundOf(face);
            theme.Appearance = Appearance.Dark;
            Dispatcher.UIThread.RunJobs();
            Color second = BackgroundOf(face);
            theme.Appearance = Appearance.Light;
            Dispatcher.UIThread.RunJobs();
            Color third = BackgroundOf(face);
            return (first, second, third, ReferenceEquals(card, board.Cards("PipelineShelf")[0]));
        });

        Assert.AreEqual(s_lightCard, light, "In Light a card is CardBackground #FFFFFF.");
        Assert.AreEqual(s_darkCard, dark, "After the change to Dark the same card is #282C33.");
        Assert.AreEqual(s_lightCard, lightAgain, "Back in Light the card is #FFFFFF again.");
        Assert.IsTrue(same, "The change rebuilt the card view.");
    }

    // A contrast theme wins over Light and Dark and gives back the appearance
    // chosen, including one chosen while the contrast theme was on, which
    // changes nothing until it goes off.
    [TestMethod]
    public void ContrastWinsOverTheAppearanceAndGivesItBack()
    {
        Assert.AreEqual(ContrastTheme.HighContrastDark, AppTheme.VariantFor(Appearance.Light, new ContrastPreference(true, SystemContrastColors.Hc1)), "hc1 wins over Light.");
        Assert.AreEqual(ContrastTheme.HighContrastLight, AppTheme.VariantFor(Appearance.Dark, new ContrastPreference(true, SystemContrastColors.Desert)), "hcwhite wins over Dark.");
        Assert.AreEqual(ContrastTheme.HighContrastLight, AppTheme.VariantFor(Appearance.System, new ContrastPreference(true, SystemContrastColors.Desert)), "hcwhite wins over System.");

        List<string> failures = Headless.Run(() =>
        {
            var problems = new List<string>();
            Application app = Application.Current!;
            AppTheme theme = AppTheme.Of(app);
            var preference = new ContrastPreference(false, SystemContrastColors.Fallback);
            theme.Follow(preference);

            void Expect(string step, ThemeVariant variant)
            {
                Dispatcher.UIThread.RunJobs();
                if (app.RequestedThemeVariant != variant || app.ActualThemeVariant != variant)
                {
                    problems.Add($"{step}: the app requests {app.RequestedThemeVariant} and shows {app.ActualThemeVariant}, not {variant}");
                }
            }

            theme.Appearance = Appearance.Dark;
            Expect("Dark", ThemeVariant.Dark);
            preference.Set(true, SystemContrastColors.Desert);
            Expect("hcwhite over Dark", ContrastTheme.HighContrastLight);
            theme.Appearance = Appearance.Light;
            Expect("Light chosen under hcwhite", ContrastTheme.HighContrastLight);
            if (theme.Appearance != Appearance.Light)
            {
                problems.Add($"Light chosen under hcwhite: the theme keeps {theme.Appearance}");
            }

            preference.Set(true, SystemContrastColors.Hc1);
            Expect("hc1 after hcwhite", ContrastTheme.HighContrastDark);
            preference.Set(false, SystemContrastColors.Fallback);
            Expect("contrast off, the newest choice back", ThemeVariant.Light);

            preference.Set(true, SystemContrastColors.NightSky);
            Expect("hc2 over Light", ContrastTheme.HighContrastDark);
            theme.Appearance = Appearance.Dark;
            Expect("Dark chosen under hc2", ContrastTheme.HighContrastDark);
            preference.Set(false, SystemContrastColors.Fallback);
            Expect("contrast off, Dark back", ThemeVariant.Dark);

            // System under a contrast theme gives back Default, the system's own look (Light, headless).
            theme.Appearance = Appearance.System;
            preference.Set(true, SystemContrastColors.Hcblack);
            Expect("hcblack over System", ContrastTheme.HighContrastDark);
            preference.Set(false, SystemContrastColors.Fallback);
            Dispatcher.UIThread.RunJobs();
            if (app.RequestedThemeVariant != ThemeVariant.Default || app.ActualThemeVariant != ThemeVariant.Light)
            {
                problems.Add($"contrast off, System back: the app requests {app.RequestedThemeVariant} and shows {app.ActualThemeVariant}, not Default shown as Light");
            }

            return problems;
        });

        Assert.IsEmpty(failures, string.Join(Environment.NewLine, failures));
    }

    // One owner means one: no code of PaperRoute.Desktop but AppTheme sets a
    // theme variant, and none but AppTheme and the two readers it uses reads
    // the system's contrast state, except the two uses ThemeOwnership names
    // (the measurement's pinned window and the platform check's record). A
    // second writer or reader fails here, by file and line, and so does a
    // named exception that no longer matches anything.
    [TestMethod]
    public void OnlyAppThemeSetsTheVariantOrReadsTheSystemContrast()
    {
        IReadOnlyList<(string Path, string Text)> files = ThemeOwnership.AppFiles();
        (List<string> findings, List<string> allowed) = ThemeOwnership.Scan(files);
        TestContext.WriteLine($"Scanned {files.Count} files; allowed uses: {string.Join("; ", allowed)}");

        Assert.IsTrue(files.Any(file => file.Path == "App.axaml.cs") && files.Any(file => file.Path == "App.axaml"), "The scan did not read the app's own files.");
        Assert.Contains("Styles/AppTheme.cs: sets a theme variant", allowed, "The scan did not find AppTheme's own write.");
        Assert.Contains("Styles/AppTheme.cs: makes a contrast preference from the platform", allowed, "The scan did not find AppTheme's own reading.");
        CollectionAssert.AreEquivalent(ThemeOwnership.Exceptions.ToList(), allowed, "Every named exception must still match a use, and no use may be allowed without one. Allowed: " + string.Join("; ", allowed));
        Assert.IsEmpty(findings, "Code other than AppTheme sets a theme variant or reads the system's contrast state:" + Environment.NewLine + string.Join(Environment.NewLine, findings));
    }

    // The owner check's teeth: a view that sets its window's variant (plainly
    // and with ??=), an XAML attribute, a style's Setter, XAML's
    // property-element form, and a view model of the shell that asks Avalonia
    // for the contrast preference are each named; the same words in a
    // comment, a comparison, and AppTheme's own, are not.
    [TestMethod]
    public void TheOwnerCheckFindsASecondWriterAndASecondReader()
    {
        (string Path, string Text)[] files =
        [
            ("BoardView.axaml.cs", "namespace PaperRoute.Desktop;\r\n// RequestedThemeVariant = ThemeVariant.Dark is AppTheme's job.\r\npublic partial class BoardView\r\n{\r\n    void Pin(Window window) => window.RequestedThemeVariant = ThemeVariant.Dark;\r\n    void Keep(Window window) => window.RequestedThemeVariant ??= ThemeVariant.Dark;\r\n    bool IsDark(Window window) => window.RequestedThemeVariant == ThemeVariant.Dark;\r\n}\r\n"),
            ("MainWindow.axaml", "<Window xmlns=\"https://github.com/avaloniaui\"\r\n        <!-- RequestedThemeVariant=\"Dark\" -->\r\n        RequestedThemeVariant=\"Light\">\r\n  <Window.Styles>\r\n    <Style Selector=\"Border.region\">\r\n      <Setter Property=\"RequestedThemeVariant\" Value=\"Dark\" />\r\n    </Style>\r\n  </Window.Styles>\r\n  <Window.RequestedThemeVariant>Dark</Window.RequestedThemeVariant>\r\n</Window>\r\n"),
            ("Shell/NoticeStrip.cs", "bool high = Application.Current!.PlatformSettings!.GetColorValues().ContrastPreference == ColorContrastPreference.High;\r\n"),
            ("Styles/AppTheme.cs", "_application.RequestedThemeVariant = variant;\r\nFollow(ContrastPreference.FromPlatform(_application.PlatformSettings));\r\n"),
        ];

        (List<string> findings, List<string> allowed) = ThemeOwnership.Scan(files);

        CollectionAssert.AreEqual(
            new[]
            {
                "BoardView.axaml.cs:5: sets a theme variant: void Pin(Window window) => window.RequestedThemeVariant = ThemeVariant.Dark;",
                "BoardView.axaml.cs:6: sets a theme variant: void Keep(Window window) => window.RequestedThemeVariant ??= ThemeVariant.Dark;",
                "MainWindow.axaml:3: sets a theme variant: RequestedThemeVariant=\"Light\">",
                "MainWindow.axaml:6: sets a theme variant: <Setter Property=\"RequestedThemeVariant\" Value=\"Dark\" />",
                "MainWindow.axaml:9: sets a theme variant: <Window.RequestedThemeVariant>Dark</Window.RequestedThemeVariant>",
                "Shell/NoticeStrip.cs:1: reads the system's colour values: bool high = Application.Current!.PlatformSettings!.GetColorValues().ContrastPreference == ColorContrastPreference.High;",
            },
            findings,
            "Found:" + Environment.NewLine + string.Join(Environment.NewLine, findings));
        CollectionAssert.AreEqual(new[] { "Styles/AppTheme.cs: sets a theme variant", "Styles/AppTheme.cs: makes a contrast preference from the platform" }, allowed);
    }

    private static Color BackgroundOf(Border border) => ((ISolidColorBrush)border.Background!).Color;
}
