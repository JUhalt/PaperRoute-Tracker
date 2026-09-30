Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.Globalization
Imports System.IO
Imports System.Linq
Imports System.Windows.Forms
Imports ManuscriptPipeline.Controls
Imports ManuscriptPipeline.Forms
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

' Your Citations on the Insights page (#91): how the researcher's own
' published work has been cited, from open data, only when they update it.
' Showing the tab never sends anything; the figures come from the saved
' snapshot and are computed on this computer.
Partial Public Class Form1

    Private tabInsightsCitations As ShelfTabButton = Nothing
    Private insightsFootnote As Label = Nothing

    ' Tests supply a store in their own folder and a stand-in for the update.
    Friend citationStoreFactory As Func(Of CitationStore) = Nothing
    Friend citationsUpdatePrompt As Func(Of CitationSnapshot, CitationSnapshot) = Nothing


    Private Function CurrentCitationStore() As CitationStore
        Return If(citationStoreFactory?.Invoke(), New CitationStore())
    End Function


    ' The researcher's ORCID iD, normalized, or "".
    Private Function OwnOrcidNormalized() As String
        Try
            Dim value As String = OwnOrcid()
            Return If(value.Length = 0, String.Empty, OrcidIdentifierService.NormalizeAndValidate(value))
        Catch ex As ArgumentException
            Return String.Empty
        End Try
    End Function


    Private Function CreateCitationsView() As Control

        Dim dpi As Integer = DeviceDpi
        Dim host As New Panel With {.Dock = DockStyle.Fill, .AutoScroll = True, .BackColor = UiTheme.BoardBackground()}
        Dim stack As New TableLayoutPanel With {
            .Location = New Point(0, 0), .AutoSize = True, .AutoSizeMode = AutoSizeMode.GrowAndShrink,
            .ColumnCount = 1, .Margin = New Padding(0), .Padding = New Padding(0), .BackColor = UiTheme.BoardBackground()
        }
        stack.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        host.Controls.Add(stack)

        Dim section As New SectionCard With {
            .AutoSize = True, .AutoSizeMode = AutoSizeMode.GrowAndShrink,
            .Padding = New Padding(UiTheme.Px(16, dpi), UiTheme.Px(12, dpi), UiTheme.Px(16, dpi), UiTheme.Px(14, dpi)),
            .Margin = New Padding(0, 0, 0, UiTheme.Px(10, dpi)),
            .BackColor = UiTheme.CardBackground(), .ForeColor = UiTheme.PrimaryText(), .AccessibleName = "Your citations"
        }
        Dim card As New TableLayoutPanel With {
            .AutoSize = True, .AutoSizeMode = AutoSizeMode.GrowAndShrink,
            .ColumnCount = 1, .Margin = New Padding(0), .Padding = New Padding(0), .BackColor = UiTheme.CardBackground()
        }
        card.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        section.Controls.Add(card)
        AddRow(stack, section)

        Dim wrapping As New List(Of Label)()
        Dim tiles As FlowLayoutPanel = Nothing
        Dim grid As DataGridView = Nothing
        Dim addText As Func(Of String, Color, Label) =
            Function(text, color)
                Dim label As New Label With {.Text = text, .AutoSize = True, .UseMnemonic = False, .ForeColor = color, .BackColor = UiTheme.CardBackground(), .Margin = New Padding(0, UiTheme.Px(4, dpi), 0, UiTheme.Px(4, dpi))}
                AddRow(card, label)
                wrapping.Add(label)
                Return label
            End Function

        Dim snapshot As CitationSnapshot = Nothing
        Dim unreadable As Boolean = False
        Try
            snapshot = CurrentCitationStore().Load()
        Catch ex As Exception When TypeOf ex Is InvalidDataException OrElse TypeOf ex Is IOException OrElse TypeOf ex Is UnauthorizedAccessException
            unreadable = True
        End Try

        Dim isExample As Boolean = ExampleLibraryService.IsActive
        Dim orcid As String = OwnOrcidNormalized()
        Dim blocked As OnlineBlockReason? = OnlineAccess.BlockReason(OnlineServiceCatalog.Citations)

        Dim updateButton As New Button With {.Text = "Update from OpenAlex...", .AutoSize = True, .MinimumSize = New Size(UiTheme.Px(96, dpi), UiTheme.Px(34, dpi)), .Margin = New Padding(0, UiTheme.Px(8, dpi), 0, 0)}
        AddHandler updateButton.Click, Sub(sender, e) UpdateCitations()
        updateButton.Enabled = Not isExample AndAlso orcid.Length > 0 AndAlso Not blocked.HasValue

        If unreadable Then addText("PaperRoute couldn't read your saved citations. Updating them again replaces the file.", UiTheme.InfoColor())

        If snapshot Is Nothing AndAlso Not isExample AndAlso orcid.Length = 0 Then

            addText("See how your published work has been cited, using open data. Mark yourself in Authors & Affiliations (Edit, then This is me) and add your ORCID iD.", UiTheme.PrimaryText())
            Dim open As New Button With {.Text = "Open Authors && Affiliations", .AutoSize = True, .MinimumSize = New Size(UiTheme.Px(96, dpi), UiTheme.Px(34, dpi)), .Margin = New Padding(0, UiTheme.Px(8, dpi), 0, 0)}
            AddHandler open.Click, Sub(sender, e) ShowOwnAuthorRecord()
            AddRow(card, open)

        ElseIf snapshot Is Nothing Then

            addText("See how your published work has been cited, from OpenAlex, an open index of scholarly works. Update from OpenAlex reads the works on your public ORCID record (" & orcid & ") and looks them up, with your published manuscripts' DOIs, in OpenAlex. You confirm which works are yours before anything is saved.", UiTheme.PrimaryText())
            If isExample Then addText("The example library has no citations to show.", UiTheme.SecondaryText())
            AddRow(card, updateButton)

        Else

            Dim retrievalYear As Integer = If(snapshot.RetrievedUtc, DateTime.UtcNow).Year
            Dim summary As CitationSummary = CitationMetricsService.Summarize(snapshot.Works, retrievalYear)
            Dim culture As CultureInfo = CultureInfo.CurrentCulture

            tiles = New FlowLayoutPanel With {.AutoSize = True, .AutoSizeMode = AutoSizeMode.GrowAndShrink, .WrapContents = True, .Margin = New Padding(0), .BackColor = UiTheme.CardBackground()}
            For Each tile In {
                (summary.Citations.ToString("N0", culture), "Citations", "The sum of each confirmed work's citations in OpenAlex."),
                (summary.HIndex.ToString(culture), "h-index", CitationMetricsService.HDefinition),
                (summary.I10Index.ToString(culture), "i10-index", CitationMetricsService.I10Definition),
                (summary.GIndex.ToString(culture), "g-index", CitationMetricsService.GDefinition),
                (If(summary.MQuotient.HasValue, summary.MQuotient.Value.ToString("N2", culture), "–"), "m-quotient", CitationMetricsService.MDefinition)
            }
                Dim control As Control = CreateStatTile(tile.Item1, tile.Item2, UiTheme.PrimaryText())
                control.AccessibleDescription = tile.Item3
                cardToolTip.SetToolTip(control, tile.Item3)
                For Each child As Control In control.Controls
                    cardToolTip.SetToolTip(child, tile.Item3)
                Next
                tiles.Controls.Add(control)
            Next
            AddRow(card, tiles)

            Dim retrievedOn As String = If(snapshot.RetrievedUtc.HasValue, snapshot.RetrievedUtc.Value.ToLocalTime().ToString("MMM d, yyyy", culture), "an unknown date")
            If snapshot.Source = JournalFactCatalog.ExampleSource Then
                addText("Fictional figures for the example library, for " & summary.Works.ToString(culture) & If(summary.Works = 1, " work.", " works."), UiTheme.SecondaryText())
            Else
                addText("From OpenAlex on " & retrievedOn & ", for the " & summary.Works.ToString("N0", culture) & If(summary.Works = 1, " work", " works") & " you confirmed" &
                        If(snapshot.Orcid.Length > 0, " (ORCID iD " & snapshot.Orcid & ")", String.Empty) & ".", UiTheme.SecondaryText())
            End If
            If Not isExample AndAlso snapshot.Orcid.Length > 0 AndAlso orcid.Length > 0 AndAlso snapshot.Orcid <> orcid Then
                addText("These figures are for another ORCID iD than the one marked as yours (" & orcid & "). Update to use yours.", UiTheme.InfoColor())
            End If

            Dim years As List(Of YearCount) = CitationMetricsService.PerYear(snapshot.Works, retrievalYear)
            addText("Citations by year: " & String.Join("  ·  ", years.Select(Function(item) item.Year.ToString(CultureInfo.InvariantCulture) & " " & item.Count.ToString("N0", culture) &
                    If(item.Year = retrievalYear, " so far", String.Empty))), UiTheme.PrimaryText())

            If blocked.HasValue AndAlso Not isExample Then
                addText(If(blocked.Value = OnlineBlockReason.WorkOffline,
                           "You're working offline, so updating is off. Turn off Work offline in Settings to update.",
                           "Your citations is turned off in Settings > Preferences... > Online services, so updating is off."), UiTheme.InfoColor())
            End If
            If isExample Then addText("Updating is off in the example library.", UiTheme.SecondaryText())
            AddRow(card, updateButton)

            ' The researcher's manuscripts in PaperRoute, joined by DOI.
            Dim byDoi As Dictionary(Of String, CitedWork) = snapshot.Works.Where(Function(item) item.Doi.Length > 0).GroupBy(Function(item) item.Doi).ToDictionary(Function(group) group.Key, Function(group) group.First())
            Dim tracked As List(Of (Manuscript As Manuscript, Work As CitedWork)) = manuscripts.
                Where(Function(item) item IsNot Nothing).
                Select(Function(item) (item, If(byDoi.ContainsKey(CitationKeys.Doi(If(item.Metadata?.Doi, String.Empty))), byDoi(CitationKeys.Doi(If(item.Metadata?.Doi, String.Empty))), Nothing))).
                Where(Function(item) item.Item2 IsNot Nothing).
                ToList()

            If tracked.Count > 0 Then
                Dim heading As New Label With {.Text = "Your manuscripts in PaperRoute", .AutoSize = True, .Font = New Font(Me.Font, FontStyle.Bold), .ForeColor = UiTheme.PrimaryText(), .Margin = New Padding(0, UiTheme.Px(4, dpi), 0, UiTheme.Px(6, dpi))}
                AddRow(stack, heading)
                grid = CreateCitationsGrid(tracked, retrievalYear)
                AddRow(stack, grid)
            End If
            Dim trackedKeys As New HashSet(Of String)(tracked.Select(Function(item) CitationKeys.ForWork(item.Work)), StringComparer.OrdinalIgnoreCase)
            Dim others As Integer = snapshot.Works.Where(Function(item) Not trackedKeys.Contains(CitationKeys.ForWork(item))).Count()
            If others > 0 Then
                AddRow(stack, New Label With {.Text = "Other works on your record: " & others.ToString("N0", culture) & " (not tracked in PaperRoute).", .AutoSize = True, .UseMnemonic = False, .ForeColor = UiTheme.SecondaryText(), .Margin = New Padding(0, UiTheme.Px(6, dpi), 0, 0)})
            End If

        End If

        Dim notes As New Label With {
            .Text = CitationMetricsService.HDefinition & " " & CitationMetricsService.I10Definition & " " & CitationMetricsService.GDefinition & " " & CitationMetricsService.MDefinition &
                    Environment.NewLine & CitationMetricsService.FwciDefinition & Environment.NewLine & CitationMetricsService.PerYearNote & " " & CitationMetricsService.DatabasesNote,
            .AutoSize = True, .UseMnemonic = False, .ForeColor = UiTheme.SecondaryText(), .Margin = New Padding(0, UiTheme.Px(10, dpi), 0, 0)
        }
        AddRow(stack, notes)

        Dim fit As Action =
            Sub()
                Dim width As Integer = Math.Max(UiTheme.Px(420, dpi), host.ClientSize.Width - If(host.VerticalScroll.Visible, 0, SystemInformation.VerticalScrollBarWidth) - UiTheme.Px(2, dpi))
                Dim inner As Integer = width - section.Padding.Horizontal
                stack.SuspendLayout()
                section.MinimumSize = New Size(width, 0)
                section.MaximumSize = New Size(width, 0)
                If tiles IsNot Nothing Then
                    tiles.MinimumSize = New Size(inner, 0)
                    tiles.MaximumSize = New Size(inner, 0)
                End If
                For Each label As Label In wrapping
                    label.MaximumSize = New Size(inner, 0)
                Next
                For Each label As Label In stack.Controls.OfType(Of Label)()
                    label.MaximumSize = New Size(width, 0)
                Next
                ' Tall enough for every row, so the page scrolls instead of the grid.
                If grid IsNot Nothing Then grid.Size = New Size(width, grid.ColumnHeadersHeight + grid.Rows.GetRowsHeight(DataGridViewElementStates.Visible) + UiTheme.Px(2, dpi))
                ' The tiles re-flow for the new width before the card sizes to them.
                tiles?.PerformLayout()
                card.PerformLayout()
                stack.ResumeLayout(True)
            End Sub
        AddHandler host.ClientSizeChanged, Sub(sender, e) fit()
        fit()
        Return host

    End Function


    Private Shared Sub AddRow(table As TableLayoutPanel, control As Control)
        Dim row As Integer = table.RowCount
        table.RowCount = row + 1
        table.RowStyles.Add(New RowStyle(SizeType.AutoSize))
        table.Controls.Add(control, 0, row)
    End Sub


    Private Function CreateCitationsGrid(tracked As List(Of (Manuscript As Manuscript, Work As CitedWork)), retrievalYear As Integer) As DataGridView

        Dim dpi As Integer = DeviceDpi
        Dim grid As DataGridView = CreatePageGrid("Your manuscripts in PaperRoute with their citations. Press Enter to open the selected manuscript.")
        grid.Dock = DockStyle.None
        grid.Margin = New Padding(0)
        grid.ScrollBars = ScrollBars.None

        grid.Columns.Add(New DataGridViewTextBoxColumn With {.Name = "Title", .HeaderText = "Manuscript", .AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, .FillWeight = 220, .MinimumWidth = UiTheme.Px(200, dpi)})
        grid.Columns.Add(New DataGridViewTextBoxColumn With {.Name = "Journal", .HeaderText = "Journal", .AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, .FillWeight = 110})
        grid.Columns.Add(New DataGridViewTextBoxColumn With {.Name = "Published", .HeaderText = "Published", .AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells, .ValueType = GetType(DateTime)})
        grid.Columns("Published").DefaultCellStyle.Format = "d"
        grid.Columns.Add(New DataGridViewTextBoxColumn With {.Name = "Citations", .HeaderText = "Citations", .AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells, .ValueType = GetType(Double)})
        grid.Columns("Citations").DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight
        grid.Columns.Add(New DataGridViewTextBoxColumn With {.Name = "Fwci", .HeaderText = "FWCI", .AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells, .ToolTipText = CitationMetricsService.FwciDefinition})
        grid.Columns.Add(New DataGridViewTextBoxColumn With {.Name = "Percentile", .HeaderText = "Percentile", .AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells, .ToolTipText = "Among works of the same type, year, and field, in OpenAlex."})
        grid.Columns.Add(New DataGridViewTextBoxColumn With {.Name = "Journals", .HeaderText = "Journals tried", .AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells, .ValueType = GetType(Double)})
        grid.Columns("Journals").DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight
        For Each column As DataGridViewColumn In grid.Columns
            column.SortMode = DataGridViewColumnSortMode.Automatic
        Next

        For Each entry In tracked.OrderByDescending(Function(item) RouteAnalyticsService.PublishedDateOf(item.Manuscript))
            Dim published As DateTime? = RouteAnalyticsService.PublishedDateOf(entry.Manuscript)
            Dim index As Integer = grid.Rows.Add(
                entry.Manuscript.Title,
                If(String.IsNullOrWhiteSpace(entry.Work.Journal), If(entry.Manuscript.Metadata?.PublicationJournal, String.Empty), entry.Work.Journal),
                If(published.HasValue, CObj(published.Value), Nothing),
                CDbl(entry.Work.CitedByCount),
                CitationsService.FwciText(entry.Work, retrievalYear),
                CitationsService.PercentileText(entry.Work),
                CDbl(RouteAnalyticsService.DescribeManuscript(entry.Manuscript, DateTime.Today).JournalCount))
            grid.Rows(index).Tag = entry.Manuscript
        Next

        AddHandler grid.CellDoubleClick, Sub(sender, e) If e.RowIndex >= 0 Then OpenInsightRoute(grid.Rows(e.RowIndex))
        AddHandler grid.KeyDown,
            Sub(sender, e)
                If e.KeyCode = Keys.Enter AndAlso grid.CurrentRow IsNot Nothing Then
                    e.Handled = True
                    OpenInsightRoute(grid.CurrentRow)
                End If
            End Sub
        Return grid

    End Function


    ' Opens Authors & Affiliations so the researcher can mark themselves.
    Private Sub ShowOwnAuthorRecord()
        NavigateTo(WorkspacePage.Library)
        If currentPage = WorkspacePage.Library AndAlso tabLibraryAuthors IsNot Nothing Then
            tabLibraryAuthors.Checked = True
            lblStatus.Text = "Select yourself, choose Edit, check This is me, and add your ORCID iD. Then come back to Insights > Your Citations."
        End If
    End Sub


    Private Sub UpdateCitations()

        Dim blocked As OnlineBlockReason? = OnlineAccess.BlockReason(OnlineServiceCatalog.Citations)
        If blocked.HasValue AndAlso citationsUpdatePrompt Is Nothing Then
            MessageBox.Show(Me, OnlineAccess.BlockedMessage(OnlineServiceCatalog.Find(OnlineServiceCatalog.Citations), blocked.Value),
                            "Your Citations", MessageBoxButtons.OK, MessageBoxIcon.Information)
            FillInsights()
            Return
        End If

        Dim orcid As String = OwnOrcidNormalized()
        If orcid.Length = 0 OrElse ExampleLibraryService.IsActive Then Return

        Dim store As CitationStore = CurrentCitationStore()
        Dim previous As CitationSnapshot = Nothing
        Try
            previous = store.Load()
        Catch ex As Exception When TypeOf ex Is InvalidDataException OrElse TypeOf ex Is IOException OrElse TypeOf ex Is UnauthorizedAccessException
            previous = Nothing
        End Try

        Dim updated As CitationSnapshot
        If citationsUpdatePrompt IsNot Nothing Then
            updated = citationsUpdatePrompt(previous)
        Else
            Using dialog As New CitationsUpdateForm(orcid, CitationsService.PublishedDois(manuscripts), previous, New OnlineCitationSource())
                updated = If(dialog.ShowDialog(Me) = DialogResult.OK, dialog.Result, Nothing)
            End Using
        End If
        If updated Is Nothing Then Return

        Try
            store.Save(updated)
            lblStatus.Text = "Your citations were updated from OpenAlex."
        Catch ex As Exception When TypeOf ex Is IOException OrElse TypeOf ex Is UnauthorizedAccessException
            MessageBox.Show(Me, "PaperRoute couldn't save your citations, so the figures weren't changed." & Environment.NewLine & Environment.NewLine & ex.Message,
                            "Your Citations", MessageBoxButtons.OK, MessageBoxIcon.Warning)
        End Try
        FillInsights()

    End Sub


    ' For tests.
    Friend Sub UpdateCitationsForTest()
        UpdateCitations()
    End Sub

End Class
