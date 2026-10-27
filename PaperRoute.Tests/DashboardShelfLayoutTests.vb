Imports System.Drawing
Imports System.Linq
Imports System.Reflection
Imports System.Runtime.ExceptionServices
Imports System.Threading
Imports System.Windows.Forms
Imports ManuscriptPipeline
Imports ManuscriptPipeline.Controls
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services
Imports Microsoft.VisualStudio.TestTools.UnitTesting

<TestClass>
<DoNotParallelize>
Public Class DashboardShelfLayoutTests

    <TestMethod>
    <DataRow(1.0F)>
    <DataRow(1.25F)>
    <DataRow(1.5F)>
    Public Sub MinimumWindowKeepsEveryShelfAndTabUsable(fontScale As Single)
        RunOnStaThread(
            Sub()
                Using board As New LayoutOnlyBoard()
                    board.Prepare(fontScale)
                    board.Show()
                    board.Size = board.MinimumSize
                    Application.DoEvents()
                    For Each shelf As FlowLayoutPanel In board.EachShelf()
                        For Each tab As RadioButton In board.ShelfTabs
                            AssertInsideAncestors(tab, "Shelf tab " & tab.Text)
                        Next
                        Assert.AreEqual(1, board.Shelves.Count(Function(item) item.Visible), "Exactly one shelf is shown at a time.")
                        Assert.IsTrue(shelf.Visible, "The selected tab shows its shelf.")
                        AssertInsideAncestors(shelf, "Shelf viewport")
                        Assert.IsTrue(shelf.ClientSize.Height >= 2 * SystemInformation.VerticalScrollBarArrowHeight + 8,
                            "Each shelf needs room for usable vertical scroll controls.")
                        Dim lastCard As Control = shelf.Controls(shelf.Controls.Count - 1)
                        For Each button As Button In Descendants(lastCard).OfType(Of Button)()
                            shelf.ScrollControlIntoView(button)
                            Application.DoEvents()
                            AssertInsideAncestors(button, "Scrolled card action " & button.Text)
                        Next
                    Next
                    board.Close()
                End Using
            End Sub)
    End Sub

    Private Shared Sub AssertInsideAncestors(control As Control, context As String)
        Dim rectangle As Rectangle = control.RectangleToScreen(control.ClientRectangle)
        Dim ancestor As Control = control.Parent
        While ancestor IsNot Nothing
            Dim viewport As Rectangle = ancestor.RectangleToScreen(ancestor.ClientRectangle)
            Assert.IsTrue(viewport.Contains(rectangle),
                $"{context}: {rectangle} exceeds {ancestor.GetType().Name} viewport {viewport}.")
            ancestor = ancestor.Parent
        End While
    End Sub

    <TestMethod>
    Public Sub ShelvesFitAfterMaximizingAndRestoringWindow()
        RunOnStaThread(
            Sub()
                Using board As New LayoutOnlyBoard()
                    board.Prepare(1.0F)
                    board.Opacity = 0
                    board.Show()
                    board.Size = board.MinimumSize
                    Application.DoEvents()
                    For cycle As Integer = 1 To 3
                        board.WindowState = FormWindowState.Maximized
                        Application.DoEvents()
                        board.WindowState = FormWindowState.Normal
                        Application.DoEvents()
                        For Each shelf As FlowLayoutPanel In board.EachShelf()
                            Assert.IsFalse(shelf.HorizontalScroll.Visible,
                                $"Restore cycle {cycle}: shelf {shelf.ClientSize}, display {shelf.DisplayRectangle}, first card {shelf.Controls(0).Bounds}.")
                        Next
                    Next
                    board.Close()
                End Using
            End Sub)
    End Sub

    <TestMethod>
    <DataRow(1.0F)>
    <DataRow(1.25F)>
    <DataRow(1.5F)>
    Public Sub ShelvesDoNotRetainHorizontalRangeThroughResizeCycles(fontScale As Single)
        ' Larger text exercises layout pressure. These are not native Windows DPI tests.
        RunOnStaThread(
            Sub()
                Using board As New LayoutOnlyBoard()
                    board.Prepare(fontScale)
                    board.Show()
                    For Each width As Integer In {900, 2200, 900, 1400, 900, 1180, 900}
                        board.Size = New Size(width, 900)
                        Application.DoEvents()
                        For Each shelf As FlowLayoutPanel In board.EachShelf()
                            Dim diagnostic = $"font scale {fontScale}, window width {width}, shelf {shelf.ClientSize}, display {shelf.DisplayRectangle}, scroll {shelf.AutoScrollPosition}"
                            Assert.IsFalse(shelf.HorizontalScroll.Visible, diagnostic)
                            Assert.IsTrue(shelf.VerticalScroll.Visible, "Populated shelves must scroll vertically: " & diagnostic)
                            Assert.AreEqual(0, shelf.AutoScrollPosition.X, diagnostic)
                            Assert.IsTrue(shelf.DisplayRectangle.Width <= shelf.ClientSize.Width, diagnostic)
                            For Each card As Control In shelf.Controls
                                Assert.IsTrue(card.Right + card.Margin.Right <= shelf.ClientSize.Width - shelf.Padding.Right,
                                    "Every card must fit the visible shelf width: " & diagnostic)
                                For Each button As Button In Descendants(card).OfType(Of Button)()
                                    Assert.IsTrue(button.Parent.ClientRectangle.Contains(button.Bounds),
                                        "Card action must fit without horizontal scrolling: " & button.Text & ", " & diagnostic)
                                    Assert.IsTrue(button.TabStop AndAlso button.Enabled, "Card actions remain keyboard eligible.")
                                Next
                                Dim route = card.Controls.OfType(Of Label)().Single(Function(label) label.Text.StartsWith("View route"))
                                Assert.IsTrue(card.ClientRectangle.Contains(route.Bounds), "View route must fit inside its card.")
                            Next
                            shelf.ScrollControlIntoView(shelf.Controls(shelf.Controls.Count - 1))
                            Application.DoEvents()
                            Assert.IsTrue(shelf.AutoScrollPosition.Y < 0, "Last card must remain reachable by vertical scrolling.")
                            Assert.AreEqual(0, shelf.AutoScrollPosition.X, diagnostic)
                        Next
                    Next
                    board.Close()
                End Using
            End Sub)
    End Sub

    <TestMethod>
    Public Sub ShelfTabsShowCountsAndOneShelfAtATime()
        RunOnStaThread(
            Sub()
                Using board As New LayoutOnlyBoard()
                    board.Prepare(1.0F)
                    board.Show()
                    Application.DoEvents()

                    CollectionAssert.AreEqual(
                        {"Pipeline (30)", "Published (30)", "File Drawer (30)"},
                        board.ShelfTabs.Select(Function(tab) tab.Text).ToList())
                    Assert.IsTrue(board.ShelfTabs.First().Checked, "The board opens on the Pipeline shelf.")

                    Dim tabList As List(Of RadioButton) = board.ShelfTabs.ToList()
                    Dim shelfList As List(Of FlowLayoutPanel) = board.Shelves.ToList()
                    For index As Integer = 0 To tabList.Count - 1
                        tabList(index).Checked = True
                        Application.DoEvents()
                        For other As Integer = 0 To shelfList.Count - 1
                            Assert.AreEqual(other = index, shelfList(other).Visible, $"Tab {tabList(index).Text}, shelf {other}.")
                            Assert.AreEqual(other = index, tabList(other).Checked, $"Only the selected tab is checked: {tabList(other).Text}.")
                        Next
                    Next

                    board.Close()
                End Using
            End Sub)
    End Sub

    <TestMethod>
    Public Sub CardsOpenOnClickAndKeepOtherActionsInAMenu()
        RunOnStaThread(
            Sub()
                Using board As New LayoutOnlyBoard()
                    board.Prepare(1.0F)
                    board.SetManuscripts(New List(Of Manuscript) From {
                        New Manuscript With {.Title = "Pipeline card", .Location = ManuscriptLocation.Pipeline, .CurrentStage = PaperStage.Draft, .TargetJournal = "Memory & Cognition"},
                        New Manuscript With {.Title = "Published card", .Location = ManuscriptLocation.Published, .CurrentStage = PaperStage.Published, .TargetJournal = "Psychology & Aging"},
                        New Manuscript With {.Title = "Filed card", .Location = ManuscriptLocation.FileDrawer, .CurrentStage = PaperStage.Draft, .TargetJournal = "Cognition & Emotion"}
                    })
                    board.Show()
                    Application.DoEvents()

                    Dim expectedMenus As String()() = {
                        New String() {"Open", "View Route", "", "Move to File Drawer...", "", "Delete..."},
                        New String() {"Open", "View Route", "", "Delete..."},
                        New String() {"Open", "View Route", "", "Restore to Pipeline", "", "Delete..."}
                    }
                    Dim journals As String() = {"Memory & Cognition", "Psychology & Aging", "Cognition & Emotion"}
                    Dim shelfIndex As Integer = 0

                    For Each shelf As FlowLayoutPanel In board.EachShelf()
                        Dim card As Control = shelf.Controls(0)

                        Dim buttons As List(Of Button) = Descendants(card).OfType(Of Button)().ToList()
                        Assert.AreEqual(1, buttons.Count, "Only the more-actions button remains on the card.")
                        StringAssert.StartsWith(buttons(0).AccessibleName, "More actions for ")
                        Assert.IsTrue(buttons(0).TabStop AndAlso buttons(0).Enabled)

                        CollectionAssert.AreEqual(
                            expectedMenus(shelfIndex),
                            card.ContextMenuStrip.Items.Cast(Of ToolStripItem)().Select(Function(item) item.Text).ToList())
                        Assert.IsTrue(card.Controls.Cast(Of Control)().All(Function(child) child.ContextMenuStrip Is card.ContextMenuStrip),
                            "Right-clicking anywhere on the card shows the same menu.")

                        Dim title As LinkLabel = card.Controls.OfType(Of LinkLabel)().First()
                        Assert.IsTrue(title.TabStop, "The title is the card's keyboard-focusable way to open it.")
                        Assert.IsFalse(title.UseMnemonic)

                        Dim journal As Label = card.Controls.OfType(Of Label)().Single(Function(label) label.Text = journals(shelfIndex))
                        Assert.IsFalse(journal.UseMnemonic, "Journal names keep their ampersand.")

                        Dim hasStageClock As Boolean = card.Controls.OfType(Of Label)().Any(Function(label) label.Text.StartsWith("added today"))
                        Assert.AreEqual(shelfIndex = 0, hasStageClock, "Only active Pipeline cards show time in stage.")

                        shelfIndex += 1
                    Next

                    board.Close()
                End Using
            End Sub)
    End Sub

    <TestMethod>
    Public Sub NeedsAttentionListsOnlyItemsThatNeedAttention()
        RunOnStaThread(
            Sub()
                Using board As New LayoutOnlyBoard()
                    board.Prepare(1.0F)
                    board.Show()
                    Application.DoEvents()

                    Assert.IsTrue(board.PrivateLabel("lblAttentionClear").Visible, "An all-clear board says so once.")
                    Assert.IsFalse(board.AttentionItems.Any(Function(item) item.Visible), "Zero counts are not listed.")

                    board.SetSamples(2, withTargetJournal:=False)
                    Application.DoEvents()

                    Assert.IsFalse(board.PrivateLabel("lblAttentionClear").Visible)
                    Dim missingJournal As Label = board.PrivateLabel("lblMissingJournal")
                    Assert.IsTrue(missingJournal.Visible)
                    StringAssert.StartsWith(missingJournal.Text, "2 ")
                    Assert.AreEqual(1, board.AttentionItems.Count(Function(item) item.Visible), "Only the non-zero item is listed.")

                    board.Close()
                End Using
            End Sub)
    End Sub

    <TestMethod>
    Public Sub CardsFillAlignedColumnsThatFitTheShelf()
        RunOnStaThread(
            Sub()
                Using board As New LayoutOnlyBoard()
                    board.Prepare(1.0F)
                    board.Show()
                    Dim previousColumns As Integer = 0
                    ' Windows limits a window to the screen, so the expected column
                    ' count follows the shelf width actually reached.
                    For Each width As Integer In {900, 1400, 2200}
                        board.Size = New Size(width, 900)
                        Application.DoEvents()
                        Dim shelf As ManuscriptShelfPanel = DirectCast(board.Shelves.First(), ManuscriptShelfPanel)
                        Dim cards As List(Of Control) = shelf.Controls.Cast(Of Control)().ToList()
                        Dim columns As Integer = cards.Select(Function(card) card.Left).Distinct().Count()
                        Dim available As Integer = shelf.ClientSize.Width - shelf.Padding.Horizontal
                        Dim fits As Integer = Math.Max(1, (available + shelf.CardGap) \ (shelf.MinimumCardWidth + shelf.CardGap))
                        Dim diagnostic As String = $"window width {board.Width}, shelf {shelf.ClientSize}, columns {columns}"

                        Assert.AreEqual(Math.Min(fits, cards.Count), columns, "The shelf shows as many columns as fit: " & diagnostic)
                        Assert.IsTrue(columns >= previousColumns, "Wider windows never show fewer columns: " & diagnostic)
                        previousColumns = columns
                        Assert.AreEqual(1, cards.Select(Function(card) card.Width).Distinct().Count(), "Columns share one width: " & diagnostic)
                        For Each row In cards.GroupBy(Function(card) card.Top)
                            Assert.IsTrue(row.Count() <= columns, "Rows never exceed the column count: " & diagnostic)
                        Next
                        Assert.IsTrue(cards.All(Function(card) card.Right <= shelf.ClientSize.Width - shelf.Padding.Right),
                            "Every column fits the visible width: " & diagnostic)
                    Next
                    board.Close()
                End Using
            End Sub)
    End Sub

    <TestMethod>
    Public Sub AttentionChipsTakeFocusAndFilterFromTheKeyboard()
        RunOnStaThread(
            Sub()
                Using board As New LayoutOnlyBoard()
                    board.Prepare(1.0F)
                    board.SetManuscripts(New List(Of Manuscript) From {
                        New Manuscript With {.Title = "Has a journal", .Location = ManuscriptLocation.Pipeline, .CurrentStage = PaperStage.Draft, .TargetJournal = "Memory & Cognition"},
                        New Manuscript With {.Title = "Needs a journal", .Location = ManuscriptLocation.Pipeline, .CurrentStage = PaperStage.Draft}
                    })
                    board.Show()
                    Application.DoEvents()

                    Dim chip As Label = board.PrivateLabel("lblMissingJournal")
                    Assert.IsTrue(chip.TabStop AndAlso chip.CanSelect, "Attention chips are keyboard stops.")
                    Assert.AreEqual(AccessibleRole.PushButton, chip.AccessibleRole)

                    board.PressKey(chip, Keys.Enter)
                    Application.DoEvents()
                    Assert.AreEqual("Active", ChipTone(chip), "Enter applies the chip's filter.")
                    Dim pipeline As FlowLayoutPanel = board.Shelves.First()
                    Assert.AreEqual(1, pipeline.Controls.Count)
                    Assert.AreEqual("Needs a journal", pipeline.Controls(0).AccessibleName)

                    board.PressKey(chip, Keys.Space)
                    Application.DoEvents()
                    Assert.AreNotEqual("Active", ChipTone(chip), "Space clears it again.")
                    Assert.AreEqual(2, pipeline.Controls.Count)

                    board.Close()
                End Using
            End Sub)
    End Sub

    <TestMethod>
    Public Sub CardsSummarizeTheRouteInWords()
        RunOnStaThread(
            Sub()
                Using board As New LayoutOnlyBoard()
                    board.Prepare(1.0F)
                    Dim rejected As New JournalSubmission With {.JournalName = "First", .SubmittedDate = New DateTime(2026, 1, 5)}
                    rejected.Decisions.Add(New EditorialDecisionEvent With {.Decision = EditorialDecision.DeskRejected, .DecisionDate = New DateTime(2026, 1, 20)})
                    Dim current As New JournalSubmission With {.JournalName = "Second", .SubmittedDate = New DateTime(2026, 3, 1)}
                    current.Decisions.Add(New EditorialDecisionEvent With {.Decision = EditorialDecision.MajorRevision, .DecisionDate = New DateTime(2026, 4, 2)})
                    Dim manuscript As New Manuscript With {.Title = "Second journal", .Location = ManuscriptLocation.Pipeline, .CurrentStage = PaperStage.Revision, .TargetJournal = "Second"}
                    manuscript.Submissions.Add(current)
                    manuscript.Submissions.Add(rejected)
                    board.SetManuscripts(New List(Of Manuscript) From {manuscript})
                    board.Show()
                    Application.DoEvents()

                    Dim card As Control = board.Shelves.First().Controls(0)
                    Assert.IsTrue(card.Controls.OfType(Of Label)().Any(Function(label) label.Text = "2nd journal " & ChrW(&HB7) & " major revision"),
                        "The footer says where the manuscript is on its route.")
                    Assert.IsFalse(card.Controls.OfType(Of Label)().Any(Function(label) label.Text.Contains("submissions")),
                        "The old count line is gone.")

                    board.Close()
                End Using
            End Sub)
    End Sub

    <TestMethod>
    Public Sub PagesOpenFromTheRailAndKeyboardWithBackAndForward()
        RunOnStaThread(
            Sub()
                Using board As New LayoutOnlyBoard()
                    board.Prepare(1.0F)
                    board.Show()
                    Application.DoEvents()

                    CollectionAssert.AreEqual(
                        {"Board", "Library", "Journals", "Deadlines", "Insights", "Import & Export"},
                        board.RailPages.Select(Function(page) page.Text).ToList())
                    Assert.AreEqual("Board", board.ActivePageName)
                    Assert.IsFalse(Descendants(board).OfType(Of Button)().Any(Function(button) button.Text.StartsWith("Data")),
                        "The Data menu's commands moved to pages.")

                    board.PressCommandKey(Keys.Control Or Keys.D2)
                    Application.DoEvents()
                    Assert.AreEqual("Library", board.ActivePageName)
                    Assert.IsTrue(board.RailPages.Single(Function(page) page.Text = "Library").Checked, "The rail shows the current page.")
                    Assert.IsFalse(board.Shelves.First().Visible, "The board is hidden behind other pages.")
                    Dim grid As DataGridView = Descendants(board).OfType(Of DataGridView)().Single()
                    Assert.AreEqual(90, grid.Rows.Count, "The Library lists every manuscript on every shelf.")

                    board.PressCommandKey(Keys.Control Or Keys.D6)
                    Application.DoEvents()
                    Assert.AreEqual("ImportExport", board.ActivePageName)

                    board.PressCommandKey(Keys.Alt Or Keys.Left)
                    Application.DoEvents()
                    Assert.AreEqual("Library", board.ActivePageName, "Alt+Left goes back.")
                    board.PressCommandKey(Keys.Alt Or Keys.Left)
                    Application.DoEvents()
                    Assert.AreEqual("Board", board.ActivePageName)
                    Assert.IsTrue(board.Shelves.First().Visible)
                    board.PressCommandKey(Keys.Alt Or Keys.Right)
                    Application.DoEvents()
                    Assert.AreEqual("Library", board.ActivePageName, "Alt+Right goes forward.")

                    board.Close()
                End Using
            End Sub)
    End Sub

    <TestMethod>
    Public Sub EveryDataCommandHasAPlaceAndBackupStaysWithinTwoClicks()
        RunOnStaThread(
            Sub()
                Using board As New LayoutOnlyBoard()
                    board.Prepare(1.0F)
                    board.Show()
                    Application.DoEvents()

                    board.PressCommandKey(Keys.Control Or Keys.D6)
                    Application.DoEvents()
                    Dim commands As List(Of String) = Descendants(board).OfType(Of Button)().Select(Function(button) button.Text).ToList()
                    For Each command As String In {
                        "Import Spreadsheet...", "Import BibTeX / RIS...", "Get Import Template...",
                        "Export Library to Excel...", "Export Library as BibTeX...", "Export Library as RIS...",
                        "Publication & CV Export...", "Backup Library...", "Restore Backup..."}
                        CollectionAssert.Contains(commands, command, "Import & Export lists " & command)
                    Next

                    ' Settings in the rail is the first click; each item is the second.
                    Using menu As ContextMenuStrip = board.SettingsMenu()
                        CollectionAssert.AreEqual(
                            {"Preferences...", "Work Offline", "Check for Updates...", "Diagnostics...", "", "Backup Library...", "Restore Backup...", "", "About PaperRoute"},
                            menu.Items.Cast(Of ToolStripItem)().Select(Function(item) item.Text).ToList())
                    End Using
                    Assert.IsTrue(Descendants(board).OfType(Of Button)().Any(Function(button) button.Text = "Settings"))
                    Assert.IsTrue(Descendants(board).OfType(Of Button)().Any(Function(button) button.Text = "Help"))

                    board.Close()
                End Using
            End Sub)
    End Sub

    Private Shared Function ChipTone(chip As Label) As String
        Return chip.GetType().GetProperty("Tone").GetValue(chip).ToString()
    End Function

    <TestMethod>
    Public Sub ShelfRangesRecalculateWhenCardsAreReplacedWithEmptyRows()
        RunOnStaThread(
            Sub()
                Using board As New LayoutOnlyBoard()
                    board.Prepare(1.0F)
                    board.Show()
                    For Each sampleCount As Integer In {0, 5, 1, 0}
                        board.SetSamples(sampleCount)
                        For Each width As Integer In {900, 1400, 900}
                            board.Size = New Size(width, 900)
                            Application.DoEvents()
                            For Each shelf As FlowLayoutPanel In board.EachShelf()
                                Assert.IsFalse(shelf.HorizontalScroll.Visible,
                                    $"Replacing rows must clear stale horizontal extent: {sampleCount} cards, width {width}, display {shelf.DisplayRectangle}.")
                                Assert.AreEqual(0, shelf.AutoScrollPosition.X)
                                If sampleCount = 0 Then
                                    Assert.IsFalse(shelf.VerticalScroll.Visible,
                                        $"The empty shelf must clear obsolete vertical range: width {width}, shelf {shelf.ClientSize}, display {shelf.DisplayRectangle}, row {shelf.Controls(0).Bounds}.")
                                End If
                            Next
                        Next
                    Next
                    board.Close()
                End Using
            End Sub)
    End Sub

    ' The shelf bar's hairline runs under both of its groups (#141), so the
    ' tabs and the filters sit on one line, whether they share a line or the
    ' filters wrap above the tabs. Before the change the groups covered the
    ' line the bar drew along its own bottom edge, and only the piece between
    ' them showed, reading as a fourth tab's underline.
    <TestMethod>
    Public Sub TheSplitBarBaselineRunsUnderBothGroupsInBothLayouts()
        RunOnStaThread(
            Sub()
                Using host As New Form With {.StartPosition = FormStartPosition.Manual, .Location = New Point(-20000, -20000), .ShowInTaskbar = False, .BackColor = UiTheme.BoardBackground()}
                    ' Groups as the Board's: tabs that carry the line under
                    ' themselves, and filters that stop short of the bottom.
                    Dim tabs As New FlowLayoutPanel With {.AutoSize = True, .AutoSizeMode = AutoSizeMode.GrowAndShrink, .WrapContents = False, .Margin = New Padding(0), .Padding = New Padding(0), .BackColor = UiTheme.BoardBackground()}
                    For Each name As String In {"Pipeline (3)", "Published (1)", "File Drawer (2)"}
                        tabs.Controls.Add(New ShelfTabButton With {.Text = name})
                    Next
                    Dim tools As New FlowLayoutPanel With {.AutoSize = True, .AutoSizeMode = AutoSizeMode.GrowAndShrink, .WrapContents = False, .Margin = New Padding(0), .Padding = New Padding(0, 0, 0, 6), .BackColor = UiTheme.BoardBackground()}
                    tools.Controls.Add(New Label With {.Text = "Sort: Current order", .AutoSize = True, .Margin = New Padding(8, 5, 0, 0), .BackColor = UiTheme.CardBackground()})
                    Dim bar As New SplitBar(tabs, tools) With {.WrapRightAbove = True, .ShowBaseline = True, .Dock = DockStyle.Top, .BackColor = UiTheme.BoardBackground()}
                    host.Controls.Add(bar)
                    host.Show()

                    For Each arrangement In {(Name:="side by side", Width:=900), (Name:="wrapped", Width:=300)}
                        host.ClientSize = New Size(arrangement.Width, 200)
                        Application.DoEvents()
                        Assert.AreEqual(arrangement.Name = "wrapped", tools.Bottom <= tabs.Top, "The bar is " & arrangement.Name & ".")
                        AssertBottomRow(bar, Function(x) UiTheme.CardBorder(), arrangement.Name)
                    Next

                    host.Close()
                End Using
            End Sub)
    End Sub

    ' On the Board the line runs the bar's full width, under the shelf tabs
    ' and the stage filter and sort, and the selected tab sits on it with
    ' its accent underline (#141).
    <TestMethod>
    Public Sub TheShelfSplitBarIsOneLineUnderTabsAndFilters()
        RunOnStaThread(
            Sub()
                Using board As New LayoutOnlyBoard()
                    board.Prepare(1.0F)
                    board.Show()
                    Application.DoEvents()
                    Dim bar As SplitBar = Descendants(board).OfType(Of SplitBar)().Single(Function(item) item.ShowBaseline)
                    Dim selected As RadioButton = board.ShelfTabs.Single(Function(tab) tab.Checked)
                    Dim under As Rectangle = bar.RectangleToClient(selected.RectangleToScreen(selected.ClientRectangle))

                    AssertBottomRow(bar, Function(x) If(x >= under.Left AndAlso x < under.Right, UiTheme.AccentColor(), UiTheme.CardBorder()), "the Board's shelf bar")

                    board.Close()
                End Using
            End Sub)
    End Sub

    ' Every pixel of the bar's bottom row, as drawn, against what each
    ' column should be.
    Private Shared Sub AssertBottomRow(bar As Control, expected As Func(Of Integer, Color), context As String)
        Using image As New Bitmap(bar.Width, bar.Height)
            bar.DrawToBitmap(image, New Rectangle(0, 0, bar.Width, bar.Height))
            Dim wrong As List(Of Integer) = Enumerable.Range(0, image.Width).
                Where(Function(x) image.GetPixel(x, image.Height - 1).ToArgb() <> expected(x).ToArgb()).ToList()
            Assert.AreEqual(0, wrong.Count,
                $"{context}: {wrong.Count} of {image.Width} pixels of the bottom row are not the line, from x = {If(wrong.Count = 0, 0, wrong.First())} to {If(wrong.Count = 0, 0, wrong.Last())}.")
        End Using
    End Sub

    ' A card's tags are a chip tall, centred on the journal line (#141), on
    ' a card built for the record, kept for it, or rebuilt when its tags
    ' change (#133). Before the change the strip was the journal line's
    ' height, two pixels less than a chip, which was shaved top and bottom.
    <TestMethod>
    Public Sub TagStripsAreAtLeastAChipTallOnTheJournalLine()
        RunOnStaThread(
            Sub()
                Using board As New LayoutOnlyBoard()
                    board.Prepare(1.0F)
                    Dim tagged As New Manuscript With {.Title = "Tagged manuscript", .Location = ManuscriptLocation.Pipeline, .CurrentStage = PaperStage.Draft, .TargetJournal = "Memory & Cognition"}
                    tagged.Tags.AddRange({"teaching", "grant"})
                    Dim library As New List(Of Manuscript) From {tagged}
                    board.SetManuscripts(library)
                    board.Show()
                    Application.DoEvents()

                    Dim built As Control = board.Shelves.First().Controls(0)
                    AssertTagStrip(built, "Built")

                    board.SetManuscripts(library)
                    Application.DoEvents()
                    Assert.AreSame(built, board.Shelves.First().Controls(0), "The card is kept between renders.")
                    AssertTagStrip(built, "Kept")

                    tagged.Tags.Add("replication")
                    board.SetManuscripts(library)
                    Application.DoEvents()
                    Dim rebuilt As Control = board.Shelves.First().Controls(0)
                    Assert.AreNotSame(built, rebuilt, "New tags rebuild the card.")
                    AssertTagStrip(rebuilt, "Rebuilt")

                    board.Close()
                End Using
            End Sub)
    End Sub

    ' The empty Board's privacy note has as much room below its text as
    ' above (#258, item 14). Its panel was sized to the text as the welcome
    ' was built, before the note had the window's font, and the text then
    ' grew: 14 pixels above it and 2 below at 125%, the descenders nearly
    ' on the edge. With the window's text at 100%, 125% and 150%, the note
    ' is as far from the panel's bottom as from its top, to a pixel.
    <TestMethod>
    Public Sub TheEmptyBoardsPrivacyNoteSitsInTheMiddleOfItsPanel()
        For Each scale As Single In {1.0F, 1.25F, 1.5F}
            Dim fontScale As Single = scale
            RunOnStaThread(
                Sub()
                    Using board As New LayoutOnlyBoard()
                        board.Prepare(fontScale)
                        board.SetManuscripts(New List(Of Manuscript)())
                        board.Show()
                        Application.DoEvents()

                        Dim note As Label = Descendants(board).OfType(Of Label)().Single(Function(label) label.Text.StartsWith("PaperRoute keeps your library on this computer.", StringComparison.Ordinal))
                        Dim panel As Control = note.Parent
                        Assert.IsTrue(note.Visible, "The welcome shows on an empty library.")

                        Dim above As Integer = note.Top
                        Dim below As Integer = panel.ClientSize.Height - note.Bottom
                        Assert.IsTrue(Math.Abs(above - below) <= 1, $"With the text at {fontScale:P0} the note has {above} pixels above it in its panel and {below} below.")

                        board.Close()
                    End Using
                End Sub)
        Next
    End Sub

    ' A card's route line sits where it does whether or not it is cut short
    ' (#258, item 15). Cut short with an ellipsis, "2nd journal · major
    ' rev..." sat 2 pixels above every other card's route line and its own
    ' View route: a label centres its text only when the text fits its box,
    ' and the box was taller than a line. With the window's text at 100%,
    ' 125% and 150%, the line's ink starts on the same row cut short as
    ' whole.
    <TestMethod>
    Public Sub ACardsShortenedRouteLineSitsWhereAWholeOneDoes()
        For Each scale As Single In {1.0F, 1.25F, 1.5F}
            Dim fontScale As Single = scale
            RunOnStaThread(
                Sub()
                    Using board As New LayoutOnlyBoard()
                        board.Prepare(fontScale)
                        board.Show()
                        Application.DoEvents()

                        Dim card As Control = board.Shelves.First().Controls(0)
                        Dim route As Label = card.Controls.OfType(Of Label)().Single(Function(label) Not TypeOf label Is LinkLabel AndAlso label.AutoEllipsis AndAlso label.TextAlign = ContentAlignment.MiddleLeft)
                        ' The app draws text with GDI; a test host starts with GDI+.
                        route.UseCompatibleTextRendering = False

                        route.Text = "2nd journal"
                        Application.DoEvents()
                        Dim whole As Integer = InkTop(route)

                        route.Text = "2nd journal · major revision · two rejections before it, and a long way still to go"
                        Application.DoEvents()
                        Assert.IsTrue(TextRenderer.MeasureText(route.Text, route.Font).Width > route.Width, "The long line is cut short.")
                        Dim shortened As Integer = InkTop(route)

                        Assert.AreEqual(whole, shortened, $"With the text at {fontScale:P0} the route line's ink starts {whole} pixels into its {route.Height}-pixel box whole and {shortened} cut short.")

                        board.Close()
                    End Using
                End Sub)
        Next
    End Sub

    ' The first row of a control's picture with ink on it: a pixel clearly
    ' unlike the commonest color, its background.
    Private Shared Function InkTop(control As Control) As Integer
        Using image As New Bitmap(control.Width, control.Height)
            control.DrawToBitmap(image, New Rectangle(Point.Empty, control.Size))
            Dim counts As New Dictionary(Of Integer, Integer)()
            For y As Integer = 0 To image.Height - 1
                For x As Integer = 0 To image.Width - 1
                    Dim pixel As Integer = image.GetPixel(x, y).ToArgb()
                    Dim count As Integer = 0
                    counts.TryGetValue(pixel, count)
                    counts(pixel) = count + 1
                Next
            Next
            Dim back As Color = Color.FromArgb(counts.OrderByDescending(Function(entry) entry.Value).First().Key)
            For y As Integer = 0 To image.Height - 1
                For x As Integer = 0 To image.Width - 1
                    Dim pixel As Color = image.GetPixel(x, y)
                    If Math.Abs(CInt(pixel.R) - back.R) + Math.Abs(CInt(pixel.G) - back.G) + Math.Abs(CInt(pixel.B) - back.B) > 96 Then
                        Return y
                    End If
                Next
            Next
            Throw New InvalidOperationException("'" & control.Text & "' drew no ink.")
        End Using
    End Function

    Private Shared Sub AssertTagStrip(card As Control, context As String)
        Dim strip As TagStrip = card.Controls.OfType(Of TagStrip)().Single()
        Dim journal As Label = card.Controls.OfType(Of Label)().Single(Function(label) label.Text = "Memory & Cognition")
        Dim chip As Integer = TagChipPainter.Measure("Ag", strip.Font, strip.DeviceDpi).Height
        Assert.IsTrue(strip.Height >= chip, $"{context}: the strip is {strip.Height} px tall, a chip {chip} px.")
        Dim offCentre As Integer = Math.Abs((strip.Top * 2 + strip.Height) - (journal.Top * 2 + journal.Height))
        Assert.IsTrue(offCentre <= 2, $"{context}: the strip ({strip.Top}, {strip.Height} px) is centred on the journal line ({journal.Top}, {journal.Height} px).")
    End Sub

    Private Shared Iterator Function Descendants(parent As Control) As IEnumerable(Of Control)
        For Each child As Control In parent.Controls
            Yield child
            For Each descendant As Control In Descendants(child)
                Yield descendant
            Next
        Next
    End Function

    Private Class LayoutOnlyBoard
        Inherits Form1

        ' Skip the normal Load event, which reads repositories and checks updates.
        ' Repository constructors only resolve paths; this fixture never loads or saves.
        Protected Overrides Sub OnLoad(e As EventArgs)
        End Sub

        Protected Overrides ReadOnly Property ShowWithoutActivation As Boolean
            Get
                Return True
            End Get
        End Property

        Public Sub Prepare(fontScale As Single)
            InvokePrivate("BuildInterface")
            Font = New Font(Font.FontFamily, 10.0F * fontScale)
            ' Enough cards that a full-height shelf still needs to scroll when a
            ' wide window fits six columns.
            SetSamples(30)
            StartPosition = FormStartPosition.Manual
            Location = New Point(-20000, -20000)
            ShowInTaskbar = False
        End Sub

        Public Sub SetSamples(count As Integer, Optional withTargetJournal As Boolean = True)
            Dim samples As New List(Of Manuscript)
            For Each location As ManuscriptLocation In [Enum].GetValues(Of ManuscriptLocation)()
                For index As Integer = 1 To count
                    samples.Add(New Manuscript With {
                        .Title = "Synthetic shelf layout manuscript with a long title " & index,
                        .Location = location,
                        .CurrentStage = If(location = ManuscriptLocation.Published, PaperStage.Published, PaperStage.Draft),
                        .TargetJournal = If(withTargetJournal, "A fictional journal with a long descriptive name", String.Empty)
                    })
                Next
            Next
            SetManuscripts(samples)
        End Sub

        Public Sub SetManuscripts(samples As List(Of Manuscript))
            GetType(Form1).GetField("manuscripts", BindingFlags.Instance Or BindingFlags.NonPublic).SetValue(Me, samples)
            InvokePrivate("RenderManuscripts")
        End Sub

        Public ReadOnly Property Shelves As IEnumerable(Of FlowLayoutPanel)
            Get
                Return {"pipelinePanel", "publishedPanel", "fileDrawerPanel"}.Select(
                    Function(name) DirectCast(GetType(Form1).GetField(name, BindingFlags.Instance Or BindingFlags.NonPublic).GetValue(Me), FlowLayoutPanel))
            End Get
        End Property

        Public ReadOnly Property ShelfTabs As IEnumerable(Of RadioButton)
            Get
                Return {"tabPipeline", "tabPublished", "tabFileDrawer"}.Select(
                    Function(name) DirectCast(GetType(Form1).GetField(name, BindingFlags.Instance Or BindingFlags.NonPublic).GetValue(Me), RadioButton))
            End Get
        End Property

        Public ReadOnly Property RailPages As IEnumerable(Of RadioButton)
            Get
                Dim buttons = DirectCast(GetType(Form1).GetField("railButtons", BindingFlags.Instance Or BindingFlags.NonPublic).GetValue(Me), System.Collections.IDictionary)
                Return buttons.Values.Cast(Of RadioButton)().ToList()
            End Get
        End Property

        Public ReadOnly Property ActivePageName As String
            Get
                Return GetType(Form1).GetField("currentPage", BindingFlags.Instance Or BindingFlags.NonPublic).GetValue(Me).ToString()
            End Get
        End Property

        Public Sub PressCommandKey(keyData As Keys)
            Dim message As New Message()
            GetType(Form1).GetMethod("ProcessCmdKey", BindingFlags.Instance Or BindingFlags.NonPublic).
                Invoke(Me, New Object() {message, keyData})
        End Sub

        Public Function SettingsMenu() As ContextMenuStrip
            Return DirectCast(GetType(Form1).GetMethod("BuildSettingsMenu", BindingFlags.Instance Or BindingFlags.NonPublic).Invoke(Me, Nothing), ContextMenuStrip)
        End Function

        Public Sub PressKey(target As Control, key As Keys)
            target.GetType().GetMethod("OnKeyDown", BindingFlags.Instance Or BindingFlags.NonPublic).
                Invoke(target, New Object() {New KeyEventArgs(key)})
        End Sub

        Public Function PrivateLabel(name As String) As Label
            Return DirectCast(GetType(Form1).GetField(name, BindingFlags.Instance Or BindingFlags.NonPublic).GetValue(Me), Label)
        End Function

        Public ReadOnly Property AttentionItems As IEnumerable(Of Label)
            Get
                Return {"lblOverdueRevisions", "lblRevisionDueSoon", "lblLongReviews", "lblMissingJournal", "lblRecentRejections"}.
                    Select(Function(name) PrivateLabel(name))
            End Get
        End Property

        ' Selects each shelf tab in turn and yields the shelf it shows, then
        ' returns to the Pipeline tab.
        Public Iterator Function EachShelf() As IEnumerable(Of FlowLayoutPanel)
            Dim tabList As List(Of RadioButton) = ShelfTabs.ToList()
            Dim shelfList As List(Of FlowLayoutPanel) = Shelves.ToList()
            For index As Integer = 0 To tabList.Count - 1
                tabList(index).Checked = True
                Application.DoEvents()
                Yield shelfList(index)
            Next
            tabList(0).Checked = True
            Application.DoEvents()
        End Function

        Private Sub InvokePrivate(name As String)
            GetType(Form1).GetMethod(name, BindingFlags.Instance Or BindingFlags.NonPublic).Invoke(Me, Nothing)
        End Sub
    End Class

    Private Shared Sub RunOnStaThread(action As Action)
        StaRunner.Run(action)
    End Sub
End Class
