# Responsiveness budgets: the after table — 2026-10-16

The same measurements as [the before table](Responsiveness-Before-2026-10-16.md), taken by the same test on the same machine after the second and third responsiveness pull requests ([#132](https://github.com/JUhalt/PaperRoute-Tracker/issues/132) and [#133](https://github.com/JUhalt/PaperRoute-Tracker/issues/133)). Read the before table's "How the numbers were taken" first; only the differences are repeated here.

## What changed between the tables

- **#132.** Backup, Restore and the save, with its sweep of the managed root, run on a worker thread while the window is disabled the way a modal dialog disables its owner (`EnableWindow`, not `Form.Enabled`, which walks every control on a large Board and cost about 3 s each way in the profile) and the status line says what is happening; the UI thread processes messages until the work ends. The Insights figures are worked out on a worker thread and come back through the window's message queue while the page keeps what it shows.
- **#133.** `RenderManuscripts` keeps every card it has built and shows a card again when the text it shows is unchanged, parking cards that leave a shelf; a card's actions look its manuscript up by id when used, so a restore keeps the cards whose text is unchanged. The Library filter refills the grid once typing pauses (a 200 ms timer, as the Board's search), and the grid's rows go in together. The Import & Export page is built once and kept. The dirty-timer tick, within budget before, is unchanged.
- Rows added to the test: the debounced Library refill, a Deadlines filter keystroke, the Insights figures, and second visits to Deadlines and Import & Export. Backup and Restore are now timed through the handlers' seams (`WriteBackup`, `InspectBackupFile`, `ReplaceLibraryFrom`) rather than the services directly; the work timed is the same.
- Enforced from #133 on, on the 500 library, with room to spare on this busy host: the Library filter keystroke, the Board render, Save from the manuscript page and the inspection step of Restore. Backup's stall varied from 61 to 419 ms across runs and stays reported. So does the replace step of Restore, which #133 brought from 13 s to the 50 to 60 ms below, but which measured 95 and 331 ms in two later Debug runs on this host: like Backup it pumps for 30 to 60 s, long enough to meet one of the host's own stalls. Journals to Board, which #133 also brought within budget, stays reported: at 87 to 89 ms against 100 ms it has no room on this host. Manuscript page to Board, also changed by #133, is still over in Release in the tables below; from 2026-10-17 it is within and enforced (see the startup section below). The example library runs first and carries the process's first-time costs, so its rows stay report-only.
- **2026-10-17, after the merge with #116.** A restore now ends in a restart rather than an in-place reload, so `ReplaceLibraryFrom` is gone, and the test reports the row "Restore: replace the library, reload, render" below as "Restore: replace the library (the window then restarts)": it times the swap and the notice through `RestoreFromArchive`, with the restart recorded rather than run. Its numbers below were measured before the merge and were not taken again. Since c4af8e9 a card's actions act on the card's own record while the library holds it, and look the manuscript up by id only when it does not. The recorded numbers and the budgets are unchanged.
- **2026-10-17, the Deadlines and Import & Export pages (#133, second batch).** Both pages are now drawn by one list each and kept between visits; their rows are enforced. The tables below are from 2026-10-16 and were not changed; the new figures, the host and the reasons are in [their section below](#2026-10-17-the-deadlines-and-import--export-pages).
- **2026-10-17, startup, the manuscript page and the page switches (#133, second part).** The Board fills in after its first screen, one manuscript page is kept and loads one record after another, and pages are made and disposed where they stay; warm startup, the Board's fill and the switches back to the Board from Insights and the manuscript page are enforced. The tables below were not changed; the new figures, the host, the reasons and the notes on merging the two batches are in [their section below](#2026-10-17-startup-the-manuscript-page-and-the-page-switches-133-second-part).
- **2026-10-23, the manuscript page, Journals, Insights, the Published shelf and Library to Board (#133, part C).** Switches paint nothing until they return, so their rows no longer hold the screen's half-second wait behind Library to Board's spikes: the wait moved into the paint after a switch, which the researcher still waits on and which is reported in rows of its own (see the review follow-ups at the end of that section); the manuscript page, Journals, Insights and the shelves not shown are prepared while the window idles; the shortlist is drawn in one list. Library to Board, Board to Journals, Board to Insights, the first switch to Published and the manuscript page's versions and details step are enforced; the three manuscript-page opens are still over budget on a busy host and stay reported. The figures, the host and the reasons are in [their section below](#2026-10-23-the-manuscript-page-journals-insights-the-published-shelf-and-library-to-board-133-part-c).
- **2026-10-26, the three manuscript-page opens and Journals to Board (#133).** The page left keeps its last record's shape until the next open, is prepared in that shape, lays out nothing it leaves hidden or that nothing changed in, and leaving Journals renders less: an open lays out 15 to 21 containers where it laid out 40 to 44. The three opens and both visits' Journals to Board are enforced; Board to Library, the paint rows and the idle slice stay reported. The figures, the host and the reasons are in [their section below](#2026-10-26-the-three-manuscript-page-opens-and-journals-to-board-133).

## Host during the after runs

The same machine as the before table, again busy and shared: about 75% CPU load sampled during the runs, with another PaperRoute test host from a sibling worktree running throughout, plus Firefox, OneDrive sync, CrossDeviceService, Malwarebytes and Thunderbird; 14.8 GB of memory free. Variation between runs on this host stayed at tens of percent (Backup's UI stall, for one, ranged from 61 to 419 ms across four runs), so differences smaller than that between the before and after tables are noise.

## Where the time went, and what remains

The profile behind the choices (a scratch test, not kept) measured, on the 500 library with the Board visible: building one card about 3 ms (its context menu 2 ms of that), giving a card its ten window handles about 43 ms, adding 167 cards to the visible shelf 55 to 60 ms each and linear in the count, adding 100 cards to a hidden shelf 41 ms in all, `Form.Enabled = False` 2.9 s and `= True` 1.9 s, the native `EnableWindow` under 1 ms, `EditManuscriptForm`'s constructor 2.6 to 3.1 s of which `LoadManuscript` is 0.75 s, the Deadlines page 0.8 s to build detached and 5.9 s to attach (its 221 rows carry about 1,100 handles; suspending layout made no difference), Import & Export 0.5 s to build and 1.8 s to attach, and a `Rows.AddRange` fill of the Library grid 219 ms against 557 ms one row at a time. Handle creation is the cost this machine pays: about 4 to 5 ms per handle, far above a quiet desktop, with PowerToys, Malwarebytes and a cross-device service running.

What this leaves over budget on the 500 library, and why:

- **Warm startup** builds all 500 cards and gives the 167 Pipeline cards their handles before the Board is interactive; nothing is rebuilt twice any more, but the first build remains. Only a card with fewer controls, or a shelf that creates cards as they scroll into view, would cut it further, and both are beyond the least change this issue allowed. *Done on 2026-10-17: see the startup section below.*
- **Board to Deadlines**, its filter keystroke and its second visit all rebuild the page's 221 rows, about 1,100 handles. Keeping the rows between fills the way the Board keeps its cards was tried and measured at 2.4 s for a keystroke and 3.1 s for a second visit against 4.6 s: on this machine every window operation, re-parenting included, costs milliseconds, so the change did not reach the budget and was not kept. A list that draws its rows instead of hosting a control per row is the change that would. *Done on 2026-10-17: see the Deadlines and Import & Export section below.*
- **Board to Import & Export (first visit)** is the same handle creation on first build; the second-visit row shows the kept page. *Done on 2026-10-17: see the Deadlines and Import & Export section below.*
- **Board to a manuscript page** is `EditManuscriptForm`'s construction, re-done for each manuscript opened. Keeping an editor for the next manuscript would need the form to load another record safely; that is a change to a 3,000-line form, not taken here. *Done on 2026-10-17: see the startup section below.*
- **Board to Library** and **Library to Board** build and dispose a 500-row grid; the refill is now a single `AddRange`, and the page switch sits a little over the 100 ms line. *Taken further on 2026-10-17: see the startup section below, where both rows stay reported.*
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

## 2026-10-17: startup, the manuscript page and the page switches (#133, second part)

Branch `fix/v092-speed-startup`, from ab5f682. The rows of the Deadlines and Import & Export pages belong to the sibling branch `fix/v092-speed-deadlines` and are left as they were. Run B is the quiet Release run of the profile behind this branch (2026-10-17, 09:27, mostly under 20% CPU, no other test host at its end), the baseline the work was planned against.

### What changed

- **Windows made once, where they stay.** WinForms makes a control's window as soon as it is shown, and a form's as soon as it is marked visible; with no window around it, the window is made on WinForms' parking window and moved into place later, and a control removed before it is disposed moves its windows there again before they are destroyed. On this machine each window made, moved or destroyed costs 1 to 6 ms. Pages and the editor's parts are now shown once their parent has a window (`InPlace.WhenAttached`), and a page left is disposed where it is, after focus moves out of it. The Library grid's columns are sized from their header and one cell per distinct text instead of `AllCells`.
- **The Board fills in after its first screen.** At startup only the shelf shown is built, and only the cards that fill its view and one row more get their windows before the Board is interactive; the rest show a card at a time while the window idles (a 15 ms timer, 30 ms of work a tick, waiting while a save, backup or restore runs). A shelf not shown yet is built the first time it is shown. A card's context menu is built when its window is made or when first asked for (`CardPanel`). A render that changes one card replaces that card alone; before, every card left the shelf and came back.
- **One manuscript page, kept.** The editor loads one record after another (`LoadRecord`, with everything it holds for a record in one `RecordState` replaced as a whole). The page is built for the first manuscript opened and kept, hidden, for the next: a manuscript loads into it, the page shows in one layout pass, and the Versions tab and the selected submission's details follow once it has painted. Leaving hides it, and the editor lets go of the record while the window idles. It is built again when the colors, the DPI or the font change.
- Enforced from here on, on the 500 library: warm startup, the Board's fill, the Library refill, the page switches back to the Board from the Library (until 2026-10-18; reported since, see below), Insights and the manuscript page, letting go of the record after the manuscript page is left (from 2026-10-18), and the save of a Pipeline manuscript whose card changes; the Library keystroke, the Board render, Save from the manuscript page and Restore's inspection as before.
- **2026-10-18, after review.** A shelf switched to for the first time shows its first screen at once: built while hidden, it is measured by the space it fills, where it had been measured at the 200 x 100 it was made at and showed 2 cards, the rest coming in a card at a time. The kept page becomes the page's editor only once a record has loaded, so an open that fails part way leaves nothing to ask about or save; a record with no reroute offer clears the offer the last record left in its hidden panel; the shortlist rows are no longer styled one by one (master's #106 styles them once merged) and `UiPolish.ApplyToControls` is master's; a list's empty hint takes its list's colors while hidden too. Letting go of the record is enforced (below).

### Numbers

500 library. Release against the budget x 1, Debug against x 2.

| Action | Before (Release, above) | Run B | Release 21:10 / 21:14 | Debug 21:21 | Release 10-18 00:32 / 00:45 | Budget ms | Held |
|---|---:|---:|---:|---:|---:|---:|---|
| Warm startup to an interactive Board | 4371 | 2928 | 1055 / 940 | 4945 | 764 / 815 | 1500 | enforced |
| Board: the rest of the shown shelf, in idle slices (longest stall) | new | | 62 / 70 | 384 | 76 / 59 | 250 | enforced |
| First switch to Published (the shelf built when first shown) | new | | 674 / 622 (2 cards) | 3653 | 314 / 273 (15 cards) | 100 | reported |
| Board to Library | 228 | 103 | 55 / 81 | 219 | 44 / 57 | 100 | reported |
| One Library filter keystroke | 1 | | 1 / 2 | 4 | 1 / 1 | 50 | enforced |
| Library filter: refill once typing pauses | 153 | | 13 / 44 | 65 | 14 / 29 | 250 | enforced |
| Library to Board | 339 | 115 | 274 / 38 | 66 | 128 / 31 | 100 | reported from 10-18 |
| Board to Journals | 274 | 76 | 133 / 188 | 503 | 102 / 141 | 100 | reported |
| Journals to Board | 89 | 42 | 99 / 96 | 240 | 30 / 39 | 100 | reported |
| Board to Insights | 265 | 101 | 206 / 129 | 212 | 57 / 62 | 100 | reported |
| Insights to Board | 135 | 46 | 62 / 35 | 102 | 16 / 15 | 100 | enforced |
| Board to a manuscript page, the first (the page is built) | 2925 | 1186 | 1985 / 1478 | 2267 | 422 / 453 | 100 | reported |
| Manuscript page: versions and details after its first paint | new | | 338 / 190 | 583 | 86 / 79 | 250 | reported |
| Save from the manuscript page | 71 | | 136 / 87 | 426 | 47 / 29 | 250 | enforced |
| Manuscript page to Board | 205 | 617 (after Save) | 53 / 34 | 60 | 18 / 20 | 100 | enforced |
| Manuscript page: the record let go of while the window idles | new | | 189 / 155 | 361 | 35 / 42 | 250 | enforced from 10-18 |
| Board to a manuscript page, the kept page | new | | 274 / 273 | 578 | 74 / 64 | 100 | reported |
| Kept manuscript page to Board | new | | 40 / 39 | 54 | 23 / 20 | 100 | enforced |
| Board render (RenderManuscripts) | 13 | | 59 / 40 | 109 | 17 / 14 | 250 | enforced |
| Save of a Pipeline manuscript whose card changes | new | | 67 / 53 | 133 | 31 / 32 | 250 | enforced |

The first Release run failed on Library to Board (274 ms against 100) and the second on Restore's inspection (520 ms against 250, enforced before this branch); the Debug run failed on startup (4,945 ms against 3,000). Each of those rows measured 38, 61 and 940 to 1,055 ms in the other runs: the failures are this host's, see below. (Library to Board's, read again after the merge, are not the host's alone: see the report-only rows below.) The quietest run of the branch (19:28, CPU 11 to 25%, no other test host, before the kept page) held every row enforced now that it measured, with warm startup at 950 ms, Library to Board 27 ms, Board to Journals 111 ms and Board to Insights 210 ms.

The 2026-10-18 runs, after the review fixes, were taken on a quieter host and are not a measure of those fixes against the runs before them; only the first switch to Published changed what it does (15 cards with their windows where it gave 2). The 00:32 run started as the Release build ended (CPU 63% at its start; the example library's first rows, run first, took 10.3 s for startup and 9.8 s for the first keystroke against 0.5 s and 6 ms at 00:45) and failed on Library to Board at 128 ms. A run at 00:38, left out of the table, failed on the same row at 419 ms: a sibling worktree's Debug test host started at 00:39:54, during it. The 00:45 run, with no other test host (checked every 15 s) and CPU 0 to 65% from the run itself, held every enforced row. None of the fixes changes the work Library to Board does; it has measured 27 to 419 ms on this host across the branch.

### Host during these runs

Windows 11 build 26300, 12 logical processors, window DPI 96. CPU load sampled at the start and end of each run: 83 and 74% for the first Release run, 76 and 19% for the second, 17 and 86% for the Debug run. No other PaperRoute test host ran during the first Release run; a sibling worktree's Debug test host ran from 21:16, through the end of the second Release run and the whole Debug run, about seven CPU-minutes. Desktop programs running throughout: CrossDeviceService (the most CPU time), OneNote, PowerToys, 1Password, PrusaSlicer and several Claude sessions, plus other builds and tests of sibling worktrees on and off through the day. Another UI test host costs this branch most: window creation is shared across the desktop, and the profile measured a window at 0.9 to 1.6 ms quiet and about 5.8 ms while one ran. The 2026-10-18 runs (00:32 to 00:48) had the same desktop programs open, with CPU at 7 to 10% between runs and 20 GB of memory free; the only other test host was the sibling's named above.

### Report-only rows, and why

- **Board to Library, Board to Journals, Journals to Board, Board to Insights** build their pages' windows (Library 14 and a 500-row grid, Journals 19, Insights 30). Measured step by step on a quiet host (CPU 5%): Board to Library 54 to 91 ms, Board to Journals 80 to 108 ms of which the Journals editor's constructor is 26 to 74 ms, Board to Insights 62 to 106 ms, Journals to Board 45 to 67 ms. Within the 100 ms line on some quiet runs and over it on others, they have no room to hold.
- **Library to Board**, reported from 2026-10-18. It disposes the Library page, its 500-row grid with it, and shows the Board again. It measured 27 to 100 ms in most runs of the branch and of the merge, and 128 to 481 ms in others: 274, 128 and 419 ms in Release (above), 337 ms in a review's run of the targeted Debug filter on the merged branch, and 481 ms in a Debug run of the budget test alone on 2026-10-18 at 05:41, with no other test host and CPU 0 to 68%, the run's own. In every one of those runs whose rows are recorded, Board to Library, the step before it, read 40 to 55 ms. The spikes are therefore not the host's alone, and where they come from is not known yet; until it is, the row is reported, as Board to Library is.
- **First switch to Published** builds that shelf's cards and gives its first screen their windows (15 cards in a 1280 x 840 window; until 2026-10-18 only 2, see above, so the 10-17 numbers understate it). Before this branch startup built those cards, and the first switch still gave every Published card its windows (1.1 to 2.6 s in the profile); later switches take 10 to 22 ms.
- **Board to a manuscript page, both rows.** The first open builds the page: the editor's constructor is 0.9 s on this host (building its controls 753 ms, styling them 419 ms, measured quiet). Building it while the window idles after startup was tried and left out: it is one step of 0.8 to 0.9 s, which would hold the window once at a moment the researcher did not choose. An open on the kept page measured, quiet: loading the record 145 to 290 ms (the submissions list and the shortlist's rows 50 to 155, the journal's notes and the Overview from its top 40 to 130), the header 54 to 102 ms and the one layout pass 100 to 400 ms. Loading a record replaces the windows of its shortlist rows, tag chips and lists, and the page lays out again; reaching 100 ms needs rows and chips that are kept and re-filled rather than rebuilt, which is a change to the editor's sections, not taken here.
- **The idle step after an open** builds the Versions tab and the selected submission's details once the page has painted, and has held the window 57 to 583 ms on this host across the branch's runs. The step after a leave, which lets go of the record and destroys its windows about 15 ms after the Board is back, is enforced from 2026-10-17 against the 250 ms UI-thread budget: the enforced "Manuscript page to Board" rows end when the Board shows and do not include it, so the two rows together are what a researcher going back waits. It runs as one step, longer than the idle queue's 30 ms slice; splitting it (the record's versions and details, then its rows and chips, then its lists) is left for later.
- The **example library** runs first and carries the process's first-time costs, so its rows stay report-only, as above.

### The first seconds after startup

Until the fill reaches the end of the shown shelf, the cards it has not reached are hidden: the shelf's scroll range, and a screen reader's object navigation, end at the last card shown. Tab going forward never stops there: focus entering a card shows the row after it. Shift+Tab into the shelf from outside it, as from the rail wrapping back, reaches the last card shown so far rather than the shelf's last card until the fill has passed. On the 500 library the fill takes about 5 s of idle time, a card at a time; it waits while another page covers the Board and goes on when the Board is back. A search typed meanwhile keeps a card for exactly what it finds; a found card past the first screen shows as the fill reaches it. The tab of a shelf not shown yet counts all its manuscripts; switched to, that shelf shows its first screen at once, measured by the space it fills, since a hidden shelf keeps the size it was made at.

### Merge notes

- **Form1.Shell.vb.** Before the merge (see "As merged" below): `LeaveCurrentPage` moves focus out of the page (`MoveFocusOutOf`) and disposes every page in place, Deadlines included; Import & Export keeps `Remove`, and the kept manuscript page is hidden and let go of while the window idles (`QueueManuscriptPageUnload`). `ShowPage`: the Board branch queues the fill, a manuscript shows on the kept page (`ShowManuscriptPage`, or `BuildMissingManuscriptPage`), and Journals goes through `CreateJournalLibraryEditor`. `EmbedForm` no longer sets `Visible`; `HostEditor` and the Library tabs show through `InPlace.WhenAttached`. `FitLibraryColumns` sits beside `CreatePageGrid`; `CreatePageFrame` is unchanged.
- **Form1.vb.** `DeferBoardFill` before `RenderManuscripts` in `Form1_Load`; `RenderManuscripts` and `ShowShelf` in fill mode; `FillShelf` in place; in `CreateManuscriptCard` the `CardPanel` menu lines and the `Enter` hook, near #187's tag-strip edit; `CardRecord.HiddenByFill`; the idle timer disposed in `OnFormClosed`.
- **New files:** `Form1.BoardFill.vb`, `Controls/CardPanel.vb`, `Controls/InPlace.vb` (`WhenAttached`, `HoldPlace` and `Dispose` live here, not in Form1.Shell.vb).
- **Other files:** Form1.Insights.vb, Form1.ManuscriptPage.vb (`CreateManuscriptEditor` no longer passes `workingCopy`: the page loads every record, a recovered draft included, through `LoadRecord`), EditManuscriptForm.vb and EditManuscriptForm.Workflow.vb, SubmissionDetailsForm.vb, TagChips.vb, EmptyHint.vb, UiPolish.vb, UiTheme.vb.
- **EmptyHint, every list that has one.** The change is app-wide, not the manuscript page's alone: a hint now shows only once its list has a window or a form around it, re-syncs when the list's window is made (`HandleCreated`), tracks whether it last showed (`Shown`) instead of reading the hint's `Visible`, which reads False under a hidden parent, takes its list's colors on every update, hidden or not (a hidden hint kept the colors its list had before it was styled, or when a record shown before left it empty), and gains `EmptyHint.Refresh(list)`. It applies to the Library grid, both Insights grids, AuthorLibraryForm, JournalLibraryForm, SubmissionDetailsForm, the version history and every other list with a hint. No Deadlines list has one, and neither master nor the sibling branch touches EmptyHint.vb after ab5f682.
- **Overlaps with master since ab5f682.** `UiPolish.ApplyToControls(parent)` is now master's #106 helper word for word, in the same place (it styles the controls under `parent`), so the two sides merge as one; this branch calls it as `ApplyToControls(_versionsHost)` in `BuildVersionHistory`. This branch no longer styles shortlist rows as they are built: they look as on ab5f682 until the merge, where master's `ApplyToControls(shortlistRows)` at the end of `RefreshShortlist` styles them. `ViewCatalog.CatalogBoard.Prepare` now takes `fillInSlices` (the filled Board view) in place of `PrepareFilled`; keep master's `WindowChrome.Apply(Me)` at its top. Form1.vb, EditManuscriptForm.vb, TagChips.vb and ViewCatalog.vb also change on both sides; resolve them hunk by hunk.
- **Tests:** LibraryBoard.vb (its `CreateManuscriptEditor` is master's again; `PumpWhile` moved to TestSupport), TestSupport.vb (`PumpWhile`, `DeepestActiveControl` and `ContrastRatio`, new), ResponsivenessBudgetTests.vb (rows to merge one by one with the sibling branch's), ViewCatalog.vb (two views), BackgroundLibraryWorkTests.vb and KeyboardAccessTests.vb (one test each), and the new BoardFillTests.vb, ManuscriptEditorReuseTests.vb, PageSwitchTests.vb and KeptManuscriptPageTests.vb. #187's additions to DashboardShelfLayoutTests and ManuscriptPageTests must pass after the merge.
- **Coverage, corrected.** Two commit messages of this branch (ee5e392, f48e4ae) say the names, Dark and theme sweeps cover the two new ViewCatalog views. On this branch only the names sweep (AccessibleNameTests) reads ViewCatalog; DarkThemeLegibilityTests and WorkflowThemeKeyboardSmokeTests build their own dialogs. Master's Dark sweep (`NoViewKeepsLightColorsInDark`, over every ViewCatalog view) covers them once merged. What holds the reused manuscript page to its themes here is ManuscriptEditorReuseTests: an editor that loads another record matches a fresh one, colors included, in Light, Dark and a contrast theme, and the parts built for each record (the version history, the shortlist's rows, the tag chips) are judged on their own: their text at 4.5:1 against its surface in Light and Dark, and the user's system colors under a contrast theme (#138).
- **As merged with `fix/v092-speed-deadlines` (2026-10-18).** The shell keeps three pages, the manuscript page, Deadlines and Import & Export, by one mechanism (`IsKeptPage`): leaving moves focus out of the page (`MoveFocusOutOf`) and hides a kept page in place, the manuscript page's record then let go of while the window idles; every other page is disposed in place, with no `Remove` first. `OnSystemColorsChanged` covers all three: a hidden kept page is disposed in place at once, the one showing as it is left, and the next visit builds it anew; the manuscript page is also built anew when it finds its colors, DPI or font changed, as above. The Board fills in slices as on this branch. Every row of both 2026-10-17 sections stays in `ResponsivenessBudgetTests`, and every enforced flag but Library to Board's (reported from 2026-10-18, see above); the merge was not measured again in Release. Master's code since ab5f682 now runs on measured rows of both 2026-10-17 sections, whose Release figures were taken without it: #106's shortlist styling (`UiPolish.ApplyToControls(shortlistRows)` at the end of `RefreshShortlist`, for each record the kept page loads or lets go of), #141's chip measure (`TagChipPainter.Measure`, for each tagged card built) and #143's `WindowChrome.Apply(Me)` in `Form1_Load`. In the merged branch's Debug runs, which include it, the only enforced row named over x2 is Library to Board. Those runs: the targeted Debug filter at 03:07 had one failure, which its quiet output did not name, with sibling worktrees' Debug test hosts starting during it; the filter passed at 03:33 (Library to Board 100 ms) and at 03:54, each on a quiet host; a review's run of the filter on `33db3b9` failed Library to Board at 337 ms (Board to Library 45 ms in the same run); and the budget test run alone at 05:41, on a quiet host, failed it at 481 ms, with every other enforced row within x2 (500 library: warm startup 768 ms, Board to Deadlines 64, Board to Import & Export 40, the record let go of 41 UI, Save from the manuscript page 32 UI). With the row reported, the filter passed again at 05:48 on a quiet host, Library to Board at 79 ms and Board to Library at 36.

## 2026-10-23: the manuscript page, Journals, Insights, the Published shelf and Library to Board (#133, part C)

Branch `fix/v092-speed-pages`, from `fix/v092-speed-startup` at `a3c0d10` (master `cfc93a0` with #195 and #200), with master `fdf477b` merged in at `f5c6947`. It takes the rows the two 2026-10-17 sections left over budget on the 500 library: the first and later opens of the manuscript page, its versions and details after the first paint, the first switch to the Published shelf, Board to Journals, Board to Insights and Library to Board. "Before" below is the profile's three clean Release runs on `a3c0d10` (2026-10-18 at 06:24, 07:37 and 07:39, the desktop locked, no other test host); "after" is three Release runs of this section's last commit (below). The sections above are history and were not changed.

### Library to Board: where its spikes came from

Timed 60 times in a row in the profile, Library to Board was 13 to 25 ms of work (disposing the Library page and its 500-row grid 2 to 6 ms, showing the Board 12 to 20) plus 420 to 545 ms in 7 to 18% of the trials. Every spike fell inside `boardPage.Visible = True`, never in the dispose: Windows paints a window it shows, resizes or brings forward there and then, before the call returns (the Board's "Filter by stage" box was painted inside its `WM_SIZE`), and during a spike the UI thread waited, using 0 to 47 ms of CPU, with no garbage collection. The same wait turned up in a kept page's `BringToFront`, in layout passes, in the `RedrawWindow(RDW_UPDATENOW)` after the manuscript page opened (the versions and details step read 596 ms once) and in one slice of the Board's fill (528 ms, failing an enforced row on a quiet host). With drawing held by `WM_SETREDRAW` there was no spike in 60 trials. The profile ran with the desktop locked and probably the display off; on 2026-10-23 the same wait, 78 to 483 ms with no CPU used, was timed on an unlocked desktop too, in the first paint after 6 of 18 opens of the manuscript page.

The cause is therefore painting, which this machine's screen sometimes keeps waiting, done synchronously inside a switch; not the Library page's dispose. Every page and shelf switch now holds the drawing of the page area (or of the shelves' host) and paints once after it has returned, and nothing forces a paint. `PageSwitchTests.SwitchesPaintNothingSynchronously` holds that: no `WM_PAINT`, `WM_ERASEBKGND` or `WM_NCPAINT` is sent in the page area while any of the seven pages switches to any other, nor in the shelves' host while the Board changes shelves; on `a80a319`'s code it fails with 789 lines, Library to Board's among them. Library to Board then read 12 to 50 ms in 60 switches in a row (median 23), and 13 to 93 ms in the budget runs since, the 93 in a busy run.

That moved the wait; it did not end it. The paint the switch no longer does runs once the window next processes its messages, on the UI thread, before the researcher sees the page, and the screen's wait comes with it: in the review follow-ups' runs (below) the Board's paint after Library to Board read 400 and 430 ms in two of five runs, and the first paint after Insights' second visit 493 ms in one, while the switches themselves stayed at 10 to 50 ms. What the switch rows show is the work of the switch; what the researcher waits for is that row and the paint row after it.

### What changed

- **`a80a319`, pins first.** Before any product change: the shortlist's keyboard map, default actions, focus after each action, reading order, tooltip, legibility and fit (`JournalShortlistRowsTests`), where focus lands entering each page (`KeyboardAccessTests.EnteringAPageByKeyboardKeepsFocusWhereItLandsToday`), and a report-only row for a Pipeline manuscript with three shortlisted journals and tags on the kept page, the open researchers make most, which the budget test never timed (852 to 1,321 ms in the profile).
- **`d42b344`, switches paint nothing until they return.** `RedrawHold` (`Controls/InPlace.vb`) holds a window's drawing and on release invalidates it, frame and children included, without `RDW_UPDATENOW`. One `SwitchPage` serves every page switch: the recovery draft, the timers and focus leave the page while the page area still draws, since a control under a held window cannot take focus; the page left is put away and the next shown with the page area held; the Board is hidden last, after the focus move WinForms makes as a Board holding focus hides. The shelves' host is held while shelves change places. The versions and details step dispatches the paints waiting and then builds its parts, forcing no paint. (The plan's one-shot timer for that step was tried and dropped: `Application.DoEvents` returned before its first tick, so the catalog views and the tests that open a page and process messages once found no details.)
- **`502e5d5`.** The paint after a switch is timed in rows of its own, reported, so the rows that process messages next time their own work.
- **`d6d2d8f`, one layout per container per open.** The kept page lists its containers once and holds them all from before a record loads until it shows: an open of a Pipeline manuscript with three shortlisted journals raised 144 to 194 Layout events before, 76 to 91 after.
- **`54871cc`, the shortlist drawn in one list.** `JournalShortlistList`, on `DeadlineList`'s pattern, draws each row's place, status, name, details, facts and evidence and its Edit... and ⋯ buttons, with the keyboard map, default actions, focus and reading order the pins recorded, and raises its own focus and reorder events (it raises no name-change event; until the review follow-ups below, reorder only when the journals or their order changed). Three shortlisted journals made 20 to 24 windows and a keyboard-cue broadcast over the whole window per Button, then were restyled (131 to 178 ms of the profile's 852 to 1,321); they now make none. The pins hold line for line except the list's own role, now List where the panel was a Client.
- **`ec83959`, the manuscript page prepared while the window idles.** The editor's containers hold their layout while it is built and styled and lay out once each; beside the Board's fill the page is built with no manuscript, laid out a slice at a time, put in the page area hidden at the size it shows at, and given its windows a slice at a time, never inside a part made hidden. Every open, the first included, is then an open of the kept page; an open before the preparation ends finishes it there and then, as every first open did before.
- **`9630acf`, Journals and Insights kept and prepared.** Each is built, placed hidden and given its windows in idle steps after the manuscript page, and kept, as Deadlines and Import & Export are. Every visit to Journals reads the author library and the manuscripts afresh (`JournalLibraryForm.Reload`), since the manuscript page and the Authors view save that library too and the Journals editor saves the whole of it; every visit to Insights begins as a new page did (Your Journals, no journal chosen, the figures worked out anew). A page built in other colors, DPI or font is built anew, and one let go for new system colors is prepared again. An author library that cannot be read while Journals is prepared is left for the visit, which says why, as before.
- **`ba20219`, the other shelves put together once the shown one has filled.** Behind no other page, a step at a time: a shelf's cards in its order, hidden past the first screen, a slice at a time; the shelf laid out at its size and rendered as a shelf shown for the first time is, which puts right anything a save or a search changed meanwhile; its own window; its first screen's windows, a card a step. The first switch shows cards whose windows are made; a shelf switched to before it is ready is built there and then, as before; a shelf prepared while the window was smaller shows its larger first screen at once.
- **This section's commit.** The budget test paints what the test's own edit of the title changed in a reported row before timing Save from the manuscript page (and before the Pipeline save), since the save processes messages while its work runs off the UI thread and timed that paint as its own: the row read 445 and 490 ms in two runs with the desktop locked and 57 to 172 in the others. The paint row reads 1 to 2 ms on an unlocked desktop.
- **Not taken, with the reason.** Drawing the tag chips (the plan's step 3b): their windows took 2 to 20 ms of an open. Building the editor's sections lazily: once the page is prepared in idle time it saves nothing on any row and would break the catalog views that expect the submission's details after an open. Pooling shortlist rows and tag chips: still a window each and a broadcast per open, and the page left must hold nothing of the manuscript. Keeping the Library page: its dispose took 2 to 6 ms; the spike was the paint.

### Numbers

500 library, Release, against the budget x 1. Enforced rows are held x 1 in Release, x 2 in Debug and a further x 3 when `GITHUB_ACTIONS` is set (x 3 for a Release run on the CI runner, x 6 for a Debug one). After: runs C (18:07), D (18:13) and E (18:16) on 2026-10-23; see the host below.

| Action | Before (10-18: 06:24 / 07:37 / 07:39) | After (10-23: C / D / E) | Budget ms | Held |
|---|---:|---:|---:|---|
| Warm startup to an interactive Board | 815 / 321 / 855 | 1244 / 1323 / 1194 | 1500 | enforced (unchanged here) |
| Board: the rest of the shown shelf, in idle slices (the longest idle step, the pages' and shelves' preparation included) | 65 / 528 / 77 | 139 / 177 / 125 | 250 | enforced |
| Idle work left after the fill (the other shelves put together), its longest step | new | 0 / 0 / 0 | 250 | enforced |
| Idle work from startup: its longest step, against the idle slice | new | 139 / 177 / 125 | 30 | reported |
| First switch to Published | 274 / 305 / 337 | 12 / 15 / 14 | 100 | enforced from 10-23 |
| Board to Library | 36 / 33 / 37 | 39 / 58 / 58 | 100 | reported (unchanged here) |
| Library to Board | 285 / 21 / 16 | 16 / 23 / 21 | 100 | enforced from 10-23 |
| Board to Journals | 94 / 116 / 83 | 20 / 25 / 25 | 100 | enforced from 10-23 |
| Journals to Board | 40 / 40 / 34 | 51 / 53 / 83 | 100 | reported |
| Board to Journals (second visit) | new | 10 / 20 / 23 | 100 | enforced |
| Journals to Board (second visit) | new | 58 / 85 / 65 | 100 | reported |
| Board to Insights | 45 / 58 / 68 | 19 / 18 / 18 | 100 | enforced from 10-23 |
| Insights to Board | 16 / 20 / 22 | 16 / 36 / 34 | 100 | enforced |
| Board to Insights (second visit) | new | 39 / 39 / 28 | 100 | enforced |
| Insights to Board (second visit) | new | 19 / 22 / 22 | 100 | enforced |
| Board to a manuscript page, the first (on the page prepared while the window idled) | 510 / 1023 / 823 | 105 / 116 / 103 | 100 | reported: over |
| Manuscript page: its first paint | new | 19 / 27 / 23 | 250 | reported |
| Manuscript page: versions and details after its first paint | 79 / 596 / 61 | 62 / 104 / 109 | 250 | enforced from 10-23 |
| Manuscript page: its paint after the edit | new | 1 / 1 / 2 | 250 | reported |
| Save from the manuscript page | 51 / 53 / 57 | 111 / 67 / 124 | 250 | enforced |
| Manuscript page to Board | 19 / 23 / 27 | 19 / 23 / 23 | 100 | enforced |
| Board: its paint once the manuscript page is left | new | 34 / 43 / 55 | 250 | reported |
| Manuscript page: the record let go of while the window idles | 65 / 58 / 79 | 108 / 82 / 109 | 250 | enforced |
| Board to a manuscript page, the kept page (another manuscript) | 592 / 597 / 140 | 136 / 125 / 121 | 100 | reported: over |
| Kept manuscript page to Board | 25 / 21 / 24 | 23 / 28 / 28 | 100 | enforced |
| Board to a manuscript page, the kept page (a Pipeline manuscript with three shortlisted journals and tags) | not timed (852 to 1,321 in the profile) | 94 / 165 / 108 | 100 | reported: over |
| Board render (RenderManuscripts) | 10 / 8 / 14 | 18 / 21 / 28 | 250 | enforced |
| Save of a Pipeline manuscript whose card changes | 36 / 46 / 32 | 74 / 75 / 89 | 250 | enforced |

UI equals wall for every switch row. The rows this branch did not change read higher in the after runs than in the profile's (warm startup 1,194 to 1,323 ms against 321 to 855, Board to Library 39 to 58 against 33 to 37, Save 67 to 124 against 51 to 57): the machine was busier on 2026-10-23 than on the profile's locked, quiet morning, so the after column understates what this branch changed rather than overstating it. The example library's rows stay report-only, as above; in run C its first switch to Published read 7 ms, Board to Journals 13, Board to Insights 19, the first open 104 and the kept page 98.

How the rows read through the day on this branch, Release, 500 library, from its commits' messages and runs: Board to Journals 10 to 54 ms and Board to Insights 10 to 47 on the prepared pages (their second visits 7 to 56), from 42 to 378 and 40 to 217 built at each visit in this branch's 19 runs before `9630acf`; the first switch to Published 10 to 15 ms in every run since `ba20219`, from 210 to 1,750; Library to Board 13 to 93 ms in every run since `d42b344`.

### Debug

The targeted Debug filter of the brief, against the budget x 2. At 17:21 (on `ba20219`, the desktop in use, a VirtualBox VM running), all 445 tests ran: 440 passed and 5 were skipped (the file-dialog title bar rows, which need `PAPERROUTE_FILE_DIALOG_TESTS`). Its 500 library: the first switch to Published 13 ms, Library to Board 25, Board to Journals 23 and 22, Board to Insights 16 and 35, the versions and details step 113, the first open 91, the kept page 85 and the Pipeline open 128. The filter ran twice more on this section's commit, at 18:39 and 19:05, with the desktop unlocked and no VM but a sibling worktree's (`claude-v092-scaling-step4`) test run for 188 of 341 and 244 of 359 samples: both times 445 tests, 440 passed and 5 skipped. Their 500 library, in that order: the first switch to Published 13 and 7 ms, Library to Board 19 and 35, Board to Journals 24 and 26 (second visits 31 and 18), Board to Insights 17 and 16 (25 and 29), the versions and details step 137 and 142, Save from the manuscript page 88 and 74, the record let go of 156 and 67, the longest idle step 138 and 124; and, reported, the first open 118 and 105, the kept page 162 and 92, the Pipeline open 116 and 71.

### Host during these runs

Windows 11 (NT 10.0.26300), 12 logical processors, window DPI 96, sampled every 3 to 4 s during each run (CPU load, test hosts, builds, VirtualBox processes, seconds since the last input, and whether the lock screen ran). Desktop programs as in the sections above; several sibling worktrees built and tested on and off through the day.

- **Run C, 18:07 to 18:10:** no other test host and no VirtualBox VM; the desktop unlocked and in use (input within seconds); CPU 25 to 65%, mean 42. Every enforced row held.
- **Runs D, 18:13 to 18:16, and E, 18:16 to 18:19:** no VM; the desktop unlocked and idle (no input for 1 to 7 minutes); a sibling worktree's (`claude-v092-scaling-step4`) Debug test run throughout; CPU 21 to 62%, means 39 and 42. Every enforced row held.
- **Busy runs, recorded so the variance is on the record.** Run A (18:00) with a VirtualBox VM running, the desktop in use and the sibling's test host for 9 of 57 samples (CPU up to 100%) failed six rows enforced before this branch: warm startup 1,852 ms, Board to Deadlines 208, Board to Import & Export 122, Save from the manuscript page 274, the record let go of 355 and Restore's inspection 1,400. Run B (18:04), the VM running for 25 of 36 samples, failed warm startup at 1,718 ms. Earlier runs on this branch failed Save from the manuscript page at 445 and 490 ms with the desktop locked (see this section's commit above), and, once, beside a sibling worktree's test host, the versions and details step at 269 ms and Board to Import & Export at 114 ms.

### What stays reported, and what remains over budget

- **The three manuscript-page opens are still over budget on this host.** The first open (on the prepared page), another manuscript on the kept page and a Pipeline manuscript with three shortlisted journals and tags read 103 to 196, 121 to 219 and 94 to 187 ms in the five final runs, and 36 to 196, 32 to 318 and 42 to 190 ms across all of 2026-10-23's Release runs: within 100 ms on a quiet host (36 to 65, 32 to 102 and 42 to 94 in the three runs on `ec83959` with the desktop locked), over it on a busy one. From 510 to 1,023 ms and 64 to 1,321 ms in the profile, they now do the same work at every open: loading the record (7 to 45 ms; 104 to 144 for the first), the header (1 to 9) and showing the page (2 to 38, its tag chips' windows included), and then one layout pass over the editor's 39 containers, 90 to 144 ms of every open in a probe today with a VirtualBox VM running. Each container lays out once, and some more than once: the Overview's scrolling host four times an open, and the Overview's table, the editor form and seven of the page's other containers twice; the page's frame, the Overview's table and its scrolling host take about half of the pass. Reaching 100 ms with room on a busy host would take that pass halved: an Overview that lays out once whatever the record (its scrolling host not laying out again as its contents and the header settle, and fewer nested self-sizing tables, for example the details card's labels and fields in one table), which is a change to the editor's layout that `ManuscriptEditorReuseTests` compare control by control, bounds included, in three themes. It was not taken in this batch, and the three rows are not enforced: on this host they would fail most runs. No feature or edit is at stake; the work is the layout.
- **Journals to Board** (51 to 83 ms; second visits 58 to 85; 28 to 161 across the day) stays reported: leaving reads back the author library its editor saved and renders the Board, as after the Journal Library dialog, and the plan enforced it only at 60 ms or less in three quiet runs.
- **Board to Library** (39 to 93 ms) was not part of this batch and stays reported.
- **The first open before the page is prepared**, in the first second or so after startup, builds the page there and then as every first open did before, 510 to 1,023 ms in the profile; it is not a budget row.
- **The idle steps against the idle queue's 30 ms slice** (reported): the longest idle step from startup read 87 to 177 ms in the final runs. Timed one by one by a probe (not kept): the fill's own steps about 25 to 84 ms (a card's windows), the manuscript page's preparation steps 27 to 94, Journals' 26 to 108 (its editor's constructor), Insights' 31 to 52, the shelves' cards 26 to 41 a step, the shelf's render 21 to 95, its window 15 to 61 and a card's windows 7 to 32. Each is under the UI thread's 250 ms, which is held, and none is cut further.
- **What the prepared pages and shelves cost.** They add windows to the main window while they wait hidden: about 100 for the manuscript page, Journals and Insights, and about 300 for the two hidden shelves' first screens (2,234 windows against 1,936 after startup's idle work on the 500 library). Each keyboard-cue broadcast reaches them. Alternating boards with and without the shelves prepared, a probe measured kept-page opens at medians of 96 to 121 ms with them and 74 to 140 without; the difference between rounds was larger than between the two, so no cost on an open could be told from this host's noise.
- The **example library** runs first and carries the process's first-time costs, so its rows stay report-only.
- The **paint rows** (the first paint after an open, the Board's after a leave, the page's after the edit, and from the review follow-ups the paint after Library to Board, after each visit to Journals and Insights and after the first switch to Published) are reported, as Deadlines' painting is: they hold the half-second wait this machine's painting can take. It is the screen's wait, not the work's, but the researcher waits through it on the UI thread all the same.

### Tests that hold this section

`PageSwitchTests` (`SwitchesPaintNothingSynchronously`; `LeavingJournalsAndInsightsHidesThemInPlace`; `JournalsAndInsightsArePreparedWhileTheWindowIdles`; `TheJournalsPageShownAgainReadsTheLibraryAfresh`; `AnInsightsVisitBeginsAsANewPageDid`; `JournalsAndInsightsArePreparedAnewAfterTheSystemColorsChange`; `PageWindowsAreCreatedOnceInPlace` with second visits; `LeavingAPageDisposesItInPlace` for the Library), `BoardFillTests.OtherShelvesArePreparedAfterTheFill`, `KeptManuscriptPageTests.ThePageIsPreparedWhileTheWindowIdles` and `AnOpenMakesNoWindowButItsTagChips`, `JournalShortlistRowsTests` (the pins), `KeyboardAccessTests.EnteringAPageByKeyboardKeepsFocusWhereItLandsToday` and `LeavingAPageByKeyboardKeepsFocusWhereItLandsToday`, `ManuscriptEditorReuseTests` in Light, Dark and a contrast theme, and the catalog's shortlist stage, which shows the drawn shortlist. (As first written, this line said the names sweep read the drawn shortlist; it did not, since no view it read showed a shortlisted journal. Since the review follow-ups below, the names and default-action sweeps read that stage too.) Each new test was run against the code before its commit and failed there (see each commit's message).

### Review follow-ups (2026-10-23, evening)

A review of the branch found claims above that went further than what was shown, and a few defects. Each was taken up in a commit of its own on this branch; each fix that changes behaviour comes with a test that failed on the code before it.

- **The paint after a switch is timed.** Reported rows timed with `PaintNow` follow Library to Board, both visits to Journals, both visits to Insights (before their figures, whose rows process messages) and the first switch to Published, as one already followed each manuscript-page open; before, nothing timed that paint, and the next row hid the Board again. The summary at the top of this table and the cause paragraph above are reworded to match.
- **Insights begins each visit with nothing of the last.** A kept Insights page cleared its view only when a tab other than Your Journals was checked, and never its tiles, so a visit showed the last visit's tiles, tab counts and, left on Your Journals, its grid under "Calculating from your records..." until the figures came back. It now hides its tiles and view and takes the counts off its tabs until `ShowInsights`; hidden rather than destroyed, so the switch does no more work (`AnInsightsVisitBeginsAsANewPageDid`, which looks before the figures are back).
- **The author library is read before the page area is held.** Leaving Journals or the Authors view reads back the library the editor saved; since `d42b344` that read ran inside the hold, and it can show the "Previous Save Opened" notice (#111), which then sat over a page area that could not paint. It runs before the hold now; only the Board's render stays inside (`LeavingJournalsReadsTheAuthorLibraryWhileThePageAreaDraws`). When and what the library saves is unchanged.
- **The drawn shortlist.** Its evidence tooltip, set on the list, is cleared when the pointer leaves and when the rows are replaced, so the record let go of keeps none; the reorder event follows what a reader reads of the rows (status, details, facts, evidence, what the menu allows), so Make Target Journal and an Edit... are announced; a button is focused only while the list has the keyboard, not behind another window. New pins: each Tab onto a drawn button is heard by a WinEvent hook, in order; a click on each Edit... and ⋯, a press let go off a button, a right-click and the context-menu key on each button (the key opening the row's menu is new with the drawn list). The pins' page now has the keyboard first, as a researcher's window does; every earlier pin holds line for line.
- **The sweeps read the drawn shortlist.** Both the names sweep and the default-action sweep read the catalog's shortlist stage, and neither can pass by finding no drawn row.
- **Focus as a Board holding the keyboard hides.** A pin with real focus on a card title, opened with Enter and left with Alt+Left; with the Board hidden as `f5c6947` hid it, the three lines were the same. Both land the keyboard in the hidden Board's search box behind the manuscript page until it is moved, as the pins without the keyboard record it "on the hidden Board"; that is the window's behaviour from before this branch, and is not changed here.
- Smaller: `LibraryBoard.PaintNow` calls the product's `PumpPaints` instead of a copy; the reuse test judges drawn parts with the rows test's contrast helpers; two unused members are gone.
- **Left as it is:** commit `a80a319` ends with a different co-author line from the branch's others; no history is rewritten here, so it is for the merge to correct if the branch is squashed.

Five Release runs of the budget test on the follow-ups' last commit, 500 library, against the budget x 1: F (22:02) beside a sibling worktree's (`claude-v092-scaling-step4`) Debug test run, CPU 82% as it began; G (22:08), H (22:13), I (22:17) and J (22:20) with no other test host and no VM, no input for over an hour (the lock screen up in H to J; not sampled in F and G), CPU 0 to 82%, means 42 to 51, the run's own. Run G failed one enforced row, the Board's fill, whose longest idle step read 528 ms against 250: the screen's wait inside a slice that shows cards, as the profile once recorded (see the cause paragraph above); the fill is not changed by this branch's follow-ups. Every other enforced row held in all five.

| Action | F / G / H / I / J | Budget ms | Held |
|---|---:|---:|---|
| First switch to Published | 13 / 11 / 11 / 8 / 11 | 100 | enforced |
| Published: its paint after the first switch | 23 / 19 / 17 / 4 / 23 | 250 | reported (new) |
| Library to Board | 37 / 28 / 11 / 16 / 10 | 100 | enforced |
| Board: its paint after Library to Board | 86 / 48 / 400 / 430 / 123 | 250 | reported (new) |
| Board to Journals | 23 / 12 / 15 / 16 / 13 | 100 | enforced |
| Journals: its paint after the switch | 7 / 6 / 5 / 6 / 5 | 250 | reported (new) |
| Board to Journals (second visit) | 25 / 16 / 14 / 23 / 12 | 100 | enforced |
| Journals: its paint after the switch (second visit) | 8 / 7 / 6 / 6 / 6 | 250 | reported (new) |
| Board to Insights | 25 / 25 / 23 / 22 / 18 | 100 | enforced |
| Insights: its paint after the switch | 9 / 4 / 4 / 4 / 6 | 250 | reported (new) |
| Board to Insights (second visit), with the last visit's figures hidden | 44 / 34 / 46 / 32 / 50 | 100 | enforced |
| Insights: its paint after the switch (second visit) | 5 / 3 / 3 / 4 / 493 | 250 | reported (new) |
| Board to a manuscript page, the first | 90 / 113 / 123 / 99 / 81 | 100 | reported |
| Manuscript page: its first paint | 367 / 25 / 26 / 481 / 23 | 250 | reported |
| Manuscript page: versions and details after its first paint | 87 / 130 / 237 / 65 / 77 | 250 | enforced |
| Board: its paint once the manuscript page is left | 490 / 45 / 59 / 52 / 437 | 250 | reported |
| Board to a manuscript page, the kept page (another manuscript) | 87 / 48 / 120 / 108 / 73 | 100 | reported |
| Board to a manuscript page, the kept page (a Pipeline manuscript with three shortlisted journals and tags) | 125 / 99 / 138 / 149 / 82 | 100 | reported |

Board to Insights' second visit read 32 to 50 ms against 28 to 39 in runs C to E, within the 7 to 56 ms the second visits to Journals and Insights read through the day before the change; hiding two panels and retitling two tabs, under the hold, is the only work added to it. Every enforced row here held in every run; the three manuscript-page opens stay reported, 48 to 149 ms, over 100 in some runs as above. Seven paint readings of the five runs were over 250 ms: the Board's after Library to Board twice (400, 430), Insights' after its second visit once (493), the manuscript page's first paint twice (367, 481) and the Board's after the manuscript page twice (490, 437). Each fell on the first paint after a switch, never in a switch, and is of the size of the wait the profile timed with no CPU used (CPU was not timed per paint here); the same paints read 3 to 123 ms in the other runs. The other switch rows (Deadlines, Import & Export, and the switches back to the Board) likewise end before their paint since `d42b344`, and their paint is timed only where a row after them processes messages.

The targeted Debug filter of the brief ran once on the follow-ups (22:27 to 23:02), with two sibling worktrees' (`claude-v092-scaling-step4` and `-step4b`) Debug test runs beside it for most of it and the lock screen up: 450 tests, 444 passed, 5 skipped (the file-dialog title bar rows, which need `PAPERROUTE_FILE_DIALOG_TESTS`), and the budget test failed two enforced rows these commits do not change, against the budget x 2: the Board fill's longest idle step at 572 ms and the versions and details step at 513, with the Board's paint after Library to Board at 620 and Insights' after its second visit at 478 in the same run. The budget test alone in Debug then failed the versions and details step at 981 ms beside one sibling run (Journals' paint after its second visit 537 in it), and passed at 23:28 with no other test host: every enforced row within x 2, Library to Board 34 ms and its paint 48, Board to Journals 23 and 24 (paints 7 and 8), Board to Insights 31 and 50 (paints 7 and 8), the first switch to Published 11 (paint 19), the versions and details step 157. Each failure is a reading of 440 ms or more on a step that takes 65 to 237 ms in Release, the size of the screen's wait; on this host a failure of these rows is run again on a quiet host before it is read as a regression, as the budget test's notes say.

### Merge notes (2026-10-24)

- **Merged:** master `b03aa20` (#208 to #211: the File Drawer's ways in and out, steps 0 to 3 of the scaling work, the display evidence tool) at `1fe715e`, then `fix/v092-scaling-step4` (#212, step 4 of #93, not yet on master) at `578ed94`. Neither had a textual conflict.
- **What overlapped, and why nothing changed.** In EditManuscriptForm, master sets the File Drawer group's visibility from the working copy's location after a submission or a decision (`RefreshLifecycleControls`, `SubmissionDetailChanged`), as `LoadManuscript` already did for every record the kept page loads, and Record Submission... starts at the Target journal box's text. #209's `StartSizes` runs once, in `BuildInterface`, before `Form1_Load` queues the fill and the pages' preparation, so the pages prepared hidden are laid out at the capped size. A card's menu is still built on first request (`CardPanel`), which is how FileDrawerTests reach Move to File Drawer... and Restore to Pipeline. #212 lays Submission Details' inline header out by its tables, with no resize handler, so details built after the page's first paint, while the Submissions section is hidden, take their width when it shows; SubmissionDetailsLayoutTests' hosted part takes that path at the main window's minimum size.
- **The rows time the same window here.** The main window is now capped to the working area (#209). This host's test process sees a 1536x912 working area at 96 DPI, so the window opens at 1280x840 as before (912 less the 72-pixel margin is 840). On a smaller screen, as the 1024x768 build runner's, the window is shorter, and so are the pages prepared in it and the shelves' first screens.
- **Not measured again in Release.** Master's code now runs on rows this section measured without it: #210's changes to `ManuscriptLifecycleService.ReconcileFromLatestWorkflow`, which each record's load runs; #203's filing check (`CanSuggestFiling`) on each Pipeline card; and #212's header table in each submission's details. The targeted Debug filter ran on each merge, against the budget x 2, with no VM: on `1fe715e` 449 tests, 444 passed, 5 skipped; on `578ed94` 457 tests, 452 passed, 5 skipped (the file-dialog title bar rows each time). Every enforced row held in both. The three reported opens read 159, 650 and 221 ms in the first run, in which rows no merged change touches read higher too (Board to Import & Export 103 ms, against 49 in the second; CPU was not sampled), and 94, 81 and 82 ms in the second. Two report-only rows read over twice their 250 ms UI-thread budget: the example library's longest idle step after the fill, 529 ms on `578ed94` (109 on `1fe715e`), and Find Journals' first paint on the 500 library, 541 on `1fe715e` (239 on `578ed94`); both are Debug readings taken beside other test hosts, read as host load as the 650 is.
- **Left to the pull request's full run.** SubmissionWorkflowConsistencyTests (9 tests, among them Record Submission...'s start at the target journal), VersionEditorScrollTests (1) and UnderlineTabControlTests (2) fall outside the targeted filter, as do 15 of ManuscriptLifecycleServiceTests' 18; the full Release suite on the pull request runs them, and their results in that run's trx are read before it merges.

## 2026-10-24: the keyboard on the page opened, and micro-motion (#213, #142)

Branch `fix/v092-motion`, stacked on speed part C (`fix/v092-speed-pages` at `aca56a6`, PR #215, not yet on master; that branch's later `039e83b`, a test-only change, is not in this one). It takes #213 (opening a page from the Board by keyboard left the keyboard in the hidden Board) and #142 (micro-motion: hover and press states and a page cross-fade). Both change the page switch part C rewrote, so the switch rows were measured again with motion off and with motion on. The sections above are history and were not changed.

### What changed

- **`7ec79ea`, #213.** A Board that has the keyboard (Windows' focus, or the window's own record of the focused control) gives it to the page shown: the manuscript page's Overview tab, a rail page's own first control, or the window's first stop for a page with none. Going back to the Board puts it on the control it came from, or on the new card's title if a save rebuilt the card. Both moves are made under the page area's `RedrawHold`: made after the hold, focusing the Overview tab painted it synchronously inside the switch (a radio button entered without Tab repaints itself), which `SwitchesPaintNothingSynchronously` caught.
- **`a934035`, hover and press (#142).** `UiMotion` reads Windows' Animation effects setting once a session, can be forced by tests, and is off under a contrast theme whatever it says. One Windows Forms clock per UI thread eases ActionButton, FilterChip, the rail's items and a Board card's border over about 100 ms, each step repainting only the control eased; a press shows at once, and a card's highlight for the keyboard is immediate. The colors end on the theme's tokens. A toned filter chip eases toward the card surface: the plan's step toward the text, the rail's rule, left a hovered warning chip at 3.79:1 (it is 4.51:1 at rest).
- **`ede1a48`, the cross-fade (#142).** As a switch begins, a worker reads the page area off the screen and puts the picture, hidden, in an overlay of its own over the page area; once the switch has returned the overlay is shown and faded out over 150 ms on the window's clock. A picture not ready, a switch that took 150 ms or more, a switch within 50 ms of a fade, a manuscript's open, or motion off is a cut, as before. The overlay is an owned, disabled, layered, click-through popup that never activates, is not a control, and is in UI Automation's raw view only; the first key, button, wheel or touch ends the fade and goes on to the page. SyncRail holds the rail's drawing, so a switch draws nothing of the window synchronously. Idle work waits while a fade runs (at most its length), and Insights' figures end a fade still running as they arrive.
- **This section's commit, the budget test.** `EveryActionIsMeasuredOnBothLibraries` runs with motion forced off and fails if any switch faded. The new `PageSwitchesWithMotionOnGrowByNoMoreThanTheFade` runs the switches that test enforces, on boards of their own, once with motion off (reported) and once with motion on, each followed by its paint and the same pause (260 ms of the window processing its messages, and at least 80 ms after a fade has ended, so the screen has stopped showing it); it holds each switch with motion on to the page-switch budget plus the fade's length, 100 + 150 ms, and fails if nothing faded or if a switch quicker than the fade was a cut for any reason but a picture not read in time or a manuscript's open. Its rows are enforced x 1 in Release, x 2 in Debug and a further x 3 when `GITHUB_ACTIONS` is set, as every other enforced row.

### Motion off: the switch rows, as part C holds them

500 library, Release, `EveryActionIsMeasuredOnBothLibraries` with motion forced off, against the budget x 1; the branch's last commit. Runs A (10:02), B (10:08), C (10:12) and D (10:22) on 2026-10-24; see the host below. UI equals wall for every switch row.

| Action | A / B / C / D | Budget ms | Held |
|---|---:|---:|---|
| First switch to Published | 5 / 13 / 6 / 9 | 100 | enforced |
| Board to Library | 26 / 509 / 59 / 60 | 100 | reported |
| Library to Board | 8 / 22 / 20 / 17 | 100 | enforced |
| Board to Journals | 7 / 12 / 12 / 14 | 100 | enforced |
| Journals to Board | 27 / 38 / 42 / 50 | 100 | reported |
| Board to Journals (second visit) | 6 / 17 / 13 / 14 | 100 | enforced |
| Journals to Board (second visit) | 24 / 62 / 44 / 33 | 100 | reported |
| Board to Deadlines | 18 / 61 / 37 / 34 | 100 | enforced |
| Deadlines to Board | 10 / 26 / 18 / 12 | 100 | enforced |
| Board to Deadlines (second visit) | 6 / 23 / 14 / 11 | 100 | enforced |
| Deadlines to Board (second visit) | 8 / 21 / 10 / 12 | 100 | enforced |
| Board to Insights | 7 / 20 / 19 / 9 | 100 | enforced |
| Insights to Board | 15 / 19 / 19 / 17 | 100 | enforced |
| Board to Insights (second visit) | 11 / 24 / 34 / 15 | 100 | enforced |
| Insights to Board (second visit) | 8 / 20 / 21 / 16 | 100 | enforced |
| Board to Import & Export | 28 / 69 / 60 / 47 | 100 | enforced |
| Import & Export to Board | 7 / 18 / 15 / 18 | 100 | enforced |
| Board to Import & Export (second visit) | 4 / 7 / 6 / 5 | 100 | enforced |
| Import & Export to Board (second visit) | 8 / 19 / 16 / 19 | 100 | enforced |
| Board to a manuscript page, the first | 32 / 124 / 81 / 58 | 100 | reported |
| Manuscript page: versions and details after its first paint | 34 / 182 / 78 / 55 | 250 | enforced |
| Manuscript page to Board | 12 / 19 / 18 / 15 | 100 | enforced |
| Board to a manuscript page, the kept page (another manuscript) | 41 / 80 / 67 / 46 | 100 | reported |
| Kept manuscript page to Board | 10 / 14 / 16 / 13 | 100 | enforced |
| Board to a manuscript page, the kept page (a Pipeline manuscript with three shortlisted journals and tags) | 48 / 96 / 91 / 47 | 100 | reported |

Every enforced switch row held in all four runs, within the ranges part C's runs read on 2026-10-23 (the first switch to Published 8 to 15, Library to Board 10 to 37, Board to Journals 12 to 25, Board to Insights 18 to 25 and its second visit 28 to 50; Board to Import & Export's first visit 35 to 98 in this branch's runs before the cross-fade). Run B, beside a sibling worktree's Debug test run, failed three enforced rows no switch is part of: warm startup 1,667 ms, the Board fill's longest idle step 380 and the save of a Pipeline manuscript whose card changes 407; Board to Library, reported, read 509 there. Runs A, C and D held every enforced row. On the same host before the cross-fade (`7ec79ea`, 07:27, and `a934035`, 08:11, motion following Windows' setting, on), the switch rows read in the same ranges.

### Motion on against motion off, switch by switch

500 library, Release, `PageSwitchesWithMotionOnGrowByNoMoreThanTheFade`, three runs at 10:18, 10:19 and 10:21 with no other test host: each switch with motion off and then with motion on, on boards of their own, with the same pauses (in these runs 220 ms; see below). Off and on are runs 1 / 2 / 3; the difference is of the medians.

| Switch | Motion off ms | Motion on ms | On less off | With motion on (Begin, the picture shown) |
|---|---:|---:|---:|---|
| Board to Library | 28 / 29 / 24 | 33 / 72 / 48 | +20 | faded 3 of 3 (1.7 to 3.2 ms) |
| Library to Board | 8 / 17 / 17 | 9 / 18 / 18 | +1 | faded 3 of 3 (1.5 to 2.4) |
| Board to Journals | 8 / 11 / 11 | 10 / 10 / 12 | -1 | faded 3 of 3 (1.0 to 1.9) |
| Journals to Board | 21 / 41 / 45 | 41 / 25 / 19 | -16 | faded 3 of 3 (1.1 to 2.0) |
| Board to Journals (second visit) | 7 / 8 / 9 | 7 / 10 / 7 | -1 | faded 3 of 3 (1.1 to 1.2) |
| Journals to Board (second visit) | 36 / 23 / 37 | 35 / 39 / 31 | -1 | faded 3 of 3 (1.3 to 1.7) |
| Board to Deadlines | 21 / 17 / 31 | 19 / 23 / 21 | 0 | faded 3 of 3 (1.1 to 1.4) |
| Deadlines to Board | 8 / 11 / 13 | 11 / 12 / 8 | 0 | faded 3 of 3 (1.0 to 1.3) |
| Board to Deadlines (second visit) | 8 / 7 / 7 | 8 / 12 / 8 | +1 | faded 3 of 3 (1.3 to 1.6) |
| Deadlines to Board (second visit) | 7 / 9 / 13 | 13 / 17 / 9 | +4 | faded 3 of 3 (1.1 to 2.0) |
| Board to Insights | 8 / 15 / 14 | 14 / 19 / 15 | +1 | faded 3 of 3 (1.6 to 2.3) |
| Insights to Board | 13 / 9 / 7 | 11 / 10 / 10 | +1 | faded 3 of 3 (1.2 to 1.9) |
| Board to Insights (second visit) | 12 / 25 / 25 | 15 / 16 / 17 | -9 | faded 3 of 3 (1.4 to 1.6) |
| Insights to Board (second visit) | 13 / 15 / 10 | 10 / 10 / 9 | -3 | faded 3 of 3 (0.9 to 1.4) |
| Board to Import & Export | 28 / 28 / 24 | 27 / 25 / 26 | -2 | faded 3 of 3 (1.1 to 1.2) |
| Import & Export to Board | 10 / 14 / 8 | 16 / 13 / 12 | +3 | faded 3 of 3 (1.1) |
| Board to Import & Export (second visit) | 3 / 5 / 5 | 6 / 8 / 6 | +1 | faded 3 of 3 (1.2 to 1.8) |
| Import & Export to Board (second visit) | 9 / 9 / 11 | 12 / 15 / 11 | +3 | faded 3 of 3 (1.1 to 1.2) |
| Board to a manuscript page, the first | 52 / 34 / 39 | 41 / 36 / 47 | +2 | a cut by design, 3 of 3 |
| Manuscript page to Board | 10 / 13 / 14 | 10 / 11 / 14 | -2 | faded 3 of 3 (1.0 to 1.5) |
| Board to a manuscript page, the kept page | 38 / 38 / 43 | 39 / 32 / 31 | -6 | a cut by design, 3 of 3 |
| Kept manuscript page to Board | 14 / 9 / 14 | 11 / 9 / 10 | -4 | faded 3 of 3 (1.0 to 1.2) |

Every switch that could fade faded in every run (20 of 22; the two opens are cuts), and no picture was late. With motion on, a switch grew by 0.9 to 3.2 ms of showing the picture, and by -16 to +20 ms in all, the median of the three runs, against the fade's 150: well within the page-switch budget plus the fade's length, and within the page-switch budget itself. Board to Library, the first switch of each board, makes the overlay; its +20 is the largest difference and the noisiest row (33 to 72 ms). The pauses let the processor idle, so these rows read higher than the same switches straight after other work in the table above (Library to Board 8 to 17 here, against 8 to 22 there; the opens 31 to 52 against 32 to 124). The interleaved measurement in `MotionFadeTests.AReadInFlightHoldsNoSwitch` (Debug, the two modes taking turns a page at a time, 50 switches each) read a median of 15 ms on against 12 off off the screen, and 23 against 21 on the screen with the display on.

The pause was then made to end at least 80 ms after a fade, since in a Debug run a fade that ended late left the next switch a cut ("the last fade had only just ended", Board to Import & Export). With that pause, three more Release runs (10:34, 10:35 and 10:37) ran beside a sibling worktree's test run (22 of 23, 21 of 28 and 26 of 30 samples): every switch that could fade faded but one, Begin read 1.1 to 4.6 ms, and the medians' differences read -27 to +31 ms, the noise of that host. The first of them failed one enforced row: Import & Export to Board with motion on, 468 ms, a cut because it outlasted the fade; in the same run the motion-off pass read 428 ms for the kept manuscript page to Board, and the third run's motion-off pass 563 for Board to Insights' second visit and 341 for Board to Import & Export: the host's half-second stall, which falls on a switch with motion off as with motion on, as part C's notes say of the main test. The other two passed. Debug, against the budget x 2, at 10:40 beside the same sibling run: passed, 20 of 22 switches faded, every switch with motion on 6 to 86 ms.

### What the fade's proofs found (the plan's 4a, 4b and 4c)

- **4a, the read and the switch.** As planned, the worker first read the window's own device context. Whenever that read waited on the screen, the switch waited with it: every change a switch makes to the window's child windows updates the window's device contexts, and one held by a waiting read held the switch (Debug, 50 switches on the 500 library, off the screen: 5 reads of 50 ms or more, up to 459, held 5 switches, up to 470; 15 in a run on a busier host). Holding the rail's drawing did not help. Read off the screen's device context instead, the reads stopped holding the switch; with the display asleep (no input for half an hour) they waited 256 ms (median) and still held 3 of 50 switches, up to 449 ms, while the rail's two buttons drew themselves synchronously as they were checked. SyncRail now holds the rail's drawing. A run with the display on afterwards read no read of 50 ms or more (17 ms median, 36 longest) and held no switch, so whether the rail's hold alone frees a switch from a read that waits that long is not measured. Showing the picture on the UI thread also waited once (522 ms in 50 switches); the worker puts the picture in the overlay, and the switch only shows it (0.9 to 3.2 ms in Release above). With the read held 400 ms by a test seam, every switch returned in 8 to 28 ms, a cut.
- **4b, the picture is the page left.** `MotionFadeTests.ThePictureIsThePageLeft` compares what the screen shows under the opaque overlay with what it showed before the switch, on an unlocked desktop with `PAPERROUTE_ON_SCREEN_TESTS=1`. Run at 09:25 and 09:57 it was Inconclusive: the screen read the same before and after a switch, the display asleep. Off the screen, where every other test runs, a read of the screen returns nothing of the window, so the harness times the reads and the overlay but cannot check the picture.
- **4c, manuscript opens.** The page's versions and details are built on the UI thread once it has painted (the enforced row above: 34 to 182 ms in Release today, 85 and 189 in the step runs), more than a frame, so a fade over an open would hold the picture of the page left over the manuscript page that long and then cut. Opens stay a cut; leaving the manuscript page fades. The maintainer's alternatives: frames off the UI thread (which needs #142's "on a Windows.Forms.Timer" relaxed), or building the deferred parts after the fade, as they already wait for library work.
- **The overlay's life.** Left alone, the overlay was hidden 148 to 178 ms after the switch returned (Debug); when the new page's paint held the thread longer, it went at the first frame after (254 ms once, the thread held 249), since its frames run on the window's thread. #142's "disposed within 200 ms" is met as hidden and kept, one overlay per window, as the kept pages are; destroying it after each fade would cost about 2.6 ms there and 1.5 ms more at the next switch (the profile's figures).
- **Paints.** Shown with `ShowWindow`, the overlay had every window of the page just shown sent `WM_NCPAINT` and `WM_ERASEBKGND` inside the switch (5,276 in `SwitchesPaintNothingSynchronously` with motion on); shown and hidden with `SetWindowPos` and `SWP_DEFERERASE`, the test holds at zero with motion off and on.

### Host during these runs

Windows 11 (NT 10.0.26300), 12 logical processors, window DPI 96 (the display at 125%), sampled every 3 s (CPU load, test hosts, builds, VirtualBox processes, seconds since the last input). No VirtualBox VM ran. The lock screen's process ran through most of the morning, though the desktop was in use at times.

- **Run A, 10:02 to 10:05:** no input for 27 minutes or more; a sibling worktree's test host for 5 of 52 samples; CPU 0 to 91%, mean 16.
- **Run B, 10:08 to 10:11:** the desktop in use at the end; a sibling worktree's test host for 35 of 47 samples; CPU 0 to 100%, mean 34. The busy run.
- **Run C, 10:12 to 10:15:** no input for 1.5 minutes or more; a sibling's test host for 7 of 39 samples; CPU 0 to 97%, mean 20.
- **Run D, 10:22 to 10:24:** no input for 12 minutes or more; a sibling's test host for 1 of 31 samples; CPU 0 to 53%, mean 21.
- **Motion runs 1 to 3, 10:18 to 10:22:** no other test host; no input for 7 minutes or more; CPU means 11, 17 and 26.
- **Motion runs with the longer pause, 10:34 to 10:40:** a sibling worktree's test host for most of each (22 of 23, 21 of 28, 26 of 30 samples); no input for 23 minutes or more; CPU 0 to 76%, means 26, 20 and 19.

### What remains, and what stays over budget

- **Still over budget, reported as before, and not changed here:** the three manuscript-page opens (the first 32 to 124 ms, the kept page 41 to 80, the Pipeline manuscript 47 to 96 in runs A to D), whose cost is the editor's one layout pass (see 2026-10-23), and Board to Library in the busy run B (509 ms; 26 to 60 in the others), which builds its page and its 500-row grid at each visit. The paint rows and the idle slice stay reported as in part C.
- **Left to the release certification:** the Acceptance's captures (a mid-fade frame, and a card and a button at rest, hover and press, at 125% in Light and Dark), which need the harness on an unlocked desktop with the display on and the overlay captured from the screen, which the harness's `PrintWindow` does not reach; the on-screen check that the picture is the page left; and the Narrator pass on the certification VM for #213 (the page is announced, not the search box) and #142 (nothing new is announced).
- **For the maintainer:** opens as a cut (4c above); the overlay kept hidden rather than destroyed; the setting read once a session, as #142 asks, so a change in Windows' setting takes effect at the next start (a contrast theme stops motion at once).

### Review follow-ups (2026-10-24, afternoon)

A review of this branch found that the fade's picture could hold PaperRoute's own windows, that several of the claims above had no test that could fail, and that some test seams were never used. The commits after `c74ba2b`:

- **`63471d2`, more cuts.** Leaving the manuscript page after the Unsaved Changes question is a cut ("a question was just answered"), whatever the answer. The message box is centred over the page, and with Animation effects on Windows animates its close, so the read could hold the question and fade it out over the page shown. A switch begun while another visible window of the window's thread, such as a tooltip going away, is over the page area is a cut too ("another window is over the page area"). The check (`EnumThreadWindows`) took 0.006 ms a call in Release with the window's 12 top-level windows. An overlay Windows cannot make (out of window handles) is also a cut, so the fade never stops a switch. Covered by `MotionFadeTests.LeavingAfterTheUnsavedChangesQuestionIsACut` and `AnotherWindowOverThePageAreaMakesTheSwitchACut`.
- **`cebc43c`, the overlay through UI Automation.** `TheOverlayTakesNoFocusInputOrPlaceInTheTree` now asks UI Automation from a worker thread, the way a screen reader does (`CUIAutomation`). The overlay's element is neither a control, nor content, nor focusable. The control view passes over it, and the window's control-view children are the same with it showing as with it gone; in the raw view it is the window's child. With the overlay's `WM_GETOBJECT` branch disabled for one run, the test failed: the overlay read as a control, a content element and keyboard-focusable, in the window's control view. The in-process `AutomationProperty` helper is gone.
- **`c5145c2`.** `HoldAtMilliseconds`, `LastPictureStarted` and `LastPictureFinished`, which nothing used, are removed. A stale note on hovered filter chips is corrected.
- **`6aa56aa`, click-through.** In both rows of `AClickDuringTheFadeEndsItAndReachesTheControlUnderThePointer`, the overlay must answer `WM_NCHITTEST` at the tab with `HTTRANSPARENT`. A second row runs on the screen (with `PAPERROUTE_ON_SCREEN_TESTS=1`) and requires `WindowFromPoint` at the tab, with the overlay showing, to find the tab and the click to check it. Run at about 12:48 (the commit body says 12:55), it found the lock screen's window (`LockScreenBackstopFrame`) over the whole screen, although the input desktop could be switched to, which is the check both on-screen tests make. With a check without the fade first, it is now Inconclusive whenever another window covers the tab. It is left for a run on an unlocked desktop that is in use, with the captures.
- **`59ace02` and `17d3073`, the rail and the way back to the Board.** `SwitchesPaintNothingSynchronously` watches the whole rail through every switch. With SyncRail's hold taken out for one run, the motion-off row failed with two `WM_PAINT`s to the rail's buttons on every page switch. The hold stays with motion off. `KeyboardAccessTests.ReturningAfterASaveOrADeleteLandsOnAShownControl` drives `ReturnFocusToBoard`'s other two branches. After a save that rebuilt the card, the keyboard lands on the new card's title, shown, in the Board. After a delete, it lands on the collapse button, outside the Board. With either branch disabled, the test failed. The Alt+Left rows of `EnteringAPageByKeyboardKeepsFocusWhereItLandsToday` now list each control entered, in turn. The keyboard passes through Collapse navigation, as the page is left, before the Board's control takes it: two focus changes. That hop is new with #213 and is not removed here, and the Narrator pass (`Narrator-Pass.md`) now listens for it. If Narrator speaks it, the fix is to show the Board and select the control it came from under the hold, before the page is put away, in place of `MoveFocusOutOf`. That reorders part C's switch.
- **`f783dd8`.** Tests that force motion on also turn the contrast seam off, so on a host with a contrast theme they still test motion.
- **This section's commit, the budget test.** `PageSwitchesWithMotionOnGrowByNoMoreThanTheFade` also fails any enforced switch that, with motion on, takes more than the fade's length (150 ms × the multiplier) longer than its motion-off row, as #142's Acceptance puts it. Before, it held only the absolute 100 + 150 ms.

#### Release, 500 library, after the follow-ups

Run 4 (14:16) passed both budget tests. The lock screen was up with no input for 3 minutes or more, and a sibling worktree's Debug test host ran for 15 of 56 samples (14:17:54 to 14:18:52, the end of the first test and the start of the second); CPU 2 to 76%, mean 23. With motion off (`EveryActionIsMeasuredOnBothLibraries`), every enforced row held:

| Switch, motion off | ms | Held |
|---|---:|---|
| First switch to Published | 5 | enforced |
| Board to Library | 25 | reported |
| Library to Board | 11 | enforced |
| Board to Journals / second visit | 8 / 9 | enforced |
| Journals to Board / second visit | 25 / 35 | reported |
| Board to Deadlines / second visit | 36 / 21 | enforced |
| Deadlines to Board / second visit | 18 / 10 | enforced |
| Board to Insights / second visit | 11 / 15 | enforced |
| Insights to Board / second visit | 19 / 11 | enforced |
| Board to Import & Export / second visit | 36 / 4 | enforced |
| Import & Export to Board / second visit | 15 / 15 | enforced |
| Board to a manuscript page, the first | 46 | reported |
| Manuscript page: versions and details after its first paint | 39 | enforced (250) |
| Manuscript page to Board | 18 | enforced |
| Board to a manuscript page, the kept page / the Pipeline manuscript | 36 / 47 | reported |
| Kept manuscript page to Board | 11 | enforced |

With motion on against motion off (`PageSwitchesWithMotionOnGrowByNoMoreThanTheFade`, the same run), 20 of 22 switches faded (the two opens are cuts by design), and no picture was late. Showing the picture (Begin) took 1.3 to 4.2 ms. The differences, on less off, ranged from -19 to +19 ms over the switches that fade, against the 150 allowed: Board to Library 44 / 53, Library to Board 15 / 15, Journals to Board 25 / 44 and its second visit 43 / 24, Board to Deadlines 23 / 22, Board to Import & Export 33 / 33, Import & Export to Board 12 / 24, Manuscript page to Board 14 / 13, kept page to Board 11 / 13. The first open read 57 / 88 (a cut).

The three runs before it fell on a busier host, and none of their failures is on the code these commits changed:

- **Run 1 (13:29).** The desktop was in use and a sibling's Debug test host ran throughout (139 of 139 samples, CPU mean 53). The main test failed seven enforced rows, two of them switches: Board to Deadlines 283 ms and Board to Import & Export 404 ms. The others were warm startup 1,868, the versions and details 979, the two saves 444 and 282, and the record let go of 406. The motion test passed, with 17 of 22 faded; three switches of 167 to 246 ms were cuts because they outlasted the fade.
- **Run 2 (13:48).** The sibling ran for 34 of 78 samples and the desktop was in use. The main test failed seven enforced rows, among them Board to Deadlines 3,089 ms and Board to Import & Export 408. The motion test failed one switch, Board to Import & Export with motion on at 419 ms against 35 with motion off. Both the absolute row and the new growth check failed it: a cut that outlasted the fade, the host's stall.
- **Run 3 (13:57), the motion test alone.** The desktop was in use and the sibling ran for 11 of 36 samples. The test passed. The differences ranged from -11 to +67 ms and Begin from 1.3 to 8.2.

The window check is not the cause of the higher readings while the desktop was in use: it costs microseconds (measured above).

## 2026-10-26: the three manuscript-page opens and Journals to Board (#133)

Branch `fix/v092-manuscript-open`, from master `dfb353e` (speed parts A to C, #217's hosted Submission Details, #220's cross-fade and #213's focus). It takes the last rows the 2026-10-23 section left over budget and reported: the three manuscript-page opens, then Journals to Board and Board to Library. "Before" is `dfb353e`: three Release runs of the budget test on 2026-10-25 (A 09:24, B 10:02, C 12:07) and two more that evening, on the host of the after runs (22:14 and 22:23, alternated with this branch's code). "After" is three Release runs of the branch's last commit on 2026-10-26 (G1 00:17, G2 00:20, G3 00:23), with the rows enforced. The sections above are history and were not changed.

### Where the time went

A profile of `dfb353e` (a scratch test with stopwatch marks, layout and window probes; not kept) found the opens were one layout pass, 55 to 80% of each, and that about half of that pass came from the page left: as it let go of its record while the window idled, it laid itself out as a page with no manuscript (an empty header two pixels shorter, its hidden sections reflowed around emptied labels), and the next open grew it all back. The same manuscript opened again after that let-go moved 35 controls; opened again without it, none. The rest was a request every panel and table in the page makes as the page is shown (a `ScrollableControl` lays itself out as it becomes visible), 19 to 23 layouts an open of containers nothing had changed in. Leaving Journals rendered the Board while it was hidden, filling the stage filter again with the same options, showing again every attention chip already shown, and reading the search box once per manuscript: 2,954 messages sent a leave.

### What changed

- **`50f50fc`, a guard first.** `LibraryBoard.Open` now checks after every open, in every test that opens through it or (since the review follow-ups below) through the view catalog, in the theme and size each runs at, that no container of the page still holds its layout and that laying each one shown out again moves, resizes and scrolls nothing (`AssertSettled`; the budget tests turn it off). `KeptManuscriptPageTests.AnOpenLeavesNothingToLayOut` opens records that differ in every way the page's shape follows, straight from the page and from the Board. On `dfb353e` it found a defect that was already there: opened after the window was resized on the Board, the Overview's scroll range ended 57 pixels short of its last card (a card that grew as it took its new width asked for its host's layout while that layout ran, and WinForms let the request go). A scrolling host whose range lags its contents now lays out once more after the pass; in the guard's 22 opens that ran once.
- **`107ad74`, the page left holds its layout from letting go of its record to the next open.** Nothing of the manuscript stays on it, as before; only nothing is laid out until the next record shows, from the last record's shape. Letting go of the record now lays out no container.
- **`351a540`, nothing hidden is laid out during an open.** A panel or table left hidden (a section not shown, the reroute offer, the File Drawer card's table, the Unsaved changes bar) keeps what it has to lay out until it shows.
- **`814910b`, the page is prepared in the shape a page left keeps.** Its editor shows no record as one that let go of its record does (`ShowNoRecord`, folded into `UnloadRecord` after the review below, which also makes the first snapshot of a working copy in idle time), and the header's labels keep one line's height with no text, so the first open no longer grows the header and lays out the editor under it again.
- **`92847f0`, nothing already shown is shown again on a hidden page.** `InPlace.SetShown` sets a control's visibility only when its window is not already so: the Overview at each open, the shortlist's parts, the notes card, the File Drawer card, and the Board's attention chips.
- **`6b74e99`, leaving Journals renders less.** The stage filter keeps its options when they and its font and DPI are the same; the search and the stage chosen are read once a render; a chip's cursor is set only when it changes; a filter chip keeps its text and its bold font. 2,954 messages sent a leave became 492.
- **`f05c6dd`, the layout showing the page asks of a container nothing changed in is let go.** Just before the page shows, the open notes which held containers nothing has asked to lay out, as WinForms records it (`LayoutRequests` reads and clears a held control's `LayoutDeferred` state by reflection; where it is not found, every container lays out as before), and lets go of the one request showing the page makes of each of them. A request a container inside makes during the pass, the page's own new size, and a part shown or hidden while the page was held (hidden under a hidden page, a part asks nothing of its container; the guard found this with the Unsaved changes bar) still lay out. This was the plan's reserve, taken after the opens failed the gate below without it.
- **`a01db42`, the shortlist's rows are arranged at the width they will have.** Hidden while no journal was shortlisted, the drawn list had no width, measured its rows at the narrowest a row can be (293 pixels for three journals), was laid out so, and then took 160.
- **This section's commit** enforces the three opens and both visits' Journals to Board in `EveryActionIsMeasuredOnBothLibraries`, and the opens and Journals to Board in `PageSwitchesWithMotionOnGrowByNoMoreThanTheFade`, with the existing multipliers (x1 Release, x2 Debug, a further x3 when `GITHUB_ACTIONS` is set). The review follow-ups below leave the Pipeline open reported again.

Nothing here changes when or what the library saves: the save path, `LoadRecord`'s reading of the author library, `Bind` and the baseline are untouched.

### Numbers

500 library, Release, UI ms against the budget x1 (UI equals wall for every switch row). A to C and the 22:14 and 22:23 runs are `dfb353e`; G1 to G3 the branch, the rows enforced.

| Action | A / B / C (10-25 day) | 22:14 / 22:23 (10-25 evening) | G1 / G2 / G3 (10-26) | Budget ms | Held |
|---|---:|---:|---:|---:|---|
| Board to a manuscript page, the first (on the page prepared while the window idled) | 50 / 56 / 75 | 120 / 121 | 51 / 69 / 60 | 100 | enforced from 10-26 |
| Board to a manuscript page, the kept page (another manuscript) | 45 / 45 / 55 | 106 / 60 | 76 / 71 / 54 | 100 | enforced from 10-26 |
| Board to a manuscript page, the kept page (a Pipeline manuscript with three shortlisted journals and tags) | 58 / 49 / 142 | 119 / 100 | 84 / 85 / 86 | 100 | enforced from 10-26; reported again after the review (below) |
| Manuscript page: its first paint | 488 / 412 / 19 | 24 / 21 | 25 / 24 / 32 | 250 | reported |
| Manuscript page: versions and details after its first paint | 40 / 48 / 62 | 107 / 145 | 54 / 121 / 176 | 250 | enforced |
| Manuscript page: the record let go of while the window idles | 46 / 41 / 55 | 95 / 109 | 10 / 8 / 11 | 250 | enforced |
| Journals to Board | 39 / 31 / 48 | 63 / 49 | 37 / 33 / 37 | 100 | enforced from 10-26 |
| Journals to Board (second visit) | 32 / 39 / 40 | 78 / 49 | 55 / 26 / 30 | 100 | enforced from 10-26 |
| Board to Library | 42 / 26 / 44 | 68 / 43 | 53 / 48 / 73 | 100 | reported |
| Library to Board | 14 / 11 / 16 | 22 / 19 | 21 / 36 / 28 | 100 | enforced |
| Manuscript page to Board | 13 / 9 / 20 | 20 / 25 | 25 / 32 / 13 | 100 | enforced |
| Save from the manuscript page | 52 / 53 / 67 | 69 / 101 | 43 / 79 / 41 | 250 | enforced |
| Warm startup to an interactive Board | 1154 / 871 / 950 | 1124 / 863 | 923 / 1183 / 1168 | 1500 | enforced |

All three G runs passed every enforced row. The evening's host read the code before this branch about twice as slow as the morning's (the first open 120 and 121 ms against 50 to 75): no other test host and no VM, the desktop idle for one and a half to two and a half hours (the display most likely asleep), the sampler's load readings swinging from 0 to 85% while the processes it could see used about a core and a half, the test's own one of them. So the evening's before and after columns are the comparison; the day's are the record of `dfb353e` on a quieter screen.

With motion on (`PageSwitchesWithMotionOnGrowByNoMoreThanTheFade`, 00:25, the same host): 20 of 22 switches faded, no picture was late; the first open read 44 ms with motion off and 41 on, the kept page 50 and 41 (both cuts by design), Journals to Board 16 and 21 and its second visit 37 and 48; the test passed with the new rows enforced.

What an open does, counted by a scratch probe (Release, 500 library; the counts do not depend on the host), before (`dfb353e` with the scroll-range fix) and after (`a01db42`):

| Open | Containers laid out | Containers moved | Controls moved | Windows made |
|---|---:|---:|---:|---:|
| The first, on the prepared page | 44 to 16 | 15 to 6 | 35 to 13 | 2 (its tag chips) |
| Another manuscript on the kept page | 40 to 15 | 14 to 3 | 27 to 6 | 1 |
| The Pipeline manuscript, after the other | 42 to 21 | 18 to 9 | 33 to 17 | 2 |

And timed in a scratch loop of twelve rounds on the evening's host (medians of 11, about 2,000 windows in the main window against the budget test's 4,780, alternated with the code before the branch): the first open 111 and 102 ms before, 34 to 40 after; another manuscript 102 and 101, 34 to 40; the Pipeline manuscript 89 and 99, 48 to 54; the layout pass, from the page shown to its last layout, 79 to 88 ms before and 17 to 30 after. Journals to Board in the same loop, sixteen leaves each: medians 100 and 152 ms before, 48 and 50 after.

### What stays reported, and what remains

- **The gate.** The plan held each open to 60 ms on a quiet host and 80 on a busy one, so that 100 would hold with room. In the after runs the first open and another manuscript read 51 to 76 ms and the Pipeline manuscript 84 to 86, all three inside 100 in every run, on a host that read the code before at 60 to 121; no run was taken on the morning's quieter screen, nor beside a sibling test host or a VM. The Pipeline open has the least room. On this host such a run can still pass 100 ms, as the enforced first visits to Deadlines and Import & Export can (see the 2026-10-17 section); a failure of these rows is run again on a quiet host before it is read as a regression.
- **What is left of an open.** Loading the record and the header, 8 to 19 ms (most of the Pipeline manuscript's in its tag chips' windows, one per tag, which `AnOpenMakesNoWindowButItsTagChips` pins); then the pass, 17 to 30 ms, most of it the Overview's nested self-sizing tables measuring their children again whenever the record changes what they hold: laid out again with nothing moving, the Overview costs 11 to 18 ms on this host, though each of its controls measures itself in under 0.2 ms. Fewer nested self-sizing tables (the drawn Overview of the plan's Design 2) is the lever after 1.0.
- **Board to Library** (53 to 73 ms here; 26 to 509 across 2026-10-24 and 10-25) stays reported. About half of it is making the page's 14 windows, its 500-row grid's among them, at every visit, which a page built for each visit and disposed as it is left needs (`PageSwitchTests.LeavingAPageDisposesItInPlace` and the "entered new" line of `KeyboardAccessTests.EnteringAPageByKeyboardKeepsFocusWhereItLandsToday` pin that shape); its data, 500 rows, takes 4 to 19 ms. A Library page kept and prepared while the window idles, as Journals is, would take it under 100 with room; it is offered to the maintainer, with its costs: those two pins change, about 15 windows and a 500-row grid stay alive while hidden, its rows go stale if a change skips `RenderManuscripts`, and its preparation can fall inside timed rows.
- **The paint rows** (the first paint after an open, the Board's after a leave, and the paints after Library to Board, Journals, Insights and the first switch to Published) stay reported, by the maintainer's earlier decision: they hold the wait this machine's screen can take.
- **The idle steps against the idle queue's 30 ms slice** stay reported (102 to 127 ms in the G runs); each is under the UI thread's 250, which is enforced.
- **#133's Acceptance** is therefore not met in full: Board to Library, the paint rows and the idle slice stay reported, and after the review below the Pipeline open too.

### Host during these runs

Windows 11 (NT 10.0.26300), 12 logical processors, window DPI 96, sampled every 3 to 4 s. Runs A to C (2026-10-25, morning): A with no input for 12 minutes and no other test host, B with no input for 50 minutes, C beside a sibling worktree's test host for 15 of 48 samples. The evening and night runs (22:14 to 00:27): no other PaperRoute test host and no VirtualBox VM, the desktop idle throughout, Firefox, Thunderbird, OneNote, OneDrive, CrossDeviceService, Malwarebytes, 1Password, PowerToys and several Claude sessions open; this worktree's own builds between runs. The computer slept from 14:59 to 17:20, during this branch's first Debug filter run.

### Debug

The brief's targeted Debug filter, with ScalingMeasurementTests and, from the fifth commit, DashboardShelfLayoutTests, FileDrawerTests, BoardFillTests and RecoveryDraftTests, ran on each commit; each commit's message gives its run. Two runs found something before their commit, which put it right: on `50f50fc`, the paint test's own new check sent messages inside a watched switch (the check is off in that test, which then passed), and on `6b74e99`, a test that called a changed method by reflection (it now asks what the Board shows). On `6b74e99` a keyboard pin's rail row also failed once while the desktop was in use as the test began, and passed alone. On this section's commit (00:41 to 01:05, no other test host): 338 tests, 336 passed, 2 skipped (the on-screen fade rows), the motion budget test among them with its new rows enforced x2.

### Tests that hold this section

`KeptManuscriptPageTests` (`AnOpenLeavesNothingToLayOut`, `TheRecordLetGoOfLaysNothingOut`, `TheSameManuscriptOpenedAgainMovesNothing`, `AnOpenLaysOutNoHiddenSection`, `APreparedPageMatchesOneLetGoOf`, `AnOpenReshowsNothingOnTheHiddenPage`, `AContainerNothingChangedInIsNotLaidOut`, `TheShortlistTakesItsHeightOnce`), `PageSwitchTests` (`LeavingJournalsShowsWhatChangedOnTheBoard`, `LeavingJournalsReshowsNoAttentionChip`, `LeavingJournalsWithNothingChangedRefillsNoFilter`, `AFilterChipMeasuresAsBefore`), `ManuscriptEditorReuseTests.NothingOfTheManuscriptLeftReachesTheNextOne` in Light, Dark and a contrast theme, `SubmissionDetailsLayoutTests.HostedTheCorrespondenceToolbarAndHintAreWhole` and `ManuscriptEditorScalingTests.TheRevisionDeadlineRowHoldsOnTheKeptPageOpenedAgain` on the kept page opened again after the window was made as small as it goes, and the guard in every test that opens a manuscript through `LibraryBoard` or the view catalog. Each new pin was run on the code before its commit and failed there, and each commit's message quotes its failure. `LeavingJournalsShowsWhatChangedOnTheBoard` (which passed on `dfb353e`), `AFilterChipMeasuresAsBefore` and the two held-page rows are guards of behaviour that must not change rather than pins that failed first; the guard failed on `dfb353e` only for the scroll-range defect its own commit fixed. One correction to those messages: `f05c6dd`'s says the guard "failed five tests" without the save bar's case; it failed six rows of four tests (`NothingOfTheManuscriptLeftReachesTheNextOne` in its three themes among them).

### Review follow-ups (2026-10-26, night)

A review of this section's commits found the opens enforced on three night runs alone, the settled-layout guard missing from the view catalog's opens, and smaller things. The commits after `90e7368`:

- **`e59deca`.** `LayoutProbe` loses the members no test used (`Sequence`, which built a path string at every Layout and move event, `ContainerLayouts`, `ContainerMoves`, `Described`) and its own copy of `Descendants`; `KeptManuscriptPageTests` shares `ManuscriptEditorReuseTests.ShowSection`.
- **`627212e`, the guard on the view catalog's opens.** `CatalogBoard.Open` fails a view whose page an open left with layout to do, so the sweeps check it too: names, keyboard, Dark legibility, scaling, and `SubmissionDetailsLayoutTests` and `ManuscriptEditorScalingTests` on the held page opened after the window was made small. They found nothing. `AnOpenLeavesNothingToLayOut` runs in Light at text size 1.0, in Dark at 1.25 and in a contrast theme at 1.5, where it ran only in Light at 1.0. `PageBoard` (`ManuscriptPageTests`) and `RecoveryDraftTests`' board still open without the guard; the two claims above that said otherwise are corrected.
- **`ae986db`, a pin for the prepared page's header.** `KeptManuscriptPageTests.TheFirstOpenOnThePreparedPageMovesNoShell`. With `814910b`'s one-line minimum height taken out, the first open grew the header from 61 to 63 pixels and moved the editor 2 down.
- **`0a8bad2`, nothing of a record let go of is kept.** What the page showed when its layout was held was noted for every control, the record's own parts among them, so the window kept those parts, disposed, and through the version history the last working copy, until the next open. They are dropped as the record is let go of; `ThePageLeftHoldsNothingOfTheManuscript` now checks it and failed without it.
- **`aea85b1`.** A shortlist arranged at its guessed width no longer arranges its rows a second time as its height is set.
- **`0bbe971`.** `ShowNoRecord` is folded into `UnloadRecord`, which takes the library's records.
- **`b6b6d94`, a pin for the attention chips' kept text.** `PageSwitchTests.LeavingJournalsWithNothingChangedRefillsNoFilter` requires that no chip's text is read from its window during the leave; without `ControlStyles.CacheText`, each of the four chips shown was read 32 times. `SentMessageWatch`'s hook catches what it cannot read, and the tests' fonts are disposed.
- **This commit.** The runs below; the Pipeline open reported again; the test's comments date the enforcement 2026-10-26 and give the readings beside the rows still enforced.

#### Release, 500 library: beside another test host, and on a quiet host

The code is the branch with the follow-ups above (those that change what an open does are `0a8bad2` to `0bbe971`); Q4 also has this commit's change to the test. B1 (02:36) and B2 (02:58) ran beside a Debug test host of this branch, the catalog sweeps and then the kept-page, reuse and page-switch tests (in 42 of 43 samples each; the sampler's load mean 44 and 45%). Q1 to Q3 (02:48 to 02:56) and Q4 (03:29) ran with no other test host (load mean 51 and 54%; as in the night runs above, the reading swings while the processes it can see use about a core and a half). In every run no VirtualBox VM ran and nothing had touched the desktop for more than four and a half hours, so the display was most likely asleep. None is therefore the daytime run on a quiet screen the review asked for: this session ran at night, and that run is still to take. G1 to G3 are `90e7368`, from the table above.

| Action | G1 / G2 / G3 | Q1 / Q2 / Q3 / Q4 | B1 / B2 | Budget ms | Held |
|---|---:|---:|---:|---:|---|
| Board to a manuscript page, the first (on the page prepared while the window idled) | 51 / 69 / 60 | 48 / 59 / 69 / 66 | 87 / 63 | 100 | enforced |
| Board to a manuscript page, the kept page (another manuscript) | 76 / 71 / 54 | 66 / 67 / 82 / 56 | 88 / 93 | 100 | enforced |
| Board to a manuscript page, the kept page (a Pipeline manuscript with three shortlisted journals and tags) | 84 / 85 / 86 | 75 / 81 / 57 / 74 | 135 / 96 | 100 | reported from this commit |
| Manuscript page: versions and details after its first paint | 54 / 121 / 176 | 128 / 115 / 131 / 123 | 780 / 206 | 250 | enforced |
| Manuscript page: the record let go of while the window idles | 10 / 8 / 11 | 12 / 16 / 17 / 8 | 14 / 15 | 250 | enforced |
| Manuscript page to Board | 25 / 32 / 13 | 27 / 20 / 21 / 16 | 24 / 22 | 100 | enforced |
| Save from the manuscript page | 43 / 79 / 41 | 46 / 85 / 56 / 33 | 65 / 93 | 250 | enforced |
| Journals to Board | 37 / 33 / 37 | 37 / 26 / 40 / 29 | 80 / 106 | 100 | enforced |
| Journals to Board (second visit) | 55 / 26 / 30 | 30 / 26 / 32 / 31 | 78 / 105 | 100 | enforced |
| Board to Library | 53 / 48 / 73 | 47 / 57 / 61 / 45 | 56 / 104 | 100 | reported |
| Library to Board | 21 / 36 / 28 | 17 / 23 / 20 / 18 | 18 / 41 | 100 | enforced |
| Board to Deadlines | | 60 / 42 / 57 / 63 | 83 / 104 | 100 | enforced (2026-10-17) |
| Board to Import & Export | | 70 / 81 / 77 / 66 | 174 / 108 | 100 | enforced (2026-10-17) |
| Warm startup to an interactive Board | 923 / 1183 / 1168 | 1094 / 1133 / 1402 / 1138 | 1336 / 1569 | 1500 | enforced |

Q1 to Q4 passed every enforced row. B1 failed four: the Pipeline open (135), Board to Import & Export (174), the versions and details (780) and the restore's inspection of the backup (387). B2 failed five: Journals to Board (106) and its second visit (105), Board to Deadlines (104), Board to Import & Export (108) and the warm startup (1,569).

#### What these runs decide

- **The Pipeline open is reported again.** The brief asked that the three opens hold 100 ms with room on a busy host before they are enforced. The Pipeline open read 135 and 96 beside another test host and 57 to 86 on a quiet one, with no room on either, and the maintainer has not accepted the re-run rule below for it. So it is reported, with its reason in the test, as the review proposed. What is left of it is its tag chips' windows (one per tag) and the Overview's nested self-sizing tables (above).
- **The first open and another manuscript on the kept page stay enforced**, under the rule the suite already holds the first visits to Deadlines and Import & Export to, now written in the test beside them: a failure is run again on a quiet host before it is read as a regression. They held 100 in both runs beside another test host (87 and 63; 88 and 93), but the kept page's room there is thin, and neither meets the plan's gate (60 quiet, 80 busy). If room on a busy host is to be the line for enforcing, both go back to reported; that is the maintainer's decision.
- **Journals to Board stays enforced under the same rule**, and that is the maintainer's to confirm too. It read 26 to 55 ms in seven quiet runs and 78 to 106 beside another test host, much as Deadlines' and Import & Export's first visits did in the same runs (83 and 104; 174 and 108).
- **#133's Acceptance** is still not met in full: Board to Library, the Pipeline open, the paint rows and the idle slice stay reported.

#### Debug

The brief's targeted Debug filter on this commit's code (03:08 to 03:28, no other test host and no VM): 267 tests, 265 passed, 2 skipped (the on-screen fade rows). Before it, beside the busy runs, the catalog sweeps with the guard passed (ManuscriptEditorScaling, SubmissionDetailsLayout, AccessibleName, DarkThemeLegibility, ScalingMeasurement, ViewCatalog and WindowChrome), and so did KeptManuscriptPageTests, ManuscriptEditorReuseTests and PageSwitchTests (47 tests); after it, DashboardShelfLayoutTests, FileDrawerTests, BoardFillTests and RecoveryDraftTests passed too (57 tests).
