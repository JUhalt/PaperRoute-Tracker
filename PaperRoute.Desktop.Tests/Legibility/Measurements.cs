using System.Globalization;

namespace PaperRoute.Desktop.Tests.Legibility;

// Every value a rule measures, written to legibility-<rule>-<case>.txt beside
// the pictures (Pictures.Directory, so the CI trial job's artifact carries
// it), so a reviewer reads numbers, not pictures (the harness design, part
// 2.1). The first line names the columns; then one line per measurement,
// its cells separated by tabs, numbers in the invariant culture. A run
// writes its file anew, so a second run never adds to the first. The name is
// in lower case for every rule and theme case (legibility-r1-light.txt,
// legibility-r2-hc1.txt), as the pictures beside it are named.
// Limits of headless: HeadlessLimits.md
internal static class Measurements
{
    public static string FileName(string rule, string themeCase) => $"legibility-{rule}-{themeCase}.txt".ToLowerInvariant();

    // Writes the file into the folder (Pictures.Directory() for a rule's run)
    // and returns its full path.
    public static string Write(string folder, string rule, string themeCase, IReadOnlyList<string> columns, IEnumerable<IReadOnlyList<string>> rows)
    {
        var lines = new List<string> { Line(columns, columns.Count) };
        lines.AddRange(rows.Select(row => Line(row, columns.Count)));
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, FileName(rule, themeCase));
        File.WriteAllLines(path, lines);
        return path;
    }

    // A ratio or other measured number, to two decimals, whatever the culture.
    public static string Number(double value) => value.ToString("F2", CultureInfo.InvariantCulture);

    private static string Line(IReadOnlyList<string> cells, int count)
    {
        if (cells.Count != count)
        {
            throw new ArgumentException($"A measurement line has {cells.Count} cells, not {count}: {string.Join(" | ", cells)}", nameof(cells));
        }

        if (cells.Any(cell => cell.AsSpan().IndexOfAny('\t', '\r', '\n') >= 0))
        {
            throw new ArgumentException($"A measurement cell holds a tab or a line break: {string.Join(" | ", cells)}", nameof(cells));
        }

        return string.Join('\t', cells);
    }
}
