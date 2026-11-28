using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using ManuscriptPipeline.Services;

namespace PaperRoute.Desktop.Tests.Legibility;

// R3, route kinds told apart without colour (harness design 2.4): every kind
// of RouteKinds drawn alone in its fixed slot at render scale 4, in all six
// theme cases, and judged by Rules.R3Findings. Each case's picture is saved
// for the maintainer's eye (rules-r3-<case>.png) and its numbers beside it
// (legibility-r3-<case>.txt). A failure listed in KnownFailures.tsv does not
// fail the run, as with R1 (KnownFailuresAreCurrent re-runs it through
// CurrentFindings).
// Limits of headless: HeadlessLimits.md
[TestClass]
public sealed class R3Tests
{
    // The columns of legibility-r3-<case>.txt.
    private static readonly string[] s_patternColumns = ["theme case", "pair", "positions differing (%)", "commonest colours ratio"];

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void R3_RouteKindsDifferWithoutColourInEveryTheme()
    {
        CollectionAssert.AreEqual(
            Enum.GetNames<RouteDotState>().Select(name => name.ToLowerInvariant()).ToArray(),
            RouteKinds.All.Select(kind => kind.Name).ToArray(),
            "The registry holds every route-dot state the card draws, in order.");

        IReadOnlyList<KnownFailure> known = KnownFailures.Load();
        var findings = new List<Finding>();
        foreach (ThemeCase themeCase in ThemeCase.All)
        {
            List<(string Name, Color[] Pixels)> swatches = Draw(themeCase, RouteKinds.All, "rules-r3-" + themeCase.Name.ToLowerInvariant(), TestContext);
            IReadOnlyList<PatternMeasure> measures = Rules.R3Measures(themeCase.Name, swatches);
            findings.AddRange(Rules.R3Findings(measures, themeCase.Contrast is not null).Where(finding => !KnownFailures.Lists(known, finding)));
            string path = Measurements.Write(
                Pictures.Directory(),
                "R3",
                themeCase.Name,
                s_patternColumns,
                measures.Select(measured => (IReadOnlyList<string>)
                [
                    measured.ThemeCase,
                    $"{measured.One} and {measured.Other}",
                    (measured.Differing * 100).ToString("F1", CultureInfo.InvariantCulture),
                    Measurements.Number(measured.Ratio),
                ]));
            TestContext.WriteLine($"Measured values saved: {path}");
            TestContext.WriteLine($"{themeCase}: " + string.Join("; ", measures.Select(measured => measured.Line)));
        }

        Assert.IsEmpty(findings, "R3, route kinds alike without colour:" + Environment.NewLine + string.Join(Environment.NewLine, findings.Select(finding => $"{finding.ThemeCase}: {finding}")));
    }

    // The rule's teeth: "closed" drawn at 10 px, like "accepted", differs from
    // it only in colour (1.09:1 in Light), so the two share a pattern and fail.
    [TestMethod]
    public void R3_FailsTwoStatesThatDifferOnlyInColour()
    {
        RouteKind large = new("closed at 10 px", () =>
        {
            Avalonia.Controls.Shapes.Ellipse dot = RouteKinds.Dot("closed");
            dot.Width = RouteKinds.Slot;
            dot.Height = RouteKinds.Slot;
            dot.Margin = default;
            return dot;
        });
        RouteKind accepted = RouteKinds.All.Single(kind => kind.Name == "accepted");

        List<(string Name, Color[] Pixels)> swatches = Draw(ThemeCase.Light, [large, accepted], "rules-r3-teeth-light", TestContext);
        IReadOnlyList<PatternMeasure> measures = Rules.R3Measures(ThemeCase.Light.Name, swatches);
        List<Finding> findings = Rules.R3Findings(measures, contrast: false);
        string measured = string.Join(Environment.NewLine, measures.Select(measure => measure.Line));
        TestContext.WriteLine(measured);

        Assert.HasCount(1, findings, "One pair, alike: " + string.Join("; ", measures.Select(measure => measure.Line)));
        Assert.AreEqual(new Finding("R3", "Light", "closed at 10 px and accepted", findings[0].Line), findings[0], "The finding names R3, its theme case and the pair.");
        StringAssert.StartsWith(findings[0].Line, "closed at 10 px and accepted: ");
        StringAssert.EndsWith(findings[0].Line, "; commonest colours 1.09:1: the same pattern and under 3:1");
    }

    // What the R3 run reports today, listed failures included: every theme
    // case drawn again, without saving its picture.
    internal static IReadOnlyList<Finding> CurrentFindings() =>
        ThemeCase.All
            .SelectMany(themeCase => Rules.R3Findings(themeCase.Name, Draw(themeCase, RouteKinds.All, picture: null, context: null), themeCase.Contrast is not null))
            .ToList();

    // Draws each kind alone in its slot, on CardBackgroundBrush, at render
    // scale 4, saves the picture (unless none is named, when the frame is
    // settled as Pictures.Save settles it) and returns each slot's pixels,
    // read through FramePixels.
    private static List<(string Name, Color[] Pixels)> Draw(ThemeCase themeCase, IReadOnlyList<RouteKind> kinds, string? picture, TestContext? context) => Headless.Run(() =>
    {
        themeCase.Apply(Application.Current!);
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = RouteKinds.Slot,
            Margin = new Thickness(RouteKinds.Slot),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
        };
        var slots = new List<(string Name, Border Slot)>();
        foreach (RouteKind kind in kinds)
        {
            var slot = new Border { Width = RouteKinds.Slot, Height = RouteKinds.Slot, Child = kind.Create() };
            slot.Bind(Border.BackgroundProperty, slot.GetResourceObservable("CardBackgroundBrush"));
            row.Children.Add(slot);
            slots.Add((kind.Name, slot));
        }

        var window = new Window { Width = 100, Height = 40, Content = row };
        window.Bind(Window.BackgroundProperty, window.GetResourceObservable("CardBackgroundBrush"));
        window.Show();
        try
        {
            window.SetRenderScaling(RouteKinds.Scale);
            if (picture is not null)
            {
                Pictures.Save(window, picture, context!);
            }
            else
            {
                Dispatcher.UIThread.RunJobs();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick(30);
            }

            FramePixels frame = FramePixels.Capture(window);
            int side = (int)Math.Round(RouteKinds.Slot * RouteKinds.Scale);
            var swatches = new List<(string Name, Color[] Pixels)>();
            foreach ((string name, Border slot) in slots)
            {
                Point origin = slot.TranslatePoint(default, window) ?? throw new AssertFailedException($"The slot of {name} is not in the window.");
                var area = new PixelRect((int)Math.Round(origin.X * RouteKinds.Scale), (int)Math.Round(origin.Y * RouteKinds.Scale), side, side);
                Assert.IsTrue(frame.Contains(area), $"The slot of {name} lies outside the frame {frame.Width} x {frame.Height}.");
                var pixels = new Color[side * side];
                for (int y = 0; y < side; y++)
                {
                    for (int x = 0; x < side; x++)
                    {
                        pixels[(y * side) + x] = frame.At(area.X + x, area.Y + y);
                    }
                }

                swatches.Add((name, pixels));
            }

            return swatches;
        }
        finally
        {
            window.Close();
        }
    });
}
