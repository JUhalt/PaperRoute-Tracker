namespace PaperRoute.Desktop.Tests.Styles;

// NoViewOrStyleNamesALiteral and its teeth (harness design 3.1, pull request
// S2). Every colour, font family, font size, corner radius and duration the
// views and styles use is a token of Tokens.axaml; LiteralScan says what it
// looks for, what it leaves to the styles, and which files it exempts and why.
[TestClass]
public sealed class LiteralScanTests
{
    public TestContext TestContext { get; set; } = null!;

    // No .axaml or .cs file of PaperRoute.Desktop, outside Tokens.axaml and
    // the contrast code, writes a colour, a font family, a font size, a
    // non-zero corner radius, a duration or a numeric font weight of its own.
    // A literal fails here by file and line. The scan must also have read the
    // app's files and the token file's own values, and every exempt file must
    // still exist, so an exemption cannot outlive its file.
    [TestMethod]
    public void NoViewOrStyleNamesALiteral()
    {
        IReadOnlyList<(string Path, string Text)> files = ThemeOwnership.AppFiles();
        (List<string> findings, Dictionary<string, int> exempt) = LiteralScan.Scan(files);
        TestContext.WriteLine($"Scanned {files.Count} files. Exempt: " +
            string.Join("; ", LiteralScan.ExemptFiles.Select(file => $"{file.Key} ({(exempt.TryGetValue(file.Key, out int count) ? count : 0)} literals), {file.Value}")));

        Assert.IsGreaterThanOrEqualTo(10, files.Count(file => file.Path.EndsWith(".axaml", StringComparison.Ordinal)), "The scan read too few .axaml files.");
        Assert.IsTrue(files.Any(file => file.Path == "App.axaml.cs"), "The scan did not read the app's .cs files.");
        foreach (string path in LiteralScan.ExemptFiles.Keys)
        {
            Assert.IsTrue(files.Any(file => file.Path == path), $"The exemption names {path}, which the app no longer has.");
        }

        Assert.IsGreaterThan(100, exempt["Styles/Tokens.axaml"], "The scan should see the token file's own colours, about two for each themed brush.");
        Assert.IsEmpty(findings, "Views or styles name a literal; make it a token of Tokens.axaml:" + Environment.NewLine + string.Join(Environment.NewLine, findings));
    }

    // The scan's teeth. A copy of a real style file with a literal put back
    // fails, by file, line and kind: Foreground="#BBBBBB" and CornerRadius="5"
    // in Rail.axaml (the design's two), and Button.axaml's hover duration as
    // it was before S2. Across those copies and a synthetic style file and
    // code file, every kind is found as an attribute, as a Setter and in code;
    // a font size and a corner radius also as a property element and in a
    // Setter.Value. The same words
    // in a comment, a radius of 0, a named weight, a token reference (as an
    // attribute or as a <DynamicResource /> element) and a constant's name
    // are not found.
    [TestMethod]
    public void TheLiteralScanFindsALiteralPutBack()
    {
        IReadOnlyList<(string Path, string Text)> real = ThemeOwnership.AppFiles();
        string rail = real.Single(file => file.Path == "Styles/Rail.axaml").Text;
        string button = real.Single(file => file.Path == "Styles/Button.axaml").Text;

        AssertFindsOne("Styles/Rail.axaml", rail, "<ContentPresenter Name=\"PART_ContentPresenter\"", "<ContentPresenter Name=\"PART_ContentPresenter\" Foreground=\"#BBBBBB\"", "names a colour");
        AssertFindsOne("Styles/Rail.axaml", rail, "CornerRadius=\"{DynamicResource FocusRingRadius}\"", "CornerRadius=\"5\"", "names a corner radius");
        AssertFindsOne("Styles/Button.axaml", button, "Duration=\"{DynamicResource HoverDuration}\"", "Duration=\"0:0:0.1\"", "names a duration");

        (string Path, string Text)[] files =
        [
            ("Styles/Teeth.axaml", "<Styles xmlns=\"https://github.com/avaloniaui\">\r\n  <!-- <Setter Property=\"FontSize\" Value=\"12\" /> and #BBBBBB in a comment -->\r\n  <Style Selector=\"TextBlock.teeth\">\r\n    <Setter Property=\"FontSize\" Value=\"12\" />\r\n    <Setter Property=\"FontWeight\" Value=\"700\" />\r\n    <Setter Property=\"FontWeight\" Value=\"Bold\" />\r\n    <Setter Property=\"FontFamily\" Value=\"Segoe UI\" />\r\n    <Setter Property=\"CornerRadius\" Value=\"0\" />\r\n    <Setter Property=\"CornerRadius\" Value=\"{DynamicResource ControlRadius}\" />\r\n    <Setter Value=\"#AABBCCDD\" Property=\"Foreground\" />\r\n  </Style>\r\n  <Style Selector=\"Button /template/ ContentPresenter#PART_ContentPresenter\">\r\n    <Setter Property=\"Transitions\"><Transitions><BrushTransition Property=\"Background\" Duration=\"0:0:0.2\" /></Transitions></Setter>\r\n    <Setter Property=\"TextElement.FontSize\" Value=\"{DynamicResource BodyFontSize}\" />\r\n  </Style>\r\n  <TextBlock TextElement.FontSize=\"14\" Text=\"&#x22EF;\" />\r\n  <Border><Border.CornerRadius>5</Border.CornerRadius></Border>\r\n  <Style Selector=\"TextBlock.element\">\r\n    <Setter Property=\"FontSize\"><Setter.Value>12</Setter.Value></Setter>\r\n    <Setter Property=\"CornerRadius\">\r\n      <Setter.Value>\r\n        <CornerRadius>4</CornerRadius>\r\n      </Setter.Value>\r\n    </Setter>\r\n    <Setter Property=\"FontFamily\"><Setter.Value><DynamicResource ResourceKey=\"UiFontFamily\" /></Setter.Value></Setter>\r\n  </Style>\r\n  <TextBlock FontFamily=\"Segoe UI\" FontWeight=\"600\" />\r\n  <Style Selector=\"Border.teeth\">\r\n    <Setter Property=\"CornerRadius\" Value=\"4\" />\r\n    <Setter Property=\"Duration\" Value=\"0:0:0.2\" />\r\n  </Style>\r\n</Styles>\r\n"),
            ("Shell/Teeth.cs", "// FontSize = 12 and \"#BBBBBB\" in a comment.\r\nvar title = new TextBlock { FontSize = 12, FontWeight = (FontWeight)700, FontFamily = new FontFamily(\"Segoe UI\") };\r\nvar frame = new Border { CornerRadius = new CornerRadius(0), Background = Brush.Parse(\"#BBBBBB\") };\r\nvar ring = new Border { CornerRadius = new CornerRadius(4) };\r\nvar ease = new BrushTransition { Duration = TimeSpan.FromMilliseconds(100) };\r\ndouble size = PlatformCheckWindow.BodyLineFontSize; bool big = title.FontSize == 12;\r\n"),
            ("Styles/Tokens.axaml", "<SolidColorBrush x:Key=\"CardBorderBrush\" Color=\"#3D444F\" />\r\n"),
        ];

        (List<string> findings, Dictionary<string, int> exempt) = LiteralScan.Scan(files);

        CollectionAssert.AreEqual(
            new[]
            {
                "Styles/Teeth.axaml:4: names a font size: <Setter Property=\"FontSize\" Value=\"12\" />",
                "Styles/Teeth.axaml:5: names a numeric font weight: <Setter Property=\"FontWeight\" Value=\"700\" />",
                "Styles/Teeth.axaml:7: names a font family: <Setter Property=\"FontFamily\" Value=\"Segoe UI\" />",
                "Styles/Teeth.axaml:10: names a colour: <Setter Value=\"#AABBCCDD\" Property=\"Foreground\" />",
                "Styles/Teeth.axaml:13: names a duration: <Setter Property=\"Transitions\"><Transitions><BrushTransition Property=\"Background\" Duration=\"0:0:0.2\" /></Transitions></Setter>",
                "Styles/Teeth.axaml:16: names a font size: <TextBlock TextElement.FontSize=\"14\" Text=\"&#x22EF;\" />",
                "Styles/Teeth.axaml:17: names a corner radius: <Border><Border.CornerRadius>5</Border.CornerRadius></Border>",
                "Styles/Teeth.axaml:19: names a font size: <Setter Property=\"FontSize\"><Setter.Value>12</Setter.Value></Setter>",
                "Styles/Teeth.axaml:22: names a corner radius: <CornerRadius>4</CornerRadius>",
                "Styles/Teeth.axaml:27: names a font family: <TextBlock FontFamily=\"Segoe UI\" FontWeight=\"600\" />",
                "Styles/Teeth.axaml:27: names a numeric font weight: <TextBlock FontFamily=\"Segoe UI\" FontWeight=\"600\" />",
                "Styles/Teeth.axaml:29: names a corner radius: <Setter Property=\"CornerRadius\" Value=\"4\" />",
                "Styles/Teeth.axaml:30: names a duration: <Setter Property=\"Duration\" Value=\"0:0:0.2\" />",
                "Shell/Teeth.cs:2: names a font size: var title = new TextBlock { FontSize = 12, FontWeight = (FontWeight)700, FontFamily = new FontFamily(\"Segoe UI\") };",
                "Shell/Teeth.cs:2: names a numeric font weight: var title = new TextBlock { FontSize = 12, FontWeight = (FontWeight)700, FontFamily = new FontFamily(\"Segoe UI\") };",
                "Shell/Teeth.cs:2: names a font family: var title = new TextBlock { FontSize = 12, FontWeight = (FontWeight)700, FontFamily = new FontFamily(\"Segoe UI\") };",
                "Shell/Teeth.cs:3: names a colour: var frame = new Border { CornerRadius = new CornerRadius(0), Background = Brush.Parse(\"#BBBBBB\") };",
                "Shell/Teeth.cs:4: names a corner radius: var ring = new Border { CornerRadius = new CornerRadius(4) };",
                "Shell/Teeth.cs:5: names a duration: var ease = new BrushTransition { Duration = TimeSpan.FromMilliseconds(100) };",
            },
            findings,
            "Found:" + Environment.NewLine + string.Join(Environment.NewLine, findings));
        Assert.AreEqual(1, exempt["Styles/Tokens.axaml"], "A colour in the token file is counted, not failed.");
    }

    // The scan of a copy of one real file, with one token reference turned
    // back into a literal, names exactly that line.
    private static void AssertFindsOne(string path, string text, string token, string literal, string what)
    {
        Assert.AreEqual(2, text.Split(token).Length, $"{path} must hold {token} exactly once.");
        string copy = text.Replace(token, literal, StringComparison.Ordinal);
        (List<string> clean, _) = LiteralScan.Scan([(path, text)]);
        Assert.IsEmpty(clean, $"The real {path} names a literal: {string.Join("; ", clean)}");

        string[] lines = copy.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        int line = Array.FindIndex(lines, candidate => candidate.Contains(literal, StringComparison.Ordinal));
        (List<string> findings, _) = LiteralScan.Scan([(path, copy)]);
        CollectionAssert.AreEqual(new[] { $"{path}:{line + 1}: {what}: {lines[line].Trim()}" }, findings, $"A copy of {path} with {literal}. Found: {string.Join("; ", findings)}");
    }
}
