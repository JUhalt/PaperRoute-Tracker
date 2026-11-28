namespace PaperRoute.ViewModels.Platform;

// One running copy per library (the plan's single instance, start-up design
// 2.3, pull request I1 of build 6). A copy takes a hold on its library's data
// root before any storage step; a second copy started on the same library
// finds the hold taken, asks the first copy to bring its window forward, and
// exits before touching storage (Program.vb:64-72).
//
// The forms live in PaperRoute.Desktop: on Windows today's mutex and event
// with PaperRoute 1.0.x's key, so the two apps refuse to open one library
// twice; on Linux and macOS a lock file beside the data root, which is the
// guarantee, and a local socket that only carries "come forward".
public interface ISingleInstance
{
    // This copy's hold on the library at the data root, or null when another
    // copy holds it.
    IInstanceHold? TryHold(string dataRoot);

    // Asks the copy that holds the library at the data root to bring its
    // window forward, waiting no longer than the limit. False when no copy
    // could be reached; the hold, not this answer, decides whether a library
    // is open elsewhere.
    bool AskToComeForward(string dataRoot, TimeSpan limit);
}

// A copy's hold on one library. Dispose releases it, on the thread that took
// it (the Windows mutex belongs to that thread, SingleInstanceService.vb:
// 105-107); a second call does nothing.
public interface IInstanceHold : IDisposable
{
    // The data root this hold was taken for, as it was given.
    string DataRoot { get; }

    // Calls comeForward, on a background thread, each time another copy asks
    // this one to come forward; the caller posts it to its window. A request
    // that came before Listen, while the storage step ran, is delivered once,
    // at once. Called once per hold.
    void Listen(Action comeForward);
}
