# Reviewer Response Workflow — v0.5 development preview

This guide describes unreleased work for [#27](https://github.com/JUhalt/PaperRoute-Tracker/issues/27). **v0.4.0 remains the current Stable release.** The [Stable user guide](USER_GUIDE.md) continues to describe that released version.

## Record the journal conversation

Open the manuscript's existing journal submission and record the relevant editorial decision. Open **Reviewer Responses...** to work with the response matrix for that submission. A normal revision stays within that submission; it does not require another submission attempt.

Each item contains an existing editorial decision, an explicitly chosen positive revision-round number, a reviewer/editor label, the original comment, an action, a draft response, a manuscript location, and notes. Use multiple items for separate requests. Keep reviewer labels consistent when several comments came from the same person.

The statuses are **Unresolved**, **In progress**, **Addressed**, and **Not applicable**. They describe your response work and do not change the manuscript's lifecycle state or record a resubmission. The workflow uses manual entry and requires no AI service or account.

## Edit, order, and save

Add or edit an item, select its decision and round explicitly, and save the item back into the matrix. Use the status filter to concentrate on unfinished work. Reordering changes the stored item order; show all statuses before reordering so hidden items do not make the result ambiguous.

The matrix works on a copy. **Save & Close** accepts its changes into the manuscript working copy shown by Submission Details. Close Submission Details, then choose **Save & Close** in Manuscript Details to persist the entire manuscript. **Cancel** in the matrix discards that matrix session's edits; **Cancel** in Manuscript Details discards all unsaved manuscript changes, including accepted child-dialog edits. Submission Details itself has a **Close** button and no separate save/cancel boundary.

A decision referenced by response items cannot be deleted until those items are reassigned or removed. This keeps the matrix attached to the journal conversation that produced it.

## Export a response draft

Use **Export Markdown...** to open an editable, human-readable response draft, then **Save Markdown...** to write it to a file. The export includes the submission context, decision and revision-round associations, reviewer comments, statuses, actions, draft responses, locations, and notes in stored order. You can revise the preview before saving, edit the `.md` file in a text editor, or import it into a document-writing workflow. Edits to this draft do not update the matrix.

Exporting does not save matrix edits, mark comments addressed, record a submission, or change the library. An exported file is a separate snapshot of the matrix currently being reviewed; later edits do not update that file automatically.

## Development storage and checks

This increment uses **Schema 6** to preserve reviewer-response data and to keep older application versions from opening a newer library and silently discarding the added fields. The Schema 5 migration retains original manuscript/author file bytes, saves the previous schema marker, and does not invent comments, reviewers, decisions, or revision rounds.

Use the disposable demo and separate development storage when trying this preview. Retain a portable backup made by the Stable version before any future upgrade. Do not open a newer library or restore its backup in an older application; use the pre-upgrade backup if you need to return to Stable. The Excel workbook remains a partial interchange format and is not a replacement for a full library backup. Installed upgrade, clean install, updater, final theme/DPI checks, and packaged release verification must still be completed before v0.5 publication.
