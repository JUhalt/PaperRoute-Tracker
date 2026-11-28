using System.Runtime.InteropServices;
using Avalonia.Media;

namespace PaperRoute.Desktop.Styles;

// The six system colours a Windows contrast theme gives and today's app maps
// its tokens to (UiTheme.vb:38-45, 95-623; d1 5.2): the window, its text, the
// inactive (gray) text, the selection and its text, and the hyperlink colour.
//
// Read() asks Windows (user32!GetSysColor) on Windows, and off Windows gives a
// fixed black-on-white set until the Linux theme question is looked at (d1
// 5.2). Hc1, NightSky, Hcblack and Desert are Windows' four contrast themes,
// fixed for tests and pictures: their values were read from the theme files
// (hc1.theme, hc2.theme, hcblack.theme and hcwhite.theme in Windows'
// "Ease of Access Themes" folder, section [Control Panel\Colors], on
// 2026-11-27), and R2_TheContrastSetsAreWindowsOwn compares them with those
// files on every Windows run.
public readonly record struct SystemContrastColors(
    Color Window,
    Color WindowText,
    Color GrayText,
    Color Highlight,
    Color HighlightText,
    Color HotTrack)
{
    // GetSysColor indices (winuser.h).
    internal const int ColorWindow = 5;
    internal const int ColorWindowText = 8;
    internal const int ColorHighlight = 13;
    internal const int ColorHighlightText = 14;
    internal const int ColorGrayText = 17;
    internal const int ColorHotLight = 26;

    // Off Windows: black on white, the selection and links #1A0DAB (d1 5.2).
    public static SystemContrastColors Fallback { get; } = new(
        Window: Color.FromRgb(0xFF, 0xFF, 0xFF),
        WindowText: Color.FromRgb(0x00, 0x00, 0x00),
        GrayText: Color.FromRgb(0x6D, 0x6D, 0x6D),
        Highlight: Color.FromRgb(0x1A, 0x0D, 0xAB),
        HighlightText: Color.FromRgb(0xFF, 0xFF, 0xFF),
        HotTrack: Color.FromRgb(0x1A, 0x0D, 0xAB));

    // hc1.theme, a dark set.
    public static SystemContrastColors Hc1 { get; } = new(
        Window: Color.FromRgb(0x2D, 0x32, 0x36),
        WindowText: Color.FromRgb(0xFF, 0xFF, 0xFF),
        GrayText: Color.FromRgb(0xA6, 0xA6, 0xA6),
        Highlight: Color.FromRgb(0xA1, 0xBF, 0xDE),
        HighlightText: Color.FromRgb(0x21, 0x2D, 0x3B),
        HotTrack: Color.FromRgb(0x70, 0xEB, 0xDE));

    // hc2.theme, a dark set. The trial's stand-in for "Night sky", written
    // from memory, already held these values.
    public static SystemContrastColors NightSky { get; } = new(
        Window: Color.FromRgb(0x00, 0x00, 0x00),
        WindowText: Color.FromRgb(0xFF, 0xFF, 0xFF),
        GrayText: Color.FromRgb(0xA6, 0xA6, 0xA6),
        Highlight: Color.FromRgb(0xD6, 0xB4, 0xFD),
        HighlightText: Color.FromRgb(0x2B, 0x2B, 0x2B),
        HotTrack: Color.FromRgb(0x80, 0x80, 0xFF));

    // hcblack.theme, a dark set.
    public static SystemContrastColors Hcblack { get; } = new(
        Window: Color.FromRgb(0x20, 0x20, 0x20),
        WindowText: Color.FromRgb(0xFF, 0xFF, 0xFF),
        GrayText: Color.FromRgb(0xA6, 0xA6, 0xA6),
        Highlight: Color.FromRgb(0x8E, 0xE3, 0xF0),
        HighlightText: Color.FromRgb(0x26, 0x3B, 0x50),
        HotTrack: Color.FromRgb(0x75, 0xE9, 0xFC));

    // hcwhite.theme, a light set. The trial's stand-in for "Desert", written
    // from memory, already held these values.
    public static SystemContrastColors Desert { get; } = new(
        Window: Color.FromRgb(0xFF, 0xFA, 0xEF),
        WindowText: Color.FromRgb(0x3D, 0x3D, 0x3D),
        GrayText: Color.FromRgb(0x67, 0x67, 0x67),
        Highlight: Color.FromRgb(0x90, 0x39, 0x09),
        HighlightText: Color.FromRgb(0xFF, 0xF5, 0xE3),
        HotTrack: Color.FromRgb(0x1C, 0x5E, 0x75));

    // Whether the window colour is dark: today's IsDark test, Color.GetBrightness
    // below one half (UiTheme.vb:68), which is HSL lightness, (max + min) / 2.
    public bool IsDark => (Math.Max(Window.R, Math.Max(Window.G, Window.B)) + Math.Min(Window.R, Math.Min(Window.G, Window.B))) / 510.0 < 0.5;

    // The colours of the running system: Windows' own, or the fallback.
    public static SystemContrastColors Read()
    {
        if (!OperatingSystem.IsWindows())
        {
            return Fallback;
        }

        return new SystemContrastColors(
            Window: FromColorRef(NativeMethods.GetSysColor(ColorWindow)),
            WindowText: FromColorRef(NativeMethods.GetSysColor(ColorWindowText)),
            GrayText: FromColorRef(NativeMethods.GetSysColor(ColorGrayText)),
            Highlight: FromColorRef(NativeMethods.GetSysColor(ColorHighlight)),
            HighlightText: FromColorRef(NativeMethods.GetSysColor(ColorHighlightText)),
            HotTrack: FromColorRef(NativeMethods.GetSysColor(ColorHotLight)));
    }

    // A COLORREF is 0x00BBGGRR: red in the low byte (GetRValue, GetGValue,
    // GetBValue; learn.microsoft.com GetSysColor, read 2026-11-15 by d1).
    public static Color FromColorRef(uint colorRef) => Color.FromRgb(
        (byte)(colorRef & 0xFF),
        (byte)((colorRef >> 8) & 0xFF),
        (byte)((colorRef >> 16) & 0xFF));

    private static class NativeMethods
    {
        [DllImport("user32.dll")]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        public static extern uint GetSysColor(int index);
    }
}
