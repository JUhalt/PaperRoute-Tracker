namespace PaperRoute.Desktop.Platform;

// What a keyring says to a request (start-up design 2.7, pull request K1).
public enum KeyringAnswer
{
    // TryGet: the item is there. TrySet: the keyring took the item.
    Found,

    // The keyring answered, and it holds no such item.
    NotFound,

    // The keyring is locked, and was not unlocked.
    Locked,

    // No keyring could be asked: none runs, or its library or service is missing.
    Unreachable,

    // The keyring, or the person at its prompt, said no.
    Refused,
}

// One user's keyring, reduced to what the keyring protector needs: one secret
// per service and account. The Secret Service on Linux (pull request K2) and
// the login keychain on macOS (pull request K3) implement it; tests use a
// fake. The protector calls every member on a worker, never on the interface
// thread.
public interface IKeyring
{
    // Whether a keyring can be asked at all: null when it can, else why not,
    // in a sentence a person can read. The protector waits for it ten seconds
    // at most (KeyringSecretProtector.ProbeLimit) and reads no answer as not
    // available.
    string? WhyNotReachable();

    // No time limit: a locked keyring may be showing its own unlock prompt
    // while a person types.
    KeyringAnswer TryGet(string service, string account, out byte[]? secret);

    // Stores the secret, replacing any item of that service and account.
    KeyringAnswer TrySet(string service, string account, byte[] secret);
}
