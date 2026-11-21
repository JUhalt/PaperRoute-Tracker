namespace PaperRoute.ViewModels.Session;

// The trial's form of the plan's dialog-and-notice service (d1 3.1): notices
// only. The trial opens no dialog.
public interface IInteractions
{
    void Notice(string text);
}
