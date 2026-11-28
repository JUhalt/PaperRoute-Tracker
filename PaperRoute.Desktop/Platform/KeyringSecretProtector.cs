using System.Security.Cryptography;
using ManuscriptPipeline;
using ManuscriptPipeline.Services;

namespace PaperRoute.Desktop.Platform;

// Stored keys on Linux and macOS (start-up design 2.7, pull request K1). The
// keyring keeps one random 256-bit master key per user and profile (service
// "PaperRoute", account StorageEnvironment.DataFolderName(), so the
// development profile has its own), and the master key seals the key files
// (KeyFileCipher). ProtectedKeyStore and its files stay as they are.
//
// The rules, each with its test in KeyringProtectorTests:
// - Lazy: nothing touches the keyring until a key is first protected or read.
//   Then the master key is fetched once for the run and kept in memory; one
//   fetch runs at a time, and other callers wait for it.
// - Store, read back, then use: when the keyring holds no master key, a new
//   one is made, stored, and read back, and only what came back is used. If
//   any step fails, Protect throws and the key store writes no file. So no key
//   file is ever sealed with a key the keyring does not hold.
// - Only a copy that holds its library makes a master key: until
//   AllowMasterKeyCreation (start-up step 6, after a successful hold), "no
//   such item" reads as "no key". Locked, unreachable, refused or no answer
//   never make one, because a new key would orphan every key file written.
// - Two time limits: the availability probe gets ten seconds, and no answer
//   means not available; the lookup and the store get none, because a locked
//   keyring may be showing its own unlock prompt. Both run on a worker.
// - The interface thread never waits for the keyring: there, when the master
//   key is not in memory yet, Unprotect reads as no key and Protect says the
//   keyring is being asked, the fetch starts on a worker, and MasterKeyReady
//   says when to try again.
// - A failed fetch is asked again at the next use after a minute.
//
// Every failure is a CryptographicException in Unprotect, which the key store
// reads as "no key", and an InvalidOperationException with the reason in
// Protect, the type the core's own unavailable protector throws.
public sealed class KeyringSecretProtector : ISecretProtector, IExplainsAvailability
{
    public const string Service = "PaperRoute";

    public const string AskingTheKeyring = "PaperRoute is asking the keyring for access; try again in a moment.";

    public static readonly TimeSpan ProbeLimit = TimeSpan.FromSeconds(10);

    public static readonly TimeSpan RetryAfter = TimeSpan.FromSeconds(60);

    private const string NoAnswer = "PaperRoute can't keep keys on this computer: the keyring did not answer.";
    private const string CouldNotAsk = "PaperRoute could not ask the keyring.";
    private const string NoLibraryHeld = "PaperRoute keeps keys only for the library it has open, and none is open yet.";
    private const string NoMasterKey = "The keyring holds no PaperRoute key yet.";
    private const string NotKept = "The keyring did not keep PaperRoute's key, so no key was saved.";
    private const string Unusable = "The keyring holds a PaperRoute key PaperRoute cannot use, so no key was saved.";

    private readonly IKeyring _keyring;
    private readonly Func<bool> _isInterfaceThread;
    private readonly TimeProvider _clock;
    private readonly TimeSpan _probeLimit;
    private readonly object _gate = new();

    private byte[]? _masterKey;
    private bool _mayCreate;
    private Task<Outcome>? _fetch;
    private string? _failure;
    private DateTimeOffset _failedAt;
    private Task<string?>? _probe;
    private DateTimeOffset _probedAt;
    private int _readyRaised;

    // The account, the clock and the probe's limit are for tests; the app
    // passes the keyring and Dispatcher.UIThread.CheckAccess only.
    public KeyringSecretProtector(
        IKeyring keyring,
        Func<bool> isInterfaceThread,
        string? account = null,
        TimeProvider? clock = null,
        TimeSpan? probeLimit = null)
    {
        ArgumentNullException.ThrowIfNull(keyring);
        ArgumentNullException.ThrowIfNull(isInterfaceThread);
        _keyring = keyring;
        _isInterfaceThread = isInterfaceThread;
        Account = account ?? StorageEnvironment.DataFolderName();
        _clock = clock ?? TimeProvider.System;
        _probeLimit = probeLimit ?? ProbeLimit;
    }

    // Raised once, on a worker, when the master key is first in memory, so a
    // view that was told "try again in a moment" can refresh (#435's Settings).
    public event EventHandler? MasterKeyReady;

    // The keyring account the master key is kept under.
    public string Account { get; }

    public bool IsAvailable => WhyNotAvailable is null;

    // Asks the probe, once a run while it says yes. On the interface thread it
    // does not wait: an unanswered probe reads as the keyring being asked.
    public string? WhyNotAvailable
    {
        get
        {
            if (!KeyFileCipher.IsSupported)
            {
                return KeyFileCipher.NotSupported;
            }

            Task<string?> probe = Probe();
            if (!probe.IsCompleted && _isInterfaceThread())
            {
                return AskingTheKeyring;
            }

            return probe.GetAwaiter().GetResult();
        }
    }

    // Start-up step 6, after this copy took the hold on its library.
    public void AllowMasterKeyCreation()
    {
        lock (_gate)
        {
            _mayCreate = true;
        }
    }

    public byte[] Protect(byte[] clear)
    {
        ArgumentNullException.ThrowIfNull(clear);
        return KeyFileCipher.Encrypt(MasterKey(protecting: true), clear);
    }

    public byte[] Unprotect(byte[] stored)
    {
        ArgumentNullException.ThrowIfNull(stored);
        return KeyFileCipher.Decrypt(MasterKey(protecting: false), stored);
    }

    private byte[] MasterKey(bool protecting)
    {
        if (!KeyFileCipher.IsSupported)
        {
            throw Failure(protecting, KeyFileCipher.NotSupported);
        }

        while (true)
        {
            Task<Outcome> fetch;
            lock (_gate)
            {
                if (_masterKey is not null)
                {
                    return _masterKey;
                }

                if (_fetch is null)
                {
                    if (_failure is not null && _clock.GetUtcNow() - _failedAt < RetryAfter)
                    {
                        throw Failure(protecting, _failure);
                    }

                    // Only a Protect may make the master key: a read has no
                    // file a new key could open.
                    bool create = protecting && _mayCreate;
                    _fetch = Task.Run(() => Fetch(create));
                }

                fetch = _fetch;
            }

            if (!fetch.IsCompleted && _isInterfaceThread())
            {
                throw Failure(protecting, AskingTheKeyring);
            }

            Outcome outcome = fetch.GetAwaiter().GetResult();
            if (outcome.Key is not null)
            {
                return outcome.Key;
            }

            if (outcome.Failure is not null)
            {
                throw Failure(protecting, outcome.Failure);
            }

            // The keyring holds no master key, and that fetch made none.
            if (!protecting)
            {
                throw new CryptographicException(NoMasterKey);
            }

            lock (_gate)
            {
                if (!_mayCreate)
                {
                    throw new InvalidOperationException(NoLibraryHeld);
                }
            }

            // Creation was allowed after that fetch started: the next one makes it.
        }
    }

    // Runs on a worker and never throws. It sets the state before its task
    // ends, so a caller that did not wait finds the key on its next call.
    private Outcome Fetch(bool create)
    {
        Outcome outcome;
        try
        {
            outcome = Ask(create);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            outcome = Outcome.Failed(CouldNotAsk);
        }

        bool ready = false;
        lock (_gate)
        {
            _fetch = null;
            if (outcome.Key is not null)
            {
                _masterKey = outcome.Key;
                ready = true;
            }
            else if (outcome.Failure is not null)
            {
                _failure = outcome.Failure;
                _failedAt = _clock.GetUtcNow();
            }
        }

        if (ready && Interlocked.Exchange(ref _readyRaised, 1) == 0)
        {
            MasterKeyReady?.Invoke(this, EventArgs.Empty);
        }

        return outcome;
    }

    private Outcome Ask(bool create)
    {
        string? whyNot = Probe().GetAwaiter().GetResult();
        if (whyNot is not null)
        {
            return Outcome.Failed(whyNot);
        }

        KeyringAnswer answer = _keyring.TryGet(Service, Account, out byte[]? found);
        if (answer == KeyringAnswer.Found)
        {
            return Usable(found);
        }

        if (answer != KeyringAnswer.NotFound)
        {
            return Outcome.Failed(Reason(answer));
        }

        if (!create)
        {
            return Outcome.Absent;
        }

        // Make, store, read back; use only what came back.
        byte[] made = RandomNumberGenerator.GetBytes(KeyFileCipher.KeySize);
        try
        {
            answer = _keyring.TrySet(Service, Account, made);
            if (answer != KeyringAnswer.Found)
            {
                return Outcome.Failed(answer == KeyringAnswer.NotFound ? NotKept : Reason(answer));
            }

            answer = _keyring.TryGet(Service, Account, out byte[]? back);
            if (answer != KeyringAnswer.Found)
            {
                return Outcome.Failed(answer == KeyringAnswer.NotFound ? NotKept : Reason(answer));
            }

            return Usable(back);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(made);
        }
    }

    // One probe a run while it says yes; a "no" is asked again after a minute.
    private Task<string?> Probe()
    {
        lock (_gate)
        {
            if (_probe is null ||
                (_probe.IsCompleted && _probe.Result is not null && _clock.GetUtcNow() - _probedAt >= RetryAfter))
            {
                _probe = Task.Run(RunProbe);
            }

            return _probe;
        }
    }

    private string? RunProbe()
    {
        string? whyNot;
        try
        {
            Task<string?> asking = Task.Run(_keyring.WhyNotReachable);
            whyNot = asking.Wait(_probeLimit) ? asking.Result : NoAnswer;
        }
        catch (AggregateException)
        {
            whyNot = CouldNotAsk;
        }

        lock (_gate)
        {
            _probedAt = _clock.GetUtcNow();
        }

        return whyNot;
    }

    private static Outcome Usable(byte[]? key) =>
        key is { Length: KeyFileCipher.KeySize } ? Outcome.Of([.. key]) : Outcome.Failed(Unusable);

    private static string Reason(KeyringAnswer answer) => answer switch
    {
        KeyringAnswer.Locked => "The keyring is locked, so PaperRoute could not use it. Unlock it and try again.",
        KeyringAnswer.Unreachable => "PaperRoute can't keep keys on this computer: no keyring is running.",
        KeyringAnswer.Refused => "The keyring refused PaperRoute's request.",
        _ => CouldNotAsk,
    };

    private static Exception Failure(bool protecting, string reason) =>
        protecting ? new InvalidOperationException(reason) : new CryptographicException(reason);

    private sealed record Outcome(byte[]? Key, string? Failure)
    {
        public static readonly Outcome Absent = new(null, null);

        public static Outcome Of(byte[] key) => new(key, null);

        public static Outcome Failed(string reason) => new(null, reason);
    }
}
