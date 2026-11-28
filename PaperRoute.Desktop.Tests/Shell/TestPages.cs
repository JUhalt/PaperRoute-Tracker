using ManuscriptPipeline.Models;
using PaperRoute.Desktop.Tests.Board;
using PaperRoute.ViewModels;
using PaperRoute.ViewModels.Session;
using PaperRoute.ViewModels.Shell;

namespace PaperRoute.Desktop.Tests.Shell;

// A page that records what the navigator asks of it. Its leave guard answers
// AllowsLeave at once, or, while HeldAnswer is set, waits until the test
// gives the answer, as a page's question to the person would.
internal sealed class FakePage(PageKey key) : IPage
{
    public PageKey Key { get; } = key;

    public string Title => Key.Name;

    public List<PageRequest> ShownWith { get; } = [];

    public int HiddenCount { get; private set; }

    public int LeaveQuestions { get; private set; }

    public bool AllowsLeave { get; set; } = true;

    public TaskCompletionSource<bool>? HeldAnswer { get; set; }

    public void Shown(PageRequest request) => ShownWith.Add(request);

    public void Hidden() => HiddenCount++;

    public ValueTask<bool> CanLeaveAsync()
    {
        LeaveQuestions++;
        return HeldAnswer is { } held ? new ValueTask<bool>(held.Task) : ValueTask.FromResult(AllowsLeave);
    }
}

// A navigator over fake pages, with a library held in memory: the eight
// pages of build 5, every one kept but Settings, as the real catalog has
// them (shell design 3.1 row 16). Every page made is counted.
internal sealed class TestPages
{
    private static readonly PageKey[] s_keys =
    [
        PageKey.Board, PageKey.Library, PageKey.Journals, PageKey.Deadlines,
        PageKey.Insights, PageKey.ImportExport, PageKey.Manuscript, PageKey.Settings,
    ];

    public TestPages(params Manuscript[] manuscripts)
    {
        Session = new MemorySession(manuscripts, Papers.Today);
        Catalog = new PageCatalog(s_keys.Select(key => new PageCatalogRow(
            key, key.Name, Glyph: null, Digit: null, RailPlace.None, Kept: key != PageKey.Settings, Make: _ => Make(key))));
        Navigator = new Navigator(Catalog, Session, new NoticeLog(), new QueuedUiDispatcher(), new ManualDebouncer());
    }

    public MemorySession Session { get; }

    public PageCatalog Catalog { get; }

    public Navigator Navigator { get; }

    public List<FakePage> Made { get; } = [];

    public IReadOnlyList<FakePage> MadeFor(PageKey key) => [.. Made.Where(page => page.Key == key)];

    // The page of that key made last.
    public FakePage Page(PageKey key) => Made.Last(page => page.Key == key);

    private FakePage Make(PageKey key)
    {
        var page = new FakePage(key);
        Made.Add(page);
        return page;
    }
}

// Notices, kept for a test to read.
internal sealed class NoticeLog : IInteractions
{
    public List<string> Notices { get; } = [];

    public void Notice(string text) => Notices.Add(text);
}
