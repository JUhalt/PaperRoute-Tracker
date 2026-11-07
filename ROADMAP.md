# PaperRoute Roadmap

PaperRoute is moving toward one connected goal:

> **A trustworthy, local-first record of how an academic manuscript actually moves from working draft to publication.**

The roadmap is organized around that workflow rather than isolated features. Each release should deepen the same canonical manuscript route without creating parallel sources of truth.

The live GitHub milestones and issues remain the source of truth for active work. This file explains how those pieces fit together.

## Workflow model

PaperRoute treats the manuscript as the long-lived research object.

```text
Manuscript
├─ Version History
│  ├─ working versions
│  ├─ submitted snapshots
│  └─ revised versions
│
├─ Submission Attempt — Journal A
│  ├─ readiness checklist
│  ├─ exact submission packet
│  ├─ submitted manuscript version
│  ├─ editorial decision
│  ├─ correspondence / reviewer files
│  ├─ revision round(s)
│  └─ accepted / rejected / withdrawn
│
└─ Submission Attempt — Journal B
   └─ begins only when the manuscript is genuinely rerouted
```

A normal revision **does not create a new submission attempt**. It remains part of the journal interaction that produced the decision. External integrations may suggest metadata, but they must never silently rewrite authoritative manuscript history, lifecycle state, version relationships, readiness state, or packet contents.

## Depth without clutter

The model above is deliberately deep. The interface should not feel deep. Features belong where and when they are relevant: one window, pages instead of stacked dialogs, one save boundary per manuscript, and empty areas that explain themselves. New capability should add depth to the record without adding weight to everyday use.

---

## Release train

Targets are directional rather than promises. Data integrity, migrations, backup/recovery, and release certification take precedence over cadence.

| Release | Theme | Workflow question |
| --- | --- | --- |
| **v0.1** | Reliable Core ✅ | Can I trust PaperRoute with local manuscript data? |
| **v0.2** | Metadata & Integrations ✅ | Can PaperRoute describe and connect my research accurately? |
| **v0.3** | The Route ✅ | What happened to this manuscript, in what order, and which file was which? |
| **v0.4** | Submission Readiness ✅ | What exactly am I preparing and sending to this journal? |
| **v0.5** | Reviewer Response Workflow ✅ | What did the journal ask me to change, and how am I responding? |
| **v0.6** | Workspace UI ✅ | Can I find and act on everything from one calm, uncluttered workspace? |
| **v0.7** | Deadline Center ✅ | What requires action, and when? |
| **v0.8** | Route Analytics & Reports ✅ | What does this publication journey show me—and how can I communicate it? |
| **v0.9** | Journal Choice & Guidance ✅ | Where should this manuscript go, what will that journal ask of me, and how does someone new learn the route? |
| **v0.9.1** | Packet File Safety ✅ | Does a save ever delete a file the library did not ask it to? |
| **v0.9.2** | 1.0 Hardening ✅ | Is the entire workflow polished, resilient, and certifiable? |
| **v1.0** | **Trusted Research Workflow — Preview** | Would I trust this with my real publication pipeline? |
| **v1.1** | macOS and Linux | Can PaperRoute be the same calm, trustworthy app on every desktop? |
| **v1.2** | Open Science Studies | What did I plan before I saw the data, and where can others check what I shared? |

On September 26, 2026, the train was re-sequenced so the interface can catch up with the depth of the data model: v0.6 became **Workspace UI**, Deadline Center moved to v0.7, Route Analytics & Reports moved to v0.8, and optional AI assistance became a proposal for after 1.0. Deadlines and Analytics arrive as pages in the new workspace rather than as additional dialogs.

On September 28, 2026, the proposals awaiting scope review moved into **v0.9 — Journal Choice & Guidance**, and 1.0 hardening moved to **v0.9.1**. Optional AI assistance returns before 1.0, and every online feature is grouped under one local-first rule rather than added piecemeal.

On October 9, 2026, v0.9.1 became a Stable patch for one defect found right after v0.9.0 ([#107](https://github.com/JUhalt/PaperRoute-Tracker/issues/107)), and 1.0 hardening moved to **v0.9.2**. The maintainer also settled the shape of 1.0 and what follows: before 1.0, speed (no page switch or save that stalls the window), consistency at every scale and theme, micro-motion that WinForms can do credibly, and the Windows 11 chrome on every window; after 1.0, one cross-platform interface on Avalonia for Windows, macOS, and Linux (**v1.1**), where the fewer, calmer surfaces pass and composition-grade motion belong.

---

## COMPLETE — v0.1 Reliable Core

PaperRoute v0.1 established the trusted local core: installation/update infrastructure, versioned storage, conservative migrations, recovery/backups, import/export, diagnostics, accessibility, and high-DPI groundwork.

## COMPLETE — v0.2 Metadata & Integrations

PaperRoute v0.2 added structured authors and affiliations, DOI/Crossref and ORCID workflows, BibTeX/RIS exchange, Journal Library, external research links, publication/CV exports, reminders/calendar support, notifications, Help, and the User Guide.

**Safety rule:** integrations can assist, but cannot silently alter authoritative lifecycle history.

## COMPLETE — v0.3 The Route

Issues: **[#23 Visual Route View](https://github.com/JUhalt/PaperRoute-Tracker/issues/23)** and **[#24 Manuscript Version History](https://github.com/JUhalt/PaperRoute-Tracker/issues/24)**

v0.3 established the canonical historical spine used by later releases:

- deterministic Visual Route projection
- Schema 3 manuscript Version History
- Schema 4 chronology/audit provenance
- immutable managed manuscript snapshots
- linked-file and metadata-only versions
- Current State vs. Current Version
- submission/decision/revision-round version associations
- reroute semantics and canonical workflow chronology
- Route drill-down into authoritative Manuscript Details
- safe transactional version deletion
- contextual workflow help
- managed-library recovery hardening
- readable updater release notes
- responsive/high-DPI hardening
- reusable `ZZZ-CERT-v0.3-*` manual-certification fixtures
- 299-test automated regression suite
- installed-update, backup/restore, clean-install, updater, managed-file, and scaling certification

The narrow/high-DPI shelf scrollbar follow-up from v0.3, [#37](https://github.com/JUhalt/PaperRoute-Tracker/issues/37), was brought forward into v0.5 and completed in [PR #51](https://github.com/JUhalt/PaperRoute-Tracker/pull/51). It shipped in v0.5.0.

---

## COMPLETE — v0.4 Submission Readiness

PaperRoute v0.4.0 was released on September 13, 2026. It completed Schema 5, journal-specific readiness, exact Submission Packets, local file-integrity checks, connected preparation/submission navigation, and the associated migration, installer, updater, backup/recovery, keyboard, theme, and DPI certification.

Issues:

- **[#25 Submission Packet Vault and file integrity](https://github.com/JUhalt/PaperRoute-Tracker/issues/25)**
- **[#26 Per-journal readiness checklists](https://github.com/JUhalt/PaperRoute-Tracker/issues/26)**
- **[#38 Schema 5 readiness and submission-packet foundation](https://github.com/JUhalt/PaperRoute-Tracker/issues/38)**

Implementation was delivered through [PR #40](https://github.com/JUhalt/PaperRoute-Tracker/pull/40). Scope was owned by [#38](https://github.com/JUhalt/PaperRoute-Tracker/issues/38), [#26](https://github.com/JUhalt/PaperRoute-Tracker/issues/26), [#25](https://github.com/JUhalt/PaperRoute-Tracker/issues/25), and release certification [#42](https://github.com/JUhalt/PaperRoute-Tracker/issues/42).

Release certification included native 100%/125%/150% scaling, Light/Dark/System checks, clean install, installed v0.3-to-v0.4 migration, RC1 Preview updater validation, and release-candidate packaging/asset verification.

v0.4 turns a manuscript version into a concrete, reconstructable journal submission package.

## Connected workflow

```text
Current manuscript version
        ↓
Choose target journal / preparation
        ↓
Apply journal checklist template
        ↓
Resolve manuscript-specific readiness
        ↓
Assemble Submission Packet
├─ exact manuscript snapshot
├─ cover letter
├─ figures / tables
├─ supplements
├─ required statements / metadata
└─ journal-specific supporting files
        ↓
Freeze/verify exact files actually sent
        ↓
Record Journal Submission
```

## v0.4A — Schema 5 foundation

**Owner:** [#38](https://github.com/JUhalt/PaperRoute-Tracker/issues/38); foundation for #25 and #26.

Build the persistence model before adding UI:

- reusable journal checklist template items
- manuscript/journal-specific readiness state
- checklist item statuses: unresolved, complete, not applicable
- submission packet model
- packet file-role model
- managed-copy vs. external-link semantics
- optional deterministic SHA-256 integrity metadata
- link packet to the exact manuscript version
- optional link to a journal submission/revision round when one exists
- clone/save/load/backup/restore support
- Schema 4 → Schema 5 conservative migration
- regression coverage before any workflow UI ships

**Critical rule:** opening or editing readiness data must never change manuscript lifecycle state.

## v0.4B — Journal checklist templates

**Owner:** [#26](https://github.com/JUhalt/PaperRoute-Tracker/issues/26).

- reusable per-journal templates
- required files/statements/metadata
- journal-specific notes and formatting requirements
- template editing without silently changing historical manuscript completion state

## v0.4C — Manuscript-specific readiness

**Owner:** [#26](https://github.com/JUhalt/PaperRoute-Tracker/issues/26).

- apply a journal template to a manuscript preparation
- explainable readiness derived from individual items
- complete / not applicable / unresolved state
- readiness is advisory and never blocks recording a real submission

## v0.4D — Submission Packet Vault

**Owner:** [#25](https://github.com/JUhalt/PaperRoute-Tracker/issues/25).

- assemble the exact files intended for a submission
- preserve file roles and packet membership
- managed copies and intentional external links
- reconstruct a packet without guessing which files belonged to it

## v0.4E — File integrity

**Owner:** [#25](https://github.com/JUhalt/PaperRoute-Tracker/issues/25).

- optional local SHA-256 hashing
- clear unchanged / changed / missing status
- integrity checks never modify source files
- managed files participate in portable backup/restore

## v0.4F — Submission integration

**Owner:** [#25](https://github.com/JUhalt/PaperRoute-Tracker/issues/25), coordinated with #26.

- connect the finished packet to the exact submitted manuscript version
- associate the packet with the journal submission/revision round
- submission portal shortcut from the readiness workflow
- preserve explicit user control over when a real submission is recorded

## v0.4G — Readiness UX

**Owners:** [#26](https://github.com/JUhalt/PaperRoute-Tracker/issues/26) for readiness and [#25](https://github.com/JUhalt/PaperRoute-Tracker/issues/25) for packets; shared navigation and keyboard/DPI evidence are part of their acceptance.

- concise readiness summary
- clear unresolved requirements
- packet completeness/integrity status
- direct navigation between manuscript, readiness, packet, and submission
- responsive/high-DPI behavior from the start

## v0.4H — Certification and release

**Owner:** [#42](https://github.com/JUhalt/PaperRoute-Tracker/issues/42).

- Schema 4 → 5 migration
- backup/restore
- linked-file preservation
- managed-packet deletion/recovery safety
- hash determinism
- installed v0.3 → v0.4 update
- clean install
- release packaging/checksums

---

## COMPLETE — v0.5 Reviewer Response Workflow

Milestone: **[v0.5.0 — Reviewer Response Workflow](https://github.com/JUhalt/PaperRoute-Tracker/milestone/7)**. Feature owner: **[#27 Reviewer Response Matrix](https://github.com/JUhalt/PaperRoute-Tracker/issues/27)**. Completed fixes: **[#37 shelf scrolling](https://github.com/JUhalt/PaperRoute-Tracker/issues/37)**, merged in **[PR #51](https://github.com/JUhalt/PaperRoute-Tracker/pull/51)**, and **[#67 literal ampersands](https://github.com/JUhalt/PaperRoute-Tracker/issues/67)**, merged in **[PR #68](https://github.com/JUhalt/PaperRoute-Tracker/pull/68)**. PaperRoute v0.5.0 was released on September 26, 2026 from the certified v0.5.0-rc.2 source.

Own the iterative conversation within a journal submission attempt: reviewer/editor comments, action items, status, manuscript location references, response drafting, revision-round linkage, and editable response-to-reviewers export.

The workflow must remain fully usable without AI.

### Release scope

- Manually add, edit, remove, filter, and reorder reviewer/editor comments and actions within an existing submission.
- Keep each item linked to an existing editorial decision and an explicitly entered revision-round number. Never derive historical rounds from list position or create submission events while drafting responses.
- Track unresolved, in-progress, addressed, and not-applicable states, draft responses, manuscript locations, and notes.
- Export an editable Markdown response draft without changing saved matrix data.
- Preserve the existing child-dialog and final Manuscript Details save/cancel boundaries.
- Introduce conservative Schema 5 to 6 migration, deep cloning, reference validation, and persistence/export regression coverage.

The [user guide](docs/USER_GUIDE.md#reviewer-response-matrix) describes the implemented workflow and its nested save boundaries. The [release notes](docs/releases/0.5.0.md) and [upgrade notes](UPGRADE_NOTES.md) describe Schema 6 and require v0.5 or later when restoring v0.5 backups; older restore code may ignore the new reviewer-response fields.

Release certification included the combined regression suite, native 100%/125%/150% and Light/Dark checks of the response dialogs, installed v0.4-to-v0.5 migration through the real Preview updater, clean installation, portable backup/restore with managed and linked files, and published-asset verification. Evidence is recorded in [#52](https://github.com/JUhalt/PaperRoute-Tracker/issues/52).

### Visual polish brought forward

[#37](https://github.com/JUhalt/PaperRoute-Tracker/issues/37) is closed after [PR #51](https://github.com/JUhalt/PaperRoute-Tracker/pull/51) removed stale horizontal shelf scroll space while preserving vertical scrolling and card actions. The [shelf certification record](ManualCertification/Shelf-Layout-2026-09-20.md) records native 100%/125%/150% checks. Final v0.5 certification also covers the combined application and new response dialogs; the shelf record does not certify those later changes.

---

## COMPLETE — v0.6 Workspace UI

Milestone: **[v0.6.0 — Workspace UI](https://github.com/JUhalt/PaperRoute-Tracker/milestone/8)**

Five releases added depth beneath each manuscript, and the interface grew by stacking dialogs: the application has 47 dialog forms, and reaching a reviewer-response item takes Board → Manuscript Details → Submission Details → Reviewer Responses → item editor. v0.6 keeps every feature and changes where they live.

- **[#54 Single-window navigation](https://github.com/JUhalt/PaperRoute-Tracker/issues/54)** — Board, Library, Journals, Reminders, and Import & Export as pages in one window; the Data and Settings menus return to their proper jobs.
- **[#55 Manuscript workspace page](https://github.com/JUhalt/PaperRoute-Tracker/issues/55)** — a manuscript opens as a tabbed page with one **Save / Discard** boundary instead of nested Save & Close steps.
- **[#56 Submissions master/detail](https://github.com/JUhalt/PaperRoute-Tracker/issues/56)** — decisions, rounds, correspondence, packets, and reviewer responses beside the submission they belong to.
- **[#57 Card-based visual system](https://github.com/JUhalt/PaperRoute-Tracker/issues/57)** — cards instead of group boxes, consistent spacing and button hierarchy, and time-in-stage on board cards from recorded dates only.
- **[#58 Import & Export page](https://github.com/JUhalt/PaperRoute-Tracker/issues/58)** — every way in and out on one page, each explaining what it preserves.
- **[#59 Welcome and empty states](https://github.com/JUhalt/PaperRoute-Tracker/issues/59)** — every empty area says what belongs there and offers the first action.
- **[#60 Faster Add Manuscript](https://github.com/JUhalt/PaperRoute-Tracker/issues/60)** — paste a title page and review locally parsed fields; optionally set a first deadline as an ordinary reminder.

PaperRoute v0.6.0 was released on September 27, 2026 from [PR #75](https://github.com/JUhalt/PaperRoute-Tracker/pull/75), after the v0.6.0-rc.1 Preview. It kept the Schema 6 library, so existing save semantics, lifecycle rules, and data-safety guarantees carried over unchanged. Certification (clean installation, native 100%/125%/150%, installed upgrades through the live Preview and Stable updaters with byte-identical libraries, and published-asset verification) is recorded in [#74](https://github.com/JUhalt/PaperRoute-Tracker/issues/74).

---

## COMPLETE — v0.7 Deadline Center

Milestone: **[v0.7.0 — Deadline Center](https://github.com/JUhalt/PaperRoute-Tracker/milestone/9)**

Issue: **[#28 Deadline Center](https://github.com/JUhalt/PaperRoute-Tracker/issues/28)**

Aggregate revision deadlines, follow-up dates, readiness/submission obligations, reviewer-response work, and manuscript reminders using the existing canonical reminder engine rather than a second scheduling system. The Deadline Center takes over the **Reminders** page in the v0.6 workspace.

- **[#61 Publication check and metadata completion](https://github.com/JUhalt/PaperRoute-Tracker/issues/61)** — user-initiated checks for a possible publication of a tracked manuscript, and batch Crossref completion that fills only empty fields. Status changes only through an explicit **Mark Published** choice.

Decisions settled on September 27, 2026:

- The page is called **Deadlines**, in the rail and as its title, and replaces **Reminders**.
- **Postpone** changes the date on the record that owns it (a submission's follow-up date, a decision's revision deadline, or a reminder's date). There is no separate snooze field, so the Deadlines page and the manuscript page cannot disagree.
- Publication-check results (possible and ignored matches) are remembered. This is v0.7's only storage change: **Schema 7**, with the same conservative migration and backup/restore coverage as Schema 6.

Planned slices, tracked in the v0.7 integration PR:

1. **Obligations in the engine (#28).** Revision rows carry reviewer-response progress from the decision that set the deadline. Undated work appears in a **No date** group: a revision without a deadline, a prepared packet with open checklist items, and possible publications to review. Every item links to the record that owns it; status is deterministic and tested. The journal name is filled for revision deadlines.
2. **The Deadlines page (#28).** Overdue, Today, Next 7 days, Later, No date, and Done groups; kind filters and search; Open, Postpone, Done, and Clear on each row; calendar export; a rail badge for overdue and due-today items; and a link from the board's Needs Attention.
3. **Publication check and fill blanks (#61).** User-initiated Crossref checks by DOI and title, optional ORCID works, review with Mark Published, Ignore, or Review later, and a previewed fill of empty fields only. Schema 7.
4. **Carry-over:** [#76](https://github.com/JUhalt/PaperRoute-Tracker/issues/76).
5. **Certification and release.** Preview, then Stable, using the v0.6 procedure: in-app updater only, an unlocked host for VM checks, and snapshots before each VM step.

PaperRoute v0.7.0 was released on September 28, 2026 from [PR #78](https://github.com/JUhalt/PaperRoute-Tracker/pull/78), after the v0.7.0-rc.1 Preview. It moved the library to Schema 7 with a marker-only upgrade. Certification (published-asset verification, an installed v0.6.0 upgraded through the live Preview updater with byte-identical data files, clean installation, and the live Stable updater) is recorded in [#79](https://github.com/JUhalt/PaperRoute-Tracker/issues/79).

---

## COMPLETE — v0.8 Route Analytics & Reports

Milestone: **[v0.8.0 — Route Analytics & Reports](https://github.com/JUhalt/PaperRoute-Tracker/milestone/10)**

Issue: **[#30 Route statistics and time-to-publication analytics](https://github.com/JUhalt/PaperRoute-Tracker/issues/30)**

Derive transparent local analytics and print-friendly route reports from the same canonical history. Missing dates must produce partial/missing results rather than fabricated values. Analytics becomes a page in the v0.6 workspace.

- **[#62 Your history with each journal](https://github.com/JUhalt/PaperRoute-Tracker/issues/62)** — submissions, outcomes, and turnaround from the researcher's own records, shown on the Journals page.
- **[#63 Shareable pipeline status report](https://github.com/JUhalt/PaperRoute-Tracker/issues/63)** — a local HTML snapshot for a supervisor or coauthor that excludes notes, correspondence, reviewer comments, and file paths by default.
- **[#64 Work types and colored tags](https://github.com/JUhalt/PaperRoute-Tracker/issues/64)** — the dimensions reports, CV exports, and the board can filter and group by. Existing records migrate with an unspecified type rather than an inferred one.
- **[#82 Route map](https://github.com/JUhalt/PaperRoute-Tracker/issues/82)** — each route drawn to scale, colored by who held it (the journal, the author, or production), with every event numbered and explained, and every route side by side on Insights. It shows a student, or a researcher looking back, how much of publishing is waiting and how much is work.

PaperRoute v0.8.0 was released on September 28, 2026 from [PR #80](https://github.com/JUhalt/PaperRoute-Tracker/pull/80) and [PR #90](https://github.com/JUhalt/PaperRoute-Tracker/pull/90), after the v0.8.0-rc.1 Preview. It moved the library to Schema 8 with a marker-only upgrade. Certification (published assets, the live updater from v0.7.0, the installed Schema 8 upgrade, a clean install, and native display checks) is recorded in [#85](https://github.com/JUhalt/PaperRoute-Tracker/issues/85).

---

## COMPLETE — v0.9 Journal Choice & Guidance

Milestone: **[v0.9.0 — Journal Choice & Guidance](https://github.com/JUhalt/PaperRoute-Tracker/milestone/13)**

[PR #94](https://github.com/JUhalt/PaperRoute-Tracker/pull/94), merged to master on October 2, 2026, delivered the journal shortlist and Choosing a Journal guide (#65, #89), the example library (#83), Online services and Work offline (#86), journal facts and metrics (#87), journals that publish work like yours (#88), your citations (#91), and the optional AI assistant (#84 with #29). It moves the library to Schema 9. Reading a journal's author instructions into its checklist ([#95](https://github.com/JUhalt/PaperRoute-Tracker/issues/95)) followed in [PR #100](https://github.com/JUhalt/PaperRoute-Tracker/pull/100), and polish from the acceptance audit ([#96](https://github.com/JUhalt/PaperRoute-Tracker/issues/96)) in [PR #102](https://github.com/JUhalt/PaperRoute-Tracker/pull/102). The packet export with RO-Crate metadata ([#45](https://github.com/JUhalt/PaperRoute-Tracker/issues/45)) was evaluated and then adopted for v0.9 on October 3, 2026, and merged in [PR #103](https://github.com/JUhalt/PaperRoute-Tracker/pull/103).

Help researchers decide where a manuscript should go and what that journal will ask of them, and let anyone new learn the route, before the 1.0 hardening pass. Offline guidance comes first; connected help adds facts and suggestions without making the library depend on the network.

**Connected, but local-first.** Every online feature follows the rules the Crossref publication check already keeps: the user starts it, except the startup update check, which is on by default and can be turned off, lookups are read-only, results are previewed before anything is applied, and anything stored records its source and the date it was retrieved. One settings page lists every service and exactly what it sends, and **Work offline** turns them all off. PaperRoute stays complete without them.

Offline:

- **[#65 Per-manuscript journal shortlist](https://github.com/JUhalt/PaperRoute-Tracker/issues/65)** — Considering, Preferred, Backup, and Ruled out, with the reasons kept on the manuscript and offered again when it is rerouted.
- **[#89 Choosing a journal](https://github.com/JUhalt/PaperRoute-Tracker/issues/89)** — a one-page guide adapted, with attribution, from the Think. Check. Submit. checklist (CC BY 4.0), plus fit questions and your own history with each journal.
- **[#83 Example library for teaching](https://github.com/JUhalt/PaperRoute-Tracker/issues/83)** — a fictional research group's routes to explore, edit, and discard without touching your library.
- **[#45 Packet export with RO-Crate metadata](https://github.com/JUhalt/PaperRoute-Tracker/issues/45)** — adopted for v0.9 on October 3, 2026: **Export Packet...** in the vault makes a local .zip of a packet's chosen files with a checksum list, a summary page, and RO-Crate 1.3 metadata, after showing each file's hidden information. The evaluation and the rules the export follows are in [ManualCertification/v0.9-RO-Crate-Evaluation](ManualCertification/v0.9-RO-Crate-Evaluation/README.md).

Connected, optional, and started by the user:

- **[#86 Online services](https://github.com/JUhalt/PaperRoute-Tracker/issues/86)** — one local-first rule, one settings page, and a **Work offline** switch.
- **[#87 Journal facts from open indexes](https://github.com/JUhalt/PaperRoute-Tracker/issues/87)** — DOAJ and OpenAlex (both CC0): publisher, fees, license, review type, typical time to publication, and links to the journal's aims and scope, author instructions, editorial board, and its sharing policy in Open Policy Finder (linked, never fetched); open citation metrics (2-year mean citedness, h-index, i10-index) fetched from OpenAlex, and proprietary ones (Impact Factor, CiteScore, SJR, SNIP) recorded by the researcher with source and year. Never a combined prestige score.
- **[#88 Journals that publish work like yours](https://github.com/JUhalt/PaperRoute-Tracker/issues/88)** — suggestions backed by recently published papers that match keywords you review; never scores or predictions.
- **[#91 Your citations](https://github.com/JUhalt/PaperRoute-Tracker/issues/91)** — the researcher's own citation record from open data, on request: citations, h-index, i10-index, g-index, m-quotient, and field-weighted citation impact per published manuscript, each with its source, date, and definition.
- **[#84 Optional AI assistant](https://github.com/JUhalt/PaperRoute-Tracker/issues/84)** (with [#29](https://github.com/JUhalt/PaperRoute-Tracker/issues/29)) — off by default, with your own key or a local model: decision letters to the decision and reviewer comments, and drafting help for responses and cover letters. [#95](https://github.com/JUhalt/PaperRoute-Tracker/issues/95) reads a journal's author instructions, pasted by the researcher and never fetched, into proposed checklist requirements, each checked against its source sentence. An AI fit summary was dropped on October 2, 2026: the fit questions (#89) leave that judgment to the researcher, and #88's evidence already shows what each journal publishes.

PaperRoute v0.9.0 was released on October 9, 2026 from [PR #105](https://github.com/JUhalt/PaperRoute-Tracker/pull/105), after the v0.9.0-rc.1 Preview ([PR #104](https://github.com/JUhalt/PaperRoute-Tracker/pull/104)). It moved the library to Schema 9 with a marker-only upgrade. Certification (published assets, the live Preview updater from v0.8.0 with byte-identical data files, a clean install, native display checks, and the live Stable updater) is recorded in [#97](https://github.com/JUhalt/PaperRoute-Tracker/issues/97).

---

## COMPLETE — v0.9.1 Packet File Safety

Milestone: **[v0.9.1 — Packet File Safety](https://github.com/JUhalt/PaperRoute-Tracker/milestone/14)**

A Stable patch for one defect, [#107](https://github.com/JUhalt/PaperRoute-Tracker/issues/107): every save swept the whole managed library and deleted the packet snapshots of manuscripts the open library did not contain, which bites on a second computer sharing Documents, after a reinstall before Restore Backup, and after a library is recovered from an older copy. The sweep now looks only at manuscripts in the library, and startup recovery puts such files back instead of deleting them.

PaperRoute v0.9.1 was released on October 9, 2026 from [PR #163](https://github.com/JUhalt/PaperRoute-Tracker/pull/163). It keeps the Schema 9 library of v0.9.0. Certification (the candidate, the defect reproduced on the installed v0.9.0, the live Stable updater with the fix shown on the installed app, and the published-asset audit) is recorded in [#108](https://github.com/JUhalt/PaperRoute-Tracker/issues/108).

---

## COMPLETE — v0.9.2 1.0 Hardening

Renumbered from v0.9.1 on October 9, 2026, when v0.9.1 became a Stable patch for #107. The bounded work is in [milestone 11](https://github.com/JUhalt/PaperRoute-Tracker/milestone/11), due November 2, 2026, and tracked by [#43](https://github.com/JUhalt/PaperRoute-Tracker/issues/43). It merged in batches of related issues, often several to a pull request: [PR #236](https://github.com/JUhalt/PaperRoute-Tracker/pull/236) took six.

**Scope and evidence tracker:** [#43](https://github.com/JUhalt/PaperRoute-Tracker/issues/43). The bounded work is linked there, and both decisions it asked for are recorded below: sharing ([#155](https://github.com/JUhalt/PaperRoute-Tracker/issues/155)) on October 15, 2026, and signing ([#152](https://github.com/JUhalt/PaperRoute-Tracker/issues/152)) on October 17, with its timing on October 24.

PaperRoute v0.9.2 was released on October 30, 2026 from [PR #251](https://github.com/JUhalt/PaperRoute-Tracker/pull/251), with the fixes that certification asked for in [PR #253](https://github.com/JUhalt/PaperRoute-Tracker/pull/253), [PR #260](https://github.com/JUhalt/PaperRoute-Tracker/pull/260), [PR #261](https://github.com/JUhalt/PaperRoute-Tracker/pull/261), [PR #264](https://github.com/JUhalt/PaperRoute-Tracker/pull/264) and [PR #267](https://github.com/JUhalt/PaperRoute-Tracker/pull/267). It keeps the Schema 9 library of v0.9.0. Certification is recorded in [#147](https://github.com/JUhalt/PaperRoute-Tracker/issues/147), from one ordered checklist, [v0.9.2-Certification-Checklist.md](ManualCertification/v0.9.2-Certification-Checklist.md): the candidate, CI, the budget table and the host's on-screen checks before the tag, then the published-asset audit and the installed-app checks on the published build, with the update gates from v0.9.1 only. Its installed checks closed [#92](https://github.com/JUhalt/PaperRoute-Tracker/issues/92) (E1.2 and E3.1), [#93](https://github.com/JUhalt/PaperRoute-Tracker/issues/93) (E4.1 and E5.1) and [#152](https://github.com/JUhalt/PaperRoute-Tracker/issues/152) (E1.1, with [PR #273](https://github.com/JUhalt/PaperRoute-Tracker/pull/273)) on October 31, 2026, and #147 closed on November 1. [#142](https://github.com/JUhalt/PaperRoute-Tracker/issues/142) and [#213](https://github.com/JUhalt/PaperRoute-Tracker/issues/213) stay open in the v1.0 milestone for the Narrator item its E8.1 failed ([#276](https://github.com/JUhalt/PaperRoute-Tracker/issues/276)).

Systematic burn-down before 1.0:

- speed: instant page switches and no stall on save, lookup, or startup, measured on a large library ([#131](https://github.com/JUhalt/PaperRoute-Tracker/issues/131), [#132](https://github.com/JUhalt/PaperRoute-Tracker/issues/132) and [#133](https://github.com/JUhalt/PaperRoute-Tracker/issues/133), merged in [PR #183](https://github.com/JUhalt/PaperRoute-Tracker/pull/183), [PR #195](https://github.com/JUhalt/PaperRoute-Tracker/pull/195), [PR #200](https://github.com/JUhalt/PaperRoute-Tracker/pull/200), [PR #215](https://github.com/JUhalt/PaperRoute-Tracker/pull/215), [PR #248](https://github.com/JUhalt/PaperRoute-Tracker/pull/248) and [PR #249](https://github.com/JUhalt/PaperRoute-Tracker/pull/249); what #133 leaves reported is under Residual limitations)
- micro-motion: a short cross-fade between pages, hover and press states, honouring Windows' animation setting ([#142](https://github.com/JUhalt/PaperRoute-Tracker/issues/142), merged in [PR #220](https://github.com/JUhalt/PaperRoute-Tracker/pull/220), open for its captures and Narrator pass)
- Windows 11 chrome: rounded corners and the dark title bar on every window, in every theme ([#143](https://github.com/JUhalt/PaperRoute-Tracker/issues/143) and [#188](https://github.com/JUhalt/PaperRoute-Tracker/issues/188), merged in [PR #189](https://github.com/JUhalt/PaperRoute-Tracker/pull/189) and [PR #194](https://github.com/JUhalt/PaperRoute-Tracker/pull/194))
- migration/recovery verification ([#109](https://github.com/JUhalt/PaperRoute-Tracker/issues/109) to [#111](https://github.com/JUhalt/PaperRoute-Tracker/issues/111), [#118](https://github.com/JUhalt/PaperRoute-Tracker/issues/118) to [#126](https://github.com/JUhalt/PaperRoute-Tracker/issues/126), and the chained upgrade test of [#145](https://github.com/JUhalt/PaperRoute-Tracker/issues/145))
- backup/restore certification ([#114](https://github.com/JUhalt/PaperRoute-Tracker/issues/114) to [#116](https://github.com/JUhalt/PaperRoute-Tracker/issues/116), [#226](https://github.com/JUhalt/PaperRoute-Tracker/issues/226); the gates are on [#147](https://github.com/JUhalt/PaperRoute-Tracker/issues/147))
- installer/updater certification, including the install step's first-run time ([#92](https://github.com/JUhalt/PaperRoute-Tracker/issues/92), closed on October 31, 2026, once the certification measured it; [#127](https://github.com/JUhalt/PaperRoute-Tracker/issues/127), [#149](https://github.com/JUhalt/PaperRoute-Tracker/issues/149))
- keyboard/accessibility pass ([#136](https://github.com/JUhalt/PaperRoute-Tracker/issues/136) to [#140](https://github.com/JUhalt/PaperRoute-Tracker/issues/140), [#191](https://github.com/JUhalt/PaperRoute-Tracker/issues/191) to [#193](https://github.com/JUhalt/PaperRoute-Tracker/issues/193), the last in [PR #238](https://github.com/JUhalt/PaperRoute-Tracker/pull/238); [#213](https://github.com/JUhalt/PaperRoute-Tracker/issues/213), merged in [PR #220](https://github.com/JUhalt/PaperRoute-Tracker/pull/220), open for its Narrator pass)
- responsive sizing across dialogs and DPI levels, with one scaling rule for every dialog ([#93](https://github.com/JUhalt/PaperRoute-Tracker/issues/93), merged in [PR #209](https://github.com/JUhalt/PaperRoute-Tracker/pull/209), [PR #212](https://github.com/JUhalt/PaperRoute-Tracker/pull/212), [PR #214](https://github.com/JUhalt/PaperRoute-Tracker/pull/214) and [PR #218](https://github.com/JUhalt/PaperRoute-Tracker/pull/218); confirmed at 150% and 125% by the release certification, [#147](https://github.com/JUhalt/PaperRoute-Tracker/issues/147), and closed on October 31, 2026)
- retain regression coverage for the shelf scrolling fix completed for v0.5 ([#37](https://github.com/JUhalt/PaperRoute-Tracker/issues/37), [PR #51](https://github.com/JUhalt/PaperRoute-Tracker/pull/51)) during the whole-application hardening pass
- consistency audit of every workflow against the v0.6 workspace patterns ([#151](https://github.com/JUhalt/PaperRoute-Tracker/issues/151), recorded in [Workflow-Consistency-v0.9.2.md](ManualCertification/Workflow-Consistency-v0.9.2.md))
- onboarding: the v0.6 welcome and empty states, the v0.9 example library, and the welcome's link to the Quick Start ([#130](https://github.com/JUhalt/PaperRoute-Tracker/issues/130)); the guided tutorial is deferred until after 1.0
- File Drawer revival/rerouting polish, building on the v0.9 journal shortlist ([#65](https://github.com/JUhalt/PaperRoute-Tracker/issues/65); [#202](https://github.com/JUhalt/PaperRoute-Tracker/issues/202) to [#205](https://github.com/JUhalt/PaperRoute-Tracker/issues/205), [#207](https://github.com/JUhalt/PaperRoute-Tracker/issues/207))
- small touches from the little-touches review ([#221](https://github.com/JUhalt/PaperRoute-Tracker/issues/221) to [#232](https://github.com/JUhalt/PaperRoute-Tracker/issues/232), merged in [PR #235](https://github.com/JUhalt/PaperRoute-Tracker/pull/235) to [PR #237](https://github.com/JUhalt/PaperRoute-Tracker/pull/237); the second wave, [#239](https://github.com/JUhalt/PaperRoute-Tracker/issues/239) to [#245](https://github.com/JUhalt/PaperRoute-Tracker/issues/245), merged in [PR #247](https://github.com/JUhalt/PaperRoute-Tracker/pull/247))
- test-suite redundancy/obsolescence audit ([#150](https://github.com/JUhalt/PaperRoute-Tracker/issues/150), merged in [PR #234](https://github.com/JUhalt/PaperRoute-Tracker/pull/234))
- manual certification fixtures for major workflows ([#145](https://github.com/JUhalt/PaperRoute-Tracker/issues/145))
- portable project-sharing decision ([#155](https://github.com/JUhalt/PaperRoute-Tracker/issues/155))
- installer trust/signing decision ([#152](https://github.com/JUhalt/PaperRoute-Tracker/issues/152), with the application in [#216](https://github.com/JUhalt/PaperRoute-Tracker/issues/216))

## Decisions and deferrals

Each decision 1.0 needed, and each default adopted without a decision issue, with its date and the issue that does the work. [#43](https://github.com/JUhalt/PaperRoute-Tracker/issues/43) records the same.

**Installer signing** ([#152](https://github.com/JUhalt/PaperRoute-Tracker/issues/152)), decided on October 17, 2026. The maintainer applies for free code signing for open-source projects, as the [code signing policy](CODE_SIGNING.md) describes, and staying unsigned is the fallback until it is approved. v0.9.2 ships unsigned, and so does 1.0 if approval has not come by then; the User Guide's [Installing](docs/USER_GUIDE.md#installing) section explains the Windows warning and the SHA-256 check. Once signing is approved, signing in the release build, a Stable build refused when Setup's signature is not valid, and a verifier self-test case become their own issue. #152 closed on October 31, 2026, when the release certification ([#147](https://github.com/JUhalt/PaperRoute-Tracker/issues/147)) recorded what Windows shows for a downloaded Setup and the User Guide was corrected to match ([PR #273](https://github.com/JUhalt/PaperRoute-Tracker/pull/273)). On October 24, 2026, the maintainer chose to submit the application once v0.9.2 is published, so the release it points to links the code signing policy from its release notes, as SignPath asks of a release page; that is [#216](https://github.com/JUhalt/PaperRoute-Tracker/issues/216), then in the v1.0 milestone. The maintainer submitted it on October 30, 2026, the day v0.9.2 was published. SignPath declined it on October 31, 2026, for want of public adoption signals, and invited a new application later, so 1.0 ships unsigned. #216 moved to v1.1 with the routes to try next: WinGet, a Microsoft Store (MSIX) build, and a new application once the project has external references.

**Supported versions** ([#153](https://github.com/JUhalt/PaperRoute-Tracker/issues/153)), decided on October 17, 2026. Only the 0.9 releases are supported for updating straight to 1.0. An older version's update check offers 1.0 as well: the migration tests cover that update, but no release certification does, so create a backup with **Settings → Backup Library...** first. Windows 11 x64 is supported; Windows 10 and Windows on ARM may work, but they are not tested. The [upgrade notes](UPGRADE_NOTES.md#supported-versions) and the [security policy](.github/SECURITY.md) say so, the chained upgrade test ([#145](https://github.com/JUhalt/PaperRoute-Tracker/issues/145)) starts from Schema 9, and the release certification's update gates run from the previous Stable: v0.9.2's from v0.9.1 ([#147](https://github.com/JUhalt/PaperRoute-Tracker/issues/147)), and 1.0's from v0.9.2 ([#313](https://github.com/JUhalt/PaperRoute-Tracker/issues/313)).

**Security updates to the bundled runtime** ([#154](https://github.com/JUhalt/PaperRoute-Tracker/issues/154)), adopted as the default on October 15, 2026. The [security policy](.github/SECURITY.md) sets a best-effort target: a patch release within 30 days of a .NET security update that fixes a runtime component PaperRoute ships, reaching users through the update check. The signal comes from [#144](https://github.com/JUhalt/PaperRoute-Tracker/issues/144) (closed): dependency alerts, a vulnerable-package check in every CI build, and the runtime version recorded for each release.

**Sharing** ([#155](https://github.com/JUhalt/PaperRoute-Tracker/issues/155)), adopted as the default on October 15, 2026. Export Packet, the portable backup and the Pipeline and Route reports are the sharing story for 1.0. A whole-project export is not planned, so no implementation issue follows.

- **Audience:** a coauthor or supervisor receives a packet or a report; a backup moves a library between its owner's own computers.
- **Files:** a packet's chosen files; a report's summary; a backup's whole library.
- **Private metadata:** Export Packet shows each file's hidden document information before export; reports leave out notes, correspondence, reviewer comments and file paths; a backup carries everything and is not for sharing, as the User Guide says under [How do I share a packet's files or keep a copy?](docs/USER_GUIDE.md#how-do-i-share-a-packets-files-or-keep-a-copy).
- **Recovery:** restore from a backup, which first makes an emergency ZIP of the library it replaces.

**The accessibility claim** ([#156](https://github.com/JUhalt/PaperRoute-Tracker/issues/156)), decided on October 17, 2026. 1.0 claims a keyboard-complete interface, with every input named for screen readers and Esc pressing each dialog's Cancel or Close, all enforced by sweep tests ([#136](https://github.com/JUhalt/PaperRoute-Tracker/issues/136), [#137](https://github.com/JUhalt/PaperRoute-Tracker/issues/137), [#139](https://github.com/JUhalt/PaperRoute-Tracker/issues/139)), plus a recorded Narrator pass and the User Guide's [Accessibility](docs/USER_GUIDE.md#accessibility) section, which says what was tested. Windows contrast themes are supported ([#138](https://github.com/JUhalt/PaperRoute-Tracker/issues/138)), with a pass by hand under one dark and one light contrast theme recorded on the release certification ([#147](https://github.com/JUhalt/PaperRoute-Tracker/issues/147)). It is not a formal claim of conformance to an accessibility standard, which would need an audit a one-person project cannot back.

**Onboarding** ([#130](https://github.com/JUhalt/PaperRoute-Tracker/issues/130)), adopted as the default on October 9, 2026 (decision 6 of the v0.9.1 plan). Onboarding for 1.0 is the welcome and empty states ([#59](https://github.com/JUhalt/PaperRoute-Tracker/issues/59)), the example library ([#83](https://github.com/JUhalt/PaperRoute-Tracker/issues/83)) and the welcome's link to the Quick Start; the guided tutorial moves to after 1.0.

**Portable.zip** ([#149](https://github.com/JUhalt/PaperRoute-Tracker/issues/149)), adopted as the default on October 9, 2026 (decision 7 of the v0.9.1 plan). Releases no longer ship a Portable.zip: "portable" suggested that the library travels with the folder, which it does not, and the file was never launched in certification. The CI smoke-test build stays.

## Residual limitations

What 1.0 knowingly leaves as it is, for [#44](https://github.com/JUhalt/PaperRoute-Tracker/issues/44) to cite, each with where it is recorded.

- **Dates PaperRoute makes up on import.** When a spreadsheet import has no submission date, PaperRoute stores the day of the import, and with no decision date it stores the submission date; the record's notes say so ([#126](https://github.com/JUhalt/PaperRoute-Tracker/issues/126)). Crossref, ORCID, BibTeX and RIS imports store a year-only or year-and-month date as the first day of that year or month. Insights counts all of these as written and cannot tell them from recorded dates, so figures that include imported records are approximate, as the User Guide's [Insights and Reports](docs/USER_GUIDE.md#insights-and-reports) says for spreadsheet imports. A marker for a date's precision is the first Schema 10 step, after 1.0 ([#157](https://github.com/JUhalt/PaperRoute-Tracker/issues/157), deferred on [#43](https://github.com/JUhalt/PaperRoute-Tracker/issues/43)).
- **Windows that keep fixed pixel sizes or scale by hand** ([#93](https://github.com/JUhalt/PaperRoute-Tracker/issues/93)). At 125% and 150% display scaling, the text in these windows grows with the display, while their sizes grow only where the code scales them by hand. With all of #93's scaling fixes merged, and Delete Manuscript moved onto `DialogScaling` for 1.0.0-rc.2, 32 windows besides the main window set no 96-DPI base dimensions:
  - the add and edit windows for affiliations, authors, manuscript authors, correspondence and files, external links, journal submissions, reminders, reviewer comments, and packet files;
  - five that are also shown inside other windows: the manuscript page, **Submission Details**, **Reviewer Response Matrix**, **Authors & Affiliations**, and **Journal Library**;
  - **Add Manuscript**, **Choose Journal**, **Export BibTeX** and **Export RIS**, **Export Reviewer Responses**, **Fill Blanks from Crossref**, **Import BibTeX** and **Import RIS**, **Mark as Published**, **Move to File Drawer**, **ORCID Public Profile**, **Paste a Title Page**, the **Pipeline Report** and **Route Report** previews, **PaperRoute Preferences**, the **PaperRoute User Guide** window, **Publication & CV Export**, **Readiness Notes**, **Route View**, **Submission Packet Vault**, and **Submission Readiness**.

  The 17 windows on #93's one scaling rule, `DialogScaling`, scale with the display: **About PaperRoute**, **Add Journal to Shortlist** and **Shortlisted Journal**, **Add Journal** and **Edit Journal**, **Add Manuscript Version** and **Edit Manuscript Version**, **Add Readiness Requirement** and **Edit Readiness Requirement**, **Check for Publications**, **Delete Manuscript**, **DOI & Crossref Metadata**, the Deadlines page's postpone window, the error window, **Journal, Preprint & Project Links**, **Map Spreadsheet Columns**, **New Submission Packet** and **Edit Submission Packet**, **PaperRoute could not start**, **PaperRoute Diagnostics**, **PaperRoute Update Available**, and **Updating PaperRoute**.

  The real-display sessions of October 23 and 24, 2026, recorded on #93, opened every window at 150% on a 1920x1080 screen and at 125% on a 1366x768 one, and each window they found cut has been fixed. At 150%, Route View opens at its 100% size, so its route map's key uses one column, and Preferences opens at about its 125% size, and its content scrolls. The release certification confirmed both on the published v0.9.2, with all 63 windows ok at each ([#147](https://github.com/JUhalt/PaperRoute-Tracker/issues/147), E4.1 and E5.1), and #93 closed on October 31, 2026. The 1.0 certification's capture at a real 150% ([#313](https://github.com/JUhalt/PaperRoute-Tracker/issues/313), C3, November 6, 2026) found six windows that still cut text, each unchanged since v0.9.2, and 1.0.0-rc.2 fixes them: the main window's search fields, the help icons in windows Windows Forms does not scale, **Delete Manuscript**'s warning, **Map Spreadsheet Columns**' grid, **Add Author**'s notes, and the affiliations list in **Add Manuscript Author**.
- **Screens 1.0 certifies** ([#93](https://github.com/JUhalt/PaperRoute-Tracker/issues/93)). 1.0 is checked at 1920x1080 up to 150% and at 1366x768 up to 125%, as #93's plan amendment A settled. 1366x768 at 150% is not certified: there, Add Metric's buttons sit below the fold, and two of Paste a Title Page's boxes are 1 and 3 px short of two lines ([PR #218](https://github.com/JUhalt/PaperRoute-Tracker/pull/218)).
- **What Windows draws in Dark** ([#106](https://github.com/JUhalt/PaperRoute-Tracker/issues/106)). In PaperRoute's Dark appearance, five surfaces keep the colors Windows gives them, or PaperRoute's Light ones, and none of them hides anything that has to be read: the report preview, the borders of number boxes, the route map's journal and author segments and numbered events, the links in the guide's window, and the outlines of text boxes, lists and drop-down lists. The User Guide's [What Windows draws](docs/USER_GUIDE.md#what-windows-draws) lists them, and the Dark tests' allow-list (`DarkAllowList` in `PaperRoute.Tests/DarkThemeLegibilityTests.vb`) holds the sweep and the guide to that list.
- **A contrast theme switched while PaperRoute runs.** Contrast themes are supported ([#138](https://github.com/JUhalt/PaperRoute-Tracker/issues/138)), but turning a Windows contrast theme on or off while PaperRoute is open leaves some colors as they were until PaperRoute restarts: the rail and the Board mix the old and the new colors. Restart PaperRoute after switching.
- **The Board's first seconds on a large library** ([#133](https://github.com/JUhalt/PaperRoute-Tracker/issues/133), [PR #200](https://github.com/JUhalt/PaperRoute-Tracker/pull/200)). The Board shows its first screen of cards at once and fills in the rest while the window is idle, about five seconds for 500 manuscripts. Until it finishes, Tab going forward shows each next row, but Shift+Tab into the shelf from outside it, the shelf's scroll range and a screen reader's object navigation end at the last card shown so far. The User Guide's [Screen readers](docs/USER_GUIDE.md#screen-readers) section says so. In the same idle time the window prepares the manuscript page, Journals, Insights and, from [PR #249](https://github.com/JUhalt/PaperRoute-Tracker/pull/249), the Library, so that their first visits are quick; how long the steps of that work take is under the next entry.
- **What #133 leaves reported** ([#133](https://github.com/JUhalt/PaperRoute-Tracker/issues/133), [PR #248](https://github.com/JUhalt/PaperRoute-Tracker/pull/248), [PR #249](https://github.com/JUhalt/PaperRoute-Tracker/pull/249)). On the 500-manuscript test library, the budget test holds every page and shelf switch it times, the manuscript-page opens among them, to its 100 ms budget but one. Under the rule the first visits to Deadlines and Import & Export already followed, which the maintainer applied on October 26, 2026 to the rows enforced that day, a failure on a busy computer is run again on a quiet one before it counts as a regression: beside another test run, a held row has read more than 100 ms (another manuscript on the kept page, 117 ms once). As the maintainer decided that day, #133 closes with three things timed and reported, not held, recorded here and in the [responsiveness record](ManualCertification/Responsiveness-After-2026-10-16.md):
  - **opening a Pipeline manuscript with shortlisted journals and tags**: 57 to 86 ms in PR #248's seven runs on a quiet computer and 96 and 135 ms in its two beside another test run, with no room on either. What is left of it is its tag chips' windows, one per tag, and the Overview's nested self-sizing tables, which measure their children again whenever the record changes what they hold; laid out again with nothing moving, the Overview takes 11 to 18 ms. A drawn Overview is the lever after 1.0;
  - **the paint after a switch or an open**, by the maintainer's earlier decision: it holds the screen's own wait, which has read up to about half a second on the test computer, where a paint otherwise takes 10 to 50 ms;
  - **the idle slice**: the Board's fill and the pages' preparation (above) are queued in 30 ms slices, but their longest step read 52 to 127 ms in the runs of October 26, 2026. Each step is held under the 250 ms budget for work on the window's UI thread.
- **Removed files stay on disk.** When a manuscript, a version or a packet file is deleted and the library saved, PaperRoute moves its managed copies into the dated `removed` folder inside the PaperRoute Library folder ([#110](https://github.com/JUhalt/PaperRoute-Tracker/issues/110)). Nothing empties that folder: the files stay on disk, and in a synced Documents folder, until the user deletes them. See [Removed files](docs/USER_GUIDE.md#removed-files).
- **Edits lost when PaperRoute is stopped outright.** The recovery draft ([#123](https://github.com/JUhalt/PaperRoute-Tracker/issues/123)) keeps the open manuscript page's unsaved edits when Windows shuts down or an error ends the session, and the next start offers them. Nothing is written when PaperRoute is stopped without running its own code, such as by a power cut or by ending its process, and there is no timed autosave, so the edits made since the last **Save** are lost then.
- **The edges of micro-motion** ([#142](https://github.com/JUhalt/PaperRoute-Tracker/issues/142), [PR #220](https://github.com/JUhalt/PaperRoute-Tracker/pull/220)). Opening a manuscript page is always a cut, not a fade, by design. PaperRoute is system-aware, so on a monitor whose scale differs from the one Windows signed in with, a page switch is a plain cut; a real pair of monitors at different scales, and a window spanning two monitors, are not tested. The fade's picture is read from the screen, so for up to 120 ms, after a wait of at most about 60 ms for it, it can include a notification or another app's window that overlapped the page, and it is placed from a worker thread with a call Microsoft does not document as safe there; if that call fails, the switch is a cut. If the window stops responding for 5 seconds or more while a fade shows, UI Automation describes the fade through its MSAA proxy, and an MSAA client that walks top-level windows can list it as an unnamed, unavailable window, which never takes focus.
- **File Drawer edges** ([#151](https://github.com/JUhalt/PaperRoute-Tracker/issues/151), [#207](https://github.com/JUhalt/PaperRoute-Tracker/issues/207)). Restore to Pipeline is only on the Board card's **⋯** menu, not on the manuscript page or in the Library, and after it the Board stays on the File Drawer tab; the status line says where the manuscript went ([#225](https://github.com/JUhalt/PaperRoute-Tracker/issues/225)). A manuscript that a rejection takes out of the File Drawer can be offered "consider filing" again, and an unsaved edit to the File Drawer reason is dropped when a recorded submission or decision takes the manuscript out ([PR #210](https://github.com/JUhalt/PaperRoute-Tracker/pull/210)). The [workflow walk](ManualCertification/Workflow-Consistency-v0.9.2.md) records the first two as File Drawer polish.
- **The first start after installing** ([#92](https://github.com/JUhalt/PaperRoute-Tracker/issues/92)). Setup's last step can still take longer than Velopack's 30-second wait on a slow or busy computer; Setup then says **Install Partially Succeeded**, though PaperRoute is installed, as the User Guide's [Installing](docs/USER_GUIDE.md#installing) section says. v0.9.2 ships English resources only, 52 fewer files to scan. Its certification timed the step on a clean Windows 11 test computer: the worst of five clean installs took 8.28 s with Defender on and 7.79 s with it off, under the 15-second rule, and #92 closed on October 31, 2026 ([#147](https://github.com/JUhalt/PaperRoute-Tracker/issues/147)).
- **Smaller edges recorded in review:**
  - correspondence records removed from a live submission are never swept, so their managed copies stay in the library folder; a path under `removed` counts as managed until the first save; and when a restore cannot carry `removed` across, the old library stays as `PaperRoute Library.restore-rollback-<id>`, which the result dialog does not mention ([PR #164](https://github.com/JUhalt/PaperRoute-Tracker/pull/164));
  - when a Windows shutdown is canceled, the action it interrupted carries on, and a second Save Error box can follow a shutdown's retry of a failed save ([PR #183](https://github.com/JUhalt/PaperRoute-Tracker/pull/183));
  - at the main window's minimum size, 960x680, the Correspondence & Files page on the manuscript page scrolls by about 105 px, and with the Unsaved changes bar showing and a seven-button item chosen, its toolbar's last 6 px need a scroll ([PR #233](https://github.com/JUhalt/PaperRoute-Tracker/pull/233));
  - the Insights route chart raises focus events only, with no selection events, and a route has no default action, so a screen reader's own "do default action" does nothing; Enter and a click open the route ([PR #238](https://github.com/JUhalt/PaperRoute-Tracker/pull/238));
  - after an **Add Author** whose save failed, with the author kept on screen, an ORCID import in the same window whose author save also fails leaves the works it saved linked to an author that never reached disk; both failures are reported ([PR #247](https://github.com/JUhalt/PaperRoute-Tracker/pull/247)).
- **The installer is not code-signed** ([#152](https://github.com/JUhalt/PaperRoute-Tracker/issues/152)). Your browser and Windows may warn before Setup runs. The User Guide's [What Windows shows](docs/USER_GUIDE.md#what-windows-shows) explains the warning and how to check the download's SHA-256 hash, and the [code signing policy](CODE_SIGNING.md) says what will be signed once a signing application is approved. The application to the SignPath Foundation, submitted on October 30, 2026, was declined on October 31 for want of public adoption signals; 1.0 ships unsigned, and [#216](https://github.com/JUhalt/PaperRoute-Tracker/issues/216), in v1.1, holds the routes to try next.
- **Deferred until after 1.0** ([#43](https://github.com/JUhalt/PaperRoute-Tracker/issues/43), October 9, 2026): the guided tutorial, which has no issue yet; date precision and "unknown date" markers, as the first Schema 10 step ([#157](https://github.com/JUhalt/PaperRoute-Tracker/issues/157)); dated history copies, a restore journal, and a single shared library validator, none of which is built; and, for v1.1, macOS and Linux on one cross-platform interface ([#160](https://github.com/JUhalt/PaperRoute-Tracker/issues/160)), composition-grade motion with the translucent half of the Windows 11 chrome ([#162](https://github.com/JUhalt/PaperRoute-Tracker/issues/162)), and the fewer, calmer surfaces pass ([#161](https://github.com/JUhalt/PaperRoute-Tracker/issues/161)). The workflow walk ([#151](https://github.com/JUhalt/PaperRoute-Tracker/issues/151), October 18, 2026) also moved two things to v1.1: access keys for the 46 windows that have none, since Tab, Enter and Esc already reach every control, which #160's parity list carries, and the Journals and Authors editors' own button rows, which wait for the v1.1 design simplification. A drawn Overview (above) is unscheduled and has no issue yet.

---

# v1.0 Trusted Research Workflow

**Release-readiness tracker:** [#44](https://github.com/JUhalt/PaperRoute-Tracker/issues/44), in [milestone 12](https://github.com/JUhalt/PaperRoute-Tracker/milestone/12), due November 9, 2026. 1.0 is certified by the standing procedure in [Release-Certification.md](ManualCertification/Release-Certification.md) ([#146](https://github.com/JUhalt/PaperRoute-Tracker/issues/146), [PR #171](https://github.com/JUhalt/PaperRoute-Tracker/pull/171)), and v0.9.2's certification ([#147](https://github.com/JUhalt/PaperRoute-Tracker/issues/147)) is the rehearsal for the 1.0 record. 1.0 is published only with the maintainer's sign-off on #44.

Status: v1.0.0-rc.1 is the Preview candidate; v0.9.2 is the current Stable release. Certification and release are tracked in [#313](https://github.com/JUhalt/PaperRoute-Tracker/issues/313), from one ordered checklist, [v1.0-Certification-Checklist.md](ManualCertification/v1.0-Certification-Checklist.md): the rc on Preview first, then Stable v1.0.0 with the rc's code.

Besides the readiness record, the milestone holds what v0.9.2's certification left for 1.0: the dialog polish its C3 review noted ([#266](https://github.com/JUhalt/PaperRoute-Tracker/issues/266)) and the hint it found cut at 150% ([#278](https://github.com/JUhalt/PaperRoute-Tracker/issues/278)); the on-screen fade test's setup ([#268](https://github.com/JUhalt/PaperRoute-Tracker/issues/268)); pinning the .NET SDK so the release bundles the runtime the candidate tested ([#269](https://github.com/JUhalt/PaperRoute-Tracker/issues/269)); Narrator with page fades on ([#276](https://github.com/JUhalt/PaperRoute-Tracker/issues/276)), which #213 and #142 wait on; a restore into OneDrive ([#295](https://github.com/JUhalt/PaperRoute-Tracker/issues/295)); closing on a large library ([#300](https://github.com/JUhalt/PaperRoute-Tracker/issues/300)); what a Preview install is offered ([#274](https://github.com/JUhalt/PaperRoute-Tracker/issues/274)); starting fresh ([#296](https://github.com/JUhalt/PaperRoute-Tracker/issues/296)); and the candidate's display-scaling mode ([#302](https://github.com/JUhalt/PaperRoute-Tracker/issues/302)). v0.9.2's certification, [#147](https://github.com/JUhalt/PaperRoute-Tracker/issues/147), closed on November 1, 2026, after its installed checks closed #92, #93 and #152.

PaperRoute 1.0 is not defined by feature count.

> **I trust this application with my research workflow.**

The 1.0 bar includes stable migrations, reliable installer/updater behavior, curated regression coverage, recovery tooling, proven backup/restore, an accessible keyboard-complete UI, transparent local analytics, coherent manuscript/submission/decision/revision/version relationships, clear privacy boundaries, and fully certified release artifacts.

For 1.0, accessible means what [#156](https://github.com/JUhalt/PaperRoute-Tracker/issues/156) decided and the User Guide's [Accessibility](docs/USER_GUIDE.md#accessibility) section says was tested: keyboard-complete, with every input named for screen readers and Esc pressing each dialog's Cancel or Close, all enforced by tests, a recorded Narrator pass, and Windows contrast themes supported.

PaperRoute does not trade trustworthiness for cadence.

---

# v1.1 macOS and Linux

Milestone: **[v1.1 — macOS and Linux](https://github.com/JUhalt/PaperRoute-Tracker/milestone/15)**

One cross-platform interface on Avalonia, whose GPU compositor brings the motion and effects a WinForms app cannot, with the models, services, storage, migrations, and tests carried over; the fewer, calmer surfaces pass that goes with a rewritten interface; and Velopack packaging on three platforms. The migration is decided and planned there, not before 1.0, and 1.0.x on WinForms stays the Stable line until the Avalonia build replaces it.

Its issues are:
- deciding and planning the Avalonia migration ([#160](https://github.com/JUhalt/PaperRoute-Tracker/issues/160));
- composition-grade motion and effects ([#162](https://github.com/JUhalt/PaperRoute-Tracker/issues/162));
- the fewer, calmer surfaces pass ([#161](https://github.com/JUhalt/PaperRoute-Tracker/issues/161));
- date precision and "unknown date" markers, Schema 10 ([#157](https://github.com/JUhalt/PaperRoute-Tracker/issues/157));
- offering Stable releases to people on the Preview channel ([#158](https://github.com/JUhalt/PaperRoute-Tracker/issues/158));
- running the official RO-Crate validator once on a golden export ([#159](https://github.com/JUhalt/PaperRoute-Tracker/issues/159));
- a **Settings > Start Fresh...** command that sets the library aside and begins empty, with a backup and an undo ([#297](https://github.com/JUhalt/PaperRoute-Tracker/issues/297));
- code signing after SignPath's decline: WinGet, a Microsoft Store (MSIX) build, or a new application once the project has external references ([#216](https://github.com/JUhalt/PaperRoute-Tracker/issues/216)).

---

# v1.2 Open Science Studies

Milestone: **[v1.2 — Open Science Studies](https://github.com/JUhalt/PaperRoute-Tracker/milestone/16)**. Tracker: **[#254](https://github.com/JUhalt/PaperRoute-Tracker/issues/254)**.

PaperRoute's route begins at the manuscript, but research begins earlier. A study is planned, preregistered and approved; its data are collected and analysed; then it feeds one paper or several. v1.2 adds the study above the manuscript, so a study can move out of a folder tree into PaperRoute. What the study shares goes to the open-science site the researcher chooses.

On November 1, 2026, the proposal was scoped into fourteen bounded issues. OSF's changes shaped it ([COS](https://www.cos.io/osf-changes)):
- from November 16, 2026, no new OSF projects can be created;
- from February 19, 2027, projects are read-only;
- OSF Registries carry on unchanged.

The home for open materials and data is moving; preregistration on OSF is not.

```text
Study
├─ Ethics approvals, funder, sharing commitment, licence
├─ Preregistration
│  ├─ guided plan (van 't Veer & Giner-Sorolla, 2016), frozen and fingerprinted
│  ├─ amendments, each a new frozen version
│  └─ registry record (OSF Registries, AsPredicted, PsychArchives, a Registered Report)
├─ Deviations log → transparency statement at submission
├─ Materials · Data · Code (participant data linked, never copied in)
├─ Sharing records (any site; through a connector, drafts only)
└─ Manuscripts it feeds → each with its own route
```

- **The record.**
  - Studies in the library, under Schema 11 ([#279](https://github.com/JUhalt/PaperRoute-Tracker/issues/279)).
  - Study files: managed, linked or metadata only, fingerprinted, and found again when moved ([#280](https://github.com/JUhalt/PaperRoute-Tracker/issues/280)).
  - The Studies page and the study workspace ([#281](https://github.com/JUhalt/PaperRoute-Tracker/issues/281)).
  - Studies brought in from a folder tree or an OSF download ([#283](https://github.com/JUhalt/PaperRoute-Tracker/issues/283)).
- **The plan.**
  - A guided preregistration, frozen and fingerprinted, or an existing one recorded ([#284](https://github.com/JUhalt/PaperRoute-Tracker/issues/284)).
  - The deviations log and a transparency statement ([#287](https://github.com/JUhalt/PaperRoute-Tracker/issues/287)).
  - Ethics approvals and data-sharing dates in Deadlines ([#288](https://github.com/JUhalt/PaperRoute-Tracker/issues/288)).
- **The data.** Participant data stays outside the library, and never enters an export, an upload or a shared report ([#282](https://github.com/JUhalt/PaperRoute-Tracker/issues/282)).
- **Your open-science home.**
  - Any site works through a study export as an RO-Crate ([#285](https://github.com/JUhalt/PaperRoute-Tracker/issues/285)) and a recorded deposit.
  - **Check deposit** and **Upload a new version** work under one contract that never publishes, submits or deletes on a site ([#286](https://github.com/JUhalt/PaperRoute-Tracker/issues/286)).
  - Three connectors:
    - OSF Registries, for preregistrations ([#289](https://github.com/JUhalt/PaperRoute-Tracker/issues/289));
    - InvenioRDM, for Zenodo and institutional repositories ([#290](https://github.com/JUhalt/PaperRoute-Tracker/issues/290));
    - Dataverse ([#291](https://github.com/JUhalt/PaperRoute-Tracker/issues/291)).
- **Teaching.** An example study in the example library ([#292](https://github.com/JUhalt/PaperRoute-Tracker/issues/292)).

**Deferred:**
- a timestamp of a frozen plan's fingerprint;
- Route View starting at the study;
- connectors without a draft-only path or an upload API: Figshare, Dryad and SWORD. ResearchBox, AsPredicted and PsychArchives are reached by export and a recorded link.

Reference management stays with tools such as Zotero. v1.2 follows v1.1, so its interface is built on Avalonia.

---

# Proposals awaiting scope review

None at present. Open-science Studies ([#254](https://github.com/JUhalt/PaperRoute-Tracker/issues/254)) was scoped into v1.2 on November 1, 2026.

The proposals reviewed on September 28, 2026 are scoped into v0.9, and #45 was adopted for v0.9 on October 3, 2026.

New research-informed ideas remain proposals until scoped. Every accepted finding or development path must have a linked issue and a roadmap disposition before implementation.
