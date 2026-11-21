Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services
Imports Microsoft.VisualStudio.TestTools.UnitTesting

<TestClass>
Public Class RouteSummaryServiceTests

    Private Shared ReadOnly Separator As String = " " & ChrW(&HB7) & " "

    <TestMethod>
    Public Sub UnsubmittedManuscriptsSayWhereTheyAre()
        Dim draft = RouteSummaryService.Describe(New Manuscript With {.Location = ManuscriptLocation.Pipeline})
        Assert.AreEqual(0, draft.Dots.Count)
        Assert.AreEqual("Not yet submitted", draft.Text)

        Dim published = RouteSummaryService.Describe(New Manuscript With {.Location = ManuscriptLocation.Published})
        Assert.AreEqual("No submissions recorded", published.Text, "Published work imported without submissions is not 'not yet submitted'.")
    End Sub

    <TestMethod>
    Public Sub FirstSubmissionAwaitingDecision()
        Dim summary = RouteSummaryService.Describe(WithSubmissions({Nothing}))
        CollectionAssert.AreEqual({RouteDotState.Current}, summary.Dots.ToList())
        Assert.AreEqual("1st journal", summary.Text)
    End Sub

    <TestMethod>
    Public Sub CurrentDecisionFollowsEarlierRejections()
        Dim summary = RouteSummaryService.Describe(WithSubmissions({EditorialDecision.DeskRejected, EditorialDecision.MajorRevision}))
        CollectionAssert.AreEqual({RouteDotState.Closed, RouteDotState.Current}, summary.Dots.ToList())
        Assert.AreEqual("2nd journal" & Separator & "major revision", summary.Text)
    End Sub

    <TestMethod>
    Public Sub PendingSubmissionCountsEarlierRejections()
        Dim summary = RouteSummaryService.Describe(WithSubmissions({EditorialDecision.RejectedAfterReview, Nothing}))
        CollectionAssert.AreEqual({RouteDotState.Closed, RouteDotState.Current}, summary.Dots.ToList())
        Assert.AreEqual("2nd journal" & Separator & "1 rejection", summary.Text)
    End Sub

    <TestMethod>
    Public Sub ClosedRouteCountsJournalsAndRejections()
        Dim summary = RouteSummaryService.Describe(WithSubmissions({EditorialDecision.Rejected, EditorialDecision.DeskRejected, EditorialDecision.RejectedAfterReview}))
        CollectionAssert.AreEqual({RouteDotState.Closed, RouteDotState.Closed, RouteDotState.Closed}, summary.Dots.ToList())
        Assert.AreEqual("3 journals" & Separator & "3 rejections", summary.Text)

        Dim withdrawn = RouteSummaryService.Describe(WithSubmissions({EditorialDecision.Withdrawn}))
        CollectionAssert.AreEqual({RouteDotState.Closed}, withdrawn.Dots.ToList())
        Assert.AreEqual("1 journal" & Separator & "withdrawn", withdrawn.Text)
    End Sub

    <TestMethod>
    Public Sub AcceptedSubmissionIsMarked()
        Dim summary = RouteSummaryService.Describe(WithSubmissions({EditorialDecision.Accepted}))
        CollectionAssert.AreEqual({RouteDotState.Accepted}, summary.Dots.ToList())
        Assert.AreEqual("1st journal" & Separator & "accepted", summary.Text)
    End Sub

    <TestMethod>
    Public Sub SubmissionsAreOrderedByDateAndDotsAreCapped()
        Dim decisions As New List(Of EditorialDecision?)
        For index As Integer = 1 To 10
            decisions.Add(EditorialDecision.Rejected)
        Next
        decisions.Add(Nothing)
        Dim manuscript As Manuscript = WithSubmissions(decisions.ToArray())
        ' Stored order must not matter.
        manuscript.Submissions.Reverse()

        Dim summary = RouteSummaryService.Describe(manuscript)
        Assert.AreEqual(RouteSummaryService.MaximumDots, summary.Dots.Count)
        Assert.AreEqual(RouteDotState.Current, summary.Dots.Last(), "The newest submission is the last dot.")
        Assert.AreEqual("11th journal" & Separator & "10 rejections", summary.Text)
    End Sub

    <TestMethod>
    Public Sub LatestDecisionOnASubmissionWins()
        Dim manuscript As Manuscript = WithSubmissions({EditorialDecision.MajorRevision})
        manuscript.Submissions(0).Decisions.Add(New EditorialDecisionEvent With {
            .Decision = EditorialDecision.Accepted,
            .DecisionDate = manuscript.Submissions(0).Decisions(0).DecisionDate.AddDays(40)
        })
        Assert.AreEqual("1st journal" & Separator & "accepted", RouteSummaryService.Describe(manuscript).Text)
    End Sub

    ' One submission per entry, a month apart; Nothing means no decision yet.
    Private Shared Function WithSubmissions(decisions As EditorialDecision?()) As Manuscript
        Dim manuscript As New Manuscript With {.Location = ManuscriptLocation.Pipeline}
        Dim submitted As New DateTime(2025, 1, 10)
        For Each decision In decisions
            Dim submission As New JournalSubmission With {.JournalName = "Journal " & (manuscript.Submissions.Count + 1), .SubmittedDate = submitted}
            If decision.HasValue Then
                submission.Decisions.Add(New EditorialDecisionEvent With {.Decision = decision.Value, .DecisionDate = submitted.AddDays(20)})
            End If
            manuscript.Submissions.Add(submission)
            submitted = submitted.AddMonths(1)
        Next
        Return manuscript
    End Function

End Class
