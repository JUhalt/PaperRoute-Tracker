using System.Reflection;
using System.Text.RegularExpressions;

namespace PaperRoute.Desktop.Tests.Startup;

// Velopack first (start-up design 2.4, step 0; today's Program.vb:39):
// VelopackApp.Build().Run() may answer an install, update or uninstall hook
// named by its own first argument (only Velopack's installer passes one), or,
// inside an installed copy, apply a downloaded update, and end the process,
// so nothing may run before it. Velopack cannot run in a test, so this is
// the one start-up check made on the source text: Program.cs, copied to the
// test output as text (Source\), the file that declares the app's entry point.
[TestClass]
public sealed partial class ProgramSourceTests
{
    private const string VelopackFirst = "VelopackApp.Build().Run();";

    [TestMethod]
    public void MainCallsVelopackFirst()
    {
        MethodInfo? entry = typeof(App).Assembly.EntryPoint;
        Assert.IsNotNull(entry, "PaperRoute.Desktop has no entry point.");
        Assert.AreEqual("Program.Main", $"{entry.DeclaringType?.Name}.{entry.Name}", "The entry point is not Program.Main, so this check reads the wrong method.");

        string path = Path.Combine(AppContext.BaseDirectory, "Source", "Program.cs");
        Assert.IsTrue(File.Exists(path), $"{path} was not copied to the test output.");

        string source = File.ReadAllText(path);
        string first = FirstStatementOfMain(source);
        Assert.AreEqual(VelopackFirst, first,
            $"Main's first statement must be {VelopackFirst} Velopack may have to answer an install, update or uninstall hook, " +
            "or apply an update, and end the process before anything else runs (start-up design 2.4, step 0). " +
            $"The first statement is: {first}");

        // Code that runs before Main's first statement: Program's static
        // constructor, or a module initializer anywhere in the app.
        Assert.IsFalse(StaticConstructor().IsMatch(source),
            "Program.cs declares a static constructor, which runs before Velopack: move its work after VelopackApp.Build().Run().");
        string[] initializers = [.. Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "Source"), "*.cs", SearchOption.AllDirectories)
            .Where(file => File.ReadAllText(file).Contains("ModuleInitializer", StringComparison.Ordinal))
            .Select(file => Path.GetFileName(file))];
        Assert.IsEmpty(initializers,
            $"A module initializer runs before Velopack: {string.Join(", ", initializers)}.");

        // The design's second clause, that nothing stands between this call
        // and "return StartupSequence.Run(...)", waits for I3, which brings
        // that call; until then the trial's start-up follows it.
    }

    // Main's first statement, with its white space taken out, up to and
    // including its semicolon. Comments before it are skipped; anything else,
    // a preprocessor line included, counts as a statement.
    private static string FirstStatementOfMain(string source)
    {
        Match main = MainDeclaration().Match(source);
        Assert.IsTrue(main.Success, "Program.cs declares no static Main(string[] args).");

        int at = source.IndexOf('{', main.Index + main.Length);
        Assert.IsGreaterThanOrEqualTo(0, at, "Main has no body.");
        at++;
        while (at < source.Length)
        {
            if (char.IsWhiteSpace(source[at]))
            {
                at++;
            }
            else if (string.CompareOrdinal(source, at, "//", 0, 2) == 0)
            {
                int end = source.IndexOf('\n', at);
                at = end < 0 ? source.Length : end + 1;
            }
            else if (string.CompareOrdinal(source, at, "/*", 0, 2) == 0)
            {
                int end = source.IndexOf("*/", at + 2, StringComparison.Ordinal);
                at = end < 0 ? source.Length : end + 2;
            }
            else
            {
                break;
            }
        }

        int stop = source.IndexOfAny([';', '{', '}'], at);
        Assert.IsGreaterThanOrEqualTo(0, stop, "Main's body has no statement.");
        string statement = source[at..(stop + 1)];
        return string.Concat(statement.Where(c => !char.IsWhiteSpace(c)));
    }

    [GeneratedRegex(@"\bstatic\s+(?:int|void|async\s+Task(?:<int>)?)\s+Main\s*\(\s*string\s*\[\s*\]\s*\w+\s*\)")]
    private static partial Regex MainDeclaration();

    [GeneratedRegex(@"\bstatic\s+Program\s*\(\s*\)")]
    private static partial Regex StaticConstructor();
}
