using System.Security.Cryptography;
using System.Text;
using PaperRoute.Desktop.Platform;

namespace PaperRoute.Desktop.Tests.Keys;

// The key-file format of the keyring protectors (start-up design 2.7, pull
// request K1): "PRK1", a 12-byte nonce, the 16-byte tag, the ciphertext,
// sealed with AES-GCM (System.Security.Cryptography.AesGcm), the header as
// associated data. Every vector is made here, with keys made here; no key or
// file is printed, and a failure names what was checked, never a value.
[TestClass]
public sealed class KeyFileCipherTests
{
    private static readonly byte[] s_clear = Encoding.UTF8.GetBytes("https://example.test\ntest-key-made-in-the-test");

    [TestMethod]
    public void AFileRoundTrips()
    {
        byte[] key = RandomNumberGenerator.GetBytes(KeyFileCipher.KeySize);

        byte[] file = KeyFileCipher.Encrypt(key, s_clear);

        Assert.IsTrue(KeyFileCipher.Decrypt(key, file).AsSpan().SequenceEqual(s_clear), "A sealed key file did not open to the text it was sealed from.");
        Assert.IsTrue(KeyFileCipher.Decrypt(key, KeyFileCipher.Encrypt(key, [])).Length == 0, "An empty text did not round trip.");
    }

    // The layout, read by hand with AesGcm itself: a file the cipher writes
    // opens by the layout, and a file built by the layout opens with the
    // cipher. Two seals of one text differ, because each takes a new nonce.
    [TestMethod]
    public void TheLayoutIsHeaderNonceTagCiphertext()
    {
        byte[] key = RandomNumberGenerator.GetBytes(KeyFileCipher.KeySize);
        byte[] header = Encoding.ASCII.GetBytes("PRK1");

        byte[] file = KeyFileCipher.Encrypt(key, s_clear);

        Assert.AreEqual(4 + 12 + 16 + s_clear.Length, file.Length, "A key file is the header, the nonce, the tag and the ciphertext, nothing more.");
        Assert.IsTrue(file.AsSpan(0, 4).SequenceEqual(header), "A key file does not start with PRK1.");
        byte[] opened = new byte[s_clear.Length];
        using (var aes = new AesGcm(key, 16))
        {
            aes.Decrypt(file.AsSpan(4, 12), file.AsSpan(32), file.AsSpan(16, 16), opened, header);
        }

        Assert.IsTrue(opened.AsSpan().SequenceEqual(s_clear), "The cipher's file did not open by the layout.");

        byte[] nonce = RandomNumberGenerator.GetBytes(12);
        byte[] byHand = new byte[32 + s_clear.Length];
        header.CopyTo(byHand, 0);
        nonce.CopyTo(byHand, 4);
        using (var aes = new AesGcm(key, 16))
        {
            aes.Encrypt(nonce, s_clear, byHand.AsSpan(32), byHand.AsSpan(16, 16), header);
        }

        Assert.IsTrue(KeyFileCipher.Decrypt(key, byHand).AsSpan().SequenceEqual(s_clear), "A file built by the layout did not open with the cipher.");
        Assert.IsFalse(KeyFileCipher.Encrypt(key, s_clear).AsSpan().SequenceEqual(file), "Two seals of one text were the same: the nonce was not new.");
    }

    // Every byte, one at a time: the header, the nonce, the tag and the
    // ciphertext are all covered.
    [TestMethod]
    public void AChangedByteIsRefused()
    {
        byte[] key = RandomNumberGenerator.GetBytes(KeyFileCipher.KeySize);
        byte[] file = KeyFileCipher.Encrypt(key, s_clear);

        for (int i = 0; i < file.Length; i++)
        {
            byte[] changed = [.. file];
            changed[i] ^= 0x01;
            Assert.Throws<CryptographicException>(() => KeyFileCipher.Decrypt(key, changed), $"A file with byte {i} changed was not refused.");
        }
    }

    [TestMethod]
    public void AWrongKeyIsRefused()
    {
        byte[] key = RandomNumberGenerator.GetBytes(KeyFileCipher.KeySize);
        byte[] other = RandomNumberGenerator.GetBytes(KeyFileCipher.KeySize);
        byte[] file = KeyFileCipher.Encrypt(key, s_clear);

        // Any CryptographicException, as the key store asks: the exact type is
        // the provider's (CNG, OpenSSL or CryptoKit).
        Assert.Throws<CryptographicException>(() => KeyFileCipher.Decrypt(other, file), "A file opened with another key.");
        Assert.ThrowsExactly<ArgumentException>(() => KeyFileCipher.Decrypt(key.AsSpan(0, 16), file), "A key of the wrong size was used.");
    }

    // Every shorter length: a cut ciphertext fails the tag, and a file shorter
    // than the header, nonce and tag is refused before any decryption.
    [TestMethod]
    public void ATruncatedFileIsRefused()
    {
        byte[] key = RandomNumberGenerator.GetBytes(KeyFileCipher.KeySize);
        byte[] file = KeyFileCipher.Encrypt(key, s_clear);

        for (int length = 0; length < file.Length; length++)
        {
            byte[] cut = file[..length];
            Assert.Throws<CryptographicException>(() => KeyFileCipher.Decrypt(key, cut), $"A file cut to {length} of {file.Length} bytes was not refused.");
        }
    }
}
