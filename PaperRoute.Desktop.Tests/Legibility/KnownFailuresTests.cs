using System.Text.RegularExpressions;
using PaperRoute.Desktop.Styles;
using PaperRoute.Desktop.Tests.Styles;

namespace PaperRoute.Desktop.Tests.Legibility;

// The known-failures list stays current (the harness design, part 2.4): an
// entry a rule no longer reports is stale, every entry carries its issue, and
// the number of entries is written here too, so the list cannot grow unseen.
// Limits of headless: HeadlessLimits.md
[TestClass]
public sealed partial class KnownFailuresTests
{
    // How many entries KnownFailures.tsv holds. Change it with the list.
    private const int ExpectedCount = 0;

    // The rules that have a check in this project, each with what re-runs it
    // on its run tests' own inputs and gives everything it reports today,
    // listed failures included, and which entries that re-run can judge on
    // this system; an entry for another rule cannot be judged yet. Each
    // rule's pull request adds its name. A rule is re-run here only when an
    // entry names it (the teeth below re-run R1, R2 and R3 on every run, R2
    // and R3 on spoiled inputs).
    private static readonly Dictionary<string, RuleCheck> s_rulesWithACheck = new(StringComparer.Ordinal)
    {
        ["R1"] = new(() => R1Tests.Variants.SelectMany(variant => Rules.R1(variant, TokenFile.Colors(variant), TokenPairs.All)).ToList(), _ => true),
        ["R2"] = new(() => R2Tests.CurrentFindings(R2Tests.Inputs.Real), entry => R2Tests.Judges(R2Tests.RealThemeFolder, entry)),
        ["R3"] = new(() => R3Tests.CurrentFindings(RouteKinds.All), _ => true),
    };

    public TestContext TestContext { get; set; } = null!;

    // The list, read strictly; then the same check on lists spoiled one way
    // each, which must name exactly that fault.
    [TestMethod]
    public void KnownFailuresAreCurrent()
    {
        IReadOnlyList<KnownFailure> entries = KnownFailures.Load();
        IReadOnlyList<Finding> findings = CurrentFindings(entries);
        List<string> problems = Problems(entries, ExpectedCount, findings);
        foreach (KnownFailure entry in entries.Where(entry => s_rulesWithACheck.TryGetValue(entry.Rule, out RuleCheck? check) && !check.JudgedHere(entry)))
        {
            TestContext.WriteLine($"Line {entry.Line}: {entry.Rule}, {entry.Subject}, {entry.ThemeCase} cannot be judged on this system (its check does not run here), so it is not called stale.");
        }

        Assert.IsEmpty(problems, "The known-failures list:" + Environment.NewLine + string.Join(Environment.NewLine, problems));

        // The check's teeth.
        var stillFails = new Finding("R1", "Light", "MutedTextBrush on CardBackgroundBrush", "Light: MutedTextBrush on CardBackgroundBrush is 1.84:1");
        KnownFailure listed = Entry("R1\tMutedTextBrush on CardBackgroundBrush\tLight\t#1\ta muted ink too light on the card");
        Assert.IsEmpty(Problems([listed], 1, [stillFails]), "An entry the rule still reports, with its issue, is current.");

        // What the rules' runs leave out (KnownFailures.Lists): the listed
        // finding, and not the same pair in another theme case.
        var otherCase = stillFails with { ThemeCase = "Dark", Line = "Dark: MutedTextBrush on CardBackgroundBrush is 1.84:1" };
        Assert.IsTrue(KnownFailures.Lists([listed], stillFails), "A listed finding must be left out of its rule's run.");
        Assert.IsFalse(KnownFailures.Lists([listed], otherCase), "The same pair in a theme case the list does not name must still be reported.");

        KnownFailure noIssue = Entry("R1\tMutedTextBrush on CardBackgroundBrush\tLight\t\ta muted ink too light on the card");
        Assert.AreEqual(
            "Line 10: R1, MutedTextBrush on CardBackgroundBrush, Light has no issue number (\"#\" and its number).",
            string.Join(Environment.NewLine, Problems([noIssue], 1, [stillFails])),
            "An entry without its issue.");

        Assert.AreEqual(
            "Line 10: R1, MutedTextBrush on CardBackgroundBrush, Light is stale: R1 no longer reports it.",
            string.Join(Environment.NewLine, Problems([listed], 1, CurrentFindings([listed]))),
            "An entry the real token file no longer fails.");

        // R2 and R3 are judged the same way, each by re-running its checks on
        // the inputs its run tests use. Here the re-runs get those inputs
        // spoiled one way per check (the faults of R2Tests' and R3Tests' own
        // teeth), so every check must report its entry, through the set or
        // theme case that ends its loop; an entry the spoiling leaves alone is
        // stale. A re-run that dropped a check, a set or a theme case would
        // leave its entry stale here.
        R2Tests.Inputs real = R2Tests.Inputs.Real;
        Dictionary<string, SystemColorRole> accentOnText = real.Roles.ToDictionary(StringComparer.Ordinal);
        accentOnText["AccentBrush"] = SystemColorRole.WindowText;
        R2Tests.Inputs spoiled = real with
        {
            Keys = [.. real.Keys, "UnmappedTestBrush"],
            Roles = accentOnText,
            Sets = real.Sets.Select(set => set.Name == "hc1" ? (set.Name, set.Colors with { GrayText = set.Colors.Window }) : set).ToList(),
            ThemeFolder = Path.Combine(AppContext.BaseDirectory, "no theme folder"),
        };
        IReadOnlyList<Finding> r2Findings = R2Tests.CurrentFindings(spoiled);
        KnownFailure[] r2Current =
        [
            Entry("R2\tUnmappedTestBrush\tcontrast\t#1\tcheck 1, the role table: a key with no role"),
            Entry("R2\tUnmappedTestBrush\thcwhite\t#1\tcheck 1, in a shown window: a key with no role"),
            Entry("R2\tOnAccentTextBrush on AccentBrush\tcontrast\t#1\tcheck 2: the accent on WindowText"),
            Entry("R2\tMutedTextBrush on CardBackgroundBrush\thc1\t#1\tcheck 3: hc1's GrayText as its Window"),
            Entry("R2\thcwhite.theme\thcwhite\t#1\tthe theme files: a folder without them"),
        ];
        Assert.IsEmpty(Problems(r2Current, r2Current.Length, r2Findings), "Each R2 check reports its entry on the spoiled inputs.");
        KnownFailure r2Stale = Entry("R2\tMutedTextBrush on CardBackgroundBrush\thc2\t#1\ta muted ink too faint in hc2");
        Assert.AreEqual(
            "Line 10: R2, MutedTextBrush on CardBackgroundBrush, hc2 is stale: R2 no longer reports it.",
            string.Join(Environment.NewLine, Problems([r2Stale], 1, r2Findings)),
            "An R2 entry for the real hc2, which passes check 3.");

        // A theme file's entry is judged only where the theme files are read
        // (on Windows): elsewhere R2's re-run cannot report it, so it is not
        // stale there; every other entry is judged everywhere.
        KnownFailure[] themeFiles =
        [
            Entry("R2\thc1.theme GrayText\thc1\t#1\ta theme file that differs from the app's set"),
            Entry("R2\thc1.theme\thc1\t#1\ta theme file missing"),
        ];
        Assert.IsEmpty(Problems(themeFiles, 2, [], entry => R2Tests.Judges(null, entry)), "Where no theme file is read, a theme file's entry is not judged.");
        Assert.AreEqual(
            "Line 10: R2, hc1.theme GrayText, hc1 is stale: R2 no longer reports it." + Environment.NewLine +
            "Line 10: R2, hc1.theme, hc1 is stale: R2 no longer reports it.",
            string.Join(Environment.NewLine, Problems(themeFiles, 2, [], entry => R2Tests.Judges(spoiled.ThemeFolder, entry))),
            "Where the theme files are read, a theme file's entry R2 does not report is stale.");
        Assert.AreEqual(
            "Line 10: R2, MutedTextBrush on CardBackgroundBrush, hc2 is stale: R2 no longer reports it.",
            string.Join(Environment.NewLine, Problems([r2Stale], 1, [], entry => R2Tests.Judges(null, entry))),
            "Where no theme file is read, any other R2 entry is still judged.");

        RouteKind current = RouteKinds.All.Single(kind => kind.Name == "current");
        RouteKind accepted = RouteKinds.All.Single(kind => kind.Name == "accepted");
        IReadOnlyList<Finding> r3Findings = R3Tests.CurrentFindings([R3Tests.ClosedAtTenPixels, current, accepted]);
        KnownFailure[] r3Current =
        [
            Entry("R3\tclosed at 10 px and accepted\tLight\t#1\ttwo route dots alike without colour"),
            Entry("R3\tclosed at 10 px and accepted\thcwhite\t#1\ttwo route dots alike under contrast"),
        ];
        Assert.IsEmpty(Problems(r3Current, r3Current.Length, r3Findings), "R3 reports its entries on the spoiled kinds, in the first and the last theme case.");
        KnownFailure r3Stale = Entry("R3\tcurrent and accepted\tLight\t#1\ttwo route dots alike without colour");
        Assert.AreEqual(
            "Line 10: R3, current and accepted, Light is stale: R3 no longer reports it.",
            string.Join(Environment.NewLine, Problems([r3Stale], 1, r3Findings)),
            "An R3 entry for the real current and accepted dots, which differ.");

        KnownFailure noCheck = Entry("R5\tthe card\tDark\t#1\ta light patch");
        Assert.AreEqual(
            "Line 10: R5, the card, Dark names a rule with no check in this project yet.",
            string.Join(Environment.NewLine, Problems([noCheck], 1, CurrentFindings([noCheck]))),
            "An entry for a rule not built yet.");

        Assert.AreEqual(
            $"The list holds {entries.Count} entries; {nameof(KnownFailuresTests)}.{nameof(ExpectedCount)} says {ExpectedCount + 1}.",
            string.Join(Environment.NewLine, Problems(entries, ExpectedCount + 1, findings)),
            "A count that differs from the constant.");

        Assert.Throws<FormatException>(() => KnownFailures.Parse([KnownFailures.Header, "R1\tMutedTextBrush on CardBackgroundBrush\tLight\t#1"], "spoiled"),
            "A line with four cells must not be read.");
    }

    // What is wrong with the list, judged against the findings; judgedHere
    // says which entries the re-runs can judge on this system (by default,
    // each rule's own answer).
    private static List<string> Problems(IReadOnlyList<KnownFailure> entries, int expectedCount, IReadOnlyList<Finding> findings, Func<KnownFailure, bool>? judgedHere = null)
    {
        var problems = new List<string>();
        if (entries.Count != expectedCount)
        {
            problems.Add($"The list holds {entries.Count} entries; {nameof(KnownFailuresTests)}.{nameof(ExpectedCount)} says {expectedCount}.");
        }

        foreach (KnownFailure entry in entries)
        {
            string name = $"Line {entry.Line}: {entry.Rule}, {entry.Subject}, {entry.ThemeCase}";
            if (!IssueNumber().IsMatch(entry.Issue))
            {
                problems.Add($"{name} has no issue number (\"#\" and its number).");
            }

            // An entry whose check does not run on this system is not judged
            // stale here; a system where it runs judges it.
            if (!s_rulesWithACheck.TryGetValue(entry.Rule, out RuleCheck? check))
            {
                problems.Add($"{name} names a rule with no check in this project yet.");
            }
            else if ((judgedHere ?? check.JudgedHere)(entry) && !findings.Any(finding => finding.Rule == entry.Rule && finding.Subject == entry.Subject && finding.ThemeCase == entry.ThemeCase))
            {
                problems.Add($"{name} is stale: {entry.Rule} no longer reports it.");
            }
        }

        return problems;
    }

    // What the rules the entries name report today, listed failures included:
    // each such rule with a check, re-run once.
    private static IReadOnlyList<Finding> CurrentFindings(IReadOnlyList<KnownFailure> entries) =>
        entries.Select(entry => entry.Rule).Distinct(StringComparer.Ordinal)
            .Where(s_rulesWithACheck.ContainsKey)
            .SelectMany(rule => s_rulesWithACheck[rule].Rerun())
            .ToList();

    // One rule's check: what re-runs it, and which entries that re-run can
    // judge on this system.
    private sealed record RuleCheck(Func<IReadOnlyList<Finding>> Rerun, Func<KnownFailure, bool> JudgedHere);

    // One data line, read as the tenth line of a file with the real header.
    private static KnownFailure Entry(string line)
    {
        string[] lines = [.. Enumerable.Repeat("#", 8), KnownFailures.Header, line];
        return KnownFailures.Parse(lines, "spoiled").Single();
    }

    [GeneratedRegex(@"^#[1-9][0-9]*$")]
    private static partial Regex IssueNumber();
}
