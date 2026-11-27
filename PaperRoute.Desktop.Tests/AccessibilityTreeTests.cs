using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Styling;
using ManuscriptPipeline.Models;
using PaperRoute.Desktop.Tests.Board;
using PaperRoute.Desktop.Tests.Input;
using PaperRoute.TestFixtures;
using PaperRoute.ViewModels.Board;
using PaperRoute.ViewModels.Session;

namespace PaperRoute.Desktop.Tests;

// What a screen reader and the keyboard meet on the shown Board, held to a
// checked-in text (the performance wave of 2026-11-21: a change made for speed
// must not change it). One line per element of the automation tree, walked
// from the window's peer as an accessibility client walks it: its control type
// as its peer reports it, its name, its help text, its automation id, whether
// it is a tab stop, and its place in the Tab order (the stops Tab reaches from
// where the window puts the keyboard first, numbered from 1; "-" for none).
// Then the Tab order itself, one stop a line.
//
// The fixtures' manuscript ids are new on each write, so a card's id in an
// automation id is written as its title and occurrence.
//
// When the text differs, the test writes what it found beside the pictures
// (<name>.actual.txt) and names the first line that differs. A change that
// means to alter the tree replaces the expected file with that text, and says
// why in its commit.
// Limits of headless: HeadlessLimits.md
[TestClass]
public sealed class AccessibilityTreeTests : StorageCheckedTests
{
    private const int ExampleTabLimit = 120;
    private const int FiveHundredTabLimit = 40;

    private static readonly Regex s_cardKey = new("Card\\.[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\\.[0-9]+", RegexOptions.CultureInvariant);

    private string _root = string.Empty;

    public TestContext TestContext { get; set; } = null!;

    [TestInitialize]
    public void CreateRoot()
    {
        _root = Path.Combine(Path.GetTempPath(), "PaperRoute-TrialTree-" + Guid.NewGuid().ToString("N"));
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
    public void TheExampleBoardsTreeAndTabOrderAreUnchanged()
    {
        using DiskLibrarySession session = Open("example", root => LargeLibraryFixture.WriteExample(root));
        string text = Headless.Run(() =>
        {
            using BoardWindow board = BoardWindow.Show(session, ThemeVariant.Light);
            return AccessibilityTree.Describe(board, ExampleTabLimit);
        });

        Compare("board-tree-example", text);
    }

    [TestMethod]
    public void TheFiveHundredLibrarysFirstScreenIsUnchanged()
    {
        using DiskLibrarySession session = Open("five-hundred", root => LargeLibraryFixture.Create(root, 500));
        string text = Headless.Run(() =>
        {
            using BoardWindow board = BoardWindow.Show(session, ThemeVariant.Light);
            return AccessibilityTree.Describe(board, FiveHundredTabLimit);
        });

        Compare("board-tree-500", text);
    }

    private void Compare(string name, string actual)
    {
        string expectedPath = Path.Combine(AppContext.BaseDirectory, "Expected", name + ".txt");
        string expected = File.Exists(expectedPath) ? File.ReadAllText(expectedPath).Replace("\r\n", "\n", StringComparison.Ordinal) : string.Empty;
        if (expected == actual)
        {
            return;
        }

        string folder = Pictures.Directory();
        Directory.CreateDirectory(folder);
        string actualPath = Path.Combine(folder, name + ".actual.txt");
        File.WriteAllText(actualPath, actual);
        string[] want = expected.Split('\n');
        string[] got = actual.Split('\n');
        int line = 0;
        while (line < want.Length && line < got.Length && want[line] == got[line])
        {
            line++;
        }

        Assert.Fail($"The accessibility tree of {name} differs from {expectedPath} at line {line + 1}:\n" +
            $"  expected: {(line < want.Length ? want[line] : "(end)")}\n" +
            $"  found:    {(line < got.Length ? got[line] : "(end)")}\n" +
            $"What was found is in {actualPath}.");
    }

    private DiskLibrarySession Open(string folder, Action<string> write)
    {
        string root = Path.Combine(_root, folder);
        Directory.CreateDirectory(root);
        write(root);
        var session = new DiskLibrarySession(
            Path.Combine(root, "data"),
            Path.Combine(root, "library"),
            Path.Combine(root, "settings"),
            LibraryKind.Example,
            LargeLibraryFixture.Today);
        session.LoadAsync(CancellationToken.None).GetAwaiter().GetResult();
        return session;
    }

    // The tree and the Tab order of a shown Board, as text. The tree is
    // written first, as the window first shows it; then the keyboard walks
    // the Tab order (which may scroll a shelf), up to limit stops or until a
    // stop comes round again.
    internal static class AccessibilityTree
    {
        public static string Describe(BoardWindow board, int limit)
        {
            var keys = board.Board.Shelves.SelectMany(shelf => shelf.Cards)
                .GroupBy(card => card.AutomationKey)
                .ToDictionary(group => group.Key, group => group.First());
            string Clean(string? value) => s_cardKey.Replace(value ?? string.Empty, match =>
                keys.TryGetValue(match.Value, out CardViewModel? card)
                    ? "Card[" + card.Title + "#" + match.Value[(match.Value.LastIndexOf('.') + 1)..] + "]"
                    : "Card[?]")
                .Replace("\r", "\\r", StringComparison.Ordinal)
                .Replace("\n", "\\n", StringComparison.Ordinal);

            // The tree as shown, before any key: its peers and their owners.
            var nodes = new List<(int Depth, AutomationPeer Peer, Control? Owner)>();
            Walk(ControlAutomationPeer.CreatePeerForElement(board.Window), 0, nodes);

            // The Tab order.
            IReadOnlyList<Control> stops = FocusWalk.Tab(board.Window, limit);
            var places = new Dictionary<Control, int>(ReferenceEqualityComparer.Instance);
            for (int index = 0; index < stops.Count; index++)
            {
                places[stops[index]] = index + 1;
            }

            var text = new StringBuilder();
            text.Append("# tree\n");
            foreach ((int depth, AutomationPeer peer, Control? owner) in nodes)
            {
                bool tabStop = owner is not null && owner.Focusable && owner.IsTabStop;
                string place = owner is not null && places.TryGetValue(owner, out int at) ? at.ToString(CultureInfo.InvariantCulture) : "-";
                text.Append(new string(' ', depth * 2))
                    .Append(peer.GetAutomationControlType())
                    .Append(" | name=\"").Append(Clean(peer.GetName())).Append('"')
                    .Append(" | help=\"").Append(Clean(peer.GetHelpText())).Append('"')
                    .Append(" | id=\"").Append(Clean(peer.GetAutomationId())).Append('"')
                    .Append(" | tabstop=").Append(tabStop ? "yes" : "no")
                    .Append(" | tab=").Append(place)
                    .Append('\n');
            }

            text.Append(CultureInfo.InvariantCulture, $"# tab order (up to {limit} stops)\n");
            for (int index = 0; index < stops.Count; index++)
            {
                Control stop = stops[index];
                AutomationPeer peer = ControlAutomationPeer.CreatePeerForElement(stop);
                text.Append(CultureInfo.InvariantCulture, $"{index + 1} | {stop.GetType().Name} | {peer.GetAutomationControlType()} | name=\"{Clean(peer.GetName())}\" | id=\"{Clean(peer.GetAutomationId())}\"\n");
            }

            return text.ToString();
        }

        private static void Walk(AutomationPeer peer, int depth, List<(int, AutomationPeer, Control?)> nodes)
        {
            nodes.Add((depth, peer, (peer as ControlAutomationPeer)?.Owner));
            foreach (AutomationPeer child in peer.GetChildren())
            {
                Walk(child, depth + 1, nodes);
            }
        }
    }
}
