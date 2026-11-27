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
// Limits of headless: HeadlessLimits.md
[TestClass]
public sealed class R2Tests
{
    public TestContext TestContext { get; set; } = null!;

    // Check 1: every token, derived brush and bridge key has a system role,
    // and a shown window resolves each, in both contrast variants, to its
    // role's colour (a derived brush to the colour derived from its role's),
    // with each set entered from a Light base and from a Dark base. Extends
    // the trial's EveryTokenTakesItsSystemColourUnderAContrastTheme.
    [TestMethod]
    public void R2_EveryTokenDerivedBrushAndBridgeKeyTakesItsRole()
    {
        string[] tokens = TokenFile.ThemedKeys()["Light"].Where(key => key != "OutlineThickness").ToArray();
        IReadOnlyDictionary<string, SystemColorRole> roles = Rules.R2Roles();
        List<string> roleFindings = Rules.R2RoleFindings(tokens.Concat(ThemeTokens.DerivedKeys).Concat(Rules.R2BridgeRoles.Keys), roles);
        Assert.IsEmpty(roleFindings, "R2, check 1:" + Environment.NewLine + string.Join(Environment.NewLine, roleFindings));

        var problems = new List<string>();
        int resolved = 0;
        foreach (ThemeCase themeCase in ThemeCase.ContrastCases)
        {
            SystemContrastColors colors = themeCase.Contrast!.Value;
            IReadOnlyDictionary<string, Color> derived = ContrastTheme.Colors(colors);
            foreach (ThemeVariant contrastBase in ThemeCase.ContrastBases)
            {
                string where = $"{themeCase} from {contrastBase}";
                (List<string> found, int count) = Headless.Run(() =>
                {
                    var local = new List<string>();
                    int seen = 0;
                    themeCase.Apply(Application.Current!, contrastBase);
                    var window = new Window { Width = 100, Height = 100 };
                    window.Show();
                    try
                    {
                        ThemeVariant expectedVariant = ContrastTheme.VariantFor(colors);
                        if (window.ActualThemeVariant != expectedVariant)
                        {
                            local.Add($"{where}: the window's variant is {window.ActualThemeVariant}, not {expectedVariant}");
                        }

                        foreach (string key in tokens.Concat(ThemeTokens.DerivedKeys).Concat(Rules.R2BridgeRoles.Keys))
                        {
                            Color expected = ContrastTheme.TokenRoles.TryGetValue(key, out SystemColorRole role)
                                ? ContrastTheme.ColorOf(colors, role)
                                : derived[key];
                            seen++;
                            if (!window.TryFindResource(key, window.ActualThemeVariant, out object? value) || value is not ISolidColorBrush brush)
                            {
                                local.Add($"{where}: {key} does not resolve to a solid brush");
                            }
                            else if (brush.Color != expected)
                            {
                                local.Add($"{where}: {key} is {brush.Color}, not {expected} ({roles[key]})");
                            }
                        }
                    }
                    finally
                    {
                        window.Close();
                    }

                    return (local, seen);
                });
                problems.AddRange(found);
                resolved += count;
            }
        }

        TestContext.WriteLine($"{tokens.Length} tokens, {ThemeTokens.DerivedKeys.Count} derived brushes and {Rules.R2BridgeRoles.Count} bridge keys; {resolved} resolutions in {ThemeCase.ContrastCases.Count} sets from {ThemeCase.ContrastBases.Count} bases.");
        Assert.IsEmpty(problems, "R2, check 1:" + Environment.NewLine + string.Join(Environment.NewLine, problems));
    }

    // Check 2: every R1 pair, mapped through the roles, is one of the pairs a
    // contrast theme is built from.
    [TestMethod]
    public void R2_EveryPairIsADesignedPairUnderContrast()
    {
        List<(string Where, string Ink, string Surface)> pairs = TokenTests.AllPairs().ToList();
        List<string> findings = Rules.R2DesignedPairFindings(pairs, Rules.R2Roles());
        TestContext.WriteLine($"{pairs.Count} pairs mapped through the roles.");
        Assert.IsGreaterThan(40, pairs.Count, "Too few pairs were mapped.");
        Assert.IsEmpty(findings, "R2, check 2:" + Environment.NewLine + string.Join(Environment.NewLine, findings));
    }

    // Check 3: every R1 pair at 4.5:1 in each of Windows' four contrast sets
    // and the off-Windows fallback set. Every ratio is written beside the
    // pictures (legibility-r2-<set>.txt).
    [TestMethod]
    public void R2_EveryPairHasItsContrastInEveryWindowsContrastSet()
    {
        List<(string Where, string Ink, string Surface)> pairs = TokenTests.AllPairs().ToList();
        var findings = new List<string>();
        IEnumerable<(string Name, SystemContrastColors Colors)> sets = ThemeCase.ContrastCases
            .Select(themeCase => (themeCase.Name, themeCase.Contrast!.Value))
            .Append(("fallback", SystemContrastColors.Fallback));
        foreach ((string name, SystemContrastColors colors) in sets)
        {
            var measured = new List<string>();
            findings.AddRange(Rules.R2ContrastFindings(name, pairs, ContrastTheme.Colors(colors), measured));
            MeasuredValues.Write("r2", name, measured, TestContext);
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
            string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Resources", "Ease of Access Themes");
            Assert.IsTrue(Directory.Exists(folder), $"Windows has no contrast theme folder at {folder}: R2's contrast sets cannot be compared with Windows' own.");
            var findings = new List<string>();
            foreach (ThemeCase themeCase in ThemeCase.ContrastCases)
            {
                string path = Path.Combine(folder, themeCase.ThemeFile);
                if (!File.Exists(path))
                {
                    findings.Add($"{themeCase.ThemeFile} is missing from {folder}");
                    continue;
                }

                findings.AddRange(Rules.R2ThemeFileFindings(themeCase.ThemeFile, File.ReadAllText(path), themeCase.Contrast!.Value));
                TestContext.WriteLine($"Read {path}.");
            }

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
        List<(string Where, string Ink, string Surface)> pairs = TokenTests.AllPairs().ToList();

        // Check 2: the primary button's text, HighlightText, on WindowText.
        Dictionary<string, SystemColorRole> accentOnText = Rules.R2Roles().ToDictionary(StringComparer.Ordinal);
        accentOnText["AccentBrush"] = SystemColorRole.WindowText;
        Assert.AreEqual(
            "a primary button: OnAccentTextBrush on AccentBrush is HighlightText on WindowText, not a designed pair",
            string.Join(Environment.NewLine, Rules.R2DesignedPairFindings(pairs, accentOnText)),
            "AccentBrush on WindowText while OnAccentTextBrush stays HighlightText.");

        // Check 3: hc1 with its inactive text in the window colour.
        SystemContrastColors grey = SystemContrastColors.Hc1 with { GrayText = SystemContrastColors.Hc1.Window };
        List<string> greyFindings = Rules.R2ContrastFindings("hc1, GrayText as Window", pairs, ContrastTheme.Colors(grey), new List<string>());
        TestContext.WriteLine(string.Join(Environment.NewLine, greyFindings));
        Assert.HasCount(pairs.Count(pair => pair.Ink == "MutedTextBrush"), greyFindings, "Every muted pair, and only those, fails.");
        Assert.IsTrue(greyFindings.All(finding => finding.Contains(": MutedTextBrush on ", StringComparison.Ordinal) && finding.EndsWith(" is 1.00:1", StringComparison.Ordinal)),
            "A muted pair is reported at 1.00:1:" + Environment.NewLine + string.Join(Environment.NewLine, greyFindings));
        Assert.IsEmpty(Rules.R2ContrastFindings("hc1", pairs, ContrastTheme.Colors(SystemContrastColors.Hc1), new List<string>()), "Real hc1 passes.");

        // Check 1: a token the role table does not know.
        string[] keys = [.. TokenFile.ThemedKeys()["Light"].Where(key => key != "OutlineThickness"), .. ThemeTokens.DerivedKeys, "UnmappedTestBrush"];
        Assert.AreEqual(
            "UnmappedTestBrush has no system role",
            string.Join(Environment.NewLine, Rules.R2RoleFindings(keys, Rules.R2Roles())),
            "A token with no role.");
    }
}
