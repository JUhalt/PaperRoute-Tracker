Imports System
Imports System.IO
Imports System.Linq
Imports System.Windows.Forms
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

Namespace Forms
    Partial Public Class EditManuscriptForm

        ' Every dialog shares this Manuscript Details working copy. Navigation
        ' unwinds each child before opening another, so stale clones cannot later
        ' overwrite changes made in a nested dialog.
        Private Sub RunSubmissionWorkflow(request As SubmissionWorkflowRequest)
            While request IsNot Nothing
                Dim nextRequest As SubmissionWorkflowRequest = Nothing
                Select Case request.Target
                    Case SubmissionWorkflowTarget.Readiness
                        Using dialog As New ManuscriptReadinessForm(_workingManuscript, _authorLibrary, request)
                            If dialog.ShowDialog(Me) = DialogResult.OK Then nextRequest = dialog.RequestedNavigation
                        End Using
                    Case SubmissionWorkflowTarget.Packets
                        Using dialog As New SubmissionPacketVaultForm(_workingManuscript, request)
                            If dialog.ShowDialog(Me) = DialogResult.OK Then nextRequest = dialog.RequestedNavigation
                        End Using
                    Case SubmissionWorkflowTarget.Version
                        If request.VersionId.HasValue Then versionHistoryControl.SelectVersionById(request.VersionId.Value)
                        ScrollControlIntoDetailsView(versionHistoryControl)
                    Case SubmissionWorkflowTarget.Submission
                        Dim submission = _workingManuscript.Submissions.FirstOrDefault(
                            Function(item) item IsNot Nothing AndAlso request.SubmissionId.HasValue AndAlso item.Id = request.SubmissionId.Value)
                        If submission IsNot Nothing Then
                            SelectSubmissionById(submission.Id)
                            Using dialog As New SubmissionDetailsForm(_workingManuscript, submission, True)
                                dialog.ShowDialog(Me)
                                nextRequest = dialog.RequestedNavigation
                            End Using
                            RefreshLifecycleControls()
                            RefreshSubmissionList()
                            SelectSubmissionById(submission.Id)
                        End If
                    Case SubmissionWorkflowTarget.RecordSubmission
                        nextRequest = RecordPreparedPacketSubmission(request)
                    Case SubmissionWorkflowTarget.Manuscript
                        ScrollControlIntoDetailsView(FindGroupBoxByText(Me, "Manuscript"))
                End Select
                request = nextRequest
            End While
        End Sub

        Private Function RecordPreparedPacketSubmission(request As SubmissionWorkflowRequest) As SubmissionWorkflowRequest
            Dim packet = _workingManuscript.SubmissionPackets.FirstOrDefault(
                Function(item) item IsNot Nothing AndAlso request.PacketId.HasValue AndAlso item.Id = request.PacketId.Value)
            If packet Is Nothing OrElse packet.SubmissionId.HasValue Then Return Nothing
            Dim journal = _authorLibrary.Journals.FirstOrDefault(
                Function(item) item IsNot Nothing AndAlso packet.JournalId.HasValue AndAlso item.Id = packet.JournalId.Value)
            Dim returnToPacket As New SubmissionWorkflowRequest With {
                .Target = SubmissionWorkflowTarget.Packets, .PacketId = packet.Id
            }
            Using dialog As New AddSubmissionForm(packet.JournalName, packet.JournalId,
                                                  If(journal Is Nothing, String.Empty, journal.SubmissionPortalUrl))
                If dialog.ShowDialog(Me) <> DialogResult.OK OrElse dialog.CreatedSubmission Is Nothing Then Return returnToPacket
                Try
                    SubmissionPacketWorkflowService.RecordSubmissionForPacket(_workingManuscript, packet.Id, dialog.CreatedSubmission)
                Catch ex As Exception When TypeOf ex Is InvalidOperationException OrElse
                    TypeOf ex Is InvalidDataException OrElse TypeOf ex Is ArgumentException
                    MessageBox.Show(Me, ex.Message, "Submission Not Recorded", MessageBoxButtons.OK, MessageBoxIcon.Information)
                    Return returnToPacket
                End Try
                RefreshLifecycleControls()
                RefreshSubmissionList()
                Return New SubmissionWorkflowRequest With {
                    .Target = SubmissionWorkflowTarget.Submission,
                    .SubmissionId = dialog.CreatedSubmission.Id,
                    .PacketId = packet.Id
                }
            End Using
        End Function
    End Class
End Namespace
