Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.IO
Imports System.Linq
Imports System.Windows.Forms
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

Namespace Forms

    Public Class PublicationExportForm
        Inherits Form

        Private ReadOnly _manuscripts As List(Of Manuscript)
        Private ReadOnly _authorLibrary As AuthorLibraryData

        Private ReadOnly cmbScope As New ComboBox()
        Private ReadOnly cmbStyle As New ComboBox()
        Private ReadOnly cmbFormat As New ComboBox()
        Private ReadOnly chkGroupByType As New CheckBox()
        Private ReadOnly lstItems As New CheckedListBox()
        Private ReadOnly txtPreview As New TextBox()

        Private _displayed As New List(Of Manuscript)()


        Public Sub New(
            manuscripts As IEnumerable(Of Manuscript),
            authorLibrary As AuthorLibraryData
        )

            _manuscripts =
                If(
                    manuscripts,
                    Enumerable.Empty(Of Manuscript)()
                ).
                Where(
                    Function(item)
                        Return item IsNot Nothing
                    End Function
                ).
                ToList()

            _authorLibrary =
                If(
                    authorLibrary,
                    New AuthorLibraryData()
                )

            BuildInterface()
            UiPolish.ApplyDialog(Me)

            cmbScope.SelectedIndex = 0
            cmbStyle.SelectedIndex = 0
            cmbFormat.SelectedIndex = 0

            RefreshItems()

        End Sub


        Private Sub BuildInterface()

            Me.Text =
                "Publication & CV Export"

            Me.StartPosition =
                FormStartPosition.CenterParent

            Me.Size =
                New Size(
                    1050,
                    760
                )

            Me.MinimumSize =
                New Size(
                    860,
                    620
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
                .ColumnCount = 1,
                .RowCount = 3,
                .Padding = New Padding(18)
            }

            root.RowStyles.Add(New RowStyle(SizeType.AutoSize))
            root.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
            root.RowStyles.Add(New RowStyle(SizeType.AutoSize))

            Dim options As New FlowLayoutPanel With {
                .Dock = DockStyle.Fill,
                .AutoSize = True,
                .FlowDirection = FlowDirection.LeftToRight,
                .WrapContents = True,
                .Padding = New Padding(0, 0, 0, 10)
            }

            options.Controls.Add(
                CreateOptionLabel(
                    "Scope"
                )
            )

            cmbScope.DropDownStyle =
                ComboBoxStyle.DropDownList

            cmbScope.Width =
                190

            cmbScope.Items.Add(
                New OptionItem(
                    PublicationExportScope.PublishedOnly,
                    "Published only"
                )
            )

            cmbScope.Items.Add(
                New OptionItem(
                    PublicationExportScope.AcceptedAndPublished,
                    "Accepted / In Press / Published"
                )
            )

            cmbScope.Items.Add(
                New OptionItem(
                    PublicationExportScope.AllManuscripts,
                    "All manuscripts"
                )
            )

            options.Controls.Add(
                cmbScope
            )

            options.Controls.Add(
                CreateOptionLabel(
                    "Style"
                )
            )

            cmbStyle.DropDownStyle =
                ComboBoxStyle.DropDownList

            cmbStyle.Width =
                160

            cmbStyle.Items.Add(
                New OptionItem(
                    PublicationExportStyle.CvSection,
                    "CV section"
                )
            )

            cmbStyle.Items.Add(
                New OptionItem(
                    PublicationExportStyle.PublicationList,
                    "Publication list"
                )
            )

            options.Controls.Add(
                cmbStyle
            )

            options.Controls.Add(
                CreateOptionLabel(
                    "Format"
                )
            )

            cmbFormat.DropDownStyle =
                ComboBoxStyle.DropDownList

            cmbFormat.Width =
                150

            cmbFormat.Items.Add(
                New OptionItem(
                    PublicationExportFormat.PlainText,
                    "Plain text"
                )
            )

            cmbFormat.Items.Add(
                New OptionItem(
                    PublicationExportFormat.Markdown,
                    "Markdown"
                )
            )

            cmbFormat.Items.Add(
                New OptionItem(
                    PublicationExportFormat.Html,
                    "HTML"
                )
            )

            options.Controls.Add(
                cmbFormat
            )

            ' Sections such as Journal articles and Conference papers (#64).
            chkGroupByType.Text = "Group by type"
            chkGroupByType.AutoSize = True
            chkGroupByType.Margin = New Padding(12, 6, 3, 3)
            AddHandler chkGroupByType.CheckedChanged, Sub(sender, e) RefreshPreview()
            options.Controls.Add(chkGroupByType)

            Dim btnAll As New Button With {
                .Text = "Select All",
                .AutoSize = True
            }

            Dim btnNone As New Button With {
                .Text = "Select None",
                .AutoSize = True
            }

            AddHandler btnAll.Click,
                Sub(sender, e)

                    For index As Integer = 0 To lstItems.Items.Count - 1

                        lstItems.SetItemChecked(
                            index,
                            True
                        )

                    Next

                    RefreshPreview()

                End Sub

            AddHandler btnNone.Click,
                Sub(sender, e)

                    For index As Integer = 0 To lstItems.Items.Count - 1

                        lstItems.SetItemChecked(
                            index,
                            False
                        )

                    Next

                    RefreshPreview()

                End Sub

            options.Controls.Add(
                btnAll
            )

            options.Controls.Add(
                btnNone
            )

            AddHandler cmbScope.SelectedIndexChanged,
                Sub(sender, e)
                    RefreshItems()
                End Sub

            AddHandler cmbStyle.SelectedIndexChanged,
                Sub(sender, e)
                    RefreshPreview()
                End Sub

            AddHandler cmbFormat.SelectedIndexChanged,
                Sub(sender, e)
                    RefreshPreview()
                End Sub

            AddHandler lstItems.ItemCheck,
                Sub(sender, e)

                    If Me.IsHandleCreated Then

                        BeginInvoke(
                            New Action(
                                AddressOf RefreshPreview
                            )
                        )

                    End If

                End Sub

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
                                0.42
                            )

                    End If

                End Sub

            lstItems.Dock =
                DockStyle.Fill

            lstItems.CheckOnClick =
                True

            lstItems.HorizontalScrollbar =
                True

            split.Panel1.Controls.Add(
                lstItems
            )

            txtPreview.Dock =
                DockStyle.Fill

            txtPreview.Multiline =
                True

            txtPreview.ReadOnly =
                True

            txtPreview.ScrollBars =
                ScrollBars.Both

            txtPreview.WordWrap =
                False

            split.Panel2.Controls.Add(
                txtPreview
            )

            Dim footer As New FlowLayoutPanel With {
                .Dock = DockStyle.Fill,
                .AutoSize = True,
                .FlowDirection = FlowDirection.RightToLeft,
                .WrapContents = False,
                .Padding = New Padding(0, 10, 0, 0)
            }

            Dim btnSave As New Button With {
                .Text = "Save Export...",
                .AutoSize = True,
                .Height = 38
            }

            Dim btnCopy As New Button With {
                .Text = "Copy to Clipboard",
                .AutoSize = True,
                .Height = 38
            }

            Dim btnClose As New Button With {
                .Text = "Close",
                .AutoSize = True,
                .Height = 38,
                .DialogResult = DialogResult.Cancel
            }

            AddHandler btnSave.Click,
                AddressOf SaveExport

            AddHandler btnCopy.Click,
                AddressOf CopyPreview

            footer.Controls.Add(btnSave)
            footer.Controls.Add(btnCopy)
            footer.Controls.Add(btnClose)

            root.Controls.Add(options, 0, 0)
            root.Controls.Add(split, 0, 1)
            root.Controls.Add(footer, 0, 2)

            Me.CancelButton =
                btnClose

            Me.Controls.Add(
                root
            )

        End Sub


        Private Function CreateOptionLabel(
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
                    8,
                    7,
                    4,
                    0
                )
            }

        End Function


        Private Sub RefreshItems()

            If cmbScope.SelectedItem Is Nothing Then
                Return
            End If

            Dim scope As PublicationExportScope =
                CType(
                    DirectCast(
                        cmbScope.SelectedItem,
                        OptionItem
                    ).Value,
                    PublicationExportScope
                )

            _displayed =
                PublicationExportService.SelectByScope(
                    _manuscripts,
                    scope
                )

            lstItems.BeginUpdate()

            Try

                lstItems.Items.Clear()

                For Each manuscript As Manuscript In
                    _displayed

                    Dim index As Integer =
                        lstItems.Items.Add(
                            New PublicationListItem(
                                manuscript
                            )
                        )

                    lstItems.SetItemChecked(
                        index,
                        True
                    )

                Next

            Finally

                lstItems.EndUpdate()

            End Try

            RefreshPreview()

        End Sub


        Private Function SelectedManuscripts() As List(Of Manuscript)

            Dim selected As New List(Of Manuscript)()

            For Each itemObject As Object In
                lstItems.CheckedItems

                Dim item As PublicationListItem =
                    TryCast(
                        itemObject,
                        PublicationListItem
                    )

                If item IsNot Nothing Then

                    selected.Add(
                        item.Manuscript
                    )

                End If

            Next

            Return selected

        End Function


        Private Function CurrentExport() As String

            If cmbFormat.SelectedItem Is Nothing OrElse
               cmbStyle.SelectedItem Is Nothing Then

                Return String.Empty

            End If

            Return PublicationExportService.Export(
                SelectedManuscripts(),
                _authorLibrary,
                CType(
                    DirectCast(
                        cmbFormat.SelectedItem,
                        OptionItem
                    ).Value,
                    PublicationExportFormat
                ),
                CType(
                    DirectCast(
                        cmbStyle.SelectedItem,
                        OptionItem
                    ).Value,
                    PublicationExportStyle
                ),
                chkGroupByType.Checked
            )

        End Function


        Private Sub RefreshPreview()

            txtPreview.Text =
                CurrentExport()

        End Sub


        Private Sub CopyPreview(
            sender As Object,
            e As EventArgs
        )

            Dim content As String =
                CurrentExport()

            If String.IsNullOrWhiteSpace(
                content
            ) Then

                MessageBox.Show(
                    Me,
                    "Select at least one manuscript first.",
                    "Nothing to Copy",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                )

                Return

            End If

            Try

                Clipboard.SetText(
                    content
                )

            Catch ex As Exception

                MessageBox.Show(
                    Me,
                    "PaperRoute could not copy the publication export to the clipboard." &
                    Environment.NewLine &
                    Environment.NewLine &
                    ex.Message,
                    "Clipboard Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                )

            End Try

        End Sub


        Private Sub SaveExport(
            sender As Object,
            e As EventArgs
        )

            Dim content As String =
                CurrentExport()

            If String.IsNullOrWhiteSpace(
                content
            ) Then

                MessageBox.Show(
                    Me,
                    "Select at least one manuscript first.",
                    "Nothing to Export",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                )

                Return

            End If

            Dim format As PublicationExportFormat =
                CType(
                    DirectCast(
                        cmbFormat.SelectedItem,
                        OptionItem
                    ).Value,
                    PublicationExportFormat
                )

            Dim filter As String
            Dim extension As String

            Select Case format

                Case PublicationExportFormat.PlainText

                    filter =
                        "Text file (*.txt)|*.txt"

                    extension =
                        "txt"

                Case PublicationExportFormat.Markdown

                    filter =
                        "Markdown file (*.md)|*.md"

                    extension =
                        "md"

                Case PublicationExportFormat.Html

                    filter =
                        "HTML file (*.html)|*.html"

                    extension =
                        "html"

                Case Else

                    Return

            End Select

            Using picker As New SaveFileDialog With {
                .Title = "Save Publication Export",
                .Filter = filter,
                .DefaultExt = extension,
                .AddExtension = True,
                .OverwritePrompt = True,
                .FileName =
                    "PaperRoute-Publications." &
                    extension
            }

                If picker.ShowDialog(Me) <>
                   DialogResult.OK Then

                    Return

                End If

                Try

                    File.WriteAllText(
                        picker.FileName,
                        content
                    )

                    MessageBox.Show(
                        Me,
                        "Publication export saved successfully." &
                        Environment.NewLine &
                        Environment.NewLine &
                        picker.FileName,
                        "Export Complete",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information
                    )

                Catch ex As Exception

                    MessageBox.Show(
                        Me,
                        "PaperRoute could not save the publication export." &
                        Environment.NewLine &
                        Environment.NewLine &
                        ex.Message,
                        "Export Error",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error
                    )

                End Try

            End Using

        End Sub


        Private Class OptionItem

            Public ReadOnly Property Value As Object
            Public ReadOnly Property Text As String


            Public Sub New(
                value As Object,
                text As String
            )

                Me.Value =
                    value

                Me.Text =
                    text

            End Sub


            Public Overrides Function ToString() As String

                Return Text

            End Function

        End Class


        Private Class PublicationListItem

            Public ReadOnly Property Manuscript As Manuscript


            Public Sub New(
                manuscript As Manuscript
            )

                Me.Manuscript =
                    manuscript

            End Sub


            Public Overrides Function ToString() As String

                Dim yearText As String =
                    If(
                        Manuscript.Metadata IsNot Nothing AndAlso
                        Manuscript.Metadata.PublishedDate.HasValue,
                        Manuscript.Metadata.PublishedDate.Value.Year.ToString(),
                        "n.d."
                    )

                Return yearText &
                    " — " &
                    Manuscript.Title &
                    "  [" &
                    Manuscript.CurrentStage.ToString() &
                    "]"

            End Function

        End Class

    End Class

End Namespace
