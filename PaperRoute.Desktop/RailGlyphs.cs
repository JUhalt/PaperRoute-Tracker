using Avalonia.Data.Converters;
using Avalonia.Media;

namespace PaperRoute.Desktop;

// The rail's line icons on a 16-unit grid, as RailPainter.DrawGlyph draws them
// today (ManuscriptPipeline\Controls\RailItems.vb:144-227), turned into path
// data: rectangles as closed paths, GDI+ arcs as SVG arcs, curves as Béziers.
// The view strokes them in the item's ink at 1.5 px with round caps.
public static class RailGlyphs
{
    private static readonly Dictionary<string, string> s_paths = new(StringComparer.Ordinal)
    {
        ["Board"] = "M1.5,2.5 H5.5 V13.5 H1.5 Z M6.5,2.5 H10.5 V9.5 H6.5 Z M11.5,2.5 H14.5 V11.5 H11.5 Z",
        ["Library"] = "M2,4 L14,4 M2,8 L14,8 M2,12 L14,12",
        ["Journals"] = "M3,2.5 L12.5,2.5 L12.5,13.5 L4,13.5 L3,12.5 Z M5.5,5.5 L10,5.5",
        ["Deadlines"] = "M2,3 H14 V14 H2 Z M2,6.5 L14,6.5 M5.5,1.5 L5.5,4.5 M10.5,1.5 L10.5,4.5",
        ["Insights"] = "M2,13.5 L14,13.5 M3.5,8 H5.5 V13 H3.5 Z M7,3 H9 V13 H7 Z M10.5,6 H12.5 V13 H10.5 Z",
        ["ImportExport"] = "M5,2.5 L5,11.5 M2.5,9 L5,11.5 L7.5,9 M11,13.5 L11,4.5 M8.5,7 L11,4.5 L13.5,7",
        ["Online"] = "M4.5,13 A3,3 0 0 1 4.5,7 M4,7.5 A4,4 0 0 1 12,7.5 M12,6.5 A2.5,3.25 0 0 1 12,13 M4.5,13 L12,13",
        ["Settings"] =
            "M5.8,8 A2.2,2.2 0 1 1 10.2,8 A2.2,2.2 0 1 1 5.8,8 " +
            "M12.4,8 L14.2,8 M11.11,11.11 L12.38,12.38 M8,12.4 L8,14.2 M4.89,11.11 L3.62,12.38 " +
            "M3.6,8 L1.8,8 M4.89,4.89 L3.62,3.62 M8,3.6 L8,1.8 M11.11,4.89 L12.38,3.62",
        ["Help"] =
            "M2,8 A6,6 0 1 1 14,8 A6,6 0 1 1 2,8 " +
            "M6.4,6.3 C6.4,4.3 9.8,4.3 9.6,6.6 C9.4,8 8,8 8,9.6 M8,11.4 L8,11.6",
    };

    // The glyph names the rail uses.
    public static IReadOnlyCollection<string> Names => s_paths.Keys;

    // Binds a RailItemViewModel.Glyph to a Path's Data; null for an unknown name.
    public static FuncValueConverter<string?, Geometry?> ToGeometry { get; } = new(GeometryOf);

    // A new geometry for a glyph, or null. Not cached: a geometry is an
    // AvaloniaObject, bound to the thread that made it.
    public static Geometry? GeometryOf(string? name) =>
        name is not null && s_paths.TryGetValue(name, out string? data) ? Geometry.Parse(data) : null;
}
