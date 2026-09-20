# PaperRoute v0.4 disposable UI demo

This Windows Forms harness opens the actual PaperRoute forms with synthetic samples. Use it for screenshots, narrow-window checks, keyboard navigation, and experimenting with edits before using a real manuscript. The optional `workflow` surface runs connected Manuscript Details in a disposable storage session; `board` opens the actual main board with a disposable synthetic library. The harness is separate from the release solution and requires Windows with the .NET 10 SDK.

From the repository root:

```powershell
dotnet run --project ManualCertification/V04Demo/PaperRoute.V04Demo.csproj -- vault
dotnet run --project ManualCertification/V04Demo/PaperRoute.V04Demo.csproj -- vault --minimum
dotnet run --project ManualCertification/V04Demo/PaperRoute.V04Demo.csproj -- readiness --minimum
dotnet run --project ManualCertification/V04Demo/PaperRoute.V04Demo.csproj -- packet --minimum
dotnet run --project ManualCertification/V04Demo/PaperRoute.V04Demo.csproj -- file --minimum
dotnet run --project ManualCertification/V04Demo/PaperRoute.V04Demo.csproj -- submission
dotnet run --project ManualCertification/V04Demo/PaperRoute.V04Demo.csproj -- submission --minimum
dotnet run --project ManualCertification/V04Demo/PaperRoute.V04Demo.csproj -- workflow
dotnet run --project ManualCertification/V04Demo/PaperRoute.V04Demo.csproj -- workflow --minimum
dotnet run --project ManualCertification/V04Demo/PaperRoute.V04Demo.csproj -- board --minimum --primary
```

Other surface arguments are `packet-new`, `file-new`, and `notes`. With no surface argument the vault opens. `--empty` is supported for `vault` and `readiness`; it clears the sample packets and readiness profiles while keeping versions and the journal library available. `--integrity` is supported only for the populated vault and creates the disposable file fixtures described below. `workflow` and `board` support `--minimum` and cannot be combined with `--empty` or `--integrity`. `--help` displays the options. To reuse a completed build, add `--no-build` before the application argument separator `--`.

Each run starts fresh. By default, the title ends with `[DEMO - unsaved sample data]`. On these single-dialog surfaces, Save & Close and Cancel operate on the sample objects and close the window. Use the normal buttons in the vault/readiness forms to open their child dialogs, or launch those dialogs directly with the commands above. The `workflow` launcher stays open so you can save and reopen the sample. Close the demo before rebuilding its referenced application.

The standalone `submission` surface opens the real Submission Details form on the existing fictional submission, without repositories or new files. Use it to inspect the summary, publisher-portal row, and minimum-size layout without repeating the workflow. Decisions and correspondence remain editable disposable sample events; closing discards those edits. This preview does not enable **View Submission Packets...** navigation. Use `workflow` to test that connected action and the final save boundary. `submission --minimum` applies the actual minimum after the form finishes its normal initial sizing.

The board's resize and DPI evidence is recorded in [Shelf-Layout-2026-09-20.md](../Shelf-Layout-2026-09-20.md). Final native 150% verification after the minimum-height refinement passed with vertical shelf scrolling, no horizontal shelf range, and a reachable last-card `View route` action.

## Data and persistence limits

No demo surface starts PaperRoute's main program or invokes storage migration. The default single-dialog surfaces do not construct repositories or open Manuscript Details, and never load, save, import, or delete manuscript/library records. Their sample files are metadata entries; the one missing linked file points to a unique temporary path that is never created. These surfaces write no files unless `--integrity` is supplied. The explicit `workflow` and `board` surfaces instead use repositories beneath their unique temporary session roots, as described below.

The standalone vault's production constructor constructs `ManagedLibraryService`, which resolves the normal library path but performs no writes. These single-dialog surfaces do not invoke the managed-copy commit or staged-deletion services. Vault Save changes only the in-memory manuscript; removal changes only sample packet entries. They do not certify managed storage, backup/restore, persisted outer-save behavior, or actual file deletion. The workflow session can exercise the real outer-save boundary against disposable storage; that still does not replace full release certification.

The real Browse/Open controls remain available. For file-picker experimentation, choose a disposable file you created for testing. A selected file can be inspected or opened by its normal associated application. On single-dialog surfaces, no selected content is copied because the outer persistence step is absent. In a workflow session, managed-copy choices are committed to that session's temporary managed library when you save Manuscript Details.

## Main board in disposable storage

The `board` surface opens the actual main board with five fictional manuscripts in each shelf. Before any repository or board is constructed, it configures and verifies a new `PaperRoute-Board-Demo-<GUID>` session under the operating system temporary directory. Settings, author data, manuscript data, and managed files all stay beneath this root. Automatic update checks and reminder notifications are disabled in the session settings. No installed profile or existing manuscript is loaded, and normal startup migration is not invoked.

Use `board --minimum --primary` to start at minimum size on the primary display with actual `DeviceDpi` in the title. Narrow, widen, maximize, and restore the board repeatedly. Shelves should have no horizontal scroll range when their cards fit; scroll vertically to the last card in every shelf. Check **Open**, **Move to File Drawer** or **Restore**, **Delete**, and **View route**, plus Tab and Shift+Tab navigation. Search for an absent title and clear the search to compare empty and populated shelves. Each launch starts fresh and leaves its disposable directory for inspection afterward. The real import/export/file-picker actions remain available and operate on explicitly selected paths, so use disposable files if exercising those controls.

The automated shelf regressions include larger text sizes; those are layout stress tests, not native DPI certification. For native 100%, 125%, and 150% checks, follow the display-scaling instructions below and verify the title's actual DPI value each time.

The board demo writes `board-layout.json` beneath its disposable session directory after the initial display and each settled resize. It records actual DPI, window state, shelf scroll ranges, and synthetic card bounds for the last 30 observations. Keep this diagnostic file alongside screenshots when reporting resize problems; it contains no existing user-library data.

## Connected workflow in disposable storage

Run the `workflow` command above, then choose **Open Manuscript Details**. This mode starts with a fictional Draft, one metadata-only Version History snapshot, a journal checklist with unresolved requirements, and no packets or submissions.

1. Open **Submission Readiness...**, update a checklist item, and save the readiness window.
2. Inspect **Version History**. The initial snapshot has no local file; you can edit its descriptive label or add another metadata-only version.
3. Open **Submission Packets...** and prepare a packet tied to the intended exact version and readiness profile. Metadata-only file records are enough to exercise this flow.
4. Save the packet window, then **Save & Close** in Manuscript Details. The launcher reloads the saved repository and displays the packet's exact version. The stage should still be Draft, with zero recorded submissions.
5. Reopen Details and confirm the checklist, version, and packet edits remain. Record a journal submission using fictional details such as manuscript number `DEMO-2026-0043`, and associate the prepared packet with that submission. Saving should now show the recorded submission and packet association in the launcher.
6. Reopen, make a small manuscript or packet edit, and cancel Manuscript Details. **Reload Saved State** should still show the previously saved manuscript state.

With `workflow --minimum`, Manuscript Details opens at its actual minimum size after its normal initial layout. Resize, maximize, and restore it; use its section navigation and child dialogs, and check that the connected actions and Save/Cancel remain reachable. The launcher reports saved data, so child-dialog edits appear there only after the outer save.

Before constructing any sample, service, or form, this mode claims a new `PaperRoute-V04-Workflow-Demo-<GUID>` directory directly under the operating system temporary directory. A friend-only startup hook configures it once for that process. It rejects existing directories and refuses to switch roots after any storage root has been resolved. No environment variable, normal application setting, or installed profile is changed.

All default repository paths are redirected beneath that directory, including nested author/journal editors, manuscript repositories, managed files, and application settings. The current and legacy roots are separate subdirectories (`current-data`, `legacy-data`, `managed-library`, and `legacy-managed-library`); the harness never runs migration. The launcher displays the full session path, and the saved sample lives at `current-data/data/manuscripts.json`. Its seeded reusable journal/checklist lives at `current-data/data/authors.json`.

**Save & Close** really saves and reloads the disposable repository. **Cancel** discards the Details form's working manuscript changes. Reusable author/journal library dialogs retain their normal separate save behavior, but their saves also stay inside this session. Deleting the sample removes it only from this session; start another demo process for a fresh sample. This fixture automatically reads no real manuscript or reusable library and needs no existing local document. File-picker and external-lookup controls still perform their usual actions when explicitly used.

Closing the workflow launcher leaves the session directory intact as evidence. Each new run starts in a different directory; there is no automatic resume or recursive cleanup. You may inspect or remove that specific `PaperRoute-V04-Workflow-Demo-<GUID>` directory afterward. Keep the directory and screenshots when reporting a problem. These checks demonstrate the connected workflow and isolated persistence, not backup/restore or complete release certification.

## Disposable integrity checks

```powershell
dotnet run --project ManualCertification/V04Demo/PaperRoute.V04Demo.csproj -- vault --integrity
dotnet run --project ManualCertification/V04Demo/PaperRoute.V04Demo.csproj -- vault --integrity --minimum
```

This explicit mode creates a new `PaperRoute-V04-Integrity-Demo-<GUID>` directory directly under the operating system's temporary directory. It writes four tiny UTF-8 text files there, records baselines using the real packet integrity service, rewrites only its own `changed.txt`, and deletes only its own `missing.txt` during fixture setup. Setup uses the service to verify all five expected states before opening the form. Each run has a different directory. The selected file details show its full path; the sample packet notes also include the directory. No fixture is placed in PaperRoute's managed library, and no existing user file is used.

The title includes `disposable integrity files`. Select the integrity packet and use **Check Files**. Inspect these five entries:

| Fixture | Expected result after Check Files |
| --- | --- |
| `no-baseline.txt` | No fingerprint; a file exists but has no recorded fingerprint. |
| `unchanged.txt` | Unchanged; its current `abc` content matches its recorded fingerprint. |
| `changed.txt` | Changed; its baseline was `abc`, then setup wrote `xyz`, keeping the same three-byte size. |
| `missing.txt` | Missing; setup captured its fingerprint before deleting this fixture. |
| Metadata-only checklist | Metadata only; no local file or fingerprint is required. |

Select `no-baseline.txt` and choose **Record Fingerprint**, then **Check Files** again; it should now show Unchanged. Inspect the changed entry and confirm a check does not replace its original fingerprint. Resize the vault, adjust the splitter, and select each status to confirm the longer details and buttons remain readable. The fixtures retain long notes for the layout checks. Close and restart the demo for a fresh set of starting conditions.

All packet records, captured fingerprints, and edits remain in memory and are discarded when the demo closes. The three remaining text files are deliberately left in their unique temporary directory as disposable evidence. You may remove that specific `PaperRoute-V04-Integrity-Demo-<GUID>` directory afterward. The demo performs no recursive cleanup. These checks cover linked-file verification and the vault UI; persisted managed-file behavior still needs separate certification.

## Suggested visual checks

The demo defaults to Light. Add `--dark` for Dark or `--system` for Follow Windows on any surface; these flags are mutually exclusive and affect only the demo process. They never change Windows settings or saved PaperRoute preferences. Native dark mode requires a supported Windows version; record the effective appearance when certifying it.

```powershell
dotnet run --project ManualCertification/V04Demo/PaperRoute.V04Demo.csproj -- vault --minimum --dark
dotnet run --project ManualCertification/V04Demo/PaperRoute.V04Demo.csproj -- readiness --minimum --system
```

- At normal and minimum size, read the introduction and selected packet/item detail, inspect the longest list entries, and verify all action buttons remain reachable.
- Resize the window and the vault splitter; change packet/profile/file selection and confirm the relevant details refresh.
- Exercise Save, Cancel, tab navigation, Escape, checklist status changes, and Notes. The journal fixture has one new template requirement for the refresh action.
- Compare packet/file editor layout in populated and new-record states. Check the empty vault and readiness states with `--empty`.
- Select the missing linked figure and inspect its recorded path. Open is disabled for missing files. Use the optional integrity mode above to check missing, changed, unchanged, and absent-baseline behavior with real disposable fixtures.
- For genuine display scaling checks, set Windows scaling through the normal display settings and start a new demo process. The host uses SystemAware DPI; `--minimum` uses the form's actual minimum size after initialization. It does not simulate Windows DPI by changing fonts or calling `Scale`.

Add `--primary` to place a demo on the primary display and append its actual
`DeviceDpi` to the title. This avoids recording a scaling pass against the wrong
monitor on a multi-display desktop. Expected DPI values are 96 at 100%, 120 at
125%, and 144 at 150%; verify the title instead of assuming a Settings change
has taken effect. Restore the original Windows scaling after the checks.

Record the commit or working-tree changes, Windows scaling, surface, window size, and screenshot path alongside pass/fail notes. A clean visual result is evidence for these dialogs only; it does not certify the entire application or a release.
