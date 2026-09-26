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
| **v0.5** | **Reviewer Response Workflow — active** | What did the journal ask me to change, and how am I responding? |
| **v0.6** | **Workspace UI — next** | Can I find and act on everything from one calm, uncluttered workspace? |
| **v0.7** | Deadline Center | What requires action, and when? |
| **v0.8** | Route Analytics & Reports | What does this publication journey show me—and how can I communicate it? |
| **v0.9** | 1.0 Hardening | Is the entire workflow polished, resilient, and certifiable? |
| **v1.0** | Trusted Research Workflow | Would I trust this with my real publication pipeline? |

On September 26, 2026, the train was re-sequenced so the interface can catch up with the depth of the data model: v0.6 became **Workspace UI**, Deadline Center moved to v0.7, Route Analytics & Reports moved to v0.8, and optional AI assistance became a proposal for after 1.0. Deadlines and Analytics arrive as pages in the new workspace rather than as additional dialogs.

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

The narrow/high-DPI shelf scrollbar follow-up from v0.3, [#37](https://github.com/JUhalt/PaperRoute-Tracker/issues/37), was brought forward into v0.5 and completed in [PR #51](https://github.com/JUhalt/PaperRoute-Tracker/pull/51). It is included in the v0.5 candidate.

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

# ACTIVE — v0.5 Reviewer Response Workflow

Milestone: **[v0.5.0 — Reviewer Response Workflow](https://github.com/JUhalt/PaperRoute-Tracker/milestone/7)**. Feature owner: **[#27 Reviewer Response Matrix](https://github.com/JUhalt/PaperRoute-Tracker/issues/27)**. Completed fixes: **[#37 shelf scrolling](https://github.com/JUhalt/PaperRoute-Tracker/issues/37)**, merged in **[PR #51](https://github.com/JUhalt/PaperRoute-Tracker/pull/51)**, and **[#67 literal ampersands](https://github.com/JUhalt/PaperRoute-Tracker/issues/67)**, merged in **[PR #68](https://github.com/JUhalt/PaperRoute-Tracker/pull/68)**. The final candidate is v0.5.0-rc.2.

Own the iterative conversation within a journal submission attempt: reviewer/editor comments, action items, status, manuscript location references, response drafting, revision-round linkage, and editable response-to-reviewers export.

The workflow must remain fully usable without AI.

### Release scope

- Manually add, edit, remove, filter, and reorder reviewer/editor comments and actions within an existing submission.
- Keep each item linked to an existing editorial decision and an explicitly entered revision-round number. Never derive historical rounds from list position or create submission events while drafting responses.
- Track unresolved, in-progress, addressed, and not-applicable states, draft responses, manuscript locations, and notes.
- Export an editable Markdown response draft without changing saved matrix data.
- Preserve the existing child-dialog and final Manuscript Details save/cancel boundaries.
- Introduce conservative Schema 5 to 6 migration, deep cloning, reference validation, and persistence/export regression coverage.

The [user guide](docs/USER_GUIDE.md#reviewer-response-matrix) describes the implemented workflow and its nested save boundaries. The [candidate release notes](docs/releases/0.5.0.md) and [upgrade notes](UPGRADE_NOTES.md) describe Schema 6 and require v0.5 or later when restoring v0.5 backups; older restore code may ignore the new reviewer-response fields.

v0.4.0 remains the current Stable release. Before v0.5 publication, complete combined regression tests, native keyboard/theme/100%/125%/150% checks, v0.4-to-v0.5 installed migration, clean installation, updater checks, and package/checksum verification. Record the evidence, merge the release work, and reconcile version metadata before tagging. Close the milestone after the release is published.

### Visual polish brought forward

[#37](https://github.com/JUhalt/PaperRoute-Tracker/issues/37) is closed after [PR #51](https://github.com/JUhalt/PaperRoute-Tracker/pull/51) removed stale horizontal shelf scroll space while preserving vertical scrolling and card actions. The [shelf certification record](ManualCertification/Shelf-Layout-2026-09-20.md) records native 100%/125%/150% checks. Final v0.5 certification also covers the combined application and new response dialogs; the shelf record does not certify those later changes.

---

# v0.6 Workspace UI

Milestone: **[v0.6.0 — Workspace UI](https://github.com/JUhalt/PaperRoute-Tracker/milestone/8)**

Five releases added depth beneath each manuscript, and the interface grew by stacking dialogs: the application has 47 dialog forms, and reaching a reviewer-response item takes Board → Manuscript Details → Submission Details → Reviewer Responses → item editor. v0.6 keeps every feature and changes where they live.

- **[#54 Single-window navigation](https://github.com/JUhalt/PaperRoute-Tracker/issues/54)** — Board, Library, Journals, Reminders, and Import & Export as pages in one window; the Data and Settings menus return to their proper jobs.
- **[#55 Manuscript workspace page](https://github.com/JUhalt/PaperRoute-Tracker/issues/55)** — a manuscript opens as a tabbed page with one **Save / Discard** boundary instead of nested Save & Close steps.
- **[#56 Submissions master/detail](https://github.com/JUhalt/PaperRoute-Tracker/issues/56)** — decisions, rounds, correspondence, packets, and reviewer responses beside the submission they belong to.
- **[#57 Card-based visual system](https://github.com/JUhalt/PaperRoute-Tracker/issues/57)** — cards instead of group boxes, consistent spacing and button hierarchy, and time-in-stage on board cards from recorded dates only.
- **[#58 Import & Export page](https://github.com/JUhalt/PaperRoute-Tracker/issues/58)** — every way in and out on one page, each explaining what it preserves.
- **[#59 Welcome and empty states](https://github.com/JUhalt/PaperRoute-Tracker/issues/59)** — every empty area says what belongs there and offers the first action.
- **[#60 Faster Add Manuscript](https://github.com/JUhalt/PaperRoute-Tracker/issues/60)** — paste a title page and review locally parsed fields; optionally set a first deadline as an ordinary reminder.

v0.6 is planned without a storage-schema change. Existing save semantics, lifecycle rules, and data-safety guarantees carry over unchanged; only their presentation moves.

---

# v0.7 Deadline Center

Milestone: **[v0.7.0 — Deadline Center](https://github.com/JUhalt/PaperRoute-Tracker/milestone/9)**

Issue: **[#28 Deadline Center](https://github.com/JUhalt/PaperRoute-Tracker/issues/28)**

Aggregate revision deadlines, follow-up dates, readiness/submission obligations, reviewer-response work, and manuscript reminders using the existing canonical reminder engine rather than a second scheduling system. The Deadline Center takes over the **Reminders** page in the v0.6 workspace.

- **[#61 Publication check and metadata completion](https://github.com/JUhalt/PaperRoute-Tracker/issues/61)** — user-initiated checks for a possible publication of a tracked manuscript, and batch Crossref completion that fills only empty fields. Status changes only through an explicit **Mark Published** choice.

---

# v0.8 Route Analytics & Reports

Milestone: **[v0.8.0 — Route Analytics & Reports](https://github.com/JUhalt/PaperRoute-Tracker/milestone/10)**

Issue: **[#30 Route statistics and time-to-publication analytics](https://github.com/JUhalt/PaperRoute-Tracker/issues/30)**

Derive transparent local analytics and print-friendly route reports from the same canonical history. Missing dates must produce partial/missing results rather than fabricated values. Analytics becomes a page in the v0.6 workspace.

- **[#62 Your history with each journal](https://github.com/JUhalt/PaperRoute-Tracker/issues/62)** — submissions, outcomes, and turnaround from the researcher's own records, shown on the Journals page.
- **[#63 Shareable pipeline status report](https://github.com/JUhalt/PaperRoute-Tracker/issues/63)** — a local HTML snapshot for a supervisor or coauthor that excludes notes, correspondence, reviewer comments, and file paths by default.
- **[#64 Work types and colored tags](https://github.com/JUhalt/PaperRoute-Tracker/issues/64)** — the dimensions reports, CV exports, and the board can filter and group by. Existing records migrate with an unspecified type rather than an inferred one.

---

# v0.9 1.0 Hardening

**Scope and evidence tracker:** [#43](https://github.com/JUhalt/PaperRoute-Tracker/issues/43). Create or link bounded work before implementation and record explicit decisions for sharing/signing.

Systematic burn-down before 1.0:

- migration/recovery verification
- backup/restore certification
- installer/updater certification
- keyboard/accessibility pass
- responsive sizing across dialogs and DPI levels
- retain regression coverage for the shelf scrolling fix completed for v0.5 ([#37](https://github.com/JUhalt/PaperRoute-Tracker/issues/37), [PR #51](https://github.com/JUhalt/PaperRoute-Tracker/pull/51)) during the whole-application hardening pass
- consistency audit of every workflow against the v0.6 workspace patterns
- guided onboarding/tutorial using the shared contextual-help catalog, building on the v0.6 welcome and empty states
- File Drawer revival/rerouting polish, including a decision on the per-manuscript journal shortlist proposal ([#65](https://github.com/JUhalt/PaperRoute-Tracker/issues/65))
- test-suite redundancy/obsolescence audit
- manual certification fixtures for major workflows
- portable project-sharing decision
- installer trust/signing decision

---

# v1.0 Trusted Research Workflow

**Future release-readiness tracker:** [#44](https://github.com/JUhalt/PaperRoute-Tracker/issues/44). Detailed scope and certification procedures follow the v0.9 review; the milestone is a planning placeholder with no release date.

PaperRoute 1.0 is not defined by feature count.

> **I trust this application with my research workflow.**

The 1.0 bar includes stable migrations, reliable installer/updater behavior, curated regression coverage, recovery tooling, proven backup/restore, accessible keyboard-first UI, transparent local analytics, coherent manuscript/submission/decision/revision/version relationships, clear privacy boundaries, and fully certified release artifacts.

PaperRoute does not trade trustworthiness for cadence.

---

# Proposals awaiting scope review

- [#29 — Optional AI-assisted reviewer action extraction (after 1.0)](https://github.com/JUhalt/PaperRoute-Tracker/issues/29): moved out of the pre-1.0 train on September 26, 2026. AI extraction is most valuable once the Reviewer Response Matrix is comfortable to use inline (v0.6). Its safeguards are retained: opt-in, preview-before-apply, non-authoritative, never required, and no manuscript or reviewer content silently transmitted.
- [#65 — Per-manuscript journal shortlist](https://github.com/JUhalt/PaperRoute-Tracker/issues/65): an ordered list of candidate journals per manuscript (Considering, Preferred, Backup, Ruled out) that can offer the next reroute target. Reviewed with v0.9 rerouting polish.
- [#45 — Evaluate an optional RO-Crate export for submission packets](https://github.com/JUhalt/PaperRoute-Tracker/issues/45): explore whether self-describing, locally exported packets help preserve selected files and version context outside PaperRoute. The issue cites the research paper and official specification, defines a synthetic evaluation, and requires a documented adopt/defer/reject decision. It is not assigned to a release and does not replace backup/restore.

New research-informed ideas remain proposals until scoped. Every accepted finding or development path must have a linked issue and a roadmap disposition before implementation.
