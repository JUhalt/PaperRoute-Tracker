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

' Packet export (#45): one local .zip that is a plain package (files, a
' SHA-256 manifest, a summary page) and an RO-Crate 1.3 crate, written from
' allow-listed fields only, without changing a record or a file.
<TestClass>
<DoNotParallelize>
Public Class PacketExportTests

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
    Public Sub RoleDefaults_AllowListCoversEveryRole()
        Dim expected As New HashSet(Of SubmissionPacketFileRole) From {
            SubmissionPacketFileRole.Manuscript, SubmissionPacketFileRole.BlindedManuscript, SubmissionPacketFileRole.TitlePage,
            SubmissionPacketFileRole.Figure, SubmissionPacketFileRole.Table, SubmissionPacketFileRole.Supplement,
            SubmissionPacketFileRole.Highlights, SubmissionPacketFileRole.GraphicalAbstract, SubmissionPacketFileRole.ReportingChecklist,
            SubmissionPacketFileRole.DataAvailability
        }

        Dim included As New HashSet(Of SubmissionPacketFileRole)()
        For Each role As SubmissionPacketFileRole In [Enum].GetValues(Of SubmissionPacketFileRole)()
            If PacketExportService.IsIncludedByDefault(role) Then
                included.Add(role)
                Assert.AreEqual("", PacketExportService.DefaultExclusionReason(role), role.ToString())
            Else
                Assert.IsTrue(PacketExportService.DefaultExclusionReason(role).Length > 0, role.ToString() & " needs a reason.")
            End If
        Next

        Assert.IsTrue(expected.SetEquals(included), "Only the allow-listed roles start checked: " & String.Join(", ", included))
        Assert.AreEqual("can name the editor or suggested reviewers", PacketExportService.DefaultExclusionReason(SubmissionPacketFileRole.CoverLetter))
        Assert.AreEqual("names reviewers and quotes their comments", PacketExportService.DefaultExclusionReason(SubmissionPacketFileRole.ResponseToReviewers))
        Assert.IsFalse(PacketExportService.IsIncludedByDefault(SubmissionPacketFileRole.Other))
        Dim future As SubmissionPacketFileRole = CType(99, SubmissionPacketFileRole)
        Assert.IsFalse(PacketExportService.IsIncludedByDefault(future), "A role added later starts unchecked.")
        Assert.IsTrue(PacketExportService.DefaultExclusionReason(future).Length > 0)
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
    Public Sub BlindedPacket_HasNoPeopleAndWarnsAboutNames()
        Dim fixture As ExportFixture = BuildExampleFixture(_root)
        Dim main As SubmissionPacketFile = fixture.FileWithRole(SubmissionPacketFileRole.Manuscript)
        main.Role = SubmissionPacketFileRole.BlindedManuscript
        main.OriginalFileName = "Anonymized manuscript.pdf"
        WritePdf(main.LocalFilePath, infoAuthorLiteral:="Josiah Carberry")
        main.Sha256 = String.Empty
        Dim title As SubmissionPacketFile = fixture.FileWithRole(SubmissionPacketFileRole.TitlePage)
        WriteDocx(title.LocalFilePath, "J. Carberry", "Someone Else")
        title.Sha256 = String.Empty
        Dim named As SubmissionPacketFile = AddFile(fixture, SubmissionPacketFileRole.Supplement, "Carberry_main.pdf", BuildPdf(), "Extra analysis")

        Dim plan As PacketExportPlan = PacketExportService.Prepare(fixture.Manuscript, fixture.Packet, fixture.Library)
        Assert.IsTrue(plan.IsBlinded)
        Assert.IsFalse(plan.IncludeAuthors, "An anonymized package names no authors.")
        plan.IncludeAuthors = True
        Assert.IsFalse(plan.IncludeAuthors, "Turning authors on is ignored for an anonymized packet.")
        Assert.AreEqual(2, plan.Authors.Count, "Authors are still known, to check names against.")

        PacketExportService.CheckFiles(plan)
        Dim titleRow As PacketExportRow = RowFor(plan, "Title page")
        Assert.IsFalse(titleRow.Include)
        Assert.AreEqual("Title page.docx starts unchecked: this packet is anonymized and a title page names the authors.", titleRow.Note)
        Assert.IsFalse(RowFor(plan, "Cover letter").Include)
        Assert.IsTrue(plan.BlindedManuscriptNamesPerson(), "The anonymized PDF's Author names a person.")

        ' Every way a family name can reach the package is warned about.
        titleRow.Include = True
        plan.PackageName = "Carberry revision 2"
        plan.RefreshNames()
        Dim warnings As IReadOnlyList(Of String) = plan.AuthorNameWarnings()
        CollectionAssert.Contains(warnings.ToList(), "Check Anonymized manuscript.pdf: its name, label, or hidden information includes " & PacketExportService.Quoted("Carberry") & ".")
        CollectionAssert.Contains(warnings.ToList(), "Check Title page.docx: its name, label, or hidden information includes " & PacketExportService.Quoted("Carberry") & ".")
        CollectionAssert.Contains(warnings.ToList(), "Check Carberry_main.pdf: its name, label, or hidden information includes " & PacketExportService.Quoted("Carberry") & ".")
        CollectionAssert.Contains(warnings.ToList(), "The package name includes " & PacketExportService.Quoted("Carberry") & ". Change it before exporting.")
        Assert.AreEqual(4, warnings.Count, String.Join(" | ", warnings))

        ' Without them, nothing in what PaperRoute writes names an author.
        titleRow.Include = False
        RowFor(plan, "Extra analysis").Include = False
        plan.PackageName = "Anonymized revision 2"
        Dim zipPath As String = OutputPath("blinded.zip")
        Export(plan, zipPath)
        Dim json As String = EntryText(zipPath, "ro-crate-metadata.json")
        Dim html As String = EntryText(zipPath, "ro-crate-preview.html")
        Dim manifest As String = EntryText(zipPath, "manifest-sha256.txt")
        For Each content As String In {json, html, manifest}
            For Each name As String In {"Carberry", "Josiah", "Riley", "Placeholder", "orcid.org", "Brown University", "Fictional Institute"}
                Assert.IsFalse(content.Contains(name, StringComparison.OrdinalIgnoreCase), name & " must not appear in an anonymized package.")
            Next
        Next
        Dim graph As JsonArray = GraphOf(zipPath)
        Assert.IsFalse(graph.Any(Function(node) node("@type").GetValue(Of String)() = "Person" OrElse node("@type").GetValue(Of String)() = "Organization"))
        Assert.IsNull(EntityOf(graph, "#manuscript")("author"))
        CollectionAssert.AreEqual({"Check Anonymized manuscript.pdf: its name, label, or hidden information includes " & PacketExportService.Quoted("Carberry") & "."}, plan.AuthorNameWarnings().ToArray(),
                                  "The anonymized manuscript itself still names an author inside the file.")

        ' A deep look at what PaperRoute wrote: every entry outside files/,
        ' decompressed, and every entry name, in UTF-8 or UTF-16, any case.
        Dim written As List(Of (Entry As String, Bytes As Byte())) = DeepScan(zipPath).
            Where(Function(item) Not item.Entry.StartsWith("files/", StringComparison.Ordinal) OrElse item.Entry.EndsWith(" (name)", StringComparison.Ordinal)).
            ToList()
        For Each marker As String In PersonMarkers()
            Dim found As String = FindMarker(written, marker, ignoreCase:=True)
            Assert.IsNull(found, marker & " is in " & found)
        Next

        ' With the anonymized manuscript cleaned, nothing in the package names
        ' an author, inside the files included.
        WritePdf(main.LocalFilePath)
        PacketExportService.CheckFiles(plan)
        Assert.AreEqual(0, plan.AuthorNameWarnings().Count, String.Join(" | ", plan.AuthorNameWarnings()))
        Dim cleanZip As String = OutputPath("blinded-clean.zip")
        Export(plan, cleanZip)
        Dim everything As List(Of (Entry As String, Bytes As Byte())) = DeepScan(cleanZip)
        Assert.IsTrue(everything.Any(Function(item) item.Entry.StartsWith("files/Supplement A.odt > ", StringComparison.Ordinal)), "The scan looks inside the files.")
        For Each marker As String In PersonMarkers()
            Dim found As String = FindMarker(everything, marker, ignoreCase:=True)
            Assert.IsNull(found, marker & " is in " & found)
        Next
    End Sub


    ' Who the example packet's authors are, in every form a package could
    ' name them.
    Private Shared Function PersonMarkers() As String()
        Return {"Carberry", "Josiah", "Riley", "Placeholder", "orcid.org", ValidOrcid, ValidOrcid.Replace("-", ""),
                "Brown University", "Fictional Institute", "Department of Examples"}
    End Function


    <TestMethod>
    Public Sub BlindedPacket_IdentifiedManuscriptStartsUnchecked()
        Dim fixture As ExportFixture = BuildExampleFixture(_root)
        AddFile(fixture, SubmissionPacketFileRole.BlindedManuscript, "Anonymized manuscript.pdf", BuildPdf(), "Anonymized manuscript")

        Dim plan As PacketExportPlan = PacketExportService.Prepare(fixture.Manuscript, fixture.Packet, fixture.Library)
        PacketExportService.CheckFiles(plan)
        Assert.IsTrue(plan.IsBlinded)

        Dim identified As PacketExportRow = RowFor(plan, "Main text, revision 2")
        Assert.IsTrue(identified.CanInclude)
        Assert.IsFalse(identified.Include, "The manuscript with author details starts unchecked in an anonymized packet.")
        Assert.AreEqual("Main text (revision 2).pdf starts unchecked: this packet is anonymized and, unlike the blinded manuscript, this one may name the authors.", identified.Note)
        Assert.IsTrue(RowFor(plan, "Anonymized manuscript").Include)

        Dim zipPath As String = OutputPath("blinded-defaults.zip")
        Export(plan, zipPath)
        Dim manuscript As JsonObject = EntityOf(GraphOf(zipPath), "#manuscript")
        Assert.AreEqual(PacketExportService.CrateId("Anonymized manuscript.pdf"), manuscript("encoding")("@id").GetValue(Of String)(), "Only the anonymized manuscript is its encoding.")
        Assert.IsNull(EntryBytes(zipPath, "files/Main text (revision 2).pdf"))

        ' Not anonymized: the manuscript is included as usual.
        fixture.Packet.Files.RemoveAll(Function(item) item.Role = SubmissionPacketFileRole.BlindedManuscript)
        Dim plain As PacketExportPlan = PacketExportService.Prepare(fixture.Manuscript, fixture.Packet, fixture.Library)
        PacketExportService.CheckFiles(plain)
        Assert.IsTrue(RowFor(plain, "Main text, revision 2").Include)
        Assert.AreEqual("", RowFor(plain, "Main text, revision 2").Note)
    End Sub


    <TestMethod>
    Public Sub BlindedPacket_ChecksVersionJournalAndCommonNameForms()
        Dim fixture As ExportFixture = BuildExampleFixture(_root)
        Dim main As SubmissionPacketFile = fixture.FileWithRole(SubmissionPacketFileRole.Manuscript)
        main.Role = SubmissionPacketFileRole.BlindedManuscript
        main.OriginalFileName = "Anonymized manuscript.pdf"
        WritePdf(main.LocalFilePath)
        main.Sha256 = String.Empty
        Dim mueller As New AuthorRecord With {.GivenName = "Anna", .FamilyName = "M" & ChrW(&HFC) & "ller"}
        fixture.Library.Authors.Add(mueller)
        fixture.Manuscript.Authors.Add(New ManuscriptAuthor With {.AuthorId = mueller.Id})
        fixture.Version.Label = "Carberry2026 R2"
        fixture.Packet.JournalName = "Bulletin of the Placeholder Society"
        AddFile(fixture, SubmissionPacketFileRole.Supplement, "Carberry2026_supplement.pdf", BuildPdf(), "Author-year supplement")
        AddFile(fixture, SubmissionPacketFileRole.Supplement, "PlaceholderEtAl_data.csv", Encoding.UTF8.GetBytes("a,b" & vbLf), "Camel-case data")
        AddFile(fixture, SubmissionPacketFileRole.Supplement, "Mueller lab notes.txt", Encoding.UTF8.GetBytes("notes" & vbLf), "Spelled-out umlaut")

        Dim plan As PacketExportPlan = PacketExportService.Prepare(fixture.Manuscript, fixture.Packet, fixture.Library)
        PacketExportService.CheckFiles(plan)
        Dim warnings As List(Of String) = plan.AuthorNameWarnings().ToList()

        CollectionAssert.Contains(warnings, "Check Carberry2026_supplement.pdf: its name, label, or hidden information includes " & PacketExportService.Quoted("Carberry") & ".")
        CollectionAssert.Contains(warnings, "Check PlaceholderEtAl_data.csv: its name, label, or hidden information includes " & PacketExportService.Quoted("Placeholder") & ".")
        CollectionAssert.Contains(warnings, "Check Mueller lab notes.txt: its name, label, or hidden information includes " & PacketExportService.Quoted(mueller.FamilyName) & ".")
        CollectionAssert.Contains(warnings, "The version label includes " & PacketExportService.Quoted("Carberry") & ". Change it in Version History before exporting.")
        CollectionAssert.Contains(warnings, "The journal name includes " & PacketExportService.Quoted("Placeholder") & ". Check the packet's journal before exporting.")
        Assert.AreEqual(5, warnings.Count, String.Join(" | ", warnings))

        ' Not anonymized: no name checks at all.
        main.Role = SubmissionPacketFileRole.Manuscript
        Dim plain As PacketExportPlan = PacketExportService.Prepare(fixture.Manuscript, fixture.Packet, fixture.Library)
        PacketExportService.CheckFiles(plain)
        Assert.AreEqual(0, plain.AuthorNameWarnings().Count)
    End Sub


    <TestMethod>
    Public Sub BlindedPacket_WithoutStructuredAuthorsSaysNamesWerentChecked()
        Dim fixture As ExportFixture = SimpleFixture()
        AddFile(fixture, SubmissionPacketFileRole.BlindedManuscript, "Carberry anonymized.pdf", BuildPdf(), "Anonymized manuscript")
        fixture.Manuscript.CoAuthors = "Josiah Carberry, Riley Placeholder"

        Dim plan As PacketExportPlan = PacketExportService.Prepare(fixture.Manuscript, fixture.Packet, fixture.Library)
        PacketExportService.CheckFiles(plan)
        CollectionAssert.AreEqual({PacketExportPlan.NoAuthorsToCheckText}, plan.AuthorNameWarnings().ToArray(),
                                  "With only legacy co-author text, the names couldn't be checked, and the window says so.")
    End Sub


    <TestMethod>
    <DataRow("Carberry_main.pdf", "Carberry", True)>
    <DataRow("Carberry2026_supplement.pdf", "Carberry", True)>
    <DataRow("2026Carberry.pdf", "Carberry", True)>
    <DataRow("CarberryEtAl.docx", "Carberry", True)>
    <DataRow("jCarberry.pdf", "Carberry", True)>
    <DataRow("JCarberry_CV.pdf", "Carberry", True)>
    <DataRow("carberry notes.txt", "Carberry", True)>
    <DataRow("SMITH_2026.pdf", "Smith", True)>
    <DataRow("Carberryville.pdf", "Carberry", False)>
    <DataRow("Mccarberry.pdf", "Carberry", False)>
    <DataRow("Leeds data.csv", "Lee", False)>
    <DataRow("LEEDS.csv", "Lee", False)>
    <DataRow("Garcia_review.pdf", "García", True)>
    <DataRow("Muller lab.docx", "Müller", True)>
    <DataRow("Mueller lab.docx", "Müller", True)>
    <DataRow("MÜLLER.pdf", "Mueller", True)>
    <DataRow("Sorensen 2026.pdf", "Sørensen", True)>
    <DataRow("", "Carberry", False)>
    Public Sub ContainsName_FindsAuthorYearCamelCaseAndAccents(value As String, name As String, expected As Boolean)
        Assert.AreEqual(expected, PacketExportService.ContainsName(value, name), value & " / " & name)
    End Sub


    <TestMethod>
    <DataRow("C:\Users\x\Figure 1.png", "Figure 1.png")>
    <DataRow("a/b/c.pdf", "c.pdf")>
    <DataRow("..\evil.pdf", "evil.pdf")>
    <DataRow("../../evil.pdf", "evil.pdf")>
    <DataRow("a:b*c?.pdf", "a_b_c_.pdf")>
    <DataRow("q""u<o>t|e.txt", "q_u_o_t_e.txt")>
    <DataRow("report. . ", "report")>
    <DataRow("   leading.pdf", "leading.pdf")>
    <DataRow(".", "Figure.png")>
    <DataRow("..", "Figure.png")>
    <DataRow("", "Figure.png")>
    <DataRow("dir\", "Figure.png")>
    <DataRow("CON.txt", "_CON.txt")>
    <DataRow("con", "_con")>
    <DataRow("com1", "_com1")>
    <DataRow("NUL .txt", "_NUL.txt")>
    <DataRow("LPT9.tar.gz", "_LPT9.tar.gz")>
    <DataRow("COM10.txt", "COM10.txt")>
    <DataRow("CONSOLE.txt", "CONSOLE.txt")>
    <DataRow("Table #1 (50% sample).csv", "Table #1 (50% sample).csv")>
    Public Sub FileNames_AreCleaned(requested As String, expected As String)
        Assert.AreEqual(expected, PacketExportService.CleanFileName(requested, "Figure", ".png"))
    End Sub


    <TestMethod>
    Public Sub FileNames_HandleControlCharactersLengthAndMissingOriginals()
        Assert.AreEqual("bell_.txt", PacketExportService.CleanFileName("bell" & ChrW(7) & ".txt", "Other", ""))
        Assert.AreEqual("del_.txt", PacketExportService.CleanFileName("del" & ChrW(&H7F) & ".txt", "Other", ""))
        Assert.AreEqual("tab_name.txt", PacketExportService.CleanFileName("tab" & vbTab & "name.txt", "Other", ""))

        Dim longName As String = PacketExportService.CleanFileName(New String("a"c, 150) & ".pdf", "Other", "")
        Assert.AreEqual(New String("a"c, 100) & ".pdf", longName, "The stem is capped at 100 characters.")
        Dim cutAtSpace As String = PacketExportService.CleanFileName(New String("b"c, 99) & "   tail.pdf", "Other", "")
        Assert.AreEqual(New String("b"c, 99) & ".pdf", cutAtSpace, "A capped stem loses its trailing space.")

        Dim unnamed As New SubmissionPacketFile With {
            .Role = SubmissionPacketFileRole.Manuscript, .Label = "Main text",
            .LocalFilePath = "C:\Users\SECRETUSER\store\main_SECRETSTORE1.pdf", .StorageMode = SubmissionPacketFileStorageMode.ManagedCopy
        }
        Assert.AreEqual("Main text.pdf", PacketExportService.DefaultRequestedName(unnamed), "The label and the stored file's extension, never its path.")
        unnamed.Label = ""
        Assert.AreEqual("Manuscript.pdf", PacketExportService.DefaultRequestedName(unnamed))
        unnamed.OriginalFileName = "C:\Users\SECRETUSER\Desktop\Draft (final).docx"
        Assert.AreEqual("Draft (final).docx", PacketExportService.DefaultRequestedName(unnamed))
    End Sub


    <TestMethod>
    Public Sub FileNames_ReservedNamesAreCheckedAfterCapping()
        ' Capping the stem, then trimming its spaces, leaves a bare "CON".
        Dim padded As String = "CON" & New String(" "c, 97) & "draft.pdf"
        Assert.AreEqual("_CON.pdf", PacketExportService.CleanFileName(padded, "Figure", ".png"))
        Assert.AreEqual("_nul.txt", PacketExportService.CleanFileName("nul" & New String(" "c, 120) & "x.txt", "Figure", ".png"))
        Assert.AreEqual("_LPT1", PacketExportService.CleanFileName("LPT1" & New String(" "c, 99) & "tail", "Figure", ""))

        ' A reserved name at the cap keeps within it.
        Dim longReserved As String = PacketExportService.CleanFileName("AUX." & New String("a"c, 120) & ".pdf", "Figure", "")
        Assert.AreEqual("_AUX." & New String("a"c, 95) & ".pdf", longReserved)

        Dim fixture As ExportFixture = SimpleFixture()
        Dim plan As PacketExportPlan = PacketExportService.Prepare(fixture.Manuscript, fixture.Packet, fixture.Library)
        plan.PackageName = "CON" & New String(" "c, 97) & "x"
        Assert.AreEqual("_CON.zip", plan.DefaultFileName())
    End Sub


    <TestMethod>
    Public Sub FileNames_AreUniqueIgnoringCase()
        CollectionAssert.AreEqual(
            {"Figure 1.png", "figure 1 (2).PNG", "Figure 1 (3).png"},
            PacketExportService.UniqueNames({"Figure 1.png", "figure 1.PNG", "Figure 1.png"}).ToArray())
        CollectionAssert.AreEqual(
            {"notes", "NOTES (2)", "notes.txt"},
            PacketExportService.UniqueNames({"notes", "NOTES", "notes.txt"}).ToArray())
        Dim longStem As String = New String("z"c, 100) & ".pdf"
        Dim numbered As List(Of String) = PacketExportService.UniqueNames({longStem, longStem})
        Assert.AreEqual(New String("z"c, 96) & " (2).pdf", numbered(1), "A numbered stem stays within 100 characters.")

        Dim fixture As ExportFixture = SimpleFixture()
        AddFile(fixture, SubmissionPacketFileRole.Figure, "Figure 1.png", BuildPng(shade:=1), "Figure 1a")
        AddFile(fixture, SubmissionPacketFileRole.Figure, "figure 1.PNG", BuildPng(shade:=2), "Figure 1b")
        AddFile(fixture, SubmissionPacketFileRole.Figure, "Figure 1.png", BuildPng(shade:=3), "Figure 1c")
        Dim plan As PacketExportPlan = PacketExportService.Prepare(fixture.Manuscript, fixture.Packet, fixture.Library)
        PacketExportService.CheckFiles(plan)

        CollectionAssert.AreEqual({"Figure 1.png", "figure 1 (2).PNG", "Figure 1 (3).png"}, plan.Rows.Select(Function(item) item.OutputName).ToArray())

        plan.Rows(0).Include = False
        plan.RefreshNames()
        CollectionAssert.AreEqual({"Figure 1.png", "figure 1.PNG", "Figure 1 (2).png"}, plan.Rows.Select(Function(item) item.OutputName).ToArray(),
                                  "A file left out reserves no name.")

        plan.Rows(1).RequestedName = "..\a:b?"
        plan.RefreshNames()
        Assert.AreEqual("a_b_.PNG", plan.Rows(1).OutputName, "An edited name is cleaned and gets its extension back.")
        plan.Rows(2).RequestedName = "A_B_.png"
        plan.RefreshNames()
        Assert.AreEqual("A_B_ (2).png", plan.Rows(2).OutputName)
    End Sub


    <TestMethod>
    Public Sub CrateIds_RoundTripToZipEntryNames()
        Dim names As String() = {"Table #1 (50% sample).csv", "Main text (revision 2).pdf", "Müller résumé.pdf", "a+b=c&d.txt", "100%.txt"}
        For Each name As String In names
            Dim id As String = PacketExportService.CrateId(name)
            StringAssert.StartsWith(id, "files/")
            Assert.IsFalse(id.Contains(" "c), id)
            Assert.IsFalse(id.Contains("#"c), id)
            Assert.IsFalse(Regex.IsMatch(id, "%(?![0-9A-F]{2})"), "No bare percent sign: " & id)
            Assert.IsTrue(Regex.IsMatch(id.Substring(6), "^[A-Za-z0-9\-._~%]+$"), "Only unreserved characters and escapes: " & id)
            Assert.IsTrue(String.Equals(name, Uri.UnescapeDataString(id.Substring(6)), StringComparison.Ordinal), id)
        Next

        Dim fixture As ExportFixture = SimpleFixture()
        For Each name As String In names
            AddFile(fixture, SubmissionPacketFileRole.Supplement, name, Encoding.UTF8.GetBytes(name), name)
        Next
        Dim plan As PacketExportPlan = PacketExportService.Prepare(fixture.Manuscript, fixture.Packet, fixture.Library)
        PacketExportService.CheckFiles(plan)
        Dim zipPath As String = OutputPath("ids.zip")
        Export(plan, zipPath)

        Dim fileEntries As List(Of String) = EntryNames(zipPath).Where(Function(item) item.StartsWith("files/", StringComparison.Ordinal)).ToList()
        Dim parts As List(Of String) = RefIds(EntityOf(GraphOf(zipPath), "./")("hasPart"))
        Assert.AreEqual(names.Length, fileEntries.Count)
        Assert.AreEqual(fileEntries.Count, parts.Count)
        For Each id As String In parts
            Dim entryName As String = "files/" & Uri.UnescapeDataString(id.Substring(6))
            Assert.IsTrue(fileEntries.Any(Function(item) String.Equals(item, entryName, StringComparison.Ordinal)), id & " names no entry.")
        Next
        For Each entryName As String In fileEntries
            Assert.IsTrue(parts.Any(Function(id) String.Equals("files/" & Uri.UnescapeDataString(id.Substring(6)), entryName, StringComparison.Ordinal)), entryName & " is not in hasPart.")
        Next
    End Sub


    <TestMethod>
    Public Sub Fingerprints_AreInJsonAndHtml()
        Dim fixture As ExportFixture = SimpleFixture()
        Dim unchanged As SubmissionPacketFile = AddFile(fixture, SubmissionPacketFileRole.Manuscript, "Main.pdf", BuildPdf(), "Main text")
        Dim changed As SubmissionPacketFile = AddFile(fixture, SubmissionPacketFileRole.Figure, "Figure.png", BuildPng(shade:=1), "Figure")
        File.WriteAllBytes(changed.LocalFilePath, BuildPng(shade:=99))
        AddFile(fixture, SubmissionPacketFileRole.Table, "Table.csv", Encoding.UTF8.GetBytes("a,b" & vbLf), "Table", record:=False)

        Dim plan As PacketExportPlan = PacketExportService.Prepare(fixture.Manuscript, fixture.Packet, fixture.Library)
        PacketExportService.CheckFiles(plan)
        RowFor(plan, "Figure").Include = True
        Dim zipPath As String = OutputPath("fingerprints.zip")
        Dim result As PacketExportResult = Export(plan, zipPath)
        Assert.AreEqual(3, result.FileCount)
        Assert.AreEqual("fingerprints.zip", result.ZipFileName)

        Dim graph As JsonArray = GraphOf(zipPath)
        Dim expectations As New Dictionary(Of String, (Status As String, Recorded As String)) From {
            {"Main.pdf", ("Unchanged", unchanged.Sha256.ToLowerInvariant())},
            {"Figure.png", ("Changed since recorded", changed.Sha256.ToLowerInvariant())},
            {"Table.csv", ("Not recorded", "")}
        }
        For Each pair In expectations
            Dim entity As JsonObject = EntityOf(graph, PacketExportService.CrateId(pair.Key))
            Dim properties As List(Of String) = RefIds(entity("additionalProperty"))
            Dim fingerprint As JsonObject = EntityOf(graph, properties(0))
            Assert.AreEqual("PropertyValue", fingerprint("@type").GetValue(Of String)())
            Assert.AreEqual("Fingerprint", fingerprint("name").GetValue(Of String)())
            Assert.AreEqual(pair.Value.Status, fingerprint("value").GetValue(Of String)(), pair.Key)
            If pair.Value.Recorded.Length = 0 Then
                Assert.AreEqual(1, properties.Count, "No recorded fingerprint for " & pair.Key)
                Assert.IsTrue(TypeOf entity("additionalProperty") Is JsonObject, "A single reference, not a one-item array.")
            Else
                Assert.AreEqual(2, properties.Count)
                StringAssert.EndsWith(properties(1), "-recorded-sha256")
                Dim recorded As JsonObject = EntityOf(graph, properties(1))
                Assert.AreEqual("Recorded SHA-256", recorded("name").GetValue(Of String)())
                Assert.AreEqual(pair.Value.Recorded, recorded("value").GetValue(Of String)())
            End If
        Next
        Assert.AreNotEqual(changed.Sha256.ToLowerInvariant(), EntityOf(graph, "files/Figure.png")("sha256").GetValue(Of String)(),
                           "sha256 describes the bytes written, not the recorded fingerprint.")

        Dim html As String = EntryText(zipPath, "ro-crate-preview.html")
        StringAssert.Contains(html, ">Unchanged<")
        StringAssert.Contains(html, "Changed since recorded<br>Recorded SHA-256: <code style=""word-break:break-all"">" & changed.Sha256.ToLowerInvariant() & "</code>")
        StringAssert.Contains(html, ">Not recorded<")
    End Sub


    <TestMethod>
    Public Sub Manifest_IsSha256sumFormat()
        Dim fixture As ExportFixture = BuildExampleFixture(_root)
        Dim oldTime As New DateTime(2001, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        For Each item As SubmissionPacketFile In fixture.Packet.Files.Where(Function(candidate) candidate.LocalFilePath.Length > 0)
            File.SetLastWriteTimeUtc(item.LocalFilePath, oldTime)
        Next

        Dim plan As PacketExportPlan = PacketExportService.Prepare(fixture.Manuscript, fixture.Packet, fixture.Library)
        PacketExportService.CheckFiles(plan)
        Dim zipPath As String = OutputPath("manifest.zip")
        Export(plan, zipPath)

        Dim bytes As Byte() = EntryBytes(zipPath, "manifest-sha256.txt")
        Assert.IsFalse(bytes.Length >= 3 AndAlso bytes(0) = &HEF AndAlso bytes(1) = &HBB AndAlso bytes(2) = &HBF, "No byte order mark.")
        Assert.IsFalse(bytes.Contains(CByte(13)), "LF line ends only.")
        Dim lines As String() = Encoding.UTF8.GetString(bytes).Split(ChrW(10))
        Assert.AreEqual("", lines.Last(), "The last line ends with LF.")
        Dim included As IReadOnlyList(Of PacketExportRow) = plan.IncludedRows()
        Assert.AreEqual(included.Count, lines.Length - 1)

        For index As Integer = 0 To included.Count - 1
            Dim line As String = lines(index)
            Assert.IsTrue(Regex.IsMatch(line, "^[0-9a-f]{64}  files/.+$"), line)
            Dim row As PacketExportRow = included(index)
            Dim entryName As String = "files/" & row.OutputName
            Assert.AreEqual(entryName, line.Substring(66))
            Dim entry As Byte() = EntryBytes(zipPath, entryName)
            Dim source As Byte() = File.ReadAllBytes(row.Source.LocalFilePath)
            CollectionAssert.AreEqual(source, entry, "Files are copied byte for byte.")
            Assert.AreEqual(Convert.ToHexString(SHA256.HashData(entry)).ToLowerInvariant(), line.Substring(0, 64))
        Next

        Using archive As ZipArchive = ZipFile.OpenRead(zipPath)
            Dim times As List(Of DateTimeOffset) = archive.Entries.Select(Function(item) item.LastWriteTime).Distinct().ToList()
            Assert.AreEqual(1, times.Count, "Every entry has the export time.")
            Assert.AreNotEqual(2001, times(0).Year, "No source time travels.")
            Assert.IsTrue(Math.Abs((times(0).UtcDateTime - ExportTime).TotalMinutes) < 1, "The entries carry the export time.")
        End Using
    End Sub


    <TestMethod>
    Public Sub Metadata_IsFlattenedCompactRoCrate13()
        Dim fixture As ExportFixture = BuildExampleFixture(_root)
        Dim zipPath As String = ExportWithDefaults(fixture, "crate.zip")

        Dim bytes As Byte() = EntryBytes(zipPath, "ro-crate-metadata.json")
        Assert.IsFalse(bytes(0) = &HEF, "UTF-8 without a byte order mark.")
        Assert.IsFalse(bytes.Contains(CByte(13)), "LF line ends.")
        Dim document As JsonObject = JsonNode.Parse(bytes).AsObject()
        CollectionAssert.AreEquivalent({"@context", "@graph"}, document.Select(Function(item) item.Key).ToArray())
        Assert.AreEqual(PacketExportService.RoCrateContext, document("@context").GetValue(Of String)())
        Dim graph As JsonArray = document("@graph").AsArray()

        Dim descriptor As JsonObject = EntityOf(graph, "ro-crate-metadata.json")
        Assert.AreEqual("CreativeWork", descriptor("@type").GetValue(Of String)())
        Assert.AreEqual(PacketExportService.RoCrateSpec, descriptor("conformsTo")("@id").GetValue(Of String)())
        Assert.AreEqual("./", descriptor("about")("@id").GetValue(Of String)())

        Dim root As JsonObject = EntityOf(graph, "./")
        Assert.AreEqual("Dataset", root("@type").GetValue(Of String)())
        Assert.AreEqual(ExamplePacketLabel, root("name").GetValue(Of String)())
        Assert.AreEqual("2026-10-03", root("datePublished").GetValue(Of String)())
        Assert.AreEqual("#rights-not-stated", root("license")("@id").GetValue(Of String)())
        Assert.AreEqual("#manuscript", root("mainEntity")("@id").GetValue(Of String)())
        CollectionAssert.AreEqual({"#submission", "#export"}, RefIds(root("mentions")).ToArray())
        Dim description As String = root("description").GetValue(Of String)()
        StringAssert.StartsWith(description, "Submission packet for """ & ExampleTitle & """, exported from PaperRoute on 2026-10-03.")
        StringAssert.Contains(description, "The package's date is the day it was exported, not a publication date.")

        ' Flattened and compact: every value is a literal, a lone {"@id"}, or
        ' an array of two or more of them; every reference resolves.
        Dim ids As New HashSet(Of String)(StringComparer.Ordinal)
        Dim references As New List(Of String)()
        For Each node As JsonNode In graph
            Dim entity As JsonObject = node.AsObject()
            Assert.IsTrue(ids.Add(entity("@id").GetValue(Of String)()), "Duplicate @id " & entity("@id").ToJsonString())
            Assert.IsTrue(TypeOf entity("@type") Is JsonValue, "One type per entity.")
            For Each pair In entity
                If pair.Key = "@id" OrElse pair.Key = "@type" Then Continue For
                If TypeOf pair.Value Is JsonArray Then
                    Dim items As JsonArray = pair.Value.AsArray()
                    Assert.IsTrue(items.Count >= 2, entity("@id").ToJsonString() & "." & pair.Key & " is a one-item array.")
                    For Each item As JsonNode In items
                        If TypeOf item Is JsonObject Then references.Add(LoneId(item.AsObject(), pair.Key))
                    Next
                ElseIf TypeOf pair.Value Is JsonObject Then
                    references.Add(LoneId(pair.Value.AsObject(), pair.Key))
                End If
            Next
        Next
        For Each reference As String In references.Where(Function(item) item <> PacketExportService.RoCrateSpec)
            Assert.IsTrue(ids.Contains(reference), reference & " is referenced but not described.")
        Next

        Dim software As JsonObject = EntityOf(graph, PacketExportService.SoftwareUrl)
        Assert.AreEqual("SoftwareApplication", software("@type").GetValue(Of String)())
        Assert.AreEqual("PaperRoute Tracker", software("name").GetValue(Of String)())
        Assert.AreEqual(PacketExportService.SoftwareUrl, software("url").GetValue(Of String)())
        Assert.AreEqual(TestVersion, software("version").GetValue(Of String)())
        Assert.IsNull(software("license"), "The software's license is left out.")

        Dim export As JsonObject = EntityOf(graph, "#export")
        Assert.AreEqual("CreateAction", export("@type").GetValue(Of String)())
        Assert.AreEqual("2026-10-03T16:00:00Z", export("endTime").GetValue(Of String)())
        Assert.AreEqual(PacketExportService.SoftwareUrl, export("instrument")("@id").GetValue(Of String)())
        Assert.AreEqual("./", export("result")("@id").GetValue(Of String)())
        Assert.IsNull(export("agent"), "The export never names a person.")

        Dim manuscript As JsonObject = EntityOf(graph, "#manuscript")
        Assert.AreEqual("ScholarlyArticle", manuscript("@type").GetValue(Of String)())
        Assert.AreEqual("Revision 2", manuscript("version").GetValue(Of String)())
        Assert.AreEqual("files/Main%20text%20%28revision%202%29.pdf", manuscript("encoding")("@id").GetValue(Of String)())
        Assert.AreEqual("https://doi.org/10.5555/example.anchoring", manuscript("exampleOfWork")("@id").GetValue(Of String)())
        Assert.AreEqual("Periodical", EntityOf(graph, "#journal")("@type").GetValue(Of String)())
        Assert.AreEqual("0000-0019", EntityOf(graph, "#journal")("issn").GetValue(Of String)())

        Dim table As JsonObject = EntityOf(graph, PacketExportService.CrateId("Table #1 (50% sample).csv"))
        Assert.AreEqual("File", table("@type").GetValue(Of String)())
        Assert.AreEqual("text/csv", table("encodingFormat").GetValue(Of String)())
        Assert.AreEqual("Table: Table 1", table("description").GetValue(Of String)())
        Assert.IsTrue(Regex.IsMatch(table("contentSize").GetValue(Of String)(), "^\d+$"), "contentSize is the byte count as a string.")
        Assert.IsTrue(Regex.IsMatch(table("sha256").GetValue(Of String)(), "^[0-9a-f]{64}$"))
    End Sub


    <TestMethod>
    Public Sub Metadata_NeverClaimsGplOrAnArticleDate()
        Dim fixture As ExportFixture = BuildExampleFixture(_root)
        Dim zipPath As String = ExportWithDefaults(fixture, "published.zip")
        Dim json As String = EntryText(zipPath, "ro-crate-metadata.json")
        Dim html As String = EntryText(zipPath, "ro-crate-preview.html")

        For Each content As String In {json, html}
            For Each forbidden As String In {"GPL", "General Public License", "gnu.org", "spdx", "10.5555/example.preprint", "2026-01-01", "isBasedOn"}
                Assert.IsFalse(content.Contains(forbidden, StringComparison.OrdinalIgnoreCase), forbidden)
            Next
        Next

        Dim graph As JsonArray = GraphOf(zipPath)
        For Each article As JsonNode In graph.Where(Function(node) node("@type").GetValue(Of String)() = "ScholarlyArticle")
            Assert.IsNull(article("datePublished"), "No article date: none is recorded at a known precision.")
        Next
        Assert.AreEqual("2026-10-03", EntityOf(graph, "./")("datePublished").GetValue(Of String)(), "The package's date is the export date.")
        Dim published As JsonObject = EntityOf(graph, "https://doi.org/10.5555/example.anchoring")
        Assert.AreEqual("Published", published("creativeWorkStatus").GetValue(Of String)())
        Assert.AreEqual("#published-in", published("isPartOf")("@id").GetValue(Of String)())
        Assert.AreEqual(ExampleJournal, EntityOf(graph, "#published-in")("name").GetValue(Of String)())
        Assert.IsNull(published("identifier"))
        StringAssert.Contains(html, "<th scope=""row"">Published as</th><td><a href=""https://doi.org/10.5555/example.anchoring"">")

        ' Accepted, not yet published: the DOI's work stays, without the
        ' status, and the summary page doesn't claim publication either.
        fixture.Manuscript.Location = ManuscriptLocation.Pipeline
        fixture.Manuscript.CurrentStage = PaperStage.Accepted
        Dim acceptedZip As String = ExportWithDefaults(fixture, "accepted.zip")
        Dim acceptedGraph As JsonArray = GraphOf(acceptedZip)
        Dim acceptedWork As JsonObject = EntityOf(acceptedGraph, "https://doi.org/10.5555/example.anchoring")
        Assert.IsNotNull(acceptedWork)
        Assert.IsNull(acceptedWork("creativeWorkStatus"))
        Dim acceptedHtml As String = EntryText(acceptedZip, "ro-crate-preview.html")
        Assert.IsFalse(acceptedHtml.Contains("Published", StringComparison.OrdinalIgnoreCase), "Not published, so not called published.")
        StringAssert.Contains(acceptedHtml, "<th scope=""row"">DOI</th><td><a href=""https://doi.org/10.5555/example.anchoring"">")

        ' A DOI that isn't valid writes no published work at all.
        fixture.Manuscript.Metadata.Doi = "not a doi"
        Dim invalidZip As String = ExportWithDefaults(fixture, "invalid-doi.zip")
        Dim invalidGraph As JsonArray = GraphOf(invalidZip)
        Assert.AreEqual(1, invalidGraph.Where(Function(node) node("@type").GetValue(Of String)() = "ScholarlyArticle").Count())
        Assert.IsNull(EntityOf(invalidGraph, "#manuscript")("exampleOfWork"))
        Assert.IsNull(EntityOf(invalidGraph, "#published-in"))
        Assert.IsFalse(EntryText(invalidZip, "ro-crate-preview.html").Contains("doi.org"))
    End Sub


    <TestMethod>
    Public Sub RevisionRound_HasNoInventedDate()
        Dim fixture As ExportFixture = BuildExampleFixture(_root)

        Dim roundZip As String = ExportWithDefaults(fixture, "round.zip")
        Dim submission As JsonObject = EntityOf(GraphOf(roundZip), "#submission")
        Assert.AreEqual("SendAction", submission("@type").GetValue(Of String)())
        Assert.AreEqual("Sent to Fictional Journal of Psychology, revision round 2", submission("name").GetValue(Of String)())
        Assert.AreEqual("#manuscript", submission("object")("@id").GetValue(Of String)())
        Assert.AreEqual("#journal", submission("recipient")("@id").GetValue(Of String)())
        Assert.IsNull(submission("startTime"), "The date this round was sent isn't recorded.")
        Assert.AreEqual("The date this revision round was sent is not recorded.", submission("description").GetValue(Of String)())
        For Each entryName As String In {"ro-crate-metadata.json", "ro-crate-preview.html"}
            Assert.IsFalse(EntryText(roundZip, entryName).Contains("2026-03-02"), "The first submission's date is not the round's.")
        Next
        StringAssert.Contains(EntryText(roundZip, "ro-crate-preview.html"), "Revision round 2. The date this revision round was sent is not recorded.")

        ' The packet's round left at its default: its version is revision
        ' round 2 of the same submission, so the round comes from there and
        ' the first submission's date is still never given.
        fixture.Packet.RevisionRoundNumber = Nothing
        Dim inferredZip As String = ExportWithDefaults(fixture, "inferred.zip")
        Dim inferred As JsonObject = EntityOf(GraphOf(inferredZip), "#submission")
        Assert.AreEqual("Sent to Fictional Journal of Psychology, revision round 2", inferred("name").GetValue(Of String)())
        Assert.IsNull(inferred("startTime"), "A revision isn't dated with the first submission's date.")
        Assert.AreEqual("The date this revision round was sent is not recorded.", inferred("description").GetValue(Of String)())
        For Each entryName As String In {"ro-crate-metadata.json", "ro-crate-preview.html"}
            Assert.IsFalse(EntryText(inferredZip, entryName).Contains("2026-03-02"), entryName)
        Next
        Assert.IsFalse(EntryText(inferredZip, "ro-crate-preview.html").Contains("Sent on"))

        ' A version that answers one of the submission's decisions but
        ' records no round is still a revision.
        fixture.Version.RevisionRoundNumber = Nothing
        fixture.Version.SubmissionId = Nothing
        Dim unknownZip As String = ExportWithDefaults(fixture, "unknown-round.zip")
        Dim unknown As JsonObject = EntityOf(GraphOf(unknownZip), "#submission")
        Assert.AreEqual("Sent to Fictional Journal of Psychology, revision (round not recorded)", unknown("name").GetValue(Of String)())
        Assert.IsNull(unknown("startTime"))
        Assert.AreEqual("The date this revision was sent is not recorded.", unknown("description").GetValue(Of String)())
        StringAssert.Contains(EntryText(unknownZip, "ro-crate-preview.html"), "A revision (round not recorded). The date this revision was sent is not recorded.")
        Assert.IsFalse(EntryText(unknownZip, "ro-crate-metadata.json").Contains("2026-03-02"))

        ' A version revised for another submission, then sent here afresh:
        ' this submission's date stands.
        fixture.Version.DecisionId = Nothing
        fixture.Version.SubmissionId = Guid.NewGuid()
        fixture.Version.RevisionRoundNumber = 1
        Dim freshZip As String = ExportWithDefaults(fixture, "fresh.zip")
        Assert.AreEqual("2026-03-02", EntityOf(GraphOf(freshZip), "#submission")("startTime").GetValue(Of String)())

        ' The version first sent to this submission.
        fixture.Version.SubmissionId = fixture.Submission.Id
        fixture.Version.RevisionRoundNumber = Nothing
        Dim firstZip As String = ExportWithDefaults(fixture, "first.zip")
        Dim first As JsonObject = EntityOf(GraphOf(firstZip), "#submission")
        Assert.AreEqual("Sent to Fictional Journal of Psychology", first("name").GetValue(Of String)())
        Assert.AreEqual("2026-03-02", first("startTime").GetValue(Of String)(), "The date only: no invented time or zone.")
        Assert.IsNull(first("description"))

        fixture.Packet.SubmissionId = Nothing
        Dim unsentZip As String = ExportWithDefaults(fixture, "unsent.zip")
        Dim unsentGraph As JsonArray = GraphOf(unsentZip)
        Assert.IsNull(EntityOf(unsentGraph, "#submission"))
        CollectionAssert.AreEqual({"#export", "#journal"}, RefIds(EntityOf(unsentGraph, "./")("mentions")).ToArray(), "The journal is still referenced.")
        StringAssert.Contains(EntryText(unsentZip, "ro-crate-preview.html"), "Not linked to a recorded submission")

        fixture.Packet.JournalName = ""
        fixture.Packet.JournalId = Nothing
        Dim bareGraph As JsonArray = GraphOf(ExportWithDefaults(fixture, "bare.zip"))
        Assert.IsTrue(TypeOf EntityOf(bareGraph, "./")("mentions") Is JsonObject, "A single mention is one reference, not a one-item array.")
        Assert.IsNull(EntityOf(bareGraph, "#journal"))
    End Sub


    <TestMethod>
    Public Sub Orcid_IsAnIdOnlyWhenTheChecksumPasses()
        Dim fixture As ExportFixture = SimpleFixture()
        Dim institute As New AffiliationRecord With {.Department = "Department of Examples", .Institution = "Fictional Institute", .City = "SECRET-CITY", .Country = "SECRET-COUNTRY"}
        Dim brown As New AffiliationRecord With {.Institution = "Brown University", .Notes = "SECRET-AFFILIATION"}
        fixture.Library.Affiliations.AddRange({institute, brown})
        Dim valid As New AuthorRecord With {.GivenName = "Josiah", .FamilyName = "Carberry", .Orcid = "https://orcid.org/" & ValidOrcid}
        Dim invalid As New AuthorRecord With {.GivenName = "Riley", .FamilyName = "Placeholder", .Orcid = "0000-0002-1825-0098"}
        Dim unnamed As New AuthorRecord()
        fixture.Library.Authors.AddRange({valid, invalid, unnamed})
        fixture.Manuscript.Authors.Add(New ManuscriptAuthor With {.AuthorId = valid.Id, .AffiliationIds = New List(Of Guid) From {institute.Id}})
        fixture.Manuscript.Authors.Add(New ManuscriptAuthor With {.AuthorId = invalid.Id, .AffiliationIds = New List(Of Guid) From {institute.Id, brown.Id, Guid.NewGuid()}})
        fixture.Manuscript.Authors.Add(New ManuscriptAuthor With {.AuthorId = unnamed.Id})
        fixture.Manuscript.Authors.Add(New ManuscriptAuthor With {.AuthorId = Guid.NewGuid()})
        AddFile(fixture, SubmissionPacketFileRole.Manuscript, "Main.pdf", BuildPdf(), "Main")

        Dim zipPath As String = ExportWithDefaults(fixture, "people.zip")
        Dim graph As JsonArray = GraphOf(zipPath)
        Dim json As String = EntryText(zipPath, "ro-crate-metadata.json")
        Dim html As String = EntryText(zipPath, "ro-crate-preview.html")

        CollectionAssert.AreEqual({"https://orcid.org/" & ValidOrcid, "#author-2"}, RefIds(EntityOf(graph, "#manuscript")("author")).ToArray())
        Dim carberry As JsonObject = EntityOf(graph, "https://orcid.org/" & ValidOrcid)
        Assert.AreEqual("Person", carberry("@type").GetValue(Of String)())
        Assert.AreEqual("Josiah Carberry", carberry("name").GetValue(Of String)())
        Assert.AreEqual("#org-1", carberry("affiliation")("@id").GetValue(Of String)())
        Dim placeholder As JsonObject = EntityOf(graph, "#author-2")
        Assert.AreEqual("Riley Placeholder", placeholder("name").GetValue(Of String)())
        CollectionAssert.AreEqual({"#org-1", "#org-2"}, RefIds(placeholder("affiliation")).ToArray())
        Assert.AreEqual("Department of Examples, Fictional Institute", EntityOf(graph, "#org-1")("name").GetValue(Of String)())
        Assert.AreEqual("Organization", EntityOf(graph, "#org-1")("@type").GetValue(Of String)())
        Assert.AreEqual("Brown University", EntityOf(graph, "#org-2")("name").GetValue(Of String)())
        Assert.AreEqual(2, graph.Where(Function(node) node("@type").GetValue(Of String)() = "Person").Count(), "Unnamed and unknown authors are skipped.")

        For Each content As String In {json, html}
            For Each absent As String In {"1825-0098", "SECRET", "ror.org", "email", "mailto", "(Unnamed author)"}
                Assert.IsFalse(content.Contains(absent, StringComparison.OrdinalIgnoreCase), absent)
            Next
        Next
        StringAssert.Contains(html, "<a href=""https://orcid.org/" & ValidOrcid & """>Josiah Carberry</a> (Department of Examples, Fictional Institute)")
        StringAssert.Contains(html, "Riley Placeholder (Department of Examples, Fictional Institute; Brown University)")
    End Sub


    <TestMethod>
    Public Sub Options_LeaveOutAuthorsAndAbstract()
        Dim fixture As ExportFixture = BuildExampleFixture(_root)

        Dim withEverything As String = ExportWithDefaults(fixture, "everything.zip")
        Dim fullGraph As JsonArray = GraphOf(withEverything)
        Assert.IsNotNull(EntityOf(fullGraph, "#manuscript")("abstract"))
        Assert.AreEqual("anchoring, clinical judgment, risk estimates", EntityOf(fullGraph, "#manuscript")("keywords").GetValue(Of String)())
        Assert.AreEqual(2, fullGraph.Where(Function(node) node("@type").GetValue(Of String)() = "Person").Count())

        Dim zipPath As String = ExportWithDefaults(fixture, "bare.zip",
            Sub(plan)
                plan.IncludeAuthors = False
                plan.IncludeAbstract = False
            End Sub)
        Dim graph As JsonArray = GraphOf(zipPath)
        Dim manuscript As JsonObject = EntityOf(graph, "#manuscript")
        Assert.IsNull(manuscript("author"))
        Assert.IsNull(manuscript("abstract"))
        Assert.IsNull(manuscript("keywords"))
        Assert.IsFalse(graph.Any(Function(node) node("@type").GetValue(Of String)() = "Person" OrElse node("@type").GetValue(Of String)() = "Organization"))
        Dim html As String = EntryText(zipPath, "ro-crate-preview.html")
        For Each absent As String In {"Josiah", "Placeholder", "Brown University", "A fictional study", "clinical judgment", ">Authors<", ">Abstract<"}
            Assert.IsFalse(html.Contains(absent), absent)
        Next
    End Sub


    <TestMethod>
    Public Sub NotIncluded_UsesRoleNamesOnly()
        Dim fixture As ExportFixture = BuildExampleFixture(_root, withSecrets:=True)
        Dim zipPath As String = ExportWithDefaults(fixture, "left-out.zip")
        Dim html As String = EntryText(zipPath, "ro-crate-preview.html")
        Dim json As String = EntryText(zipPath, "ro-crate-metadata.json")

        StringAssert.Contains(html, "<h2>Not included</h2>")
        StringAssert.Contains(html, "<li>Response to reviewers: left out of this package</li>")
        StringAssert.Contains(html, "<li>Reporting checklist: no file: recorded in PaperRoute only</li>")
        StringAssert.Contains(html, "<li>Cover letter: left out of this package</li>")
        StringAssert.Contains(html, "<li>Supplement: left out of this package</li>")
        For Each content As String In {html, json}
            For Each absent As String In {"SECRET-RESPONSE-LABEL", "SECRET-RESPONSE-NAME", "SECRET-LABEL-CHECKLIST", "Cover letter.txt", "Supplement B.txt", "can name the editor", "names reviewers"}
                Assert.IsFalse(content.Contains(absent), absent)
            Next
        Next
        StringAssert.Contains(EntityOf(GraphOf(zipPath), "./")("description").GetValue(Of String)(),
                              "Not included: Cover letter, Supplement, Reporting checklist, Response to reviewers.")
    End Sub


    <TestMethod>
    Public Sub SummaryPage_ClaimsOnlyWhatPaperRouteWrote()
        Dim fixture As ExportFixture = BuildExampleFixture(_root)
        Dim zipPath As String = ExportWithDefaults(fixture, "with-response.zip",
            Sub(plan)
                RowFor(plan, "Response to reviewers").Include = True
                RowFor(plan, "Cover letter").Include = True
            End Sub)
        Dim html As String = EntryText(zipPath, "ro-crate-preview.html")

        StringAssert.Contains(html, ">Response to reviewers.txt</a>", "The response to reviewers is in the package.")
        Assert.IsFalse(html.Contains("This package leaves out", StringComparison.Ordinal), "The page can't say the package leaves out what a file holds.")
        StringAssert.Contains(html, "<p>The summary and metadata PaperRoute wrote leave out notes, correspondence, reviewer names and comments, manuscript numbers, portal links, and where files are kept on the computer. The files themselves are copied as they are.</p>")
    End Sub


    <TestMethod>
    Public Sub DefaultFileName_IsTheCleanedPackageName()
        Dim fixture As ExportFixture = SimpleFixture("Revision 2: to J/Psych?")
        Dim plan As PacketExportPlan = PacketExportService.Prepare(fixture.Manuscript, fixture.Packet, fixture.Library)
        Assert.AreEqual("Revision 2: to J/Psych?", plan.PackageName)
        Assert.AreEqual("Revision 2_ to J_Psych_.zip", plan.DefaultFileName(), "The package name keeps its parts; separators become _.")

        plan.PackageName = ""
        Assert.AreEqual("Submission packet.zip", plan.DefaultFileName())
        plan.PackageName = "CON"
        Assert.AreEqual("_CON.zip", plan.DefaultFileName())
        plan.PackageName = "  Revision 2 v1.2  "
        Assert.AreEqual("Revision 2 v1.2.zip", plan.DefaultFileName())

        fixture.Packet.Label = "   "
        Assert.AreEqual("Submission packet", PacketExportService.Prepare(fixture.Manuscript, fixture.Packet, fixture.Library).PackageName)
    End Sub


    <TestMethod>
    <DataRow("main.PDF", "application/pdf")>
    <DataRow("draft.docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document")>
    <DataRow("old.doc", "application/msword")>
    <DataRow("sheet.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")>
    <DataRow("slides.pptx", "application/vnd.openxmlformats-officedocument.presentationml.presentation")>
    <DataRow("text.odt", "application/vnd.oasis.opendocument.text")>
    <DataRow("table.csv", "text/csv")>
    <DataRow("notes.md", "text/markdown")>
    <DataRow("figure.png", "image/png")>
    <DataRow("photo.JPEG", "image/jpeg")>
    <DataRow("art.tiff", "image/tiff")>
    <DataRow("vector.svg", "image/svg+xml")>
    <DataRow("paper.tex", "application/x-tex")>
    <DataRow("archive.zip", "application/zip")>
    <DataRow("unknown.xyz", "application/octet-stream")>
    <DataRow("no-extension", "application/octet-stream")>
    Public Sub MediaTypes_ByExtension(name As String, expected As String)
        Assert.AreEqual(expected, PacketExportService.MediaType(name))
    End Sub


    <TestMethod>
    Public Sub NothingPrivateLeaks()
        Dim fixture As ExportFixture = BuildExampleFixture(_root, withSecrets:=True)

        ' Every field outside the allow-list carries a marker, including
        ' ones whose value could pass for an allowed one.
        Assert.AreEqual("SECRET-Manuscript.TargetJournal", fixture.Manuscript.TargetJournal)
        Assert.AreEqual("SECRET-JournalRecord.Name", fixture.Library.Journals(0).Name)
        Assert.AreEqual("SECRET-JournalRecord.AimsScopeUrl", fixture.Library.Journals(0).AimsScopeUrl)
        Assert.AreEqual("SECRET-JournalFact.Value", fixture.Library.Journals(0).Facts.Single().Value)
        Assert.AreEqual("SECRET-JournalCandidate.Notes", fixture.Manuscript.JournalShortlist.Single().Notes)
        Assert.AreEqual("SECRET-PublicationMatch.Title", fixture.Manuscript.PublicationMatches.Single().Title)
        Assert.AreEqual("SECRET-AssistantSuggestion.SourceText", fixture.Submission.Decisions(0).Suggestion.SourceText)
        Assert.AreEqual(SecretDate, fixture.Version.CreatedDate)
        Assert.AreEqual(SecretDate, fixture.Submission.Decisions(0).DecisionDate)
        Assert.AreEqual(SecretDate, fixture.Packet.CreatedAtUtc)
        Assert.AreEqual(SecretDate, fixture.Packet.Files(0).HashComputedAtUtc)
        Dim ids As List(Of Guid) = fixture.RecordIds()
        For Each id As Guid In {fixture.Submission.Decisions(0).Id, fixture.Submission.Correspondence(0).Id, fixture.Submission.ReviewerResponses(0).Id,
                                fixture.Packet.ReadinessProfileId.Value, fixture.Manuscript.TargetJournalId.Value, fixture.Manuscript.PublicationMatches(0).Id}
            CollectionAssert.Contains(ids, id)
        Next

        Dim plan As PacketExportPlan = PacketExportService.Prepare(fixture.Manuscript, fixture.Packet, fixture.Library)
        PacketExportService.CheckFiles(plan)
        For Each row As PacketExportRow In plan.Rows
            row.Include = row.CanInclude AndAlso row.IncludedByDefault
        Next
        Assert.IsTrue(plan.IncludedRows().Any(Function(item) item.Fingerprint = PacketExportFingerprint.Changed), "The changed supplement is included too.")
        Dim zipPath As String = OutputPath("leak.zip")
        Export(plan, zipPath)

        Dim scan As List(Of (Entry As String, Bytes As Byte())) = DeepScan(zipPath)
        Assert.IsNull(FindMarker(scan, "SECRET", ignoreCase:=True), "A SECRET marker leaked into " & FindMarker(scan, "SECRET", ignoreCase:=True))
        Assert.IsNull(FindMarker(scan, "2031-07-19"), "A record's date leaked into " & FindMarker(scan, "2031-07-19"))
        Assert.IsNull(FindMarker(scan, _root), "The source folder's path leaked.")
        Assert.IsNull(FindMarker(scan, Path.GetFileName(_root)), "The temporary folder's name leaked.")
        For Each id As Guid In fixture.RecordIds()
            For Each form As String In {id.ToString("D"), id.ToString("N"), id.ToString("D").ToUpperInvariant()}
                Assert.IsNull(FindMarker(scan, form), "A record id leaked: " & form)
            Next
        Next

        ' Short names could match by chance inside compressed bytes, so the
        ' user and machine names are looked for in what PaperRoute writes.
        Dim packageFiles As New List(Of (Entry As String, Bytes As Byte()))()
        For Each entryName As String In {"manifest-sha256.txt", "ro-crate-preview.html", "ro-crate-metadata.json"}
            Dim written As String = EntryText(zipPath, entryName).Replace(PacketExportService.SoftwareUrl, "")
            packageFiles.Add((entryName, Encoding.UTF8.GetBytes(written)))
            Assert.IsFalse(Regex.IsMatch(written, "(?i)\b[a-z]:[\\/]|file:/|\\\\[a-z0-9]|/users/|appdata"), entryName & " holds a local path.")
            Dim dates As String() = {
                "2031-07-19", "July 19, 2031", "19 July 2031", "Jul 19, 2031",
                SecretDate.ToString("d", CultureInfo.CurrentCulture), SecretDate.ToString("D", CultureInfo.CurrentCulture),
                SecretDate.ToString("MMMM d, yyyy", CultureInfo.CurrentCulture), SecretDate.ToString("d", CultureInfo.InvariantCulture)
            }
            For Each absent As String In {"2026-01-01", "2026-03-02", "10.5555/SECRET-PREPRINT", "HIDDEN-"}.Concat(dates)
                Assert.IsFalse(written.Contains(absent), entryName & " holds " & absent)
            Next
        Next
        packageFiles.AddRange(EntryNames(zipPath).Select(Function(item) (item, Encoding.UTF8.GetBytes(item))))
        For Each name As String In {Environment.UserName, Environment.MachineName}.Where(Function(item) item IsNot Nothing AndAlso item.Length >= 3)
            Assert.IsNull(FindMarker(packageFiles, name), "A Windows name leaked: " & name)
        Next

        Dim outside As List(Of String) = EntryNames(zipPath).Where(Function(item) Not item.StartsWith("files/", StringComparison.Ordinal)).ToList()
        CollectionAssert.AreEquivalent({"manifest-sha256.txt", "ro-crate-preview.html", "ro-crate-metadata.json"}, outside)
        Assert.IsFalse(EntryNames(zipPath).Any(Function(item) item.Contains(":"c) OrElse item.Contains("\"c) OrElse item.Contains("..")), "No alternate stream, separator, or parent in an entry name.")

        ' The scan does look inside documents and compressed PDF streams.
        StringAssert.Contains(FindMarker(scan, "HIDDEN-DOCX-CREATOR"), "files/Title page.docx > docProps/core.xml")
        StringAssert.Contains(FindMarker(scan, "HIDDEN-PDF-XMP"), "files/Main text (revision 2).pdf > stream")
        StringAssert.Contains(FindMarker(scan, "HIDDEN-ODT-CREATOR"), "files/Supplement A.odt > meta.xml")
    End Sub


    <TestMethod>
    Public Sub Export_ChangesNoRecordAndNoFile()
        Dim fixture As ExportFixture = BuildExampleFixture(_root, withSecrets:=True)
        Dim manuscriptBefore As String = Snapshot(fixture.Manuscript)
        Dim libraryBefore As String = Snapshot(fixture.Library)
        Dim filesBefore As New Dictionary(Of String, (Bytes As Byte(), Written As DateTime))(StringComparer.OrdinalIgnoreCase)
        For Each item As SubmissionPacketFile In fixture.Packet.Files.Where(Function(candidate) candidate.LocalFilePath.Length > 0)
            filesBefore(item.LocalFilePath) = (File.ReadAllBytes(item.LocalFilePath), File.GetLastWriteTimeUtc(item.LocalFilePath))
        Next
        Dim unfingerprinted As SubmissionPacketFile = fixture.Packet.Files.Single(Function(item) item.Role = SubmissionPacketFileRole.Table)
        Assert.AreEqual("", unfingerprinted.Sha256)

        Dim plan As PacketExportPlan = PacketExportService.Prepare(fixture.Manuscript, fixture.Packet, fixture.Library)
        PacketExportService.CheckFiles(plan)
        For Each row As PacketExportRow In plan.Rows
            row.Include = True
            row.Label &= " (edited)"
        Next
        plan.PackageName = "Edited name"
        Export(plan, OutputPath("unchanged.zip"))

        Assert.AreEqual(manuscriptBefore, Snapshot(fixture.Manuscript), "The export changes no record.")
        Assert.AreEqual(libraryBefore, Snapshot(fixture.Library))
        Assert.AreEqual("", unfingerprinted.Sha256, "No fingerprint is recorded.")
        Assert.AreEqual(ExamplePacketLabel, fixture.Packet.Label)
        For Each pair In filesBefore
            CollectionAssert.AreEqual(pair.Value.Bytes, File.ReadAllBytes(pair.Key), pair.Key)
            Assert.AreEqual(pair.Value.Written, File.GetLastWriteTimeUtc(pair.Key), pair.Key)
        Next
    End Sub


    <TestMethod>
    Public Sub MissingFileDuringExport_LeavesNoZip()
        Dim fixture As ExportFixture = BuildExampleFixture(_root, withSecrets:=True)
        Dim plan As PacketExportPlan = PacketExportService.Prepare(fixture.Manuscript, fixture.Packet, fixture.Library)
        PacketExportService.CheckFiles(plan)
        Dim figure As PacketExportRow = plan.IncludedRows().First(Function(item) item.Role = SubmissionPacketFileRole.Figure)
        File.Delete(figure.Source.LocalFilePath)

        Dim zipPath As String = OutputPath("missing.zip")
        Dim failure As PacketExportException = Assert.ThrowsExactly(Of PacketExportException)(Sub() Export(plan, zipPath))
        Assert.AreEqual(PacketExportFailure.SourceMissing, failure.Kind)
        Assert.AreEqual(PacketExportService.Quoted("Figure 1.png") & " is missing, so nothing was exported.", failure.Message)
        Assert.IsFalse(failure.Message.Contains(_root), "Messages never name where files are kept.")
        Assert.IsFalse(File.Exists(zipPath))
        Assert.AreEqual(0, Directory.GetFiles(Path.GetDirectoryName(zipPath), "*.partial").Length, "No partial file is left.")

        Dim existing As String = OutputPath("existing.zip")
        File.WriteAllText(existing, "old export")
        Assert.ThrowsExactly(Of PacketExportException)(Sub() Export(plan, existing))
        Assert.AreEqual("old export", File.ReadAllText(existing), "An earlier file at the destination is untouched.")
        Assert.AreEqual(0, Directory.GetFiles(Path.GetDirectoryName(zipPath), "*.partial").Length)
    End Sub


    <TestMethod>
    Public Sub FileChangedAfterPreview_StopsTheExport()
        Dim fixture As ExportFixture = BuildExampleFixture(_root)
        Dim plan As PacketExportPlan = PacketExportService.Prepare(fixture.Manuscript, fixture.Packet, fixture.Library)
        PacketExportService.CheckFiles(plan)
        Dim data As PacketExportRow = RowFor(plan, "Data availability statement")
        Assert.AreEqual(PacketExportFingerprint.Unchanged, data.Fingerprint)
        File.AppendAllText(data.Source.LocalFilePath, "An edit after the preview." & vbLf)

        Dim zipPath As String = OutputPath("changed.zip")
        Dim failure As PacketExportException = Assert.ThrowsExactly(Of PacketExportException)(Sub() Export(plan, zipPath))
        Assert.AreEqual(PacketExportFailure.ChangedSincePreview, failure.Kind)
        Assert.AreEqual(PacketExportService.Quoted("Data availability.txt") & " changed after the list was checked. Check the list again, then export.", failure.Message)
        Assert.IsFalse(File.Exists(zipPath))
        Assert.AreEqual(0, Directory.GetFiles(Path.GetDirectoryName(zipPath), "*.partial").Length)

        ' Checking again shows the change, and the file starts unchecked.
        PacketExportService.CheckFiles(plan)
        Assert.AreEqual(PacketExportFingerprint.Changed, data.Fingerprint)
        Assert.IsFalse(data.Include)
        Export(plan, zipPath)
        Assert.IsTrue(File.Exists(zipPath))
    End Sub


    <TestMethod>
    Public Sub UnfingerprintedFileChangedAfterPreview_StopsTheExport()
        Dim fixture As ExportFixture = SimpleFixture()
        AddFile(fixture, SubmissionPacketFileRole.Manuscript, "Main.pdf", BuildPdf(), "Main text")
        Dim docx As String = Path.Combine(fixture.SourceFolder, "anonymized.docx")
        WriteDocx(docx, "", "")
        Dim record As New SubmissionPacketFile With {
            .Role = SubmissionPacketFileRole.BlindedManuscript, .Label = "Anonymized", .OriginalFileName = "Anonymized.docx",
            .LocalFilePath = docx, .StorageMode = SubmissionPacketFileStorageMode.LinkedExternal
        }
        fixture.Packet.Files.Add(record)

        Dim plan As PacketExportPlan = PacketExportService.Prepare(fixture.Manuscript, fixture.Packet, fixture.Library)
        PacketExportService.CheckFiles(plan)
        Dim row As PacketExportRow = RowFor(plan, "Anonymized")
        Assert.AreEqual(PacketExportFingerprint.NotRecorded, row.Fingerprint)
        Assert.AreEqual("None found", row.Hidden.Summary())
        Assert.IsTrue(row.Include)
        Assert.IsFalse(plan.NeedsAcknowledgment())
        Assert.AreEqual(Sha256Of(docx), row.ObservedSha256)
        Export(plan, OutputPath("first.zip"))

        ' Saved again after the check, now naming its author: what was shown
        ' and acknowledged no longer describes it.
        WriteDocx(docx, "Josiah Carberry", "Josiah Carberry")
        Dim zipPath As String = OutputPath("changed.zip")
        Dim failure As PacketExportException = Assert.ThrowsExactly(Of PacketExportException)(Sub() Export(plan, zipPath))
        Assert.AreEqual(PacketExportFailure.ChangedSincePreview, failure.Kind)
        Assert.AreEqual(PacketExportService.Quoted("Anonymized.docx") & " changed after the list was checked. Check the list again, then export.", failure.Message)
        Assert.IsFalse(File.Exists(zipPath))
        Assert.AreEqual(0, Directory.GetFiles(Path.GetDirectoryName(zipPath), "*.partial").Length)

        ' Checking again shows what the file holds now.
        PacketExportService.CheckFiles(plan)
        Assert.AreEqual("Author: Josiah Carberry; Last saved by: Josiah Carberry", row.Hidden.Summary())
        Assert.IsTrue(plan.NeedsAcknowledgment())
        Assert.IsTrue(plan.BlindedManuscriptNamesPerson())
        Export(plan, zipPath)
        Assert.IsTrue(File.Exists(zipPath))
        Assert.AreEqual("", record.Sha256, "Nothing is recorded.")
        Assert.IsNull(record.HashComputedAtUtc)
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
    Public Sub WriteZip_RefusesNothingIncludedAndThePacketsOwnFiles()
        Dim fixture As ExportFixture = SimpleFixture()
        Dim main As SubmissionPacketFile = AddFile(fixture, SubmissionPacketFileRole.Manuscript, "Main.pdf", BuildPdf(), "Main")
        AddFile(fixture, SubmissionPacketFileRole.Supplement, "Data.zip", Encoding.UTF8.GetBytes("not really a zip"), "Data")
        Dim plan As PacketExportPlan = PacketExportService.Prepare(fixture.Manuscript, fixture.Packet, fixture.Library)

        Dim empty As PacketExportException = Assert.ThrowsExactly(Of PacketExportException)(Sub() Export(plan, OutputPath("none.zip")))
        Assert.AreEqual(PacketExportFailure.NothingIncluded, empty.Kind)
        Assert.AreEqual("Choose at least one file to export.", empty.Message)

        PacketExportService.CheckFiles(plan)
        Dim supplementPath As String = RowFor(plan, "Data").Source.LocalFilePath
        Dim before As Byte() = File.ReadAllBytes(supplementPath)
        Dim own As PacketExportException = Assert.ThrowsExactly(Of PacketExportException)(Sub() Export(plan, supplementPath))
        Assert.AreEqual(PacketExportFailure.DestinationUnwritable, own.Kind)
        CollectionAssert.AreEqual(before, File.ReadAllBytes(supplementPath), "A packet file is never overwritten.")

        Dim nowhere As PacketExportException = Assert.ThrowsExactly(Of PacketExportException)(
            Sub() Export(plan, Path.Combine(_root, "no such folder", "out.zip")))
        Assert.AreEqual(PacketExportFailure.DestinationUnwritable, nowhere.Kind)
        Assert.AreEqual("PaperRoute couldn't write the .zip there. Choose another folder.", nowhere.Message)

        Using source As New CancellationTokenSource()
            source.Cancel()
            Dim stopped As PacketExportException = Assert.ThrowsExactly(Of PacketExportException)(
                Sub() PacketExportService.WriteZip(plan, OutputPath("stopped.zip"), ExportTime, TestVersion, source.Token))
            Assert.AreEqual(PacketExportFailure.Cancelled, stopped.Kind)
        End Using
        Assert.IsFalse(File.Exists(OutputPath("stopped.zip")))
        Assert.AreEqual(0, Directory.GetFiles(Path.GetDirectoryName(OutputPath("x.zip")), "*.partial").Length)
        Assert.IsTrue(File.Exists(main.LocalFilePath))
    End Sub


    <TestMethod>
    Public Sub SyntheticExport_WritesCrateForOfflineChecker()
        Dim fixture As ExportFixture = BuildExampleFixture(_root)
        Dim zipPath As String = ExportWithDefaults(fixture, "paperroute-export-synthetic.zip",
            Sub(plan)
                ' The user includes the changed supplement, to show its status.
                RowFor(plan, "Supplementary note").Include = True
            End Sub)

        Dim names As List(Of String) = EntryNames(zipPath)
        CollectionAssert.AreEquivalent(
            {"files/Main text (revision 2).pdf", "files/Title page.docx", "files/Figure 1.png", "files/Figure 1 (2).png",
             "files/Table #1 (50% sample).csv", "files/Data availability.txt", "files/Supplement A.odt", "files/Supplement B.txt",
             "manifest-sha256.txt", "ro-crate-preview.html", "ro-crate-metadata.json"},
            names)
        Dim graph As JsonArray = GraphOf(zipPath)
        Assert.AreEqual(8, RefIds(EntityOf(graph, "./")("hasPart")).Count)
        Dim changedFile As JsonObject = EntityOf(graph, PacketExportService.CrateId("Supplement B.txt"))
        Dim html As String = EntryText(zipPath, "ro-crate-preview.html")
        StringAssert.StartsWith(html, "<!DOCTYPE html>")
        Assert.IsFalse(html.Contains("<script", StringComparison.OrdinalIgnoreCase))
        Assert.IsFalse(html.Contains("<img", StringComparison.OrdinalIgnoreCase))
        StringAssert.Contains(html, "Get-FileHash -Algorithm SHA256")
        StringAssert.Contains(html, "sha256sum -c manifest-sha256.txt")
        StringAssert.Contains(html, "Rights not stated: PaperRoute records no license for these files, and this package grants none.")
        StringAssert.Contains(html, "The package&#39;s date is the day it is exported.")

        Dim evidence As String = Environment.GetEnvironmentVariable("PAPERROUTE_EXPORT_EVIDENCE_DIR")
        If Not String.IsNullOrWhiteSpace(evidence) Then
            Directory.CreateDirectory(evidence)
            File.Copy(zipPath, Path.Combine(evidence, "paperroute-export-synthetic.zip"), overwrite:=True)
        End If
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


    Private Function ExportWithDefaults(fixture As ExportFixture, name As String, Optional configure As Action(Of PacketExportPlan) = Nothing) As String
        Dim plan As PacketExportPlan = PacketExportService.Prepare(fixture.Manuscript, fixture.Packet, fixture.Library)
        PacketExportService.CheckFiles(plan)
        If configure IsNot Nothing Then configure(plan)
        Dim zipPath As String = OutputPath(name)
        Export(plan, zipPath)
        Return zipPath
    End Function


    Private Shared Function RowFor(plan As PacketExportPlan, rowLabel As String) As PacketExportRow
        Return plan.Rows.Single(Function(item) item.Label = rowLabel)
    End Function


    Private Shared Function GraphOf(zipPath As String) As JsonArray
        Return JsonNode.Parse(EntryText(zipPath, "ro-crate-metadata.json"))("@graph").AsArray()
    End Function


    Private Shared Function EntityOf(graph As JsonArray, id As String) As JsonObject
        Return graph.Select(Function(node) node.AsObject()).SingleOrDefault(Function(item) item("@id").GetValue(Of String)() = id)
    End Function


    ' The ids of one reference or an array of them.
    Private Shared Function RefIds(value As JsonNode) As List(Of String)
        If value Is Nothing Then Return New List(Of String)()
        If TypeOf value Is JsonArray Then Return value.AsArray().Select(Function(item) item("@id").GetValue(Of String)()).ToList()
        Return New List(Of String) From {value("@id").GetValue(Of String)()}
    End Function


    Private Shared Function LoneId(reference As JsonObject, propertyName As String) As String
        Assert.AreEqual(1, reference.Count, propertyName & " nests an object other than a lone {""@id""}.")
        Return reference("@id").GetValue(Of String)()
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
