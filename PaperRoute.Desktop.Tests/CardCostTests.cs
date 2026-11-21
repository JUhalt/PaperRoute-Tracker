using System.Diagnostics;
using System.Globalization;
using System.Text;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Threading;
using ManuscriptPipeline.Models;
using PaperRoute.Desktop.Tests.Board;
using PaperRoute.TestFixtures;
using PaperRoute.ViewModels.Board;
using PaperRoute.ViewModels.Session;

namespace PaperRoute.Desktop.Tests;

// What one card costs, warm, headless (the card-cost profile of 2026-11-20,
// made a test so that anyone can measure a card again). On the 500 library,
// 120 cards of the Published shelf and 120 of the Pipeline, each set timed
// in rounds after two warm-ups:
//   first show: a new CardView made, given its card and laid out in a 300 px
//               column (make + bind + attach), per card;
//   reuse:      each of those views given another card and laid out again
//               (rebind + relayout), per card, as rows of N refill a row.
// The table goes to the test log: per set and part, the median of the rounds
// and their spread. Reported, not held. Run by hand (TestCategory Manual):
//   dotnet test ... --configuration Release --filter "FullyQualifiedName~CardCostTests"
[TestClass]
public sealed class CardCostTests : StorageCheckedTests
{
    private const int Cards = 120;
    private const int Rounds = 6;
    private const int WarmUps = 2;
    private const double CardWidth = 300;

    private static readonly string[] s_parts = ["make", "bind", "attach", "first show", "rebind", "relayout", "reuse"];

    private string _root = string.Empty;

    public TestContext TestContext { get; set; } = null!;

    [TestInitialize]
    public void CreateRoot()
    {
        _root = Path.Combine(Path.GetTempPath(), "PaperRoute-CardCost-" + Guid.NewGuid().ToString("N"));
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
            // Left for the system's temporary-file clean-up.
        }
        catch (UnauthorizedAccessException)
        {
            // Left for the system's temporary-file clean-up.
        }
    }

    [TestMethod]
    [TestCategory(BoardBudgetTests.ManualCategory)]
    public void ACardsFirstShowAndReuseAreMeasured()
    {
        LargeLibraryFixture.Create(_root, 500);
        using var session = new DiskLibrarySession(
            Path.Combine(_root, "data"), Path.Combine(_root, "library"), Path.Combine(_root, "settings"),
            LibraryKind.Example, LargeLibraryFixture.Today);
        session.LoadAsync(CancellationToken.None).GetAwaiter().GetResult();

        string table = Headless.Run(() => Measure(session), TimeSpan.FromMinutes(20));
        TestContext.WriteLine(table);
    }

    private static string Measure(ILibrarySession session)
    {
        var board = new BoardHarness(session.Manuscripts, today: LargeLibraryFixture.Today);
        var sets = new List<(string Name, List<CardViewModel> First, List<CardViewModel> Then)>
        {
            Split("Published", board.Published.Cards.ToList()),
            Split("Pipeline", board.Pipeline.Cards.ToList()),
        };

        var host = new StackPanel { Width = CardWidth, HorizontalAlignment = HorizontalAlignment.Left };
        var window = new Window { Width = 1280, Height = 800, Content = host };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        try
        {
            for (int warm = 0; warm < WarmUps; warm++)
            {
                foreach ((_, List<CardViewModel> first, List<CardViewModel> then) in sets)
                {
                    Run(host, window, first, then);
                }
            }

            var results = sets.ToDictionary(set => set.Name, _ => new List<double[]>());
            for (int round = 0; round < Rounds; round++)
            {
                // Alternate the order of the sets from round to round.
                IEnumerable<(string Name, List<CardViewModel> First, List<CardViewModel> Then)> order = round % 2 == 0 ? sets : Enumerable.Reverse(sets);
                foreach ((string name, List<CardViewModel> first, List<CardViewModel> then) in order)
                {
                    results[name].Add(Run(host, window, first, then));
                }
            }

            var text = new StringBuilder();
            text.AppendLine(CultureInfo.InvariantCulture, $"Card cost, ms per card, warm, headless ({Cards} cards a set, {Rounds} rounds after {WarmUps} warm-ups, {CardWidth} px wide, build {BuildName()}):");
            text.AppendLine("| set | part | median | min | max | rounds |");
            text.AppendLine("|---|---|---|---|---|---|");
            foreach ((string name, List<double[]> rounds) in results)
            {
                for (int part = 0; part < s_parts.Length; part++)
                {
                    double[] values = rounds.Select(round => round[part]).OrderBy(value => value).ToArray();
                    string all = string.Join(" ", rounds.Select(round => round[part].ToString("F2", CultureInfo.InvariantCulture)));
                    text.AppendLine(CultureInfo.InvariantCulture, $"| {name} | {s_parts[part]} | {Median(values):F2} | {values[0]:F2} | {values[^1]:F2} | {all} |");
                }
            }

            return text.ToString();
        }
        finally
        {
            window.Close();
        }
    }

    private static (string, List<CardViewModel>, List<CardViewModel>) Split(string name, List<CardViewModel> all)
    {
        List<CardViewModel> first = Enumerable.Range(0, Cards).Select(index => all[index % all.Count]).ToList();
        List<CardViewModel> then = Enumerable.Range(0, Cards).Select(index => all[all.Count - 1 - (index % all.Count)]).ToList();
        return (name, first, then);
    }

    // One round: per card, make, bind, attach, first show, rebind, relayout, reuse.
    private static double[] Run(StackPanel host, Window window, List<CardViewModel> cards, List<CardViewModel> others)
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var clock = new Stopwatch();
        double make = 0, bind = 0, attach = 0, rebind = 0, relayout = 0;
        var views = new List<CardView>(cards.Count);
        foreach (CardViewModel card in cards)
        {
            clock.Restart();
            var view = new CardView();
            make += clock.Elapsed.TotalMilliseconds;
            clock.Restart();
            view.DataContext = card;
            bind += clock.Elapsed.TotalMilliseconds;
            clock.Restart();
            host.Children.Add(view);
            window.UpdateLayout();
            attach += clock.Elapsed.TotalMilliseconds;
            views.Add(view);
        }

        for (int index = 0; index < views.Count; index++)
        {
            clock.Restart();
            views[index].DataContext = others[index];
            rebind += clock.Elapsed.TotalMilliseconds;
            clock.Restart();
            window.UpdateLayout();
            relayout += clock.Elapsed.TotalMilliseconds;
        }

        host.Children.Clear();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        double n = cards.Count;
        return [make / n, bind / n, attach / n, (make + bind + attach) / n, rebind / n, relayout / n, (rebind + relayout) / n];
    }

    private static double Median(double[] sorted) =>
        sorted.Length % 2 == 1 ? sorted[sorted.Length / 2] : (sorted[(sorted.Length / 2) - 1] + sorted[sorted.Length / 2]) / 2;

    private static string BuildName()
    {
#if DEBUG
        return "Debug";
#else
        return "Release";
#endif
    }
}
