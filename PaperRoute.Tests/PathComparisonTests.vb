Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.Linq
Imports System.Reflection
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

' Path comparison (core stage 2, step 4; design risk R11): on Windows the
' twelve call sites compare exactly as they did with OrdinalIgnoreCase, so
' two spellings of one managed file are still one file.
<TestClass>
Public Class PathComparisonTests

    Private _root As String = String.Empty

    <TestInitialize>
    Public Sub Initialize()
        _root = CreateTemporaryRoot()
    End Sub

    <TestCleanup>
    Public Sub Cleanup()
        DeleteTemporaryRoot(_root)
    End Sub


    ' Pairs a call site can meet: drive and UNC paths, mixed separators,
    ' case, accents, dots and spaces, device names, long names, and folder
    ' names such as the excluded "removed" folder.
    Private Shared Function PathPairs() As List(Of (First As String, Second As String))
        Dim longName As String = New String("a"c, 200) & ".docx"
        Return New List(Of (First As String, Second As String)) From {
            ("C:\A\b.docx", "c:\a\B.DOCX"),
            ("C:\A\b.docx", "C:\A\b.docx"),
            ("C:\A\b.docx", "C:\A\c.docx"),
            ("C:\Users\a\PaperRoute Library\", "C:\Users\a\paperroute library\x\draft..docx"),
            ("\\Server\Share\Papers\draft .docx", "\\server\share\papers\DRAFT .docx"),
            ("\\server\share\papers", "\\server\share\papers2"),
            ("C:\mixed/sep\file.txt", "C:\MIXED/SEP\FILE.TXT"),
            ("C:\mixed/sep\file.txt", "C:\mixed\sep\file.txt"),
            ("C:\x\Résumé.pdf", "C:\x\RÉSUMÉ.PDF"),
            ("C:\x\NUL.txt", "c:\x\nul.TXT"),
            ("C:\x\CON", "C:\x\con"),
            ("C:\x\" & longName, "C:\X\" & longName.ToUpperInvariant()),
            ("removed", "Removed"),
            ("removed", "removed2"),
            ("/home/a/PaperRoute Library/x.docx", "/home/a/paperroute library/X.docx"),
            ("", ""),
            ("C:\straße\a.docx", "C:\STRASSE\a.docx"),
            ("C:\x\a?b*.docx", "c:\X\A?B*.DOCX"),
            ("C:\x\b:c.docx", "C:\x\B:C.docx")
        }
    End Function


    <TestMethod>
    Public Sub TheRuleIsTheSystemsRule()

        If OperatingSystem.IsWindows() OrElse OperatingSystem.IsMacOS() Then
            Assert.IsTrue(PathComparison.IgnoresCase)
            Assert.AreEqual(StringComparison.OrdinalIgnoreCase, PathComparison.Comparison)
            Assert.AreSame(StringComparer.OrdinalIgnoreCase, PathComparison.Comparer)
        Else
            Assert.IsFalse(PathComparison.IgnoresCase)
            Assert.AreEqual(StringComparison.Ordinal, PathComparison.Comparison)
            Assert.AreSame(StringComparer.Ordinal, PathComparison.Comparer)
        End If

    End Sub


    ' The old expression and the new one, side by side, for each shape of
    ' call: String.Equals, StartsWith and Distinct.
    <TestMethod>
    Public Sub OnWindowsEveryComparisonAnswersAsTheOldOne()

        If Not OperatingSystem.IsWindows() Then Return

        For Each pair In PathPairs()
            Dim label As String = pair.First & " | " & pair.Second
            Assert.AreEqual(
                String.Equals(pair.First, pair.Second, StringComparison.OrdinalIgnoreCase),
                String.Equals(pair.First, pair.Second, PathComparison.Comparison), "Equals: " & label)
            Assert.AreEqual(
                pair.Second.StartsWith(pair.First, StringComparison.OrdinalIgnoreCase),
                pair.Second.StartsWith(pair.First, PathComparison.Comparison), "StartsWith: " & label)
            CollectionAssert.AreEqual(
                {pair.First, pair.Second}.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                {pair.First, pair.Second}.Distinct(PathComparison.Comparer).ToList(), "Distinct: " & label)
        Next

    End Sub


    <TestMethod>
    Public Sub TwoSpellingsOfOnePathAreOnePathOnWindows()

        Dim samePath As MethodInfo = GetType(PacketExportService).GetMethod("SamePath", BindingFlags.NonPublic Or BindingFlags.Static)
        Assert.IsNotNull(samePath)

        Dim same As Boolean = CBool(samePath.Invoke(Nothing, New Object() {"C:\A\b.docx", "c:\a\B.DOCX"}))
        Assert.AreEqual(PathComparison.IgnoresCase, same)
        If OperatingSystem.IsWindows() Then Assert.IsTrue(same)

        Dim library As New ManagedLibraryService(Path.Combine(_root, "Library"))
        Dim inside As String = Path.Combine(_root.ToUpperInvariant(), "LIBRARY", "m", "versions", "v", "draft_ab12cd34.docx")
        Assert.AreEqual(PathComparison.IgnoresCase, library.IsManagedPath(inside))
        If OperatingSystem.IsWindows() Then Assert.IsTrue(library.IsManagedPath(inside))

    End Sub


    ' Two packet records whose paths differ only in case: on Windows one
    ' file, held once during the move; on Linux two files.
    <TestMethod>
    Public Sub PacketFilesDifferingOnlyInCaseAreOneFileWhereCaseIsIgnored()

        Dim folder As String = Path.Combine(_root, "files")
        Directory.CreateDirectory(folder)
        Dim lower As String = Path.Combine(folder, "letter.txt")
        Dim upper As String = Path.Combine(folder, "LETTER.TXT")
        File.WriteAllText(lower, "Dear Editor")
        If Not File.Exists(upper) Then File.WriteAllText(upper, "Dear Editor")

        Dim manuscript As New Manuscript With {.Title = "A test manuscript"}
        Dim version As New ManuscriptVersion With {.Label = "Version 1"}
        manuscript.Versions.Add(version)
        Dim packet As New SubmissionPacket With {.Label = "Test packet", .ManuscriptVersionId = version.Id, .JournalName = "Test Journal"}
        manuscript.SubmissionPackets.Add(packet)
        For Each stored As String In {lower, upper}
            packet.Files.Add(New SubmissionPacketFile With {
                .Role = SubmissionPacketFileRole.CoverLetter, .Label = "Letter", .OriginalFileName = Path.GetFileName(stored),
                .LocalFilePath = stored, .StorageMode = SubmissionPacketFileStorageMode.LinkedExternal
            })
        Next

        Dim plan As PacketExportPlan = PacketExportService.Prepare(manuscript, packet, New AuthorLibraryData())
        PacketExportService.CheckFiles(plan)

        Dim held As List(Of String) = PacketExportService.HeldPacketFilePaths(plan)
        Assert.AreEqual(If(PathComparison.IgnoresCase, 1, 2), held.Count)
        If OperatingSystem.IsWindows() Then CollectionAssert.AreEqual({lower}, held)

    End Sub

End Class
