Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.IO
Imports System.Linq
Imports System.Reflection
Imports System.Windows.Forms
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline
Imports ManuscriptPipeline.Forms
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

' Import BibTeX/RIS... saves like every other save in the main window
' (#227): through SaveManuscripts, so the sweep of the managed root runs
' behind the disabled window while the status line says so, and a save
' that does not go through takes the imported records back out of the
' library, as the spreadsheet import does.
<TestClass>
<DoNotParallelize>
Public Class BibliographyImportUiTests

    Private _root As String = String.Empty

    Private Const FirstTitle As String = "A first imported study"

    Private Const SecondTitle As String = "A second imported study"

    Private Const ExistingTitle As String = "Already in the library"

    ' The main window's fields resolve the storage roots as it is built;
    ' the override keeps its saves off the user's profile.
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

    ' The import's save is the window's save: the status line says
    ' "Saving your library..." while the worker writes, and a fresh read of
    ' the library finds both records. Before the change the import wrote
    ' the library inline, on the UI thread, and the status line never said
    ' so.
    <TestMethod>
    Public Sub AnImportSavesThroughTheWindowsSave()
        StaRunner.Run(
            Sub()
                Using board As New ImportBoard(), dialogs As New ManuscriptPageTests.DialogWatcher()
                    board.Prepare(New Manuscript With {.Title = ExistingTitle})

                    Dim shown As New List(Of String)()
                    AddHandler board.StatusLabel().TextChanged, Sub(sender, e) shown.Add(board.StatusLabel().Text)

                    board.Import(WriteTwoRecords())

                    CollectionAssert.Contains(shown, "Saving your library...", "The status line said: " & String.Join(" / ", shown))
                    Assert.AreEqual(1, board.SaveCount, "One save, through SaveManuscripts.")
                    CollectionAssert.AreEqual({"Import Complete"}, dialogs.Closed)

                    CollectionAssert.AreEquivalent(
                        {ExistingTitle, FirstTitle, SecondTitle},
                        New ManuscriptRepository().Load().Select(Function(manuscript) manuscript.Title).ToList(),
                        "The library on disk has the two new manuscripts.")

                    board.Close()
                End Using
            End Sub)
    End Sub

    ' A save that does not go through takes the imported records back out,
    ' so a later save, made for something else, does not write records the
    ' researcher was told did not import. Before the change they stayed in
    ' the library in memory.
    <TestMethod>
    Public Sub AnImportWhoseSaveFailsTakesItsRecordsBackOut()
        StaRunner.Run(
            Sub()
                Using board As New ImportBoard(), dialogs As New ManuscriptPageTests.DialogWatcher()
                    Dim existing As New Manuscript With {.Title = ExistingTitle}
                    board.Prepare(existing)
                    board.SaveFails = True

                    board.Import(WriteTwoRecords())

                    Assert.AreEqual(1, board.Library.Count, "The library is back to the manuscript it had.")
                    Assert.AreSame(existing, board.Library.Single())

                    Dim imported As List(Of Guid) = board.IdsAtLastSave.Where(Function(id) id <> existing.Id).ToList()
                    Assert.AreEqual(2, imported.Count, "The import had added its two records when it saved.")
                    Assert.IsFalse(board.Library.Any(Function(manuscript) imported.Contains(manuscript.Id)), "No imported record remains.")
                    Assert.AreEqual(0, dialogs.Closed.Count, "Nothing says the import completed: " & String.Join(", ", dialogs.Closed))

                    board.SaveFails = False
                    Assert.IsTrue(board.SaveNow(), "A later save goes through.")

                    CollectionAssert.AreEqual(
                        {ExistingTitle},
                        New ManuscriptRepository().Load().Select(Function(manuscript) manuscript.Title).ToList(),
                        "The later save writes none of the imported records.")

                    board.Close()
                End Using
            End Sub)
    End Sub

    ' An import whose reusable-author save fails saves nothing, so none of
    ' it stays in memory either: neither the new manuscripts nor the new
    ' authors, which a later save of anything would otherwise write. The
    ' author save is made to fail by a folder where its temporary file
    ' goes. Before the change the library held 3 manuscripts and the
    ' author library 2 new authors after the failure.
    <TestMethod>
    Public Sub AnImportWhoseAuthorSaveFailsTakesItsRecordsBackOut()
        StaRunner.Run(
            Sub()
                Using board As New ImportBoard(), dialogs As New ManuscriptPageTests.DialogWatcher()
                    Dim existing As New Manuscript With {.Title = ExistingTitle}
                    board.Prepare(existing)

                    Dim blocker As String = New AuthorLibraryRepository().DataFilePath & ".tmp"
                    Directory.CreateDirectory(blocker)

                    board.Import(WriteTwoRecords())

                    CollectionAssert.AreEqual({"Bibliography Import Error"}, dialogs.Closed)
                    Assert.AreEqual(0, board.SaveCount, "The library is not saved.")
                    Assert.AreEqual(1, board.Library.Count, "The library is back to the manuscript it had.")
                    Assert.AreSame(existing, board.Library.Single())
                    Dim authors As AuthorLibraryData = board.AuthorLibrary
                    Assert.AreEqual(0, authors.Authors.Count, "No new author stays in memory.")

                    Directory.Delete(blocker)
                    Assert.IsTrue(board.SaveNow(), "A later save goes through.")
                    CollectionAssert.AreEqual(
                        {ExistingTitle},
                        New ManuscriptRepository().Load().Select(Function(manuscript) manuscript.Title).ToList(),
                        "The later save writes none of the imported records.")

                    Dim authorRepository As New AuthorLibraryRepository()
                    authorRepository.SaveKeepingJournals(authors)
                    Assert.IsFalse(
                        authorRepository.Load().Authors.Any(Function(author) author.FamilyName = "Whitfield" OrElse author.FamilyName = "Okafor"),
                        "A later author save writes none of the import's authors.")

                    board.Close()
                End Using
            End Sub)
    End Sub

    ' Two records, neither a duplicate of the library's manuscript, under
    ' the test's root.
    Private Function WriteTwoRecords() As String
        Dim bibPath As String = Path.Combine(_root, "two-records.bib")
        File.WriteAllText(
            bibPath,
            "@article{first2026, title={" & FirstTitle & "}, author={Whitfield, Dana}, journal={Fictional Journal of Methods}, year={2026}}" & Environment.NewLine &
            "@article{second2025, title={" & SecondTitle & "}, author={Okafor, Rui}, journal={Fictional Journal of Methods}, year={2025}}")
        Return bibPath
    End Function

    ' The real main window on in-memory records, as FileDrawerTests builds
    ' it: Load is skipped and the window never activates. Its saves are
    ' the window's own, written under the test's root, unless told to fail.
    Private NotInheritable Class ImportBoard
        Inherits Form1

        Public SaveCount As Integer

        ' A save that does not go through, as SaveManuscripts returns when
        ' a write fails or other library work is running.
        Public SaveFails As Boolean

        ' The library's records as the last save found them.
        Public IdsAtLastSave As New List(Of Guid)()

        Protected Overrides Sub OnLoad(e As EventArgs)
        End Sub

        Protected Overrides ReadOnly Property ShowWithoutActivation As Boolean
            Get
                Return True
            End Get
        End Property

        Protected Overrides Function SaveManuscripts() As Boolean
            SaveCount += 1
            IdsAtLastSave = Library.Select(Function(manuscript) manuscript.Id).ToList()
            If SaveFails Then
                Return False
            End If
            Return MyBase.SaveManuscripts()
        End Function

        Public Function SaveNow() As Boolean
            Return SaveManuscripts()
        End Function

        ' Import BibTeX/RIS... with this file chosen; the review window is
        ' answered Import Selected.
        Public Sub Import(bibPath As String)
            Using answer As New SubmissionWorkflowConsistencyTests.ModalAnswer(Of BibliographyImportForm)(
                Sub(dialog) DirectCast(dialog.AcceptButton, Button).PerformClick())
                ImportBibliographyFile(bibPath)
            End Using
            Application.DoEvents()
        End Sub

        Public ReadOnly Property Library As List(Of Manuscript)
            Get
                Return DirectCast(GetType(Form1).GetField("manuscripts", BindingFlags.Instance Or BindingFlags.NonPublic).GetValue(Me), List(Of Manuscript))
            End Get
        End Property

        ' The window's reusable author library in memory.
        Public ReadOnly Property AuthorLibrary As AuthorLibraryData
            Get
                Return DirectCast(GetType(Form1).GetField("authorLibrary", BindingFlags.Instance Or BindingFlags.NonPublic).GetValue(Me), AuthorLibraryData)
            End Get
        End Property

        Public Function StatusLabel() As Label
            Return DirectCast(GetType(Form1).GetField("lblStatus", BindingFlags.Instance Or BindingFlags.NonPublic).GetValue(Me), Label)
        End Function

        Public Sub Prepare(ParamArray records As Manuscript())
            CallPrivate("BuildInterface")
            GetType(Form1).GetField("manuscripts", BindingFlags.Instance Or BindingFlags.NonPublic).SetValue(Me, records.ToList())
            CallPrivate("RenderManuscripts")
            StartPosition = FormStartPosition.Manual
            Location = New Point(-20000, -20000)
            ShowInTaskbar = False
            Show()
            Application.DoEvents()
        End Sub

        Private Sub CallPrivate(name As String, ParamArray args As Object())
            GetType(Form1).GetMethod(name, BindingFlags.Instance Or BindingFlags.NonPublic).Invoke(Me, args)
        End Sub
    End Class

End Class
