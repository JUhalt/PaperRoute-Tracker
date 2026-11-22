Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.IO.Compression
Imports System.Linq
Imports System.Security.Cryptography
Imports System.Text
Imports System.Text.Json
Imports System.Text.RegularExpressions
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

' The bytes PaperRoute stores (design risk R2). Every data file it writes
' today ends each line with CR LF, as Windows does; core stage 2 (v1.1)
' must keep that on every system, so a Windows user's files do not change.
<TestClass>
Public Class StoredBytesTests

    Private _root As String = String.Empty


    <TestInitialize>
    Public Sub Initialize()
        _root = CreateTemporaryRoot()
    End Sub


    <TestCleanup>
    Public Sub Cleanup()
        ManuscriptPipeline.PlatformServices.ResetForTests(CoreTestPlatform.Secrets, CoreTestPlatform.Launcher)
        DeleteTemporaryRoot(_root)
    End Sub


    <TestMethod>
    Public Sub EveryDataFileEndsEachLineWithCrLf()

        Dim currentData As String = Path.Combine(_root, "current-data")
        Dim dataDirectory As String = Path.Combine(currentData, "data")
        Dim managedRoot As String = Path.Combine(_root, "managed")
        Directory.CreateDirectory(managedRoot)

        StorageMigrationService.EnsureCurrentStorage(
            currentData, Path.Combine(_root, "legacy-data"), managedRoot, Path.Combine(_root, "legacy-managed"))

        ' Saved twice, so the first save becomes manuscripts.bak.
        Dim repository As New ManuscriptRepository(dataDirectory, managedRoot)
        repository.Save(New List(Of Manuscript) From {Sample("First title")})
        Dim saved As New List(Of Manuscript) From {Sample("Second title")}
        repository.Save(saved)

        Dim library As New AuthorLibraryData()
        library.Authors.Add(New AuthorRecord With {.GivenName = "Ada", .FamilyName = "Example"})
        Call New AuthorLibraryRepository(dataDirectory).Save(library)

        Call New CitationStore(dataDirectory).Save(
            New CitationSnapshot With {
                .Orcid = "0000-0002-1825-0097",
                .Source = "Example",
                .Works = New List(Of CitedWork) From {
                    New CitedWork With {.OpenAlexId = "W1", .Doi = "10.5555/stored-bytes", .CitedByCount = 3, .Title = "A work"}
                }
            })

        Call New AppSettingsService(currentData).Save(New AppSettings())

        Call New RecoveryDraftStore(dataDirectory).Write(
            New RecoveryDraft With {
                .ManuscriptId = Guid.NewGuid(),
                .WrittenAtUtc = DateTime.UtcNow,
                .Reason = "test",
                .Manuscript = Sample("Unsaved title")
            })

        ' A portable backup holds two files of its own: backup.json and
        ' backup-info.txt.
        Dim backupPath As String = Path.Combine(_root, "backup.zip")
        Call New PortableBackupService(managedRoot).CreateBackup(backupPath, saved, repository)
        Dim extracted As String = Path.Combine(_root, "backup-entries")
        Directory.CreateDirectory(extracted)
        Using archive As ZipArchive = ZipFile.OpenRead(backupPath)
            For Each name As String In {BackupManifest.FileName, "backup-info.txt"}
                archive.Entries.Single(Function(entry) entry.FullName = name).ExtractToFile(Path.Combine(extracted, name))
            Next
        End Using

        For Each written As String In {
            Path.Combine(extracted, BackupManifest.FileName),
            Path.Combine(extracted, "backup-info.txt"),
            Path.Combine(dataDirectory, "manuscripts.json"),
            Path.Combine(dataDirectory, "manuscripts.bak"),
            Path.Combine(dataDirectory, "authors.json"),
            Path.Combine(dataDirectory, "citations.json"),
            Path.Combine(currentData, "settings.json"),
            StorageMigrationService.SchemaFilePath(currentData),
            Path.Combine(dataDirectory, "recovery", RecoveryDraftStore.FileName)
        }
            AssertEveryLineEndsWithCrLf(written)
        Next

    End Sub


    ' The writers set StoredText.NewLine (core stage 2, step 3): on Windows
    ' it is the line break they wrote before, so no stored byte changes.
    <TestMethod>
    Public Sub StoredLineBreakIsWhatWindowsWritesToday()

        Assert.AreEqual(vbCrLf, ManuscriptPipeline.StoredText.NewLine)

        If OperatingSystem.IsWindows() Then
            Assert.AreEqual(Environment.NewLine, ManuscriptPipeline.StoredText.NewLine)
        End If

    End Sub


    ' On Windows the serializer's default line break is already CR LF, so no
    ' byte test there can tell a writer that sets StoredText.NewLine from one
    ' that does not. This reads the code instead, on every system: each
    ' indented JSON options initialiser in PaperRoute.Core sets it.
    <TestMethod>
    Public Sub EveryIndentedJsonWriterSetsTheStoredLineBreak()

        Dim core As String = Path.Combine(TestRepository.Root(), "PaperRoute.Core")
        Dim writers As Integer = 0
        Dim missing As New List(Of String)()

        For Each source As String In Directory.GetFiles(core, "*.vb", SearchOption.AllDirectories)

            If source.Contains(Path.DirectorySeparatorChar & "obj" & Path.DirectorySeparatorChar) OrElse
               source.Contains(Path.DirectorySeparatorChar & "bin" & Path.DirectorySeparatorChar) Then Continue For

            ' Comments dropped and white space removed, so an initialiser
            ' reads the same however its lines are laid out.
            Dim code As String = Regex.Replace(
                String.Join(vbLf, File.ReadAllLines(source).Where(Function(line) Not line.TrimStart().StartsWith("'"c))),
                "\s+", "")

            Dim indented As Integer = 0
            For Each initialiser As Match In Regex.Matches(code, "NewJsonSerializerOptionsWith\{[^}]*\}")

                If Not initialiser.Value.Contains(".WriteIndented=True") Then Continue For
                indented += 1

                ' The one indented initialiser that only reads: its options
                ' never write a file.
                If code.Substring(0, initialiser.Index).EndsWith("PrivateSharedFunctionCreateJsonOptions()AsJsonSerializerOptionsDimoptionsAs") AndAlso
                   Path.GetFileName(source) = "Schema5MigrationService.vb" Then Continue For

                writers += 1
                If Not initialiser.Value.Contains(".NewLine=StoredText.NewLine") Then
                    missing.Add(Path.GetFileName(source) & ": " & initialiser.Value)
                End If

            Next

            ' Indentation switched on anywhere else would escape the check.
            Assert.AreEqual(
                indented,
                Regex.Matches(code, "WriteIndented=True").Count,
                Path.GetFileName(source) & " sets WriteIndented outside a New JsonSerializerOptions With {...} initialiser.")

        Next

        Assert.AreEqual(0, missing.Count, "Without .NewLine = StoredText.NewLine: " & String.Join(" | ", missing))
        Assert.IsTrue(writers >= 14, "Found " & writers.ToString() & " indented JSON writers; PaperRoute.Core has 14.")

    End Sub


    ' The stored text built line by line (design 5.1 row 2) uses
    ' StoredText.NewLine, not the system's line break.
    <TestMethod>
    Public Sub StoredTextIsNotBuiltWithTheSystemLineBreak()

        Dim services As String = Path.Combine(TestRepository.Root(), "PaperRoute.Core", "Services")

        For Each name As String In {"TitlePageParserService.vb", "FlexibleExcelImporter.vb", "PortableBackupService.vb"}
            Dim code As String = String.Join(vbLf, File.ReadAllLines(Path.Combine(services, name)).Where(Function(line) Not line.TrimStart().StartsWith("'"c)))
            Assert.IsFalse(code.Contains("Environment.NewLine"), name & " builds stored text with Environment.NewLine.")
        Next

    End Sub


    ' RIS detection (design 5.1 row 3) finds a record after a CR LF on every
    ' system, as Windows does today.
    <TestMethod>
    Public Sub RisAfterAPreambleIsRecognisedAfterCrLf()

        Dim sample As String = "Exported from a reference manager" & vbCrLf & "TY  - JOUR" & vbCrLf & "ER  - " & vbCrLf

        Assert.AreEqual(BibliographyFormat.Ris, BibliographyExchangeService.DetectFormat("references.txt", sample))

    End Sub


    ' Design risk R2 (b): the standing schema-4 library, loaded and saved by
    ' today's core, gives the very bytes PaperRoute 1.0.1 wrote. Every file
    ' under the temporary root is hashed: the data files the migration and
    ' the two saves write, and the managed and linked files, which the save's
    ' sweep of the managed root must leave alone.
    '
    ' The values were recorded from PaperRoute 1.0.1 (tag v1.0.1, 6af1399)
    ' on 2026-11-19, on Windows (zone UTC-5), by a throwaway test in that
    ' tag's PaperRoute.Tests (its services are in the app assembly there):
    ' the same seed, migration, loads and saves as below, then the hashes
    ' taken as below. Two runs gave the same values. Only two things in the
    ' raw bytes differed between those runs, and both are handled here:
    ' - manuscripts.json and manuscripts.bak hold the four seeded paths,
    '   which name the random temporary folder (and use "/" on Linux): each
    '   path, as the serializer writes it, is put back to its placeholder
    '   from the fixture before hashing.
    ' - schema.json and schema.v5.bak to schema.v8.bak hold an UpdatedAtUtc
    '   stamp, the clock at the migration step that wrote each: 1.0.1 read
    '   DateTime.UtcNow, so each stamp it wrote during the run was replaced
    '   by Stamp before hashing. Today's core reads CoreClock, so the test
    '   clock below writes Stamp itself and these files are compared as
    '   written.
    ' - The three fixture files are LF in git and were CRLF in that Windows
    '   checkout (core.autocrlf), and the save copies them to the .bak files
    '   as they are: the seed below gives them CRLF line ends, so the same
    '   bytes are seeded on every system and from any checkout.
    ' No stored value depends on the time zone: the fixture's dates carry
    ' no offset or end in Z, and are written back as read (CalendarDateJson).
    ' To record again: run that throwaway test at tag v1.0.1, or read the
    ' values this test prints when it fails, after checking every changed
    ' byte against 1.0.1 by hand.
    <TestMethod>
    <DoNotParallelize>
    Public Sub TheSchema4LibrarySavedGivesTheBytesPaperRoute101Wrote()

        Const Stamp As String = "2026-11-19T12:00:00.0000000Z"
        ManuscriptPipeline.PlatformServices.Clock = New FixedTimeProvider(New DateTimeOffset(2026, 11, 19, 12, 0, 0, TimeSpan.Zero))

        Dim expected As New Dictionary(Of String, String)(StringComparer.Ordinal) From {
            {"current-data/data/authors.bak", "FEF7C7FFFA6A9A77AC7ACBF4BA57CDAD1869B2DFEBDCF207F3CCD73F1246EB11"},
            {"current-data/data/authors.json", "EF0F57094C5FA6C98B47CFF2692AEE525F99391AE6EDDAA91457CBBC86087D79"},
            {"current-data/data/manuscripts.bak", "A91C03AAF90378C717EC0C5861015F44D9B83C52B654A626B0BF074A3EE68DDA"},
            {"current-data/data/manuscripts.json", "C9551084CD837B802C6488C83F4F21B0B3F453E9675BEBFFA8542A2EBC8E9DD8"},
            {"current-data/data/schema.json", "14B8FA38A8F3DE03964F87E18E6BDFADDBCD49846C523016E990A3750B78940E"},
            {"current-data/data/schema.v4.bak", "8CC85B66A1857DA5F7CEC13CFF2A304ED6BCE9C5BB9A7506550F1E53AEFBDD1F"},
            {"current-data/data/schema.v5.bak", "4112285E8989F608A6BC5C12213C0E2571CC8EEBC74EFDC9E310CDC97E76A9C1"},
            {"current-data/data/schema.v6.bak", "CBCFE5BCB634969705CB798BE7796AEFA8A89EE9A2BD72F33A16DB3B8D97C7B7"},
            {"current-data/data/schema.v7.bak", "FEB5AF58EB688DD0B37C085427C0B71EC78DF58D0FD307C93EDB2E17494D3594"},
            {"current-data/data/schema.v8.bak", "660A42C66CD12A7DE1F07810B3E576370B0C6BCE03D281C8DF149810119FFE88"},
            {"current-managed/11111111111111111111111111111111/eeeeeeeeeeeeeeeeeeeeeeeeeeeeeee1/ffffffffffffffffffffffffffffff01/decision.txt", "B722F72456CFE11F2BF802875B382EF3D6EBA15AB1E325BBC504B864863CF373"},
            {"current-managed/11111111111111111111111111111111/versions/11111111111111111111111111111101/manuscript.txt", "B8F62FB1B63CFD13846241183B15D91DC409E3D8671816DCAD91698893C05D3F"},
            {"external/working.txt", "F757C5B5AA4AA53F019CD38D9E3B76FFAAA4B3ADD6AFE482C2EFAA5CA4B0E61E"}
        }

        ' The seed of Schema4CertificationTests.
        Dim currentData As String = Path.Combine(_root, "current-data")
        Dim dataDirectory As String = Path.Combine(currentData, "data")
        Dim managedRoot As String = Path.Combine(_root, "current-managed")
        Directory.CreateDirectory(dataDirectory)
        Directory.CreateDirectory(managedRoot)

        Dim fixtures As String = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Schema4Representative")
        Dim manuscriptsJson As String = File.ReadAllText(Path.Combine(fixtures, "manuscripts.json")).Replace(vbCrLf, vbLf).Replace(vbLf, vbCrLf)
        Dim manuscriptId As String = Guid.Parse("11111111-1111-1111-1111-111111111111").ToString("N")
        Dim paths As New Dictionary(Of String, String) From {
            {"__MANAGED_VERSION__", Path.Combine(managedRoot, manuscriptId, "versions", Guid.Parse("11111111-1111-1111-1111-111111111101").ToString("N"), "manuscript.txt")},
            {"__MANAGED_CORRESPONDENCE__", Path.Combine(managedRoot, manuscriptId, Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeee1").ToString("N"), Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffff01").ToString("N"), "decision.txt")},
            {"__EXTERNAL_VERSION__", Path.Combine(_root, "external", "working.txt")},
            {"__MISSING_EXTERNAL__", Path.Combine(_root, "external", "missing.txt")}
        }
        WriteFile(paths("__MANAGED_VERSION__"), {0, 17, 128, 255, 13, 10})
        WriteFile(paths("__MANAGED_CORRESPONDENCE__"), {65, 66, 67, 13, 10, 128})
        WriteFile(paths("__EXTERNAL_VERSION__"), {84, 101, 120, 116, 13, 10, 255})
        For Each pair As KeyValuePair(Of String, String) In paths
            manuscriptsJson = manuscriptsJson.Replace("""" & pair.Key & """", JsonSerializer.Serialize(pair.Value))
        Next
        File.WriteAllText(Path.Combine(dataDirectory, "manuscripts.json"), manuscriptsJson)
        CopyWithCrLf(Path.Combine(fixtures, "authors.json"), Path.Combine(dataDirectory, "authors.json"))
        CopyWithCrLf(Path.Combine(fixtures, "schema.json"), StorageMigrationService.SchemaFilePath(currentData))

        ' Opened as PaperRoute opens a library, then saved.
        StorageMigrationService.EnsureCurrentStorage(
            currentData, Path.Combine(_root, "legacy-data"), managedRoot, Path.Combine(_root, "legacy-managed"))
        Dim repository As New ManuscriptRepository(dataDirectory, managedRoot)
        Dim authors As New AuthorLibraryRepository(dataDirectory)
        Dim manuscripts As List(Of Manuscript) = repository.Load()
        Dim library As AuthorLibraryData = authors.Load()
        repository.Save(manuscripts)
        authors.Save(library)

        Assert.AreEqual(
            """UpdatedAtUtc"": """ & Stamp & """",
            File.ReadAllLines(StorageMigrationService.SchemaFilePath(currentData))(2).Trim(),
            "The migration did not read the test clock.")

        Dim actual As New Dictionary(Of String, String)(StringComparer.Ordinal)
        For Each filePath As String In Directory.GetFiles(_root, "*", SearchOption.AllDirectories)
            Dim bytes As Byte() = File.ReadAllBytes(filePath)
            For Each pair As KeyValuePair(Of String, String) In paths
                bytes = ReplaceBytes(bytes, Encoding.UTF8.GetBytes(JsonSerializer.Serialize(pair.Value)), Encoding.UTF8.GetBytes("""" & pair.Key & """"))
            Next
            Assert.IsFalse(
                Encoding.UTF8.GetString(bytes).Contains(JsonSerializer.Serialize(_root).Trim(""""c)),
                Path.GetFileName(filePath) & " holds the temporary folder in a form this test does not put back.")
            actual.Add(Path.GetRelativePath(_root, filePath).Replace(Path.DirectorySeparatorChar, "/"c), Convert.ToHexString(SHA256.HashData(bytes)))
        Next

        Dim differences As New List(Of String)()
        For Each name As String In expected.Keys.Union(actual.Keys).OrderBy(Function(item) item, StringComparer.Ordinal)
            Dim recorded As String = Nothing
            Dim written As String = Nothing
            expected.TryGetValue(name, recorded)
            actual.TryGetValue(name, written)
            If recorded <> written Then differences.Add(name & ": 1.0.1 " & If(recorded, "(no file)") & ", now " & If(written, "(no file)"))
        Next

        Assert.AreEqual(
            0, differences.Count,
            "Not the bytes PaperRoute 1.0.1 wrote: " & String.Join(" | ", differences) &
            ". Today's values: " & String.Join(" | ", actual.OrderBy(Function(pair) pair.Key, StringComparer.Ordinal).Select(Function(pair) pair.Key & " " & pair.Value)))

    End Sub


    ' Every occurrence of `find` in `data` replaced by `replacement`.
    Private Shared Function ReplaceBytes(data As Byte(), find As Byte(), replacement As Byte()) As Byte()

        Dim result As New List(Of Byte)(data.Length)
        Dim index As Integer = 0
        While index < data.Length
            Dim found As Boolean = index <= data.Length - find.Length
            For offset As Integer = 0 To find.Length - 1
                If Not found Then Exit For
                found = data(index + offset) = find(offset)
            Next
            If found Then
                result.AddRange(replacement)
                index += find.Length
            Else
                result.Add(data(index))
                index += 1
            End If
        End While
        Return result.ToArray()

    End Function


    ' A byte copy with every line ending in CR LF, whatever the checkout gave.
    Private Shared Sub CopyWithCrLf(source As String, destination As String)
        Dim lf As Byte() = {10}
        Dim crLf As Byte() = {13, 10}
        WriteFile(destination, ReplaceBytes(ReplaceBytes(File.ReadAllBytes(source), crLf, lf), lf, crLf))
    End Sub


    Private Shared Sub WriteFile(filePath As String, bytes As Byte())
        Directory.CreateDirectory(Path.GetDirectoryName(filePath))
        File.WriteAllBytes(filePath, bytes)
    End Sub


    Private Shared Sub AssertEveryLineEndsWithCrLf(filePath As String)

        Assert.IsTrue(File.Exists(filePath), "PaperRoute wrote " & filePath & ".")
        Dim bytes As Byte() = File.ReadAllBytes(filePath)

        Dim lineEnds As Integer = 0
        For index As Integer = 0 To bytes.Length - 1
            If bytes(index) = 10 Then
                Assert.IsTrue(
                    index > 0 AndAlso bytes(index - 1) = 13,
                    Path.GetFileName(filePath) & " has a line feed with no carriage return before it, at byte " & index.ToString() & ".")
                lineEnds += 1
            End If
        Next

        Assert.IsTrue(lineEnds > 0, Path.GetFileName(filePath) & " has no line end; it is written indented, so it has several.")

    End Sub


    Private Shared Function Sample(title As String) As Manuscript
        Return New Manuscript With {
            .Title = title,
            .Location = ManuscriptLocation.Pipeline,
            .CurrentStage = PaperStage.Draft,
            .TargetJournal = "A fictional journal"
        }
    End Function

End Class
