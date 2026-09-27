Imports System.Drawing
Imports System.IO
Imports System.Linq
Imports System.Reflection
Imports System.Runtime.ExceptionServices
Imports System.Threading
Imports System.Windows.Forms
Imports ManuscriptPipeline
Imports ManuscriptPipeline.Forms
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services
Imports Microsoft.VisualStudio.TestTools.UnitTesting

' The manuscript page (#55): one working copy per open manuscript, saved or
' discarded explicitly, and never lost silently when leaving.
<TestClass>
<DoNotParallelize>
Public Class ManuscriptPageTests

    <TestMethod>
    Public Sub OpensAsAPageAndGoesBackToWhereItWasOpened()
        RunOnStaThread(
            Sub()
                Using board As New PageBoard()
                    Dim first As Manuscript = Sample("First manuscript")
                    board.Prepare(first)

                    board.Open(first)
                    Assert.AreEqual("Manuscript", board.PageName)
                    Assert.IsTrue(board.RailPage("Board").Checked, "The rail keeps showing the page the manuscript was opened from.")
                    Assert.IsFalse(board.Editor.HasUnsavedChanges(), "Opening a manuscript changes nothing.")
                    Assert.IsFalse(Descendants(board.Editor).OfType(Of Button)().Any(Function(button) button.Text.Contains("Save") AndAlso button.Text.Contains("Close")),
                        "The page has no Save & Close footer.")
                    CollectionAssert.AreEqual(
                        {"Overview", "Authors", "Versions", "Submissions", "Readiness & Packets"},
                        Descendants(board.Editor).OfType(Of RadioButton)().Select(Function(tab) tab.Text).ToList())

                    board.PressCommandKey(Keys.Alt Or Keys.Left)
                    Assert.AreEqual("Board", board.PageName)
                    Assert.AreEqual(0, board.SaveCount, "Viewing never saves.")

                    board.Close()
                End Using
            End Sub)
    End Sub

    <TestMethod>
    Public Sub SaveKeepsEditsAndDiscardReturnsToTheSavedVersion()
        RunOnStaThread(
            Sub()
                Using board As New PageBoard()
                    Dim manuscript As Manuscript = Sample("Original title")
                    board.Prepare(manuscript)
                    board.Open(manuscript)

                    board.Field("Title").Text = "Saved title"
                    Assert.IsTrue(board.Editor.HasUnsavedChanges())
                    Assert.AreEqual("Original title", manuscript.Title, "Edits stay in the working copy until saved.")
                    board.Tick()
                    Assert.IsTrue(board.SaveBarVisible, "The Unsaved changes bar appears.")

                    board.PressCommandKey(Keys.Control Or Keys.S)
                    Assert.AreEqual("Saved title", manuscript.Title)
                    Assert.AreEqual(1, board.SaveCount)
                    Assert.IsFalse(board.Editor.HasUnsavedChanges())
                    Assert.IsFalse(board.SaveBarVisible)

                    board.Field("Title").Text = "Discarded title"
                    board.Discard()
                    Assert.AreEqual("Saved title", manuscript.Title, "Discard leaves the library unchanged.")
                    Assert.AreEqual("Saved title", board.Field("Title").Text, "The page shows the saved version again.")
                    Assert.IsFalse(board.Editor.HasUnsavedChanges())
                    Assert.AreEqual(1, board.SaveCount, "Discard never saves.")

                    board.Close()
                End Using
            End Sub)
    End Sub

    <TestMethod>
    Public Sub LeavingWithUnsavedChangesAsksToSaveDiscardOrStay()
        RunOnStaThread(
            Sub()
                Using board As New PageBoard()
                    Dim manuscript As Manuscript = Sample("Original title")
                    board.Prepare(manuscript)
                    board.Open(manuscript)
                    board.Field("Title").Text = "Edited title"

                    Dim asked As Integer = 0
                    board.SetPrompt(Function(title)
                                       asked += 1
                                       Return DialogResult.Cancel
                                   End Function)
                    board.PressCommandKey(Keys.Control Or Keys.D2)
                    Assert.AreEqual(1, asked)
                    Assert.AreEqual("Manuscript", board.PageName, "Cancel stays on the page.")
                    Assert.IsTrue(board.RailPage("Board").Checked, "The rail returns to the page's origin.")
                    Assert.AreEqual("Edited title", board.Field("Title").Text, "Staying keeps the edits.")

                    board.SetPrompt(Function(title) DialogResult.No)
                    board.PressCommandKey(Keys.Alt Or Keys.Left)
                    Assert.AreEqual("Board", board.PageName)
                    Assert.AreEqual("Original title", manuscript.Title, "No discards the edits.")
                    Assert.AreEqual(0, board.SaveCount)

                    board.Open(manuscript)
                    board.Field("Title").Text = "Kept title"
                    board.SetPrompt(Function(title) DialogResult.Yes)
                    board.PressCommandKey(Keys.Control Or Keys.D5)
                    Assert.AreEqual("ImportExport", board.PageName)
                    Assert.AreEqual("Kept title", manuscript.Title, "Yes saves before leaving.")
                    Assert.AreEqual(1, board.SaveCount)

                    board.Close()
                End Using
            End Sub)
    End Sub

    <TestMethod>
    Public Sub OpeningAnotherManuscriptProtectsTheOneBeingEdited()
        RunOnStaThread(
            Sub()
                Using board As New PageBoard()
                    Dim first As Manuscript = Sample("First")
                    Dim second As Manuscript = Sample("Second")
                    board.Prepare(first, second)
                    board.Open(first)
                    board.Field("Title").Text = "First, edited"

                    board.SetPrompt(Function(title) DialogResult.Cancel)
                    board.Open(second)
                    Assert.AreEqual("First, edited", board.Field("Title").Text, "Cancel keeps the first manuscript open.")

                    board.SetPrompt(Function(title) DialogResult.No)
                    board.Open(second)
                    Assert.AreEqual("Second", board.Field("Title").Text)
                    Assert.AreEqual("First", first.Title)

                    board.PressCommandKey(Keys.Alt Or Keys.Left)
                    Assert.AreEqual("First", board.Field("Title").Text, "Back returns to the first manuscript, as saved.")

                    board.Close()
                End Using
            End Sub)
    End Sub

    <TestMethod>
    Public Sub ClosingPaperRouteWithUnsavedChangesAsks()
        RunOnStaThread(
            Sub()
                Using board As New PageBoard()
                    Dim manuscript As Manuscript = Sample("Original title")
                    board.Prepare(manuscript)
                    board.Open(manuscript)
                    board.Field("Title").Text = "Edited title"

                    board.SetPrompt(Function(title) DialogResult.Cancel)
                    board.Close()
                    Assert.IsFalse(board.IsDisposed, "Cancel keeps PaperRoute open.")
                    Assert.IsTrue(board.Visible)

                    board.SetPrompt(Function(title) DialogResult.Yes)
                    board.Close()
                    Assert.AreEqual("Edited title", manuscript.Title, "Yes saves before closing.")
                    Assert.AreEqual(1, board.SaveCount)
                End Using
            End Sub)
    End Sub

    <TestMethod>
    Public Sub DeletingFromThePageRemovesTheManuscriptAndReturns()
        RunOnStaThread(
            Sub()
                Using board As New PageBoard()
                    Dim keep As Manuscript = Sample("Keep")
                    Dim remove As Manuscript = Sample("Remove")
                    board.Prepare(keep, remove)
                    board.Open(remove)
                    board.Field("Title").Text = "Edits on a deleted manuscript are not asked about"

                    Dim asked As Boolean = False
                    board.SetPrompt(Function(title)
                                       asked = True
                                       Return DialogResult.Cancel
                                   End Function)
                    board.ConfirmDelete()
                    Application.DoEvents()

                    Assert.IsFalse(asked)
                    Assert.AreEqual("Board", board.PageName)
                    CollectionAssert.AreEqual({"Keep"}, board.Library.Select(Function(item) item.Title).ToList())
                    Assert.AreEqual(1, board.SaveCount)

                    board.PressCommandKey(Keys.Alt Or Keys.Right)
                    Assert.AreEqual("Board", board.PageName, "History skips the deleted manuscript.")

                    board.Close()
                End Using
            End Sub)
    End Sub

    <TestMethod>
    Public Sub OverviewShowsTheLinkedJournalsNotesAndChecklist()
        RunOnStaThread(
            Sub()
                Dim directory As String = Path.Combine(Path.GetTempPath(), "PaperRoute-JournalNotes-" & Guid.NewGuid().ToString("N"))
                Try
                    Dim journal As New JournalRecord With {.Name = "Fictional Methods Review", .Notes = "Report exact p values."}
                    journal.ReadinessChecklistTemplate.Add(New JournalChecklistTemplateItem With {.Title = "Data availability statement"})
                    Dim library As New AuthorLibraryData()
                    library.Journals.Add(journal)
                    Dim repository As New AuthorLibraryRepository(directory)
                    repository.Save(library)

                    Dim linked As Manuscript = Sample("Linked")
                    linked.TargetJournal = journal.Name
                    linked.TargetJournalId = journal.Id

                    Using editor As New EditManuscriptForm(linked, {linked}, repository, pageMode:=True)
                        ShowOffscreen(editor)
                        Dim notes As GroupBox = Descendants(editor).OfType(Of GroupBox)().Single(Function(group) group.Text = "Notes for Fictional Methods Review")
                        Assert.IsTrue(notes.Visible)
                        Dim text As String = notes.Controls.OfType(Of Label)().Single().Text
                        StringAssert.Contains(text, "Report exact p values.")
                        StringAssert.Contains(text, "1 checklist item")
                        Assert.IsFalse(editor.HasUnsavedChanges(), "Showing notes changes nothing.")
                    End Using

                    Using editor As New EditManuscriptForm(Sample("Unlinked"), Nothing, repository, pageMode:=True)
                        ShowOffscreen(editor)
                        Assert.IsFalse(Descendants(editor).OfType(Of GroupBox)().Any(Function(group) group.Text.StartsWith("Notes for") AndAlso group.Visible),
                            "Without a linked journal record there are no notes to show.")
                    End Using
                Finally
                    If IO.Directory.Exists(directory) Then IO.Directory.Delete(directory, True)
                End Try
            End Sub)
    End Sub

    ' Children report Visible only while their form is shown.
    Private Shared Sub ShowOffscreen(form As Form)
        form.StartPosition = FormStartPosition.Manual
        form.Location = New Point(-20000, -20000)
        form.ShowInTaskbar = False
        form.Show()
        Application.DoEvents()
    End Sub

    Private Shared Function Sample(title As String) As Manuscript
        Return New Manuscript With {
            .Title = title,
            .Location = ManuscriptLocation.Pipeline,
            .CurrentStage = PaperStage.Draft,
            .TargetJournal = "A fictional journal"
        }
    End Function

    Private Shared Iterator Function Descendants(parent As Control) As IEnumerable(Of Control)
        For Each child As Control In parent.Controls
            Yield child
            For Each descendant As Control In Descendants(child)
                Yield descendant
            Next
        Next
    End Function

    ' The real board and manuscript page, on in-memory samples. Saving is
    ' counted instead of written, and the reusable author library is a
    ' throwaway directory.
    Private NotInheritable Class PageBoard
        Inherits Form1

        Private ReadOnly _authorDirectory As String =
            Path.Combine(Path.GetTempPath(), "PaperRoute-PageTests-" & Guid.NewGuid().ToString("N"))

        Public SaveCount As Integer

        Protected Overrides Sub OnLoad(e As EventArgs)
        End Sub

        Protected Overrides ReadOnly Property ShowWithoutActivation As Boolean
            Get
                Return True
            End Get
        End Property

        Protected Overrides Function CreateManuscriptEditor(manuscript As Manuscript) As EditManuscriptForm
            Directory.CreateDirectory(_authorDirectory)
            Return New EditManuscriptForm(manuscript, Library, New AuthorLibraryRepository(_authorDirectory), pageMode:=True)
        End Function

        Protected Overrides Function SaveManuscripts() As Boolean
            SaveCount += 1
            Return True
        End Function

        Protected Overrides Function LoadAuthorLibrary() As Boolean
            Return True
        End Function

        Public Sub Prepare(ParamArray samples As Manuscript())
            CallPrivate("BuildInterface")
            GetType(Form1).GetField("manuscripts", BindingFlags.Instance Or BindingFlags.NonPublic).SetValue(Me, samples.ToList())
            CallPrivate("RenderManuscripts")
            StartPosition = FormStartPosition.Manual
            Location = New Point(-20000, -20000)
            ShowInTaskbar = False
            Show()
            Application.DoEvents()
        End Sub

        Public ReadOnly Property Library As List(Of Manuscript)
            Get
                Return DirectCast(GetType(Form1).GetField("manuscripts", BindingFlags.Instance Or BindingFlags.NonPublic).GetValue(Me), List(Of Manuscript))
            End Get
        End Property

        Public Sub Open(manuscript As Manuscript)
            CallPrivate("OpenManuscript", manuscript, Nothing)
            Application.DoEvents()
        End Sub

        Public ReadOnly Property Editor As EditManuscriptForm
            Get
                Return DirectCast(GetType(Form1).GetField("manuscriptEditor", BindingFlags.Instance Or BindingFlags.NonPublic).GetValue(Me), EditManuscriptForm)
            End Get
        End Property

        Public Function Field(name As String) As TextBox
            Return Descendants(Editor).OfType(Of TextBox)().Single(Function(box) box.AccessibleName = name)
        End Function

        Public ReadOnly Property PageName As String
            Get
                Return GetType(Form1).GetField("currentPage", BindingFlags.Instance Or BindingFlags.NonPublic).GetValue(Me).ToString()
            End Get
        End Property

        Public Function RailPage(text As String) As RadioButton
            Dim buttons = DirectCast(GetType(Form1).GetField("railButtons", BindingFlags.Instance Or BindingFlags.NonPublic).GetValue(Me), System.Collections.IDictionary)
            Return buttons.Values.Cast(Of RadioButton)().Single(Function(button) button.Text = text)
        End Function

        ' A method rather than a property, which WinForms would try to serialize.
        Public Sub SetPrompt(value As Func(Of String, DialogResult))
            unsavedChangesPrompt = value
        End Sub

        Public ReadOnly Property SaveBarVisible As Boolean
            Get
                Dim bar = DirectCast(GetType(Form1).GetField("manuscriptSaveBar", BindingFlags.Instance Or BindingFlags.NonPublic).GetValue(Me), Control)
                Return bar IsNot Nothing AndAlso bar.Visible
            End Get
        End Property

        Public Sub Tick()
            CallPrivate("ManuscriptDirtyTimer_Tick", Nothing, EventArgs.Empty)
        End Sub

        Public Sub Discard()
            CallPrivate("DiscardManuscriptChanges")
            Application.DoEvents()
        End Sub

        Public Sub ConfirmDelete()
            CallPrivate("ManuscriptDeleteConfirmed", Nothing, EventArgs.Empty)
        End Sub

        Public Sub PressCommandKey(keyData As Keys)
            Dim message As New Message()
            GetType(Form1).GetMethod("ProcessCmdKey", BindingFlags.Instance Or BindingFlags.NonPublic).
                Invoke(Me, New Object() {message, keyData})
            Application.DoEvents()
        End Sub

        Private Sub CallPrivate(name As String, ParamArray args As Object())
            GetType(Form1).GetMethod(name, BindingFlags.Instance Or BindingFlags.NonPublic).Invoke(Me, args)
        End Sub

        Protected Overrides Sub Dispose(disposing As Boolean)
            MyBase.Dispose(disposing)
            Try
                If Directory.Exists(_authorDirectory) Then Directory.Delete(_authorDirectory, True)
            Catch ex As IOException
            End Try
        End Sub
    End Class

    Private Shared Sub RunOnStaThread(action As Action)
        Dim failure As Exception = Nothing
        Dim thread As New Thread(
            Sub()
                Try
                    action()
                Catch ex As Exception
                    failure = ex
                End Try
            End Sub) With {.IsBackground = True}
        thread.SetApartmentState(ApartmentState.STA)
        thread.Start()
        Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(90)), "Manuscript page test timed out.")
        If failure IsNot Nothing Then ExceptionDispatchInfo.Capture(failure).Throw()
    End Sub

End Class
