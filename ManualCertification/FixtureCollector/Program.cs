using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

internal static class Program
{
    private const string EvidenceShare = @"\\VBOXSVR\PaperRouteEvidence";
    private const string LinkedFixture = @"\\VBOXSVR\PaperRouteInputs\external\linked-figure.txt";
    private const int JsonLimit = 8 * 1024 * 1024;
    private const long FileLimit = 128L * 1024 * 1024;
    // The csproj's Version, so the report cannot name another (1.1.0 reported "1.0.0").
    private static readonly string CollectorVersion = typeof(Program).Assembly.GetName().Version?.ToString(3) ?? "";
    // v1.2: the standing certification fixture's naming rule (#145).
    private const string CertificationPrefix = "ZZZ-CERT-";
    private static readonly JsonSerializerOptions OutputOptions = new() { WriteIndented = true };
    private static string Id(int n) => $"50000000-0000-0000-0000-{n:000000000000}";
    private static string Key(int n) => Id(n).Replace("-", "");
    private static readonly string[] ManagedRelative =
    {
        $"{Key(1)}/versions/{Key(4)}/manuscript-v1.txt",
        $"{Key(1)}/packets/{Key(9)}/{Key(10)}/cover-letter.txt",
        $"{Key(1)}/{Key(2)}/{Key(12)}/decision-letter.txt"
    };

    [STAThread]
    private static int Main()
    {
        var runId = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + "-" + Guid.NewGuid().ToString("N")[..8];
        Dictionary<string, object?> report;
        try { report = Collect(); }
        catch (Exception ex)
        {
            // Do not copy exception messages, paths, file contents, or stack traces from rejected data.
            report = new() { ["Status"] = "ReadFailed", ["Reason"] = ex is JsonException ? "InvalidJson" : ex.GetType().Name };
        }
        report["CollectorVersion"] = CollectorVersion;
        report["RecordedAtUtc"] = DateTime.UtcNow;
        report["RunId"] = runId;
        report["ReadOnlyCollection"] = true;
        report["Limitations"] = "File evidence only. No installer, UI, native-DPI, or updater behavior is certified by this report. Run with PaperRoute closed for a stable comparison.";
        try
        {
            // The only writes performed by this executable are reports on the explicit evidence share.
            var destination = Path.Combine(EvidenceShare, "fixture-evidence");
            Directory.CreateDirectory(destination);
            File.WriteAllText(Path.Combine(destination, $"fixture-evidence-{runId}.json"), JsonSerializer.Serialize(report, OutputOptions), new UTF8Encoding(false));
            return report["Status"] is "SyntheticFixture" or "CertificationFixture" or "EmptyLibrary" ? 0 : 2;
        }
        catch { return 3; } // No fallback writes to the guest library or host profile.
    }

    private static Dictionary<string, object?> Reject(string reason) => new() { ["Status"] = "RejectedOtherData", ["Reason"] = reason };

    private static Dictionary<string, object?> Collect()
    {
        var dataRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PaperRoute", "data");
        var managedRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "PaperRoute Library");
        var manuscriptsPath = Path.Combine(dataRoot, "manuscripts.json");
        if (!File.Exists(manuscriptsPath))
            return new() { ["Status"] = "EmptyLibrary", ["Reason"] = "NoManuscriptFile", ["ManuscriptCount"] = 0 };
        if (!OrdinaryPath(manuscriptsPath)) return Reject("ReparsePath");
        var manuscriptBytes = ReadBounded(manuscriptsPath);
        using var document = JsonDocument.Parse(manuscriptBytes);
        if (document.RootElement.ValueKind != JsonValueKind.Array) return Reject("UnexpectedLibraryShape");
        if (document.RootElement.GetArrayLength() == 0)
            return new() { ["Status"] = "EmptyLibrary", ["Reason"] = "EmptyManuscriptArray", ["ManuscriptCount"] = 0 };
        if (!IsFixture(document.RootElement)) return CollectCertificationFixture(document.RootElement, dataRoot, managedRoot, manuscriptsPath, manuscriptBytes);
        var manuscript = document.RootElement[0];
        var authorsPath = Path.Combine(dataRoot, "authors.json");
        if (!File.Exists(authorsPath) || !OrdinaryPath(authorsPath)) return Reject("SyntheticAuthorLibraryMissing");
        var authorBytes = ReadBounded(authorsPath);
        using var authors = JsonDocument.Parse(authorBytes);
        if (!IsFixtureAuthors(authors.RootElement)) return Reject("OtherReusableMetadata");
        var manifestPath = Path.Combine(AppContext.BaseDirectory, "synthetic-fixture-expected.json");
        using var manifest = JsonDocument.Parse(ReadBounded(manifestPath));
        if (GetText(manifest.RootElement.GetProperty("RecordIds"), "Manuscript") != Id(1))
            return Reject("UnexpectedExpectedManifest");

        var submission = manuscript.GetProperty("Submissions")[0];
        var packet = manuscript.GetProperty("SubmissionPackets")[0];
        var version = manuscript.GetProperty("Versions")[0];
        var managedPacket = packet.GetProperty("Files").EnumerateArray().Single(x => GetText(x, "Id") == Id(10));
        var correspondence = submission.GetProperty("Correspondence").EnumerateArray().Single(x => GetText(x, "Id") == Id(12));
        var references = new[] { GetText(version, "LocalFilePath"), GetText(managedPacket, "LocalFilePath"), GetText(correspondence, "LocalFilePath") };
        var managed = new List<object>();
        for (var index = 0; index < ManagedRelative.Length; index++)
        {
            var relative = ManagedRelative[index];
            var expectedPath = Path.GetFullPath(Path.Combine(managedRoot, relative));
            var pathMatches = string.Equals(Path.GetFullPath(references[index]), expectedPath, StringComparison.OrdinalIgnoreCase);
            var expected = manifest.RootElement.GetProperty("ManagedFiles").EnumerateArray().Single(x => GetText(x, "RelativeLibraryPath") == relative);
            managed.Add(new
            {
                RelativeLibraryPath = relative, RecordedPathMatchesExpected = pathMatches,
                Evidence = pathMatches ? HashEvidence(expectedPath, GetText(expected, "Sha256")) : new Dictionary<string, object?> { ["Status"] = "UnexpectedReferenceNotRead" }
            });
        }
        var linked = packet.GetProperty("Files").EnumerateArray().Single(x => GetText(x, "Id") == Id(11));
        var linkedMatches = string.Equals(GetText(linked, "LocalFilePath"), LinkedFixture, StringComparison.OrdinalIgnoreCase);
        var responses = submission.TryGetProperty("ReviewerResponses", out var responseArray) && responseArray.ValueKind == JsonValueKind.Array ? responseArray : default;
        var responseMetadata = responses.ValueKind == JsonValueKind.Array ? responses.EnumerateArray().Select(x => new
        {
            Id = GetText(x, "Id"), DecisionId = GetText(x, "DecisionId"),
            RevisionRoundNumber = x.TryGetProperty("RevisionRoundNumber", out var round) ? round.GetInt32() : 0,
            Status = x.TryGetProperty("Status", out var status) ? status.ToString() : "",
            ResponseRecordSha256 = Sha(Encoding.UTF8.GetBytes(x.GetRawText()))
        }).ToArray() : null;
        var backupSummaries = InspectBackups(dataRoot);
        var report = new Dictionary<string, object?>
        {
            ["Status"] = "SyntheticFixture", ["FixtureManuscriptId"] = Id(1), ["FixtureSubmissionId"] = Id(2),
            ["DataDirectory"] = dataRoot, ["ManagedLibraryDirectory"] = managedRoot,
            ["InstalledApplication"] = InstalledFiles(),
            ["Schema"] = SchemaEvidence(Path.Combine(dataRoot, "schema.json")),
            ["PreviousSchemaMarker"] = SchemaEvidence(Path.Combine(dataRoot, "schema.v5.bak")),
            // v1.1: every earlier marker a migration kept (schema.v5.bak, schema.v6.bak, ...).
            ["SchemaMarkers"] = SchemaMarkers(dataRoot),
            ["ManuscriptsJson"] = new { Sha256 = Sha(manuscriptBytes), SizeBytes = manuscriptBytes.Length },
            ["AuthorsJson"] = new { Sha256 = Sha(authorBytes), SizeBytes = authorBytes.Length },
            ["Counts"] = new
            {
                Manuscripts = 1, Submissions = 1, Decisions = Count(submission, "Decisions"), Correspondence = Count(submission, "Correspondence"),
                Versions = Count(manuscript, "Versions"), Packets = Count(manuscript, "SubmissionPackets"), ReadinessProfiles = Count(manuscript, "ReadinessProfiles"),
                ReviewerResponses = responseMetadata?.Length ?? 0
            },
            ["WorkflowAssociations"] = new
            {
                CurrentVersionId = GetText(manuscript, "CurrentVersionId"), PacketVersionId = GetText(packet, "ManuscriptVersionId"),
                PacketSubmissionId = GetText(packet, "SubmissionId"), PacketReadinessProfileId = GetText(packet, "ReadinessProfileId"),
                DecisionIds = submission.GetProperty("Decisions").EnumerateArray().Select(x => GetText(x, "Id")).ToArray(),
                ReadinessStatuses = manuscript.GetProperty("ReadinessProfiles")[0].GetProperty("Items").EnumerateArray().Select(x => x.GetProperty("Status").ToString()).ToArray()
            },
            ["ReviewerResponseMetadata"] = responseMetadata,
            ["ManagedFiles"] = managed,
            ["LinkedFile"] = new
            {
                RecordedPathMatchesExpected = linkedMatches,
                Evidence = linkedMatches ? HashEvidence(LinkedFixture, GetText(manifest.RootElement.GetProperty("LinkedFile"), "Sha256")) : new Dictionary<string, object?> { ["Status"] = "UnexpectedReferenceNotRead" }
            },
            ["Backups"] = backupSummaries
        };
        report["LibraryUnchangedDuringCollection"] = Sha(ReadBounded(manuscriptsPath)) == Sha(manuscriptBytes) && Sha(ReadBounded(authorsPath)) == Sha(authorBytes);
        return report;
    }

    // v1.2: the standing certification fixture (#145), a library whose every manuscript title starts
    // with ZZZ-CERT-. Each is named by id and title only. A library with any other manuscript is
    // rejected, as an unrelated library always was, with nothing read from it.
    private static Dictionary<string, object?> CollectCertificationFixture(JsonElement root, string dataRoot, string managedRoot, string manuscriptsPath, byte[] manuscriptBytes)
    {
        var named = CertificationManuscripts(root);
        if (named.Length == 0) return Reject("SyntheticIdentifiersNotPresent");
        if (named.Length != root.GetArrayLength()) return Reject("OtherManuscriptsPresent");
        var report = new Dictionary<string, object?>
        {
            ["Status"] = "CertificationFixture", ["CertificationManuscripts"] = named,
            ["DataDirectory"] = dataRoot, ["ManagedLibraryDirectory"] = managedRoot,
            ["InstalledApplication"] = InstalledFiles(),
            ["Schema"] = SchemaEvidence(Path.Combine(dataRoot, "schema.json")),
            ["SchemaMarkers"] = SchemaMarkers(dataRoot),
            ["ManuscriptsJson"] = new { Sha256 = Sha(manuscriptBytes), SizeBytes = manuscriptBytes.Length }
        };
        report["LibraryUnchangedDuringCollection"] = Sha(ReadBounded(manuscriptsPath)) == Sha(manuscriptBytes);
        return report;
    }

    // Only records whose title starts with ZZZ-CERT-, by id and title: nothing from any other record.
    internal static object[] CertificationManuscripts(JsonElement root) => root.ValueKind != JsonValueKind.Array ? Array.Empty<object>()
        : root.EnumerateArray().Where(x => GetText(x, "Title").StartsWith(CertificationPrefix, StringComparison.Ordinal))
            .Select(x => (object)new { Id = GetText(x, "Id"), Title = GetText(x, "Title") }).ToArray();

    private static Dictionary<string, object> SchemaMarkers(string dataRoot) =>
        Directory.GetFiles(dataRoot, "schema.v*.bak").OrderBy(file => file, StringComparer.OrdinalIgnoreCase).ToDictionary(file => Path.GetFileName(file), file => SchemaEvidence(file));

    private static bool IsFixture(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Array || root.GetArrayLength() != 1) return false;
        var manuscript = root[0];
        return GetText(manuscript, "Id") == Id(1) && OneWithId(manuscript, "Submissions", 2)
            && OneWithId(manuscript, "Versions", 4) && OneWithId(manuscript, "ReadinessProfiles", 5)
            && OneWithId(manuscript, "SubmissionPackets", 9)
            && HasId(manuscript.GetProperty("Submissions")[0], "Decisions", 3)
            && HasId(manuscript.GetProperty("Submissions")[0], "Correspondence", 12)
            && HasId(manuscript.GetProperty("SubmissionPackets")[0], "Files", 10)
            && HasId(manuscript.GetProperty("SubmissionPackets")[0], "Files", 11);
    }

    private static bool IsFixtureAuthors(JsonElement root) => root.ValueKind == JsonValueKind.Object
        && Count(root, "Authors") == 0 && Count(root, "Affiliations") == 0 && OneWithId(root, "Journals", 6);
    private static bool OneWithId(JsonElement parent, string name, int id) => Count(parent, name) == 1 && GetText(parent.GetProperty(name)[0], "Id") == Id(id);
    private static bool HasId(JsonElement parent, string name, int id) => parent.TryGetProperty(name, out var collection) && collection.ValueKind == JsonValueKind.Array && collection.EnumerateArray().Any(x => GetText(x, "Id") == Id(id));
    private static int Count(JsonElement parent, string name) => parent.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Array ? value.GetArrayLength() : 0;
    private static string GetText(JsonElement parent, string name) => parent.ValueKind == JsonValueKind.Object && parent.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : "";

    private static object InspectBackups(string dataRoot)
    {
        var allowed = new List<object>();
        var otherSkipped = 0;
        foreach (var name in new[] { "manuscripts.bak", "authors.bak" })
        {
            var path = Path.Combine(dataRoot, name);
            if (!File.Exists(path) || !OrdinaryPath(path)) continue;
            try
            {
                var bytes = ReadBounded(path);
                using var doc = JsonDocument.Parse(bytes);
                var safe = name.StartsWith("manuscripts") ? IsFixture(doc.RootElement) : IsFixtureAuthors(doc.RootElement);
                if (safe) allowed.Add(new { Name = name, Type = "AutomaticBackup", Sha256 = Sha(bytes), SizeBytes = bytes.Length });
                else otherSkipped++;
            }
            catch { otherSkipped++; }
        }
        var folder = Path.Combine(dataRoot, "backups");
        if (Directory.Exists(folder) && OrdinaryPath(folder))
        {
            foreach (var path in Directory.EnumerateFiles(folder, "*.zip", SearchOption.TopDirectoryOnly).Take(50))
            {
                try
                {
                    if (!OrdinaryPath(path) || new FileInfo(path).Length > FileLimit) { otherSkipped++; continue; }
                    using var zip = ZipFile.OpenRead(path);
                    var entries = zip.Entries.Where(e => e.FullName == "manuscripts.json").ToArray();
                    if (entries.Length != 1 || entries[0].Length > JsonLimit) { otherSkipped++; continue; }
                    using var stream = entries[0].Open();
                    using var json = JsonDocument.Parse(stream);
                    if (!IsFixture(json.RootElement)) { otherSkipped++; continue; }
                    allowed.Add(new { Name = Path.GetFileName(path), Type = "SyntheticPortableBackup", Evidence = HashEvidence(path, null) });
                }
                catch { otherSkipped++; }
            }
        }
        return new { SyntheticBackups = allowed, UnrecognizedOrEmptyBackupsSkipped = otherSkipped, Limit = "At most 50 top-level ZIP files; no extraction or contents copied." };
    }

    private static object InstalledFiles()
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return new[] { Path.Combine(local, "JUhalt.PaperRouteTracker"), @"C:\PaperRouteCertification\installed" }
            .Select(root => new
            {
                InstallRoot = root,
                Binaries = new[] { @"current\PaperRouteTracker.dll", @"current\PaperRouteTracker.exe", "Update.exe" }.Select(relative =>
                {
                    var path = Path.Combine(root, relative);
                    var exists = File.Exists(path) && OrdinaryPath(path);
                    var info = exists ? FileVersionInfo.GetVersionInfo(path) : null;
                    return new { RelativePath = relative, ProductVersion = info?.ProductVersion, FileVersion = info?.FileVersion, Evidence = HashEvidence(path, null) };
                }).ToArray()
            }).ToArray();
    }

    private static object SchemaEvidence(string path)
    {
        if (!File.Exists(path)) return new { Status = "Missing" };
        if (!OrdinaryPath(path)) return new { Status = "ReparsePathNotRead" };
        var bytes = ReadBounded(path);
        using var document = JsonDocument.Parse(bytes);
        var version = document.RootElement.TryGetProperty("SchemaVersion", out var value) ? value.GetInt32() : -1;
        return new { Status = "Read", SchemaVersion = version, Sha256 = Sha(bytes), SizeBytes = bytes.Length };
    }

    private static Dictionary<string, object?> HashEvidence(string path, string? expected)
    {
        try
        {
            if (!File.Exists(path)) return new() { ["Status"] = "Missing" };
            if (!OrdinaryPath(path)) return new() { ["Status"] = "ReparsePathNotRead" };
            var info = new FileInfo(path);
            if (info.Length > FileLimit) return new() { ["Status"] = "OverSizeLimit" };
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            var hash = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
            return new() { ["Status"] = "Read", ["SizeBytes"] = stream.Length, ["Sha256"] = hash, ["LastWriteTimeUtc"] = info.LastWriteTimeUtc, ["MatchesExpectedFixtureHash"] = expected == null ? null : hash == expected };
        }
        catch (Exception ex) { return new() { ["Status"] = "Unavailable", ["Reason"] = ex.GetType().Name }; }
    }

    private static byte[] ReadBounded(string path)
    {
        if (new FileInfo(path).Length > JsonLimit) throw new InvalidDataException("Size limit");
        return File.ReadAllBytes(path);
    }
    private static string Sha(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    private static bool OrdinaryPath(string path)
    {
        for (var candidate = Path.GetFullPath(path); !string.IsNullOrEmpty(candidate); candidate = Path.GetDirectoryName(candidate))
        {
            if ((File.Exists(candidate) || Directory.Exists(candidate)) && (File.GetAttributes(candidate) & FileAttributes.ReparsePoint) != 0) return false;
        }
        return true;
    }
}
