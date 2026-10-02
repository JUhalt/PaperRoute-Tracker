Imports System
Imports System.ComponentModel
Imports System.Drawing
Imports System.Text.RegularExpressions
Imports System.Threading
Imports System.Threading.Tasks
Imports System.Windows.Forms
Imports ManuscriptPipeline.Services

Namespace Forms

    ' How a starting point joins the response draft.
    Friend Enum DraftPlacement
        Replace
        Below
    End Enum


    ' A starting point for one reviewer response (#84). The request runs
    ' here, with Stop, and the answer comes back as an AI suggestion to edit.
    ' Nothing changes in the comment until the researcher chooses how to use
    ' it; Stop, Cancel, and a failure change nothing.
    Friend Class AssistantDraftForm
        Inherits Form

        Private Enum Stage
            Waiting
            Running
            Result
            Failed
        End Enum

        Private ReadOnly _request As AssistantRequest
        Private ReadOnly _hasDraft As Boolean
        Private ReadOnly _recipient As String
        Private _stage As Stage = Stage.Waiting
        Private _cancellation As CancellationTokenSource
        Private _closeWhenStopped As Boolean
        Private _reply As AssistantReply
        Private _chosenText As String = String.Empty
        Private _placement As DraftPlacement = DraftPlacement.Replace

        Private ReadOnly _font As New Font("Segoe UI", 10.0F)
        Private ReadOnly _boldFont As New Font(_font, FontStyle.Bold)
        Private ReadOnly lblHeading As New Label()
        Private ReadOnly txtSent As New TextBox()
        Private ReadOnly txtSuggestion As New TextBox()
        Private ReadOnly lblStatus As New Label()
        Private ReadOnly btnPrimary As New Button()
        Private ReadOnly btnBelow As New Button()
        Private ReadOnly btnCancel As New Button()


        ' request: already confirmed by the caller; currentDraft: the
        ' response box's text, which decides between Use and Replace or Add.
        Public Sub New(request As AssistantRequest, currentDraft As String)

            If request Is Nothing Then Throw New ArgumentNullException(NameOf(request))
            _request = request
            _hasDraft = Not String.IsNullOrWhiteSpace(currentDraft)
            _recipient = RecipientText()

            BuildInterface()
            ShowRunning()
            UiPolish.ApplyDialog(Me)

        End Sub


        ' Who a request goes to, such as "Claude at api.anthropic.com".
        Friend Shared Function RecipientText() As String
            Return If(OnlineAccess.CurrentAssistant()?.Recipient, "the AI assistant")
        End Function


        ' An answer's line breaks as a Windows text box shows them.
        Friend Shared Function TextBoxText(text As String) As String
            Return Regex.Replace(If(text, String.Empty), "\r\n|\r|\n", Environment.NewLine)
        End Function


        ' The answer, after Use as Draft, Replace Draft, or Add Below.
        <DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
        Public ReadOnly Property Reply As AssistantReply
            Get
                Return If(_chosenText.Length > 0, _reply, Nothing)
            End Get
        End Property

        ' The suggestion as the researcher edited it.
        <DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
        Public ReadOnly Property ChosenText As String
            Get
                Return _chosenText
            End Get
        End Property

        <DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
        Public ReadOnly Property Placement As DraftPlacement
            Get
                Return _placement
            End Get
        End Property


        Private Sub BuildInterface()

            SuspendLayout()

            Text = AssistantService.FeatureName(AssistantService.DraftResponseFeature)
            StartPosition = FormStartPosition.CenterParent
            ShowInTaskbar = False
            MinimizeBox = False
            ' Sizes below are at 96 DPI and scale with the display.
            AutoScaleDimensions = New SizeF(96.0F, 96.0F)
            ClientSize = New Size(700, 460)
            MinimumSize = New Size(540, 380)
            Font = _font
            AutoScaleMode = AutoScaleMode.Dpi

            Dim root As New TableLayoutPanel With {.Dock = DockStyle.Fill, .ColumnCount = 1, .RowCount = 4, .Padding = New Padding(18, 16, 18, 14)}
            root.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
            root.RowStyles.Add(New RowStyle(SizeType.AutoSize))
            root.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
            root.RowStyles.Add(New RowStyle(SizeType.AutoSize))
            root.RowStyles.Add(New RowStyle(SizeType.AutoSize))

            lblHeading.AutoSize = True
            lblHeading.UseMnemonic = False
            lblHeading.Margin = New Padding(0, 0, 0, 10)

            ' What was sent, while waiting and after a failure; the
            ' suggestion to edit once it arrives.
            Dim body As New Panel With {.Dock = DockStyle.Fill, .Margin = New Padding(0)}
            txtSent.Multiline = True
            txtSent.ReadOnly = True
            txtSent.ScrollBars = ScrollBars.Vertical
            txtSent.WordWrap = True
            txtSent.Dock = DockStyle.Fill
            txtSent.AccessibleName = "What was sent"
            txtSent.Text = TextBoxText(AssistantService.WhatIsSent(_request))
            txtSent.Select(0, 0)

            txtSuggestion.Multiline = True
            txtSuggestion.AcceptsReturn = True
            txtSuggestion.ScrollBars = ScrollBars.Vertical
            txtSuggestion.WordWrap = True
            txtSuggestion.Dock = DockStyle.Fill
            txtSuggestion.Visible = False
            txtSuggestion.AccessibleName = "AI suggestion to edit"
            AddHandler txtSuggestion.TextChanged, Sub(sender, e) RefreshUseButtons()
            body.Controls.Add(txtSuggestion)
            body.Controls.Add(txtSent)

            lblStatus.AutoSize = True
            lblStatus.UseMnemonic = False
            lblStatus.ForeColor = UiTheme.SecondaryText()
            lblStatus.Margin = New Padding(0, 8, 0, 0)

            Dim buttons As New FlowLayoutPanel With {.AutoSize = True, .FlowDirection = FlowDirection.RightToLeft, .WrapContents = False, .Dock = DockStyle.Fill, .Margin = New Padding(0, 12, 0, 0)}
            For Each button As Button In {btnPrimary, btnBelow, btnCancel}
                button.AutoSize = True
                button.MinimumSize = New Size(96, 34)
            Next
            btnBelow.Margin = New Padding(0, 0, 8, 0)
            btnCancel.Margin = New Padding(0, 0, 8, 0)
            btnBelow.Text = "&Add Below"
            AddHandler btnPrimary.Click, Async Sub(sender, e) Await PrimaryAsync()
            AddHandler btnBelow.Click, Sub(sender, e) Choose(DraftPlacement.Below)
            AddHandler btnCancel.Click, AddressOf CancelClicked
            buttons.Controls.Add(btnPrimary)
            buttons.Controls.Add(btnBelow)
            buttons.Controls.Add(btnCancel)
            AcceptButton = btnPrimary
            ' Esc stops a request, or closes; a running request vetoes the close.
            ' The button's own handler decides, so Stop never closes the window.
            CancelButton = btnCancel
            btnCancel.DialogResult = DialogResult.None

            root.Controls.Add(lblHeading, 0, 0)
            root.Controls.Add(body, 0, 1)
            root.Controls.Add(lblStatus, 0, 2)
            root.Controls.Add(buttons, 0, 3)
            Controls.Add(root)

            AddHandler root.SizeChanged,
                Sub(sender, e)
                    Dim width As Integer = Math.Max(LogicalToDeviceUnits(240), root.ClientSize.Width - root.Padding.Horizontal - LogicalToDeviceUnits(4))
                    lblHeading.MaximumSize = New Size(width, 0)
                    lblStatus.MaximumSize = New Size(width, 0)
                End Sub

            ResumeLayout(False)
            PerformLayout()

        End Sub


        ' ---------------------------------------------------------------
        ' Stages
        ' ---------------------------------------------------------------

        Private Sub ShowRunning()
            lblHeading.ResetFont()
            lblHeading.ForeColor = UiTheme.PrimaryText()
            lblHeading.Text = "Asking " & _recipient & " for a starting point..."
            txtSuggestion.Visible = False
            txtSent.Visible = True
            lblStatus.ForeColor = UiTheme.SecondaryText()
            lblStatus.Text = "Nothing changes until you use the suggestion."
            btnPrimary.Visible = False
            btnBelow.Visible = False
            btnCancel.Text = "Stop"
            btnCancel.Enabled = True
        End Sub


        Private Sub ShowResult(reply As AssistantReply, text As String)
            _stage = Stage.Result
            _reply = reply
            Dim model As String = If(reply?.Model, String.Empty).Trim()
            lblHeading.Font = _boldFont
            lblHeading.ForeColor = UiTheme.PrimaryText()
            lblHeading.Text = "AI suggestion from " & If(reply?.ProviderName, "the AI assistant") &
                If(model.Length > 0, " (" & model & ")", String.Empty) & ": a draft for you to rewrite."
            txtSent.Visible = False
            txtSuggestion.Text = TextBoxText(text)
            ' The caret starts at the beginning, so typing never replaces it.
            txtSuggestion.Select(0, 0)
            txtSuggestion.Visible = True
            lblStatus.ForeColor = UiTheme.InfoColor()
            lblStatus.Text = If(reply IsNot Nothing AndAlso reply.Truncated, "The answer was cut short.", String.Empty)
            btnPrimary.Text = If(_hasDraft, "&Replace Draft", "&Use as Draft")
            btnPrimary.Visible = True
            btnBelow.Visible = _hasDraft
            btnCancel.Text = "Cancel"
            btnCancel.Enabled = True
            RefreshUseButtons()
            ActiveControl = txtSuggestion
            txtSuggestion.Select(0, 0)
            txtSuggestion.ScrollToCaret()
        End Sub


        Private Sub ShowFailure(message As String)
            _stage = Stage.Failed
            lblHeading.ResetFont()
            lblHeading.ForeColor = UiTheme.InfoColor()
            lblHeading.Text = message
            txtSuggestion.Visible = False
            txtSent.Visible = True
            lblStatus.Text = String.Empty
            btnPrimary.Text = "&Try Again"
            btnPrimary.Enabled = True
            btnPrimary.Visible = True
            btnBelow.Visible = False
            btnCancel.Text = "Close"
            btnCancel.Enabled = True
            ActiveControl = btnPrimary
        End Sub


        Private Sub RefreshUseButtons()
            If _stage <> Stage.Result Then Return
            Dim usable As Boolean = txtSuggestion.Text.Trim().Length > 0
            btnPrimary.Enabled = usable
            btnBelow.Enabled = usable
        End Sub


        ' ---------------------------------------------------------------
        ' The request
        ' ---------------------------------------------------------------

        Protected Overrides Async Sub OnShown(e As EventArgs)
            MyBase.OnShown(e)
            If _stage = Stage.Waiting Then Await RunAsync()
        End Sub


        Friend Async Function RunAsync() As Task

            If _cancellation IsNot Nothing OrElse IsDisposed Then Return

            Dim provider As IAssistantProvider = AssistantService.CurrentProvider()
            If provider Is Nothing Then
                Dim reason As String = AssistantRunner.Unavailable()
                ShowFailure(If(reason.Length > 0, reason, "Set up the AI assistant in Settings > Preferences > AI assistant first."))
                Return
            End If

            _stage = Stage.Running
            ShowRunning()
            _cancellation = New CancellationTokenSource()
            Dim token As CancellationToken = _cancellation.Token

            Try
                Dim answer As AssistantReply = Await provider.CompleteAsync(_request, token)
                If IsDisposed Then Return
                If token.IsCancellationRequested Then Throw New OperationCanceledException(token)
                ShowResult(answer, AssistantService.TextOf(answer))
            Catch ex As Exception
                If IsDisposed Then Return
                ShowFailure(AssistantRunner.Describe(ex))
            Finally
                _cancellation?.Dispose()
                _cancellation = Nothing
            End Try

            If _closeWhenStopped AndAlso Not IsDisposed Then
                DialogResult = DialogResult.Cancel
                Close()
            End If

        End Function


        Private Async Function PrimaryAsync() As Task
            Select Case _stage
                Case Stage.Result
                    Choose(DraftPlacement.Replace)
                Case Stage.Failed
                    ' Every send is confirmed, as the first one was.
                    If Not AssistantRunner.Confirm(Me, _request) Then Return
                    Await RunAsync()
            End Select
        End Function


        Private Sub Choose(placement As DraftPlacement)
            If _stage <> Stage.Result Then Return
            Dim text As String = txtSuggestion.Text.Trim()
            If text.Length = 0 Then Return
            _chosenText = text
            _placement = placement
            DialogResult = DialogResult.OK
            Close()
        End Sub


        Private Sub CancelClicked(sender As Object, e As EventArgs)
            If _cancellation IsNot Nothing Then
                btnCancel.Enabled = False
                _cancellation.Cancel()
                Return
            End If
            DialogResult = DialogResult.Cancel
            Close()
        End Sub


        ' Closing while a request runs stops it first, then closes.
        Protected Overrides Sub OnFormClosing(e As FormClosingEventArgs)
            If _cancellation IsNot Nothing Then
                Dim closeAfter As Boolean = e.CloseReason = CloseReason.UserClosing OrElse e.CloseReason = CloseReason.None
                If closeAfter Then _closeWhenStopped = True
                btnCancel.Enabled = False
                _cancellation.Cancel()
                If closeAfter Then
                    e.Cancel = True
                    Return
                End If
            End If
            MyBase.OnFormClosing(e)
        End Sub


        Protected Overrides Sub Dispose(disposing As Boolean)
            If disposing Then
                If _cancellation IsNot Nothing Then
                    _cancellation.Cancel()
                    _cancellation.Dispose()
                    _cancellation = Nothing
                End If
            End If
            MyBase.Dispose(disposing)
            If disposing Then
                _boldFont.Dispose()
                _font.Dispose()
            End If
        End Sub


        ' ---------------------------------------------------------------
        ' For tests
        ' ---------------------------------------------------------------

        Friend ReadOnly Property IsRunning As Boolean
            Get
                Return _cancellation IsNot Nothing
            End Get
        End Property

        Friend ReadOnly Property HasResult As Boolean
            Get
                Return _stage = Stage.Result
            End Get
        End Property

        Friend ReadOnly Property HasFailed As Boolean
            Get
                Return _stage = Stage.Failed
            End Get
        End Property

        Friend ReadOnly Property HeadingText As String
            Get
                Return lblHeading.Text
            End Get
        End Property

        Friend ReadOnly Property StatusText As String
            Get
                Return lblStatus.Text
            End Get
        End Property

        Friend ReadOnly Property SuggestionBox As TextBox
            Get
                Return txtSuggestion
            End Get
        End Property

        Friend ReadOnly Property PrimaryAction As Button
            Get
                Return btnPrimary
            End Get
        End Property

        Friend ReadOnly Property AddBelowAction As Button
            Get
                Return btnBelow
            End Get
        End Property

        Friend ReadOnly Property DismissAction As Button
            Get
                Return btnCancel
            End Get
        End Property

    End Class

End Namespace
