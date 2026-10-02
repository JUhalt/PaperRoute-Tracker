Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.Globalization
Imports System.Text.RegularExpressions
Imports System.Windows.Forms
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

Namespace Forms

    Public Class AddDecisionForm
        Inherits Form

        Private Class DecisionOption

            Public ReadOnly Property Label As String
            Public ReadOnly Property Value As EditorialDecision

            Public Sub New(label As String, value As EditorialDecision)
                Me.Label = label
                Me.Value = value
            End Sub

            Public Overrides Function ToString() As String
                Return Label
            End Function

        End Class


        Private ReadOnly _existingDecision As EditorialDecisionEvent

        Private ReadOnly _root As New TableLayoutPanel()
        Private ReadOnly bannerPanel As New TableLayoutPanel()
        Private ReadOnly lblBanner As New Label()
        Private ReadOnly txtBanner As New TextBox()
        Private ReadOnly cmbDecision As New ComboBox()
        Private ReadOnly dtpDecisionDate As New DateTimePicker()
        Private ReadOnly chkDeadline As New CheckBox()
        Private ReadOnly dtpDeadline As New DateTimePicker()
        Private ReadOnly txtNotes As New TextBox()
        Private ReadOnly lblCheck As New Label()

        Private _createdDecision As EditorialDecisionEvent

        ' Where a new decision came from, when it began as an AI suggestion
        ' from a decision letter (#84).
        Private _suggestion As AssistantSuggestion

        Private _bannerHeight As Integer

        Private _boldFont As Font


        Public ReadOnly Property CreatedDecision As EditorialDecisionEvent
            Get
                Return _createdDecision
            End Get
        End Property


        Public Sub New()

            _existingDecision = Nothing

            BuildInterface()
            UiPolish.ApplyDialog(Me)

        End Sub


        Public Sub New(existingDecision As EditorialDecisionEvent)

            _existingDecision = existingDecision

            BuildInterface()
            LoadExistingDecision()
            UiPolish.ApplyDialog(Me)

        End Sub


        Private Sub BuildInterface()

            SuspendLayout()

            If _existingDecision Is Nothing Then
                Me.Text = "Record Editorial Decision"
            Else
                Me.Text = "Edit Editorial Decision"
            End If

            Me.StartPosition = FormStartPosition.CenterParent
            Me.FormBorderStyle = FormBorderStyle.FixedDialog
            Me.MaximizeBox = False
            Me.MinimizeBox = False
            ' Sizes below are at 96 DPI and scale with the display, so the
            ' fixed rows keep their text at 125% and 150%.
            Me.AutoScaleDimensions = New SizeF(96.0F, 96.0F)
            Me.ClientSize = New Size(650, 500)
            Me.Font = New Font("Segoe UI", 10.0F)
            _boldFont = New Font(Me.Font, FontStyle.Bold)
            Me.AutoScaleMode = AutoScaleMode.Dpi

            _root.Dock = DockStyle.Fill
            _root.ColumnCount = 2
            _root.RowCount = 7
            _root.Padding = New Padding(22)

            _root.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 170))
            _root.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))

            ' The AI suggestion banner takes no room until it is shown.
            _root.RowStyles.Add(New RowStyle(SizeType.Absolute, 0))
            _root.RowStyles.Add(New RowStyle(SizeType.Absolute, 58))
            _root.RowStyles.Add(New RowStyle(SizeType.Absolute, 58))
            _root.RowStyles.Add(New RowStyle(SizeType.Absolute, 58))
            _root.RowStyles.Add(New RowStyle(SizeType.Absolute, 40))
            _root.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
            _root.RowStyles.Add(New RowStyle(SizeType.Absolute, 55))

            BuildBanner()
            _root.Controls.Add(bannerPanel, 0, 0)
            _root.SetColumnSpan(bannerPanel, 2)

            cmbDecision.Dock = DockStyle.Fill
            cmbDecision.DropDownStyle = ComboBoxStyle.DropDownList

            AddDecisionOption("Rejected", EditorialDecision.Rejected)
            AddDecisionOption("Desk Rejected", EditorialDecision.DeskRejected)
            AddDecisionOption("Rejected After Review", EditorialDecision.RejectedAfterReview)
            AddDecisionOption("Major Revision", EditorialDecision.MajorRevision)
            AddDecisionOption("Minor Revision", EditorialDecision.MinorRevision)
            AddDecisionOption("Revise and Resubmit", EditorialDecision.ReviseAndResubmit)
            AddDecisionOption("Accepted", EditorialDecision.Accepted)
            AddDecisionOption("Withdrawn", EditorialDecision.Withdrawn)

            cmbDecision.SelectedIndex = 0

            AddHandler cmbDecision.SelectedIndexChanged,
                Sub(sender, e)
                    lblCheck.Text = String.Empty
                End Sub

            _root.Controls.Add(CreateFieldLabel("Decision"), 0, 1)
            _root.Controls.Add(cmbDecision, 1, 1)

            dtpDecisionDate.Format = DateTimePickerFormat.Short
            dtpDecisionDate.Value = DateTime.Today
            dtpDecisionDate.Width = 180

            _root.Controls.Add(CreateFieldLabel("Decision date"), 0, 2)
            _root.Controls.Add(dtpDecisionDate, 1, 2)

            Dim deadlinePanel As New FlowLayoutPanel With {
                .Dock = DockStyle.Fill,
                .FlowDirection = FlowDirection.LeftToRight,
                .WrapContents = False
            }

            chkDeadline.Text = "Set deadline"
            chkDeadline.AutoSize = True

            dtpDeadline.Format = DateTimePickerFormat.Short
            dtpDeadline.Value = DateTime.Today.AddDays(30)
            dtpDeadline.Width = 180
            dtpDeadline.Enabled = False

            AddHandler chkDeadline.CheckedChanged,
                AddressOf DeadlineCheckedChanged

            deadlinePanel.Controls.Add(chkDeadline)
            deadlinePanel.Controls.Add(dtpDeadline)

            _root.Controls.Add(CreateFieldLabel("Revision deadline"), 0, 3)
            _root.Controls.Add(deadlinePanel, 1, 3)

            Dim lblNotes As New Label With {
                .Text = "Decision / Editor / Reviewer Notes",
                .AutoSize = True,
                .Anchor = AnchorStyles.Left,
                .Font = _boldFont
            }

            _root.Controls.Add(lblNotes, 0, 4)
            _root.SetColumnSpan(lblNotes, 2)

            txtNotes.Dock = DockStyle.Fill
            txtNotes.Multiline = True
            txtNotes.ScrollBars = ScrollBars.Vertical

            _root.Controls.Add(txtNotes, 0, 5)
            _root.SetColumnSpan(txtNotes, 2)

            Dim buttons As New FlowLayoutPanel With {
                .Dock = DockStyle.Fill,
                .FlowDirection = FlowDirection.RightToLeft,
                .WrapContents = False
            }

            Dim btnSave As New Button With {
                .AutoSize = True,
                .Height = 36
            }

            If _existingDecision Is Nothing Then
                btnSave.Text = "Add Decision"
            Else
                btnSave.Text = "Save Changes"
            End If

            Dim btnCancel As New Button With {
                .Text = "Cancel",
                .AutoSize = True,
                .Height = 36,
                .DialogResult = DialogResult.Cancel
            }

            AddHandler btnSave.Click,
                AddressOf SaveDecision

            ' What to fix before the decision can be saved.
            lblCheck.AutoSize = True
            lblCheck.UseMnemonic = False
            lblCheck.ForeColor = UiTheme.InfoColor()
            lblCheck.Margin = New Padding(0, 9, 12, 0)

            buttons.Controls.Add(btnSave)
            buttons.Controls.Add(btnCancel)
            buttons.Controls.Add(lblCheck)

            _root.Controls.Add(buttons, 0, 6)
            _root.SetColumnSpan(buttons, 2)

            Me.AcceptButton = btnSave
            Me.CancelButton = btnCancel

            Me.Controls.Add(_root)

            ResumeLayout(False)
            PerformLayout()

        End Sub


        Private Sub BuildBanner()

            bannerPanel.Dock = DockStyle.Fill
            bannerPanel.ColumnCount = 1
            bannerPanel.RowCount = 2
            bannerPanel.Margin = New Padding(0, 0, 0, 10)
            bannerPanel.Padding = New Padding(10, 8, 10, 10)
            bannerPanel.Visible = False
            bannerPanel.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
            bannerPanel.RowStyles.Add(New RowStyle(SizeType.AutoSize))
            bannerPanel.RowStyles.Add(New RowStyle(SizeType.Percent, 100))

            lblBanner.Text = "AI suggestion: check the decision and dates before adding."
            lblBanner.AutoSize = True
            lblBanner.UseMnemonic = False
            lblBanner.Font = _boldFont
            lblBanner.Margin = New Padding(0, 0, 0, 6)

            txtBanner.Dock = DockStyle.Fill
            txtBanner.Multiline = True
            txtBanner.ReadOnly = True
            txtBanner.WordWrap = True
            txtBanner.ScrollBars = ScrollBars.Vertical
            txtBanner.Margin = New Padding(0)
            txtBanner.AccessibleName = "What the decision letter says"

            bannerPanel.Controls.Add(lblBanner, 0, 0)
            bannerPanel.Controls.Add(txtBanner, 0, 1)

        End Sub


        Private Sub AddDecisionOption(
            label As String,
            value As EditorialDecision
        )

            cmbDecision.Items.Add(
                New DecisionOption(label, value)
            )

        End Sub


        Private Sub LoadExistingDecision()

            For i As Integer = 0 To cmbDecision.Items.Count - 1

                Dim optionItem As DecisionOption =
                    DirectCast(cmbDecision.Items(i), DecisionOption)

                If optionItem.Value = _existingDecision.Decision Then
                    cmbDecision.SelectedIndex = i
                    Exit For
                End If

            Next

            dtpDecisionDate.Value =
                _existingDecision.DecisionDate

            If _existingDecision.RevisionDeadline.HasValue Then

                chkDeadline.Checked = True

                dtpDeadline.Value =
                    _existingDecision.RevisionDeadline.Value

            End If

            txtNotes.Text =
                _existingDecision.Notes

        End Sub


        ' Fills the form from what the AI assistant read in a decision letter
        ' (#84): a proposal to check, shown under a banner with the letter's
        ' own sentences. A decision the letter doesn't state plainly is left
        ' for the researcher to choose.
        Friend Sub UseProposal(
            proposal As DecisionLetterProposal,
            letter As String,
            suggestion As AssistantSuggestion
        )

            If proposal Is Nothing Then
                Throw New ArgumentNullException(NameOf(proposal))
            End If

            _suggestion =
                ManuscriptCloneService.CloneSuggestion(suggestion)

            cmbDecision.SelectedIndex = -1

            If proposal.Decision.HasValue Then

                For i As Integer = 0 To cmbDecision.Items.Count - 1

                    If DirectCast(cmbDecision.Items(i), DecisionOption).Value = proposal.Decision.Value Then
                        cmbDecision.SelectedIndex = i
                        Exit For
                    End If

                Next

            End If

            If proposal.DecisionDate.HasValue Then

                dtpDecisionDate.Value =
                    Clamp(proposal.DecisionDate.Value, dtpDecisionDate)

            End If

            If proposal.RevisionDeadline.HasValue Then

                Dim deadline As DateTime =
                    proposal.RevisionDeadline.Value.Date

                If deadline < dtpDecisionDate.Value.Date Then
                    deadline = dtpDecisionDate.Value.Date
                End If

                dtpDeadline.Value =
                    Clamp(deadline, dtpDeadline)

                chkDeadline.Checked = True

            End If

            Dim letterText As String =
                If(letter, String.Empty).Trim()

            If txtNotes.Text.Trim().Length = 0 AndAlso letterText.Length > 0 Then

                If letterText.Length > txtNotes.MaxLength Then
                    txtNotes.MaxLength = 0
                End If

                txtNotes.Text = letterText

            End If

            ShowBanner(BannerText(proposal))

        End Sub


        ' The letter's sentences and where the dates came from.
        Private Shared Function BannerText(
            proposal As DecisionLetterProposal
        ) As String

            Dim lines As New List(Of String)()

            If Not proposal.Decision.HasValue Then
                lines.Add("Decision: not stated plainly in the letter. Choose it below.")
            ElseIf proposal.DecisionQuote.Length > 0 Then
                lines.Add("Decision: " & Quoted(proposal.DecisionQuote))
            Else
                lines.Add("Decision: " & EditorialDecisionDisplayService.Format(proposal.Decision.Value))
            End If

            If proposal.DecisionDate.HasValue Then
                lines.Add("Decision date: " & proposal.DecisionDate.Value.ToString("MMM d, yyyy", CultureInfo.CurrentCulture) & ", the letter's date")
            Else
                lines.Add("Decision date: not found in the letter; today's date is filled in.")
            End If

            If proposal.RevisionDeadline.HasValue Then
                lines.Add("Revision deadline: " & If(proposal.DeadlineBasis.Length > 0, proposal.DeadlineBasis, "from the letter") &
                          If(proposal.DeadlineQuote.Length > 0, ", from " & Quoted(proposal.DeadlineQuote), String.Empty))
            Else
                lines.Add("Revision deadline: none found in the letter.")
            End If

            Return String.Join(Environment.NewLine, lines)

        End Function


        ' Shows the banner and makes the dialog taller to hold it.
        Private Sub ShowBanner(details As String)

            txtBanner.Text = details
            bannerPanel.BackColor = UiTheme.InfoMutedBackground()
            lblBanner.ForeColor = UiTheme.PrimaryText()
            bannerPanel.Visible = True

            Dim width As Integer =
                Math.Max(LogicalToDeviceUnits(200), _root.ClientSize.Width - _root.Padding.Horizontal - bannerPanel.Padding.Horizontal)

            lblBanner.MaximumSize = New Size(width, 0)

            Dim needed As Integer =
                lblBanner.GetPreferredSize(New Size(width, 0)).Height +
                lblBanner.Margin.Vertical +
                txtBanner.Font.Height * 4 + LogicalToDeviceUnits(10) +
                bannerPanel.Padding.Vertical +
                bannerPanel.Margin.Vertical

            Dim growth As Integer =
                needed - _bannerHeight

            _bannerHeight = needed
            _root.RowStyles(0).Height = needed

            ' Taller by the banner, as far as the screen allows; the notes
            ' give up the rest.
            Dim frame As Integer = Height - ClientSize.Height
            Dim available As Integer = Screen.FromPoint(Cursor.Position).WorkingArea.Height - frame - LogicalToDeviceUnits(16)
            Dim target As Integer = Math.Min(ClientSize.Height + growth, Math.Max(ClientSize.Height, available))

            ClientSize = New Size(ClientSize.Width, target)

        End Sub


        Private Shared Function Clamp(
            value As DateTime,
            picker As DateTimePicker
        ) As DateTime

            Dim day As DateTime = value.Date

            If day < picker.MinDate Then
                Return picker.MinDate
            End If

            If day > picker.MaxDate Then
                Return picker.MaxDate
            End If

            Return day

        End Function


        Private Shared Function Quoted(value As String) As String
            Return ChrW(&H201C) & Regex.Replace(value, "\s+", " ").Trim() & ChrW(&H201D)
        End Function


        Private Function CreateFieldLabel(text As String) As Label

            Return New Label With {
                .Text = text,
                .AutoSize = True,
                .Anchor = AnchorStyles.Left,
                .Font = _boldFont
            }

        End Function


        Private Sub DeadlineCheckedChanged(
            sender As Object,
            e As EventArgs
        )

            dtpDeadline.Enabled =
                chkDeadline.Checked

        End Sub


        Private Sub SaveDecision(
            sender As Object,
            e As EventArgs
        )

            If cmbDecision.SelectedItem Is Nothing Then
                lblCheck.Text = "Choose the decision."
                cmbDecision.Focus()
                Return
            End If

            lblCheck.Text = String.Empty

            Dim selectedOption As DecisionOption =
                DirectCast(
                    cmbDecision.SelectedItem,
                    DecisionOption
                )

            Dim deadline As DateTime? = Nothing

            If chkDeadline.Checked Then
                deadline = dtpDeadline.Value.Date
            End If

            Dim decisionId As Guid

            If _existingDecision Is Nothing Then
                decisionId = Guid.NewGuid()
            Else
                decisionId = _existingDecision.Id
            End If

            _createdDecision =
                New EditorialDecisionEvent With {
                    .Id = decisionId,
                    .RecordedAtUtc =
                        If(
                            _existingDecision Is Nothing,
                            Nothing,
                            _existingDecision.RecordedAtUtc
                        ),
                    .LastModifiedAtUtc =
                        If(
                            _existingDecision Is Nothing,
                            Nothing,
                            _existingDecision.LastModifiedAtUtc
                        ),
                    .DecisionDate = dtpDecisionDate.Value.Date,
                    .Decision = selectedOption.Value,
                    .RevisionDeadline = deadline,
                    .Notes = txtNotes.Text.Trim(),
                    .Suggestion =
                        If(
                            _existingDecision Is Nothing,
                            ManuscriptCloneService.CloneSuggestion(_suggestion),
                            ManuscriptCloneService.CloneSuggestion(_existingDecision.Suggestion)
                        )
                }

            If _existingDecision Is Nothing Then

                ChronologyProvenanceService.StampCreated(
                    _createdDecision
                )

            Else

                ChronologyProvenanceService.StampModified(
                    _createdDecision
                )

            End If

            Me.DialogResult = DialogResult.OK

        End Sub


        ' For tests.
        Friend ReadOnly Property DecisionList As ComboBox
            Get
                Return cmbDecision
            End Get
        End Property

        Friend ReadOnly Property DecisionDatePicker As DateTimePicker
            Get
                Return dtpDecisionDate
            End Get
        End Property

        Friend ReadOnly Property DeadlineCheck As CheckBox
            Get
                Return chkDeadline
            End Get
        End Property

        Friend ReadOnly Property DeadlinePicker As DateTimePicker
            Get
                Return dtpDeadline
            End Get
        End Property

        Friend ReadOnly Property NotesBox As TextBox
            Get
                Return txtNotes
            End Get
        End Property

        Friend ReadOnly Property BannerBox As TextBox
            Get
                Return txtBanner
            End Get
        End Property

        Friend ReadOnly Property BannerShown As Boolean
            Get
                Return _bannerHeight > 0
            End Get
        End Property

        Friend ReadOnly Property CheckText As String
            Get
                Return lblCheck.Text
            End Get
        End Property

        Friend Sub SaveForTest()
            SaveDecision(Me, EventArgs.Empty)
        End Sub


        Protected Overrides Sub Dispose(disposing As Boolean)
            MyBase.Dispose(disposing)
            If disposing Then
                _boldFont?.Dispose()
                _boldFont = Nothing
            End If
        End Sub

    End Class

End Namespace
