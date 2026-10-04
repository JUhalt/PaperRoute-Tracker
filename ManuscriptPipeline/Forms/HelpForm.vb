Imports System
Imports System.Drawing
Imports System.Windows.Forms
Imports ManuscriptPipeline.Services

Namespace Forms

    Public Class HelpForm
        Inherits Form

        Private ReadOnly txtSearch As New TextBox()
        Private ReadOnly txtGuide As New RichTextBox()

        Private _searchStart As Integer = 0


        ' With openExample, the footer also offers the example library
        ' (#96); the example's own window passes none.
        Public Sub New(Optional startAt As String = Nothing, Optional openExample As Action = Nothing)

            BuildInterface(openExample)
            LoadGuide()
            UiPolish.ApplyDialog(Me)

            ' Open at a section, such as "Choosing a Journal", with its
            ' heading at the top of the view.
            If Not String.IsNullOrWhiteSpace(startAt) Then
                AddHandler Me.Shown,
                    Sub(sender, e)
                        ' The heading on a line of its own, else any mention.
                        Dim index As Integer = txtGuide.Text.IndexOf(ChrW(10) & startAt & ChrW(10), StringComparison.Ordinal)
                        index = If(index >= 0, index + 1, txtGuide.Text.IndexOf(startAt, StringComparison.Ordinal))
                        If index < 0 Then Return
                        txtGuide.SelectionStart = txtGuide.TextLength
                        txtGuide.ScrollToCaret()
                        txtGuide.SelectionStart = index
                        txtGuide.SelectionLength = 0
                        txtGuide.ScrollToCaret()
                        _searchStart = index
                    End Sub
            End If

        End Sub


        ' Where the guide is showing, for tests.
        Friend ReadOnly Property GuideSelectionStart As Integer
            Get
                Return txtGuide.SelectionStart
            End Get
        End Property

        Friend ReadOnly Property GuideText As String
            Get
                Return txtGuide.Text
            End Get
        End Property


        Private Sub BuildInterface(openExample As Action)

            Me.Text =
                "PaperRoute User Guide"

            Me.StartPosition =
                FormStartPosition.CenterParent

            Me.Size =
                New Size(
                    980,
                    760
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

            Me.KeyPreview =
                True

            AddHandler Me.KeyDown,
                AddressOf HandleShortcut

            Dim root As New TableLayoutPanel With {
                .Dock = DockStyle.Fill,
                .ColumnCount = 1,
                .RowCount = 3,
                .Padding = New Padding(16)
            }

            root.RowStyles.Add(New RowStyle(SizeType.AutoSize))
            root.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
            root.RowStyles.Add(New RowStyle(SizeType.AutoSize))

            Dim searchBar As New FlowLayoutPanel With {
                .Dock = DockStyle.Fill,
                .AutoSize = True,
                .FlowDirection = FlowDirection.LeftToRight,
                .WrapContents = True,
                .Margin = New Padding(0, 0, 0, 8)
            }

            Dim lblSearch As New Label With {
                .Text = "Find",
                .AutoSize = True,
                .Anchor = AnchorStyles.Left,
                .Font =
                    New Font(
                        Me.Font,
                        FontStyle.Bold
                    ),
                .Margin = New Padding(0, 7, 8, 0)
            }

            txtSearch.Width =
                320

            txtSearch.PlaceholderText =
                "Feature or task..."

            AddHandler txtSearch.TextChanged,
                Sub(sender, e)
                    _searchStart = 0
                End Sub

            AddHandler txtSearch.KeyDown,
                Sub(sender, e)

                    If e.KeyCode = Keys.Enter Then

                        FindNext(
                            sender,
                            EventArgs.Empty
                        )

                        e.SuppressKeyPress =
                            True

                    End If

                End Sub

            Dim btnFind As New Button With {
                .Text = "Find Next",
                .AutoSize = True,
                .Height = 34
            }

            AddHandler btnFind.Click,
                AddressOf FindNext

            searchBar.Controls.Add(
                lblSearch
            )

            searchBar.Controls.Add(
                txtSearch
            )

            searchBar.Controls.Add(
                btnFind
            )

            txtGuide.Dock =
                DockStyle.Fill

            txtGuide.ReadOnly =
                True

            txtGuide.WordWrap =
                True

            txtGuide.ScrollBars =
                RichTextBoxScrollBars.Vertical

            txtGuide.BackColor =
                SystemColors.Window

            txtGuide.ForeColor =
                SystemColors.WindowText

            txtGuide.Font =
                New Font(
                    "Segoe UI",
                    10.0F
                )

            txtGuide.DetectUrls =
                True

            AddHandler txtGuide.LinkClicked,
                AddressOf OpenClickedLink

            Dim footer As New FlowLayoutPanel With {
                .Dock = DockStyle.Fill,
                .AutoSize = True,
                .FlowDirection = FlowDirection.RightToLeft,
                .WrapContents = True,
                .Padding = New Padding(0, 10, 0, 0)
            }

            Dim btnClose As New Button With {
                .Text = "Close",
                .AutoSize = True,
                .Height = 36,
                .DialogResult = DialogResult.OK
            }

            Dim btnGitHub As New Button With {
                .Text = "Open Guide on GitHub",
                .AutoSize = True,
                .Height = 36
            }

            AddHandler btnGitHub.Click,
                Sub(sender, e)

                    Try

                        UrlSafetyService.OpenInBrowser(
                            UserGuideService.GitHubGuideUrl
                        )

                    Catch ex As Exception

                        MessageBox.Show(
                            Me,
                            ex.Message,
                            "Open User Guide",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information
                        )

                    End Try

                End Sub

            footer.Controls.Add(
                btnClose
            )

            footer.Controls.Add(
                btnGitHub
            )

            ' The example opens in its own window, and the guide stays open
            ' beside it for the walk in Teaching with PaperRoute.
            If openExample IsNot Nothing Then

                Dim btnExample As New Button With {
                    .Text = "Explore an Example Library...",
                    .AutoSize = True,
                    .Height = 36,
                    .AccessibleDescription = "Opens a fictional research group's library in a separate window. Your own library is not changed."
                }

                AddHandler btnExample.Click,
                    Sub(sender, e)
                        openExample()
                    End Sub

                footer.Controls.Add(
                    btnExample
                )

            End If

            root.Controls.Add(searchBar, 0, 0)
            root.Controls.Add(txtGuide, 0, 1)
            root.Controls.Add(footer, 0, 2)

            Me.AcceptButton =
                btnFind

            Me.CancelButton =
                btnClose

            Me.Controls.Add(
                root
            )

        End Sub


        Private Sub LoadGuide()

            txtGuide.Text =
                UserGuideService.ToPlainText(
                    UserGuideService.LoadMarkdown()
                )

            txtGuide.SelectionStart =
                0

            txtGuide.ScrollToCaret()

        End Sub


        Private Sub FindNext(
            sender As Object,
            e As EventArgs
        )

            Dim searchText As String =
                txtSearch.Text.Trim()

            If String.IsNullOrWhiteSpace(
                searchText
            ) Then

                txtSearch.Focus()
                Return

            End If

            Dim index As Integer =
                txtGuide.Find(
                    searchText,
                    _searchStart,
                    RichTextBoxFinds.None
                )

            If index < 0 AndAlso
               _searchStart > 0 Then

                _searchStart =
                    0

                index =
                    txtGuide.Find(
                        searchText,
                        0,
                        RichTextBoxFinds.None
                    )

            End If

            If index < 0 Then

                MessageBox.Show(
                    Me,
                    "PaperRoute could not find '" &
                    searchText &
                    "' in the local User Guide.",
                    "Guide Search",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                )

                Return

            End If

            txtGuide.Focus()

            txtGuide.Select(
                index,
                searchText.Length
            )

            txtGuide.ScrollToCaret()

            _searchStart =
                index +
                Math.Max(
                    1,
                    searchText.Length
                )

        End Sub


        Private Sub OpenClickedLink(
            sender As Object,
            e As LinkClickedEventArgs
        )

            Try

                UrlSafetyService.OpenInBrowser(
                    e.LinkText
                )

            Catch ex As Exception

                MessageBox.Show(
                    Me,
                    ex.Message,
                    "Open Link",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                )

            End Try

        End Sub


        Private Sub HandleShortcut(
            sender As Object,
            e As KeyEventArgs
        )

            If e.Control AndAlso
               e.KeyCode = Keys.F Then

                txtSearch.Focus()
                txtSearch.SelectAll()

                e.SuppressKeyPress =
                    True

            End If

        End Sub

    End Class

End Namespace
