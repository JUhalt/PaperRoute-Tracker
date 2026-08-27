Imports System
Imports System.Drawing
Imports System.IO
Imports System.Windows.Forms
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

Namespace Forms

    Public Class AddCorrespondenceForm
        Inherits Form

        Private Class CorrespondenceOption

            Public ReadOnly Property Label As String
            Public ReadOnly Property Value As CorrespondenceType

            Public Sub New(label As String, value As CorrespondenceType)
                Me.Label = label
                Me.Value = value
            End Sub

            Public Overrides Function ToString() As String
                Return Label
            End Function

        End Class


        Private ReadOnly _existingItem As CorrespondenceItem

        Private ReadOnly cmbType As New ComboBox()
        Private ReadOnly dtpDate As New DateTimePicker()
        Private ReadOnly txtTitle As New TextBox()
        Private ReadOnly txtLocalFile As New TextBox()
        Private ReadOnly txtSourceUrl As New TextBox()
        Private ReadOnly txtNotes As New TextBox()

        Private _createdItem As CorrespondenceItem


        Public ReadOnly Property CreatedItem As CorrespondenceItem
            Get
                Return _createdItem
            End Get
        End Property


        Public Sub New()

            _existingItem = Nothing

            BuildInterface()

        End Sub


        Public Sub New(existingItem As CorrespondenceItem)

            _existingItem = existingItem

            BuildInterface()
            UiPolish.ApplyDialog(Me)
            LoadExistingItem()

        End Sub


        Private Sub BuildInterface()

            If _existingItem Is Nothing Then
                Me.Text = "Add Correspondence or File"
            Else
                Me.Text = "Edit Correspondence or File"
            End If

            Me.StartPosition = FormStartPosition.CenterParent
            Me.FormBorderStyle = FormBorderStyle.FixedDialog
            Me.MaximizeBox = False
            Me.MinimizeBox = False
            Me.ClientSize = New Size(700, 620)
            Me.Font = New Font("Segoe UI", 10.0F)
            Me.AutoScaleMode = AutoScaleMode.Dpi

            Dim root As New TableLayoutPanel With {
                .Dock = DockStyle.Fill,
                .ColumnCount = 2,
                .RowCount = 8,
                .Padding = New Padding(22)
            }

            root.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 170))
            root.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))

            root.RowStyles.Add(New RowStyle(SizeType.Absolute, 58))
            root.RowStyles.Add(New RowStyle(SizeType.Absolute, 58))
            root.RowStyles.Add(New RowStyle(SizeType.Absolute, 58))
            root.RowStyles.Add(New RowStyle(SizeType.Absolute, 58))
            root.RowStyles.Add(New RowStyle(SizeType.Absolute, 58))
            root.RowStyles.Add(New RowStyle(SizeType.Absolute, 40))
            root.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
            root.RowStyles.Add(New RowStyle(SizeType.Absolute, 55))

            ' =================================================
            ' Type
            ' =================================================

            cmbType.Dock = DockStyle.Fill
            cmbType.DropDownStyle = ComboBoxStyle.DropDownList

            AddTypeOption("Decision Letter", CorrespondenceType.DecisionLetter)
            AddTypeOption("Reviewer Comments", CorrespondenceType.ReviewerComments)
            AddTypeOption("Editor Email", CorrespondenceType.EditorEmail)
            AddTypeOption("Cover Letter", CorrespondenceType.CoverLetter)
            AddTypeOption("Response to Reviewers", CorrespondenceType.ResponseToReviewers)
            AddTypeOption("Revised Manuscript", CorrespondenceType.RevisedManuscript)
            AddTypeOption("Acceptance Letter", CorrespondenceType.AcceptanceLetter)
            AddTypeOption("Portal Snapshot", CorrespondenceType.PortalSnapshot)
            AddTypeOption("Other", CorrespondenceType.Other)

            cmbType.SelectedIndex = 0

            root.Controls.Add(CreateFieldLabel("Type"), 0, 0)
            root.Controls.Add(cmbType, 1, 0)

            ' =================================================
            ' Date
            ' =================================================

            dtpDate.Format = DateTimePickerFormat.Short
            dtpDate.Value = DateTime.Today
            dtpDate.Width = 180

            root.Controls.Add(CreateFieldLabel("Date"), 0, 1)
            root.Controls.Add(dtpDate, 1, 1)

            ' =================================================
            ' Title
            ' =================================================

            txtTitle.Dock = DockStyle.Fill

            root.Controls.Add(CreateFieldLabel("Title"), 0, 2)
            root.Controls.Add(txtTitle, 1, 2)

            ' =================================================
            ' Local file
            ' =================================================

            Dim filePanel As New TableLayoutPanel With {
                .Dock = DockStyle.Fill,
                .ColumnCount = 3,
                .RowCount = 1
            }

            filePanel.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
            filePanel.ColumnStyles.Add(New ColumnStyle(SizeType.AutoSize))
            filePanel.ColumnStyles.Add(New ColumnStyle(SizeType.AutoSize))

            txtLocalFile.Dock = DockStyle.Fill
            txtLocalFile.ReadOnly = True

            Dim btnBrowse As New Button With {
                .Text = "Browse...",
                .AutoSize = True,
                .Height = 32
            }

            Dim btnClearFile As New Button With {
                .Text = "Clear",
                .AutoSize = True,
                .Height = 32
            }

            AddHandler btnBrowse.Click, AddressOf BrowseForFile
            AddHandler btnClearFile.Click, AddressOf ClearFile

            filePanel.Controls.Add(txtLocalFile, 0, 0)
            filePanel.Controls.Add(btnBrowse, 1, 0)
            filePanel.Controls.Add(btnClearFile, 2, 0)

            root.Controls.Add(CreateFieldLabel("Local file"), 0, 3)
            root.Controls.Add(filePanel, 1, 3)

            ' =================================================
            ' Source URL
            ' =================================================

            txtSourceUrl.Dock = DockStyle.Fill

            root.Controls.Add(CreateFieldLabel("Source URL"), 0, 4)
            root.Controls.Add(txtSourceUrl, 1, 4)

            ' =================================================
            ' Notes
            ' =================================================

            Dim lblNotes As New Label With {
                .Text = "Notes",
                .AutoSize = True,
                .Anchor = AnchorStyles.Left,
                .Font = New Font(Me.Font, FontStyle.Bold)
            }

            root.Controls.Add(lblNotes, 0, 5)
            root.SetColumnSpan(lblNotes, 2)

            txtNotes.Dock = DockStyle.Fill
            txtNotes.Multiline = True
            txtNotes.ScrollBars = ScrollBars.Vertical

            root.Controls.Add(txtNotes, 0, 6)
            root.SetColumnSpan(txtNotes, 2)

            ' =================================================
            ' Buttons
            ' =================================================

            Dim buttonPanel As New FlowLayoutPanel With {
                .Dock = DockStyle.Fill,
                .FlowDirection = FlowDirection.RightToLeft,
                .WrapContents = False
            }

            Dim btnSave As New Button With {
                .AutoSize = True,
                .Height = 36
            }

            If _existingItem Is Nothing Then
                btnSave.Text = "Add Item"
            Else
                btnSave.Text = "Save Changes"
            End If

            Dim btnCancel As New Button With {
                .Text = "Cancel",
                .AutoSize = True,
                .Height = 36,
                .DialogResult = DialogResult.Cancel
            }

            AddHandler btnSave.Click, AddressOf SaveItem

            buttonPanel.Controls.Add(btnSave)
            buttonPanel.Controls.Add(btnCancel)

            root.Controls.Add(buttonPanel, 0, 7)
            root.SetColumnSpan(buttonPanel, 2)

            Me.AcceptButton = btnSave
            Me.CancelButton = btnCancel

            Me.Controls.Add(root)

        End Sub


        Private Sub AddTypeOption(label As String, value As CorrespondenceType)

            cmbType.Items.Add(New CorrespondenceOption(label, value))

        End Sub


        Private Function CreateFieldLabel(text As String) As Label

            Return New Label With {
                .Text = text,
                .AutoSize = True,
                .Anchor = AnchorStyles.Left,
                .Font = New Font(Me.Font, FontStyle.Bold)
            }

        End Function


        Private Sub LoadExistingItem()

            For i As Integer = 0 To cmbType.Items.Count - 1

                Dim optionItem As CorrespondenceOption = DirectCast(cmbType.Items(i), CorrespondenceOption)

                If optionItem.Value = _existingItem.Type Then
                    cmbType.SelectedIndex = i
                    Exit For
                End If

            Next

            dtpDate.Value = _existingItem.ItemDate
            txtTitle.Text = _existingItem.Title
            txtLocalFile.Text = _existingItem.LocalFilePath
            txtSourceUrl.Text = _existingItem.SourceUrl
            txtNotes.Text = _existingItem.Notes

        End Sub


        Private Sub BrowseForFile(sender As Object, e As EventArgs)

            Using dialog As New OpenFileDialog()

                dialog.Title = "Select correspondence or manuscript file"
                dialog.Filter = "Common documents|*.pdf;*.doc;*.docx;*.rtf;*.txt;*.html;*.htm;*.eml;*.msg|All files|*.*"
                dialog.CheckFileExists = True
                dialog.Multiselect = False

                If dialog.ShowDialog(Me) <> DialogResult.OK Then
                    Return
                End If

                txtLocalFile.Text = dialog.FileName

                If String.IsNullOrWhiteSpace(txtTitle.Text) Then
                    txtTitle.Text = Path.GetFileName(dialog.FileName)
                End If

            End Using

        End Sub


        Private Sub ClearFile(sender As Object, e As EventArgs)

            txtLocalFile.Text = String.Empty

        End Sub


        Private Sub SaveItem(sender As Object, e As EventArgs)

            If cmbType.SelectedItem Is Nothing Then
                Return
            End If

            Dim localPath As String = txtLocalFile.Text.Trim()

            If Not String.IsNullOrWhiteSpace(localPath) Then

                If Not File.Exists(localPath) Then

                    MessageBox.Show(
                        Me,
                        "The selected local file could not be found. Choose another file or clear the file field.",
                        "File Not Found",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information
                    )

                    Return

                End If

            End If

            Dim sourceUrl As String = txtSourceUrl.Text.Trim()

            If Not String.IsNullOrWhiteSpace(sourceUrl) Then

                Dim sourceUri As Uri = Nothing
                Dim validUri As Boolean = Uri.TryCreate(sourceUrl, UriKind.Absolute, sourceUri)

                If Not validUri Then
                    ShowInvalidUrlMessage()
                    Return
                End If

                If sourceUri Is Nothing Then
                    ShowInvalidUrlMessage()
                    Return
                End If

                If sourceUri.Scheme <> Uri.UriSchemeHttp AndAlso sourceUri.Scheme <> Uri.UriSchemeHttps Then
                    ShowInvalidUrlMessage()
                    Return
                End If

            End If

            Dim itemTitle As String = txtTitle.Text.Trim()

            If String.IsNullOrWhiteSpace(itemTitle) AndAlso Not String.IsNullOrWhiteSpace(localPath) Then
                itemTitle = Path.GetFileName(localPath)
            End If

            If String.IsNullOrWhiteSpace(itemTitle) Then

                MessageBox.Show(
                    Me,
                    "Please enter a title for this item.",
                    "Title Required",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                )

                txtTitle.Focus()
                Return

            End If

            Dim selectedOption As CorrespondenceOption = DirectCast(cmbType.SelectedItem, CorrespondenceOption)

            Dim itemId As Guid
            Dim managedCopy As Boolean

            If _existingItem Is Nothing Then

                itemId = Guid.NewGuid()
                managedCopy = False

            Else

                itemId = _existingItem.Id
                managedCopy = _existingItem.IsManagedCopy

            End If

            _createdItem =
                New CorrespondenceItem With {
                    .Id = itemId,
                    .RecordedAtUtc =
                        If(
                            _existingItem Is Nothing,
                            Nothing,
                            _existingItem.RecordedAtUtc
                        ),
                    .LastModifiedAtUtc =
                        If(
                            _existingItem Is Nothing,
                            Nothing,
                            _existingItem.LastModifiedAtUtc
                        ),
                    .ItemDate = dtpDate.Value.Date,
                    .Type = selectedOption.Value,
                    .Title = itemTitle,
                    .Notes = txtNotes.Text.Trim(),
                    .LocalFilePath = localPath,
                    .SourceUrl = sourceUrl,
                    .IsManagedCopy = managedCopy
                }

            If _existingItem Is Nothing Then

                ChronologyProvenanceService.StampCreated(
                    _createdItem
                )

            Else

                ChronologyProvenanceService.StampModified(
                    _createdItem
                )

            End If

            Me.DialogResult = DialogResult.OK

        End Sub


        Private Sub ShowInvalidUrlMessage()

            MessageBox.Show(
                Me,
                "The source URL must be a valid http:// or https:// address. You may also leave this field blank.",
                "Invalid Source URL",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information
            )

            txtSourceUrl.Focus()

        End Sub

    End Class

End Namespace