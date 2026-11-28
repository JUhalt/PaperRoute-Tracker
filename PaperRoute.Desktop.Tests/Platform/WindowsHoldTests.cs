using System.Runtime.Versioning;
using ManuscriptPipeline.Services;
using PaperRoute.Desktop.Platform;
using PaperRoute.ViewModels.Platform;

namespace PaperRoute.Desktop.Tests.Platform;

// The Windows form of single instance (start-up design 2.3, pull request I1):
// today's SingleInstanceService behind the new interface, with PaperRoute
// 1.0.x's key and object names (SingleInstanceKeyPinTest pins the key; it is
// not touched). They run on Windows; on Linux and macOS each asserts that the
// lock-file form is chosen instead, never skipped.
[TestClass]
public sealed class WindowsHoldTests
{
    [TestMethod]
    public void TheWindowsHoldUsesTheKeyOf1_0()
    {
        if (!OperatingSystem.IsWindows())
        {
            _ = InstanceHoldTests.ChosenForm();
            return;
        }

        string root = InstanceHoldTests.NewRoot();
        var form = (WindowsSingleInstance)InstanceHoldTests.ChosenForm();

        // The new app holds the library: 1.0.x's service, with its own key for
        // the same folder, in any letter case, finds it taken.
        using (IInstanceHold? hold = form.TryHold(root))
        {
            Assert.IsNotNull(hold, "The library at a new data root could not be held.");
            foreach (string spelling in new[] { root, root.ToUpperInvariant(), root.ToLowerInvariant() + "\\" })
            {
                using SingleInstanceService? service = SingleInstanceService.TryStart(SingleInstanceService.KeyFor(spelling));
                Assert.IsNull(service, "While the new app held the library, PaperRoute 1.0.x's service could take the same library (its key, its mutex name).");
            }
        }

        // 1.0.x holds the library: the new app finds it taken, and its request
        // to come forward reaches 1.0.x's listener.
        using (SingleInstanceService? service = SingleInstanceService.TryStart(SingleInstanceService.KeyFor(root)))
        {
            Assert.IsNotNull(service, "After the new app released the library, PaperRoute 1.0.x's service could not take it.");
            using var arrived = new SemaphoreSlim(0);
            service.Listen(() => arrived.Release());
            using (IInstanceHold? refused = form.TryHold(root))
            {
                Assert.IsNull(refused, "While PaperRoute 1.0.x's service held the library, the new app could take it too.");
            }

            Assert.IsTrue(form.AskToComeForward(root, InstanceHoldTests.Limit), "The new app's request to come forward did not find PaperRoute 1.0.x's window.");
            Assert.IsTrue(arrived.Wait(InstanceHoldTests.Wait), "PaperRoute 1.0.x's listener did not get the new app's request to come forward.");
        }
    }

    [TestMethod]
    public void TheHoldIsReleasedOnTheThreadThatTookIt()
    {
        if (!OperatingSystem.IsWindows())
        {
            _ = InstanceHoldTests.ChosenForm();
            return;
        }

        // The rule the hold keeps (SingleInstanceService.vb:105-107): the mutex
        // belongs to the thread that took it. Released there, a copy that has
        // the mutex open at that moment (another start) can take it. Released
        // from another thread, the mutex stays owned by the taker for as long
        // as that thread lives, which for start-up is Main's whole life.
        var form = (WindowsSingleInstance)InstanceHoldTests.ChosenForm();
        Assert.IsTrue(OthersCanTakeIt(form, releaseOnTheTaker: true),
            "Released on the thread that took it, the hold left the mutex owned: another start that had it open could not take it.");

        // The tooth: a release from another thread is what this test catches.
        Assert.IsFalse(OthersCanTakeIt(form, releaseOnTheTaker: false),
            "Released from another thread, the mutex was free anyway, so this test could not tell the two apart.");
    }

    // Takes the hold on a thread of its own, keeps that thread alive while a
    // second handle to the same mutex is open, releases the hold on the taker
    // or on this thread, and says whether the second handle can take the mutex.
    [SupportedOSPlatform("windows")]
    private static bool OthersCanTakeIt(WindowsSingleInstance form, bool releaseOnTheTaker)
    {
        string root = InstanceHoldTests.NewRoot();
        IInstanceHold? hold = null;
        using var taken = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        using var released = new ManualResetEventSlim();
        using var end = new ManualResetEventSlim();
        var taker = new Thread(() =>
        {
            hold = form.TryHold(root);
            taken.Set();
            if (releaseOnTheTaker)
            {
                release.Wait();
                hold?.Dispose();
                released.Set();
            }

            end.Wait();
        }) { IsBackground = true, Name = "Hold taker" };
        taker.Start();
        try
        {
            Assert.IsTrue(taken.Wait(InstanceHoldTests.Wait), "The taker thread did not take the hold.");
            Assert.IsNotNull(hold, "The library at a new data root could not be held.");

            // Another start has the mutex open at the moment of the release.
            using var other = Mutex.OpenExisting(@"Local\" + SingleInstanceService.KeyFor(root) + "-instance");
            if (releaseOnTheTaker)
            {
                release.Set();
                Assert.IsTrue(released.Wait(InstanceHoldTests.Wait), "The taker thread did not release the hold.");
            }
            else
            {
                hold.Dispose();
            }

            bool took;
            try
            {
                took = other.WaitOne(0);
            }
            catch (AbandonedMutexException)
            {
                took = false;
            }

            if (took)
            {
                other.ReleaseMutex();
            }

            return took;
        }
        finally
        {
            end.Set();
            Assert.IsTrue(taker.Join(InstanceHoldTests.Wait), "The taker thread did not end.");
        }
    }
}
