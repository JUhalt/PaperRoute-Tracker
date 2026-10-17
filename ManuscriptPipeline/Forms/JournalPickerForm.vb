Imports System
Imports System.Drawing
Imports System.Linq
Imports System.Windows.Forms
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

Namespace Forms

    Public Class JournalPickerForm
        Inherits Form

        Private ReadOnly _library As AuthorLibraryData

        Private ReadOnly txtSearch As New TextBox()
        Private ReadOnly lstJournals As New ListBox()

        Private _selectedJournal As JournalRecord


        Public ReadOnly Property SelectedJournal As JournalRecord
            Get
                Return _selectedJournal
            End Get
        End Property


        Public Sub New(
            library As AuthorLibraryData
        )

            _library =
                If(
                    library,
                    New AuthorLibraryData()
                )

            BuildInterface()
            UiPolish.ApplyDialog(Me)
            RefreshList()

        End Sub


        Private Sub BuildInterface()

            Me.Text =
                "Choose Journal"

            Me.StartPosition =
                FormStartPosition.CenterParent

            Me.Size =
                New Size(
                    680,
                    540
                )

            Me.MinimumSize =
                New Size(
                    540,
                    420
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

            root.RowStyles.Add(
                New RowStyle(
                    SizeType.AutoSize
                )
            )

            root.RowStyles.Add(
                New RowStyle(
                    SizeType.Percent,
                    100
                )
            )

            root.RowStyles.Add(
                New RowStyle(
                    SizeType.AutoSize
                )
            )

            txtSearch.Dock =
                DockStyle.Top

            txtSearch.PlaceholderText =
                "Search journals..."

            ' Placeholder text is not a name, and no label precedes either
            ' control (#137).
            txtSearch.AccessibleName =
                "Search journals"

            AddHandler txtSearch.TextChanged,
                Sub(sender, e)
                    RefreshList()
                End Sub

            lstJournals.Dock =
                DockStyle.Fill

            lstJournals.AccessibleName =
                "Journals"

            AddHandler lstJournals.DoubleClick,
                AddressOf ChooseSelected

            Dim buttons As New FlowLayoutPanel With {
                .Dock = DockStyle.Fill,
                .AutoSize = True,
                .FlowDirection = FlowDirection.RightToLeft,
                .WrapContents = False,
                .Padding = New Padding(0, 10, 0, 0)
            }

            Dim btnChoose As New Button With {
                .Text = "Choose",
                .AutoSize = True,
                .Height = 36
            }

            Dim btnCancel As New Button With {
                .Text = "Cancel",
                .AutoSize = True,
                .Height = 36,
                .DialogResult = DialogResult.Cancel
            }

            AddHandler btnChoose.Click,
                AddressOf ChooseSelected

            buttons.Controls.Add(
                btnChoose
            )

            buttons.Controls.Add(
                btnCancel
            )

            root.Controls.Add(
                txtSearch,
                0,
                0
            )

            root.Controls.Add(
                lstJournals,
                0,
                1
            )

            root.Controls.Add(
                buttons,
                0,
                2
            )

            Me.AcceptButton =
                btnChoose

            Me.CancelButton =
                btnCancel

            Me.Controls.Add(
                root
            )

        End Sub


        Private Sub RefreshList()

            Dim searchText As String =
                txtSearch.Text.Trim()

            Dim journals =
                _library.Journals.
                    Where(
                        Function(item)

                            If item Is Nothing Then
                                Return False
                            End If

                            If String.IsNullOrWhiteSpace(
                                searchText
                            ) Then

                                Return True

                            End If

                            Return (
                                item.Name.Contains(
                                    searchText,
                                    StringComparison.CurrentCultureIgnoreCase
                                ) OrElse
                                item.Publisher.Contains(
                                    searchText,
                                    StringComparison.CurrentCultureIgnoreCase
                                )
                            )

                        End Function
                    ).
                    OrderByDescending(
                        Function(item)
                            Return item.IsFavorite
                        End Function
                    ).
                    ThenByDescending(
                        Function(item)
                            Return item.IsShortlisted
                        End Function
                    ).
                    ThenBy(
                        Function(item)
                            Return item.Name
                        End Function,
                        StringComparer.CurrentCultureIgnoreCase
                    ).
                    ToList()

            lstJournals.BeginUpdate()

            Try

                lstJournals.Items.Clear()

                For Each journal As JournalRecord In journals

                    lstJournals.Items.Add(
                        journal
                    )

                Next

            Finally

                lstJournals.EndUpdate()

            End Try

        End Sub


        Private Sub ChooseSelected(
            sender As Object,
            e As EventArgs
        )

            Dim selected As JournalRecord =
                TryCast(
                    lstJournals.SelectedItem,
                    JournalRecord
                )

            If selected Is Nothing Then

                MessageBox.Show(
                    Me,
                    "Select a journal first.",
                    "Choose Journal",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                )

                Return

            End If

            _selectedJournal =
                selected

            Me.DialogResult =
                DialogResult.OK

        End Sub

    End Class

End Namespace
