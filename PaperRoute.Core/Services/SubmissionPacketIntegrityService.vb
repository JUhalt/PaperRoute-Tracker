Imports System
Imports System.IO
Imports System.Security
Imports System.Security.Cryptography
Imports System.Threading
Imports ManuscriptPipeline.Models

Namespace Services

    Public NotInheritable Class SubmissionPacketIntegrityService

        Private Const ReadBufferSize As Integer = 81920

        Private Sub New()
        End Sub

        ' Opening a handle checks readability; inspection never reads file contents.
        Public Shared Function Inspect(
            file As SubmissionPacketFile
        ) As PacketFileIntegrityResult

            ValidateFile(file)

            Dim preliminary As PacketFileIntegrityResult = CheckRecord(file)
            If preliminary IsNot Nothing Then Return preliminary

            Try
                Using source As FileStream = OpenRead(file.LocalFilePath)
                    Dim info As New FileInfo(file.LocalFilePath)
                    info.Refresh()

                    If String.IsNullOrWhiteSpace(file.Sha256) Then
                        Return New PacketFileIntegrityResult(
                            PacketFileIntegrityStatus.NotRecorded,
                            "No fingerprint recorded. Record a fingerprint to compare this file later.",
                            fileSizeBytes:=source.Length,
                            lastWriteTimeUtc:=info.LastWriteTimeUtc
                        )
                    End If

                    Return New PacketFileIntegrityResult(
                        PacketFileIntegrityStatus.NotChecked,
                        "Fingerprint recorded. Check the file to compare its current contents.",
                        fileSizeBytes:=source.Length,
                        lastWriteTimeUtc:=info.LastWriteTimeUtc
                    )
                End Using
            Catch ex As Exception When IsFileAccessException(ex)
                Return FileAccessFailure(ex)
            End Try

        End Function

        Public Shared Function Verify(
            file As SubmissionPacketFile,
            Optional cancellationToken As CancellationToken = Nothing
        ) As PacketFileIntegrityResult

            ValidateFile(file)
            cancellationToken.ThrowIfCancellationRequested()

            Dim preliminary As PacketFileIntegrityResult = CheckRecord(file)
            If preliminary IsNot Nothing Then Return preliminary

            Dim baseline As String = file.Sha256
            If String.IsNullOrWhiteSpace(baseline) Then Return Inspect(file)

            Dim observed As PacketFileIntegrityResult = ReadFingerprint(file, cancellationToken)
            If observed.Status <> PacketFileIntegrityStatus.Unchanged Then Return observed

            Dim matches As Boolean = String.Equals(
                baseline.Trim(), observed.Sha256, StringComparison.OrdinalIgnoreCase
            )

            Return New PacketFileIntegrityResult(
                If(matches, PacketFileIntegrityStatus.Unchanged, PacketFileIntegrityStatus.Changed),
                If(matches,
                   "Unchanged: the file contents match the recorded fingerprint.",
                   "Changed: the file contents differ from the recorded fingerprint."),
                observed.Sha256,
                observed.FileSizeBytes,
                observed.LastWriteTimeUtc,
                observed.CheckedAtUtc
            )

        End Function

        ' This is the only operation that changes baseline fields, and only after
        ' the entire read succeeds. The caller chooses when to persist the record.
        Public Shared Function CaptureBaseline(
            file As SubmissionPacketFile,
            Optional replaceExisting As Boolean = False,
            Optional capturedAtUtc As DateTime? = Nothing,
            Optional cancellationToken As CancellationToken = Nothing
        ) As PacketFileIntegrityResult

            ValidateFile(file)
            cancellationToken.ThrowIfCancellationRequested()

            SyncLock file
                If file.StorageMode = SubmissionPacketFileStorageMode.MetadataOnly Then Return CheckRecord(file)

                If Not replaceExisting AndAlso Not String.IsNullOrWhiteSpace(file.Sha256) Then
                    Throw New InvalidOperationException(
                        "A fingerprint is already recorded. Explicitly choose to replace it before recording another."
                    )
                End If

                Dim preliminary As PacketFileIntegrityResult = CheckRecord(file)
                If preliminary IsNot Nothing Then Return preliminary

                Dim observed As PacketFileIntegrityResult = ReadFingerprint(file, cancellationToken)
                If observed.Status <> PacketFileIntegrityStatus.Unchanged Then Return observed

                cancellationToken.ThrowIfCancellationRequested()

                Dim recordedAt As DateTime = If(capturedAtUtc, observed.CheckedAtUtc.Value)
                file.Sha256 = observed.Sha256
                file.FileSizeBytes = observed.FileSizeBytes
                file.LastWriteTimeUtc = observed.LastWriteTimeUtc
                file.HashComputedAtUtc = recordedAt

                Return New PacketFileIntegrityResult(
                    PacketFileIntegrityStatus.Unchanged,
                    "Fingerprint recorded for the current file contents.",
                    observed.Sha256,
                    observed.FileSizeBytes,
                    observed.LastWriteTimeUtc,
                    observed.CheckedAtUtc
                )
            End SyncLock

        End Function

        Private Shared Function ReadFingerprint(
            file As SubmissionPacketFile,
            cancellationToken As CancellationToken
        ) As PacketFileIntegrityResult

            Try
                cancellationToken.ThrowIfCancellationRequested()

                ' FileShare.Read rejects writers and deletion for the whole read.
                Using source As FileStream = OpenRead(file.LocalFilePath)
                    Dim info As New FileInfo(file.LocalFilePath)
                    info.Refresh()
                    Dim originalLength As Long = source.Length
                    Dim originalWriteTime As DateTime = info.LastWriteTimeUtc
                    Dim totalRead As Long = 0
                    Dim fingerprint As String

                    Using hash As IncrementalHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256)
                        Dim buffer(ReadBufferSize - 1) As Byte

                        Do
                            cancellationToken.ThrowIfCancellationRequested()
                            Dim bytesRead As Integer = source.Read(buffer, 0, buffer.Length)
                            If bytesRead = 0 Then Exit Do
                            hash.AppendData(buffer, 0, bytesRead)
                            totalRead += bytesRead
                        Loop

                        fingerprint = Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant()
                    End Using

                    info.Refresh()
                    If source.Length <> originalLength OrElse
                       totalRead <> originalLength OrElse
                       info.Length <> originalLength OrElse
                       info.LastWriteTimeUtc <> originalWriteTime Then

                        Return New PacketFileIntegrityResult(
                            PacketFileIntegrityStatus.Unavailable,
                            "The file changed while being read. Check it again after other applications finish saving."
                        )
                    End If

                    cancellationToken.ThrowIfCancellationRequested()

                    Return New PacketFileIntegrityResult(
                        PacketFileIntegrityStatus.Unchanged,
                        "File fingerprint computed.",
                        fingerprint,
                        originalLength,
                        originalWriteTime,
                        DateTime.UtcNow
                    )
                End Using
            Catch ex As Exception When IsFileAccessException(ex)
                Return FileAccessFailure(ex)
            End Try

        End Function

        Private Shared Function OpenRead(localPath As String) As FileStream
            Return New FileStream(
                localPath, FileMode.Open, FileAccess.Read, FileShare.Read,
                ReadBufferSize, FileOptions.SequentialScan
            )
        End Function

        Private Shared Function CheckRecord(
            file As SubmissionPacketFile
        ) As PacketFileIntegrityResult

            If file.StorageMode = SubmissionPacketFileStorageMode.MetadataOnly Then
                Return New PacketFileIntegrityResult(
                    PacketFileIntegrityStatus.MetadataOnly,
                    "Metadata only: no local file to check."
                )
            End If

            If file.StorageMode <> SubmissionPacketFileStorageMode.LinkedExternal AndAlso
               file.StorageMode <> SubmissionPacketFileStorageMode.ManagedCopy Then
                Return New PacketFileIntegrityResult(
                    PacketFileIntegrityStatus.Unavailable,
                    "The file storage mode is not recognized."
                )
            End If

            If String.IsNullOrWhiteSpace(file.LocalFilePath) Then
                Return New PacketFileIntegrityResult(
                    PacketFileIntegrityStatus.Missing,
                    "Missing: no local file path is recorded."
                )
            End If

            Return Nothing

        End Function

        Private Shared Function IsFileAccessException(ex As Exception) As Boolean
            Return TypeOf ex Is IOException OrElse
                TypeOf ex Is UnauthorizedAccessException OrElse
                TypeOf ex Is SecurityException OrElse
                TypeOf ex Is ArgumentException OrElse
                TypeOf ex Is NotSupportedException
        End Function

        Private Shared Function FileAccessFailure(ex As Exception) As PacketFileIntegrityResult
            If TypeOf ex Is FileNotFoundException OrElse TypeOf ex Is DirectoryNotFoundException Then
                Return New PacketFileIntegrityResult(
                    PacketFileIntegrityStatus.Missing,
                    "Missing: the local file could not be found. The recorded fingerprint is unchanged."
                )
            End If

            Return New PacketFileIntegrityResult(
                PacketFileIntegrityStatus.Unavailable,
                "Unavailable: the local file could not be read. It may be locked or access may be denied. The recorded fingerprint is unchanged."
            )
        End Function

        Private Shared Sub ValidateFile(file As SubmissionPacketFile)
            If file Is Nothing Then Throw New ArgumentNullException(NameOf(file))
        End Sub

    End Class

End Namespace
