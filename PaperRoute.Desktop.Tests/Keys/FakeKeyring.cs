using System.Security.Cryptography;
using PaperRoute.Desktop.Platform;

namespace PaperRoute.Desktop.Tests.Keys;

// A keyring in memory for the keyring protector's tests (start-up design 2.7):
// it plays each answer, a delay (a gate the test opens), a throw, a store that
// silently keeps nothing, and one that keeps another key. It counts every
// call. Every secret in it is made by the test or the protector, never
// printed: a failure message names a key by its name only.
internal sealed class FakeKeyring : IKeyring
{
    private readonly object _gate = new();
    private readonly Dictionary<(string Service, string Account), byte[]> _items = [];
    private readonly List<byte[]> _offered = [];
    private int _probeCalls;
    private int _getCalls;
    private int _setCalls;

    // What the probe says: null when a keyring answers.
    public string? WhyNot { get; set; }

    // When set, every TryGet answers this instead of looking.
    public KeyringAnswer? GetAnswer { get; set; }

    // When set, every TrySet answers this and keeps nothing.
    public KeyringAnswer? SetAnswer { get; set; }

    // Every lookup and store throws (the probe still answers).
    public bool Throws { get; set; }

    public StoreBehaviour Store { get; set; } = StoreBehaviour.Keeps;

    // Closed: the probe, or the lookup, waits until the test opens it.
    public ManualResetEventSlim? ProbeGate { get; set; }

    public ManualResetEventSlim? GetGate { get; set; }

    // Set when a lookup has started.
    public ManualResetEventSlim GetEntered { get; } = new(false);

    public int ProbeCalls => Volatile.Read(ref _probeCalls);

    public int GetCalls => Volatile.Read(ref _getCalls);

    public int SetCalls => Volatile.Read(ref _setCalls);

    public int Calls => ProbeCalls + GetCalls + SetCalls;

    // The keys the protector offered to TrySet, as copies.
    public IReadOnlyList<byte[]> Offered
    {
        get
        {
            lock (_gate)
            {
                return [.. _offered];
            }
        }
    }

    public void Hold(string account, byte[] secret)
    {
        lock (_gate)
        {
            _items[(KeyringSecretProtector.Service, account)] = [.. secret];
        }
    }

    public byte[]? Held(string account)
    {
        lock (_gate)
        {
            return _items.TryGetValue((KeyringSecretProtector.Service, account), out byte[]? secret) ? [.. secret] : null;
        }
    }

    public string? WhyNotReachable()
    {
        Interlocked.Increment(ref _probeCalls);
        ProbeGate?.Wait();
        return WhyNot;
    }

    public KeyringAnswer TryGet(string service, string account, out byte[]? secret)
    {
        Interlocked.Increment(ref _getCalls);
        GetEntered.Set();
        GetGate?.Wait();
        secret = null;
        if (Throws)
        {
            throw new InvalidOperationException("The fake keyring was told to throw.");
        }

        if (GetAnswer is KeyringAnswer forced)
        {
            return forced;
        }

        lock (_gate)
        {
            if (!_items.TryGetValue((service, account), out byte[]? held))
            {
                return KeyringAnswer.NotFound;
            }

            secret = [.. held];
            return KeyringAnswer.Found;
        }
    }

    public KeyringAnswer TrySet(string service, string account, byte[] secret)
    {
        Interlocked.Increment(ref _setCalls);
        if (Throws)
        {
            throw new InvalidOperationException("The fake keyring was told to throw.");
        }

        lock (_gate)
        {
            _offered.Add([.. secret]);
            if (SetAnswer is KeyringAnswer forced)
            {
                return forced;
            }

            switch (Store)
            {
                case StoreBehaviour.KeepsNothing:
                    break;
                case StoreBehaviour.KeepsAnother:
                    _items[(service, account)] = RandomNumberGenerator.GetBytes(secret.Length);
                    break;
                default:
                    _items[(service, account)] = [.. secret];
                    break;
            }

            return KeyringAnswer.Found;
        }
    }

    internal enum StoreBehaviour
    {
        Keeps,

        // Says it stored the key and keeps nothing.
        KeepsNothing,

        // Says it stored the key and keeps another one (as if another copy
        // stored its own key in between).
        KeepsAnother,
    }
}
