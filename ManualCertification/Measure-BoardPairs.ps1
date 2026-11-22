#requires -Version 7
<#
.SYNOPSIS
Compares two builds of the Board trial in alternating pairs and applies the merge rule.
.DESCRIPTION
The comparison of the measurement method (docs/design/Board-Trial-Measurement-Method.md,
sections 5 and 7). Two builds take turns in one sitting (before, after; after,
before; ...) so that drift of the PC's load falls on both alike. Each run is
its own process and writes one JSON file:

  -Mode window    the trial's own "--measure" mode: a real window on a read-only
                  snapshot of the test library (-Library). Real-window sittings
                  are run on the development PC by the maintainer or the
                  coordinating session, never by automated agents or CI.
  -Mode headless  the headless budget test alone (BoardBudgetTests), run with
                  "dotnet test --no-build" in each build's tree. It makes its own
                  500 library. Headless sittings are screening only.

There is no quiet gate: the script never waits for the PC to calm down. It
records the load of every run and replaces a pair afterwards when a run was
disturbed (other programs left fewer than two processors free), wrote no
JSON or ended in error. That is decided from those facts alone, before any
timing is read. At most 5 pairs are replaced; past that the sitting is void.

Then the rule, on UI (the verdict figure): the pairs where after is lower,
ties counting against, against the exact one-sided 5% count of a fair coin;
the size floor; Run UI must not point the other way; the watch rows; and,
report-only, the verdict rule proposed in the method's section 7.

-Analyze reads a sitting's folder again without running anything, so the
rule can be checked on saved or canned files. -Pool adds earlier sittings of
the same change and mode: their pairs are counted together with this one's.

The JSON files name the computer, so they stay on the PC that ran them. The
summary (summary.md beside them) names no computer and no folder.
.EXAMPLE
pwsh -NoProfile -File ./ManualCertification/Measure-BoardPairs.ps1 -Mode headless -Before <tree> -After <tree> -Out <folder> -Pairs 10 -PrimaryRow stage-back-to-all
.EXAMPLE
pwsh -NoProfile -File ./ManualCertification/Measure-BoardPairs.ps1 -Analyze <folder of a sitting> -Pool <folder of an earlier sitting>
#>
[CmdletBinding(DefaultParameterSetName = 'Run')]
param(
    [Parameter(Mandatory, ParameterSetName = 'Run')] [ValidateSet('window', 'headless')] [string] $Mode,
    # window: the two builds' PaperRoute.Desktop.exe. headless: the two built
    # trees, each the folder that holds PaperRoute.Desktop.slnx.
    [Parameter(Mandatory, ParameterSetName = 'Run')] [string] $Before,
    [Parameter(Mandatory, ParameterSetName = 'Run')] [string] $After,
    # window only: the test library the snapshot is taken from.
    [Parameter(ParameterSetName = 'Run')] [string] $Library,
    # A new or empty folder for this sitting's files.
    [Parameter(Mandatory, ParameterSetName = 'Run')] [string] $Out,
    [Parameter(ParameterSetName = 'Run')] [ValidatePattern('^[A-Za-z0-9_-]+$')] [string] $BeforeName = 'before',
    [Parameter(ParameterSetName = 'Run')] [ValidatePattern('^[A-Za-z0-9_-]+$')] [string] $AfterName = 'after',
    # 10 pairs for an expected gain of 20% or more; 20 for 10 to 15%, and 20
    # on a busy day whenever the expected gain is under 30%.
    [Parameter(ParameterSetName = 'Run')] [ValidateRange(1, 100)] [int] $Pairs = 10,
    # The one row named before the runs, expected to be lower after.
    [Parameter(Mandatory, ParameterSetName = 'Run')] [string] $PrimaryRow,
    # Rows named before the runs as expected to rise: reported, never blocking.
    [Parameter(ParameterSetName = 'Run')] [string[]] $ExpectedToRise = @(),
    # A known extra load (Start-BoardTrialLoad.ps1) under every "after" run
    # only: with the same build on both sides, the pairs are the validation's
    # R and S (or M) runs.
    [Parameter(ParameterSetName = 'Run')] [ValidateSet('None', 'Spin', 'Memory')] [string] $Load = 'None',
    # A sitting's folder to read again; nothing is run.
    [Parameter(Mandatory, ParameterSetName = 'Analyze')] [string] $Analyze,
    # Earlier sittings of the same change and mode, pooled into one count.
    [Parameter(ParameterSetName = 'Run')] [Parameter(ParameterSetName = 'Analyze')] [string[]] $Pool = @(),
    # Prints the counts the sign test needs for 1 to 40 pairs, and runs nothing.
    [Parameter(Mandatory, ParameterSetName = 'SignTable')] [switch] $SignTable,
    # Return the result as an object (with the summary's lines) instead of printing the lines.
    [switch] $PassThru
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# One alpha everywhere: 0.05, one-sided. A sitting of fewer pairs never decides.
$Alpha = 0.05
$MinimumPairs = 10
# At most this many pairs are replaced; one more and the sitting is void.
$MostReplacements = 5
$SittingFile = 'sitting.json'
$TestName = 'PaperRoute.Desktop.Tests.BoardBudgetTests.EveryRowIsMeasuredOnTheExampleAndTheFiveHundredLibrary'

function Get-Field($object, [string] $name) {
    if ($null -eq $object) { return $null }
    $property = $object.PSObject.Properties[$name]
    if ($null -eq $property) { return $null }
    return $property.Value
}

function Get-Median([double[]] $values) {
    if ($null -eq $values -or $values.Count -eq 0) { return $null }
    $sorted = [double[]]($values | Sort-Object)
    if ($sorted.Count % 2 -eq 1) { return $sorted[($sorted.Count - 1) / 2] }
    return ($sorted[$sorted.Count / 2 - 1] + $sorted[$sorted.Count / 2]) / 2
}

function Format-Ms($value) {
    if ($null -eq $value) { return 'absent' }
    return ([double] $value).ToString('0.#', [Globalization.CultureInfo]::InvariantCulture)
}

# "12.5 ms", or "absent" with no unit.
function Format-MsUnit($value) {
    if ($null -eq $value) { return 'absent' }
    return "$(Format-Ms $value) ms"
}

function Format-Number($value, [string] $format) {
    if ($null -eq $value) { return 'absent' }
    return ([double] $value).ToString($format, [Globalization.CultureInfo]::InvariantCulture)
}

# P(X >= k) for X ~ Binomial(n, 1/2), exact, as a fraction of 2^n.
function Get-UpperTail([int] $n, [int] $k) {
    $sum = [Numerics.BigInteger]::Zero
    $choose = [Numerics.BigInteger]::One
    for ($i = 0; $i -le $n; $i++) {
        if ($i -ge $k) { $sum += $choose }
        $choose = $choose * ($n - $i) / ($i + 1)
    }
    return [double] $sum / [double] [Numerics.BigInteger]::Pow(2, $n)
}

# The smallest count k of n with P(X >= k) <= alpha for a fair coin, exact
# binomial; null when no count reaches it. Integers only, so the edge is exact.
function Get-SignNeed([int] $n) {
    if ($n -lt 1) { return $null }
    $total = [Numerics.BigInteger]::Pow(2, $n)
    $tails = [Numerics.BigInteger[]]::new($n + 2)
    $choose = [Numerics.BigInteger]::One
    $coefficients = [Numerics.BigInteger[]]::new($n + 1)
    for ($i = 0; $i -le $n; $i++) { $coefficients[$i] = $choose; $choose = $choose * ($n - $i) / ($i + 1) }
    $tails[$n + 1] = [Numerics.BigInteger]::Zero
    for ($i = $n; $i -ge 0; $i--) { $tails[$i] = $tails[$i + 1] + $coefficients[$i] }
    # alpha = 1/20: P(X >= k) <= 1/20  <=>  20 * tail <= 2^n.
    $scale = [Numerics.BigInteger]::new([int] [Math]::Round(1 / $Alpha))
    for ($k = 0; $k -le $n; $k++) {
        if ($scale * $tails[$k] -le $total) { return $k }
    }
    return $null
}

# The pairs where a is above b (higher), or b above a (lower), with ties
# counting against: a tie is neither.
function Get-Count($pairs, [string] $id, [string] $field, [string] $direction) {
    $count = 0; $seen = 0
    foreach ($pair in $pairs) {
        $b = Get-Field (Get-Field $pair.Before.Rows $id) $field
        $a = Get-Field (Get-Field $pair.After.Rows $id) $field
        if ($null -eq $b -or $null -eq $a) { continue }
        $seen++
        if ($direction -eq 'lower' -and [double] $a -lt [double] $b) { $count++ }
        if ($direction -eq 'higher' -and [double] $a -gt [double] $b) { $count++ }
    }
    $need = if ($seen -ge $MinimumPairs) { Get-SignNeed $seen } else { $null }
    [pscustomobject]@{ Count = $count; Pairs = $seen; Need = $need; Reached = ($null -ne $need -and $count -ge $need) }
}

# The figure of one row in each run where it is present, as a list that may be empty.
function Get-Values($runs, [string] $id, [string] $field) {
    $values = @(foreach ($run in $runs) {
        $value = Get-Field (Get-Field $run.Rows $id) $field
        if ($null -ne $value) { [double] $value }
    })
    return , ([double[]] $values)
}

function Get-RowIds($rows) {
    $rows.PSObject.Properties | ForEach-Object Name
}

# What makes a run unusable, decided from facts that are not timings: no
# JSON, an error, no rows of the library held, a load that ended before the
# run, or a disturbed PC. Null when the run is usable. A disturbed of null
# (load unknown) counts as not disturbed.
function Get-DropReason($run, [string] $mode) {
    if ($run.Skipped) { return 'not run: its pair was already to be replaced' }
    if (-not $run.Written) { return 'wrote no JSON' }
    if ($null -eq $run.Json) { return 'JSON unreadable' }
    if ($mode -eq 'window' -and $run.Exit -ne 0) { return "ended in error (exit $($run.Exit))" }
    if ($null -ne (Get-Field $run.Json 'error')) { return 'ended in error' }
    if (@(Get-RowIds $run.Rows).Count -eq 0) { return 'no rows of the library held' }
    if ($run.LoadOutlasted) { return 'outlasted its load' }
    if ((Get-Field (Get-Field $run.Json 'conditions') 'disturbed') -eq $true) { return 'disturbed' }
    return $null
}

function Get-LoadText($run) {
    $conditions = Get-Field $run.Json 'conditions'
    $others = Get-Field $conditions 'othersBusyProcessors'
    $share = Get-Field $conditions 'notRunShare'
    $disturbed = Get-Field $conditions 'disturbed'
    $processors = Get-Field $run.Json 'processors'
    $busy = if ($null -eq $others) { 'others busy unknown' } else { "others busy $(Format-Number $others '0.0') of $processors" }
    $state = if ($null -eq $disturbed) { 'load unknown' } elseif ($disturbed) { 'disturbed' } else { 'not disturbed' }
    "$busy, not running $(if ($null -eq $share) { 'absent' } else { Format-Number $share '0%' }), $state"
}

# One run of a sitting's record with its JSON file, and why it is dropped
# (null when it is kept).
function Read-Run($entry, [string] $folder, [string] $mode) {
    $path = Join-Path $folder $entry.file
    $written = Test-Path -LiteralPath $path -PathType Leaf
    $json = $null
    if ($written) { try { $json = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json } catch { $json = $null } }
    $rows = [pscustomobject]@{}
    if ($null -ne $json) {
        $held = [string] (Get-Field $json 'heldLibrary')
        foreach ($row in @(Get-Field $json 'rows')) {
            if ($null -eq $row) { continue }
            if ($held -ne '' -and (Get-Field $row 'library') -ne $held) { continue }
            if ($null -eq $rows.PSObject.Properties[$row.id]) { $rows | Add-Member -NotePropertyName $row.id -NotePropertyValue $row }
        }
    }
    $run = [pscustomobject]@{
        Pair = [int] $entry.pair; Order = [int] $entry.order; Name = [string] $entry.name
        Exit = Get-Field $entry 'exit'; LoadOutlasted = [bool] (Get-Field $entry 'loadOutlasted'); Skipped = [bool] (Get-Field $entry 'skipped')
        Written = $written; Json = $json; Rows = $rows; Drop = $null
    }
    $run.Drop = Get-DropReason $run $mode
    return $run
}

# Reads one sitting's record and its JSON files. Only what the rule needs is
# kept from each file: the rows of the library held, by id, and its facts.
function Read-Sitting([string] $folder) {
    $recordPath = Join-Path $folder $SittingFile
    if (-not (Test-Path -LiteralPath $recordPath -PathType Leaf)) { throw "A sitting's folder holds $SittingFile; one given does not." }
    try { $record = Get-Content -LiteralPath $recordPath -Raw | ConvertFrom-Json } catch { throw "A sitting's $SittingFile could not be read as JSON." }
    $runs = @(foreach ($entry in @($record.runs)) { Read-Run $entry $folder $record.mode })
    $runs = @($runs | Sort-Object Order)
    $sitting = [pscustomobject]@{
        Record = $record; Runs = $runs; Kept = @(); Dropped = @(); Replaced = 0; Void = $false; Incomplete = $false
    }
    foreach ($number in @($runs | ForEach-Object Pair | Sort-Object -Unique)) {
        $b = @($runs | Where-Object { $_.Pair -eq $number -and $_.Name -eq $record.beforeName })
        $a = @($runs | Where-Object { $_.Pair -eq $number -and $_.Name -eq $record.afterName })
        if ($b.Count -eq 1 -and $a.Count -eq 1 -and $null -eq $b[0].Drop -and $null -eq $a[0].Drop) {
            $sitting.Kept += [pscustomobject]@{ Pair = $number; Before = $b[0]; After = $a[0] }
        } else {
            $sitting.Replaced++
            foreach ($run in @($b) + @($a)) {
                $reason = if ($null -ne $run.Drop) { $run.Drop } else { 'its pair was replaced' }
                $sitting.Dropped += "pair $number $($run.Name): $reason"
            }
            if ($b.Count -eq 0 -or $a.Count -eq 0) { $sitting.Dropped += "pair $number`: a run is missing from the record" }
        }
    }
    $sitting.Void = $sitting.Replaced -gt $MostReplacements
    $sitting.Incomplete = -not $sitting.Void -and $sitting.Kept.Count -lt [int] $record.pairs
    return $sitting
}

# The verdict rule proposed in the method's section 7, for one build of one
# sitting, report-only: 5 runs that are not disturbed, from at most 10 tries.
function Get-ProposedLines($sitting, [string] $name, [string] $primary) {
    $lines = [Collections.Generic.List[string]]::new()
    $tries = @($sitting.Runs | Where-Object { $_.Name -eq $name -and -not $_.Skipped } | Select-Object -First 10)
    $usable = @($tries | Where-Object { $null -eq $_.Drop } | Select-Object -First 5)
    $unknown = @($usable | Where-Object { $null -eq (Get-Field (Get-Field $_.Json 'conditions') 'disturbed') }).Count
    if ($usable.Count -lt 5) {
        $lines.Add("  ${name}: too busy to judge: $($usable.Count) usable runs not disturbed, of $($tries.Count) tried; 5 are needed.")
        foreach ($run in $tries) { $lines.Add("    pair $($run.Pair): $(if ($null -ne $run.Json) { Get-LoadText $run } else { 'no figures' })$(if ($null -ne $run.Drop -and $run.Drop -ne 'disturbed') { "; $($run.Drop)" })") }
        return $lines
    }
    $lines.Add("  ${name}: the first 5 runs not disturbed, of $($tries.Count) tried ($unknown with load unknown). Median UI against the budget:")
    $first = $usable[0]
    $multiplier = [double] (Get-Field $first.Json 'budgetMultiplier')
    if ($multiplier -le 0) { $multiplier = 1 }
    foreach ($id in Get-RowIds $first.Rows) {
        $row = $first.Rows.$id
        if ((Get-Field $row 'rule') -eq 'report-only' -and $id -ne $primary) { continue }
        $ui = Get-Median (Get-Values $usable $id 'uiMs')
        $budget = [double] (Get-Field $row 'budgetMs') * $multiplier
        $verdict = if ($null -eq $ui) { 'absent' } elseif ($ui -le $budget) { 'within' } else { 'over' }
        $lines.Add("    ${id}: UI $(Format-MsUnit $ui) against $(Format-MsUnit $budget), $verdict. Beside it: Run UI $(Format-MsUnit (Get-Median (Get-Values $usable $id 'runUiMs'))), fast-core share $(Format-Number (Get-Median (Get-Values $usable $id 'perfCoreShare')) '0.00'), GC $(Format-MsUnit (Get-Median (Get-Values $usable $id 'gcPauseMs'))), compilation $(Format-MsUnit (Get-Median (Get-Values $usable $id 'jitMs'))).")
    }
    return $lines
}

function Get-Facts($run) {
    $json = $run.Json
    "build $(Get-Field $json 'build'), option $(Get-Field $json 'option'), dpi $(Get-Field $json 'dpi'), $(Get-Field $json 'processors') processors, day $(Get-Field $json 'day')"
}

# The summary of a sitting, with earlier sittings pooled. Names no computer and no folder.
function Get-Summary($sittings) {
    $current = $sittings[0]
    $record = $current.Record
    $bName = [string] $record.beforeName; $aName = [string] $record.afterName
    $primary = [string] $record.primaryRow
    $rise = @($record.expectedToRise | Where-Object { $_ })
    foreach ($earlier in $sittings | Select-Object -Skip 1) {
        foreach ($field in 'mode', 'primaryRow', 'beforeName', 'afterName', 'load') {
            if ([string] (Get-Field $earlier.Record $field) -ne [string] (Get-Field $record $field)) { throw "A pooled sitting differs in $field; only sittings of the same change, mode and primary row are pooled." }
        }
    }
    $counted = @($sittings | Where-Object { -not $_.Void })
    $pairs = @($counted | ForEach-Object { $_.Kept })
    $lines = [Collections.Generic.List[string]]::new()
    $lines.Add("Board pairs: $($record.mode) mode, $bName against $aName, order alternating, $($record.pairs) pairs planned per sitting.")
    if ($record.mode -eq 'headless') { $lines.Add('Headless sittings are screening only: the real window decides.') }
    if ($record.load -ne 'None') { $lines.Add("Load: $($record.load) under every $aName run. The counts compare the PC with and without the added load, not two builds; others busy includes the added load.") }
    $lines.Add("Primary row (named before the runs): $primary, expected lower. Expected to rise: $(if ($rise.Count) { $rise -join ', ' } else { 'none' }).")
    $lines.Add('The deciding sitting is the one the pull request named before the runs; every sitting is listed in it.')
    $index = 0
    foreach ($sitting in $sittings) {
        $index++
        $state = if ($sitting.Void) { "void: $($sitting.Replaced) pairs to replace, more than $MostReplacements; not counted" } elseif ($sitting.Incomplete) { "incomplete: $($sitting.Kept.Count) of $($sitting.Record.pairs) pairs kept" } else { "$($sitting.Kept.Count) pairs kept" }
        $label = if ($index -eq 1) { 'this sitting' } else { "earlier sitting $($index - 1)" }
        $lines.Add("Sitting $index ($label, started $($sitting.Record.startedAt)): $state, $($sitting.Replaced) replaced.")
        foreach ($drop in $sitting.Dropped) { $lines.Add("  dropped: $drop") }
    }
    $firstB = @($current.Runs | Where-Object { $_.Name -eq $bName -and $null -ne $_.Json } | Select-Object -First 1)
    $firstA = @($current.Runs | Where-Object { $_.Name -eq $aName -and $null -ne $_.Json } | Select-Object -First 1)
    if ($firstB.Count) { $lines.Add("${bName}: $(Get-Facts $firstB[0])") }
    if ($firstA.Count) { $lines.Add("${aName}: $(Get-Facts $firstA[0])") }
    $lines.Add('')

    # Every row of the library held, over the counted pairs.
    $ids = [Collections.Generic.List[string]]::new()
    foreach ($pair in $pairs) { foreach ($id in Get-RowIds $pair.Before.Rows) { if (-not $ids.Contains($id)) { $ids.Add($id) } } }
    $lines.Add("| row | budget | rule | $bName UI | $aName UI | $aName lower | $bName Run UI | $aName Run UI | $aName Run UI lower |")
    $lines.Add('|---|---|---|---|---|---|---|---|---|')
    $befores = @($pairs | ForEach-Object { $_.Before }); $afters = @($pairs | ForEach-Object { $_.After })
    foreach ($id in $ids) {
        $row = $pairs | ForEach-Object { Get-Field $_.Before.Rows $id } | Where-Object { $null -ne $_ } | Select-Object -First 1
        $bu = Get-Values $befores $id 'uiMs'; $au = Get-Values $afters $id 'uiMs'
        $lower = Get-Count $pairs $id 'uiMs' 'lower'; $runLower = Get-Count $pairs $id 'runUiMs' 'lower'
        $range = { param($v) if ($v.Count) { "$(Format-Ms (Get-Median $v)) ($(Format-Ms ($v | Measure-Object -Minimum).Minimum) to $(Format-Ms ($v | Measure-Object -Maximum).Maximum))" } else { 'absent' } }
        $lines.Add("| $id | $(Get-Field $row 'budgetMs') | $(Get-Field $row 'rule') | $(& $range $bu) | $(& $range $au) | $($lower.Count) of $($lower.Pairs) | $(Format-Ms (Get-Median (Get-Values $befores $id 'runUiMs'))) | $(Format-Ms (Get-Median (Get-Values $afters $id 'runUiMs'))) | $($runLower.Count) of $($runLower.Pairs) |")
    }
    $lines.Add('')

    # The rule (method, section 5) on the primary row.
    $result = [ordered]@{
        Verdict = ''; Lower = 0; Pairs = $pairs.Count; Need = $null; MedianDifferenceMs = $null; FloorMs = $null
        RunUiHigher = 0; RunUiPairs = 0; RunUiStop = $false; Replaced = ($sittings | Measure-Object -Property Replaced -Sum).Sum
        WatchHigher = @(); WatchBlocking = @(); Void = ($counted.Count -eq 0)
    }
    $lines.Add("Rule on $primary (UI; ties count against; one-sided 5%, exact binomial):")
    $known = @($pairs | Where-Object { $null -ne (Get-Field $_.Before.Rows $primary) -and $null -ne (Get-Field $_.After.Rows $primary) })
    $count = Get-Count $pairs $primary 'uiMs' 'lower'
    $result.Lower = $count.Count; $result.Pairs = $count.Pairs; $result.Need = $count.Need
    $differences = [double[]] @($known | ForEach-Object { [double] $_.Before.Rows.$primary.uiMs - [double] $_.After.Rows.$primary.uiMs })
    $ratios = [double[]] @($known | Where-Object { [double] $_.Before.Rows.$primary.uiMs -gt 0 } | ForEach-Object { [double] $_.After.Rows.$primary.uiMs / [double] $_.Before.Rows.$primary.uiMs } | Sort-Object)
    $beforeMedian = Get-Median ([double[]] @($known | ForEach-Object { [double] $_.Before.Rows.$primary.uiMs }))
    $afterMedian = Get-Median ([double[]] @($known | ForEach-Object { [double] $_.After.Rows.$primary.uiMs }))
    $difference = Get-Median $differences
    $floor = if ($null -ne $beforeMedian) { [Math]::Max(2, 0.03 * $beforeMedian) } else { $null }
    $result.MedianDifferenceMs = $difference; $result.FloorMs = $floor
    $lines.Add("  medians: $bName $(Format-MsUnit $beforeMedian), $aName $(Format-MsUnit $afterMedian).")
    $needText = if ($null -ne $count.Need) { "needs $($count.Need) (P = $(Format-Number (Get-UpperTail $count.Pairs $count.Need) '0.000'))" } else { "a decision needs at least $MinimumPairs pairs" }
    $lines.Add("  $aName lower in $($count.Count) of $($count.Pairs) pairs; $needText.")
    $lines.Add("  median difference ($bName - $aName) $(Format-MsUnit $difference); size floor $(Format-MsUnit $floor) (the larger of 2 ms and 3% of $bName's median).")
    if ($ratios.Count -ge 4) {
        $n = $ratios.Count
        $coverage = 1 - 2 * ($n + 1) / [Math]::Pow(2, $n)
        $lines.Add("  median ratio $aName / $bName $(Format-Number (Get-Median $ratios) '0.000'), from $(Format-Number $ratios[1] '0.000') to $(Format-Number $ratios[$n - 2] '0.000') (2nd smallest to 2nd largest, $(Format-Number (100 * $coverage) '0.0')%).")
    }
    $runUi = Get-Count $pairs $primary 'runUiMs' 'higher'
    $result.RunUiHigher = $runUi.Count; $result.RunUiPairs = $runUi.Pairs; $result.RunUiStop = $runUi.Reached
    $runUiNeed = if ($null -ne $runUi.Need) { "needs $($runUi.Need) to stop" } else { "too few pairs carry it to test" }
    $lines.Add("  Run UI (report-only): $aName higher in $($runUi.Count) of $($runUi.Pairs) pairs that carry it; $runUiNeed.")

    if ($result.Void) {
        $result.Verdict = 'void'
        $lines.Add('Verdict: void. Every sitting needed more than 5 replacements.')
    } elseif ($null -eq $count.Need) {
        $result.Verdict = 'not decided'
        $lines.Add("Verdict: not decided. $($count.Pairs) pairs; a decision needs at least $MinimumPairs.")
    } elseif ($runUi.Reached) {
        $result.Verdict = 'stop'
        $lines.Add("Verdict: stop. Run UI points the other way ($aName higher in $($runUi.Count) of $($runUi.Pairs)); a person looks before anything is merged.")
    } elseif (-not $count.Reached) {
        $result.Verdict = 'not shown'
        $lines.Add("Verdict: gain not shown. $aName lower in $($count.Count) of $($count.Pairs); $($count.Need) needed.")
    } elseif ($difference -lt $floor) {
        $result.Verdict = 'not shown'
        $lines.Add("Verdict: gain not shown. The count is reached, but the median difference $(Format-Ms $difference) ms is under the floor of $(Format-Ms $floor) ms.")
    } else {
        $result.Verdict = 'gain shown'
        $lines.Add("Verdict: gain shown on $primary.")
    }

    # Watch rows: each sitting on its own. Higher once: a new sitting for that
    # row. Higher again in another sitting: it blocks the merge.
    $lines.Add('')
    $lines.Add('Watch rows (every row not named; UI higher after, ties count against):')
    $flagged = 0
    foreach ($id in $ids) {
        if ($id -eq $primary) { continue }
        $higherIn = @(foreach ($sitting in $counted) { $c = Get-Count $sitting.Kept $id 'uiMs' 'higher'; if ($c.Reached) { "$($c.Count) of $($c.Pairs)" } })
        if ($rise -contains $id) {
            $c = Get-Count $pairs $id 'uiMs' 'higher'
            $lines.Add("  ${id} (expected to rise): $aName higher in $($c.Count) of $($c.Pairs) pairs. Reported; it does not block.")
            continue
        }
        if ($higherIn.Count -eq 0) { continue }
        $flagged++
        $result.WatchHigher += $id
        if ($higherIn.Count -ge 2) {
            $result.WatchBlocking += $id
            $lines.Add("  ${id}: higher again ($($higherIn -join '; ')). It blocks the merge.")
        } else {
            $lines.Add("  ${id}: higher ($($higherIn[0])). Run a new sitting for this row; it blocks only if higher again.")
        }
    }
    if ($flagged -eq 0) { $lines.Add('  none higher by the count.') }
    $lines.Add('Figures of two sittings or two days are never compared; only the pairs inside a sitting are.')

    # Section 7, report-only, on this sitting.
    $lines.Add('')
    $lines.Add('proposed verdict (report-only)')
    if ($record.mode -ne 'window') { $lines.Add('  The proposed rule is for real-window runs; these headless lines are for reading only.') }
    foreach ($name in $bName, $aName) { foreach ($line in Get-ProposedLines $current $name $primary) { $lines.Add($line) } }
    $lines.Add('  Whether the validation of Run UI passed is not recorded here (method, section 6).')

    $lines.Add('')
    $lines.Add('Runs of this sitting:')
    foreach ($run in $current.Runs) {
        $state = if ($null -ne $run.Drop) { "dropped: $($run.Drop)" } else { 'kept' }
        $lines.Add("  pair $($run.Pair) $($run.Name): $(if ($run.Written -and $null -ne $run.Json) { Get-LoadText $run } else { 'no figures' }); $state.")
    }
    $result.Lines = $lines.ToArray()
    return [pscustomobject] $result
}

function Save-Record([string] $folder, $record) {
    $record | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $folder $SittingFile) -Encoding utf8NoBOM
}

# One measured run, its own process. Returns the exit code and whether the
# load (if any) ended before the run did.
function Invoke-Run([string] $exeOrTree, [string] $file, [bool] $withLoad, [string] $folder) {
    if ($Mode -eq 'window') {
        $start = [Diagnostics.ProcessStartInfo]::new($exeOrTree)
        foreach ($argument in '--snapshot-from', $Library, '--measure', $file) { $start.ArgumentList.Add($argument) }
    } else {
        $results = Join-Path $folder ([IO.Path]::GetFileNameWithoutExtension($file))
        New-Item -ItemType Directory -Force -Path $results | Out-Null
        $start = [Diagnostics.ProcessStartInfo]::new('dotnet')
        $start.WorkingDirectory = $exeOrTree
        foreach ($argument in 'test', 'PaperRoute.Desktop.Tests/PaperRoute.Desktop.Tests.csproj', '--configuration', 'Release', '--no-build',
            '--filter', "FullyQualifiedName=$TestName", '--results-directory', $results) { $start.ArgumentList.Add($argument) }
        # The test writes budget-headless.json into the folder this names.
        $start.Environment['PAPERROUTE_TRIAL_PICTURES'] = $results
    }
    $start.UseShellExecute = $false
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    $loadProcess = $null
    $outlasted = $false
    try {
        if ($withLoad) {
            $loadProcess = & (Join-Path $PSScriptRoot 'Start-BoardTrialLoad.ps1') -Kind $Load -Seconds 60
            # The load starts 2 s before the measured process.
            [Threading.Thread]::Sleep(2000)
        }
        $process = [Diagnostics.Process]::Start($start)
        $output = $process.StandardOutput.ReadToEndAsync()
        $errors = $process.StandardError.ReadToEndAsync()
        $process.WaitForExit()
        $outlasted = $null -ne $loadProcess -and $loadProcess.HasExited
        $logName = [IO.Path]::GetFileNameWithoutExtension($file) + '.log'
        Set-Content -LiteralPath (Join-Path $folder $logName) -Value ($output.Result + $errors.Result) -Encoding utf8NoBOM
        $exit = $process.ExitCode
        $process.Dispose()
    } finally {
        if ($null -ne $loadProcess) {
            if (-not $loadProcess.HasExited) { $loadProcess.Kill($true) }
            $loadProcess.Dispose()
        }
    }
    if ($Mode -eq 'headless') {
        $written = Join-Path $results 'budget-headless.json'
        if (Test-Path -LiteralPath $written -PathType Leaf) { Move-Item -LiteralPath $written -Destination $file -Force }
    }
    return [pscustomobject]@{ Exit = $exit; LoadOutlasted = $outlasted }
}

if ($SignTable) {
    return @(foreach ($n in 1..40) {
        $need = Get-SignNeed $n
        [pscustomobject]@{ Pairs = $n; Need = $need; P = if ($null -ne $need) { Get-UpperTail $n $need } else { $null }; PBelow = if ($null -ne $need -and $need -gt 0) { Get-UpperTail $n ($need - 1) } else { $null } }
    })
}

if ($PSCmdlet.ParameterSetName -eq 'Run') {
    if ($BeforeName -eq $AfterName) { throw '-BeforeName and -AfterName must differ.' }
    foreach ($pathName in 'Before', 'After') {
        if (-not (Test-Path -LiteralPath (Get-Variable $pathName -ValueOnly))) { throw "Nothing at the -$pathName path." }
        Set-Variable $pathName ([IO.Path]::GetFullPath((Get-Variable $pathName -ValueOnly), (Get-Location).ProviderPath))
    }
    if ($Mode -eq 'window') {
        foreach ($pathName in 'Before', 'After') {
            if (-not (Test-Path -LiteralPath (Get-Variable $pathName -ValueOnly) -PathType Leaf)) { throw "No program file at the -$pathName path." }
        }
        if (-not $Library -or -not (Test-Path -LiteralPath (Join-Path $Library 'data\manuscripts.json'))) { throw 'Window mode needs -Library, a test library folder.' }
        $Library = [IO.Path]::GetFullPath($Library)
    } else {
        if ($Library) { throw '-Library is for window mode; the headless test makes its own 500 library.' }
        foreach ($pathName in 'Before', 'After') {
            $tree = Get-Variable $pathName -ValueOnly
            if (-not (Test-Path -LiteralPath (Join-Path $tree 'PaperRoute.Desktop.slnx') -PathType Leaf)) { throw "The -$pathName path is not a tree that holds PaperRoute.Desktop.slnx." }
            if (-not (Test-Path -LiteralPath (Join-Path $tree 'PaperRoute.Desktop.Tests/bin/Release/net10.0/PaperRoute.Desktop.Tests.dll') -PathType Leaf)) { throw "The tree at the -$pathName path has no Release build of the trial's tests; build it first." }
        }
    }
    foreach ($id in @($PrimaryRow) + @($ExpectedToRise)) { if ($id -notmatch '^[a-z0-9-]+$') { throw 'Row ids are written like stage-back-to-all.' } }
    if ($ExpectedToRise -contains $PrimaryRow) { throw 'The primary row cannot also be expected to rise.' }
    New-Item -ItemType Directory -Force -Path $Out | Out-Null
    $Out = [IO.Path]::GetFullPath($Out)
    if (@(Get-ChildItem -LiteralPath $Out -Force).Count -ne 0) { throw 'The -Out folder must be new or empty: one sitting per folder.' }
    $poolSittings = @($Pool | ForEach-Object { Read-Sitting $_ })
    foreach ($earlier in $poolSittings) {
        if ($earlier.Record.mode -ne $Mode -or $earlier.Record.primaryRow -ne $PrimaryRow -or $earlier.Record.beforeName -ne $BeforeName -or $earlier.Record.afterName -ne $AfterName -or $earlier.Record.load -ne $Load) {
            throw 'A pooled sitting differs in mode, primary row, names or load; only sittings of the same change, mode and primary row are pooled.'
        }
    }

    $record = [ordered]@{
        mode = $Mode; beforeName = $BeforeName; afterName = $AfterName; pairs = $Pairs; primaryRow = $PrimaryRow
        expectedToRise = @($ExpectedToRise); load = $Load; startedAt = [DateTime]::Now.ToString('yyyy-MM-dd HH:mm'); runs = @()
    }
    Save-Record $Out $record
    $kept = 0; $replaced = 0; $pair = 0; $order = 0
    while ($kept -lt $Pairs -and $replaced -le $MostReplacements) {
        $pair++
        $sides = if ($pair % 2 -eq 1) { @(@($BeforeName, $Before), @($AfterName, $After)) } else { @(@($AfterName, $After), @($BeforeName, $Before)) }
        $usable = $true
        foreach ($side in $sides) {
            $order++
            $file = Join-Path $Out ('pair{0:00}-{1}.json' -f $pair, $side[0])
            if (-not $usable) {
                # The pair is replaced whatever its second run gives, so that run is not spent.
                $record.runs += [pscustomobject] [ordered]@{ pair = $pair; order = $order; name = $side[0]; file = [IO.Path]::GetFileName($file); exit = $null; seconds = 0; loadOutlasted = $false; skipped = $true }
                Save-Record $Out $record
                Write-Host "pair $pair $($side[0]): not run, the pair is already to be replaced"
                continue
            }
            $watch = [Diagnostics.Stopwatch]::StartNew()
            $ran = Invoke-Run $side[1] $file ($Load -ne 'None' -and $side[0] -eq $AfterName) $Out
            $watch.Stop()
            $entry = [ordered]@{ pair = $pair; order = $order; name = $side[0]; file = [IO.Path]::GetFileName($file); exit = $ran.Exit; seconds = [Math]::Round($watch.Elapsed.TotalSeconds, 1); loadOutlasted = $ran.LoadOutlasted }
            $record.runs += [pscustomobject] $entry
            Save-Record $Out $record
            # Only the facts that are not timings decide whether the pair is
            # kept (Get-DropReason), the same as when the sitting is read again.
            $run = Read-Run ([pscustomobject] $entry) $Out $Mode
            if ($null -ne $run.Drop) { $usable = $false }
            Write-Host "pair $pair $($side[0]): exit $($ran.Exit), $($entry.seconds) s, $(if ($null -ne $run.Json) { Get-LoadText $run } else { 'no figures' })$(if ($null -ne $run.Drop) { "; $($run.Drop), pair to be replaced" })"
        }
        if ($usable) { $kept++ } else { $replaced++ }
    }
    if ($replaced -gt $MostReplacements) { Write-Host "Void: more than $MostReplacements pairs needed replacing." }
    $folder = $Out
} else {
    $folder = $Analyze
    $poolSittings = @($Pool | ForEach-Object { Read-Sitting $_ })
}

$summary = Get-Summary (@(Read-Sitting $folder) + $poolSittings)
$summary.Lines | Set-Content -LiteralPath (Join-Path $folder 'summary.md') -Encoding utf8NoBOM
if ($PassThru) { return $summary }
$summary.Lines
