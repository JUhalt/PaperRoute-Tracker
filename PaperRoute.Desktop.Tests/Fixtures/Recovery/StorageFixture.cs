using System.Security.Cryptography;
using System.Text;
using ManuscriptPipeline.Models;
using ManuscriptPipeline.Services;
using PaperRoute.ViewModels.Startup;

namespace PaperRoute.Desktop.Tests.Fixtures.Recovery;

// The storage-step fixtures (start-up design 5.3 part a, build 6 F1): the
// storage gates that only the storage step meets, each made in code in a new
// folder of its own in the temporary folder, with the four roots the core's
// resolvers would name (current-data, legacy-data, managed-library,
// legacy-managed-library). The step runs over them in-process, through the
// core's four-root EnsureCurrentStorage overload, and the recovery through a
// StartupRecoveryService over the fixture's own library: nothing here reads
// or writes a default root. Hashes lists every file with its SHA-256, for the
// lists before and after.
//
// newer-schema: a library whose storage marker says schema 10, from a newer
//   PaperRoute (today's rule, StartupRecoveryService.vb:75-86).
// legacy-folder: a legacy data folder beside an empty current one, with the
//   hold's lock file beside the current root (start-up design 2.3a), held
//   open as a live hold would be.
// old-schema-damaged-with-good-previous-save: storage marker 1, manuscripts.json
//   cut in half, manuscripts.bak whole and in the shape a schema-1 build wrote
//   (a JSON array of manuscripts with an Id and a Title).
internal sealed class StorageFixture : IDisposable
{
    public const string NewerSchema = "newer-schema";
    public const string LegacyFolder = "legacy-folder";
    public const string OldSchemaDamagedWithGoodPreviousSave = "old-schema-damaged-with-good-previous-save";

    // The previous save of the old-schema fixture, as a schema-1 build wrote it.
    private const string SchemaOneLibrary =
        "[\r\n" +
        "  {\r\n" +
        "    \"Id\": \"5b0d1f3e-2c4a-4f6e-9a1b-7c3d5e7f9a01\",\r\n" +
        "    \"Title\": \"Tidal Records of the Northern Shelf\"\r\n" +
        "  },\r\n" +
        "  {\r\n" +
        "    \"Id\": \"5b0d1f3e-2c4a-4f6e-9a1b-7c3d5e7f9a02\",\r\n" +
        "    \"Title\": \"A Second Look at Shelf Currents\"\r\n" +
        "  }\r\n" +
        "]\r\n";

    private static readonly string[] s_schemaOneTitles = ["Tidal Records of the Northern Shelf", "A Second Look at Shelf Currents"];
    private static readonly string[] s_savedTitles = ["Coastal Erosion in Winter", "Notes on Estuary Sediment"];

    private FileStream? _hold;

    private StorageFixture(string name)
    {
        Name = name;
        Root = Path.Combine(Path.GetTempPath(), "PaperRoute-StorageFixture-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);
    }

    public string Name { get; }

    public string Root { get; }

    public string CurrentDataRoot => Path.Combine(Root, "current-data");

    public string LegacyDataRoot => Path.Combine(Root, "legacy-data");

    public string ManagedLibraryRoot => Path.Combine(Root, "managed-library");

    public string LegacyManagedLibraryRoot => Path.Combine(Root, "legacy-managed-library");

    public string DataFolder => Path.Combine(CurrentDataRoot, "data");

    public string SchemaFilePath => Path.Combine(DataFolder, "schema.json");

    public string LibraryFile => Path.Combine(DataFolder, "manuscripts.json");

    public string PreviousSaveFile => Path.Combine(DataFolder, "manuscripts.bak");

    // Where the hold's lock file lies: beside the data root, never inside it
    // (start-up design 2.3a), so the legacy migration still finds the current
    // root empty.
    public string HoldFile => Path.Combine(Root, "current-data.instance.lock");

    // The titles of the library the fixture's step should end with.
    public IReadOnlyList<string> Titles { get; private set; } = [];

    // The bytes of manuscripts.json as damaged (the old-schema fixture only).
    public byte[] DamagedLibrary { get; private set; } = [];

    public static StorageFixture Make(string name)
    {
        var fixture = new StorageFixture(name);
        try
        {
            switch (name)
            {
                case NewerSchema:
                    fixture.SaveLibrary(fixture.DataFolder);
                    File.WriteAllText(fixture.SchemaFilePath, "{\"SchemaVersion\":10}");
                    break;

                case LegacyFolder:
                    fixture.SaveLibrary(Path.Combine(fixture.LegacyDataRoot, "data"));
                    Directory.CreateDirectory(fixture.CurrentDataRoot);
                    fixture._hold = new FileStream(fixture.HoldFile, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                    break;

                case OldSchemaDamagedWithGoodPreviousSave:
                    Directory.CreateDirectory(fixture.DataFolder);
                    byte[] whole = Encoding.UTF8.GetBytes(SchemaOneLibrary);
                    File.WriteAllBytes(fixture.PreviousSaveFile, whole);
                    fixture.DamagedLibrary = whole[..(whole.Length / 2)];
                    File.WriteAllBytes(fixture.LibraryFile, fixture.DamagedLibrary);
                    File.WriteAllText(fixture.SchemaFilePath, "{\"SchemaVersion\":1}");
                    fixture.Titles = s_schemaOneTitles;
                    break;

                default:
                    throw new ArgumentException($"There is no storage-step fixture named {name}.", nameof(name));
            }

            return fixture;
        }
        catch
        {
            fixture.Dispose();
            throw;
        }
    }

    // The hold's stand-in, open with no sharing while the fixture lives (legacy-folder only).
    public FileStream? Hold => _hold;

    // The storage step over the fixture's four roots; counted runs before each attempt.
    public StorageStep Step(Action? counted = null) => new(
        () =>
        {
            counted?.Invoke();
            StorageMigrationService.EnsureCurrentStorage(CurrentDataRoot, LegacyDataRoot, ManagedLibraryRoot, LegacyManagedLibraryRoot);
        },
        () => SchemaFilePath);

    // The failure window's recovery over the fixture's own library.
    public IStartupRecovery Recovery() => new StartupRecovery(
        new StartupRecoveryService(new ManuscriptRepository(DataFolder, ManagedLibraryRoot), ManagedLibraryRoot, SchemaFilePath));

    // The library in the current root, as the next load reads it.
    public ManuscriptRepository Repository() => new(DataFolder, ManagedLibraryRoot);

    // Every file under the folder (the fixture's root when none is named), by
    // its path from that folder, with its SHA-256.
    public SortedDictionary<string, string> Hashes(string? folder = null) => HashesOf(folder ?? Root);

    public static SortedDictionary<string, string> HashesOf(string folder)
    {
        var hashes = new SortedDictionary<string, string>(StringComparer.Ordinal);
        if (!Directory.Exists(folder))
        {
            return hashes;
        }

        foreach (string file in Directory.GetFiles(folder, "*", SearchOption.AllDirectories))
        {
            string name = Path.GetRelativePath(folder, file).Replace('\\', '/');
            using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            hashes[name] = Convert.ToHexString(SHA256.HashData(stream));
        }

        return hashes;
    }

    // The two lists differ in no file, added, removed or changed.
    public static void AssertSame(SortedDictionary<string, string> before, SortedDictionary<string, string> after, string message)
    {
        string[] changed = [.. before.Keys.Union(after.Keys, StringComparer.Ordinal)
            .Where(name => !before.TryGetValue(name, out string? was) || !after.TryGetValue(name, out string? now) || was != now)
            .Order(StringComparer.Ordinal)];
        Assert.IsEmpty(changed, $"{message} Files added, removed or changed: {string.Join(", ", changed)}");
    }

    public void Dispose()
    {
        _hold?.Dispose();
        _hold = null;
        try
        {
            Directory.Delete(Root, recursive: true);
        }
        catch (IOException)
        {
            // Left for the system's temporary-file clean-up.
        }
        catch (UnauthorizedAccessException)
        {
            // Left for the system's temporary-file clean-up.
        }
    }

    // A library of two manuscripts saved twice by today's repository, so its
    // previous save holds it as well.
    private void SaveLibrary(string dataFolder)
    {
        var repository = new ManuscriptRepository(dataFolder, ManagedLibraryRoot);
        List<Manuscript> library = [.. s_savedTitles.Select(title => new Manuscript { Title = title })];
        repository.Save(library);
        repository.Save(library);
        Titles = s_savedTitles;
    }
}
