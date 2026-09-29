Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.Linq
Imports System.Windows.Forms
Imports System.Threading.Tasks
Imports ManuscriptPipeline.Forms
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services
Imports ManuscriptPipeline.Controls

Public Class Form1

    Private activeAttentionFilter As AttentionFilter =
    AttentionFilter.None

    Private ReadOnly lblRevisionDueSoon As New FilterChip()

    Private ReadOnly settingsService As New AppSettingsService()

    Private appSettings As AppSettings =
    New AppSettings()

    Private manuscripts As New List(Of Manuscript)()

    Private ReadOnly repository As New ManuscriptRepository()
    Private ReadOnly authorRepository As New AuthorLibraryRepository()

    Private authorLibrary As New AuthorLibraryData()

    Private ReadOnly pipelinePanel As New ManuscriptShelfPanel()
    Private ReadOnly publishedPanel As New ManuscriptShelfPanel()
    Private ReadOnly fileDrawerPanel As New ManuscriptShelfPanel()

    ' One shelf is shown at a time under a single scroll area; the tabs carry
    ' each shelf's count.
    Private ReadOnly tabPipeline As New ShelfTabButton()
    Private ReadOnly tabPublished As New ShelfTabButton()
    Private ReadOnly tabFileDrawer As New ShelfTabButton()
    Private ReadOnly shelfHost As New Panel()

    Private ReadOnly lblStatus As New Label()

    Private ReadOnly txtBoardSearch As New TextBox()
    Private ReadOnly cboStageFilter As New ComboBox()
    Private ReadOnly cboBoardSort As New ComboBox()
    Private ReadOnly btnClearBoardFilters As New ActionButton()

    Private ReadOnly lblAttentionTitle As New Label()
    Private ReadOnly lblAttentionClear As New Label()
    Private ReadOnly lblOverdueRevisions As New FilterChip()
    Private ReadOnly lblLongReviews As New FilterChip()
    Private ReadOnly lblMissingJournal As New FilterChip()
    Private ReadOnly lblRecentRejections As New FilterChip()

    Private uiInitialized As Boolean = False
    Private suppressBoardFilterEvents As Boolean = False

    Private ReadOnly boardSearchDebounceTimer As New Timer With {
        .Interval = 200
    }

    Private authorSearchIndex As New AuthorLibrarySearchIndex(
        Nothing
    )

    Private currentAttentionSnapshots As New Dictionary(
        Of Guid,
        ManuscriptAttentionSnapshot
    )()

    Private cardTitleFont As Font = Nothing
    Private cardBadgeFont As Font = Nothing
    Private cardInsightFont As Font = Nothing
    Private cardMetaFont As Font = Nothing
    Private attentionTitleFont As Font = Nothing

    Private ReadOnly cardToolTip As New ToolTip()


    Private NotInheritable Class StageFilterOption

        Public ReadOnly Property Stage As PaperStage?
        Public ReadOnly Property DisplayName As String
        Public ReadOnly Property Count As Integer


        Public Sub New(
            stage As PaperStage?,
            displayName As String,
            count As Integer
        )

            Me.Stage = stage
            Me.DisplayName = displayName
            Me.Count = count

        End Sub


        Public Overrides Function ToString() As String

            Return DisplayName &
                " (" & Count.ToString() & ")"

        End Function

    End Class


    ' =====================================================
    ' Startup
    ' =====================================================

    Private Sub OpenAbout(
    sender As Object,
    e As EventArgs
)

        Using dialog As New AboutForm()

            dialog.ShowDialog(Me)

        End Using

    End Sub
    Private Sub Form1_Load(
    sender As Object,
    e As EventArgs
) Handles MyBase.Load

        If uiInitialized Then
            Return
        End If

        uiInitialized = True

        appSettings =
        settingsService.Load()

        UiPolish.InstallGlobalDialogStyling()

        BuildInterface()

        If Not LoadManuscripts() Then

            ' Do not allow PaperRoute to continue running with an
            ' artificial empty library after a data-load failure.
            BeginInvoke(
            New Action(
                Sub()
                    Close()
                End Sub
            )
        )

            Return

        End If

        If Not LoadAuthorLibrary() Then

            BeginInvoke(
                New Action(
                    Sub()
                        Close()
                    End Sub
                )
            )

            Return

        End If

        RenderManuscripts()

        ' The example window neither notifies nor checks for updates.
        If Not ExampleLibraryService.IsActive Then

            TryShowStartupReminderNotification()

            If appSettings.CheckForUpdatesAutomatically Then
                BeginAutomaticUpdateCheck()
            End If

        End If

    End Sub

    ' =====================================================
    ' Interface
    ' =====================================================

    Private Sub BuildInterface()

        Me.Controls.Clear()

        Me.Text =
            If(
                ExampleLibraryService.IsActive,
                "PaperRoute Tracker - Example Library",
                If(
                    StorageEnvironment.IsDevelopmentProfile(),
                    "PaperRoute Tracker [Development]",
                    "PaperRoute Tracker"
                )
            )
        Me.StartPosition = FormStartPosition.CenterScreen
        Me.Size = New Size(1280, 840)
        Me.MinimumSize = New Size(960, 680)
        Me.Font = New Font("Segoe UI", 10.0F)
        Me.AutoScaleMode = AutoScaleMode.Dpi
        Me.BackColor = UiTheme.BoardBackground()
        Me.DoubleBuffered = True

        ResetSharedDashboardFonts()

        ' =================================================
        ' Shell: the left rail and one content area
        ' =================================================

        Dim shell As New TableLayoutPanel With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 2,
            .RowCount = 1,
            .Margin = New Padding(0),
            .Padding = New Padding(0),
            .BackColor = UiTheme.BoardBackground()
        }

        railColumn = New ColumnStyle(SizeType.Absolute, RailWidth())
        shell.ColumnStyles.Add(railColumn)
        shell.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        shell.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))

        Dim content As New TableLayoutPanel With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 1,
            .RowCount = 2,
            .Margin = New Padding(0),
            .Padding = New Padding(0),
            .BackColor = UiTheme.BoardBackground()
        }

        content.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        content.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        content.RowStyles.Add(New RowStyle(SizeType.AutoSize))

        contentHost.Dock = DockStyle.Fill
        contentHost.Margin = New Padding(0)
        contentHost.BackColor = UiTheme.BoardBackground()
        contentHost.Controls.Clear()

        boardPage.Dock = DockStyle.Fill
        boardPage.BackColor = UiTheme.BoardBackground()
        boardPage.Controls.Clear()
        contentHost.Controls.Add(boardPage)

        ' =================================================
        ' Board page
        ' =================================================

        Dim body As New TableLayoutPanel With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 1,
            .RowCount = 4,
            .Padding = New Padding(UiTheme.Px(22, DeviceDpi), UiTheme.Px(14, DeviceDpi), UiTheme.Px(22, DeviceDpi), UiTheme.Px(8, DeviceDpi)),
            .BackColor = UiTheme.BoardBackground(),
            .AutoScroll = False
        }

        body.ColumnStyles.Add(
            New ColumnStyle(
                SizeType.Percent,
                100.0F
            )
        )

        ' The board is built at runtime. At high Windows scaling, fonts grow
        ' even though hard-coded pixel row heights do not. Keep all text and
        ' tool rows content-driven and give the remaining height to the one
        ' visible shelf.
        body.RowStyles.Add(New RowStyle(SizeType.AutoSize))
        body.RowStyles.Add(New RowStyle(SizeType.AutoSize))
        body.RowStyles.Add(New RowStyle(SizeType.AutoSize))
        body.RowStyles.Add(New RowStyle(SizeType.Percent, 100))

        ' Page header: title, search, and the one primary action.
        txtBoardSearch.PlaceholderText =
            "Search titles, journals, authors"

        txtBoardSearch.Width =
            Math.Max(
                UiTheme.Px(250, DeviceDpi),
                TextRenderer.MeasureText(
                    txtBoardSearch.PlaceholderText,
                    Me.Font
                ).Width + UiTheme.Px(20, DeviceDpi)
            )

        txtBoardSearch.Margin =
            New Padding(0)
        txtBoardSearch.BackColor = UiTheme.CardBackground()
        txtBoardSearch.ForeColor = UiTheme.PrimaryText()
        txtBoardSearch.BorderStyle = BorderStyle.None
        txtBoardSearch.AccessibleName = "Search the board (Ctrl+F)"

        Dim searchField As RoundedPanel =
            CreateSearchField(txtBoardSearch)

        Dim btnAdd As New ActionButton With {
            .Text = "+ Add Manuscript",
            .Role = ActionButtonRole.Primary,
            .AccessibleName = "Add Manuscript",
            .Width = GetResponsiveButtonWidth("+ Add Manuscript", 150),
            .Height = searchField.Height,
            .Margin = New Padding(UiTheme.Px(10, DeviceDpi), 1, 0, 0)
        }

        AddHandler btnAdd.Click,
            AddressOf AddManuscript

        searchField.TabIndex = 0
        btnAdd.TabIndex = 1

        body.Controls.Add(
            CreatePageHeader("Board", searchField, btnAdd),
            0,
            0
        )

        ' Needs Attention
        Dim attentionBar As New FlowLayoutPanel With {
            .Dock = DockStyle.Top,
            .AutoSize = True,
            .AutoSizeMode = AutoSizeMode.GrowAndShrink,
            .FlowDirection = FlowDirection.LeftToRight,
            .WrapContents = True,
            .Padding = New Padding(0, UiTheme.Px(10, DeviceDpi), 0, UiTheme.Px(6, DeviceDpi)),
            .Margin = New Padding(0),
            .BackColor = UiTheme.BoardBackground()
        }

        lblAttentionTitle.Text = "NEEDS ATTENTION"
        lblAttentionTitle.AutoSize = True
        lblAttentionTitle.Font = attentionTitleFont
        lblAttentionTitle.ForeColor = UiTheme.MutedText()
        lblAttentionTitle.Margin = New Padding(0, 5, 10, 0)

        For Each chip As FilterChip In {lblOverdueRevisions, lblRevisionDueSoon, lblLongReviews, lblMissingJournal, lblRecentRejections}
            ConfigureAttentionLabel(chip)
        Next

        AddHandler lblOverdueRevisions.Click, AddressOf FilterOverdueRevisions
        AddHandler lblRevisionDueSoon.Click, AddressOf FilterRevisionDueSoon
        AddHandler lblLongReviews.Click, AddressOf FilterLongReviews
        AddHandler lblMissingJournal.Click, AddressOf FilterMissingJournal
        AddHandler lblRecentRejections.Click, AddressOf FilterRecentRejections

        ' Shown instead of a row of zero counts when nothing needs attention.
        lblAttentionClear.Text = "Nothing needs attention right now."
        lblAttentionClear.AutoSize = True
        lblAttentionClear.ForeColor = UiTheme.SecondaryText()
        lblAttentionClear.Margin = New Padding(0, 5, 18, 0)
        lblAttentionClear.Visible = False

        attentionBar.Controls.Add(lblAttentionTitle)
        attentionBar.Controls.Add(lblAttentionClear)
        attentionBar.Controls.Add(lblOverdueRevisions)
        attentionBar.Controls.Add(lblRevisionDueSoon)
        attentionBar.Controls.Add(lblLongReviews)
        attentionBar.Controls.Add(lblMissingJournal)
        attentionBar.Controls.Add(lblRecentRejections)

        ' The chips filter the board; the dates themselves live on Deadlines.
        Dim lnkDeadlines As New LinkLabel With {
            .Text = "View in Deadlines →",
            .AutoSize = True,
            .UseMnemonic = False,
            .LinkBehavior = LinkBehavior.HoverUnderline,
            .LinkColor = UiTheme.AccentColor(),
            .ActiveLinkColor = UiTheme.AccentSecondaryColor(),
            .VisitedLinkColor = UiTheme.AccentColor(),
            .Margin = New Padding(UiTheme.Px(4, DeviceDpi), 5, 0, 0),
            .AccessibleName = "View in Deadlines",
            .AccessibleDescription = "Opens the Deadlines page (Ctrl+4)."
        }
        AddHandler lnkDeadlines.LinkClicked, Sub(sender, e) NavigateTo(WorkspacePage.Deadlines)
        attentionBar.Controls.Add(lnkDeadlines)

        body.Controls.Add(attentionBar, 0, 1)

        ' Shelf tabs with the stage filter and sort beside them.
        cboStageFilter.Width =
            Math.Max(
                150,
                TextRenderer.MeasureText(
                    "Under Review",
                    Me.Font
                ).Width + 46
            )
        cboStageFilter.DropDownStyle = ComboBoxStyle.DropDownList
        cboStageFilter.Items.Clear()
        RefreshStageFilterItems()
        cboStageFilter.Margin = New Padding(UiTheme.Px(8, DeviceDpi), UiTheme.Px(5, DeviceDpi), 0, 0)
        cboStageFilter.BackColor = UiTheme.CardBackground()
        cboStageFilter.ForeColor = UiTheme.PrimaryText()
        cboStageFilter.FlatStyle = FlatStyle.Flat
        cboStageFilter.AccessibleName = "Filter by stage"

        cboBoardSort.Width =
            Math.Max(
                235,
                TextRenderer.MeasureText(
                    "Sort: Newest stage change",
                    Me.Font
                ).Width + 46
            )
        cboBoardSort.DropDownWidth = cboBoardSort.Width
        cboBoardSort.DropDownStyle = ComboBoxStyle.DropDownList
        cboBoardSort.Items.Clear()
        cboBoardSort.Items.Add("Sort: Current order")
        cboBoardSort.Items.Add("Sort: Title A-Z")
        cboBoardSort.Items.Add("Sort: Title Z-A")
        cboBoardSort.Items.Add("Sort: Most rejections")
        cboBoardSort.Items.Add("Sort: Fewest rejections")
        cboBoardSort.Items.Add("Sort: Newest stage change")
        cboBoardSort.Items.Add("Sort: Oldest stage change")
        cboBoardSort.SelectedIndex = 0
        cboBoardSort.Margin = New Padding(UiTheme.Px(8, DeviceDpi), UiTheme.Px(5, DeviceDpi), 0, 0)
        cboBoardSort.BackColor = UiTheme.CardBackground()
        cboBoardSort.ForeColor = UiTheme.PrimaryText()
        cboBoardSort.FlatStyle = FlatStyle.Flat
        cboBoardSort.AccessibleName = "Sort the board"

        btnClearBoardFilters.Text = "Clear"
        btnClearBoardFilters.AccessibleName = "Clear search and filters"
        btnClearBoardFilters.Width = GetResponsiveButtonWidth("Clear", 72)
        btnClearBoardFilters.Height = Math.Max(cboBoardSort.PreferredHeight + 2, GetResponsiveButtonHeight(28))
        btnClearBoardFilters.Enabled = False
        btnClearBoardFilters.Margin = New Padding(UiTheme.Px(8, DeviceDpi), UiTheme.Px(4, DeviceDpi), 0, 0)

        AddHandler boardSearchDebounceTimer.Tick, AddressOf BoardSearchDebounceElapsed
        AddHandler txtBoardSearch.TextChanged, AddressOf BoardSearchTextChanged
        AddHandler cboStageFilter.SelectedIndexChanged, AddressOf BoardFilterChanged
        AddHandler cboBoardSort.SelectedIndexChanged, AddressOf BoardFilterChanged
        AddHandler btnClearBoardFilters.Click, AddressOf ClearBoardFilters

        ConfigureFlowPanel(pipelinePanel)
        ConfigureFlowPanel(publishedPanel)
        ConfigureFlowPanel(fileDrawerPanel)

        Dim shelfTabs As New FlowLayoutPanel With {
            .AutoSize = True,
            .AutoSizeMode = AutoSizeMode.GrowAndShrink,
            .FlowDirection = FlowDirection.LeftToRight,
            .WrapContents = False,
            .Padding = New Padding(0),
            .Margin = New Padding(0),
            .BackColor = UiTheme.BoardBackground()
        }

        ConfigureShelfTab(tabPipeline, "Pipeline", pipelinePanel)
        ConfigureShelfTab(tabPublished, "Published", publishedPanel)
        ConfigureShelfTab(tabFileDrawer, "File Drawer", fileDrawerPanel)

        shelfTabs.Controls.Add(tabPipeline)
        shelfTabs.Controls.Add(tabPublished)
        shelfTabs.Controls.Add(tabFileDrawer)

        Dim shelfTools As New FlowLayoutPanel With {
            .AutoSize = True,
            .AutoSizeMode = AutoSizeMode.GrowAndShrink,
            .FlowDirection = FlowDirection.LeftToRight,
            .WrapContents = False,
            .Padding = New Padding(0, 0, 0, UiTheme.Px(6, DeviceDpi)),
            .Margin = New Padding(0),
            .BackColor = UiTheme.BoardBackground()
        }

        shelfTools.Controls.Add(cboStageFilter)
        shelfTools.Controls.Add(cboBoardSort)
        shelfTools.Controls.Add(btnClearBoardFilters)

        ' Shelf tabs at the left and the stage filter and sort at the right,
        ' over one hairline; the filters move above the tabs when narrow.
        Dim shelfBar As New SplitBar(shelfTabs, shelfTools) With {
            .WrapRightAbove = True,
            .ShowBaseline = True,
            .Dock = DockStyle.Top,
            .Margin = New Padding(0, 0, 0, UiTheme.Px(10, DeviceDpi)),
            .BackColor = UiTheme.BoardBackground()
        }

        body.Controls.Add(shelfBar, 0, 2)

        shelfHost.Dock = DockStyle.Fill
        shelfHost.Margin = New Padding(0)
        shelfHost.BackColor = UiTheme.BoardBackground()
        shelfHost.Controls.Clear()
        shelfHost.Controls.Add(fileDrawerPanel)
        shelfHost.Controls.Add(publishedPanel)
        shelfHost.Controls.Add(pipelinePanel)

        tabPipeline.Checked = True
        ShowShelf(pipelinePanel)

        body.Controls.Add(shelfHost, 0, 3)

        boardPage.Controls.Add(body)
        boardBody = body

        boardWelcome = BuildWelcome()
        boardWelcome.Visible = False
        boardPage.Controls.Add(boardWelcome)

        ' =================================================
        ' Status line
        ' =================================================

        lblStatus.Dock = DockStyle.Fill
        lblStatus.AutoSize = True
        lblStatus.TextAlign = ContentAlignment.MiddleLeft
        lblStatus.Padding = New Padding(UiTheme.Px(22, DeviceDpi), 4, 0, 6)
        lblStatus.Margin = New Padding(0)
        lblStatus.ForeColor = UiTheme.MutedText()
        lblStatus.Text = "Local storage enabled."

        content.Controls.Add(contentHost, 0, 0)
        content.Controls.Add(lblStatus, 0, 1)

        shell.Controls.Add(BuildRail(), 0, 0)
        shell.Controls.Add(content, 1, 0)
        ApplyNavigationCollapsed(appSettings.NavigationCollapsed, persist:=False)

        Me.Controls.Add(shell)

        If ExampleLibraryService.IsActive Then
            Me.Controls.Add(CreateExampleBanner())
        End If

        currentPage = WorkspacePage.Board
        SyncRail()

        AddHandler pipelinePanel.Resize,
            Sub(sender, e)
                ResizeCards(pipelinePanel)
            End Sub

        AddHandler publishedPanel.Resize,
            Sub(sender, e)
                ResizeCards(publishedPanel)
            End Sub

        AddHandler fileDrawerPanel.Resize,
            Sub(sender, e)
                ResizeCards(fileDrawerPanel)
            End Sub

    End Sub

    Private Sub FilterOverdueRevisions(
    sender As Object,
    e As EventArgs
)

        If Not AttentionLabelHasItems(sender) Then
            Return
        End If

        ToggleAttentionFilter(
            AttentionFilter.OverdueRevision
        )

    End Sub


    Private Sub FilterRevisionDueSoon(
        sender As Object,
        e As EventArgs
    )

        If Not AttentionLabelHasItems(sender) Then
            Return
        End If

        ToggleAttentionFilter(
            AttentionFilter.RevisionDueSoon
        )

    End Sub


    Private Sub FilterLongReviews(
        sender As Object,
        e As EventArgs
    )

        If Not AttentionLabelHasItems(sender) Then
            Return
        End If

        ToggleAttentionFilter(
            AttentionFilter.LongReview
        )

    End Sub


    Private Sub FilterMissingJournal(
        sender As Object,
        e As EventArgs
    )

        If Not AttentionLabelHasItems(sender) Then
            Return
        End If

        ToggleAttentionFilter(
            AttentionFilter.MissingTargetJournal
        )

    End Sub


    Private Sub FilterRecentRejections(
        sender As Object,
        e As EventArgs
    )

        If Not AttentionLabelHasItems(sender) Then
            Return
        End If

        ToggleAttentionFilter(
            AttentionFilter.RecentRejection
        )

    End Sub


    Private Function AttentionLabelHasItems(
        sender As Object
    ) As Boolean

        Dim label As Label =
            TryCast(
                sender,
                Label
            )

        If label Is Nothing OrElse
           label.Tag Is Nothing Then

            Return True

        End If

        Dim count As Integer

        If Integer.TryParse(
            label.Tag.ToString(),
            count
        ) Then

            Return count > 0

        End If

        Return True

    End Function


    Private Sub ToggleAttentionFilter(
        filter As AttentionFilter
    )

        If activeAttentionFilter =
            filter Then

            activeAttentionFilter =
                AttentionFilter.None

        Else

            activeAttentionFilter =
                filter

        End If

        RenderManuscripts()

    End Sub

    Private Sub ConfigureAttentionLabel(
    label As Label
)

        label.AutoSize =
        True

        label.Margin =
        New Padding(
            0,
            2,
            8,
            2
        )

        label.Cursor =
        Cursors.Hand

    End Sub


    Private Sub RefreshAttentionDashboard()

        Dim overdueCount As Integer = 0
        Dim dueSoonCount As Integer = 0
        Dim longReviewCount As Integer = 0
        Dim missingJournalCount As Integer = 0
        Dim recentRejectionCount As Integer = 0


        For Each manuscript As Manuscript In manuscripts

            Dim snapshot As ManuscriptAttentionSnapshot =
                GetAttentionSnapshot(
                    manuscript
                )

            If snapshot.HasOverdueRevision Then

                overdueCount += 1

            End If


            If snapshot.IsRevisionDueSoon Then

                dueSoonCount += 1

            End If


            If snapshot.IsLongWaitingManuscript Then

                longReviewCount += 1

            End If


            If snapshot.HasMissingTargetJournal Then

                missingJournalCount += 1

            End If


            If snapshot.WasRecentlyRejected Then

                recentRejectionCount += 1

            End If

        Next


        lblOverdueRevisions.Text =
        overdueCount.ToString() &
        " overdue revision" &
        If(
            overdueCount = 1,
            "",
            "s"
        )


        lblRevisionDueSoon.Text =
        dueSoonCount.ToString() &
        " revision" &
        If(
            dueSoonCount = 1,
            "",
            "s"
        ) &
        " due soon"


        lblLongReviews.Text =
        longReviewCount.ToString() &
        " waiting " &
        appSettings.LongReviewThresholdDays.ToString() &
        "+ days"


        lblMissingJournal.Text =
        missingJournalCount.ToString() &
        " no target journal"


        lblRecentRejections.Text =
        recentRejectionCount.ToString() &
        " rejected in last " &
        appSettings.RecentRejectionThresholdDays.ToString() &
        " days"


        StyleAttentionLabel(
        lblOverdueRevisions,
        overdueCount,
        AttentionFilter.OverdueRevision,
        FilterChipTone.Danger
    )

        StyleAttentionLabel(
        lblRevisionDueSoon,
        dueSoonCount,
        AttentionFilter.RevisionDueSoon,
        FilterChipTone.Warning
    )

        StyleAttentionLabel(
        lblLongReviews,
        longReviewCount,
        AttentionFilter.LongReview,
        FilterChipTone.Warning
    )

        StyleAttentionLabel(
        lblMissingJournal,
        missingJournalCount,
        AttentionFilter.MissingTargetJournal,
        FilterChipTone.Neutral
    )

        StyleAttentionLabel(
        lblRecentRejections,
        recentRejectionCount,
        AttentionFilter.RecentRejection,
        FilterChipTone.Danger
    )

        lblAttentionClear.Visible =
            overdueCount + dueSoonCount + longReviewCount + missingJournalCount + recentRejectionCount = 0 AndAlso
            activeAttentionFilter = AttentionFilter.None

    End Sub


    Private Sub StyleAttentionLabel(
    chip As FilterChip,
    count As Integer,
    filter As AttentionFilter,
    tone As FilterChipTone
)

        ' Keep zero-count chips enabled so WinForms does not replace
        ' our theme-aware colors with the low-contrast disabled color.
        chip.Enabled =
        True

        chip.Tag =
        count

        ' List only what needs attention. An active filter's chip stays so
        ' it can be clicked to clear the filter.
        chip.Visible =
            count > 0 OrElse
            activeAttentionFilter = filter

        chip.Cursor =
            If(count > 0 OrElse activeAttentionFilter = filter, Cursors.Hand, Cursors.Default)

        chip.Tone =
            If(activeAttentionFilter = filter, FilterChipTone.Active, tone)

    End Sub

    Private Const MissingTargetInsight As String = "No target journal selected"

    Private Function BuildManuscriptInsight(
    manuscript As Manuscript,
    ByRef insightColor As Color
) As String

        insightColor =
        UiTheme.SecondaryText()

        Dim snapshot As ManuscriptAttentionSnapshot =
            GetAttentionSnapshot(
                manuscript
            )


        ' =================================================
        ' Overdue revision
        ' =================================================

        If snapshot.HasOverdueRevision AndAlso
           snapshot.OverdueDays.HasValue Then

            Dim overdueDays As Integer =
                snapshot.OverdueDays.Value

            insightColor =
                UiTheme.DangerColor()

            Return "Revision overdue by " &
                overdueDays.ToString() &
                " day" &
                If(
                    overdueDays = 1,
                    "",
                    "s"
                )

        End If


        ' =================================================
        ' Revision due soon
        ' =================================================

        If snapshot.IsRevisionDueSoon AndAlso
           snapshot.RevisionDaysRemaining.HasValue Then

            Dim remainingDays As Integer =
                snapshot.RevisionDaysRemaining.Value

            insightColor =
                UiTheme.WarningColor()

            If remainingDays = 0 Then
                Return "Revision due today"
            End If

            Return "Revision due in " &
                remainingDays.ToString() &
                " day" &
                If(
                    remainingDays = 1,
                    "",
                    "s"
                )

        End If


        ' =================================================
        ' Long review
        ' =================================================

        If snapshot.IsLongWaitingManuscript AndAlso
           snapshot.WaitingDays.HasValue Then

            insightColor =
                UiTheme.WarningColor()

            Return "Waiting " &
                snapshot.WaitingDays.Value.ToString() &
                " days"

        End If


        ' =================================================
        ' Recent rejection
        ' =================================================

        If snapshot.WasRecentlyRejected AndAlso
           snapshot.RejectionDaysAgo.HasValue Then

            Dim daysAgo As Integer =
                snapshot.RejectionDaysAgo.Value

            insightColor =
                UiTheme.DangerColor()

            Return "Rejected " &
                daysAgo.ToString() &
                " day" &
                If(
                    daysAgo = 1,
                    "",
                    "s"
                ) &
                " ago"

        End If


        ' =================================================
        ' Missing target
        ' =================================================

        If snapshot.HasMissingTargetJournal Then

            insightColor =
            UiTheme.WarningColor()

            Return MissingTargetInsight

        End If


        ' =================================================
        ' File Drawer suggestion
        ' =================================================

        If manuscript.Location =
            ManuscriptLocation.Pipeline AndAlso
       snapshot.RejectionCount >=
            appSettings.FileDrawerSuggestionThreshold Then

            insightColor =
            UiTheme.WarningColor()

            Return snapshot.RejectionCount.ToString() &
            " rejections; consider filing"

        End If


        Return String.Empty

    End Function

    ' The board search as a rounded field with a magnifier, like the other
    ' rounded controls. The text box itself keeps focus and keyboard behavior.
    Private Function CreateSearchField(box As TextBox) As RoundedPanel

        Dim iconSpace As Integer = UiTheme.Px(30, Me.DeviceDpi)
        Dim inset As Integer = UiTheme.Px(7, Me.DeviceDpi)

        Dim field As New RoundedPanel With {
            .BackColor = UiTheme.CardBackground(),
            .BorderColor = UiTheme.CardBorder(),
            .BorderThickness = 1.0F,
            .CornerRadius = UiTheme.Px(UiTheme.ControlRadius, Me.DeviceDpi),
            .Margin = New Padding(0, 1, 10, 0),
            .Padding = New Padding(iconSpace, inset, UiTheme.Px(10, Me.DeviceDpi), 0),
            .Cursor = Cursors.IBeam
        }

        field.Width =
            box.Width + field.Padding.Horizontal

        field.Height =
            Math.Max(
                cboStageFilter.PreferredHeight + UiTheme.Px(4, Me.DeviceDpi),
                box.PreferredHeight + inset * 2
            )

        box.Dock = DockStyle.Fill
        field.Controls.Add(box)

        AddHandler field.Paint,
            Sub(sender, e)
                Dim size As Single = UiTheme.Px(12, Me.DeviceDpi)
                Dim left As Single = (iconSpace - size) / 2.0F + UiTheme.Px(2, Me.DeviceDpi)
                Dim top As Single = (field.Height - size) / 2.0F
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias
                Using pen As New Pen(UiTheme.MutedText(), Math.Max(1.5F, UiTheme.Px(2, Me.DeviceDpi) * 0.8F))
                    e.Graphics.DrawEllipse(pen, left, top, size * 0.72F, size * 0.72F)
                    e.Graphics.DrawLine(pen, left + size * 0.62F, top + size * 0.62F, left + size, top + size)
                End Using
            End Sub

        AddHandler field.MouseDown,
            Sub(sender, e)
                box.Focus()
            End Sub

        ' The field's border shows keyboard focus.
        AddHandler box.GotFocus,
            Sub(sender, e)
                field.BorderColor = UiTheme.AccentColor()
            End Sub

        AddHandler box.LostFocus,
            Sub(sender, e)
                field.BorderColor = UiTheme.CardBorder()
            End Sub

        Return field

    End Function


    Private Sub ConfigureShelfTab(
        tab As ShelfTabButton,
        text As String,
        shelf As ManuscriptShelfPanel
    )

        tab.Text = text

        AddHandler tab.CheckedChanged,
            Sub(sender, e)
                If tab.Checked Then
                    ShowShelf(shelf)
                End If
            End Sub

    End Sub


    Private Sub ShowShelf(shelf As ManuscriptShelfPanel)

        For Each candidate As ManuscriptShelfPanel In {pipelinePanel, publishedPanel, fileDrawerPanel}
            candidate.Visible = candidate Is shelf
        Next

        ResizeCards(shelf)

    End Sub


    ' =====================================================
    ' Persistence
    ' =====================================================

    Private Function LoadManuscripts() As Boolean

        Try

            manuscripts =
            repository.Load()

            If repository.LastLoadRecoveredFromBackup Then

                Dim recoveryMessage As String =
                "PaperRoute detected a problem with the primary manuscript data file." &
                Environment.NewLine &
                Environment.NewLine &
                "Your library was recovered successfully from the automatic safety backup." &
                Environment.NewLine &
                Environment.NewLine &
                manuscripts.Count.ToString() &
                " manuscript(s) were recovered."

                If Not String.IsNullOrWhiteSpace(
                repository.LastRecoveryPreservedFilePath
            ) Then

                    recoveryMessage &=
                    Environment.NewLine &
                    Environment.NewLine &
                    "The damaged primary file was preserved for recovery and diagnostics at:" &
                    Environment.NewLine &
                    repository.LastRecoveryPreservedFilePath

                End If

                recoveryMessage &=
                Environment.NewLine &
                Environment.NewLine &
                "PaperRoute has restored a valid primary data file and can continue normally."

                MessageBox.Show(
                Me,
                recoveryMessage,
                "Library Recovered",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning
            )

                lblStatus.Text =
                "Library recovered from safety backup - " &
                DateTime.Now.ToString("h:mm tt")

            Else

                lblStatus.Text =
                "Loaded " &
                manuscripts.Count.ToString() &
                " manuscript(s) from local storage."

            End If

            If Not String.IsNullOrWhiteSpace(
                repository.LastManagedLibraryRecoveryWarning
            ) Then

                MessageBox.Show(
                    Me,
                    "Your manuscript database loaded successfully, but PaperRoute could not finish recovery or cleanup of an internal managed-version staging folder." &
                    Environment.NewLine &
                    Environment.NewLine &
                    "PaperRoute will continue rather than treat this as database corruption." &
                    Environment.NewLine &
                    Environment.NewLine &
                    "Some managed Version History files may appear as [FILE NOT FOUND] until the staging-folder problem is resolved." &
                    Environment.NewLine &
                    Environment.NewLine &
                    repository.LastManagedLibraryRecoveryWarning,
                    "Managed Version Recovery Warning",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning
                )

                lblStatus.Text =
                    "Library loaded with a managed-file recovery warning."

            End If

            Return True

        Catch ex As Exception

            manuscripts =
            New List(Of Manuscript)()

            Dim errorMessage As String =
            "PaperRoute could not safely load your manuscript library." &
            Environment.NewLine &
            Environment.NewLine &
            "The primary data file could not be read, and PaperRoute could not recover a valid safety backup." &
            Environment.NewLine &
            Environment.NewLine &
            "PaperRoute will close rather than continue with an empty library. Your existing data files have not been intentionally overwritten." &
            Environment.NewLine &
            Environment.NewLine &
            "Error details:" &
            Environment.NewLine &
            ex.Message

            MessageBox.Show(
            Me,
            errorMessage,
            "Library Could Not Be Loaded",
            MessageBoxButtons.OK,
            MessageBoxIcon.Error
        )

            lblStatus.Text =
            "Library could not be loaded safely."

            Return False

        End Try

    End Function


    ' Overridable so tests never read a real library.
    Protected Overridable Function LoadAuthorLibrary() As Boolean

        Try

            authorLibrary =
                authorRepository.Load()

            authorSearchIndex =
                New AuthorLibrarySearchIndex(
                    authorLibrary
                )

            If authorRepository.LastLoadRecoveredFromBackup Then

                Dim message As String =
                    "PaperRoute recovered the reusable author library from its safety backup."

                If Not String.IsNullOrWhiteSpace(
                    authorRepository.LastRecoveryPreservedFilePath
                ) Then

                    message &=
                        Environment.NewLine &
                        Environment.NewLine &
                        "The damaged author-library file was preserved at:" &
                        Environment.NewLine &
                        authorRepository.LastRecoveryPreservedFilePath

                End If

                MessageBox.Show(
                    Me,
                    message,
                    "Author Library Recovered",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning
                )

            End If

            Return True

        Catch ex As Exception

            MessageBox.Show(
                Me,
                "PaperRoute could not safely load the reusable author library." &
                Environment.NewLine &
                Environment.NewLine &
                ex.Message &
                Environment.NewLine &
                Environment.NewLine &
                "PaperRoute will close rather than continue with uncertain author metadata.",
                "Author Library Could Not Be Loaded",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error
            )

            Return False

        End Try

    End Function


    ' Overridable so tests never write a real library.
    Protected Overridable Function SaveManuscripts() As Boolean

        Try

            repository.Save(
            manuscripts
        )

            lblStatus.Text =
            "Saved locally - " &
            DateTime.Now.ToString("h:mm tt")

            Return True

        Catch ex As Exception

            MessageBox.Show(
            Me,
            "PaperRoute could not save your data." &
            Environment.NewLine &
            Environment.NewLine &
            ex.Message,
            "Save Error",
            MessageBoxButtons.OK,
            MessageBoxIcon.Error
        )

            lblStatus.Text =
            "Warning: latest changes were not saved."

            Return False

        End Try

    End Function

    ' =====================================================
    ' Flow panels
    ' =====================================================

    Private Sub ConfigureFlowPanel(
    panel As ManuscriptShelfPanel
)

        panel.Dock = DockStyle.Fill

        ' The shelf lays cards out in columns that always fit its visible
        ' width, and reconciles the scroll range after each layout (#37).
        panel.FlowDirection = FlowDirection.TopDown
        panel.WrapContents = False
        UpdateShelfGrid(panel)

        panel.AutoScroll = True
        panel.AutoScrollMargin = New Size(0, 0)
        panel.AutoScrollMinSize = Size.Empty
        panel.AutoSize = False
        panel.Padding = New Padding(4)
        panel.BackColor = UiTheme.BoardBackground()
        panel.BorderStyle = BorderStyle.None

    End Sub

    ' =====================================================
    ' Render manuscripts    ' =====================================================
    ' Render manuscripts
    ' =====================================================

    Private Function GetResponsiveButtonHeight(
        minimumHeight As Integer
    ) As Integer

        Return Math.Max(
            minimumHeight,
            TextRenderer.MeasureText(
                "Ag",
                Me.Font
            ).Height + 12
        )

    End Function


    Private Function GetResponsiveButtonWidth(
        text As String,
        minimumWidth As Integer
    ) As Integer

        Return Math.Max(
            minimumWidth,
            TextRenderer.MeasureText(
                text,
                Me.Font
            ).Width + 30
        )

    End Function


    Private Sub RefreshStageFilterItems()

        Dim selectedStage As PaperStage? = Nothing

        Dim existingSelection As StageFilterOption =
            TryCast(
                cboStageFilter.SelectedItem,
                StageFilterOption
            )

        If existingSelection IsNot Nothing Then
            selectedStage = existingSelection.Stage
        End If

        Dim counts As Dictionary(Of PaperStage, Integer) =
            ManuscriptStageSummaryService.CountByStage(
                manuscripts
            )

        suppressBoardFilterEvents = True

        Try

            cboStageFilter.BeginUpdate()
            cboStageFilter.Items.Clear()

            cboStageFilter.Items.Add(
                New StageFilterOption(
                    Nothing,
                    "All stages",
                    manuscripts.Count
                )
            )

            AddStageFilterOption(
                PaperStage.Idea,
                "Idea",
                counts
            )

            AddStageFilterOption(
                PaperStage.Draft,
                "Draft",
                counts
            )

            AddStageFilterOption(
                PaperStage.Submitted,
                "Submitted",
                counts
            )

            AddStageFilterOption(
                PaperStage.UnderReview,
                "Under Review",
                counts
            )

            AddStageFilterOption(
                PaperStage.Revision,
                "Revision",
                counts
            )

            AddStageFilterOption(
                PaperStage.Accepted,
                "Accepted",
                counts
            )

            AddStageFilterOption(
                PaperStage.InPress,
                "In Press",
                counts
            )

            AddStageFilterOption(
                PaperStage.Published,
                "Published",
                counts
            )

            Dim selectedIndex As Integer = 0

            If selectedStage.HasValue Then

                For index As Integer = 0 To cboStageFilter.Items.Count - 1

                    Dim optionItem As StageFilterOption =
                        TryCast(
                            cboStageFilter.Items(index),
                            StageFilterOption
                        )

                    If optionItem IsNot Nothing AndAlso
                       optionItem.Stage.HasValue AndAlso
                       optionItem.Stage.Value = selectedStage.Value Then

                        selectedIndex = index
                        Exit For

                    End If

                Next

            End If

            cboStageFilter.SelectedIndex = selectedIndex

            Dim widestText As String = String.Empty
            Dim widestWidth As Integer = 0

            For Each item As Object In cboStageFilter.Items

                Dim itemText As String = item.ToString()
                Dim itemWidth As Integer =
                    TextRenderer.MeasureText(
                        itemText,
                        Me.Font
                    ).Width

                If itemWidth > widestWidth Then
                    widestWidth = itemWidth
                    widestText = itemText
                End If

            Next

            cboStageFilter.Width =
                Math.Max(
                    150,
                    TextRenderer.MeasureText(
                        widestText,
                        Me.Font
                    ).Width + 46
                )

            cboStageFilter.DropDownWidth =
                cboStageFilter.Width

        Finally

            cboStageFilter.EndUpdate()
            suppressBoardFilterEvents = False

        End Try

    End Sub


    Private Sub AddStageFilterOption(
        stage As PaperStage,
        displayName As String,
        counts As Dictionary(Of PaperStage, Integer)
    )

        Dim count As Integer = 0

        If counts.ContainsKey(stage) Then
            count = counts(stage)
        End If

        cboStageFilter.Items.Add(
            New StageFilterOption(
                stage,
                displayName,
                count
            )
        )

    End Sub


    Private Sub BoardSearchTextChanged(
        sender As Object,
        e As EventArgs
    )

        If suppressBoardFilterEvents Then
            Return
        End If

        boardSearchDebounceTimer.Stop()
        boardSearchDebounceTimer.Start()

    End Sub


    Private Sub BoardSearchDebounceElapsed(
        sender As Object,
        e As EventArgs
    )

        boardSearchDebounceTimer.Stop()

        If suppressBoardFilterEvents Then
            Return
        End If

        RenderManuscripts()

    End Sub


    Private Sub BoardFilterChanged(
        sender As Object,
        e As EventArgs
    )

        If suppressBoardFilterEvents Then
            Return
        End If

        boardSearchDebounceTimer.Stop()
        RenderManuscripts()

    End Sub


    Private Sub ClearBoardFilters(
        sender As Object,
        e As EventArgs
    )

        boardSearchDebounceTimer.Stop()
        suppressBoardFilterEvents = True

        Try

            txtBoardSearch.Text =
                String.Empty

            cboStageFilter.SelectedIndex =
                0

            cboBoardSort.SelectedIndex =
                0

            activeAttentionFilter =
                AttentionFilter.None

        Finally

            suppressBoardFilterEvents = False

        End Try

        RenderManuscripts()

    End Sub


    Private Function HasActiveBoardFilters() As Boolean

        If Not String.IsNullOrWhiteSpace(
            txtBoardSearch.Text
        ) Then

            Return True

        End If

        Dim selectedStageOption As StageFilterOption =
            TryCast(
                cboStageFilter.SelectedItem,
                StageFilterOption
            )

        If selectedStageOption IsNot Nothing AndAlso
           selectedStageOption.Stage.HasValue Then

            Return True

        End If

        If cboBoardSort.SelectedIndex > 0 Then
            Return True
        End If

        If activeAttentionFilter <>
            AttentionFilter.None Then

            Return True

        End If

        Return False

    End Function


    Private Function ContainsSearchText(
        value As String,
        query As String
    ) As Boolean

        If String.IsNullOrWhiteSpace(value) Then
            Return False
        End If

        Return value.IndexOf(
            query,
            StringComparison.CurrentCultureIgnoreCase
        ) >= 0

    End Function



    Private Function StructuredAuthorSearchText(
        manuscript As Manuscript
    ) As String

        Return authorSearchIndex.BuildSearchText(
            manuscript
        )

    End Function


    Private Function ManuscriptMatchesBoardFilters(
    manuscript As Manuscript
) As Boolean

        Dim query As String =
        txtBoardSearch.Text.Trim()

        Dim snapshot As ManuscriptAttentionSnapshot =
            GetAttentionSnapshot(
                manuscript
            )


        If Not String.IsNullOrWhiteSpace(
        query
    ) Then

            Dim matchesSearch As Boolean =
            ContainsSearchText(
                manuscript.Title,
                query
            ) OrElse
            ContainsSearchText(
                manuscript.TargetJournal,
                query
            ) OrElse
            ContainsSearchText(
                StructuredAuthorSearchText(
                    manuscript
                ),
                query
            ) OrElse
            If(manuscript.Tags, New List(Of String)()).Any(Function(tag) ContainsSearchText(tag, query.TrimStart("#"c))) OrElse
            (manuscript.WorkType <> WorkType.Unspecified AndAlso ContainsSearchText(WorkTypeService.DisplayName(manuscript.WorkType), query))

            If Not matchesSearch Then
                Return False
            End If

        End If


        Dim selectedStageOption As StageFilterOption =
            TryCast(
                cboStageFilter.SelectedItem,
                StageFilterOption
            )

        If selectedStageOption IsNot Nothing AndAlso
           selectedStageOption.Stage.HasValue AndAlso
           manuscript.CurrentStage <> selectedStageOption.Stage.Value Then

            Return False

        End If


        Select Case activeAttentionFilter

            Case AttentionFilter.OverdueRevision

                If Not snapshot.HasOverdueRevision Then

                    Return False

                End If


            Case AttentionFilter.RevisionDueSoon

                If Not snapshot.IsRevisionDueSoon Then

                    Return False

                End If


            Case AttentionFilter.LongReview

                If Not snapshot.IsLongWaitingManuscript Then

                    Return False

                End If


            Case AttentionFilter.MissingTargetJournal

                If Not snapshot.HasMissingTargetJournal Then

                    Return False

                End If


            Case AttentionFilter.RecentRejection

                If Not snapshot.WasRecentlyRejected Then

                    Return False

                End If

        End Select


        Return True

    End Function

    Private Function GetVisibleManuscripts() As List(Of Manuscript)

        Dim result As New List(Of Manuscript)()

        For Each manuscript As Manuscript In manuscripts

            If ManuscriptMatchesBoardFilters(
                manuscript
            ) Then

                result.Add(
                    manuscript
                )

            End If

        Next


        Select Case cboBoardSort.SelectedIndex

            Case 1

                result.Sort(
                    AddressOf CompareTitleAscending
                )

            Case 2

                result.Sort(
                    AddressOf CompareTitleDescending
                )

            Case 3

                result.Sort(
                    AddressOf CompareRejectionsDescending
                )

            Case 4

                result.Sort(
                    AddressOf CompareRejectionsAscending
                )

            Case 5

                result.Sort(
                    AddressOf CompareStageDateDescending
                )

            Case 6

                result.Sort(
                    AddressOf CompareStageDateAscending
                )

        End Select


        Return result

    End Function


    Private Function CompareTitleAscending(
        first As Manuscript,
        second As Manuscript
    ) As Integer

        Return StringComparer.CurrentCultureIgnoreCase.Compare(
            first.Title,
            second.Title
        )

    End Function


    Private Function CompareTitleDescending(
        first As Manuscript,
        second As Manuscript
    ) As Integer

        Return StringComparer.CurrentCultureIgnoreCase.Compare(
            second.Title,
            first.Title
        )

    End Function


    Private Function CompareRejectionsDescending(
        first As Manuscript,
        second As Manuscript
    ) As Integer

        Return GetAttentionSnapshot(
            second
        ).RejectionCount.CompareTo(
            GetAttentionSnapshot(
                first
            ).RejectionCount
        )

    End Function


    Private Function CompareRejectionsAscending(
        first As Manuscript,
        second As Manuscript
    ) As Integer

        Return GetAttentionSnapshot(
            first
        ).RejectionCount.CompareTo(
            GetAttentionSnapshot(
                second
            ).RejectionCount
        )

    End Function


    Private Function CompareStageDateDescending(
        first As Manuscript,
        second As Manuscript
    ) As Integer

        Return second.StageEnteredDate.CompareTo(
            first.StageEnteredDate
        )

    End Function


    Private Function CompareStageDateAscending(
        first As Manuscript,
        second As Manuscript
    ) As Integer

        Return first.StageEnteredDate.CompareTo(
            second.StageEnteredDate
        )

    End Function


    Private Function BuildSectionTitle(
        title As String,
        visibleCount As Integer,
        totalCount As Integer
    ) As String

        If HasActiveBoardFilters() AndAlso
           visibleCount <> totalCount Then

            Return title &
                " (" &
                visibleCount.ToString() &
                " of " &
                totalCount.ToString() &
                ")"

        End If

        Return title &
            " (" &
            totalCount.ToString() &
            ")"

    End Function

    Private Function BuildAttentionSnapshots(
        today As DateTime
    ) As Dictionary(Of Guid, ManuscriptAttentionSnapshot)

        Dim result As New Dictionary(
            Of Guid,
            ManuscriptAttentionSnapshot
        )()

        For Each manuscript As Manuscript In manuscripts

            If manuscript Is Nothing Then
                Continue For
            End If

            result(manuscript.Id) =
                ManuscriptAttentionService.Evaluate(
                    manuscript,
                    today,
                    appSettings.RevisionWarningDays,
                    appSettings.LongReviewThresholdDays,
                    appSettings.RecentRejectionThresholdDays
                )

        Next

        Return result

    End Function


    Private Function GetAttentionSnapshot(
        manuscript As Manuscript
    ) As ManuscriptAttentionSnapshot

        If manuscript Is Nothing Then
            Return New ManuscriptAttentionSnapshot()
        End If

        Dim snapshot As ManuscriptAttentionSnapshot =
            Nothing

        If currentAttentionSnapshots.TryGetValue(
            manuscript.Id,
            snapshot
        ) Then

            Return snapshot

        End If

        snapshot =
            ManuscriptAttentionService.Evaluate(
                manuscript,
                DateTime.Today,
                appSettings.RevisionWarningDays,
                appSettings.LongReviewThresholdDays,
                appSettings.RecentRejectionThresholdDays
            )

        currentAttentionSnapshots(manuscript.Id) =
            snapshot

        Return snapshot

    End Function


    Private Sub ClearAndDisposeControls(
        panel As Control
    )

        While panel.Controls.Count > 0

            Dim child As Control =
                panel.Controls(0)

            panel.Controls.RemoveAt(
                0
            )

            child.Dispose()

        End While

    End Sub


    Private Sub ResetSharedDashboardFonts()

        DisposeSharedDashboardFonts()

        ' Card and strip text sizes follow the board font, so larger text
        ' settings scale the cards with everything else.
        Dim textScale As Single = Me.Font.SizeInPoints / 10.0F

        cardTitleFont =
            New Font(
                Me.Font.FontFamily,
                10.5F * textScale,
                FontStyle.Bold
            )

        cardBadgeFont =
            New Font(
                Me.Font.FontFamily,
                8.0F * textScale,
                FontStyle.Bold
            )

        cardInsightFont =
            New Font(
                Me.Font.FontFamily,
                9.0F * textScale,
                FontStyle.Bold
            )

        cardMetaFont =
            New Font(
                Me.Font.FontFamily,
                9.0F * textScale,
                FontStyle.Regular
            )

        attentionTitleFont =
            New Font(
                Me.Font.FontFamily,
                8.5F * textScale,
                FontStyle.Bold
            )

    End Sub


    Private Sub DisposeSharedDashboardFonts()

        For Each font As Font In New Font() {
            cardTitleFont,
            cardBadgeFont,
            cardInsightFont,
            cardMetaFont,
            attentionTitleFont
        }

            If font IsNot Nothing Then
                font.Dispose()
            End If

        Next

        cardTitleFont = Nothing
        cardBadgeFont = Nothing
        cardInsightFont = Nothing
        cardMetaFont = Nothing
        attentionTitleFont = Nothing

    End Sub


    Protected Overrides Function ProcessCmdKey(
        ByRef msg As Message,
        keyData As Keys
    ) As Boolean

        Select Case keyData

            ' Ctrl+S saves an open manuscript.
            Case Keys.Control Or Keys.S
                If currentPage = WorkspacePage.Manuscript Then
                    SaveManuscriptPage()
                    Return True
                End If

            ' Ctrl+F jumps to the board search.
            Case Keys.Control Or Keys.F
                NavigateTo(WorkspacePage.Board)
                If txtBoardSearch.CanFocus Then
                    txtBoardSearch.Focus()
                    txtBoardSearch.SelectAll()
                End If
                Return True

            ' Ctrl+1 to Ctrl+6 open the rail's pages in order.
            Case Keys.Control Or Keys.D1, Keys.Control Or Keys.NumPad1
                NavigateTo(WorkspacePage.Board)
                Return True
            Case Keys.Control Or Keys.D2, Keys.Control Or Keys.NumPad2
                NavigateTo(WorkspacePage.Library)
                Return True
            Case Keys.Control Or Keys.D3, Keys.Control Or Keys.NumPad3
                NavigateTo(WorkspacePage.Journals)
                Return True
            Case Keys.Control Or Keys.D4, Keys.Control Or Keys.NumPad4
                NavigateTo(WorkspacePage.Deadlines)
                Return True
            Case Keys.Control Or Keys.D5, Keys.Control Or Keys.NumPad5
                NavigateTo(WorkspacePage.Insights)
                Return True
            Case Keys.Control Or Keys.D6, Keys.Control Or Keys.NumPad6
                NavigateTo(WorkspacePage.ImportExport)
                Return True

            Case Keys.Alt Or Keys.Left, Keys.BrowserBack
                GoBack()
                Return True
            Case Keys.Alt Or Keys.Right, Keys.BrowserForward
                GoForward()
                Return True

            Case Keys.F1
                OpenUserGuide(Me, EventArgs.Empty)
                Return True

        End Select

        Return MyBase.ProcessCmdKey(
            msg,
            keyData
        )

    End Function


    Protected Overrides Sub OnFormClosed(
        e As FormClosedEventArgs
    )

        boardSearchDebounceTimer.Stop()
        boardSearchDebounceTimer.Dispose()
        manuscriptDirtyTimer.Stop()
        manuscriptDirtyTimer.Dispose()
        cardToolTip.Dispose()
        DisposeSharedDashboardFonts()

        MyBase.OnFormClosed(
            e
        )

    End Sub


    Private Sub RenderManuscripts()

        boardSearchDebounceTimer.Stop()

        currentAttentionSnapshots =
            BuildAttentionSnapshots(
                DateTime.Today
            )

        RefreshStageFilterItems()
        RefreshAttentionDashboard()

        Me.SuspendLayout()
        pipelinePanel.SuspendLayout()
        publishedPanel.SuspendLayout()
        fileDrawerPanel.SuspendLayout()

        ClearAndDisposeControls(
            pipelinePanel
        )

        ClearAndDisposeControls(
            publishedPanel
        )

        ClearAndDisposeControls(
            fileDrawerPanel
        )


        ' =================================================
        ' Total counts
        ' =================================================

        Dim pipelineTotal As Integer = 0
        Dim publishedTotal As Integer = 0
        Dim drawerTotal As Integer = 0

        For Each manuscript As Manuscript In manuscripts

            Select Case manuscript.Location

                Case ManuscriptLocation.Pipeline
                    pipelineTotal += 1

                Case ManuscriptLocation.Published
                    publishedTotal += 1

                Case ManuscriptLocation.FileDrawer
                    drawerTotal += 1

            End Select

        Next


        ' =================================================
        ' Filtered / sorted records
        ' =================================================

        Dim visibleManuscripts As List(Of Manuscript) =
        GetVisibleManuscripts()

        Dim pipelineCount As Integer = 0
        Dim publishedCount As Integer = 0
        Dim drawerCount As Integer = 0


        For Each manuscript As Manuscript In visibleManuscripts

            Select Case manuscript.Location

                Case ManuscriptLocation.Pipeline

                    Dim card As Panel =
                    CreateManuscriptCard(
                        manuscript,
                        pipelinePanel
                    )

                    pipelinePanel.Controls.Add(
                    card
                )

                    pipelineCount += 1


                Case ManuscriptLocation.Published

                    Dim card As Panel =
                    CreateManuscriptCard(
                        manuscript,
                        publishedPanel
                    )

                    publishedPanel.Controls.Add(
                    card
                )

                    publishedCount += 1


                Case ManuscriptLocation.FileDrawer

                    Dim card As Panel =
                    CreateManuscriptCard(
                        manuscript,
                        fileDrawerPanel
                    )

                    fileDrawerPanel.Controls.Add(
                    card
                )

                    drawerCount += 1

            End Select

        Next


        ' =================================================
        ' Section headings
        ' =================================================

        tabPipeline.Text =
        BuildSectionTitle(
            "Pipeline",
            pipelineCount,
            pipelineTotal
        )

        tabPublished.Text =
        BuildSectionTitle(
            "Published",
            publishedCount,
            publishedTotal
        )

        tabFileDrawer.Text =
        BuildSectionTitle(
            "File Drawer",
            drawerCount,
            drawerTotal
        )


        ' =================================================
        ' Empty states
        ' =================================================

        If pipelineCount = 0 Then

            Dim pipelineMessage As String

            If pipelineTotal = 0 Then

                pipelineMessage =
                "No active manuscripts. Click Add Manuscript."

            Else

                pipelineMessage =
                "No Pipeline manuscripts match the current search or filter."

            End If

            pipelinePanel.Controls.Add(
            CreateEmptyLabel(
                pipelineMessage
            )
        )

        End If


        If publishedCount = 0 Then

            Dim publishedMessage As String

            If publishedTotal = 0 Then

                publishedMessage =
                "No published manuscripts yet."

            Else

                publishedMessage =
                "No Published manuscripts match the current search or filter."

            End If

            publishedPanel.Controls.Add(
            CreateEmptyLabel(
                publishedMessage
            )
        )

        End If


        If drawerCount = 0 Then

            Dim drawerMessage As String

            If drawerTotal = 0 Then

                drawerMessage =
                "The File Drawer is empty."

            Else

                drawerMessage =
                "No File Drawer manuscripts match the current search or filter."

            End If

            fileDrawerPanel.Controls.Add(
            CreateEmptyLabel(
                drawerMessage
            )
        )

        End If


        btnClearBoardFilters.Enabled =
        HasActiveBoardFilters()

        ' =================================================
        ' Finish layout and repaint
        ' =================================================

        ResizeCards(
        pipelinePanel
    )

        ResizeCards(
        publishedPanel
    )

        ResizeCards(
        fileDrawerPanel
    )


        pipelinePanel.ResumeLayout(
            True
        )

        publishedPanel.ResumeLayout(
            True
        )

        fileDrawerPanel.ResumeLayout(
            True
        )

        Me.ResumeLayout(
            True
        )

        UpdateRailCounts()
        ShowWelcomeWhenEmpty()
        RefreshOpenPage()

    End Sub


    Private Function CreateManuscriptCard(
        manuscript As Manuscript,
        parentPanel As ManuscriptShelfPanel
    ) As Panel

        Dim dpi As Integer = Me.DeviceDpi
        Dim pad As Integer = UiTheme.Px(14, dpi)
        Dim stageText As String = FormatStage(manuscript.CurrentStage).ToUpperInvariant()
        Dim route As RouteSummary = RouteSummaryService.Describe(manuscript)

        ' The status line shows what needs attention now, otherwise how long
        ' the manuscript has been in its stage. A missing target journal shows
        ' on the journal line instead.
        Dim insightColor As Color
        Dim insightText As String = BuildManuscriptInsight(manuscript, insightColor)
        Dim statusIsUrgent As Boolean = insightText.Length > 0 AndAlso insightText <> MissingTargetInsight
        Dim statusText As String = If(statusIsUrgent, insightText, StageClockService.Describe(manuscript, DateTime.Today))
        Dim hasJournal As Boolean = Not String.IsNullOrWhiteSpace(manuscript.TargetJournal)

        Dim titleLine As Integer = TextRenderer.MeasureText("Ag", cardTitleFont).Height
        Dim metaLine As Integer = TextRenderer.MeasureText("Ag", cardMetaFont).Height
        Dim badgeHeight As Integer = TextRenderer.MeasureText(stageText, cardBadgeFont).Height + UiTheme.Px(6, dpi)
        Dim badgeWidth As Integer = TextRenderer.MeasureText(stageText, cardBadgeFont).Width + UiTheme.Px(16, dpi)
        Dim moreHeight As Integer = Math.Max(UiTheme.Px(26, dpi), metaLine + UiTheme.Px(6, dpi))

        Dim rowTop As Integer = pad
        Dim rowHeight As Integer = Math.Max(badgeHeight, moreHeight)
        Dim titleTop As Integer = rowTop + rowHeight + UiTheme.Px(8, dpi)
        Dim journalTop As Integer = titleTop + titleLine * 2 + UiTheme.Px(4, dpi)
        Dim dividerTop As Integer = journalTop + metaLine + UiTheme.Px(12, dpi)
        Dim footerTop As Integer = dividerTop + 1 + UiTheme.Px(8, dpi)
        Dim footerHeight As Integer = metaLine + UiTheme.Px(4, dpi)

        Dim card As New RoundedPanel With {
            .Width = parentPanel.MinimumCardWidth,
            .Height = footerTop + footerHeight + pad,
            .BackColor = UiTheme.CardBackground(),
            .BorderColor = UiTheme.CardBorder(),
            .BorderThickness = 1.0F,
            .CornerRadius = UiTheme.Px(UiTheme.CardRadius, dpi),
            .Margin = New Padding(0),
            .Cursor = Cursors.Hand,
            .AccessibleName = manuscript.Title
        }

        ' =================================================
        ' Stage, status, and more actions
        ' =================================================

        Dim stageBadge As New PillLabel With {
            .Text = stageText,
            .UseMnemonic = False,
            .Left = pad,
            .Top = rowTop + (rowHeight - badgeHeight) \ 2,
            .Width = badgeWidth,
            .Height = badgeHeight,
            .Font = cardBadgeFont,
            .BackColor = UiTheme.StageBackground(manuscript.CurrentStage),
            .ForeColor = UiTheme.StageForeground(manuscript.CurrentStage)
        }

        Dim lblStatusLine As Label = Nothing

        If statusText.Length > 0 Then
            lblStatusLine = New Label With {
                .Text = statusText,
                .AutoSize = False,
                .AutoEllipsis = True,
                .UseMnemonic = False,
                .Left = stageBadge.Right + UiTheme.Px(8, dpi),
                .Top = rowTop + (rowHeight - metaLine - 2) \ 2,
                .Height = metaLine + 2,
                .Font = If(statusIsUrgent, cardInsightFont, cardMetaFont),
                .ForeColor = If(statusIsUrgent, insightColor, UiTheme.MutedText())
            }
            cardToolTip.SetToolTip(lblStatusLine, statusText)
        End If

        Dim cardMenu As ContextMenuStrip = BuildCardMenu(manuscript)

        AddHandler card.Disposed,
            Sub(sender, e)
                cardMenu.Dispose()
            End Sub

        Dim btnMore As New ActionButton With {
            .Text = ChrW(&H22EF),
            .Role = ActionButtonRole.Quiet,
            .Padding = New Padding(0),
            .Width = moreHeight + UiTheme.Px(6, dpi),
            .Height = moreHeight,
            .Top = rowTop + (rowHeight - moreHeight) \ 2,
            .AccessibleName = "More actions for " & manuscript.Title,
            .UseMnemonic = False
        }

        AddHandler btnMore.Click,
            Sub(sender, e)
                cardMenu.Show(btnMore, New Point(0, btnMore.Height))
            End Sub

        ' =================================================
        ' Title and journal
        ' =================================================

        ' A link, so the title is the card's keyboard-focusable way to open it.
        Dim lblTitle As New LinkLabel With {
            .Text = manuscript.Title,
            .AutoSize = False,
            .AutoEllipsis = True,
            .UseMnemonic = False,
            .Left = pad,
            .Top = titleTop,
            .Height = titleLine * 2 + 2,
            .Font = cardTitleFont,
            .LinkBehavior = LinkBehavior.HoverUnderline,
            .LinkColor = UiTheme.PrimaryText(),
            .ActiveLinkColor = UiTheme.AccentColor(),
            .VisitedLinkColor = UiTheme.PrimaryText(),
            .Cursor = Cursors.Hand,
            .AccessibleDescription = String.Join(", ", {FormatStage(manuscript.CurrentStage), statusText, If(hasJournal, manuscript.TargetJournal, "no target journal"), route.Text}.Where(Function(part) part.Length > 0))
        }
        cardToolTip.SetToolTip(lblTitle, manuscript.Title)

        Dim lblJournal As New Label With {
            .Text = If(hasJournal, manuscript.TargetJournal, If(manuscript.Location = ManuscriptLocation.Pipeline, "No target journal yet", "No target journal")),
            .AutoSize = False,
            .AutoEllipsis = True,
            .UseMnemonic = False,
            .Left = pad,
            .Top = journalTop,
            .Height = metaLine + 2,
            .Font = cardMetaFont,
            .ForeColor = If(hasJournal, UiTheme.SecondaryText(), If(manuscript.Location = ManuscriptLocation.Pipeline, UiTheme.WarningColor(), UiTheme.MutedText()))
        }
        If hasJournal Then cardToolTip.SetToolTip(lblJournal, manuscript.TargetJournal)

        ' Tags share the journal line, right-aligned, so every card keeps the
        ' same height. Clicking one searches the board for it.
        Dim tagStrip As TagStrip = Nothing
        If manuscript.Tags IsNot Nothing AndAlso manuscript.Tags.Count > 0 Then
            tagStrip = New TagStrip With {
                .Tags = manuscript.Tags.ToList(),
                .Library = authorLibrary,
                .Font = cardMetaFont,
                .Top = journalTop,
                .Height = metaLine + 2,
                .BackColor = UiTheme.CardBackground(),
                .Cursor = Cursors.Hand
            }
            cardToolTip.SetToolTip(tagStrip, String.Join(", ", manuscript.Tags) & " (click to search the board)")
            AddHandler tagStrip.MouseClick,
                Sub(sender, e)
                    If e.Button = MouseButtons.Left Then txtBoardSearch.Text = manuscript.Tags(0)
                End Sub
        End If

        ' =================================================
        ' Route footer
        ' =================================================

        Dim divider As New Panel With {
            .Left = pad,
            .Top = dividerTop,
            .Height = 1,
            .BackColor = UiTheme.SubtleBorder()
        }

        Dim dots As New RouteDots With {
            .Left = pad,
            .Dots = route.Dots,
            .BackColor = UiTheme.CardBackground()
        }
        dots.Top = footerTop + (footerHeight - dots.Height) \ 2
        dots.Visible = route.Dots.Count > 0

        Dim lblRouteSummary As New Label With {
            .Text = route.Text,
            .AutoSize = False,
            .AutoEllipsis = True,
            .UseMnemonic = False,
            .Left = If(route.Dots.Count > 0, dots.Right + UiTheme.Px(6, dpi), pad),
            .Top = footerTop,
            .Height = footerHeight,
            .TextAlign = ContentAlignment.MiddleLeft,
            .Font = cardMetaFont,
            .ForeColor = UiTheme.SecondaryText()
        }

        Const routeLinkText As String = "View route →"
        Dim routeLinkWidth As Integer = TextRenderer.MeasureText(routeLinkText, cardMetaFont).Width + UiTheme.Px(4, dpi)

        ' A real link, so the route is reachable from the keyboard.
        Dim lblRoute As New LinkLabel With {
            .Text = routeLinkText,
            .AutoSize = False,
            .UseMnemonic = False,
            .Width = routeLinkWidth,
            .Height = footerHeight,
            .Top = footerTop,
            .Font = cardMetaFont,
            .TextAlign = ContentAlignment.MiddleRight,
            .LinkBehavior = LinkBehavior.HoverUnderline,
            .LinkColor = UiTheme.AccentColor(),
            .ActiveLinkColor = UiTheme.AccentSecondaryColor(),
            .VisitedLinkColor = UiTheme.AccentColor(),
            .Cursor = Cursors.Hand,
            .AccessibleName = "View route for " & manuscript.Title
        }

        ' =================================================
        ' Behavior
        ' =================================================

        Dim openOnLeftClick As MouseEventHandler =
            Sub(sender, e)
                If e.Button = MouseButtons.Left Then
                    OpenManuscript(manuscript)
                End If
            End Sub

        AddHandler lblTitle.LinkClicked,
            Sub(sender, e)
                OpenManuscript(manuscript)
            End Sub

        AddHandler lblRoute.LinkClicked,
            Sub(sender, e)
                OpenRouteView(manuscript)
            End Sub

        ' Hovering anywhere on the card, or focusing its title, highlights it.
        Dim setHot As Action(Of Boolean) =
            Sub(hot)
                card.BorderColor = If(hot, UiTheme.AccentColor(), UiTheme.CardBorder())
            End Sub

        Dim refreshHover As EventHandler =
            Sub(sender, e)
                If card.IsDisposed Then Return
                setHot(lblTitle.Focused OrElse card.ClientRectangle.Contains(card.PointToClient(Control.MousePosition)))
            End Sub

        AddHandler lblTitle.GotFocus, refreshHover
        AddHandler lblTitle.LostFocus, refreshHover

        card.Controls.Add(lblTitle)
        card.Controls.Add(btnMore)
        card.Controls.Add(stageBadge)
        If lblStatusLine IsNot Nothing Then card.Controls.Add(lblStatusLine)
        card.Controls.Add(lblJournal)
        If tagStrip IsNot Nothing Then card.Controls.Add(tagStrip)
        card.Controls.Add(divider)
        card.Controls.Add(dots)
        card.Controls.Add(lblRouteSummary)
        card.Controls.Add(lblRoute)

        AddHandler card.MouseClick, openOnLeftClick
        AddHandler card.MouseEnter, refreshHover
        AddHandler card.MouseLeave, refreshHover

        ' Right-click (or the keyboard context-menu key on the focused title)
        ' anywhere on the card shows the same menu.
        card.ContextMenuStrip = cardMenu

        For Each child As Control In card.Controls
            child.ContextMenuStrip = cardMenu
            AddHandler child.MouseEnter, refreshHover
            AddHandler child.MouseLeave, refreshHover
            If Not TypeOf child Is LinkLabel AndAlso Not TypeOf child Is ButtonBase AndAlso Not TypeOf child Is TagStrip Then
                AddHandler child.MouseClick, openOnLeftClick
            End If
        Next

        ' =================================================
        ' Width-dependent layout
        ' =================================================

        Dim layoutCard As Action =
            Sub()
                Dim right As Integer = card.ClientSize.Width - pad
                Dim contentWidth As Integer = Math.Max(1, right - pad)

                btnMore.Left = Math.Max(pad, card.ClientSize.Width - btnMore.Width - UiTheme.Px(8, dpi))

                If lblStatusLine IsNot Nothing Then
                    lblStatusLine.Width = Math.Max(1, btnMore.Left - lblStatusLine.Left - UiTheme.Px(4, dpi))
                End If

                lblTitle.Width = contentWidth
                lblJournal.Width = contentWidth
                If tagStrip IsNot Nothing Then
                    ' Tags take at most half the line; the journal keeps the rest.
                    tagStrip.Width = tagStrip.PreferredWidth(contentWidth \ 2)
                    tagStrip.Left = right - tagStrip.Width
                    lblJournal.Width = Math.Max(1, contentWidth - tagStrip.Width - If(tagStrip.Width > 0, UiTheme.Px(8, dpi), 0))
                End If
                divider.Width = contentWidth

                lblRoute.Left = Math.Max(pad, right - routeLinkWidth)
                lblRouteSummary.Width = Math.Max(1, lblRoute.Left - lblRouteSummary.Left - UiTheme.Px(8, dpi))
            End Sub

        AddHandler card.ClientSizeChanged,
            Sub(sender, e)
                layoutCard()
            End Sub

        layoutCard()

        Return card

    End Function


    Private Function BuildCardMenu(
        manuscript As Manuscript
    ) As ContextMenuStrip

        Dim cardMenu As New ContextMenuStrip()

        cardMenu.Items.Add(
            "Open",
            Nothing,
            Sub(sender, e)
                OpenManuscript(manuscript)
            End Sub
        )

        cardMenu.Items.Add(
            "View Route",
            Nothing,
            Sub(sender, e)
                OpenRouteView(manuscript)
            End Sub
        )

        If manuscript.Location = ManuscriptLocation.Pipeline Then

            cardMenu.Items.Add(New ToolStripSeparator())
            cardMenu.Items.Add(
                "Move to File Drawer...",
                Nothing,
                Sub(sender, e)
                    MoveToFileDrawer(manuscript)
                End Sub
            )

        ElseIf manuscript.Location = ManuscriptLocation.FileDrawer Then

            cardMenu.Items.Add(New ToolStripSeparator())
            cardMenu.Items.Add(
                "Restore to Pipeline",
                Nothing,
                Sub(sender, e)
                    RestoreToPipeline(manuscript)
                End Sub
            )

        End If

        cardMenu.Items.Add(New ToolStripSeparator())

        Dim deleteItem As ToolStripItem =
            cardMenu.Items.Add(
                "Delete...",
                Nothing,
                Sub(sender, e)
                    DeleteManuscript(manuscript)
                End Sub
            )

        deleteItem.ForeColor = UiTheme.DangerColor()

        Return cardMenu

    End Function


    Private Function CreateEmptyLabel(
    text As String
) As Label

        Return New Label With {
        .Text = text,
        .AutoSize = False,
        .Width = 650,
        .Height = Math.Max(48, TextRenderer.MeasureText("Ag", Me.Font).Height + 24),
        .Padding = New Padding(12),
        .ForeColor = UiTheme.SecondaryText(),
        .BackColor = UiTheme.BoardBackground()
    }

    End Function


    ' Card columns follow both the display DPI and the text size, so larger
    ' text gets fewer, wider columns rather than cramped cards.
    Private Sub UpdateShelfGrid(
        panel As ManuscriptShelfPanel
    )

        Dim textScale As Single = Me.Font.SizeInPoints / 10.0F

        panel.MinimumCardWidth =
            CInt(UiTheme.Px(280, Me.DeviceDpi) * textScale)

        panel.CardGap =
            UiTheme.Px(UiTheme.SpaceMd, Me.DeviceDpi)

    End Sub


    Private Sub ResizeCards(
        panel As ManuscriptShelfPanel
    )

        If panel Is Nothing OrElse
           panel.IsDisposed OrElse
           panel.ClientSize.Width <= 0 Then

            Return

        End If

        UpdateShelfGrid(panel)

        panel.SuspendLayout()

        Try

            panel.AutoScrollMinSize =
                Size.Empty

            If panel.AutoScrollPosition.X <> 0 Then

                panel.AutoScrollPosition =
                    New Point(0, -panel.AutoScrollPosition.Y)

            End If

        Finally

            panel.ResumeLayout(
                True
            )

        End Try

    End Sub


    ' =====================================================
    ' Add / open
    ' =====================================================

    Private Sub AddManuscript(
        sender As Object,
        e As EventArgs
    )

        ' Pasting a title page matches authors against a fresh copy of the
        ' reusable library, so a failed save never leaves unsaved people in the
        ' board's copy. If the library cannot be read, Add Manuscript still works.
        Dim editableLibrary As AuthorLibraryData = Nothing

        Try
            editableLibrary = authorRepository.Load()
        Catch ex As Exception
            editableLibrary = Nothing
        End Try

        Using dialog As New AddManuscriptForm(editableLibrary)

            If dialog.ShowDialog(Me) =
                DialogResult.OK AndAlso
               dialog.CreatedManuscript IsNot Nothing Then

                ' Save reusable people first, as bibliography import does, so a
                ' manuscript never references an author record that failed to save.
                If dialog.AuthorLibraryChanged Then
                    Try
                        authorRepository.Save(editableLibrary)
                    Catch ex As Exception
                        MessageBox.Show(
                            Me,
                            "PaperRoute could not save the new authors, so the manuscript was not added." &
                            Environment.NewLine &
                            Environment.NewLine &
                            ex.Message,
                            "Add Manuscript",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Warning
                        )
                        Return
                    End Try

                    ' Refresh the board's library and author search index.
                    If Not LoadAuthorLibrary() Then
                        Return
                    End If
                End If

                manuscripts.Add(
                    dialog.CreatedManuscript
                )

                SaveManuscripts()
                RenderManuscripts()

            End If

        End Using

    End Sub


    Private Sub OpenRouteView(
        manuscript As Manuscript
    )

        Dim selectedWaypoint As ManuscriptRouteWaypoint =
            Nothing

        Using dialog As New ManuscriptRouteViewForm(
            manuscript
        )

            dialog.ShowDialog(
                Me
            )

            selectedWaypoint =
                dialog.SelectedWaypoint

        End Using

        If selectedWaypoint IsNot Nothing Then

            OpenManuscript(
                manuscript,
                selectedWaypoint
            )

        End If

    End Sub


    ' =====================================================
    ' Delete manuscript
    ' =====================================================

    Private Sub DeleteManuscript(
        manuscript As Manuscript
    )

        Using dialog As New DeleteManuscriptForm(
            manuscript.Title
        )

            If dialog.ShowDialog(Me) <>
                DialogResult.OK Then

                Return

            End If

        End Using

        manuscripts.Remove(
            manuscript
        )

        SaveManuscripts()
        RenderManuscripts()

    End Sub


    ' =====================================================
    ' File Drawer
    ' =====================================================

    Private Sub MoveToFileDrawer(
        manuscript As Manuscript
    )

        Dim result As DialogResult =
            MessageBox.Show(
                Me,
                "Move '" &
                manuscript.Title &
                "' to the File Drawer?" &
                Environment.NewLine &
                Environment.NewLine &
                "Its history, submissions, decisions, and correspondence will be preserved.",
                "Move to File Drawer",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question
            )

        If result <>
            DialogResult.Yes Then

            Return

        End If

        Dim reason As String =
            Microsoft.VisualBasic.Interaction.InputBox(
                "Optional: Why are you filing this manuscript?",
                "File Drawer",
                ""
            )

        manuscript.Location =
            ManuscriptLocation.FileDrawer

        manuscript.FileDrawerDate =
            DateTime.Now

        manuscript.FileDrawerReason =
            reason.Trim()

        Dim historyNote As String =
            "Moved to File Drawer."

        If Not String.IsNullOrWhiteSpace(
            reason
        ) Then

            historyNote &=
                " Reason: " &
                reason.Trim()

        End If

        Dim fileDrawerHistory As New HistoryEvent With {
            .EventDate =
                manuscript.FileDrawerDate.Value,
            .Stage =
                manuscript.CurrentStage,
            .Note =
                historyNote
        }

        ChronologyProvenanceService.StampCreated(
            fileDrawerHistory
        )

        manuscript.History.Add(
            fileDrawerHistory
        )

        SaveManuscripts()
        RenderManuscripts()

    End Sub


    Private Sub RestoreToPipeline(
        manuscript As Manuscript
    )

        manuscript.Location =
            ManuscriptLocation.Pipeline

        manuscript.FileDrawerDate =
            Nothing

        manuscript.FileDrawerReason =
            String.Empty

        Dim restoreHistory As New HistoryEvent With {
            .EventDate = DateTime.Now,
            .Stage =
                manuscript.CurrentStage,
            .Note =
                "Restored from File Drawer to active Pipeline."
        }

        ChronologyProvenanceService.StampCreated(
            restoreHistory
        )

        manuscript.History.Add(
            restoreHistory
        )

        SaveManuscripts()
        RenderManuscripts()

    End Sub

    ' =====================================================
    ' Standard Excel template
    ' =====================================================

    Private Sub ExportBlankTemplate(
    sender As Object,
    e As EventArgs
)

        Using dialog As New SaveFileDialog()

            dialog.Title =
            "Save PaperRoute Import Template"

            dialog.Filter =
            "Excel workbook (*.xlsx)|*.xlsx"

            dialog.DefaultExt =
            "xlsx"

            dialog.AddExtension =
            True

            dialog.FileName =
            "PaperRoute_Import_Template.xlsx"

            dialog.OverwritePrompt =
            True

            If dialog.ShowDialog(Me) <> DialogResult.OK Then
                Return
            End If

            Try

                Dim generator As New StandardTemplateGenerator()

                generator.Generate(
                dialog.FileName
            )

                MessageBox.Show(
                Me,
                "The PaperRoute import template was created successfully." &
                Environment.NewLine &
                Environment.NewLine &
                dialog.FileName,
                "Template Created",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information
            )

            Catch ex As Exception

                MessageBox.Show(
                Me,
                "PaperRoute could not create the Excel template." &
                Environment.NewLine &
                Environment.NewLine &
                ex.Message,
                "Template Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error
            )

            End Try

        End Using

    End Sub


    ' =====================================================
    ' Export complete library
    ' =====================================================

    Private Sub ExportLibraryExcel(
    sender As Object,
    e As EventArgs
)


        If manuscripts.Count = 0 Then

            MessageBox.Show(
            Me,
            "There are no manuscripts to export.",
            "Nothing to Export",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information
        )

            Return

        End If

        Using dialog As New SaveFileDialog()

            dialog.Title =
            "Export PaperRoute Library"

            dialog.Filter =
            "Excel workbook (*.xlsx)|*.xlsx"

            dialog.DefaultExt =
            "xlsx"

            dialog.AddExtension =
            True

            dialog.FileName =
            "PaperRoute_Export_" &
            DateTime.Now.ToString("yyyy-MM-dd") &
            ".xlsx"

            dialog.OverwritePrompt =
            True

            If dialog.ShowDialog(Me) <> DialogResult.OK Then
                Return
            End If

            Try

                Dim exporter As New LibraryExcelExporter()

                exporter.Export(
                dialog.FileName,
                manuscripts
            )

                MessageBox.Show(
                Me,
                "Your PaperRoute library was exported successfully." &
                Environment.NewLine &
                Environment.NewLine &
                manuscripts.Count.ToString() &
                " manuscript(s)" &
                Environment.NewLine &
                Environment.NewLine &
                dialog.FileName &
                Environment.NewLine &
                Environment.NewLine &
                "Note: The workbook contains correspondence metadata and file paths. It does not embed the actual document files.",
                "Export Complete",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information
            )

            Catch ex As Exception

                MessageBox.Show(
                Me,
                "PaperRoute could not export the library." &
                Environment.NewLine &
                Environment.NewLine &
                ex.Message,
                "Export Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error
            )

            End Try

        End Using

    End Sub

    ' =====================================================
    ' Portable backup
    ' =====================================================

    Private Sub BackupLibrary(
    sender As Object,
    e As EventArgs
)

        If manuscripts.Count = 0 Then

            MessageBox.Show(
            Me,
            "There are no manuscripts to back up.",
            "Nothing to Back Up",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information
        )

            Return

        End If

        ' Make absolutely sure JSON and managed copies are current.
        If Not SaveManuscripts() Then
            Return
        End If

        Using dialog As New SaveFileDialog()

            dialog.Title =
            "Back Up PaperRoute Library"

            dialog.Filter =
            "ZIP archive (*.zip)|*.zip"

            dialog.DefaultExt =
            "zip"

            dialog.AddExtension =
            True

            dialog.FileName =
            "PaperRoute_Backup_" &
            DateTime.Now.ToString("yyyy-MM-dd_HHmmss") &
            ".zip"

            dialog.OverwritePrompt =
            True

            If dialog.ShowDialog(Me) <> DialogResult.OK Then
                Return
            End If

            Try

                Dim backupService As New PortableBackupService()

                backupService.CreateBackup(
                dialog.FileName,
                manuscripts,
                repository
            )

                MessageBox.Show(
                Me,
                "Your PaperRoute library was backed up successfully." &
                Environment.NewLine &
                Environment.NewLine &
                dialog.FileName &
                Environment.NewLine &
                Environment.NewLine &
                "The backup contains:" &
                Environment.NewLine &
                "- Native PaperRoute data" &
                Environment.NewLine &
                "- Excel library export" &
                Environment.NewLine &
                "- Managed document files",
                "Backup Complete",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information
            )

            Catch ex As Exception

                MessageBox.Show(
                Me,
                "PaperRoute could not create the backup." &
                Environment.NewLine &
                Environment.NewLine &
                ex.Message,
                "Backup Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error
            )

            End Try

        End Using

    End Sub

    ' =====================================================
    ' Portable restore
    ' =====================================================

    Private Sub RestoreLibraryBackup(
    sender As Object,
    e As EventArgs
)

        Using dialog As New OpenFileDialog()

            dialog.Title =
            "Restore PaperRoute Backup"

            dialog.Filter =
            "PaperRoute backup (*.zip)|*.zip|ZIP archives (*.zip)|*.zip"

            dialog.CheckFileExists =
            True

            dialog.Multiselect =
            False

            If dialog.ShowDialog(Me) <> DialogResult.OK Then
                Return
            End If

            Dim restoreService As New PortableRestoreService()
            Dim inspection As BackupInspection

            Try

                inspection =
                restoreService.InspectBackup(
                    dialog.FileName
                )

            Catch ex As Exception

                MessageBox.Show(
                Me,
                "This backup could not be validated." &
                Environment.NewLine &
                Environment.NewLine &
                ex.Message,
                "Invalid Backup",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error
            )

                Return

            End Try

            Dim sizeInMb As Double =
            inspection.UncompressedBytes / 1024.0 / 1024.0

            Dim preview As String =
            "Restore this PaperRoute backup?" &
            Environment.NewLine &
            Environment.NewLine &
            "Manuscripts: " &
            inspection.ManuscriptCount.ToString() &
            Environment.NewLine &
            "Submissions: " &
            inspection.SubmissionCount.ToString() &
            Environment.NewLine &
            "Editorial decisions: " &
            inspection.DecisionCount.ToString() &
            Environment.NewLine &
            "Correspondence records: " &
            inspection.CorrespondenceCount.ToString() &
            Environment.NewLine &
            "Managed files: " &
            inspection.ManagedFileCount.ToString() &
            Environment.NewLine &
            "Archive entries: " &
            inspection.ArchiveEntryCount.ToString() &
            Environment.NewLine &
            "Expanded size: " &
            sizeInMb.ToString("N1") &
            " MB" &
            Environment.NewLine &
            Environment.NewLine &
            "IMPORTANT: This will REPLACE the library currently loaded in PaperRoute." &
            Environment.NewLine &
            Environment.NewLine &
            "An emergency backup of your current library will be created before the restore begins."

            Dim confirmation As DialogResult =
            MessageBox.Show(
                Me,
                preview,
                "Restore Backup",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning
            )

            If confirmation <> DialogResult.Yes Then
                Return
            End If

            Dim typedConfirmation As String =
            Microsoft.VisualBasic.Interaction.InputBox(
                "Type RESTORE to replace your current PaperRoute library.",
                "Confirm Restore",
                ""
            )

            If Not String.Equals(
            typedConfirmation.Trim(),
            "RESTORE",
            StringComparison.Ordinal
        ) Then

                MessageBox.Show(
                Me,
                "Restore cancelled. The confirmation text did not match RESTORE.",
                "Restore Cancelled",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information
            )

                Return

            End If

            Try

                Dim restoreResult As RestoreResult =
                restoreService.RestoreBackup(
                    dialog.FileName,
                    manuscripts,
                    repository
                )

                manuscripts =
                repository.Load()

                RenderManuscripts()

                Dim completionMessage As String =
                "The PaperRoute backup was restored successfully." &
                Environment.NewLine &
                Environment.NewLine &
                restoreResult.ManuscriptCount.ToString() &
                " manuscript(s) restored."

                If Not String.IsNullOrWhiteSpace(
                restoreResult.EmergencyBackupPath
            ) Then

                    completionMessage &=
                    Environment.NewLine &
                    Environment.NewLine &
                    "Your previous library was backed up automatically to:" &
                    Environment.NewLine &
                    restoreResult.EmergencyBackupPath

                End If

                MessageBox.Show(
                Me,
                completionMessage,
                "Restore Complete",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information
            )

                lblStatus.Text =
                "Backup restored - " &
                DateTime.Now.ToString("h:mm tt")

            Catch ex As Exception

                MessageBox.Show(
                Me,
                "PaperRoute could not restore the backup." &
                Environment.NewLine &
                Environment.NewLine &
                ex.Message &
                Environment.NewLine &
                Environment.NewLine &
                "The existing library was preserved or rolled back where possible.",
                "Restore Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error
            )

            End Try

        End Using

    End Sub

    ' =====================================================
    ' Excel import
    ' =====================================================

    Private Sub ImportExcelHistory(
    sender As Object,
    e As EventArgs
)

        Using dialog As New OpenFileDialog()

            dialog.Title = "Import Spreadsheet into PaperRoute"
            dialog.Filter = "Excel workbooks (*.xlsx;*.xlsm)|*.xlsx;*.xlsm|All files (*.*)|*.*"
            dialog.CheckFileExists = True
            dialog.Multiselect = False

            If dialog.ShowDialog(Me) <> DialogResult.OK Then
                Return
            End If

            Dim importResult As ExcelImportResult
            Dim detectedFormat As String

            Try

                Dim isStandardTemplate As Boolean = False

                Using workbook As New ClosedXML.Excel.XLWorkbook(dialog.FileName)

                    Dim hasManuscripts As Boolean = False
                    Dim hasSubmissions As Boolean = False
                    Dim hasDecisions As Boolean = False
                    Dim hasCorrespondence As Boolean = False

                    For Each worksheet As ClosedXML.Excel.IXLWorksheet In workbook.Worksheets

                        Select Case worksheet.Name.Trim().ToUpperInvariant()

                            Case "MANUSCRIPTS"
                                hasManuscripts = True

                            Case "SUBMISSIONS"
                                hasSubmissions = True

                            Case "DECISIONS"
                                hasDecisions = True

                            Case "CORRESPONDENCE"
                                hasCorrespondence = True

                        End Select

                    Next

                    isStandardTemplate =
                    hasManuscripts AndAlso
                    hasSubmissions AndAlso
                    hasDecisions AndAlso
                    hasCorrespondence

                End Using

                If isStandardTemplate Then

                    Dim importer As New StandardExcelImporter()

                    importResult = importer.Import(dialog.FileName)
                    detectedFormat = "Standard PaperRoute template"

                Else

                    Dim legacyImporter As New LegacyExcelImporter()

                    If legacyImporter.CanImport(dialog.FileName) Then

                        importResult = legacyImporter.Import(dialog.FileName)
                        detectedFormat = "Legacy tracker"

                    Else

                        Using mappingDialog As New ExcelMappingForm(dialog.FileName)

                            If mappingDialog.ShowDialog(Me) <> DialogResult.OK Then
                                Return
                            End If

                            Dim flexibleImporter As New FlexibleExcelImporter()

                            importResult =
                                flexibleImporter.Import(
                                    dialog.FileName,
                                    mappingDialog.SelectedWorksheetName,
                                    mappingDialog.HeaderRow,
                                    mappingDialog.Mappings
                                )

                            detectedFormat = "Mapped spreadsheet"

                        End Using

                    End If

                End If

            Catch ex As Exception

                MessageBox.Show(
                Me,
                "PaperRoute could not read this workbook." &
                Environment.NewLine &
                Environment.NewLine &
                ex.Message,
                "Excel Import Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error
            )

                Return

            End Try


            Dim existingTitles As New HashSet(Of String)(
            StringComparer.OrdinalIgnoreCase
        )

            For Each existingManuscript As Manuscript In manuscripts

                If Not String.IsNullOrWhiteSpace(existingManuscript.Title) Then
                    existingTitles.Add(existingManuscript.Title.Trim())
                End If

            Next


            Dim manuscriptsToAdd As New List(Of Manuscript)()
            Dim duplicateCount As Integer = 0

            For Each importedManuscript As Manuscript In importResult.Manuscripts

                If existingTitles.Contains(importedManuscript.Title.Trim()) Then

                    duplicateCount += 1

                Else

                    manuscriptsToAdd.Add(importedManuscript)

                End If

            Next


            If manuscriptsToAdd.Count = 0 Then

                MessageBox.Show(
                Me,
                "No new manuscripts are available to import." &
                Environment.NewLine &
                Environment.NewLine &
                duplicateCount.ToString() &
                " manuscript(s) matched titles already in PaperRoute.",
                "Nothing to Import",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information
            )

                Return

            End If


            Dim submissionCount As Integer = 0
            Dim decisionCount As Integer = 0
            Dim correspondenceCount As Integer = 0

            For Each importedManuscript As Manuscript In manuscriptsToAdd

                submissionCount += importedManuscript.Submissions.Count

                For Each submission As JournalSubmission In importedManuscript.Submissions

                    decisionCount += submission.Decisions.Count
                    correspondenceCount += submission.Correspondence.Count

                Next

            Next


            Dim preview As String =
            "Detected format: " &
            detectedFormat &
            Environment.NewLine &
            Environment.NewLine &
            "Manuscripts to add: " &
            manuscriptsToAdd.Count.ToString() &
            Environment.NewLine &
            "Journal submissions: " &
            submissionCount.ToString() &
            Environment.NewLine &
            "Editorial decisions: " &
            decisionCount.ToString() &
            Environment.NewLine &
            "Correspondence/files: " &
            correspondenceCount.ToString()


            If duplicateCount > 0 Then

                preview &=
                Environment.NewLine &
                "Existing manuscript titles skipped: " &
                duplicateCount.ToString()

            End If


            If importResult.Warnings.Count > 0 Then

                preview &=
                Environment.NewLine &
                "Import warnings: " &
                importResult.Warnings.Count.ToString()

                Dim warningLimit As Integer =
                Math.Min(5, importResult.Warnings.Count)

                preview &=
                Environment.NewLine &
                Environment.NewLine &
                "First warnings:"

                For i As Integer = 0 To warningLimit - 1

                    preview &=
                    Environment.NewLine &
                    "- " &
                    importResult.Warnings(i)

                Next

                If importResult.Warnings.Count > warningLimit Then

                    preview &=
                    Environment.NewLine &
                    "- ...and " &
                    (importResult.Warnings.Count - warningLimit).ToString() &
                    " more."

                End If

            End If


            preview &=
            Environment.NewLine &
            Environment.NewLine &
            "Import these records?"


            Dim confirmation As DialogResult =
            MessageBox.Show(
                Me,
                preview,
                "Excel Import Preview",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question
            )

            If confirmation <> DialogResult.Yes Then
                Return
            End If


            Try

                repository.CreatePreImportBackup()

            Catch ex As Exception

                MessageBox.Show(
                Me,
                "PaperRoute could not create the pre-import backup." &
                Environment.NewLine &
                Environment.NewLine &
                ex.Message &
                Environment.NewLine &
                Environment.NewLine &
                "The import has been cancelled.",
                "Backup Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error
            )

                Return

            End Try


            Dim importedAtUtc As DateTime =
                DateTime.UtcNow

            For Each importedManuscript As Manuscript In manuscriptsToAdd

                ChronologyProvenanceService.StampImportedManuscript(
                    importedManuscript,
                    importedAtUtc
                )

                manuscripts.Add(
                    importedManuscript
                )

            Next


            If Not SaveManuscripts() Then

                For Each importedManuscript As Manuscript In manuscriptsToAdd
                    manuscripts.Remove(importedManuscript)
                Next

                Return

            End If


            RenderManuscripts()

            MessageBox.Show(
            Me,
            manuscriptsToAdd.Count.ToString() &
            " manuscript(s) were imported successfully." &
            Environment.NewLine &
            Environment.NewLine &
            "Format: " &
            detectedFormat,
            "Import Complete",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information
        )

        End Using

    End Sub

    ' =====================================================
    ' Updates
    ' =====================================================

    Private Async Sub BeginAutomaticUpdateCheck()

        ' Let the main window finish rendering before any update prompt appears.
        Await Task.Delay(1500)

        Await UpdateService.CheckAndOfferUpdateAsync(
            Me,
            appSettings.UpdateChannel,
            False
        )

    End Sub


    Private Async Sub CheckForUpdatesNow(
        sender As Object,
        e As EventArgs
    )

        Await UpdateService.CheckAndOfferUpdateAsync(
            Me,
            appSettings.UpdateChannel,
            True
        )

    End Sub


    ' =====================================================
    ' Settings
    ' =====================================================


    Private Sub OpenSettings(
    sender As Object,
    e As EventArgs
)

        Using dialog As New SettingsForm(
        appSettings
    )

            If dialog.ShowDialog(Me) <>
            DialogResult.OK Then

                Return

            End If

            RenderManuscripts()

            If dialog.AppearanceChanged Then

                Dim restartResult As DialogResult =
                MessageBox.Show(
                    Me,
                    "Restart PaperRoute now to apply the new appearance?",
                    "Restart Required",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question
                )

                If restartResult =
                DialogResult.Yes Then

                    Application.Restart()

                End If

            End If

        End Using

    End Sub


    Private Sub OpenDiagnostics(
        sender As Object,
        e As EventArgs
    )

        Using dialog As New DiagnosticsForm(
            appSettings
        )

            dialog.ShowDialog(Me)

        End Using

    End Sub

    ' =====================================================
    ' Formatting
    ' =====================================================

    Private Function FormatStage(
        stage As PaperStage
    ) As String

        Select Case stage

            Case PaperStage.Idea
                Return "Idea"

            Case PaperStage.Draft
                Return "Draft"

            Case PaperStage.Submitted
                Return "Submitted"

            Case PaperStage.UnderReview
                Return "Under Review"

            Case PaperStage.Revision
                Return "Revision"

            Case PaperStage.Accepted
                Return "Accepted"

            Case PaperStage.InPress
                Return "In Press"

            Case PaperStage.Published
                Return "Published"

            Case Else
                Return stage.ToString()

        End Select

    End Function

End Class
