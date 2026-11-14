# RO-Crate export for submission packets: evaluation and decision (#45)

**Decision update, October 3, 2026:** Adopted for v0.9.0 by the maintainer on October 3, 2026; the export follows the rules below; implemented in [PR #103](https://github.com/JUhalt/PaperRoute-Tracker/pull/103). **Export Packet...** in the Submission Packet vault writes one .zip that is both the plain package (`files/`, `manifest-sha256.txt`, and the summary page, here named `ro-crate-preview.html`) and an RO-Crate 1.3 crate (`ro-crate-metadata.json`); the summary page and checksum list make sense without the metadata. The original decision below is kept as history.

Evidence: this evaluation's synthetic packet, rebuilt as a test fixture and exported by PaperRoute's own code (`PacketExportTests.SyntheticExport_WritesCrateForOfflineChecker`, with the changed supplement included to show its status), then checked offline with a re-implementation of roc-validator 0.12.1's `ro-crate-1.3` rules (the official validator was not run then; for its later run on the same export, see [Official validator run (#159)](#official-validator-run-159)), passed 85 checks with no failures and seven RECOMMENDED warnings. All seven come from refusing to invent data, or are by design: no publisher; no organization URL; no ROR id; no contact point; no ORCID for one author; a local "rights not stated" license entity rather than a license URL; and, in the checker's extra provenance check, no person as the actions' agent and no end time for the submission. The checker's informational notes are optional file properties and the undescribed `manifest-sha256.txt`; its privacy checks (no local paths, no GPL on the content, no publication date) passed.

Where the build differs from the rules:

- Blinded mode comes only from a blinded manuscript in the packet, because PaperRoute records no journal's review type (rule 5).
- In blinded mode the manuscript with author details (role Manuscript) also starts unchecked, beside the title page and cover letter, and the author-name check also covers labels, the package name, the version label, and the journal name, not only file names (rule 5).
- A file that is locked or can't be read shows "Can't be read" and, like a missing file, can't be included (rule 7).
- The official validator, roc-validator 0.12.1, was run once on a golden crate on November 14, 2026 (rule 11). On the crate extracted to a folder it found no REQUIRED failures. On the zip itself, a Windows path bug in the validator stopped it finding the files; see [Official validator run (#159)](#official-validator-run-159).

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

  The official validator was not installed for this evaluation: it needs a download from PyPI and a network fetch of the context. The command noted for a later check was
  `python -m pip install "roc-validator==0.12.1"` then `rocrate-validator -y validate -np -p ro-crate-1.3 -l RECOMMENDED <crate folder>`. It was run later on PaperRoute's own export, not on this crate; see [Official validator run (#159)](#official-validator-run-159).
- **Without PaperRoute.** A reader with only the two folders, a browser, and PowerShell understood both and verified all eight files.
  - **For a person, the two are the same page,** and the plain one is slightly better: it says how to check the files.
  - **For a program,** the RO-Crate adds typed links: manuscript, version, authors with ORCID iDs, journal with ISSN, the submission, and the export. But it still left out the most important facts, fingerprint status and what was not included.
- **Privacy.** Nothing identifying leaked through what PaperRoute wrote: no notes, correspondence, reviewer names, manuscript number, portal link, absolute path, or user name. But hidden document metadata, a PDF's author and a Word file's creator, travelled inside the copied files.

## Official validator run (#159)

On November 14, 2026, the official validator, roc-validator 0.12.1 (CRS4, from PyPI), was run once on PaperRoute's own export. These numbers are separate from the two sets above, which came from an offline re-implementation of its rules: 85 checks passed with seven warnings on PaperRoute's export, and 86 passed with six warnings on `make_eval.py`'s crate.

- **Crate.** `paperroute-export-synthetic.zip`, written by PaperRoute's export code at commit 800d6c1 (the 1.0.x code) through `PacketExportTests.SyntheticExport_WritesCrateForOfflineChecker`, with `PAPERROUTE_EXPORT_EVIDENCE_DIR` set. Its metadata comes from the test fixture, so the crate names PaperRoute `0.9.0-test` and the date 2026-10-03. It is 10,396 bytes, with SHA-256 `E13FEE7C38B5AC260FA25D8DD8C70222B99079C7836B6C5AA6F0100C368D33AA`. The hash names the file that was checked; another build may not write the same bytes. The zip is not committed, since the test writes it again.
- **Tools.** roc-validator 0.12.1 in a new virtual environment, with Python 3.14.7 on Windows 11 (10.0.26300). Every installed package version is in [roc-validator-0.12.1-freeze.txt](roc-validator-0.12.1-freeze.txt).
- **Commands.** `<crate>` is the zip extracted to a folder, with the folder's path written with forward slashes:
  - `python -m venv venv` then `venv\Scripts\python -m pip install "roc-validator==0.12.1"`
  - `rocrate-validator --disable-color -y validate -np -p ro-crate-1.3 -l RECOMMENDED -v -w 200 <crate>`
  - `rocrate-validator --disable-color -y validate -np -p ro-crate-1.3 -l RECOMMENDED -f json -o roc-validator-0.12.1-report.json <crate>`
  - `rocrate-validator --disable-color -y validate -np -p ro-crate-1.3 -v -w 200 <crate>` (REQUIRED checks only, the default level)
- **Why a folder with forward slashes.** On Windows, 0.12.1 did not give a usable result in two other ways:
  - With a path written with backslashes, on the zip and on the folder, it stopped with "SHACL validation could not be executed by pySHACL 0.40.2: PatternError: bad escape \U at position 9". It took the crate's base address from the Windows path (`file://C:\...`), and pySHACL read that text as a regular expression replacement while writing a warning.
  - On the zip with a forward-slash path, it looked for each file inside the zip as `files\Main text (revision 2).pdf`, with a backslash, and logged "File not found in zip" for all eight files. It then reported REQUIRED check ro-crate-1.3_18.1, "Data Entity: REQUIRED resource availability", and RECOMMENDED check ro-crate-1.3_69.1, "Missing local File SHOULD use localPath": 158 checks passed, 8 failed, 6 skipped. The zip's entries are named with `/`, as the zip format requires, and the same files pass that check in the extracted folder, so this is the validator's Windows path handling, not a fault in the export.
  - Both come from Windows path separators, not from the spaces, `#`, `%` or parentheses in the file names, so roc-validator 0.13.1 was not tried.
- **Network.** On its first run the validator fetched the RO-Crate 1.3 context (`https://w3id.org/ro/crate/1.3/context`) into its HTTP cache, and later runs read it from there. It did not check whether the crate's web addresses respond: those checks are off by default, and it skipped them.
- **Result with RECOMMENDED checks, on the extracted folder: no REQUIRED failures.** It ran 172 checks, 66 REQUIRED and 106 RECOMMENDED. 160 passed (63 REQUIRED, 97 RECOMMENDED), 6 RECOMMENDED checks failed, and 6 were skipped (3 REQUIRED, 3 RECOMMENDED). It printed "[FAILED] RO-Crate is not a valid ro-crate-1.3 !!!" and exited with code 1, because at this level a failed RECOMMENDED check fails the run. The JSON report's requirement totals read 92 passed and 0 failed, although the text report lists six RECOMMENDED requirements as not met. The failed checks, in the validator's words:
  - ro-crate-1.3_56.1, on `./`: "The Root Data Entity SHOULD have a `publisher` property (Organization or Person)".
  - ro-crate-1.3_85.0, on `#rights-not-stated`: "A License entity SHOULD have an absolute HTTP(S) URL as its @id (e.g., an SPDX license URI such as https://spdx.org/licenses/MIT.html)".
  - ro-crate-1.3_86.2, on `#org-1` and `#org-2`: "The organization SHOULD have a URL."
  - ro-crate-1.3_89.0, on `#org-1` and `#org-2`: "An Organization entity SHOULD have a ROR identifier as its @id (e.g., https://ror.org/03f0f6041)".
  - ro-crate-1.3_90.1, on `#org-1` and `#org-2`: "An Organization's contactPoint SHOULD reference a ContactPoint contextual entity".
  - ro-crate-1.3_91.0, on `#author-2`: "A Person entity SHOULD have an ORCID identifier as its @id (e.g., https://orcid.org/0000-0001-2345-6789)".

  These are six of the seven warnings the re-implementation gave, and all come from refusing to invent data or are by design. The validator did not report the seventh, which came from the re-implementation's extra provenance check (no person as the actions' agent, no end time for the submission).
- **Skipped checks.** ro-crate-1.3_9.1, 22.1 and 70.1: "availability check is disabled or not applicable" (22.1: "disabled or not requested"). ro-crate-1.3_21.1, 50.1 and 52.1: "RO-Crate is attached".
- **Result with REQUIRED checks only.** 66 checks: 63 passed, 0 failed, 3 skipped. It printed "[OK] RO-Crate is a valid ro-crate-1.3 !!!" and exited with code 0.
- **Report.** [roc-validator-0.12.1-report.json](roc-validator-0.12.1-report.json) is the JSON report of the RECOMMENDED run. Local paths in it were replaced with `<crate>`, `<venv>` and `<home>`; nothing else was changed.
- **When to run it again.** This is a one-time record, not a CI job. Run it again if the crate writer (`PacketExportPackage.MetadataJson`) or the pinned RO-Crate version changes.

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
