Imports System
Imports System.Collections.Generic
Imports System.IO
Imports ClosedXML.Excel
Imports ManuscriptPipeline.Models

Namespace Services

    Public Class LibraryExcelExporter

        Public Sub Export(
            filePath As String,
            manuscripts As IEnumerable(Of Manuscript)
        )

            If String.IsNullOrWhiteSpace(filePath) Then
                Throw New ArgumentException("A destination file path is required.")
            End If

            If manuscripts Is Nothing Then
                Throw New ArgumentNullException(NameOf(manuscripts))
            End If

            ' =================================================
            ' Start with the official template so exports and
            ' imports always share the same workbook structure.
            ' =================================================

            Dim generator As New StandardTemplateGenerator()

            generator.Generate(filePath)

            Using workbook As New XLWorkbook(filePath)

                Dim manuscriptsSheet As IXLWorksheet =
                    workbook.Worksheet("Manuscripts")

                Dim submissionsSheet As IXLWorksheet =
                    workbook.Worksheet("Submissions")

                Dim decisionsSheet As IXLWorksheet =
                    workbook.Worksheet("Decisions")

                Dim correspondenceSheet As IXLWorksheet =
                    workbook.Worksheet("Correspondence")

                Dim manuscriptRow As Integer = 2
                Dim submissionRow As Integer = 2
                Dim decisionRow As Integer = 2
                Dim correspondenceRow As Integer = 2

                For Each manuscript As Manuscript In manuscripts

                    Dim manuscriptExternalId As String =
                        "M-" & manuscript.Id.ToString("N")

                    ' =========================================
                    ' Manuscript
                    ' =========================================

                    manuscriptsSheet.Cell(manuscriptRow, 1).Value =
                        manuscriptExternalId

                    manuscriptsSheet.Cell(manuscriptRow, 2).Value =
                        manuscript.Title

                    manuscriptsSheet.Cell(manuscriptRow, 3).Value =
                        FormatStage(manuscript.CurrentStage)

                    manuscriptsSheet.Cell(manuscriptRow, 4).Value =
                        FormatLocation(manuscript.Location)

                    manuscriptsSheet.Cell(manuscriptRow, 5).Value =
                        manuscript.TargetJournal

                    manuscriptsSheet.Cell(manuscriptRow, 6).Value =
                        manuscript.StageEnteredDate

                    If manuscript.FileDrawerDate.HasValue Then

                        manuscriptsSheet.Cell(manuscriptRow, 7).Value =
                            manuscript.FileDrawerDate.Value

                    End If

                    manuscriptsSheet.Cell(manuscriptRow, 8).Value =
                        manuscript.FileDrawerReason

                    manuscriptRow += 1

                    ' =========================================
                    ' Submissions
                    ' =========================================

                    For Each submission As JournalSubmission In manuscript.Submissions

                        Dim submissionExternalId As String =
                            "S-" & submission.Id.ToString("N")

                        submissionsSheet.Cell(submissionRow, 1).Value =
                            submissionExternalId

                        submissionsSheet.Cell(submissionRow, 2).Value =
                            manuscriptExternalId

                        submissionsSheet.Cell(submissionRow, 3).Value =
                            submission.JournalName

                        submissionsSheet.Cell(submissionRow, 4).Value =
                            submission.ManuscriptNumber

                        submissionsSheet.Cell(submissionRow, 5).Value =
                            submission.SubmittedDate

                        submissionsSheet.Cell(submissionRow, 6).Value =
                            submission.PortalUrl

                        submissionsSheet.Cell(submissionRow, 7).Value =
                            submission.Notes

                        submissionRow += 1

                        ' =====================================
                        ' Editorial decisions
                        ' =====================================

                        For Each decisionEvent As EditorialDecisionEvent In submission.Decisions

                            Dim decisionExternalId As String =
                                "D-" & decisionEvent.Id.ToString("N")

                            decisionsSheet.Cell(decisionRow, 1).Value =
                                decisionExternalId

                            decisionsSheet.Cell(decisionRow, 2).Value =
                                submissionExternalId

                            decisionsSheet.Cell(decisionRow, 3).Value =
                                decisionEvent.DecisionDate

                            decisionsSheet.Cell(decisionRow, 4).Value =
                                FormatDecision(decisionEvent.Decision)

                            If decisionEvent.RevisionDeadline.HasValue Then

                                decisionsSheet.Cell(decisionRow, 5).Value =
                                    decisionEvent.RevisionDeadline.Value

                            End If

                            decisionsSheet.Cell(decisionRow, 6).Value =
                                decisionEvent.Notes

                            decisionRow += 1

                        Next

                        ' =====================================
                        ' Correspondence and files
                        ' =====================================

                        For Each item As CorrespondenceItem In submission.Correspondence

                            Dim correspondenceExternalId As String =
                                "C-" & item.Id.ToString("N")

                            correspondenceSheet.Cell(correspondenceRow, 1).Value =
                                correspondenceExternalId

                            correspondenceSheet.Cell(correspondenceRow, 2).Value =
                                submissionExternalId

                            correspondenceSheet.Cell(correspondenceRow, 3).Value =
                                item.ItemDate

                            correspondenceSheet.Cell(correspondenceRow, 4).Value =
                                FormatCorrespondenceType(item.Type)

                            correspondenceSheet.Cell(correspondenceRow, 5).Value =
                                item.Title

                            correspondenceSheet.Cell(correspondenceRow, 6).Value =
                                item.LocalFilePath

                            If Not String.IsNullOrWhiteSpace(item.LocalFilePath) Then

                                If item.IsManagedCopy Then

                                    correspondenceSheet.Cell(correspondenceRow, 7).Value =
                                        "ManagedCopy"

                                Else

                                    correspondenceSheet.Cell(correspondenceRow, 7).Value =
                                        "Link"

                                End If

                            End If

                            correspondenceSheet.Cell(correspondenceRow, 8).Value =
                                item.SourceUrl

                            correspondenceSheet.Cell(correspondenceRow, 9).Value =
                                item.Notes

                            correspondenceRow += 1

                        Next

                    Next

                Next

                workbook.Save()

            End Using

        End Sub


        ' =====================================================
        ' Stage formatting
        ' =====================================================

        Private Function FormatStage(
            stage As PaperStage
        ) As String

            Select Case stage

                Case PaperStage.Idea
                    Return "Idea"

                Case PaperStage.Draft
                    Return "Draft"

                Case PaperStage.Submitted
                    Return "Submitted"

                Case PaperStage.UnderReview
                    Return "UnderReview"

                Case PaperStage.Revision
                    Return "Revision"

                Case PaperStage.Accepted
                    Return "Accepted"

                Case PaperStage.InPress
                    Return "InPress"

                Case PaperStage.Published
                    Return "Published"

                Case Else
                    Return stage.ToString()

            End Select

        End Function


        ' =====================================================
        ' Location formatting
        ' =====================================================

        Private Function FormatLocation(
            location As ManuscriptLocation
        ) As String

            Select Case location

                Case ManuscriptLocation.Pipeline
                    Return "Pipeline"

                Case ManuscriptLocation.Published
                    Return "Published"

                Case ManuscriptLocation.FileDrawer
                    Return "FileDrawer"

                Case Else
                    Return location.ToString()

            End Select

        End Function


        ' =====================================================
        ' Editorial decision formatting
        ' =====================================================

        Private Function FormatDecision(
            decision As EditorialDecision
        ) As String

            Select Case decision

                Case EditorialDecision.Rejected
                    Return "Rejected"

                Case EditorialDecision.DeskRejected
                    Return "Desk Rejected"

                Case EditorialDecision.RejectedAfterReview
                    Return "Rejected After Review"

                Case EditorialDecision.MajorRevision
                    Return "Major Revision"

                Case EditorialDecision.MinorRevision
                    Return "Minor Revision"

                Case EditorialDecision.ReviseAndResubmit
                    Return "Revise and Resubmit"

                Case EditorialDecision.Accepted
                    Return "Accepted"

                Case EditorialDecision.Withdrawn
                    Return "Withdrawn"

                Case Else
                    Return decision.ToString()

            End Select

        End Function


        ' =====================================================
        ' Correspondence type formatting
        ' =====================================================

        Private Function FormatCorrespondenceType(
            itemType As CorrespondenceType
        ) As String

            Select Case itemType

                Case CorrespondenceType.DecisionLetter
                    Return "Decision Letter"

                Case CorrespondenceType.ReviewerComments
                    Return "Reviewer Comments"

                Case CorrespondenceType.EditorEmail
                    Return "Editor Email"

                Case CorrespondenceType.CoverLetter
                    Return "Cover Letter"

                Case CorrespondenceType.ResponseToReviewers
                    Return "Response to Reviewers"

                Case CorrespondenceType.RevisedManuscript
                    Return "Revised Manuscript"

                Case CorrespondenceType.AcceptanceLetter
                    Return "Acceptance Letter"

                Case CorrespondenceType.PortalSnapshot
                    Return "Portal Snapshot"

                Case CorrespondenceType.Other
                    Return "Other"

                Case Else
                    Return "Other"

            End Select

        End Function

    End Class

End Namespace