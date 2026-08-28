Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.Linq
Imports ManuscriptPipeline.Models

Namespace Services

    Public NotInheritable Class SubmissionPacketService

        Private Sub New()
        End Sub


        Public Shared Function CreatePacket(
            manuscript As Manuscript,
            manuscriptVersionId As Guid,
            label As String,
            notes As String,
            Optional readinessProfileId As Guid? = Nothing,
            Optional submissionId As Guid? = Nothing,
            Optional revisionRoundNumber As Integer? = Nothing,
            Optional createdAtUtc As DateTime? = Nothing
        ) As SubmissionPacket

            ValidateManuscript(manuscript)
            EnsureCollections(manuscript)

            Dim version As ManuscriptVersion =
                RequireVersion(manuscript, manuscriptVersionId)

            Dim readiness As ManuscriptReadiness =
                ResolveReadiness(manuscript, readinessProfileId)

            Dim submission As JournalSubmission =
                ResolveSubmission(manuscript, submissionId)

            ValidateRevisionRound(submission, revisionRoundNumber)
            ValidateJournalConsistency(readiness, submission)

            Dim packet As New SubmissionPacket With {
                .Id = Guid.NewGuid(),
                .ReadinessProfileId = If(readiness Is Nothing, Nothing, CType(readiness.Id, Guid?)),
                .JournalId = ResolveJournalId(manuscript, readiness, submission),
                .JournalName = ResolveJournalName(manuscript, readiness, submission),
                .ManuscriptVersionId = version.Id,
                .SubmissionId = If(submission Is Nothing, Nothing, CType(submission.Id, Guid?)),
                .RevisionRoundNumber = revisionRoundNumber,
                .Label = NormalizePacketLabel(label),
                .Notes = NormalizeText(notes),
                .CreatedAtUtc = If(createdAtUtc.HasValue, createdAtUtc.Value, DateTime.UtcNow)
            }

            manuscript.SubmissionPackets.Add(packet)

            Return packet

        End Function


        Public Shared Function UpdatePacket(
            manuscript As Manuscript,
            packetId As Guid,
            manuscriptVersionId As Guid,
            label As String,
            notes As String,
            Optional readinessProfileId As Guid? = Nothing,
            Optional submissionId As Guid? = Nothing,
            Optional revisionRoundNumber As Integer? = Nothing,
            Optional modifiedAtUtc As DateTime? = Nothing
        ) As Boolean

            ValidateManuscript(manuscript)
            EnsureCollections(manuscript)

            Dim packet As SubmissionPacket =
                RequirePacket(manuscript, packetId)

            Dim version As ManuscriptVersion =
                RequireVersion(manuscript, manuscriptVersionId)

            Dim readiness As ManuscriptReadiness =
                ResolveReadiness(manuscript, readinessProfileId)

            Dim submission As JournalSubmission =
                ResolveSubmission(manuscript, submissionId)

            ValidateRevisionRound(submission, revisionRoundNumber)
            ValidateJournalConsistency(readiness, submission)

            Dim journalId As Guid? =
                ResolveJournalId(manuscript, readiness, submission)

            Dim journalName As String =
                ResolveJournalName(manuscript, readiness, submission)

            Dim readinessId As Guid? =
                If(readiness Is Nothing, Nothing, CType(readiness.Id, Guid?))

            Dim resolvedSubmissionId As Guid? =
                If(submission Is Nothing, Nothing, CType(submission.Id, Guid?))

            Dim normalizedLabel As String = NormalizePacketLabel(label)
            Dim normalizedNotes As String = NormalizeText(notes)

            Dim changed As Boolean =
                packet.ManuscriptVersionId <> version.Id OrElse
                Not NullableGuidEquals(packet.ReadinessProfileId, readinessId) OrElse
                Not NullableGuidEquals(packet.JournalId, journalId) OrElse
                Not String.Equals(packet.JournalName, journalName, StringComparison.Ordinal) OrElse
                Not NullableGuidEquals(packet.SubmissionId, resolvedSubmissionId) OrElse
                Not NullableIntegerEquals(packet.RevisionRoundNumber, revisionRoundNumber) OrElse
                Not String.Equals(packet.Label, normalizedLabel, StringComparison.Ordinal) OrElse
                Not String.Equals(packet.Notes, normalizedNotes, StringComparison.Ordinal)

            If Not changed Then
                Return False
            End If

            packet.ManuscriptVersionId = version.Id
            packet.ReadinessProfileId = readinessId
            packet.JournalId = journalId
            packet.JournalName = journalName
            packet.SubmissionId = resolvedSubmissionId
            packet.RevisionRoundNumber = revisionRoundNumber
            packet.Label = normalizedLabel
            packet.Notes = normalizedNotes
            packet.LastModifiedAtUtc = If(modifiedAtUtc.HasValue, modifiedAtUtc.Value, DateTime.UtcNow)

            Return True

        End Function


        Public Shared Function AddFile(
            packet As SubmissionPacket,
            role As SubmissionPacketFileRole,
            label As String,
            notes As String,
            localFilePath As String,
            storageMode As SubmissionPacketFileStorageMode,
            Optional modifiedAtUtc As DateTime? = Nothing
        ) As SubmissionPacketFile

            If packet Is Nothing Then
                Throw New ArgumentNullException(NameOf(packet))
            End If

            If Not [Enum].IsDefined(GetType(SubmissionPacketFileRole), role) Then
                Throw New ArgumentOutOfRangeException(NameOf(role))
            End If

            If Not [Enum].IsDefined(GetType(SubmissionPacketFileStorageMode), storageMode) Then
                Throw New ArgumentOutOfRangeException(NameOf(storageMode))
            End If

            If packet.Files Is Nothing Then
                packet.Files = New List(Of SubmissionPacketFile)()
            End If

            Dim normalizedPath As String = NormalizeText(localFilePath)

            If storageMode <> SubmissionPacketFileStorageMode.MetadataOnly Then

                If String.IsNullOrWhiteSpace(normalizedPath) Then
                    Throw New ArgumentException(
                        "A linked or managed Submission Packet file requires a local file path.",
                        NameOf(localFilePath)
                    )
                End If

                normalizedPath = Path.GetFullPath(normalizedPath)

                If Not File.Exists(normalizedPath) Then
                    Throw New FileNotFoundException(
                        "The selected Submission Packet file could not be found.",
                        normalizedPath
                    )
                End If

            End If

            Dim normalizedLabel As String = NormalizeText(label)

            If String.IsNullOrWhiteSpace(normalizedLabel) Then

                If String.IsNullOrWhiteSpace(normalizedPath) Then
                    normalizedLabel = FriendlyRoleName(role)
                Else
                    normalizedLabel = Path.GetFileName(normalizedPath)
                End If

            End If

            Dim packetFile As New SubmissionPacketFile With {
                .Id = Guid.NewGuid(),
                .Role = role,
                .Label = normalizedLabel,
                .Notes = NormalizeText(notes),
                .LocalFilePath = normalizedPath,
                .StorageMode = storageMode,
                .OriginalFileName = If(String.IsNullOrWhiteSpace(normalizedPath), String.Empty, Path.GetFileName(normalizedPath))
            }

            If storageMode <> SubmissionPacketFileStorageMode.MetadataOnly Then

                Dim info As New FileInfo(normalizedPath)
                packetFile.FileSizeBytes = info.Length
                packetFile.LastWriteTimeUtc = info.LastWriteTimeUtc

            End If

            packet.Files.Add(packetFile)
            TouchPacket(packet, modifiedAtUtc)

            Return packetFile

        End Function


        Public Shared Function RemoveFile(
            packet As SubmissionPacket,
            packetFileId As Guid,
            managedLibrary As ManagedLibraryService
        ) As SubmissionPacketFile

            If packet Is Nothing Then
                Throw New ArgumentNullException(NameOf(packet))
            End If

            If managedLibrary Is Nothing Then
                Throw New ArgumentNullException(NameOf(managedLibrary))
            End If

            Dim packetFile As SubmissionPacketFile =
                RequireFile(packet, packetFileId)

            If IsCommittedManagedFile(packetFile, managedLibrary) Then
                Throw New InvalidOperationException(
                    "This file is already stored in the PaperRoute Library. Safe physical deletion is intentionally deferred to the next Packet Vault safety checkpoint; the file record was left unchanged."
                )
            End If

            packet.Files.Remove(packetFile)
            TouchPacket(packet, Nothing)

            Return packetFile

        End Function


        Public Shared Function RemovePacket(
            manuscript As Manuscript,
            packetId As Guid,
            managedLibrary As ManagedLibraryService
        ) As SubmissionPacket

            ValidateManuscript(manuscript)
            EnsureCollections(manuscript)

            If managedLibrary Is Nothing Then
                Throw New ArgumentNullException(NameOf(managedLibrary))
            End If

            Dim packet As SubmissionPacket = RequirePacket(manuscript, packetId)

            If packet.Files IsNot Nothing AndAlso
               packet.Files.Any(
                   Function(item)
                       Return item IsNot Nothing AndAlso
                           IsCommittedManagedFile(item, managedLibrary)
                   End Function
               ) Then

                Throw New InvalidOperationException(
                    "This Submission Packet contains a file already stored in the PaperRoute Library. Safe managed-packet deletion is intentionally deferred to the next Packet Vault safety checkpoint; the packet was left unchanged."
                )

            End If

            manuscript.SubmissionPackets.Remove(packet)

            Return packet

        End Function


        Public Shared Function IsCommittedManagedFile(
            packetFile As SubmissionPacketFile,
            managedLibrary As ManagedLibraryService
        ) As Boolean

            If packetFile Is Nothing OrElse managedLibrary Is Nothing Then
                Return False
            End If

            Return packetFile.StorageMode = SubmissionPacketFileStorageMode.ManagedCopy AndAlso
                Not String.IsNullOrWhiteSpace(packetFile.LocalFilePath) AndAlso
                managedLibrary.IsManagedPath(packetFile.LocalFilePath)

        End Function


        Public Shared Function FriendlyRoleName(
            role As SubmissionPacketFileRole
        ) As String

            Select Case role
                Case SubmissionPacketFileRole.BlindedManuscript
                    Return "Blinded manuscript"
                Case SubmissionPacketFileRole.TitlePage
                    Return "Title page"
                Case SubmissionPacketFileRole.CoverLetter
                    Return "Cover letter"
                Case SubmissionPacketFileRole.GraphicalAbstract
                    Return "Graphical abstract"
                Case SubmissionPacketFileRole.ReportingChecklist
                    Return "Reporting checklist"
                Case SubmissionPacketFileRole.ResponseToReviewers
                    Return "Response to reviewers"
                Case SubmissionPacketFileRole.DataAvailability
                    Return "Data availability"
                Case Else
                    Return role.ToString()
            End Select

        End Function


        Private Shared Sub ValidateManuscript(manuscript As Manuscript)

            If manuscript Is Nothing Then
                Throw New ArgumentNullException(NameOf(manuscript))
            End If

        End Sub


        Private Shared Sub EnsureCollections(manuscript As Manuscript)

            If manuscript.Versions Is Nothing Then
                manuscript.Versions = New List(Of ManuscriptVersion)()
            End If

            If manuscript.ReadinessProfiles Is Nothing Then
                manuscript.ReadinessProfiles = New List(Of ManuscriptReadiness)()
            End If

            If manuscript.SubmissionPackets Is Nothing Then
                manuscript.SubmissionPackets = New List(Of SubmissionPacket)()
            End If

            If manuscript.Submissions Is Nothing Then
                manuscript.Submissions = New List(Of JournalSubmission)()
            End If

        End Sub


        Private Shared Function RequireVersion(
            manuscript As Manuscript,
            versionId As Guid
        ) As ManuscriptVersion

            If versionId = Guid.Empty Then
                Throw New ArgumentException(
                    "A Submission Packet requires an exact manuscript Version History record.",
                    NameOf(versionId)
                )
            End If

            Dim version As ManuscriptVersion =
                manuscript.Versions.FirstOrDefault(
                    Function(item)
                        Return item IsNot Nothing AndAlso item.Id = versionId
                    End Function
                )

            If version Is Nothing Then
                Throw New ArgumentException(
                    "The selected manuscript Version History record does not exist.",
                    NameOf(versionId)
                )
            End If

            Return version

        End Function


        Private Shared Function RequirePacket(
            manuscript As Manuscript,
            packetId As Guid
        ) As SubmissionPacket

            If packetId = Guid.Empty Then
                Throw New ArgumentException(
                    "A valid Submission Packet identifier is required.",
                    NameOf(packetId)
                )
            End If

            Dim packet As SubmissionPacket =
                manuscript.SubmissionPackets.FirstOrDefault(
                    Function(item)
                        Return item IsNot Nothing AndAlso item.Id = packetId
                    End Function
                )

            If packet Is Nothing Then
                Throw New ArgumentException(
                    "The selected Submission Packet does not exist.",
                    NameOf(packetId)
                )
            End If

            Return packet

        End Function


        Private Shared Function RequireFile(
            packet As SubmissionPacket,
            packetFileId As Guid
        ) As SubmissionPacketFile

            If packetFileId = Guid.Empty Then
                Throw New ArgumentException(
                    "A valid Submission Packet file identifier is required.",
                    NameOf(packetFileId)
                )
            End If

            If packet.Files Is Nothing Then
                packet.Files = New List(Of SubmissionPacketFile)()
            End If

            Dim packetFile As SubmissionPacketFile =
                packet.Files.FirstOrDefault(
                    Function(item)
                        Return item IsNot Nothing AndAlso item.Id = packetFileId
                    End Function
                )

            If packetFile Is Nothing Then
                Throw New ArgumentException(
                    "The selected Submission Packet file does not exist.",
                    NameOf(packetFileId)
                )
            End If

            Return packetFile

        End Function


        Private Shared Function ResolveReadiness(
            manuscript As Manuscript,
            readinessProfileId As Guid?
        ) As ManuscriptReadiness

            If Not readinessProfileId.HasValue Then
                Return Nothing
            End If

            If readinessProfileId.Value = Guid.Empty Then
                Throw New ArgumentException(
                    "A readiness-profile reference cannot be empty.",
                    NameOf(readinessProfileId)
                )
            End If

            Dim readiness As ManuscriptReadiness =
                manuscript.ReadinessProfiles.FirstOrDefault(
                    Function(item)
                        Return item IsNot Nothing AndAlso item.Id = readinessProfileId.Value
                    End Function
                )

            If readiness Is Nothing Then
                Throw New ArgumentException(
                    "The selected readiness profile does not exist on this manuscript.",
                    NameOf(readinessProfileId)
                )
            End If

            Return readiness

        End Function


        Private Shared Function ResolveSubmission(
            manuscript As Manuscript,
            submissionId As Guid?
        ) As JournalSubmission

            If Not submissionId.HasValue Then
                Return Nothing
            End If

            If submissionId.Value = Guid.Empty Then
                Throw New ArgumentException(
                    "A journal-submission reference cannot be empty.",
                    NameOf(submissionId)
                )
            End If

            Dim submission As JournalSubmission =
                manuscript.Submissions.FirstOrDefault(
                    Function(item)
                        Return item IsNot Nothing AndAlso item.Id = submissionId.Value
                    End Function
                )

            If submission Is Nothing Then
                Throw New ArgumentException(
                    "The selected real journal submission does not exist on this manuscript.",
                    NameOf(submissionId)
                )
            End If

            Return submission

        End Function


        Private Shared Sub ValidateRevisionRound(
            submission As JournalSubmission,
            revisionRoundNumber As Integer?
        )

            If Not revisionRoundNumber.HasValue Then
                Return
            End If

            If revisionRoundNumber.Value <= 0 Then
                Throw New ArgumentOutOfRangeException(
                    NameOf(revisionRoundNumber),
                    "A revision-round number must be greater than zero."
                )
            End If

            If submission Is Nothing Then
                Throw New ArgumentException(
                    "A revision round can only be associated with a real journal submission."
                )
            End If

        End Sub


        Private Shared Sub ValidateJournalConsistency(
            readiness As ManuscriptReadiness,
            submission As JournalSubmission
        )

            If readiness Is Nothing OrElse submission Is Nothing Then
                Return
            End If

            If readiness.JournalId.HasValue AndAlso
               submission.JournalId.HasValue AndAlso
               readiness.JournalId.Value <> submission.JournalId.Value Then

                Throw New ArgumentException(
                    "The selected readiness profile and real journal submission refer to different reusable journals."
                )
            End If

        End Sub


        Private Shared Function ResolveJournalId(
            manuscript As Manuscript,
            readiness As ManuscriptReadiness,
            submission As JournalSubmission
        ) As Guid?

            If readiness IsNot Nothing AndAlso readiness.JournalId.HasValue Then
                Return readiness.JournalId
            End If

            If submission IsNot Nothing AndAlso submission.JournalId.HasValue Then
                Return submission.JournalId
            End If

            Return manuscript.TargetJournalId

        End Function


        Private Shared Function ResolveJournalName(
            manuscript As Manuscript,
            readiness As ManuscriptReadiness,
            submission As JournalSubmission
        ) As String

            If readiness IsNot Nothing AndAlso
               Not String.IsNullOrWhiteSpace(readiness.JournalName) Then
                Return readiness.JournalName.Trim()
            End If

            If submission IsNot Nothing AndAlso
               Not String.IsNullOrWhiteSpace(submission.JournalName) Then
                Return submission.JournalName.Trim()
            End If

            Return NormalizeText(manuscript.TargetJournal)

        End Function


        Private Shared Function NormalizePacketLabel(value As String) As String

            Dim normalized As String = NormalizeText(value)

            If String.IsNullOrWhiteSpace(normalized) Then
                Return "Submission packet"
            End If

            Return normalized

        End Function


        Private Shared Function NormalizeText(value As String) As String
            Return If(value, String.Empty).Trim()
        End Function


        Private Shared Sub TouchPacket(
            packet As SubmissionPacket,
            modifiedAtUtc As DateTime?
        )

            packet.LastModifiedAtUtc =
                If(modifiedAtUtc.HasValue, modifiedAtUtc.Value, DateTime.UtcNow)

        End Sub


        Private Shared Function NullableGuidEquals(left As Guid?, right As Guid?) As Boolean

            If left.HasValue <> right.HasValue Then
                Return False
            End If

            If Not left.HasValue Then
                Return True
            End If

            Return left.Value = right.Value

        End Function


        Private Shared Function NullableIntegerEquals(left As Integer?, right As Integer?) As Boolean

            If left.HasValue <> right.HasValue Then
                Return False
            End If

            If Not left.HasValue Then
                Return True
            End If

            Return left.Value = right.Value

        End Function

    End Class

End Namespace
