# Board trial: Linux checklist

This checklist answers the plan's question 5: **does the Board trial start on Linux?**
A person follows it once, on a real Ubuntu 24.04 desktop. Most steps have one action; where
a step has more, they are listed in the order to do them. Under each step, "Note:" says
what to write down.

The trial's window is made with Avalonia, a toolkit for windows that runs on Windows, Linux
and macOS. Avalonia's name appears in a few results below.

Some commands were not tried by the person who wrote this checklist. They are marked
**not tried here**, with the reason. If such a command does not work, write down what
happened and go on.

## What this checklist answers

- Whether the trial, published for Linux, starts from a folder on Ubuntu 24.04.
- Whether the window shows the example library, its text and its icons.
- Whether `--self-check` passes on Linux.
- Whether `--snapshot-from` opens a copy of a test library on Linux.
- Whether the system's file dialog, the system's launcher and a restart work from the trial
  (`--platform-check`).
- Whether the trial, packed as an AppImage, starts on a machine without `libfuse2`.

## What it does not answer

- It does not test the Windows Forms app. Only the Avalonia Board trial runs on Linux.
- It does not measure speed. Timings on a virtual machine say little about a real PC.
- It does not test other Linux systems (Fedora, Debian, and others), or macOS.
- It does not use your own PaperRoute library. Never point the trial at it.

## What is needed

- A Windows PC with the repository and the .NET SDK the repository pins (`global.json`).
- An Ubuntu 24.04 desktop. A VirtualBox guest is fine. Use a desktop session, not a server
  without a screen.
- On Ubuntu, **no `libfuse2` installed.** The plan wants to know whether an AppImage starts
  without it. Do not install it during this checklist.
- A way to copy a folder from Windows to Ubuntu, for example a VirtualBox shared folder or a
  USB drive.
- About one hour.

## Part A. Get the trial onto the machine

### A1. Publish the trial for Linux on the Windows PC

Open PowerShell in the repository folder. First, in the same PowerShell window and before
any `dotnet` command in this checklist, turn off the build telemetry that Avalonia's build
tools send otherwise (the repository's CI sets the same variable):

```powershell
$env:AVALONIA_TELEMETRY_OPTOUT = "1"
```

Then run:

```powershell
dotnet publish PaperRoute.Desktop/PaperRoute.Desktop.csproj --configuration Release --runtime linux-x64 --self-contained --output ..\board-trial-publish-linux-x64
```

The output goes beside the repository folder, not inside it: the repository's `.gitignore`
does not cover a published folder inside it, so a later commit could pick one up.

What it produced when it was tried for this checklist (2026-11-21, commit `f9fb22e`, with
the output in a scratch folder outside the repository):

- The publish ended with exit code 0, in about 2 minutes, with 5 warnings and no errors. All 5
  warnings are CA1416 in the core (`ProtectedKeyStore.vb` and `SingleInstanceService.vb`
  call Windows-only APIs). The trial's code references neither class: its list of allowed
  core references (`PaperRoute.Desktop.Tests/CoreSurface/allowed-core-references.txt`) names
  neither.
- The folder held 237 files, 124.5 MB in all.
- The program file is **`PaperRoute.Desktop`**, with no extension (78,192 bytes). Beside it
  are `PaperRoute.Desktop.dll`, `PaperRoute.ViewModels.dll`, `PaperRoute.Core.dll`, the .NET
  runtime's own files (such as `libcoreclr.so`), and Avalonia's drawing libraries
  `libSkiaSharp.so` and `libHarfBuzzSharp.so`.
- The folder was published into a scratch folder and was not started: there is no Linux
  machine here.

Note: whether the publish ended without errors, the folder's size, and the name of the
program file.

### A2. Copy the folder to Ubuntu

Copy the whole `board-trial-publish-linux-x64` folder to your home folder on Ubuntu, for example to
`~/board-trial`. Copy the whole folder, not single files.

**Not tried here:** how you copy depends on your setup (shared folder, USB drive, network).

Note: how you copied the folder.

### A3. Check that `libfuse2` is not installed

On Ubuntu, open a terminal. Run:

```bash
dpkg -l | grep -i fuse
```

**Not tried here** (no Ubuntu machine was used to write this checklist). On Ubuntu 24.04 the
FUSE 2 library may be listed under a name such as `libfuse2t64`; this name was not checked.

How to read the lines: `libfuse2` is installed if a line starts with `ii` and names
`libfuse2` or `libfuse2t64`. Lines that name `fuse3` or `libfuse3-3` do not count: that is
FUSE 3, a different library. If `libfuse2` is installed, **do not remove it**. Write that
down, go on, and treat the result of F4 as "with libfuse2".

Note: every line the command printed, or "nothing printed", and whether `libfuse2` is
installed by the rule above.

### A4. Note the system

Run:

```bash
lsb_release -a; echo "$XDG_SESSION_TYPE"
```

**Not tried here.**

Note: the Ubuntu version, and whether the session is `x11` or `wayland`.

## Part B. Start the trial from the folder

### B1. Go to the folder

```bash
cd ~/board-trial
```

### B2. Make the program file executable

A copy from Windows usually loses the "execute" permission. Set it again:

```bash
chmod +x PaperRoute.Desktop
```

**Not tried here.**

Note: whether the command printed an error.

### B3. Start the trial

```bash
./PaperRoute.Desktop
```

**Not tried here.**

Note: whether a window opened. If it did not, copy every line the terminal printed.

If the trial stops with an error that names `ManagedLibraryService` or a Documents folder,
check whether your home folder has a `Documents` folder (`ls ~`). Issue #395 found that the
core throws when there is no `~/Documents`, as on a CI runner. A normal Ubuntu desktop has
one. Write down what you found.

### B4. Look at the window

Look at the window for one minute. Do not change anything yet.

Note, one line each:

- Does the window title say "PaperRoute Board Trial - Example Library"?
- Does the banner say this is the example library?
- How many manuscripts does the Board show in all? The example holds **7**.
  The line under the brand in the rail should say "7 manuscripts".
- Does the notice strip at the bottom say "Loaded 7 manuscripts from local storage."?
- Is the text clear and of a normal size? Are any letters missing or shown as boxes?
- Do the rail's icons and the logo show, or are there empty squares?
- Is the window light or dark, and does that match your Ubuntu theme?

### B5. Try the Board

Click the Published tab, type a word in the search box, choose a stage in the stage list,
then clear the search.

Note: anything that looked wrong or did not respond.

### B6. Close the window

Close the window with its close button.

Note: whether the terminal shows the prompt again, and any lines printed when it closed.

## Part C. Self-check

### C1. Run the self-check

This mode opens no window. It loads the example, checks where the trial stores its files,
and ends.

```bash
./PaperRoute.Desktop --self-check; echo "exit code: $?"
```

**Not tried here** on Linux. On Windows and in CI, this mode ends with the line
"self-check passed" and exit code 0.

Note: the last two lines printed, and every line that starts with `FAIL`.

## Part D. A copy of a test library (`--snapshot-from`)

The trial can show a read-only copy of a library folder. It copies the folder into a
temporary folder of its own and shows the copy. It never changes the folder you name.

**Use only a test library.** A test library comes from one of these places:

- The manual test that writes the 500-manuscript library. On the Windows PC, in the
  repository folder, in a PowerShell window where `$env:AVALONIA_TELEMETRY_OPTOUT = "1"` was
  set (see A1; set it again in a new window), run:

  ```powershell
  dotnet test PaperRoute.Desktop.Tests/PaperRoute.Desktop.Tests.csproj --configuration Release --filter "FullyQualifiedName~WriteLargeLibraryForMeasurement"
  ```

  It writes the library into `TestResults\v11-trial\large-500` in the repository.
  **Not tried here:** this test was not run while writing this checklist.
- Any other folder that holds a `data` folder and is **not** your own library.

What the trial refuses, and what it cannot refuse:

- It refuses PaperRoute's own default storage folders, and any folder inside them, before it
  reads anything there.
- It **cannot** refuse a library that its owner moved out of the default folders, because
  finding it would mean reading the real settings. On Windows it also cannot refuse a default
  folder reached through a drive letter made with `subst` or a mapped drive. On Linux, in the
  same way, it may not recognize a default folder reached through a bind mount or a mounted
  share (this was not tried). So the choice of folder is yours: name only a test library.

### D1. Copy the test library to Ubuntu

Copy the `large-500` folder to Ubuntu, for example to `~/trial-libraries/large-500`.

Note: how you copied it.

### D2. Start the trial on the copy

```bash
./PaperRoute.Desktop --snapshot-from "$HOME/trial-libraries/large-500"
```

**Not tried here.**

Note:

- Does the window title name the folder?
- Does the banner say "Read-only snapshot of the library in ..." with a time?
- How many manuscripts does the rail show? The 500 library holds 500: 167 in the Pipeline,
  272 Published and 61 in the File Drawer.
- Does scrolling the Published shelf feel smooth or slow? (No numbers are needed.)

### D3. Close the window

Note: whether it closed normally.

## Part E. The platform check (`--platform-check`)

This mode opens the trial's window on the example library and then tries three things, one
after the other. Before each one, the notice strip at the bottom of the window, and the
terminal, say what happens next:

1. "A file dialog opens now. Choose any file, or cancel. Nothing you choose is opened."
2. "The trial asks the system to open example.org in your browser now."
3. "The trial starts a second copy of itself now and waits for it to finish. No second window
   opens."

Then it writes the results to a JSON file, closes the window and ends.

No step has a time limit of its own except the second copy (two minutes). If a dialog or a
browser never comes back, closing the trial's window is the way out: the trial then writes
what it learnt so far to the same JSON file, with the step that was still running, stops a
second copy that is still running, and ends with exit code 1.

The JSON file must not exist yet, unless an earlier platform check wrote it: the trial
refuses to replace any other file. Use a new file name for each run.

### E1. Make a folder for the result

The JSON file must go into a folder that exists and is not one of PaperRoute's own folders.

```bash
mkdir -p "$HOME/trial-results"
```

### E2. Start the check

```bash
./PaperRoute.Desktop --platform-check "$HOME/trial-results/platform-check.json"; echo "exit code: $?"
```

**Not tried here:** no real window, dialog or browser was opened while writing this
checklist. The mode was tested only with stand-ins.

### E3. Answer the file dialog

When the file dialog opens, choose any file, or press Cancel. The trial does not open or
read what you choose. The JSON records only that the dialog came back and how many items
you chose; it does not record their names. If the dialog fails, the JSON records only the
kind of error, not its message, because a message may name the file you chose.

If nothing comes back after a minute, close the trial's window, note that it was on the
file dialog, and keep the terminal lines.

Note: whether the notice strip showed the first sentence above; whether the Board showed the
7 manuscripts of the example behind the dialog (the rail says "7 manuscripts"); whether a
dialog opened, what it looked like (the GNOME file chooser, or another); and whether you
chose a file or cancelled.

### E4. Watch the browser

A browser window or tab with example.org should open. You may close it.

If nothing happens after a minute and the trial's window does not move on, close the
trial's window, note that it was on the browser, and keep the terminal lines.

Note: whether the notice strip showed the second sentence; whether a page with example.org
appeared, and in which browser. Only this note tells whether a page appeared: the JSON can
say only whether the system accepted the request.

### E5. Wait for the second copy

The trial starts a second copy of itself with `--self-check` and waits up to two minutes.
The second copy opens no window. After two minutes the trial stops it by itself.

If the window is still open a minute after the two minutes, close it, note that it was on
the second copy, and keep the terminal lines.

Note: whether the notice strip showed the third sentence; how long the wait seemed; and
whether the window closed by itself at the end.

### E6. Read the exit code and the file

```bash
cat "$HOME/trial-results/platform-check.json"
```

The exit code is the last line E2 printed, after the window closed ("exit code: ..."). It is
0 when the check ran to its end, whatever each result was. It is 1 when the check itself
broke or the window was closed before the end, and 2 when the command line or the file's
place was refused (nothing was opened then).

If the window was closed before the end, the file has `error` ("The window was closed before
the check finished.") and `stepRunning` (`load`, `fileDialog`, `launcher` or `restart`): the
step that never came back. That step has `tried: true, ok: null`, and the steps after it
`tried: false` ("Not reached").

What each result in the file means. Each one has `tried`, `ok`, `detail`, and
`milliseconds`; when something threw an error, also `exceptionType`, and for the launcher
and the restart `exceptionMessage`.

- `fileDialog`
  - `tried: true, ok: true`: the system's file dialog opened and came back. `itemsChosen`
    says how many items you chose (0 means you cancelled).
  - `tried: true, ok: false`: the dialog threw an error. Read `exceptionType`. There is no
    `exceptionMessage` for the dialog: a message may name the file you chose, so it is not
    kept.
  - `tried: false, ok: false`: Avalonia reports no file dialog on this system, so none was
    opened.
- `launcher`
  - `ok: true`: the system accepted the request to open https://example.org. This does not
    show that a page appeared: only your note from E4 tells that.
  - `ok: false`: the system reports that it could not open it, or the launcher threw an error.
    If the browser did open anyway, write that down: it matters.
- `restart`
  - `ok: true, exitCode: 0`: a second copy started from the program file and its self-check
    passed.
  - `ok: false, exitCode: 1`: the second copy started, but its self-check did not pass.
  - `ok: false, exitCode: 2`: the second copy refused its command line or folder, so its
    self-check did not run.
  - `ok: false` with any other `exitCode`: the trial's self-check exits only 0 or 1, so this
    code came from somewhere else, for example the AppImage runtime (one that cannot use
    FUSE) or the .NET runtime, before the self-check finished.
  - `ok: false, stoppedAfterLimit: true`: the second copy did not end within two minutes and
    the trial stopped it.
  - `ok: false` with `exceptionType`: the second copy could not be started, or waiting for it
    failed (then it was stopped).
  - `tried: false, ok: null`: a restart was not tried. This happens when the trial was
    started as `dotnet PaperRoute.Desktop.dll`, which has no program file of its own to start
    again. Start `./PaperRoute.Desktop` instead.
  - `startedFrom` says where the program came from: `APPIMAGE` (the AppImage this trial runs
    from, Part F) or `process path` (the program file in the folder). `program` gives only the
    file's name. If `detail` ends with "APPIMAGE is set, but this process is not running from
    it ...", the terminal had an `APPIMAGE` variable from another program (for example a
    terminal opened from an AppImage editor), and the trial ignored it.

The file also says which trial ran: `trialVersion` (the trial's version, a `+`, and the
hash of the commit it was built from) and `build` (`Release` or `Debug`). And it holds facts
about the system: `operatingSystem`, `runtimeIdentifier`,
`processArchitecture`, `appImageSet` (whether the `APPIMAGE` variable was set; whether the
trial ran from it is the restart's `startedFrom`), `xdgSessionType` (`x11` or `wayland`),
`waylandDisplaySet` and `displaySet` (only whether these are set, not their values),
`renderScaling`, `themeVariant` and `contrastPreference`. It holds no user name, computer
name or name of a file you chose.

Note: the exit code, and for each of the three results the values of `tried` and `ok`.

## Part F. The same from an AppImage

**Every command in this part is not tried here.** Packing an AppImage needs the AppImage
tool (`appimagetool`), which is a download. The maintainer decides whether to download it
and from where.

**Who does what.** F1 to F3 are the maintainer's: the maintainer packs the AppImage, on a
machine of their choice, before handing it over. They are not exact commands, only what has
to happen. **A tester starts at F4** with the AppImage file the maintainer gave. If no
AppImage was given, skip Part F and go to Part G.

### F1. Get the AppImage tool (maintainer)

**Not tried here:** the maintainer chooses the download.

Note: where the tool came from and its version.

### F2. Make an AppDir (maintainer)

An AppImage is made from a folder (an "AppDir"). It holds:

- the published folder of A1, for example under `usr/bin`;
- a file named `AppRun` that starts `usr/bin/PaperRoute.Desktop` with the arguments it was
  given;
- a `.desktop` file that names the program;
- an icon (the repository's `ManuscriptPipeline/Assets/PaperRoute.png` can serve).

**Not tried here.** The exact layout that `appimagetool` accepts was not checked.

Note: the files you put in the AppDir.

### F3. Pack the AppImage (maintainer)

Run the AppImage tool on the AppDir. **Not tried here:** its options were not checked.

Note: the name of the AppImage file, and whether the tool printed warnings.

### F4. Make it executable and start it

```bash
chmod +x <the AppImage file>
./<the AppImage file>
```

**Not tried here.** If it fails with a message about FUSE, that answers part of question 5:
write the message down exactly. Some AppImages can be run without FUSE by unpacking them
first (an option named `--appimage-extract-and-run`); whether this AppImage accepts it was
not checked. Try it only after you have noted the first result.

Note: whether the window opened, and every line the terminal printed.

### F5. Run the platform check from the AppImage

```bash
./<the AppImage file> --platform-check "$HOME/trial-results/platform-check-appimage.json"; echo "exit code: $?"
```

**Not tried here.** This time the JSON should say `appImageSet: true`, and the restart should
say `startedFrom: APPIMAGE`. The trial takes the AppImage as the program to restart only when
the program it runs lies inside the folder where the AppImage was mounted (the `APPDIR`
variable the AppImage runtime sets); if the restart says `startedFrom: process path` with the
note "APPIMAGE is set, but this process is not running from it", write that down.

If F4 needed `--appimage-extract-and-run`, give it here as well, before `--platform-check`.
The restart will not pass that option on to the second copy, so the second copy may fail to
start in the same way F4 first did (an `exitCode` other than 0, 1 or 2). Setting
`APPIMAGE_EXTRACT_AND_RUN=1` at the start of the command instead
(`APPIMAGE_EXTRACT_AND_RUN=1 ./<the AppImage file> --platform-check ...`) would reach the
second copy too; **not checked here** whether this AppImage honours it. If you try it, run it
as a second check with a new file name, after the first.

Note: the same as in E3 to E6, and whether you gave `--appimage-extract-and-run` or
`APPIMAGE_EXTRACT_AND_RUN=1`.

## Part G. What to send back

Send these to the maintainer:

1. The JSON file `platform-check.json` from Part E.
2. The JSON file `platform-check-appimage.json` from Part F, if you got that far.
3. Your notes from every step, with the step's number (A1, A2, ...).
4. Any lines the terminal printed that looked like errors, copied as text.
5. If you can, one screenshot of the window from B4.

Before you send them, read them once. They should not hold your name, your computer's name
or paths in your home folder; the JSON files are written without them. Your own notes and
the terminal lines may hold them; remove them if you wish.

## Result sheet

| Step | Result |
|---|---|
| A1 publish | |
| A3 libfuse2 present? | |
| A4 Ubuntu version, session type | |
| B3 window opened? | |
| B4 title, banner, 7 manuscripts, text, icons, theme | |
| B5 Board responds? | |
| C1 self-check exit code | |
| D2 snapshot opened, 500 manuscripts? | |
| E exit code (window closed early? on which step?) | |
| E3 Board showed 7 manuscripts behind the dialog? | |
| E4 a page with example.org appeared? | |
| E fileDialog tried / ok | |
| E launcher tried / ok | |
| E restart tried / ok | |
| F4 AppImage started without libfuse2? | |
| F5 platform check from the AppImage | |
