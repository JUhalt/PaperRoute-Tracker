Imports System

Namespace Models

    Public Class ReminderOccurrence

        Public Property SourceId As Guid

        Public Property ManuscriptId As Guid

        Public Property ManuscriptTitle As String = String.Empty

        Public Property Kind As ReminderKind

        Public Property DueDate As DateTime

        Public Property Status As ReminderStatus

        Public Property Title As String = String.Empty

        Public Property Notes As String = String.Empty

        Public Property JournalName As String = String.Empty

        Public Property SubmissionId As Guid? = Nothing

        Public Property IsEditableReminder As Boolean = False


        Public ReadOnly Property KindLabel As String
            Get

                Select Case Kind

                    Case ReminderKind.RevisionDeadline
                        Return "Revision"

                    Case ReminderKind.SubmissionFollowUp
                        Return "Follow-up"

                    Case ReminderKind.Custom
                        Return "Custom"

                    Case Else
                        Return Kind.ToString()

                End Select

            End Get
        End Property


        Public ReadOnly Property StatusLabel As String
            Get

                Select Case Status

                    Case ReminderStatus.Overdue
                        Return "Overdue"

                    Case ReminderStatus.DueToday
                        Return "Due today"

                    Case ReminderStatus.Upcoming
                        Return "Upcoming"

                    Case Else
                        Return Status.ToString()

                End Select

            End Get
        End Property

    End Class

End Namespace
