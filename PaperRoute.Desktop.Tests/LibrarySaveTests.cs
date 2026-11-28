using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using ManuscriptPipeline.Models;
using ManuscriptPipeline.Services;
using PaperRoute.Desktop.Tests.Board;
using PaperRoute.TestFixtures;
using PaperRoute.ViewModels;
using PaperRoute.ViewModels.Board;
using PaperRoute.ViewModels.Session;

namespace PaperRoute.Desktop.Tests;

// The session's library save (shell design 2.6). Each test builds a
// DiskLibrarySession over a temporary folder of its own, written by the test
// fixture: no test reaches a person's library, and every fault is made on a
// file the test made there. A save that must be caught while it runs is held
// on its worker (HeldWork), which signals that it has started; nothing here
// waits a fixed time or races the thread pool.
[TestClass]
public sealed class LibrarySaveTests
{
    private string _root = string.Empty;

    private string DataDirectory => Path.Combine(_root, "data");

    private string ManagedDirectory => Path.Combine(_root, "library");

    private string LibraryFile => Path.Combine(DataDirectory, "manuscripts.json");

    [TestInitialize]
    public void CreateRoot()
    {
        _root = Path.Combine(Path.GetTempPath(), "PaperRoute-TrialSave-" + Guid.NewGuid().ToString("N"));
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
    }

    // A save writes the library file and nothing else (no schema marker, no
    // settings, no author library), and a new session reads back what was
    // saved.
    [TestMethod]
    public async Task ASaveWritesTheLibraryAndReloadsTheSame()
    {
        LargeLibraryFixture.WriteExample(_root);
        using DiskLibrarySession session = await LoadedSession();
        IReadOnlyDictionary<string, string> before = FileState(_root);
        session.Manuscripts[0].Title = "Saved by the session";
        int states = 0;
        int changes = 0;
        session.StateChanged += (_, _) => states++;
        session.LibraryChanged += (_, _) => changes++;

        SaveOutcome outcome = await session.SaveAsync(CancellationToken.None);

        Assert.AreEqual(SaveOutcomeKind.Saved, outcome.Kind, outcome.Message);
        Assert.AreEqual(string.Empty, outcome.Warning);
        Assert.IsFalse(session.SaveFailed);
        Assert.IsFalse(session.IsWorking);
        Assert.AreEqual(2, states, "StateChanged is raised as the save starts and once it has ended.");
        Assert.AreEqual(1, changes, "LibraryChanged is raised once after the save.");
        string[] changed = Changed(before, FileState(_root));
        CollectionAssert.Contains(changed, "data/manuscripts.json", "The save did not write manuscripts.json.");
        CollectionAssert.IsSubsetOf(changed, new[] { "data/manuscripts.json", "data/manuscripts.bak" },
            "The save changed other files than the library file and its backup: " + string.Join(", ", changed));

        using DiskLibrarySession again = await LoadedSession();
        CollectionAssert.AreEqual(Facts(session.Manuscripts), Facts(again.Manuscripts), "A new session does not read back the library as it was saved.");
        Assert.AreEqual("Saved by the session", again.Manuscripts[0].Title);
    }

    // The save runs on a worker, never on the caller's thread, and publishes
    // on the caller's context: IsWorking ends, StateChanged and LibraryChanged
    // are raised and the task completes only when that context runs, though
    // the worker has written the library by then.
    [TestMethod]
    public async Task ASaveRunsOffTheCallersThreadAndPublishesOnIt()
    {
        LargeLibraryFixture.WriteExample(_root);
        using DiskLibrarySession session = await LoadedSession();
        using var held = new HeldWork(session);
        var ui = new InterfaceThread();
        int caller = Environment.CurrentManagedThreadId;
        var raised = new List<string>();
        session.StateChanged += (_, _) => raised.Add("StateChanged on " + Environment.CurrentManagedThreadId);
        session.LibraryChanged += (_, _) => raised.Add("LibraryChanged on " + Environment.CurrentManagedThreadId);
        string unsaved = Hash(LibraryFile);
        session.Manuscripts[0].Title = "Saved on a worker";

        Task<SaveOutcome> save = ui.Enter(() => session.SaveAsync(CancellationToken.None));
        held.WaitUntilHeld();

        Assert.AreNotEqual(caller, held.WorkerThread, "The save ran on the caller's thread.");
        Assert.IsTrue(session.IsWorking, "IsWorking is true while the save runs.");
        CollectionAssert.AreEqual(new[] { "StateChanged on " + caller }, raised, "StateChanged is raised on the caller's thread as the save starts.");

        held.Release();
        WaitForTheWorker(session);
        Assert.AreNotEqual(unsaved, Hash(LibraryFile), "The worker did not write the library.");
        Assert.IsFalse(save.IsCompleted, "The save completed before the caller's context ran: it published on another thread.");
        Assert.IsTrue(session.IsWorking, "IsWorking ended before the save published on the caller's context.");
        Assert.HasCount(1, raised, "The save published before the caller's context ran: " + string.Join("; ", raised));

        Assert.IsGreaterThan(0, ui.RunPosted(TimeSpan.FromSeconds(30)), "The save posted nothing to the caller's context.");
        Assert.IsTrue(save.IsCompleted, "The save had not completed when the caller's context had run.");
        Assert.AreEqual(SaveOutcomeKind.Saved, (await save).Kind);
        Assert.IsFalse(session.IsWorking);
        CollectionAssert.AreEqual(
            new[] { "StateChanged on " + caller, "StateChanged on " + caller, "LibraryChanged on " + caller },
            raised,
            "The save's end was not published on the caller's thread.");
    }

    // One piece of library work at a time (shell design 2.6 rule 3): while a
    // save runs, a load is refused at once, reads nothing and publishes
    // nothing; so is a second save.
    [TestMethod]
    public async Task ALoadWhileASaveRunsIsRefused()
    {
        LargeLibraryFixture.WriteExample(_root);
        using DiskLibrarySession session = await LoadedSession();
        using var held = new HeldWork(session);
        int changes = 0;
        session.LibraryChanged += (_, _) => changes++;
        Task<SaveOutcome> save = session.SaveAsync(CancellationToken.None);
        held.WaitUntilHeld();

        Task<bool> load = session.LoadAsync(CancellationToken.None);
        Task<SaveOutcome> second = session.SaveAsync(CancellationToken.None);

        Assert.IsTrue(load.IsCompleted, "A load refused while a save runs returns at once.");
        Assert.IsFalse(await load, "A load while a save runs is refused.");
        Assert.IsTrue(second.IsCompleted, "A save refused while another runs returns at once.");
        Assert.AreEqual(SaveOutcomeKind.Refused, (await second).Kind, "A second save while one runs is refused.");
        Assert.AreEqual(1, held.Entries, "A refused load or save started a worker of its own.");
        Assert.AreEqual(0, changes, "A refused load published.");

        held.Release();
        Assert.AreEqual(SaveOutcomeKind.Saved, (await save).Kind);
        Assert.IsTrue(await session.LoadAsync(CancellationToken.None), "Once the save has ended, a load runs.");
    }

    // The other way round: while a load runs, a save is refused at once and
    // writes nothing. The session has loaded once already, so the refusal is
    // the running load's, not the want of a library to save: once that load
    // ends, the same save goes through.
    [TestMethod]
    public async Task ASaveWhileALoadRunsIsRefused()
    {
        LargeLibraryFixture.WriteExample(_root);
        using DiskLibrarySession session = await LoadedSession();
        IReadOnlyDictionary<string, string> before = FileState(_root);
        using var held = new HeldWork(session);
        Task<bool> load = session.LoadAsync(CancellationToken.None);
        held.WaitUntilHeld();
        int states = 0;
        session.StateChanged += (_, _) => states++;

        Task<SaveOutcome> save = session.SaveAsync(CancellationToken.None);

        Assert.IsTrue(save.IsCompleted, "A save refused while a load runs returns at once.");
        Assert.AreEqual(SaveOutcomeKind.Refused, (await save).Kind, "A save while a load runs is refused.");
        Assert.AreEqual(0, states, "A refused save changed the session's state.");
        Assert.IsFalse(session.IsWorking);
        Assert.IsFalse(session.SaveFailed, "A refused save is not a failure.");
        CollectionAssert.AreEquivalent(before.ToArray(), FileState(_root).ToArray(), "A refused save wrote something.");

        held.Release();
        Assert.IsTrue(await load);
        Assert.AreEqual(SaveOutcomeKind.Saved, (await session.SaveAsync(CancellationToken.None)).Kind, "Once the load has ended, the save runs.");
    }

    // A failure (here a folder where the save's temporary file must go) sets
    // SaveFailed, carries the message and leaves the library file as it was;
    // a save that succeeds clears the flag.
    [TestMethod]
    public async Task AFailedSaveSetsTheFlagAndAGoodSaveClearsIt()
    {
        LargeLibraryFixture.WriteExample(_root);
        using DiskLibrarySession session = await LoadedSession();
        string blocker = BlockTheSave();
        string unsaved = Hash(LibraryFile);
        int states = 0;
        session.StateChanged += (_, _) => states++;

        SaveOutcome failed = await session.SaveAsync(CancellationToken.None);

        Assert.AreEqual(SaveOutcomeKind.Failed, failed.Kind);
        Assert.IsFalse(string.IsNullOrWhiteSpace(failed.Message), "A failed save carries the exception's message.");
        Assert.IsTrue(session.SaveFailed, "A failed save did not set SaveFailed.");
        Assert.IsFalse(session.IsWorking);
        Assert.AreEqual(2, states, "StateChanged is raised as the save starts and once it has ended.");
        Assert.AreEqual(unsaved, Hash(LibraryFile), "A failed save changed manuscripts.json.");

        Directory.Delete(blocker);
        SaveOutcome good = await session.SaveAsync(CancellationToken.None);

        Assert.AreEqual(SaveOutcomeKind.Saved, good.Kind, good.Message);
        Assert.IsFalse(session.SaveFailed, "A good save did not clear SaveFailed.");
        Assert.AreEqual(4, states);
    }

    // Only a good save clears the flag: a refused save, a load and another
    // failure leave it set.
    [TestMethod]
    public async Task AFailedSaveStaysUntilAGoodSave()
    {
        LargeLibraryFixture.WriteExample(_root);
        using DiskLibrarySession session = await LoadedSession();
        string blocker = BlockTheSave();
        Assert.AreEqual(SaveOutcomeKind.Failed, (await session.SaveAsync(CancellationToken.None)).Kind);

        using (var held = new HeldWork(session))
        {
            Task<bool> load = session.LoadAsync(CancellationToken.None);
            held.WaitUntilHeld();
            Assert.AreEqual(SaveOutcomeKind.Refused, (await session.SaveAsync(CancellationToken.None)).Kind);
            Assert.IsTrue(session.SaveFailed, "A refused save cleared SaveFailed.");
            held.Release();
            Assert.IsTrue(await load);
        }

        Assert.IsTrue(session.SaveFailed, "A load cleared SaveFailed.");
        Assert.AreEqual(SaveOutcomeKind.Failed, (await session.SaveAsync(CancellationToken.None)).Kind);
        Assert.IsTrue(session.SaveFailed, "A second failure cleared SaveFailed.");

        Directory.Delete(blocker);
        Assert.AreEqual(SaveOutcomeKind.Saved, (await session.SaveAsync(CancellationToken.None)).Kind);
        Assert.IsFalse(session.SaveFailed, "A good save did not clear SaveFailed.");
    }

    // A snapshot writes nothing (critic N8): its save is refused, changes
    // nothing on disk, in the copy or the folder copied, and raises nothing.
    [TestMethod]
    public async Task ASnapshotRefusesToSave()
    {
        string source = Path.Combine(_root, "Lab library");
        string sessionFolder = Path.Combine(_root, "session");
        LargeLibraryFixture.WriteExample(source);
        SnapshotCopy copy = ReadOnlySnapshot.Copy(source, Path.Combine(sessionFolder, "current-data"), sessionFolder);
        using var session = new DiskLibrarySession(copy, Path.Combine(sessionFolder, "managed-library"), LargeLibraryFixture.Today);
        Assert.IsTrue(await session.LoadAsync(CancellationToken.None));
        IReadOnlyDictionary<string, string> before = FileState(_root);
        session.Manuscripts[0].Title = "Changed in a snapshot";
        int states = 0;
        int changes = 0;
        session.StateChanged += (_, _) => states++;
        session.LibraryChanged += (_, _) => changes++;

        SaveOutcome outcome = await session.SaveAsync(CancellationToken.None);

        Assert.IsTrue(session.IsReadOnly);
        Assert.AreEqual(SaveOutcomeKind.Refused, outcome.Kind, "A snapshot's save is refused.");
        Assert.AreEqual(0, states + changes, "A snapshot's refused save raised an event.");
        Assert.IsFalse(session.SaveFailed);
        CollectionAssert.AreEquivalent(before.ToArray(), FileState(_root).ToArray(), "A snapshot's save wrote a file.");
    }

    // Safety, by a list of the kinds that may save: every value of LibraryKind
    // but the example cannot be saved, and so cannot a kind the list does not
    // know, as one added later would be until it is allowed on purpose. A
    // session of that unknown kind refuses both save members and writes
    // nothing.
    [TestMethod]
    public async Task OnlyTheExampleMaySave()
    {
        LibraryKind[] kinds = Enum.GetValues<LibraryKind>();
        foreach (LibraryKind kind in kinds)
        {
            Assert.AreEqual(kind != LibraryKind.Example, DiskLibrarySession.IsReadOnlyKind(kind),
                $"{kind}: only the example may be saved.");
        }

        var later = (LibraryKind)(kinds.Max(kind => (int)kind) + 1);
        Assert.IsTrue(DiskLibrarySession.IsReadOnlyKind(later),
            "A kind the session does not know can be saved: the session lists the kinds that may not save, not those that may.");

        LargeLibraryFixture.WriteExample(_root);
        using var session = new DiskLibrarySession(DataDirectory, ManagedDirectory, _root, later, LargeLibraryFixture.Today);
        Assert.IsTrue(await session.LoadAsync(CancellationToken.None));
        IReadOnlyDictionary<string, string> before = FileState(_root);
        session.Manuscripts[0].Title = "Saved by an unknown kind";
        session.Settings.NavigationCollapsed = !session.Settings.NavigationCollapsed;

        Assert.IsTrue(session.IsReadOnly);
        Assert.AreEqual(SaveOutcomeKind.Refused, (await session.SaveAsync(CancellationToken.None)).Kind, "A library of an unknown kind was saved.");
        Assert.IsFalse(await session.SaveSettingsAsync(), "The settings of an unknown kind were saved.");
        CollectionAssert.AreEquivalent(before.ToArray(), FileState(_root).ToArray(), "A library of an unknown kind wrote a file.");
    }

    // A library file the load could not read is never saved over: with no
    // repository from a good load, the save is refused.
    [TestMethod]
    public async Task ASaveAfterALoadThatFailedIsRefused()
    {
        Directory.CreateDirectory(DataDirectory);
        await File.WriteAllTextAsync(LibraryFile, "{ this is not a library");
        using DiskLibrarySession session = await LoadedSession();
        Assert.AreEqual("Library could not be loaded safely.", session.StartupNotice);
        IReadOnlyDictionary<string, string> before = FileState(_root);

        Assert.AreEqual(SaveOutcomeKind.Refused, (await session.SaveAsync(CancellationToken.None)).Kind, "A library that could not be read was saved over.");
        CollectionAssert.AreEquivalent(before.ToArray(), FileState(_root).ToArray(), "The refused save wrote a file.");
    }

    // The same rule in the one place it can be broken: a session that read a
    // good library, and then reloaded one it could not read (the backup gone,
    // so nothing rebuilds it), keeps no repository from the first load. Its
    // save is refused, and the damaged file stays byte for byte as it was
    // rather than being replaced and turned into the backup.
    [TestMethod]
    public async Task ASaveAfterAReloadThatFailedIsRefused()
    {
        LargeLibraryFixture.Create(_root, 3);
        using DiskLibrarySession session = await LoadedSession();
        Assert.IsNotEmpty(session.Manuscripts, "The fixture's library did not load.");
        const string damaged = "{ this is not a library";
        await File.WriteAllTextAsync(LibraryFile, damaged);
        File.Delete(Path.Combine(DataDirectory, "manuscripts.bak"));

        Assert.IsTrue(await session.LoadAsync(CancellationToken.None), "The reload was refused.");
        Assert.AreEqual("Library could not be loaded safely.", session.StartupNotice);
        IReadOnlyDictionary<string, string> before = FileState(_root);

        Assert.AreEqual(SaveOutcomeKind.Refused, (await session.SaveAsync(CancellationToken.None)).Kind,
            "A library a reload could not read was saved over through the repository of the load before it.");
        CollectionAssert.AreEquivalent(before.ToArray(), FileState(_root).ToArray(), "The refused save wrote a file.");
        Assert.AreEqual(damaged, await File.ReadAllTextAsync(LibraryFile), "The library file the reload could not read was changed.");
    }

    // A token cancelled before the call throws, and nothing starts.
    [TestMethod]
    public async Task ACancelledSaveWritesNothing()
    {
        LargeLibraryFixture.WriteExample(_root);
        using DiskLibrarySession session = await LoadedSession();
        IReadOnlyDictionary<string, string> before = FileState(_root);
        int states = 0;
        session.StateChanged += (_, _) => states++;
        using var cancel = new CancellationTokenSource();
        await cancel.CancelAsync();

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => session.SaveAsync(cancel.Token));

        Assert.AreEqual(0, states);
        Assert.IsFalse(session.IsWorking);
        CollectionAssert.AreEquivalent(before.ToArray(), FileState(_root).ToArray(), "A cancelled save wrote a file.");
    }

    // A save that goes through but leaves a removed version's folder in place,
    // because something holds it, carries the repository's warning naming the
    // folder; it is not a failure. The next save, once nothing holds the
    // folder, moves it and carries no warning.
    [TestMethod]
    public async Task TheSaveWarningComesBack()
    {
        LargeLibraryFixture.Create(_root, 3);
        using DiskLibrarySession session = await LoadedSession();
        (Manuscript manuscript, ManuscriptVersion draft, string folder) = FirstDraft(session);
        ManuscriptVersionService.DeleteVersion(manuscript, draft.Id);

        SaveOutcome kept;
        using (HoldInPlace(folder, draft.LocalFilePath))
        {
            kept = await session.SaveAsync(CancellationToken.None);
        }

        Assert.AreEqual(SaveOutcomeKind.Saved, kept.Kind, kept.Message);
        StringAssert.Contains(kept.Warning, folder, "The save's warning does not name the folder left in place.");
        Assert.IsFalse(session.SaveFailed, "A save with a warning went through: it is not a failure.");
        Assert.IsTrue(Directory.Exists(folder), "The folder held in place was moved.");

        SaveOutcome next = await session.SaveAsync(CancellationToken.None);

        Assert.AreEqual(SaveOutcomeKind.Saved, next.Kind, next.Message);
        Assert.AreEqual(string.Empty, next.Warning, "The next save still warns.");
        Assert.IsFalse(Directory.Exists(folder), "The next save did not move the folder once nothing held it.");
    }

    // Save through the repository that loaded (shell design 2.6 rule 2): it
    // stages the folders of removed versions against what its own load
    // referenced (ManuscriptRepository.vb:1105-1131). A save through a new
    // repository has no such baseline and stages nothing, so the removed
    // version's folder would stay on disk for good. The folder is moved to
    // the managed folder's removed area, never deleted.
    [TestMethod]
    public async Task ASaveAfterRemovingAVersionRemovesItsManagedFolder()
    {
        LargeLibraryFixture.Create(_root, 3);
        using DiskLibrarySession session = await LoadedSession();
        (Manuscript manuscript, ManuscriptVersion draft, string folder) = FirstDraft(session);
        string file = Path.GetFileName(draft.LocalFilePath);
        Assert.IsTrue(Directory.Exists(folder), "The fixture wrote no folder for the version.");
        ManuscriptVersionService.DeleteVersion(manuscript, draft.Id);

        Assert.AreEqual(SaveOutcomeKind.Saved, (await session.SaveAsync(CancellationToken.None)).Kind);

        Assert.IsFalse(Directory.Exists(folder),
            "The removed version's managed folder is still there: the save did not go through the repository that loaded the library.");
        string removed = Path.Combine(Path.GetFullPath(ManagedDirectory), "removed");
        Assert.IsTrue(Directory.Exists(removed) && Directory.GetFiles(removed, file, SearchOption.AllDirectories).Length > 0,
            "The removed version's file was not kept in the managed folder's removed area.");
        using DiskLibrarySession again = await LoadedSession();
        Assert.IsFalse(again.Manuscripts.Single(each => each.Id == manuscript.Id).Versions.Any(version => version.Id == draft.Id),
            "The saved library still holds the removed version.");
    }

    // The Board's render guard (shell design 2.6 rule 4): a search whose wait
    // ends while a save runs does not render, so nothing reads the library
    // the worker is normalising; the Board renders, with that search, on the
    // LibraryChanged the session raises once the save has ended.
    [TestMethod]
    public async Task ABoardSearchDuringASaveRendersAfterIt()
    {
        LargeLibraryFixture.WriteExample(_root);
        using DiskLibrarySession session = await LoadedSession();
        var search = new ManualDebouncer();
        var board = new BoardViewModel(session, search, new QueuedUiDispatcher());
        board.Render();
        int renders = board.RenderCount;
        string title = session.Manuscripts.First(manuscript => manuscript.Location == ManuscriptLocation.Pipeline).Title;
        using var held = new HeldWork(session);
        var ui = new InterfaceThread();

        Task<SaveOutcome> save = ui.Enter(() => session.SaveAsync(CancellationToken.None));
        held.WaitUntilHeld();
        board.SearchText = title;
        Assert.IsTrue(search.Fire(), "The search's wait was not started.");

        Assert.AreEqual(renders, board.RenderCount, "The Board rendered while the save ran: it read the library the worker was normalising.");

        held.Release();
        Assert.IsGreaterThan(0, ui.RunPosted(TimeSpan.FromSeconds(30)), "The save posted nothing to the caller's context.");
        Assert.AreEqual(SaveOutcomeKind.Saved, (await save).Kind);
        Assert.AreEqual(renders + 1, board.RenderCount, "The Board did not render once after the save.");
        Assert.IsTrue(board.HasActiveFilters, "The render after the save did not apply the search.");
        CollectionAssert.Contains(board.Shelves[0].Cards.Select(card => card.Title).ToList(), title,
            "The render after the save shows no card for the search.");
    }

    // A subscriber that throws as the save starts does not end the save: it
    // stays IsWorking, refusing a load and a second save, until its worker has
    // ended, and only then does the task fault, with that subscriber's own
    // exception. SaveFailed says what the worker did (it saved), both end
    // events are raised once, and the next save runs.
    [TestMethod]
    public async Task ASubscriberThatThrowsAsASaveStartsDoesNotEndIt()
    {
        LargeLibraryFixture.WriteExample(_root);
        using DiskLibrarySession session = await LoadedSession();
        using var held = new HeldWork(session);
        var thrown = new InvalidOperationException("A subscriber of StateChanged threw.");
        int states = 0;
        int changes = 0;
        session.StateChanged += (_, _) =>
        {
            if (++states == 1)
            {
                throw thrown;
            }
        };
        session.LibraryChanged += (_, _) => changes++;
        session.Manuscripts[0].Title = "Saved past a subscriber that threw";

        Task<SaveOutcome> save = session.SaveAsync(CancellationToken.None);
        held.WaitUntilHeld();

        Assert.IsFalse(save.IsCompleted, "The save's task ended while its worker was held: the subscriber's exception ended the save before its worker.");
        Assert.IsTrue(session.IsWorking, "IsWorking is true while the worker runs, though a subscriber threw.");
        Task<bool> load = session.LoadAsync(CancellationToken.None);
        Task<SaveOutcome> second = session.SaveAsync(CancellationToken.None);
        Assert.IsTrue(load.IsCompleted && !await load, "A load while the save's worker runs is refused, though a subscriber threw.");
        Assert.IsTrue(second.IsCompleted, "A save refused while another runs returns at once.");
        Assert.AreEqual(SaveOutcomeKind.Refused, (await second).Kind, "A second save while the worker runs is refused, though a subscriber threw.");

        held.Release();
        InvalidOperationException caught = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => save);

        Assert.AreSame(thrown, caught, "The save's task did not fault with the subscriber's own exception.");
        Assert.IsFalse(session.IsWorking, "The save stayed IsWorking after its worker had ended.");
        Assert.IsFalse(session.SaveFailed, "The subscriber's exception was taken for a failed save.");
        Assert.AreEqual(2, states, "StateChanged is raised once more as the save ends.");
        Assert.AreEqual(1, changes, "LibraryChanged is raised once as the save ends.");
        using (DiskLibrarySession again = await LoadedSession())
        {
            Assert.AreEqual("Saved past a subscriber that threw", again.Manuscripts[0].Title, "The library file does not hold what was saved.");
        }

        Assert.AreEqual(SaveOutcomeKind.Saved, (await session.SaveAsync(CancellationToken.None)).Kind, "The save after it did not run.");
    }

    // The other side of the same rule: when a subscriber throws as the save
    // starts and the worker's save fails, SaveFailed says so once the save has
    // ended. A subscriber's exception neither sets nor clears it.
    [TestMethod]
    public async Task ASubscriberThatThrowsDoesNotHideAFailedSave()
    {
        LargeLibraryFixture.WriteExample(_root);
        using DiskLibrarySession session = await LoadedSession();
        _ = BlockTheSave();
        var thrown = new InvalidOperationException("A subscriber of StateChanged threw.");
        int states = 0;
        session.StateChanged += (_, _) =>
        {
            if (++states == 1)
            {
                throw thrown;
            }
        };

        InvalidOperationException caught = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => session.SaveAsync(CancellationToken.None));
        WaitForTheWorker(session);

        Assert.IsTrue(session.SaveFailed, "The worker's save failed, but SaveFailed does not say so after a subscriber threw.");
        Assert.IsFalse(session.IsWorking);
        Assert.AreSame(thrown, caught);
    }

    // A subscriber that throws at the StateChanged raised as the save ends
    // does not stop LibraryChanged: the Board's render, held while the save
    // ran, is released, and the task faults with that exception once the save
    // has ended, IsWorking false and SaveFailed false.
    [TestMethod]
    public async Task ASubscriberThatThrowsAsASaveEndsDoesNotStopLibraryChanged()
    {
        LargeLibraryFixture.WriteExample(_root);
        using DiskLibrarySession session = await LoadedSession();
        var search = new ManualDebouncer();
        var board = new BoardViewModel(session, search, new QueuedUiDispatcher());
        board.Render();
        int renders = board.RenderCount;
        string title = session.Manuscripts.First(manuscript => manuscript.Location == ManuscriptLocation.Pipeline).Title;
        var thrown = new InvalidOperationException("A subscriber of StateChanged threw.");
        int states = 0;
        int changes = 0;
        session.StateChanged += (_, _) =>
        {
            if (++states == 2)
            {
                throw thrown;
            }
        };
        session.LibraryChanged += (_, _) => changes++;
        using var held = new HeldWork(session);
        var ui = new InterfaceThread();

        Task<SaveOutcome> save = ui.Enter(() => session.SaveAsync(CancellationToken.None));
        held.WaitUntilHeld();
        board.SearchText = title;
        Assert.IsTrue(search.Fire(), "The search's wait was not started.");
        Assert.AreEqual(renders, board.RenderCount, "The Board rendered while the save ran.");

        held.Release();
        Assert.IsGreaterThan(0, ui.RunPosted(TimeSpan.FromSeconds(30)), "The save posted nothing to the caller's context.");
        InvalidOperationException caught = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => save);

        Assert.AreEqual(1, changes, "LibraryChanged was not raised after a subscriber of the ending StateChanged threw.");
        Assert.AreEqual(renders + 1, board.RenderCount, "The Board's render held during the save was not released.");
        Assert.AreSame(thrown, caught, "The save's task did not fault with the subscriber's own exception.");
        Assert.IsFalse(session.IsWorking);
        Assert.IsFalse(session.SaveFailed, "The subscriber's exception was taken for a failed save.");
    }

    // E2.10's automated counterpart ("The mismatched library", 1.0's
    // checklist): a library file that names fewer manuscripts than the managed
    // folder holds, the empty library first and then one manuscript short, is
    // saved, and the managed folder stays byte for byte as it was. Only what
    // the session's own load referenced may be staged.
    [TestMethod]
    public async Task ASaveOfAMismatchedLibraryLeavesTheManagedFolderAsItWas()
    {
        LargeLibraryFixture.Create(_root, 4);
        string full = await File.ReadAllTextAsync(LibraryFile);
        JsonArray shorter = JsonNode.Parse(full)?.AsArray() ?? throw new AssertFailedException("The fixture's library is not a list.");
        int withVersions = shorter.ToList().FindIndex(node => node?["Versions"]?.AsArray().Count > 0);
        Assert.IsGreaterThanOrEqualTo(0, withVersions, "The fixture wrote no manuscript with managed versions.");
        shorter.RemoveAt(withVersions);

        foreach (string mismatched in new[] { "[]", shorter.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) })
        {
            await File.WriteAllTextAsync(LibraryFile, mismatched);
            using DiskLibrarySession session = await LoadedSession();
            IReadOnlyDictionary<string, string> managed = FileState(ManagedDirectory);
            Assert.IsNotEmpty(managed);

            Assert.AreEqual(SaveOutcomeKind.Saved, (await session.SaveAsync(CancellationToken.None)).Kind);

            CollectionAssert.AreEquivalent(managed.ToArray(), FileState(ManagedDirectory).ToArray(),
                $"Saving a library of {session.Manuscripts.Count} manuscripts changed the managed folder.");
        }
    }

    private async Task<DiskLibrarySession> LoadedSession()
    {
        var session = new DiskLibrarySession(DataDirectory, ManagedDirectory, _root, LibraryKind.Example, LargeLibraryFixture.Today);
        Assert.IsTrue(await session.LoadAsync(CancellationToken.None), "The load was refused.");
        return session;
    }

    // A folder where the save writes its temporary file: the save throws
    // before it replaces manuscripts.json.
    private string BlockTheSave()
    {
        string blocker = Path.Combine(DataDirectory, "manuscripts.tmp");
        Directory.CreateDirectory(blocker);
        return blocker;
    }

    // The first manuscript's first draft, with the managed folder the
    // repository names for it.
    private (Manuscript Manuscript, ManuscriptVersion Draft, string Folder) FirstDraft(ILibrarySession session)
    {
        foreach (Manuscript manuscript in session.Manuscripts)
        {
            ManuscriptVersion? draft = manuscript.Versions.FirstOrDefault(version => version.Label == "Draft 1" && version.IsManagedCopy);
            if (draft is not null)
            {
                string folder = Path.Combine(Path.GetFullPath(ManagedDirectory), manuscript.Id.ToString("N"), "versions", draft.Id.ToString("N"));
                return (manuscript, draft, folder);
            }
        }

        throw new AssertFailedException("The fixture wrote no managed draft.");
    }

    // Something that keeps a version's folder where it is, as a file in use
    // does: on Windows the file held open with no sharing; elsewhere, where an
    // open file does not stop a move, its parent folder made read-only for
    // the time being.
    private static IDisposable HoldInPlace(string folder, string file)
    {
        if (OperatingSystem.IsWindows())
        {
            return File.Open(file, FileMode.Open, FileAccess.Read, FileShare.None);
        }

        string parent = Path.GetDirectoryName(folder) ?? throw new AssertFailedException("The version's folder has no parent.");
        return new ReadOnlyFolder(parent);
    }

    // Each manuscript as "Id|Title|stage|place|versions", in the library's
    // order.
    private static string[] Facts(IEnumerable<Manuscript> manuscripts) =>
        [.. manuscripts.Select(manuscript =>
            $"{manuscript.Id}|{manuscript.Title}|{manuscript.CurrentStage}|{manuscript.Location}|{manuscript.Versions.Count}")];

    private static string Hash(string file) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file)));

    // Every file under root, by its path relative to root ("/" between
    // folders), with a hash of its bytes.
    private static IReadOnlyDictionary<string, string> FileState(string root) =>
        Directory.GetFiles(root, "*", SearchOption.AllDirectories).ToDictionary(
            file => Path.GetRelativePath(root, file).Replace('\\', '/'),
            Hash,
            StringComparer.Ordinal);

    // The files added, removed or changed between two states.
    private static string[] Changed(IReadOnlyDictionary<string, string> before, IReadOnlyDictionary<string, string> after) =>
        [.. after.Keys.Union(before.Keys)
            .Where(file => !before.TryGetValue(file, out string? was) || !after.TryGetValue(file, out string? now) || was != now)
            .Order(StringComparer.Ordinal)];

    // Returns once the session's running worker has ended: WaitForWork, run on
    // a thread of its own, so a wait that never returned fails the test
    // instead of hanging it.
    private static void WaitForTheWorker(DiskLibrarySession session)
    {
        var thread = new Thread(() => session.WaitForWork()) { IsBackground = true, Name = "WaitForWork" };
        thread.Start();
        Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(60)), "WaitForWork did not return once the worker was released.");
    }

    // Holds the session's next piece of library work at the start of its
    // worker, on the worker's thread, until Release; work after it is not
    // held. The worker signals when it is held.
    private sealed class HeldWork : IDisposable
    {
        private readonly DiskLibrarySession _session;
        private readonly ManualResetEventSlim _entered = new();
        private readonly ManualResetEventSlim _gate = new();
        private int _entries;

        public HeldWork(DiskLibrarySession session)
        {
            _session = session;
            session.WorkerStarting = Hold;
        }

        // How many workers have started since the hold was set.
        public int Entries => Volatile.Read(ref _entries);

        // The held worker's thread, once WaitUntilHeld has returned.
        public int WorkerThread { get; private set; }

        public void WaitUntilHeld() =>
            Assert.IsTrue(_entered.Wait(TimeSpan.FromSeconds(30)), "The library work did not reach its worker.");

        public void Release() => _gate.Set();

        // The two events are left undisposed: a worker still held when a test
        // fails must find its gate open, not disposed.
        public void Dispose()
        {
            _gate.Set();
            _session.WorkerStarting = null;
        }

        private void Hold()
        {
            if (Interlocked.Increment(ref _entries) != 1)
            {
                return;
            }

            WorkerThread = Environment.CurrentManagedThreadId;
            _entered.Set();
            _gate.Wait(TimeSpan.FromSeconds(60));
        }
    }

    // A folder whose entries cannot be moved or removed until disposed (not
    // on Windows, where a folder's read-only flag does not stop a move).
    [System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
    private sealed class ReadOnlyFolder : IDisposable
    {
        private readonly string _folder;
        private readonly UnixFileMode _mode;

        public ReadOnlyFolder(string folder)
        {
            _folder = folder;
            _mode = File.GetUnixFileMode(folder);
            File.SetUnixFileMode(folder, UnixFileMode.UserRead | UnixFileMode.UserExecute);
        }

        public void Dispose() => File.SetUnixFileMode(_folder, _mode);
    }

    // An interface thread the test drives: what is posted to it waits in a
    // queue until the test runs it on its own thread, so a test can tell work
    // done on the caller's context from work done on a worker.
    private sealed class InterfaceThread : SynchronizationContext
    {
        private readonly Queue<(SendOrPostCallback Callback, object? State)> _posted = new();

        private int Pending
        {
            get
            {
                lock (_posted)
                {
                    return _posted.Count;
                }
            }
        }

        public override void Post(SendOrPostCallback d, object? state)
        {
            lock (_posted)
            {
                _posted.Enqueue((d, state));
            }
        }

        public override SynchronizationContext CreateCopy() => this;

        // Runs start with this as the current context, so the awaits it
        // begins come back here.
        public T Enter<T>(Func<T> start)
        {
            SynchronizationContext? previous = Current;
            SetSynchronizationContext(this);
            try
            {
                return start();
            }
            finally
            {
                SetSynchronizationContext(previous);
            }
        }

        // Waits for a first post (the limit only bounds a failure), then runs
        // on this thread the callbacks queued at that moment and returns how
        // many.
        public int RunPosted(TimeSpan wait)
        {
            if (!SpinWait.SpinUntil(() => Pending > 0, wait))
            {
                return 0;
            }

            int count = Pending;
            for (int i = 0; i < count; i++)
            {
                (SendOrPostCallback callback, object? state) next;
                lock (_posted)
                {
                    next = _posted.Dequeue();
                }

                Enter(() =>
                {
                    next.callback(next.state);
                    return 0;
                });
            }

            return count;
        }
    }
}
