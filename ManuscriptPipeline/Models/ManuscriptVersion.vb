Imports System

Namespace Models

    Public Class ManuscriptVersion

        Public Property Id As Guid = Guid.NewGuid()

        Public Property CreatedDate As DateTime = DateTime.Now

        Public Property Label As String = String.Empty

        Public Property Notes As String = String.Empty

        Public Property LocalFilePath As String = String.Empty

        Public Property IsManagedCopy As Boolean = False

        Public Property SubmissionId As Guid? = Nothing

        Public Property DecisionId As Guid? = Nothing

        Public Property RevisionRoundNumber As Integer? = Nothing

    End Class

End Namespace
