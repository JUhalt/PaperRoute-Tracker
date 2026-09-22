# Reviewer Response Workflow — v0.5 guide

This focused guide describes the v0.5 workflow tracked in [#27](https://github.com/JUhalt/PaperRoute-Tracker/issues/27). See the [complete user guide](USER_GUIDE.md#reviewer-response-matrix), [release notes](releases/0.5.0.md), and [repository homepage](../README.md) for current release availability. The filename retains its original preview name so existing links continue to work.

## Record the journal conversation

Open the manuscript's existing journal submission and record the relevant editorial decision. Open **Reviewer Responses...** to work with the response matrix for that submission. A normal revision stays within that submission; it does not require another submission attempt.

Each item contains an existing editorial decision, an explicitly chosen positive revision-round number, a reviewer/editor label, the original comment, an action, a draft response, a manuscript location, and notes. A label and either a comment or an action are required. Use multiple items for separate requests. Keep reviewer labels consistent when several comments came from the same person.

The statuses are **Unresolved**, **In progress**, **Addressed**, and **Not applicable**. They describe your response work and do not change the manuscript's lifecycle state or record a resubmission. The workflow uses manual entry and requires no AI service or account.

## Edit, order, and save

Choose **Add Comment...** or **Edit...**, select the decision and round explicitly, and choose **Save Comment** to accept the item into the matrix. Use **Show status** to concentrate on unfinished work. Select **All statuses** before using **Move Up** or **Move Down**; reordering changes the complete stored sequence.

The matrix works on a copy. **Save & Close** accepts its changes into the manuscript working copy shown by Submission Details. Close Submission Details, then choose **Save & Close** in Manuscript Details to persist the entire manuscript. **Cancel** in the matrix discards that matrix session's edits; **Cancel** in Manuscript Details discards all unsaved manuscript changes, including accepted child-dialog edits. Submission Details itself has a **Close** button and no separate save/cancel boundary.

A decision referenced by response items cannot be deleted until those items are reassigned or removed. This keeps the matrix attached to the journal conversation that produced it.

## Export a response draft

Use **Export Markdown...** to open an editable, human-readable response draft, then **Save Markdown...** to write it to a file. The export includes the submission context, decision and revision-round associations, reviewer comments, statuses, actions, draft responses, locations, and notes in stored order. It includes all items regardless of the status filter. Review the working notes before sharing the draft with a journal. You can revise the preview before saving, edit the `.md` file in a text editor, or import it into a document-writing workflow. Edits to this draft do not update the matrix.

Exporting does not save matrix edits, mark comments addressed, record a submission, or change the library. An exported file is a separate snapshot of the matrix currently being reviewed; later edits do not update that file automatically.

## Storage and backup

v0.5 uses **Schema 6** to preserve reviewer-response data. The migration from Schema 5 validates the existing library, retains original manuscript/author file bytes and the previous schema marker, and does not invent comments, reviewers, decisions, or revision rounds. Older application versions refuse to open a Schema 6 library directly.

Retain a separate portable ZIP backup made by v0.4 before upgrading. Restore a backup made by v0.5 with **v0.5 or later**: older restore code may ignore reviewer-response fields even though direct opening of newer libraries is blocked. Use the pre-upgrade v0.4 backup if you need to return to v0.4. The Excel workbook remains a partial interchange format and is not a replacement for a full library backup. See [upgrade notes](../UPGRADE_NOTES.md).
