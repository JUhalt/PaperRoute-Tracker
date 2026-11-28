namespace PaperRoute.ViewModels.Startup;

// The steps of start-up (start-up design 2.4), one member each, which
// StartupSequence calls in today's order (Program.vb:31-90). The name in each
// comment is the step's record name: the tests' FakeStartupSteps records it
// when the step is called, so the tests read the order from the calls
// themselves, never from source text. The real steps come with the real
// start (build 6, I3); until then nothing outside the tests runs a sequence.
//
// A step keeps what it finds for the steps after it (the command line, the
// session, the hold). The sequence is given only what it decides with: the
// kind of start, the data root and whether this copy holds it.
public interface IStartupSteps
{
    // 1 "options": reads the command line and keeps it for the later steps.
    // It calls nothing in the core. Null refuses the command line: the step
    // has said why on standard error, and no other step runs.
    StartupMode? ReadOptions(IReadOnlyList<string> args);

    // 2 "platform": registers the system's own services, such as stored keys,
    // before anything could read a key (Program.vb:44).
    void RegisterPlatform();

    // 3 "session": starts the example's session, a check mode's, or the
    // session a plain start gets (Program.vb:49-50); which library a plain
    // start opens is the start-up design's question 4, not this step's.
    // Before any data root is found, so the session decides where every
    // root lies.
    void BeginSession();

    // 4 "data-root": finds the data root, once. It throws when the system
    // names no home folder (StorageEnvironment.UserFolder off Windows).
    string FindDataRoot();

    // 5 "error-reports": hooks the error reports to the process's unhandled
    // errors and to faulted tasks nobody awaited (Program.vb:56-62).
    void HookErrorReports();

    // 6 "single-instance": takes this copy's hold on the library at the data
    // root. Null when another copy holds it. Disposing the hold releases it.
    IDisposable? TryHold(string dataRoot);

    // 6 "ask-to-come-forward": asks the copy that holds the library to bring
    // its window forward, waiting no longer than the limit. False when it
    // could not be reached.
    bool AskToComeForward(string dataRoot, TimeSpan limit);

    // 7 "avalonia": runs the app with its windows, and returns the process's
    // exit code when the app ends.
    int RunApp();

    // The end, "end-session": ends the session that step 3 began, which
    // deletes the example's folder (Program.vb:88-90). Safe to call when
    // step 3 began nothing or failed partway.
    void EndSession();

    // "no-window-message": tells the person, before any window can exist,
    // why PaperRoute stops (map:377): the system's message box (user32
    // MessageBoxW) on Windows, the same words on standard error on Linux and
    // macOS. The sequence calls it only from step 4, so it is one of the
    // real steps 1 to 6 that I3 builds: I3's DesktopStartupSteps owns this
    // split and its test, which shows the form each system gets without
    // opening a box (for example, a child process on Ubuntu and macOS whose
    // data-root step fails, with exit code 1 and the exact sentence on
    // standard error, and a test that the Windows form is the box). The
    // sequence's own tests show only that the step is called with the text.
    void NoWindowMessage(string text);
}

// What kind of start the command line asked for: a person's start, or a
// check mode, which runs a fixed task with no person and exits with a code.
public enum StartupMode
{
    Person,
    Check,
}
