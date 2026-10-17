Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.Globalization
Imports System.IO
Imports System.Linq
Imports System.Reflection
Imports System.Text.Json
Imports System.Windows.Forms
Imports ManuscriptPipeline
Imports ManuscriptPipeline.Controls
Imports ManuscriptPipeline.Forms
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services
Imports Microsoft.VisualStudio.TestTools.UnitTesting

' Opening one manuscript after another on the manuscript page (#133): no
' edit, working copy or unsaved-changes question of the manuscript left can
' reach the one opened next. Each test edits one record in every way the
' page allows, lets the edits go or keeps them, opens another, and holds the
' page to what a window that only ever opened the second one shows, control
' by control. Written before the page kept its editor between manuscripts,
' they held for an editor built anew for each one, and they hold the kept
' editor to the same.
<TestClass>
<DoNotParallelize>
Public Class ManuscriptEditorReuseTests

    Private _root As String = String.Empty


    <TestInitialize>
    Public Sub Initialize()
        _root = CreateTemporaryRoot()
    End Sub


    <TestCleanup>
    Public Sub Cleanup()
        DeleteTemporaryRoot(_root)
    End Sub


    ' Edits to a filed manuscript, let go with No, then a Pipeline
    ' manuscript; and the other way round.
    <TestMethod>
    Public Sub NothingOfTheManuscriptLeftReachesTheNextOne()

        Dim library As LargeLibraryFixture.SyntheticLibrary = Library40()
        Dim filed As Manuscript = FiledManuscript(library)
        Dim active As Manuscript = ActiveManuscript(library)

        For Each pair In {(filed, active), (active, filed)}

            Dim left As Manuscript = pair.Item1
            Dim opened As Manuscript = pair.Item2

            StaRunner.Run(
                Sub()
                    Using reused As New LibraryBoard(library, Path.Combine(_root, "settings-reused")),
                          fresh As New LibraryBoard(library, Path.Combine(_root, "settings-fresh"))

                        reused.Start()
                        fresh.Start()

                        Dim savedLeft As String = Json(Record(reused, left.Id))
                        Dim savedOpened As String = Json(Record(reused, opened.Id))

                        Open(reused, Record(reused, left.Id))
                        EditEverything(reused)
                        Assert.IsTrue(reused.Editor.HasUnsavedChanges(), "The edits are unsaved changes.")

                        Dim asked As New List(Of String)()
                        reused.SetPrompt(Function(title)
                                             asked.Add(title)
                                             Return DialogResult.No
                                         End Function)

                        Open(reused, Record(reused, opened.Id))

                        CollectionAssert.AreEqual({left.Title}, asked, "The question names the manuscript being left, once.")
                        Assert.AreEqual(opened.Id, reused.OpenManuscriptId)
                        Assert.IsFalse(reused.Editor.HasUnsavedChanges(), "The manuscript opened has no unsaved changes.")
                        Assert.AreEqual(savedLeft, Json(Record(reused, left.Id)), "No left the edited manuscript as it was saved.")
                        Assert.AreEqual(savedOpened, Json(Record(reused, opened.Id)), "Opening changes nothing.")
                        Assert.AreEqual(0, reused.SaveCount, "Nothing was saved.")

                        Open(fresh, Record(fresh, opened.Id))
                        AssertSamePage(fresh, reused, opened.Title)

                        reused.SetPrompt(Function(title) DialogResult.No)
                        reused.Close()
                        fresh.Close()
                    End Using
                End Sub, TimeSpan.FromMinutes(5))

        Next

    End Sub


    ' Yes saves the manuscript being left, and only it, before the next
    ' opens; Cancel stays on it with its edits.
    <TestMethod>
    Public Sub YesSavesOnlyTheManuscriptLeftAndCancelStaysOnIt()

        Dim library As LargeLibraryFixture.SyntheticLibrary = Library40()
        Dim filed As Manuscript = FiledManuscript(library)
        Dim active As Manuscript = ActiveManuscript(library)

        StaRunner.Run(
            Sub()
                Using board As New LibraryBoard(library, Path.Combine(_root, "settings"))

                    board.Start()
                    Dim savedActive As String = Json(Record(board, active.Id))

                    Open(board, Record(board, filed.Id))
                    board.Field("Title").Text = filed.Title & " (kept)"

                    board.SetPrompt(Function(title) DialogResult.Cancel)
                    Open(board, Record(board, active.Id))
                    Assert.AreEqual("Manuscript", board.PageName)
                    Assert.AreEqual(filed.Id, board.OpenManuscriptId, "Cancel stays on the manuscript being edited.")
                    Assert.AreEqual(filed.Title & " (kept)", board.Field("Title").Text, "Cancel keeps the edits.")
                    Assert.IsTrue(board.Editor.HasUnsavedChanges())
                    Assert.AreEqual(0, board.SaveCount)

                    board.SetPrompt(Function(title) DialogResult.Yes)
                    Open(board, Record(board, active.Id))
                    Assert.AreEqual(active.Id, board.OpenManuscriptId)
                    Assert.AreEqual(1, board.SaveCount, "Yes saves once.")
                    Assert.AreEqual(filed.Title & " (kept)", Record(board, filed.Id).Title, "Yes saved the edits of the manuscript left.")
                    Assert.AreEqual(savedActive, Json(Record(board, active.Id)), "The manuscript opened is as it was saved.")
                    Assert.AreEqual(active.Title, board.Field("Title").Text)
                    Assert.IsFalse(board.Editor.HasUnsavedChanges())

                    board.Close()
                End Using
            End Sub, TimeSpan.FromMinutes(5))

    End Sub


    ' A recovered draft (#123) is unsaved changes of its own manuscript
    ' only: the next one opens clean, and the first opens again as saved.
    <TestMethod>
    Public Sub ARecoveredDraftIsUnsavedOnlyForItsOwnManuscript()

        Dim library As LargeLibraryFixture.SyntheticLibrary = Library40()
        Dim filed As Manuscript = FiledManuscript(library)
        Dim active As Manuscript = ActiveManuscript(library)

        Dim draft As Manuscript = ManuscriptCloneService.CloneManuscript(filed)
        draft.Title = filed.Title & " (recovered)"
        Dim drafts As New RecoveryDraftStore(Path.GetDirectoryName(library.Repository.DataFilePath))

        StaRunner.Run(
            Sub()
                Using board As New LibraryBoard(library, Path.Combine(_root, "settings")),
                      fresh As New LibraryBoard(library, Path.Combine(_root, "settings-fresh"))

                    ' Start finds no draft; one is written, as a shutdown
                    ' would, and the offer is accepted.
                    board.Start()
                    drafts.Write(New RecoveryDraft With {.ManuscriptId = filed.Id, .WrittenAtUtc = DateTime.UtcNow, .Manuscript = draft})
                    board.AcceptRecoveryDraft()
                    Application.DoEvents()

                    Assert.AreEqual(filed.Id, board.OpenManuscriptId)
                    Assert.AreEqual(draft.Title, board.Field("Title").Text, "The page opens with the draft.")
                    Assert.IsTrue(board.Editor.HasUnsavedChanges(), "The draft is unsaved changes.")

                    board.SetPrompt(Function(title) DialogResult.No)
                    Open(board, Record(board, active.Id))
                    Assert.IsFalse(board.Editor.HasUnsavedChanges(), "The next manuscript has none.")

                    fresh.Start()
                    Open(fresh, Record(fresh, active.Id))
                    AssertSamePage(fresh, board, active.Title)

                    board.SetPrompt(Function(title)
                                        Assert.Fail("Nothing is unsaved, so nothing is asked.")
                                        Return DialogResult.Cancel
                                    End Function)
                    Open(board, Record(board, filed.Id))
                    Assert.AreEqual(filed.Title, board.Field("Title").Text, "The draft's manuscript opens again as saved.")
                    Assert.IsFalse(board.Editor.HasUnsavedChanges())
                    Assert.AreEqual(0, board.SaveCount)

                    board.SetPrompt(Function(title) DialogResult.No)
                    board.Close()
                    fresh.Close()
                End Using
            End Sub, TimeSpan.FromMinutes(5))

    End Sub


    ' After a save that failed (#113) the Unsaved changes bar says Save
    ' tries again, on every manuscript opened until a save succeeds.
    <TestMethod>
    Public Sub TheFailedSaveHintShowsOnTheNextManuscriptToo()

        Dim library As LargeLibraryFixture.SyntheticLibrary = Library40()
        Dim filed As Manuscript = FiledManuscript(library)
        Dim active As Manuscript = ActiveManuscript(library)

        StaRunner.Run(
            Sub()
                Using board As New LibraryBoard(library, Path.Combine(_root, "settings"))

                    board.Start()
                    board.MarkLastSaveFailed()

                    For Each manuscript As Manuscript In {filed, active}
                        Open(board, Record(board, manuscript.Id))
                        board.Tick()
                        Assert.IsTrue(board.SaveBarVisible, "The bar shows on " & manuscript.Title & ".")
                        Assert.AreEqual("Your last save did not finish. Save tries again.", board.SaveBarHint)
                        Assert.IsFalse(board.Editor.HasUnsavedChanges())
                    Next

                    board.SetPrompt(Function(title) DialogResult.No)
                    board.Close()
                End Using
            End Sub, TimeSpan.FromMinutes(5))

    End Sub


    ' A waypoint Route View asked for, and a window the submission's
    ' details asked for, both still waiting when the next manuscript opens,
    ' do nothing to it.
    <TestMethod>
    Public Sub WorkPostedForTheManuscriptLeftDoesNothingToTheNext()

        Dim library As LargeLibraryFixture.SyntheticLibrary = Library40()
        Dim filed As Manuscript = FiledManuscript(library)
        Dim active As Manuscript = ActiveManuscript(library)

        StaRunner.Run(
            Sub()
                Using board As New LibraryBoard(library, Path.Combine(_root, "settings")),
                      fresh As New LibraryBoard(library, Path.Combine(_root, "settings-fresh"))

                    board.Start()
                    fresh.Start()
                    Open(fresh, Record(fresh, active.Id))

                    ' The waypoint is posted while the page opens.
                    Dim waypoint As New ManuscriptRouteWaypoint With {
                        .Kind = ManuscriptRouteWaypointKind.Submission,
                        .SubmissionId = Record(board, filed.Id).Submissions(0).Id
                    }
                    board.OpenAt(Record(board, filed.Id), waypoint)
                    board.Open(Record(board, active.Id))
                    Application.DoEvents()
                    AssertSamePage(fresh, board, active.Title)

                    ' The details' request is posted from its own click.
                    Open(board, Record(board, filed.Id))
                    Dim detail As SubmissionDetailsForm = ViewCatalog.Descendants(board.Editor).OfType(Of SubmissionDetailsForm)().Single()
                    Dim request As New SubmissionWorkflowRequest With {
                        .Target = SubmissionWorkflowTarget.Version
                    }
                    Dim handlers As [Delegate] = DirectCast(GetType(SubmissionDetailsForm).
                        GetField("NavigationRequestedEvent", BindingFlags.Instance Or BindingFlags.NonPublic).GetValue(detail), [Delegate])
                    handlers.DynamicInvoke(request)
                    board.Open(Record(board, active.Id))
                    Application.DoEvents()
                    AssertSamePage(fresh, board, active.Title)

                    Assert.AreEqual(0, board.SaveCount)
                    board.Close()
                    fresh.Close()
                End Using
            End Sub, TimeSpan.FromMinutes(5))

    End Sub


    ' ---------------------------------------------------------------
    ' The library and its two manuscripts
    ' ---------------------------------------------------------------

    ' Forty synthetic manuscripts, with the filed one given a latest
    ' submission whose two reviewer comments can be reordered.
    Private Function Library40() As LargeLibraryFixture.SyntheticLibrary

        Dim library As LargeLibraryFixture.SyntheticLibrary =
            LargeLibraryFixture.Create(Path.Combine(_root, "library"), 40)

        Dim filed As Manuscript = library.Manuscripts.First(Function(item) item.Location = ManuscriptLocation.FileDrawer)
        filed.FileDrawerReason = "Set aside for a later revision."
        Dim latest As New JournalSubmission With {.JournalName = "Fictional Journal 03", .SubmittedDate = LargeLibraryFixture.Today.AddDays(-20)}
        Dim decision As New EditorialDecisionEvent With {.Decision = EditorialDecision.MajorRevision, .DecisionDate = LargeLibraryFixture.Today.AddDays(-5)}
        latest.Decisions.Add(decision)
        latest.ReviewerResponses.Add(New ReviewerResponseItem With {.DecisionId = decision.Id, .RevisionRoundNumber = 1, .ReviewerLabel = "Reviewer 1", .CommentText = "Clarify the sample."})
        latest.ReviewerResponses.Add(New ReviewerResponseItem With {.DecisionId = decision.Id, .RevisionRoundNumber = 1, .ReviewerLabel = "Reviewer 2", .CommentText = "Share the data."})
        filed.Submissions.Add(latest)

        library.Repository.Save(library.Manuscripts)
        Return library

    End Function


    Private Shared Function FiledManuscript(library As LargeLibraryFixture.SyntheticLibrary) As Manuscript
        Return library.Manuscripts.First(Function(item) item.Location = ManuscriptLocation.FileDrawer)
    End Function


    ' A Pipeline manuscript with submissions, a shortlist and tags.
    Private Shared Function ActiveManuscript(library As LargeLibraryFixture.SyntheticLibrary) As Manuscript
        Return library.Manuscripts.First(Function(item) item.Location = ManuscriptLocation.Pipeline AndAlso
                                             item.Submissions.Count > 0 AndAlso item.JournalShortlist.Count > 0 AndAlso item.Tags.Count > 0)
    End Function


    ' The board's own record of a manuscript.
    Private Shared Function Record(board As LibraryBoard, id As Guid) As Manuscript
        Return board.Library.Single(Function(item) item.Id = id)
    End Function


    Private Shared Function Json(manuscript As Manuscript) As String
        Return JsonSerializer.Serialize(manuscript)
    End Function


    ' Opens a manuscript as a click on its card does, and processes what
    ' the page posts as it opens.
    Friend Shared Sub Open(board As LibraryBoard, manuscript As Manuscript)
        board.Open(manuscript)
        Application.DoEvents()
    End Sub


    ' Every edit the page allows that leaves a mark on the page itself: the
    ' fields, an uncommitted and a committed tag, the type, an author
    ' removed and another selected, a shortlisted journal, the reviewer
    ' comments reordered, another submission selected, the Overview
    ' scrolled, focus in the title, and the Submissions tab left showing.
    Private Shared Sub EditEverything(board As LibraryBoard)

        Dim editor As EditManuscriptForm = board.Editor

        board.Field("Title").Text = "A title that must not reach the next manuscript"
        board.Field("Target journal").Text = "A journal that must not reach the next manuscript"
        If Field(Of SectionCard)(editor, "fileDrawerGroup").Visible Then
            board.Field("File Drawer reason").Text = "A reason that must not reach the next manuscript"
        End If

        Dim stage As ComboBox = ViewCatalog.Descendants(editor).OfType(Of ComboBox)().Single(Function(box) box.AccessibleName = "Current stage")
        stage.SelectedIndex = (stage.SelectedIndex + 1) Mod stage.Items.Count
        Dim type As ComboBox = ViewCatalog.Descendants(editor).OfType(Of ComboBox)().Single(Function(box) box.AccessibleName = "Type of work")
        type.SelectedIndex = (type.SelectedIndex + 1) Mod type.Items.Count

        Dim tags As TagEditor = Field(Of TagEditor)(editor, "tagEditor")
        tags.Input.Text = "committed-leak"
        tags.Commit()

        Dim authors As ListBox = Field(Of ListBox)(editor, "lstAuthors")
        If authors.Items.Count > 0 Then
            authors.SelectedIndex = 0
            Field(Of Button)(editor, "btnRemoveAuthor").PerformClick()
        End If
        If authors.Items.Count > 0 Then
            authors.SelectedIndex = authors.Items.Count - 1
        End If

        editor.candidatePrompt = Function(existing) New JournalCandidate With {.JournalName = "A journal shortlisted only for the manuscript left", .Status = CandidateStatus.Preferred, .Notes = "Must not leak."}
        editor.AddShortlistCandidateForTest()
        editor.candidatePrompt = Nothing

        ShowSection(editor, "Submissions")
        Dim matrices As List(Of ReviewerResponseMatrixForm) = ViewCatalog.Descendants(editor).OfType(Of ReviewerResponseMatrixForm)().ToList()
        If matrices.Count = 1 Then
            Dim responses As ListBox = DirectCast(GetType(ReviewerResponseMatrixForm).GetField("lstResponses", BindingFlags.Instance Or BindingFlags.NonPublic).GetValue(matrices(0)), ListBox)
            If responses.Items.Count > 1 Then
                responses.SelectedIndex = 0
                GetType(ReviewerResponseMatrixForm).GetMethod("MoveItem", BindingFlags.Instance Or BindingFlags.NonPublic).Invoke(matrices(0), New Object() {1})
                Application.DoEvents()
            End If
        End If

        Dim submissions As ListBox = Field(Of ListBox)(editor, "lstSubmissions")
        If submissions.Items.Count > 1 Then
            submissions.SelectedIndex = 0
            Application.DoEvents()
        End If

        ShowSection(editor, "Overview")
        Dim overview As ScrollableControl = DirectCast(Sections(editor)(0).Tag, ScrollableControl)
        overview.AutoScrollPosition = New Point(0, 10000)
        editor.ActiveControl = ViewCatalog.Descendants(editor).OfType(Of TextBox)().Single(Function(box) box.AccessibleName = "Title")
        tags.Input.Text = "uncommitted-leak"
        ShowSection(editor, "Submissions")
        Application.DoEvents()

    End Sub


    Private Shared Function Sections(editor As EditManuscriptForm) As List(Of ShelfTabButton)
        Return DirectCast(GetType(EditManuscriptForm).GetField("_sectionTabs", BindingFlags.Instance Or BindingFlags.NonPublic).GetValue(editor), List(Of ShelfTabButton))
    End Function


    Private Shared Function ShownSection(editor As EditManuscriptForm) As String
        Return String.Join(",", Sections(editor).Where(Function(tab) tab.Checked).Select(Function(tab) tab.Text))
    End Function


    Private Shared Sub ShowSection(editor As EditManuscriptForm, name As String)
        Sections(editor).Single(Function(tab) tab.Text = name).Checked = True
        Application.DoEvents()
    End Sub


    Private Shared Function Field(Of T)(target As Object, name As String) As T
        Return DirectCast(target.GetType().GetField(name, BindingFlags.Instance Or BindingFlags.NonPublic).GetValue(target), T)
    End Function


    ' ---------------------------------------------------------------
    ' The page, control by control
    ' ---------------------------------------------------------------

    ' The page on the board under test matches the page on a board that
    ' opened only this manuscript, on every section: what each control
    ' shows, where it is, and whether it is visible, enabled, selected,
    ' scrolled or undoable; then the editor's own state.
    Friend Shared Sub AssertSamePage(expected As LibraryBoard, actual As LibraryBoard, title As String)

        Assert.AreEqual(title, actual.Field("Title").Text)
        Assert.AreEqual(ShownSection(expected.Editor), ShownSection(actual.Editor), "The section the page shows")
        AssertSameLines(Describe(expected.PageView), Describe(actual.PageView), "The page as it opened")

        For Each section As String In {"Overview", "Authors", "Versions", "Submissions", "Readiness & Packets", "Overview"}
            ShowSection(expected.Editor, section)
            ShowSection(actual.Editor, section)
            AssertSameLines(Describe(expected.PageView), Describe(actual.PageView), "The " & section & " section")
        Next

        Assert.AreEqual(expected.Editor.HasUnsavedChanges(), actual.Editor.HasUnsavedChanges(), "Unsaved changes")
        Assert.AreEqual(Json(expected.Editor.RecoveryCopy()), Json(actual.Editor.RecoveryCopy()), "The working copy")
        Assert.AreEqual(ActiveName(expected.Editor), ActiveName(actual.Editor), "The editor's active control")
        Assert.AreEqual(expected.SaveBarVisible, actual.SaveBarVisible, "The Unsaved changes bar")

    End Sub


    Private Shared Sub AssertSameLines(expected As List(Of String), actual As List(Of String), context As String)

        Dim differences As New List(Of String)()

        For index As Integer = 0 To Math.Max(expected.Count, actual.Count) - 1
            Dim wanted As String = If(index < expected.Count, expected(index), "(nothing)")
            Dim found As String = If(index < actual.Count, actual(index), "(nothing)")
            If wanted <> found Then
                differences.Add("expected " & wanted & Environment.NewLine & "   found " & found)
                If differences.Count = 8 Then Exit For
            End If
        Next

        Assert.AreEqual(0, differences.Count,
            context & " differs from a page that opened only this manuscript (" & expected.Count & " against " & actual.Count & " controls):" &
            Environment.NewLine & String.Join(Environment.NewLine, differences))

    End Sub


    Private Shared Function ActiveName(container As ContainerControl) As String
        Dim active As Control = container.ActiveControl
        Return If(active Is Nothing, "(none)", active.GetType().Name & " " & If(active.AccessibleName, active.Name))
    End Function


    ' One line per control, depth first. What a control shows and holds is
    ' described wherever it is; whether it is set visible only while its
    ' parent is shown, and where it is laid out and how far it is scrolled
    ' only while it is shown itself, since WinForms lays out and updates a
    ' hidden control only once it shows, and a section the user has looked
    ' at keeps its last layout while hidden.
    Friend Shared Function Describe(root As Control) As List(Of String)
        Dim lines As New List(Of String)()
        Describe(root, root.GetType().Name, lines)
        Return lines
    End Function


    Private Shared Sub Describe(control As Control, path As String, lines As List(Of String))

        Dim shown As Boolean = control.Parent Is Nothing OrElse control.Parent.Visible

        Dim parts As New List(Of String) From {
            path,
            "text=" & control.Text,
            "enabled=" & control.Enabled.ToString(),
            "colors=" & control.BackColor.ToArgb().ToString("X8", CultureInfo.InvariantCulture) & "/" & control.ForeColor.ToArgb().ToString("X8", CultureInfo.InvariantCulture),
            "name=" & If(control.AccessibleName, String.Empty),
            "tab=" & control.TabStop.ToString() & "," & control.TabIndex.ToString(CultureInfo.InvariantCulture)
        }

        If shown Then parts.Add("own visible=" & OwnVisible(control).ToString())
        If control.Visible Then parts.Add("bounds=" & control.Bounds.ToString())

        Dim box As TextBoxBase = TryCast(control, TextBoxBase)
        If box IsNot Nothing Then parts.Add("undo=" & box.CanUndo.ToString())

        Dim check As CheckBox = TryCast(control, CheckBox)
        If check IsNot Nothing Then parts.Add("checked=" & check.Checked.ToString())

        Dim radio As RadioButton = TryCast(control, RadioButton)
        If radio IsNot Nothing Then parts.Add("checked=" & radio.Checked.ToString())

        Dim list As ListBox = TryCast(control, ListBox)
        If list IsNot Nothing Then
            parts.Add("selected=" & list.SelectedIndex.ToString(CultureInfo.InvariantCulture))
            parts.Add("items=" & String.Join(" | ", list.Items.Cast(Of Object)().Select(Function(item) Convert.ToString(item, CultureInfo.InvariantCulture))))
        End If

        Dim combo As ComboBox = TryCast(control, ComboBox)
        If combo IsNot Nothing Then
            parts.Add("selected=" & combo.SelectedIndex.ToString(CultureInfo.InvariantCulture))
            parts.Add("items=" & combo.Items.Count.ToString(CultureInfo.InvariantCulture))
        End If

        ' A tab control reports no selected tab until it has its window.
        Dim tabs As TabControl = TryCast(control, TabControl)
        If tabs IsNot Nothing AndAlso control.Visible Then parts.Add("tab selected=" & tabs.SelectedIndex.ToString(CultureInfo.InvariantCulture))

        Dim scrollable As ScrollableControl = TryCast(control, ScrollableControl)
        If control.Visible AndAlso scrollable IsNot Nothing AndAlso scrollable.AutoScroll Then parts.Add("scroll=" & scrollable.AutoScrollPosition.ToString())

        Dim chip As TagChipButton = TryCast(control, TagChipButton)
        If chip IsNot Nothing Then parts.Add("tag=" & chip.TagText)

        lines.Add(String.Join("; ", parts))

        For index As Integer = 0 To control.Controls.Count - 1
            Dim child As Control = control.Controls(index)
            Describe(child, path & "/" & index.ToString(CultureInfo.InvariantCulture) & ":" & child.GetType().Name, lines)
        Next

    End Sub


    ' Whether the control itself is set visible, whatever its parents are.
    Friend Shared Function OwnVisible(control As Control) As Boolean
        Return CBool(GetStateMethod.Invoke(control, New Object() {VisibleState}))
    End Function

    Private Shared ReadOnly GetStateMethod As MethodInfo =
        GetType(Control).GetMethods(BindingFlags.Instance Or BindingFlags.NonPublic).
            Single(Function(method) method.Name = "GetState" AndAlso method.GetParameters().Length = 1 AndAlso method.GetParameters()(0).ParameterType.IsEnum)

    Private Shared ReadOnly VisibleState As Object =
        [Enum].Parse(GetStateMethod.GetParameters()(0).ParameterType, "Visible")

End Class
