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
# when the self-check does not pass or the example window does not open; without
# it the script always ends with 0.
#
# The library named with -Library must be a TEST library (the 500-manuscript
# library the manual test WriteLargeLibraryForMeasurement writes). Never name a
# person's own library here.
[CmdletBinding()]
param(
    # The published trial's program file: <folder>/PaperRoute.Desktop
    [Parameter(Mandatory)] [string] $Program,
    # A folder for the pictures, the results and the logs. It is created.
    [Parameter(Mandatory)] [string] $Out,
    # A test library for --snapshot-from and --measure. Left out: those steps are skipped.
    [string] $Library,
    [switch] $RequireWindow,
    [string] $Display = ':99'
)

$ErrorActionPreference = 'Continue'
$WindowTitle = 'PaperRoute Board Trial'

New-Item -ItemType Directory -Force -Path $Out | Out-Null
$Out = (Resolve-Path -LiteralPath $Out).Path
$facts = [ordered]@{}
$lines = [System.Collections.Generic.List[string]]::new()

function Note([string] $key, $value) {
    $facts[$key] = $value
    $text = "${key}: $value"
    $lines.Add($text)
    Write-Host $text
}

# Runs a tool and returns its output lines; a missing tool or a failure gives an empty list.
function Invoke-Tool([string] $name, [string[]] $arguments) {
    try { return @(& $name @arguments 2>$null) } catch { return @() }
}

# The ids of the visible windows whose title holds the text.
function Find-Windows([string] $title) {
    @(Invoke-Tool 'xdotool' @('search', '--onlyvisible', '--name', $title) | Where-Object { $_ -match '^\d+$' })
}

function Save-Screen([string] $name) {
    $file = Join-Path $Out $name
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

# Asks the window to close as a person's click on its close button would.
function Close-Window([string] $title) {
    Invoke-Tool 'wmctrl' @('-c', $title) | Out-Null
}

function Stop-Hard($process) {
    if ($null -ne $process -and -not $process.HasExited) { try { $process.Kill($true) } catch { } }
}

$xvfb = $null
$manager = $null
$selfCheckPassed = $false
$windowOpened = $false
try {
    if (-not (Test-Path -LiteralPath $Program)) {
        Note 'program' "not found at the path given"
        throw 'no program'
    }
    Invoke-Tool 'chmod' @('+x', $Program) | Out-Null
    Note 'program' (Split-Path -Leaf $Program)
    Note 'system' ((Invoke-Tool 'uname' @('-sr')) -join ' ')
    $release = @(Get-Content -LiteralPath '/etc/os-release' -ErrorAction SilentlyContinue | Where-Object { $_ -like 'PRETTY_NAME=*' })
    if ($release.Count -gt 0) { Note 'distribution' ($release[0] -replace '^PRETTY_NAME=', '' -replace '"', '') }

    # --- the virtual display and a window manager
    $xvfb = Start-Process -FilePath 'Xvfb' -ArgumentList @($Display, '-screen', '0', '1920x1080x24', '-nolisten', 'tcp') -PassThru `
        -RedirectStandardOutput (Join-Path $Out 'xvfb-stdout.txt') -RedirectStandardError (Join-Path $Out 'xvfb-stderr.txt')
    $env:DISPLAY = $Display
    $ready = $false
    foreach ($try in 1..40) {
        if ((Invoke-Tool 'xdpyinfo' @('-display', $Display)).Count -gt 0) { $ready = $true; break }
        Start-Sleep -Milliseconds 250
    }
    Note 'virtual display' $(if ($ready) { "ready at $Display, 1920x1080" } else { 'did not come up' })
    if (-not $ready) { throw 'no display' }
    $manager = Start-Process -FilePath 'openbox' -PassThru `
        -RedirectStandardOutput (Join-Path $Out 'openbox-stdout.txt') -RedirectStandardError (Join-Path $Out 'openbox-stderr.txt')
    Start-Sleep -Seconds 2
    Note 'window manager' $(if ($null -ne $manager -and -not $manager.HasExited) { 'openbox running' } else { 'openbox did not stay up' })
    Note 'session type' $(if ($env:XDG_SESSION_TYPE) { $env:XDG_SESSION_TYPE } else { 'not set (no desktop session: X11 under Xvfb)' })

    # --- the self-check: no window
    $selfLog = Join-Path $Out 'self-check.txt'
    & $Program --self-check *> $selfLog
    $selfExit = $LASTEXITCODE
    $selfCheckPassed = ($selfExit -eq 0)
    Note 'self-check exit code' $selfExit
    $last = @(Get-Content -LiteralPath $selfLog -ErrorAction SilentlyContinue | Where-Object { $_.Trim() } | Select-Object -Last 1)
    if ($last.Count -gt 0) { Note 'self-check last line' $last[0] }

    # --- the example library in a real window
    $watch = [Diagnostics.Stopwatch]::StartNew()
    $trial = Start-Trial 'example' @()
    $ids = @()
    while ($watch.Elapsed.TotalSeconds -lt 90 -and -not $trial.HasExited) {
        # @(...) keeps a single id a list: a function that returns one item hands back the item itself.
        $ids = @(Find-Windows $WindowTitle)
        if ($ids.Count -gt 0) { break }
        Start-Sleep -Milliseconds 500
    }
    $windowOpened = ($ids.Count -gt 0)
    Note 'example window opened' $(if ($windowOpened) { "yes, after $([Math]::Round($watch.Elapsed.TotalSeconds, 1)) s" } elseif ($trial.HasExited) { "no: the trial ended with exit code $($trial.ExitCode)" } else { 'no: no window after 90 s' })
    if ($windowOpened) {
        Start-Sleep -Seconds 5      # the library loads and the first frames are drawn
        $titles = @($ids | ForEach-Object { (Invoke-Tool 'xdotool' @('getwindowname', "$_")) -join ' ' } | Where-Object { $_ } | Select-Object -Unique)
        Note 'example window title' ($titles -join '; ')
        $geometry = @($ids | ForEach-Object { Invoke-Tool 'xdotool' @('getwindowgeometry', "$_") } | Where-Object { $_ -match 'Geometry' } | Select-Object -First 1)
        if ($geometry.Count -gt 0) { Note 'example window size' ($geometry[0].Trim() -replace '^Geometry:\s*', '') }
        Note 'picture of the example window' $(if (Save-Screen 'example-window.png') { 'example-window.png' } else { 'not taken' })
        Close-Window $WindowTitle
        $closed = Wait-Exit $trial 30
        Note 'example window closed by its close request' $(if ($closed) { "yes, exit code $($trial.ExitCode)" } else { 'no: still running after 30 s, stopped' })
    }
    Stop-Hard $trial

    # --- a test library: a read-only snapshot, timed in the real window
    if ($Library -and (Test-Path -LiteralPath (Join-Path $Library 'data'))) {
        $measureFile = Join-Path $Out 'measure-xvfb.json'
        $trial = Start-Trial 'measure' @('--snapshot-from', $Library, '--measure', $measureFile)
        $pictured = $false
        $watch.Restart()
        while ($watch.Elapsed.TotalSeconds -lt 240 -and -not $trial.HasExited) {
            if (-not $pictured -and @(Find-Windows $WindowTitle).Count -gt 0) { Start-Sleep -Seconds 2; $pictured = Save-Screen 'measure-window.png' }
            Start-Sleep -Milliseconds 500
        }
        $ended = $trial.HasExited
        Note 'measure run' $(if ($ended) { "ended after $([Math]::Round($watch.Elapsed.TotalSeconds, 1)) s with exit code $($trial.ExitCode)" } else { 'did not end within 240 s, stopped' })
        Stop-Hard $trial
        if (Test-Path -LiteralPath $measureFile) {
            try {
                $measure = Get-Content -LiteralPath $measureFile -Raw | ConvertFrom-Json
                $rows = @($measure.rows)
                Note 'measure rows written' $rows.Count
                foreach ($id in 'warm-startup', 'first-switch-published', 'search-keystroke', 'scroll-published-longest-step') {
                    $row = @($rows | Where-Object { $_.id -eq $id -and $_.library -ne 'example' }) | Select-Object -First 1
                    if ($null -eq $row) { $row = @($rows | Where-Object id -eq $id) | Select-Object -Last 1 }
                    if ($null -ne $row) { Note "measure $id (UI ms / budget, on a shared runner: not a verdict)" "$([Math]::Round([double]$row.uiMs, 1)) / $($row.budgetMs)" }
                }
            } catch { Note 'measure file' 'written, but it could not be read as JSON' }
        } else { Note 'measure file' 'not written' }
    } else { Note 'measure run' 'skipped: no test library was given' }

    # --- the platform check: the file dialog, the launcher, a second copy
    $platformFile = Join-Path $Out 'platform-check-xvfb.json'
    $trial = Start-Trial 'platform' @('--platform-check', $platformFile)
    $watch.Restart()
    $shots = 0
    $nextKey = 12
    while ($watch.Elapsed.TotalSeconds -lt 240 -and -not $trial.HasExited) {
        $elapsed = $watch.Elapsed.TotalSeconds
        if ($elapsed -ge $nextKey -and $nextKey -le 60) {
            # A dialog waits for a person here. Its picture is taken, then Escape answers it as "cancel".
            $shots++
            Save-Screen "platform-check-$shots.png" | Out-Null
            Invoke-Tool 'xdotool' @('key', 'Escape') | Out-Null
            $nextKey += 12
        }
        Start-Sleep -Milliseconds 500
    }
    if (-not $trial.HasExited) {
        Save-Screen 'platform-check-stuck.png' | Out-Null
        Close-Window $WindowTitle      # the trial then writes what it learnt so far
        Wait-Exit $trial 40 | Out-Null
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
            foreach ($name in 'operatingSystem', 'runtimeIdentifier', 'xdgSessionType', 'displaySet', 'waylandDisplaySet', 'renderScaling', 'themeVariant') {
                Note "platform check $name" $(if ($null -eq $platform.$name) { 'null' } else { $platform.$name })
            }
            if ($platform.error) { Note 'platform check error' "$($platform.error) (step running: $($platform.stepRunning))" }
        } catch { Note 'platform check file' 'written, but it could not be read as JSON' }
    } else { Note 'platform check file' 'not written' }
}
catch {
    Note 'stopped early' $_.Exception.Message
}
finally {
    # A browser the launcher may have started, then the window manager and the display.
    foreach ($name in 'firefox', 'chrome', 'chromium') { Invoke-Tool 'pkill' @('-f', $name) | Out-Null }
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
$summary.Add('The pictures and the result files are in this job''s artifact. Figures from a shared runner are not a verdict on speed.')
$summary | Set-Content -LiteralPath (Join-Path $Out 'summary.md') -Encoding utf8
if ($env:GITHUB_STEP_SUMMARY) { $summary | Add-Content -LiteralPath $env:GITHUB_STEP_SUMMARY -Encoding utf8 }

if ($RequireWindow -and -not ($selfCheckPassed -and $windowOpened)) {
    Write-Host "::error title=Board trial on Linux::The self-check passed: $selfCheckPassed. The example window opened: $windowOpened."
    exit 1
}
exit 0
