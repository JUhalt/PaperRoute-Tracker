# Changelog

All notable changes to PaperRoute Tracker will be documented here.

## [Unreleased] — v0.4 Submission Readiness

The release target is **v0.4.0**, currently an **unreleased candidate**. Development is tracked in [draft PR #40](https://github.com/JUhalt/PaperRoute-Tracker/pull/40) and the [v0.4 milestone](https://github.com/JUhalt/PaperRoute-Tracker/milestone/6). The current stable release remains v0.3.0. See the [candidate release notes](docs/releases/0.4.0.md) and [development checkpoint](ManualCertification/v0.4-Development-Checkpoint.md) for validation evidence and outstanding release gates.

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
- Installed upgrade, clean install, display-scaling, and artifact certification are tracked in [release gate #42](https://github.com/JUhalt/PaperRoute-Tracker/issues/42). This entry does not claim publication or completion of those gates.

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
