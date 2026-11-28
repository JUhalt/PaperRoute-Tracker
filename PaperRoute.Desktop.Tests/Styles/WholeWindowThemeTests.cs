using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ManuscriptPipeline.Models;
using PaperRoute.Desktop.Styles;
using PaperRoute.TestFixtures;
using PaperRoute.ViewModels.Session;

namespace PaperRoute.Desktop.Tests.Styles;

// The whole window follows every theme change while it is open (harness
// design 3.4, pull request S3; #365's symptom as a test). After each change,
// every brush any control holds that is a token brush of any variant (Light,
// Dark and the two contrast variants: Tokens.axaml's brushes and the derived
// ones) must be the current variant's own object; none may be left from the
// variant before. Object identity, so the check is exact. And after a round
// trip back to Dark and to Light, every shown control must hold the same
// token (or the same other brush) in each brush property, and the same
// motion, as it held there: a setter left from a contrast theme holds a brush
// of the right variant, which identity alone cannot see.
[TestClass]
public sealed class WholeWindowThemeTests
{
    private static readonly Dictionary<Type, AvaloniaProperty[]> s_brushProperties = [];

    private string _root = string.Empty;

    public TestContext TestContext { get; set; } = null!;

    [TestInitialize]
    public void CreateRoot()
    {
        _root = Path.Combine(Path.GetTempPath(), "PaperRoute-TrialWholeWindow-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    [TestCleanup]
    public void DeleteRoot()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // Left for Windows' temporary-file clean-up.
        }
        catch (UnauthorizedAccessException)
        {
            // Left for Windows' temporary-file clean-up.
        }
    }

    // The shell over the example library (its rail, banner, notice strip and
    // Board) and the style gallery, both open, go Light, Dark, hc2, hcwhite,
    // hc1, Dark, Light through AppTheme. Each part must hold at least one
    // token brush of the current variant, and no part one of another.
    [TestMethod]
    public void TheWholeWindowFollowsEveryThemeChange()
    {
        LargeLibraryFixture.WriteExample(_root);
        using var session = new DiskLibrarySession(
            Path.Combine(_root, "data"),
            Path.Combine(_root, "library"),
            _root,
            LibraryKind.Example,
            LargeLibraryFixture.Today);
        session.LoadAsync(CancellationToken.None).GetAwaiter().GetResult();

        List<string> failures = Headless.Run(() =>
        {
            var problems = new List<string>();
            Application app = Application.Current!;
            AppTheme theme = AppTheme.Of(app);
            var preference = new ContrastPreference(false, SystemContrastColors.Fallback);
            theme.Follow(preference);
            theme.Appearance = Appearance.Light;
            TokenBrushes brushes = TokenBrushes.Of(app);

            using BoardWindow board = BoardWindow.Show(session);
            var gallery = new StyleGallery();
            var galleryWindow = new Window { Width = 1280, Height = 900, Content = gallery };
            galleryWindow.Bind(Window.BackgroundProperty, galleryWindow.GetResourceObservable("BoardBackgroundBrush"));
            galleryWindow.Show();
            try
            {
                Visual[] parts = new[] { "Rail", "Banner", "NoticeStrip" }.Select(name => (Visual)Named(board.Window, name)).Append(board.View).ToArray();
                (string Name, Func<IEnumerable<Visual>> Visuals)[] regions =
                [
                    ("the rail", () => parts[0].GetSelfAndVisualDescendants()),
                    ("the banner", () => parts[1].GetSelfAndVisualDescendants()),
                    ("the notice strip", () => parts[2].GetSelfAndVisualDescendants()),
                    ("the Board", () => parts[3].GetSelfAndVisualDescendants()),
                    ("the rest of the shell window", () =>
                    {
                        var inParts = parts.SelectMany(part => part.GetSelfAndVisualDescendants()).ToHashSet();
                        return board.Window.GetSelfAndVisualDescendants().Where(visual => !inParts.Contains(visual));
                    }),
                    ("the gallery", () => galleryWindow.GetSelfAndVisualDescendants()),
                ];

                // The two last steps return to a variant shown before, and
                // every shown control must then hold what it held there.
                (string Name, Action Change, ThemeVariant Variant, string? SameAs)[] steps =
                [
                    ("Light", () => { }, ThemeVariant.Light, null),
                    ("Dark", () => theme.Appearance = Appearance.Dark, ThemeVariant.Dark, null),
                    ("hc2", () => preference.Set(true, SystemContrastColors.NightSky), ContrastTheme.HighContrastDark, null),
                    ("hcwhite", () => preference.Set(true, SystemContrastColors.Desert), ContrastTheme.HighContrastLight, null),
                    ("hc1", () => preference.Set(true, SystemContrastColors.Hc1), ContrastTheme.HighContrastDark, null),
                    ("Dark again (contrast off)", () => preference.Set(false, SystemContrastColors.Fallback), ThemeVariant.Dark, "Dark"),
                    ("Light again", () => theme.Appearance = Appearance.Light, ThemeVariant.Light, "Light"),
                ];

                var shown = new Dictionary<string, Dictionary<(Visual, AvaloniaProperty), (string Where, string Held)>>(StringComparer.Ordinal);
                foreach ((string name, Action change, ThemeVariant variant, string? sameAs) in steps)
                {
                    change();
                    Dispatcher.UIThread.RunJobs();
                    if (app.ActualThemeVariant != variant)
                    {
                        problems.Add($"{name}: the app shows {app.ActualThemeVariant}, not {variant}");
                        continue;
                    }

                    // A brush transition (100 ms in Light and Dark) shows an
                    // eased brush for a moment: the check waits until every
                    // transition has ended (five seconds at most, for a busy
                    // PC); a brush left behind stays.
                    List<string> stale = [];
                    Dictionary<string, int> held = [];
                    int easing = 0;
                    BoardWindow.WaitFor(() =>
                    {
                        (stale, held, easing) = brushes.Judge(regions, variant);
                        return stale.Count == 0 && easing == 0;
                    }, TimeSpan.FromSeconds(5));
                    problems.AddRange(stale.Select(line => $"{name}: {line}"));
                    if (easing > 0)
                    {
                        problems.Add($"{name}: {easing} brushes were still easing after five seconds");
                    }

                    problems.AddRange(held.Where(region => region.Value == 0).Select(region => $"{name}: {region.Key} holds no token brush of {variant}"));

                    // Contrast.axaml's rules hold in both open windows exactly
                    // under a contrast variant: the class is there, and its
                    // "no motion" rule has stopped every button face.
                    bool contrast = ContrastTheme.IsContrast(variant);
                    foreach (Window open in new[] { board.Window, galleryWindow })
                    {
                        if (open.Classes.Contains(AppTheme.ContrastClass) != contrast)
                        {
                            problems.Add($"{name}: {Describe(open)} {(contrast ? "lacks" : "carries")} the class {AppTheme.ContrastClass}");
                        }
                    }

                    // The rule's selector is Button exactly, so a scroll bar's
                    // RepeatButton or a ToggleButton keeps its own motion.
                    string[] moving = regions.SelectMany(region => region.Visuals())
                        .OfType<ContentPresenter>()
                        .Where(face => face.Name == "PART_ContentPresenter" && face.TemplatedParent?.GetType() == typeof(Button) && face.Transitions is not null)
                        .Select(face => Describe((StyledElement)face.TemplatedParent!))
                        .ToArray();
                    if (contrast && moving.Length > 0)
                    {
                        problems.Add($"{name}: {moving.Length} button faces still have motion under a contrast theme: {string.Join(", ", moving)}");
                    }
                    TestContext.WriteLine($"{name}: token brushes of {variant} held by " + string.Join(", ", held.Select(region => $"{region.Key} {region.Value}")));

                    // A round trip must leave every shown control as it was:
                    // a setter left from a contrast theme (#365's symptom)
                    // holds a brush of the right variant, so identity alone
                    // cannot see it.
                    shown[name] = brushes.Shown(regions);
                    if (sameAs is not null && shown.TryGetValue(sameAs, out Dictionary<(Visual, AvaloniaProperty), (string Where, string Held)>? before))
                    {
                        int compared = 0;
                        foreach (KeyValuePair<(Visual, AvaloniaProperty), (string Where, string Held)> entry in shown[name])
                        {
                            if (!before.TryGetValue(entry.Key, out (string Where, string Held) then))
                            {
                                continue;
                            }

                            compared++;
                            if (then.Held != entry.Value.Held)
                            {
                                problems.Add($"{name}: {entry.Value.Where} holds {entry.Value.Held}, not {then.Held} as at {sameAs}");
                            }
                        }

                        TestContext.WriteLine($"{name}: {compared} brush values of shown controls compared with {sameAs}");
                        if (compared < 1000)
                        {
                            problems.Add($"{name}: only {compared} brush values of shown controls were compared with {sameAs}");
                        }
                    }
                }
            }
            finally
            {
                galleryWindow.Close();
            }

            return problems;
        });

        Assert.IsEmpty(failures, string.Join(Environment.NewLine, failures));
    }

    // The check's teeth: a border given the Light card brush once, as a
    // StaticResource would, is named after the change to Dark; a border that
    // follows the token is not.
    [TestMethod]
    public void TheWholeWindowCheckFindsABrushLeftFromTheVariantBefore()
    {
        (List<string> stale, int held) = Headless.Run(() =>
        {
            Application app = Application.Current!;
            AppTheme theme = AppTheme.Of(app);
            theme.Appearance = Appearance.Light;
            TokenBrushes brushes = TokenBrushes.Of(app);
            var kept = new Border { Name = "Kept", Width = 50, Height = 50 };
            var followed = new Border { Name = "Followed", Width = 50, Height = 50 };
            var window = new Window { Width = 200, Height = 120, Content = new StackPanel { Children = { kept, followed } } };
            window.Show();
            try
            {
                if (!app.TryGetResource("CardBackgroundBrush", ThemeVariant.Light, out object? light) || light is not IBrush lightCard)
                {
                    throw new AssertFailedException("CardBackgroundBrush is not a brush in Light.");
                }

                kept.Background = lightCard;
                followed.Bind(Border.BackgroundProperty, followed.GetResourceObservable("CardBackgroundBrush"));
                theme.Appearance = Appearance.Dark;
                Dispatcher.UIThread.RunJobs();
                (string Name, Func<IEnumerable<Visual>> Visuals)[] regions = [("the window", () => window.GetSelfAndVisualDescendants())];
                (List<string> found, Dictionary<string, int> counts, _) = brushes.Judge(regions, app.ActualThemeVariant);
                return (found, counts["the window"]);
            }
            finally
            {
                window.Close();
            }
        });

        CollectionAssert.AreEqual(new[] { "the window: Border#Kept.Background holds CardBackgroundBrush of Light" }, stale, "Found:" + Environment.NewLine + string.Join(Environment.NewLine, stale));
        Assert.IsTrue(held >= 1, "The border that follows the token holds no brush of Dark.");
    }

    private static Control Named(Window window, string name) =>
        window.FindControl<Control>(name) ?? throw new AssertFailedException($"The window has no {name}.");

    // Every brush-valued property a type has (Background, Foreground,
    // BorderBrush, Fill, Stroke, the selection and caret brushes, ...).
    private static AvaloniaProperty[] BrushProperties(Type type)
    {
        if (!s_brushProperties.TryGetValue(type, out AvaloniaProperty[]? properties))
        {
            properties = AvaloniaPropertyRegistry.Instance.GetRegistered(type)
                .Where(property => typeof(IBrush).IsAssignableFrom(property.PropertyType))
                .Distinct()
                .ToArray();
            s_brushProperties[type] = properties;
        }

        return properties;
    }

    private static string Describe(StyledElement visual) =>
        visual is StyledElement { Name: { Length: > 0 } name } ? $"{visual.GetType().Name}#{name}" : visual.GetType().Name;

    // The token brushes of the running application, by variant.
    private sealed class TokenBrushes
    {
        private readonly Dictionary<IBrush, string> _names = new(ReferenceEqualityComparer.Instance);
        private readonly Dictionary<IBrush, string> _keys = new(ReferenceEqualityComparer.Instance);
        private readonly Dictionary<ThemeVariant, HashSet<IBrush>> _byVariant = [];

        public static TokenBrushes Of(Application application)
        {
            var result = new TokenBrushes();
            string[] keys = TokenFile.ThemedKeys()["Light"].Where(key => key != "OutlineThickness").Concat(ThemeTokens.DerivedKeys).ToArray();
            foreach (ThemeVariant variant in new[] { ThemeVariant.Light, ThemeVariant.Dark, ContrastTheme.HighContrastDark, ContrastTheme.HighContrastLight })
            {
                var set = new HashSet<IBrush>(ReferenceEqualityComparer.Instance);
                foreach (string key in keys)
                {
                    if (!application.TryGetResource(key, variant, out object? value) || value is not IBrush brush)
                    {
                        throw new AssertFailedException($"{key} is not a brush in {variant}.");
                    }

                    set.Add(brush);
                    result._names.TryAdd(brush, $"{key} of {variant}");
                    result._keys.TryAdd(brush, key);
                }

                result._byVariant[variant] = set;
            }

            return result;
        }

        // Each brush of another variant a region's controls hold, as
        // "region: control.Property holds Key of Variant", both the value
        // shown and the value under a running transition; per region, how many
        // brushes of the current variant its controls show; and how many
        // properties are still easing towards a token brush (the shown value
        // is then an eased brush, so the check waits until none is).
        public (List<string> Stale, Dictionary<string, int> Held, int Easing) Judge(IEnumerable<(string Name, Func<IEnumerable<Visual>> Visuals)> regions, ThemeVariant current)
        {
            HashSet<IBrush> now = _byVariant[current];
            var stale = new List<string>();
            var held = new Dictionary<string, int>(StringComparer.Ordinal);
            int easing = 0;
            foreach ((string name, Func<IEnumerable<Visual>> visuals) in regions)
            {
                int count = 0;
                foreach (Visual visual in visuals())
                {
                    foreach (AvaloniaProperty property in BrushProperties(visual.GetType()))
                    {
                        IBrush? shown = visual.GetValue(property) as IBrush;
                        IBrush? underneath = property is StyledProperty<IBrush?> styled && visual.GetBaseValue(styled) is { HasValue: true } value ? value.Value : shown;
                        if (shown is not null && _names.TryGetValue(shown, out string? token))
                        {
                            if (now.Contains(shown))
                            {
                                count++;
                            }
                            else
                            {
                                stale.Add($"{name}: {Describe(visual)}.{property.Name} holds {token}");
                            }
                        }

                        if (underneath is not null && !ReferenceEquals(underneath, shown) && _names.TryGetValue(underneath, out string? eased))
                        {
                            easing++;
                            if (!now.Contains(underneath))
                            {
                                stale.Add($"{name}: {Describe(visual)}.{property.Name} holds {eased} under a transition");
                            }
                        }
                    }
                }

                held[name] = count;
            }

            return (stale, held, easing);
        }

        // What every effectively visible control shows in each brush property
        // (the token's key whatever its variant, "null", or "other" with the
        // colour or the brush's type) and whether its motion is on, keyed by
        // the control and the property.
        public Dictionary<(Visual, AvaloniaProperty), (string Where, string Held)> Shown(IEnumerable<(string Name, Func<IEnumerable<Visual>> Visuals)> regions)
        {
            var result = new Dictionary<(Visual, AvaloniaProperty), (string Where, string Held)>();
            foreach ((string name, Func<IEnumerable<Visual>> visuals) in regions)
            {
                foreach (Visual visual in visuals().Where(visual => visual.IsEffectivelyVisible))
                {
                    string where = visual.TemplatedParent is StyledElement parent
                        ? $"{name}: {Describe(visual)} in {Describe(parent)}[{string.Join(" ", parent.Classes)}]"
                        : $"{name}: {Describe(visual)}";
                    foreach (AvaloniaProperty property in BrushProperties(visual.GetType()))
                    {
                        string held = visual.GetValue(property) switch
                        {
                            null => "null",
                            IBrush brush when _keys.TryGetValue(brush, out string? key) => key,
                            ISolidColorBrush solid => $"other {solid.Color} at {solid.Opacity}",
                            object other => $"other {other.GetType().Name}",
                        };
                        result.TryAdd((visual, property), ($"{where}.{property.Name}", held));
                    }

                    // Contrast.axaml also turns motion off; it must come back.
                    string motion = visual.Transitions is { } transitions ? $"{transitions.Count} transitions" : "null";
                    result.TryAdd((visual, Animatable.TransitionsProperty), ($"{where}.Transitions", motion));
                }
            }

            return result;
        }
    }
}
