Imports System
Imports System.Drawing
Imports System.Linq
Imports System.Windows.Forms
Imports ManuscriptPipeline.Controls
Imports ManuscriptPipeline.Forms
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

' A manuscript opens as a page in the main window. Its tabs edit one working
' copy; the Unsaved changes bar saves or discards it. Leaving the page,
' going back, opening another manuscript, or closing PaperRoute with unsaved
' changes asks first.
Partial Public Class Form1

    Private currentManuscriptId As Guid = Guid.Empty
    Private manuscriptOrigin As WorkspacePage = WorkspacePage.Board
    Private pendingManuscriptWaypoint As ManuscriptRouteWaypoint = Nothing

    Private manuscriptEditor As EditManuscriptForm = Nothing
    Private manuscriptSaveBar As Control = Nothing
    Private manuscriptTitleLabel As Label = Nothing
    Private manuscriptStagePill As PillLabel = Nothing
    Private manuscriptMetaLabel As Label = Nothing
    Private manuscriptShelfLabel As Label = Nothing

    Private WithEvents manuscriptDirtyTimer As New Timer With {.Interval = 300}

    ' Asks what to do with unsaved manuscript changes. Tests replace it.
    Friend unsavedChangesPrompt As Func(Of String, DialogResult) = Nothing


    ' Creates the page's editor. Tests supply a disposable author library.
    Protected Overridable Function CreateManuscriptEditor(
        manuscript As Manuscript
    ) As EditManuscriptForm

        Return New EditManuscriptForm(
            manuscript,
            manuscripts,
            authorRepository,
            pageMode:=True
        )

    End Function


    Private Sub OpenManuscript(
        manuscript As Manuscript,
        Optional routeWaypoint As ManuscriptRouteWaypoint = Nothing
    )

        If manuscript Is Nothing Then
            Return
        End If

        ' Already open: only move to the waypoint, keeping unsaved changes.
        If currentPage = WorkspacePage.Manuscript AndAlso
           currentManuscriptId = manuscript.Id Then

            If routeWaypoint IsNot Nothing AndAlso
               manuscriptEditor IsNot Nothing AndAlso
               Not manuscriptEditor.IsDisposed Then

                manuscriptEditor.ShowRouteWaypoint(routeWaypoint)

            End If

            Return

        End If

        pendingManuscriptWaypoint = routeWaypoint

        Try
            NavigateToRequest(
                New PageRequest(WorkspacePage.Manuscript, manuscript.Id),
                recordHistory:=True
            )
        Finally
            pendingManuscriptWaypoint = Nothing
        End Try

    End Sub


    Private Function PageTitle(page As WorkspacePage) As String

        Select Case page
            Case WorkspacePage.Library : Return "Library"
            Case WorkspacePage.Journals : Return "Journals"
            Case WorkspacePage.Deadlines : Return "Deadlines"
            Case WorkspacePage.Insights : Return "Insights"
            Case WorkspacePage.ImportExport : Return "Import & Export"
            Case Else : Return "Board"
        End Select

    End Function


    ' =====================================================
    ' Page
    ' =====================================================

    Private Function BuildManuscriptPage(
        manuscript As Manuscript
    ) As Control

        If manuscript Is Nothing Then
            Return New Label With {
                .Text = "This manuscript is no longer in the library.",
                .Padding = New Padding(UiTheme.Px(22, DeviceDpi)),
                .ForeColor = UiTheme.SecondaryText()
            }
        End If

        Dim dpi As Integer = DeviceDpi

        Dim page As New TableLayoutPanel With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 1,
            .RowCount = 2,
            .Margin = New Padding(0),
            .Padding = New Padding(0),
            .BackColor = UiTheme.BoardBackground()
        }

        page.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        page.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        page.RowStyles.Add(New RowStyle(SizeType.AutoSize))

        Dim frame As New TableLayoutPanel With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 1,
            .RowCount = 3,
            .Margin = New Padding(0),
            .Padding = New Padding(UiTheme.Px(22, dpi), UiTheme.Px(12, dpi), UiTheme.Px(22, dpi), 0),
            .BackColor = UiTheme.BoardBackground()
        }

        frame.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        frame.RowStyles.Add(New RowStyle(SizeType.AutoSize))
        frame.RowStyles.Add(New RowStyle(SizeType.AutoSize))
        frame.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))

        ' Breadcrumb back to where the manuscript was opened.
        Dim crumb As New FlowLayoutPanel With {
            .AutoSize = True,
            .AutoSizeMode = AutoSizeMode.GrowAndShrink,
            .WrapContents = False,
            .Margin = New Padding(0, 0, 0, UiTheme.Px(6, dpi)),
            .BackColor = UiTheme.BoardBackground()
        }

        Dim lnkBack As New LinkLabel With {
            .Text = ChrW(&H2190) & " " & PageTitle(manuscriptOrigin),
            .AutoSize = True,
            .UseMnemonic = False,
            .Margin = New Padding(0),
            .Font = New Font(Me.Font, FontStyle.Bold),
            .LinkBehavior = LinkBehavior.HoverUnderline,
            .LinkColor = UiTheme.AccentColor(),
            .ActiveLinkColor = UiTheme.AccentSecondaryColor(),
            .VisitedLinkColor = UiTheme.AccentColor(),
            .AccessibleName = "Back to " & PageTitle(manuscriptOrigin) & " (Alt+Left)"
        }

        AddHandler lnkBack.LinkClicked,
            Sub(sender, e)
                GoBack()
            End Sub

        manuscriptShelfLabel = New Label With {
            .AutoSize = True,
            .UseMnemonic = False,
            .Margin = New Padding(UiTheme.Px(4, dpi), 0, 0, 0),
            .ForeColor = UiTheme.MutedText()
        }

        crumb.Controls.Add(lnkBack)
        crumb.Controls.Add(manuscriptShelfLabel)

        ' Title, stage, and journal, with the page's actions.
        Dim header As New TableLayoutPanel With {
            .Dock = DockStyle.Top,
            .AutoSize = True,
            .AutoSizeMode = AutoSizeMode.GrowAndShrink,
            .ColumnCount = 2,
            .RowCount = 1,
            .Margin = New Padding(0, 0, 0, UiTheme.Px(4, dpi)),
            .BackColor = UiTheme.BoardBackground()
        }

        header.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        header.ColumnStyles.Add(New ColumnStyle(SizeType.AutoSize))
        header.RowStyles.Add(New RowStyle(SizeType.AutoSize))

        Dim identity As New FlowLayoutPanel With {
            .AutoSize = True,
            .AutoSizeMode = AutoSizeMode.GrowAndShrink,
            .FlowDirection = FlowDirection.TopDown,
            .WrapContents = False,
            .Margin = New Padding(0),
            .BackColor = UiTheme.BoardBackground()
        }

        manuscriptTitleLabel = New Label With {
            .AutoSize = True,
            .UseMnemonic = False,
            .Margin = New Padding(0, 0, 0, UiTheme.Px(6, dpi)),
            .Font = New Font(Me.Font.FontFamily, Me.Font.SizeInPoints * 1.45F, FontStyle.Bold),
            .ForeColor = UiTheme.PrimaryText()
        }

        Dim meta As New FlowLayoutPanel With {
            .AutoSize = True,
            .AutoSizeMode = AutoSizeMode.GrowAndShrink,
            .WrapContents = False,
            .Margin = New Padding(0),
            .BackColor = UiTheme.BoardBackground()
        }

        manuscriptStagePill = New PillLabel With {
            .UseMnemonic = False,
            .Font = cardBadgeFont,
            .Margin = New Padding(0, 0, UiTheme.Px(10, dpi), 0)
        }

        manuscriptMetaLabel = New Label With {
            .AutoSize = True,
            .UseMnemonic = False,
            .Margin = New Padding(0, UiTheme.Px(2, dpi), 0, 0),
            .ForeColor = UiTheme.SecondaryText()
        }

        meta.Controls.Add(manuscriptStagePill)
        meta.Controls.Add(manuscriptMetaLabel)
        identity.Controls.Add(manuscriptTitleLabel)
        identity.Controls.Add(meta)

        Dim actions As New FlowLayoutPanel With {
            .AutoSize = True,
            .AutoSizeMode = AutoSizeMode.GrowAndShrink,
            .WrapContents = False,
            .Anchor = AnchorStyles.Top Or AnchorStyles.Right,
            .Margin = New Padding(UiTheme.Px(12, dpi), 0, 0, 0),
            .BackColor = UiTheme.BoardBackground()
        }

        Dim btnRoute As New ActionButton With {
            .Text = "View Route",
            .Width = GetResponsiveButtonWidth("View Route", 96),
            .Height = GetResponsiveButtonHeight(34),
            .Margin = New Padding(0)
        }

        AddHandler btnRoute.Click,
            Sub(sender, e)
                Dim saved As Manuscript = FindManuscript(currentManuscriptId)
                If saved IsNot Nothing Then OpenRouteView(saved)
            End Sub

        Dim pageMenu As New ContextMenuStrip()
        pageMenu.Items.Add("Route Report...", Nothing, Sub(sender, e) SaveRouteReport(FindManuscript(currentManuscriptId)))
        pageMenu.Items.Add("Check for Publication...", Nothing, Sub(sender, e) CheckOpenManuscriptForPublication())
        pageMenu.Items.Add(New ToolStripSeparator())
        Dim deleteItem As ToolStripItem =
            pageMenu.Items.Add(
                "Delete Manuscript...",
                Nothing,
                Sub(sender, e)
                    If manuscriptEditor IsNot Nothing AndAlso Not manuscriptEditor.IsDisposed Then
                        manuscriptEditor.ConfirmDeleteFromPage()
                    End If
                End Sub)
        deleteItem.ForeColor = UiTheme.DangerColor()

        Dim btnMore As New ActionButton With {
            .Text = ChrW(&H22EF),
            .Role = ActionButtonRole.Quiet,
            .UseMnemonic = False,
            .Padding = New Padding(0),
            .Width = GetResponsiveButtonHeight(34) + UiTheme.Px(6, dpi),
            .Height = GetResponsiveButtonHeight(34),
            .Margin = New Padding(UiTheme.Px(6, dpi), 0, 0, 0),
            .AccessibleName = "More actions for this manuscript"
        }

        AddHandler btnMore.Click,
            Sub(sender, e)
                pageMenu.Show(btnMore, New Point(0, btnMore.Height))
            End Sub

        AddHandler page.Disposed,
            Sub(sender, e)
                pageMenu.Dispose()
            End Sub

        Dim btnCitation As New ActionButton With {
            .Text = "Copy Citation",
            .Width = GetResponsiveButtonWidth("Copy Citation", 110),
            .Height = GetResponsiveButtonHeight(34),
            .Margin = New Padding(0, 0, UiTheme.Px(8, dpi), 0),
            .AccessibleDescription = "Copies the saved manuscript's citation, formatted as in Publication & CV Export."
        }

        AddHandler btnCitation.Click,
            Sub(sender, e)
                CopyManuscriptCitation()
            End Sub

        actions.Controls.Add(btnCitation)
        actions.Controls.Add(btnRoute)
        actions.Controls.Add(btnMore)

        header.Controls.Add(identity, 0, 0)
        header.Controls.Add(actions, 1, 0)

        ' Long titles wrap beside the actions. The frame sets the wrap width
        ' before it lays out the header, so the header is measured once.
        Dim titleLabel As Label = manuscriptTitleLabel
        Dim fitTitle As Action =
            Sub()
                Dim width As Integer = Math.Max(UiTheme.Px(200, dpi), frame.ClientSize.Width - frame.Padding.Horizontal - actions.PreferredSize.Width - UiTheme.Px(24, dpi))
                If titleLabel.MaximumSize.Width <> width Then
                    titleLabel.MaximumSize = New Size(width, 0)
                End If
            End Sub

        AddHandler frame.Resize,
            Sub(sender, e)
                fitTitle()
            End Sub

        ' The editor: tabs over one working copy, without its dialog footer.
        Dim editor As EditManuscriptForm = CreateManuscriptEditor(manuscript)
        EmbedForm(editor)
        AddHandler editor.DeleteConfirmed, AddressOf ManuscriptDeleteConfirmed
        manuscriptEditor = editor

        frame.Controls.Add(crumb, 0, 0)
        frame.Controls.Add(header, 0, 1)
        frame.Controls.Add(editor, 0, 2)

        manuscriptSaveBar = BuildSaveBar()
        manuscriptSaveBar.Visible = False

        page.Controls.Add(frame, 0, 0)
        page.Controls.Add(manuscriptSaveBar, 0, 1)

        RefreshManuscriptHeader()
        fitTitle()

        Dim waypoint As ManuscriptRouteWaypoint = pendingManuscriptWaypoint

        If waypoint IsNot Nothing AndAlso IsHandleCreated Then
            BeginInvoke(
                New Action(
                    Sub()
                        If manuscriptEditor Is editor AndAlso Not editor.IsDisposed Then
                            editor.ShowRouteWaypoint(waypoint)
                        End If
                    End Sub))
        End If

        Return page

    End Function


    ' The Unsaved changes bar, shown only while there is something to save.
    Private Function BuildSaveBar() As Control

        Dim dpi As Integer = DeviceDpi

        Dim bar As New TableLayoutPanel With {
            .Dock = DockStyle.Fill,
            .AutoSize = True,
            .AutoSizeMode = AutoSizeMode.GrowAndShrink,
            .ColumnCount = 4,
            .RowCount = 1,
            .Margin = New Padding(0),
            .Padding = New Padding(UiTheme.Px(22, dpi), UiTheme.Px(10, dpi), UiTheme.Px(22, dpi), UiTheme.Px(10, dpi)),
            .BackColor = UiTheme.HeaderBackground()
        }

        bar.ColumnStyles.Add(New ColumnStyle(SizeType.AutoSize))
        bar.ColumnStyles.Add(New ColumnStyle(SizeType.AutoSize))
        bar.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        bar.ColumnStyles.Add(New ColumnStyle(SizeType.AutoSize))
        bar.RowStyles.Add(New RowStyle(SizeType.AutoSize))

        AddHandler bar.Paint,
            Sub(sender, e)
                Using line As New Pen(UiTheme.CardBorder())
                    e.Graphics.DrawLine(line, 0, 0, bar.Width, 0)
                End Using
            End Sub

        Dim lblDot As New Label With {
            .Text = ChrW(&H25CF),
            .AutoSize = True,
            .Anchor = AnchorStyles.Left,
            .Margin = New Padding(0, 0, UiTheme.Px(6, dpi), 0),
            .ForeColor = UiTheme.WarningColor(),
            .AccessibleRole = AccessibleRole.None
        }

        Dim lblMessage As New Label With {
            .Text = "Unsaved changes",
            .AutoSize = True,
            .Anchor = AnchorStyles.Left,
            .Margin = New Padding(0, 0, UiTheme.Px(12, dpi), 0),
            .Font = New Font(Me.Font, FontStyle.Bold),
            .ForeColor = UiTheme.PrimaryText()
        }

        Dim lblHint As New Label With {
            .Text = "Save keeps them. Discard returns to the last saved version.",
            .AutoSize = True,
            .AutoEllipsis = True,
            .Anchor = AnchorStyles.Left,
            .Margin = New Padding(0),
            .ForeColor = UiTheme.MutedText()
        }

        Dim buttons As New FlowLayoutPanel With {
            .AutoSize = True,
            .AutoSizeMode = AutoSizeMode.GrowAndShrink,
            .WrapContents = False,
            .Anchor = AnchorStyles.Right,
            .Margin = New Padding(UiTheme.Px(12, dpi), 0, 0, 0),
            .BackColor = UiTheme.HeaderBackground()
        }

        Dim btnDiscard As New ActionButton With {
            .Text = "Discard",
            .Width = GetResponsiveButtonWidth("Discard", 88),
            .Height = GetResponsiveButtonHeight(34),
            .Margin = New Padding(0, 0, UiTheme.Px(8, dpi), 0),
            .AccessibleName = "Discard unsaved changes"
        }

        Dim btnSave As New ActionButton With {
            .Text = "Save",
            .Role = ActionButtonRole.Primary,
            .Width = GetResponsiveButtonWidth("Save", 88),
            .Height = GetResponsiveButtonHeight(34),
            .Margin = New Padding(0),
            .AccessibleName = "Save manuscript (Ctrl+S)"
        }

        AddHandler btnDiscard.Click,
            Sub(sender, e)
                DiscardManuscriptChanges()
            End Sub

        AddHandler btnSave.Click,
            Sub(sender, e)
                SaveManuscriptPage()
            End Sub

        buttons.Controls.Add(btnDiscard)
        buttons.Controls.Add(btnSave)

        bar.Controls.Add(lblDot, 0, 0)
        bar.Controls.Add(lblMessage, 1, 0)
        bar.Controls.Add(lblHint, 2, 0)
        bar.Controls.Add(buttons, 3, 0)

        Return bar

    End Function


    ' The saved manuscript's citation, formatted as Publication & CV Export
    ' formats it.
    Private Sub CopyManuscriptCitation()

        Dim manuscript As Manuscript = FindManuscript(currentManuscriptId)

        If manuscript Is Nothing Then
            Return
        End If

        Try
            Clipboard.SetText(PublicationExportService.FormatCitation(manuscript, If(authorLibrary, New AuthorLibraryData())))
            lblStatus.Text = "Citation copied."
        Catch ex As Exception When TypeOf ex Is System.Runtime.InteropServices.ExternalException OrElse TypeOf ex Is InvalidOperationException
            lblStatus.Text = "The clipboard is busy; try Copy Citation again."
        End Try

    End Sub


    Private Sub RefreshManuscriptHeader()

        Dim manuscript As Manuscript = FindManuscript(currentManuscriptId)

        If manuscript Is Nothing OrElse manuscriptTitleLabel Is Nothing OrElse manuscriptTitleLabel.IsDisposed Then
            Return
        End If

        manuscriptTitleLabel.Text = manuscript.Title
        manuscriptShelfLabel.Text = "/  " & FormatShelf(manuscript.Location)

        Dim stageText As String = FormatStage(manuscript.CurrentStage).ToUpperInvariant()
        Dim stageSize As Size = TextRenderer.MeasureText(stageText, manuscriptStagePill.Font)
        manuscriptStagePill.Text = stageText
        manuscriptStagePill.Size = New Size(stageSize.Width + UiTheme.Px(16, DeviceDpi), stageSize.Height + UiTheme.Px(6, DeviceDpi))
        manuscriptStagePill.BackColor = UiTheme.StageBackground(manuscript.CurrentStage)
        manuscriptStagePill.ForeColor = UiTheme.StageForeground(manuscript.CurrentStage)

        manuscriptMetaLabel.Text =
            String.Join(
                "  " & ChrW(&HB7) & "  ",
                {If(String.IsNullOrWhiteSpace(manuscript.TargetJournal), "No target journal", manuscript.TargetJournal),
                 RouteSummaryService.Describe(manuscript).Text})

    End Sub


    Private Sub ManuscriptDirtyTimer_Tick(sender As Object, e As EventArgs) Handles manuscriptDirtyTimer.Tick

        If manuscriptEditor Is Nothing OrElse
           manuscriptEditor.IsDisposed OrElse
           manuscriptSaveBar Is Nothing OrElse
           manuscriptSaveBar.IsDisposed Then
            Return
        End If

        Dim dirty As Boolean = manuscriptEditor.HasUnsavedChanges()

        If manuscriptSaveBar.Visible <> dirty Then
            manuscriptSaveBar.Visible = dirty
        End If

    End Sub


    ' =====================================================
    ' Save, discard, delete, and leaving
    ' =====================================================

    ' Commits the page's working copy and saves the library. Returns False
    ' when a field needs attention, a stage change was not completed, or the
    ' library could not be saved.
    Friend Function SaveManuscriptPage() As Boolean

        If manuscriptEditor Is Nothing OrElse manuscriptEditor.IsDisposed Then
            Return True
        End If

        If Not manuscriptEditor.CommitChanges() Then
            Return False
        End If

        Dim saved As Boolean = SaveManuscripts()

        ' The editor may have saved new reusable authors.
        LoadAuthorLibrary()
        RenderManuscripts()
        RefreshManuscriptHeader()

        If manuscriptSaveBar IsNot Nothing AndAlso Not manuscriptSaveBar.IsDisposed Then
            manuscriptSaveBar.Visible = manuscriptEditor.HasUnsavedChanges()
        End If

        Return saved

    End Function


    ' Rebuilds the page from the saved manuscript.
    Private Sub DiscardManuscriptChanges()

        If currentPage <> WorkspacePage.Manuscript Then
            Return
        End If

        Dim request As PageRequest = CurrentRequest

        LeaveCurrentPage()
        ShowPage(request)

        lblStatus.Text = "Unsaved changes discarded."

    End Sub


    ' A check can mark the saved manuscript published, so the page must
    ' match the library first; afterwards it shows the saved version.
    Private Sub CheckOpenManuscriptForPublication()

        If manuscriptEditor Is Nothing OrElse manuscriptEditor.IsDisposed Then Return

        If manuscriptEditor.HasUnsavedChanges() Then
            MessageBox.Show(Me, "Save or discard your changes to this manuscript first, then check for a publication.",
                            "Check for Publication", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If

        Dim request As PageRequest = CurrentRequest
        CheckForPublications({currentManuscriptId})

        If currentPage = WorkspacePage.Manuscript AndAlso FindManuscript(currentManuscriptId) IsNot Nothing Then
            LeaveCurrentPage()
            ShowPage(request)
        End If

    End Sub


    Private Sub ManuscriptDeleteConfirmed(sender As Object, e As EventArgs)

        Dim manuscript As Manuscript = FindManuscript(currentManuscriptId)

        If manuscript Is Nothing Then
            Return
        End If

        manuscripts.Remove(manuscript)
        SaveManuscripts()

        ' The manuscript is gone, so there is nothing left to ask about.
        manuscriptEditor = Nothing

        Dim origin As WorkspacePage = manuscriptOrigin

        ' Leave after the editor's click handler has returned.
        BeginInvoke(
            New Action(
                Sub()
                    NavigateToRequest(New PageRequest(origin), recordHistory:=False)
                    RenderManuscripts()
                End Sub))

    End Sub


    ' Asks about unsaved manuscript changes before leaving the page. Returns
    ' False to stay.
    Friend Function ConfirmLeaveManuscript() As Boolean

        If currentPage <> WorkspacePage.Manuscript OrElse
           manuscriptEditor Is Nothing OrElse
           manuscriptEditor.IsDisposed OrElse
           Not manuscriptEditor.HasUnsavedChanges() Then
            Return True
        End If

        Dim title As String = If(FindManuscript(currentManuscriptId)?.Title, "this manuscript")

        Dim choice As DialogResult =
            If(unsavedChangesPrompt IsNot Nothing,
               unsavedChangesPrompt(title),
               MessageBox.Show(
                   Me,
                   "Save your changes to '" & title & "'?" &
                   Environment.NewLine &
                   Environment.NewLine &
                   "Choose No to discard them.",
                   "Unsaved Changes",
                   MessageBoxButtons.YesNoCancel,
                   MessageBoxIcon.Warning))

        Select Case choice
            Case DialogResult.Yes
                Return SaveManuscriptPage()
            Case DialogResult.No
                Return True
            Case Else
                Return False
        End Select

    End Function


    Protected Overrides Sub OnFormClosing(e As FormClosingEventArgs)

        If Not e.Cancel AndAlso Not ConfirmLeaveManuscript() Then
            e.Cancel = True
        End If

        MyBase.OnFormClosing(e)

    End Sub

End Class
