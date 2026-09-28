Imports System
Imports System.Collections.Generic
Imports System.Linq
Imports System.Text.Json
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

' Route statistics (#30) and your history with each journal (#62), from
' synthetic histories. Only recorded dates count; nothing is estimated.
<TestClass>
Public Class RouteAnalyticsTests

    Private Shared ReadOnly Today As New DateTime(2026, 9, 28)

    <TestMethod>
    Public Sub ASubmissionIsMeasuredFromItsRecordedDecisions()
        Dim submission As JournalSubmission = Submitted("Collabra", Today.AddDays(-200),
            (EditorialDecision.MajorRevision, Today.AddDays(-150)),
            (EditorialDecision.MinorRevision, Today.AddDays(-60)),
            (EditorialDecision.Accepted, Today.AddDays(-30)))
        submission.Decisions.Add(New EditorialDecisionEvent With {.Decision = EditorialDecision.None, .DecisionDate = Today.AddDays(-190)})

        Dim statistics As SubmissionStatistics = RouteAnalyticsService.DescribeSubmission(submission, Today)

        Assert.AreEqual(EditorialDecision.MajorRevision, statistics.FirstDecision.Value, "A decision of None is not a decision.")
        Assert.AreEqual(50, statistics.DaysToFirstDecision.Value)
        Assert.AreEqual(2, statistics.RevisionRounds)
        Assert.AreEqual(SubmissionOutcome.Accepted, statistics.Outcome)
        Assert.AreEqual(Today.AddDays(-30), statistics.ClosedDate.Value)
        Assert.AreEqual(170, statistics.DaysAtJournal.Value)
        Assert.IsTrue(statistics.WentToReview.Value)
    End Sub

    <TestMethod>
    Public Sub OpenAndInconsistentSubmissionsLeaveValuesOut()
        Dim waiting As SubmissionStatistics = RouteAnalyticsService.DescribeSubmission(Submitted("Assessment", Today.AddDays(-40)), Today)
        Assert.AreEqual(SubmissionOutcome.Open, waiting.Outcome)
        Assert.IsFalse(waiting.DaysToFirstDecision.HasValue)
        Assert.IsFalse(waiting.WentToReview.HasValue, "Without a decision the record does not say.")
        Assert.AreEqual(40, waiting.DaysAtJournal.Value, "An open submission counts to today.")

        Dim slip As SubmissionStatistics = RouteAnalyticsService.DescribeSubmission(
            Submitted("Assessment", Today.AddDays(-10), (EditorialDecision.DeskRejected, Today.AddDays(-20))), Today)
        Assert.IsFalse(slip.DaysToFirstDecision.HasValue, "A decision dated before the submission is a slip, not a negative duration.")
        Assert.AreEqual(SubmissionOutcome.DeskRejected, slip.Outcome)
        Assert.IsFalse(slip.WentToReview.Value)

        Dim unspecified As SubmissionStatistics = RouteAnalyticsService.DescribeSubmission(
            Submitted("Assessment", Today.AddDays(-50), (EditorialDecision.Rejected, Today.AddDays(-20))), Today)
        Assert.IsFalse(unspecified.WentToReview.HasValue, "A plain rejection does not say whether the paper was reviewed.")
    End Sub

    <TestMethod>
    Public Sub AManuscriptRouteCountsJournalsAndTimeToAcceptanceAndPublication()
        Dim manuscript As New Manuscript With {.Title = "Anchoring replication", .CurrentStage = PaperStage.Published, .Location = ManuscriptLocation.Published}
        manuscript.Submissions.Add(Submitted("Collabra", Today.AddDays(-200), (EditorialDecision.Accepted, Today.AddDays(-60))))
        manuscript.Submissions.Add(Submitted("Psychological Letters", Today.AddDays(-300), (EditorialDecision.DeskRejected, Today.AddDays(-290))))
        manuscript.History.Add(New HistoryEvent With {.Stage = PaperStage.Published, .EventDate = Today.AddDays(-20)})

        Dim statistics As ManuscriptStatistics = RouteAnalyticsService.DescribeManuscript(manuscript, Today)

        CollectionAssert.AreEqual({"Psychological Letters", "Collabra"}, statistics.Submissions.Select(Function(item) item.JournalName).ToList(), "Oldest first.")
        Assert.AreEqual(2, statistics.JournalCount)
        Assert.AreEqual(Today.AddDays(-300), statistics.FirstSubmitted.Value)
        Assert.AreEqual(240, statistics.DaysToAcceptance.Value)
        Assert.AreEqual(280, statistics.DaysToPublication.Value, "Without a publication date field, the recorded move to Published counts.")

        manuscript.Metadata.PublishedDate = Today.AddDays(-45)
        Assert.AreEqual(255, RouteAnalyticsService.DescribeManuscript(manuscript, Today).DaysToPublication.Value, "The publication date field wins.")

        Dim unsubmitted As ManuscriptStatistics = RouteAnalyticsService.DescribeManuscript(New Manuscript With {.Title = "Idea"}, Today)
        Assert.IsFalse(unsubmitted.FirstSubmitted.HasValue)
        Assert.IsFalse(unsubmitted.DaysToAcceptance.HasValue)
    End Sub

    <TestMethod>
    Public Sub JournalHistoryGroupsLinkedAndExactNamesButNeverGuesses()
        Dim collabra As New JournalRecord With {.Name = "Collabra: Psychology"}
        Dim first As New Manuscript With {.Title = "First"}
        Dim linked As JournalSubmission = Submitted("Collabra (old name)", Today.AddDays(-100), (EditorialDecision.MajorRevision, Today.AddDays(-60)))
        linked.JournalId = collabra.Id
        first.Submissions.Add(linked)
        Dim second As New Manuscript With {.Title = "Second"}
        second.Submissions.Add(Submitted("collabra:  psychology", Today.AddDays(-80), (EditorialDecision.DeskRejected, Today.AddDays(-76))))
        second.Submissions.Add(Submitted("Collabra Psych", Today.AddDays(-50)))

        Dim library As LibraryStatistics = RouteAnalyticsService.ForLibrary({first, second}, Today, {collabra})

        Dim grouped As JournalHistory = library.Journals.Single(Function(item) item.JournalId.HasValue AndAlso item.JournalId.Value = collabra.Id)
        Assert.AreEqual("Collabra: Psychology", grouped.JournalName)
        Assert.AreEqual(2, grouped.Count, "A linked record and an exactly matching name count together.")
        Assert.AreEqual(1, grouped.CountOutcome(SubmissionOutcome.DeskRejected))
        Assert.AreEqual(1, grouped.WithRevisions)
        Assert.AreEqual(22.0, grouped.MedianDaysToFirstDecision.Value.Value, "The median of 4 and 40 days.")
        Assert.AreEqual(2, grouped.MedianDaysToFirstDecision.SampleSize)
        Assert.AreEqual(40.0, grouped.MedianReviewDays.Value.Value, "Desk rejections are not review time.")
        Assert.AreEqual(Today.AddDays(-80), grouped.LastSubmitted)

        Dim separate As JournalHistory = library.Journals.Single(Function(item) item.JournalName = "Collabra Psych")
        Assert.IsFalse(separate.JournalId.HasValue, "A similar name is shown on its own, not merged by guess.")
        Assert.AreEqual(SubmissionOutcome.Open, separate.Submissions.Single().Statistics.Outcome)
    End Sub

    <TestMethod>
    Public Sub LibraryMediansUseOnlyRecordsThatHaveTheValue()
        Dim library As New List(Of Manuscript)
        For Each days In {10, 30, 50, 120}
            Dim manuscript As New Manuscript With {.Title = "Paper " & days}
            manuscript.Submissions.Add(Submitted("Journal", Today.AddDays(-200), (If(days = 10, EditorialDecision.DeskRejected, EditorialDecision.Accepted), Today.AddDays(-200 + days))))
            library.Add(manuscript)
        Next
        library.Add(New Manuscript With {.Title = "Not submitted"})
        Dim before As String = JsonSerializer.Serialize(library)

        Dim statistics As LibraryStatistics = RouteAnalyticsService.ForLibrary(library, Today)

        Assert.AreEqual(40.0, statistics.MedianDaysToFirstDecision.Value.Value)
        Assert.AreEqual(4, statistics.MedianDaysToFirstDecision.SampleSize)
        Assert.AreEqual(10.0, statistics.MedianDaysToDeskRejection.Value.Value)
        Assert.AreEqual(50.0, statistics.MedianReviewDays.Value.Value)
        Assert.AreEqual(50.0, statistics.MedianDaysToAcceptance.Value.Value)
        Assert.AreEqual(3, statistics.MedianDaysToAcceptance.SampleSize)
        Assert.IsFalse(statistics.MedianDaysToPublication.HasValue, "No publication dates, no median.")
        Assert.AreEqual(3, statistics.CountOutcome(SubmissionOutcome.Accepted))
        Assert.AreEqual(before, JsonSerializer.Serialize(library), "Statistics never change records.")
        Assert.AreEqual(JsonSerializer.Serialize(statistics.Journals.Select(Function(item) item.Count)),
                        JsonSerializer.Serialize(RouteAnalyticsService.ForLibrary(library, Today).Journals.Select(Function(item) item.Count)), "Deterministic.")
    End Sub

    Private Shared Function Submitted(journal As String, submittedOn As DateTime, ParamArray decisions As (Decision As EditorialDecision, DecisionDate As DateTime)()) As JournalSubmission
        Dim submission As New JournalSubmission With {.JournalName = journal, .SubmittedDate = submittedOn}
        For Each decision In decisions
            submission.Decisions.Add(New EditorialDecisionEvent With {.Decision = decision.Decision, .DecisionDate = decision.DecisionDate})
        Next
        Return submission
    End Function

End Class
