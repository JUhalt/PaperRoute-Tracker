using System.Text.RegularExpressions;

namespace PaperRoute.Desktop.Tests.CoreSurface;

// Lifted bans (start-up design 2.8). BannedSymbols.txt forbids the core
// members that write (RS0030, an error in PaperRoute.ViewModels and in
// PaperRoute.Desktop, which share the list). A lifted ban is one of them that
// exactly one file may call, under "#pragma warning disable RS0030" with its
// reason in a comment on the line above. lifted-bans.txt names each with its
// owner file, its reason and its issue. This test reads both projects'
// sources as text (Source\ and ViewModelSources\, copied to the test output)
// and fails on a pragma in a file that owns no ban, on a banned name under a
// pragma whose file does not own that ban, on a line whose owner no longer
// calls its ban, and on a ban listed twice. A pragma's region is read by
// member name, not by type: the type each call reaches is pinned by
// allowed-core-references.txt (TrialReferencesOnlyTheListedCoreSurface).
// The allowed list's second category, a line that writes the person's
// storage, must name a lifted ban.
[TestClass]
public sealed partial class LiftedBanTests
{
    private const string ListName = "lifted-bans.txt";

    // The allowed list's second category (allowed-core-references.txt's header).
    private const string PersonsStorage = "WRITES THE PERSON'S STORAGE: ";

    // Each project's folder in the repository, and where its sources are copied.
    private static readonly (string Project, string Folder)[] s_trees =
    [
        ("PaperRoute.Desktop/", "Source"),
        ("PaperRoute.ViewModels/", "ViewModelSources"),
    ];

    [TestMethod]
    public void EachLiftedBanHasOneOwner()
    {
        var problems = new List<string>();
        Dictionary<string, string> banned = ReadBanned();
        IReadOnlyList<LiftedBan> lifted = ReadList(banned, problems);
        IReadOnlyList<Region> regions = ReadRegions();

        foreach (Region region in regions)
        {
            LiftedBan[] owned = [.. lifted.Where(ban => ban.Owner == region.File)];
            if (owned.Length == 0)
            {
                problems.Add($"{region.File}:{region.Line} lifts the ban (RS0030), but {ListName} gives that file no ban.");
                continue;
            }

            if (!region.HasReason)
            {
                problems.Add($"{region.File}:{region.Line} lifts the ban (RS0030) with no reason in a comment on the line above.");
            }

            foreach (IGrouping<string, string> name in banned.Keys.GroupBy(id => banned[id]))
            {
                if (Names(region.Text, name.Key) && !owned.Any(ban => banned[ban.Id] == name.Key))
                {
                    problems.Add($"{region.File}:{region.Line} names {name.Key} under its pragma ({string.Join(", ", name)}), " +
                        $"but {ListName} does not give that file the ban: a banned member has one owner.");
                }
            }
        }

        foreach (LiftedBan ban in lifted)
        {
            if (banned.TryGetValue(ban.Id, out string? name) &&
                !regions.Any(region => region.File == ban.Owner && Names(region.Text, name)))
            {
                problems.Add($"{ban.Id}: its owner {ban.Owner} no longer calls {name} under a pragma; remove the line so the list stays true.");
            }
        }

        string[] lifts = [.. lifted.Select(ban => ban.Id)];
        foreach ((string id, string reason) in ReadAllowed())
        {
            if (reason.StartsWith(PersonsStorage, StringComparison.Ordinal) && !lifts.Contains(id, StringComparer.Ordinal))
            {
                problems.Add($"allowed-core-references.txt: {id} writes the person's storage, but {ListName} names no owner for it.");
            }
        }

        Assert.IsEmpty(problems, string.Join(Environment.NewLine, problems));
    }

    // Every banned symbol's documentation ID, with the name a call writes:
    // the member's name, or the type's for a type or a constructor.
    private static Dictionary<string, string> ReadBanned()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "CoreSurface", "BannedSymbols.txt");
        Assert.IsTrue(File.Exists(path), $"{path} was not copied to the test output.");
        var banned = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string raw in File.ReadLines(path))
        {
            string text = raw.Trim();
            if (text.Length == 0 || text.StartsWith('#'))
            {
                continue;
            }

            int separator = text.IndexOf(';', StringComparison.Ordinal);
            string id = (separator < 0 ? text : text[..separator]).Trim();
            banned[id] = CalledName(id);
        }

        Assert.IsNotEmpty(banned, "BannedSymbols.txt names nothing: the reader is broken.");
        return banned;
    }

    private static string CalledName(string id)
    {
        string head = id[2..];
        int open = head.IndexOf('(', StringComparison.Ordinal);
        head = open < 0 ? head : head[..open];
        int tick = head.IndexOf('`', StringComparison.Ordinal);
        head = tick < 0 ? head : head[..tick];
        string[] parts = head.Split('.');
        return parts[^1] == "#ctor" ? parts[^2] : parts[^1];
    }

    private static IReadOnlyList<LiftedBan> ReadList(Dictionary<string, string> banned, List<string> problems)
    {
        string path = Path.Combine(AppContext.BaseDirectory, "CoreSurface", ListName);
        Assert.IsTrue(File.Exists(path), $"{path} was not copied to the test output.");

        var lines = new List<LiftedBan>();
        int number = 0;
        foreach (string raw in File.ReadLines(path))
        {
            number++;
            string text = raw.Trim();
            if (text.Length == 0 || text.StartsWith('#'))
            {
                continue;
            }

            string[] fields = text.Split(';');
            if (fields.Length != 3)
            {
                problems.Add($"{ListName} line {number}: give \"<documentation ID>;<owner file>;<reason> (#<issue>)\".");
                continue;
            }

            string id = fields[0].Trim();
            string owner = fields[1].Trim();
            string reason = fields[2].Trim();
            if (!banned.ContainsKey(id))
            {
                problems.Add($"{ListName} line {number}: {id} is not in BannedSymbols.txt, so there is no ban to lift.");
            }

            if (OwnerPath(owner) is not { } file || !File.Exists(file))
            {
                problems.Add($"{ListName} line {number}: the owner {owner} is not a .cs file of PaperRoute.Desktop or PaperRoute.ViewModels.");
            }

            if (!IssueAtTheEnd().IsMatch(reason))
            {
                problems.Add($"{ListName} line {number}: {id} needs a reason ending in its issue, \" (#<number>)\".");
            }

            if (lines.Any(line => line.Id == id))
            {
                problems.Add($"{ListName} line {number}: {id} is listed again: a lifted ban has one owner.");
            }
            else if (lines.Count > 0 && string.CompareOrdinal(lines[^1].Id, id) > 0)
            {
                problems.Add($"{ListName} line {number}: {id} is out of order (the list is sorted, ordinal).");
            }

            lines.Add(new LiftedBan(id, owner));
        }

        return lines;
    }

    // An owner's path in the repository to its copy in the test output.
    private static string? OwnerPath(string owner)
    {
        foreach ((string project, string folder) in s_trees)
        {
            if (owner.StartsWith(project, StringComparison.Ordinal) && owner.EndsWith(".cs", StringComparison.Ordinal))
            {
                return Path.Combine(AppContext.BaseDirectory, folder, owner[project.Length..]);
            }
        }

        return null;
    }

    // Every stretch of source from a pragma that lifts the ban to the pragma
    // that restores it (or the end of the file), comments left out.
    private static IReadOnlyList<Region> ReadRegions()
    {
        var regions = new List<Region>();
        foreach ((string project, string folder) in s_trees)
        {
            string root = Path.Combine(AppContext.BaseDirectory, folder);
            Assert.IsTrue(Directory.Exists(root), $"{root} was not copied to the test output.");
            foreach (string path in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
            {
                string file = project + Path.GetRelativePath(root, path).Replace('\\', '/');
                string[] lines = File.ReadAllLines(path);
                for (int i = 0; i < lines.Length; i++)
                {
                    if (!CoreSurfaceTests.IsSaveBanPragma(lines[i], "disable"))
                    {
                        continue;
                    }

                    var text = new List<string>();
                    int end = i + 1;
                    while (end < lines.Length && !CoreSurfaceTests.IsSaveBanPragma(lines[end], "restore"))
                    {
                        int comment = lines[end].IndexOf("//", StringComparison.Ordinal);
                        text.Add(comment < 0 ? lines[end] : lines[end][..comment]);
                        end++;
                    }

                    regions.Add(new Region(file, i + 1, string.Join('\n', text), CoreSurfaceTests.HasReasonAbove(lines, i)));
                    i = end;
                }
            }
        }

        return regions;
    }

    private static bool Names(string text, string name) =>
        Regex.IsMatch(text, @"\b" + Regex.Escape(name) + @"\b", RegexOptions.CultureInvariant);

    private static IEnumerable<(string Id, string Reason)> ReadAllowed()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "CoreSurface", "allowed-core-references.txt");
        Assert.IsTrue(File.Exists(path), $"{path} was not copied to the test output.");
        foreach (string raw in File.ReadLines(path))
        {
            string text = raw.Trim();
            int separator = text.IndexOf(';', StringComparison.Ordinal);
            if (text.Length > 0 && !text.StartsWith('#') && separator > 0)
            {
                yield return (text[..separator].Trim(), text[(separator + 1)..].Trim());
            }
        }
    }

    [GeneratedRegex(@"\S \(#[0-9]+\)$")]
    private static partial Regex IssueAtTheEnd();

    private sealed record LiftedBan(string Id, string Owner);

    private sealed record Region(string File, int Line, string Text, bool HasReason);
}
