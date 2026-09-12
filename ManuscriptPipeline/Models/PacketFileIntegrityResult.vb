Imports System

Namespace Models

    Public NotInheritable Class PacketFileIntegrityResult

        Public Sub New(
            status As PacketFileIntegrityStatus,
            message As String,
            Optional sha256 As String = "",
            Optional fileSizeBytes As Long? = Nothing,
            Optional lastWriteTimeUtc As DateTime? = Nothing,
            Optional checkedAtUtc As DateTime? = Nothing
        )
            Me.Status = status
            Me.Message = If(message, String.Empty)
            Me.Sha256 = If(sha256, String.Empty)
            Me.FileSizeBytes = fileSizeBytes
            Me.LastWriteTimeUtc = lastWriteTimeUtc
            Me.CheckedAtUtc = checkedAtUtc
        End Sub

        Public ReadOnly Property Status As PacketFileIntegrityStatus

        Public ReadOnly Property Message As String

        ' These values describe the observed file, not a replacement baseline.
        Public ReadOnly Property Sha256 As String

        Public ReadOnly Property FileSizeBytes As Long?

        Public ReadOnly Property LastWriteTimeUtc As DateTime?

        Public ReadOnly Property CheckedAtUtc As DateTime?

    End Class

End Namespace
