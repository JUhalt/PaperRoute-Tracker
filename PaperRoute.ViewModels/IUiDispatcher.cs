namespace PaperRoute.ViewModels;

// Work posted to the interface thread, to run once the work in hand and the
// messages already queued are done (d1 3.4). The Board posts the flush of a
// closed stage or sort list through it, as Form1 does with BeginInvoke
// (Form1.vb BoardFilterListClosed, :1957-1969). The Desktop implements it
// with Dispatcher.Post; tests queue the work and run it by hand.
public interface IUiDispatcher
{
    void Post(Action action);
}
