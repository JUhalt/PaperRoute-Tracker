using System.Globalization;
using Avalonia.Data.Converters;

namespace PaperRoute.Desktop;

// Converters the Board's views use in compiled bindings.
public static class BoardConverters
{
    // True when the bound index equals the parameter: a shelf is shown while
    // its index is the selected shelf's (d1 4.1, "IsVisible = its index ==
    // SelectedShelfIndex").
    public static readonly IValueConverter IsIndex = new IndexConverter();

    // True when the bound value's name equals the parameter: a card's tone
    // ("Warning", "Danger", "Muted") or a route dot's state ("Closed",
    // "Current", "Accepted") turns its style class on.
    public static readonly IValueConverter IsNamed = new NameConverter();

    private sealed class IndexConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            value is int index
            && int.TryParse(System.Convert.ToString(parameter, CultureInfo.InvariantCulture), NumberStyles.Integer, CultureInfo.InvariantCulture, out int wanted)
            && index == wanted;

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            throw new NotSupportedException("A shelf's visibility is shown, never set.");
    }

    private sealed class NameConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            value is not null
            && string.Equals(System.Convert.ToString(value, CultureInfo.InvariantCulture), System.Convert.ToString(parameter, CultureInfo.InvariantCulture), StringComparison.Ordinal);

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            throw new NotSupportedException("A style class is shown, never set.");
    }
}
