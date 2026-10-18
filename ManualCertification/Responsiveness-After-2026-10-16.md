# Responsiveness budgets: the after table — 2026-10-16

The same measurements as [the before table](Responsiveness-Before-2026-10-16.md), taken by the same test on the same machine after the second and third responsiveness pull requests ([#132](https://github.com/JUhalt/PaperRoute-Tracker/issues/132) and [#133](https://github.com/JUhalt/PaperRoute-Tracker/issues/133)). Read the before table's "How the numbers were taken" first; only the differences are repeated here.

## What changed between the tables

- **#132.** Backup, Restore and the save, with its sweep of the managed root, run on a worker thread while the window is disabled the way a modal dialog disables its owner (`EnableWindow`, not `Form.Enabled`, which walks every control on a large Board and cost about 3 s each way in the profile) and the status line says what is happening; the UI thread processes messages until the work ends. The Insights figures are worked out on a worker thread and come back through the window's message queue while the page keeps what it shows.
- **#133.** `RenderManuscripts` keeps every card it has built and shows a card again when the text it shows is unchanged, parking cards that leave a shelf; a card's actions look its manuscript up by id when used, so a restore keeps the cards whose text is unchanged. The Library filter refills the grid once typing pauses (a 200 ms timer, as the Board's search), and the grid's rows go in together. The Import & Export page is built once and kept. The dirty-timer tick, within budget before, is unchanged.
- Rows added to the test: the debounced Library refill, a Deadlines filter keystroke, the Insights figures, and second visits to Deadlines and Import & Export. Backup and Restore are now timed through the handlers' seams (`WriteBackup`, `InspectBackupFile`, `ReplaceLibraryFrom`) rather than the services directly; the work timed is the same.
- Enforced from #133 on, on the 500 library, with room to spare on this busy host: the Library filter keystroke, the Board render, Save from the manuscript page and the inspection step of Restore. Backup's stall varied from 61 to 419 ms across runs and stays reported. So does the replace step of Restore, which #133 brought from 13 s to the 50 to 60 ms below, but which measured 95 and 331 ms in two later Debug runs on this host: like Backup it pumps for 30 to 60 s, long enough to meet one of the host's own stalls. Journals to Board, which #133 also brought within budget, stays reported: at 87 to 89 ms against 100 ms it has no room on this host. Manuscript page to Board, also changed by #133, is still over in Release. The example library runs first and carries the process's first-time costs, so its rows stay report-only.
- **2026-10-17, after the merge with #116.** A restore now ends in a restart rather than an in-place reload, so `ReplaceLibraryFrom` is gone, and the test reports the row "Restore: replace the library, reload, render" below as "Restore: replace the library (the window then restarts)": it times the swap and the notice through `RestoreFromArchive`, with the restart recorded rather than run. Its numbers below were measured before the merge and were not taken again. Since c4af8e9 a card's actions act on the card's own record while the library holds it, and look the manuscript up by id only when it does not. The recorded numbers and the budgets are unchanged.

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

## 2026-10-17: startup, the manuscript page and the page switches (#133, second part)

Branch `fix/v092-speed-startup`, from ab5f682. The rows of the Deadlines and Import & Export pages belong to the sibling branch `fix/v092-speed-deadlines` and are left as they were. Run B is the quiet Release run of the profile behind this branch (2026-10-17, 09:27, mostly under 20% CPU, no other test host at its end), the baseline the work was planned against.

### What changed

- **Windows made once, where they stay.** WinForms makes a control's window as soon as it is shown, and a form's as soon as it is marked visible; with no window around it, the window is made on WinForms' parking window and moved into place later, and a control removed before it is disposed moves its windows there again before they are destroyed. On this machine each window made, moved or destroyed costs 1 to 6 ms. Pages and the editor's parts are now shown once their parent has a window (`InPlace.WhenAttached`), and a page left is disposed where it is, after focus moves out of it. The Library grid's columns are sized from their header and one cell per distinct text instead of `AllCells`.
- **The Board fills in after its first screen.** At startup only the shelf shown is built, and only the cards that fill its view and one row more get their windows before the Board is interactive; the rest show a card at a time while the window idles (a 15 ms timer, 30 ms of work a tick, waiting while a save, backup or restore runs). A shelf not shown yet is built the first time it is shown. A card's context menu is built when its window is made or when first asked for (`CardPanel`). A render that changes one card replaces that card alone; before, every card left the shelf and came back.
- **One manuscript page, kept.** The editor loads one record after another (`LoadRecord`, with everything it holds for a record in one `RecordState` replaced as a whole). The page is built for the first manuscript opened and kept, hidden, for the next: a manuscript loads into it, the page shows in one layout pass, and the Versions tab and the selected submission's details follow once it has painted. Leaving hides it, and the editor lets go of the record while the window idles. It is built again when the colors, the DPI or the font change.
- Enforced from here on, on the 500 library: warm startup, the Board's fill, the Library refill, the page switches back to the Board from the Library, Insights and the manuscript page, letting go of the record after the manuscript page is left (from 2026-10-18), and the save of a Pipeline manuscript whose card changes; the Library keystroke, the Board render, Save from the manuscript page and Restore's inspection as before.
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
| Library to Board | 339 | 115 | 274 / 38 | 66 | 128 / 31 | 100 | enforced |
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

The first Release run failed on Library to Board (274 ms against 100) and the second on Restore's inspection (520 ms against 250, enforced before this branch); the Debug run failed on startup (4,945 ms against 3,000). Each of those rows measured 38, 61 and 940 to 1,055 ms in the other runs: the failures are this host's, see below. The quietest run of the branch (19:28, CPU 11 to 25%, no other test host, before the kept page) held every row enforced now that it measured, with warm startup at 950 ms, Library to Board 27 ms, Board to Journals 111 ms and Board to Insights 210 ms.

The 2026-10-18 runs, after the review fixes, were taken on a quieter host and are not a measure of those fixes against the runs before them; only the first switch to Published changed what it does (15 cards with their windows where it gave 2). The 00:32 run started as the Release build ended (CPU 63% at its start; the example library's first rows, run first, took 10.3 s for startup and 9.8 s for the first keystroke against 0.5 s and 6 ms at 00:45) and failed on Library to Board at 128 ms. A run at 00:38, left out of the table, failed on the same row at 419 ms: a sibling worktree's Debug test host started at 00:39:54, during it. The 00:45 run, with no other test host (checked every 15 s) and CPU 0 to 65% from the run itself, held every enforced row. None of the fixes changes the work Library to Board does; it has measured 27 to 419 ms on this host across the branch.

### Host during these runs

Windows 11 build 26300, 12 logical processors, window DPI 96. CPU load sampled at the start and end of each run: 83 and 74% for the first Release run, 76 and 19% for the second, 17 and 86% for the Debug run. No other PaperRoute test host ran during the first Release run; a sibling worktree's Debug test host ran from 21:16, through the end of the second Release run and the whole Debug run, about seven CPU-minutes. Desktop programs running throughout: CrossDeviceService (the most CPU time), OneNote, PowerToys, 1Password, PrusaSlicer and several Claude sessions, plus other builds and tests of sibling worktrees on and off through the day. Another UI test host costs this branch most: window creation is shared across the desktop, and the profile measured a window at 0.9 to 1.6 ms quiet and about 5.8 ms while one ran. The 2026-10-18 runs (00:32 to 00:48) had the same desktop programs open, with CPU at 7 to 10% between runs and 20 GB of memory free; the only other test host was the sibling's named above.

### Report-only rows, and why

- **Board to Library, Board to Journals, Journals to Board, Board to Insights** build their pages' windows (Library 14 and a 500-row grid, Journals 19, Insights 30). Measured step by step on a quiet host (CPU 5%): Board to Library 54 to 91 ms, Board to Journals 80 to 108 ms of which the Journals editor's constructor is 26 to 74 ms, Board to Insights 62 to 106 ms, Journals to Board 45 to 67 ms. Within the 100 ms line on some quiet runs and over it on others, they have no room to hold.
- **First switch to Published** builds that shelf's cards and gives its first screen their windows (15 cards in a 1280 x 840 window; until 2026-10-18 only 2, see above, so the 10-17 numbers understate it). Before this branch startup built those cards, and the first switch still gave every Published card its windows (1.1 to 2.6 s in the profile); later switches take 10 to 22 ms.
- **Board to a manuscript page, both rows.** The first open builds the page: the editor's constructor is 0.9 s on this host (building its controls 753 ms, styling them 419 ms, measured quiet). Building it while the window idles after startup was tried and left out: it is one step of 0.8 to 0.9 s, which would hold the window once at a moment the researcher did not choose. An open on the kept page measured, quiet: loading the record 145 to 290 ms (the submissions list and the shortlist's rows 50 to 155, the journal's notes and the Overview from its top 40 to 130), the header 54 to 102 ms and the one layout pass 100 to 400 ms. Loading a record replaces the windows of its shortlist rows, tag chips and lists, and the page lays out again; reaching 100 ms needs rows and chips that are kept and re-filled rather than rebuilt, which is a change to the editor's sections, not taken here.
- **The idle step after an open** builds the Versions tab and the selected submission's details once the page has painted, and has held the window 57 to 583 ms on this host across the branch's runs. The step after a leave, which lets go of the record and destroys its windows about 15 ms after the Board is back, is enforced from 2026-10-17 against the 250 ms UI-thread budget: the enforced "Manuscript page to Board" rows end when the Board shows and do not include it, so the two rows together are what a researcher going back waits. It runs as one step, longer than the idle queue's 30 ms slice; splitting it (the record's versions and details, then its rows and chips, then its lists) is left for later.
- The **example library** runs first and carries the process's first-time costs, so its rows stay report-only, as above.

### The first seconds after startup

Until the fill reaches the end of the shown shelf, the cards it has not reached are hidden: the shelf's scroll range, and a screen reader's object navigation, end at the last card shown. Tab going forward never stops there: focus entering a card shows the row after it. Shift+Tab into the shelf from outside it, as from the rail wrapping back, reaches the last card shown so far rather than the shelf's last card until the fill has passed. On the 500 library the fill takes about 5 s of idle time, a card at a time; it waits while another page covers the Board and goes on when the Board is back. A search typed meanwhile keeps a card for exactly what it finds; a found card past the first screen shows as the fill reaches it. The tab of a shelf not shown yet counts all its manuscripts; switched to, that shelf shows its first screen at once, measured by the space it fills, since a hidden shelf keeps the size it was made at.

### Merge notes

- **Form1.Shell.vb.** `LeaveCurrentPage` moves focus out of the page (`MoveFocusOutOf`) and disposes every page in place, Deadlines included; Import & Export keeps `Remove`, and the kept manuscript page is hidden and let go of while the window idles (`QueueManuscriptPageUnload`). `ShowPage`: the Board branch queues the fill, a manuscript shows on the kept page (`ShowManuscriptPage`, or `BuildMissingManuscriptPage`), and Journals goes through `CreateJournalLibraryEditor`. `EmbedForm` no longer sets `Visible`; `HostEditor` and the Library tabs show through `InPlace.WhenAttached`. `FitLibraryColumns` sits beside `CreatePageGrid`; `CreatePageFrame` is unchanged.
- **Form1.vb.** `DeferBoardFill` before `RenderManuscripts` in `Form1_Load`; `RenderManuscripts` and `ShowShelf` in fill mode; `FillShelf` in place; in `CreateManuscriptCard` the `CardPanel` menu lines and the `Enter` hook, near #187's tag-strip edit; `CardRecord.HiddenByFill`; the idle timer disposed in `OnFormClosed`.
- **New files:** `Form1.BoardFill.vb`, `Controls/CardPanel.vb`, `Controls/InPlace.vb` (`WhenAttached`, `HoldPlace` and `Dispose` live here, not in Form1.Shell.vb).
- **Other files:** Form1.Insights.vb, Form1.ManuscriptPage.vb (`CreateManuscriptEditor` no longer passes `workingCopy`: the page loads every record, a recovered draft included, through `LoadRecord`), EditManuscriptForm.vb and EditManuscriptForm.Workflow.vb, SubmissionDetailsForm.vb, TagChips.vb, EmptyHint.vb, UiPolish.vb, UiTheme.vb.
- **EmptyHint, every list that has one.** The change is app-wide, not the manuscript page's alone: a hint now shows only once its list has a window or a form around it, re-syncs when the list's window is made (`HandleCreated`), tracks whether it last showed (`Shown`) instead of reading the hint's `Visible`, which reads False under a hidden parent, takes its list's colors on every update, hidden or not (a hidden hint kept the colors its list had before it was styled, or when a record shown before left it empty), and gains `EmptyHint.Refresh(list)`. It applies to the Library grid, both Insights grids, AuthorLibraryForm, JournalLibraryForm, SubmissionDetailsForm, the version history and every other list with a hint. No Deadlines list has one, and neither master nor the sibling branch touches EmptyHint.vb after ab5f682.
- **Overlaps with master since ab5f682.** `UiPolish.ApplyToControls(parent)` is now master's #106 helper word for word, in the same place (it styles the controls under `parent`), so the two sides merge as one; this branch calls it as `ApplyToControls(_versionsHost)` in `BuildVersionHistory`. This branch no longer styles shortlist rows as they are built: they look as on ab5f682 until the merge, where master's `ApplyToControls(shortlistRows)` at the end of `RefreshShortlist` styles them. `ViewCatalog.CatalogBoard.Prepare` now takes `fillInSlices` (the filled Board view) in place of `PrepareFilled`; keep master's `WindowChrome.Apply(Me)` at its top. Form1.vb, EditManuscriptForm.vb, TagChips.vb and ViewCatalog.vb also change on both sides; resolve them hunk by hunk.
- **Tests:** LibraryBoard.vb (its `CreateManuscriptEditor` is master's again; `PumpWhile` moved to TestSupport), TestSupport.vb (`PumpWhile`, `DeepestActiveControl` and `ContrastRatio`, new), ResponsivenessBudgetTests.vb (rows to merge one by one with the sibling branch's), ViewCatalog.vb (two views), BackgroundLibraryWorkTests.vb and KeyboardAccessTests.vb (one test each), and the new BoardFillTests.vb, ManuscriptEditorReuseTests.vb, PageSwitchTests.vb and KeptManuscriptPageTests.vb. #187's additions to DashboardShelfLayoutTests and ManuscriptPageTests must pass after the merge.
- **Coverage, corrected.** Two commit messages of this branch (ee5e392, f48e4ae) say the names, Dark and theme sweeps cover the two new ViewCatalog views. On this branch only the names sweep (AccessibleNameTests) reads ViewCatalog; DarkThemeLegibilityTests and WorkflowThemeKeyboardSmokeTests build their own dialogs. Master's Dark sweep (`NoViewKeepsLightColorsInDark`, over every ViewCatalog view) covers them once merged. What holds the reused manuscript page to its themes here is ManuscriptEditorReuseTests: an editor that loads another record matches a fresh one, colors included, in Light, Dark and a contrast theme, and the parts built for each record (the version history, the shortlist's rows, the tag chips) are judged on their own: their text at 4.5:1 against its surface in Light and Dark, and the user's system colors under a contrast theme (#138).
