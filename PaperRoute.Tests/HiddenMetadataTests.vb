Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.Linq
Imports System.Text
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline.Services

' The hidden information a packet export (#45) shows before files are
' copied: read only, never removed, and never a crash on a bad file.
<TestClass>
Public Class HiddenMetadataTests

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
    Public Sub Docx_ReportsAuthorSaverCompanyTemplateCommentsAndTrackedChanges()
        Dim target As String = Path.Combine(_root, "document.docx")
        WriteDocx(target, "HIDDEN Creator", "HIDDEN Saver",
                  company:="HIDDEN Company", templatePath:="\\server\SECRET\x.dotx",
                  commentAuthor:="HIDDEN Commenter", trackedAuthor:="HIDDEN Tracker")

        Dim report As HiddenMetadataReport = HiddenMetadataService.Inspect(target)

        Assert.AreEqual(HiddenMetadataState.Checked, report.State)
        AssertFinding(report, "Author", "HIDDEN Creator", True)
        AssertFinding(report, "Last saved by", "HIDDEN Saver", True)
        AssertFinding(report, "Comment authors", "HIDDEN Commenter", True)
        AssertFinding(report, "Tracked changes by", "HIDDEN Tracker", True)
        AssertFinding(report, "Company", "HIDDEN Company", False)
        AssertFinding(report, "Template", "\\server\SECRET\x.dotx", False)
        Assert.IsTrue(report.HasFindings)
        Assert.IsFalse(report.ReachedLimit)
        Assert.AreEqual(
            "Author: HIDDEN Creator; Last saved by: HIDDEN Saver; Comment authors: HIDDEN Commenter; Tracked changes by: HIDDEN Tracker; Company: HIDDEN Company; Template: \\server\SECRET\x.dotx",
            report.Summary())

        Dim plain As String = Path.Combine(_root, "plain.docx")
        WriteDocx(plain, "", "", templatePath:="Normal.dotm")
        Dim plainReport As HiddenMetadataReport = HiddenMetadataService.Inspect(plain)
        Assert.AreEqual(HiddenMetadataState.Checked, plainReport.State)
        Assert.AreEqual(0, plainReport.Findings.Count, "A bare template name such as Normal.dotm is not a path.")
        Assert.IsFalse(plainReport.HasFindings)
        Assert.AreEqual("None found", plainReport.Summary())
    End Sub


    <TestMethod>
    Public Sub Docx_ValuesAreCleanedCappedAndListedOnce()
        Dim target As String = Path.Combine(_root, "many.docx")
        Dim others As IEnumerable(Of String) = Enumerable.Range(1, 14).Select(Function(number) "Reviewer " & number.ToString())
        WriteDocx(target, "  HIDDEN   spaced" & vbTab & "name  ", New String("x"c, 200),
                  commentAuthor:="Reviewer 1", extraCommentAuthors:=others.Concat({"reviewer 1"}))

        Dim report As HiddenMetadataReport = HiddenMetadataService.Inspect(target)

        AssertFinding(report, "Author", "HIDDEN spaced name", True)
        Dim saver As String = report.Findings.Single(Function(item) item.Label = "Last saved by").Value
        Assert.AreEqual(121, saver.Length, "Long values are cut to 120 characters and an ellipsis.")
        StringAssert.EndsWith(saver, "…")
        Dim commenters As String() = report.Findings.Single(Function(item) item.Label = "Comment authors").Value.Split(", ")
        Assert.AreEqual(10, commenters.Length, "At most ten values per label.")
        Assert.AreEqual(10, commenters.Distinct(StringComparer.OrdinalIgnoreCase).Count(), "Each value once, ignoring case.")
    End Sub


    <TestMethod>
    Public Sub Odt_ReportsInitialCreatorAndLastSaver()
        Dim target As String = Path.Combine(_root, "supplement.odt")
        WriteOdt(target, "HIDDEN Initial", "HIDDEN Last")

        Dim report As HiddenMetadataReport = HiddenMetadataService.Inspect(target)

        Assert.AreEqual(HiddenMetadataState.Checked, report.State)
        AssertFinding(report, "Author", "HIDDEN Initial", True)
        AssertFinding(report, "Last saved by", "HIDDEN Last", True)
        Assert.AreEqual(2, report.Findings.Count, "The generator names software, not a person.")
    End Sub


    <TestMethod>
    Public Sub Pdf_ReadsLiteralStringsWithEscapes()
        Dim target As String = Path.Combine(_root, "literal.pdf")
        WritePdf(target, infoAuthorLiteral:="HIDDEN \(A\) B\\C \101 (nested) \" & vbLf & "joined")

        Dim report As HiddenMetadataReport = HiddenMetadataService.Inspect(target)

        Assert.AreEqual(HiddenMetadataState.Checked, report.State)
        AssertFinding(report, "Author", "HIDDEN (A) B\C A (nested) joined", True)
    End Sub


    <TestMethod>
    Public Sub Pdf_ReadsHexUtf16BigEndian()
        Dim target As String = Path.Combine(_root, "hex.pdf")
        WritePdf(target, infoAuthorHexUtf16:="HIDDEN Ünïcode 名前")

        Dim report As HiddenMetadataReport = HiddenMetadataService.Inspect(target)

        AssertFinding(report, "Author", "HIDDEN Ünïcode 名前", True)
    End Sub


    <TestMethod>
    Public Sub Pdf_ReadsXmpCreatorInsideFlateStream()
        Dim target As String = Path.Combine(_root, "xmp.pdf")
        WritePdf(target, xmpCreatorFlate:="HIDDEN XMP & Co")
        Dim raw As String = Encoding.Latin1.GetString(File.ReadAllBytes(target))
        Assert.IsFalse(raw.Contains("HIDDEN XMP"), "The fixture compresses the XMP packet.")

        Dim report As HiddenMetadataReport = HiddenMetadataService.Inspect(target)

        AssertFinding(report, "Author", "HIDDEN XMP & Co", True)
    End Sub


    <TestMethod>
    Public Sub Pdf_ReadsAuthorInsideCompressedObjectStream()
        Dim target As String = Path.Combine(_root, "objstm.pdf")
        WritePdf(target, objStmAuthor:="HIDDEN ObjStm Author")
        Assert.IsFalse(Encoding.Latin1.GetString(File.ReadAllBytes(target)).Contains("HIDDEN ObjStm"), "The fixture compresses the object stream.")

        Dim report As HiddenMetadataReport = HiddenMetadataService.Inspect(target)

        AssertFinding(report, "Author", "HIDDEN ObjStm Author", True)
    End Sub


    <TestMethod>
    Public Sub Pdf_DoesNotReportProducerOrCreatorSoftware()
        Dim target As String = Path.Combine(_root, "software.pdf")
        WritePdf(target, xmpCreatorFlate:="HIDDEN Person", producer:="HIDDEN-SOFTWARE 1.0")

        Dim report As HiddenMetadataReport = HiddenMetadataService.Inspect(target)

        Assert.AreEqual(1, report.Findings.Count)
        AssertFinding(report, "Author", "HIDDEN Person", True)
        Assert.IsFalse(report.Summary().Contains("HIDDEN-SOFTWARE"), "Producer, Creator, pdf:Producer, and CreatorTool name software.")

        Dim softwareOnly As String = Path.Combine(_root, "software-only.pdf")
        WritePdf(softwareOnly, producer:="HIDDEN-SOFTWARE 1.0")
        Dim softwareReport As HiddenMetadataReport = HiddenMetadataService.Inspect(softwareOnly)
        Assert.AreEqual(HiddenMetadataState.Checked, softwareReport.State)
        Assert.AreEqual("None found", softwareReport.Summary())
    End Sub


    <TestMethod>
    Public Sub Jpeg_ReportsCameraDetailsAndLocation()
        Dim target As String = Path.Combine(_root, "photo.jpg")
        WriteJpeg(target, "HIDDEN-MAKE", "Model X", withGps:=True, artist:="HIDDEN Photographer")

        Dim report As HiddenMetadataReport = HiddenMetadataService.Inspect(target)

        Assert.AreEqual(HiddenMetadataState.Checked, report.State)
        AssertFinding(report, "Camera details", "HIDDEN-MAKE Model X", False)
        AssertFinding(report, "Location", "recorded", False)
        AssertFinding(report, "Author", "HIDDEN Photographer", True)

        Dim noGps As String = Path.Combine(_root, "no-gps.jpeg")
        WriteJpeg(noGps, "", "", withGps:=False)
        Dim noGpsReport As HiddenMetadataReport = HiddenMetadataService.Inspect(noGps)
        AssertFinding(noGpsReport, "Camera details", "recorded", False)
        Assert.IsFalse(noGpsReport.Findings.Any(Function(item) item.Label = "Location"))
    End Sub


    <TestMethod>
    Public Sub Png_ReportsTextAndInternationalTextAuthor()
        Dim target As String = Path.Combine(_root, "figure.png")
        WritePng(target, textAuthor:="HIDDEN Text Author", itxtAuthor:="HIDDEN Ïnternational 作者")

        Dim report As HiddenMetadataReport = HiddenMetadataService.Inspect(target)

        Assert.AreEqual(HiddenMetadataState.Checked, report.State)
        AssertFinding(report, "Author", "HIDDEN Text Author, HIDDEN Ïnternational 作者", True)

        Dim plain As String = Path.Combine(_root, "plain.png")
        WritePng(plain)
        Assert.AreEqual("None found", HiddenMetadataService.Inspect(plain).Summary())
    End Sub


    <TestMethod>
    <DataRow(".txt", HiddenMetadataState.PlainType, "Not checked (no hidden fields for this type)")>
    <DataRow(".CSV", HiddenMetadataState.PlainType, "Not checked (no hidden fields for this type)")>
    <DataRow(".md", HiddenMetadataState.PlainType, "Not checked (no hidden fields for this type)")>
    <DataRow(".bin", HiddenMetadataState.NotChecked, "Not checked")>
    <DataRow(".doc", HiddenMetadataState.NotChecked, "Not checked")>
    Public Sub PlainAndUnknownTypes_AreNotChecked(extension As String, expected As HiddenMetadataState, summary As String)
        Dim target As String = Path.Combine(_root, "file" & extension)
        File.WriteAllText(target, "condition,estimate" & vbLf & "Author: not a hidden field" & vbLf)

        Dim report As HiddenMetadataReport = HiddenMetadataService.Inspect(target)

        Assert.AreEqual(expected, report.State)
        Assert.AreEqual(summary, report.Summary())
        Assert.IsFalse(report.HasFindings)
    End Sub


    <TestMethod>
    Public Sub EmptyAndMissingFiles_AreReportedPlainly()
        Dim empty As String = Path.Combine(_root, "empty.pdf")
        File.WriteAllBytes(empty, Array.Empty(Of Byte)())
        Assert.AreEqual("None found", HiddenMetadataService.Inspect(empty).Summary())

        Dim emptyText As String = Path.Combine(_root, "empty.txt")
        File.WriteAllBytes(emptyText, Array.Empty(Of Byte)())
        Assert.AreEqual(HiddenMetadataState.PlainType, HiddenMetadataService.Inspect(emptyText).State)

        Assert.AreEqual(HiddenMetadataState.CouldNotCheck, HiddenMetadataService.Inspect(Path.Combine(_root, "absent.docx")).State)
        Assert.AreEqual(HiddenMetadataState.CouldNotCheck, HiddenMetadataService.Inspect("").State)
    End Sub


    <TestMethod>
    Public Sub DamagedFiles_NeverThrow()
        Dim random As New Random(45)

        Dim garbage(4095) As Byte
        random.NextBytes(garbage)
        Dim fakeDocx As String = Path.Combine(_root, "random.docx")
        File.WriteAllBytes(fakeDocx, garbage)
        Assert.AreEqual(HiddenMetadataState.CouldNotCheck, HiddenMetadataService.Inspect(fakeDocx).State)
        Assert.AreEqual("Couldn't be checked", HiddenMetadataService.Inspect(fakeDocx).Summary())

        Dim whole As Byte() = BuildPdf(infoAuthorLiteral:="HIDDEN Author", xmpCreatorFlate:="HIDDEN XMP")
        Dim truncated As String = Path.Combine(_root, "truncated.pdf")
        File.WriteAllBytes(truncated, whole.Take(whole.Length \ 2).ToArray())
        Assert.AreEqual(HiddenMetadataState.CouldNotCheck, HiddenMetadataService.Inspect(truncated).State)

        ' Random bytes, half of them behind a real signature so each reader
        ' runs on garbage, plus cut and bit-flipped copies of real files.
        Dim signatures As New Dictionary(Of String, Byte())(StringComparer.Ordinal) From {
            {".pdf", Encoding.ASCII.GetBytes("%PDF-1.7" & vbLf)},
            {".jpg", New Byte() {&HFF, &HD8, &HFF, &HE1, &H0, &H40, &H45, &H78, &H69, &H66, 0, 0, &H4D, &H4D, 0, &H2A}},
            {".png", New Byte() {&H89, &H50, &H4E, &H47, &HD, &HA, &H1A, &HA}},
            {".docx", New Byte() {&H50, &H4B, 3, 4}},
            {".odt", New Byte() {&H50, &H4B, 3, 4}},
            {".txt", Array.Empty(Of Byte)()},
            {".bin", Array.Empty(Of Byte)()}
        }

        Dim docxPath As String = Path.Combine(_root, "real.docx")
        WriteDocx(docxPath, "HIDDEN", "HIDDEN", commentAuthor:="HIDDEN", trackedAuthor:="HIDDEN")
        Dim odtPath As String = Path.Combine(_root, "real.odt")
        WriteOdt(odtPath, "HIDDEN", "HIDDEN")
        Dim realFiles As New Dictionary(Of String, Byte())(StringComparer.Ordinal) From {
            {".pdf", BuildPdf(infoAuthorLiteral:="HIDDEN", infoAuthorHexUtf16:="HIDDEN", xmpCreatorFlate:="HIDDEN", objStmAuthor:="HIDDEN")},
            {".jpg", BuildJpeg("HIDDEN", "HIDDEN", True, "HIDDEN")},
            {".png", BuildPng("HIDDEN", "HIDDEN")},
            {".docx", File.ReadAllBytes(docxPath)},
            {".odt", File.ReadAllBytes(odtPath)}
        }

        Dim count As Integer = 0
        For Each extension As String In signatures.Keys
            For sample As Integer = 0 To 59
                Dim body(random.Next(0, 2048)) As Byte
                random.NextBytes(body)
                Dim bytes As Byte() = If(sample Mod 2 = 0, signatures(extension).Concat(body).ToArray(), body)
                If sample Mod 3 = 0 AndAlso realFiles.ContainsKey(extension) Then
                    bytes = realFiles(extension).ToArray()
                    If sample Mod 2 = 0 Then
                        bytes = bytes.Take(random.Next(1, bytes.Length)).ToArray()
                    Else
                        For flip As Integer = 0 To 7
                            Dim at As Integer = random.Next(0, bytes.Length)
                            bytes(at) = CByte(bytes(at) Xor (1 << random.Next(0, 8)))
                        Next
                    End If
                End If

                Dim target As String = Path.Combine(_root, "fuzz-" & count.ToString() & extension)
                File.WriteAllBytes(target, bytes)
                Dim report As HiddenMetadataReport = HiddenMetadataService.Inspect(target)
                Assert.IsNotNull(report)
                Assert.IsFalse(String.IsNullOrEmpty(report.Summary()))
                count += 1
            Next
        Next
    End Sub


    <TestMethod>
    Public Sub ByteLimit_IsReportedWhenReached()
        Dim target As String = Path.Combine(_root, "large.pdf")
        WritePdf(target, infoAuthorLiteral:="HIDDEN Middle Author", padding:=4096)
        Assert.IsTrue(New FileInfo(target).Length > 8192)

        Dim limited As HiddenMetadataReport = HiddenMetadataService.Inspect(target, byteLimit:=1024)
        Assert.AreEqual(HiddenMetadataState.Checked, limited.State)
        Assert.IsTrue(limited.ReachedLimit)
        Assert.IsFalse(limited.Findings.Any(), "The author sits in the part that wasn't read.")
        Assert.AreEqual("None found (checked part of this large file)", limited.Summary())

        Dim whole As HiddenMetadataReport = HiddenMetadataService.Inspect(target)
        Assert.IsFalse(whole.ReachedLimit)
        AssertFinding(whole, "Author", "HIDDEN Middle Author", True)

        Dim docx As String = Path.Combine(_root, "large.docx")
        WriteDocx(docx, "HIDDEN Creator", "HIDDEN Saver", body:=New String("w"c, 20000), trackedAuthor:="HIDDEN Tracker")
        Dim cut As HiddenMetadataReport = HiddenMetadataService.Inspect(docx, byteLimit:=2048)
        Assert.AreEqual(HiddenMetadataState.Checked, cut.State, "A part cut off at the limit keeps what was read.")
        Assert.IsTrue(cut.ReachedLimit)
        AssertFinding(cut, "Author", "HIDDEN Creator", True)
        StringAssert.EndsWith(cut.Summary(), " (checked part of this large file)")
    End Sub


    <TestMethod>
    Public Sub Inspect_ChangesNothing()
        Dim targets As New List(Of String)()
        Dim docx As String = Path.Combine(_root, "unchanged.docx")
        WriteDocx(docx, "HIDDEN Creator", "HIDDEN Saver", commentAuthor:="HIDDEN Commenter")
        targets.Add(docx)
        Dim pdf As String = Path.Combine(_root, "unchanged.pdf")
        WritePdf(pdf, infoAuthorLiteral:="HIDDEN", xmpCreatorFlate:="HIDDEN")
        targets.Add(pdf)
        Dim png As String = Path.Combine(_root, "unchanged.png")
        WritePng(png, "HIDDEN")
        targets.Add(png)

        For Each target As String In targets
            Dim stamp As New DateTime(2001, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            File.SetLastWriteTimeUtc(target, stamp)
            Dim before As Byte() = File.ReadAllBytes(target)

            ' Another reader holding the file open doesn't stop the check.
            Using other As New FileStream(target, FileMode.Open, FileAccess.Read, FileShare.Read)
                Dim report As HiddenMetadataReport = HiddenMetadataService.Inspect(target)
                Assert.AreEqual(HiddenMetadataState.Checked, report.State, target)
                Assert.IsTrue(report.HasFindings, target)
            End Using

            CollectionAssert.AreEqual(before, File.ReadAllBytes(target), "Inspection never writes.")
            Assert.AreEqual(stamp, File.GetLastWriteTimeUtc(target), "Inspection never changes the file's time.")
        Next
    End Sub


    <TestMethod>
    Public Sub Summary_Texts()
        Dim findings As New List(Of HiddenMetadataFinding) From {
            New HiddenMetadataFinding("Author", "A. Researcher", True),
            New HiddenMetadataFinding("Last saved by", "B. Person", True)
        }

        Assert.AreEqual("Author: A. Researcher; Last saved by: B. Person", New HiddenMetadataReport(HiddenMetadataState.Checked, findings).Summary())
        Assert.AreEqual("None found", New HiddenMetadataReport(HiddenMetadataState.Checked).Summary())
        Assert.AreEqual("Not checked (no hidden fields for this type)", New HiddenMetadataReport(HiddenMetadataState.PlainType).Summary())
        Assert.AreEqual("Not checked", New HiddenMetadataReport(HiddenMetadataState.NotChecked).Summary())
        Assert.AreEqual("Couldn't be checked", New HiddenMetadataReport(HiddenMetadataState.CouldNotCheck).Summary())
        Assert.AreEqual("None found (checked part of this large file)", New HiddenMetadataReport(HiddenMetadataState.Checked, reachedLimit:=True).Summary())
        Assert.AreEqual(
            "Author: A. Researcher; Last saved by: B. Person (checked part of this large file)",
            New HiddenMetadataReport(HiddenMetadataState.Checked, findings, True).Summary())

        Assert.IsTrue(New HiddenMetadataReport(HiddenMetadataState.Checked, findings).HasFindings)
        Assert.IsFalse(New HiddenMetadataReport(HiddenMetadataState.NotChecked, findings).HasFindings, "Only a checked file has findings.")
    End Sub


    Private Shared Sub AssertFinding(report As HiddenMetadataReport, findingLabel As String, value As String, namesPerson As Boolean)
        Dim finding As HiddenMetadataFinding = report.Findings.SingleOrDefault(Function(item) item.Label = findingLabel)
        Assert.IsNotNull(finding, findingLabel & " was not reported. Summary: " & report.Summary())
        Assert.AreEqual(value, finding.Value, findingLabel)
        Assert.AreEqual(namesPerson, finding.NamesPerson, findingLabel)
    End Sub

End Class
