using ManuscriptPipeline;

namespace PaperRoute.Desktop.Tests.ManuscriptPage;

// The core's clock fixed at one instant until disposed. The assembly's tests
// run one at a time (AssemblySetup), so no other test reads it meanwhile.
internal sealed class FixedClock : TimeProvider, IDisposable
{
    private readonly TimeProvider _before = PlatformServices.Clock;
    private readonly DateTimeOffset _now;

    public FixedClock(DateTimeOffset now)
    {
        _now = now;
        PlatformServices.Clock = this;
    }

    public DateTime Local => _now.UtcDateTime.ToLocalTime();

    public override DateTimeOffset GetUtcNow() => _now;

    public void Dispose() => PlatformServices.Clock = _before;
}
