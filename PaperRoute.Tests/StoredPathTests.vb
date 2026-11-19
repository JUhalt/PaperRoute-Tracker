Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.Linq
Imports System.Text.Json
Imports System.Text.Json.Nodes
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

' A stored path's file name (core stage 2, step 4; design risk R13): on
' Windows restore reads it with Path.GetFileName, exactly as before, and a
' backup whose stored paths were written on Linux restores here.
<TestClass>
Public Class StoredPathTests

    Private _root As String = String.Empty

    <TestInitialize>
    Public Sub Initialize()
        _root = CreateTemporaryRoot()
    End Sub

    <TestCleanup>
    Public Sub Cleanup()
        DeleteTemporaryRoot(_root)
    End Sub


    Private Shared ReadOnly LongName As String = New String("n"c, 220) & "_ab12cd34.docx"

    ' Stored paths restore can meet, from Windows and from Linux, and the
    ' odd shapes Path.GetFileName has its own answer for.
    Private Shared Function StoredPaths() As String()
        Return {
            Nothing,
            "",
            "letter.docx",
            "C:\Users\a\PaperRoute Library\0f\versions\1e\letter_ab12cd34.docx",
            "C:\Users\a\PaperRoute Library\0f\versions\1e\draft._ab12cd34.docx",
            "C:\Users\a\PaperRoute Library\0f\versions\1e\draft _ab12cd34.docx",
            "C:\Users\a\PaperRoute Library\0f\versions\1e\NUL_ab12cd34.txt",
            "C:\Users\a\PaperRoute Library\0f\versions\1e\CON.v2_ab12cd34.docx",
            "C:\Users\a\PaperRoute Library\0f\versions\1e\" & LongName,
            "\\server\share\PaperRoute Library\0f\packets\2d\3c\Résumé_ab12cd34.pdf",
            "\\server\share",
            "C:\Users\a/PaperRoute Library/0f\versions/1e\mixed_ab12cd34.docx",
            "/home/a/PaperRoute Library/0f/versions/1e/letter_ab12cd34.docx",
            "/home/a/PaperRoute Library/0f/versions/1e/a b.c d_ab12cd34.tar.gz",
            "C:\",
            "C:draft.docx",
            "C:\folder\",
            "C:\folder\name with spaces .docx"
        }
    End Function


    ' The old expression and the new one, side by side.
    <TestMethod>
    Public Sub OnWindowsTheFileNameIsPathGetFileName()

        If Not OperatingSystem.IsWindows() Then Return

        For Each stored As String In StoredPaths()
            Assert.AreEqual(Path.GetFileName(stored), StoredPath.FileName(stored), "Stored path: " & If(stored, "(nothing)"))
        Next

    End Sub


    ' The rule used off Windows cuts after the last "\" or "/", so a path
    ' written on either system gives its own file name.
    <TestMethod>
    Public Sub OffWindowsTheNameFollowsTheLastSeparatorOfEitherKind()

        Assert.IsNull(StoredPath.LastSegment(Nothing))
        Assert.AreEqual("", StoredPath.LastSegment(""))
        Assert.AreEqual("letter.docx", StoredPath.LastSegment("letter.docx"))
        Assert.AreEqual("letter_ab12cd34.docx", StoredPath.LastSegment("C:\Users\a\PaperRoute Library\0f\versions\1e\letter_ab12cd34.docx"))
        Assert.AreEqual("letter_ab12cd34.docx", StoredPath.LastSegment("/home/a/PaperRoute Library/0f/versions/1e/letter_ab12cd34.docx"))
        Assert.AreEqual("mixed_ab12cd34.docx", StoredPath.LastSegment("C:\Users\a/PaperRoute Library/0f\versions/1e\mixed_ab12cd34.docx"))
        Assert.AreEqual("Résumé_ab12cd34.pdf", StoredPath.LastSegment("\\server\share\PaperRoute Library\0f\packets\2d\3c\Résumé_ab12cd34.pdf"))
        Assert.AreEqual("", StoredPath.LastSegment("C:\folder\"))

        ' For every stored path that names a file under a folder, both
        ' rules agree on Windows. (They differ only for a bare share,
        ' "\\server\share", and a drive-relative "C:draft.docx", which
        ' PaperRoute never stores for a managed file.)
        If OperatingSystem.IsWindows() Then
            Dim underAFolder As IEnumerable(Of String) = StoredPaths().Where(
                Function(item) item IsNot Nothing AndAlso
                    item.IndexOfAny({"\"c, "/"c}) >= 0 AndAlso
                    Not item.EndsWith("\", StringComparison.Ordinal) AndAlso
                    item <> "\\server\share")
            Assert.AreEqual(11, underAFolder.Count())
            For Each stored As String In underAFolder
                Assert.AreEqual(Path.GetFileName(stored), StoredPath.LastSegment(stored), stored)
            Next
        End If

    End Sub


    ' A small backup whose managed version, packet file and correspondence
    ' were stored under a Linux managed root restores on this system: the
    ' extracted files are found and the paths rewritten under the target's
    ' managed root.
    <TestMethod>
    Public Sub ABackupWithLinuxStoredPathsRestores()

        Dim sourceData As String = Path.Combine(_root, "source-data")
        Dim sourceManaged As String = Path.Combine(_root, "source-library")
        Dim linuxRoot As String = "/home/researcher/Documents/PaperRoute Library"

        Dim manuscripts As List(Of Manuscript) = CreateRepresentativeLibrary()
        Dim manuscript As Manuscript = manuscripts(0)
        Dim submission As JournalSubmission = manuscript.Submissions(0)
        Dim mid As String = manuscript.Id.ToString("N")

        Dim version As New ManuscriptVersion With {.Label = "Linux draft", .IsManagedCopy = True, .CreatedDate = New DateTime(2026, 7, 1)}
        manuscript.Versions.Add(version)
        Dim versionName As String = "draft._ab12cd34.docx"
        Dim linuxPaths As New Dictionary(Of String, String)()
        version.LocalFilePath = WriteManaged(sourceManaged, versionName, "version", mid, "versions", version.Id.ToString("N"))
        linuxPaths(version.LocalFilePath) = linuxRoot & "/" & mid & "/versions/" & version.Id.ToString("N") & "/" & versionName

        Dim packet As New SubmissionPacket With {.Label = "Linux packet", .ManuscriptVersionId = version.Id, .JournalName = "Journal of Example Studies"}
        manuscript.SubmissionPackets.Add(packet)
        Dim packetFile As New SubmissionPacketFile With {
            .Role = SubmissionPacketFileRole.CoverLetter, .Label = "Cover letter", .OriginalFileName = "cover letter.txt",
            .StorageMode = SubmissionPacketFileStorageMode.ManagedCopy
        }
        packet.Files.Add(packetFile)
        Dim packetName As String = "cover letter_ab12cd34.txt"
        packetFile.LocalFilePath = WriteManaged(sourceManaged, packetName, "packet file", mid, "packets", packet.Id.ToString("N"), packetFile.Id.ToString("N"))
        linuxPaths(packetFile.LocalFilePath) = linuxRoot & "/" & mid & "/packets/" & packet.Id.ToString("N") & "/" & packetFile.Id.ToString("N") & "/" & packetName

        Dim item As New CorrespondenceItem With {
            .ItemDate = New DateTime(2026, 7, 10), .Type = CorrespondenceType.DecisionLetter, .Title = "Linux decision letter", .IsManagedCopy = True
        }
        submission.Correspondence.Add(item)
        Dim letterName As String = "decision-letter.txt"
        item.LocalFilePath = WriteManaged(sourceManaged, letterName, "letter", mid, submission.Id.ToString("N"), item.Id.ToString("N"))
        linuxPaths(item.LocalFilePath) = linuxRoot & "/" & mid & "/" & submission.Id.ToString("N") & "/" & item.Id.ToString("N") & "/" & letterName

        ' Saved here (saving checks the managed files exist), then the
        ' saved file's three stored paths are made the Linux ones, as a
        ' library saved on Linux holds them.
        Dim sourceRepository As New ManuscriptRepository(sourceData, sourceManaged)
        sourceRepository.Save(manuscripts)
        Dim saved As JsonNode = JsonNode.Parse(File.ReadAllText(sourceRepository.DataFilePath))
        Assert.AreEqual(3, ReplaceStoredPaths(saved, linuxPaths))
        File.WriteAllText(sourceRepository.DataFilePath, saved.ToJsonString())
        StringAssert.Contains(File.ReadAllText(sourceRepository.DataFilePath), linuxRoot & "/" & mid & "/versions/")

        Dim backupPath As String = Path.Combine(_root, "linux.zip")
        Call New PortableBackupService(sourceManaged).CreateBackup(backupPath, manuscripts, sourceRepository)

        Dim targetData As String = Path.Combine(_root, "target-data")
        Dim targetManaged As String = Path.Combine(_root, "target-library")
        Dim targetRepository As New ManuscriptRepository(targetData, targetManaged)
        Dim current As New List(Of Manuscript)()
        targetRepository.Save(current)

        Dim result As RestoreResult = New PortableRestoreService(targetManaged).RestoreBackup(backupPath, current, targetRepository)
        Assert.AreEqual(3, result.ManuscriptCount)

        Dim restored As Manuscript = targetRepository.Load().Single(Function(candidate) candidate.Id = manuscript.Id)
        Dim restoredVersion As ManuscriptVersion = restored.Versions.Single(Function(candidate) candidate.Id = version.Id)
        Dim restoredFile As SubmissionPacketFile = restored.SubmissionPackets.Single(Function(candidate) candidate.Id = packet.Id).Files.Single()
        Dim restoredItem As CorrespondenceItem = restored.Submissions.Single(Function(candidate) candidate.Id = submission.Id).Correspondence.Single(Function(candidate) candidate.Id = item.Id)

        Assert.AreEqual(Path.Combine(targetManaged, mid, "versions", version.Id.ToString("N"), versionName), restoredVersion.LocalFilePath)
        Assert.AreEqual(Path.Combine(targetManaged, mid, "packets", packet.Id.ToString("N"), packetFile.Id.ToString("N"), packetName), restoredFile.LocalFilePath)
        Assert.AreEqual(Path.Combine(targetManaged, mid, submission.Id.ToString("N"), item.Id.ToString("N"), letterName), restoredItem.LocalFilePath)

        Assert.AreEqual("version", File.ReadAllText(restoredVersion.LocalFilePath))
        Assert.AreEqual("packet file", File.ReadAllText(restoredFile.LocalFilePath))
        Assert.AreEqual("letter", File.ReadAllText(restoredItem.LocalFilePath))

    End Sub


    Private Shared Function WriteManaged(managedRoot As String, name As String, content As String, ParamArray folders As String()) As String
        Dim folder As String = Path.Combine({managedRoot}.Concat(folders).ToArray())
        Directory.CreateDirectory(folder)
        Dim written As String = Path.Combine(folder, name)
        File.WriteAllText(written, content)
        Return written
    End Function


    ' Every LocalFilePath value the map names, replaced; the count replaced.
    Private Shared Function ReplaceStoredPaths(node As JsonNode, replacements As Dictionary(Of String, String)) As Integer
        Dim replaced As Integer = 0
        If TypeOf node Is JsonObject Then
            Dim record As JsonObject = DirectCast(node, JsonObject)
            For Each key As String In record.Select(Function(pair) pair.Key).ToList()
                Dim child As JsonNode = record(key)
                If child Is Nothing Then Continue For
                If key = "LocalFilePath" AndAlso child.GetValueKind() = JsonValueKind.String AndAlso
                   replacements.ContainsKey(child.GetValue(Of String)()) Then
                    record(key) = JsonValue.Create(replacements(child.GetValue(Of String)()))
                    replaced += 1
                Else
                    replaced += ReplaceStoredPaths(child, replacements)
                End If
            Next
        ElseIf TypeOf node Is JsonArray Then
            For Each child As JsonNode In DirectCast(node, JsonArray)
                If child IsNot Nothing Then replaced += ReplaceStoredPaths(child, replacements)
            Next
        End If
        Return replaced
    End Function

End Class
