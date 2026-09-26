Imports System.Drawing
Imports System.Linq
Imports System.Reflection
Imports System.Runtime.ExceptionServices
Imports System.Threading
Imports System.Windows.Forms
Imports ManuscriptPipeline
Imports ManuscriptPipeline.Models
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
                        {"Pipeline (10)", "Published (10)", "File Drawer (10)"},
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
            ' Enough cards that one full-height shelf still needs to scroll.
            SetSamples(10)
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
        Dim failure As Exception = Nothing
        Dim thread As New Thread(
            Sub()
                Try
                    action()
                Catch ex As Exception
                    failure = ex
                End Try
            End Sub) With {.IsBackground = True}
        thread.SetApartmentState(ApartmentState.STA)
        thread.Start()
        Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(30)), "Shelf layout test timed out.")
        If failure IsNot Nothing Then ExceptionDispatchInfo.Capture(failure).Throw()
    End Sub
End Class
