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

`global.json` pins the .NET SDK at 10.0.403, so local and GitHub Actions builds bundle the same .NET runtime, the one `THIRD-PARTY-NOTICES.txt` names. Moving the SDK is a deliberate change, made together with the notices. The release scripts check them against the runtime pack the publish restores: its version, the source commit on line 1 of its `Microsoft.NETCore.App.versions.txt`, and its own `THIRD-PARTY-NOTICES.TXT`, which the notices reproduce ([#269](https://github.com/JUhalt/PaperRoute-Tracker/issues/269)).

```powershell
dotnet restore ManuscriptPipeline.slnx
dotnet build ManuscriptPipeline.slnx --configuration Release
```

Before opening a pull request, confirm the solution builds in Release configuration, run the automated test suite, and describe how you tested the change.

```powershell
dotnet test PaperRoute.Tests/PaperRoute.Tests.vbproj --configuration Release --no-build
```

## Data safety

Changes involving persistence, backup/restore, managed files, or import logic should preserve existing local data whenever possible. Avoid silently changing storage locations or destructive behavior without a migration path.

## Style

- Prefer clear, explicit VB.NET over clever compact syntax.
- Keep UI changes DPI-aware.
- Preserve Light/Dark/System theme compatibility.
- Add user-facing validation before destructive actions.
