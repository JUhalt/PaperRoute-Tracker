using System.Security.Cryptography;
using ManuscriptPipeline;

namespace PaperRoute.Desktop.Platform;

// Keys for a session that is not the person's own library: the example
// library and the check modes (start-up design 2.7, pull request K1). The
// key that seals their key files is made in memory for this run and never
// leaves it, so the keyring is never touched, and a key kept there cannot be
// read by any later run. Their keys folder goes with their session anyway
// (ExampleLibraryService.EndSession), so nothing is lost.
public sealed class SessionKeyProtector : ISecretProtector, IExplainsAvailability
{
    private readonly byte[] _key = RandomNumberGenerator.GetBytes(KeyFileCipher.KeySize);

    public bool IsAvailable => WhyNotAvailable is null;

    public string? WhyNotAvailable => KeyFileCipher.IsSupported ? null : KeyFileCipher.NotSupported;

    // The protector off Windows for a session: this one for an isolated
    // session (the example, the check modes), without asking for the
    // keyring's; the keyring's, made only when asked, for the own library.
    // Registration on Linux and macOS (pull requests K2 and K3) calls it.
    public static ISecretProtector ForSession(bool isolatedSession, Func<ISecretProtector> keyringProtector)
    {
        ArgumentNullException.ThrowIfNull(keyringProtector);
        return isolatedSession ? new SessionKeyProtector() : keyringProtector();
    }

    public byte[] Protect(byte[] clear)
    {
        ArgumentNullException.ThrowIfNull(clear);
        return KeyFileCipher.Encrypt(_key, clear);
    }

    public byte[] Unprotect(byte[] stored)
    {
        ArgumentNullException.ThrowIfNull(stored);
        return KeyFileCipher.Decrypt(_key, stored);
    }
}
