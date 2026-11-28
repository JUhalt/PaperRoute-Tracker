using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PaperRoute.Desktop.Styles;
using PaperRoute.Desktop.Tests.Styles;
using PaperRoute.TestFixtures;
using PaperRoute.ViewModels.Session;

namespace PaperRoute.Desktop.Tests.Legibility;

// One surface a sweep shows: its name, the library its factory is built over
// (none for the gallery), the window's size, the factory (called on the
// interface thread) and what to do once the window is shown.
internal sealed record SweepSubject(string Name, string? Library, double Width, double Height, Func<ILibrarySession?, Control> Create, Action<Control>? Shown = null);

// What a sweep reads from one subject in one theme case: every part that
// paints a background (R5's input), every text drawn in full (R7's), and how
// many texts were left out because a scroller or the window's edge hides
// part of them; every disabled text drawn in full (R8's) and every border
// drawn in full (R9's edges), with how many of each were left out the same
// way; and, when the sweep is asked to walk the focus, what taking focus
// changes around each focusable control (R9's focus).
internal sealed record SweepFrame(string Subject, string ThemeCase, IReadOnlyList<PaintedPart> Parts, IReadOnlyList<DrawnText> Texts, int HiddenTexts)
{
    public IReadOnlyList<DrawnText> DisabledTexts { get; init; } = [];

    public int HiddenDisabledTexts { get; init; }

    public IReadOnlyList<DrawnEdge> Edges { get; init; } = [];

    public int HiddenEdges { get; init; }

    public IReadOnlyList<FocusChange> Focus { get; init; } = [];
}

// The sweeps (the harness design, part 2.1): one rule run over every surface
// the harness can build, in its theme cases. Sweep shows a subject in a
// window, in a theme case, saves its picture (sweep-<subject>-<case>.png),
// reads the frame and walks the window's visual tree, handing each rule its
// inputs. Saving the picture draws frames, which cannot be relied on to
// finish a transition (Pictures.Save), so nothing a sweep shows may be
// easing. The rules (Rules.R5, Rules.R7, Rules.R8, Rules.R9) judge; the sweep
// only reads.
//
// The subjects are every built target surface (TargetSurfaceCatalog, today
// the Board on the example library) and the style gallery (part 2.4).
// Limits of headless: HeadlessLimits.md
internal static class Sweep
{
    // The width of the frame around a text whose commonest colour is the
    // text's surface (R7, part 2.4): 2 px, so large bold text cannot become
    // its own surface.
    public const int SurfaceFrame = 2;

    // The token brushes a text's ink or surface may be: every brush of the
    // token file and every derived brush, in one fixed order.
    private static readonly string[] s_tokenKeys =
        [.. TokenFile.Colors("Light").Keys.Concat(ThemeTokens.DerivedKeys).Distinct().Order(StringComparer.Ordinal)];

    public static IReadOnlyList<SweepSubject> Subjects { get; } =
    [
        .. TargetSurfaceCatalog.Surfaces.Select(surface =>
            new SweepSubject(surface.Key, surface.Library, 1280, 800, session => surface.Create(session ?? throw new InvalidOperationException($"{surface.Key} needs a library.")))),
        new SweepSubject("Styles/Gallery", null, 1280, 900, _ => new StyleGallery(), view => ((StyleGallery)view).FocusTheFocusedButton()),
    ];

    // How far around a focusable control R9 looks for what taking focus
    // changes (part 2.4): its bounds plus 4 px on every side, where a focus
    // ring drawn outside the control would show.
    public const int FocusReach = 4;

    // Shows the subject in the theme case and reads it, in one Headless.Run
    // (a fresh application, so no theme case outlives its test), over a
    // library of its own when it needs one. With walkFocus (R9, under a
    // contrast case only), it then gives each focusable control the keyboard
    // focus in turn and reads what changes.
    public static SweepFrame Read(SweepSubject subject, ThemeCase themeCase, TestContext context, bool walkFocus = false)
    {
        ArgumentNullException.ThrowIfNull(subject);
        ArgumentNullException.ThrowIfNull(themeCase);
        using DiskLibrarySession? session = subject.Library switch
        {
            null => null,
            TargetSurfaceCatalog.ExampleLibrary => OpenExample(),
            _ => throw new InvalidOperationException($"{subject.Name}: no test library is known by \"{subject.Library}\"."),
        };

        string picture = $"sweep-{subject.Name.Replace('/', '-').ToLowerInvariant()}-{themeCase.Name.ToLowerInvariant()}";
        return Headless.Run(() =>
        {
            themeCase.Apply(Application.Current!);
            Control view = subject.Create(session);
            var window = new Window { Width = subject.Width, Height = subject.Height, Content = view };
            window.Bind(Window.BackgroundProperty, window.GetResourceObservable("BoardBackgroundBrush"));
            window.Show();
            try
            {
                Dispatcher.UIThread.RunJobs();
                subject.Shown?.Invoke(view);
                Pictures.Save(window, picture, context);
                FramePixels frame = FramePixels.Capture(window);
                IReadOnlyList<PaintedPart> parts = Parts(window, frame, subject.Name, themeCase.Name);
                (IReadOnlyList<DrawnText> texts, int hidden) = Texts(window, frame, subject.Name, themeCase.Name, disabled: false);
                (IReadOnlyList<DrawnText> disabledTexts, int hiddenDisabled) = Texts(window, frame, subject.Name, themeCase.Name, disabled: true);
                (IReadOnlyList<DrawnEdge> edges, int hiddenEdges) = Edges(window, frame, subject.Name, themeCase.Name);
                return new SweepFrame(subject.Name, themeCase.Name, parts, texts, hidden)
                {
                    DisabledTexts = disabledTexts,
                    HiddenDisabledTexts = hiddenDisabled,
                    Edges = edges,
                    HiddenEdges = hiddenEdges,
                    Focus = walkFocus ? WalkFocus(window, subject.Name, themeCase) : [],
                };
            }
            finally
            {
                window.Close();
            }
        });
    }

    // R5's input: every visible control that paints a background, the
    // window included. Its brush, laid over what lies beneath it when the
    // brush is not opaque, names the cause; the commonest colour of its own
    // pixels (its shown bounds less its visible children's) catches a
    // template part or an opacity.
    //
    // A shape (Ellipse, Rectangle, Path) paints a Fill, not a background, so
    // it is not a part and R5 does not judge it: a light route dot or icon in
    // Dark would pass. As a visible child, its bounds are also left out of
    // its parent's own pixels. The design's words, "paints a background",
    // allow this; a later rule that judges fills adds a Shape case to
    // BackgroundOf.
    private static List<PaintedPart> Parts(Window window, FramePixels frame, string subject, string themeCase)
    {
        var parts = new List<PaintedPart>();
        var seen = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (Control control in new Control[] { window }.Concat(window.GetVisualDescendants().OfType<Control>()))
        {
            if (!control.IsEffectivelyVisible || BackgroundOf(control) is not { } brush || !Paints(brush) || Shown(control, window, frame) is not { } shown)
            {
                continue;
            }

            Color? laid = brush is ISolidColorBrush solid ? Over(solid, Beneath(control)) : null;
            IEnumerable<PixelRect> children = control.GetVisualChildren().OfType<Control>()
                .Where(child => child.IsEffectivelyVisible && child.Bounds.Width > 0 && child.Bounds.Height > 0)
                .Select(child => Pixels(Bounds(child, window)));
            Color? own = frame.CommonestOwnColour(shown, children);
            parts.Add(new PaintedPart(themeCase, subject, Numbered(seen, Key(control, window)), laid, own, shown));
        }

        return parts;
    }

    // R7's input: every visible, enabled text element (a TextBlock, or a text
    // box's text presenter) drawn in full: its strongest pixel against its
    // surface, the commonest colour of the 2 px frame around it; its full-ink
    // pixels, those exactly its ink's colour; and its token ratio, the ink
    // token's against the surface token's, where both are tokens and the
    // surface drawn is the surface token's colour. Disabled text is R8's, read
    // the same way with disabled set: the text of every disabled control,
    // against the surface around it, which is the control's own fill.
    private static (IReadOnlyList<DrawnText> Texts, int Hidden) Texts(Window window, FramePixels frame, string subject, string themeCase, bool disabled)
    {
        var texts = new List<DrawnText>();
        var seen = new Dictionary<string, int>(StringComparer.Ordinal);
        int hidden = 0;
        foreach (Control control in window.GetVisualDescendants().OfType<Control>())
        {
            (string? text, IBrush? ink) = control switch
            {
                TextBlock block => (TextOf(block), block.Foreground),
                TextPresenter presenter => (presenter.Text, presenter.Foreground),
                _ => (null, null),
            };
            if (string.IsNullOrWhiteSpace(text) || !control.IsEffectivelyVisible || control.IsEffectivelyEnabled == disabled
                || control.Bounds.Width <= 0 || control.Bounds.Height <= 0)
            {
                continue;
            }

            PixelRect bounds = Pixels(Bounds(control, window));
            PixelRect clip = Clip(control, window, frame);
            if (bounds.Intersect(clip) != bounds)
            {
                hidden++;
                continue;
            }

            PixelRect around = new PixelRect(bounds.X - SurfaceFrame, bounds.Y - SurfaceFrame, bounds.Width + (2 * SurfaceFrame), bounds.Height + (2 * SurfaceFrame)).Intersect(clip);
            if (frame.CommonestOwnColour(around, [bounds]) is not { } surface)
            {
                hidden++;
                continue;
            }

            Color? inkColour = ink is ISolidColorBrush solidInk && solidInk.Color.A == 255 && solidInk.Opacity >= 1 ? solidInk.Color : null;
            string inkToken = inkColour is null ? "-" : TokenOf(control, ink);
            (ISolidColorBrush? surfaceBrush, Control? painter) = SurfaceBrush(control);
            string surfaceToken = surfaceBrush is not null && painter is not null && surfaceBrush.Color == surface ? TokenOf(painter, surfaceBrush) : "-";
            double? tokenRatio = inkColour is { } inkValue && inkToken != "-" && surfaceToken != "-"
                ? Contrast.ContrastRatio(inkValue, surfaceBrush!.Color)
                : null;
            string flat = Flat(text);
            texts.Add(new DrawnText(
                themeCase,
                subject,
                Numbered(seen, Key(control, window)),
                flat,
                inkColour,
                inkToken,
                surface,
                surfaceToken,
                tokenRatio,
                frame.StrongestContrast(bounds, surface),
                inkColour is { } full ? CountOf(frame, bounds, full) : 0,
                bounds));
        }

        return (texts, hidden);
    }

    // R9's edges: every visible element that draws a border (a Border, or a
    // content presenter, which draws a templated control's border) of the
    // same width on all four sides, in a brush that paints, at least 2 px
    // each way and drawn in full: how far its edge stands out from the
    // surface around it (FramePixels.EdgeContrast, at the middle of each
    // side, the weakest side), the surface being the commonest colour of the
    // 2 px frame around its bounds, as R7's is. A border on fewer than four
    // sides is a dividing line, not a control's outline, and is not read.
    private static (IReadOnlyList<DrawnEdge> Edges, int Hidden) Edges(Window window, FramePixels frame, string subject, string themeCase)
    {
        var edges = new List<DrawnEdge>();
        var seen = new Dictionary<string, int>(StringComparer.Ordinal);
        int hidden = 0;
        foreach (Control control in window.GetVisualDescendants().OfType<Control>())
        {
            (Thickness thickness, IBrush? brush) = control switch
            {
                Border border => (border.BorderThickness, border.BorderBrush),
                ContentPresenter presenter => (presenter.BorderThickness, presenter.BorderBrush),
                _ => (default, null),
            };
            if (brush is null || !Paints(brush) || !control.IsEffectivelyVisible || !thickness.IsUniform || thickness.Left <= 0)
            {
                continue;
            }

            PixelRect bounds = Pixels(Bounds(control, window));
            if (bounds.Width < 2 || bounds.Height < 2)
            {
                continue;
            }

            // The border must be drawn in full, inside every clipping
            // ancestor. The frame around it is read wherever the window
            // shows it: a templated control that clips to its own bounds
            // (a button around its content presenter) hides none of what
            // surrounds its edge.
            PixelRect clip = Clip(control, window, frame);
            PixelRect around = new PixelRect(bounds.X - SurfaceFrame, bounds.Y - SurfaceFrame, bounds.Width + (2 * SurfaceFrame), bounds.Height + (2 * SurfaceFrame))
                .Intersect(new PixelRect(0, 0, frame.Width, frame.Height));
            if (bounds.Intersect(clip) != bounds || frame.CommonestOwnColour(around, [bounds]) is not { } surface)
            {
                hidden++;
                continue;
            }

            edges.Add(new DrawnEdge(themeCase, subject, Numbered(seen, Key(control, window)), surface, frame.EdgeContrast(bounds, surface), bounds));
        }

        return (edges, hidden);
    }

    // R9's focus: every focusable control a person can reach with Tab
    // (focusable, a tab stop, visible and enabled), each given the keyboard
    // focus in turn from a window where nothing has it. For each, the pixels
    // of its bounds plus FocusReach that differ between the frame before and
    // the frame after, less its text areas (TextAreas: a text box's caret and
    // selection), and the strongest of them against the contrast set's
    // window colour. A control is not judged, with the reason, when it is
    // not drawn in full, when it shows focus without having it (the gallery's
    // "focused" inputs, whose focus pseudo-classes are set by hand, so they
    // have no look without focus to compare), when it does not take the
    // focus, or when taking it moves or removes it (a scroller bringing it
    // into view), since then its pixels are not comparable.
    private static List<FocusChange> WalkFocus(Window window, string subject, ThemeCase themeCase)
    {
        Color windowColour = themeCase.Contrast?.Window
            ?? throw new InvalidOperationException($"{themeCase}: R9 walks the focus under a contrast set only, against its window colour.");
        IFocusManager focus = window.FocusManager ?? throw new AssertFailedException("The window has no focus manager.");
        focus.Focus(null!);
        Dispatcher.UIThread.RunJobs();

        var seen = new Dictionary<string, int>(StringComparer.Ordinal);
        List<(Control Control, string Key)> walked = window.GetVisualDescendants().OfType<Control>()
            .Where(control => control.Focusable && KeyboardNavigation.GetIsTabStop(control) && control.IsEffectivelyVisible && control.IsEffectivelyEnabled
                && control.Bounds.Width > 0 && control.Bounds.Height > 0)
            .Select(control => (control, Numbered(seen, Key(control, window))))
            .ToList();

        var changes = new List<FocusChange>();
        foreach ((Control control, string key) in walked)
        {
            FocusChange Skipped(PixelRect area, string reason) =>
                new(themeCase.Name, subject, key, windowColour, 0, 0, area, reason);

            if (TopLevel.GetTopLevel(control) != window)
            {
                changes.Add(Skipped(default, "it left the window before its turn"));
                continue;
            }

            FramePixels before = FramePixels.Capture(window);
            Rect placed = Bounds(control, window);
            PixelRect bounds = Pixels(placed);
            PixelRect area = new PixelRect(bounds.X - FocusReach, bounds.Y - FocusReach, bounds.Width + (2 * FocusReach), bounds.Height + (2 * FocusReach))
                .Intersect(new PixelRect(0, 0, before.Width, before.Height));
            if (bounds.Intersect(Clip(control, window, before)) != bounds)
            {
                changes.Add(Skipped(area, "not drawn in full"));
                continue;
            }

            if (control.Classes.Contains(":focus") || control.Classes.Contains(":focus-within") || control.Classes.Contains(":focus-visible"))
            {
                changes.Add(Skipped(area, "it shows focus without having it"));
                continue;
            }

            if (!control.Focus(NavigationMethod.Tab))
            {
                changes.Add(Skipped(area, "it does not take the focus"));
                continue;
            }

            FramePixels after = FramePixels.Capture(window);
            if (TopLevel.GetTopLevel(control) != window || Bounds(control, window) != placed)
            {
                changes.Add(Skipped(area, "taking the focus moved it"));
            }
            else
            {
                PixelRect[] textAreas = TextAreas(control, window);
                int changed = 0;
                double strongest = 0;
                for (int y = area.Y; y < area.Bottom; y++)
                {
                    for (int x = area.X; x < area.Right; x++)
                    {
                        Color now = after.At(x, y);
                        if (now != before.At(x, y) && !textAreas.Any(text => text.Contains(new PixelPoint(x, y))))
                        {
                            changed++;
                            strongest = Math.Max(strongest, Contrast.ContrastRatio(now, windowColour));
                        }
                    }
                }

                changes.Add(new FocusChange(themeCase.Name, subject, key, windowColour, changed, strongest, area, null));
            }

            focus.Focus(null!);
            Dispatcher.UIThread.RunJobs();
        }

        return changes;
    }

    // Where a control draws a text box's caret and selection: the bounds of
    // every text presenter in it. Taking the focus by Tab shows the caret, or
    // selects all the text, both at full contrast, so a text box whose focus
    // frame were gone would still change pixels there. R9 leaves these areas
    // out, and judges a text box by its frame alone.
    private static PixelRect[] TextAreas(Control control, Window window) =>
        [.. new Visual[] { control }.Concat(control.GetVisualDescendants()).OfType<TextPresenter>()
            .Where(presenter => presenter.IsEffectivelyVisible && presenter.Bounds.Width > 0 && presenter.Bounds.Height > 0)
            .Select(presenter => Pixels(Bounds(presenter, window)))];

    // The background brush a control paints with, if it has one.
    private static IBrush? BackgroundOf(Control control) => control switch
    {
        Border border => border.Background,
        Panel panel => panel.Background,
        ContentPresenter presenter => presenter.Background,
        TemplatedControl templated => templated.Background,
        TextBlock block => block.Background,
        _ => null,
    };

    // Whether a brush paints anything: not a transparent solid colour.
    private static bool Paints(IBrush brush) =>
        brush.Opacity > 0 && (brush is not ISolidColorBrush solid || solid.Color.A > 0);

    // The colour beneath a control: the nearest ancestor that paints with a
    // solid colour, laid over what lies beneath that; black beneath the window.
    private static Color Beneath(Control control)
    {
        for (Visual? visual = control.GetVisualParent(); visual is not null; visual = visual.GetVisualParent())
        {
            if (visual is Control ancestor && BackgroundOf(ancestor) is ISolidColorBrush solid && Paints(solid))
            {
                return Over(solid, Beneath(ancestor));
            }
        }

        return Colors.Black;
    }

    // A solid brush laid over a colour, by its alpha and its opacity.
    private static Color Over(ISolidColorBrush brush, Color under)
    {
        double alpha = brush.Color.A / 255.0 * brush.Opacity;
        byte Mix(byte top, byte bottom) => (byte)Math.Round((alpha * top) + ((1 - alpha) * bottom));
        return Color.FromRgb(Mix(brush.Color.R, under.R), Mix(brush.Color.G, under.G), Mix(brush.Color.B, under.B));
    }

    // The brush of the surface a text sits on: the nearest ancestor that
    // paints, when that one paints one opaque colour, and that ancestor.
    private static (ISolidColorBrush? Brush, Control? Painter) SurfaceBrush(Control control)
    {
        for (Visual? visual = control.GetVisualParent(); visual is not null; visual = visual.GetVisualParent())
        {
            if (visual is Control ancestor && BackgroundOf(ancestor) is { } brush && Paints(brush))
            {
                return brush is ISolidColorBrush solid && solid.Color.A == 255 && solid.Opacity >= 1 ? (solid, ancestor) : (null, null);
            }
        }

        return (null, null);
    }

    // The token key whose brush, in the control's theme, is this very brush
    // object, or "-" when none is.
    private static string TokenOf(Control control, IBrush? brush)
    {
        foreach (string key in s_tokenKeys)
        {
            if (control.TryFindResource(key, control.ActualThemeVariant, out object? found) && ReferenceEquals(found, brush))
            {
                return key;
            }
        }

        return "-";
    }

    // A text block's text, from its runs when it has no plain text.
    private static string? TextOf(TextBlock block) =>
        !string.IsNullOrEmpty(block.Text) ? block.Text
        : block.Inlines is { Count: > 0 } inlines ? string.Concat(inlines.OfType<Run>().Select(run => run.Text))
        : null;

    // A text on one line, as a measurement cell holds it.
    private static string Flat(string text) =>
        string.Join(' ', text.Split(['\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries)).Trim();

    // How many pixels of the rectangle are exactly the colour.
    private static int CountOf(FramePixels frame, PixelRect rect, Color colour)
    {
        int count = 0;
        for (int y = rect.Y; y < rect.Bottom; y++)
        {
            for (int x = rect.X; x < rect.Right; x++)
            {
                if (frame.At(x, y) == colour)
                {
                    count++;
                }
            }
        }

        return count;
    }

    // A control's name in a sweep: the views (user controls) it sits in and
    // its named ancestors that are not parts of a template (a template's
    // PART_ names repeat in every control), then itself, each as Type or
    // Type#Name, joined by "/". The same on every system, because it comes
    // from the tree, not from where things are drawn.
    private static string Key(Control control, Window window)
    {
        var steps = new List<string>();
        for (Visual? visual = control.GetVisualParent(); visual is not null && visual != window; visual = visual.GetVisualParent())
        {
            if (visual is Control ancestor && ((!string.IsNullOrEmpty(ancestor.Name) && ancestor.TemplatedParent is null) || ancestor is UserControl))
            {
                steps.Add(Label(ancestor));
            }
        }

        steps.Reverse();
        steps.Add(Label(control));
        return string.Join('/', steps);
    }

    private static string Label(Control control) =>
        string.IsNullOrEmpty(control.Name) ? control.GetType().Name : $"{control.GetType().Name}#{control.Name}";

    // The key, with " [n]" from its second use on, in the tree's order, so
    // every line names one element.
    private static string Numbered(Dictionary<string, int> seen, string key)
    {
        int count = seen.TryGetValue(key, out int before) ? before + 1 : 1;
        seen[key] = count;
        return count == 1 ? key : $"{key} [{count}]";
    }

    // The part of a control the window shows: its bounds inside the frame
    // and inside every ancestor that clips (a scroller's viewport), or none.
    private static PixelRect? Shown(Control control, Window window, FramePixels frame)
    {
        PixelRect shown = Pixels(Bounds(control, window)).Intersect(Clip(control, window, frame));
        return shown.Width > 0 && shown.Height > 0 ? shown : null;
    }

    // Where a control's pixels can show: the frame, less what each clipping
    // ancestor cuts away.
    private static PixelRect Clip(Control control, Window window, FramePixels frame)
    {
        var clip = new PixelRect(0, 0, frame.Width, frame.Height);
        for (Visual? visual = control.GetVisualParent(); visual is not null; visual = visual.GetVisualParent())
        {
            if (visual.ClipToBounds && visual is Control ancestor)
            {
                clip = clip.Intersect(Pixels(Bounds(ancestor, window)));
            }
        }

        return clip;
    }

    // A control's bounds in the window (headless draws at scale 1, so in pixels).
    private static Rect Bounds(Control control, Window window)
    {
        Point origin = control.TranslatePoint(default, window) ?? throw new AssertFailedException($"{control} is not in the window.");
        return new Rect(origin, control.Bounds.Size);
    }

    private static PixelRect Pixels(Rect rect)
    {
        int left = (int)Math.Floor(rect.X);
        int top = (int)Math.Floor(rect.Y);
        return new PixelRect(left, top, (int)Math.Ceiling(rect.Right) - left, (int)Math.Ceiling(rect.Bottom) - top);
    }

    // The example library, written into a folder of its own under the storage guard's root.
    private static DiskLibrarySession OpenExample()
    {
        string root = Path.Combine(AssemblySetup.StorageRoot, "sweep-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        LargeLibraryFixture.WriteExample(root);
        var session = new DiskLibrarySession(
            Path.Combine(root, "data"),
            Path.Combine(root, "library"),
            root,
            LibraryKind.Example,
            LargeLibraryFixture.Today);
        session.LoadAsync(CancellationToken.None).GetAwaiter().GetResult();
        Assert.HasCount(7, session.Manuscripts);
        return session;
    }
}
