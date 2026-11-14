Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.Linq
Imports System.Reflection
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline
Imports ManuscriptPipeline.Forms
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

' An ORCID import whose save fails leaves memory as the disk is (#243):
' what was not saved is taken back out, so a later save of anything never
' writes an import the researcher was told failed, and the message says
' what is on disk. A save is made to fail by a folder where its temporary
' file goes; FileStream(FileMode.Create) on it throws
' UnauthorizedAccessException, and the writers' cleanup, which tests
' File.Exists, leaves it alone. No test imports twice: the pre-import
' copies are named to the second.
<TestClass>
<DoNotParallelize>
Public Class OrcidImportUiTests

    Private _root As String = String.Empty

    Private Const OrcidId As String = "0000-0002-1825-0097"

    Private Const WorkTitle As String = "Imported ORCID Work"

    Private Const ExistingTitle As String = "Already in the library"

    ' The author library form's repositories resolve their folders under
    ' the override, so nothing is read from or written to the profile.
    <TestInitialize>
    Public Sub Initialize()
        _root = CreateTemporaryRoot()
        StorageEnvironment.RootOverride = Path.Combine(_root, "storage")
    End Sub

    <TestCleanup>
    Public Sub Cleanup()
        StorageEnvironment.RootOverride = Nothing
        DeleteTemporaryRoot(_root)
    End Sub

    ' Nothing was saved, so nothing stays: the work comes back out of the
    ' live list, and the ORCID iD and affiliation out of the author library.
    ' Before the change the live list held 2 manuscripts and the iD stayed
    ' set in memory.
    <TestMethod>
    Public Sub AnOrcidImportWhoseManuscriptSaveFailsChangesNothing()
        StaRunner.Run(
            Sub()
                Dim live As List(Of Manuscript) = Seed()

                Using form As New AuthorLibraryForm(live)
                    Dim blocker As String = Path.Combine(Path.GetDirectoryName(New ManuscriptRepository().DataFilePath), "manuscripts.tmp")
                    Directory.CreateDirectory(blocker)

                    Using dialogs As New ManuscriptPageTests.DialogWatcher()
                        form.ApplyOrcidChanges(LibraryOf(form).Authors.Single(), CreateProfile(), CreateOptions())
                        CollectionAssert.AreEqual({"ORCID Import Error"}, dialogs.Closed)
                    End Using

                    Assert.AreEqual(1, live.Count, "The work comes back out of the live list.")
                    Assert.AreEqual(String.Empty, LibraryOf(form).Authors.Single().Orcid, "The ORCID iD comes back out.")
                    Assert.AreEqual(0, LibraryOf(form).Affiliations.Count, "The affiliation comes back out.")
                    CollectionAssert.AreEqual({ExistingTitle}, New ManuscriptRepository().Load().Select(Function(item) item.Title).ToList(), "Disk is unchanged.")
                    Assert.AreEqual(String.Empty, New AuthorLibraryRepository().Load().Authors.Single().Orcid, "Disk is unchanged.")

                    ' A later save of anything writes none of it.
                    Directory.Delete(blocker)
                    Dim manuscripts As New ManuscriptRepository()
                    manuscripts.Save(live)
                    Dim authors As New AuthorLibraryRepository()
                    authors.SaveKeepingJournals(LibraryOf(form))
                    Assert.IsFalse(New ManuscriptRepository().Load().Any(Function(item) item.Title = WorkTitle), "The work never reaches disk.")
                    Assert.IsFalse(New AuthorLibraryRepository().Load().Authors.Any(Function(item) item.Orcid = OrcidId), "The iD never reaches disk.")
                End Using
            End Sub)
    End Sub

    ' The works were saved and stay; the author library was not, so the iD
    ' and affiliation come back out, and the message says the works were
    ' imported but the rest was not. Before the change the title was
    ' "ORCID Import Error" and the iD stayed in memory.
    <TestMethod>
    Public Sub AnOrcidImportWhoseAuthorSaveFailsKeepsOnlyTheSavedWorks()
        StaRunner.Run(
            Sub()
                Dim live As List(Of Manuscript) = Seed()

                Using form As New AuthorLibraryForm(live)
                    Dim blocker As String = New AuthorLibraryRepository().DataFilePath & ".tmp"
                    Directory.CreateDirectory(blocker)

                    Using dialogs As New ManuscriptPageTests.DialogWatcher()
                        form.ApplyOrcidChanges(LibraryOf(form).Authors.Single(), CreateProfile(), CreateOptions())
                        CollectionAssert.AreEqual({"ORCID Import Incomplete"}, dialogs.Closed)
                    End Using

                    CollectionAssert.AreEquivalent({ExistingTitle, WorkTitle}, live.Select(Function(item) item.Title).ToList(), "The saved work stays in the live list.")
                    CollectionAssert.AreEquivalent({ExistingTitle, WorkTitle}, New ManuscriptRepository().Load().Select(Function(item) item.Title).ToList(), "The work is on disk.")
                    Assert.AreEqual(String.Empty, LibraryOf(form).Authors.Single().Orcid, "The ORCID iD comes back out.")
                    Assert.AreEqual(0, LibraryOf(form).Affiliations.Count, "The affiliation comes back out.")

                    ' A later author save writes none of the rest.
                    Directory.Delete(blocker)
                    Dim authors As New AuthorLibraryRepository()
                    authors.SaveKeepingJournals(LibraryOf(form))
                    Dim saved As AuthorLibraryData = New AuthorLibraryRepository().Load()
                    Assert.IsFalse(saved.Authors.Any(Function(item) item.Orcid = OrcidId), "The iD never reaches disk.")
                    Assert.AreEqual(0, saved.Affiliations.Count, "The affiliation never reaches disk.")
                End Using
            End Sub)
    End Sub

    ' ORCID... from Manage Library on the manuscript page's Authors tab
    ' (#353). The page's working copy is the page's
    ' alone until its own Save: an unsaved author removal is not written by
    ' the import, Discard leaves the disk as it was, and the works imported
    ' reach the main window's library, so the page's Save keeps them. Every
    ' check is made and the failures reported together.
    <TestMethod>
    Public Sub AnOrcidImportFromTheManuscriptPageKeepsTheWorkingCopyUnsavedAndTheWorks()
        StaRunner.Run(
            Sub()
                Dim library As New AuthorLibraryData()
                Dim author As New AuthorRecord With {.GivenName = "Local", .FamilyName = "Author"}
                library.Authors.Add(author)
                Dim seededAuthors As New AuthorLibraryRepository()
                seededAuthors.Save(library)
                Dim seededManuscripts As New ManuscriptRepository()
                seededManuscripts.Save(New List(Of Manuscript) From {
                    New Manuscript With {
                        .Title = ExistingTitle,
                        .Authors = New List(Of ManuscriptAuthor) From {New ManuscriptAuthor With {.AuthorId = author.Id}}
                    }
                })

                ' The board reads and writes the same files as the window it
                ' opens, under the override.
                Dim files As New LargeLibraryFixture.SyntheticLibrary With {
                    .Name = "orcid-import-save",
                    .ManagedDirectory = Path.Combine(_root, "managed"),
                    .Repository = New ManuscriptRepository(),
                    .AuthorRepository = New AuthorLibraryRepository()
                }

                Dim failures As New List(Of String)()

                Using board As New LibraryBoard(files, Path.Combine(_root, "settings")),
                      dialogs As New ManuscriptPageTests.DialogWatcher()
                    board.Start()
                    board.Open(board.Library.Single())

                    ' An unsaved edit on the Authors tab: Remove takes the one
                    ' author out of the page's working copy.
                    Dim authorsList As System.Windows.Forms.ListBox =
                        DirectCast(GetType(EditManuscriptForm).GetField("lstAuthors", BindingFlags.Instance Or BindingFlags.NonPublic).GetValue(board.Editor), System.Windows.Forms.ListBox)
                    Assert.AreEqual(1, authorsList.Items.Count, "The page lists the manuscript's one author.")
                    authorsList.SelectedIndex = 0
                    GetType(EditManuscriptForm).GetMethod("RemoveStructuredAuthor", BindingFlags.Instance Or BindingFlags.NonPublic).
                        Invoke(board.Editor, New Object() {Nothing, EventArgs.Empty})
                    Assert.AreEqual(0, authorsList.Items.Count, "Remove took the author off the page.")

                    ' Manage Library (its Click handler), then ORCID... with one
                    ' work chosen, as OpenSelectedAuthorOrcid applies it.
                    Dim imported As Integer = 0
                    Using answer As New SubmissionWorkflowConsistencyTests.ModalAnswer(Of AuthorLibraryForm)(
                        Sub(dialog)
                            dialog.ApplyOrcidChanges(LibraryOf(dialog).Authors.Single(), CreateProfile(), CreateOptions())
                            imported += 1
                            dialog.Close()
                        End Sub)
                        GetType(EditManuscriptForm).GetMethod("ManageAuthorLibrary", BindingFlags.Instance Or BindingFlags.NonPublic).
                            Invoke(board.Editor, New Object() {Nothing, EventArgs.Empty})
                    End Using
                    System.Windows.Forms.Application.DoEvents()

                    Assert.AreEqual(1, imported, "Manage Library opened Authors & Affiliations and the import ran.")
                    CollectionAssert.AreEqual({"ORCID Import Complete"}, dialogs.Closed, "The import went through.")

                    Dim afterImport As Manuscript = New ManuscriptRepository().Load().Single(Function(item) item.Title = ExistingTitle)
                    If afterImport.Authors.Count <> 1 Then
                        failures.Add("(1) After the import, before Save, the manuscript on disk has " & afterImport.Authors.Count.ToString() & " authors, not 1: the page's unsaved removal was written.")
                    End If
                    If Not board.Library.Any(Function(item) item.Title = WorkTitle) Then
                        failures.Add("(2a) After the import the main window's library does not hold the imported work (" & board.Library.Count.ToString() & " manuscripts).")
                    End If

                    ' Discard drops the page's edits.
                    board.Discard()
                    Dim afterDiscard As Manuscript = New ManuscriptRepository().Load().Single(Function(item) item.Title = ExistingTitle)
                    If afterDiscard.Authors.Count <> 1 Then
                        failures.Add("(1b) After Discard the manuscript on disk has " & afterDiscard.Authors.Count.ToString() & " authors, not 1: the discarded removal stays on disk.")
                    End If

                    ' The page's Save writes the main window's library.
                    Dim saved As Boolean = board.SaveManuscriptPage()
                    Assert.IsTrue(saved, "The page saved.")
                    Dim afterSave As List(Of Manuscript) = New ManuscriptRepository().Load()
                    If Not afterSave.Any(Function(item) item.Title = WorkTitle) Then
                        failures.Add("(2) After the page's Save the imported work is gone from disk: " & String.Join(" | ", afterSave.Select(Function(item) item.Title)))
                    End If

                    board.SetPrompt(Function(title) System.Windows.Forms.DialogResult.No)
                    board.Close()
                End Using

                Assert.AreEqual(0, failures.Count, String.Join(Environment.NewLine, failures))
            End Sub)
    End Sub

    ' The same import, then the page's Save (#353): the file holds the
    ' page's edit and the work, and a second save, which moves the first
    ' file to manuscripts.bak, still keeps the work. The window opened from
    ' the page still counts the page's unsaved authors: the author just
    ' removed there is used by no manuscript until the import, and then by
    ' the work imported for them, so Delete Author refuses.
    <TestMethod>
    Public Sub AnOrcidImportFromTheManuscriptPageThenSaveKeepsTheEditAndTheWorks()
        StaRunner.Run(
            Sub()
                Dim author As AuthorRecord = SeedLinkedLibrary()

                Using board As LibraryBoard = StartBoard(),
                      dialogs As New ManuscriptPageTests.DialogWatcher()
                    board.Open(board.Library.Single())
                    RemoveTheOnlyAuthor(board.Editor)

                    Dim countUsage As Func(Of AuthorLibraryForm, Integer) =
                        Function(dialog) CInt(GetType(AuthorLibraryForm).GetMethod("CountAuthorUsage", BindingFlags.Instance Or BindingFlags.NonPublic).Invoke(dialog, New Object() {author.Id}))
                    Dim usage As Integer = -1
                    Dim usageAfter As Integer = -1
                    Assert.AreEqual(1, ImportFromThePage(board.Editor, CreateOptions(),
                        Sub(dialog) usage = countUsage(dialog),
                        Sub(dialog) usageAfter = countUsage(dialog)))
                    CollectionAssert.AreEqual({"ORCID Import Complete"}, dialogs.Closed, "The import went through.")
                    Assert.AreEqual(0, usage, "Usage counts read the page's working copy, where the author was removed.")
                    Assert.AreEqual(1, usageAfter, "After the import the work imported for the author counts, so the author cannot be deleted.")
                    Assert.IsTrue(PageRecords(board.Editor).Any(Function(item) item.Title = WorkTitle), "The page's copy of the records holds the work.")

                    Assert.IsTrue(board.SaveManuscriptPage(), "The page saved.")
                    Dim afterSave As List(Of Manuscript) = New ManuscriptRepository().Load()
                    Assert.AreEqual(0, afterSave.Single(Function(item) item.Title = ExistingTitle).Authors.Count, "Save wrote the page's edit.")
                    Assert.IsTrue(afterSave.Any(Function(item) item.Title = WorkTitle), "Save kept the imported work.")

                    Assert.IsTrue(board.SaveNow(), "A second save went through.")
                    Assert.IsTrue(New ManuscriptRepository().Load().Any(Function(item) item.Title = WorkTitle), "A second save keeps the imported work.")

                    board.SetPrompt(Function(title) System.Windows.Forms.DialogResult.No)
                    board.Close()
                End Using
            End Sub)
    End Sub

    ' Only the iD and an affiliation, from the page (#353): the author
    ' library is saved and manuscripts.json is not touched, though the
    ' page has an unsaved edit.
    <TestMethod>
    Public Sub AnIdOnlyOrcidApplyFromTheManuscriptPageLeavesTheManuscriptsFileAlone()
        StaRunner.Run(
            Sub()
                SeedLinkedLibrary()
                Dim manuscriptsFile As String = New ManuscriptRepository().DataFilePath
                Dim before As Byte() = File.ReadAllBytes(manuscriptsFile)

                Using board As LibraryBoard = StartBoard(),
                      dialogs As New ManuscriptPageTests.DialogWatcher()
                    board.Open(board.Library.Single())
                    RemoveTheOnlyAuthor(board.Editor)

                    Dim idOnly As New OrcidApplyOptions With {.AddAffiliations = True, .SelectedWorkPutCodes = New List(Of Long)()}
                    Assert.AreEqual(1, ImportFromThePage(board.Editor, idOnly))

                    CollectionAssert.AreEqual(before, File.ReadAllBytes(manuscriptsFile), "manuscripts.json is byte for byte as it was.")
                    Assert.AreEqual(OrcidId, New AuthorLibraryRepository().Load().Authors.Single().Orcid, "The iD is saved in the author library.")
                    Assert.AreEqual(1, board.Library.Count, "No work was imported.")

                    board.SetPrompt(Function(title) System.Windows.Forms.DialogResult.No)
                    board.Close()
                End Using
            End Sub)
    End Sub

    ' An import from the page whose works save fails (#353, #243): the works
    ' come back out of the main window's list, which the import went into,
    ' the page's copy never gets them, nothing tells the main window, and
    ' manuscripts.json is as it was; the page's Save then writes no work.
    <TestMethod>
    Public Sub AnOrcidImportFromTheManuscriptPageWhoseSaveFailsChangesNothing()
        StaRunner.Run(
            Sub()
                SeedLinkedLibrary()
                Dim manuscriptsFile As String = New ManuscriptRepository().DataFilePath
                Dim before As Byte() = File.ReadAllBytes(manuscriptsFile)

                Using board As LibraryBoard = StartBoard(),
                      dialogs As New ManuscriptPageTests.DialogWatcher()
                    board.Open(board.Library.Single())
                    Dim raised As Integer = 0
                    AddHandler board.Editor.ManuscriptsImported, Sub(sender, e) raised += 1

                    Dim blocker As String = Path.Combine(Path.GetDirectoryName(manuscriptsFile), "manuscripts.tmp")
                    Directory.CreateDirectory(blocker)
                    Assert.AreEqual(1, ImportFromThePage(board.Editor, CreateOptions()))
                    Directory.Delete(blocker)

                    CollectionAssert.AreEqual({"ORCID Import Error"}, dialogs.Closed, "The import failed.")
                    Assert.AreEqual(1, board.Library.Count, "The work comes back out of the main window's list.")
                    Assert.IsFalse(PageRecords(board.Editor).Any(Function(item) item.Title = WorkTitle), "The page's copy of the records never gets the work.")
                    Assert.AreEqual(0, raised, "The main window is not told of an import.")
                    CollectionAssert.AreEqual(before, File.ReadAllBytes(manuscriptsFile), "manuscripts.json is byte for byte as it was.")

                    Assert.IsTrue(board.SaveManuscriptPage(), "The page saved.")
                    Assert.IsFalse(New ManuscriptRepository().Load().Any(Function(item) item.Title = WorkTitle), "The page's Save writes no work.")

                    board.SetPrompt(Function(title) System.Windows.Forms.DialogResult.No)
                    board.Close()
                End Using
            End Sub)
    End Sub

    ' A manuscript the library does not hold yet, on the page (#353): the
    ' works go into the library given and onto disk, and the new
    ' manuscript, never saved, is not written by the import.
    <TestMethod>
    Public Sub AnOrcidImportFromThePageOfANewManuscriptDoesNotWriteIt()
        StaRunner.Run(
            Sub()
                Dim author As AuthorRecord = SeedLinkedLibrary()
                Dim live As List(Of Manuscript) = New ManuscriptRepository().Load()
                Dim unsaved As New Manuscript With {
                    .Title = "Never saved",
                    .Authors = New List(Of ManuscriptAuthor) From {New ManuscriptAuthor With {.AuthorId = author.Id}}
                }

                Using editor As New EditManuscriptForm(unsaved, live, New AuthorLibraryRepository(), pageMode:=True),
                      dialogs As New ManuscriptPageTests.DialogWatcher()
                    ViewCatalog.ShowOffscreen(editor)

                    Assert.AreEqual(1, ImportFromThePage(editor, CreateOptions()))
                    CollectionAssert.AreEqual({"ORCID Import Complete"}, dialogs.Closed, "The import went through.")

                    CollectionAssert.AreEquivalent({ExistingTitle, WorkTitle}, New ManuscriptRepository().Load().Select(Function(item) item.Title).ToList(), "Disk holds the library and the work, not the new manuscript.")
                    CollectionAssert.AreEquivalent({ExistingTitle, WorkTitle}, live.Select(Function(item) item.Title).ToList(), "The library given holds the work.")
                    Assert.IsTrue(PageRecords(editor).Any(Function(item) item.Title = WorkTitle), "The page's copy of the records holds the work.")
                    editor.Close()
                End Using
            End Sub)
    End Sub

    ' The works imported from the page show on the Board when the user goes
    ' back to it without saving anything (#353).
    <TestMethod>
    Public Sub OrcidWorksImportedFromTheManuscriptPageShowOnTheBoard()
        StaRunner.Run(
            Sub()
                SeedLinkedLibrary()

                Using board As LibraryBoard = StartBoard(),
                      dialogs As New ManuscriptPageTests.DialogWatcher()
                    ' Every shelf built first: building one later renders
                    ' the Board again, which would show the work anyway.
                    board.WaitForBoardFill()
                    board.WaitForIdleWork()
                    board.Open(board.Library.Single())
                    Assert.AreEqual(1, ImportFromThePage(board.Editor, CreateOptions()))

                    board.SetPrompt(Function(title) System.Windows.Forms.DialogResult.No)
                    board.NavigateTo(Form1.WorkspacePage.Board)
                    System.Windows.Forms.Application.DoEvents()
                    board.WaitForBoardFill()

                    Dim titles As List(Of String) =
                        {"pipelinePanel", "publishedPanel", "fileDrawerPanel"}.
                            SelectMany(Function(shelf) board.Shelf(shelf).Controls.OfType(Of ManuscriptPipeline.Controls.RoundedPanel)()).
                            Select(Function(card) card.AccessibleName).ToList()
                    CollectionAssert.Contains(titles, WorkTitle, "The Board shows the imported work: " & String.Join(" | ", titles))

                    board.Close()
                End Using
            End Sub)
    End Sub

    ' Authors & Affiliations, opened from the manuscript page, saves the
    ' author library at once: here an ORCID iD and affiliation, and a new
    ' person, through the window's own saves. The page is then left with
    ' nothing to save, and Import BibTeX / RIS... imports a record naming
    ' that new person. The import must keep what the window saved and name
    ' the person it saved, not write back the main window's copy of the
    ' library as read before the page opened.
    <TestMethod>
    Public Sub ABibliographyImportAfterLeavingTheManuscriptPageKeepsWhatManageLibrarySaved()
        StaRunner.Run(
            Sub()
                Dim author As AuthorRecord = SeedLinkedLibrary()
                Dim added As New AuthorRecord With {.GivenName = "Dana", .FamilyName = "Whitfield"}

                Using board As LibraryBoard = StartBoard(),
                      dialogs As New ManuscriptPageTests.DialogWatcher()
                    board.Open(board.Library.Single())

                    Dim idOnly As New OrcidApplyOptions With {.AddAffiliations = True, .SelectedWorkPutCodes = New List(Of Long)()}
                    Assert.AreEqual(1, ImportFromThePage(board.Editor, idOnly, afterImport:=
                        Sub(dialog)
                            LibraryOf(dialog).Authors.Add(added)
                            GetType(AuthorLibraryForm).GetMethod("SaveLibrary", BindingFlags.Instance Or BindingFlags.NonPublic).Invoke(dialog, Nothing)
                        End Sub))
                    Dim afterWindow As AuthorLibraryData = New AuthorLibraryRepository().Load()
                    Assert.AreEqual(OrcidId, afterWindow.Authors.Single(Function(item) item.Id = author.Id).Orcid, "Authors & Affiliations saved the iD.")
                    Assert.IsTrue(afterWindow.Authors.Any(Function(item) item.Id = added.Id), "Authors & Affiliations saved the new person.")
                    Assert.AreEqual(1, afterWindow.Affiliations.Count, "Authors & Affiliations saved the affiliation.")

                    LeaveThePageWithNothingToSave(board)
                    ImportOneBibRecord(board, "Whitfield, Dana")
                    CollectionAssert.Contains(dialogs.Closed, "Import Complete", "The import went through: " & String.Join(", ", dialogs.Closed))

                    Dim failures As New List(Of String)()
                    Dim saved As AuthorLibraryData = New AuthorLibraryRepository().Load()
                    Dim local As AuthorRecord = saved.Authors.SingleOrDefault(Function(item) item.Id = author.Id)
                    If local Is Nothing OrElse local.Orcid <> OrcidId Then
                        failures.Add("(1) After the import the author library on disk has lost the ORCID iD saved from the page.")
                    End If
                    If saved.Affiliations.Count <> 1 Then
                        failures.Add("(2) After the import the author library on disk has " & saved.Affiliations.Count.ToString() & " affiliations, not the 1 saved from the page.")
                    End If
                    If Not saved.Authors.Any(Function(item) item.Id = added.Id) Then
                        failures.Add("(3) After the import the person added from the page is gone from disk.")
                    End If
                    Dim whitfields As Integer = saved.Authors.Where(Function(item) item.FamilyName = "Whitfield").Count()
                    If whitfields <> 1 Then
                        failures.Add("(4) The author library on disk has " & whitfields.ToString() & " people named Whitfield, not 1.")
                    End If
                    Dim importedStudy As Manuscript = New ManuscriptRepository().Load().Single(Function(item) item.Title = BibTitle)
                    If Not importedStudy.Authors.Any(Function(link) link.AuthorId = added.Id) Then
                        failures.Add("(5) The imported record does not name the person the page's window saved.")
                    End If

                    board.Close()
                    Assert.AreEqual(0, failures.Count, String.Join(Environment.NewLine, failures))
                End Using
            End Sub)
    End Sub

    ' Opened from the manuscript page after an unsaved Remove on its
    ' Authors tab, Authors & Affiliations must not delete the person the
    ' saved manuscript still names: the page's edits can still be
    ' discarded, and the saved manuscript would then name a person who is
    ' gone, shown as [Missing author record].
    <TestMethod>
    Public Sub APersonTheSavedManuscriptNamesCannotBeDeletedFromThePageAfterAnUnsavedRemove()
        DeleteFromThePageAfterAnUnsavedRemove("lstAuthors", "DeleteSelectedAuthor", "Delete Author", "Author Is In Use")
    End Sub

    ' The same for the affiliation that person's assignment on the saved
    ' manuscript names.
    <TestMethod>
    Public Sub AnAffiliationTheSavedManuscriptNamesCannotBeDeletedFromThePageAfterAnUnsavedRemove()
        DeleteFromThePageAfterAnUnsavedRemove("lstAffiliations", "DeleteSelectedAffiliation", "Delete Affiliation", "Affiliation Is In Use")
    End Sub

    ' An import whose author library cannot be read imports nothing and
    ' writes nothing: the read says what failed, and the damaged file is
    ' left as it is, not replaced by the main window's copy.
    <TestMethod>
    Public Sub ABibliographyImportWhoseAuthorLibraryCannotBeReadImportsNothing()
        StaRunner.Run(
            Sub()
                SeedLinkedLibrary()

                Using board As LibraryBoard = StartBoard(),
                      dialogs As New ManuscriptPageTests.DialogWatcher()
                    Dim authors As New AuthorLibraryRepository()
                    File.WriteAllText(authors.DataFilePath, "{ not json")
                    If File.Exists(authors.BackupFilePath) Then File.Delete(authors.BackupFilePath)
                    Dim authorsBefore As Byte() = File.ReadAllBytes(authors.DataFilePath)
                    Dim manuscriptsBefore As Byte() = File.ReadAllBytes(New ManuscriptRepository().DataFilePath)

                    ImportOneBibRecord(board, "Whitfield, Dana")

                    Assert.AreEqual(1, board.StartupFailures.Count, "Reading the author library said what failed.")
                    Assert.IsFalse(dialogs.Closed.Contains("Import Complete"), "Nothing says the import completed: " & String.Join(", ", dialogs.Closed))
                    CollectionAssert.AreEqual(authorsBefore, File.ReadAllBytes(authors.DataFilePath), "The author library file is left as it was.")
                    CollectionAssert.AreEqual(manuscriptsBefore, File.ReadAllBytes(New ManuscriptRepository().DataFilePath), "manuscripts.json is left as it was.")
                    Assert.AreEqual(1, board.Library.Count, "Nothing was imported.")

                    board.Close()
                End Using
            End Sub)
    End Sub

    ' One author with one affiliation on disk, and a manuscript naming the
    ' author with that affiliation. On the page: Remove takes the author
    ' off; then Manage Library, Delete on the one item of the list named,
    ' answered Yes; then Discard. The person and the affiliation are on
    ' disk still, Delete was refused, and the page names no missing person.
    Private Sub DeleteFromThePageAfterAnUnsavedRemove(listField As String, deleteMethod As String, deleteTitle As String, inUseTitle As String)
        StaRunner.Run(
            Sub()
                Dim seeded As (Author As AuthorRecord, Affiliation As AffiliationRecord) = SeedAffiliatedLibrary()

                Using board As LibraryBoard = StartBoard(),
                      dialogs As New ManuscriptPageTests.DialogWatcher(deleteTitle)
                    board.Open(board.Library.Single())
                    RemoveTheOnlyAuthor(board.Editor)

                    Assert.AreEqual(1, DeleteFromThePage(board.Editor, listField, deleteMethod, Function(item) True))
                    board.Discard()

                    Dim failures As New List(Of String)()
                    Dim saved As AuthorLibraryData = New AuthorLibraryRepository().Load()
                    If Not saved.Authors.Any(Function(item) item.Id = seeded.Author.Id) Then
                        failures.Add("(1) After Discard the saved manuscript names a person no longer in the author library.")
                    End If
                    If Not saved.Affiliations.Any(Function(item) item.Id = seeded.Affiliation.Id) Then
                        failures.Add("(2) After Discard the saved manuscript names an affiliation no longer in the author library.")
                    End If
                    If Not dialogs.Closed.Contains(inUseTitle) Then
                        failures.Add("(3) Delete was not refused; the dialogs were: " & String.Join(", ", dialogs.Closed))
                    End If
                    Dim shown As List(Of String) = AuthorsShown(board.Editor)
                    If shown.Count <> 1 OrElse shown.Any(Function(text) text.Contains("[Missing author record]")) Then
                        failures.Add("(4) After Discard the page's Authors tab shows: " & String.Join(" | ", shown))
                    End If

                    board.SetPrompt(Function(title) System.Windows.Forms.DialogResult.No)
                    board.Close()
                    Assert.AreEqual(0, failures.Count, String.Join(Environment.NewLine, failures))
                End Using
            End Sub)
    End Sub

    Private Const BibTitle As String = "A later imported study"

    ' Leaves the manuscript page for Import & Export, as the rail does;
    ' nothing on the page is left to save, so nothing is asked.
    Private Shared Sub LeaveThePageWithNothingToSave(board As LibraryBoard)
        Dim asked As Integer = 0
        board.SetPrompt(
            Function(title)
                asked += 1
                Return System.Windows.Forms.DialogResult.No
            End Function)
        board.NavigateTo(Form1.WorkspacePage.ImportExport)
        System.Windows.Forms.Application.DoEvents()
        Assert.AreEqual(0, asked, "Nothing on the page was left to save.")
        Assert.AreEqual("ImportExport", board.PageName)
    End Sub

    ' Import BibTeX / RIS... with a file of one record by the author given;
    ' the review window is answered Import Selected.
    Private Sub ImportOneBibRecord(board As LibraryBoard, bibAuthor As String)
        Dim bibPath As String = Path.Combine(_root, "one-record.bib")
        File.WriteAllText(bibPath, "@article{later2026, title={" & BibTitle & "}, author={" & bibAuthor & "}, journal={Fictional Journal of Methods}, year={2026}}")
        Using answer As New SubmissionWorkflowConsistencyTests.ModalAnswer(Of BibliographyImportForm)(
            Sub(dialog) DirectCast(dialog.AcceptButton, System.Windows.Forms.Button).PerformClick())
            board.ImportBibliographyFile(bibPath)
        End Using
        System.Windows.Forms.Application.DoEvents()
    End Sub

    ' Manage Library on the page (its Click handler); in the window it
    ' opens, inspect runs first, then the first item of the list named that
    ' pick accepts is selected and the Delete method named runs, and the
    ' window is closed. Returns how many times the window opened.
    Private Shared Function DeleteFromThePage(editor As EditManuscriptForm, listField As String, deleteMethod As String, pick As Func(Of Object, Boolean), Optional inspect As Action(Of AuthorLibraryForm) = Nothing) As Integer
        Dim opened As Integer = 0
        Using answer As New SubmissionWorkflowConsistencyTests.ModalAnswer(Of AuthorLibraryForm)(
            Sub(dialog)
                opened += 1
                inspect?.Invoke(dialog)
                Dim list As System.Windows.Forms.ListBox = DirectCast(GetType(AuthorLibraryForm).GetField(listField, BindingFlags.Instance Or BindingFlags.NonPublic).GetValue(dialog), System.Windows.Forms.ListBox)
                Dim index As Integer = list.Items.Cast(Of Object)().ToList().FindIndex(Function(item) pick(item))
                If index >= 0 Then
                    list.SelectedIndex = index
                    GetType(AuthorLibraryForm).GetMethod(deleteMethod, BindingFlags.Instance Or BindingFlags.NonPublic).Invoke(dialog, New Object() {Nothing, EventArgs.Empty})
                End If
                dialog.Close()
            End Sub)
            GetType(EditManuscriptForm).GetMethod("ManageAuthorLibrary", BindingFlags.Instance Or BindingFlags.NonPublic).
                Invoke(editor, New Object() {Nothing, EventArgs.Empty})
        End Using
        System.Windows.Forms.Application.DoEvents()
        Return opened
    End Function

    ' What the page's Authors tab lists.
    Private Shared Function AuthorsShown(editor As EditManuscriptForm) As List(Of String)
        Dim authorsList As System.Windows.Forms.ListBox =
            DirectCast(GetType(EditManuscriptForm).GetField("lstAuthors", BindingFlags.Instance Or BindingFlags.NonPublic).GetValue(editor), System.Windows.Forms.ListBox)
        Return authorsList.Items.Cast(Of Object)().Select(Function(item) authorsList.GetItemText(item)).ToList()
    End Function

    ' One author with one affiliation on disk, and one manuscript that
    ' names that author with that affiliation.
    Private Shared Function SeedAffiliatedLibrary() As (Author As AuthorRecord, Affiliation As AffiliationRecord)
        Dim library As New AuthorLibraryData()
        Dim author As New AuthorRecord With {.GivenName = "Local", .FamilyName = "Author"}
        Dim affiliation As New AffiliationRecord With {.Institution = "Seeded University", .Country = "USA"}
        library.Authors.Add(author)
        library.Affiliations.Add(affiliation)
        Dim authors As New AuthorLibraryRepository()
        authors.Save(library)
        Dim manuscripts As New ManuscriptRepository()
        manuscripts.Save(New List(Of Manuscript) From {
            New Manuscript With {
                .Title = ExistingTitle,
                .Authors = New List(Of ManuscriptAuthor) From {
                    New ManuscriptAuthor With {.AuthorId = author.Id, .AffiliationIds = New List(Of Guid) From {affiliation.Id}}
                }
            }
        })
        Return (author, affiliation)
    End Function

    ' One author on disk, and one manuscript that names that author.
    Private Shared Function SeedLinkedLibrary() As AuthorRecord
        Dim library As New AuthorLibraryData()
        Dim author As New AuthorRecord With {.GivenName = "Local", .FamilyName = "Author"}
        library.Authors.Add(author)
        Dim authors As New AuthorLibraryRepository()
        authors.Save(library)
        Dim manuscripts As New ManuscriptRepository()
        manuscripts.Save(New List(Of Manuscript) From {
            New Manuscript With {
                .Title = ExistingTitle,
                .Authors = New List(Of ManuscriptAuthor) From {New ManuscriptAuthor With {.AuthorId = author.Id}}
            }
        })
        Return author
    End Function

    ' The main window, started on the files under the override, as the
    ' window it opens reads and writes them.
    Private Function StartBoard() As LibraryBoard
        Dim files As New LargeLibraryFixture.SyntheticLibrary With {
            .Name = "orcid-import-page",
            .ManagedDirectory = Path.Combine(_root, "managed"),
            .Repository = New ManuscriptRepository(),
            .AuthorRepository = New AuthorLibraryRepository()
        }
        Dim board As New LibraryBoard(files, Path.Combine(_root, "settings"))
        board.Start()
        Return board
    End Function

    ' Remove on the page's Authors tab, with its one author selected.
    Private Shared Sub RemoveTheOnlyAuthor(editor As EditManuscriptForm)
        Dim authorsList As System.Windows.Forms.ListBox =
            DirectCast(GetType(EditManuscriptForm).GetField("lstAuthors", BindingFlags.Instance Or BindingFlags.NonPublic).GetValue(editor), System.Windows.Forms.ListBox)
        authorsList.SelectedIndex = 0
        GetType(EditManuscriptForm).GetMethod("RemoveStructuredAuthor", BindingFlags.Instance Or BindingFlags.NonPublic).
            Invoke(editor, New Object() {Nothing, EventArgs.Empty})
        Assert.AreEqual(0, authorsList.Items.Count, "Remove took the author off the page.")
    End Sub

    ' Manage Library on the page (its Click handler); in the window it
    ' opens, inspect runs first, then ORCID... is applied to the one author
    ' with the options given. Returns how many times it was applied.
    Private Shared Function ImportFromThePage(editor As EditManuscriptForm, options As OrcidApplyOptions, Optional inspect As Action(Of AuthorLibraryForm) = Nothing, Optional afterImport As Action(Of AuthorLibraryForm) = Nothing) As Integer
        Dim applied As Integer = 0
        Using answer As New SubmissionWorkflowConsistencyTests.ModalAnswer(Of AuthorLibraryForm)(
            Sub(dialog)
                inspect?.Invoke(dialog)
                dialog.ApplyOrcidChanges(LibraryOf(dialog).Authors.Single(), CreateProfile(), options)
                afterImport?.Invoke(dialog)
                applied += 1
                dialog.Close()
            End Sub)
            GetType(EditManuscriptForm).GetMethod("ManageAuthorLibrary", BindingFlags.Instance Or BindingFlags.NonPublic).
                Invoke(editor, New Object() {Nothing, EventArgs.Empty})
        End Using
        System.Windows.Forms.Application.DoEvents()
        Return applied
    End Function

    ' The page's own copy of the library's records.
    Private Shared Function PageRecords(editor As EditManuscriptForm) As List(Of Manuscript)
        Return DirectCast(GetType(EditManuscriptForm).GetProperty("_allManuscripts", BindingFlags.Instance Or BindingFlags.NonPublic).GetValue(editor), List(Of Manuscript))
    End Function

    ' One author and one manuscript on disk, and the live list read back
    ' from it, as the window holds it.
    Private Shared Function Seed() As List(Of Manuscript)
        Dim library As New AuthorLibraryData()
        library.Authors.Add(New AuthorRecord With {.GivenName = "Local", .FamilyName = "Author"})
        Dim authors As New AuthorLibraryRepository()
        authors.Save(library)

        Dim manuscripts As New ManuscriptRepository()
        manuscripts.Save(New List(Of Manuscript) From {New Manuscript With {.Title = ExistingTitle}})
        Return New ManuscriptRepository().Load()
    End Function

    ' The form's author library in memory.
    Private Shared Function LibraryOf(form As AuthorLibraryForm) As AuthorLibraryData
        Return DirectCast(GetType(AuthorLibraryForm).GetField("_library", BindingFlags.Instance Or BindingFlags.NonPublic).GetValue(form), AuthorLibraryData)
    End Function

    ' OrcidApplyServiceTests' profile: one affiliation and one work.
    Private Shared Function CreateProfile() As OrcidProfileSuggestion
        Dim profile As New OrcidProfileSuggestion With {
            .Orcid = OrcidId,
            .GivenName = "Remote",
            .FamilyName = "Researcher",
            .CreditName = "R. Researcher"
        }
        profile.Affiliations.Add(
            New OrcidAffiliationSuggestion With {
                .Institution = "Example University",
                .Department = "Psychology",
                .City = "Hartford",
                .Region = "CT",
                .Country = "US"
            })
        profile.Works.Add(
            New OrcidWorkSuggestion With {
                .PutCode = 42,
                .Title = WorkTitle,
                .WorkType = "JOURNAL_ARTICLE",
                .Doi = "10.1234/imported",
                .PublishedDate = New DateTime(2025, 1, 1)
            })
        Return profile
    End Function

    Private Shared Function CreateOptions() As OrcidApplyOptions
        Return New OrcidApplyOptions With {
            .AddAffiliations = True,
            .SelectedWorkPutCodes = New List(Of Long) From {42}
        }
    End Function

End Class
