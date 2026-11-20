namespace PaperRoute.Desktop.Tests;

// Every headless view test runs its whole body through this helper, in one
// Dispatch call: with PerTest isolation each Dispatch gets a fresh application,
// so a window does not survive from one call to the next (d1 6.1).
//
// The test thread blocks on the dispatched work with a time limit and never
// awaits it: Dispatch completes its task on the dispatcher thread, so an await
// would continue there, and a session disposed from there deadlocks.
internal static class Headless
{
    private static readonly TimeSpan s_timeout = TimeSpan.FromSeconds(60);

    public static void Run(Action body)
    {
        ArgumentNullException.ThrowIfNull(body);
        Run(() =>
        {
            body();
            return true;
        });
    }

    public static T Run<T>(Func<T> body)
    {
        ArgumentNullException.ThrowIfNull(body);
        Task<T> work = AssemblySetup.Session.Dispatch(body, CancellationToken.None);
        return work.WaitAsync(s_timeout).GetAwaiter().GetResult();
    }
}
