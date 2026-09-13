#requires -Version 7.2
<#
.SYNOPSIS
Build, test, and verify a local v0.4 candidate without installing or publishing it.
.EXAMPLE
pwsh -NoProfile -File .\ManualCertification\Build-V04ReleaseCandidate.ps1 -OutputDirectory .\artifacts\v0.4.0-candidate-01
.NOTES
Run from a quiet checkout: source files must not change during this script.
Each attempt requires a fresh output directory. Failed attempts retain their logs.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string] $OutputDirectory,

    [ValidatePattern('^0\.4\.0(?:-[0-9A-Za-z]+(?:[.-][0-9A-Za-z]+)*)?$')]
    [string] $Version = '0.4.0',

    [switch] $PreflightOnly
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false

if (-not $IsWindows) { throw 'This candidate targets Windows x64 and requires Windows.' }

$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$artifactsRoot = [IO.Path]::GetFullPath((Join-Path $repoRoot 'artifacts'))
$candidateRoot = [IO.Path]::GetFullPath($OutputDirectory, $repoRoot).TrimEnd('\', '/')
$artifactPrefix = $artifactsRoot + [IO.Path]::DirectorySeparatorChar
if (-not $candidateRoot.StartsWith($artifactPrefix, [StringComparison]::OrdinalIgnoreCase)) {
    throw "OutputDirectory must be a new subdirectory of '$artifactsRoot'."
}

# Reject junction/symlink ancestors so a nominal artifacts path cannot redirect writes.
$ancestor = $candidateRoot
while (-not [string]::IsNullOrEmpty($ancestor)) {
    if (Test-Path -LiteralPath $ancestor) {
        $item = Get-Item -LiteralPath $ancestor -Force
        if (-not $item.PSIsContainer -or ($item.Attributes -band [IO.FileAttributes]::ReparsePoint)) {
            throw "Output path ancestor '$ancestor' is not an ordinary directory."
        }
    }
    $ancestor = [IO.Path]::GetDirectoryName($ancestor)
}
if ((Test-Path -LiteralPath $candidateRoot) -and
    @(Get-ChildItem -LiteralPath $candidateRoot -Force).Count -ne 0) {
    throw "OutputDirectory '$candidateRoot' is not empty. Choose a fresh candidate directory."
}

$dotnet = (Get-Command dotnet -CommandType Application -ErrorAction Stop | Select-Object -First 1).Source
$dnx = (Get-Command dnx -CommandType Application -ErrorAction Stop | Select-Object -First 1).Source
$git = (Get-Command git -CommandType Application -ErrorAction Stop | Select-Object -First 1).Source
$relativeOutput = [IO.Path]::GetRelativePath($repoRoot, $candidateRoot).Replace('\', '/')
& $git -C $repoRoot check-ignore --quiet -- "$relativeOutput/"
if ($LASTEXITCODE -ne 0) { throw 'The candidate directory must be ignored by Git.' }
& $git -C $repoRoot diff --check
if ($LASTEXITCODE -ne 0) { throw 'git diff --check failed.' }

function Read-SafeXml([string] $Path) {
    $document = [xml]::new()
    $document.XmlResolver = $null
    $document.Load($Path)
    return ,$document
}

function Get-SourceSnapshot {
    $paths = @(& $git -C $repoRoot -c core.quotepath=false ls-files --cached --others --exclude-standard)
    if ($LASTEXITCODE -ne 0) { throw 'Could not enumerate candidate source files.' }
    return @($paths | Sort-Object -Unique | ForEach-Object {
        $sourcePath = Join-Path $repoRoot $_
        $hash = if (Test-Path -LiteralPath $sourcePath -PathType Leaf) {
            (Get-FileHash -LiteralPath $sourcePath -Algorithm SHA256).Hash.ToLowerInvariant()
        } else { $null }
        [ordered]@{ Path = $_; Sha256 = $hash }
    })
}

$projectPath = Join-Path $repoRoot 'ManuscriptPipeline/ManuscriptPipeline.vbproj'
$project = Read-SafeXml $projectPath
$numericVersion = '0.4.0.0'
foreach ($pair in @(@('Version', $Version), @('AssemblyVersion', $numericVersion), @('FileVersion', $numericVersion))) {
    $nodes = @($project.SelectNodes("/Project/PropertyGroup/$($pair[0])"))
    if ($nodes.Count -ne 1 -or $nodes[0].InnerText -cne $pair[1]) {
        throw "Project $($pair[0]) must explicitly equal '$($pair[1])' before packaging."
    }
}
$velopack = @($project.SelectNodes('/Project/ItemGroup/PackageReference[@Include="Velopack"]'))
if ($velopack.Count -ne 1 -or $velopack[0].GetAttribute('Version') -ne '1.2.0') {
    throw 'The runtime and this candidate packager must both use Velopack 1.2.0.'
}
$releaseNotes = Join-Path $repoRoot "docs/releases/$Version.md"
if (-not (Test-Path -LiteralPath $releaseNotes -PathType Leaf)) {
    throw "Exact release notes are required at '$releaseNotes'."
}
$sdk = (& $dotnet --version | Out-String).Trim()
if ($LASTEXITCODE -ne 0 -or $sdk -notmatch '^10\.') { throw 'An active .NET 10 SDK is required.' }
$commit = (& $git -C $repoRoot rev-parse HEAD | Out-String).Trim()
if ($LASTEXITCODE -ne 0) { throw 'Could not identify the checkout commit.' }
$sourceStatus = @(& $git -C $repoRoot status --porcelain=v1 --untracked-files=all)
if ($LASTEXITCODE -ne 0) { throw 'Could not record checkout status.' }
$channel = if ($Version.Contains('-')) { 'preview' } else { 'stable' }
$packId = 'JUhalt.PaperRouteTracker'

if ($PreflightOnly) {
    [pscustomobject]@{
        Version = $Version; Channel = $channel; OutputDirectory = $candidateRoot
        Commit = $commit; DirtyCheckout = ($sourceStatus.Count -gt 0); DotnetSdk = $sdk
        Velopack = '1.2.0'; ReleaseNotes = $releaseNotes
    }
    return
}

$sourceSnapshotJson = ConvertTo-Json -InputObject @(Get-SourceSnapshot) -Depth 3
$buildRoot = Join-Path $candidateRoot 'build'
$publishRoot = Join-Path $candidateRoot 'publish'
$releaseRoot = Join-Path $candidateRoot 'releases'
$testRoot = Join-Path $candidateRoot 'tests'
$logRoot = Join-Path $candidateRoot 'logs'
foreach ($directory in @($candidateRoot, $buildRoot, $publishRoot, $releaseRoot, $testRoot, $logRoot)) {
    [void][IO.Directory]::CreateDirectory($directory)
}
$utf8 = [Text.UTF8Encoding]::new($false)
[IO.File]::WriteAllLines((Join-Path $candidateRoot 'source-status.txt'), [string[]] $sourceStatus, $utf8)
[IO.File]::WriteAllText((Join-Path $candidateRoot 'source-files.json'), $sourceSnapshotJson, $utf8)
$startedAt = [DateTime]::UtcNow
$invocations = [Collections.Generic.List[object]]::new()

function Invoke-CandidateCommand([string] $Name, [string] $Executable, [string[]] $Arguments) {
    Write-Host "Candidate step: $Name"
    $logPath = Join-Path $logRoot "$Name.log"
    $invocations.Add([ordered]@{ Step = $Name; Executable = $Executable; Arguments = $Arguments })
    & $Executable @Arguments 2>&1 | Tee-Object -FilePath $logPath | Out-Host
    $exitCode = $LASTEXITCODE
    if ($exitCode -ne 0) { throw "$Name failed with exit code $exitCode. See '$logPath'." }
}

function Get-Sha256([string] $Path) {
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Assert-ArchivePayload([string] $Path, [string] $ExpectedDllHash, [switch] $CheckNuspec) {
    $archive = [IO.Compression.ZipFile]::OpenRead($Path)
    try {
        $dllEntries = @($archive.Entries | Where-Object { $_.Name -ceq 'PaperRouteTracker.dll' })
        $exeEntries = @($archive.Entries | Where-Object { $_.Name -ceq 'PaperRouteTracker.exe' })
        if ($dllEntries.Count -ne 1 -or $exeEntries.Count -lt 1) {
            throw "'$Path' does not contain an unambiguous PaperRoute payload."
        }
        $stream = $dllEntries[0].Open()
        try { $dllHash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($stream)).ToLowerInvariant() }
        finally { $stream.Dispose() }
        if ($dllHash -cne $ExpectedDllHash) { throw "The application DLL in '$Path' differs from the published DLL." }
        foreach ($legalName in @('LICENSE.txt', 'NOTICE.md')) {
            $legalEntries = @($archive.Entries | Where-Object { $_.Name -ceq $legalName })
            if ($legalEntries.Count -ne 1) { throw "'$Path' must contain exactly one '$legalName'." }
            $legalStream = $legalEntries[0].Open()
            try { $legalHash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($legalStream)).ToLowerInvariant() }
            finally { $legalStream.Dispose() }
            if ($legalHash -cne (Get-Sha256 (Join-Path $repoRoot $legalName))) {
                throw "'$legalName' in '$Path' differs from the repository notice."
            }
        }
        if ($CheckNuspec) {
            $specs = @($archive.Entries | Where-Object { $_.Name.EndsWith('.nuspec', [StringComparison]::OrdinalIgnoreCase) })
            if ($specs.Count -ne 1) { throw "Expected one nuspec in '$Path'." }
            $specStream = $specs[0].Open()
            try {
                $spec = [xml]::new()
                $spec.XmlResolver = $null
                $spec.Load($specStream)
            }
            finally { $specStream.Dispose() }
            $idNode = $spec.SelectSingleNode('/*[local-name()="package"]/*[local-name()="metadata"]/*[local-name()="id"]')
            $versionNode = $spec.SelectSingleNode('/*[local-name()="package"]/*[local-name()="metadata"]/*[local-name()="version"]')
            if ($null -eq $idNode -or $null -eq $versionNode -or
                $idNode.InnerText -cne $packId -or $versionNode.InnerText -cne $Version) {
                throw "Package identity/version mismatch in '$Path'."
            }
        }
        return [ordered]@{ FileName = [IO.Path]::GetFileName($Path); PayloadDllSha256 = $dllHash }
    }
    finally { $archive.Dispose() }
}

Push-Location $repoRoot
try {
    $properties = @("-p:Version=$Version", "-p:AssemblyVersion=$numericVersion", "-p:FileVersion=$numericVersion", '-p:ContinuousIntegrationBuild=true')
    Invoke-CandidateCommand '01-build' $dotnet (@(
        'build', 'ManuscriptPipeline.slnx', '--configuration', 'Release', '--artifacts-path', $buildRoot
    ) + $properties)
    Invoke-CandidateCommand '02-test' $dotnet (@(
        'test', 'PaperRoute.Tests/PaperRoute.Tests.vbproj', '--configuration', 'Release', '--no-build', '--no-restore',
        '--artifacts-path', $buildRoot, '--logger', 'trx;LogFileName=PaperRoute.Tests.trx', '--results-directory', $testRoot
    ) + $properties)
    $trxPath = Join-Path $testRoot 'PaperRoute.Tests.trx'
    $trx = Read-SafeXml $trxPath
    $counters = $trx.SelectSingleNode('//*[local-name()="ResultSummary"]/*[local-name()="Counters"]')
    if ($null -eq $counters -or [int] $counters.GetAttribute('total') -lt 1 -or
        [int] $counters.GetAttribute('passed') -ne [int] $counters.GetAttribute('total')) {
        throw 'The TRX report must contain tests, all passing, with none skipped.'
    }
    Invoke-CandidateCommand '03-publish' $dotnet (@(
        'publish', 'ManuscriptPipeline/ManuscriptPipeline.vbproj', '--configuration', 'Release', '--runtime', 'win-x64',
        '--self-contained', 'true', '--artifacts-path', $buildRoot, '--output', $publishRoot,
        '-p:PublishSingleFile=false', '-p:DebugType=None'
    ) + $properties)

    foreach ($required in @('PaperRouteTracker.exe', 'PaperRouteTracker.dll', 'PaperRouteTracker.deps.json',
        'PaperRouteTracker.runtimeconfig.json', 'coreclr.dll', 'hostfxr.dll', 'System.Windows.Forms.dll',
        'docs/USER_GUIDE.md', 'LICENSE.txt', 'NOTICE.md')) {
        if (-not (Test-Path -LiteralPath (Join-Path $publishRoot $required) -PathType Leaf)) {
            throw "The self-contained publish payload is missing '$required'."
        }
    }
    $dllPath = Join-Path $publishRoot 'PaperRouteTracker.dll'
    $versionInfo = [Diagnostics.FileVersionInfo]::GetVersionInfo($dllPath)
    $assemblyVersion = [Reflection.AssemblyName]::GetAssemblyName($dllPath).Version.ToString()
    if (($versionInfo.ProductVersion -split '\+')[0] -cne $Version -or
        $versionInfo.FileVersion -cne $numericVersion -or $assemblyVersion -cne $numericVersion) {
        throw "Published binary versions do not match '$Version' / '$numericVersion'."
    }
    $dllHash = Get-Sha256 $dllPath
    Invoke-CandidateCommand '04-package' $dnx @(
        'vpk', '--version', '1.2.0', 'pack', '--packId', $packId, '--packVersion', $Version,
        '--packDir', $publishRoot, '--mainExe', 'PaperRouteTracker.exe', '--packTitle', 'PaperRoute Tracker',
        '--packAuthors', 'Joshua Uhalt', '--icon', (Join-Path $repoRoot 'ManuscriptPipeline/Assets/PaperRoute.ico'),
        '--releaseNotes', $releaseNotes, '--channel', $channel, '--outputDir', $releaseRoot,
        '--skip-updates', 'true', '--legacyConsole', 'true'
    )

    $packages = @(Get-ChildItem -LiteralPath $releaseRoot -File -Filter '*-full.nupkg')
    $installers = @(Get-ChildItem -LiteralPath $releaseRoot -File -Filter '*Setup.exe')
    $portable = @(Get-ChildItem -LiteralPath $releaseRoot -File -Filter '*Portable.zip')
    if ($packages.Count -ne 1 -or $installers.Count -ne 1 -or $portable.Count -ne 1 -or $installers[0].Length -le 0) {
        throw 'Expected exactly one full package, installer, and portable ZIP in the fresh release directory.'
    }
    $packageProof = Assert-ArchivePayload $packages[0].FullName $dllHash -CheckNuspec
    $portableProof = Assert-ArchivePayload $portable[0].FullName $dllHash
    $feedPath = Join-Path $releaseRoot "releases.$channel.json"
    $feed = Get-Content -LiteralPath $feedPath -Raw | ConvertFrom-Json
    $fullAsset = @($feed.Assets | Where-Object { $_.FileName -ceq $packages[0].Name })
    $packageHash = Get-Sha256 $packages[0].FullName
    if ($fullAsset.Count -ne 1 -or $fullAsset[0].PackageId -cne $packId -or
        $fullAsset[0].Version -cne $Version -or $fullAsset[0].Type -ine 'Full' -or
        $fullAsset[0].SHA256 -ine $packageHash -or [long] $fullAsset[0].Size -ne $packages[0].Length) {
        throw 'The channel feed does not accurately describe the verified full package.'
    }

    $assetProof = @(Get-ChildItem -LiteralPath $releaseRoot -File | Sort-Object Name | ForEach-Object {
        [ordered]@{ FileName = $_.Name; SizeBytes = $_.Length; Sha256 = (Get-Sha256 $_.FullName) }
    })
    $checksumPath = Join-Path $releaseRoot 'SHA256SUMS.txt'
    $checksumLines = @($assetProof | ForEach-Object { '{0}  {1}' -f $_.Sha256, $_.FileName })
    [IO.File]::WriteAllLines($checksumPath, [string[]] $checksumLines, $utf8)
    foreach ($asset in $assetProof) {
        if ((Get-Sha256 (Join-Path $releaseRoot $asset.FileName)) -cne $asset.Sha256) {
            throw "Release asset '$($asset.FileName)' changed during verification."
        }
    }
    $signature = Get-AuthenticodeSignature -LiteralPath $installers[0].FullName
    $finishedSnapshotJson = ConvertTo-Json -InputObject @(Get-SourceSnapshot) -Depth 3
    $finishedCommit = (& $git -C $repoRoot rev-parse HEAD | Out-String).Trim()
    if ($LASTEXITCODE -ne 0 -or $finishedCommit -cne $commit -or $finishedSnapshotJson -cne $sourceSnapshotJson) {
        throw 'Source files or HEAD changed during the candidate run. Use a fresh output directory after edits finish.'
    }
    $evidence = [ordered]@{
        Status = 'VerifiedLocalCandidate'; Version = $Version; NumericVersion = $numericVersion
        Channel = $channel; PackageId = $packId; VelopackVersion = '1.2.0'; DotnetSdk = $sdk
        Commit = $commit; DirtyCheckout = ($sourceStatus.Count -gt 0); SourceStatusFile = 'source-status.txt'
        SourceSnapshotFile = 'source-files.json'; SourceSnapshotSha256 = (Get-Sha256 (Join-Path $candidateRoot 'source-files.json'))
        StartedAtUtc = $startedAt.ToString('o'); CompletedAtUtc = [DateTime]::UtcNow.ToString('o')
        TestsPassed = [int] $counters.GetAttribute('passed'); TestReport = 'tests/PaperRoute.Tests.trx'
        ProductVersion = $versionInfo.ProductVersion; AssemblyVersion = $assemblyVersion
        FileVersion = $versionInfo.FileVersion; PublishedDllSha256 = $dllHash
        ReleaseNotesSha256 = (Get-Sha256 $releaseNotes)
        Package = $packageProof; Portable = $portableProof; Assets = $assetProof
        ChecksumFileSha256 = (Get-Sha256 $checksumPath); InstallerSignatureStatus = $signature.Status.ToString()
        Invocations = @($invocations.ToArray())
        Limitations = @('No installer or app launch certification', 'No live GitHub update-feed certification',
            'No previous-package seed or delta package certification', 'A dirty checkout is not an immutable release source')
    }
    [IO.File]::WriteAllText((Join-Path $candidateRoot 'candidate-verification.json'), ($evidence | ConvertTo-Json -Depth 8), $utf8)
    Write-Host "Verified local candidate: $candidateRoot"
    Write-Host "Tests: $($evidence.TestsPassed) passed. Installer signature: $($evidence.InstallerSignatureStatus)."
}
finally { Pop-Location }
