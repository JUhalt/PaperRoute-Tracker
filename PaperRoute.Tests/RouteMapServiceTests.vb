Imports System
Imports System.Collections.Generic
Imports System.Linq
Imports System.Text.Json
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

' The route map (#82): a route drawn to scale from recorded dates only.
<TestClass>
Public Class RouteMapServiceTests

    Private Shared ReadOnly Today As New DateTime(2026, 9, 28)

    <TestMethod>
    Public Sub APublishedRouteSplitsIntoJournalAuthorAndProductionTime()
        Dim manuscript As Manuscript = Anchoring()
        Dim before As String = JsonSerializer.Serialize(manuscript)

        Dim map As RouteMap = RouteMapService.Build(manuscript, Today)

        CollectionAssert.AreEqual(
            {"Journal 7", "Author 48", "Journal 32", "Author 69", "Journal 42", "Author 14", "Journal 24", "Production 28"},
            map.Segments.Select(Function(item) item.Kind.ToString() & " " & item.Days).ToList())
        Assert.AreEqual(264, map.TotalDays)
        Assert.AreEqual(105, map.DaysOf(RouteSegmentKind.Journal))
        Assert.AreEqual(131, map.DaysOf(RouteSegmentKind.Author))
        Assert.AreEqual(28, map.DaysOf(RouteSegmentKind.Production))
        Assert.IsFalse(map.Ongoing)
        CollectionAssert.AreEqual({"Psychological Letters", "Open Psychology"}, map.Journals)
        CollectionAssert.AreEqual(
            {"Submitted", "DeskRejected", "Submitted", "MajorRevision", "Resubmitted", "MinorRevision", "Resubmitted", "Accepted", "Published"},
            map.Markers.Select(Function(item) If(item.Kind = RouteMarkerKind.Decision, item.Decision.ToString(), item.Kind.ToString())).ToList())
        Assert.AreEqual(before, JsonSerializer.Serialize(manuscript), "Drawing a route never changes the record.")
    End Sub

    <TestMethod>
    Public Sub AMissingResubmissionIsShownAsNotRecordedNeverEstimated()
        Dim manuscript As Manuscript = Anchoring()
        manuscript.History.RemoveAll(Function(item) item.EventDate.Month = 6)

        Dim map As RouteMap = RouteMapService.Build(manuscript, Today)

        Dim unknown As RouteSegment = map.Segments.Single(Function(item) item.Kind = RouteSegmentKind.NotRecorded)
        Assert.AreEqual(New DateTime(2026, 4, 2), unknown.Start)
        Assert.AreEqual(New DateTime(2026, 7, 22), unknown.Finish, "From the revision request to the next decision.")
        Assert.AreEqual(264, map.TotalDays, "The route still spans the same dates.")
    End Sub

    <TestMethod>
    Public Sub OpenStretchesRunToTodayAsOngoing()
        Dim reviewing As New Manuscript With {.Title = "Reviewing", .CurrentStage = PaperStage.UnderReview}
        reviewing.Submissions.Add(Submitted("Assessment", Today.AddDays(-40)))
        Dim reviewMap As RouteMap = RouteMapService.Build(reviewing, Today)
        Assert.AreEqual(RouteSegmentKind.Journal, reviewMap.Segments.Single().Kind)
        Assert.AreEqual(40, reviewMap.TotalDays)
        Assert.IsTrue(reviewMap.Ongoing)

        Dim revising As New Manuscript With {.Title = "Revising", .CurrentStage = PaperStage.Revision}
        revising.Submissions.Add(Submitted("Assessment", Today.AddDays(-90), (EditorialDecision.MajorRevision, Today.AddDays(-30))))
        Dim last As RouteSegment = RouteMapService.Build(revising, Today).Segments.Last()
        Assert.AreEqual(RouteSegmentKind.Author, last.Kind, "The current stage says it is the author's turn.")
        Assert.IsTrue(last.Ongoing)

        Dim accepted As New Manuscript With {.Title = "Accepted", .CurrentStage = PaperStage.Accepted}
        accepted.Submissions.Add(Submitted("Assessment", Today.AddDays(-90), (EditorialDecision.Accepted, Today.AddDays(-10))))
        Dim production As RouteSegment = RouteMapService.Build(accepted, Today).Segments.Last()
        Assert.AreEqual(RouteSegmentKind.Production, production.Kind)
        Assert.AreEqual(10, production.Days)
        Assert.IsTrue(production.Ongoing)

        Dim rejected As New Manuscript With {.Title = "Rerouting", .CurrentStage = PaperStage.Draft}
        rejected.Submissions.Add(Submitted("Assessment", Today.AddDays(-60), (EditorialDecision.RejectedAfterReview, Today.AddDays(-20))))
        Dim rerouting As RouteSegment = RouteMapService.Build(rejected, Today).Segments.Last()
        Assert.AreEqual(RouteSegmentKind.Author, rerouting.Kind, "After a rejection, preparing the next submission is the author's time.")
        Assert.IsTrue(rerouting.Ongoing)
    End Sub

    <TestMethod>
    Public Sub AFiledRouteEndsAtItsLastDecision()
        Dim filed As New Manuscript With {.Title = "Filed", .CurrentStage = PaperStage.Draft, .Location = ManuscriptLocation.FileDrawer}
        filed.Submissions.Add(Submitted("Assessment", Today.AddDays(-200), (EditorialDecision.Rejected, Today.AddDays(-150))))

        Dim map As RouteMap = RouteMapService.Build(filed, Today)

        Assert.AreEqual(1, map.Segments.Count)
        Assert.AreEqual(50, map.TotalDays)
        Assert.IsFalse(map.Ongoing, "A filed manuscript's route is not ongoing.")
    End Sub

    <TestMethod>
    Public Sub InconsistentAndFutureDatesAreLeftOut()
        Dim manuscript As New Manuscript With {.Title = "Slips", .CurrentStage = PaperStage.Draft, .Location = ManuscriptLocation.FileDrawer}
        manuscript.Submissions.Add(Submitted("Assessment", Today.AddDays(-50), (EditorialDecision.DeskRejected, Today.AddDays(-60)), (EditorialDecision.Rejected, Today.AddDays(-20))))
        manuscript.Submissions.Add(Submitted("Future Journal", Today.AddDays(10)))

        Dim map As RouteMap = RouteMapService.Build(manuscript, Today)

        Assert.AreEqual(30, map.TotalDays, "A decision dated before its submission is skipped; the later one closes the route.")
        Assert.IsFalse(map.Journals.Contains("Future Journal"))
        Assert.IsTrue(RouteMapService.Build(New Manuscript With {.Title = "Idea"}, Today).IsEmpty)
    End Sub

    <TestMethod>
    Public Sub EachNumberedStepSaysWhatItTook()
        Dim steps As List(Of RouteStep) = RouteMapService.Steps(RouteMapService.Build(Anchoring(), Today))

        CollectionAssert.AreEqual(
            {"Desk rejected", "Submitted to Open Psychology", "Major revision", "Resubmitted", "Minor revision", "Resubmitted", "Accepted", "Published"},
            steps.Select(Function(item) item.Title).ToList())
        CollectionAssert.AreEqual(
            {"7 days after submission", "after 48 days of rerouting", "32 days after submission", "after 69 days of revising",
             "42 days after resubmission", "after 14 days of revising", "24 days after resubmission", "28 days after acceptance"},
            steps.Select(Function(item) item.Note).ToList())
        CollectionAssert.AreEqual(Enumerable.Range(1, 8).ToList(), steps.Select(Function(item) item.Number).ToList(), "The first submission is day 0, not a step.")
        Assert.AreEqual("2 journals  ·  2 revisions  ·  1 rejection", RouteMapService.Brief(RouteMapService.Build(Anchoring(), Today)))
    End Sub

    <TestMethod>
    Public Sub TheLibraryLinesUpPublishedRoutesUnlessAskedForAll()
        Dim longer As Manuscript = Anchoring()
        Dim shorter As New Manuscript With {.Title = "Quick note", .CurrentStage = PaperStage.Published, .Location = ManuscriptLocation.Published}
        shorter.Submissions.Add(Submitted("Open Psychology", New DateTime(2026, 5, 1), (EditorialDecision.Accepted, New DateTime(2026, 6, 10))))
        shorter.Metadata.PublishedDate = New DateTime(2026, 7, 1)
        Dim reviewing As New Manuscript With {.Title = "Still in review", .CurrentStage = PaperStage.UnderReview}
        reviewing.Submissions.Add(Submitted("Assessment", Today.AddDays(-30)))
        Dim idea As New Manuscript With {.Title = "Idea"}

        Dim published As RouteMapLibrary = RouteMapService.Library({longer, reviewing, shorter, idea}, Today, False)
        CollectionAssert.AreEqual({"Quick note", "Anchoring effects in clinical risk estimates"}, published.Routes.Select(Function(item) item.Manuscript.Title).ToList(), "Shortest first.")
        Assert.AreEqual(162.5, published.MedianDaysToPublication.Value.Value, "The median of 61 and 264 days.")
        Assert.AreEqual((105 + 40) / 325.0, published.Share(RouteSegmentKind.Journal), 0.0001)
        Assert.AreEqual(1.0, [Enum].GetValues(GetType(RouteSegmentKind)).Cast(Of RouteSegmentKind)().Sum(Function(kind) published.Share(kind)), 0.0001)

        Dim all As RouteMapLibrary = RouteMapService.Library({longer, reviewing, shorter, idea}, Today, True)
        CollectionAssert.AreEqual({"Still in review", "Quick note", "Anchoring effects in clinical risk estimates"}, all.Routes.Select(Function(item) item.Manuscript.Title).ToList(),
                                  "Unfinished work runs to today; a manuscript never submitted has no route.")
        Assert.IsFalse(all.Routes(0).IsPublished)
        Assert.AreEqual(2, all.MedianDaysToPublication.SampleSize, "Only published routes count toward time to publication.")
    End Sub

    ' The route from the design mockup: a desk rejection, rerouting, two
    ' revision rounds with recorded returns to review, acceptance, and
    ' publication, 264 days in all.
    Friend Shared Function Anchoring() As Manuscript
        Dim manuscript As New Manuscript With {
            .Title = "Anchoring effects in clinical risk estimates",
            .CurrentStage = PaperStage.Published,
            .Location = ManuscriptLocation.Published
        }
        manuscript.Submissions.Add(Submitted("Psychological Letters", New DateTime(2026, 1, 5), (EditorialDecision.DeskRejected, New DateTime(2026, 1, 12))))
        manuscript.Submissions.Add(Submitted("Open Psychology", New DateTime(2026, 3, 1),
            (EditorialDecision.MajorRevision, New DateTime(2026, 4, 2)),
            (EditorialDecision.MinorRevision, New DateTime(2026, 7, 22)),
            (EditorialDecision.Accepted, New DateTime(2026, 8, 29))))
        manuscript.History.Add(New HistoryEvent With {.Stage = PaperStage.UnderReview, .EventDate = New DateTime(2026, 6, 10)})
        manuscript.History.Add(New HistoryEvent With {.Stage = PaperStage.UnderReview, .EventDate = New DateTime(2026, 8, 5)})
        manuscript.Metadata.PublishedDate = New DateTime(2026, 9, 26)
        Return manuscript
    End Function

    Private Shared Function Submitted(journal As String, submittedOn As DateTime, ParamArray decisions As (Decision As EditorialDecision, DecisionDate As DateTime)()) As JournalSubmission
        Dim submission As New JournalSubmission With {.JournalName = journal, .SubmittedDate = submittedOn}
        For Each decision In decisions
            submission.Decisions.Add(New EditorialDecisionEvent With {.Decision = decision.Decision, .DecisionDate = decision.DecisionDate})
        Next
        Return submission
    End Function

End Class
