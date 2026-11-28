namespace PaperRoute.ViewModels.Shell;

// The one way to switch pages (shell design 2.3): from the rail, a key, a
// link, a notice button or history. Today's NavigateToRequest, GoBack and
// GoForward (Form1.Shell.vb:480-530, 777-821), without the window.
public interface INavigator
{
    // The page shown and the request that showed it; null before the first.
    PageRequest? Current { get; }

    IPage? CurrentPage { get; }

    // The two history stacks, the entry Back or Forward takes first. Kept in
    // memory only, with no cap, as today (3.1 row 19).
    IReadOnlyList<PageRequest> BackHistory { get; }

    IReadOnlyList<PageRequest> ForwardHistory { get; }

    // The page the manuscript page was entered from, which the rail checks
    // while a manuscript is shown: set when the manuscript page is entered
    // from another page, kept from one manuscript to the next (3.1 row 8).
    // The Board before any manuscript is shown, as today.
    PageKey Origin { get; }

    // The entry Back will reach, skipping manuscripts the library no longer
    // holds, so a back link can name it truly (3.1 row 30); null when Back
    // has nowhere to go.
    PageRequest? BackTarget { get; }

    // Shows the page asked for. The page shown is asked first whether it may
    // be left; a refusal changes nothing and returns false. A recorded move
    // puts the page left on Back and clears Forward. The page already shown
    // (another section of the manuscript shown included) records nothing and
    // returns true. While a move is under way (its leave guard, the page
    // being made, Hidden and Shown), another move is refused (returns false)
    // and changes nothing, so a page cannot switch pages from its Shown or
    // Hidden.
    Task<bool> GoAsync(PageRequest request, bool recordHistory = true);

    // Back and Forward skip manuscripts the library no longer holds. A
    // refusal puts nothing out of place; with nowhere to go they do nothing.
    Task<bool> GoBackAsync();

    Task<bool> GoForwardAsync();
}
