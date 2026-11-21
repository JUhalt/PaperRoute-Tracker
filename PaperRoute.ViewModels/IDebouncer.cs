namespace PaperRoute.ViewModels;

// A restartable one-shot timer on the interface thread (d1 3.4): the Board's
// search waits 200 ms after the last keystroke (Form1.vb
// boardSearchDebounceTimer, :79-81). The Desktop implements it with a
// DispatcherTimer; tests fire it by hand, so no test waits.
public interface IDebouncer
{
    // Starts the wait again; action runs once when it ends.
    void Restart(TimeSpan delay, Action action);

    // Drops a pending run.
    void Stop();
}
