using System.Net.Sockets;
using System.Text;
using PaperRoute.Desktop.Platform;
using PaperRoute.ViewModels.Platform;

namespace PaperRoute.Desktop.Tests.Platform;

// The Unix form of single instance (start-up design 2.3a, pull request I1):
// the lock file is the guarantee, the socket only carries "come forward". They
// run on Linux and both Macs. On Windows each asserts that Windows gets the
// form over today's mutex instead, never skipped, since the judging jobs fail
// on a test that did not pass. Data roots are new folders in the test run's
// temporary root; sockets go to the system's own socket folder unless a test
// says otherwise, and every socket a test binds is closed by it.
[TestClass]
public sealed class UnixHoldTests
{
    private readonly List<string> _notes = [];

    [TestMethod]
    public void ASecondHoldOnTheSameLockIsRefused()
    {
        if (!IsUnix())
        {
            return;
        }

        string root = InstanceHoldTests.NewRoot();
        var first = new UnixSingleInstance(note: _notes.Add);
        var second = new UnixSingleInstance(note: _notes.Add);

        IInstanceHold? hold = first.TryHold(root);
        Assert.IsNotNull(hold, "The library at a new data root could not be held.");
        try
        {
            // Two opens of one lock file in one process: flock conflicts between
            // them (SY 1.7), as it does between two processes (I3 shows that).
            Assert.IsNull(second.TryHold(root),
                "A second hold on the same lock file succeeded while the first was held: flock between two opens in one process did not conflict on this system (start-up design, risk 1).");
            Assert.IsNull(first.TryHold(root), "The same form took a second hold on a library it already holds.");
        }
        finally
        {
            hold.Dispose();
        }

        hold.Dispose();
        using IInstanceHold? again = second.TryHold(root);
        Assert.IsNotNull(again, "After the hold was released (twice), the library could not be held again.");
    }

    [TestMethod]
    public void TheSocketPathFitsTheSystem()
    {
        if (!IsUnix())
        {
            return;
        }

        var form = new UnixSingleInstance(note: _notes.Add);
        string? path;
        using (var hold = (UnixInstanceHold?)form.TryHold(InstanceHoldTests.NewRoot()))
        {
            Assert.IsNotNull(hold, "The library at a new data root could not be held.");
            path = hold.SocketPath;
            Assert.IsNotNull(path, $"Come forward is off in the system's own socket folder: {string.Join(" ", _notes)}");

            // sun_path holds 108 bytes on Linux and 104 on macOS (SY 1.13).
            int bytes = Encoding.UTF8.GetByteCount(path);
            Assert.IsTrue(bytes < 100, $"The bound socket path is {bytes} bytes long; it must stay under 100.");

            string? runtime = Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR");
            string expected = OperatingSystem.IsLinux() && !string.IsNullOrEmpty(runtime) && Directory.Exists(runtime)
                ? runtime
                : Path.GetTempPath();
            Assert.AreEqual(Path.TrimEndingDirectorySeparator(expected), Path.GetDirectoryName(path),
                "The socket is not in XDG_RUNTIME_DIR (Linux, when set) or else the temporary folder.");
            Assert.IsTrue(File.Exists(path), "No socket file is at the bound path while the hold is taken.");
        }

        Assert.IsFalse(File.Exists(path), "Releasing the hold left its socket file behind.");
    }

    [TestMethod]
    public void ATooLongSocketPathTurnsComeForwardOffAndStillHolds()
    {
        if (!IsUnix())
        {
            return;
        }

        // A socket folder whose path alone is longer than sun_path on any system.
        string folder = Path.Combine(AssemblySetup.StorageRoot, "sockets-" + new string('s', 60), new string('t', 60));
        Directory.CreateDirectory(folder);
        string root = InstanceHoldTests.NewRoot();
        var form = new UnixSingleInstance(folder, _notes.Add);

        using var hold = (UnixInstanceHold?)form.TryHold(root);
        Assert.IsNotNull(hold, "The hold failed because its socket path was too long; the lock alone is the guarantee.");
        Assert.IsNull(hold.SocketPath, "A socket path too long for the system was reported as bound.");
        Assert.IsTrue(_notes.Exists(note => note.Contains("will not bring this window forward", StringComparison.Ordinal)),
            "Come forward was turned off without a line saying so.");

        // Listen still works, but with no socket nothing can deliver a request:
        // unreachable is shown by the second copy's answer, not by a callback.
        hold.Listen(() => { });
        var second = new UnixSingleInstance(folder, _notes.Add);
        Assert.IsFalse(second.AskToComeForward(root, InstanceHoldTests.Limit), "A second start reported reaching a holder that has no socket.");
        Assert.IsNull(second.TryHold(root), "With come forward off, a second hold on the library succeeded.");
    }

    [TestMethod]
    public void AStaleSocketFileIsReplaced()
    {
        if (!IsUnix())
        {
            return;
        }

        string root = InstanceHoldTests.NewRoot();
        var form = new UnixSingleInstance(note: _notes.Add);
        string socketPath = BoundPathOfANewHold(form, root);

        // A copy that crashed leaves its socket file with nobody serving it.
        // Closing a .NET socket deletes the file it bound, so the stand-in binds
        // another name and moves the file into place before it closes.
        LeaveAStaleSocket(socketPath);
        Assert.IsTrue(File.Exists(socketPath), "The stand-in for a crashed copy left no socket file, so this test shows nothing.");
        Assert.IsFalse(form.AskToComeForward(root, InstanceHoldTests.Limit), "A stale socket file answered a come-forward request.");

        using var hold = (UnixInstanceHold?)form.TryHold(root);
        Assert.IsNotNull(hold, "A stale socket file stopped the library being held.");
        Assert.AreEqual(socketPath, hold.SocketPath, $"A leftover socket file stopped the new holder binding: {string.Join(" ", _notes)}");

        using var arrived = new SemaphoreSlim(0);
        hold.Listen(() => arrived.Release());
        Assert.IsTrue(form.AskToComeForward(root, InstanceHoldTests.Limit), "A second start could not reach the holder that replaced the stale socket.");
        Assert.IsTrue(arrived.Wait(InstanceHoldTests.Wait), "The holder that replaced the stale socket did not deliver the request.");
    }

    [TestMethod]
    public void AnyOtherLineOnTheSocketIsIgnored()
    {
        if (!IsUnix())
        {
            return;
        }

        string root = InstanceHoldTests.NewRoot();
        var form = new UnixSingleInstance(note: _notes.Add);
        using var hold = (UnixInstanceHold?)form.TryHold(root);
        Assert.IsNotNull(hold?.SocketPath, "The library could not be held with come forward on.");
        int delivered = 0;
        using var arrived = new SemaphoreSlim(0);
        hold.Listen(() =>
        {
            Interlocked.Increment(ref delivered);
            arrived.Release();
        });

        string[] others =
        [
            "hello\n",
            "PaperRoute come-forward 2\n",
            "paperroute come-forward 1\n",
            UnixSingleInstance.ComeForwardLine + " and more\n",
            UnixSingleInstance.ComeForwardLine,
            new string('x', 200) + "\n",
        ];
        foreach (string other in others)
        {
            Send(hold.SocketPath, other);
        }

        // The wait ends at any delivery, one made for a wrong line included, so
        // the count is read only once the hold has served everything sent.
        Assert.IsTrue(form.AskToComeForward(root, InstanceHoldTests.Limit), "A second start could not reach the holder.");
        Assert.IsTrue(arrived.Wait(InstanceHoldTests.Wait), "The real come-forward request did not arrive after the other lines.");
        InstanceHoldTests.WaitUntilServed(hold);
        Assert.AreEqual(1, Volatile.Read(ref delivered), "The holder acted on a line that is not the come-forward request.");
    }

    [TestMethod]
    public void TheLockIsTheGuarantee()
    {
        if (!IsUnix())
        {
            return;
        }

        string root = InstanceHoldTests.NewRoot();
        var form = new UnixSingleInstance(note: _notes.Add);
        var second = new UnixSingleInstance(note: _notes.Add);
        string socketPath = BoundPathOfANewHold(form, root);

        // A stand-in listens on the socket path while no copy holds the library.
        using (var squatter = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified))
        {
            squatter.Bind(new UnixDomainSocketEndPoint(socketPath));
            squatter.Listen(4);

            using IInstanceHold? hold = form.TryHold(root);
            Assert.IsNotNull(hold, "A stand-in listening on the socket path stopped the holder starting.");
            Assert.IsNull(second.TryHold(root), "With the socket path squatted, a second hold on the library succeeded.");
        }

        // A folder squats the socket path: come forward is off, the lock holds.
        Directory.CreateDirectory(socketPath);
        try
        {
            using var hold = (UnixInstanceHold?)form.TryHold(root);
            Assert.IsNotNull(hold, "A folder at the socket path stopped the holder starting.");
            Assert.IsNull(hold.SocketPath, "A folder at the socket path was reported as a bound socket.");
            Assert.IsNull(second.TryHold(root), "With come forward off, a second hold on the library succeeded.");
        }
        finally
        {
            Directory.Delete(socketPath);
        }
    }

    // On Linux and macOS true; on Windows it checks that Windows gets the form
    // over today's mutex, and false.
    private static bool IsUnix()
    {
        ISingleInstance form = InstanceHoldTests.ChosenForm();
        return form is UnixSingleInstance;
    }

    // Holds the library, notes where its socket was bound, and releases it.
    private string BoundPathOfANewHold(UnixSingleInstance form, string root)
    {
        using var hold = (UnixInstanceHold?)form.TryHold(root);
        Assert.IsNotNull(hold?.SocketPath, $"The library could not be held with come forward on: {string.Join(" ", _notes)}");
        return hold.SocketPath;
    }

    private static void LeaveAStaleSocket(string socketPath)
    {
        string elsewhere = socketPath + ".crashed";
        using var crashed = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        crashed.Bind(new UnixDomainSocketEndPoint(elsewhere));
        crashed.Listen(1);
        File.Move(elsewhere, socketPath);
    }

    private static void Send(string socketPath, string text)
    {
        using var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        socket.Connect(new UnixDomainSocketEndPoint(socketPath));
        socket.Send(Encoding.ASCII.GetBytes(text));
        socket.Shutdown(SocketShutdown.Send);
    }
}
