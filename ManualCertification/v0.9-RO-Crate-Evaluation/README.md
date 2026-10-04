# RO-Crate export for submission packets: evaluation and decision (#45)

**Decision update, October 3, 2026:** Adopted for v0.9.0 by the maintainer on October 3, 2026; the export follows the rules below; implemented in the pull request for branch v0.9-ro-crate-export. **Export Packet...** in the Submission Packet vault writes one .zip that is both the plain package (`files/`, `manifest-sha256.txt`, and the summary page, here named `ro-crate-preview.html`) and an RO-Crate 1.3 crate (`ro-crate-metadata.json`); the summary page and checksum list make sense without the metadata. The original decision below is kept as history.

Evidence: this evaluation's synthetic packet, rebuilt as a test fixture and exported by PaperRoute's own code (`PacketExportTests.SyntheticExport_WritesCrateForOfflineChecker`, with the changed supplement included to show its status), then checked offline with a re-implementation of roc-validator 0.12.1's `ro-crate-1.3` rules (the official validator was not run), passed 85 checks with no failures and seven RECOMMENDED warnings. All seven come from refusing to invent data, or are by design: no publisher; no organization URL; no ROR id; no contact point; no ORCID for one author; a local "rights not stated" license entity rather than a license URL; and, in the checker's extra provenance check, no person as the actions' agent and no end time for the submission. The checker's informational notes are optional file properties and the undescribed `manifest-sha256.txt`; its privacy checks (no local paths, no GPL on the content, no publication date) passed.

Where the build differs from the rules:

- Blinded mode comes only from a blinded manuscript in the packet, because PaperRoute records no journal's review type (rule 5).
- In blinded mode the manuscript with author details (role Manuscript) also starts unchecked, beside the title page and cover letter, and the author-name check also covers labels, the package name, the version label, and the journal name, not only file names (rule 5).
- A file that is locked or can't be read shows "Can't be read" and, like a missing file, can't be included (rule 7).
- The official validator has not yet been run on a golden crate (rule 11); that remains release evidence to gather.

**Decision, October 3, 2026: deferred until after 1.0.** PaperRoute does not export submission packets as RO-Crates in v0.9.0. The proposal stays open as an after-1.0 idea, with the rules below for any future build. No implementation issue was created.

This folder records the bounded evaluation that [#45](https://github.com/JUhalt/PaperRoute-Tracker/issues/45) asked for. `make_eval.py` regenerates the wholly synthetic packet and both exports with Python's standard library only (`python make_eval.py`; output goes to `work/`, which git ignores).

## What was compared

One synthetic packet ("Revision 2 to Fictional Journal of Psychology") was exported two ways from the same allowed fields:

- **Plain:** `files/`, `manifest-sha256.txt` (sha256sum format), and an `index.html` summary in the style of the shareable status report (#63).
- **RO-Crate 1.3:** the same `files/`, `ro-crate-metadata.json`, and an `ro-crate-preview.html` that is the same summary page.

RO-Crate 1.3 (published June 22, 2026, `https://w3id.org/ro/crate/1.3`) was chosen as the current release. The packet holds eight files with tricky cases, such as two files both named `Figure 1.png`, a name with `#` and `%`, a file changed since its fingerprint, one never fingerprinted, a response to reviewers, and a records-only entry. Its source records carry `SECRET-` markers in every field the issue says to leave out.

## Results

- **Source unchanged.** The source records and files hash the same before and after the export.
- **Validation.** An offline re-implementation of the community validator's `ro-crate-1.3` rules (roc-validator 0.12.1) found one REQUIRED failure in the first crate: the PaperRoute software entry had no `url`. After the fixes now in `make_eval.py`, it reports 86 checks passed, no REQUIRED failures, and six RECOMMENDED warnings. Every warning comes from refusing to invent data:
  - no publisher;
  - no organization URL;
  - no ROR id;
  - no contact point;
  - no ORCID for one author;
  - no license URL, because none is recorded.

  The official validator was not installed: it needs a download from PyPI and a network fetch of the context. To confirm with it later:
  `python -m pip install "roc-validator==0.12.1"` then `rocrate-validator -y validate -np -p ro-crate-1.3 -l RECOMMENDED <crate folder>`.
- **Without PaperRoute.** A reader with only the two folders, a browser, and PowerShell understood both and verified all eight files.
  - **For a person, the two are the same page,** and the plain one is slightly better: it says how to check the files.
  - **For a program,** the RO-Crate adds typed links: manuscript, version, authors with ORCID iDs, journal with ISSN, the submission, and the export. But it still left out the most important facts, fingerprint status and what was not included.
- **Privacy.** Nothing identifying leaked through what PaperRoute wrote: no notes, correspondence, reviewer names, manuscript number, portal link, absolute path, or user name. But hidden document metadata, a PDF's author and a Word file's creator, travelled inside the copied files.

## Why deferred, not adopted or rejected

- **No reader.** No journal, publisher, or submission system accepts RO-Crate, and there is no manuscript or submission profile. RO-Crate is used for data, workflows, and lab notebooks (WorkflowHub, Galaxy, Dataverse exporters, and the ELN Consortium's `.eln` format, which is pinned to 1.2).
- **The hard work isn't RO-Crate work.** Preview and per-file inclusion, safe file names, hidden metadata, blinding, and leak tests are needed by any packet export. PaperRoute has no packet export yet, and the portable-sharing decision belongs to the v0.9.1 hardening tracker, [#43](https://github.com/JUhalt/PaperRoute-Tracker/issues/43), which authorizes no sharing feature.
- **The data model can't yet support an honest crate.**
  - Publication dates have no precision: a year-only date is stored as January 1.
  - Nothing records rights or a license.
  - No date is kept for each revision round.
  - The version record has no fingerprint.
  - Every stored file path is absolute.
  - RO-Crate's required root `license` and `datePublished` then need careful wording ("rights not stated"; the export date) to avoid misleading a reader.
- **Not rejected.** The evaluation showed that every rule in #45 can be met, and an `ro-crate-metadata.json` could later sit beside a plain export at low cost. As with the AI reviewer-extraction proposal (#29), the problem is timing and audience, not principle.

## Revisit when

- A plain, user-started packet export has been accepted through #43 and shipped.
- A real reader is named, for example:
  - a journal or repository that reads crate metadata natively;
  - an ELN import;
  - repeated requests from users.
- The model records publication-date precision, rights ("not stated" allowed), and per-round submission dates.
- The RO-Crate spec has settled (RO-Crate 2 is in progress), and the version to pin is supported by the official validator and by that reader.
- PaperRoute 1.0 has shipped.

## Rules for any future build

These were learned from this evaluation. Most apply to a plain packet export too.

1. **Plain export first.** Build the plain export: `files/`, `manifest-sha256.txt`, and an HTML summary. RO-Crate is only an optional `ro-crate-metadata.json` written from the same allow-listed plan. The package must make sense without it.
2. **Local and started by the user.** No network during export. The context is referenced by URL but never fetched. Write the JSON with System.Text.Json; add no JSON-LD dependency.
3. **Roles are allow-listed.** "Other", unknown roles, Response to reviewers, and Cover letter start unchecked, with the warning shown in the preview, not written into the package. A test covers every `SubmissionPacketFileRole`.
4. **Hidden document metadata is shown before export, and the user must acknowledge it.** This covers:
   - PDF Info and XMP;
   - Word properties, comment and tracked-change authors, and template paths;
   - OpenDocument metadata;
   - image EXIF and GPS.

   Never strip it silently, since copies must stay byte-identical for their fingerprints.
5. **Blinded packets stay blinded.** When a packet holds an anonymized manuscript, or the journal uses double-blind review:
   - no people or organizations and no author line;
   - the title page and cover letter start unchecked;
   - file names are checked for author surnames.
6. **Safe file names.** Keep only the last path part of the original name and remove invalid characters, including `:`. Reject `..`, reserved names, and trailing dots or spaces, cap the length, and number duplicates. Every name and free-text label is editable in the preview. Copy only the main data stream (no `File.Copy`, so no alternate data streams), with fresh timestamps.
7. **Fingerprints are compared, never changed.** Hash the bytes actually written and compare them with the stored fingerprint: Unchanged, Changed, Not recorded, or Missing. Changed files start unchecked, and the status goes into the JSON as well as the HTML. Never call `CaptureBaseline` or write `Sha256`.
8. **No invented history.**
   - Give a round date only from a record of that round.
   - The submitted version and the published work are separate entities, with the DOI only on the published work.
   - Never use a preprint DOI as the manuscript's identifier, and never infer `isBasedOn` from date order.
   - Give a publication date only at the precision recorded.
9. **Honest rights and dates.**
   - The root `license` points to a local "rights not stated" entity.
   - GPL is never applied to the root, a file, the manuscript, or the metadata; preferably the software's license is left out.
   - The root `datePublished` is the export date, and the description says so.
10. **Identifiers are checked first.** Validate the ORCID checksum and DOI format before using either as an `@id`. Never fill a person from the Windows user name, the machine name, or the "me" author.
11. **Tests.**
    - Seed secret markers in every field not on the allow-list, then search the output for them. Unzip OOXML and ODF files, decompress PDF streams, and search UTF-8, UTF-16LE, UTF-16BE, and percent-encoded or JSON-escaped forms. Check for alternate data streams.
    - Compare a serialized snapshot of the source records before and after.
    - Validate a golden crate with a pinned official validator as release evidence.
