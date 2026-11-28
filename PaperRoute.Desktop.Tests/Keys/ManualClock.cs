namespace PaperRoute.Desktop.Tests.Keys;

// A clock that moves only when a test moves it, for the keyring protector's
// time limits (start-up design 2.7). A timer fires when Advance passes its
// time, on the thread that advanced the clock, so a test meets a limit of
// seconds or minutes without that time passing. A timer due at once fires at
// the next Advance; a periodic one fires at most once for each Advance.
internal sealed class ManualClock : TimeProvider
{
    private readonly object _gate = new();
    private readonly List<ManualTimer> _timers = [];
    private DateTimeOffset _now = new(2026, 11, 28, 12, 0, 0, TimeSpan.Zero);

    // Timers set and not yet fired, changed off or disposed.
    public int Pending
    {
        get
        {
            lock (_gate)
            {
                return _timers.Count;
            }
        }
    }

    public override DateTimeOffset GetUtcNow()
    {
        lock (_gate)
        {
            return _now;
        }
    }

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new ManualTimer(this, callback, state);
        timer.Change(dueTime, period);
        return timer;
    }

    public void Advance(TimeSpan by)
    {
        List<ManualTimer> due;
        lock (_gate)
        {
            _now += by;
            due = [.. _timers.Where(timer => timer.DueAt <= _now)];
            foreach (ManualTimer timer in due)
            {
                if (timer.Period > TimeSpan.Zero)
                {
                    timer.DueAt = _now + timer.Period;
                }
                else
                {
                    _timers.Remove(timer);
                }
            }
        }

        // Outside the lock: a callback may read the clock or set a timer.
        foreach (ManualTimer timer in due)
        {
            timer.Fire();
        }
    }

    private sealed class ManualTimer(ManualClock clock, TimerCallback callback, object? state) : ITimer
    {
        public DateTimeOffset DueAt { get; set; }

        public TimeSpan Period { get; private set; }

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            lock (clock._gate)
            {
                clock._timers.Remove(this);
                if (dueTime == Timeout.InfiniteTimeSpan)
                {
                    return true;
                }

                Period = period == Timeout.InfiniteTimeSpan ? TimeSpan.Zero : period;
                DueAt = clock._now + dueTime;
                clock._timers.Add(this);
                return true;
            }
        }

        public void Fire() => callback(state);

        public void Dispose()
        {
            lock (clock._gate)
            {
                clock._timers.Remove(this);
            }
        }

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
