Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.Linq
Imports System.Windows.Forms
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

Namespace Forms

    ' Paste a title page, review what PaperRoute proposes, and edit it before
    ' anything is applied. Parsing happens on this computer; nothing is saved
    ' until the new manuscript is added.
    Public Class TitlePageImportForm
        Inherits Form

        Private Const ColumnUse As String = "Use"
        Private Const ColumnName As String = "Author"
        Private Const ColumnAffiliations As String = "Affiliations"
        Private Const ColumnCorresponding As String = "Corresponding"
        Private Const ColumnLibrary As String = "Library"

        Private Const InLibraryText As String = "In your library"
        Private Const NewAuthorText As String = "New author"

        Private ReadOnly _library As AuthorLibraryData

        Private ReadOnly txtSource As New TextBox()
        Private ReadOnly txtTitle As New TextBox()
        Private ReadOnly gridAuthors As New DataGridView()
        Private ReadOnly txtAbstract As New TextBox()
        Private ReadOnly txtKeywords As New TextBox()
        Private ReadOnly txtUnplaced As New TextBox()
        Private ReadOnly lblSummary As New Label()
        Private ReadOnly btnUse As New Button()
        Private ReadOnly parseTimer As New System.Windows.Forms.Timer With {.Interval = 350}

        Private _parsed As New TitlePageParseResult()
        Private _reviewed As TitlePageParseResult

        Public ReadOnly Property ReviewedProposal As TitlePageParseResult
            Get
                Return _reviewed
            End Get
        End Property

        Public ReadOnly Property SourceText As String
            Get
                Return txtSource.Text
            End Get
        End Property

        Public Sub New(
            library As AuthorLibraryData,
            Optional initialText As String = ""
        )

            _library = If(library, New AuthorLibraryData())

            BuildInterface()
            UiPolish.ApplyDialog(Me)

            If Not String.IsNullOrWhiteSpace(initialText) Then
                txtSource.Text = initialText
                ParseNow()
            End If

            UpdateSummary()

        End Sub

        Private Sub BuildInterface()

            Me.Text = "Paste a Title Page"
            Me.StartPosition = FormStartPosition.CenterParent
            Me.Font = New Font("Segoe UI", 10.0F)
            Me.AutoScaleMode = AutoScaleMode.Dpi
            Me.ClientSize = New Size(1040, 680)
            Me.MinimumSize = New Size(820, 560)
            Me.MinimizeBox = False
            Me.ShowInTaskbar = False

            Dim root As New TableLayoutPanel With {
                .Dock = DockStyle.Fill,
                .ColumnCount = 2,
                .RowCount = 2,
                .Padding = New Padding(16)
            }

            root.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 42))
            root.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 58))
            root.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
            root.RowStyles.Add(New RowStyle(SizeType.AutoSize))

            root.Controls.Add(BuildSourcePanel(), 0, 0)
            root.Controls.Add(BuildPreviewPanel(), 1, 0)

            Dim footer As Control = BuildFooter()
            root.Controls.Add(footer, 0, 1)
            root.SetColumnSpan(footer, 2)

            Me.Controls.Add(root)

            AddHandler parseTimer.Tick,
                Sub(sender, e)
                    ParseNow()
                End Sub

            AddHandler Me.FormClosed,
                Sub(sender, e)
                    parseTimer.Stop()
                    parseTimer.Dispose()
                End Sub

        End Sub

        Private Function BuildSourcePanel() As Control

            Dim panel As New TableLayoutPanel With {
                .Dock = DockStyle.Fill,
                .ColumnCount = 1,
                .RowCount = 3,
                .Margin = New Padding(0, 0, 12, 0)
            }

            panel.RowStyles.Add(New RowStyle(SizeType.AutoSize))
            panel.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
            panel.RowStyles.Add(New RowStyle(SizeType.AutoSize))

            txtSource.Dock = DockStyle.Fill
            txtSource.Multiline = True
            txtSource.AcceptsReturn = True
            txtSource.ScrollBars = ScrollBars.Vertical
            txtSource.WordWrap = True
            txtSource.Font = New Font("Consolas", 9.5F)
            txtSource.AccessibleName = "Title page text"

            AddHandler txtSource.TextChanged,
                Sub(sender, e)
                    parseTimer.Stop()
                    parseTimer.Start()
                End Sub

            panel.Controls.Add(CreateHeading("Paste a title page"), 0, 0)
            panel.Controls.Add(txtSource, 0, 1)
            panel.Controls.Add(
                CreateHint(
                    "Copy the title page from Word or from your LaTeX source. " &
                    "It is read on this computer; nothing is sent anywhere."),
                0,
                2)

            Return panel

        End Function

        Private Function BuildPreviewPanel() As Control

            Dim panel As New TableLayoutPanel With {
                .Dock = DockStyle.Fill,
                .ColumnCount = 1,
                .RowCount = 11,
                .Margin = New Padding(0)
            }

            For Each style As RowStyle In {
                New RowStyle(SizeType.AutoSize),
                New RowStyle(SizeType.AutoSize),
                New RowStyle(SizeType.AutoSize),
                New RowStyle(SizeType.AutoSize),
                New RowStyle(SizeType.Percent, 44),
                New RowStyle(SizeType.AutoSize),
                New RowStyle(SizeType.Percent, 34),
                New RowStyle(SizeType.AutoSize),
                New RowStyle(SizeType.AutoSize),
                New RowStyle(SizeType.AutoSize),
                New RowStyle(SizeType.Percent, 22)
            }
                panel.RowStyles.Add(style)
            Next

            txtTitle.Dock = DockStyle.Fill
            txtTitle.AccessibleName = "Proposed title"

            ConfigureAuthorGrid()

            txtAbstract.Dock = DockStyle.Fill
            txtAbstract.Multiline = True
            txtAbstract.AcceptsReturn = True
            txtAbstract.ScrollBars = ScrollBars.Vertical
            txtAbstract.AccessibleName = "Proposed abstract"

            txtKeywords.Dock = DockStyle.Fill
            txtKeywords.AccessibleName = "Proposed keywords"

            txtUnplaced.Dock = DockStyle.Fill
            txtUnplaced.Multiline = True
            txtUnplaced.ReadOnly = True
            txtUnplaced.ScrollBars = ScrollBars.Vertical
            txtUnplaced.AccessibleName = "Text that was not placed"

            AddHandler txtTitle.TextChanged, Sub(sender, e) UpdateSummary()
            AddHandler txtAbstract.TextChanged, Sub(sender, e) UpdateSummary()
            AddHandler txtKeywords.TextChanged, Sub(sender, e) UpdateSummary()

            panel.Controls.Add(CreateHeading("PaperRoute found"), 0, 0)
            panel.Controls.Add(CreateFieldLabel("Title"), 0, 1)
            panel.Controls.Add(txtTitle, 0, 2)
            panel.Controls.Add(CreateFieldLabel("Authors, in order (separate affiliations with ;)"), 0, 3)
            panel.Controls.Add(gridAuthors, 0, 4)
            panel.Controls.Add(CreateFieldLabel("Abstract"), 0, 5)
            panel.Controls.Add(txtAbstract, 0, 6)
            panel.Controls.Add(CreateFieldLabel("Keywords (separate with commas)"), 0, 7)
            panel.Controls.Add(txtKeywords, 0, 8)
            panel.Controls.Add(CreateFieldLabel("Not placed (shown for review; not saved)"), 0, 9)
            panel.Controls.Add(txtUnplaced, 0, 10)

            Return panel

        End Function

        Private Sub ConfigureAuthorGrid()

            gridAuthors.Dock = DockStyle.Fill
            gridAuthors.AllowUserToAddRows = False
            gridAuthors.AllowUserToDeleteRows = False
            gridAuthors.AllowUserToResizeRows = False
            gridAuthors.RowHeadersVisible = False
            gridAuthors.SelectionMode = DataGridViewSelectionMode.CellSelect
            gridAuthors.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None
            gridAuthors.EditMode = DataGridViewEditMode.EditOnKeystrokeOrF2
            gridAuthors.AccessibleName = "Proposed authors"

            ' Single-line rows keep several authors visible; long affiliations
            ' show in full as a cell tooltip and while editing.
            gridAuthors.Columns.Add(
                New DataGridViewCheckBoxColumn With {
                    .Name = ColumnUse,
                    .HeaderText = "Use",
                    .AutoSizeMode = DataGridViewAutoSizeColumnMode.ColumnHeader,
                    .SortMode = DataGridViewColumnSortMode.NotSortable
                })

            gridAuthors.Columns.Add(
                New DataGridViewTextBoxColumn With {
                    .Name = ColumnName,
                    .HeaderText = "Author",
                    .AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                    .FillWeight = 40,
                    .MinimumWidth = 140,
                    .SortMode = DataGridViewColumnSortMode.NotSortable
                })

            gridAuthors.Columns.Add(
                New DataGridViewTextBoxColumn With {
                    .Name = ColumnAffiliations,
                    .HeaderText = "Affiliations",
                    .AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                    .FillWeight = 60,
                    .MinimumWidth = 140,
                    .SortMode = DataGridViewColumnSortMode.NotSortable
                })

            gridAuthors.Columns.Add(
                New DataGridViewCheckBoxColumn With {
                    .Name = ColumnCorresponding,
                    .HeaderText = "Corresponding",
                    .AutoSizeMode = DataGridViewAutoSizeColumnMode.ColumnHeader,
                    .SortMode = DataGridViewColumnSortMode.NotSortable
                })

            gridAuthors.Columns.Add(
                New DataGridViewTextBoxColumn With {
                    .Name = ColumnLibrary,
                    .HeaderText = "Library",
                    .ReadOnly = True,
                    .AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells,
                    .SortMode = DataGridViewColumnSortMode.NotSortable
                })

            ' Commit check box clicks immediately so the summary stays current.
            AddHandler gridAuthors.CurrentCellDirtyStateChanged,
                Sub(sender, e)
                    If gridAuthors.IsCurrentCellDirty AndAlso
                       TypeOf gridAuthors.CurrentCell Is DataGridViewCheckBoxCell Then
                        gridAuthors.CommitEdit(DataGridViewDataErrorContexts.Commit)
                    End If
                End Sub

            AddHandler gridAuthors.CellValueChanged, AddressOf AuthorCellChanged

        End Sub

        Private Function BuildFooter() As Control

            Dim footer As New TableLayoutPanel With {
                .Dock = DockStyle.Fill,
                .AutoSize = True,
                .ColumnCount = 3,
                .RowCount = 1,
                .Margin = New Padding(0, 12, 0, 0)
            }

            footer.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
            footer.ColumnStyles.Add(New ColumnStyle(SizeType.AutoSize))
            footer.ColumnStyles.Add(New ColumnStyle(SizeType.AutoSize))

            lblSummary.AutoSize = True
            lblSummary.Anchor = AnchorStyles.Left
            lblSummary.UseMnemonic = False
            lblSummary.ForeColor = UiTheme.SecondaryText()

            Dim btnCancel As New Button With {
                .Text = "Cancel",
                .AutoSize = True,
                .Height = 34,
                .DialogResult = DialogResult.Cancel
            }

            btnUse.Text = "Use These Details"
            btnUse.AutoSize = True
            btnUse.Height = 34
            AddHandler btnUse.Click, AddressOf UseDetails

            footer.Controls.Add(lblSummary, 0, 0)
            footer.Controls.Add(btnCancel, 1, 0)
            footer.Controls.Add(btnUse, 2, 0)

            Me.CancelButton = btnCancel

            Return footer

        End Function

        ' Reads the pasted text now instead of waiting for the typing pause.
        Friend Sub ParseNow()

            parseTimer.Stop()
            _parsed = TitlePageParserService.Parse(txtSource.Text)

            txtTitle.Text = _parsed.Title
            txtAbstract.Text = _parsed.AbstractText
            txtKeywords.Text = String.Join(", ", _parsed.Keywords)

            gridAuthors.Rows.Clear()

            For Each author As TitlePageAuthor In _parsed.Authors
                gridAuthors.Rows.Add(
                    True,
                    author.Name.DisplayName,
                    String.Join("; ", author.Affiliations),
                    author.IsCorrespondingAuthor,
                    LibraryStatus(author.Name))
            Next

            txtUnplaced.Text =
                String.Join(
                    Environment.NewLine,
                    _parsed.UnplacedLines.Concat(
                        _parsed.Warnings.Select(Function(warning) "Check: " & warning)))

            UpdateSummary()

        End Sub

        Private Sub AuthorCellChanged(sender As Object, e As DataGridViewCellEventArgs)

            If e.RowIndex < 0 Then
                Return
            End If

            If gridAuthors.Columns(e.ColumnIndex).Name = ColumnName Then
                Dim row As DataGridViewRow = gridAuthors.Rows(e.RowIndex)
                Dim name As BibliographyAuthor =
                    BibliographyTextService.ParsePersonName(CellText(row, ColumnName), Nothing)
                row.Cells(ColumnLibrary).Value = LibraryStatus(name)
            End If

            UpdateSummary()

        End Sub

        Private Function LibraryStatus(name As BibliographyAuthor) As String

            If String.IsNullOrWhiteSpace(name.DisplayName) Then
                Return String.Empty
            End If

            Return If(
                TitlePageApplyService.FindLibraryAuthor(name, _library) IsNot Nothing,
                InLibraryText,
                NewAuthorText)

        End Function

        Private Iterator Function UsedAuthorRows() As IEnumerable(Of DataGridViewRow)

            For Each row As DataGridViewRow In gridAuthors.Rows
                If IsChecked(row.Cells(ColumnUse).Value) AndAlso
                   CellText(row, ColumnName).Length > 0 Then
                    Yield row
                End If
            Next

        End Function

        Private Sub UpdateSummary()

            If txtSource.TextLength = 0 Then
                lblSummary.Text = "Paste text to see what PaperRoute finds."
                btnUse.Enabled = False
                Return
            End If

            Dim used As List(Of DataGridViewRow) = UsedAuthorRows().ToList()
            Dim inLibrary As Integer =
                used.Where(Function(row) CellText(row, ColumnLibrary) = InLibraryText).Count()
            Dim keywordCount As Integer = BibliographyTextService.SplitKeywords(txtKeywords.Text).Count

            Dim parts As New List(Of String)()

            If used.Count > 0 Then
                parts.Add(
                    Plural(used.Count, "author") &
                    If(inLibrary > 0, " (" & inLibrary.ToString() & " in your library)", String.Empty))
            End If

            If txtAbstract.Text.Trim().Length > 0 Then
                parts.Add("abstract")
            End If

            If keywordCount > 0 Then
                parts.Add(Plural(keywordCount, "keyword"))
            End If

            If _parsed.UnplacedLines.Count > 0 Then
                parts.Add(Plural(_parsed.UnplacedLines.Count, "line") & " not placed")
            End If

            lblSummary.Text =
                If(parts.Count = 0,
                   "Nothing recognized yet. You can still type a title.",
                   String.Join(" " & ChrW(&HB7) & " ", parts))

            btnUse.Enabled =
                txtTitle.Text.Trim().Length > 0 OrElse
                used.Count > 0 OrElse
                txtAbstract.Text.Trim().Length > 0 OrElse
                keywordCount > 0

        End Sub

        Private Sub UseDetails(sender As Object, e As EventArgs)

            gridAuthors.EndEdit()

            Dim reviewed As New TitlePageParseResult With {
                .SourceFormat = _parsed.SourceFormat,
                .Title = BibliographyTextService.CollapseWhitespace(txtTitle.Text),
                .AbstractText = txtAbstract.Text.Trim()
            }

            reviewed.Keywords.AddRange(BibliographyTextService.SplitKeywords(txtKeywords.Text))
            reviewed.UnplacedLines.AddRange(_parsed.UnplacedLines)
            reviewed.Warnings.AddRange(_parsed.Warnings)

            For Each row As DataGridViewRow In UsedAuthorRows()
                Dim author As New TitlePageAuthor With {
                    .Name = BibliographyTextService.ParsePersonName(CellText(row, ColumnName), Nothing),
                    .IsCorrespondingAuthor = IsChecked(row.Cells(ColumnCorresponding).Value)
                }

                author.Affiliations.AddRange(
                    CellText(row, ColumnAffiliations).
                        Split(";"c).
                        Select(Function(item) BibliographyTextService.CollapseWhitespace(item)).
                        Where(Function(item) item.Length > 0))

                reviewed.Authors.Add(author)
            Next

            _reviewed = reviewed
            Me.DialogResult = DialogResult.OK

        End Sub

        Private Shared Function CellText(row As DataGridViewRow, column As String) As String

            Return BibliographyTextService.CollapseWhitespace(Convert.ToString(row.Cells(column).Value))

        End Function

        Private Shared Function IsChecked(value As Object) As Boolean

            Return TypeOf value Is Boolean AndAlso DirectCast(value, Boolean)

        End Function

        Private Shared Function Plural(count As Integer, noun As String) As String

            Return count.ToString() & " " & noun & If(count = 1, String.Empty, "s")

        End Function

        Private Function CreateHeading(text As String) As Label

            Return New Label With {
                .Text = text,
                .AutoSize = True,
                .UseMnemonic = False,
                .Font = New Font(Me.Font.FontFamily, 12.0F, FontStyle.Bold),
                .Margin = New Padding(0, 0, 0, 8)
            }

        End Function

        Private Function CreateFieldLabel(text As String) As Label

            Return New Label With {
                .Text = text,
                .AutoSize = True,
                .UseMnemonic = False,
                .Font = New Font(Me.Font, FontStyle.Bold),
                .Margin = New Padding(0, 8, 0, 2)
            }

        End Function

        Private Shared Function CreateHint(text As String) As Label

            Return New Label With {
                .Text = text,
                .AutoSize = True,
                .UseMnemonic = False,
                .MaximumSize = New Size(400, 0),
                .ForeColor = UiTheme.SecondaryText(),
                .Margin = New Padding(0, 6, 0, 0)
            }

        End Function

    End Class

End Namespace
