Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.Linq
Imports System.Windows.Forms
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

Namespace Forms

    Partial Public Class EditManuscriptForm

        Private ReadOnly btnReadiness As New Button()
        Private ReadOnly btnSubmissionPackets As New Button()
        ' The AI assistant's cover letter starting point (#84), shown only
        ' while the assistant is turned on.
        Private ReadOnly btnCoverLetter As New Button()

        ' Test seam: shows the cover letter window instead of ShowDialog.
        Friend coverLetterPrompt As Action(Of CoverLetterForm) = Nothing


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

            btnCoverLetter.Text = "Draft Cover Letter..."
            btnCoverLetter.AutoSize = True
            btnCoverLetter.Height = 36
            btnCoverLetter.Anchor = AnchorStyles.Left
            btnCoverLetter.Visible = AssistantRunner.IsTurnedOn()
            AddHandler btnCoverLetter.Click, Sub(sender, e) OpenCoverLetter()

            buttons.Controls.Add(btnReadiness)
            buttons.Controls.Add(btnSubmissionPackets)
            buttons.Controls.Add(btnCoverLetter)

            ' The assistant may have been turned on or off since the page opened.
            AddHandler layout.VisibleChanged,
                Sub(sender, e)
                    If layout.Visible Then btnCoverLetter.Visible = AssistantRunner.IsTurnedOn()
                End Sub

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


        ' A cover letter starting point from the page's current values: the
        ' title, target journal, type of work, keywords, and abstract, and the
        ' journal's facts from the Journal Library. Never notes or author
        ' names. Nothing on the page or in the library changes.
        Private Sub OpenCoverLetter()

            Dim reason As String = AssistantRunner.Unavailable()
            If reason.Length > 0 AndAlso coverLetterPrompt Is Nothing Then
                MessageBox.Show(Me.FindForm(), reason, AssistantService.FeatureName(AssistantService.CoverLetterFeature), MessageBoxButtons.OK, MessageBoxIcon.Information)
                Return
            End If

            Dim typeName As String =
                If(_workingManuscript.WorkType = WorkType.Unspecified, String.Empty, WorkTypeService.DisplayName(_workingManuscript.WorkType))

            Using dialog As New CoverLetterForm(
                txtTitle.Text,
                txtTargetJournal.Text,
                typeName,
                _workingManuscript.Metadata?.Keywords,
                _workingManuscript.Metadata?.AbstractText,
                AddressOf CoverLetterJournalFacts)

                If coverLetterPrompt IsNot Nothing Then
                    coverLetterPrompt(dialog)
                Else
                    dialog.ShowDialog(Me.FindForm())
                End If

            End Using

        End Sub


        ' One line of facts for a journal in the Journal Library: the
        ' manuscript's linked journal when the name still matches, else the
        ' first with that name. "" when none matches.
        Private Function CoverLetterJournalFacts(journalName As String) As String

            Dim key As String = RouteAnalyticsService.NameKey(journalName)
            If key.Length = 0 Then Return String.Empty

            Dim library As List(Of JournalRecord) = If(_authorLibrary?.Journals, New List(Of JournalRecord)())
            Dim record As JournalRecord = Nothing
            If _workingManuscript.TargetJournalId.HasValue Then
                record = library.FirstOrDefault(Function(item) item IsNot Nothing AndAlso item.Id = _workingManuscript.TargetJournalId.Value AndAlso RouteAnalyticsService.NameKey(item.Name) = key)
            End If
            If record Is Nothing Then
                record = library.FirstOrDefault(Function(item) item IsNot Nothing AndAlso RouteAnalyticsService.NameKey(item.Name) = key)
            End If

            Return JournalFactsService.OneLine(record)

        End Function


        Friend Sub DraftCoverLetterForTest()
            OpenCoverLetter()
        End Sub


        Friend ReadOnly Property CoverLetterButton As Button
            Get
                Return btnCoverLetter
            End Get
        End Property


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
