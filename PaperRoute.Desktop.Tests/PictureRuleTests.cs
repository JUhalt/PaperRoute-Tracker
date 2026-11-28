using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PaperRoute.Desktop.Tests.Board;
using PaperRoute.Desktop.Tests.Legibility;
using PaperRoute.TestFixtures;
using PaperRoute.ViewModels.Board;
using PaperRoute.ViewModels.Session;

namespace PaperRoute.Desktop.Tests;

// The two picture tests of d1 6.2 (step 12): rules checked on the pixels the
// headless Skia platform draws, not golden images, so a change of font
// hinting does not fail them. The example library, a 1280 x 800 window, each
// picture saved beside the others for a person to look at.
//
// P-R5: in Dark, no light patch on the Board. A 4 x 4 px patch inside each
// card's padding (top-left, 3 px in from the border), the page background in
// the gap between two cards, and each chip's fill (in its left padding, at
// mid-height: a chip is fully rounded, so its top-left corner is outside it):
// no pixel of a patch above relative luminance 0.6, the "light" line of
// DarkThemeLegibilityTests.vb IsLight. No patch touches text.
//
// P-R7: in Light and in Dark, on every shelf, inside each card title's
// bounds, the strongest pixel against the card's background (read from the
// card's own patch) has a contrast of 4.5:1 or more, the strongest-pixel rule
// (plan :164).
//
// Both read the frame through Legibility/FramePixels.cs, whose primitives
// PrimitiveTests checks on bitmaps made in code.
[TestClass]
public sealed class PictureRuleTests : StorageCheckedTests
{
    private const int PatchSize = 4;

    private string _root = string.Empty;

    public TestContext TestContext { get; set; } = null!;

    [TestInitialize]
    public void CreateRoot()
    {
        _root = Path.Combine(Path.GetTempPath(), "PaperRoute-TrialPictures-" + Guid.NewGuid().ToString("N"));
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

    [TestMethod]
    public void P_R5_NoLightPatchesOnTheDarkBoard()
    {
        using DiskLibrarySession session = OpenExample();
        (string path, List<string> patches) = Headless.Run(() =>
        {
            using BoardWindow board = BoardWindow.Show(session, ThemeVariant.Dark);
            string path = Pictures.Save(board.Window, "rules-p-r5-dark", TestContext);
            FramePixels frame = FramePixels.Capture(board.Window);
            var patches = new List<string>();

            IReadOnlyList<CardView> cards = board.Cards("PipelineShelf");
            Assert.IsGreaterThanOrEqualTo(2, cards.Count, "The example's Pipeline shows two cards or more.");
            foreach (CardView card in cards)
            {
                patches.Add(frame.Describe("card " + Title(card), CardPatch(card, board.Window)));
            }

            // The page between the first two cards, in the 12 px gap.
            Rect first = Bounds(cards[0].Part<Border>("Card"), board.Window);
            Rect second = Bounds(cards[1].Part<Border>("Card"), board.Window);
            Assert.IsLessThan(1, Math.Abs(first.Y - second.Y), "The first two cards share a row.");
            Assert.IsGreaterThanOrEqualTo(first.Right + PatchSize + 4, second.X, "The gap between the cards holds a patch.");
            patches.Add(frame.Describe("page between the first two cards",
                new PixelRect((int)Math.Ceiling(first.Right + 4), (int)Math.Round(first.Center.Y), PatchSize, PatchSize)));

            List<Button> chips = board.Find<ItemsControl>("Chips").GetVisualDescendants().OfType<Button>()
                .Where(chip => chip.IsEffectivelyVisible).ToList();
            Assert.IsNotEmpty(chips, "The example shows chips.");
            foreach (Button chip in chips)
            {
                Rect bounds = Bounds(chip, board.Window);
                var patch = new PixelRect(
                    (int)Math.Ceiling(bounds.X + chip.BorderThickness.Left + 3),
                    (int)Math.Round(bounds.Center.Y - (PatchSize / 2.0)),
                    PatchSize,
                    PatchSize);
                TextBlock text = chip.GetVisualDescendants().OfType<TextBlock>().First(block => !string.IsNullOrEmpty(block.Text));
                Assert.IsLessThanOrEqualTo(Bounds(text, board.Window).X, (double)patch.Right, $"The patch of the chip \"{chip.Content}\" stays clear of its text.");
                patches.Add(frame.Describe($"chip \"{chip.Content}\"", patch));
            }

            return (path, patches);
        });

        foreach (string patch in patches)
        {
            TestContext.WriteLine(patch);
        }

        List<string> light = patches.Where(patch => patch.Contains(" LIGHT", StringComparison.Ordinal)).ToList();
        Assert.IsEmpty(light, "Light patches on the Dark Board:" + Environment.NewLine + string.Join(Environment.NewLine, light));
        Assert.IsTrue(File.Exists(path), $"No picture at {path}.");
    }

    [TestMethod]
    public void P_R7_CardTitlesHaveTheirContrastOnScreenInLight() => TitlesHaveTheirContrast(ThemeVariant.Light, "rules-p-r7-light");

    [TestMethod]
    public void P_R7_CardTitlesHaveTheirContrastOnScreenInDark() => TitlesHaveTheirContrast(ThemeVariant.Dark, "rules-p-r7-dark");

    private void TitlesHaveTheirContrast(ThemeVariant variant, string name)
    {
        using DiskLibrarySession session = OpenExample();
        (string path, List<(string Title, double Ratio)> titles) = Headless.Run(() =>
        {
            using BoardWindow board = BoardWindow.Show(session, variant);
            string path = Pictures.Save(board.Window, name, TestContext);
            var titles = new List<(string, double)>();
            foreach ((int index, string shelf) in new[] { (0, "PipelineShelf"), (1, "PublishedShelf"), (2, "FileDrawerShelf") })
            {
                board.Board.SelectedShelfIndex = index;
                Dispatcher.UIThread.RunJobs();
                board.Window.UpdateLayout();
                FramePixels frame = FramePixels.Capture(board.Window);
                foreach (CardView card in board.Cards(shelf))
                {
                    Color background = frame.At(CardPatch(card, board.Window).X + 1, CardPatch(card, board.Window).Y + 1);
                    PixelRect title = Pixels(Bounds(card.Part<TextBlock>("TitleText"), board.Window));
                    Assert.IsTrue(frame.Contains(title), $"The title of \"{Title(card)}\" lies inside the window.");
                    titles.Add(($"{shelf}: {Title(card)}", frame.StrongestContrast(title, background)));
                }
            }

            return (path, titles);
        });

        foreach ((string title, double ratio) in titles)
        {
            TestContext.WriteLine($"{ratio.ToString("0.00", CultureInfo.InvariantCulture)}:1  {title}");
        }

        Assert.AreEqual(7, titles.Count, "Every card of the example's three shelves was checked.");
        List<string> faint = titles.Where(item => item.Ratio < Thresholds.Text)
            .Select(item => $"{item.Title}: {item.Ratio.ToString("0.00", CultureInfo.InvariantCulture)}:1").ToList();
        Assert.IsEmpty(faint, $"Titles under {Thresholds.Text}:1 against their card:" + Environment.NewLine + string.Join(Environment.NewLine, faint));
        Assert.IsTrue(File.Exists(path), $"No picture at {path}.");
    }

    // The card's patch: 4 x 4 px, 3 px in from the inside of its border, top-left.
    private static PixelRect CardPatch(CardView card, Window window)
    {
        Border border = card.Part<Border>("Card");
        Rect bounds = Bounds(border, window);
        return new PixelRect(
            (int)Math.Ceiling(bounds.X + border.BorderThickness.Left + 3),
            (int)Math.Ceiling(bounds.Y + border.BorderThickness.Top + 3),
            PatchSize,
            PatchSize);
    }

    private static string Title(CardView card) => (card.DataContext as CardViewModel)?.Title ?? "(no title)";

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

    private DiskLibrarySession OpenExample()
    {
        LargeLibraryFixture.WriteExample(_root);
        var session = new DiskLibrarySession(
            Path.Combine(_root, "data"),
            Path.Combine(_root, "library"),
            _root,
            LibraryKind.Example,
            LargeLibraryFixture.Today);
        session.LoadAsync(CancellationToken.None).GetAwaiter().GetResult();
        Assert.HasCount(7, session.Manuscripts);
        return session;
    }
}
