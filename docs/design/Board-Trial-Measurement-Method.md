# Board trial: timing on a busy PC

How the Avalonia Board trial times its Board, what it records about how busy the PC was, and how two builds are compared. It answers the maintainer's note of November 21, 2026 on the two rows still over their budget: "Remember that these tests are on a very, very busy PC. Perhaps we can account or correct for that?"

**Every figure this method adds is report-only.** The budgets, the multipliers (Debug x2, GitHub Actions x3) and the headless test's rule are what they were before. Wall and UI are computed as before; the new reads add a few microseconds inside each row's stopwatch and at each probe tick (section 3). Run UI failed the validation of November 27, 2026 (section 6): it is labelled "not validated", it is not trusted even as a diagnosis on the development PC, and it counts toward no verdict. The verdict rule of section 7, on UI, is in force from November 27, 2026, by the maintainer's decision.

Sections 1 to 8 keep the numbers that the code's comments cite as "busy-PC design, section N".

## What is built, and what is not yet

Built (the pull request that adds this document):

- The thread clock, the new per-row figures, the run's `conditions`, the failure message (sections 1 to 4 and 8). `PaperRoute.Desktop/Measurement/ThreadClock.cs`, `RunConditions.cs`, `BoardMeasurement.cs`, `WindowMeasurement.cs`, and `PaperRoute.ViewModels/Measurement/BoardMeasurementReport.cs`.
- The quiet-PC line at the end of the Windows Forms timing tests' failure messages (`PaperRoute.Tests/ResponsivenessBudgetTests.vb`).
- `ManualCertification/Measure-BoardPairs.ps1`, the comparison of section 5 and the verdict lines of section 7, with its check on canned measurements, `ManualCertification/Test-BoardPairs.ps1`, which the trial's Windows CI job runs.
- `ManualCertification/Start-BoardTrialLoad.ps1`, the known load of section 6.

Built after it:

- The two report-only rows at the end of the measured sequence, `search-keystroke-published-hidden` and `switch-published-after-hidden-change` (section 3, last bullet), in `BoardMeasurement.cs`, with their checks in `PaperRoute.Desktop.Tests/BoardBudgetTests.cs` and `MeasurementReportTests.cs`. They are compared only between builds.
- After the validation and the decisions of November 27, 2026: the new disturbed line (section 2), in `MeasurementConditions.DisturbedLine` and `RunConditions.cs`; the label "not validated" on Run UI in the JSON (`runUiMsStatus`), in the report's table and in the pairs summary; the pairs script's verdict lines under the rule in force, with no budget verdict for a report-only row and no verdict read from Run UI (sections 5, 7 and 9); and a load that lasts up to 120 s and takes 1 to 6 threads (section 6).
- The rows for preparing Published while the Board is idle and the way a run is told its side (section 13), in `BoardMeasurement.cs`, `TrialOptions.cs` and the pairs script, with their checks in `BoardBudgetTests.cs`, `MeasureModeTests.cs` and `Test-BoardPairs.ps1`.
- The rest of the idle design's timed rows, as scenarios with their own sides (section 13, "The other scenarios"): the time to "prepared" with the Board's own idle timer, a key that meets a step and a key after a pause, the first switch after a search with and without the idle refresh, the refresh itself (also on a library of several thousand), a switch asked for during a step, a switch just after a maximise or a scaling change, managed memory and a theme change, and the frame after each step. All report-only, with their load-free checks in `BoardBudgetTests.cs` and `HiddenShelfPreparationTests.cs`.

Run on the development PC:

- The validation of section 6, on November 27, 2026. **Run UI failed it.** On that PC Run UI is not trusted even as a diagnosis; it stays in the JSON, labelled "not validated". It has been validated on no PC.
- The load series of November 27, 2026 (section 6), from which the maintainer set the disturbed line.
- A deciding real-window sitting for #404's search with Published hidden, on November 27, 2026, under the disturbed line then in force.

Not built yet:

- For idle preparation: a scaling change in a real window (the trial cannot change its own display scaling, so that scenario runs headless only), keys sent as real key presses in a real window, and the check by hand of whether normal use skips preparation, which needs the app to turn preparation on (a later step). No sitting of the other scenarios has been run (section 13).

On Linux the thread clock and the load readings first ran in CI, and they gave readings (section 11, "Shown by CI").

## 1 What the figures mean, in plain words

- **Wall.** A stopwatch around one action, such as typing the search.
- **UI** (the verdict). The longest time during the action when a key press could not have been handled. A 1 ms timer on the interface thread measures it, and it is never more than Wall. UI is what a person would have felt on the PC as it was at that moment.
- **Ran** (`runMs`). How long the interface thread was actually running on a processor during the action. Windows and Linux keep this count for every thread.
- **Not running** (`notRunMs` = Wall - Ran). Time the thread was ready but other programs had the processor, or time it was waiting (for a frame, the disk, a garbage collection run by another thread).
- **Run UI** (`runUiMs`, the corrected figure). The same longest stall as UI, but counting only the time the thread was running. It was meant to answer one question: how long was the Board's own work in one stretch, leaving out the time other programs took the processor away? It failed the validation of November 27, 2026 (section 6), so it is labelled "not validated" wherever it is written.
- **Fast-core share** (`perfCoreShare`). The share of the probe's readings taken on the PC's faster kind of core. The development PC has 12 logical processors of two kinds: 4 faster ones on 2 performance cores, each with a twin, and 8 efficiency cores.
- **How busy the PC was** (per run). On average, how many of the PC's logical processors other programs kept busy during the run.

What the correction can and cannot do:

- It removes time when the Board **was not running**. In one experiment with a synthetic load, a piece of work kept 23.5 to 25.5 ms of running time while its stopwatch time went from 24 to 148 ms. In the validation of November 27, 2026 this did not carry over to the Board: under a known spin load the time not running stayed small, and Run UI rose by almost as much as UI (section 6).
- It cannot remove the Board **running slower** while it runs: two programs sharing one core, an efficiency core instead of a performance core, caches and memory traffic taken by other programs, a lower processor speed, the Board's own garbage collection and its first-use compilation. The same experiment showed a memory-heavy load raising running time by 21 to 41%. So Run UI is **not** "the cost on a quiet PC". It can read high (slower running) and it can read low (real waits by the Board itself are left out).
- Headless runs started from a console in the background and real-window runs in the foreground may run on different kinds of core. `perfCoreShare` shows this; it does not correct for it.
- So Run UI was meant as a **diagnosis**: a row whose Run UI is well over its budget would be over because of its own work, and a row whose UI is over but whose Run UI is under might be over because of the load. It failed the validation (section 6), so on the development PC it is not trusted even as that. It stays in the JSON and the summaries, labelled "not validated", and no verdict reads it. The verdict uses UI.

## 2 What is recorded

Per row, in `rows[]` of the JSON. A figure that could not be read is JSON `null` and "absent" in the table, never 0, and it never fails a test.

| JSON name | Unit | How it is read: Windows / Linux | Absent when |
|---|---|---|---|
| `runMs` | ms | the thread clock (section 4), read just inside the stopwatch | no thread clock on this system, or the run's rate check failed; `process-start-to-board` (not timed on the thread) |
| `notRunMs` | ms | `wallMs - runMs` as computed, not clamped (a small negative value shows the clock's error) | as `runMs` |
| `runUiMs` | ms | the largest thread-clock increase inside one probe gap, the tail to the row's end included. Not validated (section 6): the run's `runUiMsStatus` says so | as `runMs` |
| `perfCoreShare` | 0 to 1 | share of probe readings on the highest efficiency class. Windows: `GetCurrentProcessorNumberEx`, mapped once per run with `GetSystemCpuSetInformation`. Linux: `sched_getcpu` with `/sys/devices/system/cpu/cpuN/cpu_capacity` | Linux without `cpu_capacity` (usual on x86 virtual machines); all processors of one class |
| `gcPauseMs` | ms | change in `GC.GetTotalPauseDuration()`, the whole process | not measured for `process-start-to-board` |
| `jitMs` | ms | change in `JitInfo.GetCompilationTime(currentThread: true)` | as `gcPauseMs` |
| `waits` | true | written only for a row that waits by design: warm startup (the load runs off the thread), every paint row, the scroll row in a real window, `process-start-to-board` | never written as false |

Per run, beside `rows`: `runUiMsStatus`, the text `"not validated"`, written by builds from the change that labels Run UI on. Earlier files, those of November 27, 2026 included, do not carry it, and no reader needs it: the pairs script labels Run UI by itself. The report's table heads the column "Run UI ms (not validated)", and the pairs summary writes "Run UI (not validated)".

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
| `disturbed` | true/false | `othersBusyProcessors > processors / 4`: other programs kept more than a quarter of the processors busy, on average over the run (3 of 12 on the development PC, 1 of 4 on a CI runner). The line of November 27, 2026; before it, `othersBusyProcessors > processors - 2` | `null` when `othersBusyProcessors` is absent. `null` counts as not disturbed: the run is kept and printed as "load unknown" |

`machineBusyProcessors` can read slightly above the number of processors, because the counters move in steps. It is not clamped.

`disturbed` is computed from the unrounded reading, while `othersBusyProcessors` is written rounded to 0.01. A run at 3.004 of 12 is written as 3 and still counts as disturbed.

**The disturbed line.** The method started with "other programs leave fewer than two processors free" and allowed one re-set after the validation, for later sittings only. On November 27, 2026 the maintainer made that re-set: a run is disturbed when other programs keep more than 3 of the 12 processors busy. The code writes it as a quarter of the processors, so it reads 1 of 4 on a CI runner. It came after two findings of that day (section 6): under the spin load other programs kept 7.0 to 9.7 processors busy and UI rose by 68 to 149% on the six disturbed rows in the real window, yet no run counted as disturbed under the old line; and the load series showed timings rising from the first extra load thread. The line holds for later sittings only. Sittings already taken keep their verdicts: each run's JSON carries the flag its own build wrote, and the pairs script reads that flag, so reading an earlier sitting again drops the same runs. For the same reason a pool of sittings that spans the change (section 9, `-Pool`) counts each sitting under the line its own runs were written with.

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
3. **Replacement** happens only when a run was `disturbed`, wrote no JSON, ended in error, timed out, lacks a row or a UI figure that its partner has, outlasted its load, or, in a comparison of idle sides (section 13), carries another idle side than its build was given or stopped its idle preparation. It is decided from those facts before any timing is read; a `null` disturbed counts as not disturbed. Two of them are not free of timings: whether a run outlasted its load depends on how long the run took, and whether its idle preparation stopped depends on whether a step was slow, so in both slower runs are dropped more often. The summary lists both kinds of drop apart. The pair is replaced by a new pair at the end. At most 5 replacements; otherwise the sitting is void. Every dropped run is listed. A sitting is judged only at the pairs it planned: one stopped short is "not decided", so a sitting cannot be read after each pair and stopped at the first count that passes.
4. **Sign test on UI.** Count the pairs where after's UI is lower than before's. **Ties count against.** The gain is shown when the count reaches the one-sided 5% level of a fair coin (exact binomial):

   | pairs | 10 | 11 | 12 | 13 | 14 | 15 | 16 | 17 | 18 | 19 | 20 |
   |---|---|---|---|---|---|---|---|---|---|---|---|
   | lower in at least | 9 | 9 | 10 | 10 | 11 | 12 | 12 | 13 | 13 | 14 | 15 |

   For example P(at least 9 of 10) = 11/1024 = 0.011 and P(at least 8 of 10) = 0.055; P(at least 15 of 20) = 0.021 and P(at least 14 of 20) = 0.058. One alpha, 0.05 one-sided, everywhere.
5. **One deciding sitting per change and mode.** Re-running sittings until one passes adds up their chances of a false pass. A second sitting is allowed only after a code change. Otherwise every sitting's pairs are pooled into one count and judged at the same alpha (the table, extended by exact binomial). Every sitting, passed or failed, is listed in the pull request. Pooling slows the adding-up but does not stop it: each pooled look is another chance. For a change that does nothing, the exact chance of a false pass at the first look that passes is, with sittings of 10 pairs, 0.011 at one look, 0.027 at two, 0.061 at three and 0.074 at four; with sittings of 20 pairs, 0.021, 0.051, 0.074 and 0.091. So three sittings of 10 pairs, or two of 20, go past 5%, and the summary gives these figures whenever sittings are pooled. How to hold the line past two sittings is a question for the maintainer (section 12). A sitting given twice, or a copy of one, is refused.
6. **Size floor.** The median of the paired differences (before - after) of UI is at least the larger of 2 ms and 3% of before's median.
7. **Run UI decides nothing.** Until November 27, 2026 this step read: if after is higher on Run UI by the step 4 count, stop, and a person looks before anything is merged. Run UI failed the validation that day (section 6), so the summary prints its count, labelled "not validated", and no verdict reads it. Removing the stop follows from the decision that Run UI counts toward no verdict, but it was a merge safeguard, so the pull request that removes it asks the maintainer to confirm it.
8. **Watch rows.** If after is higher on a watch row by the step 4 count, run a new sitting for that row. Block the merge only if the same row is higher again. A watch row that gains is a finding, not a reason to merge.
9. **Report** for the primary row: both medians, the count, the median difference, and the median of the after / before ratios with the interval from the 2nd smallest to the 2nd largest ratio (97.9% at 10 pairs).
10. Figures of two sittings or two days are **never** compared. The same code has read 157 and 305 ms on two days.

The deciding sitting is a real-window sitting. Real-window sittings are run on the development PC by the maintainer or the coordinating session, never by automated agents or CI. A headless sitting is screening only.

## 6 The validation experiment (the protocol, and its result of November 27, 2026)

**Question.** Does `runUiMs` stay put when a known extra load is added, where Wall and UI do not? And what load can it not remove?

**The load.** `ManualCertification/Start-BoardTrialLoad.ps1 -Kind Spin|Memory -Seconds <n>` starts a **separate** process. Load threads inside the test process would stretch its garbage-collection pauses and mix two effects. The process runs 6 threads at normal priority. Since November 27, 2026 `-Threads` takes 1 to 6, so that the load series below can be repeated; 6 stays the default and the method's number. Spin threads do integer arithmetic. Memory threads each stream a 64 MB array, one write per 64-byte line. Starting the process and compiling the load took 5 to 25 s on the development PC, so the script returns only once the threads are running (the process prints "ready"; a load not running within 60 s is stopped and the script fails), and `-Seconds` counts from that moment. The process ends by itself `-Seconds` (at most 120 since November 27, 2026; at most 60 before) after it is ready, and the caller also stops it in a `finally` block. The pairs script asks for what its mode needs: 60 s under a real-window run, 120 s under a headless run. The load runs in its own hidden console, so Ctrl+C reaches it only through the caller's `finally` block; if the caller is killed without it, the load still ends by itself. It changes no setting. With about 2.7 processors of other work, 6 threads still leave 12 logical processors short of full, so the spin arm tests **modest oversubscription only, not saturation**. Where it does slow the Board, it may be through twin sharing or core kind rather than time off the processor.

**Protocol.** One sitting; a Release build of master with the measurement code; the headless budget test alone, each run its own process.

- Arms: **R** (the PC as it is), **S** (R plus the spin load), **M** (R plus the memory load). Order R S M M S R, twice: 4 runs per arm, 12 runs. The load starts 2 s before the test process and ends when it exits. A run that outlasts its load is dropped and run again. **The load's length:** the load lasted at most 60 s, and a headless run of the budget test alone takes 0.5 to 0.75 min on a good day but took 7 to 8 min on November 21. On November 27, 2026, a good day, every headless run under the memory load still outlasted the 60 s load, and that arm was void (below). The load can now last up to 120 s, and the pairs script asks for 120 s under a headless run. That 120 s is a default set in the code; the maintainer has not decided it, and the choice stays theirs (section 12). On a day like November 21 every loaded headless run would still outlast it, and dropping them favours the fast runs. The pairs script warns when `-Load` is used with `-Mode headless`.
- Rows checked: rows without `waits` whose median `runUiMs` under R is at least 20 ms (on the 500 library in 6e7e68c: the first switch, the stage rows, the open list, the search keystroke and the two report-only rows, plus the scroll row headless).
- The same in a real window: 6 R and 6 S, alternating, through the pairs script's `-Load` switch, which puts the load under the "after" runs only (run with the same build as before and after). Like every real-window sitting, this is run on the development PC by the maintainer or the coordinating session, never by automated agents or CI.

**Fixed before the first run.** The bounds below are frozen before any data is taken. A revised bound is tested only on a new set of runs, never on the runs that suggested it. The disturbed line may be re-set from the R runs, but only for later comparison sittings. It was re-set once, on November 27, 2026, by the maintainer's decision after the validation and the load series (section 2).

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

### The result of November 27, 2026

**What was run.** On the development PC, one Release build of master's measurement code (6e7e68c, as #417 records it), on the 500 test library, through the pairs script's `-Load` switch with the same build as before and after. Real window: 6 runs without the load (R) and 6 with the spin load (S), alternating. Headless: 4 R and 4 S, alternating; then R against M. The headless arms ran as two sittings of the pairs script, R against S and then R against M, each in alternating pairs, not in the order R S M M S R above.

**Rows checked:** the rows without `waits` whose median R `runUiMs` was at least 20 ms: 7 in the real window (the scroll row waits there) and 8 headless.

In the tables, figures are medians in ms, rounded to 0.1 ms with a half rounded up. Where a figure differs from #417 by 0.1 ms, the median ends in exactly 5 and #417 rounded it down; for the same reason the low end of the UI rise, the open list's 67.5%, is 68% here and 67% in #417. B is the pass bound: the larger of 10% of R's median Run UI and 3 ms. A rise is the median under S less the median under R. A row counts as disturbed when its UI rose by at least 3 B. It passes when its Run UI moved by at most B and by at most a quarter of its UI's rise.

**Real window** (6 R and 6 S runs)

| Row | UI, R | UI, S | Run UI, R | Run UI, S | B | UI rise | Run UI rise | Result |
|---|---:|---:|---:|---:|---:|---:|---:|---|
| `first-switch-published` | 218.2 | 392.9 | 206.4 | 383.7 | 20.6 | 174.7 | 177.3 | fails both bounds |
| `stage-all-to-submitted` | 106.5 | 197.4 | 105.5 | 196.0 | 10.6 | 90.9 | 90.5 | fails both bounds |
| `stage-back-to-all` | 86.5 | 103.2 | 73.5 | 97.7 | 7.4 | 16.7 | 24.2 | not disturbed (3 B is 22.1) |
| `open-list-stepped-closed` | 101.2 | 169.5 | 79.6 | 139.5 | 8.0 | 68.3 | 59.9 | fails both bounds |
| `search-keystroke` | 77.0 | 135.1 | 75.8 | 133.7 | 7.6 | 58.1 | 58.0 | fails both bounds |
| `search-keystroke-published-hidden` | 33.6 | 82.0 | 33.3 | 81.4 | 3.3 | 48.4 | 48.1 | fails both bounds |
| `switch-published-after-hidden-change` | 93.1 | 232.1 | 91.3 | 223.3 | 9.1 | 139.0 | 132.0 | fails both bounds |

**Headless** (4 R and 4 S runs)

| Row | UI, R | UI, S | Run UI, R | Run UI, S | B | UI rise | Run UI rise | Result |
|---|---:|---:|---:|---:|---:|---:|---:|---|
| `first-switch-published` | 376.6 | 642.3 | 367.3 | 625.2 | 36.7 | 265.7 | 258.0 | fails both bounds |
| `stage-all-to-submitted` | 212.9 | 296.8 | 208.1 | 288.3 | 20.8 | 83.9 | 80.2 | fails both bounds |
| `stage-back-to-all` | 154.8 | 242.5 | 136.3 | 211.1 | 13.6 | 87.8 | 74.8 | fails both bounds |
| `open-list-stepped-closed` | 118.5 | 183.9 | 108.0 | 176.0 | 10.8 | 65.5 | 68.1 | fails both bounds |
| `search-keystroke` | 144.8 | 223.6 | 131.9 | 214.7 | 13.2 | 78.8 | 82.8 | fails both bounds |
| `scroll-published-longest-step` | 298.5 | 518.8 | 279.6 | 488.7 | 28.0 | 220.3 | 209.2 | fails both bounds |
| `search-keystroke-published-hidden` | 82.2 | 177.8 | 79.6 | 173.0 | 8.0 | 95.6 | 93.4 | fails both bounds |
| `switch-published-after-hidden-change` | 200.0 | 329.6 | 194.0 | 317.0 | 19.4 | 129.6 | 123.1 | fails both bounds |

**What the frozen bounds gave.**

- Real window: the load disturbed 6 of the 7 checked rows. All 6 fail both bounds: their Run UI rose by 88 to 102% of their UI's rise, where the removal bound allows 25%. `stage-back-to-all` is reported as not disturbed, not as passed.
- Headless: the load disturbed all 8 checked rows. All 8 fail both bounds: their Run UI rose by 85 to 105% of their UI's rise.
- R against R (the R runs split into the first and the second half in time): the median Run UI moved by at most B on every checked row except `search-keystroke-published-hidden` in the real window (4.2 ms against a B of 3.3 ms), and `scroll-published-longest-step` (74.5 against 28.0 ms) and `search-keystroke-published-hidden` (9.0 against 8.0 ms) headless. Read row by row and on the absolute bound, as in #417, the experiment says nothing about those rows; the protocol's sentence does not say whether one failing row makes the whole experiment uninformative. The other disturbed rows, 5 in the real window and 6 headless, passed R against R and failed under S.
- The rate check passed in every run: 12 in the real window, 8 headless.
- The share of the rows' time the interface thread was not running (`notRunShare`) stayed small: per run, 4 to 8% without the load and 4 to 7% with it in the real window, 5 to 7% and 4 to 7% headless. The thread's own running time grew. What made it slower is not measured.
- The fast-core share, compared first as the protocol says: in the real window the median share was 1.00 with and without the load on every checked row, except `switch-published-after-hidden-change` under the load (0.83). Headless, R and S differed on 7 of the 8 rows.
- Other programs, the added load included, kept 7.0 to 9.7 of the 12 processors busy under S, and 1.0 to 2.8 under R. No run counted as disturbed under the line then in force.

**Conclusion, in the method's terms: Run UI failed.** S fails. In the real window four disturbed rows that passed R against R had the same fast-core share with and without the load (the first switch, Stage to Submitted, the open list and the search keystroke), and all four fail both bounds. So, by the rule above, Run UI is **not trusted even as a diagnosis on this PC**. It stays in the JSON, labelled "not validated" (section 2), and the report's table and the pairs summary label it so too. Headless the share differed on most rows, so the headless failure alone would support no claim; it points the same way. No S run failed the rate check.

**The memory arm is void.** Headless, all 6 runs under the memory load outlasted the 60 s load (the pairs script recorded 65.8 to 67.7 s from the start of the load to the end of each run), and the protocol does not allow a run that outlasts its load. Its figures are not counted. The load now lasts up to 120 s under a headless run (above), so the arm can be run again on a good day.

**Recorded without explanation.** Headless, the interface thread ran mostly on the slower kind of core: without the load, the median fast-core share of the checked rows was 0.00 to 0.33. In the real window it was 1.00 on every checked row. The two modes also read the same row very differently: without the load, `search-keystroke-published-hidden` had a median UI of 82.2 ms headless and 33.6 ms in the real window, the same build on the same day.

The table by load band over all R runs and recorded pairs, asked for above ("The real load"), has not been made.

### The load series of November 27, 2026

**What it was.** A real window on the development PC, the same build as the validation, the 500 test library: 18 runs between 13:32 and 13:37, with no load threads and with one to five spinning load threads, three runs each, the order of the thread counts reversed between repetitions. The load was a scratch copy of the load script with the number of threads as a setting; the repository's script takes `-Threads` since then.

**By what each run recorded** (how many of the 12 processors other programs kept busy, the load threads included; medians of UI in ms)

| Other programs kept busy | Runs | `first-switch-published` | `stage-all-to-submitted` | `open-list-stepped-closed` | `search-keystroke` |
|---|---:|---:|---:|---:|---:|
| under 3 | 1 | 190.3 | 103.2 | 97.0 | 62.7 |
| 3 to under 5 | 5 | 256.1 | 143.5 | 105.5 | 90.0 |
| 5 to under 7 | 5 | 388.4 | 215.1 | 187.3 | 130.4 |
| 7 and more | 7 | 470.2 | 223.5 | 183.9 | 150.4 |

**What it showed.** Timings rose with the load from the first extra thread: there was no range in which extra load cost nothing. Against the runs with no load threads, one load thread raised the medians of these four rows by 13 to 38% (three runs each). No run counted as disturbed under the line then in force. The fast-core share of the first switch was 1.00 in 17 of the 18 runs. Three runs for each thread count is thin, and the PC's own load moved during the series: the runs without load threads recorded 1.7, 3.3 and 4.5 processors kept busy by other programs. So the table by what each run recorded says more than one by thread count. The maintainer set the disturbed line from it (section 2).

## 7 The verdict rule (in force from November 27, 2026)

For "the real window decides". It was proposed to the maintainer with this method, and the maintainer accepted it as proposed on November 27, 2026. It is in force from that day:

- **Figure:** UI, as today. A budget is about what a person feels, busy PC or not.
- **Runs:** 5 real-window runs that are not `disturbed` (a `null` counts as not disturbed and is printed "load unknown"), from at most 10 tries in one sitting (Release, the 500 test library). Verdict: the median UI of those 5 against the budget.
- **Too busy to judge:** fewer than 5 undisturbed runs out of 10. The report says so and lists each run's `othersBusyProcessors` and `notRunShare`.
- **A report-only row** gets no budget verdict, the primary row of a comparison included: it is compared only between builds (section 5).
- **Beside it, report-only:** the median `runUiMs`, labelled "not validated" (it failed the validation, section 6), `perfCoreShare`, `gcPauseMs` and `jitMs`. Never a lower bound or a low quantile. **Run UI never counts toward a verdict.**
- **Headless in CI** keeps today's single run, multipliers and rule as the guard.

The pairs script prints these lines under "Verdict on the budgets (method, section 7, in force since November 27, 2026)". Before that day it printed them under "proposed verdict (report-only)", and they decided nothing.

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
- `-Out <new folder>`, `-Pairs`, `-PrimaryRow <row id>`, `-ExpectedToRise <row ids>`, `-BeforeName`, `-AfterName`, `-Load None|Spin|Memory`. The load lasts 60 s under a real-window run and 120 s under a headless run; the sitting's record keeps the length, and `-LoadSecondsFor window|headless` prints it without running anything.
- `-BeforeIdle` and `-AfterIdle`, given together: each build's side of an idle-preparation comparison (section 13). The two sides must belong to the same scenario; the scaling scenario is refused in a window.
- `-RunLimitMinutes`: a run still going after this long is stopped and its pair replaced ("timed out"). By default 15 min headless and 3 min in a window, far above any budget, so the limit is not a timing.
- `-Pool <earlier sittings' folders>` counts earlier sittings of the same change, mode and primary row together with this one. A void sitting is listed but adds no pairs. A sitting given twice, or a copy of one, is refused. Each sitting's runs keep the disturbed flag their build wrote, so a pool that spans November 27, 2026 mixes the two lines of section 2.
- `-Analyze <a sitting's folder>` reads a sitting again without running anything, for a later re-analysis or a check.

The script runs pairs until it has kept `-Pairs` of them. Once a pair's first run is dropped, its second run is not spent: the pair is replaced whatever it would give. A sitting stopped by hand is judged "not decided" (section 5, step 3); if it stopped between the two runs of its last pair, that pair is "not finished", not replaced.

Each sitting's folder holds `sitting.json` (the runs, in order, with their exit codes), one JSON file per run, each run's output, and `summary.md`. The JSON files name the computer and the runs' output may hold folder paths, so the sitting's folder stays on the PC that ran it; only `summary.md` is shared. The summary names no computer and no folder.

`ManualCertification/Test-BoardPairs.ps1` checks the rule on canned sittings and needs no build: the counts against the table above, 9 of 10 (shown), 8 of 10 (not shown, with the P of the count seen printed apart from the P of the count needed), a disturbed pair that is replaced (with and without its second run), a tie, absent figures, the size floor (also one between the rounded and the true floor), Run UI pointing the other way (it decides nothing), a watch row higher once and again, a void sitting, two and three sittings pooled, a sitting given twice or copied (refused), a sitting stopped short and one stopped inside its last pair (not decided), a missing row, a null UI figure and a run that timed out (replaced), a run that outlasted its load (listed apart), and five pairs (never decides). Since November 27, 2026 it also checks the verdict heading of section 7 (no line calls the rule proposed), a report-only primary row (no budget verdict), the label "not validated" on every line that names Run UI, the load's length by mode, and the load script's limits (`-Seconds` up to 120, `-Threads` 1 to 6 and 6 unless set), read without starting it. For idle sides it also checks a sitting of the search scenario (its refresh rows reported apart, never blocking), and the refusal of two sides of different scenarios and of the scaling scenario in a window. The trial's Windows CI job runs it.

## 10 What one run and one comparison cost

- One real-window run: about 0.3 min. The new readings add about 0.2 s, and up to about 1.5 s while `GetSystemTimes` is slow under load.
- One headless run of the budget test alone: about 0.5 to 0.75 min (the test method itself took 15.4 s in one recorded run; process start and discovery are assumed to add 15 to 30 s). On November 21, with other programs keeping 10.7 to 11.4 of 12 processors busy, two such runs through the pairs script took 7.2 and 7.9 min. On November 27, in the validation, the pairs script recorded 21.3 to 27.7 s for a headless run without the load and 37.2 to 41.1 s with the spin load, the load's start included; a real-window run took 6.7 to 8.5 s without it and 14.1 to 17.7 s with it.
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

**Measured on the development PC on November 27, 2026** (section 6): the validation, in which Run UI failed, and the load series. Under the spin load the time not running stayed small (`notRunShare` 4 to 7% per run, against 4 to 8% without it) while the interface thread's own running time grew; what made it slower is not measured.

**Shown by CI** (run 45003699839 of 2026-11-22, on the pull request that added this document; each runner has 4 processors):

- **Ubuntu:** `threadClock` is `thread-cputime`, in the headless test and in the real window under a virtual display. The load was read: other programs kept 0.04 (headless) and 0.23 (window) of the 4 processors busy, and `stealProcessors` read 0. So `clock_gettime` through `libc.so.6`, `/proc/stat` and `sysconf` work on the runner.
- **Windows:** `threadClock` is `cycles` at 2,596,000 cycles per ms, so the registry's `~MHz` value exists on the runner. The two rate checks read 0.9981 and 0.9980 of nominal, each after 25 spins, and passed.
- `perfCoreShare` is `null` on both runners.
- One observation from one run on each system, not a finding: headless, on the 500 library, the first switch to Published ran for 395.1 of its 401.9 ms on Ubuntu, with a garbage-collection pause of 80.2 ms inside the running time, and for 91.4 of its 125.6 ms on Windows, with a pause of 34.4 ms. How a pause shows in the thread's own clock on each system has not been looked into.

**Assumed:**

- That removing time off the processor is most of what a real busy day adds. One synthetic experiment suggested it. The validation of November 27, 2026 did not bear it out for the spin load: the time not running stayed small and Run UI rose almost as much as UI (section 6). Whether it holds for a real busy day is not known.
- That `sched_getcpu` and `cpu_capacity` give a core-class map on a Linux machine with two kinds of core. The CI runner gave none, as expected of a virtual machine.
- That the thread-cycle counter runs at the nominal rate on other Windows PCs. The per-run check catches a slower counter, and a faster one as soon as one spin runs without being taken off the processor (section 4); a PC so busy that no spin of 25 runs whole could still let a faster counter through.
- That `Environment.ProcessorCount` is the machine's processor count. The load readings count every processor, while `ProcessorCount` follows an affinity or container limit; under such a limit a run is marked disturbed too often. On the development PC and the GitHub runners the two are equal.
- That more than 3 of the 12 processors busy with other programs is a useful disturbed line. The line is the maintainer's decision of November 27, 2026, in place of the starting value "fewer than two processors free"; the load series found no load below which timings stop rising (section 6).
- That the same line scales as a quarter of the processors on a PC with another number of them (1 of 4 on a CI runner). The code writes it so; the decision named only 3 of 12, so the scaling is assumed, not decided.
- The run and build times of section 10, except the recorded ones.
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

## 12 Defaults the maintainer can change, and what was decided

These follow the method's own recommendations and were taken as defaults while the maintainer was away. Three of them (1, 2 and 4) were settled on November 27, 2026:

1. **Run UI counts toward no verdict and no merge.** It was a default because Run UI was meant as a diagnosis. Since November 27, 2026 it follows from the validation: Run UI failed it and is not trusted even as a diagnosis on the development PC (section 6).
2. **A run is disturbed** when other programs keep more than 3 of the 12 processors busy on average. Decided by the maintainer on November 27, 2026, as the one re-set the method allowed, for later sittings only. The code writes it as a quarter of the processors, so it reads 1 of 4 on a CI runner; that scaling to other processor counts was not part of the decision (section 11, "Assumed"). The starting value was "fewer than two processors free" (section 2).
3. **When Published is prepared while the Board is idle**, the switch after it gets a new id (`first-switch-published-after-idle`), so today's `first-switch-published` keeps its meaning. The maintainer chose this on November 27, 2026; it is built (section 13).
4. **The verdict rule of section 7** was a proposal, printed under "proposed verdict (report-only)". The maintainer accepted it as proposed on November 27, 2026; it is in force from that day.

Open points, left to the maintainer (where the code holds a default for one, the point says so):

- **Pooling past two sittings.** Three sittings of 10 pairs, or two of 20, take the chance of a false pass past 5% (section 5). Options: cap pooling at two sittings of 10 pairs; decide only at a total number of pairs fixed in advance; or use a stricter count for pooled looks. Until then the summary prints the figures.
- **The load and long headless runs.** A loaded headless run that outlasts its load is dropped. Since November 27, 2026 the pairs script asks for a load of 120 s under a headless run. That is a default set in the code, not a decision: none of the decisions of November 27 covers the load's length, and it awaits the maintainer's. Every headless run of November 27 took at most 67.7 s from the start of the load to its own end; on a busy day (7 to 8 min a run) every loaded headless run would still outlast it (section 6).
- **The slow-step limit in a measurement run.** A measurement run keeps the 120 ms limit of idle preparation. In the one headless sitting run so far, on the development PC on November 27, 2026, every run of the side `prepared` on the 500 library stopped before Published was prepared and did not time the first switch after idle (section 13); that is an observation of one sitting, not a rule. Raising the limit for measurement runs would change what is measured. Only a side that runs steps can stop (the side `none` runs none), so only that side's runs are dropped, and the kept pairs favour the faster moments of the PC on that side.

## 13 Preparing Published while the Board is idle

The Board can prepare the hidden Published shelf in a few short steps while nobody touches it, so that its first switch makes no card view (the maintainer's decision of November 21, 2026). Preparation is off unless a test or a run turns it on; the trial app does not turn it on yet. These rows show what it does to the first switch and what each step costs.

**How a run is told its side.** A run is one side of a with-and-without comparison, or neither:

- In a real window: `PaperRoute.Desktop.exe --snapshot-from <library> --measure <file>.json --after-idle <side>`. `--after-idle` is accepted only with `--measure`.
- Headless: the budget test reads the variable `PAPERROUTE_TRIAL_AFTER_IDLE`.
- The side is one of `none` (no step), `width` (the width step only), `half` (the width step and the first two layout steps) or `prepared` (every step, until the Board says Published is prepared), or a side of one of the other scenarios below.
- With no side, the run is exactly as before: the same rows in the same order, `first-switch-published` among them, and no `idlePreparation` in the JSON.

**The rows of a run told a side.** They take the place of `first-switch-published` and its paint, after the startup rows, so every row after them keeps its work and its order, and the last two rows stay last. A first switch happens once per window, so a run has either these rows or `first-switch-published`, never both. Each is report-only, and none can fail a test by its time.

| Row | What it times | Budget, for scale |
|---|---|---|
| `prepare-published-idle` | the preparation from its start to done, a frame after each step; Wall and Ran are its cost, UI the longest wait a key would meet | filter keystroke, 50 |
| `prepare-published-longest-step` | its longest step: Wall the longest step's, UI and every busy-PC figure the longest-UI step's. Its note lists each step: W (width), R (rows) or L (layout, with its height cap on the ladder), Wall, UI and card views made. This is the row to judge in a real window | UI thread, 250 |
| `first-switch-published-after-idle` | the first switch after the wait below; its note gives the steps before it, the card views ready in the hidden shelf and the card views made at the switch | page switch, 100 |
| `first-switch-published-after-idle-paint` | its frame | UI thread, 250 |

The measurement drives the preparation itself, step by step, with the Board's idle timer off, so no step can run inside another row; the warm-startup row's note says how many ran inside it (none). Then every side waits the same way: preparation done (at once on the side `none`), a fixed idle period of 1 s, one frame. Only then is the switch timed. On the side `none` the two preparation rows read about 0, with 0 steps.

When preparation stops (a step that took, or is predicted to take, more than 120 ms, or an accessibility client that has read the Board's shelves, that is the shelf host's children), the switch is not the one the row is about. It is made untimed, so the rows after it do the same work, and the two switch rows are not written. A client that has read only the Board above the shelves (its header, tabs or search box) does not stop preparation: no peer exists for Published's Border then, and a step raises no automation event for it (a test listens on every peer such a client holds). The JSON's `idlePreparation` gives, per library, the side, the steps run, the card views they made, whether Published was prepared at the switch, why preparation stopped (`skippedReason`, or `null`), whether the switch was timed, the card views it made (on `mid-step` the switch's own, those made in its row less those of the step it waited for), and what the wait did: the idle period waited (`idleWaitMs`) and the frames drawn from the start of the idle period to the switch (`framesBeforeSwitch`: 1, or 2 on the maximise and scaling scenarios, whose change and its frame come after the idle period).

**Running a comparison.** The same build on both sides, so the pairs compare the preparation and nothing else; the wait before the switch is the same on both sides, so warm-up during the wait falls on both alike:

```
pwsh -NoProfile -File ./ManualCertification/Measure-BoardPairs.ps1 -Mode window -Before <exe> -After <same exe> -Library <test library> -BeforeIdle none -AfterIdle prepared -BeforeName without -AfterName with -Out <new folder> -Pairs 10 -PrimaryRow first-switch-published-after-idle
pwsh -NoProfile -File ./ManualCertification/Measure-BoardPairs.ps1 -Mode headless -Before <tree> -After <same tree> -BeforeIdle none -AfterIdle prepared -BeforeName without -AfterName with -Out <new folder> -Pairs 10 -PrimaryRow first-switch-published-after-idle
```

The primary row is `first-switch-published-after-idle`, expected lower on the side `prepared`. The two preparation rows before it are not watch rows: they time the work preparation moves out of the switch, so they read about 0 on the side `none` and more on `prepared` by design. Whenever idle sides are given the script reports them apart and they never block; the longest step is judged on its own, in a real window, against the UI thread budget. The rows after the primary row are watch rows. A run whose preparation stopped lacks the primary row: it is dropped and its pair replaced (section 5, step 3), and the summary lists these drops apart, because whether a step is slow is a timing and slower runs are dropped more often. The script refuses two equal sides, drops a run whose JSON names another idle side than its build was given (or has one where none was given), and pooling refuses a sitting of other sides.

**Compared only between the two sides of a sitting.** These rows are never compared with `first-switch-published`, with a budget row, with another sitting or with another day. Only the pairs inside one sitting are.

**Counts, which do not depend on the load** (headless, the development PC, November 27, 2026). With the preparer's clock standing still, so that no step reads as slow, the 500 library's first switch after idle made 21 card views on the side `none` and 0 on the side `prepared`, where the five steps had made those 21. With the real clock, headless, preparation on the 500 library stopped as a slow step after the width step and two layout steps (6 card views made) in every run of one sitting, while other programs kept under 2 of 12 processors busy. Headless card views cost several times what they cost in a real window, so this says nothing yet about a real window; until a real-window sitting has run, the headless sitting on the 500 library is void on the side `prepared`. Whether a measurement run may raise the 120 ms limit is left to the maintainer: it would change what is measured.

### The other scenarios

The idle design's section 8 lists more rows than the first switch. A first switch happens once per window, so every row that needs one of its own has its own **scenario**, and each scenario has its own sides, told the same way (`--after-idle <side>` in a real window, `PAPERROUTE_TRIAL_AFTER_IDLE` headless). Every side of a scenario writes the same rows, so the pairs script can pair them, and the pairs script refuses two sides of different scenarios. The scenario's rows sit between the two preparation rows and `first-switch-published-after-idle`, so the rows after the switch keep their work and their order in every run; after the switch the scenario's change is undone, untimed (the search cleared, the window restored, the scaling and the theme put back). The four sides above form the scenario `switch`, and their rows are exactly as before. A run told no side is master's run, with master's rows in master's order.

Every row below is report-only: none can fail a test by its time, and none gets a budget verdict. **They are compared only between the two sides of one sitting**, never with a budget row, with `first-switch-published`, with another sitting or with another day. The design's rule for shipping reads them so: the first switch after preparation against the one without it, the longest step against 250 ms in a real window, and the other rows for harm beyond what the design's section 14 accepts.

**Which run produces which rows.** Each run also writes the two preparation rows first and, unless preparation stopped, `first-switch-published-after-idle` and its paint last.

| Scenario | Sides (without, with) | The run | Rows it adds, in order | Design row |
|---|---|---|---|---|
| `switch` | `none`; `width`, `half`, `prepared`, `mid-step` | window or headless | none; on `mid-step` the last step runs inside the first switch's row | 1 to 4, 11 |
| `key` | `key-none`, `key-step` | window or headless | `search-keystroke-step-due` | 6 (a key that meets a step) |
| `timer` | `timer-none`, `timer-on` | window or headless | `search-keystroke-after-pause`, `refresh-published-timer` | 5 (time to prepared, steps per session, the stop), 6 (a key after a pause) |
| `search` | `search-pending`, `search-refreshed` (both prepare) | window or headless; for row 10 a window on a library of several thousand | `search-keystroke-published-prepared`, `refresh-published-after-search`, `search-cleared-published-prepared`, `refresh-published-after-clear` | 8, 10 |
| `maximise` | `maximise-none`, `maximise-prepared` | window or headless (headless: a resize, below) | `maximise-window` | 12 (a maximise) |
| `scaling` | `scaling-none`, `scaling-prepared` | headless only | `scaling-change` | 12 (a scaling change) |
| `theme` | `theme-none`, `theme-prepared` | window or headless | `theme-change`, `refresh-published-after-theme` | 9 (memory, a theme change) |

What each side does before the first switch:

- `mid-step`: the width step and the three capped layout steps, the idle period and its frame, then the switch is asked for as the last, full-size step starts: the row times that step, which the switch waits for, and the switch. Compare with `none`.
- `key-step`: the same four steps; then the keystroke row times the last step and, after it, the search keystroke (the search row's work). On `key-none` no step is due and the row is the keystroke alone.
- `timer-on`: the Board's own idle timer is turned on just after the first frame and prepares Published by itself; `prepare-published-idle` times from that moment to "prepared" (its 0.4 s of quiet, its ticks and the frame after each step included), and its note gives the steps the timer ran and whether the slow-step stop fired. `prepare-published-longest-step` gives the longest of those steps by the preparer's own clock; each is one job on the interface thread, so its UI is its Wall, and it has no busy-PC figures. Then the search's first letters are typed, a pause of 0.5 s, and the last letter: `search-keystroke-after-pause`, whose note gives the steps that ran in the pause and the key (the design expects none: a refresh waits for 2 s after a render). Then `refresh-published-timer` times the timer's own refresh, from the key's frame to Published prepared again. The timer is turned off before the idle period. On `timer-none` the timer stays off and both refresh and preparation rows read about 0.
- `search-pending` and `search-refreshed`: both prepare every step. The search is typed while Published is hidden (`search-keystroke-published-prepared`; on the 500 library it leaves about a fifth of Published), the refresh after it, the search cleared (most of Published back), the refresh after that, and the search typed again with its refresh, untimed. The refreshes run only on `search-refreshed` (one row step and one full-size layout each); on `search-pending` the refresh rows read about 0 and the switch finds the rows stale. The first switch then shows the search's cards.
- `maximise-prepared` and `maximise-none`: after the idle period and its frame, the window is maximised (`maximise-window`, until the shelf host has its new size), one frame, and the switch at once: within 0.4 s of the maximise and before any refresh, since the measurement drives no step there.
- `scaling-prepared` and `scaling-none`: the same with the display scaling changed to 1.25 (`scaling-change`).
- `theme-prepared` and `theme-none`: the managed heap is read after a full collection before preparation and after it (only this scenario takes a collection, since it changes what the rows after it meet), then the theme is changed to the other one (`theme-change`) and the refresh after it runs (one full-size layout on `theme-prepared`, nothing on `theme-none`). The switch is made in the changed theme; the theme is put back after it.

| Row | What it times | Budget, for scale |
|---|---|---|
| `search-keystroke-step-due` | the last step, if one is due, then the search keystroke (`teaching` typed, the render, layout) | filter keystroke, 50 |
| `search-keystroke-after-pause` | the last letter of the search, typed 0.5 s after the others: its render and layout | filter keystroke, 50 |
| `refresh-published-timer` | from the key's frame to Published prepared again by the idle timer (waits by design) | UI thread, 250 |
| `search-keystroke-published-prepared` | the search typed while Published is prepared and hidden | filter keystroke, 50 |
| `refresh-published-after-search`, `refresh-published-after-clear` | the idle refresh after the search, and after it is cleared: each step and the frame after it; the note lists each step (R the rows, L a layout) with its Wall, UI, card views and frame | UI thread, 250 |
| `search-cleared-published-prepared` | the search cleared while Published is hidden | filter keystroke, 50 |
| `maximise-window` | the maximise, until the shelf host has its new size, and layout (waits by design) | UI thread, 250 |
| `scaling-change` | the scaling change and layout | UI thread, 250 |
| `theme-change` | the theme change: restyling and layout | UI thread, 250 |
| `refresh-published-after-theme` | the idle refresh after the theme change | UI thread, 250 |

The pairs script reports the refresh rows apart with the two preparation rows: they time work the idle refresh moves out of the switch, so they read about 0 on a side that does not refresh, by design, and never block. Every other row of a scenario is a watch row. Where the side with preparation is expected to cost more (the keystroke that meets a step, the switch on `mid-step`), name as `-AfterName` the side expected lower, so the count and the median difference give the size of the harm.

**What the record adds.** Each run's `idlePreparation` record also gives, where its scenario has them (otherwise `null` or empty): `scenario`; `sessionSteps`, every step the session ran up to and with the switch; `stepsMet` and `stepsMetViews`, the steps run inside the row a key or a click meets and the card views they made (`mid-step`, `key`, and on `timer` the steps in the pause and the key), on `mid-step` and `key` counted inside the row's own stopwatch, so a step run before the row is not counted as met; `viewsMadeInRow`, every card view made inside that row (`mid-step` and `key`); `refreshSteps`; `rowsStaleAtSwitch`; `managedBytesBefore` and `managedBytesAfter` (`theme`); `stepFrameMs`, the frame drawn after each step the measurement drove (row 13; empty on `timer-on`, whose steps the timer runs); and `changeToSwitchMs`, from the start of a maximise or a scaling change to the start of the switch. Every side's longest-step note also gives the frame after each step.

**Running a comparison of a scenario**, for example:

```
pwsh -NoProfile -File ./ManualCertification/Measure-BoardPairs.ps1 -Mode window -Before <exe> -After <same exe> -Library <test library> -BeforeIdle search-pending -AfterIdle search-refreshed -BeforeName pending -AfterName refreshed -Out <new folder> -Pairs 10 -PrimaryRow first-switch-published-after-idle
pwsh -NoProfile -File ./ManualCertification/Measure-BoardPairs.ps1 -Mode window -Before <exe> -After <same exe> -Library <test library> -BeforeIdle key-step -AfterIdle key-none -BeforeName step -AfterName none -Out <new folder> -Pairs 10 -PrimaryRow search-keystroke-step-due
```

For row 10, `BoardBudgetTests.WriteSeveralThousandLibraryForMeasurement` (by hand, `TestCategory` Manual) writes a library of 3,000 into `TestResults\v11-trial\large-3000`; the search scenario is then run in a window with `--snapshot-from` that folder.

**Not as the design states it, and why.**

- A maximise headless is a resize to 1,920 x 1,040: the headless platform has no screen to maximise to. A real window is maximised (`WindowState.Maximized`) and restored. The run fails rather than time nothing if the shelf host's size did not change (a window maximised already).
- A scaling change can be made only headless (the headless platform's `SetRenderScaling`). A program cannot change its own display scaling in a real window: that needs the person to change the display's scaling, or to move the window to a screen with another scaling, while Published is prepared, and then switch; `--after-idle` refuses the scaling sides.
- The keys of the `key` and `timer` scenarios are the search rows' own work (the search text set, the render its wait ends in), as every search row of the method; no key press reaches the window, so the idle timer's 0.4 s of quiet after a key is not met by them, only its 2 s after a render. A real key press needs a real-window driver the trial does not have.
- On `timer-on` the timer is turned on just after the first frame, so its 0.4 s of quiet counts from then; the app would turn it on with the window and count from its last render, which is a little earlier. The design's dedicated window "runs no other row"; here the timer is turned off before the idle period, so no tick can land in a later row, and the run goes on with every row, so its sides pair with the others.
- Whether normal use on the development PC skips preparation for an accessibility client (row 13's check by hand) needs the app to turn preparation on, which comes after the real-window sittings.
- In CI the timer scenario cannot hold the preparer's clock still, since its idle rules read that clock: it runs on the real clock with the slow-step limit raised, so the stop cannot fire and only the counts are held. A measurement run keeps the 120 ms limit, so headless on the development PC preparation will often stop, as on the side `prepared`.

**Load-free checks** (in CI, with the preparer's clock standing still, but on `timer-on` as just said): `BoardBudgetTests.TheScenarioRowsAreMeasuredForEachSide` runs every new side once, on the 500 library where it prepares and the example where it does not, and holds where the rows sit, that the sides of a scenario wait alike, that every step of the session is counted once, and what each scenario did: on `mid-step` and `key-step` the last step met inside the row, counted inside the row's own stopwatch, with its card views among those made in the row (on `mid-step` every card view made in the row was the step's, so the switch after it made none), no step in the pause before the key on `timer-on` and a refresh of one row step and one layout, the refreshes of `search-refreshed` (one row step and one layout each) and Published's rows current at its switch but stale on `search-pending`, no step between a maximise or a scaling change and the switch, and on `theme-prepared` one refresh step and the heap read. `HiddenShelfPreparationTests.R10_ARefreshOnALibraryOfSeveralThousandIsOneRowStepAndOneLayout` holds row 10's counts on a library of 3,000: after the search and after it is cleared, one row step brings every row to the cards and one full-size layout follows, and the switch after the last refresh makes no card view and refills no row. `MeasureModeTests` holds the sides, their scenarios and the record's JSON, `ThePairsScriptKnowsEverySideAndItsScenario` that the pairs script knows the same sides and scenarios.

**Counts, which do not depend on the load** (those checks, headless, the development PC, November 27, 2026; no time here is a finding). On the 500 library: `mid-step` ran 4 steps that made 12 card views, the last step inside the switch's row made 9, and the switch itself none; on `key-step` the step the key met made 9. On `timer-on` the idle timer ran 5 steps (21 card views), no step ran in the pause or the key, its refresh was one row step and one layout, the switch made none, and the session ran 7 steps. On `search-refreshed` the three refreshes ran 6 steps, Published's rows were current at the switch and it made no card view; on `search-pending` its rows were stale at the switch, which made no new card view but gave the views it held other cards. On `maximise-prepared` (a resize to 1,920 x 1,040 headless) the switch made 14 card views; on `scaling-prepared` none; on `theme-prepared` the refresh was one layout and the switch made none. On the library of 3,000, Published held 1,631 cards and the search left 287; each of the two refreshes was one row step, which refilled, added or removed 544 rows, and one layout, and they made no card view; the switch after them made none and refilled no row.
