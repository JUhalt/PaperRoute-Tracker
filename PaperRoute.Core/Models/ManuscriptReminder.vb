Imports System

Namespace Models

    Public Class ManuscriptReminder

        Public Property Id As Guid = CoreIds.NewId()

        Public Property DueDate As DateTime = CoreClock.Today

        Public Property Title As String = String.Empty

        Public Property Notes As String = String.Empty

        Public Property IsCompleted As Boolean = False

        Public Property CompletedDate As DateTime? = Nothing

    End Class

End Namespace
