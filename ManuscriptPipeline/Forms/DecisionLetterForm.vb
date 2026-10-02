Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.Globalization
Imports System.Linq
Imports System.Text.RegularExpressions
Imports System.Threading
Imports System.Threading.Tasks
Imports System.Windows.Forms
Imports ManuscriptPipeline.Controls
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

Namespace Forms

    ' Where the comments read from a decision letter go (#84).
    Friend Enum DecisionLetterMode
        ' From Editorial History: the decision itself and its comments.
        NewDecision
        ' From the reviewer response matrix: comments for a recorded decision.
        ExistingDecision
    End Enum


    ' A comment the researcher accepted from a decision letter.
    Friend NotInheritable Class AcceptedLetterComment

        Public Property ReviewerLabel As String = String.Empty

        Public Property Text As String = String.Empty

        ' The letter's own text the comment came from; empty when the
        ' comment isn't in the letter, so nothing else is recorded as its
        ' source.
        Public Property SourceExcerpt As String = String.Empty

    End Class


    ' One proposed comment as the researcher reviews it.
    Friend NotInheritable Class LetterCommentRow

        Public Property Candidate As LetterCommentCandidate

        Public Property Use As Boolean

        Public Property ReviewerLabel As String = String.Empty

        Public Property Text As String = String.Empty

        ' The same reviewer and text are already in the submission's matrix.
        Public Property AlreadyInMatrix As Boolean

        Public ReadOnly Property StatusText As String
            Get
                If AlreadyInMatrix Then Return DecisionLetterForm.AlreadyInMatrixText
                Return If(Candidate IsNot Nothing AndAlso Candidate.Found, DecisionLetterForm.InLetterText, DecisionLetterForm.NotFoundText)
            End Get
        End Property

    End Class


    ' Read Decision Letter (#84): the researcher pastes a decision letter,
    ' sees exactly what will be sent and to whom, and gets back the decision,
    ' its dates, and the reviewers' comments as a proposal to check. Nothing
    ' in the library changes here: the caller adds what was accepted.
    Friend Class DecisionLetterForm
        Inherits Form

        Friend Const InLetterText As String = "In the letter"
        Friend Const NotFoundText As String = "Not found in the letter: check it"
        Friend Const AlreadyInMatrixText As String = "Already in the matrix"

        Private Const ColumnUse As String = "Use"
        Private Const ColumnReviewer As String = "Reviewer"
        Private Const ColumnComment As String = "Comment"
        Private Const ColumnStatus As String = "Status"

        Private Enum Stage
            Paste
            Running
            Review
        End Enum

        ' Test seam: shows one step of a letter flow (this window or the
        ' decision that follows it) instead of ShowDialog.
        Friend Shared DialogRunner As Func(Of Form, IWin32Window, DialogResult) = Nothing

        ' Test seam: the day the answer's dates are checked against, for
        ' windows opened by other windows.
        Friend Shared TodayOverride As DateTime? = Nothing


        ' Shows a step of a letter flow modally, or through the test seam.
        Friend Shared Function RunDialog(dialog As Form, owner As IWin32Window) As DialogResult
            If DialogRunner IsNot Nothing Then Return DialogRunner(dialog, owner)
            Return dialog.ShowDialog(owner)
        End Function


        Private ReadOnly _mode As DecisionLetterMode
        Private ReadOnly _submission As JournalSubmission
        Private ReadOnly _today As DateTime
        Private ReadOnly _rows As New List(Of LetterCommentRow)()
        Private ReadOnly _accepted As New List(Of AcceptedLetterComment)()
        Private _stage As Stage = Stage.Paste
        Private _cancellation As CancellationTokenSource
        Private _closeWhenStopped As Boolean
        Private _proposal As DecisionLetterProposal
        Private _letter As String = String.Empty
        Private _chosenDecisionId As Guid?
        Private _round As Integer = 1
        Private _loadingRows As Boolean
        Private _loadingComment As Boolean
        Private _formFont As Font
        Private _boldFont As Font

        Private ReadOnly lblIntro As New Label()
        Private ReadOnly body As New Panel()
        Private ReadOnly pastePanel As New TableLayoutPanel()
        Private ReadOnly txtLetter As New TextBox()
        Private ReadOnly lnkSent As New LinkLabel()
        Private ReadOnly txtSent As New TextBox()
        Private ReadOnly reviewPanel As New TableLayoutPanel()
        Private ReadOnly proposalsPanel As New TableLayoutPanel()
        Private ReadOnly txtSource As New TextBox()
        Private ReadOnly lblDecision As New Label()
        Private ReadOnly lblDecisionDetails As New Label()
        Private ReadOnly lblDecisionChoice As New Label()
        Private ReadOnly cmbDecision As New ComboBox()
        Private ReadOnly nudRound As New NumericUpDown()
        Private ReadOnly lblTruncated As New Label()
        Private ReadOnly gridComments As New DataGridView()
        Private ReadOnly txtComment As New TextBox()
        Private ReadOnly lblStatus As New Label()
        Private ReadOnly btnPrimary As New Button()
        Private ReadOnly btnBack As New Button()
        Private ReadOnly btnCancel As New Button()


        ' letter: text to start with, such as a decision's notes; today
        ' bounds the dates the answer may give.
        Public Sub New(mode As DecisionLetterMode, submission As JournalSubmission, Optional letter As String = Nothing, Optional today As DateTime? = Nothing)

            If submission Is Nothing Then Throw New ArgumentNullException(NameOf(submission))
            _mode = mode
            _submission = submission
            _today = If(today, If(TodayOverride, DateTime.Today)).Date

            BuildInterface()
            If Not String.IsNullOrWhiteSpace(letter) Then txtLetter.Text = ForTextBox(letter).Trim()
            ShowPaste()
            UiPolish.ApplyDialog(Me)

        End Sub


        Protected Overrides Sub OnLoad(e As EventArgs)
            MyBase.OnLoad(e)
            ResponsiveDialogSizingService.FitToWorkingArea(Me)
        End Sub


        ' ---------------------------------------------------------------
        ' What the caller adds
        ' ---------------------------------------------------------------

        ' What the assistant read, after the researcher accepts.
        Friend ReadOnly Property Proposal As DecisionLetterProposal
            Get
                Return _proposal
            End Get
        End Property

        ' The comments the researcher checked, as edited, in order.
        Friend ReadOnly Property AcceptedComments As IReadOnlyList(Of AcceptedLetterComment)
            Get
                Return _accepted.AsReadOnly()
            End Get
        End Property

        Friend ReadOnly Property RevisionRound As Integer
            Get
                Return _round
            End Get
        End Property

        ' The recorded decision the comments belong to (from the matrix).
        Friend ReadOnly Property ChosenDecisionId As Guid?
            Get
                Return _chosenDecisionId
            End Get
        End Property

        ' The letter exactly as it was sent.
        Friend ReadOnly Property LetterText As String
            Get
                Return _letter
            End Get
        End Property


        ' Where the decision came from: the letter's sentences stating the
        ' decision and the deadline.
        Friend Function DecisionSuggestion() As AssistantSuggestion
            If _proposal Is Nothing Then Return Nothing
            Dim quotes As IEnumerable(Of String) = {_proposal.DecisionQuote, _proposal.DeadlineQuote}.Where(Function(quote) Not String.IsNullOrWhiteSpace(quote))
            Return AssistantService.SuggestionFor(AssistantService.DecisionLetterFeature, ReplyOf(_proposal), String.Join(Environment.NewLine, quotes), _proposal.SuggestedUtc)
        End Function


        ' The accepted comments as matrix items for a decision, each
        ' recording the letter's text it came from.
        Friend Function DraftItems(decisionId As Guid) As List(Of ReviewerResponseItem)
            Dim drafts As New List(Of ReviewerResponseItem)()
            If _proposal Is Nothing Then Return drafts
            Dim reply As AssistantReply = ReplyOf(_proposal)
            For Each comment As AcceptedLetterComment In _accepted
                drafts.Add(New ReviewerResponseItem With {
                    .DecisionId = decisionId,
                    .RevisionRoundNumber = _round,
                    .ReviewerLabel = comment.ReviewerLabel,
                    .CommentText = comment.Text,
                    .Status = ReviewerResponseStatus.Unresolved,
                    .CommentSuggestion = AssistantService.SuggestionFor(AssistantService.DecisionLetterFeature, reply, comment.SourceExcerpt, _proposal.SuggestedUtc)
                })
            Next
            Return drafts
        End Function


        Private Shared Function ReplyOf(read As DecisionLetterProposal) As AssistantReply
            Return New AssistantReply With {.ProviderName = read.ProviderName, .Model = read.Model}
        End Function


        ' ---------------------------------------------------------------
        ' Interface
        ' ---------------------------------------------------------------

        Private Sub BuildInterface()

            SuspendLayout()

            Text = If(_mode = DecisionLetterMode.NewDecision, "Read Decision Letter", "Add Comments from a Decision Letter")
            StartPosition = FormStartPosition.CenterParent
            ShowInTaskbar = False
            MinimizeBox = False
            ' Sizes below are at 96 DPI and scale with the display.
            AutoScaleDimensions = New SizeF(96.0F, 96.0F)
            ClientSize = New Size(1060, 700)
            MinimumSize = New Size(880, 620)
            _formFont = New Font("Segoe UI", 10.0F)
            Font = _formFont
            _boldFont = New Font(_formFont, FontStyle.Bold)
            AutoScaleMode = AutoScaleMode.Dpi

            Dim root As New TableLayoutPanel With {.Dock = DockStyle.Fill, .ColumnCount = 1, .RowCount = 4, .Padding = New Padding(18, 16, 18, 14)}
            root.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
            root.RowStyles.Add(New RowStyle(SizeType.AutoSize))
            root.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
            root.RowStyles.Add(New RowStyle(SizeType.AutoSize))
            root.RowStyles.Add(New RowStyle(SizeType.AutoSize))

            lblIntro.AutoSize = True
            lblIntro.UseMnemonic = False
            lblIntro.Margin = New Padding(0, 0, 0, 10)

            BuildPastePanel()
            BuildReviewPanel()

            body.Dock = DockStyle.Fill
            body.Margin = New Padding(0)
            body.Controls.Add(reviewPanel)
            body.Controls.Add(pastePanel)

            lblStatus.AutoSize = True
            lblStatus.UseMnemonic = False
            lblStatus.Margin = New Padding(0, 8, 0, 0)

            Dim buttons As New FlowLayoutPanel With {.AutoSize = True, .FlowDirection = FlowDirection.RightToLeft, .WrapContents = False, .Dock = DockStyle.Fill, .Margin = New Padding(0, 12, 0, 0)}
            btnPrimary.AutoSize = True
            btnPrimary.MinimumSize = New Size(96, 34)
            AddHandler btnPrimary.Click, Async Sub(sender, e) Await PrimaryAsync()
            btnCancel.Text = "Cancel"
            btnCancel.AutoSize = True
            btnCancel.MinimumSize = New Size(96, 34)
            btnCancel.Margin = New Padding(0, 0, 8, 0)
            AddHandler btnCancel.Click, AddressOf CancelClicked
            btnBack.Text = "Read Again"
            btnBack.AutoSize = True
            btnBack.MinimumSize = New Size(96, 34)
            btnBack.Margin = New Padding(0, 0, 8, 0)
            btnBack.AccessibleName = "Read the letter again"
            AddHandler btnBack.Click, Sub(sender, e) ShowPaste()
            buttons.Controls.Add(btnPrimary)
            buttons.Controls.Add(btnCancel)
            buttons.Controls.Add(btnBack)
            ' Esc stops a request, or closes; a running request vetoes closing.
            ' The button's own handler decides, so Stop never closes the window.
            CancelButton = btnCancel
            btnCancel.DialogResult = DialogResult.None
            AcceptButton = btnPrimary

            root.Controls.Add(lblIntro, 0, 0)
            root.Controls.Add(body, 0, 1)
            root.Controls.Add(lblStatus, 0, 2)
            root.Controls.Add(buttons, 0, 3)
            Controls.Add(root)

            AddHandler root.SizeChanged,
                Sub(sender, e)
                    Dim width As Integer = Math.Max(LogicalToDeviceUnits(300), root.ClientSize.Width - root.Padding.Horizontal - LogicalToDeviceUnits(4))
                    lblIntro.MaximumSize = New Size(width, 0)
                    lblStatus.MaximumSize = New Size(width, 0)
                End Sub
            AddHandler proposalsPanel.SizeChanged,
                Sub(sender, e)
                    Dim width As Integer = Math.Max(LogicalToDeviceUnits(200), proposalsPanel.ClientSize.Width - LogicalToDeviceUnits(4))
                    For Each label As Label In {lblDecision, lblDecisionDetails, lblTruncated}
                        label.MaximumSize = New Size(width, 0)
                    Next
                End Sub

            ResumeLayout(False)
            PerformLayout()

        End Sub


        Private Sub BuildPastePanel()

            pastePanel.Dock = DockStyle.Fill
            pastePanel.ColumnCount = 1
            pastePanel.RowCount = 4
            pastePanel.Margin = New Padding(0)
            pastePanel.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
            pastePanel.RowStyles.Add(New RowStyle(SizeType.AutoSize))
            pastePanel.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
            pastePanel.RowStyles.Add(New RowStyle(SizeType.AutoSize))
            pastePanel.RowStyles.Add(New RowStyle(SizeType.AutoSize))

            Dim heading As New Label With {.Text = "&Paste the decision letter", .AutoSize = True, .Font = _boldFont, .Margin = New Padding(0, 0, 0, 4)}

            txtLetter.Dock = DockStyle.Fill
            txtLetter.Multiline = True
            txtLetter.AcceptsReturn = True
            txtLetter.ScrollBars = ScrollBars.Vertical
            txtLetter.WordWrap = True
            ' A letter may be longer than a text box allows by default; the
            ' length is checked before anything is sent.
            txtLetter.MaxLength = 0
            txtLetter.AccessibleName = "Paste the decision letter"
            AddHandler txtLetter.TextChanged, Sub(sender, e) RefreshPaste()

            lnkSent.Text = "Show what will be sent"
            lnkSent.AutoSize = True
            lnkSent.Margin = New Padding(0, 6, 0, 0)
            AddHandler lnkSent.LinkClicked, Sub(sender, e) ToggleSent()

            txtSent.ReadOnly = True
            txtSent.Multiline = True
            txtSent.WordWrap = True
            txtSent.ScrollBars = ScrollBars.Vertical
            txtSent.Visible = False
            txtSent.Dock = DockStyle.Top
            txtSent.Height = 150
            txtSent.Margin = New Padding(0, 4, 0, 0)
            txtSent.AccessibleName = "What Read Letter will send"

            pastePanel.Controls.Add(heading, 0, 0)
            pastePanel.Controls.Add(txtLetter, 0, 1)
            pastePanel.Controls.Add(lnkSent, 0, 2)
            pastePanel.Controls.Add(txtSent, 0, 3)

        End Sub


        Private Sub BuildReviewPanel()

            reviewPanel.Dock = DockStyle.Fill
            reviewPanel.ColumnCount = 2
            reviewPanel.RowCount = 1
            reviewPanel.Margin = New Padding(0)
            reviewPanel.Visible = False
            reviewPanel.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 40))
            reviewPanel.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 60))
            reviewPanel.RowStyles.Add(New RowStyle(SizeType.Percent, 100))

            ' The letter, where selecting a comment shows its source.
            Dim letterSide As New TableLayoutPanel With {.Dock = DockStyle.Fill, .ColumnCount = 1, .RowCount = 2, .Margin = New Padding(0, 0, 14, 0)}
            letterSide.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
            letterSide.RowStyles.Add(New RowStyle(SizeType.AutoSize))
            letterSide.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
            letterSide.Controls.Add(New Label With {.Text = "The letter", .AutoSize = True, .UseMnemonic = False, .Font = _boldFont, .Margin = New Padding(0, 0, 0, 4)}, 0, 0)
            txtSource.Dock = DockStyle.Fill
            txtSource.Multiline = True
            txtSource.ReadOnly = True
            txtSource.WordWrap = True
            txtSource.ScrollBars = ScrollBars.Vertical
            txtSource.HideSelection = False
            txtSource.MaxLength = 0
            txtSource.AccessibleName = "The decision letter as sent"
            letterSide.Controls.Add(txtSource, 0, 1)

            ' What the assistant proposes.
            proposalsPanel.Dock = DockStyle.Fill
            proposalsPanel.ColumnCount = 1
            proposalsPanel.RowCount = 8
            proposalsPanel.Margin = New Padding(0)
            proposalsPanel.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
            For Each style As RowStyle In {
                New RowStyle(SizeType.AutoSize),
                New RowStyle(SizeType.AutoSize),
                New RowStyle(SizeType.AutoSize),
                New RowStyle(SizeType.AutoSize),
                New RowStyle(SizeType.AutoSize),
                New RowStyle(SizeType.Percent, 60),
                New RowStyle(SizeType.AutoSize),
                New RowStyle(SizeType.Percent, 40)
            }
                proposalsPanel.RowStyles.Add(style)
            Next

            lblDecision.AutoSize = True
            lblDecision.UseMnemonic = False
            lblDecision.Font = _boldFont
            lblDecision.Margin = New Padding(0, 0, 0, 2)
            lblDecisionDetails.AutoSize = True
            lblDecisionDetails.UseMnemonic = False
            lblDecisionDetails.ForeColor = UiTheme.SecondaryText()
            lblDecisionDetails.Margin = New Padding(0, 0, 0, 8)
            lblDecision.Visible = _mode = DecisionLetterMode.NewDecision
            lblDecisionDetails.Visible = _mode = DecisionLetterMode.NewDecision

            ' Where the comments go: the decision (from the matrix) and round.
            Dim target As New TableLayoutPanel With {.Dock = DockStyle.Fill, .AutoSize = True, .ColumnCount = 2, .RowCount = 2, .Margin = New Padding(0, 0, 0, 4)}
            target.ColumnStyles.Add(New ColumnStyle(SizeType.AutoSize))
            target.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
            target.RowStyles.Add(New RowStyle(SizeType.AutoSize))
            target.RowStyles.Add(New RowStyle(SizeType.AutoSize))
            lblDecisionChoice.Text = "Editorial &decision"
            lblDecisionChoice.AutoSize = True
            lblDecisionChoice.Anchor = AnchorStyles.Left
            lblDecisionChoice.Margin = New Padding(0, 6, 12, 6)
            cmbDecision.DropDownStyle = ComboBoxStyle.DropDownList
            cmbDecision.Dock = DockStyle.Fill
            cmbDecision.Margin = New Padding(0, 4, 0, 4)
            cmbDecision.AccessibleName = "Editorial decision"
            Dim decisions As List(Of EditorialDecisionEvent) = If(_submission.Decisions, New List(Of EditorialDecisionEvent)()).Where(Function(item) item IsNot Nothing).ToList()
            For Each decision As EditorialDecisionEvent In decisions
                cmbDecision.Items.Add(New DecisionChoice(decision, cmbDecision.Items.Count + 1))
            Next
            If cmbDecision.Items.Count = 1 Then cmbDecision.SelectedIndex = 0
            AddHandler cmbDecision.SelectedIndexChanged, Sub(sender, e) RefreshReview()
            lblDecisionChoice.Visible = _mode = DecisionLetterMode.ExistingDecision
            cmbDecision.Visible = _mode = DecisionLetterMode.ExistingDecision
            Dim lblRound As New Label With {.Text = "Revision &round", .AutoSize = True, .Anchor = AnchorStyles.Left, .Margin = New Padding(0, 6, 12, 6)}
            nudRound.Minimum = 1
            nudRound.Maximum = 999
            nudRound.Value = 1
            ' A minimum, so an early layout in a still-empty panel can't
            ' collapse it before the window scales for the display.
            nudRound.MinimumSize = New Size(90, 0)
            nudRound.Width = 90
            nudRound.Anchor = AnchorStyles.Left
            nudRound.Margin = New Padding(0, 4, 0, 4)
            nudRound.AccessibleName = "Revision round"
            target.Controls.Add(lblDecisionChoice, 0, 0)
            target.Controls.Add(cmbDecision, 1, 0)
            target.Controls.Add(lblRound, 0, 1)
            target.Controls.Add(nudRound, 1, 1)

            lblTruncated.Text = "The answer was cut short, so some comments may be missing."
            lblTruncated.AutoSize = True
            lblTruncated.UseMnemonic = False
            lblTruncated.ForeColor = UiTheme.WarningColor()
            lblTruncated.Margin = New Padding(0, 4, 0, 0)
            lblTruncated.Visible = False

            Dim lblComments As New Label With {.Text = "Reviewer comments", .AutoSize = True, .UseMnemonic = False, .Font = _boldFont, .Margin = New Padding(0, 8, 0, 4)}

            ConfigureGrid()

            Dim lblEditor As New Label With {.Text = "Selected &comment (edit it before adding)", .AutoSize = True, .Margin = New Padding(0, 8, 0, 4)}
            txtComment.Dock = DockStyle.Fill
            txtComment.Multiline = True
            txtComment.AcceptsReturn = True
            txtComment.WordWrap = True
            txtComment.ScrollBars = ScrollBars.Vertical
            txtComment.AccessibleName = "Selected comment"
            AddHandler txtComment.TextChanged, AddressOf CommentEdited

            proposalsPanel.Controls.Add(lblDecision, 0, 0)
            proposalsPanel.Controls.Add(lblDecisionDetails, 0, 1)
            proposalsPanel.Controls.Add(target, 0, 2)
            proposalsPanel.Controls.Add(lblTruncated, 0, 3)
            proposalsPanel.Controls.Add(lblComments, 0, 4)
            proposalsPanel.Controls.Add(gridComments, 0, 5)
            proposalsPanel.Controls.Add(lblEditor, 0, 6)
            proposalsPanel.Controls.Add(txtComment, 0, 7)

            reviewPanel.Controls.Add(letterSide, 0, 0)
            reviewPanel.Controls.Add(proposalsPanel, 1, 0)

        End Sub


        Private Sub ConfigureGrid()

            gridComments.Dock = DockStyle.Fill
            gridComments.AllowUserToAddRows = False
            gridComments.AllowUserToDeleteRows = False
            gridComments.AllowUserToResizeRows = False
            gridComments.AllowUserToOrderColumns = False
            gridComments.RowHeadersVisible = False
            gridComments.SelectionMode = DataGridViewSelectionMode.FullRowSelect
            gridComments.MultiSelect = False
            gridComments.EditMode = DataGridViewEditMode.EditOnKeystrokeOrF2
            gridComments.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None
            gridComments.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize
            gridComments.AccessibleName = "Reviewer comments the assistant found"

            gridComments.Columns.Add(New DataGridViewCheckBoxColumn With {
                .Name = ColumnUse, .HeaderText = "Use",
                .AutoSizeMode = DataGridViewAutoSizeColumnMode.ColumnHeader,
                .SortMode = DataGridViewColumnSortMode.NotSortable
            })
            gridComments.Columns.Add(New DataGridViewTextBoxColumn With {
                .Name = ColumnReviewer, .HeaderText = "Reviewer", .MaxInputLength = 80,
                .AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, .FillWeight = 24, .MinimumWidth = 80,
                .SortMode = DataGridViewColumnSortMode.NotSortable
            })
            gridComments.Columns.Add(New DataGridViewTextBoxColumn With {
                .Name = ColumnComment, .HeaderText = "Comment", .ReadOnly = True,
                .AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, .FillWeight = 76, .MinimumWidth = 120,
                .SortMode = DataGridViewColumnSortMode.NotSortable
            })
            gridComments.Columns.Add(New DataGridViewTextBoxColumn With {
                .Name = ColumnStatus, .HeaderText = "Status", .ReadOnly = True,
                .AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells, .MinimumWidth = 100,
                .SortMode = DataGridViewColumnSortMode.NotSortable
            })

            ' A check box counts as soon as it is clicked.
            AddHandler gridComments.CurrentCellDirtyStateChanged,
                Sub(sender, e)
                    If gridComments.IsCurrentCellDirty AndAlso TypeOf gridComments.CurrentCell Is DataGridViewCheckBoxCell Then
                        gridComments.CommitEdit(DataGridViewDataErrorContexts.Commit)
                    End If
                End Sub
            AddHandler gridComments.CellValueChanged, AddressOf GridCellChanged
            AddHandler gridComments.SelectionChanged, Sub(sender, e) ShowSelectedComment()

            EmptyHint.Attach(gridComments, "No reviewer comments were found in the letter.")

        End Sub


        ' ---------------------------------------------------------------
        ' Paste
        ' ---------------------------------------------------------------

        Private Sub ShowPaste()

            _stage = Stage.Paste
            reviewPanel.Visible = False
            pastePanel.Visible = True
            lblIntro.Text = "Read Letter sends only the letter's text to " & Recipient() & ", with PaperRoute's instructions. Nothing else from your library is sent."
            btnBack.Visible = False
            btnPrimary.Text = "Read Letter"
            btnCancel.Text = "Cancel"
            txtLetter.ReadOnly = False
            AcceptButton = btnPrimary
            ShowStatus(String.Empty)
            RefreshPaste()
            ActiveControl = txtLetter
            ' The caret starts at the beginning, so typing never replaces a
            ' letter already there.
            txtLetter.Select(0, 0)

        End Sub


        Private Sub RefreshPaste()

            If _stage <> Stage.Paste Then Return
            Dim pasted As String = PastedLetter()
            Dim tooLong As Boolean = pasted.Length > AssistantService.MaximumLetterLength
            btnPrimary.Enabled = pasted.Length > 0 AndAlso Not tooLong
            If tooLong Then
                ShowStatus("The letter is longer than PaperRoute sends at once. Paste the reviewers' comments in parts.", UiTheme.InfoColor())
            ElseIf pasted.Length = 0 Then
                ShowStatus("Paste the letter from the journal's email or website, with the reviewers' comments.")
            ElseIf lblStatus.Text.StartsWith("The letter is longer", StringComparison.Ordinal) OrElse lblStatus.Text.StartsWith("Paste the letter", StringComparison.Ordinal) Then
                ShowStatus(String.Empty)
            End If
            If txtSent.Visible Then txtSent.Text = SentText(pasted)

        End Sub


        Private Sub ToggleSent()
            txtSent.Visible = Not txtSent.Visible
            lnkSent.Text = If(txtSent.Visible, "Hide what will be sent", "Show what will be sent")
            If txtSent.Visible Then txtSent.Text = SentText(PastedLetter())
        End Sub


        Private Shared Function SentText(letter As String) As String
            Return ForTextBox(AssistantService.WhatIsSent(AssistantService.BuildLetterRequest(letter)))
        End Function


        ' The pasted letter with its line breaks made Windows', so the places
        ' the answer gives are places in the letter shown for review.
        Private Function PastedLetter() As String
            Return ForTextBox(txtLetter.Text).Trim()
        End Function


        Private Shared Function ForTextBox(value As String) As String
            Return Regex.Replace(If(value, String.Empty), "\r\n|\r|\n", vbCrLf)
        End Function


        ' Who the letter goes to, as the researcher set it up.
        Private Shared Function Recipient() As String
            If AssistantService.ProviderFactory IsNot Nothing Then
                Dim provider As IAssistantProvider = AssistantService.CurrentProvider()
                If provider IsNot Nothing AndAlso Not String.IsNullOrEmpty(provider.ProviderName) Then Return provider.ProviderName
            End If
            Dim connection As AssistantConnection = OnlineAccess.CurrentAssistant()
            Return If(connection Is Nothing, "the AI assistant", connection.Recipient)
        End Function


        ' ---------------------------------------------------------------
        ' Reading the letter
        ' ---------------------------------------------------------------

        Private Async Function PrimaryAsync() As Task
            Select Case _stage
                Case Stage.Paste : Await ReadLetterAsync()
                Case Stage.Review : AcceptReview()
            End Select
        End Function


        Friend Async Function ReadLetterAsync() As Task

            If _stage <> Stage.Paste OrElse _cancellation IsNot Nothing Then Return
            Dim letter As String = PastedLetter()
            If letter.Length = 0 OrElse letter.Length > AssistantService.MaximumLetterLength Then Return

            Dim reason As String = AssistantRunner.Unavailable()
            If reason.Length > 0 Then
                ShowStatus(reason, UiTheme.InfoColor())
                Return
            End If

            Dim request As AssistantRequest = AssistantService.BuildLetterRequest(letter)
            If Not AssistantRunner.Confirm(Me, request) Then
                ShowStatus("Nothing was sent.")
                Return
            End If
            Dim provider As IAssistantProvider = AssistantService.CurrentProvider()
            If provider Is Nothing Then
                ShowStatus("Set up the AI assistant in Settings > Preferences > AI assistant first.", UiTheme.InfoColor())
                Return
            End If

            Dim cancellation As New CancellationTokenSource()
            _cancellation = cancellation
            _stage = Stage.Running
            txtLetter.ReadOnly = True
            btnPrimary.Enabled = False
            btnCancel.Text = "Stop"
            ShowStatus("Reading the letter with " & Recipient() & "...")

            Try
                Dim reply As AssistantReply = Await provider.CompleteAsync(request, cancellation.Token)
                If IsDisposed Then Return
                cancellation.Token.ThrowIfCancellationRequested()
                Dim found As DecisionLetterProposal = AssistantService.ReadLetterReply(reply, letter, _today)
                If String.IsNullOrEmpty(found.ProviderName) Then found.ProviderName = If(provider.ProviderName, String.Empty)
                If String.IsNullOrEmpty(found.Model) Then found.Model = If(provider.Model, String.Empty)
                _letter = letter
                ShowReview(found)
            Catch ex As OperationCanceledException When cancellation.IsCancellationRequested
                If IsDisposed Then Return
                ShowPaste()
                ShowStatus("Stopped. Nothing was changed.")
            Catch ex As Exception
                If IsDisposed Then Return
                ShowPaste()
                ShowStatus(AssistantRunner.Describe(ex), UiTheme.InfoColor())
            Finally
                cancellation.Dispose()
                _cancellation = Nothing
                If Not IsDisposed Then
                    If _stage = Stage.Running Then _stage = Stage.Paste
                    btnCancel.Text = "Cancel"
                    btnCancel.Enabled = True
                    txtLetter.ReadOnly = False
                End If
            End Try

            ' Closing while reading stopped the request; now close.
            If _closeWhenStopped AndAlso Not IsDisposed Then
                _closeWhenStopped = False
                If IsHandleCreated Then
                    BeginInvoke(New MethodInvoker(AddressOf CloseAfterStop))
                Else
                    CloseAfterStop()
                End If
            End If

        End Function


        Private Sub CloseAfterStop()
            If IsDisposed Then Return
            DialogResult = DialogResult.Cancel
            If Not Modal Then Close()
        End Sub


        ' ---------------------------------------------------------------
        ' Review
        ' ---------------------------------------------------------------

        Private Sub ShowReview(found As DecisionLetterProposal)

            _proposal = found
            _stage = Stage.Review
            pastePanel.Visible = False
            reviewPanel.Visible = True
            btnBack.Visible = True
            btnCancel.Text = "Cancel"
            txtSource.Text = _letter
            txtSource.Select(0, 0)

            Dim origin As String = If(found.ProviderName.Length > 0, found.ProviderName, "the AI assistant") &
                If(found.Model.Length > 0, " (" & found.Model & ")", String.Empty)
            lblIntro.Text = "AI suggestion from " & origin & ": check each item before adding."

            lblDecision.Text = If(found.Decision.HasValue,
                                  "Decision: " & EditorialDecisionDisplayService.Format(found.Decision.Value),
                                  "Decision: not stated plainly; choose it in the next step")
            lblDecisionDetails.Text = DecisionDetails(found)
            lblTruncated.Visible = found.Truncated

            Dim present As HashSet(Of String) = MatrixKeys()
            _rows.Clear()
            For Each candidate As LetterCommentCandidate In found.Comments
                Dim inMatrix As Boolean = present.Contains(MatchKey(candidate.ReviewerLabel, candidate.Text))
                _rows.Add(New LetterCommentRow With {
                    .Candidate = candidate,
                    .ReviewerLabel = candidate.ReviewerLabel,
                    .Text = candidate.Text,
                    .AlreadyInMatrix = inMatrix,
                    .Use = candidate.Found AndAlso Not inMatrix
                })
            Next
            FillGrid()
            AcceptButton = btnPrimary
            RefreshReview()
            ActiveControl = If(gridComments.Rows.Count > 0, CType(gridComments, Control), btnPrimary)

        End Sub


        ' The decision's quote and dates, with where the deadline came from.
        Private Shared Function DecisionDetails(found As DecisionLetterProposal) As String
            Dim lines As New List(Of String)()
            If found.DecisionQuote.Length > 0 Then lines.Add(Quoted(found.DecisionQuote))
            If found.DecisionDate.HasValue Then
                lines.Add("Letter date: " & DateText(found.DecisionDate.Value))
            ElseIf found.LetterDateNotUsed.HasValue Then
                lines.Add("Letter date: " & DateText(found.LetterDateNotUsed.Value) & " is far from today; check it in the next step")
            Else
                lines.Add("Letter date: not found; check it in the next step")
            End If
            If found.RevisionDeadline.HasValue Then
                lines.Add("Revision deadline: " & DateText(found.RevisionDeadline.Value) &
                          If(found.DeadlineBasis.Length > 0, " (" & LowerFirst(found.DeadlineBasis) & ")", String.Empty))
                If found.DeadlineQuote.Length > 0 Then lines.Add(Quoted(found.DeadlineQuote))
            ElseIf found.DeadlineNotUsed Then
                lines.Add("Revision deadline: set it in the next step")
                If found.DeadlineQuote.Length > 0 Then lines.Add(Quoted(found.DeadlineQuote))
            Else
                lines.Add("Revision deadline: none found")
            End If
            Return String.Join(Environment.NewLine, lines)
        End Function


        Private Sub FillGrid()

            _loadingRows = True
            Try
                gridComments.Rows.Clear()
                For Each row As LetterCommentRow In _rows
                    Dim index As Integer = gridComments.Rows.Add(row.Use, row.ReviewerLabel, OneLine(row.Text), row.StatusText)
                    Dim gridRow As DataGridViewRow = gridComments.Rows(index)
                    gridRow.Tag = row
                    gridRow.Cells(ColumnComment).ToolTipText = row.Text
                    If row.AlreadyInMatrix Then
                        gridRow.Cells(ColumnStatus).Style.ForeColor = UiTheme.MutedText()
                    ElseIf Not row.Candidate.Found Then
                        gridRow.Cells(ColumnStatus).Style.ForeColor = UiTheme.WarningColor()
                    End If
                Next
            Finally
                _loadingRows = False
            End Try

            If gridComments.Rows.Count > 0 Then
                gridComments.CurrentCell = gridComments.Rows(0).Cells(ColumnReviewer)
                gridComments.Rows(0).Selected = True
            End If
            ShowSelectedComment()

        End Sub


        Private Sub GridCellChanged(sender As Object, e As DataGridViewCellEventArgs)

            If _loadingRows OrElse e.RowIndex < 0 OrElse e.ColumnIndex < 0 Then Return
            Dim gridRow As DataGridViewRow = gridComments.Rows(e.RowIndex)
            Dim row As LetterCommentRow = TryCast(gridRow.Tag, LetterCommentRow)
            If row Is Nothing Then Return
            Select Case gridComments.Columns(e.ColumnIndex).Name
                Case ColumnUse
                    Dim value As Object = gridRow.Cells(ColumnUse).Value
                    row.Use = TypeOf value Is Boolean AndAlso DirectCast(value, Boolean)
                Case ColumnReviewer
                    row.ReviewerLabel = Collapse(Convert.ToString(gridRow.Cells(ColumnReviewer).Value, CultureInfo.CurrentCulture))
            End Select
            RefreshReview()

        End Sub


        Private Function SelectedGridRow() As DataGridViewRow
            If gridComments.SelectedRows.Count = 0 Then Return Nothing
            Return gridComments.SelectedRows(0)
        End Function


        ' The selected comment in the editor, and its source in the letter.
        Private Sub ShowSelectedComment()

            Dim gridRow As DataGridViewRow = SelectedGridRow()
            Dim row As LetterCommentRow = If(gridRow Is Nothing, Nothing, TryCast(gridRow.Tag, LetterCommentRow))
            _loadingComment = True
            Try
                txtComment.Text = If(row Is Nothing, String.Empty, ForTextBox(row.Text))
                txtComment.Enabled = row IsNot Nothing
            Finally
                _loadingComment = False
            End Try
            If row IsNot Nothing AndAlso row.Candidate.Found Then
                ShowInLetter(row.Candidate.SourceStart, row.Candidate.SourceLength)
            Else
                txtSource.SelectionLength = 0
            End If

        End Sub


        ' Selects text in the letter, scrolled so its start is in view.
        Private Sub ShowInLetter(start As Integer, length As Integer)
            If start < 0 OrElse length <= 0 OrElse start + length > txtSource.TextLength Then Return
            txtSource.Select(start + length, 0)
            txtSource.ScrollToCaret()
            txtSource.Select(start, 0)
            txtSource.ScrollToCaret()
            txtSource.Select(start, length)
        End Sub


        Private Sub CommentEdited(sender As Object, e As EventArgs)

            If _loadingComment Then Return
            Dim gridRow As DataGridViewRow = SelectedGridRow()
            Dim row As LetterCommentRow = If(gridRow Is Nothing, Nothing, TryCast(gridRow.Tag, LetterCommentRow))
            If row Is Nothing Then Return
            row.Text = txtComment.Text
            _loadingRows = True
            Try
                gridRow.Cells(ColumnComment).Value = OneLine(row.Text)
                gridRow.Cells(ColumnComment).ToolTipText = row.Text
            Finally
                _loadingRows = False
            End Try
            RefreshReview()

        End Sub


        Private Sub RefreshReview()

            If _stage <> Stage.Review Then Return
            Dim count As Integer = _rows.Where(Function(item) item.Use).Count()
            Dim commentsText As String = count.ToString(CultureInfo.CurrentCulture) & If(count = 1, " Comment", " Comments")
            If _mode = DecisionLetterMode.NewDecision Then
                btnPrimary.Text = If(count = 0, "Add Decision...", "Add Decision && " & commentsText & "...")
                btnPrimary.Enabled = True
            Else
                btnPrimary.Text = If(count = 0, "Add Comments", "Add " & commentsText)
                btnPrimary.Enabled = count > 0
            End If
            ShowStatus(If(count = 0, "No comments to add", count.ToString(CultureInfo.CurrentCulture) & If(count = 1, " comment to add", " comments to add")))

        End Sub


        Private Sub AcceptReview()

            If _stage <> Stage.Review OrElse _proposal Is Nothing Then Return
            If gridComments.IsCurrentCellInEditMode Then gridComments.EndEdit()

            Dim used As List(Of LetterCommentRow) = _rows.Where(Function(item) item.Use).ToList()
            For Each row As LetterCommentRow In used
                If Collapse(row.ReviewerLabel).Length = 0 Then
                    SelectRow(row)
                    ShowStatus("Enter the reviewer for each comment you add.", UiTheme.InfoColor())
                    Return
                End If
                If row.Text.Trim().Length = 0 Then
                    SelectRow(row)
                    ShowStatus("Enter the text of each comment you add, or uncheck it.", UiTheme.InfoColor())
                    Return
                End If
            Next

            Dim chosen As DecisionChoice = TryCast(cmbDecision.SelectedItem, DecisionChoice)
            If _mode = DecisionLetterMode.ExistingDecision Then
                If used.Count = 0 Then Return
                If chosen Is Nothing Then
                    ShowStatus("Choose the decision these comments belong to.", UiTheme.InfoColor())
                    cmbDecision.Focus()
                    Return
                End If
            End If

            _accepted.Clear()
            For Each row As LetterCommentRow In used
                Dim candidate As LetterCommentCandidate = row.Candidate
                _accepted.Add(New AcceptedLetterComment With {
                    .ReviewerLabel = Collapse(row.ReviewerLabel),
                    .Text = ForTextBox(row.Text).Trim(),
                    .SourceExcerpt = If(candidate.Found AndAlso candidate.SourceStart >= 0 AndAlso candidate.SourceStart + candidate.SourceLength <= _letter.Length,
                                        _letter.Substring(candidate.SourceStart, candidate.SourceLength).Trim(),
                                        String.Empty)
                })
            Next
            _chosenDecisionId = If(_mode = DecisionLetterMode.ExistingDecision AndAlso chosen IsNot Nothing, chosen.Id, CType(Nothing, Guid?))
            _round = Decimal.ToInt32(nudRound.Value)
            DialogResult = DialogResult.OK

        End Sub


        Private Sub SelectRow(row As LetterCommentRow)
            For Each gridRow As DataGridViewRow In gridComments.Rows
                If gridRow.Tag Is row Then
                    gridComments.CurrentCell = gridRow.Cells(ColumnReviewer)
                    gridRow.Selected = True
                    Exit For
                End If
            Next
        End Sub


        ' ---------------------------------------------------------------
        ' Stop, Cancel, and closing
        ' ---------------------------------------------------------------

        Private Sub CancelClicked(sender As Object, e As EventArgs)
            Dim running As CancellationTokenSource = _cancellation
            If running IsNot Nothing Then
                btnCancel.Enabled = False
                running.Cancel()
                Return
            End If
            DialogResult = DialogResult.Cancel
            If Not Modal Then Close()
        End Sub


        Protected Overrides Sub OnFormClosing(e As FormClosingEventArgs)
            Dim running As CancellationTokenSource = _cancellation
            If running IsNot Nothing Then
                ' Closing waits for the request to stop; Windows shutting
                ' down is never held up.
                Dim closeAfter As Boolean = e.CloseReason = CloseReason.UserClosing OrElse e.CloseReason = CloseReason.None
                If closeAfter Then _closeWhenStopped = True
                btnCancel.Enabled = False
                running.Cancel()
                If closeAfter Then
                    e.Cancel = True
                    Return
                End If
            End If
            MyBase.OnFormClosing(e)
        End Sub


        Protected Overrides Sub Dispose(disposing As Boolean)
            If disposing Then
                ' A request still running ends; its own code disposes it.
                _cancellation?.Cancel()
            End If
            MyBase.Dispose(disposing)
            If disposing Then
                _boldFont?.Dispose()
                _boldFont = Nothing
                _formFont?.Dispose()
                _formFont = Nothing
            End If
        End Sub


        ' ---------------------------------------------------------------
        ' Helpers
        ' ---------------------------------------------------------------

        Private Sub ShowStatus(message As String, Optional color As Color? = Nothing)
            lblStatus.Text = message
            lblStatus.ForeColor = If(color, UiTheme.SecondaryText())
        End Sub


        Private Function MatrixKeys() As HashSet(Of String)
            Dim keys As New HashSet(Of String)(StringComparer.Ordinal)
            For Each item As ReviewerResponseItem In If(_submission.ReviewerResponses, New List(Of ReviewerResponseItem)())
                If item IsNot Nothing Then keys.Add(MatchKey(item.ReviewerLabel, item.CommentText))
            Next
            Return keys
        End Function


        ' The same reviewer and comment, ignoring spacing and letter case.
        Private Shared Function MatchKey(label As String, comment As String) As String
            Return Collapse(label).ToUpperInvariant() & "|" & Collapse(comment).ToUpperInvariant()
        End Function


        Private Shared Function Collapse(value As String) As String
            Return Regex.Replace(If(value, String.Empty), "\s+", " ").Trim()
        End Function


        Private Shared Function OneLine(value As String) As String
            Dim line As String = Collapse(value)
            Return If(line.Length <= 400, line, line.Substring(0, 400) & ChrW(&H2026))
        End Function


        Private Shared Function Quoted(value As String) As String
            Return ChrW(&H201C) & Collapse(value) & ChrW(&H201D)
        End Function


        Private Shared Function DateText(value As DateTime) As String
            Return value.ToString("MMM d, yyyy", CultureInfo.CurrentCulture)
        End Function


        ' "Stated in the letter" reads as "(stated in the letter)".
        Private Shared Function LowerFirst(value As String) As String
            If String.IsNullOrEmpty(value) OrElse Not Char.IsUpper(value(0)) Then Return value
            Return Char.ToLower(value(0), CultureInfo.CurrentCulture) & value.Substring(1)
        End Function


        ' A recorded decision, named as in the comment editor.
        Private NotInheritable Class DecisionChoice

            Public ReadOnly Property Id As Guid

            Private ReadOnly _label As String

            Public Sub New(decision As EditorialDecisionEvent, ordinal As Integer)
                Id = decision.Id
                _label = "Decision " & ordinal.ToString(CultureInfo.CurrentCulture) & " " & ChrW(&H2014) & " " &
                    decision.DecisionDate.ToString("MMM d, yyyy", CultureInfo.CurrentCulture) & " " & ChrW(&H2014) & " " &
                    EditorialDecisionDisplayService.Format(decision.Decision)
            End Sub

            Public Overrides Function ToString() As String
                Return _label
            End Function

        End Class


        ' ---------------------------------------------------------------
        ' For tests
        ' ---------------------------------------------------------------

        Friend ReadOnly Property LetterBox As TextBox
            Get
                Return txtLetter
            End Get
        End Property

        Friend ReadOnly Property SentBox As TextBox
            Get
                Return txtSent
            End Get
        End Property

        Friend ReadOnly Property ReviewLetterBox As TextBox
            Get
                Return txtSource
            End Get
        End Property

        Friend ReadOnly Property CommentBox As TextBox
            Get
                Return txtComment
            End Get
        End Property

        Friend ReadOnly Property CommentsGrid As DataGridView
            Get
                Return gridComments
            End Get
        End Property

        Friend ReadOnly Property CandidateRows As IReadOnlyList(Of LetterCommentRow)
            Get
                Return _rows.AsReadOnly()
            End Get
        End Property

        Friend ReadOnly Property DecisionBox As ComboBox
            Get
                Return cmbDecision
            End Get
        End Property

        Friend ReadOnly Property RoundBox As NumericUpDown
            Get
                Return nudRound
            End Get
        End Property

        Friend ReadOnly Property PrimaryButton As Button
            Get
                Return btnPrimary
            End Get
        End Property

        Friend ReadOnly Property CancelButtonForTest As Button
            Get
                Return btnCancel
            End Get
        End Property

        Friend ReadOnly Property ReadAgainButton As Button
            Get
                Return btnBack
            End Get
        End Property

        Friend ReadOnly Property IntroText As String
            Get
                Return lblIntro.Text
            End Get
        End Property

        Friend ReadOnly Property StatusText As String
            Get
                Return lblStatus.Text
            End Get
        End Property

        Friend ReadOnly Property DecisionText As String
            Get
                Return lblDecision.Text & Environment.NewLine & lblDecisionDetails.Text
            End Get
        End Property

        Friend ReadOnly Property TruncatedNoteShown As Boolean
            Get
                Return lblTruncated.Visible
            End Get
        End Property

        Friend ReadOnly Property IsReviewing As Boolean
            Get
                Return _stage = Stage.Review
            End Get
        End Property

        Friend ReadOnly Property IsRunning As Boolean
            Get
                Return _stage = Stage.Running
            End Get
        End Property

        Friend Sub SetUseForTest(index As Integer, value As Boolean)
            gridComments.Rows(index).Cells(ColumnUse).Value = value
        End Sub

        Friend Sub SetReviewerForTest(index As Integer, value As String)
            gridComments.Rows(index).Cells(ColumnReviewer).Value = value
        End Sub

        Friend Sub SelectCommentForTest(index As Integer)
            gridComments.CurrentCell = gridComments.Rows(index).Cells(ColumnReviewer)
            gridComments.Rows(index).Selected = True
        End Sub

        Friend Sub ToggleSentForTest()
            ToggleSent()
        End Sub

        Friend Sub AcceptForTest()
            AcceptReview()
        End Sub

        Friend Sub CancelForTest()
            CancelClicked(btnCancel, EventArgs.Empty)
        End Sub

    End Class

End Namespace
