using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace PaperRoute.ViewModels.Notices;

// The notice strip's model (shell design 2.4): the status line and the
// standing notices. Each result replaces the status line, as today's status
// line does (Form1.vb:647-664); a standing notice stays until withdrawn, and
// results still show beside it, where today the failed-save warning hides
// every other text (Form1.vb:655-661). Standing notices show above the status
// line: Failure first, then Warning, then Info, the newest first within a
// kind. Used on the interface thread, as every view model is.
public sealed class NoticeStripViewModel : ObservableObject
{
    private readonly ObservableCollection<StandingNotice> _standing = [];
    private string _status = string.Empty;

    public NoticeStripViewModel()
    {
        StandingNotices = new ReadOnlyObservableCollection<StandingNotice>(_standing);
    }

    // The status line: the last result given.
    public string Status
    {
        get => _status;
        private set => SetProperty(ref _status, value);
    }

    // In the order they show, top first.
    public ReadOnlyObservableCollection<StandingNotice> StandingNotices { get; }

    // A result replaces the status line; null becomes empty, as the trial's
    // shell had it. Standing notices are left as they are.
    public void Notice(string? text) => Status = text ?? string.Empty;

    // Shows a notice that stays. One already shown under the same key goes,
    // and the new one goes in as the newest of its kind, above the others of
    // that kind.
    public void Show(StandingNotice notice)
    {
        ArgumentNullException.ThrowIfNull(notice);
        Withdraw(notice.Key);
        int place = 0;
        while (place < _standing.Count && _standing[place].Kind > notice.Kind)
        {
            place++;
        }

        _standing.Insert(place, notice);
    }

    // Removes the notice shown under this key; a key not shown changes nothing.
    public void Withdraw(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        for (int i = 0; i < _standing.Count; i++)
        {
            if (string.Equals(_standing[i].Key, key, StringComparison.Ordinal))
            {
                _standing.RemoveAt(i);
                return;
            }
        }
    }
}
