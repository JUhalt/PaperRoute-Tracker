Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.IO.Compression
Imports System.Linq
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
