Imports System
Imports System.Diagnostics
Imports System.Linq
Imports System.Windows.Forms
Imports ManuscriptPipeline.Models

Namespace Forms
    Partial Public Class ManuscriptReadinessForm
        Private ReadOnly _workflowContext As SubmissionWorkflowRequest
        Private ReadOnly _viewPackets As New ToolStripMenuItem("Save && View Packets")
        Private ReadOnly _portal As New Button With {.Text = "Open Submission Portal", .AutoSize = True}

        Public Property RequestedNavigation As SubmissionWorkflowRequest
            Get
                Return _requestedNavigation
            End Get
            Private Set(value As SubmissionWorkflowRequest)
                _requestedNavigation = value
            End Set
        End Property
        Private _requestedNavigation As SubmissionWorkflowRequest

        Private Sub ConfigureWorkflowNavigation(host As FlowLayoutPanel)
            If _workflowContext IsNot Nothing Then
                Dim navigation As New Button With {
                    .Text = "Save && Go To...", .AutoSize = True,
                    .AccessibleName = "Save readiness and navigate"
                }
                Dim menu As New ContextMenuStrip()
                menu.Items.Add(_viewPackets)
                Dim manuscript = menu.Items.Add("Save && Return to Manuscript")
                AddHandler _viewPackets.Click, Sub() RequestWorkflowNavigation(SubmissionWorkflowTarget.Packets)
                AddHandler manuscript.Click, Sub() RequestWorkflowNavigation(SubmissionWorkflowTarget.Manuscript)
                AddHandler navigation.Click, Sub() menu.Show(navigation, 0, navigation.Height)
                navigation.ContextMenuStrip = menu
                host.Controls.Add(navigation)
            End If
            _portal.AccessibleName = "Open selected journal submission portal"
            AddHandler _portal.Click, AddressOf OpenReadinessPortal
            host.Controls.Add(_portal)
        End Sub

        Friend Function RequestWorkflowNavigation(target As SubmissionWorkflowTarget) As Boolean
            If _workflowContext Is Nothing Then Return False
            Dim profile As ManuscriptReadiness = GetSelectedProfile()
            If target = SubmissionWorkflowTarget.Packets AndAlso profile Is Nothing Then Return False
            If target <> SubmissionWorkflowTarget.Packets AndAlso target <> SubmissionWorkflowTarget.Manuscript Then Return False
            RequestedNavigation = New SubmissionWorkflowRequest With {
                .Target = target,
                .ReadinessProfileId = If(profile Is Nothing, Nothing, CType(profile.Id, Guid?))
            }
            SaveReadiness(Me, EventArgs.Empty)
            Return True
        End Function

        Private Sub UpdateWorkflowButtons()
            _viewPackets.Enabled = GetSelectedProfile() IsNot Nothing
            _portal.Enabled = GetReadinessPortal() IsNot Nothing
        End Sub

        Friend Function GetReadinessPortal() As Uri
            Dim profile As ManuscriptReadiness = GetSelectedProfile()
            If profile Is Nothing OrElse Not profile.JournalId.HasValue Then Return Nothing
            Dim journal As JournalRecord = _library.Journals.FirstOrDefault(
                Function(item) item IsNot Nothing AndAlso item.Id = profile.JournalId.Value)
            Dim portal As Uri = Nothing
            If journal Is Nothing OrElse Not Uri.TryCreate(journal.SubmissionPortalUrl, UriKind.Absolute, portal) Then Return Nothing
            If portal.Scheme <> Uri.UriSchemeHttp AndAlso portal.Scheme <> Uri.UriSchemeHttps Then Return Nothing
            Return portal
        End Function

        Private Sub OpenReadinessPortal(sender As Object, e As EventArgs)
            Dim portal As Uri = GetReadinessPortal()
            If portal Is Nothing Then Return
            Try
                Process.Start(New ProcessStartInfo(portal.AbsoluteUri) With {.UseShellExecute = True})
            Catch ex As Exception
                MessageBox.Show(Me, "PaperRoute could not open the submission portal." & Environment.NewLine & ex.Message,
                    "Submission Portal", MessageBoxButtons.OK, MessageBoxIcon.Information)
            End Try
        End Sub
    End Class
End Namespace
