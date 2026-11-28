using System.Reflection;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.Threading;
using PaperRoute.Desktop.Styles;

namespace PaperRoute.Desktop.Tests;

// The limits of headless, written beside the checks in HeadlessLimits.md
// (copied to the test output), and the tests that prove its rows (the
// harness design, part 2.7). A row lands with its proof; the note's own test
// fails when a named proof is no longer a test method, when a row names no
// test and is not a statement, when the plan's three limits are missing, or
// when a rule's code file lacks its pointer line.
[TestClass]
public sealed partial class HeadlessLimitTests
{
    // The line every rule's code file carries, pointing a reader here.
    private const string PointerLine = "// Limits of headless: HeadlessLimits.md";

    // How a "Proved by" cell that names no test begins.
    private const string StatementMark = "A statement";

    // The plan's three limits, in the words of build issue 3's acceptance line
    // (Avalonia-Migration-Plan.md:394), looked for without regard to case.
    private static readonly string[] s_planLimits =
        ["no UI Automation or AT-SPI tree", "render scale 1 unless set", "no native dialogs"];

    public TestContext TestContext { get; set; } = null!;

    // Limit 2: a shown window draws at render scale 1, one pixel per unit,
    // and draws at twice the size once a test sets a scale of 2.
    [TestMethod]
    public void HeadlessRendersAtScaleOneUnlessSet()
    {
        (double scaling, PixelSize frame, double setScaling, PixelSize setFrame) = Headless.Run(() =>
        {
            var window = new Window
            {
                Width = 200,
                Height = 100,
                Content = new Border { Margin = new Thickness(20), Background = Brushes.Teal },
            };
            window.Show();
            try
            {
                double before = window.RenderScaling;
                PixelSize atOne = FrameSize(window);
                window.SetRenderScaling(2);
                return (before, atOne, window.RenderScaling, FrameSize(window));
            }
            finally
            {
                window.Close();
            }
        });

        TestContext.WriteLine($"Unset: render scaling {scaling}, frame {frame}. After SetRenderScaling(2): render scaling {setScaling}, frame {setFrame}.");
        Assert.AreEqual(1.0, scaling, "A headless window renders at scale 1 unless a test sets another.");
        Assert.AreEqual(new PixelSize(200, 100), frame, "At scale 1 a 200 x 100 window's frame is 200 x 100 pixels.");
        Assert.AreEqual(2.0, setScaling, "SetRenderScaling(2) gives the window a render scaling of 2.");
        Assert.AreEqual(new PixelSize(400, 200), setFrame, "After SetRenderScaling(2) the frame is twice the size.");
    }

    // Limit 4: the headless platform reports a Light system with no contrast
    // preference and raises no colour change, so the app's contrast follower
    // reads no system colours (SystemContrastColors.Read, GetSysColor on
    // Windows) and keeps the fallback set.
    [TestMethod]
    public void HeadlessReportsLightAndNoContrast()
    {
        (bool hasSettings, PlatformColorValues? values, ThemeVariant actual, bool high, bool fallback, int changes) = Headless.Run(() =>
        {
            Application app = Application.Current!;
            IPlatformSettings? settings = app.PlatformSettings;
            if (settings is null)
            {
                return (false, (PlatformColorValues?)null, app.ActualThemeVariant, false, false, 0);
            }

            int raised = 0;
            settings.ColorValuesChanged += (_, _) => raised++;
            PlatformColorValues colors = settings.GetColorValues();
            ContrastPreference preference = ContrastPreference.FromPlatform(settings);
            preference.Refresh();
            Dispatcher.UIThread.RunJobs();
            return (true, colors, app.ActualThemeVariant, preference.IsHigh, preference.Colors == SystemContrastColors.Fallback, raised);
        });

        Assert.IsTrue(hasSettings, "The headless platform offers no platform settings.");
        TestContext.WriteLine($"Theme variant {values!.ThemeVariant}, contrast preference {values.ContrastPreference}, " +
            $"accent colours {values.AccentColor1}, {values.AccentColor2}, {values.AccentColor3}; the app's variant {actual}; colour changes raised: {changes}.");
        Assert.AreEqual(PlatformThemeVariant.Light, values.ThemeVariant, "Headless reports a Light system.");
        Assert.AreEqual(ColorContrastPreference.NoPreference, values.ContrastPreference, "Headless reports no contrast preference.");
        Assert.AreEqual(ThemeVariant.Light, actual, "The app, following the system, shows Light.");
        Assert.IsFalse(high, "The contrast follower found a contrast theme on the headless platform.");
        Assert.IsTrue(fallback, "The contrast follower read system colours instead of keeping the fallback set.");
        Assert.AreEqual(0, changes, "The headless platform raised a colour change.");
    }

    // Limit 8: no system text size reaches headless, and Avalonia 12.1.6
    // offers no text-size setting to follow. The public types of every
    // Avalonia assembly the app loads (PaperRoute.Desktop's references,
    // followed through the Avalonia assemblies, so the Windows, X11 and
    // macOS back ends are among them), and of Avalonia.Headless, the
    // platform these tests run on, which the app itself does not load, hold
    // no type or member named like one. The test fails when an upgrade adds
    // one, so that the text-size statement (HeadlessLimits.md row 8, the
    // plan's section 9) is read again then. Its teeth: the same walk over
    // the real types with a word they must hold ("FontSize") names two
    // members it must find, so a scan that reads nothing fails; and the
    // scan over a type that has text-size members must name each of them.
    [TestMethod]
    public void AvaloniaOffersNoTextSizeSetting()
    {
        IReadOnlyList<Assembly> assemblies = [.. AvaloniaAssembliesTheAppLoads(), typeof(HeadlessWindowExtensions).Assembly];
        List<Type> types = assemblies.SelectMany(assembly => assembly.GetExportedTypes()).ToList();
        TestContext.WriteLine($"Scanned {types.Count} public types in {assemblies.Count} assemblies: " +
            string.Join(", ", assemblies.Select(assembly => assembly.GetName().Name + " " + assembly.GetName().Version)));
        List<string> names = assemblies.Select(assembly => assembly.GetName().Name!).ToList();
        foreach (string needed in new[] { "Avalonia.Base", "Avalonia.Controls", "Avalonia.Win32", "Avalonia.X11", "Avalonia.Native", "Avalonia.Headless" })
        {
            CollectionAssert.Contains(names, needed, $"The scan does not reach {needed}.");
        }

        // The scan reads the real types and their members: Avalonia 12.1.6
        // has about 1,600 public types in these assemblies, and the same walk
        // with the word "FontSize" finds the two members named here.
        Assert.IsGreaterThanOrEqualTo(1000, types.Count, $"The scan read only {types.Count} public types, so it cannot show an absence.");
        List<string> fontSize = MembersNamedLike(types, ["FontSize"]);
        foreach (string member in new[] { "Avalonia.Controls.TextBlock.FontSize", "Avalonia.Controls.Documents.TextElement.FontSizeProperty" })
        {
            CollectionAssert.Contains(fontSize, member, $"The same walk with the word \"FontSize\" does not find {member}, so it does not read the real members.");
        }

        List<string> found = TextSizeMembers(types);
        Assert.IsEmpty(found,
            "Avalonia now offers what looks like a text-size setting, so the text-size statement (HeadlessLimits.md row 8, the plan's section 9) must be read again: " +
            string.Join(", ", found));

        // The scan's teeth.
        string specimen = typeof(TextSizeSpecimen).FullName!;
        CollectionAssert.AreEqual(
            new[] { specimen + ".FontScale", specimen + ".TextScaleFactor", specimen + ".TextScalingChanged", specimen + ".TextSizeFactorFor" },
            TextSizeMembers([typeof(TextSizeSpecimen)]),
            "The scan must name every member of the specimen that is named like a text-size setting, and nothing else.");
    }

    // The note names the plan's three limits, every test it names as a proof
    // is a test method of this project, and every rule's code file points to
    // the note. Then the same check, on a note and a file spoiled one way
    // each, must find exactly that fault.
    [TestMethod]
    public void TheLimitsNoteNamesEveryLimitAndEveryRuleLinksIt()
    {
        string note = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "HeadlessLimits.md"));
        IReadOnlySet<string> tests = TestMethodNames();
        IReadOnlyList<(string Name, string Text)> files = RuleFiles();

        foreach (string proof in ProofNames(note))
        {
            TestContext.WriteLine($"Named proof: {proof}");
        }

        foreach ((string name, _) in files)
        {
            TestContext.WriteLine($"Rule file: {name}");
        }

        List<string> problems = NoteProblems(note, tests, files);
        Assert.IsEmpty(problems, "The limits of headless:" + Environment.NewLine + string.Join(Environment.NewLine, problems));

        // The check's teeth: one fault each, and only that fault reported.
        string noDialogs = note.Replace("no native dialogs", "no system dialogs", StringComparison.OrdinalIgnoreCase);
        Assert.AreEqual(
            "The note lacks the plan's limit \"no native dialogs\".",
            string.Join(Environment.NewLine, NoteProblems(noDialogs, tests, files)),
            "A note without the plan's third limit.");

        string renamed = note.Replace("`HeadlessReportsLightAndNoContrast`", "`HeadlessReportsLightAndNoContrastAtAll`", StringComparison.Ordinal);
        Assert.AreEqual(
            "Row 4 names HeadlessReportsLightAndNoContrastAtAll as its proof, which is not a test method that CI runs here.",
            string.Join(Environment.NewLine, NoteProblems(renamed, tests, files)),
            "A note naming a proof that does not exist.");

        string bare = note.Replace("`HeadlessReportsLightAndNoContrast`", "HeadlessReportsLightAndNoContrast", StringComparison.Ordinal);
        Assert.AreEqual(
            "Row 4 names no test and is not a statement.",
            string.Join(Environment.NewLine, NoteProblems(bare, tests, files)),
            "A note naming its proof without a code span.");

        string manual = note.Replace("`HeadlessRendersAtScaleOneUnlessSet`", $"`{nameof(BoardBudgetTests.WriteLargeLibraryForMeasurement)}`", StringComparison.Ordinal);
        Assert.AreEqual(
            $"Row 2 names {nameof(BoardBudgetTests.WriteLargeLibraryForMeasurement)} as its proof, which is not a test method that CI runs here.",
            string.Join(Environment.NewLine, NoteProblems(manual, tests, files)),
            "A note naming as its proof a test in the Manual category, which CI never runs.");

        List<(string Name, string Text)> unlinked = [.. files, ("Legibility/Unlinked.cs", "namespace PaperRoute.Desktop.Tests.Legibility;" + Environment.NewLine)];
        Assert.AreEqual(
            $"Legibility/Unlinked.cs lacks the line \"{PointerLine}\".",
            string.Join(Environment.NewLine, NoteProblems(note, tests, unlinked)),
            "A rule's file without the pointer line.");
    }

    private static List<string> NoteProblems(string note, IReadOnlySet<string> tests, IReadOnlyList<(string Name, string Text)> files)
    {
        var problems = new List<string>();
        foreach (string limit in s_planLimits)
        {
            if (!note.Contains(limit, StringComparison.OrdinalIgnoreCase))
            {
                problems.Add($"The note lacks the plan's limit \"{limit}\".");
            }
        }

        List<(string Row, string Proof)> proofs = ProofCells(note);
        if (proofs.Count == 0)
        {
            problems.Add("The note has no table rows with a \"Proved by\" column.");
        }

        foreach ((string row, string proof) in proofs)
        {
            List<string> names = TestShapedNames(proof).ToList();
            if (names.Count == 0 && !proof.StartsWith(StatementMark, StringComparison.Ordinal))
            {
                problems.Add($"Row {row} names no test and is not a statement.");
            }

            foreach (string name in names.Where(name => !tests.Contains(name)))
            {
                problems.Add($"Row {row} names {name} as its proof, which is not a test method that CI runs here.");
            }
        }

        foreach ((string name, string text) in files)
        {
            if (!text.Split('\n').Any(line => line.Trim() == PointerLine))
            {
                problems.Add($"{name} lacks the line \"{PointerLine}\".");
            }
        }

        return problems;
    }

    // Each table row's number and its "Proved by" cell.
    private static List<(string Row, string Proof)> ProofCells(string note)
    {
        var cells = new List<(string, string)>();
        int column = -1;
        foreach (string line in note.Split('\n').Select(line => line.Trim()))
        {
            if (!line.StartsWith('|'))
            {
                column = -1;
                continue;
            }

            string[] parts = line.Trim('|').Split('|').Select(part => part.Trim()).ToArray();
            int header = Array.FindIndex(parts, part => part.Equals("Proved by", StringComparison.OrdinalIgnoreCase));
            if (header >= 0)
            {
                column = header;
            }
            else if (column >= 0 && column < parts.Length && !parts[0].StartsWith("---", StringComparison.Ordinal))
            {
                cells.Add((parts[0], parts[column]));
            }
        }

        return cells;
    }

    private static IEnumerable<string> ProofNames(string note) =>
        ProofCells(note).SelectMany(cell => TestShapedNames(cell.Proof));

    // Code spans that look like a test method's name: one identifier, no dot,
    // slash or space (so `KeyboardAccessTests.vb` and `--measure` are not).
    // A name written bare is not read here; it is caught because a cell must
    // name a test in a code span or start with StatementMark.
    private static IEnumerable<string> TestShapedNames(string cell) =>
        CodeSpan().Matches(cell).Select(match => match.Groups[1].Value)
            .Where(span => TestShape().IsMatch(span));

    // The test methods CI runs: a method or class marked [Ignore], or in the
    // Manual category that build.yml's filter leaves out, proves nothing.
    private static IReadOnlySet<string> TestMethodNames() =>
        typeof(HeadlessLimitTests).Assembly.GetTypes()
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Where(method => method.GetCustomAttributes<TestMethodAttribute>(inherit: true).Any())
            .Where(method => !method.IsDefined(typeof(IgnoreAttribute), inherit: true)
                && !method.DeclaringType!.IsDefined(typeof(IgnoreAttribute), inherit: true))
            .Where(method => !method.GetCustomAttributes<TestCategoryBaseAttribute>(inherit: true)
                .Concat(method.DeclaringType!.GetCustomAttributes<TestCategoryBaseAttribute>(inherit: true))
                .SelectMany(category => category.TestCategories)
                .Contains(BoardBudgetTests.ManualCategory, StringComparer.Ordinal))
            .Select(method => method.Name)
            .ToHashSet(StringComparer.Ordinal);

    // Every .cs file under Legibility/ and Parity/ (once it exists), and
    // AccessibilityTreeTests.cs, read from the project's own folder above
    // the test output.
    private static IReadOnlyList<(string Name, string Text)> RuleFiles()
    {
        string project = ProjectFolder();
        var names = new List<string>();
        string legibility = Path.Combine(project, "Legibility");
        Assert.IsTrue(Directory.Exists(legibility), $"No Legibility folder in {project}.");
        foreach (string folder in new[] { legibility, Path.Combine(project, "Parity") }.Where(Directory.Exists))
        {
            names.AddRange(Directory.GetFiles(folder, "*.cs", SearchOption.AllDirectories));
        }

        string tree = Path.Combine(project, "AccessibilityTreeTests.cs");
        Assert.IsTrue(File.Exists(tree), $"No AccessibilityTreeTests.cs in {project}.");
        names.Add(tree);
        return names
            .Select(path => (Path.GetRelativePath(project, path).Replace('\\', '/'), File.ReadAllText(path)))
            .OrderBy(file => file.Item1, StringComparer.Ordinal)
            .ToList();
    }

    private static string ProjectFolder()
    {
        DirectoryInfo? folder = new(AppContext.BaseDirectory);
        while (folder is not null && !File.Exists(Path.Combine(folder.FullName, "PaperRoute.Desktop.Tests.csproj")))
        {
            folder = folder.Parent;
        }

        Assert.IsNotNull(folder, $"No PaperRoute.Desktop.Tests.csproj above {AppContext.BaseDirectory}.");
        return folder.FullName;
    }

    // The Avalonia assemblies PaperRoute.Desktop references, and the
    // Avalonia assemblies they reference in turn, sorted by name.
    private static IReadOnlyList<Assembly> AvaloniaAssembliesTheAppLoads()
    {
        var found = new SortedDictionary<string, Assembly>(StringComparer.Ordinal);
        var waiting = new Queue<Assembly>([typeof(App).Assembly]);
        while (waiting.Count > 0)
        {
            foreach (AssemblyName reference in waiting.Dequeue().GetReferencedAssemblies())
            {
                string name = reference.Name ?? string.Empty;
                bool avalonia = name == "Avalonia" || name.StartsWith("Avalonia.", StringComparison.Ordinal);
                if (avalonia && !found.ContainsKey(name))
                {
                    Assembly loaded = Assembly.Load(reference);
                    found[name] = loaded;
                    waiting.Enqueue(loaded);
                }
            }
        }

        return [.. found.Values];
    }

    // Every type and member whose name holds one of the words of a
    // text-size setting, as "Type.Member", sorted.
    private static List<string> TextSizeMembers(IEnumerable<Type> types) =>
        MembersNamedLike(types, ["TextScal", "FontScal", "TextSizeFactor"]);

    // Every type and member (accessors left out, as their property or event
    // is named) whose name holds one of the words, ignoring case, as
    // "Type.Member", sorted.
    private static List<string> MembersNamedLike(IEnumerable<Type> types, IReadOnlyList<string> words)
    {
        bool Named(string name) => words.Any(word => name.Contains(word, StringComparison.OrdinalIgnoreCase));
        var found = new List<string>();
        foreach (Type type in types)
        {
            if (Named(type.Name))
            {
                found.Add(type.FullName ?? type.Name);
            }

            foreach (MemberInfo member in type.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                if (member is not MethodBase { IsSpecialName: true } && Named(member.Name))
                {
                    found.Add((type.FullName ?? type.Name) + "." + member.Name);
                }
            }
        }

        found.Sort(StringComparer.Ordinal);
        return found;
    }

    // The scan's bad input: what a text-size setting in a later Avalonia
    // might look like, beside a member that is not one.
    private sealed class TextSizeSpecimen
    {
        public event EventHandler? TextScalingChanged;

        public static double FontScale => 1;

        public double TextScaleFactor { get; set; } = 1;

        public double Width { get; set; }

        public double TextSizeFactorFor(double size) => size * TextScaleFactor;

        public void Raise() => TextScalingChanged?.Invoke(this, EventArgs.Empty);
    }

    private static PixelSize FrameSize(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        using WriteableBitmap? frame = window.CaptureRenderedFrame();
        Assert.IsNotNull(frame, "The headless platform captured no frame.");
        return frame.PixelSize;
    }

    [GeneratedRegex("`([^`]+)`")]
    private static partial Regex CodeSpan();

    [GeneratedRegex("^[A-Za-z][A-Za-z0-9_]*$")]
    private static partial Regex TestShape();
}
