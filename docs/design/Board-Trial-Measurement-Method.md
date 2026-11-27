# Board trial: timing on a busy PC

How the Avalonia Board trial times its Board, what it records about how busy the PC was, and how two builds are compared. It answers the maintainer's note of November 21, 2026 on the two rows still over their budget: "Remember that these tests are on a very, very busy PC. Perhaps we can account or correct for that?"

**Every figure this method adds is report-only.** The budgets, the multipliers (Debug x2, GitHub Actions x3), the verdicts and the headless test's rule are what they were before. Wall and UI are computed as before; the new reads add a few microseconds inside each row's stopwatch and at each probe tick (section 3). Run UI is a diagnosis, not a verdict. The verdict rule of section 7 is a proposal and changes nothing until the maintainer accepts it.

Sections 1 to 8 keep the numbers that the code's comments cite as "busy-PC design, section N".

## What is built, and what is not yet

Built (the pull request that adds this document):

- The thread clock, the new per-row figures, the run's `conditions`, the failure message (sections 1 to 4 and 8). `PaperRoute.Desktop/Measurement/ThreadClock.cs`, `RunConditions.cs`, `BoardMeasurement.cs`, `WindowMeasurement.cs`, and `PaperRoute.ViewModels/Measurement/BoardMeasurementReport.cs`.
- The quiet-PC line at the end of the Windows Forms timing tests' failure messages (`PaperRoute.Tests/ResponsivenessBudgetTests.vb`).
- `ManualCertification/Measure-BoardPairs.ps1`, the comparison of section 5 and the proposed verdict lines of section 7, with its check on canned measurements, `ManualCertification/Test-BoardPairs.ps1`, which the trial's Windows CI job runs.
- `ManualCertification/Start-BoardTrialLoad.ps1`, the known load of section 6.

Built after it:

- The two report-only rows at the end of the measured sequence, `search-keystroke-published-hidden` and `switch-published-after-hidden-change` (section 3, last bullet), in `BoardMeasurement.cs`, with their checks in `PaperRoute.Desktop.Tests/BoardBudgetTests.cs` and `MeasurementReportTests.cs`. They are compared only between builds.
- The rows for preparing Published while the Board is idle and the way a run is told its side (section 13), in `BoardMeasurement.cs`, `TrialOptions.cs` and the pairs script, with their checks in `BoardBudgetTests.cs`, `MeasureModeTests.cs` and `Test-BoardPairs.ps1`.

Not built yet:

- The validation of section 6 has **not been run**. Until it has, Run UI is not validated on any PC.
- The pairs for #404's search with Published hidden. Its row, `search-keystroke-published-hidden`, and the switch after it are built (section 3), but no sitting has been run with them.
- For idle preparation: the window that times from the first frame to "prepared" with the idle timer on, and the other rows the idle design lists (a key meeting a step, a switch during a step, after a maximise or a scaling change, a library of several thousand, memory). No sitting has been run (section 13).

On Linux the thread clock and the load readings first ran in CI, and they gave readings (section 11, "Shown by CI").

## 1 What the figures mean, in plain words

- **Wall.** A stopwatch around one action, such as typing the search.
- **UI** (the verdict). The longest time during the action when a key press could not have been handled. A 1 ms timer on the interface thread measures it, and it is never more than Wall. UI is what a person would have felt on the PC as it was at that moment.
- **Ran** (`runMs`). How long the interface thread was actually running on a processor during the action. Windows and Linux keep this count for every thread.
- **Not running** (`notRunMs` = Wall - Ran). Time the thread was ready but other programs had the processor, or time it was waiting (for a frame, the disk, a garbage collection run by another thread).
- **Run UI** (`runUiMs`, the corrected figure). The same longest stall as UI, but counting only the time the thread was running. It answers one question: how long was the Board's own work in one stretch, leaving out the time other programs took the processor away?
- **Fast-core share** (`perfCoreShare`). The share of the probe's readings taken on the PC's faster kind of core. The development PC has 12 logical processors of two kinds: 4 faster ones on 2 performance cores, each with a twin, and 8 efficiency cores.
- **How busy the PC was** (per run). On average, how many of the PC's logical processors other programs kept busy during the run.

What the correction can and cannot do:

- It removes time when the Board **was not running**. In one experiment with a synthetic load, a piece of work kept 23.5 to 25.5 ms of running time while its stopwatch time went from 24 to 148 ms. Whether this holds on a real busy day is what the new figures will show.
- It cannot remove the Board **running slower** while it runs: two programs sharing one core, an efficiency core instead of a performance core, caches and memory traffic taken by other programs, a lower processor speed, the Board's own garbage collection and its first-use compilation. The same experiment showed a memory-heavy load raising running time by 21 to 41%. So Run UI is **not** "the cost on a quiet PC". It can read high (slower running) and it can read low (real waits by the Board itself are left out).
- Headless runs started from a console in the background and real-window runs in the foreground may run on different kinds of core. `perfCoreShare` shows this; it does not correct for it.
- So Run UI is a **diagnosis**. A row whose Run UI is well over its budget is over because of its own work, not because the PC is busy. A row whose UI is over but whose Run UI is under may be over because of the load. The verdict still uses UI.

## 2 What is recorded

Per row, in `rows[]` of the JSON. A figure that could not be read is JSON `null` and "absent" in the table, never 0, and it never fails a test.

| JSON name | Unit | How it is read: Windows / Linux | Absent when |
|---|---|---|---|
| `runMs` | ms | the thread clock (section 4), read just inside the stopwatch | no thread clock on this system, or the run's rate check failed; `process-start-to-board` (not timed on the thread) |
| `notRunMs` | ms | `wallMs - runMs` as computed, not clamped (a small negative value shows the clock's error) | as `runMs` |
| `runUiMs` | ms | the largest thread-clock increase inside one probe gap, the tail to the row's end included | as `runMs` |
| `perfCoreShare` | 0 to 1 | share of probe readings on the highest efficiency class. Windows: `GetCurrentProcessorNumberEx`, mapped once per run with `GetSystemCpuSetInformation`. Linux: `sched_getcpu` with `/sys/devices/system/cpu/cpuN/cpu_capacity` | Linux without `cpu_capacity` (usual on x86 virtual machines); all processors of one class |
| `gcPauseMs` | ms | change in `GC.GetTotalPauseDuration()`, the whole process | not measured for `process-start-to-board` |
| `jitMs` | ms | change in `JitInfo.GetCompilationTime(currentThread: true)` | as `gcPauseMs` |
| `waits` | true | written only for a row that waits by design: warm startup (the load runs off the thread), every paint row, the scroll row in a real window, `process-start-to-board` | never written as false |

Per run, in a `conditions` object. Headless, it covers the 500 library's measurement only.

| JSON name | Unit | How it is read: Windows / Linux | Absent when |
|---|---|---|---|
| `threadClock` | text | `"cycles"` (Windows `QueryThreadCycleTime`) or `"thread-cputime"` (Linux `clock_gettime`), or `"absent: <reason>"`, the reason from a fixed list: `no thread clock on this system`, `nominal speed unreadable`, `rate check failed`. Never an exception's message | never |
| `cyclesPerMs` | number | Windows only: the nominal rate the cycles are divided by | Linux; nominal speed unreadable |
| `rateCheck` | [before, after] | Windows only: each check's closest spin as a share of nominal, the spins it took, and whether it passed (section 4) | as `cyclesPerMs` |
| `machineBusyProcessors` | processors | change in busy time of all processors / elapsed time. Windows: `GetSystemTimes` (kernel + user - idle). Linux: the first line of `/proc/stat`, busy = user + nice + system + irq + softirq. Read on a thread of its own (never the interface thread) before the first row and after the last, each reading timed at its midpoint | the read failed |
| `loadReadMs` | [before, after] | how long each of the two load readings took | as above |
| `stealProcessors` | processors | Linux only: change in `steal` / elapsed time | Windows; the read failed |
| `othersBusyProcessors` | processors | `machineBusyProcessors` less this process's own processor time / elapsed time, never below 0 | as `machineBusyProcessors` |
| `notRunShare` | 0 to 1 | the sum of `notRunMs` / the sum of `wallMs`, over the rows without `waits` | as `runMs` |
| `disturbed` | true/false | `othersBusyProcessors > processors - 2`: other programs left fewer than two processors free, on average over the run | `null` when `othersBusyProcessors` is absent. `null` counts as not disturbed: the run is kept and printed as "load unknown" |

`machineBusyProcessors` can read slightly above the number of processors, because the counters move in steps. It is not clamped.

**Why `disturbed` uses other programs' load and not `notRunShare`.** A build can change its own share of time not running (a build that waits more, for example). If pairs were replaced on that figure, they would be replaced more often for one build than the other, and the comparison would be biased. Other programs' load depends mostly not on the build, because the process's own processor time is subtracted. It is not exact: in a real window, the window manager's and graphics driver's work for the trial's frames counts as "other programs", and headless the test runner's own processes do too. `notRunShare` is still recorded and printed, so a reader can see a disturbed row or run by eye.

Nothing new names the PC, the user, a folder or another program. The JSON's existing `machineName` field stays as it is; the pairs summary never copies it.

## 3 How each row is timed

- **Unchanged.** Every row is timed once per process, in a fixed order, with the same actions headless and in a real window. Wall, UI and the verdict are computed as before. The new reads below sit inside the stopwatch and in each probe tick, so they add a few microseconds to Wall and to UI's gaps.
- **Added inside the timed step.** The thread clock and the two counters (garbage collection, compilation) are read just after the stopwatch starts and just before it stops. Each tick of the UI probe reads the thread clock once (about 1 microsecond on Windows) and the processor number. For the scroll row, every new figure, `notRunMs` included, comes from the step with the longest UI, and `notRunMs` is computed from that step's own Wall, not from the row's `wallMs` (which is the longest Wall of any step). Wherever the scroll row's figures are set against a Wall (the failure message, `notRunShare`, the test's bound on `runMs`), that Wall is the step's own, `runMs + notRunMs`.
- **Untimed, once per run.** The rate check before the first row and after the last (25 to 200 spins of 2 ms, so about 50 ms at least). The two machine-load readings, each on a thread of its own while the interface thread waits. Not a thread-pool task: a blocking wait may run a queued task inline on the waiting thread, and the headless interface thread is itself a thread-pool thread. A `GetSystemTimes` call has been seen to block for up to 727 ms on the development PC under full load, so it is never called on the interface thread and never between timed rows. The stopwatch is read before and after each call; the midpoint is the reading's time, and the call's length goes into `loadReadMs`.
- **The real window's process-start row.** In a real window the first rate check, the core-class map and the first load reading run after the process starts and before the Board. The `process-start-to-board` row leaves that set-up out (it is timed and subtracted, and the row's note gives how long it took), so the row reads as it did before.
- **No repetition inside a process.** The first switch to Published, warm startup and the first keystroke happen once per process. A repeat would run warm and time a cheaper action than the one the budget is about. Rows are repeated only by running more processes: alternating pairs to compare (section 5), five runs for a verdict (section 7).
- **No quiet gate.** On a very busy PC a gate may never open: one experiment saw the PC 100% busy with 63 to 170 threads waiting. The pairs script does not wait for quiet. It records the load and replaces disturbed pairs afterwards.
- **Priority, affinity and power settings are never touched**, in measurement or in validation.
- **The last two rows, report-only.** After the scroll row the Board goes back to Pipeline, so Published is hidden after the scroll showed it, with no stage row between. `search-keystroke-published-hidden` times the search there with the same work as `search-keystroke`: "teaching" typed into an empty search, the render its wait ends in, and layout. It is shown against the filter keystroke budget for scale. It is the second "teaching" typed on its library (in the headless test the example library's two come before the 500 library's), so it runs warm. `switch-published-after-hidden-change` then times the switch that shows Published again, against the page switch budget for scale, and stays the last row of the 500 library; the headless test fails if either row is missing or moved. Neither is a row of d1 6.3 and neither can fail a test by its time; the checks made after them can. Their figures are compared only between two builds that carry the same measurement code (section 5), never with a budget row: for #404 the search is the primary row and the switch is named in advance as expected to rise. No row above them changed in work, order or id; in the headless test the example library's two new rows run before the 500 library's, and the run's conditions now cover the two rows.

## 4 The corrected figure

**Definition.** `runUiMs` = the largest, over the row's probe gaps, of (thread clock at the end of the gap - thread clock at its start). The tail from the last tick to the end of the row counts as a gap. Because it is measured gap by gap, it stays right where UI is below Wall. That happens headless too: on an Ubuntu CI run the open list read Wall 104.6 and UI 66.4 ms.

**The thread clock.**

- Linux: `clock_gettime(CLOCK_THREAD_CPUTIME_ID, ...)` through P/Invoke into `libc.so.6`, checked with `NativeLibrary.TryLoad` first. If it does not load, the figures are `null`. No conversion is needed.
- Windows: `QueryThreadCycleTime` on the current thread, divided by the **nominal** rate, the registry's `~MHz` value for processor 0 times 1000. Microsoft says not to turn these cycles into time in general, so every run checks that the counter runs at that rate: spins of 2 ms, untimed, at least 25 and at most 200, until one spin's cycles per stopwatch millisecond come within 2% of nominal. Being taken off the processor can only lower a spin's ratio, so one close spin shows the counter is not slower than nominal. It cannot show the counter is not faster: a spin of a faster counter that was taken off the processor for a while can land inside the 2% window. So a spin that reads more than 2% above nominal fails the check at once, since no time off the processor explains it. A faster counter is then caught as soon as one spin runs without being taken off the processor; on the development PC under load about one spin in five did, so 25 spins leave a faster counter unseen in under 1% of checks (0.8 to the 25th power). In a reviewer's probe at the true rate, 400 spins read at most 1.0003 of nominal, quiet and under load. The check runs before the first row and again after the last. If either fails, every thread-clock figure of the run is `null` and `threadClock` says `absent: rate check failed`. The largest ratio is not used as the rate: it sits at or just below the true rate and so would overstate running time.
- Not used: `GetThreadTimes`. On the development PC it moves only in steps of 15.6 ms.

**Arithmetic.** `runMs` = clock after - clock before. `notRunMs` = `wallMs - runMs`, recorded raw. A gap's running time is at most the gap, so `runMs <= wallMs` and `runUiMs <= uiMs` should hold up to the clock's error. The headless budget test checks them with a relative margin: `runMs <= 1.02 x wallMs + 1` and `runUiMs <= 1.02 x uiMs + 1` (the 2% matches the rate check), and writes each comparison to the test log. Load only takes time away from the thread, so it cannot push a figure past these bounds. Nothing is clamped. Garbage-collection and compilation time are shown beside the figure and never subtracted: when they happen inside a row, they are part of what that row costs.

**What it cannot remove.** Slower running: a busy twin on the same core, an efficiency core instead of a performance core, a lower clock when the chip is busy, cold caches, memory bandwidth taken by other programs. Also the Board's own garbage collections and first-use compilation, which `gcPauseMs` and `jitMs` show.

**What it leaves out that is real.** Waits that belong to the action: a garbage-collection suspension started by another thread, hard page faults, locks, waits on the render thread. These fall into "not running", so `runUiMs` can read below the action's undisturbed cost. On CI virtual machines, time the host takes may count as running (assumed). Rows with `waits` keep the figure, but it cannot separate waiting by design from load, so they are left out of `notRunShare` and out of the validation.

## 5 Comparisons

The rule in force: an optimisation is merged only when it helps by more than the noise. Made exact:

1. **Before any run**, the pull request names **one primary row**, the direction expected, the mode, and **the deciding sitting**. It may also name rows expected to rise. Every other row is a **watch row**.
2. **One sitting.** Both builds are Release, carry the same measurement code, and use the 500 test library. Alternating pairs (before, after; after, before; ...): **10 pairs** for an expected gain of 20% or more, **20 pairs** for 10 to 15%, and 20 pairs on a busy day whenever the expected gain is under 30%. A simulation built from the development PC's measured spread put the chance that 10 pairs detect a 20% gain at 0.93 on a quiet night but only 0.41 on a busy day, and a 10% gain at only 0.11. A sitting of fewer than 10 pairs never decides.
3. **Replacement** happens only when a run was `disturbed`, wrote no JSON, ended in error, timed out, lacks a row or a UI figure that its partner has, or outlasted its load. It is decided from those facts before any timing is read; a `null` disturbed counts as not disturbed. One of them is not free of timings: whether a run outlasted its load depends on how long the run took, so slower runs are dropped more often. The summary lists those drops apart. The pair is replaced by a new pair at the end. At most 5 replacements; otherwise the sitting is void. Every dropped run is listed. A sitting is judged only at the pairs it planned: one stopped short is "not decided", so a sitting cannot be read after each pair and stopped at the first count that passes.
4. **Sign test on UI.** Count the pairs where after's UI is lower than before's. **Ties count against.** The gain is shown when the count reaches the one-sided 5% level of a fair coin (exact binomial):

   | pairs | 10 | 11 | 12 | 13 | 14 | 15 | 16 | 17 | 18 | 19 | 20 |
   |---|---|---|---|---|---|---|---|---|---|---|---|
   | lower in at least | 9 | 9 | 10 | 10 | 11 | 12 | 12 | 13 | 13 | 14 | 15 |

   For example P(at least 9 of 10) = 11/1024 = 0.011 and P(at least 8 of 10) = 0.055; P(at least 15 of 20) = 0.021 and P(at least 14 of 20) = 0.058. One alpha, 0.05 one-sided, everywhere.
5. **One deciding sitting per change and mode.** Re-running sittings until one passes adds up their chances of a false pass. A second sitting is allowed only after a code change. Otherwise every sitting's pairs are pooled into one count and judged at the same alpha (the table, extended by exact binomial). Every sitting, passed or failed, is listed in the pull request. Pooling slows the adding-up but does not stop it: each pooled look is another chance. For a change that does nothing, the exact chance of a false pass at the first look that passes is, with sittings of 10 pairs, 0.011 at one look, 0.027 at two, 0.061 at three and 0.074 at four; with sittings of 20 pairs, 0.021, 0.051, 0.074 and 0.091. So three sittings of 10 pairs, or two of 20, go past 5%, and the summary gives these figures whenever sittings are pooled. How to hold the line past two sittings is a question for the maintainer (section 12). A sitting given twice, or a copy of one, is refused.
6. **Size floor.** The median of the paired differences (before - after) of UI is at least the larger of 2 ms and 3% of before's median.
7. **Run UI must not point the other way.** Run UI, report-only, is tested the same way. If after is higher on Run UI by the step 4 count, stop: a person looks before anything is merged.
8. **Watch rows.** If after is higher on a watch row by the step 4 count, run a new sitting for that row. Block the merge only if the same row is higher again. A watch row that gains is a finding, not a reason to merge.
9. **Report** for the primary row: both medians, the count, the median difference, and the median of the after / before ratios with the interval from the 2nd smallest to the 2nd largest ratio (97.9% at 10 pairs).
10. Figures of two sittings or two days are **never** compared. The same code has read 157 and 305 ms on two days.

The deciding sitting is a real-window sitting. Real-window sittings are run on the development PC by the maintainer or the coordinating session, never by automated agents or CI. A headless sitting is screening only.

## 6 The validation experiment (a protocol, not yet run)

**Question.** Does `runUiMs` stay put when a known extra load is added, where Wall and UI do not? And what load can it not remove?

**The load.** `ManualCertification/Start-BoardTrialLoad.ps1 -Kind Spin|Memory -Seconds 60` starts a **separate** process. Load threads inside the test process would stretch its garbage-collection pauses and mix two effects. The process runs 6 threads at normal priority. Spin threads do integer arithmetic. Memory threads each stream a 64 MB array, one write per 64-byte line. Starting the process and compiling the load took 5 to 25 s on the development PC, so the script returns only once the threads are running (the process prints "ready"; a load not running within 60 s is stopped and the script fails), and `-Seconds` counts from that moment. The process ends by itself `-Seconds` (at most 60) after it is ready, and the caller also stops it in a `finally` block. The load runs in its own hidden console, so Ctrl+C reaches it only through the caller's `finally` block; if the caller is killed without it, the load still ends by itself. It changes no setting. With about 2.7 processors of other work, 6 threads still leave 12 logical processors short of full, so the spin arm tests **modest oversubscription only, not saturation**. Where it does slow the Board, it may be through twin sharing or core kind rather than time off the processor.

**Protocol.** One sitting; a Release build of master with the measurement code; the headless budget test alone, each run its own process.

- Arms: **R** (the PC as it is), **S** (R plus the spin load), **M** (R plus the memory load). Order R S M M S R, twice: 4 runs per arm, 12 runs. The load starts 2 s before the test process and ends when it exits. A run that outlasts its load is dropped and run again. **Open before step 9:** the load lasts at most 60 s, and a headless run of the budget test alone takes 0.5 to 0.75 min on a good day but took 7 to 8 min on November 21. On such a day every loaded headless run outlasts its load, and dropping them favours the fast runs. The headless arms need runs shorter than about 55 s, or another way to time the load; that choice is the maintainer's (section 12). The pairs script warns when `-Load` is used with `-Mode headless`.
- Rows checked: rows without `waits` whose median `runUiMs` under R is at least 20 ms (on the 500 library: the first switch, the stage rows, the open list, the search keystroke).
- The same in a real window: 6 R and 6 S, alternating, through the pairs script's `-Load` switch, which puts the load under the "after" runs only (run with the same build as before and after). Like every real-window sitting, this is run on the development PC by the maintainer or the coordinating session, never by automated agents or CI.

**Fixed before the first run.** The bounds below are frozen before any data is taken. A revised bound is tested only on a new set of runs, never on the runs that suggested it. The disturbed line may be re-set from the R runs, but only for later comparison sittings.

**Disturbed rows first.** Let B be a row's pass bound: the larger of 10% of R's median `runUiMs` and 3 ms. A checked row counts only if S raised its median UI by at least 3 x B. A row that did not is reported as "not disturbed", not as passed. If no row is disturbed, repeat at another time of day, at most three sittings; then report "S could not disturb this PC". Never add threads.

**R against R first.** Split the R runs into two halves (first and second in time). Each checked row must pass the same bounds R against R. If not, the PC's own swings are larger than the bound and the experiment is uninformative.

**Pass, for each disturbed row** (both must hold):

- absolute: |median S `runUiMs` - median R `runUiMs`| <= B;
- removal: |median S `runUiMs` - median R `runUiMs`| <= 0.25 x (median S UI - median R UI).

The rate check must also have passed in every run. Under M the figures are reported against the same bounds, and a failure is expected.

**The real load, report-only, at no extra runs.** Across all R runs and every pair already recorded with the new figures, report how UI and `runUiMs` change with `othersBusyProcessors` and `notRunShare` (a table by load band, and the slope of each). If the correction works, `runUiMs` changes much less than UI.

**What to conclude.**

- S passes, M passes: the correction removes both kinds of load seen here. Write that, and the M figures.
- S passes, M fails: Run UI removes time off the processor but not memory contention. The report says so beside every Run UI figure.
- S fails: first compare `perfCoreShare` between R and S. If it differs, the failure may come from core placement (an assumption, not a measured cause); say so, and make no claim. Otherwise Run UI is not trusted even as a diagnosis on that PC: it stays in the JSON, labelled "not validated". Check whether the S runs failed the rate check.

## 7 The verdict rule proposed to the maintainer (a proposal)

For "the real window decides", once accepted:

- **Figure:** UI, as today. A budget is about what a person feels, busy PC or not.
- **Runs:** 5 real-window runs that are not `disturbed` (a `null` counts as not disturbed and is printed "load unknown"), from at most 10 tries in one sitting (Release, the 500 test library). Verdict: the median UI of those 5 against the budget.
- **Too busy to judge:** fewer than 5 undisturbed runs out of 10. The report says so and lists each run's `othersBusyProcessors` and `notRunShare`.
- **Beside it, report-only:** the median `runUiMs`, `perfCoreShare`, `gcPauseMs` and `jitMs`, and whether the validation passed. Never a lower bound or a low quantile.
- **Headless in CI** keeps today's single run, multipliers and rule as the guard.

Until it is accepted, the pairs script prints these lines under "proposed verdict (report-only)", and they decide nothing.

## 8 The failure message of the headless timing tests

The test still fails on today's rule. The message is built from the failed rows and the run's `conditions`, all measured in that run:

```
500: Stage filter back to All stages took 404 ms against 250 ms.
  Of the 404 ms from its start to its end, the interface thread ran for 151 ms and was not running for 253 ms (63%); garbage collection paused 12 ms, compilation 0 ms.
During this run other programs kept 9.8 of 12 processors busy on average.
Timing tests need a quiet PC. Close other programs and run the test again before reading this as a slower Board.
```

- The first line gives UI, as before. The second line names the row's Wall, "from its start to its end", because its figures are shares of the Wall, and on some rows UI is shorter than the Wall (for example `took 259 ms against 250 ms.` and then `Of the 336 ms from its start to its end, the interface thread ran for 284 ms ...`).
- With several failed rows, each row's two lines come first, then the load line and the last line once.
- When the running time could not be read, the second line reads `How long the interface thread ran could not be read on this system.`
- When the load could not be read, the third line reads `How busy the PC was could not be read on this system.`
- When the thread ran for at least 90% of the row, the second line reads, for example, `Of the 257 ms from its start to its end, the interface thread was running for 240 ms. A busy PC can still slow running code.`
- The budget shown is the run's own (x2 Debug, x3 on GitHub Actions), as before.
- The last line is always there, so the message also reads correctly on a quiet PC.

The message states measured evidence. It never names a cause.

The Windows Forms timing tests (`ResponsivenessBudgetTests.vb`) end their two failure messages with the same last line. They have no thread clock and no load reading to quote, so the line is all they add.

## 9 Running a comparison

`ManualCertification/Measure-BoardPairs.ps1`:

- `-Mode window -Before <exe> -After <exe> -Library <test library>`: each run is the trial's `--measure` mode in a real window. Run only on the development PC by the maintainer or the coordinating session.
- `-Mode headless -Before <tree> -After <tree>`: each run is the headless budget test alone, `dotnet test --no-build` in a built tree.
- `-Out <new folder>`, `-Pairs`, `-PrimaryRow <row id>`, `-ExpectedToRise <row ids>`, `-BeforeName`, `-AfterName`, `-Load None|Spin|Memory`.
- `-BeforeIdle` and `-AfterIdle`, given together: each build's side of an idle-preparation comparison (section 13).
- `-RunLimitMinutes`: a run still going after this long is stopped and its pair replaced ("timed out"). By default 15 min headless and 3 min in a window, far above any budget, so the limit is not a timing.
- `-Pool <earlier sittings' folders>` counts earlier sittings of the same change, mode and primary row together with this one. A void sitting is listed but adds no pairs. A sitting given twice, or a copy of one, is refused.
- `-Analyze <a sitting's folder>` reads a sitting again without running anything, for a later re-analysis or a check.

The script runs pairs until it has kept `-Pairs` of them. Once a pair's first run is dropped, its second run is not spent: the pair is replaced whatever it would give. A sitting stopped by hand is judged "not decided" (section 5, step 3); if it stopped between the two runs of its last pair, that pair is "not finished", not replaced.

Each sitting's folder holds `sitting.json` (the runs, in order, with their exit codes), one JSON file per run, each run's output, and `summary.md`. The JSON files name the computer and the runs' output may hold folder paths, so the sitting's folder stays on the PC that ran it; only `summary.md` is shared. The summary names no computer and no folder.

`ManualCertification/Test-BoardPairs.ps1` checks the rule on canned sittings and needs no build: the counts against the table above, 9 of 10 (shown), 8 of 10 (not shown, with the P of the count seen printed apart from the P of the count needed), a disturbed pair that is replaced (with and without its second run), a tie, absent figures, the size floor (also one between the rounded and the true floor), Run UI pointing the other way, a watch row higher once and again, a void sitting, two and three sittings pooled, a sitting given twice or copied (refused), a sitting stopped short and one stopped inside its last pair (not decided), a missing row, a null UI figure and a run that timed out (replaced), a run that outlasted its load (listed apart), and five pairs (never decides). The trial's Windows CI job runs it.

## 10 What one run and one comparison cost

- One real-window run: about 0.3 min. The new readings add about 0.2 s, and up to about 1.5 s while `GetSystemTimes` is slow under load.
- One headless run of the budget test alone: about 0.5 to 0.75 min (the test method itself took 15.4 s in one recorded run; process start and discovery are assumed to add 15 to 30 s). On November 21, with other programs keeping 10.7 to 11.4 of 12 processors busy, two such runs through the pairs script took 7.2 and 7.9 min.
- One Release build of the trial and both test projects: assumed 5 to 15 min on a busy PC. A comparison needs two builds; the validation needs one. The before and after trees live in worktrees outside the system's temporary folder.
- A verdict sitting (section 7): 5 to 10 real-window runs, about 2 to 3 min.
- A comparison: 10 pairs in a real window about 7 min, headless about 10 to 15 min. 20 pairs double that. Up to 5 replacement pairs add up to half again. Add 10 to 30 min for the two builds.
- The validation: 12 headless runs, about 8 to 10 min plus about 2 min for the load starts, plus one build. Up to two more sittings if the spin load does not disturb. The real-window part, 12 runs, is about 4 min.

## 11 Verified, assumed, and left out

**Verified by reading the code:**

- UI is the smaller of Wall and the longest gap of a 1 ms timer at Input priority, the probe's tail included. Each row is timed once, in a fixed order.
- The scroll row takes its longest Wall and its longest UI as two separate maxima over its steps, and the row's `wallMs` is the longest Wall.
- Rows of N is the only shelf layout; the every-card build was removed. The search keystroke, the first switch, Stage to Submitted and the scroll are over their budgets under rows of N too: they are measured and reported with the rule `waiting: over under rows of N too` (`WaitingForRowsOfN`), and the headless test fails on enforced rows only.
- The Windows Forms tests build the same failure text as the trial's first line, except that the trial's line now ends with a full stop.

**Measured on the development PC while the method was designed** (probes, not part of the tests):

- Over 200 spins of 2 ms, 78 came within 2% of the nominal 2,496 MHz, and the closest read 0.02% above it. `QueryThreadCycleTime` took 0.8 to 1.4 microseconds a call. Logical processors 0 to 3 are of the faster class, 4 to 11 of the slower; a normal-priority thread from a background console ran only on 4 to 11 over 300 wakeups.
- `GetThreadTimes` moves in 15.6 ms steps. `GetSystemTimes` took a median of 8.5 ms at an ordinary moment and blocked for a median of 398 ms (up to 727 ms) under full load. A busy twin core made the same work take about 3.6 times the processor time.
- With a synthetic load, running time stayed at 23.5 to 25.5 ms while Wall went from 24 to 148 ms, and memory load raised running time by 21 to 41%.

**Measured with the built code** (headless budget test on the development PC): under the first rule of the rate check (stop at the first spin within 2%), the checks passed at 0.98 to 0.999 of nominal after 1 to 9 spins; a 50 ms sleep read under 0.03 ms of running time; every 500 row stayed within `runMs <= 1.02 x wallMs + 1` and `runUiMs <= 1.02 x uiMs + 1`. In ordinary runs other programs kept 10.7 to 11.4 of 12 processors busy, so every run was `disturbed`. On a day like that, a headless sitting under the starting line would be void.

One observation from two headless runs of the same build, not a finding: Stage filter back to All stages read Run UI 185 ms with a fast-core share of 1, and 338 ms with a share of 0. Run UI does not remove the kind of core a run lands on (section 1).

**Shown by CI** (run 45003699839 of 2026-11-22, on the pull request that added this document; each runner has 4 processors):

- **Ubuntu:** `threadClock` is `thread-cputime`, in the headless test and in the real window under a virtual display. The load was read: other programs kept 0.04 (headless) and 0.23 (window) of the 4 processors busy, and `stealProcessors` read 0. So `clock_gettime` through `libc.so.6`, `/proc/stat` and `sysconf` work on the runner.
- **Windows:** `threadClock` is `cycles` at 2,596,000 cycles per ms, so the registry's `~MHz` value exists on the runner. The two rate checks read 0.9981 and 0.9980 of nominal, each after 25 spins, and passed.
- `perfCoreShare` is `null` on both runners.
- One observation from one run on each system, not a finding: headless, on the 500 library, the first switch to Published ran for 395.1 of its 401.9 ms on Ubuntu, with a garbage-collection pause of 80.2 ms inside the running time, and for 91.4 of its 125.6 ms on Windows, with a pause of 34.4 ms. How a pause shows in the thread's own clock on each system has not been looked into.

**Assumed:**

- That removing time off the processor is most of what a real busy day adds. One synthetic experiment suggests it; the validation and the new figures will show it.
- That `sched_getcpu` and `cpu_capacity` give a core-class map on a Linux machine with two kinds of core. The CI runner gave none, as expected of a virtual machine.
- That the thread-cycle counter runs at the nominal rate on other Windows PCs. The per-run check catches a slower counter, and a faster one as soon as one spin runs without being taken off the processor (section 4); a PC so busy that no spin of 25 runs whole could still let a faster counter through.
- That `Environment.ProcessorCount` is the machine's processor count. The load readings count every processor, while `ProcessorCount` follows an affinity or container limit; under such a limit a run is marked disturbed too often. On the development PC and the GitHub runners the two are equal.
- That the "fewer than two processors free" line is right. It is a starting value.
- The run and build times of section 10, except the two recorded ones.
- That the interface thread does not wait on the render thread inside the synchronous rows.
- That time a CI host takes from its virtual machine may count as running.

**Left out, and why:**

- Load per row: too coarse, and the reading blocks under load.
- Repetition inside a process and a low quantile: warm repeats time a cheaper action, and a low quantile reads low by design.
- A lower bound as a figure: it must never be a verdict, so it is not recorded.
- A run at High priority as a reference: it changes priority, which is ruled out; R serves as the reference.
- Power mode, battery, Linux pressure stall information, run queue and context switches: `notRunMs` holds their effect.
- Raw cycle counts: they can be recomputed from `runMs` and `cyclesPerMs`.
- Dividing by a calibration workload, or subtracting garbage-collection and compilation time: neither is honest.
- Correcting for core kind: `perfCoreShare` is recorded, but no honest factor between the two kinds exists for every row.
- Event tracing: it needs elevation and is not on CI.
- A quiet gate: it may never open.
- Automatic retries of a failed test: failures are accepted and explained.

## 12 Defaults the maintainer can change

These follow the method's own recommendations and were taken as defaults while the maintainer was away:

1. **Run UI counts toward no verdict and no merge.** It is a diagnosis: a row whose Run UI is over budget is over on its own work.
2. **A run is disturbed** when other programs leave fewer than two of the PC's logical processors free on average. A starting value; it may be re-set once from the validation's R runs, for later sittings only.
3. **When Published is prepared while the Board is idle**, the switch after it gets a new id (`first-switch-published-after-idle`), so today's `first-switch-published` keeps its meaning. The maintainer chose this on November 27, 2026; it is built (section 13).
4. **The verdict rule of section 7 stays a proposal**, printed under "proposed verdict (report-only)".

Open points, left to the maintainer (nothing in the code decides them):

- **Pooling past two sittings.** Three sittings of 10 pairs, or two of 20, take the chance of a false pass past 5% (section 5). Options: cap pooling at two sittings of 10 pairs; decide only at a total number of pairs fixed in advance; or use a stricter count for pooled looks. Until then the summary prints the figures.
- **The load and long headless runs.** A loaded headless run that outlasts the 60 s load is dropped, and on a busy day every one does (section 6).
- **The slow-step limit in a measurement run.** A measurement run keeps the 120 ms limit of idle preparation, so headless runs on the 500 library stop before Published is prepared and do not time the first switch after idle (section 13). Raising the limit for measurement runs would change what is measured; dropping the runs that stopped favours the faster moments of the PC.

## 13 Preparing Published while the Board is idle

The Board can prepare the hidden Published shelf in a few short steps while nobody touches it, so that its first switch makes no card view (the maintainer's decision of November 21, 2026). Preparation is off unless a test or a run turns it on; the trial app does not turn it on yet. These rows show what it does to the first switch and what each step costs.

**How a run is told its side.** A run is one side of a with-and-without comparison, or neither:

- In a real window: `PaperRoute.Desktop.exe --snapshot-from <library> --measure <file>.json --after-idle <side>`. `--after-idle` is accepted only with `--measure`.
- Headless: the budget test reads the variable `PAPERROUTE_TRIAL_AFTER_IDLE`.
- The side is one of `none` (no step), `width` (the width step only), `half` (the width step and the first two layout steps) or `prepared` (every step, until the Board says Published is prepared).
- With no side, the run is exactly as before: the same rows in the same order, `first-switch-published` among them, and no `idlePreparation` in the JSON.

**The rows of a run told a side.** They take the place of `first-switch-published` and its paint, after the startup rows, so every row after them keeps its work and its order, and the last two rows stay last. A first switch happens once per window, so a run has either these rows or `first-switch-published`, never both. Each is report-only, and none can fail a test by its time.

| Row | What it times | Budget, for scale |
|---|---|---|
| `prepare-published-idle` | the preparation from its start to done, a frame after each step; Wall and Ran are its cost, UI the longest wait a key would meet | filter keystroke, 50 |
| `prepare-published-longest-step` | its longest step: Wall the longest step's, UI and every busy-PC figure the longest-UI step's. Its note lists each step: W (width), R (rows) or L (layout, with its height cap on the ladder), Wall, UI and card views made. This is the row to judge in a real window | UI thread, 250 |
| `first-switch-published-after-idle` | the first switch after the wait below; its note gives the steps before it, the card views ready in the hidden shelf and the card views made at the switch | page switch, 100 |
| `first-switch-published-after-idle-paint` | its frame | UI thread, 250 |

The measurement drives the preparation itself, step by step, with the Board's idle timer off, so no step can run inside another row; the warm-startup row's note says how many ran inside it (none). Then every side waits the same way: preparation done (at once on the side `none`), a fixed idle period of 1 s, one frame. Only then is the switch timed. On the side `none` the two preparation rows read about 0, with 0 steps.

When preparation stops (a step that took, or is predicted to take, more than 120 ms, or an accessibility client that has read the Board), the switch is not the one the row is about. It is made untimed, so the rows after it do the same work, and the two switch rows are not written. The JSON's `idlePreparation` gives, per library, the side, the steps run, the card views they made, whether Published was prepared at the switch, why preparation stopped (`skippedReason`, or `null`), whether the switch was timed, and the card views it made.

**Running a comparison.** The same build on both sides, so the pairs compare the preparation and nothing else; the wait before the switch is the same on both sides, so warm-up during the wait falls on both alike:

```
pwsh -NoProfile -File ./ManualCertification/Measure-BoardPairs.ps1 -Mode window -Before <exe> -After <same exe> -Library <test library> -BeforeIdle none -AfterIdle prepared -BeforeName without -AfterName with -Out <new folder> -Pairs 10 -PrimaryRow first-switch-published-after-idle
pwsh -NoProfile -File ./ManualCertification/Measure-BoardPairs.ps1 -Mode headless -Before <tree> -After <same tree> -BeforeIdle none -AfterIdle prepared -BeforeName without -AfterName with -Out <new folder> -Pairs 10 -PrimaryRow first-switch-published-after-idle
```

The primary row is `first-switch-published-after-idle`, expected lower on the side `prepared`. The other rows after it are watch rows. A run whose preparation stopped lacks the primary row: it is dropped and its pair replaced (section 5, step 3), and the summary lists these drops apart, because whether a step is slow is a timing and slower runs are dropped more often. Pooling refuses a sitting of other sides.

**Compared only between the two sides of a sitting.** These rows are never compared with `first-switch-published`, with a budget row, with another sitting or with another day. Only the pairs inside one sitting are.

**Counts, which do not depend on the load** (headless, the development PC, November 27, 2026). With the preparer's clock standing still, so that no step reads as slow, the 500 library's first switch after idle made 21 card views on the side `none` and 0 on the side `prepared`, where the five steps had made those 21. With the real clock, headless, preparation on the 500 library stopped as a slow step after the width step and two layout steps (6 card views made) in every run of one sitting, while other programs kept under 2 of 12 processors busy. Headless card views cost several times what they cost in a real window, so this says nothing yet about a real window; until a real-window sitting has run, the headless sitting on the 500 library is void on the side `prepared`. Whether a measurement run may raise the 120 ms limit is left to the maintainer: it would change what is measured.
