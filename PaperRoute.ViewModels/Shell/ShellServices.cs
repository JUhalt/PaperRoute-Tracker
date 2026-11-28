using PaperRoute.ViewModels.Session;

namespace PaperRoute.ViewModels.Shell;

// What a page's factory is given (shell design 2.1, 2.3): the library
// session, notices, the navigator (the one way to switch pages), and the
// interface thread's dispatcher and debouncer.
public sealed record ShellServices(
    ILibrarySession Session,
    IInteractions Interactions,
    INavigator Navigator,
    IUiDispatcher Dispatcher,
    IDebouncer Debouncer);
