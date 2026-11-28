using PaperRoute.ViewModels.Notices;

namespace PaperRoute.ViewModels.Session;

// The trial's form of the plan's dialog-and-notice service (d1 3.1): notices
// only. The trial opens no dialog. Notice gives a result, which replaces the
// status line; Show and Withdraw give and take back a notice that stays
// (shell design 2.4). Questions come with the issues that bring each one.
public interface IInteractions
{
    void Notice(string text);

    void Show(StandingNotice notice);

    void Withdraw(string key);
}
