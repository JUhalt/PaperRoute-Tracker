using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using PaperRoute.Desktop.Styles;
using PaperRoute.Desktop.Tests.Styles;

namespace PaperRoute.Desktop.Tests.Legibility;

// R2, contrast-theme tokens (harness design 2.4), in Windows' four real
// contrast sets (2.2). The rule's three checks are in Rules.R2.cs; these
// tests run them on the real token file, roles and pairs, and on bad inputs.
// A failure listed in KnownFailures.tsv does not fail these runs, as with R1
// (KnownFailuresAreCurrent re-runs them through CurrentFindings). Check 3
// writes every ratio it measures to legibility-r2-<set>.txt beside the
// pictures.
// Limits of headless: HeadlessLimits.md
[TestClass]
public sealed class R2Tests
{
    // The columns of legibility-r2-<set>.txt, as R1's file has them.
    private static readonly string[] s_ratioColumns = ["theme case", "pair", "ratio", "where"];

    public TestContext TestContext { get; set; } = null!;

    // The tokens check 1 judges: every themed token but the outline thickness,
    // which is no colour.
    private static string[] Tokens => TokenFile.ThemedKeys()["Light"].Where(key => key != "OutlineThickness").ToArray();

    // Check 3's sets: Windows' four contrast sets and the off-Windows fallback.
    private static IEnumerable<(string Name, SystemContrastColors Colors)> Sets =>
        ThemeCase.ContrastCases
            .Select(themeCase => (themeCase.Name, themeCase.Contrast!.Value))
            .Append(("fallback", SystemContrastColors.Fallback));

    // Check 1: every token, derived brush and bridge key has a system role,
    // and a shown window resolves each, in both contrast variants, to its
    // role's colour (a derived brush to the colour derived from its role's),
    // with each set entered from a Light base and from a Dark base. Extends
    // the trial's EveryTokenTakesItsSystemColourUnderAContrastTheme.
    [TestMethod]
    public void R2_EveryTokenDerivedBrushAndBridgeKeyTakesItsRole()
    {
        IReadOnlyList<KnownFailure> known = KnownFailures.Load();
        List<Finding> roleFindings = RoleTableFindings().Where(finding => !KnownFailures.Lists(known, finding)).ToList();
        Assert.IsEmpty(roleFindings, "R2, check 1:" + Environment.NewLine + string.Join(Environment.NewLine, roleFindings));

        (List<Finding> resolutionFindings, int resolved) = ResolutionFindings();
        List<Finding> problems = resolutionFindings.Where(finding => !KnownFailures.Lists(known, finding)).ToList();
        TestContext.WriteLine($"{Tokens.Length} tokens, {ThemeTokens.DerivedKeys.Count} derived brushes and {Rules.R2BridgeRoles.Count} bridge keys; {resolved} resolutions in {ThemeCase.ContrastCases.Count} sets from {ThemeCase.ContrastBases.Count} bases.");
        Assert.IsEmpty(problems, "R2, check 1:" + Environment.NewLine + string.Join(Environment.NewLine, problems));
    }

    // Check 2: every R1 pair, mapped through the roles, is one of the pairs a
    // contrast theme is built from.
    [TestMethod]
    public void R2_EveryPairIsADesignedPairUnderContrast()
    {
        IReadOnlyList<KnownFailure> known = KnownFailures.Load();
        List<Finding> findings = DesignedPairFindings().Where(finding => !KnownFailures.Lists(known, finding)).ToList();
        int pairs = TokenPairs.All.Count();
        TestContext.WriteLine($"{pairs} pairs mapped through the roles.");
        Assert.IsGreaterThan(40, pairs, "Too few pairs were mapped.");
        Assert.IsEmpty(findings, "R2, check 2:" + Environment.NewLine + string.Join(Environment.NewLine, findings));
    }

    // Check 3: every R1 pair at 4.5:1 in each of Windows' four contrast sets
    // and the off-Windows fallback set. Every ratio is written beside the
    // pictures (legibility-r2-<set>.txt), in the pairs' order.
    [TestMethod]
    public void R2_EveryPairHasItsContrastInEveryWindowsContrastSet()
    {
        IReadOnlyList<KnownFailure> known = KnownFailures.Load();
        var findings = new List<Finding>();
        foreach ((string name, SystemContrastColors colors) in Sets)
        {
            IReadOnlyList<PairRatio> ratios = Rules.R2Ratios(name, ContrastTheme.Colors(colors), TokenPairs.All);
            findings.AddRange(Rules.R2ContrastFindings(ratios).Where(finding => !KnownFailures.Lists(known, finding)));
            string path = Measurements.Write(
                Pictures.Directory(),
                "R2",
                name,
                s_ratioColumns,
                ratios.Select(measured => (IReadOnlyList<string>)
                    [measured.ThemeCase, $"{measured.Pair.Ink} on {measured.Pair.Surface}", Measurements.Number(measured.Ratio), measured.Pair.Where]));
            TestContext.WriteLine($"Measured values saved: {path}");
        }

        Assert.IsEmpty(findings, "R2, check 3, pairs under 4.5:1:" + Environment.NewLine + string.Join(Environment.NewLine, findings));
    }

    // The four contrast sets are Windows' own: on Windows, each set equals the
    // six colours of its theme file in Windows' "Ease of Access Themes"
    // folder, and a missing folder or file fails. Off Windows there is
    // nothing to read. Then the comparison, on a theme file spoiled one way,
    // must find exactly that fault, on every system.
    [TestMethod]
    public void R2_TheContrastSetsAreWindowsOwn()
    {
        string image = Environment.GetEnvironmentVariable("ImageVersion") is { Length: > 0 } version ? version : "none (not a CI runner)";
        TestContext.WriteLine($"OS: {RuntimeInformation.OSDescription}; runner image version: {image}.");

        if (OperatingSystem.IsWindows())
        {
            string folder = ThemeFolder();
            Assert.IsTrue(Directory.Exists(folder), $"Windows has no contrast theme folder at {folder}: R2's contrast sets cannot be compared with Windows' own.");
            IReadOnlyList<KnownFailure> known = KnownFailures.Load();
            List<Finding> findings = ThemeFileFindings(folder, TestContext.WriteLine).Where(finding => !KnownFailures.Lists(known, finding)).ToList();
            Assert.IsEmpty(findings, "The contrast sets differ from Windows' own:" + Environment.NewLine + string.Join(Environment.NewLine, findings));
        }
        else
        {
            TestContext.WriteLine("Not Windows: no theme file read, so nothing compared.");
        }

        // The comparison's teeth: hc1's colours in a theme file's form, with
        // a later section that reuses a key, then spoiled one way each.
        SystemContrastColors hc1 = SystemContrastColors.Hc1;
        string section = string.Join("\r\n",
            "[Theme]",
            "",
            "[Control Panel\\Colors]",
            "Window=45 50 54",
            "WindowText=255 255 255",
            "GrayText=166 166 166",
            "Hilight=161 191 222",
            "HilightText=33 45 59",
            "HotTrackingColor=112 235 222",
            "",
            "[Control Panel\\Cursors]",
            "GrayText=1 2 3");
        Assert.IsEmpty(Rules.R2ThemeFileFindings("hc1.theme", section, hc1), "The faithful copy of hc1's section.");
        Assert.AreEqual(
            "hc1.theme: GrayText is 160 160 160, but the app's GrayText is 166 166 166",
            string.Join(Environment.NewLine, Rules.R2ThemeFileFindings("hc1.theme", section.Replace("GrayText=166 166 166", "GrayText=160 160 160", StringComparison.Ordinal), hc1)),
            "A theme file whose GrayText differs.");
        Assert.AreEqual(
            "hc1.theme: no Hilight in [Control Panel\\Colors]",
            string.Join(Environment.NewLine, Rules.R2ThemeFileFindings("hc1.theme", section.Replace("Hilight=161 191 222", string.Empty, StringComparison.Ordinal), hc1)),
            "A theme file without its selection colour.");
    }

    // The rule's teeth: a role table that puts AccentBrush on WindowText while
    // OnAccentTextBrush stays HighlightText fails check 2; a set whose
    // GrayText equals its Window fails check 3; a token with no role fails
    // check 1. Each finds exactly its fault.
    [TestMethod]
    public void R2_FailsBadRoleTablesAndSets()
    {
        IReadOnlyList<TokenPair> pairs = TokenPairs.All.ToList();

        // Check 2: the primary button's text, HighlightText, on WindowText.
        Dictionary<string, SystemColorRole> accentOnText = Rules.R2Roles().ToDictionary(StringComparer.Ordinal);
        accentOnText["AccentBrush"] = SystemColorRole.WindowText;
        Assert.AreEqual(
            "a primary button: OnAccentTextBrush on AccentBrush is HighlightText on WindowText, not a designed pair",
            string.Join(Environment.NewLine, Rules.R2DesignedPairFindings(pairs, accentOnText)),
            "AccentBrush on WindowText while OnAccentTextBrush stays HighlightText.");

        // Check 3: hc1 with its inactive text in the window colour.
        SystemContrastColors grey = SystemContrastColors.Hc1 with { GrayText = SystemContrastColors.Hc1.Window };
        List<Finding> greyFindings = Rules.R2ContrastFindings("hc1, GrayText as Window", ContrastTheme.Colors(grey), pairs);
        TestContext.WriteLine(string.Join(Environment.NewLine, greyFindings));
        Assert.IsNotEmpty(greyFindings, "The spoiled set must fail some pair.");
        Assert.HasCount(pairs.Count(pair => pair.Ink == "MutedTextBrush"), greyFindings, "Every muted pair, and only those, fails.");
        Assert.IsTrue(greyFindings.All(finding => finding.Line.Contains(": MutedTextBrush on ", StringComparison.Ordinal) && finding.Line.EndsWith(" is 1.00:1", StringComparison.Ordinal)),
            "A muted pair is reported at 1.00:1:" + Environment.NewLine + string.Join(Environment.NewLine, greyFindings));
        Assert.IsEmpty(Rules.R2ContrastFindings("hc1", ContrastTheme.Colors(SystemContrastColors.Hc1), pairs), "Real hc1 passes.");

        // Check 1: a token the role table does not know.
        string[] keys = [.. Tokens, .. ThemeTokens.DerivedKeys, "UnmappedTestBrush"];
        Assert.AreEqual(
            "UnmappedTestBrush has no system role",
            string.Join(Environment.NewLine, Rules.R2RoleFindings(keys, Rules.R2Roles())),
            "A token with no role.");
    }

    // Check 1's resolution, given a key that is no token and no derived brush:
    // a bridge key, whose role only the bridge's table gives, is held to that
    // role's colour, and a key with no role anywhere is reported. Neither
    // throws. The window is a stand-in that resolves only the keys it is told.
    [TestMethod]
    public void R2_ReportsABridgeKeyWithNoTokenRole()
    {
        SystemContrastColors hc1 = SystemContrastColors.Hc1;
        Dictionary<string, SystemColorRole> roles = Rules.R2Roles().ToDictionary(StringComparer.Ordinal);
        roles["TestBridgeBrush"] = SystemColorRole.Window;
        string[] keys = ["PrimaryTextBrush", "TestBridgeBrush", "UnmappedTestBrush"];
        Color window = ContrastTheme.ColorOf(hc1, SystemColorRole.Window);
        Color text = ContrastTheme.ColorOf(hc1, SystemColorRole.WindowText);

        // The bridge key is not drawn: a window without the bridge.
        List<Finding> unbridged = Rules.R2ResolutionFindings("hc1", "hc1 from Light", keys, roles, hc1,
            key => key == "PrimaryTextBrush" ? (Color?)text : null);
        CollectionAssert.AreEqual(
            new[]
            {
                "hc1 from Light: TestBridgeBrush does not resolve to a solid brush",
                "hc1 from Light: UnmappedTestBrush has no system role, so no colour is expected of it",
            },
            unbridged.Select(finding => finding.Line).ToArray(),
            "A bridge key the window lacks, and a key with no role.");
        Assert.IsTrue(unbridged.All(finding => finding.Rule == "R2" && finding.ThemeCase == "hc1"), "Each finding names R2 and its set.");

        // The bridge key in its role's colour passes; in another colour it fails, naming its role.
        Assert.AreEqual(
            "hc1 from Light: UnmappedTestBrush has no system role, so no colour is expected of it",
            string.Join(Environment.NewLine, Rules.R2ResolutionFindings("hc1", "hc1 from Light", keys, roles, hc1,
                key => key == "TestBridgeBrush" ? (Color?)window : text)),
            "A bridge key in its role's colour.");
        Assert.AreEqual(
            $"hc1 from Light: TestBridgeBrush is {text}, not {window} (Window)",
            Rules.R2ResolutionFindings("hc1", "hc1 from Light", ["TestBridgeBrush"], roles, hc1, _ => (Color?)text).Single().Line,
            "A bridge key in the wrong colour.");
    }

    // What the R2 runs report today, listed failures included: the role
    // table, the window's resolution, the designed pairs, every set's ratios,
    // and, on Windows where the folder is, the theme files.
    internal static IReadOnlyList<Finding> CurrentFindings()
    {
        var findings = new List<Finding>(RoleTableFindings());
        findings.AddRange(ResolutionFindings().Findings);
        findings.AddRange(DesignedPairFindings());
        foreach ((string name, SystemContrastColors colors) in Sets)
        {
            findings.AddRange(Rules.R2ContrastFindings(name, ContrastTheme.Colors(colors), TokenPairs.All));
        }

        if (OperatingSystem.IsWindows() && Directory.Exists(ThemeFolder()))
        {
            findings.AddRange(ThemeFileFindings(ThemeFolder(), _ => { }));
        }

        return findings;
    }

    private static List<Finding> RoleTableFindings() =>
        Rules.R2RoleFindings(Tokens.Concat(ThemeTokens.DerivedKeys).Concat(Rules.R2BridgeRoles.Keys), Rules.R2Roles());

    private static List<Finding> DesignedPairFindings() => Rules.R2DesignedPairFindings(TokenPairs.All, Rules.R2Roles());

    // Check 1 in a shown window, in every contrast set from both bases, and
    // the number of keys resolved.
    private static (List<Finding> Findings, int Resolved) ResolutionFindings()
    {
        string[] keys = [.. Tokens, .. ThemeTokens.DerivedKeys, .. Rules.R2BridgeRoles.Keys];
        IReadOnlyDictionary<string, SystemColorRole> roles = Rules.R2Roles();
        var findings = new List<Finding>();
        int resolved = 0;
        foreach (ThemeCase themeCase in ThemeCase.ContrastCases)
        {
            SystemContrastColors colors = themeCase.Contrast!.Value;
            foreach (ThemeVariant contrastBase in ThemeCase.ContrastBases)
            {
                string where = $"{themeCase} from {contrastBase}";
                findings.AddRange(Headless.Run(() =>
                {
                    var local = new List<Finding>();
                    themeCase.Apply(Application.Current!, contrastBase);
                    var window = new Window { Width = 100, Height = 100 };
                    window.Show();
                    try
                    {
                        ThemeVariant expectedVariant = ContrastTheme.VariantFor(colors);
                        if (window.ActualThemeVariant != expectedVariant)
                        {
                            local.Add(new Finding("R2", themeCase.Name, "the window's variant", $"{where}: the window's variant is {window.ActualThemeVariant}, not {expectedVariant}"));
                        }

                        local.AddRange(Rules.R2ResolutionFindings(themeCase.Name, where, keys, roles, colors, key =>
                            window.TryFindResource(key, window.ActualThemeVariant, out object? value) && value is ISolidColorBrush brush ? (Color?)brush.Color : null));
                    }
                    finally
                    {
                        window.Close();
                    }

                    return local;
                }));
                resolved += keys.Length;
            }
        }

        return (findings, resolved);
    }

    // Windows' folder of contrast theme files.
    private static string ThemeFolder() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Resources", "Ease of Access Themes");

    // Each contrast set against its theme file in the folder; a missing file is a finding.
    private static List<Finding> ThemeFileFindings(string folder, Action<string> log)
    {
        var findings = new List<Finding>();
        foreach (ThemeCase themeCase in ThemeCase.ContrastCases)
        {
            string path = Path.Combine(folder, themeCase.ThemeFile);
            if (!File.Exists(path))
            {
                findings.Add(new Finding("R2", themeCase.Name, themeCase.ThemeFile, $"{themeCase.ThemeFile} is missing from {folder}"));
                continue;
            }

            findings.AddRange(Rules.R2ThemeFileFindings(themeCase.ThemeFile, File.ReadAllText(path), themeCase.Contrast!.Value));
            log($"Read {path}.");
        }

        return findings;
    }
}
