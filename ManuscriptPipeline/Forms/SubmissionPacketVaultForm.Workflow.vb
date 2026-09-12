Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.Linq
Imports System.Windows.Forms
Imports ManuscriptPipeline.Models

Namespace Forms

    Partial Public Class SubmissionPacketVaultForm

        Private ReadOnly _workflowContext As SubmissionWorkflowRequest
        Private ReadOnly _workflowItems As New Dictionary(Of SubmissionWorkflowTarget, ToolStripMenuItem)()
        Private ReadOnly _workflowScopeLabel As New Label()
        Private ReadOnly _showAllPacketsLink As New LinkLabel()
        Private _workflowNavigationButton As Button
        Private _showAllPackets As Boolean
        Private _requestedNavigation As SubmissionWorkflowRequest

        Public ReadOnly Property RequestedNavigation As SubmissionWorkflowRequest
            Get
                Return _requestedNavigation
            End Get
        End Property

        Private Shared Function CopyWorkflowContext(
            source As SubmissionWorkflowRequest
        ) As SubmissionWorkflowRequest

            If source Is Nothing Then Return Nothing

            Return New SubmissionWorkflowRequest With {
                .Target = source.Target,
                .PacketId = source.PacketId,
                .ReadinessProfileId = source.ReadinessProfileId,
                .VersionId = source.VersionId,
                .SubmissionId = source.SubmissionId
            }

        End Function

        Private Function BuildWorkflowPacketHeading() As Control

            Dim heading As New TableLayoutPanel With {
                .AutoSize = True,
                .AutoSizeMode = AutoSizeMode.GrowAndShrink,
                .Dock = DockStyle.Fill,
                .ColumnCount = 1,
                .RowCount = 3,
                .Margin = New Padding(0, 0, 0, 8)
            }
            heading.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
            heading.RowStyles.Add(New RowStyle(SizeType.AutoSize))
            heading.RowStyles.Add(New RowStyle(SizeType.AutoSize))
            heading.RowStyles.Add(New RowStyle(SizeType.AutoSize))
            heading.Controls.Add(New Label With {
                .Text = "Submission Packets",
                .AutoSize = True,
                .Dock = DockStyle.Fill,
                .Font = New Font(Me.Font, FontStyle.Bold),
                .Margin = New Padding(0)
            }, 0, 0)

            _workflowScopeLabel.AutoSize = True
            _workflowScopeLabel.Dock = DockStyle.Fill
            _workflowScopeLabel.UseMnemonic = False
            _workflowScopeLabel.AccessibleName = "Packet workflow scope"
            _workflowScopeLabel.Margin = New Padding(0, 4, 0, 0)
            _showAllPacketsLink.AutoSize = True
            _showAllPacketsLink.Text = "Show all packets"
            _showAllPacketsLink.AccessibleName = "Show all manuscript packets"
            _showAllPacketsLink.Margin = New Padding(0, 2, 0, 0)
            AddHandler _showAllPacketsLink.LinkClicked,
                Sub(sender As Object, e As LinkLabelLinkClickedEventArgs)
                    ShowAllWorkflowPackets()
                End Sub
            heading.Controls.Add(_workflowScopeLabel, 0, 1)
            heading.Controls.Add(_showAllPacketsLink, 0, 2)
            UpdateWorkflowScopeLabel()
            Return heading

        End Function

        Private Function HasWorkflowFilter() As Boolean
            Return Not _showAllPackets AndAlso _workflowContext IsNot Nothing AndAlso
                (_workflowContext.ReadinessProfileId.HasValue OrElse
                 _workflowContext.SubmissionId.HasValue OrElse _workflowContext.VersionId.HasValue)
        End Function

        Private Function MatchesWorkflowScope(packet As SubmissionPacket) As Boolean

            If Not HasWorkflowFilter() Then Return True

            Return (Not _workflowContext.ReadinessProfileId.HasValue OrElse
                    packet.ReadinessProfileId.Equals(_workflowContext.ReadinessProfileId)) AndAlso
                (Not _workflowContext.SubmissionId.HasValue OrElse
                 packet.SubmissionId.Equals(_workflowContext.SubmissionId)) AndAlso
                (Not _workflowContext.VersionId.HasValue OrElse
                 packet.ManuscriptVersionId = _workflowContext.VersionId.Value)

        End Function

        Private Sub UpdateWorkflowScopeLabel()

            If Not HasWorkflowFilter() Then
                _workflowScopeLabel.Text = "Showing all manuscript packets."
                _workflowScopeLabel.Visible = _workflowContext IsNot Nothing
                _showAllPacketsLink.Visible = False
                Return
            End If

            Dim scopes As New List(Of String)()
            If _workflowContext.ReadinessProfileId.HasValue Then scopes.Add("this readiness profile")
            If _workflowContext.SubmissionId.HasValue Then scopes.Add("this actual submission")
            If _workflowContext.VersionId.HasValue Then scopes.Add("this exact version")
            _workflowScopeLabel.Text = "Showing packets linked to " & String.Join(" and ", scopes) & "."
            _workflowScopeLabel.Visible = True
            _showAllPacketsLink.Visible = True

        End Sub

        Friend Sub ShowAllWorkflowPackets()
            If _integrityBusy OrElse IsDisposed OrElse Disposing Then Return
            _showAllPackets = True
            RefreshPackets()
        End Sub

        Private Sub EnsureWorkflowPacketVisible(packet As SubmissionPacket)
            If packet IsNot Nothing AndAlso Not MatchesWorkflowScope(packet) Then _showAllPackets = True
        End Sub

        Private Sub AddWorkflowNavigation(buttons As FlowLayoutPanel)

            If _workflowContext Is Nothing Then Return

            _workflowNavigationButton = New Button With {
                .Text = "Save && Go To...",
                .AutoSize = True,
                .Height = 36,
                .AccessibleName = "Save packet changes and go to related record"
            }
            Dim menu As New ContextMenuStrip()
            AddWorkflowMenuItem(menu, SubmissionWorkflowTarget.Manuscript, "Manuscript Details")
            AddWorkflowMenuItem(menu, SubmissionWorkflowTarget.Version, "Exact Version")
            AddWorkflowMenuItem(menu, SubmissionWorkflowTarget.Readiness, "Readiness")
            AddWorkflowMenuItem(menu, SubmissionWorkflowTarget.Submission, "Actual Submission")
            AddWorkflowMenuItem(menu, SubmissionWorkflowTarget.RecordSubmission, "Record Submission...")
            AddHandler _workflowNavigationButton.Click,
                Sub(sender As Object, e As EventArgs)
                    UpdateWorkflowNavigation()
                    menu.Show(_workflowNavigationButton, New Point(0, _workflowNavigationButton.Height))
                End Sub
            AddHandler Me.Disposed, Sub(sender As Object, e As EventArgs) menu.Dispose()
            buttons.Controls.Add(_workflowNavigationButton)

        End Sub

        Private Sub AddWorkflowMenuItem(
            menu As ContextMenuStrip,
            target As SubmissionWorkflowTarget,
            label As String
        )

            Dim item As New ToolStripMenuItem(label)
            AddHandler item.Click,
                Sub(sender As Object, e As EventArgs)
                    RequestWorkflowNavigation(target)
                End Sub
            _workflowItems.Add(target, item)
            menu.Items.Add(item)

        End Sub

        Private Function CanRequestWorkflowNavigation(target As SubmissionWorkflowTarget) As Boolean

            If _workflowContext Is Nothing OrElse _integrityBusy OrElse IsDisposed OrElse Disposing Then Return False
            If target = SubmissionWorkflowTarget.Manuscript Then Return True

            Dim packet As SubmissionPacket = GetSelectedPacket()
            If packet Is Nothing Then Return False

            Select Case target
                Case SubmissionWorkflowTarget.Version
                    Return _workingManuscript.Versions IsNot Nothing AndAlso
                        _workingManuscript.Versions.Any(Function(item) item IsNot Nothing AndAlso
                            item.Id = packet.ManuscriptVersionId)
                Case SubmissionWorkflowTarget.Readiness
                    Return packet.ReadinessProfileId.HasValue AndAlso _workingManuscript.ReadinessProfiles IsNot Nothing AndAlso
                        _workingManuscript.ReadinessProfiles.Any(Function(item) item IsNot Nothing AndAlso
                            item.Id = packet.ReadinessProfileId.Value)
                Case SubmissionWorkflowTarget.Submission
                    Return packet.SubmissionId.HasValue AndAlso _workingManuscript.Submissions IsNot Nothing AndAlso
                        _workingManuscript.Submissions.Any(Function(item) item IsNot Nothing AndAlso
                            item.Id = packet.SubmissionId.Value)
                Case SubmissionWorkflowTarget.RecordSubmission
                    Return Not packet.SubmissionId.HasValue
                Case Else
                    Return False
            End Select

        End Function

        Private Sub UpdateWorkflowNavigation()
            If _workflowNavigationButton Is Nothing Then Return
            _workflowNavigationButton.Enabled = Not _integrityBusy
            For Each entry In _workflowItems
                entry.Value.Enabled = CanRequestWorkflowNavigation(entry.Key)
            Next
        End Sub

        Friend Function RequestWorkflowNavigation(target As SubmissionWorkflowTarget) As Boolean

            If Not CanRequestWorkflowNavigation(target) Then Return False

            Dim request As New SubmissionWorkflowRequest With {.Target = target}
            Dim packet As SubmissionPacket = GetSelectedPacket()
            If packet IsNot Nothing Then
                request.PacketId = packet.Id
                request.ReadinessProfileId = packet.ReadinessProfileId
                request.VersionId = packet.ManuscriptVersionId
                request.SubmissionId = packet.SubmissionId
            End If

            SaveVault(Me, EventArgs.Empty)
            If Me.DialogResult <> DialogResult.OK Then Return False
            _requestedNavigation = request
            Return True

        End Function

        Private Function BuildWorkflowPacketDetail(packet As SubmissionPacket) As String

            Dim details As New List(Of String)()
            Dim version As ManuscriptVersion = If(_workingManuscript.Versions Is Nothing, Nothing,
                _workingManuscript.Versions.FirstOrDefault(Function(item) item IsNot Nothing AndAlso
                    item.Id = packet.ManuscriptVersionId))
            details.Add("Version: " & If(version Is Nothing, "(Missing version)",
                version.CreatedDate.ToShortDateString() & " — " & WorkflowText(version.Label, "(Unlabeled version)")))
            details.Add("Journal: " & WorkflowText(packet.JournalName, "(Not recorded)"))

            If packet.ReadinessProfileId.HasValue Then
                Dim profile As ManuscriptReadiness = If(_workingManuscript.ReadinessProfiles Is Nothing, Nothing,
                    _workingManuscript.ReadinessProfiles.FirstOrDefault(Function(item) item IsNot Nothing AndAlso
                        item.Id = packet.ReadinessProfileId.Value))
                details.Add("Readiness: " & If(profile Is Nothing, "(Missing profile)",
                    WorkflowText(profile.JournalName, "(Unnamed readiness profile)") &
                    " — profile created " & profile.CreatedAtUtc.ToLocalTime().ToShortDateString()))
            Else
                details.Add("Readiness: no linked profile")
            End If

            If packet.SubmissionId.HasValue Then
                Dim submission As JournalSubmission = If(_workingManuscript.Submissions Is Nothing, Nothing,
                    _workingManuscript.Submissions.FirstOrDefault(Function(item) item IsNot Nothing AndAlso
                        item.Id = packet.SubmissionId.Value))
                If submission Is Nothing Then
                    details.Add("Actual submission: (Missing record)")
                Else
                    details.Add("Actual submission: " & WorkflowText(submission.JournalName, "(Unnamed journal)") &
                        " — " & submission.SubmittedDate.ToShortDateString())
                    details.Add("Manuscript number: " & WorkflowText(submission.ManuscriptNumber, "(Not recorded)"))
                End If
            Else
                details.Add("Actual submission: none linked; this packet only records preparation")
            End If

            details.Add("Revision round: " & If(packet.RevisionRoundNumber.HasValue,
                packet.RevisionRoundNumber.GetValueOrDefault().ToString(), "(Not linked to a round)"))
            details.Add(If(packet.Files Is Nothing, 0, packet.Files.Count).ToString() & " file record(s)")
            If Not String.IsNullOrWhiteSpace(packet.Notes) Then
                details.Add(String.Empty)
                details.Add("Notes: " & packet.Notes)
            End If
            Return String.Join(Environment.NewLine, details)

        End Function

        Private Shared Function WorkflowText(value As String, fallback As String) As String
            Return If(String.IsNullOrWhiteSpace(value), fallback, value.Trim())
        End Function

    End Class

End Namespace
