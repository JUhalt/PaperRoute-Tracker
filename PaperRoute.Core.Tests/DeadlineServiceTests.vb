Imports System
Imports System.Collections.Generic
Imports System.Linq
Imports System.Text.Json
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

' The Deadlines page's items (#28): dated items from the one reminder
' engine, undated obligations, and recently completed reminders.
<TestClass>
Public Class DeadlineServiceTests

    Private Shared ReadOnly Today As New DateTime(2026, 9, 27)

    <TestMethod>
    <DataRow(-3, DeadlineGroup.Overdue)>
    <DataRow(0, DeadlineGroup.Today)>
    <DataRow(1, DeadlineGroup.Next7Days)>
    <DataRow(7, DeadlineGroup.Next7Days)>
    <DataRow(8, DeadlineGroup.Later)>
    Public Sub GroupsFollowTheDateCountedFromToday(days As Integer, expected As DeadlineGroup)
        Assert.AreEqual(expected, DeadlineService.GroupFor(Today.AddDays(days), Today))
        Assert.AreEqual(expected, DeadlineService.GroupFor(Today.AddDays(days).AddHours(23), Today.AddHours(1)), "Times of day do not matter.")
    End Sub

    <TestMethod>
    Public Sub RevisionShowsProgressFromTheCommentsOnItsDecision()
        Dim manuscript As Manuscript = Revision("Anchoring replication", Today.AddDays(5))
        Dim submission As JournalSubmission = manuscript.Submissions.Single()
        Dim decision As EditorialDecisionEvent = submission.Decisions.Single()
        For Each status In {ReviewerResponseStatus.Addressed, ReviewerResponseStatus.Addressed, ReviewerResponseStatus.NotApplicable,
                            ReviewerResponseStatus.InProgress, ReviewerResponseStatus.Unresolved}
            submission.ReviewerResponses.Add(New ReviewerResponseItem With {.DecisionId = decision.Id, .RevisionRoundNumber = 1, .Status = status})
        Next
        ' A comment on an earlier decision is not part of this revision.
        submission.ReviewerResponses.Add(New ReviewerResponseItem With {.DecisionId = Guid.NewGuid(), .Status = ReviewerResponseStatus.Unresolved})

        Dim item As DeadlineItem = DeadlineService.Build({manuscript}, Today).Single()

        Assert.AreEqual(DeadlineKind.Revision, item.Kind)
        Assert.AreEqual(DeadlineGroup.Next7Days, item.Group)
        Assert.AreEqual(Today.AddDays(5), item.DueDate)
        Assert.AreEqual(decision.Id, item.DecisionId)
        Assert.AreEqual(submission.Id, item.SubmissionId)
        Assert.AreEqual("Collabra: Psychology", item.JournalName, "Revision deadlines name their journal.")
        Assert.AreEqual(3, item.ProgressDone)
        Assert.AreEqual(1, item.ProgressActive)
        Assert.AreEqual(5, item.ProgressTotal)
    End Sub

    <TestMethod>
    Public Sub RevisionWithoutADeadlineIsListedOnceWithNoDate()
        Dim undated As Manuscript = Revision("Sleep and emotion", Nothing)
        Dim submission As JournalSubmission = undated.Submissions.Single()
        submission.ReviewerResponses.Add(New ReviewerResponseItem With {.DecisionId = submission.Decisions.Single().Id, .Status = ReviewerResponseStatus.Addressed})
        submission.ReviewerResponses.Add(New ReviewerResponseItem With {.DecisionId = submission.Decisions.Single().Id, .Status = ReviewerResponseStatus.Unresolved})
        Dim dated As Manuscript = Revision("Anchoring replication", Today.AddDays(5))

        Dim items As List(Of DeadlineItem) = DeadlineService.Build({undated, dated}, Today)

        Assert.AreEqual(2, items.Count, "Each revision appears once.")
        Dim noDate As DeadlineItem = items.Single(Function(item) item.ManuscriptId = undated.Id)
        Assert.AreEqual(DeadlineGroup.NoDate, noDate.Group)
        Assert.IsNull(noDate.DueDate, "No date is invented.")
        Assert.AreEqual("Revision has no deadline", noDate.Title)
        Assert.AreEqual(1, noDate.ProgressDone)
        Assert.AreEqual(2, noDate.ProgressTotal)
        Assert.AreEqual(DeadlineGroup.Next7Days, items.Single(Function(item) item.ManuscriptId = dated.Id).Group)
    End Sub

    <TestMethod>
    Public Sub UnsubmittedPacketWithOpenChecklistItemsIsPreparationWork()
        Dim manuscript As New Manuscript With {.Title = "Teaching open science", .CurrentStage = PaperStage.Draft, .Location = ManuscriptLocation.Pipeline}
        Dim profile As New ManuscriptReadiness With {.JournalName = "Nurse Education Today"}
        profile.Items.Add(New ReadinessItemState With {.Title = "Cover letter", .IsRequired = True, .Status = ReadinessItemStatus.Complete})
        profile.Items.Add(New ReadinessItemState With {.Title = "Ethics statement", .IsRequired = True, .Status = ReadinessItemStatus.Unresolved})
        profile.Items.Add(New ReadinessItemState With {.Title = "Data statement", .IsRequired = True, .Status = ReadinessItemStatus.Unresolved})
        profile.Items.Add(New ReadinessItemState With {.Title = "Optional graphical abstract", .IsRequired = False, .Status = ReadinessItemStatus.Unresolved})
        manuscript.ReadinessProfiles.Add(profile)
        Dim prepared As New SubmissionPacket With {.ReadinessProfileId = profile.Id, .JournalName = "Nurse Education Today"}
        Dim submitted As New SubmissionPacket With {.ReadinessProfileId = profile.Id, .SubmissionId = Guid.NewGuid()}
        manuscript.SubmissionPackets.Add(prepared)
        manuscript.SubmissionPackets.Add(submitted)

        Dim item As DeadlineItem = DeadlineService.Build({manuscript}, Today).Single()

        Assert.AreEqual(DeadlineKind.Preparation, item.Kind)
        Assert.AreEqual(DeadlineGroup.NoDate, item.Group)
        Assert.AreEqual(prepared.Id, item.PacketId, "A packet already linked to a submission is not preparation work.")
        Assert.AreEqual("Packet for Nurse Education Today: 2 checklist items open", item.Title)
        Assert.AreEqual(1, item.ProgressDone)
        Assert.AreEqual(3, item.ProgressTotal, "Only required items count.")

        manuscript.CurrentStage = PaperStage.Accepted
        Assert.AreEqual(0, DeadlineService.Build({manuscript}, Today).Count, "Accepted work needs no preparation.")
        manuscript.CurrentStage = PaperStage.Draft
        manuscript.Location = ManuscriptLocation.FileDrawer
        Assert.AreEqual(0, DeadlineService.Build({manuscript}, Today).Count, "File Drawer work is set aside.")
        manuscript.Location = ManuscriptLocation.Pipeline
        For Each state As ReadinessItemState In profile.Items
            state.Status = ReadinessItemStatus.NotApplicable
        Next
        Assert.AreEqual(0, DeadlineService.Build({manuscript}, Today).Count, "A resolved checklist leaves nothing to do.")
    End Sub

    <TestMethod>
    Public Sub CompletedRemindersStayInDoneForThirtyDays()
        Dim manuscript As New Manuscript With {.Title = "Registered report"}
        manuscript.Reminders.Add(New ManuscriptReminder With {.Title = "Open", .DueDate = Today.AddDays(2)})
        manuscript.Reminders.Add(New ManuscriptReminder With {.Title = "Recent", .DueDate = Today.AddDays(-5), .IsCompleted = True, .CompletedDate = Today.AddDays(-2)})
        manuscript.Reminders.Add(New ManuscriptReminder With {.Title = "Edge", .DueDate = Today.AddDays(-40), .IsCompleted = True, .CompletedDate = Today.AddDays(-30)})
        manuscript.Reminders.Add(New ManuscriptReminder With {.Title = "Old", .DueDate = Today.AddDays(-60), .IsCompleted = True, .CompletedDate = Today.AddDays(-31)})

        Dim items As List(Of DeadlineItem) = DeadlineService.Build({manuscript}, Today)

        CollectionAssert.AreEqual({"Open", "Recent", "Edge"}, items.Select(Function(item) item.Title).ToList(),
            "Open items come first; Done lists the most recently completed first.")
        Assert.AreEqual(DeadlineGroup.Next7Days, items(0).Group)
        Assert.IsTrue(items.Skip(1).All(Function(item) item.Group = DeadlineGroup.Done AndAlso item.CompletedDate.HasValue))
        Assert.AreEqual(manuscript.Reminders(1).Id, items(1).ReminderId)
    End Sub

    <TestMethod>
    Public Sub FollowUpsAndRemindersKeepTheirOwners()
        Dim manuscript As New Manuscript With {.Title = "Grit scale", .CurrentStage = PaperStage.Submitted}
        Dim submission As New JournalSubmission With {.JournalName = "Assessment", .SubmittedDate = Today.AddDays(-34), .FollowUpDate = Today.AddDays(-3)}
        manuscript.Submissions.Add(submission)
        Dim reminder As New ManuscriptReminder With {.Title = "Send draft to coauthors", .DueDate = Today}
        manuscript.Reminders.Add(reminder)

        Dim items As List(Of DeadlineItem) = DeadlineService.Build({manuscript}, Today)

        Dim followUp As DeadlineItem = items(0)
        Assert.AreEqual(DeadlineKind.FollowUp, followUp.Kind)
        Assert.AreEqual(DeadlineGroup.Overdue, followUp.Group)
        Assert.AreEqual("Follow up with Assessment", followUp.Title)
        Assert.AreEqual(submission.Id, followUp.SubmissionId)
        Assert.AreEqual(DeadlineKind.Reminder, items(1).Kind)
        Assert.AreEqual(DeadlineGroup.Today, items(1).Group)
        Assert.AreEqual(reminder.Id, items(1).ReminderId)
    End Sub

    <TestMethod>
    Public Sub BuildingNeverChangesTheLibraryAndIsDeterministic()
        Dim library As New List(Of Manuscript) From {
            Revision("Anchoring replication", Today.AddDays(5)),
            Revision("Sleep and emotion", Nothing),
            New Manuscript With {.Title = "Grit scale", .CurrentStage = PaperStage.Submitted}
        }
        library(2).Submissions.Add(New JournalSubmission With {.JournalName = "Assessment", .FollowUpDate = Today.AddDays(-3)})
        library(2).Reminders.Add(New ManuscriptReminder With {.Title = "Check portal", .DueDate = Today.AddDays(12)})
        Dim before As String = JsonSerializer.Serialize(library)

        Dim first As String = JsonSerializer.Serialize(DeadlineService.Build(library, Today))
        Dim second As String = JsonSerializer.Serialize(DeadlineService.Build(library, Today))

        Assert.AreEqual(before, JsonSerializer.Serialize(library), "Building the page stores nothing.")
        Assert.AreEqual(first, second)
        CollectionAssert.AreEqual(
            {DeadlineGroup.Overdue, DeadlineGroup.Next7Days, DeadlineGroup.Later, DeadlineGroup.NoDate},
            DeadlineService.Build(library, Today).Select(Function(item) item.Group).ToList())
    End Sub

    <TestMethod>
    Public Sub PostponeChangesTheRecordThatOwnsTheDate()
        Dim dated As Manuscript = Revision("Anchoring replication", Today.AddDays(2))
        Dim datedItem As DeadlineItem = DeadlineService.Build({dated}, Today).Single()
        Assert.AreEqual(Today.AddDays(2), DeadlineService.SetDate(dated, datedItem, Today.AddDays(9).AddHours(15)).Value, "The previous date comes back for undo.")
        Assert.AreEqual(Today.AddDays(9), dated.Submissions.Single().Decisions.Single().RevisionDeadline.Value, "Revision deadlines live on the decision, as dates.")
        Assert.IsFalse(dated.RevisionDeadline.HasValue)

        Dim undated As Manuscript = Revision("Sleep and emotion", Nothing)
        Dim setDeadline As DeadlineItem = DeadlineService.Build({undated}, Today).Single()
        Assert.IsFalse(DeadlineService.SetDate(undated, setDeadline, Today.AddDays(14)).HasValue)
        Assert.AreEqual(Today.AddDays(14), undated.Submissions.Single().Decisions.Single().RevisionDeadline.Value, "Set deadline writes the latest decision.")
        Assert.AreEqual(DeadlineGroup.Later, DeadlineService.Build({undated}, Today).Single().Group, "The item now has its date.")

        ' An older record keeps the deadline on the manuscript itself.
        Dim legacy As Manuscript = Revision("Older record", Nothing)
        legacy.RevisionDeadline = Today.AddDays(-1)
        Dim legacyItem As DeadlineItem = DeadlineService.Build({legacy}, Today).Single()
        DeadlineService.SetDate(legacy, legacyItem, Today.AddDays(6))
        Assert.AreEqual(Today.AddDays(6), legacy.RevisionDeadline.Value)
        Assert.IsFalse(legacy.Submissions.Single().Decisions.Single().RevisionDeadline.HasValue)

        Dim waiting As New Manuscript With {.Title = "Grit scale", .CurrentStage = PaperStage.Submitted}
        Dim submission As New JournalSubmission With {.JournalName = "Assessment", .FollowUpDate = Today.AddDays(-3)}
        waiting.Submissions.Add(submission)
        Dim reminder As New ManuscriptReminder With {.Title = "Send draft", .DueDate = Today}
        waiting.Reminders.Add(reminder)
        Dim items As List(Of DeadlineItem) = DeadlineService.Build({waiting}, Today)

        DeadlineService.SetDate(waiting, items.Single(Function(item) item.Kind = DeadlineKind.FollowUp), Today.AddDays(7))
        Assert.AreEqual(Today.AddDays(7), submission.FollowUpDate.Value)
        DeadlineService.SetDate(waiting, items.Single(Function(item) item.Kind = DeadlineKind.FollowUp), Nothing)
        Assert.IsFalse(submission.FollowUpDate.HasValue, "Clear removes only the follow-up date.")
        Assert.AreEqual(1, waiting.Submissions.Count)

        DeadlineService.SetDate(waiting, items.Single(Function(item) item.Kind = DeadlineKind.Reminder), Today.AddDays(1))
        Assert.AreEqual(Today.AddDays(1), reminder.DueDate)
        Assert.ThrowsExactly(Of ArgumentException)(
            Sub() DeadlineService.SetDate(waiting, items.Single(Function(item) item.Kind = DeadlineKind.Reminder), Nothing))
    End Sub

    <TestMethod>
    Public Sub DoneCompletesOnlyReminders()
        Dim manuscript As New Manuscript With {.Title = "Registered report"}
        Dim reminder As New ManuscriptReminder With {.Title = "Upload preregistration", .DueDate = Today.AddDays(-1)}
        manuscript.Reminders.Add(reminder)
        Dim item As DeadlineItem = DeadlineService.Build({manuscript}, Today).Single()

        Assert.AreSame(reminder, DeadlineService.Complete(manuscript, item, Today.AddHours(10)))
        Assert.IsTrue(reminder.IsCompleted)
        Assert.AreEqual(Today, reminder.CompletedDate.Value)
        Assert.AreEqual(DeadlineGroup.Done, DeadlineService.Build({manuscript}, Today).Single().Group)

        Dim revisionItem As DeadlineItem = DeadlineService.Build({Revision("Anchoring replication", Today)}, Today).Single()
        Assert.ThrowsExactly(Of InvalidOperationException)(Sub() DeadlineService.Complete(manuscript, revisionItem, Today))
    End Sub

    <TestMethod>
    Public Sub TheRailCountsOnlyWhatIsDueNow()
        Dim library As New List(Of Manuscript) From {
            Revision("Overdue", Today.AddDays(-2)),
            Revision("Today", Today),
            Revision("Soon", Today.AddDays(1)),
            Revision("Undated", Nothing)
        }
        Assert.AreEqual(2, DeadlineService.CountDueNow(library, Today))
    End Sub

    Private Shared Function Revision(title As String, deadline As DateTime?) As Manuscript
        Dim manuscript As New Manuscript With {.Title = title, .CurrentStage = PaperStage.Revision, .Location = ManuscriptLocation.Pipeline}
        Dim submission As New JournalSubmission With {.JournalName = "Collabra: Psychology", .SubmittedDate = Today.AddDays(-60)}
        submission.Decisions.Add(New EditorialDecisionEvent With {
            .Decision = EditorialDecision.MajorRevision,
            .DecisionDate = Today.AddDays(-30),
            .RevisionDeadline = deadline
        })
        manuscript.Submissions.Add(submission)
        Return manuscript
    End Function

End Class
