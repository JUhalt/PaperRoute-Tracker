# Cross-system fixtures (core stage 2, step 14)

`CrossSystemSessionTests` compares what the scripted session writes with three recorded files. They are recorded on the CI runners only (UTC, `en-US`), never on a PC.

| File | What it is | Taken from |
|---|---|---|
| `session-hashes.txt` | The golden hash list: one line per file the session wrote (`saved/`, `backup/`, `restored/`), its path, a tab, the SHA-256 of its bytes after normalisation; sorted by ordinal path; every line ends in LF. | `session-hashes-linux.txt` or `session-hashes-windows.txt`, attached by `TheSessionWritesTheGoldenHashList`. Check it in only when the two are identical. |
| `windows-backup.zip` | The portable backup the session made on the Windows runner. | `windows-backup.zip`, attached by `ThisSystemsBackupRestores` in the Windows Build job. |
| `linux-backup.zip` | The same, made on the Linux runner. | `linux-backup.zip`, attached by `ThisSystemsBackupRestores` in the Core on Linux job. |

An attachment lands in the job's `TestResults` folder, under `<user>_<machine>_<time>/In/<id>/<machine>/`. The Windows Build job uploads it in the artifact `PaperRoute-test-results` (inside its `TestResults/` folder). The Core on Linux job uploads it in `PaperRoute-core-linux-results` (at the top of the artifact).

Copy each file here unchanged. `.gitattributes` marks `*.txt` here `-text` and `*.zip` `binary`, so git stores and checks out their bytes exactly. Check with `git ls-files --eol PaperRoute.Core.Tests/Fixtures/CrossSystem` that the list shows `-text`.

The golden file must be recorded again when the session's data changes on purpose, for example after a version bump: `backup.json` stores the app's version.
