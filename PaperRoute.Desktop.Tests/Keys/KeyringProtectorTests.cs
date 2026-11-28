using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using ManuscriptPipeline;
using ManuscriptPipeline.Services;
using PaperRoute.Desktop.Platform;

namespace PaperRoute.Desktop.Tests.Keys;

// Stored keys off Windows (start-up design 2.7, pull request K1): the keyring
// protector over a fake keyring, through the core's own ProtectedKeyStore
// (its test constructor) where a key file is involved, and the session-only
// protector of the example library. No real keyring is used, so the class runs
// the same on all four runners. Every key and master key is made here and
// never printed: a failure names a key by its name, never by its value.
[TestClass]
public sealed class KeyringProtectorTests
{
    private const string Account = "PaperRoute-KeysTest";
    private const string Name = ProtectedKeyStore.OpenAlex;
    private const string NoKeyring = "PaperRoute can't keep keys on this computer: no keyring is running.";
    private static readonly TimeSpan s_wait = TimeSpan.FromSeconds(10);

    // Gates a test closed; all are opened at the end, so no worker stays blocked.
    private readonly List<ManualResetEventSlim> _gates = [];

    [TestCleanup]
    public void OpenEveryGate()
    {
        foreach (ManualResetEventSlim gate in _gates)
        {
            gate.Set();
        }
    }

    [TestMethod]
    public void AKeyRoundTrips()
    {
        var keyring = new FakeKeyring();
        string folder = NewFolder();
        var store = new ProtectedKeyStore(folder, Protector(keyring));
        string key = NewKey();
        string endpointKey = NewKey();

        store.Save(Name, key);
        store.SaveFor(ProtectedKeyStore.AssistantEndpoint, "https://example.test", endpointKey);

        Assert.IsTrue(store.Load(Name) == key, "The key 'openalex' did not come back as saved.");
        Assert.IsTrue(store.LoadFor(ProtectedKeyStore.AssistantEndpoint, "https://example.test") == endpointKey,
            "The key 'assistant-endpoint' did not come back with its address.");
        Assert.IsTrue(keyring.Held(Account) is { Length: KeyFileCipher.KeySize }, "The keyring holds no 256-bit master key after a key was saved.");

        var nextRun = new ProtectedKeyStore(folder, Protector(keyring));
        Assert.IsTrue(nextRun.Load(Name) == key, "The key 'openalex' was not read by the next run, with the master key from the keyring.");
    }

    [TestMethod]
    public void TheMasterKeyIsFetchedOncePerRun()
    {
        var keyring = new FakeKeyring();
        keyring.Hold(Account, RandomNumberGenerator.GetBytes(KeyFileCipher.KeySize));
        var store = new ProtectedKeyStore(NewFolder(), Protector(keyring));

        store.Save(Name, NewKey());
        store.Save(ProtectedKeyStore.Anthropic, NewKey());
        for (int i = 0; i < 3; i++)
        {
            Assert.IsTrue(store.HasKey(Name) && store.HasKey(ProtectedKeyStore.Anthropic), "A saved key did not read back.");
        }

        Assert.AreEqual(1, keyring.ProbeCalls, "The keyring's availability was asked more than once in one run.");
        Assert.AreEqual(1, keyring.GetCalls, "The master key was fetched more than once in one run.");
        Assert.AreEqual(0, keyring.SetCalls, "A master key was stored although the keyring held one.");
    }

    // What start-up does with the protector: make it (step 2) and allow it to
    // make a master key (step 6). Neither asks the keyring, so the Mac's
    // keychain question, if any, comes when a key is first needed.
    [TestMethod]
    public void NothingAtStartUpTouchesTheKeyring()
    {
        var keyring = new FakeKeyring();

        var protector = new KeyringSecretProtector(keyring, () => false);
        protector.MasterKeyReady += (_, _) => { };
        protector.AllowMasterKeyCreation();
        Thread.Sleep(200);

        Assert.AreEqual(0, keyring.Calls, "Making the protector, or allowing it to make a master key, asked the keyring.");
    }

    [TestMethod]
    public void OnlyNotFoundMakesANewMasterKey()
    {
        var empty = new FakeKeyring();
        new ProtectedKeyStore(NewFolder(), Protector(empty)).Save(Name, NewKey());
        Assert.AreEqual(1, empty.SetCalls, "A keyring that answered NotFound did not get one new master key.");
        Assert.AreEqual(2, empty.GetCalls, "The new master key was not read back once after it was stored.");

        var holding = new FakeKeyring();
        holding.Hold(Account, RandomNumberGenerator.GetBytes(KeyFileCipher.KeySize));
        new ProtectedKeyStore(NewFolder(), Protector(holding)).Save(Name, NewKey());
        Assert.AreEqual(0, holding.SetCalls, "A keyring that answered Found got a new master key.");
    }

    [TestMethod]
    [DataRow("Locked")]
    [DataRow("Unreachable")]
    [DataRow("Refused")]
    [DataRow("Throws")]
    [DataRow("NoKeyring")]
    [DataRow("Silent")]
    public void ALockedOrSilentKeyringNeverMakesANewMasterKey(string how)
    {
        FakeKeyring keyring = Failing(how);
        string folder = NewFolder();
        var clock = new ManualClock();
        var store = new ProtectedKeyStore(folder, Protector(keyring, clock: clock));

        Exception? failed = FastForward(clock, () => Catch(() => store.Save(Name, NewKey())));
        Assert.IsInstanceOfType<InvalidOperationException>(failed, $"A save through a keyring that is {how} did not fail.");

        Assert.AreEqual(0, keyring.SetCalls, $"A keyring that is {how} got a new master key.");
        Assert.IsEmpty(FilesIn(folder), $"A save through a keyring that is {how} left a file.");
    }

    // A good key file, then a protector whose keyring fails each way: the key
    // store reads no key and throws nothing. The protector may make a master
    // key, and still a read never stores one: only a save may.
    [TestMethod]
    [DataRow("Locked")]
    [DataRow("Unreachable")]
    [DataRow("Refused")]
    [DataRow("NotFound")]
    [DataRow("Throws")]
    [DataRow("NoKeyring")]
    [DataRow("Silent")]
    public void EveryKeyringFailureReadsAsNoKey(string how)
    {
        byte[] master = RandomNumberGenerator.GetBytes(KeyFileCipher.KeySize);
        string folder = NewFolder();
        Directory.CreateDirectory(folder);
        File.WriteAllBytes(Path.Combine(folder, Name + ".key"), KeyFileCipher.Encrypt(master, Encoding.UTF8.GetBytes(NewKey())));
        var good = new FakeKeyring();
        good.Hold(Account, master);
        Assert.IsTrue(new ProtectedKeyStore(folder, Protector(good)).HasKey(Name), "The key file made for this test does not read with its own master key.");

        FakeKeyring keyring = Failing(how);
        var clock = new ManualClock();
        var store = new ProtectedKeyStore(folder, Protector(keyring, clock: clock));

        Assert.IsTrue(FastForward(clock, () => store.Load(Name)) is null, $"The key 'openalex' was read through a keyring that is {how}.");
        Assert.IsFalse(FastForward(clock, () => store.HasKey(Name)), $"The key 'openalex' was reported kept through a keyring that is {how}.");
        Assert.AreEqual(0, keyring.SetCalls, $"Reading through a keyring that is {how} stored a master key.");
    }

    [TestMethod]
    public void ASaveWithNoKeyringSaysWhy()
    {
        var keyring = new FakeKeyring { WhyNot = NoKeyring };
        var protector = Protector(keyring);
        string folder = NewFolder();

        Assert.IsFalse(protector.IsAvailable, "Keys were said to be kept with no keyring running.");
        Assert.AreEqual(NoKeyring, protector.WhyNotAvailable, "The reason keys cannot be kept is not the keyring's.");
        InvalidOperationException refused = Assert.ThrowsExactly<InvalidOperationException>(() => new ProtectedKeyStore(folder, protector).Save(Name, NewKey()));
        Assert.AreEqual(NoKeyring, refused.Message, "A save with no keyring did not say why it failed.");
        Assert.IsEmpty(FilesIn(folder), "A save with no keyring left a file.");
        Assert.AreEqual(0, keyring.GetCalls, "The keyring was asked for the master key although it is not running.");
    }

    // A DPAPI file copied from Windows: version 1 and DPAPI's provider id
    // (df9d8cd0-1501-11d1-8c7a-00c04fc297eb), then bytes made here.
    [TestMethod]
    public void AWindowsKeyFileReadsAsNoKey()
    {
        var keyring = new FakeKeyring();
        keyring.Hold(Account, RandomNumberGenerator.GetBytes(KeyFileCipher.KeySize));
        string folder = NewFolder();
        Directory.CreateDirectory(folder);
        byte[] dpapi = [0x01, 0x00, 0x00, 0x00, 0xD0, 0x8C, 0x9D, 0xDF, 0x01, 0x15, 0xD1, 0x11, 0x8C, 0x7A, 0x00, 0xC0, 0x4F, 0xC2, 0x97, 0xEB, .. RandomNumberGenerator.GetBytes(200)];
        File.WriteAllBytes(Path.Combine(folder, Name + ".key"), dpapi);

        var store = new ProtectedKeyStore(folder, Protector(keyring));

        Assert.IsTrue(store.Load(Name) is null, "A DPAPI key file was read as a key.");
        Assert.IsFalse(store.HasKey(Name), "A DPAPI key file was reported as a kept key.");
    }

    [TestMethod]
    public void ATamperedFileReadsAsNoKey()
    {
        var keyring = new FakeKeyring();
        string folder = NewFolder();
        var store = new ProtectedKeyStore(folder, Protector(keyring));
        string key = NewKey();
        store.Save(Name, key);
        string path = Path.Combine(folder, Name + ".key");
        byte[] original = File.ReadAllBytes(path);

        // The header, the nonce, the tag, the ciphertext's first and last byte.
        foreach (int at in new[] { 0, KeyFileCipher.HeaderSize, KeyFileCipher.HeaderSize + KeyFileCipher.NonceSize, KeyFileCipher.Overhead, original.Length - 1 })
        {
            byte[] changed = [.. original];
            changed[at] ^= 0x01;
            File.WriteAllBytes(path, changed);
            Assert.IsTrue(store.Load(Name) is null, $"The key 'openalex' was read from a file with byte {at} changed.");
        }

        File.WriteAllBytes(path, original);
        Assert.IsTrue(store.Load(Name) == key, "The key 'openalex' did not read from its untouched file.");
    }

    [TestMethod]
    public void TwoCallersShareOneFetch()
    {
        byte[] master = RandomNumberGenerator.GetBytes(KeyFileCipher.KeySize);
        var keyring = new FakeKeyring { GetGate = Gate() };
        keyring.Hold(Account, master);
        var protector = Protector(keyring);
        byte[] clear = Encoding.UTF8.GetBytes(NewKey());
        byte[] file = KeyFileCipher.Encrypt(master, clear);

        Task<byte[]> first = Task.Run(() => protector.Unprotect(file));
        Assert.IsTrue(keyring.GetEntered.Wait(s_wait), "The first caller did not ask the keyring.");
        Task<byte[]> second = Task.Run(() => protector.Unprotect(file));
        Thread.Sleep(300);
        Assert.IsFalse(first.IsCompleted || second.IsCompleted, "A caller did not wait for the fetch that was running.");
        keyring.GetGate!.Set();

        Assert.IsTrue(Task.WaitAll([first, second], s_wait), "The callers did not finish once the keyring answered.");
        Assert.IsTrue(first.Result.AsSpan().SequenceEqual(clear) && second.Result.AsSpan().SequenceEqual(clear), "A caller did not read the key file.");
        Assert.AreEqual(1, keyring.GetCalls, "Two callers made two fetches instead of sharing one.");
    }

    // The account is StorageEnvironment.DataFolderName(): the development
    // profile (PAPERROUTE_DEV_STORAGE, or a debugger) keeps its own master
    // key, so its key files never open in the person's own profile.
    [TestMethod]
    public void TheDevelopmentProfileHasItsOwnMasterKey()
    {
        const string Variable = "PAPERROUTE_DEV_STORAGE";
        var keyring = new FakeKeyring();
        string? before = Environment.GetEnvironmentVariable(Variable);
        KeyringSecretProtector development;
        KeyringSecretProtector own;
        try
        {
            Environment.SetEnvironmentVariable(Variable, "1");
            development = new KeyringSecretProtector(keyring, () => false);
            Environment.SetEnvironmentVariable(Variable, null);
            own = new KeyringSecretProtector(keyring, () => false);
        }
        finally
        {
            Environment.SetEnvironmentVariable(Variable, before);
        }

        Assert.AreEqual(ProductInfo.DevelopmentDataFolderName, development.Account, "The development profile's master key is not kept under its own data folder's name.");
        if (Debugger.IsAttached)
        {
            // A debugger makes every start the development profile.
            Assert.AreEqual(ProductInfo.DevelopmentDataFolderName, own.Account, "Under a debugger the profile is the development one.");
            return;
        }

        Assert.AreEqual(ProductInfo.DataFolderName, own.Account, "The own profile's master key is not kept under its data folder's name.");
        development.AllowMasterKeyCreation();
        own.AllowMasterKeyCreation();
        string developmentFolder = NewFolder();
        string ownFolder = NewFolder();
        new ProtectedKeyStore(developmentFolder, development).Save(Name, NewKey());
        new ProtectedKeyStore(ownFolder, own).Save(Name, NewKey());

        byte[]? developmentMaster = keyring.Held(development.Account);
        byte[]? ownMaster = keyring.Held(own.Account);
        Assert.IsTrue(developmentMaster is not null && ownMaster is not null, "Each profile did not get a master key of its own.");
        Assert.IsFalse(developmentMaster.AsSpan().SequenceEqual(ownMaster), "The two profiles share one master key.");
        File.Copy(Path.Combine(developmentFolder, Name + ".key"), Path.Combine(ownFolder, "copied.key"));
        Assert.IsTrue(new ProtectedKeyStore(ownFolder, own).Load("copied") is null, "A development profile's key file was read by the own profile.");
    }

    // The .NET type is System.Security.Cryptography.AesGcm: OpenSSL on Linux,
    // the system's CNG on Windows, Apple's CryptoKit on macOS. This fails on a
    // runner where it is missing (design risk 13).
    [TestMethod]
    public void AesGcmWorksHere()
    {
        Assert.IsTrue(AesGcm.IsSupported, "System.Security.Cryptography.AesGcm is not supported here, so PaperRoute cannot keep keys on this system.");
        Assert.IsTrue(AesGcm.NonceByteSizes.MinSize <= KeyFileCipher.NonceSize && KeyFileCipher.NonceSize <= AesGcm.NonceByteSizes.MaxSize,
            "AES-GCM here does not take a 12-byte nonce.");
        Assert.IsTrue(AesGcm.TagByteSizes.MinSize <= KeyFileCipher.TagSize && KeyFileCipher.TagSize <= AesGcm.TagByteSizes.MaxSize,
            "AES-GCM here does not take a 16-byte tag.");
        byte[] key = RandomNumberGenerator.GetBytes(KeyFileCipher.KeySize);
        byte[] clear = Encoding.UTF8.GetBytes(NewKey());
        Assert.IsTrue(KeyFileCipher.Decrypt(key, KeyFileCipher.Encrypt(key, clear)).AsSpan().SequenceEqual(clear), "AES-GCM did not round trip here.");
        Assert.IsTrue(new SessionKeyProtector().IsAvailable && Protector(new FakeKeyring()).IsAvailable, "A protector says keys cannot be kept here.");
    }

    // Design 2.7 (M7): the key used is the one the keyring gave back, never
    // the one made, so no key file is sealed with a key the keyring lacks.
    [TestMethod]
    public void AMasterKeyIsUsedOnlyAfterItIsStoredAndReadBack()
    {
        var keepsNothing = new FakeKeyring { Store = FakeKeyring.StoreBehaviour.KeepsNothing };
        string folder = NewFolder();
        InvalidOperationException notKept = Assert.ThrowsExactly<InvalidOperationException>(
            () => new ProtectedKeyStore(folder, Protector(keepsNothing)).Save(Name, NewKey()),
            "A save went ahead although the keyring kept no master key.");
        Assert.AreEqual(1, keepsNothing.SetCalls, "The new master key was not offered to the keyring.");
        Assert.IsEmpty(FilesIn(folder), "A key file was written with a master key the keyring did not keep.");
        Assert.Contains("did not keep", notKept.Message, "The failed save does not say the keyring kept nothing.");

        var keepsAnother = new FakeKeyring { Store = FakeKeyring.StoreBehaviour.KeepsAnother };
        string otherFolder = NewFolder();
        string key = NewKey();
        new ProtectedKeyStore(otherFolder, Protector(keepsAnother)).Save(Name, key);
        byte[] file = File.ReadAllBytes(Path.Combine(otherFolder, Name + ".key"));
        byte[] held = keepsAnother.Held(Account) ?? throw new AssertFailedException("The keyring holds no master key.");
        byte[] made = keepsAnother.Offered.Single();

        byte[]? opened = null;
        Exception? refused = Catch(() => opened = KeyFileCipher.Decrypt(held, file));
        Assert.IsTrue(refused is null && opened.AsSpan().SequenceEqual(Encoding.UTF8.GetBytes(key)),
            "The key 'openalex' was not sealed with the master key the keyring holds.");
        Assert.Throws<CryptographicException>(() => KeyFileCipher.Decrypt(made, file),
            "The key 'openalex' was sealed with the master key that was made, which the keyring does not hold.");
    }

    [TestMethod]
    [DataRow(KeyringAnswer.Refused)]
    [DataRow(KeyringAnswer.Locked)]
    [DataRow(KeyringAnswer.Unreachable)]
    public void AFailedStoreWritesNoKeyFile(KeyringAnswer answer)
    {
        var keyring = new FakeKeyring { SetAnswer = answer };
        string folder = NewFolder();
        var store = new ProtectedKeyStore(folder, Protector(keyring));

        Assert.ThrowsExactly<InvalidOperationException>(() => store.Save(Name, NewKey()), $"A save whose master key the keyring answered {answer} did not fail.");

        Assert.IsEmpty(FilesIn(folder), $"A save whose master key the keyring answered {answer} left a file.");
        Assert.IsTrue(store.Load(Name) is null, "A key reads after a failed save.");
    }

    // Start-up step 6 allows it after a successful hold; until then "no such
    // item" is "no key", so a copy that holds no library (a second copy, a
    // copy before its hold) can never race the holder to make a master key.
    // A copy reads a master key that exists either way.
    [TestMethod]
    public void OnlyACopyThatHoldsItsLibraryMakesAMasterKey()
    {
        var keyring = new FakeKeyring();
        string folder = NewFolder();
        var protector = new KeyringSecretProtector(keyring, () => false, Account);
        var store = new ProtectedKeyStore(folder, protector);
        string key = NewKey();

        Assert.ThrowsExactly<InvalidOperationException>(() => store.Save(Name, key), "A copy that holds no library made a master key.");
        Assert.AreEqual(0, keyring.SetCalls, "A copy that holds no library stored a master key.");
        Assert.IsEmpty(FilesIn(folder), "A copy that holds no library wrote a key file.");

        protector.AllowMasterKeyCreation();
        store.Save(Name, key);
        Assert.AreEqual(1, keyring.SetCalls, "The copy that holds its library did not make the master key.");

        var notHolding = new KeyringSecretProtector(keyring, () => false, Account);
        Assert.IsTrue(new ProtectedKeyStore(folder, notHolding).Load(Name) == key, "A copy that holds no library could not read a key under an existing master key.");
        Assert.AreEqual(1, keyring.SetCalls, "Reading made a master key.");
    }

    [TestMethod]
    public void TheExampleNeverTouchesTheKeyring()
    {
        var keyring = new FakeKeyring();
        bool asked = false;

        ISecretProtector example = SessionKeyProtector.ForSession(isolatedSession: true, () =>
        {
            asked = true;
            return Protector(keyring);
        });
        var store = new ProtectedKeyStore(NewFolder(), example);
        string key = NewKey();
        store.Save(Name, key);

        Assert.IsInstanceOfType<SessionKeyProtector>(example, "The example's session did not get the session-only protector.");
        Assert.IsFalse(asked, "The keyring protector was made for the example's session.");
        Assert.IsTrue(store.Load(Name) == key, "The key 'openalex' did not round trip in the example's session.");
        Assert.AreEqual(0, keyring.Calls, "The example's session asked the keyring.");

        KeyringSecretProtector ownProtector = Protector(keyring);
        Assert.AreSame(ownProtector, SessionKeyProtector.ForSession(isolatedSession: false, () => ownProtector), "The own library did not get the keyring protector.");
    }

    // The example's window is one run of its process: a key kept there cannot
    // be read by any later run, so its keys last only as long as its window.
    [TestMethod]
    public void TheExampleKeepsKeysOnlyForItsWindow()
    {
        string folder = NewFolder();
        var window = new SessionKeyProtector();
        string key = NewKey();
        new ProtectedKeyStore(folder, window).Save(Name, key);

        Assert.IsTrue(new ProtectedKeyStore(folder, window).Load(Name) == key, "The key 'openalex' was not read in the window that kept it.");
        Assert.IsTrue(new ProtectedKeyStore(folder, new SessionKeyProtector()).Load(Name) is null, "The key 'openalex' kept in one example window was read by another.");
    }

    [TestMethod]
    public void TheInterfaceThreadNeverWaitsForTheKeyring()
    {
        byte[] master = RandomNumberGenerator.GetBytes(KeyFileCipher.KeySize);
        var keyring = new FakeKeyring { GetGate = Gate() };
        keyring.Hold(Account, master);
        KeyringSecretProtector protector = Protector(keyring, onInterfaceThread: true);
        using var ready = new ManualResetEventSlim(false);
        protector.MasterKeyReady += (_, _) => ready.Set();
        byte[] clear = Encoding.UTF8.GetBytes(NewKey());
        byte[] file = KeyFileCipher.Encrypt(master, clear);

        Task<Exception?> read = Task.Run(() => Catch(() => protector.Unprotect(file)));
        Assert.IsTrue(Finished(read, TimeSpan.FromSeconds(2)), "Unprotect on the interface thread waited for the keyring.");
        Assert.IsInstanceOfType<CryptographicException>(read.Result, "Unprotect on the interface thread did not read as no key while the keyring was asked.");
        Assert.IsTrue(keyring.GetEntered.Wait(s_wait), "The fetch did not start on a worker.");

        Task<Exception?> save = Task.Run(() => Catch(() => protector.Protect(clear)));
        Assert.IsTrue(Finished(save, TimeSpan.FromSeconds(2)), "Protect on the interface thread waited for the keyring.");
        Assert.IsInstanceOfType<InvalidOperationException>(save.Result, "Protect on the interface thread did not refuse while the keyring was asked.");
        Assert.AreEqual(KeyringSecretProtector.AskingTheKeyring, save.Result!.Message, "Protect did not say the keyring is being asked.");

        var probing = new FakeKeyring { ProbeGate = Gate() };
        KeyringSecretProtector probed = Protector(probing, onInterfaceThread: true);
        Task<string?> why = Task.Run(() => probed.WhyNotAvailable);
        Assert.IsTrue(Finished(why, TimeSpan.FromSeconds(2)), "WhyNotAvailable on the interface thread waited for the probe.");
        Assert.AreEqual(KeyringSecretProtector.AskingTheKeyring, why.Result, "An unanswered probe did not read as the keyring being asked.");

        keyring.GetGate!.Set();
        Assert.IsTrue(ready.Wait(s_wait), "MasterKeyReady did not come once the keyring answered.");
        Assert.IsTrue(protector.Unprotect(file).AsSpan().SequenceEqual(clear), "Once the master key was ready, the key file did not read on the interface thread.");
        Assert.AreEqual(1, keyring.GetCalls, "The interface thread's calls started more than one fetch.");
    }

    // The lookup has no time limit: here the keyring answers only after two
    // minutes on the protector's clock, twelve times the probe's limit, as a
    // person typing at an unlock prompt would, and the answer still counts.
    // The probe's limit runs on that clock too, so the minutes pass at once
    // and a slow runner cannot cut the probe short.
    [TestMethod]
    public void AKeyringPromptIsNotCutOff()
    {
        byte[] master = RandomNumberGenerator.GetBytes(KeyFileCipher.KeySize);
        var keyring = new FakeKeyring { GetGate = Gate() };
        keyring.Hold(Account, master);
        var clock = new ManualClock();
        KeyringSecretProtector protector = Protector(keyring, clock: clock);
        byte[] clear = Encoding.UTF8.GetBytes(NewKey());
        byte[] file = KeyFileCipher.Encrypt(master, clear);

        Task<byte[]> reading = Task.Run(() => protector.Unprotect(file));
        Assert.IsTrue(keyring.GetEntered.Wait(s_wait), "The keyring was not asked.");
        clock.Advance(TimeSpan.FromMinutes(2));
        Assert.IsFalse(Finished(reading, TimeSpan.FromSeconds(1)), "The lookup ended while the keyring was still answering two minutes on: it was cut off.");
        keyring.GetGate!.Set();

        Assert.IsTrue(Finished(reading, s_wait), "The lookup did not finish once the keyring answered.");
        Assert.IsTrue(reading.Result.AsSpan().SequenceEqual(clear), "The key file did not read with the master key that came late.");
    }

    // The protector as the app makes it, with only a test clock: a probe that
    // never answers is still waited for at 9.9 seconds, and at ten it is read
    // as not available, without the keyring being asked for the master key.
    [TestMethod]
    public void TheProbeGivesUpAfterTenSeconds()
    {
        var keyring = new FakeKeyring { ProbeGate = Gate() };
        var clock = new ManualClock();
        var protector = new KeyringSecretProtector(keyring, () => false, Account, clock);

        Task<string?> why = Task.Run(() => protector.WhyNotAvailable);
        Assert.IsTrue(SpinWait.SpinUntil(() => clock.Pending > 0, s_wait), "The probe was given no time limit.");
        clock.Advance(TimeSpan.FromSeconds(9.9));
        Assert.IsFalse(Finished(why, TimeSpan.FromMilliseconds(500)), "The probe gave up before ten seconds.");
        clock.Advance(TimeSpan.FromSeconds(0.1));
        Assert.IsTrue(Finished(why, s_wait), "The probe did not give up at ten seconds.");
        Assert.Contains("did not answer", why.Result ?? string.Empty, "A keyring that did not answer was not said to be silent.");
        Assert.IsFalse(protector.IsAvailable, "A keyring that did not answer was said to be available.");
        Task<Exception?> save = Task.Run(() => Catch(() => protector.Protect([1, 2, 3])));
        Assert.IsTrue(Finished(save, s_wait), "Protect waited for a probe that did not answer.");
        Assert.IsInstanceOfType<InvalidOperationException>(save.Result, "Protect did not fail when the keyring did not answer.");
        Assert.AreEqual(0, keyring.GetCalls, "The master key was looked up although the keyring did not answer.");
    }

    [TestMethod]
    public void MasterKeyReadyFiresOnce()
    {
        var keyring = new FakeKeyring();
        KeyringSecretProtector protector = Protector(keyring);
        int raised = 0;
        protector.MasterKeyReady += (_, _) => Interlocked.Increment(ref raised);
        var store = new ProtectedKeyStore(NewFolder(), protector);

        store.Save(Name, NewKey());
        store.Save(ProtectedKeyStore.Anthropic, NewKey());
        Assert.IsTrue(store.HasKey(Name), "A saved key did not read back.");

        Assert.AreEqual(1, raised, "MasterKeyReady did not come exactly once.");

        var locked = new FakeKeyring { GetAnswer = KeyringAnswer.Locked };
        KeyringSecretProtector failing = Protector(locked);
        int failedRaised = 0;
        failing.MasterKeyReady += (_, _) => Interlocked.Increment(ref failedRaised);
        Assert.IsFalse(new ProtectedKeyStore(NewFolder(), failing).HasKey(Name), "A key read through a locked keyring.");
        Assert.AreEqual(0, failedRaised, "MasterKeyReady came although no master key was fetched.");
    }

    // #435's Settings will listen for MasterKeyReady: a listener that throws
    // must not turn a key read into an exception, nor keep the next listener
    // from hearing.
    [TestMethod]
    public void AThrowingReadyHandlerDoesNotEscapeARead()
    {
        byte[] master = RandomNumberGenerator.GetBytes(KeyFileCipher.KeySize);
        string folder = NewFolder();
        Directory.CreateDirectory(folder);
        string key = NewKey();
        File.WriteAllBytes(Path.Combine(folder, Name + ".key"), KeyFileCipher.Encrypt(master, Encoding.UTF8.GetBytes(key)));
        var keyring = new FakeKeyring();
        keyring.Hold(Account, master);
        KeyringSecretProtector protector = Protector(keyring);
        bool told = false;
        protector.MasterKeyReady += (_, _) => throw new InvalidOperationException("A listener failed.");
        protector.MasterKeyReady += (_, _) => told = true;

        string? read = null;
        Exception? escaped = Catch(() => read = new ProtectedKeyStore(folder, protector).Load(Name));

        Assert.IsNull(escaped, $"A MasterKeyReady handler's exception escaped a key read: {escaped?.GetType().Name}.");
        Assert.IsTrue(read == key, "The key 'openalex' did not read while a MasterKeyReady handler threw.");
        Assert.IsTrue(told, "A listener after one that threw was not told the master key is ready.");
    }

    // Design 2.7: a failed fetch is asked again at the next use after a minute,
    // not at every use.
    [TestMethod]
    public void AFailedFetchIsAskedAgainAfterAMinute()
    {
        var clock = new ManualClock();
        var keyring = new FakeKeyring { GetAnswer = KeyringAnswer.Locked };
        keyring.Hold(Account, RandomNumberGenerator.GetBytes(KeyFileCipher.KeySize));
        KeyringSecretProtector protector = Protector(keyring, clock: clock);
        byte[] clear = Encoding.UTF8.GetBytes(NewKey());

        Assert.ThrowsExactly<InvalidOperationException>(() => protector.Protect(clear), "A locked keyring gave a master key.");
        clock.Advance(TimeSpan.FromSeconds(59));
        Assert.ThrowsExactly<InvalidOperationException>(() => protector.Protect(clear), "The failure was forgotten within the minute.");
        Assert.AreEqual(1, keyring.GetCalls, "The keyring was asked again within a minute of a failure.");

        keyring.GetAnswer = null;
        clock.Advance(TimeSpan.FromSeconds(2));
        Assert.IsTrue(protector.Unprotect(protector.Protect(clear)).AsSpan().SequenceEqual(clear), "After the minute, the master key was not fetched again.");
        Assert.AreEqual(2, keyring.GetCalls, "The keyring was not asked again after the minute.");
    }

    // A probe that said no is a failed fetch too: no keyring was running when
    // the app started, and one starts later. It is asked again at the next use
    // after a minute, not before.
    [TestMethod]
    public void AProbeThatSaidNoIsAskedAgainAfterAMinute()
    {
        var clock = new ManualClock();
        var keyring = new FakeKeyring { WhyNot = NoKeyring };
        KeyringSecretProtector protector = Protector(keyring, clock: clock);
        byte[] clear = Encoding.UTF8.GetBytes(NewKey());

        Assert.ThrowsExactly<InvalidOperationException>(() => protector.Protect(clear), "A save went ahead with no keyring running.");
        keyring.WhyNot = null;
        clock.Advance(TimeSpan.FromSeconds(59));
        Assert.ThrowsExactly<InvalidOperationException>(() => protector.Protect(clear), "The probe's no was forgotten within the minute.");
        Assert.AreEqual(1, keyring.ProbeCalls, "The probe was asked again within a minute of saying no.");

        clock.Advance(TimeSpan.FromSeconds(2));
        Assert.IsTrue(protector.Unprotect(protector.Protect(clear)).AsSpan().SequenceEqual(clear), "After the minute, a keyring that started late was not used.");
        Assert.AreEqual(2, keyring.ProbeCalls, "The probe was not asked again after the minute.");
    }

    private static KeyringSecretProtector Protector(
        FakeKeyring keyring,
        bool onInterfaceThread = false,
        TimeProvider? clock = null)
    {
        var protector = new KeyringSecretProtector(keyring, () => onInterfaceThread, Account, clock);
        protector.AllowMasterKeyCreation();
        return protector;
    }

    private FakeKeyring Failing(string how)
    {
        var keyring = how switch
        {
            "Locked" => new FakeKeyring { GetAnswer = KeyringAnswer.Locked },
            "Unreachable" => new FakeKeyring { GetAnswer = KeyringAnswer.Unreachable },
            "Refused" => new FakeKeyring { GetAnswer = KeyringAnswer.Refused },
            "NotFound" => new FakeKeyring(),
            "Throws" => new FakeKeyring { Throws = true },
            "NoKeyring" => new FakeKeyring { WhyNot = NoKeyring },
            "Silent" => new FakeKeyring { ProbeGate = Gate() },
            _ => throw new ArgumentOutOfRangeException(nameof(how), how, "Not a way for the fake keyring to fail."),
        };

        // A master key is there for every answer that would otherwise find one.
        if (how != "NotFound")
        {
            keyring.Hold(Account, RandomNumberGenerator.GetBytes(KeyFileCipher.KeySize));
        }

        return keyring;
    }

    private ManualResetEventSlim Gate()
    {
        var gate = new ManualResetEventSlim(false);
        _gates.Add(gate);
        return gate;
    }

    private static string NewFolder() => Path.Combine(AssemblySetup.StorageRoot, "keys-" + Guid.NewGuid().ToString("N"));

    // A key made here: one word of printable characters, as the store asks.
    private static string NewKey() => "test-" + Guid.NewGuid().ToString("N");

    private static string[] FilesIn(string folder) => Directory.Exists(folder) ? Directory.GetFiles(folder) : [];

    private static bool Finished(Task task, TimeSpan limit) => Task.WhenAny(task, Task.Delay(limit)).GetAwaiter().GetResult() == task;

    // Runs the action on a worker while the clock runs fast, a second every ten
    // milliseconds, so a probe that never answers meets its limit at once.
    private static T FastForward<T>(ManualClock clock, Func<T> action)
    {
        Task<T> running = Task.Run(action);
        var watch = Stopwatch.StartNew();
        while (!Finished(running, TimeSpan.FromMilliseconds(10)))
        {
            if (watch.Elapsed > s_wait)
            {
                throw new AssertFailedException("The keyring protector did not answer, even past its time limit.");
            }

            clock.Advance(TimeSpan.FromSeconds(1));
        }

        return running.GetAwaiter().GetResult();
    }

    private static Exception? Catch(Action action)
    {
        try
        {
            action();
            return null;
        }
        catch (Exception ex)
        {
            return ex;
        }
    }
}
