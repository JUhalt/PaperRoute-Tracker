Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.Linq
Imports System.Windows.Forms
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

Namespace Forms

    Public Class JournalEditForm
        Inherits Form

        Private ReadOnly _source As JournalRecord

        Private ReadOnly txtName As New TextBox()
        Private ReadOnly txtPublisher As New TextBox()
        Private ReadOnly txtHomepage As New TextBox()
        Private ReadOnly txtPortal As New TextBox()
        Private ReadOnly txtNotes As New TextBox()
        Private ReadOnly chkFavorite As New CheckBox()
        Private ReadOnly chkShortlist As New CheckBox()

        Private ReadOnly lstChecklist As New ListBox()
        Private ReadOnly lblChecklistInfo As New Label()
        Private ReadOnly btnEditChecklist As New Button()
        Private ReadOnly btnRemoveChecklist As New Button()
        Private ReadOnly btnMoveChecklistUp As New Button()
        Private ReadOnly btnMoveChecklistDown As New Button()

        Private ReadOnly _workingChecklist As New List(
            Of JournalChecklistTemplateItem
        )()

        Private _result As JournalRecord


        Public ReadOnly Property Result As JournalRecord
            Get
                Return _result
            End Get
        End Property


        Public Sub New(
            source As JournalRecord
        )

            _source = source

            CloneChecklistFromSource()
            BuildInterface()
            UiPolish.ApplyDialog(Me)
            LoadSource()
            RefreshChecklist()

        End Sub


        Private Sub BuildInterface()

            Me.Text =
                If(
                    _source Is Nothing,
                    "Add Journal",
                    "Edit Journal"
                )

            Me.StartPosition =
                FormStartPosition.CenterParent

            Me.Size =
                New Size(
                    820,
                    720
                )

            Me.MinimumSize =
                New Size(
                    700,
                    600
                )

            Me.Font =
                New Font(
                    "Segoe UI",
                    10.0F
                )

            Me.AutoScaleMode =
                AutoScaleMode.Dpi

            Dim shell As New TableLayoutPanel With {
                .Dock = DockStyle.Fill,
                .ColumnCount = 1,
                .RowCount = 2,
                .Padding = New Padding(14)
            }

            shell.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
            shell.RowStyles.Add(New RowStyle(SizeType.AutoSize))

            Dim tabs As New TabControl With {
                .Dock = DockStyle.Fill
            }

            tabs.TabPages.Add(
                CreateGeneralTab()
            )

            tabs.TabPages.Add(
                CreateChecklistTab()
            )

            Dim footer As New FlowLayoutPanel With {
                .Dock = DockStyle.Fill,
                .AutoSize = True,
                .FlowDirection = FlowDirection.RightToLeft,
                .WrapContents = False,
                .Padding = New Padding(0, 10, 0, 0)
            }

            Dim btnSave As New Button With {
                .Text = "Save",
                .AutoSize = True,
                .Height = 36
            }

            Dim btnCancel As New Button With {
                .Text = "Cancel",
                .AutoSize = True,
                .Height = 36,
                .DialogResult = DialogResult.Cancel
            }

            AddHandler btnSave.Click,
                AddressOf SaveJournal

            footer.Controls.Add(btnSave)
            footer.Controls.Add(btnCancel)

            shell.Controls.Add(tabs, 0, 0)
            shell.Controls.Add(footer, 0, 1)

            Me.AcceptButton =
                btnSave

            Me.CancelButton =
                btnCancel

            Me.Controls.Add(shell)

        End Sub


        Private Function CreateGeneralTab() As TabPage

            Dim tab As New TabPage(
                "Journal"
            )

            tab.Padding =
                New Padding(14)

            Dim general As New TableLayoutPanel With {
                .Dock = DockStyle.Fill,
                .ColumnCount = 2,
                .RowCount = 6
            }

            general.ColumnStyles.Add(
                New ColumnStyle(
                    SizeType.Absolute,
                    180
                )
            )

            general.ColumnStyles.Add(
                New ColumnStyle(
                    SizeType.Percent,
                    100
                )
            )

            For index As Integer = 0 To 4
                general.RowStyles.Add(
                    New RowStyle(
                        SizeType.AutoSize
                    )
                )
            Next

            general.RowStyles.Add(
                New RowStyle(
                    SizeType.Percent,
                    100
                )
            )

            txtName.Dock =
                DockStyle.Fill

            txtPublisher.Dock =
                DockStyle.Fill

            txtHomepage.Dock =
                DockStyle.Fill

            txtPortal.Dock =
                DockStyle.Fill

            txtNotes.Dock =
                DockStyle.Fill

            txtNotes.Multiline =
                True

            txtNotes.ScrollBars =
                ScrollBars.Vertical

            txtNotes.MinimumSize =
                New Size(
                    0,
                    150
                )

            Dim flags As New FlowLayoutPanel With {
                .Dock = DockStyle.Fill,
                .AutoSize = True,
                .FlowDirection = FlowDirection.LeftToRight,
                .WrapContents = True
            }

            chkFavorite.Text =
                "Favorite"

            chkFavorite.AutoSize =
                True

            chkShortlist.Text =
                "Shortlist"

            chkShortlist.AutoSize =
                True

            flags.Controls.Add(chkFavorite)
            flags.Controls.Add(chkShortlist)

            general.Controls.Add(CreateLabel("Journal name"), 0, 0)
            general.Controls.Add(txtName, 1, 0)

            general.Controls.Add(CreateLabel("Publisher"), 0, 1)
            general.Controls.Add(txtPublisher, 1, 1)

            general.Controls.Add(CreateLabel("Homepage URL"), 0, 2)
            general.Controls.Add(txtHomepage, 1, 2)

            general.Controls.Add(CreateLabel("Submission portal"), 0, 3)
            general.Controls.Add(txtPortal, 1, 3)

            general.Controls.Add(CreateLabel("List status"), 0, 4)
            general.Controls.Add(flags, 1, 4)

            general.Controls.Add(CreateLabel("Notes"), 0, 5)
            general.Controls.Add(txtNotes, 1, 5)

            tab.Controls.Add(general)

            Return tab

        End Function


        Private Function CreateChecklistTab() As TabPage

            Dim tab As New TabPage(
                "Readiness Checklist"
            )

            tab.Padding =
                New Padding(14)

            Dim root As New TableLayoutPanel With {
                .Dock = DockStyle.Fill,
                .ColumnCount = 1,
                .RowCount = 4
            }

            root.RowStyles.Add(New RowStyle(SizeType.AutoSize))
            root.RowStyles.Add(New RowStyle(SizeType.AutoSize))
            root.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
            root.RowStyles.Add(New RowStyle(SizeType.AutoSize))

            Dim intro As New Label With {
                .AutoSize = True,
                .MaximumSize = New Size(720, 0),
                .Text =
                    "Build the reusable submission-readiness template for this journal. " &
                    "New manuscript readiness profiles copy these requirements as snapshots; " &
                    "later template edits do not silently rewrite existing manuscript records.",
                .Margin = New Padding(0, 0, 0, 8)
            }

            lblChecklistInfo.AutoSize =
                True

            lblChecklistInfo.ForeColor =
                SystemColors.GrayText

            lblChecklistInfo.Margin =
                New Padding(
                    0,
                    0,
                    0,
                    8
                )

            lstChecklist.Dock =
                DockStyle.Fill

            AddHandler lstChecklist.SelectedIndexChanged,
                AddressOf ChecklistSelectionChanged

            AddHandler lstChecklist.DoubleClick,
                AddressOf EditChecklistItem

            Dim buttons As New FlowLayoutPanel With {
                .Dock = DockStyle.Fill,
                .AutoSize = True,
                .FlowDirection = FlowDirection.LeftToRight,
                .WrapContents = True,
                .Padding = New Padding(0, 10, 0, 0)
            }

            Dim btnAdd As New Button With {
                .Text = "Add Requirement",
                .AutoSize = True,
                .Height = 36
            }

            btnEditChecklist.Text =
                "Edit"

            btnEditChecklist.AutoSize =
                True

            btnEditChecklist.Height =
                36

            btnRemoveChecklist.Text =
                "Remove"

            btnRemoveChecklist.AutoSize =
                True

            btnRemoveChecklist.Height =
                36

            btnMoveChecklistUp.Text =
                "↑"

            btnMoveChecklistUp.AutoSize =
                True

            btnMoveChecklistUp.Height =
                36

            btnMoveChecklistUp.AccessibleName =
                "Move requirement up"

            btnMoveChecklistDown.Text =
                "↓"

            btnMoveChecklistDown.AutoSize =
                True

            btnMoveChecklistDown.Height =
                36

            btnMoveChecklistDown.AccessibleName =
                "Move requirement down"

            AddHandler btnAdd.Click,
                AddressOf AddChecklistItem

            AddHandler btnEditChecklist.Click,
                AddressOf EditChecklistItem

            AddHandler btnRemoveChecklist.Click,
                AddressOf RemoveChecklistItem

            AddHandler btnMoveChecklistUp.Click,
                AddressOf MoveChecklistItemUp

            AddHandler btnMoveChecklistDown.Click,
                AddressOf MoveChecklistItemDown

            buttons.Controls.Add(btnAdd)
            buttons.Controls.Add(btnEditChecklist)
            buttons.Controls.Add(btnRemoveChecklist)
            buttons.Controls.Add(btnMoveChecklistUp)
            buttons.Controls.Add(btnMoveChecklistDown)

            root.Controls.Add(intro, 0, 0)
            root.Controls.Add(lblChecklistInfo, 0, 1)
            root.Controls.Add(lstChecklist, 0, 2)
            root.Controls.Add(buttons, 0, 3)

            tab.Controls.Add(root)

            Return tab

        End Function


        Private Function CreateLabel(
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


        Private Sub CloneChecklistFromSource()

            If _source Is Nothing OrElse
               _source.ReadinessChecklistTemplate Is Nothing Then

                Return

            End If

            For Each item As JournalChecklistTemplateItem In
                _source.ReadinessChecklistTemplate.
                    Where(
                        Function(candidate)
                            Return candidate IsNot Nothing
                        End Function
                    ).
                    OrderBy(
                        Function(candidate)
                            Return candidate.SortOrder
                        End Function
                    ).
                    ThenBy(
                        Function(candidate)
                            Return candidate.Title
                        End Function,
                        StringComparer.CurrentCultureIgnoreCase
                    )

                _workingChecklist.Add(
                    CloneChecklistItem(item)
                )

            Next

        End Sub


        Private Function CloneChecklistItem(
            source As JournalChecklistTemplateItem
        ) As JournalChecklistTemplateItem

            Return New JournalChecklistTemplateItem With {
                .Id = source.Id,
                .Title = source.Title,
                .Description = source.Description,
                .Category = source.Category,
                .SortOrder = source.SortOrder,
                .IsRequired = source.IsRequired
            }

        End Function


        Private Sub LoadSource()

            If _source Is Nothing Then
                Return
            End If

            txtName.Text =
                _source.Name

            txtPublisher.Text =
                _source.Publisher

            txtHomepage.Text =
                _source.HomepageUrl

            txtPortal.Text =
                _source.SubmissionPortalUrl

            txtNotes.Text =
                _source.Notes

            chkFavorite.Checked =
                _source.IsFavorite

            chkShortlist.Checked =
                _source.IsShortlisted

        End Sub


        Private Sub RefreshChecklist(
            Optional selectIndex As Integer = -1
        )

            lstChecklist.BeginUpdate()

            Try

                lstChecklist.Items.Clear()

                For Each item As JournalChecklistTemplateItem In
                    _workingChecklist

                    lstChecklist.Items.Add(
                        FormatChecklistItem(item)
                    )

                Next

            Finally

                lstChecklist.EndUpdate()

            End Try

            If _workingChecklist.Count > 0 Then

                If selectIndex < 0 Then
                    selectIndex = 0
                End If

                lstChecklist.SelectedIndex =
                    Math.Min(
                        selectIndex,
                        _workingChecklist.Count - 1
                    )

            End If

            UpdateChecklistButtons()
            UpdateChecklistInfo()

        End Sub


        Private Function FormatChecklistItem(
            item As JournalChecklistTemplateItem
        ) As String

            Dim importance As String =
                If(
                    item.IsRequired,
                    "Required",
                    "Optional"
                )

            Dim category As String =
                If(
                    String.IsNullOrWhiteSpace(
                        item.Category
                    ),
                    "General",
                    item.Category.Trim()
                )

            Return (
                importance &
                "  •  " &
                category &
                "  —  " &
                item.Title
            )

        End Function


        Private Function GetSelectedChecklistIndex() As Integer

            Dim index As Integer =
                lstChecklist.SelectedIndex

            If index < 0 OrElse
               index >= _workingChecklist.Count Then

                Return -1

            End If

            Return index

        End Function


        Private Sub ChecklistSelectionChanged(
            sender As Object,
            e As EventArgs
        )

            UpdateChecklistButtons()
            UpdateChecklistInfo()

        End Sub


        Private Sub UpdateChecklistInfo()

            Dim index As Integer =
                GetSelectedChecklistIndex()

            If index < 0 Then

                lblChecklistInfo.Text =
                    _workingChecklist.Count.ToString() &
                    " reusable requirement(s)."

                Return

            End If

            Dim item As JournalChecklistTemplateItem =
                _workingChecklist(index)

            Dim detail As String =
                If(
                    String.IsNullOrWhiteSpace(
                        item.Description
                    ),
                    "No additional instructions.",
                    item.Description.Trim()
                )

            lblChecklistInfo.Text =
                item.Title &
                " — " &
                detail

        End Sub


        Private Sub UpdateChecklistButtons()

            Dim index As Integer =
                GetSelectedChecklistIndex()

            Dim hasSelection As Boolean =
                index >= 0

            btnEditChecklist.Enabled =
                hasSelection

            btnRemoveChecklist.Enabled =
                hasSelection

            btnMoveChecklistUp.Enabled =
                hasSelection AndAlso
                index > 0

            btnMoveChecklistDown.Enabled =
                hasSelection AndAlso
                index >= 0 AndAlso
                index < _workingChecklist.Count - 1

        End Sub


        Private Sub AddChecklistItem(
            sender As Object,
            e As EventArgs
        )

            Using dialog As New JournalChecklistItemEditForm(
                Nothing
            )

                If dialog.ShowDialog(Me) <>
                   DialogResult.OK OrElse
                   dialog.Result Is Nothing Then

                    Return

                End If

                Dim added As JournalChecklistTemplateItem =
                    dialog.Result

                added.SortOrder =
                    (_workingChecklist.Count + 1) *
                    10

                _workingChecklist.Add(added)

                RefreshChecklist(
                    _workingChecklist.Count - 1
                )

            End Using

        End Sub


        Private Sub EditChecklistItem(
            sender As Object,
            e As EventArgs
        )

            Dim index As Integer =
                GetSelectedChecklistIndex()

            If index < 0 Then
                Return
            End If

            Using dialog As New JournalChecklistItemEditForm(
                _workingChecklist(index)
            )

                If dialog.ShowDialog(Me) <>
                   DialogResult.OK OrElse
                   dialog.Result Is Nothing Then

                    Return

                End If

                _workingChecklist(index) =
                    dialog.Result

                RefreshChecklist(index)

            End Using

        End Sub


        Private Sub RemoveChecklistItem(
            sender As Object,
            e As EventArgs
        )

            Dim index As Integer =
                GetSelectedChecklistIndex()

            If index < 0 Then
                Return
            End If

            Dim selected As JournalChecklistTemplateItem =
                _workingChecklist(index)

            Dim result As DialogResult =
                MessageBox.Show(
                    Me,
                    "Remove '" &
                    selected.Title &
                    "' from this reusable journal template?" &
                    Environment.NewLine &
                    Environment.NewLine &
                    "Existing manuscript readiness profiles keep their saved snapshot of this requirement.",
                    "Remove Readiness Requirement",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question
                )

            If result <>
               DialogResult.Yes Then
                Return
            End If

            _workingChecklist.RemoveAt(index)
            NormalizeSortOrder()

            RefreshChecklist(
                Math.Min(
                    index,
                    _workingChecklist.Count - 1
                )
            )

        End Sub


        Private Sub MoveChecklistItemUp(
            sender As Object,
            e As EventArgs
        )

            Dim index As Integer =
                GetSelectedChecklistIndex()

            If index <= 0 Then
                Return
            End If

            Dim item As JournalChecklistTemplateItem =
                _workingChecklist(index)

            _workingChecklist.RemoveAt(index)
            _workingChecklist.Insert(index - 1, item)

            NormalizeSortOrder()
            RefreshChecklist(index - 1)

        End Sub


        Private Sub MoveChecklistItemDown(
            sender As Object,
            e As EventArgs
        )

            Dim index As Integer =
                GetSelectedChecklistIndex()

            If index < 0 OrElse
               index >= _workingChecklist.Count - 1 Then
                Return
            End If

            Dim item As JournalChecklistTemplateItem =
                _workingChecklist(index)

            _workingChecklist.RemoveAt(index)
            _workingChecklist.Insert(index + 1, item)

            NormalizeSortOrder()
            RefreshChecklist(index + 1)

        End Sub


        Private Sub NormalizeSortOrder()

            For index As Integer =
                0 To _workingChecklist.Count - 1

                _workingChecklist(index).SortOrder =
                    (index + 1) *
                    10

            Next

        End Sub


        Private Sub SaveJournal(
            sender As Object,
            e As EventArgs
        )

            If String.IsNullOrWhiteSpace(
                txtName.Text
            ) Then

                MessageBox.Show(
                    Me,
                    "Enter a journal name.",
                    "Journal Name Required",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                )

                txtName.Focus()
                Return

            End If

            Dim homepage As String
            Dim portal As String

            Try

                homepage =
                    UrlSafetyService.NormalizeOptionalHttpUrl(
                        txtHomepage.Text,
                        "Journal homepage"
                    )

                portal =
                    UrlSafetyService.NormalizeOptionalHttpUrl(
                        txtPortal.Text,
                        "Submission portal"
                    )

            Catch ex As ArgumentException

                MessageBox.Show(
                    Me,
                    ex.Message,
                    "Check Journal URL",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                )

                Return

            End Try

            NormalizeSortOrder()

            _result =
                New JournalRecord With {
                    .Id =
                        If(
                            _source Is Nothing,
                            Guid.NewGuid(),
                            _source.Id
                        ),
                    .Name = txtName.Text.Trim(),
                    .Publisher = txtPublisher.Text.Trim(),
                    .HomepageUrl = homepage,
                    .SubmissionPortalUrl = portal,
                    .Notes = txtNotes.Text.Trim(),
                    .IsFavorite = chkFavorite.Checked,
                    .IsShortlisted = chkShortlist.Checked,
                    .ReadinessChecklistTemplate =
                        _workingChecklist.
                            Select(
                                Function(item)
                                    Return CloneChecklistItem(item)
                                End Function
                            ).
                            ToList()
                }

            Me.DialogResult =
                DialogResult.OK

        End Sub

    End Class

End Namespace
