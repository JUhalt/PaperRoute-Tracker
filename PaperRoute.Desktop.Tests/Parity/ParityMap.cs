using System.Globalization;

namespace PaperRoute.Desktop.Tests.Parity;

// The target catalog and the parity map as data (harness design 2.3): two
// tab-separated files beside this one, copied to the test output.
// target-surfaces.tsv lists every surface the Avalonia interface will have;
// parity-map.tsv gives each of the WinForms catalog's 85 keys
// (PaperRoute.Tests/ViewCatalog.vb) its destination there, or "removed" with
// the target map's reason. Tabs, because hosted and stage keys hold commas.
//
// The reader is strict: a header line, "#" comments, no empty cell except the
// optional last one (note, reason), which a line may leave out. Every
// malformed line is reported with its file and line number. The checks
// return one line per finding and assert nothing; ParityMapTests asserts.
// No Avalonia object is made.
internal sealed class ParityMap
{
    public const string SurfacesFile = "target-surfaces.tsv";
    public const string MapFile = "parity-map.tsv";

    public const string Removed = "removed";
    public const string Planned = "planned";
    public const string Built = "built";

    // The inventory of the target map (Surface-Inventory-and-Target-Map.md,
    // section 3) has rows 1 to 97.
    public const int InventoryRows = 97;

    // The build issues of v1.1 on GitHub: #418 is the plan's build 3 (the
    // first with an issue) and #439 its build 24.
    public const int FirstBuildIssue = 418;
    public const int LastBuildIssue = 439;

    // The WinForms catalog's parts and their sizes (ViewCatalog.vb: Dialogs,
    // Hosted, Stages). A new WinForms view fails the VB lock
    // (ParityMapKeyTests.TheParityMapNamesEveryCatalogKeyOnce) until it has a
    // row, and this table until its count is raised with it.
    public static IReadOnlyDictionary<string, int> CatalogParts { get; } =
        new Dictionary<string, int>(StringComparer.Ordinal) { ["dialog"] = 63, ["hosted"] = 12, ["stage"] = 10 };

    private static readonly string[] s_surfaceColumns = ["key", "map rows", "built by", "state", "read from", "note"];
    private static readonly string[] s_mapColumns = ["part", "key", "destination", "map row", "read from", "reason"];

    private ParityMap(IReadOnlyList<TargetSurfaceRow> surfaces, IReadOnlyList<ParityRow> views)
    {
        Surfaces = surfaces;
        Views = views;
    }

    public IReadOnlyList<TargetSurfaceRow> Surfaces { get; }

    public IReadOnlyList<ParityRow> Views { get; }

    public static string Folder => Path.Combine(AppContext.BaseDirectory, "Parity");

    public static ParityMap Load() =>
        Parse(File.ReadAllText(Path.Combine(Folder, SurfacesFile)), File.ReadAllText(Path.Combine(Folder, MapFile)));

    // Throws a FormatException naming every malformed line as "file:line: what".
    public static ParityMap Parse(string surfacesText, string mapText)
    {
        var problems = new List<string>();

        var surfaces = new List<TargetSurfaceRow>();
        foreach (TableLine line in ReadTable(SurfacesFile, surfacesText, s_surfaceColumns, problems))
        {
            if (TryReadRows(line, 1, "map rows", problems, out IReadOnlyList<int> rows))
            {
                surfaces.Add(new TargetSurfaceRow(line.Cells[0], rows, line.Cells[2], line.Cells[3], line.Cells[4], line.Cells[5], line.Where));
            }
        }

        var views = new List<ParityRow>();
        foreach (TableLine line in ReadTable(MapFile, mapText, s_mapColumns, problems))
        {
            if (TryReadRows(line, 3, "map row", problems, out IReadOnlyList<int> rows))
            {
                views.Add(new ParityRow(line.Cells[0], line.Cells[1], line.Cells[2], rows, line.Cells[4], line.Cells[5], line.Where));
            }
        }

        if (problems.Count > 0)
        {
            throw new FormatException(string.Join(Environment.NewLine, problems));
        }

        return new ParityMap(surfaces, views);
    }

    // Every view needs a destination or a named reason: the parts are the
    // catalog's, each part has the catalog's number of rows (so a view with
    // no row shows as a short part), and a removed view says why.
    public IReadOnlyList<string> ViewsWithoutADestinationOrReason()
    {
        var findings = new List<string>();
        foreach (ParityRow view in Views)
        {
            if (!CatalogParts.ContainsKey(view.Part))
            {
                findings.Add($"{view.Where}: \"{view.Key}\" has the part \"{view.Part}\", not dialog, hosted or stage.");
            }

            if (view.Destination == Removed && view.Reason.Length == 0)
            {
                findings.Add($"{view.Where}: {view.Part} \"{view.Key}\" is removed with no reason.");
            }
        }

        foreach ((string part, int expected) in CatalogParts)
        {
            int rows = Views.Count(view => view.Part == part);
            if (rows != expected)
            {
                findings.Add($"{MapFile}: {rows} {part} rows; the WinForms catalog has {expected} (a view has no row, or a row is not a catalog key).");
            }
        }

        return findings;
    }

    public IReadOnlyList<string> DestinationsThatAreNotSurfaces()
    {
        var keys = Surfaces.Select(surface => surface.Key).ToHashSet(StringComparer.Ordinal);
        return Views
            .Where(view => view.Destination != Removed && !keys.Contains(view.Destination))
            .Select(view => $"{view.Where}: {view.Part} \"{view.Key}\" goes to \"{view.Destination}\", which {SurfacesFile} does not list.")
            .ToList();
    }

    public IReadOnlyList<string> SurfacesNeitherBuiltNorPlanned()
    {
        var findings = new List<string>();
        foreach (TargetSurfaceRow surface in Surfaces)
        {
            if (surface.State is not (Planned or Built))
            {
                findings.Add($"{surface.Where}: {surface.Key} has the state \"{surface.State}\", not {Planned} or {Built}.");
            }

            if (!IsBuildIssue(surface.BuiltBy))
            {
                findings.Add($"{surface.Where}: {surface.Key} is built by \"{surface.BuiltBy}\", not a build issue from #{FirstBuildIssue} to #{LastBuildIssue}.");
            }
        }

        return findings;
    }

    // A surface marked built must have a factory in the code catalog, a
    // surface with a factory must be marked built, and a factory's key must
    // be a row of the data.
    public IReadOnlyList<string> BuiltStatesThatDifferFrom(IEnumerable<string> factoryKeys)
    {
        var factories = factoryKeys.ToHashSet(StringComparer.Ordinal);
        var keys = Surfaces.Select(surface => surface.Key).ToHashSet(StringComparer.Ordinal);
        var findings = new List<string>();
        foreach (string factory in factories.Where(factory => !keys.Contains(factory)).Order(StringComparer.Ordinal))
        {
            findings.Add($"TargetSurfaceCatalog has a factory for \"{factory}\", which {SurfacesFile} does not list.");
        }

        foreach (TargetSurfaceRow surface in Surfaces)
        {
            if (surface.State == Built && !factories.Contains(surface.Key))
            {
                findings.Add($"{surface.Where}: {surface.Key} is marked {Built}, but TargetSurfaceCatalog has no factory for it.");
            }
            else if (surface.State != Built && factories.Contains(surface.Key))
            {
                findings.Add($"{surface.Where}: {surface.Key} has a factory in TargetSurfaceCatalog, but is marked \"{surface.State}\", not {Built}.");
            }
        }

        return findings;
    }

    public IReadOnlyList<string> SurfacesStillPlannedFor(string issue) =>
        Surfaces
            .Where(surface => surface.BuiltBy == issue && surface.State != Built)
            .Select(surface => $"{surface.Where}: {surface.Key} is built by {issue} and still marked \"{surface.State}\".")
            .ToList();

    // A surface no view goes to needs a note that says why.
    public IReadOnlyList<string> UnreachedSurfacesWithoutANote()
    {
        var reached = Views.Select(view => view.Destination).ToHashSet(StringComparer.Ordinal);
        return Surfaces
            .Where(surface => !reached.Contains(surface.Key) && surface.Note.Length == 0)
            .Select(surface => $"{surface.Where}: no view in {MapFile} goes to {surface.Key}, and its note does not say why.")
            .ToList();
    }

    // The map rows of both files together must cover inventory rows 1 to 97,
    // except the named rows, each with its reason; a named row that is
    // covered after all is stale.
    public IReadOnlyList<string> InventoryRowsNotCovered(IReadOnlyDictionary<int, string> namedRows)
    {
        var covered = Surfaces.SelectMany(surface => surface.MapRows).Concat(Views.SelectMany(view => view.MapRows)).ToHashSet();
        var findings = new List<string>();
        for (int row = 1; row <= InventoryRows; row++)
        {
            bool named = namedRows.TryGetValue(row, out string? reason);
            if (!covered.Contains(row) && !named)
            {
                findings.Add($"Inventory row {row} is in no target surface's or view's map rows and is not named as a row no target surface covers.");
            }
            else if (covered.Contains(row) && named)
            {
                findings.Add($"Inventory row {row} is named as a row no target surface covers, but the data covers it.");
            }
            else if (named && string.IsNullOrWhiteSpace(reason))
            {
                findings.Add($"Inventory row {row} is named as a row no target surface covers, with no reason.");
            }
        }

        foreach (int row in namedRows.Keys.Where(row => row is < 1 or > InventoryRows).Order())
        {
            findings.Add($"Inventory row {row} is named, but the inventory has rows 1 to {InventoryRows}.");
        }

        return findings;
    }

    // Each view whose destination is a surface not built yet, with that
    // surface's build issue. A removed view has no place to build and is not
    // listed.
    public IReadOnlyList<UnbuiltView> UnbuiltViews()
    {
        var surfaces = Surfaces.ToDictionary(surface => surface.Key, StringComparer.Ordinal);
        return Views
            .Where(view => view.Destination != Removed && surfaces.TryGetValue(view.Destination, out TargetSurfaceRow? surface) && surface.State != Built)
            .Select(view => new UnbuiltView(surfaces[view.Destination].BuiltBy, view.Part, view.Key, view.Destination))
            .ToList();
    }

    public IReadOnlyList<ParityRow> BuiltViews()
    {
        var built = Surfaces.Where(surface => surface.State == Built).Select(surface => surface.Key).ToHashSet(StringComparer.Ordinal);
        return Views.Where(view => built.Contains(view.Destination)).ToList();
    }

    // A build issue is "#NNN", the GitHub issue of one of the plan's builds:
    // build N (Avalonia-Migration-Plan.md section 8) is issue 415 + N.
    public static bool IsBuildIssue(string text) =>
        text.Length > 1
        && text[0] == '#'
        && text[1] != '0'
        && int.TryParse(text.AsSpan(1), NumberStyles.None, CultureInfo.InvariantCulture, out int number)
        && number is >= FirstBuildIssue and <= LastBuildIssue;

    // Orders build issues as the plan numbers its builds, which is by issue number.
    public static int BuildIssueOrder(string issue) =>
        int.Parse(issue.AsSpan(1), NumberStyles.None, CultureInfo.InvariantCulture);

    private static List<TableLine> ReadTable(string file, string text, string[] columns, List<string> problems)
    {
        var lines = new List<TableLine>();
        var firstLineOfKey = new Dictionary<string, int>(StringComparer.Ordinal);
        int keyColumn = Array.IndexOf(columns, "key");
        string[] raw = text.ReplaceLineEndings("\n").Split('\n');

        // A final line break leaves one empty string after it.
        int count = raw.Length > 0 && raw[^1].Length == 0 ? raw.Length - 1 : raw.Length;
        bool header = false;
        for (int index = 0; index < count; index++)
        {
            string where = $"{file}:{index + 1}";
            if (raw[index].StartsWith('#'))
            {
                continue;
            }

            string[] cells = raw[index].Split('\t');
            if (!header)
            {
                header = true;
                if (!cells.SequenceEqual(columns, StringComparer.Ordinal))
                {
                    problems.Add($"{where}: the header is not \"{string.Join("<tab>", columns)}\".");
                }

                continue;
            }

            if (cells.Length < columns.Length - 1 || cells.Length > columns.Length)
            {
                problems.Add($"{where}: {cells.Length} cells; a line has {columns.Length - 1} or {columns.Length} (the last, \"{columns[^1]}\", may be left out).");
                continue;
            }

            string[] full = [.. cells, .. Enumerable.Repeat(string.Empty, columns.Length - cells.Length)];
            int empty = Array.FindIndex(full, 0, columns.Length - 1, cell => cell.Trim().Length == 0);
            if (empty >= 0)
            {
                problems.Add($"{where}: the cell \"{columns[empty]}\" is empty.");
                continue;
            }

            int padded = Array.FindIndex(full, cell => cell != cell.Trim());
            if (padded >= 0)
            {
                problems.Add($"{where}: the cell \"{columns[padded]}\" starts or ends with a space.");
                continue;
            }

            if (firstLineOfKey.TryGetValue(full[keyColumn], out int first))
            {
                problems.Add($"{where}: the key \"{full[keyColumn]}\" is already on line {first}.");
                continue;
            }

            firstLineOfKey.Add(full[keyColumn], index + 1);
            lines.Add(new TableLine(where, full));
        }

        if (!header)
        {
            problems.Add($"{file}: no header line.");
        }

        return lines;
    }

    private static bool TryReadRows(TableLine line, int column, string name, List<string> problems, out IReadOnlyList<int> rows)
    {
        var read = new List<int>();
        foreach (string part in line.Cells[column].Split(','))
        {
            if (!int.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out int row) || row < 1 || row > InventoryRows)
            {
                problems.Add($"{line.Where}: the cell \"{name}\" is \"{line.Cells[column]}\", not inventory rows from 1 to {InventoryRows} separated by commas.");
                rows = [];
                return false;
            }

            read.Add(row);
        }

        rows = read;
        return true;
    }

    private sealed record TableLine(string Where, string[] Cells);
}

// One row of target-surfaces.tsv. Where is "file:line".
internal sealed record TargetSurfaceRow(string Key, IReadOnlyList<int> MapRows, string BuiltBy, string State, string ReadFrom, string Note, string Where);

// One row of parity-map.tsv. Where is "file:line".
internal sealed record ParityRow(string Part, string Key, string Destination, IReadOnlyList<int> MapRows, string ReadFrom, string Reason, string Where);

// A WinForms view whose destination is not built yet, and the issue that builds it.
internal sealed record UnbuiltView(string BuiltBy, string Part, string Key, string Destination);
