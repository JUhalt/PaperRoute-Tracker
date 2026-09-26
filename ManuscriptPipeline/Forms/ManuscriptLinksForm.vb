Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.Linq
Imports System.Windows.Forms
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

Namespace Forms

    Public Class ManuscriptLinksForm
        Inherits Form

        Private ReadOnly _manuscript As Manuscript
        Private ReadOnly _allManuscripts As List(Of Manuscript)
        Private ReadOnly _repository As New AuthorLibraryRepository()

        Private _library As AuthorLibraryData
        Private _selectedJournalId As Guid?
        Private _workingLinks As New List(Of ManuscriptExternalLink)()

        Private ReadOnly txtTargetJournal As New TextBox()
        Private ReadOnly lblLinkedJournal As New Label()
        Private ReadOnly txtManuscriptUrl As New TextBox()
        Private ReadOnly txtPreprintDoi As New TextBox()
        Private ReadOnly txtPreprintUrl As New TextBox()
        Private ReadOnly lstLinks As New ListBox()


        Public Sub New(
            manuscript As Manuscript,
            Optional allManuscripts As IEnumerable(Of Manuscript) = Nothing
        )

            If manuscript Is Nothing Then
                Throw New ArgumentNullException(NameOf(manuscript))
            End If

            _manuscript = manuscript

            _allManuscripts =
                If(
                    allManuscripts,
                    Enumerable.Empty(Of Manuscript)()
                ).
                Where(
                    Function(item)
                        Return item IsNot Nothing
                    End Function
                ).
                ToList()

            Dim currentIndex As Integer =
                _allManuscripts.FindIndex(
                    Function(item)
                        Return item.Id =
                            manuscript.Id
                    End Function
                )

            If currentIndex >= 0 Then

                _allManuscripts(currentIndex) =
                    manuscript

            Else

                _allManuscripts.Add(
                    manuscript
                )

            End If

            _library =
                _repository.Load()

            _selectedJournalId =
                manuscript.TargetJournalId

            If manuscript.RelatedLinks IsNot Nothing Then

                For Each item As ManuscriptExternalLink In
                    manuscript.RelatedLinks

                    If item Is Nothing Then
                        Continue For
                    End If

                    _workingLinks.Add(
                        CloneLink(
                            item
                        )
                    )

                Next

            End If

            BuildInterface()
            UiPolish.ApplyDialog(Me)
            LoadValues()
            RefreshLinks()
            RefreshJournalStatus()

        End Sub


        Private Sub BuildInterface()

            Me.Text =
                "Journal, Preprint & Project Links"

            Me.StartPosition =
                FormStartPosition.CenterParent

            Me.Size =
                New Size(
                    900,
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

            Dim scrollHost As New Panel With {
                .Dock = DockStyle.Fill,
                .AutoScroll = True,
                .Padding = New Padding(0)
            }

            Dim root As New TableLayoutPanel With {
                .Dock = DockStyle.Top,
                .AutoSize = True,
                .AutoSizeMode = AutoSizeMode.GrowAndShrink,
                .ColumnCount = 1,
                .RowCount = 4,
                .Padding = New Padding(18)
            }

            root.ColumnStyles.Add(
                New ColumnStyle(
                    SizeType.Percent,
                    100
                )
            )

            root.RowStyles.Add(New RowStyle(SizeType.AutoSize))
            root.RowStyles.Add(New RowStyle(SizeType.AutoSize))
            root.RowStyles.Add(New RowStyle(SizeType.Absolute, 220))
            root.RowStyles.Add(New RowStyle(SizeType.AutoSize))

            Dim journalGroup As New GroupBox With {
                .Text = "Target Journal",
                .Dock = DockStyle.Top,
                .AutoSize = True,
                .AutoSizeMode = AutoSizeMode.GrowAndShrink,
                .Padding = New Padding(14),
                .Margin = New Padding(3, 3, 3, 8)
            }

            Dim journalLayout As New TableLayoutPanel With {
                .Dock = DockStyle.Top,
                .AutoSize = True,
                .ColumnCount = 2,
                .RowCount = 4
            }

            journalLayout.ColumnStyles.Add(
                New ColumnStyle(
                    SizeType.Absolute,
                    150
                )
            )

            journalLayout.ColumnStyles.Add(
                New ColumnStyle(
                    SizeType.Percent,
                    100
                )
            )

            For journalRowIndex As Integer = 0 To 3

                journalLayout.RowStyles.Add(
                    New RowStyle(
                        SizeType.AutoSize
                    )
                )

            Next

            txtTargetJournal.Dock =
                DockStyle.Fill

            txtManuscriptUrl.Dock =
                DockStyle.Fill

            lblLinkedJournal.AutoSize =
                True

            lblLinkedJournal.ForeColor =
                SystemColors.GrayText

            lblLinkedJournal.Anchor =
                AnchorStyles.Left

            Dim journalButtons As New FlowLayoutPanel With {
                .Dock = DockStyle.Fill,
                .AutoSize = True,
                .FlowDirection = FlowDirection.LeftToRight,
                .WrapContents = True,
                .Margin = New Padding(0, 6, 0, 0)
            }

            Dim btnChoose As New Button With {
                .Text = "Choose from Library...",
                .AutoSize = True
            }

            Dim btnManage As New Button With {
                .Text = "Manage Journals...",
                .AutoSize = True
            }

            Dim btnClearLink As New Button With {
                .Text = "Clear Library Link",
                .AutoSize = True
            }

            Dim btnHomepage As New Button With {
                .Text = "Open Homepage",
                .AutoSize = True
            }

            Dim btnPortal As New Button With {
                .Text = "Open Portal",
                .AutoSize = True
            }

            Dim btnManuscriptUrl As New Button With {
                .Text = "Open Manuscript URL",
                .AutoSize = True
            }

            AddHandler btnChoose.Click,
                AddressOf ChooseJournal

            AddHandler btnManage.Click,
                AddressOf ManageJournals

            AddHandler btnClearLink.Click,
                AddressOf ClearJournalLink

            AddHandler btnHomepage.Click,
                Sub(sender, e)
                    OpenSelectedJournalUrl(
                        homepage:=True
                    )
                End Sub

            AddHandler btnPortal.Click,
                Sub(sender, e)
                    OpenSelectedJournalUrl(
                        homepage:=False
                    )
                End Sub

            AddHandler btnManuscriptUrl.Click,
                AddressOf OpenManuscriptUrl

            journalButtons.Controls.Add(btnChoose)
            journalButtons.Controls.Add(btnManage)
            journalButtons.Controls.Add(btnClearLink)
            journalButtons.Controls.Add(btnHomepage)
            journalButtons.Controls.Add(btnPortal)
            journalButtons.Controls.Add(btnManuscriptUrl)

            journalLayout.Controls.Add(CreateLabel("Journal"), 0, 0)
            journalLayout.Controls.Add(txtTargetJournal, 1, 0)

            journalLayout.Controls.Add(CreateLabel("Library link"), 0, 1)
            journalLayout.Controls.Add(lblLinkedJournal, 1, 1)

            journalLayout.Controls.Add(CreateLabel("Manuscript URL"), 0, 2)
            journalLayout.Controls.Add(txtManuscriptUrl, 1, 2)

            journalLayout.Controls.Add(journalButtons, 0, 3)
            journalLayout.SetColumnSpan(journalButtons, 2)

            journalGroup.Controls.Add(journalLayout)

            Dim preprintGroup As New GroupBox With {
                .Text = "Preprint",
                .Dock = DockStyle.Top,
                .AutoSize = True,
                .AutoSizeMode = AutoSizeMode.GrowAndShrink,
                .Padding = New Padding(14),
                .Margin = New Padding(3, 8, 3, 8)
            }

            Dim preprintLayout As New TableLayoutPanel With {
                .Dock = DockStyle.Top,
                .AutoSize = True,
                .ColumnCount = 2,
                .RowCount = 3
            }

            preprintLayout.ColumnStyles.Add(
                New ColumnStyle(
                    SizeType.Absolute,
                    150
                )
            )

            preprintLayout.ColumnStyles.Add(
                New ColumnStyle(
                    SizeType.Percent,
                    100
                )
            )

            For preprintRowIndex As Integer = 0 To 2

                preprintLayout.RowStyles.Add(
                    New RowStyle(
                        SizeType.AutoSize
                    )
                )

            Next

            txtPreprintDoi.Dock = DockStyle.Fill
            txtPreprintUrl.Dock = DockStyle.Fill

            Dim preprintButtons As New FlowLayoutPanel With {
                .Dock = DockStyle.Fill,
                .AutoSize = True,
                .FlowDirection = FlowDirection.LeftToRight,
                .WrapContents = True,
                .Margin = New Padding(0, 6, 0, 0)
            }

            Dim btnOpenPreprint As New Button With {
                .Text = "Open Preprint",
                .AutoSize = True
            }

            AddHandler btnOpenPreprint.Click,
                AddressOf OpenPreprint

            preprintButtons.Controls.Add(btnOpenPreprint)

            preprintLayout.Controls.Add(CreateLabel("Preprint DOI"), 0, 0)
            preprintLayout.Controls.Add(txtPreprintDoi, 1, 0)

            preprintLayout.Controls.Add(CreateLabel("Preprint URL"), 0, 1)
            preprintLayout.Controls.Add(txtPreprintUrl, 1, 1)

            preprintLayout.Controls.Add(preprintButtons, 0, 2)
            preprintLayout.SetColumnSpan(preprintButtons, 2)

            preprintGroup.Controls.Add(preprintLayout)

            Dim linksGroup As New GroupBox With {
                .Text = "Related Web Links",
                .Dock = DockStyle.Fill,
                .Padding = New Padding(14),
                .Margin = New Padding(3, 8, 3, 8),
                .MinimumSize = New Size(0, 200)
            }

            Dim linksLayout As New TableLayoutPanel With {
                .Dock = DockStyle.Fill,
                .ColumnCount = 1,
                .RowCount = 2
            }

            linksLayout.RowStyles.Add(
                New RowStyle(
                    SizeType.Percent,
                    100
                )
            )

            linksLayout.RowStyles.Add(
                New RowStyle(
                    SizeType.AutoSize
                )
            )

            lstLinks.Dock = DockStyle.Fill

            AddHandler lstLinks.DoubleClick,
                AddressOf EditLink

            Dim linkButtons As New FlowLayoutPanel With {
                .Dock = DockStyle.Fill,
                .AutoSize = True,
                .FlowDirection = FlowDirection.LeftToRight,
                .WrapContents = True,
                .Padding = New Padding(0, 8, 0, 0)
            }

            Dim btnAddLink As New Button With {
                .Text = "Add Link",
                .AutoSize = True
            }

            Dim btnEditLink As New Button With {
                .Text = "Edit",
                .AutoSize = True
            }

            Dim btnDeleteLink As New Button With {
                .Text = "Delete",
                .AutoSize = True
            }

            Dim btnOpenLink As New Button With {
                .Text = "Open",
                .AutoSize = True
            }

            AddHandler btnAddLink.Click,
                AddressOf AddLink

            AddHandler btnEditLink.Click,
                AddressOf EditLink

            AddHandler btnDeleteLink.Click,
                AddressOf DeleteLink

            AddHandler btnOpenLink.Click,
                AddressOf OpenLink

            linkButtons.Controls.Add(btnAddLink)
            linkButtons.Controls.Add(btnEditLink)
            linkButtons.Controls.Add(btnDeleteLink)
            linkButtons.Controls.Add(btnOpenLink)

            linksLayout.Controls.Add(lstLinks, 0, 0)
            linksLayout.Controls.Add(linkButtons, 0, 1)

            linksGroup.Controls.Add(linksLayout)

            Dim footer As New FlowLayoutPanel With {
                .Dock = DockStyle.Fill,
                .AutoSize = True,
                .FlowDirection = FlowDirection.RightToLeft,
                .WrapContents = False,
                .Padding = New Padding(0, 10, 0, 0)
            }

            Dim btnSave As New Button With {
                .Text = "Save && Close",
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
                AddressOf SaveChanges

            footer.Controls.Add(btnSave)
            footer.Controls.Add(btnCancel)

            root.Controls.Add(journalGroup, 0, 0)
            root.Controls.Add(preprintGroup, 0, 1)
            root.Controls.Add(linksGroup, 0, 2)
            root.Controls.Add(footer, 0, 3)

            Me.AcceptButton = btnSave
            Me.CancelButton = btnCancel

            scrollHost.Controls.Add(
                root
            )

            Me.Controls.Add(
                scrollHost
            )

        End Sub


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
                    8,
                    3,
                    8
                )
            }

        End Function


        Private Sub LoadValues()

            Dim linkedJournal As JournalRecord =
                FindSelectedJournal()

            If linkedJournal IsNot Nothing Then

                txtTargetJournal.Text =
                    linkedJournal.Name

            Else

                txtTargetJournal.Text =
                    _manuscript.TargetJournal

            End If

            txtManuscriptUrl.Text =
                _manuscript.ManuscriptUrl

            If _manuscript.Metadata Is Nothing Then
                _manuscript.Metadata = New ManuscriptMetadata()
            End If

            txtPreprintDoi.Text =
                _manuscript.Metadata.PreprintDoi

            txtPreprintUrl.Text =
                _manuscript.Metadata.PreprintUrl

        End Sub


        Private Sub RefreshJournalStatus()

            If Not _selectedJournalId.HasValue Then

                lblLinkedJournal.Text =
                    "Not linked to the reusable journal library."

                Return

            End If

            Dim selected As JournalRecord =
                FindSelectedJournal()

            If selected Is Nothing Then

                lblLinkedJournal.Text =
                    "The linked journal record is no longer available."

                Return

            End If

            lblLinkedJournal.Text =
                selected.Name &
                If(
                    String.IsNullOrWhiteSpace(
                        selected.Publisher
                    ),
                    String.Empty,
                    " — " &
                    selected.Publisher
                )

        End Sub


        Private Function FindSelectedJournal() As JournalRecord

            If Not _selectedJournalId.HasValue Then
                Return Nothing
            End If

            Return _library.Journals.
                FirstOrDefault(
                    Function(item)
                        Return item IsNot Nothing AndAlso
                            item.Id = _selectedJournalId.Value
                    End Function
                )

        End Function


        Private Sub ChooseJournal(
            sender As Object,
            e As EventArgs
        )

            If _library.Journals.Count = 0 Then

                ManageJournals(
                    sender,
                    e
                )

                If _library.Journals.Count = 0 Then
                    Return
                End If

            End If

            Using dialog As New JournalPickerForm(
                _library
            )

                If dialog.ShowDialog(Me) <>
                   DialogResult.OK OrElse
                   dialog.SelectedJournal Is Nothing Then

                    Return

                End If

                _selectedJournalId =
                    dialog.SelectedJournal.Id

                txtTargetJournal.Text =
                    dialog.SelectedJournal.Name

                RefreshJournalStatus()

            End Using

        End Sub


        Private Sub ManageJournals(
            sender As Object,
            e As EventArgs
        )

            Using dialog As New JournalLibraryForm(
                _allManuscripts
            )

                dialog.ShowDialog(
                    Me
                )

            End Using

            _library =
                _repository.Load()

            If _selectedJournalId.HasValue AndAlso
               FindSelectedJournal() Is Nothing Then

                _selectedJournalId =
                    Nothing

            End If

            RefreshJournalStatus()

        End Sub


        Private Sub ClearJournalLink(
            sender As Object,
            e As EventArgs
        )

            _selectedJournalId =
                Nothing

            RefreshJournalStatus()

        End Sub


        Private Sub OpenSelectedJournalUrl(
            homepage As Boolean
        )

            Dim selected As JournalRecord =
                FindSelectedJournal()

            If selected Is Nothing Then

                MessageBox.Show(
                    Me,
                    "Link this manuscript to a reusable journal first.",
                    "No Linked Journal",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                )

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
                        "This journal has no homepage URL saved.",
                        "This journal has no submission portal URL saved."
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


        Private Sub OpenManuscriptUrl(
            sender As Object,
            e As EventArgs
        )

            If String.IsNullOrWhiteSpace(
                txtManuscriptUrl.Text
            ) Then

                MessageBox.Show(
                    Me,
                    "Save a manuscript-specific URL first.",
                    "No Manuscript URL",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                )

                Return

            End If

            Try

                UrlSafetyService.OpenInBrowser(
                    txtManuscriptUrl.Text
                )

            Catch ex As Exception

                MessageBox.Show(
                    Me,
                    ex.Message,
                    "Open Manuscript URL",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                )

            End Try

        End Sub


        Private Sub OpenPreprint(
            sender As Object,
            e As EventArgs
        )

            Dim url As String =
                txtPreprintUrl.Text.Trim()

            If String.IsNullOrWhiteSpace(url) Then

                Dim doi As String =
                    DoiNormalizer.Normalize(
                        txtPreprintDoi.Text
                    )

                If DoiNormalizer.IsValid(
                    doi
                ) Then

                    url =
                        "https://doi.org/" &
                        doi

                End If

            End If

            If String.IsNullOrWhiteSpace(url) Then

                MessageBox.Show(
                    Me,
                    "Save a preprint URL or DOI first.",
                    "No Preprint Link",
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
                    "Open Preprint",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                )

            End Try

        End Sub


        Private Sub RefreshLinks()

            lstLinks.BeginUpdate()

            Try

                lstLinks.Items.Clear()

                For Each item As ManuscriptExternalLink In
                    _workingLinks

                    lstLinks.Items.Add(
                        item
                    )

                Next

            Finally

                lstLinks.EndUpdate()

            End Try

        End Sub


        Private Sub AddLink(
            sender As Object,
            e As EventArgs
        )

            Using dialog As New ExternalLinkEditForm(
                Nothing
            )

                If dialog.ShowDialog(Me) <>
                   DialogResult.OK OrElse
                   dialog.Result Is Nothing Then

                    Return

                End If

                _workingLinks.Add(
                    dialog.Result
                )

                RefreshLinks()

                lstLinks.SelectedIndex =
                    lstLinks.Items.Count - 1

            End Using

        End Sub


        Private Sub EditLink(
            sender As Object,
            e As EventArgs
        )

            Dim selected As ManuscriptExternalLink =
                TryCast(
                    lstLinks.SelectedItem,
                    ManuscriptExternalLink
                )

            If selected Is Nothing Then
                Return
            End If

            Using dialog As New ExternalLinkEditForm(
                selected
            )

                If dialog.ShowDialog(Me) <>
                   DialogResult.OK OrElse
                   dialog.Result Is Nothing Then

                    Return

                End If

                Dim index As Integer =
                    _workingLinks.FindIndex(
                        Function(item)
                            Return item.Id =
                                selected.Id
                        End Function
                    )

                If index >= 0 Then

                    _workingLinks(index) =
                        dialog.Result

                End If

                RefreshLinks()

                If index >= 0 AndAlso
                   index < lstLinks.Items.Count Then

                    lstLinks.SelectedIndex =
                        index

                End If

            End Using

        End Sub


        Private Sub DeleteLink(
            sender As Object,
            e As EventArgs
        )

            Dim selected As ManuscriptExternalLink =
                TryCast(
                    lstLinks.SelectedItem,
                    ManuscriptExternalLink
                )

            If selected Is Nothing Then
                Return
            End If

            _workingLinks.RemoveAll(
                Function(item)
                    Return item.Id =
                        selected.Id
                End Function
            )

            RefreshLinks()

        End Sub


        Private Sub OpenLink(
            sender As Object,
            e As EventArgs
        )

            Dim selected As ManuscriptExternalLink =
                TryCast(
                    lstLinks.SelectedItem,
                    ManuscriptExternalLink
                )

            If selected Is Nothing Then
                Return
            End If

            Try

                UrlSafetyService.OpenInBrowser(
                    selected.Url
                )

            Catch ex As Exception

                MessageBox.Show(
                    Me,
                    ex.Message,
                    "Open External Link",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                )

            End Try

        End Sub


        Private Sub SaveChanges(
            sender As Object,
            e As EventArgs
        )

            Dim targetText As String =
                txtTargetJournal.Text.Trim()

            If _selectedJournalId.HasValue Then

                Dim selected As JournalRecord =
                    FindSelectedJournal()

                If selected Is Nothing OrElse
                   Not String.Equals(
                       targetText,
                       selected.Name,
                       StringComparison.CurrentCultureIgnoreCase
                   ) Then

                    _selectedJournalId =
                        Nothing

                End If

            End If

            Dim manuscriptUrl As String

            Try

                manuscriptUrl =
                    UrlSafetyService.NormalizeOptionalHttpUrl(
                        txtManuscriptUrl.Text,
                        "Manuscript URL"
                    )

            Catch ex As ArgumentException

                MessageBox.Show(
                    Me,
                    ex.Message,
                    "Check Manuscript URL",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                )

                txtManuscriptUrl.Focus()
                Return

            End Try

            Dim preprintDoi As String =
                DoiNormalizer.Normalize(
                    txtPreprintDoi.Text
                )

            If Not String.IsNullOrWhiteSpace(
                preprintDoi
            ) AndAlso
               Not DoiNormalizer.IsValid(
                   preprintDoi
               ) Then

                MessageBox.Show(
                    Me,
                    "Enter a valid preprint DOI or leave it blank.",
                    "Check Preprint DOI",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                )

                txtPreprintDoi.Focus()
                Return

            End If

            Dim preprintUrl As String

            Try

                preprintUrl =
                    UrlSafetyService.NormalizeOptionalHttpUrl(
                        txtPreprintUrl.Text,
                        "Preprint URL"
                    )

            Catch ex As ArgumentException

                MessageBox.Show(
                    Me,
                    ex.Message,
                    "Check Preprint URL",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                )

                txtPreprintUrl.Focus()
                Return

            End Try

            _manuscript.TargetJournal =
                targetText

            _manuscript.TargetJournalId =
                _selectedJournalId

            _manuscript.ManuscriptUrl =
                manuscriptUrl

            _manuscript.Metadata.PreprintDoi =
                preprintDoi

            _manuscript.Metadata.PreprintUrl =
                preprintUrl

            _manuscript.RelatedLinks =
                _workingLinks.
                    Select(
                        Function(item)
                            Return CloneLink(
                                item
                            )
                        End Function
                    ).
                    ToList()

            Me.DialogResult =
                DialogResult.OK

        End Sub


        Private Shared Function CloneLink(
            source As ManuscriptExternalLink
        ) As ManuscriptExternalLink

            Return New ManuscriptExternalLink With {
                .Id = source.Id,
                .Label = source.Label,
                .Url = source.Url,
                .Notes = source.Notes
            }

        End Function

    End Class

End Namespace
