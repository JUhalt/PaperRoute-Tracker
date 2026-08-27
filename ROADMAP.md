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

---

## Release train

Targets are directional rather than promises. Data integrity, migrations, backup/recovery, and release certification take precedence over cadence.

| Release | Theme | Workflow question |
| --- | --- | --- |
| **v0.1** | Reliable Core ✅ | Can I trust PaperRoute with local manuscript data? |
| **v0.2** | Metadata & Integrations ✅ | Can PaperRoute describe and connect my research accurately? |
| **v0.3** | The Route ✅ | What happened to this manuscript, in what order, and which file was which? |
| **v0.4** | **Submission Readiness — active** | What exactly am I preparing and sending to this journal? |
| **v0.5** | Reviewer Response Workflow | What did the journal ask me to change, and how am I responding? |
| **v0.6** | Deadline Center | What requires action, and when? |
| **v0.7** | Route Analytics & Reports | What does this publication journey show me—and how can I communicate it? |
| **v0.8** | Optional AI Assistance | Can AI reduce clerical work without becoming authoritative? |
| **v0.9** | 1.0 Hardening | Is the entire workflow polished, resilient, and certifiable? |
| **v1.0** | Trusted Research Workflow | Would I trust this with my real publication pipeline? |

---

## COMPLETE — v0.1 Reliable Core

PaperRoute v0.1 established the trusted local core: installation/update infrastructure, versioned storage, conservative migrations, recovery/backups, import/export, diagnostics, accessibility, and high-DPI groundwork.

## COMPLETE — v0.2 Metadata & Integrations

PaperRoute v0.2 added structured authors and affiliations, DOI/Crossref and ORCID workflows, BibTeX/RIS exchange, Journal Library, external research links, publication/CV exports, reminders/calendar support, notifications, Help, and the User Guide.

**Safety rule:** integrations can assist, but cannot silently alter authoritative lifecycle history.

## COMPLETE — v0.3 The Route

Issues: **#23 Visual Route View** and **#24 Manuscript Version History**

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

Known cosmetic follow-up: some narrow/high-DPI manuscript shelves can still display an unnecessary horizontal scrollbar even though controls remain reachable. Track this as a hardening issue rather than reopening v0.3.

---

# ACTIVE — v0.4 Submission Readiness

Issues:

- **#25 Submission Packet Vault and file integrity**
- **#26 Per-journal readiness checklists**
- **new foundation issue:** Schema 5 readiness and submission-packet foundation

v0.4 turns a manuscript version into a concrete, reconstructable journal submission package.

## Planned flow

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

- reusable per-journal templates
- required files/statements/metadata
- journal-specific notes and formatting requirements
- template editing without silently changing historical manuscript completion state

## v0.4C — Manuscript-specific readiness

- apply a journal template to a manuscript preparation
- explainable readiness derived from individual items
- complete / not applicable / unresolved state
- readiness is advisory and never blocks recording a real submission

## v0.4D — Submission Packet Vault

- assemble the exact files intended for a submission
- preserve file roles and packet membership
- managed copies and intentional external links
- reconstruct a packet without guessing which files belonged to it

## v0.4E — File integrity

- optional local SHA-256 hashing
- clear unchanged / changed / missing status
- integrity checks never modify source files
- managed files participate in portable backup/restore

## v0.4F — Submission integration

- connect the finished packet to the exact submitted manuscript version
- associate the packet with the journal submission/revision round
- submission portal shortcut from the readiness workflow
- preserve explicit user control over when a real submission is recorded

## v0.4G — Readiness UX

- concise readiness summary
- clear unresolved requirements
- packet completeness/integrity status
- direct navigation between manuscript, readiness, packet, and submission
- responsive/high-DPI behavior from the start

## v0.4H — Certification and release

- Schema 4 → 5 migration
- backup/restore
- linked-file preservation
- managed-packet deletion/recovery safety
- hash determinism
- installed v0.3 → v0.4 update
- clean install
- release packaging/checksums

---

# v0.5 Reviewer Response Workflow

Issue: **#27 Reviewer Response Matrix**

Own the iterative conversation within a journal submission attempt: reviewer/editor comments, action items, status, manuscript location references, response drafting, revision-round linkage, and editable response-to-reviewers export.

The workflow must remain fully usable without AI.

---

# v0.6 Deadline Center

Issue: **#28 Deadline Center**

Aggregate revision deadlines, follow-up dates, readiness/submission obligations, reviewer-response work, and manuscript reminders using the existing canonical reminder engine rather than a second scheduling system.

---

# v0.7 Route Analytics & Reports

Issue: **#30 Route statistics and time-to-publication analytics**

Derive transparent local analytics and print-friendly route reports from the same canonical history. Missing dates must produce partial/missing results rather than fabricated values.

---

# v0.8 Optional AI Assistance

Issue: **#29 Optional AI-assisted reviewer action extraction**

AI remains opt-in, preview-before-apply, non-authoritative, and unnecessary for core workflows. No manuscript/reviewer content is silently transmitted externally.

---

# v0.9 1.0 Hardening

Systematic burn-down before 1.0:

- migration/recovery verification
- backup/restore certification
- installer/updater certification
- keyboard/accessibility pass
- responsive sizing across dialogs and DPI levels
- eliminate phantom horizontal shelf scrolling at narrow/high-DPI widths
- consistent master/detail patterns
- guided onboarding/tutorial using the shared contextual-help catalog
- File Drawer revival/rerouting polish
- test-suite redundancy/obsolescence audit
- manual certification fixtures for major workflows
- portable project-sharing decision
- installer trust/signing decision

---

# v1.0 Trusted Research Workflow

PaperRoute 1.0 is not defined by feature count.

> **I trust this application with my research workflow.**

The 1.0 bar includes stable migrations, reliable installer/updater behavior, curated regression coverage, recovery tooling, proven backup/restore, accessible keyboard-first UI, transparent local analytics, coherent manuscript/submission/decision/revision/version relationships, clear privacy boundaries, and fully certified release artifacts.

PaperRoute does not trade trustworthiness for cadence.
