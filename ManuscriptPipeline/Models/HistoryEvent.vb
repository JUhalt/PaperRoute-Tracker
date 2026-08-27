Imports System

Namespace Models

    Public Class HistoryEvent

        Public Property Id As Guid = Guid.NewGuid()

        Public Property RecordedAtUtc As DateTime? = Nothing

        Public Property LastModifiedAtUtc As DateTime? = Nothing

        Public Property EventDate As DateTime = DateTime.Now

        Public Property Stage As PaperStage = PaperStage.Idea

        Public Property Note As String = String.Empty

    End Class

End Namespace
