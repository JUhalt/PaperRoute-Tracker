# Responsiveness budgets: the before table — 2026-10-16

The measurements [issue #131](https://github.com/JUhalt/PaperRoute-Tracker/issues/131) asks for, taken before any production change, on the example library and on a synthetic library of 500 manuscripts. The rows marked OVER are the scope of the second and third pull requests: [#132](https://github.com/JUhalt/PaperRoute-Tracker/issues/132) moves Backup, Restore, Insights and the save's sweep off the UI thread; [#133](https://github.com/JUhalt/PaperRoute-Tracker/issues/133) fixes what remains over budget. The after table will sit beside this file.

## How the numbers were taken

- `PaperRoute.Tests/ResponsivenessBudgetTests.vb` opens the real main window (`LibraryBoard`, a `Form1` with its repositories, author library and settings swapped for the test's temporary root) and performs each action under a `Stopwatch`. It is report-only: every row is compared with its budget and written to the test output, and nothing fails until #133 marks a budget `Enforced`.
- `PaperRoute.Tests/LargeLibraryFixture.vb` writes the 500 library: 500 fictional manuscripts with submissions, decisions, reviewer responses, correspondence, two managed versions each, reminders, shortlists, tags, readiness profiles and submission packets, backed by 2,297 managed folders. The same seed gives the same library every run. The example library (`ExampleLibraryService.Create`) is the small case: 7 manuscripts, no managed files.
- **Wall** is the Stopwatch around the action. **UI** is the longest stretch in which the window processed no messages during the action, measured by a probe timer on the UI thread; for an action that runs wholly on the UI thread, which before #132 is every action here, the two are equal. The verdict compares UI with the budget times the multiplier (1 for a Release build on this host; a Debug build doubles every budget, the CI runner triples it).
- The example library runs first, so its rows carry the first JIT compilation and assembly loading of each code path; the 500 rows run warm. The example library's startup, filter keystroke, manuscript page, Backup and Find Journals rows are mostly that first-time cost, not work proportional to seven manuscripts.
- Startup is `Form1_Load`'s sequence (settings, `BuildInterface`, `LoadManuscripts`, `LoadAuthorLibrary`, `RenderManuscripts`) followed by `Show` and one drained message queue, without the online gate, the update check and the reminder notification. The window is off-screen and no Paint event arrived, so this is "to an interactive Board", not literally to the first paint.
- `ShowPage` builds the Journals page from the user's author library; the test builds it the same way (`BuildHostedPage` around a `JournalLibraryForm`) from its own, because no seam lets a test supply one, and leaves it as `LeaveCurrentPage` leaves a hosted editor (reload the author library, re-render the Board).
- Backup and Restore are timed as the handlers call the services after their dialogs. Find Journals and Look Up Journal Facts use the recorded OpenAlex and DOAJ fixtures; nothing goes online.
- The dirty-timer tick is the median of ten ticks.

## Host

- Windows 11 (build 10.0.26300), 12 logical processors, 47.7 GB RAM, .NET SDK 10.0.401. The test window reports DPI 96: the test process is not DPI-aware, so the display's own scale does not apply to it.
- The host was busy and shared throughout. A sample during the Debug run showed about 91% CPU load with another PaperRoute test host and two `dotnet` builds from sibling worktrees running, plus Firefox, OneDrive sync, CrossDeviceService, Malwarebytes and PrusaSlicer; 15 GB of memory was free. The numbers are therefore upper bounds for this machine, and a quiet host would be faster; the before and after tables are comparable because the same test takes both on the same machine.
- Each table is one run (Release: 4 m 40 s; Debug: 4 m 11 s). Variation between runs on this host is tens of percent, so differences smaller than that between the two tables are noise.

## Release build

Verdict: UI against the budget x 1.

| Library | Action | Wall ms | UI ms | Budget ms | Verdict |
|---|---|---:|---:|---:|---|
| example | Warm startup to an interactive Board (Show, queue drained) | 10572 | 10572 | 1500 (warm startup) | OVER |
| example | Board to Library | 125 | 125 | 100 (page switch) | OVER |
| example | One Library filter keystroke | 4747 | 4747 | 50 (filter keystroke) | OVER |
| example | Library to Board | 95 | 95 | 100 (page switch) | within |
| example | Board to Journals (built as ShowPage builds it, from the test's author library) | 281 | 281 | 100 (page switch) | OVER |
| example | Journals to Board | 272 | 272 | 100 (page switch) | OVER |
| example | Board to Deadlines | 364 | 364 | 100 (page switch) | OVER |
| example | Deadlines to Board | 64 | 64 | 100 (page switch) | within |
| example | Board to Insights | 271 | 271 | 100 (page switch) | OVER |
| example | Insights to Board | 80 | 80 | 100 (page switch) | within |
| example | Board to Import & Export | 1446 | 1446 | 100 (page switch) | OVER |
| example | Import & Export to Board | 80 | 80 | 100 (page switch) | within |
| example | Board to a manuscript page | 2741 | 2741 | 100 (page switch) | OVER |
| example | One dirty-timer tick (median of 10) | 0 | 0 | 5 (dirty-timer tick) | within |
| example | Save from the manuscript page | 96 | 96 | 250 (save) | within |
| example | Manuscript page to Board | 425 | 425 | 100 (page switch) | OVER |
| example | Board render (RenderManuscripts) | 304 | 304 | 250 (UI thread) | OVER |
| example | Backup (PortableBackupService.CreateBackup) | 34411 | 34411 | 250 (UI thread) | OVER |
| example | Restore: inspect the backup | 36 | 36 | 250 (UI thread) | within |
| example | Restore: replace the library, reload, render | 866 | 866 | 250 (UI thread) | OVER |
| example | Find Journals: click to first paint | 330 | 330 | 250 (UI thread) | OVER |
| example | Find Journals: results rendered (fixture responses) | 3 | 3 | 250 (UI thread) | within |
| example | Look Up Journal Facts: click to first paint | 116 | 116 | 250 (UI thread) | within |
| example | Look Up Journal Facts: results rendered (fixture responses) | 148 | 148 | 250 (UI thread) | within |
| 500 | Warm startup to an interactive Board (Show, queue drained) | 4096 | 4096 | 1500 (warm startup) | OVER |
| 500 | Board to Library | 187 | 187 | 100 (page switch) | OVER |
| 500 | One Library filter keystroke | 410 | 410 | 50 (filter keystroke) | OVER |
| 500 | Library to Board | 402 | 402 | 100 (page switch) | OVER |
| 500 | Board to Journals (built as ShowPage builds it, from the test's author library) | 243 | 243 | 100 (page switch) | OVER |
| 500 | Journals to Board | 8853 | 8853 | 100 (page switch) | OVER |
| 500 | Board to Deadlines | 3873 | 3873 | 100 (page switch) | OVER |
| 500 | Deadlines to Board | 243 | 243 | 100 (page switch) | OVER |
| 500 | Board to Insights | 259 | 259 | 100 (page switch) | OVER |
| 500 | Insights to Board | 101 | 101 | 100 (page switch) | OVER |
| 500 | Board to Import & Export | 1529 | 1529 | 100 (page switch) | OVER |
| 500 | Import & Export to Board | 134 | 134 | 100 (page switch) | OVER |
| 500 | Board to a manuscript page | 2828 | 2828 | 100 (page switch) | OVER |
| 500 | One dirty-timer tick (median of 10) | 0 | 0 | 5 (dirty-timer tick) | within |
| 500 | Save from the manuscript page | 2503 | 2503 | 250 (save) | OVER |
| 500 | Manuscript page to Board | 6596 | 6596 | 100 (page switch) | OVER |
| 500 | Board render (RenderManuscripts) | 12367 | 12367 | 250 (UI thread) | OVER |
| 500 | Backup (PortableBackupService.CreateBackup) | 30767 | 30767 | 250 (UI thread) | OVER |
| 500 | Restore: inspect the backup | 12886 | 12886 | 250 (UI thread) | OVER |
| 500 | Restore: replace the library, reload, render | 68171 | 68171 | 250 (UI thread) | OVER |
| 500 | Find Journals: click to first paint | 252 | 252 | 250 (UI thread) | OVER |
| 500 | Find Journals: results rendered (fixture responses) | 250 | 250 | 250 (UI thread) | within |
| 500 | Look Up Journal Facts: click to first paint | 135 | 135 | 250 (UI thread) | within |
| 500 | Look Up Journal Facts: results rendered (fixture responses) | 162 | 162 | 250 (UI thread) | within |

## Debug build

Verdict: UI against the budget x 2.

| Library | Action | Wall ms | UI ms | Budget ms | Verdict |
|---|---|---:|---:|---:|---|
| example | Warm startup to an interactive Board (Show, queue drained) | 9596 | 9596 | 1500 (warm startup) | OVER |
| example | Board to Library | 91 | 91 | 100 (page switch) | within |
| example | One Library filter keystroke | 2933 | 2933 | 50 (filter keystroke) | OVER |
| example | Library to Board | 59 | 59 | 100 (page switch) | within |
| example | Board to Journals (built as ShowPage builds it, from the test's author library) | 246 | 246 | 100 (page switch) | OVER |
| example | Journals to Board | 299 | 299 | 100 (page switch) | OVER |
| example | Board to Deadlines | 332 | 332 | 100 (page switch) | OVER |
| example | Deadlines to Board | 57 | 57 | 100 (page switch) | within |
| example | Board to Insights | 248 | 248 | 100 (page switch) | OVER |
| example | Insights to Board | 77 | 77 | 100 (page switch) | within |
| example | Board to Import & Export | 1406 | 1406 | 100 (page switch) | OVER |
| example | Import & Export to Board | 87 | 87 | 100 (page switch) | within |
| example | Board to a manuscript page | 6018 | 6018 | 100 (page switch) | OVER |
| example | One dirty-timer tick (median of 10) | 0 | 0 | 5 (dirty-timer tick) | within |
| example | Save from the manuscript page | 88 | 88 | 250 (save) | within |
| example | Manuscript page to Board | 359 | 359 | 100 (page switch) | OVER |
| example | Board render (RenderManuscripts) | 328 | 328 | 250 (UI thread) | within |
| example | Backup (PortableBackupService.CreateBackup) | 21106 | 21106 | 250 (UI thread) | OVER |
| example | Restore: inspect the backup | 41 | 41 | 250 (UI thread) | within |
| example | Restore: replace the library, reload, render | 704 | 704 | 250 (UI thread) | OVER |
| example | Find Journals: click to first paint | 10239 | 10239 | 250 (UI thread) | OVER |
| example | Find Journals: results rendered (fixture responses) | 4 | 4 | 250 (UI thread) | within |
| example | Look Up Journal Facts: click to first paint | 101 | 101 | 250 (UI thread) | within |
| example | Look Up Journal Facts: results rendered (fixture responses) | 122 | 122 | 250 (UI thread) | within |
| 500 | Warm startup to an interactive Board (Show, queue drained) | 3759 | 3759 | 1500 (warm startup) | OVER |
| 500 | Board to Library | 165 | 165 | 100 (page switch) | within |
| 500 | One Library filter keystroke | 351 | 351 | 50 (filter keystroke) | OVER |
| 500 | Library to Board | 238 | 238 | 100 (page switch) | OVER |
| 500 | Board to Journals (built as ShowPage builds it, from the test's author library) | 215 | 215 | 100 (page switch) | OVER |
| 500 | Journals to Board | 9055 | 9055 | 100 (page switch) | OVER |
| 500 | Board to Deadlines | 4341 | 4341 | 100 (page switch) | OVER |
| 500 | Deadlines to Board | 254 | 254 | 100 (page switch) | OVER |
| 500 | Board to Insights | 207 | 207 | 100 (page switch) | OVER |
| 500 | Insights to Board | 103 | 103 | 100 (page switch) | within |
| 500 | Board to Import & Export | 1643 | 1643 | 100 (page switch) | OVER |
| 500 | Import & Export to Board | 146 | 146 | 100 (page switch) | within |
| 500 | Board to a manuscript page | 3979 | 3979 | 100 (page switch) | OVER |
| 500 | One dirty-timer tick (median of 10) | 0 | 0 | 5 (dirty-timer tick) | within |
| 500 | Save from the manuscript page | 3617 | 3617 | 250 (save) | OVER |
| 500 | Manuscript page to Board | 6433 | 6433 | 100 (page switch) | OVER |
| 500 | Board render (RenderManuscripts) | 12555 | 12555 | 250 (UI thread) | OVER |
| 500 | Backup (PortableBackupService.CreateBackup) | 29628 | 29628 | 250 (UI thread) | OVER |
| 500 | Restore: inspect the backup | 10621 | 10621 | 250 (UI thread) | OVER |
| 500 | Restore: replace the library, reload, render | 57991 | 57991 | 250 (UI thread) | OVER |
| 500 | Find Journals: click to first paint | 266 | 266 | 250 (UI thread) | within |
| 500 | Find Journals: results rendered (fixture responses) | 231 | 231 | 250 (UI thread) | within |
| 500 | Look Up Journal Facts: click to first paint | 113 | 113 | 250 (UI thread) | within |
| 500 | Look Up Journal Facts: results rendered (fixture responses) | 103 | 103 | 250 (UI thread) | within |

## What the table says

Within budget on the 500 library, in both builds: the dirty-timer tick (the serialisation of one working copy is well under a millisecond here), Look Up Journal Facts, and the rendering of Find Journals results. The Board, Library and Insights page switches sit a little over the 100 ms line.

Over budget on the 500 library, and where the time goes by reading the code beside the numbers:

- **Backup (31 s), Restore (13 s to inspect, 68 s to restore)** and **Save from the manuscript page (2.5 s)** hold the UI thread for their whole run. #132 moves them to a worker thread behind the disabled window and a status line; the wall time is the disk's, the UI time should fall to the completion handler's.
- **Board render (12.4 s with the Board visible)**, **Manuscript page to Board (6.6 s)** and **Journals to Board (8.9 s)** are one cost: `RenderManuscripts` rebuilds every card, with about ten controls and a context menu each, and showing the Board page then creates their window handles. Leaving a hosted editor or saving from the manuscript page re-renders the whole Board.
- **Warm startup (4.1 s)** is the library load plus that first render and handle creation. The example library's 10.6 s is mostly first-time JIT and assembly loading, which the installed app also pays on every launch.
- **Board to Deadlines (3.9 s)** builds one `DeadlineRow` control per item; the 500 library has several hundred items.
- **Board to a manuscript page (2.8 s)** is the construction of `EditManuscriptForm`, re-done on every open.
- **Board to Import & Export (1.5 s)** on both libraries builds static content: its cost is in layout, not data.
- **One Library filter keystroke (410 ms)** rebuilds the whole grid, with `RouteSummaryService.Describe` for every manuscript, on each keystroke.
- **Insights (259 ms)** computes `RouteAnalyticsService.ForLibrary` on the UI thread while the page is built; #132 moves the computation off it.

The over-budget rows that #132 does not take are the scope of #133.
