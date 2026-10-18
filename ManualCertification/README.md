# PaperRoute Manual Certification Fixtures

This directory contains disposable import fixtures used for repeatable F5/manual certification.

## Release certification

- [How releases are certified](Release-Certification.md): the standing procedure every release certification issue follows, from v0.9.2 on
- [Capture-HarnessWindow.ps1](Capture-HarnessWindow.ps1): captures a harness window once it reports itself painted, with evidence JSON
- [The standing certification fixture](#standing-certification-fixture) and [the fixture collector](#fixture-collector) that reports on it

## Standing certification fixture

From v0.9.2, every release certification uses one standing fixture (#145): a fictional library of four manuscripts, written as a 0.9.x (Schema 9) library is on disk, with journal shortlists, journal facts, `citations.json`, AI-suggestion provenance, packets with managed and linked files, reviewer responses and versions. Its ids, dates and files are fixed, so it is the same every time it is written.

| Id | Title |
| --- | --- |
| `92000000-0000-0000-0000-000000000001` | ZZZ-CERT-v0.9.2-01 Major revision with responses |
| `92000000-0000-0000-0000-000000000002` | ZZZ-CERT-v0.9.2-02 Rejected and rerouted |
| `92000000-0000-0000-0000-000000000003` | ZZZ-CERT-v0.9.2-03 Published |
| `92000000-0000-0000-0000-000000000004` | ZZZ-CERT-v0.9.2-04 Draft with a shortlist |

The generator is `PaperRoute.Tests/CertificationFixture.vb`. `CertificationFixtureTests` writes the fixture twice and compares the two byte for byte, and its chained upgrade test carries the fixture through what an update to this build does to a 0.9 library: open, save, back up, restore into a new library, open again. Every record, and every managed and linked file byte for byte, must come through. The chain starts from Schema 9 because only the 0.9 releases update straight to 1.0 (#153); the Schema 3 to 9 steps keep their own migration tests.

To regenerate the fixture for a certification, name a new or empty folder outside PaperRoute's own folders and run the on-demand test from the repository root:

```powershell
$env:PAPERROUTE_CERTIFICATION_FIXTURE = 'C:\PaperRouteCertification\fixture'
dotnet test PaperRoute.Tests/PaperRoute.Tests.vbproj --filter "FullyQualifiedName~WritesTheFixtureForACertificationOnDemand"
```

It writes `PaperRoute\data` (the library files), `PaperRoute Library` (the managed copies), `Linked files`, and `PaperRoute-certification-fixture.zip`, a portable backup of the library. Restore the ZIP with **Restore Backup...**. The managed copies move with it; the linked files are referenced where they were written, so put `Linked files` at the same path on the certification VM.

## Fixture collector

The evidence collector every certification since v0.5 cites (`fixture-evidence-<stamp>.json` in #97 and #108) is in [FixtureCollector](FixtureCollector/USAGE.md): `Program.cs`, `PaperRouteFixtureCollector.csproj` and `USAGE.md`.

- Version 1.2.0, and its JSON reports `CollectorVersion` 1.2.0, read from the build. Version 1.1.0 (from v0.7, which added `SchemaMarkers`) still reported 1.0.0, so an earlier report with `SchemaMarkers` came from 1.1.0.
- It never changes the library; its only write is the report, on the evidence share. For the v0.4 one-manuscript fixture it reports what it always has. For the standing fixture it reports `CertificationFixture`, with `CertificationManuscripts` naming each `ZZZ-CERT-` manuscript by id and title. Nothing is read from any other record: a library with any other manuscript is rejected, with nothing copied.
- Build it with `dotnet build ManualCertification/FixtureCollector/PaperRouteFixtureCollector.csproj -c Release` and copy `bin\Release\net10.0-windows\win-x64` to the input share's `diagnostic-collector` folder. Run it only in the certification VM, as USAGE.md says.

## v0.4 release records

- [Release closure and published-asset verification](v0.4-Release-Closure.md)
- [Historical development checkpoints](v0.4-Development-Checkpoint.md)
- [Disposable workflow demo](V04Demo/README.md)
- [Candidate packaging and installation procedure](v0.4-Release-Packaging.md)

## v0.9 evaluations

- [RO-Crate export for submission packets: evaluation, deferral, and adoption (#45)](v0.9-RO-Crate-Evaluation/README.md)

## Safety

Certification workbooks are **additive imports**.

They create new PaperRoute manuscripts with new internal identifiers. They do not overwrite existing manuscripts or Routes.

Importing the same workbook more than once will create another copy of the certification manuscripts, so delete the prior `ZZZ-CERT-*` records before re-importing when you want a clean certification run.

## Naming

Fixtures use conspicuous titles:

```text
ZZZ-CERT-<release>-<case>
```

They are intentionally easy to find, sort, and delete after certification.

## v0.3 Version History / Route fixture

`v0.3-VersionHistory-Certification.xlsx` contains six manuscripts:

1. `ZZZ-CERT-v0.3-01 Empty Draft`
   - Draft manuscript with no submissions.
   - Use for empty Version History, metadata-only versions, and basic Route navigation.

2. `ZZZ-CERT-v0.3-02 Active Submission`
   - Submitted to a journal with no editorial decision.
   - Use for submission-linked version testing.

3. `ZZZ-CERT-v0.3-03 Major Revision`
   - Active Major Revision with a deadline and reviewer correspondence.
   - Use for decision-linked versions, revision rounds, and reviewer-workflow groundwork.

4. `ZZZ-CERT-v0.3-04 Rejection and Reroute`
   - Rejected by Journal A, then submitted to Journal B.
   - Use for reroute visualization.

5. `ZZZ-CERT-v0.3-05 Withdrawn`
   - Latest submission was withdrawn.
   - Expected current manuscript state is Draft.

6. `ZZZ-CERT-v0.3-06 Same-Day Revision`
   - Submission and Major Revision decision share a calendar date.
   - Use for deterministic same-day chronology.

The standard workbook format currently imports manuscripts, submissions, decisions, and correspondence. Version records are then created manually during the Version History certification steps so the user-facing workflow itself is exercised.
