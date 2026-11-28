# Shows the Board trial in a real window on a macOS CI runner and keeps what it
# saw: pictures of the screen, the trial's own result files, its terminal lines,
# and a table of facts. It is the macOS partner of Show-TrialWindowOnLinux.ps1.
#
# A hosted macOS runner has a logged-in desktop, so no virtual display is
# needed. The windows are found, closed and answered through System Events
# (AppleScript), which needs the Accessibility permission; GitHub's runner
# images grant it to the runner's agent, to bash and to osascript
# (actions/runner-images, images/macos/scripts/build/configure-tccdb-macos.sh).
# When System Events cannot be used, the window server's list of windows is
# read instead (JavaScript for Automation and CoreGraphics), which needs no
# permission; then the window can be seen but not closed by its close button.
#
# One part runs per call, so that each one is its own step in the workflow:
#   -Part Window         the example library in a real window: wait for the
#                        window, take a picture, close it by its close button
#   -Part Measure        --snapshot-from the 500 test library and --measure in
#                        the real window; the timings are recorded, never judged
#   -Part PlatformCheck  --platform-check: the file dialog (answered with
#                        Escape), the launcher, a second copy; recorded
#
# Every outcome is recorded in <Out>/<part>.md and in the step summary. The
# script ends with exit code 1 when its part did not work, and 0 when it did.
# The macOS job is report-only: its steps carry continue-on-error, so an exit
# code of 1 marks the step in the job's summary file and never fails the job.
#
# -Library must be the 500-manuscript TEST library that the manual test
# WriteLargeLibraryForMeasurement writes: a folder whose path ends with
# TestResults/v11-trial/large-500. Any other folder is refused, so that a
# picture of a person's own Board can never land in the results.
#
# On a CI runner the script also quits Safari, which the platform check's
# launcher may have opened. It does that only where GITHUB_ACTIONS is set, never
# on a person's own Mac.
[CmdletBinding()]
param(
    # The published trial's program file: <folder>/PaperRoute.Desktop
    [Parameter(Mandatory)] [string] $Program,
    # A folder for the pictures, the results and the logs. It is created.
    [Parameter(Mandatory)] [string] $Out,
    [Parameter(Mandatory)] [ValidateSet('Window', 'Measure', 'PlatformCheck')] [string] $Part,
    # The 500 test library for -Part Measure.
    [string] $Library
)

$ErrorActionPreference = 'Continue'
# The main window's title starts with this and goes on with " - <library>".
$MainTitle = '^PaperRoute Tracker( - |$)'
$LibraryEnding = 'TestResults/v11-trial/large-500'

New-Item -ItemType Directory -Force -Path $Out | Out-Null
$Out = (Resolve-Path -LiteralPath $Out).Path
$tag = $Part.ToLowerInvariant()
$toolLog = Join-Path $Out "$tag-tools-stderr.txt"
$facts = [ordered]@{}
$failures = [System.Collections.Generic.List[string]]::new()
$script:ToolExitCode = 0

function Note([string] $key, $value) {
    $facts[$key] = $value
    Write-Host "${key}: $value"
}

# Runs a tool and returns its output lines; what it writes to its error stream is
# kept in a log, and its exit code in $script:ToolExitCode. A missing tool or a
# failure gives an empty list.
function Invoke-Tool([string] $name, [string[]] $arguments) {
    try {
        $lines = @(& $name @arguments 2>> $toolLog)
        $script:ToolExitCode = $LASTEXITCODE
        return $lines
    }
    catch {
        Add-Content -LiteralPath $toolLog -Value "$name : $($_.Exception.Message)"
        $script:ToolExitCode = -1
        return @()
    }
}

# The small scripts System Events and the window server are asked through.
$scripts = Join-Path ([IO.Path]::GetTempPath()) "board-trial-mac-$([Guid]::NewGuid().ToString('N'))"
New-Item -ItemType Directory -Force -Path $scripts | Out-Null

# Each window of the process: name, subrole, size and the number of sheets on it.
$ListScript = Join-Path $scripts 'list-windows.applescript'
@'
on run argv
    set thePid to (item 1 of argv) as integer
    set out to {}
    tell application "System Events"
        set found to (every process whose unix id is thePid)
        if (count of found) is 0 then return ""
        repeat with w in (every window of (item 1 of found))
            set theName to ""
            set theRole to ""
            set theSize to ""
            set theSheets to 0
            try
                set theName to (name of w) as text
            end try
            try
                set theRole to (subrole of w) as text
            end try
            try
                set s to size of w
                set theSize to ((item 1 of s) as text) & "x" & ((item 2 of s) as text)
            end try
            try
                set theSheets to count of sheets of w
            end try
            set end of out to theName & tab & theRole & tab & theSize & tab & (theSheets as text)
        end repeat
    end tell
    set AppleScript's text item delimiters to linefeed
    return out as text
end run
'@ | Set-Content -LiteralPath $ListScript -Encoding utf8NoBOM

# The same from the window server, which needs no Accessibility permission:
# the process's normal windows (layer 0) with their names and sizes. A name is
# empty when the caller may not record the screen.
$ServerScript = Join-Path $scripts 'list-windows.js'
@'
ObjC.import('CoreGraphics');
function run(argv) {
    var pid = parseInt(argv[0], 10);
    var list = ObjC.deepUnwrap(ObjC.castRefToObject($.CGWindowListCopyWindowInfo(1, 0)));
    var out = [];
    for (var i = 0; i < list.length; i++) {
        var w = list[i];
        if (w.kCGWindowOwnerPID !== pid || w.kCGWindowLayer !== 0) continue;
        var b = w.kCGWindowBounds || {};
        out.push((w.kCGWindowName || '') + '\t\t' + b.Width + 'x' + b.Height + '\t0');
    }
    return out.join('\n');
}
'@ | Set-Content -LiteralPath $ServerScript -Encoding utf8NoBOM

# Clicks the close button of each main window, as a person would.
$CloseScript = Join-Path $scripts 'close-window.applescript'
@'
on run argv
    set thePid to (item 1 of argv) as integer
    set closed to 0
    tell application "System Events"
        set p to first process whose unix id is thePid
        set frontmost of p to true
        repeat with w in (every window of p)
            try
                if ((name of w) as text) starts with "PaperRoute Tracker" then
                    click (first button of w whose subrole is "AXCloseButton")
                    set closed to closed + 1
                end if
            end try
        end repeat
    end tell
    return closed as text
end run
'@ | Set-Content -LiteralPath $CloseScript -Encoding utf8NoBOM

# Brings the process to the front and presses one key: Escape (key code 53),
# or Command-Q.
$KeyScript = Join-Path $scripts 'press-key.applescript'
@'
on run argv
    set thePid to (item 1 of argv) as integer
    tell application "System Events"
        set frontmost of (first process whose unix id is thePid) to true
        delay 0.5
        if (item 2 of argv) is "quit" then
            keystroke "q" using command down
        else
            key code 53
        end if
    end tell
    return "sent"
end run
'@ | Set-Content -LiteralPath $KeyScript -Encoding utf8NoBOM

$script:WindowSource = $null
# The windows of the process, from System Events, else from the window server.
function Get-TrialWindows($process) {
    $lines = Invoke-Tool 'osascript' @($ListScript, "$($process.Id)")
    if ($script:ToolExitCode -eq 0) { $source = 'System Events' }
    else {
        $lines = Invoke-Tool 'osascript' @('-l', 'JavaScript', $ServerScript, "$($process.Id)")
        $source = if ($script:ToolExitCode -eq 0) { 'the window server' } else { $null }
    }
    if ($null -ne $source) { $script:WindowSource = $source }
    @($lines | Where-Object { $_ } | ForEach-Object {
            $cells = "$_" -split "`t"
            [pscustomobject]@{
                Name    = $cells[0]
                Subrole = if ($cells.Count -gt 1) { $cells[1] } else { '' }
                Size    = if ($cells.Count -gt 2) { $cells[2] } else { '' }
                Sheets  = if ($cells.Count -gt 3) { [int] $cells[3] } else { 0 }
            }
        })
}

# The main windows among them. A window with no name (the window server gives
# none without the screen recording permission) counts, and the title stays unknown.
function Get-MainWindows($process) {
    @(Get-TrialWindows $process | Where-Object { $_.Name -match $MainTitle -or ($script:WindowSource -eq 'the window server' -and -not $_.Name) })
}

function Save-Screen([string] $name) {
    $file = Join-Path $Out $name
    Remove-Item -LiteralPath $file -ErrorAction SilentlyContinue
    Invoke-Tool 'screencapture' @('-x', $file) | Out-Null
    return (Test-Path -LiteralPath $file) -and (Get-Item -LiteralPath $file).Length -gt 0
}

# Starts the trial with its terminal lines kept in two files.
function Start-Trial([string] $name, [string[]] $arguments) {
    $parameters = @{
        FilePath               = $Program
        PassThru               = $true
        RedirectStandardOutput = (Join-Path $Out "$name-stdout.txt")
        RedirectStandardError  = (Join-Path $Out "$name-stderr.txt")
    }
    if ($arguments.Count -gt 0) { $parameters.ArgumentList = $arguments }
    Start-Process @parameters
}

function Wait-Exit($process, [int] $seconds) {
    if ($null -eq $process) { return $true }
    return $process.WaitForExit($seconds * 1000)
}

function Stop-Hard($process) {
    if ($null -ne $process -and -not $process.HasExited) { try { $process.Kill($true) } catch { } }
}

# Waits up to $seconds for a main window. Returns the windows found, or none.
function Wait-MainWindow($process, [int] $seconds) {
    $watch = [Diagnostics.Stopwatch]::StartNew()
    while ($watch.Elapsed.TotalSeconds -lt $seconds -and -not $process.HasExited) {
        $found = @(Get-MainWindows $process)
        if ($found.Count -gt 0) { return $found }
        Start-Sleep -Milliseconds 500
    }
    return @()
}

# Asks the main window to close: its close button first, then Command-Q, as a
# person would. Returns how it was asked, or $null when neither could be sent.
function Close-MainWindow($process) {
    $clicked = @(Invoke-Tool 'osascript' @($CloseScript, "$($process.Id)"))
    if ($script:ToolExitCode -eq 0 -and $clicked.Count -gt 0 -and "$($clicked[0])" -match '^[1-9]') { return 'its close button' }
    $sent = @(Invoke-Tool 'osascript' @($KeyScript, "$($process.Id)", 'quit'))
    if ($script:ToolExitCode -eq 0 -and $sent -contains 'sent') { return 'Command-Q' }
    return $null
}

# Closes a running trial as a person would, and records how it ended.
function Close-AndRecord($process, [string] $what) {
    $how = Close-MainWindow $process
    if ($null -eq $how) {
        Note "$what closed" 'no: neither its close button nor Command-Q could be sent (see the tools log); stopped'
        $failures.Add("the $what could not be asked to close")
        Stop-Hard $process
        return
    }
    if (Wait-Exit $process 30) {
        Note "$what closed" "yes, by $how, exit code $($process.ExitCode)"
        if ($process.ExitCode -ne 0) { $failures.Add("the $what ended with exit code $($process.ExitCode) after $how") }
    }
    else {
        Note "$what closed" "no: asked by $how, still running after 30 s, stopped"
        $failures.Add("the $what did not close within 30 s of $how")
        Stop-Hard $process
    }
}

$trial = $null
try {
    if (-not (Test-Path -LiteralPath $Program)) {
        Note 'program' 'not found at the path given'
        throw 'the program was not found'
    }
    Invoke-Tool 'chmod' @('+x', $Program) | Out-Null
    Note 'program' (Split-Path -Leaf $Program)

    switch ($Part) {
        'Window' {
            Note 'system' "macOS $((Invoke-Tool 'sw_vers' @('-productVersion')) -join ' ') ($((Invoke-Tool 'sw_vers' @('-buildVersion')) -join ' '))"
            Note 'machine' ((Invoke-Tool 'uname' @('-m')) -join ' ')
            $translated = (Invoke-Tool 'sysctl' @('-n', 'sysctl.proc_translated')) -join ''
            Note 'this shell runs under Rosetta' $(if ($translated -eq '1') { 'yes' } else { 'no' })
            $display = @(Invoke-Tool 'system_profiler' @('SPDisplaysDataType') | Where-Object { $_ -match 'Resolution|UI Looks like' } | ForEach-Object { $_.Trim() })
            Note 'display' $(if ($display.Count -gt 0) { $display -join '; ' } else { 'not reported' })
            Invoke-Tool 'codesign' @('--verify', '--verbose=1', $Program) | Out-Null
            Note 'program signature' $(if ($script:ToolExitCode -eq 0) { 'valid' } else { "not valid (codesign exit code $script:ToolExitCode; see the tools log)" })

            $watch = [Diagnostics.Stopwatch]::StartNew()
            $trial = Start-Trial 'example' @()
            $windows = @(Wait-MainWindow $trial 90)
            $opened = $windows.Count -gt 0
            Note 'windows listed by' $(if ($script:WindowSource) { $script:WindowSource } else { 'nothing: neither System Events nor the window server answered' })
            Note 'example window opened' $(if ($opened) { "yes, after $([Math]::Round($watch.Elapsed.TotalSeconds, 1)) s" } elseif ($trial.HasExited) { "no: the trial ended with exit code $($trial.ExitCode)" } else { 'no: no window after 90 s' })
            if ($opened) {
                Start-Sleep -Seconds 5      # the library loads and the first frames are drawn
                $windows = @(Get-MainWindows $trial)
                $titles = @($windows | ForEach-Object Name | Where-Object { $_ } | Select-Object -Unique)
                Note 'example window title' $(if ($titles.Count -gt 0) { $titles -join '; ' } else { 'not known' })
                $sizes = @($windows | ForEach-Object Size | Where-Object { $_ } | Select-Object -Unique)
                if ($sizes.Count -gt 0) { Note 'example window size' ($sizes -join '; ') }
                Note 'picture of the screen' $(if (Save-Screen 'example-window.png') { 'example-window.png' } else { 'not taken (see the tools log)' })
                Close-AndRecord $trial 'example window'
            }
            else {
                Save-Screen 'example-no-window.png' | Out-Null
                $failures.Add('the example window did not open')
            }
        }

        'Measure' {
            if (-not $Library) { throw 'no test library was given' }
            $full = [IO.Path]::GetFullPath($Library).TrimEnd('/')
            if (-not $full.EndsWith($LibraryEnding, [StringComparison]::Ordinal)) {
                Note 'test library' "refused: its path does not end with $LibraryEnding"
                throw 'the library given is not the 500 test library'
            }
            if (-not (Test-Path -LiteralPath (Join-Path $Library 'data'))) {
                Note 'test library' 'not found at the path given'
                throw 'the 500 test library was not found'
            }
            # A run that ends by itself and writes its rows has drawn its frames in the window.
            $measureFile = Join-Path $Out 'measure-macos.json'
            Remove-Item -LiteralPath $measureFile -ErrorAction SilentlyContinue
            $trial = Start-Trial 'measure' @('--snapshot-from', $Library, '--measure', $measureFile)
            $pictured = $false
            $watch = [Diagnostics.Stopwatch]::StartNew()
            while ($watch.Elapsed.TotalSeconds -lt 240 -and -not $trial.HasExited) {
                if (-not $pictured -and @(Get-MainWindows $trial).Count -gt 0) { Start-Sleep -Seconds 2; $pictured = Save-Screen 'measure-window.png' }
                Start-Sleep -Milliseconds 500
            }
            $ended = $trial.HasExited
            Note 'picture of the timed run' $(if ($pictured) { 'measure-window.png' } else { 'not taken' })
            Note 'timed run' $(if ($ended) { "ended after $([Math]::Round($watch.Elapsed.TotalSeconds, 1)) s with exit code $($trial.ExitCode)" } else { 'did not end within 240 s, stopped' })
            if (-not $ended) { $failures.Add('the timed run in the window did not end within 240 s') }
            elseif ($trial.ExitCode -ne 0) { $failures.Add("the timed run in the window ended with exit code $($trial.ExitCode)") }
            Stop-Hard $trial
            $rows = @()
            if (Test-Path -LiteralPath $measureFile) {
                try {
                    $measure = Get-Content -LiteralPath $measureFile -Raw | ConvertFrom-Json
                    $rows = @($measure.rows)
                    Note 'timed rows written' $rows.Count
                    foreach ($id in 'warm-startup', 'first-switch-published', 'search-keystroke', 'scroll-published-longest-step') {
                        $row = @($rows | Where-Object id -eq $id) | Select-Object -Last 1
                        if ($null -ne $row) { Note "timed $id (UI ms / budget; a shared runner, not a verdict)" "$([Math]::Round([double]$row.uiMs, 1)) / $($row.budgetMs)" }
                    }
                } catch { Note 'timed run file' 'written, but it could not be read as JSON' }
            } else { Note 'timed run file' 'not written' }
            if ($rows.Count -eq 0) { $failures.Add('the timed run wrote no rows') }
        }

        'PlatformCheck' {
            # The file dialog, the launcher, a second copy. The outcomes are
            # recorded, never judged; the step fails only when the check does not
            # end by itself with its file.
            $platformFile = Join-Path $Out 'platform-check-macos.json'
            Remove-Item -LiteralPath $platformFile -ErrorAction SilentlyContinue
            $trial = Start-Trial 'platform' @('--platform-check', $platformFile)
            $watch = [Diagnostics.Stopwatch]::StartNew()
            $answered = $false
            while ($watch.Elapsed.TotalSeconds -lt 240 -and -not $trial.HasExited) {
                if (-not $answered) {
                    # The dialog is a second window of the process, or a sheet on its
                    # window. The window server gives no subroles or sheets, and maybe
                    # no names: there a second window is the sign.
                    $all = @(Get-TrialWindows $trial)
                    $dialog = if ($script:WindowSource -eq 'System Events') { @($all | Where-Object { $_.Name -notmatch $MainTitle -or $_.Sheets -gt 0 }) }
                    elseif ($all.Count -gt 1) { @($all | Select-Object -Skip 1) }
                    else { @() }
                    $late = $watch.Elapsed.TotalSeconds -ge 90
                    if ($dialog.Count -gt 0 -or $late) {
                        # The dialog waits for a person. Its picture is taken, then Escape answers it as "cancel".
                        Start-Sleep -Seconds 1
                        Note 'file dialog seen' $(if ($dialog.Count -gt 0) { "yes ($(@($dialog | ForEach-Object { if ($_.Sheets -gt 0) { 'a sheet' } else { "a window, subrole $($_.Subrole)" } }) -join '; '))" } else { 'no window or sheet after 90 s; Escape sent anyway' })
                        Note 'picture of the file dialog' $(if (Save-Screen 'platform-check-dialog.png') { 'platform-check-dialog.png' } else { 'not taken' })
                        $sent = @(Invoke-Tool 'osascript' @($KeyScript, "$($trial.Id)", 'escape'))
                        Note 'Escape sent' $(if ($script:ToolExitCode -eq 0 -and $sent -contains 'sent') { 'yes' } else { 'no (see the tools log)' })
                        $answered = $true
                    }
                }
                Start-Sleep -Milliseconds 500
            }
            if (-not $trial.HasExited) {
                Save-Screen 'platform-check-stuck.png' | Out-Null
                Close-MainWindow $trial | Out-Null      # the trial then writes what it learnt so far
                Wait-Exit $trial 40 | Out-Null
                $failures.Add('the platform check did not end by itself within 240 s')
            }
            Note 'platform check' $(if ($trial.HasExited) { "ended after $([Math]::Round($watch.Elapsed.TotalSeconds, 1)) s with exit code $($trial.ExitCode)" } else { 'did not end, stopped' })
            Stop-Hard $trial
            if (Test-Path -LiteralPath $platformFile) {
                try {
                    $platform = Get-Content -LiteralPath $platformFile -Raw | ConvertFrom-Json
                    foreach ($step in 'fileDialog', 'launcher', 'restart') {
                        $result = $platform.$step
                        if ($null -ne $result) { Note "platform check $step" "tried $($result.tried), ok $(if ($null -eq $result.ok) { 'null' } else { $result.ok }): $($result.detail)" }
                    }
                    foreach ($name in 'operatingSystem', 'runtimeIdentifier', 'renderScaling', 'themeVariant') {
                        Note "platform check $name" $(if ($null -eq $platform.$name) { 'null' } else { $platform.$name })
                    }
                    if ($platform.error) { Note 'platform check error' "$($platform.error) (step running: $($platform.stepRunning))" }
                } catch { Note 'platform check file' 'written, but it could not be read as JSON' }
            }
            else {
                Note 'platform check file' 'not written'
                $failures.Add('the platform check wrote no file')
            }
        }
    }
}
catch {
    Note 'stopped early' $_.Exception.Message
    $failures.Add("stopped early: $($_.Exception.Message)")
}
finally {
    Stop-Hard $trial
    # Safari, which the launcher may have opened on a runner.
    if ($env:GITHUB_ACTIONS) { Invoke-Tool 'pkill' @('-x', 'Safari') | Out-Null }
    Remove-Item -LiteralPath $scripts -Recurse -Force -ErrorAction SilentlyContinue
}

# --- the facts of this part
$titles = @{ Window = 'The example library in a real window'; Measure = 'The timed run in a real window'; PlatformCheck = 'The platform check' }
$summary = [System.Collections.Generic.List[string]]::new()
$summary.Add("### $($titles[$Part]) on macOS")
$summary.Add('')
$summary.Add('| | |')
$summary.Add('|---|---|')
foreach ($key in $facts.Keys) { $summary.Add("| $key | $(("$($facts[$key])") -replace '\|', '/') |") }
$summary.Add('')
if ($failures.Count -gt 0) {
    $summary.Add('Did not work:')
    $summary.Add('')
    foreach ($failure in $failures) { $summary.Add("- $failure") }
    $summary.Add('')
}
$summary | Set-Content -LiteralPath (Join-Path $Out "$tag.md") -Encoding utf8
if ($env:GITHUB_STEP_SUMMARY) { $summary | Add-Content -LiteralPath $env:GITHUB_STEP_SUMMARY -Encoding utf8 }

if ($failures.Count -gt 0) {
    # A warning, not an error: the macOS job is report-only.
    Write-Host "::warning title=Board trial on macOS ($Part)::$($failures -join '; ')"
    exit 1
}
exit 0
