namespace PaperRoute.ViewModels.Shell;

// A page Ctrl+S can save (shell design 2.3). Optional: a page that does not
// save does not implement it, and Ctrl+S then passes on, as today's key does
// off the manuscript page (Form1.vb:2546-2551).
public interface ISavingPage
{
    bool CanSave { get; }

    Task SaveAsync();
}
