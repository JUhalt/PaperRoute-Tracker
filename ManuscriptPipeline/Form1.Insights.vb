Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.Globalization
Imports System.Linq
Imports System.Windows.Forms
Imports ManuscriptPipeline.Controls
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

' The Insights page (#30, #62): how your work has moved through journals,
' from your own records. Calculated on this computer each time the page is
' shown; nothing here changes a record. Your Citations (#91) adds figures
' from OpenAlex, only when the researcher updates them.
Partial Public Class Form1

    Private insightsContent As Panel = Nothing
    Private insightsGrid As DataGridView = Nothing
    Private insightsJournal As JournalHistory = Nothing
    Private tabInsightsJournals As ShelfTabButton = Nothing
    Private tabInsightsRoutes As ShelfTabButton = Nothing
    Private tabInsightsMap As ShelfTabButton = Nothing
    ' The Route Map shows published routes unless asked for all of them.
    Private insightsMapAll As Boolean = False
    ' Opens a route from the Route Map; tests replace the dialog.
    Friend routeViewOpener As Action(Of Manuscript) = Nothing
    Private insightsTiles As FlowLayoutPanel = Nothing
    Private insightsScope As LinkLabel = Nothing


    Private Function BuildInsightsPage() As Control

        insightsJournal = Nothing

        Dim btnReport As New ActionButton With {
            .Text = "Pipeline Report...",
            .Width = GetResponsiveButtonWidth("Pipeline Report...", 140),
            .Height = GetResponsiveButtonHeight(34),
            .AccessibleDescription = "Saves a read-only summary of the pipeline to share with a supervisor or coauthor."
        }
        AddHandler btnReport.Click, AddressOf SavePipelineReport

        Dim frame As TableLayoutPanel = CreatePageFrame(
            "Insights",
            "How your work has moved through journals, from your own PaperRoute records. Calculated on this computer; missing dates are left out, never estimated. Your Citations adds figures from OpenAlex, only when you update them.",
            btnReport)

        Dim dpi As Integer = DeviceDpi
        Dim body As New TableLayoutPanel With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 1,
            .RowCount = 5,
            .Margin = New Padding(0),
            .BackColor = UiTheme.BoardBackground()
        }
        body.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        body.RowStyles.Add(New RowStyle(SizeType.AutoSize))
        body.RowStyles.Add(New RowStyle(SizeType.AutoSize))
        body.RowStyles.Add(New RowStyle(SizeType.AutoSize))
        body.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        body.RowStyles.Add(New RowStyle(SizeType.AutoSize))

        insightsTiles = New FlowLayoutPanel With {
            .AutoSize = True,
            .AutoSizeMode = AutoSizeMode.GrowAndShrink,
            .WrapContents = True,
            .Dock = DockStyle.Top,
            .Margin = New Padding(0, 0, 0, UiTheme.Px(10, dpi)),
            .BackColor = UiTheme.BoardBackground(),
            .AccessibleName = "At a glance"
        }
        body.Controls.Add(insightsTiles, 0, 0)

        Dim views As New FlowLayoutPanel With {
            .Dock = DockStyle.Top,
            .AutoSize = True,
            .AutoSizeMode = AutoSizeMode.GrowAndShrink,
            .WrapContents = False,
            .Margin = New Padding(0, 0, 0, UiTheme.Px(6, dpi)),
            .BackColor = UiTheme.BoardBackground()
        }
        AddHandler views.Paint,
            Sub(sender, e)
                Using line As New Pen(UiTheme.CardBorder())
                    e.Graphics.DrawLine(line, 0, views.Height - 1, views.Width, views.Height - 1)
                End Using
            End Sub
        tabInsightsJournals = New ShelfTabButton() With {.Text = "Your Journals"}
        tabInsightsRoutes = New ShelfTabButton() With {.Text = "Your Routes"}
        tabInsightsMap = New ShelfTabButton() With {.Text = "Route Map"}
        tabInsightsCitations = New ShelfTabButton() With {.Text = "Your Citations"}
        views.Controls.Add(tabInsightsJournals)
        views.Controls.Add(tabInsightsRoutes)
        views.Controls.Add(tabInsightsMap)
        views.Controls.Add(tabInsightsCitations)
        body.Controls.Add(views, 0, 1)

        ' "Showing manuscripts sent to X · Show all" while a journal is chosen.
        insightsScope = New LinkLabel With {
            .AutoSize = True,
            .UseMnemonic = False,
            .Visible = False,
            .LinkColor = UiTheme.AccentColor(),
            .ActiveLinkColor = UiTheme.AccentColor(),
            .VisitedLinkColor = UiTheme.AccentColor(),
            .ForeColor = UiTheme.SecondaryText(),
            .Margin = New Padding(0, 0, 0, UiTheme.Px(6, dpi))
        }
        AddHandler insightsScope.LinkClicked,
            Sub(sender, e)
                insightsJournal = Nothing
                FillInsights()
            End Sub
        body.Controls.Add(insightsScope, 0, 2)

        insightsContent = New Panel With {.Dock = DockStyle.Fill, .Margin = New Padding(0), .BackColor = UiTheme.BoardBackground()}
        body.Controls.Add(insightsContent, 0, 3)

        insightsFootnote = New Label With {
            .Text = "Days are calendar days between recorded dates. A first decision is the earliest decision recorded for a submission; " &
                    "review time leaves out desk rejections. Journals group by their Journal Library record or exact name, never by a guess.",
            .AutoSize = True,
            .UseMnemonic = False,
            .ForeColor = UiTheme.SecondaryText(),
            .Margin = New Padding(0, UiTheme.Px(8, dpi), 0, 0)
        }
        body.Controls.Add(insightsFootnote, 0, 4)
        AddHandler body.SizeChanged,
            Sub(sender, e)
                For Each note As Label In body.Controls.OfType(Of Label)()
                    note.MaximumSize = New Size(Math.Max(UiTheme.Px(300, dpi), body.ClientSize.Width - UiTheme.Px(8, dpi)), 0)
                Next
            End Sub

        frame.Controls.Add(body, 0, 2)

        tabInsightsJournals.Checked = True
        AddHandler tabInsightsJournals.CheckedChanged, Sub(sender, e) If tabInsightsJournals.Checked Then FillInsights()
        AddHandler tabInsightsRoutes.CheckedChanged, Sub(sender, e) If tabInsightsRoutes.Checked Then FillInsights()
        AddHandler tabInsightsMap.CheckedChanged, Sub(sender, e) If tabInsightsMap.Checked Then FillInsights()
        AddHandler tabInsightsCitations.CheckedChanged, Sub(sender, e) If tabInsightsCitations.Checked Then FillInsights()

        FillInsights()
        Return frame

    End Function


    Private Function CurrentJournalLibrary() As IEnumerable(Of JournalRecord)
        Return If(authorLibrary?.Journals, New List(Of JournalRecord)())
    End Function


    Private Sub FillInsights()

        If insightsContent Is Nothing OrElse insightsContent.IsDisposed Then Return

        Dim statistics As LibraryStatistics = RouteAnalyticsService.ForLibrary(manuscripts, DateTime.Today, CurrentJournalLibrary())

        ' A chosen journal may no longer exist after an edit.
        If insightsJournal IsNot Nothing Then
            Dim name As String = insightsJournal.JournalName
            insightsJournal = statistics.Journals.FirstOrDefault(Function(item) item.JournalName = name)
        End If

        FillInsightTiles(statistics)

        tabInsightsJournals.Text = "Your Journals (" & statistics.Journals.Count.ToString(CultureInfo.CurrentCulture) & ")"
        tabInsightsRoutes.Text = "Your Routes (" & statistics.Manuscripts.Where(Function(item) item.Submissions.Count > 0).Count().ToString(CultureInfo.CurrentCulture) & ")"

        For Each child As Control In insightsContent.Controls.Cast(Of Control)().ToList()
            insightsContent.Controls.Remove(child)
            child.Dispose()
        Next

        insightsScope.Visible = insightsJournal IsNot Nothing AndAlso Not tabInsightsJournals.Checked AndAlso Not tabInsightsCitations.Checked
        insightsFootnote.Visible = Not tabInsightsCitations.Checked
        If insightsScope.Visible Then
            insightsScope.Text = "Showing manuscripts sent to " & insightsJournal.JournalName & "  ·  Show all"
            insightsScope.LinkArea = New LinkArea(insightsScope.Text.Length - 8, 8)
        End If

        If tabInsightsCitations.Checked Then
            insightsGrid = Nothing
            insightsContent.Controls.Add(CreateCitationsView())
            Return
        End If

        If tabInsightsMap.Checked Then
            insightsGrid = Nothing
            insightsContent.Controls.Add(CreateRouteMapView())
            Return
        End If

        insightsGrid = If(tabInsightsRoutes.Checked, CreateRoutesGrid(statistics), CreateJournalsGrid(statistics))
        insightsContent.Controls.Add(insightsGrid)

    End Sub


    Private Sub FillInsightTiles(statistics As LibraryStatistics)

        insightsTiles.SuspendLayout()
        For Each child As Control In insightsTiles.Controls.Cast(Of Control)().ToList()
            insightsTiles.Controls.Remove(child)
            child.Dispose()
        Next

        Dim published As Integer = manuscripts.Where(Function(item) item.Location = ManuscriptLocation.Published).Count()
        Dim submissions As Integer = statistics.Submissions.Count()

        insightsTiles.Controls.Add(CreateStatTile(manuscripts.Count.ToString(CultureInfo.CurrentCulture),
            If(manuscripts.Count = 1, "manuscript", "manuscripts") & "  ·  " & published.ToString(CultureInfo.CurrentCulture) & " published", UiTheme.PrimaryText()))
        insightsTiles.Controls.Add(CreateStatTile(submissions.ToString(CultureInfo.CurrentCulture),
            If(submissions = 1, "submission", "submissions") & "  ·  " & statistics.CountOutcome(SubmissionOutcome.Accepted).ToString(CultureInfo.CurrentCulture) & " accepted", UiTheme.PrimaryText()))
        insightsTiles.Controls.Add(CreateStatTile(DaysText(statistics.MedianDaysToFirstDecision),
            "median to a first decision" & SampleText(statistics.MedianDaysToFirstDecision), UiTheme.AccentColor()))
        insightsTiles.Controls.Add(CreateStatTile(DaysText(statistics.MedianDaysToAcceptance),
            "median from first submission to acceptance" & SampleText(statistics.MedianDaysToAcceptance), UiTheme.AccentColor()))

        insightsTiles.ResumeLayout(True)

    End Sub


    Private Shared Function DaysText(median As Median) As String
        If Not median.HasValue Then Return "—"
        Dim days As Double = median.Value.Value
        Return days.ToString(If(days = Math.Floor(days), "0", "0.0"), CultureInfo.CurrentCulture) & If(days = 1, " day", " days")
    End Function


    Private Shared Function SampleText(median As Median) As String
        Return If(median.SampleSize = 0, " (not recorded yet)", " (of " & median.SampleSize.ToString(CultureInfo.CurrentCulture) & ")")
    End Function


    ' A number over a caption, as on the Deadlines page.
    Private Function CreateStatTile(value As String, caption As String, valueColor As Color) As Control

        Dim dpi As Integer = DeviceDpi
        Dim panel As New RoundedPanel With {
            .BackColor = UiTheme.CardBackground(),
            .BorderColor = UiTheme.CardBorder(),
            .BorderThickness = 1.0F,
            .CornerRadius = UiTheme.Px(UiTheme.ControlRadius, dpi),
            .Padding = New Padding(UiTheme.Px(12, dpi), UiTheme.Px(8, dpi), UiTheme.Px(12, dpi), UiTheme.Px(8, dpi)),
            .Margin = New Padding(0, 0, UiTheme.Px(10, dpi), UiTheme.Px(6, dpi)),
            .AccessibleName = value & " " & caption,
            .AccessibleRole = AccessibleRole.StaticText
        }
        Dim number As New Label With {
            .AutoSize = True,
            .Text = value,
            .UseMnemonic = False,
            .Font = New Font(Me.Font.FontFamily, Me.Font.SizeInPoints * 1.45F, FontStyle.Bold),
            .ForeColor = valueColor,
            .BackColor = UiTheme.CardBackground(),
            .Location = New Point(panel.Padding.Left, panel.Padding.Top)
        }
        Dim text As New Label With {
            .AutoSize = True,
            .Text = caption,
            .UseMnemonic = False,
            .ForeColor = UiTheme.SecondaryText(),
            .BackColor = UiTheme.CardBackground()
        }
        panel.Controls.Add(number)
        panel.Controls.Add(text)
        text.Location = New Point(panel.Padding.Left, number.Bottom)
        panel.Size = New Size(
            Math.Max(UiTheme.Px(150, dpi), Math.Max(number.PreferredWidth, TextRenderer.MeasureText(caption, Me.Font).Width) + panel.Padding.Horizontal),
            text.Bottom + panel.Padding.Bottom)
        Return panel

    End Function


    Private Function CreateJournalsGrid(statistics As LibraryStatistics) As DataGridView

        Dim grid As DataGridView = CreatePageGrid("Your journals. Press Enter to see the manuscripts sent to the selected journal.")
        Dim dpi As Integer = DeviceDpi

        grid.Columns.Add(New DataGridViewTextBoxColumn With {.Name = "Journal", .HeaderText = "Journal", .AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, .FillWeight = 200, .MinimumWidth = UiTheme.Px(200, dpi)})
        For Each column In {("Submissions", "Submissions"), ("Accepted", "Accepted"), ("Revisions", "Asked to revise"),
                            ("Rejected", "Rejected after review"), ("Desk", "Desk rejected"), ("FirstDecision", "Median days to first decision"),
                            ("Review", "Median days in review")}
            grid.Columns.Add(New DataGridViewTextBoxColumn With {.Name = column.Item1, .HeaderText = column.Item2, .AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells, .ValueType = GetType(Double)})
            grid.Columns(column.Item1).DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight
        Next
        grid.Columns.Add(New DataGridViewTextBoxColumn With {.Name = "Last", .HeaderText = "Last submitted", .AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells, .ValueType = GetType(DateTime)})
        grid.Columns("Last").DefaultCellStyle.Format = "d"
        For Each column As DataGridViewColumn In grid.Columns
            column.SortMode = DataGridViewColumnSortMode.Automatic
        Next

        For Each history As JournalHistory In statistics.Journals
            Dim rejected As Integer = history.CountOutcome(SubmissionOutcome.RejectedAfterReview) + history.CountOutcome(SubmissionOutcome.Rejected)
            Dim index As Integer = grid.Rows.Add(
                history.JournalName,
                CDbl(history.Count),
                CDbl(history.CountOutcome(SubmissionOutcome.Accepted)),
                CDbl(history.WithRevisions),
                CDbl(rejected),
                CDbl(history.CountOutcome(SubmissionOutcome.DeskRejected)),
                MedianCell(history.MedianDaysToFirstDecision),
                MedianCell(history.MedianReviewDays),
                history.LastSubmitted)
            grid.Rows(index).Tag = history
        Next

        Dim openJournal As Action(Of DataGridViewRow) =
            Sub(row)
                insightsJournal = TryCast(row?.Tag, JournalHistory)
                If insightsJournal IsNot Nothing Then tabInsightsRoutes.Checked = True
            End Sub
        AddHandler grid.CellDoubleClick, Sub(sender, e) If e.RowIndex >= 0 Then openJournal(grid.Rows(e.RowIndex))
        AddHandler grid.KeyDown,
            Sub(sender, e)
                If e.KeyCode = Keys.Enter AndAlso grid.CurrentRow IsNot Nothing Then
                    e.Handled = True
                    openJournal(grid.CurrentRow)
                End If
            End Sub

        EmptyHint.Attach(grid, Function() "No submissions recorded yet. Record one on a manuscript's Submissions tab, and your history with each journal appears here.")
        Return grid

    End Function


    Private Function CreateRoutesGrid(statistics As LibraryStatistics) As DataGridView

        Dim grid As DataGridView = CreatePageGrid("Your routes. Press Enter to open the selected manuscript.")
        Dim dpi As Integer = DeviceDpi

        grid.Columns.Add(New DataGridViewTextBoxColumn With {.Name = "Title", .HeaderText = "Manuscript", .AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, .FillWeight = 220, .MinimumWidth = UiTheme.Px(220, dpi)})
        grid.Columns.Add(New DataGridViewTextBoxColumn With {.Name = "Stage", .HeaderText = "Stage", .AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells})
        grid.Columns.Add(New DataGridViewTextBoxColumn With {.Name = "Route", .HeaderText = "Route", .AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, .FillWeight = 80})
        For Each column In {("Journals", "Journals"), ("Rounds", "Revision rounds"), ("Acceptance", "Days to acceptance"), ("Publication", "Days to publication")}
            grid.Columns.Add(New DataGridViewTextBoxColumn With {.Name = column.Item1, .HeaderText = column.Item2, .AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells, .ValueType = GetType(Double)})
            grid.Columns(column.Item1).DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight
        Next
        grid.Columns.Add(New DataGridViewTextBoxColumn With {.Name = "First", .HeaderText = "First submitted", .AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells, .ValueType = GetType(DateTime)})
        grid.Columns("First").DefaultCellStyle.Format = "d"
        For Each column As DataGridViewColumn In grid.Columns
            column.SortMode = DataGridViewColumnSortMode.Automatic
        Next

        Dim included As HashSet(Of Manuscript) = Nothing
        If insightsJournal IsNot Nothing Then included = New HashSet(Of Manuscript)(insightsJournal.Submissions.Select(Function(item) item.Manuscript))

        For Each route As ManuscriptStatistics In statistics.Manuscripts
            If route.Submissions.Count = 0 Then Continue For
            If included IsNot Nothing AndAlso Not included.Contains(route.Manuscript) Then Continue For
            Dim index As Integer = grid.Rows.Add(
                route.Manuscript.Title,
                FormatStage(route.Manuscript.CurrentStage),
                RouteSummaryService.Describe(route.Manuscript).Text,
                CDbl(route.JournalCount),
                CDbl(route.RevisionRounds),
                If(route.DaysToAcceptance.HasValue, CObj(CDbl(route.DaysToAcceptance.Value)), Nothing),
                If(route.DaysToPublication.HasValue, CObj(CDbl(route.DaysToPublication.Value)), Nothing),
                route.FirstSubmitted.Value)
            grid.Rows(index).Tag = route.Manuscript
        Next

        AddHandler grid.CellDoubleClick, Sub(sender, e) If e.RowIndex >= 0 Then OpenInsightRoute(grid.Rows(e.RowIndex))
        AddHandler grid.KeyDown,
            Sub(sender, e)
                If e.KeyCode = Keys.Enter AndAlso grid.CurrentRow IsNot Nothing Then
                    e.Handled = True
                    OpenInsightRoute(grid.CurrentRow)
                End If
            End Sub

        EmptyHint.Attach(grid, Function() "No routes yet. A manuscript's route begins with its first recorded submission.")
        Return grid

    End Function


    ' Every route lined up at day 0 on one scale (#82): how long each took,
    ' and how much of that was waiting on journals versus the author's own
    ' turns. Published routes by default, so the lengths compare like with
    ' like.
    Private Function CreateRouteMapView() As Control

        Dim dpi As Integer = DeviceDpi
        Dim scoped As IEnumerable(Of Manuscript) = manuscripts
        If insightsJournal IsNot Nothing Then scoped = insightsJournal.Submissions.Select(Function(item) item.Manuscript).Distinct()
        Dim library As RouteMapLibrary = RouteMapService.Library(scoped, DateTime.Today, insightsMapAll)

        Dim host As New Panel With {.Dock = DockStyle.Fill, .AutoScroll = True, .BackColor = UiTheme.BoardBackground()}
        Dim section As New SectionCard With {
            .Location = New Point(0, 0),
            .AutoSize = True,
            .AutoSizeMode = AutoSizeMode.GrowAndShrink,
            .Padding = New Padding(UiTheme.Px(16, dpi), UiTheme.Px(12, dpi), UiTheme.Px(16, dpi), UiTheme.Px(14, dpi)),
            .Margin = New Padding(0),
            .BackColor = UiTheme.CardBackground(),
            .ForeColor = UiTheme.PrimaryText(),
            .AccessibleName = "Route Map"
        }
        Dim card As New TableLayoutPanel With {
            .Dock = DockStyle.Top,
            .AutoSize = True,
            .AutoSizeMode = AutoSizeMode.GrowAndShrink,
            .ColumnCount = 1,
            .RowCount = 4,
            .Padding = New Padding(0),
            .Margin = New Padding(0),
            .BackColor = UiTheme.CardBackground()
        }
        card.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        For index As Integer = 0 To 3
            card.RowStyles.Add(New RowStyle(SizeType.AutoSize))
        Next
        section.Controls.Add(card)

        Dim count As Integer = library.Routes.Count
        Dim median As Median = library.MedianDaysToPublication
        Dim summary As String
        If insightsMapAll Then
            Dim unfinished As Integer = library.Routes.Where(Function(item) Not item.IsPublished).Count()
            summary = count.ToString(CultureInfo.CurrentCulture) & If(count = 1, " route", " routes") & " lined up at day 0; " &
                      unfinished.ToString(CultureInfo.CurrentCulture) & " not yet published, drawn to today or to the last decision." &
                      If(median.HasValue, " Published: median " & DaysText(median) & " from first submission to publication.", String.Empty)
        ElseIf count = 0 Then
            summary = "No published routes yet. A route is drawn from a manuscript's first recorded submission to its publication date."
        Else
            summary = count.ToString(CultureInfo.CurrentCulture) & If(count = 1, " published route", " published routes") & " lined up at day 0." &
                      If(median.HasValue, " Median " & DaysText(median) & " from first submission to publication.", String.Empty)
        End If

        Dim header As New FlowLayoutPanel With {
            .AutoSize = True,
            .AutoSizeMode = AutoSizeMode.GrowAndShrink,
            .WrapContents = True,
            .Anchor = AnchorStyles.Left Or AnchorStyles.Top,
            .Margin = New Padding(0),
            .BackColor = UiTheme.CardBackground()
        }
        Dim lblSummary As New Label With {
            .AutoSize = True,
            .UseMnemonic = False,
            .Text = summary,
            .Font = New Font(Me.Font, FontStyle.Bold),
            .ForeColor = UiTheme.PrimaryText(),
            .BackColor = UiTheme.CardBackground(),
            .Margin = New Padding(0, 0, UiTheme.Px(12, dpi), 0)
        }
        Dim toggle As New LinkLabel With {
            .AutoSize = True,
            .UseMnemonic = False,
            .Text = If(insightsMapAll, "Show published routes only", "Include work not yet published"),
            .LinkColor = UiTheme.AccentColor(),
            .ActiveLinkColor = UiTheme.AccentColor(),
            .VisitedLinkColor = UiTheme.AccentColor(),
            .BackColor = UiTheme.CardBackground(),
            .Margin = New Padding(0)
        }
        AddHandler toggle.LinkClicked,
            Sub(sender, e)
                insightsMapAll = Not insightsMapAll
                FillInsights()
            End Sub
        header.Controls.Add(lblSummary)
        header.Controls.Add(toggle)
        card.Controls.Add(header, 0, 0)

        Dim shares As New List(Of (Kind As RouteSegmentKind, Text As String))()
        For Each item In {(RouteSegmentKind.Journal, "With a journal"), (RouteSegmentKind.Author, "With you, revising or rerouting"),
                          (RouteSegmentKind.Production, "In production"), (RouteSegmentKind.NotRecorded, "Not recorded")}
            Dim share As Double = library.Share(item.Item1)
            If share > 0 Then shares.Add((item.Item1, item.Item2 & "  ·  " & share.ToString("0%", CultureInfo.CurrentCulture)))
        Next
        card.Controls.Add(New RouteMapLegend With {
            .Anchor = AnchorStyles.Left Or AnchorStyles.Right Or AnchorStyles.Top,
            .Margin = New Padding(0, UiTheme.Px(10, dpi), 0, 0),
            .BackColor = UiTheme.CardBackground(),
            .Items = shares
        }, 0, 1)

        Dim chart As New RouteMapChart With {
            .Anchor = AnchorStyles.Left Or AnchorStyles.Right Or AnchorStyles.Top,
            .Margin = New Padding(0, UiTheme.Px(12, dpi), 0, 0),
            .BackColor = UiTheme.CardBackground(),
            .Routes = library.Routes
        }
        AddHandler chart.RouteOpened,
            Sub(sender, entry)
                If routeViewOpener IsNot Nothing Then
                    routeViewOpener(entry.Manuscript)
                Else
                    OpenRouteView(entry.Manuscript)
                End If
            End Sub
        card.Controls.Add(chart, 0, 2)

        card.Controls.Add(New Label With {
            .AutoSize = True,
            .UseMnemonic = False,
            .Text = "Percentages are shares of all " & library.TotalDays.ToString("N0", CultureInfo.CurrentCulture) & " days drawn. " &
                    "Click a route, or select it and press Enter, to open its route map." &
                    If(insightsMapAll, " A dotted end and + mark a route still under way today.", String.Empty),
            .ForeColor = UiTheme.SecondaryText(),
            .BackColor = UiTheme.CardBackground(),
            .Margin = New Padding(0, UiTheme.Px(10, dpi), 0, 0)
        }, 0, 3)

        host.Controls.Add(section)
        Dim fit As Action =
            Sub()
                Dim width As Integer = Math.Max(UiTheme.Px(420, dpi), host.ClientSize.Width - If(host.VerticalScroll.Visible, 0, SystemInformation.VerticalScrollBarWidth) - UiTheme.Px(2, dpi))
                section.MinimumSize = New Size(width, 0)
                section.MaximumSize = New Size(width, 0)
                Dim inner As Integer = width - section.Padding.Horizontal
                For Each label As Label In card.Controls.OfType(Of Label)()
                    label.MaximumSize = New Size(inner, 0)
                Next
                header.MaximumSize = New Size(inner, 0)
                lblSummary.MaximumSize = New Size(inner, 0)
            End Sub
        AddHandler host.ClientSizeChanged, Sub(sender, e) fit()
        fit()
        Return host

    End Function


    Private Shared Function MedianCell(median As Median) As Object
        Return If(median.HasValue, CObj(median.Value.Value), Nothing)
    End Function


    Private Sub OpenInsightRoute(row As DataGridViewRow)
        Dim manuscript As Manuscript = TryCast(row?.Tag, Manuscript)
        If manuscript IsNot Nothing Then OpenManuscript(manuscript)
    End Sub

End Class
