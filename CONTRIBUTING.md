# Contributing to PaperRoute

Thanks for helping improve PaperRoute Tracker.

## Good contributions

- Reproducible bug reports
- Spreadsheet importer edge cases
- Accessibility and DPI-scaling fixes
- Academic publishing workflow improvements
- Documentation corrections
- Small, focused pull requests

## Development setup

PaperRoute currently targets .NET 10 Windows Forms and is written in Visual Basic .NET.

The solution, `ManuscriptPipeline.slnx`, has six projects: the app in `ManuscriptPipeline` (the windows, and the few services that belong to the interface, such as the theme, the dialog scaling and the updater), its data model and other services in `PaperRoute.Core` (a .NET 10 library without Windows Forms), the Windows implementations the core asks for in `PaperRoute.Platform` (the DPAPI key protection, the launcher that opens links and files, and the file identity the packet export asks for, which the app registers at start-up, and single instance, which the app calls directly; the core never references this library), the tests in `PaperRoute.Tests` and, for the core's tests that need no Windows, `PaperRoute.Core.Tests`, and `PaperRoute.TestFixtures`, test data the tests share (the 500-manuscript library and the Board's checked-in sort orders), which nothing the app ships references. The app and the library share the root namespace `ManuscriptPipeline`, so a type keeps its name whichever of the two holds it.

`global.json` pins the .NET SDK at 10.0.403, so local and GitHub Actions builds bundle the same .NET runtime, the one `THIRD-PARTY-NOTICES.txt` names. Moving the SDK is a deliberate change, made together with the notices. The release scripts check them against the runtime pack the publish restores: its version, the source commit on line 1 of its `Microsoft.NETCore.App.versions.txt`, and its own `THIRD-PARTY-NOTICES.TXT`, which the notices reproduce ([#269](https://github.com/JUhalt/PaperRoute-Tracker/issues/269)).

```powershell
dotnet restore ManuscriptPipeline.slnx
dotnet build ManuscriptPipeline.slnx --configuration Release
```

Before opening a pull request, confirm the solution builds in Release configuration, run the automated test suite, and describe how you tested the change.

```powershell
dotnet test PaperRoute.Tests/PaperRoute.Tests.vbproj --configuration Release --no-build
dotnet test PaperRoute.Core.Tests/PaperRoute.Core.Tests.vbproj --configuration Release --no-build
```

Four tests in `PaperRoute.Core.Tests` (category `SameDataEverywhere`) compare the bytes a session stores with values recorded on the CI runners, which are on UTC with the en-US culture. On a computer in another time zone or culture they report themselves inconclusive and say why. CI runs them on Windows and on Ubuntu, where a test that did not run counts as not passing.

`PaperRoute.Core` must not call an API that exists only on Windows, so that it can run on Linux and macOS as well as Windows; CI builds and tests it on Ubuntu, as below, and nothing tests it on macOS. Windows code belongs in `PaperRoute.Platform`: behind one of the core's interfaces when the core needs it, or called by the app directly when only the app does, as single instance is. Two checks hold this, and either one fails a pull request: the core treats the platform warning CA1416 as a build error, and `.github/scripts/Get-WindowsOnlyApiInventory.ps1 -FailOnWindowsOnly`, which also finds what the compiler cannot see (a `DllImport`, a named kernel object), fails on any windows-only row. CI runs the inventory in the Windows job and again in **Core on Linux**, which builds the core on Ubuntu and requires every test in `PaperRoute.Core.Tests` to pass there too, none skipped. To run the inventory yourself:

```powershell
./.github/scripts/Get-WindowsOnlyApiInventory.ps1 -Root . -FailOnWindowsOnly
```

## Data safety

Changes involving persistence, backup/restore, managed files, or import logic should preserve existing local data whenever possible. Avoid silently changing storage locations or destructive behavior without a migration path.

## Style

- Prefer clear, explicit VB.NET over clever compact syntax.
- Keep UI changes DPI-aware.
- Preserve Light/Dark/System theme compatibility.
- Add user-facing validation before destructive actions.
