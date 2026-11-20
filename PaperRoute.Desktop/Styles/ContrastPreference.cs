using Avalonia.Platform;

namespace PaperRoute.Desktop.Styles;

// Whether a contrast theme is on, and its colours, in a form a headless test
// can drive (d1 5.2). Today's app asks SystemInformation.HighContrast
// (UiTheme.vb:43-45) and has a test seam beside it (HighContrastOverride);
// here the app and the tests share one object:
//
//   - FromPlatform reads Avalonia's PlatformColorValues.ContrastPreference
//     (r2 4.1) and Windows' system colours, and reads both again when Avalonia
//     raises ColorValuesChanged or the app calls Refresh (the window's
//     activation, for a switch between two contrast themes that Avalonia
//     might not report: not checked on a real Windows contrast theme);
//   - a test makes one with its own values and calls Set.
//
// Changed fires only when something the theme depends on has changed.
public sealed class ContrastPreference
{
    private readonly Func<(bool IsHigh, SystemContrastColors Colors)>? _read;

    public ContrastPreference(bool isHigh, SystemContrastColors colors)
    {
        IsHigh = isHigh;
        Colors = colors;
    }

    private ContrastPreference(Func<(bool IsHigh, SystemContrastColors Colors)> read)
    {
        _read = read;
        (IsHigh, Colors) = read();
    }

    public event EventHandler? Changed;

    public bool IsHigh { get; private set; }

    public SystemContrastColors Colors { get; private set; }

    // The running platform's preference. The system colours are read only
    // while a contrast theme is on; otherwise they keep the fallback set,
    // which nothing shows.
    public static ContrastPreference FromPlatform(IPlatformSettings? settings)
    {
        var preference = new ContrastPreference(() =>
        {
            bool high = settings?.GetColorValues().ContrastPreference == ColorContrastPreference.High;
            return (high, high ? SystemContrastColors.Read() : SystemContrastColors.Fallback);
        });
        if (settings is not null)
        {
            settings.ColorValuesChanged += (_, _) => preference.Refresh();
        }

        return preference;
    }

    // Reads the platform again (a preference from FromPlatform only).
    public void Refresh()
    {
        if (_read is not null)
        {
            (bool high, SystemContrastColors colors) = _read();
            Set(high, colors);
        }
    }

    // Takes new values; raises Changed if they differ from the old ones.
    public void Set(bool isHigh, SystemContrastColors colors)
    {
        if (isHigh == IsHigh && colors == Colors)
        {
            return;
        }

        IsHigh = isHigh;
        Colors = colors;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
