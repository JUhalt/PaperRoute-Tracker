Imports System.Drawing
Imports System.IO
Imports System.Linq
Imports System.Reflection
Imports System.Runtime.ExceptionServices
Imports System.Threading
Imports System.Windows.Forms
Imports ManuscriptPipeline
Imports ManuscriptPipeline.Controls
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

    <TestMethod>
    Public Sub SubmissionsShowTheListBesideTheSelectedSubmission()
        RunOnStaThread(
            Sub()
                Using board As New PageBoard()
                    Dim manuscript As Manuscript = WithRoute()
                    board.Prepare(manuscript)
                    board.Open(manuscript)

                    Dim list As ListBox = Descendants(board.Editor).OfType(Of ListBox)().Single(Function(box) box.AccessibleName = "Journal submissions")
                    Assert.AreEqual(2, list.Items.Count)
                    Assert.AreEqual(1, list.SelectedIndex, "The most recent submission is selected.")

                    Dim detail As SubmissionDetailsForm = Descendants(board.Editor).OfType(Of SubmissionDetailsForm)().Single()
                    CollectionAssert.AreEqual(
                        {"Editorial History", "Reviewer Responses", "Correspondence & Files"},
                        Descendants(detail).OfType(Of TabPage)().Select(Function(page) page.Text.Replace("&&", "&")).ToList())
                    Assert.IsFalse(Descendants(detail).OfType(Of Button)().Any(Function(button) button.Text = "Close" OrElse button.Text = "Reviewer Responses..."),
                        "Inline, there is no Close button and responses are a tab.")
                    Dim matrix As ReviewerResponseMatrixForm = Descendants(detail).OfType(Of ReviewerResponseMatrixForm)().Single()
                    Assert.AreEqual(2, Descendants(matrix).OfType(Of ListBox)().Single().Items.Count, "The matrix lists the submission's comments.")

                    list.SelectedIndex = 0
                    Application.DoEvents()
                    Dim first As SubmissionDetailsForm = Descendants(board.Editor).OfType(Of SubmissionDetailsForm)().Single()
                    Assert.AreNotSame(detail, first, "Selecting another submission shows it.")
                    Assert.IsTrue(Descendants(first).OfType(Of Label)().Any(Function(label) label.Text = "First Journal"))

                    Assert.IsFalse(board.Editor.HasUnsavedChanges(), "Viewing submissions changes nothing.")
                    Assert.AreEqual(2, manuscript.Submissions.Count)

                    board.Close()
                End Using
            End Sub)
    End Sub

    <TestMethod>
    Public Sub InlineReviewerResponsesAreSavedOrDiscardedWithThePage()
        RunOnStaThread(
            Sub()
                Using board As New PageBoard()
                    Dim manuscript As Manuscript = WithRoute()
                    Dim responses As List(Of ReviewerResponseItem) = manuscript.Submissions(1).ReviewerResponses
                    Dim originalOrder As List(Of String) = responses.Select(Function(item) item.ReviewerLabel).ToList()
                    board.Prepare(manuscript)
                    board.Open(manuscript)

                    MoveFirstResponseDown(board)
                    Assert.IsTrue(board.Editor.HasUnsavedChanges(), "Reordering inline is an unsaved change.")
                    CollectionAssert.AreEqual(originalOrder, manuscript.Submissions(1).ReviewerResponses.Select(Function(item) item.ReviewerLabel).ToList(),
                        "The stored manuscript is unchanged until Save.")

                    board.Discard()
                    CollectionAssert.AreEqual(originalOrder, manuscript.Submissions(1).ReviewerResponses.Select(Function(item) item.ReviewerLabel).ToList())
                    Assert.AreEqual(0, board.SaveCount)

                    MoveFirstResponseDown(board)
                    board.PressCommandKey(Keys.Control Or Keys.S)
                    CollectionAssert.AreEqual({"Reviewer 2", "Reviewer 1"}, manuscript.Submissions(1).ReviewerResponses.Select(Function(item) item.ReviewerLabel).ToList(),
                        "Save stores the new order.")
                    Assert.AreEqual(1, board.SaveCount)
                    Assert.IsTrue(manuscript.Submissions(1).ReviewerResponses.All(Function(item) item.DecisionId = manuscript.Submissions(1).Decisions(0).Id AndAlso item.RevisionRoundNumber = 1),
                        "Items keep their decision and explicit round.")

                    board.Close()
                End Using
            End Sub)
    End Sub

    ' #59: an empty library opens on a welcome, which never adds data.
    <TestMethod>
    Public Sub AnEmptyLibraryOpensOnTheWelcomeUntilTheFirstManuscript()
        RunOnStaThread(
            Sub()
                Using board As New PageBoard()
                    board.Prepare()

                    Assert.IsTrue(board.Welcome.Visible, "An empty library shows the welcome.")
                    Assert.AreEqual(0, board.Library.Count, "The welcome adds no sample data.")
                    Assert.AreEqual(0, board.SaveCount)
                    CollectionAssert.AreEqual(
                        {"Paste a title page", "ORCID works", "BibTeX or RIS", "Spreadsheet"},
                        Descendants(board.Welcome).OfType(Of FilterChip)().Select(Function(chip) chip.Text).ToList())

                    board.Library.Add(Sample("The first manuscript"))
                    board.Render()
                    Assert.IsFalse(board.Welcome.Visible, "The shelves replace the welcome.")

                    board.Library.Clear()
                    board.Render()
                    Assert.IsTrue(board.Welcome.Visible)

                    board.Close()
                End Using
            End Sub)
    End Sub

    ' #54 and #59: the Library filters as you type and says when nothing matches.
    <TestMethod>
    Public Sub LibraryFilterNarrowsTheTableAndExplainsNoMatches()
        RunOnStaThread(
            Sub()
                Using board As New PageBoard()
                    Dim revising As Manuscript = Sample("Attention in the wild")
                    revising.TargetJournal = "Journal of Fictional Revisions"
                    board.Prepare(revising, Sample("A second study"), Sample("A third study"))
                    board.PressCommandKey(Keys.Control Or Keys.D2)
                    Assert.AreEqual("Library", board.PageName)

                    Dim grid As DataGridView = Descendants(board).OfType(Of DataGridView)().Single()
                    Dim filter As TextBox = Descendants(board).OfType(Of TextBox)().Single(Function(box) box.AccessibleName = "Filter the Library")
                    Dim hint As Label = grid.Controls.OfType(Of Label)().Single()
                    Assert.AreEqual(3, grid.Rows.Count)
                    Assert.IsFalse(hint.Visible)

                    filter.Text = "revisions"
                    Assert.AreEqual(1, grid.Rows.Count, "The filter matches the target journal.")
                    Assert.AreSame(revising, grid.Rows(0).Tag)

                    filter.Text = "no such paper"
                    Assert.AreEqual(0, grid.Rows.Count)
                    Assert.IsTrue(hint.Visible)
                    StringAssert.Contains(hint.Text, "no such paper")

                    filter.Text = String.Empty
                    Assert.AreEqual(3, grid.Rows.Count)
                    Assert.IsFalse(hint.Visible)

                    board.Close()
                End Using
            End Sub)
    End Sub

    ' #58: ORCID works are listed with the other imports and open the author they belong to.
    <TestMethod>
    Public Sub ImportExistingWorkReachesOrcidWorksThroughTheAuthor()
        RunOnStaThread(
            Sub()
                Using board As New PageBoard()
                    board.Prepare()

                    Descendants(board.Welcome).OfType(Of Button)().Single(Function(button) button.Text = "Import Existing Work").PerformClick()
                    Application.DoEvents()
                    Assert.AreEqual("ImportExport", board.PageName)

                    Descendants(board).OfType(Of Button)().Single(Function(button) button.Text = "ORCID Works...").PerformClick()
                    Application.DoEvents()
                    Assert.AreEqual("Library", board.PageName)
                    Assert.IsTrue(board.AuthorEditorShown, "ORCID works open Authors & Affiliations.")
                    Assert.AreEqual(0, board.Library.Count)

                    board.Close()
                End Using
            End Sub)
    End Sub

    ' The rail collapses to icons for more room and expands again.
    <TestMethod>
    Public Sub TheRailCollapsesToIconsAndExpandsAgain()
        RunOnStaThread(
            Sub()
                Using board As New PageBoard()
                    board.Prepare(Sample("First manuscript"))
                    Dim toggle As Button = Descendants(board).OfType(Of Button)().Single(Function(button) button.AccessibleName = "Collapse navigation")
                    Dim expandedWidth As Integer = board.RailPage("Board").Width
                    Dim logo As RailLogo = Descendants(board).OfType(Of RailLogo)().Single()
                    StringAssert.Contains(logo.AccessibleName, "About PaperRoute")

                    toggle.PerformClick()
                    Application.DoEvents()
                    Assert.AreEqual("Expand navigation", toggle.AccessibleName)
                    Assert.IsTrue(board.RailPage("Board").Width * 2 < expandedWidth, "Collapsed, the rail shows only icons.")
                    Assert.AreEqual("Library", board.RailPage("Library").Text, "Pages keep their names for tooltips and assistive technology.")
                    Assert.IsTrue(logo.Visible, "The logo remains when collapsed.")
                    Assert.IsFalse(Descendants(board).OfType(Of Label)().Single(Function(label) label.Text = ProductInfo.Tagline).Visible,
                        "The tagline folds away with the name.")
                    board.PressCommandKey(Keys.Control Or Keys.D2)
                    Assert.AreEqual("Library", board.PageName, "Navigation works while collapsed.")

                    toggle.PerformClick()
                    Application.DoEvents()
                    Assert.AreEqual("Collapse navigation", toggle.AccessibleName)
                    Assert.AreEqual(expandedWidth, board.RailPage("Board").Width)
                    Dim settings = DirectCast(GetType(Form1).GetField("appSettings", BindingFlags.Instance Or BindingFlags.NonPublic).GetValue(board), AppSettings)
                    Assert.IsFalse(settings.NavigationCollapsed, "Settings that were never loaded are never saved.")

                    board.Close()
                End Using
            End Sub)
    End Sub

    Private Shared Sub MoveFirstResponseDown(board As PageBoard)
        Dim matrix As ReviewerResponseMatrixForm = Descendants(board.Editor).OfType(Of ReviewerResponseMatrixForm)().Single()
        Dim list As ListBox = DirectCast(GetType(ReviewerResponseMatrixForm).GetField("lstResponses", BindingFlags.Instance Or BindingFlags.NonPublic).GetValue(matrix), ListBox)
        list.SelectedIndex = 0
        GetType(ReviewerResponseMatrixForm).GetMethod("MoveItem", BindingFlags.Instance Or BindingFlags.NonPublic).Invoke(matrix, New Object() {1})
        Application.DoEvents()
    End Sub

    ' Two submissions: a desk rejection, then a major revision with two
    ' reviewer comments.
    Private Shared Function WithRoute() As Manuscript
        Dim manuscript As Manuscript = Sample("Routed manuscript")
        manuscript.CurrentStage = PaperStage.Revision
        Dim first As New JournalSubmission With {.JournalName = "First Journal", .SubmittedDate = New DateTime(2026, 1, 5)}
        first.Decisions.Add(New EditorialDecisionEvent With {.Decision = EditorialDecision.DeskRejected, .DecisionDate = New DateTime(2026, 1, 12)})
        Dim second As New JournalSubmission With {.JournalName = "Second Journal", .SubmittedDate = New DateTime(2026, 3, 1)}
        Dim decision As New EditorialDecisionEvent With {.Decision = EditorialDecision.MajorRevision, .DecisionDate = New DateTime(2026, 4, 2)}
        second.Decisions.Add(decision)
        second.ReviewerResponses.Add(New ReviewerResponseItem With {.DecisionId = decision.Id, .RevisionRoundNumber = 1, .ReviewerLabel = "Reviewer 1", .CommentText = "Clarify the sample."})
        second.ReviewerResponses.Add(New ReviewerResponseItem With {.DecisionId = decision.Id, .RevisionRoundNumber = 1, .ReviewerLabel = "Reviewer 2", .CommentText = "Share the data."})
        manuscript.Submissions.Add(first)
        manuscript.Submissions.Add(second)
        Return manuscript
    End Function

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

        Private _authorEditor As Form

        Protected Overrides Function CreateAuthorLibraryEditor() As Form
            _authorEditor = New Form()
            Return _authorEditor
        End Function

        Public ReadOnly Property AuthorEditorShown As Boolean
            Get
                Return _authorEditor IsNot Nothing AndAlso Not _authorEditor.IsDisposed AndAlso _authorEditor.Visible
            End Get
        End Property

        Public ReadOnly Property Welcome As Control
            Get
                Return DirectCast(GetType(Form1).GetField("boardWelcome", BindingFlags.Instance Or BindingFlags.NonPublic).GetValue(Me), Control)
            End Get
        End Property

        Public Sub Render()
            CallPrivate("RenderManuscripts")
            Application.DoEvents()
        End Sub

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
        Dim nativeThreadId As Integer = 0
        Dim thread As New Thread(
            Sub()
                nativeThreadId = GetCurrentThreadId()
                Try
                    action()
                Catch ex As Exception
                    failure = ex
                End Try
            End Sub) With {.IsBackground = True}
        thread.SetApartmentState(ApartmentState.STA)
        thread.Start()
        If Not thread.Join(TimeSpan.FromSeconds(90)) Then
            ' A modal message box would block the test silently; name what is open.
            Assert.Fail("Manuscript page test timed out. Windows open on its thread: " & DescribeThreadWindows(nativeThreadId))
        End If
        If failure IsNot Nothing Then ExceptionDispatchInfo.Capture(failure).Throw()
    End Sub

    Private Shared Function DescribeThreadWindows(threadId As Integer) As String
        Dim windows As New List(Of String)()
        EnumThreadWindows(threadId,
            Function(handle, parameter)
                Dim title As New System.Text.StringBuilder(256)
                Dim className As New System.Text.StringBuilder(256)
                GetWindowText(handle, title, title.Capacity)
                GetClassName(handle, className, className.Capacity)
                windows.Add($"'{title}' ({className}{If(IsWindowVisible(handle), ", visible", "")})")
                Return True
            End Function, IntPtr.Zero)
        Return If(windows.Count = 0, "none", String.Join("; ", windows))
    End Function

    Private Delegate Function EnumWindowsCallback(handle As IntPtr, parameter As IntPtr) As Boolean

    <System.Runtime.InteropServices.DllImport("kernel32.dll")>
    Private Shared Function GetCurrentThreadId() As Integer
    End Function

    <System.Runtime.InteropServices.DllImport("user32.dll")>
    Private Shared Function EnumThreadWindows(threadId As Integer, callback As EnumWindowsCallback, parameter As IntPtr) As Boolean
    End Function

    <System.Runtime.InteropServices.DllImport("user32.dll", CharSet:=System.Runtime.InteropServices.CharSet.Unicode)>
    Private Shared Function GetWindowText(handle As IntPtr, text As System.Text.StringBuilder, capacity As Integer) As Integer
    End Function

    <System.Runtime.InteropServices.DllImport("user32.dll", CharSet:=System.Runtime.InteropServices.CharSet.Unicode)>
    Private Shared Function GetClassName(handle As IntPtr, text As System.Text.StringBuilder, capacity As Integer) As Integer
    End Function

    <System.Runtime.InteropServices.DllImport("user32.dll")>
    Private Shared Function IsWindowVisible(handle As IntPtr) As Boolean
    End Function

End Class
