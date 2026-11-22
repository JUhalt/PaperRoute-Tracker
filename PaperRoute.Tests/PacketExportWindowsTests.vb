Imports System
Imports System.Collections.Generic
Imports System.Diagnostics
Imports System.Globalization
Imports System.IO
Imports System.IO.Compression
Imports System.Linq
Imports System.Runtime.InteropServices
Imports System.Security.Cryptography
Imports System.Text
Imports System.Text.Json.Nodes
Imports System.Text.RegularExpressions
Imports System.Threading
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

' The parts of PacketExportTests (#45) that need Windows (core stage 2,
' batch 3): a packet file held open by another program, which only Windows'
' sharing rules make unreadable (design d2 5.3), and the packet's own file
' reached by another spelling (the \\?\ form, a junction made with cmd.exe,
' a short 8.3 name), which only Windows has and only WindowsFileIdentity,
' registered by PlatformSetup, tells apart. Each test is as it was in
' PacketExportTests; the rest of PacketExportTests is in
' PaperRoute.Core.Tests, and this class has copies of the helpers it shares
' there.
<TestClass>
<DoNotParallelize>
Public Class PacketExportWindowsTests

    Private _root As String

    <TestInitialize>
    Public Sub CreateRoot()
        _root = CreateTemporaryRoot()
    End Sub

    <TestCleanup>
    Public Sub RemoveRoot()
        DeleteTemporaryRoot(_root)
    End Sub


    <TestMethod>
    Public Sub Plan_DefaultsFollowRoleFingerprintAndFile()
        Dim fixture As ExportFixture = SimpleFixture()
        AddFile(fixture, SubmissionPacketFileRole.Manuscript, "Main.pdf", BuildPdf(infoAuthorLiteral:="HIDDEN Author"), "Main text")
        Dim changed As SubmissionPacketFile = AddFile(fixture, SubmissionPacketFileRole.Figure, "Figure 2.png", BuildPng(shade:=1), "Figure 2")
        File.WriteAllBytes(changed.LocalFilePath, BuildPng(shade:=2))
        Dim missing As SubmissionPacketFile = AddFile(fixture, SubmissionPacketFileRole.Table, "Table 1.csv", Encoding.UTF8.GetBytes("a,b" & vbLf), "Table 1")
        File.Delete(missing.LocalFilePath)
        Dim locked As SubmissionPacketFile = AddFile(fixture, SubmissionPacketFileRole.Supplement, "Supplement.txt", Encoding.UTF8.GetBytes("supplement"), "Supplement A")
        AddFile(fixture, SubmissionPacketFileRole.Highlights, "Highlights.txt", Encoding.UTF8.GetBytes("highlights"), "Highlights", record:=False)
        AddFile(fixture, SubmissionPacketFileRole.CoverLetter, "Cover letter.txt", Encoding.UTF8.GetBytes("Dear Editor"), "Cover letter")
        AddFile(fixture, SubmissionPacketFileRole.Other, "Decision letter.txt", Encoding.UTF8.GetBytes("Decision"), "Decision letter")
        fixture.Packet.Files.Add(New SubmissionPacketFile With {.Role = SubmissionPacketFileRole.ReportingChecklist, .Label = "Checklist in the portal", .StorageMode = SubmissionPacketFileStorageMode.MetadataOnly})
        fixture.Packet.Files.Add(New SubmissionPacketFile With {.Role = SubmissionPacketFileRole.Supplement, .Label = "No path recorded", .OriginalFileName = "Lost.txt", .StorageMode = SubmissionPacketFileStorageMode.LinkedExternal})
        fixture.Packet.Files.Add(Nothing)

        Dim plan As PacketExportPlan = PacketExportService.Prepare(fixture.Manuscript, fixture.Packet, fixture.Library)

        Assert.AreEqual(9, plan.Rows.Count, "One row per file; a null entry is skipped.")
        Assert.AreEqual(0, plan.IncludedRows().Count, "Nothing is included before the files are checked.")
        Assert.AreEqual("Checking...", PacketExportService.FingerprintText(RowFor(plan, "Main text").Fingerprint))
        Assert.AreEqual(PacketExportFingerprint.NoFile, RowFor(plan, "Checklist in the portal").Fingerprint)
        Assert.AreEqual("Reporting checklist has no file. The package lists it by role only.", RowFor(plan, "Checklist in the portal").Note)
        Assert.AreEqual(PacketExportFingerprint.Missing, RowFor(plan, "No path recorded").Fingerprint)
        Assert.AreEqual("Lost.txt can't be included: the file wasn't found.", RowFor(plan, "No path recorded").Note)
        Assert.AreEqual(SubmissionPacketFileRole.Other, plan.Rows(0).Role, "Rows keep the vault's order: role, then label.")

        Using holder As New FileStream(locked.LocalFilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None)
            PacketExportService.CheckFiles(plan)
        End Using

        Dim main As PacketExportRow = RowFor(plan, "Main text")
        Assert.AreEqual(PacketExportFingerprint.Unchanged, main.Fingerprint)
        Assert.IsTrue(main.Include)
        Assert.AreEqual("", main.Note)
        Assert.IsTrue(main.Hidden.HasFindings)
        Assert.AreEqual(main.RecordedSha256, main.ObservedSha256)

        Dim changedRow As PacketExportRow = RowFor(plan, "Figure 2")
        Assert.AreEqual(PacketExportFingerprint.Changed, changedRow.Fingerprint)
        Assert.IsTrue(changedRow.CanInclude)
        Assert.IsFalse(changedRow.Include, "A changed file starts unchecked.")
        Assert.AreEqual("Figure 2.png starts unchecked: it changed since its fingerprint was recorded.", changedRow.Note)

        Dim missingRow As PacketExportRow = RowFor(plan, "Table 1")
        Assert.AreEqual(PacketExportFingerprint.Missing, missingRow.Fingerprint)
        Assert.IsFalse(missingRow.CanInclude)
        Assert.AreEqual("Table 1.csv can't be included: the file wasn't found.", missingRow.Note)

        Dim lockedRow As PacketExportRow = RowFor(plan, "Supplement A")
        Assert.AreEqual(PacketExportFingerprint.Unreadable, lockedRow.Fingerprint)
        Assert.AreEqual("Can't be read", PacketExportService.FingerprintText(lockedRow.Fingerprint))
        Assert.AreEqual("Supplement.txt can't be included: the file couldn't be read. It may be open in another program.", lockedRow.Note)

        Dim highlights As PacketExportRow = RowFor(plan, "Highlights")
        Assert.AreEqual(PacketExportFingerprint.NotRecorded, highlights.Fingerprint)
        Assert.IsTrue(highlights.Include, "Never fingerprinted is still included by default.")
        Assert.AreEqual("", highlights.RecordedSha256)
        Assert.AreEqual(Sha256Of(highlights.Source.LocalFilePath), highlights.ObservedSha256, "The check fingerprints it too, to notice a later change.")
        Assert.AreEqual("", fixture.Packet.Files.Single(Function(item) item IsNot Nothing AndAlso item.Label = "Highlights").Sha256, "Nothing is recorded.")

        Dim letter As PacketExportRow = RowFor(plan, "Cover letter")
        Assert.IsFalse(letter.Include)
        Assert.AreEqual("Cover letter.txt starts unchecked: it can name the editor or suggested reviewers.", letter.Note)
        Assert.AreEqual("Decision letter.txt starts unchecked: it may contain anything, so check it first.", RowFor(plan, "Decision letter").Note)

        Dim checklist As PacketExportRow = RowFor(plan, "Checklist in the portal")
        Assert.IsFalse(checklist.CanInclude)
        Assert.IsNull(checklist.Hidden)

        missingRow.Include = True
        checklist.Include = True
        lockedRow.Include = True
        Assert.IsFalse(missingRow.Include, "A file that can't be included stays out.")
        Assert.IsFalse(checklist.Include)
        Assert.IsFalse(lockedRow.Include)
        changedRow.Include = True
        Assert.IsTrue(changedRow.Include, "The user may include a changed file.")
        letter.Include = True
        Assert.IsTrue(letter.Include)

        ' Checking again keeps the user's choices for files that didn't change.
        PacketExportService.CheckFiles(plan)
        Assert.IsTrue(changedRow.Include)
        Assert.IsTrue(letter.Include)
        Assert.AreEqual(PacketExportFingerprint.Unchanged, RowFor(plan, "Supplement A").Fingerprint, "The file is readable once released.")
        Assert.IsTrue(RowFor(plan, "Supplement A").Include, "A file whose state changed gets its default again.")
    End Sub


    <TestMethod>
    Public Sub WriteZip_RefusesAPacketFileReachedAnotherWay()
        Dim real As String = Path.Combine(_root, "Packet files with a long folder name")
        Directory.CreateDirectory(real)
        Dim supplement As String = Path.Combine(real, "Supplement.zip")
        File.WriteAllText(supplement, "the packet's own supplement")
        Dim fixture As ExportFixture = SimpleFixture()
        AddFile(fixture, SubmissionPacketFileRole.Manuscript, "Main.pdf", BuildPdf(), "Main")
        fixture.Packet.Files.Add(New SubmissionPacketFile With {
            .Role = SubmissionPacketFileRole.Supplement, .Label = "Data", .OriginalFileName = "Supplement.zip",
            .LocalFilePath = supplement, .StorageMode = SubmissionPacketFileStorageMode.LinkedExternal
        })
        Dim plan As PacketExportPlan = PacketExportService.Prepare(fixture.Manuscript, fixture.Packet, fixture.Library)
        PacketExportService.CheckFiles(plan)

        ' The same file by spellings that Path.GetFullPath leaves as they are:
        ' the extended-length form, and a junction to its folder where this
        ' machine can make one. (A short 8.3 name it expands, so the path
        ' comparison already catches that one.)
        Dim spellings As New List(Of String) From {"\\?\" & supplement}
        Dim junction As String = Path.Combine(_root, "linked")
        Try
            If TryCreateJunction(junction, real) Then spellings.Add(Path.Combine(junction, "Supplement.zip"))
            Dim shortFolder As String = ShortPathOf(real)
            If shortFolder.Length > 0 AndAlso Not String.Equals(shortFolder, real, StringComparison.OrdinalIgnoreCase) Then
                Assert.AreEqual(supplement, Path.GetFullPath(Path.Combine(shortFolder, "Supplement.zip")), "A short name is expanded.")
            End If

            For Each other As String In spellings
                Assert.IsFalse(String.Equals(Path.GetFullPath(other), supplement, StringComparison.OrdinalIgnoreCase), "Another spelling: " & other)
                Dim failure As PacketExportException = Assert.ThrowsExactly(Of PacketExportException)(Sub() Export(plan, other))
                Assert.AreEqual(PacketExportFailure.DestinationUnwritable, failure.Kind, other)
                Assert.AreEqual("That file is part of this packet. Choose another name for the .zip.", failure.Message, other)
                Assert.AreEqual("the packet's own supplement", File.ReadAllText(supplement), "A packet file is never overwritten: " & other)
                Assert.AreEqual(0, Directory.GetFiles(real, "*.partial").Length)
            Next
            If spellings.Count = 1 Then Assert.Inconclusive("Checked the extended-length spelling only: this machine couldn't make a junction.")
        Finally
            If Directory.Exists(junction) Then Directory.Delete(junction)
        End Try
    End Sub


    <TestMethod>
    Public Sub MoveIntoPlace_HoldsOnlyReadableFilesAndStopsWhenCancelled()
        Dim fixture As ExportFixture = SimpleFixture()
        Dim main As SubmissionPacketFile = AddFile(fixture, SubmissionPacketFileRole.Manuscript, "Main.pdf", BuildPdf(), "Main")
        Dim letter As SubmissionPacketFile = AddFile(fixture, SubmissionPacketFileRole.CoverLetter, "Letter.txt", Encoding.UTF8.GetBytes("Dear Editor"), "Letter")
        Dim missing As SubmissionPacketFile = AddFile(fixture, SubmissionPacketFileRole.Table, "Table.csv", Encoding.UTF8.GetBytes("a,b" & vbLf), "Table")
        File.Delete(missing.LocalFilePath)
        Dim locked As SubmissionPacketFile = AddFile(fixture, SubmissionPacketFileRole.Supplement, "Locked.txt", Encoding.UTF8.GetBytes("locked"), "Locked")
        fixture.Packet.Files.Add(New SubmissionPacketFile With {.Role = SubmissionPacketFileRole.ReportingChecklist, .Label = "Checklist", .StorageMode = SubmissionPacketFileStorageMode.MetadataOnly})

        Dim plan As PacketExportPlan = PacketExportService.Prepare(fixture.Manuscript, fixture.Packet, fixture.Library)
        Using holder As New FileStream(locked.LocalFilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None)
            PacketExportService.CheckFiles(plan)
        End Using
        Assert.AreEqual(PacketExportFingerprint.Missing, RowFor(plan, "Table").Fingerprint)
        Assert.AreEqual(PacketExportFingerprint.Unreadable, RowFor(plan, "Locked").Fingerprint)
        Assert.IsFalse(RowFor(plan, "Letter").Include)

        ' Only the files the check could read are held during the move,
        ' included or not: a missing or unreadable one (perhaps on a share
        ' that has gone offline) is never opened again.
        CollectionAssert.AreEquivalent({main.LocalFilePath, letter.LocalFilePath}, PacketExportService.HeldPacketFilePaths(plan))

        Dim temporary As String = OutputPath("moved.zip.test.partial")
        Dim destination As String = OutputPath("moved.zip")
        File.WriteAllText(temporary, "the finished zip")
        Using source As New CancellationTokenSource()
            source.Cancel()
            Assert.ThrowsExactly(Of OperationCanceledException)(Sub() PacketExportService.MoveIntoPlace(plan, temporary, destination, source.Token))
        End Using
        Assert.IsFalse(File.Exists(destination), "A stopped export moves nothing into place.")
        Assert.IsTrue(File.Exists(temporary))

        ' A held file is still never replaced, however its path is spelled.
        Dim failure As PacketExportException = Assert.ThrowsExactly(Of PacketExportException)(
            Sub() PacketExportService.MoveIntoPlace(plan, temporary, "\\?\" & letter.LocalFilePath))
        Assert.AreEqual(PacketExportFailure.DestinationUnwritable, failure.Kind)
        Assert.AreEqual("Dear Editor", File.ReadAllText(letter.LocalFilePath))

        PacketExportService.MoveIntoPlace(plan, temporary, destination)
        Assert.AreEqual("the finished zip", File.ReadAllText(destination))
        Assert.IsFalse(File.Exists(temporary))
    End Sub


    ' ---- Helpers ----------------------------------------------------------------

    Private Function SimpleFixture(Optional packetLabel As String = "Test packet") As ExportFixture
        Dim manuscript As New Manuscript With {.Title = "A test manuscript"}
        Dim version As New ManuscriptVersion With {.Label = "Version 1"}
        manuscript.Versions.Add(version)
        Dim packet As New SubmissionPacket With {.Label = packetLabel, .ManuscriptVersionId = version.Id, .JournalName = "Test Journal"}
        manuscript.SubmissionPackets.Add(packet)
        Dim folder As String = Path.Combine(_root, "files")
        Directory.CreateDirectory(folder)
        Return New ExportFixture With {
            .Root = _root, .SourceFolder = folder, .Manuscript = manuscript,
            .Library = New AuthorLibraryData(), .Packet = packet, .Version = version
        }
    End Function


    Private Shared Function AddFile(
        fixture As ExportFixture,
        role As SubmissionPacketFileRole,
        originalName As String,
        content As Byte(),
        Optional label As String = "",
        Optional record As Boolean = True
    ) As SubmissionPacketFile
        Dim extension As String = Path.GetExtension(originalName.Replace(":"c, "_"c))
        Dim stored As String = Path.Combine(fixture.SourceFolder, "stored-" & Guid.NewGuid().ToString("N") & extension)
        File.WriteAllBytes(stored, content)
        Dim item As New SubmissionPacketFile With {
            .Role = role, .Label = label, .OriginalFileName = originalName,
            .LocalFilePath = stored, .StorageMode = SubmissionPacketFileStorageMode.LinkedExternal
        }
        If record Then SubmissionPacketIntegrityService.CaptureBaseline(item)
        fixture.Packet.Files.Add(item)
        Return item
    End Function


    Private Function OutputPath(name As String) As String
        Dim folder As String = Path.Combine(_root, "out")
        Directory.CreateDirectory(folder)
        Return Path.Combine(folder, name)
    End Function


    Private Shared Function Export(plan As PacketExportPlan, zipPath As String) As PacketExportResult
        Return PacketExportService.WriteZip(plan, zipPath, ExportTime, TestVersion)
    End Function


    Private Shared Function RowFor(plan As PacketExportPlan, rowLabel As String) As PacketExportRow
        Return plan.Rows.Single(Function(item) item.Label = rowLabel)
    End Function


    Private Shared Function Sha256Of(filePath As String) As String
        Return Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(filePath))).ToLowerInvariant()
    End Function


    ' A directory junction needs no special rights; False when it can't be made.
    Private Shared Function TryCreateJunction(link As String, target As String) As Boolean
        Try
            Dim start As New ProcessStartInfo("cmd.exe", "/c mklink /J """ & link & """ """ & target & """") With {
                .UseShellExecute = False, .CreateNoWindow = True, .RedirectStandardOutput = True, .RedirectStandardError = True
            }
            Using maker As Process = Process.Start(start)
                maker.StandardOutput.ReadToEnd()
                maker.StandardError.ReadToEnd()
                If Not maker.WaitForExit(30000) Then Return False
            End Using
            Return Directory.Exists(link) AndAlso File.Exists(Path.Combine(link, Path.GetFileName(Directory.GetFiles(target).First())))
        Catch ex As Exception When TypeOf ex Is IOException OrElse TypeOf ex Is System.ComponentModel.Win32Exception OrElse TypeOf ex Is InvalidOperationException
            Return False
        End Try
    End Function


    ' The folder's short 8.3 path, or "" when the volume makes none.
    Private Shared Function ShortPathOf(longPath As String) As String
        Dim buffer As New StringBuilder(1024)
        Dim length As Integer = GetShortPathName(longPath, buffer, buffer.Capacity)
        If length <= 0 OrElse length >= buffer.Capacity Then Return String.Empty
        Return buffer.ToString()
    End Function


    <DllImport("kernel32.dll", CharSet:=CharSet.Unicode, SetLastError:=True)>
    Private Shared Function GetShortPathName(longPath As String, shortPath As StringBuilder, bufferLength As Integer) As Integer
    End Function

End Class
