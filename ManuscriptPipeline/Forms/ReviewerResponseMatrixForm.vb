Imports System
Imports System.Drawing
Imports System.IO
Imports System.Linq
Imports System.Text
Imports System.Windows.Forms
Imports ManuscriptPipeline.Controls
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

        ' Inline, the matrix is a tab of the manuscript page's submission detail:
        ' it edits the submission (itself part of the page's working copy)
        ' directly, and the page's Save or Discard decides what is kept.
        Private ReadOnly _inline As Boolean

        ' Raised inline after comments are added, edited, removed, or reordered.
        Friend Event Changed As EventHandler

        Public Sub New(manuscript As Manuscript, submission As JournalSubmission)
            Me.New(manuscript, submission, inline:=False)
        End Sub

        Friend Sub New(manuscript As Manuscript, submission As JournalSubmission, inline As Boolean)
            If submission Is Nothing Then Throw New ArgumentNullException(NameOf(submission))
            _inline = inline
            _manuscript = manuscript
            _source = submission
            _working = If(inline, submission, ManuscriptCloneService.CloneSubmission(submission))
            Text = "Reviewer Response Matrix"
            Font = New Font("Segoe UI", 10.0F)
            AutoScaleMode = AutoScaleMode.Dpi
            StartPosition = FormStartPosition.CenterParent
            Size = New Size(1040, 760)
            MinimumSize = New Size(800, 580)
            BuildInterface()
            EmptyHint.Attach(
                lstResponses,
                Function()
                    If Not _working.Decisions.Any(Function(item) item IsNot Nothing) Then
                        Return "Record the editorial decision first; reviewer comments belong to a decision."
                    End If
                    Return If(_working.ReviewerResponses.Count = 0,
                              "No reviewer comments yet. Choose Add Comment... to record the first request.",
                              "No comments have this status.")
                End Function)
            UiPolish.ApplyDialog(Me)
            RefreshItems()
        End Sub

        Private Sub BuildInterface()
            Dim root As New TableLayoutPanel With {
                .Dock = DockStyle.Fill, .ColumnCount = 1, .RowCount = 6, .Padding = If(_inline, New Padding(8), New Padding(18))
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
            ' Inline, the Submissions tab already says what this is.
            If Not _inline Then root.Controls.Add(intro, 0, 0)

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

            ' Inline, the list sits above the details so both use the pane's full
            ' width; in the dialog they sit side by side.
            Dim body As New TableLayoutPanel With {
                .Dock = DockStyle.Fill, .ColumnCount = If(_inline, 1, 2), .RowCount = If(_inline, 2, 1), .Margin = New Padding(0)
            }
            If _inline Then
                body.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
                body.RowStyles.Add(New RowStyle(SizeType.Percent, 55))
                body.RowStyles.Add(New RowStyle(SizeType.Percent, 45))
            Else
                body.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 42))
                body.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 58))
                body.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
            End If
            lstResponses.Dock = DockStyle.Fill
            lstResponses.IntegralHeight = False
            lstResponses.DrawMode = DrawMode.OwnerDrawVariable
            AddHandler lstResponses.MeasureItem, Sub(sender, e) e.ItemHeight = lstResponses.Font.Height * 2 + UiTheme.Px(26, DeviceDpi)
            AddHandler lstResponses.DrawItem, AddressOf DrawResponseItem
            lstResponses.AccessibleName = "Reviewer comments"
            lstResponses.Margin = If(_inline, New Padding(0, 0, 0, 8), New Padding(0, 0, 10, 0))
            AddHandler lstResponses.SelectedIndexChanged, Sub() RefreshSelection()
            AddHandler lstResponses.DoubleClick, AddressOf EditItem
            txtDetails.Dock = DockStyle.Fill
            txtDetails.Multiline = True
            txtDetails.ReadOnly = True
            txtDetails.ScrollBars = ScrollBars.Vertical
            txtDetails.AccessibleName = "Selected reviewer comment details"
            txtDetails.Margin = New Padding(0)
            body.Controls.Add(lstResponses, 0, 0)
            If _inline Then
                body.Controls.Add(txtDetails, 0, 1)
            Else
                body.Controls.Add(txtDetails, 1, 0)
            End If
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
                .Text = If(_inline,
                    "Changes here join the manuscript's unsaved changes. Save the manuscript to store them.",
                    "Save & Close keeps these edits with the manuscript's unsaved changes. Save the manuscript to store them permanently."),
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
            If _inline Then
                footer.FlowDirection = FlowDirection.LeftToRight
                footer.Controls.Add(export)
                save.Dispose()
                cancel.Dispose()
            Else
                footer.Controls.AddRange({save, cancel, export})
                AcceptButton = save
                CancelButton = cancel
            End If
            root.Controls.Add(footer, 0, 5)
            Controls.Add(root)
        End Sub

        Private Shared Sub ConfigureAction(button As Button, label As String, accessibleLabel As String, handler As EventHandler)
            button.Text = label
            button.AccessibleName = accessibleLabel
            button.AutoSize = True
            AddHandler button.Click, handler
        End Sub

        ' Shows the submission's current comments and decisions after a change
        ' made elsewhere on the manuscript page.
        Friend Sub RefreshFromSubmission()
            RefreshItems(SelectedId())
        End Sub

        Private Sub NotifyChanged()
            If _inline Then RaiseEvent Changed(Me, EventArgs.Empty)
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

        ' The status as a pill, then the reviewer and round, then the comment.
        Private Sub DrawResponseItem(sender As Object, e As DrawItemEventArgs)

            If e.Index < 0 OrElse e.Index >= lstResponses.Items.Count Then Return

            Dim item As ReviewerResponseItem = DirectCast(lstResponses.Items(e.Index), ResponseChoice).Item
            Dim selected As Boolean = (e.State And DrawItemState.Selected) = DrawItemState.Selected
            Dim bounds As Rectangle = e.Bounds
            Dim g As Graphics = e.Graphics

            Using background As New SolidBrush(If(selected, UiTheme.AccentMutedBackground(), UiTheme.CardBackground()))
                g.FillRectangle(background, bounds)
            End Using
            If selected Then
                Using accent As New SolidBrush(UiTheme.AccentColor())
                    g.FillRectangle(accent, bounds.Left, bounds.Top, UiTheme.Px(3, DeviceDpi), bounds.Height)
                End Using
            End If
            Using divider As New Pen(UiTheme.SubtleBorder())
                g.DrawLine(divider, bounds.Left, bounds.Bottom - 1, bounds.Right, bounds.Bottom - 1)
            End Using

            Dim font As Font = lstResponses.Font
            Dim line As Integer = font.Height
            Dim inset As Integer = UiTheme.Px(UiTheme.SpaceMd, DeviceDpi)
            Dim top As Integer = bounds.Top + UiTheme.Px(9, DeviceDpi)
            Const flags As TextFormatFlags = TextFormatFlags.NoPrefix Or TextFormatFlags.NoPadding Or TextFormatFlags.SingleLine Or TextFormatFlags.EndEllipsis

            Dim fill As Color
            Dim ink As Color
            Select Case item.Status
                Case ReviewerResponseStatus.Unresolved
                    fill = UiTheme.WarningMutedBackground() : ink = UiTheme.WarningColor()
                Case ReviewerResponseStatus.InProgress
                    fill = UiTheme.AccentMutedBackground() : ink = UiTheme.AccentColor()
                Case ReviewerResponseStatus.Addressed
                    fill = UiTheme.Blend(UiTheme.SuccessColor(), UiTheme.CardBackground(), 0.85F) : ink = UiTheme.SuccessColor()
                Case Else
                    fill = UiTheme.HoverBackground() : ink = UiTheme.SecondaryText()
            End Select

            Using bold As New Font(font, FontStyle.Bold)
                Dim status As String = ReviewerResponseService.FormatStatus(item.Status)
                Dim statusSize As Size = TextRenderer.MeasureText(g, status, bold, Size.Empty, TextFormatFlags.NoPrefix Or TextFormatFlags.NoPadding Or TextFormatFlags.SingleLine)
                Dim pill As New Rectangle(bounds.Left + inset, top - UiTheme.Px(2, DeviceDpi), statusSize.Width + UiTheme.Px(14, DeviceDpi), line + UiTheme.Px(4, DeviceDpi))
                g.SmoothingMode = Drawing2D.SmoothingMode.AntiAlias
                Using path As Drawing2D.GraphicsPath = RoundedShapes.Create(pill, pill.Height / 2.0F)
                    Using brush As New SolidBrush(fill)
                        g.FillPath(brush, path)
                    End Using
                End Using
                g.SmoothingMode = Drawing2D.SmoothingMode.Default
                TextRenderer.DrawText(g, status, bold, pill, ink, flags Or TextFormatFlags.HorizontalCenter Or TextFormatFlags.VerticalCenter)

                Dim who As String = item.ReviewerLabel & "  " & ChrW(&HB7) & "  Round " & item.RevisionRoundNumber.ToString()
                Dim whoLeft As Integer = pill.Right + UiTheme.Px(UiTheme.SpaceSm, DeviceDpi)
                TextRenderer.DrawText(g, who, bold, New Rectangle(whoLeft, top, Math.Max(0, bounds.Right - inset - whoLeft), line), UiTheme.PrimaryText(), flags)
            End Using

            Dim comment As String = If(String.IsNullOrWhiteSpace(item.CommentText), item.ActionText, item.CommentText)
            comment = If(comment, String.Empty).Replace(vbCr, " ").Replace(vbLf, " ")
            TextRenderer.DrawText(g, comment, font,
                New Rectangle(bounds.Left + inset, top + line + UiTheme.Px(8, DeviceDpi), Math.Max(0, bounds.Width - inset * 2), line),
                UiTheme.SecondaryText(), flags)

            e.DrawFocusRectangle()

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
                    "Record an editorial decision under Editorial History before adding reviewer comments.",
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
                NotifyChanged()
            End Using
        End Sub

        Private Sub EditItem(sender As Object, e As EventArgs)
            Dim item As ReviewerResponseItem = SelectedItem()
            If item Is Nothing Then Return
            Using editor As New ReviewerResponseItemForm(_working, item)
                If editor.ShowDialog(Me) <> DialogResult.OK Then Return
                Dim updated As ReviewerResponseItem = ReviewerResponseService.UpdateItem(_working, item.Id, editor.EditedItem)
                RefreshItems(updated.Id)
                NotifyChanged()
            End Using
        End Sub

        Private Sub RemoveItem(sender As Object, e As EventArgs)
            Dim item As ReviewerResponseItem = SelectedItem()
            If item Is Nothing Then Return
            If MessageBox.Show(Me, "Remove this reviewer comment and its response draft?", "Remove Reviewer Comment",
                               MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) <> DialogResult.Yes Then Return
            ReviewerResponseService.RemoveItem(_working, item.Id)
            RefreshItems()
            NotifyChanged()
        End Sub

        Private Sub MoveItem(offset As Integer)
            Dim item As ReviewerResponseItem = SelectedItem()
            If item Is Nothing OrElse cmbStatus.SelectedIndex <> 0 Then Return
            ReviewerResponseService.MoveItem(_working, item.Id, offset)
            RefreshItems(item.Id)
            NotifyChanged()
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
