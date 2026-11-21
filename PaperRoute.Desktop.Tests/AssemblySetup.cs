using Avalonia.Headless;
using ManuscriptPipeline.Services;

// One headless session has one dispatcher thread, and the storage guard is process-wide.
[assembly: DoNotParallelize]

namespace PaperRoute.Desktop.Tests;

// Set-up and clean-up for the whole test assembly (d1 6.1).
//
// The storage guard: before anything else, every default storage root points
// into a new temporary folder, so no test, and no code under test, can read or
// write %LOCALAPPDATA%\PaperRoute or the user's library. At the end the run fails
// if a default root answered even once.
//
// The headless session: started and disposed from a thread-pool thread with a
// time limit, never awaited, so nothing continues on the dispatcher thread and
// disposes the session there (that deadlocks).
[TestClass]
public sealed class AssemblySetup
{
    private static readonly TimeSpan s_sessionTimeout = TimeSpan.FromSeconds(60);
    private static string? s_storageRoot;
    private static HeadlessUnitTestSession? s_session;

    internal static HeadlessUnitTestSession Session =>
        s_session ?? throw new InvalidOperationException("The headless session has not started.");

    internal static string StorageRoot =>
        s_storageRoot ?? throw new InvalidOperationException("The storage guard has not been set up.");

    [AssemblyInitialize]
    public static void Initialize(TestContext context)
    {
        _ = context;

        // First: the storage guard, before any core type resolves a root.
        string root = Path.Combine(Path.GetTempPath(), "PaperRoute-TrialTests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        s_storageRoot = root;
        StorageEnvironment.RootOverride = root;

        Task<HeadlessUnitTestSession> start = Task.Run(
            () => HeadlessUnitTestSession.StartNew(typeof(TestApp), AvaloniaTestIsolationLevel.PerTest));
        if (!start.Wait(s_sessionTimeout))
        {
            throw new TimeoutException($"The headless session did not start within {s_sessionTimeout.TotalSeconds} s.");
        }

        s_session = start.Result;
    }

    [AssemblyCleanup]
    public static void Cleanup()
    {
        bool disposed = true;
        if (s_session is not null)
        {
            HeadlessUnitTestSession session = s_session;
            s_session = null;
            disposed = Task.Run(session.Dispose).Wait(s_sessionTimeout);
        }

        int defaultRootUses = StorageEnvironment.DefaultRootUses;
        StorageEnvironment.RootOverride = null;
        if (s_storageRoot is not null)
        {
            try
            {
                Directory.Delete(s_storageRoot, recursive: true);
            }
            catch (IOException)
            {
                // Left for Windows' temporary-file clean-up.
            }
            catch (UnauthorizedAccessException)
            {
                // Left for Windows' temporary-file clean-up.
            }

            s_storageRoot = null;
        }

        Assert.AreEqual(0, defaultRootUses,
            "A default storage root was resolved during the test run: something bypassed the temporary root and may have reached the user's own library.");
        Assert.IsTrue(disposed, $"The headless session did not shut down within {s_sessionTimeout.TotalSeconds} s.");
    }
}
