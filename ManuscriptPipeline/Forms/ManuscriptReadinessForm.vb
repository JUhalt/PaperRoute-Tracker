Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.Linq
Imports System.Windows.Forms
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

Namespace Forms

    Public Class ManuscriptReadinessForm
        Inherits Form

        Private ReadOnly _sourceManuscript As Manuscript
        Private ReadOnly _workingManuscript As Manuscript
        Private ReadOnly _library As AuthorLibraryData

        Private ReadOnly cmbProfiles As New ComboBox()
        Private ReadOnly lblSummary As New Label()
        Private ReadOnly lstItems As New ListBox()
        Private ReadOnly lblItemDetail As New Label()

        Private ReadOnly btnRefreshTemplate As New Button()
        Private ReadOnly btnDeleteProfile As New Button()

        Private ReadOnly btnComplete As New Button()
        Private ReadOnly btnNotApplicable As New Button()
        Private ReadOnly btnReset As New Button()
        Private ReadOnly btnNotes As New Button()

        Private ReadOnly _displayedItems As New List(
            Of ReadinessItemState
        )()


        Public Sub New(
            manuscript As Manuscript,
            library As AuthorLibraryData
        )

            If manuscript Is Nothing Then
                Throw New ArgumentNullException(
                    NameOf(manuscript)
                )
            End If

            _sourceManuscript =
                manuscript

            _workingManuscript =
                ManuscriptCloneService.CloneManuscript(
                    manuscript
                )

            _library =
                If(
                    library,
                    New AuthorLibraryData()
                )

            BuildInterface()
            UiPolish.ApplyDialog(Me)
            RefreshProfiles()

        End Sub


        Private Sub BuildInterface()

            Me.Text =
                "Submission Readiness"

            Me.StartPosition =
                FormStartPosition.CenterParent

            Me.Size =
                New Size(
                    940,
                    720
                )

            Me.MinimumSize =
                New Size(
                    760,
                    600
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
                .RowCount = 6,
                .Padding = New Padding(18)
            }

            root.RowStyles.Add(New RowStyle(SizeType.AutoSize))
            root.RowStyles.Add(New RowStyle(SizeType.AutoSize))
            root.RowStyles.Add(New RowStyle(SizeType.AutoSize))
            root.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
            root.RowStyles.Add(New RowStyle(SizeType.AutoSize))
            root.RowStyles.Add(New RowStyle(SizeType.AutoSize))

            Dim intro As New Label With {
                .AutoSize = True,
                .MaximumSize = New Size(870, 0),
                .Text =
                    "Track whether '" &
                    _workingManuscript.Title &
                    "' is ready for a specific journal. " &
                    "Readiness is advisory: it never changes the manuscript stage, creates a submission, or blocks you from recording what actually happened.",
                .Margin = New Padding(0, 0, 0, 10)
            }

            Dim profileBar As New FlowLayoutPanel With {
                .Dock = DockStyle.Fill,
                .AutoSize = True,
                .FlowDirection = FlowDirection.LeftToRight,
                .WrapContents = True,
                .Margin = New Padding(0, 0, 0, 8)
            }

            Dim lblProfile As New Label With {
                .Text = "Journal profile:",
                .AutoSize = True,
                .Font = New Font(
                    Me.Font,
                    FontStyle.Bold
                ),
                .Margin = New Padding(0, 9, 8, 0)
            }

            cmbProfiles.DropDownStyle =
                ComboBoxStyle.DropDownList

            cmbProfiles.Width =
                300

            AddHandler cmbProfiles.SelectedIndexChanged,
                AddressOf ProfileSelectionChanged

            Dim btnNewProfile As New Button With {
                .Text = "New from Journal...",
                .AutoSize = True,
                .Height = 36
            }

            btnRefreshTemplate.Text =
                "Check for New Requirements"

            btnRefreshTemplate.AutoSize =
                True

            btnRefreshTemplate.Height =
                36

            btnDeleteProfile.Text =
                "Delete Profile"

            btnDeleteProfile.AutoSize =
                True

            btnDeleteProfile.Height =
                36

            AddHandler btnNewProfile.Click,
                AddressOf NewProfile

            AddHandler btnRefreshTemplate.Click,
                AddressOf RefreshFromTemplate

            AddHandler btnDeleteProfile.Click,
                AddressOf DeleteProfile

            profileBar.Controls.Add(lblProfile)
            profileBar.Controls.Add(cmbProfiles)
            profileBar.Controls.Add(btnNewProfile)
            profileBar.Controls.Add(btnRefreshTemplate)
            profileBar.Controls.Add(btnDeleteProfile)

            lblSummary.AutoSize =
                True

            lblSummary.Font =
                New Font(
                    Me.Font,
                    FontStyle.Bold
                )

            lblSummary.Margin =
                New Padding(
                    0,
                    0,
                    0,
                    8
                )

            Dim itemArea As New TableLayoutPanel With {
                .Dock = DockStyle.Fill,
                .ColumnCount = 1,
                .RowCount = 2,
                .Margin = New Padding(0)
            }

            itemArea.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
            itemArea.RowStyles.Add(New RowStyle(SizeType.AutoSize))

            lstItems.Dock =
                DockStyle.Fill

            AddHandler lstItems.SelectedIndexChanged,
                AddressOf ItemSelectionChanged

            lblItemDetail.AutoSize =
                True

            lblItemDetail.MaximumSize =
                New Size(
                    860,
                    0
                )

            lblItemDetail.ForeColor =
                SystemColors.GrayText

            lblItemDetail.Margin =
                New Padding(
                    0,
                    8,
                    0,
                    0
                )

            itemArea.Controls.Add(lstItems, 0, 0)
            itemArea.Controls.Add(lblItemDetail, 0, 1)

            Dim itemButtons As New FlowLayoutPanel With {
                .Dock = DockStyle.Fill,
                .AutoSize = True,
                .FlowDirection = FlowDirection.LeftToRight,
                .WrapContents = True,
                .Padding = New Padding(0, 10, 0, 0)
            }

            btnComplete.Text =
                "Mark Complete"

            btnComplete.AutoSize =
                True

            btnComplete.Height =
                36

            btnNotApplicable.Text =
                "Not Applicable"

            btnNotApplicable.AutoSize =
                True

            btnNotApplicable.Height =
                36

            btnReset.Text =
                "Reset"

            btnReset.AutoSize =
                True

            btnReset.Height =
                36

            btnNotes.Text =
                "Notes..."

            btnNotes.AutoSize =
                True

            btnNotes.Height =
                36

            AddHandler btnComplete.Click,
                Sub(sender, e)
                    SetSelectedStatus(
                        ReadinessItemStatus.Complete
                    )
                End Sub

            AddHandler btnNotApplicable.Click,
                Sub(sender, e)
                    SetSelectedStatus(
                        ReadinessItemStatus.NotApplicable
                    )
                End Sub

            AddHandler btnReset.Click,
                Sub(sender, e)
                    SetSelectedStatus(
                        ReadinessItemStatus.Unresolved
                    )
                End Sub

            AddHandler btnNotes.Click,
                AddressOf EditSelectedNotes

            itemButtons.Controls.Add(btnComplete)
            itemButtons.Controls.Add(btnNotApplicable)
            itemButtons.Controls.Add(btnReset)
            itemButtons.Controls.Add(btnNotes)

            Dim footer As New FlowLayoutPanel With {
                .Dock = DockStyle.Fill,
                .AutoSize = True,
                .FlowDirection = FlowDirection.RightToLeft,
                .WrapContents = False,
                .Padding = New Padding(0, 12, 0, 0)
            }

            Dim btnSave As New Button With {
                .Text = "Save & Close",
                .AutoSize = True,
                .Height = 38
            }

            Dim btnCancel As New Button With {
                .Text = "Cancel",
                .AutoSize = True,
                .Height = 38,
                .DialogResult = DialogResult.Cancel
            }

            AddHandler btnSave.Click,
                AddressOf SaveReadiness

            footer.Controls.Add(btnSave)
            footer.Controls.Add(btnCancel)

            root.Controls.Add(intro, 0, 0)
            root.Controls.Add(profileBar, 0, 1)
            root.Controls.Add(lblSummary, 0, 2)
            root.Controls.Add(itemArea, 0, 3)
            root.Controls.Add(itemButtons, 0, 4)
            root.Controls.Add(footer, 0, 5)

            Me.AcceptButton =
                btnSave

            Me.CancelButton =
                btnCancel

            Me.Controls.Add(root)

        End Sub


        Private Sub RefreshProfiles(
            Optional selectedId As Guid? = Nothing
        )

            If Not selectedId.HasValue Then

                Dim currentItem As ProfileListItem =
                    TryCast(
                        cmbProfiles.SelectedItem,
                        ProfileListItem
                    )

                If currentItem IsNot Nothing Then
                    selectedId =
                        currentItem.Id
                End If

            End If

            cmbProfiles.BeginUpdate()

            Try

                cmbProfiles.Items.Clear()

                Dim profiles =
                    _workingManuscript.ReadinessProfiles.
                        Where(
                            Function(item)
                                Return item IsNot Nothing
                            End Function
                        ).
                        OrderByDescending(
                            Function(item)
                                Return (
                                    _workingManuscript.TargetJournalId.HasValue AndAlso
                                    item.JournalId.HasValue AndAlso
                                    item.JournalId.Value =
                                        _workingManuscript.TargetJournalId.Value
                                )
                            End Function
                        ).
                        ThenBy(
                            Function(item)
                                Return item.JournalName
                            End Function,
                            StringComparer.CurrentCultureIgnoreCase
                        ).
                        ToList()

                For Each profile As ManuscriptReadiness In profiles

                    cmbProfiles.Items.Add(
                        New ProfileListItem(
                            profile.Id,
                            FormatProfileName(profile)
                        )
                    )

                Next

            Finally

                cmbProfiles.EndUpdate()

            End Try

            If cmbProfiles.Items.Count > 0 Then

                Dim selectedIndex As Integer =
                    0

                If selectedId.HasValue Then

                    For index As Integer =
                        0 To cmbProfiles.Items.Count - 1

                        Dim item As ProfileListItem =
                            TryCast(
                                cmbProfiles.Items(index),
                                ProfileListItem
                            )

                        If item IsNot Nothing AndAlso
                           item.Id = selectedId.Value Then

                            selectedIndex =
                                index

                            Exit For

                        End If

                    Next

                End If

                cmbProfiles.SelectedIndex =
                    selectedIndex

            Else

                RefreshItems()

            End If

            UpdateProfileButtons()

        End Sub


        Private Function FormatProfileName(
            profile As ManuscriptReadiness
        ) As String

            Dim name As String =
                If(
                    String.IsNullOrWhiteSpace(
                        profile.JournalName
                    ),
                    "(Unnamed journal)",
                    profile.JournalName.Trim()
                )

            If _workingManuscript.TargetJournalId.HasValue AndAlso
               profile.JournalId.HasValue AndAlso
               profile.JournalId.Value =
                   _workingManuscript.TargetJournalId.Value Then

                name &=
                    "  •  Current target"

            End If

            Return name

        End Function


        Private Function GetSelectedProfile() As ManuscriptReadiness

            Dim selected As ProfileListItem =
                TryCast(
                    cmbProfiles.SelectedItem,
                    ProfileListItem
                )

            If selected Is Nothing Then
                Return Nothing
            End If

            Return _workingManuscript.ReadinessProfiles.
                FirstOrDefault(
                    Function(item)
                        Return item IsNot Nothing AndAlso
                            item.Id = selected.Id
                    End Function
                )

        End Function


        Private Sub ProfileSelectionChanged(
            sender As Object,
            e As EventArgs
        )

            RefreshItems()
            UpdateProfileButtons()

        End Sub


        Private Sub RefreshItems(
            Optional selectedItemId As Guid? = Nothing
        )

            _displayedItems.Clear()
            lstItems.Items.Clear()

            Dim profile As ManuscriptReadiness =
                GetSelectedProfile()

            If profile Is Nothing Then

                lblSummary.Text =
                    "No readiness profile yet. Choose New from Journal to begin."

                lblItemDetail.Text =
                    "Readiness profiles are journal-specific snapshots. They do not create submission records."

                UpdateItemButtons()
                Return

            End If

            If profile.Items Is Nothing Then
                profile.Items =
                    New List(Of ReadinessItemState)()
            End If

            For Each item As ReadinessItemState In
                profile.Items.
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

                _displayedItems.Add(item)

                lstItems.Items.Add(
                    FormatReadinessItem(item)
                )

            Next

            Dim summary As ReadinessSummary =
                SubmissionReadinessService.GetSummary(
                    profile
                )

            lblSummary.Text =
                FormatSummary(summary)

            If _displayedItems.Count > 0 Then

                Dim indexToSelect As Integer =
                    0

                If selectedItemId.HasValue Then

                    For index As Integer =
                        0 To _displayedItems.Count - 1

                        If _displayedItems(index).Id =
                           selectedItemId.Value Then

                            indexToSelect =
                                index

                            Exit For

                        End If

                    Next

                End If

                lstItems.SelectedIndex =
                    indexToSelect

            Else

                lblItemDetail.Text =
                    "This journal profile currently has no checklist requirements."

            End If

            UpdateItemButtons()

        End Sub


        Private Function FormatReadinessItem(
            item As ReadinessItemState
        ) As String

            Dim statusText As String

            Select Case item.Status

                Case ReadinessItemStatus.Complete
                    statusText = "✓"

                Case ReadinessItemStatus.NotApplicable
                    statusText = "N/A"

                Case Else
                    statusText = "○"

            End Select

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
                statusText &
                "  " &
                importance &
                "  •  " &
                category &
                "  —  " &
                item.Title
            )

        End Function


        Private Function FormatSummary(
            summary As ReadinessSummary
        ) As String

            Dim stateText As String =
                If(
                    summary.IsReady,
                    "Required items resolved",
                    "Needs attention"
                )

            Return (
                "Required: " &
                summary.RequiredResolved.ToString() &
                "/" &
                summary.RequiredTotal.ToString() &
                " resolved (" &
                summary.RequiredComplete.ToString() &
                " complete, " &
                summary.RequiredNotApplicable.ToString() &
                " N/A)" &
                "   •   Optional: " &
                summary.OptionalResolved.ToString() &
                "/" &
                summary.OptionalTotal.ToString() &
                " resolved" &
                "   •   " &
                stateText
            )

        End Function


        Private Function GetSelectedItem() As ReadinessItemState

            Dim index As Integer =
                lstItems.SelectedIndex

            If index < 0 OrElse
               index >= _displayedItems.Count Then

                Return Nothing

            End If

            Return _displayedItems(index)

        End Function


        Private Sub ItemSelectionChanged(
            sender As Object,
            e As EventArgs
        )

            Dim item As ReadinessItemState =
                GetSelectedItem()

            If item Is Nothing Then

                lblItemDetail.Text =
                    String.Empty

                UpdateItemButtons()
                Return

            End If

            Dim description As String =
                If(
                    String.IsNullOrWhiteSpace(
                        item.Description
                    ),
                    "No additional instructions.",
                    item.Description.Trim()
                )

            Dim notes As String =
                If(
                    String.IsNullOrWhiteSpace(
                        item.UserNotes
                    ),
                    "No manuscript-specific notes.",
                    "Notes: " &
                        item.UserNotes.Trim()
                )

            lblItemDetail.Text =
                description &
                "   •   " &
                notes

            UpdateItemButtons()

        End Sub


        Private Sub UpdateProfileButtons()

            Dim hasProfile As Boolean =
                GetSelectedProfile() IsNot Nothing

            btnRefreshTemplate.Enabled =
                hasProfile

            btnDeleteProfile.Enabled =
                hasProfile

        End Sub


        Private Sub UpdateItemButtons()

            Dim hasItem As Boolean =
                GetSelectedItem() IsNot Nothing

            btnComplete.Enabled =
                hasItem

            btnNotApplicable.Enabled =
                hasItem

            btnReset.Enabled =
                hasItem

            btnNotes.Enabled =
                hasItem

        End Sub


        Private Sub NewProfile(
            sender As Object,
            e As EventArgs
        )

            If _library.Journals Is Nothing OrElse
               _library.Journals.Count = 0 Then

                MessageBox.Show(
                    Me,
                    "The reusable Journal Library is empty. Add a journal there first, then return to Submission Readiness.",
                    "Journal Library Required",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                )

                Return

            End If

            Using dialog As New JournalPickerForm(
                _library
            )

                If dialog.ShowDialog(Me) <>
                   DialogResult.OK OrElse
                   dialog.SelectedJournal Is Nothing Then

                    Return

                End If

                Dim journal As JournalRecord =
                    dialog.SelectedJournal

                Dim existing As ManuscriptReadiness =
                    SubmissionReadinessService.FindProfileForJournal(
                        _workingManuscript,
                        journal.Id
                    )

                Dim profile As ManuscriptReadiness =
                    SubmissionReadinessService.CreateProfileFromJournal(
                        _workingManuscript,
                        journal
                    )

                RefreshProfiles(profile.Id)

                If existing IsNot Nothing Then

                    MessageBox.Show(
                        Me,
                        "A readiness profile for this journal already exists. PaperRoute selected the existing profile instead of creating a duplicate.",
                        "Readiness Profile Already Exists",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information
                    )

                End If

            End Using

        End Sub


        Private Sub RefreshFromTemplate(
            sender As Object,
            e As EventArgs
        )

            Dim profile As ManuscriptReadiness =
                GetSelectedProfile()

            If profile Is Nothing Then
                Return
            End If

            If Not profile.JournalId.HasValue Then

                MessageBox.Show(
                    Me,
                    "This readiness snapshot is no longer linked to a reusable journal, so PaperRoute cannot check it for newly added requirements.",
                    "Reusable Journal Not Linked",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                )

                Return

            End If

            Dim journal As JournalRecord =
                _library.Journals.
                    FirstOrDefault(
                        Function(item)
                            Return item IsNot Nothing AndAlso
                                item.Id = profile.JournalId.Value
                        End Function
                    )

            If journal Is Nothing Then

                MessageBox.Show(
                    Me,
                    "The reusable journal record is no longer available. The existing readiness snapshot remains valid, but it cannot be refreshed from the template.",
                    "Reusable Journal Not Found",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                )

                Return

            End If

            Dim added As Integer =
                SubmissionReadinessService.AddMissingTemplateItems(
                    profile,
                    journal
                )

            RefreshItems()

            If added = 0 Then

                MessageBox.Show(
                    Me,
                    "No new requirements were found. Existing snapshot text and statuses were left unchanged.",
                    "Checklist Is Current",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                )

            Else

                MessageBox.Show(
                    Me,
                    added.ToString() &
                    " new requirement(s) were added from the journal template. Existing readiness items were not rewritten.",
                    "New Requirements Added",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                )

            End If

        End Sub


        Private Sub DeleteProfile(
            sender As Object,
            e As EventArgs
        )

            Dim profile As ManuscriptReadiness =
                GetSelectedProfile()

            If profile Is Nothing Then
                Return
            End If

            Dim result As DialogResult =
                MessageBox.Show(
                    Me,
                    "Delete the readiness profile for '" &
                    profile.JournalName &
                    "'?" &
                    Environment.NewLine &
                    Environment.NewLine &
                    "This removes the manuscript-specific checklist snapshot. It does not change the manuscript stage or delete the reusable journal template.",
                    "Delete Readiness Profile",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning
                )

            If result <>
               DialogResult.Yes Then
                Return
            End If

            Try

                SubmissionReadinessService.RemoveProfile(
                    _workingManuscript,
                    profile.Id
                )

            Catch ex As InvalidOperationException

                MessageBox.Show(
                    Me,
                    ex.Message,
                    "Readiness Profile Is In Use",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                )

                Return

            End Try

            RefreshProfiles()

        End Sub


        Private Sub SetSelectedStatus(
            status As ReadinessItemStatus
        )

            Dim item As ReadinessItemState =
                GetSelectedItem()

            If item Is Nothing Then
                Return
            End If

            SubmissionReadinessService.SetStatus(
                item,
                status
            )

            RefreshItems(item.Id)

        End Sub


        Private Sub EditSelectedNotes(
            sender As Object,
            e As EventArgs
        )

            Dim item As ReadinessItemState =
                GetSelectedItem()

            If item Is Nothing Then
                Return
            End If

            Using dialog As New ReadinessItemNotesForm(
                item
            )

                If dialog.ShowDialog(Me) <>
                   DialogResult.OK Then

                    Return

                End If

                SubmissionReadinessService.SetItemNotes(
                    item,
                    dialog.Notes
                )

                RefreshItems(item.Id)

            End Using

        End Sub


        Private Sub SaveReadiness(
            sender As Object,
            e As EventArgs
        )

            Dim committed As Manuscript =
                ManuscriptCloneService.CloneManuscript(
                    _workingManuscript
                )

            _sourceManuscript.ReadinessProfiles =
                committed.ReadinessProfiles

            Me.DialogResult =
                DialogResult.OK

        End Sub


        Private Class ProfileListItem

            Public ReadOnly Property Id As Guid

            Private ReadOnly _display As String


            Public Sub New(
                id As Guid,
                display As String
            )

                Me.Id =
                    id

                _display =
                    display

            End Sub


            Public Overrides Function ToString() As String

                Return _display

            End Function

        End Class

    End Class

End Namespace
