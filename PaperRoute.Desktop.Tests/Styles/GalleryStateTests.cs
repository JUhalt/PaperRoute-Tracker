using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PaperRoute.Desktop.Styles;
using PaperRoute.Desktop.Tests.Legibility;

namespace PaperRoute.Desktop.Tests.Styles;

// The style gallery's state specimens show their states (#463): each
// hovered, pressed, focused and off specimen carries its state's
// pseudo-class when it is drawn, and the part of it that shows the state
// holds the very brush object of the token its style names for that state,
// in all six theme cases. The gallery is shown as the sweeps show it
// (Sweep.Subjects) and read at the moment its picture is taken (after
// Pictures.Save's settling), so a state cleared after it was set fails here.
// It is also read at once, before any frame is drawn: a specimen that is
// still easing towards its state holds a brush of its own, and a transition
// runs on the real clock in a headless window too, so read only after the
// settling it would fail on a quick machine and pass on a busy one.
//
// Where a role's pressed brush is its hovered one (secondary, quiet, danger:
// Button.axaml), only the pseudo-class tells press from hover; the
// primary button (AccentPressedBrush) and the link (AccentSecondaryBrush)
// show it in a brush too.
[TestClass]
public sealed partial class GalleryStateTests
{
    private const string Presenter = "PART_ContentPresenter";

    // A part that is the specimen itself.
    private const string Itself = "";

    // The focus ring Avalonia draws in the adorner layer (FocusAdorner).
    private const string FocusRing = "focus ring";

    private static readonly StateSpecimen[] s_pressed =
    [
        new("SecondaryPressed", ":pressed", Presenter, ContentPresenter.BackgroundProperty, "HoverBackgroundBrush"),
        new("PrimaryPressed", ":pressed", Presenter, ContentPresenter.BackgroundProperty, ThemeTokens.AccentPressedBrush),
        new("QuietPressed", ":pressed", Presenter, ContentPresenter.BackgroundProperty, "HoverBackgroundBrush"),
        new("DangerPressed", ":pressed", Presenter, ContentPresenter.BackgroundProperty, "HoverBackgroundBrush"),
        new("LinkPressed", ":pressed", Presenter, ContentPresenter.ForegroundProperty, "AccentSecondaryBrush"),
    ];

    private static readonly StateSpecimen[] s_others =
    [
        new("SecondaryHovered", ":pointerover", Presenter, ContentPresenter.BackgroundProperty, "HoverBackgroundBrush"),
        new("PrimaryHovered", ":pointerover", Presenter, ContentPresenter.BackgroundProperty, ThemeTokens.AccentHoverBrush),
        new("QuietHovered", ":pointerover", Presenter, ContentPresenter.BackgroundProperty, "HoverBackgroundBrush"),
        new("DangerHovered", ":pointerover", Presenter, ContentPresenter.BackgroundProperty, "HoverBackgroundBrush"),
        new("QuietIconHovered", ":pointerover", Presenter, ContentPresenter.BackgroundProperty, "HoverBackgroundBrush"),
        new("LinkHovered", ":pointerover", Presenter, ContentPresenter.ForegroundProperty, "AccentBrush"),
        new("ChipNoTargetHovered", ":pointerover", Presenter, ContentPresenter.BackgroundProperty, "HoverBackgroundBrush"),
        new("ChipRevisionsDueHovered", ":pointerover", Presenter, ContentPresenter.BackgroundProperty, ThemeTokens.WarningChipHoverBrush),
        new("ChipRejectedHovered", ":pointerover", Presenter, ContentPresenter.BackgroundProperty, ThemeTokens.DangerChipHoverBrush),
        new("ChipWaitingHovered", ":pointerover", Presenter, ContentPresenter.BackgroundProperty, ThemeTokens.AccentChipHoverBrush),
        new("TabPublishedHovered", ":pointerover", "PART_LayoutRoot", TextElement.ForegroundProperty, "SecondaryTextBrush"),
        new("CardHovered", ":pointerover", Itself, Border.BorderBrushProperty, "AccentBrush"),
        new("Focused", ":focus-visible", FocusRing, Border.BorderBrushProperty, "AccentSecondaryBrush"),
        new("TextBoxFocused", ":focus", "PART_BorderElement", Border.BorderBrushProperty, "AccentBrush"),
        new("ComboFocused", ":focus-within", "Background", Border.BorderBrushProperty, "AccentBrush"),
        new("SecondaryOff", ":disabled", Presenter, ContentPresenter.BackgroundProperty, "BoardBackgroundBrush"),
        new("PrimaryOff", ":disabled", Presenter, ContentPresenter.BackgroundProperty, "BoardBackgroundBrush"),
        new("QuietOff", ":disabled", Presenter, ContentPresenter.ForegroundProperty, "MutedTextBrush"),
        new("DangerOff", ":disabled", Presenter, ContentPresenter.BackgroundProperty, "BoardBackgroundBrush"),
        new("ChipOff", ":disabled", Presenter, ContentPresenter.BackgroundProperty, "CardBackgroundBrush"),
        new("TextBoxOff", ":disabled", "PART_BorderElement", Border.BackgroundProperty, "BoardBackgroundBrush"),
        new("ComboOff", ":disabled", "Background", Border.BackgroundProperty, "BoardBackgroundBrush"),
    ];

    // The token brushes a part may hold, to name the one it holds instead.
    private static readonly string[] s_tokenKeys =
        [.. TokenFile.Colors("Light").Keys.Concat(ThemeTokens.DerivedKeys).Distinct().Order(StringComparer.Ordinal)];

    public TestContext TestContext { get; set; } = null!;

    // The guard of #463: every Pressed specimen carries :pressed when it is
    // drawn, and its pressed brush is the one drawn (before the fix the
    // primary button drew AccentHoverBrush and the link AccentBrush, their
    // hovered brushes, in every theme case).
    [TestMethod]
    public void EveryPressedSpecimenDrawsItsPressedBrush() => AssertStates(s_pressed);

    // The same check for every other state specimen: hovered, focused, off.
    [TestMethod]
    public void EveryHoveredFocusedAndOffSpecimenDrawsItsStatesBrush() => AssertStates(s_others);

    // No specimen is on its way to its state when the gallery is shown: read
    // at once, with no frame drawn and no time passed, each already holds its
    // state's brush. The first fix of #463 pressed its specimens after the
    // first layout and left the face's transition on, so the pressed brush
    // eased in over 100 ms of real time: its pictures and its own guard were
    // right on a busy PC and wrong on a quick one.
    [TestMethod]
    public void NoStateSpecimenIsStillEasingWhenItIsShown() => AssertStates([.. s_pressed, .. s_others], settle: false);

    // Every state specimen of the gallery (a control named for its state,
    // the column heads aside) is in one of the two lists, so a specimen added
    // later is checked too.
    [TestMethod]
    public void EveryStateSpecimenIsChecked()
    {
        List<string> named = Headless.Run(() =>
        {
            Window window = ShowGallery(ThemeCase.Light);
            try
            {
                return window.GetVisualDescendants().OfType<Control>()
                    .Where(control => control is not TextBlock && control.Name is { } name && StateName().IsMatch(name))
                    .Select(control => control.Name!)
                    .ToList();
            }
            finally
            {
                window.Close();
            }
        });

        List<string> listed = s_pressed.Concat(s_others).Select(specimen => specimen.Name).ToList();
        Assert.AreEqual(
            "",
            string.Join(", ", named.Except(listed).Order(StringComparer.Ordinal)),
            "State specimens of the gallery that no row here checks.");
        Assert.AreEqual(
            "",
            string.Join(", ", listed.Except(named).Order(StringComparer.Ordinal)),
            "Rows here that name a specimen the gallery does not show.");
        CollectionAssert.AreEquivalent(listed, named, "A state specimen is listed or shown twice.");
    }

    // The check's teeth, and the cause of #463 shown step by step: a primary
    // button given :pointerover and :pressed before it is shown, as the
    // gallery did, loses :pressed when its template is applied at the first
    // layout (Button.OnApplyTemplate sets it from IsPressed, which is false),
    // and draws its hovered brush. The check names both faults.
    [TestMethod]
    public void TheStateCheckFailsAButtonPressedBeforeItsTemplateIsApplied()
    {
        List<string> faults = Headless.Run(() =>
        {
            ThemeCase.Light.Apply(Application.Current!);
            var button = new Button { Name = "PrimaryPressed", Content = "+ Add Manuscript", Classes = { "primary" } };
            var pseudo = (IPseudoClasses)button.Classes;
            pseudo.Set(":pointerover", true);
            pseudo.Set(":pressed", true);
            var window = new Window { Width = 300, Height = 120, Content = button };
            window.Show();
            try
            {
                Settle(window);
                return Faults(window, ThemeCase.Light.Name, s_pressed.Where(specimen => specimen.Name == "PrimaryPressed"));
            }
            finally
            {
                window.Close();
            }
        });

        CollectionAssert.AreEqual(
            new[]
            {
                "Light: PrimaryPressed does not carry :pressed.",
                "Light: PrimaryPressed's PART_ContentPresenter holds AccentHoverBrush (#FF0E6A63) in Background, not AccentPressedBrush (#FF0C5E58).",
            },
            faults,
            "The check names the lost pseudo-class and the hovered brush:" + Environment.NewLine + string.Join(Environment.NewLine, faults));
    }

    // The teeth of the read at once: a primary button pressed after it is
    // shown, with its transition left on, as the first fix of #463 pressed
    // its specimens. Its face holds the easing's own brush, still in the
    // hovered colour, and the check names it.
    [TestMethod]
    public void TheStateCheckFailsAPressThatIsStillEasing()
    {
        List<string> faults = Headless.Run(() =>
        {
            ThemeCase.Light.Apply(Application.Current!);
            var button = new Button { Name = "PrimaryPressed", Content = "+ Add Manuscript", Classes = { "primary" } };
            var pseudo = (IPseudoClasses)button.Classes;
            pseudo.Set(":pointerover", true);
            var window = new Window { Width = 300, Height = 120, Content = button };
            window.Show();
            try
            {
                Dispatcher.UIThread.RunJobs();
                pseudo.Set(":pressed", true);
                return Faults(window, ThemeCase.Light.Name, s_pressed.Where(specimen => specimen.Name == "PrimaryPressed"));
            }
            finally
            {
                window.Close();
            }
        });

        CollectionAssert.AreEqual(
            new[]
            {
                "Light: PrimaryPressed's PART_ContentPresenter holds a brush of no token (#FF0E6A63) in Background, not AccentPressedBrush (#FF0C5E58).",
            },
            faults,
            "The check names the easing brush:" + Environment.NewLine + string.Join(Environment.NewLine, faults));
    }

    private void AssertStates(IReadOnlyList<StateSpecimen> specimens, bool settle = true)
    {
        var faults = new List<string>();
        foreach (ThemeCase themeCase in ThemeCase.All)
        {
            faults.AddRange(Headless.Run(() =>
            {
                Window window = ShowGallery(themeCase, settle);
                try
                {
                    return Faults(window, themeCase.Name, specimens);
                }
                finally
                {
                    window.Close();
                }
            }));
        }

        Assert.IsEmpty(faults, "State specimens that do not show their state:" + Environment.NewLine + string.Join(Environment.NewLine, faults));
        TestContext.WriteLine($"{specimens.Count} specimens show their states in {ThemeCase.All.Count} theme cases{(settle ? "" : ", read at once")}.");
    }

    // Shows the gallery as the sweeps do (Sweep.Read), in the theme case, and
    // lets it settle as Pictures.Save does before the picture is taken; or,
    // with settle off, runs only the jobs that showing it left (the focus
    // ring is made by one): no frame is drawn and no time is waited for.
    // Call inside Headless.Run.
    private static Window ShowGallery(ThemeCase themeCase, bool settle = true)
    {
        SweepSubject subject = Sweep.Subjects.Single(candidate => candidate.Name == "Styles/Gallery");
        themeCase.Apply(Application.Current!);
        Control view = subject.Create(null);
        var window = new Window { Width = subject.Width, Height = subject.Height, Content = view };
        window.Bind(Window.BackgroundProperty, window.GetResourceObservable("BoardBackgroundBrush"));
        window.Show();
        Dispatcher.UIThread.RunJobs();
        subject.Shown?.Invoke(view);
        if (settle)
        {
            Settle(window);
        }
        else
        {
            Dispatcher.UIThread.RunJobs();
        }

        return window;
    }

    // As Pictures.Save settles a window before its picture: jobs run and
    // frames are drawn. The ticks do not finish a transition (it runs on the
    // real clock), which is why the specimens are also read at once.
    private static void Settle(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(30);
        Dispatcher.UIThread.RunJobs();
        window.CaptureRenderedFrame()?.Dispose();
    }

    // What each specimen fails of its state, one line per fault.
    private static List<string> Faults(Window window, string themeCase, IEnumerable<StateSpecimen> specimens)
    {
        var faults = new List<string>();
        List<Control> controls = window.GetVisualDescendants().OfType<Control>().ToList();
        foreach (StateSpecimen specimen in specimens)
        {
            Control? control = controls.FirstOrDefault(candidate => candidate.Name == specimen.Name);
            if (control is null)
            {
                faults.Add($"{themeCase}: no specimen named {specimen.Name}.");
                continue;
            }

            if (!control.Classes.Contains(specimen.State))
            {
                faults.Add($"{themeCase}: {specimen.Name} does not carry {specimen.State}.");
            }

            Control? part = PartOf(control, specimen.Part);
            if (part is null)
            {
                faults.Add($"{themeCase}: {specimen.Name} has no {specimen.Part}.");
                continue;
            }

            object? held = part.GetValue(specimen.Property);
            object? expected = Token(part, specimen.Token);
            if (expected is null || !ReferenceEquals(held, expected))
            {
                string where = specimen.Part == Itself ? specimen.Name : $"{specimen.Name}'s {specimen.Part}";
                faults.Add($"{themeCase}: {where} holds {Describe(part, held)} in {specimen.Property.Name}, not {specimen.Token}{Colour(expected)}.");
            }
        }

        return faults;
    }

    private static Control? PartOf(Control control, string part) => part switch
    {
        Itself => control,
        FocusRing => AdornerLayer.GetAdornerLayer(control)?.Children
            .Where(adorner => AdornerLayer.GetAdornedElement(adorner) == control)
            .SelectMany(adorner => new[] { adorner }.Concat(adorner.GetVisualDescendants().OfType<Control>()))
            .OfType<Border>()
            .FirstOrDefault(),
        _ => control.GetVisualDescendants().OfType<Control>().FirstOrDefault(child => child.Name == part && child.TemplatedParent == control),
    };

    // The token's brush as the part's theme variant finds it, as a
    // {DynamicResource} on the part resolves it (and as Sweep names a
    // surface's token).
    private static object? Token(Control part, string key) =>
        part.TryFindResource(key, part.ActualThemeVariant, out object? found) ? found : null;

    // A held value by the token whose brush object it is, with its colour.
    private static string Describe(Control part, object? held) =>
        held is null
            ? "nothing"
            : (s_tokenKeys.FirstOrDefault(key => ReferenceEquals(Token(part, key), held)) ?? "a brush of no token") + Colour(held);

    // " (#AARRGGBB)" for a solid brush; nothing for anything else.
    private static string Colour(object? value) =>
        value is ISolidColorBrush { Color: var c } ? $" (#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2})" : "";

    // A specimen named for its state.
    [GeneratedRegex("(Hovered|Pressed|Focused|Off)$")]
    private static partial Regex StateName();

    // One state specimen: its name, the pseudo-class of its state, the part
    // that shows the state (a template part's name, Itself or FocusRing), and
    // the property of that part that must hold the token's brush.
    private sealed record StateSpecimen(string Name, string State, string Part, AvaloniaProperty Property, string Token);
}
