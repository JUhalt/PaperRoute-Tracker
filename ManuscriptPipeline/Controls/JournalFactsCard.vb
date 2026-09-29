Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.Linq
Imports System.Windows.Forms
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

Namespace Controls

    ' The selected journal on the Journals page (#87): its facts with their
    ' sources, its links, and its metrics, each labelled by name, source, and
    ' year and never combined into a score.
    Friend Class JournalFactsCard
        Inherits Panel

        Private ReadOnly content As New TableLayoutPanel()
        Private ReadOnly wrapping As New List(Of Control)()
        Private ReadOnly toolTip As New ToolTip()

        Public Event LookUpRequested As EventHandler
        Public Event EditRequested As EventHandler


        Public Sub New()
            SetStyle(ControlStyles.OptimizedDoubleBuffer Or ControlStyles.ResizeRedraw, True)
            AutoScroll = True
            BackColor = UiTheme.CardBackground()
            ForeColor = UiTheme.PrimaryText()
            Padding = New Padding(18, 16, 18, 16)

            content.Dock = DockStyle.Top
            content.AutoSize = True
            content.AutoSizeMode = AutoSizeMode.GrowAndShrink
            content.ColumnCount = 1
            content.BackColor = BackColor
            content.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
            Controls.Add(content)
        End Sub


        ' The command the card offers now, for tests: "Look Up Facts..." or
        ' "Refresh Facts...", or Nothing when no journal is selected.
        Friend ReadOnly Property LookUpButton As ActionButton
            Get
                Return Descendants(content).OfType(Of ActionButton)().FirstOrDefault(Function(button) button.Text.EndsWith("Facts...", StringComparison.Ordinal))
            End Get
        End Property

        Friend ReadOnly Property ShownText As String
            Get
                Return String.Join(Environment.NewLine, Descendants(content).OfType(Of Label)().Select(Function(item) item.Text))
            End Get
        End Property


        Public Sub ShowNothing(message As String)
            Rebuild(
                Sub()
                    AddText(message, UiTheme.SecondaryText(), New Padding(0, 4, 0, 0))
                End Sub)
        End Sub


        ' historyText: the researcher's own history with the journal.
        ' blockedMessage: why Look Up can't run now (Work offline), or "".
        Public Sub ShowJournal(record As JournalRecord, historyText As String, blockedMessage As String)

            If record Is Nothing Then
                ShowNothing(String.Empty)
                Return
            End If

            Rebuild(
                Sub()
                    Dim dpi As Integer = DeviceDpi
                    Dim title As Label = AddText(If(String.IsNullOrWhiteSpace(record.Name), "(Unnamed journal)", record.Name.Trim()), UiTheme.PrimaryText(), New Padding(0, 0, 0, 2))
                    title.Font = New Font(Font.FontFamily, Font.SizeInPoints * 1.3F, FontStyle.Bold)

                    Dim identity As New List(Of String)()
                    If Not String.IsNullOrWhiteSpace(record.Publisher) Then identity.Add(record.Publisher.Trim())
                    If record.Issns IsNot Nothing AndAlso record.Issns.Count > 0 Then identity.Add("ISSN " & String.Join(", ", record.Issns))
                    If identity.Count > 0 Then AddText(String.Join(" · ", identity), UiTheme.SecondaryText(), New Padding(0, 0, 0, 8))

                    Dim hasSourceFacts As Boolean = record.Facts.Any(Function(item) Not item.EnteredByYou)
                    Dim isExample As Boolean = record.Facts.Any(Function(item) item.Source = JournalFactCatalog.ExampleSource)

                    Dim actions As New FlowLayoutPanel With {.AutoSize = True, .WrapContents = True, .Margin = New Padding(0, 2, 0, 6), .BackColor = BackColor}
                    Dim lookUp As New ActionButton With {
                        .Text = If(hasSourceFacts AndAlso Not isExample, "Refresh Facts...", "Look Up Facts..."),
                        .Role = ActionButtonRole.Primary,
                        .AutoSize = True,
                        .Enabled = blockedMessage.Length = 0,
                        .Margin = New Padding(0, 0, UiTheme.Px(8, dpi), 0)
                    }
                    FitButton(lookUp)
                    AddHandler lookUp.Click, Sub(sender, e) RaiseEvent LookUpRequested(Me, EventArgs.Empty)
                    toolTip.SetToolTip(lookUp, "Look up this journal in DOAJ and OpenAlex, two open indexes. You see what was found before anything is saved.")
                    Dim edit As New ActionButton With {.Text = "Edit...", .Role = ActionButtonRole.Secondary, .AutoSize = True, .Margin = New Padding(0)}
                    FitButton(edit)
                    AddHandler edit.Click, Sub(sender, e) RaiseEvent EditRequested(Me, EventArgs.Empty)
                    actions.Controls.Add(lookUp)
                    actions.Controls.Add(edit)
                    Add(actions)

                    If blockedMessage.Length > 0 Then AddText(blockedMessage, UiTheme.InfoColor(), New Padding(0, 0, 0, 6))
                    If Not String.IsNullOrWhiteSpace(historyText) Then AddText(historyText, UiTheme.SecondaryText(), New Padding(0, 0, 0, 4))

                    If Not hasSourceFacts Then
                        AddText("Nothing looked up yet. PaperRoute can look up this journal in DOAJ and OpenAlex, two open indexes, by its ISSN or name. You see what was found before anything is saved, and it only fills empty fields.",
                                UiTheme.SecondaryText(), New Padding(0, 6, 0, 0))
                    End If

                    ' Publishing facts.
                    Dim publishing As List(Of JournalFact) = JournalFactCatalog.Definitions.
                        Where(Function(item) item.Group = JournalFactGroup.Publishing AndAlso item.Key <> JournalFactCatalog.Sharing).
                        Select(Function(item) JournalFactsService.BestFact(record, item.Key)).
                        Where(Function(item) item IsNot Nothing).
                        ToList()
                    If publishing.Count > 0 Then
                        AddHeading("Publishing")
                        Dim grid As TableLayoutPanel = AddGrid()
                        For Each fact As JournalFact In publishing
                            AddFactRow(grid, JournalFactCatalog.LabelOf(fact), JournalFactsService.DisplayValue(fact), fact.Url, fact.Source, String.Empty)
                        Next
                    End If

                    ' Links, opened in the browser.
                    Dim links As New List(Of (Text As String, Url As String)) From {
                        ("Homepage", record.HomepageUrl),
                        ("Aims and scope", record.AimsScopeUrl),
                        ("Author instructions", record.AuthorInstructionsUrl),
                        ("Editorial board", record.EditorialBoardUrl),
                        ("Submission portal", record.SubmissionPortalUrl),
                        ("Sharing policy (Open Policy Finder)", JournalFactCatalog.SharingPolicyUrl(record))
                    }
                    links = links.Where(Function(item) UrlSafetyService.IsSafeHttpUrl(item.Url)).ToList()
                    If links.Count > 0 Then
                        AddHeading("Links")
                        Dim flow As New FlowLayoutPanel With {.AutoSize = True, .WrapContents = True, .Margin = New Padding(0, 0, 0, 4), .BackColor = BackColor, .Dock = DockStyle.Top}
                        For Each link In links
                            flow.Controls.Add(CreateLink(link.Text, link.Url, New Padding(0, 2, UiTheme.Px(16, dpi), 2)))
                        Next
                        Add(flow)
                        wrapping.Add(flow)
                    End If

                    ' Metrics: open ones, then the researcher's, latest year first.
                    Dim open As List(Of JournalFact) = record.Facts.Where(Function(item) Not item.EnteredByYou AndAlso JournalFactCatalog.GroupOf(item) = JournalFactGroup.OpenMetric).ToList()
                    Dim yours As List(Of JournalFact) = record.Facts.Where(Function(item) item.EnteredByYou).
                        GroupBy(Function(item) item.Key & "|" & item.Label).
                        Select(Function(group) group.OrderByDescending(Function(item) item.Year).First()).
                        ToList()
                    AddHeading("Metrics")
                    If open.Count > 0 Then
                        AddText("From open data (OpenAlex, all works it indexes)", UiTheme.MutedText(), New Padding(0, 0, 0, 2))
                        Dim grid As TableLayoutPanel = AddGrid()
                        For Each fact As JournalFact In open.OrderBy(Function(item) JournalFactCatalog.Definitions.ToList().FindIndex(Function(definition) definition.Key = item.Key))
                            AddFactRow(grid, JournalFactCatalog.LabelOf(fact), JournalFactsService.DisplayValue(fact), String.Empty, fact.Source, If(JournalFactCatalog.Find(fact.Key)?.Definition, String.Empty))
                        Next
                    End If
                    If yours.Count > 0 Then
                        AddText("Entered by you", UiTheme.MutedText(), New Padding(0, 4, 0, 2))
                        Dim grid As TableLayoutPanel = AddGrid()
                        For Each fact As JournalFact In yours
                            AddFactRow(grid, JournalFactCatalog.LabelOf(fact), fact.Value, fact.Url,
                                       fact.Source & If(fact.Year.HasValue, ", " & fact.Year.Value.ToString(), String.Empty),
                                       If(JournalFactCatalog.Find(fact.Key)?.Definition, String.Empty))
                        Next
                    End If
                    If open.Count = 0 AndAlso yours.Count = 0 Then
                        AddText("No metrics yet. Look Up Facts... adds OpenAlex's open metrics, and Edit... > Facts and Metrics adds ones you look up yourself, such as CiteScore, with their year.",
                                UiTheme.SecondaryText(), New Padding(0, 0, 0, 2))
                    End If
                    Dim dora As LinkLabel = CreateLink(JournalFactCatalog.DoraNote & " About journal metrics (DORA)", JournalFactCatalog.DoraUrl, New Padding(0, 6, 0, 0))
                    dora.LinkArea = New LinkArea(JournalFactCatalog.DoraNote.Length + 1, "About journal metrics (DORA)".Length)
                    dora.ForeColor = UiTheme.MutedText()
                    Add(dora)
                    wrapping.Add(dora)

                    ' Where the facts came from, and when.
                    Dim sources As String = JournalFactsService.SourcesLine(record)
                    If sources.Length > 0 Then AddText(sources, UiTheme.MutedText(), New Padding(0, 12, 0, 0))
                    If JournalFactsService.IsStale(JournalFactsService.LastChecked(record), DateTime.UtcNow) Then
                        AddText("Checked over a year ago; it may be out of date. Refresh Facts... checks again.", UiTheme.InfoColor(), New Padding(0, 2, 0, 0))
                    End If
                End Sub)

        End Sub


        ' ---------------------------------------------------------------

        Private Sub Rebuild(build As Action)
            SuspendLayout()
            content.SuspendLayout()
            Try
                For Each control As Control In content.Controls.Cast(Of Control)().ToList()
                    control.Dispose()
                Next
                content.Controls.Clear()
                content.RowStyles.Clear()
                content.RowCount = 0
                wrapping.Clear()
                AutoScrollPosition = Point.Empty
                build()
            Finally
                content.ResumeLayout(True)
                ResumeLayout(True)
            End Try
            WrapToWidth()
        End Sub


        ' ActionButton paints its own face, so it is sized to its text.
        Private Sub FitButton(button As ActionButton)
            button.AutoSize = False
            button.Size = New Size(
                TextRenderer.MeasureText(button.Text, Font).Width + UiTheme.Px(32, DeviceDpi),
                Math.Max(TextRenderer.MeasureText(button.Text, Font).Height + UiTheme.Px(14, DeviceDpi), UiTheme.Px(32, DeviceDpi)))
        End Sub


        Private Sub Add(control As Control)
            Dim row As Integer = content.RowCount
            content.RowCount = row + 1
            content.RowStyles.Add(New RowStyle(SizeType.AutoSize))
            content.Controls.Add(control, 0, row)
        End Sub


        Private Function AddText(text As String, color As Color, margin As Padding) As Label
            Dim label As New Label With {.Text = text, .AutoSize = True, .UseMnemonic = False, .ForeColor = color, .Margin = margin, .BackColor = BackColor}
            Add(label)
            wrapping.Add(label)
            Return label
        End Function


        Private Sub AddHeading(text As String)
            Dim heading As Label = AddText(text, UiTheme.PrimaryText(), New Padding(0, UiTheme.Px(14, DeviceDpi), 0, 4))
            heading.Font = New Font(Font, FontStyle.Bold)
        End Sub


        ' Label | value | source, the value wrapping in its own column.
        Private Function AddGrid() As TableLayoutPanel
            Dim grid As New TableLayoutPanel With {.AutoSize = True, .AutoSizeMode = AutoSizeMode.GrowAndShrink, .ColumnCount = 3, .Dock = DockStyle.Top, .Margin = New Padding(0), .BackColor = BackColor}
            grid.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, UiTheme.Px(170, DeviceDpi)))
            grid.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
            grid.ColumnStyles.Add(New ColumnStyle(SizeType.AutoSize))
            Add(grid)
            Return grid
        End Function


        Private Sub AddFactRow(grid As TableLayoutPanel, label As String, value As String, url As String, source As String, definition As String)

            Dim row As Integer = grid.RowCount
            grid.RowCount = row + 1
            grid.RowStyles.Add(New RowStyle(SizeType.AutoSize))

            Dim name As New Label With {.Text = label, .AutoSize = True, .UseMnemonic = False, .ForeColor = UiTheme.SecondaryText(), .Margin = New Padding(0, 3, 8, 3), .BackColor = BackColor}
            If definition.Length > 0 Then toolTip.SetToolTip(name, definition)

            Dim shown As Label
            If UrlSafetyService.IsSafeHttpUrl(url) Then
                shown = CreateLink(value, url, New Padding(0, 3, 8, 3))
            Else
                shown = New Label With {.Text = value, .AutoSize = True, .UseMnemonic = False, .ForeColor = UiTheme.PrimaryText(), .Margin = New Padding(0, 3, 8, 3), .BackColor = BackColor}
            End If
            If definition.Length > 0 Then toolTip.SetToolTip(shown, definition)
            shown.Tag = "value"

            Dim tag As New Label With {.Text = source, .AutoSize = True, .UseMnemonic = False, .ForeColor = UiTheme.MutedText(), .Margin = New Padding(0, 3, 0, 3), .BackColor = BackColor}

            grid.Controls.Add(name, 0, row)
            grid.Controls.Add(shown, 1, row)
            grid.Controls.Add(tag, 2, row)

        End Sub


        Private Function CreateLink(text As String, url As String, margin As Padding) As LinkLabel
            Dim link As New LinkLabel With {
                .Text = text, .AutoSize = True, .UseMnemonic = False, .Margin = margin, .BackColor = BackColor,
                .LinkBehavior = LinkBehavior.HoverUnderline,
                .LinkColor = UiTheme.AccentColor(), .ActiveLinkColor = UiTheme.AccentSecondaryColor(), .VisitedLinkColor = UiTheme.AccentColor(),
                .Tag = url
            }
            toolTip.SetToolTip(link, url)
            AddHandler link.LinkClicked,
                Sub(sender, e)
                    Try
                        UrlSafetyService.OpenInBrowser(url)
                    Catch ex As Exception
                        MessageBox.Show(FindForm(), ex.Message, "Open Link", MessageBoxButtons.OK, MessageBoxIcon.Information)
                    End Try
                End Sub
            Return link
        End Function


        Protected Overrides Sub OnResize(e As EventArgs)
            MyBase.OnResize(e)
            WrapToWidth()
        End Sub


        ' Text wraps to the card's width at every size and scale.
        Private Sub WrapToWidth()
            Dim width As Integer = Math.Max(UiTheme.Px(160, DeviceDpi), ClientSize.Width - Padding.Horizontal - SystemInformation.VerticalScrollBarWidth)
            For Each item As Control In wrapping
                item.MaximumSize = New Size(Math.Max(UiTheme.Px(120, DeviceDpi), width - item.Margin.Horizontal), 0)
            Next
            Dim valueWidth As Integer = Math.Max(UiTheme.Px(120, DeviceDpi), width - UiTheme.Px(170, DeviceDpi) - UiTheme.Px(110, DeviceDpi))
            For Each value As Label In Descendants(content).OfType(Of Label)().Where(Function(item) TryCast(item.Tag, String) = "value")
                value.MaximumSize = New Size(valueWidth, 0)
            Next
        End Sub


        Protected Overrides Sub OnPaint(e As PaintEventArgs)
            MyBase.OnPaint(e)
            Using pen As New Pen(UiTheme.CardBorder())
                e.Graphics.DrawRectangle(pen, 0, 0, ClientSize.Width - 1, ClientSize.Height - 1)
            End Using
        End Sub


        Private Shared Iterator Function Descendants(parent As Control) As IEnumerable(Of Control)
            For Each child As Control In parent.Controls
                Yield child
                For Each descendant As Control In Descendants(child)
                    Yield descendant
                Next
            Next
        End Function


        Protected Overrides Sub Dispose(disposing As Boolean)
            If disposing Then toolTip.Dispose()
            MyBase.Dispose(disposing)
        End Sub

    End Class

End Namespace
