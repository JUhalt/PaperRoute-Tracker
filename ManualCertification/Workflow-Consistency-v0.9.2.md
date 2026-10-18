# Workflow consistency walk, v0.9.2

Every window and page PaperRoute shows, checked once against the v0.6 workspace patterns (#54 to #57, with the empty states of #59), for #151 and boxes 2 and 4 of #43. Walked on October 18, 2026, on v0.9.2 as it will ship (master cfc93a0 with speed parts A and B, pull requests #195 and #200).

**How it was read.** A throwaway sweep, never checked in, built every view in `PaperRoute.Tests/ViewCatalog.vb` (its dialogs, hosted pages and later steps) on the shared STA runner, each on a temporary folder of its own, styled as the app styles it, and read from the live window: the default button (AcceptButton, Enter), the cancel button (CancelButton, Esc), the buttons along the bottom, the access keys of labels, buttons, check boxes and options, the shared empty hints and the **?** help buttons. Every row below was read that way (L). Rows marked C were also read in the code, for a reason or a step the catalog does not show.

## The patterns

- **Page frame.** A page title with the page's own actions on the right, at most one of them primary, a line saying what the page is for, then the content. The manuscript page adds a crumb back to where it was opened (**← Board / shelf**), tabs, and the **Save / Discard** bar.
- **Dialog footer.** Buttons along the bottom right. A dialog that commits ends with **Cancel** and its main action, which is the default button (Enter) and is filled as the one primary action; Esc is Cancel. A dialog that only shows things ends with **Close**, which is both.
- **Shared empty state.** An empty list or table says what belongs there and how to add the first one (`EmptyHint`).
- **Shared contextual help.** A **?** button beside a field or badge that needs a definition (`ContextHelpControl`, words from `WorkflowHelpCatalog`); F1 opens the User Guide.
- **Access keys.** Fifteen windows give their fields or buttons access keys (Alt and a letter); the rest give none, which one row decides for all of them.

In the table, the default button is in bold, Enter and Esc name what they press, and "keys" lists the window's Alt letters ("—" for none).

## Windows and pages

| Window | Pattern followed | Deviation | Keep, fix or defer |
|---|---|---|---|
| Main window: Board, its welcome, and the Board filled in slices (L, C) | Page frame: Board with search and **+ Add Manuscript**; shelf tabs with counts; Needs Attention chips; the welcome on an empty library. No Enter or Esc; Ctrl+1 to 6, Alt+Left and Right, Ctrl+F, F1; keys — | Published and File Drawer shelves, when empty, say only "No published manuscripts yet." and "The File Drawer is empty.", not what belongs there (Form1.vb:2595-2639) | Defer: copy polish, with File Drawer polish (decision 6) and the calmer surfaces of v1.1 |
| Library page (L) | Page frame: Library with its filter; Manuscripts and Authors & Affiliations tabs; table with the shared empty hint; Enter opens a row; keys — | — | Keep: follows the page frame |
| Library › Authors & Affiliations, `AuthorLibraryForm` hosted (L) | The editor hosted as #54 allowed: Close hidden, Enter and Esc cleared; a button row under each list; shared empty hints; keys — | Its actions sit under its lists, not in the page header | Defer: hosted editors are #54's interim; the v1.1 design simplification on #43 takes them |
| Journals page, `JournalLibraryForm` hosted (L) | As above: Add Journal, Edit, Delete, Open Homepage, Open Portal in a row under the list; shared empty hint; keys — | A dialog's button row inside a page, where Board and Deadlines put actions in the header | Defer: same reason |
| Deadlines page (L) | Page frame: Deadlines with **Export Calendar...** and **+ Add Reminder**; filter; drawn list that says when nothing needs action; keys — | — | Keep: follows the page frame |
| Insights page, and Your Citations before and after an update (L) | Page frame: Insights with **Pipeline Report...**; tabs; tables with shared empty hints; Your Citations offers Update from OpenAlex... or Open Authors & Affiliations; keys — | — | Keep: follows the page frame |
| Import & Export page (L) | Page frame: fourteen commands with headings and a line each; Backup and Restore last; keys — | — | Keep: follows the page frame |
| Manuscript page, `EditManuscriptForm` hosted: opened again, with Submission Details and the reviewer matrix inline, and after its shortlist is rebuilt (L, C) | Crumb, title with Copy Citation, View Route and ⋯; tabs; Save and Discard bar (Ctrl+S); six shared empty hints; keys — of its own, the inline matrix brings S, A, E, R, U, D, X | No Restore to Pipeline or Move to File Drawer here (File Drawer note 5); its labelled fields have no keys | Defer: note 5 is File Drawer polish; keys are the app-wide row |
| `AboutForm` (L) | Footer Copy Version Info · **Close**; Enter and Esc Close; keys — | — | Keep: a viewer ends with Close |
| `AddCorrespondenceForm` (L) | Cancel · **Add Item**; Enter Add Item, Esc Cancel; keys — | — | Keep: follows the dialog footer |
| `AddDecisionForm` (L) | Cancel · **Add Decision**; keys — | — | Keep: follows the dialog footer |
| `AddManuscriptForm` (L) | Cancel · **Add Manuscript**; keys — | — | Keep: follows the dialog footer |
| `AddSubmissionForm` (L) | Cancel · **Add Submission**; **?** on Journal manuscript ID; keys — | — | Keep: follows the dialog footer |
| `AffiliationEditForm` (L) | Cancel · **Save**; keys — | — | Keep: follows the dialog footer |
| `ApiKeyForm`, `OpenAlexKeyForm` (L) | Cancel · **Add Key**, off until a key is typed; keys K | — | Keep: follows the dialog footer |
| `AssistantConsentForm` (L) | Cancel · **Send**; keys S | — | Keep: follows the dialog footer |
| `AssistantDraftForm` (L, C) | Close · **Try Again**, then **Use as Draft** or **Replace Draft** with Add Below; Esc Close; keys T, then U or R, A | — | Keep: follows the dialog footer |
| `AuthorEditForm` (L) | Cancel · **Save**; keys — | — | Keep: follows the dialog footer |
| `AuthorInstructionsForm`, pasting and reviewing (L) | Cancel · **Read Instructions**, then Read Again · Cancel · **Add 6 Requirements**; Esc stops a reading, then cancels; keys T, P on the paste step | The review step's headings over the read text, the grid and the quote carry no keys | Keep: they head read-only text and a list, not fields |
| `AuthorLibraryForm` as a dialog (L) | Button row under each list · **Close**; Enter and Esc Close; shared empty hints; keys — | — | Keep: a library editor ends with Close |
| `BibliographyExportSelectionForm` (L) | Cancel · **Export Selected**; Esc Cancel; keys — | Enter pressed nothing: no default button | Fix, done: Enter is Export Selected ("Give three dialogs a default button") |
| `BibliographyImportForm` (L) | Cancel · **Import Selected**; Esc Cancel; keys — | Enter pressed nothing: no default button | Fix, done: Enter is Import Selected (same commit) |
| `CitationsUpdateForm`, looking up and confirming (L) | Cancel · **Look Up**, then Cancel · **Save**; keys — | — | Keep: follows the dialog footer |
| `CoverLetterForm` (L, C) | Close · **Draft**, then Save As..., Draft Again · **Copy**; Esc stops a request, then closes; keys J, T, K, A, D, then S, A, C | A is Abstract on the first step and Draft Again on the result; the two never show together (CoverLetterForm.vb:320-369) | Keep: one letter per step |
| `CrossrefLookupForm` (L) | **Look Up** beside the DOI; Cancel · Apply Selected; keys — | — | Keep: Enter looks up, the footer applies |
| `DeadlineDateForm` (L) | Cancel · **Postpone to** the chosen day; keys — | — | Keep: follows the dialog footer |
| `DecisionLetterForm`, pasting and reviewing (L) | Cancel · **Read Letter**, then Read Again · Cancel · **Add Decision & 3 Comments...**; keys P, then D, R, C | The review step's headings over the letter and the comment grid carry no keys | Keep: they head read-only text and a list; its fields have keys |
| `DeleteManuscriptForm` (L) | Cancel · Delete Manuscript, off until DELETE is typed; no default; Esc Cancel; keys — | No default button | Keep: Enter must never delete |
| `DiagnosticsForm` (L) | Open Data Folder, Save Report..., Copy Report · **Close**; keys — | — | Keep: a viewer ends with Close |
| `EditManuscriptForm` (L) | Only ever the manuscript page; see that row | — | Keep: never shown as a dialog |
| `ExcelMappingForm` (L, C) | Auto-map, Clear mappings; Cancel · Import Mapped Data; Esc Cancel; keys — | No default button, so its main action is not filled as primary | Keep: Enter in Header row would import before the columns it maps are seen |
| `ExternalLinkEditForm` (L) | Cancel · **Save**; keys — | — | Keep: follows the dialog footer |
| `FillBlanksForm`, looking up and filling (L) | Cancel · **Look Up 1 Manuscript**, then **Fill 5 Fields**; Esc stops a lookup, then cancels; keys — | — | Keep: follows the dialog footer |
| `HelpForm` (L) | Find box with **Find Next**; Open Guide on GitHub · Close; Esc Close; keys — | — | Keep: Enter finds, Esc closes |
| `JournalCandidateForm` (L) | Cancel · **OK**; keys J, S, W | Its thirteen check questions have no keys | Keep: thirteen letters would crowd a checklist; Tab and Space reach each |
| `JournalChecklistItemEditForm` (L) | Cancel · **Save**; four **?** help buttons; keys — | — | Keep: follows the dialog footer |
| `JournalEditForm` (L) | Cancel · **Save**; keys — | — | Keep: follows the dialog footer |
| `JournalFactsForm`, finding and reviewing (L, C) | Find by name, Cancel · Look Up, then Cancel · **Save**; Enter in the name box runs Find; keys N | No default button until there is an ISSN | Keep: Enter belongs to Find while the name is typed |
| `JournalLibraryForm` as a dialog (L) | Add Journal, Edit, Delete, Open Homepage, Open Portal · **Close**; shared empty hint; keys — | — | Keep: a library editor ends with Close |
| `JournalMetricForm` (L) | Cancel · **Save**, off until filled; keys N, M, V, Y, S, F | — | Keep: follows the dialog footer |
| `JournalPickerForm` (L) | Cancel · **Choose**; keys — | — | Keep: follows the dialog footer |
| `JournalSuggestionsForm`, searching and choosing (L, C) | Cancel · **Search** (Enter while typing a keyword is Add), then Back · Cancel · **Add Checked to Shortlist**; keys K, D | Published in the last had no key while the window's other fields do | Fix, done: Alt+L ("Give the unkeyed fields of two keyed dialogs access keys"); Articles must match stays without one, since a key there would check its first option |
| `ManuscriptAuthorForm` (L) | Cancel · **Save**; keys — | — | Keep: follows the dialog footer |
| `ManuscriptLinksForm` (L) | Link buttons; Cancel · **Save & Close**; keys — | — | Keep: follows the dialog footer |
| `ManuscriptReadinessForm` (L) | Cancel · **Save & Close**; two **?** help buttons; keys — | — | Keep: follows the dialog footer |
| `ManuscriptRouteViewForm` (L) | **Close**; Enter on a waypoint's link opens it; two **?** help buttons; keys — | — | Keep: a viewer ends with Close |
| `ManuscriptVersionEditForm` (L) | Cancel · **Save Changes**; five **?** help buttons; keys — | — | Keep: follows the dialog footer |
| `MarkPublishedForm` (L) | Cancel · **Mark Published**; keys — | — | Keep: follows the dialog footer |
| `OrcidLookupForm` (L, C) | Look Up beside the iD; Cancel · Apply Selected, off until a lookup; no default; Esc Cancel; keys — | Unlike DOI & Crossref Metadata, Enter in the iD box does nothing | Keep: as the default, Enter would look up again and reset the changes and works already checked (OrcidLookupForm.vb:456-561) |
| `PacketExportForm` (L) | Cancel · Export... (filled); no default; keys N, A, K, E | No default button | Keep: Enter commits an edit in the file list (PacketExportUiTests) |
| `PublicationCheckForm` (L) | Cancel · **Check 1 Manuscript**; Esc stops a check, then cancels; keys — | — | Keep: follows the dialog footer |
| `PublicationExportForm` (L) | Close · Copy to Clipboard · Save Export...; no default; Esc Close; keys — | No default button | Keep: two equal ways out, neither the main one |
| `ReadinessItemNotesForm` (L) | Cancel · **Save Notes**; keys — | — | Keep: follows the dialog footer |
| `ReminderEditForm` (L) | Cancel · **Add Reminder**; keys — | — | Keep: follows the dialog footer |
| `ReportForm` (L) | Cancel · **Save Report...**; keys — | — | Keep: follows the dialog footer |
| `ReviewerResponseExportForm` (L) | Save Markdown... · Close; no default; Esc Close; keys S | No default button | Keep: its one field takes Enter as a new line |
| `ReviewerResponseItemForm` (L) | Cancel · **Save Comment**; keys D, R, V, S, C, G | Its five text fields, on three tabs, had no keys while its other fields do | Fix, done: Alt+M, P, E, L, N (same commit as Find Journals) |
| `ReviewerResponseMatrixForm`, as a dialog and inline (L) | Export Markdown... · Cancel · **Save & Close**; inline it joins the page's Save; shared empty hint; keys S, A, E, R, U, D, X | — | Keep: follows the dialog footer |
| `SettingsForm` (L) | Cancel · **Save**; keys — | — | Keep: follows the dialog footer |
| `StartupFailureForm` (L) | Use the Previous Save, Restore From Backup..., Open Data Folder, Copy Details · **Close**; keys — | — | Keep: Enter and Esc never start a recovery |
| `SubmissionDetailsForm`, as a dialog and inline (L) | Reviewer Responses... · **Close**; inline no footer; shared empty hints; keys — | — | Keep: follows the dialog footer |
| `SubmissionPacketEditForm`, `SubmissionPacketFileEditForm` (L) | Cancel · **Save Changes**; keys — | — | Keep: follows the dialog footer |
| `SubmissionPacketVaultForm` (L) | Packet and file buttons; Cancel · **Save & Close**; keys — | — | Keep: follows the dialog footer |
| `TitlePageImportForm`, pasting and reading (L) | Cancel · **Use These Details**; Esc Cancel; keys — | Enter pressed nothing: no default button | Fix, done: Enter is Use These Details ("Give three dialogs a default button") |
| `UnexpectedErrorForm` (L) | Copy Details, Open Log Folder, Close PaperRoute · **Keep Working**; keys — | — | Keep: Enter and Esc keep working |
| `UpdateProgressForm` (L) | Progress only; no buttons | No Esc | Keep: nothing in it can stop a download (#136) |
| `UpdatePromptForm` (L) | Later · **Download & Restart**; Esc Later; keys — | — | Keep: follows the dialog footer |
| Access keys, app-wide (L) | Fifteen windows above have keys; no window gives one letter to two controls shown together | The other forty-six windows have none, and no page has keys of its own | Defer to v1.1: Tab, Enter and Esc reach every control (#136, #137, #139); choosing letters is a window-by-window pass, and the no-repeat test from this walk holds each new one against the keys shown with it, tab by tab |
| Windows' own boxes: messages, file pickers (C) | Drawn by Windows, with its own keys and colors | Not in the catalog | Keep: the guide's "What Windows draws" (#139) says so |
| Move to File Drawer's reason prompt (C) | A Visual Basic input box after a Yes/No question (Form1.vb:3501-3527) | Cancel at the reason still files and saves, since it cannot be told from OK with no reason (note 6); two prompts for one move; not in the catalog, so no sweep reads it | Fix: proposed issue D, for Cancel; asking twice is File Drawer polish, deferred; look at it in Dark and at 150% during certification |
| Restore Backup's typed confirmation (C) | A Visual Basic input box, "Confirm Restore", after the Yes/No preview (Form1.vb:3980-3998) | Not in the catalog, so no sweep reads it | Keep: typing RESTORE is the safeguard before the library is replaced; look at it in Dark and at 150% during certification |

## File Drawer notes

**The walk.** The example library (`ExampleLibraryService`, written by `LargeLibraryFixture.WriteExample` under a temporary folder) in the real main window (`LibraryBoard`), on "Example: a null result on priming and choice": filed with three rejections and the reason "Three rejections; revisit with a larger sample." On the Board's File Drawer tab, its card's ⋯ menu, then **Restore to Pipeline**; its page; **Add Journal...** for Fictional Open Psychology as Preferred; the offer's **Make It the Target Journal**; **Record Submission...** on Submissions; **Save**; back to the Board.

**What worked.** Restore saved at once, and the card moved to Pipeline. After a journal was shortlisted, the page offered it: "Rejected by Fictional Assessment Quarterly on Apr 9, 2026. Next on your shortlist: Fictional Open Psychology (Preferred)." Taking the offer set the target journal and put the change on the Save bar. The new submission moved the stage to Submitted, and the route reads "4th journal · 3 rejections". Nothing invented a submission or a date.

**Rough edges**, each with where it is (line numbers on this branch):

1. **Leaving the File Drawer throws away why it was filed.** `RestoreToPipeline` (Form1.vb:3574-3606) clears the filed date and reason, and its history note, "Restored from File Drawer to active Pipeline.", keeps neither. Move to File Drawer writes the reason into its own note (Form1.vb:3538-3549), but a manuscript filed by an import (StandardExcelImporter.vb:230-262, FlexibleExcelImporter.vb:530-552, LegacyExcelImporter.vb:403-410) or in the example has no such note, so the reason is gone, and Route View loses its File Drawer waypoint, which is drawn only while the manuscript is filed (ManuscriptRouteProjectionService.vb:556-579). Mark Published does the same to a filed manuscript, which a publication check lists (PublicationMatchService.vb:71-75, PublicationCheckForm.vb:223): `PublicationMatchService.MarkPublished` (PublicationMatchService.vb:303-316, from PublicationCheckForm.vb:534 and Deadlines' Mark Published..., Form1.Publications.vb:95) clears both, and its note, "Published in … Marked from a publication check." (PublicationMatchService.vb:336-340), keeps neither. That second path was found by reading the code, not met in the walk; a stage change to Published on the manuscript page keeps both (EditManuscriptForm.vb:4108-4119). Fix: proposed issue A.
2. **"3 rejections; consider filing" on a manuscript just revived and submitted.** The card's File Drawer suggestion (Form1.vb:1059-1068) counts every rejection the manuscript ever had (ManuscriptAttentionService.vb:78-85), so after the restore and a fourth submission the card read "SUBMITTED · 3 rejections; consider filing". Fix: proposed issue B.
3. **Record Submission... forgets the target journal.** The Submissions tab opens a blank Add Submission (EditManuscriptForm.vb:3508) right after Make It the Target Journal, while recording a submission from a stage change (EditManuscriptForm.vb:3790-3794) and from a packet (EditManuscriptForm.Workflow.vb:58) start at the journal. Fix: proposed issue C.
4. **Restore says nothing about where the manuscript went.** The Board stays on the File Drawer tab, which now says "The File Drawer is empty.", and the status line says only "Saved locally". Defer: File Drawer polish (decision 6).
5. **Revival is only on the Board.** Restore to Pipeline is on the card's ⋯ menu alone (Form1.vb:3250-3272); the manuscript page shows when and why it was filed (EditManuscriptForm.vb:1165-1237) but its ⋯ menu has Route Report, Check for Publication and Delete (Form1.ManuscriptPage.vb:489-501), and the Library has neither command. Defer: polish.
6. **Move to File Drawer asks twice, and its second Cancel still files.** A Yes/No question, then an input box for an optional reason (Form1.vb:3501-3527). The input box returns the same empty text for Cancel as for OK with no reason, so Cancel there still moves the manuscript, dates it, adds "Moved to File Drawer." to its history and saves (Form1.vb:3529-3568). Fix: proposed issue D, for Cancel. Defer: asking twice is polish; the input box's Dark and 150% look is for certification.
7. **The revived card reads "DRAFT · 62 days",** counting the time in the File Drawer as time in Draft, since the stage did not change (StageClockService). Keep: it is accurate, and the history shows the restore.

## Proposed issues

- **A. Leaving the File Drawer keeps why it was filed**, by Restore to Pipeline and by Mark Published (note 1).
- **B. No "consider filing" while a submission is open or after a revival** (note 2).
- **C. Record Submission... starts at the target journal** (note 3).
- **D. Cancel on Move to File Drawer leaves the manuscript where it was** (note 6).

Fixed during the walk, each with its test in `KeyboardAccessTests`: "Give three dialogs a default button" (`EveryDialogWithAMainActionHasADefaultButton`) and "Give the unkeyed fields of two keyed dialogs access keys" (`EachFieldOfTheReviewerCommentAndFindJournalsHasAnAccessKey`, and the guard `NoWindowRepeatsAnAccessKeyItShowsTogether`).
