using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using PaperRoute.ViewModels.Measurement;

namespace PaperRoute.Desktop.Measurement;

// What the PC was doing around one measurement run (busy-PC design, section
// 2), all report-only: how many processors were busy, how many of them other
// programs kept busy, the share of the rows' time the interface thread was
// not running, and whether the run counts as disturbed.
//
// The machine's load is read twice, before the first row and after the last,
// each time on a thread of its own: GetSystemTimes was seen to block for up
// to 727 ms on the development PC under full load, so it is never called on
// the interface thread or between timed rows. The stopwatch is read before
// and after each reading; the midpoint is the reading's time and the
// difference goes into loadReadMs. Windows: GetSystemTimes (kernel + user -
// idle). Linux: the first line of /proc/stat, busy = user + nice + system +
// irq + softirq, and steal apart. Every failure gives null; nothing throws.
// The counters move in steps (15.6 ms a processor on Windows, a clock tick
// on Linux), so the figures are coarse over a short run and can read a
// little above the processor count; they are not clamped.
public sealed class RunConditions
{
    private static readonly Stopwatch s_clock = Stopwatch.StartNew();
    private readonly LoadReading _before;

    private RunConditions(LoadReading before) => _before = before;

    // The first reading. The caller waits for it, untimed, before the first row.
    public static RunConditions Begin() => new(ReadOffThread());

    // The second reading, after the last row, and the run's figures. clock is
    // the thread clock the rows were timed with, already closed; rows are the
    // run's rows.
    public MeasurementConditions End(ThreadClock clock, IReadOnlyList<MeasuredRow> rows) =>
        Compute(clock, _before, ReadOffThread(), rows, Environment.ProcessorCount);

    // The run's figures from two readings. Others' load is the machine's less
    // this process's own processor time, never below 0; it is not exact (the
    // window manager's and the test runner's work for this process count as
    // other programs). A run is disturbed when other programs kept more than
    // a quarter of the processors busy on average
    // (MeasurementConditions.DisturbedLine: 3 of 12, 1 of 4), the line the
    // maintainer set on November 27, 2026. The machine's load counts every
    // processor, while processors (from Environment.ProcessorCount) follows
    // an affinity or container limit, so the line assumes the process runs
    // with neither.
    public static MeasurementConditions Compute(ThreadClock clock, LoadReading before, LoadReading after, IReadOnlyList<MeasuredRow> rows, int processors)
    {
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);
        ArgumentNullException.ThrowIfNull(rows);

        double seconds = (after.MidpointMs - before.MidpointMs) / 1000;
        double? machine = Rate(before.BusySeconds, after.BusySeconds, seconds);
        double? own = Rate(before.ProcessSeconds, after.ProcessSeconds, seconds);
        double? others = machine is { } all && own is { } mine ? Math.Max(0, all - mine) : null;
        return new MeasurementConditions
        {
            ThreadClock = clock.Describe(),
            CyclesPerMs = clock.CyclesPerMs,
            RateCheckBefore = clock.Before,
            RateCheckAfter = clock.After,
            MachineBusyProcessors = machine,
            LoadReadBeforeMs = before.ReadMs,
            LoadReadAfterMs = after.ReadMs,
            StealProcessors = Rate(before.StealSeconds, after.StealSeconds, seconds),
            OthersBusyProcessors = others,
            NotRunShare = NotRunShare(rows),
            Disturbed = others is { } busy ? busy > MeasurementConditions.DisturbedLine(processors) : null,
        };
    }

    // Time not running over Wall, summed over the rows that do not wait and
    // have both; null when no row has them. Each row's Wall is the one its
    // figures were taken against (MeasuredRow.OwnWallMs: for the scroll row,
    // its longest-UI step's).
    public static double? NotRunShare(IReadOnlyList<MeasuredRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        List<MeasuredRow> counted = rows.Where(row => !row.Waits && row.NotRunMs is not null).ToList();
        double wall = counted.Sum(row => row.OwnWallMs);
        return counted.Count == 0 || wall <= 0 ? null : counted.Sum(row => row.NotRunMs!.Value) / wall;
    }

    // One reading of the machine's load, on a thread of its own; the caller
    // waits for it. Not a thread-pool task: a blocking wait may run a queued
    // task inline on the waiting thread, and the headless interface thread
    // is itself a thread-pool thread.
    public static LoadReading ReadOffThread()
    {
        LoadReading? reading = null;
        var reader = new Thread(() => reading = Read()) { IsBackground = true, Name = "Load reading" };
        reader.Start();
        reader.Join();
        return reading ?? throw new InvalidOperationException("The load reading ended without a result.");
    }

    private static LoadReading Read()
    {
        double? processBefore = ProcessSeconds();
        double start = s_clock.Elapsed.TotalMilliseconds;
        (double? busy, double? steal) = SystemSeconds();
        double end = s_clock.Elapsed.TotalMilliseconds;
        double? processAfter = ProcessSeconds();
        double? process = processBefore is { } first && processAfter is { } last ? (first + last) / 2 : null;
        return new LoadReading((start + end) / 2, busy is null ? null : end - start, busy, steal, process, Environment.CurrentManagedThreadId);
    }

    private static double? Rate(double? before, double? after, double seconds) =>
        before is { } from && after is { } to && seconds > 0 ? (to - from) / seconds : null;

    private static double? ProcessSeconds()
    {
        try
        {
            using var process = Process.GetCurrentProcess();
            return process.TotalProcessorTime.TotalSeconds;
        }
        catch (Exception ex) when (ex is InvalidOperationException or NotSupportedException or PlatformNotSupportedException or System.ComponentModel.Win32Exception)
        {
            return null;
        }
    }

    // All processors' busy seconds since boot, and on Linux the steal.
    private static (double? Busy, double? Steal) SystemSeconds()
    {
        if (OperatingSystem.IsWindows())
        {
            return (WindowsBusySeconds(), null);
        }

        if (OperatingSystem.IsLinux())
        {
            return LinuxSeconds();
        }

        return (null, null);
    }

    [SupportedOSPlatform("windows")]
    private static double? WindowsBusySeconds()
    {
        try
        {
            // FILETIMEs in 100 ns units; kernel time includes idle time.
            return GetSystemTimes(out long idle, out long kernel, out long user) ? (kernel + user - idle) / 1e7 : null;
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            return null;
        }
    }

    // The first line of /proc/stat: "cpu  user nice system idle iowait irq
    // softirq steal guest guest_nice", in clock ticks. Guest time is already
    // counted in user and nice.
    [SupportedOSPlatform("linux")]
    private static (double? Busy, double? Steal) LinuxSeconds()
    {
        try
        {
            if (TicksPerSecond() is not { } ticks)
            {
                return (null, null);
            }

            string? line = File.ReadLines("/proc/stat").FirstOrDefault();
            string[] fields = line?.Split(' ', StringSplitOptions.RemoveEmptyEntries) ?? [];
            if (fields.Length < 8 || fields[0] != "cpu")
            {
                return (null, null);
            }

            long Field(int index) => long.Parse(fields[index], NumberStyles.None, CultureInfo.InvariantCulture);
            double busy = (Field(1) + Field(2) + Field(3) + Field(6) + Field(7)) / ticks;
            double? steal = fields.Length > 8 ? Field(8) / ticks : null;
            return (busy, steal);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FormatException or OverflowException)
        {
            return (null, null);
        }
    }

    // sysconf(_SC_CLK_TCK): the clock ticks /proc/stat counts in.
    [SupportedOSPlatform("linux")]
    private static double? TicksPerSecond()
    {
        try
        {
            nint ticks = SysConf(2);
            return ticks > 0 ? (double)ticks : null;
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            return null;
        }
    }

    [SupportedOSPlatform("windows")]
    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemTimes(out long idleTime, out long kernelTime, out long userTime);

    [SupportedOSPlatform("linux")]
    [DllImport("libc.so.6", EntryPoint = "sysconf")]
    private static extern nint SysConf(int name);
}

// One reading of the machine's load: its time by the run's stopwatch (the
// midpoint of the reading), how long it took (null when the load could not be
// read), all processors' busy seconds and the steal (Linux) since boot, and
// this process's processor seconds. Absent figures are null. ReadOnThread is
// the managed id of the thread that read it, so a test can hold that it was
// not the interface thread.
public sealed record LoadReading(double MidpointMs, double? ReadMs, double? BusySeconds, double? StealSeconds, double? ProcessSeconds, int ReadOnThread = 0);

// Which kind of core the calling thread is on (busy-PC design, section 2,
// perfCoreShare): a map of each logical processor to its efficiency class,
// made once per process. Windows: GetSystemCpuSetInformation, and
// GetCurrentProcessorNumberEx at each reading. Linux: cpu_capacity under
// /sys/devices/system/cpu, and sched_getcpu. The map is null where it cannot
// be read (usual on x86 virtual machines) or every processor is of one class.
public sealed class ProcessorClasses
{
    private static readonly Lazy<ProcessorClasses?> s_current = new(Make);

    private readonly Dictionary<int, int> _classOf;
    private readonly int _fastest;

    private ProcessorClasses(Dictionary<int, int> classOf)
    {
        _classOf = classOf;
        _fastest = classOf.Values.Max();
    }

    public static ProcessorClasses? Current => s_current.Value;

    // How many logical processors are of the fastest class, and how many in all.
    public int FastProcessors => _classOf.Values.Count(value => value == _fastest);

    public int AllProcessors => _classOf.Count;

    // Whether the calling thread runs on the fastest class now; null when the
    // processor number cannot be read or is not in the map.
    public bool? OnFastCore()
    {
        int? processor = CurrentProcessor();
        return processor is { } key && _classOf.TryGetValue(key, out int value) ? value == _fastest : null;
    }

    private static ProcessorClasses? Make()
    {
        Dictionary<int, int>? map = null;
        if (OperatingSystem.IsWindows())
        {
            map = WindowsMap();
        }
        else if (OperatingSystem.IsLinux())
        {
            map = LinuxMap();
        }

        return map is { Count: > 0 } && map.Values.Distinct().Count() > 1 && CurrentProcessor() is not null ? new ProcessorClasses(map) : null;
    }

    private static int? CurrentProcessor()
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                GetCurrentProcessorNumberEx(out ProcessorNumber number);
                return (number.Group << 8) | number.Number;
            }

            if (OperatingSystem.IsLinux())
            {
                int cpu = SchedGetCpu();
                return cpu >= 0 ? cpu : null;
            }
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            return null;
        }

        return null;
    }

    // SYSTEM_CPU_SET_INFORMATION entries: Size (4 bytes), Type (4), then for
    // a CPU set Id (4), Group (2), LogicalProcessorIndex (1), CoreIndex (1),
    // LastLevelCacheIndex (1), NumaNodeIndex (1), EfficiencyClass (1).
    [SupportedOSPlatform("windows")]
    private static Dictionary<int, int>? WindowsMap()
    {
        IntPtr buffer = IntPtr.Zero;
        try
        {
            _ = GetSystemCpuSetInformation(IntPtr.Zero, 0, out uint length, IntPtr.Zero, 0);
            if (length == 0)
            {
                return null;
            }

            buffer = Marshal.AllocHGlobal((int)length);
            if (!GetSystemCpuSetInformation(buffer, length, out uint returned, IntPtr.Zero, 0))
            {
                return null;
            }

            var map = new Dictionary<int, int>();
            int offset = 0;
            while (offset + 19 <= returned)
            {
                int size = Marshal.ReadInt32(buffer, offset);
                int type = Marshal.ReadInt32(buffer, offset + 4);
                if (size <= 0)
                {
                    break;
                }

                if (type == 0)
                {
                    int group = (ushort)Marshal.ReadInt16(buffer, offset + 12);
                    int logical = Marshal.ReadByte(buffer, offset + 14);
                    map[(group << 8) | logical] = Marshal.ReadByte(buffer, offset + 18);
                }

                offset += size;
            }

            return map;
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or OutOfMemoryException)
        {
            return null;
        }
        finally
        {
            if (buffer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
    }

    [SupportedOSPlatform("linux")]
    private static Dictionary<int, int>? LinuxMap()
    {
        try
        {
            var map = new Dictionary<int, int>();
            foreach (string folder in Directory.EnumerateDirectories("/sys/devices/system/cpu", "cpu*"))
            {
                string name = Path.GetFileName(folder);
                if (!int.TryParse(name.AsSpan(3), NumberStyles.None, CultureInfo.InvariantCulture, out int cpu))
                {
                    continue;
                }

                string file = Path.Combine(folder, "cpu_capacity");
                if (!File.Exists(file))
                {
                    return null;
                }

                map[cpu] = int.Parse(File.ReadAllText(file).Trim(), NumberStyles.None, CultureInfo.InvariantCulture);
            }

            return map;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FormatException or OverflowException)
        {
            return null;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessorNumber
    {
        public ushort Group;
        public byte Number;
        public byte Reserved;
    }

    [SupportedOSPlatform("windows")]
    [DllImport("kernel32.dll")]
    private static extern void GetCurrentProcessorNumberEx(out ProcessorNumber number);

    [SupportedOSPlatform("windows")]
    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemCpuSetInformation(IntPtr information, uint bufferLength, out uint returnedLength, IntPtr process, uint flags);

    [SupportedOSPlatform("linux")]
    [DllImport("libc.so.6", EntryPoint = "sched_getcpu")]
    private static extern int SchedGetCpu();
}
