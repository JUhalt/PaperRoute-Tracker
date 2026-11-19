Imports System
Imports ManuscriptPipeline.Models

Namespace Services

    Public Enum ManuscriptStageWorkflowRequirement
        None
        ActiveSubmission
        RevisionDecision
        AcceptanceDecision
    End Enum


    Public NotInheritable Class ManuscriptStagePolicyService

        Private Sub New()
        End Sub


        Public Shared Function GetRequirement(
            stage As PaperStage
        ) As ManuscriptStageWorkflowRequirement

            Select Case stage

                Case PaperStage.Submitted,
                     PaperStage.UnderReview

                    Return ManuscriptStageWorkflowRequirement.ActiveSubmission

                Case PaperStage.Revision

                    Return ManuscriptStageWorkflowRequirement.RevisionDecision

                Case PaperStage.Accepted,
                     PaperStage.InPress,
                     PaperStage.Published

                    Return ManuscriptStageWorkflowRequirement.AcceptanceDecision

                Case Else

                    Return ManuscriptStageWorkflowRequirement.None

            End Select

        End Function


        Public Shared Function IsStageSupported(
            manuscript As Manuscript,
            stage As PaperStage
        ) As Boolean

            If manuscript Is Nothing Then
                Throw New ArgumentNullException(NameOf(manuscript))
            End If

            Dim requirement As ManuscriptStageWorkflowRequirement =
                GetRequirement(
                    stage
                )

            If requirement =
               ManuscriptStageWorkflowRequirement.None Then

                Return True

            End If

            Dim latestSubmission As JournalSubmission =
                ManuscriptAttentionService.
                    GetLatestSubmission(
                        manuscript
                    )

            If latestSubmission Is Nothing Then
                Return False
            End If

            Dim latestDecision As EditorialDecisionEvent =
                ManuscriptAttentionService.
                    GetLatestDecision(
                        latestSubmission
                    )

            Select Case requirement

                Case ManuscriptStageWorkflowRequirement.ActiveSubmission

                    Return latestDecision Is Nothing OrElse
                        latestDecision.Decision =
                        EditorialDecision.None

                Case ManuscriptStageWorkflowRequirement.RevisionDecision

                    If latestDecision Is Nothing Then
                        Return False
                    End If

                    Select Case latestDecision.Decision

                        Case EditorialDecision.MajorRevision,
                             EditorialDecision.MinorRevision,
                             EditorialDecision.ReviseAndResubmit

                            Return True

                        Case Else
                            Return False

                    End Select

                Case ManuscriptStageWorkflowRequirement.AcceptanceDecision

                    Return latestDecision IsNot Nothing AndAlso
                        latestDecision.Decision =
                        EditorialDecision.Accepted

                Case Else
                    Return True

            End Select

        End Function


        Public Shared Function RequirementDescription(
            requirement As ManuscriptStageWorkflowRequirement
        ) As String

            Select Case requirement

                Case ManuscriptStageWorkflowRequirement.ActiveSubmission
                    Return "an active Journal Submission record"

                Case ManuscriptStageWorkflowRequirement.RevisionDecision
                    Return "a revision editorial decision"

                Case ManuscriptStageWorkflowRequirement.AcceptanceDecision
                    Return "an accepted editorial decision"

                Case Else
                    Return String.Empty

            End Select

        End Function

    End Class

End Namespace
