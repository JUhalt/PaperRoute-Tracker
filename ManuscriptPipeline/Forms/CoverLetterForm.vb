Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.IO
Imports System.Linq
Imports System.Runtime.InteropServices
Imports System.Text
Imports System.Threading
Imports System.Threading.Tasks
Imports System.Windows.Forms
Imports ManuscriptPipeline.Services

Namespace Forms

    ' A starting point for a cover letter (#84). The researcher checks the
    ' few details that are sent, sees exactly what will be sent, and gets back
    ' a letter to rewrite, copy, or save as a file. Nothing is written to the
    ' library: the details edited here are used only for this request.
    Friend Class CoverLetterForm
        Inherits Form

        Private Enum Stage
            Ready
            Running
            Result
        End Enum

        Private ReadOnly _workType As String
        Private ReadOnly _factsFor As Func(Of String, String)
        Private ReadOnly _recipient As String
        Private _stage As Stage = Stage.Ready
        Private _cancellation As CancellationTokenSource
        Private _closeWhenStopped As Boolean
        Private _reply As AssistantReply
        Private _sentShown As Boolean

        Private ReadOnly _font As New Font("Segoe UI", 10.0F)
        Private ReadOnly _boldFont As New Font(_font, FontStyle.Bold)
        Private ReadOnly lblIntro As New Label()
        Private ReadOnly readyPanel As New TableLayoutPanel()
        Private ReadOnly txtJournal As New TextBox()
        Private ReadOnly lblFacts As New Label()
        Private ReadOnly txtTitle As New TextBox()
        Private ReadOnly txtKeywords As New TextBox()
        Private ReadOnly lblWorkType As New Label()
        Private ReadOnly txtAbstract As New TextBox()
        Private ReadOnly lnkSent As New LinkLabel()
        Private ReadOnly txtSent As New TextBox()
        Private ReadOnly txtLetter As New TextBox()
        Private ReadOnly lblStatus As New Label()
        Private ReadOnly btnPrimary As New Button()
        Private ReadOnly btnSave As New Button()
        Private ReadOnly btnAgain As New Button()
        Private ReadOnly btnClose As New Button()

        ' Test seams: the path Save As writes to (Nothing cancels), and the
        ' clipboard.
        Friend SavePathPrompt As Func(Of String) = Nothing
        Friend ClipboardWriter As Action(Of String) = Nothing


        ' The page's working values. factsFor gives one line of a journal's
        ' facts from the Journal Library, or "", for the journal typed here.
        Public Sub New(title As String, journal As String, workType As String, keywords As IEnumerable(Of String), abstractText As String,
                       Optional factsFor As Func(Of String, String) = Nothing)

            _workType = If(workType, String.Empty).Trim()
            _factsFor = If(factsFor, Function(name) String.Empty)
            _recipient = AssistantDraftForm.RecipientText()

            BuildInterface()
            txtJournal.Text = If(journal, String.Empty).Trim()
            txtTitle.Text = If(title, String.Empty).Trim()
            txtKeywords.Text = String.Join(", ", If(keywords, Enumerable.Empty(Of String)()).Where(Function(item) Not String.IsNullOrWhiteSpace(item)).Select(Function(item) item.Trim()))
            txtAbstract.Text = AssistantDraftForm.TextBoxText(If(abstractText, String.Empty).Trim())
            For Each box As TextBox In {txtJournal, txtTitle, txtKeywords, txtAbstract}
                box.Select(0, 0)
            Next
            ShowReady()
            RefreshFacts()
            UiPolish.ApplyDialog(Me)
            ActiveControl = txtJournal

        End Sub


        Private Sub BuildInterface()

            SuspendLayout()

            Text = AssistantService.FeatureName(AssistantService.CoverLetterFeature)
            StartPosition = FormStartPosition.CenterParent
            ShowInTaskbar = False
            MinimizeBox = False
            ' Sizes below are at 96 DPI and scale with the display.
            AutoScaleDimensions = New SizeF(96.0F, 96.0F)
            ClientSize = New Size(780, 660)
            MinimumSize = New Size(620, 560)
            Font = _font
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

            BuildReadyPanel()

            txtLetter.Multiline = True
            txtLetter.AcceptsReturn = True
            txtLetter.ScrollBars = ScrollBars.Vertical
            txtLetter.WordWrap = True
            txtLetter.Dock = DockStyle.Fill
            txtLetter.Visible = False
            txtLetter.AccessibleName = "Cover letter to edit"
            AddHandler txtLetter.TextChanged, Sub(sender, e) RefreshLetterButtons()

            Dim body As New Panel With {.Dock = DockStyle.Fill, .Margin = New Padding(0)}
            body.Controls.Add(txtLetter)
            body.Controls.Add(readyPanel)

            lblStatus.AutoSize = True
            lblStatus.UseMnemonic = False
            lblStatus.Margin = New Padding(0, 8, 0, 0)

            Dim buttons As New FlowLayoutPanel With {.AutoSize = True, .FlowDirection = FlowDirection.RightToLeft, .WrapContents = False, .Dock = DockStyle.Fill, .Margin = New Padding(0, 12, 0, 0)}
            For Each button As Button In {btnPrimary, btnSave, btnAgain, btnClose}
                button.AutoSize = True
                button.MinimumSize = New Size(96, 34)
                If button IsNot btnPrimary Then button.Margin = New Padding(0, 0, 8, 0)
                buttons.Controls.Add(button)
            Next
            btnSave.Text = "&Save As..."
            btnAgain.Text = "Draft &Again"
            AddHandler btnPrimary.Click, Async Sub(sender, e) Await PrimaryAsync()
            AddHandler btnSave.Click, Sub(sender, e) SaveLetter()
            AddHandler btnAgain.Click, Sub(sender, e) DraftAgain()
            AddHandler btnClose.Click, AddressOf CloseClicked
            AcceptButton = btnPrimary
            ' Esc stops a request, or closes; a running request vetoes the close.
            CancelButton = btnClose

            root.Controls.Add(lblIntro, 0, 0)
            root.Controls.Add(body, 0, 1)
            root.Controls.Add(lblStatus, 0, 2)
            root.Controls.Add(buttons, 0, 3)
            Controls.Add(root)

            AddHandler root.SizeChanged,
                Sub(sender, e)
                    Dim width As Integer = Math.Max(LogicalToDeviceUnits(240), root.ClientSize.Width - root.Padding.Horizontal - LogicalToDeviceUnits(4))
                    lblIntro.MaximumSize = New Size(width, 0)
                    lblStatus.MaximumSize = New Size(width, 0)
                End Sub

            ResumeLayout(False)
            PerformLayout()

        End Sub


        Private Sub BuildReadyPanel()

            readyPanel.Dock = DockStyle.Fill
            readyPanel.ColumnCount = 2
            readyPanel.RowCount = 9
            readyPanel.Margin = New Padding(0)
            readyPanel.ColumnStyles.Add(New ColumnStyle(SizeType.AutoSize))
            readyPanel.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
            For row As Integer = 0 To 8
                readyPanel.RowStyles.Add(If(row = 6, New RowStyle(SizeType.Percent, 100), New RowStyle(SizeType.AutoSize)))
            Next

            AddField("&Journal", txtJournal, 0)
            lblFacts.AutoSize = True
            lblFacts.Dock = DockStyle.Fill
            lblFacts.UseMnemonic = False
            lblFacts.ForeColor = UiTheme.MutedText()
            lblFacts.Margin = New Padding(0, 0, 0, 4)
            readyPanel.Controls.Add(lblFacts, 1, 1)
            AddField("&Title", txtTitle, 2)
            AddField("&Keywords (comma-separated)", txtKeywords, 3)

            readyPanel.Controls.Add(FieldLabel("Type of work"), 0, 4)
            lblWorkType.AutoSize = True
            lblWorkType.Anchor = AnchorStyles.Left
            lblWorkType.UseMnemonic = False
            lblWorkType.Margin = New Padding(0, 6, 0, 6)
            lblWorkType.Text = If(_workType.Length > 0, _workType, "Not specified")
            readyPanel.Controls.Add(lblWorkType, 1, 4)

            readyPanel.Controls.Add(FieldLabel("&Abstract"), 0, 5)
            Dim note As New Label With {
                .Text = "Used only for this request; PaperRoute doesn't save it.",
                .AutoSize = True, .Anchor = AnchorStyles.Left, .UseMnemonic = False,
                .ForeColor = UiTheme.MutedText(), .Margin = New Padding(0, 10, 0, 2)
            }
            readyPanel.Controls.Add(note, 1, 5)

            txtAbstract.Multiline = True
            txtAbstract.AcceptsReturn = True
            txtAbstract.ScrollBars = ScrollBars.Vertical
            txtAbstract.WordWrap = True
            txtAbstract.Dock = DockStyle.Fill
            txtAbstract.Margin = New Padding(0, 2, 0, 4)
            txtAbstract.AccessibleName = "Abstract"
            readyPanel.Controls.Add(txtAbstract, 0, 6)
            readyPanel.SetColumnSpan(txtAbstract, 2)

            lnkSent.Text = "Show what will be sent"
            lnkSent.AutoSize = True
            lnkSent.Margin = New Padding(0, 6, 0, 0)
            AddHandler lnkSent.LinkClicked, Sub(sender, e) ToggleSent()
            readyPanel.Controls.Add(lnkSent, 0, 7)
            readyPanel.SetColumnSpan(lnkSent, 2)

            txtSent.Multiline = True
            txtSent.ReadOnly = True
            txtSent.ScrollBars = ScrollBars.Vertical
            txtSent.WordWrap = True
            txtSent.Dock = DockStyle.Fill
            txtSent.Visible = False
            txtSent.Margin = New Padding(0, 4, 0, 0)
            txtSent.AccessibleName = "What will be sent"
            readyPanel.Controls.Add(txtSent, 0, 8)
            readyPanel.SetColumnSpan(txtSent, 2)

            AddHandler txtJournal.TextChanged, Sub(sender, e) RefreshFacts()
            For Each box As TextBox In {txtTitle, txtKeywords, txtAbstract}
                AddHandler box.TextChanged, Sub(sender, e) RefreshSent()
            Next

        End Sub


        Private Sub AddField(label As String, field As TextBox, row As Integer)
            readyPanel.Controls.Add(FieldLabel(label), 0, row)
            field.Dock = DockStyle.Fill
            field.Margin = New Padding(0, 4, 0, 4)
            field.AccessibleName = label.Replace("&", String.Empty)
            readyPanel.Controls.Add(field, 1, row)
        End Sub


        Private Shared Function FieldLabel(text As String) As Label
            Return New Label With {.Text = text, .AutoSize = True, .Anchor = AnchorStyles.Left, .Margin = New Padding(0, 6, 12, 6)}
        End Function


        ' ---------------------------------------------------------------
        ' What is sent
        ' ---------------------------------------------------------------

        ' The keywords as typed, split at commas or semicolons.
        Friend Shared Function KeywordsOf(text As String) As List(Of String)
            Return If(text, String.Empty).Split({","c, ";"c, ControlChars.Cr, ControlChars.Lf}, StringSplitOptions.RemoveEmptyEntries).
                Select(Function(item) item.Trim()).Where(Function(item) item.Length > 0).
                Distinct(StringComparer.OrdinalIgnoreCase).ToList()
        End Function


        Friend Function CurrentRequest() As AssistantRequest
            Return AssistantService.BuildCoverLetterRequest(txtTitle.Text, txtJournal.Text, _workType, KeywordsOf(txtKeywords.Text), txtAbstract.Text, FactsLine())
        End Function


        Private Function FactsLine() As String
            If String.IsNullOrWhiteSpace(txtJournal.Text) Then Return String.Empty
            Return If(_factsFor(txtJournal.Text), String.Empty).Trim()
        End Function


        Private Sub RefreshFacts()
            Dim facts As String = FactsLine()
            lblFacts.Text = If(facts.Length > 0, "From your Journal Library: " & facts, String.Empty)
            lblFacts.Visible = facts.Length > 0
            RefreshSent()
        End Sub


        Private Sub ToggleSent()
            Dim show As Boolean = Not _sentShown
            _sentShown = show
            readyPanel.SuspendLayout()
            readyPanel.RowStyles(6) = New RowStyle(SizeType.Percent, If(show, 50, 100))
            readyPanel.RowStyles(8) = If(show, New RowStyle(SizeType.Percent, 50), New RowStyle(SizeType.AutoSize))
            txtSent.Visible = show
            readyPanel.ResumeLayout(True)
            lnkSent.Text = If(show, "Hide what will be sent", "Show what will be sent")
            RefreshSent()
        End Sub


        Private Sub RefreshSent()
            If Not _sentShown Then Return
            txtSent.Text = AssistantDraftForm.TextBoxText(AssistantService.WhatIsSent(CurrentRequest()))
            txtSent.Select(0, 0)
        End Sub


        ' ---------------------------------------------------------------
        ' Stages
        ' ---------------------------------------------------------------

        Private Sub ShowReady()
            _stage = Stage.Ready
            lblIntro.ResetFont()
            lblIntro.Text = "Draft sends only these details to " & _recipient & ", with PaperRoute's instructions. Author names and your notes are never sent."
            txtLetter.Visible = False
            readyPanel.Visible = True
            SetEditable(True)
            btnPrimary.Text = "&Draft"
            btnPrimary.Visible = True
            btnPrimary.Enabled = True
            btnSave.Visible = False
            btnAgain.Visible = False
            btnClose.Text = "Close"
            btnClose.Enabled = True
        End Sub


        Private Sub ShowRunning()
            _stage = Stage.Running
            SetEditable(False)
            SetStatus("Asking " & _recipient & " for a starting point...", False)
            btnPrimary.Visible = False
            btnClose.Text = "Stop"
            btnClose.Enabled = True
        End Sub


        Private Sub ShowResult(reply As AssistantReply, text As String)
            _stage = Stage.Result
            _reply = reply
            Dim model As String = If(reply?.Model, String.Empty).Trim()
            lblIntro.Font = _boldFont
            lblIntro.Text = "AI suggestion from " & If(reply?.ProviderName, "the AI assistant") &
                If(model.Length > 0, " (" & model & ")", String.Empty) & ": a starting point to rewrite. Fill in the [bracketed] parts."
            readyPanel.Visible = False
            txtLetter.Text = AssistantDraftForm.TextBoxText(text)
            txtLetter.Select(0, 0)
            txtLetter.Visible = True
            SetStatus(If(reply IsNot Nothing AndAlso reply.Truncated, "The answer was cut short.", String.Empty), True)
            btnPrimary.Text = "&Copy"
            btnPrimary.Visible = True
            btnSave.Visible = True
            btnAgain.Visible = True
            btnClose.Text = "Close"
            btnClose.Enabled = True
            RefreshLetterButtons()
            ActiveControl = txtLetter
            txtLetter.Select(0, 0)
            txtLetter.ScrollToCaret()
        End Sub


        Private Sub SetEditable(editable As Boolean)
            For Each box As TextBox In {txtJournal, txtTitle, txtKeywords, txtAbstract}
                box.ReadOnly = Not editable
            Next
        End Sub


        Private Sub SetStatus(text As String, notice As Boolean)
            lblStatus.ForeColor = If(notice, UiTheme.InfoColor(), UiTheme.SecondaryText())
            lblStatus.Text = text
        End Sub


        Private Sub RefreshLetterButtons()
            If _stage <> Stage.Result Then Return
            Dim usable As Boolean = txtLetter.Text.Trim().Length > 0
            btnPrimary.Enabled = usable
            btnSave.Enabled = usable
        End Sub


        ' ---------------------------------------------------------------
        ' The request
        ' ---------------------------------------------------------------

        Private Async Function PrimaryAsync() As Task
            Select Case _stage
                Case Stage.Ready : Await DraftAsync()
                Case Stage.Result : CopyLetter()
            End Select
        End Function


        Friend Async Function DraftAsync() As Task

            If _stage <> Stage.Ready OrElse _cancellation IsNot Nothing OrElse IsDisposed Then Return

            Dim reason As String = AssistantRunner.Unavailable()
            If reason.Length > 0 Then
                SetStatus(reason, True)
                Return
            End If
            If String.IsNullOrWhiteSpace(txtTitle.Text) Then
                SetStatus("Enter the title first.", True)
                txtTitle.Focus()
                Return
            End If

            Dim request As AssistantRequest = CurrentRequest()
            If Not AssistantRunner.Confirm(Me, request) Then
                SetStatus("Nothing was sent.", False)
                Return
            End If
            Dim provider As IAssistantProvider = AssistantService.CurrentProvider()
            If provider Is Nothing Then
                SetStatus("Set up the AI assistant in Settings > Preferences > AI assistant first.", True)
                Return
            End If

            ShowRunning()
            _cancellation = New CancellationTokenSource()
            Dim token As CancellationToken = _cancellation.Token

            Try
                Dim answer As AssistantReply = Await provider.CompleteAsync(request, token)
                If IsDisposed Then Return
                If token.IsCancellationRequested Then Throw New OperationCanceledException(token)
                ShowResult(answer, AssistantService.TextOf(answer))
            Catch ex As Exception
                If IsDisposed Then Return
                ShowReady()
                SetStatus(AssistantRunner.Describe(ex), True)
            Finally
                _cancellation?.Dispose()
                _cancellation = Nothing
            End Try

            If _closeWhenStopped AndAlso Not IsDisposed Then
                DialogResult = DialogResult.Cancel
                Close()
            End If

        End Function


        ' Back to the details, as they were, to change any and draft again.
        Private Sub DraftAgain()
            If _stage <> Stage.Result Then Return
            ShowReady()
            SetStatus("Change any details if you like, then choose Draft.", False)
            ActiveControl = btnPrimary
        End Sub


        Private Sub CopyLetter()
            Dim text As String = txtLetter.Text
            If text.Trim().Length = 0 Then Return
            Try
                If ClipboardWriter IsNot Nothing Then
                    ClipboardWriter(text)
                Else
                    Clipboard.SetText(text)
                End If
                SetStatus("Copied.", False)
            Catch ex As Exception When TypeOf ex Is ExternalException OrElse TypeOf ex Is InvalidOperationException
                SetStatus("The clipboard is busy; try Copy again.", True)
            End Try
        End Sub


        Private Sub SaveLetter()
            Dim text As String = txtLetter.Text
            If text.Trim().Length = 0 Then Return
            Dim target As String = If(SavePathPrompt IsNot Nothing, SavePathPrompt(), PickSavePath())
            If String.IsNullOrWhiteSpace(target) Then Return
            Try
                File.WriteAllText(target, text, New UTF8Encoding(False))
                SetStatus("Saved as " & Path.GetFileName(target) & ".", False)
            Catch ex As Exception When TypeOf ex Is IOException OrElse TypeOf ex Is UnauthorizedAccessException OrElse
                                       TypeOf ex Is Security.SecurityException OrElse TypeOf ex Is ArgumentException OrElse TypeOf ex Is NotSupportedException
                SetStatus("The letter couldn't be saved. " & ex.Message, True)
            End Try
        End Sub


        Private Function PickSavePath() As String
            Using picker As New SaveFileDialog With {
                .Title = "Save Cover Letter", .Filter = "Text (*.txt)|*.txt|Markdown (*.md)|*.md",
                .DefaultExt = "txt", .AddExtension = True, .OverwritePrompt = True,
                .FileName = "Cover letter.txt"
            }
                Return If(picker.ShowDialog(Me) = DialogResult.OK, picker.FileName, Nothing)
            End Using
        End Function


        Private Sub CloseClicked(sender As Object, e As EventArgs)
            If _cancellation IsNot Nothing Then
                _cancellation.Cancel()
                btnClose.Enabled = False
                Return
            End If
            DialogResult = DialogResult.Cancel
            Close()
        End Sub


        ' Closing while a request runs stops it first, then closes.
        Protected Overrides Sub OnFormClosing(e As FormClosingEventArgs)
            If _cancellation IsNot Nothing Then
                _cancellation.Cancel()
                btnClose.Enabled = False
                If e.CloseReason = CloseReason.UserClosing OrElse e.CloseReason = CloseReason.None Then
                    _closeWhenStopped = True
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

        Friend ReadOnly Property FactsText As String
            Get
                Return lblFacts.Text
            End Get
        End Property

        Friend ReadOnly Property WorkTypeText As String
            Get
                Return lblWorkType.Text
            End Get
        End Property

        Friend ReadOnly Property JournalBox As TextBox
            Get
                Return txtJournal
            End Get
        End Property

        Friend ReadOnly Property TitleBox As TextBox
            Get
                Return txtTitle
            End Get
        End Property

        Friend ReadOnly Property KeywordsBox As TextBox
            Get
                Return txtKeywords
            End Get
        End Property

        Friend ReadOnly Property AbstractBox As TextBox
            Get
                Return txtAbstract
            End Get
        End Property

        Friend ReadOnly Property SentBox As TextBox
            Get
                Return txtSent
            End Get
        End Property

        Friend ReadOnly Property LetterBox As TextBox
            Get
                Return txtLetter
            End Get
        End Property

        Friend ReadOnly Property SentLink As LinkLabel
            Get
                Return lnkSent
            End Get
        End Property

        Friend ReadOnly Property PrimaryAction As Button
            Get
                Return btnPrimary
            End Get
        End Property

        Friend ReadOnly Property SaveAction As Button
            Get
                Return btnSave
            End Get
        End Property

        Friend ReadOnly Property AgainAction As Button
            Get
                Return btnAgain
            End Get
        End Property

        Friend ReadOnly Property CloseAction As Button
            Get
                Return btnClose
            End Get
        End Property

        Friend Sub ToggleSentForTest()
            ToggleSent()
        End Sub

    End Class

End Namespace
