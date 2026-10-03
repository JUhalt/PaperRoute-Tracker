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

    ' One proposed requirement as the researcher reviews it.
    Friend NotInheritable Class RequirementRow

        Public Property Candidate As RequirementCandidate

        Public Property Use As Boolean

        Public Property Title As String = String.Empty

        Public Property Category As String = String.Empty

        Public Property IsRequired As Boolean = True

        ' A requirement with the same title is already in the checklist.
        Public Property AlreadyInChecklist As Boolean

        ' The numbers are checked against the requirement as edited, so a
        ' corrected title clears the warning.
        Public ReadOnly Property NumbersDiffer As Boolean
            Get
                Return Candidate IsNot Nothing AndAlso Candidate.Found AndAlso Not AssistantService.NumbersAppearIn(Title, Candidate.Quote)
            End Get
        End Property

        Public ReadOnly Property StatusText As String
            Get
                If AlreadyInChecklist Then Return AuthorInstructionsForm.AlreadyInChecklistText
                If Candidate Is Nothing OrElse Not Candidate.Found Then Return AuthorInstructionsForm.NotFoundText
                If NumbersDiffer Then Return AuthorInstructionsForm.NumbersDifferText
                Return AuthorInstructionsForm.InTextText
            End Get
        End Property

    End Class


    ' Read Author Instructions (#95): the researcher pastes part of a
    ' journal's instructions for authors (PaperRoute never fetches the page),
    ' sees exactly what will be sent and to whom, and gets back proposed
    ' checklist requirements, each checked word for word against the text.
    ' Nothing changes here: the journal editor adds what was accepted, and the
    ' journal's own Save keeps it.
    Friend Class AuthorInstructionsForm
        Inherits Form

        Friend Const InTextText As String = "In the text"
        Friend Const NotFoundText As String = "Not in the text"
        Friend Const NumbersDifferText As String = "Numbers differ"
        Friend Const AlreadyInChecklistText As String = "Already listed"

        Private Const ColumnUse As String = "Use"
        Private Const ColumnTitle As String = "Requirement"
        Private Const ColumnCategory As String = "Category"
        Private Const ColumnRequired As String = "Required"
        Private Const ColumnAppliesTo As String = "AppliesTo"
        Private Const ColumnStatus As String = "Status"

        Private Enum Stage
            Paste
            Running
            Review
        End Enum

        ' Test seams: how the window is shown, and how a link is opened.
        Friend Shared DialogRunner As Func(Of Form, IWin32Window, DialogResult) = Nothing
        Friend Shared LinkOpener As Action(Of String) = Nothing


        Friend Shared Function RunDialog(dialog As Form, owner As IWin32Window) As DialogResult
            If DialogRunner IsNot Nothing Then Return DialogRunner(dialog, owner)
            Return dialog.ShowDialog(owner)
        End Function


        Private ReadOnly _journalName As String
        Private ReadOnly _link As String
        Private ReadOnly _existingTitles As List(Of String)
        Private ReadOnly _categories As List(Of String)
        Private ReadOnly _rows As New List(Of RequirementRow)()
        Private ReadOnly _accepted As New List(Of JournalChecklistTemplateItem)()
        Private _stage As Stage = Stage.Paste
        Private _cancellation As CancellationTokenSource
        Private _closeWhenStopped As Boolean
        Private _proposal As AuthorInstructionsProposal
        Private _text As String = String.Empty
        Private _loadingRows As Boolean
        Private ReadOnly _formFont As New Font("Segoe UI", 10.0F)
        Private ReadOnly _boldFont As New Font(_formFont, FontStyle.Bold)

        Private ReadOnly lblIntro As New Label()
        Private ReadOnly body As New Panel()
        Private ReadOnly pastePanel As New TableLayoutPanel()
        Private ReadOnly lnkOpen As New LinkLabel()
        Private ReadOnly txtArticleType As New TextBox()
        Private ReadOnly txtText As New TextBox()
        Private ReadOnly lblCount As New Label()
        Private ReadOnly lnkSent As New LinkLabel()
        Private ReadOnly txtSent As New TextBox()
        Private ReadOnly reviewPanel As New TableLayoutPanel()
        Private ReadOnly proposalsPanel As New TableLayoutPanel()
        Private ReadOnly txtSource As New TextBox()
        Private ReadOnly lblTruncated As New Label()
        Private ReadOnly gridRequirements As New DataGridView()
        Private ReadOnly txtQuote As New TextBox()
        Private ReadOnly lblStatus As New Label()
        Private ReadOnly btnPrimary As New Button()
        Private ReadOnly btnBack As New Button()
        Private ReadOnly btnCancel As New Button()


        ' journalName titles the window; link is the journal's author
        ' instructions page, only ever opened in the browser; the existing
        ' titles and categories are the checklist as it stands now.
        Public Sub New(journalName As String, link As String, existingTitles As IEnumerable(Of String), existingCategories As IEnumerable(Of String), Optional text As String = Nothing)

            _journalName = If(journalName, String.Empty).Trim()
            _link = If(UrlSafetyService.IsSafeHttpUrl(link), link.Trim(), String.Empty)
            _existingTitles = If(existingTitles, Enumerable.Empty(Of String)()).Where(Function(title) Not String.IsNullOrWhiteSpace(title)).ToList()
            _categories = AssistantService.CategoriesFor(existingCategories)

            BuildInterface()
            If Not String.IsNullOrWhiteSpace(text) Then txtText.Text = ForTextBox(text).Trim()
            ShowPaste()
            UiPolish.ApplyDialog(Me)

        End Sub


        Protected Overrides Sub OnLoad(e As EventArgs)
            MyBase.OnLoad(e)
            ResponsiveDialogSizingService.FitToWorkingArea(Me)
        End Sub


        ' ---------------------------------------------------------------
        ' What the journal editor adds
        ' ---------------------------------------------------------------

        ' The requirements the researcher checked, as edited, in order, each
        ' recording that it began as an AI suggestion and the text it came
        ' from.
        Friend ReadOnly Property AcceptedRequirements As IReadOnlyList(Of JournalChecklistTemplateItem)
            Get
                Return _accepted.AsReadOnly()
            End Get
        End Property

        Friend ReadOnly Property Proposal As AuthorInstructionsProposal
            Get
                Return _proposal
            End Get
        End Property


        ' ---------------------------------------------------------------
        ' Interface
        ' ---------------------------------------------------------------

        Private Sub BuildInterface()

            SuspendLayout()

            Text = "Read Author Instructions"
            StartPosition = FormStartPosition.CenterParent
            ShowInTaskbar = False
            MinimizeBox = False
            ' Sizes below are at 96 DPI and scale with the display.
            AutoScaleDimensions = New SizeF(96.0F, 96.0F)
            ClientSize = New Size(1040, 700)
            MinimumSize = New Size(860, 620)
            Font = _formFont
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
            btnBack.AccessibleName = "Read the instructions again"
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
                    lblCount.MaximumSize = New Size(width, 0)
                End Sub
            AddHandler proposalsPanel.SizeChanged,
                Sub(sender, e)
                    lblTruncated.MaximumSize = New Size(Math.Max(LogicalToDeviceUnits(200), proposalsPanel.ClientSize.Width - LogicalToDeviceUnits(4)), 0)
                End Sub

            ResumeLayout(False)
            PerformLayout()

        End Sub


        Private Sub BuildPastePanel()

            pastePanel.Dock = DockStyle.Fill
            pastePanel.ColumnCount = 1
            pastePanel.RowCount = 6
            pastePanel.Margin = New Padding(0)
            pastePanel.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
            pastePanel.RowStyles.Add(New RowStyle(SizeType.AutoSize))
            pastePanel.RowStyles.Add(New RowStyle(SizeType.AutoSize))
            pastePanel.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
            pastePanel.RowStyles.Add(New RowStyle(SizeType.AutoSize))
            pastePanel.RowStyles.Add(New RowStyle(SizeType.AutoSize))
            pastePanel.RowStyles.Add(New RowStyle(SizeType.AutoSize))

            ' Where to copy from: the journal's own page, opened in the browser.
            lnkOpen.AutoSize = True
            lnkOpen.UseMnemonic = False
            lnkOpen.Margin = New Padding(0, 0, 0, 8)
            lnkOpen.Text = "Open the author instructions" & If(_journalName.Length > 0, " for " & _journalName, String.Empty)
            lnkOpen.Visible = _link.Length > 0
            AddHandler lnkOpen.LinkClicked, Sub(sender, e) OpenInstructions()

            ' The article type, so requirements for other types are left out.
            Dim typeRow As New TableLayoutPanel With {.Dock = DockStyle.Top, .AutoSize = True, .ColumnCount = 2, .RowCount = 1, .Margin = New Padding(0, 0, 0, 8)}
            typeRow.ColumnStyles.Add(New ColumnStyle(SizeType.AutoSize))
            typeRow.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
            Dim lblType As New Label With {.Text = "Article &type (optional)", .AutoSize = True, .Anchor = AnchorStyles.Left, .Margin = New Padding(0, 6, 12, 6)}
            txtArticleType.Dock = DockStyle.Fill
            txtArticleType.MaxLength = 80
            txtArticleType.PlaceholderText = "e.g., Research article"
            txtArticleType.Margin = New Padding(0, 4, 0, 4)
            txtArticleType.AccessibleName = "Article type"
            AddHandler txtArticleType.TextChanged, Sub(sender, e) RefreshPaste()
            typeRow.Controls.Add(lblType, 0, 0)
            typeRow.Controls.Add(txtArticleType, 1, 0)

            Dim textSide As New TableLayoutPanel With {.Dock = DockStyle.Fill, .ColumnCount = 1, .RowCount = 2, .Margin = New Padding(0)}
            textSide.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
            textSide.RowStyles.Add(New RowStyle(SizeType.AutoSize))
            textSide.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
            Dim heading As New Label With {.Text = "&Paste the author instructions", .AutoSize = True, .Font = _boldFont, .Margin = New Padding(0, 0, 0, 4)}
            txtText.Dock = DockStyle.Fill
            txtText.Multiline = True
            txtText.AcceptsReturn = True
            txtText.AcceptsTab = False
            txtText.ScrollBars = ScrollBars.Vertical
            txtText.WordWrap = True
            ' The length is checked before anything is sent.
            txtText.MaxLength = 0
            txtText.AccessibleName = "Paste the author instructions"
            AddHandler txtText.TextChanged, Sub(sender, e) RefreshPaste()
            textSide.Controls.Add(heading, 0, 0)
            textSide.Controls.Add(txtText, 0, 1)

            lblCount.AutoSize = True
            lblCount.UseMnemonic = False
            lblCount.ForeColor = UiTheme.SecondaryText()
            lblCount.Margin = New Padding(0, 4, 0, 0)

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
            txtSent.Height = 130
            txtSent.Margin = New Padding(0, 4, 0, 0)
            txtSent.AccessibleName = "What Read Instructions will send"

            pastePanel.Controls.Add(lnkOpen, 0, 0)
            pastePanel.Controls.Add(typeRow, 0, 1)
            pastePanel.Controls.Add(textSide, 0, 2)
            pastePanel.Controls.Add(lblCount, 0, 3)
            pastePanel.Controls.Add(lnkSent, 0, 4)
            pastePanel.Controls.Add(txtSent, 0, 5)

        End Sub


        Private Sub BuildReviewPanel()

            reviewPanel.Dock = DockStyle.Fill
            reviewPanel.ColumnCount = 2
            reviewPanel.RowCount = 1
            reviewPanel.Margin = New Padding(0)
            reviewPanel.Visible = False
            reviewPanel.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 38))
            reviewPanel.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 62))
            reviewPanel.RowStyles.Add(New RowStyle(SizeType.Percent, 100))

            ' The text as sent, where selecting a requirement shows its source.
            Dim textSide As New TableLayoutPanel With {.Dock = DockStyle.Fill, .ColumnCount = 1, .RowCount = 2, .Margin = New Padding(0, 0, 14, 0)}
            textSide.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
            textSide.RowStyles.Add(New RowStyle(SizeType.AutoSize))
            textSide.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
            textSide.Controls.Add(New Label With {.Text = "The instructions", .AutoSize = True, .UseMnemonic = False, .Font = _boldFont, .Margin = New Padding(0, 0, 0, 4)}, 0, 0)
            txtSource.Dock = DockStyle.Fill
            txtSource.Multiline = True
            txtSource.ReadOnly = True
            txtSource.WordWrap = True
            txtSource.ScrollBars = ScrollBars.Vertical
            txtSource.HideSelection = False
            txtSource.MaxLength = 0
            txtSource.AccessibleName = "The author instructions as sent"
            textSide.Controls.Add(txtSource, 0, 1)

            proposalsPanel.Dock = DockStyle.Fill
            proposalsPanel.ColumnCount = 1
            proposalsPanel.RowCount = 5
            proposalsPanel.Margin = New Padding(0)
            proposalsPanel.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
            proposalsPanel.RowStyles.Add(New RowStyle(SizeType.AutoSize))
            proposalsPanel.RowStyles.Add(New RowStyle(SizeType.AutoSize))
            proposalsPanel.RowStyles.Add(New RowStyle(SizeType.Percent, 68))
            proposalsPanel.RowStyles.Add(New RowStyle(SizeType.AutoSize))
            proposalsPanel.RowStyles.Add(New RowStyle(SizeType.Percent, 32))

            lblTruncated.Text = "The answer was cut short, so some requirements may be missing."
            lblTruncated.AutoSize = True
            lblTruncated.UseMnemonic = False
            lblTruncated.ForeColor = UiTheme.WarningColor()
            lblTruncated.Margin = New Padding(0, 0, 0, 4)
            lblTruncated.Visible = False

            Dim lblRequirements As New Label With {.Text = "Proposed requirements (edit them before adding)", .AutoSize = True, .UseMnemonic = False, .Font = _boldFont, .Margin = New Padding(0, 0, 0, 4)}

            ConfigureGrid()

            Dim lblQuote As New Label With {.Text = "From the text", .AutoSize = True, .UseMnemonic = False, .Margin = New Padding(0, 8, 0, 4)}
            txtQuote.Dock = DockStyle.Fill
            txtQuote.Multiline = True
            txtQuote.ReadOnly = True
            txtQuote.WordWrap = True
            txtQuote.ScrollBars = ScrollBars.Vertical
            txtQuote.AccessibleName = "Where the selected requirement is in the text"

            proposalsPanel.Controls.Add(lblTruncated, 0, 0)
            proposalsPanel.Controls.Add(lblRequirements, 0, 1)
            proposalsPanel.Controls.Add(gridRequirements, 0, 2)
            proposalsPanel.Controls.Add(lblQuote, 0, 3)
            proposalsPanel.Controls.Add(txtQuote, 0, 4)

            reviewPanel.Controls.Add(textSide, 0, 0)
            reviewPanel.Controls.Add(proposalsPanel, 1, 0)

        End Sub


        Private Sub ConfigureGrid()

            gridRequirements.Dock = DockStyle.Fill
            gridRequirements.AllowUserToAddRows = False
            gridRequirements.AllowUserToDeleteRows = False
            gridRequirements.AllowUserToResizeRows = False
            gridRequirements.AllowUserToOrderColumns = False
            gridRequirements.RowHeadersVisible = False
            gridRequirements.SelectionMode = DataGridViewSelectionMode.FullRowSelect
            gridRequirements.MultiSelect = False
            gridRequirements.EditMode = DataGridViewEditMode.EditOnKeystrokeOrF2
            ' A requirement shows whole, on as many lines as it needs.
            gridRequirements.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells
            gridRequirements.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize
            gridRequirements.AccessibleName = "Requirements the assistant found"

            gridRequirements.Columns.Add(New DataGridViewCheckBoxColumn With {
                .Name = ColumnUse, .HeaderText = "Use",
                .AutoSizeMode = DataGridViewAutoSizeColumnMode.ColumnHeader,
                .SortMode = DataGridViewColumnSortMode.NotSortable
            })
            Dim titleColumn As New DataGridViewTextBoxColumn With {
                .Name = ColumnTitle, .HeaderText = "Requirement", .MaxInputLength = 200,
                .AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, .FillWeight = 75, .MinimumWidth = 140,
                .SortMode = DataGridViewColumnSortMode.NotSortable
            }
            titleColumn.DefaultCellStyle.WrapMode = DataGridViewTriState.True
            gridRequirements.Columns.Add(titleColumn)
            gridRequirements.Columns.Add(New DataGridViewTextBoxColumn With {
                .Name = ColumnCategory, .HeaderText = "Category", .MaxInputLength = 80,
                .AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells, .MinimumWidth = 80,
                .SortMode = DataGridViewColumnSortMode.NotSortable
            })
            gridRequirements.Columns.Add(New DataGridViewCheckBoxColumn With {
                .Name = ColumnRequired, .HeaderText = "Required",
                .AutoSizeMode = DataGridViewAutoSizeColumnMode.ColumnHeader,
                .SortMode = DataGridViewColumnSortMode.NotSortable
            })
            Dim appliesColumn As New DataGridViewTextBoxColumn With {
                .Name = ColumnAppliesTo, .HeaderText = "Applies to", .ReadOnly = True,
                .AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, .FillWeight = 25, .MinimumWidth = 90,
                .SortMode = DataGridViewColumnSortMode.NotSortable
            }
            appliesColumn.DefaultCellStyle.WrapMode = DataGridViewTriState.True
            gridRequirements.Columns.Add(appliesColumn)
            gridRequirements.Columns.Add(New DataGridViewTextBoxColumn With {
                .Name = ColumnStatus, .HeaderText = "Status", .ReadOnly = True,
                .AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells, .MinimumWidth = 100,
                .SortMode = DataGridViewColumnSortMode.NotSortable
            })

            ' A check box counts as soon as it is clicked.
            AddHandler gridRequirements.CurrentCellDirtyStateChanged,
                Sub(sender, e)
                    If gridRequirements.IsCurrentCellDirty AndAlso TypeOf gridRequirements.CurrentCell Is DataGridViewCheckBoxCell Then
                        gridRequirements.CommitEdit(DataGridViewDataErrorContexts.Commit)
                    End If
                End Sub
            AddHandler gridRequirements.CellValueChanged, AddressOf GridCellChanged
            AddHandler gridRequirements.SelectionChanged, Sub(sender, e) ShowSelectedRequirement()

            EmptyHint.Attach(gridRequirements, "No requirements were found in the text.")

        End Sub


        ' ---------------------------------------------------------------
        ' Paste
        ' ---------------------------------------------------------------

        Private Sub ShowPaste()

            _stage = Stage.Paste
            reviewPanel.Visible = False
            pastePanel.Visible = True
            lblIntro.Text = "Read Instructions sends only the text you paste here, and the article type if you give one, to " & Recipient() &
                ", with PaperRoute's instructions. Nothing from your library is sent."
            btnBack.Visible = False
            btnPrimary.Text = "Read Instructions"
            btnCancel.Text = "Cancel"
            txtText.ReadOnly = False
            txtArticleType.ReadOnly = False
            AcceptButton = btnPrimary
            ShowStatus(String.Empty)
            RefreshPaste()
            ActiveControl = txtText
            txtText.Select(0, 0)

        End Sub


        Private Sub RefreshPaste()

            If _stage <> Stage.Paste Then Return
            Dim pasted As String = PastedText()
            Dim tooLong As Boolean = pasted.Length > AssistantService.MaximumInstructionsLength
            btnPrimary.Enabled = pasted.Length > 0 AndAlso Not tooLong
            lblCount.Text = pasted.Length.ToString("N0", CultureInfo.CurrentCulture) & " of " &
                AssistantService.MaximumInstructionsLength.ToString("N0", CultureInfo.CurrentCulture) &
                " characters. Paste one section at a time."
            lblCount.ForeColor = If(tooLong, UiTheme.InfoColor(), UiTheme.SecondaryText())
            If tooLong Then
                ShowStatus("The text is longer than PaperRoute sends at once. Paste one section at a time.", UiTheme.InfoColor())
            ElseIf pasted.Length = 0 Then
                ShowStatus("Copy the part of the journal's instructions for authors that lists what to prepare, and paste it here.")
            ElseIf lblStatus.Text.StartsWith("The text is longer", StringComparison.Ordinal) OrElse lblStatus.Text.StartsWith("Copy the part", StringComparison.Ordinal) Then
                ShowStatus(String.Empty)
            End If
            If txtSent.Visible Then txtSent.Text = SentText(pasted)

        End Sub


        Private Sub ToggleSent()
            txtSent.Visible = Not txtSent.Visible
            lnkSent.Text = If(txtSent.Visible, "Hide what will be sent", "Show what will be sent")
            If txtSent.Visible Then txtSent.Text = SentText(PastedText())
        End Sub


        Private Function SentText(text As String) As String
            Return ForTextBox(AssistantService.WhatIsSent(CurrentRequest(text)))
        End Function


        Private Function CurrentRequest(text As String) As AssistantRequest
            Return AssistantService.BuildAuthorInstructionsRequest(text, txtArticleType.Text)
        End Function


        ' The pasted text with its line breaks made Windows', so the places the
        ' answer gives are places in the text shown for review.
        Private Function PastedText() As String
            Return ForTextBox(txtText.Text).Trim()
        End Function


        Private Shared Function ForTextBox(value As String) As String
            Return Regex.Replace(If(value, String.Empty), "\r\n|\r|\n", vbCrLf)
        End Function


        Private Sub OpenInstructions()
            If _link.Length = 0 Then Return
            Try
                If LinkOpener IsNot Nothing Then
                    LinkOpener(_link)
                Else
                    UrlSafetyService.OpenInBrowser(_link)
                End If
            Catch ex As Exception
                ShowStatus("The link couldn't be opened: " & ex.Message, UiTheme.InfoColor())
            End Try
        End Sub


        ' Who the text goes to, as the researcher set it up.
        Private Shared Function Recipient() As String
            If AssistantService.ProviderFactory IsNot Nothing Then
                Dim provider As IAssistantProvider = AssistantService.CurrentProvider()
                If provider IsNot Nothing AndAlso Not String.IsNullOrEmpty(provider.ProviderName) Then Return provider.ProviderName
            End If
            Dim connection As AssistantConnection = OnlineAccess.CurrentAssistant()
            Return If(connection Is Nothing, "the AI assistant", connection.Recipient)
        End Function


        ' ---------------------------------------------------------------
        ' Reading the instructions
        ' ---------------------------------------------------------------

        Private Async Function PrimaryAsync() As Task
            Select Case _stage
                Case Stage.Paste : Await ReadAsync()
                Case Stage.Review : AcceptReview()
            End Select
        End Function


        Friend Async Function ReadAsync() As Task

            If _stage <> Stage.Paste OrElse _cancellation IsNot Nothing Then Return
            Dim text As String = PastedText()
            If text.Length = 0 OrElse text.Length > AssistantService.MaximumInstructionsLength Then Return

            Dim reason As String = AssistantRunner.Unavailable()
            If reason.Length > 0 Then
                ShowStatus(reason, UiTheme.InfoColor())
                Return
            End If

            Dim request As AssistantRequest = CurrentRequest(text)
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
            txtText.ReadOnly = True
            txtArticleType.ReadOnly = True
            btnPrimary.Enabled = False
            btnCancel.Text = "Stop"
            ShowStatus("Reading the instructions with " & Recipient() & "...")

            Try
                Dim reply As AssistantReply = Await provider.CompleteAsync(request, cancellation.Token)
                If IsDisposed Then Return
                cancellation.Token.ThrowIfCancellationRequested()
                Dim found As AuthorInstructionsProposal = AssistantService.ReadAuthorInstructionsReply(reply, text, _categories)
                If String.IsNullOrEmpty(found.ProviderName) Then found.ProviderName = If(provider.ProviderName, String.Empty)
                If String.IsNullOrEmpty(found.Model) Then found.Model = If(provider.Model, String.Empty)
                _text = text
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
                    If _stage = Stage.Paste Then
                        txtText.ReadOnly = False
                        txtArticleType.ReadOnly = False
                    End If
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

        Private Sub ShowReview(found As AuthorInstructionsProposal)

            _proposal = found
            _stage = Stage.Review
            pastePanel.Visible = False
            reviewPanel.Visible = True
            btnBack.Visible = True
            btnCancel.Text = "Cancel"
            txtSource.Text = _text
            txtSource.Select(0, 0)

            Dim origin As String = If(found.ProviderName.Length > 0, found.ProviderName, "the AI assistant") &
                If(found.Model.Length > 0, " (" & found.Model & ")", String.Empty)
            lblIntro.Text = "AI suggestion from " & origin & ": check each requirement before adding. The assistant can miss requirements, including ones on other pages, so read the journal's instructions too."
            lblTruncated.Visible = found.Truncated

            _rows.Clear()
            For Each candidate As RequirementCandidate In found.Requirements
                Dim known As Boolean = IsInChecklist(candidate.Title)
                Dim row As New RequirementRow With {
                    .Candidate = candidate,
                    .Title = candidate.Title,
                    .Category = candidate.Category,
                    .IsRequired = candidate.IsRequired,
                    .AlreadyInChecklist = known
                }
                ' Only what the text plainly supports starts checked.
                row.Use = candidate.Found AndAlso Not row.NumbersDiffer AndAlso Not known
                _rows.Add(row)
            Next
            FillGrid()
            AcceptButton = btnPrimary
            RefreshReview()
            ActiveControl = If(gridRequirements.Rows.Count > 0, CType(gridRequirements, Control), btnPrimary)

        End Sub


        Private Function IsInChecklist(title As String) As Boolean
            Return _existingTitles.Any(Function(existing) AssistantService.SameRequirement(existing, title))
        End Function


        Private Sub FillGrid()

            _loadingRows = True
            Try
                gridRequirements.Rows.Clear()
                For Each row As RequirementRow In _rows
                    Dim index As Integer = gridRequirements.Rows.Add(row.Use, row.Title, row.Category, row.IsRequired, row.Candidate.AppliesTo, row.StatusText)
                    Dim gridRow As DataGridViewRow = gridRequirements.Rows(index)
                    gridRow.Tag = row
                    gridRow.Cells(ColumnTitle).ToolTipText = row.Title
                    ColorStatus(gridRow, row)
                Next
            Finally
                _loadingRows = False
            End Try

            If gridRequirements.Rows.Count > 0 Then
                gridRequirements.CurrentCell = gridRequirements.Rows(0).Cells(ColumnTitle)
                gridRequirements.Rows(0).Selected = True
            End If
            ShowSelectedRequirement()

        End Sub


        Private Shared Sub ColorStatus(gridRow As DataGridViewRow, row As RequirementRow)
            Dim cell As DataGridViewCell = gridRow.Cells(ColumnStatus)
            cell.Value = row.StatusText
            If row.AlreadyInChecklist Then
                cell.Style.ForeColor = UiTheme.MutedText()
            ElseIf row.StatusText <> InTextText Then
                cell.Style.ForeColor = UiTheme.WarningColor()
            Else
                cell.Style.ForeColor = Color.Empty
            End If
        End Sub


        Private Sub GridCellChanged(sender As Object, e As DataGridViewCellEventArgs)

            If _loadingRows OrElse e.RowIndex < 0 OrElse e.ColumnIndex < 0 Then Return
            Dim gridRow As DataGridViewRow = gridRequirements.Rows(e.RowIndex)
            Dim row As RequirementRow = TryCast(gridRow.Tag, RequirementRow)
            If row Is Nothing Then Return
            Select Case gridRequirements.Columns(e.ColumnIndex).Name
                Case ColumnUse
                    Dim value As Object = gridRow.Cells(ColumnUse).Value
                    row.Use = TypeOf value Is Boolean AndAlso DirectCast(value, Boolean)
                Case ColumnTitle
                    row.Title = Collapse(Convert.ToString(gridRow.Cells(ColumnTitle).Value, CultureInfo.CurrentCulture))
                    gridRow.Cells(ColumnTitle).ToolTipText = row.Title
                    row.AlreadyInChecklist = IsInChecklist(row.Title)
                Case ColumnCategory
                    row.Category = Collapse(Convert.ToString(gridRow.Cells(ColumnCategory).Value, CultureInfo.CurrentCulture))
                Case ColumnRequired
                    Dim value As Object = gridRow.Cells(ColumnRequired).Value
                    row.IsRequired = TypeOf value Is Boolean AndAlso DirectCast(value, Boolean)
            End Select
            _loadingRows = True
            Try
                ColorStatus(gridRow, row)
            Finally
                _loadingRows = False
            End Try
            If gridRow.Selected Then ShowSelectedRequirement()
            RefreshReview()

        End Sub


        Private Function SelectedGridRow() As DataGridViewRow
            If gridRequirements.SelectedRows.Count = 0 Then Return Nothing
            Return gridRequirements.SelectedRows(0)
        End Function


        ' The selected requirement's source, selected in the text, and what
        ' its status means.
        Private Sub ShowSelectedRequirement()

            Dim gridRow As DataGridViewRow = SelectedGridRow()
            Dim row As RequirementRow = If(gridRow Is Nothing, Nothing, TryCast(gridRow.Tag, RequirementRow))
            If row Is Nothing Then
                txtQuote.Text = String.Empty
                txtSource.SelectionLength = 0
                Return
            End If

            Dim lines As New List(Of String)()
            If row.Candidate.Found Then
                lines.Add(ForTextBox(row.Candidate.Quote))
                If row.NumbersDiffer Then lines.Add(String.Empty) : lines.Add("A number in the requirement isn't in this sentence. Correct the requirement, or uncheck it.")
            Else
                lines.Add("The assistant's quote isn't in the text, so it may be invented. Check the journal's instructions before you add it:")
                lines.Add(ForTextBox(row.Candidate.Quote))
            End If
            If row.AlreadyInChecklist Then lines.Add(String.Empty) : lines.Add("The checklist already has a requirement with this title.")
            If row.Candidate.AppliesTo.Length > 0 Then lines.Add(String.Empty) : lines.Add("Applies to: " & row.Candidate.AppliesTo & ".")
            txtQuote.Text = String.Join(Environment.NewLine, lines)
            txtQuote.Select(0, 0)

            If row.Candidate.Found Then
                ShowInText(row.Candidate.SourceStart, row.Candidate.SourceLength)
            Else
                txtSource.SelectionLength = 0
            End If

        End Sub


        ' Selects text in the instructions, scrolled so its start is in view.
        Private Sub ShowInText(start As Integer, length As Integer)
            If start < 0 OrElse length <= 0 OrElse start + length > txtSource.TextLength Then Return
            txtSource.Select(start + length, 0)
            txtSource.ScrollToCaret()
            txtSource.Select(start, 0)
            txtSource.ScrollToCaret()
            txtSource.Select(start, length)
        End Sub


        Private Sub RefreshReview()

            If _stage <> Stage.Review Then Return
            Dim count As Integer = _rows.Where(Function(item) item.Use).Count()
            btnPrimary.Text = If(count = 0, "Add Requirements", If(count = 1, "Add 1 Requirement", "Add " & count.ToString(CultureInfo.CurrentCulture) & " Requirements"))
            btnPrimary.Enabled = count > 0
            ShowStatus(If(count = 0, "No requirements to add", count.ToString(CultureInfo.CurrentCulture) & If(count = 1, " requirement to add", " requirements to add")) &
                ". They join the checklist, and are kept when you save the journal.")

        End Sub


        Private Sub AcceptReview()

            If _stage <> Stage.Review OrElse _proposal Is Nothing Then Return
            If gridRequirements.IsCurrentCellInEditMode Then gridRequirements.EndEdit()

            Dim used As List(Of RequirementRow) = _rows.Where(Function(item) item.Use).ToList()
            If used.Count = 0 Then Return
            For Each row As RequirementRow In used
                If Collapse(row.Title).Length = 0 Then
                    SelectRow(row)
                    ShowStatus("Enter each requirement you add, or uncheck it.", UiTheme.InfoColor())
                    Return
                End If
            Next

            Dim reply As New AssistantReply With {.ProviderName = _proposal.ProviderName, .Model = _proposal.Model}
            _accepted.Clear()
            For Each row As RequirementRow In used
                ' Only the text's own words are recorded as the source.
                Dim source As String = If(row.Candidate.Found, row.Candidate.Quote, String.Empty)
                _accepted.Add(New JournalChecklistTemplateItem With {
                    .Id = Guid.NewGuid(),
                    .Title = Collapse(row.Title),
                    .Category = Collapse(row.Category),
                    .IsRequired = row.IsRequired,
                    .Description = AssistantService.RequirementDescription(row.Candidate),
                    .Suggestion = AssistantService.SuggestionFor(AssistantService.AuthorInstructionsFeature, reply, source, _proposal.SuggestedUtc)
                })
            Next
            DialogResult = DialogResult.OK

        End Sub


        Private Sub SelectRow(row As RequirementRow)
            For Each gridRow As DataGridViewRow In gridRequirements.Rows
                If gridRow.Tag Is row Then
                    gridRequirements.CurrentCell = gridRow.Cells(ColumnTitle)
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
                _boldFont.Dispose()
                _formFont.Dispose()
            End If
        End Sub


        ' ---------------------------------------------------------------
        ' Helpers
        ' ---------------------------------------------------------------

        Private Sub ShowStatus(message As String, Optional color As Color? = Nothing)
            lblStatus.Text = message
            lblStatus.ForeColor = If(color, UiTheme.SecondaryText())
        End Sub


        Private Shared Function Collapse(value As String) As String
            Return Regex.Replace(If(value, String.Empty), "\s+", " ").Trim()
        End Function


        ' ---------------------------------------------------------------
        ' For tests
        ' ---------------------------------------------------------------

        Friend ReadOnly Property InstructionsBox As TextBox
            Get
                Return txtText
            End Get
        End Property

        Friend ReadOnly Property ArticleTypeBox As TextBox
            Get
                Return txtArticleType
            End Get
        End Property

        Friend ReadOnly Property SentBox As TextBox
            Get
                Return txtSent
            End Get
        End Property

        Friend ReadOnly Property SourceBox As TextBox
            Get
                Return txtSource
            End Get
        End Property

        Friend ReadOnly Property QuoteBox As TextBox
            Get
                Return txtQuote
            End Get
        End Property

        Friend ReadOnly Property RequirementsGrid As DataGridView
            Get
                Return gridRequirements
            End Get
        End Property

        Friend ReadOnly Property OpenLink As LinkLabel
            Get
                Return lnkOpen
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

        Friend ReadOnly Property CountText As String
            Get
                Return lblCount.Text
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

        Friend ReadOnly Property Rows As IReadOnlyList(Of RequirementRow)
            Get
                Return _rows.AsReadOnly()
            End Get
        End Property

        Friend Sub SetUseForTest(index As Integer, value As Boolean)
            gridRequirements.Rows(index).Cells(ColumnUse).Value = value
        End Sub

        Friend Sub SetTitleForTest(index As Integer, value As String)
            gridRequirements.Rows(index).Cells(ColumnTitle).Value = value
        End Sub

        Friend Sub SetRequiredForTest(index As Integer, value As Boolean)
            gridRequirements.Rows(index).Cells(ColumnRequired).Value = value
        End Sub

        Friend Sub SelectRequirementForTest(index As Integer)
            gridRequirements.CurrentCell = gridRequirements.Rows(index).Cells(ColumnTitle)
            gridRequirements.Rows(index).Selected = True
            ShowSelectedRequirement()
        End Sub

        Friend Sub ToggleSentForTest()
            ToggleSent()
        End Sub

        Friend Sub OpenLinkForTest()
            OpenInstructions()
        End Sub

        Friend Sub AcceptForTest()
            AcceptReview()
        End Sub

        Friend Sub CancelForTest()
            CancelClicked(Me, EventArgs.Empty)
        End Sub

    End Class

End Namespace
