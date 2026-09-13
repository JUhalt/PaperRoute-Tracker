Imports System

Namespace Models

    Public Class JournalChecklistTemplateItem

        Public Property Id As Guid = Guid.NewGuid()

        Public Property Title As String = String.Empty

        Public Property Description As String = String.Empty

        Public Property Category As String = String.Empty

        Public Property SortOrder As Integer = 0

        Public Property IsRequired As Boolean = True

    End Class

End Namespace
