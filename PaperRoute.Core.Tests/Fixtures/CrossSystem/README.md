# Cross-system fixtures (core stage 2, step 14)

`CrossSystemSessionTests` compares what the scripted session writes with three recorded files. They are recorded on the CI runners only (UTC, `en-US`), never on a PC.

| File | What it is | Taken from |
|---|---|---|
| `session-hashes.txt` | The golden hash list: one line per file the session wrote (`saved/`, `backup/`, `restored/`), its path, a tab, the SHA-256 of its bytes after normalisation; sorted by ordinal path; every line ends in LF. | `session-hashes-linux.txt` or `session-hashes-windows.txt`, attached by `TheSessionWritesTheGoldenHashList`. Check it in only when the two are identical. |
| `windows-backup.zip` | The portable backup the session made on the Windows runner. | `windows-backup.zip`, attached by `ThisSystemsBackupRestores` in the Windows Build job. |
| `linux-backup.zip` | The same, made on the Linux runner. | `linux-backup.zip`, attached by `ThisSystemsBackupRestores` in the Core on Linux job. |

An attachment lands in the job's `TestResults` folder, under `<user>_<machine>_<time>/In/<id>/<machine>/`. The Windows Build job uploads it in the artifact `PaperRoute-test-results` (inside its `TestResults/` folder). The Core on Linux job uploads it in `PaperRoute-core-linux-results` (at the top of the artifact).

Copy each file here unchanged. `.gitattributes` marks `*.txt` here `-text` and `*.zip` `binary`, so git stores and checks out their bytes exactly. Check with `git ls-files --eol PaperRoute.Core.Tests/Fixtures/CrossSystem` that the list shows `-text`.

A version bump needs no recording: `backup.json` stores the app's version, and the test checks it against the build's version and then lists it as `{app version}` (`NormaliseTheAppVersion`).

When the session's data changes on purpose (the fixture, the session's steps, or what the core writes), record all three files again together, from one CI run: `session-hashes.txt`, `windows-backup.zip` and `linux-backup.zip`. Restore copies `authors.json` and `citations.json` out of a backup unchanged, so an old checked-in backup restores the old bytes and fails against a newly recorded `restored/` part.

When a list differs from the golden one, the failing test names the attachment that holds the files it was made from (`session-files-<system>.zip`, or `restored-from-<system>-on-<system>.zip`): diff the differing file between the two runners' artifacts.
