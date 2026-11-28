namespace PaperRoute.Desktop.Tests.Legibility;

// One line of KnownFailures.tsv: a real fault a rule finds, kept with its issue.
internal sealed record KnownFailure(int Line, string Rule, string Subject, string ThemeCase, string Issue, string Why);

// Reads KnownFailures.tsv (copied to the test output), strictly: comment lines
// start with "#", the first other line is the header, and every line has five
// tab-separated cells. A malformed line throws with the file and line number.
// The issue cell may be empty here so that KnownFailuresAreCurrent can name
// the entry that lacks one.
// Limits of headless: HeadlessLimits.md
internal static class KnownFailures
{
    public const string Header = "rule\tsubject\ttheme case\tissue\twhy";

    public static string FilePath => Path.Combine(AppContext.BaseDirectory, "Legibility", "KnownFailures.tsv");

    public static IReadOnlyList<KnownFailure> Load() => Parse(File.ReadAllLines(FilePath), "Legibility/KnownFailures.tsv");

    public static IReadOnlyList<KnownFailure> Parse(IReadOnlyList<string> lines, string name)
    {
        var entries = new List<KnownFailure>();
        bool headerSeen = false;
        for (int index = 0; index < lines.Count; index++)
        {
            string line = lines[index];
            int number = index + 1;
            if (line.StartsWith('#'))
            {
                continue;
            }

            if (!headerSeen)
            {
                if (line != Header)
                {
                    throw new FormatException($"{name}:{number}: the header must be \"{Header.Replace("\t", "<tab>", StringComparison.Ordinal)}\".");
                }

                headerSeen = true;
                continue;
            }

            string[] cells = line.Split('\t');
            if (cells.Length != 5)
            {
                throw new FormatException($"{name}:{number}: {cells.Length} cells, not 5.");
            }

            if (cells.Where((cell, column) => column != 3 && cell.Trim().Length == 0).Any())
            {
                throw new FormatException($"{name}:{number}: an empty cell.");
            }

            entries.Add(new KnownFailure(number, cells[0], cells[1], cells[2], cells[3], cells[4]));
        }

        if (!headerSeen)
        {
            throw new FormatException($"{name}: no header line.");
        }

        return entries;
    }

    // Whether the list holds this finding: the same rule, subject and theme case.
    public static bool Lists(IEnumerable<KnownFailure> entries, Finding finding) =>
        entries.Any(entry => entry.Rule == finding.Rule && entry.Subject == finding.Subject && entry.ThemeCase == finding.ThemeCase);
}
