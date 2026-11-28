using PaperRoute.ViewModels.Platform;

namespace PaperRoute.Desktop.Tests.Platform;

// Single instance in memory, for the start-up tests that need a hold without
// a real lock (start-up design 2.3; I3 and L1 use it). It keeps the contract
// of the real forms, which InstanceHoldTests checks against it: one hold per
// data root, a come-forward request that came before Listen delivered once at
// Listen on a thread of its own, and a release that may be called twice. It
// records every call, and plays a library held by another copy and a copy that
// cannot be reached. Data roots are compared as written: two spellings that
// differ only in case are two libraries here, where the Windows form (its key
// is upper-cased) and macOS's default volume see one.
internal sealed class FakeSingleInstance : ISingleInstance
{
    private readonly object _gate = new();
    private readonly Dictionary<string, FakeInstanceHold> _held = new(StringComparer.Ordinal);
    private readonly HashSet<string> _heldElsewhere = new(StringComparer.Ordinal);
    private readonly List<string> _calls = [];

    // False plays a copy that holds the library but cannot be reached.
    public bool Reachable { get; set; } = true;

    // Every call, in order: "hold <root>", "refused <root>", "ask <root>",
    // "release <root>".
    public IReadOnlyList<string> Calls
    {
        get
        {
            lock (_gate)
            {
                return [.. _calls];
            }
        }
    }

    // Another copy holds the library at the data root.
    public void HoldElsewhere(string dataRoot)
    {
        lock (_gate)
        {
            _heldElsewhere.Add(dataRoot);
        }
    }

    public IInstanceHold? TryHold(string dataRoot)
    {
        lock (_gate)
        {
            if (_held.ContainsKey(dataRoot) || _heldElsewhere.Contains(dataRoot))
            {
                _calls.Add("refused " + dataRoot);
                return null;
            }

            _calls.Add("hold " + dataRoot);
            var hold = new FakeInstanceHold(this, dataRoot);
            _held.Add(dataRoot, hold);
            return hold;
        }
    }

    public bool AskToComeForward(string dataRoot, TimeSpan limit)
    {
        FakeInstanceHold? hold;
        lock (_gate)
        {
            _calls.Add("ask " + dataRoot);
            if (!Reachable)
            {
                return false;
            }

            if (_heldElsewhere.Contains(dataRoot))
            {
                return true;
            }

            if (!_held.TryGetValue(dataRoot, out hold))
            {
                return false;
            }
        }

        hold.Ask();
        return true;
    }

    private void Release(FakeInstanceHold hold)
    {
        lock (_gate)
        {
            _calls.Add("release " + hold.DataRoot);
            _held.Remove(hold.DataRoot);
        }
    }

    // A hold that delivers on a thread of its own, one request at a time in
    // the order they came, as the interface promises and the real forms do
    // (the Windows listener thread, the Unix serving task): a callback that
    // touches a window without posting to it fails here as it would there.
    internal sealed class FakeInstanceHold(FakeSingleInstance owner, string dataRoot) : IInstanceHold
    {
        // How long Dispose waits for the delivery thread to end; only a
        // callback that never returns reaches it.
        private static readonly TimeSpan s_stopLimit = TimeSpan.FromSeconds(20);

        private readonly object _gate = new();
        private readonly List<Exception> _thrown = [];
        private Thread? _delivering;
        private bool _listening;
        private int _waiting;
        private bool _disposed;

        public string DataRoot { get; } = dataRoot;

        public bool Released
        {
            get
            {
                lock (_gate)
                {
                    return _disposed;
                }
            }
        }

        // The thread Dispose first ran on, or null before it ran.
        public int? ReleasedOnThread { get; private set; }

        // What comeForward threw, in order. The delivery thread goes on, and a
        // test reads it here instead of losing its test host.
        public IReadOnlyList<Exception> Thrown
        {
            get
            {
                lock (_gate)
                {
                    return [.. _thrown];
                }
            }
        }

        public void Listen(Action comeForward)
        {
            ArgumentNullException.ThrowIfNull(comeForward);
            lock (_gate)
            {
                if (_listening)
                {
                    throw new InvalidOperationException("This hold is already listening.");
                }

                _listening = true;
                if (_disposed)
                {
                    return;
                }

                _delivering = new Thread(() => Deliver(comeForward)) { IsBackground = true, Name = "Fake come forward" };
                _delivering.Start();
            }
        }

        // Requests asked before the release are delivered, then the delivery
        // thread ends and is joined, as the Windows listener is.
        public void Dispose()
        {
            Thread? delivering;
            lock (_gate)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                ReleasedOnThread = Environment.CurrentManagedThreadId;
                delivering = _delivering;
                Monitor.PulseAll(_gate);
            }

            if (delivering is not null && delivering != Thread.CurrentThread)
            {
                delivering.Join(s_stopLimit);
            }

            owner.Release(this);
        }

        internal void Ask()
        {
            lock (_gate)
            {
                if (_disposed)
                {
                    return;
                }

                _waiting++;
                Monitor.PulseAll(_gate);
            }
        }

        private void Deliver(Action comeForward)
        {
            while (true)
            {
                lock (_gate)
                {
                    while (_waiting == 0 && !_disposed)
                    {
                        Monitor.Wait(_gate);
                    }

                    if (_waiting == 0)
                    {
                        return;
                    }

                    _waiting--;
                }

                try
                {
                    comeForward();
                }
                catch (Exception ex)
                {
                    lock (_gate)
                    {
                        _thrown.Add(ex);
                    }
                }
            }
        }
    }
}
