using System.Runtime.InteropServices;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using PaperRoute.ViewModels.Platform;

namespace PaperRoute.ViewModels.Startup;

// The model of "PaperRoute could not start" (start-up design 3.2 rows 20 to
// 31, build 6 F1): what failed, the technical details, what may be put right,
// the work in hand and its status line. One model for both of its views: the
// window before start-up and the panel over the main window (F2, W2). Used
// on the interface thread, as every view model is.
//
// Today's rules (StartupFailureForm.vb), with their words:
// - The model is built whatever failed. The recovery is built only when the
//   failure offers something, and one that cannot be built, or a check that
//   cannot be made, offers nothing (a missing Documents folder may be why
//   start-up failed).
// - Use the Previous Save is offered only when manuscripts.bak reads, the
//   library file does not, and the storage marker is not from a newer
//   PaperRoute; Restore From Backup... wherever the failure offers it, unless
//   a newer PaperRoute wrote the library.
// - Restore reads the archive first, then asks, then restores; reading and
//   restoring run on a worker while the status line says so, and a close is
//   refused meanwhile (CanClose), while an operating-system shutdown waits
//   for the work to end (WaitForWorkAsync).
// - A restore is one recovery from the picker to the end: no other begins
//   meanwhile (CanRecover). Unlike today's picker, the new one is not modal
//   to the window, so Close works while it waits, and an answer that comes
//   after the view closed (Closed) reads, asks and restores nothing.
// - A recovery that succeeds says what it put in place, and the model is then
//   Recovered: the caller goes on with the recovered library. One that fails
//   says why and changes nothing here.
// Changed on purpose (start-up design 3.2 row 26): what today's message boxes
// said (a failed recovery, the result) is said in the status line, and Copy
// Details carries a short System section, scrubbed of the person's name and
// the computer's, in place of the Diagnostics report, which joins when #435
// ports it.
public sealed class StartupFailureViewModel : ObservableObject
{
    // Today's words (StartupFailureForm.vb), which StartupFailureTextsMatchTodays compares.
    internal const string TitleText = "PaperRoute could not start";
    internal const string FileLabel = "File:   ";
    internal const string FolderLabel = "Folder: ";
    internal const string ErrorLabel = "Error: ";
    internal const string BecauseLabel = "Because: ";
    internal const string ErrorDetailsLabel = "Error details:";
    internal const string ReadingTheBackup = "Reading the backup...";
    internal const string RestoringTheBackup = "Restoring the backup...";
    internal const string BackupNotValidated = "This backup could not be validated. Nothing was changed.";
    internal const string PreviousSaveInPlace = "PaperRoute put the previous save in place of your library file";
    internal const string KeptTheReplacedFileAt = " and kept the file it replaced at ";
    internal const string RestoredLead = "PaperRoute restored ";
    internal const string OneManuscript = " manuscript";
    internal const string Manuscripts = " manuscripts";
    internal const string FromTheBackup = " from the backup";
    internal const string KeptTheLibraryFilesAt = " and kept your library files from before the restore at ";

    // The System section of Copy Details (new: no counterpart today).
    internal const string SystemHeading = "System:";

    private readonly IStartupRecovery? _recovery;
    private readonly IStartupQuestions _questions;
    private bool _isWorking;
    private bool _recovered;
    private bool _restoring;
    private bool _closed;
    private string _status = string.Empty;
    private Task _work = Task.CompletedTask;

    public StartupFailureViewModel(StartupFailure failure, Func<IStartupRecovery> createRecovery, IStartupQuestions questions)
    {
        ArgumentNullException.ThrowIfNull(failure);
        ArgumentNullException.ThrowIfNull(createRecovery);
        ArgumentNullException.ThrowIfNull(questions);
        Failure = failure;
        _questions = questions;

        if (failure.OffersPreviousSave || failure.OffersRestore)
        {
            try
            {
                _recovery = createRecovery();
            }
            catch (Exception)
            {
                _recovery = null;
            }
        }

        OffersPreviousSave = failure.OffersPreviousSave && Check(recovery => recovery.PreviousSaveIsValid());
        OffersRestore = failure.OffersRestore && Check(recovery => !recovery.LibraryIsFromANewerPaperRoute());
        Details = BuildDetails(failure);
    }

    public string Title => TitleText;

    public StartupFailure Failure { get; }

    public string Heading => Failure.Heading;

    public string Explanation => Failure.Explanation;

    // The file and folder, then the error and each error that caused it.
    public string Details { get; }

    public bool OffersPreviousSave { get; }

    public bool OffersRestore { get; }

    // True while the backup is read or restored on a worker.
    public bool IsWorking
    {
        get => _isWorking;
        private set
        {
            if (SetProperty(ref _isWorking, value))
            {
                OnPropertyChanged(nameof(CanClose));
            }
        }
    }

    // A person's close is refused while the work runs: closed partway, a
    // restore could leave the library half replaced (#295).
    public bool CanClose => !IsWorking;

    // True once the previous save or a backup was put in place.
    public bool Recovered
    {
        get => _recovered;
        private set
        {
            if (SetProperty(ref _recovered, value))
            {
                OnPropertyChanged(nameof(CanRecover));
            }
        }
    }

    // Whether a recovery may begin: not once one succeeded or the view
    // closed, and not while a restore is under way, its picker and its
    // confirmation included. The window stays usable while the picker waits
    // (start-up design 3.2 row 28: on one runner such a picker never
    // returned, #451), so Close works then, but the offers are turned off.
    public bool CanRecover => !_restoring && !Recovered && !_closed;

    // The status line: the work in hand, or what the last recovery did or why it failed.
    public string Status
    {
        get => _status;
        private set => SetProperty(ref _status, value);
    }

    // Use the Previous Save, as today on the calling thread: it replaces one
    // file. Done when Recovered is true; otherwise Status says why.
    public void UsePreviousSave()
    {
        if (!OffersPreviousSave || !CanRecover || _recovery is null)
        {
            return;
        }

        string replacedFile;
        try
        {
            replacedFile = _recovery.UsePreviousSave();
        }
        catch (Exception ex)
        {
            Status = ex.Message;
            return;
        }

        Finish(PreviousSaveInPlace + (string.IsNullOrEmpty(replacedFile) ? "." : KeptTheReplacedFileAt + replacedFile + "."));
    }

    // Restore From Backup...: asks for the archive, reads it on a worker,
    // asks to go on with its contents, then restores it on a worker. Nothing
    // changes until the person says yes. The whole of it is one recovery:
    // no other begins meanwhile (CanRecover), and an answer that comes after
    // the view closed goes no further.
    public async Task RestoreFromBackupAsync()
    {
        if (!OffersRestore || !CanRecover || _recovery is null)
        {
            return;
        }

        IStartupRecovery recovery = _recovery;
        SetRestoring(true);
        try
        {
            await RestoreAsync(recovery);
        }
        finally
        {
            SetRestoring(false);
        }
    }

    // The view calls this once it has closed: a picker or a confirmation that
    // answers afterwards, or a reading that ends afterwards, changes nothing.
    // A person's close waits for CanClose; a shutdown first waits for the
    // work (WaitForWorkAsync).
    public void Closed()
    {
        if (!_closed)
        {
            _closed = true;
            OnPropertyChanged(nameof(CanRecover));
        }
    }

    // The work in hand, which an operating-system shutdown waits for before
    // the view closes, as today's window does (StartupFailureForm.vb,
    // OnFormClosing). It ends on the worker when the work ends, whatever the
    // outcome, and never throws, so a shutdown may also hold the window's
    // thread while it waits, as today's does. Done at once when nothing runs.
    public Task WaitForWorkAsync() => _work;

    private async Task RestoreAsync(IStartupRecovery recovery)
    {
        string? backupPath = await _questions.ChooseBackupAsync();
        if (string.IsNullOrEmpty(backupPath) || _closed)
        {
            return;
        }

        BackupInspection inspection;
        try
        {
            inspection = await RunWork(ReadingTheBackup, () => recovery.InspectBackup(backupPath));
        }
        catch (Exception ex)
        {
            Status = BackupNotValidated + " " + ex.Message;
            return;
        }

        if (_closed)
        {
            return;
        }

        bool confirmed = await _questions.ConfirmRestoreAsync(inspection);
        if (!confirmed || _closed)
        {
            return;
        }

        RestoreResult result;
        try
        {
            result = await RunWork(RestoringTheBackup, () => recovery.RestoreFromBackup(backupPath));
        }
        catch (Exception ex)
        {
            // A failure during the swap says what was put back and names the
            // emergency ZIP (#116); before it, nothing was changed.
            Status = ex.Message;
            return;
        }

        Finish(RestoredLead + result.ManuscriptCount.ToString(System.Globalization.CultureInfo.CurrentCulture) +
            (result.ManuscriptCount == 1 ? OneManuscript : Manuscripts) + FromTheBackup +
            (string.IsNullOrWhiteSpace(result.EmergencyBackupPath) ? "." : KeptTheLibraryFilesAt + result.EmergencyBackupPath + "."));
    }

    // What Copy Details puts on the clipboard: the title, what failed, the
    // details, the full error, and the System section.
    public string CopyText()
    {
        var builder = new StringBuilder();
        builder.AppendLine(TitleText);
        builder.AppendLine(new string('=', TitleText.Length));
        builder.AppendLine();
        builder.AppendLine(Heading);
        builder.AppendLine(Explanation);
        builder.AppendLine();
        builder.AppendLine(Details);
        builder.AppendLine();
        builder.AppendLine(ErrorDetailsLabel);
        builder.AppendLine(Failure.Cause.ToString());
        builder.AppendLine();
        builder.Append(SystemSection(SystemFacts(), PrivacyScrubber.ForThisProcess(Environment.ProcessPath)));
        return builder.ToString();
    }

    // The System section: one line per fact, each scrubbed of the program's
    // and working folders, the home folder, the person's name and the computer's.
    internal static string SystemSection(IEnumerable<(string Label, string Value)> facts, PrivacyScrubber scrubber)
    {
        var builder = new StringBuilder();
        builder.AppendLine(SystemHeading);
        foreach ((string label, string value) in facts)
        {
            builder.AppendLine("  " + label + ": " + scrubber.Scrub(value));
        }

        return builder.ToString();
    }

    private static IEnumerable<(string Label, string Value)> SystemFacts() =>
    [
        ("Operating system", RuntimeInformation.OSDescription),
        ("Runtime", RuntimeInformation.FrameworkDescription),
        ("Architecture", RuntimeInformation.ProcessArchitecture.ToString()),
    ];

    private static string BuildDetails(StartupFailure failure)
    {
        var builder = new StringBuilder();
        builder.AppendLine(FileLabel + failure.FilePath);
        builder.AppendLine(FolderLabel + failure.FolderPath);
        builder.AppendLine();
        builder.AppendLine(ErrorLabel + failure.Cause.Message);
        for (Exception? inner = failure.Cause.InnerException; inner is not null; inner = inner.InnerException)
        {
            builder.AppendLine(BecauseLabel + inner.Message);
        }

        return builder.ToString().TrimEnd();
    }

    // A check that cannot be made offers nothing: this model is what
    // PaperRoute shows when it cannot start, so it must be built.
    private bool Check(Func<IStartupRecovery, bool> check)
    {
        if (_recovery is null)
        {
            return false;
        }

        try
        {
            return check(_recovery);
        }
        catch (Exception)
        {
            return false;
        }
    }

    // The backup's reading or its restore, on a worker: the status line says
    // what is happening and a close is refused until it ends; the work's own
    // exception comes back to the caller.
    private async Task<T> RunWork<T>(string status, Func<T> work)
    {
        IsWorking = true;
        Status = status;
        try
        {
            Task<T> running = Task.Run(work);
            _work = running.ContinueWith(static _ => { }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            return await running;
        }
        finally
        {
            Status = string.Empty;
            IsWorking = false;
        }
    }

    private void SetRestoring(bool restoring)
    {
        _restoring = restoring;
        OnPropertyChanged(nameof(CanRecover));
    }

    private void Finish(string sentence)
    {
        Status = sentence;
        Recovered = true;
    }
}
