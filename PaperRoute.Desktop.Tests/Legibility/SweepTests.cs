using System.Text.RegularExpressions;

namespace PaperRoute.Desktop.Tests.Legibility;

// What the sweeps (Sweep) promise about their keys, the names a known
// failure is listed by (KnownFailures.tsv).
// Limits of headless: HeadlessLimits.md
[TestClass]
public sealed partial class SweepTests
{
    public TestContext TestContext { get; set; } = null!;

    // Every element of the style gallery is keyed by the named specimen it
    // belongs to (StyleGallery/Type#Name/...), so a gallery row added later
    // (each new class gets one, the harness design, part 3.2) renumbers no
    // other key, and no listed failure goes stale by it. The window's own
    // parts (Window, its template's borders) are not the gallery's, and the
    // gallery's page (StyleGallery/Border, first in the tree) is not named,
    // because a named page would prefix every key.
    [TestMethod]
    public void EveryGalleryElementIsKeyedByItsSpecimensName()
    {
        SweepSubject gallery = Sweep.Subjects.Single(subject => subject.Name == "Styles/Gallery");
        SweepFrame read = Sweep.Read(gallery, ThemeCase.Light, TestContext);
        Assert.IsNotEmpty(read.Parts, "The sweep found parts on the gallery.");
        Assert.IsNotEmpty(read.Texts, "The sweep found texts on the gallery.");

        List<string> keys = [.. read.Parts.Select(part => part.Part), .. read.Texts.Select(text => text.Element)];
        Assert.IsGreaterThan(100, keys.Count(key => key.StartsWith("StyleGallery/", StringComparison.Ordinal)), "The gallery's elements are found.");
        List<string> unnamed = keys
            .Where(key => key.StartsWith("StyleGallery/", StringComparison.Ordinal) && key != "StyleGallery/Border" && !SpecimenKey().IsMatch(key))
            .ToList();
        Assert.IsEmpty(unnamed, $"{unnamed.Count} of {keys.Count} gallery elements are not keyed by a named specimen:" + Environment.NewLine + string.Join(Environment.NewLine, unnamed));
    }

    // The gallery, then a step of the form Type#Name (a named specimen).
    [GeneratedRegex(@"^StyleGallery/\w+#\w+(/|$)")]
    private static partial Regex SpecimenKey();
}
