Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.Linq
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
        repository.Save(New List(Of Manuscript) From {Sample("Second title")})

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

        For Each written As String In {
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
