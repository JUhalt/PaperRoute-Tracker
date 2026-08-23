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

            ValidateManuscript(
                manuscript
            )

            EnsureCollections(
                manuscript
            )

            ValidateRevisionRound(
                revisionRoundNumber
            )

            If isManagedCopy AndAlso
               String.IsNullOrWhiteSpace(localFilePath) Then

                Throw New ArgumentException(
                    "A managed manuscript version requires a file path.",
                    NameOf(localFilePath)
                )

            End If

            Dim resolvedSubmissionId As Guid? =
                submissionId

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
                   resolvedSubmissionId.Value <>
                   decisionSubmission.Id Then

                    Throw New ArgumentException(
                        "The selected editorial decision does not belong to the selected journal submission.",
                        NameOf(decisionId)
                    )

                End If

                resolvedSubmissionId =
                    decisionSubmission.Id

            End If

            Dim version As New ManuscriptVersion With {
                .Id = Guid.NewGuid(),
                .CreatedDate = If(
                    createdDate.HasValue,
                    createdDate.Value,
                    DateTime.Now
                ),
                .Label = If(
                    label,
                    String.Empty
                ),
                .Notes = If(
                    notes,
                    String.Empty
                ),
                .LocalFilePath = If(
                    localFilePath,
                    String.Empty
                ),
                .IsManagedCopy = isManagedCopy,
                .SubmissionId = resolvedSubmissionId,
                .DecisionId = decisionId,
                .RevisionRoundNumber = revisionRoundNumber
            }

            ChronologyProvenanceService.StampCreated(
                version
            )

            manuscript.Versions.Add(
                version
            )

            If makeCurrent Then

                manuscript.CurrentVersionId =
                    version.Id

            End If

            Return version

        End Function


        Public Shared Sub SetCurrentVersion(
            manuscript As Manuscript,
            versionId As Guid
        )

            ValidateManuscript(
                manuscript
            )

            EnsureCollections(
                manuscript
            )

            ValidateGuidReference(
                versionId,
                NameOf(versionId)
            )

            Dim version As ManuscriptVersion =
                FindVersion(
                    manuscript,
                    versionId
                )

            If version Is Nothing Then

                Throw New ArgumentException(
                    "The selected manuscript version does not exist.",
                    NameOf(versionId)
                )

            End If

            manuscript.CurrentVersionId =
                version.Id

        End Sub


        Public Shared Sub LinkVersionToSubmission(
            manuscript As Manuscript,
            versionId As Guid,
            submissionId As Guid
        )

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

            ValidateGuidReference(
                submissionId,
                NameOf(submissionId)
            )

            Dim submission As JournalSubmission =
                FindSubmission(
                    manuscript,
                    submissionId
                )

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

            version.SubmissionId =
                submission.Id

            ChronologyProvenanceService.StampModified(
                version
            )

        End Sub


        Public Shared Sub LinkVersionToDecision(
            manuscript As Manuscript,
            versionId As Guid,
            decisionId As Guid,
            Optional revisionRoundNumber As Integer? = Nothing
        )

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

            ValidateGuidReference(
                decisionId,
                NameOf(decisionId)
            )

            ValidateRevisionRound(
                revisionRoundNumber
            )

            Dim submission As JournalSubmission =
                FindSubmissionForDecision(
                    manuscript,
                    decisionId
                )

            If submission Is Nothing Then

                Throw New ArgumentException(
                    "The selected editorial decision does not exist on this manuscript.",
                    NameOf(decisionId)
                )

            End If

            version.SubmissionId =
                submission.Id

            version.DecisionId =
                decisionId

            If revisionRoundNumber.HasValue Then

                version.RevisionRoundNumber =
                    revisionRoundNumber

            End If

            ChronologyProvenanceService.StampModified(
                version
            )

        End Sub


        Private Shared Sub ValidateManuscript(
            manuscript As Manuscript
        )

            If manuscript Is Nothing Then

                Throw New ArgumentNullException(
                    NameOf(manuscript)
                )

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

            ValidateGuidReference(
                versionId,
                NameOf(versionId)
            )

            Dim version As ManuscriptVersion =
                FindVersion(
                    manuscript,
                    versionId
                )

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
