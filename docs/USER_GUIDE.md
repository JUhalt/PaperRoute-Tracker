# PaperRoute User Guide

PaperRoute is a local-first academic manuscript tracker for researchers. It is designed to keep the complete route of a paper understandable: idea, writing, submission, peer review, revision, publication, or the File Drawer.

This guide describes **PaperRoute v0.5.0 — Reviewer Response Workflow**. See the [v0.5.0 release notes](releases/0.5.0.md) for the changes and the [repository homepage](../README.md) for current release availability.

## Quick Start

If you only read one section, read this one.

1. Open PaperRoute and choose **Add Manuscript**.
2. Give the manuscript a title and place it at the stage that best matches reality.
3. Open **Manuscript Details** to add structured authors, a target journal, metadata, links, and submission history.
4. Before submitting, record the exact manuscript snapshot in **Version History**, use **Submission Readiness...** for the journal's checklist, and assemble **Submission Packets...** for the files you intend to send.
5. When you actually submit the paper, use the packet's **Record Submission...** action or add a **Journal Submission** with the journal, date, Journal manuscript ID if available, portal URL, and optional follow-up date.
6. Save each child dialog, then choose **Save & Close** in **Manuscript Details** to persist the complete workflow.
7. When the journal responds, open that submission and record the **Editorial Decision**. Revision decisions can carry a revision deadline.
8. Open **Reviewer Responses...** in that submission to track individual comments, actions, and response drafts. Save the matrix, close Submission Details, then save Manuscript Details. Keep original letters and revised files under the submission's correspondence.
9. Use **Settings > Reminders & Calendar...** to see revision deadlines, journal follow-ups, and custom reminders in one place.
10. Use **Data > Backup Library...** before major changes or moving PaperRoute to another computer.

PaperRoute does not require an account for its core workflow, and the manuscript-tracking database is stored locally.

---

## What PaperRoute Does

### The three shelves

PaperRoute organizes manuscripts into three broad locations:

- **Pipeline**: active work, including ideas, drafts, submissions, reviews, and revisions.
- **Published**: completed published work.
- **File Drawer**: work you are not currently pursuing.

A manuscript can move through stages without losing its history. The File Drawer is not a deletion mechanism; a filed manuscript can be restored to the active Pipeline.

### Manuscript stages

PaperRoute currently supports these stages:

- Idea
- Draft
- Submitted
- Under Review
- Revision
- Accepted
- In Press
- Published

The stage describes the manuscript's current lifecycle position. Submission history, editorial decisions, and correspondence provide the detailed record underneath that stage.

### The Needs Attention area

The main board can flag manuscripts that may need action, including:

- overdue revision deadlines;
- revision deadlines approaching within your configured warning window;
- unusually long reviews;
- missing target journals; and
- recent rejections.

These are prompts, not automatic decisions. PaperRoute does not move manuscripts or file them simply because a threshold was reached.

### Search, filters, and sorting

The main board supports search, stage filtering, and sorting. Search includes manuscript information and structured author metadata where available.

---

## Manuscript Details

Open a manuscript from the board to reach **Manuscript Details**.

This is the main working area for:

- title;
- legacy co-author text;
- target journal;
- current stage;
- publication metadata;
- structured authors and affiliations;
- manuscript Version History;
- journal-specific readiness and submission packets;
- journal submissions and their reviewer-response matrices;
- preprint and project links; and
- File Drawer information when relevant.

PaperRoute uses a working copy while the Manuscript Details window is open. Readiness, packet, version, submission, and reviewer-response edits saved in child dialogs update that working copy. Choose **Save & Close** in Manuscript Details to persist it; choosing **Cancel** there discards unsaved manuscript changes.

### Version History and the Route

**Version History** records meaningful manuscript snapshots without overwriting earlier files. A version may be:

- copied into the PaperRoute Library as an immutable historical snapshot;
- linked to an original file that remains under your control; or
- tracked as metadata only.

A version can be associated with the journal submission for which that exact file was sent. A later revised version can also be associated with the editorial decision that prompted it and with a revision-round number.

**Current Version** means the manuscript snapshot you currently consider your active working version. This is different from **Current State**, which means the manuscript's lifecycle position such as Submitted, Revision, Accepted, or Draft.

Use **View route →** from the main board to see the manuscript's deterministic publication history. The Route is read-only: double-clicking or opening a Route waypoint returns you to the authoritative record in Manuscript Details rather than creating a second editing pathway.

Deleting a Version History record is also working-copy based. If the version owns an immutable PaperRoute Library snapshot, the snapshot is removed only when **Save & Close** succeeds. Choosing **Cancel** leaves the saved version history and managed snapshot intact. Deleting a linked-file version never deletes the original external file.

### Submission Readiness and Packets

These features were introduced in PaperRoute v0.4.0.

From **Manuscript Details**, open **Submission Readiness...** to apply a reusable journal checklist and track manuscript-specific requirements. Complete, not-applicable, and unresolved states explain the readiness summary. Readiness is advisory: it does not change the manuscript stage or prevent recording a real submission.

Choose **New from Journal...** to copy that journal's current template into a new readiness profile. Later template edits do not rewrite existing profile wording or progress. **Add New Template Requirements** adds requirements that are new to the selected profile while retaining its existing states and notes.

Open **Submission Packets...** to assemble file records tied to an exact **Version History** entry. You can optionally associate a packet with an existing journal submission and revision round. Preparing a packet does not record a submission. Managed copies become separate snapshots when Manuscript Details is saved; external links continue to point to the original files. Metadata-only entries contain no file to check.

#### Move between related records

In readiness, **Save & Go To... > Save & View Packets** opens the packets linked to the selected profile. **Open Submission Portal** uses that profile's linked journal when it has a web portal recorded.

In the vault, **Save & Go To...** can return to Manuscript Details, select the packet's exact version, or open its linked readiness profile or actual submission. **Submission Packets...** in Version History shows packets for the selected version; **View Submission Packets...** in Submission Details shows packets for that submission. The vault explains which records it is showing, and **Show all packets** removes that filter. New packets inherit the selected context, with choices visible in the packet editor.

For an unlinked preparation packet, choose **Save & Go To... > Record Submission...** and complete **Record Journal Submission**. Cancel returns to the prepared packet without recording an event. Adding the submission associates that packet with the new record and applies the usual manuscript-stage rules. Unresolved readiness and an empty file list do not prevent recording what actually happened. For a revision round of an existing submission, edit the packet and select the existing submission and round instead of recording a duplicate submission.

These navigation actions save the current child dialog into Manuscript Details' working copy. Choose **Save & Close** in Manuscript Details to keep the full workflow; Cancel there discards its unsaved manuscript changes. Version records retain their existing submission/decision history: the same exact version can appear in separate packets for different interactions. Packet navigation shows each packet's own associations without rewriting that earlier history.

Changing a submission to a different linked journal is rejected if it conflicts with its packets. Review or reassign those packet associations first. Imported revision-round numbers above 99 are preserved when editing notes.

#### Check whether packet files changed

1. Select a file and choose **Record Fingerprint** to record a SHA-256 fingerprint of its current contents. This is optional and reads the file without changing it.
2. Choose **Check Files** to compare all files in the selected packet with their saved fingerprints. Checking never replaces a fingerprint or changes manuscript history.
3. Read the status beside each file and the selected-file details:

| Status | Meaning |
| --- | --- |
| No fingerprint | No content comparison point has been recorded. |
| Not checked | A fingerprint exists, but contents have not been checked in this open window. |
| Unchanged | Contents matched the fingerprint at the last check. |
| Changed | Contents differed from the fingerprint at the last check. |
| Missing | No file was found at its recorded path. |
| Unavailable | PaperRoute could not read the file, for example because it was locked or access was denied. |
| Metadata only | This entry has no file contents to compare. |

Results reflect the last observation, not continuous monitoring. Choose **Check Files** again after editing or moving files. File size and modification date alone do not establish that contents are unchanged. Hashing runs in the background; Cancel closes the dialog without adopting pending results.

**Replace Fingerprint...** explicitly replaces an existing comparison point after confirmation. To preserve an earlier submitted file, retain its packet record and add a new file record instead. A fingerprint detects differences; it cannot reconstruct an old external file. Choose a managed copy when you need PaperRoute to retain the actual bytes.

Choose **Save & Close** in the vault and then **Save & Close** in **Manuscript Details** to persist newly recorded fingerprints and packet edits. Canceling Manuscript Details discards those unsaved changes. Managed-file copies and portable backup/restore preserve the saved fingerprints; they do not silently record a new baseline. A packet-linked version or submission must be unlinked from the packet, retargeted where appropriate, or have the packet removed before that referenced record can be deleted.

### Legacy co-author text

Older or imported records may still contain free-text co-author information. PaperRoute preserves that text rather than silently parsing or replacing it.

Structured authors are the preferred workflow for reusable people, affiliations, ordering, and ORCID information.

---

## Reusable Authors and Affiliations

Choose **Data > Authors & Affiliations...** to manage reusable people and institutions.

An author record can contain:

- given, middle, and family names;
- suffix;
- preferred/display name;
- ORCID iD;
- notes;
- reusable affiliations; and
- the **Me** designation for your own author record.

A manuscript stores its own author order. Reordering authors on one manuscript does not reorder them everywhere else.

A manuscript can also designate a corresponding author independently of reusable author identity.

### Why use the reusable author library?

It avoids retyping the same collaborator information across manuscripts and gives DOI/Crossref, ORCID, bibliography import, and publication export a structured author model to work with.

---

## DOI and Crossref Metadata

Open a manuscript's metadata workflow and use DOI/Crossref when you have a DOI or doi.org link.

PaperRoute can:

- normalize DOI input;
- retrieve public metadata from Crossref;
- preview the returned information;
- apply only the fields you choose;
- match structured authors by ORCID or name;
- create missing reusable authors/affiliations when approved; and
- record provenance for applied Crossref metadata.

Crossref enrichment does **not** silently change manuscript stage, shelf/location, or target journal.

External metadata should help fill a record, not take control of the research workflow.

---

## ORCID Public-Profile Import

Choose **Data > Authors & Affiliations...**, select an author, and use the ORCID workflow.

PaperRoute can read public ORCID information for a user-supplied ORCID iD, including public identity information, public employment/affiliation data, and public works.

Important boundaries:

- the lookup is user initiated;
- PaperRoute stores no ORCID password;
- PaperRoute stores no ORCID OAuth token or client secret;
- a successful public lookup confirms that the iD exists in the public registry, not that the record holder authenticated to PaperRoute.

When importing public works, dated works can be placed directly on the Published shelf only when you explicitly choose that behavior. Undated works remain Ideas. Duplicate protection uses DOI first and exact title second.

---

## BibTeX and RIS

Choose **Data > Import BibTeX / RIS...** to bring bibliography records into PaperRoute.

PaperRoute maps common scholarly metadata such as:

- title;
- authors;
- DOI;
- journal/outlet;
- publication date;
- volume;
- issue;
- pages;
- publisher;
- URL;
- abstract; and
- keywords.

Unsupported or ambiguous fields are shown as warnings instead of being silently discarded.

Importing publication metadata does not fabricate journal-submission history.

### Exporting bibliography records

Use:

- **Data > Export Library as BibTeX...**
- **Data > Export Library as RIS...**

You choose which manuscripts to export.

---

## Journal Library, Portals, Preprints, and Project Links

Choose **Data > Journal Library...** to maintain reusable journal information.

Journal records can include:

- journal name;
- publisher;
- journal homepage;
- submission portal;
- notes;
- Favorite status;
- Shortlist status; and
- a reusable readiness checklist.

A manuscript can link its target journal to one of these reusable records while retaining the free-text target-journal field for backward compatibility.

### Journal checklist templates

Add or edit a journal and open its **Readiness Checklist** tab. Use **Add Requirement** to give a requirement a title, instructions, category, and required/optional designation. You can edit, remove, or reorder requirements before saving the journal.

Apply this template from a manuscript's **Submission Readiness...** window. Each readiness profile keeps its own copied requirements and completion history, so changing the reusable journal template does not silently rewrite earlier preparations.

### Publisher portals

PaperRoute stores portal **links**, not publisher credentials.

Do not store publisher passwords in PaperRoute notes or fields. PaperRoute intentionally does not provide a password vault.

### Manuscript-specific URLs

A manuscript may also have a manuscript-specific URL, such as a deep link into a publisher portal or other workflow page. This is separate from the journal's general submission portal.

### Preprints

The manuscript Links workflow supports:

- preprint DOI;
- preprint URL.

### Related web links

You can save labeled web links such as:

- OSF Project;
- Preregistration;
- Data Repository;
- Materials;
- Publisher Page; or
- another project destination.

For safety, PaperRoute opens only valid `http://` or `https://` URLs.

---

## Journal Submissions

A manuscript can have multiple journal submissions over time.

Each submission can contain:

- journal name;
- optional reusable journal link;
- Journal manuscript ID;
- submission date;
- publisher portal URL;
- optional follow-up date; and
- notes.

The follow-up date appears in **Settings > Reminders & Calendar...** whenever it is explicitly set. PaperRoute treats it as a user-owned reminder, so recording an editorial decision does not silently remove it; clear or change the follow-up date when it is no longer useful.

### Reusing the Journal Library

When recording a submission, choose **Use Library...** to select a reusable journal. PaperRoute can populate the journal and standard portal URL.

A submission can still retain its own deeper or manuscript-specific portal URL.

---

## Editorial Decisions and Revision Deadlines

Inside a journal submission, record editorial decisions as they arrive.

Decision records preserve the journal-specific history rather than reducing everything to a single current status.

Revision decisions can include a revision deadline. When the manuscript is in the Revision stage, that deadline appears in the reminder workflow.

PaperRoute's reminder view is derived from the stored editorial-decision deadline; it does not create a second hidden copy of the revision date.

For discoverability, **Manuscript Details** also shows a **Revision deadline** row. **Set / Edit...** opens the latest submission's editorial decision so you can record or change the deadline without hunting through the submission tabs. If the manuscript has no journal submission yet, PaperRoute explains that a submission must be recorded first.

---

## Reviewer Response Matrix

Open an existing journal submission in **Manuscript Details**, record its editorial decision, then choose **Reviewer Responses...** in Submission Details. Keep a normal revision within the existing journal submission; create a new submission attempt only when that is what happened.

### Record and track comments

Choose **Add Comment...** and select the recorded **Editorial decision**. Enter the positive **Revision round** number explicitly; PaperRoute does not infer historical rounds from the order of decisions.

Each item stores a reviewer/editor label, comment or action, status, draft response, manuscript location, and working notes. A reviewer label and either a comment or an action are required. Use separate items for separate requests and consistent labels such as "Reviewer 1" or "Editor".

| Status | Meaning |
| --- | --- |
| Unresolved | You have not yet resolved this request. |
| In progress | You are working on the revision or response. |
| Addressed | You consider this request addressed. |
| Not applicable | You have decided the request does not apply; explain why in the response or notes. |

These statuses describe response work. They do not change manuscript stage, record a submission, or complete a reminder. The workflow uses manual entry and works without AI or an online account.

Choose **Save Comment** to accept an item into the matrix. Use **Show status** to focus the list. Choose **All statuses** before using **Move Up** or **Move Down**; ordering applies to the complete sequence of items. A decision linked to response items cannot be deleted until you reassign or remove those items.

### Save the complete workflow

1. Choose **Save Comment** in the item editor.
2. Choose **Save & Close** in the matrix to accept its edits into the manuscript working copy.
3. Choose **Close** in Submission Details. This window has no additional save/cancel boundary.
4. Choose **Save & Close** in Manuscript Details to store the manuscript permanently.

**Cancel** in the item editor or matrix discards that dialog's edits. **Cancel** in Manuscript Details discards all its unsaved manuscript changes, including changes already accepted from the matrix. Reopen the manuscript to confirm saved work.

### Export an editable response draft

Choose **Export Markdown...**, review or edit the preview, then choose **Save Markdown...** to save a file. The export includes the submission context, decision and revision round, reviewer labels, comments, actions, statuses, responses, locations, and working notes in stored order. The status filter does not limit the export: it includes the complete matrix. Review the notes before sharing the draft with a journal.

Preview edits and the exported file are independent of the matrix. Exporting does not save matrix edits or change item status or manuscript history. An export can therefore include work that you later cancel in PaperRoute. Subsequent matrix changes do not update a previously exported file.

---

## Correspondence and Files

Submission history can include correspondence and related files such as:

- decision letters;
- reviewer comments;
- editor emails;
- cover letters;
- response-to-reviewers letters;
- revised manuscripts;
- acceptance letters; and
- other supporting material.

PaperRoute can maintain managed local copies or intentional external file links depending on the workflow.

Externally linked files remain references to their original paths. If those files are moved outside PaperRoute, the external link may no longer resolve.

---

## Reminders and Calendar

Choose **Settings > Reminders & Calendar...**.

PaperRoute combines three reminder sources:

1. **Revision deadlines** recorded in the manuscript workflow.
2. **Submission follow-up dates** explicitly recorded on journal submissions; these remain active until you clear or change them.
3. **Custom reminders** that you create yourself.

The reminder list shows due date, status, type, manuscript, reminder title, and journal where relevant.

### Reminder status

PaperRoute uses the local calendar date to classify reminders as:

- Overdue
- Due today
- Upcoming

The calculation is deterministic: the same stored dates and same "today" date produce the same status.

### Custom reminders

Use **Add Reminder...** in the Reminders & Calendar window to create a manuscript-specific reminder.

Custom reminders can be edited or marked complete from the same window.

Revision and follow-up reminders come from their source records. To change one of those dates, edit the editorial decision or journal submission that owns it.

### Calendar export

Choose **Export Calendar (.ics)...** to create a portable iCalendar file containing active PaperRoute reminders.

The `.ics` file can be imported into calendar software that supports iCalendar, including Outlook, Google Calendar, and Apple Calendar.

Calendar export does not change manuscript or reminder data.

### Windows notifications

Windows reminder notifications are optional and disabled by default.

Enable them in **Settings > Preferences...**.

When enabled, PaperRoute checks active reminders when the application starts and can show a Windows notification for overdue, due-today, and near-term reminders.

You can configure how many days ahead count as near-term.

Important limitations:

- PaperRoute does not run a hidden cloud reminder service.
- If PaperRoute is not running, it cannot perform its startup reminder check.
- Windows may suppress or change how notification balloons are displayed.
- A Windows notification failure never prevents PaperRoute from opening or using the Reminders & Calendar view.

The in-app reminder list is the authoritative reminder view.

---

## Publication and CV Export

Choose **Data > Publication & CV Export...**.

You can filter the source records to:

- Published only;
- Accepted / In Press / Published; or
- All manuscripts.

You can then select individual records.

Output formats include:

- Plain text
- Markdown
- HTML

Output styles include a general publication list and a CV-oriented section.

PaperRoute uses structured authors when available and falls back to legacy author text when needed. Publication metadata such as year, journal, volume, issue, pages, DOI, publication URL, and preprint information is included when available.

Export never changes manuscript data.

---

## Spreadsheet Import and Export

Choose **Data > Import Spreadsheet...**.

PaperRoute supports three broad import paths.

### Standard PaperRoute workbook

Use **Data > Get Import Template...** for the supported workbook structure.

The workbook includes:

- Manuscripts
- Submissions
- Decisions
- Correspondence

This format exchanges the supported manuscript, submission, decision, and correspondence fields. It does not preserve the complete library: Version History, readiness profiles, submission packets, their fingerprints, and reviewer-response matrices are not included. Use a portable ZIP backup for complete library preservation.

### Legacy tracker

PaperRoute can recognize the original development tracker format when expected legacy columns are present.

### Map your spreadsheet

If the workbook is not recognized, PaperRoute can open a column-mapping workflow so you can map your own headings to PaperRoute fields.

Only Title is required.

### Exporting the library to Excel

Use **Data > Export Library to Excel...** to create a workbook from the current library.

---

## Backup and Restore

Choose **Data > Backup Library...** to create a portable ZIP backup.

A backup can contain:

```text
backup-info.txt
manuscripts.json
authors.json
library.xlsx
files\
```

Depending on the library, `authors.json` contains reusable authors, affiliations, and journals, including journal checklist templates. `manuscripts.json` preserves manuscript histories, readiness profiles, packet associations, saved fingerprints, and reviewer-response items with their decisions, revision rounds, statuses, drafts, and order.

Managed document copies, including version and packet snapshots, are included. Externally linked files remain references; retain those originals separately. The included Excel workbook is a partial human-readable export, not a replacement for the native JSON and managed files in the ZIP.

### Restore safety

**Data > Restore Backup...**:

1. validates the selected archive;
2. previews record/file counts;
3. asks for confirmation;
4. creates an emergency backup of the current library; and
5. restores the selected backup.

PaperRoute is intentionally conservative about restore operations because the manuscript library is the primary research-workflow record.

PaperRoute v0.5 uses **Schema 6**. Restore a backup made by v0.5 with **v0.5 or later**. Older restore code may ignore reviewer-response fields even though an older application refuses to open a Schema 6 library directly. Keep a separate pre-upgrade v0.4 ZIP backup if you need to return to v0.4; do not use a v0.5 backup for that rollback. See the [upgrade notes](../UPGRADE_NOTES.md).

---

## File Drawer

The File Drawer is for work you are not currently pursuing.

PaperRoute can suggest the File Drawer after a configurable number of rejections, but it does not automatically file the manuscript.

A filed manuscript can later be restored to the active Pipeline.

---

## Preferences, Updates, and Diagnostics

Choose **Settings > Preferences...** to configure:

- Light / Dark / Follow Windows appearance;
- Needs Attention thresholds;
- File Drawer suggestion threshold;
- reminder notification preferences;
- Stable or Preview update channel; and
- automatic update checking.

Theme changes currently take effect after restarting PaperRoute.

### Updates

Installed builds can check GitHub Releases for PaperRoute updates.

Stable installations default to the Stable channel. To opt into Preview, choose **Settings > Preferences... > Update channel**. Use **Settings > Check for Updates...** to check immediately.

Portable/developer builds are intended for development and smoke testing and do not behave exactly like an installed updater-enabled build.

### Diagnostics

Use **Settings > Diagnostics...** when troubleshooting storage, environment, or application-state problems.

---

# How Do I...?

## How do I add a manuscript I am currently writing?

1. Choose **Add Manuscript**.
2. Enter the title, or choose **Paste a Title Page...** (see below).
3. Use Draft as the stage if active writing has begun.
4. Optionally check **Remind me** and name a first deadline. It becomes an ordinary custom reminder under **Reminders & Calendar**.
5. Choose **Add Manuscript**, then open Manuscript Details to add or adjust structured authors and a target journal.

### Paste a title page

**Paste a Title Page...** reads a title page copied from Word or from LaTeX source (including the apa7, authblk, and REVTeX author commands) and proposes the title, authors in order with affiliations and the corresponding author, the abstract, and keywords. Superscript affiliation numbers that paste as plain digits, such as `Whitfield1,2*`, are recognized.

- Parsing happens on this computer. Nothing is sent anywhere.
- Review and edit everything before choosing **Use These Details**. Clear **Use** to leave an author out; separate an author's affiliations with semicolons.
- The **Library** column shows whether each author already exists in your reusable author library. Existing authors and affiliations are reused; new ones are created only when you add the manuscript.
- **Not placed** lists lines PaperRoute could not assign, such as a running head or an email address, along with anything to check, such as an affiliation number with no matching line. These lines are shown for review and are not saved.
- A pasted abstract and keywords only fill fields that are still empty.

## How do I add an already-published article?

You can add it manually, import it from ORCID, import it from BibTeX/RIS, or create it and enrich it with DOI/Crossref metadata.

For a manual record:

1. Add the manuscript.
2. Set the stage to Published.
3. Enter publication metadata.
4. Add structured authors if desired.
5. Save.

## How do I record a new journal submission?

1. Open Manuscript Details.
2. In Journal Submissions, choose **Add Submission**.
3. Enter the journal or choose **Use Library...**.
4. Enter the submission date and Journal manuscript ID if known.
5. Save the publisher portal URL if useful.
6. Optionally enable a follow-up date.
7. Save the submission, then choose **Save & Close** in Manuscript Details.

If you already prepared an unlinked packet, use its **Save & Go To... > Record Submission...** action instead to associate the packet with the new submission. For another revision round at the same journal, retain the existing submission and select that submission and round in the packet editor.

## How do I prepare a packet before submitting?

1. In Manuscript Details, open **Version History** and record the exact manuscript snapshot.
2. Open **Submission Readiness...**, choose **New from Journal...**, and review the copied requirements.
3. Use **Save & Go To... > Save & View Packets**, then create a packet linked to the intended version.
4. Add file records as managed copies, external links, or metadata only.
5. Optionally **Record Fingerprint** for each local file and use **Check Files** to compare contents before sending them.
6. Save the vault, then choose **Save & Close** in Manuscript Details.

This records preparation only. Record the journal submission after it actually occurs. A managed copy retains bytes when the manuscript is saved; a linked original can change independently of PaperRoute.

## How do I remind myself to check on a journal?

Use either method:

- Edit the journal submission and set a follow-up date; or
- open **Settings > Reminders & Calendar...** and add a custom reminder.

An explicitly saved submission follow-up remains active until you clear or change it, even if an editorial decision is later recorded.

## How do I record an R&R or revision request?

1. Open the relevant journal submission.
2. Add the editorial decision.
3. Choose the appropriate revision decision.
4. Record the decision date and revision deadline if one exists.
5. Save.
6. Confirm the manuscript is in the Revision stage.

The deadline will then appear in Reminders & Calendar and in the Needs Attention workflow as appropriate.

## How do I record reviewer comments and my response?

1. Open the existing journal submission and record the relevant editorial decision.
2. Choose **Reviewer Responses...**, then **Add Comment...**.
3. Select that decision, enter the revision round and reviewer label, and record a comment or action.
4. Add a draft response, manuscript location, notes, and the appropriate status, then choose **Save Comment**.
5. Choose **Save & Close** in the matrix, **Close** in Submission Details, and **Save & Close** in Manuscript Details.

Use **Export Markdown...** in the matrix for an editable response draft. Keep the original reviewer/editor letters and revised manuscript files as correspondence under the same submission. See [Reviewer Response Matrix](#reviewer-response-matrix) for ordering, export, and save behavior.

## How do I move a rejected paper to another journal?

1. Record the rejection under the old journal submission.
2. Keep the manuscript in the active Pipeline if you intend to reroute it.
3. Change the target journal.
4. Add a new journal submission when you resubmit.

Do not overwrite the old submission. The old submission is part of the manuscript's route.

## How do I put a manuscript in the File Drawer?

Open Manuscript Details and use the File Drawer workflow.

The File Drawer is reversible; it is not deletion.

## How do I attach an OSF project?

1. Open Manuscript Details.
2. Choose **Journal, Preprint & Links...**.
3. Under Related Web Links, choose **Add Link**.
4. Use a label such as `OSF Project`.
5. Enter the `https://` URL.
6. Save.

## How do I save a preprint?

Open **Journal, Preprint & Links...** and enter the preprint DOI and/or preprint URL.

## How do I make a CV publication list?

1. Choose **Data > Publication & CV Export...**.
2. Select Published only, or Accepted / In Press / Published if desired.
3. Choose the manuscripts to include.
4. Select CV section.
5. Choose Plain text, Markdown, or HTML.
6. Copy the preview or save the export.

## How do I move PaperRoute to another computer?

The safest approach is:

1. create a portable backup with **Data > Backup Library...**;
2. install PaperRoute on the destination computer;
3. use **Data > Restore Backup...**;
4. confirm manuscript counts and managed files before retiring the old installation.

## How do I recover from a bad import or restore?

PaperRoute creates safety backups around high-risk operations.

Do not overwrite or manually delete the PaperRoute data directory while troubleshooting. Use Diagnostics and the recovery/backup workflow first.

---

## Privacy and Data Locations

PaperRoute's core workflow is local-first.

Installed application data is stored under:

```text
%LocalAppData%\PaperRoute\
```

Visual Studio debugger sessions use the isolated development profile:

```text
%LocalAppData%\PaperRoute-Dev\
```

Development managed-file copies are stored separately from the stable managed library.

External services are used only for explicit features such as Crossref metadata lookup, public ORCID lookup, GitHub update checks, or links you choose to open.

PaperRoute does not require a PaperRoute account for the core manuscript library.

---

## Troubleshooting

### A window looks clipped or unusable

PaperRoute includes high-DPI and responsive-dialog support, but Windows display scaling can expose edge cases.

Try:

1. resize the dialog;
2. look for a scrollbar in content-heavy dialogs;
3. verify Windows display scaling;
4. capture a screenshot; and
5. report the window name and scaling level on GitHub.

Do not work around clipping by deleting or manually editing data files.

### A publisher or project link will not open

PaperRoute opens only valid `http://` or `https://` URLs.

Check that the stored link includes the full scheme, for example:

```text
https://example.org/path
```

### A reminder notification did not appear

Open **Settings > Reminders & Calendar...** first. If the reminder is present there, the stored reminder data is working.

Then check:

- notifications are enabled in PaperRoute Preferences;
- Windows has not suppressed application notifications;
- the reminder falls within the configured notification window.

Normal PaperRoute use does not depend on system notification delivery.

### The local User Guide did not load

PaperRoute ships a local copy of this guide. Open it from the **Help** button in the main header or **Settings > User Guide...**. If the local file cannot be read, the in-app Help window falls back to a short built-in Quick Start and can link to the GitHub copy when internet access is available.

---

## What Comes After v0.5?

The Route, Version History, readiness profiles, submission packets, and reviewer responses form one connected manuscript record. Later releases build on that record:

- **v0.6 — Deadline Center:** richer action and deadline management.
- **v0.7 — Route Analytics & Reports:** printable/archiveable manuscript-route reports and timing analytics.
- **v0.8 — Optional AI Assistance:** user-controlled clerical assistance that never becomes authoritative.
- **v0.9 — 1.0 Hardening:** onboarding/tutorial work, accessibility, responsive-layout refinement, and release certification.

The in-app `?` guidance is intentionally reusable so a future guided tutorial can teach the same concepts without maintaining a second vocabulary.

---

## Getting Help and Contributing

The repository is:

https://github.com/JUhalt/PaperRoute-Tracker

Bug reports, usability feedback, importer edge cases, and pull requests are welcome.

When reporting a problem, include:

- PaperRoute version;
- Windows version;
- display scaling if the issue is visual;
- whether you are using Stable, Preview, or a development build;
- the window/workflow involved; and
- screenshots or exact error text when possible.

Avoid posting manuscript content or private correspondence publicly unless you intentionally choose to share it.
