Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.Linq
Imports System.Windows.Forms
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

Namespace Forms

    Public Class BibliographyImportForm
        Inherits Form

        Private ReadOnly _parseResult As BibliographyParseResult
        Private ReadOnly _existingManuscripts As IEnumerable(Of Manuscript)

        Private ReadOnly lstRecords As New CheckedListBox()
        Private ReadOnly txtDetails As New TextBox()
        Private ReadOnly chkPublished As New CheckBox()

        Private _selectedRecords As List(Of BibliographyRecord) =
            New List(Of BibliographyRecord)()

        Public ReadOnly Property SelectedRecords As List(Of BibliographyRecord)
            Get
                Return _selectedRecords
            End Get
        End Property

        Public ReadOnly Property ImportPublishedRecordsAsPublished As Boolean
            Get
                Return chkPublished.Checked
            End Get
        End Property

        Public Sub New(
            parseResult As BibliographyParseResult,
            existingManuscripts As IEnumerable(Of Manuscript)
        )
            If parseResult Is Nothing Then
                Throw New ArgumentNullException(NameOf(parseResult))
            End If

            _parseResult = parseResult
            _existingManuscripts =
                If(
                    existingManuscripts,
                    Enumerable.Empty(Of Manuscript)()
                )

            BuildInterface()
            UiPolish.ApplyDialog(Me)
            PopulateRecords()
        End Sub

        Private Sub BuildInterface()
            Me.Text =
                "Import " &
                If(
                    _parseResult.Format = BibliographyFormat.BibTeX,
                    "BibTeX",
                    "RIS"
                )

            Me.StartPosition = FormStartPosition.CenterParent
            Me.Size = New Size(980, 700)
            Me.MinimumSize = New Size(800, 560)
            Me.Font = New Font("Segoe UI", 10.0F)
            Me.AutoScaleMode = AutoScaleMode.Dpi

            Dim root As New TableLayoutPanel With {
                .Dock = DockStyle.Fill,
                .ColumnCount = 1,
                .RowCount = 4,
                .Padding = New Padding(18)
            }

            root.RowStyles.Add(New RowStyle(SizeType.AutoSize))
            root.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
            root.RowStyles.Add(New RowStyle(SizeType.AutoSize))
            root.RowStyles.Add(New RowStyle(SizeType.AutoSize))

            Dim intro As New Label With {
                .AutoSize = True,
                .MaximumSize = New Size(900, 0),
                .Text =
                    "Review the parsed bibliography records before importing. " &
                    "Duplicates are left unchecked. Warnings identify fields PaperRoute could not map safely.",
                .Margin = New Padding(0, 0, 0, 10)
            }

            Dim split As New SplitContainer With {
                .Dock = DockStyle.Fill,
                .Orientation = Orientation.Vertical
            }

            AddHandler Me.Shown,
                Sub(sender, e)
                    If split.ClientSize.Width > 0 Then
                        split.SplitterDistance =
                            CInt(
                                split.ClientSize.Width *
                                0.62
                            )
                    End If
                End Sub

            lstRecords.Dock = DockStyle.Fill
            lstRecords.CheckOnClick = True
            lstRecords.HorizontalScrollbar = True
            ' Neither the list nor its details have a label (#137).
            lstRecords.AccessibleName = "Records to import"

            AddHandler lstRecords.SelectedIndexChanged,
                AddressOf RecordSelectionChanged

            split.Panel1.Controls.Add(lstRecords)

            txtDetails.Dock = DockStyle.Fill
            txtDetails.Multiline = True
            txtDetails.ReadOnly = True
            txtDetails.ScrollBars = ScrollBars.Vertical
            txtDetails.AccessibleName = "Selected record details"

            split.Panel2.Controls.Add(txtDetails)

            Dim options As New FlowLayoutPanel With {
                .Dock = DockStyle.Fill,
                .AutoSize = True,
                .FlowDirection = FlowDirection.LeftToRight,
                .WrapContents = True,
                .Padding = New Padding(0, 10, 0, 4)
            }

            chkPublished.Text =
                "Import records with publication metadata as Published"

            chkPublished.AutoSize = True
            chkPublished.Checked = True

            Dim btnSelectAllNew As New Button With {
                .Text = "Select All New",
                .AutoSize = True
            }

            Dim btnSelectNone As New Button With {
                .Text = "Select None",
                .AutoSize = True
            }

            AddHandler btnSelectAllNew.Click,
                Sub(sender, e)
                    SelectAllNew()
                End Sub

            AddHandler btnSelectNone.Click,
                Sub(sender, e)
                    For index As Integer = 0 To lstRecords.Items.Count - 1
                        lstRecords.SetItemChecked(index, False)
                    Next
                End Sub

            options.Controls.Add(chkPublished)
            options.Controls.Add(btnSelectAllNew)
            options.Controls.Add(btnSelectNone)

            Dim footer As New FlowLayoutPanel With {
                .Dock = DockStyle.Fill,
                .AutoSize = True,
                .FlowDirection = FlowDirection.RightToLeft,
                .WrapContents = False,
                .Padding = New Padding(0, 8, 0, 0)
            }

            Dim btnImport As New Button With {
                .Text = "Import Selected",
                .AutoSize = True,
                .Height = 38
            }

            Dim btnCancel As New Button With {
                .Text = "Cancel",
                .AutoSize = True,
                .Height = 38,
                .DialogResult = DialogResult.Cancel
            }

            AddHandler btnImport.Click,
                AddressOf ConfirmImport

            footer.Controls.Add(btnImport)
            footer.Controls.Add(btnCancel)

            root.Controls.Add(intro, 0, 0)
            root.Controls.Add(split, 0, 1)
            root.Controls.Add(options, 0, 2)
            root.Controls.Add(footer, 0, 3)

            Me.AcceptButton = btnImport
            Me.CancelButton = btnCancel
            Me.Controls.Add(root)
        End Sub

        Private Sub PopulateRecords()
            lstRecords.Items.Clear()

            For Each record As BibliographyRecord In _parseResult.Records
                Dim duplicateReason As String =
                    BibliographyExchangeService.FindDuplicateReason(
                        record,
                        _existingManuscripts
                    )

                Dim item As New ImportListItem With {
                    .Record = record,
                    .DuplicateReason = duplicateReason
                }

                Dim index As Integer = lstRecords.Items.Add(item)

                lstRecords.SetItemChecked(
                    index,
                    String.IsNullOrWhiteSpace(duplicateReason)
                )
            Next

            If lstRecords.Items.Count > 0 Then
                lstRecords.SelectedIndex = 0
            Else
                txtDetails.Text = BuildFileWarningText()
            End If
        End Sub

        Private Sub SelectAllNew()
            For index As Integer = 0 To lstRecords.Items.Count - 1
                Dim item As ImportListItem =
                    TryCast(
                        lstRecords.Items(index),
                        ImportListItem
                    )

                If item Is Nothing Then
                    Continue For
                End If

                lstRecords.SetItemChecked(
                    index,
                    String.IsNullOrWhiteSpace(item.DuplicateReason)
                )
            Next
        End Sub

        Private Sub RecordSelectionChanged(
            sender As Object,
            e As EventArgs
        )
            Dim item As ImportListItem =
                TryCast(
                    lstRecords.SelectedItem,
                    ImportListItem
                )

            If item Is Nothing Then
                Return
            End If

            txtDetails.Text = BuildDetailText(item)
        End Sub

        Private Function BuildDetailText(
            item As ImportListItem
        ) As String

            Dim record As BibliographyRecord = item.Record

            Dim lines As New List(Of String) From {
                "Title: " &
                    If(
                        String.IsNullOrWhiteSpace(record.Title),
                        "(missing)",
                        record.Title
                    ),
                "Authors: " & record.Authors.Count.ToString(),
                "Journal / outlet: " &
                    If(
                        String.IsNullOrWhiteSpace(record.Journal),
                        "(none)",
                        record.Journal
                    ),
                "Published: " &
                    If(
                        record.PublishedDate.HasValue,
                        record.PublishedDate.Value.ToString("yyyy-MM-dd"),
                        "(none)"
                    ),
                "DOI: " &
                    If(
                        String.IsNullOrWhiteSpace(record.Doi),
                        "(none)",
                        record.Doi
                    ),
                "Source type: " &
                    If(
                        String.IsNullOrWhiteSpace(record.SourceType),
                        "(none)",
                        record.SourceType
                    )
            }

            If Not String.IsNullOrWhiteSpace(item.DuplicateReason) Then
                lines.Add(String.Empty)
                lines.Add("DUPLICATE: " & item.DuplicateReason)
            End If

            If record.Warnings.Count > 0 Then
                lines.Add(String.Empty)
                lines.Add("Warnings:")

                For Each warning As String In record.Warnings
                    lines.Add("• " & warning)
                Next
            End If

            If _parseResult.FileWarnings.Count > 0 Then
                lines.Add(String.Empty)
                lines.Add("File-level warnings:")

                For Each warning As String In _parseResult.FileWarnings
                    lines.Add("• " & warning)
                Next
            End If

            Return String.Join(Environment.NewLine, lines)
        End Function

        Private Function BuildFileWarningText() As String
            If _parseResult.FileWarnings.Count = 0 Then
                Return "No records were found."
            End If

            Return String.Join(
                Environment.NewLine,
                _parseResult.FileWarnings.
                    Select(Function(item) "• " & item)
            )
        End Function

        Private Sub ConfirmImport(
            sender As Object,
            e As EventArgs
        )
            Dim selected As New List(Of BibliographyRecord)()

            For Each itemObject As Object In lstRecords.CheckedItems
                Dim item As ImportListItem =
                    TryCast(itemObject, ImportListItem)

                If item Is Nothing Then
                    Continue For
                End If

                selected.Add(item.Record)
            Next

            If selected.Count = 0 Then
                MessageBox.Show(
                    Me,
                    "Select at least one bibliography record to import.",
                    "Nothing Selected",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                )
                Return
            End If

            _selectedRecords = selected
            Me.DialogResult = DialogResult.OK
        End Sub

        Private Class ImportListItem

            Public Property Record As BibliographyRecord
            Public Property DuplicateReason As String = String.Empty

            Public Overrides Function ToString() As String
                Dim prefix As String =
                    If(
                        String.IsNullOrWhiteSpace(DuplicateReason),
                        String.Empty,
                        "[Duplicate] "
                    )

                Return prefix & Record.DisplayName
            End Function

        End Class

    End Class

End Namespace
