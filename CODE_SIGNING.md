# Code signing policy

**Status:** PaperRoute Tracker's Windows releases are not code-signed yet. The project will apply to the [SignPath Foundation](https://signpath.org) for free code signing for open-source projects once v0.9.2 is published ([#216](https://github.com/JUhalt/PaperRoute-Tracker/issues/216)). Until a certificate is issued, Windows shows a warning before Setup runs; the User Guide's [Installing](docs/USER_GUIDE.md#installing) section explains the warning and how to check a download against `SHA256SUMS.txt`.

When signing begins, this section will read: "Free code signing provided by [SignPath.io](https://about.signpath.io), certificate by [SignPath Foundation](https://signpath.org)."

## What is signed

- Only files built from this repository's source code by the release workflow: PaperRoute's own program files (`PaperRouteTracker.exe` and `PaperRouteTracker.dll`) and, if SignPath's terms allow it, the Setup program that installs them.
- Third-party libraries and the bundled .NET runtime are shipped as their publishers released them and are never signed with PaperRoute's certificate. Their names, versions and licenses are in [`THIRD-PARTY-NOTICES.txt`](THIRD-PARTY-NOTICES.txt).
- Every signed file carries the product name "PaperRoute Tracker" and the version of the release it belongs to.

## How a release is built and signed

- Releases are built only by [`.github/workflows/release.yml`](.github/workflows/release.yml), on GitHub-hosted runners, from a clean checkout of a `v*` tag on `master`, with no files carried over from earlier builds.
- The workflow uploads the unsigned files as a workflow artifact and submits them to SignPath, which verifies where they came from: the repository, the branch, the commit and the build job.
- Every signing request is approved by hand by an approver listed below before anything is signed.
- Signing happens before `SHA256SUMS.txt` is written and before the release is published, so the published checksums are those of the signed files.
- Build scripts and workflow files are source code too: they change only through the same reviewed pull requests as the app.

The signing step is added to the release workflow when the SignPath application is approved; until then the workflow publishes unsigned builds as described above.

## Team and roles

| Role | Members |
| --- | --- |
| Committers and reviewers | [Joshua Uhalt (@JUhalt)](https://github.com/JUhalt), the project's maintainer |
| Approvers | [Joshua Uhalt (@JUhalt)](https://github.com/JUhalt) |

Every change reaches `master` through a pull request that the maintainer reviews and merges after the automated build and tests pass. Some changes are prepared with an AI coding assistant; they are reviewed and merged the same way, and their commits say so. Everyone in these roles uses multi-factor authentication on GitHub and on SignPath.

## Privacy

PaperRoute sends no usage data, telemetry or crash reports. What it can contact, and what it sends, is in the [privacy policy](PRIVACY.md).

## Reporting a problem

Report a suspected security problem privately, as described in the [security policy](.github/SECURITY.md).
