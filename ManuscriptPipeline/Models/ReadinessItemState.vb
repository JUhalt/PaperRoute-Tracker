Imports System

Namespace Models

    Public Class ReadinessItemState

        Public Property Id As Guid = Guid.NewGuid()

        Public Property TemplateItemId As Guid? = Nothing

        ' Snapshot the requirement text so later edits to the reusable
        ' journal template do not silently rewrite manuscript history.
        Public Property Title As String = String.Empty

        Public Property Description As String = String.Empty

        Public Property Category As String = String.Empty

        Public Property SortOrder As Integer = 0

        Public Property IsRequired As Boolean = True

        Public Property Status As ReadinessItemStatus =
            ReadinessItemStatus.Unresolved

        Public Property UserNotes As String = String.Empty

        Public Property CompletedAtUtc As DateTime? = Nothing

        Public Property LastModifiedAtUtc As DateTime? = Nothing

    End Class

End Namespace
