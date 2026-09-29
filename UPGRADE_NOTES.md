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

Use **Settings → Backup Library...** to create a portable backup. In v0.5 and earlier, it is **Data → Backup Library...**.

## Moving from v0.7 to v0.8

PaperRoute v0.8 adds route maps, the Insights page, shareable reports, and work types and tags. **v0.8.0 is the current Stable release.** v0.8 upgrades the library to **Schema 8**, which adds work types, tags, and tag colors.

Before upgrading, create a portable ZIP backup with v0.7 (**Settings → Backup Library...**) and keep it separately.

The Schema 7-to-8 upgrade validates the existing library first and then changes only the schema marker, keeping the previous marker as `schema.v7.bak`. Manuscript and author data, managed files, and automatic backups stay byte-for-byte, and every manuscript starts with no type and no tags; nothing is inferred. If validation fails, nothing changes and PaperRoute says why.

After upgrading:

- Open **Insights** (Ctrl+5). Import & Export moves to Ctrl+6.
- Open any submitted manuscript's Route to see its route map. A revision stretch shows as "not recorded" when the return to review was never entered; add an Under Review history entry with that date to split it.
- v0.7 refuses to open a Schema 8 library. To go back, restore the backup you made with v0.7; changes made in v0.8 are not in it. Restore v0.8 backups with v0.8 or later.

## Moving from v0.6 to v0.7

PaperRoute v0.7 replaces the Reminders page with **Deadlines** and adds the publication check. It upgrades the library to **Schema 7**, which remembers possible publications you have reviewed.

Before upgrading, create a portable ZIP backup with v0.6 (**Settings → Backup Library...**) and keep it separately.

The Schema 6-to-7 upgrade validates the existing library first and then changes only the schema marker, keeping the previous marker as `schema.v6.bak`. Manuscript and author data, managed files, and automatic backups stay byte-for-byte. If validation fails, nothing changes and PaperRoute says why.

After upgrading:

- Open **Deadlines** (Ctrl+4). Your revision deadlines, follow-ups, and reminders are all there, with work that has no date yet under **No date**.
- **Postpone** changes the date on the record that owns it, so the manuscript page shows the same date.
- v0.6 refuses to open a Schema 7 library. To go back, restore the backup you made with v0.6; changes made in v0.7 are not in it. Restore v0.7 backups with v0.7 or later.

## Moving from v0.5 to v0.6

PaperRoute v0.6 changes where things live and how they look. It does **not** migrate your data: the library stays on **Schema 6**, so v0.5 can still open a library that v0.6 has used, and portable backups made by either version restore in both. A backup before upgrading is still a sensible precaution.

What to expect after upgrading:

- PaperRoute opens in one window. The left rail replaces the **Data** and **Settings** menus; the User Guide's "Finding your way around" table lists each command's new location.
- A manuscript opens as a page instead of the Manuscript Details dialog. Edits collect in one **Unsaved changes** bar; choose **Save** (or press Ctrl+S) to keep them, or **Discard**. Leaving the page or closing PaperRoute with unsaved changes asks first.
- Submission details, editorial decisions, reviewer responses, and correspondence are edited on the manuscript's **Submissions** tab and saved with the manuscript.
- A Markdown response draft escapes fewer characters, so its raw text differs from a v0.5 export. It renders the same.

## Moving from v0.4 to v0.5

PaperRoute v0.5 adds the Reviewer Response Matrix and uses storage **Schema 6**. Installed v0.4-to-v0.5 migration, clean installation, and portable backup/restore were certified before Stable publication.

Before upgrading, create a portable ZIP backup with v0.4 and retain it separately. The Schema 5-to-6 migration validates the existing library, retains the original manuscript and author data bytes and previous schema marker, and preserves existing histories, readiness profiles, packets, managed files, and external links. It does not invent reviewer comments, editorial decisions, or revision rounds.

After upgrading, confirm your existing manuscript, version, submission, and packet histories first. On a disposable manuscript, open a submission with an editorial decision, choose **Reviewer Responses...**, add a comment, and choose **Save Comment**. Choose **Save & Close** in the matrix, **Close** in Submission Details, and **Save & Close** in Manuscript Details. Reopen it to confirm the reviewer response persisted. A Markdown export is a separate draft; exporting alone does not save the matrix.

### Backups and returning to an older version

v0.5 ZIP backups preserve reviewer-response fields and their decision/round links, statuses, drafts, and stored order alongside the existing library data. Restore a v0.5 backup with **v0.5 or later**. Older restore code may ignore the new fields and lose reviewer responses, even though older applications refuse to open a Schema 6 library directly.

If you need to return to v0.4, use the separate backup made by v0.4 before the upgrade. A v0.5 backup is not a supported rollback path, and changes made after the pre-upgrade backup will not be present in it. Do not edit the schema marker to force an older application to open newer data.

Excel exports remain partial interchange files and do not preserve the response matrix or complete workflow. Use a portable ZIP backup for the full library; retain externally linked original files separately.

## Earlier upgrade: v0.3 to v0.4

Installed v0.3-to-v0.4 migration and clean-install behavior were certified before v0.4 Stable publication. That migration moves compatible Schema 4 libraries to Schema 5 while preserving existing manuscript history and managed/linked files.

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
