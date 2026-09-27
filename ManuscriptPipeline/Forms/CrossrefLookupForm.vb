Imports System
Imports System.Drawing
Imports System.Linq
Imports System.Text
Imports System.Threading.Tasks
Imports System.Windows.Forms
Imports ManuscriptPipeline.Controls
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

Namespace Forms

    Public Class CrossrefLookupForm
        Inherits Form

        Private ReadOnly _manuscript As Manuscript
        Private ReadOnly _authorLibrary As AuthorLibraryData
        Private ReadOnly _client As New CrossrefClient()

        Private _suggestion As CrossrefMetadataSuggestion

        Private ReadOnly txtDoi As New TextBox()
        Private ReadOnly btnLookup As New Button()
        Private ReadOnly lblStatus As New Label()
        Private ReadOnly txtPreview As New TextBox()

        Private ReadOnly chkDoi As New CheckBox()
        Private ReadOnly chkTitle As New CheckBox()
        Private ReadOnly chkPublication As New CheckBox()
        Private ReadOnly chkAbstractKeywords As New CheckBox()
        Private ReadOnly chkAuthors As New CheckBox()

        Private ReadOnly btnApply As New Button()

        Public Sub New(
            manuscript As Manuscript,
            authorLibrary As AuthorLibraryData
        )

            If manuscript Is Nothing Then
                Throw New ArgumentNullException(NameOf(manuscript))
            End If

            If authorLibrary Is Nothing Then
                Throw New ArgumentNullException(NameOf(authorLibrary))
            End If

            _manuscript =
                manuscript

            _authorLibrary =
                authorLibrary

            BuildInterface()
            LoadCurrentDoi()

            UiPolish.ApplyDialog(
                Me
            )

        End Sub


        Private Sub BuildInterface()

            Me.Text =
                "DOI & Crossref Metadata"

            Me.StartPosition =
                FormStartPosition.CenterParent

            Me.Size =
                New Size(
                    820,
                    720
                )

            Me.MinimumSize =
                New Size(
                    720,
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
                .RowCount = 7,
                .Padding = New Padding(18)
            }

            root.RowStyles.Add(New RowStyle(SizeType.AutoSize))
            root.RowStyles.Add(New RowStyle(SizeType.AutoSize))
            root.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
            root.RowStyles.Add(New RowStyle(SizeType.AutoSize))
            root.RowStyles.Add(New RowStyle(SizeType.AutoSize))
            root.RowStyles.Add(New RowStyle(SizeType.AutoSize))
            root.RowStyles.Add(New RowStyle(SizeType.AutoSize))

            Dim intro As New Label With {
                .Text =
                    "Paste a DOI or doi.org link. PaperRoute will ask Crossref for metadata, show a preview, and only apply the fields you select.",
                .AutoSize = True,
                .MaximumSize = New Size(740, 0),
                .Margin = New Padding(0, 0, 0, 12)
            }

            Dim lookupRow As New TableLayoutPanel With {
                .Dock = DockStyle.Top,
                .AutoSize = True,
                .ColumnCount = 2,
                .Margin = New Padding(0, 0, 0, 10)
            }

            lookupRow.ColumnStyles.Add(
                New ColumnStyle(
                    SizeType.Percent,
                    100
                )
            )

            lookupRow.ColumnStyles.Add(
                New ColumnStyle(
                    SizeType.AutoSize
                )
            )

            txtDoi.Dock =
                DockStyle.Fill

            txtDoi.PlaceholderText =
                "10.xxxx/... or https://doi.org/..."

            btnLookup.Text =
                "Look Up"

            btnLookup.AutoSize =
                True

            btnLookup.Height =
                36

            AddHandler btnLookup.Click,
                AddressOf LookupMetadata

            lookupRow.Controls.Add(
                txtDoi,
                0,
                0
            )

            lookupRow.Controls.Add(
                btnLookup,
                1,
                0
            )

            lblStatus.AutoSize =
                True

            lblStatus.ForeColor =
                SystemColors.GrayText

            lblStatus.Margin =
                New Padding(
                    0,
                    0,
                    0,
                    8
                )

            txtPreview.Multiline =
                True

            txtPreview.ReadOnly =
                True

            txtPreview.ScrollBars =
                ScrollBars.Vertical

            txtPreview.Dock =
                DockStyle.Fill

            txtPreview.Font =
                New Font(
                    "Consolas",
                    9.5F
                )

            Dim applyGroup As New SectionCard With {
                .Text = "Apply selected",
                .Dock = DockStyle.Top,
                .AutoSize = True,
                .AutoSizeMode = AutoSizeMode.GrowAndShrink,
                .Padding = New Padding(12),
                .Margin = New Padding(0, 10, 0, 10)
            }

            Dim options As New FlowLayoutPanel With {
                .Dock = DockStyle.Top,
                .AutoSize = True,
                .AutoSizeMode = AutoSizeMode.GrowAndShrink,
                .FlowDirection = FlowDirection.TopDown,
                .WrapContents = False
            }

            ConfigureOption(
                chkDoi,
                "DOI",
                True
            )

            ConfigureOption(
                chkTitle,
                "Title",
                False
            )

            ConfigureOption(
                chkPublication,
                "Journal and publication details",
                False
            )

            ConfigureOption(
                chkAbstractKeywords,
                "Abstract and keywords",
                False
            )

            ConfigureOption(
                chkAuthors,
                "Add missing structured authors and affiliations",
                False
            )

            options.Controls.Add(
                chkDoi
            )

            options.Controls.Add(
                chkTitle
            )

            options.Controls.Add(
                chkPublication
            )

            options.Controls.Add(
                chkAbstractKeywords
            )

            options.Controls.Add(
                chkAuthors
            )

            applyGroup.Controls.Add(
                options
            )

            Dim caution As New Label With {
                .Text =
                    "PaperRoute will not change manuscript stage, shelf/location, target journal, or existing author order. New Crossref authors are only added when selected.",
                .AutoSize = True,
                .MaximumSize = New Size(740, 0),
                .ForeColor = SystemColors.GrayText,
                .Margin = New Padding(0, 0, 0, 10)
            }

            Dim footer As New FlowLayoutPanel With {
                .Dock = DockStyle.Bottom,
                .AutoSize = True,
                .AutoSizeMode = AutoSizeMode.GrowAndShrink,
                .FlowDirection = FlowDirection.RightToLeft,
                .WrapContents = False
            }

            btnApply.Text =
                "Apply Selected"

            btnApply.AutoSize =
                True

            btnApply.Height =
                38

            btnApply.Enabled =
                False

            Dim btnCancel As New Button With {
                .Text = "Cancel",
                .AutoSize = True,
                .Height = 38,
                .DialogResult = DialogResult.Cancel
            }

            AddHandler btnApply.Click,
                AddressOf ApplySelected

            footer.Controls.Add(
                btnApply
            )

            footer.Controls.Add(
                btnCancel
            )

            root.Controls.Add(
                intro,
                0,
                0
            )

            root.Controls.Add(
                lookupRow,
                0,
                1
            )

            root.Controls.Add(
                txtPreview,
                0,
                2
            )

            root.Controls.Add(
                lblStatus,
                0,
                3
            )

            root.Controls.Add(
                applyGroup,
                0,
                4
            )

            root.Controls.Add(
                caution,
                0,
                5
            )

            root.Controls.Add(
                footer,
                0,
                6
            )

            Me.AcceptButton =
                btnLookup

            Me.CancelButton =
                btnCancel

            Me.Controls.Add(
                root
            )

        End Sub


        Private Shared Sub ConfigureOption(
            checkBox As CheckBox,
            text As String,
            defaultChecked As Boolean
        )

            checkBox.Text =
                text

            checkBox.AutoSize =
                True

            checkBox.Checked =
                defaultChecked

            checkBox.Margin =
                New Padding(
                    3,
                    3,
                    3,
                    6
                )

        End Sub


        Private Sub LoadCurrentDoi()

            If _manuscript.Metadata Is Nothing Then

                _manuscript.Metadata =
                    New ManuscriptMetadata()

            End If

            txtDoi.Text =
                If(
                    _manuscript.Metadata.Doi,
                    String.Empty
                )

        End Sub


        Private Async Sub LookupMetadata(
            sender As Object,
            e As EventArgs
        )

            Dim normalized As String =
                DoiNormalizer.Normalize(
                    txtDoi.Text
                )

            If Not DoiNormalizer.IsValid(
                normalized
            ) Then

                MessageBox.Show(
                    Me,
                    "Please enter a valid DOI, such as 10.1037/amp0001234.",
                    "Valid DOI Required",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                )

                txtDoi.Focus()
                Return

            End If

            btnLookup.Enabled =
                False

            btnApply.Enabled =
                False

            lblStatus.Text =
                "Looking up Crossref metadata..."

            txtPreview.Clear()

            Try

                _suggestion =
                    Await _client.LookupAsync(
                        normalized
                    )

                txtDoi.Text =
                    DoiNormalizer.Normalize(
                        _suggestion.Doi
                    )

                txtPreview.Text =
                    BuildPreview(
                        _suggestion
                    )

                ConfigureDefaults(
                    _suggestion
                )

                lblStatus.Text =
                    "Crossref match found. Review the preview and choose what PaperRoute should apply."

                btnApply.Enabled =
                    True

            Catch ex As Exception

                _suggestion =
                    Nothing

                lblStatus.Text =
                    "Lookup failed."

                MessageBox.Show(
                    Me,
                    ex.Message,
                    "Crossref Lookup",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning
                )

            Finally

                btnLookup.Enabled =
                    True

            End Try

        End Sub


        Private Sub ConfigureDefaults(
            suggestion As CrossrefMetadataSuggestion
        )

            chkDoi.Checked =
                True

            chkTitle.Checked =
                String.IsNullOrWhiteSpace(
                    _manuscript.Title
                ) AndAlso
                Not String.IsNullOrWhiteSpace(
                    suggestion.Title
                )

            Dim metadata As ManuscriptMetadata =
                _manuscript.Metadata

            chkPublication.Checked =
                String.IsNullOrWhiteSpace(
                    metadata.PublicationJournal
                ) AndAlso
                Not String.IsNullOrWhiteSpace(
                    suggestion.Journal
                )

            chkAbstractKeywords.Checked =
                String.IsNullOrWhiteSpace(
                    metadata.AbstractText
                ) AndAlso
                Not String.IsNullOrWhiteSpace(
                    suggestion.AbstractText
                )

            chkAuthors.Checked =
                _manuscript.Authors.Count = 0 AndAlso
                suggestion.Authors.Count > 0

        End Sub


        Private Function BuildPreview(
            suggestion As CrossrefMetadataSuggestion
        ) As String

            Dim builder As New StringBuilder()

            AppendPreview(
                builder,
                "DOI",
                suggestion.Doi
            )

            AppendPreview(
                builder,
                "Title",
                suggestion.Title
            )

            AppendPreview(
                builder,
                "Journal",
                suggestion.Journal
            )

            AppendPreview(
                builder,
                "Published",
                If(
                    suggestion.PublishedDate.HasValue,
                    suggestion.PublishedDate.Value.ToString("yyyy-MM-dd"),
                    String.Empty
                )
            )

            AppendPreview(
                builder,
                "Volume",
                suggestion.Volume
            )

            AppendPreview(
                builder,
                "Issue",
                suggestion.Issue
            )

            AppendPreview(
                builder,
                "Pages",
                suggestion.Pages
            )

            AppendPreview(
                builder,
                "Publisher",
                suggestion.Publisher
            )

            AppendPreview(
                builder,
                "URL",
                suggestion.Url
            )

            If suggestion.Keywords.Count > 0 Then

                AppendPreview(
                    builder,
                    "Keywords",
                    String.Join(
                        "; ",
                        suggestion.Keywords
                    )
                )

            End If

            If Not String.IsNullOrWhiteSpace(
                suggestion.AbstractText
            ) Then

                builder.AppendLine()
                builder.AppendLine("Abstract")
                builder.AppendLine(
                    suggestion.AbstractText
                )

            End If

            If suggestion.Authors.Count > 0 Then

                builder.AppendLine()
                builder.AppendLine("Authors")

                For i As Integer = 0 To suggestion.Authors.Count - 1

                    Dim author As CrossrefAuthorSuggestion =
                        suggestion.Authors(i)

                    Dim matchDescription As String =
                        DescribeAuthorMatch(
                            author
                        )

                    builder.Append(
                        (i + 1).ToString()
                    )

                    builder.Append(". ")
                    builder.Append(
                        author.DisplayName
                    )

                    If Not String.IsNullOrWhiteSpace(
                        author.Orcid
                    ) Then

                        builder.Append(
                            " | ORCID "
                        )

                        builder.Append(
                            CrossrefApplyService.NormalizeOrcid(
                                author.Orcid
                            )
                        )

                    End If

                    builder.Append(
                        " | "
                    )

                    builder.AppendLine(
                        matchDescription
                    )

                Next

            End If

            Return builder.ToString().Trim()

        End Function


        Private Function DescribeAuthorMatch(
            suggestion As CrossrefAuthorSuggestion
        ) As String

            Dim normalizedOrcid As String =
                CrossrefApplyService.NormalizeOrcid(
                    suggestion.Orcid
                )

            If Not String.IsNullOrWhiteSpace(
                normalizedOrcid
            ) Then

                Dim orcidMatch As AuthorRecord =
                    _authorLibrary.Authors.
                        FirstOrDefault(
                            Function(item)
                                Return String.Equals(
                                    CrossrefApplyService.NormalizeOrcid(item.Orcid),
                                    normalizedOrcid,
                                    StringComparison.OrdinalIgnoreCase
                                )
                            End Function
                        )

                If orcidMatch IsNot Nothing Then

                    Return "matches " &
                        orcidMatch.DisplayName

                End If

            End If

            Dim nameMatch As AuthorRecord =
                _authorLibrary.Authors.
                    FirstOrDefault(
                        Function(item)
                            Return String.Equals(
                                item.DisplayName.Trim(),
                                suggestion.DisplayName.Trim(),
                                StringComparison.OrdinalIgnoreCase
                            )
                        End Function
                    )

            If nameMatch IsNot Nothing Then

                Return "matches " &
                    nameMatch.DisplayName

            End If

            Return "new author"

        End Function


        Private Shared Sub AppendPreview(
            builder As StringBuilder,
            label As String,
            value As String
        )

            If String.IsNullOrWhiteSpace(
                value
            ) Then

                Return

            End If

            builder.Append(
                label.PadRight(12)
            )

            builder.AppendLine(
                value.Trim()
            )

        End Sub


        Private Sub ApplySelected(
            sender As Object,
            e As EventArgs
        )

            If _suggestion Is Nothing Then
                Return
            End If

            Dim options As New CrossrefApplyOptions With {
                .ApplyDoi = chkDoi.Checked,
                .ApplyTitle = chkTitle.Checked,
                .ApplyPublicationDetails = chkPublication.Checked,
                .ApplyAbstractAndKeywords = chkAbstractKeywords.Checked,
                .AddMissingAuthors = chkAuthors.Checked
            }

            Dim result As CrossrefApplyResult =
                CrossrefApplyService.Apply(
                    _suggestion,
                    _manuscript,
                    _authorLibrary,
                    options
                )

            _authorLibraryChanged =
                result.AuthorLibraryChanged

            Me.DialogResult =
                DialogResult.OK

        End Sub


        Private _authorLibraryChanged As Boolean

        Public ReadOnly Property LibraryChanged As Boolean
            Get
                Return _authorLibraryChanged
            End Get
        End Property

    End Class

End Namespace
