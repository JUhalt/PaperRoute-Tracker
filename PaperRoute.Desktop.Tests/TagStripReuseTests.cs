using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ManuscriptPipeline.Models;
using PaperRoute.ViewModels.Board;

namespace PaperRoute.Desktop.Tests;

// A tag strip given card after card (as rows of N refill a card view) shows
// what a new strip given the same tags shows: the same chips and "+N", the
// same palettes, the same size, the same accessibility tree, tooltip and
// name. The lists run through more tags, fewer, none, the same again, the
// same texts in another palette, and long names that need "+N".
[TestClass]
public sealed class TagStripReuseTests
{
    private const double Width = 160;

    private static readonly IReadOnlyList<(string Text, TagPalette Palette)[]> s_lists =
    [
        [("alpha", TagPalette.Blue)],
        [("alpha", TagPalette.Blue), ("beta", TagPalette.Teal), ("gamma", TagPalette.Violet)],
        [("alpha", TagPalette.Blue), ("beta", TagPalette.Teal), ("gamma", TagPalette.Violet)],
        [("beta", TagPalette.Teal)],
        [],
        [("alpha", TagPalette.Amber), ("beta", TagPalette.Teal), ("gamma", TagPalette.Violet)],
        [("a rather long tag name", TagPalette.Blue), ("another long tag name", TagPalette.Violet), ("third", TagPalette.Teal), ("fourth", TagPalette.Rose)],
        [("x", TagPalette.Blue), ("y", TagPalette.Blue), ("z", TagPalette.Blue), ("w", TagPalette.Blue), ("v", TagPalette.Blue), ("u", TagPalette.Blue), ("t", TagPalette.Blue)],
        [("a rather long tag name", TagPalette.Blue), ("another long tag name", TagPalette.Violet), ("third", TagPalette.Teal), ("fourth", TagPalette.Rose)],
        [("solo", TagPalette.Rose)],
        [],
        [("alpha", TagPalette.Blue), ("beta", TagPalette.Teal), ("gamma", TagPalette.Violet), ("delta", TagPalette.Amber), ("epsilon", TagPalette.Violet)],
    ];

    [TestMethod]
    public void AStripGivenTagAfterTagShowsWhatANewStripShows()
    {
        List<string> wrong = Headless.Run(() =>
        {
            var reused = new TagStripPanel { Width = Width };
            var fresh = new ContentControl();
            var panel = new StackPanel { Children = { reused, fresh } };
            var window = new Window { Width = 400, Height = 200, Content = panel };
            window.Show();
            Dispatcher.UIThread.RunJobs();
            var wrong = new List<string>();
            try
            {
                for (int step = 0; step < s_lists.Count; step++)
                {
                    IReadOnlyList<TagViewModel> tags = Tags(s_lists[step]);
                    var strip = new TagStripPanel { Width = Width, Tags = tags };
                    fresh.Content = strip;
                    reused.Tags = Tags(s_lists[step]);
                    window.UpdateLayout();
                    Dispatcher.UIThread.RunJobs();

                    string got = Describe(reused);
                    string want = Describe(strip);
                    if (got != want)
                    {
                        wrong.Add($"step {step}: reused strip {got}\n  new strip {want}");
                    }
                }
            }
            finally
            {
                window.Close();
            }

            return wrong;
        });

        Assert.IsEmpty(wrong, string.Join("\n", wrong));
    }

    private static IReadOnlyList<TagViewModel> Tags((string Text, TagPalette Palette)[] list) =>
        list.Select(tag => new TagViewModel(tag.Text, tag.Palette)).ToList();

    // What a strip shows and tells: its texts, the chips drawn with their
    // classes, places and sizes, its own size, and its automation tree.
    private static string Describe(TagStripPanel strip)
    {
        IEnumerable<string> drawn = strip.Children
            .Where(child => child.IsEffectivelyVisible && child.Bounds.Width > 0)
            .Select(child => $"{((TextBlock)((Border)child).Child!).Text}[{string.Join(" ", child.Classes)}]@{child.Bounds} {((Border)child).Background} {((TextBlock)((Border)child).Child!).Foreground}");
        AutomationPeer peer = ControlAutomationPeer.CreatePeerForElement(strip);
        IEnumerable<string> tree = peer.GetChildren().Select(Peer);
        return $"shows [{string.Join(", ", strip.ShownTexts)}] size {strip.DesiredSize} bounds {strip.Bounds} " +
            $"drawn [{string.Join(", ", drawn)}] tip \"{ToolTip.GetTip(strip)}\" name \"{AutomationProperties.GetName(strip)}\" " +
            $"tree [{Peer(peer)}: {string.Join(", ", tree)}] visuals {strip.GetVisualDescendants().Count(visual => visual.IsEffectivelyVisible)}";
    }

    private static string Peer(AutomationPeer peer) =>
        $"{peer.GetAutomationControlType()} \"{peer.GetName()}\" ({string.Join(", ", peer.GetChildren().Select(Peer))})";
}

