using ManuscriptPipeline.Models;
using ManuscriptPipeline.Services;
using PaperRoute.ViewModels;
using PaperRoute.ViewModels.Board;
using PaperRoute.ViewModels.Session;

namespace PaperRoute.Desktop.Tests.Board;

// A library held in memory: no folder, no repository, so a Board rule test
// touches no storage at all. Tests change the list and raise LibraryChanged,
// as today's pages change Form1's list and render.
internal sealed class MemorySession : ILibrarySession
{
    private AuthorLibraryData? _authorLibrary;

    public MemorySession(IEnumerable<Manuscript> manuscripts, DateTime today, AppSettings? settings = null, AuthorLibraryData? authorLibrary = null)
    {
        Library = manuscripts.ToList();
        Today = today.Date;
        Settings = settings ?? new AppSettings();
        AuthorLibrary = authorLibrary;
    }

    public List<Manuscript> Library { get; }

    public LibraryKind Kind => LibraryKind.Example;

    public LibrarySnapshot? Snapshot => null;

    public bool IsReadOnly => true;

    public DateTime Today { get; set; }

    public IReadOnlyList<Manuscript> Manuscripts => Library;

    public AuthorLibraryData? AuthorLibrary
    {
        get => _authorLibrary;
        set
        {
            _authorLibrary = value;
            SearchIndex = new AuthorLibrarySearchIndex(value);
        }
    }

    public AuthorLibrarySearchIndex SearchIndex { get; private set; } = new(null);

    public AppSettings Settings { get; set; }

    public string StartupNotice => string.Empty;

    public event EventHandler? LibraryChanged;

    public void RaiseChanged() => LibraryChanged?.Invoke(this, EventArgs.Empty);

    public Task<bool> LoadAsync(CancellationToken cancel)
    {
        RaiseChanged();
        return Task.FromResult(true);
    }

    public bool WaitForWork() => false;

    public void Dispose()
    {
        LibraryChanged = null;
    }
}

// The search wait, fired by hand (d1 3.4): no test waits 200 ms.
internal sealed class ManualDebouncer : IDebouncer
{
    private Action? _pending;

    public TimeSpan Delay { get; private set; }

    public int Restarts { get; private set; }

    public bool IsPending => _pending is not null;

    public void Restart(TimeSpan delay, Action action)
    {
        Delay = delay;
        Restarts++;
        _pending = action;
    }

    public void Stop() => _pending = null;

    // Runs the pending action, as the timer's tick would. False if none.
    public bool Fire()
    {
        Action? action = _pending;
        _pending = null;
        action?.Invoke();
        return action is not null;
    }
}

// Manuscripts for the rule tests, with dates around a fixed day.
internal static class Papers
{
    public static readonly DateTime Today = new(2026, 10, 16);

    public static Manuscript Paper(
        string title,
        PaperStage stage = PaperStage.Draft,
        ManuscriptLocation location = ManuscriptLocation.Pipeline,
        string journal = "Journal",
        int stageDaysAgo = 10) => new()
        {
            Title = title,
            CurrentStage = stage,
            Location = location,
            TargetJournal = journal,
            StageEnteredDate = Today.AddDays(-stageDaysAgo),
        };

    public static JournalSubmission Submission(int daysAgo, params (EditorialDecision Decision, int DaysAgo, int? DeadlineInDays)[] decisions)
    {
        var submission = new JournalSubmission { JournalName = "Journal", SubmittedDate = Today.AddDays(-daysAgo) };
        foreach ((EditorialDecision decision, int decidedDaysAgo, int? deadline) in decisions)
        {
            submission.Decisions.Add(new EditorialDecisionEvent
            {
                Decision = decision,
                DecisionDate = Today.AddDays(-decidedDaysAgo),
                RevisionDeadline = deadline.HasValue ? Today.AddDays(deadline.Value) : null,
            });
        }

        return submission;
    }

    public static Manuscript With(this Manuscript manuscript, params JournalSubmission[] submissions)
    {
        manuscript.Submissions.AddRange(submissions);
        return manuscript;
    }

    // A Pipeline revision whose deadline is days away (negative: past).
    public static Manuscript Revision(string title, int deadlineInDays, ManuscriptLocation location = ManuscriptLocation.Pipeline) =>
        Paper(title, PaperStage.Revision, location)
            .With(Submission(60, (EditorialDecision.MajorRevision, 30, deadlineInDays)));

    // Under review, submitted days ago, no decision.
    public static Manuscript Waiting(string title, int submittedDaysAgo, ManuscriptLocation location = ManuscriptLocation.Pipeline) =>
        Paper(title, PaperStage.UnderReview, location).With(Submission(submittedDaysAgo));

    // A draft whose latest submission was rejected days ago.
    public static Manuscript Rejected(string title, int rejectedDaysAgo, ManuscriptLocation location = ManuscriptLocation.Pipeline) =>
        Paper(title, PaperStage.Draft, location).With(Submission(rejectedDaysAgo + 40, (EditorialDecision.Rejected, rejectedDaysAgo, null)));

    // A draft with this many submissions, each rejected long ago.
    public static Manuscript RejectedTimes(string title, int rejections, ManuscriptLocation location = ManuscriptLocation.Pipeline, string journal = "Journal")
    {
        Manuscript manuscript = Paper(title, PaperStage.Draft, location, journal);
        for (int index = 0; index < rejections; index++)
        {
            int submitted = 400 - (index * 100);
            manuscript.Submissions.Add(Submission(submitted, (EditorialDecision.Rejected, submitted - 50, null)));
        }

        return manuscript;
    }
}

// A Board over a memory session, with its hand-fired search wait and its
// posted work run by hand.
internal sealed class BoardHarness
{
    public BoardHarness(IEnumerable<Manuscript> manuscripts, AppSettings? settings = null, AuthorLibraryData? authorLibrary = null, DateTime? today = null)
    {
        Session = new MemorySession(manuscripts, today ?? Papers.Today, settings, authorLibrary);
        Debouncer = new ManualDebouncer();
        Posts = new QueuedUiDispatcher();
        Board = new BoardViewModel(Session, Debouncer, Posts);
        Board.Render();
    }

    public MemorySession Session { get; }

    public ManualDebouncer Debouncer { get; }

    public QueuedUiDispatcher Posts { get; }

    public BoardViewModel Board { get; }

    public ShelfViewModel Pipeline => Board.Shelves[0];

    public ShelfViewModel Published => Board.Shelves[1];

    public ShelfViewModel FileDrawer => Board.Shelves[2];

    // Types a search and lets its wait end.
    public void Search(string text)
    {
        Board.SearchText = text;
        if (!Debouncer.Fire())
        {
            // The same text again starts no wait; render as a tick would.
            Board.Render();
        }
    }

    public ChipViewModel Chip(AttentionFilter filter) => Board.Chips.Single(chip => chip.Filter == filter);

    public CardViewModel Card(string title) =>
        Board.Shelves.SelectMany(shelf => shelf.Cards).Single(card => card.Title == title);

    public static List<string> Titles(ShelfViewModel shelf) => shelf.Cards.Select(card => card.Title).ToList();
}

// Every Board test checks, as it ends, that no default storage root answered
// (d1 1.3 and 6.1); the assembly's storage guard points them all at a
// temporary folder before any test runs.
public abstract class StorageCheckedTests
{
    private int _usesAtStart;

    [TestInitialize]
    public void RecordDefaultRootUses()
    {
        Assert.IsNotNull(StorageEnvironment.RootOverride, "The storage guard has set a temporary root before any test.");
        _usesAtStart = StorageEnvironment.DefaultRootUses;
    }

    [TestCleanup]
    public void NoDefaultRootWasUsed()
    {
        Assert.AreEqual(_usesAtStart, StorageEnvironment.DefaultRootUses,
            "A default storage root was resolved: the test may have reached the user's own library.");
        Assert.AreEqual(0, StorageEnvironment.DefaultRootUses, "No default storage root has been resolved in this run.");
    }
}
