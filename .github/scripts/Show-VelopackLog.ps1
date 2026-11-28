# Prints the end of Velopack's own log on a CI runner and copies the log into
# a folder the job uploads. Report-only, for start-up G1's read before merge
# (start-up design, section 4, G1, and risk 6): VelopackApp.Build().Run() is
# the first statement of the new app's Main, and Velopack writes its log on
# every start, the tests' child processes included, outside the uploaded
# results. The read settles what the log holds (the design assumes no paths
# beyond the program's). The script never fails a step, and its steps go once
# that read is done.
#
# Where Velopack 1.2.161 writes the log: the temporary folder on Linux
# (Path.GetTempPath(), /tmp on the runner); ~/Library/Logs on macOS, with the
# temporary folder ($TMPDIR) as the fallback there. Each place is looked at.
param(
    [Parameter(Mandatory)] [string] $Out,
    [int] $Last = 80
)

$ErrorActionPreference = 'Continue'
$candidates = @(
    (Join-Path $HOME 'Library/Logs/velopack.log'),
    (Join-Path ([IO.Path]::GetTempPath()) 'velopack.log'),
    '/tmp/velopack.log'
) | Select-Object -Unique

$found = 0
foreach ($path in $candidates) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        continue
    }

    $found++
    $lines = @(Get-Content -LiteralPath $path)
    Write-Host "Velopack's log at $path`: $($lines.Count) lines; the last $([Math]::Min($Last, $lines.Count)) follow."
    $lines | Select-Object -Last $Last | ForEach-Object { Write-Host "  $_" }
    New-Item -ItemType Directory -Force -Path $Out | Out-Null
    Copy-Item -LiteralPath $path -Destination (Join-Path $Out "velopack-$found.log") -Force
}

if ($found -eq 0) {
    Write-Host "No Velopack log was found at: $($candidates -join ', ')."
}

exit 0
