using PaperRoute.ViewModels.Session;

namespace PaperRoute.ViewModels.Shell;

// The navigator (shell design 2.1, 3.1 rows 8 and 16 to 22): the page shown,
// the two history stacks and the leave guards, as a pure class with no
// window. Pages come from the catalog: a kept page is made once and shown
// again; any other is made afresh on each visit. Called on the interface
// thread; an await on a leave guard comes back to it.
public sealed class Navigator : INavigator
{
    private readonly PageCatalog _catalog;
    private readonly ILibrarySession _session;
    private readonly Dictionary<PageKey, IPage> _kept = [];
    private readonly Stack<PageRequest> _back = new();
    private readonly Stack<PageRequest> _forward = new();

    // A move is waiting on a leave guard; another is refused meanwhile.
    private bool _moving;

    public Navigator(PageCatalog catalog, ILibrarySession session, IInteractions interactions, IUiDispatcher dispatcher, IDebouncer debouncer)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(session);
        _catalog = catalog;
        _session = session;
        Services = new ShellServices(session, interactions, this, dispatcher, debouncer);
    }

    private enum Move
    {
        Refused,
        Stayed,
        Moved,
    }

    // What each page's factory is given.
    public ShellServices Services { get; }

    public PageRequest? Current { get; private set; }

    public IPage? CurrentPage { get; private set; }

    public IReadOnlyList<PageRequest> BackHistory => [.. _back];

    public IReadOnlyList<PageRequest> ForwardHistory => [.. _forward];

    public PageKey Origin { get; private set; } = PageKey.Board;

    public PageRequest? BackTarget => _back.FirstOrDefault(IsHeld);

    public async Task<bool> GoAsync(PageRequest request, bool recordHistory = true)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (_moving)
        {
            return false;
        }

        _moving = true;
        try
        {
            PageRequest? left = Current;
            Move move = await MoveAsync(request);
            if (move == Move.Moved && recordHistory && left is not null)
            {
                _back.Push(left);
                _forward.Clear();
            }

            return move != Move.Refused;
        }
        finally
        {
            _moving = false;
        }
    }

    public Task<bool> GoBackAsync() => WalkAsync(_back, _forward);

    public Task<bool> GoForwardAsync() => WalkAsync(_forward, _back);

    // Back or Forward: the first entry still held is taken, the entries
    // skipped on the way (manuscripts since deleted) go with it, and the
    // page left goes on the other stack. A refusal leaves both as they were.
    // Today's GoBack, GoForward and PopExisting (Form1.Shell.vb:777-821).
    private async Task<bool> WalkAsync(Stack<PageRequest> from, Stack<PageRequest> to)
    {
        if (_moving)
        {
            return false;
        }

        int skipped = 0;
        PageRequest? target = null;
        foreach (PageRequest entry in from)
        {
            if (IsHeld(entry))
            {
                target = entry;
                break;
            }

            skipped++;
        }

        if (target is null)
        {
            return false;
        }

        _moving = true;
        try
        {
            PageRequest? left = Current;
            if (await MoveAsync(target) == Move.Refused)
            {
                return false;
            }

            for (int i = 0; i <= skipped; i++)
            {
                from.Pop();
            }

            if (left is not null)
            {
                to.Push(left);
            }

            return true;
        }
        finally
        {
            _moving = false;
        }
    }

    // One switch, history aside. The page shown, asked again, only takes a
    // new section (3.1 row 17); otherwise the page shown is asked whether it
    // may be left, then hidden, and the page asked for is shown.
    private async Task<Move> MoveAsync(PageRequest request)
    {
        PageCatalogRow row = _catalog.Find(request.Page);
        if (Current is { } here && CurrentPage is { } shown && request.SamePlace(here))
        {
            if (request != here)
            {
                Current = request;
                shown.Shown(request);
            }

            return Move.Stayed;
        }

        if (CurrentPage is { } leaving)
        {
            if (!await leaving.CanLeaveAsync())
            {
                return Move.Refused;
            }

            leaving.Hidden();
        }

        IPage page = PageFor(row);
        if (request.Page == PageKey.Manuscript && Current is { } from && from.Page != PageKey.Manuscript)
        {
            Origin = from.Page;
        }

        Current = request;
        CurrentPage = page;
        page.Shown(request);
        return Move.Moved;
    }

    private IPage PageFor(PageCatalogRow row)
    {
        if (row.Kept && _kept.TryGetValue(row.Key, out IPage? kept))
        {
            return kept;
        }

        IPage page = row.Make(Services)
            ?? throw new InvalidOperationException($"The catalog's factory for {row.Key} made no page.");
        if (row.Kept)
        {
            _kept[row.Key] = page;
        }

        return page;
    }

    // A history entry can still be shown: any page but a manuscript the
    // library no longer holds (3.1 row 20).
    private bool IsHeld(PageRequest entry) =>
        entry.Page != PageKey.Manuscript
        || _session.Manuscripts.Any(manuscript => manuscript is not null && manuscript.Id == entry.ManuscriptId);
}
