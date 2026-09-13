Imports System
Imports System.Collections.Generic
Imports System.IO
Imports ManuscriptPipeline.Models

Namespace Services

    Friend NotInheritable Class ManagedPacketDeletionService

        Friend Const StagingFolderName As String =
            ".paperroute-packet-delete"

        Private ReadOnly _rootDirectory As String


        Friend Sub New(
            rootDirectory As String
        )

            If String.IsNullOrWhiteSpace(
                rootDirectory
            ) Then

                Throw New ArgumentException(
                    "A managed-library root directory is required.",
                    NameOf(rootDirectory)
                )

            End If

            _rootDirectory =
                Path.GetFullPath(
                    rootDirectory
                )

        End Sub


        Friend Function BeginDeletionTransaction(
            manuscripts As IEnumerable(Of Manuscript)
        ) As ManagedPacketDeletionTransaction

            Dim transaction As New ManagedPacketDeletionTransaction(
                _rootDirectory
            )

            Try

                transaction.StageOrphanedPacketFiles(
                    manuscripts
                )

                Return transaction

            Catch

                Try
                    transaction.Rollback()
                Catch
                    ' Preserve the original staging failure.
                End Try

                Throw

            End Try

        End Function


        Friend Sub RecoverStagedDeletions(
            manuscripts As IEnumerable(Of Manuscript)
        )

            Dim stagingRoot As String =
                Path.Combine(
                    _rootDirectory,
                    StagingFolderName
                )

            If Not Directory.Exists(
                stagingRoot
            ) Then

                Return

            End If

            Dim referenced =
                BuildReferencedPacketFileMap(
                    manuscripts
                )

            For Each transactionDirectory As String In
                Directory.GetDirectories(
                    stagingRoot
                )

                RecoverTransactionDirectory(
                    transactionDirectory,
                    referenced
                )

            Next

            DeleteDirectoryIfEmpty(
                stagingRoot
            )

        End Sub


        Private Sub RecoverTransactionDirectory(
            transactionDirectory As String,
            referenced As Dictionary(
                Of Guid,
                Dictionary(
                    Of Guid,
                    HashSet(Of Guid)
                )
            )
        )

            For Each manuscriptDirectory As String In
                Directory.GetDirectories(
                    transactionDirectory
                )

                Dim manuscriptId As Guid

                If Not Guid.TryParseExact(
                    Path.GetFileName(
                        manuscriptDirectory
                    ),
                    "N",
                    manuscriptId
                ) Then

                    Continue For

                End If

                For Each packetDirectory As String In
                    Directory.GetDirectories(
                        manuscriptDirectory
                    )

                    Dim packetId As Guid

                    If Not Guid.TryParseExact(
                        Path.GetFileName(
                            packetDirectory
                        ),
                        "N",
                        packetId
                    ) Then

                        Continue For

                    End If

                    For Each fileDirectory As String In
                        Directory.GetDirectories(
                            packetDirectory
                        )

                        Dim packetFileId As Guid

                        If Not Guid.TryParseExact(
                            Path.GetFileName(
                                fileDirectory
                            ),
                            "N",
                            packetFileId
                        ) Then

                            Continue For

                        End If

                        Dim fileIsReferenced As Boolean =
                            IsReferenced(
                                referenced,
                                manuscriptId,
                                packetId,
                                packetFileId
                            )

                        If fileIsReferenced Then

                            Dim originalDirectory As String =
                                GetPacketFileDirectoryPath(
                                    manuscriptId,
                                    packetId,
                                    packetFileId
                                )

                            If Directory.Exists(
                                originalDirectory
                            ) Then

                                ' An existing directory does not prove that the
                                ' referenced snapshot was restored. Preserve
                                ' both copies so recovery cannot discard the
                                ' only complete file after an interrupted save.
                                Throw New IOException(
                                    "PaperRoute could not safely restore a staged Submission Packet file because the original directory already exists: " &
                                    originalDirectory &
                                    ". The staged files have been preserved at: " &
                                    fileDirectory
                                )

                            Else

                                Directory.CreateDirectory(
                                    Path.GetDirectoryName(
                                        originalDirectory
                                    )
                                )

                                Directory.Move(
                                    fileDirectory,
                                    originalDirectory
                                )

                            End If

                        Else

                            Directory.Delete(
                                fileDirectory,
                                True
                            )

                        End If

                    Next

                    DeleteDirectoryIfEmpty(
                        packetDirectory
                    )

                Next

                DeleteDirectoryIfEmpty(
                    manuscriptDirectory
                )

            Next

            DeleteDirectoryIfEmpty(
                transactionDirectory
            )

        End Sub


        Friend Shared Function BuildReferencedPacketFileMap(
            manuscripts As IEnumerable(Of Manuscript)
        ) As Dictionary(
            Of Guid,
            Dictionary(
                Of Guid,
                HashSet(Of Guid)
            )
        )

            Dim result As New Dictionary(
                Of Guid,
                Dictionary(
                    Of Guid,
                    HashSet(Of Guid)
                )
            )()

            If manuscripts Is Nothing Then
                Return result
            End If

            For Each manuscript As Manuscript In manuscripts

                If manuscript Is Nothing OrElse
                   manuscript.Id = Guid.Empty Then

                    Continue For

                End If

                Dim packetMap As New Dictionary(
                    Of Guid,
                    HashSet(Of Guid)
                )()

                If manuscript.SubmissionPackets IsNot Nothing Then

                    For Each packet As SubmissionPacket In
                        manuscript.SubmissionPackets

                        If packet Is Nothing OrElse
                           packet.Id = Guid.Empty Then

                            Continue For

                        End If

                        Dim fileIds As New HashSet(Of Guid)()

                        If packet.Files IsNot Nothing Then

                            For Each packetFile As SubmissionPacketFile In
                                packet.Files

                                If packetFile Is Nothing OrElse
                                   packetFile.Id = Guid.Empty OrElse
                                   packetFile.StorageMode <>
                                       SubmissionPacketFileStorageMode.ManagedCopy Then

                                    Continue For

                                End If

                                fileIds.Add(
                                    packetFile.Id
                                )

                            Next

                        End If

                        packetMap(packet.Id) =
                            fileIds

                    Next

                End If

                result(manuscript.Id) =
                    packetMap

            Next

            Return result

        End Function


        Private Shared Function IsReferenced(
            referenced As Dictionary(
                Of Guid,
                Dictionary(
                    Of Guid,
                    HashSet(Of Guid)
                )
            ),
            manuscriptId As Guid,
            packetId As Guid,
            packetFileId As Guid
        ) As Boolean

            If Not referenced.ContainsKey(
                manuscriptId
            ) Then

                Return False

            End If

            Dim packetMap =
                referenced(manuscriptId)

            If Not packetMap.ContainsKey(
                packetId
            ) Then

                Return False

            End If

            Return packetMap(packetId).Contains(
                packetFileId
            )

        End Function


        Private Function GetPacketFileDirectoryPath(
            manuscriptId As Guid,
            packetId As Guid,
            packetFileId As Guid
        ) As String

            Return Path.Combine(
                _rootDirectory,
                manuscriptId.ToString("N"),
                "packets",
                packetId.ToString("N"),
                packetFileId.ToString("N")
            )

        End Function


        Private Shared Sub DeleteDirectoryIfEmpty(
            directoryPath As String
        )

            If Not Directory.Exists(
                directoryPath
            ) Then

                Return

            End If

            If Directory.GetFileSystemEntries(
                directoryPath
            ).Length > 0 Then

                Return

            End If

            Directory.Delete(
                directoryPath,
                False
            )

        End Sub


        Friend NotInheritable Class ManagedPacketDeletionTransaction
            Implements IDisposable

            Private ReadOnly _rootDirectory As String
            Private ReadOnly _stagingRoot As String
            Private ReadOnly _operations As New List(Of MoveOperation)()

            Private _completed As Boolean = False


            Friend Sub New(
                rootDirectory As String
            )

                _rootDirectory =
                    rootDirectory

                _stagingRoot =
                    Path.Combine(
                        rootDirectory,
                        ManagedPacketDeletionService.StagingFolderName,
                        Guid.NewGuid().ToString("N")
                    )

            End Sub


            Friend ReadOnly Property StagedDirectoryCount As Integer
                Get
                    Return _operations.Count
                End Get
            End Property


            Friend Sub StageOrphanedPacketFiles(
                manuscripts As IEnumerable(Of Manuscript)
            )

                Dim referenced =
                    ManagedPacketDeletionService.BuildReferencedPacketFileMap(
                        manuscripts
                    )

                If Not Directory.Exists(
                    _rootDirectory
                ) Then

                    Return

                End If

                For Each manuscriptDirectory As String In
                    Directory.GetDirectories(
                        _rootDirectory
                    )

                    Dim manuscriptId As Guid

                    If Not Guid.TryParseExact(
                        Path.GetFileName(
                            manuscriptDirectory
                        ),
                        "N",
                        manuscriptId
                    ) Then

                        Continue For

                    End If

                    Dim packetsDirectory As String =
                        Path.Combine(
                            manuscriptDirectory,
                            "packets"
                        )

                    If Not Directory.Exists(
                        packetsDirectory
                    ) Then

                        Continue For

                    End If

                    For Each packetDirectory As String In
                        Directory.GetDirectories(
                            packetsDirectory
                        )

                        Dim packetId As Guid

                        If Not Guid.TryParseExact(
                            Path.GetFileName(
                                packetDirectory
                            ),
                            "N",
                            packetId
                        ) Then

                            Continue For

                        End If

                        For Each fileDirectory As String In
                            Directory.GetDirectories(
                                packetDirectory
                            )

                            Dim packetFileId As Guid

                            If Not Guid.TryParseExact(
                                Path.GetFileName(
                                    fileDirectory
                                ),
                                "N",
                                packetFileId
                            ) Then

                                Continue For

                            End If

                            If ManagedPacketDeletionService.IsReferenced(
                                referenced,
                                manuscriptId,
                                packetId,
                                packetFileId
                            ) Then

                                Continue For

                            End If

                            Dim stagedDirectory As String =
                                Path.Combine(
                                    _stagingRoot,
                                    manuscriptId.ToString("N"),
                                    packetId.ToString("N"),
                                    packetFileId.ToString("N")
                                )

                            Directory.CreateDirectory(
                                Path.GetDirectoryName(
                                    stagedDirectory
                                )
                            )

                            Directory.Move(
                                fileDirectory,
                                stagedDirectory
                            )

                            _operations.Add(
                                New MoveOperation(
                                    fileDirectory,
                                    stagedDirectory
                                )
                            )

                        Next

                        ManagedPacketDeletionService.DeleteDirectoryIfEmpty(
                            packetDirectory
                        )

                    Next

                    ManagedPacketDeletionService.DeleteDirectoryIfEmpty(
                        packetsDirectory
                    )

                Next

            End Sub


            Friend Sub Commit()

                If _completed Then
                    Return
                End If

                ' The authoritative JSON has already been replaced. Cleanup
                ' failures must not trigger rollback or report an unsaved edit.
                _completed =
                    True

                Try

                    If Directory.Exists(
                        _stagingRoot
                    ) Then

                        Directory.Delete(
                            _stagingRoot,
                            True
                        )

                    End If

                    ManagedPacketDeletionService.DeleteDirectoryIfEmpty(
                        Path.GetDirectoryName(
                            _stagingRoot
                        )
                    )

                Catch
                    ' Startup recovery will discard unreferenced staging.
                End Try

            End Sub


            Friend Sub Rollback()

                If _completed Then
                    Return
                End If

                For index As Integer =
                    _operations.Count - 1 To 0 Step -1

                    Dim operation As MoveOperation =
                        _operations(index)

                    If Not Directory.Exists(
                        operation.StagedDirectory
                    ) Then

                        Continue For

                    End If

                    If Directory.Exists(
                        operation.OriginalDirectory
                    ) Then

                        Throw New IOException(
                            "PaperRoute could not restore a staged Submission Packet file because the original directory already exists: " &
                            operation.OriginalDirectory
                        )

                    End If

                    Directory.CreateDirectory(
                        Path.GetDirectoryName(
                            operation.OriginalDirectory
                        )
                    )

                    Directory.Move(
                        operation.StagedDirectory,
                        operation.OriginalDirectory
                    )

                Next

                If Directory.Exists(
                    _stagingRoot
                ) Then

                    Try
                        Directory.Delete(
                            _stagingRoot,
                            True
                        )
                    Catch
                        ' Best-effort cleanup only.
                    End Try

                End If

                ManagedPacketDeletionService.DeleteDirectoryIfEmpty(
                    Path.GetDirectoryName(
                        _stagingRoot
                    )
                )

                _completed =
                    True

            End Sub


            Public Sub Dispose() Implements IDisposable.Dispose

                If Not _completed Then
                    Rollback()
                End If

            End Sub


            Private NotInheritable Class MoveOperation

                Friend ReadOnly Property OriginalDirectory As String
                Friend ReadOnly Property StagedDirectory As String


                Friend Sub New(
                    originalDirectory As String,
                    stagedDirectory As String
                )

                    Me.OriginalDirectory =
                        originalDirectory

                    Me.StagedDirectory =
                        stagedDirectory

                End Sub

            End Class

        End Class

    End Class

End Namespace
