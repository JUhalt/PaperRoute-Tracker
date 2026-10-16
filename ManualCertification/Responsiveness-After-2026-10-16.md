# Responsiveness budgets: the after table — 2026-10-16

The same measurements as [the before table](Responsiveness-Before-2026-10-16.md), taken by the same test on the same machine after the second and third responsiveness pull requests ([#132](https://github.com/JUhalt/PaperRoute-Tracker/issues/132) and [#133](https://github.com/JUhalt/PaperRoute-Tracker/issues/133)). Read the before table's "How the numbers were taken" first; only the differences are repeated here.

## What changed between the tables

- **#132.** Backup, Restore and the save, with its sweep of the managed root, run on a worker thread while the window is disabled the way a modal dialog disables its owner (`EnableWindow`, not `Form.Enabled`, which walks every control on a large Board and cost about 3 s each way in the profile) and the status line says what is happening; the UI thread processes messages until the work ends. The Insights figures are worked out on a worker thread and come back through the window's message queue while the page keeps what it shows.
- **#133.** `RenderManuscripts` keeps every card it has built and shows a card again when the text it shows is unchanged, parking cards that leave a shelf; a card's actions look its manuscript up by id when used, so a restore keeps the cards whose text is unchanged. The Library filter refills the grid once typing pauses (a 200 ms timer, as the Board's search), and the grid's rows go in together. The Import & Export page is built once and kept. The dirty-timer tick, within budget before, is unchanged.
- Rows added to the test: the debounced Library refill, a Deadlines filter keystroke, the Insights figures, and second visits to Deadlines and Import & Export. Backup and Restore are now timed through the handlers' seams (`WriteBackup`, `InspectBackupFile`, `ReplaceLibraryFrom`) rather than the services directly; the work timed is the same.
- Enforced from #133 on, on the 500 library, with room to spare on this busy host: the Library filter keystroke, the Board render, Save from the manuscript page and the inspection step of Restore. Backup's stall varied from 61 to 419 ms across runs and stays reported. So does the replace step of Restore, which #133 brought from 13 s to the 50 to 60 ms below, but which measured 95 and 331 ms in two later Debug runs on this host: like Backup it pumps for 30 to 60 s, long enough to meet one of the host's own stalls. Journals to Board, which #133 also brought within budget, stays reported: at 87 to 89 ms against 100 ms it has no room on this host. Manuscript page to Board, also changed by #133, is still over in Release. The example library runs first and carries the process's first-time costs, so its rows stay report-only.

## Host during the after runs

The same machine as the before table, again busy and shared: about 75% CPU load sampled during the runs, with another PaperRoute test host from a sibling worktree running throughout, plus Firefox, OneDrive sync, CrossDeviceService, Malwarebytes and Thunderbird; 14.8 GB of memory free. Variation between runs on this host stayed at tens of percent (Backup's UI stall, for one, ranged from 61 to 419 ms across four runs), so differences smaller than that between the before and after tables are noise.

## Where the time went, and what remains

The profile behind the choices (a scratch test, not kept) measured, on the 500 library with the Board visible: building one card about 3 ms (its context menu 2 ms of that), giving a card its ten window handles about 43 ms, adding 167 cards to the visible shelf 55 to 60 ms each and linear in the count, adding 100 cards to a hidden shelf 41 ms in all, `Form.Enabled = False` 2.9 s and `= True` 1.9 s, the native `EnableWindow` under 1 ms, `EditManuscriptForm`'s constructor 2.6 to 3.1 s of which `LoadManuscript` is 0.75 s, the Deadlines page 0.8 s to build detached and 5.9 s to attach (its 221 rows carry about 1,100 handles; suspending layout made no difference), Import & Export 0.5 s to build and 1.8 s to attach, and a `Rows.AddRange` fill of the Library grid 219 ms against 557 ms one row at a time. Handle creation is the cost this machine pays: about 4 to 5 ms per handle, far above a quiet desktop, with PowerToys, Malwarebytes and a cross-device service running.

What this leaves over budget on the 500 library, and why:

- **Warm startup** builds all 500 cards and gives the 167 Pipeline cards their handles before the Board is interactive; nothing is rebuilt twice any more, but the first build remains. Only a card with fewer controls, or a shelf that creates cards as they scroll into view, would cut it further, and both are beyond the least change this issue allowed.
- **Board to Deadlines**, its filter keystroke and its second visit all rebuild the page's 221 rows, about 1,100 handles. Keeping the rows between fills the way the Board keeps its cards was tried and measured at 2.4 s for a keystroke and 3.1 s for a second visit against 4.6 s: on this machine every window operation, re-parenting included, costs milliseconds, so the change did not reach the budget and was not kept. A list that draws its rows instead of hosting a control per row is the change that would.
- **Board to Import & Export (first visit)** is the same handle creation on first build; the second-visit row shows the kept page.
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
