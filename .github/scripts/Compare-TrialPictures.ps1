#requires -Version 7.0
# Lists every picture that differs between two folders of Avalonia trial
# pictures, with the number of pixels that differ.
#
# The trial's view tests save PNG pictures in TestResults/pictures, which the
# CI job "Avalonia Board trial (Windows)" uploads in its artifact
# PaperRoute-trial-results. Download that artifact for master and for a
# branch and point this script at the two pictures folders: a pull request
# lists the pictures it changes, with these counts and why (the harness and
# style design, part 3.6). The pictures are judged by rules, never as golden
# images; this script only shows what changed.
#
# One row per picture that is in one folder only, that changed size, or that
# has a pixel of another colour (alpha included), then a count of the
# pictures that are the same. It never fails because pictures differ.
#
# Windows only: it reads the PNG files through System.Drawing.
param(
    [Parameter(Mandatory)] [string] $Before,
    [Parameter(Mandatory)] [string] $After
)

$ErrorActionPreference = 'Stop'

if (-not $IsWindows) { throw 'Compare-TrialPictures.ps1 reads PNG files through System.Drawing, which needs Windows.' }
foreach ($folder in $Before, $After) {
    if (-not (Test-Path -LiteralPath $folder -PathType Container)) { throw "No folder at $folder." }
}

Add-Type -AssemblyName System.Drawing

# The pixel count runs in compiled code: a loop over a million pixels is slow
# in a script. It takes plain arrays, so it needs no reference to System.Drawing.
if (-not ('TrialPixelCount' -as [type])) {
    Add-Type -TypeDefinition @"
public static class TrialPixelCount
{
    public static long Differing(int[] first, int[] second)
    {
        long differing = 0;
        for (int i = 0; i < first.Length; i++)
        {
            if (first[i] != second[i])
            {
                differing++;
            }
        }

        return differing;
    }
}
"@
}

# The picture's size and its pixels as 32-bit ARGB values, row by row.
function Read-Picture([string] $Path) {
    $bitmap = [System.Drawing.Bitmap]::new($Path)
    try {
        $width = $bitmap.Width
        $height = $bitmap.Height
        $data = $bitmap.LockBits([System.Drawing.Rectangle]::new(0, 0, $width, $height),
            [System.Drawing.Imaging.ImageLockMode]::ReadOnly, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        try {
            $pixels = [int[]]::new($width * $height)
            for ($y = 0; $y -lt $height; $y++) {
                [Runtime.InteropServices.Marshal]::Copy([IntPtr]::Add($data.Scan0, $y * $data.Stride), $pixels, $y * $width, $width)
            }
        }
        finally {
            $bitmap.UnlockBits($data)
        }
    }
    finally {
        $bitmap.Dispose()
    }
    return [pscustomobject] @{ Width = $width; Height = $height; Pixels = $pixels }
}

function Get-Pictures([string] $Folder) {
    $root = (Resolve-Path -LiteralPath $Folder).Path
    $pictures = @{}
    foreach ($file in Get-ChildItem -LiteralPath $root -Recurse -File -Filter '*.png') {
        $pictures[[IO.Path]::GetRelativePath($root, $file.FullName).Replace('\', '/')] = $file.FullName
    }
    return $pictures
}

$old = Get-Pictures $Before
$new = Get-Pictures $After
$names = @(@($old.Keys) + @($new.Keys) | Sort-Object -Unique)
$same = 0
$rows = @(foreach ($name in $names) {
    if (-not $new.ContainsKey($name)) {
        [pscustomobject] @{ Picture = $name; Change = 'only before'; DifferingPixels = $null; Pixels = $null }
        continue
    }
    if (-not $old.ContainsKey($name)) {
        [pscustomobject] @{ Picture = $name; Change = 'only after'; DifferingPixels = $null; Pixels = $null }
        continue
    }
    $one = Read-Picture $old[$name]
    $two = Read-Picture $new[$name]
    if ($one.Width -ne $two.Width -or $one.Height -ne $two.Height) {
        $change = "size $($one.Width) x $($one.Height) to $($two.Width) x $($two.Height)"
        [pscustomobject] @{ Picture = $name; Change = $change; DifferingPixels = $null; Pixels = $null }
        continue
    }
    $differing = [TrialPixelCount]::Differing($one.Pixels, $two.Pixels)
    if ($differing -gt 0) {
        [pscustomobject] @{ Picture = $name; Change = 'pixels'; DifferingPixels = $differing; Pixels = $one.Pixels.Length }
    }
    else {
        $same++
    }
})

$rows
Write-Host "$($rows.Count) of $($names.Count) pictures differ; $same are the same."
