using ManuscriptPipeline.Services;
using PaperRoute.ViewModels.Startup;

namespace PaperRoute.Desktop.Tests.Startup;

// The failure window's recovery as a stand-in (start-up design 4, build 6 F1):
// each check answers as the test sets it, each action is recorded in the
// shared CallLog with the thread it ran on, and nothing touches a file. The
// backup's reading can be held at a gate, so a test sees the work in hand
// without racing it.
internal sealed class FakeStartupRecovery : IStartupRecovery, IDisposable
{
    private readonly CallLog _log;
    private readonly ManualResetEventSlim _inspectionStarted = new(false);
    private readonly ManualResetEventSlim _inspectionReleased = new(true);

    public FakeStartupRecovery(CallLog? log = null)
    {
        _log = log ?? new CallLog();
    }

    public CallLog Log => _log;

    public bool PreviousSaveValid { get; set; } = true;

    public bool FromANewerPaperRoute { get; set; }

    // Thrown by both checks, as by a recovery whose folders cannot be found.
    public Exception? ChecksThrow { get; set; }

    public string ReplacedFile { get; set; } = string.Empty;

    public Exception? UsePreviousSaveThrows { get; set; }

    public Exception? InspectionThrows { get; set; }

    public Exception? RestoreThrows { get; set; }

    public BackupInspection Inspection { get; set; } = new() { ManuscriptCount = 3, SubmissionCount = 5, ManagedFileCount = 2 };

    public RestoreResult Result { get; set; } = new() { ManuscriptCount = 3, EmergencyBackupPath = string.Empty };

    // Set once the backup's reading has begun on its thread.
    public ManualResetEventSlim InspectionStarted => _inspectionStarted;

    // The next reading waits until ReleaseInspection.
    public void HoldInspection() => _inspectionReleased.Reset();

    public void ReleaseInspection() => _inspectionReleased.Set();

    public bool PreviousSaveIsValid() => ChecksThrow is null ? PreviousSaveValid : throw ChecksThrow;

    public bool LibraryIsFromANewerPaperRoute() => ChecksThrow is null ? FromANewerPaperRoute : throw ChecksThrow;

    public string UsePreviousSave()
    {
        _log.Add("use-previous-save");
        return UsePreviousSaveThrows is null ? ReplacedFile : throw UsePreviousSaveThrows;
    }

    public BackupInspection InspectBackup(string backupZipPath)
    {
        _log.Add("inspect " + backupZipPath);
        _inspectionStarted.Set();
        if (!_inspectionReleased.Wait(TimeSpan.FromSeconds(60)))
        {
            throw new TimeoutException("The test never released the backup's reading.");
        }

        return InspectionThrows is null ? Inspection : throw InspectionThrows;
    }

    public RestoreResult RestoreFromBackup(string backupZipPath)
    {
        _log.Add("restore " + backupZipPath);
        return RestoreThrows is null ? Result : throw RestoreThrows;
    }

    public void Dispose()
    {
        _inspectionReleased.Set();
        _inspectionStarted.Dispose();
        _inspectionReleased.Dispose();
    }
}

// The failure window's questions as a stand-in: the archive the test chose
// (null for none) and the answer to the confirmation, each recorded in the
// shared CallLog with its thread. Either can be held open until the test
// answers it.
internal sealed class RecordingStartupQuestions : IStartupQuestions
{
    private readonly CallLog _log;
    private readonly TaskCompletionSource _confirmationAsked = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private TaskCompletionSource<string?>? _heldChoice;
    private TaskCompletionSource<bool>? _heldConfirmation;

    public RecordingStartupQuestions(CallLog? log = null)
    {
        _log = log ?? new CallLog();
    }

    public CallLog Log => _log;

    public string? Backup { get; set; } = "backup.zip";

    public bool Confirms { get; set; } = true;

    public BackupInspection? Asked { get; private set; }

    // Done once the confirmation has been asked.
    public Task ConfirmationAsked => _confirmationAsked.Task;

    // The next picker waits until ReleaseChoice answers it, as a picker
    // that is still open does.
    public void HoldChoice() => _heldChoice = new TaskCompletionSource<string?>();

    public void ReleaseChoice(string? backup) =>
        (_heldChoice ?? throw new InvalidOperationException("No picker is held.")).SetResult(backup);

    // The next confirmation waits until ReleaseConfirmation answers it.
    public void HoldConfirmation() => _heldConfirmation = new TaskCompletionSource<bool>();

    public void ReleaseConfirmation(bool confirms) =>
        (_heldConfirmation ?? throw new InvalidOperationException("No confirmation is held.")).SetResult(confirms);

    public Task<string?> ChooseBackupAsync()
    {
        _log.Add("choose");
        return _heldChoice?.Task ?? Task.FromResult(Backup);
    }

    public Task<bool> ConfirmRestoreAsync(BackupInspection backup)
    {
        _log.Add("confirm");
        Asked = backup;
        _confirmationAsked.TrySetResult();
        return _heldConfirmation?.Task ?? Task.FromResult(Confirms);
    }
}

// What the stand-ins were asked, in order, each with the managed thread it ran on.
internal sealed class CallLog
{
    private readonly object _gate = new();
    private readonly List<(string Name, int Thread)> _calls = [];

    public void Add(string name)
    {
        lock (_gate)
        {
            _calls.Add((name, Environment.CurrentManagedThreadId));
        }
    }

    public string[] Names
    {
        get
        {
            lock (_gate)
            {
                return [.. _calls.Select(call => call.Name)];
            }
        }
    }

    public int ThreadOf(string name)
    {
        lock (_gate)
        {
            return _calls.Single(call => call.Name == name).Thread;
        }
    }
}
