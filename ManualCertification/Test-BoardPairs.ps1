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
copies the machine name or names the folder, and reads the load script's
limits without starting it. Exit code 1 on any mismatch.
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
function New-RunJson($ui, $runUi, [double] $watchUi, [double] $riseUi, $conditions, [bool] $noPrimary = $false, [string] $primaryRule = 'enforced') {
    $row = {
        param($library, $id, $budget, $rule, $uiMs, $runUiMs)
        [ordered]@{
            library = $library; id = $id; action = $id; wallMs = $uiMs; uiMs = $uiMs; budgetMs = $budget; budgetName = 'canned'
            verdict = $(if ($uiMs -le $budget) { 'within' } else { 'over' }); rule = $rule
            runMs = $runUiMs; notRunMs = $(if ($null -ne $runUiMs) { $uiMs - $runUiMs } else { $null }); runUiMs = $runUiMs
            perfCoreShare = 0; gcPauseMs = 1.5; jitMs = 0
        }
    }
    $rows = [Collections.Generic.List[object]]::new()
    $rows.Add((& $row 'example' $primary 250 'report-only' 999 900))
    $rows.Add((& $row '500' $rise 100 'waiting: over under rows of N too' $riseUi $null))
    if (-not $noPrimary) { $rows.Add((& $row '500' $primary 250 $primaryRule $ui $runUi)) }
    $rows.Add((& $row '500' $watch 250 'enforced' $watchUi $watchUi))
    [ordered]@{
        mode = 'headless'; machineName = $machine; dpi = 96; option = 'b'; syncIntoResetThreshold = 64; day = '2026-11-21'
        daySource = "the computer's date"; build = 'Release'; budgetMultiplier = 1; takenAt = '2026-11-21T22:00:00'; processors = 12
        operatingSystem = 'canned'; heldLibrary = '500'; waitingForRowsOfN = @($rise)
        rows = $rows.ToArray()
        conditions = $conditions
    } | ConvertTo-Json -Depth 6
}

function New-Conditions($disturbed = $false, $others = 6.0) {
    [ordered]@{ threadClock = 'cycles'; cyclesPerMs = 2496000; rateCheck = $null; machineBusyProcessors = 7.0; loadReadMs = @(10, 10)
        stealProcessors = $null; othersBusyProcessors = $others; notRunShare = 0.3; disturbed = $disturbed }
}

# A sitting of canned pairs. Each pair is a hashtable: B and A (UI of the
# primary row before and after), optional BRun/ARun (Run UI; default UI less
# 50), BWatch/AWatch, BCond/ACond (conditions, or 'none' for null),
# BNoPrimary/ANoPrimary (the primary row left out), BUiNull/AUiNull (its UI
# null), BOutlasted/AOutlasted and BTimedOut/ATimedOut (the record's facts),
# SkipSecond (the second run not run) and OmitSecond (the sitting stopped
# before the second run). $primaryRule is the primary row's rule in every
# run. Each sitting starts a minute after the last, as real ones do, so no
# two canned sittings have the same record.
$script:sittingMinute = 0
function New-Sitting([string] $name, [int] $planned, [object[]] $pairs, [string] $primaryRule = 'enforced') {
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
            if ($p.ContainsKey("${key}UiNull")) { $ui = $null }
            $watchUi = if ($p.ContainsKey("${key}Watch")) { [double] $p["${key}Watch"] } else { 150 }
            $conditions = if ($p.ContainsKey("${key}Cond")) { $p["${key}Cond"] } else { New-Conditions }
            if ($conditions -eq 'none') { $conditions = $null }
            $file = 'pair{0:00}-{1}.json' -f $pair, $side
            if ($p.ContainsKey('OmitSecond') -and $side -eq $sides[1]) { continue }
            if ($p.ContainsKey('SkipSecond') -and $side -eq $sides[1]) {
                $runs.Add([ordered]@{ pair = $pair; order = $order; name = $side; file = $file; exit = $null; seconds = 0; loadOutlasted = $false; skipped = $true })
                continue
            }
            New-RunJson $ui $runUi $watchUi 90 $conditions ($p.ContainsKey("${key}NoPrimary")) $primaryRule | Set-Content -LiteralPath (Join-Path $folder $file) -Encoding utf8NoBOM
            $runs.Add([ordered]@{ pair = $pair; order = $order; name = $side; file = $file; exit = 1; seconds = 30
                loadOutlasted = $p.ContainsKey("${key}Outlasted"); timedOut = $p.ContainsKey("${key}TimedOut") })
        }
    }
    $script:sittingMinute++
    [ordered]@{ mode = 'headless'; beforeName = 'before'; afterName = 'after'; pairs = $planned; primaryRow = $primary
        expectedToRise = @($rise); load = 'None'; startedAt = ([DateTime]::new(2026, 11, 21, 22, 0, 0).AddMinutes($script:sittingMinute).ToString('yyyy-MM-dd HH:mm')); runs = $runs.ToArray() } |
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
# The verdict rule of section 7 is in force since November 27, 2026: its
# heading says so, and no line calls it proposed or report-only.
Assert-That (@($r.Lines | Where-Object { $_ -eq 'Verdict on the budgets (method, section 7, in force since November 27, 2026)' }).Count -eq 1) 'the verdict heading says the rule is in force'
Assert-That (@($r.Lines | Where-Object { $_ -match '(?i)proposed' }).Count -eq 0) 'no line calls the verdict rule proposed'
# An enforced primary row gets its budget verdict, for each build.
Assert-That (@($r.Lines | Where-Object { $_ -like "    ${primary}: UI * ms against 250 ms, *. Beside it: *" }).Count -eq 2) 'an enforced primary row is judged against its budget'
# Run UI failed the validation: every line that names it says it is not validated.
$runUiLines = @($r.Lines | Where-Object { $_ -like '*Run UI*' })
Assert-That ($runUiLines.Count -ge 3 -and @($runUiLines | Where-Object { $_ -notlike '*not validated*' }).Count -eq 0) "every line that names Run UI says it is not validated ($(@($runUiLines | Where-Object { $_ -notlike '*not validated*' }).Count) do not)"

# 3. 8 of 10 lower: one short of 9.
$eight = New-Sitting 'eight-of-ten' 10 (New-Pairs 10 -higher 3, 7)
$r = Invoke-Check '8 of 10 lower' @($eight)
Assert-That ($r.Verdict -eq 'not shown' -and $r.Lower -eq 8 -and $r.Need -eq 9) "not shown, 8 of 10 (got $($r.Verdict), $($r.Lower))"
# The P printed beside the count seen is that count's, apart from the P of the count needed.
Assert-That (@($r.Lines | Where-Object { $_ -like '*after lower in 8 of 10 pairs (P of at least 8 = 0.055); a gain needs 9 of 10 (P = 0.011).*' }).Count -eq 1) 'the P of the count seen and the P of the count needed are printed apart'

# 4. A disturbed pair is replaced. Pair 4 (after lower) is disturbed; pairs 7
# and 9 are higher; pair 11 is the replacement. Kept: 10 pairs, 8 lower: not
# shown. Had the disturbed pair been counted, 9 of 11 would have passed.
$pairs = New-Pairs 11 -higher 7, 9
$pairs[3]['ACond'] = New-Conditions -disturbed $true -others 11.2
$r = Invoke-Check 'a disturbed pair, replaced' @(New-Sitting 'disturbed-replaced' 10 $pairs)
Assert-That ($r.Verdict -eq 'not shown' -and $r.Lower -eq 8 -and $r.Pairs -eq 10 -and $r.Replaced -eq 1) "not shown, 8 of 10 kept, 1 replaced (got $($r.Verdict), $($r.Lower) of $($r.Pairs), $($r.Replaced) replaced)"
Assert-That (@($r.Lines | Where-Object { $_ -like '*dropped: pair 4 after: disturbed*' }).Count -eq 1) 'the dropped run is listed with its reason'

# 4b. The same, as the script runs it: pair 4's first run (after) is
# disturbed, so its second run (before) is not spent.
$pairs = New-Pairs 11 -higher 7, 9
$pairs[3]['ACond'] = New-Conditions -disturbed $true -others 11.2
$pairs[3]['SkipSecond'] = $true
$r = Invoke-Check 'a disturbed first run, its second not run' @(New-Sitting 'disturbed-skipped' 10 $pairs)
Assert-That ($r.Verdict -eq 'not shown' -and $r.Lower -eq 8 -and $r.Pairs -eq 10 -and $r.Replaced -eq 1) "not shown, 8 of 10 kept, 1 replaced (got $($r.Verdict), $($r.Lower) of $($r.Pairs), $($r.Replaced) replaced)"
Assert-That (@($r.Lines | Where-Object { $_ -like '*dropped: pair 4 before: not run*' }).Count -eq 1) 'the run not spent is listed'

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
# Run UI failed the validation of November 27, 2026, so it is never part of
# a verdict: the gain is shown, and the Run UI count is printed beside it.
$pairs = New-Pairs 10 -higher 7
foreach ($p in $pairs) { $p['BRun'] = 100; $p['ARun'] = 120 }
$r = Invoke-Check 'Run UI the other way' @(New-Sitting 'run-ui-other-way' 10 $pairs)
Assert-That ($r.Verdict -eq 'gain shown' -and $r.RunUiHigher -eq 10 -and $r.RunUiPairs -eq 10) "gain shown, Run UI higher in 10 of 10 decides nothing (got $($r.Verdict), Run UI higher in $($r.RunUiHigher) of $($r.RunUiPairs))"
Assert-That (@($r.Lines | Where-Object { $_ -eq '  Run UI (not validated): after higher in 10 of 10 pairs that carry it. Report-only; it counts toward no verdict.' }).Count -eq 1) 'the Run UI count is printed, not validated, counting toward no verdict'
# Run UI far over the budget, UI within it: the budget verdict reads UI alone.
Assert-That (@($r.Lines | Where-Object { $_ -like "    ${primary}: UI * against 250 ms, within. Beside it: Run UI (not validated) 120 ms,*" }).Count -eq 1) 'the budget verdict reads UI, not Run UI'

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

# 13. The size floor is not rounded: before's median 150 ms gives a floor of
# 4.5 ms, and a median difference of 4.2 ms is under it (rounded to 4 ms, it
# would pass).
$r = Invoke-Check 'the floor, not rounded' @(New-Sitting 'floor-rounding' 10 @(for ($p = 1; $p -le 10; $p++) { @{ B = 150; A = 145.8 } }))
Assert-That ($r.Verdict -eq 'not shown' -and $r.Lower -eq 10 -and [Math]::Abs($r.FloorMs - 4.5) -lt 1e-9) "not shown under a floor of 4.5 ms (got $($r.Verdict), floor $($r.FloorMs))"

# 14. A sitting given twice, or a copy of it, is refused: its pairs would
# count twice (two 8 of 10 would read 16 of 20).
foreach ($case in @(@{ Title = 'the same folder twice'; Folders = @($eight, $eight) }, @{ Title = 'a copy of a sitting'; Folders = @($eight, (Join-Path $WorkFolder 'eight-of-ten-copy')) })) {
    if ($case.Folders[1] -ne $eight) { Copy-Item -LiteralPath $eight -Destination $case.Folders[1] -Recurse }
    $refused = $false
    try { & $pairsScript -Analyze $case.Folders[0] -Pool $case.Folders[1] -PassThru | Out-Null } catch { $refused = $_.Exception.Message -like '*given twice*' }
    Assert-That $refused "$($case.Title) is refused"
}

# 15. A sitting stopped short is not decided, even at 9 of 10: 20 planned.
$r = Invoke-Check 'stopped at 10 of 20 planned' @(New-Sitting 'incomplete' 20 (New-Pairs 10 -higher 7))
Assert-That ($r.Verdict -eq 'not decided' -and $r.Lower -eq 9) "not decided at 9 of 10 of 20 planned (got $($r.Verdict), $($r.Lower))"

# 16. Stopped between the two runs of the last pair: not finished, not a
# replacement, and not decided.
$pairs = New-Pairs 10 -higher 7
$pairs[9]['OmitSecond'] = $true
$r = Invoke-Check 'the last pair not finished' @(New-Sitting 'not-finished' 10 $pairs)
Assert-That ($r.Verdict -eq 'not decided' -and $r.Replaced -eq 0 -and $r.NotFinished -eq 1) "not decided, 0 replaced, 1 not finished (got $($r.Verdict), $($r.Replaced), $($r.NotFinished))"

# 17. A run without the primary row, or with its UI null, is dropped before
# any timing is read and its pair replaced and listed; so is a run that timed
# out. Pairs 3, 5 and 8 are replaced by 11 to 13; pair 7 is higher: 9 of 10.
$pairs = New-Pairs 13 -higher 7
$pairs[2]['ANoPrimary'] = $true
$pairs[4]['BUiNull'] = $true
$pairs[7]['ATimedOut'] = $true
$r = Invoke-Check 'a row missing, a UI null, a run timed out' @(New-Sitting 'row-missing' 10 $pairs)
Assert-That ($r.Verdict -eq 'gain shown' -and $r.Pairs -eq 10 -and $r.Lower -eq 9 -and $r.Replaced -eq 3) "gain shown, 9 of 10, 3 replaced (got $($r.Verdict), $($r.Lower) of $($r.Pairs), $($r.Replaced) replaced)"
Assert-That (@($r.Lines | Where-Object { $_ -like "*pair 3: row $primary missing from after*" }).Count -eq 1) 'the missing row is listed'
Assert-That (@($r.Lines | Where-Object { $_ -like "*pair 5 before: row $primary has no UI figure*" }).Count -eq 1) 'the null UI figure is listed'
Assert-That (@($r.Lines | Where-Object { $_ -like '*pair 8 after: timed out*' }).Count -eq 1) 'the run that timed out is listed'

# 18. A run that outlasted its load is dropped, and listed apart as a drop
# that depends on how long the run took.
$pairs = New-Pairs 11 -higher 7
$pairs[1]['AOutlasted'] = $true
$r = Invoke-Check 'a run outlasted its load' @(New-Sitting 'outlasted' 10 $pairs)
Assert-That ($r.Replaced -eq 1 -and @($r.Lines | Where-Object { $_ -like '*1 runs outlasted their load*depends on how long*' }).Count -eq 1) "1 replaced, listed apart (got $($r.Replaced))"

# 19. Three sittings pooled: the summary says the chance of a false pass
# is past 5%.
$three = New-Sitting 'pool-3' 10 (New-Pairs 10 -higher 1, 4)
$r = Invoke-Check 'three sittings pooled' @($three, $two, $one)
Assert-That (@($r.Lines | Where-Object { $_ -like '*3 sittings are pooled*6.1% at three*past the 5%*' }).Count -eq 1) 'pooled sittings carry the chance of a false pass'

# 20. A report-only primary row (as for #404's search with Published
# hidden) is compared only between builds: the pairs rule judges it, and the
# verdict lines of section 7 give its UI with no budget verdict.
$r = Invoke-Check 'a report-only primary row' @(New-Sitting 'report-only-primary' 10 (New-Pairs 10 -higher 7) -primaryRule 'report-only')
Assert-That ($r.Verdict -eq 'gain shown' -and $r.Lower -eq 9) "the pairs rule still judges it: gain shown, 9 of 10 (got $($r.Verdict), $($r.Lower))"
Assert-That (@($r.Lines | Where-Object { $_ -like "    ${primary}: UI * against *" }).Count -eq 0) 'a report-only primary row gets no budget verdict'
Assert-That (@($r.Lines | Where-Object { $_ -like "    ${primary}: UI * ms. Report-only: compared only between builds, never with a budget. Beside it: Run UI (not validated) *" }).Count -eq 2) 'its line says it is compared only between builds, for each build'
Assert-That (@($r.Lines | Where-Object { $_ -like "    ${watch}: UI * ms against 250 ms, *" }).Count -eq 2) 'the enforced rows beside it keep their budget verdicts'

# 21. The load lasts what the mode needs: 60 s under a real-window run, 120 s
# under a headless run (every headless run under the memory load outlasted
# 60 s on November 27, 2026). Nothing is run.
Write-Host 'The load length by mode'
foreach ($case in @(@{ Mode = 'window'; Seconds = 60 }, @{ Mode = 'headless'; Seconds = 120 })) {
    $seconds = $null
    try { $seconds = & $pairsScript -LoadSecondsFor $case.Mode } catch { $seconds = "refused: $($_.Exception.Message)" }
    Assert-That ($seconds -eq $case.Seconds) "the load lasts $($case.Seconds) s under a $($case.Mode) run (got $seconds)"
}

# 22. The load (Start-BoardTrialLoad.ps1), read without starting it: -Seconds
# goes up to 120, long enough for a headless run under the memory load, and
# no further, so the load still ends by itself soon; -Threads runs from 1 to
# 6, and is 6, the method's number, unless set.
Write-Host 'The load script'
$loadCommand = Get-Command (Join-Path $PSScriptRoot 'Start-BoardTrialLoad.ps1')
$rangeOf = { param($name) @($loadCommand.Parameters[$name].Attributes | Where-Object { $_ -is [Management.Automation.ValidateRangeAttribute] }) }
$secondsRange = @(& $rangeOf 'Seconds'); $threadsRange = @(& $rangeOf 'Threads')
Assert-That ($secondsRange.Count -eq 1 -and [int] $secondsRange[0].MinRange -eq 1 -and [int] $secondsRange[0].MaxRange -eq 120) "-Seconds runs from 1 to 120 (got $(@($secondsRange | ForEach-Object { "$($_.MinRange) to $($_.MaxRange)" }) -join ', '))"
Assert-That ($threadsRange.Count -eq 1 -and [int] $threadsRange[0].MinRange -eq 1 -and [int] $threadsRange[0].MaxRange -eq 6) "-Threads runs from 1 to 6 (got $(@($threadsRange | ForEach-Object { "$($_.MinRange) to $($_.MaxRange)" }) -join ', '))"
$threadsDefault = @($loadCommand.ScriptBlock.Ast.ParamBlock.Parameters | Where-Object { $_.Name.VariablePath.UserPath -eq 'Threads' } | ForEach-Object { if ($null -ne $_.DefaultValue) { $_.DefaultValue.Extent.Text } })
Assert-That ($threadsDefault.Count -eq 1 -and $threadsDefault[0] -eq '6') "-Threads is 6 unless set (got $($threadsDefault -join ', '))"

if ($failures.Count) {
    Write-Host "Board pairs check: $($failures.Count) failed."
    exit 1
}
Write-Host 'Board pairs check: every case gave the verdict the rule gives by hand.'
