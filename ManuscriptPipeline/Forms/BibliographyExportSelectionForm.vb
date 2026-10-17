Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.Linq
Imports System.Windows.Forms
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

Namespace Forms

    Public Class BibliographyExportSelectionForm
        Inherits Form

        Private ReadOnly _manuscripts As List(Of Manuscript)
        Private ReadOnly lstManuscripts As New CheckedListBox()

        Private _selectedManuscripts As List(Of Manuscript) =
            New List(Of Manuscript)()

        Public ReadOnly Property SelectedManuscripts As List(Of Manuscript)
            Get
                Return _selectedManuscripts
            End Get
        End Property

        Public Sub New(
            manuscripts As IEnumerable(Of Manuscript),
            formatName As String
        )
            _manuscripts =
                If(
                    manuscripts,
                    Enumerable.Empty(Of Manuscript)()
                ).
                Where(Function(item) item IsNot Nothing).
                ToList()

            BuildInterface(formatName)
            UiPolish.ApplyDialog(Me)
            Populate()
        End Sub

        Private Sub BuildInterface(formatName As String)
            Me.Text = "Export " & formatName
            Me.StartPosition = FormStartPosition.CenterParent
            Me.Size = New Size(760, 600)
            Me.MinimumSize = New Size(620, 480)
            Me.Font = New Font("Segoe UI", 10.0F)
            Me.AutoScaleMode = AutoScaleMode.Dpi

            Dim root As New TableLayoutPanel With {
                .Dock = DockStyle.Fill,
                .ColumnCount = 1,
                .RowCount = 3,
                .Padding = New Padding(18)
            }

            root.RowStyles.Add(New RowStyle(SizeType.AutoSize))
            root.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
            root.RowStyles.Add(New RowStyle(SizeType.AutoSize))

            Dim header As New FlowLayoutPanel With {
                .Dock = DockStyle.Fill,
                .AutoSize = True,
                .FlowDirection = FlowDirection.LeftToRight,
                .WrapContents = True,
                .Padding = New Padding(0, 0, 0, 10)
            }

            Dim btnAll As New Button With {
                .Text = "Select All",
                .AutoSize = True
            }

            Dim btnPublished As New Button With {
                .Text = "Published Only",
                .AutoSize = True
            }

            Dim btnNone As New Button With {
                .Text = "Select None",
                .AutoSize = True
            }

            AddHandler btnAll.Click,
                Sub(sender, e)
                    For index As Integer = 0 To lstManuscripts.Items.Count - 1
                        lstManuscripts.SetItemChecked(index, True)
                    Next
                End Sub

            AddHandler btnPublished.Click,
                Sub(sender, e)
                    For index As Integer = 0 To lstManuscripts.Items.Count - 1
                        Dim item As ExportListItem =
                            TryCast(
                                lstManuscripts.Items(index),
                                ExportListItem
                            )

                        lstManuscripts.SetItemChecked(
                            index,
                            item IsNot Nothing AndAlso
                            item.Manuscript.Location =
                                ManuscriptLocation.Published
                        )
                    Next
                End Sub

            AddHandler btnNone.Click,
                Sub(sender, e)
                    For index As Integer = 0 To lstManuscripts.Items.Count - 1
                        lstManuscripts.SetItemChecked(index, False)
                    Next
                End Sub

            header.Controls.Add(btnAll)
            header.Controls.Add(btnPublished)
            header.Controls.Add(btnNone)

            lstManuscripts.Dock = DockStyle.Fill
            lstManuscripts.CheckOnClick = True
            lstManuscripts.HorizontalScrollbar = True
            ' No label precedes the list (#137).
            lstManuscripts.AccessibleName = "Manuscripts to export"

            Dim footer As New FlowLayoutPanel With {
                .Dock = DockStyle.Fill,
                .AutoSize = True,
                .FlowDirection = FlowDirection.RightToLeft,
                .WrapContents = False,
                .Padding = New Padding(0, 10, 0, 0)
            }

            Dim btnExport As New Button With {
                .Text = "Export Selected",
                .AutoSize = True,
                .Height = 38
            }

            Dim btnCancel As New Button With {
                .Text = "Cancel",
                .AutoSize = True,
                .Height = 38,
                .DialogResult = DialogResult.Cancel
            }

            AddHandler btnExport.Click,
                AddressOf ConfirmExport

            footer.Controls.Add(btnExport)
            footer.Controls.Add(btnCancel)

            root.Controls.Add(header, 0, 0)
            root.Controls.Add(lstManuscripts, 0, 1)
            root.Controls.Add(footer, 0, 2)

            Me.CancelButton = btnCancel
            Me.Controls.Add(root)
        End Sub

        Private Sub Populate()
            For Each manuscript As Manuscript In
                _manuscripts.
                    OrderBy(
                        Function(item) item.Title,
                        StringComparer.CurrentCultureIgnoreCase
                    )

                Dim index As Integer =
                    lstManuscripts.Items.Add(
                        New ExportListItem With {
                            .Manuscript = manuscript
                        }
                    )

                lstManuscripts.SetItemChecked(index, True)
            Next
        End Sub

        Private Sub ConfirmExport(
            sender As Object,
            e As EventArgs
        )
            Dim selected As New List(Of Manuscript)()

            For Each item As Object In lstManuscripts.CheckedItems
                Dim exportItem As ExportListItem =
                    TryCast(item, ExportListItem)

                If exportItem IsNot Nothing Then
                    selected.Add(exportItem.Manuscript)
                End If
            Next

            If selected.Count = 0 Then
                MessageBox.Show(
                    Me,
                    "Select at least one manuscript to export.",
                    "Nothing Selected",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                )
                Return
            End If

            _selectedManuscripts = selected
            Me.DialogResult = DialogResult.OK
        End Sub

        Private Class ExportListItem

            Public Property Manuscript As Manuscript

            Public Overrides Function ToString() As String
                If Manuscript Is Nothing Then
                    Return "(Invalid manuscript)"
                End If

                Return Manuscript.Title &
                    "  [" &
                    Manuscript.Location.ToString() &
                    "]"
            End Function

        End Class

    End Class

End Namespace
