Imports System
Imports System.Collections.Generic
Imports System.ComponentModel
Imports System.Drawing
Imports System.Globalization
Imports System.Linq
Imports System.Threading
Imports System.Threading.Tasks
Imports System.Windows.Forms
Imports ManuscriptPipeline.Controls
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

Namespace Forms

    ' Export Submission Packet (#45): a preview of a packet's files (which
    ' to include, their names in the package, their fingerprints, and the
    ' hidden information inside each), then one local .zip. Nothing is sent
    ' anywhere and nothing in the library changes: the window works on a
    ' plan prepared from copies of the records.
    Friend Class PacketExportForm
        Inherits Form

        Friend Const IntroText As String =
            "Makes a .zip of this packet's files with a summary page, a checksum list, and RO-Crate metadata that research tools can read. " &
            "Nothing is sent anywhere, and nothing in your library changes."
        Friend Const BlindedText As String =
            "This packet has an anonymized manuscript, so the package is anonymized: it names no authors, and the title page and cover letter start unchecked."
        Friend Const AcknowledgeText As String =
            "I've checked the hidden information listed above. Files are copied exactly as they are; PaperRoute doesn't remove it."
        Friend Const UncheckedTypesText As String = "Files marked Not checked may still hold hidden information."
        Friend Const NothingToNoteText As String = "Nothing to note about these files."
        Friend Const CheckingText As String = "Checking the packet's files on this computer..."
        Friend Const ExportingText As String = "Exporting..."
        Friend Const CheckFailedText As String = "PaperRoute couldn't check the packet's files. Close this window and try again."
        Friend Const ExportFailedText As String = "PaperRoute couldn't export the packet. Nothing was written."

        Private Const ColumnInclude As String = "Include"
        Private Const ColumnName As String = "Name"
        Private Const ColumnRole As String = "Role"
        Private Const ColumnLabel As String = "Label"
        Private Const ColumnSize As String = "Size"
        Private Const ColumnFingerprint As String = "Fingerprint"
        Private Const ColumnHidden As String = "Hidden"

        Private ReadOnly _plan As PacketExportPlan
        Private _loading As Task
        Private _cancellation As CancellationTokenSource
        Private _busy As Boolean
        Private _exporting As Boolean
        Private _closeWhenStopped As Boolean
        Private _loadingRows As Boolean
        ' The included files with hidden information when the state was last
        ' refreshed: a file joining them needs the acknowledgment again.
        Private _findingRows As New HashSet(Of PacketExportRow)()
        Private _formFont As Font
        Private _boldFont As Font

        Private ReadOnly lblIntro As New Label()
        Private ReadOnly lblBlinded As New Label()
        Private ReadOnly txtPackageName As New TextBox()
        Private ReadOnly gridFiles As New DataGridView()
        Private ReadOnly txtNotes As New TextBox()
        Private ReadOnly txtDetails As New TextBox()
        Private ReadOnly chkAuthors As New CheckBox()
        Private ReadOnly chkAbstract As New CheckBox()
        Private ReadOnly lblStrongWarning As New Label()
        Private ReadOnly chkAcknowledge As New CheckBox()
        Private ReadOnly lblStatus As New Label()
        Private ReadOnly btnExport As New ActionButton()
        Private ReadOnly btnCancel As New ActionButton()

        ' Tests choose the file instead of a Save dialog.
        Friend SavePathPrompt As Func(Of String) = Nothing

        <DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
        Friend Property SavedPath As String = String.Empty


        ' Reads the records once, through PacketExportService.Prepare, and
        ' keeps only the plan made from copies of them.
        Public Sub New(manuscript As Manuscript, packet As SubmissionPacket, library As AuthorLibraryData)

            _plan = PacketExportService.Prepare(manuscript, packet, library)
            BuildInterface()
            FillGrid()
            txtDetails.Text = DetailsText()
            RefreshState()
            UiPolish.ApplyDialog(Me)

        End Sub


        Protected Overrides Sub OnLoad(e As EventArgs)
            MyBase.OnLoad(e)
            ResponsiveDialogSizingService.FitToWorkingArea(Me)
        End Sub


        ' The files are checked once the window shows, off the UI thread.
        Protected Overrides Sub OnShown(e As EventArgs)
            MyBase.OnShown(e)
            If _loading Is Nothing Then _loading = CheckFilesAsync(String.Empty, Nothing)
        End Sub


        ' ---------------------------------------------------------------
        ' What tests use
        ' ---------------------------------------------------------------

        Friend ReadOnly Property Plan As PacketExportPlan
            Get
                Return _plan
            End Get
        End Property

        ' The first check of the files; complete before the window shows.
        Friend ReadOnly Property Loading As Task
            Get
                Return If(_loading, Task.CompletedTask)
            End Get
        End Property

        Friend ReadOnly Property Grid As DataGridView
            Get
                Return gridFiles
            End Get
        End Property

        Friend ReadOnly Property ExportButton As Button
            Get
                Return btnExport
            End Get
        End Property

        Friend ReadOnly Property CancelButtonForTest As Button
            Get
                Return btnCancel
            End Get
        End Property

        Friend ReadOnly Property AcknowledgeBox As CheckBox
            Get
                Return chkAcknowledge
            End Get
        End Property

        Friend ReadOnly Property AuthorsBox As CheckBox
            Get
                Return chkAuthors
            End Get
        End Property

        Friend ReadOnly Property AbstractBox As CheckBox
            Get
                Return chkAbstract
            End Get
        End Property

        Friend ReadOnly Property PackageNameBox As TextBox
            Get
                Return txtPackageName
            End Get
        End Property

        Friend ReadOnly Property NotesBox As TextBox
            Get
                Return txtNotes
            End Get
        End Property

        Friend ReadOnly Property DetailsBox As TextBox
            Get
                Return txtDetails
            End Get
        End Property

        Friend ReadOnly Property BlindedNote As Label
            Get
                Return lblBlinded
            End Get
        End Property

        Friend ReadOnly Property StrongWarning As Label
            Get
                Return lblStrongWarning
            End Get
        End Property

        Friend ReadOnly Property StatusText As String
            Get
                Return lblStatus.Text
            End Get
        End Property

        ' Each sets a cell as an edit would, through the same handlers.
        Friend Sub SetIncludeForTest(index As Integer, value As Boolean)
            gridFiles.Rows(index).Cells(ColumnInclude).Value = value
        End Sub

        Friend Sub SetNameForTest(index As Integer, value As String)
            gridFiles.Rows(index).Cells(ColumnName).Value = value
        End Sub

        Friend Sub SetLabelForTest(index As Integer, value As String)
            gridFiles.Rows(index).Cells(ColumnLabel).Value = value
        End Sub


        ' ---------------------------------------------------------------
        ' Interface
        ' ---------------------------------------------------------------

        Private Sub BuildInterface()

            SuspendLayout()

            Text = "Export Submission Packet"
            StartPosition = FormStartPosition.CenterParent
            ShowInTaskbar = False
            MinimizeBox = False
            ' Sizes below are at 96 DPI and scale with the display.
            AutoScaleDimensions = New SizeF(96.0F, 96.0F)
            ClientSize = New Size(1120, 720)
            MinimumSize = New Size(900, 640)
            _formFont = New Font("Segoe UI", 10.0F)
            Font = _formFont
            _boldFont = New Font(_formFont, FontStyle.Bold)
            AutoScaleMode = AutoScaleMode.Dpi

            Dim root As New TableLayoutPanel With {.Dock = DockStyle.Fill, .ColumnCount = 1, .RowCount = 10, .Padding = New Padding(18, 16, 18, 14)}
            root.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
            For Each style As RowStyle In {
                New RowStyle(SizeType.AutoSize),
                New RowStyle(SizeType.AutoSize),
                New RowStyle(SizeType.AutoSize),
                New RowStyle(SizeType.Percent, 55),
                New RowStyle(SizeType.Percent, 45),
                New RowStyle(SizeType.AutoSize),
                New RowStyle(SizeType.AutoSize),
                New RowStyle(SizeType.AutoSize),
                New RowStyle(SizeType.AutoSize),
                New RowStyle(SizeType.AutoSize)
            }
                root.RowStyles.Add(style)
            Next

            lblIntro.Text = IntroText
            lblIntro.AutoSize = True
            lblIntro.UseMnemonic = False
            lblIntro.Margin = New Padding(0, 0, 0, 10)

            lblBlinded.Text = BlindedText
            lblBlinded.AutoSize = True
            lblBlinded.UseMnemonic = False
            lblBlinded.ForeColor = UiTheme.InfoColor()
            lblBlinded.Margin = New Padding(0, 0, 0, 8)
            lblBlinded.Visible = _plan.IsBlinded

            ' The package's name: its summary page's heading and the default
            ' name of the .zip.
            Dim nameRow As New TableLayoutPanel With {.Dock = DockStyle.Fill, .AutoSize = True, .ColumnCount = 2, .RowCount = 1, .Margin = New Padding(0, 0, 0, 8)}
            nameRow.ColumnStyles.Add(New ColumnStyle(SizeType.AutoSize))
            nameRow.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
            nameRow.RowStyles.Add(New RowStyle(SizeType.AutoSize))
            Dim lblName As New Label With {.Text = "Package &name", .AutoSize = True, .Anchor = AnchorStyles.Left, .Margin = New Padding(0, 3, 10, 3)}
            txtPackageName.Dock = DockStyle.Fill
            txtPackageName.Margin = New Padding(0)
            txtPackageName.MaxLength = 200
            txtPackageName.AccessibleName = "Package name"
            txtPackageName.Text = _plan.PackageName
            AddHandler txtPackageName.TextChanged,
                Sub(sender, e)
                    If _exporting Then Return
                    _plan.PackageName = txtPackageName.Text
                    RefreshState()
                End Sub
            nameRow.Controls.Add(lblName, 0, 0)
            nameRow.Controls.Add(txtPackageName, 1, 0)

            ConfigureGrid()

            ' Notes about the files beside the package's details.
            Dim lower As New TableLayoutPanel With {.Dock = DockStyle.Fill, .ColumnCount = 2, .RowCount = 2, .Margin = New Padding(0)}
            lower.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 50))
            lower.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 50))
            lower.RowStyles.Add(New RowStyle(SizeType.AutoSize))
            lower.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
            Dim lblNotes As New Label With {.Text = "Notes", .AutoSize = True, .UseMnemonic = False, .Font = _boldFont, .Margin = New Padding(0, 0, 6, 4)}
            Dim lblDetails As New Label With {.Text = "Package details", .AutoSize = True, .UseMnemonic = False, .Font = _boldFont, .Margin = New Padding(6, 0, 0, 4)}
            ConfigureReadOnlyBox(txtNotes, "Notes about these files")
            txtNotes.Margin = New Padding(0, 0, 6, 0)
            ConfigureReadOnlyBox(txtDetails, "Package details")
            txtDetails.Margin = New Padding(6, 0, 0, 0)
            lower.Controls.Add(lblNotes, 0, 0)
            lower.Controls.Add(lblDetails, 1, 0)
            lower.Controls.Add(txtNotes, 0, 1)
            lower.Controls.Add(txtDetails, 1, 1)

            Dim options As New FlowLayoutPanel With {.Dock = DockStyle.Fill, .AutoSize = True, .FlowDirection = FlowDirection.LeftToRight, .WrapContents = True, .Margin = New Padding(0, 8, 0, 0)}
            chkAuthors.Text = "Include &authors (names, ORCID iDs, affiliations)"
            chkAuthors.AutoSize = True
            chkAuthors.Checked = _plan.IncludeAuthors
            chkAuthors.Margin = New Padding(0, 0, 24, 0)
            AddHandler chkAuthors.CheckedChanged, Sub(sender, e) _plan.IncludeAuthors = chkAuthors.Checked
            chkAbstract.Text = "Include the abstract and &keywords"
            chkAbstract.AutoSize = True
            chkAbstract.Checked = _plan.IncludeAbstract
            chkAbstract.Margin = New Padding(0)
            AddHandler chkAbstract.CheckedChanged, Sub(sender, e) _plan.IncludeAbstract = chkAbstract.Checked
            options.Controls.Add(chkAuthors)
            options.Controls.Add(chkAbstract)

            lblStrongWarning.AutoSize = True
            lblStrongWarning.UseMnemonic = False
            lblStrongWarning.Font = _boldFont
            lblStrongWarning.ForeColor = UiTheme.WarningColor()
            lblStrongWarning.Margin = New Padding(0, 8, 0, 0)
            lblStrongWarning.Visible = False

            ' Wraps: its height is set from its text as the window resizes.
            chkAcknowledge.Text = AcknowledgeText
            chkAcknowledge.AutoSize = False
            chkAcknowledge.CheckAlign = ContentAlignment.TopLeft
            chkAcknowledge.TextAlign = ContentAlignment.TopLeft
            chkAcknowledge.UseMnemonic = False
            chkAcknowledge.Anchor = AnchorStyles.Left Or AnchorStyles.Right Or AnchorStyles.Top
            chkAcknowledge.Margin = New Padding(0, 8, 0, 0)
            chkAcknowledge.Visible = False
            AddHandler chkAcknowledge.CheckedChanged, Sub(sender, e) RefreshState()

            lblStatus.AutoSize = True
            lblStatus.UseMnemonic = False
            lblStatus.Margin = New Padding(0, 8, 0, 0)

            Dim buttons As New FlowLayoutPanel With {.AutoSize = True, .FlowDirection = FlowDirection.RightToLeft, .WrapContents = False, .Dock = DockStyle.Fill, .Margin = New Padding(0, 10, 0, 0)}
            btnExport.Text = "&Export..."
            btnExport.Role = ActionButtonRole.Primary
            btnExport.AutoSize = True
            btnExport.MinimumSize = New Size(96, 34)
            btnExport.Margin = New Padding(0)
            btnExport.AccessibleName = "Export the package as a zip file"
            AddHandler btnExport.Click, Async Sub(sender, e) Await ExportAsync()
            btnCancel.Text = "Cancel"
            btnCancel.AutoSize = True
            btnCancel.MinimumSize = New Size(96, 34)
            btnCancel.Margin = New Padding(0, 0, 8, 0)
            btnCancel.DialogResult = DialogResult.Cancel
            buttons.Controls.Add(btnExport)
            buttons.Controls.Add(btnCancel)
            ' No default button: Enter commits an edit in the file list.
            CancelButton = btnCancel

            root.Controls.Add(lblIntro, 0, 0)
            root.Controls.Add(lblBlinded, 0, 1)
            root.Controls.Add(nameRow, 0, 2)
            root.Controls.Add(gridFiles, 0, 3)
            root.Controls.Add(lower, 0, 4)
            root.Controls.Add(options, 0, 5)
            root.Controls.Add(lblStrongWarning, 0, 6)
            root.Controls.Add(chkAcknowledge, 0, 7)
            root.Controls.Add(lblStatus, 0, 8)
            root.Controls.Add(buttons, 0, 9)
            Controls.Add(root)

            AddHandler root.SizeChanged, Sub(sender, e) FitWrappedText(root)

            ResumeLayout(False)
            PerformLayout()

        End Sub


        Private Sub ConfigureGrid()

            gridFiles.Dock = DockStyle.Fill
            gridFiles.Margin = New Padding(0, 0, 0, 10)
            gridFiles.AllowUserToAddRows = False
            gridFiles.AllowUserToDeleteRows = False
            gridFiles.AllowUserToResizeRows = False
            gridFiles.AllowUserToOrderColumns = False
            gridFiles.RowHeadersVisible = False
            gridFiles.SelectionMode = DataGridViewSelectionMode.FullRowSelect
            gridFiles.MultiSelect = False
            gridFiles.EditMode = DataGridViewEditMode.EditOnKeystrokeOrF2
            gridFiles.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None
            gridFiles.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize
            gridFiles.AccessibleName = "Files in this packet"

            gridFiles.Columns.Add(New DataGridViewCheckBoxColumn With {
                .Name = ColumnInclude, .HeaderText = "Include",
                .AutoSizeMode = DataGridViewAutoSizeColumnMode.ColumnHeader,
                .SortMode = DataGridViewColumnSortMode.NotSortable
            })
            gridFiles.Columns.Add(New DataGridViewTextBoxColumn With {
                .Name = ColumnName, .HeaderText = "Name in package", .MaxInputLength = 160,
                .AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, .FillWeight = 30, .MinimumWidth = 140,
                .SortMode = DataGridViewColumnSortMode.NotSortable
            })
            gridFiles.Columns.Add(New DataGridViewTextBoxColumn With {
                .Name = ColumnRole, .HeaderText = "Role", .ReadOnly = True,
                .AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells,
                .SortMode = DataGridViewColumnSortMode.NotSortable
            })
            ' The label becomes part of the file's description in the package.
            gridFiles.Columns.Add(New DataGridViewTextBoxColumn With {
                .Name = ColumnLabel, .HeaderText = "Label", .MaxInputLength = 200,
                .AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, .FillWeight = 22, .MinimumWidth = 100,
                .SortMode = DataGridViewColumnSortMode.NotSortable
            })
            Dim sizeColumn As New DataGridViewTextBoxColumn With {
                .Name = ColumnSize, .HeaderText = "Size", .ReadOnly = True,
                .AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells,
                .SortMode = DataGridViewColumnSortMode.NotSortable
            }
            sizeColumn.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight
            gridFiles.Columns.Add(sizeColumn)
            gridFiles.Columns.Add(New DataGridViewTextBoxColumn With {
                .Name = ColumnFingerprint, .HeaderText = "Fingerprint", .ReadOnly = True,
                .AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells,
                .SortMode = DataGridViewColumnSortMode.NotSortable
            })
            gridFiles.Columns.Add(New DataGridViewTextBoxColumn With {
                .Name = ColumnHidden, .HeaderText = "Hidden information", .ReadOnly = True,
                .AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, .FillWeight = 30, .MinimumWidth = 140,
                .SortMode = DataGridViewColumnSortMode.NotSortable
            })

            ' A check box counts as soon as it is clicked.
            AddHandler gridFiles.CurrentCellDirtyStateChanged,
                Sub(sender, e)
                    If gridFiles.IsCurrentCellDirty AndAlso TypeOf gridFiles.CurrentCell Is DataGridViewCheckBoxCell Then
                        gridFiles.CommitEdit(DataGridViewDataErrorContexts.Commit)
                    End If
                End Sub
            AddHandler gridFiles.CellValueChanged, AddressOf GridCellChanged
            ' A name being edited shows its cleaned form once the edit ends.
            AddHandler gridFiles.CellEndEdit, Sub(sender, e) RefreshGridValues()

        End Sub


        Private Shared Sub ConfigureReadOnlyBox(box As TextBox, accessibleName As String)
            box.Dock = DockStyle.Fill
            box.Multiline = True
            box.ReadOnly = True
            box.WordWrap = True
            box.ScrollBars = ScrollBars.Vertical
            box.AccessibleName = accessibleName
        End Sub


        ' Labels and the acknowledgment wrap to the window's width.
        Private Sub FitWrappedText(root As TableLayoutPanel)

            Dim width As Integer = Math.Max(LogicalToDeviceUnits(300), root.ClientSize.Width - root.Padding.Horizontal - LogicalToDeviceUnits(4))
            For Each label As Label In {lblIntro, lblBlinded, lblStrongWarning, lblStatus}
                label.MaximumSize = New Size(width, 0)
            Next

            Dim glyph As Integer = LogicalToDeviceUnits(16) + 8
            Dim textHeight As Integer = TextRenderer.MeasureText(chkAcknowledge.Text, chkAcknowledge.Font, New Size(Math.Max(1, width - glyph), 0), TextFormatFlags.WordBreak).Height
            chkAcknowledge.Width = width
            chkAcknowledge.Height = textHeight + LogicalToDeviceUnits(6)

        End Sub


        ' ---------------------------------------------------------------
        ' The file list
        ' ---------------------------------------------------------------

        Private Sub FillGrid()

            _loadingRows = True
            Try
                gridFiles.Rows.Clear()
                For Each row As PacketExportRow In _plan.Rows
                    Dim index As Integer = gridFiles.Rows.Add()
                    gridFiles.Rows(index).Tag = row
                    UpdateGridRow(gridFiles.Rows(index))
                Next
            Finally
                _loadingRows = False
            End Try

        End Sub


        Private Sub RefreshGridValues()
            For Each gridRow As DataGridViewRow In gridFiles.Rows
                UpdateGridRow(gridRow)
            Next
        End Sub


        ' Shows one file as the plan has it. A cell being edited keeps the
        ' text being typed.
        Private Sub UpdateGridRow(gridRow As DataGridViewRow)

            Dim row As PacketExportRow = TryCast(gridRow.Tag, PacketExportRow)
            If row Is Nothing Then Return

            Dim wasLoading As Boolean = _loadingRows
            _loadingRows = True
            Try
                Dim locked As Boolean = Not row.CanInclude
                SetCell(gridRow, ColumnInclude, row.Include)
                SetCell(gridRow, ColumnName, If(row.Fingerprint = PacketExportFingerprint.NoFile, "No file", row.OutputName))
                SetCell(gridRow, ColumnRole, row.RoleName)
                SetCell(gridRow, ColumnLabel, row.Label)
                SetCell(gridRow, ColumnSize, If(row.Fingerprint = PacketExportFingerprint.NoFile, "—", PacketExportService.SizeText(row.SizeBytes)))
                SetCell(gridRow, ColumnFingerprint, PacketExportService.FingerprintText(row.Fingerprint))
                Dim hidden As String = HiddenText(row)
                SetCell(gridRow, ColumnHidden, hidden)

                gridRow.Cells(ColumnHidden).ToolTipText = hidden
                gridRow.Cells(ColumnInclude).ToolTipText = row.Note
                For Each column As String In {ColumnInclude, ColumnName, ColumnLabel}
                    If gridRow.Cells(column).ReadOnly <> locked Then gridRow.Cells(column).ReadOnly = locked
                Next

                ' Text first; color only repeats it.
                gridRow.DefaultCellStyle.ForeColor = If(locked AndAlso row.Fingerprint <> PacketExportFingerprint.Pending, UiTheme.MutedText(), Color.Empty)
                Select Case row.Fingerprint
                    Case PacketExportFingerprint.Changed
                        gridRow.Cells(ColumnFingerprint).Style.ForeColor = UiTheme.WarningColor()
                    Case PacketExportFingerprint.Missing, PacketExportFingerprint.Unreadable
                        gridRow.Cells(ColumnFingerprint).Style.ForeColor = UiTheme.DangerColor()
                    Case Else
                        gridRow.Cells(ColumnFingerprint).Style.ForeColor = Color.Empty
                End Select
                gridRow.Cells(ColumnHidden).Style.ForeColor =
                    If(row.Hidden IsNot Nothing AndAlso row.Hidden.HasFindings, UiTheme.WarningColor(), Color.Empty)
            Finally
                _loadingRows = wasLoading
            End Try

        End Sub


        Private Sub SetCell(gridRow As DataGridViewRow, column As String, value As Object)
            Dim cell As DataGridViewCell = gridRow.Cells(column)
            If gridFiles.IsCurrentCellInEditMode AndAlso cell Is gridFiles.CurrentCell Then Return
            If Not Object.Equals(cell.Value, value) Then cell.Value = value
        End Sub


        Private Shared Function HiddenText(row As PacketExportRow) As String
            If row.Hidden IsNot Nothing Then Return row.Hidden.Summary()
            If row.Fingerprint = PacketExportFingerprint.Pending Then Return PacketExportService.FingerprintText(PacketExportFingerprint.Pending)
            Return "—"
        End Function


        Private Sub GridCellChanged(sender As Object, e As DataGridViewCellEventArgs)

            If _loadingRows OrElse _busy OrElse e.RowIndex < 0 OrElse e.ColumnIndex < 0 Then Return
            Dim gridRow As DataGridViewRow = gridFiles.Rows(e.RowIndex)
            Dim row As PacketExportRow = TryCast(gridRow.Tag, PacketExportRow)
            If row Is Nothing Then Return
            Dim value As Object = gridRow.Cells(e.ColumnIndex).Value

            Select Case gridFiles.Columns(e.ColumnIndex).Name
                Case ColumnInclude
                    row.Include = TypeOf value Is Boolean AndAlso DirectCast(value, Boolean)
                Case ColumnName
                    row.RequestedName = Convert.ToString(value, CultureInfo.CurrentCulture)
                Case ColumnLabel
                    row.Label = Convert.ToString(value, CultureInfo.CurrentCulture).Trim()
                Case Else
                    Return
            End Select

            ' Names are cleaned, and duplicates numbered, among the files included.
            _plan.RefreshNames()
            RefreshGridValues()
            RefreshState()

        End Sub


        ' ---------------------------------------------------------------
        ' State
        ' ---------------------------------------------------------------

        Private Sub RefreshState()

            If IsDisposed Then Return

            gridFiles.Enabled = Not _busy
            chkAuthors.Enabled = Not _busy AndAlso Not _plan.IsBlinded
            chkAbstract.Enabled = Not _busy
            chkAcknowledge.Enabled = Not _busy
            txtPackageName.ReadOnly = _exporting
            If _busy Then
                btnExport.Enabled = False
                Return
            End If

            Dim included As IReadOnlyList(Of PacketExportRow) = _plan.IncludedRows()
            Dim withFindings As New HashSet(Of PacketExportRow)(
                included.Where(Function(item) item.Hidden IsNot Nothing AndAlso item.Hidden.HasFindings))
            Dim gained As Boolean = withFindings.Any(Function(item) Not _findingRows.Contains(item))
            _findingRows = withFindings
            ' Ticked for other files: a file with hidden information joining
            ' them needs a fresh look.
            If gained AndAlso chkAcknowledge.Checked Then chkAcknowledge.Checked = False
            chkAcknowledge.Visible = withFindings.Count > 0

            Dim namesPerson As Boolean = _plan.BlindedManuscriptNamesPerson()
            If namesPerson Then lblStrongWarning.Text = StrongWarningText()
            lblStrongWarning.Visible = namesPerson

            Dim notes As String = NotesText()
            If txtNotes.Text <> notes Then txtNotes.Text = notes

            btnExport.Enabled = _plan.FilesChecked AndAlso
                included.Count > 0 AndAlso
                (withFindings.Count = 0 OrElse chkAcknowledge.Checked)

        End Sub


        ' Why files start unchecked or can't be included, then warnings.
        Private Function NotesText() As String

            Dim lines As New List(Of String)()
            For Each row As PacketExportRow In _plan.Rows
                If row.Note.Length > 0 Then lines.Add(row.Note)
            Next
            lines.AddRange(_plan.AuthorNameWarnings())
            If _plan.HasUncheckedTypes() Then lines.Add(UncheckedTypesText)
            If lines.Count = 0 AndAlso _plan.FilesChecked Then lines.Add(NothingToNoteText)
            Return String.Join(Environment.NewLine, lines)

        End Function


        Private Function StrongWarningText() As String

            Dim finding As HiddenMetadataFinding = _plan.IncludedRows().
                Where(Function(item) item.Role = SubmissionPacketFileRole.BlindedManuscript AndAlso item.Hidden IsNot Nothing).
                SelectMany(Function(item) item.Hidden.Findings).
                FirstOrDefault(Function(item) item.NamesPerson)
            Dim shown As String = If(finding Is Nothing, String.Empty, " (" & finding.Label & ": " & finding.Value & ")")
            Return "The anonymized manuscript's hidden information names a person" & shown & ". " &
                "Anyone you send it to can see it. Remove it in the program that made the file, then add the cleaned file to the packet."

        End Function


        ' Read-only: what the package says about the manuscript.
        Private Function DetailsText() As String

            Dim version As String = If(
                _plan.VersionLabel.Length > 0,
                _plan.VersionLabel,
                If(_plan.VersionLinked AndAlso Not _plan.VersionFound, "Not found", "Not recorded"))
            Dim journal As String = If(_plan.JournalName.Length > 0, _plan.JournalName, "Not recorded")
            If _plan.JournalName.Length > 0 AndAlso _plan.JournalIssns.Count > 0 Then journal &= ", ISSN " & String.Join(", ", _plan.JournalIssns)

            Return String.Join(Environment.NewLine, {
                "Title: " & _plan.Title,
                "Version: " & version,
                "Journal: " & journal,
                "Submission: " & PacketExportPackage.SubmissionText(_plan),
                PacketExportService.RightsLine,
                PacketExportPackage.PackageDateNote
            })

        End Function


        Private Sub ShowStatus(message As String, Optional color As Color? = Nothing)
            lblStatus.Text = message
            lblStatus.ForeColor = If(color, UiTheme.SecondaryText())
        End Sub


        ' ---------------------------------------------------------------
        ' Checking the files
        ' ---------------------------------------------------------------

        ' Compares each file with its fingerprint and looks for hidden
        ' information, off the UI thread; then shows what was found and
        ' statusAfter. A new look needs a new acknowledgment.
        Private Async Function CheckFilesAsync(statusAfter As String, colorAfter As Color?) As Task

            If _busy OrElse IsDisposed Then Return

            Dim firstCheck As Boolean = Not _plan.FilesChecked
            Dim cancellation As New CancellationTokenSource()
            _cancellation = cancellation
            _busy = True
            ShowStatus(CheckingText, UiTheme.InfoColor())
            RefreshState()

            Dim failed As Boolean = False
            Try
                Await Task.Run(Sub() PacketExportService.CheckFiles(_plan, cancellation.Token))
            Catch ex As Exception
                ' Stopped by closing (the window is gone), or a file system
                ' failure: nothing can be exported until a check succeeds.
                failed = True
            Finally
                cancellation.Dispose()
                _cancellation = Nothing
                _busy = False
            End Try

            If IsDisposed Then Return

            _findingRows = New HashSet(Of PacketExportRow)()
            chkAcknowledge.Checked = False
            RefreshGridValues()
            If failed Then
                ShowStatus(CheckFailedText, UiTheme.DangerColor())
            Else
                ShowStatus(If(statusAfter, String.Empty), colorAfter)
            End If
            RefreshState()
            If firstCheck AndAlso Not failed AndAlso gridFiles.Rows.Count > 0 Then ActiveControl = gridFiles

        End Function


        ' ---------------------------------------------------------------
        ' Exporting
        ' ---------------------------------------------------------------

        ' Asks where to save the .zip, then writes it off the UI thread. The
        ' window stays open and says how it went.
        Friend Async Function ExportAsync() As Task

            If _busy OrElse IsDisposed Then Return
            If gridFiles.IsCurrentCellInEditMode Then gridFiles.EndEdit()
            RefreshState()
            If Not btnExport.Enabled Then Return

            Dim zipPath As String = ChooseZipPath()
            If String.IsNullOrWhiteSpace(zipPath) OrElse IsDisposed OrElse _busy Then Return

            Dim cancellation As New CancellationTokenSource()
            _cancellation = cancellation
            _busy = True
            _exporting = True
            ShowStatus(ExportingText, UiTheme.InfoColor())
            RefreshState()

            Dim message As String
            Dim messageColor As Color = UiTheme.DangerColor()
            Dim checkAgain As Boolean = False
            Try
                Dim result As PacketExportResult = Await Task.Run(
                    Function() PacketExportService.WriteZip(_plan, zipPath, cancellationToken:=cancellation.Token))
                SavedPath = zipPath
                message = "Exported " & FileCountText(result.FileCount) & " to " & result.ZipFileName & "."
                messageColor = UiTheme.SuccessColor()
            Catch ex As PacketExportException
                message = ex.Message
                ' A file went missing or changed: show the list as it is now.
                checkAgain = ex.Kind = PacketExportFailure.ChangedSincePreview OrElse
                    ex.Kind = PacketExportFailure.SourceMissing OrElse
                    ex.Kind = PacketExportFailure.SourceUnreadable OrElse
                    ex.Kind = PacketExportFailure.SourceChangedWhileReading
            Catch ex As Exception
                message = ExportFailedText
            Finally
                cancellation.Dispose()
                _cancellation = Nothing
                _busy = False
                _exporting = False
            End Try

            If IsDisposed Then Return

            ' Closing while exporting stopped it; now close.
            If _closeWhenStopped Then
                _closeWhenStopped = False
                If IsHandleCreated Then
                    BeginInvoke(New MethodInvoker(AddressOf CloseAfterStop))
                Else
                    CloseAfterStop()
                End If
                Return
            End If

            If checkAgain Then
                Await CheckFilesAsync(message, messageColor)
            Else
                ShowStatus(message, messageColor)
                RefreshState()
            End If

        End Function


        Private Function ChooseZipPath() As String

            If SavePathPrompt IsNot Nothing Then Return SavePathPrompt()

            Using picker As New SaveFileDialog With {
                .Title = Text,
                .Filter = "Zip file (*.zip)|*.zip",
                .DefaultExt = "zip",
                .AddExtension = True,
                .OverwritePrompt = True,
                .FileName = _plan.DefaultFileName()
            }
                If picker.ShowDialog(Me) <> DialogResult.OK Then Return String.Empty
                Return picker.FileName
            End Using

        End Function


        Private Shared Function FileCountText(count As Integer) As String
            Return count.ToString(CultureInfo.CurrentCulture) & If(count = 1, " file", " files")
        End Function


        ' ---------------------------------------------------------------
        ' Closing
        ' ---------------------------------------------------------------

        Protected Overrides Sub OnFormClosing(e As FormClosingEventArgs)
            Dim running As CancellationTokenSource = _cancellation
            If running IsNot Nothing Then
                running.Cancel()
                ' An export removes its unfinished .zip before the window
                ' closes; Windows shutting down is never held up.
                If _exporting AndAlso (e.CloseReason = CloseReason.UserClosing OrElse e.CloseReason = CloseReason.None) Then
                    _closeWhenStopped = True
                    btnCancel.Enabled = False
                    e.Cancel = True
                    Return
                End If
            End If
            MyBase.OnFormClosing(e)
        End Sub


        Private Sub CloseAfterStop()
            If IsDisposed Then Return
            DialogResult = DialogResult.Cancel
            If Not Modal Then Close()
        End Sub


        Protected Overrides Sub Dispose(disposing As Boolean)
            If disposing Then
                ' A check still running ends; its own code disposes it.
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

    End Class

End Namespace
