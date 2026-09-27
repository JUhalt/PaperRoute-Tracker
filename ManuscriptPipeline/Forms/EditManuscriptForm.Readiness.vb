Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.Linq
Imports System.Windows.Forms
Imports ManuscriptPipeline.Models

Namespace Forms

    Partial Public Class EditManuscriptForm

        Private ReadOnly btnReadiness As New Button()
        Private ReadOnly btnSubmissionPackets As New Button()


        Private Function CreateJournalToolsPanel() As Control

            Dim panel As New FlowLayoutPanel With {
                .Dock = DockStyle.Fill,
                .AutoSize = True,
                .AutoSizeMode = AutoSizeMode.GrowAndShrink,
                .FlowDirection = FlowDirection.LeftToRight,
                .WrapContents = True,
                .Margin = New Padding(0)
            }

            btnReadiness.Text =
                "Submission Readiness..."

            btnReadiness.AutoSize =
                True

            btnReadiness.Height =
                36

            btnReadiness.Anchor =
                AnchorStyles.Left

            AddHandler btnReadiness.Click,
                AddressOf OpenSubmissionReadiness

            btnSubmissionPackets.Text =
                "Submission Packets..."

            btnSubmissionPackets.AutoSize =
                True

            btnSubmissionPackets.Height =
                36

            btnSubmissionPackets.Anchor =
                AnchorStyles.Left

            AddHandler btnSubmissionPackets.Click,
                AddressOf OpenSubmissionPackets

            panel.Controls.Add(
                btnJournalLinks
            )

            Return panel

        End Function


        ' The Readiness & Packets tab: a summary and the two focused editors.
        Private Function CreateReadinessSection() As Control

            Dim layout As New TableLayoutPanel With {
                .Dock = DockStyle.Fill,
                .ColumnCount = 1,
                .RowCount = 4,
                .Padding = New Padding(0),
                .Margin = New Padding(0)
            }

            layout.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
            layout.RowStyles.Add(New RowStyle(SizeType.AutoSize))
            layout.RowStyles.Add(New RowStyle(SizeType.AutoSize))
            layout.RowStyles.Add(New RowStyle(SizeType.AutoSize))
            layout.RowStyles.Add(New RowStyle(SizeType.Percent, 100))

            Dim lblDescription As New Label With {
                .Text =
                    "Journal-specific checklists and the exact files you prepare for each submission. " &
                    "Each opens in its own window; what you keep there stays with this manuscript's other unsaved changes until you save.",
                .AutoSize = True,
                .MaximumSize = New Size(760, 0),
                .UseMnemonic = False,
                .ForeColor = SystemColors.GrayText,
                .Margin = New Padding(0, 0, 0, 10)
            }

            lblReadinessSummary.AutoSize = True
            lblReadinessSummary.MaximumSize = New Size(760, 0)
            lblReadinessSummary.UseMnemonic = False
            lblReadinessSummary.Margin = New Padding(0, 0, 0, 10)

            Dim buttons As New FlowLayoutPanel With {
                .Dock = DockStyle.Fill,
                .AutoSize = True,
                .AutoSizeMode = AutoSizeMode.GrowAndShrink,
                .FlowDirection = FlowDirection.LeftToRight,
                .WrapContents = True,
                .Margin = New Padding(0)
            }

            buttons.Controls.Add(btnReadiness)
            buttons.Controls.Add(btnSubmissionPackets)

            layout.Controls.Add(lblDescription, 0, 0)
            layout.Controls.Add(lblReadinessSummary, 0, 1)
            layout.Controls.Add(buttons, 0, 2)

            Return layout

        End Function


        Private Sub RefreshReadinessSummary()

            Dim profiles As Integer =
                If(_workingManuscript.ReadinessProfiles, New List(Of ManuscriptReadiness)()).Where(Function(item) item IsNot Nothing).Count()

            Dim packets As List(Of SubmissionPacket) =
                If(_workingManuscript.SubmissionPackets, New List(Of SubmissionPacket)()).Where(Function(item) item IsNot Nothing).ToList()

            Dim linked As Integer =
                packets.Where(Function(item) item.SubmissionId.HasValue).Count()

            lblReadinessSummary.Text =
                If(profiles = 0,
                   "No readiness checklist yet. Start one from the target journal in Submission Readiness.",
                   profiles.ToString() & If(profiles = 1, " readiness checklist.", " readiness checklists.")) &
                Environment.NewLine &
                If(packets.Count = 0,
                   "No submission packets yet.",
                   packets.Count.ToString() & If(packets.Count = 1, " packet", " packets") &
                   ", " & linked.ToString() & " linked to a recorded submission.")

        End Sub


        Private Sub OpenSubmissionReadiness(
            sender As Object,
            e As EventArgs
        )

            _authorLibrary =
                _authorRepository.Load()

            RunSubmissionWorkflow(New SubmissionWorkflowRequest With {.Target = SubmissionWorkflowTarget.Readiness})

        End Sub


        Private Sub OpenSubmissionPackets(
            sender As Object,
            e As EventArgs
        )

            RunSubmissionWorkflow(New SubmissionWorkflowRequest With {.Target = SubmissionWorkflowTarget.Packets})

        End Sub


        Private Sub CopyReadinessStateToOriginal(
            committed As Manuscript
        )

            If committed Is Nothing Then
                Return
            End If

            _originalManuscript.ReadinessProfiles =
                committed.ReadinessProfiles

            _originalManuscript.SubmissionPackets =
                committed.SubmissionPackets

        End Sub

    End Class

End Namespace
