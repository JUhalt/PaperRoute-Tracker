Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.IO.Compression
Imports System.Linq
Imports System.Text
Imports System.Threading
Imports System.Threading.Tasks
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
    <DataRow(".txt", HiddenMetadataState.PlainType, "No hidden fields for this file type")>
    <DataRow(".CSV", HiddenMetadataState.PlainType, "No hidden fields for this file type")>
    <DataRow(".md", HiddenMetadataState.PlainType, "No hidden fields for this file type")>
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
        Assert.AreEqual("No hidden fields for this file type", New HiddenMetadataReport(HiddenMetadataState.PlainType).Summary())
        Assert.AreEqual("Not checked", New HiddenMetadataReport(HiddenMetadataState.NotChecked).Summary())
        Assert.AreEqual("Couldn't be checked", New HiddenMetadataReport(HiddenMetadataState.CouldNotCheck).Summary())
        Assert.AreEqual("None found (checked part of this large file)", New HiddenMetadataReport(HiddenMetadataState.Checked, reachedLimit:=True).Summary())
        Assert.AreEqual(
            "Author: A. Researcher; Last saved by: B. Person (checked part of this large file)",
            New HiddenMetadataReport(HiddenMetadataState.Checked, findings, True).Summary())

        Assert.IsTrue(New HiddenMetadataReport(HiddenMetadataState.Checked, findings).HasFindings)
        Assert.IsFalse(New HiddenMetadataReport(HiddenMetadataState.NotChecked, findings).HasFindings, "Only a checked file has findings.")

        ' What couldn't be read follows what was found.
        Assert.AreEqual(
            "Author: A. Researcher; Last saved by: B. Person; part of this file couldn't be checked",
            New HiddenMetadataReport(HiddenMetadataState.Checked, findings, gap:=HiddenMetadataGap.PartUnreadable).Summary())
        Assert.AreEqual(
            "Author: A. Researcher; Last saved by: B. Person; the rest couldn't be checked (encrypted)",
            New HiddenMetadataReport(HiddenMetadataState.Checked, findings, gap:=HiddenMetadataGap.Encrypted).Summary())
        Assert.IsTrue(New HiddenMetadataReport(HiddenMetadataState.Checked, findings, gap:=HiddenMetadataGap.Encrypted).HasFindings)

        ' A file that couldn't all be read never claims "None found".
        Dim nothingRead As New HiddenMetadataReport(HiddenMetadataState.Checked, gap:=HiddenMetadataGap.PartUnreadable)
        Assert.AreEqual(HiddenMetadataState.CouldNotCheck, nothingRead.State)
        Assert.AreEqual("Couldn't be checked", nothingRead.Summary())
        Assert.AreEqual("Couldn't be checked (encrypted)", New HiddenMetadataReport(HiddenMetadataState.Checked, gap:=HiddenMetadataGap.Encrypted).Summary())
    End Sub


    <TestMethod>
    Public Sub Docx_ReportsPeopleInNotesHeadersFootersPeopleListAndCustomProperties()
        Dim target As String = Path.Combine(_root, "parts.docx")
        Dim propertyStart As String = "<property fmtid=""{D5CDD505-2E9C-101B-9397-08002B2CF9AE}"" pid="""
        WritePackage(target,
            ("[Content_Types].xml", ContentTypesXml),
            ("docProps/core.xml", CoreXml("")),
            ("word/document.xml", WordPart("document", "<w:body><w:p><w:r><w:t>No tracked changes in the body.</w:t></w:r></w:p></w:body>")),
            ("word/footnotes.xml", WordPart("footnotes", "<w:footnote w:id=""1""><w:p><w:ins w:id=""1"" w:author=""HIDDEN Footnote Editor""><w:r><w:t>x</w:t></w:r></w:ins></w:p></w:footnote>")),
            ("word/endnotes.xml", WordPart("endnotes", "<w:endnote w:id=""1""><w:p><w:del w:id=""2"" w:author=""HIDDEN Endnote Editor""><w:r><w:delText>x</w:delText></w:r></w:del></w:p></w:endnote>")),
            ("word/header1.xml", WordPart("hdr", "<w:p><w:pPr><w:pPrChange w:id=""3"" w:author=""HIDDEN Header Editor""><w:pPr/></w:pPrChange></w:pPr></w:p>")),
            ("word/footer2.xml", WordPart("ftr", "<w:p><w:ins w:id=""4"" w:author=""HIDDEN Footer Editor""><w:r><w:t>x</w:t></w:r></w:ins></w:p>")),
            ("word/people.xml",
                "<?xml version=""1.0"" encoding=""UTF-8""?><w15:people xmlns:w15=""http://schemas.microsoft.com/office/word/2012/wordml"">" &
                "<w15:person w15:author=""HIDDEN Person""><w15:presenceInfo w15:providerId=""AD"" w15:userId=""S::hidden.person@example.org::0f1e2d3c""/></w15:person></w15:people>"),
            ("docProps/custom.xml",
                "<?xml version=""1.0"" encoding=""UTF-8""?><Properties xmlns=""http://schemas.openxmlformats.org/officeDocument/2006/custom-properties"" " &
                "xmlns:vt=""http://schemas.openxmlformats.org/officeDocument/2006/docPropsVTypes"">" &
                propertyStart & "2"" name=""_AuthorEmail""><vt:lpwstr>hidden.sender@example.org</vt:lpwstr></property>" &
                propertyStart & "3"" name=""_AuthorEmailDisplayName""><vt:lpwstr>HIDDEN Sender</vt:lpwstr></property>" &
                propertyStart & "4"" name=""Contact point""><vt:lpwstr>HIDDEN Contact</vt:lpwstr></property>" &
                propertyStart & "5"" name=""Reply to""><vt:lpwstr>hidden.reply@example.org</vt:lpwstr></property>" &
                propertyStart & "6"" name=""Project""><vt:lpwstr>Anchoring study</vt:lpwstr></property></Properties>"))

        Dim report As HiddenMetadataReport = HiddenMetadataService.Inspect(target)

        Assert.AreEqual(HiddenMetadataState.Checked, report.State)
        AssertValues(report, "Tracked changes by", "HIDDEN Footnote Editor", "HIDDEN Endnote Editor", "HIDDEN Header Editor", "HIDDEN Footer Editor")
        AssertValues(report, "Comment authors", "HIDDEN Person", "hidden.person@example.org")
        AssertValues(report, "Custom properties", "hidden.sender@example.org", "HIDDEN Sender", "HIDDEN Contact", "hidden.reply@example.org")
        Assert.IsFalse(report.Summary().Contains("Anchoring study"), "A property that names no one isn't listed.")
        Assert.IsTrue(report.Findings.All(Function(item) item.NamesPerson))
        Assert.IsTrue(report.HasFindings)
    End Sub


    <TestMethod>
    Public Sub Docx_ReportsTheAttachedTemplatePath()
        ' Word keeps only the template's name in app.xml and its full path,
        ' with the Windows user name, in settings.xml.rels.
        Dim target As String = Path.Combine(_root, "from-template.docx")
        WriteTemplateDocx(target, "file:///C:\Users\HIDDENUSER\Documents\Custom%20Office%20Templates\Thesis.dotx")

        Dim report As HiddenMetadataReport = HiddenMetadataService.Inspect(target)

        AssertFinding(report, "Template", "C:\Users\HIDDENUSER\Documents\Custom Office Templates\Thesis.dotx", False)
        Assert.AreEqual("Template: C:\Users\HIDDENUSER\Documents\Custom Office Templates\Thesis.dotx", report.Summary())

        Dim bare As String = Path.Combine(_root, "bare-template.docx")
        WriteTemplateDocx(bare, "Thesis.dotx")
        Assert.AreEqual("None found", HiddenMetadataService.Inspect(bare).Summary(), "A template name without a folder says nothing.")
    End Sub


    <TestMethod>
    Public Sub Xlsx_ReportsCellNoteAuthorsAndThreadedCommentPeople()
        Dim target As String = Path.Combine(_root, "data.xlsx")
        WritePackage(target,
            ("[Content_Types].xml", ContentTypesXml),
            ("xl/workbook.xml", "<workbook xmlns=""http://schemas.openxmlformats.org/spreadsheetml/2006/main""/>"),
            ("xl/comments1.xml",
                "<?xml version=""1.0"" encoding=""UTF-8""?><comments xmlns=""http://schemas.openxmlformats.org/spreadsheetml/2006/main"">" &
                "<authors><author>HIDDEN Cell Note Author</author><author>tc={6C4E0D7A-1B2C-4D5E-8F90-123456789ABC}</author></authors>" &
                "<commentList><comment ref=""A1"" authorId=""0""><text><t>Check this value.</t></text></comment></commentList></comments>"),
            ("xl/persons/person.xml",
                "<?xml version=""1.0"" encoding=""UTF-8""?><personList xmlns=""http://schemas.microsoft.com/office/spreadsheetml/2018/threadedcomments"">" &
                "<person displayName=""HIDDEN Thread Person"" id=""{11111111-2222-3333-4444-555555555555}"" userId=""hidden.thread@example.org"" providerId=""AD""/></personList>"))

        Dim report As HiddenMetadataReport = HiddenMetadataService.Inspect(target)

        Assert.AreEqual(HiddenMetadataState.Checked, report.State)
        AssertValues(report, "Comment authors", "HIDDEN Cell Note Author", "HIDDEN Thread Person", "hidden.thread@example.org")
        Assert.IsFalse(report.Summary().Contains("tc="), "Excel's stand-in for a threaded comment names no one.")
    End Sub


    <TestMethod>
    Public Sub Pptx_ReportsCommentAuthors()
        Dim target As String = Path.Combine(_root, "talk.pptx")
        WritePackage(target,
            ("[Content_Types].xml", ContentTypesXml),
            ("ppt/presentation.xml", "<p:presentation xmlns:p=""http://schemas.openxmlformats.org/presentationml/2006/main""/>"),
            ("ppt/commentAuthors.xml",
                "<?xml version=""1.0"" encoding=""UTF-8""?><p:cmAuthorLst xmlns:p=""http://schemas.openxmlformats.org/presentationml/2006/main"">" &
                "<p:cmAuthor id=""1"" name=""HIDDEN Slide Commenter"" initials=""HS"" lastIdx=""1"" clrIdx=""0""><p:extLst><p:ext uri=""{19B8F6BF-5375-455C-9EA6-DF929625EA0E}"">" &
                "<p15:presenceInfo xmlns:p15=""http://schemas.microsoft.com/office/powerpoint/2012/main"" userId=""S::hidden.slide@example.org::abc"" providerId=""AD""/>" &
                "</p:ext></p:extLst></p:cmAuthor></p:cmAuthorLst>"),
            ("ppt/authors.xml",
                "<?xml version=""1.0"" encoding=""UTF-8""?><p188:authorLst xmlns:p188=""http://schemas.microsoft.com/office/powerpoint/2018/8/main"">" &
                "<p188:author id=""{22222222-3333-4444-5555-666666666666}"" name=""HIDDEN Modern Commenter"" initials=""HM"" userId=""hidden.modern@example.org"" providerId=""AD""/></p188:authorLst>"))

        Dim report As HiddenMetadataReport = HiddenMetadataService.Inspect(target)

        Assert.AreEqual(HiddenMetadataState.Checked, report.State)
        AssertValues(report, "Comment authors", "HIDDEN Slide Commenter", "hidden.slide@example.org", "HIDDEN Modern Commenter", "hidden.modern@example.org")
    End Sub


    <TestMethod>
    Public Sub Odt_ReportsCommentAndTrackedChangeAuthors()
        ' "Apply user data" off: meta.xml names no one, but comments and
        ' tracked changes still carry their authors.
        Dim target As String = Path.Combine(_root, "methods.odt")
        Dim namespaces As String =
            "xmlns:office=""urn:oasis:names:tc:opendocument:xmlns:office:1.0"" xmlns:text=""urn:oasis:names:tc:opendocument:xmlns:text:1.0"" " &
            "xmlns:style=""urn:oasis:names:tc:opendocument:xmlns:style:1.0"" xmlns:dc=""http://purl.org/dc/elements/1.1/"" " &
            "xmlns:meta=""urn:oasis:names:tc:opendocument:xmlns:meta:1.0"""
        WritePackage(target,
            ("mimetype", "application/vnd.oasis.opendocument.text"),
            ("meta.xml", "<?xml version=""1.0"" encoding=""UTF-8""?><office:document-meta " & namespaces & "><office:meta><meta:generator>Synthetic Writer</meta:generator></office:meta></office:document-meta>"),
            ("content.xml",
                "<?xml version=""1.0"" encoding=""UTF-8""?><office:document-content " & namespaces & "><office:body><office:text>" &
                "<text:tracked-changes><text:changed-region text:id=""ct1""><text:insertion><office:change-info>" &
                "<dc:creator>HIDDEN Odt Tracker</dc:creator><dc:date>2026-01-01T00:00:00</dc:date></office:change-info></text:insertion></text:changed-region></text:tracked-changes>" &
                "<text:p>Methods<office:annotation><dc:creator>HIDDEN Odt Commenter</dc:creator><dc:date>2026-01-01T00:00:00</dc:date><text:p>A comment.</text:p></office:annotation></text:p>" &
                "</office:text></office:body></office:document-content>"),
            ("styles.xml",
                "<?xml version=""1.0"" encoding=""UTF-8""?><office:document-styles " & namespaces & "><office:master-styles><style:master-page style:name=""Standard"">" &
                "<style:header><text:p><office:annotation><dc:creator>HIDDEN Header Commenter</dc:creator><text:p>In the header.</text:p></office:annotation></text:p></style:header>" &
                "</style:master-page></office:master-styles></office:document-styles>"))

        Dim report As HiddenMetadataReport = HiddenMetadataService.Inspect(target)

        Assert.AreEqual(HiddenMetadataState.Checked, report.State)
        AssertValues(report, "Tracked changes by", "HIDDEN Odt Tracker")
        AssertValues(report, "Comment authors", "HIDDEN Odt Commenter", "HIDDEN Header Commenter")
        Assert.IsFalse(report.Findings.Any(Function(item) item.Label = "Author" OrElse item.Label = "Last saved by"))
    End Sub


    <TestMethod>
    Public Sub Docx_PartLargerThan32MillionCharactersKeepsWhatWasRead()
        ' Big tables: about 36 million characters of body, read in full at
        ' the default limit, and cut off at a smaller one.
        Dim target As String = Path.Combine(_root, "large-tables.docx")
        Using output As New FileStream(target, FileMode.Create, FileAccess.Write, FileShare.None)
            Using archive As New ZipArchive(output, ZipArchiveMode.Create)
                AddEntry(archive, "[Content_Types].xml", ContentTypesXml)
                AddEntry(archive, "docProps/core.xml", CoreXml("HIDDEN Large Creator"))
                Dim body As ZipArchiveEntry = archive.CreateEntry("word/document.xml", CompressionLevel.Fastest)
                Using writer As New StreamWriter(body.Open(), New UTF8Encoding(False))
                    writer.Write("<?xml version=""1.0"" encoding=""UTF-8""?><w:document xmlns:w=""" & WordNamespace & """><w:body>")
                    Dim chunk As String = Repeat("<w:p><w:r><w:t>Row of a large table.</w:t></w:r></w:p>", 1024 * 1024)
                    Dim written As Long = 0
                    Do While written < 36_000_000
                        writer.Write(chunk)
                        written += chunk.Length
                    Loop
                    writer.Write("</w:body></w:document>")
                End Using
            End Using
        End Using

        Dim whole As HiddenMetadataReport = HiddenMetadataService.Inspect(target)
        Assert.AreEqual(HiddenMetadataState.Checked, whole.State, whole.Summary())
        Assert.IsFalse(whole.ReachedLimit)
        Assert.AreEqual(HiddenMetadataGap.None, whole.Gap)
        Assert.AreEqual("Author: HIDDEN Large Creator", whole.Summary())

        Dim cut As HiddenMetadataReport = HiddenMetadataService.Inspect(target, byteLimit:=8L * 1024 * 1024)
        Assert.AreEqual(HiddenMetadataState.Checked, cut.State)
        Assert.IsTrue(cut.ReachedLimit)
        Assert.AreEqual("Author: HIDDEN Large Creator (checked part of this large file)", cut.Summary())
    End Sub


    <TestMethod>
    Public Sub DamagedPart_KeepsWhatWasReadAndSaysTheRestWasNotChecked()
        Dim target As String = Path.Combine(_root, "damaged-comments.docx")
        Dim brokenComments As String =
            "<?xml version=""1.0"" encoding=""UTF-8""?><w:comments xmlns:w=""" & WordNamespace & """>" &
            "<w:comment w:id=""0"" w:author=""HIDDEN Early Commenter""><w:p/></w:comment><w:comment w:id=""1"" w:author=""unfinished"
        WritePackage(target,
            ("[Content_Types].xml", ContentTypesXml),
            ("docProps/core.xml", CoreXml("HIDDEN Kept Creator")),
            ("word/document.xml", WordPart("document", "<w:body/>")),
            ("word/comments.xml", brokenComments))

        Dim report As HiddenMetadataReport = HiddenMetadataService.Inspect(target)

        Assert.AreEqual(HiddenMetadataState.Checked, report.State)
        Assert.AreEqual(HiddenMetadataGap.PartUnreadable, report.Gap)
        Assert.IsTrue(report.HasFindings)
        Assert.AreEqual(
            "Author: HIDDEN Kept Creator; Comment authors: HIDDEN Early Commenter; part of this file couldn't be checked",
            report.Summary())

        ' Nothing found and a part unread: never "None found".
        Dim nothingElse As String = Path.Combine(_root, "only-damaged.docx")
        WritePackage(nothingElse,
            ("[Content_Types].xml", ContentTypesXml),
            ("docProps/core.xml", CoreXml("")),
            ("word/comments.xml", brokenComments.Replace("HIDDEN Early Commenter", "")))
        Assert.AreEqual("Couldn't be checked", HiddenMetadataService.Inspect(nothingElse).Summary())

        ' A DTD is never processed, so an entity can't put a name in.
        Dim withDtd As String = Path.Combine(_root, "dtd.docx")
        WritePackage(withDtd,
            ("[Content_Types].xml", ContentTypesXml),
            ("docProps/core.xml",
                "<?xml version=""1.0""?><!DOCTYPE c [<!ENTITY who ""HIDDEN Entity"">]><cp:coreProperties xmlns:cp=""http://schemas.openxmlformats.org/package/2006/metadata/core-properties"" " &
                "xmlns:dc=""http://purl.org/dc/elements/1.1/""><dc:creator>&who;</dc:creator></cp:coreProperties>"))
        Dim dtdReport As HiddenMetadataReport = HiddenMetadataService.Inspect(withDtd)
        Assert.AreEqual(HiddenMetadataState.CouldNotCheck, dtdReport.State)
        Assert.IsFalse(dtdReport.Summary().Contains("HIDDEN Entity"))

        ' A photo damaged after its Exif block keeps what the block said.
        Dim photo As Byte() = BuildJpeg("HIDDEN-MAKE", "Model X", True, "HIDDEN Photographer")
        Dim damagedPhoto As String = Path.Combine(_root, "damaged.jpg")
        File.WriteAllBytes(damagedPhoto, photo.Take(photo.Length - 2).Concat(New Byte() {&HFF, &HE0, 0, &H50}).ToArray())
        Dim photoReport As HiddenMetadataReport = HiddenMetadataService.Inspect(damagedPhoto)
        Assert.AreEqual(HiddenMetadataState.Checked, photoReport.State)
        AssertFinding(photoReport, "Author", "HIDDEN Photographer", True)
        AssertFinding(photoReport, "Location", "recorded", False)
        StringAssert.EndsWith(photoReport.Summary(), "; part of this file couldn't be checked")
    End Sub


    <TestMethod>
    Public Sub Png_CompressedAuthorThatWontInflate_IsNotNoneFound()
        Dim target As String = Path.Combine(_root, "broken-author.png")
        Dim bytes As New List(Of Byte) From {&H89, &H50, &H4E, &H47, &HD, &HA, &H1A, &HA}
        AddPngChunk(bytes, "IHDR", New Byte() {0, 0, 0, 1, 0, 0, 0, 1, 8, 2, 0, 0, 0})
        ' "Author", NUL, method 0, then a zlib header and a block of a type that doesn't exist.
        AddPngChunk(bytes, "zTXt", Encoding.Latin1.GetBytes("Author").Concat(New Byte() {0, 0, &H78, &H9C, &HFF, &HFF, &HFF, 0}).ToArray())
        AddPngChunk(bytes, "IEND", Array.Empty(Of Byte)())
        File.WriteAllBytes(target, bytes.ToArray())

        Dim report As HiddenMetadataReport = HiddenMetadataService.Inspect(target)

        Assert.AreEqual(HiddenMetadataState.CouldNotCheck, report.State)
        Assert.AreEqual("Couldn't be checked", report.Summary())
    End Sub


    <TestMethod>
    Public Sub Pdf_ReadsAnAuthorGivenByReference()
        Dim target As String = Path.Combine(_root, "indirect.pdf")
        WriteRawPdf(target,
            "1 0 obj" & vbLf & "<< /Title (Synthetic) /Author 4 0 R >>" & vbLf & "endobj" & vbLf &
            "4 0 obj" & vbLf & "(HIDDEN Indirect Author)" & vbLf & "endobj" & vbLf,
            "/Info 1 0 R")

        Dim report As HiddenMetadataReport = HiddenMetadataService.Inspect(target)

        Assert.AreEqual(HiddenMetadataState.Checked, report.State)
        Assert.AreEqual(HiddenMetadataGap.None, report.Gap)
        Assert.AreEqual("Author: HIDDEN Indirect Author", report.Summary())

        ' A reference that leads nowhere is an author that wasn't checked.
        Dim nowhere As String = Path.Combine(_root, "indirect-missing.pdf")
        WriteRawPdf(nowhere, "1 0 obj" & vbLf & "<< /Author 9 0 R >>" & vbLf & "endobj" & vbLf, "/Info 1 0 R")
        Dim nowhereReport As HiddenMetadataReport = HiddenMetadataService.Inspect(nowhere)
        Assert.AreEqual(HiddenMetadataState.CouldNotCheck, nowhereReport.State)
        Assert.AreEqual("Couldn't be checked", nowhereReport.Summary())
    End Sub


    <TestMethod>
    Public Sub Pdf_EncryptedFile_IsNeverReadAsNames()
        ' "Restrict editing": opens without a password, but every Info string
        ' is RC4 ciphertext.
        Dim ciphertext As String = Encoding.Latin1.GetString(New Byte() {&H93, &HA7, &H1C, &HE4, &H5B, &H22, &HF0, &H8D, &H61, &HC9})
        Dim encryptDictionary As String =
            "2 0 obj" & vbLf & "<< /Filter /Standard /V 2 /R 3 /Length 128 /O <0011223344556677> /U <8899AABBCCDDEEFF> /P -3904 >>" & vbLf & "endobj" & vbLf
        Dim target As String = Path.Combine(_root, "restricted.pdf")
        WriteRawPdf(target,
            "1 0 obj" & vbLf & "<< /Author (" & ciphertext & ") /Title <8A3F11> >>" & vbLf & "endobj" & vbLf & encryptDictionary,
            "/Info 1 0 R /Encrypt 2 0 R /ID [<0123><0123>]")

        Dim report As HiddenMetadataReport = HiddenMetadataService.Inspect(target)

        Assert.AreEqual(HiddenMetadataState.CouldNotCheck, report.State)
        Assert.AreEqual(HiddenMetadataGap.Encrypted, report.Gap)
        Assert.AreEqual(0, report.Findings.Count, "Ciphertext is never shown as a name.")
        Assert.AreEqual("Couldn't be checked (encrypted)", report.Summary())

        ' Metadata left unencrypted is still read, and the rest is reported as unread.
        Dim xmp As String =
            "<x:xmpmeta xmlns:x=""adobe:ns:meta/""><rdf:RDF xmlns:rdf=""http://www.w3.org/1999/02/22-rdf-syntax-ns#""><rdf:Description rdf:about="""" xmlns:dc=""http://purl.org/dc/elements/1.1/"">" &
            "<dc:creator><rdf:Seq><rdf:li>HIDDEN Open XMP</rdf:li></rdf:Seq></dc:creator></rdf:Description></rdf:RDF></x:xmpmeta>"
        Dim openMetadata As String = Path.Combine(_root, "restricted-open-metadata.pdf")
        WriteRawPdf(openMetadata,
            "1 0 obj" & vbLf & "<< /Author (" & ciphertext & ") >>" & vbLf & "endobj" & vbLf &
            encryptDictionary.Replace("/P -3904", "/P -3904 /EncryptMetadata false") &
            "3 0 obj" & vbLf & "<< /Type /Metadata /Subtype /XML /Length " & xmp.Length.ToString() & " >>" & vbLf & "stream" & vbLf & xmp & vbLf & "endstream" & vbLf & "endobj" & vbLf,
            "/Info 1 0 R /Encrypt 2 0 R")
        Dim openReport As HiddenMetadataReport = HiddenMetadataService.Inspect(openMetadata)
        Assert.AreEqual(HiddenMetadataState.Checked, openReport.State)
        AssertFinding(openReport, "Author", "HIDDEN Open XMP", True)
        Assert.AreEqual("Author: HIDDEN Open XMP; the rest couldn't be checked (encrypted)", openReport.Summary())

        ' Text about encryption isn't encryption.
        Dim plain As String = Path.Combine(_root, "about-encryption.pdf")
        WriteRawPdf(plain,
            "1 0 obj" & vbLf & "<< /Subject (On /Encrypt and /EncryptMetadata) /Author (HIDDEN Plain Author) >>" & vbLf & "endobj" & vbLf,
            "/Info 1 0 R")
        Assert.AreEqual("Author: HIDDEN Plain Author", HiddenMetadataService.Inspect(plain).Summary())
    End Sub


    <TestMethod>
    Public Sub Pdf_CraftedFiles_FinishQuicklyAndStopWhenCancelled()
        ' Each of these took minutes to hours when every "stream" searched
        ' to the end for "endstream", every "/Author(" walked 64 KB, or every
        ' "stream" scanned 16 KB back for its dictionary.
        Dim crafted As New Dictionary(Of String, String) From {
            {"no-endstream.pdf", Repeat("1 0 obj<<>>stream" & vbLf & New String("e"c, 64), 4 * 1024 * 1024)},
            {"unclosed-literals.pdf", Repeat("/Author(", 4 * 1024 * 1024)},
            {"stray-stream-keywords.pdf", Repeat("stream" & vbLf, 8 * 1024 * 1024)},
            {"unclosed-referenced-strings.pdf", "/Author 1 0 R" & vbLf & Repeat("1 0 obj (", 4 * 1024 * 1024)}
        }

        Dim slow As New List(Of String)()
        For Each item As KeyValuePair(Of String, String) In crafted
            Dim target As String = Path.Combine(_root, item.Key)
            File.WriteAllBytes(target, Encoding.Latin1.GetBytes("%PDF-1.7" & vbLf & item.Value & vbLf & "%%EOF" & vbLf))
            Using cancel As New CancellationTokenSource()
                Dim check As Task(Of HiddenMetadataReport) = Task.Run(Function() HiddenMetadataService.Inspect(target, cancellationToken:=cancel.Token))
                If check.Wait(TimeSpan.FromSeconds(20)) Then
                    Assert.IsFalse(String.IsNullOrEmpty(check.Result.Summary()), item.Key)
                Else
                    slow.Add(item.Key)
                End If
                cancel.Cancel()
            End Using
        Next
        Assert.AreEqual(0, slow.Count, "Still being checked after 20 seconds: " & String.Join(", ", slow))

        ' Closing the export dialog cancels the check, which then stops.
        Dim large As String = Path.Combine(_root, "cancelled.pdf")
        File.WriteAllBytes(large, Encoding.Latin1.GetBytes("%PDF-1.7" & vbLf & Repeat("/Author(", 16 * 1024 * 1024) & vbLf & "%%EOF" & vbLf))
        Using cancel As New CancellationTokenSource()
            Dim check As Task(Of HiddenMetadataReport) = Task.Run(Function() HiddenMetadataService.Inspect(large, cancellationToken:=cancel.Token))
            cancel.CancelAfter(50)
            Dim ended As Boolean
            Try
                ended = check.Wait(TimeSpan.FromSeconds(20))
            Catch ex As AggregateException When TypeOf ex.InnerException Is OperationCanceledException
                ended = True
            End Try
            Assert.IsTrue(ended, "A cancelled check stops.")
        End Using
    End Sub


    Private Const ContentTypesXml As String =
        "<?xml version=""1.0"" encoding=""UTF-8""?><Types xmlns=""http://schemas.openxmlformats.org/package/2006/content-types"">" &
        "<Default Extension=""xml"" ContentType=""application/xml""/></Types>"

    Private Const WordNamespace As String = "http://schemas.openxmlformats.org/wordprocessingml/2006/main"


    Private Shared Function CoreXml(creator As String) As String
        Return "<?xml version=""1.0"" encoding=""UTF-8""?><cp:coreProperties xmlns:cp=""http://schemas.openxmlformats.org/package/2006/metadata/core-properties"" " &
            "xmlns:dc=""http://purl.org/dc/elements/1.1/""><dc:title>Synthetic</dc:title>" &
            If(creator.Length > 0, "<dc:creator>" & creator & "</dc:creator>", String.Empty) &
            "</cp:coreProperties>"
    End Function


    Private Shared Function WordPart(rootName As String, inner As String) As String
        Return "<?xml version=""1.0"" encoding=""UTF-8""?><w:" & rootName & " xmlns:w=""" & WordNamespace & """>" & inner & "</w:" & rootName & ">"
    End Function


    Private Shared Sub WriteTemplateDocx(target As String, templateTarget As String)
        WritePackage(target,
            ("[Content_Types].xml", ContentTypesXml),
            ("docProps/app.xml", "<?xml version=""1.0"" encoding=""UTF-8""?><Properties xmlns=""http://schemas.openxmlformats.org/officeDocument/2006/extended-properties""><Template>Thesis.dotx</Template></Properties>"),
            ("word/document.xml", WordPart("document", "<w:body/>")),
            ("word/settings.xml", WordPart("settings", "<w:attachedTemplate xmlns:r=""http://schemas.openxmlformats.org/officeDocument/2006/relationships"" r:id=""rId1""/>")),
            ("word/_rels/settings.xml.rels",
                "<?xml version=""1.0"" encoding=""UTF-8""?><Relationships xmlns=""http://schemas.openxmlformats.org/package/2006/relationships"">" &
                "<Relationship Id=""rId1"" Type=""http://schemas.openxmlformats.org/officeDocument/2006/relationships/attachedTemplate"" " &
                "Target=""" & templateTarget & """ TargetMode=""External""/></Relationships>"))
    End Sub


    Private Shared Sub WritePackage(target As String, ParamArray parts() As (Name As String, Content As String))
        Using output As New FileStream(target, FileMode.Create, FileAccess.Write, FileShare.None)
            Using archive As New ZipArchive(output, ZipArchiveMode.Create)
                For Each part As (Name As String, Content As String) In parts
                    AddEntry(archive, part.Name, part.Content)
                Next
            End Using
        End Using
    End Sub


    Private Shared Sub AddEntry(archive As ZipArchive, entryName As String, content As String)
        Dim entry As ZipArchiveEntry = archive.CreateEntry(entryName, CompressionLevel.Optimal)
        Using target As Stream = entry.Open()
            Dim bytes As Byte() = New UTF8Encoding(False).GetBytes(content)
            target.Write(bytes, 0, bytes.Length)
        End Using
    End Sub


    ' A PDF from its objects, written as Latin-1 so each character is one byte.
    Private Shared Sub WriteRawPdf(target As String, objects As String, trailerEntries As String)
        File.WriteAllBytes(target, Encoding.Latin1.GetBytes(
            "%PDF-1.7" & vbLf & objects &
            "trailer" & vbLf & "<< /Size 10 " & trailerEntries & " >>" & vbLf & "startxref" & vbLf & "0" & vbLf & "%%EOF" & vbLf))
    End Sub


    ' A PNG chunk; the inspector doesn't check CRCs.
    Private Shared Sub AddPngChunk(output As List(Of Byte), chunkType As String, data As Byte())
        Dim length As Integer = data.Length
        output.AddRange(New Byte() {CByte((length >> 24) And &HFF), CByte((length >> 16) And &HFF), CByte((length >> 8) And &HFF), CByte(length And &HFF)})
        output.AddRange(Encoding.ASCII.GetBytes(chunkType))
        output.AddRange(data)
        output.AddRange(New Byte() {0, 0, 0, 0})
    End Sub


    ' The unit repeated until the text is at least totalLength characters.
    Private Shared Function Repeat(unit As String, totalLength As Integer) As String
        Dim builder As New StringBuilder(totalLength + unit.Length)
        Do While builder.Length < totalLength
            builder.Append(unit)
        Loop
        Return builder.ToString()
    End Function


    Private Shared Sub AssertValues(report As HiddenMetadataReport, findingLabel As String, ParamArray expected As String())
        Dim finding As HiddenMetadataFinding = report.Findings.SingleOrDefault(Function(item) item.Label = findingLabel)
        Assert.IsNotNull(finding, findingLabel & " was not reported. Summary: " & report.Summary())
        CollectionAssert.AreEquivalent(expected, finding.Value.Split(", "), findingLabel & ": " & finding.Value)
        Assert.IsTrue(finding.NamesPerson, findingLabel)
    End Sub


    Private Shared Sub AssertFinding(report As HiddenMetadataReport, findingLabel As String, value As String, namesPerson As Boolean)
        Dim finding As HiddenMetadataFinding = report.Findings.SingleOrDefault(Function(item) item.Label = findingLabel)
        Assert.IsNotNull(finding, findingLabel & " was not reported. Summary: " & report.Summary())
        Assert.AreEqual(value, finding.Value, findingLabel)
        Assert.AreEqual(namesPerson, finding.NamesPerson, findingLabel)
    End Sub

End Class
