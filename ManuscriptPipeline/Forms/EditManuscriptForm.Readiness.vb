Imports System
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

            panel.Controls.Add(
                btnReadiness
            )

            panel.Controls.Add(
                btnSubmissionPackets
            )

            Return panel

        End Function


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
