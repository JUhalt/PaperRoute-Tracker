# Security Policy

## Supported versions

Security fixes are prioritized for the current stable PaperRoute release and active development line.

PaperRoute supports Windows 11 on x64 computers. Windows 10 and Windows on ARM may work, but they are not tested. Only the 0.9 releases are supported for updating straight to 1.0. An older version's update check offers 1.0 as well: the migration tests cover that update, but no release certification does, so create a backup with **Settings → Backup Library...** first. The [upgrade notes](https://github.com/JUhalt/PaperRoute-Tracker/blob/master/UPGRADE_NOTES.md#supported-versions) say the same.

PaperRoute bundles the .NET runtime it runs on, so a .NET runtime security fix reaches you only in a PaperRoute release. When a .NET security update fixes a runtime component PaperRoute ships, the target, on a best-effort basis, is a PaperRoute patch release within 30 days; a fix to a component PaperRoute does not ship needs no release. Installed copies receive it through the update check when PaperRoute starts, or **Settings > Check for Updates...**. Dependency alerts, a vulnerable-package check in every CI build, and the runtime version recorded for each release ([#144](https://github.com/JUhalt/PaperRoute-Tracker/issues/144)) show when such a fix is due.

## Reporting a vulnerability

Please do not publish suspected vulnerabilities, sensitive local paths, manuscript content, credentials, tokens, or private research data in a public GitHub issue.

Send suspected vulnerability reports privately to Joshua Uhalt at [Josh.Uhalt@gmail.com](mailto:Josh.Uhalt@gmail.com). Do not include private research data unless needed and explicitly agreed; start with a minimal, anonymized description.

Useful reports include:

- the affected PaperRoute version;
- the relevant workflow or component;
- clear reproduction steps;
- expected and observed behavior;
- the potential impact; and
- any suggested mitigation.

PaperRoute is local-first, but features involving files, imports, external metadata services, the optional AI assistant and its provider keys, update infrastructure, and local persistence should still be treated as security-sensitive.
