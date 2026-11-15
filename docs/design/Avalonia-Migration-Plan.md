# Avalonia Migration Plan (issue #160)

**Status: a proposal.** Nothing in this plan is decided until the maintainer signs it off on [#160](https://github.com/JUhalt/PaperRoute-Tracker/issues/160). What the maintainer has already settled is quoted from [the comment of November 14, 2026](https://github.com/JUhalt/PaperRoute-Tracker/issues/160#issuecomment-6880580068) and marked as settled; everything else is a recommendation.

It describes the release candidate v1.0.1-rc.1 at commit `d67c312`. Every `path:line` is that commit's, relative to the repository root. It was written from five readings of the code made on November 14, 2026 (Windows-only APIs, layering, the interface, tests and parity, packaging), from the surface inventory and target map of [#161](https://github.com/JUhalt/PaperRoute-Tracker/issues/161) (`Surface-Inventory-and-Target-Map.md` in this folder, in draft pull request #370, itself a proposal), and from checks of the code made while writing. Nothing was built or run. Outside facts carry their address and the date they were read.

---

## Summary

**What is proposed.** PaperRoute's interface is rewritten on Avalonia, a cross-platform interface toolkit, for Windows and Linux in v1.1 and for macOS in v1.1.x. The data model and services (50,535 lines) move unchanged, or nearly so, into their own library. The interface (73,080 lines today) is rewritten, not converted, in the shape of #161's target map: one window, pages and panels instead of 62 modal dialogs, one save boundary per manuscript.

| Point of #160 | Recommendation |
|---|---|
| What stays | 167 of the 176 Models and Services files (all 76 Models files, 91 of the 100 Services files) move to a new library, `PaperRoute.Core`. By reading 1's classification, 144 files move unchanged, 20 need a small change, 5 need an interface first, and 7 are interface code that stays with the interface; the first move also leaves `FileOpenService` and `UpdateService` behind until their interfaces exist (section 1). A checked-in script lists every Windows-only API that is left (Appendix A). |
| What is rewritten | Every form, control, `Form1` partial, the six interface services and the start-up. The new interface project is **C#**, over the VB library by an ordinary project reference. The interface reaches the services through one application-services object built from `Form1`'s existing test seams, and through small interfaces for dialogs, file opening, single instance, secrets and updates. |
| How parity is proved | The view catalog in `PaperRoute.Tests/ViewCatalog.vb` becomes a parity map: each of today's 85 views and stages names its place in the target map, and a test fails if one has none. Legibility rules, keyboard routes and the guide's shortcut table are re-tested on Avalonia's headless test platform. A scripted session on Linux must leave the data files byte-identical to Windows once the managed-folder path is normalised. |
| How certification carries over | The installed-app gates run once per platform. A Linux guest in VirtualBox on the present PC stands in for the Windows VM; macOS needs a real Mac. The WinForms 1.0.x line keeps its certification through the existing carry-over rules until the Avalonia build replaces it. |
| Packaging | Parallel build jobs (Windows, Linux, later macOS), one upload step, one verifier run, one `SHA256SUMS.txt`, one draft release. Windows keeps the `stable` and `preview` channels; Linux and macOS get channels named by platform. macOS must be signed and notarised. |
| Sequencing and size | Settled: v1.1 before v1.2; 1.0.x on WinForms stays Stable; groundwork first; Windows and Linux first. Recommended: build the Avalonia project on `master` beside the WinForms app, never in the same release. |

**What it costs.** About **43 to 77 weeks** of one maintainer's work for the v1.1 Preview on Windows and Linux, with a midpoint near 60 weeks. That is the sum of 19 interface build units sized from the source (30 to 55 weeks) and six pieces of groundwork, testing, packaging, guide and certification work (13 to 22 weeks). macOS adds 3 to 6 weeks in v1.1.x. These are judgements from source size, not measurements. The December trial port measures one unit, the Board, and the estimate is then rescaled by the rule in section 7.

**What is asked of the maintainer.** Sign off this plan on #160, or say what to change. Then answer the questions in section 12; the four that block early work are: C# for the interface (the December checkpoint); `master` or a long-lived branch; whether 1.0.x Preview installs should be offered the Avalonia Preview; and whether a Linux guest in VirtualBox is acceptable as the Linux certification machine.

---

## 1. What stays

### Facts

| Measure | Value | Source |
|---|---|---|
| Models | 76 files, 2,517 lines; no reference to any Services or interface type; import only `System` and `System.Collections.Generic` | reading 2, section 0 and 7 |
| Services | 100 files, 48,018 lines (the issue's 91 files and about 31,700 lines are older counts) | reading 2, section 0 |
| Rows in the generated inventory | 665: 430 windows-only, 225 needs-reading, 10 portable-note | `Windows-Only-API-Inventory.md` |
| Files with a windows-only or needs-reading row | 49, all in Services; 127 files have none; no Models file has a row | same |
| Classification of the 176 files | A (moves unchanged) 144, B (small change) 20, C (needs an interface first) 5, D (interface code, stays) 7 | reading 1 |
| Rows in the 7 D files | 369 of the 430 windows-only rows | reading 1 |
| System.Drawing rows | 226 in 6 files; 144 in `ManuscriptPipeline/Services/UiTheme.vb` | inventory |
| Why type names, not only `Imports`, are counted | the project imports `System.Drawing` and `System.Windows.Forms` for every file (`ManuscriptPipeline/ManuscriptPipeline.vbproj:26-28`) | reading 1 |
| Upward references from Models and Services into the interface | 2 files, 17 lines: `Services/UiPolish.vb:8` and 12 control names; `Services/UpdateService.vb:7,73,76,219,375` (`Form1`, `UpdateProgressForm`, `UpdatePromptForm`) | reading 2, section 1 |
| Movable files that call a staying interface service | 5 lines in 3 files: `Services/WorkTypeService.vb:157,172`, `Services/ErrorReportService.vb:140`, `Services/UserGuideService.vb:35-36` | reading 2, section 1a |
| Shell launches (`UseShellExecute = True`) | 2 in Services (`FileOpenService.vb:116`, `UrlSafetyService.vb:92`), 3 in the project (also `Forms/DiagnosticsForm.vb:435`). The issue's "10 launch sites" does not reproduce; it may count callers (`UrlSafetyService.OpenInBrowser` is called on 22 lines in 16 interface files). | reading 1 |
| Calls that fail off Windows | named `EventWaitHandle` (`SingleInstanceService.vb:55,64`); DPAPI `ProtectedData` (`ProtectedKeyStore.vb:88,100`); `kernel32 GetFileInformationByHandle` (`PacketExportService.vb:1428`); `user32 AllowSetForegroundWindow` (`SingleInstanceService.vb:20`) | reading 1 |
| Not found | `Microsoft.Win32` (Registry, SystemEvents), VB `My.*`, `InputBox`, `MsgBox`, `System.Management`, `EventLog`, hard-coded drive letters, `%APPDATA%` | inventory |
| Packages | Anthropic 13.3.0, ClosedXML 0.105.1 and Velopack 1.2.161 all run on Linux and macOS (`ManuscriptPipeline.vbproj:32-34`). The issue's "Velopack 1.2.0" is out of date. | readings 1 and 5 |
| Line endings | None of the 15 indented JSON writers (11 files, for example `ManuscriptRepository.vb:121-125`) sets `JsonSerializerOptions.NewLine`, so data written on Linux would have LF line breaks where Windows writes CR LF. Stored text also takes the platform newline: `TitlePageParserService.vb:409,1072`, `FlexibleExcelImporter.vb:1095`. | reading 1; checked: 16 `WriteIndented = True` or `NewLine =` lines in 12 Services files, the extra one being `PacketExportPackage.vb`'s fixed `vbLf` |
| Managed-file paths | Stored as absolute paths: the managed root under Documents plus ids and the file name (`ManagedLibraryService.vb:206-218`, written at `:1571-1596`). Restore rewrites them onto the local root, taking the file name with `Path.GetFileName` of the stored path (`PortableRestoreService.vb:1984-1996`, `:2009-2017`). | checked while writing |

The last row matters for Linux. On Linux and macOS `Path.GetFileName` treats only `/` as a separator, so restoring a backup made on Windows would read `C:\Users\...\file_ab12cd34.docx` as one file name. This was found by reading, not run. It belongs to stage 2 of the split (section 10).

**Per-OS folders.** The data root is `LocalApplicationData` plus the data folder name, and the managed library is `MyDocuments` plus `PaperRoute Library` (`StorageMigrationService.vb:87-132`). Since .NET 8, `MyDocuments` is `XDG_DOCUMENTS_DIR` or `$HOME/Documents` on Linux and `$HOME/Documents` on macOS, and `LocalApplicationData` on macOS is `Library/Application Support`; on Linux `LocalApplicationData` was not changed by .NET 8 and is `$HOME/.local/share` (Microsoft, ".NET 8 breaking change: GetFolderPath behavior on Unix", https://learn.microsoft.com/en-us/dotnet/core/compatibility/core-libraries/8.0/getfolderpath-unix, read November 14, 2026; whether `XDG_DATA_HOME` is honoured was not checked). So the code needs no change for folders; the guide needs one table of them.

### Recommendation

Move all 76 Models files and the 91 Services files that use no interface toolkit into a new VB library, `PaperRoute.Core`, targeting plain `net10.0`, in two stages. Stage 1 moves them and changes nothing for the user. Stage 2, before any Linux build, makes the B changes, adds the five C interfaces with their Windows and Linux implementations, and fixes the restore path defect. Keep the inventory script in the repository and make "zero windows-only rows in the core" the acceptance of stage 2.

What replaces each category (full table in reading 1; the per-file list is in Appendix A's output):

| Category | Replacement |
|---|---|
| WinForms and System.Drawing in the 7 D files (`UiTheme`, `UiPolish`, `UiMotion`, `WindowChrome`, `DialogScaling`, `ResponsiveDialogSizingService`, `WindowsNotificationService`) | Stay with the WinForms app; rewritten as Avalonia styles and services in the new interface. |
| Colours in the core | Only `WorkTypeService.Background/Foreground` (`:154-183`); they move to the interface side. Colours, where the core needs them later, are plain ARGB values. |
| Shell launches | One `ILauncher` (open file, open link, show in folder) that the interface supplies; Avalonia's `Launcher` service does this on all three systems (https://docs.avaloniaui.net/docs/services/launcher, read November 14, 2026). The program-file warning (`FileOpenService.vb:32-47`) stays in the core and gains Linux and macOS extensions (`.sh`, `.desktop`, `.AppImage`, `.app`, `.command`, the executable bit). |
| Single instance | `ISingleInstance`: Windows as today; Linux and macOS a lock plus a local socket in the data root for "come forward". |
| Secrets | `ISecretProtector`: DPAPI on Windows, the Secret Service on Linux, the Keychain on macOS. `OnlineAccess.KeyStoreFactory` (`OnlineAccess.vb:158`) is the existing seam. Keys protected on one OS cannot be read on another, as today across Windows accounts (`ProtectedKeyStore.vb:42-43`). |
| Updates | `IUpdateUi` for the prompts; the Velopack manager, channels and `UpdateSteps` (`UpdateService.vb:358-388`) stay portable. The version text is passed in from the app, not read in the core (reading 2, step 3). |
| File identity | Guard `GetFileInformationByHandle` with `OperatingSystem.IsWindows()`; the code already falls back to comparing paths (`PacketExportService.vb:1399-1401`). |
| Path comparison | One OS-aware comparer, used at `ManagedLibraryService.vb:112,147,281`, `StorageMigrationService.vb:1145`, `StorageEnvironment.vb:52`, `PacketExportService.vb:1483`. |
| Line endings | One shared JSON options factory with `NewLine = vbCrLf` (15 sites in 11 files), and a fixed newline in stored text. On Windows this writes the same bytes as today. |

### Reasons

- 144 of 176 files have nothing to replace, and the 5 lines that tie movable files to interface services are known (reading 2, section 1a).
- A plain `net10.0` target makes a new use of WinForms in the core a compile error. The fallback, `net10.0-windows` for stage 1, keeps WinForms reachable and invites new upward use (reading 2, section 7).
- The line-ending and path changes do not change any Windows byte, so they can land on the WinForms line and be certified there first.

### Alternatives and why not

- **Move the 9 files reading 2 calls "interface services", including `FileOpenService` and `UpdateService`, into the core behind interfaces in stage 1** (reading 1's C class). Not in stage 1: it adds interface work to a move that should change nothing. Reading 1 and reading 2 disagree here; this plan takes reading 2's smaller first cut and reading 1's interfaces in stage 2.
- **Port Models and Services to C#.** A second migration of 50,535 lines and their 160-file VB test suite, for nothing the interface needs (reading 3, section 5).

### What would change it

A compile of the retargeted core showing late binding under `Option Strict Off` that fails on `net10.0` (not inventoried, reading 2), or CA1416 platform warnings treated as errors.

---

## 2. What is rewritten

### Facts

| Item | Today (`d67c312`) | Issue says | Source |
|---|---|---|---|
| Window classes | 63: 62 in `Forms\` plus `Form1` (`Forms\OpenAlexKeyForm.vb:9` inherits `ApiKeyForm`) | 59 | reading 3 |
| `Forms\` | 71 files, 47,487 lines | 66 files, about 33,000 | reading 3 |
| `Controls\` | 33 files, 10,189 lines, 28 control classes | 19 controls, about 3,300 | reading 3 |
| Main window | 18 files, 12,533 lines (`Form1.vb` 5,120; 16 partials 7,382) | 15 partials, about 6,600 | reading 3 |
| `UiPolish` | 1,126 lines | 582 | reading 3 |
| All interface code to rewrite | **73,080 lines** (70,278 plus 2,802 in six interface services) | about 43,500 | reading 3 |
| Workaround code that is deleted, not ported | about 2,866 lines of `Controls\` (`PageFade` 1,225, `CheckGlyph`, `ThemedBorder`, `ThemedDatePicker`, `InPlace` and others); 55 `DllImport` lines in the interface, 28 in `PageFade.vb` | | reading 3, 2.1 |
| View models | none: `INotifyPropertyChanged`, `ICommand`, `ObservableCollection`, `BindingSource` appear 0 times; the closest is `EditManuscriptForm.RecordState` (`Forms\EditManuscriptForm.vb:20-64`) | | reading 3, 3.4 |
| Test seams on `Form1` | 13 `Protected Overridable` methods (for example `Form1.vb:1459, 1590, 4239, 4539`; `Form1.ManuscriptPage.vb:82`; `Form1.Shell.vb:1637, 1646`); 12 `Friend` prompt delegates (for example `Form1.ManuscriptPage.vb:72`, `Form1.RecoveryDraft.vb:18`), 23 more in `Forms\`; 96 reflection sites into 34 private members | issue: "four subclass `Form1`" (there are 15) | readings 3 and 4 |
| VB-only features in the interface | 3 `WithEvents`, 7 `Handles`, 1 `InputBox` (`Form1.vb:4330`); no `My.*` calls, XML literals or `On Error`. The start depends on the VB application framework (`Program.vb:114`, `ApplicationEvents.vb:8-87`). | | reading 3, section 5 |
| No built-in Avalonia counterpart | the report preview's `WebBrowser` (`Forms\ReportForm.vb:26`); 6 `DataGridView`s with in-cell editing (for example `TitlePageImportForm`, `ExcelMappingForm`) need the separate DataGrid package and templated columns | | reading 3, 2.2 |
| Avalonia | 12.1.6 is the latest stable (November 9, 2026), MIT licence, net8.0 and net10.0 builds (https://www.nuget.org/packages/Avalonia, read November 14, 2026). 12.0 (April 7, 2026) targets .NET 10, reworked the compositor and added a native Linux accessibility backend (https://avaloniaui.net/blog/avalonia-12, read November 14, 2026). | | reading 5 |
| Avalonia and VB | Official templates are C# and F# only (https://github.com/AvaloniaUI/avalonia-dotnet-templates, read November 14, 2026). The only VB name generator is the community `Nukepayload2.SourceGenerators.AvaloniaUI` 1.0.0, tested only with Avalonia 11.0.9 (https://www.nuget.org/packages/Nukepayload2.SourceGenerators.AvaloniaUI, read November 14, 2026). Whether compiled XAML works in a VB project on Avalonia 12 was not checked. | | reading 5 |

### Recommendation: the language of the interface project

A **C# interface project** (`PaperRoute.Avalonia`, name to be settled in the first build issue) over the VB `PaperRoute.Core`, by an ordinary project reference. The new interface tests are C# too. Models, Services and their tests stay VB. The maintainer left this open for the December checkpoint; the trial port should confirm it.

**Reasons.**
- The interface is a rewrite in any language: no WinForms control code carries over, so keeping VB saves almost no text in the interface (reading 3, section 5).
- The interface uses almost no VB-only features.
- Avalonia's templates, generated `x:Name` fields and compiled bindings are C#-first; for VB there is one untested community generator, and view-model change notification would be written by hand.
- C# compiles with strict typing; the VB project runs with `Option Strict Off` by the SDK default (reading 2, section 3).

**Alternatives and why not.**
- **VB code-behind through `AvaloniaXamlLoader`.** It keeps one language for 50,535 library lines, 92,494 test lines and the interface, and keeps the view models textually close to the VB code they replace. But it rests on a build path nobody has shown working on Avalonia 12, with no template and no generators. Worth one hour in the trial; not the default.
- **A full port to C#.** See section 1.

**What would change it.** The trial shows a VB Avalonia 12 project with compiled XAML and compiled bindings building cleanly, and the maintainer weighs one language above the tooling.

### Recommendation: how the interface reaches the services

1. **Services stay as they are.** The five source interfaces already in Services (`IAssistantProvider`, `ICitationSource`, `IJournalFactsSource`, `IJournalSuggestionsSource`, `IPublicationSource`) carry over unchanged (reading 3, 3.1).
2. **One application-services object** (a "library session"): load, save on a worker with the failure flag, backup, restore, restart and recovery drafts, lifted from `Form1`'s 13 overridable seams and its save and load steps (`Form1.vb:1590-1641`). The shell view model and the tests both use it. It replaces the 96 reflection sites.
3. **Each prompt delegate becomes a method on a dialog-and-notice service** (`IInteractions`, name open): the 35 `Friend` prompt delegates mark exactly where the interface asks the outside world something, and the target map turns most of them into notices or panels anyway.
4. **`ManuscriptEditorViewModel` is built around today's `RecordState`** (original, working copy, baseline snapshot, `EditManuscriptForm.vb:20-64, 698-700`), so "one save boundary per manuscript" lives in one object.
5. **Access across the assembly line:** the core grants `InternalsVisibleTo` to the app, the tests, `PaperRoute.V04Demo`, `PaperRoute.DisplayEvidence` and later the Avalonia project; no `Friend` member needs to become `Public` in stage 1 (reading 2, section 2). Before v1.1 ships, the 8 `Friend` classes reading 3 names as used by the interface (its summary says 9; the list has 8) (`SingleInstanceService`, `StorageFile`, `StorageFileInUseException`, `PacketExportPackage`, the four `Assistant*` types) become `Public`, so the C# project does not depend on a friend grant.

A "view model" here means a plain class that holds what a page shows and the commands it offers, and tells the page when a value changes; the page binds to it instead of setting control text by hand. Whether to use a helper library for this is left to the first build issue (not checked here).

**Alternatives and why not.** Keeping the reflection seams is impossible: they name WinForms fields. Passing services through every page's constructor, as 10 of 95 form constructors do today (reading 3, 3.1), spreads the save boundary across pages.

**What would change it.** The trial's working-copy spike (section 10) showing that `RecordState` does not fit a bound view model.

---

## 3. How parity is proved

"Parity" means the Avalonia build does everything the WinForms build does, in the target map's new shape, no worse for keyboard, screen-reader or contrast users, and with the same data on disk.

### Facts

| Item | Value | Source |
|---|---|---|
| Test suite | 160 files, 1,448 methods, 413 data rows, 1,741 results; MSTest 4.5.2 on `net10.0-windows` (`PaperRoute.Tests/PaperRoute.Tests.vbproj:4-5, 13`) | reading 4; `PaperRoute.Tests/README.md:3` |
| Tests that reach a real form | 621 of 1,448 methods (heuristic) | reading 4 |
| Pure Models and Services files | 77 files, 613 methods, plus about 130 pure methods in mixed files | reading 4 |
| Form-driving files | 64 call `StaRunner.Run` or `RunOnStaThread` (the issue says 23); 15 classes subclass `Form1` | reading 4 |
| View catalog | `ViewCatalog.vb`: 63 dialogs (`:53-117`), 12 hosted views (`:123-136`), 10 stages (`:144-155`); 75 views, 85 with stages; kept complete by `EveryViewHasAFactoryOrAReasonItHasNoneYet` (`:1177-1178`) | reading 4 |
| Legibility rules | WCAG contrast (`TestSupport.vb:182-211`); 4.5:1 for text, 3:1 for disabled captions and contrast-theme edges; "light" means relative luminance above 0.6 (`DarkThemeLegibilityTests.vb:1018-1020`); a 5-entry Dark allow-list (`:34-40`); rules R1 to R11 in reading 4, section 2 | reading 4 |
| Keyboard routes in the guide | `docs/USER_GUIDE.md:1041-1074`: 13 window-wide shortcuts and 10 bullets; tied to the code by `UserGuideServiceTests.vb:946-1019` (a scan of `ProcessCmdKey`) and 19 methods in `KeyboardAccessTests.vb` | reading 4 |
| Items 56 and 62 (v0.9.1 plan) | access keys exist in 15 windows (`ManualCertification/Workflow-Consistency-v0.9.2.md:15`); the help control is still 19 by 19 (`Forms/ContextHelpControl.vb:43-45`) | reading 4 |
| Byte-identical check today | a hash list, `Get-ChildItem | Get-FileHash | Export-Csv` over the data, managed and linked folders (`ManualCertification/v1.0-Certification-Checklist.md:428-430`); the collector (`ManualCertification/FixtureCollector/Program.cs`, 293 lines) is a `win-x64` program with Windows folders | reading 4 |
| Avalonia headless testing | real pixels need `.UseSkia()` with `UseHeadlessDrawing = false`, then `CaptureRenderedFrame()`; keyboard input is simulated per window; official adapters for xUnit v3 (`Avalonia.Headless.XUnit` 12.1.6) and NUnit; no MSTest package (https://docs.avaloniaui.net/docs/concepts/headless, https://www.nuget.org/packages/Avalonia.Headless.XUnit, read November 14, 2026) | readings 4 and 5 |

### Recommendation

Prove parity with five checks, all in code except the last:

1. **A parity map over the view catalog.** The Avalonia test project gets its own catalog of target surfaces (pages, panels, Stage panels, the modal windows that stay) with a factory for each, and a data table that maps each of today's 85 catalog keys to a target surface from the target map (5a to 5e). Two tests: every target surface has a factory (as `EveryViewHasAFactoryOrAReasonItHasNoneYet` does today), and every WinForms key has a destination or a named reason (removed as a duplicate route, 5e). This is what "the Avalonia build's view catalog matches the target map of fewer, calmer surfaces" means in a test.
2. **Legibility, in two tiers.** R1 to R3 (token pairs at 4.5:1, contrast-theme tokens, route kinds told apart without colour) as plain colour tests over the style tokens. R5, R7, R8 and R9 (no light parts in Dark, strongest-pixel 4.5:1, disabled text 3:1, edges 3:1) on headless rendering with Skia. The thresholds, the allow-list mechanism and the strongest-pixel rule carry over. R8 becomes "a disabled control's text is at least 3:1 on its surface"; the WinForms-only parts (the "standard Windows button" rule, the caption recolouring test, title-bar chrome) are dropped as not applicable, and the plan says so in the issue that ports them.
3. **Keyboard.** The guide's shortcut table is tested against the Avalonia key bindings instead of `ProcessCmdKey`'s source. The 19 keyboard tests are rewritten against the new pages. Items 56 and 62 are acceptance lines of the pages they touch: access keys and one default-button rule in the most-used dialogs (the existing sweep `NoWindowRepeatsAnAccessKeyItShowsTogether` is the model); tooltip text mirrored into accessible descriptions; a help control of 24 logical pixels; a single roving tab stop on Deadlines rows; arrow keys between Board cards.
4. **Same data on disk.** A scripted session (open the standing fixture, edit, save, back up, restore) runs on Windows and on Linux over the same restored library. The data files must be byte-identical after one normalisation: the managed root prefix and the path separator in the stored `LocalFilePath` fields. Everything else, including line breaks and JSON layout, must match byte for byte. This needs the stage 2 line-ending fix first, and a collector build per OS with the folders taken from `Environment.SpecialFolder`.
5. **Screen readers by hand.** Narrator on Windows and Orca on Linux, per page, recorded like today's Narrator pass. Avalonia's docs call its accessibility "Full support" on all three systems, but fixes were still landing in 12.1.4 to 12.1.6 (reading 5), so it is proven per view, not assumed.

**Test framework.** The core tests stay MSTest and move unchanged into a `net10.0` core test project that also runs on `ubuntu-latest`. The new interface tests use **xUnit v3 with `Avalonia.Headless.XUnit`**, the official adapter, unless the December trial shows a hand-written MSTest harness working in under a day.

### Reasons

The catalog already keeps itself complete by reflection; a parity map extends that guarantee across the change of shape. The legibility rules are numbers, not WinForms behaviour. Normalising only the path prefix keeps the strictest comparison that can hold: absolute paths differ between a Windows and a Linux home folder by design.

### Alternatives and why not

- **Byte identity with no normalisation.** Impossible while paths are absolute (`ManagedLibraryService.vb:206-218`).
- **Library-relative paths in the data files.** Would make the files identical without normalising, but it is a schema change with a migration; it could join Schema 10 (#157) if the maintainer wants it, not as part of the toolkit change.
- **MSTest for the interface tests by a hand-written harness.** Keeps one framework and today's `TestCategory` filters, but nothing supports it officially.

### What would change it

Headless Skia rendering giving different strongest-pixel results from on-screen rendering (fonts, smoothing), which would push R7 to a tolerance or to on-screen tests.

---

## 4. How certification carries over

### Facts

- The per-install gates today: v1.0's E1.1 to E11.1 (`ManualCertification/v1.0-Certification-Checklist.md:457-775`) and E12 (`:815-825`); v1.0.1's V1 to V16 (`ManualCertification/v1.0.1-Certification-Checklist.md:175-277`) and S1 to S5 (`:301-316`); lines carry over between releases on unchanged files (`:318-331`) (reading 4).
- Windows-specific gates: SmartScreen and Edge on first run, Defender, Start menu and Apps entry, DPI awareness, OneDrive, Narrator and Windows contrast themes, WinGet (reading 4, section 7).
- E2.12 (a file held with `FileShare.None`) depends on Windows share-mode locking; Linux and macOS do not lock files by default, so that gate needs a new design there (reading 4; inference).
- Velopack on Linux makes one `.AppImage` and no installer; updates replace the file in place and ask for `pkexec` in a privileged folder (https://docs.velopack.io/packaging/operating-systems/linux, read November 14, 2026). On macOS it makes an `.app`, a `.pkg` and a portable `.zip` (https://docs.velopack.io/packaging/operating-systems/macos, read November 14, 2026).
- macOS may run in a virtual machine only on Apple hardware (an Apple Developer Forums thread, https://developer.apple.com/forums/thread/845455, read November 14, 2026; Apple's licence itself was not read), so the Windows PC cannot host a macOS certification machine.

### Recommendation

- **The WinForms line keeps its certification** until the Avalonia build replaces it: 1.0.x patch releases carry the E and V lines by the existing carry-over rules, and the Windows VM stays as it is.
- **Each platform gets its own gate set with the same five gates** the issue names: clean install, update from the previous version on that platform's feed, backup and restore round trip, the recovery fixtures, uninstall keeps the library.
- **Linux stand-in:** a Linux guest in VirtualBox on the present PC, with snapshots and a clean profile like the Windows VM. Recommended distribution for v1.1: Ubuntu 24.04 LTS with GNOME, plus Fedora 43 if time allows. Linux gates: download and mark executable, AppImage start on a stock system (FUSE), first-run hook time, data folders, update in place (and the `pkexec` path), uninstall by deleting the file.
- **macOS stand-in (v1.1.x):** a real Apple-silicon Mac. A hosted `macos-latest` runner can run the automated gates but cannot show what Gatekeeper shows a user who downloaded the file.
- **E2.12 on Linux:** recorded as "not applicable as written"; the counterpart gate is decided with the build issue for start-up and recovery.
- **The guide** gains "What Linux shows" beside "What Windows shows" (`docs/USER_GUIDE.md:32`), and later "What macOS shows".

### Reasons

The same host, the same evidence-JSON method, and no new hardware for v1.1. The WinForms line cannot lose its certification by a change it does not ship.

### Alternatives and why not

A physical Linux machine gives real GPU rendering and Wayland, but adds hardware and does not replace snapshots. Hosted runners alone cannot observe first-run prompts.

### What would change it

Avalonia rendering badly inside a VirtualBox guest (its OpenGL path was not checked), which would push the Linux gate onto a physical machine.

---

## 5. Packaging on three platforms

### Facts

- `.github/workflows/release.yml` is one job on `windows-latest` (`:19`): `win-x64` self-contained publish (`:183-194`), `vpk pack ... --channel stable|preview --noPortable true` (`:274-288`), a new draft with `--merge false` (`:290-319`), then `Verify-ReleaseAssets.ps1` Stage, documents and `SHA256SUMS.txt`, Verify, publish (`:321-387`) (reading 5). The line numbers in the issue (`144-155`, `233-246`) are older.
- `UpdateService` pins `ExplicitChannel` to the literal `"stable"` or `"preview"` (`UpdateService.vb:317-320, 330-340`). A Linux or Mac build would read the Windows feed, `releases.stable.json` (reading 5).
- Velopack's docs: "every unique os/rid has its own channel" (https://docs.velopack.io/packaging/channels, read November 14, 2026). macOS packages can be built only on macOS; Linux packages can be built on any OS (https://docs.velopack.io/packaging/cross-compiling, read November 14, 2026). So the issue's "vpk pack runs on the OS it packages for" is strictly true only for macOS.
- `vpk upload github --merge` adds to an existing release; uploading a feed file that already exists is refused (Velopack source at tag 1.2.161, `GitHubUploadCommand.cs:26-27`, `_GitRelease.cs:164-194`, read November 14, 2026).
- `Verify-ReleaseAssets.ps1` and `ManualCertification/Build-ReleaseCandidate.ps1` are Windows-specific: channel set (`:19`), `publish/win-x64` (`:23`), the `win-x64` runtime-pack notices, exactly one `*Setup.exe`; the candidate script refuses to run off Windows (`:30`) and reads WinForms' `HighDpiMode` from IL (`:189-303`) (reading 5).
- macOS: notarisation is required by default (https://support.apple.com/en-us/102445, read November 14, 2026); since macOS Sequoia, Control-click no longer overrides Gatekeeper (https://developer.apple.com/news/?id=saqachfa, August 6, 2024, read November 14, 2026); the Apple Developer Program costs 99 USD a year (https://developer.apple.com/programs/enroll/, read November 14, 2026).
- GitHub-hosted runners: `ubuntu-latest` is x64; `macos-latest` is arm64; `macos-15-intel` is x64; standard runners are free for public repositories, and this repository is public (https://docs.github.com/en/actions/reference/runners/github-hosted-runners, read November 14, 2026).

### Recommendation

| Job | Runner | Platform | Channels (Stable / Preview) | Assets |
|---|---|---|---|---|
| build-win | `windows-latest` | `win-x64` | `stable` / `preview` (unchanged) | full and delta `.nupkg`, `-Setup.exe`, feed |
| build-linux | `ubuntu-latest` | `linux-x64` | `linux-x64-stable` / `linux-x64-preview` | full and delta `.nupkg`, `.AppImage`, feed |
| build-osx (v1.1.x) | `macos-latest` | `osx-arm64` | `osx-arm64-stable` / `osx-arm64-preview` | full and delta `.nupkg`, `-Setup.pkg`, feed; signed and notarised |
| upload | one runner | all | each | first upload `--merge false`, later ones `--merge true`, all `--publish false` |
| verify and publish | one runner | all | all | one Stage run, one `SHA256SUMS.txt` over every asset and the documents, one Verify run, publish |

- Each build job runs its own tests before packing: Windows today's suite, Linux the core tests and the headless interface tests.
- `UpdateService.ChannelName` becomes a function of OS, architecture and Stable or Preview, with Windows unchanged, and a test ties it to release.yml's `--channel` values.
- The verifier and the candidate script get a per-platform table: channel name, installer pattern, AppImage count, required native files (`libcoreclr.so` and `libhostfxr.so` on Linux in place of `coreclr.dll`, `hostfxr.dll` and `System.Windows.Forms.dll`), and each runtime pack's notices. The `HighDpiMode` IL check retires with the WinForms line.
- macOS signing and notarisation are required for v1.1.x, not optional: Developer ID Application and Installer certificates, a `notarytool` profile, a temporary keychain in CI, and the secrets added to the signing decision of #43 and #216.
- **`osx-x64` is left out** unless the maintainer wants Intel Macs: news reports of WWDC 2025 say macOS 26 is the last release for Intel Macs, while .NET 10 still lists macOS 27 on x64 (reading 5); testing it needs an Intel Mac.
- Minimums to state: Ubuntu 22.04 or later, Debian 13, Fedora 43 or later, x64 only, for v1.1; macOS 15 for v1.1.x. These are the stricter of .NET 10's list (https://github.com/dotnet/core/blob/main/release-notes/10.0/supported-os.md) and Avalonia's tiers (https://docs.avaloniaui.net/docs/supported-platforms), both read November 14, 2026.

### Reasons

One draft, one checksum file and one verifier run keep today's guarantees. Keeping Windows' channel names keeps every 1.0.x install, the WinGet manifest's `-stable-Setup.exe` address and its tests valid. Building the AppImage on Ubuntu costs nothing extra, because the Linux tests must run there anyway.

### Alternatives and why not

- **The issue's matrix with each job uploading its own channel in a chain.** The fallback if `vpk upload github` cannot upload a release folder built on another OS (not checked).
- **Renaming Windows' channels to `win-x64-stable`.** Velopack's own example, but it strands every installed 1.0.x on the old feed name.
- **Shipping macOS unsigned like Windows.** Every user would have to open System Settings to run it.

### What would change it

A shared Windows `preview` feed means 1.0.x WinForms Preview installs are offered the Avalonia Preview by default (reading 5). If the maintainer does not want that, the Avalonia build needs its own Windows Preview channel name, and the 1.0.x Preview users a notice.

---

## 6. Sequencing and size

### Facts (settled by the maintainer, November 14, 2026)

1. The order stays: v1.1 is the Avalonia interface; v1.2 is built once, on Avalonia; the order is looked at again at a mid-December checkpoint.
2. v1.0.1 ships first, on WinForms, with no new windows.
3. Groundwork starts alongside: #161's inventory and target map, then the Models and Services library, merged only after v1.0.1 is out, then a one-week trial port of the Board in December, not merged.
4. Windows and Linux first; macOS as v1.1.x.
5. Still open for December: the interface language, what the trial tests besides the Board, the estimate in weeks.

### Recommendation

- 1.0.x on WinForms stays the Stable line and takes data-safety fixes only after v1.0.1 (whose wider scope the maintainer approved as an exception). No release mixes the two toolkits.
- **Build the Avalonia project on `master`, beside the WinForms app, both over `PaperRoute.Core`.** `release.yml` packs the WinForms app until the switch. At the switch, a `release/v1.0.x` branch is cut for WinForms Stable patches, and `master` packs the Avalonia app as the v1.1 Preview. Interim Avalonia builds are CI artifacts, never releases.
- #158 (Stable releases offered on the Preview channel) ships with v1.1's first Preview, as already scheduled.
- The two dependent v1.1 issues, #161 (fewer, calmer surfaces) and #162 (composition-grade motion), cite this plan once it is signed off.

### Reasons

With one core, a data-safety fix lands once and is tested against both interfaces. A branch kept open for most of a year would diverge from every 1.0.x fix in the core.

### Alternatives and why not

**A long-lived Avalonia branch**, as the issue words its default. It keeps `master` WinForms-only and CI time unchanged, but every core fix has to be merged across, and the split's value (one library, one test suite) is lost until the end.

### What would change it

CI time on `master` growing past what the maintainer accepts, or the Avalonia project's build breaking `master` often.

---

## 7. The estimate in weeks

### Build units

From reading 3, section 4.2: 19 units covering all 73,080 interface lines. Its sizes are S (under about 1 week), M (1 to 2), L (2 to 4) and XL (over 4 weeks of one maintainer), judged from source size, drawing technique and how much the target map changes the shape.

| # | Unit | Lines replaced | Size |
|---:|---|---:|:-:|
| 0 | Style system and theme | 5,409 | M |
| 1 | Shell and navigation (rail, page host, notice strip, the application-services boundary) | ~4,815 | L |
| 2 | Board | ~5,542 | L |
| 3 | Manuscript page: frame, Overview, Authors | 10,863 | XL |
| 4 | Manuscript: new-manuscript state | 1,048 | M |
| 5 | Manuscript: Versions | 3,123 | M |
| 6 | Manuscript: Submissions | 5,667 | L |
| 7 | Manuscript: Readiness and Packets | 6,472 | XL |
| 8 | Manuscript: Route section | 1,797 | M |
| 9 | Library | ~2,587 | M |
| 10 | Journals | 3,869 | M-L |
| 11 | Deadlines | 3,276 | M |
| 12 | Insights | 2,676 | M |
| 13 | Import and Export | ~5,028 | L |
| 14 | Settings page | ~3,043 | M |
| 15 | One Stage panel for assistants and lookups | 4,592 | L |
| 16 | Help panel and example library | 872 | S |
| 17 | Start-up, errors and recovery | ~2,081 | M |
| 18 | Updates | ~320 | S |

### Arithmetic

Assumptions: S = 0.5 to 1 week; M = 1 to 2; M-L = 1.5 to 3; L = 2 to 4; XL = 4 to 6 (reading 3 gives "over 4"; the cap of 6 is this plan's assumption). Each unit's size includes its view models and its own headless tests.

| Part | Count | Low (weeks) | High (weeks) |
|---|---:|---:|---:|
| S units | 2 | 1 | 2 |
| M units | 9 | 9 | 18 |
| M-L units | 1 | 1.5 | 3 |
| L units | 5 | 10 | 20 |
| XL units | 2 | 8 | 12 |
| **Interface units** | 19 | **29.5** | **55** |
| Split, stage 1 (section 10, steps 1 to 8) | | 2 | 3 |
| Split, stage 2 (B changes, five interfaces with Linux implementations, core tests on Linux) | | 2 | 4 |
| Shared parity harness (headless project, catalog and parity map, legibility rules, keyboard sweep, replacements for the source-scan tests) | | 3 | 5 |
| Packaging (release matrix, channel names, verifier, candidate script, notices per platform) | | 2 | 4 |
| Guide (walkthroughs for the new windows, "What Linux shows", keyboard section) | | 2 | 3 |
| Linux certification set, first run, fixes | | 2 | 3 |
| **Total for the v1.1 Preview (Windows and Linux)** | | **42.5** | **77** |
| macOS in v1.1.x (signing, `osx-arm64` job, Mac gates, menu bar and Command-key shortcuts, not examined) | | 3 | 6 |

So: **about 43 to 77 weeks**, midpoint near 60, before calibration. In months of one maintainer, roughly 10 to 18. The issue's "months, not weeks" stands.

### Calibration from the December trial

The trial ports the Board (unit 2, L, 2 to 4 weeks, midpoint 3) for one week. If the week finishes a share *f* of the Board's checklist (section 10), the Board's measured size is 1/*f* weeks and the scale factor is k = (1/*f*) / 3. The rescaled estimate is k times 60 weeks. For example, half the Board in the week gives 2 weeks, k = 0.67, about 40 weeks. A trial that finishes the Board early gives *f* above 1.

The Board does not measure the manuscript page (units 3 to 8: 27,922 lines, about 38% of the interface, and the save-boundary redesign; reading 3). The working-copy spike in section 10 is the check on that part; if it goes badly, units 3 and 7 move to the top of their bands whatever k says.

### What the estimate leaves out

Answers to #161's open questions 1, 4, 5, 6 and 12 change the save boundaries and so the size of units 2, 3, 6, 7 and 10 (reading 3). Composition-grade motion (#162) and the issues already in the v1.1 milestone (#157, #297, #333, #334, #365, #368) are not in these weeks.

---

## 8. Build issues to open

To open after sign-off; not opened by this plan. Each interface issue's acceptance carries the line "**the Avalonia build's view catalog matches the target map of fewer, calmer surfaces**" for the target-map rows it builds, and "**the guide's walkthroughs for these surfaces are rewritten for the new windows**". Issue 21 carries both lines for the whole app.

1. **Move Models and Services into `PaperRoute.Core` (stage 1).** Steps 1 to 8 of section 10. Acceptance: full suite green after each step; installed build's About box and a new backup's manifest report the app version; the inventory shows no WinForms, System.Drawing or interface-type row in the core.
2. **Make the core run on Linux (stage 2).** The 20 B changes, the five interfaces with Windows and Linux implementations, the restore path fix, a `net10.0` core test project on `ubuntu-latest`. Acceptance: the inventory script reports zero windows-only rows for the core; the core tests pass on Windows and Ubuntu; data files written on Windows are byte-identical before and after.
3. **Headless parity harness.** Avalonia test project, target catalog and parity map, legibility rules R1 to R3 and R5, R7, R8, R9, keyboard helpers. Acceptance: the parity-map tests run and list every unmapped WinForms view; a deliberately low-contrast token fails R1.
4. **Style system and theme (unit 0).** Tokens and the five families from #161's Appendix C; Light, Dark, contrast themes. Acceptance: R1 to R3 pass in Light and Dark; theme changes without a restart.
5. **Shell and navigation (unit 1).** Window, rail, page host, notice strip, history, shortcuts, the library-session object. Acceptance: the 13 window-wide shortcuts work and the guide's table is tested against the key bindings.
6. **Start-up, errors and recovery (unit 17).** Velopack first, single instance before storage, storage failure window, recovery drafts. Acceptance: the start-up order of `Program.vb:31-87` is kept and tested; recovery fixtures pass.
7. **One Stage panel (unit 15).** Input, consent, running with Stop, review with Use boxes, apply; the six assistant windows. Acceptance: the 14 Stage surfaces of target map 5c use it.
8. **Board (unit 2).** Virtualised card grid, filters, Needs Attention, card menus, Move to File Drawer panel with Undo. Acceptance: arrow keys between cards (item 62); Board tests on the large-library fixture within today's responsiveness budget.
9. **Manuscript page frame, Overview, Authors (unit 3).** `ManuscriptEditorViewModel` around `RecordState`; seven dialogs become inline editors. Acceptance: one Save bar keeps every edit; the save question on leaving works as `Form1.ManuscriptPage.vb:1251-1301` does.
10. **New-manuscript state (unit 4).** Acceptance: Save creates, Discard leaves nothing on disk.
11. **Versions (unit 5).** Acceptance: version edit mode keeps every field of `ManuscriptVersionEditForm`.
12. **Submissions (unit 6).** Inline editors, "Record and Save", file drop on Windows and Linux. Acceptance: the stage-change Save flow stops before writing and resumes, per target map 5b rows 85 and 86.
13. **Readiness and Packets (unit 7).** Two windows merge into the working copy; integrity checks with Stop; packet export. Acceptance: no Save & Close or "Save & Go To" left; packet integrity tests pass.
14. **Route section (unit 8).** Acceptance: route kinds told apart without colour (R3); keyboard order of waypoints as the guide describes.
15. **Library (unit 9).** Acceptance: a DataGrid for the Manuscripts table; author and affiliation detail panels.
16. **Journals (unit 10).** Acceptance: one detail panel, no modal chain; tooltip text of metric definitions mirrored into accessible descriptions (item 62).
17. **Deadlines (unit 11).** Acceptance: the keyboard map of `Controls/DeadlineList.vb:64-75` with a single roving tab stop (item 62); the startup reminder as a system notification on Windows and Linux and in the notice strip.
18. **Insights (unit 12).** Route map chart as a drawn control; a native report preview replacing `WebBrowser`. Acceptance: R5 passes with the report preview off the allow-list or with a stated reason.
19. **Import and Export (unit 13).** Import review panel with in-cell editing, export panel, backup, restore with one confirmation window. Acceptance: backup and restore round trip byte-identical on Windows and Linux.
20. **Settings, Help and Updates (units 14, 16, 18).** Acceptance: help control at 24 logical pixels (item 62); access keys and one default-button rule in the most-used dialogs that remain (item 56); update offer in the notice strip.
21. **Parity sign-off and the guide.** Acceptance: the Avalonia build's view catalog matches the target map of fewer, calmer surfaces, whole; the guide's walkthroughs are rewritten for the new windows, with "What Linux shows" and the per-OS folders; Narrator and Orca passes recorded.
22. **Release on two platforms.** The matrix of section 5, channel names, verifier and candidate script per platform. Acceptance: a Preview tag produces one draft with Windows and Linux assets, one `SHA256SUMS.txt`, and the verifier passes; a Linux install updates from the previous Linux Preview.
23. **Linux certification set.** Acceptance: the five gates written for Linux and run once on the chosen guest.
24. **macOS (v1.1.x).** Signing, `osx-arm64` job, macOS gates on a Mac, menu bar and Command-key shortcuts.

---

## 9. What becomes ordinary on Avalonia

These need no issue of their own; they come with the units above.

- Theme change without a restart, and so no window left half-switched when the system theme changes (#365's symptom), since styles are resolved live.
- Contrast themes read from the platform instead of `SystemColors` (`UiTheme.vb:43-69`).
- DPI scaling and dialog fitting: `DialogScaling`, `ResponsiveDialogSizingService` and most of `UiPolish` disappear.
- Page cross-fade by the compositor (a transitioning content control) in place of `PageFade`'s screen capture and 28 P/Invoke lines.
- Dark check boxes, themed borders, combo ellipsis, last-column fill: about 2,866 lines of workarounds (reading 3).
- Virtualised lists in place of the Board's card cache and idle fill (`Form1.vb:100-107`, `Form1.BoardFill.vb`).
- Window chrome without `dwmapi` calls (`WindowChrome.vb:236-256`).
- Following the system text size: the issue lists it; how Avalonia exposes it was not checked, so the style-system issue should confirm it.

---

## 10. The order of the first steps

### The Models and Services split, step by step (reading 2)

Each step is one pull request with the full suite green; merged only after v1.0.1 ships, as settled.

1. **Pin the version behaviour (test only).** Test that `PacketExportService.AppVersion()` and `OnlineAccess.UserAgent()` report the app's version. Today's tests compare `AppVersion()` with itself (`BackupRestoreTests.vb:1043`). After the move both read the core assembly (`PacketExportService.vb:1100-1107`, `OnlineAccess.vb:449-450`), and the version goes into every backup manifest (`PortableBackupService.vb:305`).
2. **Move the tag colours out of `WorkTypeService`** (`:154-183`) to the app side; callers `Controls/TagChips.vb:28,32,33,328` and 4 test lines.
3. **Take the version and guide address off `UpdateService`.** `ErrorReportService.vb:140` and `UserGuideService.vb:35-36` receive the version text and installed flag from the app.
4. **Rework `IsolatedSessionStorageTests`** (`:197`, `:359-380`), which loads the app's assembly into an isolated load context and creates `Form1` from it.
5. **Add the empty `PaperRoute.Core.vbproj`** with `RootNamespace` `ManuscriptPipeline` (or every type changes its full name), the three version properties written explicitly (`release.yml:89-93` requires explicit nodes), `InternalsVisibleTo` for `PaperRouteTracker`, `PaperRoute.Tests`, `PaperRoute.V04Demo` and `PaperRoute.DisplayEvidence`; add it to `ManuscriptPipeline.slnx`; teach `release.yml` and `Build-ReleaseCandidate.ps1` to check the core's versions.
6. **`git mv` the 76 Models files.**
7. **`git mv` the 91 Services files**; move the Anthropic and ClosedXML package references to the core; add `System.Security.Cryptography.ProtectedData` (needed on plain `net10.0`, reading 2). One installed-build check: a new backup's manifest and the About box say 1.0.x.
8. **Certification tooling hashes and version-checks the core DLL** (`Capture-HarnessWindow.ps1:255-258`, `FixtureCollector/Program.cs:244`, `Build-ReleaseCandidate.ps1:441-458`).

Then stage 2 (build issue 2): the JSON newline factory, fixed newlines in stored text, the path comparer, the file-identity guard, the restore path fix, the five interfaces, the core test project on Linux, and the inventory as a gate. The newline and path changes write the same bytes on Windows and can ship in a 1.0.x patch if the maintainer wants them certified early.

### The December trial port

One week, on a throwaway branch, not merged: the Board with its cards on the example library, in a C# Avalonia 12 project over `PaperRoute.Core`. It must answer, in this order:

1. **How long the Board takes.** Checklist: shelves and cards, search, stage and sort filters, Needs Attention chips, card menu, Move to File Drawer panel, arrow keys between cards, Dark and Light. The share finished gives *f* for section 7.
2. **Does the C# project build over the VB core** with compiled bindings, and does a VB project with one compiled `.axaml` file build (one hour at most)?
3. **Which test framework.** One legibility rule (R1 and R5) and one keyboard test (card Tab order) on headless Avalonia: first with a hand-written MSTest setup, then with xUnit v3 if that fails.
4. **The working-copy spike** (half a day to a day, no styling): a `ManuscriptEditorViewModel` over `RecordState` with one bound field, the baseline snapshot and the Unsaved changes state. This is what the Board cannot measure.
5. **Does it start on Linux:** the trial built for `linux-x64` and packed as an AppImage, started on Ubuntu 24.04 in the VirtualBox guest without installing `libfuse2`, with the file dialog and `Launcher` tried once.
6. **Large library.** The Board on the large-library fixture, scrolled and filtered, against today's responsiveness budget.

---

## 11. Risks

| Risk | What would show it early |
|---|---|
| The estimate is too low: the interface is 1.7 times the issue's figure, and most of `EditManuscriptForm.vb` (4,914 lines) and `Form1.vb` (5,120) is orchestration woven into control code, not measured (reading 3). | The trial's *f* and the working-copy spike. |
| The version silently changes in user data after the split: manifests and the User-Agent read the core's version (reading 2). | Split step 1's test; the installed-build check in step 7. |
| Late binding under `Option Strict Off` breaks on the retargeted core (not inventoried). | Compiling a copy of the core on `net10.0` before step 6. |
| Cross-OS byte identity fails on more than paths: other stored text with the platform newline (only samples of 143 newline rows were read, reading 1), or ICU against Windows text comparison (`WorkTypeService.vb:88-90`, not checked). | The scripted session of section 3, run on Linux right after stage 2. |
| Restoring a Windows backup on Linux mangles managed paths (`PortableRestoreService.vb:1984-1996`, found by reading). | A stage 2 test restoring a Windows-made backup on Ubuntu. |
| No MSTest adapter for Avalonia headless; a custom harness may be flaky. | Trial question 3. |
| VB tooling for Avalonia does not work, if the maintainer chooses VB. | Trial question 2. |
| AppImage needs FUSE that a stock system or the CI runner lacks; file dialogs need a D-Bus portal a minimal guest lacks; Avalonia renders badly in a VirtualBox guest (none checked). | Trial question 5. |
| Screen-reader parity falls short: Avalonia's accessibility fixes were still landing in 12.1.4 to 12.1.6. | An Orca pass over the trial's Board. |
| Owner-drawn lists lose keyboard maps or accessibility trees quietly (`DeadlineList.vb:64-75, 988-1060`; `JournalShortlistList.vb:50-60`). | Keyboard tests written before the page in issues 16 and 17. |
| Pixel-based legibility rules differ between headless Skia and the screen. | Trial question 3: compare one R5 result with a screenshot. |
| ClosedXML column widths on Linux without default fonts (not checked). | A core test exporting a spreadsheet on `ubuntu-latest` in stage 2. |
| File-system behaviour per OS: `File.Replace` (`StorageFile.vb:221` and others), locking (E2.12), case-insensitive macOS volumes (not checked). | Core tests on Ubuntu in stage 2; macOS later. |
| macOS has no hardware, certificate or fee by January. | The maintainer's answer to question 6. |
| `vpk upload github` cannot upload another OS's release folder (not checked). | A dry run to a draft release in a fork or test repository during issue 22. |

---

## 12. Questions for the maintainer

1. Is this plan signed off as it stands, or what should change? (#160's acceptance asks for sign-off before any window is rewritten.)
2. The interface language: C# over the VB library (recommended), to be confirmed at the December checkpoint with the trial's answer?
3. Build the Avalonia project on `master` beside the WinForms app (recommended), or on a long-lived branch as the issue first proposed?
4. Should 1.0.x WinForms Preview installs be offered the Avalonia v1.1 Preview through the shared Windows `preview` feed (the default), or should the Avalonia build get its own Windows Preview channel?
5. Is a Linux guest in VirtualBox on the present PC acceptable as the Linux certification machine, and which distributions: Ubuntu 24.04 LTS only (recommended), or Fedora 43 as well?
6. macOS for v1.1.x: will there be an Apple-silicon Mac by January, will the project pay the 99 USD a year Apple fee (or apply for a waiver), and should Intel Macs (`osx-x64`) be built at all?
7. Exports (BibTeX, RIS, Markdown, HTML): keep the platform's line ending (recommended; they are the user's files) or a fixed one?
8. Keys protected on one OS cannot be read after the library is copied to another: accept and document (recommended)?
9. The December trial's scope: the Board plus the working-copy spike and the Linux start check (recommended), or the Board plus the Versions section as reading 3 suggests?

---

## Appendix A: the Windows-only API inventory

**What.** `.github/scripts/Get-WindowsOnlyApiInventory.ps1` (PowerShell 7) reads every `.vb` file in `ManuscriptPipeline/Models` and `ManuscriptPipeline/Services` and reports one row per Windows-only or doubtful API use, with the suggested replacement, then summaries by category, pattern and file, and the packages. It only reads files: no build, no `dotnet`. Two runs give identical output. Its limits are in the script's header (it reads one line at a time; types reached only through inference are missed).

**Why `.github/scripts`.** That folder holds the repository's checks over the source and the release assets that need no installed app (`Verify-ReleaseAssets.ps1`, `Set-RunnerDisplay.ps1`), and the stage 2 build issue is expected to run this script in CI as a gate. `ManualCertification` holds scripts that build candidates, drive virtual machines or capture installed windows, which this script does not.

**Output.** [Windows-Only-API-Inventory.md](Windows-Only-API-Inventory.md), beside this plan, generated at `d67c312`. Regenerate it from the repository root with:

```powershell
pwsh -NoProfile -File .github/scripts/Get-WindowsOnlyApiInventory.ps1 -Root . -AsMarkdown | Out-File -LiteralPath docs/design/Windows-Only-API-Inventory.md -Encoding utf8NoBOM
```

Add `-AsCsv <path>` to write every row, including the portable notes, to a CSV file as well.

**Reading it.** `windows-only` rows must be replaced before a file can run on Linux. `needs-reading` rows are heuristic hits a person reads; reading 1's verdicts are in section 1 of this plan and in the B, C and D lists there. `portable-note` rows do not block. At `d67c312`: 665 rows (430, 225, 10). The target after stage 2: zero `windows-only` rows in the files that moved to the core.
