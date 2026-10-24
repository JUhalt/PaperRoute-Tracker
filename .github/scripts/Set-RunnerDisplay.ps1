# Gives a GitHub Windows runner a 1920x1080 display before the tests run.
#
# The runner's console session starts at 1024x768, and Windows Forms holds a
# top-level window to the largest size the screen can track, so the tests
# that size windows for a certified screen (1920x1080 and 1366x768, #93)
# met a smaller one in CI than on a developer's computer. The tests stay
# correct on either; this makes CI judge what the certified screens show.
#
# It never fails the build: if the display cannot take the size, it says
# so in the log and the step summary, and the tests run at the size there is.
param(
    [int] $Width = 1920,
    [int] $Height = 1080
)

$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName System.Windows.Forms

Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;

public static class RunnerDisplay
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct DEVMODE
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmDeviceName;
        public short dmSpecVersion;
        public short dmDriverVersion;
        public short dmSize;
        public short dmDriverExtra;
        public int dmFields;
        public int dmPositionX;
        public int dmPositionY;
        public int dmDisplayOrientation;
        public int dmDisplayFixedOutput;
        public short dmColor;
        public short dmDuplex;
        public short dmYResolution;
        public short dmTTOption;
        public short dmCollate;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmFormName;
        public short dmLogPixels;
        public int dmBitsPerPel;
        public int dmPelsWidth;
        public int dmPelsHeight;
        public int dmDisplayFlags;
        public int dmDisplayFrequency;
        public int dmICMMethod;
        public int dmICMIntent;
        public int dmMediaType;
        public int dmDitherType;
        public int dmReserved1;
        public int dmReserved2;
        public int dmPanningWidth;
        public int dmPanningHeight;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern bool EnumDisplaySettings(string deviceName, int modeNum, ref DEVMODE devMode);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern int ChangeDisplaySettings(ref DEVMODE devMode, int flags);

    // DISP_CHANGE_SUCCESSFUL is 0; anything else is Windows' reason.
    public static int Set(int width, int height)
    {
        DEVMODE mode = new DEVMODE();
        mode.dmSize = (short)Marshal.SizeOf(typeof(DEVMODE));
        if (!EnumDisplaySettings(null, -1, ref mode)) return -100;
        mode.dmPelsWidth = width;
        mode.dmPelsHeight = height;
        mode.dmFields = 0x80000 | 0x100000; // DM_PELSWIDTH | DM_PELSHEIGHT
        return ChangeDisplaySettings(ref mode, 0);
    }
}
"@

function Describe-Screen {
    $screen = [System.Windows.Forms.Screen]::PrimaryScreen
    "{0}x{1} (working area {2}x{3})" -f $screen.Bounds.Width, $screen.Bounds.Height, $screen.WorkingArea.Width, $screen.WorkingArea.Height
}

$before = Describe-Screen
$result = [RunnerDisplay]::Set($Width, $Height)
Start-Sleep -Milliseconds 500
$after = Describe-Screen

$reasons = @{ 0 = 'changed'; 1 = 'changed, restart needed'; -1 = 'failed'; -2 = 'not a mode this display has'; -100 = 'the current mode could not be read' }
$reason = if ($reasons.ContainsKey($result)) { $reasons[$result] } else { "code $result" }

$line = "Runner display: $before before, $after after asking for ${Width}x${Height} ($reason)."
Write-Host $line
if ($env:GITHUB_STEP_SUMMARY) { $line >> $env:GITHUB_STEP_SUMMARY }
exit 0