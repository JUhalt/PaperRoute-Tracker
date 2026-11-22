#requires -Version 7
<#
.SYNOPSIS
Checks the Board pairs rule (Measure-BoardPairs.ps1) on canned measurements.
.DESCRIPTION
Writes small sittings of canned JSON files, reads each with
Measure-BoardPairs.ps1 -Analyze (nothing is run or built), and compares its
verdict with what the rule of the measurement method gives by hand
(docs/design/Board-Trial-Measurement-Method.md, section 5). It also checks the
sign test's counts against the method's table, and that a summary never
copies the machine name or names the folder. Exit code 1 on any mismatch.
.EXAMPLE
pwsh -NoProfile -File ./ManualCertification/Test-BoardPairs.ps1
#>
[CmdletBinding()]
param(
    # Where the canned sittings are written; by default a new folder under
    # the repository's ignored artifacts folder.
    [string] $WorkFolder
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$pairsScript = Join-Path $PSScriptRoot 'Measure-BoardPairs.ps1'
if (-not $WorkFolder) { $WorkFolder = Join-Path $repoRoot ('artifacts/board-pairs-check/run-' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss')) }
if (Test-Path -LiteralPath $WorkFolder) { throw 'Use a new work folder.' }
New-Item -ItemType Directory -Force -Path $WorkFolder | Out-Null
$WorkFolder = [IO.Path]::GetFullPath($WorkFolder)
$machine = 'CANNED-MACHINE-NAME'
$primary = 'stage-back-to-all'
$watch = 'open-list-stepped-closed'
$rise = 'first-switch-published'
$failures = [Collections.Generic.List[string]]::new()

function Assert-That([bool] $condition, [string] $what) {
    if ($condition) { Write-Host "  ok: $what" } else { Write-Host "  FAILED: $what"; $failures.Add($what) }
}

# One run's JSON, shaped like the trial's report: the example library's rows
# (never held) and the 500 library's.
function New-RunJson([double] $ui, $runUi, [double] $watchUi, [double] $riseUi, $conditions) {
    $row = {
        param($library, $id, $budget, $rule, $uiMs, $runUiMs)
        [ordered]@{
            library = $library; id = $id; action = $id; wallMs = $uiMs; uiMs = $uiMs; budgetMs = $budget; budgetName = 'canned'
            verdict = $(if ($uiMs -le $budget) { 'within' } else { 'over' }); rule = $rule
            runMs = $runUiMs; notRunMs = $(if ($null -ne $runUiMs) { $uiMs - $runUiMs } else { $null }); runUiMs = $runUiMs
            perfCoreShare = 0; gcPauseMs = 1.5; jitMs = 0
        }
    }
    [ordered]@{
        mode = 'headless'; machineName = $machine; dpi = 96; option = 'b'; syncIntoResetThreshold = 64; day = '2026-11-21'
        daySource = "the computer's date"; build = 'Release'; budgetMultiplier = 1; takenAt = '2026-11-21T22:00:00'; processors = 12
        operatingSystem = 'canned'; heldLibrary = '500'; waitingForRowsOfN = @($rise)
        rows = @(
            (& $row 'example' $primary 250 'report-only' 999 900),
            (& $row '500' $rise 100 'waiting for rows of N' $riseUi $null),
            (& $row '500' $primary 250 'enforced' $ui $runUi),
            (& $row '500' $watch 250 'enforced' $watchUi $watchUi)
        )
        conditions = $conditions
    } | ConvertTo-Json -Depth 6
}

function New-Conditions($disturbed = $false, $others = 6.0) {
    [ordered]@{ threadClock = 'cycles'; cyclesPerMs = 2496000; rateCheck = $null; machineBusyProcessors = 7.0; loadReadMs = @(10, 10)
        stealProcessors = $null; othersBusyProcessors = $others; notRunShare = 0.3; disturbed = $disturbed }
}

# A sitting of canned pairs. Each pair is a hashtable: B and A (UI of the
# primary row before and after), optional BRun/ARun (Run UI; default UI less
# 50), BWatch/AWatch, BCond/ACond (conditions, or 'none' for null).
function New-Sitting([string] $name, [int] $planned, [object[]] $pairs) {
    $folder = Join-Path $WorkFolder $name
    New-Item -ItemType Directory -Force -Path $folder | Out-Null
    $runs = [Collections.Generic.List[object]]::new()
    $order = 0
    for ($i = 0; $i -lt $pairs.Count; $i++) {
        $pair = $i + 1; $p = $pairs[$i]
        $sides = if ($pair % 2 -eq 1) { 'before', 'after' } else { 'after', 'before' }
        foreach ($side in $sides) {
            $order++
            $key = if ($side -eq 'before') { 'B' } else { 'A' }
            $ui = [double] $p[$key]
            $runUi = if ($p.ContainsKey("${key}Run")) { $p["${key}Run"] } else { $ui - 50 }
            $watchUi = if ($p.ContainsKey("${key}Watch")) { [double] $p["${key}Watch"] } else { 150 }
            $conditions = if ($p.ContainsKey("${key}Cond")) { $p["${key}Cond"] } else { New-Conditions }
            if ($conditions -eq 'none') { $conditions = $null }
            $file = 'pair{0:00}-{1}.json' -f $pair, $side
            New-RunJson $ui $runUi $watchUi 90 $conditions | Set-Content -LiteralPath (Join-Path $folder $file) -Encoding utf8NoBOM
            $runs.Add([ordered]@{ pair = $pair; order = $order; name = $side; file = $file; exit = 1; seconds = 30; loadOutlasted = $false })
        }
    }
    [ordered]@{ mode = 'headless'; beforeName = 'before'; afterName = 'after'; pairs = $planned; primaryRow = $primary
        expectedToRise = @($rise); load = 'None'; startedAt = '2026-11-21 22:00'; runs = $runs.ToArray() } |
        ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $folder 'sitting.json') -Encoding utf8NoBOM
    return $folder
}

# Pairs where after is lower by 30 ms, except those listed as higher (+10)
# or tied (equal). Before's UI moves a little from pair to pair.
function New-Pairs([int] $count, [int[]] $higher = @(), [int[]] $tied = @()) {
    @(for ($pair = 1; $pair -le $count; $pair++) {
        $b = 200 + ($pair % 4) * 5
        $a = if ($higher -contains $pair) { $b + 10 } elseif ($tied -contains $pair) { $b } else { $b - 30 }
        @{ B = $b; A = $a }
    })
}

function Invoke-Check([string] $title, [string[]] $folders) {
    Write-Host "$title"
    $result = & $pairsScript -Analyze $folders[0] -Pool @($folders | Select-Object -Skip 1) -PassThru
    $text = $result.Lines -join "`n"
    Assert-That (-not $text.Contains($machine)) 'the summary does not copy the machine name'
    Assert-That (-not $text.Contains($WorkFolder) -and -not $text.Contains($repoRoot)) 'the summary names no folder'
    return $result
}

# 1. The sign test's counts against the method's table (one-sided 5%, exact binomial).
Write-Host 'Sign test counts'
$table = & $pairsScript -SignTable
$expected = @{ 10 = 9; 11 = 9; 12 = 10; 13 = 10; 14 = 11; 15 = 12; 16 = 12; 17 = 13; 18 = 13; 19 = 14; 20 = 15 }
foreach ($n in $expected.Keys | Sort-Object) {
    $row = $table | Where-Object Pairs -eq $n
    Assert-That ($row.Need -eq $expected[$n] -and $row.P -le 0.05 -and $row.PBelow -gt 0.05) "$n pairs need $($expected[$n]) (script: $($row.Need), P $([Math]::Round($row.P, 4)), one fewer $([Math]::Round($row.PBelow, 4)))"
}
$ten = $table | Where-Object Pairs -eq 10; $twenty = $table | Where-Object Pairs -eq 20
Assert-That ([Math]::Abs($ten.P - 11 / 1024) -lt 1e-12 -and [Math]::Round($ten.PBelow, 3) -eq 0.055) 'P(>= 9 of 10) = 11/1024 and P(>= 8 of 10) = 0.055'
Assert-That ([Math]::Round($twenty.P, 3) -eq 0.021 -and [Math]::Round($twenty.PBelow, 3) -eq 0.058) 'P(>= 15 of 20) = 0.021 and P(>= 14 of 20) = 0.058'

# 2. 9 of 10 lower: 9 reaches the count of 10; median difference 30 ms over
# the floor (max(2, 3% of about 205) = 6.2 ms).
$r = Invoke-Check '9 of 10 lower' @(New-Sitting 'nine-of-ten' 10 (New-Pairs 10 -higher 7))
Assert-That ($r.Verdict -eq 'gain shown' -and $r.Lower -eq 9 -and $r.Pairs -eq 10 -and $r.Need -eq 9) "gain shown, 9 of 10, needs 9 (got $($r.Verdict), $($r.Lower) of $($r.Pairs), needs $($r.Need))"
Assert-That ($r.MedianDifferenceMs -eq 30) "median difference 30 ms (got $($r.MedianDifferenceMs))"
Assert-That (@($r.Lines | Where-Object { $_ -like '*proposed verdict (report-only)*' }).Count -eq 1) 'the proposed-verdict heading is printed'

# 3. 8 of 10 lower: one short of 9.
$r = Invoke-Check '8 of 10 lower' @(New-Sitting 'eight-of-ten' 10 (New-Pairs 10 -higher 3, 7))
Assert-That ($r.Verdict -eq 'not shown' -and $r.Lower -eq 8 -and $r.Need -eq 9) "not shown, 8 of 10 (got $($r.Verdict), $($r.Lower))"

# 4. A disturbed pair is replaced. Pair 4 (after lower) is disturbed; pairs 7
# and 9 are higher; pair 11 is the replacement. Kept: 10 pairs, 8 lower: not
# shown. Had the disturbed pair been counted, 9 of 11 would have passed.
$pairs = New-Pairs 11 -higher 7, 9
$pairs[3]['ACond'] = New-Conditions -disturbed $true -others 11.2
$r = Invoke-Check 'a disturbed pair, replaced' @(New-Sitting 'disturbed-replaced' 10 $pairs)
Assert-That ($r.Verdict -eq 'not shown' -and $r.Lower -eq 8 -and $r.Pairs -eq 10 -and $r.Replaced -eq 1) "not shown, 8 of 10 kept, 1 replaced (got $($r.Verdict), $($r.Lower) of $($r.Pairs), $($r.Replaced) replaced)"
Assert-That (@($r.Lines | Where-Object { $_ -like '*dropped: pair 4 after: disturbed*' }).Count -eq 1) 'the dropped run is listed with its reason'

# 5. Ties count against: 8 lower, 1 tied, 1 higher is 8 of 10, not shown.
# Had the tie counted for, 9 of 10 would have passed.
$r = Invoke-Check 'a tie' @(New-Sitting 'tie' 10 (New-Pairs 10 -higher 3 -tied 7))
Assert-That ($r.Verdict -eq 'not shown' -and $r.Lower -eq 8) "not shown, 8 of 10 with the tie against (got $($r.Verdict), $($r.Lower))"

# 6. Absent fields: Run UI null in pairs 2 and 5, a run with no conditions
# and one with the load unknown. Nothing is dropped; 9 of 10 passes; Run UI
# is counted over the 8 pairs that carry it.
$pairs = New-Pairs 10 -higher 7
$pairs[1]['BRun'] = $null; $pairs[1]['ARun'] = $null; $pairs[4]['ARun'] = $null
$pairs[2]['BCond'] = 'none'
$pairs[5]['ACond'] = New-Conditions -disturbed $null -others $null
$r = Invoke-Check 'fields absent' @(New-Sitting 'absent' 10 $pairs)
Assert-That ($r.Verdict -eq 'gain shown' -and $r.Pairs -eq 10 -and $r.Replaced -eq 0 -and $r.RunUiPairs -eq 8) "gain shown, 10 kept, Run UI over 8 pairs (got $($r.Verdict), $($r.Pairs), $($r.Replaced) replaced, $($r.RunUiPairs))"
Assert-That (@($r.Lines | Where-Object { $_ -like '*load unknown*' }).Count -ge 1) 'a run with the load unknown is printed so'

# 7. The size floor: 10 of 10 lower by 1 ms is under max(2, 3% of 205) ms.
$r = Invoke-Check 'under the size floor' @(New-Sitting 'floor' 10 @(for ($p = 1; $p -le 10; $p++) { @{ B = 205; A = 204 } }))
Assert-That ($r.Verdict -eq 'not shown' -and $r.Lower -eq 10 -and $r.MedianDifferenceMs -eq 1) "not shown under the floor (got $($r.Verdict), $($r.Lower), $($r.MedianDifferenceMs) ms)"

# 8. Run UI the other way: UI lower in 9 of 10, Run UI higher in all 10.
$pairs = New-Pairs 10 -higher 7
foreach ($p in $pairs) { $p['BRun'] = 100; $p['ARun'] = 120 }
$r = Invoke-Check 'Run UI the other way' @(New-Sitting 'run-ui-other-way' 10 $pairs)
Assert-That ($r.Verdict -eq 'stop' -and $r.RunUiHigher -eq 10) "stop (got $($r.Verdict), Run UI higher in $($r.RunUiHigher))"

# 9. A watch row higher in 10 of 10: a new sitting for it; higher again in a
# second sitting: it blocks. The row expected to rise never blocks.
$pairs = New-Pairs 10 -higher 7
foreach ($p in $pairs) { $p['BWatch'] = 150; $p['AWatch'] = 170 }
$first = New-Sitting 'watch-1' 10 $pairs
$r = Invoke-Check 'a watch row higher' @($first)
Assert-That ($r.Verdict -eq 'gain shown' -and @($r.WatchHigher) -contains $watch -and @($r.WatchBlocking).Count -eq 0) "gain shown, $watch flagged, not blocking (got $($r.Verdict), $(@($r.WatchHigher) -join ','), $(@($r.WatchBlocking) -join ','))"
$second = New-Sitting 'watch-2' 10 $pairs
$r = Invoke-Check 'a watch row higher again' @($second, $first)
Assert-That (@($r.WatchBlocking) -contains $watch) "$watch blocks when higher again (got $(@($r.WatchBlocking) -join ','))"

# 10. Void: 6 pairs to replace, one more than 5.
$pairs = New-Pairs 16 -higher 7
foreach ($i in 0, 2, 4, 6, 8, 10) { $pairs[$i]['BCond'] = New-Conditions -disturbed $true -others 11.5 }
$r = Invoke-Check 'more than 5 replacements' @(New-Sitting 'void' 10 $pairs)
Assert-That ($r.Verdict -eq 'void' -and $r.Replaced -eq 6) "void, 6 replaced (got $($r.Verdict), $($r.Replaced))"

# 11. Pooling: 8 of 10, then 7 of 10, neither enough alone; together 15 of
# 20 reaches 15.
$one = New-Sitting 'pool-1' 10 (New-Pairs 10 -higher 2, 5)
$two = New-Sitting 'pool-2' 10 (New-Pairs 10 -higher 3, 6, 9)
$r = Invoke-Check 'two sittings pooled' @($two, $one)
Assert-That ($r.Verdict -eq 'gain shown' -and $r.Lower -eq 15 -and $r.Pairs -eq 20 -and $r.Need -eq 15) "gain shown, 15 of 20 (got $($r.Verdict), $($r.Lower) of $($r.Pairs), needs $($r.Need))"

# 12. Five pairs never decide.
$r = Invoke-Check 'five pairs' @(New-Sitting 'five' 5 (New-Pairs 5))
Assert-That ($r.Verdict -eq 'not decided' -and $r.Lower -eq 5) "not decided (got $($r.Verdict), $($r.Lower))"

if ($failures.Count) {
    Write-Host "Board pairs check: $($failures.Count) failed."
    exit 1
}
Write-Host 'Board pairs check: every case gave the verdict the rule gives by hand.'
