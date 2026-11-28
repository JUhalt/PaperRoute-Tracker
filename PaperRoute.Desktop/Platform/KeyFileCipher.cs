using System.Security.Cryptography;

namespace PaperRoute.Desktop.Platform;

// The format of a key file kept by the protectors that do not use DPAPI
// (start-up design 2.7, pull request K1): the four bytes "PRK1", a 12-byte
// random nonce, the 16-byte tag, then the ciphertext, sealed with AES-GCM
// (System.Security.Cryptography.AesGcm) under a 256-bit key, the header as
// associated data. ProtectedKeyStore writes and reads these bytes as they are.
//
// Any file that does not open with the key, whatever the reason (another
// header, such as a DPAPI file copied from Windows; a changed byte; a cut
// file; another key), is refused with a CryptographicException, which the key
// store reads as "no key". Nothing here touches a keyring or a file.
public static class KeyFileCipher
{
    public const int KeySize = 32;
    public const int NonceSize = 12;
    public const int TagSize = 16;
    public const int HeaderSize = 4;

    // Header, nonce and tag: what a file holds beyond its ciphertext.
    public const int Overhead = HeaderSize + NonceSize + TagSize;

    // Said when this system's .NET cannot do AES-GCM.
    public const string NotSupported = "PaperRoute can't keep keys on this computer: AES-GCM encryption is not available here.";

    public static ReadOnlySpan<byte> Header => "PRK1"u8;

    public static bool IsSupported => AesGcm.IsSupported;

    public static byte[] Encrypt(ReadOnlySpan<byte> key, ReadOnlySpan<byte> clear)
    {
        CheckKey(key);
        if (!IsSupported)
        {
            throw new InvalidOperationException(NotSupported);
        }

        byte[] file = new byte[Overhead + clear.Length];
        Header.CopyTo(file);
        Span<byte> nonce = file.AsSpan(HeaderSize, NonceSize);
        RandomNumberGenerator.Fill(nonce);
        using var aes = new AesGcm(key, TagSize);
        aes.Encrypt(nonce, clear, file.AsSpan(Overhead), file.AsSpan(HeaderSize + NonceSize, TagSize), Header);
        return file;
    }

    public static byte[] Decrypt(ReadOnlySpan<byte> key, ReadOnlySpan<byte> file)
    {
        CheckKey(key);
        if (!IsSupported)
        {
            throw new CryptographicException(NotSupported);
        }

        if (file.Length < Overhead || !file[..HeaderSize].SequenceEqual(Header))
        {
            throw new CryptographicException("This is not a PaperRoute key file.");
        }

        byte[] clear = new byte[file.Length - Overhead];
        using var aes = new AesGcm(key, TagSize);

        // A changed byte or another key fails the tag check
        // (AuthenticationTagMismatchException, a CryptographicException).
        aes.Decrypt(file.Slice(HeaderSize, NonceSize), file[Overhead..], file.Slice(HeaderSize + NonceSize, TagSize), clear, Header);
        return clear;
    }

    private static void CheckKey(ReadOnlySpan<byte> key)
    {
        if (key.Length != KeySize)
        {
            throw new ArgumentException($"A key file's key is {KeySize} bytes.", nameof(key));
        }
    }
}
