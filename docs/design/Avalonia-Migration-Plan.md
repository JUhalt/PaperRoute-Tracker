# Avalonia Migration Plan (issue #160)

**Status: signed off by the maintainer on November 21, 2026, as it stands** (recorded on [#160](https://github.com/JUhalt/PaperRoute-Tracker/issues/160)). It was written on November 14 as a proposal and its wording is unchanged: its recommendations are now the plan, except where a later decision of the maintainer's on #160 says otherwise, and the questions it puts to the maintainer stay open until each is answered. What the maintainer had settled before it was written is quoted from the two comments of November 14, 2026 ([decisions](https://github.com/JUhalt/PaperRoute-Tracker/issues/160#issuecomment-6880580068), [change](https://github.com/JUhalt/PaperRoute-Tracker/issues/160#issuecomment-6881023901)) and marked as settled; everything else is a recommendation.

It describes the release candidate v1.0.1-rc.1 at commit `d67c312`. Every `path:line` is that commit's, relative to the repository root; paths that start with `Services/`, `Models/`, `Forms/`, `Controls/` or `Form1` are under `ManuscriptPipeline/`. It was written from checks of the code made on November 14, 2026 (Windows-only APIs, layering, the interface, tests and parity, packaging), from the surface inventory and target map of [#161](https://github.com/JUhalt/PaperRoute-Tracker/issues/161) (`Surface-Inventory-and-Target-Map.md` in this folder, in draft pull request #370, itself a proposal), and from the checked-in inventory script. The working notes of those checks are not in the repository, so each fact names the file, line or folder it was counted from, and the one large table they produced, the classification of the 176 Models and Services files, is Appendix B. Nothing was built or run. Outside facts carry their address and the date they were read.

---

## Summary

**What is proposed.** PaperRoute's interface is rewritten on Avalonia, a cross-platform interface toolkit, for Windows and Linux in v1.1 and for macOS in v1.1.x. The data model and services move, unchanged or nearly so, into their own library: 167 files and 47,021 lines in the first step, and 47,581 lines once the portable parts of `FileOpenService` and `UpdateService` follow in the second. The interface (73,080 lines today) is rewritten, not converted, in the shape of #161's target map: one window, pages and panels instead of today's 62 separate windows, one save boundary per manuscript.

| Point of #160 | Recommendation |
|---|---|
| What stays | 167 of the 176 Models and Services files (all 76 Models files, 91 of the 100 Services files) move to a new library, `PaperRoute.Core`. By the classification in Appendix B, 144 files move unchanged, 20 need a small change, 5 need an interface first, and 7 are interface code that stays with the interface; the first move also leaves `FileOpenService` and `UpdateService` behind until their interfaces exist (section 1). A checked-in script lists every Windows-only API that is left (Appendix A). |
| What is rewritten | Every form, control, `Form1` partial, the six interface services and the start-up; the seventh interface file, `WindowsNotificationService`, is replaced by a platform notifier. The new interface project is **C#**, over the VB library by an ordinary project reference. The interface reaches the services through one application-services object built from `Form1`'s existing test seams, and through small interfaces for dialogs, file opening, single instance, relaunching, secrets, notifications and updates. |
| How parity is proved | The view catalog in `PaperRoute.Tests/ViewCatalog.vb` becomes a parity map: each of today's 85 views and stages names its place in the target map, and a test fails if one has none. Legibility rules, keyboard routes and the guide's shortcut table are re-tested on Avalonia's headless test platform, with one embedded font. With a fixed clock and fixed ids, a scripted session on Linux (and later macOS) must leave the data files byte-identical to Windows once the managed-folder path is normalised. |
| How certification carries over | The installed-app gates run once per platform, and the Windows gates run again for the Avalonia build. A Linux guest in VirtualBox on the present PC stands in for the Windows VM; macOS needs a real Mac. The WinForms 1.0.x line keeps its certification through the existing carry-over rules until the Avalonia build replaces it. |
| Packaging | Parallel build jobs (Windows, Linux, later macOS), one upload step, one verifier run, one `SHA256SUMS.txt`, one draft release. The app keeps its assembly name, package id and main program name. Windows keeps the `stable` and `preview` channels; Linux and macOS get channels named by platform. macOS must be signed and notarised. |
| Sequencing and size | Settled: v1.1 before v1.2; groundwork first; Windows and Linux first. Recommended: 1.0.x on WinForms stays the Stable line; build the Avalonia project on `master` beside the WinForms app, never in the same release. |

**What it costs.** About **51 to 92 weeks** of one maintainer's work for the v1.1 Preview on Windows and Linux, with a midpoint near 72 weeks. That is the sum of 19 interface build units sized from the source (29.5 to 55 weeks) and eleven pieces of groundwork, testing, packaging, guide, certification and release work (21.5 to 37 weeks). macOS adds 4 to 7 weeks in v1.1.x. These are judgements from source size, not measurements. The December trial port measures one unit, the Board, and the interface units are then rescaled by the rule in section 7.

**What is asked of the maintainer.** Sign off this plan on #160, or say what to change. Then answer the questions in section 12. The three that block work before the trial are: C# for the interface (question 2, the December checkpoint); `master` or a long-lived branch (question 3); and whether a Linux guest in VirtualBox is acceptable as the Linux certification machine (question 5). Question 4 (the Preview feed) is needed before the first Avalonia Preview, and question 6 (a Mac) by about January.

---

## 1. What stays

### Facts

| Measure | Value | Source |
|---|---|---|
| Models | 76 files, 2,517 lines; no reference to any Services or interface type; import only `System` and `System.Collections.Generic` | count and `Imports` scan over `ManuscriptPipeline/Models` |
| Services | 100 files, 48,018 lines (the issue's 91 files and about 31,700 lines are older counts) | count over `ManuscriptPipeline/Services` |
| Files that stay with the interface | 9 files, 3,514 lines: the 7 D files (`UiTheme` 627, `UiPolish` 1,126, `UiMotion` 311, `WindowChrome` 262, `DialogScaling` 323, `ResponsiveDialogSizingService` 153, `WindowsNotificationService` 152) and, until stage 2, `FileOpenService` 124 and `UpdateService` 436. So 167 files and 47,021 lines move in stage 1. | line counts of those files |
| Rows in the generated inventory | 665: 430 windows-only, 225 needs-reading, 10 portable-note | `Windows-Only-API-Inventory.md` |
| Files with a windows-only or needs-reading row | 49, all in Services; 127 files have none; no Models file has a row | same |
| Classification of the 176 files | A (moves unchanged) 144, B (small change) 20, C (needs an interface first) 5, D (interface code, stays) 7 | Appendix B |
| Rows in the 7 D files | 369 of the 430 windows-only rows | inventory CSV (`-AsCsv`) |
| System.Drawing rows | 226 in 6 files; 140 in `ManuscriptPipeline/Services/UiTheme.vb` | inventory CSV, System.Drawing category |
| Why type names, not only `Imports`, are counted | the project imports `System.Drawing` and `System.Windows.Forms` for every file (`ManuscriptPipeline/ManuscriptPipeline.vbproj:26-28`) | project file |
| Upward references from Models and Services into the interface | 2 files, 17 lines (the inventory's rows): `Services/UiPolish.vb:8` and 12 control names (`:223, 282, 308, 334, 364, 433, 455, 471, 498, 514, 539, 553`); `Services/UpdateService.vb:7,73,219,375` (`Form1`, `UpdateProgressForm`, `UpdatePromptForm`), plus a use of a `Form1` member at `:76` (`window.IsLibraryWorkInProgress`) | inventory, App UI rows |
| Movable files that call a staying interface service | 5 lines in 3 files: `Services/WorkTypeService.vb:157,172`, `Services/ErrorReportService.vb:140`, `Services/UserGuideService.vb:35-36` | the lines cited |
| Shell launches (`UseShellExecute = True`) | 2 in Services (`FileOpenService.vb:116`, `UrlSafetyService.vb:92`), 3 in the project (also `Forms/DiagnosticsForm.vb:435`). The issue's "10 launch sites" does not reproduce; it may count callers (`UrlSafetyService.OpenInBrowser` is called on 22 lines in 16 interface files). | scan of `ManuscriptPipeline` |
| Calls that fail off Windows | named `EventWaitHandle` (`SingleInstanceService.vb:55,64`); DPAPI, Windows' per-user data protection (`ProtectedData`, `ProtectedKeyStore.vb:88,100`); `kernel32 GetFileInformationByHandle` (`PacketExportService.vb:1428`); `user32 AllowSetForegroundWindow` (`SingleInstanceService.vb:20`) | the lines cited |
| Not found | `Microsoft.Win32` (Registry, SystemEvents), VB `My.*`, `InputBox`, `MsgBox`, `System.Management`, `EventLog`, hard-coded drive letters, `%APPDATA%` | inventory |
| Packages | Anthropic 13.3.0 (netstandard2.0, net8.0, net9.0), ClosedXML 0.105.1 (netstandard2.0, netstandard2.1) and Velopack 1.2.161 (net472, netstandard2.0, net8.0 to net10.0) ship no Windows-only target (`ManuscriptPipeline.vbproj:32-34`; https://www.nuget.org/packages/Anthropic/13.3.0, https://www.nuget.org/packages/ClosedXML/0.105.1, https://www.nuget.org/packages/Velopack/1.2.161, read November 14, 2026). Velopack's readme says it builds packages for Windows, macOS and Linux. Whether each runs correctly on Linux and macOS was not tested. The issue's "Velopack 1.2.0" is out of date. | project file; NuGet pages |
| Line endings | None of the 15 indented JSON writers (11 files, for example `ManuscriptRepository.vb:121-125`) sets `JsonSerializerOptions.NewLine`, so data written on Linux would have LF line breaks where Windows writes CR LF. Stored text also takes the platform newline: `TitlePageParserService.vb:409,1072`, `FlexibleExcelImporter.vb:1095`. | scan: 16 `WriteIndented = True` or `NewLine =` lines in 12 Services files, the extra one being `PacketExportPackage.vb`'s fixed `vbLf` |
| Managed-file paths | Stored as absolute paths: the managed root under Documents plus ids and the file name (`ManagedLibraryService.vb:206-218`, written at `:1571-1596`). Restore rewrites them onto the local root, taking the file name with `Path.GetFileName` of the stored path (`PortableRestoreService.vb:1984-1996`, `:2009-2017`). | the lines cited |
| Managed-copy names | A managed copy keeps the source file's own name and adds a random 8-character suffix (`ManagedLibraryService.vb:986-1007`). Packet export adds the Windows-invalid characters to the platform's set by hand (`PacketExportService.vb:1658-1663`); the interface does not, for example `Form1.Reports.vb:49`. | the lines cited |
| Clock and ids | 67 lines in 44 Models and Services files call `DateTime.Now`, `UtcNow` or `Today`; 48 lines in 32 files call `Guid.NewGuid`; there is no `TimeProvider`, `IClock` or `Func(Of DateTime)` seam | scan of `ManuscriptPipeline/Models` and `ManuscriptPipeline/Services` |

The two path rows matter for Linux, in both directions. On Linux and macOS `Path.GetFileName` treats only `/` as a separator, so restoring a backup made on Windows would read `C:\Users\...\file_ab12cd34.docx` as one file name. The other way round, a Linux file name may contain `:`, `?`, `*`, `\`, `<`, `>` or `|`, which a managed copy keeps verbatim, so a Linux library or backup holding such a file cannot be restored on Windows; on Linux `Path.GetInvalidFileNameChars` returns only NUL and `/`. Both were found by reading, not run. They belong to stage 2 of the split (section 10).

**Per-OS folders.** The data root is `LocalApplicationData` plus the data folder name, and the managed library is `MyDocuments` plus `PaperRoute Library` (`StorageMigrationService.vb:87-132`). Since .NET 8, `MyDocuments` is `XDG_DOCUMENTS_DIR` or `$HOME/Documents` on Linux and `$HOME/Documents` on macOS, and `LocalApplicationData` on macOS is `Library/Application Support`; on Linux `LocalApplicationData` was not changed by .NET 8 and is `$HOME/.local/share` (Microsoft, ".NET 8 breaking change: GetFolderPath behavior on Unix", https://learn.microsoft.com/en-us/dotnet/core/compatibility/core-libraries/8.0/getfolderpath-unix, read November 14, 2026; whether `XDG_DATA_HOME` is honoured was not checked). So the code needs no change for folders; the guide needs one table of them.

### Recommendation

Move all 76 Models files and the 91 Services files that use no interface toolkit into a new VB library, `PaperRoute.Core`, targeting plain `net10.0`, in two stages. Stage 1 moves them and changes nothing for the user. Stage 2, before any Linux build, makes the B changes, adds the five C interfaces with their Windows and Linux implementations, adds a clock and an id seam, makes managed-copy and export file names Windows-safe on every OS, and fixes the restore path defect. Keep the inventory script in the repository and make "zero windows-only rows in the core" the acceptance of stage 2.

What replaces each category (the per-file list is Appendix B; the per-row list is in Appendix A's output):

| Category | Replacement |
|---|---|
| WinForms and System.Drawing in the 7 D files (`UiTheme`, `UiPolish`, `UiMotion`, `WindowChrome`, `DialogScaling`, `ResponsiveDialogSizingService`, `WindowsNotificationService`) | Stay with the WinForms app; rewritten as Avalonia styles and services in the new interface (the notifier as a platform service, below). |
| Colours in the core | Only `WorkTypeService.Background/Foreground` (`:154-183`); they move to the interface side. Colours, where the core needs them later, are plain ARGB values. |
| Shell launches | One `ILauncher` (open file, open link, show in folder) that the interface supplies; Avalonia's `Launcher` service does this on all three systems (https://docs.avaloniaui.net/docs/services/launcher, read November 14, 2026). The program-file warning (`FileOpenService.vb:32-47`) stays in the core and gains Linux and macOS extensions (`.sh`, `.desktop`, `.AppImage`, `.app`, `.command`, the executable bit). |
| Single instance | `ISingleInstance`: Windows as today; Linux and macOS a lock plus a local socket in the data root for "come forward". `KeyFor` upper-cases the data-root path (`SingleInstanceService.vb:37`), which is wrong on case-sensitive file systems, and the restart hand-off (`Program.vb:94-98`) is redesigned with it. |
| Relaunching | `IRelauncher`: today the app restarts with `Application.Restart()` after a restore, a recovery or a change of appearance (`Form1.vb:4572-4576`), and the example library starts a second process from `Environment.ProcessPath` (`ExampleLibraryService.vb:41`). Inside an AppImage that path lies inside the mounted image; the AppImage file itself is in the `APPIMAGE` variable (https://docs.appimage.org/packaging-guide/environment-variables.html, read November 14, 2026). The Linux implementation starts `$APPIMAGE`, macOS opens the bundle with `open -n`, and Windows keeps `Environment.ProcessPath`. |
| Secrets | `ISecretProtector`: DPAPI on Windows, the Secret Service on Linux (through a D-Bus or libsecret binding, with a stated fallback when no keyring runs), the Keychain on macOS. `OnlineAccess.KeyStoreFactory` (`OnlineAccess.vb:158`) is the existing seam. Keys protected on one OS cannot be read on another, as today across Windows accounts (`ProtectedKeyStore.vb:42-43`). |
| Notifications | `INotifier`: today's reminder is a WinForms `NotifyIcon` balloon (`WindowsNotificationService.vb:11,36,56`). Avalonia has no system-notification API, and its tray icon needs StatusNotifierItem or AppIndicator on Linux, which GNOME offers only with an extension (https://docs.avaloniaui.net/docs/reference/controls/tray-icon, read November 14, 2026). Windows keeps a system notification; Linux shows the reminder in the notice strip only in v1.1, and a D-Bus `org.freedesktop.Notifications` client is a later choice. |
| Updates | `IUpdateUi` for the prompts; the Velopack manager, channels and `UpdateSteps` (`UpdateService.vb:358-388`) stay portable. The version text is passed in from the app, not read in the core (split step 3). |
| File identity | Guard `GetFileInformationByHandle` with `OperatingSystem.IsWindows()`; the code already falls back to comparing paths (`PacketExportService.vb:1399-1401`). |
| Path comparison | One OS-aware comparer, used at `ManagedLibraryService.vb:112,147,281`, `StorageMigrationService.vb:1145`, `StorageEnvironment.vb:52`, `PacketExportService.vb:1483`. |
| File names | One Windows-safe name rule (the set of `PacketExportService.vb:1658-1663`) for managed copies and every export name, on every OS. |
| Clock and ids | A `TimeProvider` and an id factory passed through the services in place of the direct calls counted above; the system clock and `Guid.NewGuid` stay the defaults, so nothing changes for the user. |
| Line endings | One shared JSON options factory with `NewLine = vbCrLf` (15 sites in 11 files), and a fixed newline in stored text. On Windows this writes the same bytes as today. |

### Reasons

- 144 of 176 files have nothing to replace, and the 5 lines that tie movable files to interface services are known.
- A plain `net10.0` target makes a new use of WinForms in the core a compile error. The fallback, `net10.0-windows` for stage 1, keeps WinForms reachable and invites new upward use.
- The line-ending, path, name and seam changes do not change any Windows byte, so they can land on the WinForms line and be certified there first.

### Alternatives and why not

- **Move `FileOpenService` and `UpdateService` into the core behind interfaces in stage 1**, with the 7 D files left behind. Not in stage 1: it adds interface work to a move that should change nothing. This plan moves the 9 files in two steps: the 7 D files stay with the interface for good, and the two services stay behind in stage 1 and move, behind interfaces, in stage 2.
- **Port Models and Services to C#.** A second migration of about 47,600 lines (the core after stage 2) and their 160-file VB test suite, for nothing the interface needs.

### What would change it

A compile of the retargeted core showing late binding under `Option Strict Off` that fails on `net10.0` (not inventoried), or CA1416 warnings (the analyser's warning for a Windows-only API called from cross-platform code) treated as errors.

---

## 2. What is rewritten

A "view model" here means a plain class that holds what a page shows and the commands it offers, and tells the page when a value changes; the page binds to it instead of setting control text by hand. Whether to use a helper library for this is left to the first build issue (not checked here).

### Facts

| Item | Today (`d67c312`) | Issue says | Source |
|---|---|---|---|
| Window classes | 63: 62 in `Forms\` plus `Form1` (`Forms\OpenAlexKeyForm.vb:9` inherits `ApiKeyForm`) | 59 | scan of `Inherits` lines |
| `Forms\` | 69 code files, 47,247 lines (and 2 resource files, 240 lines) | 66 files, about 33,000 | count over `ManuscriptPipeline/Forms` |
| `Controls\` | 33 files, 10,189 lines, 28 control classes | 19 controls, about 3,300 | count over `ManuscriptPipeline/Controls` |
| Main window | 18 files, 12,533 lines (`Form1.vb` 5,120; `Form1.Designer.vb` 31; 16 partials 7,382) | 15 partials, about 6,600 | count over `ManuscriptPipeline/Form1*.vb` |
| `UiPolish` | 1,126 lines | 582 | line count |
| All interface code to rewrite | **73,080 lines**: 70,278 (Forms 47,247, Controls 10,189, `Form1` 12,533, `Program.vb` 174, `ApplicationEvents.vb` 91, `My Project` 44) plus 2,802 in the six interface services (`UiTheme`, `UiPolish`, `UiMotion`, `WindowChrome`, `DialogScaling`, `ResponsiveDialogSizingService`). The seventh D file, `WindowsNotificationService` (152 lines), is not counted: it is replaced by the platform notifier of section 1, which is costed in the platform-services line of section 7. | about 43,500 | line counts |
| Workaround code that is deleted, not ported | about 2,866 lines of `Controls\` (`PageFade` 1,225, `CheckGlyph`, `ThemedBorder`, `ThemedDatePicker`, `InPlace` and others); 55 `DllImport` lines in Forms, Controls and the root files (65 with the six interface services), 28 of them in `PageFade.vb` | | scan for `DllImport` |
| View models | none: `INotifyPropertyChanged`, `ICommand`, `ObservableCollection`, `BindingSource` appear 0 times; the closest is `EditManuscriptForm.RecordState` (`Forms\EditManuscriptForm.vb:20-64`) | | scan of the interface |
| Test seams on `Form1` | 12 `Protected Overridable` methods (`Form1.vb:1459, 1557, 1590, 1948, 4203, 4239, 4539, 4548, 4572`; `Form1.ManuscriptPage.vb:82`; `Form1.Shell.vb:1637, 1646`); 11 `Friend` prompt delegates (`Form1.Citations.vb:23,24`; `Form1.Deadlines.vb:32,33`; `Form1.Example.vb:14`; `Form1.Insights.vb:27`; `Form1.ManuscriptPage.vb:72,76`; `Form1.Publications.vb:16`; `Form1.RecoveryDraft.vb:18`; `Form1.Welcome.vb:18`) plus the `publicationSource` field (`Form1.Publications.vb:15`), and 24 more `Friend` `Func` or `Action` fields in `Forms\`; 96 reflection sites into 34 private members | issue: "four subclass `Form1`" (there are 15) | scan of `Form1*.vb`, `Forms\` and `PaperRoute.Tests` |
| VB-only features in the interface | 3 `WithEvents`, 7 `Handles`, 1 `InputBox` (`Form1.vb:4330`); no `My.*` calls, XML literals or `On Error`. The start depends on the VB application framework (`Program.vb:114`, `ApplicationEvents.vb:8-87`). | | scan of the interface |
| Fonts | 65 lines in 64 interface files name `Segoe UI` (for example `Forms/AboutForm.vb:33`), a font Ubuntu does not ship | | scan of Forms, Controls, the root files and Services |
| No built-in Avalonia counterpart | the report preview's `WebBrowser` (`Forms\ReportForm.vb:26`); 6 `DataGridView`s with in-cell editing (for example `TitlePageImportForm`, `ExcelMappingForm`) need the separate DataGrid package and templated columns | | scan of the interface |
| Avalonia | 12.1.6 is the latest stable (November 9, 2026), MIT licence, net8.0 and net10.0 builds (https://www.nuget.org/packages/Avalonia, read November 14, 2026). 12.0 (April 7, 2026) targets .NET 10, reworked the compositor and added a native Linux accessibility backend; its Wayland backend is a private preview, not generally released (https://avaloniaui.net/blog/avalonia-12, read November 14, 2026). | | outside |
| Avalonia and VB | Official templates are C# and F# only (https://github.com/AvaloniaUI/avalonia-dotnet-templates, read November 14, 2026). The only VB name generator is the community `Nukepayload2.SourceGenerators.AvaloniaUI` 1.0.0, tested only with Avalonia 11.0.9 (https://www.nuget.org/packages/Nukepayload2.SourceGenerators.AvaloniaUI, read November 14, 2026). Whether compiled XAML works in a VB project on Avalonia 12 was not checked. | | outside |

### Recommendation: the language of the interface project

A **C# interface project** (`PaperRoute.Avalonia`, name to be settled in the first build issue) over the VB `PaperRoute.Core`, by an ordinary project reference. Whatever the project is called, its assembly keeps the name `PaperRouteTracker` (section 5). The new interface tests are C# too. Models, Services and their tests stay VB. The maintainer left this open for the December checkpoint; the trial port should confirm it.

**Reasons.**
- The interface is a rewrite in any language: no WinForms control code carries over, so keeping VB saves almost no text in the interface.
- The interface uses almost no VB-only features.
- Avalonia's templates, generated `x:Name` fields and compiled bindings are C#-first; for VB there is one untested community generator, and view-model change notification would be written by hand.
- C# compiles with strict typing; the VB project runs with `Option Strict Off` by the SDK default (no `Option Strict` line in the project file or the core).

**Alternatives and why not.**
- **VB code-behind through `AvaloniaXamlLoader`.** It keeps one language for 47,021 library lines, 92,494 test lines and the interface, and keeps the view models textually close to the VB code they replace. But it rests on a build path nobody has shown working on Avalonia 12, with no template and no generators. Worth one hour in the trial; not the default.
- **A full port to C#.** See section 1.

**What would change it.** The trial shows a VB Avalonia 12 project with compiled XAML and compiled bindings building cleanly, and the maintainer weighs one language above the tooling.

### Recommendation: how the interface reaches the services

1. **Services stay as they are.** The five source interfaces already in Services (`IAssistantProvider`, `ICitationSource`, `IJournalFactsSource`, `IJournalSuggestionsSource`, `IPublicationSource`) carry over unchanged.
2. **One application-services object** (a "library session"): load, save on a worker with the failure flag, backup, restore, restart and recovery drafts, lifted from `Form1`'s 12 overridable seams and its save and load steps (`Form1.vb:1590-1641`). The shell view model and the tests both use it. It replaces the 96 reflection sites.
3. **Each prompt delegate becomes a method on a dialog-and-notice service** (`IInteractions`, name open): the 35 `Friend` prompt delegates mark exactly where the interface asks the outside world something, and the target map turns most of them into notices or panels anyway.
4. **`ManuscriptEditorViewModel` is built around today's `RecordState`** (original, working copy, baseline snapshot, `EditManuscriptForm.vb:20-64, 698-700`), so "one save boundary per manuscript" lives in one object.
5. **Access across the assembly line:** the core grants `InternalsVisibleTo` to the app, the tests, `PaperRoute.V04Demo`, `PaperRoute.DisplayEvidence` and later the Avalonia project; no `Friend` member needs to become `Public` in stage 1. Before v1.1 ships, every `Friend` type and member the Avalonia project calls becomes `Public`, or moves behind the application-services object, so the C# project does not depend on a friend grant. That is more than the `Friend` classes the interface uses (`SingleInstanceService`, `StorageFile`, `StorageFileInUseException`, `PacketExportPackage`, the four `Assistant*` types): the interface also calls `Friend` members of `Public` classes, for example `ReminderService.SafeManuscriptTitle` (`ReminderService.vb:465`), `OnlineAccess.KeyStore` (`OnlineAccess.vb:457`), `AssistantService.ProviderFactory` (`AssistantService.vb:149`), `ExampleLibraryService.StartSession` (`ExampleLibraryService.vb:80`, called at `Program.vb:45`), `ManuscriptCloneService.CloneSuggestion` (`ManuscriptCloneService.vb:512`, called at `Forms/AddDecisionForm.vb:367`) and `PortableBackupService.CreateBackup` (`PortableBackupService.vb:85`). A name scan finds about 20 such members (heuristic, not exhaustive). The list is produced from a source scan in the shell build issue (issue 5). A C# project that reads the VB app's internals through `InternalsVisibleTo` is already set up in this repository (`ManualCertification/V04Demo/PaperRoute.V04Demo.csproj:12`, `ManuscriptPipeline/Properties/AssemblyInfo.vb:4`), so this is tidiness before release, not a risk to the language choice.

**Alternatives and why not.** Keeping the reflection seams is impossible: they name WinForms fields. Passing services through every page's constructor, as 10 of 95 form constructors do today, spreads the save boundary across pages.

**What would change it.** The trial's working-copy spike (section 10) showing that `RecordState` does not fit a bound view model.

---

## 3. How parity is proved

"Parity" means the Avalonia build does everything the WinForms build does, in the target map's new shape, no worse for keyboard, screen-reader or contrast users, and with the same data on disk.

### Facts

| Item | Value | Source |
|---|---|---|
| Test suite | 160 files, 1,448 methods, 413 data rows, 1,741 results; MSTest 4.5.2 on `net10.0-windows` (`PaperRoute.Tests/PaperRoute.Tests.vbproj:4-5, 13`) | `PaperRoute.Tests/README.md:3`; scan of `PaperRoute.Tests` |
| Tests that reach a real form | 621 of 1,448 methods (heuristic) | scan of `PaperRoute.Tests` |
| Pure Models and Services files | 77 files, 613 methods, plus about 130 pure methods in mixed files | scan of `PaperRoute.Tests` |
| Drive-letter literals in tests | 42 lines in 12 test files (for example `PaperRoute.Tests/ExternalLinkSafetyTests.vb:22,50`, `ManuscriptVersionTests.vb:75`), so the core tests do not move to Linux unchanged | scan of `PaperRoute.Tests` |
| Form-driving files | 64 call `StaRunner.Run` or `RunOnStaThread` (the issue says 23); 15 classes subclass `Form1` | scan of `PaperRoute.Tests` |
| View catalog | `ViewCatalog.vb`: 63 dialogs (`:53-117`), 12 hosted views (`:123-136`), 10 stages (`:144-155`); 75 views, 85 with stages; kept complete by `EveryViewHasAFactoryOrAReasonItHasNoneYet` (`:1177-1178`) | the lines cited |
| Legibility rules | WCAG contrast (`TestSupport.vb:182-211`); 4.5:1 for text, 3:1 for disabled captions and contrast-theme edges; "light" means relative luminance above 0.6 (`DarkThemeLegibilityTests.vb:1018-1020`); a 5-entry Dark allow-list (`:34-40`). This plan numbers the rules R1 to R11: R1 token pairs at 4.5:1, R2 contrast-theme tokens, R3 route kinds told apart without colour, R5 no light parts in Dark, R7 strongest-pixel 4.5:1, R8 disabled text 3:1, R9 edges 3:1; R4, R6, R10 and R11 are the WinForms-only rules dropped below | the lines cited |
| Keyboard routes in the guide | `docs/USER_GUIDE.md:1041-1074`: 13 window-wide shortcuts and 10 bullets; tied to the code by `UserGuideServiceTests.vb:946-1019` (a scan of `ProcessCmdKey`) and 19 methods in `KeyboardAccessTests.vb` | the lines cited |
| Items 56 and 62 (v0.9.1 plan) | access keys exist in 15 windows (`ManualCertification/Workflow-Consistency-v0.9.2.md:13`); the help control is still 19 by 19 (`Forms/ContextHelpControl.vb:43-45`) | the lines cited |
| Byte-identical check today | a hash list, `Get-ChildItem | Get-FileHash | Export-Csv` over the data, managed and linked folders (`ManualCertification/v1.0-Certification-Checklist.md:428-430`); the collector (`ManualCertification/FixtureCollector/Program.cs`, 293 lines) is a `win-x64` program with Windows folders | the lines cited |
| Avalonia headless testing | Avalonia's headless platform runs windows in memory, with no screen. Real pixels need `.UseSkia()` with `UseHeadlessDrawing = false`, then `CaptureRenderedFrame()`; keyboard input is simulated per window; official adapters for xUnit v3 (`Avalonia.Headless.XUnit` 12.1.6) and NUnit; no MSTest package, but a documented "Manual setup" with `HeadlessUnitTestSession.StartNew` and `Dispatch` for other runners (the page recommends the xUnit or NUnit integration for most cases). The page says nothing about fonts, render scaling or accessibility (https://docs.avaloniaui.net/docs/concepts/headless, https://www.nuget.org/packages/Avalonia.Headless.XUnit, read November 14, 2026) | outside |

### Recommendation

Prove parity with five checks, all in code except the last:

1. **A parity map over the view catalog.** The Avalonia test project gets its own catalog of target surfaces (pages, panels, Stage panels, the modal windows that stay) with a factory for each, and a data table that maps each of today's 85 catalog keys to a target surface from the target map (5a to 5e). Two tests: every target surface has a factory (as `EveryViewHasAFactoryOrAReasonItHasNoneYet` does today), and every WinForms key has a destination or a named reason (removed as a duplicate route, 5e). This is what "the Avalonia build's view catalog matches the target map of fewer, calmer surfaces" means in a test.
2. **Legibility, in two tiers.** R1 to R3 as plain colour tests over the style tokens. R5, R7, R8 and R9 on headless rendering with Skia, with one font family embedded in the app and in the headless tests, so that the pixel rules give the same answer on Windows and Ubuntu runners (today's hard-coded `Segoe UI` does not exist on Ubuntu; this is an inference, not run). The thresholds, the allow-list mechanism and the strongest-pixel rule carry over. R8 becomes "a disabled control's text is at least 3:1 on its surface"; the WinForms-only parts (the "standard Windows button" rule, the caption recolouring test, title-bar chrome) are dropped as not applicable, and the plan says so in the issue that ports them.
3. **Keyboard.** The guide's shortcut table is tested against the Avalonia key bindings instead of `ProcessCmdKey`'s source. The 19 keyboard tests are rewritten against the new pages. Items 56 and 62 are acceptance lines of the pages they touch: access keys and one default-button rule in the most-used dialogs (the existing sweep `NoWindowRepeatsAnAccessKeyItShowsTogether` is the model); tooltip text mirrored into accessible descriptions; a help control of 24 logical pixels; a single roving tab stop on Deadlines rows; arrow keys between Board cards.
4. **Same data on disk.** Stage 2 adds the clock and id seams of section 1, and the managed-copy suffix (`ManagedLibraryService.vb:1000-1007`) comes from the same id factory. A scripted session (open the standing fixture, edit, save, back up, restore) then runs on Windows and on Linux over the same restored library, with fixed clock values, fixed ids and the same time zone set on both machines (whether stored times carry a local offset was not checked). The data files must be byte-identical after one normalisation: the managed root prefix and the path separator in the stored `LocalFilePath` fields. Everything else, including line breaks and JSON layout, must match byte for byte. In v1.1.x the same session runs on macOS, with the same normalisation. This needs the stage 2 line-ending fix first, and a collector build per OS with the folders taken from `Environment.SpecialFolder`. A value the seams turn out not to cover is named in the check and normalised by name, never by a general pattern.
5. **Screen readers by hand.** Narrator on Windows and Orca on Linux, per page, recorded like today's Narrator pass. Avalonia's accessibility page lists UI Automation, NSAccessibility and AT-SPI2 as full support (https://docs.avaloniaui.net/docs/app-development/accessibility, read November 14, 2026), but the Linux backend is new in 12.0, so it is proven per view, not assumed.

**What headless cannot prove.** The headless platform replaces the platform backends, so checks 1 to 3 do not show what reaches UI Automation or AT-SPI (check 5 does, by hand), they render at scale 1 unless a test sets another, and they do not exercise native dialogs. DPI scaling, native file dialogs and the accessibility tree stay in the installed-app gates and the manual passes.

**Test framework.** The core tests stay MSTest and move into a `net10.0` core test project that also runs on `ubuntu-latest`, after the 42 drive-letter literals in 12 test files are made portable (budgeted in stage 2). The new interface tests use **xUnit v3 with `Avalonia.Headless.XUnit`**, the official adapter, unless the December trial shows an MSTest harness on the documented `HeadlessUnitTestSession` API working in under a day.

### Reasons

The catalog already keeps itself complete by reflection; a parity map extends that guarantee across the change of shape. The legibility rules are numbers, not WinForms behaviour. With the clock and ids fixed, normalising only the path prefix is the strictest comparison that can hold: absolute paths differ between a Windows and a Linux home folder by design.

### Alternatives and why not

- **Normalising only the path prefix, with no seams** (this plan's first draft). Cannot pass: each run writes new timestamps and new ids (the 67 clock lines and 48 `Guid.NewGuid` lines of section 1; for example `Services/AssistantService.vb:70`, `Services/CitationsService.vb:47`, `Services/JournalFactsService.vb:16` default a property to the clock), and every managed copy gets a random suffix that lands in the stored `LocalFilePath`, beyond the root prefix. Two sessions would differ even on the same OS.
- **Normalising ids, timestamps and the managed-copy suffix instead of adding seams.** Needs no core change, but a normalisation by pattern can hide a real difference, such as a time written in the wrong zone or an id that changed on restore. The fallback if the seams cost more than their week in stage 2.
- **Byte identity with no normalisation.** Impossible while paths are absolute (`ManagedLibraryService.vb:206-218`).
- **Library-relative paths in the data files.** Would make the files identical without normalising, but it is a schema change with a migration; it could join Schema 10 (#157) if the maintainer wants it, not as part of the toolkit change.
- **MSTest for the interface tests through the documented `HeadlessUnitTestSession` API.** Keeps one framework and today's `TestCategory` filters, and sits on a documented API, but Avalonia recommends its xUnit or NUnit integration for most cases and ships no MSTest adapter. Trial question 3 tries it first.

### What would change it

Headless Skia rendering giving different strongest-pixel results from on-screen rendering (fonts, smoothing), which would push R7 to a tolerance or to on-screen tests.

---

## 4. How certification carries over

### Facts

- The per-install gates today: v1.0's E1.1 to E11.1 (`ManualCertification/v1.0-Certification-Checklist.md:457-775`) and E12 (`:815-825`); v1.0.1's V1 to V16 (`ManualCertification/v1.0.1-Certification-Checklist.md:175-277`) and S1 to S5 (`:301-316`); lines carry over between releases on unchanged files (`:318-331`).
- Windows-specific gates: SmartScreen and Edge on first run, Defender, Start menu and Apps entry, DPI awareness (150% needs the maintainer), OneDrive, Narrator and Windows contrast themes, WinGet (the two checklists above).
- E2.12 (a file held with `FileShare.None`) depends on Windows share-mode locking; Linux and macOS do not lock files by default, so that gate needs a new design there (inference).
- Velopack on Linux makes one `.AppImage` and no installer; updates replace the file in place and ask for `pkexec` in a privileged folder. The page does not say that Velopack creates a menu or launcher entry; it leaves that to AppImage install tools (https://docs.velopack.io/packaging/operating-systems/linux, read November 14, 2026). On macOS it makes an `.app`, a `.pkg` and a portable `.zip` (https://docs.velopack.io/packaging/operating-systems/macos, read November 14, 2026).
- Avalonia 12's Wayland backend is a private preview (https://avaloniaui.net/blog/avalonia-12, read November 14, 2026), so on Ubuntu's default GNOME Wayland session the app runs through XWayland on any machine.
- Avalonia's support tiers (https://docs.avaloniaui.net/docs/supported-platforms, read November 14, 2026): Tier 1 is Ubuntu 25.x, Fedora 43 and Debian 13; Ubuntu 16.04 to 24.x, Fedora 30 to 42 and Debian 9 to 12 are Tier 2, "not part of the test matrix". .NET 10 supports Ubuntu 22.04, 24.04 and 26.04 and Fedora 43 and 44 (https://github.com/dotnet/core/blob/main/release-notes/10.0/supported-os.md, read November 14, 2026).
- macOS may run in a virtual machine only on Apple hardware (an Apple Developer Forums thread, https://developer.apple.com/forums/thread/845455, read November 14, 2026; Apple's licence itself was not read), so the Windows PC cannot host a macOS certification machine.

### Recommendation

- **The WinForms line keeps its certification** until the Avalonia build replaces it: 1.0.x patch releases carry the E and V lines by the existing carry-over rules, and the Windows VM stays as it is.
- **The Avalonia build is certified again on Windows.** It is a new interface, so the carry-over rules do not apply to it: every Windows gate (E1 to E12, V1 to V16, Narrator, contrast themes, DPI at 150%) runs again on the Windows VM for the first Avalonia Preview.
- **Each platform gets its own gate set with the same five gates** the issue names: clean install, update from the previous version on that platform's feed, backup and restore round trip, the recovery fixtures, uninstall keeps the library.
- **Linux stand-in:** a Linux guest in VirtualBox on the present PC, with snapshots and a clean profile like the Windows VM. Recommended distribution for v1.1: Ubuntu 24.04 LTS with GNOME, because it is the newest LTS release on both lists at writing (26.04 is in .NET 10's list but not yet in Avalonia's tiers); it is Avalonia Tier 2. Fedora 43 (Tier 1) is added if time allows. Linux gates: download and mark executable; AppImage start on a stock system (an AppImage needs FUSE, the Linux library that lets it mount itself); first-run hook time; data folders; one run in a GNOME Wayland session at 125% or 150% scaling, where the app runs through XWayland (scaling, input methods, "come forward"); restart after restore and opening the example library from the AppImage; menu entry and desktop integration (Velopack makes none, so the gate records what the user sees, the counterpart of the Start menu gate); update in place (and the `pkexec` path); uninstall by deleting the file.
- **macOS stand-in (v1.1.x):** a real Apple-silicon Mac. A hosted `macos-latest` runner can run the automated gates but cannot show what Gatekeeper (macOS's check on downloaded apps) shows a user who downloaded the file.
- **E2.12 on Linux:** recorded as "not applicable as written"; the counterpart gate is decided with the build issue for start-up and recovery.
- **The guide** gains "What Linux shows" beside "What Windows shows" (`docs/USER_GUIDE.md:32`), including that there is no menu entry until the user adds one, and later "What macOS shows".

### Reasons

The same host and the same method of recording evidence as JSON files, and no new hardware for v1.1. The WinForms line cannot lose its certification by a change it does not ship.

### Alternatives and why not

A physical Linux machine gives real GPU rendering, but adds hardware and does not replace snapshots; it would not give native Wayland either, since the app runs through XWayland on a Wayland desktop. What a guest may miss is XWayland behaviour (fractional scaling, input methods, focus for "come forward") if it falls back to an X11 session, which is why one gate runs in a Wayland session. Hosted runners alone cannot observe first-run prompts.

### What would change it

Avalonia rendering badly inside a VirtualBox guest (its OpenGL path was not checked), or the guest unable to run a GNOME Wayland session, which would push the Linux gate onto a physical machine.

---

## 5. Packaging on three platforms

### Facts

- `.github/workflows/release.yml` is one job on `windows-latest` (`:19`): `win-x64` self-contained publish (`:183-194`), the previous release of the same channel downloaded for delta packages (`:249-270`), `vpk pack ... --channel stable|preview --noPortable true` with `--packId JUhalt.PaperRouteTracker` and `--mainExe PaperRouteTracker.exe` (`:274-288`, `:278`, `:281`), a new draft with `--merge false` (`:290-319`), then `Verify-ReleaseAssets.ps1` Stage, documents and `SHA256SUMS.txt`, Verify, publish (`:321-387`). The line numbers in the issue (`144-155`, `233-246`) are older.
- The certification tools name the program files: `FixtureCollector/Program.cs:244` hashes `current\PaperRouteTracker.dll` and `current\PaperRouteTracker.exe`.
- `UpdateService` pins `ExplicitChannel` to the literal `"stable"` or `"preview"` (`UpdateService.vb:317-320, 330-340`). A Linux or Mac build would read the Windows feed, `releases.stable.json`.
- Velopack's docs: "every unique os/rid has its own channel" (https://docs.velopack.io/packaging/channels, read November 14, 2026). macOS packages can be built only on macOS; Linux packages can be built on any OS (https://docs.velopack.io/packaging/cross-compiling, read November 14, 2026). So the issue's "vpk pack runs on the OS it packages for" is strictly true only for macOS.
- `vpk upload github --merge` adds to an existing release; uploading a feed file that already exists is refused (Velopack source at tag 1.2.161, https://github.com/velopack/velopack/tree/1.2.161, files `GitHubUploadCommand.cs:26-27` and `_GitRelease.cs:164-194`, read November 14, 2026).
- Asset names carry the channel (for example v1.0.0's `JUhalt.PaperRouteTracker-1.0.0-stable-full.nupkg`), so per-platform channels do not collide in one release.
- `Verify-ReleaseAssets.ps1` and `ManualCertification/Build-ReleaseCandidate.ps1` are Windows-specific: channel set (`:19`), `publish/win-x64` (`:23`), the `win-x64` runtime-pack notices, exactly one `*Setup.exe`; the candidate script refuses to run off Windows (`:30`) and reads WinForms' `HighDpiMode` from IL (`:189-303`).
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

- The Avalonia app keeps `AssemblyName` `PaperRouteTracker`, the package id `JUhalt.PaperRouteTracker` and the main program `PaperRouteTracker.exe` on Windows, so existing installs, shortcuts, WinGet's `-stable-Setup.exe` address and the certification tools keep working. What Velopack does to shortcuts if the main program's name changed was not checked; keeping the name avoids the question.
- Each build job downloads the last release of its own channel first (`vpk download github --channel <its channel>`), as release.yml does today per channel, so its delta packages are made against the right base.
- Each build job runs its own tests before packing: Windows today's suite, Linux the core tests and the headless interface tests.
- `UpdateService.ChannelName` becomes a function of OS, architecture and Stable or Preview, with Windows unchanged, and a test ties it to release.yml's `--channel` values.
- The verifier and the candidate script get a per-platform table: channel name, installer pattern, AppImage count, required native files (`libcoreclr.so` and `libhostfxr.so` on Linux in place of `coreclr.dll`, `hostfxr.dll` and `System.Windows.Forms.dll`), and each runtime pack's notices. The `HighDpiMode` IL check retires with the WinForms line.
- macOS signing and notarisation are required for v1.1.x, not optional: Developer ID Application and Installer certificates, a `notarytool` profile, a temporary keychain in CI, and the secrets added to the signing decision of #43 and #216.
- **`osx-x64` is left out** unless the maintainer wants Intel Macs: news reports of WWDC 2025 say macOS 26 is the last release for Intel Macs (for example heise online, https://heise.de/-10438678, found by search November 14, 2026), while .NET 10 still lists macOS 27 on x64 (https://github.com/dotnet/core/blob/main/release-notes/10.0/supported-os.md, read November 14, 2026); testing it needs an Intel Mac.
- Minimums to state: Ubuntu 24.04 or later, Debian 13, Fedora 43 or later, x64 only, for v1.1; macOS 15 for v1.1.x. Ubuntu 24.04 is the release the Linux gates certify. These sit within .NET 10's list (above); Ubuntu 24.04 and macOS 15 are Avalonia Tier 2 (tested less often than Tier 1), Debian 13 and Fedora 43 are Tier 1 (Avalonia's tiers, above). Ubuntu 22.04, which .NET 10 still supports, is not stated, because no gate would run on it.

### Reasons

One draft, one checksum file and one verifier run keep today's guarantees. Keeping Windows' channel names, the package id and the program name keeps every 1.0.x install, the WinGet manifest's `-stable-Setup.exe` address and its tests valid. Building the AppImage on Ubuntu costs nothing extra, because the Linux tests must run there anyway.

### Alternatives and why not

- **The issue's matrix with each job uploading its own channel in a chain.** The fallback if `vpk upload github` cannot upload a release folder built on another OS (not checked).
- **Renaming Windows' channels to `win-x64-stable`.** Velopack's own example, but it strands every installed 1.0.x on the old feed name.
- **Shipping macOS unsigned like Windows.** Every user would have to open System Settings to run it.

### What would change it

A shared Windows `preview` feed means 1.0.x WinForms Preview installs are offered the Avalonia Preview by default. If the maintainer does not want that, the Avalonia build needs its own Windows Preview channel name, and the 1.0.x Preview users a notice.

---

## 6. Sequencing and size

### Facts (settled by the maintainer, November 14, 2026)

1. The order stays: v1.1 is the Avalonia interface; v1.2 is built once, on Avalonia; the order is looked at again at a mid-December checkpoint.
2. v1.0.1 ships first, on WinForms, with no new windows.
3. Groundwork starts alongside: #161's inventory and target map, then the Models and Services library, merged only after v1.0.1 is out, then a one-week trial port of the Board in December, not merged.
4. Windows and Linux first; macOS as v1.1.x.
5. Still open for December: the interface language, what the trial tests besides the Board, the estimate in weeks.
6. #158 (Stable releases offered on the Preview channel) ships with v1.1's first Preview, not in v1.0.1 (the second comment).

### Recommendation

- 1.0.x on WinForms stays the Stable line and takes data-safety fixes only after v1.0.1 (whose wider scope the maintainer approved as an exception). No release mixes the two toolkits.
- **Build the Avalonia project on `master`, beside the WinForms app, both over `PaperRoute.Core`.** `release.yml` packs the WinForms app until the switch. At the switch, a `release/v1.0.x` branch is cut for WinForms Stable patches, and `master` packs the Avalonia app as the v1.1 Preview. Interim Avalonia builds are CI artifacts, never releases.
- #158 ships with v1.1's first Preview, as settled.
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

19 units covering all 73,080 interface lines, each the files it replaces at `d67c312`. Sizes are S (under about 1 week), M (1 to 2), L (2 to 4) and XL (over 4 weeks of one maintainer), judged from source size, drawing technique and how much the target map changes the shape.

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

Assumptions: S = 0.5 to 1 week; M = 1 to 2; M-L = 1.5 to 3; L = 2 to 4; XL = 4 to 6 (the cap of 6 is this plan's assumption). Each unit's size includes its view models and its own headless tests.

| Part | Count | Low (weeks) | High (weeks) |
|---|---:|---:|---:|
| S units | 2 | 1 | 2 |
| M units | 9 | 9 | 18 |
| M-L units | 1 | 1.5 | 3 |
| L units | 5 | 10 | 20 |
| XL units | 2 | 8 | 12 |
| **Interface units** | 19 | **29.5** | **55** |
| Split, stage 1 (section 10, steps 1 to 8) | | 2 | 3 |
| Split, stage 2 (B changes, the five interfaces' Windows parts, clock and id seams, Windows-safe names, both restore directions, porting the core tests to Linux) | | 3 | 5 |
| Linux platform services (secret store with a fallback, single instance with a lock and socket, relauncher for the AppImage, notifier on Windows and the notice strip on Linux) | | 2 | 4 |
| Shared parity harness (headless project, catalog and parity map, legibility rules, keyboard sweep, replacements for the source-scan tests) | | 3 | 5 |
| Font decision (one embedded family in place of the hard-coded `Segoe UI`) | | 0.5 | 1 |
| Packaging (release matrix, channel names, verifier, candidate script, notices per platform) | | 2 | 4 |
| Guide (walkthroughs for the new windows, "What Linux shows", keyboard section) | | 2 | 3 |
| Screen-reader passes per surface (Narrator and Orca) | | 1 | 2 |
| Windows certification of the Avalonia build | | 2 | 3 |
| Linux certification set, first run, fixes | | 2 | 3 |
| Release candidates (1.0 went through four) | | 2 | 4 |
| **Groundwork and release** | 11 | **21.5** | **37** |
| **Total for the v1.1 Preview (Windows and Linux)** | | **51** | **92** |
| macOS in v1.1.x (signing, `osx-arm64` job, Mac gates, VoiceOver pass, menu bar and Command-key shortcuts, not examined) | | 4 | 7 |

So: **about 51 to 92 weeks**, midpoint near 72 (interface units 42.25, groundwork and release 29.25), before calibration. In months of one maintainer, roughly 12 to 21. The issue's "months, not weeks" stands.

### Calibration from the December trial

The trial ports the Board (unit 2, L, 2 to 4 weeks, midpoint 3) in one week. The trial records the days of that week spent on the Board itself, *d*, since the same week also answers questions 2 to 6 of section 10. If those days finish a share *f* of the Board's checklist, the Board's measured size is (*d* / 5) / *f* weeks, and the scale factor is k = ((*d* / 5) / *f*) / 3. Only the interface units are rescaled: the rescaled estimate is k times 42.25 weeks, plus the groundwork and release lines as estimated (29.25 weeks at their midpoint), since the Board's speed says nothing about that work. For example, 4 days on the Board finishing half its checklist gives 1.6 weeks, k = 0.53, about 22.5 plus 29.25, so about 52 weeks. A trial that finishes the Board early gives *f* above 1. The checklist includes the Board's headless tests and one accessibility pass, because each unit's size includes them; without them *f* would come out too high.

The Board does not measure the manuscript page (units 3 to 8: 28,970 lines, about 40% of the interface, and the save-boundary redesign). The working-copy spike in section 10 is the check on that part; if it goes badly, units 3 and 7 move to the top of their bands whatever k says.

### What the estimate leaves out

Answers to #161's open questions 1, 4, 5, 6 and 12 change the save boundaries and so the size of units 2, 3, 6, 7 and 10. Composition-grade motion (#162) and the issues already in the v1.1 milestone (#157, #297, #333, #334, #365, #368) are not in these weeks; #333 (the text engine) may overlap the font decision line. A Linux system notification (a D-Bus client) is not in these weeks; v1.1 uses the notice strip on Linux. Checked and not a gap: printing (no `PrintDocument` or `PrintDialog` in the project); file drag and drop is one list (`Forms/SubmissionDetailsForm.vb:1658,2131`), and the guide has no images, so screenshots are not a cost.

---

## 8. Build issues to open

To open after sign-off; not opened by this plan. Each interface issue's acceptance carries the line "**the Avalonia build's view catalog matches the target map of fewer, calmer surfaces**" for the target-map rows it builds, and "**the guide's walkthroughs for these surfaces are rewritten for the new windows**". Issue 21 carries both lines for the whole app.

1. **Move Models and Services into `PaperRoute.Core` (stage 1).** Steps 1 to 8 of section 10. Acceptance: full suite green after each step; installed build's About box and a new backup's manifest report the app version; the inventory shows no WinForms, System.Drawing or interface-type row in the core.
2. **Make the core run on Linux (stage 2).** The 20 B changes, the five interfaces with their Windows implementations, the clock and id seams, one Windows-safe name rule for managed copies and exports on every OS, the restore path fix, a `net10.0` core test project on `ubuntu-latest` with the drive-letter literals made portable. Acceptance: the inventory script reports zero windows-only rows for the core; the core tests pass on Windows and Ubuntu; a Windows-made backup restores on Ubuntu and a Linux-made backup restores on Windows; data files written on Windows are byte-identical before and after.
3. **Headless parity harness.** Avalonia test project, target catalog and parity map, legibility rules R1 to R3 and R5, R7, R8, R9, keyboard helpers, one embedded font family. Acceptance: the parity-map tests run and list every unmapped WinForms view; a deliberately low-contrast token fails R1; one R7 result is the same on Windows and Ubuntu runners; the limits of headless (no UI Automation or AT-SPI tree, render scale 1 unless set, no native dialogs) are written beside the checks.
4. **Style system and theme (unit 0).** Tokens and the five families from #161's Appendix C; Light, Dark, contrast themes; the embedded font family. Acceptance: R1 to R3 pass in Light and Dark; theme changes without a restart; whether and how Avalonia follows the system text size is checked and stated.
5. **Shell and navigation (unit 1).** Window, rail, page host, notice strip, history, shortcuts, the library-session object; the source scan listing every `Friend` type and member the interface calls. Acceptance: the 13 window-wide shortcuts work and the guide's table is tested against the key bindings.
6. **Start-up, errors and recovery (unit 17), with the Linux platform services.** Velopack first, single instance before storage (lock and socket on Linux, with a case-correct key), relaunch through `IRelauncher`, storage failure window, recovery drafts, the secret store with its fallback. Acceptance: the start-up order of `Program.vb:31-87` is kept and tested; recovery fixtures pass; restart after restore and opening the example library work from an AppImage.
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
17. **Deadlines (unit 11).** Acceptance: the keyboard map of `Controls/DeadlineList.vb:64-75` with a single roving tab stop (item 62); the startup reminder in the notice strip on every platform, and as a system notification on Windows through `INotifier`.
18. **Insights (unit 12).** Route map chart as a drawn control; a native report preview replacing `WebBrowser`. Acceptance: R5 passes with the report preview off the allow-list or with a stated reason.
19. **Import and Export (unit 13).** Import review panel with in-cell editing, export panel, backup, restore with one confirmation window. Acceptance: backup and restore round trip byte-identical on Windows and Linux under the fixed clock, fixed ids and path normalisation of section 3, check 4.
20. **Settings, Help and Updates (units 14, 16, 18).** Acceptance: help control at 24 logical pixels (item 62); access keys and one default-button rule in the most-used dialogs that remain (item 56); update offer in the notice strip.
21. **Parity sign-off and the guide.** Acceptance: the Avalonia build's view catalog matches the target map of fewer, calmer surfaces, whole; the guide's walkthroughs are rewritten for the new windows, with "What Linux shows" and the per-OS folders; Narrator and Orca passes recorded per surface.
22. **Release on two platforms.** The matrix of section 5, channel names, the kept assembly name, package id and main program, a per-channel `vpk download` in each build job, verifier and candidate script per platform. Acceptance: a Preview tag produces one draft with Windows and Linux assets, one `SHA256SUMS.txt`, and the verifier passes; a Linux install updates from the previous Linux Preview.
23. **Certification of the Avalonia build.** Acceptance: the Windows gates run again on the Windows VM; the five gates and the Linux lines of section 4 are written for Linux and run once on the chosen guest.
24. **macOS (v1.1.x).** Signing, `osx-arm64` job, macOS gates on a Mac, a VoiceOver pass, menu bar and Command-key shortcuts, relaunch through the bundle. Acceptance includes: the scripted session of section 3, check 4, leaves the data files byte-identical to Windows after path normalisation.

---

## 9. What becomes ordinary on Avalonia

These need no issue of their own; they come with the units above.

- Theme change without a restart, and so no window left half-switched when the system theme changes (#365's symptom), since styles are resolved live.
- Contrast themes read from the platform instead of `SystemColors` (`UiTheme.vb:43-69`).
- DPI scaling and dialog fitting: `DialogScaling`, `ResponsiveDialogSizingService` and most of `UiPolish` disappear.
- Page cross-fade by the compositor (a transitioning content control) in place of `PageFade`'s screen capture and 28 P/Invoke lines.
- Dark check boxes, themed borders, combo ellipsis, last-column fill: about 2,866 lines of workarounds.
- Virtualised lists in place of the Board's card cache and idle fill (`Form1.vb:100-107`, `Form1.BoardFill.vb`).
- Window chrome without `dwmapi` calls (`WindowChrome.vb:236-256`).

Following the system text size, which the issue lists here, is not claimed as a feature. It is an acceptance line of issue 4 and a risk in section 11: the line asks that it be checked and stated, and this paragraph is the statement. What is checked (issue 4, November 2026): Avalonia 12.1.6 offers no text-size setting in its public API, the Windows, X11 and macOS back ends included (`AvaloniaOffersNoTextSizeSetting`, which fails when an upgrade adds a member named like one); and the platform check (`--platform-check`) now records the system's text setting (Windows' `TextScaleFactor`; GNOME's `text-scaling-factor` and `Xft.dpi`), the window's render scaling, and the heights in the window's device pixels of a 13 px body line in Inter and of a fixed box 100 layout units high, which grows only when everything grows (`ThePlatformCheckRecordsTextScaleScalingAndLineHeight`). The Linux window job under Xvfb runs the check at the default and again with `Xft.dpi: 144`, and judges neither. The guest runs of November 28, 2026, with the published trial of master, give the statement: **on Windows nothing changes, and on Ubuntu everything grows.** In a Windows 11 guest (display scaling 100 %) at Text size 100 % and 150 %, the check read the setting (1 and 1.5) and the window stayed as it was: render scaling 1, the line 16 pixels, the box 100, and every measured control the same size. Only what Windows draws itself grew (the title bar, the file dialog). Today's WinForms app, opened in the same session, did not change either, so the two interfaces agree. In an Ubuntu 26.10 guest (GNOME 51, a Wayland session, the app through XWayland) at text scaling 1.0 and 1.5, GNOME turned its setting into `Xft.dpi` 96 and 144, Avalonia took that as its render scaling (1 and 1.5), and the line and the box grew together (16 to 24 pixels, 100 to 150): the whole window is enlarged, not the text alone. macOS has no text-size check yet. The same statement is in the limits of headless (`PaperRoute.Desktop.Tests/HeadlessLimits.md`, row 8). Whether v1.1 should follow Windows' text size, which today's app does not, is a question for the maintainer.

---

## 10. The order of the first steps

### The Models and Services split, step by step

Each step is one pull request with the full suite green; merged only after v1.0.1 ships, as settled.

1. **Pin the version behaviour (test only).** Test that `PacketExportService.AppVersion()` and `OnlineAccess.UserAgent()` report the app's version. Today's tests compare `AppVersion()` with itself (`BackupRestoreTests.vb:1043`). After the move both read the core assembly (`PacketExportService.vb:1100-1107`, `OnlineAccess.vb:449-450`), and the version goes into every backup manifest (`PortableBackupService.vb:305`).
2. **Move the tag colours out of `WorkTypeService`** (`:154-183`) to the app side; callers `Controls/TagChips.vb:28,32,33,328` and 4 test lines.
3. **Take the version and guide address off `UpdateService`.** `ErrorReportService.vb:140` and `UserGuideService.vb:35-36` receive the version text and installed flag from the app.
4. **Rework `IsolatedSessionStorageTests`** (`:197`, `:359-380`), which loads the app's assembly into an isolated load context and creates `Form1` from it.
5. **Add the empty `PaperRoute.Core.vbproj`** with `RootNamespace` `ManuscriptPipeline` (or every type changes its full name), the three version properties written explicitly (`release.yml:89-93` requires explicit nodes), `InternalsVisibleTo` for `PaperRouteTracker`, `PaperRoute.Tests`, `PaperRoute.V04Demo` and `PaperRoute.DisplayEvidence`; add it to `ManuscriptPipeline.slnx`; teach `release.yml` and `Build-ReleaseCandidate.ps1` to check the core's versions.
6. **`git mv` the 76 Models files.**
7. **`git mv` the 91 Services files**; move the Anthropic and ClosedXML package references to the core; add `System.Security.Cryptography.ProtectedData` (needed on plain `net10.0`, where `ProtectedData` is not part of the shared framework). One installed-build check: a new backup's manifest and the About box say 1.0.x.
8. **Certification tooling hashes and version-checks the core DLL** (`Capture-HarnessWindow.ps1:255-258`, `FixtureCollector/Program.cs:244`, `Build-ReleaseCandidate.ps1:441-458`).

Then stage 2 (build issue 2): the JSON newline factory, fixed newlines in stored text, the path comparer, the file-identity guard, the clock and id seams, the Windows-safe name rule, the restore path fix in both directions, the five interfaces, the core test project on Linux, and the inventory as a gate. The newline, path, name and seam changes write the same bytes on Windows and can ship in a 1.0.x patch if the maintainer wants them certified early.

### The December trial port

One week, on a throwaway branch, not merged: the Board with its cards on the example library, in a C# Avalonia 12 project over `PaperRoute.Core`. Recommended scope (question 9): it answers, in this order:

1. **How long the Board takes.** Checklist: shelves and cards, search, stage and sort filters, Needs Attention chips, card menu, Move to File Drawer panel, arrow keys between cards, Dark and Light, the Board's headless tests, and one accessibility pass (Narrator on Windows). The days spent on the Board, *d*, and the share finished, *f*, are recorded for section 7.
2. **Does the C# project build over the VB core** with compiled bindings, and does a VB project with one compiled `.axaml` file build (one hour at most)?
3. **Which test framework.** One legibility rule (R1 and R5) and one keyboard test (card Tab order) on headless Avalonia: first with MSTest through the documented `HeadlessUnitTestSession` API, then with xUnit v3 if that fails.
4. **The working-copy spike** (half a day to a day, no styling): a `ManuscriptEditorViewModel` over `RecordState` with one bound field, the baseline snapshot and the Unsaved changes state. This is what the Board cannot measure.
5. **Does it start on Linux:** the trial built for `linux-x64` and packed as an AppImage, started on Ubuntu 24.04 in the VirtualBox guest without installing `libfuse2`, with the file dialog and `Launcher` tried once, a restart of the app from `$APPIMAGE`, and the example library opened from it.
6. **Large library.** The Board on the large-library fixture, scrolled and filtered, against today's responsiveness budget.

---

## 11. Risks

| Risk | What would show it early |
|---|---|
| The estimate is too low: the interface is 1.7 times the issue's figure, and most of `EditManuscriptForm.vb` (4,914 lines) and `Form1.vb` (5,120) is orchestration woven into control code, not measured. | The trial's *f* and the working-copy spike. |
| The version silently changes in user data after the split: manifests and the User-Agent read the core's version. | Split step 1's test; the installed-build check in step 7. |
| Late binding under `Option Strict Off` breaks on the retargeted core (not inventoried). | Compiling a copy of the core on `net10.0` before step 6. |
| Cross-OS byte identity fails on more than paths: other stored text with the platform newline (only samples of the 143 newline rows were read), a clock or id use the seams miss, stored times with a local offset (not checked), or ICU (the Unicode library .NET uses on Linux) against Windows text comparison (`WorkTypeService.vb:88-90`, not checked). | The scripted session of section 3, run on Linux right after stage 2. |
| Restoring a backup across systems mangles managed paths: a Windows backup on Linux (`PortableRestoreService.vb:1984-1996`), or a Linux file name with a Windows-invalid character on Windows (`ManagedLibraryService.vb:986-1007`); both found by reading. | Stage 2 tests restoring a Windows-made backup on Ubuntu and a Linux-made backup on Windows. |
| Relaunching fails on Linux: `Application.Restart()` (`Form1.vb:4572-4576`) has no Avalonia counterpart, and `Environment.ProcessPath` (`ExampleLibraryService.vb:41`) lies inside the AppImage's mount, which may be gone once the parent exits or after an update replaced the file (mount lifetime not checked). | Trial question 5; the Linux gate for restart after restore. |
| No system notification on Linux: Avalonia has no notification API, and its tray icon needs an extension on GNOME. | Decided in this plan: notice strip on Linux; a D-Bus client only if the maintainer asks. |
| Fonts differ per system: 64 interface files hard-code `Segoe UI`, which Ubuntu lacks, so layouts and pixel rules would vary by runner. | Issue 3's acceptance: one R7 result equal on Windows and Ubuntu with the embedded font. |
| No MSTest adapter for Avalonia headless; a harness on `HeadlessUnitTestSession` may be flaky. | Trial question 3. |
| VB tooling for Avalonia does not work, if the maintainer chooses VB. | Trial question 2. |
| AppImage needs FUSE that a stock system or the CI runner lacks; file dialogs need a D-Bus portal a minimal guest lacks; Avalonia renders badly in a VirtualBox guest (none checked). | Trial question 5. |
| XWayland behaviour on GNOME Wayland (fractional scaling, input methods, "come forward") differs from an X11 session; Avalonia's own Wayland backend is a private preview. | The Linux gate in a GNOME Wayland session at 125% or 150%. |
| Screen-reader parity falls short: Avalonia's Linux accessibility backend is new in 12.0. | An Orca pass over the trial's Board. |
| Following the system text size may not be available or may differ per OS (Avalonia 12.1.6 offers no text-size setting, `AvaloniaOffersNoTextSizeSetting`; issue 4's guest runs showed that the system's setting does not reach a window on Windows and enlarges the whole window on Ubuntu, section 9). | Issue 4 (style system). |
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
9. The December trial's scope: the Board plus the working-copy spike and the Linux start check (recommended), or the Board plus the Versions section?

---

## Appendix A: the Windows-only API inventory

**What.** `.github/scripts/Get-WindowsOnlyApiInventory.ps1` (PowerShell 7) reads every `.vb` file under `PaperRoute.Core` (at `d67c312`, before the core-library split moved them there, these were `ManuscriptPipeline/Models` and `ManuscriptPipeline/Services`) and reports one row per Windows-only or doubtful API use, with the suggested replacement, then summaries by category, pattern and file, and the packages. It only reads files: no build, no `dotnet`. Two runs give identical output. Its limits are in the script's header (it reads one line at a time; types reached only through inference are missed).

**Why `.github/scripts`.** That folder holds the repository's checks over the source and the release assets that need no installed app (`Verify-ReleaseAssets.ps1`, `Set-RunnerDisplay.ps1`), and the stage 2 build issue is expected to run this script in CI as a gate. `ManualCertification` holds scripts that build candidates, drive virtual machines or capture installed windows, which this script does not.

**Output.** [Windows-Only-API-Inventory.md](Windows-Only-API-Inventory.md), beside this plan, generated at `d67c312`. Regenerate it from the repository root with:

```powershell
pwsh -NoProfile -File .github/scripts/Get-WindowsOnlyApiInventory.ps1 -Root . -AsMarkdown | Out-File -LiteralPath docs/design/Windows-Only-API-Inventory.md -Encoding utf8NoBOM
```

Add `-AsCsv <path>` to write every row, including the portable notes, to a CSV file as well.

**Reading it.** `windows-only` rows must be replaced before a file can run on Linux. `needs-reading` rows are heuristic hits a person reads; the verdicts are in Appendix B. `portable-note` rows do not block. At `d67c312`: 665 rows (430, 225, 10). The target after stage 2: zero `windows-only` rows in the files that moved to the core.

---

## Appendix B: the classification of the 176 Models and Services files

Made by reading each file's inventory rows at `d67c312`. Paths are under `ManuscriptPipeline/`. A moves unchanged; B needs a small change; C needs an interface first; D is interface code that stays with the interface. The line numbers are the reading's; they were not all re-opened while the plan was checked.

| # | File | Lines | Class | What changes, and why (path:line) |
|---|---|---|---|---|
| 1 | `Models/AffiliationRecord.vb` | 125 | A | No row. |
| 2 | `Models/AppAppearance.vb` | 9 | A | No row. |
| 3 | `Models/AppSettings.vb` | 50 | A | No row. |
| 4 | `Models/AppUpdateChannel.vb` | 8 | A | No row. |
| 5 | `Models/AssistantSettings.vb` | 44 | A | No row. |
| 6 | `Models/AssistantSuggestion.vb` | 27 | A | No row. |
| 7 | `Models/AttentionFilter.vb` | 12 | A | No row. |
| 8 | `Models/AuthorLibraryData.vb` | 26 | A | No row. |
| 9 | `Models/AuthorRecord.vb` | 132 | A | No row. |
| 10 | `Models/BibliographyAuthor.vb` | 41 | A | No row. |
| 11 | `Models/BibliographyFormat.vb` | 6 | A | No row. |
| 12 | `Models/BibliographyImportOptions.vb` | 7 | A | No row. |
| 13 | `Models/BibliographyImportResult.vb` | 10 | A | No row. |
| 14 | `Models/BibliographyParseResult.vb` | 11 | A | No row. |
| 15 | `Models/BibliographyRecord.vb` | 68 | A | No row. |
| 16 | `Models/CitationSnapshot.vb` | 79 | A | No row. |
| 17 | `Models/CorrespondenceItem.vb` | 29 | A | No row. |
| 18 | `Models/CorrespondenceType.vb` | 15 | A | No row. |
| 19 | `Models/CrossrefApplyOptions.vb` | 17 | A | No row. |
| 20 | `Models/CrossrefApplyResult.vb` | 15 | A | No row. |
| 21 | `Models/CrossrefAuthorSuggestion.vb` | 38 | A | No row. |
| 22 | `Models/CrossrefMetadataSuggestion.vb` | 43 | A | No row. |
| 23 | `Models/DeadlineGroup.vb` | 13 | A | No row. |
| 24 | `Models/DeadlineItem.vb` | 49 | A | No row. |
| 25 | `Models/DeadlineKind.vb` | 12 | A | No row. |
| 26 | `Models/EditorialDecision.vb` | 15 | A | No row. |
| 27 | `Models/EditorialDecisionEvent.vb` | 28 | A | No row. |
| 28 | `Models/ExcelColumnMapping.vb` | 11 | A | No row. |
| 29 | `Models/ExcelImportField.vb` | 23 | A | No row. |
| 30 | `Models/HistoryEvent.vb` | 21 | A | No row. |
| 31 | `Models/JournalCandidate.vb` | 96 | A | No row. |
| 32 | `Models/JournalChecklistTemplateItem.vb` | 26 | A | No row. |
| 33 | `Models/JournalFact.vb` | 51 | A | No row. |
| 34 | `Models/JournalRecord.vb` | 78 | A | No row. |
| 35 | `Models/JournalSubmission.vb` | 40 | A | No row. |
| 36 | `Models/Manuscript.vb` | 120 | A | No row. |
| 37 | `Models/ManuscriptAuthor.vb` | 18 | A | No row. |
| 38 | `Models/ManuscriptExternalLink.vb` | 33 | A | No row. |
| 39 | `Models/ManuscriptLocation.vb` | 9 | A | No row. |
| 40 | `Models/ManuscriptMetadata.vb` | 38 | A | No row. |
| 41 | `Models/ManuscriptReadiness.vb` | 27 | A | No row. |
| 42 | `Models/ManuscriptReminder.vb` | 21 | A | No row. |
| 43 | `Models/ManuscriptRoute.vb` | 76 | A | No row. |
| 44 | `Models/ManuscriptVersion.vb` | 31 | A | No row. |
| 45 | `Models/OnlineServicesSettings.vb` | 24 | A | No row. |
| 46 | `Models/OrcidAffiliationSuggestion.vb` | 77 | A | No row. |
| 47 | `Models/OrcidApplyOptions.vb` | 24 | A | No row. |
| 48 | `Models/OrcidApplyResult.vb` | 15 | A | No row. |
| 49 | `Models/OrcidProfileSuggestion.vb` | 56 | A | No row. |
| 50 | `Models/OrcidWorkSuggestion.vb` | 54 | A | No row. |
| 51 | `Models/PacketFileIntegrityResult.vb` | 38 | A | No row. |
| 52 | `Models/PacketFileIntegrityStatus.vb` | 13 | A | No row. |
| 53 | `Models/PaperStage.vb` | 14 | A | No row. |
| 54 | `Models/PublicationExportFormat.vb` | 9 | A | No row. |
| 55 | `Models/PublicationExportScope.vb` | 9 | A | No row. |
| 56 | `Models/PublicationExportStyle.vb` | 8 | A | No row. |
| 57 | `Models/PublicationMatch.vb` | 63 | A | No row. |
| 58 | `Models/ReadinessItemState.vb` | 38 | A | No row. |
| 59 | `Models/ReadinessItemStatus.vb` | 9 | A | No row. |
| 60 | `Models/ReadinessSummary.vb` | 35 | A | No row. |
| 61 | `Models/ReminderKind.vb` | 9 | A | No row. |
| 62 | `Models/ReminderOccurrence.vb` | 77 | A | No row. |
| 63 | `Models/ReminderStatus.vb` | 9 | A | No row. |
| 64 | `Models/ReviewerResponseItem.vb` | 27 | A | No row. |
| 65 | `Models/ReviewerResponseStatus.vb` | 10 | A | No row. |
| 66 | `Models/SubmissionPacket.vb` | 39 | A | No row. |
| 67 | `Models/SubmissionPacketFile.vb` | 36 | A | No row. |
| 68 | `Models/SubmissionPacketFileRole.vb` | 19 | A | No row. |
| 69 | `Models/SubmissionPacketFileStorageMode.vb` | 9 | A | No row. |
| 70 | `Models/SubmissionWorkflowRequest.vb` | 23 | A | No row. |
| 71 | `Models/TagColor.vb` | 25 | A | No row. |
| 72 | `Models/TitlePageApplyResult.vb` | 19 | A | No row. |
| 73 | `Models/TitlePageAuthor.vb` | 20 | A | No row. |
| 74 | `Models/TitlePageParseResult.vb` | 35 | A | No row. |
| 75 | `Models/TitlePageSourceFormat.vb` | 9 | A | No row. |
| 76 | `Models/WorkType.vb` | 17 | A | No row. |
| 77 | `Services/AppSettingsService.vb` | 224 | B | set JsonSerializerOptions.NewLine = vbCrLf on the indented options (:37). |
| 78 | `Services/AssistantProviders.vb` | 648 | A | Uses ProtectedKeyStore through OnlineAccess.KeyStore (:164, :580). |
| 79 | `Services/AssistantService.vb` | 685 | A | Environment.NewLine builds prompt text sent to the model (:175-:177) and a joined answer (:545); not a data file. |
| 80 | `Services/AssistantSuggestionService.vb` | 98 | A | No row. |
| 81 | `Services/AuthorLibraryRepository.vb` | 790 | B | set JsonSerializerOptions.NewLine = vbCrLf on the indented options (:80). Calls WorkTypeService (:576), portable part. |
| 82 | `Services/AuthorLibrarySearchIndex.vb` | 162 | A | No row. |
| 83 | `Services/BibliographyExchangeService.vb` | 366 | A | Calls WorkTypeService mapping (:258-:259). |
| 84 | `Services/BibliographyTextService.vb` | 224 | A | No row. |
| 85 | `Services/BibTeXService.vb` | 965 | A | Calls WorkTypeService mapping (:87). Export text uses Environment.NewLine/AppendLine: see the line-ending decision (question 7). |
| 86 | `Services/CalendarDateJson.vb` | 95 | A | No row. |
| 87 | `Services/ChronologyProvenanceService.vb` | 367 | A | No row. |
| 88 | `Services/CitationMetricsService.vb` | 94 | A | No row. |
| 89 | `Services/CitationsService.vb` | 388 | A | No row. |
| 90 | `Services/CitationStore.vb` | 169 | B | set JsonSerializerOptions.NewLine = vbCrLf on the indented options (:22). |
| 91 | `Services/CrossrefApplyService.vb` | 564 | A | Calls WorkTypeService.FromRegistry (:179). |
| 92 | `Services/CrossrefClient.vb` | 649 | A | No row. |
| 93 | `Services/DeadlineService.vb` | 372 | A | No row. |
| 94 | `Services/DialogScaling.vb` | 323 | D | TextRenderer (:232-:249), Screen, user32/gdi32 P/Invoke (:309-:317). |
| 95 | `Services/DoiNormalizer.vb` | 102 | A | No row. |
| 96 | `Services/EditorialDecisionDisplayService.vb` | 19 | A | No row. |
| 97 | `Services/ErrorReportService.vb` | 207 | B | Calls UpdateService.CurrentVersionText (:140), which moves to the portable version helper; label "Windows:" (:141) becomes "OS:". RuntimeInformation is portable. |
| 98 | `Services/ExampleLibraryService.vb` | 384 | A | Starts its own executable with UseShellExecute = False (:41-:46): portable on Windows; inside an AppImage it needs `IRelauncher` (section 1). |
| 99 | `Services/FileOpenService.vb` | 124 | C | Launcher + confirm seam: IsProgramOrScript (:32-:47) and the delegate overload (:68-:83) are portable; MessageBox (:91-:104) and shell launch (:113-:118) go behind ILauncher/IConfirm in the UI. Extend the program-extension list for Linux and macOS (.sh, .desktop, .AppImage, .app, .command; executable bit). |
| 100 | `Services/FlexibleExcelImporter.vb` | 1173 | B | Appends to imported notes with Environment.NewLine (:1095); notes are stored in the library. Use a fixed newline. |
| 101 | `Services/HiddenMetadataService.vb` | 1866 | A | Backslash uses accept both separators (:335, :557): portable. |
| 102 | `Services/IcsCalendarService.vb` | 302 | A | ICS lines use a fixed vbCrLf (:11); Environment.NewLine (:261) joins description text that is escaped to \n (:147-:160) (read, portable). |
| 103 | `Services/IssnService.vb` | 93 | A | No row. |
| 104 | `Services/JournalChoiceGuide.vb` | 83 | A | No row. |
| 105 | `Services/JournalFactCatalog.vb` | 194 | A | No row. |
| 106 | `Services/JournalFactsClients.vb` | 530 | A | No row. |
| 107 | `Services/JournalFactsService.vb` | 800 | A | Calls only UrlSafetyService.IsSafeHttpUrl (:542 and 3 more lines), which stays in the core. |
| 108 | `Services/JournalShortlistService.vb` | 293 | A | No row. |
| 109 | `Services/JournalSuggestionService.vb` | 491 | A | No row. |
| 110 | `Services/LegacyExcelImporter.vb` | 700 | A | No row. |
| 111 | `Services/LibraryExcelExporter.vb` | 417 | A | Calls WorkTypeService (:91); ClosedXML ships no Windows-only target. |
| 112 | `Services/ManagedLibraryService.vb` | 1626 | B | Three OrdinalIgnoreCase path comparisons (:112, :147, :281) need an OS-aware comparer; SpecialFolder.MyDocuments (:50) maps to ~/Documents (or XDG_DOCUMENTS_DIR) on Linux and macOS since .NET 8. Managed-copy names (:986-:1007) need the Windows-safe name rule and the id factory. |
| 113 | `Services/ManagedPacketDeletionService.vb` | 1040 | A | No row. |
| 114 | `Services/ManuscriptAttentionService.vb` | 513 | A | No row. |
| 115 | `Services/ManuscriptCloneService.vb` | 549 | A | No row. |
| 116 | `Services/ManuscriptLifecycleService.vb` | 941 | A | No row. |
| 117 | `Services/ManuscriptRepository.vb` | 1713 | B | set JsonSerializerOptions.NewLine = vbCrLf on the indented options (:122). Environment.NewLine uses read are messages. Calls WorkTypeService (:1074), portable part. |
| 118 | `Services/ManuscriptRouteProjectionService.vb` | 1010 | A | No row. |
| 119 | `Services/ManuscriptStagePolicyService.vb` | 152 | A | No row. |
| 120 | `Services/ManuscriptStageSummaryService.vb` | 43 | A | No row. |
| 121 | `Services/ManuscriptVersionService.vb` | 803 | A | No row. |
| 122 | `Services/OnlineAccess.vb` | 744 | A | Uses ProtectedKeyStore through its public surface (:158, :457-:481, :711-:723), which the C seam keeps. |
| 123 | `Services/OnlineServiceCatalog.vb` | 134 | A | Line-ending rows only (le-appendline): on-screen or export text; see the line-ending decision. |
| 124 | `Services/OrcidApplyService.vb` | 442 | A | Calls WorkTypeService.FromRegistry (:405). |
| 125 | `Services/OrcidClient.vb` | 1027 | A | No row. |
| 126 | `Services/OrcidIdentifierService.vb` | 139 | A | No row. |
| 127 | `Services/PacketExportPackage.vb` | 515 | B | Portable code (JsonWriterOptions already fixes NewLine = vbLf, :96). The packet README tells only Windows users how to check hashes (:464): add the shasum/sha256sum line. Depends on PacketExportService types, which move after that file's B change. |
| 128 | `Services/PacketExportService.vb` | 1668 | B | FileIdentity (:1385-:1396) uses kernel32 GetFileInformationByHandle (:1428-:1430): guard with OperatingSystem.IsWindows() and return Nothing elsewhere (the code already falls back to path comparison, :1399-:1401) or add a stat() identity; SamePath (:1483) needs an OS-aware comparer. Reserved names (:696-:698) and the explicit invalid-character set (:1659-:1662) stay: a Windows-safe name is safe everywhere. |
| 129 | `Services/PortableBackupService.vb` | 547 | B | set JsonSerializerOptions.NewLine = vbCrLf on the indented options (:321). backup-info text uses Environment.NewLine (:257-:287). Calls PacketExportService (:305). |
| 130 | `Services/PortableRestoreService.vb` | 2553 | B | set JsonSerializerOptions.NewLine = vbCrLf on the indented options (:200). Calls WorkTypeService (:1819), portable part. Restore path fix (:1984-:1996). |
| 131 | `Services/ProductInfo.vb` | 34 | A | No row. |
| 132 | `Services/ProtectedKeyStore.vb` | 139 | C | ISecretProtector seam for ProtectedData.Protect/Unprotect (:88, :100), which throws PlatformNotSupportedException off Windows. File layout and the rest of the class stay. Windows DPAPI; Linux Secret Service (libsecret); macOS Keychain. OnlineAccess already has a KeyStoreFactory seam (:158). |
| 133 | `Services/PublicationCheckService.vb` | 223 | A | No row. |
| 134 | `Services/PublicationExportService.vb` | 842 | A | Export text built with AppendLine (28 lines): see the line-ending decision. |
| 135 | `Services/PublicationMatchService.vb` | 439 | A | No row. |
| 136 | `Services/RecoveryDraftStore.vb` | 174 | B | set JsonSerializerOptions.NewLine = vbCrLf on the indented options (:70). |
| 137 | `Services/ReleaseNotesPresentationService.vb` | 325 | A | Environment.NewLine joins on-screen release notes (:242). |
| 138 | `Services/ReminderService.vb` | 484 | A | No row. |
| 139 | `Services/ReportService.vb` | 232 | A | No row. |
| 140 | `Services/ResponsiveDialogSizingService.vb` | 153 | D | Form sizing with Screen.FromControl (:119) and Drawing geometry. |
| 141 | `Services/ReviewerResponseExportService.vb` | 137 | A | Export text built with AppendLine (15 lines): see the line-ending decision. |
| 142 | `Services/ReviewerResponseService.vb` | 182 | A | No row. |
| 143 | `Services/RisService.vb` | 578 | A | Calls WorkTypeService mapping (:174). Export line endings: see the line-ending decision. |
| 144 | `Services/RouteAnalyticsService.vb` | 443 | A | No row. |
| 145 | `Services/RouteMapService.vb` | 387 | A | No row. |
| 146 | `Services/RouteNavigationService.vb` | 48 | A | No row. |
| 147 | `Services/RouteSummaryService.vb` | 130 | A | No row. |
| 148 | `Services/Schema5MigrationService.vb` | 258 | B | set JsonSerializerOptions.NewLine = vbCrLf on the indented options (:218, :243). |
| 149 | `Services/Schema6MigrationService.vb` | 58 | B | set JsonSerializerOptions.NewLine = vbCrLf on the indented options (:54). |
| 150 | `Services/Schema7MigrationService.vb` | 83 | B | set JsonSerializerOptions.NewLine = vbCrLf on the indented options (:42). Calls WorkTypeService (:73), portable part. |
| 151 | `Services/SingleInstanceService.vb` | 117 | C | ISingleInstance seam. KeyFor (:36-:40) upper-cases the path, which is wrong on case-sensitive file systems. Named Mutex (:48) works on Unix, but the named EventWaitHandle (:55, :64) does not ("named system events are not supported" on Unix) and AllowSetForegroundWindow (:20-:22) is user32. Per-OS implementation: Windows as today; Linux/macOS a lock plus a Unix domain socket for "come forward". |
| 152 | `Services/StageClockService.vb` | 54 | A | No row. |
| 153 | `Services/StandardExcelImporter.vb` | 1223 | A | Calls WorkTypeService (:247, :266); ClosedXML ships no Windows-only target. |
| 154 | `Services/StandardTemplateGenerator.vb` | 605 | A | No row. |
| 155 | `Services/StartupRecoveryService.vb` | 252 | A | No row. |
| 156 | `Services/StorageEnvironment.vb` | 203 | B | One OrdinalIgnoreCase comparison of the temp folder (:52); otherwise portable. |
| 157 | `Services/StorageFile.vb` | 265 | A | No row. |
| 158 | `Services/StorageMigrationService.vb` | 1433 | B | set JsonSerializerOptions.NewLine = vbCrLf on the indented options (:725, :968, :1233, :1393); OS-aware comparer (:1145). SpecialFolder roots (:89-:130) work as written; the plan states the per-OS data and library folders (section 1). |
| 159 | `Services/SubmissionPacketIntegrityService.vb` | 260 | A | No row. |
| 160 | `Services/SubmissionPacketService.vb` | 653 | A | No row. |
| 161 | `Services/SubmissionPacketWorkflowService.vb` | 55 | A | No row. |
| 162 | `Services/SubmissionReadinessService.vb` | 427 | A | No row. |
| 163 | `Services/SubmissionReadinessValidationService.vb` | 585 | A | No row. |
| 164 | `Services/TitlePageApplyService.vb` | 172 | A | No row. |
| 165 | `Services/TitlePageParserService.vb` | 1332 | B | Joins imported abstract paragraphs with Environment.NewLine (:409, :1072); the abstract is stored in the library, so a Linux import writes LF where Windows writes CR LF. Use a fixed newline. |
| 166 | `Services/UiMotion.vb` | 311 | D | WinForms Timer clock (:120, :139), Control owner (:210), user32 P/Invoke (:193). |
| 167 | `Services/UiPolish.vb` | 1126 | D | Restyles WinForms controls; names UI types (ActionButton, ThemedBorder, CheckGlyph, SectionCard) and Imports ManuscriptPipeline.Controls (:8). Rewritten, not moved. |
| 168 | `Services/UiTheme.vb` | 627 | D | Theme tokens as System.Drawing.Color, SystemColors (Dark detection, high contrast), SystemInformation.HighContrast (:44); used by 66 UI files. The ARGB values could later seed an Avalonia theme, but the file stays. |
| 169 | `Services/UpdateService.vb` | 436 | C | Split: CurrentVersionText (Application.ProductVersion :31) to a portable version helper (assembly informational version); Velopack manager, channels and the UpdateSteps seam (:358-:388) move; prompts (UpdatePromptForm :375, UpdateProgressForm :219, Form1 :73 and :76, MessageBox :387) stay behind an IUpdateUi in the UI. |
| 170 | `Services/UrlSafetyService.vb` | 100 | C | Launcher seam: IsSafeHttpUrl stays; OpenInBrowser (:77-:96, UseShellExecute :92) moves behind the same ILauncher (16 UI files call it, 22 lines). |
| 171 | `Services/UserGuideService.vb` | 468 | B | Calls UpdateService.CurrentVersionText and IsInstalledBuild (:35-:36): point them at the portable part of the UpdateService split. |
| 172 | `Services/WindowChrome.vb` | 262 | D | dwmapi/user32/kernel32 P/Invoke (:236-:256) on Form handles. |
| 173 | `Services/WindowsNotificationService.vb` | 152 | D | NotifyIcon balloon (:36-:56) and WinForms Timer; only caller is in the UI (1 line). The Avalonia build supplies its own notifier (`INotifier`, section 1). |
| 174 | `Services/WorkflowHelpCatalog.vb` | 101 | A | No row. |
| 175 | `Services/WorkflowPresentationService.vb` | 69 | A | No row. |
| 176 | `Services/WorkTypeService.vb` | 261 | B | Move Background/Foreground (:156-:183, System.Drawing.Color and SystemColors, UiTheme.IsHighContrast) to the UI or return ARGB values; the rest (parse, tags, BibTeX/RIS mapping) is portable. No core file calls the two colour functions. |

Totals: A 144, B 20, C 5, D 7 (of 176). The files that move in stage 1 are the 144 A and 20 B files and the C files `ProtectedKeyStore`, `SingleInstanceService` and `UrlSafetyService`, which move unchanged and gain their interfaces in stage 2: 167 files.
