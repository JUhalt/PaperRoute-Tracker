namespace PaperRoute.ViewModels.Platform;

// The three things "--platform-check" (d1 1.3 Modes, step 15) asks the
// system to do, behind one small interface: the real ones are a thin class in
// PaperRoute.Desktop (Avalonia's StorageProvider and Launcher, and
// System.Diagnostics.Process) that no test runs; the tests give stand-ins.
public interface IPlatformActions
{
    // False when the platform has no file dialog (StorageProvider.CanOpen).
    bool CanOpenFileDialog { get; }

    // Opens the system's file dialog once and returns the names of the items
    // the person chose (empty when they cancelled). The check only counts
    // them: nothing chosen is opened or read, and no name is kept.
    Task<IReadOnlyList<string>> OpenFileDialogAsync();

    // Asks the system to open the address in the person's browser; true when
    // the system reports that it did.
    Task<bool> LaunchAsync(Uri address);

    // Starts the program file with the arguments, its output and errors read
    // and dropped, and no window of its own. Throws when it cannot start.
    IStartedProgram StartProgram(string fileName, IReadOnlyList<string> arguments);
}

// A program StartProgram started.
public interface IStartedProgram : IDisposable
{
    // True when the program exited within the limit.
    Task<bool> WaitForExitAsync(TimeSpan limit);

    // Its exit code, once it has exited.
    int ExitCode { get; }

    // Stops it, and the processes it started.
    void Stop();
}
