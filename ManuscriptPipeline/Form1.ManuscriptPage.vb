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

    ' The open page's editor and Unsaved changes bar; Nothing while no
    ' manuscript page is shown.
    Private manuscriptEditor As EditManuscriptForm = Nothing
    Private manuscriptSaveBar As Control = Nothing
    Private manuscriptSaveHint As Label = Nothing
    Private manuscriptTitleLabel As Label = Nothing
    Private manuscriptStagePill As PillLabel = Nothing
    Private manuscriptMetaLabel As Label = Nothing
    Private manuscriptShelfLabel As Label = Nothing

    ' The page is built once and kept for every manuscript opened (#133):
    ' its editor loads one record after another (LoadRecord) and lets go of
    ' each once the page is left (UnloadRecord), hidden in the page area
    ' until the next. It is prepared while the window idles after startup,
    ' or built by a manuscript opened before then, and built again if the
    ' colors change, as under a Windows contrast theme switched while
    ' PaperRoute runs, or the window's DPI or font.
    Private manuscriptPage As Control = Nothing
    Private manuscriptPageEditor As EditManuscriptForm = Nothing
    Private manuscriptPageSaveBar As Control = Nothing
    Private manuscriptPageSaveHint As Label = Nothing
    Private manuscriptBackLink As LinkLabel = Nothing
    Private fitManuscriptTitle As Action = Nothing
    Private manuscriptPagePalette As String = String.Empty

    ' The kept page's containers, outer ones first, listed once as it is
    ' built: an open holds the layout of each while the record loads, and
    ' each then lays out once (ShowPageInOnePass).
    Private manuscriptPageContainers As List(Of Control) = Nothing

    Private Const SaveBarHint As String =
        "Save keeps them. Discard returns to the last saved version."

    ' After a failed save with nothing edited since, there is no saved
    ' version for Discard to return to, so the bar says what Save does
    ' instead (#113).
    Private Const SaveFailedHint As String =
        "Your last save did not finish. Save tries again."

    Private WithEvents manuscriptDirtyTimer As New Timer With {.Interval = 300}

    ' Asks what to do with unsaved manuscript changes. Tests replace it.
    Friend unsavedChangesPrompt As Func(Of String, DialogResult) = Nothing

    ' Shows an unexpected error, given whether there are unsaved changes;
    ' Abort means Close PaperRoute (#121). Tests replace it.
    Friend unhandledErrorPrompt As Func(Of Exception, Boolean, DialogResult) = Nothing


    ' Creates the page's editor. Tests supply a disposable author library.
    ' The page asks for it with no manuscript: it loads each one itself,
    ' a recovered draft (#123) included, through LoadRecord.
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

    ' A page for a manuscript the library no longer holds.
    Private Function BuildMissingManuscriptPage() As Control

        Return New Label With {
            .Text = "This manuscript is no longer in the library.",
            .Padding = New Padding(UiTheme.Px(22, DeviceDpi)),
            .ForeColor = UiTheme.SecondaryText()
        }

    End Function


    ' Shows a manuscript on the kept page: its editor loads the record
    ' while the page is hidden, the page shows in one layout pass, and the
    ' Versions tab, the selected submission's details and any waypoint
    ' asked for follow once it has painted (#133). Every container of the
    ' page holds its layout from before the record loads until the page
    ' shows: loading a record changes the text, the parts shown and the
    ' rows of a hidden page, and each change laid out its containers
    ' again, up to the page, though nothing of it was shown.
    Private Sub ShowManuscriptPage(
        manuscript As Manuscript
    )

        Dim page As Control = KeptManuscriptPage()
        Dim editor As EditManuscriptForm = manuscriptPageEditor
        Dim containers As List(Of Control) = manuscriptPageContainers

        For Each container As Control In containers
            container.SuspendLayout()
        Next

        Try

            manuscriptBackLink.Text = ChrW(&H2190) & " " & PageTitle(manuscriptOrigin)
            manuscriptBackLink.AccessibleName = "Back to " & PageTitle(manuscriptOrigin) & " (Alt+Left)"
            manuscriptPageSaveHint.Text = SaveBarHint
            manuscriptPageSaveBar.Visible = False

            editor.LoadRecord(manuscript, manuscripts, pendingRecoveredCopy, deferParts:=True)

        Catch
            ' A load that failed part way leaves the page hidden and laid
            ' out again as each container lets go of its layout.
            For index As Integer = containers.Count - 1 To 0 Step -1
                containers(index).ResumeLayout(True)
            Next
            Throw
        End Try

        ' The page's editor only once its record has loaded: a load that
        ' failed part way leaves no editor to ask about, save or draft,
        ' as a page whose editor failed to build left none (#121).
        manuscriptEditor = editor
        manuscriptSaveBar = manuscriptPageSaveBar
        manuscriptSaveHint = manuscriptPageSaveHint

        RefreshManuscriptHeader()
        fitManuscriptTitle()

        ShowPageInOnePass(page, containers)
        currentPageView = page
        manuscriptDirtyTimer.Start()

        Dim token As Object = editor.RecordToken
        Dim waypoint As ManuscriptRouteWaypoint = pendingManuscriptWaypoint

        ' The Versions tab and the selected submission's details follow the
        ' page's first paint (FinishManuscriptPage).
        If IsHandleCreated Then
            BeginInvoke(New Action(Sub() FinishManuscriptPage(editor, token, waypoint)))
        Else
            editor.EnsureDeferredParts()
        End If

    End Sub


    ' After the page's first paint, for the record it was posted for only:
    ' while library work runs (#132) it waits for the window to idle. The
    ' paints waiting are dispatched first, as the message loop would
    ' dispatch them next (PumpPaints): the page shows, then the parts are
    ' built. Nothing is forced to paint; forcing it (RedrawWindow with
    ' RDW_UPDATENOW) is where this machine waited on the screen.
    Private Sub FinishManuscriptPage(
        editor As EditManuscriptForm,
        token As Object,
        waypoint As ManuscriptRouteWaypoint
    )

        If manuscriptEditor IsNot editor OrElse editor.IsDisposed OrElse editor.RecordToken IsNot token Then
            Return
        End If

        If libraryWorkInProgress Then
            QueueIdleWork(Sub() FinishManuscriptPage(editor, token, waypoint))
            Return
        End If

        PumpPaints()

        ' A paint handler may have moved on: the record is checked again.
        If manuscriptEditor IsNot editor OrElse editor.IsDisposed OrElse editor.RecordToken IsNot token Then
            Return
        End If

        editor.EnsureDeferredParts()

        If waypoint IsNot Nothing Then
            editor.ShowRouteWaypoint(waypoint)
        End If

    End Sub


    ' Dispatches the paints waiting for the thread's windows, as the
    ' message loop would next, and nothing else: no input, no posted
    ' message, no timer. It works from any message loop, the window's or a
    ' dialog's, and from a test's Application.DoEvents. A paint that
    ' invalidates again cannot hold it (the cap). The budget test times
    ' the paint after a switch with it (LibraryBoard.PaintNow).
    Friend Shared Sub PumpPaints()

        Dim message As NativeMessage
        Dim remaining As Integer = 1000

        While remaining > 0 AndAlso PeekMessage(message, IntPtr.Zero, WM_PAINT, WM_PAINT, PM_REMOVE)
            TranslateMessage(message)
            DispatchMessage(message)
            remaining -= 1
        End While

    End Sub


    Private Const WM_PAINT As UInteger = &HF
    Private Const PM_REMOVE As UInteger = &H1

    <Runtime.InteropServices.StructLayout(Runtime.InteropServices.LayoutKind.Sequential)>
    Private Structure NativeMessage
        Public Handle As IntPtr
        Public Message As UInteger
        Public WParam As IntPtr
        Public LParam As IntPtr
        Public Time As UInteger
        Public X As Integer
        Public Y As Integer
    End Structure

    <Runtime.InteropServices.DllImport("user32.dll")>
    Private Shared Function PeekMessage(ByRef message As NativeMessage, handle As IntPtr, filterMin As UInteger, filterMax As UInteger, remove As UInteger) As Boolean
    End Function

    <Runtime.InteropServices.DllImport("user32.dll")>
    Private Shared Function TranslateMessage(ByRef message As NativeMessage) As Boolean
    End Function

    <Runtime.InteropServices.DllImport("user32.dll")>
    Private Shared Function DispatchMessage(ByRef message As NativeMessage) As IntPtr
    End Function


    ' Shows a page loaded while hidden in one layout pass: its containers,
    ' outer ones first, hold their layout while the page comes forward,
    ' and each then lays out once, inner ones first. Shown plainly, every
    ' container lays itself out again each time one inside it does, which
    ' on the manuscript page took longer than loading it. The Board is
    ' hidden by the switch (SwitchPage), once drawing is released.
    Private Sub ShowPageInOnePass(
        page As Control,
        containers As List(Of Control)
    )

        page.Visible = True
        page.BringToFront()

        ' Each container lets go of its layout, inner ones first, and lays
        ' out once if anything in it changed since it was held: the record
        ' loaded, the header, a part shown or hidden for the record. One in
        ' which nothing changed is not laid out again; laying out every
        ' container whatever changed took twice as long on this machine.
        For index As Integer = containers.Count - 1 To 0 Step -1
            containers(index).ResumeLayout(True)
        Next

    End Sub


    ' The kept page, built and put in the page area, hidden, if it is not
    ' there yet or the colors it was built in have changed.
    Private Function KeptManuscriptPage() As Control

        If manuscriptPage IsNot Nothing AndAlso
           (manuscriptPage.IsDisposed OrElse manuscriptPagePalette <> ManuscriptPageStamp()) Then
            DropManuscriptPage()
        End If

        If manuscriptPage Is Nothing Then
            BuildManuscriptPage()
        End If

        ' What the preparation had not laid out yet lays out now.
        manuscriptPageEditor.LetGoOfLayouts(Integer.MaxValue)
        PutManuscriptPageInPlace()

        Return manuscriptPage

    End Function


    ' The page goes in the page area hidden, behind the Board.
    Private Sub PutManuscriptPageInPlace()

        If manuscriptPage.Parent Is Nothing Then
            manuscriptPage.Visible = False
            manuscriptPage.Dock = DockStyle.Fill
            contentHost.Controls.Add(manuscriptPage)
            manuscriptPage.SendToBack()
        End If

    End Sub


    ' What the kept page took when it was built: the colors, and the
    ' window's DPI and font, which set its sizes.
    Private Function ManuscriptPageStamp() As String

        Return UiTheme.PaletteStamp() & "," & DeviceDpi.ToString(Globalization.CultureInfo.InvariantCulture) & "," & Font.ToString()

    End Function


    ' Lets the kept page go. Its successor is prepared while the window
    ' idles, or built by the next manuscript opened if that comes first.
    Private Sub DropManuscriptPage()

        If manuscriptPage IsNot Nothing AndAlso Not manuscriptPage.IsDisposed Then
            InPlace.Dispose(manuscriptPage)
        End If

        manuscriptPage = Nothing
        manuscriptPageEditor = Nothing
        manuscriptPageSaveBar = Nothing
        manuscriptPageSaveHint = Nothing
        manuscriptBackLink = Nothing
        fitManuscriptTitle = Nothing
        manuscriptPageContainers = Nothing

        QueueManuscriptPagePreparation()

    End Sub


    ' The page is prepared while the window idles (#133), a step at a
    ' time: built with no manuscript and the library's tags offered, its
    ' parts laid out a slice at a time, put in the page area hidden and
    ' laid out at the size it shows at, and its windows made a slice at a
    ' time. The first manuscript opened then finds it as every one after
    ' it does. Startup queues it beside the Board's fill; a page let go
    ' for new colors queues its successor. An open before it is ready
    ' finishes it there and then (KeptManuscriptPage), and a page shown
    ' makes the rest of its windows as it shows.
    Private Sub QueueManuscriptPagePreparation()

        QueueIdleWork(AddressOf PrepareManuscriptPage)

    End Sub


    Private Sub PrepareManuscriptPage()

        If IsDisposed OrElse manuscriptPage IsNot Nothing Then
            Return
        End If

        BuildManuscriptPage()
        manuscriptPage.Visible = False
        manuscriptPageEditor.SuggestTags(manuscripts)

        Dim page As Control = manuscriptPage
        QueueIdleWork(Sub() LayOutPreparedManuscriptPage(page))

    End Sub


    Private Function StillPreparing(page As Control) As Boolean

        Return page Is manuscriptPage AndAlso Not page.IsDisposed AndAlso Not page.Visible AndAlso Not IsDisposed

    End Function


    Private Sub LayOutPreparedManuscriptPage(page As Control)

        If Not StillPreparing(page) Then
            Return
        End If

        If Not manuscriptPageEditor.LetGoOfLayouts(CInt(IdleWorkSlice)) Then
            QueueIdleWork(Sub() LayOutPreparedManuscriptPage(page))
            Return
        End If

        ' A hidden page takes no part in its parent's layout, so it is
        ' given the size it shows at.
        PutManuscriptPageInPlace()
        page.Bounds = contentHost.DisplayRectangle
        page.PerformLayout()
        QueueIdleWork(Sub() MakeManuscriptPageWindows(page))

    End Sub


    ' The hidden page's windows, about a slice's worth a step.
    Private Sub MakeManuscriptPageWindows(page As Control)

        If Not StillPreparing(page) Then
            Return
        End If

        If Not InPlace.MakeWindows(page, CInt(IdleWorkSlice)) Then
            QueueIdleWork(Sub() MakeManuscriptPageWindows(page))
        End If

    End Sub


    ' After the page is left (#133): its editor lets go of the record and
    ' its header clears while the window idles, unless a manuscript has
    ' been opened on the page since. Leaving only hides the page, so
    ' going back to the Board costs no more than hiding it; letting go of
    ' a record destroys the windows made for it, which on a slow machine
    ' takes a few hundred milliseconds.
    Private Sub QueueManuscriptPageUnload()

        Dim editor As EditManuscriptForm = manuscriptPageEditor

        If editor Is Nothing Then
            Return
        End If

        Dim token As Object = editor.RecordToken

        QueueIdleWork(
            Sub()
                If editor Is manuscriptPageEditor AndAlso Not editor.IsDisposed AndAlso
                   editor.RecordToken Is token AndAlso currentPageView IsNot manuscriptPage Then
                    editor.UnloadRecord()
                    ClearManuscriptHeader()
                End If
            End Sub)

    End Sub

    ' Builds the kept page around an editor with no manuscript loaded.
    Private Sub BuildManuscriptPage()

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

        ' Its text names the page the manuscript was opened from (ShowManuscriptPage).
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
        ' It is made once, with no manuscript, and loads each one opened.
        Dim editor As EditManuscriptForm = CreateManuscriptEditor(Nothing)
        EmbedForm(editor)
        InPlace.WhenAttached(editor, Sub() editor.Visible = True)
        AddHandler editor.DeleteConfirmed, AddressOf ManuscriptDeleteConfirmed
        AddHandler editor.StatusMessage, Sub(sender, message) lblStatus.Text = message

        frame.Controls.Add(crumb, 0, 0)
        frame.Controls.Add(header, 0, 1)
        frame.Controls.Add(editor, 0, 2)

        Dim saveBar As Control = BuildSaveBar()
        saveBar.Visible = False

        page.Controls.Add(frame, 0, 0)
        page.Controls.Add(saveBar, 0, 1)

        manuscriptPage = page
        manuscriptPageEditor = editor
        manuscriptPageSaveBar = saveBar
        manuscriptBackLink = lnkBack
        fitManuscriptTitle = fitTitle
        manuscriptPagePalette = ManuscriptPageStamp()
        manuscriptPageContainers =
            {CType(page, Control)}.Concat(DescendantControls(page)).Where(Function(control) control.HasChildren).ToList()

    End Sub


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

        manuscriptPageSaveHint = New Label With {
            .Text = SaveBarHint,
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
        bar.Controls.Add(manuscriptPageSaveHint, 2, 0)
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


    ' The kept page's header once it is left, as its editor is (#133): it
    ' names no manuscript while hidden.
    Private Sub ClearManuscriptHeader()

        If manuscriptTitleLabel Is Nothing OrElse manuscriptTitleLabel.IsDisposed Then
            Return
        End If

        manuscriptTitleLabel.Text = String.Empty
        manuscriptShelfLabel.Text = String.Empty
        manuscriptStagePill.Text = String.Empty
        manuscriptMetaLabel.Text = String.Empty

    End Sub


    Private Sub ManuscriptDirtyTimer_Tick(sender As Object, e As EventArgs) Handles manuscriptDirtyTimer.Tick

        If manuscriptEditor Is Nothing OrElse
           manuscriptEditor.IsDisposed OrElse
           manuscriptSaveBar Is Nothing OrElse
           manuscriptSaveBar.IsDisposed Then
            Return
        End If

        RefreshSaveBar()

    End Sub


    ' Shows the Unsaved changes bar while the editor differs from the model
    ' or the last save did not reach the disk (#113), with a hint for each.
    Private Sub RefreshSaveBar()

        Dim edited As Boolean = manuscriptEditor.HasUnsavedChanges()

        Dim hint As String =
            If(librarySaveFailed AndAlso Not edited, SaveFailedHint, SaveBarHint)

        If manuscriptSaveHint.Text <> hint Then
            manuscriptSaveHint.Text = hint
        End If

        Dim dirty As Boolean = edited OrElse librarySaveFailed

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

        ' While a save, backup or restore runs (#132), the library's save
        ' would be refused: the edits stay on the page, still unsaved, rather
        ' than go into records the work is reading and be saved nowhere.
        If libraryWorkInProgress Then
            Return False
        End If

        If Not manuscriptEditor.CommitChanges() Then
            Return False
        End If

        Dim saved As Boolean = SaveManuscripts()

        ' What a draft of this session held is now saved (#123).
        If saved Then
            DiscardRecoveryDraft()
        End If

        ' The editor may have saved new reusable authors.
        LoadAuthorLibrary()
        RenderManuscripts()
        RefreshManuscriptHeader()

        If manuscriptSaveBar IsNot Nothing AndAlso Not manuscriptSaveBar.IsDisposed Then
            RefreshSaveBar()
        End If

        Return saved

    End Function


    ' Rebuilds the page from the saved manuscript.
    Private Sub DiscardManuscriptChanges()

        If currentPage <> WorkspacePage.Manuscript Then
            Return
        End If

        SwitchPage(CurrentRequest)

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
            SwitchPage(request)
        End If

    End Sub


    Private Sub ManuscriptDeleteConfirmed(sender As Object, e As EventArgs)

        Dim manuscript As Manuscript = FindManuscript(currentManuscriptId)

        If manuscript Is Nothing Then
            Return
        End If

        manuscripts.Remove(manuscript)

        If SaveManuscripts() Then
            ReportSavedAction("Deleted """ & ReminderService.SafeManuscriptTitle(manuscript) & """.")
        End If

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

        Return ConfirmUnsavedChanges("Choose No to discard them.")

    End Function


    ' Asks about unsaved manuscript changes before an action that would
    ' otherwise miss them (#112): Yes saves first, No goes on without them
    ' as the hint says, Cancel stops. Returns False to stop.
    Friend Function ConfirmUnsavedChanges(
        noHint As String
    ) As Boolean

        If Not HasUnsavedPageEdits() Then
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
                   noHint,
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


    Private Function HasUnsavedPageEdits() As Boolean

        Return currentPage = WorkspacePage.Manuscript AndAlso
            manuscriptEditor IsNot Nothing AndAlso
            Not manuscriptEditor.IsDisposed AndAlso
            manuscriptEditor.HasUnsavedChanges()

    End Function


    ' An error on the UI thread (#121): one plain dialog that says what
    ' happened and whether there are unsaved changes. Keep Working returns
    ' to the window; Close PaperRoute closes it the normal way, so the Save
    ' or Discard question still appears, once any library work running
    ' has ended (#132), since a close before then is refused.
    Friend Sub ReportUnhandledError(
        failure As Exception
    )

        Dim unsavedChanges As Boolean = HasUnsavedPageEdits() OrElse librarySaveFailed

        Dim choice As DialogResult

        If unhandledErrorPrompt IsNot Nothing Then
            choice = unhandledErrorPrompt(failure, unsavedChanges)
        Else
            ' A guard no test can fail before it: this runs inside the error
            ' handler, where a second failure would end PaperRoute with
            ' nothing shown. A plain box offers the same two ways out, and
            ' nothing escapes from here.
            Try
                Using dialog As New UnexpectedErrorForm(failure, unsavedChanges, UnexpectedErrorForm.LogFolder())
                    choice = dialog.ShowDialog(Me)
                End Using
            Catch ex As Exception
                choice = FallbackErrorChoice(failure, unsavedChanges)
            End Try
        End If

        If choice = DialogResult.Abort Then
            If libraryWorkInProgress Then
                CloseOnceLibraryWorkHasEnded()
            Else
                Close()
            End If
        End If

    End Sub


    ' The error dialog could not be shown: a message box cannot name its
    ' buttons, so the text does. Yes keeps working (Cancel), No closes
    ' PaperRoute (Abort). Should even the box fail, keep working.
    Private Function FallbackErrorChoice(
        failure As Exception,
        unsavedChanges As Boolean
    ) As DialogResult

        Try
            Dim answer As DialogResult =
                MessageBox.Show(
                    Me,
                    "PaperRoute ran into a problem." &
                    Environment.NewLine &
                    Environment.NewLine &
                    If(String.IsNullOrWhiteSpace(failure?.Message), "An unexpected error occurred.", failure.Message) &
                    Environment.NewLine &
                    Environment.NewLine &
                    If(unsavedChanges, "There are unsaved changes. ", "") &
                    "Keep working? Choose No to close PaperRoute.",
                    "PaperRoute",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Error,
                    MessageBoxDefaultButton.Button1)

            Return If(answer = DialogResult.No, DialogResult.Abort, DialogResult.Cancel)
        Catch ex As Exception
            Return DialogResult.Cancel
        End Try

    End Function


    ' A save failed since the last good one (#113): closing would lose the
    ' changes it did not write, so closing asks first. Yes tries the save
    ' again, No closes without saving, Cancel stays. Returns False to stay.
    Friend Function ConfirmCloseUnsavedLibrary() As Boolean

        If Not librarySaveFailed Then
            Return True
        End If

        Dim choice As DialogResult =
            If(unsavedChangesPrompt IsNot Nothing,
               unsavedChangesPrompt("your library"),
               MessageBox.Show(
                   Me,
                   "Your latest changes have not been saved." &
                   Environment.NewLine &
                   Environment.NewLine &
                   "Save again before closing? Choose No to close without saving.",
                   "Unsaved Changes",
                   MessageBoxButtons.YesNoCancel,
                   MessageBoxIcon.Warning))

        Select Case choice
            Case DialogResult.Yes
                Return SaveManuscripts()
            Case DialogResult.No
                Return True
            Case Else
                Return False
        End Select

    End Function


    Protected Overrides Sub OnFormClosing(e As FormClosingEventArgs)

        ' Windows is shutting down (#123): a prompt would hold it up, so the
        ' open page's working copy goes to a recovery draft instead. A save
        ' that failed (#113) left its changes in the model, not the page, so
        ' no draft holds them: the save is tried once more, without a
        ' question, and only a second failure shows its message. A save,
        ' backup or restore still running (#132) is never cut off: it ends
        ' first, and what it ended in counts here. A restore whose swap went
        ' through hands over instead (#116): the page's edits belong to the
        ' library it replaced, so no draft of them goes to the restored one.
        If e.CloseReason = CloseReason.WindowsShutDown Then
            FinishLibraryWorkForShutdown()

            If handingOverAfterRestore Then
                DiscardRecoveryDraft()
            Else
                WriteRecoveryDraft("Windows was shutting down")
            End If

            If librarySaveFailed Then
                SaveManuscripts()
            End If

            MyBase.OnFormClosing(e)
            Return
        End If

        ' A restore has replaced the library on disk and is handing over to
        ' a new session (#116): what is in memory is the replaced library,
        ' so nothing is asked and nothing is saved, and a draft this session
        ' wrote of it goes too.
        If handingOverAfterRestore Then
            DiscardRecoveryDraft()
            MyBase.OnFormClosing(e)
            Return
        End If

        ' Any other close is refused while library work runs (#132): the
        ' status line says what is running, and the window can be closed
        ' once it has ended.
        If libraryWorkInProgress Then
            e.Cancel = True
        End If

        If Not e.Cancel AndAlso Not ConfirmLeaveManuscript() Then
            e.Cancel = True
        End If

        If Not e.Cancel AndAlso Not ConfirmCloseUnsavedLibrary() Then
            e.Cancel = True
        End If

        ' The page's edits were saved or let go, so a draft an error wrote
        ' earlier in this session is stale (#123).
        If Not e.Cancel Then
            DiscardRecoveryDraft()
        End If

        MyBase.OnFormClosing(e)

    End Sub

End Class
