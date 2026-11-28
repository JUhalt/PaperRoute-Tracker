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

    // What the page does when shown or hidden, beyond counting it.
    public Action? WhenShown { get; set; }

    public Action? WhenHidden { get; set; }

    public void Shown(PageRequest request)
    {
        ShownWith.Add(request);
        WhenShown?.Invoke();
    }

    public void Hidden()
    {
        HiddenCount++;
        WhenHidden?.Invoke();
    }

    public ValueTask<bool> CanLeaveAsync()
    {
        LeaveQuestions++;
        return HeldAnswer is { } held ? new ValueTask<bool>(held.Task) : ValueTask.FromResult(AllowsLeave);
    }
}

// A navigator over fake pages, with a library held in memory: the real
// catalog's rows with only the factory swapped, so which pages are kept is
// what the catalog ships (shell design 3.1 row 16). Every page made is
// counted.
internal sealed class TestPages
{
    public TestPages(params Manuscript[] manuscripts)
    {
        Session = new MemorySession(manuscripts, Papers.Today);
        Catalog = new PageCatalog(PageCatalog.Standard.Rows.Select(row => row with { Make = _ => Make(row.Key) }));
        Navigator = new Navigator(Catalog, Session, new NoticeLog(), new QueuedUiDispatcher(), new ManualDebouncer());
    }

    public MemorySession Session { get; }

    public PageCatalog Catalog { get; }

    public Navigator Navigator { get; }

    public List<FakePage> Made { get; } = [];

    // A page whose factory throws, as a broken page would.
    public PageKey? Broken { get; set; }

    public IReadOnlyList<FakePage> MadeFor(PageKey key) => [.. Made.Where(page => page.Key == key)];

    // The page of that key made last.
    public FakePage Page(PageKey key) => Made.Last(page => page.Key == key);

    private FakePage Make(PageKey key)
    {
        if (key == Broken)
        {
            throw new InvalidOperationException($"The {key} page is broken.");
        }

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
