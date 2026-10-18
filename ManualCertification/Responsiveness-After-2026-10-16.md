# Responsiveness budgets: the after table — 2026-10-16

The same measurements as [the before table](Responsiveness-Before-2026-10-16.md), taken by the same test on the same machine after the second and third responsiveness pull requests ([#132](https://github.com/JUhalt/PaperRoute-Tracker/issues/132) and [#133](https://github.com/JUhalt/PaperRoute-Tracker/issues/133)). Read the before table's "How the numbers were taken" first; only the differences are repeated here.

## What changed between the tables

- **#132.** Backup, Restore and the save, with its sweep of the managed root, run on a worker thread while the window is disabled the way a modal dialog disables its owner (`EnableWindow`, not `Form.Enabled`, which walks every control on a large Board and cost about 3 s each way in the profile) and the status line says what is happening; the UI thread processes messages until the work ends. The Insights figures are worked out on a worker thread and come back through the window's message queue while the page keeps what it shows.
- **#133.** `RenderManuscripts` keeps every card it has built and shows a card again when the text it shows is unchanged, parking cards that leave a shelf; a card's actions look its manuscript up by id when used, so a restore keeps the cards whose text is unchanged. The Library filter refills the grid once typing pauses (a 200 ms timer, as the Board's search), and the grid's rows go in together. The Import & Export page is built once and kept. The dirty-timer tick, within budget before, is unchanged.
- Rows added to the test: the debounced Library refill, a Deadlines filter keystroke, the Insights figures, and second visits to Deadlines and Import & Export. Backup and Restore are now timed through the handlers' seams (`WriteBackup`, `InspectBackupFile`, `ReplaceLibraryFrom`) rather than the services directly; the work timed is the same.
- Enforced from #133 on, on the 500 library, with room to spare on this busy host: the Library filter keystroke, the Board render, Save from the manuscript page and the inspection step of Restore. Backup's stall varied from 61 to 419 ms across runs and stays reported. So does the replace step of Restore, which #133 brought from 13 s to the 50 to 60 ms below, but which measured 95 and 331 ms in two later Debug runs on this host: like Backup it pumps for 30 to 60 s, long enough to meet one of the host's own stalls. Journals to Board, which #133 also brought within budget, stays reported: at 87 to 89 ms against 100 ms it has no room on this host. Manuscript page to Board, also changed by #133, is still over in Release. The example library runs first and carries the process's first-time costs, so its rows stay report-only.
- **2026-10-17, after the merge with #116.** A restore now ends in a restart rather than an in-place reload, so `ReplaceLibraryFrom` is gone, and the test reports the row "Restore: replace the library, reload, render" below as "Restore: replace the library (the window then restarts)": it times the swap and the notice through `RestoreFromArchive`, with the restart recorded rather than run. Its numbers below were measured before the merge and were not taken again. Since c4af8e9 a card's actions act on the card's own record while the library holds it, and look the manuscript up by id only when it does not. The recorded numbers and the budgets are unchanged.
- **2026-10-17, the Deadlines and Import & Export pages (#133, second batch).** Both pages are now drawn by one list each and kept between visits; their rows are enforced. The tables below are from 2026-10-16 and were not changed; the new figures, the host and the reasons are in [the section at the end](#2026-10-17-the-deadlines-and-import--export-pages).

## Host during the after runs

The same machine as the before table, again busy and shared: about 75% CPU load sampled during the runs, with another PaperRoute test host from a sibling worktree running throughout, plus Firefox, OneDrive sync, CrossDeviceService, Malwarebytes and Thunderbird; 14.8 GB of memory free. Variation between runs on this host stayed at tens of percent (Backup's UI stall, for one, ranged from 61 to 419 ms across four runs), so differences smaller than that between the before and after tables are noise.

## Where the time went, and what remains

The profile behind the choices (a scratch test, not kept) measured, on the 500 library with the Board visible: building one card about 3 ms (its context menu 2 ms of that), giving a card its ten window handles about 43 ms, adding 167 cards to the visible shelf 55 to 60 ms each and linear in the count, adding 100 cards to a hidden shelf 41 ms in all, `Form.Enabled = False` 2.9 s and `= True` 1.9 s, the native `EnableWindow` under 1 ms, `EditManuscriptForm`'s constructor 2.6 to 3.1 s of which `LoadManuscript` is 0.75 s, the Deadlines page 0.8 s to build detached and 5.9 s to attach (its 221 rows carry about 1,100 handles; suspending layout made no difference), Import & Export 0.5 s to build and 1.8 s to attach, and a `Rows.AddRange` fill of the Library grid 219 ms against 557 ms one row at a time. Handle creation is the cost this machine pays: about 4 to 5 ms per handle, far above a quiet desktop, with PowerToys, Malwarebytes and a cross-device service running.

What this leaves over budget on the 500 library, and why:

- **Warm startup** builds all 500 cards and gives the 167 Pipeline cards their handles before the Board is interactive; nothing is rebuilt twice any more, but the first build remains. Only a card with fewer controls, or a shelf that creates cards as they scroll into view, would cut it further, and both are beyond the least change this issue allowed.
- **Board to Deadlines**, its filter keystroke and its second visit all rebuild the page's 221 rows, about 1,100 handles. Keeping the rows between fills the way the Board keeps its cards was tried and measured at 2.4 s for a keystroke and 3.1 s for a second visit against 4.6 s: on this machine every window operation, re-parenting included, costs milliseconds, so the change did not reach the budget and was not kept. A list that draws its rows instead of hosting a control per row is the change that would. *Done on 2026-10-17: see the section at the end.*
- **Board to Import & Export (first visit)** is the same handle creation on first build; the second-visit row shows the kept page. *Done on 2026-10-17: see the section at the end.*
- **Board to a manuscript page** is `EditManuscriptForm`'s construction, re-done for each manuscript opened. Keeping an editor for the next manuscript would need the form to load another record safely; that is a change to a 3,000-line form, not taken here.
- **Board to Library** and **Library to Board** build and dispose a 500-row grid; the refill is now a single `AddRange`, and the page switch sits a little over the 100 ms line.
- **Board to Insights** builds the page's tiles, tabs and grid; the figures themselves are off the UI thread.
- The example library's first-visit rows (startup, the first filter keystroke, the manuscript page, Import & Export, Find Journals) are the process's first-time costs: JIT, assembly loading and, for the keystroke, the first text change in the process, which Windows' text services make expensive once. The 500 rows, run second, show the work itself.

## Release build

Verdict: UI against the budget x 1; enforced rows marked.

| Library | Action | Wall ms | UI ms | Budget ms | Verdict |
|---|---|---:|---:|---:|---|
| example | Warm startup to an interactive Board (Show, queue drained) | 1556 | 1556 | 1500 (warm startup) | OVER |
| example | Board to Library | 155 | 155 | 100 (page switch) | OVER |
| example | One Library filter keystroke | 6 | 6 | 50 (filter keystroke) | within |
| example | Library filter: refill once typing pauses | 14 | 14 | 250 (UI thread) | within |
| example | Library to Board | 96 | 96 | 100 (page switch) | within |
| example | Board to Journals (built as ShowPage builds it, from the test's author library) | 315 | 315 | 100 (page switch) | OVER |
| example | Journals to Board | 90 | 90 | 100 (page switch) | within |
| example | Board to Deadlines | 419 | 419 | 100 (page switch) | OVER |
| example | Deadlines: one filter keystroke | 153 | 153 | 250 (UI thread) | within |
| example | Deadlines to Board | 75 | 75 | 100 (page switch) | within |
| example | Board to Deadlines (second visit) | 352 | 352 | 100 (page switch) | OVER |
| example | Deadlines to Board (second visit) | 71 | 71 | 100 (page switch) | within |
| example | Board to Insights | 200 | 200 | 100 (page switch) | OVER |
| example | Insights: figures ready (calculated off the UI thread) | 167 | 156 | 250 (UI thread) | within |
| example | Insights to Board | 128 | 128 | 100 (page switch) | OVER |
| example | Board to Import & Export | 2169 | 2169 | 100 (page switch) | OVER |
| example | Import & Export to Board | 152 | 152 | 100 (page switch) | OVER |
| example | Board to Import & Export (second visit) | 130 | 130 | 100 (page switch) | OVER |
| example | Import & Export to Board (second visit) | 148 | 148 | 100 (page switch) | OVER |
| example | Board to a manuscript page | 3876 | 3876 | 100 (page switch) | OVER |
| example | One dirty-timer tick (median of 10) | 0 | 0 | 5 (dirty-timer tick) | within |
| example | Save from the manuscript page | 58 | 40 | 250 (save) | within |
| example | Manuscript page to Board | 232 | 232 | 100 (page switch) | OVER |
| example | Board render (RenderManuscripts) | 3 | 3 | 250 (UI thread) | within |
| example | Backup | 48016 | 62 | 250 (UI thread) | within |
| example | Restore: inspect the backup | 48 | 31 | 250 (UI thread) | within |
| example | Restore: replace the library, reload, render | 666 | 40 | 250 (UI thread) | within |
| example | Find Journals: click to first paint | 310 | 310 | 250 (UI thread) | OVER |
| example | Find Journals: results rendered (fixture responses) | 4 | 4 | 250 (UI thread) | within |
| example | Look Up Journal Facts: click to first paint | 112 | 112 | 250 (UI thread) | within |
| example | Look Up Journal Facts: results rendered (fixture responses) | 164 | 164 | 250 (UI thread) | within |
| 500 | Warm startup to an interactive Board (Show, queue drained) | 4371 | 4371 | 1500 (warm startup) | OVER |
| 500 | Board to Library | 228 | 228 | 100 (page switch) | OVER |
| 500 | One Library filter keystroke | 1 | 1 | 50 (filter keystroke) | within, enforced |
| 500 | Library filter: refill once typing pauses | 153 | 153 | 250 (UI thread) | within |
| 500 | Library to Board | 339 | 339 | 100 (page switch) | OVER |
| 500 | Board to Journals (built as ShowPage builds it, from the test's author library) | 274 | 274 | 100 (page switch) | OVER |
| 500 | Journals to Board | 89 | 89 | 100 (page switch) | within |
| 500 | Board to Deadlines | 4631 | 4631 | 100 (page switch) | OVER |
| 500 | Deadlines: one filter keystroke | 6595 | 6595 | 250 (UI thread) | OVER |
| 500 | Deadlines to Board | 436 | 436 | 100 (page switch) | OVER |
| 500 | Board to Deadlines (second visit) | 8006 | 8006 | 100 (page switch) | OVER |
| 500 | Deadlines to Board (second visit) | 1151 | 1151 | 100 (page switch) | OVER |
| 500 | Board to Insights | 265 | 265 | 100 (page switch) | OVER |
| 500 | Insights: figures ready (calculated off the UI thread) | 190 | 182 | 250 (UI thread) | within |
| 500 | Insights to Board | 135 | 135 | 100 (page switch) | OVER |
| 500 | Board to Import & Export | 1791 | 1791 | 100 (page switch) | OVER |
| 500 | Import & Export to Board | 190 | 190 | 100 (page switch) | OVER |
| 500 | Board to Import & Export (second visit) | 172 | 172 | 100 (page switch) | OVER |
| 500 | Import & Export to Board (second visit) | 179 | 179 | 100 (page switch) | OVER |
| 500 | Board to a manuscript page | 2925 | 2925 | 100 (page switch) | OVER |
| 500 | One dirty-timer tick (median of 10) | 0 | 0 | 5 (dirty-timer tick) | within |
| 500 | Save from the manuscript page | 404 | 71 | 250 (save) | within, enforced |
| 500 | Manuscript page to Board | 205 | 205 | 100 (page switch) | OVER |
| 500 | Board render (RenderManuscripts) | 13 | 13 | 250 (UI thread) | within, enforced |
| 500 | Backup | 26735 | 47 | 250 (UI thread) | within |
| 500 | Restore: inspect the backup | 12817 | 47 | 250 (UI thread) | within, enforced |
| 500 | Restore: replace the library, reload, render | 58702 | 60 | 250 (UI thread) | within |
| 500 | Find Journals: click to first paint | 229 | 229 | 250 (UI thread) | within |
| 500 | Find Journals: results rendered (fixture responses) | 238 | 238 | 250 (UI thread) | within |
| 500 | Look Up Journal Facts: click to first paint | 125 | 125 | 250 (UI thread) | within |
| 500 | Look Up Journal Facts: results rendered (fixture responses) | 126 | 126 | 250 (UI thread) | within |

## Debug build

Verdict: UI against the budget x 2; enforced rows marked.

| Library | Action | Wall ms | UI ms | Budget ms | Verdict |
|---|---|---:|---:|---:|---|
| example | Warm startup to an interactive Board (Show, queue drained) | 1598 | 1598 | 1500 (warm startup) | within |
| example | Board to Library | 207 | 207 | 100 (page switch) | OVER |
| example | One Library filter keystroke | 7 | 7 | 50 (filter keystroke) | within |
| example | Library filter: refill once typing pauses | 17 | 17 | 250 (UI thread) | within |
| example | Library to Board | 140 | 140 | 100 (page switch) | within |
| example | Board to Journals (built as ShowPage builds it, from the test's author library) | 416 | 416 | 100 (page switch) | OVER |
| example | Journals to Board | 124 | 124 | 100 (page switch) | within |
| example | Board to Deadlines | 828 | 828 | 100 (page switch) | OVER |
| example | Deadlines: one filter keystroke | 417 | 417 | 250 (UI thread) | within |
| example | Deadlines to Board | 203 | 203 | 100 (page switch) | OVER |
| example | Board to Deadlines (second visit) | 716 | 716 | 100 (page switch) | OVER |
| example | Deadlines to Board (second visit) | 205 | 205 | 100 (page switch) | OVER |
| example | Board to Insights | 363 | 363 | 100 (page switch) | OVER |
| example | Insights: figures ready (calculated off the UI thread) | 275 | 272 | 250 (UI thread) | within |
| example | Insights to Board | 228 | 228 | 100 (page switch) | OVER |
| example | Board to Import & Export | 2962 | 2962 | 100 (page switch) | OVER |
| example | Import & Export to Board | 262 | 262 | 100 (page switch) | OVER |
| example | Board to Import & Export (second visit) | 242 | 242 | 100 (page switch) | OVER |
| example | Import & Export to Board (second visit) | 212 | 212 | 100 (page switch) | OVER |
| example | Board to a manuscript page | 10376 | 10376 | 100 (page switch) | OVER |
| example | One dirty-timer tick (median of 10) | 0 | 0 | 5 (dirty-timer tick) | within |
| example | Save from the manuscript page | 123 | 64 | 250 (save) | within |
| example | Manuscript page to Board | 626 | 626 | 100 (page switch) | OVER |
| example | Board render (RenderManuscripts) | 19 | 19 | 250 (UI thread) | within |
| example | Backup | 37043 | 115 | 250 (UI thread) | within |
| example | Restore: inspect the backup | 45 | 18 | 250 (UI thread) | within |
| example | Restore: replace the library, reload, render | 740 | 37 | 250 (UI thread) | within |
| example | Find Journals: click to first paint | 267 | 267 | 250 (UI thread) | within |
| example | Find Journals: results rendered (fixture responses) | 3 | 3 | 250 (UI thread) | within |
| example | Look Up Journal Facts: click to first paint | 98 | 98 | 250 (UI thread) | within |
| example | Look Up Journal Facts: results rendered (fixture responses) | 126 | 126 | 250 (UI thread) | within |
| 500 | Warm startup to an interactive Board (Show, queue drained) | 3969 | 3969 | 1500 (warm startup) | OVER |
| 500 | Board to Library | 212 | 212 | 100 (page switch) | OVER |
| 500 | One Library filter keystroke | 1 | 1 | 50 (filter keystroke) | within, enforced |
| 500 | Library filter: refill once typing pauses | 79 | 79 | 250 (UI thread) | within |
| 500 | Library to Board | 286 | 286 | 100 (page switch) | OVER |
| 500 | Board to Journals (built as ShowPage builds it, from the test's author library) | 216 | 216 | 100 (page switch) | OVER |
| 500 | Journals to Board | 87 | 87 | 100 (page switch) | within |
| 500 | Board to Deadlines | 4114 | 4114 | 100 (page switch) | OVER |
| 500 | Deadlines: one filter keystroke | 6194 | 6194 | 250 (UI thread) | OVER |
| 500 | Deadlines to Board | 248 | 248 | 100 (page switch) | OVER |
| 500 | Board to Deadlines (second visit) | 4243 | 4243 | 100 (page switch) | OVER |
| 500 | Deadlines to Board (second visit) | 251 | 251 | 100 (page switch) | OVER |
| 500 | Board to Insights | 135 | 135 | 100 (page switch) | within |
| 500 | Insights: figures ready (calculated off the UI thread) | 121 | 114 | 250 (UI thread) | within |
| 500 | Insights to Board | 87 | 87 | 100 (page switch) | within |
| 500 | Board to Import & Export | 1446 | 1446 | 100 (page switch) | OVER |
| 500 | Import & Export to Board | 141 | 141 | 100 (page switch) | within |
| 500 | Board to Import & Export (second visit) | 150 | 150 | 100 (page switch) | within |
| 500 | Import & Export to Board (second visit) | 121 | 121 | 100 (page switch) | within |
| 500 | Board to a manuscript page | 2798 | 2798 | 100 (page switch) | OVER |
| 500 | One dirty-timer tick (median of 10) | 0 | 0 | 5 (dirty-timer tick) | within |
| 500 | Save from the manuscript page | 370 | 91 | 250 (save) | within, enforced |
| 500 | Manuscript page to Board | 198 | 198 | 100 (page switch) | within |
| 500 | Board render (RenderManuscripts) | 24 | 24 | 250 (UI thread) | within, enforced |
| 500 | Backup | 33085 | 62 | 250 (UI thread) | within |
| 500 | Restore: inspect the backup | 11921 | 46 | 250 (UI thread) | within, enforced |
| 500 | Restore: replace the library, reload, render | 49586 | 50 | 250 (UI thread) | within |
| 500 | Find Journals: click to first paint | 386 | 386 | 250 (UI thread) | within |
| 500 | Find Journals: results rendered (fixture responses) | 339 | 339 | 250 (UI thread) | within |
| 500 | Look Up Journal Facts: click to first paint | 98 | 98 | 250 (UI thread) | within |
| 500 | Look Up Journal Facts: results rendered (fixture responses) | 152 | 152 | 250 (UI thread) | within |

## 2026-10-17: the Deadlines and Import & Export pages

The second speed batch's first half for [#133](https://github.com/JUhalt/PaperRoute-Tracker/issues/133), on the branch `fix/v092-speed-deadlines` from master `ab5f682`, measured by the same test on the same machine. Only the Deadlines and Import & Export rows changed; the other rows belong to the sibling batch (startup, the manuscript page, the other page switches) and are left to it.

### What changed

- **Deadlines draws its rows in one window.** `DeadlineList`, a `ScrollableControl`, draws every heading, row, button, the empty list's sentence and the Done line, and scrolls itself. Each row keeps its text, its painting, its actions in the same order, its buttons and its **⋯**, its keyboard map (Tab through each row, its buttons and its **⋯**, then the Done line; Up and Down from row to row; Left and Right across the rows and the Done line; Enter, Space, the context-menu key, a double-click and a right-click as before), and focus on the same row across a refill. A screen reader sees one list, *Deadlines*, whose children in reading order are the headings, each row as a list item followed by its buttons, the sentence and the Done link; each move of the keyboard is announced. The four counts are drawn by one `DeadlineGlance`. The page makes **17 windows** however long the list is, where its 221 rows on the 500 library made about **1,140**.
- **Import & Export lays out its commands in one window.** `CommandList` holds the 14 buttons and draws the five headings and the 14 descriptions around them, wrapping as before; it lays itself out only when its width, font or DPI changes, where the table it replaces laid itself out about 190 times on the first visit. The page makes **19 windows** where it made **40**.
- **Both pages are kept and hidden in place.** Deadlines is built once (each visit clears the filter text, scrolls to the top and refills; the chosen chip and Show done carry over, as before), Import & Export as before; leaving hides a kept page with `Visible` rather than taking it out of the window, and a kept page is put in the window hidden at its final size so its windows are made once.
- **The two reserve levers were used**, after a first visit read 134 and 141 ms on a busy host: the Deadlines page puts its counts, chips and list in the page frame's own rows (one table fewer), and `CreatePageHeader` makes no window for an empty actions panel (one fewer on Import & Export, and on the Journals page).
- `Control.AccessibilityNotifyClients` raises nothing on .NET 10.0 (it calls `NotifyWinEvent` only while the `NoClientNotifications` switch is set), so the drawn lists raise their focus, reorder and name events with `NotifyWinEvent` themselves. The route chart's description change on Insights uses the WinForms call and is therefore silent too; that is outside this batch and is recorded for a separate fix.

### What the test holds now

- **Enforced on the 500 library** (x1 in Release, x2 in Debug, x3 more when `GITHUB_ACTIONS` is set, so x3 for a Release run on the CI runner and x6 for a Debug one): Board to Deadlines, the Deadlines filter keystroke, Deadlines to Board, both second-visit Deadlines rows, Board to Import & Export, Import & Export to Board, and both second-visit Import & Export rows.
- **Two new enforced rows**, against the UI-thread budget: painting the visible Deadlines rows (`DrawToBitmap`; the other rows do not pump messages, so painting is timed on its own), and a refill after a change (`RenderManuscripts` while Deadlines is open, which every row action's save runs).
- **Guards outside the timed steps:** the 500 library has at least 200 open deadline items (221), and the list holds every one of them after the visit, after the keystroke (the filter "a" matches every item) and after the second visit, so no row can pass by showing fewer.

### Host during these runs

The same machine, Windows 11 (NT 10.0.26300), 12 logical processors, window DPI 96, with about 17 to 23 GB of memory free; Firefox, Thunderbird, OneNote, Word, PowerToys, Malwarebytes, CrossDeviceService, OneDrive, 1Password and the ChatGPT and Claude apps running throughout, and a VirtualBox VM until about 14:30. Sibling worktrees (`claude-v092-speed-startup`, `claude-v092-polish`) ran their own test hosts on and off all afternoon. Each run below records the processes and five CPU samples before and after it, and a CPU sample every 2 seconds during it.

- **Run D, 15:24, quiet:** no other test host and CPU 12 to 33% through the first 100 seconds of the run, which hold both libraries' page rows; a sibling test host started later.
- **Run C, 14:52:** quiet for its first minute; a sibling Debug test host (`claude-v092-polish`) started about a minute in, with CPU up to 82%, so the 500 library's rows may have shared the machine with it.
- Busy runs, recorded so the variance is on the record: **14:37** (no other test host, but the VM was shutting down) read Board to Deadlines 134 ms and Board to Import & Export 141 ms, before the two levers; **14:46** (a sibling test host running, CPU 36 to 71%) read 205 and 287 ms and Import & Export to Board 104 ms, and also failed Save from the manuscript page at 306 ms; **15:03** (two sibling test hosts, CPU 30 to 82%) read 70 and 90 ms and failed only Restore's inspection step, at 522 ms. On this machine a window costs 2 ms to make when it is quiet and two to three times that when it is not, so a first visit's 17 to 19 windows move most with the host.

### Release build, 500 library

Before: the after table above (2026-10-16), and the profile's quiet run of 2026-10-17 at 08:17 on `ab5f682`. A, B: this branch before the levers and before the rows were enforced (A at 13:55 with a sibling Debug test host starting, B at 14:22 on a quiet host). C, D: this branch as committed, with the rows enforced.

| Action | Before (10-16) | Before (profile) | A | B | C | D | Budget ms | Verdict in C and D |
|---|---:|---:|---:|---:|---:|---:|---:|---|
| Board to Deadlines | 4631 | 2300 | 167 | 39 | 97 | 42 | 100 (page switch) | within, enforced |
| Deadlines: one filter keystroke | 6595 | 3292 | 6 | 2 | 4 | 1 | 250 (UI thread) | within, enforced |
| Deadlines: paint the visible rows (DrawToBitmap) | — | — | — | — | 23 | 8 | 250 (UI thread) | within, enforced |
| Deadlines: refill after a change (RenderManuscripts while Deadlines is open) | 3 to 7 s | — | — | — | 62 | 18 | 250 (UI thread) | within, enforced |
| Deadlines to Board | 436 | 98 | 53 | 19 | 32 | 20 | 100 (page switch) | within, enforced |
| Board to Deadlines (second visit) | 8006 | 2918 | 29 | 12 | 20 | 8 | 100 (page switch) | within, enforced |
| Deadlines to Board (second visit) | 1151 | 187 | 46 | 17 | 34 | 21 | 100 (page switch) | within, enforced |
| Board to Import & Export | 1791 | 845 | 1526 | 53 | 98 | 47 | 100 (page switch) | within, enforced |
| Import & Export to Board | 190 | 77 | 42 | 18 | 42 | 18 | 100 (page switch) | within, enforced |
| Board to Import & Export (second visit) | 172 | 65 | 43 | 8 | 7 | 3 | 100 (page switch) | within, enforced |
| Import & Export to Board (second visit) | 179 | 86 | 48 | 20 | 63 | 16 | 100 (page switch) | within, enforced |

Run A's Import & Export rows are before its own change (the second commit), which B, C and D include. The refill row's "before" is the keystroke's cost on 2026-10-16: every save from a row action rebuilt the list the same way. UI equals wall for every row here.

The commit messages name runs differently. The first commit's message (`fede4b2`) calls a quiet run of that commit at 14:06 "B" (Board to Deadlines 61 ms, the keystroke 3, Deadlines to Board 21, the second visit 12 and its leave 21, Import & Export to Board 22); that run is not this table's B, which is the 14:22 run after the second commit. "Profile run B" in all three messages is this table's "Before (profile)" column.

**Run E, 17:25, after the review fixes** (the Import & Export list out of the Tab order, a pressed button scrolled into view by its own bounds, the heading under the pointer read, one accessible object for drawn text), beside a sibling Release test host (`claude-v092-speed-startup`), CPU 0 to 57%: Board to Deadlines 62 ms, the keystroke 3, painting 19, the refill 43, Deadlines to Board 24, the second visit 20 and its leave 26, Board to Import & Export 81, Import & Export to Board 28, its second visit 9 and that leave 28, all within. The run failed only Restore's inspection step, at 458 ms, a row outside this batch that a busy run failed before (522 ms at 15:03).

### Release build, example library (report-only)

| Action | 10-16 | C | D |
|---|---:|---:|---:|
| Board to Deadlines | 419 | 77 | 63 |
| Deadlines: one filter keystroke | 153 | 1 | 1 |
| Deadlines: paint the visible rows (DrawToBitmap) | — | 23 | 12 |
| Deadlines: refill after a change | — | 23 | 10 |
| Deadlines to Board | 75 | 22 | 11 |
| Board to Deadlines (second visit) | 352 | 12 | 4 |
| Deadlines to Board (second visit) | 71 | 21 | 11 |
| Board to Import & Export | 2169 | 89 | 51 |
| Import & Export to Board | 152 | 33 | 15 |
| Board to Import & Export (second visit) | 130 | 6 | 3 |
| Import & Export to Board (second visit) | 148 | 26 | 18 |

The example library runs first and carries the process's first-time costs (the JIT of the new controls among them), so its rows stay report-only.

### Debug build

The targeted Debug filter at 15:28, with two sibling Debug test hosts (`claude-v092-polish`, `claude-v092-speed-startup`) running beside it and CPU 22 to 84%: verdict against the budget x 2; all 152 tests passed.

| Action | 10-16, 500 | 500 | example |
|---|---:|---:|---:|
| Board to Deadlines | 4114 | 85 | 112 |
| Deadlines: one filter keystroke | 6194 | 2 | 5 |
| Deadlines: paint the visible rows (DrawToBitmap) | — | 60 | 48 |
| Deadlines: refill after a change | — | 74 | 49 |
| Deadlines to Board | 248 | 36 | 32 |
| Board to Deadlines (second visit) | 4243 | 18 | 15 |
| Deadlines to Board (second visit) | 251 | 43 | 54 |
| Board to Import & Export | 1446 | 127 | 160 |
| Import & Export to Board | 141 | 32 | 35 |
| Board to Import & Export (second visit) | 150 | 16 | 5 |
| Import & Export to Board (second visit) | 121 | 46 | 41 |

After the review fixes, the targeted Debug filter at 17:49 failed only the budget test, beside one and then two sibling Debug test hosts. Its 500 library ran slow throughout (warm startup 13,984 ms, Board to a manuscript page 8,592 ms), and four enforced rows exceeded x2: Board to Deadlines 236 ms, the refill 636, Board to Import & Export 1,198 and Save from the manuscript page 584; the example library's first visit to Import & Export read 94 ms in the same run. Run again at 18:15 with no other test host until a sibling started near its end (18:22:53), all 137 tests passed, with the 500 library at Board to Deadlines 34 ms, the keystroke 2, painting 12, the refill 35, Deadlines to Board 20, the second visit 11 and its leave 16, Board to Import & Export 64, Import & Export to Board 16, its second visit 3 and that leave 16.

### Where the time goes now

A scratch timing on the 500 library (not kept), on a moderately loaded host, broke the first visit to Deadlines down as: building the page and filling the list 8 ms (the data 1 ms, the 221 row models under 1 ms, laying out the entries 5 ms), putting the page in the window hidden 4 ms, giving it its size and laying it out 4 ms, making its windows as it is shown 35 ms (2 ms a window), bringing it forward and hiding the Board 2 ms. A second visit is a refill and showing the kept page; a keystroke is a refill alone, since no window is made or destroyed. Leaving either page is hiding it and showing the Board again, and `boardPage.Visible = True` (13 to 46 ms in the profile) is the floor of every "to Board" row, which the sibling batch owns. Import & Export's first visit is the same: its build, about 20 ms of layout and text measurement, and its 19 windows.

### What remains over budget

Of these rows, none on a quiet host. On a busy one the first visits to both pages can still pass 100 ms (134 to 287 ms in the busy runs above), because window creation slows with the host; making fewer windows would mean drawing the filter chips or the page header, which are shared with other pages and tests, and building the pages before they are opened would only move the cost into startup. They stay enforced at x1, as the earlier rows do, and hold x3 on the CI runner.

These rows are enforced while Journals to Board is only reported (see the top of this document) for two reasons: #133's brief requires it, and on a quiet host their first visits read 39 to 53 ms, where Journals to Board read 87 to 89 ms on a busy one. The budget test can therefore fail on this host while a sibling build or test host runs, and so can the targeted Debug filter, whose x2 allows 200 ms against busy Release readings of up to 287 ms above. Run such a failure again on a quiet host before reading it as a regression.
