Imports System
Imports System.Collections.Generic
Imports System.Linq
Imports ManuscriptPipeline.Models

Namespace Services

    ' What needs action, and when (#28). Dated items come only from
    ' ReminderService, PaperRoute's one reminder engine. This adds the work
    ' that has no date yet and recently completed reminders. It reads the
    ' library and never changes it, so building the page stores nothing.
    Public NotInheritable Class DeadlineService

        ' "Next 7 days" counts from today, so it never shrinks at the end of
        ' a calendar week.
        Public Const NearDays As Integer = 7

        ' Completed reminders stay in the Done group this long.
        Public Const DoneDays As Integer = 30

        Private Sub New()
        End Sub


        Public Shared Function Build(
            manuscripts As IEnumerable(Of Manuscript),
            asOfDate As DateTime
        ) As List(Of DeadlineItem)

            If manuscripts Is Nothing Then
                Throw New ArgumentNullException(NameOf(manuscripts))
            End If

            Dim today As DateTime = asOfDate.Date
            Dim library As List(Of Manuscript) = manuscripts.Where(Function(item) item IsNot Nothing).ToList()
            Dim items As New List(Of DeadlineItem)()

            For Each occurrence As ReminderOccurrence In ReminderService.BuildOccurrences(library, today)
                Dim owner As Manuscript = library.FirstOrDefault(Function(item) item.Id = occurrence.ManuscriptId)
                items.Add(FromOccurrence(occurrence, owner, today))
            Next

            For Each manuscript As Manuscript In library
                AddRevisionWithoutDeadline(items, manuscript)
                AddPacketPreparation(items, manuscript)
                AddCompletedReminders(items, manuscript, today)
            Next

            Return items.
                OrderBy(Function(item) item.Group).
                ThenBy(Function(item) SortKey(item)).
                ThenBy(Function(item) item.ManuscriptTitle, StringComparer.CurrentCultureIgnoreCase).
                ThenBy(Function(item) item.Title, StringComparer.CurrentCultureIgnoreCase).
                ToList()

        End Function


        Public Shared Function GroupFor(dueDate As DateTime, asOfDate As DateTime) As DeadlineGroup

            Dim days As Integer = (dueDate.Date - asOfDate.Date).Days

            If days < 0 Then Return DeadlineGroup.Overdue
            If days = 0 Then Return DeadlineGroup.Today
            If days <= NearDays Then Return DeadlineGroup.Next7Days
            Return DeadlineGroup.Later

        End Function


        ' Dated groups run soonest first; Done runs most recently completed first.
        Private Shared Function SortKey(item As DeadlineItem) As Long
            If item.Group = DeadlineGroup.Done Then
                Return -If(item.CompletedDate, DateTime.MinValue).Ticks
            End If
            Return If(item.DueDate, DateTime.MaxValue).Ticks
        End Function


        Private Shared Function FromOccurrence(
            occurrence As ReminderOccurrence,
            manuscript As Manuscript,
            today As DateTime
        ) As DeadlineItem

            Dim item As New DeadlineItem With {
                .Group = GroupFor(occurrence.DueDate, today),
                .DueDate = occurrence.DueDate.Date,
                .Title = occurrence.Title,
                .ManuscriptId = occurrence.ManuscriptId,
                .ManuscriptTitle = occurrence.ManuscriptTitle,
                .JournalName = occurrence.JournalName,
                .SubmissionId = occurrence.SubmissionId
            }

            Select Case occurrence.Kind

                Case ReminderKind.RevisionDeadline
                    ' The deadline belongs to the latest decision, and so do the
                    ' reviewer comments that make up the revision.
                    Dim submission As JournalSubmission = ManuscriptAttentionService.GetLatestSubmission(manuscript)
                    Dim decision As EditorialDecisionEvent = ManuscriptAttentionService.GetLatestDecision(submission)
                    item.Kind = DeadlineKind.Revision
                    item.Title = "Revision due"
                    item.DecisionId = If(decision Is Nothing, CType(Nothing, Guid?), decision.Id)
                    SetCommentProgress(item, submission, decision)

                Case ReminderKind.SubmissionFollowUp
                    item.Kind = DeadlineKind.FollowUp
                    item.Title = If(String.IsNullOrWhiteSpace(occurrence.JournalName),
                                    "Follow up on the submission",
                                    "Follow up with " & occurrence.JournalName)

                Case Else
                    item.Kind = DeadlineKind.Reminder
                    item.ReminderId = occurrence.SourceId

            End Select

            Return item

        End Function


        ' A revision is under way but no deadline is recorded. It stays visible
        ' without an invented date.
        Private Shared Sub AddRevisionWithoutDeadline(items As List(Of DeadlineItem), manuscript As Manuscript)

            If manuscript.CurrentStage <> PaperStage.Revision Then Return

            Dim submission As JournalSubmission = ManuscriptAttentionService.GetLatestSubmission(manuscript)
            Dim decision As EditorialDecisionEvent = ManuscriptAttentionService.GetLatestDecision(submission)

            ' The same rule ReminderService uses for a dated revision, so an
            ' obligation is never listed twice.
            If (decision IsNot Nothing AndAlso decision.RevisionDeadline.HasValue) OrElse
               manuscript.RevisionDeadline.HasValue Then
                Return
            End If

            Dim item As New DeadlineItem With {
                .Kind = DeadlineKind.Revision,
                .Group = DeadlineGroup.NoDate,
                .Title = "Revision has no deadline",
                .ManuscriptId = manuscript.Id,
                .ManuscriptTitle = ReminderService.SafeManuscriptTitle(manuscript),
                .JournalName = ReminderService.JournalFor(manuscript, submission),
                .SubmissionId = If(submission Is Nothing, CType(Nothing, Guid?), submission.Id),
                .DecisionId = If(decision Is Nothing, CType(Nothing, Guid?), decision.Id)
            }

            SetCommentProgress(item, submission, decision)
            items.Add(item)

        End Sub


        ' A packet prepared for a journal but not yet submitted, whose
        ' checklist still has required items open.
        Private Shared Sub AddPacketPreparation(items As List(Of DeadlineItem), manuscript As Manuscript)

            If manuscript.Location <> ManuscriptLocation.Pipeline OrElse
               manuscript.CurrentStage >= PaperStage.Accepted OrElse
               manuscript.SubmissionPackets Is Nothing OrElse
               manuscript.ReadinessProfiles Is Nothing Then
                Return
            End If

            For Each packet As SubmissionPacket In manuscript.SubmissionPackets

                If packet Is Nothing OrElse packet.SubmissionId.HasValue OrElse Not packet.ReadinessProfileId.HasValue Then
                    Continue For
                End If

                Dim profile As ManuscriptReadiness =
                    manuscript.ReadinessProfiles.FirstOrDefault(Function(candidate) candidate IsNot Nothing AndAlso candidate.Id = packet.ReadinessProfileId.Value)

                If profile Is Nothing Then Continue For

                Dim summary As ReadinessSummary = SubmissionReadinessService.GetSummary(profile)
                If summary.RequiredUnresolved = 0 Then Continue For

                Dim journal As String = If(String.IsNullOrWhiteSpace(packet.JournalName), If(profile.JournalName, String.Empty), packet.JournalName).Trim()
                Dim open As String = summary.RequiredUnresolved.ToString() &
                                     If(summary.RequiredUnresolved = 1, " checklist item open", " checklist items open")

                items.Add(New DeadlineItem With {
                    .Kind = DeadlineKind.Preparation,
                    .Group = DeadlineGroup.NoDate,
                    .Title = If(journal.Length = 0, "Packet: ", "Packet for " & journal & ": ") & open,
                    .ManuscriptId = manuscript.Id,
                    .ManuscriptTitle = ReminderService.SafeManuscriptTitle(manuscript),
                    .JournalName = journal,
                    .PacketId = packet.Id,
                    .ProgressDone = summary.RequiredResolved,
                    .ProgressTotal = summary.RequiredTotal
                })

            Next

        End Sub


        Private Shared Sub AddCompletedReminders(items As List(Of DeadlineItem), manuscript As Manuscript, today As DateTime)

            If manuscript.Reminders Is Nothing Then Return

            For Each reminder As ManuscriptReminder In manuscript.Reminders

                If reminder Is Nothing OrElse Not reminder.IsCompleted OrElse Not reminder.CompletedDate.HasValue Then
                    Continue For
                End If

                Dim completed As DateTime = reminder.CompletedDate.Value.Date
                If completed < today.AddDays(-DoneDays) OrElse completed > today Then Continue For

                items.Add(New DeadlineItem With {
                    .Kind = DeadlineKind.Reminder,
                    .Group = DeadlineGroup.Done,
                    .DueDate = reminder.DueDate.Date,
                    .CompletedDate = completed,
                    .Title = If(String.IsNullOrWhiteSpace(reminder.Title), "Reminder", reminder.Title.Trim()),
                    .ManuscriptId = manuscript.Id,
                    .ManuscriptTitle = ReminderService.SafeManuscriptTitle(manuscript),
                    .ReminderId = reminder.Id
                })

            Next

        End Sub


        ' Addressed and not-applicable comments count as done.
        Private Shared Sub SetCommentProgress(item As DeadlineItem, submission As JournalSubmission, decision As EditorialDecisionEvent)

            If submission Is Nothing OrElse decision Is Nothing OrElse submission.ReviewerResponses Is Nothing Then Return

            Dim comments As List(Of ReviewerResponseItem) =
                submission.ReviewerResponses.Where(Function(comment) comment IsNot Nothing AndAlso comment.DecisionId = decision.Id).ToList()

            item.ProgressTotal = comments.Count
            item.ProgressDone = comments.Where(Function(comment) comment.Status = ReviewerResponseStatus.Addressed OrElse
                                                                 comment.Status = ReviewerResponseStatus.NotApplicable).Count()
            item.ProgressActive = comments.Where(Function(comment) comment.Status = ReviewerResponseStatus.InProgress).Count()

        End Sub

    End Class

End Namespace
