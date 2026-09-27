Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.Linq
Imports System.Windows.Forms
Imports ManuscriptPipeline.Controls
Imports ManuscriptPipeline.Forms
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

' The single-window shell: a left rail of pages, one content area, and
' back/forward navigation. The Board is always kept; other pages are built
' when opened and disposed when left. Library editors that save as they go
' (journals, authors, reminders) are hosted as pages until later v0.6 work
' converts them.
Partial Public Class Form1

    Friend Enum WorkspacePage
        Board
        Library
        Journals
        Reminders
        ImportExport
        ' Not in the rail: a manuscript opened from another page.
        Manuscript
    End Enum

    Private ReadOnly contentHost As New Panel()
    Private ReadOnly boardPage As New Panel()
    Private ReadOnly railButtons As New Dictionary(Of WorkspacePage, RailButton)()
    Private ReadOnly lblRailSummary As New Label()
    Private ReadOnly railItems As New List(Of Control)()
    Private railColumn As ColumnStyle = Nothing
    Private railBrand As TableLayoutPanel = Nothing
    Private railLogo As RailLogo = Nothing
    Private lblRailBrand As Label = Nothing
    Private btnRailToggle As RailCommandButton = Nothing
    Private navigationCollapsed As Boolean = False
    Private ReadOnly backHistory As New Stack(Of PageRequest)()
    Private ReadOnly forwardHistory As New Stack(Of PageRequest)()
    Private currentPage As WorkspacePage = WorkspacePage.Board
    Private currentPageView As Control = Nothing
    Private syncingRail As Boolean = False

    ' Set when the current page hosted an editor that saves as it goes.
    Private pageHostedEditor As Boolean = False

    ' Library page state.
    Private libraryGrid As DataGridView = Nothing
    Private libraryContent As Panel = Nothing
    Private libraryAuthorsForm As Form = Nothing
    Private libraryFilter As TextBox = Nothing
    Private tabLibraryManuscripts As ShelfTabButton = Nothing
    Private tabLibraryAuthors As ShelfTabButton = Nothing


    ' =====================================================
    ' Rail
    ' =====================================================

    Private Function RailWidth() As Integer
        Return UiTheme.Px(208, DeviceDpi)
    End Function

    ' Collapsed, the rail shows only icons.
    Private Function CompactRailWidth() As Integer
        Return UiTheme.Px(60, DeviceDpi)
    End Function


    Private Function BuildRail() As Control

        Dim dpi As Integer = DeviceDpi
        Dim itemWidth As Integer = RailWidth() - UiTheme.Px(20, dpi)
        Dim itemHeight As Integer = Math.Max(UiTheme.Px(34, dpi), TextRenderer.MeasureText("Ag", Me.Font).Height + UiTheme.Px(14, dpi))

        Dim rail As New TableLayoutPanel With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 1,
            .RowCount = 4,
            .Margin = New Padding(0),
            .Padding = New Padding(UiTheme.Px(10, dpi), UiTheme.Px(16, dpi), UiTheme.Px(10, dpi), UiTheme.Px(12, dpi)),
            .BackColor = UiTheme.HeaderBackground()
        }

        rail.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        rail.RowStyles.Add(New RowStyle(SizeType.AutoSize))
        rail.RowStyles.Add(New RowStyle(SizeType.AutoSize))
        rail.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        rail.RowStyles.Add(New RowStyle(SizeType.AutoSize))

        AddHandler rail.Paint,
            Sub(sender, e)
                Using line As New Pen(UiTheme.CardBorder())
                    e.Graphics.DrawLine(line, rail.Width - 1, 0, rail.Width - 1, rail.Height)
                End Using
            End Sub

        ' Brand: the logo, the name, and the library summary, beside the
        ' control that collapses the rail to icons.
        Dim brand As New TableLayoutPanel With {
            .AutoSize = True,
            .AutoSizeMode = AutoSizeMode.GrowAndShrink,
            .Dock = DockStyle.Top,
            .ColumnCount = 3,
            .RowCount = 2,
            .Margin = New Padding(0, 0, 0, UiTheme.Px(18, dpi)),
            .BackColor = UiTheme.HeaderBackground()
        }

        brand.ColumnStyles.Add(New ColumnStyle(SizeType.AutoSize))
        brand.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        brand.ColumnStyles.Add(New ColumnStyle(SizeType.AutoSize))
        brand.RowStyles.Add(New RowStyle(SizeType.AutoSize))
        brand.RowStyles.Add(New RowStyle(SizeType.AutoSize))

        railLogo = New RailLogo With {
            .Size = New Size(UiTheme.Px(30, dpi), UiTheme.Px(30, dpi)),
            .Anchor = AnchorStyles.Left,
            .AccessibleName = "PaperRoute logo. Double-click for About PaperRoute."
        }

        AddHandler railLogo.DoubleClick, AddressOf OpenAbout
        cardToolTip.SetToolTip(railLogo, "Double-click for About PaperRoute")

        Dim lblBrand As New Label With {
            .Text = "PaperRoute",
            .AutoSize = True,
            .Margin = New Padding(0),
            .Cursor = Cursors.Hand,
            .Font = New Font(Me.Font.FontFamily, Me.Font.SizeInPoints * 1.3F, FontStyle.Bold),
            .ForeColor = UiTheme.AccentColor(),
            .AccessibleName = "PaperRoute. Double-click for About PaperRoute."
        }

        AddHandler lblBrand.DoubleClick, AddressOf OpenAbout
        lblRailBrand = lblBrand

        lblRailSummary.AutoSize = True
        lblRailSummary.MaximumSize = New Size(itemWidth - UiTheme.Px(43, dpi), 0)
        lblRailSummary.Margin = New Padding(0, 2, 0, 0)
        lblRailSummary.Font = New Font(Me.Font.FontFamily, Me.Font.SizeInPoints * 0.85F)
        lblRailSummary.ForeColor = UiTheme.MutedText()
        lblRailSummary.UseMnemonic = False
        lblRailSummary.Text =
            If(StorageEnvironment.IsDevelopmentProfile(), "Development profile", "Local library")

        btnRailToggle = New RailCommandButton(RailGlyph.CollapseRail, "Collapse navigation") With {
            .Size = New Size(UiTheme.Px(28, dpi), UiTheme.Px(28, dpi)),
            .Anchor = AnchorStyles.Top Or AnchorStyles.Left
        }

        AddHandler btnRailToggle.Click,
            Sub(sender, e)
                ApplyNavigationCollapsed(Not navigationCollapsed, persist:=True)
            End Sub

        brand.Controls.Add(railLogo, 0, 0)
        brand.Controls.Add(lblBrand, 1, 0)
        brand.Controls.Add(btnRailToggle, 2, 0)
        brand.Controls.Add(lblRailSummary, 1, 1)
        brand.SetColumnSpan(lblRailSummary, 2)
        railBrand = brand
        railItems.Clear()

        ' Pages
        Dim pages As New FlowLayoutPanel With {
            .AutoSize = True,
            .AutoSizeMode = AutoSizeMode.GrowAndShrink,
            .FlowDirection = FlowDirection.TopDown,
            .WrapContents = False,
            .Margin = New Padding(0),
            .BackColor = UiTheme.HeaderBackground(),
            .AccessibleName = "Pages",
            .AccessibleRole = AccessibleRole.PageTabList
        }

        railButtons.Clear()

        For Each entry In {
            (WorkspacePage.Board, RailGlyph.Board, "Board"),
            (WorkspacePage.Library, RailGlyph.Library, "Library"),
            (WorkspacePage.Journals, RailGlyph.Journals, "Journals"),
            (WorkspacePage.Reminders, RailGlyph.Reminders, "Reminders"),
            (WorkspacePage.ImportExport, RailGlyph.ImportExport, "Import & Export")
        }
            Dim page As WorkspacePage = entry.Item1
            Dim button As New RailButton(entry.Item2, entry.Item3) With {
                .Width = itemWidth,
                .Height = itemHeight
            }
            AddHandler button.CheckedChanged,
                Sub(sender, e)
                    If button.Checked AndAlso Not syncingRail Then
                        NavigateTo(page)
                    End If
                End Sub
            cardToolTip.SetToolTip(button, entry.Item3 & " (Ctrl+" & (CInt(page) + 1).ToString() & ")")
            railButtons(page) = button
            railItems.Add(button)
            pages.Controls.Add(button)
        Next

        ' Commands
        Dim commands As New FlowLayoutPanel With {
            .AutoSize = True,
            .AutoSizeMode = AutoSizeMode.GrowAndShrink,
            .FlowDirection = FlowDirection.TopDown,
            .WrapContents = False,
            .Margin = New Padding(0),
            .Padding = New Padding(0, UiTheme.Px(8, dpi), 0, 0),
            .BackColor = UiTheme.HeaderBackground()
        }

        AddHandler commands.Paint,
            Sub(sender, e)
                Using line As New Pen(UiTheme.CardBorder())
                    e.Graphics.DrawLine(line, UiTheme.Px(6, dpi), 0, commands.Width - UiTheme.Px(6, dpi), 0)
                End Using
            End Sub

        Dim btnSettings As New RailCommandButton(RailGlyph.Settings, "Settings") With {
            .Width = itemWidth,
            .Height = itemHeight
        }

        Dim settingsMenu As ContextMenuStrip = BuildSettingsMenu()

        AddHandler btnSettings.Click,
            Sub(sender, e)
                settingsMenu.Show(btnSettings, New Point(btnSettings.Width, 0), ToolStripDropDownDirection.AboveRight)
            End Sub

        Dim btnHelp As New RailCommandButton(RailGlyph.Help, "Help") With {
            .Width = itemWidth,
            .Height = itemHeight
        }

        AddHandler btnHelp.Click, AddressOf OpenUserGuide
        cardToolTip.SetToolTip(btnHelp, "User Guide (F1)")
        cardToolTip.SetToolTip(btnSettings, "Settings")
        railItems.Add(btnSettings)
        railItems.Add(btnHelp)

        commands.Controls.Add(btnSettings)
        commands.Controls.Add(btnHelp)

        rail.Controls.Add(brand, 0, 0)
        rail.Controls.Add(pages, 0, 1)
        rail.Controls.Add(commands, 0, 3)

        Return rail

    End Function


    ' Collapses the rail to icons for more room, or expands it again. The
    ' choice is remembered once settings have been loaded.
    Private Sub ApplyNavigationCollapsed(collapsed As Boolean, persist As Boolean)

        If railColumn Is Nothing OrElse railBrand Is Nothing Then
            Return
        End If

        navigationCollapsed = collapsed

        Dim dpi As Integer = DeviceDpi
        Dim width As Integer = If(collapsed, CompactRailWidth(), RailWidth())

        SuspendLayout()

        railColumn.Width = width

        For Each item As Control In railItems
            item.Width = width - UiTheme.Px(20, dpi)
        Next

        lblRailBrand.Visible = Not collapsed
        lblRailSummary.Visible = Not collapsed

        ' Collapsed, the toggle sits under the logo.
        railBrand.SetRowSpan(railLogo, If(collapsed, 1, 2))
        railBrand.SetCellPosition(btnRailToggle, New TableLayoutPanelCellPosition(If(collapsed, 0, 2), If(collapsed, 1, 0)))
        railLogo.Margin = If(collapsed,
                             New Padding(UiTheme.Px(5, dpi), 0, 0, UiTheme.Px(8, dpi)),
                             New Padding(UiTheme.Px(5, dpi), 0, UiTheme.Px(8, dpi), 0))
        btnRailToggle.Margin = New Padding(If(collapsed, UiTheme.Px(6, dpi), 0), 0, 0, 0)

        btnRailToggle.Glyph = If(collapsed, RailGlyph.ExpandRail, RailGlyph.CollapseRail)
        btnRailToggle.Text = If(collapsed, "Expand navigation", "Collapse navigation")
        btnRailToggle.AccessibleName = btnRailToggle.Text
        cardToolTip.SetToolTip(btnRailToggle, btnRailToggle.Text)
        btnRailToggle.Invalidate()

        ResumeLayout(True)

        If persist AndAlso uiInitialized Then
            appSettings.NavigationCollapsed = collapsed
            Try
                settingsService.Save(appSettings)
            Catch
                ' A layout preference never blocks work.
            End Try
        End If

    End Sub


    ' Preferences, updates, diagnostics, and backup. Backup and Restore stay
    ' within two clicks of every page.
    Private Function BuildSettingsMenu() As ContextMenuStrip

        Dim menu As New ContextMenuStrip()

        menu.Items.Add("Preferences...", Nothing, AddressOf OpenSettings)
        menu.Items.Add("Check for Updates...", Nothing, AddressOf CheckForUpdatesNow)
        menu.Items.Add("Diagnostics...", Nothing, AddressOf OpenDiagnostics)
        menu.Items.Add(New ToolStripSeparator())
        menu.Items.Add("Backup Library...", Nothing, AddressOf BackupLibrary)
        menu.Items.Add("Restore Backup...", Nothing, AddressOf RestoreLibraryBackup)
        menu.Items.Add(New ToolStripSeparator())
        menu.Items.Add("About PaperRoute", Nothing, AddressOf OpenAbout)

        AddHandler Me.FormClosed,
            Sub(sender, e)
                menu.Dispose()
            End Sub

        Return menu

    End Function


    Private Sub SyncRail()

        ' A manuscript page belongs to the page it was opened from.
        Dim highlighted As WorkspacePage =
            If(currentPage = WorkspacePage.Manuscript, manuscriptOrigin, currentPage)

        syncingRail = True

        Try
            For Each pair In railButtons
                pair.Value.Checked = pair.Key = highlighted
            Next
        Finally
            syncingRail = False
        End Try

    End Sub


    ' Library size and due reminders, refreshed with the board.
    Private Sub UpdateRailCounts()

        Dim count As Integer = manuscripts.Count

        lblRailSummary.Text =
            If(StorageEnvironment.IsDevelopmentProfile(), "Development profile", "Local library") &
            Environment.NewLine &
            count.ToString() & If(count = 1, " manuscript", " manuscripts")

        Dim reminders As RailButton = Nothing

        If railButtons.TryGetValue(WorkspacePage.Reminders, reminders) Then
            reminders.Badge =
                ReminderService.NotificationCandidates(
                    manuscripts,
                    DateTime.Today,
                    appSettings.ReminderNotificationDaysAhead
                ).Count
        End If

    End Sub


    ' =====================================================
    ' Navigation
    ' =====================================================

    ' One entry in the back/forward history: a page, and for a manuscript
    ' page, which manuscript.
    Friend Structure PageRequest
        Public Page As WorkspacePage
        Public ManuscriptId As Guid

        Public Sub New(page As WorkspacePage, Optional manuscriptId As Guid = Nothing)
            Me.Page = page
            Me.ManuscriptId = manuscriptId
        End Sub
    End Structure


    Friend ReadOnly Property ActivePage As WorkspacePage
        Get
            Return currentPage
        End Get
    End Property


    Private ReadOnly Property CurrentRequest As PageRequest
        Get
            Return New PageRequest(currentPage, If(currentPage = WorkspacePage.Manuscript, currentManuscriptId, Guid.Empty))
        End Get
    End Property


    Friend Sub NavigateTo(
        page As WorkspacePage,
        Optional recordHistory As Boolean = True
    )

        NavigateToRequest(New PageRequest(page), recordHistory)

    End Sub


    ' Returns False when the user chose to stay on a manuscript with unsaved
    ' changes.
    Private Function NavigateToRequest(
        request As PageRequest,
        recordHistory As Boolean
    ) As Boolean

        If request.Page = currentPage AndAlso
           (request.Page <> WorkspacePage.Manuscript OrElse request.ManuscriptId = currentManuscriptId) Then
            SyncRail()
            Return True
        End If

        If Not ConfirmLeaveManuscript() Then
            SyncRail()
            Return False
        End If

        Dim previous As PageRequest = CurrentRequest

        LeaveCurrentPage()

        If recordHistory Then
            backHistory.Push(previous)
            forwardHistory.Clear()
        End If

        If request.Page = WorkspacePage.Manuscript Then
            If previous.Page <> WorkspacePage.Manuscript Then
                manuscriptOrigin = previous.Page
            End If
            currentManuscriptId = request.ManuscriptId
        End If

        currentPage = request.Page
        ShowPage(request)
        SyncRail()
        Return True

    End Function


    Private Sub GoBack()

        Dim target As PageRequest? = PopExisting(backHistory)
        If Not target.HasValue Then Return

        Dim here As PageRequest = CurrentRequest

        If NavigateToRequest(target.Value, recordHistory:=False) Then
            forwardHistory.Push(here)
        Else
            backHistory.Push(target.Value)
        End If

    End Sub


    Private Sub GoForward()

        Dim target As PageRequest? = PopExisting(forwardHistory)
        If Not target.HasValue Then Return

        Dim here As PageRequest = CurrentRequest

        If NavigateToRequest(target.Value, recordHistory:=False) Then
            backHistory.Push(here)
        Else
            forwardHistory.Push(target.Value)
        End If

    End Sub


    ' Skips history entries for manuscripts that have since been deleted.
    Private Function PopExisting(history As Stack(Of PageRequest)) As PageRequest?

        While history.Count > 0
            Dim entry As PageRequest = history.Pop()
            If entry.Page <> WorkspacePage.Manuscript OrElse FindManuscript(entry.ManuscriptId) IsNot Nothing Then
                Return entry
            End If
        End While

        Return Nothing

    End Function


    Private Function FindManuscript(id As Guid) As Manuscript
        Return manuscripts.FirstOrDefault(Function(item) item IsNot Nothing AndAlso item.Id = id)
    End Function


    Private Sub LeaveCurrentPage()

        manuscriptDirtyTimer.Stop()
        manuscriptEditor = Nothing
        manuscriptSaveBar = Nothing

        If currentPageView Is Nothing Then
            Return
        End If

        contentHost.Controls.Remove(currentPageView)
        currentPageView.Dispose()
        currentPageView = Nothing
        libraryGrid = Nothing
        libraryContent = Nothing
        libraryAuthorsForm = Nothing
        libraryFilter = Nothing

        ' Hosted editors save as they go. Refresh the board's copies, as the
        ' board did after their dialogs closed.
        If pageHostedEditor Then
            pageHostedEditor = False
            If LoadAuthorLibrary() Then
                RenderManuscripts()
            End If
        End If

    End Sub


    Private Sub ShowPage(request As PageRequest)

        If request.Page = WorkspacePage.Board Then
            boardPage.Visible = True
            boardPage.BringToFront()
            Return
        End If

        Dim view As Control

        Select Case request.Page
            Case WorkspacePage.Manuscript
                view = BuildManuscriptPage(FindManuscript(request.ManuscriptId))
            Case WorkspacePage.Library
                view = BuildLibraryPage()
            Case WorkspacePage.Journals
                view = BuildHostedPage(
                    "Journals",
                    "Your reusable journal records and their submission checklists. Changes are saved as you make them.",
                    New JournalLibraryForm(manuscripts))
            Case WorkspacePage.Reminders
                view = BuildHostedPage(
                    "Reminders",
                    "Revision deadlines, follow-ups, and your own reminders, with calendar export. Changes are saved as you make them.",
                    New RemindersForm(manuscripts, repository))
            Case Else
                view = BuildImportExportPage()
        End Select

        view.Dock = DockStyle.Fill
        currentPageView = view
        contentHost.Controls.Add(view)
        view.BringToFront()
        boardPage.Visible = False

        If request.Page = WorkspacePage.Manuscript Then
            manuscriptDirtyTimer.Start()
        End If

    End Sub


    ' =====================================================
    ' Page building blocks
    ' =====================================================

    ' A page title on the left and any actions on the right, centered on
    ' one line. The actions take their own line under the title when the
    ' window is narrow.
    Private Function CreatePageHeader(
        title As String,
        ParamArray actions As Control()
    ) As Control

        Dim lblPageTitle As New Label With {
            .Text = title,
            .AutoSize = True,
            .UseMnemonic = False,
            .Margin = New Padding(0),
            .Font = New Font(Me.Font.FontFamily, Me.Font.SizeInPoints * 1.6F, FontStyle.Bold),
            .ForeColor = UiTheme.PrimaryText(),
            .AccessibleRole = AccessibleRole.StaticText
        }

        Dim tools As New FlowLayoutPanel With {
            .AutoSize = True,
            .AutoSizeMode = AutoSizeMode.GrowAndShrink,
            .FlowDirection = FlowDirection.LeftToRight,
            .WrapContents = False,
            .Margin = New Padding(0),
            .BackColor = UiTheme.BoardBackground()
        }

        For Each action As Control In actions
            tools.Controls.Add(action)
        Next

        Return New SplitBar(lblPageTitle, tools) With {
            .CenterVertically = True,
            .Dock = DockStyle.Top,
            .Margin = New Padding(0),
            .BackColor = UiTheme.BoardBackground()
        }

    End Function


    Private Function CreatePageFrame(
        title As String,
        description As String,
        ParamArray actions As Control()
    ) As TableLayoutPanel

        Dim frame As New TableLayoutPanel With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 1,
            .RowCount = 3,
            .Padding = New Padding(UiTheme.Px(22, DeviceDpi), UiTheme.Px(14, DeviceDpi), UiTheme.Px(22, DeviceDpi), UiTheme.Px(8, DeviceDpi)),
            .BackColor = UiTheme.BoardBackground()
        }

        frame.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        frame.RowStyles.Add(New RowStyle(SizeType.AutoSize))
        frame.RowStyles.Add(New RowStyle(SizeType.AutoSize))
        frame.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))

        frame.Controls.Add(CreatePageHeader(title, actions), 0, 0)

        Dim lblDescription As New Label With {
            .Text = description,
            .AutoSize = True,
            .UseMnemonic = False,
            .Dock = DockStyle.Top,
            .Margin = New Padding(0, UiTheme.Px(4, DeviceDpi), 0, UiTheme.Px(12, DeviceDpi)),
            .ForeColor = UiTheme.SecondaryText()
        }

        frame.Controls.Add(lblDescription, 0, 1)

        Return frame

    End Function


    ' Shows an existing library editor inside a page. Its Close button is
    ' hidden: leaving the page closes it.
    Private Function HostEditor(editor As Form) As Form

        EmbedForm(editor)

        For Each button As Button In DescendantControls(editor).OfType(Of Button)().ToList()
            If button.DialogResult <> DialogResult.None AndAlso
               String.Equals(button.Text, "Close", StringComparison.OrdinalIgnoreCase) Then
                button.Visible = False
            End If
        Next

        editor.Visible = True
        pageHostedEditor = True
        Return editor

    End Function


    ' Shows a form as a child control that fills its place in a page.
    Private Shared Sub EmbedForm(form As Form)

        form.TopLevel = False
        form.FormBorderStyle = FormBorderStyle.None
        form.MinimumSize = Size.Empty
        form.Dock = DockStyle.Fill
        form.AcceptButton = Nothing
        form.CancelButton = Nothing
        form.ShowInTaskbar = False
        form.Visible = True

    End Sub


    Private Function BuildHostedPage(
        title As String,
        description As String,
        editor As Form
    ) As Control

        Dim frame As TableLayoutPanel = CreatePageFrame(title, description)
        frame.Controls.Add(HostEditor(editor), 0, 2)
        Return frame

    End Function


    Private Shared Iterator Function DescendantControls(parent As Control) As IEnumerable(Of Control)
        For Each child As Control In parent.Controls
            Yield child
            For Each descendant As Control In DescendantControls(child)
                Yield descendant
            Next
        Next
    End Function


    ' =====================================================
    ' Library page
    ' =====================================================

    Private Function BuildLibraryPage() As Control

        libraryFilter = New TextBox With {
            .PlaceholderText = "Filter by title, journal, stage, or route",
            .BorderStyle = BorderStyle.None,
            .BackColor = UiTheme.CardBackground(),
            .ForeColor = UiTheme.PrimaryText(),
            .AccessibleName = "Filter the Library"
        }
        libraryFilter.Width = Math.Max(UiTheme.Px(260, DeviceDpi), TextRenderer.MeasureText(libraryFilter.PlaceholderText, Me.Font).Width + UiTheme.Px(16, DeviceDpi))

        AddHandler libraryFilter.TextChanged,
            Sub(sender, e)
                FillLibraryGrid()
            End Sub

        Dim frame As TableLayoutPanel = CreatePageFrame(
            "Library",
            "Every manuscript on every shelf, and the reusable authors and affiliations they share.",
            CreateSearchField(libraryFilter))

        ' Manuscripts | Authors
        Dim views As New FlowLayoutPanel With {
            .Dock = DockStyle.Top,
            .AutoSize = True,
            .AutoSizeMode = AutoSizeMode.GrowAndShrink,
            .WrapContents = False,
            .Margin = New Padding(0, 0, 0, UiTheme.Px(10, DeviceDpi)),
            .BackColor = UiTheme.BoardBackground()
        }

        AddHandler views.Paint,
            Sub(sender, e)
                Using line As New Pen(UiTheme.CardBorder())
                    e.Graphics.DrawLine(line, 0, views.Height - 1, views.Width, views.Height - 1)
                End Using
            End Sub

        ' The view tabs belong to this page instance and are disposed with it.
        tabLibraryManuscripts = New ShelfTabButton() With {.Text = "Manuscripts (" & manuscripts.Count.ToString() & ")"}
        tabLibraryAuthors = New ShelfTabButton() With {.Text = "Authors & Affiliations", .UseMnemonic = False}
        views.Controls.Add(tabLibraryManuscripts)
        views.Controls.Add(tabLibraryAuthors)

        libraryContent = New Panel With {
            .Dock = DockStyle.Fill,
            .Margin = New Padding(0),
            .BackColor = UiTheme.BoardBackground()
        }

        Dim body As New TableLayoutPanel With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 1,
            .RowCount = 2,
            .Margin = New Padding(0),
            .BackColor = UiTheme.BoardBackground()
        }
        body.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        body.RowStyles.Add(New RowStyle(SizeType.AutoSize))
        body.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        body.Controls.Add(views, 0, 0)
        body.Controls.Add(libraryContent, 0, 1)
        frame.Controls.Add(body, 0, 2)

        tabLibraryManuscripts.Checked = True
        AddHandler tabLibraryManuscripts.CheckedChanged, AddressOf LibraryViewChanged
        AddHandler tabLibraryAuthors.CheckedChanged, AddressOf LibraryViewChanged

        ShowLibraryManuscripts()

        Return frame

    End Function


    Private Sub LibraryViewChanged(sender As Object, e As EventArgs)

        Dim tab As RadioButton = DirectCast(sender, RadioButton)

        If Not tab.Checked OrElse libraryContent Is Nothing Then
            Return
        End If

        If tab Is tabLibraryAuthors Then
            ShowLibraryAuthors()
        Else
            ShowLibraryManuscripts()
        End If

    End Sub


    Private Sub ClearLibraryContent()

        Dim hadAuthors As Boolean = libraryAuthorsForm IsNot Nothing

        For Each child As Control In libraryContent.Controls.Cast(Of Control)().ToList()
            libraryContent.Controls.Remove(child)
            child.Dispose()
        Next

        libraryGrid = Nothing
        libraryAuthorsForm = Nothing

        ' The authors editor saves as it goes.
        If hadAuthors AndAlso LoadAuthorLibrary() Then
            RenderManuscripts()
        End If

    End Sub


    Private Sub ShowLibraryAuthors()

        ClearLibraryContent()
        libraryAuthorsForm = HostEditor(CreateAuthorLibraryEditor())
        libraryContent.Controls.Add(libraryAuthorsForm)

    End Sub


    ' Creates the Authors & Affiliations editor. Tests supply a stand-in so
    ' they never read the real author library.
    Protected Overridable Function CreateAuthorLibraryEditor() As Form

        Return New AuthorLibraryForm(manuscripts)

    End Function


    Private Sub ShowLibraryManuscripts()

        ClearLibraryContent()

        Dim grid As New DataGridView With {
            .Dock = DockStyle.Fill,
            .ReadOnly = True,
            .AllowUserToAddRows = False,
            .AllowUserToDeleteRows = False,
            .AllowUserToResizeRows = False,
            .SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            .MultiSelect = False,
            .RowHeadersVisible = False,
            .BorderStyle = BorderStyle.None,
            .CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
            .ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None,
            .EnableHeadersVisualStyles = False,
            .StandardTab = True,
            .BackgroundColor = UiTheme.CardBackground(),
            .GridColor = UiTheme.SubtleBorder(),
            .AccessibleName = "All manuscripts. Press Enter to open the selected manuscript.",
            .ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize
        }

        grid.DefaultCellStyle.BackColor = UiTheme.CardBackground()
        grid.DefaultCellStyle.ForeColor = UiTheme.PrimaryText()
        grid.DefaultCellStyle.SelectionBackColor = UiTheme.AccentMutedBackground()
        grid.DefaultCellStyle.SelectionForeColor = UiTheme.PrimaryText()
        grid.DefaultCellStyle.Padding = New Padding(UiTheme.Px(6, DeviceDpi), 0, UiTheme.Px(6, DeviceDpi), 0)
        grid.ColumnHeadersDefaultCellStyle.BackColor = UiTheme.HeaderBackground()
        grid.ColumnHeadersDefaultCellStyle.ForeColor = UiTheme.SecondaryText()
        grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = UiTheme.HeaderBackground()
        grid.ColumnHeadersDefaultCellStyle.Font = New Font(Me.Font, FontStyle.Bold)
        grid.ColumnHeadersDefaultCellStyle.Padding = New Padding(UiTheme.Px(6, DeviceDpi), UiTheme.Px(6, DeviceDpi), UiTheme.Px(6, DeviceDpi), UiTheme.Px(6, DeviceDpi))
        grid.RowTemplate.Height = TextRenderer.MeasureText("Ag", Me.Font).Height + UiTheme.Px(14, DeviceDpi)

        grid.Columns.Add(New DataGridViewTextBoxColumn With {.Name = "Title", .HeaderText = "Title", .AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, .FillWeight = 220, .MinimumWidth = UiTheme.Px(220, DeviceDpi)})
        grid.Columns.Add(New DataGridViewTextBoxColumn With {.Name = "Stage", .HeaderText = "Stage", .AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells})
        grid.Columns.Add(New DataGridViewTextBoxColumn With {.Name = "Shelf", .HeaderText = "Shelf", .AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells})
        grid.Columns.Add(New DataGridViewTextBoxColumn With {.Name = "Journal", .HeaderText = "Target journal", .AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, .FillWeight = 55})
        grid.Columns.Add(New DataGridViewTextBoxColumn With {.Name = "Route", .HeaderText = "Route", .AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, .FillWeight = 70})
        grid.Columns.Add(New DataGridViewTextBoxColumn With {.Name = "Days", .HeaderText = "Days in stage", .AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells, .ValueType = GetType(Integer)})
        grid.Columns("Days").DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight

        For Each column As DataGridViewColumn In grid.Columns
            column.SortMode = DataGridViewColumnSortMode.Automatic
        Next

        AddHandler grid.CellDoubleClick,
            Sub(sender, e)
                If e.RowIndex >= 0 Then OpenLibraryRow(grid.Rows(e.RowIndex))
            End Sub

        AddHandler grid.KeyDown,
            Sub(sender, e)
                If e.KeyCode = Keys.Enter AndAlso grid.CurrentRow IsNot Nothing Then
                    e.Handled = True
                    OpenLibraryRow(grid.CurrentRow)
                End If
            End Sub

        libraryGrid = grid
        FillLibraryGrid()
        libraryContent.Controls.Add(grid)

        EmptyHint.Attach(
            grid,
            Function() If(manuscripts.Count = 0,
                          "No manuscripts yet. Add one from the Board, or bring in existing work from Import & Export.",
                          "No manuscripts match """ & libraryFilter.Text.Trim() & """."))

    End Sub


    Private Sub FillLibraryGrid()

        If libraryGrid Is Nothing OrElse libraryGrid.IsDisposed Then
            Return
        End If

        Dim sortColumn As DataGridViewColumn = libraryGrid.SortedColumn
        Dim sortOrder As SortOrder = libraryGrid.SortOrder
        Dim selected As Manuscript = TryCast(libraryGrid.CurrentRow?.Tag, Manuscript)

        libraryGrid.Rows.Clear()

        Dim filter As String = If(libraryFilter?.Text, String.Empty).Trim()

        For Each manuscript As Manuscript In manuscripts
            Dim route As String = RouteSummaryService.Describe(manuscript).Text

            If filter.Length > 0 AndAlso
               Not {manuscript.Title, manuscript.TargetJournal, FormatStage(manuscript.CurrentStage), FormatShelf(manuscript.Location), route}.
                   Any(Function(value) If(value, String.Empty).IndexOf(filter, StringComparison.CurrentCultureIgnoreCase) >= 0) Then
                Continue For
            End If

            Dim entered As DateTime = manuscript.StageEnteredDate.Date
            Dim days As Object =
                If(manuscript.Location = ManuscriptLocation.Pipeline AndAlso entered > DateTime.MinValue.Date AndAlso entered <= DateTime.Today,
                   CObj((DateTime.Today - entered).Days),
                   Nothing)

            Dim index As Integer = libraryGrid.Rows.Add(
                manuscript.Title,
                FormatStage(manuscript.CurrentStage),
                FormatShelf(manuscript.Location),
                manuscript.TargetJournal,
                route,
                days)

            libraryGrid.Rows(index).Tag = manuscript

            If manuscript Is selected Then
                libraryGrid.CurrentCell = libraryGrid.Rows(index).Cells(0)
            End If
        Next

        If sortColumn IsNot Nothing AndAlso sortOrder <> SortOrder.None Then
            libraryGrid.Sort(sortColumn, If(sortOrder = SortOrder.Descending, System.ComponentModel.ListSortDirection.Descending, System.ComponentModel.ListSortDirection.Ascending))
        End If

        If tabLibraryManuscripts IsNot Nothing AndAlso Not tabLibraryManuscripts.IsDisposed Then
            tabLibraryManuscripts.Text = "Manuscripts (" & manuscripts.Count.ToString() & ")"
        End If

    End Sub


    Private Shared Function FormatShelf(location As ManuscriptLocation) As String

        Select Case location
            Case ManuscriptLocation.FileDrawer
                Return "File Drawer"
            Case ManuscriptLocation.Published
                Return "Published"
            Case Else
                Return "Pipeline"
        End Select

    End Function


    Private Sub OpenLibraryRow(row As DataGridViewRow)

        Dim manuscript As Manuscript = TryCast(row?.Tag, Manuscript)

        If manuscript IsNot Nothing Then
            OpenManuscript(manuscript)
        End If

    End Sub


    ' Keeps an open Library page in step with the board after edits.
    Private Sub RefreshOpenPage()

        If currentPage = WorkspacePage.Library Then
            FillLibraryGrid()
        End If

    End Sub


    ' =====================================================
    ' Import & Export page
    ' =====================================================

    Private Function BuildImportExportPage() As Control

        Dim frame As TableLayoutPanel = CreatePageFrame(
            "Import & Export",
            "Every way of bringing work into PaperRoute or taking it out, and what each one keeps.")

        Dim scroller As New Panel With {
            .Dock = DockStyle.Fill,
            .AutoScroll = True,
            .Margin = New Padding(0),
            .BackColor = UiTheme.BoardBackground()
        }

        Dim list As New TableLayoutPanel With {
            .Dock = DockStyle.Top,
            .AutoSize = True,
            .AutoSizeMode = AutoSizeMode.GrowAndShrink,
            .ColumnCount = 2,
            .Margin = New Padding(0),
            .Padding = New Padding(0, 0, UiTheme.Px(8, DeviceDpi), 0),
            .BackColor = UiTheme.BoardBackground()
        }

        list.ColumnStyles.Add(New ColumnStyle(SizeType.AutoSize))
        list.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))

        Dim buttonWidth As Integer =
            {"Import Spreadsheet...", "Import BibTeX / RIS...", "ORCID Works...", "Get Import Template...", "Add Manuscript...",
             "Export Library to Excel...", "Export Library as BibTeX...", "Export Library as RIS...",
             "Publication & CV Export...", "Backup Library...", "Restore Backup..."}.
            Max(Function(text) GetResponsiveButtonWidth(text, 0)) + UiTheme.Px(8, DeviceDpi)

        Dim addSection As Action(Of String) =
            Sub(heading)
                list.RowStyles.Add(New RowStyle(SizeType.AutoSize))
                Dim lblHeading As New Label With {
                    .Text = heading.ToUpperInvariant(),
                    .AutoSize = True,
                    .UseMnemonic = False,
                    .Font = attentionTitleFont,
                    .ForeColor = UiTheme.MutedText(),
                    .Margin = New Padding(0, If(list.RowCount <= 1, 0, UiTheme.Px(14, DeviceDpi)), 0, UiTheme.Px(6, DeviceDpi))
                }
                list.Controls.Add(lblHeading, 0, list.RowCount - 1)
                list.SetColumnSpan(lblHeading, 2)
                list.RowCount += 1
            End Sub

        Dim addCommand As Action(Of String, String, EventHandler) =
            Sub(text, description, handler)
                list.RowStyles.Add(New RowStyle(SizeType.AutoSize))
                Dim button As New ActionButton With {
                    .Text = text,
                    .UseMnemonic = False,
                    .Width = buttonWidth,
                    .Height = GetResponsiveButtonHeight(32),
                    .Margin = New Padding(0, 0, UiTheme.Px(14, DeviceDpi), UiTheme.Px(8, DeviceDpi)),
                    .AccessibleDescription = description
                }
                AddHandler button.Click, handler
                Dim lblDescription As New Label With {
                    .Text = description,
                    .AutoSize = True,
                    .UseMnemonic = False,
                    .Anchor = AnchorStyles.Left Or AnchorStyles.Right,
                    .Margin = New Padding(0, 0, 0, UiTheme.Px(8, DeviceDpi)),
                    .ForeColor = UiTheme.SecondaryText()
                }
                list.Controls.Add(button, 0, list.RowCount - 1)
                list.Controls.Add(lblDescription, 1, list.RowCount - 1)
                list.RowCount += 1
            End Sub

        list.RowCount = 1

        addSection("Bring work in")
        addCommand("Import Spreadsheet...", "A PaperRoute template, a legacy tracker, or any workbook through column mapping. You see a preview, including likely duplicates, before anything is added.", AddressOf ImportExcelHistory)
        addCommand("Import BibTeX / RIS...", "Records from a reference manager. You review each record first; likely duplicates start unchecked.", AddressOf ImportBibliography)
        addCommand("ORCID Works...", "Public works from an author's ORCID record, reviewed before anything is added. Opens Library > Authors & Affiliations: select the author, then choose ORCID...", Sub(sender, e) OpenOrcidWorks())
        addCommand("Add Manuscript...", "Includes Paste a Title Page, which reads a Word or LaTeX title page on this computer.", AddressOf AddManuscript)
        addCommand("Get Import Template...", "A blank workbook with the standard columns, ready to fill in.", AddressOf ExportBlankTemplate)

        addSection("Take work out")
        addCommand("Export Library to Excel...", "A readable workbook of the library. It is a partial format; a backup keeps everything.", AddressOf ExportLibraryExcel)
        addCommand("Export Library as BibTeX...", "Your manuscripts as references for a reference manager.", AddressOf ExportBibTeX)
        addCommand("Export Library as RIS...", "The same, in RIS format.", AddressOf ExportRis)
        addCommand("Publication & CV Export...", "Formatted lists of your work for a CV or report.", AddressOf OpenPublicationExport)

        addSection("Keep it safe")
        addCommand("Backup Library...", "A ZIP of the complete library, including files PaperRoute manages. The only format that keeps everything.", AddressOf BackupLibrary)
        addCommand("Restore Backup...", "Replaces the current library with a backup after showing what it contains. A safety backup of the current library is made first.", AddressOf RestoreLibraryBackup)

        AddHandler scroller.Resize,
            Sub(sender, e)
                Dim descriptionWidth As Integer = Math.Max(UiTheme.Px(200, DeviceDpi), scroller.ClientSize.Width - buttonWidth - UiTheme.Px(40, DeviceDpi))
                For Each label As Label In list.Controls.OfType(Of Label)()
                    If list.GetColumn(label) = 1 Then label.MaximumSize = New Size(descriptionWidth, 0)
                Next
            End Sub

        scroller.Controls.Add(list)
        frame.Controls.Add(scroller, 0, 2)

        Return frame

    End Function

End Class
