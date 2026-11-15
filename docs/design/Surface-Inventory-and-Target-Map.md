# Surface Inventory and Target Map (issue #161)

## 1. What this is

This is the design deliverable of issue #161: a list of every surface PaperRoute Tracker shows today, and a map of the fewer, calmer surfaces the Avalonia windows of v1.1 are built from. It describes `master` at commit `800d6c1` (v1.0.0), and every `file:line` reference is that commit's. v1.0.1 added no window, dialog or message box, but its fixes moved lines in 13 source files (`Form1.vb`, `Form1.Bibliography.vb`, `Form1.ManuscriptPage.vb`, `Form1.RecoveryDraft.vb`, `Program.vb`, `Forms\AuthorLibraryForm.vb`, `Forms\CrossrefLookupForm.vb`, `Forms\EditManuscriptForm.vb`, `Forms\EditManuscriptForm.Readiness.vb`, `Forms\JournalSuggestionsForm.vb`, `Services\DialogScaling.vb`, `Services\OnlineAccess.vb`, `Services\RecoveryDraftStore.vb`); read those references with `git show 800d6c1:<path>`. It follows the roadmap brief "Depth without clutter" (`ROADMAP.md:37-39`) and its four rules: one window; pages instead of stacked dialogs; one save boundary per manuscript; empty areas that explain themselves. Nothing is removed from the record a researcher keeps: every field and action that loses its window is given a home below. This is a proposal; nothing in it is decided until the maintainer signs it off on #161.

How it was made: eight readers read the code group by group, and three machine-made lists (63 window classes, 109 `ShowDialog(` call sites, 202 `MessageBox.Show(` call sites) were checked against their readings. Where readings disagreed, the code was checked or the disagreement is listed in section 8 or under "Gaps" at the end of this section.

**Gaps in this document** (things the readings and the checks did not settle):

- The exact handler lines that open a manuscript from the Library grid and from the Your Citations table were not traced.
- Paste a Title Page reads pasted text only (`USER_GUIDE.md:1081`: a title page "copied from Word or from LaTeX source"); it opens no file.
- The error window (`UnexpectedErrorForm`) and the automatic update offer (`UpdatePromptForm`) are owned by the main window but can appear while another dialog is open. Their deepest depth is given as 1; a deeper stack was not traced or tested. After the map the error window stays modal and could appear over a file picker or a destructive confirmation (2.2).
- The example library is a second copy of the main window in its own process. The same depths apply inside it; it was not traced separately.
- "Everyday" is a judgement from the User Guide, not measured use. Two readers disagreed on Preferences and Help (see 2.3).
- Message-box kinds are partly judgement. Four call sites carry several messages each (`SettingsForm.vb:1626`, `StartupFailureForm.vb:858`, `UpdateService.vb:387`, `ReviewerResponseItemForm.vb:222`); each is counted once under its most common kind. `ManuscriptVersionHistoryControl.vb:1288` uses an Information icon but reports a failed open; it is counted as an error.
- `Services\FileOpenService.vb:91` (open a program?) is reached from several Open File actions; which ones was not traced.
- `PrimaryText` reads `SystemColors.ControlText`, so its colour is not in the code. The manuscript-page stage pill's padding (`StagePillSize`, `Form1.ManuscriptPage.vb:1074`) was not read. The count of about 241 plain buttons styled by `UiPolish` comes from a grep and was not checked line by line.
- Two readers each found a possible defect that was not run or confirmed (question 10).
- Issue #161 gives 59 `Form` subclasses, 102 `ShowDialog` calls and 199 `MessageBox.Show` calls; at `800d6c1` there are 63, 109 and 202. The issue says `EditManuscriptForm` opens 10 dialogs; the code shows 15 classes (section 3, note 4).

Settled from the code since the first draft, so no longer gaps: Read Decision Letter shows only while the assistant is on (`SubmissionDetailsForm.vb:1047, 1199`); a "Don't ask again" choice is stored in the settings file (`Form1.Online.vb:133-151`: `ConfirmedUses`, then `settingsService.Save`); managed-file copies and deletions happen at the manuscript's Save (`USER_GUIDE.md:166`).

## 2. The numbers

### 2.1 Surfaces today

| What | Count | How it was counted |
|---|---:|---|
| Window classes in the code | 63 | `forms.json`: every class that inherits `Form` (62 inherit `Form` directly; `OpenAlexKeyForm` inherits `ApiKeyForm`). |
| Top-level windows that are not modal | 2 | The main window (`Form1`) and the example library window (a second `Form1` in its own process). |
| Pages | 9 | Board, Library: Manuscripts, Library: Authors & Affiliations, Journals, Deadlines, Insights, Import & Export, the manuscript page, the missing-manuscript page. |
| Panels | 22 | 12 in the main window (the Board's empty-library welcome, the Affiliations tab, Your Citations, the manuscript page's five tabs, the submission detail pane and its three tabs) plus 10 inside modal windows (7 Preferences sections, 3 Edit journal tabs). |
| Modal dialogs | 62 | 61 window classes shown modally (all 63 except `Form1` and `EditManuscriptForm`, which is only ever a page), plus the Visual Basic `InputBox` used to confirm Restore (`Form1.vb:4240`). Four classes are both a page or panel and a modal dialog: `AuthorLibraryForm`, `JournalLibraryForm`, `SubmissionDetailsForm`, `ReviewerResponseMatrixForm`. `UpdateProgressForm` is shown with `Show(owner)` while the owner is disabled (`UpdateService.vb:223, 236`), so it is counted as modal. |
| System file pickers | 20 call sites | `OpenFileDialog` / `SaveFileDialog` calls among the 109 `ShowDialog` sites. The other 89 sites open PaperRoute windows (Appendix B). |
| System notifications | 1 | The startup reminder that Windows shows outside the window (`NotifyIcon` balloon, `Services\WindowsNotificationService.vb:36-56`, called from `Form1.RemindersHelp.vb:76`). |
| Message boxes | 202 call sites | 56 notices + 119 errors + 17 confirmations + 10 questions = 202 (Appendix A). |
| Menus (not counted as surfaces) | 8 | Rail Settings menu (`Form1.Shell.vb:369`), board card menu (`Form1.vb:3402`), manuscript page ⋯ menu (`Form1.ManuscriptPage.vb:815`), Deadlines row ⋯ menu (`Controls\DeadlineList.vb:121`), shortlist row menu (`Controls\JournalShortlistList.vb:122`), tag chip colour menu (`Controls\TagChips.vb:290`), and the "Save & Go To..." menus of Submission Readiness (`ManuscriptReadinessForm.Workflow.vb:29`) and the Packet Vault (`SubmissionPacketVaultForm.Workflow.vb:142`). |
| Popups (not counted as surfaces) | 1 kind, 5 places | The click-help "?" control (`Forms\ContextHelpControl.vb:9-14`, a `ToolTip` with a dismiss timer), constructed at `ContextHelpControl.vb:93`, `ManuscriptReadinessForm.vb:199, 343` and `ManuscriptRouteViewForm.vb:1023, 1047`. Its style is in 7.5. |

The inventory table in section 3 has 97 rows: 2 windows + 9 pages + 22 panels + 62 modal dialogs + 1 row for the 20 file-picker sites + 1 system notification.

### 2.2 The deepest chain

- **Today: 5 modal windows** above the main window, by two routes (section 4, chains 1 and 2). A message box can sit on top of any of them as one more level.
- **Everyday use today: 2 PaperRoute windows** above the main window (for example manuscript page > Journal, Preprint & Links > Add Link), or 3 counting a file picker (manuscript page > Submission Packets > Add File > Browse).
- **After the map: 2.** Only two things can stack: the startup-failure recovery (the recovery window, then its file picker or its restore confirmation), or the error window over a file picker or a destructive confirmation. In everyday use the most is 1: a file picker, a destructive confirmation or a question that stays (5d) over the one window.

### 2.3 Modal windows reachable in everyday use, before and after

**Before: 24.** Counted as distinct PaperRoute window classes that open modally from a surface a researcher uses in everyday work (marked "yes" in the Everyday column of section 3). File pickers and message boxes are not counted. The 24: Add Manuscript, Paste a Title Page, the Route, Postpone / Set Deadline, Add / Edit Reminder, Add / Edit Version, Journal, Preprint & Links, Add / Edit Link, Add / Edit Author (manuscript author), Manage Library (Authors & Affiliations as a window), Add / Edit Author (reusable person), DOI & Crossref Metadata, journal shortlist candidate, Choose from Library (journal picker), Edit journal, Record / Edit Submission, Add / Edit Decision, Add / Edit Correspondence, reviewer comment editor, Submission Readiness, Readiness Notes, Submission Packet Vault, New / Edit Packet, Add / Edit Packet File.

**After: 0.** Of the 24, 17 become in-page panels or inline editing, 6 merge into a page or panel, and 1 is removed as a duplicate route (section 5). What still interrupts everyday work after the map is not a PaperRoute window: the system file pickers (version file, correspondence file, packet file), the destructive confirmations, the save question when leaving a manuscript with unsaved changes (`Form1.ManuscriptPage.vb:1271`), the stage-change questions on Save (`EditManuscriptForm.vb:3978, 4014, 4064`, unless question 12 makes them an in-page step) and the copy-or-link question for dropped files (`SubmissionDetailsForm.vb:2173`).

If Preferences and Help are counted as everyday (one reader did, one did not), the numbers are 26 before and 0 after: Preferences becomes a page and Help a side panel.

### 2.4 Message boxes

| | Notice | Error | Confirmation | Question | Total |
|---|---:|---:|---:|---:|---:|
| Today | 56 | 119 | 17 | 10 | 202 |

| Target | Count |
|---|---:|
| In-window notice | 111 |
| Inline validation beside the field | 40 |
| Stays a question | 8 |
| Stays as a destructive confirmation | 14 |
| Not needed after the redesign (one of them, `Form1.vb:4227`, merges into the Restore confirmation window, 5c) | 28 |
| Stays as a system box because no window exists (`ApplicationEvents.vb:70`) | 1 |
| **Total** | **202** |

So 23 boxes stay (8 + 14 + 1) and 179 go (111 + 40 + 28). Section 6 has the table by owner.

## 3. The inventory

Rule codes: **W** one window; **P** pages instead of stacked dialogs; **S** one save boundary per manuscript; **E** empty areas explain themselves. A modal dialog is counted as breaking W and P unless it is one of the kinds the rules allow to stay modal (a destructive confirmation, a file picker, or a window that needs an answer before the record can change; for message boxes only, a fourth reason is proposed in section 6: a safety warning before PaperRoute starts another program); those are marked "allowed". A page that breaks P does so through the dialogs it opens.

Depth is the number of modal windows open above the main window, including this one; message boxes are left out. "0-2" means shallowest 0, deepest 2. Boxes are message-box call sites counted once, on the surface that raises them: notice / error / confirmation / question. "Where" gives the line that opens or builds the surface; Appendix B lists every `ShowDialog` site.

### 3.1 Windows

| # | Surface | Kind | What it is for | Reached from | Depth | Everyday | Rules it breaks | Boxes n/e/c/q | Where |
|---:|---|---|---|---|---|---|---|---|---|
| 1 | Main window (`Form1`) | window | The one PaperRoute window: rail of pages, back and forward, status line; startup and close | App start | 0 | yes | P (startup, save-failure and close notices are boxes) | 3/1/0/3 | `My Project\Application.Designer.vb:31`; `Form1.vb:150-232` |
| 2 | Example library window | window | A fictional group's library to explore; discarded on close; your library is never opened | Welcome link; Import & Export; Help | 0 | no | W (by design: a separate process) | 0/0/0/0 | `Form1.Example.vb:17-31` -> `ExampleLibraryService.vb:46` |

### 3.2 Pages

| # | Surface | Kind | What it is for | Reached from | Depth | Everyday | Rules it breaks | Boxes n/e/c/q | Where |
|---:|---|---|---|---|---|---|---|---|---|
| 3 | Board | page | Every manuscript as a card on its shelf (Pipeline, Published, File Drawer); Needs Attention; search, filter, sort | Start page; rail; Ctrl+1 | 0 | yes | P, S (card actions save at once) | 0/1/0/0 | `Form1.Shell.vb:46, 231` |
| 4 | Library: Manuscripts | page | Every manuscript in one sortable table | Rail Library; Ctrl+2 | 0 | yes | none | 0/0/0/0 | `Form1.Shell.vb:232, 1503-1585` |
| 5 | Library: Authors & Affiliations (Authors) | page | Reusable people; ORCID import for an author | Library tab; Import & Export "ORCID Works..."; Welcome chip; Your Citations | 0 | yes | P | 3/3/1/0 | `Form1.Shell.vb:1544, 1625-1639` |
| 6 | Journals | page | Reusable journal records, checklists, facts and metrics; saved as you go | Rail; Ctrl+3 | 0 | yes | P | 4/3/1/0 | `Form1.Shell.vb:233, 1086, 1648` |
| 7 | Deadlines | page | Everything that needs action, by when: Postpone, Done, Add Reminder, calendar export | Rail; Ctrl+4; Board link | 0 | yes | P, S (row actions save at once) | 2/3/1/0 | `Form1.Shell.vb:234`; `Form1.Deadlines.vb:44` |
| 8 | Insights | page | How your work moved through journals (Your Journals, Your Routes, Route Map); Pipeline Report | Rail; Ctrl+5 | 0 | yes | P | 1/0/0/0 | `Form1.Shell.vb:235` |
| 9 | Import & Export | page | Every way in and out; keep records current; Backup and Restore | Rail; Ctrl+6; Welcome | 0 | no | P (picker, review window, result box in a row) | 11/11/2/0 | `Form1.Shell.vb:236, 1936` |
| 10 | Manuscript page (`EditManuscriptForm`, `pageMode:=True`) | page | One manuscript on one working copy; tabs; Unsaved changes bar | Board card; Library row; Insights; Deadlines; Route waypoint; recovery offer; history | 0 | yes | P (15 dialog classes; note 4) | 1/0/0/1 | `Form1.ManuscriptPage.vb:86, 192, 889-890` |
| 11 | Missing manuscript | page | Says a manuscript reached through history is no longer in the library | Back / forward | 0 | no | none | 0/0/0/0 | `Form1.ManuscriptPage.vb:154-160` |

### 3.3 Panels

| # | Surface | Kind | What it is for | Reached from | Depth | Everyday | Rules it breaks | Boxes n/e/c/q | Where |
|---:|---|---|---|---|---|---|---|---|---|
| 12 | Board welcome (empty library) | panel | The two ways in, the privacy promise, Quick Start, the example library | Board while the library is empty | 0 | no | none | 0/0/0/0 | `Form1.Welcome.vb:21-32` |
| 13 | Authors & Affiliations: Affiliations | panel | Institutions recorded once for every author who shares them | Inner tab | 0-1 | no | P | 1/0/1/0 | `AuthorLibraryForm.vb:137-139` |
| 14 | Insights: Your Citations | panel | How your published work is cited, from OpenAlex, updated on request | Insights tab | 0 | no | P | 1/1/0/0 | `Form1.Insights.vb:187` |
| 15 | Manuscript page: Overview | panel | Title, target journal, stage, revision deadline, DOI and metadata, links, journal shortlist, type, tags, File Drawer | First tab | 0 | yes | P, S (8 dialogs; journal library saved apart) | 3/1/0/3 | `EditManuscriptForm.vb:1679-1706` |
| 16 | Manuscript page: Authors | panel | Ordered authors, affiliations, corresponding author | Tab | 0 | yes | P, S (Manage Library saves apart) | 1/0/0/0 | `EditManuscriptForm.vb:1681` |
| 17 | Manuscript page: Versions (Version History) | panel | Which file was which; current version | Tab | 0 | yes | P | 0/6/1/0 | `EditManuscriptForm.vb:472, 1682` |
| 18 | Manuscript page: Submissions | panel | Journal submissions with the selected one's details beside the list | Tab; Deadlines row | 0 | yes | P | 1/1/1/0 | `EditManuscriptForm.vb:660, 1683` |
| 19 | Submission detail pane (`SubmissionDetailsForm` inline) | panel | The selected submission: header, Open Portal, three tabs | Selecting a submission | 0 | yes | none | 0/0/0/0 | `EditManuscriptForm.vb:3329, 3361` |
| 20 | Submission: Editorial History | panel | Decisions as they arrive; Read Decision Letter | Detail tab | 0-1 | yes | P | 1/2/1/0 | `SubmissionDetailsForm.vb:459-470` |
| 21 | Submission: Reviewer Responses (matrix inline) | panel | Reviewer comments, revision actions, response drafts | Detail tab; Deadlines | 0 | yes | P | 1/1/1/0 | `SubmissionDetailsForm.vb:530-548` |
| 22 | Submission: Correspondence & Files | panel | Letters, reviews and revised files kept with the submission | Detail tab | 0-1 | yes | P | 2/2/1/1 | `SubmissionDetailsForm.vb:463-471` |
| 23 | Manuscript page: Readiness & Packets | panel | Readiness and packet summary; Draft Cover Letter | Tab; Deadlines row | 0 | yes | P ("Each opens in its own window") | 1/1/0/0 | `EditManuscriptForm.vb:675, 1684` |
| 24 | Preferences: Appearance | panel | Follow Windows, Light or Dark | Inside Preferences | 1 | no | W | 0/0/0/1 | `SettingsForm.vb:246, 363` |
| 25 | Preferences: Needs Attention | panel | Long review, revision warning, recent rejection thresholds | Inside Preferences | 1 | no | W | 0/0/0/0 | `SettingsForm.vb:249, 414` |
| 26 | Preferences: Reminders & Windows Notifications | panel | Startup reminder notification and days ahead | Inside Preferences | 1 | no | W | 0/0/0/0 | `SettingsForm.vb:252, 473` |
| 27 | Preferences: File Drawer | panel | Rejections before the File Drawer is suggested | Inside Preferences | 1 | no | W | 0/0/0/0 | `SettingsForm.vb:255, 584` |
| 28 | Preferences: Updates | panel | Stable or Preview; check at start | Inside Preferences | 1 | no | W | 0/0/0/0 | `SettingsForm.vb:258, 646` |
| 29 | Preferences: Online services | panel | Work offline; each service and what it sends; OpenAlex key | Inside Preferences; rail Online button | 1 | no | W, P | 0/0/0/0 | `SettingsForm.vb:291, 744` |
| 30 | Preferences: AI assistant | panel | Turn on and set up the optional assistant; Test Connection | Inside Preferences | 1 | no | W, P | 0/2/0/0 | `SettingsForm.vb:299, 929` |
| 31 | Edit journal: Journal | panel | Name, publisher, ISSNs, five links, Favorite, Watch list, notes | Inside Edit journal | 1-3 | yes | W | 0/0/0/0 | `JournalEditForm.vb:132-134` |
| 32 | Edit journal: Readiness Checklist | panel | The journal's reusable readiness template | Inside Edit journal | 1-3 | no | W, P | 0/0/0/0 | `JournalEditForm.vb:136-138` |
| 33 | Edit journal: Facts and Metrics | panel | Looked-up facts and metrics you enter | Inside Edit journal | 1-3 | no | W, P | 0/0/0/0 | `JournalEditForm.vb:140-142` |

### 3.4 Modal dialogs

| # | Surface | Kind | What it is for | Reached from | Depth | Everyday | Rules it breaks | Boxes n/e/c/q | Where |
|---:|---|---|---|---|---|---|---|---|---|
| 34 | About PaperRoute (`AboutForm`) | dialog | Version, license, Copy Version Info | Logo or name double-click; Settings menu | 1 | no | W, P | 1/1/0/0 | `Form1.vb:145` |
| 35 | Add Manuscript (`AddManuscriptForm`) | dialog | Add a manuscript you are writing | Board; Welcome; Import & Export | 1 | yes | W, P, S (saves authors, then manuscripts) | 0/1/0/0 | `Form1.vb:3557` |
| 36 | Paste a Title Page (`TitlePageImportForm`) | dialog | Propose title, authors, abstract, keywords from a pasted title page | Add Manuscript | 2 | yes | W, P | 0/0/0/0 | `AddManuscriptForm.vb:361` |
| 37 | The Route (`ManuscriptRouteViewForm`) | dialog | A manuscript's journey as a map; a waypoint opens the page there | Board card; card menu; manuscript page; Insights Route Map | 1 | yes | W, P | 0/0/0/0 | `Form1.vb:3633` |
| 38 | Delete Manuscript (`DeleteManuscriptForm`) | dialog | Type DELETE to remove a manuscript permanently | Card menu; manuscript page ⋯ | 1 | no | allowed (destructive confirmation) | 0/0/0/0 | `Form1.vb:3666`; `EditManuscriptForm.vb:3844` |
| 39 | Move to File Drawer (`MoveToFileDrawerForm`) | dialog | Move a manuscript to the File Drawer with an optional reason | Card menu | 1 | no | W, P, S | 0/0/0/0 | `Form1.vb:3711` |
| 40 | Import BibTeX / RIS (`BibliographyImportForm`) | dialog | Review parsed records; likely duplicates unchecked | Import & Export; Welcome chip | 1 | no | W, P | 0/1/0/0 | `Form1.Bibliography.vb:69` |
| 41 | Choose manuscripts to export (`BibliographyExportSelectionForm`) | dialog | Which manuscripts go into a BibTeX or RIS file | Import & Export | 1 | no | W, P, E (blank list when empty) | 0/1/0/0 | `Form1.Bibliography.vb:274` |
| 42 | Map Spreadsheet Columns (`ExcelMappingForm`) | dialog | Map an unknown workbook's columns | Import Spreadsheet, after the picker | 1 | no | W, P | 0/2/0/0 | `Form1.vb:4571` |
| 43 | Publication & CV Export (`PublicationExportForm`) | dialog | Formatted lists of your work for a CV or report | Import & Export | 1 | no | W, P | 1/4/0/0 | `Form1.JournalsExports.vb:19` |
| 44 | Check for Publications (`PublicationCheckForm`) | dialog | Ask Crossref (and ORCID) whether tracked work is published | Import & Export; manuscript page ⋯ | 1 | no | W, P, S (saves each change) | 1/2/0/0 (incl. Review Match, note 5) | `Form1.Publications.vb:41` |
| 45 | Fill Blanks from Crossref (`FillBlanksForm`) | dialog | Fill empty fields of manuscripts with a DOI after showing each change | Import & Export | 1 | no | W, P, S (saves the library) | 0/0/0/0 | `Form1.Publications.vb:53` |
| 46 | Mark Published (`MarkPublishedForm`) | dialog | Shows what Mark Published will change | Deadlines row; publication check | 1-2 | no | W, P, S | 0/0/0/0 | `Form1.Publications.vb:89`; `PublicationCheckForm.vb:541` |
| 47 | Pipeline Report / Route Report (`ReportForm`) | dialog | Preview and save a read-only report | Insights; manuscript page ⋯ | 1 | no | W, P | 0/1/0/0 | `Form1.Reports.vb:37, 53` |
| 48 | Update from OpenAlex (`CitationsUpdateForm`) | dialog | Check which works are yours before figures are saved | Your Citations | 1 | no | W, P | 0/0/0/0 | `Form1.Citations.vb:363` |
| 49 | Postpone / Set Deadline (`DeadlineDateForm`) | dialog | Tomorrow, a week, two weeks or any date | Deadlines row | 1 | yes | W, P, S (saves at once) | 0/0/0/0 | `Form1.Deadlines.vb:424` |
| 50 | Add / Edit Reminder (`ReminderEditForm`) | dialog | A manuscript-specific reminder | Deadlines "+ Add Reminder"; row menu | 1 | yes | W, P, S (saves at once) | 0/2/0/0 | `Form1.Deadlines.vb:518, 547` |
| 51 | Preferences (`SettingsForm`) | dialog | Appearance, thresholds, reminders, updates, online services, AI assistant | Settings menu; rail Online button | 1 | no (note 6) | W, P | 0/0/0/0 (see rows 24, 30) | `Form1.vb:4922` |
| 52 | Add OpenAlex Key (`OpenAlexKeyForm`) | dialog | Paste an OpenAlex key | Preferences: Online services | 2 | no | W, P | 0/0/0/0 (inherits row 53's box) | `SettingsForm.vb:1542` |
| 53 | Add Claude Key / Add Server Key (`ApiKeyForm`) | dialog | Paste an assistant key | Preferences: AI assistant | 2 | no | W, P | 0/1/0/0 | `SettingsForm.vb:1549` |
| 54 | Diagnostics (`DiagnosticsForm`) | dialog | Safe technical information; Copy, Save, Open Data Folder | Settings menu | 1 | no | W, P | 2/3/0/0 | `Form1.vb:4983` |
| 55 | User Guide (`HelpForm`) | dialog | The User Guide, opened at a section | Rail Help; F1; Quick Start; the Overview "How to choose a journal" link (`EditManuscriptForm.vb:1836`); three links in other windows (`JournalCandidateForm.vb:370`, `SettingsForm.vb:899, 1155`) | 1-2 | no (note 6) | W, P | 1/2/0/0 | `Form1.RemindersHelp.vb:20` and 5 more (App. B) |
| 56 | PaperRoute ran into a problem (`UnexpectedErrorForm`) | dialog | The error, unsaved changes, Keep Working or Close | Any unhandled error on the UI thread | 1 (gap) | no | allowed (proposed; question 3) | 0/0/0/0 | `Form1.ManuscriptPage.vb:1325` |
| 57 | PaperRoute could not start (`StartupFailureForm`) | dialog | What failed; previous save or a backup to put in place | Storage or library load failure; author library unreadable mid-session | 0-1 | no | allowed (answer needed before any record is shown) | 1/3/1/0 | `StartupFailureForm.vb:236, 240` |
| 58 | Update available (`UpdatePromptForm`) | dialog | Offer an update; Download & Restart or Later | Check for Updates; automatic check 1.5 s after start | 1 | no | W, P | 1/0/0/0 (`UpdateService.vb:387`) | `UpdateService.vb:376` |
| 59 | Installing update (`UpdateProgressForm`) | dialog | Progress while the update installs | After Download & Restart | 1 | no | W | 0/0/0/0 | `UpdateService.vb:223` (Show, owner disabled) |
| 60 | Confirm Restore (`InputBox`) | dialog | Type RESTORE to replace the library | Restore Backup, after the preview box | 1 | no | P (second confirmation for one act) | 0/0/0/0 | `Form1.vb:4240` |
| 61 | Add / Edit Version (`ManuscriptVersionEditForm`) | dialog | One version record: label, date, notes, file, storage, submission, round, current | Versions tab | 1 | yes | W, P | 0/3/0/0 | `ManuscriptVersionHistoryControl.vb:978, 1050` |
| 62 | Journal, Preprint & Links (`ManuscriptLinksForm`) | dialog | Link the target journal; manuscript URL; preprint; related links | Overview | 1 | yes | W, P, S (own Save & Close), E (empty related links) | 4/7/0/0 | `EditManuscriptForm.vb:3887` |
| 63 | Add / Edit Link (`ExternalLinkEditForm`) | dialog | One related link: label, URL, notes | Journal, Preprint & Links | 2 | yes | W, P | 0/3/0/0 | `ManuscriptLinksForm.vb:916, 957` |
| 64 | Add / Edit Author, manuscript (`ManuscriptAuthorForm`) | dialog | Person, affiliations for this manuscript, corresponding | Authors tab | 1 | yes | W, P, E (blank lists) | 0/1/0/0 | `EditManuscriptForm.vb:2902, 2963` |
| 65 | Manage Library (`AuthorLibraryForm` as a window) | dialog | Authors & Affiliations over a manuscript | Authors tab; Add Author with an empty library | 1 | yes | W, P, S (saves the library and maybe manuscripts) | 0/0/0/0 (same code as rows 5, 13) | `EditManuscriptForm.vb:2832` |
| 66 | Add / Edit Author, reusable (`AuthorEditForm`) | dialog | Names, ORCID iD, This is me, notes | Authors & Affiliations | 1-2 | yes | W, P | 0/2/0/0 | `AuthorLibraryForm.vb:497, 550` |
| 67 | Add / Edit Affiliation (`AffiliationEditForm`) | dialog | Institution, department, place, notes | Affiliations tab | 1-2 | no | W, P | 0/1/0/0 | `AuthorLibraryForm.vb:912, 953` |
| 68 | ORCID Public-Profile Import (`OrcidLookupForm`) | dialog | Preview an author's public ORCID record and choose what to apply | Authors & Affiliations "ORCID..." | 1-2 | no | W, P | 0/3/0/0 | `AuthorLibraryForm.vb:622` |
| 69 | DOI & Crossref Metadata (`CrossrefLookupForm`) | dialog | Look up a DOI and choose what to apply | Overview | 1 | yes | W, P | 0/2/0/0 | `EditManuscriptForm.vb:2553` |
| 70 | Shortlist journal (`JournalCandidateForm`) | dialog | One shortlist journal: status, why, history, trust and fit checks | Overview shortlist | 1 | yes | W, P | 0/2/0/0 | `EditManuscriptForm.vb:2020` |
| 71 | Find Journals (`JournalSuggestionsForm`) | dialog | Journals that published similar work, with evidence | Overview shortlist | 1 | no | W, P | 0/1/0/0 | `EditManuscriptForm.vb:2116` |
| 72 | Choose from Library (`JournalPickerForm`) | dialog | Pick a journal from the Journal Library | Record Submission; Links; Readiness | 2 | yes | W, P, E (no-match list is blank) | 1/0/0/0 | `AddSubmissionForm.vb:644`; `ManuscriptLinksForm.vb:645`; `ManuscriptReadinessForm.vb:897` |
| 73 | Manage Journals (`JournalLibraryForm` as a window) | dialog | The Journal Library over Links | Links "Manage Journals..."; Choose from Library when the Journal Library is empty (`ManuscriptLinksForm.vb:628-633`) | 2 | no | W, P, S (saves while the manuscript is unsaved) | 0/0/0/0 (same code as row 6) | `ManuscriptLinksForm.vb:675` |
| 74 | Edit journal (`JournalEditForm`) | dialog | One reusable journal, its checklist and metrics | Journals page; Manage Journals | 1-3 | yes | W, P, S (nested saves) | 1/4/1/0 | `JournalLibraryForm.vb:443, 483` |
| 75 | Add / Edit Requirement (`JournalChecklistItemEditForm`) | dialog | One checklist template requirement | Edit journal | 2-4 | no | W, P | 0/1/0/0 | `JournalEditForm.vb:834, 939` |
| 76 | Add / Edit Metric (`JournalMetricForm`) | dialog | One metric you enter | Edit journal | 2-4 | no | W, P, S (nested saves) | 0/2/0/0 | `JournalEditForm.vb:1328` |
| 77 | Look Up Facts (`JournalFactsForm`) | dialog | OpenAlex and DOAJ facts, previewed before saving | Journals facts card | 1-3 | no | W, P | 0/0/0/0 | `JournalLibraryForm.vb:710` |
| 78 | Read Author Instructions (`AuthorInstructionsForm`) | dialog | Assistant proposes checklist requirements from pasted instructions | Edit journal (assistant on) | 2-4 | no | W, P | 0/0/0/0 | `AuthorInstructionsForm.vb:86` (from `JournalEditForm.vb:897`) |
| 79 | Submission Readiness (`ManuscriptReadinessForm`) | dialog | Apply a journal checklist and track each requirement | Readiness & Packets; vault "Save & Go To" | 1 | yes | W, P, S (own Save & Close) | 3/5/1/0 | `EditManuscriptForm.Workflow.vb:22` |
| 80 | Readiness Notes (`ReadinessItemNotesForm`) | dialog | Notes for one requirement | Submission Readiness | 2 | yes | W, P, S (third commit level) | 0/0/0/0 | `ManuscriptReadinessForm.vb:1118` |
| 81 | Submission Packet Vault (`SubmissionPacketVaultForm`) | dialog | The exact files for one version; fingerprints; export | Readiness & Packets; Versions; submission detail | 1 | yes | W, P, S (own Save & Close) | 0/4/3/0 | `EditManuscriptForm.Workflow.vb:26` |
| 82 | New / Edit Packet (`SubmissionPacketEditForm`) | dialog | Name, version, readiness profile, submission, round, notes | Vault | 2 | yes | W, P, S | 0/3/0/0 | `SubmissionPacketVaultForm.vb:1082, 1131` |
| 83 | Add / Edit Packet File (`SubmissionPacketFileEditForm`) | dialog | Role, label, notes, storage of one file | Vault | 2 | yes | W, P, S | 0/3/0/0 | `SubmissionPacketVaultForm.vb:1259, 1317` |
| 84 | Export Submission Packet (`PacketExportForm`) | dialog | Preview a packet and write one .zip | Vault | 2 | no | W, P | 0/0/0/0 | `SubmissionPacketVaultForm.Export.vb:37` |
| 85 | Record / Edit Submission (`AddSubmissionForm`) | dialog | Journal, date, ID, portal, follow-up, notes | Submissions tab; stage change on Save; packet | 1 | yes | W, P, S (own Record step) | 1/2/0/0 | `EditManuscriptForm.vb:3547, 3623, 3921`; `EditManuscriptForm.Workflow.vb:60` |
| 86 | Add / Edit Decision (`AddDecisionForm`) | dialog | Decision, date, revision deadline, notes | Editorial History; Overview revision deadline; after a decision letter | 1-2 | yes | W, P, S | 0/0/0/0 | `SubmissionDetailsForm.vb:1274, 1395`; `EditManuscriptForm.vb:2471, 2497` |
| 87 | Read Decision Letter (`DecisionLetterForm`) | dialog | Assistant reads a pasted letter into a decision and reviewer comments | Editorial History; Reviewer Responses (assistant on) | 1-3 | no | W, P | 0/0/0/0 | `DecisionLetterForm.vb:98` (from `SubmissionDetailsForm.vb:1325`, `ReviewerResponseMatrixForm.vb:400`) |
| 88 | Add / Edit Correspondence (`AddCorrespondenceForm`) | dialog | Type, date, title, file, source URL, notes | Correspondence & Files | 1-2 | yes | W, P, S | 0/3/0/0 | `SubmissionDetailsForm.vb:2309, 2338` |
| 89 | Draft Cover Letter (`CoverLetterForm`) | dialog | A cover-letter starting point to copy or save | Readiness & Packets (assistant on) | 1 | no | W, P | 0/0/0/0 | `EditManuscriptForm.Readiness.vb:214` |
| 90 | Submission Details (`SubmissionDetailsForm` as a window) | dialog | The submission details opened in the middle of Save | Save with a stage that needs a decision | 1 | no | W, P | 0/0/0/0 (same code as rows 20, 22) | `EditManuscriptForm.vb:4088` |
| 91 | Reviewer Response Matrix (`ReviewerResponseMatrixForm` as a window) | dialog | The matrix on a clone, with its own Save & Close | Modal Submission Details only | 2 | no | W, P, S | 0/1/0/0 | `SubmissionDetailsForm.vb:500` |
| 92 | Reviewer comment (`ReviewerResponseItemForm`) | dialog | One comment: decision, round, reviewer, status, comment, action, draft, location | Reviewer Responses | 1-3 | yes | W, P, S (Save Comment step) | 1/1/0/0 | `ReviewerResponseMatrixForm.vb:379, 420` |
| 93 | Export Markdown (`ReviewerResponseExportForm`) | dialog | Edit the reviewer-response Markdown before saving | Reviewer Responses | 1-3 | no | W, P | 0/1/0/0 | `ReviewerResponseMatrixForm.vb:463` |
| 94 | Suggest a Starting Point (`AssistantDraftForm`) | dialog | Assistant drafts one reviewer response | Reviewer comment (assistant on) | 2-4 | no | W, P | 0/0/0/0 | `ReviewerResponseItemForm.vb:247` |
| 95 | Before Sending (`AssistantConsentForm`) | dialog | Shows what will be sent and to whom; Send or Cancel | Every assistant feature, when consent is needed | 2-5 | no | W, P | 0/0/0/0 | `AssistantConsentForm.vb:194` |

### 3.5 File pickers

| # | Surface | Kind | What it is for | Reached from | Depth | Everyday | Rules it breaks | Boxes | Where |
|---:|---|---|---|---|---|---|---|---|---|
| 96 | System file pickers (`OpenFileDialog`, `SaveFileDialog`) | file-picker | Choose a file to import, attach or restore, or where to save | 20 call sites | 1-4 | some (version, correspondence and packet files) | allowed (file picker) | none | Appendix B (20 rows) |

### 3.6 System notification

| # | Surface | Kind | What it is for | Reached from | Depth | Everyday | Rules it breaks | Boxes n/e/c/q | Where |
|---:|---|---|---|---|---|---|---|---|---|
| 97 | Startup reminder notification (`NotifyIcon` balloon) | system notification | Reminders due within the chosen days ahead, shown by Windows outside the window, at most once a day, when turned on in Preferences (row 26) | App start (`Form1.vb:224` -> `Form1.RemindersHelp.vb:39-86`) | n/a (not modal) | no | none (outside the window by design) | 0/0/0/0 | `Form1.RemindersHelp.vb:76` -> `Services\WindowsNotificationService.vb:36-56` |

**Box totals in the table.** The Boxes column sums to 200: 56 notices, 118 errors, 17 confirmations and 9 questions, where row 24 carries `Form1.vb:4935` (a question asked by the main window after Preferences closes) and row 44 carries the two Review Match boxes in `MarkPublishedForm.vb`. Two call sites belong to no single surface: `ApplicationEvents.vb:70` (fatal error with no window) and `Services\FileOpenService.vb:91` (open a program?): 1 error and 1 question. 200 + 2 = 202, and by kind 56 + 119 + 17 + 10.

**Notes.**

1. "Reached from" for the main window's pages comes from `Form1.Shell.vb:231-236` (rail) and `Form1.vb:2475-2492` (Ctrl+1 to Ctrl+6).
2. Rows 5, 6, 19 and 21 are `Form` classes hosted inside the main window (`TopLevel = False`, `Form1.Shell.vb:1282-1326`; `EditManuscriptForm.vb:3329`; `SubmissionDetailsForm.vb:534`). They are pages or panels today; the same classes also open as modal windows (rows 65, 73, 90, 91).
3. `EditManuscriptForm` has a dialog mode (`EditManuscriptForm.vb:233`, footer `1590-1731`) that no app code constructs; its only construction is `pageMode:=True` at `Form1.ManuscriptPage.vb:86`.
4. The manuscript page opens 15 dialog classes, not the 10 the issue names: the 10, plus Delete Manuscript (`EditManuscriptForm.vb:3844`), Draft Cover Letter (`EditManuscriptForm.Readiness.vb:214`), Submission Readiness and the Packet Vault (`EditManuscriptForm.Workflow.vb:22, 26`), and Add / Edit Version through the Versions tab control (`ManuscriptVersionHistoryControl.vb:978, 1050`).
5. The two boxes in `MarkPublishedForm.vb` (167, 173) are in the shared `PublicationActions.OpenMatch` (Review Match), raised from the publication check (`PublicationCheckForm.vb:494`) and from a Deadlines row (`Form1.Publications.vb:74`). They are counted once, on row 44.
6. Readers disagreed on whether Preferences and Help are everyday; see 2.3.
7. `Form1.OpenAssistantSettings` (`Form1.Online.vb:155-159`) opens Preferences at the AI assistant but has no caller in the app.

## 4. Chains

A chain here is three or more surfaces open at once, counting the main window: the main window and at least two modal windows stacked on it. "MP" is the manuscript page in the main window. Depth is the number of modal windows. Any window in a chain can also raise a message box as one more level; boxes are left out. The line on each arrow is where the next window opens.

### Depth 5

1. MP, Save with a stage that needs a decision > Submission Details (`EditManuscriptForm.vb:4088`) > Reviewer Response Matrix (`SubmissionDetailsForm.vb:500`) > reviewer comment (`ReviewerResponseMatrixForm.vb:379` or `420`) > Suggest a Starting Point (`ReviewerResponseItemForm.vb:247`) > Before Sending, on Try Again (`AssistantDraftForm.vb:333` -> `AssistantConsentForm.vb:194`).
2. MP > Journal, Preprint & Links (`EditManuscriptForm.vb:3887`) > Manage Journals (`ManuscriptLinksForm.vb:675`) > Edit journal (`JournalLibraryForm.vb:443` or `483`) > Read Author Instructions (`JournalEditForm.vb:897` -> `AuthorInstructionsForm.vb:86`) > Before Sending (`AuthorInstructionsForm.vb:606` -> `AssistantConsentForm.vb:194`).

### Depth 4

3. MP > Submission Details (`EditManuscriptForm.vb:4088`) > Reviewer Response Matrix (`SubmissionDetailsForm.vb:500`) > reviewer comment (`ReviewerResponseMatrixForm.vb:379`/`420`) > Before Sending (`ReviewerResponseItemForm.vb:244` -> `AssistantConsentForm.vb:194`).
4. MP > Submission Details > Reviewer Response Matrix > Read Decision Letter (`ReviewerResponseMatrixForm.vb:400` -> `DecisionLetterForm.vb:98`) > Before Sending (`DecisionLetterForm.vb:665`).
5. MP > Submission Details > Reviewer Response Matrix > Export Markdown (`ReviewerResponseMatrixForm.vb:463`) > save picker (`ReviewerResponseExportForm.vb:65`).
6. MP > Journal, Preprint & Links > Manage Journals > Edit journal > Add / Edit Metric (`JournalEditForm.vb:1328`).
7. MP > Journal, Preprint & Links > Manage Journals > Edit journal > Add / Edit Requirement (`JournalEditForm.vb:834`/`939`).
8. MP > Journal, Preprint & Links > Manage Journals > Edit journal > Read Author Instructions, when no consent is needed (`JournalEditForm.vb:897`).

### Depth 3

9. MP > Submission Details (`EditManuscriptForm.vb:4088`) > Reviewer Response Matrix (`SubmissionDetailsForm.vb:500`) > reviewer comment (`ReviewerResponseMatrixForm.vb:379`/`420`). This is the modal form of the "three nested editors" the issue names.
10. MP > Submission Details > Read Decision Letter (`SubmissionDetailsForm.vb:1325`) > Before Sending (`DecisionLetterForm.vb:665`). Add Decision then opens at depth 2 in its place (`SubmissionDetailsForm.vb:1333`).
11. MP > Submission Details > Add / Edit Correspondence (`SubmissionDetailsForm.vb:2309`/`2338`) > Browse picker (`AddCorrespondenceForm.vb:318`).
12. MP, Submissions tab, inline Reviewer Responses > reviewer comment (`ReviewerResponseMatrixForm.vb:379`/`420`) > Suggest a Starting Point (`ReviewerResponseItemForm.vb:247`) > Before Sending (`AssistantDraftForm.vb:333`). This is the everyday form of the three nested editors: page, inline detail, inline matrix, then three modal windows.
13. MP > Journal, Preprint & Links > Manage Journals > Edit journal (`JournalLibraryForm.vb:443`/`483`).
14. MP > Journal, Preprint & Links > Manage Journals > Look Up Facts (`JournalLibraryForm.vb:710`).
15. Journals page > Edit journal (`JournalLibraryForm.vb:443`/`483`) > Read Author Instructions (`JournalEditForm.vb:897`) > Before Sending (`AuthorInstructionsForm.vb:606`).
16. MP > Submission Packet Vault (`EditManuscriptForm.Workflow.vb:26`) > Add / Edit Packet File (`SubmissionPacketVaultForm.vb:1259`/`1317`) > Browse picker (`SubmissionPacketFileEditForm.vb:535`).
17. MP > Submission Packet Vault > Export Packet (`SubmissionPacketVaultForm.Export.vb:37`) > save picker (`PacketExportForm.vb:838`).

### Depth 2

18. Main window > Preferences (`Form1.vb:4922`) > User Guide (`SettingsForm.vb:899` or `1155`), Add OpenAlex Key (`SettingsForm.vb:1542`) or Add Claude / Server Key (`SettingsForm.vb:1549`).
19. Main window > Diagnostics (`Form1.vb:4983`) > save picker (`DiagnosticsForm.vb:339`).
20. Main window > Add Manuscript (`Form1.vb:3557`) > Paste a Title Page (`AddManuscriptForm.vb:361`).
21. Main window > Check for Publications (`Form1.Publications.vb:41`) > Mark Published (`PublicationCheckForm.vb:541`).
22. Main window > Publication & CV Export (`Form1.JournalsExports.vb:19`) > save picker (`PublicationExportForm.vb:735`).
23. Main window > Pipeline or Route Report (`Form1.Reports.vb:37`/`53`) > save picker (`ReportForm.vb:195`).
24. Main window > PaperRoute could not start (`StartupFailureForm.vb:240`) > backup picker (`StartupFailureForm.vb:821`), or the restore confirmation box (`StartupFailureForm.vb:801`).
25. Journals page > Edit journal > Add / Edit Requirement (`JournalEditForm.vb:834`/`939`) or Add / Edit Metric (`JournalEditForm.vb:1328`).
26. Library: Authors & Affiliations is a page, so its editors are depth 1; the same editors opened from MP > Manage Library (`EditManuscriptForm.vb:2832`) are depth 2: Add / Edit Author (`AuthorLibraryForm.vb:497`/`550`), ORCID (`622`), Add / Edit Affiliation (`912`/`953`).
27. MP > Journal, Preprint & Links > Choose from Library (`ManuscriptLinksForm.vb:645`), Add / Edit Link (`916`/`957`) or Manage Journals (`675`).
28. MP > Record / Edit Submission (`EditManuscriptForm.vb:3547`, `3623`, `3921`; `EditManuscriptForm.Workflow.vb:60`) > Choose from Library (`AddSubmissionForm.vb:644`).
29. MP > Shortlist journal (`EditManuscriptForm.vb:2020`) > User Guide (`JournalCandidateForm.vb:370`).
30. MP > Submission Readiness (`EditManuscriptForm.Workflow.vb:22`) > Choose from Library (`ManuscriptReadinessForm.vb:897`) or Readiness Notes (`1118`).
31. MP > Submission Packet Vault (`EditManuscriptForm.Workflow.vb:26`) > New / Edit Packet (`SubmissionPacketVaultForm.vb:1082`/`1131`), Add / Edit Packet File (`1259`/`1317`) or Export Packet (`SubmissionPacketVaultForm.Export.vb:37`).
32. MP > Draft Cover Letter (`EditManuscriptForm.Readiness.vb:214`) > Before Sending (`CoverLetterForm.vb:423`) or save picker (`CoverLetterForm.vb:505`).
33. MP > Add / Edit Version (`ManuscriptVersionHistoryControl.vb:978`/`1050`) > Browse picker (`ManuscriptVersionEditForm.vb:1470`).
34. MP, inline submission detail > Add / Edit Correspondence (`SubmissionDetailsForm.vb:2309`/`2338`) > Browse picker (`AddCorrespondenceForm.vb:318`); > Read Decision Letter (`SubmissionDetailsForm.vb:1325`) > Before Sending (`DecisionLetterForm.vb:665`).
35. MP, inline Reviewer Responses > Export Markdown (`ReviewerResponseMatrixForm.vb:463`) > save picker (`ReviewerResponseExportForm.vb:65`); > reviewer comment > Suggest a Starting Point (`ReviewerResponseItemForm.vb:247`) or Before Sending (`ReviewerResponseItemForm.vb:244`).
36. MP > Submission Details (`EditManuscriptForm.vb:4088`) > Reviewer Response Matrix (`SubmissionDetailsForm.vb:500`), Add / Edit Decision (`1274`/`1395`), Add / Edit Correspondence (`2309`/`2338`), Read Decision Letter (`1325`) or a file picker (`2077`, `2109`).

Moving between Submission Readiness and the Packet Vault through "Save & Go To..." closes one window before opening the other (`EditManuscriptForm.Workflow.vb:17-45`), so they never stack.

## 5. The target map

Each row says where the surface goes. Where a window goes away, the fields and actions it holds are listed with their new home.

### 5a. Stay as pages (and the panels inside them)

| Surface (row) | Becomes | What changes |
|---|---|---|
| Main window (1) | The one Avalonia window: rail, page host, one notice strip | The status line grows into an in-window notice strip that carries startup, recovery, save-failure, update and result notices. |
| Example library window (2) | Stays a separate instance of the main window | The separate process is what guarantees your library is never opened. Its banner is its page-level notice. |
| Board (3), welcome (12) | Board page; welcome stays its empty state | Card actions stay on the card (5b, 5c). |
| Library: Manuscripts (4) | Library page | No change. |
| Library: Authors & Affiliations (5), Affiliations (13) | Library page, Authors & Affiliations view (native, not a hosted Form) | Author, affiliation and ORCID editors open beside the list (5b, 5c). |
| Journals (6) | Journals page: list and selected-journal detail panel | Edit journal, its tabs (31-33) and Look Up Facts open in the detail panel. |
| Deadlines (7) | Deadlines page | Postpone and reminders edit in the row (5b). |
| Insights (8), Your Citations (14) | Insights page | The route opens on the manuscript page; reports and the citations update open in the page (5b, 5c). |
| Import & Export (9) | Import & Export page | Import review, export and result steps become panels of this page; file pickers stay. |
| Manuscript page (10) and its tabs (15-23) | Manuscript page with sections Overview, Authors, Versions, Submissions, Readiness & Packets, and a new Route section | Its 15 dialog classes become panels, rows or sections writing to the same working copy. The submission detail pane (19) and its tabs (20-22) become native panels. |
| Missing manuscript (11) | Manuscript page empty state | No change. |
| Preferences (51) and its sections (24-30) | A Settings page in the rail; the seven sections become its panels | See 5c. |
| Startup reminder notification (97) | Stays a system notification | On Windows the platform toast or balloon; on Linux and macOS the platform notification service. The same summary also goes in the notice strip, so a researcher who missed or blocked the system notification still sees it; the Preferences row (26) keeps its name with "Windows" replaced by the platform's word. |

### 5b. Become in-page panels or inline editing

| Surface (row) | Becomes | Where it goes; where its fields and actions go |
|---|---|---|
| Move to File Drawer (39) | In-page panel | A small panel on the board card (optional reason, Move, Cancel), then an Undo notice; also a "Move to File Drawer" action in the manuscript page header that sets the location in the working copy and shows the Overview's File Drawer card. Location, date, reason and the History event are unchanged. |
| Publication & CV Export (43) | In-page panel | Import & Export page, one Export panel: scope, style, format, group by type, item checks, preview, Save Export, Copy to Clipboard. It also takes over the BibTeX/RIS selection (5c). |
| Mark Published (46) | In-page panel | The publication result (Check for Publications panel, Deadlines row) expands to "PaperRoute will:" with the list of changes, Mark Published and Cancel. |
| Pipeline Report / Route Report (47) | In-page panel | Insights page and the manuscript page's Route section: manuscript checklist, include next deadline, privacy note, preview, Save Report, open after saving. |
| Postpone / Set Deadline (49) | Inline editing | The Deadlines row: tomorrow, one week, two weeks, a date field, and the same "Postpone to <date>" button. |
| Add / Edit Reminder (50) | Inline editing | Deadlines page: an add row at the top and in-row editing (manuscript, date, description, notes); problems shown beside the field. |
| Add OpenAlex Key (52), Add Claude / Server Key (53) | Inline editing | Settings page, Online services and AI assistant panels: a masked key field with Show, the hint, the key-page link, Add / Replace / Remove. |
| User Guide (55) | In-page panel | A Help side panel docked in the main window, opened at a section by F1, the rail Help button or any help link (all 7 routes). Find, the GitHub link and Explore an Example Library stay in it. |
| Update available (58) | In-page panel | An update notice in the notice strip: version, What's new, Download & Restart, Later. A manual check shows its result in Settings > Updates. |
| Add / Edit Version (61) | In-page panel | Versions section, edit mode of the details pane: label, date, notes, file (Browse stays a picker), storage mode, submission, decision, revision round, make current. |
| Add / Edit Link (63) | Inline editing | Overview, Links card: each related link row (label, URL, notes) edited in place. |
| Add / Edit Author, manuscript (64) | Inline editing | Authors section: an expanded author row (person, affiliations for this manuscript, corresponding). An empty library shows its own explanation and a way to add a person. |
| Add / Edit Author, reusable (66), Add / Edit Affiliation (67) | In-page panel | Authors & Affiliations view: a detail panel beside each list with every field. |
| Shortlist journal (70) | In-page panel | Overview shortlist: an editor that expands under the row (journal, status, why, history, trust and fit checks with hints). The guide opens in the Help panel. |
| Choose from Library (72) | Inline editing | A searchable Journal Library chooser in the journal field itself (target journal, submission editor, readiness "New from Journal"), favorites and watch list first, with an empty-state line and "Add a journal". |
| Edit journal (74) and tabs (31-33) | In-page panel | Journals page detail panel with sections Journal, Readiness checklist, Facts and metrics; every field (name, publisher, ISSNs, five links, Favorite, Watch list, notes) and every checklist and metric action. One Save per journal record (question 4). |
| Add / Edit Requirement (75), Add / Edit Metric (76) | Inline editing | Rows of the Readiness checklist and Facts and metrics lists in the journal detail panel. Definitions, "Where it's published" and the DORA note sit beside the row. |
| Readiness Notes (80) | Inline editing | The notes box under the requirement's instructions in the Readiness section. |
| New / Edit Packet (82), Add / Edit Packet File (83) | In-page panel | Packet detail and file detail panels of the Packets section (replacing today's read-only detail boxes). Browse stays a picker. |
| Export Submission Packet (84) | In-page panel | An export panel from the selected packet: preview, include, names, fingerprints, authors and abstract, hidden-information acknowledgment, Export, status line. The save picker stays. |
| Record / Edit Submission (85) | In-page panel | Submissions section: the detail area's edit mode; Record Submission adds a draft row. The packet route opens the same panel. Use Library becomes the inline chooser. Save with a stage that needs a submission (today the question at `EditManuscriptForm.vb:3978`, then `AddSubmissionForm` at `3921`, then Save goes on): Save stops before writing anything; the Submissions section opens the new-submission panel with a "Record and Save" button; the stage change is held in the working copy, unsaved. "Record and Save" adds the submission and finishes the Save; Cancel ends the Save with nothing written, and the stage change stays an unsaved edit like any other. |
| Add / Edit Decision (86) | Inline editing | Editorial History: the decision row expands to its fields. The Overview's revision deadline edits the latest decision's deadline in place. A decision-letter proposal fills the same editor with its banner. Save with a stage that needs a decision (today the question at `EditManuscriptForm.vb:4064`, then the modal Submission Details at `4088`, then `ReconcileFromLatestWorkflow` at `4094-4103` inside the save path) follows the same flow as row 85: Save stops before writing, the latest submission's Editorial History opens with a new decision row and "Record and Save", and Cancel leaves the stage change unsaved with nothing written. |
| Add / Edit Correspondence (88) | Inline editing | Correspondence & Files: the details box beside the list becomes editable. Browse stays a picker. |
| Reviewer comment (92) | Inline editing | Reviewer Responses: the details box beside the list becomes the editor (metadata row; Comment & action, Draft response, Location & notes as sections). The Save Comment step goes; the manuscript's Save keeps it. |
| Export Markdown (93) | In-page panel | Reviewer Responses: an export preview replacing the list until closed. The save picker stays. |
| Paste a Title Page (36) | In-page panel | A panel of the new-manuscript page (5c, row 35) that fills title, authors, abstract and keywords; the proposal grid keeps Use, Author, Affiliations, Corresponding and Library. |

That is 30 surfaces (counting 31-33 with row 74 and 52-53 as two).

### 5c. Merge

**One Stage panel pattern.** Issue #161 asks that assistant-style result windows share one Stage pattern. These windows already work in stages (input or what will be sent, running with Stop, review, apply). Each becomes the same in-page Stage panel, opened where its result lands:

| Surface (row) | Stage panel opens in | Notes |
|---|---|---|
| Read Decision Letter (87) | Editorial History or Reviewer Responses | Paste, what is sent, Stop, review with Use checkboxes, then the decision editor inline. |
| Suggest a Starting Point (94) | Under the response field of the reviewer comment editor | Use as Draft, Replace Draft, Add Below, Try Again. |
| Draft Cover Letter (89) | Readiness & Packets | Copy; Save As keeps its picker. |
| Read Author Instructions (78) | Journal detail panel, Readiness checklist | Accepted requirements land in the same checklist. |
| Before Sending (95) | The first step of every assistant Stage panel | What will be sent, to whom, Send, "Don't ask again". Proposed; question 2. |
| Look Up Facts (77) | Journal detail panel | Name search, matches, changes preview with checkboxes, Stop, Save. |
| Find Journals (71) | Beside the Overview shortlist | Keywords and exact request, Stop and timer, evidence list, "Add N to Shortlist". |
| Update from OpenAlex (48) | Insights > Your Citations | What will be sent, Stop, works to check, Save. |
| Check for Publications (44) | Import & Export (library) and the manuscript page (one manuscript) | Match cards are the ones the Deadlines page already shows. From the manuscript page, changes go into the working copy (question 7). |
| Fill Blanks from Crossref (45) | Import & Export | Fill stays an explicit library-wide save, said as such. |
| ORCID Public-Profile Import (68) | Authors & Affiliations, beside the selected author | All apply options stay. |
| DOI & Crossref Metadata (69) | Overview, metadata section | Applies into the working copy, as today. |
| Import BibTeX / RIS (40), Map Spreadsheet Columns (42) | Import & Export, one import review panel | Steps: mapping (spreadsheets), preview, Import. The import preview box (`Form1.vb:4751`) is replaced by the panel's Import button. |

**Other merges.**

| Surface (row) | Merges into | Where its fields and actions go |
|---|---|---|
| Preferences (51) | Settings page in the rail | Its seven sections as panels; the rail Online button opens Online services; the restart offer becomes a notice with Restart now. The rail Settings menu items (Preferences, Work Offline, Check for Updates, Diagnostics, Backup, Restore, About) become sections or actions of this page; Backup and Restore also stay on Import & Export. |
| About PaperRoute (34) | Settings page, About section | Version, license, Copy Version Info with an inline "Copied" note; the logo double-click opens it. |
| Diagnostics (54) | Settings page, Diagnostics section | The report, Copy, Save (picker stays), Open Data Folder, results as inline notes. |
| Installing update (59) | The update notice | Progress and status inline; the window is locked while it installs. |
| The Route (37) | Manuscript page, Route section | Board, Insights and the header button open the page there; a waypoint scrolls to the right section instead of closing a window. |
| Add Manuscript (35) | The manuscript page in a "new manuscript" state | Title, target journal, starting stage (Idea or Draft), first deadline (as a reminder), Paste a Title Page; Save creates it, Discard abandons it (question 5). |
| Journal, Preprint & Links (62) | Overview | The Target journal row gains the library link (choose, clear, open homepage or portal); a Links card holds manuscript URL, preprint DOI and URL, and related links. Manage Journals goes (5e): a new journal is added inline in the chooser, full editing on the Journals page. The duplicate target journal box and the second commit step go. |
| Submission Readiness (79) | Readiness & Packets section, Readiness part | Profile switcher, requirement list and status, detail with notes, New from Journal, Add New Template Requirements, Delete Profile, Open Submission Portal, a link to the profile's packets. Its Save & Close, Cancel and "Save & Go To" go; the manuscript's Save keeps the edits. |
| Submission Packet Vault (81) | Readiness & Packets section, Packets part | Packet list with scope and Show all packets, packet detail, file list, file detail, integrity bar (Check Files, Record or Replace Fingerprint, Stop), Export, links to version, readiness, submission and Record Submission. Its Save & Close goes. |
| Choose manuscripts to export (41) | The Import & Export Export panel (row 43) | All / Published only / None selection serves BibTeX, RIS and Publication & CV export. |
| Confirm Restore (60) | One Restore confirmation (stays modal, 5d) | The backup preview (today the box at `Form1.vb:4227`) and the typed RESTORE field in one window; the mismatch box (`Form1.vb:4252`) becomes validation beside the field. |

That is 25 surfaces: 14 into the Stage pattern and 11 other merges.

### 5d. Stay modal

| Surface | Reason |
|---|---|
| Delete Manuscript (38) | Destructive confirmation; Delete is enabled only after typing DELETE. |
| One Restore confirmation (from 60 and `Form1.vb:4227`) | Destructive confirmation: it replaces the whole library. |
| PaperRoute could not start (57) | An answer is needed before any record can be shown or changed. Before startup it is the window's whole content; mid-session a blocking recovery panel. Its copy and open results become inline status lines. |
| PaperRoute ran into a problem (56) | Proposed to stay: work should not go on in an unknown state without an answer. Close goes through the normal save question. Question 3. |
| System file pickers (96, 20 sites) | File pickers. |
| 14 destructive confirmations and 8 questions (section 6) | Destructive confirmations (as section 6 defines them, a choice and not the Discard test); an answer needed before the record changes or is lost; or, for `Services\FileOpenService.vb:91`, a safety warning before PaperRoute starts another program (a fourth reason, proposed; question 13). The restore preview box (`Form1.vb:4227`) is not among them: it merges into the Restore confirmation above. |
| `ApplicationEvents.vb:70` | Not a rule case: there is no window to show anything in, so the system box stays. |

### 5e. Removed as duplicate routes

| Surface (row) | Why | Where its route goes |
|---|---|---|
| Manage Library (65) | Same class and code as the Authors & Affiliations page | "Go to Authors & Affiliations" from the Authors section for full library work; for the in-context case, create-person and create-affiliation steps in the inline author picker, so adding a person or an affiliation mid-edit never leaves the page (5f, "Library records created from a manuscript"). |
| Manage Journals (73) | Same class and code as the Journals page | The inline journal chooser with an inline "Add a journal" form (name, ISSN, portal) that does not leave the page; "Go to Journals" for full journal editing. An empty Journal Library shows that form at once instead of opening a window (today `ManuscriptLinksForm.vb:628-633`). |
| Submission Details as a window (90) | The inline detail pane has every field and action | Save with a stage that needs a decision stops before writing and opens the latest submission's Editorial History with a new decision row and "Record and Save" (row 86 in 5b). |
| Reviewer Response Matrix as a window (91) | Reachable only from row 90; the inline tab has every action | Its Save & Close goes; the manuscript's Save replaces it. |

### 5f. Tally

Of the 62 modal dialogs: 30 become panels or inline editing (5b), 25 merge (5c), 3 stay modal as windows (5d: Delete Manuscript, the startup failure, the error window; the Restore confirmation counts under 5c), and 4 are removed as duplicate routes (5e). 30 + 25 + 3 + 4 = 62.

Save boundaries after the map: every manuscript edit that today has its own Save step (Links, Readiness, Vault, packet and file editors, Record Submission, Add Decision, Add Correspondence, reviewer comment, Readiness Notes, Add Manuscript) writes to the page's working copy and is kept by the one Save bar. Single actions that save at once outside the page (Deadlines row actions, board card actions, Mark Published, Check for Publications, Fill Blanks) are question 1.

**Library records created from a manuscript.** Today three kinds of reusable record made from inside a manuscript are saved to the author or journal library at once, apart from the manuscript: new authors from Paste a Title Page and Add Manuscript (`authorRepository.Save` at `Form1.vb:3561-3590`, before the manuscript is added); people and affiliations made in Manage Library over the page (`EditManuscriptForm.vb:2828-2839`, which then reloads the library from disk); and journals made in Manage Journals over Links (`ManuscriptLinksForm.vb:628-633, 666-682`). Proposed (question 5): (a) new people, affiliations and journals created from the manuscript page, including the new-manuscript state, are held in the working copy beside the manuscript and written at the manuscript's Save, before the manuscript (as new authors are today); Discard drops them. They are created in place (the inline author picker's create-person and create-affiliation steps; the journal chooser's inline "Add a journal" form), so none of them makes the page leave with unsaved changes and meet the save question (`USER_GUIDE.md:169`). The alternative (b) is to save them at once and name that as a second, library-level save boundary. Editing an existing library record still happens on its own page (Authors & Affiliations, Journals), which goes through the save question like any other page change. Under (a), the possible defect in question 10 (Manage Library writing the unsaved working copy, `AuthorLibraryForm.vb:696-700`) cannot arise.

## 6. Message boxes

All 202 `MessageBox.Show(` call sites, by the file that holds them. Partial files are grouped under their class (`Form1*.vb`, `EditManuscriptForm*.vb`, `ManuscriptReadinessForm*.vb`, `SubmissionPacketVaultForm*.vb`). Each site is counted once, even where the same code runs on two surfaces. Appendix A lists every site.

| Owner | Sites | Notice | Error | Confirmation | Question | In-window notice | Inline validation | Stays a question | Stays: destructive confirmation | Not needed | Stays: no window |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| AboutForm | 2 | 1 | 1 | 0 | 0 | 2 | 0 | 0 | 0 | 0 | 0 |
| AddCorrespondenceForm | 3 | 0 | 3 | 0 | 0 | 0 | 3 | 0 | 0 | 0 | 0 |
| AddManuscriptForm | 1 | 0 | 1 | 0 | 0 | 0 | 1 | 0 | 0 | 0 | 0 |
| AddSubmissionForm | 3 | 1 | 2 | 0 | 0 | 1 | 2 | 0 | 0 | 0 | 0 |
| AffiliationEditForm | 1 | 0 | 1 | 0 | 0 | 0 | 1 | 0 | 0 | 0 | 0 |
| ApiKeyForm | 1 | 0 | 1 | 0 | 0 | 1 | 0 | 0 | 0 | 0 | 0 |
| ApplicationEvents (no window) | 1 | 0 | 1 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 1 |
| AuthorEditForm | 2 | 0 | 2 | 0 | 0 | 0 | 2 | 0 | 0 | 0 | 0 |
| AuthorLibraryForm | 9 | 4 | 3 | 2 | 0 | 6 | 0 | 0 | 2 | 1 | 0 |
| BibliographyExportSelectionForm | 1 | 0 | 1 | 0 | 0 | 0 | 0 | 0 | 0 | 1 | 0 |
| BibliographyImportForm | 1 | 0 | 1 | 0 | 0 | 0 | 0 | 0 | 0 | 1 | 0 |
| CrossrefLookupForm | 2 | 0 | 2 | 0 | 0 | 1 | 1 | 0 | 0 | 0 | 0 |
| DiagnosticsForm | 5 | 2 | 3 | 0 | 0 | 5 | 0 | 0 | 0 | 0 | 0 |
| EditManuscriptForm (manuscript page) | 13 | 6 | 3 | 1 | 3 | 5 | 2 | 3 | 1 | 2 | 0 |
| ExcelMappingForm | 2 | 0 | 2 | 0 | 0 | 0 | 2 | 0 | 0 | 0 | 0 |
| ExternalLinkEditForm | 3 | 0 | 3 | 0 | 0 | 0 | 3 | 0 | 0 | 0 | 0 |
| FileOpenService (service) | 1 | 0 | 0 | 0 | 1 | 0 | 0 | 1 | 0 | 0 | 0 |
| Form1 (main window and its pages) | 44 | 19 | 17 | 3 | 5 | 30 | 1 | 3 | 1 | 9 | 0 |
| HelpForm | 3 | 1 | 2 | 0 | 0 | 2 | 1 | 0 | 0 | 0 | 0 |
| JournalCandidateForm | 2 | 0 | 2 | 0 | 0 | 2 | 0 | 0 | 0 | 0 | 0 |
| JournalChecklistItemEditForm | 1 | 0 | 1 | 0 | 0 | 0 | 1 | 0 | 0 | 0 | 0 |
| JournalEditForm | 6 | 1 | 4 | 1 | 0 | 2 | 3 | 0 | 0 | 1 | 0 |
| JournalFactsCard (control) | 1 | 0 | 1 | 0 | 0 | 1 | 0 | 0 | 0 | 0 | 0 |
| JournalLibraryForm | 7 | 4 | 2 | 1 | 0 | 4 | 0 | 0 | 1 | 2 | 0 |
| JournalMetricForm | 2 | 0 | 2 | 0 | 0 | 1 | 1 | 0 | 0 | 0 | 0 |
| JournalPickerForm | 1 | 1 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 1 | 0 |
| JournalSuggestionsForm | 1 | 0 | 1 | 0 | 0 | 1 | 0 | 0 | 0 | 0 | 0 |
| ManuscriptAuthorForm | 1 | 0 | 1 | 0 | 0 | 0 | 0 | 0 | 0 | 1 | 0 |
| ManuscriptLinksForm | 11 | 4 | 7 | 0 | 0 | 4 | 3 | 0 | 0 | 4 | 0 |
| ManuscriptReadinessForm | 9 | 3 | 5 | 1 | 0 | 8 | 0 | 0 | 1 | 0 | 0 |
| ManuscriptVersionEditForm | 3 | 0 | 3 | 0 | 0 | 0 | 3 | 0 | 0 | 0 | 0 |
| ManuscriptVersionHistoryControl (Versions tab) | 7 | 0 | 6 | 1 | 0 | 6 | 0 | 0 | 1 | 0 | 0 |
| MarkPublishedForm | 2 | 1 | 1 | 0 | 0 | 1 | 0 | 0 | 0 | 1 | 0 |
| OrcidLookupForm | 3 | 0 | 3 | 0 | 0 | 2 | 1 | 0 | 0 | 0 | 0 |
| PublicationCheckForm | 1 | 0 | 1 | 0 | 0 | 0 | 1 | 0 | 0 | 0 | 0 |
| PublicationExportForm | 5 | 1 | 4 | 0 | 0 | 3 | 0 | 0 | 0 | 2 | 0 |
| ReminderEditForm | 2 | 0 | 2 | 0 | 0 | 0 | 2 | 0 | 0 | 0 | 0 |
| ReportForm | 1 | 0 | 1 | 0 | 0 | 1 | 0 | 0 | 0 | 0 | 0 |
| ReviewerResponseExportForm | 1 | 0 | 1 | 0 | 0 | 1 | 0 | 0 | 0 | 0 | 0 |
| ReviewerResponseItemForm | 2 | 1 | 1 | 0 | 0 | 1 | 1 | 0 | 0 | 0 | 0 |
| ReviewerResponseMatrixForm | 4 | 1 | 2 | 1 | 0 | 2 | 0 | 0 | 1 | 1 | 0 |
| SettingsForm | 2 | 0 | 2 | 0 | 0 | 1 | 1 | 0 | 0 | 0 | 0 |
| StartupFailureForm | 5 | 1 | 3 | 1 | 0 | 4 | 0 | 0 | 1 | 0 | 0 |
| SubmissionDetailsForm | 10 | 3 | 4 | 2 | 1 | 7 | 0 | 1 | 2 | 0 | 0 |
| SubmissionPacketEditForm | 3 | 0 | 3 | 0 | 0 | 1 | 2 | 0 | 0 | 0 | 0 |
| SubmissionPacketFileEditForm | 3 | 0 | 3 | 0 | 0 | 0 | 2 | 0 | 0 | 1 | 0 |
| SubmissionPacketVaultForm | 7 | 0 | 4 | 3 | 0 | 4 | 0 | 0 | 3 | 0 | 0 |
| UpdateService (service) | 1 | 1 | 0 | 0 | 0 | 1 | 0 | 0 | 0 | 0 | 0 |
| **Total** | **202** | **56** | **119** | **17** | **10** | **111** | **40** | **8** | **14** | **28** | **1** |

Rows add up across: Sites = Notice + Error + Confirmation + Question = the six target columns.

**Rules used to decide the kind.**

- **Notice:** reports something that happened, or why something is not available, and asks nothing.
- **Error:** a failed or refused action, or input that did not pass a check, whatever the icon (several use the Information icon).
- **Confirmation:** asks Yes or No before the app does something the user asked for.
- **Question:** asks a choice the app needs (Save / Don't Save / Cancel, copy or link, record now, keep working or close).

**Rules used to decide the target.**

- **Stays as a destructive confirmation** when the act removes a whole record with its children (submission, decision, version, packet, packet file, readiness profile, reusable author, affiliation or journal, reviewer comment, correspondence item), replaces a fingerprint, clears a recorded date, or replaces the library from a backup, even where Discard could restore it. This is a choice, not a Discard test: 9 of the 14 (`EditManuscriptForm.vb:3730`, `ManuscriptReadinessForm.vb:1036`, `ManuscriptVersionHistoryControl.vb:1181`, `ReviewerResponseMatrixForm.vb:430`, `SubmissionDetailsForm.vb:1460, 2384`, `SubmissionPacketVaultForm.vb:1198, 1382, 1555`) act on the manuscript page's working copy, so Discard can undo them, but only by throwing away every other unsaved edit on the page with them. The other 5 act outside a working copy: `Form1.Deadlines.vb:469`, `AuthorLibraryForm.vb:875, 1023`, `JournalLibraryForm.vb:569`, `StartupFailureForm.vb:801`.
- **Stays a question** when an answer is needed before the record can change or before unsaved work could be lost (leaving a manuscript with unsaved changes; closing after a failed save; keep working or close after an error; a stage that needs a submission or decision; copy or link dropped files). One more is kept for a fourth reason, proposed and awaiting sign-off (question 13): a safety warning before PaperRoute starts another program (`Services\FileOpenService.vb:91`, a program or script that Windows would run).
- **Inline validation** when the box checks one field: the message moves beside the field, and Save waits until it passes.
- **Not needed** when the redesign removes the cause: the action is shown disabled with its reason beside it (empty library, offline, nothing selected, nothing saved yet), the answer is the panel's own button (the spreadsheet import preview; the restore preview `Form1.vb:4227`, whose text becomes the content of the one Restore confirmation window, 5c), the window that raised it is removed (the modal matrix's Save & Close), or the line cannot run (`SubmissionPacketFileEditForm.vb:606`, after a `Return`).
- **In-window notice** for everything else: results, failures to open a link or file, failed saves and lookups. They show in the panel where the action ran, or in the window's notice strip; a failed save stays visible until the next save succeeds.

Two boxes move by judgement rather than by rule: the recovered-changes question (`Form1.RecoveryDraft.vb:113`) becomes a notice with Open and Discard, because the draft changes nothing until saved. Today the draft file is deleted after either answer (`Form1.RecoveryDraft.vb:137-141`); as a notice that can be ignored, the draft file is kept until Open or Discard is chosen; opening that manuscript by another route (Board, Library, Deadlines) offers the draft first; saving the manuscript without opening the draft keeps the draft and the notice, so nothing is lost or replaced silently (`USER_GUIDE.md:169`). The restart question after Preferences (`Form1.vb:4935`) becomes a notice with Restart now, because waiting loses nothing. The removal of a checklist requirement in Edit journal (`JournalEditForm.vb:973`) is "not needed" only if journal edits keep a Save step (question 4).

## 7. One visual language

Today each of the five families has two or more versions. The definitions below are proposed as Avalonia styles (selectors with classes) that read the `UiTheme` tokens; the token table follows. Appendix C gives the five definitions as `.axaml` text for the first build issue to copy into the app's `Styles` folder (issue #161, Acceptance item 3); it has not been compiled, since nothing is built for this deliverable.

### 7.1 Button

**Today.**

- `ActionButton` (`Controls\ActionButton.vb:24-294`): rounded (ControlRadius 8), padding 14,5, hover eased over 100 ms, a 2 px rounded focus ring. Roles Primary, Secondary, Quiet, Danger. No code outside the file uses Danger.
- The same face drawn by owner-drawn rows without a control (`Controls\DeadlineRow.vb:421-431`, `Controls\JournalShortlistList.vb:418-424`): Secondary and Quiet, no easing.
- `UiPolish.StyleButton` (`Services\UiPolish.vb:622-692`) on every plain dialog `Button` (about 241 in `Forms\`, not checked one by one): flat, square, no easing, the dotted Windows focus rectangle. Primary is whichever button is the form's AcceptButton. Captions containing DELETE or REMOVE turn red. Dark caption is a hard-coded 240,240,240.
- Rail items (`Controls\RailItems.vb:45-344`): radius 7, accent-muted fill when selected, glyph and label, count badge.

**How they differ.** Rounded or square; eased or instant; ring or dotted rectangle; Primary by role or by AcceptButton; Danger by role (unused) or by caption text; disabled drawn two ways.

**One definition.** One Button style with classes `secondary` (default), `primary`, `quiet`, `danger`, and `nav` (a toggle with icon, label and optional count, for the rail). CornerRadius ControlRadius (8); Padding 14,5; MinHeight 32; sizes regular, compact (row actions) and icon (square, for ⋯). States: pointer-over eases the background over `UiMotion.HoverMilliseconds` (100 ms) to HoverBackground, primary to Blend(AccentColor, shade, 0.1); pressed is immediate (primary 0.2, others HoverBackground); disabled is BoardBackground fill, CardBorder outline, MutedText (quiet stays transparent); focus-visible is a 2 px ring inset 2 px in AccentSecondaryColor (OnAccentText on primary); nav checked is AccentMutedBackground, AccentColor ink, semibold. Danger is a class set by the code, never read from the caption.
Tokens: AccentColor, OnAccentText, CardBackground, CardBorder, HoverBackground, PrimaryText, SecondaryText, MutedText, DangerColor, BoardBackground, AccentSecondaryColor, AccentMutedBackground, SuccessColor, InfoColor, InfoMutedBackground, WarningColor, WarningMutedBackground, ControlRadius, plus the high-contrast mapping.

### 7.2 Card

**Today.** `RoundedPanel` (`Controls\RoundedPanel.vb`, default radius 14, not DPI-scaled); the board card `CardPanel` (radius CardRadius 10, border eases to accent on hover); `SectionCard` (`Controls\SectionCard.vb`, radius 10, titled, padding 14); Insights stat tiles (`Form1.Insights.vb:418-445`, radius 8, padding 12,8); Deadlines glance tiles (`Controls\DeadlineGlance.vb:101-125`, radius 8, painted); Deadlines row cards (`Controls\DeadlineRow.vb:311-344`, radius 10, 3 px danger or warning stripe, 2 px accent border when focused); route waypoint cards (`ManuscriptRouteViewForm.vb:717-746`, a literal radius 12, padding 16,13); callouts (Welcome privacy note rounded, shortlist offer square, `EditManuscriptForm.vb:1793-1798`); the journal facts card (`Controls\JournalFactsCard.vb`, square, padding 18,16).

**How they differ.** Four radii (8, 10, 12, 14) and square; five paddings; selection or focus shown four ways (eased accent border, 2 px accent border, accent-muted fill with accent border, nothing); some are controls clipped to a region, some are painted by their list.

**One definition.** One Card (Border) style: CornerRadius CardRadius (10), Background CardBackground, BorderBrush CardBorder 1 px, Padding SpaceLg (16); `compact` Padding 12,8 for tiles. Classes: `titled` (semibold PrimaryText header, SpaceSm gap; MutedText when disabled); `interactive` (pointer-over border eases to AccentColor over 100 ms; focus-within AccentColor at once); `selected` (AccentMutedBackground, AccentColor 2 px border); `stripe-danger`, `stripe-warning` (3 px left edge); `callout` (no border; AccentMutedBackground, or the Info or Warning muted background by tone). Scrolling content sits inside the card; the frame does not scroll.
Tokens: CardBackground, CardBorder, SubtleBorder (inner dividers), AccentColor, AccentMutedBackground, DangerColor, WarningColor, InfoMutedBackground, WarningMutedBackground, PrimaryText, SecondaryText, MutedText, CardRadius, SpaceSm, SpaceMd, SpaceLg.

### 7.3 Pill

**Today.** `PillLabel` (`Controls\PillLabel.vb`) for stage badges on the board card (`Form1.vb:3095-3105`, insets 16 x 6) and the manuscript page header (`Form1.ManuscriptPage.vb:763-767`, upper case); the reviewer-response status pill painted in the matrix (`ReviewerResponseMatrixForm.vb:269-313`, insets 14 x 4, bold); the rail count badge (`Controls\RailItems.vb:98-128`, warning tones, a dot when collapsed); the Deadlines kind badge (`Controls\DeadlineRow.vb:365-379`, a 28 px rounded square with a glyph).

**How they differ.** Different insets, case and weight; a clipped label in one place, painted in the others; one is a square with an icon.

**One definition.** One Pill (Border and TextBlock), not interactive: CornerRadius full, Padding 7,2, small semibold text; sizes regular and `count` (minimum width equals height). Tone classes `stage-idea` to `stage-published` (StageBackground / StageForeground), `neutral` (HoverBackground / SecondaryText), `accent`, `success`, `warning`, `danger`, `info` (each muted background with its ink). An `icon` variant is a rounded square (radius 7) with a glyph in the tone ink. High contrast draws a 1 px outline in the ink.
Tokens: StageBackground, StageForeground, AccentMutedBackground, SuccessMutedBackground, WarningMutedBackground, DangerMutedBackground, InfoMutedBackground and their inks, HoverBackground, SecondaryText.

### 7.4 Chip

**Today.** `FilterChip` (`Controls\FilterChip.vb`): a focusable pill, padding 10,3, tones Neutral, Warning, Danger, Active, hover eased, focus ring in AccentSecondaryColor. Used for Needs Attention (`Form1.vb:52-55`), the Deadlines kind filter (`Form1.Deadlines.vb:170-238`), and as launch buttons on the Welcome panel (`Form1.Welcome.vb:123-140`). Tag chips (`Controls\TagChips.vb`): painted, padding 7,2, colours from the tag palette in `Services\WorkTypeService.vb:156-185`, not `UiTheme`; the editor's chips add a remove × and an AccentColor focus ring at a different width.

**How they differ.** Two palettes; two paddings; two focus rings; only filter chips ease on hover; the same class is a toggle on Board and Deadlines and a plain button on Welcome.

**One definition.** One Chip style (a toggle): CornerRadius full, Padding 10,3 (regular) or 7,2 (`compact`, tags). Rest: CardBackground, CardBorder outline, SecondaryText. Pointer-over eases to HoverBackground (toned chips 40 % toward CardBackground). Checked: AccentMutedBackground, AccentColor ink, semibold. Focus-visible: 2 px ring in AccentSecondaryColor. Disabled: MutedText. Tone classes `warning` and `danger` for counts that need attention. A `tag` variant reads its colour pair from the tag palette (to be moved beside `UiTheme`), with an optional remove button (× in DangerColor when hot) and a colour menu; the read-only tag strip uses the same template, not focusable, with "+N" overflow. Welcome's launch actions use the Button `quiet` or `secondary` class instead of a chip.
Tokens: CardBackground, CardBorder, SecondaryText, HoverBackground, AccentMutedBackground, AccentColor, AccentSecondaryColor, WarningMutedBackground, WarningColor, DangerMutedBackground, DangerColor, MutedText, PrimaryText (contrast ring).

### 7.5 Tab

**Today.** `UnderlineTabControl` (`Controls\UnderlineTabControl.vb`), a TabControl used in five dialogs (Authors & Affiliations, Edit journal, ORCID, reviewer comment, submission detail): regular SecondaryText, selected bold PrimaryText with a 3 px AccentColor underline, dotted focus rectangle. `ShelfTabButton` (`Controls\ShelfTabButton.vb`), radio buttons used on the Board shelves, Library views, Insights views and the manuscript page sections: always bold, optional MutedText count, BoardBackground behind it whatever the surface, with the strip's hairline drawn by each parent (`Form1.Shell.vb:1535-1540`, `Form1.Insights.vb:178-183`, `EditManuscriptForm.vb:1658-1663`).

**How they differ.** Same look, two implementations; unselected text regular in one and bold in the other; counts in one only; neither has a hover state; both use the dotted focus rectangle, unlike buttons and chips.

**One definition.** One TabStrip and TabItem style: transparent background; strip hairline CardBorder 1 px; TabItem Padding SpaceMd x 6, gap SpaceXl; SecondaryText regular, selected PrimaryText semibold (width reserved for semibold) with a 3 px AccentColor underline; optional count in MutedText; pointer-over ink PrimaryText; focus-visible 2 px AccentSecondaryColor ring at ControlRadius; disabled MutedText. The rail stays the `nav` button class (7.1).
Tokens: CardBorder, SecondaryText, PrimaryText, MutedText, AccentColor, AccentSecondaryColor, SpaceMd, SpaceXl, ControlRadius.

Inputs are not one of the five families, but today only the board search field is rounded (`Form1.vb:1170-1235`); every dialog input is square (`Services\UiPolish.vb:261-408`). One TextBox / ComboBox style with ControlRadius, CardBackground, CardBorder, an AccentColor border on focus and MutedText when disabled would match the families.

The click-help "?" popup (`Forms\ContextHelpControl.vb:9-14`, five places, 2.1) is not one of the five families either. It becomes one Avalonia `ToolTip` style (CardBackground, CardBorder, ControlRadius, Padding SpaceSm, PrimaryText) shown on hover and kept open on click as a `Flyout` with the same look.

### 7.6 Tokens the families read

| Token | Where | Light | Dark | Used for |
|---|---|---|---|---|
| SpaceXs / Sm / Md / Lg / Xl | `UiTheme.vb:14-18` | 4 / 8 / 12 / 16 / 24 | same | Gaps and padding |
| CardRadius | `UiTheme.vb:19` | 10 | 10 | Cards, rows |
| ControlRadius | `UiTheme.vb:20` | 8 | 8 | Buttons, tiles, inputs, focus rings |
| Px(logical, dpi) | `UiTheme.vb:23-30` | logical x dpi / 96 | same | DPI scaling |
| High-contrast mapping | `UiTheme.vb:38-45` | surfaces Window; text, borders, inks WindowText; muted GrayText; accent Highlight; secondary accent HotTrack | same | All families |
| SubtleBorder | `UiTheme.vb:96` | 231,238,240 | 50,56,65 | Inner dividers |
| MutedText | `UiTheme.vb:113` | 100,113,125 | 138,149,161 | Disabled ink, counts |
| OnAccentText | `UiTheme.vb:129` | White | 7,32,29 | Primary button text |
| WarningMutedBackground | `UiTheme.vb:144` | 254,243,199 | 92,63,21 | Warning chip and pill, rail badge |
| DangerMutedBackground | `UiTheme.vb:159` | 254,226,226 | 91,33,38 | Danger chip and pill |
| Blend(base, toward, amount) | `UiTheme.vb:176` | function | function | Hover 0.1, pressed 0.2 |
| BoardBackground | `UiTheme.vb:194` | 244,247,248 | 24,27,32 | Page surface; disabled button fill |
| HeaderBackground | `UiTheme.vb:209` | White | 31,35,41 | Rail and header bars |
| CardBackground | `UiTheme.vb:224` | White | 40,44,51 | Cards, buttons, chips, inputs |
| CardBorder | `UiTheme.vb:239` | 218,228,231 | 61,68,79 | Outlines, hairlines |
| HoverBackground | `UiTheme.vb:254` | 235,244,245 | 50,56,65 | Hover and pressed fill; neutral pill |
| PrimaryText | `UiTheme.vb:269` | SystemColors.ControlText (not in code) | same | Main text |
| SecondaryText | `UiTheme.vb:280` | 91,105,119 | 172,181,192 | Unselected tabs, neutral chips |
| AccentColor | `UiTheme.vb:306` | 15,118,110 | 45,212,191 | Primary fill, underline, selection |
| AccentSecondaryColor | `UiTheme.vb:323` | 14,116,144 | 34,211,238 | Focus rings |
| AccentMutedBackground | `UiTheme.vb:338` | 204,251,241 | 20,69,68 | Checked chip, selected rail item, callouts |
| SelectedRowBackground / Text | `UiTheme.vb:356, 369` | tint / PrimaryText | same | Grid selection |
| DangerColor | `UiTheme.vb:380` | 190,35,45 | 248,124,124 | Danger ink, overdue stripe |
| WarningColor | `UiTheme.vb:396` | 180,83,9 | 251,191,36 | Warning ink, due-today stripe |
| SuccessColor | `UiTheme.vb:411` | 21,121,74 | 110,231,183 | Online tone, Addressed pill |
| SuccessMutedBackground | `UiTheme.vb:429` | 220,247,232 | 21,59,43 | Addressed pill |
| InfoColor | `UiTheme.vb:446` | 29,78,216 | 147,197,253 | Working offline ink |
| InfoMutedBackground | `UiTheme.vb:461` | 219,234,254 | 30,58,100 | Working offline tint |
| StageBackground(stage) | `UiTheme.vb:476-548` | Idea 237,233,254; Draft 229,231,235; Submitted 219,234,254; UnderReview 204,251,241; Revision 254,243,199; Accepted 220,252,231; InPress 207,250,254; Published 209,250,229 | Idea 72,54,120; Draft 65,68,75; Submitted 30,64,110; UnderReview 17,94,89; Revision 92,63,21; Accepted 31,76,47; InPress 19,78,74; Published 17,78,60 | Stage pills |
| StageForeground(stage) | `UiTheme.vb:551-623` | Idea 91,33,182; Draft 55,65,81; Submitted 29,78,216; UnderReview 15,118,110; Revision 180,83,9; Accepted 21,128,61; InPress 14,116,144; Published 4,120,87 | Idea 221,214,254; Draft 229,231,235; Submitted 191,219,254; UnderReview 153,246,228; Revision 253,230,138; Accepted 187,247,208; InPress 165,243,252; Published 167,243,208 | Stage pill text |
| UiMotion.HoverMilliseconds | `UiMotion.vb:31-33` | 100 ms | 100 ms | Hover easing |
| Tag palette | `WorkTypeService.vb:156-185` | per colour, e.g. Blue 219,234,254 / 30,64,175 | e.g. Blue 30,58,138 / 191,219,254 | Tag chips |

## 8. Questions for the maintainer

1. Deadlines row actions, board card actions (Move to File Drawer, Restore to Pipeline, Delete), Mark Published, Check for Publications and Fill Blanks save the library at once, outside the manuscript page's Save. Keep them as immediate saves that say so (proposed), or route them through the manuscript's one save step? Note that 5b also proposes a "Move to File Drawer" action in the manuscript page header, which would set the location in the working copy and wait for Save, while the same action on a board card saves at once: one action with two save behaviours. Keep the header variant, or drop it and keep the action on the card and in the Overview's File Drawer card only?
2. Before Sending (the assistant's send consent): keep it a modal window, or make it the first step of the Stage panel with an explicit Send button (proposed)?
3. PaperRoute ran into a problem: keep it modal (proposed), or make it a window-level banner with Keep Working and Close?
4. Edit journal in the Journals detail panel: one Save per journal record (proposed), or save as you go like the rest of the Journals page? (If save as you go, removing a checklist requirement needs a destructive confirmation.)
5. Add Manuscript becomes the manuscript page in a "new manuscript" state that Save creates and Discard abandons. Agree? And the reusable records made from a manuscript (5f, "Library records created from a manuscript"): the new authors that Paste a Title Page creates for a new manuscript (today saved before the manuscript, `Form1.vb:3561-3590`), and people, affiliations and journals created from the manuscript page. Hold them in the working copy and write them at the manuscript's Save, so Discard drops them (proposed, a), or save them at once as a second, library-level save boundary (b)?
6. The Route as a section of the manuscript page: should it show the working copy with unsaved edits, or only the saved record (as today)?
7. Check for Publication from the manuscript page: run inside the working copy so its changes wait for Save (proposed), or keep refusing while there are unsaved changes?
8. Where should this document and the signed-off map live: `docs/` or `ManualCertification`?
9. Count Preferences and Help as everyday surfaces? (It changes "before" from 24 to 26; "after" stays 0.)
10. Two readers found possible defects that were not run: an ORCID works import from Manage Library on the manuscript page may write the unsaved working copy to disk (`EditManuscriptForm.vb:2793-2830`, `AuthorLibraryForm.vb:696-700`), and Crossref-added authors may be dropped when the author library is reloaded from disk (`EditManuscriptForm.vb:2560-2563, 2838-2839`). File them now as v1.0.x issues, apart from #161?
11. The manuscript editor's dialog mode (`EditManuscriptForm.vb:233`) and `Form1.OpenAssistantSettings` (`Form1.Online.vb:155`) have no callers in the app. Leave them out of the Avalonia rewrite?
12. The three stage-change questions on Save (`EditManuscriptForm.vb:3978, 4014, 4064`) stay questions. Show them as an in-page step under the stage field (proposed) or as a modal box? Either way, Save stops before writing and resumes from the new-submission or decision panel's "Record and Save" (5b, rows 85 and 86).
13. The program-file warning (`Services\FileOpenService.vb:91`: a program or script Windows would run) changes no record and loses no work, so none of the three reasons to stay modal covers it. Accept a fourth reason, "a safety warning before PaperRoute starts another program" (proposed), or make it an inline "Open anyway" confirmation in the notice strip (in-window notice 112, stays a question 7)?
14. Issue #161's Acceptance item 3 asks that the five families "exist as Avalonia styles that the build issues consume". Appendix C gives them as `.axaml` text, uncompiled. Is that enough for sign-off, or should the item be amended so the first build issue delivers the compiled style files?

## Appendix A: every message box

Target names as in section 6. Line numbers are the line of `MessageBox.Show(` in `messagebox.json`.

| # | File | Line | Kind | What it says | Target |
|---:|---|---:|---|---|---|
| 1 | `ApplicationEvents.vb` | 70 | error | PaperRoute has to close (fatal error, shown only when no main window exists) | stays: no window to host it |
| 2 | `Controls\JournalFactsCard.vb` | 327 | error | Could not open a journal link | in-window notice |
| 3 | `Form1.Bibliography.vb` | 52 | notice | No bibliography records found in the file | in-window notice |
| 4 | `Form1.Bibliography.vb` | 223 | notice | Bibliography import complete | in-window notice |
| 5 | `Form1.Bibliography.vb` | 233 | error | Could not import the bibliography file | in-window notice |
| 6 | `Form1.Bibliography.vb` | 321 | notice | BibTeX/RIS export complete | in-window notice |
| 7 | `Form1.Bibliography.vb` | 334 | error | Could not export the bibliography | in-window notice |
| 8 | `Form1.Citations.vb` | 341 | notice | Citations update is blocked (offline or turned off) | not needed |
| 9 | `Form1.Citations.vb` | 372 | error | Could not save your citations; figures unchanged | in-window notice |
| 10 | `Form1.Deadlines.vb` | 442 | error | Date change refused | in-window notice |
| 11 | `Form1.Deadlines.vb` | 469 | confirmation | Clear the follow-up date for this submission? | stays: destructive confirmation |
| 12 | `Form1.Deadlines.vb` | 487 | error | Marking the reminder done was refused | in-window notice |
| 13 | `Form1.Deadlines.vb` | 508 | notice | Add a manuscript before creating a reminder | not needed |
| 14 | `Form1.Deadlines.vb` | 567 | notice | No dated items to export | not needed |
| 15 | `Form1.Deadlines.vb` | 586 | error | Could not create the calendar file | in-window notice |
| 16 | `Form1.Example.vb` | 27 | error | Could not open the example library | in-window notice |
| 17 | `Form1.ManuscriptPage.vb` | 1200 | notice | Save or discard changes first, then check for a publication | not needed |
| 18 | `Form1.ManuscriptPage.vb` | 1271 | question | Save your changes to this manuscript? (Yes/No/Cancel) | stays a question |
| 19 | `Form1.ManuscriptPage.vb` | 1353 | question | PaperRoute ran into a problem; keep working? Choose No to close (Yes/No fallback when the error window cannot show) | stays a question |
| 20 | `Form1.ManuscriptPage.vb` | 1388 | question | Latest changes not saved; save again before closing? | stays a question |
| 21 | `Form1.RecoveryDraft.vb` | 113 | question | PaperRoute closed before changes were saved; open them? | in-window notice |
| 22 | `Form1.Reports.vb` | 25 | notice | Add a manuscript before making a pipeline report | not needed |
| 23 | `Form1.vb` | 1349 | notice | Data file unreadable; previous save opened | in-window notice |
| 24 | `Form1.vb` | 1375 | notice | Managed-version staging folder recovery warning | in-window notice |
| 25 | `Form1.vb` | 1481 | notice | Author library unreadable; previous save opened | in-window notice |
| 26 | `Form1.vb` | 1634 | error | PaperRoute could not save your data | in-window notice |
| 27 | `Form1.vb` | 3567 | error | Could not save new authors; manuscript not added | in-window notice |
| 28 | `Form1.vb` | 3894 | notice | Import template created | in-window notice |
| 29 | `Form1.vb` | 3907 | error | Could not create the Excel template | in-window notice |
| 30 | `Form1.vb` | 3937 | notice | No manuscripts to export | not needed |
| 31 | `Form1.vb` | 3984 | notice | Library exported to Excel | in-window notice |
| 32 | `Form1.vb` | 4004 | error | Could not export the library | in-window notice |
| 33 | `Form1.vb` | 4032 | notice | No manuscripts to back up | not needed |
| 34 | `Form1.vb` | 4073 | notice | Library backed up; what the backup contains | in-window notice |
| 35 | `Form1.vb` | 4095 | error | Could not create the backup | in-window notice |
| 36 | `Form1.vb` | 4201 | error | This backup could not be validated | in-window notice |
| 37 | `Form1.vb` | 4227 | confirmation | Restore preview: replace the current library? | not needed (merged into the Restore confirmation window, 5c) |
| 38 | `Form1.vb` | 4252 | error | Restore cancelled; typed text did not match RESTORE | inline validation |
| 39 | `Form1.vb` | 4398 | error | Could not restore the backup | in-window notice |
| 40 | `Form1.vb` | 4462 | notice | Restore complete; PaperRoute restarts | in-window notice |
| 41 | `Form1.vb` | 4595 | error | Could not read this workbook | in-window notice |
| 42 | `Form1.vb` | 4644 | notice | Nothing new to import; N duplicates | in-window notice |
| 43 | `Form1.vb` | 4751 | confirmation | Spreadsheet import preview: import these? | not needed |
| 44 | `Form1.vb` | 4774 | error | Pre-import backup failed; import cancelled | in-window notice |
| 45 | `Form1.vb` | 4843 | notice | Spreadsheet import complete | in-window notice |
| 46 | `Form1.vb` | 4935 | question | Restart now to apply the new appearance? | in-window notice |
| 47 | `Forms\AboutForm.vb` | 140 | notice | Version information copied | in-window notice |
| 48 | `Forms\AboutForm.vb` | 147 | error | Could not copy the version information | in-window notice |
| 49 | `Forms\AddCorrespondenceForm.vb` | 352 | error | Selected local file not found | inline validation |
| 50 | `Forms\AddCorrespondenceForm.vb` | 398 | error | Please enter a title | inline validation |
| 51 | `Forms\AddCorrespondenceForm.vb` | 473 | error | Source URL must be http(s); may be blank | inline validation |
| 52 | `Forms\AddManuscriptForm.vb` | 422 | error | Please enter a manuscript title | inline validation |
| 53 | `Forms\AddSubmissionForm.vb` | 628 | notice | Journal Library is empty; add journals on the Journals page | in-window notice |
| 54 | `Forms\AddSubmissionForm.vb` | 717 | error | Please enter the journal name | inline validation |
| 55 | `Forms\AddSubmissionForm.vb` | 743 | error | Publisher portal URL not valid; may be blank | inline validation |
| 56 | `Forms\AffiliationEditForm.vb` | 228 | error | Institution required | inline validation |
| 57 | `Forms\ApiKeyForm.vb` | 177 | error | Could not open the key web page (also raised in OpenAlexKeyForm) | in-window notice |
| 58 | `Forms\AuthorEditForm.vb` | 263 | error | Enter a name or display-name override | inline validation |
| 59 | `Forms\AuthorEditForm.vb` | 288 | error | Enter a valid ORCID iD | inline validation |
| 60 | `Forms\AuthorLibraryForm.vb` | 606 | notice | Select an author first (ORCID) | not needed |
| 61 | `Forms\AuthorLibraryForm.vb` | 765 | error | ORCID import incomplete; backup folder named | in-window notice |
| 62 | `Forms\AuthorLibraryForm.vb` | 775 | error | Could not save the selected ORCID changes | in-window notice |
| 63 | `Forms\AuthorLibraryForm.vb` | 825 | notice | ORCID import complete (counts, backup folder) | in-window notice |
| 64 | `Forms\AuthorLibraryForm.vb` | 858 | notice | Author is in use by N manuscripts; remove first | in-window notice |
| 65 | `Forms\AuthorLibraryForm.vb` | 875 | confirmation | Delete the reusable author? | stays: destructive confirmation |
| 66 | `Forms\AuthorLibraryForm.vb` | 1006 | notice | Affiliation is in use; remove assignments first | in-window notice |
| 67 | `Forms\AuthorLibraryForm.vb` | 1023 | confirmation | Delete the reusable affiliation? | stays: destructive confirmation |
| 68 | `Forms\AuthorLibraryForm.vb` | 1134 | error | Could not save the author library | in-window notice |
| 69 | `Forms\BibliographyExportSelectionForm.vb` | 204 | error | Select at least one manuscript to export | not needed |
| 70 | `Forms\BibliographyImportForm.vb` | 375 | error | Select at least one record to import | not needed |
| 71 | `Forms\CrossrefLookupForm.vb` | 502 | error | Enter a valid DOI | inline validation |
| 72 | `Forms\CrossrefLookupForm.vb` | 561 | error | Crossref lookup failed | in-window notice |
| 73 | `Forms\DiagnosticsForm.vb` | 301 | notice | Diagnostic report copied | in-window notice |
| 74 | `Forms\DiagnosticsForm.vb` | 311 | error | Could not copy the diagnostic report | in-window notice |
| 75 | `Forms\DiagnosticsForm.vb` | 351 | notice | Diagnostic report saved | in-window notice |
| 76 | `Forms\DiagnosticsForm.vb` | 361 | error | Could not save the diagnostic report | in-window notice |
| 77 | `Forms\DiagnosticsForm.vb` | 409 | error | Could not open the data folder (shared with the startup window) | in-window notice |
| 78 | `Forms\EditManuscriptForm.Readiness.vb` | 196 | notice | AI assistant unavailable, with the reason (cover letter) | in-window notice |
| 79 | `Forms\EditManuscriptForm.vb` | 2084 | notice | Find Journals blocked (offline or turned off) | not needed |
| 80 | `Forms\EditManuscriptForm.vb` | 2447 | notice | Revision deadline needs a recorded submission first | not needed |
| 81 | `Forms\EditManuscriptForm.vb` | 2884 | notice | Every library author is already on this manuscript | in-window notice |
| 82 | `Forms\EditManuscriptForm.vb` | 3640 | error | Edited submission conflicts with a packet that uses it | inline validation |
| 83 | `Forms\EditManuscriptForm.vb` | 3717 | notice | Submission linked to a packet; unlink before deleting | in-window notice |
| 84 | `Forms\EditManuscriptForm.vb` | 3730 | confirmation | Delete this submission with its decisions, correspondence and reviewer comments? | stays: destructive confirmation |
| 85 | `Forms\EditManuscriptForm.vb` | 3978 | question | This stage needs an active submission; record it now? | stays a question |
| 86 | `Forms\EditManuscriptForm.vb` | 4014 | question | This stage needs a decision; record the submission first? | stays a question |
| 87 | `Forms\EditManuscriptForm.vb` | 4064 | question | Open the latest submission to record the decision now? | stays a question |
| 88 | `Forms\EditManuscriptForm.vb` | 4119 | notice | Requirement not found; stage not changed | in-window notice |
| 89 | `Forms\EditManuscriptForm.vb` | 4186 | error | Please enter a manuscript title | inline validation |
| 90 | `Forms\EditManuscriptForm.Workflow.vb` | 65 | error | Submission not recorded for the packet | in-window notice |
| 91 | `Forms\ExcelMappingForm.vb` | 478 | error | Only one column can map to a field | inline validation |
| 92 | `Forms\ExcelMappingForm.vb` | 498 | error | Map exactly one column to Title | inline validation |
| 93 | `Forms\ExternalLinkEditForm.vb` | 200 | error | Enter a short label such as OSF Project | inline validation |
| 94 | `Forms\ExternalLinkEditForm.vb` | 225 | error | Link URL not valid | inline validation |
| 95 | `Forms\ExternalLinkEditForm.vb` | 242 | error | Enter a URL | inline validation |
| 96 | `Forms\HelpForm.vb` | 241 | error | Could not open the guide on GitHub | in-window notice |
| 97 | `Forms\HelpForm.vb` | 356 | notice | Could not find the text in the User Guide | inline validation |
| 98 | `Forms\HelpForm.vb` | 402 | error | Could not open a clicked link | in-window notice |
| 99 | `Forms\JournalCandidateForm.vb` | 178 | error | Could not open thinkchecksubmit.org or the license page | in-window notice |
| 100 | `Forms\JournalCandidateForm.vb` | 343 | error | Could not open a fact hint link | in-window notice |
| 101 | `Forms\JournalChecklistItemEditForm.vb` | 335 | error | Enter a short requirement title | inline validation |
| 102 | `Forms\JournalEditForm.vb` | 874 | notice | AI assistant unavailable (Read Author Instructions) | in-window notice |
| 103 | `Forms\JournalEditForm.vb` | 973 | confirmation | Remove this requirement from the journal template? | not needed |
| 104 | `Forms\JournalEditForm.vb` | 1077 | error | Enter a journal name | inline validation |
| 105 | `Forms\JournalEditForm.vb` | 1099 | error | ISSN not valid, or too many ISSNs | inline validation |
| 106 | `Forms\JournalEditForm.vb` | 1134 | error | A journal URL is not a valid http(s) address | inline validation |
| 107 | `Forms\JournalEditForm.vb` | 1266 | error | Could not open the DORA link | in-window notice |
| 108 | `Forms\JournalLibraryForm.vb` | 556 | notice | Journal is linked to a manuscript or submission; remove links before deleting | in-window notice |
| 109 | `Forms\JournalLibraryForm.vb` | 569 | confirmation | Delete the reusable journal? | stays: destructive confirmation |
| 110 | `Forms\JournalLibraryForm.vb` | 619 | notice | This journal has no homepage / portal URL saved | not needed |
| 111 | `Forms\JournalLibraryForm.vb` | 643 | error | Could not open the journal link | in-window notice |
| 112 | `Forms\JournalLibraryForm.vb` | 676 | notice | Fictional journals are not in any index | in-window notice |
| 113 | `Forms\JournalLibraryForm.vb` | 691 | notice | Journal facts blocked (offline or turned off) | not needed |
| 114 | `Forms\JournalLibraryForm.vb` | 770 | error | Could not save the journal library; change not kept | in-window notice |
| 115 | `Forms\JournalMetricForm.vb` | 262 | error | Could not open the "Where it's published" link | in-window notice |
| 116 | `Forms\JournalMetricForm.vb` | 276 | error | "Where you found it" is not a valid link | inline validation |
| 117 | `Forms\JournalPickerForm.vb` | 277 | notice | Select a journal first | not needed |
| 118 | `Forms\JournalSuggestionsForm.vb` | 700 | error | Could not open an example article | in-window notice |
| 119 | `Forms\ManuscriptAuthorForm.vb` | 441 | error | Add an author to the reusable library first | not needed |
| 120 | `Forms\ManuscriptLinksForm.vb` | 719 | notice | Link this manuscript to a reusable journal first | not needed |
| 121 | `Forms\ManuscriptLinksForm.vb` | 740 | notice | Journal has no homepage / portal URL saved | not needed |
| 122 | `Forms\ManuscriptLinksForm.vb` | 764 | error | Could not open the journal link | in-window notice |
| 123 | `Forms\ManuscriptLinksForm.vb` | 786 | notice | Save a manuscript-specific URL first | not needed |
| 124 | `Forms\ManuscriptLinksForm.vb` | 806 | error | Could not open the manuscript URL | in-window notice |
| 125 | `Forms\ManuscriptLinksForm.vb` | 848 | notice | Save a preprint URL or DOI first | not needed |
| 126 | `Forms\ManuscriptLinksForm.vb` | 868 | error | Could not open the preprint | in-window notice |
| 127 | `Forms\ManuscriptLinksForm.vb` | 1045 | error | Could not open the related link | in-window notice |
| 128 | `Forms\ManuscriptLinksForm.vb` | 1097 | error | Manuscript URL not valid | inline validation |
| 129 | `Forms\ManuscriptLinksForm.vb` | 1122 | error | Enter a valid preprint DOI or leave it blank | inline validation |
| 130 | `Forms\ManuscriptLinksForm.vb` | 1147 | error | Preprint URL not valid | inline validation |
| 131 | `Forms\ManuscriptReadinessForm.vb` | 881 | error | Journal Library is empty; add a journal first | in-window notice |
| 132 | `Forms\ManuscriptReadinessForm.vb` | 924 | notice | A profile for this journal already exists; it was selected | in-window notice |
| 133 | `Forms\ManuscriptReadinessForm.vb` | 953 | error | Snapshot no longer linked to a journal | in-window notice |
| 134 | `Forms\ManuscriptReadinessForm.vb` | 976 | error | Linked journal record no longer exists | in-window notice |
| 135 | `Forms\ManuscriptReadinessForm.vb` | 998 | notice | No new requirements found; nothing changed | in-window notice |
| 136 | `Forms\ManuscriptReadinessForm.vb` | 1008 | notice | N new requirements added from the template | in-window notice |
| 137 | `Forms\ManuscriptReadinessForm.vb` | 1036 | confirmation | Delete the readiness profile for this journal? | stays: destructive confirmation |
| 138 | `Forms\ManuscriptReadinessForm.vb` | 1064 | error | Readiness profile is in use; cannot delete | in-window notice |
| 139 | `Forms\ManuscriptReadinessForm.Workflow.vb` | 78 | error | Could not open the submission portal | in-window notice |
| 140 | `Forms\ManuscriptVersionEditForm.vb` | 1543 | error | Please enter a short label for this version | inline validation |
| 141 | `Forms\ManuscriptVersionEditForm.vb` | 1578 | error | Version file could not be found | inline validation |
| 142 | `Forms\ManuscriptVersionEditForm.vb` | 1602 | error | Choose copy into Library or link the original | inline validation |
| 143 | `Forms\ManuscriptVersionHistoryControl.vb` | 1015 | error | Could not add this manuscript version | in-window notice |
| 144 | `Forms\ManuscriptVersionHistoryControl.vb` | 1095 | error | Could not update this manuscript version | in-window notice |
| 145 | `Forms\ManuscriptVersionHistoryControl.vb` | 1181 | confirmation | Delete this manuscript version? | stays: destructive confirmation |
| 146 | `Forms\ManuscriptVersionHistoryControl.vb` | 1205 | error | Could not delete this manuscript version | in-window notice |
| 147 | `Forms\ManuscriptVersionHistoryControl.vb` | 1254 | error | Current version not changed | in-window notice |
| 148 | `Forms\ManuscriptVersionHistoryControl.vb` | 1288 | error | Version file not found; record still preserved | in-window notice |
| 149 | `Forms\ManuscriptVersionHistoryControl.vb` | 1315 | error | Could not open this file | in-window notice |
| 150 | `Forms\MarkPublishedForm.vb` | 167 | notice | This match has no DOI or web address to open (Review Match) | not needed |
| 151 | `Forms\MarkPublishedForm.vb` | 173 | error | Could not open the web page (Review Match) | in-window notice |
| 152 | `Forms\OrcidLookupForm.vb` | 385 | error | Enter a valid ORCID iD | inline validation |
| 153 | `Forms\OrcidLookupForm.vb` | 437 | error | ORCID lookup failed | in-window notice |
| 154 | `Forms\OrcidLookupForm.vb` | 683 | error | Could not open the ORCID profile | in-window notice |
| 155 | `Forms\PublicationCheckForm.vb` | 304 | error | The ORCID iD typed is not valid | inline validation |
| 156 | `Forms\PublicationExportForm.vb` | 620 | error | Nothing to copy; select a manuscript first | not needed |
| 157 | `Forms\PublicationExportForm.vb` | 640 | error | Could not copy to the clipboard | in-window notice |
| 158 | `Forms\PublicationExportForm.vb` | 668 | error | Nothing to export; select a manuscript first | not needed |
| 159 | `Forms\PublicationExportForm.vb` | 749 | notice | Publication export saved | in-window notice |
| 160 | `Forms\PublicationExportForm.vb` | 762 | error | Could not save the publication export | in-window notice |
| 161 | `Forms\ReminderEditForm.vb` | 391 | error | Select a manuscript for this reminder | inline validation |
| 162 | `Forms\ReminderEditForm.vb` | 407 | error | Enter a short reminder description | inline validation |
| 163 | `Forms\ReportForm.vb` | 204 | error | Could not save the report | in-window notice |
| 164 | `Forms\ReviewerResponseExportForm.vb` | 69 | error | Markdown file could not be saved | in-window notice |
| 165 | `Forms\ReviewerResponseItemForm.vb` | 222 | notice | Assistant unavailable, or "Write the comment first" | in-window notice |
| 166 | `Forms\ReviewerResponseItemForm.vb` | 334 | error | Check Reviewer Comment (validation) | inline validation |
| 167 | `Forms\ReviewerResponseMatrixForm.vb` | 394 | notice | Assistant cannot be used now (Add from Letter) | in-window notice |
| 168 | `Forms\ReviewerResponseMatrixForm.vb` | 407 | error | Comments from the letter cannot be added | in-window notice |
| 169 | `Forms\ReviewerResponseMatrixForm.vb` | 430 | confirmation | Remove this reviewer comment and its response draft? | stays: destructive confirmation |
| 170 | `Forms\ReviewerResponseMatrixForm.vb` | 449 | error | Check Reviewer Responses (modal matrix Save & Close only) | not needed |
| 171 | `Forms\SettingsForm.vb` | 1568 | error | Could not open Anthropic's key page | in-window notice |
| 172 | `Forms\SettingsForm.vb` | 1626 | error | AI assistant field checks; also preferences or key not saved | inline validation |
| 173 | `Forms\StartupFailureForm.vb` | 532 | notice | Details copied | in-window notice |
| 174 | `Forms\StartupFailureForm.vb` | 542 | error | Could not copy the details | in-window notice |
| 175 | `Forms\StartupFailureForm.vb` | 801 | confirmation | Restore this backup? Replaces the library files and Library folder | stays: destructive confirmation |
| 176 | `Forms\StartupFailureForm.vb` | 858 | error | Previous save or backup could not be used, or Library Recovered | in-window notice |
| 177 | `Forms\StartupFailureForm.vb` | 882 | error | Could not open the User Guide link | in-window notice |
| 178 | `Forms\SubmissionDetailsForm.vb` | 1317 | notice | Assistant cannot be used now (Read Decision Letter) | in-window notice |
| 179 | `Forms\SubmissionDetailsForm.vb` | 1363 | error | Comments from the letter cannot be added | in-window notice |
| 180 | `Forms\SubmissionDetailsForm.vb` | 1454 | error | Decision has reviewer responses; cannot delete | in-window notice |
| 181 | `Forms\SubmissionDetailsForm.vb` | 1460 | confirmation | Delete this editorial decision? May change the rejection count | stays: destructive confirmation |
| 182 | `Forms\SubmissionDetailsForm.vb` | 2173 | question | Dropped files: copy into Library (Yes), link (No), or cancel | stays a question |
| 183 | `Forms\SubmissionDetailsForm.vb` | 2255 | notice | No new files added | in-window notice |
| 184 | `Forms\SubmissionDetailsForm.vb` | 2265 | notice | N files added, M skipped | in-window notice |
| 185 | `Forms\SubmissionDetailsForm.vb` | 2384 | confirmation | Remove this correspondence item? The file is not deleted | stays: destructive confirmation |
| 186 | `Forms\SubmissionDetailsForm.vb` | 2424 | error | Linked file not found; use Edit to repoint | in-window notice |
| 187 | `Forms\SubmissionDetailsForm.vb` | 2488 | error | File or source URL could not be opened | in-window notice |
| 188 | `Forms\SubmissionPacketEditForm.vb` | 652 | error | Create a Version History record before a packet | inline validation |
| 189 | `Forms\SubmissionPacketEditForm.vb` | 718 | error | Check Submission Packet (invalid value) | inline validation |
| 190 | `Forms\SubmissionPacketEditForm.vb` | 730 | error | Check Submission Packet (conflicting links) | in-window notice |
| 191 | `Forms\SubmissionPacketFileEditForm.vb` | 584 | error | Check Packet File (invalid value) | inline validation |
| 192 | `Forms\SubmissionPacketFileEditForm.vb` | 596 | error | Check Packet File (file not found) | inline validation |
| 193 | `Forms\SubmissionPacketFileEditForm.vb` | 606 | error | Unreachable duplicate of line 596 (after a Return) | not needed |
| 194 | `Forms\SubmissionPacketVaultForm.vb` | 1050 | error | Create a Version History record before a packet | in-window notice |
| 195 | `Forms\SubmissionPacketVaultForm.vb` | 1198 | confirmation | Delete this packet? Managed files move to Removed on save | stays: destructive confirmation |
| 196 | `Forms\SubmissionPacketVaultForm.vb` | 1382 | confirmation | Remove this file from the packet? | stays: destructive confirmation |
| 197 | `Forms\SubmissionPacketVaultForm.vb` | 1433 | error | File not found at the recorded path | in-window notice |
| 198 | `Forms\SubmissionPacketVaultForm.vb` | 1457 | error | Could not open the file | in-window notice |
| 199 | `Forms\SubmissionPacketVaultForm.vb` | 1555 | confirmation | Replace the saved fingerprint with the current contents? | stays: destructive confirmation |
| 200 | `Forms\SubmissionPacketVaultForm.vb` | 1639 | error | Could not finish the integrity operation | in-window notice |
| 201 | `Services\FileOpenService.vb` | 91 | question | This file is a program or script; open it anyway? | stays a question |
| 202 | `Services\UpdateService.vb` | 387 | notice | Update messages: blocked, developer build, up to date, or update error | in-window notice |

## Appendix B: every ShowDialog call

109 call sites from `showdialog.json`: 89 open PaperRoute windows and 20 open system file pickers. Not listed because they are not `ShowDialog` calls: the Restore `InputBox` (`Form1.vb:4240`) and `UpdateProgressForm` shown with `Show(owner)` (`UpdateService.vb:223`).

| # | File | Line | What it opens |
|---:|---|---:|---|
| 1 | `Form1.Bibliography.vb` | 25 | OpenFileDialog: choose a BibTeX or RIS file |
| 2 | `Form1.Bibliography.vb` | 69 | BibliographyImportForm (Import BibTeX / RIS review) |
| 3 | `Form1.Bibliography.vb` | 274 | BibliographyExportSelectionForm (choose manuscripts to export) |
| 4 | `Form1.Bibliography.vb` | 295 | SaveFileDialog: bibliography export |
| 5 | `Form1.Citations.vb` | 363 | CitationsUpdateForm (Update from OpenAlex) |
| 6 | `Form1.Deadlines.vb` | 424 | DeadlineDateForm (Postpone / Set Deadline) |
| 7 | `Form1.Deadlines.vb` | 518 | ReminderEditForm (Add Reminder) |
| 8 | `Form1.Deadlines.vb` | 547 | ReminderEditForm (Edit Reminder) |
| 9 | `Form1.Deadlines.vb` | 579 | SaveFileDialog: calendar file |
| 10 | `Form1.JournalsExports.vb` | 19 | PublicationExportForm (Publication & CV Export) |
| 11 | `Form1.ManuscriptPage.vb` | 1325 | UnexpectedErrorForm |
| 12 | `Form1.Publications.vb` | 41 | PublicationCheckForm (Check for Publications) |
| 13 | `Form1.Publications.vb` | 53 | FillBlanksForm (Fill Blanks from Crossref) |
| 14 | `Form1.Publications.vb` | 89 | MarkPublishedForm (from a Deadlines row) |
| 15 | `Form1.RemindersHelp.vb` | 20 | HelpForm (rail Help, F1) |
| 16 | `Form1.Reports.vb` | 37 | ReportForm (Pipeline Report) |
| 17 | `Form1.Reports.vb` | 53 | ReportForm (Route Report) |
| 18 | `Form1.vb` | 145 | AboutForm |
| 19 | `Form1.vb` | 3557 | AddManuscriptForm |
| 20 | `Form1.vb` | 3633 | ManuscriptRouteViewForm (the Route) |
| 21 | `Form1.vb` | 3666 | DeleteManuscriptForm (from a board card) |
| 22 | `Form1.vb` | 3711 | MoveToFileDrawerForm |
| 23 | `Form1.vb` | 3882 | SaveFileDialog: blank import template |
| 24 | `Form1.vb` | 3971 | SaveFileDialog: library Excel export |
| 25 | `Form1.vb` | 4137 | SaveFileDialog: library backup |
| 26 | `Form1.vb` | 4184 | OpenFileDialog: backup to restore |
| 27 | `Form1.vb` | 4505 | OpenFileDialog: spreadsheet to import |
| 28 | `Form1.vb` | 4571 | ExcelMappingForm (Map Spreadsheet Columns) |
| 29 | `Form1.vb` | 4922 | SettingsForm (Preferences) |
| 30 | `Form1.vb` | 4983 | DiagnosticsForm |
| 31 | `Form1.Welcome.vb` | 232 | HelpForm at Quick Start |
| 32 | `Forms\AddCorrespondenceForm.vb` | 318 | OpenFileDialog: correspondence file (Browse) |
| 33 | `Forms\AddManuscriptForm.vb` | 361 | TitlePageImportForm (Paste a Title Page) |
| 34 | `Forms\AddSubmissionForm.vb` | 644 | JournalPickerForm (Use Library) |
| 35 | `Forms\AssistantConsentForm.vb` | 194 | AssistantConsentForm (Before Sending), shared by every assistant feature |
| 36 | `Forms\AuthorInstructionsForm.vb` | 86 | AuthorInstructionsForm (Read Author Instructions), via RunDialog |
| 37 | `Forms\AuthorLibraryForm.vb` | 497 | AuthorEditForm (Add Author) |
| 38 | `Forms\AuthorLibraryForm.vb` | 550 | AuthorEditForm (Edit author) |
| 39 | `Forms\AuthorLibraryForm.vb` | 622 | OrcidLookupForm (ORCID) |
| 40 | `Forms\AuthorLibraryForm.vb` | 912 | AffiliationEditForm (Add Affiliation) |
| 41 | `Forms\AuthorLibraryForm.vb` | 953 | AffiliationEditForm (Edit affiliation) |
| 42 | `Forms\CoverLetterForm.vb` | 505 | SaveFileDialog: cover letter |
| 43 | `Forms\DecisionLetterForm.vb` | 98 | RunDialog helper: opens DecisionLetterForm, and AddDecisionForm after it (SubmissionDetailsForm.vb:1325, 1333; ReviewerResponseMatrixForm.vb:400) |
| 44 | `Forms\DiagnosticsForm.vb` | 339 | SaveFileDialog: diagnostic report |
| 45 | `Forms\EditManuscriptForm.Readiness.vb` | 214 | CoverLetterForm (Draft Cover Letter) |
| 46 | `Forms\EditManuscriptForm.vb` | 1836 | HelpForm at Choosing a Journal |
| 47 | `Forms\EditManuscriptForm.vb` | 2020 | JournalCandidateForm (shortlist Add Journal / Edit) |
| 48 | `Forms\EditManuscriptForm.vb` | 2116 | JournalSuggestionsForm (Find Journals) |
| 49 | `Forms\EditManuscriptForm.vb` | 2471 | AddDecisionForm (Overview revision deadline Set) |
| 50 | `Forms\EditManuscriptForm.vb` | 2497 | AddDecisionForm (Overview revision deadline Edit) |
| 51 | `Forms\EditManuscriptForm.vb` | 2553 | CrossrefLookupForm (DOI & Crossref Metadata) |
| 52 | `Forms\EditManuscriptForm.vb` | 2832 | AuthorLibraryForm as a modal window (Manage Library) |
| 53 | `Forms\EditManuscriptForm.vb` | 2902 | ManuscriptAuthorForm (Add Author) |
| 54 | `Forms\EditManuscriptForm.vb` | 2963 | ManuscriptAuthorForm (Edit author) |
| 55 | `Forms\EditManuscriptForm.vb` | 3547 | AddSubmissionForm (Record Submission) |
| 56 | `Forms\EditManuscriptForm.vb` | 3623 | AddSubmissionForm (Edit submission) |
| 57 | `Forms\EditManuscriptForm.vb` | 3844 | DeleteManuscriptForm (from the manuscript page) |
| 58 | `Forms\EditManuscriptForm.vb` | 3887 | ManuscriptLinksForm (Journal, Preprint & Links) |
| 59 | `Forms\EditManuscriptForm.vb` | 3921 | AddSubmissionForm (stage change needs a submission) |
| 60 | `Forms\EditManuscriptForm.vb` | 4088 | SubmissionDetailsForm as a modal window (stage change needs a decision) |
| 61 | `Forms\EditManuscriptForm.Workflow.vb` | 22 | ManuscriptReadinessForm (Submission Readiness) |
| 62 | `Forms\EditManuscriptForm.Workflow.vb` | 26 | SubmissionPacketVaultForm (Submission Packets) |
| 63 | `Forms\EditManuscriptForm.Workflow.vb` | 60 | AddSubmissionForm (Record Submission from a packet) |
| 64 | `Forms\JournalCandidateForm.vb` | 370 | HelpForm at Choosing a Journal |
| 65 | `Forms\JournalEditForm.vb` | 834 | JournalChecklistItemEditForm (Add Requirement) |
| 66 | `Forms\JournalEditForm.vb` | 939 | JournalChecklistItemEditForm (Edit requirement) |
| 67 | `Forms\JournalEditForm.vb` | 1328 | JournalMetricForm (Add / Edit Metric) |
| 68 | `Forms\JournalLibraryForm.vb` | 443 | JournalEditForm (Add Journal) |
| 69 | `Forms\JournalLibraryForm.vb` | 483 | JournalEditForm (Edit journal) |
| 70 | `Forms\JournalLibraryForm.vb` | 710 | JournalFactsForm (Look Up Facts) |
| 71 | `Forms\ManuscriptLinksForm.vb` | 645 | JournalPickerForm (Choose from Library) |
| 72 | `Forms\ManuscriptLinksForm.vb` | 675 | JournalLibraryForm as a modal window (Manage Journals) |
| 73 | `Forms\ManuscriptLinksForm.vb` | 916 | ExternalLinkEditForm (Add Link) |
| 74 | `Forms\ManuscriptLinksForm.vb` | 957 | ExternalLinkEditForm (Edit link) |
| 75 | `Forms\ManuscriptReadinessForm.vb` | 897 | JournalPickerForm (New from Journal) |
| 76 | `Forms\ManuscriptReadinessForm.vb` | 1118 | ReadinessItemNotesForm (Notes) |
| 77 | `Forms\ManuscriptVersionEditForm.vb` | 1470 | OpenFileDialog: version file (Browse) |
| 78 | `Forms\ManuscriptVersionHistoryControl.vb` | 978 | ManuscriptVersionEditForm (Add Version) |
| 79 | `Forms\ManuscriptVersionHistoryControl.vb` | 1050 | ManuscriptVersionEditForm (Edit Details) |
| 80 | `Forms\PacketExportForm.vb` | 838 | SaveFileDialog: packet .zip |
| 81 | `Forms\PublicationCheckForm.vb` | 541 | MarkPublishedForm (from a publication check result) |
| 82 | `Forms\PublicationExportForm.vb` | 735 | SaveFileDialog: publication export |
| 83 | `Forms\ReportForm.vb` | 195 | SaveFileDialog: report |
| 84 | `Forms\ReviewerResponseExportForm.vb` | 65 | SaveFileDialog: reviewer-response Markdown |
| 85 | `Forms\ReviewerResponseItemForm.vb` | 247 | AssistantDraftForm (Suggest a Starting Point) |
| 86 | `Forms\ReviewerResponseMatrixForm.vb` | 379 | ReviewerResponseItemForm (Add Comment) |
| 87 | `Forms\ReviewerResponseMatrixForm.vb` | 420 | ReviewerResponseItemForm (Edit comment) |
| 88 | `Forms\ReviewerResponseMatrixForm.vb` | 463 | ReviewerResponseExportForm (Export Markdown) |
| 89 | `Forms\SettingsForm.vb` | 899 | HelpForm at What PaperRoute sends, and when |
| 90 | `Forms\SettingsForm.vb` | 1155 | HelpForm at AI Assistant |
| 91 | `Forms\SettingsForm.vb` | 1542 | OpenAlexKeyForm (Add / Replace Key) |
| 92 | `Forms\SettingsForm.vb` | 1549 | ApiKeyForm for the Claude or server key |
| 93 | `Forms\StartupFailureForm.vb` | 236 | StartupFailureForm before any window exists (no owner) |
| 94 | `Forms\StartupFailureForm.vb` | 240 | StartupFailureForm over the main window |
| 95 | `Forms\StartupFailureForm.vb` | 821 | OpenFileDialog: backup to restore from |
| 96 | `Forms\SubmissionDetailsForm.vb` | 500 | ReviewerResponseMatrixForm as a modal window (only from the modal Submission Details) |
| 97 | `Forms\SubmissionDetailsForm.vb` | 1274 | AddDecisionForm (+ Add Decision) |
| 98 | `Forms\SubmissionDetailsForm.vb` | 1395 | AddDecisionForm (Edit Decision) |
| 99 | `Forms\SubmissionDetailsForm.vb` | 2077 | OpenFileDialog: Link Files |
| 100 | `Forms\SubmissionDetailsForm.vb` | 2109 | OpenFileDialog: Copy to Library |
| 101 | `Forms\SubmissionDetailsForm.vb` | 2309 | AddCorrespondenceForm (+ Add Item) |
| 102 | `Forms\SubmissionDetailsForm.vb` | 2338 | AddCorrespondenceForm (Edit item) |
| 103 | `Forms\SubmissionPacketFileEditForm.vb` | 535 | OpenFileDialog: packet file (Browse) |
| 104 | `Forms\SubmissionPacketVaultForm.Export.vb` | 37 | PacketExportForm (Export Packet) |
| 105 | `Forms\SubmissionPacketVaultForm.vb` | 1082 | SubmissionPacketEditForm (New Packet) |
| 106 | `Forms\SubmissionPacketVaultForm.vb` | 1131 | SubmissionPacketEditForm (Edit packet) |
| 107 | `Forms\SubmissionPacketVaultForm.vb` | 1259 | SubmissionPacketFileEditForm (Add File) |
| 108 | `Forms\SubmissionPacketVaultForm.vb` | 1317 | SubmissionPacketFileEditForm (Edit file) |
| 109 | `Services\UpdateService.vb` | 376 | UpdatePromptForm (update available) |

## Appendix C: the five families as Avalonia styles

Draft `.axaml` for the first build issue (question 14). It follows section 7 and reads the token values of 7.6; it has not been compiled. Template part names (`PART_ContentPresenter`) are those of Avalonia's Fluent theme. Three things are left to code or to the build issue and are marked in comments: the primary hover and pressed shades (today computed by `UiTheme.Blend`, so set from code at start), the dark-mode `PrimaryText` (today `SystemColors.ControlText`, not in the code), and the high-contrast mapping (`UiTheme.vb:38-45`), which Avalonia exposes as its own theme variant.

### C.1 Tokens (`Styles/Tokens.axaml`)

```xml
<ResourceDictionary xmlns="https://github.com/avaloniaui"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <CornerRadius x:Key="CardRadius">10</CornerRadius>
  <CornerRadius x:Key="ControlRadius">8</CornerRadius>
  <CornerRadius x:Key="FullRadius">999</CornerRadius>
  <Thickness x:Key="SpaceSmPad">8</Thickness>
  <Thickness x:Key="SpaceLgPad">16</Thickness>
  <ResourceDictionary.ThemeDictionaries>
    <ResourceDictionary x:Key="Light">
      <SolidColorBrush x:Key="BoardBackgroundBrush" Color="#F4F7F8"/>
      <SolidColorBrush x:Key="CardBackgroundBrush" Color="#FFFFFF"/>
      <SolidColorBrush x:Key="CardBorderBrush" Color="#DAE4E7"/>
      <SolidColorBrush x:Key="HoverBackgroundBrush" Color="#EBF4F5"/>
      <SolidColorBrush x:Key="PrimaryTextBrush" Color="#000000"/>   <!-- SystemColors.ControlText today -->
      <SolidColorBrush x:Key="SecondaryTextBrush" Color="#5B6977"/>
      <SolidColorBrush x:Key="MutedTextBrush" Color="#64717D"/>
      <SolidColorBrush x:Key="AccentBrush" Color="#0F766E"/>
      <SolidColorBrush x:Key="OnAccentTextBrush" Color="#FFFFFF"/>
      <SolidColorBrush x:Key="AccentSecondaryBrush" Color="#0E7490"/>
      <SolidColorBrush x:Key="AccentMutedBackgroundBrush" Color="#CCFBF1"/>
      <SolidColorBrush x:Key="DangerBrush" Color="#BE232D"/>
      <SolidColorBrush x:Key="DangerMutedBackgroundBrush" Color="#FEE2E2"/>
      <SolidColorBrush x:Key="WarningBrush" Color="#B45309"/>
      <SolidColorBrush x:Key="WarningMutedBackgroundBrush" Color="#FEF3C7"/>
      <SolidColorBrush x:Key="SuccessBrush" Color="#15794A"/>
      <SolidColorBrush x:Key="SuccessMutedBackgroundBrush" Color="#DCF7E8"/>
      <SolidColorBrush x:Key="InfoBrush" Color="#1D4ED8"/>
      <SolidColorBrush x:Key="InfoMutedBackgroundBrush" Color="#DBEAFE"/>
      <SolidColorBrush x:Key="StageIdeaBackgroundBrush" Color="#EDE9FE"/>
      <SolidColorBrush x:Key="StageIdeaForegroundBrush" Color="#5B21B6"/>
      <!-- AccentHoverBrush, AccentPressedBrush: UiTheme.Blend(AccentColor, shade, 0.1 / 0.2), set from code -->
    </ResourceDictionary>
    <ResourceDictionary x:Key="Dark">
      <SolidColorBrush x:Key="BoardBackgroundBrush" Color="#181B20"/>
      <SolidColorBrush x:Key="CardBackgroundBrush" Color="#282C33"/>
      <SolidColorBrush x:Key="CardBorderBrush" Color="#3D444F"/>
      <SolidColorBrush x:Key="HoverBackgroundBrush" Color="#323841"/>
      <SolidColorBrush x:Key="PrimaryTextBrush" Color="#F0F0F0"/>   <!-- open: today's dark caption 240,240,240 -->
      <SolidColorBrush x:Key="SecondaryTextBrush" Color="#ACB5C0"/>
      <SolidColorBrush x:Key="MutedTextBrush" Color="#8A95A1"/>
      <SolidColorBrush x:Key="AccentBrush" Color="#2DD4BF"/>
      <SolidColorBrush x:Key="OnAccentTextBrush" Color="#07201D"/>
      <SolidColorBrush x:Key="AccentSecondaryBrush" Color="#22D3EE"/>
      <SolidColorBrush x:Key="AccentMutedBackgroundBrush" Color="#144544"/>
      <SolidColorBrush x:Key="DangerBrush" Color="#F87C7C"/>
      <SolidColorBrush x:Key="DangerMutedBackgroundBrush" Color="#5B2126"/>
      <SolidColorBrush x:Key="WarningBrush" Color="#FBBF24"/>
      <SolidColorBrush x:Key="WarningMutedBackgroundBrush" Color="#5C3F15"/>
      <SolidColorBrush x:Key="SuccessBrush" Color="#6EE7B7"/>
      <SolidColorBrush x:Key="SuccessMutedBackgroundBrush" Color="#153B2B"/>
      <SolidColorBrush x:Key="InfoBrush" Color="#93C5FD"/>
      <SolidColorBrush x:Key="InfoMutedBackgroundBrush" Color="#1E3A64"/>
      <SolidColorBrush x:Key="StageIdeaBackgroundBrush" Color="#483678"/>
      <SolidColorBrush x:Key="StageIdeaForegroundBrush" Color="#DDD6FE"/>
    </ResourceDictionary>
  </ResourceDictionary.ThemeDictionaries>
  <!-- The other seven stages repeat the Idea pair with the StageBackground / StageForeground values of 7.6. -->
</ResourceDictionary>
```

### C.2 Button (`Styles/Button.axaml`)

```xml
<Styles xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <Style Selector="Button, ToggleButton.nav">
    <Setter Property="CornerRadius" Value="{DynamicResource ControlRadius}"/>
    <Setter Property="Padding" Value="14,5"/>
    <Setter Property="MinHeight" Value="32"/>
    <Setter Property="Background" Value="{DynamicResource CardBackgroundBrush}"/>
    <Setter Property="BorderBrush" Value="{DynamicResource CardBorderBrush}"/>
    <Setter Property="BorderThickness" Value="1"/>
    <Setter Property="Foreground" Value="{DynamicResource PrimaryTextBrush}"/>
    <Setter Property="FocusAdorner">
      <FocusAdornerTemplate>
        <Border Margin="2" BorderThickness="2" CornerRadius="{DynamicResource ControlRadius}"
                BorderBrush="{DynamicResource AccentSecondaryBrush}"/>
      </FocusAdornerTemplate>
    </Setter>
  </Style>
  <Style Selector="Button /template/ ContentPresenter#PART_ContentPresenter">
    <Setter Property="Transitions">
      <Transitions><BrushTransition Property="Background" Duration="0:0:0.1"/></Transitions>
    </Setter>
  </Style>
  <Style Selector="Button:pointerover /template/ ContentPresenter#PART_ContentPresenter">
    <Setter Property="Background" Value="{DynamicResource HoverBackgroundBrush}"/>
  </Style>
  <!-- Pressed snaps: no transition while pressed. -->
  <Style Selector="Button:pressed /template/ ContentPresenter#PART_ContentPresenter">
    <Setter Property="Transitions" Value="{x:Null}"/>
    <Setter Property="Background" Value="{DynamicResource HoverBackgroundBrush}"/>
  </Style>
  <Style Selector="Button:disabled /template/ ContentPresenter#PART_ContentPresenter">
    <Setter Property="Background" Value="{DynamicResource BoardBackgroundBrush}"/>
    <Setter Property="BorderBrush" Value="{DynamicResource CardBorderBrush}"/>
    <Setter Property="Foreground" Value="{DynamicResource MutedTextBrush}"/>
  </Style>

  <Style Selector="Button.primary">
    <Setter Property="Background" Value="{DynamicResource AccentBrush}"/>
    <Setter Property="BorderBrush" Value="{DynamicResource AccentBrush}"/>
    <Setter Property="Foreground" Value="{DynamicResource OnAccentTextBrush}"/>
  </Style>
  <Style Selector="Button.primary:pointerover /template/ ContentPresenter#PART_ContentPresenter">
    <Setter Property="Background" Value="{DynamicResource AccentHoverBrush}"/>
  </Style>
  <Style Selector="Button.primary:pressed /template/ ContentPresenter#PART_ContentPresenter">
    <Setter Property="Background" Value="{DynamicResource AccentPressedBrush}"/>
  </Style>
  <Style Selector="Button.quiet">
    <Setter Property="Background" Value="Transparent"/>
    <Setter Property="BorderBrush" Value="Transparent"/>
  </Style>
  <Style Selector="Button.quiet:disabled /template/ ContentPresenter#PART_ContentPresenter">
    <Setter Property="Background" Value="Transparent"/>
    <Setter Property="BorderBrush" Value="Transparent"/>
  </Style>
  <!-- Danger is set by code, never read from the caption. -->
  <Style Selector="Button.danger">
    <Setter Property="Foreground" Value="{DynamicResource DangerBrush}"/>
  </Style>
  <!-- Sizes: compact for row actions, icon for the ⋯ buttons (values proposed). -->
  <Style Selector="Button.compact">
    <Setter Property="Padding" Value="10,3"/>
    <Setter Property="MinHeight" Value="26"/>
  </Style>
  <Style Selector="Button.icon">
    <Setter Property="Padding" Value="0"/>
    <Setter Property="Width" Value="32"/>
    <Setter Property="Height" Value="32"/>
  </Style>
  <!-- Rail item: a toggle with icon, label and optional count. -->
  <Style Selector="ToggleButton.nav">
    <Setter Property="Background" Value="Transparent"/>
    <Setter Property="BorderBrush" Value="Transparent"/>
    <Setter Property="HorizontalContentAlignment" Value="Left"/>
  </Style>
  <Style Selector="ToggleButton.nav:checked /template/ ContentPresenter#PART_ContentPresenter">
    <Setter Property="Background" Value="{DynamicResource AccentMutedBackgroundBrush}"/>
    <Setter Property="Foreground" Value="{DynamicResource AccentBrush}"/>
    <Setter Property="FontWeight" Value="SemiBold"/>
  </Style>
</Styles>
```

### C.3 Card (`Styles/Card.axaml`)

```xml
<Styles xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <Style Selector="Border.card">
    <Setter Property="CornerRadius" Value="{DynamicResource CardRadius}"/>
    <Setter Property="Background" Value="{DynamicResource CardBackgroundBrush}"/>
    <Setter Property="BorderBrush" Value="{DynamicResource CardBorderBrush}"/>
    <Setter Property="BorderThickness" Value="1"/>
    <Setter Property="Padding" Value="{DynamicResource SpaceLgPad}"/>
  </Style>
  <Style Selector="Border.card.compact"><Setter Property="Padding" Value="12,8"/></Style>
  <Style Selector="Border.card.titled > StackPanel > TextBlock.card-title">
    <Setter Property="FontWeight" Value="SemiBold"/>
    <Setter Property="Foreground" Value="{DynamicResource PrimaryTextBrush}"/>
    <Setter Property="Margin" Value="0,0,0,8"/>
  </Style>
  <Style Selector="Border.card.interactive">
    <Setter Property="Transitions">
      <Transitions><BrushTransition Property="BorderBrush" Duration="0:0:0.1"/></Transitions>
    </Setter>
  </Style>
  <Style Selector="Border.card.interactive:pointerover">
    <Setter Property="BorderBrush" Value="{DynamicResource AccentBrush}"/>
  </Style>
  <Style Selector="Border.card.interactive:focus-within">
    <Setter Property="Transitions" Value="{x:Null}"/>
    <Setter Property="BorderBrush" Value="{DynamicResource AccentBrush}"/>
  </Style>
  <Style Selector="Border.card.selected">
    <Setter Property="Background" Value="{DynamicResource AccentMutedBackgroundBrush}"/>
    <Setter Property="BorderBrush" Value="{DynamicResource AccentBrush}"/>
    <Setter Property="BorderThickness" Value="2"/>
  </Style>
  <!-- Stripes: a 3 px left edge. -->
  <Style Selector="Border.card.stripe-danger">
    <Setter Property="BorderThickness" Value="3,1,1,1"/>
    <Setter Property="BorderBrush" Value="{DynamicResource DangerBrush}"/>
  </Style>
  <Style Selector="Border.card.stripe-warning">
    <Setter Property="BorderThickness" Value="3,1,1,1"/>
    <Setter Property="BorderBrush" Value="{DynamicResource WarningBrush}"/>
  </Style>
  <Style Selector="Border.card.callout">
    <Setter Property="BorderThickness" Value="0"/>
    <Setter Property="Background" Value="{DynamicResource AccentMutedBackgroundBrush}"/>
  </Style>
  <Style Selector="Border.card.callout.info">
    <Setter Property="Background" Value="{DynamicResource InfoMutedBackgroundBrush}"/>
  </Style>
  <Style Selector="Border.card.callout.warning">
    <Setter Property="Background" Value="{DynamicResource WarningMutedBackgroundBrush}"/>
  </Style>
</Styles>
```

Section 7.2 gives the stripes a single coloured left edge with the other three edges in CardBorder; a `Border` has one brush, so the build issue may draw the stripe as a separate 3 px element inside the card instead.

### C.4 Pill (`Styles/Pill.axaml`)

```xml
<Styles xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <Style Selector="Border.pill">
    <Setter Property="CornerRadius" Value="{DynamicResource FullRadius}"/>
    <Setter Property="Padding" Value="7,2"/>
    <Setter Property="Background" Value="{DynamicResource HoverBackgroundBrush}"/>
    <Setter Property="IsHitTestVisible" Value="False"/>
  </Style>
  <Style Selector="Border.pill > TextBlock">
    <Setter Property="FontSize" Value="11"/>
    <Setter Property="FontWeight" Value="SemiBold"/>
    <Setter Property="Foreground" Value="{DynamicResource SecondaryTextBrush}"/>
  </Style>
  <Style Selector="Border.pill.count"><Setter Property="MinWidth" Value="20"/></Style>
  <Style Selector="Border.pill.accent"><Setter Property="Background" Value="{DynamicResource AccentMutedBackgroundBrush}"/></Style>
  <Style Selector="Border.pill.accent > TextBlock"><Setter Property="Foreground" Value="{DynamicResource AccentBrush}"/></Style>
  <Style Selector="Border.pill.success"><Setter Property="Background" Value="{DynamicResource SuccessMutedBackgroundBrush}"/></Style>
  <Style Selector="Border.pill.success > TextBlock"><Setter Property="Foreground" Value="{DynamicResource SuccessBrush}"/></Style>
  <Style Selector="Border.pill.warning"><Setter Property="Background" Value="{DynamicResource WarningMutedBackgroundBrush}"/></Style>
  <Style Selector="Border.pill.warning > TextBlock"><Setter Property="Foreground" Value="{DynamicResource WarningBrush}"/></Style>
  <Style Selector="Border.pill.danger"><Setter Property="Background" Value="{DynamicResource DangerMutedBackgroundBrush}"/></Style>
  <Style Selector="Border.pill.danger > TextBlock"><Setter Property="Foreground" Value="{DynamicResource DangerBrush}"/></Style>
  <Style Selector="Border.pill.info"><Setter Property="Background" Value="{DynamicResource InfoMutedBackgroundBrush}"/></Style>
  <Style Selector="Border.pill.info > TextBlock"><Setter Property="Foreground" Value="{DynamicResource InfoBrush}"/></Style>
  <Style Selector="Border.pill.stage-idea"><Setter Property="Background" Value="{DynamicResource StageIdeaBackgroundBrush}"/></Style>
  <Style Selector="Border.pill.stage-idea > TextBlock"><Setter Property="Foreground" Value="{DynamicResource StageIdeaForegroundBrush}"/></Style>
  <!-- stage-draft ... stage-published repeat stage-idea with their own brushes. -->
  <Style Selector="Border.pill.icon">
    <Setter Property="CornerRadius" Value="7"/>
    <Setter Property="Width" Value="28"/>
    <Setter Property="Height" Value="28"/>
    <Setter Property="Padding" Value="0"/>
  </Style>
</Styles>
```

The pill's font size (11) is proposed; section 7.3 says only "small semibold text".

### C.5 Chip (`Styles/Chip.axaml`)

```xml
<Styles xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <Style Selector="ToggleButton.chip">
    <Setter Property="CornerRadius" Value="{DynamicResource FullRadius}"/>
    <Setter Property="Padding" Value="10,3"/>
    <Setter Property="Background" Value="{DynamicResource CardBackgroundBrush}"/>
    <Setter Property="BorderBrush" Value="{DynamicResource CardBorderBrush}"/>
    <Setter Property="BorderThickness" Value="1"/>
    <Setter Property="Foreground" Value="{DynamicResource SecondaryTextBrush}"/>
    <Setter Property="FocusAdorner">
      <FocusAdornerTemplate>
        <Border BorderThickness="2" CornerRadius="{DynamicResource FullRadius}"
                BorderBrush="{DynamicResource AccentSecondaryBrush}"/>
      </FocusAdornerTemplate>
    </Setter>
  </Style>
  <Style Selector="ToggleButton.chip /template/ ContentPresenter#PART_ContentPresenter">
    <Setter Property="Transitions">
      <Transitions><BrushTransition Property="Background" Duration="0:0:0.1"/></Transitions>
    </Setter>
  </Style>
  <Style Selector="ToggleButton.chip:pointerover /template/ ContentPresenter#PART_ContentPresenter">
    <Setter Property="Background" Value="{DynamicResource HoverBackgroundBrush}"/>
  </Style>
  <Style Selector="ToggleButton.chip:checked /template/ ContentPresenter#PART_ContentPresenter">
    <Setter Property="Background" Value="{DynamicResource AccentMutedBackgroundBrush}"/>
    <Setter Property="Foreground" Value="{DynamicResource AccentBrush}"/>
    <Setter Property="FontWeight" Value="SemiBold"/>
  </Style>
  <Style Selector="ToggleButton.chip:disabled /template/ ContentPresenter#PART_ContentPresenter">
    <Setter Property="Foreground" Value="{DynamicResource MutedTextBrush}"/>
  </Style>
  <Style Selector="ToggleButton.chip.warning">
    <Setter Property="Background" Value="{DynamicResource WarningMutedBackgroundBrush}"/>
    <Setter Property="Foreground" Value="{DynamicResource WarningBrush}"/>
  </Style>
  <Style Selector="ToggleButton.chip.danger">
    <Setter Property="Background" Value="{DynamicResource DangerMutedBackgroundBrush}"/>
    <Setter Property="Foreground" Value="{DynamicResource DangerBrush}"/>
  </Style>
  <!-- Tags: compact, colour pair from the tag palette (WorkTypeService.vb:156-185), set per chip. -->
  <Style Selector="ToggleButton.chip.compact, ToggleButton.chip.tag">
    <Setter Property="Padding" Value="7,2"/>
  </Style>
</Styles>
```

Section 7.4 has toned chips ease 40 % toward CardBackground on hover; that blend, like the primary button's, is set from code.

### C.6 TabStrip (`Styles/Tabs.axaml`)

```xml
<Styles xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <Style Selector="TabControl /template/ ItemsPresenter#PART_ItemsPresenter">
    <Setter Property="Margin" Value="0"/>
  </Style>
  <Style Selector="TabItem">
    <Setter Property="Background" Value="Transparent"/>
    <Setter Property="Padding" Value="12,6"/>
    <Setter Property="Margin" Value="0,0,24,0"/>
    <Setter Property="MinHeight" Value="0"/>
    <Setter Property="FontSize" Value="13"/>
    <Setter Property="FontWeight" Value="Normal"/>
    <Setter Property="Foreground" Value="{DynamicResource SecondaryTextBrush}"/>
    <Setter Property="FocusAdorner">
      <FocusAdornerTemplate>
        <Border BorderThickness="2" CornerRadius="{DynamicResource ControlRadius}"
                BorderBrush="{DynamicResource AccentSecondaryBrush}"/>
      </FocusAdornerTemplate>
    </Setter>
  </Style>
  <Style Selector="TabItem:pointerover /template/ Border#PART_LayoutRoot">
    <Setter Property="Background" Value="Transparent"/>
  </Style>
  <Style Selector="TabItem:pointerover">
    <Setter Property="Foreground" Value="{DynamicResource PrimaryTextBrush}"/>
  </Style>
  <Style Selector="TabItem:selected">
    <Setter Property="Foreground" Value="{DynamicResource PrimaryTextBrush}"/>
    <Setter Property="FontWeight" Value="SemiBold"/>
  </Style>
  <Style Selector="TabItem:selected /template/ Border#PART_SelectedPipe">
    <Setter Property="Background" Value="{DynamicResource AccentBrush}"/>
    <Setter Property="Height" Value="3"/>
  </Style>
  <Style Selector="TabItem:disabled">
    <Setter Property="Foreground" Value="{DynamicResource MutedTextBrush}"/>
  </Style>
  <!-- The strip hairline (CardBorder, 1 px) is a Border under the TabStrip in the page template,
       so every page draws it the same way instead of each parent painting it. -->
</Styles>
```

`PART_LayoutRoot` and `PART_SelectedPipe` are the Fluent theme's TabItem parts; the build issue should check them against the Avalonia version it pins. Reserving the semibold width for the selected tab (7.5) needs a hidden semibold copy of the header text in a custom header template; that is left to the build issue.
