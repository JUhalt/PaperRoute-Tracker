Imports System
Imports System.Collections.Generic
Imports System.IO
Imports ManuscriptPipeline.Models

Namespace Services

    Public NotInheritable Class SubmissionReadinessValidationService

        Private Sub New()
        End Sub


        Public Shared Sub NormalizeAndValidateManuscript(
            manuscript As Manuscript
        )

            If manuscript Is Nothing Then
                Throw New ArgumentNullException(NameOf(manuscript))
            End If

            If manuscript.ReadinessProfiles Is Nothing Then
                manuscript.ReadinessProfiles =
                    New List(Of ManuscriptReadiness)()
            End If

            If manuscript.SubmissionPackets Is Nothing Then
                manuscript.SubmissionPackets =
                    New List(Of SubmissionPacket)()
            End If

            Dim readinessById As New Dictionary(
                Of Guid,
                ManuscriptReadiness
            )()

            For Each readiness As ManuscriptReadiness In manuscript.ReadinessProfiles

                If readiness Is Nothing Then
                    Throw New InvalidDataException(
                        "The manuscript library contains a null readiness profile."
                    )
                End If

                If readiness.Id = Guid.Empty OrElse
                   readinessById.ContainsKey(readiness.Id) Then

                    Throw New InvalidDataException(
                        "The manuscript library contains invalid or duplicate readiness-profile identifiers."
                    )

                End If

                readinessById.Add(
                    readiness.Id,
                    readiness
                )

                If readiness.JournalId.HasValue AndAlso
                   readiness.JournalId.Value = Guid.Empty Then

                    Throw New InvalidDataException(
                        "A manuscript readiness profile contains an invalid journal reference."
                    )

                End If

                readiness.JournalName =
                    If(
                        readiness.JournalName,
                        String.Empty
                    )

                readiness.Notes =
                    If(
                        readiness.Notes,
                        String.Empty
                    )

                If readiness.Items Is Nothing Then
                    readiness.Items =
                        New List(Of ReadinessItemState)()
                End If

                Dim readinessItemIds As New HashSet(Of Guid)()
                Dim templateItemIds As New HashSet(Of Guid)()

                For Each item As ReadinessItemState In readiness.Items

                    If item Is Nothing Then
                        Throw New InvalidDataException(
                            "A manuscript readiness profile contains a null checklist item."
                        )
                    End If

                    If item.Id = Guid.Empty OrElse
                       Not readinessItemIds.Add(item.Id) Then

                        Throw New InvalidDataException(
                            "A manuscript readiness profile contains invalid or duplicate checklist-item identifiers."
                        )

                    End If

                    If item.TemplateItemId.HasValue Then

                        If item.TemplateItemId.Value = Guid.Empty Then
                            Throw New InvalidDataException(
                                "A manuscript readiness item contains an invalid reusable-template reference."
                            )
                        End If

                        If Not templateItemIds.Add(item.TemplateItemId.Value) Then
                            Throw New InvalidDataException(
                                "A manuscript readiness profile contains the same reusable-template item more than once."
                            )
                        End If

                    End If

                    If item.SortOrder < 0 Then
                        Throw New InvalidDataException(
                            "A manuscript readiness item contains a negative sort order."
                        )
                    End If

                    If Not [Enum].IsDefined(
                        GetType(ReadinessItemStatus),
                        item.Status
                    ) Then

                        Throw New InvalidDataException(
                            "A manuscript readiness item contains an unknown status."
                        )

                    End If

                    item.Title =
                        If(
                            item.Title,
                            String.Empty
                        )

                    item.Description =
                        If(
                            item.Description,
                            String.Empty
                        )

                    item.Category =
                        If(
                            item.Category,
                            String.Empty
                        )

                    item.UserNotes =
                        If(
                            item.UserNotes,
                            String.Empty
                        )

                Next

            Next

            Dim versionIds As New HashSet(Of Guid)()

            If manuscript.Versions IsNot Nothing Then

                For Each version As ManuscriptVersion In manuscript.Versions

                    If version IsNot Nothing AndAlso
                       version.Id <> Guid.Empty Then

                        versionIds.Add(
                            version.Id
                        )

                    End If

                Next

            End If

            Dim submissionsById As New Dictionary(Of Guid, JournalSubmission)()

            If manuscript.Submissions IsNot Nothing Then

                For Each submission As JournalSubmission In manuscript.Submissions

                    If submission IsNot Nothing AndAlso
                       submission.Id <> Guid.Empty Then

                        submissionsById.TryAdd(submission.Id, submission)

                    End If

                Next

            End If

            Dim packetIds As New HashSet(Of Guid)()

            For Each packet As SubmissionPacket In manuscript.SubmissionPackets

                If packet Is Nothing Then
                    Throw New InvalidDataException(
                        "The manuscript library contains a null Submission Packet."
                    )
                End If

                If packet.Id = Guid.Empty OrElse
                   Not packetIds.Add(packet.Id) Then

                    Throw New InvalidDataException(
                        "The manuscript library contains invalid or duplicate Submission Packet identifiers."
                    )

                End If

                Dim linkedReadiness As ManuscriptReadiness =
                    Nothing

                If packet.ReadinessProfileId.HasValue Then

                    If packet.ReadinessProfileId.Value = Guid.Empty OrElse
                       Not readinessById.TryGetValue(
                           packet.ReadinessProfileId.Value,
                           linkedReadiness
                       ) Then

                        Throw New InvalidDataException(
                            "A Submission Packet references a readiness profile that is not present on the manuscript."
                        )

                    End If

                End If

                If packet.JournalId.HasValue AndAlso
                   packet.JournalId.Value = Guid.Empty Then

                    Throw New InvalidDataException(
                        "A Submission Packet contains an invalid journal reference."
                    )

                End If

                If linkedReadiness IsNot Nothing AndAlso
                   linkedReadiness.JournalId.HasValue AndAlso
                   packet.JournalId.HasValue AndAlso
                   linkedReadiness.JournalId.Value <> packet.JournalId.Value Then

                    Throw New InvalidDataException(
                        "A Submission Packet and its readiness profile reference different journals."
                    )

                End If

                If packet.ManuscriptVersionId = Guid.Empty OrElse
                   Not versionIds.Contains(
                       packet.ManuscriptVersionId
                   ) Then

                    Throw New InvalidDataException(
                        "A Submission Packet must reference an exact manuscript version that exists in Version History."
                    )

                End If

                If packet.SubmissionId.HasValue Then

                    If packet.SubmissionId.Value = Guid.Empty OrElse
                       Not submissionsById.ContainsKey(
                           packet.SubmissionId.Value
                       ) Then

                        Throw New InvalidDataException(
                            "A Submission Packet references a journal submission that is not present on the manuscript."
                        )

                    End If

                    ValidateLinkedSubmissionJournal(
                        packet,
                        linkedReadiness,
                        submissionsById(packet.SubmissionId.Value)
                    )

                End If

                If packet.RevisionRoundNumber.HasValue Then

                    If packet.RevisionRoundNumber.Value <= 0 Then
                        Throw New InvalidDataException(
                            "A Submission Packet contains an invalid revision-round number."
                        )
                    End If

                    If Not packet.SubmissionId.HasValue Then
                        Throw New InvalidDataException(
                            "A Submission Packet cannot identify a revision round without identifying the real journal submission it belongs to."
                        )
                    End If

                End If

                packet.JournalName =
                    If(
                        packet.JournalName,
                        String.Empty
                    )

                packet.Label =
                    If(
                        packet.Label,
                        String.Empty
                    )

                packet.Notes =
                    If(
                        packet.Notes,
                        String.Empty
                    )

                If packet.Files Is Nothing Then
                    packet.Files =
                        New List(Of SubmissionPacketFile)()
                End If

                Dim packetFileIds As New HashSet(Of Guid)()

                For Each packetFile As SubmissionPacketFile In packet.Files

                    If packetFile Is Nothing Then
                        Throw New InvalidDataException(
                            "A Submission Packet contains a null file record."
                        )
                    End If

                    If packetFile.Id = Guid.Empty OrElse
                       Not packetFileIds.Add(packetFile.Id) Then

                        Throw New InvalidDataException(
                            "A Submission Packet contains invalid or duplicate file identifiers."
                        )

                    End If

                    If Not [Enum].IsDefined(
                        GetType(SubmissionPacketFileRole),
                        packetFile.Role
                    ) Then

                        Throw New InvalidDataException(
                            "A Submission Packet file contains an unknown file role."
                        )

                    End If

                    If Not [Enum].IsDefined(
                        GetType(SubmissionPacketFileStorageMode),
                        packetFile.StorageMode
                    ) Then

                        Throw New InvalidDataException(
                            "A Submission Packet file contains an unknown storage mode."
                        )

                    End If

                    packetFile.Label =
                        If(
                            packetFile.Label,
                            String.Empty
                        )

                    packetFile.Notes =
                        If(
                            packetFile.Notes,
                            String.Empty
                        )

                    packetFile.LocalFilePath =
                        If(
                            packetFile.LocalFilePath,
                            String.Empty
                        )

                    packetFile.OriginalFileName =
                        If(
                            packetFile.OriginalFileName,
                            String.Empty
                        )

                    packetFile.Sha256 =
                        If(
                            packetFile.Sha256,
                            String.Empty
                        ).Trim()

                    If packetFile.Sha256.Length > 0 AndAlso
                       Not IsValidSha256(
                           packetFile.Sha256
                       ) Then

                        Throw New InvalidDataException(
                            "A Submission Packet file contains invalid SHA-256 metadata."
                        )

                    End If

                    If packetFile.FileSizeBytes.HasValue AndAlso
                       packetFile.FileSizeBytes.Value < 0 Then

                        Throw New InvalidDataException(
                            "A Submission Packet file contains a negative file size."
                        )

                    End If

                Next

            Next

        End Sub


        ' Preflight a proposed submission edit before the caller replaces the
        ' working record or reconciles lifecycle state. This performs no mutation.
        Public Shared Sub ValidateSubmissionJournalAssociations(
            manuscript As Manuscript,
            proposedSubmission As JournalSubmission
        )

            If manuscript Is Nothing Then Throw New ArgumentNullException(NameOf(manuscript))
            If proposedSubmission Is Nothing Then Throw New ArgumentNullException(NameOf(proposedSubmission))
            If manuscript.SubmissionPackets Is Nothing Then Return

            For Each packet As SubmissionPacket In manuscript.SubmissionPackets
                If packet Is Nothing OrElse
                   Not packet.SubmissionId.HasValue OrElse
                   packet.SubmissionId.Value <> proposedSubmission.Id Then
                    Continue For
                End If

                Dim linkedReadiness As ManuscriptReadiness = Nothing
                If packet.ReadinessProfileId.HasValue AndAlso manuscript.ReadinessProfiles IsNot Nothing Then
                    For Each readiness As ManuscriptReadiness In manuscript.ReadinessProfiles
                        If readiness IsNot Nothing AndAlso readiness.Id = packet.ReadinessProfileId.Value Then
                            linkedReadiness = readiness
                            Exit For
                        End If
                    Next
                End If

                ValidateLinkedSubmissionJournal(packet, linkedReadiness, proposedSubmission)
            Next

        End Sub


        Private Shared Sub ValidateLinkedSubmissionJournal(
            packet As SubmissionPacket,
            readiness As ManuscriptReadiness,
            submission As JournalSubmission
        )

            If KnownJournalIdsConflict(packet.JournalId, submission.JournalId) OrElse
               (readiness IsNot Nothing AndAlso
                KnownJournalIdsConflict(readiness.JournalId, submission.JournalId)) Then

                Throw New InvalidDataException(
                    "This submission's reusable journal conflicts with the linked Submission Packet '" &
                    packet.Label & "' or its readiness profile. Open Submission Packets and unlink or " &
                    "reassign the packet before changing this submission's journal."
                )
            End If

        End Sub


        Private Shared Function KnownJournalIdsConflict(left As Guid?, right As Guid?) As Boolean
            ' Names are historical snapshots; missing IDs are not evidence of a
            ' conflict and must not be inferred from names or the current target.
            Return left.HasValue AndAlso left.Value <> Guid.Empty AndAlso
                right.HasValue AndAlso right.Value <> Guid.Empty AndAlso left.Value <> right.Value
        End Function


        Public Shared Sub NormalizeAndValidateJournal(
            journal As JournalRecord
        )

            If journal Is Nothing Then
                Throw New ArgumentNullException(NameOf(journal))
            End If

            If journal.ReadinessChecklistTemplate Is Nothing Then
                journal.ReadinessChecklistTemplate =
                    New List(Of JournalChecklistTemplateItem)()
            End If

            Dim templateIds As New HashSet(Of Guid)()

            For Each item As JournalChecklistTemplateItem In
                journal.ReadinessChecklistTemplate

                If item Is Nothing Then
                    Throw New InvalidDataException(
                        "A reusable journal readiness template contains a null checklist item."
                    )
                End If

                If item.Id = Guid.Empty OrElse
                   Not templateIds.Add(item.Id) Then

                    Throw New InvalidDataException(
                        "A reusable journal readiness template contains invalid or duplicate checklist-item identifiers."
                    )

                End If

                If item.SortOrder < 0 Then
                    Throw New InvalidDataException(
                        "A reusable journal readiness template contains a negative sort order."
                    )
                End If

                item.Title =
                    If(
                        item.Title,
                        String.Empty
                    )

                item.Description =
                    If(
                        item.Description,
                        String.Empty
                    )

                item.Category =
                    If(
                        item.Category,
                        String.Empty
                    )

            Next

        End Sub


        Private Shared Function IsValidSha256(
            value As String
        ) As Boolean

            If value Is Nothing OrElse
               value.Length <> 64 Then

                Return False

            End If

            For Each character As Char In value

                If Not Uri.IsHexDigit(character) Then
                    Return False
                End If

            Next

            Return True

        End Function

    End Class

End Namespace
