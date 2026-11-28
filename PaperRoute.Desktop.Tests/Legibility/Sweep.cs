using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
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
// part of them.
internal sealed record SweepFrame(string Subject, string ThemeCase, IReadOnlyList<PaintedPart> Parts, IReadOnlyList<DrawnText> Texts, int HiddenTexts);

// The sweeps (the harness design, part 2.1): one rule run over every surface
// the harness can build, in its theme cases. Sweep shows a subject in a
// window, in a theme case, lets its transitions finish, saves its picture
// (sweep-<subject>-<case>.png), reads the frame and walks the window's visual
// tree, handing each rule its inputs. The rules (Rules.R5, Rules.R7) judge;
// the sweep only reads.
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

    // Shows the subject in the theme case and reads it, in one Headless.Run
    // (a fresh application, so no theme case outlives its test), over a
    // library of its own when it needs one.
    public static SweepFrame Read(SweepSubject subject, ThemeCase themeCase, TestContext context)
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
                (IReadOnlyList<DrawnText> texts, int hidden) = Texts(window, frame, subject.Name, themeCase.Name);
                return new SweepFrame(subject.Name, themeCase.Name, parts, texts, hidden);
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
    // surface drawn is the surface token's colour. Disabled text is R8's.
    private static (IReadOnlyList<DrawnText> Texts, int Hidden) Texts(Window window, FramePixels frame, string subject, string themeCase)
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
            if (string.IsNullOrWhiteSpace(text) || !control.IsEffectivelyVisible || !control.IsEffectivelyEnabled
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
