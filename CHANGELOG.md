# Changelog

All notable changes to PaperRoute Tracker will be documented here.

## [Unreleased] — v0.9 Journal Choice & Guidance

Development is tracked in the [v0.9 milestone](https://github.com/JUhalt/PaperRoute-Tracker/milestone/13).

### Added

- **Journal shortlist** ([#65](https://github.com/JUhalt/PaperRoute-Tracker/issues/65)) on each manuscript's Overview: the journals you are considering, in your order of preference, each with a status (Considering, Preferred, Backup, Ruled out), your reasons, and whether and how a submission to it ended. After a rejection or withdrawal, the shortlist offers the next journal as the target, an offer that changes nothing until you accept it.
- **Choosing a journal** ([#89](https://github.com/JUhalt/PaperRoute-Tracker/issues/89)): each shortlisted journal has trust questions, adapted with attribution from the Think. Check. Submit. checklist (CC BY 4.0), and fit questions for the manuscript, with your own history with the journal beside them. The User Guide's new **Choosing a Journal** section explains both, and **How to choose a journal** opens Help at that section.

- **Example library for teaching** ([#83](https://github.com/JUhalt/PaperRoute-Tracker/issues/83)): the fictional Example Lab's manuscripts, opened in a separate window from the welcome or Import & Export. It covers a desk rejection and rerouting, revision rounds with reviewer comments at every stage, a publication, a preprint, and the File Drawer, with dates relative to today. The example has its own temporary storage: your library is never opened, and the example is discarded when its window closes. The User Guide's **Teaching with PaperRoute** section suggests a 15-minute walk through it.

- **Online services and Work offline** ([#86](https://github.com/JUhalt/PaperRoute-Tracker/issues/86)): **Settings > Preferences... > Online services** lists every service PaperRoute can contact, with the hosts it contacts, what it sends, and when, and lets you turn each off. **Work offline** (also **Settings > Work Offline**) stops them all, update checks included. The rail shows **Online** in green or **Working offline** in blue, and either opens Online services. Every request passes one gate that refuses it before anything is sent, follows redirects itself so each hop is checked, and reaches only the hosts listed for its service. The User Guide's **What PaperRoute sends, and when** table is checked against the app by the tests.
- **Optional OpenAlex key** ([#86](https://github.com/JUhalt/PaperRoute-Tracker/issues/86)): paste a free personal key to raise OpenAlex's daily allowance for PaperRoute's OpenAlex features. It is encrypted for your Windows account, kept out of backups, exports, and Diagnostics, and sent only to api.openalex.org in a header. PaperRoute includes no key of its own.

- **Journal facts and metrics** ([#87](https://github.com/JUhalt/PaperRoute-Tracker/issues/87)): the Journals page shows the selected journal's facts, links, and metrics beside the list. **Look Up Facts...** looks the journal up by ISSN, or finds it by name, in DOAJ and OpenAlex, two open indexes (both CC0): publication fee, license and copyright, peer review, typical time to publication, plagiarism screening, open-access status, topics, and links to its aims and scope, author instructions, editorial board, and sharing policy in Open Policy Finder (linked, never fetched). A preview shows everything found first; only blank fields are filled, and nothing you typed is replaced. Facts carry their source and the date checked, and older than a year they are marked as possibly out of date.
- **Journal metrics** ([#87](https://github.com/JUhalt/PaperRoute-Tracker/issues/87)): OpenAlex's open metrics (2-year mean citedness, h-index, i10-index), and metrics you enter with their source and year, such as the Journal Impact Factor, CiteScore, SJR, SNIP, h5-index, or an acceptance rate. Each shows its definition and where it is published. Metrics are never combined into a score or used to sort journals, and a note explains why (DORA).
- **Journals that publish work like yours** ([#88](https://github.com/JUhalt/PaperRoute-Tracker/issues/88)): **Find Journals...** on a manuscript's shortlist proposes keywords from its title and keywords, shows the exact request before anything is sent (never the abstract, notes, or files), and finds journals in OpenAlex that recently published articles mentioning them. Each journal shows its evidence: matching articles out of all its articles, open access and listed fee, your own history with it, and recent example articles linked by DOI. Checked journals join the shortlist as Considering with their evidence; nothing is scored or ranked. When OpenAlex asks PaperRoute to wait, it says how long.
- Shortlisted journals show their facts in one line, and the journal-choice questions show what's known beside them, with links. Nothing is ticked for you.
- **Your citations** ([#91](https://github.com/JUhalt/PaperRoute-Tracker/issues/91)): a **Your Citations** tab on Insights shows how your published work has been cited, from OpenAlex: citations, h-index, i10-index, g-index, and m-quotient, citations by year, and, for each of your published manuscripts with a DOI, its citations, field-weighted citation impact, and percentile. **Update from OpenAlex...** reads the works on your public ORCID record and looks up their DOIs, with your published manuscripts' DOIs; your iD goes to OpenAlex only if you choose. You check which works are yours before anything is saved. Opening the tab sends nothing. Each figure shows its definition, and a note says why databases disagree and what citations don't measure.

### Changed

- Settings are saved by writing a new file and swapping it in, keeping the previous one as settings.bak. If the settings can't be read, PaperRoute works offline until you review Online services ([#86](https://github.com/JUhalt/PaperRoute-Tracker/issues/86)).
- Network problems are explained in plain words (no connection, a timeout, a busy service) instead of the system's message, and a publication check or Fill Blanks run stops once, with the reason, when a service is turned off.

### Storage

- **Schema 9** adds journal shortlists (with the evidence behind a suggested journal), and journal ISSNs, links, facts, and metrics. As with Schemas 7 and 8, the upgrade validates the library and changes only the schema marker.
- **citations.json** holds your saved citation figures, written only when you save an update and kept with the previous copy as citations.bak. Portable backups include it, and restoring a backup that has it replaces yours.

## [0.8.0] - 2026-09-28

PaperRoute v0.8.0 — **Route Analytics & Reports** — was released on September 28, 2026 from [PR #80](https://github.com/JUhalt/PaperRoute-Tracker/pull/80) and [PR #90](https://github.com/JUhalt/PaperRoute-Tracker/pull/90), after the v0.8.0-rc.1 Preview. Certification is recorded in [#85](https://github.com/JUhalt/PaperRoute-Tracker/issues/85). v0.8 upgrades the library to Schema 8.

### Added

- An **Insights** page (Ctrl+5) with route statistics from your own records ([#30](https://github.com/JUhalt/PaperRoute-Tracker/issues/30)): median time to a first decision and from first submission to acceptance, each with its sample size, and **Your Routes**: journals, revision rounds, and days to acceptance and publication for every submitted manuscript. Missing dates are left out, never estimated.
- **Your Journals** ([#62](https://github.com/JUhalt/PaperRoute-Tracker/issues/62)): submissions, outcomes, and median days to a first decision and in review for each journal you have used, grouped by Journal Library record or exact name, never by guess. The Journals page shows the same history for the selected journal.
- **Pipeline Report...** and **Route Report...** ([#63](https://github.com/JUhalt/PaperRoute-Tracker/issues/63), [#30](https://github.com/JUhalt/PaperRoute-Tracker/issues/30)): self-contained web pages to print or save as PDF, previewed before saving. They never include notes, correspondence, reviewer comments, file paths, or contact details.

- **Route maps** ([#82](https://github.com/JUhalt/PaperRoute-Tracker/issues/82)): a manuscript's Route opens on its whole route drawn to scale, each stretch colored by who held it (the journal, you, or production), journals named above, and every event numbered and explained ("after 48 days of rerouting"). Insights gains **Route Map**, every published route lined up at day 0 with the median time to publication and the share spent waiting on journals versus revising; work not yet published can be included, drawn to today. Time the record does not assign is hatched as not recorded, never estimated.
- **Work types and tags** ([#64](https://github.com/JUhalt/PaperRoute-Tracker/issues/64)): a type (journal article, preprint, conference paper or abstract, poster, book chapter, thesis, other) and your own colored tags on each manuscript's Overview. Tags show on board cards and in the Library, and both are searchable. Imports set the type when the source states it; nothing is inferred. CV export can group by type, and the Excel workbook keeps both.

### Changed

- Import & Export moves to **Ctrl+6**; Insights takes Ctrl+5.

### Fixed

- A second launch of PaperRoute brings the open window forward instead of opening another window on the same library, where a save in one could overwrite the other ([#77](https://github.com/JUhalt/PaperRoute-Tracker/issues/77)).
- The update prompt and its progress window scale with the display, so **Download & Restart** shows its full label at 125% and 150% (found during v0.8.0-rc.1 certification, [#85](https://github.com/JUhalt/PaperRoute-Tracker/issues/85)). The prompt comes from the installed version, so the fix applies to updates offered from v0.8 onward.

### Storage

- **Schema 8** adds work types, tags, and tag colors. As with Schema 7, the upgrade validates the library and changes only the schema marker; records start with no type and no tags.

## [0.7.0] - 2026-09-28

PaperRoute v0.7.0 — **Deadline Center** — was released on September 28, 2026 from [PR #78](https://github.com/JUhalt/PaperRoute-Tracker/pull/78), after the v0.7.0-rc.1 Preview. Certification is recorded in [#79](https://github.com/JUhalt/PaperRoute-Tracker/issues/79). v0.7 upgrades the library to Schema 7.

### Added

- The **Deadlines** page replaces Reminders ([#28](https://github.com/JUhalt/PaperRoute-Tracker/issues/28)): one list of what needs action, grouped as Overdue, Today, Next 7 days, Later, No date, and Done (folded away). Counts across the top; chips for Revisions, Follow-ups, Reminders, and Preparation; and a filter box. Revisions show progress from the reviewer comments on their decision. Work without a date stays visible: a revision with no deadline, and an unsubmitted packet whose checklist has required items open.
- Each row acts on the record that owns it: **Open** lands on the submission (at its reviewer responses for a revision), **Postpone...** offers tomorrow, a week, two weeks, or any date, **Done** completes a reminder, **Clear Follow-up...** removes only the follow-up date, and **Open Readiness** opens Readiness & Packets. Enter runs a row's first action. Nothing is copied, so Deadlines and the manuscript page always agree.
- The rail's Deadlines badge counts what is overdue or due today, and **View in Deadlines →** sits beside Needs Attention on the Board. **Ctrl+4** opens Deadlines.
- **Check for Publications...** ([#61](https://github.com/JUhalt/PaperRoute-Tracker/issues/61)) asks Crossref, only when you choose, whether manuscripts you are tracking have been published: by DOI, by a preprint's link to its published version, by title, and optionally in an ORCID record. Each possible match offers **Review Match**, **Mark Published...**, and **Ignore**; unreviewed matches wait on the Deadlines page. From Import & Export, or a manuscript page's **⋯** menu.
- **Mark Published...** lists exactly what it will change, then records a normal lifecycle event: the Published shelf and stage from the publication date, a history entry with the journal and DOI, and empty publication fields filled. Submissions and decisions are left as recorded.
- **Fill Blanks from Crossref...** fills empty publication fields for manuscripts with a DOI after a preview of every change. It never replaces a value.

### Storage

- **Schema 7** remembers publication matches, so an ignored match stays ignored. The upgrade validates the library and changes only the schema marker; manuscript data, managed files, and backups are left byte-for-byte. v0.6 cannot open a library after v0.7 has used it; keep a backup made before upgrading.

### Fixed

- Revision deadlines now name their journal on the Deadlines page and in calendar exports.
- The **First deadline** date in Add Manuscript shows in full at 150% display scaling ([#76](https://github.com/JUhalt/PaperRoute-Tracker/issues/76)).
- The update prompt opens on **Download & Restart** instead of with its release notes selected.
- About PaperRoute scales with the display instead of clipping its text at 150%, and describes PaperRoute in its one-line description.
- Check for Publications says there is nothing to check yet when the library is empty, instead of that everything is already published.

## [0.6.0] - 2026-09-27

PaperRoute v0.6.0 — **Workspace** — was released on September 27, 2026 from [PR #69](https://github.com/JUhalt/PaperRoute-Tracker/pull/69), after the v0.6.0-rc.1 Preview. Certification is recorded in [#74](https://github.com/JUhalt/PaperRoute-Tracker/issues/74). v0.6 uses the same Schema 6 library as v0.5 and migrates no data.

### Added

- **Paste a Title Page...** in Add Manuscript reads Word or LaTeX title pages on this computer and proposes the title, ordered authors with affiliations, abstract, and keywords, plus an optional first deadline ([#60](https://github.com/JUhalt/PaperRoute-Tracker/issues/60)).
- Board shelves are tabs with counts over one scroll area, and cards fill as many columns as the window fits ([#57](https://github.com/JUhalt/PaperRoute-Tracker/issues/57)).
- Cards open on click, with Move, Restore, and Delete in a **⋯** menu. They show the stage, what needs attention or time in stage, and the route so far as dots and a short summary such as "2nd journal · major revision", from recorded submissions and decisions only.
- Needs Attention items are chips that filter the board and can be used from the keyboard. **Ctrl+F** jumps to search.
- A shared visual system: rounded controls, one filled primary action per window, and quiet outlines for the rest, in Light and Dark.
- One main window with a left rail of pages: **Board**, **Library** (every manuscript in a sortable table, plus Authors & Affiliations), **Journals**, **Reminders**, and **Import & Export**, with Settings and Help at the bottom. **Ctrl+1** to **Ctrl+5** open the pages, **Alt+Left** and **Alt+Right** go back and forward, and **F1** opens the User Guide ([#54](https://github.com/JUhalt/PaperRoute-Tracker/issues/54)).
- The rail can collapse to icons for more room and remembers that choice. The PaperRoute logo and the "Track • Submit • Publish" tagline head the rail; double-click the logo for About.
- The **Import & Export** page lists every way of bringing work in or taking it out, with a sentence on what each keeps. **ORCID Works...** opens the author the works belong to ([#58](https://github.com/JUhalt/PaperRoute-Tracker/issues/58)).
- An empty library opens on a welcome instead of empty shelves: **+ Add Manuscript**, **Import Existing Work**, the four ways in (a pasted title page, ORCID works, BibTeX or RIS, a spreadsheet), and a plain statement that the library stays on this computer. It never adds sample data ([#59](https://github.com/JUhalt/PaperRoute-Tracker/issues/59)).
- Empty lists and tables say what belongs there and how to add the first item, and disappear with it.
- The Library filters as you type by title, journal, stage, or route, and says when nothing matches.

- A manuscript opens as a page in the main window, with **Overview**, **Authors**, **Versions**, **Submissions**, and **Readiness & Packets** tabs. The header shows the stage, journal, and route, with **Copy Citation**, **View Route**, and **Delete Manuscript...**. Overview shows the linked journal's notes and checklist from the Journal Library ([#55](https://github.com/JUhalt/PaperRoute-Tracker/issues/55)).

- The **Submissions** tab lists each journal submission (journal, submitted date, and latest decision) beside the selected one, whose **Editorial History**, **Reviewer Responses**, and **Correspondence & Files** tabs are edited in place. **Record Submission...** and **Add Decision** remain explicit actions ([#56](https://github.com/JUhalt/PaperRoute-Tracker/issues/56)).

### Changed

- Sections in dialogs and on the manuscript page are cards, tabs are underlined text like the board's, and text boxes and lists have a quiet border that turns to the accent color while focused ([#57](https://github.com/JUhalt/PaperRoute-Tracker/issues/57)).
- Reviewer comments are listed with a status pill, the reviewer and round, and the start of the comment; the whole comment is in the details.
- **Export Markdown...** opens with the caret at the start instead of the whole draft selected, and the Markdown escapes only what would change the rendered result, so plain-text readers see `p. 6, lines 112-118` rather than `p\. 6, lines 112\-118`. The raw file differs from v0.5; the rendered result is the same ([#73](https://github.com/JUhalt/PaperRoute-Tracker/issues/73)).
- Reviewer comments are edited on the Submissions tab and join the manuscript's unsaved changes, so the Save Comment, Save & Close, Close, and Save & Close chain is now Save Comment, then Save. Export output is unchanged.
- One save step per manuscript: an **Unsaved changes** bar with **Save** (Ctrl+S) and **Discard** replaces Manuscript Details' Save & Close and Cancel. Leaving the page, opening another manuscript, or closing PaperRoute with unsaved changes asks to save, discard, or stay. What a save stores, including managed-file copies and deletions, is unchanged.
- The **Data** menu is gone and its commands have moved to pages; **Backup Library** and **Restore Backup** are under **Settings** in the rail and on the Import & Export page. The User Guide has a relocation table.

### Fixed

- Two-word field labels such as *Revision deadline* no longer lose their second line at 150% scaling.
- A stale border line no longer remains inside a manuscript card after the window is widened or maximized.
- Muted text such as counts, card metadata, and the status line meets 4.5:1 contrast on the board and on cards, in Light and Dark.
- Names that contain an ampersand, such as *Memory & Cognition*, keep it in every dialog, including Route View and Submission Details. Deliberate keyboard accelerators such as **Show &status** still work ([#71](https://github.com/JUhalt/PaperRoute-Tracker/issues/71)).
- Version History no longer refers to Save & Close or to Journal Submissions "below".
- Text-box borders now use the theme color on screen, not only in printed or captured images (found in v0.6.0-rc.1).
- The update prompt's **Download & Restart** keeps its ampersand.

## [0.5.0] - 2026-09-26

### Added

- Manual Reviewer Response Matrix within each journal submission: comments/actions, reviewer labels, four work statuses, response drafts, manuscript locations, and notes. Each item references an existing editorial decision and an explicit revision round.
- Status filtering, stored item ordering, and an editable Markdown export preview. Drafting and exporting do not record submissions or change manuscript lifecycle state.
- Working-copy save/cancel support and conservative Schema 5 to 6 migration, with decision-reference validation and portable backup/restore coverage.

PaperRoute v0.5.0 — **Reviewer Response Workflow** — was released on September 26, 2026 from the certified v0.5.0-rc.2 source. Scope was owned by [#27](https://github.com/JUhalt/PaperRoute-Tracker/issues/27); release certification was recorded in [#52](https://github.com/JUhalt/PaperRoute-Tracker/issues/52).

The September 20 [development checkpoint](ManualCertification/v0.5-Development-Evidence-2026-09-20.md) records the initial 531-test response increment. The combined response and shelf implementation passed **539 tests, 0 failed, 0 skipped** on September 22, 2026; with the ampersand fix, the rc.2 source passes **541 tests**. Final package and installed checks are recorded in #52.

### Fixed

- Manuscript shelves now reconcile stale horizontal scroll extents after wide-to-narrow resizing, maximize/restore, and card replacement. The board keeps genuine vertical scrolling and card actions reachable. Native 100%, 125%, and 150% checks passed; [PR #51](https://github.com/JUhalt/PaperRoute-Tracker/pull/51) completed [#37](https://github.com/JUhalt/PaperRoute-Tracker/issues/37).
- Literal ampersands no longer disappear from **Save & Close**, **DOI & Crossref Metadata...**, the **Data** menu items, the Version History summary, and related help labels ([#67](https://github.com/JUhalt/PaperRoute-Tracker/issues/67), [PR #68](https://github.com/JUhalt/PaperRoute-Tracker/pull/68)).

## [0.4.0] - 2026-09-13

PaperRoute v0.4.0 — **Submission Readiness** — was released on September 13, 2026. It adds journal-specific readiness, exact submission packets, local file-integrity checks, connected preparation/submission navigation, and Schema 5 persistence.

### Added

- Reusable journal checklist templates and manuscript-specific readiness with unresolved, complete, and not-applicable states. Readiness is advisory and does not change manuscript history.
- Submission Packet Vault with exact version, optional readiness/submission/round associations, file roles, managed copies, external links, and metadata-only records.
- Explicit local SHA-256 fingerprint recording and background checks, with clear changed, unchanged, missing, unavailable, and unchecked states. Replacing a fingerprint requires confirmation; checks never write source files.
- Connected navigation between Manuscript Details, readiness, Version History, packets, and actual submissions. Recording a submission remains an explicit action, and child-dialog saves remain pending until Manuscript Details is saved.
- Disposable demos for reviewing real dialogs and exercising save/reload without using an existing manuscript library.
- Schema 5 migration for readiness profiles, submission packets, saved fingerprints, and reusable journal checklist templates. Existing libraries do not require invented preparation or submission history.

### Fixed

- Long notes no longer crowd out readiness and packet lists when windows are resized. Submission Details reserves space for its portal and editorial history.
- Packet save, deletion, recovery, and portable restore preserve exact references and managed contents while leaving linked external originals untouched.
- Invalid packet/version/submission associations are rejected before file operations. Valid imported revision rounds above 99 are preserved.
- Failed schema replacement preserves prior recovery metadata. Conflicting staged versions remain recoverable, and independent packet recovery still runs.
- Light-theme action/link text and dark-theme destructive-action hover text have improved contrast.

### Compatibility and release status

- Portable ZIP backup/restore preserves readiness, journal templates, packet/version/submission references, fingerprints, and managed snapshots. Excel remains a partial interchange format.
- The [v0.4 user guide](docs/USER_GUIDE.md) describes the preparation workflow and its explicit save boundaries; [upgrade notes](UPGRADE_NOTES.md) describe the schema-4-to-5 transition.
- Release gate [#42](https://github.com/JUhalt/PaperRoute-Tracker/issues/42) closed after installed-upgrade, clean-install, native display/keyboard, updater, and artifact certification. See the [release closure record](ManualCertification/v0.4-Release-Closure.md) for the tagged build and published-asset evidence.

## [0.3.0] - 2026-08-27

### Added

- **Visual Route View** derived deterministically from stored manuscript history, showing submissions, editorial decisions, reroutes, lifecycle state, manuscript versions, and File Drawer outcomes without inventing missing workflow events.
- **Manuscript Version History** with working, submitted, and revised snapshots; current-version identity; notes; submission/decision associations; and revision-round metadata.
- Immutable **PaperRoute-managed manuscript-version snapshots**, alongside linked-file and metadata-only version histories.
- **Current State** and **Current Version** route semantics so workflow position and active manuscript snapshot remain distinct.
- Direct Route drill-down into the authoritative **Manuscript Details** record.
- Reusable contextual `?` help for Current State, Current Version, Journal manuscript ID, version dates, submission associations, decision associations, and revision rounds.
- Reusable manual-certification fixtures using the `ZZZ-CERT-v0.3-*` naming convention for repeatable Route and Version History validation.
- Managed-library recovery warning behavior that allows a valid manuscript database to continue loading when internal staged-deletion recovery encounters an access or I/O problem.

### Changed

- Route chronology now uses canonical workflow events so submission and editorial-decision cards explain lifecycle state without redundant Submitted, Revision, Draft, or Accepted stage cards.
- Rejection is presented as a **reroute** when the manuscript later moves to another journal rather than as a dead-end state.
- Real-world event chronology is kept separate from `RecordedAtUtc` and `LastModifiedAtUtc`, preventing later metadata edits from silently reordering manuscript history.
- Manuscript Details, Submission Details, Version History, Correspondence & Files, and related workflow surfaces received responsive/high-DPI layout improvements.
- Main-dashboard manuscript actions reflow more reliably at narrow widths, keeping Open, Move/Restore, Delete, and View Route reachable.
- In-app updater release notes now convert common Markdown headings, bullets, links, and paragraphs into cleaner readable text.
- Journal submission terminology now uses **Journal manuscript ID** rather than the ambiguous Manuscript number label.

### Compatibility and safety

- Existing manuscripts without Version History remain valid and load without requiring synthetic historical records.
- Schema 3 adds manuscript-version history; Schema 4 adds chronology/audit provenance while preserving legacy real-world event dates.
- Historical PaperRoute-managed manuscript snapshots remain immutable after commit.
- Deleting a PaperRoute Version History record never deletes an externally linked original file.
- Managed manuscript-version deletion is transactional: PaperRoute stages owned snapshots reversibly and removes them only after authoritative manuscript JSON saves successfully.
- Interrupted managed-version deletion can be recovered on startup without corrupting valid manuscript metadata.
- Canceling Manuscript Details discards unsaved version additions, edits, and deletions.
- Portable backup/restore preserves manuscript versions and PaperRoute-managed snapshot content.

### Testing and certification

- Automated regression suite contains **299 passing tests**.
- Manual certification passed for Route chronology, same-day ordering, rejection/reroute behavior, withdrawal behavior, Current State vs. Current Version semantics, contextual help, and Version History persistence.
- Linked-file deletion certification confirmed that deleting a PaperRoute version record leaves the external source file present and SHA-256-identical.
- Managed-copy deletion certification confirmed that both the version metadata and PaperRoute-owned snapshot are removed together after Save & Close.
- Managed-library recovery resilience was certified with a deliberately inaccessible/missing staged-deletion path; PaperRoute warned the user, loaded the valid library, and remained usable.
- Main-board and affected workflow UI passed release smoke testing at **100%, 125%, and 150% Windows display scaling**.
- Installed **v0.2 → v0.3** upgrade, updater presentation, portable backup/restore, clean-install/first-launch, release packaging, and SHA-256 checksum workflows were certified before release.

### Known cosmetic issue

- At some narrow/high-DPI main-window sizes, manuscript shelves may display an unnecessary horizontal scrollbar even when all controls fit and remain reachable. This is cosmetic and deferred to a later hardening release.

## [0.2.0] - 2026-08-22

### Added

- Storage schema 2 metadata foundation with explicit schema-1 to schema-2 migration.
- Reusable structured authors and affiliations with manuscript-specific order, affiliation assignments, corresponding-author designation, and ORCID-ready identities.
- DOI normalization and Crossref lookup with selective preview-before-apply metadata enrichment.
- ORCID public-profile lookup and one-way import for identity, affiliations, and works.
- BibTeX and RIS import/export with DOI/title duplicate protection, structured-author reuse, and visible warnings for unsupported or ambiguous fields.
- Reusable Journal Library with publisher/homepage/submission-portal metadata, favorites, shortlist status, and manuscript target-journal linkage.
- Preprint DOI/URL and labeled project/source links such as OSF destinations.
- Publication and CV export to plain text, Markdown, and HTML.
- Deterministic reminder engine for revision deadlines, submission follow-ups, and custom manuscript reminders.
- Reminders & Calendar view with overdue/due/upcoming filters and portable `.ics` calendar export.
- Optional Windows startup reminder notifications with configurable lead time.
- Canonical `docs/USER_GUIDE.md` plus searchable local/offline in-app Help.
- Isolated Visual Studio development storage and stage-count filtering.

### Changed

- External metadata integrations remain preview-first and user controlled; they do not silently change manuscript lifecycle state.
- Dated ORCID/bibliography works enter Published only when the user explicitly chooses that placement.
- Manuscript cloning, persistence normalization, and portable restore preserve schema-2 metadata, reusable identities, links, reminders, and follow-up dates.
- Explicit submission follow-up dates remain user-owned reminders until changed or cleared.
- Revision reminders derive from the latest editorial-decision deadline, with the legacy manuscript-level field retained only as a compatibility fallback.
- Main dashboard, Manuscript Details, submission controls, reminder UI, and secondary dialogs received responsive/high-DPI layout polish.

### Compatibility and safety

- Existing schema-1 manuscript data is validated before schema metadata is upgraded.
- The previous schema metadata is preserved as `schema.v1.bak` during migration.
- Invalid schema-1 manuscript JSON blocks migration rather than being silently rewritten.
- Future unsupported schema versions continue to fail closed.
- Existing free-text co-author values remain preserved as legacy text.
- PaperRoute does not store publisher portal passwords or credentials.

### Testing

- Automated regression suite contains **159 passing tests**.
- GitHub Actions builds and tests the Windows Release configuration and publishes a Windows x64 CI artifact.
- Historical release gate: final v0.2.0 publication required upgrade, backup/restore, clean-install, updater, UI, and packaging certification. See the [published v0.2.0 release](https://github.com/JUhalt/PaperRoute-Tracker/releases/tag/v0.2.0) and [release tracking issue #22](https://github.com/JUhalt/PaperRoute-Tracker/issues/22). The test total above is the historical v0.2 checkpoint.

## [0.1.0] - 2026-08-20

### Added

- Fresh installations now default to the **Stable** update channel while existing users retain their persisted channel selection.
- Regression coverage verifies Stable defaults and preservation of existing Preview settings.
- Release workflows now generate and publish SHA-256 checksums for packaged release artifacts.
- File Drawer metadata is now visible from Manuscript Details, including the filed date and an editable File Drawer reason.
- Updating a File Drawer reason records the change in manuscript history for traceability.

### Changed

- GitHub Actions used by CI and release workflows are pinned to immutable commit SHAs.
- Main-dashboard layout now scales more reliably at high Windows display scaling, including 150%, 175%, and 200%.
- Manuscript cards, shelf heights, toolbar rows, and action buttons now size more responsively from rendered content instead of relying on fixed pixel geometry.
- Manuscript Details and submission controls have improved spacing and DPI-safe sizing.
- Zero-count Needs Attention indicators remain readable in Dark mode without appearing active.
- Header controls have cleaner labels and more restrained menu chevrons.
- The PaperRoute wordmark aligns with its subtitle and opens About PaperRoute on double-click.

### Fixed

- Single-item Published and File Drawer shelves no longer show unnecessary internal scrolling when enough space is available.
- High-DPI layouts no longer clip the Add Manuscript label, Add Submission control, or Manuscript Details footer actions under the tested scaling range.
- README local-storage documentation no longer contains stale rebrand wording or accidental Markdown fencing.

### Testing

- Automated regression suite now contains **51 passing tests**, including Stable/Preview update-channel persistence coverage.
- Keyboard-only navigation and focus behavior passed the RC accessibility smoke test.
- Main UI and affected dialogs passed display-scaling review at **100%, 125%, 150%, 175%, and 200%**.

## [0.1.0-rc.1] - 2026-08-19

### Added

- Automatic recovery from a valid `manuscripts.bak` when the primary manuscript data file is missing, blank, or corrupt.
- Preservation of damaged primary manuscript files for recovery and diagnostics when automatic backup recovery succeeds.
- Fail-closed startup behavior when neither the primary manuscript database nor its safety backup can be loaded safely.
- Dedicated, testable manuscript attention service for overdue revisions, upcoming revision deadlines, long reviews, missing target journals, and recent rejections.
- Regression tests for complex manuscript/submission/decision import relationships.
- Regression tests for missing linked files and missing managed-copy import sources.
- Regression coverage proving managed-copy batch failures roll back newly created copies without deleting source files.
- Strict storage-schema validation for malformed, missing, nonnumeric, zero, and future schema versions.
- Graceful startup error reporting when PaperRoute cannot safely validate or migrate its local storage.
- Release workflow verification that Git tags, compiled ProductVersion values, and Velopack package versions agree.

### Changed

- Manuscript recovery now restores a valid safety backup rather than treating unreadable primary storage as an empty library.
- PaperRoute now closes instead of continuing with an artificial empty manuscript library after an unrecoverable load failure.
- Invalid `schema.json` files are preserved and rejected rather than silently being rewritten as the current schema.
- Missing schema metadata can still be adopted safely for valid pre-schema PaperRoute data.
- Needs Attention rules now use deterministic business logic separated from the main WinForms interface.
- Preview and Stable release builds now receive their compiled application version directly from the release tag.
- Release builds verify the published binary version before Velopack packaging.
- Current PaperRoute storage documentation now reflects the post-rebrand `PaperRoute` data and managed-library locations.

### Compatibility

- Storage schema remains **1**. RC.1 does not introduce a new data schema.
- Existing schema-1 libraries remain compatible.
- Legacy ManuscriptPipeline data is still preserved after migration.
- The legacy manuscript-level `RevisionDeadline` property remains available for compatibility; active route-aware deadline logic uses the deadline attached to the relevant editorial decision.

### Testing

- Automated regression suite expanded to **48 tests**.
- Release builds must pass the full automated test suite before packaging.
- Developer/portable builds correctly decline in-place automatic update checks.
- Installed Preview builds correctly identify themselves as installed and can query the GitHub release feed.
- Installed `0.1.0-alpha.3` has been confirmed to report itself as current on the Preview channel before RC.1 publication.

### RC.1 validation focus

- Prove that an installed `0.1.0-alpha.3` Preview build detects `0.1.0-rc.1`.
- Download and apply the RC.1 update through PaperRoute itself.
- Confirm PaperRoute restarts into `0.1.0-rc.1`.
- Confirm manuscript data, settings, schema metadata, managed files, and linked external files remain intact across the update.
- Compare pre-update and post-update SHA-256 manifests.
- Complete updater/recovery, diagnostics/encoding, accessibility/UI, documentation, and clean-machine validation before declaring `0.1.0` stable.

## [0.1.0-alpha.3] - 2026-08-19

### Added

- Safe migration from legacy ManuscriptPipeline storage into PaperRoute storage with schema versioning and rollback retention.
- Local diagnostics report with storage paths, runtime information, and privacy-safe troubleshooting details.
- Automated regression coverage for persistence, migration, backup/restore, and spreadsheet import workflows.
- **Copy Version Info** action in About PaperRoute for faster troubleshooting.

### Changed

- Preferences and Diagnostics dialogs now resize and reflow more reliably across display sizes and DPI settings.
- GitHub Actions runs now use concise CI and Release names.
- Superseded CI runs on the same ref are automatically canceled.
- CI workflow permissions are explicitly read-only unless a release needs write access.

### Testing focus

- Prove that an installed `0.1.0-alpha.2` Preview build detects, downloads, applies, and restarts into `0.1.0-alpha.3`.
- Verify all manuscript data, settings, and managed-library paths survive the update unchanged.

## [0.1.0-alpha.2] - 2026-08-19

### Added

- Velopack-based Windows installer and update packaging.
- In-app update checks from GitHub Releases.
- Stable and Preview update channels.
- Optional automatic update checks on startup.
- Manual update checks from the application.
- Release-note preview and download progress before restart.
- Public `ROADMAP.md` summarizing the route to RC and 1.0.

### Changed

- GitHub tag releases now package Velopack installer/update assets rather than only a portable ZIP.
- Installer builds use multi-file self-contained output so future delta updates can avoid repeatedly replacing the bundled .NET runtime.

### Testing focus

- Install alpha.2 with the generated Setup program.
- Use the next prerelease to prove the alpha.2 → alpha.3 automatic-update path before RC.

## [0.1.0-alpha.1] - 2026-08-19

### Added

- Rebrand from the development name ManuscriptPipeline to **PaperRoute Tracker**.
- PaperRoute application icon and refreshed teal/navy visual palette.
- Pipeline, Published, and File Drawer manuscript shelves.
- Manuscript, submission, decision, and correspondence tracking.
- Search, stage filters, sorting, and Needs Attention indicators.
- Light, Dark, and Follow Windows appearance modes.
- Configurable review/revision/File Drawer attention thresholds.
- Standard PaperRoute Excel import template and library export.
- Legacy spreadsheet importer.
- **Mapped spreadsheet importer** for arbitrary Excel column layouts.
- Portable ZIP backups and validated restore workflow.
- GitHub Actions CI for Windows x64 builds.
- Tag-driven GitHub release workflow.

### Compatibility

- Legacy local data and managed-library folders are preserved during migration so existing alpha data remains recoverable.
