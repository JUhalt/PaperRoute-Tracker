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

        Private ReadOnly _repository As AuthorLibraryRepository
        Private ReadOnly _factsSource As IJournalFactsSource
        Private ReadOnly _manuscripts As List(Of Manuscript)

        Private _library As AuthorLibraryData

        Private ReadOnly lstJournals As New ListBox()
        Private ReadOnly lblInfo As New Label()
        Private ReadOnly factsCard As New JournalFactsCard()

        ' Runs Look Up Facts on a copy and returns it changed, or Nothing;
        ' tests replace it.
        Friend lookupPrompt As Func(Of JournalRecord, JournalRecord) = Nothing


        Public Sub New(
            Optional manuscripts As IEnumerable(Of Manuscript) = Nothing
        )

            Me.New(manuscripts, New AuthorLibraryRepository(), Nothing)

        End Sub


        ' For tests: a library in its own folder, and recorded index answers.
        Friend Sub New(
            manuscripts As IEnumerable(Of Manuscript),
            repository As AuthorLibraryRepository,
            factsSource As IJournalFactsSource
        )

            _repository = repository
            _factsSource = factsSource

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

            lstJournals.IntegralHeight =
                False

            ' The list beside the selected journal's facts (#87).
            factsCard.Dock = DockStyle.Fill
            factsCard.Margin = New Padding(12, 0, 0, 0)
            AddHandler factsCard.LookUpRequested, AddressOf LookUpFacts
            AddHandler factsCard.EditRequested, AddressOf EditSelected

            Dim split As New TableLayoutPanel With {
                .Dock = DockStyle.Fill,
                .ColumnCount = 2,
                .RowCount = 1,
                .Margin = New Padding(0)
            }

            split.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 38))
            split.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 62))
            split.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
            split.Controls.Add(lstJournals, 0, 0)
            split.Controls.Add(factsCard, 1, 0)

            AddHandler lstJournals.SelectedIndexChanged,
                AddressOf SelectionChanged

            AddHandler lstJournals.DoubleClick,
                AddressOf EditSelected

            ' One wrapping row: a wrapping panel nested in an auto-sized table
            ' reserves the height of its narrowest layout, leaving a gap.
            Dim leftButtons As New FlowLayoutPanel With {
                .Dock = DockStyle.Fill,
                .AutoSize = True,
                .AutoSizeMode = AutoSizeMode.GrowAndShrink,
                .FlowDirection = FlowDirection.LeftToRight,
                .WrapContents = True,
                .Padding = New Padding(0, 10, 0, 0)
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

            leftButtons.Controls.Add(btnClose)

            root.Controls.Add(intro, 0, 0)
            root.Controls.Add(lblInfo, 0, 1)
            root.Controls.Add(split, 0, 2)
            root.Controls.Add(leftButtons, 0, 3)

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

            lblInfo.Text =
                _library.Journals.Count.ToString() &
                If(_library.Journals.Count = 1, " reusable journal.", " reusable journals.")

            If selected Is Nothing Then

                factsCard.ShowNothing(
                    If(_library.Journals.Count = 0,
                       "Add a journal to keep its links, checklist, and facts in one place.",
                       "Select a journal to see its facts, links, and metrics."))

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
                "Target of " & targetCount.ToString() & If(targetCount = 1, " manuscript", " manuscripts")
            }

            parts.Add(RouteAnalyticsService.DescribeHistory(history))

            Dim blocked As OnlineBlockReason? = OnlineAccess.BlockReason(OnlineServiceCatalog.JournalFacts)

            factsCard.ShowJournal(
                selected,
                String.Join(" · ", parts),
                If(blocked.HasValue,
                   OnlineAccess.BlockedMessage(OnlineServiceCatalog.Find(OnlineServiceCatalog.JournalFacts), blocked.Value),
                   String.Empty))

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


        ' Look Up Facts (#87) on a copy of the selected journal; the library
        ' changes only when the dialog's Save is chosen and the file is saved.
        Private Sub LookUpFacts(
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

            If ExampleLibraryService.IsActive AndAlso
               selected.Facts.Any(Function(item) item.Source = JournalFactCatalog.ExampleSource) Then

                MessageBox.Show(
                    Me,
                    "Fictional journals aren't in any index. Add a real journal to try this.",
                    "Look Up Journal Facts",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                )

                Return

            End If

            Dim blocked As OnlineBlockReason? = OnlineAccess.BlockReason(OnlineServiceCatalog.JournalFacts)
            If blocked.HasValue Then

                MessageBox.Show(
                    Me,
                    OnlineAccess.BlockedMessage(OnlineServiceCatalog.Find(OnlineServiceCatalog.JournalFacts), blocked.Value),
                    "Look Up Journal Facts",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                )

                SelectionChanged(Nothing, EventArgs.Empty)
                Return

            End If

            Dim updated As JournalRecord

            If lookupPrompt IsNot Nothing Then
                updated = lookupPrompt(JournalFactsService.Clone(selected))
            Else
                Using dialog As New JournalFactsForm(JournalFactsService.Clone(selected), If(_factsSource, New OnlineJournalFactsSource()))
                    updated = If(dialog.ShowDialog(Me) = DialogResult.OK, dialog.Result, Nothing)
                End Using
            End If

            If updated Is Nothing Then
                Return
            End If

            Dim index As Integer =
                _library.Journals.FindIndex(
                    Function(item)
                        Return item.Id = selected.Id
                    End Function
                )

            If index < 0 Then
                Return
            End If

            _library.Journals(index) = updated
            SaveAndRefresh()

        End Sub


        ' On failure the library is read back from disk, so nothing unsaved
        ' stays on screen as if it were kept.
        Private Sub SaveAndRefresh()

            Try

                _repository.Save(
                    _library
                )

            Catch ex As Exception When TypeOf ex Is IO.IOException OrElse
                                       TypeOf ex Is UnauthorizedAccessException OrElse
                                       TypeOf ex Is IO.InvalidDataException

                MessageBox.Show(
                    Me,
                    "PaperRoute couldn't save the journal library, so the change wasn't kept." &
                    Environment.NewLine &
                    Environment.NewLine &
                    ex.Message,
                    "Journal Library",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning
                )

            End Try

            Try

                _library =
                    _repository.Load()

            Catch ex As Exception When TypeOf ex Is IO.IOException OrElse
                                       TypeOf ex Is UnauthorizedAccessException OrElse
                                       TypeOf ex Is IO.InvalidDataException
                ' Keep what is on screen; the next save tries again.
            End Try

            RefreshList()

        End Sub


        ' For tests.
        Friend ReadOnly Property Card As JournalFactsCard
            Get
                Return factsCard
            End Get
        End Property

        Friend ReadOnly Property SelectedJournal As JournalRecord
            Get
                Return TryCast(lstJournals.SelectedItem, JournalRecord)
            End Get
        End Property

        Friend Sub SelectJournal(id As Guid)
            For index As Integer = 0 To lstJournals.Items.Count - 1
                If DirectCast(lstJournals.Items(index), JournalRecord).Id = id Then
                    lstJournals.SelectedIndex = index
                    Exit For
                End If
            Next
        End Sub

        Friend Sub LookUpFactsForTest()
            LookUpFacts(Me, EventArgs.Empty)
        End Sub

    End Class

End Namespace
