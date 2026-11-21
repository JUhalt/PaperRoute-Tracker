#requires -Version 7.0
<#
.SYNOPSIS
Refuse a release tag whose commit is on neither master nor the release branch of its own line.
.DESCRIPTION
release.yml runs this right after the checkout, before anything is built.
A tag vA.B.C (with or without a pre-release label) is accepted when its commit
is on master, or on release/vA.B.x, the branch of its own A.B line: v1.0.2 may
come from release/v1.0.x (the maintainer's decision of November 21, 2026). A
1.0 tag on any other branch, or a 1.1 tag on release/v1.0.x alone, is refused.
"On a branch" means the commit is the branch's tip or an ancestor of it.
The checkout holds only the tagged commit, so the two branches are fetched
first. A branch the remote does not have counts as one the commit is not on.
Prints the branch the commit was found on; throws when the tag is refused or
when it cannot be checked.
.EXAMPLE
./.github/scripts/Assert-ReleaseTagBranch.ps1 -Tag $env:GITHUB_REF_NAME -Commit $env:GITHUB_SHA
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $Tag,
    [Parameter(Mandatory)] [string] $Commit,
    [ValidatePattern('^[A-Za-z0-9_.-]+$')] [string] $Remote = 'origin'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false

if ($Tag -cnotmatch '^v(?<major>\d+)\.(?<minor>\d+)\.\d+(?:-[0-9A-Za-z]+(?:[.-][0-9A-Za-z]+)*)?$') {
    throw "Release tag '$Tag' is not a supported PaperRoute version tag."
}
$lineBranch = "release/v$($Matches.major).$($Matches.minor).x"

# An annotated tag's object peels to its commit.
$sha = git rev-parse --verify --quiet "$Commit^{commit}"
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($sha)) {
    throw "Cannot find the commit of release tag '$Tag' ($Commit)."
}
$sha = $sha.Trim()

foreach ($branch in 'master', $lineBranch) {
    # ls-remote tells a branch that is not there (exit 2 with
    # --exit-code) from one that cannot be read (any other failure).
    git ls-remote --exit-code --heads $Remote "refs/heads/$branch" | Out-Null
    $listed = $LASTEXITCODE
    if ($listed -eq 2) {
        Write-Host "The remote has no branch $branch."
        continue
    }
    if ($listed -ne 0) { throw "Cannot list branch $branch to check release tag '$Tag'." }

    git fetch --no-tags $Remote "+refs/heads/${branch}:refs/remotes/$Remote/$branch"
    if ($LASTEXITCODE -ne 0) { throw "Cannot fetch $branch to check release tag '$Tag'." }

    git merge-base --is-ancestor $sha "refs/remotes/$Remote/$branch"
    $onBranch = $LASTEXITCODE
    if ($onBranch -eq 0) {
        Write-Host "Release tag $Tag is at $sha, which is on $branch."
        $branch
        return
    }
    if ($onBranch -ne 1) { throw "Cannot check whether release tag '$Tag' is on $branch." }
}

throw "Release tag '$Tag' is at $sha, which is on neither master nor $lineBranch. A release tag must be on master or on the release branch of its own line."
