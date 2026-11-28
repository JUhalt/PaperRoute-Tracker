using System.Net.Sockets;
using PaperRoute.Desktop.Platform;
using PaperRoute.ViewModels.Platform;

namespace PaperRoute.Desktop.Tests.Platform;

// Single instance on every system (start-up design 2.3, pull request I1):
// these run the form this system gets, the Windows mutex or the Unix lock
// file, on all four runners. Every data root is a new folder in the test
// run's temporary root, so no test touches a real library.
[TestClass]
public sealed class InstanceHoldTests
{
    internal static readonly TimeSpan Limit = TimeSpan.FromSeconds(2);

    // How long a test waits for a come-forward request to arrive: a bound,
    // never a pause; the wait ends as soon as it arrives.
    internal static readonly TimeSpan Wait = TimeSpan.FromSeconds(20);

    [TestMethod]
    public void TheLockIsBesideTheDataRootNeverInside()
    {
        string root = NewRoot();
        Directory.CreateDirectory(root);
        ISingleInstance form = ChosenForm();

        using (IInstanceHold? hold = form.TryHold(root))
        {
            Assert.IsNotNull(hold, "The library at a new data root could not be held.");
            Assert.IsEmpty(Directory.GetFileSystemEntries(root),
                "The hold created something inside the data root; the storage step skips the legacy migration when the root is not empty (StorageMigrationService.vb:773-785).");
            if (form is UnixSingleInstance)
            {
                Assert.IsTrue(File.Exists(root + UnixSingleInstance.LockSuffix), "The lock file is not beside the data root, named after it.");
                Assert.IsTrue(File.Exists(root + UnixSingleInstance.IdSuffix), "The id file is not beside the data root, named after it.");
            }
        }

        Assert.IsEmpty(Directory.GetFileSystemEntries(root), "Releasing the hold left something inside the data root.");
    }

    [TestMethod]
    public void AProfileWithNoDataFolderParentStillHolds()
    {
        // The data root's parent and its parent do not exist yet, as on a new
        // account before PaperRoute ever ran.
        string root = Path.Combine(AssemblySetup.StorageRoot, "hold-" + Guid.NewGuid().ToString("N")[..8], "share", "PaperRoute");
        ISingleInstance form = ChosenForm();

        IInstanceHold? hold;
        try
        {
            hold = form.TryHold(root);
        }
        catch (IOException ex)
        {
            Assert.Fail($"The hold threw when the lock's parent folder was missing: {ex.GetType().Name}: {ex.Message}");
            return;
        }

        using (hold)
        {
            Assert.IsNotNull(hold, "The library could not be held when the data root's parent folder was missing.");
            Assert.IsFalse(Directory.Exists(root), "The hold created the data root itself; only the storage step makes it.");
            if (form is UnixSingleInstance)
            {
                Assert.IsTrue(File.Exists(UnixSingleInstance.LockFileFor(root)), "The lock file was not made in the parent folder the hold created.");
            }
        }
    }

    // The contract the start-up tests rely on from the fake as much as from
    // the real forms: one hold per data root, a second Listen refused, and a
    // release that may be called twice and frees the library.
    [TestMethod]
    [DataRow("this system's form")]
    [DataRow("the fake")]
    public void OneHoldPerLibraryReleasedOnceOrTwice(string which)
    {
        string root = NewRoot();
        ISingleInstance form = FormFor(which);

        IInstanceHold? hold = form.TryHold(root);
        Assert.IsNotNull(hold, $"The library at a new data root could not be held ({which}).");
        try
        {
            Assert.IsNull(form.TryHold(root), $"A second hold on a library already held was granted ({which}).");
            hold.Listen(() => { });
            Assert.ThrowsExactly<InvalidOperationException>(() => hold.Listen(() => { }), $"A second Listen on one hold was accepted ({which}).");
        }
        finally
        {
            hold.Dispose();
        }

        hold.Dispose();
        using IInstanceHold? again = form.TryHold(root);
        Assert.IsNotNull(again, $"After the hold was released twice, the library could not be held again ({which}).");
    }

    [TestMethod]
    [DataRow("this system's form")]
    [DataRow("the fake")]
    public void AComeForwardBeforeListenIsDeliveredOnce(string which)
    {
        string root = NewRoot();
        ISingleInstance form = FormFor(which);
        int delivered = 0;
        using var arrived = new SemaphoreSlim(0);

        using IInstanceHold? hold = form.TryHold(root);
        Assert.IsNotNull(hold, "The library at a new data root could not be held.");

        // A second start during the storage step, before the holder listens.
        Assert.IsTrue(form.AskToComeForward(root, Limit), $"A second start could not reach the holder ({which}).");

        hold.Listen(() =>
        {
            Interlocked.Increment(ref delivered);
            arrived.Release();
        });
        Assert.IsTrue(arrived.Wait(Wait), $"A come-forward request made before Listen was lost ({which}).");

        // A second request, after Listen. The second wait ends at any delivery,
        // a duplicate of the first included, so the count is read only once the
        // hold has served everything sent so far.
        Assert.IsTrue(form.AskToComeForward(root, Limit), $"A start after Listen could not reach the holder ({which}).");
        Assert.IsTrue(arrived.Wait(Wait), $"A come-forward request made after Listen was lost ({which}).");
        WaitUntilServed(hold);
        Assert.AreEqual(2, Volatile.Read(ref delivered), $"Two requests, one before Listen and one after, were not delivered once each ({which}).");
    }

    // Returns once the hold has served every request sent to it so far, its
    // callbacks included, so a count read afterwards is final. The Unix form
    // serves one connection at a time and accepts the next only after the
    // previous callback returned: a connection that sends nothing is closed
    // by the holder only after everything before it. The other forms are
    // released: the Windows listener delivers a pending signal before it
    // stops (WaitAny prefers the activate event), and Dispose joins it.
    internal static void WaitUntilServed(IInstanceHold hold)
    {
        if (hold is not UnixInstanceHold unix)
        {
            hold.Dispose();
            return;
        }

        Assert.IsNotNull(unix.SocketPath, "The hold has no socket, so nothing it served can be waited for.");
        using var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        socket.ReceiveTimeout = (int)Wait.TotalMilliseconds;
        socket.Connect(new UnixDomainSocketEndPoint(unix.SocketPath));
        socket.Shutdown(SocketShutdown.Send);
        int read;
        try
        {
            read = socket.Receive(new byte[1]);
        }
        catch (SocketException ex)
        {
            Assert.Fail($"The holder did not close a connection that sent nothing, so what it served before cannot be known: {ex.SocketErrorCode}.");
            return;
        }

        Assert.AreEqual(0, read, "The holder wrote to a connection that sent nothing.");
    }

    [TestMethod]
    public void TwoSpellingsOfOneFolderTakeOneLock() => TheLocksFollowTheDisksCaseRule();

    [TestMethod]
    public void TwoFoldersThatDifferOnlyInCaseTakeTwoLocks() => TheLocksFollowTheDisksCaseRule();

    // The disk decides (SY 1.18): on a disk that ignores case (macOS's default
    // volume, Windows) "CaseRoot" and "caseroot" are one folder and must take
    // one lock; on one that respects it (Linux) they are two folders and take
    // two. The probe shows which rule this disk has.
    private static void TheLocksFollowTheDisksCaseRule()
    {
        string folder = Path.Combine(AssemblySetup.StorageRoot, "case-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(Path.Combine(folder, "CaseProbe"));
        bool ignoresCase = Directory.Exists(Path.Combine(folder, "caseprobe"));
        ISingleInstance form = ChosenForm();

        using IInstanceHold? first = form.TryHold(Path.Combine(folder, "CaseRoot"));
        Assert.IsNotNull(first, "The library at a new data root could not be held.");
        using IInstanceHold? second = form.TryHold(Path.Combine(folder, "caseroot"));
        if (ignoresCase)
        {
            Assert.IsNull(second, "This disk ignores case, so 'CaseRoot' and 'caseroot' are one folder, yet both spellings took a lock.");
        }
        else
        {
            Assert.IsNotNull(second, "This disk respects case, so 'CaseRoot' and 'caseroot' are two folders, yet the second could not be held.");
        }
    }

    internal static string NewRoot() =>
        Path.Combine(AssemblySetup.StorageRoot, "hold-" + Guid.NewGuid().ToString("N")[..8], "PaperRoute");

    private static ISingleInstance FormFor(string which) =>
        which == "the fake" ? new FakeSingleInstance() : ChosenForm();

    // This system's form, checked to be the one the design gives it.
    internal static ISingleInstance ChosenForm()
    {
        ISingleInstance form = SingleInstanceForms.ForThisSystem(note: _ => { });
        if (OperatingSystem.IsWindows())
        {
            Assert.IsInstanceOfType<WindowsSingleInstance>(form, "Windows did not get the form over today's SingleInstanceService.");
        }
        else
        {
            Assert.IsInstanceOfType<UnixSingleInstance>(form, "Linux and macOS did not get the lock-file form.");
        }

        return form;
    }
}
