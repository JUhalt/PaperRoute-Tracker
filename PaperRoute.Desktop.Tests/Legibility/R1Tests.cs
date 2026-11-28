using System.Globalization;
using System.Text.RegularExpressions;
using PaperRoute.Desktop.Tests.Styles;

namespace PaperRoute.Desktop.Tests.Legibility;

// R1, every ink on its surface at 4.5:1, run over the real token file in
// Light and Dark (the harness design, part 2.4; the contrast cases join it
// with R2's theme cases). The trial's T1 and T2 grew into the first two tests
// here; R1_FailsADeliberatelyLowContrastToken (RuleTeethTests.cs) gives R1
// its teeth through the same reader and the same rule. A failure listed in
// KnownFailures.tsv does not fail these runs (KnownFailuresAreCurrent). Each
// run writes every ratio R1 measures to legibility-r1-<case>.txt beside the
// pictures, passing pairs included, so a reviewer reads the margins.
// Limits of headless: HeadlessLimits.md
[TestClass]
public sealed partial class R1Tests
{
    // The theme cases R1 runs in today.
    internal static IReadOnlyList<string> Variants { get; } = ["Light", "Dark"];

    // The properties whose brush is drawn as text, and those whose brush is
    // drawn under it (Fill: the route dots are shapes, not borders).
    private static readonly HashSet<string> s_inkProperties =
        new(["Foreground", "PlaceholderForeground", "SelectionForegroundBrush"], StringComparer.Ordinal);

    private static readonly HashSet<string> s_surfaceProperties = new(["Background", "Fill"], StringComparer.Ordinal);

    // Tokens the styles paint as a background that never carries text, each
    // with its reason. An entry that is no longer such a background fails.
    private static readonly Dictionary<string, string> s_neverUnderText = new(StringComparer.Ordinal)
    {
        ["MutedTextBrush"] = "the route dots' fill (Board.axaml, Ellipse.route-dot and .closed): a dot, with no text on it",
        ["CardBorderBrush"] = "1 px hairlines: the rail's rule (Rail.axaml, Border.rail-rule) and the shelf bar's baseline (Tabs.axaml, Border.hairline)",
        ["SubtleBorderBrush"] = "the 1 px line inside a card (Card.axaml, Border.divider)",
    };

    // The columns of legibility-r1-<case>.txt.
    private static readonly string[] s_ratioColumns = ["theme case", "pair", "ratio", "where"];

    public TestContext TestContext { get; set; } = null!;

    // Was T1_EveryTextPairTheBoardUsesHasItsContrast.
    // Every text and background pair the Board uses, in Light and Dark, at
    // 4.5:1. The design asked only 3:1 for muted and disabled text (R8); today's
    // suite holds every pair to 4.5:1, muted and disabled text included
    // (WorkflowThemeKeyboardSmokeTests.DrawnPairs, its "off" buttons), and
    // UiTheme.MutedText promises 4.5:1 on the board and card, so the trial does too.
    [TestMethod]
    public void R1_EveryTextPairHasItsContrast()
    {
        IReadOnlyList<KnownFailure> known = KnownFailures.Load();
        var findings = new List<Finding>();
        int checkedPairs = 0;
        foreach (string variant in Variants)
        {
            TestContext.WriteLine("R1's ratios: " + Path.GetFileName(WriteRatios(Pictures.Directory(), variant)));
            findings.AddRange(Rules.R1(variant, TokenFile.Colors(variant), TokenPairs.Text).Where(finding => !KnownFailures.Lists(known, finding)));
            checkedPairs += TokenPairs.Text.Count;
        }

        Assert.IsGreaterThan(40, checkedPairs, "Too few pairs were checked.");
        Assert.IsEmpty(findings, "Pairs under 4.5:1:" + Environment.NewLine + string.Join(Environment.NewLine, findings));
    }

    // Was T2_EveryStageAndTagPairIsLegibleInBothThemes.
    // The eight stage pills and the eight tag palettes, Light and Dark, at
    // 4.5:1 (WorkTypeAndTagTests.EveryTagColorIsLegibleInBothThemes today).
    [TestMethod]
    public void R1_EveryStageAndTagPairHasItsContrast()
    {
        IReadOnlyList<KnownFailure> known = KnownFailures.Load();
        var findings = new List<Finding>();
        foreach (string variant in Variants)
        {
            TestContext.WriteLine("R1's ratios: " + Path.GetFileName(WriteRatios(Pictures.Directory(), variant)));
            findings.AddRange(Rules.R1(variant, TokenFile.Colors(variant), TokenPairs.StagesAndTags).Where(finding => !KnownFailures.Lists(known, finding)));
        }

        Assert.AreEqual(16, TokenPairs.StagesAndTags.Count, "Eight stages and eight tag palettes.");
        Assert.IsEmpty(findings, "Pairs under 4.5:1:" + Environment.NewLine + string.Join(Environment.NewLine, findings));
    }

    // The numbers file (the harness design, part 2.1): legibility-r1-<case>.txt
    // holds every pair R1 judges in that case, one line each, with its ratio
    // in the invariant culture (here written under a culture with a decimal
    // comma), from the thinnest margin up; a second write replaces the first.
    [TestMethod]
    public void R1_WritesEveryRatioItMeasures()
    {
        string folder = Path.Combine(AssemblySetup.StorageRoot, "r1-ratios-" + Guid.NewGuid().ToString("N"));
        int pairCount = TokenPairs.All.Count();
        var lines = new Dictionary<string, string[]>(StringComparer.Ordinal);
        CultureInfo before = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            foreach (string variant in Variants)
            {
                WriteRatios(folder, variant);
                string path = WriteRatios(folder, variant);
                Assert.AreEqual($"legibility-r1-{variant.ToLowerInvariant()}.txt", Path.GetFileName(path), "One lower-case name for every rule and theme case.");
                lines[variant] = File.ReadAllLines(path);
            }
        }
        finally
        {
            CultureInfo.CurrentCulture = before;
        }

        Assert.AreEqual("legibility-r2-hcwhite.txt", Measurements.FileName("R2", "hcwhite"), "R2's files take the same form.");
        Assert.AreEqual("legibility-r3-light.txt", Measurements.FileName("R3", "Light"), "R3's files take the same form.");

        foreach ((string variant, string[] file) in lines)
        {
            Assert.AreEqual(string.Join('\t', s_ratioColumns), file[0], "The first line names the columns.");
            Assert.HasCount(pairCount + 1, file, $"{variant}: one line per pair, once, after two writes.");
            string[][] rows = file.Skip(1).Select(line => line.Split('\t')).ToArray();
            Assert.IsTrue(rows.All(cells => cells.Length == 4 && cells[0] == variant), $"{variant}: every line has four cells and names its theme case.");
            double[] ratios = rows.Select(cells => double.Parse(cells[2], NumberStyles.Float, CultureInfo.InvariantCulture)).ToArray();
            CollectionAssert.AreEqual(ratios.Order().ToArray(), ratios, $"{variant}: the lines run from the thinnest margin up.");
        }

        // Two of the thin margins the design names (part 2.4), as the file writes them.
        CollectionAssert.Contains(lines["Light"], "Light\tMutedTextBrush on BoardBackgroundBrush\t4.64\ttext on a page, card or header");
        CollectionAssert.Contains(lines["Dark"], "Dark\tMutedTextBrush on CardBackgroundBrush\t4.60\ttext on a page, card or header");
    }

    // Writes every ratio R1 measures in one theme case, over all its pairs and
    // sorted from the thinnest margin up, to legibility-r1-<case>.txt in the
    // folder, and returns the file's path. Both runs write the whole file, the
    // same each time, so either run alone leaves it complete.
    private static string WriteRatios(string folder, string variant) =>
        Measurements.Write(
            folder,
            "R1",
            variant,
            s_ratioColumns,
            Rules.R1Ratios(variant, TokenFile.Colors(variant), TokenPairs.All)
                .OrderBy(measured => measured.Ratio)
                .Select(measured => (IReadOnlyList<string>)
                    [measured.ThemeCase, $"{measured.Pair.Ink} on {measured.Pair.Surface}", Measurements.Number(measured.Ratio), measured.Pair.Where]));

    // The completeness guard: every token an .axaml file uses as an ink
    // (Foreground, PlaceholderForeground, SelectionForegroundBrush) is the ink
    // of an R1 pair, and every token it uses as a background (Background, Fill)
    // is the surface of one, unless it is on the never-under-text list. So a
    // style that starts drawing text in a new colour, or on a new surface,
    // fails here until its pair is declared and R1 judges it. A DynamicResource
    // in a form the scan cannot read fails too, so nothing passes unseen.
    [TestMethod]
    public void R1_EveryInkAndSurfaceTheStylesUseIsInAPair()
    {
        var inks = new SortedDictionary<string, string>(StringComparer.Ordinal);
        var surfaces = new SortedDictionary<string, string>(StringComparer.Ordinal);
        var unread = new List<string>();
        foreach (string file in TokenFile.AllAxamlFiles())
        {
            string name = Path.GetRelativePath(TokenFile.AxamlRoot, file).Replace('\\', '/');
            (List<(string Property, string Key)> uses, List<int> unreadLines) = BrushUses(File.ReadAllText(file));
            unread.AddRange(unreadLines.Select(line => $"{name}:{line}"));
            foreach ((string property, string key) in uses)
            {
                string leaf = property[(property.LastIndexOf('.') + 1)..];
                if (s_inkProperties.Contains(leaf))
                {
                    inks.TryAdd(key, $"{name}, {property}");
                }
                else if (s_surfaceProperties.Contains(leaf))
                {
                    surfaces.TryAdd(key, $"{name}, {property}");
                }
            }
        }

        TestContext.WriteLine("Inks: " + string.Join(", ", inks.Keys));
        TestContext.WriteLine("Backgrounds: " + string.Join(", ", surfaces.Keys));
        Assert.IsEmpty(unread, "A DynamicResource the guard cannot read (write a Setter as Property, then Value):" + Environment.NewLine + string.Join(Environment.NewLine, unread));
        Assert.IsGreaterThanOrEqualTo(10, inks.Count, "Too few inks were found; the scan of the .axaml files is broken.");
        Assert.IsGreaterThanOrEqualTo(10, surfaces.Count, "Too few backgrounds were found; the scan of the .axaml files is broken.");

        var pairInks = TokenPairs.All.Select(pair => pair.Ink).ToHashSet(StringComparer.Ordinal);
        var pairSurfaces = TokenPairs.All.Select(pair => pair.Surface).ToHashSet(StringComparer.Ordinal);
        var problems = new List<string>();
        foreach ((string key, string use) in inks.Where(ink => !pairInks.Contains(ink.Key)))
        {
            problems.Add($"{key} is drawn as text ({use}) but is the ink of no R1 pair.");
        }

        foreach ((string key, string use) in surfaces.Where(surface => !pairSurfaces.Contains(surface.Key) && !s_neverUnderText.ContainsKey(surface.Key)))
        {
            problems.Add($"{key} is drawn as a background ({use}) but is the surface of no R1 pair and not on the never-under-text list.");
        }

        foreach (string key in s_neverUnderText.Keys.Where(key => !surfaces.ContainsKey(key) || pairSurfaces.Contains(key)))
        {
            problems.Add($"{key} is on the never-under-text list, but no style draws it as a background, or an R1 pair already has it as a surface.");
        }

        Assert.IsEmpty(problems, "R1's pairs are incomplete:" + Environment.NewLine + string.Join(Environment.NewLine, problems));
    }

    // The guard's own teeth: a DynamicResource in a form the two patterns do
    // not read must fail the guard, not pass unseen. A Setter that writes
    // Value before Property is such a form; a comment is not a use.
    [TestMethod]
    public void R1_TheCompletenessGuardRefusesAFormItCannotRead()
    {
        const string read = """
            <Style Selector="TextBlock.a">
              <!-- <Setter Value="{DynamicResource PrimaryTextBrush}" Property="Foreground" /> -->
              <Setter Property="Foreground" Value="{DynamicResource PrimaryTextBrush}" />
            </Style>
            <Border Background="{DynamicResource CardBackgroundBrush}" />
            """;
        (List<(string Property, string Key)> uses, List<int> unread) = BrushUses(read);
        CollectionAssert.AreEqual(
            new[] { ("Foreground", "PrimaryTextBrush"), ("Background", "CardBackgroundBrush") },
            uses,
            "The guard must read a Property-then-Value setter and an attribute, and skip the comment.");
        Assert.IsEmpty(unread, "Nothing in the readable text may be left unread.");

        const string unreadable = """
            <Style Selector="TextBlock.b">
              <Setter Value="{DynamicResource MutedTextBrush}" Property="Foreground" />
            </Style>
            """;
        (uses, unread) = BrushUses(unreadable);
        Assert.IsEmpty(uses, "A Value-then-Property setter is not a use the guard can name.");
        CollectionAssert.AreEqual(new[] { 2 }, unread, "The guard must report the setter it cannot read, by its line.");
    }

    // Each (property, token) an .axaml file sets through DynamicResource, as a
    // Setter (Property, then Value) or as an attribute, with comments left
    // out; and the line of every "{DynamicResource" neither form reads, so
    // the guard fails on a form it would otherwise miss.
    private static (List<(string Property, string Key)> Uses, List<int> UnreadLines) BrushUses(string text)
    {
        // Comments become spaces, newlines kept, so positions and lines stay.
        string code = XmlComment().Replace(text, comment => NotNewline().Replace(comment.Value, " "));
        var uses = new List<(string Property, string Key)>();
        var read = new List<(int Start, int End)>();
        foreach (Match match in SetterUse().Matches(code))
        {
            uses.Add((match.Groups["property"].Value, match.Groups["key"].Value));
            read.Add((match.Index, match.Index + match.Length));
        }

        foreach (Match match in AttributeUse().Matches(code).Where(match => match.Groups["property"].Value != "Value"))
        {
            uses.Add((match.Groups["property"].Value, match.Groups["key"].Value));
            read.Add((match.Index, match.Index + match.Length));
        }

        var unreadLines = new List<int>();
        for (int at = code.IndexOf("{DynamicResource", StringComparison.Ordinal); at >= 0; at = code.IndexOf("{DynamicResource", at + 1, StringComparison.Ordinal))
        {
            if (!read.Any(span => span.Start <= at && at < span.End))
            {
                unreadLines.Add(code.AsSpan(0, at).Count('\n') + 1);
            }
        }

        return (uses, unreadLines);
    }

    [GeneratedRegex(@"<!--.*?-->", RegexOptions.Singleline)]
    private static partial Regex XmlComment();

    [GeneratedRegex(@"[^\n]")]
    private static partial Regex NotNewline();

    [GeneratedRegex(@"Property=""(?<property>[\w.]+)""\s+Value=""\{DynamicResource\s+(?:ResourceKey=)?(?<key>\w+)\s*\}""")]
    private static partial Regex SetterUse();

    [GeneratedRegex(@"(?<=\s)(?<property>[\w.]+)=""\{DynamicResource\s+(?:ResourceKey=)?(?<key>\w+)\s*\}""")]
    private static partial Regex AttributeUse();
}
