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

    // The rules that have a check in this project; an entry for another rule
    // cannot be judged yet. Each rule's pull request adds its name.
    private static readonly string[] s_rulesWithACheck = ["R1"];

    // The list, read strictly; then the same check on lists spoiled one way
    // each, which must name exactly that fault.
    [TestMethod]
    public void KnownFailuresAreCurrent()
    {
        IReadOnlyList<KnownFailure> entries = KnownFailures.Load();
        IReadOnlyList<Finding> findings = CurrentFindings();
        List<string> problems = Problems(entries, ExpectedCount, findings);
        Assert.IsEmpty(problems, "The known-failures list:" + Environment.NewLine + string.Join(Environment.NewLine, problems));

        // The check's teeth.
        var stillFails = new Finding("R1", "Light", "MutedTextBrush on CardBackgroundBrush", "Light: MutedTextBrush on CardBackgroundBrush is 1.84:1");
        KnownFailure listed = Entry("R1\tMutedTextBrush on CardBackgroundBrush\tLight\t#1\ta muted ink too light on the card");
        Assert.IsEmpty(Problems([listed], 1, [stillFails]), "An entry the rule still reports, with its issue, is current.");

        // What the R1 runs leave out (KnownFailures.Lists): the listed finding,
        // and not the same pair in another theme case.
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
            string.Join(Environment.NewLine, Problems([listed], 1, findings)),
            "An entry the real token file no longer fails.");

        KnownFailure noCheck = Entry("R5\tthe card\tDark\t#1\ta light patch");
        Assert.AreEqual(
            "Line 10: R5, the card, Dark names a rule with no check in this project yet.",
            string.Join(Environment.NewLine, Problems([noCheck], 1, findings)),
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

            if (!s_rulesWithACheck.Contains(entry.Rule, StringComparer.Ordinal))
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

    // What the rules with a check report today, listed failures included.
    private static IReadOnlyList<Finding> CurrentFindings() =>
        R1Tests.Variants.SelectMany(variant => Rules.R1(variant, TokenFile.Colors(variant), TokenPairs.All)).ToList();

    // One data line, read as the tenth line of a file with the real header.
    private static KnownFailure Entry(string line)
    {
        string[] lines = [.. Enumerable.Repeat("#", 8), KnownFailures.Header, line];
        return KnownFailures.Parse(lines, "spoiled").Single();
    }

    [GeneratedRegex(@"^#[1-9][0-9]*$")]
    private static partial Regex IssueNumber();
}
