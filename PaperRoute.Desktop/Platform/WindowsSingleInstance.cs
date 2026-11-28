using System.Runtime.Versioning;
using ManuscriptPipeline.Services;
using PaperRoute.ViewModels.Platform;

namespace PaperRoute.Desktop.Platform;

// Single instance on Windows (start-up design 2.3, pull request I1): today's
// SingleInstanceService, unchanged, upper-cased key included. The key and
// the object names are PaperRoute 1.0.x's (SingleInstanceKeyPinTest), so 1.0.x
// and the new app refuse to open one library twice. Windows paths ignore
// case, so the upper-cased key is the case-correct rule here.
[SupportedOSPlatform("windows")]
public sealed class WindowsSingleInstance : ISingleInstance
{
    public IInstanceHold? TryHold(string dataRoot)
    {
        ArgumentNullException.ThrowIfNull(dataRoot);
        SingleInstanceService? service = SingleInstanceService.TryStart(SingleInstanceService.KeyFor(dataRoot));
        return service is null ? null : new Hold(dataRoot, service);
    }

    // The signal is a named event, set at once: the limit is not needed here.
    public bool AskToComeForward(string dataRoot, TimeSpan limit)
    {
        ArgumentNullException.ThrowIfNull(dataRoot);
        return SingleInstanceService.SignalExisting(SingleInstanceService.KeyFor(dataRoot));
    }

    private sealed class Hold(string dataRoot, SingleInstanceService service) : IInstanceHold
    {
        private int _listening;

        public string DataRoot { get; } = dataRoot;

        // The event stays set until the listener's first wait, so a request
        // that came before Listen is delivered once, at once.
        public void Listen(Action comeForward)
        {
            ArgumentNullException.ThrowIfNull(comeForward);
            if (Interlocked.Exchange(ref _listening, 1) != 0)
            {
                throw new InvalidOperationException("This hold is already listening.");
            }

            service.Listen(comeForward);
        }

        // On the calling thread, which must be the one that took the hold: a
        // release from another thread leaves the mutex owned by the taker for
        // as long as anyone has it open (TheHoldIsReleasedOnTheThreadThatTookIt).
        // The service ignores a second call.
        public void Dispose() => service.Dispose();
    }
}
