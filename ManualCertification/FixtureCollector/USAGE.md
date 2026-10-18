# Passive synthetic-fixture evidence collector

Run this collector only in the disposable certification guest. Do not run it on
the host. It has no visible window, console, keyboard injection, UI Automation,
authentication, application launch, or library-write operation.

1. Close PaperRoute after completing the guest action being certified.
2. In guest Explorer, open
   `\\VBOXSVR\PaperRouteInputs\diagnostic-collector`.
3. Double-click **PaperRouteFixtureCollector.exe**. No dialog or terminal appears.
4. On the host, inspect the new report in the clone's **CertificationOutput**
   folder under `fixture-evidence`. In the guest this is
   `\\VBOXSVR\PaperRouteEvidence\fixture-evidence`.
5. Keep one report after the v0.4 fixture restore and before upgrading, another
   after the first v0.5 launch and close, and further reports after reviewer
   response saves and a v0.5 backup/restore. Compare data/file hashes, schema,
   decision/round links, statuses, and response-record hashes.

The executable is self-contained Windows x64 and requires no installed .NET SDK.
Keep every published file and, for the v0.4 fixture,
`synthetic-fixture-expected.json` beside it. The source is in the PaperRoute
repository under `ManualCertification/FixtureCollector`.

Only the one known synthetic manuscript and its expected submission, version,
packet, readiness, and journal identifiers authorize collection. Empty libraries
receive an **EmptyLibrary** report without content. An unrelated library or
reusable metadata receives **RejectedOtherData**, with a short reason and no
contents copied. This strict check deliberately rejects a missing expected
record; use the unchanged fixture to collect migration evidence before deletion
tests.

From version 1.2, a library whose every manuscript title starts with
`ZZZ-CERT-`, such as the standing certification fixture, receives a
**CertificationFixture** report: `CertificationManuscripts` names each
manuscript by its id and title, beside the schema markers, the installed
versions, and the hash and size of `manuscripts.json`. Nothing else is read
from those records. A library with any other manuscript receives
**RejectedOtherData** (`OtherManuscriptsPresent`) with nothing copied, so give
any manuscript added during a certification a `ZZZ-CERT-` title.

For the recognized v0.4 fixture, reports contain hashes, sizes, versions, counts,
known identifiers/associations, readiness statuses, and response metadata/hashes.
They never copy manuscript titles, reviewer labels, comments, draft responses,
notes, or arbitrary file contents. Managed file reads are limited to the three
fixture paths; linked-file reads are limited to the known read-only input-share
file. Unknown reference paths are recorded as not read. Automatic/portable
backups are reported only after their manuscript/metadata identifiers match the
fixture; unrelated or empty backups are counted as skipped. No archive is
extracted.

Standard guest locations are `%LocalAppData%\PaperRoute\data` and the Windows
Documents folder's `PaperRoute Library`. Installed binaries are inspected at
`%LocalAppData%\JUhalt.PaperRouteTracker` and the documented optional
`C:\PaperRouteCertification\installed`. The standard profile is always selected;
the collector never initializes a profile or invokes storage migration.

Report **SyntheticFixture** or **CertificationFixture** means collection
succeeded, not that all checks passed. Inspect
`LibraryUnchangedDuringCollection`, file statuses and
`MatchesExpectedFixtureHash`, installed versions, and schema values. A missing
share or locked file must not be represented as a pass. If the output share is
unavailable, the collector writes no local fallback and exits with code 3.

The only writes are timestamped JSON reports on `PaperRouteEvidence`. Reports
do not certify installation behavior, native DPI, visual correctness, or the
live updater. Continue those independent checks in the guest.
