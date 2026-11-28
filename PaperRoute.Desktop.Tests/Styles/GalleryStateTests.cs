using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
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
//
// A specimen that eases into its state is caught another way, because no
// read can be trusted to catch it: by the time a brush is read the easing
// may have ended, or may have stopped moving for good, depending on how
// quick the machine is. So a watch records every value a transition writes
// to a specimen while the gallery is shown (NoStateSpecimenEasesIntoItsState).
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
        new("TabOff", ":disabled", "PART_LayoutRoot", TextElement.ForegroundProperty, "MutedTextBrush"),
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

    // No state specimen eases into its state: while the gallery is shown and
    // settled, no transition writes a value to a state specimen or to a part
    // of one. The first fix of #463 pressed its specimens after the first
    // layout and left the face's transition on, so the pressed brush eased
    // in. Its pictures and its own guard were then right or wrong by the
    // machine's speed: right on a busy PC, wrong on a quiet one.
    [TestMethod]
    public void NoStateSpecimenEasesIntoItsState()
    {
        string[] names = [.. s_pressed.Concat(s_others).Select(specimen => specimen.Name)];
        var eased = new List<string>();
        foreach (ThemeCase themeCase in ThemeCase.All)
        {
            eased.AddRange(Headless.Run(() =>
            {
                using var watch = new EasingWatch(names);
                Window window = ShowGallery(themeCase);
                try
                {
                    return watch.Eased.Select(line => $"{themeCase.Name}: {line}").ToList();
                }
                finally
                {
                    window.Close();
                }
            }));
        }

        Assert.IsEmpty(eased, "State specimens a transition wrote to while the gallery was shown:" + Environment.NewLine + string.Join(Environment.NewLine, eased));
        TestContext.WriteLine($"No transition wrote to any of {names.Length} state specimens in {ThemeCase.All.Count} theme cases.");
    }

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

    // The watch's teeth, and why it watches for the cause and reads no
    // moment: a primary button hovered after it is shown eases its face
    // (Button.axaml gives every button face a transition, and no rule takes
    // it away on a hover). Once the easing has ended the face holds its
    // hovered token's brush, so a read of the brush finds nothing. The watch
    // still names the easing, because it saw the easing's first value the
    // moment the state changed.
    [TestMethod]
    public void TheEasingWatchNamesAnEasingThatHasEnded()
    {
        (List<string> eased, List<string> faults, bool ended) = Headless.Run(() =>
        {
            ThemeCase.Light.Apply(Application.Current!);
            var button = new Button { Name = "PrimaryHovered", Content = "+ Add Manuscript", Classes = { "primary" } };
            var window = new Window { Width = 300, Height = 120, Content = button };
            window.Show();
            try
            {
                Dispatcher.UIThread.RunJobs();
                StateSpecimen[] hovered = [.. s_others.Where(specimen => specimen.Name == "PrimaryHovered")];
                using var watch = new EasingWatch(["PrimaryHovered"]);
                ((IPseudoClasses)button.Classes).Set(":pointerover", true);
                bool done = BoardWindow.WaitFor(() => Faults(window, ThemeCase.Light.Name, hovered).Count == 0, TimeSpan.FromSeconds(10));
                return (watch.Eased.ToList(), Faults(window, ThemeCase.Light.Name, hovered), done);
            }
            finally
            {
                window.Close();
            }
        });

        Assert.IsTrue(ended, "The hover's easing did not end within ten seconds:" + Environment.NewLine + string.Join(Environment.NewLine, faults));
        Assert.IsEmpty(faults, "Once the easing has ended, a read of the brush finds nothing.");
        CollectionAssert.AreEqual(
            new[] { "PrimaryHovered's ContentPresenter.Background" },
            eased,
            "The watch names the easing that has ended:" + Environment.NewLine + string.Join(Environment.NewLine, eased));
    }

    private void AssertStates(IReadOnlyList<StateSpecimen> specimens)
    {
        var faults = new List<string>();
        foreach (ThemeCase themeCase in ThemeCase.All)
        {
            faults.AddRange(Headless.Run(() =>
            {
                Window window = ShowGallery(themeCase);
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
        TestContext.WriteLine($"{specimens.Count} specimens show their states in {ThemeCase.All.Count} theme cases.");
    }

    // Shows the gallery as the sweeps do (Sweep.Read), in the theme case, and
    // lets it settle as Pictures.Save does before the picture is taken.
    // Call inside Headless.Run.
    private static Window ShowGallery(ThemeCase themeCase)
    {
        SweepSubject subject = Sweep.Subjects.Single(candidate => candidate.Name == "Styles/Gallery");
        themeCase.Apply(Application.Current!);
        Control view = subject.Create(null);
        var window = new Window { Width = subject.Width, Height = subject.Height, Content = view };
        window.Bind(Window.BackgroundProperty, window.GetResourceObservable("BoardBackgroundBrush"));
        window.Show();
        Dispatcher.UIThread.RunJobs();
        subject.Shown?.Invoke(view);
        Settle(window);
        return window;
    }

    // As Pictures.Save settles a window before its picture: jobs run and
    // frames are drawn. That cannot be relied on to finish a transition,
    // which is why no specimen may ease at all.
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

    // Records, from its making until it is disposed, every value a transition
    // writes to a brush of a control named in the set or of one of its
    // template parts: a change at animation priority. An easing writes its
    // first value the moment the state changes, so what the watch has seen
    // does not depend on how long anything takes afterwards, nor on whether
    // the easing is still running when someone looks.
    private sealed class EasingWatch : IObserver<AvaloniaPropertyChangedEventArgs>, IDisposable
    {
        // The brushes the rows read, and with them every brush the styles
        // ease (a background or a border).
        private static readonly AvaloniaProperty[] s_brushes =
        [
            .. s_pressed.Concat(s_others).Select(specimen => specimen.Property)
                .Concat([ContentPresenter.BackgroundProperty, Border.BackgroundProperty, Border.BorderBrushProperty])
                .Distinct(),
        ];

        private readonly HashSet<string> _names;
        private readonly List<IDisposable> _subscriptions;
        private readonly List<string> _eased = [];

        public EasingWatch(IEnumerable<string> names)
        {
            _names = [.. names];
            _subscriptions = [.. s_brushes.Select(property => property.Changed.Subscribe(this))];
        }

        // What eased, one line per specimen part and property.
        public IReadOnlyList<string> Eased => [.. _eased.Distinct()];

        public void OnNext(AvaloniaPropertyChangedEventArgs value)
        {
            if (value.Priority == BindingPriority.Animation && value.Sender is Control control && Specimen(control) is { } specimen)
            {
                _eased.Add($"{specimen}'s {control.GetType().Name}.{value.Property.Name}");
            }
        }

        public void OnCompleted()
        {
        }

        public void OnError(Exception error)
        {
        }

        public void Dispose()
        {
            foreach (IDisposable subscription in _subscriptions)
            {
                subscription.Dispose();
            }
        }

        // The specimen a control is, or is a template part of.
        private string? Specimen(Control control) =>
            control.Name is { } own && _names.Contains(own) ? own
            : control.TemplatedParent is Control { Name: { } parent } && _names.Contains(parent) ? parent
            : null;
    }
}
