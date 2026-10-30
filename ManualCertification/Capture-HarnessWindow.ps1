#requires -Version 7.2
<#
.SYNOPSIS
Launch the disposable UI harness, wait until it reports its window painted, capture the window, and write evidence.
.EXAMPLE
pwsh -NoProfile -File .\ManualCertification\Capture-HarnessWindow.ps1 -HarnessPath .\ManualCertification\V04Demo\bin\Debug\net10.0-windows\PaperRoute.V04Demo.exe board --primary
.NOTES
The harness writes a "painted" marker (--painted <file>) once the shown window's load has finished and its
message loop has gone idle. This script waits for that marker and fails on timeout instead of capturing a
blank window; the marker's time goes into the evidence JSON (#146). PrintWindow reads the window's own
pixels, so other windows may overlap it. Evidence goes to a fresh directory under the ignored artifacts
folder unless -OutputDirectory says otherwise. The harness process is ended when the script finishes.
#>
[CmdletBinding(PositionalBinding = $false)]
param(
    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string] $HarnessPath,

    # The harness's own arguments (surface and options), given after the named parameters.
    # They are the only positional arguments, so a surface never binds to -Name.
    [Parameter(Mandatory, ValueFromRemainingArguments)]
    [ValidateNotNullOrEmpty()]
    [string[]] $HarnessArguments,

    [ValidatePattern('^[A-Za-z0-9][A-Za-z0-9._-]*$')]
    [string] $Name = '',

    [string] $OutputDirectory = '',

    [ValidateRange(1, 3600)]
    [int] $TimeoutSeconds = 120
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not $IsWindows) { throw 'Window capture requires Windows.' }

$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$harness = Get-Item -LiteralPath $HarnessPath
if ($harness.PSIsContainer) { throw "HarnessPath must be the harness executable, not a directory: '$HarnessPath'." }
$surface = @($HarnessArguments | Where-Object { -not $_.StartsWith('--') } | Select-Object -First 1)
if (-not $Name) { $Name = if ($surface.Count -gt 0) { $surface[0] } else { 'capture' } }
if (-not $OutputDirectory) {
    $OutputDirectory = Join-Path $repoRoot ('artifacts/captures/{0}-{1}' -f $Name, [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss'))
}
$outputRoot = [IO.Path]::GetFullPath($OutputDirectory, $repoRoot)
[void][IO.Directory]::CreateDirectory($outputRoot)
$markerPath = Join-Path $outputRoot "$Name.painted.json"
if (Test-Path -LiteralPath $markerPath) { Remove-Item -LiteralPath $markerPath }
$utf8 = [Text.UTF8Encoding]::new($false)

Add-Type -AssemblyName System.Drawing
if (-not ('HarnessCapture' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
public static class HarnessCapture
{
    delegate bool EnumProc(IntPtr handle, IntPtr parameter);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc callback, IntPtr parameter);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr handle, out uint processId);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr handle);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowText(IntPtr handle, StringBuilder text, int capacity);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr handle, out RECT rectangle);
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr handle, IntPtr hdc, uint flags);
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr handle, int attribute, out RECT value, int size);
    public struct RECT { public int Left, Top, Right, Bottom; }
    public const uint PW_RENDERFULLCONTENT = 2;
    public const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;
    // Visible top-level windows of one process, in Z order.
    public static List<IntPtr> Windows(int processId)
    {
        var list = new List<IntPtr>();
        EnumWindows((handle, _) =>
        {
            uint owner;
            GetWindowThreadProcessId(handle, out owner);
            if (owner == processId && IsWindowVisible(handle)) list.Add(handle);
            return true;
        }, IntPtr.Zero);
        return list;
    }
    public static string Title(IntPtr handle)
    {
        var text = new StringBuilder(512);
        GetWindowText(handle, text, text.Capacity);
        return text.ToString();
    }
}
'@
}
# Physical pixels from GetWindowRect, so the bitmap matches the window at any scale.
[void][HarnessCapture]::SetProcessDPIAware()

function Save-Window([IntPtr] $Handle, [string] $Path) {
    $rectangle = [HarnessCapture+RECT]::new()
    if (-not [HarnessCapture]::GetWindowRect($Handle, [ref] $rectangle)) { throw 'GetWindowRect failed.' }
    $width = $rectangle.Right - $rectangle.Left
    $height = $rectangle.Bottom - $rectangle.Top
    if ($width -le 0 -or $height -le 0) { throw "The window has no area to capture ($width x $height)." }
    # The frame Windows draws (#259): the window's rectangle also holds its invisible resize borders, which
    # PrintWindow paints as black strips on the left, right and bottom. Without the frame's bounds, the whole.
    $visible = [Drawing.Rectangle]::new(0, 0, $width, $height)
    $frame = [HarnessCapture+RECT]::new()
    if ([HarnessCapture]::DwmGetWindowAttribute($Handle, [HarnessCapture]::DWMWA_EXTENDED_FRAME_BOUNDS, [ref] $frame, 16) -eq 0) {
        $visible = [Drawing.Rectangle]::Intersect($visible, [Drawing.Rectangle]::FromLTRB(
            $frame.Left - $rectangle.Left, $frame.Top - $rectangle.Top, $frame.Right - $rectangle.Left, $frame.Bottom - $rectangle.Top))
        if ($visible.Width -le 0 -or $visible.Height -le 0) { throw 'The window''s visible frame is outside its rectangle.' }
    }
    $bitmap = [Drawing.Bitmap]::new($width, $height)
    try {
        $graphics = [Drawing.Graphics]::FromImage($bitmap)
        try {
            $hdc = $graphics.GetHdc()
            try { $painted = [HarnessCapture]::PrintWindow($Handle, $hdc, [HarnessCapture]::PW_RENDERFULLCONTENT) }
            finally { $graphics.ReleaseHdc($hdc) }
            if (-not $painted) { throw 'PrintWindow failed.' }
        }
        finally { $graphics.Dispose() }
        $cropped = $bitmap.Clone($visible, $bitmap.PixelFormat)
        try { $cropped.Save($Path, [Drawing.Imaging.ImageFormat]::Png) }
        finally { $cropped.Dispose() }
    }
    finally { $bitmap.Dispose() }
    return [ordered]@{ Width = $visible.Width; Height = $visible.Height }
}

function Get-Sha256([string] $Path) {
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

$startInfo = [Diagnostics.ProcessStartInfo]::new($harness.FullName)
$startInfo.UseShellExecute = $false
# Under --painted the harness reports failures here instead of in a dialog.
$startInfo.RedirectStandardError = $true
foreach ($argument in $HarnessArguments + @('--painted', $markerPath)) { $startInfo.ArgumentList.Add($argument) }
$startedAt = [DateTime]::UtcNow
$process = [Diagnostics.Process]::Start($startInfo)
$harnessErrors = $process.StandardError.ReadToEndAsync()
try {
    $deadline = $startedAt.AddSeconds($TimeoutSeconds)
    $marker = $null
    while ($null -eq $marker) {
        if ($process.HasExited) {
            $said = $harnessErrors.GetAwaiter().GetResult().Trim()
            throw "The harness exited with code $($process.ExitCode) before it reported the window painted; nothing was captured.$(if ($said) { " The harness said: $said" })"
        }
        if ([DateTime]::UtcNow -ge $deadline) {
            # Say whether a window was up: still loading, or never shown.
            $visible = @([HarnessCapture]::Windows($process.Id) | ForEach-Object { [HarnessCapture]::Title($_) } | Where-Object { $_ -ne '' })
            $state = if ($visible.Count -gt 0) { "a window was visible but had not finished loading: '$($visible -join "', '")'" }
                else { 'no window was visible (the harness had not shown one, or this build does not write the marker)' }
            throw "Timed out after $TimeoutSeconds s waiting for the painted marker '$markerPath'; nothing was captured. At the timeout $state."
        }
        if (Test-Path -LiteralPath $markerPath -PathType Leaf) {
            $marker = Get-Content -LiteralPath $markerPath -Raw | ConvertFrom-Json
            if ($null -eq $marker.PSObject.Properties['PaintedAtUtc']) { throw "The painted marker '$markerPath' has no PaintedAtUtc." }
        }
        if ($null -eq $marker) { Start-Sleep -Milliseconds 250 }
    }
    $waitedSeconds = [Math]::Round(([DateTime]::UtcNow - $startedAt).TotalSeconds, 2)

    $handles = @([HarnessCapture]::Windows($process.Id) | Where-Object { [HarnessCapture]::Title($_) -ne '' })
    if ($handles.Count -eq 0) { throw 'The harness reported its window painted, but it has no visible titled window to capture.' }
    $windows = [Collections.Generic.List[object]]::new()
    for ($index = 0; $index -lt $handles.Count; $index++) {
        $fileName = if ($handles.Count -eq 1) { "$Name.png" } else { '{0}-{1}.png' -f $Name, $index }
        $path = Join-Path $outputRoot $fileName
        $size = Save-Window $handles[$index] $path
        $windows.Add([ordered]@{
            Title = [HarnessCapture]::Title($handles[$index]); Width = $size.Width; Height = $size.Height
            File = $fileName; Sha256 = (Get-Sha256 $path)
        })
        Write-Host ('{0}  {1}x{2}  {3}' -f $path, $size.Width, $size.Height, $windows[$index].Title)
    }
    $applicationDll = Join-Path $harness.DirectoryName 'PaperRouteTracker.dll'
    $evidence = [ordered]@{
        Name = $Name; Harness = $harness.FullName; HarnessSha256 = (Get-Sha256 $harness.FullName)
        ApplicationDllSha256 = if (Test-Path -LiteralPath $applicationDll -PathType Leaf) { Get-Sha256 $applicationDll } else { $null }
        Arguments = @($HarnessArguments); StartedAtUtc = $startedAt.ToString('o'); Painted = $marker
        WaitedSeconds = $waitedSeconds; CapturedAtUtc = [DateTime]::UtcNow.ToString('o')
        CaptureMethod = 'PrintWindow PW_RENDERFULLCONTENT, cropped to DWMWA_EXTENDED_FRAME_BOUNDS'; Windows = @($windows.ToArray())
    }
    $evidencePath = Join-Path $outputRoot "$Name.json"
    [IO.File]::WriteAllText($evidencePath, (ConvertTo-Json -InputObject $evidence -Depth 6), $utf8)
    Write-Host "Painted at $($marker.PaintedAtUtc); waited $waitedSeconds s. Evidence: $evidencePath"
}
finally {
    if (-not $process.HasExited) {
        $process.Kill()
        $process.WaitForExit()
    }
}
