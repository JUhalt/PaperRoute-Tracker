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
    ' (orcid-import-save probe). The page's working copy is the page's
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
