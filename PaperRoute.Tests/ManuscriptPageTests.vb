Imports System.Drawing
Imports System.IO
Imports System.Linq
Imports System.Reflection
Imports System.Runtime.ExceptionServices
Imports System.Threading
Imports System.Threading.Tasks
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
                    board.PressCommandKey(Keys.Control Or Keys.D6)
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

                    ' #83 and #96: the example library opens from the welcome,
                    ' Import & Export, and Help, in its own window; nothing
                    ' here changes.
                    Dim launches As Integer = 0
                    board.exampleLauncher = Sub() launches += 1
                    Dim example As LinkLabel = Descendants(board.Welcome).OfType(Of LinkLabel)().Single(Function(link) link.Text.EndsWith("Explore an example library", StringComparison.Ordinal))
                    GetType(LinkLabel).GetMethod("OnLinkClicked", BindingFlags.Instance Or BindingFlags.NonPublic).
                        Invoke(example, New Object() {New LinkLabelLinkClickedEventArgs(example.Links(0))})
                    board.PressCommandKey(Keys.Control Or Keys.D6)
                    Descendants(board).OfType(Of ActionButton)().Single(Function(button) button.Text = "Explore an Example Library...").PerformClick()
                    Using help As HelpForm = board.CreateUserGuide()
                        ShowOffscreen(help)
                        Descendants(help).OfType(Of Button)().Single(Function(button) button.Text = "Explore an Example Library...").PerformClick()
                        Assert.IsTrue(help.Visible, "Help stays open beside the example.")
                        help.Close()
                    End Using
                    Using plain As New HelpForm()
                        Assert.IsFalse(Descendants(plain).OfType(Of Button)().Any(Function(button) button.Text = "Explore an Example Library..."),
                                       "Help opened elsewhere, or inside the example, doesn't offer it.")
                    End Using
                    Assert.AreEqual(3, launches)
                    Assert.AreEqual(0, board.Library.Count, "Opening the example adds nothing to this library.")
                    Assert.AreEqual(0, board.SaveCount)
                    board.PressCommandKey(Keys.Control Or Keys.D1)

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

    ' Deadlines (#28): grouped by when, filtered by kind or text, and every
    ' action changes the record that owns the date.
    <TestMethod>
    Public Sub DeadlinesGroupFilterAndChangeTheOwningRecord()
        RunOnStaThread(
            Sub()
                Using board As New PageBoard()
                    Dim today As DateTime = DateTime.Today
                    Dim revision As Manuscript = WithRoute()
                    Dim decision As EditorialDecisionEvent = revision.Submissions(1).Decisions.Single()
                    decision.RevisionDeadline = today.AddDays(-2)
                    Dim waiting As Manuscript = Sample("Grit scale")
                    waiting.CurrentStage = PaperStage.Submitted
                    Dim submission As New JournalSubmission With {.JournalName = "Assessment", .SubmittedDate = today.AddDays(-30), .FollowUpDate = today.AddDays(3)}
                    waiting.Submissions.Add(submission)
                    Dim reminder As New ManuscriptReminder With {.Title = "Send draft to coauthors", .DueDate = today}
                    waiting.Reminders.Add(reminder)
                    waiting.Reminders.Add(New ManuscriptReminder With {.Title = "Upload preregistration", .DueDate = today.AddDays(-4), .IsCompleted = True, .CompletedDate = today.AddDays(-1)})
                    board.Prepare(revision, waiting)

                    Dim rail As RailButton = DirectCast(board.RailPage("Deadlines"), RailButton)
                    Assert.AreEqual(2, rail.Badge, "The rail counts what is overdue or due today.")

                    board.PressCommandKey(Keys.Control Or Keys.D4)
                    Assert.AreEqual("Deadlines", board.PageName)
                    Assert.IsTrue(rail.Checked)
                    CollectionAssert.AreEqual({"OVERDUE  1", "TODAY  1", "NEXT 7 DAYS  1"},
                        Descendants(board).OfType(Of Label)().Where(Function(label) label.Text.StartsWith("OVERDUE") OrElse label.Text.StartsWith("TODAY") OrElse
                                                                              label.Text.StartsWith("NEXT") OrElse label.Text.StartsWith("LATER") OrElse
                                                                              label.Text.StartsWith("NO DATE")).Select(Function(label) label.Text).ToList())
                    CollectionAssert.AreEqual({"Revision due", "Send draft to coauthors", "Follow up with Assessment"},
                        DeadlineRows(board).Select(Function(row) row.Item.Title).ToList(), "Done stays folded away.")
                    Dim revisionRow As DeadlineRow = DeadlineRows(board).First()
                    StringAssert.Contains(revisionRow.AccessibleName, "Overdue")
                    StringAssert.Contains(revisionRow.AccessibleName, "0 of 2 comments", "A revision shows its reviewer-comment progress.")
                    CollectionAssert.AreEqual({"Open", "Postpone..."}, revisionRow.ActionTexts.ToList())

                    Descendants(board).OfType(Of LinkLabel)().Single(Function(link) link.Text.StartsWith("Done in the last")).Links(0).Enabled = True
                    ClickDoneLink(board)
                    Assert.AreEqual(4, DeadlineRows(board).Count, "Show lists recently completed reminders.")
                    ClickDoneLink(board)

                    ClickControl(Descendants(board).OfType(Of FilterChip)().Single(Function(chip) chip.Text.StartsWith("Follow-ups")))
                    CollectionAssert.AreEqual({"Follow up with Assessment"}, DeadlineRows(board).Select(Function(row) row.Item.Title).ToList())
                    ClickControl(Descendants(board).OfType(Of FilterChip)().Single(Function(chip) chip.Text.StartsWith("Follow-ups")))
                    Assert.AreEqual(3, DeadlineRows(board).Count, "Clicking the active chip shows everything again.")

                    Dim filter As TextBox = Descendants(board).OfType(Of TextBox)().Single(Function(box) box.AccessibleName = "Filter deadlines")
                    filter.Text = "grit"
                    Application.DoEvents()
                    Assert.IsTrue(DeadlineRows(board).All(Function(row) row.Item.ManuscriptTitle = "Grit scale"))
                    filter.Text = String.Empty
                    Application.DoEvents()

                    ' Postpone asks for a date and writes the submission's follow-up.
                    board.SetDeadlinePrompts(Function(item) today.AddDays(10), Function(question) True)
                    DeadlineRows(board).Single(Function(row) row.Item.Kind = DeadlineKind.FollowUp).RunAction("Postpone...")
                    Application.DoEvents()
                    Assert.AreEqual(today.AddDays(10), submission.FollowUpDate.Value)
                    Assert.AreEqual(1, board.SaveCount)
                    Assert.AreEqual(DeadlineGroup.Later, DeadlineRows(board).Single(Function(row) row.Item.Kind = DeadlineKind.FollowUp).Item.Group)

                    DeadlineRows(board).Single(Function(row) row.Item.Kind = DeadlineKind.Reminder).RunAction("Done")
                    Application.DoEvents()
                    Assert.IsTrue(reminder.IsCompleted)
                    Assert.AreEqual(2, board.SaveCount)
                    Assert.AreEqual(1, rail.Badge, "The badge follows the change.")

                    DeadlineRows(board).Single(Function(row) row.Item.Kind = DeadlineKind.FollowUp).RunAction("Clear Follow-up...")
                    Application.DoEvents()
                    Assert.IsFalse(submission.FollowUpDate.HasValue)
                    Assert.AreEqual(1, waiting.Submissions.Count, "Clearing leaves the submission as it was.")
                    Assert.AreEqual(3, board.SaveCount)

                    ' Open lands on the revision's reviewer responses.
                    DeadlineRows(board).Single().RunAction("Open")
                    Application.DoEvents()
                    Assert.AreEqual("Manuscript", board.PageName)
                    Assert.IsTrue(Descendants(board.Editor).OfType(Of RadioButton)().Single(Function(tab) tab.Text = "Submissions").Checked)
                    Dim detail As SubmissionDetailsForm = Descendants(board.Editor).OfType(Of SubmissionDetailsForm)().Single()
                    Assert.AreEqual("Second Journal", DirectCast(GetType(SubmissionDetailsForm).GetField("_submission", BindingFlags.Instance Or BindingFlags.NonPublic).GetValue(detail), JournalSubmission).JournalName)
                    Assert.AreEqual("Reviewer Responses", Descendants(detail).OfType(Of TabControl)().First().SelectedTab.Text)

                    board.PressCommandKey(Keys.Alt Or Keys.Left)
                    Assert.AreEqual("Deadlines", board.PageName, "Back returns to Deadlines.")
                    Assert.AreEqual(3, board.SaveCount, "Opening and returning save nothing.")

                    board.Close()
                End Using
            End Sub)
    End Sub

    ' Nothing to act on: a sentence, not an empty list.
    <TestMethod>
    Public Sub DeadlinesExplainAnEmptyList()
        RunOnStaThread(
            Sub()
                Using board As New PageBoard()
                    board.Prepare(Sample("Draft only"))
                    Descendants(board).OfType(Of LinkLabel)().Single(Function(link) link.Text = "View in Deadlines →").Links(0).Enabled = True
                    board.PressCommandKey(Keys.Control Or Keys.D4)
                    Assert.AreEqual(0, DeadlineRows(board).Count)
                    Assert.IsTrue(Descendants(board).OfType(Of Label)().Any(Function(label) label.Text.StartsWith("Nothing needs action right now.")))
                    Assert.AreEqual(0, DirectCast(board.RailPage("Deadlines"), RailButton).Badge)
                    board.Close()
                End Using
            End Sub)
    End Sub

    ' Possible publications (#61) wait on Deadlines until Mark Published or
    ' Ignore; each choice is saved.
    <TestMethod>
    Public Sub PossiblePublicationsAreReviewedOnDeadlines()
        RunOnStaThread(
            Sub()
                Using board As New PageBoard()
                    Dim first As Manuscript = Sample("Anchoring effects in clinical risk estimates")
                    first.CurrentStage = PaperStage.UnderReview
                    first.PublicationMatches.Add(New PublicationMatch With {.Doi = "10.5555/anchoring", .Journal = "Collabra: Psychology", .PublishedDate = DateTime.Today.AddDays(-3)})
                    Dim second As Manuscript = Sample("Grit across four countries")
                    second.CurrentStage = PaperStage.Submitted
                    second.PublicationMatches.Add(New PublicationMatch With {.Doi = "10.5555/grit", .Journal = "Assessment"})
                    board.Prepare(first, second)
                    board.markPublishedPrompt = Function(manuscript, match) True

                    board.PressCommandKey(Keys.Control Or Keys.D4)
                    Dim chip As FilterChip = Descendants(board).OfType(Of FilterChip)().Single(Function(candidate) candidate.Text.StartsWith("Publications"))
                    Assert.AreEqual("Publications  2", chip.Text)
                    Assert.IsTrue(chip.Visible)
                    Dim row As DeadlineRow = DeadlineRows(board).First(Function(candidate) candidate.Item.ManuscriptId = first.Id)
                    Assert.AreEqual("May have been published in Collabra: Psychology", row.Item.Title)
                    CollectionAssert.AreEqual({"Review Match", "Mark Published...", "Ignore Match", "Open Manuscript"}, row.ActionTexts.ToList())

                    row.RunAction("Mark Published...")
                    Application.DoEvents()
                    Assert.AreEqual(PaperStage.Published, board.Library(0).CurrentStage)
                    Assert.AreEqual(ManuscriptLocation.Published, board.Library(0).Location)
                    Assert.AreEqual("10.5555/anchoring", board.Library(0).Metadata.Doi)
                    Assert.AreEqual(1, board.SaveCount)

                    DeadlineRows(board).Single(Function(candidate) candidate.Item.Kind = DeadlineKind.Publication).RunAction("Ignore Match")
                    Application.DoEvents()
                    Assert.AreEqual(PublicationMatchStatus.Ignored, second.PublicationMatches.Single().Status)
                    Assert.AreEqual(PaperStage.Submitted, second.CurrentStage, "Ignore changes nothing else.")
                    Assert.AreEqual(2, board.SaveCount)
                    Assert.IsFalse(Descendants(board).OfType(Of FilterChip)().Single(Function(candidate) candidate.Text.StartsWith("Publications")).Visible,
                        "The Publications chip appears only while there is something to review.")

                    board.Close()
                End Using
            End Sub)
    End Sub

    <TestMethod>
    Public Sub PublicationCheckFindsKeepsAndMarksPublished()
        RunOnStaThread(
            Sub()
                Dim tracked As Manuscript = Sample("Measurement invariance of a short grit scale across four countries")
                tracked.CurrentStage = PaperStage.UnderReview
                tracked.Submissions.Add(New JournalSubmission With {.JournalName = "Assessment", .SubmittedDate = DateTime.Today.AddDays(-120)})
                Dim idea As Manuscript = Sample("An idea that has not been submitted anywhere yet")
                Dim library As New List(Of Manuscript) From {tracked, idea}
                Dim saves As Integer = 0
                Dim source As New OneTitleSource(tracked.Title, New CrossrefMetadataSuggestion With {
                    .Doi = "10.5555/grit", .Title = tracked.Title, .Journal = "Assessment", .WorkType = "journal-article", .PublishedDate = DateTime.Today.AddDays(-2)})

                Using dialog As New PublicationCheckForm(library, Nothing, String.Empty, source, Function()
                                                                                                   saves += 1
                                                                                                   Return True
                                                                                               End Function, DateTime.Today)
                    dialog.Pause = TimeSpan.Zero
                    dialog.ConfirmMarkPublished = Function(manuscript, match) True
                    ShowOffscreen(dialog)
                    CollectionAssert.AreEqual({tracked}, dialog.SelectedManuscripts, "Work that has gone to a journal is checked by default.")

                    Dim check As Task = dialog.CheckAsync()
                    While Not check.IsCompleted
                        Application.DoEvents()
                    End While
                    check.GetAwaiter().GetResult()

                    Assert.AreEqual(PublicationMatchStatus.Pending, tracked.PublicationMatches.Single().Status, "A found match is kept for later review.")
                    Assert.AreEqual(1, saves)
                    Assert.AreEqual(PaperStage.UnderReview, tracked.CurrentStage, "Finding a match changes nothing else.")
                    Assert.IsTrue(Descendants(dialog).OfType(Of Label)().Any(Function(label) label.Text = "A publication matching this manuscript may have appeared."))

                    Descendants(dialog).OfType(Of Button)().Single(Function(button) button.Text = "Mark Published...").PerformClick()
                    Application.DoEvents()
                    Assert.AreEqual(PaperStage.Published, tracked.CurrentStage)
                    Assert.AreEqual(2, saves)
                    Assert.IsTrue(dialog.Changed)
                    Assert.IsTrue(Descendants(dialog).OfType(Of Label)().Any(Function(label) label.Text.StartsWith("Marked published.") AndAlso label.Visible))
                    dialog.Close()
                End Using
            End Sub)
    End Sub

    Private NotInheritable Class OneTitleSource
        Implements IPublicationSource

        Private ReadOnly _title As String
        Private ReadOnly _work As CrossrefMetadataSuggestion

        Public Sub New(title As String, work As CrossrefMetadataSuggestion)
            _title = title
            _work = work
        End Sub

        Public Function LookupDoiAsync(doi As String, cancellationToken As Threading.CancellationToken) As Task(Of CrossrefMetadataSuggestion) Implements IPublicationSource.LookupDoiAsync
            Return Task.FromResult(Of CrossrefMetadataSuggestion)(Nothing)
        End Function

        Public Function SearchTitleAsync(title As String, cancellationToken As Threading.CancellationToken) As Task(Of List(Of CrossrefMetadataSuggestion)) Implements IPublicationSource.SearchTitleAsync
            Return Task.FromResult(If(title = _title, New List(Of CrossrefMetadataSuggestion) From {_work}, New List(Of CrossrefMetadataSuggestion)()))
        End Function

        Public Function OrcidWorksAsync(orcid As String, cancellationToken As Threading.CancellationToken) As Task(Of List(Of OrcidWorkSuggestion)) Implements IPublicationSource.OrcidWorksAsync
            Return Task.FromResult(New List(Of OrcidWorkSuggestion)())
        End Function
    End Class

    ' Insights (#30, #62): your journals and routes, from recorded dates.
    <TestMethod>
    Public Sub InsightsSummarizeJournalsAndRoutesFromTheLibrary()
        RunOnStaThread(
            Sub()
                Using board As New PageBoard()
                    Dim routed As Manuscript = WithRoute()
                    board.Prepare(routed, Sample("Unsubmitted idea"))

                    board.PressCommandKey(Keys.Control Or Keys.D5)
                    Assert.AreEqual("Insights", board.PageName)
                    Dim tiles As List(Of String) = Descendants(board).OfType(Of RoundedPanel)().Select(Function(tile) tile.AccessibleName).Where(Function(name) name IsNot Nothing).ToList()
                    Assert.IsTrue(tiles.Contains("2 submissions  ·  0 accepted"), String.Join(" | ", tiles))
                    Assert.IsTrue(tiles.Contains("19.5 days median to a first decision (of 2)"), "The median of 7 and 32 days.")
                    Assert.IsTrue(tiles.Contains("— median from first submission to acceptance (not recorded yet)"), "Nothing is estimated.")

                    Dim journals As DataGridView = Descendants(board).OfType(Of DataGridView)().Single()
                    CollectionAssert.AreEquivalent({"First Journal", "Second Journal"}, journals.Rows.Cast(Of DataGridViewRow)().Select(Function(row) CStr(row.Cells("Journal").Value)).ToList())
                    Dim second As DataGridViewRow = journals.Rows.Cast(Of DataGridViewRow)().Single(Function(row) CStr(row.Cells("Journal").Value) = "Second Journal")
                    Assert.AreEqual(1.0, CDbl(second.Cells("Revisions").Value))
                    Assert.AreEqual(32.0, CDbl(second.Cells("FirstDecision").Value))

                    journals.CurrentCell = second.Cells(0)
                    GetType(Control).GetMethod("OnKeyDown", BindingFlags.Instance Or BindingFlags.NonPublic).Invoke(journals, New Object() {New KeyEventArgs(Keys.Enter)})
                    Application.DoEvents()
                    Dim routes As DataGridView = Descendants(board).OfType(Of DataGridView)().Single()
                    Assert.AreEqual(1, routes.Rows.Count, "Enter on a journal shows the routes that went through it.")
                    Assert.AreEqual("Routed manuscript", CStr(routes.Rows(0).Cells("Title").Value))
                    Assert.IsTrue(Descendants(board).OfType(Of LinkLabel)().Any(Function(link) link.Visible AndAlso link.Text.StartsWith("Showing manuscripts sent to Second Journal")))

                    routes.CurrentCell = routes.Rows(0).Cells(0)
                    GetType(Control).GetMethod("OnKeyDown", BindingFlags.Instance Or BindingFlags.NonPublic).Invoke(routes, New Object() {New KeyEventArgs(Keys.Enter)})
                    Application.DoEvents()
                    Assert.AreEqual("Manuscript", board.PageName, "Enter on a route opens the manuscript.")
                    Assert.AreEqual(0, board.SaveCount, "Insights never save.")

                    board.Close()
                End Using
            End Sub)
    End Sub

    ' The route map (#82): every route side by side on Insights, each one
    ' opening its own route view.
    <TestMethod>
    Public Sub TheRouteMapLinesUpRoutesAndOpensTheChosenOne()
        RunOnStaThread(
            Sub()
                Using board As New PageBoard()
                    Dim published As Manuscript = RouteMapServiceTests.Anchoring()
                    Dim routed As Manuscript = WithRoute()
                    board.Prepare(published, routed, Sample("Unsubmitted idea"))
                    Dim opened As New List(Of Manuscript)()
                    board.routeViewOpener = Sub(item) opened.Add(item)

                    board.PressCommandKey(Keys.Control Or Keys.D5)
                    Descendants(board).OfType(Of ShelfTabButton)().Single(Function(item) item.Text = "Route Map").Checked = True
                    Application.DoEvents()
                    Dim chart As RouteMapChart = Descendants(board).OfType(Of RouteMapChart)().Single()
                    CollectionAssert.AreEqual({published}, chart.Routes.Select(Function(item) item.Manuscript).ToList(), "Published routes by default.")
                    Assert.IsTrue(Descendants(board).OfType(Of Label)().Any(Function(label) label.Text = "1 published route lined up at day 0. Median 264 days from first submission to publication."))
                    Assert.IsTrue(Descendants(board).OfType(Of RouteMapLegend)().Single().AccessibleName.StartsWith("With a journal  ·  40%"), "105 of 264 days.")

                    Dim toggle As LinkLabel = Descendants(board).OfType(Of LinkLabel)().Single(Function(link) link.Text = "Include work not yet published")
                    GetType(LinkLabel).GetMethod("OnLinkClicked", BindingFlags.Instance Or BindingFlags.NonPublic).
                        Invoke(toggle, New Object() {New LinkLabelLinkClickedEventArgs(toggle.Links(0))})
                    Application.DoEvents()
                    chart = Descendants(board).OfType(Of RouteMapChart)().Single()
                    CollectionAssert.AreEquivalent({published, routed}, chart.Routes.Select(Function(item) item.Manuscript).ToList(), "Work in progress runs to today; an idea has no route.")
                    Assert.IsTrue(chart.Routes.Single(Function(item) item.Manuscript Is routed).Map.Ongoing)

                    chart.SelectedIndex = chart.Routes.FindIndex(Function(item) item.Manuscript Is routed)
                    GetType(Control).GetMethod("OnKeyDown", BindingFlags.Instance Or BindingFlags.NonPublic).Invoke(chart, New Object() {New KeyEventArgs(Keys.Enter)})
                    CollectionAssert.AreEqual({routed}, opened, "Enter opens the selected route.")
                    Assert.AreEqual(0, board.SaveCount, "The route map never saves.")

                    board.Close()
                End Using
            End Sub)
    End Sub

    ' Your Citations (#91): saved figures on Insights, computed here; only
    ' Update from OpenAlex... asks anything, and the tab never saves the library.
    <TestMethod>
    Public Sub YourCitationsShowSavedFiguresAndUpdateOnlyWhenAsked()
        RunOnStaThread(
            Sub()
                Dim folder As String = Path.Combine(Path.GetTempPath(), "PaperRoute-Citations-" & Guid.NewGuid().ToString("N"))
                Dim network As New NoNetwork()
                OnlineAccess.ResetForTests()
                OnlineAccess.InnerHandlerFactory = Function() network
                Try
                    Using board As New PageBoard()
                        Dim published As Manuscript = RouteMapServiceTests.Anchoring()
                        published.Metadata.Doi = "https://doi.org/10.5555/Example.Anchoring"
                        Dim second As Manuscript = RouteMapServiceTests.Anchoring()
                        second.Title = "A second published manuscript"
                        second.Metadata.Doi = "10.5555/example.second"
                        board.Prepare(published, second, Sample("Unsubmitted idea"))
                        Dim store As New CitationStore(folder)
                        board.citationStoreFactory = Function() store
                        Dim prompted As New List(Of CitationSnapshot)()
                        board.citationsUpdatePrompt =
                            Function(previous)
                                prompted.Add(previous)
                                Dim snapshot As New CitationSnapshot With {.Orcid = "0000-0002-1825-0097", .Source = JournalFactCatalog.OpenAlexSource, .RetrievedUtc = New DateTime(2026, 9, 30, 12, 0, 0, DateTimeKind.Utc)}
                                snapshot.Works.Add(New CitedWork With {.OpenAlexId = "W1", .Doi = "10.5555/example.anchoring", .Year = 2026, .CitedByCount = 12, .Fwci = 1.2, .Percentile = 0.8,
                                                                       .CountsByYear = New List(Of YearCount) From {New YearCount With {.Year = 2026, .Count = 12}}})
                                snapshot.Works.Add(New CitedWork With {.OpenAlexId = "W2", .Doi = "10.5555/example.other", .Year = 2020, .CitedByCount = 3})
                                snapshot.Works.Add(New CitedWork With {.OpenAlexId = "W3", .Doi = "10.5555/example.second", .Year = 2018, .CitedByCount = 40, .Fwci = 12.3, .Percentile = 0.07})
                                Return snapshot
                            End Function

                        ' Without an ORCID iD, the tab says where to add one.
                        board.PressCommandKey(Keys.Control Or Keys.D5)
                        Descendants(board).OfType(Of ShelfTabButton)().Single(Function(item) item.Text = "Your Citations").Checked = True
                        Application.DoEvents()
                        Assert.IsTrue(Descendants(board).OfType(Of Button)().Any(Function(button) button.Text = "Open Authors && Affiliations"))
                        Assert.IsFalse(Descendants(board).OfType(Of Label)().Single(Function(label) label.Text.StartsWith("Days are calendar days")).Visible, "The route footnote belongs to the other views.")

                        board.SetAuthors(New AuthorRecord With {.GivenName = "Josiah", .FamilyName = "Carberry", .IsMe = True, .Orcid = "https://orcid.org/0000-0002-1825-0097"})
                        Descendants(board).OfType(Of ShelfTabButton)().Single(Function(item) item.Text.StartsWith("Your Journals")).Checked = True
                        Descendants(board).OfType(Of ShelfTabButton)().Single(Function(item) item.Text = "Your Citations").Checked = True
                        Application.DoEvents()
                        Assert.IsTrue(Descendants(board).OfType(Of Label)().Any(Function(label) label.Text.StartsWith("See how your published work has been cited, from OpenAlex") AndAlso label.Text.Contains("(0000-0002-1825-0097)")))
                        Dim update As Button = Descendants(board).OfType(Of Button)().Single(Function(button) button.Text = "Update from OpenAlex...")
                        Assert.IsTrue(update.Enabled)

                        ClickControl(update)
                        Assert.AreEqual(1, prompted.Count)
                        Assert.IsNull(prompted(0), "Nothing saved before.")
                        Assert.AreEqual(3, store.Load().Works.Count, "The confirmed works are saved.")
                        Dim labels As List(Of String) = Descendants(board).OfType(Of Label)().Select(Function(label) label.Text).ToList()
                        CollectionAssert.IsSubsetOf({"Citations", "h-index", "i10-index", "g-index", "m-quotient", "55"}, labels)
                        Assert.IsTrue(labels.Any(Function(text) text.StartsWith("From OpenAlex on Sep 30, 2026, for the 3 works you confirmed (ORCID iD 0000-0002-1825-0097).")))
                        Assert.IsTrue(labels.Any(Function(text) text.Contains("2026 12 so far")))
                        Assert.IsTrue(labels.Contains("Other works on your record: 1 (not tracked in PaperRoute)."))
                        Dim grid As DataGridView = Descendants(board).OfType(Of DataGridView)().Single()
                        Assert.AreEqual(2, grid.Rows.Count, "Your manuscripts, joined by DOI.")
                        Dim row As DataGridViewRow = grid.Rows.Cast(Of DataGridViewRow)().Single(Function(item) CStr(item.Cells("Title").Value) = published.Title)
                        Assert.AreEqual(12.0, CDbl(row.Cells("Citations").Value))
                        Assert.AreEqual("80th", CStr(row.Cells("Percentile").Value))
                        StringAssert.EndsWith(CStr(row.Cells("Fwci").Value), "(provisional)")
                        ' Numbers sort as numbers: 12.30 above 1.20, the 80th above the 7th.
                        grid.Sort(grid.Columns("Fwci"), System.ComponentModel.ListSortDirection.Descending)
                        Assert.AreEqual("A second published manuscript", CStr(grid.Rows(0).Cells("Title").Value))
                        grid.Sort(grid.Columns("Percentile"), System.ComponentModel.ListSortDirection.Descending)
                        Assert.AreEqual(published.Title, CStr(grid.Rows(0).Cells("Title").Value))

                        ' Working offline leaves the saved figures and turns Update off.
                        OnlineAccess.Configure(New OnlineServicesSettings With {.WorkOffline = True})
                        Descendants(board).OfType(Of ShelfTabButton)().Single(Function(item) item.Text.StartsWith("Your Routes")).Checked = True
                        Descendants(board).OfType(Of ShelfTabButton)().Single(Function(item) item.Text = "Your Citations").Checked = True
                        Application.DoEvents()
                        Assert.IsFalse(Descendants(board).OfType(Of Button)().Single(Function(button) button.Text = "Update from OpenAlex...").Enabled)
                        Assert.AreEqual(2, Descendants(board).OfType(Of DataGridView)().Single().Rows.Count)
                        Assert.IsTrue(Descendants(board).OfType(Of Label)().Any(Function(label) label.Text.StartsWith("You're working offline, so updating is off.")))

                        Assert.AreEqual(0, network.Requests, "Showing citations never sends anything.")
                        Assert.AreEqual(0, board.SaveCount, "Citations never save the library.")
                        board.Close()
                    End Using
                Finally
                    OnlineAccess.ResetForTests()
                    If Directory.Exists(folder) Then Directory.Delete(folder, True)
                End Try
            End Sub)
    End Sub

    <TestMethod>
    Public Sub TheRouteViewOpensOnItsRouteDrawnToScale()
        RunOnStaThread(
            Sub()
                Using view As New ManuscriptRouteViewForm(RouteMapServiceTests.Anchoring())
                    ShowOffscreen(view)
                    Dim bar As RouteMapBar = Descendants(view).OfType(Of RouteMapBar)().Single()
                    Assert.AreEqual("Route drawn to scale. 264 days from first submission to publication: 105 days with the journals, 131 days with you, 28 days in production.", bar.AccessibleName)
                    Assert.AreEqual(8, bar.Steps.Count)
                    StringAssert.StartsWith(bar.AccessibleDescription, "1: Desk rejected, Jan 12, 7 days after submission")
                    Assert.IsTrue(bar.Height > 0 AndAlso bar.Width > 0)
                    Dim onScreen As Rectangle = view.RectangleToClient(bar.RectangleToScreen(bar.ClientRectangle))
                    Assert.IsTrue(onScreen.Right <= view.ClientSize.Width, "The map fits the window's width.")
                    view.Close()
                End Using

                Using view As New ManuscriptRouteViewForm(Sample("Unsubmitted idea"))
                    ShowOffscreen(view)
                    Assert.IsFalse(Descendants(view).OfType(Of RouteMapBar)().Any(), "No route map before a first submission.")
                    view.Close()
                End Using
            End Sub)
    End Sub

    ' The journal shortlist (#65) on the Overview: added with its reasons,
    ' offered after a rejection, and saved with the page.
    <TestMethod>
    Public Sub TheShortlistOffersTheNextJournalAfterARejectionAndSavesWithThePage()
        RunOnStaThread(
            Sub()
                Using board As New PageBoard()
                    Dim manuscript As Manuscript = Sample("Rerouted manuscript")
                    manuscript.TargetJournal = "First Journal"
                    Dim first As New JournalSubmission With {.JournalName = "First Journal", .SubmittedDate = New DateTime(2026, 1, 5)}
                    first.Decisions.Add(New EditorialDecisionEvent With {.Decision = EditorialDecision.DeskRejected, .DecisionDate = New DateTime(2026, 1, 12)})
                    manuscript.Submissions.Add(first)
                    board.Prepare(manuscript)
                    board.Open(manuscript)

                    Assert.AreEqual(String.Empty, board.Editor.ShortlistOfferText, "Nothing to offer before a journal is shortlisted.")
                    board.Editor.candidatePrompt =
                        Function(existing) New JournalCandidate With {.JournalName = "Open Psychology", .Status = CandidateStatus.Preferred,
                                                                     .Notes = "Publishes replications", .Checks = New List(Of String) From {"trust.known", "fit.scope"}}
                    board.Editor.AddShortlistCandidateForTest()

                    Assert.AreEqual("Desk rejected by First Journal on Jan 12, 2026. Next on your shortlist: Open Psychology (Preferred).", board.Editor.ShortlistOfferText)
                    Assert.IsTrue(board.Editor.HasUnsavedChanges(), "The shortlist waits for Save like any change.")
                    Assert.AreEqual(0, manuscript.JournalShortlist.Count, "The saved record is untouched until Save.")

                    board.Editor.ShortlistOfferButton.PerformClick()
                    Assert.AreEqual(String.Empty, board.Editor.ShortlistOfferText, "Once it is the target journal, there is nothing more to offer.")

                    board.PressCommandKey(Keys.Control Or Keys.S)
                    Assert.AreEqual("Open Psychology", manuscript.TargetJournal)
                    Dim saved As JournalCandidate = manuscript.JournalShortlist.Single()
                    Assert.AreEqual("Publishes replications", saved.Notes)
                    CollectionAssert.AreEquivalent({"trust.known", "fit.scope"}, saved.Checks)
                    Assert.AreEqual(1, board.SaveCount)

                    board.Close()
                End Using
            End Sub)
    End Sub

    <TestMethod>
    Public Sub FoundJournalsJoinTheShortlistWithTheirEvidenceAndWaitForSave()
        RunOnStaThread(
            Sub()
                Using board As New PageBoard()
                    Dim manuscript As Manuscript = Sample("Anchoring in clinical risk estimates")
                    board.Prepare(manuscript)
                    board.Open(manuscript)

                    Dim messages As New List(Of String)()
                    AddHandler board.Editor.StatusMessage, Sub(sender, message) messages.Add(message)
                    Dim request As New JournalSuggestionRequest With {.Keywords = New List(Of String) From {"anchoring effects"}, .MatchAll = True, .SinceDate = New DateTime(2021, 9, 30)}
                    Dim found As New List(Of JournalSuggestion) From {
                        New JournalSuggestion With {.OpenAlexId = "S196734849", .Name = "Scientific Reports", .Issns = New List(Of String) From {"2045-2322"}, .MatchingArticles = 3, .AllArticles = 163365,
                                                    .Publisher = "Nature Portfolio", .Topics = New List(Of String) From {"Cancer-related molecular mechanisms research", "MicroRNA in disease regulation"},
                                                    .HomepageUrl = "http://www.nature.com/srep/index.html",
                                                    .Examples = New List(Of EvidenceExample) From {New EvidenceExample With {.Title = "Anchoring in triage", .Year = 2026, .Doi = "10.1038/s41598-026-66155-3"}}},
                        New JournalSuggestion With {.OpenAlexId = "S9692511", .Name = "Frontiers in Psychology", .Issns = New List(Of String) From {"1664-1078"}, .MatchingArticles = 2}
                    }
                    Dim result As New JournalSuggestionsResult With {.Request = request, .RetrievedUtc = New DateTime(2026, 9, 30, 12, 0, 0, DateTimeKind.Utc), .Journals = found}
                    board.Editor.journalSuggestionPrompt = Function() (found, result)
                    board.Editor.FindJournalsForTest()

                    Assert.IsTrue(board.Editor.HasUnsavedChanges(), "Found journals wait for Save like any change.")
                    Assert.AreEqual(0, manuscript.JournalShortlist.Count)
                    CollectionAssert.AreEqual({"Added 2 journals to the shortlist as Considering. Save the manuscript page to keep them."}, messages)

                    board.Editor.FindJournalsForTest()
                    Assert.AreEqual(1, messages.Count, "Journals already on the shortlist aren't added again.")

                    board.PressCommandKey(Keys.Control Or Keys.S)
                    Assert.AreEqual(2, manuscript.JournalShortlist.Count)
                    Dim saved As JournalCandidate = manuscript.JournalShortlist.First()
                    Assert.AreEqual(CandidateStatus.Considering, saved.Status)
                    Assert.AreEqual(3L, saved.Evidence.MatchingArticles)
                    Assert.AreEqual("Anchoring in triage", saved.Evidence.Examples.Single().Title)
                    CollectionAssert.AreEqual({"anchoring effects"}, saved.Evidence.Keywords)
                    Assert.AreEqual("Nature Portfolio", saved.Evidence.Publisher, "The publisher, topics, and homepage are kept with the evidence (#96).")
                    CollectionAssert.AreEqual({"Cancer-related molecular mechanisms research", "MicroRNA in disease regulation"}, saved.Evidence.Topics)
                    Assert.AreEqual("http://www.nature.com/srep/index.html", saved.Evidence.HomepageUrl)
                    board.Close()
                End Using
            End Sub)
    End Sub

    ' Find Journals is unavailable while working offline (#96): the button is
    ' off, the shortlist says why, and turning Work offline off brings it back.
    <TestMethod>
    Public Sub FindJournalsIsUnavailableWhileWorkingOffline()
        RunOnStaThread(
            Sub()
                Dim network As New NoNetwork()
                OnlineAccess.ResetForTests()
                OnlineAccess.InnerHandlerFactory = Function() network
                Try
                    Using board As New PageBoard()
                        Dim manuscript As Manuscript = Sample("Anchoring in clinical risk estimates")
                        board.Prepare(manuscript)
                        board.Open(manuscript)
                        Assert.IsTrue(board.Editor.FindJournalsButton.Enabled)
                        Assert.AreEqual(String.Empty, board.Editor.FindJournalsOffText)

                        ' Work offline, turned on from the rail or the menu.
                        Dim settings As AppSettings = DirectCast(GetType(Form1).GetField("appSettings", BindingFlags.Instance Or BindingFlags.NonPublic).GetValue(board), AppSettings)
                        settings.OnlineServices.WorkOffline = True
                        GetType(Form1).GetMethod("ApplyOnlineSettings", BindingFlags.Instance Or BindingFlags.NonPublic).Invoke(board, Nothing)
                        Application.DoEvents()
                        Assert.IsFalse(board.Editor.FindJournalsButton.Enabled, "Work offline turns the command off...")
                        StringAssert.StartsWith(board.Editor.FindJournalsOffText, "Work offline is on", "...and the shortlist says why.")
                        Assert.IsTrue(Descendants(board.Editor).OfType(Of Label)().Any(Function(label) label.Visible AndAlso label.Text = board.Editor.FindJournalsOffText))
                        Assert.AreEqual(board.Editor.FindJournalsOffText, board.Editor.FindJournalsButton.AccessibleDescription)

                        ' A shortlist change, or the page shown again, keeps it off.
                        board.Editor.candidatePrompt = Function(existing) New JournalCandidate With {.JournalName = "Open Psychology"}
                        board.Editor.AddShortlistCandidateForTest()
                        Assert.IsFalse(board.Editor.FindJournalsButton.Enabled)
                        board.Discard()
                        Assert.IsFalse(board.Editor.FindJournalsButton.Enabled)
                        StringAssert.StartsWith(board.Editor.FindJournalsOffText, "Work offline is on")

                        settings.OnlineServices.WorkOffline = False
                        settings.OnlineServices.TurnedOff.Add(OnlineServiceCatalog.JournalSuggestions)
                        GetType(Form1).GetMethod("ApplyOnlineSettings", BindingFlags.Instance Or BindingFlags.NonPublic).Invoke(board, Nothing)
                        Assert.IsFalse(board.Editor.FindJournalsButton.Enabled)
                        StringAssert.StartsWith(board.Editor.FindJournalsOffText, "Find journals (OpenAlex) is turned off", "Turned off in Online services, it says so.")

                        settings.OnlineServices.TurnedOff.Clear()
                        GetType(Form1).GetMethod("ApplyOnlineSettings", BindingFlags.Instance Or BindingFlags.NonPublic).Invoke(board, Nothing)
                        Application.DoEvents()
                        Assert.IsTrue(board.Editor.FindJournalsButton.Enabled, "Back online, Find Journals is available again.")
                        Assert.AreEqual(String.Empty, board.Editor.FindJournalsOffText)
                        Assert.IsFalse(Descendants(board.Editor).OfType(Of Label)().Any(Function(label) label.Visible AndAlso label.Text.StartsWith("Work offline is on", StringComparison.Ordinal)))
                        Assert.AreEqual(0, network.Requests, "Nothing was sent.")
                        Assert.AreEqual(0, board.SaveCount)
                        board.Close()
                    End Using
                Finally
                    OnlineAccess.ResetForTests()
                End Try
            End Sub)
    End Sub

    <TestMethod>
    Public Sub ReportsPreviewAndSaveWhatIsChosen()
        RunOnStaThread(
            Sub()
                Dim path As String = IO.Path.Combine(IO.Path.GetTempPath(), "PaperRoute-Report-" & Guid.NewGuid().ToString("N") & ".html")
                Try
                    Dim pipeline As Manuscript = Sample("In the pipeline")
                    Dim drawer As Manuscript = Sample("In the drawer")
                    drawer.Location = ManuscriptLocation.FileDrawer
                    Using dialog As New ReportForm("Pipeline Report", "report.html", New List(Of Manuscript) From {pipeline, drawer},
                                                   Function(chosen, deadlines) ReportService.PipelineReport(chosen, DateTime.Today))
                        dialog.SavePathPrompt = Function() path
                        ShowOffscreen(dialog)
                        CollectionAssert.AreEqual({pipeline}, dialog.Chosen, "Pipeline work is included by default; the File Drawer is not.")
                        dialog.SaveReport(Nothing, EventArgs.Empty)
                        Assert.AreEqual(path, dialog.SavedPath)
                    End Using
                    Dim saved As String = IO.File.ReadAllText(path)
                    StringAssert.Contains(saved, "In the pipeline")
                    Assert.IsFalse(saved.Contains("In the drawer"))
                Finally
                    If IO.File.Exists(path) Then IO.File.Delete(path)
                End Try
            End Sub)
    End Sub

    ' Work types and tags (#64): edited on the page, saved with it, shown on
    ' the card, and found by board search.
    <TestMethod>
    Public Sub TypesAndTagsAreEditedOnThePageAndShownOnTheBoard()
        RunOnStaThread(
            Sub()
                Using board As New PageBoard()
                    Dim manuscript As Manuscript = Sample("Tagged manuscript")
                    Dim other As Manuscript = Sample("Other manuscript")
                    other.Tags.Add("grant")
                    board.Prepare(manuscript, other)
                    board.Open(manuscript)

                    Dim type As ComboBox = Descendants(board.Editor).OfType(Of ComboBox)().Single(Function(box) box.AccessibleName = "Type of work")
                    Assert.AreEqual("Not specified", type.Text, "No type is inferred.")
                    type.SelectedIndex = CInt(WorkType.Poster)
                    Dim tags As TagEditor = Descendants(board.Editor).OfType(Of TagEditor)().Single()
                    CollectionAssert.Contains(tags.Input.AutoCompleteCustomSource.Cast(Of String)().ToList(), "grant", "Tags already in the library are suggested.")
                    tags.Input.Text = "teaching"
                    tags.Commit()
                    tags.Input.Text = " Teaching "
                    tags.Commit()
                    Assert.AreEqual(1, Descendants(tags).OfType(Of TagChipButton)().Count(), "A tag is added once.")
                    Assert.IsTrue(board.Editor.HasUnsavedChanges())
                    Assert.AreEqual(WorkType.Unspecified, manuscript.WorkType, "Edits stay in the working copy until saved.")

                    board.PressCommandKey(Keys.Control Or Keys.S)
                    Assert.AreEqual(WorkType.Poster, manuscript.WorkType)
                    CollectionAssert.AreEqual({"teaching"}, manuscript.Tags)
                    Assert.AreEqual(1, board.SaveCount)

                    board.PressCommandKey(Keys.Alt Or Keys.Left)
                    Assert.AreEqual("Board", board.PageName)
                    Dim strip As TagStrip = Descendants(board).OfType(Of TagStrip)().Single(Function(item) item.AccessibleName = "Tags: teaching")
                    Assert.IsTrue(strip.Width > 0, "The card shows the tag on its journal line.")

                    Dim search As TextBox = DirectCast(GetType(Form1).GetField("txtBoardSearch", BindingFlags.Instance Or BindingFlags.NonPublic).GetValue(board), TextBox)
                    Dim matches = GetType(Form1).GetMethod("ManuscriptMatchesBoardFilters", BindingFlags.Instance Or BindingFlags.NonPublic)
                    search.Text = "#teaching"
                    Assert.IsTrue(CBool(matches.Invoke(board, New Object() {manuscript})))
                    Assert.IsFalse(CBool(matches.Invoke(board, New Object() {other})))
                    search.Text = "poster"
                    Assert.IsTrue(CBool(matches.Invoke(board, New Object() {manuscript})), "The type is searchable too.")
                    search.Text = String.Empty

                    board.Close()
                End Using
            End Sub)
    End Sub

    Private Shared Function DeadlineRows(board As PageBoard) As List(Of DeadlineRow)
        Return Descendants(board).OfType(Of DeadlineRow)().ToList()
    End Function

    Private Shared Sub ClickDoneLink(board As PageBoard)
        Dim link As LinkLabel = Descendants(board).OfType(Of LinkLabel)().Single(Function(candidate) candidate.Text.StartsWith("Done in the last"))
        GetType(LinkLabel).GetMethod("OnLinkClicked", BindingFlags.Instance Or BindingFlags.NonPublic).
            Invoke(link, New Object() {New LinkLabelLinkClickedEventArgs(link.Links(0))})
        Application.DoEvents()
    End Sub

    Private Shared Sub ClickControl(control As Control)
        GetType(Control).GetMethod("OnClick", BindingFlags.Instance Or BindingFlags.NonPublic).Invoke(control, New Object() {EventArgs.Empty})
        Application.DoEvents()
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
    Private NotInheritable Class NoNetwork
        Inherits Net.Http.HttpMessageHandler

        Public Requests As Integer

        Protected Overrides Function SendAsync(request As Net.Http.HttpRequestMessage, cancellationToken As CancellationToken) As Task(Of Net.Http.HttpResponseMessage)
            Interlocked.Increment(Requests)
            Throw New InvalidOperationException("No request was expected.")
        End Function
    End Class

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

        Public Sub SetAuthors(ParamArray authors As AuthorRecord())
            Dim library As New AuthorLibraryData()
            library.Authors.AddRange(authors)
            GetType(Form1).GetField("authorLibrary", BindingFlags.Instance Or BindingFlags.NonPublic).SetValue(Me, library)
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

        Public Sub SetDeadlinePrompts(datePrompt As Func(Of DeadlineItem, DateTime?), confirmPrompt As Func(Of String, Boolean))
            deadlineDatePrompt = datePrompt
            deadlineConfirmPrompt = confirmPrompt
        End Sub

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
