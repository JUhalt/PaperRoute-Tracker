#requires -Version 7.2
<#
.SYNOPSIS
Exercise release upload verification with synthetic files and a strictly local gh mock.
.EXAMPLE
pwsh -NoProfile -File .\ManualCertification\Test-ReleaseAssetVerifier.ps1
.NOTES
No network, GitHub writes, installer launches, or application builds are performed.
Each case copies the current verifier into an isolated synthetic repository.
Reports and synthetic data remain under ignored artifacts/release-verifier-tests.
#>
[CmdletBinding()]
param(
    [ValidatePattern('^[A-Za-z0-9][A-Za-z0-9._-]*$')]
    [string] $RunName = ('run-' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss'))
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$sourceScript = Join-Path $repoRoot '.github/scripts/Verify-ReleaseAssets.ps1'
$testRoot = Join-Path $repoRoot 'artifacts/release-verifier-tests'
$runRoot = Join-Path $testRoot $RunName
$ancestor = $runRoot
while (-not [string]::IsNullOrEmpty($ancestor)) {
    if (Test-Path -LiteralPath $ancestor) {
        $item = Get-Item -LiteralPath $ancestor -Force
        if (-not $item.PSIsContainer -or ($item.Attributes -band [IO.FileAttributes]::ReparsePoint)) {
            throw 'Synthetic test output cannot traverse a file or reparse point.'
        }
    }
    $ancestor = [IO.Path]::GetDirectoryName($ancestor)
}
if (Test-Path -LiteralPath $runRoot) { throw 'Use a fresh synthetic run directory.' }
[void][IO.Directory]::CreateDirectory($runRoot)
$utf8 = [Text.UTF8Encoding]::new($false)
$results = [Collections.Generic.List[object]]::new()

function Write-Bytes([string] $Path, [byte[]] $Bytes) {
    [void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($Path))
    [IO.File]::WriteAllBytes($Path, $Bytes)
}
function Text-Bytes([string] $Text) { return ,$utf8.GetBytes($Text) }
function Byte-Hash([byte[]] $Bytes) {
    return [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($Bytes)).ToLowerInvariant()
}
function New-Case([string] $Name) {
    $root = Join-Path $runRoot $Name
    $script = Join-Path $root '.github/scripts/Verify-ReleaseAssets.ps1'
    Write-Bytes $script ([IO.File]::ReadAllBytes($sourceScript))
    foreach ($document in @('docs/releases/0.4.0.md','docs/USER_GUIDE.md','UPGRADE_NOTES.md','LICENSE.txt','NOTICE.md')) {
        Write-Bytes (Join-Path $root $document) (Text-Bytes "Synthetic $document`nContent for fixture $Name.`n")
    }
    $local = Join-Path $root 'Releases'
    $remote = [ordered]@{}
    $build = [Collections.Generic.List[object]]::new()
    $entries = [Collections.Generic.List[object]]::new()
    foreach ($item in @(
        @('JUhalt.PaperRouteTracker-0.4.0-stable-full.nupkg', 'Full'),
        @('JUhalt.PaperRouteTracker-0.4.0-stable-delta.nupkg', 'Delta'),
        @('JUhalt.PaperRouteTracker-stable-Setup.exe', 'Installer'),
        @('JUhalt.PaperRouteTracker-stable-Portable.zip', 'Portable'))) {
        $name = $item[0]; $bytes = Text-Bytes "Synthetic binary $name; never execute.`n"
        Write-Bytes (Join-Path $local $name) $bytes
        $remote[$name] = $bytes
        $build.Add([ordered]@{ RelativeFileName = $name; Type = $item[1] })
        if ($name.EndsWith('.nupkg')) {
            $entries.Add([ordered]@{
                PackageId = 'JUhalt.PaperRouteTracker'; Version = '0.4.0'; Type = $item[1]
                FileName = $name; SHA1 = 'synthetic-same-sha1'; SHA256 = (Byte-Hash $bytes).ToUpperInvariant()
                Size = $bytes.Length; NotesMarkdown = '# Synthetic notes'; NotesHTML = '<h1>Synthetic notes</h1>'
            })
        }
    }
    $seedName = 'JUhalt.PaperRouteTracker-0.3.0-stable-full.nupkg'
    $seedBytes = Text-Bytes 'Prior package seed, not uploaded.'
    Write-Bytes (Join-Path $local $seedName) $seedBytes
    $seed = [ordered]@{ PackageId = 'JUhalt.PaperRouteTracker'; Version = '0.3.0'; Type = 'Full'; FileName = $seedName; SHA256 = (Byte-Hash $seedBytes); Size = $seedBytes.Length }
    Write-Bytes (Join-Path $local 'assets.stable.json') (Text-Bytes (ConvertTo-Json -InputObject $build.ToArray() -Depth 6))
    Write-Bytes (Join-Path $local 'releases.stable.json') (Text-Bytes (ConvertTo-Json -InputObject @{ Assets = @($entries.ToArray()) + @($seed) } -Depth 6))
    # Actual uploaded bytes intentionally differ in formatting, ordering, and seed set.
    $remote['releases.stable.json'] = Text-Bytes (ConvertTo-Json -InputObject @{ Assets = @($entries[1], $entries[0]) } -Depth 6 -Compress)
    return [ordered]@{
        Name = $Name; Root = $root; Script = $script; Local = $local; Remote = $remote
        Stage = (Join-Path $root 'ReleaseAssets'); Evidence = (Join-Path $root 'ReleaseVerification')
        Release = [ordered]@{ databaseId = 404; isDraft = $true; isPrerelease = $false; tagName = 'v0.4.0'; name = 'PaperRoute Tracker v0.4.0'; body = [IO.File]::ReadAllText((Join-Path $root 'docs/releases/0.4.0.md')) }
        Calls = [Collections.Generic.List[string]]::new(); ApiCalls = 0; RaceOnApiCall = 0; OmitDigest = $false
    }
}

function gh {
    $arguments = @($args)
    # Resolve the fixture through the calling test scope; the verifier itself
    # executes in another script scope, so a script-qualified lookup is wrong.
    $mock = $ReleaseVerifierMock
    $global:LASTEXITCODE = 0
    $mock.Calls.Add($arguments -join ' ')
    if ($arguments[0] -ceq 'release' -and $arguments[1] -ceq 'view') {
        return ConvertTo-Json -InputObject $mock.Release -Depth 6 -Compress
    }
    if ($arguments[0] -ceq 'api' -and $arguments[1] -ceq 'repos/Synthetic/PaperRoute/releases/404/assets?per_page=100') {
        $mock.ApiCalls++
        $assets = [Collections.Generic.List[object]]::new()
        $id = 500
        foreach ($name in @($mock.Remote.Keys | Sort-Object)) {
            $id++
            $asset = [ordered]@{ id = $id; name = $name; size = $mock.Remote[$name].Length; state = 'uploaded'; updated_at = '2026-09-12T00:00:00Z' }
            if (-not $mock.OmitDigest) { $asset['digest'] = 'sha256:' + (Byte-Hash $mock.Remote[$name]) }
            if ($mock.RaceOnApiCall -gt 0 -and $mock.ApiCalls -eq $mock.RaceOnApiCall) { $asset.updated_at = '2026-09-12T01:00:00Z' }
            $assets.Add($asset)
        }
        $pages = [Collections.Generic.List[object]]::new()
        $pages.Add(@($assets.ToArray() | Select-Object -First 3))
        $pages.Add(@($assets.ToArray() | Select-Object -Skip 3))
        return ConvertTo-Json -InputObject $pages.ToArray() -Depth 8 -Compress
    }
    if ($arguments[0] -ceq 'release' -and $arguments[1] -ceq 'download') {
        $directoryIndex = [Array]::IndexOf($arguments, '--dir')
        if ($directoryIndex -lt 0) { throw 'Mock download requires --dir.' }
        $directory = [IO.Path]::GetFullPath($arguments[$directoryIndex + 1])
        if (-not $directory.StartsWith($mock.Evidence + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Mock download escaped case evidence.' }
        foreach ($name in $mock.Remote.Keys) { Write-Bytes (Join-Path $directory $name) $mock.Remote[$name] }
        return
    }
    throw "Mock rejected unsupported gh command; no real CLI fallback: $($arguments -join ' ')"
}

function Invoke-Verifier([object] $Case, [string] $Mode) {
    $script:ReleaseVerifierMock = $Case
    $parameters = @{
        Mode = $Mode; Repository = 'Synthetic/PaperRoute'; Version = '0.4.0'; Channel = 'stable'
        ReleaseDirectory = $Case.Local; StageDirectory = $Case.Stage; EvidenceDirectory = $Case.Evidence
    }
    if ($Mode -ceq 'Verify') { $parameters.ExpectedReleaseId = 404 }
    # Keep the real workflow output file untouched when this runner itself runs in CI.
    $priorOutput = $env:GITHUB_OUTPUT
    $env:GITHUB_OUTPUT = Join-Path $Case.Root 'github-output.txt'
    try {
        & $Case.Script @parameters *> (Join-Path $Case.Root "$Mode-output.log")
    } finally {
        $env:GITHUB_OUTPUT = $priorOutput
    }
}
function Publish-LocalStageToMock([object] $Case) {
    $Case.Remote.Clear()
    foreach ($file in Get-ChildItem -LiteralPath $Case.Stage -File) { $Case.Remote[$file.Name] = [IO.File]::ReadAllBytes($file.FullName) }
}
function Set-FeedField([object] $Case, [string] $Field, [object] $Value, [switch] $AlsoLocal) {
    $feed = $utf8.GetString($Case.Remote['releases.stable.json']) | ConvertFrom-Json
    ($feed.Assets | Where-Object Type -CEQ 'Full').$Field = $Value
    $Case.Remote['releases.stable.json'] = Text-Bytes (ConvertTo-Json -InputObject $feed -Depth 8 -Compress)
    if ($AlsoLocal) {
        $localPath = Join-Path $Case.Local 'releases.stable.json'
        $localFeed = Get-Content -LiteralPath $localPath -Raw | ConvertFrom-Json
        ($localFeed.Assets | Where-Object { $_.Version -ceq '0.4.0' -and $_.Type -ceq 'Full' }).$Field = $Value
        Write-Bytes $localPath (Text-Bytes (ConvertTo-Json -InputObject $localFeed -Depth 8))
    }
}
function Run-Case([string] $Name, [scriptblock] $Action, [string] $ExpectedError = '') {
    $case = New-Case $Name
    $started = [DateTime]::UtcNow
    try {
        & $Action $case
        if ($ExpectedError) { throw "TEST_EXPECTED_REJECTION: $ExpectedError" }
        $result = 'Passed'; $detail = 'Accepted expected valid input.'
    } catch {
        $message = $_.Exception.Message
        if ($ExpectedError -and $message -notlike 'TEST_EXPECTED_REJECTION:*' -and $message -like $ExpectedError) {
            $result = 'Passed'; $detail = "Rejected expected invalid input: $message"
        } else { $result = 'Failed'; $detail = $message }
    }
    $results.Add([ordered]@{ Name = $Name; Result = $result; Detail = $detail; Calls = @($case.Calls.ToArray()); Seconds = ([DateTime]::UtcNow - $started).TotalSeconds })
    Write-Host "$result $Name : $detail"
}

Run-Case 'valid-filtered-formatted-feed-stage-and-verify' {
    param($c)
    Invoke-Verifier $c Stage
    if ([IO.File]::ReadAllText((Join-Path $c.Stage 'releases.stable.json')) -cne $utf8.GetString($c.Remote['releases.stable.json'])) { throw 'Stage did not preserve uploaded feed bytes.' }
    if (Test-Path -LiteralPath (Join-Path $c.Stage 'assets.stable.json')) { throw 'Internal build manifest was staged.' }
    if (Test-Path -LiteralPath (Join-Path $c.Stage 'JUhalt.PaperRouteTracker-0.3.0-stable-full.nupkg')) { throw 'Prior package seed was staged.' }
    if ((Get-Content -LiteralPath (Join-Path $c.Root 'github-output.txt')) -cne 'release_id=404') { throw 'Stage did not return the verified draft ID.' }
    Publish-LocalStageToMock $c
    Invoke-Verifier $c Verify
}
Run-Case 'valid-no-server-digest-still-compares-bytes' {
    param($c)
    $c.OmitDigest = $true
    Invoke-Verifier $c Stage
    Publish-LocalStageToMock $c
    Invoke-Verifier $c Verify
}
Run-Case 'wrong-uploaded-package-bytes' {
    param($c)
    $name = 'JUhalt.PaperRouteTracker-0.4.0-stable-full.nupkg'
    $c.Remote[$name][0] = [byte]([int]$c.Remote[$name][0] -bxor 1)
    Invoke-Verifier $c Stage
} '*differs from the local build*'
Run-Case 'wrong-local-and-remote-package-hash' {
    param($c)
    Set-FeedField $c SHA256 ('0' * 64) -AlsoLocal
    Invoke-Verifier $c Stage
} '*Invalid local package metadata*'
Run-Case 'wrong-remote-feed-version' {
    param($c)
    Set-FeedField $c Version '0.9.0'
    Invoke-Verifier $c Stage
} "*Uploaded feed field 'Version' differs*"
Run-Case 'wrong-local-and-remote-package-size' {
    param($c)
    Set-FeedField $c Size 123456 -AlsoLocal
    Invoke-Verifier $c Stage
} '*Invalid local package metadata*'
Run-Case 'wrong-full-package-classified-delta' {
    param($c)
    Set-FeedField $c Type 'Delta' -AlsoLocal
    Invoke-Verifier $c Stage
} '*Invalid local package metadata*'
Run-Case 'wrong-build-manifest-package-type' {
    param($c)
    $manifestPath = Join-Path $c.Local 'assets.stable.json'
    $manifest = @(Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json)
    ($manifest | Where-Object RelativeFileName -CEQ 'JUhalt.PaperRouteTracker-0.4.0-stable-full.nupkg').Type = 'Delta'
    Write-Bytes $manifestPath (Text-Bytes (ConvertTo-Json -InputObject $manifest -Depth 6))
    Invoke-Verifier $c Stage
} '*Invalid local package metadata*'
Run-Case 'unexpected-prior-seed-uploaded' {
    param($c)
    $name = 'JUhalt.PaperRouteTracker-0.3.0-stable-full.nupkg'
    $c.Remote[$name] = [IO.File]::ReadAllBytes((Join-Path $c.Local $name))
    Invoke-Verifier $c Stage
} '*Release asset set differs*'
Run-Case 'missing-final-document' {
    param($c)
    Invoke-Verifier $c Stage
    Publish-LocalStageToMock $c
    $c.Remote.Remove('NOTICE.md')
    Invoke-Verifier $c Verify
} '*Release asset set differs*'
Run-Case 'missing-source-document' {
    param($c)
    # Move this synthetic file within its own fresh case; no real documentation touched.
    [IO.File]::Move((Join-Path $c.Root 'NOTICE.md'), (Join-Path $c.Root 'NOTICE-fixture-hidden.md'))
    Invoke-Verifier $c Stage
} '*NOTICE.md*does not exist*'
Run-Case 'final-uploaded-bytes-changed' {
    param($c)
    Invoke-Verifier $c Stage
    Publish-LocalStageToMock $c
    $c.Remote['JUhalt.PaperRouteTracker-stable-Setup.exe'][0] = 88
    Invoke-Verifier $c Verify
} '*Final uploaded asset*differs from the verified local asset*'
Run-Case 'final-checksum-inventory-incomplete' {
    param($c)
    Invoke-Verifier $c Stage
    $checksum = Join-Path $c.Stage 'SHA256SUMS.txt'
    $lines = @(Get-Content -LiteralPath $checksum | Select-Object -Skip 1)
    Write-Bytes $checksum (Text-Bytes ($lines -join "`n"))
    Publish-LocalStageToMock $c
    Invoke-Verifier $c Verify
} '*SHA256SUMS.txt does not cover exactly*'
Run-Case 'draft-published-before-verify' {
    param($c)
    Invoke-Verifier $c Stage
    Publish-LocalStageToMock $c
    $c.Release.isDraft = $false
    Invoke-Verifier $c Verify
} '*Release identity/channel changed*'
Run-Case 'asset-metadata-changed-during-stage' {
    param($c)
    $c.RaceOnApiCall = 2
    Invoke-Verifier $c Stage
} '*draft changed during verification*'

$report = [ordered]@{
    SourceScript = $sourceScript; SourceSha256 = (Get-FileHash -LiteralPath $sourceScript -Algorithm SHA256).Hash
    CompletedAtUtc = [DateTime]::UtcNow.ToString('o'); Total = $results.Count
    Passed = @($results | Where-Object { $_.Result -ceq 'Passed' }).Count
    Failed = @($results | Where-Object { $_.Result -ceq 'Failed' }).Count
    RemoteCallsMade = $false; BinariesExecuted = $false; Cases = @($results.ToArray())
}
[IO.File]::WriteAllText((Join-Path $runRoot 'report.json'), (ConvertTo-Json -InputObject $report -Depth 8), $utf8)
Write-Host "Synthetic report: $runRoot/report.json"
if ($report.Failed -gt 0) { exit 1 }
