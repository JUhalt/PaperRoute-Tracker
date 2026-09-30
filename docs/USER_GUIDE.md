﻿# PaperRoute User Guide

PaperRoute is a local-first academic manuscript tracker for researchers. It is designed to keep the complete route of a paper understandable: idea, writing, submission, peer review, revision, publication, or the File Drawer.

This guide describes **PaperRoute v0.5.0 — Reviewer Response Workflow**. See the [v0.5.0 release notes](releases/0.5.0.md) for the changes and the [repository homepage](../README.md) for current release availability.

## Quick Start

If you only read one section, read this one.

1. Open PaperRoute. An empty library opens on a welcome: choose **+ Add Manuscript** for a paper you are working on, or **Import Existing Work** to bring in published work from ORCID, BibTeX or RIS, or a spreadsheet.
2. Give the manuscript a title and place it at the stage that best matches reality.
3. Open the manuscript from its card to add structured authors, a target journal, metadata, links, and submission history.
4. Before submitting, record the exact manuscript snapshot in **Version History**, use **Submission Readiness...** for the journal's checklist, and assemble **Submission Packets...** for the files you intend to send.
5. When you actually submit the paper, use the packet's **Record Submission...** action or add a **Journal Submission** with the journal, date, Journal manuscript ID if available, portal URL, and optional follow-up date.
6. Save each focused window, then choose **Save** in the manuscript page's **Unsaved changes** bar (or press **Ctrl+S**) to keep the complete workflow.
7. When the journal responds, open that submission and record the **Editorial Decision**. Revision decisions can carry a revision deadline.
8. Select that submission on the **Submissions** tab and open its **Reviewer Responses** tab to track individual comments, actions, and response drafts, then choose **Save** on the manuscript page. Keep original letters and revised files under the submission's correspondence.
9. Open **Deadlines** in the left rail to see revision deadlines, journal follow-ups, your reminders, and unfinished submission preparation in one place.
10. Use **Settings > Backup Library...** before major changes or moving PaperRoute to another computer.

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

### Finding your way around

PaperRoute opens in one window. The left rail lists its pages: **Board**, **Library**, **Journals**, **Deadlines**, **Insights**, and **Import & Export**, with **Settings** and **Help** at the bottom. Press **Ctrl+1** to **Ctrl+6** to open the pages in that order, **Alt+Left** and **Alt+Right** to go back and forward, and **F1** for this guide.

For more room, choose the **«** button beside the PaperRoute name to collapse the rail to icons; hover over an icon to see its page, and choose **»** to expand the rail again. PaperRoute remembers your choice. Double-click the logo for **About PaperRoute**.

Earlier versions reached these places through menus. Every command is still available:

| Earlier location | Now |
| --- | --- |
| Data > Import Spreadsheet..., Import BibTeX / RIS..., Get Import Template... | **Import & Export** page |
| Data > Export Library to Excel..., as BibTeX..., as RIS..., Publication & CV Export... | **Import & Export** page |
| Data > Backup Library..., Restore Backup... | **Settings** in the rail, and the **Import & Export** page |
| Data > Authors & Affiliations... | **Library** page, **Authors & Affiliations** |
| Data > Journal Library... | **Journals** page |
| Settings > Reminders & Calendar... | **Deadlines** page |
| The **Reminders** page (v0.6) | **Deadlines** page |
| Settings > Preferences..., Check for Updates..., Diagnostics..., About | **Settings** in the rail |
| Settings > User Guide... and the Help button | **Help** in the rail, or **F1** |

The **Library** page lists every manuscript on every shelf in a sortable table; press Enter or double-click a row to open it. Type in its filter box to narrow the table by title, journal, stage, or route.

An empty list or table says what belongs there and how to add the first item.

### The Needs Attention area

The main board can flag manuscripts that may need action, including:

- overdue revision deadlines;
- revision deadlines approaching within your configured warning window;
- unusually long reviews;
- missing target journals; and
- recent rejections.

These are prompts, not automatic decisions. PaperRoute does not move manuscripts or file them simply because a threshold was reached.

Only items that currently apply are listed, each as a chip with its count. Click a chip, or Tab to it and press Enter or Space, to show just those manuscripts; activate it again to clear the filter.

### Board shelves and cards

Pipeline, Published, and File Drawer are tabs with counts; one shelf is shown at a time, with its cards in as many columns as the window fits. Each card shows:

- the stage and, for active work, what needs attention now (such as "Revision due in 9 days") or how long the manuscript has been in its stage;
- the title and target journal; and
- the route so far: one dot per journal submission (grey when it ended in a rejection or withdrawal, an accent ring for the current submission, a filled accent dot when accepted) and a short summary such as "2nd journal · major revision". The summary uses recorded submissions and decisions only.

Click a card, or Tab to its title and press Enter, to open it. **View route →** opens the Route. Move to File Drawer, Restore to Pipeline, and Delete are in the card's **⋯** menu, which also opens on right-click or with the keyboard's context-menu key.

### Search, filters, and sorting

The main board supports search, stage filtering, and sorting. Press **Ctrl+F** to jump to search. Search includes manuscript information and structured author metadata where available.

---

## The manuscript page

Click a card on the board, or a row in the Library, to open the manuscript as a page in the main window. **← Board** (or **Alt+Left**) returns to where you opened it. The header shows the title, stage, target journal, and route so far, with **Copy Citation**, **View Route**, and **Delete Manuscript...** in the **⋯** menu.

The page's tabs are **Overview** (title, target journal, stage, revision deadline, metadata, links, File Drawer details, and the linked journal's notes from the Journal Library), **Authors**, **Versions**, **Submissions**, and **Readiness & Packets**. Together they cover:

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

PaperRoute edits a working copy while the page is open. Every tab, and every focused window opened from one (readiness, packets, submissions, reviewer responses), changes that working copy. As soon as anything differs from the saved manuscript, an **Unsaved changes** bar appears at the bottom of the page:

- **Save** (or **Ctrl+S**) stores the manuscript; managed-file copies and deletions happen only when this save succeeds.
- **Discard** returns the page to the last saved version.

If you go to another page, open another manuscript, or close PaperRoute while there are unsaved changes, PaperRoute asks whether to save them, discard them, or stay. Nothing is lost silently.

### Version History and the Route

**Version History** records meaningful manuscript snapshots without overwriting earlier files. A version may be:

- copied into the PaperRoute Library as an immutable historical snapshot;
- linked to an original file that remains under your control; or
- tracked as metadata only.

A version can be associated with the journal submission for which that exact file was sent. A later revised version can also be associated with the editorial decision that prompted it and with a revision-round number.

**Current Version** means the manuscript snapshot you currently consider your active working version. This is different from **Current State**, which means the manuscript's lifecycle position such as Submitted, Revision, Accepted, or Draft.

Use **View route →** from the main board to see the manuscript's deterministic publication history. The Route is read-only: double-clicking or opening a Route waypoint returns you to the authoritative record on the manuscript page rather than creating a second editing pathway. **View Route** in the page header opens the same view.

Once a manuscript has a recorded submission, the Route opens on its **route map**: the whole route drawn to scale, from the first submission to publication (or to today, while it is under way). Each stretch is colored by who held it:

- **With the journal** (teal): from a submission or resubmission to the journal's decision.
- **With you** (amber): revising, from a revision request to the recorded return to review, and rerouting, from a rejection to the next submission.
- **In production** (slate): from acceptance to the publication date.
- **Not recorded** (hatched): time the record does not assign, such as a revision whose resubmission date was never entered. It is shown as such, never estimated. To split it, record the return to review in the manuscript's history.

The journals are named above their stretches, and every event after the first submission is numbered on the route and explained underneath: "Desk rejected, Jan 12: 7 days after submission", "Resubmitted, Jun 10: after 69 days of revising". Totals across the top say how many days the route spent with journals, with you, and in production. The same map is useful for teaching: it shows at a glance how much of publishing is waiting and how much is work.

Deleting a Version History record is also working-copy based. If the version owns an immutable PaperRoute Library snapshot, the snapshot is removed only when **Save** succeeds. Choosing **Discard** leaves the saved version history and managed snapshot intact. Deleting a linked-file version never deletes the original external file.

### Submission Readiness and Packets

These features were introduced in PaperRoute v0.4.0.

On the manuscript's **Readiness & Packets** tab, open **Submission Readiness...** to apply a reusable journal checklist and track manuscript-specific requirements. Complete, not-applicable, and unresolved states explain the readiness summary. Readiness is advisory: it does not change the manuscript stage or prevent recording a real submission.

Choose **New from Journal...** to copy that journal's current template into a new readiness profile. Later template edits do not rewrite existing profile wording or progress. **Add New Template Requirements** adds requirements that are new to the selected profile while retaining its existing states and notes.

Open **Submission Packets...** to assemble file records tied to an exact **Version History** entry. You can optionally associate a packet with an existing journal submission and revision round. Preparing a packet does not record a submission. Managed copies become separate snapshots when the manuscript is saved; external links continue to point to the original files. Metadata-only entries contain no file to check.

#### Move between related records

In readiness, **Save & Go To... > Save & View Packets** opens the packets linked to the selected profile. **Open Submission Portal** uses that profile's linked journal when it has a web portal recorded.

In the vault, **Save & Go To...** can return to the manuscript page, select the packet's exact version, or open its linked readiness profile or actual submission. **Submission Packets...** in Version History shows packets for the selected version; **View Submission Packets...** under a selected submission shows packets for that submission. The vault explains which records it is showing, and **Show all packets** removes that filter. New packets inherit the selected context, with choices visible in the packet editor.

For an unlinked preparation packet, choose **Save & Go To... > Record Submission...** and complete **Record Journal Submission**. Cancel returns to the prepared packet without recording an event. Adding the submission associates that packet with the new record and applies the usual manuscript-stage rules. Unresolved readiness and an empty file list do not prevent recording what actually happened. For a revision round of an existing submission, edit the packet and select the existing submission and round instead of recording a duplicate submission.

These navigation actions save the current window into the manuscript page's working copy. Choose **Save** on the manuscript page to keep the full workflow; **Discard** there drops its unsaved changes. Version records retain their existing submission/decision history: the same exact version can appear in separate packets for different interactions. Packet navigation shows each packet's own associations without rewriting that earlier history.

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

Choose **Save & Close** in the vault and then **Save** on the manuscript page to keep newly recorded fingerprints and packet edits. **Discard** on the page drops those unsaved changes. Managed-file copies and portable backup/restore preserve the saved fingerprints; they do not silently record a new baseline. A packet-linked version or submission must be unlinked from the packet, retargeted where appropriate, or have the packet removed before that referenced record can be deleted.

### Legacy co-author text

Older or imported records may still contain free-text co-author information. PaperRoute preserves that text rather than silently parsing or replacing it.

Structured authors are the preferred workflow for reusable people, affiliations, ordering, and ORCID information.

---

## Reusable Authors and Affiliations

Open **Library** in the left rail and choose **Authors & Affiliations** to manage reusable people and institutions.

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

### Check for publications

Papers are sometimes published before you update PaperRoute. Choose **Check for Publications...** on the **Import & Export** page, or **⋯ > Check for Publication...** on a manuscript page, to ask whether work you are tracking has appeared.

Manuscripts that have gone to a journal are checked by default; check or uncheck any others. PaperRoute then looks, one manuscript at a time:

1. by the manuscript's **DOI**, when it now resolves to a published article;
2. by its **preprint DOI**, when Crossref links the preprint to its published version;
3. by **title**, in Crossref, allowing for case, punctuation, and an added subtitle; and
4. optionally, in an **ORCID record**: the iD of the author marked "This is me" is filled in for you.

Each result reads "A publication matching this manuscript may have appeared" with **Review Match** (opens the article's page), **Mark Published...**, and **Ignore**. Results you leave stay on the **Deadlines** page under **No date** until you choose.

- **Mark Published...** shows exactly what will change, then moves the manuscript to the Published shelf with the stage Published from the publication date, adds a history entry naming the journal and DOI, updates the journal if it differs, and fills empty publication fields. Fields that have a value, and your recorded submissions and decisions, are left as they are.
- **Ignore** remembers the answer, so later checks never show that match again.

Nothing is checked in the background. If you are offline or Crossref asks PaperRoute to slow down, the check says which manuscripts it could not check and changes nothing.

### Fill blanks from Crossref

**Fill Blanks from Crossref...** on the **Import & Export** page looks up every manuscript that has a DOI and lists the empty fields Crossref can fill: journal, publisher, date, volume, issue, pages, URL, abstract, and keywords. Uncheck anything you do not want, then choose **Fill**. A field that already has a value is never listed or changed.

---

## ORCID Public-Profile Import

Open **Library > Authors & Affiliations**, select an author, and use the ORCID workflow. **Import & Export > ORCID Works...** and the welcome's **ORCID works** open the same place.

PaperRoute can read public ORCID information for a user-supplied ORCID iD, including public identity information, public employment/affiliation data, and public works.

Important boundaries:

- the lookup is user initiated;
- PaperRoute stores no ORCID password;
- PaperRoute stores no ORCID OAuth token or client secret;
- a successful public lookup confirms that the iD exists in the public registry, not that the record holder authenticated to PaperRoute.

When importing public works, dated works can be placed directly on the Published shelf only when you explicitly choose that behavior. Undated works remain Ideas. Duplicate protection uses DOI first and exact title second.

---

## BibTeX and RIS

Choose **Import & Export > Import BibTeX / RIS...** to bring bibliography records into PaperRoute.

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

- **Import & Export > Export Library as BibTeX...**
- **Import & Export > Export Library as RIS...**

You choose which manuscripts to export.

---

## Journal Library, Portals, Preprints, and Project Links

Open **Journals** in the left rail to maintain reusable journal information. Changes there are saved as you make them.

Journal records can include:

- journal name;
- publisher;
- ISSNs;
- journal homepage;
- aims and scope, author instructions, and editorial board links;
- submission portal;
- facts and metrics (see **Journal Facts and Metrics**);
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

## Journal Facts and Metrics

The **Journals** page shows the selected journal beside the list: its facts, links, and metrics, each with where it came from.

### Looking up a journal

Choose **Look Up Facts...** to look the journal up in two open indexes:

- **DOAJ** (the Directory of Open Access Journals, doaj.org), which lists fully open-access journals: the publication fee (the highest it lists), license and copyright, peer review type, typical weeks from submission to publication, plagiarism screening, and links to the journal's aims and scope, author instructions, and editorial board.
- **OpenAlex** (openalex.org), an open index of scholarly works that covers most journals, open access or not: publisher, ISSNs, whether the journal is fully open access, its main topics, and three open citation metrics.

PaperRoute sends the journal's ISSNs (the ones you saved, and any other ISSN OpenAlex lists for the same journal), and nothing else. A journal without an ISSN is first found by name: type the name, choose **Find**, and pick the journal from the list; PaperRoute then sends that journal's ISSNs, or its OpenAlex id if it has none. You see everything that was found before anything is saved:

- a blank field is filled;
- a field you typed is never replaced (**Yours is kept**);
- a fact from an index is updated when the index changed it; and
- a fact an index no longer gives is offered for removal, unchecked.

Choose **Refresh Facts...** later to check again. Facts checked more than a year ago are marked as possibly out of date. While **Work offline** is on, or Journal facts is turned off in Online services, the page shows what was saved last time, with its date.

A subscription journal isn't listed in DOAJ, which is expected. For such a journal, a fee from OpenAlex is shown as optional, for making an article open access.

The facts are data from DOAJ and OpenAlex, both public domain (CC0). OpenAlex asks to be cited as: Priem, J., Piwowar, H., & Orr, R. (2022). OpenAlex: A fully-open index of scholarly works, authors, venues, institutions, and concepts. ArXiv. https://arxiv.org/abs/2205.01833. PaperRoute isn't affiliated with or endorsed by either index.

### Links and sharing policies

The card links to the journal's homepage, aims and scope, author instructions, editorial board, and submission portal, and to its sharing policy in **Open Policy Finder** (openpolicyfinder.jisc.ac.uk), which says whether and where you may share a preprint or accepted manuscript. Links open in your web browser; PaperRoute itself reads nothing from those pages. Editors and detailed submission requirements change often, so record what you need in the journal's notes or its checklist.

### Journal metrics

Metrics appear in two groups, always with their name and source. Metrics you enter show their year; OpenAlex's are as of the date the card says they were checked.

- **From open data (OpenAlex):** 2-year mean citedness (citations received last year by works the journal published in the two years before, divided by the number of those works; similar in idea to the Journal Impact Factor, but not the same number), and the journal's h-index and i10-index across all years.
- **Entered by you:** metrics that have no open source, such as the Journal Impact Factor, CiteScore, SJR, SNIP, or h5-index, and acceptance rates or decision times a journal publishes. Open **Edit... > Facts and Metrics** and choose **Add Metric...**; each metric shows its definition and where it is published. PaperRoute doesn't look these up.

| Metric | Published by | What it measures |
| --- | --- | --- |
| Journal Impact Factor | Clarivate, Journal Citation Reports | Citations in one year to items from the two years before, per item |
| 5-year Journal Impact Factor | Clarivate, Journal Citation Reports | The same over five years |
| CiteScore | Elsevier, Scopus | Citations over four years to documents from those four years, per document |
| SJR | SCImago | Weighted citations in one year to documents from the three years before, per document |
| SNIP | CWTS, Leiden University | Citations per paper over three years, weighted for how much each field cites |
| h5-index | Google Scholar Metrics | The largest h with h articles from the last five years cited h times each |

Journal metrics describe a journal as a whole, not the quality of any single article or the work of any author, as the San Francisco Declaration on Research Assessment (https://sfdora.org/read/) explains. PaperRoute never combines metrics into one score, and never sorts or colors journals by them.

---

## Choosing a Journal

Each manuscript keeps a **journal shortlist** on its Overview: the journals you are considering, in your order of preference, with your reasons.

### Your shortlist

- **Add Journal...** asks for the journal (suggested from your Journal Library and past submissions), its status (**Considering**, **Preferred**, **Backup**, or **Ruled out**), and why you are considering it. Your own history with the journal, from the Insights page, appears as you choose it.
- Use **⋯** beside a journal to move it up or down, make it the target journal, or remove it. The number beside each status is its place in your order.
- Each journal shows whether you have submitted to it and how that ended, read from the manuscript's own submissions.
- When a submission is rejected or withdrawn while the manuscript is still in the Pipeline, the shortlist offers the next journal: your first **Preferred** journal not yet tried, then **Considering**, then **Backup**. **Make It the Target Journal** changes only the target journal; nothing else changes until you record the next submission.

The shortlist is saved with the rest of the manuscript page, and like any change it waits for **Save**.

### Is it a trusted journal?

Before submitting, check that the journal is one you can trust. These questions are adapted from the Think. Check. Submit. checklist for journals, a cross-industry initiative (https://thinkchecksubmit.org/journals/), licensed CC BY 4.0 (https://creativecommons.org/licenses/by/4.0/):

- Do you or your colleagues know the journal, and have you read its articles?
- Can you easily identify and contact the publisher?
- Is the journal clear about the type of peer review it uses?
- Are its articles indexed and archived in services you use?
- Is it clear what fees will be charged, and for what?
- Does it give clear guidelines for authors?
- Does the publisher belong to recognized industry initiatives, such as COPE, DOAJ, or OASPA?

Think. Check. Submit. suggests submitting only if you can answer yes to most of these questions.

### Is it a good fit?

Then ask whether the journal suits this manuscript:

- Do its aims and scope cover this work?
- Does it publish this type of article, within its length limits?
- Does it reach the readers you want to reach?
- Do its open-access options and fees work for you and any funder requirement?
- Does its preprint and sharing policy suit your plans?
- Does its time to decision and publication suit your timeline? Your own history with the journal, on the Insights page, helps here.

Each shortlisted journal keeps your answers as checks, so the reasons for your choice stay with the manuscript and carry over if it is rerouted. PaperRoute never scores or ranks journals for you; the judgment stays yours.

### Finding journals that publish work like this

Not sure where to start? **Find Journals...** on the shortlist looks for journals that recently published articles like this one, using OpenAlex, an open index of published research.

1. **Review the keywords.** PaperRoute proposes the manuscript's keywords (checked) and phrases from its title (unchecked). Check the ones that describe the work, or add your own. Choose whether articles must match every keyword or any keyword, and how many years back to look.
2. **See what will be sent.** Before searching, the window shows the exact request: the keywords you checked and a start date, and nothing else. Your title, abstract, notes, and files are never sent. **Show the exact web address** shows the address itself.
3. **Read the evidence.** Each journal found shows how many matching articles it published, out of all its articles in those years, whether it is open access and its listed fee, your own history with it, and up to three recent matching articles, linked by DOI.
4. **Add the ones worth a look.** **Add Checked to Shortlist** adds them as **Considering**, with the evidence, and nothing else changes. Journals already on the shortlist can't be added twice. Save the manuscript page to keep them.

OpenAlex searches the titles, abstracts, and full text of articles, so a match means an article mentions your keywords, not that it is about the same topic. Counts favor large journals, and they are evidence to read, never a ranking of quality or of your chances. PaperRoute never scores journals or labels any as predatory; the questions above are there for that judgment.

Without a key, OpenAlex allows a small amount of use each day and sometimes asks keyword searches to wait when it is busy. PaperRoute says how long, and a free OpenAlex key, added in **Settings > Preferences... > Online services**, raises the allowance.

When a shortlisted journal has facts on the **Journals** page, its row shows them in one line, and the questions show what's known beside them, such as its peer review type beside the peer review question, with links to its aims and scope, author instructions, and sharing policy. The questions are never answered for you.

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

The follow-up date appears on the **Deadlines** page whenever it is explicitly set. PaperRoute treats it as a user-owned reminder, so recording an editorial decision does not silently remove it; clear or change the follow-up date when it is no longer useful.

### Reusing the Journal Library

When recording a submission, choose **Use Library...** to select a reusable journal. PaperRoute can populate the journal and standard portal URL.

A submission can still retain its own deeper or manuscript-specific portal URL.

---

## Editorial Decisions and Revision Deadlines

Inside a journal submission, record editorial decisions as they arrive.

Decision records preserve the journal-specific history rather than reducing everything to a single current status.

Revision decisions can include a revision deadline. When the manuscript is in the Revision stage, that deadline appears in the reminder workflow.

PaperRoute's reminder view is derived from the stored editorial-decision deadline; it does not create a second hidden copy of the revision date.

For discoverability, the manuscript's **Overview** tab also shows a **Revision deadline** row. **Set / Edit...** opens the latest submission's editorial decision so you can record or change the deadline without hunting through the submission tabs. If the manuscript has no journal submission yet, PaperRoute explains that a submission must be recorded first.

---

## Reviewer Response Matrix

On the manuscript's **Submissions** tab, the list of journal submissions sits beside the selected submission. Its **Editorial History**, **Reviewer Responses**, and **Correspondence & Files** tabs show everything that belongs to it. Record the editorial decision under **Editorial History**, then track the reviewers' requests under **Reviewer Responses**. Keep a normal revision within the existing journal submission; use **Record Submission...** only when you actually send the manuscript to a journal again.

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

1. Choose **Save Comment** in the item editor. The comment joins the manuscript's unsaved changes.
2. Choose **Save** in the manuscript page's **Unsaved changes** bar (or press **Ctrl+S**) to store the manuscript permanently.

**Cancel** in the item editor discards that comment's edits. **Discard** on the manuscript page drops all its unsaved changes, including added, edited, removed, and reordered comments.

### Export an editable response draft

Choose **Export Markdown...**, review or edit the preview, then choose **Save Markdown...** to save a file. The preview opens with the cursor at the start, so typing never replaces the draft. The Markdown reads as plain text: only characters that would change how it renders, such as a leading "#" or "-", or asterisks, are escaped with a backslash. The export includes the submission context, decision and revision round, reviewer labels, comments, actions, statuses, responses, locations, and working notes in stored order. The status filter does not limit the export: it includes the complete matrix. Review the notes before sharing the draft with a journal.

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

## Deadlines

Open **Deadlines** in the left rail, press **Ctrl+4**, or choose **View in Deadlines →** beside Needs Attention on the Board. The rail shows how many items are overdue or due today.

Deadlines lists everything that needs action, grouped by when:

- **Overdue**, **Today**, **Next 7 days** (counted from today), and **Later** for dated items;
- **No date** for work that is under way without a date, such as a revision whose decision records no deadline, or a submission packet whose checklist still has required items open;
- **Done in the last 30 days**, folded away until you choose **Show**.

Four kinds of item appear, each from the record that owns it:

1. **Revisions**: the deadline on the latest editorial decision. A revision with reviewer comments shows how many are addressed.
2. **Follow-ups**: follow-up dates recorded on journal submissions. They remain until you clear or change them; recording a decision does not remove them.
3. **Reminders**: your own reminders.
4. **Preparation**: a submission packet not yet linked to a submission whose readiness checklist has required items open.

The counts across the top ignore the filters. Use the chips to show one kind, or type in the filter box to narrow by manuscript, journal, or title. Clicking the active chip shows everything again.

### Acting on an item

Each row has its main action as a button and the rest under **⋯**. With a row selected, **Enter** runs its first action and **Up**/**Down** move between rows.

| Item | Actions |
| --- | --- |
| Revision | **Open** (lands on the submission's reviewer responses), **Postpone...**; without a deadline, **Set Deadline...** |
| Follow-up | **Open** (lands on the submission), **Postpone...**, **Clear Follow-up...** |
| Reminder | **Done**, **Postpone...**, **Edit Reminder...**, **Open Manuscript** |
| Preparation | **Open Readiness** (the manuscript's Readiness & Packets tab) |

**Postpone** offers tomorrow, one week, two weeks, or any date, and its button says exactly what will happen, for example **Postpone to Oct 4**. It changes the date on the record that owns the item (the editorial decision, the journal submission, or the reminder) and saves, so the manuscript page always shows the same date. Nothing is copied: Deadlines is a view of your records, not a second list to keep in sync.

**Add Reminder** creates a manuscript-specific reminder.

### Calendar export

Choose **Export Calendar...** to save the dated items as a portable iCalendar (`.ics`) file. Undated work has no calendar date and is left out.

The `.ics` file can be imported into calendar software that supports iCalendar, including Outlook, Google Calendar, and Apple Calendar. Export does not change your library.

### Windows notifications

Windows reminder notifications are optional and disabled by default.

Enable them in **Settings > Preferences...**.

When enabled, PaperRoute checks active reminders when the application starts and can show a Windows notification for overdue, due-today, and near-term reminders.

You can configure how many days ahead count as near-term.

Important limitations:

- PaperRoute does not run a hidden cloud reminder service.
- If PaperRoute is not running, it cannot perform its startup reminder check.
- Windows may suppress or change how notification balloons are displayed.
- A Windows notification failure never prevents PaperRoute from opening or using the Deadlines page.

The Deadlines page is the authoritative view.

---

## Work Types and Tags

Every manuscript can have a **type** and any number of **tags**, on the **Type and tags** card of its Overview tab.

- **Type** says what kind of work it is: journal article, preprint, conference paper, conference abstract, poster, book chapter, thesis or dissertation, or other. A new manuscript has no type until you choose one; PaperRoute never guesses. Imports set the type when the source says it (a BibTeX entry type, an RIS `TY` line, an ORCID work type, or a Crossref record), and Crossref never replaces a type you chose.
- **Tags** are your own words, such as a grant, a lab project, or a student. Type a tag and press **Enter** (or a comma); tags already used in the library are suggested. Remove one with its **×**, or select it and press **Delete**. Right-click a tag to choose its color; otherwise its color comes from its name, so the same tag always looks the same.

Tags appear on board cards beside the journal, and in the Library's **Type** and **Tags** columns. Search the board or filter the Library by a tag or type; clicking a card's tags searches the board for the first one. **Publication & CV Export** can **Group by type**, with sections such as Journal articles and Conference papers. The Excel workbook keeps both in its **WorkType** and **Tags** columns.

Types and tags never change a manuscript's stage, shelf, or route.

## Insights and Reports

Open **Insights** in the left rail (Ctrl+5) to see how your work has moved through journals. Everything is calculated on this computer from your own PaperRoute records, each time you open the page, and nothing is changed. Nothing is sent anywhere, except when you choose **Update from OpenAlex...** on **Your Citations**.

Across the top: how many manuscripts and submissions you have, the median time to a first decision, and the median time from first submission to acceptance. Each median says how many records it is based on.

**Your Journals** lists every journal you have submitted to, with submissions, acceptances, requests to revise, rejections after review, desk rejections, the median days to a first decision, the median days in review (leaving out desk rejections), and when you last submitted. Press **Enter** or double-click a journal to see the manuscripts you sent there. A journal selected on the **Journals** page shows the same history in one line.

**Your Routes** lists every manuscript that has been submitted: its route, how many journals and revision rounds it took, and the days from first submission to acceptance and to publication. Press **Enter** or double-click to open the manuscript.

**Route Map** lines up every published route at day 0 on one scale, shortest first, colored as in a manuscript's route map. Above it: the median days from first submission to publication, and the share of all those days spent with a journal, with you (revising or rerouting), and in production. **Include work not yet published** adds manuscripts still under way (drawn to today, with a dotted end and a + on their day count) and filed ones (drawn to their last decision). Click a route, or select it with the arrow keys and press **Enter**, to open its route map. With a journal chosen on **Your Journals**, the map shows only the manuscripts sent there.

**Your Citations** shows how your published work has been cited, from OpenAlex, an open index of scholarly works. It needs your ORCID iD: in **Library > Authors & Affiliations**, edit your own record, check **This is me**, and add your iD. Opening the tab sends nothing; it shows the figures you saved last time and when they are from.

**Update from OpenAlex...** first says what will be sent. It reads the works on your public ORCID record from ORCID and looks their DOIs up in OpenAlex, with the DOIs of your published manuscripts. Titles and abstracts are never sent. The option to also ask OpenAlex which other works it links to your iD sends your iD to OpenAlex too; it can find works missing from your ORCID record, but some may be someone else's. Before anything is saved, you check the works that are yours, and the figures update as you check. Versions of one work on your ORCID record count once, and a work you leave unchecked stays unchecked next time.

Each figure is shown on its own, with its definition, and never combined into a score:

- **Citations**: the sum of your checked works' citations in OpenAlex.
- **h-index**: you have h works cited at least h times each.
- **i10-index**: the number of your works cited at least 10 times.
- **g-index**: your most-cited g works together have at least g² citations.
- **m-quotient**: your h-index divided by the years since your first publication, counting the first year.
- **Citations by year**: the last ten years, with this year marked "so far". OpenAlex counts citations by year from 2012.
- For each of your manuscripts in PaperRoute with a DOI: its citations, its field-weighted citation impact (FWCI; 1.0 is average for work of the same type, year, and field, and it is provisional for work under four years old), its citation percentile, and how many journals it went to. Press **Enter** or double-click to open the manuscript.

Citation counts differ between databases: OpenAlex, Scopus, Web of Science, and Google Scholar will not agree. They count citations; they don't measure the quality of the work. Your saved citations are kept in citations.json in PaperRoute's data folder and included in portable backups.

How the numbers work:

- Days are calendar days between dates recorded in PaperRoute. A first decision is the earliest decision recorded for a submission.
- A missing or inconsistent date (a decision dated before its submission, for example) leaves that value out. Nothing is estimated.
- On the route maps, time with you after a revision request ends at the next recorded Submitted or Under Review history entry. Without one, that stretch is drawn as not recorded.
- Submissions count under a Journal Library record when they are linked to it or use exactly its name (ignoring capitals and spacing). A different spelling is shown on its own line rather than merged by guess.

### Reports to share

- **Pipeline Report...** on the Insights page makes a read-only summary for a supervisor, mentor, or coauthor: each chosen manuscript's title, stage, journal, route, time in stage, and next deadline. Choose the manuscripts (pipeline work is chosen by default), preview the report, and save it.
- **Route Report...** in a manuscript page's **⋯** menu shows one manuscript's full route: every submission, its decisions and dates, and the durations between them, with the definitions used.

Reports are saved as a single web page that opens in any browser, where you can print it or save it as PDF. They never include notes, correspondence, reviewer comments or responses, file paths, or contact details, and a reminder appears only as "Reminder", never by its title.

## Teaching with PaperRoute

PaperRoute includes an **example library**: the fictional Example Lab's manuscripts, covering the routes a new researcher needs to see. Open it from the empty-library welcome (**Explore an example library**) or from **Import & Export → Explore an Example Library...**.

The example opens in a separate window with a banner across the top. It has its own temporary storage: your own library is never opened, anything you change in the example is discarded when its window closes, and it never checks for updates or shows notifications. **Return to My Library** closes it. Its dates are relative to today, so Deadlines always has something overdue, due today, and coming up.

### A 15-minute walk through the route

1. **The board (2 minutes).** Three shelves, and **Needs Attention**: a revision due soon, a review waiting more than 90 days, a recent rejection. Each card's footer is its route so far.
2. **One whole route (3 minutes).** Open *Example: anchoring effects in clinical risk estimates* and choose **View Route**. The route map shows 264 days from first submission to publication: a desk rejection after 7 days, 48 days of rerouting, two rounds of revision, and 28 days in production. Ask: how much of it was waiting on journals (105 days), and how much was the authors' own work (131 days)?
3. **Responding to reviewers (3 minutes).** Open *Example: retrieval practice in an introductory statistics course*, then its submission's reviewer responses: four comments at four stages of response, and a revision deadline ahead.
4. **After a rejection (2 minutes).** Open *Example: measurement invariance of a short grit scale*. Its journal shortlist offers the next journal, with the trust and fit questions answered for each. **How to choose a journal** explains them.
5. **What needs action (2 minutes).** The **Deadlines** page: an overdue follow-up, a reminder due today, and the revision deadline.
6. **The patterns (2 minutes).** **Insights**: turnaround with each journal, and the **Route Map** of every published route.
7. **The File Drawer (1 minute).** *Example: a null result on priming and choice*: three rejections and a reason for setting it aside. Setbacks are part of every researcher's record.

## Publication and CV Export

Choose **Import & Export > Publication & CV Export...**.

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

Choose **Import & Export > Import Spreadsheet...**.

PaperRoute supports three broad import paths.

### Standard PaperRoute workbook

Use **Import & Export > Get Import Template...** for the supported workbook structure.

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

Use **Import & Export > Export Library to Excel...** to create a workbook from the current library.

---

## Backup and Restore

Choose **Settings > Backup Library...**, or **Backup Library...** on the Import & Export page, to create a portable ZIP backup.

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

**Settings > Restore Backup...** (also on the Import & Export page):

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
- Stable or Preview update channel;
- automatic update checking; and
- online services, Work offline, and an optional OpenAlex key (see **Online Services and Working Offline**).

Theme changes currently take effect after restarting PaperRoute.

### Updates

Installed builds can check GitHub Releases for PaperRoute updates.

Stable installations default to the Stable channel. To opt into Preview, choose **Settings > Preferences... > Update channel**. Use **Settings > Check for Updates...** to check immediately.

Portable/developer builds are intended for development and smoke testing and do not behave exactly like an installed updater-enabled build.

### Diagnostics

Use **Settings > Diagnostics...** when troubleshooting storage, environment, or application-state problems.

## Online Services and Working Offline

PaperRoute keeps your library on this computer and works without the internet. A few features contact an online service, and only when you use them. Choose **Settings > Preferences... > Online services** to see each service, what it sends, and when, and to turn any of them off.

- **Work offline** stops every online service, update checks included, until you turn it off. Choose **Settings > Work Offline**, or check it in Online services.
- Above **Settings**, the rail shows **Online** in green, or **Working offline** in blue. Choose it to review Online services.
- Turning off one service stops only its feature, which then explains why it didn't go online. Your choices for each service are kept while Work offline is on.
- If PaperRoute can't read its settings file, it works offline until you review Online services and choose **Save**.
- A service added in a later version starts on, and appears in Online services with what it sends.
- Requests identify PaperRoute and its version, and nothing about you. Beyond what the table below lists, PaperRoute sends nothing: not your library, your files, or your notes.
- Links you choose to open, such as a journal's website, open in your web browser. They aren't PaperRoute requests, so Work offline doesn't stop them.

### OpenAlex key

OpenAlex, an open catalog of scholarly works and journals, gives everyone a small free daily allowance, and a free personal key raises it. To add one, sign in at openalex.org, copy your key from its API settings, and choose **Add Key...** under Online services.

PaperRoute keeps the key encrypted for your Windows account on this computer, sends it only to api.openalex.org, in a request header, and never puts it in backups, exports, or Diagnostics. PaperRoute doesn't include a key of its own. After moving to another computer, add your key again.

### What PaperRoute sends, and when

| Service | Contacts | Sends | When |
| --- | --- | --- | --- |
| Update check | api.github.com, github.com, objects.githubusercontent.com, release-assets.githubusercontent.com | Nothing about you or your library; it reads the list of PaperRoute releases. | At startup, if automatic checks are on, and Check for Updates. |
| DOI lookup (Crossref) | api.crossref.org | A DOI. | DOI & Crossref Metadata on a manuscript page, and Fill Blanks from Crossref. |
| Publication check | api.crossref.org, orcid.org, pub.orcid.org | The DOIs and titles of the manuscripts you check, and your ORCID iD if you include it. | Check for Publications. |
| ORCID import | orcid.org, pub.orcid.org | An ORCID iD. | ORCID... in Library > Authors & Affiliations. |
| Journal facts (DOAJ and OpenAlex) | doaj.org, api.openalex.org | A journal's ISSNs, or a name you type to find a journal and the id of the one you pick. | Look Up Facts... on the Journals page. |
| Find journals (OpenAlex) | api.openalex.org | The keywords you review, a start date, and the ids of the journals found. | Find Journals... on a manuscript's journal shortlist. |
| Your citations (ORCID and OpenAlex) | pub.orcid.org, api.openalex.org | Your ORCID iD to ORCID, and the DOIs of your works to OpenAlex; your iD to OpenAlex only if you choose. | Update from OpenAlex... on Insights > Your Citations. |

---

# How Do I...?

## How do I add a manuscript I am currently writing?

1. Choose **Add Manuscript**.
2. Enter the title, or choose **Paste a Title Page...** (see below).
3. Use Draft as the stage if active writing has begun.
4. Optionally check **Remind me** and name a first deadline. It becomes an ordinary reminder on the **Deadlines** page.
5. Choose **Add Manuscript**, then open the manuscript to add or adjust structured authors and a target journal.

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

1. Open the manuscript.
2. On the **Submissions** tab, choose **Record Submission...**.
3. Enter the journal or choose **Use Library...**.
4. Enter the submission date and Journal manuscript ID if known.
5. Save the publisher portal URL if useful.
6. Optionally enable a follow-up date.
7. Save the submission, then choose **Save** on the manuscript page.

If you already prepared an unlinked packet, use its **Save & Go To... > Record Submission...** action instead to associate the packet with the new submission. For another revision round at the same journal, retain the existing submission and select that submission and round in the packet editor.

## How do I prepare a packet before submitting?

1. Open the manuscript, go to the **Versions** tab, and record the exact manuscript snapshot.
2. On the **Readiness & Packets** tab, open **Submission Readiness...**, choose **New from Journal...**, and review the copied requirements.
3. Use **Save & Go To... > Save & View Packets**, then create a packet linked to the intended version.
4. Add file records as managed copies, external links, or metadata only.
5. Optionally **Record Fingerprint** for each local file and use **Check Files** to compare contents before sending them.
6. Save the vault, then choose **Save** on the manuscript page.

This records preparation only. Record the journal submission after it actually occurs. A managed copy retains bytes when the manuscript is saved; a linked original can change independently of PaperRoute.

## How do I remind myself to check on a journal?

Use either method:

- Edit the journal submission and set a follow-up date; or
- open **Deadlines** and choose **Add Reminder**.

An explicitly saved submission follow-up remains active until you clear or change it, even if an editorial decision is later recorded.

## How do I record an R&R or revision request?

1. Open the relevant journal submission.
2. Add the editorial decision.
3. Choose the appropriate revision decision.
4. Record the decision date and revision deadline if one exists.
5. Save.
6. Confirm the manuscript is in the Revision stage.

The deadline will then appear on the Deadlines page and in the Needs Attention workflow as appropriate.

## How do I record reviewer comments and my response?

1. Select the journal submission on the manuscript's **Submissions** tab and record the relevant editorial decision under **Editorial History**.
2. Open its **Reviewer Responses** tab and choose **Add Comment...**.
3. Select that decision, enter the revision round and reviewer label, and record a comment or action.
4. Add a draft response, manuscript location, notes, and the appropriate status, then choose **Save Comment**.
5. Choose **Save** on the manuscript page.

Use **Export Markdown...** on the Reviewer Responses tab for an editable response draft. Keep the original reviewer/editor letters and revised manuscript files as correspondence under the same submission. See [Reviewer Response Matrix](#reviewer-response-matrix) for ordering, export, and save behavior.

## How do I move a rejected paper to another journal?

1. Record the rejection under the old journal submission.
2. Keep the manuscript in the active Pipeline if you intend to reroute it.
3. Change the target journal.
4. Add a new journal submission when you resubmit.

Do not overwrite the old submission. The old submission is part of the manuscript's route.

## How do I put a manuscript in the File Drawer?

Choose **Move to File Drawer...** in the manuscript card's **⋯** menu.

The File Drawer is reversible; it is not deletion.

## How do I attach an OSF project?

1. Open the manuscript.
2. Choose **Journal, Preprint & Links...**.
3. Under Related Web Links, choose **Add Link**.
4. Use a label such as `OSF Project`.
5. Enter the `https://` URL.
6. Save.

## How do I save a preprint?

Open **Journal, Preprint & Links...** and enter the preprint DOI and/or preprint URL.

## How do I make a CV publication list?

1. Choose **Import & Export > Publication & CV Export...**.
2. Select Published only, or Accepted / In Press / Published if desired.
3. Choose the manuscripts to include.
4. Select CV section.
5. Choose Plain text, Markdown, or HTML.
6. Copy the preview or save the export.

## How do I move PaperRoute to another computer?

The safest approach is:

1. create a portable backup with **Settings > Backup Library...**;
2. install PaperRoute on the destination computer;
3. use **Settings > Restore Backup...**;
4. confirm manuscript counts and managed files before retiring the old installation.

Backups hold your library and its files, not your preferences or OpenAlex key. On the new computer, review **Settings > Preferences...** and add the key again.

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

External services are used only for the features listed in **What PaperRoute sends, and when**, and for links you choose to open. You can turn each service off, or work offline; see **Online Services and Working Offline**. A publication check sends Crossref the titles and DOIs of the manuscripts you check, and nothing else.

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

Open **Deadlines** first. If the reminder is present there, the stored reminder data is working.

Then check:

- notifications are enabled in PaperRoute Preferences;
- Windows has not suppressed application notifications;
- the reminder falls within the configured notification window.

Normal PaperRoute use does not depend on system notification delivery.

### The local User Guide did not load

PaperRoute ships a local copy of this guide. Open it with **Help** at the bottom of the left rail, or press **F1**. If the local file cannot be read, the in-app Help window falls back to a short built-in Quick Start and can link to the GitHub copy when internet access is available.

---

## What Comes Next?

The Route, Version History, readiness profiles, submission packets, reviewer responses, and Deadlines form one connected manuscript record. Later releases build on that record:

- **v0.9 — Journal Choice & Guidance:** a journal shortlist for each manuscript, an offline guide to choosing a journal, journal facts from open indexes, a teaching example library, and an optional AI assistant, with every online feature listed in one place and a switch to work offline.
- **v0.9.1 — 1.0 Hardening:** guided onboarding, accessibility, consistency, recovery, and release certification.

Planned releases are directional and may change; see the [roadmap](https://github.com/JUhalt/PaperRoute-Tracker/blob/master/ROADMAP.md).

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
