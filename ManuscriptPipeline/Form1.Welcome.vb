Imports System
Imports System.Drawing
Imports System.Windows.Forms
Imports ManuscriptPipeline.Controls
Imports ManuscriptPipeline.Services

' An empty library opens on a welcome that explains the two ways in, rather
' than on empty shelves. It appears only while the library has no
' manuscripts and never adds sample data.
Partial Public Class Form1

    Private boardBody As Control = Nothing
    Private boardWelcome As Control = Nothing


    Private Sub ShowWelcomeWhenEmpty()

        If boardWelcome Is Nothing OrElse boardBody Is Nothing Then
            Return
        End If

        Dim empty As Boolean = manuscripts.Count = 0

        If boardWelcome.Visible <> empty Then
            boardWelcome.Visible = empty
            boardBody.Visible = Not empty
        End If

    End Sub


    Private Function BuildWelcome() As Control

        Dim dpi As Integer = DeviceDpi
        Dim textWidth As Integer = UiTheme.Px(560, dpi)

        Dim page As New Panel With {
            .Dock = DockStyle.Fill,
            .AutoScroll = True,
            .BackColor = UiTheme.BoardBackground(),
            .Padding = New Padding(UiTheme.Px(44, dpi), UiTheme.Px(56, dpi), UiTheme.Px(22, dpi), UiTheme.Px(22, dpi))
        }

        Dim column As New FlowLayoutPanel With {
            .Dock = DockStyle.Top,
            .AutoSize = True,
            .AutoSizeMode = AutoSizeMode.GrowAndShrink,
            .FlowDirection = FlowDirection.TopDown,
            .WrapContents = False,
            .BackColor = UiTheme.BoardBackground()
        }

        Dim lblTitle As New Label With {
            .Text = "Your manuscript library is empty",
            .AutoSize = True,
            .UseMnemonic = False,
            .Margin = New Padding(0, 0, 0, UiTheme.Px(10, dpi)),
            .Font = New Font(Me.Font.FontFamily, Me.Font.SizeInPoints * 1.6F, FontStyle.Bold),
            .ForeColor = UiTheme.PrimaryText()
        }

        Dim lblText As New Label With {
            .Text = "Start with the paper you are working on now, or bring in what you have already published. " &
                    "Everything else, from journal submissions to reviewer responses, builds on that.",
            .AutoSize = True,
            .UseMnemonic = False,
            .MaximumSize = New Size(textWidth, 0),
            .Margin = New Padding(0, 0, 0, UiTheme.Px(16, dpi)),
            .ForeColor = UiTheme.SecondaryText()
        }

        Dim actions As New FlowLayoutPanel With {
            .AutoSize = True,
            .AutoSizeMode = AutoSizeMode.GrowAndShrink,
            .WrapContents = True,
            .Margin = New Padding(0, 0, 0, UiTheme.Px(14, dpi)),
            .BackColor = UiTheme.BoardBackground()
        }

        Dim btnAdd As New ActionButton With {
            .Text = "+ Add Manuscript",
            .Role = ActionButtonRole.Primary,
            .AccessibleName = "Add Manuscript",
            .Width = GetResponsiveButtonWidth("+ Add Manuscript", 150),
            .Height = GetResponsiveButtonHeight(36),
            .Margin = New Padding(0, 0, UiTheme.Px(10, dpi), 0)
        }

        AddHandler btnAdd.Click, AddressOf AddManuscript

        Dim btnImport As New ActionButton With {
            .Text = "Import Existing Work",
            .Width = GetResponsiveButtonWidth("Import Existing Work", 150),
            .Height = GetResponsiveButtonHeight(36),
            .Margin = New Padding(0),
            .AccessibleDescription = "Opens Import & Export"
        }

        AddHandler btnImport.Click,
            Sub(sender, e)
                NavigateTo(WorkspacePage.ImportExport)
            End Sub

        actions.Controls.Add(btnAdd)
        actions.Controls.Add(btnImport)

        ' Each way in opens its workflow directly.
        Dim ways As New FlowLayoutPanel With {
            .AutoSize = True,
            .AutoSizeMode = AutoSizeMode.GrowAndShrink,
            .WrapContents = True,
            .MaximumSize = New Size(textWidth, 0),
            .Margin = New Padding(0, 0, 0, UiTheme.Px(18, dpi)),
            .BackColor = UiTheme.BoardBackground(),
            .AccessibleName = "Ways to start"
        }

        For Each way In {
            ("Paste a title page", CType(Sub() AddManuscript(Me, EventArgs.Empty), Action)),
            ("ORCID works", CType(Sub() OpenOrcidWorks(), Action)),
            ("BibTeX or RIS", CType(Sub() ImportBibliography(Me, EventArgs.Empty), Action)),
            ("Spreadsheet", CType(Sub() ImportExcelHistory(Me, EventArgs.Empty), Action))
        }
            Dim run As Action = way.Item2
            Dim chip As New FilterChip With {
                .Text = way.Item1,
                .Margin = New Padding(0, 0, UiTheme.Px(8, dpi), UiTheme.Px(6, dpi)),
                .AccessibleDescription = "Start by importing: " & way.Item1
            }
            AddHandler chip.Click,
                Sub(sender, e)
                    run()
                End Sub
            ways.Controls.Add(chip)
        Next

        ' The privacy promise, once and plainly.
        Dim privacy As New RoundedPanel With {
            .BackColor = UiTheme.AccentMutedBackground(),
            .BorderColor = UiTheme.AccentMutedBackground(),
            .BorderThickness = 1.0F,
            .CornerRadius = UiTheme.Px(UiTheme.ControlRadius, dpi),
            .Padding = New Padding(UiTheme.Px(12, dpi), UiTheme.Px(8, dpi), UiTheme.Px(12, dpi), UiTheme.Px(8, dpi)),
            .Margin = New Padding(0)
        }

        Dim lblPrivacy As New Label With {
            .Text = "PaperRoute keeps your library on this computer. No account is needed, and your library is never uploaded.",
            .AutoSize = True,
            .UseMnemonic = False,
            .MaximumSize = New Size(textWidth - privacy.Padding.Horizontal, 0),
            .Margin = New Padding(0),
            .BackColor = UiTheme.AccentMutedBackground(),
            .ForeColor = UiTheme.PrimaryText()
        }

        privacy.Controls.Add(lblPrivacy)
        privacy.Size = New Size(lblPrivacy.PreferredSize.Width + privacy.Padding.Horizontal, lblPrivacy.PreferredSize.Height + privacy.Padding.Vertical)
        lblPrivacy.Location = New Point(privacy.Padding.Left, privacy.Padding.Top)

        column.Controls.Add(lblTitle)
        column.Controls.Add(lblText)
        column.Controls.Add(actions)
        column.Controls.Add(ways)
        column.Controls.Add(privacy)

        ' New to PaperRoute, or teaching with it: see a whole library first.
        Dim exampleText As String = "New to PaperRoute, or teaching with it? Explore an example library"
        Dim lnkExample As New LinkLabel With {
            .Text = exampleText,
            .AutoSize = True,
            .UseMnemonic = False,
            .MaximumSize = New Size(textWidth, 0),
            .Margin = New Padding(0, UiTheme.Px(16, dpi), 0, 0),
            .BackColor = UiTheme.BoardBackground(),
            .ForeColor = UiTheme.SecondaryText(),
            .LinkColor = UiTheme.AccentColor(),
            .ActiveLinkColor = UiTheme.AccentColor(),
            .VisitedLinkColor = UiTheme.AccentColor(),
            .LinkArea = New LinkArea(exampleText.IndexOf("Explore", StringComparison.Ordinal), "Explore an example library".Length),
            .AccessibleDescription = "Opens a fictional research group's library in a separate window. Your library is not changed."
        }
        AddHandler lnkExample.LinkClicked, Sub(sender, e) OpenExampleLibrary()
        If Not ExampleLibraryService.IsActive Then column.Controls.Add(lnkExample)

        page.Controls.Add(column)

        Return page

    End Function


    ' ORCID works are imported for a reusable author, from the author's
    ' ORCID record in Authors & Affiliations.
    Private Sub OpenOrcidWorks()

        NavigateTo(WorkspacePage.Library)

        If currentPage = WorkspacePage.Library AndAlso tabLibraryAuthors IsNot Nothing Then
            tabLibraryAuthors.Checked = True
            lblStatus.Text = "Select an author, then choose ORCID... to review and import their public works."
        End If

    End Sub

End Class
