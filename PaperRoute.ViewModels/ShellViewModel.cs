using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PaperRoute.ViewModels.Session;

namespace PaperRoute.ViewModels;

// The window's view model. At this step it holds only the notice strip: the
// window shows "Loading…" at once, then the session's startup notice
// ("Loaded 7 manuscripts from local storage.") once the load is done (d1 3.4).
// The session is handed in by Main; nothing here starts one.
public sealed partial class ShellViewModel : ObservableObject, IInteractions
{
    public const string LoadingText = "Loading…";

    private readonly ILibrarySession _session;

    [ObservableProperty]
    private string _notice = LoadingText;

    public ShellViewModel(ILibrarySession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        _session = session;
    }

    void IInteractions.Notice(string text) => Notice = text ?? string.Empty;

    // Started from the interface thread; the session reads off it and the
    // await comes back here (no ConfigureAwait(false) in view-model code).
    [RelayCommand]
    private async Task LoadAsync(CancellationToken cancel)
    {
        Notice = LoadingText;
        await _session.LoadAsync(cancel);
        Notice = _session.StartupNotice;
    }
}
