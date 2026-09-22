Imports System
Imports System.Drawing
Imports System.IO
Imports System.Linq
Imports System.Text
Imports System.Windows.Forms
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

Namespace Forms
    Public Class ReviewerResponseMatrixForm
        Inherits Form

        Private ReadOnly _manuscript As Manuscript
        Private ReadOnly _source As JournalSubmission
        Private ReadOnly _working As JournalSubmission
        Private ReadOnly lstResponses As New ListBox()
        Private ReadOnly cmbStatus As New ComboBox()
        Private ReadOnly txtDetails As New TextBox()
        Private ReadOnly lblCount As New Label()
        Private ReadOnly btnAdd As New Button()
        Private ReadOnly btnEdit As New Button()
        Private ReadOnly btnRemove As New Button()
        Private ReadOnly btnUp As New Button()
        Private ReadOnly btnDown As New Button()

        Public Sub New(manuscript As Manuscript, submission As JournalSubmission)
            If submission Is Nothing Then Throw New ArgumentNullException(NameOf(submission))
            _manuscript = manuscript
            _source = submission
            _working = ManuscriptCloneService.CloneSubmission(submission)
            Text = "Reviewer Response Matrix"
            Font = New Font("Segoe UI", 10.0F)
            AutoScaleMode = AutoScaleMode.Dpi
            StartPosition = FormStartPosition.CenterParent
            Size = New Size(1040, 760)
            MinimumSize = New Size(800, 580)
            BuildInterface()
            UiPolish.ApplyDialog(Me)
            RefreshItems()
        End Sub

        Private Sub BuildInterface()
            Dim root As New TableLayoutPanel With {
                .Dock = DockStyle.Fill, .ColumnCount = 1, .RowCount = 6, .Padding = New Padding(18)
            }
            root.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
            root.RowStyles.Add(New RowStyle(SizeType.AutoSize))
            root.RowStyles.Add(New RowStyle(SizeType.AutoSize))
            root.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
            root.RowStyles.Add(New RowStyle(SizeType.AutoSize))
            root.RowStyles.Add(New RowStyle(SizeType.AutoSize))
            root.RowStyles.Add(New RowStyle(SizeType.AutoSize))
            Dim intro As New Label With {
                .Text = "Track reviewer comments, revision actions, and response drafts for this submission.",
                .Dock = DockStyle.Fill, .AutoSize = True, .Margin = New Padding(0, 0, 0, 10)
            }
            root.Controls.Add(intro, 0, 0)

            Dim filters As New TableLayoutPanel With {
                .Dock = DockStyle.Fill, .ColumnCount = 3, .RowCount = 1, .AutoSize = True,
                .Margin = New Padding(0, 0, 0, 10)
            }
            filters.ColumnStyles.Add(New ColumnStyle(SizeType.AutoSize))
            filters.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 200))
            filters.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
            filters.Controls.Add(New Label With {.Text = "Show &status", .Anchor = AnchorStyles.Left,
                                                 .AutoSize = True}, 0, 0)
            cmbStatus.DropDownStyle = ComboBoxStyle.DropDownList
            cmbStatus.Dock = DockStyle.Fill
            cmbStatus.AccessibleName = "Filter response status"
            cmbStatus.Items.Add("All statuses")
            For Each status As ReviewerResponseStatus In [Enum].GetValues(Of ReviewerResponseStatus)()
                cmbStatus.Items.Add(New ReviewerResponseItemForm.StatusChoice(status))
            Next
            cmbStatus.SelectedIndex = 0
            AddHandler cmbStatus.SelectedIndexChanged, Sub() RefreshItems(SelectedId())
            filters.Controls.Add(cmbStatus, 1, 0)
            lblCount.Dock = DockStyle.Fill
            lblCount.TextAlign = ContentAlignment.MiddleRight
            lblCount.AutoEllipsis = True
            filters.Controls.Add(lblCount, 2, 0)
            root.Controls.Add(filters, 0, 1)

            Dim body As New TableLayoutPanel With {
                .Dock = DockStyle.Fill, .ColumnCount = 2, .RowCount = 1, .Margin = New Padding(0)
            }
            body.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 42))
            body.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 58))
            body.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
            lstResponses.Dock = DockStyle.Fill
            lstResponses.IntegralHeight = False
            lstResponses.HorizontalScrollbar = True
            lstResponses.AccessibleName = "Reviewer comments"
            lstResponses.Margin = New Padding(0, 0, 10, 0)
            AddHandler lstResponses.SelectedIndexChanged, Sub() RefreshSelection()
            AddHandler lstResponses.DoubleClick, AddressOf EditItem
            txtDetails.Dock = DockStyle.Fill
            txtDetails.Multiline = True
            txtDetails.ReadOnly = True
            txtDetails.ScrollBars = ScrollBars.Vertical
            txtDetails.AccessibleName = "Selected reviewer comment details"
            txtDetails.Margin = New Padding(0)
            body.Controls.Add(lstResponses, 0, 0)
            body.Controls.Add(txtDetails, 1, 0)
            root.Controls.Add(body, 0, 2)

            Dim actions As New FlowLayoutPanel With {
                .Dock = DockStyle.Fill, .AutoSize = True, .WrapContents = True,
                .Padding = New Padding(0, 10, 0, 6), .Margin = New Padding(0)
            }
            ConfigureAction(btnAdd, "&Add Comment...", "Add reviewer comment", AddressOf AddItem)
            ConfigureAction(btnEdit, "&Edit...", "Edit reviewer comment", AddressOf EditItem)
            ConfigureAction(btnRemove, "&Remove", "Remove reviewer comment", AddressOf RemoveItem)
            ConfigureAction(btnUp, "Move &Up", "Move reviewer comment up", Sub() MoveItem(-1))
            ConfigureAction(btnDown, "Move &Down", "Move reviewer comment down", Sub() MoveItem(1))
            actions.Controls.AddRange({btnAdd, btnEdit, btnRemove, btnUp, btnDown})
            root.Controls.Add(actions, 0, 3)
            Dim saveNote As New Label With {
                .Text = "Save & Close keeps these edits in Manuscript Details. Save there to store them permanently.",
                .UseMnemonic = False, .Dock = DockStyle.Fill, .AutoSize = True,
                .Margin = New Padding(0, 0, 0, 6)
            }
            root.Controls.Add(saveNote, 0, 4)
            Dim footer As New FlowLayoutPanel With {
                .Dock = DockStyle.Fill, .AutoSize = True, .FlowDirection = FlowDirection.RightToLeft,
                .WrapContents = False, .Margin = New Padding(0), .Padding = New Padding(0, 6, 0, 0)
            }
            Dim save As New Button With {.Text = "Save && Close", .AutoSize = True, .AccessibleName = "Save reviewer responses and close"}
            Dim cancel As New Button With {.Text = "Cancel", .AutoSize = True, .DialogResult = DialogResult.Cancel}
            Dim export As New Button With {.Text = "E&xport Markdown...", .AutoSize = True}
            AddHandler save.Click, AddressOf SaveChanges
            AddHandler export.Click, AddressOf ExportResponses
            footer.Controls.AddRange({save, cancel, export})
            root.Controls.Add(footer, 0, 5)
            AcceptButton = save
            CancelButton = cancel
            Controls.Add(root)
        End Sub

        Private Shared Sub ConfigureAction(button As Button, label As String, accessibleLabel As String, handler As EventHandler)
            button.Text = label
            button.AccessibleName = accessibleLabel
            button.AutoSize = True
            AddHandler button.Click, handler
        End Sub

        Private Function SelectedItem() As ReviewerResponseItem
            Dim choice As ResponseChoice = TryCast(lstResponses.SelectedItem, ResponseChoice)
            Return If(choice Is Nothing, Nothing, choice.Item)
        End Function

        Private Function SelectedId() As Guid?
            Dim item As ReviewerResponseItem = SelectedItem()
            Return If(item Is Nothing, CType(Nothing, Guid?), item.Id)
        End Function

        Private Sub RefreshItems(Optional preferredId As Guid? = Nothing)
            lstResponses.BeginUpdate()
            lstResponses.Items.Clear()
            Dim filter As ReviewerResponseItemForm.StatusChoice = TryCast(cmbStatus.SelectedItem, ReviewerResponseItemForm.StatusChoice)
            For Each item As ReviewerResponseItem In ReviewerResponseService.GetItems(_working)
                If filter IsNot Nothing AndAlso item.Status <> filter.Status Then Continue For
                lstResponses.Items.Add(New ResponseChoice(item))
                If preferredId.HasValue AndAlso item.Id = preferredId.Value Then
                    lstResponses.SelectedIndex = lstResponses.Items.Count - 1
                End If
            Next
            If lstResponses.SelectedIndex < 0 AndAlso lstResponses.Items.Count > 0 Then lstResponses.SelectedIndex = 0
            lstResponses.EndUpdate()
            lblCount.Text = lstResponses.Items.Count.ToString() & " shown / " & _working.ReviewerResponses.Count.ToString() & " comments"
            RefreshSelection()
        End Sub

        Private Sub RefreshSelection()
            Dim item As ReviewerResponseItem = SelectedItem()
            btnAdd.Enabled = _working.Decisions.Any(Function(candidateDecision) candidateDecision IsNot Nothing)
            btnEdit.Enabled = item IsNot Nothing
            btnRemove.Enabled = item IsNot Nothing
            Dim index As Integer = If(item Is Nothing, -1, _working.ReviewerResponses.FindIndex(Function(candidate) candidate.Id = item.Id))
            ' Reordering always uses the complete stored sequence. A status filter
            ' deliberately disables it so hidden comments cannot be crossed unseen.
            btnUp.Enabled = cmbStatus.SelectedIndex = 0 AndAlso index > 0
            btnDown.Enabled = cmbStatus.SelectedIndex = 0 AndAlso index >= 0 AndAlso index < _working.ReviewerResponses.Count - 1
            If item Is Nothing Then
                txtDetails.Text = If(Not btnAdd.Enabled,
                    "Record an editorial decision in Submission Details before adding reviewer comments.",
                    If(_working.ReviewerResponses.Count = 0, "No reviewer comments yet. Choose Add Comment to begin.",
                       "No comments match this status. Choose All statuses to see the complete matrix."))
                Return
            End If
            Dim decision As EditorialDecisionEvent = _working.Decisions.FirstOrDefault(Function(candidate) candidate.Id = item.DecisionId)
            Dim details As New StringBuilder()
            details.AppendLine(item.ReviewerLabel & " — Revision round " & item.RevisionRoundNumber.ToString())
            details.AppendLine("Status: " & ReviewerResponseService.FormatStatus(item.Status))
            If decision IsNot Nothing Then
                Dim ordinal As Integer = _working.Decisions.FindIndex(Function(candidate) candidate.Id = decision.Id) + 1
                details.AppendLine("Decision " & ordinal.ToString() & ": " & decision.DecisionDate.ToString("MMM d, yyyy") & " — " & EditorialDecisionDisplayService.Format(decision.Decision))
            End If
            AppendSection(details, "COMMENT", item.CommentText)
            AppendSection(details, "ACTION", item.ActionText)
            AppendSection(details, "DRAFT RESPONSE", item.ResponseText)
            AppendSection(details, "MANUSCRIPT LOCATION", item.ManuscriptLocation)
            AppendSection(details, "WORKING NOTES", item.Notes)
            txtDetails.Text = details.ToString()
            txtDetails.SelectionStart = 0
            txtDetails.ScrollToCaret()
        End Sub

        Private Shared Sub AppendSection(builder As StringBuilder, heading As String, value As String)
            builder.AppendLine().AppendLine(heading).AppendLine(If(String.IsNullOrWhiteSpace(value), "Not recorded", value))
        End Sub

        Private Sub AddItem(sender As Object, e As EventArgs)
            Using editor As New ReviewerResponseItemForm(_working)
                If editor.ShowDialog(Me) <> DialogResult.OK Then Return
                Dim added As ReviewerResponseItem = ReviewerResponseService.AddItem(_working, editor.EditedItem)
                cmbStatus.SelectedIndex = 0
                RefreshItems(added.Id)
            End Using
        End Sub

        Private Sub EditItem(sender As Object, e As EventArgs)
            Dim item As ReviewerResponseItem = SelectedItem()
            If item Is Nothing Then Return
            Using editor As New ReviewerResponseItemForm(_working, item)
                If editor.ShowDialog(Me) <> DialogResult.OK Then Return
                Dim updated As ReviewerResponseItem = ReviewerResponseService.UpdateItem(_working, item.Id, editor.EditedItem)
                RefreshItems(updated.Id)
            End Using
        End Sub

        Private Sub RemoveItem(sender As Object, e As EventArgs)
            Dim item As ReviewerResponseItem = SelectedItem()
            If item Is Nothing Then Return
            If MessageBox.Show(Me, "Remove this reviewer comment and its response draft?", "Remove Reviewer Comment",
                               MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) <> DialogResult.Yes Then Return
            ReviewerResponseService.RemoveItem(_working, item.Id)
            RefreshItems()
        End Sub

        Private Sub MoveItem(offset As Integer)
            Dim item As ReviewerResponseItem = SelectedItem()
            If item Is Nothing OrElse cmbStatus.SelectedIndex <> 0 Then Return
            ReviewerResponseService.MoveItem(_working, item.Id, offset)
            RefreshItems(item.Id)
        End Sub

        Private Sub SaveChanges(sender As Object, e As EventArgs)
            Try
                ReviewerResponseService.NormalizeAndValidateSubmission(_working)
            Catch ex As InvalidDataException
                MessageBox.Show(Me, ex.Message, "Check Reviewer Responses", MessageBoxButtons.OK, MessageBoxIcon.Information)
                Return
            End Try
            _source.ReviewerResponses = _working.ReviewerResponses.Select(Function(item) ManuscriptCloneService.CloneReviewerResponse(item)).ToList()
            DialogResult = DialogResult.OK
        End Sub

        Private Sub ExportResponses(sender As Object, e As EventArgs)
            Dim context As Manuscript = _manuscript
            If context Is Nothing Then
                context = New Manuscript()
                context.Submissions.Add(_working)
            End If
            Using export As New ReviewerResponseExportForm(ReviewerResponseExportService.ExportMarkdown(context, _working))
                export.ShowDialog(Me)
            End Using
        End Sub

        Private NotInheritable Class ResponseChoice
            Public ReadOnly Property Item As ReviewerResponseItem
            Public Sub New(response As ReviewerResponseItem)
                Item = response
            End Sub
            Public Overrides Function ToString() As String
                Dim comment As String = If(String.IsNullOrWhiteSpace(Item.CommentText), Item.ActionText, Item.CommentText)
                comment = comment.Replace(vbCr, " ").Replace(vbLf, " ")
                Return ReviewerResponseService.FormatStatus(Item.Status) & " · " & Item.ReviewerLabel &
                    " · Round " & Item.RevisionRoundNumber.ToString() & " — " & comment
            End Function
        End Class
    End Class
End Namespace
