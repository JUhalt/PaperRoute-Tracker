#requires -Version 7.0
<#
.SYNOPSIS
List the published releases on a version's channel whose version number is higher.
.DESCRIPTION
release.yml asks this before it downloads the previous release for the delta
and before it marks a Stable release Latest. A patch on an older line, such
as v1.0.3 after v1.1.0, is not the newest release: it must not become Latest,
and its delta must not start from the newer line's package.
The channel is the release's: Preview for a version with a pre-release label,
Stable for one without. Drafts are left out. Versions compare by their three
numbers only, so 1.1.0-rc.2 is not newer than 1.1.0-rc.1, and a release
with the same three numbers is never newer.
Prints the tag of each newer release; prints nothing when there is none.
.PARAMETER Release
GitHub release objects (tag_name, draft, prerelease), as the releases API
returns them. Without it, the script reads them with gh from -Repository.
.EXAMPLE
./.github/scripts/Get-NewerRelease.ps1 -Version 1.0.3 -Repository JUhalt/PaperRoute-Tracker
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [ValidatePattern('^\d+\.\d+\.\d+(?:-[0-9A-Za-z]+(?:[.-][0-9A-Za-z]+)*)?$')] [string] $Version,
    [ValidatePattern('^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$')] [string] $Repository,
    [object[]] $Release
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false

if (-not $PSBoundParameters.ContainsKey('Release')) {
    if ([string]::IsNullOrEmpty($Repository)) { throw 'Give -Repository or -Release.' }
    $json = gh api "repos/$Repository/releases?per_page=100" --paginate --slurp
    if ($LASTEXITCODE -ne 0) { throw 'Cannot read the published releases.' }
    $pages = ($json | Out-String) | ConvertFrom-Json -NoEnumerate
    $Release = @($pages | ForEach-Object { $_ } | ForEach-Object { $_ })
}

$isPrerelease = $Version.Contains('-')
$numbers = [version] ($Version -split '-', 2)[0]

foreach ($item in @($Release)) {
    if ($null -eq $item -or $item.draft -or [bool] $item.prerelease -ne $isPrerelease) { continue }
    if ([string] $item.tag_name -cmatch '^v(?<numbers>\d+\.\d+\.\d+)(?:-|$)' -and
        [version] $Matches.numbers -gt $numbers) {
        [string] $item.tag_name
    }
}
