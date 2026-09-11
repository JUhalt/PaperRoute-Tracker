# PaperRoute User Guide

PaperRoute is a local-first academic manuscript tracker for researchers. It is designed to keep the complete route of a paper understandable: idea, writing, submission, peer review, revision, publication, or the File Drawer.

This guide is the user-facing source of truth for PaperRoute v0.3.

## Quick Start

If you only read one section, read this one.

1. Open PaperRoute and choose **Add Manuscript**.
2. Give the manuscript a title and place it at the stage that best matches reality.
3. Open **Manuscript Details** to add structured authors, a target journal, metadata, links, and submission history.
4. When you submit the paper, add a **Journal Submission** with the journal, date, Journal manuscript ID if available, portal URL, and optional follow-up date.
5. When the journal responds, open that submission and record the **Editorial Decision**. Revision decisions can carry a revision deadline.
6. Save decision letters, reviewer comments, response letters, revised manuscripts, and related correspondence under the appropriate submission.
7. Use **Settings > Reminders & Calendar...** to see revision deadlines, journal follow-ups, and custom reminders in one place.
8. Use **Data > Backup Library...** before major changes or moving PaperRoute to another computer.

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
- journal submissions;
- preprint and project links; and
- File Drawer information when relevant.

PaperRoute uses a working copy while the Manuscript Details window is open. Choosing **Cancel** discards unsaved changes from that window.

### Version History and the Route

**Version History** records meaningful manuscript snapshots without overwriting earlier files. A version may be:

- copied into the PaperRoute Library as an immutable historical snapshot;
- linked to an original file that remains under your control; or
- tracked as metadata only.

A version can be associated with the journal submission for which that exact file was sent. A later revised version can also be associated with the editorial decision that prompted it and with a revision-round number.

**Current Version** means the manuscript snapshot you currently consider your active working version. This is different from **Current State**, which means the manuscript's lifecycle position such as Submitted, Revision, Accepted, or Draft.

Use **View route →** from the main board to see the manuscript's deterministic publication history. The Route is read-only: double-clicking or opening a Route waypoint returns you to the authoritative record in Manuscript Details rather than creating a second editing pathway.

Deleting a Version History record is also working-copy based. If the version owns an immutable PaperRoute Library snapshot, the snapshot is removed only when **Save & Close** succeeds. Choosing **Cancel** leaves the saved version history and managed snapshot intact. Deleting a linked-file version never deletes the original external file.

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
- Favorite status; and
- Shortlist status.

A manuscript can link its target journal to one of these reusable records while retaining the free-text target-journal field for backward compatibility.

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

This is the preferred spreadsheet format for loss-minimized round-trip import/export.

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

Depending on the library, `authors.json` contains reusable authors, affiliations, and journals.

Managed document copies are included. Externally linked files remain references.

### Restore safety

**Data > Restore Backup...**:

1. validates the selected archive;
2. previews record/file counts;
3. asks for confirmation;
4. creates an emergency backup of the current library; and
5. restores the selected backup.

PaperRoute is intentionally conservative about restore operations because the manuscript library is the primary research-workflow record.

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
2. Enter the title.
3. Use Draft as the stage if active writing has begun.
4. Open Manuscript Details to add structured authors and a target journal.
5. Save and close.

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
7. Save the submission.

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

Open the journal submission and add correspondence/files for the reviewer comments, editor communication, revised manuscript, and response-to-reviewers materials.

PaperRoute v0.3 stores these records and files. A structured Reviewer Response Matrix is planned for v0.5.

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

## What Comes After v0.3?

The Route and Version History are the historical spine. Later releases build on that same record rather than creating parallel workflow systems:

- **v0.4 — Submission Readiness:** submission packets and per-journal readiness.
- **v0.5 — Reviewer Response Workflow:** structured reviewer/editor action items and response drafting.
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
