# Board trial: timing on a busy PC

How the Avalonia Board trial times its Board, what it records about how busy the PC was, and how two builds are compared. It answers the maintainer's note of November 21, 2026 on the two rows still over their budget: "Remember that these tests are on a very, very busy PC. Perhaps we can account or correct for that?"

**Every figure this method adds is report-only.** Wall, UI, the budgets, the multipliers (Debug x2, GitHub Actions x3), the verdicts and the headless test's rule are what they were before. Run UI is a diagnosis, not a verdict. The verdict rule of section 7 is a proposal and changes nothing until the maintainer accepts it.

Sections 1 to 8 keep the numbers that the code's comments cite as "busy-PC design, section N".

## What is built, and what is not yet

Built (the pull request that adds this document):

- The thread clock, the new per-row figures, the run's `conditions`, the failure message (sections 1 to 4 and 8). `PaperRoute.Desktop/Measurement/ThreadClock.cs`, `RunConditions.cs`, `BoardMeasurement.cs`, `WindowMeasurement.cs`, and `PaperRoute.ViewModels/Measurement/BoardMeasurementReport.cs`.
- The quiet-PC line at the end of the Windows Forms timing tests' failure messages (`PaperRoute.Tests/ResponsivenessBudgetTests.vb`).
- `ManualCertification/Measure-BoardPairs.ps1`, the comparison of section 5 and the proposed verdict lines of section 7, with its check on canned measurements, `ManualCertification/Test-BoardPairs.ps1`, which the trial's Windows CI job runs.
- `ManualCertification/Start-BoardTrialLoad.ps1`, the known load of section 6.

Not built yet:

- The validation of section 6 has **not been run**. Until it has, Run UI is not validated on any PC.
- The timed row for #404's search with Published hidden (`search-keystroke-published-hidden`) and its pairs.
- The rows for preparing Published while the Board is idle.
- Nothing has run on Linux yet. The Ubuntu CI job is the first place that shows whether the Linux thread clock and load readings work.

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
| `machineBusyProcessors` | processors | change in busy time of all processors / elapsed time. Windows: `GetSystemTimes` (kernel + user - idle). Linux: the first line of `/proc/stat`, busy = user + nice + system + irq + softirq. Read on a thread-pool thread before the first row and after the last, each reading timed at its midpoint | the read failed |
| `loadReadMs` | [before, after] | how long each of the two load readings took | as above |
| `stealProcessors` | processors | Linux only: change in `steal` / elapsed time | Windows; the read failed |
| `othersBusyProcessors` | processors | `machineBusyProcessors` less this process's own processor time / elapsed time, never below 0 | as `machineBusyProcessors` |
| `notRunShare` | 0 to 1 | the sum of `notRunMs` / the sum of `wallMs`, over the rows without `waits` | as `runMs` |
| `disturbed` | true/false | `othersBusyProcessors > processors - 2`: other programs left fewer than two processors free, on average over the run | `null` when `othersBusyProcessors` is absent. `null` counts as not disturbed: the run is kept and printed as "load unknown" |

`machineBusyProcessors` can read slightly above the number of processors, because the counters move in steps. It is not clamped.

**Why `disturbed` uses other programs' load and not `notRunShare`.** A build can change its own share of time not running (a build that waits more, for example). If pairs were replaced on that figure, they would be replaced more often for one build than the other, and the comparison would be biased. Other programs' load depends mostly not on the build, because the process's own processor time is subtracted. It is not exact: in a real window, the window manager's and graphics driver's work for the trial's frames counts as "other programs", and headless the test runner's own processes do too. `notRunShare` is still recorded and printed, so a reader can see a disturbed row or run by eye.

Nothing new names the PC, the user, a folder or another program. The JSON's existing `machineName` field stays as it is; the pairs summary never copies it.

## 3 How each row is timed

- **Unchanged.** Every row is timed once per process, in a fixed order, with the same actions headless and in a real window. Wall, UI and the verdict are computed as before.
- **Added inside the timed step.** The thread clock and the two counters (garbage collection, compilation) are read just after the stopwatch starts and just before it stops. Each tick of the UI probe reads the thread clock once (about 1 microsecond on Windows) and the processor number. For the scroll row, every new figure, `notRunMs` included, comes from the step with the longest UI, and `notRunMs` is computed from that step's own Wall, not from the row's `wallMs` (which is the longest Wall of any step).
- **Untimed, once per run.** The rate check before the first row and after the last (usually a few ms, at most 200 spins of 2 ms). The two machine-load readings, on a thread-pool thread. A `GetSystemTimes` call has been seen to block for up to 727 ms on the development PC under full load, so it is never called on the interface thread and never between timed rows. The stopwatch is read before and after each call; the midpoint is the reading's time, and the call's length goes into `loadReadMs`.
- **No repetition inside a process.** The first switch to Published, warm startup and the first keystroke happen once per process. A repeat would run warm and time a cheaper action than the one the budget is about. Rows are repeated only by running more processes: alternating pairs to compare (section 5), five runs for a verdict (section 7).
- **No quiet gate.** On a very busy PC a gate may never open: one experiment saw the PC 100% busy with 63 to 170 threads waiting. The pairs script does not wait for quiet. It records the load and replaces disturbed pairs afterwards.
- **Priority, affinity and power settings are never touched**, in measurement or in validation.

## 4 The corrected figure

**Definition.** `runUiMs` = the largest, over the row's probe gaps, of (thread clock at the end of the gap - thread clock at its start). The tail from the last tick to the end of the row counts as a gap. Because it is measured gap by gap, it stays right where UI is below Wall. That happens headless too: on an Ubuntu CI run the open list read Wall 104.6 and UI 66.4 ms.

**The thread clock.**

- Linux: `clock_gettime(CLOCK_THREAD_CPUTIME_ID, ...)` through P/Invoke into `libc.so.6`, checked with `NativeLibrary.TryLoad` first. If it does not load, the figures are `null`. No conversion is needed.
- Windows: `QueryThreadCycleTime` on the current thread, divided by the **nominal** rate, the registry's `~MHz` value for processor 0 times 1000. Microsoft says not to turn these cycles into time in general, so every run checks that the counter runs at that rate: spins of 2 ms, untimed, until one spin's cycles per stopwatch millisecond come within 2% of nominal, at most 200 spins. Being taken off the processor can only lower a spin's ratio, so one close spin is enough. The check runs before the first row and again after the last. If either fails, every thread-clock figure of the run is `null` and `threadClock` says `absent: rate check failed`. The largest ratio is not used as the rate: it sits at or just below the true rate and so would overstate running time.
- Not used: `GetThreadTimes`. On the development PC it moves only in steps of 15.6 ms.

**Arithmetic.** `runMs` = clock after - clock before. `notRunMs` = `wallMs - runMs`, recorded raw. A gap's running time is at most the gap, so `runMs <= wallMs` and `runUiMs <= uiMs` should hold up to the clock's error. The headless budget test checks them with a relative margin: `runMs <= 1.02 x wallMs + 1` and `runUiMs <= 1.02 x uiMs + 1` (the 2% matches the rate check), and writes each comparison to the test log. Load only takes time away from the thread, so it cannot push a figure past these bounds. Nothing is clamped. Garbage-collection and compilation time are shown beside the figure and never subtracted: when they happen inside a row, they are part of what that row costs.

**What it cannot remove.** Slower running: a busy twin on the same core, an efficiency core instead of a performance core, a lower clock when the chip is busy, cold caches, memory bandwidth taken by other programs. Also the Board's own garbage collections and first-use compilation, which `gcPauseMs` and `jitMs` show.

**What it leaves out that is real.** Waits that belong to the action: a garbage-collection suspension started by another thread, hard page faults, locks, waits on the render thread. These fall into "not running", so `runUiMs` can read below the action's undisturbed cost. On CI virtual machines, time the host takes may count as running (assumed). Rows with `waits` keep the figure, but it cannot separate waiting by design from load, so they are left out of `notRunShare` and out of the validation.

## 5 Comparisons

The rule in force: an optimisation is merged only when it helps by more than the noise. Made exact:

1. **Before any run**, the pull request names **one primary row**, the direction expected, the mode, and **the deciding sitting**. It may also name rows expected to rise. Every other row is a **watch row**.
2. **One sitting.** Both builds are Release, carry the same measurement code, and use the 500 test library. Alternating pairs (before, after; after, before; ...): **10 pairs** for an expected gain of 20% or more, **20 pairs** for 10 to 15%, and 20 pairs on a busy day whenever the expected gain is under 30%. A simulation built from the development PC's measured spread put the chance that 10 pairs detect a 20% gain at 0.93 on a quiet night but only 0.41 on a busy day, and a 10% gain at only 0.11. A sitting of fewer than 10 pairs never decides.
3. **Replacement** happens only when a run was `disturbed`, wrote no JSON, ended in error or outlasted its load. It is decided from those facts before any timing is read; a `null` disturbed counts as not disturbed. The pair is replaced by a new pair at the end. At most 5 replacements; otherwise the sitting is void. Every dropped run is listed.
4. **Sign test on UI.** Count the pairs where after's UI is lower than before's. **Ties count against.** The gain is shown when the count reaches the one-sided 5% level of a fair coin (exact binomial):

   | pairs | 10 | 11 | 12 | 13 | 14 | 15 | 16 | 17 | 18 | 19 | 20 |
   |---|---|---|---|---|---|---|---|---|---|---|---|
   | lower in at least | 9 | 9 | 10 | 10 | 11 | 12 | 12 | 13 | 13 | 14 | 15 |

   For example P(at least 9 of 10) = 11/1024 = 0.011 and P(at least 8 of 10) = 0.055; P(at least 15 of 20) = 0.021 and P(at least 14 of 20) = 0.058. One alpha, 0.05 one-sided, everywhere.
5. **One deciding sitting per change and mode.** Re-running sittings until one passes adds up their chances of a false pass. A second sitting is allowed only after a code change. Otherwise every sitting's pairs are pooled into one count and judged at the same alpha (the table, extended by exact binomial). Every sitting, passed or failed, is listed in the pull request.
6. **Size floor.** The median of the paired differences (before - after) of UI is at least the larger of 2 ms and 3% of before's median.
7. **Run UI must not point the other way.** Run UI, report-only, is tested the same way. If after is higher on Run UI by the step 4 count, stop: a person looks before anything is merged.
8. **Watch rows.** If after is higher on a watch row by the step 4 count, run a new sitting for that row. Block the merge only if the same row is higher again. A watch row that gains is a finding, not a reason to merge.
9. **Report** for the primary row: both medians, the count, the median difference, and the median of the after / before ratios with the interval from the 2nd smallest to the 2nd largest ratio (97.9% at 10 pairs).
10. Figures of two sittings or two days are **never** compared. The same code has read 157 and 305 ms on two days.

The deciding sitting is a real-window sitting. Real-window sittings are run on the development PC by the maintainer or the coordinating session, never by automated agents or CI. A headless sitting is screening only.

## 6 The validation experiment (a protocol, not yet run)

**Question.** Does `runUiMs` stay put when a known extra load is added, where Wall and UI do not? And what load can it not remove?

**The load.** `ManualCertification/Start-BoardTrialLoad.ps1 -Kind Spin|Memory -Seconds 60` starts a **separate** process. Load threads inside the test process would stretch its garbage-collection pauses and mix two effects. The process runs 6 threads at normal priority. Spin threads do integer arithmetic. Memory threads each stream a 64 MB array, one write per 64-byte line. The process ends by itself after `-Seconds` (at most 60), and the caller also stops it in a `finally` block. It changes no setting. With about 2.7 processors of other work, 6 threads still leave 12 logical processors short of full, so the spin arm tests **modest oversubscription only, not saturation**. Where it does slow the Board, it may be through twin sharing or core kind rather than time off the processor.

**Protocol.** One sitting; a Release build of master with the measurement code; the headless budget test alone, each run its own process.

- Arms: **R** (the PC as it is), **S** (R plus the spin load), **M** (R plus the memory load). Order R S M M S R, twice: 4 runs per arm, 12 runs. The load starts 2 s before the test process and ends when it exits. A run that outlasts its load is dropped and run again.
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
  The interface thread ran for 151 ms of it and was not running for 253 ms (63%); garbage collection paused 12 ms, compilation 0 ms.
During this run other programs kept 9.8 of 12 processors busy on average.
Timing tests need a quiet PC. Close other programs and run the test again before reading this as a slower Board.
```

- With several failed rows, each row's two lines come first, then the load line and the last line once.
- When the running time could not be read, the second line reads `How long the interface thread ran could not be read on this system.`
- When the load could not be read, the third line reads `How busy the PC was could not be read on this system.`
- When the thread ran for at least 90% of the row, the second line reads, for example, `The interface thread was running for 240 of the 257 ms. A busy PC can still slow running code.`
- The budget shown is the run's own (x2 Debug, x3 on GitHub Actions), as before.
- The last line is always there, so the message also reads correctly on a quiet PC.

The message states measured evidence. It never names a cause.

The Windows Forms timing tests (`ResponsivenessBudgetTests.vb`) end their two failure messages with the same last line. They have no thread clock and no load reading to quote, so the line is all they add.

## 9 Running a comparison

`ManualCertification/Measure-BoardPairs.ps1`:

- `-Mode window -Before <exe> -After <exe> -Library <test library>`: each run is the trial's `--measure` mode in a real window. Run only on the development PC by the maintainer or the coordinating session.
- `-Mode headless -Before <tree> -After <tree>`: each run is the headless budget test alone, `dotnet test --no-build` in a built tree.
- `-Out <new folder>`, `-Pairs`, `-PrimaryRow <row id>`, `-ExpectedToRise <row ids>`, `-BeforeName`, `-AfterName`, `-Load None|Spin|Memory`.
- `-Pool <earlier sittings' folders>` counts earlier sittings of the same change, mode and primary row together with this one. A void sitting is listed but adds no pairs.
- `-Analyze <a sitting's folder>` reads a sitting again without running anything, for a later re-analysis or a check.

The script runs pairs until it has kept `-Pairs` of them. Once a pair's first run is dropped, its second run is not spent: the pair is replaced whatever it would give.

Each sitting's folder holds `sitting.json` (the runs, in order, with their exit codes), one JSON file per run, each run's output, and `summary.md`. The JSON files name the computer, so they stay on the PC that ran them. The summary names no computer and no folder.

`ManualCertification/Test-BoardPairs.ps1` checks the rule on canned sittings and needs no build: the counts against the table above, 9 of 10 (shown), 8 of 10 (not shown), a disturbed pair that is replaced (with and without its second run), a tie, absent figures, the size floor, Run UI pointing the other way, a watch row higher once and again, a void sitting, two sittings pooled, and five pairs (never decides). The trial's Windows CI job runs it.

## 10 What one run and one comparison cost

- One real-window run: about 0.3 min. The new readings add about 0.1 s, and up to about 1.5 s while `GetSystemTimes` is slow under load.
- One headless run of the budget test alone: about 0.5 to 0.75 min (the test method itself took 15.4 s in one recorded run; process start and discovery are assumed to add 15 to 30 s). On November 21, with other programs keeping 10.7 to 11.4 of 12 processors busy, two such runs through the pairs script took 7.2 and 7.9 min.
- One Release build of the trial and both test projects: assumed 5 to 15 min on a busy PC. A comparison needs two builds; the validation needs one. The before and after trees live in worktrees outside the system's temporary folder.
- A verdict sitting (section 7): 5 to 10 real-window runs, about 2 to 3 min.
- A comparison: 10 pairs in a real window about 7 min, headless about 10 to 15 min. 20 pairs double that. Up to 5 replacement pairs add up to half again. Add 10 to 30 min for the two builds.
- The validation: 12 headless runs, about 8 to 10 min plus about 2 min for the load starts, plus one build. Up to two more sittings if the spin load does not disturb. The real-window part, 12 runs, is about 4 min.

## 11 Verified, assumed, and left out

**Verified by reading the code:**

- UI is the smaller of Wall and the longest gap of a 1 ms timer at Input priority, the probe's tail included. Each row is timed once, in a fixed order.
- The scroll row takes its longest Wall and its longest UI as two separate maxima over its steps, and the row's `wallMs` is the longest Wall.
- Rows of N is the default. The search keystroke, the first switch, Stage to Submitted and the scroll wait for rows of N, and the headless test fails on enforced rows only.
- The Windows Forms tests build the same failure text as the trial's first line.

**Measured on the development PC while the method was designed** (probes, not part of the tests):

- Over 200 spins of 2 ms, 78 came within 2% of the nominal 2,496 MHz, and the closest read 0.02% above it. `QueryThreadCycleTime` took 0.8 to 1.4 microseconds a call. Logical processors 0 to 3 are of the faster class, 4 to 11 of the slower; a normal-priority thread from a background console ran only on 4 to 11 over 300 wakeups.
- `GetThreadTimes` moves in 15.6 ms steps. `GetSystemTimes` took a median of 8.5 ms at an ordinary moment and blocked for a median of 398 ms (up to 727 ms) under full load. A busy twin core made the same work take about 3.6 times the processor time.
- With a synthetic load, running time stayed at 23.5 to 25.5 ms while Wall went from 24 to 148 ms, and memory load raised running time by 21 to 41%.

**Measured with the built code** (headless budget test on the development PC): the rate checks passed at 0.98 to 0.999 of nominal after 1 to 9 spins; a 50 ms sleep read under 0.03 ms of running time; every 500 row stayed within `runMs <= 1.02 x wallMs + 1` and `runUiMs <= 1.02 x uiMs + 1`. In ordinary runs other programs kept 10.7 to 11.4 of 12 processors busy, so every run was `disturbed`. On a day like that, a headless sitting under the starting line would be void.

One observation from two headless runs of the same build, not a finding: Stage filter back to All stages read Run UI 185 ms with a fast-core share of 1, and 338 ms with a share of 0. Run UI does not remove the kind of core a run lands on (section 1).

**Assumed:**

- That removing time off the processor is most of what a real busy day adds. One synthetic experiment suggests it; the validation and the new figures will show it.
- That `clock_gettime` through `libc.so.6`, `/proc/stat`, `sysconf` and `sched_getcpu` work on the Ubuntu runner. Nothing has run on Linux yet.
- That the registry's `~MHz` value exists on the Windows CI runner. If not, the figures are absent.
- That the thread-cycle counter runs at the nominal rate on other Windows PCs; the per-run check catches it when not.
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
3. **When Published is prepared while the Board is idle**, the switch after it gets a new id (`first-switch-published-after-idle`), so today's `first-switch-published` keeps its meaning. Nothing is built for it yet.
4. **The verdict rule of section 7 stays a proposal**, printed under "proposed verdict (report-only)".
