# PaperRoute upgrade notes

PaperRoute is designed to preserve the local research library across application upgrades.

## Installed builds

For normal upgrades, use an installed PaperRoute build rather than replacing application files manually.

- **Stable** receives stable releases.
- **Preview** receives prereleases such as alpha and release-candidate builds.
- Automatic update checks can be enabled or disabled in PaperRoute settings.
- Manual update checks are available from **Settings → Check for Updates...**.

Portable and developer builds intentionally do not replace themselves in place.

## Before upgrading

PaperRoute upgrades are designed not to overwrite the manuscript database, settings, schema metadata, managed document library, or externally linked files. Even so, creating a current PaperRoute backup before an important upgrade is a sensible precaution.

Use **Data → Backup Library...** to create a portable backup.

## Preparing to move from v0.3 to v0.4

**v0.4.0 is the current Stable release.** Installed v0.3-to-v0.4 migration and clean-install behavior were certified before Stable publication. The migration moves compatible Schema 4 libraries to Schema 5 while preserving existing manuscript history and managed/linked files.

v0.4 uses storage **schema 5**. The schema-4-to-5 migration adds readiness profiles, submission packets, and reusable journal checklist templates while retaining existing manuscripts, versions, submissions, and chronology. Older records can have empty preparation histories; migration does not invent checklists, submitted files, or submission events.

Before upgrading, create a portable ZIP backup with v0.3 and retain it separately. A v0.4 library is not intended to be opened directly by v0.3, so keep that pre-upgrade backup if you need to return to the older application.

After upgrading, confirm the existing manuscript and version history first. Then try the new preparation workflow on a disposable manuscript: create a readiness profile, associate a packet with an exact version, save the child dialog, and choose **Save & Close** in Manuscript Details. Reopen the manuscript to confirm the saved associations.

v0.4 ZIP backups preserve readiness, journal templates, packet associations, saved fingerprints, and PaperRoute-managed packet/version snapshots. Externally linked originals remain external references and must be retained separately. The Excel workbook alone does not preserve this complete workflow.

## Legacy ManuscriptPipeline data

PaperRoute can migrate compatible legacy ManuscriptPipeline storage into the current PaperRoute storage layout.

The migration is intentionally conservative: legacy source data is retained where practical for rollback and recovery rather than silently deleted.

The internal Visual Basic project/folder name remains `ManuscriptPipeline` for compatibility. This does not change the user-facing application name or current PaperRoute storage location.

## After upgrading

Confirm the following:

1. PaperRoute opens normally.
2. Your manuscript count and shelves look correct.
3. A few manuscript, submission, decision, and correspondence records open as expected.
4. **Diagnostics** reports the expected application version and storage schema.
5. Your managed document links still open.
6. A fresh backup can be created successfully.

If PaperRoute reports that local storage cannot be validated safely, do not delete or replace the data files. Preserve the reported files and use the diagnostics/recovery information to investigate the problem.
