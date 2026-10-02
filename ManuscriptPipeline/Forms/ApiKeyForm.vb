Imports System
Imports System.Drawing
Imports System.Linq
Imports System.Windows.Forms
Imports ManuscriptPipeline.Services

Namespace Forms

    ' Pastes a key the researcher chooses to add: a free OpenAlex key (#86),
    ' or the AI assistant's Anthropic or server key (#84). PaperRoute never
    ' ships a key of its own; each is kept encrypted on this computer and
    ' sent only to its own service, in a header.
    Public Class ApiKeyForm
        Inherits Form

        Private ReadOnly txtKey As New TextBox()
        Private ReadOnly chkShow As New CheckBox()
        Private ReadOnly btnOk As New Button()
        Private ReadOnly lblHint As New Label()
        Private ReadOnly root As New TableLayoutPanel()
        Private ReadOnly _keyPage As String
        Private ReadOnly _keyNoun As String

        Public Const AnthropicKeyPage As String = "https://console.anthropic.com/settings/keys"


        ' title: "Add OpenAlex Key"; intro: what the key is for and where it
        ' goes; keyPage and pageLinkText: where to get one, if anywhere;
        ' keyNoun: "an OpenAlex key", for the hint about a pasted value.
        Public Sub New(title As String, intro As String, keyPage As String, pageLinkText As String, keyName As String, keyNoun As String)
            _keyPage = keyPage
            _keyNoun = keyNoun
            BuildInterface(title, intro, pageLinkText, keyName)
            UiPolish.ApplyDialog(Me)
        End Sub


        ' The AI assistant's key dialog for a key store name (#84).
        Friend Shared Function ForAssistant(keyName As String) As ApiKeyForm
            If keyName = ProtectedKeyStore.Anthropic Then
                Return New ApiKeyForm(
                    "Add Claude Key",
                    "Paste an API key from your Anthropic account. Anthropic charges that account for what the assistant uses." &
                    Environment.NewLine & Environment.NewLine &
                    "PaperRoute keeps your Anthropic key encrypted for your Windows account, sends it only to api.anthropic.com in a request header, and never puts it in backups, exports, or Diagnostics.",
                    AnthropicKeyPage,
                    "Open console.anthropic.com/settings/keys",
                    "Claude key",
                    "an Anthropic key")
            End If
            Return New ApiKeyForm(
                "Add Server Key",
                "Paste the key your server gave you, if it needs one. A model on this computer usually doesn't." &
                Environment.NewLine & Environment.NewLine &
                "PaperRoute sends it only to the address you set, and only while that address stays the same. It keeps the key encrypted for your Windows account and never puts it in backups, exports, or Diagnostics.",
                Nothing,
                Nothing,
                "Server key",
                "a server key")
        End Function


        <ComponentModel.DesignerSerializationVisibility(ComponentModel.DesignerSerializationVisibility.Hidden)>
        Public ReadOnly Property Key As String
            Get
                Return txtKey.Text.Trim()
            End Get
        End Property


        ' Sized to its content once scaled, so nothing is clipped at any scale.
        Protected Overrides Sub OnLoad(e As EventArgs)
            MyBase.OnLoad(e)
            Me.ClientSize = New Size(Me.ClientSize.Width, root.GetPreferredSize(New Size(Me.ClientSize.Width, 0)).Height)
            Me.ActiveControl = txtKey
        End Sub


        Private Sub BuildInterface(title As String, intro As String, pageLinkText As String, keyName As String)

            ' Laid out once, after every control exists, so the scale for
            ' the display reaches all of them.
            Me.SuspendLayout()

            Me.Text = title
            Me.StartPosition = FormStartPosition.CenterParent
            Me.FormBorderStyle = FormBorderStyle.FixedDialog
            Me.MaximizeBox = False
            Me.MinimizeBox = False
            Me.ShowInTaskbar = False
            ' Sizes below are at 96 DPI and scale with the display.
            Me.AutoScaleDimensions = New SizeF(96.0F, 96.0F)
            Me.ClientSize = New Size(560, 280)
            Me.Font = New Font("Segoe UI", 10.0F)
            Me.AutoScaleMode = AutoScaleMode.Dpi

            root.Dock = DockStyle.Fill
            root.ColumnCount = 1
            root.RowCount = 5
            root.Padding = New Padding(20, 16, 20, 12)
            root.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
            For index As Integer = 0 To 4
                root.RowStyles.Add(New RowStyle(SizeType.AutoSize))
            Next

            Dim lblIntro As New Label With {
                .Text = intro,
                .AutoSize = True,
                .MaximumSize = New Size(520, 0),
                .UseMnemonic = False,
                .Margin = New Padding(0, 0, 0, 10)
            }

            Dim keyRow As New TableLayoutPanel With {.Dock = DockStyle.Top, .AutoSize = True, .ColumnCount = 3, .RowCount = 1, .Margin = New Padding(0)}
            keyRow.ColumnStyles.Add(New ColumnStyle(SizeType.AutoSize))
            keyRow.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
            keyRow.ColumnStyles.Add(New ColumnStyle(SizeType.AutoSize))
            Dim lblKey As New Label With {.Text = "&Key", .AutoSize = True, .Anchor = AnchorStyles.Left, .Margin = New Padding(0, 4, 10, 4)}
            txtKey.Dock = DockStyle.Fill
            txtKey.UseSystemPasswordChar = True
            txtKey.AccessibleName = keyName
            AddHandler txtKey.TextChanged, Sub(sender, e) RefreshState()
            chkShow.Text = "Show"
            chkShow.AutoSize = True
            chkShow.Margin = New Padding(10, 4, 0, 4)
            AddHandler chkShow.CheckedChanged, Sub(sender, e) txtKey.UseSystemPasswordChar = Not chkShow.Checked
            keyRow.Controls.Add(lblKey, 0, 0)
            keyRow.Controls.Add(txtKey, 1, 0)
            keyRow.Controls.Add(chkShow, 2, 0)

            lblHint.AutoSize = True
            lblHint.UseMnemonic = False
            lblHint.ForeColor = UiTheme.SecondaryText()
            lblHint.Margin = New Padding(0, 6, 0, 0)

            Dim buttons As New FlowLayoutPanel With {.Dock = DockStyle.Fill, .AutoSize = True, .FlowDirection = FlowDirection.RightToLeft, .WrapContents = False, .Margin = New Padding(0, 12, 0, 0)}
            btnOk.Text = "Add Key"
            btnOk.AutoSize = True
            btnOk.MinimumSize = New Size(96, 34)
            btnOk.DialogResult = DialogResult.OK
            Dim btnCancel As New Button With {.Text = "Cancel", .AutoSize = True, .MinimumSize = New Size(96, 34), .DialogResult = DialogResult.Cancel}
            buttons.Controls.Add(btnOk)
            buttons.Controls.Add(btnCancel)

            root.Controls.Add(lblIntro, 0, 0)
            If Not String.IsNullOrEmpty(_keyPage) Then
                Dim lnkPage As New LinkLabel With {.Text = pageLinkText, .AutoSize = True, .Margin = New Padding(0, 0, 0, 10)}
                AddHandler lnkPage.LinkClicked, AddressOf OpenKeyPage
                root.Controls.Add(lnkPage, 0, 1)
            End If
            root.Controls.Add(keyRow, 0, 2)
            root.Controls.Add(lblHint, 0, 3)
            root.Controls.Add(buttons, 0, 4)

            Me.AcceptButton = btnOk
            Me.CancelButton = btnCancel
            Me.Controls.Add(root)
            RefreshState()

            Me.ResumeLayout(False)
            Me.PerformLayout()

        End Sub


        Private Sub OpenKeyPage(sender As Object, e As LinkLabelLinkClickedEventArgs)
            Try
                UrlSafetyService.OpenInBrowser(_keyPage)
            Catch ex As Exception
                MessageBox.Show(Me, ex.Message, "Open Web Page", MessageBoxButtons.OK, MessageBoxIcon.Information)
            End Try
        End Sub


        Private Sub RefreshState()
            Dim value As String = txtKey.Text.Trim()
            btnOk.Enabled = ProtectedKeyStore.IsPlausibleKey(value)
            lblHint.Text = HintFor(value, _keyNoun)
        End Sub


        ' Why a pasted value isn't accepted; nothing while a short key is
        ' still being typed.
        Friend Shared Function HintFor(value As String, keyNoun As String) As String
            If String.IsNullOrEmpty(value) OrElse ProtectedKeyStore.IsPlausibleKey(value) Then Return String.Empty
            If value.Any(Function(character) character <= " "c OrElse character > "~"c) Then Return "A key is one word, without spaces, line breaks, or accented letters."
            If value.Length > 200 Then Return "That's longer than " & keyNoun & ". Copy just the key."
            Return String.Empty
        End Function


        ' For tests.
        Friend ReadOnly Property KeyBox As TextBox
            Get
                Return txtKey
            End Get
        End Property

        Friend ReadOnly Property AddButton As Button
            Get
                Return btnOk
            End Get
        End Property

    End Class

End Namespace
