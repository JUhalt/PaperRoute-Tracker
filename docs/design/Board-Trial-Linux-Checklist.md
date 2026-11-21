# Board trial: Linux checklist

This checklist answers the plan's question 5: **does the Board trial start on Linux?**
A person follows it once, on a real Ubuntu 24.04 desktop. Each step has one action.
Under each step, "Note:" says what to write down.

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

Open PowerShell in the repository folder. Run:

```powershell
dotnet publish PaperRoute.Desktop/PaperRoute.Desktop.csproj --configuration Release --runtime linux-x64 --self-contained --output publish-linux-x64
```

What it produced when it was tried for this checklist (2026-11-21, commit `f9fb22e`):

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

Copy the whole `publish-linux-x64` folder to your home folder on Ubuntu, for example to
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

Note: every line the command printed, or "nothing printed".

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
  repository folder, run:

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
  folder reached through a drive letter made with `subst` or a mapped drive. So the choice of
  folder is yours: name only a test library.

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
2. "Your browser opens example.org now."
3. "The trial starts a second copy of itself now and waits for it."

Then it writes the results to a JSON file, closes the window and ends.

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
you chose; it does not record their names.

Note: whether a dialog opened, what it looked like (the GNOME file chooser, or another), and
whether you chose a file or cancelled.

### E4. Watch the browser

A browser window or tab with example.org should open. You may close it.

Note: whether the browser opened, and which browser.

### E5. Wait for the second copy

The trial starts a second copy of itself with `--self-check` and waits up to two minutes.
The second copy opens no window.

Note: how long the wait seemed, and whether the window closed by itself at the end.

### E6. Read the exit code and the file

```bash
cat "$HOME/trial-results/platform-check.json"
```

The exit code is 0 when the check ran to its end, whatever each result was. It is 1 when the
check itself broke, and 2 when the command line or the file's place was refused (nothing was
opened then).

What each result in the file means. Each one has `tried`, `ok`, `detail`, and
`milliseconds`; when something threw an error, also `exceptionType` and `exceptionMessage`.

- `fileDialog`
  - `tried: true, ok: true`: the system's file dialog opened and came back. `itemsChosen`
    says how many items you chose (0 means you cancelled).
  - `tried: true, ok: false`: the dialog threw an error. Read `exceptionType` and
    `exceptionMessage`.
  - `tried: false, ok: false`: Avalonia reports no file dialog on this system, so none was
    opened.
- `launcher`
  - `ok: true`: the system reports that it opened https://example.org.
  - `ok: false`: the system reports that it could not open it, or the launcher threw an error.
    If the browser did open anyway, write that down: it matters.
- `restart`
  - `ok: true, exitCode: 0`: a second copy started from the program file and its self-check
    passed.
  - `ok: false` with an `exitCode` other than 0: the second copy started, but its self-check
    did not pass.
  - `ok: false, stoppedAfterLimit: true`: the second copy did not end within two minutes and
    the trial stopped it.
  - `ok: false` with `exceptionType`: the second copy could not be started.
  - `tried: false, ok: null`: a restart was not tried. This happens when the trial was
    started as `dotnet PaperRoute.Desktop.dll`, which has no program file of its own to start
    again. Start `./PaperRoute.Desktop` instead.
  - `startedFrom` says where the program came from: `APPIMAGE` (from an AppImage, Part F) or
    `process path` (the program file in the folder). `program` gives only the file's name.

The file also holds facts about the system: `operatingSystem`, `runtimeIdentifier`,
`processArchitecture`, `appImageSet` (whether the trial ran from an AppImage),
`xdgSessionType` (`x11` or `wayland`), `waylandDisplaySet` and `displaySet` (only whether
these are set, not their values), `renderScaling`, `themeVariant` and `contrastPreference`.
It holds no user name, computer name or file name you chose.

Note: the exit code, and for each of the three results the values of `tried` and `ok`.

## Part F. The same from an AppImage

**Every command in this part is not tried here.** Packing an AppImage needs the AppImage
tool (`appimagetool`), which is a download. The maintainer decides whether to download it
and from where. The steps below say what has to happen; they are not exact commands.

### F1. Get the AppImage tool

**Not tried here:** the maintainer chooses the download.

Note: where the tool came from and its version.

### F2. Make an AppDir

An AppImage is made from a folder (an "AppDir"). It holds:

- the published folder of A1, for example under `usr/bin`;
- a file named `AppRun` that starts `usr/bin/PaperRoute.Desktop` with the arguments it was
  given;
- a `.desktop` file that names the program;
- an icon (the repository's `ManuscriptPipeline/Assets/PaperRoute.png` can serve).

**Not tried here.** The exact layout that `appimagetool` accepts was not checked.

Note: the files you put in the AppDir.

### F3. Pack the AppImage

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
say `startedFrom: APPIMAGE`.

Note: the same as in E3 to E6.

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
| E exit code | |
| E fileDialog tried / ok | |
| E launcher tried / ok | |
| E restart tried / ok | |
| F4 AppImage started without libfuse2? | |
| F5 platform check from the AppImage | |
