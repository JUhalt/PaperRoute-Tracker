# Shows the Board trial in a real X window on a Linux machine without a desktop
# (a CI runner), under a virtual display (Xvfb) with a window manager, and keeps
# what it saw: pictures of the screen, the trial's own result files, its
# terminal lines, and a summary.
#
# It answers, as far as a machine without a desktop session can, the question
# "does the Board trial start on Linux?" (docs/design/Board-Trial-Linux-Checklist.md).
# What it cannot answer stays with a person on a real Ubuntu desktop: a Wayland
# session, the desktop's own file dialog and browser, and how the window looks
# on a real screen.
#
# Every outcome is recorded. With -RequireWindow the script ends with exit code 1
# when one of these does not hold: the display comes up; the self-check passes;
# the example window opens and, asked to close, ends with exit code 0; and, when
# a test library is given, the timed run in the real window ends by itself and
# writes its rows. The timings themselves and the platform check are recorded,
# never judged. Without -RequireWindow the script always ends with 0.
#
# -Library must be the 500-manuscript TEST library that the manual test
# WriteLargeLibraryForMeasurement writes: a folder whose path ends with
# TestResults/v11-trial/large-500. Any other folder is refused, so that a
# picture of a person's own Board can never land in the results.
#
# On a CI runner the script also stops a browser the platform check may have
# started (by name). It does that only where GITHUB_ACTIONS is set, never on a
# person's own machine.
[CmdletBinding()]
param(
    # The published trial's program file: <folder>/PaperRoute.Desktop
    [Parameter(Mandatory)] [string] $Program,
    # A folder for the pictures, the results and the logs. It is created.
    [Parameter(Mandatory)] [string] $Out,
    # The 500 test library for --snapshot-from and --measure. Left out: those steps are skipped.
    [string] $Library,
    [switch] $RequireWindow,
    [string] $Display = ':99'
)

$ErrorActionPreference = 'Continue'
# The main window's title starts with this and goes on with " - <library>"; the
# platform check's file dialog starts with the same words and goes on differently.
$MainTitle = '^PaperRoute Board Trial( - |$)'
$DialogTitle = 'platform check: choose any file'
$LibraryEnding = 'TestResults/v11-trial/large-500'

New-Item -ItemType Directory -Force -Path $Out | Out-Null
$Out = (Resolve-Path -LiteralPath $Out).Path
$toolLog = Join-Path $Out 'tools-stderr.txt'
$facts = [ordered]@{}
$failures = [System.Collections.Generic.List[string]]::new()

function Note([string] $key, $value) {
    $facts[$key] = $value
    Write-Host "${key}: $value"
}

# Runs a tool and returns its output lines; what it writes to its error stream is
# kept in a log. A missing tool or a failure gives an empty list.
function Invoke-Tool([string] $name, [string[]] $arguments) {
    try { return @(& $name @arguments 2>> $toolLog) } catch { Add-Content -LiteralPath $toolLog -Value "$name : $($_.Exception.Message)"; return @() }
}

# The ids of the visible windows whose title matches the pattern. Callers wrap the
# call in @(...): a function that returns one item hands back the item itself.
function Find-Windows([string] $pattern) {
    @(Invoke-Tool 'xdotool' @('search', '--onlyvisible', '--name', $pattern) | Where-Object { $_ -match '^\d+$' })
}

function Save-Screen([string] $name) {
    $file = Join-Path $Out $name
    Remove-Item -LiteralPath $file -ErrorAction SilentlyContinue
    Invoke-Tool 'import' @('-window', 'root', $file) | Out-Null
    return (Test-Path -LiteralPath $file)
}

# Starts the trial with its terminal lines kept in two files.
function Start-Trial([string] $tag, [string[]] $arguments) {
    $parameters = @{
        FilePath               = $Program
        PassThru               = $true
        RedirectStandardOutput = (Join-Path $Out "$tag-stdout.txt")
        RedirectStandardError  = (Join-Path $Out "$tag-stderr.txt")
    }
    if ($arguments.Count -gt 0) { $parameters.ArgumentList = $arguments }
    Start-Process @parameters
}

function Wait-Exit($process, [int] $seconds) {
    if ($null -eq $process) { return $true }
    return $process.WaitForExit($seconds * 1000)
}

# Asks the main window to close as a person's click on its close button would.
function Close-MainWindow {
    foreach ($id in @(Find-Windows $MainTitle)) { Invoke-Tool 'wmctrl' @('-i', '-c', "$id") | Out-Null }
}

function Stop-Hard($process) {
    if ($null -ne $process -and -not $process.HasExited) { try { $process.Kill($true) } catch { } }
}

# Runs the platform check once, answers its file dialog with Escape, and notes its
# outcomes and facts under the given label. Recorded, never judged.
function Invoke-PlatformCheck([string] $label, [string] $tag, [string] $pictures, [string] $fileName) {
    $platformFile = Join-Path $Out $fileName
    Remove-Item -LiteralPath $platformFile -ErrorAction SilentlyContinue
    $trial = Start-Trial $tag @('--platform-check', $platformFile)
    $watch = [Diagnostics.Stopwatch]::StartNew()
    $answered = $false
    while ($watch.Elapsed.TotalSeconds -lt 240 -and -not $trial.HasExited) {
        if (-not $answered -and $watch.Elapsed.TotalSeconds -lt 90) {
            $dialogs = @(Find-Windows $DialogTitle)
            if ($dialogs.Count -gt 0) {
                # The dialog waits for a person. Its picture is taken, then Escape answers it as "cancel".
                Start-Sleep -Seconds 1
                Note "picture of the file dialog ($label)" $(if (Save-Screen "$pictures-dialog.png") { "$pictures-dialog.png" } else { 'not taken' })
                Invoke-Tool 'xdotool' @('windowactivate', '--sync', "$($dialogs[0])") | Out-Null
                Invoke-Tool 'xdotool' @('key', 'Escape') | Out-Null
                $answered = $true
            }
        }
        Start-Sleep -Milliseconds 500
    }
    if (-not $trial.HasExited) {
        Save-Screen "$pictures-stuck.png" | Out-Null
        Close-MainWindow      # the trial then writes what it learnt so far
        Wait-Exit $trial 40 | Out-Null
    }
    Note $label $(if ($trial.HasExited) { "ended after $([Math]::Round($watch.Elapsed.TotalSeconds, 1)) s with exit code $($trial.ExitCode)" } else { 'did not end, stopped' })
    Stop-Hard $trial
    # A browser the launcher may have started on a runner.
    if ($env:GITHUB_ACTIONS) { foreach ($name in 'firefox', 'chrome', 'chromium') { Invoke-Tool 'pkill' @('-f', $name) | Out-Null } }
    if (Test-Path -LiteralPath $platformFile) {
        try {
            $platform = Get-Content -LiteralPath $platformFile -Raw | ConvertFrom-Json
            foreach ($step in 'fileDialog', 'launcher', 'restart') {
                $result = $platform.$step
                if ($null -ne $result) { Note "$label $step" "tried $($result.tried), ok $(if ($null -eq $result.ok) { 'null' } else { $result.ok }): $($result.detail)" }
            }
            foreach ($name in 'operatingSystem', 'runtimeIdentifier', 'xdgSessionType', 'displaySet', 'waylandDisplaySet', 'renderScaling', 'themeVariant',
                'systemTextScale', 'systemTextScaleSource', 'xftDpi', 'bodyLineHeight', 'boxHeight') {
                Note "$label $name" $(if ($null -eq $platform.$name) { 'null' } else { $platform.$name })
            }
            if ($platform.error) { Note "$label error" "$($platform.error) (step running: $($platform.stepRunning))" }
        } catch { Note "$label file" 'written, but it could not be read as JSON' }
    } else { Note "$label file" 'not written' }
}

$xvfb = $null
$manager = $null
try {
    if (-not (Test-Path -LiteralPath $Program)) {
        Note 'program' 'not found at the path given'
        throw 'the program was not found'
    }
    Invoke-Tool 'chmod' @('+x', $Program) | Out-Null
    Note 'program' (Split-Path -Leaf $Program)
    Note 'system' ((Invoke-Tool 'uname' @('-sr')) -join ' ')
    $release = @(Get-Content -LiteralPath '/etc/os-release' -ErrorAction SilentlyContinue | Where-Object { $_ -like 'PRETTY_NAME=*' })
    if ($release.Count -gt 0) { Note 'distribution' ($release[0] -replace '^PRETTY_NAME=', '' -replace '"', '') }

    $useLibrary = $false
    if ($Library) {
        $full = [IO.Path]::GetFullPath($Library).Replace('\', '/').TrimEnd('/')
        if (-not $full.EndsWith($LibraryEnding, [StringComparison]::Ordinal)) {
            Note 'test library' "refused: its path does not end with $LibraryEnding"
            $failures.Add('the library given is not the 500 test library')
        }
        elseif (-not (Test-Path -LiteralPath (Join-Path $Library 'data'))) {
            Note 'test library' 'not found at the path given'
            $failures.Add('the 500 test library was not found')
        }
        else { $useLibrary = $true }
    }

    # --- the virtual display and a window manager. A runner's image may name a
    # Wayland display that no compositor serves: the trial, the file dialog and a
    # browser are told to use X.
    if ($env:WAYLAND_DISPLAY) { Note 'WAYLAND_DISPLAY' 'was set on this machine; removed for these runs' }
    $env:WAYLAND_DISPLAY = $null
    $env:GDK_BACKEND = 'x11'
    $xvfb = Start-Process -FilePath 'Xvfb' -ArgumentList @($Display, '-screen', '0', '1920x1080x24', '-nolisten', 'tcp') -PassThru `
        -RedirectStandardOutput (Join-Path $Out 'xvfb-stdout.txt') -RedirectStandardError (Join-Path $Out 'xvfb-stderr.txt')
    $env:DISPLAY = $Display
    $ready = $false
    foreach ($try in 1..40) {
        if ((Invoke-Tool 'xdpyinfo' @('-display', $Display)).Count -gt 0) { $ready = $true; break }
        Start-Sleep -Milliseconds 250
    }
    # A display number that was already taken answers too, but then our own Xvfb has ended.
    if ($ready -and $xvfb.HasExited) { $ready = $false }
    Note 'virtual display' $(if ($ready) { "ready at $Display, 1920x1080" } else { 'did not come up' })
    if (-not $ready) { throw 'the virtual display did not come up' }
    $manager = Start-Process -FilePath 'openbox' -PassThru `
        -RedirectStandardOutput (Join-Path $Out 'openbox-stdout.txt') -RedirectStandardError (Join-Path $Out 'openbox-stderr.txt')
    $managerReady = $false
    foreach ($try in 1..40) {
        if ((Invoke-Tool 'wmctrl' @('-m')).Count -gt 0) { $managerReady = $true; break }
        Start-Sleep -Milliseconds 250
    }
    Note 'window manager' $(if ($managerReady) { 'openbox running' } else { 'openbox did not answer' })
    Note 'session type' $(if ($env:XDG_SESSION_TYPE) { $env:XDG_SESSION_TYPE } else { 'not set (no desktop session: X11 under Xvfb)' })

    # --- the self-check: no window
    $trial = Start-Trial 'self-check' @('--self-check')
    if (Wait-Exit $trial 120) {
        Note 'self-check exit code' $trial.ExitCode
        if ($trial.ExitCode -ne 0) { $failures.Add("the self-check ended with exit code $($trial.ExitCode)") }
    }
    else {
        Stop-Hard $trial
        Note 'self-check exit code' 'none: it did not end within 120 s and was stopped'
        $failures.Add('the self-check did not end within 120 s')
    }
    $last = @(Get-Content -LiteralPath (Join-Path $Out 'self-check-stdout.txt') -ErrorAction SilentlyContinue | Where-Object { $_.Trim() } | Select-Object -Last 1)
    if ($last.Count -gt 0) { Note 'self-check last line' $last[0] }

    # --- the example library in a real window
    $watch = [Diagnostics.Stopwatch]::StartNew()
    $trial = Start-Trial 'example' @()
    $ids = @()
    while ($watch.Elapsed.TotalSeconds -lt 90 -and -not $trial.HasExited) {
        $ids = @(Find-Windows $MainTitle)
        if ($ids.Count -gt 0) { break }
        Start-Sleep -Milliseconds 500
    }
    $windowOpened = ($ids.Count -gt 0)
    Note 'example window opened' $(if ($windowOpened) { "yes, after $([Math]::Round($watch.Elapsed.TotalSeconds, 1)) s" } elseif ($trial.HasExited) { "no: the trial ended with exit code $($trial.ExitCode)" } else { 'no: no window after 90 s' })
    if ($windowOpened) {
        Start-Sleep -Seconds 5      # the library loads and the first frames are drawn
        $titles = @($ids | ForEach-Object { (Invoke-Tool 'xdotool' @('getwindowname', "$_")) -join ' ' } | Where-Object { $_ } | Select-Object -Unique)
        if ($titles.Count -gt 0) { Note 'example window title' ($titles -join '; ') }
        $geometry = @($ids | ForEach-Object { Invoke-Tool 'xdotool' @('getwindowgeometry', "$_") } | Where-Object { $_ -match 'Geometry' } | Select-Object -First 1)
        if ($geometry.Count -gt 0) { Note 'example window size' ($geometry[0].Trim() -replace '^Geometry:\s*', '') }
        Note 'picture of the example window' $(if (Save-Screen 'example-window.png') { 'example-window.png' } else { 'not taken' })
        Close-MainWindow
        if (Wait-Exit $trial 30) {
            Note 'example window closed by its close request' "yes, exit code $($trial.ExitCode)"
            if ($trial.ExitCode -ne 0) { $failures.Add("the example run ended with exit code $($trial.ExitCode) after its close request") }
        }
        else {
            Note 'example window closed by its close request' 'no: still running after 30 s, stopped'
            $failures.Add('the example window did not close within 30 s of its close request')
        }
    }
    else { $failures.Add('the example window did not open') }
    Stop-Hard $trial

    # --- the test library: a read-only snapshot, timed in the real window. A run
    # that ends by itself and writes its rows has drawn its frames in the window.
    if ($useLibrary) {
        $measureFile = Join-Path $Out 'measure-xvfb.json'
        Remove-Item -LiteralPath $measureFile -ErrorAction SilentlyContinue
        $trial = Start-Trial 'measure' @('--snapshot-from', $Library, '--measure', $measureFile)
        $pictured = $false
        $watch.Restart()
        while ($watch.Elapsed.TotalSeconds -lt 240 -and -not $trial.HasExited) {
            if (-not $pictured -and @(Find-Windows $MainTitle).Count -gt 0) { Start-Sleep -Seconds 2; $pictured = Save-Screen 'measure-window.png' }
            Start-Sleep -Milliseconds 500
        }
        $ended = $trial.HasExited
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
    elseif (-not $Library) { Note 'timed run' 'skipped: no test library was given' }

    # --- the platform check: the file dialog, the launcher, a second copy, and
    # what reaches the window of the system's text size. Recorded, never judged:
    # a runner has no desktop to open a browser in. It runs twice: as the display
    # starts, and again after xrdb sets the X resource Xft.dpi to 144 (150 %), to
    # see whether that reaches the window's render scaling or its text (the
    # harness design, part 3.5).
    Invoke-PlatformCheck 'platform check' 'platform' 'platform-check' 'platform-check-xvfb.json'
    $dpiFile = Join-Path $Out 'xft-dpi-144.txt'
    Set-Content -LiteralPath $dpiFile -Value 'Xft.dpi: 144' -Encoding ascii
    Invoke-Tool 'xrdb' @('-merge', $dpiFile) | Out-Null
    $dpi = @(Invoke-Tool 'xrdb' @('-query') | Where-Object { $_ -like 'Xft.dpi:*' })
    if ($dpi.Count -gt 0) {
        Note 'Xft.dpi for the second platform check' ($dpi[0] -replace '^Xft\.dpi:\s*', '')
        Invoke-PlatformCheck 'platform check at Xft.dpi 144' 'platform-dpi144' 'platform-check-dpi144' 'platform-check-xvfb-dpi144.json'
    }
    else { Note 'Xft.dpi for the second platform check' 'not set (xrdb did not answer), so the second run was skipped' }
}
catch {
    Note 'stopped early' $_.Exception.Message
    $failures.Add("stopped early: $($_.Exception.Message)")
}
finally {
    # A browser the launcher may have started on a runner, then the window manager and the display.
    if ($env:GITHUB_ACTIONS) { foreach ($name in 'firefox', 'chrome', 'chromium') { Invoke-Tool 'pkill' @('-f', $name) | Out-Null } }
    Stop-Hard $manager
    Stop-Hard $xvfb
}

# --- the summary
$summary = [System.Collections.Generic.List[string]]::new()
$summary.Add('### The Board trial in a real X window on Linux (Xvfb)')
$summary.Add('')
$summary.Add('| | |')
$summary.Add('|---|---|')
foreach ($key in $facts.Keys) { $summary.Add("| $key | $(("$($facts[$key])") -replace '\|', '/') |") }
$summary.Add('')
if ($failures.Count -gt 0) {
    $summary.Add('Not as required:')
    $summary.Add('')
    foreach ($failure in $failures) { $summary.Add("- $failure") }
    $summary.Add('')
}
$summary.Add('The pictures and the result files are in this job''s artifact. Figures from a shared runner are not a verdict on speed.')
$summary | Set-Content -LiteralPath (Join-Path $Out 'summary.md') -Encoding utf8
if ($env:GITHUB_STEP_SUMMARY) { $summary | Add-Content -LiteralPath $env:GITHUB_STEP_SUMMARY -Encoding utf8 }

if ($RequireWindow -and $failures.Count -gt 0) {
    Write-Host "::error title=Board trial on Linux::$($failures -join '; ')"
    exit 1
}
exit 0
