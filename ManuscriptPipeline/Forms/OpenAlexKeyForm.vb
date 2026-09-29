Imports System
Imports System.Drawing
Imports System.Linq
Imports System.Windows.Forms
Imports ManuscriptPipeline.Services

Namespace Forms

    ' Pastes a free personal OpenAlex key (#86). PaperRoute never ships a
    ' key of its own; the user's key is kept encrypted on this computer and
    ' sent only to api.openalex.org, in a header.
    Public Class OpenAlexKeyForm
        Inherits Form

        Private ReadOnly txtKey As New TextBox()
        Private ReadOnly chkShow As New CheckBox()
        Private ReadOnly btnOk As New Button()
        Private ReadOnly lblHint As New Label()
        Private ReadOnly root As New TableLayoutPanel()

        Public Const KeyPage As String = "https://openalex.org/settings/api"


        Public Sub New()
            BuildInterface()
            UiPolish.ApplyDialog(Me)
        End Sub


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


        Private Sub BuildInterface()

            ' Laid out once, after every control exists, so the scale for
            ' the display reaches all of them.
            Me.SuspendLayout()

            Me.Text = "Add OpenAlex Key"
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
                .Text = "A free OpenAlex key raises OpenAlex's daily allowance tenfold. Sign in at openalex.org, copy your key from Settings > API, and paste it here." &
                        Environment.NewLine & Environment.NewLine &
                        "PaperRoute keeps it encrypted on this computer, for your Windows account only, and sends it only to api.openalex.org. It is never in your backups or exports.",
                .AutoSize = True,
                .MaximumSize = New Size(520, 0),
                .UseMnemonic = False,
                .Margin = New Padding(0, 0, 0, 10)
            }

            Dim lnkPage As New LinkLabel With {.Text = "Open openalex.org/settings/api", .AutoSize = True, .Margin = New Padding(0, 0, 0, 10)}
            AddHandler lnkPage.LinkClicked, AddressOf OpenKeyPage

            Dim keyRow As New TableLayoutPanel With {.Dock = DockStyle.Top, .AutoSize = True, .ColumnCount = 3, .RowCount = 1, .Margin = New Padding(0)}
            keyRow.ColumnStyles.Add(New ColumnStyle(SizeType.AutoSize))
            keyRow.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
            keyRow.ColumnStyles.Add(New ColumnStyle(SizeType.AutoSize))
            Dim lblKey As New Label With {.Text = "&Key", .AutoSize = True, .Anchor = AnchorStyles.Left, .Margin = New Padding(0, 4, 10, 4)}
            txtKey.Dock = DockStyle.Fill
            txtKey.UseSystemPasswordChar = True
            txtKey.AccessibleName = "OpenAlex key"
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
            root.Controls.Add(lnkPage, 0, 1)
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
                UrlSafetyService.OpenInBrowser(KeyPage)
            Catch ex As Exception
                MessageBox.Show(Me, ex.Message, "Open OpenAlex", MessageBoxButtons.OK, MessageBoxIcon.Information)
            End Try
        End Sub


        Private Sub RefreshState()
            Dim value As String = txtKey.Text.Trim()
            btnOk.Enabled = ProtectedKeyStore.IsPlausibleKey(value)
            lblHint.Text = HintFor(value)
        End Sub


        ' Why a pasted value isn't accepted; nothing while a short key is
        ' still being typed.
        Friend Shared Function HintFor(value As String) As String
            If String.IsNullOrEmpty(value) OrElse ProtectedKeyStore.IsPlausibleKey(value) Then Return String.Empty
            If value.Any(Function(character) character <= " "c OrElse character > "~"c) Then Return "A key is one word, without spaces, line breaks, or accented letters."
            If value.Length > 200 Then Return "That's longer than an OpenAlex key. Copy just the key."
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
