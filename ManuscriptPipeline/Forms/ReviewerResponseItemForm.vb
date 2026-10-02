Imports System
Imports System.Drawing
Imports System.IO
Imports System.Linq
Imports System.Windows.Forms
Imports ManuscriptPipeline.Controls
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

Namespace Forms
    Public Class ReviewerResponseItemForm
        Inherits Form

        Private ReadOnly _submission As JournalSubmission
        Private ReadOnly _existing As ReviewerResponseItem
        Private ReadOnly cmbDecision As New ComboBox()
        Private ReadOnly nudRound As New NumericUpDown()
        Private ReadOnly txtReviewer As New TextBox()
        Private ReadOnly cmbStatus As New ComboBox()
        Private ReadOnly txtComment As New TextBox()
        Private ReadOnly txtAction As New TextBox()
        Private ReadOnly txtResponse As New TextBox()
        Private ReadOnly txtLocation As New TextBox()
        Private ReadOnly txtNotes As New TextBox()
        Private ReadOnly tabs As New UnderlineTabControl()
        ' The AI assistant's starting point (#84), below the response box.
        Private ReadOnly assistantRow As New TableLayoutPanel()
        Private ReadOnly btnSuggest As New Button()
        Private ReadOnly lblAssistantNote As New Label()
        Private ReadOnly lblProvenance As New Label()
        Private _assistantOn As Boolean
        ' A starting point used in this editing session, kept on Save Comment.
        Private _pendingSuggestion As AssistantSuggestion

        ' Test seams: the starting-point window, and the notices otherwise
        ' shown in message boxes.
        Friend draftPrompt As Func(Of AssistantDraftForm, DialogResult) = Nothing
        Friend noticePrompt As Action(Of String) = Nothing

        Private _editedItem As ReviewerResponseItem
        Public ReadOnly Property EditedItem As ReviewerResponseItem
            Get
                Return _editedItem
            End Get
        End Property

        Public Sub New(submission As JournalSubmission, Optional existing As ReviewerResponseItem = Nothing)
            If submission Is Nothing Then Throw New ArgumentNullException(NameOf(submission))
            _submission = submission
            _existing = existing
            Text = If(existing Is Nothing, "Add Reviewer Comment", "Edit Reviewer Comment")
            Font = New Font("Segoe UI", 10.0F)
            AutoScaleMode = AutoScaleMode.Dpi
            StartPosition = FormStartPosition.CenterParent
            Size = New Size(840, 760)
            MinimumSize = New Size(680, 600)
            BuildInterface()
            UiPolish.ApplyDialog(Me)
            LoadItem()
        End Sub

        Private Sub BuildInterface()
            Dim root As New TableLayoutPanel With {
                .Dock = DockStyle.Fill, .ColumnCount = 1, .RowCount = 4, .Padding = New Padding(18)
            }
            root.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
            root.RowStyles.Add(New RowStyle(SizeType.AutoSize))
            root.RowStyles.Add(New RowStyle(SizeType.AutoSize))
            root.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
            root.RowStyles.Add(New RowStyle(SizeType.AutoSize))
            root.Controls.Add(New Label With {
                .Text = "Link this comment to a recorded decision and enter its revision round.",
                .Dock = DockStyle.Fill, .AutoSize = True, .Margin = New Padding(0, 0, 0, 12)
            }, 0, 0)

            Dim metadata As New TableLayoutPanel With {
                .Dock = DockStyle.Fill, .AutoSize = True, .ColumnCount = 2, .RowCount = 4,
                .Margin = New Padding(0, 0, 0, 10)
            }
            metadata.ColumnStyles.Add(New ColumnStyle(SizeType.AutoSize))
            metadata.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
            cmbDecision.DropDownStyle = ComboBoxStyle.DropDownList
            cmbStatus.DropDownStyle = ComboBoxStyle.DropDownList
            cmbDecision.AccessibleName = "Editorial decision"
            cmbStatus.AccessibleName = "Response status"
            txtReviewer.AccessibleName = "Reviewer label"
            nudRound.AccessibleName = "Revision round"
            nudRound.Minimum = 1
            nudRound.Maximum = Integer.MaxValue
            nudRound.Value = 1
            AddMetadata(metadata, "Editorial &decision", cmbDecision, 0)
            AddMetadata(metadata, "Revision &round", nudRound, 1)
            AddMetadata(metadata, "Re&viewer (required)", txtReviewer, 2)
            AddMetadata(metadata, "&Status", cmbStatus, 3)
            For Each status As ReviewerResponseStatus In [Enum].GetValues(Of ReviewerResponseStatus)()
                cmbStatus.Items.Add(New StatusChoice(status))
            Next
            For Each decision As EditorialDecisionEvent In _submission.Decisions.Where(Function(item) item IsNot Nothing)
                cmbDecision.Items.Add(New DecisionChoice(decision, cmbDecision.Items.Count + 1))
            Next
            root.Controls.Add(metadata, 0, 1)

            tabs.Dock = DockStyle.Fill
            tabs.AccessibleName = "Reviewer response fields"
            Dim commentTab As New TabPage("Comment && action")
            commentTab.Controls.Add(CreateTextPair("Reviewer comment (enter a comment or action)", txtComment,
                                                  "Planned or completed action", txtAction))
            Dim responseTab As New TabPage("Draft response")
            responseTab.Controls.Add(CreateResponseSection())
            Dim notesTab As New TabPage("Location && notes")
            notesTab.Controls.Add(CreateTextPair("Manuscript location (page, line, or section)", txtLocation,
                                                "Working notes (included in export)", txtNotes))
            tabs.TabPages.AddRange({commentTab, responseTab, notesTab})
            root.Controls.Add(tabs, 0, 2)

            Dim footer As New FlowLayoutPanel With {
                .Dock = DockStyle.Fill, .AutoSize = True, .FlowDirection = FlowDirection.RightToLeft,
                .WrapContents = False, .Padding = New Padding(0, 12, 0, 0)
            }
            Dim save As New Button With {.Text = "Save &Comment", .AutoSize = True}
            Dim cancel As New Button With {.Text = "Cancel", .AutoSize = True, .DialogResult = DialogResult.Cancel}
            AddHandler save.Click, AddressOf SaveItem
            footer.Controls.Add(save)
            footer.Controls.Add(cancel)
            root.Controls.Add(footer, 0, 3)
            AcceptButton = save
            CancelButton = cancel
            Controls.Add(root)
        End Sub

        Private Shared Sub AddMetadata(panel As TableLayoutPanel, label As String, field As Control, row As Integer)
            panel.RowStyles.Add(New RowStyle(SizeType.AutoSize))
            panel.Controls.Add(New Label With {.Text = label, .AutoSize = True, .Anchor = AnchorStyles.Left,
                                              .Margin = New Padding(0, 6, 12, 6)}, 0, row)
            field.Dock = DockStyle.Fill
            field.Margin = New Padding(0, 4, 0, 4)
            panel.Controls.Add(field, 1, row)
        End Sub

        Private Shared Function CreateTextSection(label As String, field As TextBox) As Control
            Dim panel As New TableLayoutPanel With {
                .Dock = DockStyle.Fill, .ColumnCount = 1, .RowCount = 2, .Padding = New Padding(10)
            }
            panel.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
            panel.RowStyles.Add(New RowStyle(SizeType.AutoSize))
            panel.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
            panel.Controls.Add(New Label With {.Text = label, .AutoSize = True, .Dock = DockStyle.Fill}, 0, 0)
            field.Dock = DockStyle.Fill
            field.Multiline = True
            field.ScrollBars = ScrollBars.Vertical
            field.AcceptsReturn = True
            field.AccessibleName = label
            panel.Controls.Add(field, 0, 1)
            Return panel
        End Function

        ' The response box, with the assistant's starting point below it when
        ' the assistant is turned on, and where an accepted one came from.
        Private Function CreateResponseSection() As Control
            Dim panel As TableLayoutPanel = DirectCast(CreateTextSection("Response draft for the journal", txtResponse), TableLayoutPanel)
            panel.RowCount = 3
            panel.RowStyles.Add(New RowStyle(SizeType.AutoSize))

            _assistantOn = AssistantRunner.IsTurnedOn()
            assistantRow.Dock = DockStyle.Fill
            assistantRow.AutoSize = True
            assistantRow.ColumnCount = 2
            assistantRow.RowCount = 2
            assistantRow.Margin = New Padding(0, 8, 0, 0)
            assistantRow.ColumnStyles.Add(New ColumnStyle(SizeType.AutoSize))
            assistantRow.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
            assistantRow.RowStyles.Add(New RowStyle(SizeType.AutoSize))
            assistantRow.RowStyles.Add(New RowStyle(SizeType.AutoSize))

            btnSuggest.Text = "Su&ggest a Starting Point..."
            btnSuggest.AutoSize = True
            btnSuggest.MinimumSize = New Size(0, 32)
            btnSuggest.Margin = New Padding(0, 0, 12, 0)
            btnSuggest.Visible = _assistantOn
            AddHandler btnSuggest.Click, Sub(sender, e) SuggestStartingPoint()

            lblAssistantNote.AutoSize = True
            lblAssistantNote.Dock = DockStyle.Fill
            lblAssistantNote.UseMnemonic = False
            lblAssistantNote.ForeColor = UiTheme.SecondaryText()
            lblAssistantNote.Margin = New Padding(0, 6, 0, 0)
            lblAssistantNote.Visible = _assistantOn
            If _assistantOn Then
                lblAssistantNote.Text = "Sends only the reviewer, comment, and planned action to " & AssistantDraftForm.RecipientText() & ", never your notes or draft."
            End If

            lblProvenance.AutoSize = True
            lblProvenance.Dock = DockStyle.Fill
            lblProvenance.UseMnemonic = False
            lblProvenance.ForeColor = UiTheme.MutedText()
            lblProvenance.Margin = New Padding(0, 6, 0, 0)

            assistantRow.Controls.Add(btnSuggest, 0, 0)
            assistantRow.Controls.Add(lblAssistantNote, 1, 0)
            assistantRow.Controls.Add(lblProvenance, 0, 1)
            assistantRow.SetColumnSpan(lblProvenance, 2)
            panel.Controls.Add(assistantRow, 0, 2)

            AddHandler txtResponse.TextChanged, Sub(sender, e) RefreshAssistantRow()
            Return panel
        End Function

        ' Where the response began, while it has text.
        Private Sub RefreshAssistantRow()
            Dim suggestion As AssistantSuggestion = If(_pendingSuggestion, _existing?.ResponseSuggestion)
            Dim provenance As String = If(String.IsNullOrWhiteSpace(txtResponse.Text), String.Empty, AssistantSuggestionService.Describe(suggestion))
            lblProvenance.Text = provenance
            lblProvenance.Visible = provenance.Length > 0
            assistantRow.Visible = _assistantOn OrElse provenance.Length > 0
        End Sub

        Private Sub Notify(message As String)
            If noticePrompt IsNot Nothing Then
                noticePrompt(message)
            Else
                MessageBox.Show(Me, message, AssistantService.FeatureName(AssistantService.DraftResponseFeature), MessageBoxButtons.OK, MessageBoxIcon.Information)
            End If
        End Sub

        ' Sends only the reviewer label, comment, and planned action: never
        ' the notes, the location, or the response draft. Nothing changes
        ' until the researcher uses the suggestion, and nothing is saved
        ' until Save Comment and then the page's Save.
        Private Sub SuggestStartingPoint()
            Dim reason As String = AssistantRunner.Unavailable()
            If reason.Length > 0 Then
                Notify(reason)
                Return
            End If
            If String.IsNullOrWhiteSpace(txtComment.Text) AndAlso String.IsNullOrWhiteSpace(txtAction.Text) Then
                Notify("Write the comment first.")
                tabs.SelectedIndex = 0
                txtComment.Focus()
                Return
            End If

            Dim request As AssistantRequest = AssistantService.BuildResponseRequest(txtReviewer.Text, txtComment.Text, txtAction.Text)
            If Not AssistantRunner.Confirm(Me, request) Then Return

            Using dialog As New AssistantDraftForm(request, txtResponse.Text)
                Dim answer As DialogResult = If(draftPrompt IsNot Nothing, draftPrompt(dialog), dialog.ShowDialog(Me))
                If answer <> DialogResult.OK OrElse dialog.Reply Is Nothing OrElse IsDisposed Then Return
                UseStartingPoint(dialog.ChosenText, dialog.Placement, dialog.Reply)
            End Using
        End Sub

        Private Sub UseStartingPoint(text As String, placement As DraftPlacement, reply As AssistantReply)
            Dim suggestion As String = AssistantDraftForm.TextBoxText(text).Trim()
            If suggestion.Length = 0 Then Return
            Dim start As Integer = 0
            If placement = DraftPlacement.Below AndAlso txtResponse.Text.Trim().Length > 0 Then
                Dim kept As String = txtResponse.Text.TrimEnd() & Environment.NewLine & Environment.NewLine
                start = kept.Length
                txtResponse.Text = kept & suggestion
            Else
                txtResponse.Text = suggestion
            End If
            _pendingSuggestion = AssistantService.SuggestionFor(AssistantService.DraftResponseFeature, reply, txtComment.Text, DateTime.UtcNow)
            RefreshAssistantRow()
            txtResponse.Focus()
            txtResponse.Select(start, 0)
            txtResponse.ScrollToCaret()
        End Sub

        Private Shared Function CreateTextPair(firstLabel As String, firstField As TextBox,
                                              secondLabel As String, secondField As TextBox) As Control
            Dim panel As New TableLayoutPanel With {.Dock = DockStyle.Fill, .ColumnCount = 1, .RowCount = 2}
            panel.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
            panel.RowStyles.Add(New RowStyle(SizeType.Percent, 50))
            panel.RowStyles.Add(New RowStyle(SizeType.Percent, 50))
            panel.Controls.Add(CreateTextSection(firstLabel, firstField), 0, 0)
            panel.Controls.Add(CreateTextSection(secondLabel, secondField), 0, 1)
            Return panel
        End Function

        Private Sub LoadItem()
            cmbStatus.SelectedIndex = 0
            If cmbDecision.Items.Count = 1 Then cmbDecision.SelectedIndex = 0
            If _existing Is Nothing Then
                RefreshAssistantRow()
                Return
            End If
            For index As Integer = 0 To cmbDecision.Items.Count - 1
                If DirectCast(cmbDecision.Items(index), DecisionChoice).Id = _existing.DecisionId Then
                    cmbDecision.SelectedIndex = index
                    Exit For
                End If
            Next
            For index As Integer = 0 To cmbStatus.Items.Count - 1
                If DirectCast(cmbStatus.Items(index), StatusChoice).Status = _existing.Status Then
                    cmbStatus.SelectedIndex = index
                    Exit For
                End If
            Next
            nudRound.Value = Math.Max(1, _existing.RevisionRoundNumber)
            txtReviewer.Text = _existing.ReviewerLabel
            txtComment.Text = _existing.CommentText
            txtAction.Text = _existing.ActionText
            txtResponse.Text = _existing.ResponseText
            txtLocation.Text = _existing.ManuscriptLocation
            txtNotes.Text = _existing.Notes
            RefreshAssistantRow()
        End Sub

        Private Sub SaveItem(sender As Object, e As EventArgs)
            Dim draft As ReviewerResponseItem = If(_existing Is Nothing, New ReviewerResponseItem(),
                ManuscriptCloneService.CloneReviewerResponse(_existing))
            Dim decision As DecisionChoice = TryCast(cmbDecision.SelectedItem, DecisionChoice)
            draft.DecisionId = If(decision Is Nothing, Guid.Empty, decision.Id)
            draft.RevisionRoundNumber = Decimal.ToInt32(nudRound.Value)
            draft.ReviewerLabel = txtReviewer.Text
            draft.Status = DirectCast(cmbStatus.SelectedItem, StatusChoice).Status
            draft.CommentText = txtComment.Text
            draft.ActionText = txtAction.Text
            draft.ResponseText = txtResponse.Text
            draft.ManuscriptLocation = txtLocation.Text
            draft.Notes = txtNotes.Text
            ' Where the response began: a starting point used now, else the
            ' one it already had; none once the response is empty.
            If _pendingSuggestion IsNot Nothing Then draft.ResponseSuggestion = _pendingSuggestion
            If String.IsNullOrWhiteSpace(draft.ResponseText) Then draft.ResponseSuggestion = Nothing
            Try
                ' Validation happens against a disposable copy. Cancel never edits the caller.
                Dim probe As JournalSubmission = ManuscriptCloneService.CloneSubmission(_submission)
                _editedItem = If(_existing Is Nothing, ReviewerResponseService.AddItem(probe, draft),
                    ReviewerResponseService.UpdateItem(probe, _existing.Id, draft))
            Catch ex As Exception When TypeOf ex Is ArgumentException OrElse TypeOf ex Is InvalidOperationException OrElse TypeOf ex Is InvalidDataException
                MessageBox.Show(Me, ex.Message, "Check Reviewer Comment", MessageBoxButtons.OK, MessageBoxIcon.Information)
                Return
            End Try
            DialogResult = DialogResult.OK
        End Sub

        ' For tests.
        Friend ReadOnly Property SuggestButton As Button
            Get
                Return btnSuggest
            End Get
        End Property

        Friend ReadOnly Property ProvenanceText As String
            Get
                Return lblProvenance.Text
            End Get
        End Property

        Friend Sub SuggestStartingPointForTest()
            SuggestStartingPoint()
        End Sub

        Private NotInheritable Class DecisionChoice
            Public ReadOnly Property Id As Guid
            Private ReadOnly _label As String
            Public Sub New(decision As EditorialDecisionEvent, ordinal As Integer)
                Id = decision.Id
                _label = "Decision " & ordinal.ToString() & " — " & decision.DecisionDate.ToString("MMM d, yyyy") & " — " & EditorialDecisionDisplayService.Format(decision.Decision)
            End Sub
            Public Overrides Function ToString() As String
                Return _label
            End Function
        End Class

        Friend NotInheritable Class StatusChoice
            Public ReadOnly Property Status As ReviewerResponseStatus
            Public Sub New(value As ReviewerResponseStatus)
                Status = value
            End Sub
            Public Overrides Function ToString() As String
                Return ReviewerResponseService.FormatStatus(Status)
            End Function
        End Class
    End Class
End Namespace
