using System.Text.RegularExpressions;
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
    // and gives everything it reports today, listed failures included; an
    // entry for another rule cannot be judged yet. Each rule's pull request
    // adds its name. A rule is re-run only when an entry names it.
    private static readonly Dictionary<string, Func<IReadOnlyList<Finding>>> s_rulesWithACheck = new(StringComparer.Ordinal)
    {
        ["R1"] = () => R1Tests.Variants.SelectMany(variant => Rules.R1(variant, TokenFile.Colors(variant), TokenPairs.All)).ToList(),
        ["R2"] = R2Tests.CurrentFindings,
        ["R3"] = R3Tests.CurrentFindings,
    };

    // The list, read strictly; then the same check on lists spoiled one way
    // each, which must name exactly that fault.
    [TestMethod]
    public void KnownFailuresAreCurrent()
    {
        IReadOnlyList<KnownFailure> entries = KnownFailures.Load();
        IReadOnlyList<Finding> findings = CurrentFindings(entries);
        List<string> problems = Problems(entries, ExpectedCount, findings);
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

        // R2 and R3 are judged the same way, each by re-running its checks:
        // the real token file passes check 3 in hc1, and the real route dots
        // differ in Light, so these entries are stale; while a rule still
        // reports an entry, it is current.
        KnownFailure r2 = Entry("R2\tMutedTextBrush on CardBackgroundBrush\thc1\t#1\ta muted ink too faint in hc1");
        var r2Fails = new Finding("R2", "hc1", "MutedTextBrush on CardBackgroundBrush", "hc1: text on a page, card or header: MutedTextBrush on CardBackgroundBrush is 1.00:1");
        Assert.IsEmpty(Problems([r2], 1, [r2Fails]), "An R2 entry R2 still reports is current.");
        Assert.AreEqual(
            "Line 10: R2, MutedTextBrush on CardBackgroundBrush, hc1 is stale: R2 no longer reports it.",
            string.Join(Environment.NewLine, Problems([r2], 1, CurrentFindings([r2]))),
            "An R2 entry the real token file and contrast sets no longer fail.");

        KnownFailure r3 = Entry("R3\tclosed and accepted\tLight\t#1\ttwo route dots alike without colour");
        var r3Fails = new Finding("R3", "Light", "closed and accepted", "closed and accepted: 5.0 % of positions differ; commonest colours 1.09:1: the same pattern and under 3:1");
        Assert.IsEmpty(Problems([r3], 1, [r3Fails]), "An R3 entry R3 still reports is current.");
        Assert.AreEqual(
            "Line 10: R3, closed and accepted, Light is stale: R3 no longer reports it.",
            string.Join(Environment.NewLine, Problems([r3], 1, CurrentFindings([r3]))),
            "An R3 entry the real route dots no longer fail.");

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

    private static List<string> Problems(IReadOnlyList<KnownFailure> entries, int expectedCount, IReadOnlyList<Finding> findings)
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

            if (!s_rulesWithACheck.ContainsKey(entry.Rule))
            {
                problems.Add($"{name} names a rule with no check in this project yet.");
            }
            else if (!findings.Any(finding => finding.Rule == entry.Rule && finding.Subject == entry.Subject && finding.ThemeCase == entry.ThemeCase))
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
            .SelectMany(rule => s_rulesWithACheck[rule]())
            .ToList();

    // One data line, read as the tenth line of a file with the real header.
    private static KnownFailure Entry(string line)
    {
        string[] lines = [.. Enumerable.Repeat("#", 8), KnownFailures.Header, line];
        return KnownFailures.Parse(lines, "spoiled").Single();
    }

    [GeneratedRegex(@"^#[1-9][0-9]*$")]
    private static partial Regex IssueNumber();
}
