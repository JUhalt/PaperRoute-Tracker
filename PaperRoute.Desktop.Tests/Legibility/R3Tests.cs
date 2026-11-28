using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using ManuscriptPipeline.Services;

namespace PaperRoute.Desktop.Tests.Legibility;

// R3, route kinds told apart without colour (harness design 2.4): every kind
// of RouteKinds drawn alone in its fixed slot at render scale 4, in all six
// theme cases, and judged by Rules.R3Findings. Each case's picture is saved
// for the maintainer's eye (rules-r3-<case>.png) and its numbers beside it
// (legibility-r3-<case>.txt).
// Limits of headless: HeadlessLimits.md
[TestClass]
public sealed class R3Tests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void R3_RouteKindsDifferWithoutColourInEveryTheme()
    {
        CollectionAssert.AreEqual(
            Enum.GetNames<RouteDotState>().Select(name => name.ToLowerInvariant()).ToArray(),
            RouteKinds.All.Select(kind => kind.Name).ToArray(),
            "The registry holds every route-dot state the card draws, in order.");

        var findings = new List<string>();
        foreach (ThemeCase themeCase in ThemeCase.All)
        {
            List<(string Name, Color[] Pixels)> swatches = Draw(themeCase, RouteKinds.All, "rules-r3-" + themeCase.Name.ToLowerInvariant());
            var measured = new List<string>();
            findings.AddRange(Rules.R3Findings(swatches, themeCase.Contrast is not null, measured).Select(finding => $"{themeCase}: {finding}"));
            MeasuredValues.Write("r3", themeCase.Name, measured, TestContext);
            TestContext.WriteLine($"{themeCase}: " + string.Join("; ", measured));
        }

        Assert.IsEmpty(findings, "R3, route kinds alike without colour:" + Environment.NewLine + string.Join(Environment.NewLine, findings));
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

        List<(string Name, Color[] Pixels)> swatches = Draw(ThemeCase.Light, [large, accepted], "rules-r3-teeth-light");
        var measured = new List<string>();
        List<string> findings = Rules.R3Findings(swatches, contrast: false, measured);
        TestContext.WriteLine(string.Join(Environment.NewLine, measured));

        Assert.HasCount(1, findings, "One pair, alike: " + string.Join("; ", measured));
        StringAssert.StartsWith(findings[0], "closed at 10 px and accepted: ");
        StringAssert.EndsWith(findings[0], "; commonest colours 1.09:1: the same pattern and under 3:1");
    }

    // Draws each kind alone in its slot, on CardBackgroundBrush, at render
    // scale 4, saves the picture and returns each slot's pixels.
    private List<(string Name, Color[] Pixels)> Draw(ThemeCase themeCase, IReadOnlyList<RouteKind> kinds, string picture) => Headless.Run(() =>
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
            Pictures.Save(window, picture, TestContext);
            Dispatcher.UIThread.RunJobs();
            using WriteableBitmap? frame = window.CaptureRenderedFrame();
            Assert.IsNotNull(frame, "The headless platform captured no frame.");
            using ILockedFramebuffer buffer = frame.Lock();
            bool bgra = buffer.Format == PixelFormat.Bgra8888;
            Assert.IsTrue(bgra || buffer.Format == PixelFormat.Rgba8888, $"A frame in {buffer.Format}, which the test does not read.");
            var bytes = new byte[buffer.RowBytes * buffer.Size.Height];
            Marshal.Copy(buffer.Address, bytes, 0, bytes.Length);

            int side = (int)Math.Round(RouteKinds.Slot * RouteKinds.Scale);
            var swatches = new List<(string Name, Color[] Pixels)>();
            foreach ((string name, Border slot) in slots)
            {
                Point origin = slot.TranslatePoint(default, window) ?? throw new AssertFailedException($"The slot of {name} is not in the window.");
                int left = (int)Math.Round(origin.X * RouteKinds.Scale);
                int top = (int)Math.Round(origin.Y * RouteKinds.Scale);
                Assert.IsTrue(left + side <= buffer.Size.Width && top + side <= buffer.Size.Height, $"The slot of {name} lies outside the frame {buffer.Size}.");
                var pixels = new Color[side * side];
                for (int y = 0; y < side; y++)
                {
                    for (int x = 0; x < side; x++)
                    {
                        // The frame is opaque, so premultiplied colour is the colour.
                        int offset = ((top + y) * buffer.RowBytes) + ((left + x) * 4);
                        pixels[(y * side) + x] = bgra
                            ? Color.FromRgb(bytes[offset + 2], bytes[offset + 1], bytes[offset])
                            : Color.FromRgb(bytes[offset], bytes[offset + 1], bytes[offset + 2]);
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
