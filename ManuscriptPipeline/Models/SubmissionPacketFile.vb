Imports System

Namespace Models

    Public Class SubmissionPacketFile

        Public Property Id As Guid = Guid.NewGuid()

        Public Property Role As SubmissionPacketFileRole =
            SubmissionPacketFileRole.Other

        Public Property Label As String = String.Empty

        Public Property Notes As String = String.Empty

        Public Property LocalFilePath As String = String.Empty

        Public Property StorageMode As SubmissionPacketFileStorageMode =
            SubmissionPacketFileStorageMode.MetadataOnly

        Public Property OriginalFileName As String = String.Empty

        ' Optional baseline populated explicitly by the packet integrity service.
        ' Verification preserves it; the model itself never reads, hashes,
        ' copies, or modifies files.
        Public Property Sha256 As String = String.Empty

        Public Property FileSizeBytes As Long? = Nothing

        Public Property LastWriteTimeUtc As DateTime? = Nothing

        Public Property HashComputedAtUtc As DateTime? = Nothing

    End Class

End Namespace
