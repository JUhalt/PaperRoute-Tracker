using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32;

namespace PaperRoute.Desktop.Measurement;

// How long the calling thread has run on a processor, in milliseconds: the
// thread clock of the busy-PC design (section 4). It is read on the thread it
// times, which for the Board's rows is the interface thread.
//
// Windows: QueryThreadCycleTime, divided by the nominal rate (the registry's
// ~MHz x 1000). Microsoft warns against turning these cycles into time in
// general, so the rate is checked: spins of 2 ms, untimed, until one spin's
// cycles per stopwatch millisecond come within 2% of nominal, at most 200
// spins. Time off the processor can only lower a spin's ratio, so one close
// spin is enough. The check runs when the clock opens and again at Close; if
// either fails, the clock is absent for the whole run. Ubuntu:
// clock_gettime(CLOCK_THREAD_CPUTIME_ID) from libc.so.6, already in time, so no
// check. GetThreadTimes is not used: it moves in 15.6 ms steps.
//
// Every failure makes the clock absent, with a reason from a fixed list and
// never an exception message; Read then gives null. Nothing here throws.
public sealed class ThreadClock
{
    public const string CyclesKind = "cycles";
    public const string ThreadCpuTimeKind = "thread-cputime";

    // The reasons an absent clock gives (design section 2, "threadClock").
    public const string NoClockReason = "no thread clock on this system";
    public const string NominalUnreadableReason = "nominal speed unreadable";
    public const string RateCheckFailedReason = "rate check failed";

    // A spin's ratio must come this close to nominal; the tests' relative
    // margin (1.02 x wall + 1 ms) matches it.
    public const double RateTolerance = 0.02;
    public const int MaxSpins = 200;
    public const double SpinMs = 2;

    private const int ClockThreadCpuTimeId = 3;

    private readonly bool _windows;

    private ThreadClock(string kind, double? cyclesPerMs, string? reason, RateCheck? before)
    {
        Kind = kind;
        CyclesPerMs = cyclesPerMs;
        Reason = reason;
        Before = before;
        _windows = string.Equals(kind, CyclesKind, StringComparison.Ordinal);
    }

    // "cycles", "thread-cputime", or empty when no clock was found.
    public string Kind { get; }

    // Windows only: the nominal rate the cycles are divided by.
    public double? CyclesPerMs { get; }

    // Why the clock is absent, from the fixed list; null while it is present.
    public string? Reason { get; private set; }

    public bool IsPresent => Reason is null;

    // Windows only: the rate checks when the clock opened and at Close.
    public RateCheck? Before { get; }

    public RateCheck? After { get; private set; }

    // What the JSON's "threadClock" says.
    public string Describe() => Reason is null ? Kind : "absent: " + Reason;

    // A clock for the calling thread, present or absent with its reason. On
    // Windows the first rate check runs here (a few ms, at most about 400).
    public static ThreadClock Open()
    {
        if (OperatingSystem.IsWindows())
        {
            return OpenWindows();
        }

        if (OperatingSystem.IsLinux())
        {
            return OpenLinux();
        }

        return Absent(NoClockReason);
    }

    // A clock that is absent for reason, for runs and tests with no clock.
    public static ThreadClock Absent(string reason) => new(string.Empty, null, reason, null);

    // The calling thread's running time in ms, or null when the clock is absent.
    public double? Read()
    {
        if (Reason is not null)
        {
            return null;
        }

        if (_windows && OperatingSystem.IsWindows())
        {
            return Cycles() is { } cycles && CyclesPerMs is { } rate ? cycles / rate : null;
        }

        if (OperatingSystem.IsLinux())
        {
            return ThreadCpuTimeMs();
        }

        return null;
    }

    // The second rate check, after the last row (Windows; nothing elsewhere).
    // When it fails the clock becomes absent, and the run's figures from it
    // are dropped by the caller.
    public void Close()
    {
        if (Reason is not null || !_windows || !OperatingSystem.IsWindows() || CyclesPerMs is not { } rate)
        {
            return;
        }

        After = CheckRate(rate);
        if (!After.Passed)
        {
            Reason = RateCheckFailedReason;
        }
    }

    [SupportedOSPlatform("windows")]
    private static ThreadClock OpenWindows()
    {
        if (Cycles() is null)
        {
            return Absent(NoClockReason);
        }

        if (NominalMegahertz() is not { } megahertz)
        {
            return Absent(NominalUnreadableReason);
        }

        double rate = megahertz * 1000;
        RateCheck before = CheckRate(rate);
        return new ThreadClock(CyclesKind, rate, before.Passed ? null : RateCheckFailedReason, before);
    }

    [SupportedOSPlatform("linux")]
    private static ThreadClock OpenLinux()
    {
        bool exported = false;
        if (NativeLibrary.TryLoad("libc.so.6", out IntPtr libc))
        {
            exported = NativeLibrary.TryGetExport(libc, "clock_gettime", out _);
            NativeLibrary.Free(libc);
        }

        return exported && ThreadCpuTimeMs() is not null
            ? new ThreadClock(ThreadCpuTimeKind, null, null, null)
            : Absent(NoClockReason);
    }

    // Spins of SpinMs until one comes within RateTolerance of nominal. The
    // share reported is the closest spin's ratio to nominal, not the largest
    // ratio: that sits at or just below the true rate and so would overstate
    // running time.
    [SupportedOSPlatform("windows")]
    private static RateCheck CheckRate(double cyclesPerMs)
    {
        double closest = double.NaN;
        int spins = 0;
        long spinTicks = (long)(SpinMs * Stopwatch.Frequency / 1000);
        while (spins < MaxSpins)
        {
            spins++;
            long start = Stopwatch.GetTimestamp();
            ulong? first = Cycles();
            long now;
            do
            {
                now = Stopwatch.GetTimestamp();
            }
            while (now - start < spinTicks);
            ulong? last = Cycles();
            if (first is not { } from || last is not { } to)
            {
                break;
            }

            double ms = (now - start) * 1000.0 / Stopwatch.Frequency;
            double share = (to - from) / ms / cyclesPerMs;
            if (double.IsNaN(closest) || Math.Abs(share - 1) < Math.Abs(closest - 1))
            {
                closest = share;
            }

            if (Math.Abs(share - 1) <= RateTolerance)
            {
                return new RateCheck(share, spins, Passed: true);
            }
        }

        return new RateCheck(double.IsNaN(closest) ? null : closest, spins, Passed: false);
    }

    [SupportedOSPlatform("windows")]
    private static double? NominalMegahertz()
    {
        try
        {
            using RegistryKey? key = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
            object? value = key?.GetValue("~MHz");
            return value switch
            {
                int megahertz when megahertz > 0 => megahertz,
                long megahertz when megahertz > 0 => megahertz,
                _ => null,
            };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            return null;
        }
    }

    [SupportedOSPlatform("windows")]
    private static ulong? Cycles()
    {
        try
        {
            return QueryThreadCycleTime(GetCurrentThread(), out ulong cycles) ? cycles : null;
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            return null;
        }
    }

    [SupportedOSPlatform("linux")]
    private static double? ThreadCpuTimeMs()
    {
        try
        {
            return ClockGetTime(ClockThreadCpuTimeId, out TimeSpec time) == 0
                ? (time.Seconds * 1000.0) + (time.Nanoseconds / 1_000_000.0)
                : null;
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            return null;
        }
    }

    [SupportedOSPlatform("windows")]
    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryThreadCycleTime(IntPtr threadHandle, out ulong cycleTime);

    [SupportedOSPlatform("windows")]
    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentThread();

    [SupportedOSPlatform("linux")]
    [DllImport("libc.so.6", EntryPoint = "clock_gettime")]
    private static extern int ClockGetTime(int clockId, out TimeSpec time);

    // struct timespec: seconds and nanoseconds, each the width of a long
    // (64-bit on the 64-bit systems CI and the trial run on).
    [StructLayout(LayoutKind.Sequential)]
    private struct TimeSpec
    {
        public nint Seconds;
        public nint Nanoseconds;
    }
}

// One rate check (Windows): the closest spin's cycles per ms as a share of
// nominal (null when no spin could be read), the spins it took, and whether
// a spin came within ThreadClock.RateTolerance.
public sealed record RateCheck(double? Share, int Spins, bool Passed)
{
    public override string ToString() => Share is { } share
        ? string.Create(CultureInfo.InvariantCulture, $"{share:0.0000} of nominal after {Spins} spins ({(Passed ? "passed" : "failed")})")
        : string.Create(CultureInfo.InvariantCulture, $"no spin read after {Spins} spins (failed)");
}
