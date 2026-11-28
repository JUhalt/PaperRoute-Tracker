using System.Globalization;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;

namespace PaperRoute.Desktop.Tests.Legibility;

// The pixels of one rendered frame, read as colours, and the pixel
// primitives the picture rules share (the harness design, part 2.1). Moved
// out of PictureRuleTests.cs unchanged, with the primitives of today's
// WinForms rules beside it; PrimitiveTests checks each one on a bitmap made
// in code, whose right answer is known by construction.
// Limits of headless: HeadlessLimits.md
internal sealed class FramePixels
{
    private readonly byte[] _bytes;
    private readonly int _stride;
    private readonly bool _bgra;

    private FramePixels(byte[] bytes, int stride, int width, int height, bool bgra)
    {
        _bytes = bytes;
        _stride = stride;
        Width = width;
        Height = height;
        _bgra = bgra;
    }

    public int Width { get; }

    public int Height { get; }

    public static FramePixels Capture(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        using WriteableBitmap? frame = window.CaptureRenderedFrame();
        Assert.IsNotNull(frame, "The headless platform captured no frame.");
        using ILockedFramebuffer buffer = frame.Lock();
        bool bgra = buffer.Format == PixelFormat.Bgra8888;
        Assert.IsTrue(bgra || buffer.Format == PixelFormat.Rgba8888, $"A frame in {buffer.Format}, which the test does not read.");
        var bytes = new byte[buffer.RowBytes * buffer.Size.Height];
        Marshal.Copy(buffer.Address, bytes, 0, bytes.Length);
        return new FramePixels(bytes, buffer.RowBytes, buffer.Size.Width, buffer.Size.Height, bgra);
    }

    // A bitmap made in code: opaque pixels, four bytes each in the order
    // blue, green, red, alpha, row after row with no padding.
    public static FramePixels FromBgra(byte[] bytes, int width, int height)
    {
        Assert.HasCount(width * height * 4, bytes, $"A {width} x {height} bitmap holds {width * height * 4} bytes.");
        return new FramePixels(bytes, width * 4, width, height, bgra: true);
    }

    public bool Contains(PixelRect rect) =>
        rect.X >= 0 && rect.Y >= 0 && rect.Right <= Width && rect.Bottom <= Height && rect.Width > 0 && rect.Height > 0;

    // The frame is opaque, so premultiplied colour is the colour.
    public Color At(int x, int y)
    {
        int offset = (y * _stride) + (x * 4);
        return _bgra
            ? Color.FromRgb(_bytes[offset + 2], _bytes[offset + 1], _bytes[offset])
            : Color.FromRgb(_bytes[offset], _bytes[offset + 1], _bytes[offset + 2]);
    }

    // "<name>: luminance <max> (#rrggbb) in <patch>", the brightest pixel
    // of the patch, and " LIGHT" when it is above the light line.
    public string Describe(string name, PixelRect patch)
    {
        Assert.IsTrue(Contains(patch), $"The patch {patch} of {name} lies outside the frame.");
        double brightest = -1;
        Color colour = default;
        for (int y = patch.Y; y < patch.Bottom; y++)
        {
            for (int x = patch.X; x < patch.Right; x++)
            {
                Color pixel = At(x, y);
                double luminance = Contrast.RelativeLuminance(pixel);
                if (luminance > brightest)
                {
                    brightest = luminance;
                    colour = pixel;
                }
            }
        }

        return string.Create(CultureInfo.InvariantCulture,
            $"{name}: luminance {brightest:0.000} (#{colour.R:X2}{colour.G:X2}{colour.B:X2}) in {patch}{(brightest > Thresholds.LightPart ? " LIGHT" : string.Empty)}");
    }

    // The highest contrast any pixel of the rectangle has against background.
    public double StrongestContrast(PixelRect rect, Color background)
    {
        double strongest = 1;
        for (int y = rect.Y; y < rect.Bottom; y++)
        {
            for (int x = rect.X; x < rect.Right; x++)
            {
                strongest = Math.Max(strongest, Contrast.ContrastRatio(At(x, y), background));
            }
        }

        return strongest;
    }

    // The commonest colour of what a control paints itself: the pixels of
    // its bounds that none of its children's bounds cover, or none when they
    // cover all of it. A tie goes to the colour met first, row by row, as
    // DarkThemeLegibilityTests.vb CommonestOwnColor's does.
    public Color? CommonestOwnColour(PixelRect bounds, IEnumerable<PixelRect> children)
    {
        RequireInside(bounds, "The bounds");
        var covered = new bool[bounds.Width * bounds.Height];
        foreach (PixelRect child in children)
        {
            PixelRect inside = child.Intersect(bounds);
            for (int y = inside.Y; y < inside.Bottom; y++)
            {
                for (int x = inside.X; x < inside.Right; x++)
                {
                    covered[((y - bounds.Y) * bounds.Width) + (x - bounds.X)] = true;
                }
            }
        }

        var counts = new Dictionary<Color, int>();
        var order = new List<Color>();
        for (int y = bounds.Y; y < bounds.Bottom; y++)
        {
            for (int x = bounds.X; x < bounds.Right; x++)
            {
                if (covered[((y - bounds.Y) * bounds.Width) + (x - bounds.X)])
                {
                    continue;
                }

                Color pixel = At(x, y);
                if (counts.TryGetValue(pixel, out int count))
                {
                    counts[pixel] = count + 1;
                }
                else
                {
                    counts[pixel] = 1;
                    order.Add(pixel);
                }
            }
        }

        if (order.Count == 0)
        {
            return null;
        }

        int most = counts.Values.Max();
        return order.First(colour => counts[colour] == most);
    }

    // Today's R3 pattern rule (WorkflowThemeKeyboardSmokeTests.vb SamePattern):
    // a position is in a swatch's pattern when its pixel is within 2:1 of the
    // swatch's commonest colour; the share of positions that are in one
    // swatch's pattern and not in the other's, from 0 to 1.
    public static double PatternDifference(FramePixels one, PixelRect oneArea, FramePixels other, PixelRect otherArea)
    {
        Assert.AreEqual(oneArea.Size, otherArea.Size, "Two swatches compared position by position have one size.");
        // An area with no children always has a commonest colour.
        Color oneColour = one.CommonestOwnColour(oneArea, []) ?? throw new InvalidOperationException("No colour in the first swatch.");
        Color otherColour = other.CommonestOwnColour(otherArea, []) ?? throw new InvalidOperationException("No colour in the second swatch.");
        int differing = 0;
        for (int y = 0; y < oneArea.Height; y++)
        {
            for (int x = 0; x < oneArea.Width; x++)
            {
                bool inOne = Contrast.ContrastRatio(one.At(oneArea.X + x, oneArea.Y + y), oneColour) < Thresholds.R3InPattern;
                bool inOther = Contrast.ContrastRatio(other.At(otherArea.X + x, otherArea.Y + y), otherColour) < Thresholds.R3InPattern;
                if (inOne != inOther)
                {
                    differing++;
                }
            }
        }

        return differing / (double)(oneArea.Width * oneArea.Height);
    }

    // Two swatches share a pattern, whatever their colours, when nearly the
    // same positions are in each one's pattern (under 10 % differ), or nearly
    // the same positions are not (over 90 % differ).
    public static bool SamePattern(FramePixels one, PixelRect oneArea, FramePixels other, PixelRect otherArea) =>
        SharesPattern(PatternDifference(one, oneArea, other, otherArea));

    // The same judgement from a share PatternDifference measured.
    public static bool SharesPattern(double differing) =>
        differing < Thresholds.R3SamePatternUnder || differing > Thresholds.R3SamePatternOver;

    // How far a drawn shape's edge stands out from the surface
    // (DeadlineListTests.vb EdgeContrast): at the middle of each side, the
    // stronger of its two outermost pixels; the weakest of the four sides.
    public double EdgeContrast(PixelRect bounds, Color surface)
    {
        RequireInside(bounds, "The shape");
        Assert.IsTrue(bounds.Width >= 2 && bounds.Height >= 2, $"The shape {bounds} has two pixels on each side to read.");
        int middleX = bounds.X + (bounds.Width / 2);
        int middleY = bounds.Y + (bounds.Height / 2);
        (int X, int Y)[][] sides =
        [
            [(middleX, bounds.Y), (middleX, bounds.Y + 1)],
            [(middleX, bounds.Bottom - 1), (middleX, bounds.Bottom - 2)],
            [(bounds.X, middleY), (bounds.X + 1, middleY)],
            [(bounds.Right - 1, middleY), (bounds.Right - 2, middleY)],
        ];
        return sides.Min(side => side.Max(spot => Contrast.ContrastRatio(At(spot.X, spot.Y), surface)));
    }

    private void RequireInside(PixelRect rect, string what) =>
        Assert.IsTrue(Contains(rect), $"{what} {rect} lies outside the {Width} x {Height} frame.");
}
