Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.Linq
Imports System.Windows.Forms
Imports ManuscriptPipeline.Controls
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

Namespace Forms

    Public Class AuthorLibraryForm
        Inherits Form

        Private ReadOnly _repository As New AuthorLibraryRepository()
        Private ReadOnly _manuscriptRepository As New ManuscriptRepository()
        Private ReadOnly _manuscripts As List(Of Manuscript)

        Private _library As AuthorLibraryData

        Private ReadOnly lstAuthors As New ListBox()
        Private ReadOnly lstAffiliations As New ListBox()


        Public Sub New(
            manuscripts As IEnumerable(Of Manuscript)
        )

            If TypeOf manuscripts Is List(Of Manuscript) Then

                ' Form1 passes its live manuscript list. Preserve that list
                ' reference so user-approved ORCID work imports appear on
                ' the board immediately when this dialog closes.
                _manuscripts =
                    DirectCast(
                        manuscripts,
                        List(Of Manuscript)
                    )

            Else

                _manuscripts =
                    If(
                        manuscripts Is Nothing,
                        New List(Of Manuscript)(),
                        manuscripts.ToList()
                    )

            End If

            _library =
                _repository.Load()

            BuildInterface()
            EmptyHint.Attach(lstAuthors, "No reusable authors yet. Add Author creates a person you can add to any manuscript; ORCID... can then read their public profile.")
            EmptyHint.Attach(lstAffiliations, "No affiliations yet. Add Affiliation records an institution once, for every author who shares it.")
            UiPolish.ApplyDialog(Me)
            RefreshLists()

        End Sub


        Private Sub BuildInterface()

            Me.Text =
                "Authors & Affiliations"

            Me.StartPosition =
                FormStartPosition.CenterParent

            Me.Size =
                New Size(
                    900,
                    650
                )

            Me.MinimumSize =
                New Size(
                    760,
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

            Dim intro As New Label With {
                .AutoSize = True,
                .MaximumSize = New Size(820, 0),
                .Text =
                    "Create reusable people and affiliations here. " &
                    "Manuscripts reference these records without changing " &
                    "the original legacy co-author text. ORCID can optionally " &
                    "read public profile metadata after you select an author.",
                .Margin = New Padding(3, 3, 3, 12)
            }

            Dim tabs As New UnderlineTabControl With {
                .Dock = DockStyle.Fill
            }

            tabs.TabPages.Add(
                BuildAuthorsTab()
            )

            tabs.TabPages.Add(
                BuildAffiliationsTab()
            )

            Dim footer As New FlowLayoutPanel With {
                .Dock = DockStyle.Fill,
                .AutoSize = True,
                .FlowDirection = FlowDirection.RightToLeft,
                .WrapContents = False,
                .Padding = New Padding(0, 10, 0, 0)
            }

            Dim btnClose As New Button With {
                .Text = "Close",
                .AutoSize = True,
                .Height = 36,
                .DialogResult = DialogResult.OK
            }

            footer.Controls.Add(
                btnClose
            )

            root.Controls.Add(
                intro,
                0,
                0
            )

            root.Controls.Add(
                tabs,
                0,
                1
            )

            root.Controls.Add(
                footer,
                0,
                2
            )

            Me.AcceptButton =
                btnClose

            Me.Controls.Add(
                root
            )

        End Sub


        Private Function BuildAuthorsTab() As TabPage

            Dim page As New TabPage(
                "Authors"
            )

            Dim layout As New TableLayoutPanel With {
                .Dock = DockStyle.Fill,
                .ColumnCount = 1,
                .RowCount = 2,
                .Padding = New Padding(12)
            }

            layout.RowStyles.Add(
                New RowStyle(
                    SizeType.Percent,
                    100
                )
            )

            layout.RowStyles.Add(
                New RowStyle(
                    SizeType.AutoSize
                )
            )

            lstAuthors.Dock =
                DockStyle.Fill

            AddHandler lstAuthors.DoubleClick,
                AddressOf EditSelectedAuthor

            Dim buttons As New FlowLayoutPanel With {
                .Dock = DockStyle.Fill,
                .AutoSize = True,
                .FlowDirection = FlowDirection.LeftToRight,
                .WrapContents = True,
                .Padding = New Padding(0, 10, 0, 0)
            }

            Dim btnAdd As New Button With {
                .Text = "Add Author",
                .AutoSize = True,
                .Height = 36
            }

            Dim btnEdit As New Button With {
                .Text = "Edit",
                .AutoSize = True,
                .Height = 36
            }

            Dim btnOrcid As New Button With {
                .Text = "ORCID...",
                .AutoSize = True,
                .Height = 36
            }

            Dim btnDelete As New Button With {
                .Text = "Delete",
                .AutoSize = True,
                .Height = 36
            }

            AddHandler btnAdd.Click,
                AddressOf AddAuthor

            AddHandler btnEdit.Click,
                AddressOf EditSelectedAuthor

            AddHandler btnOrcid.Click,
                AddressOf OpenSelectedAuthorOrcid

            AddHandler btnDelete.Click,
                AddressOf DeleteSelectedAuthor

            buttons.Controls.Add(btnAdd)
            buttons.Controls.Add(btnEdit)
            buttons.Controls.Add(btnOrcid)
            buttons.Controls.Add(btnDelete)

            layout.Controls.Add(lstAuthors, 0, 0)
            layout.Controls.Add(buttons, 0, 1)

            page.Controls.Add(layout)

            Return page

        End Function


        Private Function BuildAffiliationsTab() As TabPage

            Dim page As New TabPage(
                "Affiliations"
            )

            Dim layout As New TableLayoutPanel With {
                .Dock = DockStyle.Fill,
                .ColumnCount = 1,
                .RowCount = 2,
                .Padding = New Padding(12)
            }

            layout.RowStyles.Add(
                New RowStyle(
                    SizeType.Percent,
                    100
                )
            )

            layout.RowStyles.Add(
                New RowStyle(
                    SizeType.AutoSize
                )
            )

            lstAffiliations.Dock =
                DockStyle.Fill

            AddHandler lstAffiliations.DoubleClick,
                AddressOf EditSelectedAffiliation

            Dim buttons As New FlowLayoutPanel With {
                .Dock = DockStyle.Fill,
                .AutoSize = True,
                .FlowDirection = FlowDirection.LeftToRight,
                .WrapContents = True,
                .Padding = New Padding(0, 10, 0, 0)
            }

            Dim btnAdd As New Button With {
                .Text = "Add Affiliation",
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

            AddHandler btnAdd.Click,
                AddressOf AddAffiliation

            AddHandler btnEdit.Click,
                AddressOf EditSelectedAffiliation

            AddHandler btnDelete.Click,
                AddressOf DeleteSelectedAffiliation

            buttons.Controls.Add(btnAdd)
            buttons.Controls.Add(btnEdit)
            buttons.Controls.Add(btnDelete)

            layout.Controls.Add(lstAffiliations, 0, 0)
            layout.Controls.Add(buttons, 0, 1)

            page.Controls.Add(layout)

            Return page

        End Function


        Private Sub RefreshLists()

            lstAuthors.Items.Clear()

            For Each author As AuthorRecord In
                _library.Authors.
                    OrderBy(
                        Function(item)
                            Return item.DisplayName
                        End Function,
                        StringComparer.CurrentCultureIgnoreCase
                    )

                lstAuthors.Items.Add(
                    author
                )

            Next

            lstAffiliations.Items.Clear()

            For Each affiliation As AffiliationRecord In
                _library.Affiliations.
                    OrderBy(
                        Function(item)
                            Return item.DisplayName
                        End Function,
                        StringComparer.CurrentCultureIgnoreCase
                    )

                lstAffiliations.Items.Add(
                    affiliation
                )

            Next

        End Sub


        Private Sub AddAuthor(
            sender As Object,
            e As EventArgs
        )

            Using dialog As New AuthorEditForm(
                Nothing
            )

                If dialog.ShowDialog(Me) <>
                   DialogResult.OK OrElse
                   dialog.Result Is Nothing Then

                    Return

                End If

                If dialog.Result.IsMe Then

                    For Each existing As AuthorRecord In
                        _library.Authors

                        existing.IsMe =
                            False

                    Next

                End If

                _library.Authors.Add(
                    dialog.Result
                )

                SaveLibrary()
                RefreshLists()

            End Using

        End Sub


        Private Sub EditSelectedAuthor(
            sender As Object,
            e As EventArgs
        )

            Dim selected As AuthorRecord =
                TryCast(
                    lstAuthors.SelectedItem,
                    AuthorRecord
                )

            If selected Is Nothing Then
                Return
            End If

            Using dialog As New AuthorEditForm(
                selected
            )

                If dialog.ShowDialog(Me) <>
                   DialogResult.OK OrElse
                   dialog.Result Is Nothing Then

                    Return

                End If

                If dialog.Result.IsMe Then

                    For Each existing As AuthorRecord In
                        _library.Authors

                        existing.IsMe =
                            False

                    Next

                End If

                Dim index As Integer =
                    _library.Authors.FindIndex(
                        Function(item)
                            Return item.Id =
                                selected.Id
                        End Function
                    )

                If index >= 0 Then

                    _library.Authors(index) =
                        dialog.Result

                End If

                SaveLibrary()
                RefreshLists()

            End Using

        End Sub


        Private Sub OpenSelectedAuthorOrcid(
            sender As Object,
            e As EventArgs
        )

            Dim selected As AuthorRecord =
                TryCast(
                    lstAuthors.SelectedItem,
                    AuthorRecord
                )

            If selected Is Nothing Then

                MessageBox.Show(
                    Me,
                    "Select an author first.",
                    "ORCID",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                )

                Return

            End If

            Using dialog As New OrcidLookupForm(
                selected
            )

                If dialog.ShowDialog(Me) <>
                   DialogResult.OK OrElse
                   dialog.Suggestion Is Nothing OrElse
                   dialog.Options Is Nothing Then

                    Return

                End If

                Try

                    Dim selectedWorkCount As Integer =
                        dialog.Options.SelectedWorkPutCodes.Count

                    If selectedWorkCount > 0 Then

                        _manuscriptRepository.CreatePreImportBackup()

                    End If

                    Dim result As OrcidApplyResult =
                        OrcidApplyService.Apply(
                            selected,
                            _library,
                            _manuscripts,
                            dialog.Suggestion,
                            dialog.Options
                        )

                    If result.ManuscriptsImported > 0 Then

                        _manuscriptRepository.Save(
                            _manuscripts
                        )

                    End If

                    _repository.SaveKeepingJournals(
                        _library
                    )

                    RefreshLists()

                    Dim message As String =
                        "ORCID changes applied." &
                        Environment.NewLine &
                        Environment.NewLine &
                        "Affiliations added: " &
                        result.AffiliationsAdded.ToString() &
                        Environment.NewLine &
                        "Works imported as Idea manuscripts: " &
                        result.ManuscriptsImported.ToString()

                    If result.DuplicateWorksSkipped > 0 Then

                        message &=
                            Environment.NewLine &
                            "Duplicate works skipped: " &
                            result.DuplicateWorksSkipped.ToString()

                    End If

                    MessageBox.Show(
                        Me,
                        message,
                        "ORCID Import Complete",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information
                    )

                Catch ex As Exception

                    MessageBox.Show(
                        Me,
                        "PaperRoute could not save the selected ORCID changes." &
                        Environment.NewLine &
                        Environment.NewLine &
                        ex.Message,
                        "ORCID Import Error",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error
                    )

                End Try

            End Using

        End Sub


        Private Sub DeleteSelectedAuthor(
            sender As Object,
            e As EventArgs
        )

            Dim selected As AuthorRecord =
                TryCast(
                    lstAuthors.SelectedItem,
                    AuthorRecord
                )

            If selected Is Nothing Then
                Return
            End If

            Dim usageCount As Integer =
                CountAuthorUsage(
                    selected.Id
                )

            If usageCount > 0 Then

                MessageBox.Show(
                    Me,
                    "'" &
                    selected.DisplayName &
                    "' is used by " &
                    usageCount.ToString() &
                    " manuscript(s). Remove the author from those manuscripts before deleting the reusable author record.",
                    "Author Is In Use",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                )

                Return

            End If

            If MessageBox.Show(
                Me,
                "Delete the reusable author '" &
                selected.DisplayName &
                "'?",
                "Delete Author",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning
            ) <> DialogResult.Yes Then

                Return

            End If

            _library.Authors.RemoveAll(
                Function(item)
                    Return item.Id =
                        selected.Id
                End Function
            )

            SaveLibrary()
            RefreshLists()

        End Sub


        Private Sub AddAffiliation(
            sender As Object,
            e As EventArgs
        )

            Using dialog As New AffiliationEditForm(
                Nothing
            )

                If dialog.ShowDialog(Me) <>
                   DialogResult.OK OrElse
                   dialog.Result Is Nothing Then

                    Return

                End If

                _library.Affiliations.Add(
                    dialog.Result
                )

                SaveLibrary()
                RefreshLists()

            End Using

        End Sub


        Private Sub EditSelectedAffiliation(
            sender As Object,
            e As EventArgs
        )

            Dim selected As AffiliationRecord =
                TryCast(
                    lstAffiliations.SelectedItem,
                    AffiliationRecord
                )

            If selected Is Nothing Then
                Return
            End If

            Using dialog As New AffiliationEditForm(
                selected
            )

                If dialog.ShowDialog(Me) <>
                   DialogResult.OK OrElse
                   dialog.Result Is Nothing Then

                    Return

                End If

                Dim index As Integer =
                    _library.Affiliations.FindIndex(
                        Function(item)
                            Return item.Id =
                                selected.Id
                        End Function
                    )

                If index >= 0 Then

                    _library.Affiliations(index) =
                        dialog.Result

                End If

                SaveLibrary()
                RefreshLists()

            End Using

        End Sub


        Private Sub DeleteSelectedAffiliation(
            sender As Object,
            e As EventArgs
        )

            Dim selected As AffiliationRecord =
                TryCast(
                    lstAffiliations.SelectedItem,
                    AffiliationRecord
                )

            If selected Is Nothing Then
                Return
            End If

            Dim usageCount As Integer =
                CountAffiliationUsage(
                    selected.Id
                )

            If usageCount > 0 Then

                MessageBox.Show(
                    Me,
                    "'" &
                    selected.DisplayName &
                    "' is used by " &
                    usageCount.ToString() &
                    " manuscript author(s). Remove that affiliation assignment before deleting it.",
                    "Affiliation Is In Use",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                )

                Return

            End If

            If MessageBox.Show(
                Me,
                "Delete the reusable affiliation '" &
                selected.DisplayName &
                "'?",
                "Delete Affiliation",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning
            ) <> DialogResult.Yes Then

                Return

            End If

            _library.Affiliations.RemoveAll(
                Function(item)
                    Return item.Id =
                        selected.Id
                End Function
            )

            SaveLibrary()
            RefreshLists()

        End Sub


        Private Function CountAuthorUsage(
            authorId As Guid
        ) As Integer

            Dim count As Integer =
                0

            For Each manuscript As Manuscript In
                _manuscripts

                If manuscript.Authors Is Nothing Then
                    Continue For
                End If

                If manuscript.Authors.Any(
                    Function(item)
                        Return item IsNot Nothing AndAlso
                            item.AuthorId = authorId
                    End Function
                ) Then

                    count += 1

                End If

            Next

            Return count

        End Function


        Private Function CountAffiliationUsage(
            affiliationId As Guid
        ) As Integer

            Dim count As Integer =
                0

            For Each manuscript As Manuscript In
                _manuscripts

                If manuscript.Authors Is Nothing Then
                    Continue For
                End If

                For Each authorLink As ManuscriptAuthor In
                    manuscript.Authors

                    If authorLink Is Nothing OrElse
                       authorLink.AffiliationIds Is Nothing Then

                        Continue For

                    End If

                    If authorLink.AffiliationIds.Contains(
                        affiliationId
                    ) Then

                        count += 1

                    End If

                Next

            Next

            Return count

        End Function


        Private Sub SaveLibrary()

            Try

                _repository.SaveKeepingJournals(
                    _library
                )

            Catch ex As Exception

                MessageBox.Show(
                    Me,
                    "PaperRoute could not save the reusable author library." &
                    Environment.NewLine &
                    Environment.NewLine &
                    ex.Message,
                    "Author Library Save Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                )

            End Try

        End Sub

    End Class

End Namespace
