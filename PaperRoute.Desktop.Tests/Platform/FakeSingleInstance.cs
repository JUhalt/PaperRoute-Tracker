using PaperRoute.ViewModels.Platform;

namespace PaperRoute.Desktop.Tests.Platform;

// Single instance in memory, for the start-up tests that need a hold without
// a real lock (start-up design 2.3; I3 and L1 use it). It keeps the contract
// of the real forms, which InstanceHoldTests checks against it: one hold per
// data root, a come-forward request that came before Listen delivered once at
// Listen, and a release that may be called twice. It records every call, and
// plays a library held by another copy and a copy that cannot be reached.
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

    internal sealed class FakeInstanceHold(FakeSingleInstance owner, string dataRoot) : IInstanceHold
    {
        private readonly object _gate = new();
        private Action? _comeForward;
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

        public void Listen(Action comeForward)
        {
            ArgumentNullException.ThrowIfNull(comeForward);
            int waiting;
            lock (_gate)
            {
                if (_comeForward is not null)
                {
                    throw new InvalidOperationException("This hold is already listening.");
                }

                _comeForward = comeForward;
                waiting = _waiting;
                _waiting = 0;
            }

            for (int i = 0; i < waiting; i++)
            {
                comeForward();
            }
        }

        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                ReleasedOnThread = Environment.CurrentManagedThreadId;
            }

            owner.Release(this);
        }

        internal void Ask()
        {
            Action? comeForward;
            lock (_gate)
            {
                if (_disposed)
                {
                    return;
                }

                comeForward = _comeForward;
                if (comeForward is null)
                {
                    _waiting++;
                    return;
                }
            }

            comeForward();
        }
    }
}
