# Board trial report

Draft of November 20, 2026, brought up to date on November 21, 2026 with the two pieces of work merged since: the platform check mode and the Linux checklist (step 15, #402), and the first change for a cheaper card (#403). Four parts wait for a person: the look of the Board on a real Windows contrast theme (step 11), the Narrator pass (step 17), the Linux checklist on a real Ubuntu machine with the first real runs of `--platform-check`, and the maintainer's own hours, which the second k needs. Section 10 lists the decisions that wait for the maintainer.

## Summary

- **What was built.** The Board of PaperRoute Tracker, ported to Avalonia 12.1.6 in C# over the unchanged Visual Basic core, beside the Windows Forms app. It has shelves and cards, search, the stage and sort boxes, the Needs Attention chips, arrow keys, Light, Dark and high contrast, a read-only copy of a library folder (`--snapshot-from`) and a measuring mode (`--measure`). Since the first draft it also has a platform check mode (`--platform-check`) with a checklist for one person on Ubuntu (#402), and a tag strip that keeps and refills its chips (#403). 236 headless tests pass on Windows and on Ubuntu (CI run 44925677319, #403's head f51b5e2, 2026-11-21); at the first draft it was 181 (CI run 44703237752).
- **Finding 1.** The C# interface over the VB core works. Compiled bindings reach VB core types, and no build error that crossed the VB/C# line is recorded in any trial pull request or in the notes.
- **Finding 2.** A card view is expensive: about 14 ms each for the first cards a process shows, and about 7 ms once warm (section 4.5). Drawing every card misses the budgets by far. Any long list in the full port must be virtualized from the start. The first change aimed at the card, a tag strip that keeps its chips (#403), lowered the search keystroke in every paired run on 2026-11-21, but it did not make a card cheaper per card: the work moved from rebinding to layout (section 4.6).
- **Finding 3.** The hours are small against the plan: 6.3 recorded hours in the push, Board work about 3.5 hours. (The 4.8 hours recorded for #402 and #403 on 2026-11-21 are listed apart, section 2.4.) But they are wall-clock hours, nearly all of unattended agent sessions, not a person's working hours. So k (0.06 to 0.20; k is the factor that scales the plan's unit sizes, section 2.3) describes this way of working. It is not a forecast of calendar time.
- **Finding 4.** Avalonia needed help in places the design did not expect: the open-list keys of the boxes, Tab between cards under rows of N, and reuse of virtualized rows. As the design foresaw, Fluent's menus, scroll bars and tooltips also need control themes for contrast.
- **Choice made.** Rows of N is the default. (Rows of N: the shelf makes only the rows of cards on screen and reuses them as you scroll.) It was chosen at step 13 from the headless numbers. The real-window run, taken afterwards, confirms it: drawing every card went over the budget on all four rows that the design's rule uses to decide (section 4.3).
- **Finding 5.** Timings taken on a busy PC can mislead. The first gain reported for the tag strip change was taken while other work loaded the PC, with the two builds measured at different times. A reviewer could not reproduce it. Only runs of the two builds in turn, on a quiet PC, separated the code from the load (section 8).
- **What waits.** Two budget rows are still over in a real window (first switch to Published, search keystroke); the report-only row, process start to Board (2,055 ms on 2026-11-20), is also over 1,500. On 2026-11-21, with the tag strip change and the PC in use, the search keystroke was 93 ms against 50 and the first switch 305 ms against 100 (section 4.6). The contrast look, the Narrator pass, the maintainer's hours, the Linux checklist on a real machine and the first real runs of `--platform-check` all need a person.

## 1. What the trial built

Steps are those of design section 8. Every pull request named here is merged, except this report's own (#400, a draft). #399 (steps 11 to 13) merged on November 20, 2026, after this report's numbers were taken from its head, 44d62e0. On November 21, 2026, #402 (step 15) merged as 294a120, and #403 (the first change for a cheaper card, work after step 13 that design section 8 did not list) merged after it.

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
| after 13 | A cheaper card: change 1 of section 4.5, and tests that hold the accessibility tree, the Tab order and each reused card's tags | Built: the tag strip keeps and refills its chips. One more attempt was tried and dropped, change 2 is written and not built, changes 3 and 4 are not started (section 4.6). | #403 |
| 14 | `--snapshot` of the real library | Not built. The argument is refused with exit code 2. Waits for the maintainer's word. | none |
| 15 | `--platform-check` and the `trial-linux` CI job | CI job done (#398). `--platform-check` and `docs/design/Board-Trial-Linux-Checklist.md` built (#402). The mode has never run with a real dialog and launcher, on Windows or on Linux; the checklist has never been worked through (section 7). | #398, #402 |
| 16 | Spikes | Working copy done. VB `.axaml` done as a throwaway folder outside the repository, never committed. | #393 |
| 17 | Narrator pass | Not done; it is the maintainer's | none |
| 18 | PR C: this report | This draft, brought up to date on 2026-11-21 | #400 (draft) |

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

### 2.4 Hours recorded after the table (2026-11-21)

The table of 2.2 and the figures of 2.3 stay as they were: they are the push's. The hours below were recorded on 2026-11-21 for #402 and #403. B, f and k are not recomputed with them.

Source: `git log` of the two pull requests' branches (`v11/board-trial-platform-check`, `v11/board-trial-card-cost`). Unlike the push's, these trailers name a part, for example "(shell)". The part in the table was still assigned from the changed files (`git show --stat`), as 2.1 says; it agrees with the trailer except where noted.

| Commit | Pull request | Trial-Hours | Part from the changed files | Note |
|---|---|---:|---|---|
| f9fb22e | #402 | 0.8 | shell | the mode: `Program.cs`, `App.axaml.cs`, `TrialOptions.cs`, the `Platform` folders, its tests |
| 3548663 | #402 | 0.3 | none: the Linux checklist alone | 2.1 has no rule for a commit that changes only a document; the trailer says shell |
| 07516d3 | #402 | 1.0 | shell | the review fixes: the mode, its tests and the checklist |
| 1146a42 | #402 | 0.4 | shell | the orchestrating session's (made at 09:39, after wave 9's last agent ended at 09:23) |
| 9ba2ec0 | #403 | 0.5 | board (Board tests) | the tree tests and `CardCostTests`, a measuring probe that could also be read as measure; 2.1 does not split a commit |
| 7f44e0b | #403 | 0.6 | board | `TagStripPanel.cs` and its test |
| 493f9aa | #403 | 0.2 | board (Board tests) | the every-card build's expected trees |
| 09f561a | #403 | 0.7 | unclear | only `SnapshotTests.cs` (the snapshot picture); 2.2 has no snapshot part; the trailer says board |
| 7563db4 | #403 | none | none | a merge of master; its changes are #402's, counted above |
| f51b5e2 | #403 | 0.3 | board (Board tests) | the orchestrating session's (made at 11:31, after wave 8's last agent ended at 11:17) |
| **Total** | | **4.8** | shell 2.2, board 1.6, unassigned 1.0 (3548663 and 09f561a) | 9 commits with a trailer |

Agent time and tokens, from the two workflows' records (2026-11-21):

| Workflow | Agents | Agent minutes | Hours | Tokens | On the clock |
|---|---|---:|---:|---:|---|
| Wave 9, #402 | 1 implementer (48.9 min), 2 reviewers (27.3 and 27.4), 1 fixer (24.4) | 127.9 | 2.13 | 685,573 | 07:42 to 09:23 |
| Wave 8, #403 | 2 implementers in turn (78.2 and 53.6 min), 3 reviewers (50.5, 46.0, 48.7), 1 fixer (35.7) | 312.7 | 5.21 | 1,084,654 | 07:39 to 11:17 |
| **Both** | 10 agents | **440.6** | **7.34** | **1,770,227** | |

The two waves ran at the same time on the same PC. Reviewers are never in a trailer. The second implementer of wave 8 (53.6 minutes) made no commit, because its work was dropped (section 4.6), so its time is in no trailer. Some trailers differ from the agent's duration: f9fb22e and 3548663 together say 1.1 h against the implementer's 0.81 h; 07516d3 says 1.0 h against the fixer's 0.41 h; 09f561a says 0.7 h against the fixer's 0.60 h. The three commits of wave 8's first implementer say 1.3 h against its 1.30 h. The orchestrating session's own time, beyond the 0.7 h of its two commits, and CI time are not measured.

What these hours would change in the calibration. B counts Board hours against the Board checklist of 2.3. Wave 9's hours are shell and Linux work, which 2.3 does not count in B. Wave 8's board hours went to the "500 library within budget" item, which is still not done, since the same rows still wait (section 4.6). So, if they were added, B would grow and f would not, and k would rise. 2.3 does not say whether hours spent after the calibration's figures were taken belong in it; that is part of decision 5.

For the maintainer: when you log hours on #160, include reviewing #402, #403 and this report (#400).

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

**MSTest 4.5.2 with `Avalonia.Headless` 12.1.6 and `Avalonia.Skia` 12.1.6**, the design's first choice. xUnit was never needed. In CI run 44703237752 (head 44d62e0), 181 of 181 pass on Windows and 181 of 181 on Ubuntu, each with 31 pictures. Later runs, read from their uploaded results files: 231 of 231 on each system in run 44915194505 (#402, head 1146a42) and 236 of 236 on each in run 44925677319 (#403, head f51b5e2), each with 31 pictures.

What it needed:
- Tests stay synchronous. Awaiting the dispatcher and then disposing on it deadlocks. `Headless.Run` runs each body in one `Dispatch` call and blocks with a timeout.
- The session is started and disposed from a thread-pool thread (`AssemblySetup.cs`).
- Pictures go to a folder named by `PAPERROUTE_TRIAL_PICTURES`, because MSTest does not tell a test where `--results-directory` points.
- `.axaml` files are linked as `Content`, because Avalonia's build removes every `None` item named `*.axaml`.

Pass mark of design 6.1: met. The Release run and CI pass; all four checks of run 44703237752 passed. Ten full local Release runs in a row passed, 181 of 181 each time (the development PC, quiet, 2026-11-20 04:15 to 04:43, 161 to 176 seconds a run, the tree of 44d62e0), and so did one Debug run of the same 181 tests (04:43 to 04:46). The harness hours are not separate from the other trailers.

**The enforced budget rows can fail on a busy PC.** Locally in Release they are held at the budget with no multiplier, as today's `ResponsivenessBudgetTests` are. On 2026-11-20, every full local run on the quiet development PC passed (two at 03:25 and 03:30, and the ten above). Two full runs made between them, while other agent work was using the PC, each failed one enforced row: back to All stages at 257 ms against 250 (the run that ended at 04:03), and the open list at 329 ms against 250 (the run that ended at 04:08). Each time the budget tests passed when run alone a minute later. Those two runs were on core stage 2 branches merged with master, so they also had a different core. A third full run, on the branch that holds all of core stage 2 and made once the other work had ended, passed all 181 (it ended at 04:11). So the cause was the busy PC, not the core. CI holds the rows at three times the budget and passed with room (section 4.1).

The same was seen on 2026-11-21. #402's local run failed one enforced row (back to All stages, 290 ms against 250) while another build and test run was using the PC (#402's description). In wave 8 a reviewer saw the two enforced rows fail at 265 to 379 ms under heavy load, and every run of them on a quiet PC passed, ten for each build (the fixer's record). The full local suite then passed 236 of 236 in Release after the merge of master, timing rows included (the development PC, 2026-11-21 11:27 to 11:30, the tree committed as f51b5e2), and so did the every-card build in Debug (`-p:ShelfOption=a`), 236 of 236 (11:37 to 11:41).

### 3.4 The working-copy spike

**It fits.** Today's way of editing a record (original, working clone from `ManuscriptCloneService`, baseline JSON snapshot, as `EditManuscriptForm` does) works under a bound view model with no core change. Three tests pass (#393).

What bends:
- A bound box writes into the working clone at every keystroke, not at Save. Save's trimming, the blank-title rule, the journal lookup and finishing a stage change need a new place in each edit-window unit.
- The #358 rule that protects typed text from a workflow step's refresh has no direct counterpart. It needs a decision per unit.
- Each unsaved-changes check takes a full snapshot: 40 to 65 µs for the largest records, measured in a loop, not as typing latency.

Limits: only Title was tried. Not tried: the other fields, saving, a recovery draft, discard, and a new baseline after a save. The test types the old value back instead of using an undo, as the design wrote.

### 3.5 Does it start on Linux

**It builds and its headless tests pass on Ubuntu. Nothing has started a window on a real Linux desktop.** Since 2026-11-21 the tools for that start exist: `--platform-check` and a checklist for one person on Ubuntu 24.04 (#402). Neither has been used on a real machine. See section 7.

### 3.6 Large library against today's budget

See section 4.

## 4. The 500 library against today's budgets

### 4.1 The comparison

This section's tables are the record of 2026-11-20 (the Windows Forms column excepted, which is of 2026-11-14). The figures of 2026-11-21, before and after the tag strip change, are in section 4.6. Its real-window figures were taken with the PC in use. Neither of its tables is compared with these.

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

The every-card build stays selectable (`-p:ShelfOption=a`). CI builds and tests only rows of N. Every card passed 178 of 178 locally in Debug at #399, and 236 of 236 on 2026-11-21 after #403's merge of master (section 3.3). Since #403 it has its own expected accessibility trees, because it draws every card.

### 4.4 The rows that still wait

The waiting list is compiled into the build (`BoardMeasurement.s_waitingUnderRows`) from the headless runs. It is not a real-window verdict.

| Row | Headless | Real window | CI Windows (× 3) |
|---|---|---|---|
| First switch to Published | over (383 / 100) | over (157 / 100) | over (304 / 300) |
| Search keystroke | over (208 / 50) | over (99 / 50) | over (170 / 150) |
| Stage filter to Submitted | 240 / 250, over in 1 of 5 runs | within (128) | within |
| Published scroll | over (288 / 250) | within (147) | within |

The cause is the cost of a card view: about 3 ms to make and about 12 ms to attach and lay out the first time (#399), about 15 ms in all. The code comment beside the same headless numbers says about 16 ms each (`PaperRoute.Desktop/Measurement/BoardMeasurement.cs:105`). Even the 21 views on one screen exceed a 100 ms switch and a 50 ms keystroke. Section 4.5 shows that these are figures for the first cards of a process; warm, a card costs about 7 ms.

What could bring them in (from #399 and the workflow records). None was built when this list was first written; the state on 2026-11-21 is in brackets, and section 4.6 has the details:
- make a card view cheaper: a flatter tree, fewer bindings, menus built later [one part built: the tag strip keeps its chips (#403); the lighter buttons and the route dots as one control are not started];
- prepare the hidden Published shelf while idle, as today's app does [not built];
- draw only the first screen at startup, as today's app does [not built];
- defer the hidden shelf's card updates until it is shown (today a shelf shown once rebinds its 21 views on every search or stage change) [written with tests, not built or run];
- for the scroll, cheaper reuse of card views (inferred; no source names a fix) [not built; the tag strip change did not make reuse cheaper per card].

One more was tried on 2026-11-21 and dropped: making fewer rows at a shelf's first show (section 4.6).

The second and third items are how today's app fills the Board while it is idle, which design 4.4 and plan :428 said would not be ported. Section 4.5 measures where a card's cost sits and which of these changes the numbers support.

For the maintainer: say whether the budget verdicts rest on the real window (the design's rule for the switch) or on headless (the stricter guard in CI).

### 4.5 Where a card's cost sits

Measured after the first draft of this report, on 2026-11-20 between 04:45 and 05:05, on master (fb3301f). Three scratch probes, none committed. The first removed one part of the real card at a time. The second built each part alone. The third re-ran the main claims with other cards, another order and twice as many cards. All are headless, Release, on the development PC, and time layout only (nothing is drawn). Figures are milliseconds per card, medians of 12 rounds.

**The "about 15 ms" of section 4.4 is a cold figure.** The very first card a process shows costs about 400 ms: the first use of the runtime's compiler, of the styles and of the templates. The next 19 cost about 14 ms each (the median of two records; single cards from 9 to 53). After a warm-up a full card costs about 7 ms to show the first time (6.9 and 7.0 on two sets of cards; the three probes agree within 0.4 ms). Giving a made view a new card and laying it out again costs about 2 ms.

| Part (warm, first show) | ms per card | How it was measured |
|---|---|---|
| The three buttons: title, ⋯, View route | 2.1 to 2.2 when removed; 1.1 to 1.3 when swapped for a plain border and text | removal, two sets of cards; the swap in a card built in code |
| The journal line with the tag strip | 1.5 to 2.0 | removal |
| The tag strip alone (part of the line above) | 1.1 to 1.35 | removal |
| The route dots | 0.9 to 1.1; not separable from noise on one of the two sets | removal |
| Everything else: pill, status, grids, borders, the focus ring | about 2, not broken down | what is left |
| Loading the XAML and binding | 0.4 to 0.5 | the card against the same tree built in code |

Not separable from noise (about 0.3 ms either way on a median): the tooltips, the accessible names, the class bindings with converters, the two empty menus, the focus ring's binding, style matching, and the length of the title.

**Reuse**, which is what a scroll and a filter pay under rows of N, costs 1.9 to 2.3 ms per view. About half of it is the tag strip: without it, giving a view a new card falls from about 1.25 ms to 0.30 ms. The buttons and the dots add nothing measurable to reuse.

**What this means for the two rows still over.**
- *First switch to Published.* It makes about 21 card views. Even warm, 21 views at about 7 ms are about 147 ms in these headless probes, against a budget of 100. So a cheaper card alone will probably not bring this row in. It also needs fewer views made at the switch, views made earlier, or a different budget. How much of the row is first-use cost, and how much is the shelf's own first show, was not measured.
- *Search keystroke.* The views of the shown shelf get new cards, and so do those of a hidden shelf that was shown before. At about 2 ms per reused view that is about 40 ms for each shelf of 21 views in these probes. The tag strip and the hidden shelf are the two targets for this row. These are estimates from the per-view figures, not measurements of the row.

**Changes the numbers support, in order.** None was built when this was written (2026-11-20); the state on 2026-11-21 is in brackets (section 4.6). Every saving below comes from removing a part, so it is the most a change could save. A real replacement must keep the keyboard focus, the accessible roles, the commands and the styles, and will save less.
1. Rework the tag strip's update: keep and refill its chips, or skip the rebuild when the tags are the same. Up to about 1 ms per reused view. [Built (#403). Per card it saved nothing measurable.]
2. Do not give a hidden shelf's views new cards until the shelf is shown. [Written with tests; not built, run or measured.]
3. One lighter control for the title, ⋯ and View route, in place of three Fluent buttons. Up to 2.2 ms per first show; 1.1 to 1.3 measured for a plain swap. [Not started.]
4. Draw the route dots as one small control. Up to about 1 ms per first show. [Not started. It would take unnamed elements out of the accessibility tree; decision 13.]
5. Pay the first-use costs before the first switch, for example by compiling ahead of time (ReadyToRun) or by making one hidden card at startup. The first cards cost about twice the warm ones. A warm-up moves that cost earlier; it does not remove it. [Not started.]

Not measured: drawing, and a real window; the card inside the real shelf; each part's share of the cold cost; any real replacement; how many cards it takes to get from the cold figure to the warm one (the probes warmed up on several hundred).

Sources: the probes' source, raw numbers and tables, in `TestResults/v11-trial-pictures/card-cost-profile-2026-11-20` on the development PC, not in the repository.

### 4.6 The first change for a cheaper card, measured (2026-11-21)

**What changed (#403, commit 7f44e0b).** When rows of N give a made card view another card, `TagStripPanel` now keeps the chips it has made and refills their text and colour class. It hides the chips a shorter list does not need, and touches nothing when the new tags read and colour as the old ones. Before, it threw every chip away and made new ones. Nothing a person sees or hears changes:
- the automation tree and Tab order of the example Board and of the 500 library's first screen match texts made on master before the change (`AccessibilityTreeTests`; the every-card build has its own texts); a reviewer's longer record (tooltips, off-screen flags, keys before and after a scroll, a first switch and a search by tag) was the same bytes before and after;
- 29 of the suite's 31 pictures are the same bytes as master's. The snapshot picture differs in its banner only, which now names a fixed folder and clock so it is the same on every run (09f561a). `board-500-chip-light.png` differs by at most 8 levels of a colour, because a 100 ms colour transition is caught at different moments; the same build gave both versions;
- every card laid out on the 500 library draws its own tags after fourteen changes (scrolls, the stage filter, sorts, a search by tag, the other shelf), in which views were given a card with other tags more than two hundred times (f51b5e2).

**Real window.** The development PC, 2026-11-21 11:32 to 11:35, Release, DPI 120 (125%), the 500 test library through `--snapshot-from`, rows of N. Five pairs, the order alternating. "Before" is master's build at 294a120 (its Board code is that of fb3301f); "after" is #403's head f51b5e2. UI ms, median (lowest to highest). Source: `TestResults/v11-trial-pictures/real-window-2026-11-21/summary.md` and its ten JSON files, on the development PC, not in the repository.

| Row | Budget | Before | After | After lower in |
|---|---:|---:|---:|---|
| Search keystroke | 50 | **137** (123 to 178) | **93** (72 to 110) | 5 of 5 pairs |
| Stage filter to Submitted | 250 | 185 (174 to 202) | 131 (74 to 173) | 5 of 5 |
| Open list stepped and closed | 250 | 160 (136 to 183) | 121 (85 to 156) | 5 of 5 |
| Back to All stages | 250 | 107 (96 to 117) | 77 (73 to 106) | 4 of 5 |
| First switch to Published | 100 | **319** (227 to 386) | **305** (167 to 432) | 2 of 5 |
| Published scroll, longest step | 250 | 183 (142 to 229) | 184 (165 to 233) | 2 of 5 |
| Warm startup | 1,500 | 1,065 (861 to 1,165) | 1,082 (815 to 1,215) | 2 of 5 |
| Process start to Board (report-only) | 1,500 | **2,699** (2,120 to 3,116) | **2,630** (2,141 to 3,027) | 3 of 5 |

Bold: over budget at the median. Paint rows were 10 to 18 ms at the median in both builds.

**This table is not compared with section 4.1.** Other programs were running on the PC (its processors 14 to 18% busy before each run). So the same Board code measured higher than in the night run of 2026-11-20: the search keystroke 137 ms here against 99 then, the first switch 319 against 157. Only before and after of the same day are compared.

**Headless.** Wave 8's fixer, the development PC quiet, 2026-11-21 10:48 to 10:52 and 11:01 to 11:05; Release; the 500 library; the budget test alone, each run its own process; the two builds run in turn, ten runs each. "Before" is fb3301f; "after" is 09f561a's tree, whose program is that of 7f44e0b. UI ms, median (lowest to highest). Source: the fixer's record in the wave's workflow journal, and #403's description.

| Row | Budget | Before | After | After lower in |
|---|---:|---:|---:|---|
| Search keystroke | 50 | **146** (121 to 205) | **85** (74 to 138) | 10 of 10 pairs; ranges overlap |
| Back to All stages | 250 | 119 (94 to 146) | 76 (61 to 102) | 9 of 10 |
| Open list stepped and closed | 250 | 115 (84 to 174) | 71.5 (65 to 126) | 9 of 10 |
| Stage filter to Submitted | 250 | 172.5 (135 to 232) | 141 (106 to 236) | 5 of 10; not separable |
| First switch to Published | 100 | **263** (226 to 318) | **269.5** (222 to 442) | 7 of 10; not separable |
| Published scroll, longest step | 250 | **276** (227 to 452) | **271** (243 to 487) | 4 of 10; not separable |
| Warm startup | 1,500 | 316 (288 to 350) | 335 (270 to 396) | 5 of 10; not separable |

These headless figures are of 2026-11-21 and are not compared with the headless column of section 4.1 (2026-11-20). 21 card views were made at the first switch in every run of both builds.

**Per card, the change saved nothing measurable.** Reuse costs the same before and after: Published 1.41 ms against 1.48, Pipeline 1.74 against 1.54 (`CardCostTests`, four runs of six rounds, the two builds in turn, the fixer's record). Giving the view its card got cheaper (Published 0.92 to 0.65 ms) and laying it out got dearer (0.49 to 0.80). So the work moved from rebinding to layout. Section 4.5 put change 1 at "up to about 1 ms per reused view"; that was a removal figure, and the real change saved none of it per card. Why the rows above fall when the per-card figure does not was not measured.

**The budgets.** No budget, no assertion and no entry of the waiting list changed. The four waiting rows still wait: the search keystroke and the first switch are over in both tables, and the stage filter and the scroll fail the wave's rule for taking a row off the waiting list, that every headless run be at or under 80% of the budget, here 200 ms (the stage filter had one run at 236; every scroll run was above 200; the fixer's record).

**Tried in wave 8 and not taken** (#403's description and the workflow's records):
- **Fewer rows made at a first show.** The list's margin started at none and grew after the first frame. It made 15 card views at the first switch in place of 21. It was dropped for four reasons: the scroll bar's thumb was 3 pixels shorter in the narrow picture; a shelf hidden again at once made the rest of its margin inside its next switch, so the cost moved instead of going away; it read four private members of Avalonia by reflection; and it changed how the row is timed as well as what is timed, so its gain was not shown (a reviewer's headless runs, five of each, one after another while the load on the PC varied: first switch median 397 ms with it, 453 with only its change to the timing, 640 without either; the ranges overlap). Its tree test also checked the tree after idle frames, not as first shown. It stays as a patch on the development PC.
- **A hidden shelf waits for its rows** (section 4.5, change 2): written, with tests, but not built, run or measured, for lack of time. The draft stays on the development PC.
- **A lighter look for the three buttons** (change 3): not started.
- **The route dots as one control** (change 4): not started. It would take five unnamed elements a card out of the accessibility tree, and the wave's rule was that the tree does not change (decision 13).

**What this means for the two rows still over.**
- *Search keystroke.* One change is built and lowered it: in 5 of 5 real-window pairs and 10 of 10 headless pairs, with the ranges overlapping. It is still over: 93 ms against 50 in the real window, 85 headless. One more change for this row (change 2, the hidden shelf) is written and not built.
- *First switch to Published.* Nothing built changes it: 2 of 5 real-window pairs and 7 of 10 headless pairs, not separable from noise. The one attempt aimed at it (fewer rows at a first show) was dropped, and its gain was not shown.

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

Seen headless by wave 8's keyboard reviewer on 2026-11-21, the same before and after #403, and for a person to judge in a real window (the workflow's record; #403's description):
- **Focus on a hidden shelf.** When a card's title has the keyboard focus and its shelf is hidden, the focus is lost. It does not come back when the shelf is shown again, and the next Right arrow goes to the rail's Board item.
- **The ⋯ menu.** Opened with Enter or Space, the menu puts the focus on the rail's Board item, and Esc leaves the menu open. This is probably a limit of headless popups; only a real window can tell.
- **Off screen.** The automation tree reports every element as on screen (`IsOffscreen` is false), the rows made below the visible part of the shelf included. What Narrator does with those rows is part of the card-count check above.

Unproven: Avalonia's list accessibility across virtualization. Avalonia pull request #20733 (item count, focus across virtualization) was closed without merging (r2 5.1).

## 7. Linux

**What CI shows.** Job `trial-linux`, "Avalonia Board trial (Linux, report-only)", on ubuntu-latest (#398). The Release build must succeed. Tests are report-only: failures are listed and the job stays green.

| Run | Pull request | Tests | Pictures |
|---|---|---|---|
| 44681774261 | #398 (first run) | 132 of 132 (from the push notes; not downloaded again) | 17; `board-light.png` looked the same as on Windows |
| 44703237752 | #399, head 44d62e0 | 181 of 181 | 31 |
| 44915194505 | #402, head 1146a42 | 231 of 231 (from the uploaded results file) | 31 |
| 44925677319 | #403, head f51b5e2 | 236 of 236 (from the uploaded results file) | 31 |

In run 44925677319 the two accessibility tree tests, whose expected texts were made on Windows, pass on Ubuntu too. "Core on Linux" passed. In run 44915194505 the three tests that start a real second program (a second copy run to its exit code, a program stopped at its limit, a missing program file) passed on both systems.

Found on the way:
- The first path fix accepted only drive-letter paths, so `--snapshot-from` and `--measure` refused every folder on Linux. 44d62e0 accepts a path from the root off Windows.
- Refusal messages show a Windows example path on every system.

Related, found by core stage 2 (#388): issue #395 (open): with no `~/Documents`, as on a CI runner, `ManagedLibraryService` throws. Three options wait for the maintainer.

**The platform check mode (#402, merged 2026-11-21).** `--platform-check <file>.json` opens the trial's window on the example library and tries three things in turn:
1. the system's file dialog, once (nothing chosen is opened or read);
2. the system's launcher, once, on `https://example.org`;
3. a restart: a second copy started with `--self-check`, waited for up to two minutes and stopped after that.

Each outcome goes to the JSON with the facts needed to judge it (the trial's version and commit, the system, whether it runs from an AppImage, the Linux session type, the scaling and the theme). Exit code 0 means the check ran to its end, whatever each outcome was; 1 means the check itself broke or its window was closed early; 2 means it was refused before anything opened. Closing the window early is the way out of a dialog or browser that never comes back. The mode goes with none of `--self-check`, `--measure` or `--snapshot-from`. The JSON keeps only the number of items chosen in the dialog, records `APPIMAGE`, `WAYLAND_DISPLAY` and `DISPLAY` only as set or not set, and scrubs other messages of folders and names.

**The checklist (#402).** `docs/design/Board-Trial-Linux-Checklist.md` is for one person on Ubuntu 24.04, in parts A to G with a result sheet: A, get the trial onto the machine (publish for `linux-x64` on the Windows PC, copy it, check that `libfuse2` is not installed); B, start it from the folder and look at the Board; C, `--self-check`; D, `--snapshot-from` on a copy of a test library; E, `--platform-check` and what every result means; F, the same from an AppImage; G, what to send back. Every command that could not be tried on the development PC is marked "not tried here". The `linux-x64` publish was tried there (2026-11-21, commit f9fb22e) and its result is recorded.

**What has never been run.**
- The mode itself, for real, on Windows or on Linux. The real dialog and the real launcher open things on a person's screen, so no test and no agent ran them; the tests use stand-ins. The restart's start of a second program opens nothing, so three tests run it for real (above).
- The app's own branch for the mode in `App.axaml.cs` (the check starting when the window opens, the exit code passed on, the early close): read, not run.
- Every checklist command marked "not tried here", and all of part F (the AppImage).

Known and left (#402's description): stopping a second copy at its limit waits up to five seconds on the interface thread, and a stopped copy leaves its own example folder in the temporary folder; whether the AppImage runtime sets `APPDIR` under `--appimage-extract-and-run` was not checked (the checklist's F5 asks the person to note what the restart reports); the JSON says `milliseconds` where `--measure` says `Ms`.

**What a real machine must still show** is now the checklist's parts A to G: the published trial starting from its folder and from an AppImage on a machine without `libfuse2`, the example library open, `--snapshot-from` over a copied library, and the three outcomes of `--platform-check` written to JSON.

For the maintainer: a real Linux start needs a person at a Linux machine or the VirtualBox guest, working through the checklist and sending back its result sheet. Agents do not touch the virtual machine. The first real run of `--platform-check` on Windows is also a person's.

## 8. What was harder than the plan thought

**Cards and lists**
- A card view costs about 14 ms for the first cards of a process and about 7 ms warm (section 4.5). Give the card view a cost budget early, before other card-heavy pages are built.
- Drawing every card was not enough; rows of N became the default. Plan virtualization from the start for any list that can reach hundreds of items.
- In Avalonia 12.1.6 an `ItemsControl` with a row `ItemTemplate`, as the design specified, rebuilt a row's card views every time the row was reused. The fix makes the row view itself the container.
- No test failed at first when a reused card kept the previous manuscript's title. Three "drawn wrong after reuse" tests were added and seen to fail with the binding broken. Every virtualized template needs such a test.
- Rows of N needs every card the same height, so the journal row is held to one tag chip tall.
- A hidden shelf shown once still rebinds its card views on each render.
- A removal measurement gives the most a change could save, not what it will save. Keeping the tag strip's chips saved nothing per card: the work moved from rebinding to layout (section 4.6).
- In Avalonia 12.1.6 changing a virtualizing panel's cache length alone makes no new rows; the panel works out its margin only when its viewport moves. Making fewer rows at a first show and the rest later needed four private members of Avalonia read by reflection, and was dropped (section 4.6).

**Keyboard**
- Avalonia's `ComboBox` does not select as the arrows move through the open list, and Esc forgets the arrowed item. `ArrowChoiceKeeper` gives Windows' behaviour. The design's fallback, "adjust only the binding", was not enough.
- `ItemsControl` handles arrow, Home, End and page keys itself. Over a virtualizing panel, Ctrl+Down scrolled to the end while focus stayed put. The row list now leaves those keys to the shelf.
- Tab out of the shelf cannot be computed: `KeyboardNavigationHandler.GetNext` is internal, and `FocusManager.FindNextElement` returned the card's own View route. It is left to Avalonia. Custom Tab routing in the full port needs a public API or its own focus-order walk.
- Focus into a row not made yet works headless, but nobody has checked it with a real keyboard or mouse wheel.

**Measurement**
- The interface-thread probe first ran at Normal priority. It ticked between layout and render jobs and hid about half the stall. It now runs at Input priority, with a test. Whether this matches `WM_TIMER` on real Windows is not confirmed. The measuring tool needs tests of its own.
- By the run's rule, agents did not open a window, so the orchestrating session took the real-window run afterwards.
- Numbers taken on a loaded PC overstated a gain (wave 8, 2026-11-21). The implementers' first figures for the tag strip change were taken while other work loaded the PC, with the two builds measured at different times: the search keystroke "511 to 288 ms, with ranges that do not overlap", and a gain per card. A reviewer could not reproduce them. Only runs of the two builds in turn, on a quiet PC, separated the code from the load: 146 to 85 ms, lower in 10 of 10 pairs, with the ranges overlapping, and no gain per card. 7f44e0b's commit message still quotes the withdrawn figures; 09f561a's message corrects them. Every before-and-after timing should be taken this way: both builds, in turn, in one sitting, on a quiet PC.
- The real-window runs of the two days differ for the same code (search keystroke 99 ms on 2026-11-20, 137 on 2026-11-21, with the PC in use). Only figures of one sitting can be compared.

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
2. **The every-card build (`-p:ShelfOption=a`).** Options: keep it selectable; remove it. Recommendation: remove it once rows of N is confirmed. CI no longer builds or tests it, so it will drift. Since #403 it also has its own expected accessibility trees to keep up to date; it passed 236 of 236 locally on 2026-11-21.
3. **Which reading the budget verdicts rest on.** Options: the real window (the design's switch rule; two rows over); headless (four rows waiting); CI × 3. Recommendation: verdicts on the real window, headless as the guard in CI. This also settles the "500 library" item of f (0 or 5).
4. **The two rows over in a real window.** Done so far (section 4.6): the tag strip keeps its chips (#403, built); fewer rows at a first show (tried and dropped); a hidden shelf that waits for its rows (written with tests, not built); a lighter title and buttons, and the route dots as one control (not started). The two rows now stand apart.
   - *Search keystroke* (on 2026-11-21, 93 ms against 50 in a real window, 85 headless, after the tag strip change lowered it in every pair). Options: build and measure the written hidden-shelf change; make the card lighter (buttons, dots); accept and change the budget. Recommendation: build and measure the hidden-shelf change next, both builds in turn on a quiet PC, because it is written and aims at this row (section 4.5). Whether it brings the row under 50 is not known.
   - *First switch to Published* (on 2026-11-21, 305 ms against 100 in a real window; nothing built has changed it, and the one attempt was dropped). Options: make the card lighter (a cheaper card alone will probably not be enough, section 4.5); make fewer views at the switch by another way than the dropped one; make views earlier, which is today's fill strategy (Published prepared while idle, first screen only) that design 4.4 and plan :428 said not to port; pay the first-use costs earlier (section 4.5, change 5); accept and change the budget. No recommendation: no change tried so far has moved this row, and the choice between porting today's fill strategy and a new budget is the maintainer's.
5. **How k is read, and H.** Options: k from agent wall-clock hours only; k from the maintainer's hours once logged; both. Recommendation: both, as the design says, after the maintainer states H and logs review hours on #160. Do not use the agent-hour k as a calendar forecast. The checklist weights of section 2.3 stayed as the design fixed them before work started; changing them now would let them follow the result. Also open: whether the hours recorded after the calibration's figures (section 2.4, 4.8 h) belong in B. Section 2.3 does not say, and this report leaves them out.
6. **`--snapshot` of the real library (step 14).** Options: build it; drop it from the trial. The evidence supports neither strongly. `--snapshot-from` was enough for the real-window run.
7. **Linux.** The mode and the checklist exist (#402); what is open is the machine and the person. Options: find a Linux machine or use the VirtualBox guest and work through the checklist of section 7; wait until a later unit. Also issue #395's three options for a missing Documents folder (do not verify and create on first use; fall back to the home folder; ask at first run).
8. **Dark mode's primary text `#F0F0F0`, and the system accent for search selection.** Options: accept; pick other values.
9. **The two #383 rule departures** (zero-count chip, missing target journal). Recommendation: accept. They match today's app, which the port is meant to keep.
10. **Contrast for Fluent's menus, scroll bars, tooltips and focus visuals.** Options: size control themes in unit 0; accept Fluent's palette there. Recommendation: size them in unit 0, as design section 9 foresaw; the real-theme look (section 5.3) will show how bad they are.
11. **The VB spike's place.** It is a folder outside the repository, not a scratch branch. Options: leave it; put it on a scratch branch for the record.
12. **The budget rows on a busy PC.** The MSTest pass mark itself is met (ten local runs in a row and a Debug run, section 3.3). But two full local runs failed one enforced budget row each while the PC was busy. Options: accept it, as today's Windows Forms budget tests behave the same way; or give the enforced rows a local margin. Recommendation: accept it, and say in the test's failure message that the PC must be quiet. The same was seen again on 2026-11-21, and every quiet run passed (section 3.3).
13. **May a cheaper card change the accessibility tree where only unnamed elements go?** Wave 8 kept the tree unchanged as a rule, so the route dots as one control (section 4.5, change 4) was not started: it would take five unnamed elements a card out of the tree. Options: allow such a change, with the expected tree texts made again and the difference reviewed; keep the tree unchanged for every cheaper-card change. No recommendation: the saving is small (up to about 1 ms per first show, by removal, and not separable from noise on one of two sets of cards), and what Narrator says about the dots today has never been heard (section 6).

## 11. Not done, and not checked

Not done:
- the contrast look on a real Windows theme (step 11);
- the Narrator pass (step 17);
- `--snapshot` (step 14);
- any real Linux start or AppImage (the `linux-x64` publish was made only on the development PC, for the checklist's part A);
- the card menu actions and the File Drawer panel (outside the trial);
- a cheaper card view beyond the tag strip: the hidden shelf that waits for its rows (written, not built), the lighter buttons and the route dots as one control (not started), paying the first-use costs earlier (not started), idle preparation of Published (not built).

Built on 2026-11-21 and so moved out of this list: `--platform-check` and the Linux checklist (step 15, #402); the tag strip that keeps its chips (#403).

Never run for real:
- `--platform-check` with a real file dialog and a real launcher, on Windows or on Linux;
- the app's own branch for the mode in `App.axaml.cs` (read, not run);
- every checklist command marked "not tried here", and all of the checklist's part F (the AppImage);
- the hidden-shelf change (section 4.6).

Not checked or not measured:
- the maintainer's hours (none logged), the orchestrating session's hours beyond its trailers, CI time, and the hours of 17c56e4;
- the Windows Forms app in a real window, or on 2026-11-20 or 2026-11-21;
- whether Avalonia reports a Windows contrast theme, and whether Input-priority probe ticks match `WM_TIMER`;
- the Ubuntu count of 132 for run 44681774261, taken from the notes;
- why the rows fall with the tag strip change when the per-card cost does not;
- the first-switch gain of the dropped first-show attempt (not shown);
- the every-card build in a real window on 2026-11-21 (only rows of N was measured that day);
- the per-row timings of CI runs 44915194505 and 44925677319 (only their test and picture counts were read);
- in a real window, the three keyboard observations of section 6.

The real-window JSON files of both days stay on the development PC and are not committed, because they hold the computer's name.
