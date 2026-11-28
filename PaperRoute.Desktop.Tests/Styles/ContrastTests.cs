using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ManuscriptPipeline.Models;
using PaperRoute.Desktop.Styles;
using PaperRoute.Desktop.Tests.Board;
using PaperRoute.TestFixtures;
using PaperRoute.ViewModels.Session;

namespace PaperRoute.Desktop.Tests.Styles;

// High contrast and the change of theme without a restart (d1 5.2, step 11):
// T4, T5, the contrast token map, and the pictures under two of Windows'
// contrast sets (SystemContrastColors.NightSky, hc2, dark, and Desert,
// hcwhite, light) for the maintainer to judge the outlines and the system
// colours by.
//
// Every test drives a ContrastPreference of its own: no test reads the
// contrast setting of this PC.
[TestClass]
public sealed class ContrastTests
{
    private static readonly Color s_lightCard = Color.Parse("#FFFFFF");
    private static readonly Color s_darkCard = Color.Parse("#282C33");
    private static readonly Color s_lightCardBorder = Color.Parse("#DAE4E7");

    private string _root = string.Empty;

    public TestContext TestContext { get; set; } = null!;

    [TestInitialize]
    public void CreateRoot()
    {
        _root = Path.Combine(Path.GetTempPath(), "PaperRoute-TrialContrast-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    [TestCleanup]
    public void DeleteRoot()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // Left for Windows' temporary-file clean-up.
        }
        catch (UnauthorizedAccessException)
        {
            // Left for Windows' temporary-file clean-up.
        }
    }

    // T4: switching RequestedThemeVariant from Light to Dark changes a shown
    // card's background at once, on the same card view (nothing rebuilt).
    [TestMethod]
    public void T4_ACardFollowsTheThemeWithoutARestart()
    {
        var session = new MemorySession(Showcase(), Papers.Today);
        (Color before, Color after, bool same) = Headless.Run(() =>
        {
            using BoardWindow board = BoardWindow.Show(session, ThemeVariant.Light);
            CardView card = board.Cards("PipelineShelf")[0];
            Color light = BackgroundOf(card.Part<Border>("Card"));
            board.Window.RequestedThemeVariant = ThemeVariant.Dark;
            Dispatcher.UIThread.RunJobs();
            CardView again = board.Cards("PipelineShelf")[0];
            return (light, BackgroundOf(again.Part<Border>("Card")), ReferenceEquals(card, again));
        });

        Assert.AreEqual(s_lightCard, before, "In Light a card is CardBackground #FFFFFF.");
        Assert.AreEqual(s_darkCard, after, "After the switch to Dark the same card is #282C33.");
        Assert.IsTrue(same, "The switch rebuilt the card view.");
    }

    // A contrast theme going on, changing, and going off, all while the Board
    // is shown: each step reaches the same card at once (today a Board built
    // before the switch keeps its colours until a restart, r1 5.1).
    [TestMethod]
    public void AContrastThemeGoesOnChangesAndGoesOffWithoutARestart()
    {
        var session = new MemorySession(Showcase(), Papers.Today);
        List<string> failures = Headless.Run(() =>
        {
            var problems = new List<string>();
            Application app = Application.Current!;
            app.RequestedThemeVariant = ThemeVariant.Light;
            var preference = new ContrastPreference(false, SystemContrastColors.Fallback);
            ContrastTheme.Follow(app, preference);

            using BoardWindow board = BoardWindow.Show(session);
            CardView card = board.Cards("PipelineShelf")[0];
            Border face = card.Part<Border>("Card");
            Border pill = card.Part<Border>("StagePill");
            Button more = card.Part<Button>("More");

            void Expect(string step, Color background, Color border, double outline, Color quietBorder)
            {
                // Back in Light the card's 100 ms border ease returns (Card.axaml), and
                // it runs on the clock: wait for it to end, up to a second.
                Dispatcher.UIThread.RunJobs();
                BoardWindow.WaitFor(() => face.BorderBrush is ISolidColorBrush { } now && now.Color == border, TimeSpan.FromSeconds(1));
                if (!ReferenceEquals(card, board.Cards("PipelineShelf")[0]))
                {
                    problems.Add($"{step}: the card view was rebuilt");
                }

                Check(problems, step + ": card background", background, BackgroundOf(face));
                Check(problems, step + ": card border", border, ((ISolidColorBrush)face.BorderBrush!).Color);
                if (pill.BorderThickness != new Thickness(outline))
                {
                    problems.Add($"{step}: the stage pill's outline is {pill.BorderThickness}, not {outline}");
                }

                Check(problems, step + ": the quiet ⋯ button's border", quietBorder, ((ISolidColorBrush)more.BorderBrush!).Color);
            }

            Expect("Light", s_lightCard, s_lightCardBorder, 0, Colors.Transparent);

            preference.Set(true, SystemContrastColors.NightSky);
            Expect("Night sky", SystemContrastColors.NightSky.Window, SystemContrastColors.NightSky.WindowText, 1, SystemContrastColors.NightSky.WindowText);
            if (app.ActualThemeVariant != ContrastTheme.HighContrastDark)
            {
                problems.Add($"Night sky: the app's variant is {app.ActualThemeVariant}, not HighContrastDark");
            }

            preference.Set(true, SystemContrastColors.Desert);
            Expect("Desert", SystemContrastColors.Desert.Window, SystemContrastColors.Desert.WindowText, 1, SystemContrastColors.Desert.WindowText);
            if (app.ActualThemeVariant != ContrastTheme.HighContrastLight)
            {
                problems.Add($"Desert: the app's variant is {app.ActualThemeVariant}, not HighContrastLight");
            }

            // Another light set: the same variant, so only the brushes' colours change.
            SystemContrastColors paler = SystemContrastColors.Desert with { Window = Color.Parse("#F5F5DC"), WindowText = Color.Parse("#202020") };
            preference.Set(true, paler);
            Expect("A second light set", paler.Window, paler.WindowText, 1, paler.WindowText);

            preference.Set(false, SystemContrastColors.Fallback);
            Expect("Light again", s_lightCard, s_lightCardBorder, 0, Colors.Transparent);
            if (app.ActualThemeVariant != ThemeVariant.Light)
            {
                problems.Add($"Light again: the app's variant is {app.ActualThemeVariant}, not Light");
            }

            return problems;
        });

        Assert.IsEmpty(failures, string.Join(Environment.NewLine, failures));
    }

    // T5: under a contrast variant, pills, tags and chips are outlined, 1 px,
    // in their own ink (PillLabel.vb:31-46, TagChips.vb:32, FilterChip.vb:197-226);
    // in Light the toned ones have no outline. The colours come from the
    // dictionary ContrastTheme made in code, through DynamicResource: the risk
    // of d1 9 did not happen, so no merged dictionary is swapped in.
    [TestMethod]
    public void T5_PillsTagsAndChipsAreOutlinedInTheirInkUnderHighContrast()
    {
        List<string> failures = Headless.Run(() =>
        {
            var problems = new List<string>();
            foreach (SystemContrastColors colors in new[] { SystemContrastColors.NightSky, SystemContrastColors.Desert })
            {
                string set = colors.IsDark ? "Night sky" : "Desert";
                Application app = Application.Current!;
                app.RequestedThemeVariant = ThemeVariant.Light;
                var preference = new ContrastPreference(false, SystemContrastColors.Fallback);
                ContrastTheme.Follow(app, preference);
                (Window window, List<Border> pills, List<Border> tags, List<Button> chips) = ShowFamilies();
                try
                {
                    foreach (Border toned in pills.Concat(tags))
                    {
                        if (toned.BorderThickness != default(Thickness))
                        {
                            problems.Add($"Light: {Describe(toned)} has an outline of {toned.BorderThickness}");
                        }
                    }

                    preference.Set(true, colors);
                    Dispatcher.UIThread.RunJobs();
                    foreach (Border border in pills.Concat(tags))
                    {
                        // The accent pill's ink is the accent, the selection colour; every other ink is the window text.
                        TextBlock text = (TextBlock)border.Child!;
                        Color ink = border.Classes.Contains("accent") ? colors.Highlight : colors.WindowText;
                        ExpectOutline(problems, $"{set}: {Describe(border)}", border.BorderThickness, border.BorderBrush, text.Foreground, ink);
                    }

                    foreach (Button chip in chips)
                    {
                        ContentPresenter face = TemplatePart<ContentPresenter>(chip, "PART_ContentPresenter");
                        Color ink = chip.Classes.Contains("active") ? colors.Highlight : colors.WindowText;
                        ExpectOutline(problems, $"{set}: chip {chip.Content}", face.BorderThickness, face.BorderBrush, face.Foreground, ink);
                        Check(problems, $"{set}: chip {chip.Content} fill", colors.Window, ((ISolidColorBrush)face.Background!).Color);
                    }
                }
                finally
                {
                    window.Close();
                }
            }

            return problems;
        });

        Assert.IsEmpty(failures, string.Join(Environment.NewLine, failures));
    }

    // Every token of Tokens.axaml has a system colour under a contrast theme
    // (and OutlineThickness 1), and the app resolves each to it in both
    // contrast variants, as UiTheme.vb maps them.
    [TestMethod]
    public void EveryTokenTakesItsSystemColourUnderAContrastTheme()
    {
        IReadOnlySet<string> lightKeys = TokenFile.ThemedKeys()["Light"];
        string[] missing = lightKeys.Where(key => key != "OutlineThickness" && !ContrastTheme.TokenRoles.ContainsKey(key)).ToArray();
        string[] extra = ContrastTheme.TokenRoles.Keys.Where(key => !lightKeys.Contains(key)).ToArray();
        Assert.IsEmpty(missing, "Tokens with no system colour: " + string.Join(", ", missing));
        Assert.IsEmpty(extra, "System colours for keys Tokens.axaml does not have: " + string.Join(", ", extra));

        List<string> failures = Headless.Run(() =>
        {
            var problems = new List<string>();
            Application app = Application.Current!;
            foreach (SystemContrastColors colors in new[] { SystemContrastColors.NightSky, SystemContrastColors.Desert })
            {
                ThemeVariant variant = ContrastTheme.Fill(app.Resources, colors);
                IReadOnlyDictionary<string, Color> expected = ContrastTheme.Colors(colors);
                foreach (string key in lightKeys.Where(key => key != "OutlineThickness").Concat(ThemeTokens.DerivedKeys))
                {
                    if (!app.TryGetResource(key, variant, out object? value) || value is not ISolidColorBrush brush)
                    {
                        problems.Add($"{variant}: {key} does not resolve to a solid brush");
                    }
                    else if (brush.Color != expected[key])
                    {
                        problems.Add($"{variant}: {key} is {brush.Color}, not {expected[key]}");
                    }
                }

                if (!app.TryGetResource("OutlineThickness", variant, out object? outline) || outline is not Thickness thickness || thickness != new Thickness(1))
                {
                    problems.Add($"{variant}: OutlineThickness is not 1");
                }

                // A Fluent resource falls back to the variant the contrast one inherits.
                ThemeVariant inherited = colors.IsDark ? ThemeVariant.Dark : ThemeVariant.Light;
                if (!app.TryGetResource("SystemControlForegroundBaseHighBrush", variant, out object? fluent)
                    || !app.TryGetResource("SystemControlForegroundBaseHighBrush", inherited, out object? fluentInherited)
                    || ((ISolidColorBrush)fluent!).Color != ((ISolidColorBrush)fluentInherited!).Color)
                {
                    problems.Add($"{variant}: Fluent's SystemControlForegroundBaseHighBrush does not fall back to {inherited}");
                }
            }

            return problems;
        });

        Assert.IsEmpty(failures, string.Join(Environment.NewLine, failures));

        // Spot checks against UiTheme.vb's contrast branches.
        IReadOnlyDictionary<string, Color> night = ContrastTheme.Colors(SystemContrastColors.NightSky);
        Assert.AreEqual(SystemContrastColors.NightSky.GrayText, night["MutedTextBrush"], "MutedText is GrayText (UiTheme.vb:115-117).");
        Assert.AreEqual(SystemContrastColors.NightSky.HotTrack, night["AccentSecondaryBrush"], "AccentSecondary is HotTrack (UiTheme.vb:325-327).");
        Assert.AreEqual(SystemContrastColors.NightSky.Window, night["WarningChipHoverBrush"], "A toned chip's hover is the window colour: no hover fill.");
        Assert.AreEqual(ThemeTokens.Blend(SystemContrastColors.NightSky.Highlight, Colors.Black, 0.1F), night["AccentHoverBrush"],
            "The primary hover darkens towards black, since IsDark is never true under contrast (ActionButton.vb:224).");

        // The pressed shade lightens on a dark set and darkens on a light one
        // (the harness design's question 10).
        Assert.AreEqual(ThemeTokens.Blend(SystemContrastColors.NightSky.Highlight, Colors.White, 0.2F), night["AccentPressedBrush"],
            "On a dark set the primary button's pressed shade lightens towards white.");
        Assert.AreEqual(ThemeTokens.Blend(SystemContrastColors.Desert.Highlight, Colors.Black, 0.2F), ContrastTheme.Colors(SystemContrastColors.Desert)["AccentPressedBrush"],
            "On a light set the primary button's pressed shade darkens towards black, as before.");
    }

    // The colour arithmetic of SystemContrastColors and the preference's event.
    [TestMethod]
    public void SystemColoursUnpackAndThePreferenceReportsOnlyChanges()
    {
        Assert.AreEqual(Color.FromRgb(0x11, 0x22, 0x33), SystemContrastColors.FromColorRef(0x00332211u), "A COLORREF is 0x00BBGGRR.");
        Assert.IsTrue(SystemContrastColors.NightSky.IsDark);
        Assert.IsFalse(SystemContrastColors.Desert.IsDark);
        Assert.IsFalse(SystemContrastColors.Fallback.IsDark);
        Assert.AreEqual(ContrastTheme.HighContrastDark, ContrastTheme.VariantFor(SystemContrastColors.NightSky));
        Assert.AreEqual(ContrastTheme.HighContrastLight, ContrastTheme.VariantFor(SystemContrastColors.Desert));
        Assert.AreEqual(ThemeVariant.Dark, ContrastTheme.HighContrastDark.InheritVariant);
        Assert.AreEqual(ThemeVariant.Light, ContrastTheme.HighContrastLight.InheritVariant);

        var preference = new ContrastPreference(false, SystemContrastColors.Fallback);
        int changes = 0;
        preference.Changed += (_, _) => changes++;
        preference.Set(false, SystemContrastColors.Fallback);
        Assert.AreEqual(0, changes, "Nothing changed, so no Changed.");
        preference.Set(true, SystemContrastColors.NightSky);
        preference.Set(true, SystemContrastColors.NightSky);
        preference.Set(true, SystemContrastColors.Desert);
        Assert.AreEqual(2, changes, "On, then a new set: two changes.");
        Assert.IsTrue(preference.IsHigh);
        Assert.AreEqual(SystemContrastColors.Desert, preference.Colors);
    }

    // The Fluent parts Contrast.axaml restyles reach the system colours: the
    // drop-down's popup and its chosen item, and a text box's selection.
    [TestMethod]
    public void TheDropDownListAndTextSelectionTakeTheSystemColours()
    {
        SystemContrastColors colors = SystemContrastColors.NightSky;
        List<string> failures = Headless.Run(() =>
        {
            var problems = new List<string>();
            ContrastTheme.Follow(Application.Current!, new ContrastPreference(true, colors));
            var combo = new ComboBox { Width = 200 };
            combo.Items.Add("All stages");
            combo.Items.Add("Draft");
            combo.SelectedIndex = 0;
            var box = new TextBox { Width = 200, Text = "drought" };
            var window = new Window { Width = 400, Height = 300, Content = new StackPanel { Children = { combo, box } } };
            window.Show();
            try
            {
                combo.IsDropDownOpen = true;
                Dispatcher.UIThread.RunJobs();
                Popup popup = combo.GetVisualDescendants().OfType<Popup>().Single(part => part.TemplatedParent == combo);
                if (popup.Child is not Border { Name: "PopupBorder" } border)
                {
                    problems.Add($"The drop-down's popup holds {popup.Child?.GetType().Name}#{popup.Child?.Name}, not Border#PopupBorder");
                }
                else
                {
                    Check(problems, "popup background", colors.Window, ((ISolidColorBrush)border.Background!).Color);
                    Check(problems, "popup border", colors.WindowText, ((ISolidColorBrush)border.BorderBrush!).Color);
                }

                if (combo.ContainerFromIndex(0) is not ComboBoxItem chosen || combo.ContainerFromIndex(1) is not ComboBoxItem other)
                {
                    problems.Add("The open list made no item containers");
                }
                else
                {
                    ContentPresenter chosenFace = TemplatePart<ContentPresenter>(chosen, "PART_ContentPresenter");
                    ContentPresenter otherFace = TemplatePart<ContentPresenter>(other, "PART_ContentPresenter");
                    Check(problems, "chosen item background", colors.Highlight, ((ISolidColorBrush)chosenFace.Background!).Color);
                    Check(problems, "chosen item text", colors.HighlightText, ((ISolidColorBrush)chosenFace.Foreground!).Color);
                    Check(problems, "other item background", colors.Window, ((ISolidColorBrush)otherFace.Background!).Color);
                    Check(problems, "other item text", colors.WindowText, ((ISolidColorBrush)otherFace.Foreground!).Color);
                }

                combo.IsDropDownOpen = false;
                Check(problems, "text selection", colors.Highlight, ((ISolidColorBrush)box.SelectionBrush!).Color);
                Check(problems, "selected text", colors.HighlightText, ((ISolidColorBrush)box.SelectionForegroundBrush!).Color);
            }
            finally
            {
                window.Close();
            }

            return problems;
        });

        Assert.IsEmpty(failures, string.Join(Environment.NewLine, failures));
    }

    // ---- Pictures ----

    [TestMethod]
    public void TheStyleGalleryIsSavedUnderNightSky() => SaveGallery(SystemContrastColors.NightSky, "style-gallery-contrast-night-sky");

    [TestMethod]
    public void TheStyleGalleryIsSavedUnderDesert() => SaveGallery(SystemContrastColors.Desert, "style-gallery-contrast-desert");

    [TestMethod]
    public void TheExampleBoardIsSavedUnderNightSky() => SaveExample(SystemContrastColors.NightSky, "board-example-contrast-night-sky");

    [TestMethod]
    public void TheExampleBoardIsSavedUnderDesert() => SaveExample(SystemContrastColors.Desert, "board-example-contrast-desert");

    [TestMethod]
    public void CardsOfEveryToneAreSavedUnderNightSky() => SaveCards(SystemContrastColors.NightSky, "cards-contrast-night-sky");

    [TestMethod]
    public void CardsOfEveryToneAreSavedUnderDesert() => SaveCards(SystemContrastColors.Desert, "cards-contrast-desert");

    private void SaveGallery(SystemContrastColors colors, string name)
    {
        string path = Headless.Run(() =>
        {
            ContrastTheme.Follow(Application.Current!, new ContrastPreference(true, colors));
            var gallery = new StyleGallery();
            var window = new Window { Width = 1280, Height = 900, Content = gallery };
            window.Bind(Window.BackgroundProperty, window.GetResourceObservable("BoardBackgroundBrush"));
            window.Show();
            try
            {
                gallery.FocusTheFocusedButton();
                return Pictures.Save(window, name, TestContext);
            }
            finally
            {
                window.Close();
            }
        });

        Assert.IsTrue(File.Exists(path), $"No picture at {path}.");
    }

    private void SaveExample(SystemContrastColors colors, string name)
    {
        LargeLibraryFixture.WriteExample(_root);
        using var session = new DiskLibrarySession(
            Path.Combine(_root, "data"),
            Path.Combine(_root, "library"),
            _root,
            LibraryKind.Example,
            LargeLibraryFixture.Today);
        session.LoadAsync(CancellationToken.None).GetAwaiter().GetResult();
        Assert.HasCount(7, session.Manuscripts);

        string path = Headless.Run(() =>
        {
            ContrastTheme.Follow(Application.Current!, new ContrastPreference(true, colors));
            using BoardWindow board = BoardWindow.Show(session);
            return Pictures.Save(board.Window, name, TestContext);
        });

        Assert.IsTrue(File.Exists(path), $"No picture at {path}.");
    }

    // The chips and the cards close up, with the first card's title focused.
    private void SaveCards(SystemContrastColors colors, string name)
    {
        var session = new MemorySession(Showcase(), Papers.Today);
        string path = Headless.Run(() =>
        {
            ContrastTheme.Follow(Application.Current!, new ContrastPreference(true, colors));
            using BoardWindow board = BoardWindow.Show(session, height: 1000);
            Assert.IsTrue(board.Shelf("PipelineShelf").FocusFirstCard(), "The shelf shows a card.");
            Dispatcher.UIThread.RunJobs();
            Panel host = board.Find<Panel>("ShelfHost");
            ItemsControl chips = board.Find<ItemsControl>("Chips");
            Point top = chips.TranslatePoint(default, board.Window) ?? default;
            Point origin = host.TranslatePoint(default, board.Window) ?? default;
            // The Board's own part only: from the attention chips down, right of the rail.
            int left = (int)Math.Min(origin.X, top.X) - 8;
            var region = new PixelRect(left, (int)top.Y - 8, (int)board.Window.Bounds.Width - left, (int)(origin.Y + host.Bounds.Height - top.Y + 8));
            return Pictures.Save(board.Window, name, TestContext, region);
        });

        Assert.IsTrue(File.Exists(path), $"No picture at {path}.");
    }

    // ---- Helpers ----

    // A window of pills (every stage and the rail's counts), tags (every
    // palette) and chips (every tone), as the families style them.
    private static (Window Window, List<Border> Pills, List<Border> Tags, List<Button> Chips) ShowFamilies()
    {
        var pills = new List<Border>();
        foreach (string cls in new[] { "stage-idea", "stage-draft", "stage-submitted", "stage-under-review", "stage-revision", "stage-accepted", "stage-in-press", "stage-published", "accent", "warning", "danger" })
        {
            var pill = new Border { Child = new TextBlock { Text = cls } };
            pill.Classes.Add("pill");
            pill.Classes.Add(cls);
            pills.Add(pill);
        }

        var tags = new List<Border>();
        foreach (string palette in new[] { "teal", "blue", "violet", "rose", "amber", "green", "slate", "orange" })
        {
            tags.Add(new Border { Classes = { "tag", "tag-" + palette }, Child = new TextBlock { Text = palette } });
        }

        var chips = new List<Button>();
        foreach (string[] classes in new string[][] { [], ["warning"], ["danger"], ["warning", "active"] })
        {
            var chip = new Button { Content = "chip " + string.Join(" ", classes) };
            chip.Classes.Add("chip");
            chip.Classes.AddRange(classes);
            chips.Add(chip);
        }

        var panel = new WrapPanel();
        foreach (Control control in pills.Concat<Control>(tags).Concat(chips))
        {
            control.Margin = new Thickness(4);
            panel.Children.Add(control);
        }

        var window = new Window { Width = 900, Height = 300, Content = panel };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return (window, pills, tags, chips);
    }

    private static void ExpectOutline(List<string> problems, string what, Thickness thickness, IBrush? border, IBrush? ink, Color expectedInk)
    {
        if (thickness != new Thickness(1))
        {
            problems.Add($"{what}: outline {thickness}, not 1");
        }

        if (border is not ISolidColorBrush outline || ink is not ISolidColorBrush text)
        {
            problems.Add($"{what}: outline or ink is not a solid brush");
            return;
        }

        if (outline.Color != text.Color)
        {
            problems.Add($"{what}: outline {outline.Color} is not its ink {text.Color}");
        }

        Check(problems, what + " ink", expectedInk, text.Color);
    }

    private static void Check(List<string> problems, string what, Color expected, Color actual)
    {
        if (expected != actual)
        {
            problems.Add($"{what} is {actual}, not {expected}");
        }
    }

    // A named part of a control's template, found among its visual descendants.
    private static T TemplatePart<T>(TemplatedControl control, string name)
        where T : Control =>
        control.GetVisualDescendants().OfType<T>().FirstOrDefault(part => part.TemplatedParent == control && part.Name == name)
        ?? throw new AssertFailedException($"{control.GetType().Name} has no {typeof(T).Name}#{name} in its template.");

    private static Color BackgroundOf(Border border) => ((ISolidColorBrush)border.Background!).Color;

    private static string Describe(Border border) => string.Join(".", border.Classes);

    // Pipeline cards of every stage up to In Press and every tone, with tags.
    private static List<Manuscript> Showcase()
    {
        Manuscript idea = Papers.Paper("An idea with no target journal yet", PaperStage.Idea, journal: string.Empty, stageDaysAgo: 3);
        Manuscript tagged = Papers.Paper("A draft with tags in two palettes", PaperStage.Draft, journal: "Fictional Open Psychology", stageDaysAgo: 12);
        tagged.Tags = ["teaching", "replication", "open science"];
        Manuscript waiting = Papers.Waiting("Under review for four months", 120);
        waiting.TargetJournal = "Fictional Assessment Quarterly";
        Manuscript dueSoon = Papers.Revision("A major revision due in five days", 5);
        dueSoon.TargetJournal = "Fictional Journal of Research Methods";
        Manuscript overdue = Papers.Revision("A revision three days overdue", -3);
        overdue.TargetJournal = "Fictional Journal of Research Methods";
        Manuscript rejected = Papers.Rejected("Rejected after review last week", 4);
        rejected.TargetJournal = "Fictional Open Psychology";
        Manuscript accepted = Papers.Paper("Accepted, waiting for proofs", PaperStage.Accepted, journal: "Fictional Open Psychology", stageDaysAgo: 20)
            .With(Papers.Submission(300, (EditorialDecision.DeskRejected, 290, null)), Papers.Submission(200, (EditorialDecision.Accepted, 20, null)));
        Manuscript inPress = Papers.Paper("In press at last", PaperStage.InPress, journal: "Fictional Journal of Research Methods", stageDaysAgo: 8)
            .With(Papers.Submission(260, (EditorialDecision.Accepted, 40, null)));
        return [idea, tagged, waiting, dueSoon, overdue, rejected, accepted, inPress];
    }
}
