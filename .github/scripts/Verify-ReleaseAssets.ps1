#requires -Version 7.0
<#
Read-only GitHub verification for release.yml. Stage verifies Velopack's draft
upload and prepares the exact public asset/checksum set locally; Verify checks
the complete draft again after the workflow uploads documentation/checksums.
Velopack 1.2 uploads BuildAssets, then generates a feed from its Full/Delta
entries. Its local feed can also contain prior download seeds and differs.
Sources: https://github.com/velopack/velopack/blob/1.2.0/src/vpk/Velopack.Deployment/GitHubRepository.cs
         https://github.com/velopack/velopack/blob/1.2.0/src/vpk/Velopack.Core/BuildAssets.cs
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [ValidateSet('Stage', 'Verify')] [string] $Mode,
    [Parameter(Mandatory)] [ValidatePattern('^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$')] [string] $Repository,
    [Parameter(Mandatory)] [ValidatePattern('^\d+\.\d+\.\d+(?:-[0-9A-Za-z]+(?:[.-][0-9A-Za-z]+)*)?$')] [string] $Version,
    [Parameter(Mandatory)] [ValidateSet('stable', 'preview')] [string] $Channel,
    [string] $ReleaseDirectory = 'Releases',
    [string] $StageDirectory = 'ReleaseAssets',
    [string] $EvidenceDirectory = 'ReleaseVerification',
    [string] $PublishDirectory = 'publish/win-x64',
    [long] $ExpectedReleaseId = 0
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$releaseRoot = [IO.Path]::GetFullPath($ReleaseDirectory, $repoRoot)
$stageRoot = [IO.Path]::GetFullPath($StageDirectory, $repoRoot)
$evidenceRoot = [IO.Path]::GetFullPath($EvidenceDirectory, $repoRoot)
$publishRoot = [IO.Path]::GetFullPath($PublishDirectory, $repoRoot)
$downloadRoot = Join-Path $evidenceRoot $Mode.ToLowerInvariant()
$notesPath = Join-Path $repoRoot "docs/releases/$Version.md"
$tag = "v$Version"
$isPrerelease = $Version.Contains('-')
if (($Channel -eq 'preview') -ne $isPrerelease) { throw 'Version and release channel disagree.' }
if ($Mode -eq 'Verify' -and $ExpectedReleaseId -le 0) { throw 'Verify requires the staged release ID.' }
$utf8 = [Text.UTF8Encoding]::new($false)

function Get-Hash([string] $Path) {
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Assert-FileName([string] $Name) {
    if ($Name -cnotmatch '^[A-Za-z0-9][A-Za-z0-9._-]*$') { throw "Unsafe or unsupported asset name '$Name'." }
}

function Assert-SameNames([string[]] $Expected, [string[]] $Actual) {
    if ($Expected.Count -ne @($Expected | Sort-Object -Unique).Count -or
        $Actual.Count -ne @($Actual | Sort-Object -Unique).Count) {
        throw 'Release asset names must be unique, including on Windows.'
    }
    $difference = @(Compare-Object $Expected $Actual -CaseSensitive)
    if ($difference.Count -gt 0) { throw "Release asset set differs: $($difference | ConvertTo-Json -Compress)" }
}

function Get-DraftRelease {
    $json = & gh release view $tag --repo $Repository --json databaseId,isDraft,isPrerelease,tagName,name,body
    if ($LASTEXITCODE -ne 0) { throw 'Cannot inspect the draft release.' }
    $release = ($json | Out-String) | ConvertFrom-Json
    if (-not $release.isDraft -or $release.tagName -cne $tag -or
        $release.isPrerelease -ne $isPrerelease -or $release.name -cne "PaperRoute Tracker $tag") {
        throw 'Release identity/channel changed, or the release is already published.'
    }
    if ($ExpectedReleaseId -gt 0 -and $release.databaseId -ne $ExpectedReleaseId) {
        throw 'The staged draft was replaced by a different release.'
    }
    return $release
}

function Get-RemoteAssets([long] $ReleaseId) {
    $json = & gh api "repos/$Repository/releases/$ReleaseId/assets?per_page=100" --paginate --slurp
    if ($LASTEXITCODE -ne 0) { throw 'Cannot enumerate all uploaded release assets.' }
    $pages = ($json | Out-String) | ConvertFrom-Json -NoEnumerate
    return @($pages | ForEach-Object { $_ } | ForEach-Object { $_ })
}

function Get-AssetSnapshot([object[]] $Assets) {
    return ConvertTo-Json -InputObject @($Assets | Sort-Object name | Select-Object id,name,size,state,updated_at,digest) -Compress
}

# The .NET runtime a self-contained publish bundles, read from its
# runtimeconfig.json, so the release record says which runtime shipped (#144).
function Get-BundledRuntimeVersion([string] $PublishRoot) {
    $configPath = Join-Path $PublishRoot 'PaperRouteTracker.runtimeconfig.json'
    if (-not (Test-Path -LiteralPath $configPath -PathType Leaf)) {
        throw "The published PaperRouteTracker.runtimeconfig.json was not found at '$configPath'."
    }
    $options = (Get-Content -LiteralPath $configPath -Raw | ConvertFrom-Json).runtimeOptions
    $included = if ($null -ne $options.PSObject.Properties['includedFrameworks']) { @($options.includedFrameworks) } else { @() }
    $runtime = @($included | Where-Object { $_.name -ceq 'Microsoft.NETCore.App' })
    if ($runtime.Count -ne 1 -or [string]::IsNullOrWhiteSpace($runtime[0].version)) {
        throw "'$configPath' does not name one bundled Microsoft.NETCore.App; the publish must be self-contained."
    }
    return [string] $runtime[0].version
}

# THIRD-PARTY-NOTICES.txt names the bundled runtime twice: in its '.NET
# runtime <version>, bundled as the packs' entry and in the heading of the
# runtime's reproduced notices. v0.9.2 bundled 10.0.14 under notices for
# 10.0.12; a runtime that moves without the notices is refused (#269).
function Assert-NoticesNameRuntime([string] $NoticesPath, [string] $RuntimeVersion) {
    if (-not (Test-Path -LiteralPath $NoticesPath -PathType Leaf)) {
        throw "THIRD-PARTY-NOTICES.txt at '$NoticesPath' does not exist."
    }
    $notices = [IO.File]::ReadAllText($NoticesPath)
    $runtimeLines = [ordered]@{
        "its '.NET runtime <version>, bundled as the packs' entry" =
            '(?m)^\.NET runtime (?<version>\S+), bundled as the packs'
        "the reproduced runtime notices' 'Microsoft.NETCore.App.Runtime.win-x64 <version>.' heading" =
            '(?m)^Reproduced from THIRD-PARTY-NOTICES\.TXT in the package\s+Microsoft\.NETCore\.App\.Runtime\.win-x64 (?<version>\S+?)\.\r?$'
    }
    foreach ($line in $runtimeLines.GetEnumerator()) {
        $named = @([regex]::Matches($notices, $line.Value) | ForEach-Object { $_.Groups['version'].Value })
        if ($named.Count -ne 1) {
            throw "THIRD-PARTY-NOTICES.txt must name the bundled .NET runtime exactly once in $($line.Key); found $($named.Count)."
        }
        if ($named[0] -cne $RuntimeVersion) {
            throw "THIRD-PARTY-NOTICES.txt names .NET runtime $($named[0]) in $($line.Key), but the publish bundles $RuntimeVersion. Regenerate the notices for the bundled runtime, or pin the SDK that bundles the runtime they name (#269)."
        }
    }
}

# Stage records the runtime and, before the draft can be published, checks
# that the notices the release uploads name it; Verify checks only the draft
# and the staged assets, so it needs no publish folder.
$runtimeVersion = if ($Mode -eq 'Stage') { Get-BundledRuntimeVersion $publishRoot } else { $null }
if ($Mode -eq 'Stage') { Assert-NoticesNameRuntime (Join-Path $repoRoot 'THIRD-PARTY-NOTICES.txt') $runtimeVersion }
$release = Get-DraftRelease
$ExpectedReleaseId = [long] $release.databaseId
$remoteAssets = @(Get-RemoteAssets $ExpectedReleaseId)
foreach ($asset in $remoteAssets) {
    Assert-FileName $asset.name
    if ($asset.state -cne 'uploaded' -or [long] $asset.size -le 0) {
        throw "Asset '$($asset.name)' has not finished uploading or is empty."
    }
}
$remoteSnapshot = Get-AssetSnapshot $remoteAssets
$feedName = "releases.$Channel.json"

if ($Mode -eq 'Stage') {
    if (Test-Path -LiteralPath $stageRoot) { throw 'The release asset staging directory must be fresh.' }
    $buildAssets = @(Get-Content -LiteralPath (Join-Path $releaseRoot "assets.$Channel.json") -Raw | ConvertFrom-Json)
    $buildNames = @($buildAssets | ForEach-Object { [string] $_.RelativeFileName })
    foreach ($name in $buildNames) { Assert-FileName $name }
    foreach ($pattern in @('*-full.nupkg', '*Setup.exe')) {
        if (@($buildNames | Where-Object { $_ -like $pattern }).Count -ne 1) {
            throw "Expected exactly one '$pattern' asset from this Velopack build."
        }
    }
    # The portable bundle is not built from v0.9.2 (#149); one here means the
    # pack step changed, and it is refused rather than published.
    if (@($buildNames | Where-Object { $_ -like '*Portable.zip' }).Count -ne 0) {
        throw "Expected no '*Portable.zip' asset from this Velopack build; the portable bundle is no longer released."
    }
    Assert-SameNames ($buildNames + $feedName) @($remoteAssets.name)
} else {
    $expectedFiles = @(Get-ChildItem -LiteralPath $stageRoot -File)
    Assert-SameNames @($expectedFiles.Name) @($remoteAssets.name)
    $notes = [IO.File]::ReadAllText($notesPath).Replace("`r`n", "`n").TrimEnd()
    if ($release.body.Replace("`r`n", "`n").TrimEnd() -cne $notes) {
        throw 'The draft release body differs from the exact versioned release notes.'
    }
}

if (Test-Path -LiteralPath $downloadRoot) { throw 'Each verification download directory must be fresh.' }
[void] [IO.Directory]::CreateDirectory($downloadRoot)
& gh release download $tag --repo $Repository --dir $downloadRoot
if ($LASTEXITCODE -ne 0) { throw 'Could not download every draft release asset.' }
$downloaded = @(Get-ChildItem -LiteralPath $downloadRoot -File)
Assert-SameNames @($remoteAssets.name) @($downloaded.Name)
foreach ($asset in $remoteAssets) {
    $file = Get-Item -LiteralPath (Join-Path $downloadRoot $asset.name)
    if ($file.Length -ne [long] $asset.size) { throw "Downloaded size mismatch for '$($asset.name)'." }
    $hash = Get-Hash $file.FullName
    # A missing server digest is not trusted as proof; downloaded bytes are
    # always compared with local files below. Check the digest when supplied.
    if ($null -ne $asset.PSObject.Properties['digest'] -and $asset.digest -and
        $asset.digest -cne "sha256:$hash") { throw "GitHub digest mismatch for '$($asset.name)'." }
}

if ($Mode -eq 'Stage') {
    foreach ($name in $buildNames) {
        if ((Get-Hash (Join-Path $downloadRoot $name)) -cne (Get-Hash (Join-Path $releaseRoot $name))) {
            throw "The uploaded '$name' differs from the local build."
        }
    }
    # Compare all feed fields with the locally generated package entries,
    # independent of order/JSON whitespace. Prior download seeds are excluded.
    $localFeed = Get-Content -LiteralPath (Join-Path $releaseRoot $feedName) -Raw | ConvertFrom-Json
    $remoteFeed = Get-Content -LiteralPath (Join-Path $downloadRoot $feedName) -Raw | ConvertFrom-Json
    $packageNames = @($buildNames | Where-Object { $_ -like '*.nupkg' })
    Assert-SameNames $packageNames @($remoteFeed.Assets.FileName)
    foreach ($name in $packageNames) {
        $local = @($localFeed.Assets | Where-Object { $_.FileName -ceq $name })
        $remote = @($remoteFeed.Assets | Where-Object { $_.FileName -ceq $name })
        if ($local.Count -ne 1 -or $remote.Count -ne 1) { throw "Ambiguous package feed entry '$name'." }
        $local = $local[0]
        $remote = $remote[0]
        $packagePath = Join-Path $releaseRoot $name
        $expectedType = if ($name -cmatch '-full\.nupkg$') { 'Full' }
            elseif ($name -cmatch '-delta\.nupkg$') { 'Delta' }
            else { throw "Invalid local package metadata for '$name': unrecognized package suffix." }
        $buildEntry = @($buildAssets | Where-Object { $_.RelativeFileName -ceq $name })
        if ($local.PackageId -cne 'JUhalt.PaperRouteTracker' -or $local.Version -cne $Version -or
            $local.SHA256 -ine (Get-Hash $packagePath) -or
            [long] $local.Size -ne (Get-Item -LiteralPath $packagePath).Length -or
            $buildEntry.Count -ne 1 -or $buildEntry[0].Type -cne $expectedType -or
            $local.Type -cne $expectedType) { throw "Invalid local package metadata for '$name'." }
        Assert-SameNames @($local.PSObject.Properties.Name) @($remote.PSObject.Properties.Name)
        foreach ($property in $local.PSObject.Properties) {
            if ((ConvertTo-Json -InputObject $property.Value -Compress -Depth 10) -cne
                (ConvertTo-Json -InputObject $remote.($property.Name) -Compress -Depth 10)) {
                throw "Uploaded feed field '$($property.Name)' differs for '$name'."
            }
        }
    }
    [void] [IO.Directory]::CreateDirectory($stageRoot)
    foreach ($name in $buildNames) {
        Copy-Item -LiteralPath (Join-Path $releaseRoot $name) -Destination (Join-Path $stageRoot $name)
    }
    # This feed is now semantically verified against local package bytes. Keep
    # its actual uploaded bytes so the public checksum describes the real feed.
    Copy-Item -LiteralPath (Join-Path $downloadRoot $feedName) -Destination (Join-Path $stageRoot $feedName)
    $documents = [ordered]@{
        'RELEASE_NOTES.md' = $notesPath
        'USER_GUIDE.md' = (Join-Path $repoRoot 'docs/USER_GUIDE.md')
        'UPGRADE_NOTES.md' = (Join-Path $repoRoot 'UPGRADE_NOTES.md')
        'LICENSE.txt' = (Join-Path $repoRoot 'LICENSE.txt')
        'NOTICE.md' = (Join-Path $repoRoot 'NOTICE.md')
        'THIRD-PARTY-NOTICES.txt' = (Join-Path $repoRoot 'THIRD-PARTY-NOTICES.txt')
    }
    foreach ($entry in $documents.GetEnumerator()) {
        $source = Get-Item -LiteralPath $entry.Value
        if ($source.Length -le 0 -or (Test-Path -LiteralPath (Join-Path $stageRoot $entry.Key))) {
            throw "Required document '$($entry.Key)' is empty or collides with a build asset."
        }
        Copy-Item -LiteralPath $source.FullName -Destination (Join-Path $stageRoot $entry.Key)
    }
    $lines = @(Get-ChildItem -LiteralPath $stageRoot -File | Sort-Object Name | ForEach-Object {
        '{0}  {1}' -f (Get-Hash $_.FullName), $_.Name
    })
    [IO.File]::WriteAllLines((Join-Path $stageRoot 'SHA256SUMS.txt'), [string[]] $lines, $utf8)
    if ($env:GITHUB_OUTPUT) { "release_id=$ExpectedReleaseId" >> $env:GITHUB_OUTPUT }
} else {
    foreach ($file in $expectedFiles) {
        $hash = Get-Hash $file.FullName
        if ((Get-Hash (Join-Path $downloadRoot $file.Name)) -cne $hash) {
            throw "Final uploaded asset '$($file.Name)' differs from the verified local asset."
        }
        Write-Host "$hash  $($file.Name)"
    }
    $checksumLines = @(Get-Content -LiteralPath (Join-Path $downloadRoot 'SHA256SUMS.txt'))
    $expectedLines = @($expectedFiles | Where-Object Name -cne 'SHA256SUMS.txt' | Sort-Object Name | ForEach-Object {
        '{0}  {1}' -f (Get-Hash $_.FullName), $_.Name
    })
    if (($checksumLines -join "`n") -cne ($expectedLines -join "`n")) {
        throw 'SHA256SUMS.txt does not cover exactly the verified release assets.'
    }
}

$after = Get-DraftRelease
$afterAssets = @(Get-RemoteAssets $ExpectedReleaseId)
if ($after.body -cne $release.body -or (Get-AssetSnapshot $afterAssets) -cne $remoteSnapshot) {
    throw 'The draft changed during verification; publication must stop.'
}
[IO.File]::WriteAllText((Join-Path $evidenceRoot "$Mode-verification.json"),
    (ConvertTo-Json -Depth 6 -InputObject ([ordered]@{
        Tag = $tag; ReleaseId = $ExpectedReleaseId; Draft = $true; RuntimeVersion = $runtimeVersion
        VerifiedAtUtc = [DateTime]::UtcNow.ToString('o'); Assets = ($remoteSnapshot | ConvertFrom-Json)
    })), $utf8)
Write-Host "$Mode verification passed for draft $tag ($ExpectedReleaseId)."
