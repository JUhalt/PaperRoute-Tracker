Imports System

Namespace Models

    Public Class CorrespondenceItem

        Public Property Id As Guid = Guid.NewGuid()

        Public Property RecordedAtUtc As DateTime? = Nothing

        Public Property LastModifiedAtUtc As DateTime? = Nothing

        Public Property ItemDate As DateTime = DateTime.Now

        Public Property Type As CorrespondenceType = CorrespondenceType.Other

        Public Property Title As String = String.Empty

        Public Property Notes As String = String.Empty

        Public Property LocalFilePath As String = String.Empty

        Public Property SourceUrl As String = String.Empty

        Public Property IsManagedCopy As Boolean = False

    End Class

End Namespace
