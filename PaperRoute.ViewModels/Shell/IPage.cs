namespace PaperRoute.ViewModels.Shell;

// The page contract (shell design 2.3): what the navigator asks of a page.
// Only the title is required; the rest have defaults, so a page that keeps
// nothing and never refuses a leave writes none of them.
public interface IPage
{
    // The page host's accessible name.
    string Title { get; }

    // Each time the page becomes the current one, with the request that made
    // it so: for the manuscript page, its manuscript and section. Also called
    // alone when another section of the manuscript shown is asked for. It
    // must not switch pages: a switch asked from here is refused.
    void Shown(PageRequest request)
    {
    }

    // Each time the page stops being the current one, once the leave was
    // allowed, and never on a refusal: stop timers and drop references, as
    // today's LeaveCurrentPage does (Form1.Shell.vb:829-848). It must not
    // switch pages: a switch asked from here is refused.
    void Hidden()
    {
    }

    // The leave guard: may the page be left now? Asked before every page
    // change, Back, Forward and the window's close. A page with unsaved work
    // answers false to stay (today only the manuscript page has one).
    ValueTask<bool> CanLeaveAsync() => ValueTask.FromResult(true);
}
