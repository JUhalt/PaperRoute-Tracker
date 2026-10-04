Imports System
Imports System.Collections.Generic
Imports System.Diagnostics
Imports System.Drawing
Imports System.IO
Imports System.IO.Compression
Imports System.Linq
Imports System.Reflection
Imports System.Runtime.ExceptionServices
Imports System.Text
Imports System.Threading
Imports System.Threading.Tasks
Imports System.Windows.Forms
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline.Forms
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

' Export Packet (#45) in the Submission Packet vault and the Export
' Submission Packet window: the defaults, anonymized packets, the
' acknowledgment of hidden information, editing names, writing the .zip
' through the save-path seam, and the layout at the minimum size.
<TestClass>
<DoNotParallelize>
Public Class PacketExportUiTests

    Private _root As String

    <TestInitialize>
    Public Sub CreateRoot()
        _root = CreateTemporaryRoot()
    End Sub

    <TestCleanup>
    Public Sub RemoveRoot()
        DeleteTemporaryRoot(_root)
    End Sub


    ' ---- The vault ----------------------------------------------------------------

    <TestMethod>
    Public Sub Vault_ExportPacketOpensTheSelectedPacket()
        RunOnSta(
            Sub()
                Dim fixture As ExportFixture = BuildExampleFixture(_root)
                fixture.Packet.CreatedAtUtc = New DateTime(2026, 9, 20, 12, 0, 0, DateTimeKind.Utc)
                fixture.Manuscript.SubmissionPackets.Add(New SubmissionPacket With {
                    .Label = "Second packet", .JournalName = "Another Journal",
                    .ManuscriptVersionId = fixture.Version.Id,
                    .CreatedAtUtc = fixture.Packet.CreatedAtUtc.AddDays(-1)
                })
                Dim manuscriptBefore As String = Snapshot(fixture.Manuscript)
                Dim libraryBefore As String = Snapshot(fixture.Library)

                Using vault As New NonactivatingVault(fixture.Manuscript, fixture.Library)
                    ShowOffscreen(vault)
                    Dim working As Manuscript = WorkingCopy(vault)
                    Dim workingBefore As String = Snapshot(working)

                    Dim opened As New List(Of PacketExportForm)()
                    Dim names As New List(Of String)()
                    Dim issns As New List(Of String)()
                    vault.ExportPrompt =
                        Sub(dialog)
                            opened.Add(dialog)
                            names.Add(dialog.Plan.PackageName)
                            issns.AddRange(dialog.Plan.JournalIssns)
                        End Sub

                    Assert.AreEqual("Export Packet...", vault.ExportPacketButton.Text)
                    Assert.IsTrue(vault.ExportPacketButton.Enabled)
                    Assert.IsTrue(vault.ExportSelectedPacket())
                    PacketList(vault).SelectedIndex = 1
                    Application.DoEvents()
                    vault.ExportPacketButton.PerformClick()

                    CollectionAssert.AreEqual({ExamplePacketLabel, "Second packet"}, names.ToArray(), "Each export is of the packet selected.")
                    CollectionAssert.AreEqual({"0000-0019"}, issns.ToArray(), "The library reaches the export: the journal's ISSN.")
                    Assert.IsTrue(opened.All(Function(item) item.IsDisposed), "The export window is closed with the vault's export.")
                    Assert.AreEqual(workingBefore, Snapshot(working), "Exporting changes nothing in the vault's copy.")
                    vault.CancelButton.PerformClick()
                    vault.Close()
                End Using

                Assert.AreEqual(manuscriptBefore, Snapshot(fixture.Manuscript))
                Assert.AreEqual(libraryBefore, Snapshot(fixture.Library))
            End Sub)
    End Sub


    <TestMethod>
    Public Sub Vault_ExportIsRefusedWhileFilesAreChecked()
        RunOnSta(
            Sub()
                Dim fixture As ExportFixture = BuildExampleFixture(_root)
                Using vault As New NonactivatingVault(fixture.Manuscript, fixture.Library)
                    ShowOffscreen(vault)
                    Dim prompted As Integer = 0
                    vault.ExportPrompt = Sub(dialog) prompted += 1

                    ' Fix the busy boundary without relying on disk speed.
                    Dim busy As FieldInfo = GetType(SubmissionPacketVaultForm).GetField("_integrityBusy", BindingFlags.Instance Or BindingFlags.NonPublic)
                    Dim updateButtons As MethodInfo = GetType(SubmissionPacketVaultForm).GetMethod("UpdatePacketButtons", BindingFlags.Instance Or BindingFlags.NonPublic)
                    busy.SetValue(vault, True)
                    Try
                        updateButtons.Invoke(vault, Nothing)
                        Assert.IsFalse(vault.ExportPacketButton.Enabled, "Export waits for Check Files or Record Fingerprint.")
                        Assert.IsFalse(vault.ExportSelectedPacket())
                    Finally
                        busy.SetValue(vault, False)
                    End Try
                    updateButtons.Invoke(vault, Nothing)
                    Assert.IsTrue(vault.ExportPacketButton.Enabled)
                    Assert.AreEqual(0, prompted)
                    vault.Close()
                End Using

                ' With no packet, there is nothing to export.
                fixture.Manuscript.SubmissionPackets.Clear()
                Using vault As New NonactivatingVault(fixture.Manuscript, fixture.Library)
                    ShowOffscreen(vault)
                    vault.ExportPrompt = Sub(dialog) Assert.Fail("No packet is selected.")
                    Assert.IsFalse(vault.ExportPacketButton.Enabled)
                    Assert.IsFalse(vault.ExportSelectedPacket())
                    vault.Close()
                End Using
            End Sub)
    End Sub


    <TestMethod>
    Public Sub Vault_ExportButtonFitsAtMinimumSize()
        RunOnSta(
            Sub()
                Dim fixture As ExportFixture = BuildExampleFixture(_root)
                Dim context As New SubmissionWorkflowRequest With {
                    .Target = SubmissionWorkflowTarget.Packets,
                    .PacketId = fixture.Packet.Id,
                    .VersionId = fixture.Packet.ManuscriptVersionId,
                    .SubmissionId = fixture.Packet.SubmissionId
                }
                For Each workflow As SubmissionWorkflowRequest In {Nothing, context}
                    Using vault As New NonactivatingVault(fixture.Manuscript, fixture.Library, workflow)
                        ShowOffscreen(vault)
                        vault.Size = vault.MinimumSize
                        vault.PerformLayout()
                        Application.DoEvents()

                        AssertInside(vault.ExportPacketButton)
                        For Each button As Button In Descendants(vault).OfType(Of Button)()
                            AssertInside(button)
                        Next
                        Dim list As ListBox = PacketList(vault)
                        Assert.IsTrue(list.ClientSize.Height >= list.ItemHeight * 3,
                                      "The packet list keeps three rows: " & list.ClientSize.Height.ToString() & If(workflow Is Nothing, "", " (from a workflow)"))
                        vault.Close()
                    End Using
                Next
            End Sub)
    End Sub


    ' ---- The export window -------------------------------------------------------

    <TestMethod>
    Public Sub Dialog_StartsWithTheSpecDefaults()
        RunOnSta(
            Sub()
                Dim fixture As ExportFixture = BuildExampleFixture(_root)
                Dim before As String = Snapshot(fixture.Manuscript)
                Using dialog As New ExportProbe(fixture)
                    ShowOffscreen(dialog)
                    PumpUntilComplete(dialog.Loading)

                    Dim expected As New Dictionary(Of String, Boolean) From {
                        {"Main text, revision 2", True},
                        {"Title page", True},
                        {"Cover letter", False},
                        {"Figure 1", True},
                        {"Figure 1, recolored", True},
                        {"Table 1", True},
                        {"Supplementary materials", True},
                        {"Supplementary note", False},
                        {"Reporting checklist (entered in the portal)", False},
                        {"Response to reviewers", False},
                        {"Data availability statement", True}
                    }
                    Assert.AreEqual(expected.Count, dialog.Grid.Rows.Count)
                    For Each item In expected
                        Dim index As Integer = RowIndex(dialog, item.Key)
                        Assert.AreEqual(item.Value, dialog.Plan.Rows(index).Include, item.Key)
                        Assert.AreEqual(item.Value, CBool(Cell(dialog, index, "Include").Value), item.Key & " in the list")
                    Next

                    Assert.AreEqual("Main text (revision 2).pdf", CStr(Cell(dialog, RowIndex(dialog, "Main text, revision 2"), "Name").Value))
                    Assert.AreEqual("Figure 1 (2).png", CStr(Cell(dialog, RowIndex(dialog, "Figure 1, recolored"), "Name").Value))
                    Assert.AreEqual("Unchanged", CStr(Cell(dialog, RowIndex(dialog, "Main text, revision 2"), "Fingerprint").Value))
                    Assert.AreEqual("Changed since recorded", CStr(Cell(dialog, RowIndex(dialog, "Supplementary note"), "Fingerprint").Value))
                    Assert.AreEqual("Not recorded", CStr(Cell(dialog, RowIndex(dialog, "Table 1"), "Fingerprint").Value))
                    Assert.AreEqual("Cover letter", CStr(Cell(dialog, RowIndex(dialog, "Cover letter"), "Role").Value))
                    StringAssert.Contains(CStr(Cell(dialog, RowIndex(dialog, "Main text, revision 2"), "Hidden").Value), "Author: HIDDEN-PDF-AUTHOR")
                    Dim plainCell As DataGridViewCell = Cell(dialog, RowIndex(dialog, "Table 1"), "Hidden")
                    Assert.AreEqual("No hidden fields for this file type", CStr(plainCell.Value))
                    Assert.IsTrue(plainCell.PreferredSize.Width <= dialog.Grid.Columns("Hidden").Width,
                                  "The text shows whole at the default size: " & plainCell.PreferredSize.Width.ToString() & " > " & dialog.Grid.Columns("Hidden").Width.ToString())
                    Assert.IsFalse(dialog.NotesBox.Text.Contains("may still hold hidden information"), "A plain-text file isn't one PaperRoute couldn't look inside.")

                    Dim checklist As Integer = RowIndex(dialog, "Reporting checklist (entered in the portal)")
                    Assert.AreEqual("No file", CStr(Cell(dialog, checklist, "Name").Value))
                    Assert.AreEqual("No file", CStr(Cell(dialog, checklist, "Fingerprint").Value))
                    For Each column As String In {"Include", "Name", "Label"}
                        Assert.IsTrue(Cell(dialog, checklist, column).ReadOnly, "A records-only entry can't be included or renamed: " & column)
                    Next
                    Assert.IsFalse(Cell(dialog, RowIndex(dialog, "Cover letter"), "Include").ReadOnly, "A cover letter may still be included.")

                    For Each note As String In {
                        "Cover letter.txt starts unchecked: it can name the editor or suggested reviewers.",
                        "Response to reviewers.txt starts unchecked: it names reviewers and quotes their comments.",
                        "Supplement B.txt starts unchecked: it changed since its fingerprint was recorded.",
                        "Reporting checklist has no file. The package lists it by role only."
                    }
                        StringAssert.Contains(dialog.NotesBox.Text, note)
                    Next

                    Assert.AreEqual(ExamplePacketLabel, dialog.PackageNameBox.Text)
                    Assert.IsTrue(dialog.AuthorsBox.Checked AndAlso dialog.AuthorsBox.Enabled)
                    Assert.IsTrue(dialog.AbstractBox.Checked AndAlso dialog.AbstractBox.Enabled)
                    Assert.IsFalse(dialog.BlindedNote.Visible)
                    Assert.IsFalse(dialog.StrongWarning.Visible)
                    Assert.IsTrue(dialog.AcknowledgeBox.Visible, "Included files hold hidden information.")
                    Assert.IsFalse(dialog.AcknowledgeBox.Checked)
                    Assert.IsFalse(dialog.ExportButton.Enabled, "Export waits for the acknowledgment.")
                    Assert.AreEqual(PacketExportForm.AcknowledgeText, dialog.AcknowledgeBox.Text)
                    Assert.AreEqual(PacketExportForm.IntroText, Descendants(dialog).OfType(Of Label)().First().Text)
                    Assert.AreEqual("", dialog.StatusText)

                    For Each line As String In {
                        "Title: " & ExampleTitle,
                        "Version: Revision 2",
                        "Journal: " & ExampleJournal & ", ISSN 0000-0019",
                        "Submission: Revision round 2. The date this revision round was sent is not recorded.",
                        PacketExportService.RightsLine,
                        "The package's date is the day it is exported."
                    }
                        CollectionAssert.Contains(dialog.DetailsBox.Lines, line)
                    Next
                    Assert.IsTrue(dialog.DetailsBox.ReadOnly AndAlso dialog.NotesBox.ReadOnly)
                    dialog.Close()
                End Using
                Assert.AreEqual(before, Snapshot(fixture.Manuscript), "Opening and checking change nothing.")
            End Sub)
    End Sub


    <TestMethod>
    Public Sub Dialog_HiddenInformationNeedsAcknowledgment()
        RunOnSta(
            Sub()
                Dim fixture As ExportFixture = SimpleFixture()
                AddFile(fixture, SubmissionPacketFileRole.Manuscript, "Main.pdf", BuildPdf(), "Main text")
                Dim docx As String = Path.Combine(fixture.SourceFolder, "title-source.docx")
                WriteDocx(docx, "HIDDEN Creator", "")
                AddStoredFile(fixture, SubmissionPacketFileRole.TitlePage, "Title page.docx", docx, "Title page")
                AddFile(fixture, SubmissionPacketFileRole.Table, "Table 1.csv", Encoding.UTF8.GetBytes("a,b" & vbLf), "Table 1")

                Using dialog As New ExportProbe(fixture)
                    ShowOffscreen(dialog)
                    PumpUntilComplete(dialog.Loading)
                    Dim title As Integer = RowIndex(dialog, "Title page")
                    Assert.AreEqual("None found", CStr(Cell(dialog, RowIndex(dialog, "Main text"), "Hidden").Value))
                    Assert.AreEqual("Author: HIDDEN Creator", CStr(Cell(dialog, title, "Hidden").Value))

                    Assert.IsTrue(dialog.AcknowledgeBox.Visible)
                    Assert.IsFalse(dialog.ExportButton.Enabled)
                    dialog.AcknowledgeBox.Checked = True
                    Assert.IsTrue(dialog.ExportButton.Enabled)

                    dialog.SetIncludeForTest(title, False)
                    Assert.IsFalse(dialog.AcknowledgeBox.Visible, "Nothing included holds hidden information.")
                    Assert.IsTrue(dialog.ExportButton.Enabled)

                    dialog.SetIncludeForTest(title, True)
                    Assert.IsTrue(dialog.AcknowledgeBox.Visible)
                    Assert.IsFalse(dialog.AcknowledgeBox.Checked, "A file with hidden information joining the package needs a new look.")
                    Assert.IsFalse(dialog.ExportButton.Enabled)

                    ' Nothing included: nothing to export.
                    For index As Integer = 0 To dialog.Grid.Rows.Count - 1
                        dialog.SetIncludeForTest(index, False)
                    Next
                    Assert.IsFalse(dialog.ExportButton.Enabled)
                    dialog.Close()
                End Using
            End Sub)
    End Sub


    <TestMethod>
    Public Sub Dialog_NamesTheFilesItCouldNotLookInside()
        RunOnSta(
            Sub()
                Dim fixture As ExportFixture = SimpleFixture()
                AddFile(fixture, SubmissionPacketFileRole.Manuscript, "Manuscript.docx", Encoding.UTF8.GetBytes("not a zip: damaged or password-protected"), "Main text")
                AddFile(fixture, SubmissionPacketFileRole.Figure, "Figure 1.bin", New Byte() {1, 2, 3}, "Figure 1")
                AddFile(fixture, SubmissionPacketFileRole.Table, "Table 1.csv", Encoding.UTF8.GetBytes("a,b" & vbLf), "Table 1")

                Using dialog As New ExportProbe(fixture)
                    ShowOffscreen(dialog)
                    PumpUntilComplete(dialog.Loading)
                    Assert.AreEqual("Couldn't be checked", CStr(Cell(dialog, RowIndex(dialog, "Main text"), "Hidden").Value))
                    Assert.AreEqual("Not checked", CStr(Cell(dialog, RowIndex(dialog, "Figure 1"), "Hidden").Value))

                    ' The note names each file as its row does, and not the
                    ' plain-text table, which has no hidden fields.
                    CollectionAssert.Contains(dialog.NotesBox.Lines,
                        "PaperRoute couldn't look inside these files, which may still hold hidden information. Manuscript.docx: couldn't be checked; Figure 1.bin: not checked.")
                    Assert.IsFalse(dialog.NotesBox.Text.Contains("Table 1.csv"))

                    dialog.SetIncludeForTest(RowIndex(dialog, "Figure 1"), False)
                    CollectionAssert.Contains(dialog.NotesBox.Lines,
                        "PaperRoute couldn't look inside these files, which may still hold hidden information. Manuscript.docx: couldn't be checked.")
                    dialog.SetIncludeForTest(RowIndex(dialog, "Main text"), False)
                    Assert.IsFalse(dialog.NotesBox.Text.Contains("couldn't look inside"), "Only included files are named.")
                    dialog.Close()
                End Using
            End Sub)
    End Sub


    <TestMethod>
    Public Sub UncheckedNote_HasNoNestedParentheses()
        Dim fixture As ExportFixture = SimpleFixture()
        AddFile(fixture, SubmissionPacketFileRole.Manuscript, "Paper.pdf", BuildPdf(), "Main text")
        AddFile(fixture, SubmissionPacketFileRole.Figure, "Figure 1.bin", New Byte() {1, 2, 3}, "Figure 1")
        Dim plan As PacketExportPlan = PacketExportService.Prepare(fixture.Manuscript, fixture.Packet, fixture.Library)
        PacketExportService.CheckFiles(plan)

        ' An encrypted PDF that opens without a password: its row says
        ' "Couldn't be checked (encrypted)", and the note says it plainly.
        Dim paper As PacketExportRow = plan.Rows.Single(Function(item) item.Label = "Main text")
        paper.Hidden = New HiddenMetadataReport(HiddenMetadataState.Checked, gap:=HiddenMetadataGap.Encrypted)
        Assert.AreEqual("Couldn't be checked (encrypted)", paper.Hidden.Summary())
        Assert.AreEqual(
            "PaperRoute couldn't look inside these files, which may still hold hidden information. Paper.pdf: couldn't be checked, encrypted; Figure 1.bin: not checked.",
            PacketExportForm.UncheckedText(plan))
    End Sub


    <TestMethod>
    Public Sub Dialog_FileChangedAfterTheCheckIsCheckedAgain()
        RunOnSta(
            Sub()
                Dim fixture As ExportFixture = SimpleFixture()
                AddFile(fixture, SubmissionPacketFileRole.Manuscript, "Main.pdf", BuildPdf(), "Main text")
                Dim docx As String = Path.Combine(fixture.SourceFolder, "supplement-source.docx")
                WriteDocx(docx, "", "")
                AddStoredFile(fixture, SubmissionPacketFileRole.Supplement, "Supplement.docx", docx, "Supplement", record:=False)
                Dim folder As String = Path.Combine(_root, "out")
                Directory.CreateDirectory(folder)

                Using dialog As New ExportProbe(fixture)
                    ShowOffscreen(dialog)
                    PumpUntilComplete(dialog.Loading)
                    Dim supplement As Integer = RowIndex(dialog, "Supplement")
                    Assert.AreEqual("Not recorded", CStr(Cell(dialog, supplement, "Fingerprint").Value))
                    Assert.AreEqual("None found", CStr(Cell(dialog, supplement, "Hidden").Value))
                    Assert.IsFalse(dialog.AcknowledgeBox.Visible)
                    dialog.SavePathPrompt = Function() Path.Combine(folder, "first.zip")
                    ExportAsAClickWould(dialog)
                    Assert.AreEqual("Exported 2 files to first.zip.", dialog.StatusText)

                    ' Saved again with the window still open, now naming its author.
                    WriteDocx(docx, "HIDDEN Late Author", "")
                    dialog.SavePathPrompt = Function() Path.Combine(folder, "second.zip")
                    ExportAsAClickWould(dialog)

                    Assert.AreEqual(PacketExportService.Quoted("Supplement.docx") & " changed after the list was checked. Check the list again, then export.", dialog.StatusText)
                    Assert.IsFalse(File.Exists(Path.Combine(folder, "second.zip")), "Nothing nobody saw is written.")
                    Assert.AreEqual("Author: HIDDEN Late Author", CStr(Cell(dialog, supplement, "Hidden").Value), "The list is checked again.")
                    Assert.IsTrue(dialog.AcknowledgeBox.Visible)
                    Assert.IsFalse(dialog.AcknowledgeBox.Checked, "What the file holds now needs a look.")
                    Assert.IsFalse(dialog.ExportButton.Enabled)

                    dialog.AcknowledgeBox.Checked = True
                    ExportAsAClickWould(dialog)
                    Assert.AreEqual("Exported 2 files to second.zip.", dialog.StatusText)
                    dialog.Close()
                End Using
                Assert.AreEqual("", fixture.Packet.Files.Single(Function(item) item.Label = "Supplement").Sha256, "Nothing is recorded.")
            End Sub)
    End Sub


    <TestMethod>
    Public Sub Dialog_ExportWritesTheZipAndStaysOpen()
        RunOnSta(
            Sub()
                Dim fixture As ExportFixture = SimpleFixture()
                Dim main As SubmissionPacketFile = AddFile(fixture, SubmissionPacketFileRole.Manuscript, "Main.pdf", BuildPdf(infoAuthorLiteral:="HIDDEN Author"), "Main text")
                AddFile(fixture, SubmissionPacketFileRole.Figure, "Figure 1.png", BuildPng(shade:=10), "Figure 1")
                AddFile(fixture, SubmissionPacketFileRole.Table, "Table 1.csv", Encoding.UTF8.GetBytes("a,b" & vbLf), "Table 1", record:=False)
                Dim manuscriptBefore As String = Snapshot(fixture.Manuscript)
                Dim libraryBefore As String = Snapshot(fixture.Library)
                Dim sourceBytes As Byte() = File.ReadAllBytes(main.LocalFilePath)
                Dim sourceTime As DateTime = File.GetLastWriteTimeUtc(main.LocalFilePath)
                Dim folder As String = Path.Combine(_root, "out")
                Directory.CreateDirectory(folder)
                Dim zipPath As String = Path.Combine(folder, "out.zip")

                Using dialog As New ExportProbe(fixture)
                    ShowOffscreen(dialog)
                    PumpUntilComplete(dialog.Loading)
                    dialog.SavePathPrompt = Function() zipPath
                    dialog.AcknowledgeBox.Checked = True
                    Assert.IsTrue(dialog.ExportButton.Enabled)

                    PumpUntilComplete(dialog.ExportAsync())

                    Assert.AreEqual("Exported 3 files to out.zip.", dialog.StatusText)
                    Assert.IsTrue(dialog.Visible, "The window stays open after exporting.")
                    Assert.AreEqual(zipPath, dialog.SavedPath)
                    Assert.IsTrue(dialog.ExportButton.Enabled, "Another export can follow.")
                    dialog.Close()
                End Using

                Assert.IsTrue(File.Exists(zipPath))
                Dim entries As List(Of String) = EntryNames(zipPath)
                CollectionAssert.AreEquivalent(
                    {"files/Main.pdf", "files/Figure 1.png", "files/Table 1.csv", "manifest-sha256.txt", "ro-crate-preview.html", "ro-crate-metadata.json"},
                    entries)
                CollectionAssert.AreEqual(sourceBytes, EntryBytes(zipPath, "files/Main.pdf"), "Copied byte for byte, hidden information included.")
                Assert.AreEqual(0, Directory.GetFiles(folder, "*.partial").Length)
                Assert.AreEqual(manuscriptBefore, Snapshot(fixture.Manuscript), "The export changes no record.")
                Assert.AreEqual(libraryBefore, Snapshot(fixture.Library))
                CollectionAssert.AreEqual(sourceBytes, File.ReadAllBytes(main.LocalFilePath))
                Assert.AreEqual(sourceTime, File.GetLastWriteTimeUtc(main.LocalFilePath))
            End Sub)
    End Sub


    <TestMethod>
    Public Sub Dialog_EditedNamesAreCleanedAndUnique()
        RunOnSta(
            Sub()
                Dim fixture As ExportFixture = SimpleFixture()
                AddFile(fixture, SubmissionPacketFileRole.Manuscript, "Main.pdf", BuildPdf(), "Main text")
                AddFile(fixture, SubmissionPacketFileRole.Table, "Table 1.csv", Encoding.UTF8.GetBytes("a,b" & vbLf), "Table 1")

                Using dialog As New ExportProbe(fixture)
                    ShowOffscreen(dialog)
                    PumpUntilComplete(dialog.Loading)
                    Dim main As Integer = RowIndex(dialog, "Main text")
                    Dim table As Integer = RowIndex(dialog, "Table 1")

                    dialog.SetNameForTest(main, "a:b?.pdf")
                    Assert.AreEqual("a_b_.pdf", dialog.Plan.Rows(main).OutputName)
                    Assert.AreEqual("a_b_.pdf", CStr(Cell(dialog, main, "Name").Value), "The list shows the cleaned name.")

                    dialog.SetNameForTest(table, "A_B_.PDF")
                    Assert.AreEqual("A_B_ (2).PDF", dialog.Plan.Rows(table).OutputName, "Equal names, ignoring case, are numbered.")
                    Assert.AreEqual("A_B_ (2).PDF", CStr(Cell(dialog, table, "Name").Value))

                    dialog.SetIncludeForTest(main, False)
                    Assert.AreEqual("A_B_.PDF", CStr(Cell(dialog, table, "Name").Value), "A file left out reserves no name.")
                    dialog.SetIncludeForTest(main, True)

                    dialog.SetNameForTest(table, "..\Results")
                    Assert.AreEqual("Results.csv", CStr(Cell(dialog, table, "Name").Value), "A name without an extension gets the file's back.")
                    dialog.SetNameForTest(table, "")
                    Assert.AreEqual("Table.csv", CStr(Cell(dialog, table, "Name").Value), "An empty name falls back to the role.")

                    dialog.SetLabelForTest(main, "  Main text, as sent  ")
                    Assert.AreEqual("Main text, as sent", dialog.Plan.Rows(main).Label)
                    Assert.AreEqual("Manuscript: Main text, as sent", dialog.Plan.Rows(main).Description())
                    dialog.Close()
                End Using
            End Sub)
    End Sub


    <TestMethod>
    Public Sub Dialog_BlindedPacketLocksAuthorsAndWarns()
        RunOnSta(
            Sub()
                Dim fixture As ExportFixture = BlindedFixture()
                Using dialog As New ExportProbe(fixture)
                    ShowOffscreen(dialog)
                    PumpUntilComplete(dialog.Loading)

                    Assert.IsTrue(dialog.BlindedNote.Visible)
                    Assert.AreEqual(PacketExportForm.BlindedText, dialog.BlindedNote.Text)
                    Assert.IsFalse(dialog.AuthorsBox.Checked, "An anonymized package names no authors.")
                    Assert.IsFalse(dialog.AuthorsBox.Enabled)
                    Assert.IsFalse(dialog.Plan.Rows(RowIndex(dialog, "Title page")).Include)
                    Assert.IsFalse(dialog.Plan.Rows(RowIndex(dialog, "Cover letter")).Include)
                    StringAssert.Contains(dialog.NotesBox.Text, "Title page.docx starts unchecked: this packet is anonymized and a title page names the authors.")
                    StringAssert.Contains(dialog.NotesBox.Text,
                        "Check Anonymized manuscript.pdf: its name, label, or hidden information includes " & PacketExportService.Quoted("Carberry") & ".")

                    Assert.IsTrue(dialog.StrongWarning.Visible, "The anonymized manuscript names a person inside the file.")
                    StringAssert.Contains(dialog.StrongWarning.Text, "(Author: Josiah Carberry)")
                    Assert.IsTrue(dialog.AcknowledgeBox.Visible, "The same acknowledgment is still needed.")
                    Assert.IsFalse(dialog.ExportButton.Enabled)

                    dialog.PackageNameBox.Text = "Carberry revision 2"
                    Assert.AreEqual("Carberry revision 2", dialog.Plan.PackageName)
                    StringAssert.Contains(dialog.NotesBox.Text,
                        "The package name includes " & PacketExportService.Quoted("Carberry") & ". Change it before exporting.")
                    dialog.PackageNameBox.Text = "Anonymized revision 2"
                    Assert.IsFalse(dialog.NotesBox.Text.Contains("The package name includes"))

                    ' Leaving the anonymized manuscript out removes the warning.
                    dialog.SetIncludeForTest(RowIndex(dialog, "Main text, revision 2"), False)
                    Assert.IsFalse(dialog.StrongWarning.Visible)
                    dialog.Close()
                End Using
            End Sub)
    End Sub


    <TestMethod>
    Public Sub Dialog_ReportsAMissingFileInPlainWords()
        RunOnSta(
            Sub()
                Dim fixture As ExportFixture = SimpleFixture()
                AddFile(fixture, SubmissionPacketFileRole.Manuscript, "Main.pdf", BuildPdf(), "Main text")
                Dim table As SubmissionPacketFile = AddFile(fixture, SubmissionPacketFileRole.Table, "Table 1.csv", Encoding.UTF8.GetBytes("a,b" & vbLf), "Table 1")
                Dim folder As String = Path.Combine(_root, "out")
                Directory.CreateDirectory(folder)
                Dim zipPath As String = Path.Combine(folder, "missing.zip")

                Using dialog As New ExportProbe(fixture)
                    ShowOffscreen(dialog)
                    PumpUntilComplete(dialog.Loading)
                    Assert.IsTrue(dialog.ExportButton.Enabled)
                    File.Delete(table.LocalFilePath)
                    dialog.SavePathPrompt = Function() zipPath

                    PumpUntilComplete(dialog.ExportAsync())

                    Assert.AreEqual(PacketExportService.Quoted("Table 1.csv") & " is missing, so nothing was exported.", dialog.StatusText)
                    Assert.IsFalse(dialog.StatusText.Contains(_root), "No folder is named.")
                    Assert.IsTrue(dialog.Visible)
                    Dim row As PacketExportRow = dialog.Plan.Rows(RowIndex(dialog, "Table 1"))
                    Assert.AreEqual(PacketExportFingerprint.Missing, row.Fingerprint, "The list is checked again.")
                    Assert.AreEqual("Missing", CStr(Cell(dialog, RowIndex(dialog, "Table 1"), "Fingerprint").Value))
                    Assert.IsTrue(dialog.ExportButton.Enabled, "The other files can still be exported.")
                    dialog.Close()
                End Using

                Assert.IsFalse(File.Exists(zipPath), "No partial package is left.")
                Assert.AreEqual(0, Directory.GetFiles(folder).Length, "No temporary file is left.")
            End Sub)
    End Sub


    <TestMethod>
    <DataRow(SystemColorMode.Classic)>
    <DataRow(SystemColorMode.Dark)>
    Public Sub Dialog_FitsAtItsMinimumSize(mode As SystemColorMode)
        RunOnSta(
            Sub()
                Dim fixture As ExportFixture = BlindedFixture()
                Using dialog As New ExportProbe(fixture)
                    ShowOffscreen(dialog)
                    PumpUntilComplete(dialog.Loading)
                    dialog.Size = dialog.MinimumSize
                    dialog.PerformLayout()
                    Application.DoEvents()

                    For Each control As Control In New Control() {
                        dialog.Grid, dialog.NotesBox, dialog.DetailsBox, dialog.PackageNameBox, dialog.AuthorsBox, dialog.AbstractBox,
                        dialog.BlindedNote, dialog.StrongWarning, dialog.AcknowledgeBox, dialog.ExportButton, dialog.CancelButtonForTest
                    }
                        AssertInside(control)
                    Next
                    For Each label As Label In Descendants(dialog).OfType(Of Label)().Where(Function(item) item.Visible AndAlso item.Text.Length > 0)
                        AssertInside(label)
                    Next

                    Dim grid As DataGridView = dialog.Grid
                    Assert.IsTrue(grid.ClientSize.Height >= grid.ColumnHeadersHeight + grid.Rows(0).Height * 2,
                                  "At least two files show: " & grid.ClientSize.Height.ToString())
                    For Each box As TextBox In {dialog.NotesBox, dialog.DetailsBox}
                        Assert.IsTrue(box.ClientSize.Height >= box.Font.Height * 2, box.AccessibleName & " keeps two lines: " & box.ClientSize.Height.ToString())
                    Next
                    Dim acknowledgment As CheckBox = dialog.AcknowledgeBox
                    Dim needed As Integer = TextRenderer.MeasureText(acknowledgment.Text, acknowledgment.Font,
                        New Size(acknowledgment.Width - dialog.LogicalToDeviceUnits(16) - 8, 0), TextFormatFlags.WordBreak).Height
                    Assert.IsTrue(acknowledgment.Height >= needed, "The acknowledgment shows all of its text.")
                    dialog.Close()
                End Using
            End Sub, mode)
    End Sub


    <TestMethod>
    Public Sub Dialog_CancelCloses()
        RunOnSta(
            Sub()
                Dim fixture As ExportFixture = SimpleFixture()
                AddFile(fixture, SubmissionPacketFileRole.Manuscript, "Main.pdf", BuildPdf(), "Main text")
                Using dialog As New ExportProbe(fixture)
                    ShowOffscreen(dialog)
                    PumpUntilComplete(dialog.Loading)
                    Assert.AreSame(dialog.CancelButtonForTest, dialog.CancelButton)
                    Assert.IsNull(dialog.AcceptButton, "Enter commits an edit in the file list.")
                    dialog.CancelButtonForTest.PerformClick()
                    Assert.AreEqual(DialogResult.Cancel, dialog.DialogResult)
                    Assert.AreEqual("", dialog.SavedPath, "Nothing was written.")
                    dialog.Close()
                End Using
            End Sub)
    End Sub


    ' ---- Fixtures -----------------------------------------------------------------

    Private Function SimpleFixture() As ExportFixture
        Dim manuscript As New Manuscript With {.Title = "A test manuscript"}
        Dim version As New ManuscriptVersion With {.Label = "Version 1"}
        manuscript.Versions.Add(version)
        Dim packet As New SubmissionPacket With {.Label = "Test packet", .ManuscriptVersionId = version.Id, .JournalName = "Test Journal"}
        manuscript.SubmissionPackets.Add(packet)
        Dim folder As String = Path.Combine(_root, "files")
        Directory.CreateDirectory(folder)
        Return New ExportFixture With {
            .Root = _root, .SourceFolder = folder, .Manuscript = manuscript,
            .Library = New AuthorLibraryData(), .Packet = packet, .Version = version
        }
    End Function


    ' The example packet, anonymized: its manuscript is the blinded one, and
    ' that PDF's Author names the first author.
    Private Function BlindedFixture() As ExportFixture
        Dim fixture As ExportFixture = BuildExampleFixture(_root)
        Dim main As SubmissionPacketFile = fixture.FileWithRole(SubmissionPacketFileRole.Manuscript)
        main.Role = SubmissionPacketFileRole.BlindedManuscript
        main.OriginalFileName = "Anonymized manuscript.pdf"
        WritePdf(main.LocalFilePath, infoAuthorLiteral:="Josiah Carberry")
        main.Sha256 = String.Empty
        Return fixture
    End Function


    Private Shared Function AddFile(
        fixture As ExportFixture,
        role As SubmissionPacketFileRole,
        originalName As String,
        content As Byte(),
        label As String,
        Optional record As Boolean = True
    ) As SubmissionPacketFile
        Dim stored As String = Path.Combine(fixture.SourceFolder, "stored-" & Guid.NewGuid().ToString("N") & Path.GetExtension(originalName))
        File.WriteAllBytes(stored, content)
        Return AddStoredFile(fixture, role, originalName, stored, label, record)
    End Function


    Private Shared Function AddStoredFile(
        fixture As ExportFixture,
        role As SubmissionPacketFileRole,
        originalName As String,
        stored As String,
        label As String,
        Optional record As Boolean = True
    ) As SubmissionPacketFile
        Dim item As New SubmissionPacketFile With {
            .Role = role, .Label = label, .OriginalFileName = originalName,
            .LocalFilePath = stored, .StorageMode = SubmissionPacketFileStorageMode.LinkedExternal
        }
        If record Then SubmissionPacketIntegrityService.CaptureBaseline(item)
        fixture.Packet.Files.Add(item)
        Return item
    End Function


    ' ---- Helpers ------------------------------------------------------------------

    Private Shared Function RowIndex(dialog As PacketExportForm, rowLabel As String) As Integer
        For index As Integer = 0 To dialog.Plan.Rows.Count - 1
            If dialog.Plan.Rows(index).Label = rowLabel Then Return index
        Next
        Assert.Fail("No row labeled " & rowLabel)
        Return -1
    End Function


    Private Shared Function Cell(dialog As PacketExportForm, index As Integer, column As String) As DataGridViewCell
        Return dialog.Grid.Rows(index).Cells(column)
    End Function


    Private Shared Function PacketList(vault As Form) As ListBox
        Dim detail As TextBox = Descendants(vault).OfType(Of TextBox)().Single(Function(item) item.AccessibleName = "Selected packet details")
        Return detail.Parent.Controls.OfType(Of ListBox)().Single()
    End Function


    Private Shared Function WorkingCopy(vault As SubmissionPacketVaultForm) As Manuscript
        Return DirectCast(GetType(SubmissionPacketVaultForm).GetField("_workingManuscript", BindingFlags.Instance Or BindingFlags.NonPublic).GetValue(vault), Manuscript)
    End Function


    Private Shared Sub ShowOffscreen(form As Form)
        If form.Visible Then Return
        form.ShowInTaskbar = False
        form.Opacity = 0
        form.StartPosition = FormStartPosition.Manual
        form.Location = New Point(-20000, -20000)
        form.Show()
        Application.DoEvents()
        form.Location = New Point(-20000, -20000)
        form.PerformLayout()
        Application.DoEvents()
    End Sub


    Private Shared Sub AssertInside(control As Control)
        Assert.IsTrue(control.Visible, control.GetType().Name & " " & control.Text & " is hidden.")
        Assert.IsTrue(control.Width > 0 AndAlso control.Height > 0)
        Dim bounds As Rectangle = control.Parent.RectangleToScreen(control.Bounds)
        Dim ancestor As Control = control.Parent
        While ancestor IsNot Nothing
            Assert.IsTrue(ancestor.ClientRectangle.Contains(ancestor.RectangleToClient(bounds)),
                          control.GetType().Name & " (" & control.AccessibleName & control.Text & ") is clipped by " & ancestor.GetType().Name &
                          "; child=" & ancestor.RectangleToClient(bounds).ToString() & "; available=" & ancestor.ClientRectangle.ToString())
            ancestor = ancestor.Parent
        End While
    End Sub


    Private Shared Iterator Function Descendants(parent As Control) As IEnumerable(Of Control)
        For Each child As Control In parent.Controls
            Yield child
            For Each nested As Control In Descendants(child)
                Yield nested
            Next
        Next
    End Function


    Private Shared Sub PumpUntilComplete(operation As Task)
        Dim timer As Stopwatch = Stopwatch.StartNew()
        While Not operation.IsCompleted AndAlso timer.Elapsed < TimeSpan.FromSeconds(20)
            Application.DoEvents()
            Thread.Sleep(1)
        End While
        Assert.IsTrue(operation.IsCompleted, "The packet export operation timed out.")
        operation.GetAwaiter().GetResult()
        Application.DoEvents()
    End Sub


    ' Starts Export from the message loop, as a click does, so the export's
    ' awaits come back to this thread. Called directly after DoEvents, they
    ' would resume on the thread pool, which is never the case in the app.
    Private Shared Sub ExportAsAClickWould(dialog As PacketExportForm)
        Dim export As Task = Nothing
        dialog.BeginInvoke(New Action(Sub() export = dialog.ExportAsync()))
        Dim timer As Stopwatch = Stopwatch.StartNew()
        While export Is Nothing AndAlso timer.Elapsed < TimeSpan.FromSeconds(20)
            Application.DoEvents()
            Thread.Sleep(1)
        End While
        Assert.IsNotNull(export, "The export didn't start.")
        PumpUntilComplete(export)
    End Sub


    Private Shared Sub RunOnSta(action As Action, Optional mode As SystemColorMode = SystemColorMode.Classic)
        Dim failure As Exception = Nothing
        Dim thread As New Thread(
            Sub()
                Dim priorMode As SystemColorMode = Application.ColorMode
                Try
                    Application.EnableVisualStyles()
                    Application.SetColorMode(mode)
                    action()
                Catch ex As Exception
                    failure = ex
                Finally
                    Application.SetColorMode(priorMode)
                End Try
            End Sub) With {.IsBackground = True}
        thread.SetApartmentState(ApartmentState.STA)
        thread.Start()
        Assert.IsTrue(thread.Join(TimeSpan.FromMinutes(2)), "The packet export UI test timed out.")
        If failure IsNot Nothing Then ExceptionDispatchInfo.Capture(failure).Throw()
    End Sub


    Private Class NonactivatingVault
        Inherits SubmissionPacketVaultForm

        Public Sub New(manuscript As Manuscript, library As AuthorLibraryData, Optional context As SubmissionWorkflowRequest = Nothing)
            MyBase.New(manuscript, context, library)
        End Sub

        Protected Overrides ReadOnly Property ShowWithoutActivation As Boolean
            Get
                Return True
            End Get
        End Property
    End Class


    Private Class ExportProbe
        Inherits PacketExportForm

        Public Sub New(fixture As ExportFixture)
            MyBase.New(fixture.Manuscript, fixture.Packet, fixture.Library)
        End Sub

        Protected Overrides ReadOnly Property ShowWithoutActivation As Boolean
            Get
                Return True
            End Get
        End Property
    End Class

End Class
