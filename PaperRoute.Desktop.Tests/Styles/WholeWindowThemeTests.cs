using Avalonia;
using Avalonia.Controls;
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
// variant before. Object identity, so the check is exact.
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

                (string Name, Action Change, ThemeVariant Variant)[] steps =
                [
                    ("Light", () => { }, ThemeVariant.Light),
                    ("Dark", () => theme.Appearance = Appearance.Dark, ThemeVariant.Dark),
                    ("hc2", () => preference.Set(true, SystemContrastColors.NightSky), ContrastTheme.HighContrastDark),
                    ("hcwhite", () => preference.Set(true, SystemContrastColors.Desert), ContrastTheme.HighContrastLight),
                    ("hc1", () => preference.Set(true, SystemContrastColors.Hc1), ContrastTheme.HighContrastDark),
                    ("Dark again (contrast off)", () => preference.Set(false, SystemContrastColors.Fallback), ThemeVariant.Dark),
                    ("Light again", () => theme.Appearance = Appearance.Light, ThemeVariant.Light),
                ];

                foreach ((string name, Action change, ThemeVariant variant) in steps)
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
                    TestContext.WriteLine($"{name}: token brushes of {variant} held by " + string.Join(", ", held.Select(region => $"{region.Key} {region.Value}")));
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

    private static string Describe(Visual visual) =>
        visual is StyledElement { Name: { Length: > 0 } name } ? $"{visual.GetType().Name}#{name}" : visual.GetType().Name;

    // The token brushes of the running application, by variant.
    private sealed class TokenBrushes
    {
        private readonly Dictionary<IBrush, string> _names = new(ReferenceEqualityComparer.Instance);
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
    }
}
