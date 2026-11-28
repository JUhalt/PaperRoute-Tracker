namespace PaperRoute.Desktop.Tests.Legibility;

// Every measured value a rule computes goes to legibility-<rule>-<case>.txt
// beside the pictures (harness design 2.1), so a reviewer reads numbers, not
// pictures; the CI trial job uploads the folder with the pictures.
// Limits of headless: HeadlessLimits.md
internal static class MeasuredValues
{
    // Writes the lines and returns the file's full path.
    public static string Write(string rule, string themeCase, IEnumerable<string> lines, TestContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        string folder = Pictures.Directory();
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, $"legibility-{rule.ToLowerInvariant()}-{themeCase.ToLowerInvariant()}.txt");
        File.WriteAllLines(path, lines);
        context.WriteLine($"Measured values saved: {path}");
        return path;
    }
}
