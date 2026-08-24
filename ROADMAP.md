# PaperRoute Roadmap

PaperRoute is moving toward one connected goal:

> **A trustworthy, local-first record of how an academic manuscript actually moves from working draft to publication.**

The roadmap is organized around that workflow rather than isolated features. Each release should deepen the same canonical manuscript route without creating parallel sources of truth.

The live GitHub milestones and issues remain the source of truth for active work. This file explains how those pieces fit together.

## The workflow model

PaperRoute treats the manuscript as the long-lived research object.

```text
Manuscript
├─ Version History
│  ├─ working versions
│  ├─ submitted snapshots
│  └─ revised versions
│
├─ Submission Attempt — Journal A
│  ├─ submitted manuscript version
│  ├─ editorial decision
│  ├─ correspondence / decision letter / reviewer files
│  ├─ revision round 1
│  │  ├─ reviewer-response work
│  │  └─ revised manuscript version
│  ├─ editorial decision
│  ├─ revision round 2
│  └─ accepted / rejected / withdrawn
│
└─ Submission Attempt — Journal B
   └─ begins only when the manuscript is genuinely rerouted
```

A normal revision **does not create a new submission attempt**. It remains part of the journal interaction that produced the decision. A later Revise & Resubmit may require a new publisher portal/manuscript ID in the real world; PaperRoute should preserve that continuity explicitly rather than pretending it is unrelated history.

External integrations may suggest metadata, but they must never silently rewrite authoritative manuscript history, lifecycle state, or version relationships.

---

## Release train

Targets are directional rather than promises. Data integrity, migrations, backup/recovery, and release certification take precedence over cadence.

| Release | Theme | Workflow question |
| --- | --- | --- |
| **v0.1** | Reliable Core ✅ | Can I trust PaperRoute with local manuscript data? |
| **v0.2** | Metadata & Integrations ✅ | Can PaperRoute describe and connect my research accurately? |
| **v0.3** | **The Route** — active | What happened to this manuscript, in what order, and which file was which? |
| **v0.4** | Submission Readiness | What exactly am I preparing and sending to this journal? |
| **v0.5** | Reviewer Response Workflow | What did the journal ask me to change, and how am I responding? |
| **v0.6** | Deadline Center | What requires action, and when? |
| **v0.7** | Route Analytics & Reports | What does this publication journey show me—and how can I communicate it? |
| **v0.8** | Optional AI Assistance | Can AI reduce clerical work without becoming authoritative? |
| **v0.9** | 1.0 Hardening | Is the entire workflow polished, resilient, and certifiable? |
| **v1.0** | Trusted Research Workflow | Would I trust this with my real publication pipeline? |
| **v1.x** | Ecosystem & Interoperability | How can PaperRoute exchange data without compromising local-first control? |
| **v2.0+** | Next-generation exploration | Which larger architectural improvements are justified by real use? |

---

## COMPLETE — v0.1 Reliable Core

PaperRoute v0.1 established the trusted local core:

- installation and updates
- versioned storage
- schema validation and conservative migration
- recovery and backups
- import/export
- accessibility and high-DPI groundwork
- diagnostics and release hardening

---

## COMPLETE — v0.2 Metadata & Integrations

PaperRoute v0.2 added the reusable research metadata layer:

- structured authors and affiliations
- DOI / Crossref metadata enrichment
- ORCID public-profile import and one-way sync
- BibTeX and RIS import/export
- Journal Library and submission portal shortcuts
- preprint / journal-version relationships and project links
- publication and CV exports
- reminders, calendar export, optional Windows notifications
- Help and User Guide
- stable updater/release certification

**Safety rule:** integrations can assist, but cannot silently alter authoritative lifecycle history.

---

# ACTIVE — v0.3 The Route

Issues: **#23 Visual Route View** and **#24 Manuscript Version History**

v0.3 establishes the canonical historical spine that later releases build upon.

## Chronology and provenance

- [x] Deterministic Route projection from canonical manuscript data
- [x] Schema 3 version-history model
- [x] Schema 4 chronology/audit provenance
- [x] Separate real-world event dates from `RecordedAtUtc` / `LastModifiedAtUtc`
- [x] Same-day deterministic ordering without allowing later edits to reshuffle history
- [x] No fabricated audit timestamps for legacy records

## Version history

- [x] Immutable managed manuscript-version snapshots
- [x] Linked external manuscript-version files
- [x] Metadata-only historical versions
- [x] Current-version identity
- [x] Version ↔ submission association
- [x] Version ↔ editorial-decision association
- [x] Revision-round metadata
- [x] Version History UI
- [x] Add / edit metadata / open file / set current workflows
- [x] Managed snapshots remain physically immutable after commit
- [ ] Transactional version deletion with managed-file orphan prevention

## Visual Route

- [x] Read-only visual timeline
- [x] Submission, decision, version, reroute, File Drawer, and current-state waypoints
- [x] Suppression of redundant workflow-derived stage cards
- [x] Responsive timeline and deterministic same-day ordering
- [ ] Route drill-down into the authoritative Manuscript Details record
- [ ] Final route terminology/help polish

## Manual certification

PaperRoute will maintain reusable, conspicuously named manual-certification fixtures. These are **additive** imports and never overwrite the user's existing library.

The fixture convention is:

```text
ZZZ-CERT-<release>-<case>
```

This allows repeatable F5 certification without turning a user's real manuscripts into test specimens.

---

# v0.4 Submission Readiness

Issues: **#25 Submission Packet Vault** and **#26 Per-journal Readiness Checklists**

v0.4 turns a manuscript version into a concrete journal submission package.

Planned flow:

```text
Current manuscript version
        ↓
Choose target journal / submission attempt
        ↓
Readiness checklist
        ↓
Submission Packet
├─ exact manuscript snapshot
├─ cover letter
├─ figures / tables / supplements
├─ required statements / metadata
└─ journal-specific supporting files
        ↓
Record Journal Submission
```

Key principles:

- the packet points to the **exact version actually sent**
- packet files are locally controlled and integrity-checked
- journal-specific readiness belongs to the submission attempt, not globally to the manuscript
- preparing a packet must not silently change lifecycle state

---

# v0.5 Reviewer Response Workflow

Issue: **#27 Reviewer Response Matrix**

This release owns the iterative conversation **within a journal submission attempt**.

```text
Submission Attempt — Journal A
        ↓
Decision: Major Revision
        ↓
Reviewer Response Matrix
├─ Reviewer 1 comments
├─ Reviewer 2 comments
├─ Editor requests
├─ manuscript location/page references
├─ response drafts
└─ addressed / unresolved / not applicable
        ↓
Revision round 1
├─ revised manuscript version
├─ response-to-reviewers document
└─ supporting correspondence/files
        ↓
Resubmit within Journal A
```

Planned capabilities:

- reviewer/editor identity or label
- individual comments/action items
- status and response drafting
- links to the correct submission, decision, and revision round
- manuscript location/page/section references
- exportable human-editable response-to-reviewers draft
- multiple reviewers and multiple rounds kept distinct
- continuation semantics for true publisher-side "Revise & Resubmit" cases where a new portal ID is issued but the intellectual journal relationship continues

The workflow must be fully usable without AI.

---

# v0.6 Deadline Center

Issue: **#28 Deadline Center**

The Deadline Center consolidates obligations already encoded elsewhere:

- revision deadlines
- follow-up dates
- submission/readiness deadlines
- reviewer-response work
- other manuscript-specific reminders

It builds on the canonical reminder engine rather than creating a second scheduling system.

---

# v0.7 Route Analytics & Reports

Issue: **#30 Route statistics and time-to-publication analytics**

The same canonical Route projection should power both analytics and human-readable reporting.

Planned analytics:

- days in major workflow stages
- number of journals / submission attempts
- reroute count
- review durations
- revision durations
- time from first draft/submission to publication
- clearly partial results when historical dates are missing

Planned reporting:

- print-friendly per-manuscript Route Report
- manuscript versions, submissions, decisions, revision rounds, and outcomes
- visually clear but restrained route timeline
- export suitable for archiving or sharing
- optional anonymized / teaching-oriented presentation mode

The teaching/reporting use case is intentional: publication is often nonlinear, and a clean route can help students see rejection, revision, reviewer response, and rerouting as normal parts of scholarly work.

Analytics and reports remain local and deterministic.

---

# v0.8 Optional AI Assistance

Issue: **#29 Optional AI-assisted reviewer action extraction**

AI enters only after the human-controlled reviewer workflow exists.

Potential uses:

- extract candidate reviewer action items
- suggest mapping comments into the Reviewer Response Matrix
- summarize repeated reviewer themes
- assist with clerical organization

Rules:

- opt-in
- preview-before-apply
- no silent lifecycle changes
- no AI dependency for core reviewer workflow
- stored manuscript history remains authoritative

---

# v0.9 1.0 Hardening

v0.9 is the systematic burn-down before 1.0:

- migration and recovery verification
- backup/restore certification
- installer/updater certification
- keyboard and accessibility pass
- responsive sizing across dialogs and DPI levels
- consistent master/detail patterns
- revisit the current Manuscript Details section-jump navigator versus true tabs if growing workflow complexity makes vertical navigation harder to understand; preserve the current layout while it remains clearer
- theme and secondary-dialog polish
- Route / Version terminology and contextual help
- evaluate a guided interactive onboarding/tutorial that reuses the same contextual-help catalog rather than maintaining separate explanations
- File Drawer revival/rerouting polish
- test-suite redundancy/obsolescence audit
- manual certification fixtures for major workflows
- portable project-sharing format decision and any safe groundwork
- installer trust/signing decision

---

# v1.0 Trusted Research Workflow

PaperRoute 1.0 is not defined by feature count.

> **I trust this application with my research workflow.**

The 1.0 bar includes:

- stable, versioned data model and proven migrations
- reliable installer and updater
- strong but curated automated regression coverage
- recovery tooling and proven backup/restore
- accessible keyboard-first UI
- transparent local Route analytics
- complete rerouting / File Drawer workflow
- coherent manuscript → submission → decision → revision → version relationships
- clear privacy boundaries and no silent external transmission of manuscript content
- release artifacts, checksums, documentation, and upgrade path verified before publication

A target date may slip when certification exposes a data-loss, migration, recovery, packaging, updater, or authoritative-history defect. PaperRoute does not trade trustworthiness for cadence.

---

# Beyond 1.0

## v1.x Ecosystem & Interoperability

Possible directions, driven by actual user demand:

- richer portable project exchange
- interoperable exports
- controlled integrations with research/document workflows
- improved batch/library operations
- additional reporting surfaces

## v2.0+ Exploration

No v2 feature is promised merely because it sounds impressive.

Exploration may include larger architectural changes only when they preserve PaperRoute's core trust model:

- local-first ownership
- explicit user control
- transparent history
- recoverable storage
- no hidden authoritative automation

The route remains the product's organizing idea.
