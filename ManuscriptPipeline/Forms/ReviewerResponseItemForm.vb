Imports System
Imports System.Drawing
Imports System.IO
Imports System.Linq
Imports System.Windows.Forms
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

            Dim tabs As New TabControl With {.Dock = DockStyle.Fill, .AccessibleName = "Reviewer response fields"}
            Dim commentTab As New TabPage("Comment && action")
            commentTab.Controls.Add(CreateTextPair("Reviewer comment (enter a comment or action)", txtComment,
                                                  "Planned or completed action", txtAction))
            Dim responseTab As New TabPage("Draft response")
            responseTab.Controls.Add(CreateTextSection("Response draft for the journal", txtResponse))
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
            If _existing Is Nothing Then Return
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
