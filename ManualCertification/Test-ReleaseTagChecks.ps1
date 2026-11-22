#requires -Version 7.2
<#
.SYNOPSIS
Exercise release.yml's tag check and newer-release check without a real tag.
.EXAMPLE
pwsh -NoProfile -File .\ManualCertification\Test-ReleaseTagChecks.ps1
.NOTES
No network and no GitHub calls. The tag check runs against a throwaway bare
repository, reached through file:// and cloned the way actions/checkout
clones a tag: one commit deep, with no branches. Each case runs the script in
its own pwsh, as release.yml does. Everything stays under the ignored
artifacts/release-tag-tests.
#>
[CmdletBinding()]
param(
    [ValidatePattern('^[A-Za-z0-9][A-Za-z0-9._-]*$')]
    [string] $RunName = ('run-' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss'))
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$tagScript = Join-Path $repoRoot '.github/scripts/Assert-ReleaseTagBranch.ps1'
$newerScript = Join-Path $repoRoot '.github/scripts/Get-NewerRelease.ps1'
$runRoot = Join-Path $repoRoot "artifacts/release-tag-tests/$RunName"
if (Test-Path -LiteralPath $runRoot) { throw 'Use a fresh run directory.' }
[void][IO.Directory]::CreateDirectory($runRoot)
$results = [Collections.Generic.List[object]]::new()

# The runner's and the developer's git settings stay out of the throwaway
# repositories: no signing, no hooks, a fixed author.
$gitOptions = @('-c', 'user.name=Release tag test', '-c', 'user.email=release-tag-test@example.invalid',
    '-c', 'commit.gpgsign=false', '-c', 'tag.gpgsign=false', '-c', 'core.hooksPath=', '-c', 'init.defaultBranch=master')
# A simple function, so git's own switches (-q, -m) reach git as they are.
function Invoke-Git {
    $directory, $arguments = $args
    $output = @(& git @gitOptions -C $directory @arguments 2>&1 | ForEach-Object { "$_" })
    if ($LASTEXITCODE -ne 0) { throw "git $($arguments -join ' ') failed in ${directory}: $($output -join ' ')" }
    return ($output -join "`n")
}
function New-Commit([string] $Directory, [string] $Name) {
    [IO.File]::WriteAllText((Join-Path $Directory 'file.txt'), "$Name`n")
    Invoke-Git $Directory add file.txt | Out-Null
    Invoke-Git $Directory commit -q -m $Name | Out-Null
    return ([string] (Invoke-Git $Directory rev-parse HEAD)).Trim()
}
function ConvertTo-Literal([string] $Text) { "'" + $Text.Replace("'", "''") + "'" }
function Add-Result([string] $Name, [bool] $Passed, [string] $Detail) {
    $results.Add([pscustomobject] @{ Case = $Name; Passed = $Passed; Detail = $Detail })
    Write-Host ('{0}  {1}{2}' -f $(if ($Passed) { 'PASS' } else { 'FAIL' }), $Name, $(if ($Passed) { '' } else { " ($Detail)" }))
}

# The history, with the branches and tags pushed to the bare origin:
#   master:          m1 - m2 - m3
#   release/v1.0.x:         m2 - r1 - r2
#   feature/x:              m2 - f1
#   release/v1.1.x:         m2 - s1   (a line branch the script does not name)
#   tag only:                         r2 - r3   (r3 is on no branch)
#   tag only:        n1                         (a root on no branch)
$work = Join-Path $runRoot 'work'
$origin = Join-Path $runRoot 'origin.git'
[void][IO.Directory]::CreateDirectory($work)
Invoke-Git $runRoot init -q --bare $origin | Out-Null
Invoke-Git $work init -q | Out-Null
$sha = @{}
$sha.m1 = New-Commit $work 'm1'
$sha.m2 = New-Commit $work 'm2'
$sha.m3 = New-Commit $work 'm3'
Invoke-Git $work checkout -q -b release/v1.0.x $sha.m2 | Out-Null
$sha.r1 = New-Commit $work 'r1'
$sha.r2 = New-Commit $work 'r2'
Invoke-Git $work checkout -q -b feature/x $sha.m2 | Out-Null
$sha.f1 = New-Commit $work 'f1'
Invoke-Git $work checkout -q -b release/v1.1.x $sha.m2 | Out-Null
$sha.s1 = New-Commit $work 's1'
Invoke-Git $work checkout -q --detach $sha.r2 | Out-Null
$sha.r3 = New-Commit $work 'r3'
Invoke-Git $work checkout -q --orphan lonely | Out-Null
$sha.n1 = New-Commit $work 'n1'

$tags = [ordered] @{
    'v1.1.0'       = @{ At = 'm3'; Annotated = $true }
    'v1.2.0-rc.1'  = @{ At = 'm3'; Annotated = $false }
    'v1.0.1'       = @{ At = 'm2'; Annotated = $true }
    'v1.0.2'       = @{ At = 'r2'; Annotated = $true }
    'v1.0.2-rc.1'  = @{ At = 'r1'; Annotated = $false }
    'v1.0.3'       = @{ At = 'f1'; Annotated = $true }
    'v1.1.1'       = @{ At = 'r2'; Annotated = $true }
    'v1.1.2'       = @{ At = 's1'; Annotated = $true }
    'v1.0.4'       = @{ At = 'n1'; Annotated = $false }
    'v1.0.5'       = @{ At = 'r3'; Annotated = $true }
    'v1.2.0'       = @{ At = 'f1'; Annotated = $false }
    'v2.0.0'       = @{ At = 'r2'; Annotated = $false }
    'v1.0'         = @{ At = 'm3'; Annotated = $false }
}
foreach ($tag in $tags.Keys) {
    $target = $sha[$tags[$tag].At]
    if ($tags[$tag].Annotated) { Invoke-Git $work tag -a -m "Tag $tag" $tag $target | Out-Null }
    else { Invoke-Git $work tag $tag $target | Out-Null }
}
Invoke-Git $work push -q $origin master release/v1.0.x release/v1.1.x feature/x --tags | Out-Null
$originUrl = ([Uri] $origin).AbsoluteUri

# One case: a fresh clone of the tag, one commit deep, as actions/checkout
# makes it; then the script, in its own pwsh, from the clone.
function Test-Tag([string] $Tag, [string] $ExpectBranch, [string] $ExpectRefusal, [switch] $PassTagObject, [string] $Remote = 'origin') {
    $name = "tag check: $Tag" + $(if ($PassTagObject) { ' (tag object as the commit)' } else { '' }) + $(if ($Remote -ne 'origin') { " (remote '$Remote')" } else { '' })
    try {
        $clone = Join-Path $runRoot ('clone-' + [Guid]::NewGuid().ToString('N').Substring(0, 8))
        [void][IO.Directory]::CreateDirectory($clone)
        Invoke-Git $clone init -q | Out-Null
        Invoke-Git $clone remote add origin $originUrl | Out-Null
        Invoke-Git $clone fetch -q --no-tags --depth=1 origin "+refs/tags/${Tag}:refs/tags/$Tag" | Out-Null
        Invoke-Git $clone checkout -q "refs/tags/$Tag" | Out-Null
        if ((Invoke-Git $clone rev-parse --is-shallow-repository).Trim() -cne 'true' -or
            (Invoke-Git $clone for-each-ref refs/heads refs/remotes).Length -ne 0) { throw 'The clone is not shallow and branch-less.' }
        $commit = if ($PassTagObject) { (Invoke-Git $clone rev-parse "refs/tags/$Tag").Trim() }
            else { (Invoke-Git $clone rev-parse 'HEAD').Trim() }
        # pwsh's error view wraps a thrown message to a console width that
        # differs between machines, so the child prints a refusal itself, on
        # one line. It ends as release.yml's pwsh steps do, with the last
        # exit code.
        $command = "try { & $(ConvertTo-Literal $tagScript) -Tag $(ConvertTo-Literal $Tag) -Commit $(ConvertTo-Literal $commit) -Remote $(ConvertTo-Literal $Remote); exit `$LASTEXITCODE } " +
            "catch { 'REFUSED: ' + `$_.Exception.Message; exit 1 }"
        $output = @(& pwsh -NoProfile -NonInteractive -WorkingDirectory $clone -Command $command 2>&1 | ForEach-Object { "$_" })
        $exit = $LASTEXITCODE
        $text = $output -join ' | '
        if ($ExpectBranch) {
            Add-Result $name ($exit -eq 0 -and $output.Count -gt 0 -and $output[-1] -ceq $ExpectBranch) "exit $exit; $text"
        } else {
            $refusals = @($output | Where-Object { $_.StartsWith('REFUSED: ', [StringComparison]::Ordinal) })
            Add-Result $name ($exit -eq 1 -and $refusals.Count -eq 1 -and $refusals[0].Contains($ExpectRefusal, [StringComparison]::Ordinal)) "exit $exit; $text"
        }
    } catch {
        Add-Result $name $false $_.Exception.Message
    }
}

Test-Tag 'v1.1.0' -ExpectBranch 'master'
Test-Tag 'v1.1.0' -ExpectBranch 'master' -PassTagObject
Test-Tag 'v1.2.0-rc.1' -ExpectBranch 'master'
Test-Tag 'v1.0.1' -ExpectBranch 'master'
Test-Tag 'v1.0.2' -ExpectBranch 'release/v1.0.x'
Test-Tag 'v1.0.2' -ExpectBranch 'release/v1.0.x' -PassTagObject
Test-Tag 'v1.0.2-rc.1' -ExpectBranch 'release/v1.0.x'
Test-Tag 'v1.0.3' -ExpectRefusal 'which is not on master or release/v1.0.x.'
Test-Tag 'v1.1.1' -ExpectRefusal 'which is not on master.'
Test-Tag 'v1.1.2' -ExpectRefusal 'which is not on master.'
Test-Tag 'v1.0.4' -ExpectRefusal 'which is not on master or release/v1.0.x.'
Test-Tag 'v1.0.5' -ExpectRefusal 'which is not on master or release/v1.0.x.'
Test-Tag 'v1.2.0' -ExpectRefusal 'which is not on master.'
Test-Tag 'v2.0.0' -ExpectRefusal 'which is not on master.'
Test-Tag 'v1.0' -ExpectRefusal 'is not a supported PaperRoute version tag'
Test-Tag 'v1.0.2' -ExpectRefusal 'Cannot list branch master' -Remote 'nosuchremote'

# The newer-release check, on release lists as the releases API returns them.
$published = @(
    [pscustomobject] @{ tag_name = 'v1.0.0'; draft = $false; prerelease = $false }
    [pscustomobject] @{ tag_name = 'v1.0.1'; draft = $false; prerelease = $false }
    [pscustomobject] @{ tag_name = 'v1.0.2-rc.1'; draft = $false; prerelease = $true }
    [pscustomobject] @{ tag_name = 'v1.1.0-rc.1'; draft = $false; prerelease = $true }
    [pscustomobject] @{ tag_name = 'v1.1.0'; draft = $false; prerelease = $false }
    [pscustomobject] @{ tag_name = 'v1.2.0'; draft = $true; prerelease = $false }
)
$newerCases = @(
    @{ Version = '1.0.2'; Releases = $published[0..1]; Expect = @() }
    @{ Version = '1.0.2'; Releases = $published; Expect = @('v1.1.0') }
    @{ Version = '1.0.3-rc.1'; Releases = $published; Expect = @('v1.1.0-rc.1') }
    @{ Version = '1.1.1'; Releases = $published; Expect = @() }
    @{ Version = '1.1.0-rc.2'; Releases = $published; Expect = @() }
    @{ Version = '1.2.0'; Releases = $published; Expect = @() }
    @{ Version = '0.9.3'; Releases = $published; Expect = @('v1.0.0', 'v1.0.1', 'v1.1.0') }
    @{ Version = '1.0.2'; Releases = @(); Expect = @() }
)
foreach ($case in $newerCases) {
    $name = "newer releases: $($case.Version) among $(@($case.Releases).Count)"
    try {
        $actual = @(& $newerScript -Version $case.Version -Release $case.Releases)
        Add-Result $name ((@($actual) -join ',') -ceq (@($case.Expect) -join ',')) "got '$($actual -join ',')', expected '$($case.Expect -join ',')'"
    } catch {
        Add-Result $name $false $_.Exception.Message
    }
}

$failed = @($results | Where-Object { -not $_.Passed })
$summary = "Release tag checks: $($results.Count - $failed.Count) passed, $($failed.Count) failed, of $($results.Count)."
$results | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath (Join-Path $runRoot 'report.json') -Encoding utf8NoBOM
Write-Host $summary
if ($env:GITHUB_STEP_SUMMARY) { $summary >> $env:GITHUB_STEP_SUMMARY }
if ($failed.Count -gt 0) { throw $summary }

# The cases run git, whose last answer may be 1 ("not an ancestor", an expected
# outcome). A GitHub Actions step ends with whatever $LASTEXITCODE holds, so
# without this line a run that passed every case failed its step.
exit 0
