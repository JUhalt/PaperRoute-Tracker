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

    Private ReadOnly lblRevisionDueSoon As New Label()

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

    Private ReadOnly lblPipelineHeader As New Label()
    Private ReadOnly lblPublishedHeader As New Label()
    Private ReadOnly lblFileDrawerHeader As New Label()

    Private ReadOnly lblStatus As New Label()

    Private ReadOnly txtBoardSearch As New TextBox()
    Private ReadOnly cboStageFilter As New ComboBox()
    Private ReadOnly cboBoardSort As New ComboBox()
    Private ReadOnly btnClearBoardFilters As New Button()

    Private ReadOnly lblAttentionTitle As New Label()
    Private ReadOnly lblOverdueRevisions As New Label()
    Private ReadOnly lblLongReviews As New Label()
    Private ReadOnly lblMissingJournal As New Label()
    Private ReadOnly lblRecentRejections As New Label()

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
    Private routeLinkFont As Font = Nothing
    Private attentionTitleFont As Font = Nothing
    Private attentionRegularFont As Font = Nothing
    Private attentionActiveFont As Font = Nothing
    Private sectionHeaderFont As Font = Nothing


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

        TryShowStartupReminderNotification()

        If appSettings.CheckForUpdatesAutomatically Then
            BeginAutomaticUpdateCheck()
        End If

    End Sub

    ' =====================================================
    ' Interface
    ' =====================================================

    Private Sub BuildInterface()

        Me.Controls.Clear()

        Me.Text =
            If(
                StorageEnvironment.IsDevelopmentProfile(),
                "PaperRoute Tracker [Development]",
                "PaperRoute Tracker"
            )
        Me.StartPosition = FormStartPosition.CenterScreen
        Me.Size = New Size(1180, 820)
        Me.MinimumSize = New Size(900, 680)
        Me.Font = New Font("Segoe UI", 10.0F)
        Me.AutoScaleMode = AutoScaleMode.Dpi
        Me.BackColor = UiTheme.BoardBackground()
        Me.DoubleBuffered = True

        ResetSharedDashboardFonts()

        Dim root As New TableLayoutPanel With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 1,
            .RowCount = 3,
            .BackColor = UiTheme.BoardBackground()
        }

        root.ColumnStyles.Add(
            New ColumnStyle(
                SizeType.Percent,
                100.0F
            )
        )

        root.RowStyles.Add(New RowStyle(SizeType.AutoSize))
        root.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
        root.RowStyles.Add(New RowStyle(SizeType.AutoSize))

        ' =================================================
        ' Header
        ' =================================================

        Dim header As New TableLayoutPanel With {
    .Dock = DockStyle.Top,
    .AutoSize = True,
    .AutoSizeMode = AutoSizeMode.GrowAndShrink,
    .ColumnCount = 2,
    .RowCount = 1,
    .Padding = New Padding(18, 10, 18, 10),
    .BackColor = UiTheme.HeaderBackground()
}

        header.ColumnStyles.Add(
    New ColumnStyle(SizeType.Percent, 100)
)

        header.ColumnStyles.Add(
    New ColumnStyle(SizeType.AutoSize)
)

        header.RowStyles.Add(
    New RowStyle(SizeType.AutoSize)
)

        ' =================================================
        ' Branding
        ' =================================================

        Dim branding As New TableLayoutPanel With {
    .Dock = DockStyle.Fill,
    .AutoSize = True,
    .AutoSizeMode = AutoSizeMode.GrowAndShrink,
    .ColumnCount = 1,
    .RowCount = 2,
    .Margin = New Padding(0)
}

        branding.RowStyles.Add(
    New RowStyle(SizeType.AutoSize)
)

        branding.RowStyles.Add(
    New RowStyle(SizeType.AutoSize)
)


        Dim lblTitle As New Label With {
    .Text = "PaperRoute",
    .AutoSize = True,
    .Anchor = AnchorStyles.Left,
    .Margin = New Padding(0),
    .Cursor = Cursors.Hand,
    .Font = New Font(
        Me.Font.FontFamily,
        16.0F,
        FontStyle.Bold
    ),
    .ForeColor = UiTheme.AccentColor()
}

        Dim lblSubtitle As New Label With {
    .Text =
        If(
            StorageEnvironment.IsDevelopmentProfile(),
            "Development profile • isolated local data",
            "Academic manuscript tracking, locally"
        ),
    .AutoSize = True,
    .Anchor = AnchorStyles.Left,
    .ForeColor = UiTheme.SecondaryText(),
    .Margin = New Padding(0, 2, 0, 0)
}

        branding.Controls.Add(
    lblTitle,
    0,
    0
)

        branding.Controls.Add(
    lblSubtitle,
    0,
    1
)


        ' =================================================
        ' Header actions
        ' =================================================

        Dim headerActions As New TableLayoutPanel With {
    .AutoSize = True,
    .AutoSizeMode = AutoSizeMode.GrowAndShrink,
    .Anchor = AnchorStyles.Right,
    .ColumnCount = 4,
    .RowCount = 1,
    .Margin = New Padding(12, 0, 0, 0),
    .BackColor = UiTheme.HeaderBackground()
}

        headerActions.RowStyles.Add(
    New RowStyle(SizeType.AutoSize)
)

        For actionColumn As Integer = 0 To 3

            headerActions.ColumnStyles.Add(
        New ColumnStyle(SizeType.AutoSize)
    )

        Next


        Dim btnAdd As New Button With {
    .Text = "Add Manuscript",
    .Width = GetResponsiveButtonWidth("Add Manuscript", 150),
    .Height = GetResponsiveButtonHeight(44),
    .Margin = New Padding(6, 0, 0, 0)
}

        Dim btnData As New Button With {
    .Text = "Data ▾",
    .Width = GetResponsiveButtonWidth("Data ▾", 110),
    .Height = GetResponsiveButtonHeight(44),
    .Margin = New Padding(6, 0, 0, 0)
}

        Dim btnHelp As New Button With {
    .Text = "Help",
    .Width = GetResponsiveButtonWidth("Help", 88),
    .Height = GetResponsiveButtonHeight(44),
    .Margin = New Padding(0)
}

        Dim btnSettings As New Button With {
    .Text = "Settings ▾",
    .Width = GetResponsiveButtonWidth("Settings ▾", 118),
    .Height = GetResponsiveButtonHeight(44),
    .Margin = New Padding(6, 0, 0, 0)
}


        ' =================================================
        ' Data menu
        ' =================================================

        Dim dataMenu As New ContextMenuStrip()

        dataMenu.Items.Add(
    "Get Import Template...",
    Nothing,
    AddressOf ExportBlankTemplate
)

        dataMenu.Items.Add(
    "Import Spreadsheet...",
    Nothing,
    AddressOf ImportExcelHistory
)

        dataMenu.Items.Add(
    "Export Library to Excel...",
    Nothing,
    AddressOf ExportLibraryExcel
)

        dataMenu.Items.Add(
    New ToolStripSeparator()
)

        dataMenu.Items.Add(
    "Import BibTeX / RIS...",
    Nothing,
    AddressOf ImportBibliography
)

        dataMenu.Items.Add(
    "Export Library as BibTeX...",
    Nothing,
    AddressOf ExportBibTeX
)

        dataMenu.Items.Add(
    "Export Library as RIS...",
    Nothing,
    AddressOf ExportRis
)

        dataMenu.Items.Add(
    "Authors & Affiliations...",
    Nothing,
    AddressOf OpenAuthorLibrary
)

        dataMenu.Items.Add(
    "Journal Library...",
    Nothing,
    AddressOf OpenJournalLibrary
)

        dataMenu.Items.Add(
    "Publication & CV Export...",
    Nothing,
    AddressOf OpenPublicationExport
)

        dataMenu.Items.Add(
    New ToolStripSeparator()
)

        dataMenu.Items.Add(
    "Backup Library...",
    Nothing,
    AddressOf BackupLibrary
)

        dataMenu.Items.Add(
    "Restore Backup...",
    Nothing,
    AddressOf RestoreLibraryBackup
)

        ' =================================================
        ' Settings menu
        ' =================================================

        Dim settingsMenu As New ContextMenuStrip()

        settingsMenu.Items.Add(
    "Preferences...",
    Nothing,
    AddressOf OpenSettings
)

        settingsMenu.Items.Add(
    "Reminders && Calendar...",
    Nothing,
    AddressOf OpenReminders
)

        settingsMenu.Items.Add(
    New ToolStripSeparator()
)

        settingsMenu.Items.Add(
    "Check for Updates...",
    Nothing,
    AddressOf CheckForUpdatesNow
)

        settingsMenu.Items.Add(
    "Diagnostics...",
    Nothing,
    AddressOf OpenDiagnostics
)

        settingsMenu.Items.Add(
    New ToolStripSeparator()
)

        settingsMenu.Items.Add(
    "User Guide...",
    Nothing,
    AddressOf OpenUserGuide
)

        settingsMenu.Items.Add(
    "About PaperRoute",
    Nothing,
    AddressOf OpenAbout
)

        ' =================================================
        ' Header handlers
        ' =================================================

        AddHandler lblTitle.DoubleClick,
    AddressOf OpenAbout

        AddHandler btnAdd.Click,
    AddressOf AddManuscript

        AddHandler btnHelp.Click,
    AddressOf OpenUserGuide

        AddHandler btnSettings.Click,
    Sub(sender, e)

        settingsMenu.Show(
            btnSettings,
            New Point(
                0,
                btnSettings.Height
            )
        )

    End Sub

        AddHandler btnData.Click,
    Sub(sender, e)

        dataMenu.Show(
            btnData,
            New Point(
                0,
                btnData.Height
            )
        )

    End Sub

        StyleCardButton(
            btnAdd,
            UiTheme.AccentColor()
        )

        StyleCardButton(
            btnData,
            UiTheme.AccentSecondaryColor()
        )

        StyleCardButton(
            btnHelp,
            UiTheme.SecondaryText()
        )

        StyleCardButton(
            btnSettings,
            UiTheme.SecondaryText()
        )


        headerActions.Controls.Add(
    btnHelp,
    0,
    0
)

        headerActions.Controls.Add(
    btnSettings,
    1,
    0
)

        headerActions.Controls.Add(
    btnData,
    2,
    0
)

        headerActions.Controls.Add(
    btnAdd,
    3,
    0
)

        ' =================================================
        ' Assemble header        ' =================================================
        ' Assemble header
        ' =================================================

        header.Controls.Add(
    branding,
    0,
    0
)

        header.Controls.Add(
    headerActions,
    1,
    0
)

        ' =================================================
        ' Main body
        ' =================================================

        Dim body As New TableLayoutPanel With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 1,
            .RowCount = 8,
            .Padding = New Padding(18, 8, 18, 12),
            .BackColor = UiTheme.BoardBackground(),
            .AutoScroll = False
        }

        body.ColumnStyles.Add(
            New ColumnStyle(
                SizeType.Percent,
                100.0F
            )
        )

        ' The dashboard is built at runtime. At high Windows scaling, fonts
        ' grow even though hard-coded pixel row heights do not. Keep all
        ' text/tool rows content-driven and reserve the percentage rows for
        ' the manuscript shelves themselves.
        body.RowStyles.Add(New RowStyle(SizeType.AutoSize))
        body.RowStyles.Add(New RowStyle(SizeType.AutoSize))

        body.RowStyles.Add(New RowStyle(SizeType.AutoSize))
        body.RowStyles.Add(New RowStyle(SizeType.Percent, 40))

        body.RowStyles.Add(New RowStyle(SizeType.AutoSize))
        body.RowStyles.Add(New RowStyle(SizeType.Percent, 30))

        body.RowStyles.Add(New RowStyle(SizeType.AutoSize))
        body.RowStyles.Add(New RowStyle(SizeType.Percent, 30))


        ' =================================================
        ' Search / filter / sort bar
        ' =================================================

        Dim attentionBar As New FlowLayoutPanel With {
    .Dock = DockStyle.Top,
    .AutoSize = True,
    .AutoSizeMode = AutoSizeMode.GrowAndShrink,
    .FlowDirection = FlowDirection.LeftToRight,
    .WrapContents = True,
    .Padding = New Padding(0, 7, 0, 3),
    .Margin = New Padding(0),
    .BackColor = UiTheme.BoardBackground()
}

        lblAttentionTitle.Text =
    "NEEDS ATTENTION"

        lblAttentionTitle.AutoSize =
    True

        lblAttentionTitle.Font =
            attentionTitleFont

        lblAttentionTitle.ForeColor =
    UiTheme.PrimaryText()

        lblAttentionTitle.Margin =
    New Padding(0, 3, 16, 0)

        ConfigureAttentionLabel(
    lblOverdueRevisions
)

        ConfigureAttentionLabel(
    lblRevisionDueSoon
)

        AddHandler lblOverdueRevisions.Click,
    AddressOf FilterOverdueRevisions

        AddHandler lblRevisionDueSoon.Click,
    AddressOf FilterRevisionDueSoon

        AddHandler lblLongReviews.Click,
    AddressOf FilterLongReviews

        AddHandler lblMissingJournal.Click,
    AddressOf FilterMissingJournal

        AddHandler lblRecentRejections.Click,
    AddressOf FilterRecentRejections

        ConfigureAttentionLabel(
    lblLongReviews
)

        ConfigureAttentionLabel(
    lblMissingJournal
)

        ConfigureAttentionLabel(
    lblRecentRejections
)


        attentionBar.Controls.Add(
    lblAttentionTitle
)

        attentionBar.Controls.Add(
    lblOverdueRevisions
)

        attentionBar.Controls.Add(
    lblRevisionDueSoon
)

        attentionBar.Controls.Add(
    lblLongReviews
)

        attentionBar.Controls.Add(
    lblMissingJournal
)

        attentionBar.Controls.Add(
    lblRecentRejections
)

        Dim boardToolbar As New FlowLayoutPanel With {
            .Dock = DockStyle.Top,
            .AutoSize = True,
            .AutoSizeMode = AutoSizeMode.GrowAndShrink,
            .FlowDirection = FlowDirection.LeftToRight,
            .WrapContents = True,
            .Padding = New Padding(0, 6, 0, 4),
            .Margin = New Padding(0),
            .BackColor = UiTheme.BoardBackground()
        }


        txtBoardSearch.PlaceholderText =
            "Search title, journal, or authors..."

        txtBoardSearch.Width =
            Math.Max(
                310,
                TextRenderer.MeasureText(
                    txtBoardSearch.PlaceholderText,
                    Me.Font
                ).Width + 28
            )

        txtBoardSearch.Margin =
            New Padding(0, 2, 10, 0)
        txtBoardSearch.BackColor = UiTheme.CardBackground()
        txtBoardSearch.ForeColor = UiTheme.PrimaryText()
        txtBoardSearch.BorderStyle = BorderStyle.FixedSingle


        cboStageFilter.Width =
            Math.Max(
                150,
                TextRenderer.MeasureText(
                    "Under Review",
                    Me.Font
                ).Width + 46
            )
        cboStageFilter.DropDownStyle =
            ComboBoxStyle.DropDownList

        cboStageFilter.Items.Clear()
        RefreshStageFilterItems()

        cboStageFilter.Margin =
            New Padding(0, 2, 10, 0)
        cboStageFilter.BackColor = UiTheme.CardBackground()
        cboStageFilter.ForeColor = UiTheme.PrimaryText()
        cboStageFilter.FlatStyle = FlatStyle.Flat


        cboBoardSort.Width =
            Math.Max(
                235,
                TextRenderer.MeasureText(
                    "Sort: Newest stage change",
                    Me.Font
                ).Width + 46
            )
        cboBoardSort.DropDownWidth = cboBoardSort.Width
        cboBoardSort.DropDownStyle =
            ComboBoxStyle.DropDownList

        cboBoardSort.Items.Clear()

        cboBoardSort.Items.Add("Sort: Current order")
        cboBoardSort.Items.Add("Sort: Title A-Z")
        cboBoardSort.Items.Add("Sort: Title Z-A")
        cboBoardSort.Items.Add("Sort: Most rejections")
        cboBoardSort.Items.Add("Sort: Fewest rejections")
        cboBoardSort.Items.Add("Sort: Newest stage change")
        cboBoardSort.Items.Add("Sort: Oldest stage change")

        cboBoardSort.SelectedIndex = 0

        cboBoardSort.Margin =
            New Padding(0, 2, 10, 0)
        cboBoardSort.BackColor = UiTheme.CardBackground()
        cboBoardSort.ForeColor = UiTheme.PrimaryText()
        cboBoardSort.FlatStyle = FlatStyle.Flat


        btnClearBoardFilters.Text = "Clear"
        btnClearBoardFilters.Width = GetResponsiveButtonWidth("Clear", 80)
        btnClearBoardFilters.Height = GetResponsiveButtonHeight(30)
        btnClearBoardFilters.Enabled = False
        btnClearBoardFilters.Margin =
            New Padding(0, 1, 0, 0)

        StyleCardButton(
            btnClearBoardFilters,
            UiTheme.SecondaryText()
        )


        AddHandler boardSearchDebounceTimer.Tick,
            AddressOf BoardSearchDebounceElapsed

        AddHandler txtBoardSearch.TextChanged,
            AddressOf BoardSearchTextChanged

        AddHandler cboStageFilter.SelectedIndexChanged,
            AddressOf BoardFilterChanged

        AddHandler cboBoardSort.SelectedIndexChanged,
            AddressOf BoardFilterChanged

        AddHandler btnClearBoardFilters.Click,
            AddressOf ClearBoardFilters


        boardToolbar.Controls.Add(
            txtBoardSearch
        )

        boardToolbar.Controls.Add(
            cboStageFilter
        )

        boardToolbar.Controls.Add(
            cboBoardSort
        )

        boardToolbar.Controls.Add(
            btnClearBoardFilters
        )

        ' =================================================
        ' Shelves
        ' =================================================

        ConfigureSectionHeader(
            lblPipelineHeader,
            "PIPELINE"
        )

        ConfigureSectionHeader(
            lblPublishedHeader,
            "PUBLISHED"
        )

        ConfigureSectionHeader(
            lblFileDrawerHeader,
            "FILE DRAWER"
        )

        ConfigureFlowPanel(
            pipelinePanel
        )

        ConfigureFlowPanel(
            publishedPanel
        )

        ConfigureFlowPanel(
            fileDrawerPanel
        )


        body.Controls.Add(
    attentionBar,
    0,
    0
)

        body.Controls.Add(
    boardToolbar,
    0,
    1
)

        body.Controls.Add(
    lblPipelineHeader,
    0,
    2
)

        body.Controls.Add(
    pipelinePanel,
    0,
    3
)

        body.Controls.Add(
    lblPublishedHeader,
    0,
    4
)

        body.Controls.Add(
    publishedPanel,
    0,
    5
)

        body.Controls.Add(
    lblFileDrawerHeader,
    0,
    6
)

        body.Controls.Add(
    fileDrawerPanel,
    0,
    7
)

        ' =================================================
        ' Footer
        ' =================================================

        lblStatus.Dock = DockStyle.Fill
        lblStatus.AutoSize = True
        lblStatus.TextAlign = ContentAlignment.MiddleLeft
        lblStatus.Padding = New Padding(18, 6, 0, 6)
        lblStatus.Margin = New Padding(0)
        lblStatus.ForeColor = SystemColors.GrayText
        lblStatus.Text = "Local storage enabled."

        root.Controls.Add(
            header,
            0,
            0
        )

        root.Controls.Add(
            body,
            0,
            1
        )

        root.Controls.Add(
            lblStatus,
            0,
            2
        )

        Me.Controls.Add(
            root
        )

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

        label.ForeColor =
        UiTheme.SecondaryText()

        label.Margin =
        New Padding(
            0,
            3,
            18,
            0
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
        UiTheme.DangerColor()
    )

        StyleAttentionLabel(
        lblRevisionDueSoon,
        dueSoonCount,
        AttentionFilter.RevisionDueSoon,
        UiTheme.WarningColor()
    )

        StyleAttentionLabel(
        lblLongReviews,
        longReviewCount,
        AttentionFilter.LongReview,
        UiTheme.WarningColor()
    )

        StyleAttentionLabel(
        lblMissingJournal,
        missingJournalCount,
        AttentionFilter.MissingTargetJournal,
        UiTheme.WarningColor()
    )

        StyleAttentionLabel(
        lblRecentRejections,
        recentRejectionCount,
        AttentionFilter.RecentRejection,
        UiTheme.DangerColor()
    )

    End Sub


    Private Sub StyleAttentionLabel(
    label As Label,
    count As Integer,
    filter As AttentionFilter,
    attentionColor As Color
)

        ' Keep zero-count labels enabled so WinForms does not replace
        ' our theme-aware foreground with the low-contrast disabled color.
        label.Enabled =
        True

        label.Tag =
        count

        If count = 0 Then

            label.ForeColor =
            UiTheme.SecondaryText()

            label.Cursor =
            Cursors.Default

        Else

            label.ForeColor =
            attentionColor

            label.Cursor =
            Cursors.Hand

        End If


        If activeAttentionFilter =
           filter Then

            label.Font =
                attentionActiveFont

        Else

            label.Font =
                attentionRegularFont

        End If

    End Sub

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
                " days for a decision"

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
                " ago • choose the next target"

        End If


        ' =================================================
        ' Missing target
        ' =================================================

        If snapshot.HasMissingTargetJournal Then

            insightColor =
            UiTheme.WarningColor()

            Return "No target journal selected"

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

            Return "Consider filing after " &
            snapshot.RejectionCount.ToString() &
            " rejections"

        End If


        Return String.Empty

    End Function

    Private Sub ConfigureSectionHeader(
    label As Label,
    text As String
)

        label.Text = text
        label.AutoSize = True
        label.Anchor = AnchorStyles.Left

        label.Font =
            sectionHeaderFont

        label.ForeColor =
        UiTheme.PrimaryText()

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


    Private Function LoadAuthorLibrary() As Boolean

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


    Private Function SaveManuscripts() As Boolean

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
    panel As FlowLayoutPanel
)

        panel.Dock = DockStyle.Fill

        ' Shelves are single vertical columns. Horizontal flow with explicit
        ' breaks can retain a two-card-wide extent when restoring from a wide
        ' window, despite placing every card on its own row. The shelf control
        ' reconciles the scroll range after this vertical layout completes.
        panel.FlowDirection = FlowDirection.TopDown
        panel.WrapContents = False

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


    Private Sub StyleCardButton(
    button As Button,
    accentColor As Color
)

        button.FlatStyle =
        FlatStyle.Flat

        button.UseVisualStyleBackColor =
        False

        button.BackColor =
        UiTheme.CardBackground()

        button.ForeColor =
        accentColor

        button.FlatAppearance.BorderColor =
        accentColor

        button.FlatAppearance.BorderSize =
        1

        button.FlatAppearance.MouseOverBackColor =
        UiTheme.HoverBackground()

        button.FlatAppearance.MouseDownBackColor =
        UiTheme.HoverBackground()

        button.Cursor =
        Cursors.Hand

    End Sub

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
            )

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

        cardTitleFont =
            New Font(
                Me.Font.FontFamily,
                11.0F,
                FontStyle.Bold
            )

        cardBadgeFont =
            New Font(
                Me.Font.FontFamily,
                8.5F,
                FontStyle.Bold
            )

        cardInsightFont =
            New Font(
                Me.Font.FontFamily,
                9.0F,
                FontStyle.Bold
            )

        routeLinkFont =
            New Font(
                Me.Font,
                FontStyle.Underline
            )

        attentionTitleFont =
            New Font(
                Me.Font.FontFamily,
                9.0F,
                FontStyle.Bold
            )

        attentionRegularFont =
            New Font(
                Me.Font,
                FontStyle.Regular
            )

        attentionActiveFont =
            New Font(
                Me.Font,
                FontStyle.Bold Or
                FontStyle.Underline
            )

        sectionHeaderFont =
            New Font(
                Me.Font.FontFamily,
                10.0F,
                FontStyle.Bold
            )

    End Sub


    Private Sub DisposeSharedDashboardFonts()

        For Each font As Font In New Font() {
            cardTitleFont,
            cardBadgeFont,
            cardInsightFont,
            routeLinkFont,
            attentionTitleFont,
            attentionRegularFont,
            attentionActiveFont,
            sectionHeaderFont
        }

            If font IsNot Nothing Then
                font.Dispose()
            End If

        Next

        cardTitleFont = Nothing
        cardBadgeFont = Nothing
        cardInsightFont = Nothing
        routeLinkFont = Nothing
        attentionTitleFont = Nothing
        attentionRegularFont = Nothing
        attentionActiveFont = Nothing
        sectionHeaderFont = Nothing

    End Sub


    Protected Overrides Sub OnFormClosed(
        e As FormClosedEventArgs
    )

        boardSearchDebounceTimer.Stop()
        boardSearchDebounceTimer.Dispose()
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

        lblPipelineHeader.Text =
        BuildSectionTitle(
            "PIPELINE",
            pipelineCount,
            pipelineTotal
        )

        lblPublishedHeader.Text =
        BuildSectionTitle(
            "PUBLISHED",
            publishedCount,
            publishedTotal
        )

        lblFileDrawerHeader.Text =
        BuildSectionTitle(
            "FILE DRAWER",
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

    End Sub


    Private Function CreateManuscriptCard(
        manuscript As Manuscript,
        parentPanel As FlowLayoutPanel
    ) As Panel

        Dim stageText As String =
            FormatStage(
                manuscript.CurrentStage
            )

        Dim snapshot As ManuscriptAttentionSnapshot =
            GetAttentionSnapshot(
                manuscript
            )

        Dim titleHeight As Integer =
            TextRenderer.MeasureText(
                "Ag",
                cardTitleFont
            ).Height

        Dim bodyTextHeight As Integer =
            TextRenderer.MeasureText(
                "Ag",
                Me.Font
            ).Height

        Dim badgeHeight As Integer =
            Math.Max(
                25,
                TextRenderer.MeasureText(
                    stageText.ToUpperInvariant(),
                    cardBadgeFont
                ).Height + 7
            )

        Dim buttonHeight As Integer =
            GetResponsiveButtonHeight(
                34
            )

        Dim titleTop As Integer =
            13

        Dim secondRowTop As Integer =
            titleTop +
            titleHeight +
            6

        Dim secondRowHeight As Integer =
            Math.Max(
                badgeHeight,
                bodyTextHeight + 4
            )

        Dim statsTop As Integer =
            secondRowTop +
            secondRowHeight +
            8

        Dim insightColor As Color

        Dim insightText As String =
            BuildManuscriptInsight(
                manuscript,
                insightColor
            )

        Dim insightTop As Integer =
            statsTop +
            bodyTextHeight +
            8

        Dim insightHeight As Integer =
            0

        If Not String.IsNullOrWhiteSpace(
            insightText
        ) Then

            insightHeight =
                TextRenderer.MeasureText(
                    "Ag",
                    cardInsightFont
                ).Height

        End If

        Dim actionsTop As Integer =
            If(
                insightHeight > 0,
                insightTop +
                insightHeight +
                10,
                statsTop +
                bodyTextHeight +
                10
            )

        Dim card As New RoundedPanel With {
            .Width =
                GetCardWidth(
                    parentPanel
                ),
            .BackColor =
                UiTheme.CardBackground(),
            .BorderColor =
                UiTheme.CardBorder(),
            .BorderThickness =
                1.0F,
            .CornerRadius =
                14,
            .Margin =
                New Padding(
                    4
                ),
            .Cursor =
                Cursors.Hand
        }


        ' =================================================
        ' Title
        ' =================================================

        Dim lblTitle As New Label With {
            .Text =
                manuscript.Title,
            .AutoEllipsis =
                True,
            .Left =
                18,
            .Top =
                titleTop,
            .Height =
                titleHeight + 2,
            .Font =
                cardTitleFont,
            .ForeColor =
                UiTheme.PrimaryText(),
            .Cursor =
                Cursors.Hand
        }


        ' =================================================
        ' Stage + journal
        ' =================================================

        Dim badgeWidth As Integer =
            TextRenderer.MeasureText(
                stageText.ToUpperInvariant(),
                cardBadgeFont
            ).Width + 22

        Dim stageBadge As New PillLabel With {
            .Text =
                stageText.ToUpperInvariant(),
            .Left =
                18,
            .Top =
                secondRowTop,
            .Width =
                badgeWidth,
            .Height =
                badgeHeight,
            .Font =
                cardBadgeFont,
            .BackColor =
                UiTheme.StageBackground(
                    manuscript.CurrentStage
                ),
            .ForeColor =
                UiTheme.StageForeground(
                    manuscript.CurrentStage
                )
        }

        Dim journalText As String =
            If(
                String.IsNullOrWhiteSpace(
                    manuscript.TargetJournal
                ),
                "Target journal not set",
                manuscript.TargetJournal
            )

        Dim lblJournal As New Label With {
            .Text =
                journalText,
            .AutoEllipsis =
                True,
            .Left =
                18 +
                badgeWidth +
                10,
            .Top =
                secondRowTop +
                Math.Max(
                    0,
                    (
                        secondRowHeight -
                        bodyTextHeight
                    ) \ 2
                ),
            .Height =
                bodyTextHeight + 3,
            .ForeColor =
                UiTheme.SecondaryText(),
            .Cursor =
                Cursors.Hand
        }


        ' =================================================
        ' Stats + Route
        ' =================================================

        Dim lblStats As New Label With {
            .Text =
                snapshot.SubmissionCount.ToString() &
                " submissions  •  " &
                snapshot.RejectionCount.ToString() &
                " rejections",
            .AutoEllipsis =
                True,
            .AutoSize =
                False,
            .Left =
                18,
            .Top =
                statsTop,
            .Height =
                bodyTextHeight + 4,
            .ForeColor =
                UiTheme.PrimaryText(),
            .Cursor =
                Cursors.Hand
        }

        Dim routeLinkText As String =
            "View route →"

        Dim routeLinkWidth As Integer =
            TextRenderer.MeasureText(
                routeLinkText,
                Me.Font
            ).Width + 4

        Dim lblRoute As New Label With {
            .Text =
                routeLinkText,
            .AutoSize =
                False,
            .Width =
                routeLinkWidth,
            .Height =
                bodyTextHeight + 4,
            .Top =
                statsTop,
            .TextAlign =
                ContentAlignment.MiddleRight,
            .ForeColor =
                UiTheme.AccentColor(),
            .Cursor =
                Cursors.Hand,
            .Font =
                routeLinkFont
        }


        ' =================================================
        ' Attention insight
        ' =================================================

        Dim lblInsight As Label =
            Nothing

        If Not String.IsNullOrWhiteSpace(
            insightText
        ) Then

            lblInsight =
                New Label With {
                    .Text =
                        insightText,
                    .AutoEllipsis =
                        True,
                    .AutoSize =
                        False,
                    .Left =
                        18,
                    .Top =
                        insightTop,
                    .Height =
                        insightHeight + 2,
                    .ForeColor =
                        insightColor,
                    .Font =
                        cardInsightFont
                }

        End If


        ' =================================================
        ' Actions
        '
        ' The supported main-window minimum leaves room for the three
        ' manuscript actions. A deterministic table keeps every button
        ' inside the visible card instead of letting FlowLayout preferred
        ' sizing create an invisible wider action surface.
        ' =================================================

        Dim btnOpen As New Button With {
            .Text =
                "Open",
            .Width =
                GetResponsiveButtonWidth(
                    "Open",
                    88
                ),
            .Height =
                buttonHeight,
            .Anchor =
                AnchorStyles.Right,
            .Margin =
                New Padding(
                    5,
                    0,
                    5,
                    6
                )
        }

        StyleCardButton(
            btnOpen,
            UiTheme.AccentColor()
        )

        AddHandler btnOpen.Click,
            Sub(sender, e)
                OpenManuscript(
                    manuscript
                )
            End Sub

        Dim locationButton As Button =
            Nothing

        If manuscript.Location =
           ManuscriptLocation.Pipeline Then

            locationButton =
                New Button With {
                    .Text =
                        "Move to File Drawer",
                    .Width =
                        GetResponsiveButtonWidth(
                            "Move to File Drawer",
                            175
                        ),
                    .Height =
                        buttonHeight,
                    .Anchor =
                        AnchorStyles.Right,
                    .Margin =
                        New Padding(
                            5,
                            0,
                            5,
                            6
                        )
                }

            StyleCardButton(
                locationButton,
                UiTheme.WarningColor()
            )

            AddHandler locationButton.Click,
                Sub(sender, e)
                    MoveToFileDrawer(
                        manuscript
                    )
                End Sub

        ElseIf manuscript.Location =
               ManuscriptLocation.FileDrawer Then

            locationButton =
                New Button With {
                    .Text =
                        "Restore to Pipeline",
                    .Width =
                        GetResponsiveButtonWidth(
                            "Restore to Pipeline",
                            175
                        ),
                    .Height =
                        buttonHeight,
                    .Anchor =
                        AnchorStyles.Right,
                    .Margin =
                        New Padding(
                            5,
                            0,
                            5,
                            6
                        )
                }

            StyleCardButton(
                locationButton,
                UiTheme.SuccessColor()
            )

            AddHandler locationButton.Click,
                Sub(sender, e)
                    RestoreToPipeline(
                        manuscript
                    )
                End Sub

        End If

        Dim btnDelete As New Button With {
            .Text =
                "Delete",
            .Width =
                GetResponsiveButtonWidth(
                    "Delete",
                    88
                ),
            .Height =
                buttonHeight,
            .Anchor =
                AnchorStyles.Right,
            .Margin =
                New Padding(
                    5,
                    0,
                    5,
                    6
                )
        }

        StyleCardButton(
            btnDelete,
            UiTheme.DangerColor()
        )

        AddHandler btnDelete.Click,
            Sub(sender, e)
                DeleteManuscript(
                    manuscript
                )
            End Sub

        Dim actionButtonCount As Integer =
            If(
                locationButton Is Nothing,
                2,
                3
            )

        Dim actionPanel As New TableLayoutPanel With {
            .Dock =
                DockStyle.Bottom,
            .ColumnCount =
                actionButtonCount + 1,
            .RowCount =
                1,
            .Height =
                buttonHeight + 14,
            .Padding =
                New Padding(
                    18,
                    0,
                    18,
                    8
                ),
            .Margin =
                New Padding(
                    0
                ),
            .BackColor =
                UiTheme.CardBackground()
        }

        actionPanel.RowStyles.Add(
            New RowStyle(
                SizeType.Percent,
                100
            )
        )

        actionPanel.ColumnStyles.Add(
            New ColumnStyle(
                SizeType.Percent,
                100
            )
        )

        For actionColumn As Integer =
            1 To actionButtonCount

            actionPanel.ColumnStyles.Add(
                New ColumnStyle(
                    SizeType.AutoSize
                )
            )

        Next

        Dim nextActionColumn As Integer =
            1

        actionPanel.Controls.Add(
            btnOpen,
            nextActionColumn,
            0
        )

        nextActionColumn +=
            1

        If locationButton IsNot Nothing Then

            actionPanel.Controls.Add(
                locationButton,
                nextActionColumn,
                0
            )

            nextActionColumn +=
                1

        End If

        actionPanel.Controls.Add(
            btnDelete,
            nextActionColumn,
            0
        )


        ' =================================================
        ' Navigation behavior
        ' =================================================

        AddHandler card.DoubleClick,
            Sub(sender, e)
                OpenManuscript(
                    manuscript
                )
            End Sub

        AddHandler lblTitle.DoubleClick,
            Sub(sender, e)
                OpenManuscript(
                    manuscript
                )
            End Sub

        AddHandler lblJournal.DoubleClick,
            Sub(sender, e)
                OpenManuscript(
                    manuscript
                )
            End Sub

        AddHandler lblStats.DoubleClick,
            Sub(sender, e)
                OpenManuscript(
                    manuscript
                )
            End Sub

        AddHandler lblRoute.Click,
            Sub(sender, e)
                OpenRouteView(
                    manuscript
                )
            End Sub


        ' =================================================
        ' Assemble
        ' =================================================

        card.Controls.Add(
            lblTitle
        )

        card.Controls.Add(
            stageBadge
        )

        card.Controls.Add(
            lblJournal
        )

        card.Controls.Add(
            lblStats
        )

        card.Controls.Add(
            lblRoute
        )

        If lblInsight IsNot Nothing Then

            card.Controls.Add(
                lblInsight
            )

        End If

        card.Controls.Add(
            actionPanel
        )


        ' =================================================
        ' Responsive layout
        ' =================================================

        Dim applyingLayout As Boolean =
            False

        Dim applyResponsiveLayout As Action =
            Sub()

                If applyingLayout Then
                    Return
                End If

                applyingLayout =
                    True

                Try

                    Dim contentWidth As Integer =
                        Math.Max(
                            1,
                            card.ClientSize.Width -
                            36
                        )

                    lblTitle.Width =
                        contentWidth

                    lblJournal.Width =
                        Math.Max(
                            70,
                            contentWidth -
                            badgeWidth -
                            10
                        )

                    lblRoute.Left =
                        Math.Max(
                            18,
                            card.ClientSize.Width -
                            routeLinkWidth -
                            18
                        )

                    lblStats.Width =
                        Math.Max(
                            80,
                            lblRoute.Left -
                            lblStats.Left -
                            10
                        )

                    If lblInsight IsNot Nothing Then

                        lblInsight.Width =
                            contentWidth

                    End If

                    Dim desiredCardHeight As Integer =
                        actionsTop +
                        8 +
                        actionPanel.Height

                    If card.Height <>
                       desiredCardHeight Then

                        card.Height =
                            desiredCardHeight

                    End If

                Finally

                    applyingLayout =
                        False

                End Try

            End Sub

        AddHandler card.ClientSizeChanged,
            Sub(sender, e)
                applyResponsiveLayout()
            End Sub

        applyResponsiveLayout()

        Return card

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


    Private Function GetCardWidth(
        panel As FlowLayoutPanel
    ) As Integer

        Return DashboardResponsiveLayoutService.
            CalculateCardWidth(
                panel.ClientSize.Width,
                panel.Padding.Horizontal,
                SystemInformation.VerticalScrollBarWidth
            )

    End Function


    Private Sub ResizeCards(
        panel As FlowLayoutPanel
    )

        If panel Is Nothing OrElse
           panel.IsDisposed OrElse
           panel.ClientSize.Width <= 0 Then

            Return

        End If

        Dim newWidth As Integer =
            GetCardWidth(
                panel
            )

        panel.SuspendLayout()

        Try

            panel.AutoScrollMinSize =
                Size.Empty

            If panel.AutoScrollPosition.X <> 0 Then

                panel.AutoScrollPosition =
                    Point.Empty

            End If

            For Each control As Control In panel.Controls

                If TypeOf control Is Panel OrElse
                   TypeOf control Is Label Then

                    If control.Width <>
                       newWidth Then

                        control.Width =
                            newWidth

                    End If

                End If

            Next

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

        ' Pasting a title page matches authors against the reusable library.
        ' If the library cannot be read, Add Manuscript still works without it.
        Dim authorLibrary As AuthorLibraryData = Nothing

        Try
            authorLibrary = authorRepository.Load()
        Catch ex As Exception
            authorLibrary = Nothing
        End Try

        Using dialog As New AddManuscriptForm(authorLibrary)

            If dialog.ShowDialog(Me) =
                DialogResult.OK AndAlso
               dialog.CreatedManuscript IsNot Nothing Then

                ' Save reusable people first, as bibliography import does, so a
                ' manuscript never references an author record that failed to save.
                If dialog.AuthorLibraryChanged Then
                    Try
                        authorRepository.Save(authorLibrary)
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
                End If

                manuscripts.Add(
                    dialog.CreatedManuscript
                )

                SaveManuscripts()
                RenderManuscripts()

            End If

        End Using

    End Sub


    Private Sub OpenManuscript(
        manuscript As Manuscript,
        Optional routeWaypoint As ManuscriptRouteWaypoint = Nothing
    )

        Using dialog As New EditManuscriptForm(
            manuscript,
            manuscripts
        )

            If routeWaypoint IsNot Nothing Then

                dialog.NavigateToRouteWaypoint(
                    routeWaypoint
                )

            End If

            Dim result As DialogResult =
                dialog.ShowDialog(Me)

            If Not LoadAuthorLibrary() Then
                Return
            End If

            If dialog.DeleteRequested Then

                manuscripts.Remove(
                    manuscript
                )

                SaveManuscripts()
                RenderManuscripts()

                Return

            End If

            If result =
                DialogResult.OK Then

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


    Private Sub OpenAuthorLibrary(
        sender As Object,
        e As EventArgs
    )

        Using dialog As New AuthorLibraryForm(
            manuscripts
        )

            dialog.ShowDialog(
                Me
            )

        End Using

        If LoadAuthorLibrary() Then

            RenderManuscripts()

        End If

    End Sub


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
