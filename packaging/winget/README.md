# WinGet manifest

These are the WinGet manifest files for PaperRoute Tracker, package `JUhalt.PaperRouteTracker` (#343, the first route named in #216). Each version has a folder of three files under `manifests\j\JUhalt\PaperRouteTracker\`, the path that `microsoft/winget-pkgs` uses, so a version folder can be copied into a fork of that repository unchanged:

- `JUhalt.PaperRouteTracker.yaml`, the version file;
- `JUhalt.PaperRouteTracker.installer.yaml`, the installer: the Stable Setup from that version's GitHub release, its SHA-256 from the release's `SHA256SUMS.txt`, installed for the current Windows account (`Scope: user`) with `--silent`;
- `JUhalt.PaperRouteTracker.locale.en-US.yaml`, the name, publisher, license and links.

The package name, publisher and product code are the ones the release workflow gives Velopack (`--packTitle`, `--packAuthors` and `--packId` in `.github/workflows/release.yml`), because Setup writes them into the Apps entry that WinGet reads.

This copy is a record. PaperRoute is not in WinGet's community catalog: nothing has been submitted to `microsoft/winget-pkgs`.

## How the files are checked

- `winget validate --manifest packaging\winget\manifests\j\JUhalt\PaperRouteTracker\1.0.0` gave "Manifest validation succeeded." with winget v1.29.380 on November 14, 2026. It checks the files against the schema; it does not download Setup or check its hash.
- Two tests in `PaperRoute.Tests\RepositoryDocsTests.vb` run with the suite. `TheWinGetManifestMatchesTheVelopackPackMetadata` checks the package name, publisher and product code against `release.yml`. `TheWinGetManifestPointsAtThatVersionsStableSetup` checks that each folder's files carry the folder's version, that the installer URL is that release's Stable Setup, that the links point at that version's tag, and the installer type, scope and switches. They can check only the hash's form, not its value.
- Installing from the manifest (`winget install --manifest <folder>`, after `winget settings --enable LocalManifestFiles`) has not been tried yet. It belongs in the certification VM, not on a working PC.

## The next version

The hash is known only once a release is published, so a version's folder is written after its release. Copy the last version's folder to one named for the new version, then change, in all three files, `PackageVersion`; in the installer, `ReleaseDate`, the tag in `InstallerUrl`, and `InstallerSha256`, which is the Setup line of the new release's `SHA256SUMS.txt` in upper case; and in the locale file, the tag in `PrivacyUrl`, `LicenseUrl`, `ReleaseNotesUrl` and `DocumentUrl`. Then run `winget validate` on the new folder and the two tests.

A `1.0.1` folder is added after v1.0.1 is published, and that is the one to submit. A 1.0.0 installed from WinGet would be offered 1.0.1 by its own update check when it first starts.

## Submitting

Submitting is the maintainer's own step: a public pull request to `microsoft/winget-pkgs` from the maintainer's account, made only with the maintainer's yes. That repository takes one package version per pull request, with manifest files only. Its checks include antivirus scans of Setup, and Microsoft may decline a package.

## Not yet known

PaperRoute updates itself, and `winget upgrade` would also update it, by running the newer Setup with `--silent`. What is not yet known:

- whether WinGet recognizes a PaperRoute installed by downloading Setup, or one that has updated itself, as this package; that can be tried only once the package is in the catalog;
- what happens to an open PaperRoute when `winget upgrade` or `winget install` runs Setup over it: Velopack is expected to close it without asking, and whether unsaved edits are kept has not been checked;
- whether a WinGet install shows any Windows or SmartScreen prompt.
