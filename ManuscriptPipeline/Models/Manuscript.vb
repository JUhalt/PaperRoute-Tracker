Imports System
Imports System.Collections.Generic

Namespace Models

    Public Class Manuscript

        Public Property Id As Guid = Guid.NewGuid()

        Public Property Title As String = String.Empty

        Public Property CoAuthors As String = String.Empty

        Public Property Authors As List(Of ManuscriptAuthor) =
            New List(Of ManuscriptAuthor)()

        Public Property TargetJournal As String = String.Empty

        Public Property TargetJournalId As Guid? = Nothing

        Public Property ManuscriptUrl As String = String.Empty

        Public Property Metadata As ManuscriptMetadata =
            New ManuscriptMetadata()

        Public Property RelatedLinks As List(Of ManuscriptExternalLink) =
            New List(Of ManuscriptExternalLink)()

        Public Property Reminders As List(Of ManuscriptReminder) =
            New List(Of ManuscriptReminder)()

        Public Property Versions As List(Of ManuscriptVersion) =
            New List(Of ManuscriptVersion)()

        Public Property CurrentVersionId As Guid? = Nothing

        Public Property CurrentStage As PaperStage = PaperStage.Idea

        Public Property Location As ManuscriptLocation = ManuscriptLocation.Pipeline

        Public Property StageEnteredDate As DateTime = DateTime.Now

        Public Property RevisionDeadline As DateTime? = Nothing

        Public Property FileDrawerDate As DateTime? = Nothing

        Public Property FileDrawerReason As String = String.Empty

        Public Property History As List(Of HistoryEvent) =
            New List(Of HistoryEvent)()

        Public Property Submissions As List(Of JournalSubmission) =
            New List(Of JournalSubmission)()


        Public ReadOnly Property SubmissionCount As Integer
            Get
                Return Submissions.Count
            End Get
        End Property


        Public ReadOnly Property RejectionCount As Integer
            Get

                Dim count As Integer = 0

                For Each submission As JournalSubmission In Submissions

                    For Each decisionEvent As EditorialDecisionEvent In submission.Decisions

                        Select Case decisionEvent.Decision

                            Case EditorialDecision.Rejected,
                                 EditorialDecision.DeskRejected,
                                 EditorialDecision.RejectedAfterReview

                                count += 1

                        End Select

                    Next

                Next

                Return count

            End Get
        End Property

    End Class

End Namespace
