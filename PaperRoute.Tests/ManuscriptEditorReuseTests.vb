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
        ' The submission's details and the version history look up the
        ' managed library, under the default data root unless told.
        StorageEnvironment.RootOverride = Path.Combine(_root, "storage")
    End Sub


    <TestCleanup>
    Public Sub Cleanup()
        UiTheme.HighContrastOverride = Nothing
        StorageEnvironment.RootOverride = Nothing
        DeleteTemporaryRoot(_root)
    End Sub


    ' Edits to a filed manuscript, let go with No, then a Pipeline
    ' manuscript, opened from the page left; and the other way round, by
    ' the usual way: back to the Board, where the page left lets go of the
    ' record while the window idles, then the next card. The page left
    ' holds its layout from letting go until the next open (#133), so that
    ' open lays out from the last record's shape: each in Light, Dark and
    ' a contrast theme.
    <TestMethod>
    <DataRow(SystemColorMode.Classic, False)>
    <DataRow(SystemColorMode.Dark, False)>
    <DataRow(SystemColorMode.Classic, True)>
    Public Sub NothingOfTheManuscriptLeftReachesTheNextOne(mode As SystemColorMode, contrastTheme As Boolean)

        Dim library As LargeLibraryFixture.SyntheticLibrary = Library40()
        Dim filed As Manuscript = FiledManuscript(library)
        Dim active As Manuscript = ActiveManuscript(library)

        For Each pair In {(filed, active, False), (active, filed, True)}

            Dim left As Manuscript = pair.Item1
            Dim opened As Manuscript = pair.Item2
            Dim throughTheBoard As Boolean = pair.Item3

            StaRunner.Run(
                Sub()
                    If mode = SystemColorMode.Dark AndAlso (SystemInformation.HighContrast OrElse Not OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000)) Then
                        Assert.Inconclusive("Native WinForms dark mode requires Windows 11 or later, outside a Windows contrast theme.")
                    End If

                    UiTheme.HighContrastOverride = contrastTheme
                    Try
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

                            If throughTheBoard Then
                                reused.NavigateTo(Form1.WorkspacePage.Board)
                                reused.WaitForIdleWork()
                                Assert.IsTrue(ViewCatalog.Descendants(reused.KeptPage).OfType(Of TextBox)().All(Function(box) box.TextLength = 0), "The page left let go of the record.")
                            End If

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
                    Finally
                        UiTheme.HighContrastOverride = Nothing
                    End Try
                End Sub, TimeSpan.FromMinutes(5), mode)

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


    ' An open that fails part way, as one whose author library cannot be
    ' read does (#121): the page does not become the failed manuscript's,
    ' so nothing of the manuscript left is asked about, saved or written
    ' to a recovery draft in its name, and it opens once it can.
    <TestMethod>
    Public Sub AnOpenThatFailsLeavesNothingToAskAboutOrSave()

        Dim library As LargeLibraryFixture.SyntheticLibrary = Library40()
        Dim filed As Manuscript = FiledManuscript(library)
        Dim active As Manuscript = ActiveManuscript(library)
        Dim authors As String = library.AuthorRepository.DataFilePath
        Dim authorsBackup As String = library.AuthorRepository.BackupFilePath

        StaRunner.Run(
            Sub()
                Using board As New LibraryBoard(library, Path.Combine(_root, "settings"))

                    board.Start()
                    Open(board, Record(board, filed.Id))
                    board.Field("Title").Text = filed.Title & " (answered No)"

                    Dim asked As New List(Of String)()
                    board.SetPrompt(Function(title)
                                        asked.Add(title)
                                        Return DialogResult.No
                                    End Function)

                    ' The author library becomes unreadable, with no backup.
                    Dim saved As Byte() = If(File.Exists(authors), File.ReadAllBytes(authors), Nothing)
                    Dim savedBackup As Byte() = If(File.Exists(authorsBackup), File.ReadAllBytes(authorsBackup), Nothing)
                    File.WriteAllText(authors, "{ damaged")
                    File.Delete(authorsBackup)

                    Dim failure As Exception = Nothing
                    Try
                        board.Open(Record(board, active.Id))
                    Catch ex As TargetInvocationException
                        failure = ex.InnerException
                    End Try

                    Assert.IsInstanceOfType(failure, GetType(InvalidDataException), "The open failed reading the author library.")
                    CollectionAssert.AreEqual({filed.Title}, asked, "Only the manuscript left was asked about.")
                    Assert.IsNull(board.Editor, "The page did not become the failed manuscript's.")

                    Dim reported As Boolean? = Nothing
                    board.unhandledErrorPrompt = Function(shown, unsaved)
                                                     reported = unsaved
                                                     Return DialogResult.Cancel
                                                 End Function
                    board.ReportUnhandledError(failure)
                    Assert.IsTrue(reported.HasValue AndAlso Not reported.Value, "The error's dialog finds no unsaved changes.")
                    Assert.IsFalse(board.WriteRecoveryDraft("Windows was shutting down"), "No draft is written in the failed manuscript's name.")

                    board.NavigateTo(Form1.WorkspacePage.Board)
                    Application.DoEvents()
                    CollectionAssert.AreEqual({filed.Title}, asked, "Leaving asks nothing.")
                    Assert.AreEqual(0, board.SaveCount, "Nothing was saved.")
                    Assert.AreEqual(filed.Title, Record(board, filed.Id).Title, "No left the manuscript as it was saved.")

                    ' Readable again, the manuscript opens as saved.
                    If saved Is Nothing Then File.Delete(authors) Else File.WriteAllBytes(authors, saved)
                    If savedBackup IsNot Nothing Then File.WriteAllBytes(authorsBackup, savedBackup)
                    Open(board, Record(board, active.Id))
                    Assert.AreEqual(active.Title, board.Field("Title").Text)
                    Assert.IsFalse(board.Editor.HasUnsavedChanges())

                    board.SetPrompt(Function(title) DialogResult.No)
                    board.Close()
                End Using
            End Sub, TimeSpan.FromMinutes(5))

    End Sub


    ' The editor itself: one that has edited a manuscript in every way and
    ' then loads another matches, control by control and color by color,
    ' one built for that other manuscript, in Light, Dark and a contrast
    ' theme; and loading the first again shows it as saved. The second
    ' pass loads it after letting go of the first (UnloadRecord), as the
    ' page does once left. The parts built for each record are legible.
    <TestMethod>
    <DataRow(SystemColorMode.Classic, False)>
    <DataRow(SystemColorMode.Dark, False)>
    <DataRow(SystemColorMode.Classic, True)>
    Public Sub AnEditorThatLoadsAnotherRecordMatchesOneBuiltForIt(mode As SystemColorMode, contrastTheme As Boolean)

        Dim library As LargeLibraryFixture.SyntheticLibrary = Library40()
        Dim filed As Manuscript = FiledManuscript(library)
        Dim active As Manuscript = ActiveManuscript(library)

        StaRunner.Run(
            Sub()
                If mode = SystemColorMode.Dark AndAlso (SystemInformation.HighContrast OrElse Not OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000)) Then
                    Assert.Inconclusive("Native WinForms dark mode requires Windows 11 or later, outside a Windows contrast theme.")
                End If

                UiTheme.HighContrastOverride = contrastTheme
                Try
                    Assert.AreEqual(mode = SystemColorMode.Dark AndAlso Not contrastTheme, UiTheme.IsDark(), "The theme the row runs in.")

                    For Each pair In {(filed, active, False), (active, filed, True)}

                        Dim savedLeft As String = Json(pair.Item1)

                        Dim reused As New EditManuscriptForm(pair.Item1, library.Manuscripts, library.AuthorRepository, pageMode:=True)

                        Using reusedHost As Form = Hosted(reused)

                            EditEverything(reused)
                            Assert.IsTrue(reused.HasUnsavedChanges())
                            Dim token As Object = reused.RecordToken

                            If pair.Item3 Then
                                reused.UnloadRecord()
                                Application.DoEvents()
                                Assert.AreEqual(String.Empty, Box(reused, "Title").Text, "Let go of, the record shows nothing.")
                            End If

                            reused.LoadRecord(pair.Item2, library.Manuscripts)
                            Application.DoEvents()
                            Assert.AreNotSame(token, reused.RecordToken, "The record is replaced as a whole.")
                            Assert.IsFalse(reused.HasUnsavedChanges())
                            Assert.AreEqual(savedLeft, Json(pair.Item1), "Loading another record changes nothing in the one left.")

                            Dim fresh As New EditManuscriptForm(pair.Item2, library.Manuscripts, library.AuthorRepository, pageMode:=True)
                            Using freshHost As Form = Hosted(fresh)
                                AssertSameEditor(freshHost, fresh, reusedHost, reused, pair.Item2.Title)
                                freshHost.Close()
                            End Using

                            AssertRecordPartsLegible(reused, pair.Item2)

                            reused.LoadRecord(pair.Item1, library.Manuscripts)
                            Application.DoEvents()
                            Assert.AreEqual(pair.Item1.Title, Box(reused, "Title").Text, "The first manuscript opens again as saved.")
                            Assert.IsFalse(reused.HasUnsavedChanges())

                            reusedHost.Close()
                        End Using

                    Next
                Finally
                    UiTheme.HighContrastOverride = Nothing
                End Try
            End Sub,
            TimeSpan.FromMinutes(5),
            mode)

    End Sub


    ' Everything the editor holds is one of: a control or a component, a
    ' setting fixed when it is made, or part of the record it has loaded,
    ' which LoadRecord replaces as a whole. A field added without being
    ' one of these could carry one manuscript's state into the next, so
    ' it fails here until it is placed. TagEditor, which the editor binds
    ' to each record's tags, likewise.
    <TestMethod>
    Public Sub EveryEditorFieldIsAControlASettingOrPartOfTheRecord()

        Dim unplaced As New List(Of String)()

        unplaced.AddRange(
            UnplacedFields(GetType(EditManuscriptForm),
                     {"_record",
                      "_authorRepository", "_pageMode",
                      "candidatePrompt", "journalSuggestionPrompt", "coverLetterPrompt"}))

        ' Bind replaces what it was given.
        unplaced.AddRange(UnplacedFields(GetType(TagEditor), {"_tags", "_library", "_suggestions"}))

        Assert.AreEqual(0, unplaced.Count, "Fields that are neither controls, settings nor part of the record:" & Environment.NewLine & String.Join(Environment.NewLine, unplaced))

    End Sub


    ' The editor shown filling a window of its own, offscreen, as the
    ' manuscript page shows it.
    Private Shared Function Hosted(editor As EditManuscriptForm) As Form

        Dim host As New QuietHost With {.Size = New Size(1100, 900), .StartPosition = FormStartPosition.Manual, .Location = New Point(-20000, -20000), .ShowInTaskbar = False}
        editor.TopLevel = False
        editor.FormBorderStyle = FormBorderStyle.None
        editor.MinimumSize = Size.Empty
        editor.Dock = DockStyle.Fill
        host.Controls.Add(editor)
        editor.Visible = True
        host.Show()
        Application.DoEvents()
        Return host

    End Function


    ' A window that never takes focus from the desktop it is shown on.
    Private NotInheritable Class QuietHost
        Inherits Form

        Protected Overrides ReadOnly Property ShowWithoutActivation As Boolean
            Get
                Return True
            End Get
        End Property

    End Class


    Private Shared Function UnplacedFields(type As Type, placed As String()) As List(Of String)

        Return type.GetFields(BindingFlags.Instance Or BindingFlags.Public Or BindingFlags.NonPublic Or BindingFlags.DeclaredOnly).
            Where(Function(field)
                      Dim kind As Type = field.FieldType
                      If GetType(Control).IsAssignableFrom(kind) OrElse GetType(ComponentModel.IComponent).IsAssignableFrom(kind) Then Return False
                      If kind.IsGenericType AndAlso kind.GetGenericTypeDefinition() Is GetType(List(Of )) AndAlso GetType(Control).IsAssignableFrom(kind.GetGenericArguments()(0)) Then Return False
                      ' An event's handlers, which the page attaches once.
                      If GetType([Delegate]).IsAssignableFrom(kind) AndAlso field.Name.EndsWith("Event", StringComparison.Ordinal) Then Return False
                      Return Not placed.Contains(field.Name)
                  End Function).
            Select(Function(field) type.Name & "." & field.Name & " As " & field.FieldType.Name).
            ToList()

    End Function


    ' ---------------------------------------------------------------
    ' The library and its two manuscripts
    ' ---------------------------------------------------------------

    ' Forty synthetic manuscripts, with the filed one given a latest
    ' submission whose two reviewer comments can be reordered, and the
    ' Pipeline one a latest submission rejected, so its shortlist offers
    ' the next journal on it.
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

        ' Rejected today, after the researcher moved it on today, so the
        ' rejection changes nothing of its stage.
        Dim active As Manuscript = ActiveManuscript(library)
        active.StageEnteredDate = LargeLibraryFixture.Today
        active.JournalShortlist.Insert(0, New JournalCandidate With {.JournalName = "A journal offered after the rejection", .Status = CandidateStatus.Preferred})
        Dim rejected As New JournalSubmission With {.JournalName = "A journal that rejected the manuscript", .SubmittedDate = LargeLibraryFixture.Today}
        rejected.Decisions.Add(New EditorialDecisionEvent With {.Decision = EditorialDecision.Rejected, .DecisionDate = LargeLibraryFixture.Today})
        active.Submissions.Add(rejected)
        Assert.IsNotNull(JournalShortlistService.RerouteOfferFor(active, DateTime.Today), "The Pipeline manuscript's shortlist offers the next journal.")

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
        EditEverything(board.Editor)
    End Sub


    Private Shared Sub EditEverything(editor As EditManuscriptForm)

        Box(editor, "Title").Text = "A title that must not reach the next manuscript"
        Box(editor, "Target journal").Text = "A journal that must not reach the next manuscript"
        If Field(Of SectionCard)(editor, "fileDrawerGroup").Visible Then
            Box(editor, "File Drawer reason").Text = "A reason that must not reach the next manuscript"
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


    Private Shared Function Box(editor As EditManuscriptForm, name As String) As TextBox
        Return ViewCatalog.Descendants(editor).OfType(Of TextBox)().Single(Function(item) item.AccessibleName = name)
    End Function


    Private Shared Function Sections(editor As EditManuscriptForm) As List(Of ShelfTabButton)
        Return DirectCast(GetType(EditManuscriptForm).GetField("_sectionTabs", BindingFlags.Instance Or BindingFlags.NonPublic).GetValue(editor), List(Of ShelfTabButton))
    End Function


    Private Shared Function ShownSection(editor As EditManuscriptForm) As String
        Return String.Join(",", Sections(editor).Where(Function(tab) tab.Checked).Select(Function(tab) tab.Text))
    End Function


    Friend Shared Sub ShowSection(editor As EditManuscriptForm, name As String)
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

        AssertSameEditor(expected.PageView, expected.Editor, actual.PageView, actual.Editor, title)
        Assert.AreEqual(expected.SaveBarVisible, actual.SaveBarVisible, "The Unsaved changes bar")

    End Sub


    ' The same for an editor and what holds it: the page, or the editor
    ' itself shown on its own.
    Private Shared Sub AssertSameEditor(expectedRoot As Control, expected As EditManuscriptForm, actualRoot As Control, actual As EditManuscriptForm, title As String)

        Assert.AreEqual(title, Box(actual, "Title").Text)
        Assert.AreEqual(ShownSection(expected), ShownSection(actual), "The section the page shows")
        AssertSameLines(Described(expectedRoot), Described(actualRoot), "The page as it opened")

        For Each section As String In {"Overview", "Authors", "Versions", "Submissions", "Readiness & Packets", "Overview"}
            ShowSection(expected, section)
            ShowSection(actual, section)
            AssertSameLines(Described(expectedRoot), Described(actualRoot), "The " & section & " section")
        Next

        Assert.AreEqual(expected.HasUnsavedChanges(), actual.HasUnsavedChanges(), "Unsaved changes")
        Assert.AreEqual(Json(expected.RecoveryCopy()), Json(actual.RecoveryCopy()), "The working copy")
        Assert.AreEqual(ActiveName(expected), ActiveName(actual), "The editor's active control")
        Assert.AreEqual(OfferedJournal(expected), OfferedJournal(actual), "The journal the reroute offer would make the target")

    End Sub


    ' The page described once the window has idled, as a researcher sees
    ' it: every list's empty hint follows its list on Application.Idle
    ' (EmptyHint), which it hooks on the first thread to attach a hint, so
    ' on a test thread after the first the hints are brought up to date
    ' here. Until then a hint keeps the colors its list had before it was
    ' styled, as a Dark run shows.
    Private Shared Function Described(root As Control) As List(Of String)
        For Each control As Control In {root}.Concat(ViewCatalog.Descendants(root)).ToList()
            EmptyHint.Refresh(control)
        Next
        Return Describe(root)
    End Function


    ' What the reroute offer's button holds, shown or not.
    Private Shared Function OfferedJournal(editor As EditManuscriptForm) As String
        Dim candidate As JournalCandidate = TryCast(editor.ShortlistOfferButton.Tag, JournalCandidate)
        Return If(candidate Is Nothing, "(none)", candidate.JournalName)
    End Function


    ' The parts the editor builds for each record after the form was
    ' styled (#133): the version history, the shortlist's rows and the tag
    ' chips. A comparison with a fresh editor cannot see a part left
    ' unstyled in both, so each label, button and chip shown is judged on
    ' its own. In Light and Dark its text is held to 4.5:1 against the
    ' surface it is drawn on. Under a contrast theme the colors are the
    ' user's (#138): a label's or button's text and surface are system
    ' colors, and a chip draws in the theme's window colors.
    Private Shared Sub AssertRecordPartsLegible(editor As EditManuscriptForm, manuscript As Manuscript)

        Dim contrastTheme As Boolean = UiTheme.IsHighContrast()
        Dim failures As New List(Of String)()
        Dim judged As New Dictionary(Of String, Integer)()

        For Each part In {("Versions", "_versionsHost"), ("Overview", "shortlistRows"), ("Overview", "tagEditor")}
            ShowSection(editor, part.Item1)
            Dim count As Integer = 0
            Dim drawn As JournalShortlistList = TryCast(Field(Of Control)(editor, part.Item2), JournalShortlistList)
            If drawn IsNot Nothing Then
                judged(part.Item2) = JudgeDrawn(drawn, part.Item2, contrastTheme, failures)
                Continue For
            End If
            For Each control As Control In ViewCatalog.Descendants(Field(Of Control)(editor, part.Item2))
                Dim text As String = If(TypeOf control Is TagChipButton, DirectCast(control, TagChipButton).TagText, control.Text)
                If Not (TypeOf control Is Label OrElse TypeOf control Is Button OrElse TypeOf control Is TagChipButton) OrElse
                   Not control.Visible OrElse Not control.Enabled OrElse String.IsNullOrWhiteSpace(text) Then Continue For
                Dim name As String = part.Item2 & ": " & control.GetType().Name & " '" & text & "' "
                Dim seen As (Ratio As Double, Surface As Color) = TextContrast(control)
                If contrastTheme Then
                    If TypeOf control Is TagChipButton Then
                        If seen.Surface.ToArgb() <> SystemColors.Window.ToArgb() Then failures.Add(name & "drawn on " & seen.Surface.ToString() & ", not the theme's window color")
                    ElseIf Not control.ForeColor.IsSystemColor OrElse Not control.BackColor.IsSystemColor Then
                        failures.Add(name & control.ForeColor.ToString() & " on " & control.BackColor.ToString() & ", not the user's colors")
                    End If
                ElseIf seen.Ratio < 4.5 Then
                    failures.Add(name & seen.Ratio.ToString("F2", CultureInfo.InvariantCulture) & ":1 on " & seen.Surface.ToString())
                End If
                count += 1
            Next
            judged(part.Item2) = count
        Next

        ShowSection(editor, "Overview")

        Assert.IsTrue(judged("_versionsHost") > 0, "The version history shows text to judge.")
        Assert.AreEqual(manuscript.JournalShortlist.Count > 0, judged("shortlistRows") > 0, "The shortlist's rows show text to judge.")
        Assert.AreEqual(manuscript.Tags.Count > 0, judged("tagEditor") > 0, "The tags show chips to judge.")
        Assert.AreEqual(0, failures.Count, "In " & If(contrastTheme, "a contrast theme", If(UiTheme.IsDark(), "Dark", "Light")) & ":" & Environment.NewLine & String.Join(Environment.NewLine, failures))

    End Sub


    ' A list that draws its rows (the shortlist, #133) is judged item by
    ' item where each is drawn, as JournalShortlistRowsTests judges it:
    ' each line's and each button's text at 4.5:1 on what it is drawn on in
    ' Light and Dark, and drawn on one of the user's colors under a contrast
    ' theme (#138). Returns how many items were judged.
    Private Shared Function JudgeDrawn(list As Control, part As String, contrastTheme As Boolean, failures As List(Of String)) As Integer

        If Not list.Visible OrElse list.Width <= 0 OrElse list.Height <= 0 Then Return 0

        Dim judged As Integer = 0
        Dim accessible As AccessibleObject = list.AccessibilityObject

        Using picture As New Bitmap(list.Width, list.Height)
            list.DrawToBitmap(picture, New Rectangle(Point.Empty, list.Size))

            For index As Integer = 0 To accessible.GetChildCount() - 1
                Dim item As AccessibleObject = accessible.GetChild(index)
                If Not (item.Role = AccessibleRole.StaticText OrElse item.Role = AccessibleRole.PushButton) OrElse String.IsNullOrWhiteSpace(item.Name) Then Continue For

                Dim bounds As Rectangle = list.RectangleToClient(item.Bounds)
                If item.Role = AccessibleRole.PushButton Then bounds.Inflate(-list.LogicalToDeviceUnits(6), -list.LogicalToDeviceUnits(6))
                bounds.Intersect(New Rectangle(Point.Empty, picture.Size))
                Dim name As String = part & ": drawn " & item.Role.ToString() & " '" & item.Name.Replace(Environment.NewLine, " ") & "' "
                If bounds.Width <= 0 OrElse bounds.Height <= 0 Then
                    failures.Add(name & "is not drawn inside the list")
                    Continue For
                End If

                Dim seen As (Ratio As Double, Surface As Color, Ink As Color) = JournalShortlistRowsTests.Contrast(picture, bounds)

                If contrastTheme Then
                    If Not JournalShortlistRowsTests.IsUserColor(seen.Surface) Then
                        failures.Add(name & "drawn on " & seen.Surface.ToString() & ", not one of the user's colors")
                    End If
                ElseIf seen.Ratio < 4.5 Then
                    failures.Add(name & seen.Ratio.ToString("F2", CultureInfo.InvariantCulture) & ":1 on " & seen.Surface.ToString())
                End If
                judged += 1
            Next
        End Using

        Return judged

    End Function


    ' How far the strongest pixels of a control's text stand out from the
    ' color of most of its picture, inside a button's or chip's frame:
    ' smoothing leaves the rest of a stroke paler.
    Private Shared Function TextContrast(control As Control) As (Ratio As Double, Surface As Color)

        Using image As New Bitmap(control.Width, control.Height)
            control.DrawToBitmap(image, New Rectangle(Point.Empty, control.Size))

            Dim area As New Rectangle(Point.Empty, control.Size)
            Dim frame As Integer = control.LogicalToDeviceUnits(If(TypeOf control Is Button, 6, If(TypeOf control Is TagChipButton, 3, 0)))
            If area.Width > 2 * frame AndAlso area.Height > 2 * frame Then area.Inflate(-frame, -frame)

            Dim seen As (Ratio As Double, Surface As Color, Ink As Color) = JournalShortlistRowsTests.Contrast(image, area)
            Return (seen.Ratio, seen.Surface)
        End Using

    End Function


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
        ' The root's own tab index is its place among what its parent holds,
        ' not part of what it shows: the page area keeps the pages prepared
        ' while the window idles (#133) in the order the idle steps added
        ' them, and only the page shown takes the keyboard. Every control
        ' under the root is held to its tab index.
        Describe(root, root.GetType().Name, lines, describeTabIndex:=False)
        Return lines
    End Function


    Private Shared Sub Describe(control As Control, path As String, lines As List(Of String), Optional describeTabIndex As Boolean = True)

        Dim shown As Boolean = control.Parent Is Nothing OrElse control.Parent.Visible

        Dim parts As New List(Of String) From {
            path,
            "text=" & control.Text,
            "enabled=" & control.Enabled.ToString(),
            "colors=" & control.BackColor.ToArgb().ToString("X8", CultureInfo.InvariantCulture) & "/" & control.ForeColor.ToArgb().ToString("X8", CultureInfo.InvariantCulture),
            "name=" & If(control.AccessibleName, String.Empty),
            "tab=" & control.TabStop.ToString() & If(describeTabIndex, "," & control.TabIndex.ToString(CultureInfo.InvariantCulture), String.Empty)
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

        ' A list that draws its rows (the shortlist, #133) is described by
        ' what it draws, as a screen reader reads it: each item's role,
        ' name and action, and, while it shows, where it is drawn.
        Dim drawn As JournalShortlistList = TryCast(control, JournalShortlistList)
        If drawn IsNot Nothing Then
            Dim accessible As AccessibleObject = drawn.AccessibilityObject
            For index As Integer = 0 To accessible.GetChildCount() - 1
                Dim item As AccessibleObject = accessible.GetChild(index)
                Dim line As String = path & "/drawn " & index.ToString(CultureInfo.InvariantCulture) & ": " & item.Role.ToString() & " '" & item.Name & "'" &
                                     If(String.IsNullOrEmpty(item.DefaultAction), String.Empty, " action '" & item.DefaultAction & "'")
                If control.Visible AndAlso drawn.IsHandleCreated Then line &= "; bounds=" & drawn.RectangleToClient(item.Bounds).ToString()
                lines.Add(line)
            Next
        End If

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
