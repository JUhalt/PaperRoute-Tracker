using System.Globalization;
using Avalonia;
using Avalonia.Media;

namespace PaperRoute.Desktop.Tests.Legibility;

// The pixel primitives of FramePixels, each on a bitmap made in code whose
// right answer is known by construction (the harness design, part 5). No
// window and no drawing: the rules built on them (P-R5, P-R7 today; the
// sweeps later) then judge real frames with primitives shown to read
// pixels right.
// Limits of headless: HeadlessLimits.md
[TestClass]
public sealed class PrimitiveTests
{
    private static readonly Color s_white = Color.FromRgb(0xFF, 0xFF, 0xFF);
    private static readonly Color s_black = Color.FromRgb(0x00, 0x00, 0x00);

    // The side of a swatch the pattern rule compares, in pixels.
    private const int Side = 10;

    private static readonly PixelRect s_swatch = new(0, 0, Side, Side);

    // The strongest pixel against the background is the darkest one inside
    // the rectangle on white. The darkest sits on a corner, once the last
    // and once the first, so a reading that drops an edge row or column
    // fails; so does one that counts a pixel outside the rectangle (black
    // ones lie just outside both corners and three sides), or takes any
    // other pixel.
    [TestMethod]
    [DataRow(7, 5)]
    [DataRow(2, 2)]
    public void StrongestContrastFindsTheDarkestPixel(int darkestX, int darkestY)
    {
        Color darkest = Color.FromRgb(0x33, 0x33, 0x33);
        var area = new PixelRect(2, 2, 6, 4);
        FramePixels frame = new CodeBitmap(12, 8, s_white)
            .Set(3, 3, Color.FromRgb(0x99, 0x99, 0x99))
            .Set(darkestX, darkestY, darkest)
            .Set(4, 5, Color.FromRgb(0x66, 0x66, 0x66))
            .Set(1, 3, s_black)
            .Set(8, 3, s_black)
            .Set(4, 1, s_black)
            .Set(8, 5, s_black)
            .Set(7, 6, s_black)
            .Set(1, 2, s_black)
            .Set(2, 1, s_black)
            .Frame();

        Assert.AreEqual(Contrast.ContrastRatio(darkest, s_white), frame.StrongestContrast(area, s_white),
            "On white, the #333333 pixel is the strongest inside the rectangle; the black pixels lie outside it.");
        Assert.AreEqual(Contrast.ContrastRatio(s_white, s_black), frame.StrongestContrast(area, s_black),
            "On black, the white pixels are the strongest: contrast against the background, not darkness.");
        Assert.AreEqual(1.0, frame.StrongestContrast(new PixelRect(9, 4, 3, 4), s_white),
            "A rectangle of background only has no contrast.");
    }

    // A panel mostly covered by a white child paints the panel's colour
    // itself; fails if the child's pixels, a child's part outside the panel,
    // or the black pixels around the panel are counted.
    [TestMethod]
    public void CommonestOwnColourLeavesChildrenOut()
    {
        Color panelColour = Color.FromRgb(0x20, 0x28, 0x30);
        var panel = new PixelRect(2, 2, 10, 10);
        var child = new PixelRect(3, 3, 8, 8);
        var corner = new PixelRect(10, 10, 8, 8);
        FramePixels frame = new CodeBitmap(16, 16, s_black)
            .Fill(panel, panelColour)
            .Fill(child, s_white)
            .Frame();

        Assert.AreEqual(s_white, frame.CommonestOwnColour(panel, []),
            "With no child left out, the child's 64 white pixels outnumber the panel's 36.");
        Assert.AreEqual(panelColour, frame.CommonestOwnColour(panel, [child, corner]),
            "With the children left out, the panel's own 33 pixels remain.");
        Assert.IsNull(frame.CommonestOwnColour(panel, [panel]), "A panel its children cover entirely paints nothing itself.");

        Color red = Color.FromRgb(0xFF, 0x00, 0x00);
        Color blue = Color.FromRgb(0x00, 0x00, 0xFF);
        FramePixels tie = new CodeBitmap(2, 1, red).Set(1, 0, blue).Frame();
        Assert.AreEqual(red, tie.CommonestOwnColour(new PixelRect(0, 0, 2, 1), []),
            "A tie goes to the colour met first, as DarkThemeLegibilityTests.vb's CommonestOwnColor does.");
    }

    // Today's R3 rule, case by case on 10 x 10 swatches (100 positions):
    // under 10 % differing is the same pattern, exactly 10 % is not; a pixel
    // within 2:1 of the commonest colour is no pattern; the colours do not
    // matter; and over 90 % differing, the same pattern inverted, is the
    // same pattern, exactly 90 % is not. Fails if a boundary, the 2:1 line
    // or the inversion differs from WorkflowThemeKeyboardSmokeTests.vb's
    // SamePattern.
    [TestMethod]
    public void SamePatternMatchesTodaysRule()
    {
        // About 1.98:1 and 2.01:1 against white, one on each side of the line.
        Color faint = Color.FromRgb(0xB8, 0xB8, 0xB8);
        Color visible = Color.FromRgb(0xB7, 0xB7, 0xB7);
        Assert.IsLessThan(Thresholds.R3InPattern, Contrast.ContrastRatio(faint, s_white), "#B8B8B8 lies within 2:1 of white.");
        Assert.IsGreaterThanOrEqualTo(Thresholds.R3InPattern, Contrast.ContrastRatio(visible, s_white), "#B7B7B7 lies beyond 2:1 of white.");

        FramePixels plain = Swatch(s_white, s_black, 0);
        AssertPattern(0.09, true, plain, Swatch(s_white, s_black, 9), "nine black pixels of 100");
        AssertPattern(0.10, false, plain, Swatch(s_white, s_black, 10), "ten black pixels of 100");
        AssertPattern(0.00, true, plain, Swatch(s_white, faint, 40), "40 pixels within 2:1 of white");
        AssertPattern(0.40, false, plain, Swatch(s_white, visible, 40), "40 pixels beyond 2:1 of white");
        AssertPattern(0.00, true, Swatch(s_white, s_black, 30), Swatch(Color.FromRgb(0x1E, 0x2A, 0x3A), Color.FromRgb(0xE0, 0xE0, 0xE0), 30),
            "the same 30 positions in other colours");

        // Inverted: in the first, the commonest colour (white, 60) fills rows
        // 4 to 9; in the second, the commonest (white, 40) fills rows 0 to 3,
        // and rows 4 to 9 hold three dark colours of 20 pixels each.
        FramePixels inverted = new CodeBitmap(Side, Side, s_white)
            .Fill(new PixelRect(0, 4, Side, 2), s_black)
            .Fill(new PixelRect(0, 6, Side, 2), Color.FromRgb(0x00, 0x00, 0x40))
            .Fill(new PixelRect(0, 8, Side, 2), Color.FromRgb(0x40, 0x00, 0x00))
            .Frame();
        AssertPattern(1.00, true, Swatch(s_white, s_black, 40), inverted, "the same rows, in and out swapped");

        // Each side of the 90 % line: the first swatch paints some of its
        // white rows 4 to 9 a fourth dark colour, so they join rows 0 to 3
        // in its pattern and 100 less that many positions differ. White
        // stays its commonest colour (50 or 51, against black's 40).
        AssertPattern(0.90, false, InvertedBut(10), inverted, "the swapped rows with ten positions alike");
        AssertPattern(0.91, true, InvertedBut(9), inverted, "the swapped rows with nine positions alike");
    }

    // Black rows 0 to 3 and white rows 4 to 9, but the first alike positions
    // of rows 4 to 9 are dark, as the inverted swatch's rows 4 to 9 are.
    private static FramePixels InvertedBut(int alike)
    {
        CodeBitmap bitmap = new CodeBitmap(Side, Side, s_white).Fill(new PixelRect(0, 0, Side, 4), s_black);
        for (int index = 0; index < alike; index++)
        {
            bitmap.Set(index % Side, 4 + (index / Side), Color.FromRgb(0x40, 0x00, 0x40));
        }

        return bitmap.Frame();
    }

    // At the middle of each side the stronger of the two outermost pixels
    // counts, and the weakest side is the answer. Each side is the weakest
    // in turn: its outermost middle pixel is white, its second #767676, and
    // the pixel just outside it black; every other side reads white, then
    // black. Fails if a side reads outside the shape or off its middle
    // (black there gives 21:1), if only the outermost pixel is read (white
    // everywhere gives 1:1), or if the strongest side wins.
    [TestMethod]
    [DataRow("top")]
    [DataRow("bottom")]
    [DataRow("left")]
    [DataRow("right")]
    public void EdgeContrastTakesTheWeakestSide(string weakSide)
    {
        Color grey = Color.FromRgb(0x76, 0x76, 0x76);
        var shape = new PixelRect(4, 4, 10, 10);
        // At the weak side's middle: its second pixel, and the pixel just
        // outside the shape. White is the outermost ring; black fills the
        // rest of the shape.
        (PixelPoint second, PixelPoint outside) = weakSide switch
        {
            "top" => (new PixelPoint(9, 5), new PixelPoint(9, 3)),
            "bottom" => (new PixelPoint(9, 12), new PixelPoint(9, 14)),
            "left" => (new PixelPoint(5, 9), new PixelPoint(3, 9)),
            "right" => (new PixelPoint(12, 9), new PixelPoint(14, 9)),
            _ => throw new ArgumentOutOfRangeException(nameof(weakSide), weakSide, "No such side."),
        };
        FramePixels frame = new CodeBitmap(20, 20, s_white)
            .Fill(new PixelRect(5, 5, 8, 8), s_black)
            .Set(second.X, second.Y, grey)
            .Set(outside.X, outside.Y, s_black)
            .Frame();

        Assert.AreEqual(Contrast.ContrastRatio(grey, s_white), frame.EdgeContrast(shape, s_white),
            $"The weakest side is the {weakSide}, whose second middle pixel is #767676.");

        FramePixels open = new CodeBitmap(20, 20, s_white)
            .Fill(new PixelRect(4, 4, 10, 1), s_black)
            .Fill(new PixelRect(4, 13, 10, 1), s_black)
            .Fill(new PixelRect(13, 4, 1, 10), s_black)
            .Frame();
        Assert.AreEqual(1.0, open.EdgeContrast(shape, s_white), "A shape with no left side has no edge there.");
    }

    // P-R5's patch reading: the brightest pixel of the patch, its luminance
    // and colour, and " LIGHT" above 0.6. Fails if a darker pixel or one
    // outside the patch (white, just right of it) is named, or if the light
    // mark is missing or wrongly given.
    [TestMethod]
    public void DescribeNamesTheBrightestPixelOfAPatch()
    {
        Color board = Color.FromRgb(0x1E, 0x1E, 0x1E);
        Color dim = Color.FromRgb(0x60, 0x60, 0x60);
        Color mid = Color.FromRgb(0xA0, 0xA0, 0xA0);
        Color light = Color.FromRgb(0xE0, 0xE0, 0xE0);
        var patch = new PixelRect(1, 1, 4, 4);
        Assert.IsLessThanOrEqualTo(Thresholds.LightPart, Contrast.RelativeLuminance(mid), "#A0A0A0 is not light.");
        Assert.IsGreaterThan(Thresholds.LightPart, Contrast.RelativeLuminance(light), "#E0E0E0 is light.");

        FramePixels dark = new CodeBitmap(8, 6, board).Set(2, 2, dim).Set(3, 4, mid).Set(5, 2, s_white).Frame();
        Assert.AreEqual($"patch: luminance {Luminance(mid)} (#A0A0A0) in {patch}", dark.Describe("patch", patch),
            "The #A0A0A0 pixel is the brightest inside; the white one lies outside.");

        FramePixels lit = new CodeBitmap(8, 6, board).Set(2, 2, mid).Set(4, 3, light).Frame();
        Assert.AreEqual($"patch: luminance {Luminance(light)} (#E0E0E0) in {patch} LIGHT", lit.Describe("patch", patch),
            "A pixel above 0.6 marks the patch light.");
    }

    private static string Luminance(Color colour) =>
        Contrast.RelativeLuminance(colour).ToString("0.000", CultureInfo.InvariantCulture);

    // A 10 x 10 swatch whose first inked positions, row by row, are ink.
    private static FramePixels Swatch(Color background, Color ink, int inked)
    {
        var bitmap = new CodeBitmap(Side, Side, background);
        for (int index = 0; index < inked; index++)
        {
            bitmap.Set(index % Side, index / Side, ink);
        }

        return bitmap.Frame();
    }

    private static void AssertPattern(double differing, bool same, FramePixels one, FramePixels other, string what)
    {
        Assert.AreEqual(differing, FramePixels.PatternDifference(one, s_swatch, other, s_swatch), 1e-12, $"The share of positions that differ, {what}.");
        Assert.AreEqual(same, FramePixels.SamePattern(one, s_swatch, other, s_swatch), $"The same pattern, {what}.");
    }

    // A bitmap made in code, opaque, in the byte order FramePixels.FromBgra reads.
    private sealed class CodeBitmap
    {
        private readonly int _width;
        private readonly int _height;
        private readonly byte[] _bytes;

        public CodeBitmap(int width, int height, Color background)
        {
            _width = width;
            _height = height;
            _bytes = new byte[width * height * 4];
            Fill(new PixelRect(0, 0, width, height), background);
        }

        public CodeBitmap Set(int x, int y, Color colour)
        {
            int offset = ((y * _width) + x) * 4;
            _bytes[offset] = colour.B;
            _bytes[offset + 1] = colour.G;
            _bytes[offset + 2] = colour.R;
            _bytes[offset + 3] = 0xFF;
            return this;
        }

        public CodeBitmap Fill(PixelRect area, Color colour)
        {
            for (int y = area.Y; y < area.Bottom; y++)
            {
                for (int x = area.X; x < area.Right; x++)
                {
                    Set(x, y, colour);
                }
            }

            return this;
        }

        public FramePixels Frame() => FramePixels.FromBgra((byte[])_bytes.Clone(), _width, _height);
    }
}
