Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.Linq
Imports System.Threading.Tasks
Imports System.Windows.Forms
Imports ManuscriptPipeline.Controls
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

Namespace Forms

    Public Class OrcidLookupForm
        Inherits Form

        Private ReadOnly _author As AuthorRecord
        Private ReadOnly _client As New OrcidClient()

        Private ReadOnly txtOrcid As New TextBox()
        Private ReadOnly btnLookup As New Button()
        Private ReadOnly btnOpenProfile As New Button()
        Private ReadOnly lblStatus As New Label()

        Private ReadOnly chkApplyName As New CheckBox()
        Private ReadOnly chkApplyCredit As New CheckBox()
        Private ReadOnly chkAffiliations As New CheckBox()
        Private ReadOnly lblProfileDetails As New Label()

        Private ReadOnly lstWorks As New CheckedListBox()
        Private ReadOnly chkImportPublished As New CheckBox()
        Private ReadOnly btnSelectAll As New Button()
        Private ReadOnly btnSelectNone As New Button()

        Private ReadOnly btnApply As New Button()

        Private _suggestion As OrcidProfileSuggestion
        Private _options As OrcidApplyOptions


        Public ReadOnly Property Suggestion As OrcidProfileSuggestion
            Get
                Return _suggestion
            End Get
        End Property


        Public ReadOnly Property Options As OrcidApplyOptions
            Get
                Return _options
            End Get
        End Property


        Public Sub New(
            author As AuthorRecord
        )

            If author Is Nothing Then
                Throw New ArgumentNullException(NameOf(author))
            End If

            _author =
                author

            BuildInterface()
            UiPolish.ApplyDialog(Me)

            txtOrcid.Text =
                author.Orcid

        End Sub


        Private Sub BuildInterface()

            Me.Text =
                "ORCID Public Profile"

            Me.StartPosition =
                FormStartPosition.CenterParent

            Me.Size =
                New Size(
                    940,
                    720
                )

            Me.MinimumSize =
                New Size(
                    800,
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
                .RowCount = 5,
                .Padding = New Padding(18)
            }

            root.RowStyles.Add(New RowStyle(SizeType.AutoSize))
            root.RowStyles.Add(New RowStyle(SizeType.AutoSize))
            root.RowStyles.Add(New RowStyle(SizeType.AutoSize))
            root.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
            root.RowStyles.Add(New RowStyle(SizeType.AutoSize))

            Dim heading As New Label With {
                .AutoSize = True,
                .Font = New Font(Me.Font.FontFamily, 14.0F, FontStyle.Bold),
                .Text = "Import public ORCID metadata for " & _author.DisplayName,
                .Margin = New Padding(0, 0, 0, 8)
            }

            Dim lookupBar As New TableLayoutPanel With {
                .Dock = DockStyle.Top,
                .AutoSize = True,
                .ColumnCount = 4,
                .Margin = New Padding(0, 0, 0, 8)
            }

            lookupBar.ColumnStyles.Add(New ColumnStyle(SizeType.AutoSize))
            lookupBar.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
            lookupBar.ColumnStyles.Add(New ColumnStyle(SizeType.AutoSize))
            lookupBar.ColumnStyles.Add(New ColumnStyle(SizeType.AutoSize))

            Dim lblOrcid As New Label With {
                .Text = "ORCID iD",
                .AutoSize = True,
                .Anchor = AnchorStyles.Left,
                .Font = New Font(Me.Font, FontStyle.Bold),
                .Margin = New Padding(0, 8, 10, 0)
            }

            txtOrcid.Dock = DockStyle.Fill

            btnLookup.Text = "Look Up"
            btnLookup.AutoSize = True
            btnLookup.Height = 36
            btnLookup.Margin = New Padding(10, 0, 0, 0)

            btnOpenProfile.Text = "Open Profile"
            btnOpenProfile.AutoSize = True
            btnOpenProfile.Height = 36
            btnOpenProfile.Enabled = False
            btnOpenProfile.Margin = New Padding(8, 0, 0, 0)

            AddHandler btnLookup.Click,
                AddressOf LookupOrcidAsync

            AddHandler btnOpenProfile.Click,
                AddressOf OpenProfile

            lookupBar.Controls.Add(lblOrcid, 0, 0)
            lookupBar.Controls.Add(txtOrcid, 1, 0)
            lookupBar.Controls.Add(btnLookup, 2, 0)
            lookupBar.Controls.Add(btnOpenProfile, 3, 0)

            lblStatus.AutoSize = True
            lblStatus.MaximumSize = New Size(860, 0)
            lblStatus.Text =
                "PaperRoute reads ORCID public data only. A successful lookup checks that the iD exists; it does not authenticate ownership and never writes back to ORCID."
            lblStatus.Margin = New Padding(0, 0, 0, 10)

            Dim tabs As New UnderlineTabControl With {
                .Dock = DockStyle.Fill
            }

            tabs.TabPages.Add(
                BuildProfileTab()
            )

            tabs.TabPages.Add(
                BuildWorksTab()
            )

            Dim footer As New FlowLayoutPanel With {
                .Dock = DockStyle.Fill,
                .AutoSize = True,
                .FlowDirection = FlowDirection.RightToLeft,
                .WrapContents = False,
                .Padding = New Padding(0, 10, 0, 0)
            }

            btnApply.Text = "Apply Selected"
            btnApply.AutoSize = True
            btnApply.Height = 38
            btnApply.Enabled = False

            Dim btnCancel As New Button With {
                .Text = "Cancel",
                .AutoSize = True,
                .Height = 38,
                .DialogResult = DialogResult.Cancel
            }

            AddHandler btnApply.Click,
                AddressOf ApplySelected

            footer.Controls.Add(btnApply)
            footer.Controls.Add(btnCancel)

            root.Controls.Add(heading, 0, 0)
            root.Controls.Add(lookupBar, 0, 1)
            root.Controls.Add(lblStatus, 0, 2)
            root.Controls.Add(tabs, 0, 3)
            root.Controls.Add(footer, 0, 4)

            Me.CancelButton =
                btnCancel

            Me.Controls.Add(
                root
            )

        End Sub


        Private Function BuildProfileTab() As TabPage

            Dim page As New TabPage(
                "Profile"
            )

            Dim layout As New TableLayoutPanel With {
                .Dock = DockStyle.Fill,
                .ColumnCount = 1,
                .RowCount = 5,
                .Padding = New Padding(14)
            }

            layout.RowStyles.Add(New RowStyle(SizeType.AutoSize))
            layout.RowStyles.Add(New RowStyle(SizeType.AutoSize))
            layout.RowStyles.Add(New RowStyle(SizeType.AutoSize))
            layout.RowStyles.Add(New RowStyle(SizeType.AutoSize))
            layout.RowStyles.Add(New RowStyle(SizeType.Percent, 100))

            chkApplyName.AutoSize = True
            chkApplyName.Enabled = False

            chkApplyCredit.AutoSize = True
            chkApplyCredit.Enabled = False

            chkAffiliations.AutoSize = True
            chkAffiliations.Enabled = False

            lblProfileDetails.AutoSize = True
            lblProfileDetails.MaximumSize = New Size(820, 0)
            lblProfileDetails.Text =
                "Look up an ORCID iD to preview public profile metadata."
            lblProfileDetails.Margin = New Padding(0, 14, 0, 0)

            layout.Controls.Add(chkApplyName, 0, 0)
            layout.Controls.Add(chkApplyCredit, 0, 1)
            layout.Controls.Add(chkAffiliations, 0, 2)
            layout.Controls.Add(lblProfileDetails, 0, 3)

            page.Controls.Add(
                layout
            )

            Return page

        End Function


        Private Function BuildWorksTab() As TabPage

            Dim page As New TabPage(
                "Works"
            )

            Dim layout As New TableLayoutPanel With {
                .Dock = DockStyle.Fill,
                .ColumnCount = 1,
                .RowCount = 4,
                .Padding = New Padding(14)
            }

            layout.RowStyles.Add(New RowStyle(SizeType.AutoSize))
            layout.RowStyles.Add(New RowStyle(SizeType.AutoSize))
            layout.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
            layout.RowStyles.Add(New RowStyle(SizeType.AutoSize))

            Dim note As New Label With {
                .AutoSize = True,
                .MaximumSize = New Size(820, 0),
                .Text =
                    "Check works to create as new PaperRoute manuscripts. You control whether dated works enter the Published shelf; undated works always enter as Idea.",
                .Margin = New Padding(0, 0, 0, 8)
            }

            chkImportPublished.Text =
                "Import selected works with a publication date as Published"

            chkImportPublished.AutoSize =
                True

            chkImportPublished.Checked =
                True

            chkImportPublished.Margin =
                New Padding(0, 0, 0, 10)

            lstWorks.Dock = DockStyle.Fill
            lstWorks.CheckOnClick = True
            lstWorks.HorizontalScrollbar = True

            Dim controls As New FlowLayoutPanel With {
                .Dock = DockStyle.Fill,
                .AutoSize = True,
                .FlowDirection = FlowDirection.LeftToRight,
                .WrapContents = False,
                .Padding = New Padding(0, 8, 0, 0)
            }

            btnSelectAll.Text = "Select All"
            btnSelectAll.AutoSize = True
            btnSelectAll.Enabled = False

            btnSelectNone.Text = "Select None"
            btnSelectNone.AutoSize = True
            btnSelectNone.Enabled = False

            AddHandler btnSelectAll.Click,
                Sub(sender, e)
                    SetAllWorkChecks(True)
                End Sub

            AddHandler btnSelectNone.Click,
                Sub(sender, e)
                    SetAllWorkChecks(False)
                End Sub

            controls.Controls.Add(btnSelectAll)
            controls.Controls.Add(btnSelectNone)

            layout.Controls.Add(note, 0, 0)
            layout.Controls.Add(chkImportPublished, 0, 1)
            layout.Controls.Add(lstWorks, 0, 2)
            layout.Controls.Add(controls, 0, 3)

            page.Controls.Add(
                layout
            )

            Return page

        End Function


        Private Async Sub LookupOrcidAsync(
            sender As Object,
            e As EventArgs
        )

            Dim normalized As String =
                OrcidIdentifierService.Normalize(
                    txtOrcid.Text
                )

            If Not OrcidIdentifierService.IsValid(
                normalized
            ) Then

                MessageBox.Show(
                    Me,
                    "Enter a valid ORCID iD, such as 0000-0002-1825-0097.",
                    "Check ORCID iD",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                )

                txtOrcid.Focus()
                Return

            End If

            ToggleLookupBusy(
                True
            )

            lblStatus.Text =
                "Reading the public ORCID record..."

            Try

                _suggestion =
                    Await _client.LookupAsync(
                        normalized
                    )

                txtOrcid.Text =
                    _suggestion.Orcid

                PopulatePreview()

                lblStatus.Text =
                    "Public ORCID record found. Review each proposed change before applying it. This is a registry existence check, not record-holder authentication."

                btnApply.Enabled =
                    True

                btnOpenProfile.Enabled =
                    True

            Catch ex As Exception

                _suggestion =
                    Nothing

                btnApply.Enabled =
                    False

                btnOpenProfile.Enabled =
                    False

                MessageBox.Show(
                    Me,
                    OnlineAccess.Describe(ex, "ORCID"),
                    "ORCID Lookup",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                )

                lblStatus.Text =
                    "No ORCID changes have been applied."

            Finally

                ToggleLookupBusy(
                    False
                )

            End Try

        End Sub


        Private Sub PopulatePreview()

            If _suggestion Is Nothing Then
                Return
            End If

            Dim publicName As String =
                _suggestion.PublicName

            chkApplyName.Text =
                If(
                    String.IsNullOrWhiteSpace(publicName),
                    "Public name is not available",
                    "Apply public name: " & publicName
                )

            chkApplyName.Enabled =
                Not String.IsNullOrWhiteSpace(
                    publicName
                )

            chkApplyName.Checked =
                chkApplyName.Enabled AndAlso
                String.IsNullOrWhiteSpace(
                    _author.GivenName
                ) AndAlso
                String.IsNullOrWhiteSpace(
                    _author.FamilyName
                )

            chkApplyCredit.Text =
                If(
                    String.IsNullOrWhiteSpace(
                        _suggestion.CreditName
                    ),
                    "Public credit name is not available",
                    "Apply credit/display name: " &
                        _suggestion.CreditName
                )

            chkApplyCredit.Enabled =
                Not String.IsNullOrWhiteSpace(
                    _suggestion.CreditName
                )

            chkApplyCredit.Checked =
                chkApplyCredit.Enabled AndAlso
                String.IsNullOrWhiteSpace(
                    _author.DisplayNameOverride
                )

            chkAffiliations.Text =
                "Add public employment affiliations to reusable library (" &
                _suggestion.Affiliations.Count.ToString() &
                ")"

            chkAffiliations.Enabled =
                _suggestion.Affiliations.Count > 0

            chkAffiliations.Checked =
                False

            Dim detailLines As New List(Of String) From {
                "ORCID iD: " & _suggestion.Orcid,
                "Public name: " &
                    If(
                        String.IsNullOrWhiteSpace(publicName),
                        "(not public)",
                        publicName
                    ),
                "Credit name: " &
                    If(
                        String.IsNullOrWhiteSpace(
                            _suggestion.CreditName
                        ),
                        "(not public)",
                        _suggestion.CreditName
                    ),
                "Public keywords: " &
                    _suggestion.Keywords.Count.ToString(),
                "Public researcher URLs: " &
                    _suggestion.ResearcherUrls.Count.ToString(),
                "Public employment affiliations: " &
                    _suggestion.Affiliations.Count.ToString(),
                "Candidate works: " &
                    _suggestion.Works.Count.ToString()
            }

            If _author.OrcidLastCheckedUtc.HasValue Then

                detailLines.Add(
                    "PaperRoute last checked this author's ORCID record: " &
                    _author.OrcidLastCheckedUtc.Value.ToLocalTime().ToString(
                        "g"
                    )
                )

            End If

            lblProfileDetails.Text =
                String.Join(
                    Environment.NewLine,
                    detailLines
                )

            lstWorks.Items.Clear()

            For Each work As OrcidWorkSuggestion In
                _suggestion.Works.
                    OrderByDescending(
                        Function(item)
                            Return item.PublishedDate
                        End Function
                    ).
                    ThenBy(
                        Function(item)
                            Return item.Title
                        End Function,
                        StringComparer.CurrentCultureIgnoreCase
                    )

                lstWorks.Items.Add(
                    work,
                    False
                )

            Next

            btnSelectAll.Enabled =
                lstWorks.Items.Count > 0

            btnSelectNone.Enabled =
                lstWorks.Items.Count > 0

        End Sub


        Private Sub SetAllWorkChecks(
            checked As Boolean
        )

            For index As Integer = 0 To lstWorks.Items.Count - 1

                lstWorks.SetItemChecked(
                    index,
                    checked
                )

            Next

        End Sub


        Private Sub ApplySelected(
            sender As Object,
            e As EventArgs
        )

            If _suggestion Is Nothing Then
                Return
            End If

            Dim selectedPutCodes As New List(Of Long)()

            For Each item As Object In
                lstWorks.CheckedItems

                Dim work As OrcidWorkSuggestion =
                    TryCast(
                        item,
                        OrcidWorkSuggestion
                    )

                If work IsNot Nothing Then

                    selectedPutCodes.Add(
                        work.PutCode
                    )

                End If

            Next

            _options =
                New OrcidApplyOptions With {
                    .ApplyName = chkApplyName.Checked,
                    .ApplyCreditName = chkApplyCredit.Checked,
                    .AddAffiliations = chkAffiliations.Checked,
                    .ImportDatedWorksAsPublished = chkImportPublished.Checked,
                    .SelectedWorkPutCodes = selectedPutCodes
                }

            Me.DialogResult =
                DialogResult.OK

        End Sub


        Private Sub OpenProfile(
            sender As Object,
            e As EventArgs
        )

            If _suggestion Is Nothing OrElse
               String.IsNullOrWhiteSpace(
                   _suggestion.Orcid
               ) Then

                Return

            End If

            Try

                UrlSafetyService.OpenInBrowser(
                    "https://orcid.org/" &
                    _suggestion.Orcid
                )

            Catch ex As Exception

                MessageBox.Show(
                    Me,
                    "PaperRoute could not open the ORCID profile." &
                    Environment.NewLine &
                    Environment.NewLine &
                    ex.Message,
                    "Open ORCID Profile",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                )

            End Try

        End Sub


        Private Sub ToggleLookupBusy(
            busy As Boolean
        )

            btnLookup.Enabled =
                Not busy

            txtOrcid.Enabled =
                Not busy

            Me.UseWaitCursor =
                busy

        End Sub

    End Class

End Namespace
