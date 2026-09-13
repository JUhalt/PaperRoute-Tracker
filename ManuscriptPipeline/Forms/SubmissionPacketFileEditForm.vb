Imports System
Imports System.Drawing
Imports System.IO
Imports System.Windows.Forms
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

Namespace Forms

    Public Class SubmissionPacketFileEditForm
        Inherits Form

        Private ReadOnly _packet As SubmissionPacket
        Private ReadOnly _existingFile As SubmissionPacketFile

        Private ReadOnly cmbRole As New ComboBox()
        Private ReadOnly cmbStorage As New ComboBox()
        Private ReadOnly txtLabel As New TextBox()
        Private ReadOnly txtPath As New TextBox()
        Private ReadOnly btnBrowse As New Button()
        Private ReadOnly txtNotes As New TextBox()


        Public Sub New(
            packet As SubmissionPacket,
            existingFile As SubmissionPacketFile
        )

            If packet Is Nothing Then
                Throw New ArgumentNullException(
                    NameOf(packet)
                )
            End If

            _packet =
                packet

            _existingFile =
                existingFile

            BuildInterface()
            UiPolish.ApplyDialog(Me)
            PopulateOptions()
            LoadExistingFile()
            UpdateStorageUi()

        End Sub


        Private Sub BuildInterface()

            Me.Text =
                If(
                    _existingFile Is Nothing,
                    "Add Packet File",
                    "Edit Packet File"
                )

            Me.StartPosition =
                FormStartPosition.CenterParent

            Me.Size =
                New Size(
                    740,
                    610
                )

            Me.MinimumSize =
                New Size(
                    640,
                    520
                )

            Me.Font =
                New Font(
                    "Segoe UI",
                    10.0F
                )

            Me.AutoScaleMode =
                AutoScaleMode.Dpi

            Dim root As New TableLayoutPanel With {
                .Dock = DockStyle.Fill,
                .ColumnCount = 2,
                .RowCount = 7,
                .Padding = New Padding(20)
            }

            root.ColumnStyles.Add(
                New ColumnStyle(
                    SizeType.Absolute,
                    180
                )
            )

            root.ColumnStyles.Add(
                New ColumnStyle(
                    SizeType.Percent,
                    100
                )
            )

            root.RowStyles.Add(New RowStyle(SizeType.AutoSize))
            root.RowStyles.Add(New RowStyle(SizeType.AutoSize))
            root.RowStyles.Add(New RowStyle(SizeType.AutoSize))
            root.RowStyles.Add(New RowStyle(SizeType.AutoSize))
            root.RowStyles.Add(New RowStyle(SizeType.AutoSize))
            root.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
            root.RowStyles.Add(New RowStyle(SizeType.AutoSize))

            Dim intro As New Label With {
                .AutoSize = True,
                .MaximumSize = New Size(660, 0),
                .Text =
                    "Choose whether PaperRoute should keep only metadata, link to an external file, or copy a snapshot into the PaperRoute Library. External linked files are never deleted by PaperRoute.",
                .Margin = New Padding(0, 0, 0, 12)
            }

            root.Controls.Add(
                intro,
                0,
                0
            )

            root.SetColumnSpan(
                intro,
                2
            )

            cmbRole.Dock =
                DockStyle.Fill

            cmbRole.DropDownStyle =
                ComboBoxStyle.DropDownList

            root.Controls.Add(
                CreateFieldLabel(
                    "File role"
                ),
                0,
                1
            )

            root.Controls.Add(
                cmbRole,
                1,
                1
            )

            cmbStorage.Dock =
                DockStyle.Fill

            cmbStorage.DropDownStyle =
                ComboBoxStyle.DropDownList

            AddHandler cmbStorage.SelectedIndexChanged,
                AddressOf StorageSelectionChanged

            root.Controls.Add(
                CreateFieldLabel(
                    "Storage"
                ),
                0,
                2
            )

            root.Controls.Add(
                cmbStorage,
                1,
                2
            )

            txtLabel.Dock =
                DockStyle.Fill

            txtLabel.PlaceholderText =
                "Optional display label"

            root.Controls.Add(
                CreateFieldLabel(
                    "Label"
                ),
                0,
                3
            )

            root.Controls.Add(
                txtLabel,
                1,
                3
            )

            Dim pathPanel As New TableLayoutPanel With {
                .Dock = DockStyle.Fill,
                .ColumnCount = 2,
                .RowCount = 1
            }

            pathPanel.ColumnStyles.Add(
                New ColumnStyle(
                    SizeType.Percent,
                    100
                )
            )

            pathPanel.ColumnStyles.Add(
                New ColumnStyle(
                    SizeType.AutoSize
                )
            )

            txtPath.Dock =
                DockStyle.Fill

            txtPath.ReadOnly =
                True

            btnBrowse.Text =
                "Browse..."

            btnBrowse.AutoSize =
                True

            btnBrowse.Height =
                34

            AddHandler btnBrowse.Click,
                AddressOf BrowseForFile

            pathPanel.Controls.Add(
                txtPath,
                0,
                0
            )

            pathPanel.Controls.Add(
                btnBrowse,
                1,
                0
            )

            root.Controls.Add(
                CreateFieldLabel(
                    "Local file"
                ),
                0,
                4
            )

            root.Controls.Add(
                pathPanel,
                1,
                4
            )

            txtNotes.Dock =
                DockStyle.Fill

            txtNotes.Multiline =
                True

            txtNotes.ScrollBars =
                ScrollBars.Vertical

            txtNotes.PlaceholderText =
                "Optional notes"

            root.Controls.Add(
                CreateFieldLabel(
                    "Notes"
                ),
                0,
                5
            )

            root.Controls.Add(
                txtNotes,
                1,
                5
            )

            Dim footer As New FlowLayoutPanel With {
                .Dock = DockStyle.Fill,
                .AutoSize = True,
                .FlowDirection = FlowDirection.RightToLeft,
                .WrapContents = False,
                .Padding = New Padding(0, 12, 0, 0)
            }

            Dim btnSave As New Button With {
                .Text =
                    If(
                        _existingFile Is Nothing,
                        "Add File",
                        "Save Changes"
                    ),
                .AutoSize = True,
                .Height = 38
            }

            Dim btnCancel As New Button With {
                .Text = "Cancel",
                .AutoSize = True,
                .Height = 38,
                .DialogResult = DialogResult.Cancel
            }

            AddHandler btnSave.Click,
                AddressOf SaveFile

            footer.Controls.Add(
                btnSave
            )

            footer.Controls.Add(
                btnCancel
            )

            root.Controls.Add(
                footer,
                0,
                6
            )

            root.SetColumnSpan(
                footer,
                2
            )

            Me.AcceptButton =
                btnSave

            Me.CancelButton =
                btnCancel

            Me.Controls.Add(
                root
            )

        End Sub


        Private Function CreateFieldLabel(
            text As String
        ) As Label

            Return New Label With {
                .Text = text,
                .AutoSize = True,
                .Anchor = AnchorStyles.Left,
                .Font = New Font(
                    Me.Font,
                    FontStyle.Bold
                ),
                .Margin = New Padding(
                    3,
                    9,
                    3,
                    9
                )
            }

        End Function


        Private Sub PopulateOptions()

            cmbRole.Items.Clear()

            For Each value As SubmissionPacketFileRole In
                [Enum].GetValues(
                    GetType(
                        SubmissionPacketFileRole
                    )
                )

                cmbRole.Items.Add(
                    New RoleOption(
                        value
                    )
                )

            Next

            cmbStorage.Items.Clear()

            cmbStorage.Items.Add(
                New StorageOption(
                    SubmissionPacketFileStorageMode.MetadataOnly,
                    "Metadata only"
                )
            )

            cmbStorage.Items.Add(
                New StorageOption(
                    SubmissionPacketFileStorageMode.LinkedExternal,
                    "Link external file"
                )
            )

            cmbStorage.Items.Add(
                New StorageOption(
                    SubmissionPacketFileStorageMode.ManagedCopy,
                    "Copy to PaperRoute Library"
                )
            )

            cmbRole.SelectedIndex =
                0

            cmbStorage.SelectedIndex =
                0

        End Sub


        Private Sub LoadExistingFile()

            If _existingFile Is Nothing Then
                Return
            End If

            SelectRole(
                _existingFile.Role
            )

            SelectStorage(
                _existingFile.StorageMode
            )

            txtLabel.Text =
                _existingFile.Label

            txtPath.Text =
                _existingFile.LocalFilePath

            txtNotes.Text =
                _existingFile.Notes

            ' Existing packet-file storage identity is immutable in-place.
            ' Create a new entry if the user wants a different storage mode
            ' or a different source file.
            cmbStorage.Enabled =
                False

            btnBrowse.Enabled =
                False

        End Sub


        Private Sub StorageSelectionChanged(
            sender As Object,
            e As EventArgs
        )

            UpdateStorageUi()

        End Sub


        Private Sub UpdateStorageUi()

            If _existingFile IsNot Nothing Then

                btnBrowse.Enabled =
                    False

                Return

            End If

            Dim storage As SubmissionPacketFileStorageMode =
                SelectedStorage()

            btnBrowse.Enabled =
                storage <>
                SubmissionPacketFileStorageMode.MetadataOnly

            If storage =
               SubmissionPacketFileStorageMode.MetadataOnly Then

                txtPath.Text =
                    String.Empty

            End If

        End Sub


        Private Sub BrowseForFile(
            sender As Object,
            e As EventArgs
        )

            Using picker As New OpenFileDialog With {
                .Title = "Choose Submission Packet file",
                .Filter = "All files (*.*)|*.*",
                .CheckFileExists = True,
                .Multiselect = False
            }

                If picker.ShowDialog(Me) <>
                   DialogResult.OK Then

                    Return

                End If

                txtPath.Text =
                    picker.FileName

                If String.IsNullOrWhiteSpace(
                    txtLabel.Text
                ) Then

                    txtLabel.Text =
                        Path.GetFileName(
                            picker.FileName
                        )

                End If

            End Using

        End Sub


        Private Sub SaveFile(
            sender As Object,
            e As EventArgs
        )

            Dim role As SubmissionPacketFileRole =
                SelectedRole()

            If _existingFile Is Nothing Then

                Try

                    SubmissionPacketService.AddFile(
                        _packet,
                        role,
                        txtLabel.Text,
                        txtNotes.Text,
                        txtPath.Text,
                        SelectedStorage()
                    )

                Catch ex As ArgumentException

                    MessageBox.Show(
                        Me,
                        ex.Message,
                        "Check Packet File",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information
                    )

                    Return

                Catch ex As FileNotFoundException

                    MessageBox.Show(
                        Me,
                        ex.Message,
                        "Check Packet File",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information
                    )

                    Return

                    MessageBox.Show(
                        Me,
                        ex.Message,
                        "Check Packet File",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information
                    )

                    Return

                End Try

            Else

                _existingFile.Role =
                    role

                _existingFile.Label =
                    If(
                        String.IsNullOrWhiteSpace(
                            txtLabel.Text
                        ),
                        SubmissionPacketService.FriendlyRoleName(
                            role
                        ),
                        txtLabel.Text.Trim()
                    )

                _existingFile.Notes =
                    txtNotes.Text.Trim()

                _packet.LastModifiedAtUtc =
                    DateTime.UtcNow

            End If

            Me.DialogResult =
                DialogResult.OK

        End Sub


        Private Function SelectedRole() As SubmissionPacketFileRole

            Dim optionItem As RoleOption =
                TryCast(
                    cmbRole.SelectedItem,
                    RoleOption
                )

            If optionItem Is Nothing Then
                Return SubmissionPacketFileRole.Other
            End If

            Return optionItem.Value

        End Function


        Private Function SelectedStorage() As SubmissionPacketFileStorageMode

            Dim optionItem As StorageOption =
                TryCast(
                    cmbStorage.SelectedItem,
                    StorageOption
                )

            If optionItem Is Nothing Then
                Return SubmissionPacketFileStorageMode.MetadataOnly
            End If

            Return optionItem.Value

        End Function


        Private Sub SelectRole(
            role As SubmissionPacketFileRole
        )

            For index As Integer =
                0 To cmbRole.Items.Count - 1

                Dim optionItem As RoleOption =
                    TryCast(
                        cmbRole.Items(index),
                        RoleOption
                    )

                If optionItem IsNot Nothing AndAlso
                   optionItem.Value = role Then

                    cmbRole.SelectedIndex =
                        index

                    Return

                End If

            Next

        End Sub


        Private Sub SelectStorage(
            storage As SubmissionPacketFileStorageMode
        )

            For index As Integer =
                0 To cmbStorage.Items.Count - 1

                Dim optionItem As StorageOption =
                    TryCast(
                        cmbStorage.Items(index),
                        StorageOption
                    )

                If optionItem IsNot Nothing AndAlso
                   optionItem.Value = storage Then

                    cmbStorage.SelectedIndex =
                        index

                    Return

                End If

            Next

        End Sub


        Private NotInheritable Class RoleOption

            Friend ReadOnly Property Value As SubmissionPacketFileRole
            Private ReadOnly _display As String


            Friend Sub New(
                value As SubmissionPacketFileRole
            )

                Me.Value =
                    value

                _display =
                    SubmissionPacketService.FriendlyRoleName(
                        value
                    )

            End Sub


            Public Overrides Function ToString() As String

                Return _display

            End Function

        End Class


        Private NotInheritable Class StorageOption

            Friend ReadOnly Property Value As SubmissionPacketFileStorageMode
            Private ReadOnly _display As String


            Friend Sub New(
                value As SubmissionPacketFileStorageMode,
                display As String
            )

                Me.Value =
                    value

                _display =
                    display

            End Sub


            Public Overrides Function ToString() As String

                Return _display

            End Function

        End Class

    End Class

End Namespace
