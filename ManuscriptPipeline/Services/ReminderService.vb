Imports System
Imports System.Collections.Generic
Imports System.Linq
Imports ManuscriptPipeline.Models

Namespace Services

    Public NotInheritable Class ReminderService

        Private Sub New()
        End Sub


        Public Shared Function BuildOccurrences(
            manuscripts As IEnumerable(Of Manuscript),
            asOfDate As DateTime
        ) As List(Of ReminderOccurrence)

            If manuscripts Is Nothing Then
                Throw New ArgumentNullException(NameOf(manuscripts))
            End If

            Dim today As DateTime =
                asOfDate.Date

            Dim occurrences As New List(Of ReminderOccurrence)()

            For Each manuscript As Manuscript In manuscripts

                If manuscript Is Nothing Then
                    Continue For
                End If

                AddRevisionDeadline(
                    occurrences,
                    manuscript,
                    today
                )

                AddSubmissionFollowUps(
                    occurrences,
                    manuscript,
                    today
                )

                AddCustomReminders(
                    occurrences,
                    manuscript,
                    today
                )

            Next

            Return occurrences.
                OrderBy(
                    Function(item)
                        Return item.DueDate.Date
                    End Function
                ).
                ThenBy(
                    Function(item)
                        Return item.ManuscriptTitle
                    End Function,
                    StringComparer.CurrentCultureIgnoreCase
                ).
                ThenBy(
                    Function(item)
                        Return item.Title
                    End Function,
                    StringComparer.CurrentCultureIgnoreCase
                ).
                ToList()

        End Function


        Public Shared Function NotificationCandidates(
            manuscripts As IEnumerable(Of Manuscript),
            asOfDate As DateTime,
            daysAhead As Integer
        ) As List(Of ReminderOccurrence)

            Dim safeDaysAhead As Integer =
                Math.Max(
                    0,
                    daysAhead
                )

            Dim lastDate As DateTime =
                asOfDate.Date.AddDays(
                    safeDaysAhead
                )

            Return BuildOccurrences(
                manuscripts,
                asOfDate
            ).
            Where(
                Function(item)
                    Return item.DueDate.Date <= lastDate
                End Function
            ).
            ToList()

        End Function


        Public Shared Function CreateNotificationSummary(
            occurrences As IEnumerable(Of ReminderOccurrence),
            daysAhead As Integer
        ) As String

            If occurrences Is Nothing Then
                Throw New ArgumentNullException(NameOf(occurrences))
            End If

            Dim items As List(Of ReminderOccurrence) =
                occurrences.
                    Where(
                        Function(item)
                            Return item IsNot Nothing
                        End Function
                    ).
                    ToList()

            If items.Count = 0 Then
                Return String.Empty
            End If

            Dim overdue As Integer =
                items.
                    Where(
                        Function(item)
                            Return item.Status = ReminderStatus.Overdue
                        End Function
                    ).
                    Count()

            Dim dueToday As Integer =
                items.
                    Where(
                        Function(item)
                            Return item.Status = ReminderStatus.DueToday
                        End Function
                    ).
                    Count()

            Dim upcoming As Integer =
                items.
                    Where(
                        Function(item)
                            Return item.Status = ReminderStatus.Upcoming
                        End Function
                    ).
                    Count()

            Dim parts As New List(Of String)()

            If overdue > 0 Then
                parts.Add(
                    overdue.ToString() &
                    " overdue"
                )
            End If

            If dueToday > 0 Then
                parts.Add(
                    dueToday.ToString() &
                    " due today"
                )
            End If

            If upcoming > 0 Then

                parts.Add(
                    upcoming.ToString() &
                    " upcoming within " &
                    Math.Max(
                        0,
                        daysAhead
                    ).ToString() &
                    " day(s)"
                )

            End If

            Return String.Join(
                ", ",
                parts
            ) &
                ". Open Reminders in PaperRoute for details."

        End Function


        Private Shared Sub AddRevisionDeadline(
            target As List(Of ReminderOccurrence),
            manuscript As Manuscript,
            today As DateTime
        )

            If manuscript.CurrentStage <> PaperStage.Revision Then
                Return
            End If

            Dim latestSubmission As JournalSubmission =
                ManuscriptAttentionService.GetLatestSubmission(
                    manuscript
                )

            Dim latestDecision As EditorialDecisionEvent =
                ManuscriptAttentionService.GetLatestDecision(
                    latestSubmission
                )

            Dim dueDate As DateTime? =
                Nothing

            Dim sourceId As Guid =
                manuscript.Id

            Dim submissionId As Guid? =
                Nothing

            If latestSubmission IsNot Nothing Then

                submissionId =
                    latestSubmission.Id

            End If

            If latestDecision IsNot Nothing AndAlso
               latestDecision.RevisionDeadline.HasValue Then

                dueDate =
                    latestDecision.RevisionDeadline.Value.Date

                sourceId =
                    latestDecision.Id

            ElseIf manuscript.RevisionDeadline.HasValue Then

                ' Compatibility fallback for older/imported records that
                ' may carry the manuscript-level field without a linked
                ' editorial-decision deadline.
                dueDate =
                    manuscript.RevisionDeadline.Value.Date

            End If

            If Not dueDate.HasValue Then
                Return
            End If

            target.Add(
                New ReminderOccurrence With {
                    .SourceId = sourceId,
                    .ManuscriptId = manuscript.Id,
                    .ManuscriptTitle =
                        SafeManuscriptTitle(
                            manuscript
                        ),
                    .Kind = ReminderKind.RevisionDeadline,
                    .DueDate = dueDate.Value,
                    .Status =
                        GetStatus(
                            dueDate.Value,
                            today
                        ),
                    .Title = "Revision deadline",
                    .Notes =
                        "Revision deadline recorded in the manuscript's editorial history.",
                    .SubmissionId =
                        submissionId,
                    .IsEditableReminder = False
                }
            )

        End Sub


        Private Shared Sub AddSubmissionFollowUps(
            target As List(Of ReminderOccurrence),
            manuscript As Manuscript,
            today As DateTime
        )

            If manuscript.Submissions Is Nothing Then
                Return
            End If

            For Each submission As JournalSubmission In manuscript.Submissions

                If submission Is Nothing OrElse
                   Not submission.FollowUpDate.HasValue Then

                    Continue For

                End If

                Dim dueDate As DateTime =
                    submission.FollowUpDate.Value.Date

                Dim journalName As String =
                    If(
                        submission.JournalName,
                        String.Empty
                    ).Trim()

                Dim title As String =
                    "Submission follow-up"

                If Not String.IsNullOrWhiteSpace(
                    journalName
                ) Then

                    title &=
                        ": " &
                        journalName

                End If

                target.Add(
                    New ReminderOccurrence With {
                        .SourceId = submission.Id,
                        .ManuscriptId = manuscript.Id,
                        .ManuscriptTitle =
                            SafeManuscriptTitle(
                                manuscript
                            ),
                        .Kind = ReminderKind.SubmissionFollowUp,
                        .DueDate = dueDate,
                        .Status =
                            GetStatus(
                                dueDate,
                                today
                            ),
                        .Title = title,
                        .Notes =
                            "Follow up on the journal submission if no editorial decision has been recorded.",
                        .JournalName = journalName,
                        .SubmissionId = submission.Id,
                        .IsEditableReminder = False
                    }
                )

            Next

        End Sub


        Private Shared Sub AddCustomReminders(
            target As List(Of ReminderOccurrence),
            manuscript As Manuscript,
            today As DateTime
        )

            If manuscript.Reminders Is Nothing Then
                Return
            End If

            For Each reminder As ManuscriptReminder In manuscript.Reminders

                If reminder Is Nothing OrElse
                   reminder.IsCompleted Then

                    Continue For

                End If

                Dim dueDate As DateTime =
                    reminder.DueDate.Date

                target.Add(
                    New ReminderOccurrence With {
                        .SourceId = reminder.Id,
                        .ManuscriptId = manuscript.Id,
                        .ManuscriptTitle =
                            SafeManuscriptTitle(
                                manuscript
                            ),
                        .Kind = ReminderKind.Custom,
                        .DueDate = dueDate,
                        .Status =
                            GetStatus(
                                dueDate,
                                today
                            ),
                        .Title =
                            If(
                                String.IsNullOrWhiteSpace(
                                    reminder.Title
                                ),
                                "Reminder",
                                reminder.Title.Trim()
                            ),
                        .Notes =
                            If(
                                reminder.Notes,
                                String.Empty
                            ),
                        .IsEditableReminder = True
                    }
                )

            Next

        End Sub


        Private Shared Function GetStatus(
            dueDate As DateTime,
            today As DateTime
        ) As ReminderStatus

            If dueDate.Date < today.Date Then
                Return ReminderStatus.Overdue
            End If

            If dueDate.Date = today.Date Then
                Return ReminderStatus.DueToday
            End If

            Return ReminderStatus.Upcoming

        End Function


        Private Shared Function SafeManuscriptTitle(
            manuscript As Manuscript
        ) As String

            If manuscript Is Nothing OrElse
               String.IsNullOrWhiteSpace(
                   manuscript.Title
               ) Then

                Return "(Untitled manuscript)"

            End If

            Return manuscript.Title.Trim()

        End Function

    End Class

End Namespace
