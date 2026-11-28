namespace PaperRoute.ViewModels.Shell;

// How the window is being closed (shell design 3.1 row 5): by the person or
// the program, or by the operating system's shutdown or log-off, which a
// close must never refuse or hold up with a question.
public enum CloseKind
{
    Ordinary,
    Shutdown,
}
