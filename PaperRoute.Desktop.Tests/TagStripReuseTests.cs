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

    // The check the Board's tests make of a card's strip
    // (CardParts.TagsDrawnWrong) is not satisfied by any strip: it accepts a
    // strip for the tags it was given, with and without "+N", and names a
    // strip that draws another tag, another colour, a chip too many or too
    // few for the tags asked about.
    [TestMethod]
    public void TheCheckOfAStripNamesAStripThatDrawsOtherTags()
    {
        (string Text, TagPalette Palette)[] two = [("alpha", TagPalette.Blue), ("beta", TagPalette.Teal)];
        (string Text, TagPalette Palette)[] many = [("a rather long tag name", TagPalette.Blue), ("another long tag name", TagPalette.Violet), ("third", TagPalette.Teal), ("fourth", TagPalette.Rose)];
        Dictionary<string, string?> found = Headless.Run(() =>
        {
            var strip = new TagStripPanel { Width = Width, Tags = Tags(two) };
            var window = new Window { Width = 400, Height = 200, Content = new StackPanel { Children = { strip } } };
            window.Show();
            Dispatcher.UIThread.RunJobs();
            try
            {
                var found = new Dictionary<string, string?>
                {
                    ["two shown"] = string.Join(", ", strip.ShownTexts),
                    ["its own tags"] = CardParts.TagsDrawnWrong(strip, Tags(two)),
                    ["another tag"] = CardParts.TagsDrawnWrong(strip, Tags([("alpha", TagPalette.Blue), ("gamma", TagPalette.Teal)])),
                    ["another colour"] = CardParts.TagsDrawnWrong(strip, Tags([("alpha", TagPalette.Amber), ("beta", TagPalette.Teal)])),
                    ["one tag fewer"] = CardParts.TagsDrawnWrong(strip, Tags([("alpha", TagPalette.Blue)])),
                    ["one tag more"] = CardParts.TagsDrawnWrong(strip, Tags([("alpha", TagPalette.Blue), ("beta", TagPalette.Teal), ("gamma", TagPalette.Violet)])),
                    ["no tags"] = CardParts.TagsDrawnWrong(strip, Tags([])),
                };

                strip.Tags = Tags(many);
                window.UpdateLayout();
                Dispatcher.UIThread.RunJobs();
                found["many shown"] = string.Join(", ", strip.ShownTexts);
                found["its own tags, with +N"] = CardParts.TagsDrawnWrong(strip, Tags(many));
                found["one tag fewer, with +N"] = CardParts.TagsDrawnWrong(strip, Tags(many.Take(3).ToArray()));
                return found;
            }
            finally
            {
                window.Close();
            }
        });

        string all = string.Join("\n", found.Select(pair => $"{pair.Key}: {pair.Value ?? "(nothing wrong)"}"));
        Assert.AreEqual("alpha, beta", found["two shown"], all);
        Assert.IsNull(found["its own tags"], all);
        Assert.Contains("gamma", found["another tag"] ?? string.Empty, all);
        Assert.Contains("tag-amber", found["another colour"] ?? string.Empty, all);
        Assert.Contains("2 tag chips for 1 tags", found["one tag fewer"] ?? string.Empty, all);
        Assert.Contains("no \"+1\"", found["one tag more"] ?? string.Empty, all);
        Assert.Contains("2 tag chips for 0 tags", found["no tags"] ?? string.Empty, all);
        Assert.IsTrue((found["many shown"] ?? string.Empty).Split(", ")[^1].StartsWith('+'), "The long tags need \"+N\". " + all);
        Assert.IsNull(found["its own tags, with +N"], all);
        Assert.IsNotNull(found["one tag fewer, with +N"], all);
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

