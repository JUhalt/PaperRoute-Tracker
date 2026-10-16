# How PaperRoute releases are certified

This is the standing procedure that every "[vX] Release certification" issue follows, from "[v0.9.2] Release certification" on. It exists because five certifications passed a save that deleted other manuscripts' packet files (#107), because v0.9.0's clean-install evidence came from a local candidate while the published build was a rebuild of the same commit (#97), because candidate-01 failed seven tests while a VM was starting on the same host (#97), and because the every-window pass found Edit Journal's 150% defects in a window v0.9 had not touched (#93). Each step below would have caught one of those.

The certification issue lists the steps it ran. A step it skips is recorded there with the reason. The candidate comes from `Build-ReleaseCandidate.ps1`; the installer, upgrade and VM mechanics are in the [v0.4](v0.4-Release-Packaging.md) and [v0.5](v0.5-Release-Packaging.md) packaging procedures.

## 1. Mismatched-library gate

Standing from v0.9.1. Take a managed folder fuller than the library beside it: first an empty `manuscripts.json`, then an older one that names fewer manuscripts than the folder holds. Open each in the candidate, save once, and compare the managed tree before and after. It must be byte-identical: hash every file. A save that deletes, moves or rewrites anything in it fails the release.

## 2. Certify by what changed

List the release's changes first. A release that changes no window layout is certified on the test suite and the data gates alone. A release that touches shared display code (theme, scaling, shared controls) gets every view at 125% in both themes, not only its new windows. Record which case the release is, and why, in the certification issue.

## 3. The eye's job is in tests before the candidate

The catalog, Dark and measuring tests of #93 and #106 run first, and pass, before any capture is taken. Captures then judge layout and taste, not legibility. The tests state what they cannot see (hand-scaled values); those are what the captures are for.

## 4. Captures are reviewed at full size

From the first pass, one window at a time, at the pixel size it was captured. A second reviewer tries to refute each finding before it is recorded. Record the reviewer and the scale with every result. Downscale only to navigate between captures, never to judge one.

## 5. Real-150% time

Real 150% display scaling gets one calibration session and one session per conversion batch, three sessions in all. Every other 150% capture (the Board, #106's one list and one grid) is taken inside those sessions, not in sessions of its own.

## 6. Capture only after the window is ready

The harness writes a "painted" marker when a window's background load has finished and its message loop is idle (`--painted <file>`; the marker carries the UTC time, `Application.HighDpiMode` and the window's `DeviceDpi`). `Capture-HarnessWindow.ps1` passes that option, waits for the marker, and fails on timeout instead of capturing; there is no fixed settle time. The marker time goes into the capture's evidence JSON. A capture taken any other way is not evidence.

## 7. A quiet host

The candidate's test suite runs with no VM starting, no snapshot being taken and no harness build in progress. A timeout failure gets exactly one isolated rerun, and both results are recorded; a suite is never rerun to green silently. The shared STA runner lands before any every-view sweep.

## 8. Certify the bytes that ship

The installed-app gates run on the published build, installed through the live updater, not on a local candidate of the same commit. Record the installed `PaperRouteTracker.dll`'s SHA-256 against the release's `SHA256SUMS.txt`, as #108 does. The approval-gated publish job the plan proposed (item 7) is not adopted (the maintainer's decision of October 9, 2026); the maintainer's sign-off before the tag is the gate.

## 9. Each standing gate runs once, in one place

The release's certification issue lists the standing gates and holds each result once. "[v0.9.2] Release certification" is the first full set; later releases copy its list and add to it.

## 10. A richer standing fixture and a documented collector

Certification uses one standing fixture richer than any single defect needs, and a documented collector for its evidence. Evidence JSON is preferred to screenshots. `Application.HighDpiMode` and the bundled .NET runtime version are recorded in the evidence (the capture JSON carries the first from the harness; the second is `RuntimeVersion` in `candidate-verification.json` and `Stage-verification.json`, #144), so the app, the harness and the release record are known to agree.

## 11. The certification issue is opened at the start of the release

Open "[vX] Release certification" when the release starts, so evidence accrues as changes merge rather than being gathered at the end.
