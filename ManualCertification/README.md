# PaperRoute Manual Certification Fixtures

This directory contains disposable import fixtures used for repeatable F5/manual certification.

## Release certification

- [How releases are certified](Release-Certification.md): the standing procedure every release certification issue follows, from v0.9.2 on
- [The v0.9.2 checklist](v0.9.2-Certification-Checklist.md): every v0.9.2 gate in one ordered list, grouped by what has to be set up, with a place for each result
- [The Narrator pass](Narrator-Pass.md): the screen-reader script the certification runs once
- [Capture-HarnessWindow.ps1](Capture-HarnessWindow.ps1): captures a harness window once it reports itself painted, with evidence JSON
- [The standing certification fixture](#standing-certification-fixture) and [the fixture collector](#fixture-collector) that reports on it
- [Display evidence](#display-evidence): how every window opens at a real display, for the 150% sessions of #93

## Standing certification fixture

From v0.9.2, every release certification uses one standing fixture (#145): a fictional library of four manuscripts, written as a 0.9.x (Schema 9) library is on disk, with journal shortlists, journal facts, `citations.json`, AI-suggestion provenance, packets with managed and linked files, reviewer responses and versions. Its ids, dates and files are fixed, so it is the same every time it is written.

| Id | Title |
| --- | --- |
| `92000000-0000-0000-0000-000000000001` | ZZZ-CERT-v0.9.2-01 Major revision with responses |
| `92000000-0000-0000-0000-000000000002` | ZZZ-CERT-v0.9.2-02 Rejected and rerouted |
| `92000000-0000-0000-0000-000000000003` | ZZZ-CERT-v0.9.2-03 Published |
| `92000000-0000-0000-0000-000000000004` | ZZZ-CERT-v0.9.2-04 Draft with a shortlist |

The generator is `PaperRoute.Tests/CertificationFixture.vb`. `CertificationFixtureTests` writes the fixture twice and compares the two byte for byte, and its chained upgrade test carries the fixture through what an update to this build does to a 0.9 library: open, save, back up, restore into a new library, open again. Every record, and every managed and linked file byte for byte, must come through. The chain starts from Schema 9 because only the 0.9 releases update straight to 1.0 (#153); the Schema 3 to 9 steps keep their own migration tests.

To regenerate the fixture for a certification, name a new or empty folder outside PaperRoute's own folders and run its writer test from the repository root:

```powershell
$env:PAPERROUTE_CERTIFICATION_FIXTURE = 'C:\PaperRouteCertification\fixture'
dotnet test PaperRoute.Tests/PaperRoute.Tests.vbproj --filter "FullyQualifiedName~WritesTheFixtureForACertificationOnDemand"
```

It writes `PaperRoute\data` (the library files), `PaperRoute Library` (the managed copies), `Linked files`, and `PaperRoute-certification-fixture.zip`, a portable backup of the library. Restore the ZIP with **Restore Backup...**. The managed copies move with it; the linked files are referenced where they were written, so put `Linked files` at the same path on the certification VM. Without the variable, the test writes all of this into its own temporary folder, so every test run checks that **Restore Backup...** accepts the fixture's backup.

## Fixture collector

The evidence collector every certification since v0.5 cites (`fixture-evidence-<stamp>.json` in #97 and #108) is in [FixtureCollector](FixtureCollector/USAGE.md): `Program.cs`, `PaperRouteFixtureCollector.csproj` and `USAGE.md`.

- Version 1.2.0, and its JSON reports `CollectorVersion` 1.2.0, read from the build. Version 1.1.0 (from v0.7, which added `SchemaMarkers`) still reported 1.0.0, so an earlier report with `SchemaMarkers` came from 1.1.0.
- It never changes the library; its only write is the report, on the evidence share. For the v0.4 one-manuscript fixture it reports what it always has. For the standing fixture it reports `CertificationFixture`, with `CertificationManuscripts` naming each `ZZZ-CERT-` manuscript by id and title. Nothing is read from any other record: a library with any other manuscript is rejected, with nothing copied.
- Build it with `dotnet build ManualCertification/FixtureCollector/PaperRouteFixtureCollector.csproj -c Release` and copy `bin\Release\net10.0-windows\win-x64` to the input share's `diagnostic-collector` folder. Run it only in the certification VM, as USAGE.md says.

## Display evidence

[DisplayEvidence](DisplayEvidence/Program.vb) records how every top-level window opens at a real display, for #93's sessions at real 150%: step 2's calibration of the measuring sweep, and the evidence for each batch of step 3. It runs the sweep's own measure on every window of the view catalog with nothing simulated, so its table and the sweep's simulated table can be set side by side.

**What it records.** Before any window it sets `HighDpiMode` to SystemAware, as `ApplicationEvents.vb` pins it. Each window is built on a temporary data root of its own, as the sweep builds it, and a window that asks for the default data root fails, so the computer's own PaperRoute data is never read or written. Each is fitted to the working area of the screen it opens on, as the app fits it. Two files are written:

- `display-evidence.json`: the Windows version, the primary screen's bounds and working area, the screen's DPI, `Application.HighDpiMode` and the app's version with its commit; then, for each window, its name, its scaling kind as the sweep reads it from the source, its size and `DeviceDpi`, whether its accept and cancel buttons are inside the client area, every cut the sweep would report, and, for calibration, each button, check box, option, single-line label and tab caption with its bounds in the client area and its preferred size.
- `display-evidence.md`: the sweep's table with one column, this display, named as the sweep names its columns (working area @ DPI).

`--png` adds a picture of each window, `<Window>.png`. The evidence goes to `--out <folder>`, or to `display-evidence-<yyyyMMdd-HHmmss>` beside the tool; a folder under `%TEMP%` is refused. It is written once every window is done, and an earlier run's `display-evidence.json` and `display-evidence.md` in the same folder are removed before the first window opens. Nothing waits for an answer: an error in one of a window's handlers fails that window by name, with no dialog; a failure goes to standard error with an exit code (2 arguments, 3 the evidence folder or the tool's own files, 4 a window failed, 5 anything else), and the windows that failed are named at the foot of both files.

**Publishing it.** On the host, from the repository root (the VM has no .NET SDK):

```powershell
dotnet publish ManualCertification/DisplayEvidence/PaperRoute.DisplayEvidence.vbproj -c Release -r win-x64 --self-contained -o artifacts/display-evidence
```

Copy the whole `artifacts\display-evidence` folder to the input share as `display-evidence`. Its `AppSource` folder is the app's window source, which each window's scaling kind is read from. CI builds the tool on every run, so it cannot fall behind the catalog.

**Running it in the VM.** Set the display in Windows (Settings > System > Display): 1920x1080 at 150% for the first session, 1366x768 at 125% for the second. Start the tool from Run (Win+R) through `cmd`, so that its standard error is kept, naming the evidence share, since the input share is read-only:

```text
cmd /c "\\VBOXSVR\PaperRouteInputs\display-evidence\PaperRoute.DisplayEvidence.exe --png --out \\VBOXSVR\PaperRouteEvidence\display-evidence-150 2> \\VBOXSVR\PaperRouteEvidence\display-evidence-150.err"
```

Use `-125` in both names for the second session. The windows flash past for a minute or two; the run is over when the `cmd` window closes. An empty `.err` file means it finished; one that is not empty means it failed, and says why. The sentence under the heading of `display-evidence.md` must read 144 DPI (120 at 125%) and `HighDpiMode SystemAware`; if the DPI is still the old one, sign out and back in, as a system-aware program can keep the scale the session started with, and run it again into a new folder, `display-evidence-150b` with its own `.err`, so the first run's evidence stays for the record.

**Comparing it with the sweep.** Take the sweep's table from the same commit, from the output of

```powershell
dotnet test PaperRoute.Tests/PaperRoute.Tests.vbproj --filter "FullyQualifiedName~EveryTopLevelWindowIsMeasuredAt96To144Dpi" --logger "console;verbosity=detailed"
```

on a host whose screen holds the windows (a smaller one marks a cell "not measured"; take that cell from the table on #93). The sweep's screens are the VM's, 1920x1080 and 1366x768 less Windows 11's taskbar, 48 pixels tall at 100% and grown with the scale, so its columns name the working areas the tool records: set the 150% column beside the sweep's `1920x1008 @ 144` and the 125% column beside `1366x708 @ 120`. Then, row by row:

- Two cells agree when they give the same verdict: both "ok", or the same accept and cancel verdicts and the same controls cut, by kind and caption, whether "clipped" or not. DeadlineDateForm's button carries the date of the run, so match it by its place.
- The tolerance allows two differences. A control cut on one side only whose preferred width is within 2 pixels of its width in the JSON is at the edge, where the rounding of scaled sizes and the display's own border widths can decide it either way. And a control clipped in a window whose real client area is shorter than the sweep's, when the control lies in the difference: the working areas match, but the sweep's window frame keeps its 100% size, so at 125% and 150% the real frame takes a few pixels more.
- The windows scaled "by hand" or "both" are outside the calibration: the sweep cannot see their hand-scaled sizes, so where they disagree the real cell is the verdict, and it is a finding for step 4.

The calibration holds when every "none" and "Windows Forms" window agrees within that tolerance. Record any that does not on #93, with both cells and its JSON entry: the simulated table is not to be trusted for that window until the measure or the simulation is corrected. The control bounds also serve step 4's per-control check: run the tool at 100% for the base, and a window Windows Forms scales should show each control at 1.5 times that size at 150%, not 1.0 and not 2.25.

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
