Imports System
Imports System.Drawing
Imports System.Net
Imports System.Net.Http
Imports System.Threading.Tasks
Imports System.Windows.Forms
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

Namespace Forms

    ' Before an AI assistant feature first sends to a service elsewhere
    ' (#84): exactly what will be sent, and to whom. Nothing is sent unless
    ' the researcher chooses Send.
    Friend Class AssistantConsentForm
        Inherits Form

        Private ReadOnly chkRemember As New CheckBox()
        Private ReadOnly txtSent As New TextBox()

        Public Sub New(request As AssistantRequest, connection As AssistantConnection)

            Text = "Before Sending"
            Font = New Font("Segoe UI", 10.0F)
            AutoScaleMode = AutoScaleMode.Dpi
            StartPosition = FormStartPosition.CenterParent
            Size = New Size(820, 640)
            MinimumSize = New Size(560, 420)
            ShowInTaskbar = False
            MinimizeBox = False

            Dim root As New TableLayoutPanel With {.Dock = DockStyle.Fill, .ColumnCount = 1, .RowCount = 5, .Padding = New Padding(18)}
            root.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
            root.RowStyles.Add(New RowStyle(SizeType.AutoSize))
            root.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
            root.RowStyles.Add(New RowStyle(SizeType.AutoSize))
            root.RowStyles.Add(New RowStyle(SizeType.AutoSize))
            root.RowStyles.Add(New RowStyle(SizeType.AutoSize))

            Dim who As String = If(connection Is Nothing, "the AI assistant", connection.Recipient)
            Dim intro As New Label With {
                .Text = "PaperRoute will send this to " & who & If(connection?.Provider = AssistantProvider.Claude, ", using your Anthropic key", String.Empty) & ":",
                .AutoSize = True, .Dock = DockStyle.Fill, .UseMnemonic = False, .Font = New Font(Font, FontStyle.Bold), .Margin = New Padding(0, 0, 0, 8)
            }
            root.Controls.Add(intro, 0, 0)

            txtSent.Multiline = True
            txtSent.ReadOnly = True
            txtSent.ScrollBars = ScrollBars.Vertical
            txtSent.WordWrap = True
            txtSent.Dock = DockStyle.Fill
            txtSent.Text = AssistantService.WhatIsSent(request).Replace(vbLf, Environment.NewLine).Replace(vbCr & Environment.NewLine, Environment.NewLine)
            txtSent.AccessibleName = "What will be sent"
            txtSent.Select(0, 0)
            root.Controls.Add(txtSent, 0, 1)

            Dim note As New Label With {
                .Text = If(connection?.Provider = AssistantProvider.Claude,
                           "Anthropic handles it under your account's terms. Nothing changes in PaperRoute until you accept a suggestion.",
                           "That service handles it under its own terms. Nothing changes in PaperRoute until you accept a suggestion."),
                .AutoSize = True, .Dock = DockStyle.Fill, .UseMnemonic = False, .ForeColor = UiTheme.SecondaryText(), .Margin = New Padding(0, 8, 0, 4)
            }
            root.Controls.Add(note, 0, 2)

            chkRemember.Text = "Don't ask again before " & AssistantService.FeatureName(request.Feature) & " sends to " & If(connection?.Origin?.Host, "this service")
            chkRemember.AutoSize = True
            chkRemember.UseMnemonic = False
            chkRemember.Margin = New Padding(0, 4, 0, 4)
            root.Controls.Add(chkRemember, 0, 3)

            Dim footer As New FlowLayoutPanel With {.Dock = DockStyle.Fill, .AutoSize = True, .FlowDirection = FlowDirection.RightToLeft, .WrapContents = False, .Padding = New Padding(0, 10, 0, 0)}
            Dim cancel As New Button With {.Text = "Cancel", .AutoSize = True, .MinimumSize = New Size(96, 34), .DialogResult = DialogResult.Cancel}
            Dim send As New Button With {.Text = "&Send", .AutoSize = True, .MinimumSize = New Size(96, 34), .DialogResult = DialogResult.OK}
            footer.Controls.Add(cancel)
            footer.Controls.Add(send)
            root.Controls.Add(footer, 0, 4)

            Controls.Add(root)
            AcceptButton = send
            CancelButton = cancel
            AddHandler Resize, Sub(sender, e)
                                   Dim width As Integer = Math.Max(200, root.ClientSize.Width - root.Padding.Horizontal)
                                   intro.MaximumSize = New Size(width, 0)
                                   note.MaximumSize = New Size(width, 0)
                               End Sub
            UiPolish.ApplyDialog(Me)
            ActiveControl = cancel

        End Sub

        Public ReadOnly Property DontAskAgain As Boolean
            Get
                Return chkRemember.Checked
            End Get
        End Property

        ' For tests.
        Friend ReadOnly Property SentText As String
            Get
                Return txtSent.Text
            End Get
        End Property

        Friend ReadOnly Property RememberBox As CheckBox
            Get
                Return chkRemember
            End Get
        End Property

    End Class


    ' What every AI assistant window does around a request (#84): say why
    ' the assistant can't be used, ask before sending to a service
    ' elsewhere, and explain a failure in plain words.
    Friend NotInheritable Class AssistantRunner

        Private Sub New()
        End Sub

        ' Test seam for the question before sending: Nothing to cancel, else
        ' whether to stop asking for this feature and service.
        Friend Shared ConsentPrompt As Func(Of IWin32Window, AssistantRequest, AssistantConnection, Boolean?) = Nothing


        ' Whether the researcher turned the assistant on and set it up, so
        ' its buttons are shown; whether it can be used right now is
        ' Unavailable's question (Work offline, a missing key).
        Public Shared Function IsTurnedOn() As Boolean
            Return AssistantService.ProviderFactory IsNot Nothing OrElse OnlineAccess.CurrentAssistant() IsNot Nothing
        End Function


        ' Why the assistant can't be used now, or "" when it can.
        Public Shared Function Unavailable() As String
            If AssistantService.ProviderFactory IsNot Nothing Then Return String.Empty
            Dim connection As AssistantConnection = OnlineAccess.CurrentAssistant()
            Dim serviceId As String = If(connection?.ServiceId, OnlineServiceCatalog.AssistantClaude)
            Dim blocked As OnlineBlockReason? = OnlineAccess.BlockReason(serviceId)
            If blocked.HasValue Then Return OnlineAccess.BlockedMessage(OnlineServiceCatalog.Find(serviceId), blocked.Value)
            If connection Is Nothing Then Return "Set up the AI assistant in Settings > Preferences > AI assistant first."
            If connection.Provider = AssistantProvider.Claude AndAlso Not OnlineAccess.KeyStore().HasKey(ProtectedKeyStore.Anthropic) Then
                Return "Add your Claude key in Settings > Preferences > AI assistant first."
            End If
            Return String.Empty
        End Function


        ' Asks first when the service is elsewhere and the researcher hasn't
        ' said not to; False when they cancel.
        Public Shared Function Confirm(owner As IWin32Window, request As AssistantRequest) As Boolean
            If AssistantService.ProviderFactory Is Nothing AndAlso Not OnlineAccess.NeedsConfirmation(request.Feature) Then Return True
            If AssistantService.ProviderFactory IsNot Nothing AndAlso ConsentPrompt Is Nothing Then Return True
            Dim connection As AssistantConnection = OnlineAccess.CurrentAssistant()
            Dim answer As Boolean?
            If ConsentPrompt IsNot Nothing Then
                answer = ConsentPrompt(owner, request, connection)
            Else
                Using dialog As New AssistantConsentForm(request, connection)
                    answer = If(dialog.ShowDialog(owner) = DialogResult.OK, dialog.DontAskAgain, CType(Nothing, Boolean?))
                End Using
            End If
            If Not answer.HasValue Then Return False
            If answer.Value Then OnlineAccess.RememberConfirmation(request.Feature)
            Return True
        End Function


        ' A failed request in plain words, ending "Nothing was changed."
        Public Shared Function Describe(ex As Exception, Optional connection As AssistantConnection = Nothing) As String
            connection = If(connection, OnlineAccess.CurrentAssistant())
            Dim name As String = If(connection Is Nothing OrElse connection.Provider = AssistantProvider.Claude, "Claude", "The server")
            Const Unchanged As String = " Nothing was changed."

            If TypeOf ex Is OnlineServiceBlockedException OrElse TypeOf ex Is AssistantException Then Return ex.Message
            If TypeOf ex Is TaskCanceledException AndAlso TypeOf ex.InnerException Is TimeoutException Then
                Return name & " didn't answer in time." & If(connection IsNot Nothing AndAlso connection.IsOnThisComputer, " A model on this computer can take a while to load; try again.", " Try again later.") & Unchanged
            End If
            If TypeOf ex Is OperationCanceledException Then Return "Stopped." & Unchanged
            If TypeOf ex Is OnlineServiceBusyException Then Return OnlineAccess.Describe(ex, name) & Unchanged

            Dim request As HttpRequestException = TryCast(ex, HttpRequestException)
            If request IsNot Nothing Then
                If Not request.StatusCode.HasValue Then
                    If connection IsNot Nothing AndAlso connection.Provider = AssistantProvider.Compatible Then
                        Return "PaperRoute couldn't reach the server at " & connection.Origin.Authority & "." &
                            If(connection.IsOnThisComputer, " Check that it is running.", " Check the address and your internet connection.") & Unchanged
                    End If
                    Return "PaperRoute couldn't reach Claude. Check your internet connection, then try again." & Unchanged
                End If
                Dim code As Integer = CInt(request.StatusCode.Value)
                Dim codeText As String = code.ToString(Globalization.CultureInfo.InvariantCulture)
                If code = 401 OrElse code = 403 Then Return name & " didn't accept the key. Check it in Settings > Preferences > AI assistant." & Unchanged
                If code = 404 Then Return name & " didn't recognize the model or address. Check them in Settings > Preferences > AI assistant." & Unchanged
                If code = 400 OrElse code = 413 OrElse code = 422 Then Return name & " couldn't accept the request (HTTP " & codeText & "). Check the model in Settings > Preferences > AI assistant, or send less text." & Unchanged
                If code = 529 Then Return name & " is overloaded right now. Try again in a few minutes." & Unchanged
                If code >= 500 Then Return name & " had a problem on its side (HTTP " & codeText & "). Try again later." & Unchanged
                Return name & " answered with an error (HTTP " & codeText & ")." & Unchanged
            End If

            Return OnlineAccess.Describe(ex, name) & Unchanged
        End Function

    End Class

End Namespace
