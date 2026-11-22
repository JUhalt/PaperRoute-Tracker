#requires -Version 7
<#
.SYNOPSIS
Starts a known extra load for the Board trial's timing validation, in a separate process.
.DESCRIPTION
The load of the measurement method (docs/design/Board-Trial-Measurement-Method.md,
section 6): one new process that runs 6 threads at normal priority for -Seconds
(at most 60), then ends by itself. Spin threads do integer arithmetic. Memory
threads each stream a 64 MB array, one write per 64-byte line.

The load runs in its own process because load threads inside the measured
process would stretch its garbage-collection pauses and mix two effects. It
changes no setting: no priority, no affinity, no power setting.

Starting pwsh and compiling the load take several seconds on a busy PC, so
the script returns the load's process only once its threads are running (it
prints "ready"), and -Seconds counts from that moment. A load not running
within 60 s is stopped and the script throws. The caller stops it in a
finally block as well, in case the caller ends first:

    $load = & ./ManualCertification/Start-BoardTrialLoad.ps1 -Kind Spin -Seconds 60
    try { <the measured run> }
    finally { if (-not $load.HasExited) { $load.Kill($true) }; $load.Dispose() }

The load runs in its own hidden console, so Ctrl+C reaches it only through
the caller's finally block. If the caller is killed without it, the load
still ends by itself -Seconds after it became ready.
.EXAMPLE
pwsh -NoProfile -File ./ManualCertification/Start-BoardTrialLoad.ps1 -Kind Memory -Seconds 10
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [ValidateSet('Spin', 'Memory')] [string] $Kind,
    [Parameter(Mandatory)] [ValidateRange(1, 60)] [int] $Seconds,
    # Internal: set only on the load's own process, which runs the threads.
    [switch] $RunHere
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# The method fixes the number of threads; a sitting that is not disturbed is
# repeated at another time of day, never run with more threads.
$threads = 6
$readyLine = 'ready'
$readyLimit = [TimeSpan]::FromSeconds(60)

if (-not $RunHere) {
    $start = [Diagnostics.ProcessStartInfo]::new((Get-Process -Id $PID).Path)
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true
    foreach ($argument in '-NoProfile', '-NonInteractive', '-File', $PSCommandPath, '-Kind', $Kind, '-Seconds', [string] $Seconds, '-RunHere') {
        $start.ArgumentList.Add($argument)
    }
    $process = [Diagnostics.Process]::Start($start)
    $line = $process.StandardOutput.ReadLineAsync()
    if (-not $line.Wait($readyLimit) -or $line.Result -ne $readyLine) {
        if (-not $process.HasExited) { $process.Kill($true) }
        $process.Dispose()
        throw "The load was not running within $($readyLimit.TotalSeconds) s."
    }
    return $process
}

Add-Type -TypeDefinition @'
using System;
using System.Diagnostics;
using System.Threading;

public static class BoardTrialLoad
{
    // Starts the threads, which run for milliseconds from now; Join waits
    // for them to end.
    public static Thread[] Start(bool memory, int threads, double milliseconds)
    {
        long deadline = Stopwatch.GetTimestamp() + (long)(milliseconds * Stopwatch.Frequency / 1000);
        var workers = new Thread[threads];
        for (int i = 0; i < threads; i++)
        {
            int seed = i + 1;
            workers[i] = new Thread(() => { if (memory) Stream(deadline); else Spin(deadline, seed); }) { IsBackground = true };
            workers[i].Start();
        }

        return workers;
    }

    public static void Join(Thread[] workers)
    {
        foreach (Thread worker in workers)
        {
            worker.Join();
        }
    }

    // Integer arithmetic only: a xorshift step, the clock read once per million.
    private static void Spin(long deadline, int seed)
    {
        uint x = (uint)seed * 2654435761u;
        while (Stopwatch.GetTimestamp() < deadline)
        {
            for (int i = 0; i < 1000000; i++)
            {
                x ^= x << 13;
                x ^= x >> 17;
                x ^= x << 5;
            }
        }

        GC.KeepAlive(x);
    }

    // One write per 64-byte line over a 64 MB array, the clock read once a pass.
    private static void Stream(long deadline)
    {
        var lines = new byte[64 * 1024 * 1024];
        byte value = 0;
        while (Stopwatch.GetTimestamp() < deadline)
        {
            value++;
            for (int i = 0; i < lines.Length; i += 64)
            {
                lines[i] = value;
            }
        }

        GC.KeepAlive(lines);
    }
}
'@

# -Seconds counts from the moment the threads run, not from this process's
# start: pwsh's start and the compile above take several seconds.
$workers = [BoardTrialLoad]::Start($Kind -eq 'Memory', $threads, $Seconds * 1000)
[Console]::Out.WriteLine($readyLine)
[Console]::Out.Flush()
[BoardTrialLoad]::Join($workers)
