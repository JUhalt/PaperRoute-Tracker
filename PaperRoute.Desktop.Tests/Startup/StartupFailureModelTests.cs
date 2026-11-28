using System.Text.RegularExpressions;
using ManuscriptPipeline.Services;
using PaperRoute.ViewModels.Platform;
using PaperRoute.ViewModels.Startup;

namespace PaperRoute.Desktop.Tests.Startup;

// The start-up failure model (start-up design 3.2 and 5.3, build 6 F1): the
// four kinds with today's words and offers, the failure window's model over a
// recovery stand-in (FakeStartupRecovery) and question stand-in
// (RecordingStartupQuestions), and the storage step's loop. No window is
// built here: the window and the panel come with F2 and W2.
[TestClass]
public sealed partial class StartupFailureModelTests
{
    private static readonly TimeSpan s_limit = TimeSpan.FromSeconds(60);

    // A path that is named, never made or opened.
    private static readonly string s_schemaPath = Path.Combine(Path.GetTempPath(), "no such data root", "data", "schema.json");

    // Each kind's heading and explanation, the title, and every fixed text
    // the model says, against the string literals of today's
    // StartupFailureForm.vb (copied to the test output as text). The changes
    // of 3.2 are not texts the model holds: the status lines carry today's
    // message-box words.
    [TestMethod]
    public void StartupFailureTextsMatchTodays()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Startup", "StartupFailureForm.vb");
        Assert.IsTrue(File.Exists(path), $"{path} was not copied to the test output.");
        string source = File.ReadAllText(path);
        var cause = new InvalidDataException("The test's failure.");
        var problems = new List<string>();

        foreach ((string factory, StartupFailure failure) in new[]
        {
            ("ForStorage", StartupFailure.ForStorage(cause, s_schemaPath)),
            ("ForManuscriptLibrary", StartupFailure.ForManuscriptLibrary(cause, s_schemaPath)),
            ("ForLibraryInUse", StartupFailure.ForLibraryInUse(cause, s_schemaPath)),
            ("ForAuthorLibrary", StartupFailure.ForAuthorLibrary(cause, s_schemaPath)),
        })
        {
            (string heading, string explanation) = TodaysWords(source, factory);
            if (failure.Heading != heading)
            {
                problems.Add($"{factory}'s heading is \"{failure.Heading}\"; today's is \"{heading}\".");
            }

            if (failure.Explanation != explanation)
            {
                problems.Add($"{factory}'s explanation is \"{failure.Explanation}\"; today's is \"{explanation}\".");
            }
        }

        string[] literals = Literals(source);
        foreach ((string name, string text) in new[]
        {
            (nameof(StartupFailureViewModel.TitleText), StartupFailureViewModel.TitleText),
            (nameof(StartupFailureViewModel.FileLabel), StartupFailureViewModel.FileLabel),
            (nameof(StartupFailureViewModel.FolderLabel), StartupFailureViewModel.FolderLabel),
            (nameof(StartupFailureViewModel.ErrorLabel), StartupFailureViewModel.ErrorLabel),
            (nameof(StartupFailureViewModel.BecauseLabel), StartupFailureViewModel.BecauseLabel),
            (nameof(StartupFailureViewModel.ErrorDetailsLabel), StartupFailureViewModel.ErrorDetailsLabel),
            (nameof(StartupFailureViewModel.ReadingTheBackup), StartupFailureViewModel.ReadingTheBackup),
            (nameof(StartupFailureViewModel.RestoringTheBackup), StartupFailureViewModel.RestoringTheBackup),
            (nameof(StartupFailureViewModel.BackupNotValidated), StartupFailureViewModel.BackupNotValidated),
            (nameof(StartupFailureViewModel.PreviousSaveInPlace), StartupFailureViewModel.PreviousSaveInPlace),
            (nameof(StartupFailureViewModel.KeptTheReplacedFileAt), StartupFailureViewModel.KeptTheReplacedFileAt),
            (nameof(StartupFailureViewModel.RestoredLead), StartupFailureViewModel.RestoredLead),
            (nameof(StartupFailureViewModel.OneManuscript), StartupFailureViewModel.OneManuscript),
            (nameof(StartupFailureViewModel.Manuscripts), StartupFailureViewModel.Manuscripts),
            (nameof(StartupFailureViewModel.FromTheBackup), StartupFailureViewModel.FromTheBackup),
            (nameof(StartupFailureViewModel.KeptTheLibraryFilesAt), StartupFailureViewModel.KeptTheLibraryFilesAt),
        })
        {
            if (!literals.Contains(text, StringComparer.Ordinal))
            {
                problems.Add($"{name} is \"{text}\", which is not one of today's texts in StartupFailureForm.vb.");
            }
        }

        Assert.IsEmpty(problems, string.Join(Environment.NewLine, problems));
        Assert.AreEqual(StartupFailureViewModel.TitleText, new StartupFailureViewModel(
            StartupFailure.ForStorage(cause, s_schemaPath), () => new FakeStartupRecovery(), new RecordingStartupQuestions()).Title);
    }

    // Today's offers by kind (StartupFailureForm.vb:63-135), and the in-use
    // test that decides between a held file and a damaged one.
    [TestMethod]
    public void EachKindOffersWhatTodaysDoes()
    {
        var cause = new InvalidDataException("The test's failure.");
        (StartupFailure Failure, StartupFailureKind Kind, bool PreviousSave, bool Restore)[] rows =
        [
            (StartupFailure.ForStorage(cause, s_schemaPath), StartupFailureKind.Storage, true, true),
            (StartupFailure.ForManuscriptLibrary(cause, s_schemaPath), StartupFailureKind.ManuscriptLibrary, true, true),
            (StartupFailure.ForLibraryInUse(cause, s_schemaPath), StartupFailureKind.LibraryInUse, false, false),
            (StartupFailure.ForAuthorLibrary(cause, s_schemaPath), StartupFailureKind.AuthorLibrary, false, true),
            (StartupFailure.ForAuthorLibrary(cause, s_schemaPath, offersRestore: false), StartupFailureKind.AuthorLibrary, false, false),
        ];

        foreach ((StartupFailure failure, StartupFailureKind kind, bool previousSave, bool restore) in rows)
        {
            Assert.AreEqual(kind, failure.Kind);
            Assert.AreEqual(previousSave, failure.OffersPreviousSave, $"{kind}: the previous save.");
            Assert.AreEqual(restore, failure.OffersRestore, $"{kind}: a backup.");
            Assert.AreEqual(s_schemaPath, failure.FilePath);
            Assert.AreEqual(Path.GetDirectoryName(s_schemaPath), failure.FolderPath);
        }

        Assert.IsTrue(StartupFailure.IsInUse(new StorageFileInUseException(s_schemaPath, new IOException("Held."))),
            "The core's in-use error is a library file in use.");
        Assert.IsFalse(StartupFailure.IsInUse(new IOException("The file is damaged.")), "Any other error is the library's own kind.");
        Assert.IsFalse(StartupFailure.IsInUse(new InvalidOperationException("Wrapped.", new StorageFileInUseException(s_schemaPath, new IOException("Held.")))),
            "Only the error itself counts, as today's Catch does (Form1.vb:1412).");
    }

    // The model opens whatever failed (StartupTests.vb:1364-1403): a recovery
    // that cannot be built, such as one whose Documents folder cannot be
    // found, offers nothing, and the model still says what failed.
    [TestMethod]
    public void ARecoveryThatCannotBeBuiltStillOpensTheWindow()
    {
        StartupFailure failure = StartupFailure.ForStorage(new InvalidDataException("The test's failure."), s_schemaPath);

        var model = new StartupFailureViewModel(
            failure,
            () => throw new InvalidOperationException("The user's Documents folder could not be located."),
            new RecordingStartupQuestions());

        Assert.AreEqual(failure.Heading, model.Heading);
        Assert.AreEqual(failure.Explanation, model.Explanation);
        StringAssert.Contains(model.Details, "The test's failure.");
        Assert.IsFalse(model.OffersPreviousSave, "No previous save without a recovery.");
        Assert.IsFalse(model.OffersRestore, "No backup without a recovery.");
        Assert.IsTrue(model.CanClose, "Close works.");
    }

    // Each offer follows the recovery's checks, and a check that throws
    // offers nothing (StartupFailureForm.vb:575-605).
    [TestMethod]
    public void EachOfferFollowsTheRecoverysChecks()
    {
        StartupFailure failure = StartupFailure.ForStorage(new InvalidDataException("The test's failure."), s_schemaPath);
        (bool Valid, bool Newer, Exception? Throws, bool PreviousSave, bool Restore)[] rows =
        [
            (true, false, null, true, true),
            (false, false, null, false, true),
            (false, true, null, false, false),
            (true, false, new IOException("The folder cannot be read."), false, false),
        ];

        foreach ((bool valid, bool newer, Exception? throws, bool previousSave, bool restore) in rows)
        {
            using var recovery = new FakeStartupRecovery { PreviousSaveValid = valid, FromANewerPaperRoute = newer, ChecksThrow = throws };
            var model = new StartupFailureViewModel(failure, () => recovery, new RecordingStartupQuestions());
            string row = $"previous save reads: {valid}, newer: {newer}, checks throw: {throws is not null}";
            Assert.AreEqual(previousSave, model.OffersPreviousSave, row);
            Assert.AreEqual(restore, model.OffersRestore, row);
        }

        using var held = new FakeStartupRecovery();
        var inUse = new StartupFailureViewModel(StartupFailure.ForLibraryInUse(new IOException("Held."), s_schemaPath), () => held, new RecordingStartupQuestions());
        Assert.IsFalse(inUse.OffersPreviousSave || inUse.OffersRestore, "A file in use is not damage: nothing is offered whatever the checks say.");
    }

    // Restore reads the archive, then asks, then restores (#115, #124), and the
    // reading and the restore run off the window's thread (#295), while the
    // questions are asked on it.
    [TestMethod]
    public void RestoreReadsThenAsksThenRestoresOffTheWindowThread()
    {
        var log = new CallLog();
        using var recovery = new FakeStartupRecovery(log);
        var questions = new RecordingStartupQuestions(log) { Backup = "chosen.zip", Confirms = true };
        StartupFailureViewModel? model = null;

        int window = WindowThread.Run(() =>
        {
            model = new StartupFailureViewModel(StartupFailure.ForStorage(new InvalidDataException("The test's failure."), s_schemaPath), () => recovery, questions);
            return model.RestoreFromBackupAsync();
        }, s_limit);

        CollectionAssert.AreEqual(new[] { "choose", "inspect chosen.zip", "confirm", "restore chosen.zip" }, log.Names,
            "The archive is read before anything is asked, and restored only after the person says yes.");
        Assert.AreEqual(window, log.ThreadOf("choose"), "The archive is chosen on the window's thread.");
        Assert.AreEqual(window, log.ThreadOf("confirm"), "The confirmation is asked on the window's thread.");
        Assert.AreNotEqual(window, log.ThreadOf("inspect chosen.zip"), "The archive is read off the window's thread.");
        Assert.AreNotEqual(window, log.ThreadOf("restore chosen.zip"), "The archive is restored off the window's thread.");
        Assert.AreSame(recovery.Inspection, questions.Asked, "The confirmation shows what the reading found.");
        Assert.IsNotNull(model);
        Assert.IsTrue(model.Recovered, "The restore put the library in place.");
        Assert.AreEqual("PaperRoute restored 3 manuscripts from the backup.", model.Status, "The status line says what was restored, in today's words.");
    }

    // No at the confirmation, or no archive chosen, restores nothing.
    [TestMethod]
    public void ACancelledRestoreConfirmationChangesNothing()
    {
        StartupFailure failure = StartupFailure.ForStorage(new InvalidDataException("The test's failure."), s_schemaPath);

        var log = new CallLog();
        using var recovery = new FakeStartupRecovery(log);
        var model = new StartupFailureViewModel(failure, () => recovery, new RecordingStartupQuestions(log) { Backup = "chosen.zip", Confirms = false });
        Run(model.RestoreFromBackupAsync());
        CollectionAssert.AreEqual(new[] { "choose", "inspect chosen.zip", "confirm" }, log.Names, "No at the confirmation restores nothing.");
        Assert.IsFalse(model.Recovered);
        Assert.AreEqual(string.Empty, model.Status, "Nothing to report.");

        var pickLog = new CallLog();
        using var notPicked = new FakeStartupRecovery(pickLog);
        var noArchive = new StartupFailureViewModel(failure, () => notPicked, new RecordingStartupQuestions(pickLog) { Backup = null });
        Run(noArchive.RestoreFromBackupAsync());
        CollectionAssert.AreEqual(new[] { "choose" }, pickLog.Names, "No archive chosen reads and restores nothing.");
        Assert.IsFalse(noArchive.Recovered);
    }

    // A backup that cannot be read, or a restore that fails, says why in the
    // status line, in today's words, and the model is not recovered.
    [TestMethod]
    public void AFailedRestoreSaysWhyAndIsNotRecovered()
    {
        StartupFailure failure = StartupFailure.ForStorage(new InvalidDataException("The test's failure."), s_schemaPath);

        var log = new CallLog();
        using var unreadable = new FakeStartupRecovery(log) { InspectionThrows = new InvalidDataException("This archive does not appear to be a PaperRoute backup.") };
        var model = new StartupFailureViewModel(failure, () => unreadable, new RecordingStartupQuestions(log));
        Run(model.RestoreFromBackupAsync());
        Assert.AreEqual(StartupFailureViewModel.BackupNotValidated + " This archive does not appear to be a PaperRoute backup.", model.Status);
        CollectionAssert.DoesNotContain(log.Names, "confirm", "Nothing is asked about an archive that cannot be read.");
        Assert.IsFalse(model.Recovered);
        Assert.IsTrue(model.CanClose && !model.IsWorking, "The work is over.");

        using var failing = new FakeStartupRecovery { RestoreThrows = new IOException("The restore stopped; your files were put back.") };
        var restoring = new StartupFailureViewModel(failure, () => failing, new RecordingStartupQuestions());
        Run(restoring.RestoreFromBackupAsync());
        Assert.AreEqual("The restore stopped; your files were put back.", restoring.Status, "A failed restore says what the core said.");
        Assert.IsFalse(restoring.Recovered);
    }

    // While the backup is read, and again while it is restored, the status
    // line says which, in today's words, and a person's close is refused;
    // when the work ends, Close works again (#295).
    [TestMethod]
    public void CloseIsRefusedWhileWorkRuns()
    {
        using var recovery = new FakeStartupRecovery();
        recovery.HoldInspection();
        recovery.HoldRestore();
        var model = new StartupFailureViewModel(StartupFailure.ForStorage(new InvalidDataException("The test's failure."), s_schemaPath),
            () => recovery, new RecordingStartupQuestions { Confirms = true });
        var changed = new List<string?>();
        model.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
        Assert.IsTrue(model.CanClose, "Close works before any work.");

        Task restore = model.RestoreFromBackupAsync();
        Assert.IsTrue(recovery.InspectionStarted.Wait(s_limit), "The backup's reading began.");

        Assert.IsTrue(model.IsWorking, "The work is in hand.");
        Assert.IsFalse(model.CanClose, "A close is refused while the work runs.");
        Assert.AreEqual("Reading the backup...", model.Status, "The status line says the backup is being read.");
        CollectionAssert.Contains(changed, nameof(StartupFailureViewModel.CanClose), "The view hears that Close is refused.");

        recovery.ReleaseInspection();
        Assert.IsTrue(recovery.RestoreStarted.Wait(s_limit), "The restore began.");
        Assert.IsTrue(model.IsWorking, "The work is in hand.");
        Assert.IsFalse(model.CanClose, "A close is refused while the restore runs.");
        Assert.AreEqual("Restoring the backup...", model.Status, "The status line says the backup is being restored.");

        recovery.ReleaseRestore();
        Run(restore);

        Assert.IsFalse(model.IsWorking);
        Assert.IsTrue(model.CanClose, "Close works once the work ends.");
        Assert.AreEqual("PaperRoute restored 3 manuscripts from the backup.", model.Status, "The work's status gives way to the result.");
    }

    // Start-up design 3.2 row 28: the window stays usable while the picker
    // waits (on one runner such a picker never returned, #451), so Close
    // works then; but the restore is one recovery, and neither Use the
    // Previous Save nor a second Restore begins until it is over.
    [TestMethod]
    public void NoOtherRecoveryBeginsWhileThePickerWaits()
    {
        var log = new CallLog();
        using var recovery = new FakeStartupRecovery(log);
        var questions = new RecordingStartupQuestions(log);
        questions.HoldChoice();
        var model = new StartupFailureViewModel(StorageFailure(), () => recovery, questions);
        Assert.IsTrue(model.CanRecover, "A recovery may begin.");

        Task restore = model.RestoreFromBackupAsync();
        Assert.IsFalse(restore.IsCompleted, "The picker waits.");
        Assert.IsFalse(model.CanRecover, "The offers are off while the picker waits.");
        Assert.IsTrue(model.CanClose, "Close works while the picker waits.");

        model.UsePreviousSave();
        Task second = model.RestoreFromBackupAsync();
        Assert.IsTrue(second.IsCompleted, "A second Restore is refused at once.");
        CollectionAssert.AreEqual(new[] { "choose" }, log.Names, "Neither the previous save nor a second picker was taken while the picker waited.");
        Assert.IsFalse(model.Recovered);

        questions.ReleaseChoice("chosen.zip");
        Run(restore);
        CollectionAssert.AreEqual(new[] { "choose", "inspect chosen.zip", "confirm", "restore chosen.zip" }, log.Names, "The first restore goes on.");
        Assert.IsTrue(model.Recovered);
        Assert.IsFalse(model.CanRecover, "Nothing more is recovered once a recovery succeeded.");
    }

    // An answer that comes after the view closed goes no further: a picker
    // reads nothing, a reading asks nothing, and a yes restores nothing.
    [TestMethod]
    public void NothingIsReadOrRestoredOnceTheViewClosed()
    {
        var pickLog = new CallLog();
        using var picked = new FakeStartupRecovery(pickLog);
        var pickQuestions = new RecordingStartupQuestions(pickLog);
        pickQuestions.HoldChoice();
        var model = new StartupFailureViewModel(StorageFailure(), () => picked, pickQuestions);
        Task restore = model.RestoreFromBackupAsync();
        model.Closed();
        Assert.IsFalse(model.CanRecover, "Nothing is offered once the view closed.");
        pickQuestions.ReleaseChoice("chosen.zip");
        Run(restore);
        CollectionAssert.AreEqual(new[] { "choose" }, pickLog.Names, "A picker that answers after the view closed reads and restores nothing.");
        Assert.IsFalse(model.Recovered);

        var readLog = new CallLog();
        using var read = new FakeStartupRecovery(readLog);
        read.HoldInspection();
        var reading = new StartupFailureViewModel(StorageFailure(), () => read, new RecordingStartupQuestions(readLog) { Backup = "chosen.zip" });
        Task readingRestore = reading.RestoreFromBackupAsync();
        Assert.IsTrue(read.InspectionStarted.Wait(s_limit), "The backup's reading began.");
        reading.Closed();
        read.ReleaseInspection();
        Run(readingRestore);
        CollectionAssert.AreEqual(new[] { "choose", "inspect chosen.zip" }, readLog.Names, "A reading that ends after the view closed asks and restores nothing.");

        var askLog = new CallLog();
        using var asked = new FakeStartupRecovery(askLog);
        var askQuestions = new RecordingStartupQuestions(askLog) { Backup = "chosen.zip" };
        askQuestions.HoldConfirmation();
        var asking = new StartupFailureViewModel(StorageFailure(), () => asked, askQuestions);
        Task askingRestore = asking.RestoreFromBackupAsync();
        Assert.IsTrue(askQuestions.ConfirmationAsked.Wait(s_limit), "The confirmation was asked.");
        Assert.IsFalse(askingRestore.IsCompleted, "The confirmation waits.");
        asking.Closed();
        askQuestions.ReleaseConfirmation(true);
        Run(askingRestore);
        CollectionAssert.AreEqual(new[] { "choose", "inspect chosen.zip", "confirm" }, askLog.Names, "A yes that comes after the view closed restores nothing.");
        Assert.IsFalse(asking.Recovered);
    }

    // Start-up design 3.2 rows 25 and 29: an operating-system shutdown waits
    // for the work in hand, then closes, as today's window does. The wait
    // ends with the work, on the worker, so a shutdown may hold the window's
    // thread while it waits, as today's does; and it never throws.
    [TestMethod]
    public void AShutdownCanWaitForTheWork()
    {
        using var recovery = new FakeStartupRecovery();
        var model = new StartupFailureViewModel(StorageFailure(), () => recovery, new RecordingStartupQuestions { Confirms = false });
        Assert.IsTrue(model.WaitForWorkAsync().IsCompleted, "Nothing to wait for before any work.");
        recovery.HoldInspection();
        bool waitedWhileWorking = false;
        bool waited = false;

        WindowThread.Run(() =>
        {
            Task restore = model.RestoreFromBackupAsync();
            Assert.IsTrue(recovery.InspectionStarted.Wait(s_limit), "The backup's reading began.");
            Task wait = model.WaitForWorkAsync();
            waitedWhileWorking = !wait.IsCompleted;
            _ = Task.Run(recovery.ReleaseInspection);
            waited = wait.Wait(s_limit);
            return restore;
        }, s_limit);

        Assert.IsTrue(waitedWhileWorking, "The wait lasts while the work runs.");
        Assert.IsTrue(waited, "The wait ended with the work while the window's thread was held.");
        Assert.IsTrue(model.WaitForWorkAsync().IsCompleted && !model.IsWorking, "The work is over.");

        using var failing = new FakeStartupRecovery { InspectionThrows = new InvalidDataException("This archive does not appear to be a PaperRoute backup.") };
        failing.HoldInspection();
        var failed = new StartupFailureViewModel(StorageFailure(), () => failing, new RecordingStartupQuestions());
        Task failedRestore = failed.RestoreFromBackupAsync();
        Assert.IsTrue(failing.InspectionStarted.Wait(s_limit), "The backup's reading began.");
        Task failedWait = failed.WaitForWorkAsync();
        failing.ReleaseInspection();
        Assert.IsTrue(failedWait.Wait(s_limit), "The wait ends with work that failed.");
        Assert.IsFalse(failedWait.IsFaulted, "The wait does not throw the work's error: the model reports it.");
        Run(failedRestore);
    }

    // Copy Details carries today's text: the title, what failed, the details
    // (file, folder, the error and what caused it) and the full error, then
    // the System section.
    [TestMethod]
    public void CopyDetailsHoldsHeadingExplanationDetailsAndTheFullError()
    {
        var cause = new InvalidDataException("The storage marker is empty.", new IOException("The disk said no."));
        var model = new StartupFailureViewModel(StartupFailure.ForStorage(cause, s_schemaPath), () => new FakeStartupRecovery(), new RecordingStartupQuestions());

        string text = model.CopyText();

        string[] inOrder =
        [
            StartupFailureViewModel.TitleText,
            new string('=', StartupFailureViewModel.TitleText.Length),
            model.Heading,
            model.Explanation,
            "File:   " + s_schemaPath,
            "Folder: " + Path.GetDirectoryName(s_schemaPath),
            "Error: The storage marker is empty.",
            "Because: The disk said no.",
            "Error details:",
            cause.ToString(),
            StartupFailureViewModel.SystemHeading,
        ];

        int at = 0;
        foreach (string part in inOrder)
        {
            int found = text.IndexOf(part, at, StringComparison.Ordinal);
            Assert.IsGreaterThanOrEqualTo(0, found, $"Copy Details lacks \"{part}\" where it belongs:{Environment.NewLine}{text}");
            at = found + part.Length;
        }

        StringAssert.StartsWith(model.Details, "File:   " + s_schemaPath, "The details start with the file.");
    }

    // The System section passes every fact through the platform check's
    // scrubber: the person's name, the computer's name and the home folder
    // never reach the clipboard from it.
    [TestMethod]
    public void TheSystemSectionNamesNoUserOrComputer()
    {
        string user = Environment.UserName;
        string computer = Environment.MachineName;
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        PrivacyScrubber scrubber = PrivacyScrubber.ForThisProcess(Environment.ProcessPath);

        string probe = StartupFailureViewModel.SystemSection(
            [("Probe", $"{user} on {computer}, in {Path.Combine(home, "PaperRoute")}")], scrubber);
        AssertNamesNoOne(probe, user, computer, home, "a fact that names them");
        if (user.Trim().Length >= 2)
        {
            StringAssert.Contains(probe, PrivacyScrubber.UserName, "The person's name is replaced.");
        }

        if (computer.Trim().Length >= 2)
        {
            StringAssert.Contains(probe, PrivacyScrubber.ComputerName, "The computer's name is replaced.");
        }

        var model = new StartupFailureViewModel(StartupFailure.ForStorage(new InvalidDataException("The test's failure."), s_schemaPath),
            () => new FakeStartupRecovery(), new RecordingStartupQuestions());
        string copy = model.CopyText();
        int system = copy.IndexOf(StartupFailureViewModel.SystemHeading, StringComparison.Ordinal);
        Assert.IsGreaterThanOrEqualTo(0, system, "Copy Details has a System section.");
        AssertNamesNoOne(copy[system..], user, computer, home, "this system's facts");
    }

    // Today's loop (StartupTests.vb:453-534): a failure the person recovers
    // runs the storage step once more, and start-up goes on.
    [TestMethod]
    public void TheStorageStepRetriesAfterARecovery()
    {
        int attempts = 0;
        var shown = new List<StartupFailure>();
        var step = new StorageStep(
            () =>
            {
                attempts++;
                if (attempts == 1)
                {
                    throw new InvalidDataException("The test's failure.");
                }
            },
            () => s_schemaPath);

        StorageReady? ready = Run(step.RunAsync(failure =>
        {
            shown.Add(failure);
            return Task.FromResult(true);
        }));

        Assert.IsNotNull(ready, "Start-up goes on with storage ready.");
        Assert.AreEqual(2, attempts, "The storage step ran once more after the recovery.");
        Assert.AreEqual(StartupFailureKind.Storage, shown.Single().Kind, "The failure is the storage kind.");
        Assert.AreEqual(s_schemaPath, shown.Single().FilePath, "It names the storage marker.");
        Assert.AreEqual("The test's failure.", shown.Single().Cause.Message);
    }

    // Today's loop (StartupTests.vb:1114-1148): a failure closed without
    // recovering ends start-up, and the step is not run again.
    [TestMethod]
    public void AStorageWindowClosedWithoutRecoveringEndsStartup()
    {
        int attempts = 0;
        var shown = new List<StartupFailure>();
        var step = new StorageStep(
            () =>
            {
                attempts++;
                throw new InvalidDataException("The test's failure.");
            },
            () => s_schemaPath);

        StorageReady? ready = Run(step.RunAsync(failure =>
        {
            shown.Add(failure);
            return Task.FromResult(false);
        }));

        Assert.IsNull(ready, "Start-up ends.");
        Assert.AreEqual(1, attempts, "The storage step is not run again.");
        Assert.AreEqual("The test's failure.", shown.Single().Cause.Message);
    }

    // Start-up design 2.4 row 9: the storage step runs on a worker, awaited,
    // so a long migration never holds the window's thread, and each failure
    // is shown back on the window's thread, where the loop resumes.
    [TestMethod]
    public void TheStorageStepPreparesOffTheWindowThread()
    {
        var log = new CallLog();
        int attempts = 0;
        var step = new StorageStep(
            () =>
            {
                attempts++;
                log.Add("prepare " + attempts.ToString(System.Globalization.CultureInfo.InvariantCulture));
                if (attempts == 1)
                {
                    throw new InvalidDataException("The test's failure.");
                }
            },
            () => s_schemaPath);
        StorageReady? ready = null;

        int window = WindowThread.Run(async () =>
        {
            ready = await step.RunAsync(failure =>
            {
                log.Add("show");
                return Task.FromResult(true);
            });
        }, s_limit);

        CollectionAssert.AreEqual(new[] { "prepare 1", "show", "prepare 2" }, log.Names);
        Assert.IsNotNull(ready, "Start-up goes on with storage ready.");
        Assert.AreNotEqual(window, log.ThreadOf("prepare 1"), "The storage step runs off the window's thread.");
        Assert.AreNotEqual(window, log.ThreadOf("prepare 2"), "The storage step runs off the window's thread when it runs again.");
        Assert.AreEqual(window, log.ThreadOf("show"), "The failure is shown on the window's thread.");
    }

    private static StartupFailure StorageFailure() => StartupFailure.ForStorage(new InvalidDataException("The test's failure."), s_schemaPath);

    private static void AssertNamesNoOne(string text, string user, string computer, string home, string what)
    {
        // The scrubber's own placeholders are taken out first: they hold words such as "user".
        string scrubbed = text
            .Replace(PrivacyScrubber.UserName, string.Empty, StringComparison.Ordinal)
            .Replace(PrivacyScrubber.ComputerName, string.Empty, StringComparison.Ordinal)
            .Replace(PrivacyScrubber.HomeFolder, string.Empty, StringComparison.Ordinal)
            .Replace(PrivacyScrubber.ProgramFolder, string.Empty, StringComparison.Ordinal)
            .Replace(PrivacyScrubber.WorkingFolder, string.Empty, StringComparison.Ordinal);
        if (user.Trim().Length >= 2)
        {
            Assert.IsFalse(scrubbed.Contains(user.Trim(), StringComparison.OrdinalIgnoreCase), $"The System section names the person, from {what}.");
        }

        if (computer.Trim().Length >= 2)
        {
            Assert.IsFalse(scrubbed.Contains(computer.Trim(), StringComparison.OrdinalIgnoreCase), $"The System section names the computer, from {what}.");
        }

        string folder = home.TrimEnd('/', '\\');
        if (folder.Length > 1)
        {
            Assert.IsFalse(scrubbed.Contains(folder, StringComparison.OrdinalIgnoreCase), $"The System section names the home folder, from {what}.");
        }
    }

    // A factory's heading and explanation in today's source: the first two
    // string literals after "Return New StartupFailure(" in the factory.
    private static (string Heading, string Explanation) TodaysWords(string source, string factory)
    {
        int start = source.IndexOf("Public Shared Function " + factory + "(", StringComparison.Ordinal);
        Assert.IsGreaterThanOrEqualTo(0, start, $"StartupFailureForm.vb has no {factory}.");
        int body = source.IndexOf("Return New StartupFailure(", start, StringComparison.Ordinal);
        Assert.IsGreaterThan(start, body, $"{factory} builds no StartupFailure.");
        string[] found = [.. VbLiteral().Matches(source[body..]).Take(2).Select(match => Unquote(match.Value))];
        Assert.HasCount(2, found, $"{factory} gives no heading and explanation.");
        return (found[0], found[1]);
    }

    private static string[] Literals(string source) =>
        [.. VbLiteral().Matches(source).Select(match => Unquote(match.Value))];

    private static string Unquote(string literal) => literal[1..^1].Replace("\"\"", "\"", StringComparison.Ordinal);

    private static T Run<T>(Task<T> work) => work.WaitAsync(s_limit).GetAwaiter().GetResult();

    private static void Run(Task work) => work.WaitAsync(s_limit).GetAwaiter().GetResult();

    // A VB string literal: quotes around text in which a quote is doubled.
    [GeneratedRegex("\"(?:[^\"]|\"\")*\"")]
    private static partial Regex VbLiteral();
}
