Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.Linq
Imports System.Reflection
Imports System.Text.Json
Imports System.Windows.Forms
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline
Imports ManuscriptPipeline.Controls
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

' The ways out of the File Drawer from a Board card (#202) and the card's
' suggestion to file (#203): what each writes to the manuscript and its
' history, and what the card then says, on the real main window over
' in-memory records, with saves counted and not written.
<TestClass>
<DoNotParallelize>
Public Class FileDrawerTests

    Private Shared ReadOnly FiledOn As New DateTime(2026, 8, 17)

    Private Const Reason As String = "Three rejections; revisit with a larger sample."

    Private Const ConsiderFiling As String = "consider filing"

    ' The card reads "3 rejections; consider filing" while the latest
    ' submission ended in a rejection, at the default threshold of three,
    ' and not once a newer submission is open (#203).
    <TestMethod>
    Public Sub TheCardSuggestsFilingOnlyWhileTheLatestRouteIsClosed()
        StaRunner.Run(
            Sub()
                Using board As New FileDrawerBoard()
                    Dim rejected As Manuscript = RejectedThreeTimes()
                    board.Prepare(rejected)
                    Assert.AreEqual("3 rejections; consider filing", StatusLine(board.Card(rejected.Title)), "Three rejections, the latest submission closed.")

                    rejected.Submissions.Add(New JournalSubmission With {.JournalName = "A fourth journal", .SubmittedDate = DateTime.Today.AddDays(-10)})
                    rejected.CurrentStage = PaperStage.Submitted
                    rejected.StageEnteredDate = DateTime.Today.AddDays(-10)
                    board.Render()
                    Assert.IsFalse(StatusLine(board.Card(rejected.Title)).Contains(ConsiderFiling), "A newer submission is open: " & StatusLine(board.Card(rejected.Title)))

                    board.Close()
                End Using
            End Sub)
    End Sub

    ' Restored from the File Drawer after its last rejection, a manuscript
    ' is not asked to consider filing again (#203): the researcher just
    ' decided otherwise.
    <TestMethod>
    Public Sub ARevivedManuscriptIsNotAskedToConsiderFilingAgain()
        StaRunner.Run(
            Sub()
                Using board As New FileDrawerBoard()
                    Dim revived As Manuscript = RejectedThreeTimes()
                    revived.Location = ManuscriptLocation.FileDrawer
                    revived.FileDrawerDate = FiledOn
                    revived.FileDrawerReason = Reason
                    board.Prepare(revived)

                    MenuItem(board.Card(revived.Title), "Restore to Pipeline").PerformClick()
                    Application.DoEvents()

                    Assert.AreEqual(ManuscriptLocation.Pipeline, revived.Location)
                    Assert.IsFalse(StatusLine(board.Card(revived.Title)).Contains(ConsiderFiling), "Just revived: " & StatusLine(board.Card(revived.Title)))

                    board.Close()
                End Using
            End Sub)
    End Sub

    ' A manuscript filed by a spreadsheet import, or in the example library,
    ' has a filed date and reason but no "Moved to File Drawer" note (#202):
    ' the restore note is the only place the reason survives, and Route View
    ' shows it. Nothing else in the manuscript changes.
    <TestMethod>
    Public Sub RestoreKeepsWhenAndWhyAManuscriptFiledByImportWasFiled()
        StaRunner.Run(
            Sub()
                Using board As New FileDrawerBoard()
                    Dim filed As Manuscript = FiledByImport(Reason)
                    Dim before As String = Snapshot(filed)
                    board.Prepare(filed)

                    MenuItem(board.Card(filed.Title), "Restore to Pipeline").PerformClick()
                    Application.DoEvents()

                    Assert.AreEqual(ManuscriptLocation.Pipeline, filed.Location)
                    Assert.IsFalse(filed.FileDrawerDate.HasValue, "Back on the Pipeline, the filed date is cleared.")
                    Assert.AreEqual(String.Empty, filed.FileDrawerReason, "Back on the Pipeline, the reason is cleared.")
                    Assert.AreEqual(1, board.SaveCount)

                    Dim restored As HistoryEvent = filed.History.Last()
                    Assert.AreEqual("Restored from File Drawer to active Pipeline (filed Aug 17, 2026: Three rejections; revisit with a larger sample.)", restored.Note)
                    Assert.AreEqual(PaperStage.Draft, restored.Stage)
                    Assert.IsTrue(restored.RecordedAtUtc.HasValue)

                    Dim route As ManuscriptRoute = ManuscriptRouteProjectionService.Project(filed)
                    Assert.IsTrue(route.Waypoints.Any(Function(point) point.Kind = ManuscriptRouteWaypointKind.Stage AndAlso point.Note = restored.Note),
                                  "Route View shows the restore note.")

                    ' The restore undone, the record reads as it did.
                    filed.History.Remove(restored)
                    filed.Location = ManuscriptLocation.FileDrawer
                    filed.FileDrawerDate = FiledOn
                    filed.FileDrawerReason = Reason
                    Assert.AreEqual(before, Snapshot(filed), "Nothing else in the manuscript changes.")

                    board.Close()
                End Using
            End Sub)
    End Sub

    <TestMethod>
    Public Sub RestoreKeepsTheDateOfAManuscriptFiledWithoutAReason()
        StaRunner.Run(
            Sub()
                Using board As New FileDrawerBoard()
                    Dim filed As Manuscript = FiledByImport(String.Empty)
                    board.Prepare(filed)

                    MenuItem(board.Card(filed.Title), "Restore to Pipeline").PerformClick()
                    Application.DoEvents()

                    Assert.AreEqual(ManuscriptLocation.Pipeline, filed.Location)
                    Assert.AreEqual("Restored from File Drawer to active Pipeline (filed Aug 17, 2026).", filed.History.Last().Note)
                    Assert.AreEqual(1, board.SaveCount)

                    board.Close()
                End Using
            End Sub)
    End Sub

    ' Filed as an import writes it: the stage it was in, with no note of
    ' the move.
    Private Shared Function FiledByImport(reason As String) As Manuscript
        Dim manuscript As New Manuscript With {
            .Title = "Filed by import",
            .Location = ManuscriptLocation.FileDrawer,
            .CurrentStage = PaperStage.Draft,
            .StageEnteredDate = New DateTime(2026, 3, 2),
            .TargetJournal = "A fictional journal",
            .FileDrawerDate = FiledOn,
            .FileDrawerReason = reason
        }
        manuscript.History.Add(New HistoryEvent With {.Stage = PaperStage.Draft, .EventDate = manuscript.StageEnteredDate, .Note = "Imported from a spreadsheet."})
        Return manuscript
    End Function

    ' On the Pipeline in Draft after three rejections, the last of them long
    ' enough ago that the card's "Rejected N days ago" line has gone, with a
    ' target journal so the card has no other line to show.
    Private Shared Function RejectedThreeTimes() As Manuscript
        Dim manuscript As New Manuscript With {
            .Title = "Rejected three times",
            .Location = ManuscriptLocation.Pipeline,
            .CurrentStage = PaperStage.Draft,
            .StageEnteredDate = DateTime.Today.AddDays(-150),
            .TargetJournal = "A fictional journal"
        }
        For round As Integer = 3 To 1 Step -1
            Dim submitted As DateTime = DateTime.Today.AddDays(-150 * round - 60)
            Dim submission As New JournalSubmission With {.JournalName = "Journal " & round.ToString(), .SubmittedDate = submitted}
            submission.Decisions.Add(New EditorialDecisionEvent With {.Decision = EditorialDecision.Rejected, .DecisionDate = submitted.AddDays(60)})
            manuscript.Submissions.Add(submission)
        Next
        manuscript.History.Add(New HistoryEvent With {.Stage = PaperStage.Draft, .EventDate = manuscript.StageEnteredDate, .Note = "Back to Draft after the third rejection."})
        Return manuscript
    End Function

    ' The card's status line: what needs attention, otherwise how long the
    ' manuscript has been in its stage.
    Private Shared Function StatusLine(card As Control) As String
        Return If(card.Controls.OfType(Of Label)().
            Where(Function(label) Not TypeOf label Is LinkLabel AndAlso Not TypeOf label Is PillLabel).
            Select(Function(label) label.Text).
            FirstOrDefault(Function(text) text.Contains(ConsiderFiling) OrElse text.Contains("day")), String.Empty)
    End Function

    Private Shared Function Snapshot(manuscript As Manuscript) As String
        Return JsonSerializer.Serialize(manuscript, CreateJsonOptions())
    End Function

    Private Shared Function MenuItem(card As Control, text As String) As ToolStripItem
        Return card.ContextMenuStrip.Items.Cast(Of ToolStripItem)().Single(Function(item) item.Text = text)
    End Function

    ' The real main window on in-memory records, as ViewCatalog builds it:
    ' Load is skipped and the window never activates. A save is counted, not
    ' written; the record's change is what these tests read.
    Private NotInheritable Class FileDrawerBoard
        Inherits Form1

        Public SaveCount As Integer

        Protected Overrides Sub OnLoad(e As EventArgs)
        End Sub

        Protected Overrides ReadOnly Property ShowWithoutActivation As Boolean
            Get
                Return True
            End Get
        End Property

        Protected Overrides Function SaveManuscripts() As Boolean
            SaveCount += 1
            Return True
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

        Public Sub Render()
            CallPrivate("RenderManuscripts")
            Application.DoEvents()
        End Sub

        ' The card with this title, on whichever shelf it is.
        Public Function Card(title As String) As RoundedPanel
            Return {"pipelinePanel", "publishedPanel", "fileDrawerPanel"}.
                SelectMany(Function(name) DirectCast(GetType(Form1).GetField(name, BindingFlags.Instance Or BindingFlags.NonPublic).GetValue(Me), Control).Controls.OfType(Of RoundedPanel)()).
                Single(Function(panel) panel.AccessibleName = title)
        End Function

        Private Sub CallPrivate(name As String, ParamArray args As Object())
            GetType(Form1).GetMethod(name, BindingFlags.Instance Or BindingFlags.NonPublic).Invoke(Me, args)
        End Sub
    End Class

End Class
