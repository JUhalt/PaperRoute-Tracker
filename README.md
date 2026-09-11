<p align="center">
  <img src="docs/paperroute-logo.png" width="160" alt="PaperRoute logo">
</p>

# PaperRoute

[![Build PaperRoute Tracker](https://github.com/JUhalt/PaperRoute-Tracker/actions/workflows/build.yml/badge.svg)](https://github.com/JUhalt/PaperRoute-Tracker/actions/workflows/build.yml)
[![Latest Release](https://img.shields.io/github/v/release/JUhalt/PaperRoute-Tracker)](https://github.com/JUhalt/PaperRoute-Tracker/releases/latest)
[![License: GPL-3.0-only](https://img.shields.io/badge/License-GPL--3.0--only-blue.svg)](LICENSE.txt)

**A local-first academic manuscript tracker for researchers.**

> **Track • Submit • Publish**

PaperRoute Tracker helps researchers manage manuscripts from idea through submission, peer review, revision, publication—or the File Drawer—without requiring an account or sending the core workflow database to a cloud service.

**Current stable release:** [v0.3.0 — The Route](https://github.com/JUhalt/PaperRoute-Tracker/releases/tag/v0.3.0). **Active development:** [v0.4.0 — Submission Readiness](https://github.com/JUhalt/PaperRoute-Tracker/milestone/6). See the [roadmap](ROADMAP.md) for planned work and its issue owners.


**New to PaperRoute?** Start with the [`PaperRoute User Guide`](docs/USER_GUIDE.md) for a Quick Start, feature tour, and task-oriented "How do I...?" reference.

### DOI & Crossref enrichment
PaperRoute can normalize a DOI or doi.org link, retrieve metadata from Crossref, preview the response, and apply only user-selected fields. Crossref never changes manuscript stage, shelf/location, or target journal. Structured authors are matched to the reusable author library by ORCID or name before new records are proposed/created.

### ORCID public-profile import
From **Data → Authors & Affiliations**, PaperRoute can read a selected author's public ORCID record, preview public identity data, employment affiliations, and works, and apply only the items the user chooses. ORCID lookup is read-only and user initiated; PaperRoute stores no ORCID password, OAuth token, or client secret.

A successful public lookup confirms that the ORCID iD exists in the registry, but PaperRoute does not treat that as proof that the record holder authenticated the iD to PaperRoute. Selected dated works can be imported directly to the Published shelf when the user explicitly chooses that behavior; undated works remain Ideas. Imported works are deduplicated by DOI first and exact title second.

### BibTeX & RIS interchange
PaperRoute can import standard BibTeX (`.bib`) and RIS (`.ris`) bibliography files with a review-before-import workflow. Common title, author, DOI, journal/outlet, publication date, volume, issue, pages, publisher, URL, abstract, and keyword metadata are mapped into the schema-2 manuscript model. Duplicate detection uses DOI first and normalized title second.

Unsupported or ambiguous source fields are shown as warnings rather than silently discarded. Imported structured authors are matched against the reusable author library before new people are created. Bibliographic publication metadata does not fabricate journal-submission history.

PaperRoute can also export any user-selected manuscripts to BibTeX or RIS for use with reference managers and scholarly tools.

### Journal library, portals & related links
PaperRoute can store reusable journal records alongside the reusable author/affiliation metadata library. Journal records may include publisher, homepage, submission portal, notes, favorite status, and shortlist status. Manuscripts can link a target journal to one of these reusable records while retaining the existing free-text target-journal field for backward compatibility.

The manuscript Links editor also stores preprint DOI/URL data and labeled project-style web links such as OSF projects, preregistrations, data repositories, or publisher pages. PaperRoute opens only valid `http://` or `https://` links and stores **no publisher passwords or credentials**.

Journal submissions may be seeded from the reusable journal library. A reusable journal link helps fill the journal and portal fields, but PaperRoute continues to preserve submission-specific history rather than inventing events from journal metadata.

### Publication & CV exports
**Data → Publication & CV Export...** creates human-readable publication output from PaperRoute metadata without modifying manuscript records. Exports can be filtered to Published records, Accepted/In Press/Published records, or the complete manuscript library, then narrowed to individually selected records.

Output is available as plain text, Markdown, or HTML in publication-list or CV-section style. Structured authors are used when available; legacy author text remains a fallback. DOI, journal, volume, issue, pages, publication URL, and preprint information are included when present, while incomplete records still produce editable output.

### Reminders, Windows notifications & calendar export
**Settings → Reminders & Calendar...** combines revision deadlines, journal-submission follow-up dates, and custom manuscript reminders into one deterministic local view. Custom reminders can be added, edited, and completed without a cloud service.

Active reminders can be exported as a portable `.ics` calendar for Outlook, Google Calendar, Apple Calendar, and other iCalendar-compatible tools. Optional Windows notifications are disabled by default and checked when PaperRoute starts; notification failure never blocks the in-app reminder workflow.

### Built-in User Guide
The **Help** button in the main PaperRoute header (and **Settings → User Guide...**) opens the current user guide maintained in [`docs/USER_GUIDE.md`](docs/USER_GUIDE.md). Installed and portable builds ship a local copy so core help remains available offline.

## Current status

**v0.3.0** - current stable release, **The Route**.

PaperRoute v0.3 adds the canonical manuscript-history layer: Visual Route View, manuscript Version History, workflow-linked versions, immutable managed snapshots, chronology/provenance tracking, and safer recovery behavior.

Development is now targeting **v0.4.0 - Submission Readiness**, including per-journal readiness checklists, exact submission packets, file-integrity tracking, and linkage between the files actually submitted and the manuscript/version/journal round they belong to. Stable users remain on the published v0.3.x release line unless they intentionally opt into Preview builds.

## Highlights

- **Pipeline, Published, and File Drawer shelves** for the complete manuscript lifecycle.
- **Journal submission history** with manuscript numbers, dates, notes, and publisher portal links.
- **Editorial decisions** including rejection, revision, acceptance, and revision deadlines.
- **Correspondence and local-file tracking** for decision letters, reviewer comments, response letters, revised manuscripts, and related material.
- **Needs Attention dashboard** for overdue revisions, long reviews, missing target journals, and recent rejections.
- **Search, stage filtering, and sorting** across the manuscript library.
- **Reusable authors and affiliations** with manuscript-specific order, corresponding-author designation, optional ORCID, and preserved legacy author text.
- **ORCID public-profile import** for user-reviewed names, affiliations, and works, with explicit Published-vs-Idea control for imported records.
- **Reusable journal library** with favorites/shortlists, publisher homepages, submission portals, and manuscript target links.
- **Preprint and project links** for OSF-style resources and other manuscript web destinations, without storing publisher credentials.
- **Publication & CV exports** to editable plain text, Markdown, and HTML.
- **Local reminders and calendar export** for revision deadlines, submission follow-ups, custom reminders, optional Windows notifications, and portable `.ics` events.
- **Built-in User Guide** with Quick Start, feature discovery, and task-oriented help.
- **Light, Dark, and Follow Windows themes** using modern .NET 10 WinForms theming.
- **Excel import/export** using the PaperRoute workbook format.
- **Legacy tracker import** for the original development spreadsheet format.
- **Column-mapping import wizard** for arbitrary spreadsheets whose headings do not match PaperRoute.
- **Portable ZIP backup and restore** with an emergency pre-restore backup.
- **Local-first storage** and a managed local document library.

## Privacy and local-first design

PaperRoute is designed so that its core manuscript-tracking workflow works offline. No PaperRoute account is required.

Current installed/portable PaperRoute application data is stored under `%LocalAppData%\PaperRoute\`.

Visual Studio debugger launches use an isolated development profile under `%LocalAppData%\PaperRoute-Dev\`, with managed copies in `Documents\PaperRoute Dev Library\`. This prevents experimental schema or UI work from modifying the stable library.

The manuscript database, automatic backup, settings, and storage-schema metadata are stored in the active profile. Managed document copies are stored in the corresponding PaperRoute managed library.

When legacy ManuscriptPipeline storage is migrated, PaperRoute retains the legacy source data where possible for rollback and recovery rather than silently deleting it.

## Installing PaperRoute

### Recommended: GitHub Release installer

On the GitHub **Releases** page, download and run the PaperRoute Setup executable from the latest stable release. Installed builds can then check GitHub Releases for future PaperRoute updates.

Fresh installations default to the **Stable** update channel. Users who intentionally want prerelease builds can choose **Preview** under **Settings → Preferences... → Update channel**. Use **Settings → Check for Updates...** to check immediately.

### Portable CI build

The repository's **Build PaperRoute Tracker** workflow still produces a self-contained `PaperRouteTracker-win-x64` artifact for smoke testing. Portable/developer builds intentionally do not perform in-place automatic updates; install PaperRoute using the Setup program to test the updater.

## Importing existing work

Choose **Data → Import Spreadsheet...**.

PaperRoute uses three import paths automatically:

### 1. Standard PaperRoute workbook

Use **Data → Get Import Template...** to generate the supported multi-sheet workbook. It contains:

- `Manuscripts`
- `Submissions`
- `Decisions`
- `Correspondence`

This is the best format for loss-minimized round-trip import/export.

### 2. Legacy tracker

PaperRoute recognizes the original development tracker when it contains the expected legacy columns such as `TITLE`, `JOURNAL`, `SUBMITTED`, `RESPONSE`, and `STATUS`.

### 3. Map your spreadsheet

If PaperRoute does not recognize either known format, it opens a column-mapping wizard. You can map headings such as:

```text
Paper Name       → Title
Authors          → Co-authors
Outlet           → Submission journal
Date Sent        → Submitted date
Current Status   → Current stage
Outcome          → Editorial decision
Decision Date    → Decision date
Comments         → Notes
```

Only **Title** is required. PaperRoute auto-suggests mappings from common academic spreadsheet headings and shows sample values before import.

## Backup and restore

Choose **Data → Backup Library...** to create a portable ZIP containing:

```text
backup-info.txt
manuscripts.json
authors.json        (when reusable author metadata exists)
library.xlsx
files\
```

Managed document copies are included in the backup. Reusable author, affiliation, and journal metadata is included when present. Externally linked files remain references to their original paths.

**Restore Backup...** validates the archive, previews record/file counts, asks for explicit confirmation, creates an emergency backup of the current library, and then restores the selected archive.

## File Drawer

PaperRoute treats **Published** and **File Drawer** as terminal shelves, while still allowing a filed manuscript to be restored to the active Pipeline. The configurable File Drawer suggestion threshold is intended as a prompt, not an automatic decision.

## Building from source

Requirements:

- Windows 11 recommended
- .NET 10 SDK
- Visual Studio 2026 or another environment capable of building VB.NET WinForms projects

Clone the repository and build:

```powershell
git clone https://github.com/JUhalt/PaperRoute-Tracker.git
cd PaperRoute-Tracker
dotnet restore ManuscriptPipeline.slnx
dotnet build ManuscriptPipeline.slnx --configuration Release
```

The internal project/folder name remains `ManuscriptPipeline` for compatibility and to avoid unnecessary namespace churn. The built assembly is `PaperRouteTracker.exe`.

## Technology

- Visual Basic .NET
- .NET 10 Windows Forms
- ClosedXML for Excel workbook support
- System.Text.Json for local persistence
- GitHub Actions for Windows CI and release builds
- Velopack for Windows installation and automatic updates

## Inspiration and independence

PaperRoute was inspired by the broader idea of academic manuscript pipeline tools, including the workflow concepts presented by PaperTrek. PaperRoute is an independent open-source project and is not affiliated with or endorsed by PaperTrek.

The implementation, local-first data model, import/export system, backup workflow, and interface are independently developed for PaperRoute.

## Roadmap

See [`ROADMAP.md`](ROADMAP.md) for the public high-level route. GitHub milestones and issues are the live source of truth for active release work.

## Contributing

Bug reports, usability feedback, importer edge cases, and pull requests are welcome. See [`CONTRIBUTING.md`](CONTRIBUTING.md).

## License

PaperRoute Tracker is licensed under the **GNU General Public License, version 3 only** (SPDX: `GPL-3.0-only`). See [`LICENSE.txt`](LICENSE.txt) for the complete license and [`NOTICE.md`](NOTICE.md) for the project-specific notice.
