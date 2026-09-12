Imports System
Imports System.Collections.Generic
Imports ManuscriptPipeline.Models

Namespace Services

    Public NotInheritable Class ManuscriptVersionService

        Private Sub New()
        End Sub

        Public Shared Function CreateVersion(
            manuscript As Manuscript,
            label As String,
            notes As String,
            localFilePath As String,
            isManagedCopy As Boolean,
            Optional submissionId As Guid? = Nothing,
            Optional decisionId As Guid? = Nothing,
            Optional revisionRoundNumber As Integer? = Nothing,
            Optional makeCurrent As Boolean = True,
            Optional createdDate As DateTime? = Nothing
        ) As ManuscriptVersion

            ValidateManuscript(manuscript)
            EnsureCollections(manuscript)
            ValidateRevisionRound(revisionRoundNumber)

            If isManagedCopy AndAlso String.IsNullOrWhiteSpace(localFilePath) Then
                Throw New ArgumentException(
                    "A managed manuscript version requires a file path.",
                    NameOf(localFilePath)
                )
            End If

            Dim resolvedSubmissionId As Guid? =
                ResolveSubmissionForLinks(
                    manuscript,
                    submissionId,
                    decisionId
                )

            Dim version As New ManuscriptVersion With {
                .Id = Guid.NewGuid(),
                .CreatedDate = If(
                    createdDate.HasValue,
                    createdDate.Value,
                    DateTime.Now
                ),
                .Label = NormalizeText(label),
                .Notes = NormalizeText(notes),
                .LocalFilePath = NormalizeText(localFilePath),
                .IsManagedCopy = isManagedCopy,
                .SubmissionId = resolvedSubmissionId,
                .DecisionId = decisionId,
                .RevisionRoundNumber = revisionRoundNumber
            }

            ChronologyProvenanceService.StampCreated(version)

            manuscript.Versions.Add(version)

            If makeCurrent Then
                manuscript.CurrentVersionId = version.Id
            End If

            Return version

        End Function

        Public Shared Function UpdateVersion(
            manuscript As Manuscript,
            versionId As Guid,
            label As String,
            notes As String,
            createdDate As DateTime,
            Optional submissionId As Guid? = Nothing,
            Optional decisionId As Guid? = Nothing,
            Optional revisionRoundNumber As Integer? = Nothing
        ) As Boolean

            ValidateManuscript(manuscript)
            EnsureCollections(manuscript)

            Dim version As ManuscriptVersion =
                RequireVersion(
                    manuscript,
                    versionId
                )

            Return UpdateVersion(
                manuscript,
                versionId,
                label,
                notes,
                createdDate,
                version.LocalFilePath,
                version.IsManagedCopy,
                submissionId,
                decisionId,
                revisionRoundNumber
            )

        End Function


        Public Shared Function UpdateVersion(
            manuscript As Manuscript,
            versionId As Guid,
            label As String,
            notes As String,
            createdDate As DateTime,
            localFilePath As String,
            isManagedCopy As Boolean,
            Optional submissionId As Guid? = Nothing,
            Optional decisionId As Guid? = Nothing,
            Optional revisionRoundNumber As Integer? = Nothing
        ) As Boolean

            ValidateManuscript(manuscript)
            EnsureCollections(manuscript)
            ValidateRevisionRound(revisionRoundNumber)

            Dim version As ManuscriptVersion =
                RequireVersion(manuscript, versionId)

            Dim normalizedFilePath As String =
                NormalizeText(localFilePath)

            If isManagedCopy AndAlso
               String.IsNullOrWhiteSpace(
                   normalizedFilePath
               ) Then

                Throw New ArgumentException(
                    "A managed manuscript version requires a file path.",
                    NameOf(localFilePath)
                )

            End If

            If version.IsManagedCopy AndAlso
               Not String.IsNullOrWhiteSpace(
                   version.LocalFilePath
               ) Then

                Dim managedLibrary As New ManagedLibraryService()

                If managedLibrary.IsManagedPath(
                    version.LocalFilePath
                ) AndAlso
                   (
                       Not isManagedCopy OrElse
                       Not String.Equals(
                           version.LocalFilePath,
                           normalizedFilePath,
                           StringComparison.OrdinalIgnoreCase
                       )
                   ) Then

                    Throw New InvalidOperationException(
                        "A PaperRoute Library manuscript snapshot cannot be replaced or detached. Create a new version instead."
                    )

                End If

            End If

            Dim resolvedSubmissionId As Guid? =
                ResolveSubmissionForUpdate(
                    manuscript,
                    version,
                    submissionId,
                    decisionId
                )

            Dim normalizedLabel As String =
                NormalizeText(label)

            Dim normalizedNotes As String =
                NormalizeText(notes)

            Dim changed As Boolean =
                Not String.Equals(
                    version.Label,
                    normalizedLabel,
                    StringComparison.Ordinal
                ) OrElse
                Not String.Equals(
                    version.Notes,
                    normalizedNotes,
                    StringComparison.Ordinal
                ) OrElse
                version.CreatedDate <> createdDate OrElse
                Not String.Equals(
                    version.LocalFilePath,
                    normalizedFilePath,
                    StringComparison.OrdinalIgnoreCase
                ) OrElse
                version.IsManagedCopy <> isManagedCopy OrElse
                Not NullableGuidEquals(
                    version.SubmissionId,
                    resolvedSubmissionId
                ) OrElse
                Not NullableGuidEquals(
                    version.DecisionId,
                    decisionId
                ) OrElse
                Not NullableIntegerEquals(
                    version.RevisionRoundNumber,
                    revisionRoundNumber
                )

            If Not changed Then
                Return False
            End If

            version.Label =
                normalizedLabel

            version.Notes =
                normalizedNotes

            version.CreatedDate =
                createdDate

            version.LocalFilePath =
                normalizedFilePath

            version.IsManagedCopy =
                isManagedCopy

            version.SubmissionId =
                resolvedSubmissionId

            version.DecisionId =
                decisionId

            version.RevisionRoundNumber =
                revisionRoundNumber

            ChronologyProvenanceService.StampModified(
                version
            )

            Return True

        End Function


        Public Shared Sub SetCurrentVersion(
            manuscript As Manuscript,
            versionId As Guid
        )

            ValidateManuscript(manuscript)
            EnsureCollections(manuscript)
            ValidateGuidReference(versionId, NameOf(versionId))

            Dim version As ManuscriptVersion =
                FindVersion(manuscript, versionId)

            If version Is Nothing Then
                Throw New ArgumentException(
                    "The selected manuscript version does not exist.",
                    NameOf(versionId)
                )
            End If

            manuscript.CurrentVersionId = version.Id

        End Sub


        Public Shared Function DeleteVersion(
            manuscript As Manuscript,
            versionId As Guid
        ) As ManuscriptVersion

            ValidateManuscript(
                manuscript
            )

            EnsureCollections(
                manuscript
            )

            Dim version As ManuscriptVersion =
                RequireVersion(
                    manuscript,
                    versionId
                )

            If manuscript.SubmissionPackets IsNot Nothing Then

                For Each packet As SubmissionPacket In manuscript.SubmissionPackets

                    If packet IsNot Nothing AndAlso
                       packet.ManuscriptVersionId = version.Id Then

                        Throw New InvalidOperationException(
                            "This version is used by a Submission Packet. Choose another exact version for that packet or delete the packet before deleting this version."
                        )

                    End If

                Next

            End If

            Dim wasCurrent As Boolean =
                manuscript.CurrentVersionId.HasValue AndAlso
                manuscript.CurrentVersionId.Value =
                version.Id

            manuscript.Versions.Remove(
                version
            )

            If wasCurrent Then

                Dim remaining As List(Of ManuscriptVersion) =
                    GetChronologicalVersions(
                        manuscript
                    )

                If remaining.Count = 0 Then

                    manuscript.CurrentVersionId =
                        Nothing

                Else

                    manuscript.CurrentVersionId =
                        remaining(
                            remaining.Count - 1
                        ).Id

                End If

            End If

            Return version

        End Function


        Public Shared Sub LinkVersionToSubmission(
            manuscript As Manuscript,
            versionId As Guid,
            submissionId As Guid
        )

            ValidateManuscript(manuscript)
            EnsureCollections(manuscript)

            Dim version As ManuscriptVersion =
                RequireVersion(manuscript, versionId)

            ValidateGuidReference(
                submissionId,
                NameOf(submissionId)
            )

            Dim submission As JournalSubmission =
                FindSubmission(manuscript, submissionId)

            If submission Is Nothing Then
                Throw New ArgumentException(
                    "The selected journal submission does not exist on this manuscript.",
                    NameOf(submissionId)
                )
            End If

            If version.DecisionId.HasValue Then
                Dim decisionSubmission As JournalSubmission =
                    FindSubmissionForDecision(
                        manuscript,
                        version.DecisionId.Value
                    )

                If decisionSubmission IsNot Nothing AndAlso
                   decisionSubmission.Id <> submission.Id Then

                    Throw New ArgumentException(
                        "This manuscript version is already linked to a decision from another journal submission.",
                        NameOf(submissionId)
                    )
                End If
            End If

            If version.SubmissionId.HasValue AndAlso
               version.SubmissionId.Value = submission.Id Then
                Return
            End If

            version.SubmissionId = submission.Id
            ChronologyProvenanceService.StampModified(version)

        End Sub

        Public Shared Sub LinkVersionToDecision(
            manuscript As Manuscript,
            versionId As Guid,
            decisionId As Guid,
            Optional revisionRoundNumber As Integer? = Nothing
        )

            ValidateManuscript(manuscript)
            EnsureCollections(manuscript)

            Dim version As ManuscriptVersion =
                RequireVersion(manuscript, versionId)

            ValidateGuidReference(decisionId, NameOf(decisionId))
            ValidateRevisionRound(revisionRoundNumber)

            Dim submission As JournalSubmission =
                FindSubmissionForDecision(manuscript, decisionId)

            If submission Is Nothing Then
                Throw New ArgumentException(
                    "The selected editorial decision does not exist on this manuscript.",
                    NameOf(decisionId)
                )
            End If

            Dim changed As Boolean =
                Not version.SubmissionId.HasValue OrElse
                version.SubmissionId.Value <> submission.Id OrElse
                Not version.DecisionId.HasValue OrElse
                version.DecisionId.Value <> decisionId OrElse
                (
                    revisionRoundNumber.HasValue AndAlso
                    Not NullableIntegerEquals(
                        version.RevisionRoundNumber,
                        revisionRoundNumber
                    )
                )

            If Not changed Then
                Return
            End If

            version.SubmissionId = submission.Id
            version.DecisionId = decisionId

            If revisionRoundNumber.HasValue Then
                version.RevisionRoundNumber = revisionRoundNumber
            End If

            ChronologyProvenanceService.StampModified(version)

        End Sub

        Public Shared Function GetChronologicalVersions(
            manuscript As Manuscript
        ) As List(Of ManuscriptVersion)

            ValidateManuscript(manuscript)
            EnsureCollections(manuscript)

            Dim result As New List(Of ManuscriptVersion)()
            Dim sourcePositions As New Dictionary(
                Of ManuscriptVersion,
                Integer
            )()

            For index As Integer = 0 To manuscript.Versions.Count - 1
                Dim version As ManuscriptVersion =
                    manuscript.Versions(index)

                If version Is Nothing Then
                    Continue For
                End If

                If Not sourcePositions.ContainsKey(version) Then
                    sourcePositions.Add(version, index)
                End If

                result.Add(version)
            Next

            result.Sort(
                Function(
                    left As ManuscriptVersion,
                    right As ManuscriptVersion
                ) As Integer

                    Dim dateComparison As Integer =
                        DateTime.Compare(
                            left.CreatedDate.Date,
                            right.CreatedDate.Date
                        )

                    If dateComparison <> 0 Then
                        Return dateComparison
                    End If

                    If left.RecordedAtUtc.HasValue AndAlso
                       right.RecordedAtUtc.HasValue Then

                        Dim recordedComparison As Integer =
                            DateTime.Compare(
                                left.RecordedAtUtc.Value,
                                right.RecordedAtUtc.Value
                            )

                        If recordedComparison <> 0 Then
                            Return recordedComparison
                        End If
                    End If

                    Dim sourceComparison As Integer =
                        sourcePositions(left).CompareTo(
                            sourcePositions(right)
                        )

                    If sourceComparison <> 0 Then
                        Return sourceComparison
                    End If

                    Return StringComparer.Ordinal.Compare(
                        left.Id.ToString("N"),
                        right.Id.ToString("N")
                    )
                End Function
            )

            Return result

        End Function

        Private Shared Function ResolveSubmissionForUpdate(
            manuscript As Manuscript,
            version As ManuscriptVersion,
            submissionId As Guid?,
            decisionId As Guid?
        ) As Guid?

            ' Historical imported records may contain unresolved non-empty
            ' references. Metadata edits must preserve those references when
            ' the user did not change the association itself.
            If NullableGuidEquals(
                version.SubmissionId,
                submissionId
            ) AndAlso
               NullableGuidEquals(
                   version.DecisionId,
                   decisionId
               ) Then

                Return submissionId

            End If

            Return ResolveSubmissionForLinks(
                manuscript,
                submissionId,
                decisionId
            )

        End Function


        Private Shared Function ResolveSubmissionForLinks(
            manuscript As Manuscript,
            submissionId As Guid?,
            decisionId As Guid?
        ) As Guid?

            Dim resolvedSubmissionId As Guid? = submissionId

            If submissionId.HasValue Then
                ValidateGuidReference(
                    submissionId.Value,
                    NameOf(submissionId)
                )

                If FindSubmission(
                    manuscript,
                    submissionId.Value
                ) Is Nothing Then

                    Throw New ArgumentException(
                        "The selected journal submission does not exist on this manuscript.",
                        NameOf(submissionId)
                    )
                End If
            End If

            If decisionId.HasValue Then
                ValidateGuidReference(
                    decisionId.Value,
                    NameOf(decisionId)
                )

                Dim decisionSubmission As JournalSubmission =
                    FindSubmissionForDecision(
                        manuscript,
                        decisionId.Value
                    )

                If decisionSubmission Is Nothing Then
                    Throw New ArgumentException(
                        "The selected editorial decision does not exist on this manuscript.",
                        NameOf(decisionId)
                    )
                End If

                If resolvedSubmissionId.HasValue AndAlso
                   resolvedSubmissionId.Value <> decisionSubmission.Id Then

                    Throw New ArgumentException(
                        "The selected editorial decision does not belong to the selected journal submission.",
                        NameOf(decisionId)
                    )
                End If

                resolvedSubmissionId = decisionSubmission.Id
            End If

            Return resolvedSubmissionId

        End Function

        Private Shared Function NullableGuidEquals(
            left As Guid?,
            right As Guid?
        ) As Boolean

            If left.HasValue <> right.HasValue Then
                Return False
            End If

            If Not left.HasValue Then
                Return True
            End If

            Return left.Value = right.Value

        End Function

        Private Shared Function NullableIntegerEquals(
            left As Integer?,
            right As Integer?
        ) As Boolean

            If left.HasValue <> right.HasValue Then
                Return False
            End If

            If Not left.HasValue Then
                Return True
            End If

            Return left.Value = right.Value

        End Function


        Private Shared Function NormalizeText(
            value As String
        ) As String

            Return If(value, String.Empty).Trim()

        End Function

        Private Shared Sub ValidateManuscript(
            manuscript As Manuscript
        )

            If manuscript Is Nothing Then
                Throw New ArgumentNullException(NameOf(manuscript))
            End If

        End Sub

        Private Shared Sub EnsureCollections(
            manuscript As Manuscript
        )

            If manuscript.Versions Is Nothing Then
                manuscript.Versions =
                    New List(Of ManuscriptVersion)()
            End If

            If manuscript.Submissions Is Nothing Then
                manuscript.Submissions =
                    New List(Of JournalSubmission)()
            End If

        End Sub

        Private Shared Sub ValidateGuidReference(
            value As Guid,
            parameterName As String
        )

            If value = Guid.Empty Then
                Throw New ArgumentException(
                    "A valid identifier is required.",
                    parameterName
                )
            End If

        End Sub

        Private Shared Sub ValidateRevisionRound(
            revisionRoundNumber As Integer?
        )

            If revisionRoundNumber.HasValue AndAlso
               revisionRoundNumber.Value <= 0 Then

                Throw New ArgumentOutOfRangeException(
                    NameOf(revisionRoundNumber),
                    "A revision-round number must be greater than zero."
                )
            End If

        End Sub

        Private Shared Function RequireVersion(
            manuscript As Manuscript,
            versionId As Guid
        ) As ManuscriptVersion

            ValidateGuidReference(versionId, NameOf(versionId))

            Dim version As ManuscriptVersion =
                FindVersion(manuscript, versionId)

            If version Is Nothing Then
                Throw New ArgumentException(
                    "The selected manuscript version does not exist.",
                    NameOf(versionId)
                )
            End If

            Return version

        End Function

        Private Shared Function FindVersion(
            manuscript As Manuscript,
            versionId As Guid
        ) As ManuscriptVersion

            For Each version As ManuscriptVersion In manuscript.Versions
                If version IsNot Nothing AndAlso
                   version.Id = versionId Then
                    Return version
                End If
            Next

            Return Nothing

        End Function

        Private Shared Function FindSubmission(
            manuscript As Manuscript,
            submissionId As Guid
        ) As JournalSubmission

            For Each submission As JournalSubmission In manuscript.Submissions
                If submission IsNot Nothing AndAlso
                   submission.Id = submissionId Then
                    Return submission
                End If
            Next

            Return Nothing

        End Function

        Private Shared Function FindSubmissionForDecision(
            manuscript As Manuscript,
            decisionId As Guid
        ) As JournalSubmission

            For Each submission As JournalSubmission In manuscript.Submissions
                If submission Is Nothing OrElse
                   submission.Decisions Is Nothing Then
                    Continue For
                End If

                For Each decision As EditorialDecisionEvent In submission.Decisions
                    If decision IsNot Nothing AndAlso
                       decision.Id = decisionId Then
                        Return submission
                    End If
                Next
            Next

            Return Nothing

        End Function

    End Class

End Namespace
