# Board trial report

Draft of November 20, 2026. Four parts wait for a person: the look of the Board on a real Windows contrast theme (step 11), the Narrator pass (step 17), a start on a real Linux machine, and the maintainer's own hours, which the second k needs. Section 10 lists the decisions that wait for the maintainer.

## Summary

- **What was built.** The Board of PaperRoute Tracker, ported to Avalonia 12.1.6 in C# over the unchanged Visual Basic core, beside the Windows Forms app. It has shelves and cards, search, the stage and sort boxes, the Needs Attention chips, arrow keys, Light, Dark and high contrast, a read-only copy of a library folder (`--snapshot-from`) and a measuring mode (`--measure`). 181 headless tests pass on Windows and on Ubuntu (CI run 44703237752).
- **Finding 1.** The C# interface over the VB core works. Compiled bindings reach VB core types, and no build error that crossed the VB/C# line is recorded in any trial pull request or in the notes.
- **Finding 2.** One card view is expensive: about 15 ms the first time (section 4.4). Drawing every card misses the budgets by far. Any long list in the full port must be virtualized from the start.
- **Finding 3.** The hours are small against the plan: 6.3 recorded hours, Board work about 3.5 hours. But they are wall-clock hours, nearly all of unattended agent sessions, not a person's working hours. So k (0.06 to 0.20; k is the factor that scales the plan's unit sizes, section 2.3) describes this way of working. It is not a forecast of calendar time.
- **Finding 4.** Avalonia needed help in places the design did not expect: the open-list keys of the boxes, Tab between cards under rows of N, and reuse of virtualized rows. As the design foresaw, Fluent's menus, scroll bars and tooltips also need control themes for contrast.
- **Choice made.** Rows of N is the default. (Rows of N: the shelf makes only the rows of cards on screen and reuses them as you scroll.) It was chosen at step 13 from the headless numbers. The real-window run, taken afterwards, confirms it: drawing every card went over the budget on all four rows that the design's rule uses to decide (section 4.3).
- **What waits.** Two budget rows are still over in a real window (first switch to Published, search keystroke); the report-only row, process start to Board (2,055 ms), is also over 1,500. The contrast look, the Narrator pass, the maintainer's hours and a real Linux start all need a person.

## 1. What the trial built

Steps are those of design section 8. Every pull request named here is merged; #399 (steps 11 to 13) merged on November 20, 2026, after this report's numbers were taken from its head, 44d62e0.

| Step | What | State | Pull request |
|---:|---|---|---|
| 0 | Maintainer's answers; tracker issue | Tracker #160 exists. The maintainer decided three things on 2026-11-15, recorded on #160: C# over the VB core, on master, two tracks. The design's other step 0 items (the names, two solution files, no developer tools, the checklist weights, H = 6) were taken at the design's defaults when work began on 2026-11-19 and reported to the maintainer, who has not yet confirmed them. `--snapshot` of the real library was held back. | none |
| 1 | PR A: fixture project | Done | #378 |
| 2 | PR B: empty trial solution | Done | #384 |
| 3 to 5 | Session and startup; Board rules; #338 and debounce | Done | #383 |
| 6 to 8 | Styles; shell; Board without cards | Done (core-reference guard and sort pins added) | #390 |
| 9, 10 | Cards and shelves (every card); arrow keys | Done | #396 |
| 11 | High contrast | Built and tested headless. The look on a real contrast theme waits for the maintainer. | #399 |
| 11b | `--snapshot-from` | Built; Linux path fix in 44d62e0 | #399 |
| 12 | Pictures, budgets, `--measure` | Built. Four budget rows wait under rows of N. | #399 |
| 13 | Rows of N behind a constant | Built; it is the default (`ShelfLayout.Option = "b"`; `-p:ShelfOption=a` builds every card) | #399 |
| 14 | `--snapshot` of the real library | Not built. The argument is refused with exit code 2. Waits for the maintainer's word. | none |
| 15 | `--platform-check` and the `trial-linux` CI job | CI job done. `--platform-check` and the Linux checklist are not built. | #398 |
| 16 | Spikes | Working copy done. VB `.axaml` done as a throwaway folder outside the repository, never committed. | #393 |
| 17 | Narrator pass | Not done; it is the maintainer's | none |
| 18 | PR C: this report | This draft | to come |

## 2. Hours and calibration (design 7.1)

### 2.1 What the hours are

Read this first:

- The recorded hours are **wall-clock time, nearly all of unattended agent sessions** (the 0.2 h on 44d62e0 is the orchestrating session's). Each trailer gives the writer's own estimate of its session's wall-clock time. Six of the 15 agent trailers differ by 0.1 h from the agent's recorded duration. They are not a person's working hours.
- So k below measures **this way of working**: agents write, review and fix, and the maintainer reviews later. It is not a forecast of calendar time for the full port.
- **k from the maintainer's own hours cannot be given.** No review hours are logged yet (section 2.3).
- The trailers say "(agent session, wall clock)", or "(main session, wall clock)" for 44d62e0, which the orchestrating session wrote, instead of the design's part names. The part of each commit below was assigned from its changed files (`git show --stat`), not recorded.

The whole push ran from about 18:00 on November 19 to about 04:00 on November 20, 2026, mostly unattended. Steps 2 to 13 span 1725a1d (18:28) to 44d62e0 (03:31), about 9 hours on the clock. That is time on the clock, not effort.

### 2.2 Hours table

Source: `git log` of branch `v11/board-trial-report` at 44d62e0 (16 commits carry the trailer); agent durations from the workflow records of the push. "Estimated" means the commit has no trailer and its hours are taken from the duration of the agent that made it.

| Part | Recorded (h) | Commits | Estimated, no trailer (h) | Plan's size for the whole unit |
|---|---:|---|---:|---|
| board (with Board tests) | 2.8 | ade6334, 5c3867c, 6e22587, f25658e, b447a43, e29a58e, f337fec | 0.71 (24cb2d7, 5174b5f, 6fadabb) | unit 2, L, 2 to 4 weeks |
| styles | 0.9 | 59af2e5, fba5671 | 0 | unit 0, M, 1 to 2 weeks |
| shell | 1.1 | 5660ff0, 1ac0156, 44d62e0 | 0.3 (983c74a) | unit 1, L, 2 to 4 weeks |
| measure | 1.1 | 2d9c561, 5c6f608 | 0 | (groundwork) |
| tests-harness, non-Board | 0.2 | eede552 | 0 | groundwork, 3 to 5 weeks |
| spike-workingcopy | 0.2 | 2165f61 | 0 | 0.5 to 1 day |
| spike-vb | 0 | none | not measured (no commit) | 1 hour |
| setup | 0 | none | 1.0 (SDK spike 0.5, 1725a1d 0.4, 63f4381 0.1), plus a share of PR A | none |
| PR A, the fixture project (4b2853c, 36fe286, setup and Board tests) | 0 | none | 0.5 (split between the two unknown) | none |
| PR A review fix (cbbabad), docs files | 0 | none | 0.6 (cbbabad) | none |
| Linux CI job (17c56e4) | 0 | none | not measured (no record covers it) | none |
| **Total** | **6.3** | 16 commits | **3.1** | |

Agent time by role, all trial workflows together (workflow records, 2026-11-15 to 2026-11-20):

| Role | Minutes | Hours | In a trailer? |
|---|---:|---:|---|
| Implementers | 460.7 | 7.68 | mostly |
| Reviewers | 158.6 | 2.64 | never |
| Fixers | 93.8 | 1.56 | only 6e22587, e29a58e and 5c6f608. The fix commits 63f4381, 6fadabb and cbbabad carry no trailer (their time is in the Estimated column). |
| **All agents** | **713.1** | **11.9** | |

Reviewers in one wave ran side by side, so the elapsed time was shorter than these sums. The agents used 8,324,820 tokens in all.

What the hours leave out:

- review agents (2.64 h) and most fix agents;
- the VB spike (3.3 minutes of agent time, no commit);
- the orchestrating session, except its 0.2 h on 44d62e0 (not measured);
- CI time (not measured);
- the design work of 2026-11-15, which came before the trial;
- every hour of the maintainer (none logged).

The trailers do not always match the agent's duration. For example, 5660ff0 says 0.4 h against 17.7 minutes (0.30 h). Computed from durations, the seven recorded board commits come to 3.04 h, against 2.8 h in the trailers.

### 2.3 Board hours B, f and k

**B, the Board hours.**
- **Main figure, B = 3.5 h:** board 2.8 h recorded + 0.71 h estimated for the rule commits of #383. These hours leave out contrast and the budgets, so B = 3.5 h is paired with an f that leaves out those two items too.
- **Wide figure, B = 5.0 h:** adds contrast (fba5671, 0.4), budgets and pictures (2d9c561, 0.6) and the review fix of steps 11b to 13 (5c6f608, 0.5), which can be read as Board work. B = 5.0 h is paired with the full f.
- 36fe286's share of PR A is left out of both, because it cannot be split.

**The checklist for f** (weights fixed in design 7.1 before work started):

| Item | Weight | State | Strict | Generous | Reason |
|---|---:|---|---:|---:|---|
| Shelves and cards | 25 | done | 25 | 25 | built; tests pass on Windows and Ubuntu |
| Search | 7 | done | 7 | 7 | built; tests pass |
| Stage and sort boxes with #338 | 8 | done | 8 | 8 | built; tests pass |
| Needs Attention chips | 7 | done | 7 | 7 | built; tests pass |
| Arrow keys and Tab routes | 8 | half (strict) or done (generous) | 4 | 8 | built; K1 and K8 pass headless. Tab out of the shelf is left to Avalonia, so today's Tab route out of the shelf was not reproduced. |
| Light, Dark and contrast | 8 | half (strict) or done (generous) | 4 | 8 | contrast tested headless with stand-in colours, never on a real Windows contrast theme |
| Board's headless tests (6.2) | 12 | done | 12 | 12 | 181 of 181 on both runners |
| Narrator pass | 5 | not done | 0 | 0 | the maintainer's step 17 |
| 500 library within budget | 10 | not done (strict) or half (generous) | 0 | 5 | two rows over in a real window: first switch 157 against 100, search keystroke 99 against 50 |
| Card menu with working actions | 5 | outside the trial | 0 | 0 | design 1.4 |
| Move to File Drawer with Undo | 5 | outside the trial | 0 | 0 | design 1.4 |
| **Sum** | **100** | | **67** | **80** | |

f (strict) = 0.67. f (generous) = 0.80. Counting each arguable item as half gives 0.72.

Paired with B = 3.5 h, which leaves out the hours for contrast and the budgets, f leaves out those two items: 25 + 7 + 8 + 7 + 4 (strict) or 8 (generous) + 12, so 0.63 (strict) or 0.67 (generous).

**Arithmetic.** H = 6, the design's default; the maintainer has not stated H. Every d and k scales as 6 / H.

For the maintainer: state H (your working day in hours).

d = B / H (days). Board size = (d / 5) / f weeks. k = size / 3 (3 weeks is the plan's midpoint for unit 2). Rescaled estimate = k × 42.25 + 29.25 weeks.

| B (h) | d = B / 6 | d / 5 (weeks) | f | Size (weeks) | k | k × 42.25 | Estimate (weeks) |
|---:|---:|---:|---:|---:|---:|---:|---:|
| 3.5 | 0.583 | 0.1167 | 0.63 | 0.1852 | 0.0617 | 2.61 | 31.9 |
| 3.5 | 0.583 | 0.1167 | 0.67 | 0.1741 | 0.0580 | 2.45 | 31.7 |
| 5.0 | 0.833 | 0.1667 | 0.67 | 0.2488 | 0.0829 | 3.50 | 32.8 |
| 5.0 | 0.833 | 0.1667 | 0.80 | 0.2083 | 0.0694 | 2.93 | 32.2 |
| 11.9 (every subagent hour; excludes the orchestrating session, CI and the maintainer) | 1.983 | 0.3967 | 0.67 | 0.5920 | 0.1973 | 8.34 | 37.6 |
| 11.9 | 1.983 | 0.3967 | 0.80 | 0.4958 | 0.1653 | 6.98 | 36.2 |

The plan as written (k = 1) gives 42.25 + 29.25 = 71.5 weeks. Even every subagent hour uses only about 8 of the 42.25 unit weeks. Nearly all of the rescaled estimate is then the 29.25 weeks of groundwork and release, which the trial did not measure.

**k given twice, as the design asks.**
1. From the total hours: k = 0.058 to 0.083 (Board hours, each B paired with its own f), 0.165 to 0.197 (every subagent hour). This calibrates the remaining units only if they are built the same way.
2. From the maintainer's own hours: **cannot be given**. Zero hours are logged.

For the maintainer: please log your hours in comments on #160, per part and per pull request. Log them for: step 0's decisions; reviewing #378, #383, #384, #390, #393, #396, #398 and #399; merging and CI fixes; the contrast look (step 11); step 14, if kept; the Narrator pass (step 17); and reviewing this report. Mark hours already spent, which you can only give from memory, as estimates.

## 3. The plan's six questions (design 7.2)

### 3.1 How long the Board takes

See section 2: B = 3.5 h (wide 5.0 h) of agent wall-clock time, f = 0.67 to 0.80 (0.63 to 0.67 against B = 3.5 h), k = 0.058 to 0.083. What is unfinished: the Narrator pass, the contrast look on a real theme, two budget rows in a real window, and the card menu and File Drawer panel (outside the trial).

### 3.2 Does C# build over the VB core with compiled bindings? Does a VB `.axaml` build?

**C# over VB: yes.** Since #383 every build compiles C# views with compiled bindings to view models that hold VB core types.
- `PaperRoute.ViewModels.csproj` references `PaperRoute.Core.vbproj`. Compiled bindings are Avalonia 12's default, every view sets `x:DataType`, and no `{ReflectionBinding}` exists.
- `CardView.axaml` binds straight to a VB core type (`x:DataType="services:RouteDotState"`).
- No build error that crossed the VB/C# line is recorded in any trial pull request or in the notes. The only noise was five CA1416 warnings inside `PaperRoute.Core` (#384), which core stage 2 removes.
- `CoreSurfaceTests` reads the built assemblies and fails on any core reference not in `allowed-core-references.txt` (46 entries) and on any listed entry nothing uses (#390, #393).
- The core needed three `InternalsVisibleTo` lines (#378, #384).

**VB `.axaml`: it builds, for the smallest case.** A VB library with one `UserControl` and one compiled binding built with 0 warnings and 0 errors (Avalonia 12.1.6, SDK 10.0.403). A misspelled binding path failed with AVLN2000, so the XAML compiler does check VB. A VB headless test saw the binding follow a change. The first error was VB name resolution, not Avalonia: `BC30456: 'CancellationToken' is not a member of 'Avalonia.Threading'`.

Limits: no window as root, no templates, no commands, no Release build, no publish. VB gets neither Avalonia's source generator nor CommunityToolkit's, so properties and commands are written by hand. This does not reopen the C# decision. The spike lives in a throwaway folder outside the repository (the design asked for a scratch branch); its time is not measured.

### 3.3 Which test framework

**MSTest 4.5.2 with `Avalonia.Headless` 12.1.6 and `Avalonia.Skia` 12.1.6**, the design's first choice. xUnit was never needed. In CI run 44703237752 (head 44d62e0), 181 of 181 pass on Windows and 181 of 181 on Ubuntu, each with 31 pictures.

What it needed:
- Tests stay synchronous. Awaiting the dispatcher and then disposing on it deadlocks. `Headless.Run` runs each body in one `Dispatch` call and blocks with a timeout.
- The session is started and disposed from a thread-pool thread (`AssemblySetup.cs`).
- Pictures go to a folder named by `PAPERROUTE_TRIAL_PICTURES`, because MSTest does not tell a test where `--results-directory` points.
- `.axaml` files are linked as `Content`, because Avalonia's build removes every `None` item named `*.axaml`.

Pass mark of design 6.1: met. The Release run and CI pass; all four checks of run 44703237752 passed. Ten full local Release runs in a row passed, 181 of 181 each time (the development PC, quiet, 2026-11-20 04:15 to 04:43, 161 to 176 seconds a run, the tree of 44d62e0), and so did one Debug run of the same 181 tests (04:43 to 04:46). The harness hours are not separate from the other trailers.

**The enforced budget rows can fail on a busy PC.** Locally in Release they are held at the budget with no multiplier, as today's `ResponsivenessBudgetTests` are. On 2026-11-20, every full local run on the quiet development PC passed (two at 03:25 and 03:30, and the ten above). Two full runs made between them, while other agent work was using the PC, each failed one enforced row: back to All stages at 257 ms against 250 (the run that ended at 04:03), and the open list at 329 ms against 250 (the run that ended at 04:08). Each time the budget tests passed when run alone a minute later. Those two runs were on core stage 2 branches merged with master, so they also had a different core. A third full run, on the branch that holds all of core stage 2 and made once the other work had ended, passed all 181 (it ended at 04:11). So the cause was the busy PC, not the core. CI holds the rows at three times the budget and passed with room (section 4.1).

### 3.4 The working-copy spike

**It fits.** Today's way of editing a record (original, working clone from `ManuscriptCloneService`, baseline JSON snapshot, as `EditManuscriptForm` does) works under a bound view model with no core change. Three tests pass (#393).

What bends:
- A bound box writes into the working clone at every keystroke, not at Save. Save's trimming, the blank-title rule, the journal lookup and finishing a stage change need a new place in each edit-window unit.
- The #358 rule that protects typed text from a workflow step's refresh has no direct counterpart. It needs a decision per unit.
- Each unsaved-changes check takes a full snapshot: 40 to 65 µs for the largest records, measured in a loop, not as typing latency.

Limits: only Title was tried. Not tried: the other fields, saving, a recovery draft, discard, and a new baseline after a save. The test types the old value back instead of using an undo, as the design wrote.

### 3.5 Does it start on Linux

**It builds and its headless tests pass on Ubuntu. Nothing has started a window on a real Linux desktop.** See section 7.

### 3.6 Large library against today's budget

See section 4.

## 4. The 500 library against today's budgets

### 4.1 The comparison

UI ms: the longest stall of the interface thread during the action. Medians for the headless (5 runs) and real-window (3 runs) columns; the Windows Forms column and the CI table are single runs. Budgets from design 6.3.

Sources and dates:
- **Windows Forms:** v1.0.1 certification run of `ResponsivenessBudgetTests`, commit d67c312, 2026-11-14, Release, one run, window off-screen (`TestResults/v101-certification-evidence/B3-budget-d67c312-console.txt` on the development PC).
- **Headless:** the development PC, Release, median of 5 runs (PR #399 body, "The numbers"); probe at Input priority (`PaperRoute.Desktop/Measurement/BoardMeasurement.cs:81-115`); 1280 x 800 and DPI 96 (`PaperRoute.Desktop.Tests/BoardBudgetTests.cs`); 2026-11-20 03:01 to 03:09 (the run files of the review fix 5c6f608, not in the repository).
- **Real window:** the development PC, 2026-11-20 03:33 to 03:35, Release, DPI 120 (125%), 1280 x 800 (from the push notes; the window size is not in the JSON), the `large-500` folder through `--snapshot-from`, median of 3 runs (`TestResults/v11-trial-pictures/real-window-2026-11-20/measure-500-*.json` on the development PC, not in the repository).

| Row | Budget | Windows Forms | Headless, every card | Headless, rows of N | Real window, every card | Real window, rows of N |
|---|---:|---:|---:|---:|---:|---:|
| Warm startup | 1,500 | 1,058 | 3,032 | 481 | **1,603** | 780 |
| Process start to Board (report-only) | 1,500 | not measured | not measured | not measured | **3,339** | **2,055** |
| First switch to Published | 100 | 6 | **3,970** | **383** | **1,955** | **157** |
| Render, no card changes | 250 | 7 | 9 | 10 | 6 | 18 |
| Stage filter to Submitted | 250 | **367** | **1,107** | 240 | **734** | 128 |
| Back to All stages | 250 | **4,230** | **1,406** | 157 | **838** | 74 |
| Open list stepped and closed | 250 | **314** | **553** | 154 | **349** | 111 |
| Search keystroke | 50 | no row | **669** | **208** | **368** | **99** |
| Published scroll, longest step | 250 | no row | 16 | **288** | 19 | 147 |

Bold: over budget at the median.

Paint rows (the frame drawn after each action; budget 250 each): real window 10 to 29 ms under every card and 14 to 18 ms under rows of N; headless rows of N 2 to 43 ms (warm startup's frame 43); headless every card 2 to 25 ms. Windows Forms paint rows for the same actions were 5 to 17 ms (Board paints after page switches, not measured in the port, were 34 and 39).

Headless stage filter to Submitted under rows of N: runs 240, 237, 246, 206 and 251. One run of five is over.

**CI, run 44703237752** (PR #399, head 44d62e0, 4 processors, DPI 96, budget × 3, rows of N only):

| Row | Budget × 3 | Windows | Ubuntu | Rule |
|---|---:|---:|---:|---|
| Warm startup | 4,500 | 316 | 199 | enforced, passed |
| First switch to Published | 300 | **304** | 191 | waiting |
| Render, no card changes | 750 | 5 | 4 | enforced, passed |
| Stage filter to Submitted | 750 | 147 | 98 | waiting |
| Back to All stages | 750 | 154 | 63 | enforced, passed |
| Open list stepped and closed | 750 | 98 | 62 | enforced, passed |
| Search keystroke | 150 | **170** | 84 | waiting |
| Published scroll, longest step | 750 | 214 | 169 | waiting |

Every enforced row, paint rows included, passed on both runners. A "waiting" row is reported but does not fail the test.

### 4.2 Which comparisons are like for like

| Row | Like for like with Windows Forms? | Why |
|---|---|---|
| Render, no card changes | same action, different probe | see below |
| Open list stepped and closed | same action, different probe | see below |
| Stage filter to Submitted | same action, different work | Windows Forms keeps card windows; the port resets both shelves |
| Back to All stages | same action, different work | Windows Forms rebuilds the visible shelf's card windows; rows of N makes only the rows on screen |
| Warm startup | **no** | Windows Forms draws only the first screen; the rest fills in while idle (wall 4,166 ms). Its window was off-screen. |
| First switch to Published | **no** | Windows Forms switches to a shelf already prepared while idle. The port makes the cards at the switch. |
| Search keystroke, scroll | no figure | Windows Forms has no such row |

Other differences: the Windows Forms run is from 2026-11-14 and off-screen; nobody measured it in a real window or on 2026-11-20. The real-window port runs used DPI 120 and the `large-500` folder; the headless and CI runs used DPI 96 and a fresh fixture. The port renders the search keystroke at once, not after the 200 ms wait. The real-window runs used the computer's date (2026-11-20); the design passes `--today 2026-10-16` only for repeatable runs and omits it for the comparison with Windows Forms (design 6.3).

The probes differ. Windows Forms' UI ms is the longest stretch in which its off-screen window processed no messages (DPI 96, one run). The port's is the longest gap between ticks of a 1 ms `DispatcherTimer` at Input priority; whether this matches `WM_TIMER` is not confirmed (section 8). The port also steps the open list by `SelectedIndex`, not by real key messages (section 9).

### 4.3 The choice: rows of N

The design's switch rule (4.4): move to rows of N if every card goes over any of four rows in the real window. Rows of N was chosen at step 13 from the headless numbers (f337fec). The real-window run, taken afterwards on 44d62e0, confirms it: in a real window every card went over all four of the design's switch rows:

| Switch row | Budget | Every card, real window | Rows of N, real window |
|---|---:|---:|---:|
| Warm startup | 1,500 | 1,603 (one run of three was within) | 780 |
| First switch to Published | 100 | 1,955 | 157 |
| Back to All stages | 250 | 838 | 74 |
| Open list stepped and closed | 250 | 349 | 111 |

Rows of N is the default (#399). It is also faster than today's Windows Forms figures on the open list, and on back to All stages, though there the two do different work (4.2). The Windows Forms run was off-screen. It costs more on one row: the Published scroll (19 ms to 147 ms in a real window), because each page rebinds card views to new cards and lays them out. Render with no changes is also higher (6 to 18 ms), still well under 250 ms.

The every-card build stays selectable (`-p:ShelfOption=a`). CI builds and tests only rows of N. Every card last passed 178 of 178 locally in Debug (#399).

### 4.4 The rows that still wait

The waiting list is compiled into the build (`BoardMeasurement.s_waitingUnderRows`) from the headless runs. It is not a real-window verdict.

| Row | Headless | Real window | CI Windows (× 3) |
|---|---|---|---|
| First switch to Published | over (383 / 100) | over (157 / 100) | over (304 / 300) |
| Search keystroke | over (208 / 50) | over (99 / 50) | over (170 / 150) |
| Stage filter to Submitted | 240 / 250, over in 1 of 5 runs | within (128) | within |
| Published scroll | over (288 / 250) | within (147) | within |

The cause is the cost of a card view: about 3 ms to make and about 12 ms to attach and lay out the first time (#399), about 15 ms in all. The code comment beside the same headless numbers says about 16 ms each (`PaperRoute.Desktop/Measurement/BoardMeasurement.cs:105`). Even the 21 views on one screen exceed a 100 ms switch and a 50 ms keystroke.

What could bring them in (from #399 and the workflow records; none is built):
- make a card view cheaper: a flatter tree, fewer bindings, menus built later;
- prepare the hidden Published shelf while idle, as today's app does;
- draw only the first screen at startup, as today's app does;
- defer the hidden shelf's card updates until it is shown (today a shelf shown once rebinds its 21 views on every search or stage change);
- for the scroll, cheaper reuse of card views (inferred; no source names a fix).

The second and third items are how today's app fills the Board while it is idle, which design 4.4 and plan :428 said would not be ported.

For the maintainer: say whether the budget verdicts rest on the real window (the design's rule for the switch) or on headless (the stricter guard in CI).

## 5. High contrast

### 5.1 What was built

Under a Windows contrast theme the tokens take the system colours as today's `UiTheme.vb` maps them (`PaperRoute.Desktop/Styles/ContrastTheme.cs`, `BuildTokenRoles`):

| System colour | Tokens |
|---|---|
| Window | every background token, every `Stage*Background` and `Tag*Background` |
| WindowText | `CardBorder`, `PrimaryText`, `SecondaryText`, `Warning`, `Danger`, every `Stage*Foreground` and `Tag*Foreground` |
| GrayText | `SubtleBorder`, `MutedText` |
| Highlight | `Accent` |
| HotTrack | `AccentSecondary` (focus rings of buttons, links, tabs and the rail; a link while pressed) |
| HighlightText | `OnAccentText` |

- Pills, tags and the warning, danger and active chips get a 1 px outline in their own ink. Warning and danger keep only their bold weight, as today.
- `Styles/Contrast.axaml` holds the rules no token can carry: a pressed chip takes the accent fill, a chip's focus ring is WindowText, the quiet ⋯ button gets an outline, card motion is off, selection uses the accent, the open drop-down list takes the card background.
- A theme change needs no restart: the brushes keep their identity and take new colours on `ColorValuesChanged`.
- The design's main risk did not happen: a theme dictionary keyed by a custom `ThemeVariant` does resolve through `DynamicResource` in 12.1.6.

What did not fit: the ⋯ card menu, the scroll bar, tooltips and the Fluent focus visuals of the search box and the two boxes keep Fluent's Light or Dark palette. Unit 0 of the full port needs control themes for these.

### 5.2 What was tested

All headless, with two stand-in themes in `SystemContrastColors.cs` ("Night sky", dark; "Desert", light). Their colours were written from memory of Windows 11's themes, not read from a Windows install.
- T4: a card follows Light to Dark on the same view.
- T5: under both stand-ins every pill, tag and chip tone is outlined in its ink.
- `AContrastThemeGoesOnChangesAndGoesOffWithoutARestart`: Light, Night sky, Desert, then light again.
- Pictures for both stand-ins are in the CI artifact.

Not confirmed: that Avalonia's Win32 platform reports `ContrastPreference.High` under a contrast theme and raises `ColorValuesChanged`, and that `GetSysColor` returns the theme's colours. That code never runs in the tests.

### 5.3 Checklist for a person

For the maintainer: run the trial (master's build, `PaperRoute.Desktop`) on Windows and work through these. Note what you see after each item.

1. Turn on Night sky, then Desert (Aquatic and Dusk if there is time). The Board should switch at once, with no restart. Switching back should restore Light or Dark.
2. Compare with the real theme: window background, text, gray text, the selection colour (link text, selected tab underline, card focus ring, route dots) and the HotTrack colour on the focus rings of buttons, links and tabs.
3. Each of these should have a 1 px outline in its ink: stage pills, tags, the warning, danger and active chips, the rail's Deadlines count, the quiet ⋯ button. Warning and danger should differ from a neutral chip only by weight and outline.
4. Look at the parts the trial could not restyle: the ⋯ menu (background, items, disabled text), the shelf scroll bar, tooltips, and the focus visuals of the search box and the two boxes. Note each one that is hard to read.
5. Check that the title bar and window borders follow the theme.
6. Item 1 also shows whether Avalonia reports the contrast preference and its change on Windows. Note it if the Board does not switch.

## 6. Narrator

For the maintainer: run Narrator on Windows with the trial and fill in the "Heard?" column.

Checklist for the maintainer's pass (design step 17). The middle column is what the code sets (`PaperRoute.Desktop/*.axaml` and the view models). What Narrator actually says has never been observed. Avalonia issue #22341 (ListBox type-ahead) does not apply: the trial uses no `ListBox`.

| Element | Narrator should say | Heard? |
|---|---|---|
| Chips | The chip's text, for example "1 revision due soon". Help text "Filter the board", or "Filter applied. Press to clear it." when active. A zero-count chip is hidden, unless it is the active filter: then it stays shown and ignores a press. | |
| Tabs | Strip named "Shelves". Each tab: "Pipeline (5)", "Published (1)", "File Drawer (1)", or "Pipeline (1 of 5)" while a filter hides some. The tab shows "Pipeline 5", but Narrator should say the bracketed form. | |
| Stage box | "Filter by stage". Items "All stages (N)", then "<Stage> (count)". With the list open, each arrow press is read while the Board stays unchanged (#338). Esc keeps the arrowed item. The Board renders once when the list closes. | |
| Sort box | "Sort the board", the seven sort texts, the same #338 reading. | |
| Card | Named by its title. | |
| Card title | The title, as a link. Its description: stage, status, journal (or "no target journal") and route, joined by commas. Enter or Space gives the notice "Opening a manuscript is not part of the trial: <title>." | |
| Card ⋯ button | "More actions for <title>". Every item disabled, help text "Not in the trial (read-only)". | |
| View route | "View route for <title>", as a link. A press gives "Viewing the route is not part of the trial: <title>." | |
| Tags and route dots | Tags read as "Tags: a, b", not focusable. Route dots should not be read. | |
| Notice strip | Read politely without moving focus: "Loading…", "Loaded 7 manuscripts from local storage.", "Deadlines is not part of the trial.", the F1 notice, and the notices above. | |
| Banner | Landmark "Example library", or "Read-only snapshot" under `--snapshot-from`. | |
| Also | "Board" as a level-1 heading; search "Search the board (Ctrl+F)"; "View in Deadlines" link; "Add Manuscript" (not in the trial); "Clear search and filters"; the rail as a navigation landmark "Pages". | |

Checks that rows of N needs before it is chosen for good (design section 9):
- **The card count.** What Narrator says about how many cards a shelf holds, when only 20 to 24 card views exist. The shelf list has no accessible name in the views.
- **The reading order across reused rows:** title, ⋯, View route, card by card, with no card read twice or skipped after a scroll.
- **Into a row not made yet.** What Narrator says after Down, Tab or Shift+Tab moves into a row that did not exist a moment before.

Unproven: Avalonia's list accessibility across virtualization. Avalonia pull request #20733 (item count, focus across virtualization) was closed without merging (r2 5.1).

## 7. Linux

**What CI shows.** Job `trial-linux`, "Avalonia Board trial (Linux, report-only)", on ubuntu-latest (#398). The Release build must succeed. Tests are report-only: failures are listed and the job stays green.

| Run | Pull request | Tests | Pictures |
|---|---|---|---|
| 44681774261 | #398 (first run) | 132 of 132 (from the push notes; not downloaded again) | 17; `board-light.png` looked the same as on Windows |
| 44703237752 | #399, head 44d62e0 | 181 of 181 | 31 |

Found on the way:
- The first path fix accepted only drive-letter paths, so `--snapshot-from` and `--measure` refused every folder on Linux. 44d62e0 accepts a path from the root off Windows.
- Refusal messages show a Windows example path on every system.

Related, found by core stage 2 (#388): issue #395 (open): with no `~/Documents`, as on a CI runner, `ManagedLibraryService` throws. Three options wait for the maintainer.

**What a real machine must still show.** First, someone writes `docs/design/Board-Trial-Linux-Checklist.md` and builds `--platform-check`; neither exists. Then, on Ubuntu 24.04:
1. `dotnet publish -r linux-x64 --self-contained` the trial.
2. Pack it as an AppImage.
3. Start it without `libfuse2`.
4. See the example library open.
5. Run `--snapshot-from` over a copied library folder.
6. Run `--platform-check`: the file dialog, `Launcher`, and a restart from `$APPIMAGE`, each outcome written to JSON.

For the maintainer: a real Linux start needs a person at a Linux machine or the VirtualBox guest. Agents do not touch the virtual machine.

## 8. What was harder than the plan thought

**Cards and lists**
- One card view costs about 15 ms the first time (section 4.4). Give the card view a cost budget early, before other card-heavy pages are built.
- Drawing every card was not enough; rows of N became the default. Plan virtualization from the start for any list that can reach hundreds of items.
- In Avalonia 12.1.6 an `ItemsControl` with a row `ItemTemplate`, as the design specified, rebuilt a row's card views every time the row was reused. The fix makes the row view itself the container.
- No test failed at first when a reused card kept the previous manuscript's title. Three "drawn wrong after reuse" tests were added and seen to fail with the binding broken. Every virtualized template needs such a test.
- Rows of N needs every card the same height, so the journal row is held to one tag chip tall.
- A hidden shelf shown once still rebinds its card views on each render.

**Keyboard**
- Avalonia's `ComboBox` does not select as the arrows move through the open list, and Esc forgets the arrowed item. `ArrowChoiceKeeper` gives Windows' behaviour. The design's fallback, "adjust only the binding", was not enough.
- `ItemsControl` handles arrow, Home, End and page keys itself. Over a virtualizing panel, Ctrl+Down scrolled to the end while focus stayed put. The row list now leaves those keys to the shelf.
- Tab out of the shelf cannot be computed: `KeyboardNavigationHandler.GetNext` is internal, and `FocusManager.FindNextElement` returned the card's own View route. It is left to Avalonia. Custom Tab routing in the full port needs a public API or its own focus-order walk.
- Focus into a row not made yet works headless, but nobody has checked it with a real keyboard or mouse wheel.

**Measurement**
- The interface-thread probe first ran at Normal priority. It ticked between layout and render jobs and hid about half the stall. It now runs at Input priority, with a test. Whether this matches `WM_TIMER` on real Windows is not confirmed. The measuring tool needs tests of its own.
- By the run's rule, agents did not open a window, so the orchestrating session took the real-window run afterwards.

**Snapshot safety**
- The first folder refusal could be passed with `\\?\` or `\\.\` prefixes, a loopback admin share or a stream name. Both refusals now share one rule with a test per spelling. Two cases still cannot be refused: a library moved out of the default folders, and a default folder reached through `subst` or a mapped drive.

**Tests and tooling**
- Headless tests must stay synchronous (section 3.3).
- The fixture's ids are random on each run, so sort orders are compared by position.
- The app's base font is 10 points, not 9 (body 13 px, page title 21 px).
- Avalonia's build package sends telemetry on every compile unless `AVALONIA_TELEMETRY_OPTOUT` is set.

**Contrast and spikes:** see sections 5.1, 3.2 and 3.4.

## 9. Where the port follows today's code and the design read otherwise

The port follows today's code in each case below.

**Board rules (#383, #390)**
- A chip whose count is zero ignores a press, even when it is the active filter.
- A missing target journal suppresses the "consider filing" suggestion.
- The card occurrence counts run over the visible sorted list.
- No Board sort depends on today's date, so all seven orders are pinned (the design said two depend on it).

**Styles (#390)**
- Base font 10 pt; card padding 14; pill inset 8 by 3; every tab bold with a 3 px underline and a gap of 22.
- Tab counts are drawn bare ("Pipeline 5").
- Dark mode's primary text has no value in today's code; the trial uses `#F0F0F0`.
- Selection in the search box uses the system accent, not PaperRoute's teal.

**Cards (#396, #399)**
- Cards are spaced by 12 with no margin of their own; the title comes before the pill in the reading order.
- A card shows its focus ring only while its title has the keyboard focus (the design said focus anywhere inside it).
- The row list's container is the row view itself.

**Keyboard (#390, #399)**
- `ArrowChoiceKeeper` on the two boxes; Tab out of the shelf left to Avalonia; arrow, Home, End and page keys left to the shelf.
- Focus moves at once after `ScrollIntoView`, with a `Dispatcher.Post` fallback (the design said Post at `Loaded`).

**Contrast (#399)**
- The primary button's hover and pressed colours darken toward black on a dark contrast theme too, because today's `IsDark` is never true under contrast.
- A chip's focus ring is the window text colour; the quiet button gets an outline.
- A separate `Contrast.axaml` is switched on for rules no token can carry.
- A colour change within one variant recolours the existing brushes in place.

**Snapshot (#399)**
- The banner and title name the folder copied and the time of the copy.
- `ILibrarySession.Snapshot` was added (not in design 3.1).
- The refusal covers all eight default folders and refuses device, UNC and stream spellings.
- A refused source or bad command line exits with code 2 and a message on stderr.
- A failed copy leaves an empty Board with the notice "The snapshot could not be taken: <reason>".

**Measurement (#399)**
- Paint rows are separate rows against 250 ms.
- The search keystroke renders at once, not after the 200 ms wait.
- The scroll moves a page at a time; the open list is opened for real and stepped by `SelectedIndex`.
- Extra rows: "switch back to Pipeline", and in a real window, process start to Board.
- The probe runs at Input priority; `CardView.MadeCount` counts card views made per row.

## 10. Open decisions for the maintainer

For the maintainer: please choose an option for each item below.

1. **Rows of N as the default.** Options: keep it; go back to every card; decide after the Narrator pass. Recommendation: keep it now and confirm after the Narrator checks of section 6. Every card went over all four switch rows in the real window, measured after the choice was made (section 4.3).
2. **The every-card build (`-p:ShelfOption=a`).** Options: keep it selectable; remove it. Recommendation: remove it once rows of N is confirmed. CI no longer builds or tests it, so it will drift.
3. **Which reading the budget verdicts rest on.** Options: the real window (the design's switch rule; two rows over); headless (four rows waiting); CI × 3. Recommendation: verdicts on the real window, headless as the guard in CI. This also settles the "500 library" item of f (0 or 5).
4. **The two rows over in a real window.** Options: make the card view cheaper; port today's fill strategy (Published prepared while idle, first screen only), which design 4.4 and plan :428 said not to port; defer the hidden shelf's updates; accept and change the budget. Recommendation: make the card view cheaper first. The per-card cost is the root of every miss, and it would help every card-heavy page.
5. **How k is read, and H.** Options: k from agent wall-clock hours only; k from the maintainer's hours once logged; both. Recommendation: both, as the design says, after the maintainer states H and logs review hours on #160. Do not use the agent-hour k as a calendar forecast. The checklist weights of section 2.3 stayed as the design fixed them before work started; changing them now would let them follow the result.
6. **`--snapshot` of the real library (step 14).** Options: build it; drop it from the trial. The evidence supports neither strongly. `--snapshot-from` was enough for the real-window run.
7. **Linux.** Options: find a Linux machine or use the VirtualBox guest for the steps of section 7; wait until a later unit. Also issue #395's three options for a missing Documents folder (do not verify and create on first use; fall back to the home folder; ask at first run).
8. **Dark mode's primary text `#F0F0F0`, and the system accent for search selection.** Options: accept; pick other values.
9. **The two #383 rule departures** (zero-count chip, missing target journal). Recommendation: accept. They match today's app, which the port is meant to keep.
10. **Contrast for Fluent's menus, scroll bars, tooltips and focus visuals.** Options: size control themes in unit 0; accept Fluent's palette there. Recommendation: size them in unit 0, as design section 9 foresaw; the real-theme look (section 5.3) will show how bad they are.
11. **The VB spike's place.** It is a folder outside the repository, not a scratch branch. Options: leave it; put it on a scratch branch for the record.
12. **The budget rows on a busy PC.** The MSTest pass mark itself is met (ten local runs in a row and a Debug run, section 3.3). But two full local runs failed one enforced budget row each while the PC was busy. Options: accept it, as today's Windows Forms budget tests behave the same way; or give the enforced rows a local margin. Recommendation: accept it, and say in the test's failure message that the PC must be quiet.

## 11. Not done, and not checked

Not done:
- the contrast look on a real Windows theme (step 11);
- the Narrator pass (step 17);
- `--snapshot` (step 14), `--platform-check` and the Linux checklist (step 15);
- any real Linux start, publish or AppImage;
- the card menu actions and the File Drawer panel (outside the trial);
- a cheaper card view, idle preparation of Published, deferred updates of a hidden shelf.

Not checked or not measured:
- the maintainer's hours (none logged), the orchestrating session's hours, CI time, and the hours of 17c56e4;
- the Windows Forms app in a real window, or on 2026-11-20;
- whether Avalonia reports a Windows contrast theme, and whether Input-priority probe ticks match `WM_TIMER`;
- the Ubuntu count of 132 for run 44681774261, taken from the notes.

The real-window JSON files stay on the development PC and are not committed, because they hold the computer's name.
