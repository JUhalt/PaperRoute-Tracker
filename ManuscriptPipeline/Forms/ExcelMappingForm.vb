Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.IO
Imports System.Windows.Forms
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

Namespace Forms

    Public Class ExcelMappingForm
        Inherits Form

        Private ReadOnly _filePath As String
        Private ReadOnly _importer As New FlexibleExcelImporter()

        Private ReadOnly cboWorksheet As New ComboBox()
        Private ReadOnly numHeaderRow As New NumericUpDown()
        Private ReadOnly gridMappings As New DataGridView()
        Private ReadOnly lblSummary As New Label()
        Private ReadOnly btnImport As New Button()
        Private ReadOnly btnAutoMap As New Button()
        Private ReadOnly btnClear As New Button()

        Private _loadingGrid As Boolean = False


        Public Sub New(
            filePath As String
        )

            If String.IsNullOrWhiteSpace(filePath) Then
                Throw New ArgumentException("An Excel file path is required.")
            End If

            _filePath = filePath

            BuildInterface()
            LoadWorkbook()

            UiPolish.ApplyDialog(Me)
            StyleGrid()

        End Sub


        Public ReadOnly Property SelectedWorksheetName As String
            Get

                If cboWorksheet.SelectedItem Is Nothing Then
                    Return String.Empty
                End If

                Return cboWorksheet.SelectedItem.ToString()

            End Get
        End Property


        Public ReadOnly Property HeaderRow As Integer
            Get
                Return CInt(numHeaderRow.Value)
            End Get
        End Property


        Public ReadOnly Property Mappings As List(Of ExcelColumnMapping)
            Get
                Return BuildMappingsFromGrid()
            End Get
        End Property


        Private Sub BuildInterface()

            Me.Text = "Map Spreadsheet Columns"
            Me.StartPosition = FormStartPosition.CenterParent
            Me.Size = New Size(980, 700)
            Me.MinimumSize = New Size(800, 560)
            Me.Font = New Font("Segoe UI", 10.0F)
            Me.AutoScaleMode = AutoScaleMode.Dpi
            Me.BackColor = UiTheme.BoardBackground()

            Dim root As New TableLayoutPanel With {
                .Dock = DockStyle.Fill,
                .ColumnCount = 1,
                .RowCount = 5,
                .Padding = New Padding(22),
                .BackColor = UiTheme.BoardBackground()
            }

            root.RowStyles.Add(New RowStyle(SizeType.Absolute, 78))
            root.RowStyles.Add(New RowStyle(SizeType.Absolute, 56))
            root.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
            root.RowStyles.Add(New RowStyle(SizeType.Absolute, 44))
            root.RowStyles.Add(New RowStyle(SizeType.Absolute, 54))

            Dim intro As New TableLayoutPanel With {
                .Dock = DockStyle.Fill,
                .ColumnCount = 1,
                .RowCount = 2,
                .BackColor = UiTheme.BoardBackground()
            }

            intro.RowStyles.Add(New RowStyle(SizeType.AutoSize))
            intro.RowStyles.Add(New RowStyle(SizeType.AutoSize))

            Dim lblTitle As New Label With {
                .Text = "Map your spreadsheet",
                .AutoSize = True,
                .Font = New Font(Me.Font.FontFamily, 18.0F, FontStyle.Bold),
                .ForeColor = UiTheme.PrimaryText()
            }

            Dim lblHelp As New Label With {
                .Text = "PaperRoute could not identify this workbook as the standard template or legacy tracker. Match your columns below; Title is the only required field.",
                .AutoSize = True,
                .MaximumSize = New Size(880, 0),
                .ForeColor = UiTheme.SecondaryText(),
                .Margin = New Padding(0, 4, 0, 0)
            }

            intro.Controls.Add(lblTitle, 0, 0)
            intro.Controls.Add(lblHelp, 0, 1)

            Dim options As New FlowLayoutPanel With {
                .Dock = DockStyle.Fill,
                .FlowDirection = FlowDirection.LeftToRight,
                .WrapContents = False,
                .Padding = New Padding(0, 8, 0, 0),
                .BackColor = UiTheme.BoardBackground()
            }

            Dim lblSheet As New Label With {
                .Text = "Worksheet",
                .AutoSize = True,
                .Margin = New Padding(0, 7, 8, 0)
            }

            cboWorksheet.DropDownStyle = ComboBoxStyle.DropDownList
            cboWorksheet.Width = 220
            cboWorksheet.Margin = New Padding(0, 2, 18, 0)

            Dim lblHeaderRow As New Label With {
                .Text = "Header row",
                .AutoSize = True,
                .Margin = New Padding(0, 7, 8, 0)
            }

            numHeaderRow.Minimum = 1
            numHeaderRow.Maximum = 100
            numHeaderRow.Value = 1
            numHeaderRow.Width = 64
            numHeaderRow.Margin = New Padding(0, 2, 18, 0)

            btnAutoMap.Text = "Auto-map"
            btnAutoMap.Width = 100
            btnAutoMap.Height = 32
            btnAutoMap.Margin = New Padding(0, 1, 8, 0)

            btnClear.Text = "Clear mappings"
            btnClear.Width = 125
            btnClear.Height = 32
            btnClear.Margin = New Padding(0, 1, 0, 0)

            options.Controls.Add(lblSheet)
            options.Controls.Add(cboWorksheet)
            options.Controls.Add(lblHeaderRow)
            options.Controls.Add(numHeaderRow)
            options.Controls.Add(btnAutoMap)
            options.Controls.Add(btnClear)

            ConfigureGrid()

            lblSummary.Dock = DockStyle.Fill
            lblSummary.TextAlign = ContentAlignment.MiddleLeft
            lblSummary.ForeColor = UiTheme.SecondaryText()

            Dim footer As New FlowLayoutPanel With {
                .Dock = DockStyle.Fill,
                .FlowDirection = FlowDirection.RightToLeft,
                .WrapContents = False,
                .BackColor = UiTheme.BoardBackground()
            }

            btnImport.Text = "Import Mapped Data"
            btnImport.Width = 165
            btnImport.Height = 38

            Dim btnCancel As New Button With {
                .Text = "Cancel",
                .Width = 95,
                .Height = 38,
                .DialogResult = DialogResult.Cancel
            }

            footer.Controls.Add(btnImport)
            footer.Controls.Add(btnCancel)

            root.Controls.Add(intro, 0, 0)
            root.Controls.Add(options, 0, 1)
            root.Controls.Add(gridMappings, 0, 2)
            root.Controls.Add(lblSummary, 0, 3)
            root.Controls.Add(footer, 0, 4)

            Me.Controls.Add(root)
            Me.CancelButton = btnCancel

            AddHandler cboWorksheet.SelectedIndexChanged, AddressOf WorkbookOptionChanged
            AddHandler numHeaderRow.ValueChanged, AddressOf WorkbookOptionChanged
            AddHandler btnAutoMap.Click, AddressOf AutoMapColumns
            AddHandler btnClear.Click, AddressOf ClearMappings
            AddHandler btnImport.Click, AddressOf ConfirmImport
            AddHandler gridMappings.CellValueChanged, AddressOf MappingValueChanged
            AddHandler gridMappings.CurrentCellDirtyStateChanged, AddressOf CommitComboEdit

        End Sub


        Private Sub ConfigureGrid()

            gridMappings.Dock = DockStyle.Fill
            ' No label names the grid (#137).
            gridMappings.AccessibleName = "Column mappings"
            gridMappings.AllowUserToAddRows = False
            gridMappings.AllowUserToDeleteRows = False
            gridMappings.AllowUserToResizeRows = False
            gridMappings.AutoGenerateColumns = False
            gridMappings.MultiSelect = False
            gridMappings.RowHeadersVisible = False
            gridMappings.SelectionMode = DataGridViewSelectionMode.FullRowSelect
            gridMappings.BorderStyle = BorderStyle.None
            gridMappings.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells

            Dim sourceColumn As New DataGridViewTextBoxColumn With {
                .Name = "SourceColumn",
                .HeaderText = "Spreadsheet column",
                .ReadOnly = True,
                .Width = 220,
                .SortMode = DataGridViewColumnSortMode.NotSortable
            }

            Dim sampleColumn As New DataGridViewTextBoxColumn With {
                .Name = "Samples",
                .HeaderText = "Example values",
                .ReadOnly = True,
                .AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                .MinimumWidth = 260,
                .SortMode = DataGridViewColumnSortMode.NotSortable
            }

            Dim mappingColumn As New DataGridViewComboBoxColumn With {
                .Name = "Mapping",
                .HeaderText = "Map to PaperRoute",
                .Width = 205,
                .FlatStyle = FlatStyle.Flat,
                .SortMode = DataGridViewColumnSortMode.NotSortable
            }

            For Each displayName As String In _importer.GetFieldDisplayNames()
                mappingColumn.Items.Add(displayName)
            Next

            gridMappings.Columns.Add(sourceColumn)
            gridMappings.Columns.Add(sampleColumn)
            gridMappings.Columns.Add(mappingColumn)

        End Sub


        Private Sub StyleGrid()

            gridMappings.EnableHeadersVisualStyles = False
            gridMappings.BackgroundColor = UiTheme.CardBackground()
            gridMappings.GridColor = UiTheme.CardBorder()
            gridMappings.DefaultCellStyle.BackColor = UiTheme.CardBackground()
            gridMappings.DefaultCellStyle.ForeColor = UiTheme.PrimaryText()
            gridMappings.DefaultCellStyle.SelectionBackColor = UiTheme.HoverBackground()
            gridMappings.DefaultCellStyle.SelectionForeColor = UiTheme.PrimaryText()
            gridMappings.DefaultCellStyle.Padding = New Padding(6, 4, 6, 4)
            gridMappings.ColumnHeadersDefaultCellStyle.BackColor = UiTheme.HeaderBackground()
            gridMappings.ColumnHeadersDefaultCellStyle.ForeColor = UiTheme.PrimaryText()
            gridMappings.ColumnHeadersDefaultCellStyle.Font = New Font(Me.Font, FontStyle.Bold)
            gridMappings.ColumnHeadersHeight = 38
            gridMappings.RowTemplate.Height = 36

        End Sub


        Private Sub LoadWorkbook()

            Dim worksheetNames As List(Of String) =
                _importer.GetWorksheetNames(_filePath)

            cboWorksheet.Items.Clear()

            For Each worksheetName As String In worksheetNames
                cboWorksheet.Items.Add(worksheetName)
            Next

            If cboWorksheet.Items.Count = 0 Then
                Throw New InvalidDataException("The selected workbook contains no worksheets.")
            End If

            cboWorksheet.SelectedIndex = 0

        End Sub


        Private Sub WorkbookOptionChanged(
            sender As Object,
            e As EventArgs
        )

            If cboWorksheet.SelectedItem Is Nothing Then
                Return
            End If

            LoadWorksheetPreview(True)

        End Sub


        Private Sub LoadWorksheetPreview(
            applySuggestions As Boolean
        )

            _loadingGrid = True

            Try

                Dim columns As List(Of ExcelColumnPreview) =
                    _importer.ReadWorksheetColumns(
                        _filePath,
                        SelectedWorksheetName,
                        HeaderRow
                    )

                gridMappings.Rows.Clear()

                For Each preview As ExcelColumnPreview In columns

                    Dim rowIndex As Integer =
                        gridMappings.Rows.Add()

                    Dim row As DataGridViewRow =
                        gridMappings.Rows(rowIndex)

                    row.Tag = preview
                    row.Cells("SourceColumn").Value = preview.HeaderName
                    row.Cells("Samples").Value = preview.SampleText

                    Dim field As ExcelImportField = ExcelImportField.Ignore

                    If applySuggestions Then
                        field = _importer.SuggestField(preview.HeaderName)
                    End If

                    row.Cells("Mapping").Value =
                        _importer.GetFieldDisplayName(field)

                Next

            Finally

                _loadingGrid = False

            End Try

            UpdateSummary()

        End Sub


        Private Sub AutoMapColumns(
            sender As Object,
            e As EventArgs
        )

            _loadingGrid = True

            Try

                For Each row As DataGridViewRow In gridMappings.Rows

                    Dim preview As ExcelColumnPreview =
                        TryCast(row.Tag, ExcelColumnPreview)

                    If preview Is Nothing Then
                        Continue For
                    End If

                    row.Cells("Mapping").Value =
                        _importer.GetFieldDisplayName(
                            _importer.SuggestField(preview.HeaderName)
                        )

                Next

            Finally

                _loadingGrid = False

            End Try

            UpdateSummary()

        End Sub


        Private Sub ClearMappings(
            sender As Object,
            e As EventArgs
        )

            _loadingGrid = True

            Try

                For Each row As DataGridViewRow In gridMappings.Rows
                    row.Cells("Mapping").Value = "Ignore this column"
                Next

            Finally

                _loadingGrid = False

            End Try

            UpdateSummary()

        End Sub


        Private Sub ConfirmImport(
            sender As Object,
            e As EventArgs
        )

            Dim mappings As List(Of ExcelColumnMapping) =
                BuildMappingsFromGrid()

            Dim usedFields As New HashSet(Of ExcelImportField)()
            Dim titleCount As Integer = 0

            For Each mapping As ExcelColumnMapping In mappings

                If mapping.Field = ExcelImportField.Ignore Then
                    Continue For
                End If

                If mapping.Field = ExcelImportField.Title Then
                    titleCount += 1
                End If

                If usedFields.Contains(mapping.Field) Then

                    MessageBox.Show(
                        Me,
                        "Only one spreadsheet column can be mapped to '" &
                        _importer.GetFieldDisplayName(mapping.Field) &
                        "'.",
                        "Duplicate Mapping",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning
                    )

                    Return

                End If

                usedFields.Add(mapping.Field)

            Next

            If titleCount <> 1 Then

                MessageBox.Show(
                    Me,
                    "Map exactly one spreadsheet column to Title before importing.",
                    "Title Required",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning
                )

                Return

            End If

            Me.DialogResult = DialogResult.OK

        End Sub


        Private Function BuildMappingsFromGrid() As List(Of ExcelColumnMapping)

            Dim result As New List(Of ExcelColumnMapping)()

            For Each row As DataGridViewRow In gridMappings.Rows

                Dim preview As ExcelColumnPreview =
                    TryCast(row.Tag, ExcelColumnPreview)

                If preview Is Nothing Then
                    Continue For
                End If

                Dim displayName As String = String.Empty

                If row.Cells("Mapping").Value IsNot Nothing Then
                    displayName = row.Cells("Mapping").Value.ToString()
                End If

                result.Add(
                    New ExcelColumnMapping With {
                        .ColumnNumber = preview.ColumnNumber,
                        .HeaderName = preview.HeaderName,
                        .Field = _importer.FieldFromDisplayName(displayName)
                    }
                )

            Next

            Return result

        End Function


        Private Sub MappingValueChanged(
            sender As Object,
            e As DataGridViewCellEventArgs
        )

            If _loadingGrid Then
                Return
            End If

            If e.RowIndex < 0 Then
                Return
            End If

            UpdateSummary()

        End Sub


        Private Sub CommitComboEdit(
            sender As Object,
            e As EventArgs
        )

            If gridMappings.IsCurrentCellDirty Then
                gridMappings.CommitEdit(DataGridViewDataErrorContexts.Commit)
            End If

        End Sub


        Private Sub UpdateSummary()

            Dim mappedCount As Integer = 0
            Dim titleMapped As Boolean = False

            For Each mapping As ExcelColumnMapping In BuildMappingsFromGrid()

                If mapping.Field = ExcelImportField.Ignore Then
                    Continue For
                End If

                mappedCount += 1

                If mapping.Field = ExcelImportField.Title Then
                    titleMapped = True
                End If

            Next

            Dim fileName As String =
                Path.GetFileName(_filePath)

            lblSummary.Text =
                fileName & "  •  " &
                mappedCount.ToString() & " column(s) mapped" &
                If(titleMapped, "  •  Ready to import", "  •  Title still needs to be mapped")

            btnImport.Enabled = titleMapped

        End Sub

    End Class

End Namespace
