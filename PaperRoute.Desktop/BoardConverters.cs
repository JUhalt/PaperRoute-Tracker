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

    private sealed class IndexConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            value is int index
            && int.TryParse(System.Convert.ToString(parameter, CultureInfo.InvariantCulture), NumberStyles.Integer, CultureInfo.InvariantCulture, out int wanted)
            && index == wanted;

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            throw new NotSupportedException("A shelf's visibility is shown, never set.");
    }
}
