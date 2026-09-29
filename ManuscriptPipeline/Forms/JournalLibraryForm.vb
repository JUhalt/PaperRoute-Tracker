Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.Linq
Imports System.Windows.Forms
Imports ManuscriptPipeline.Controls
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

Namespace Forms

    Public Class JournalLibraryForm
        Inherits Form

        Private ReadOnly _repository As New AuthorLibraryRepository()
        Private ReadOnly _manuscripts As List(Of Manuscript)

        Private _library As AuthorLibraryData

        Private ReadOnly lstJournals As New ListBox()
        Private ReadOnly lblInfo As New Label()


        Public Sub New(
            Optional manuscripts As IEnumerable(Of Manuscript) = Nothing
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

            _library =
                _repository.Load()

            BuildInterface()
            EmptyHint.Attach(lstJournals, "No journals yet. Add Journal records a journal's homepage, submission portal, notes, and checklist once, for every manuscript.")
            UiPolish.ApplyDialog(Me)
            RefreshList()

        End Sub


        Private Sub BuildInterface()

            Me.Text =
                "Journal Library"

            Me.StartPosition =
                FormStartPosition.CenterParent

            Me.Size =
                New Size(
                    900,
                    650
                )

            Me.MinimumSize =
                New Size(
                    720,
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
                .ColumnCount = 1,
                .RowCount = 4,
                .Padding = New Padding(18)
            }

            root.RowStyles.Add(
                New RowStyle(
                    SizeType.AutoSize
                )
            )

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

            Dim intro As New Label With {
                .AutoSize = True,
                .MaximumSize = New Size(840, 0),
                .Text =
                    "Store reusable journal metadata, homepages, and submission portals here. " &
                    "PaperRoute stores links only — never publisher passwords or credentials.",
                .Margin = New Padding(0, 0, 0, 8)
            }

            lblInfo.AutoSize =
                True

            lblInfo.ForeColor =
                SystemColors.GrayText

            lblInfo.Margin =
                New Padding(0, 0, 0, 8)

            lstJournals.Dock =
                DockStyle.Fill

            AddHandler lstJournals.SelectedIndexChanged,
                AddressOf SelectionChanged

            AddHandler lstJournals.DoubleClick,
                AddressOf EditSelected

            Dim footer As New TableLayoutPanel With {
                .Dock = DockStyle.Fill,
                .AutoSize = True,
                .ColumnCount = 2,
                .RowCount = 1,
                .Padding = New Padding(0, 10, 0, 0)
            }

            footer.ColumnStyles.Add(
                New ColumnStyle(
                    SizeType.Percent,
                    100
                )
            )

            footer.ColumnStyles.Add(
                New ColumnStyle(
                    SizeType.AutoSize
                )
            )

            Dim leftButtons As New FlowLayoutPanel With {
                .Dock = DockStyle.Fill,
                .AutoSize = True,
                .FlowDirection = FlowDirection.LeftToRight,
                .WrapContents = True
            }

            Dim btnAdd As New Button With {
                .Text = "Add Journal",
                .AutoSize = True,
                .Height = 36
            }

            Dim btnEdit As New Button With {
                .Text = "Edit",
                .AutoSize = True,
                .Height = 36
            }

            Dim btnDelete As New Button With {
                .Text = "Delete",
                .AutoSize = True,
                .Height = 36
            }

            Dim btnHomepage As New Button With {
                .Text = "Open Homepage",
                .AutoSize = True,
                .Height = 36
            }

            Dim btnPortal As New Button With {
                .Text = "Open Portal",
                .AutoSize = True,
                .Height = 36
            }

            AddHandler btnAdd.Click,
                AddressOf AddJournal

            AddHandler btnEdit.Click,
                AddressOf EditSelected

            AddHandler btnDelete.Click,
                AddressOf DeleteSelected

            AddHandler btnHomepage.Click,
                Sub(sender, e)
                    OpenSelectedUrl(
                        homepage:=True
                    )
                End Sub

            AddHandler btnPortal.Click,
                Sub(sender, e)
                    OpenSelectedUrl(
                        homepage:=False
                    )
                End Sub

            leftButtons.Controls.Add(btnAdd)
            leftButtons.Controls.Add(btnEdit)
            leftButtons.Controls.Add(btnDelete)
            leftButtons.Controls.Add(btnHomepage)
            leftButtons.Controls.Add(btnPortal)

            Dim btnClose As New Button With {
                .Text = "Close",
                .AutoSize = True,
                .Height = 36,
                .DialogResult = DialogResult.OK
            }

            footer.Controls.Add(leftButtons, 0, 0)
            footer.Controls.Add(btnClose, 1, 0)

            root.Controls.Add(intro, 0, 0)
            root.Controls.Add(lblInfo, 0, 1)
            root.Controls.Add(lstJournals, 0, 2)
            root.Controls.Add(footer, 0, 3)

            Me.AcceptButton =
                btnClose

            Me.Controls.Add(
                root
            )

        End Sub


        Private Sub RefreshList()

            Dim selectedId As Guid? =
                Nothing

            Dim selected As JournalRecord =
                TryCast(
                    lstJournals.SelectedItem,
                    JournalRecord
                )

            If selected IsNot Nothing Then
                selectedId = selected.Id
            End If

            lstJournals.BeginUpdate()

            Try

                lstJournals.Items.Clear()

                For Each journal As JournalRecord In
                    _library.Journals.
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
                        )

                    lstJournals.Items.Add(
                        journal
                    )

                Next

                If selectedId.HasValue Then

                    For index As Integer = 0 To lstJournals.Items.Count - 1

                        Dim item As JournalRecord =
                            TryCast(
                                lstJournals.Items(index),
                                JournalRecord
                            )

                        If item IsNot Nothing AndAlso
                           item.Id = selectedId.Value Then

                            lstJournals.SelectedIndex =
                                index

                            Exit For

                        End If

                    Next

                End If

            Finally

                lstJournals.EndUpdate()

            End Try

            SelectionChanged(
                Nothing,
                EventArgs.Empty
            )

        End Sub


        Private Sub SelectionChanged(
            sender As Object,
            e As EventArgs
        )

            Dim selected As JournalRecord =
                TryCast(
                    lstJournals.SelectedItem,
                    JournalRecord
                )

            If selected Is Nothing Then

                lblInfo.Text =
                    _library.Journals.Count.ToString() &
                    " reusable journal(s)."

                Return

            End If

            Dim targetCount As Integer =
                _manuscripts.
                    Where(
                        Function(item)
                            Return item.TargetJournalId.HasValue AndAlso
                                item.TargetJournalId.Value = selected.Id
                        End Function
                    ).
                    Count()

            ' Your own history with the journal (#62), from recorded
            ' submissions only: linked ones, and exact name matches.
            Dim history As JournalHistory =
                RouteAnalyticsService.ForLibrary(_manuscripts, DateTime.Today, _library.Journals).Journals.
                    FirstOrDefault(Function(item) item.JournalId.HasValue AndAlso item.JournalId.Value = selected.Id)

            Dim parts As New List(Of String) From {
                selected.Name & " — target on " & targetCount.ToString() & If(targetCount = 1, " manuscript", " manuscripts")
            }

            parts.Add(RouteAnalyticsService.DescribeHistory(history))

            lblInfo.Text = String.Join(" · ", parts)

        End Sub


        Private Sub AddJournal(
            sender As Object,
            e As EventArgs
        )

            Using dialog As New JournalEditForm(
                Nothing
            )

                If dialog.ShowDialog(Me) <>
                   DialogResult.OK OrElse
                   dialog.Result Is Nothing Then

                    Return

                End If

                _library.Journals.Add(
                    dialog.Result
                )

                SaveAndRefresh()

            End Using

        End Sub


        Private Sub EditSelected(
            sender As Object,
            e As EventArgs
        )

            Dim selected As JournalRecord =
                TryCast(
                    lstJournals.SelectedItem,
                    JournalRecord
                )

            If selected Is Nothing Then
                Return
            End If

            Using dialog As New JournalEditForm(
                selected
            )

                If dialog.ShowDialog(Me) <>
                   DialogResult.OK OrElse
                   dialog.Result Is Nothing Then

                    Return

                End If

                Dim index As Integer =
                    _library.Journals.FindIndex(
                        Function(item)
                            Return item.Id =
                                selected.Id
                        End Function
                    )

                If index >= 0 Then

                    _library.Journals(index) =
                        dialog.Result

                End If

                SaveAndRefresh()

            End Using

        End Sub


        Private Sub DeleteSelected(
            sender As Object,
            e As EventArgs
        )

            Dim selected As JournalRecord =
                TryCast(
                    lstJournals.SelectedItem,
                    JournalRecord
                )

            If selected Is Nothing Then
                Return
            End If

            Dim inUse As Boolean =
                _manuscripts.Any(
                    Function(item)

                        If item.TargetJournalId.HasValue AndAlso
                           item.TargetJournalId.Value = selected.Id Then

                            Return True

                        End If

                        If item.Submissions Is Nothing Then
                            Return False
                        End If

                        Return item.Submissions.Any(
                            Function(submission)
                                Return submission IsNot Nothing AndAlso
                                    submission.JournalId.HasValue AndAlso
                                    submission.JournalId.Value = selected.Id
                            End Function
                        )

                    End Function
                )

            If inUse Then

                MessageBox.Show(
                    Me,
                    "This journal is currently linked to at least one manuscript or submission. " &
                    "Remove those links before deleting the reusable journal record.",
                    "Journal Is In Use",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                )

                Return

            End If

            If MessageBox.Show(
                Me,
                "Delete the reusable journal '" &
                selected.Name &
                "'?",
                "Delete Journal",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning
            ) <> DialogResult.Yes Then

                Return

            End If

            _library.Journals.RemoveAll(
                Function(item)
                    Return item.Id =
                        selected.Id
                End Function
            )

            SaveAndRefresh()

        End Sub


        Private Sub OpenSelectedUrl(
            homepage As Boolean
        )

            Dim selected As JournalRecord =
                TryCast(
                    lstJournals.SelectedItem,
                    JournalRecord
                )

            If selected Is Nothing Then
                Return
            End If

            Dim url As String =
                If(
                    homepage,
                    selected.HomepageUrl,
                    selected.SubmissionPortalUrl
                )

            If String.IsNullOrWhiteSpace(url) Then

                MessageBox.Show(
                    Me,
                    If(
                        homepage,
                        "This journal does not have a homepage URL saved.",
                        "This journal does not have a submission portal URL saved."
                    ),
                    "No URL Saved",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                )

                Return

            End If

            Try

                UrlSafetyService.OpenInBrowser(
                    url
                )

            Catch ex As Exception

                MessageBox.Show(
                    Me,
                    ex.Message,
                    "Open Journal Link",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                )

            End Try

        End Sub


        Private Sub SaveAndRefresh()

            _repository.Save(
                _library
            )

            _library =
                _repository.Load()

            RefreshList()

        End Sub

    End Class

End Namespace
