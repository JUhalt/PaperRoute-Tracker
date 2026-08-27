Imports System
Imports System.Collections.Generic

Namespace Models

    Public Class SubmissionPacket

        Public Property Id As Guid = Guid.NewGuid()

        Public Property ReadinessProfileId As Guid? = Nothing

        Public Property JournalId As Guid? = Nothing

        Public Property JournalName As String = String.Empty

        ' A packet represents files prepared from one exact manuscript
        ' snapshot. Guid.Empty means the packet has not yet been bound.
        Public Property ManuscriptVersionId As Guid = Guid.Empty

        ' A packet may exist before a real submission. This reference is
        ' therefore optional and must never be used to manufacture an event.
        Public Property SubmissionId As Guid? = Nothing

        Public Property RevisionRoundNumber As Integer? = Nothing

        Public Property Label As String = String.Empty

        Public Property Notes As String = String.Empty

        Public Property CreatedAtUtc As DateTime = DateTime.UtcNow

        Public Property LastModifiedAtUtc As DateTime? = Nothing

        Public Property Files As List(Of SubmissionPacketFile) =
            New List(Of SubmissionPacketFile)()

    End Class

End Namespace
