Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.IO
Imports System.Linq
Imports System.Reflection
Imports System.Runtime.InteropServices
Imports System.Windows.Forms
Imports ManuscriptPipeline
Imports ManuscriptPipeline.Controls
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services
Imports Microsoft.VisualStudio.TestTools.UnitTesting

' The Board at startup (#133): the shelf shown shows its first screen of
' cards before the Board is interactive and the rest a card at a time while
' the window idles; the shelves not shown yet are prepared once it has
' filled, and one shown before then is built when it is first shown.
' Once the fill has passed, the Board is the Board it always was: a card
' for every manuscript, with everything a card offers.
<TestClass>
<DoNotParallelize>
Public Class BoardFillTests

    Private _root As String = String.Empty


    <TestInitialize>
    Public Sub Initialize()
        _root = CreateTemporaryRoot()
        StorageEnvironment.RootOverride = Path.Combine(_root, "storage")
    End Sub


    <TestCleanup>
    Public Sub Cleanup()
        StorageEnvironment.RootOverride = Nothing
        DeleteTemporaryRoot(_root)
    End Sub


    ' Every card, with the menu of its shelf on it and each of its parts,
    ' no shelf scrolling sideways and the last card reachable, at three
    ' text sizes.
    <TestMethod>
    <DataRow(1.0F)>
    <DataRow(1.25F)>
    <DataRow(1.5F)>
    Public Sub BoardFillShowsTheFirstScreenThenEveryCard(fontScale As Single)

        StaRunner.Run(
            Sub()
                Dim library As LargeLibraryFixture.SyntheticLibrary = Library120()

                Using board As New LibraryBoard(library, Path.Combine(_root, "settings"), fontScale)

                    board.Start()

                    Dim pipeline As ManuscriptShelfPanel = board.Shelf("pipelinePanel")
                    Dim total As Integer = library.Manuscripts.Where(Function(item) item.Location = ManuscriptLocation.Pipeline).Count()
                    Dim shown As Integer = Cards(pipeline).Where(Function(card) card.Visible).Count()

                    Assert.AreEqual(total, Cards(pipeline).Count, "Every Pipeline manuscript has a card.")
                    Assert.IsTrue(shown > 0 AndAlso shown < total, "The first screen shows " & shown & " of " & total & " cards.")
                    Assert.IsTrue(Cards(pipeline).Where(Function(card) Not card.Visible).All(Function(card) Not card.IsHandleCreated), "A card not shown yet has no window.")
                    Assert.AreEqual(0, board.Shelf("publishedPanel").Controls.Count, "The Published shelf waits until it is shown.")
                    StringAssert.StartsWith(board.ShelfTab("tabPublished").Text, "Published (" & library.Manuscripts.Where(Function(item) item.Location = ManuscriptLocation.Published).Count() & ")", "Its tab still counts its manuscripts.")

                    board.WaitForBoardFill()

                    For Each entry In {("tabPipeline", "pipelinePanel", ManuscriptLocation.Pipeline),
                                       ("tabPublished", "publishedPanel", ManuscriptLocation.Published),
                                       ("tabFileDrawer", "fileDrawerPanel", ManuscriptLocation.FileDrawer)}
                        Dim tab As String = entry.Item1
                        board.ShelfTab(tab).Checked = True
                        Application.DoEvents()
                        board.WaitForBoardFill()

                        Dim shelf As ManuscriptShelfPanel = board.Shelf(entry.Item2)
                        Dim location As ManuscriptLocation = entry.Item3
                        Dim expected As Integer = library.Manuscripts.Where(Function(item) item.Location = location).Count()

                        Assert.AreEqual(expected, Cards(shelf).Count, tab & ": a card for every manuscript.")
                        Assert.IsTrue(Cards(shelf).All(Function(card) card.Visible), tab & ": every card shows.")
                        For Each card As RoundedPanel In Cards(shelf)
                            CollectionAssert.AreEqual(MenuFor(location), card.ContextMenuStrip.Items.Cast(Of ToolStripItem)().Select(Function(item) item.Text).ToList(), tab & ": the card's menu.")
                            Assert.IsTrue(card.Controls.Cast(Of Control)().All(Function(part) part.ContextMenuStrip Is card.ContextMenuStrip), tab & ": each part of a card shows its menu.")
                        Next
                        Assert.IsFalse(shelf.HorizontalScroll.Visible, tab & ": the shelf never scrolls sideways.")
                        Dim last As Control = Cards(shelf).Last()
                        shelf.ScrollControlIntoView(last)
                        Application.DoEvents()
                        Assert.IsTrue(shelf.ClientRectangle.IntersectsWith(shelf.RectangleToClient(last.RectangleToScreen(last.ClientRectangle))), tab & ": the last card scrolls into view.")
                    Next

                    board.Close()
                End Using
            End Sub,
            TimeSpan.FromMinutes(5))

    End Sub


    ' A search typed while the shelf fills keeps a card for exactly what it
    ' finds, each found card showing as the fill reaches it; clearing it
    ' brings every card back as it was, and two records with one Id each
    ' keep a card of their own.
    <TestMethod>
    Public Sub FillKeepsCardsAndFollowsASearch()

        StaRunner.Run(
            Sub()
                Dim library As LargeLibraryFixture.SyntheticLibrary = Library120()

                Using board As New LibraryBoard(library, Path.Combine(_root, "settings"))

                    board.Start()
                    Dim pipeline As ManuscriptShelfPanel = board.Shelf("pipelinePanel")
                    Dim before As List(Of RoundedPanel) = Cards(pipeline)

                    board.SearchBoard("sleep")
                    Dim found As List(Of String) = board.Library.Where(Function(item) item.Location = ManuscriptLocation.Pipeline AndAlso item.Title.Contains("sleep", StringComparison.CurrentCultureIgnoreCase)).Select(Function(item) item.Title).ToList()
                    Assert.IsTrue(found.Count > 0)
                    CollectionAssert.AreEquivalent(found, Cards(pipeline).Select(Function(card) card.AccessibleName).ToList(), "The search keeps a card for what it finds.")
                    board.WaitForBoardFill()
                    Assert.IsTrue(Cards(pipeline).All(Function(card) card.Visible), "Every card found shows once the fill reaches it.")

                    board.SearchBoard(String.Empty)
                    board.WaitForBoardFill()
                    CollectionAssert.AreEqual(before, Cards(pipeline), "Every card is the one built before.")
                    Assert.IsTrue(Cards(pipeline).All(Function(card) card.Visible))

                    Dim twin As Manuscript = ManuscriptCloneService.CloneManuscript(board.Library.First(Function(item) item.Location = ManuscriptLocation.Pipeline))
                    board.Library.Add(twin)
                    board.Render()
                    board.WaitForBoardFill()
                    Assert.AreEqual(2, Cards(pipeline).Where(Function(card) card.AccessibleName = twin.Title).Count(), "Two records with one Id each have a card.")

                    board.Close()
                End Using
            End Sub,
            TimeSpan.FromMinutes(5))

    End Sub


    ' Moving to another shelf tab with the arrow keys while the Pipeline
    ' fills builds that shelf at once, showing its first screen: the cards
    ' that fill its view and one row more, as the Pipeline at startup.
    <TestMethod>
    Public Sub ShelfShownDuringTheFillIsBuiltAtOnce()

        StaRunner.Run(
            Sub()
                Dim library As LargeLibraryFixture.SyntheticLibrary = Library120()

                Using board As New LibraryBoard(library, Path.Combine(_root, "settings"))

                    board.Start()
                    Dim published As ManuscriptShelfPanel = board.Shelf("publishedPanel")
                    Assert.IsTrue(board.IsBoardFillPending)

                    ' Counted as the switch ends, before the window idles.
                    Dim shownAtSwitch As Integer = -1
                    AddHandler board.ShelfTab("tabPublished").CheckedChanged,
                        Sub(sender, e)
                            If board.ShelfTab("tabPublished").Checked Then
                                shownAtSwitch = Cards(published).Where(Function(card) card.Visible AndAlso card.IsHandleCreated).Count()
                            End If
                        End Sub

                    board.ActiveControl = board.ShelfTab("tabPipeline")
                    PostKey(board.ShelfTab("tabPipeline"), Keys.Right)
                    Application.DoEvents()

                    Assert.IsTrue(board.ShelfTab("tabPublished").Checked, "Right moves to the Published tab.")
                    Assert.IsTrue(published.Visible)
                    Dim total As Integer = library.Manuscripts.Where(Function(item) item.Location = ManuscriptLocation.Published).Count()
                    Assert.AreEqual(total, Cards(published).Count, "The shelf is built when shown.")

                    ' Every card has its place in the grid, shown or not.
                    Dim columns As Integer = Cards(published).Select(Function(card) card.Left).Distinct().Count()
                    Dim rows As Integer = CInt(Math.Ceiling(published.ClientSize.Height / CDbl(Cards(published)(0).Height + published.CardGap)))
                    Assert.AreEqual(Math.Min(total, columns * (rows + 1)), shownAtSwitch,
                                    "Its first screen shows at once: " & columns & " columns, " & rows & " rows in view of " & published.ClientSize.ToString() & ".")

                    board.WaitForBoardFill()
                    Assert.IsTrue(Cards(published).All(Function(card) card.Visible))

                    board.Close()
                End Using
            End Sub,
            TimeSpan.FromMinutes(5))

    End Sub


    ' Cards wait while another page covers the Board, so they never all
    ' make their windows at once when it comes back, and go on after.
    <TestMethod>
    Public Sub FillWaitsWhileTheBoardIsHidden()

        StaRunner.Run(
            Sub()
                Dim library As LargeLibraryFixture.SyntheticLibrary = Library120()

                Using board As New LibraryBoard(library, Path.Combine(_root, "settings"))

                    board.Start()
                    Dim pipeline As ManuscriptShelfPanel = board.Shelf("pipelinePanel")

                    board.NavigateTo(Form1.WorkspacePage.Library)
                    Dim hidden As Integer = Cards(pipeline).Where(Function(card) Not card.IsHandleCreated).Count()
                    Pump(TimeSpan.FromSeconds(1))
                    Assert.AreEqual(hidden, Cards(pipeline).Where(Function(card) Not card.IsHandleCreated).Count(), "No card is shown behind another page.")

                    board.NavigateTo(Form1.WorkspacePage.Board)
                    board.WaitForBoardFill()
                    Assert.IsTrue(Cards(pipeline).All(Function(card) card.Visible AndAlso card.IsHandleCreated))

                    board.Close()
                End Using
            End Sub,
            TimeSpan.FromMinutes(5))

    End Sub


    ' Tab from the first card reaches every card in order while the shelf
    ' is still filling: entering a card shows the row after it.
    <TestMethod>
    Public Sub TabReachesEveryCardDuringTheFill()

        StaRunner.Run(
            Sub()
                Dim library As LargeLibraryFixture.SyntheticLibrary = Library120()

                Using board As New LibraryBoard(library, Path.Combine(_root, "settings"))

                    board.Start()
                    Dim pipeline As ManuscriptShelfPanel = board.Shelf("pipelinePanel")
                    Dim all As List(Of RoundedPanel) = Cards(pipeline)
                    Assert.IsTrue(all.Any(Function(card) Not card.Visible), "The shelf is still filling.")

                    Dim reached As New List(Of RoundedPanel)()
                    Dim current As Control = all(0).Controls.OfType(Of LinkLabel)().First()
                    board.ActiveControl = current

                    For steps As Integer = 0 To all.Count * 6
                        Dim card As RoundedPanel = CardOf(current)
                        If card Is Nothing Then Exit For
                        If reached.Count = 0 OrElse reached.Last() IsNot card Then reached.Add(card)
                        board.SelectNextControl(current, True, True, True, False)
                        current = DeepestActiveControl(board)
                    Next

                    CollectionAssert.AreEqual(all, reached, "Tab reaches every card once, in order.")

                    board.Close()
                End Using
            End Sub,
            TimeSpan.FromMinutes(5))

    End Sub


    ' A card the fill shows after the first screen is the card it always
    ' was: while the shelf is still filling, Tab from the card before
    ' reaches its title, and Space on the title opens its manuscript, as
    ' Enter and a click do (#192).
    <TestMethod>
    Public Sub ATitleTheFillShowsOpensItsManuscriptOnSpace()

        StaRunner.Run(
            Sub()
                Dim library As LargeLibraryFixture.SyntheticLibrary = Library120()

                Using board As New LibraryBoard(library, Path.Combine(_root, "settings"))

                    board.Start()
                    Dim all As List(Of RoundedPanel) = Cards(board.Shelf("pipelinePanel"))
                    Dim index As Integer = all.FindIndex(Function(card) Not card.Visible)
                    Assert.IsTrue(index > 0, "The shelf is still filling.")
                    Dim hidden As RoundedPanel = all(index)

                    Dim current As Control = all(index - 1).Controls.OfType(Of LinkLabel)().First()
                    board.ActiveControl = current
                    For steps As Integer = 1 To 6
                        If CardOf(current) Is hidden Then Exit For
                        board.SelectNextControl(current, True, True, True, False)
                        current = DeepestActiveControl(board)
                    Next

                    Dim title As LinkLabel = TryCast(current, LinkLabel)
                    Assert.IsTrue(title IsNot Nothing AndAlso CardOf(title) Is hidden AndAlso title.Text = hidden.AccessibleName,
                                  "Tab reaches the title of the card the fill had not shown: " & If(current?.GetType().Name, "nothing") & ".")
                    Assert.IsTrue(board.IsBoardFillPending, "The shelf is still filling.")

                    PostKey(title, Keys.Space)
                    Application.DoEvents()

                    Assert.AreEqual(Form1.WorkspacePage.Manuscript.ToString(), board.PageName, "Space on the title opens a manuscript's page.")
                    Assert.AreEqual(hidden.AccessibleName, board.Library.Single(Function(item) item.Id = board.OpenManuscriptId).Title, "The card's own manuscript.")

                    board.Close()
                End Using
            End Sub,
            TimeSpan.FromMinutes(5))

    End Sub


    ' A card's menu is built when first asked for: a card the fill has not
    ' reached has none, and its menu, once asked for, acts on the card's own
    ' record, here one a restore put in place of the record it was built for.
    <TestMethod>
    Public Sub MenusAreBuiltWhenFirstNeeded()

        StaRunner.Run(
            Sub()
                Dim library As LargeLibraryFixture.SyntheticLibrary = Library120()

                Using board As New LibraryBoard(library, Path.Combine(_root, "settings"))

                    board.Start()
                    Dim pipeline As ManuscriptShelfPanel = board.Shelf("pipelinePanel")
                    Dim card As CardPanel = Cards(pipeline).OfType(Of CardPanel)().Last(Function(item) Not item.Visible)
                    Assert.IsFalse(card.HasMenu, "A card not shown has no menu yet.")
                    Assert.IsTrue(Cards(pipeline).Where(Function(item) item.IsHandleCreated).OfType(Of CardPanel)().All(Function(item) item.HasMenu), "A card with a window has its menu, so a right-click finds it.")

                    ' A restore's record: equal, so the card is kept, but not the same object.
                    Dim original As Manuscript = board.Library.Single(Function(item) item.Title = card.AccessibleName)
                    Dim replacement As Manuscript = ManuscriptCloneService.CloneManuscript(original)
                    board.Library(board.Library.IndexOf(original)) = replacement
                    board.Render()
                    Assert.AreSame(card, Cards(pipeline).Single(Function(item) item.AccessibleName = original.Title), "The card is kept.")

                    CollectionAssert.AreEqual(MenuFor(ManuscriptLocation.Pipeline), card.ContextMenuStrip.Items.Cast(Of ToolStripItem)().Select(Function(item) item.Text).ToList())
                    Assert.IsTrue(card.HasMenu)
                    card.ContextMenuStrip.Items.Cast(Of ToolStripItem)().Single(Function(item) item.Text = "Open").PerformClick()
                    Application.DoEvents()
                    Assert.AreEqual(replacement.Id, board.OpenManuscriptId)

                    board.Field("Title").Text = original.Title & " (saved through the card)"
                    Assert.IsTrue(board.SaveManuscriptPage())
                    Assert.AreEqual(original.Title & " (saved through the card)", replacement.Title, "The page edited the record the library holds.")
                    Assert.AreNotEqual(replacement.Title, original.Title, "The record the card was built for is left alone.")

                    board.Close()
                End Using
            End Sub,
            TimeSpan.FromMinutes(5))

    End Sub


    ' A change to one card's text rebuilds that card alone: every other
    ' card keeps its window and its place, and the shelf stays scrolled
    ' where it was. Before, every card left the shelf and came back, its
    ' windows moved off and on again, after a save that renamed one.
    <TestMethod>
    Public Sub ReplacingOneCardLeavesTheOthersParented()

        StaRunner.Run(
            Sub()
                Dim library As LargeLibraryFixture.SyntheticLibrary = Library120()

                Using board As New LibraryBoard(library, Path.Combine(_root, "settings"))

                    board.Start()
                    board.WaitForBoardFill()
                    Dim pipeline As ManuscriptShelfPanel = board.Shelf("pipelinePanel")
                    Dim cards As List(Of RoundedPanel) = BoardFillTests.Cards(pipeline)
                    Dim renamed As RoundedPanel = cards(cards.Count \ 2)

                    pipeline.AutoScrollPosition = New Point(0, 400)
                    Application.DoEvents()
                    Dim scrolled As Point = pipeline.AutoScrollPosition
                    Assert.IsTrue(scrolled.Y < 0, "The shelf is scrolled.")

                    Dim moved As New List(Of String)()
                    For Each card As RoundedPanel In cards.Where(Function(item) item IsNot renamed)
                        Dim name As String = card.AccessibleName
                        AddHandler card.ParentChanged, Sub(sender, e) moved.Add(name & " left the shelf")
                        AddHandler card.HandleDestroyed, Sub(sender, e) moved.Add(name & " lost its window")
                    Next

                    Dim subject As Manuscript = board.Library.First(Function(item) item.Title = renamed.AccessibleName)
                    subject.Title &= " (renamed)"
                    board.Render()
                    Application.DoEvents()

                    Assert.AreEqual(0, moved.Count, String.Join(Environment.NewLine, moved))
                    Assert.IsTrue(renamed.IsDisposed, "The renamed card is rebuilt.")
                    Assert.AreEqual(subject.Title, pipeline.Controls(cards.IndexOf(renamed)).AccessibleName, "Its new card takes its place.")
                    Assert.AreEqual(scrolled, pipeline.AutoScrollPosition, "The shelf stays where it was scrolled.")

                    board.Close()
                End Using
            End Sub,
            TimeSpan.FromMinutes(5))

    End Sub


    ' Once the shown shelf has filled, the shelves not shown yet are
    ' prepared while the window idles (#133), and not while another page
    ' covers the Board: each holds a card for every manuscript, hidden, and
    ' its first screen of cards has its windows, made inside the shelf and
    ' none on WinForms' parking window, while the cards the fill has not
    ' reached have none. The first switch to a shelf then shows those very
    ' cards, whose windows are already made, and the fill goes on there.
    ' Before, the first switch built the shelf there and then. With motion
    ' on (#142): the page switches fade, the shelf switch does not, and
    ' neither makes a child window.
    <TestMethod>
    Public Sub OtherShelvesArePreparedAfterTheFill()

        Dim report As New List(Of String)()

        UiMotion.EnabledOverride = True
        UiTheme.HighContrastOverride = False
        ' The test host is DPI-unaware: above 100% Windows scales the board,
        ' where a switch is a cut.
        PageFade.ScaledOverride = False
        Try
        StaRunner.Run(
            Sub()
                Dim library As LargeLibraryFixture.SyntheticLibrary = Library120()

                Using watch As New WindowCreationWatch(),
                      board As New LibraryBoard(library, Path.Combine(_root, "settings"))

                    board.Start()
                    Dim published As ManuscriptShelfPanel = board.Shelf("publishedPanel")
                    Dim drawer As ManuscriptShelfPanel = board.Shelf("fileDrawerPanel")
                    board.WaitForBoardFill()

                    board.NavigateTo(Form1.WorkspacePage.Library)
                    Application.DoEvents()
                    Dim before As Integer = published.Controls.Count + drawer.Controls.Count
                    Dim windowsBefore As Integer = Cards(published).Concat(Cards(drawer)).Where(Function(card) card.IsHandleCreated).Count()
                    Pump(TimeSpan.FromSeconds(1))
                    Assert.AreEqual(before, published.Controls.Count + drawer.Controls.Count, "No shelf is put together behind another page.")
                    Assert.AreEqual(windowsBefore, Cards(published).Concat(Cards(drawer)).Where(Function(card) card.IsHandleCreated).Count(), "No card's windows are made behind another page.")
                    board.NavigateTo(Form1.WorkspacePage.Board)
                    board.WaitForIdleWork()

                    For Each entry In {(published, ManuscriptLocation.Published), (drawer, ManuscriptLocation.FileDrawer)}
                        Dim location As ManuscriptLocation = entry.Item2
                        Dim onShelf As Integer = library.Manuscripts.Where(Function(item) item.Location = location).Count()
                        Assert.IsFalse(entry.Item1.Visible, location.ToString() & " stays hidden.")
                        Assert.AreEqual(onShelf, Cards(entry.Item1).Count, location.ToString() & ": a card for every manuscript.")
                        Assert.IsTrue(Cards(entry.Item1).Any(Function(card) card.IsHandleCreated), location.ToString() & ": its first screen has its windows.")
                        Assert.IsTrue(Cards(entry.Item1).Where(Function(card) card.IsHandleCreated).All(Function(card) DirectCast(card, CardPanel).HasMenu),
                                      location.ToString() & ": a card given its windows has its menu.")
                    Next

                    Dim prepared As List(Of RoundedPanel) = Cards(published)
                    Dim withWindows As List(Of RoundedPanel) = prepared.Where(Function(card) card.IsHandleCreated).ToList()
                    Dim shownAtSwitch As List(Of RoundedPanel) = Nothing
                    AddHandler board.ShelfTab("tabPublished").CheckedChanged,
                        Sub(sender, e)
                            If board.ShelfTab("tabPublished").Checked Then
                                shownAtSwitch = Cards(published).Where(Function(card) card.Visible).ToList()
                            End If
                        End Sub

                    Using switchWatch As New WindowCreationWatch()
                        board.ShelfTab("tabPublished").Checked = True
                        Assert.AreEqual(0, switchWatch.Children().Count, "The switch makes no window: " & String.Join(" | ", switchWatch.Children()))
                    End Using

                    CollectionAssert.AreEqual(prepared, Cards(published), "The switch shows the cards prepared.")
                    CollectionAssert.AreEqual(withWindows, shownAtSwitch, "The cards shown at the switch are those whose windows were made while the window idled.")
                    Dim total As Integer = library.Manuscripts.Where(Function(item) item.Location = ManuscriptLocation.Published).Count()
                    Dim columns As Integer = prepared.Select(Function(card) card.Left).Distinct().Count()
                    Dim rows As Integer = CInt(Math.Ceiling(published.ClientSize.Height / CDbl(prepared(0).Height + published.CardGap)))
                    Assert.AreEqual(Math.Min(total, columns * (rows + 1)), withWindows.Count,
                                    "The first screen had its windows, and only it: " & columns & " columns, " & rows & " rows in view of " & published.ClientSize.ToString() & ".")

                    board.WaitForBoardFill()
                    Assert.IsTrue(Cards(published).All(Function(card) card.Visible AndAlso card.IsHandleCreated), "The fill goes on on the shelf shown.")

                    report.AddRange(watch.Parked())
                    board.Close()
                End Using
            End Sub,
            TimeSpan.FromMinutes(5))
        Finally
            UiMotion.EnabledOverride = Nothing
            UiTheme.HighContrastOverride = Nothing
            PageFade.ScaledOverride = Nothing
        End Try

        Assert.AreEqual(0, report.Count, "Windows made on the parking window:" & Environment.NewLine & String.Join(Environment.NewLine, report))

    End Sub


    ' ---------------------------------------------------------------

    Private Function Library120() As LargeLibraryFixture.SyntheticLibrary
        Return LargeLibraryFixture.Create(Path.Combine(_root, "library"), 120)
    End Function


    Friend Shared Function Cards(shelf As Control) As List(Of RoundedPanel)
        Return shelf.Controls.OfType(Of RoundedPanel)().ToList()
    End Function


    Private Shared Function CardOf(control As Control) As RoundedPanel
        While control IsNot Nothing AndAlso Not TypeOf control Is RoundedPanel
            control = control.Parent
        End While
        Return DirectCast(control, RoundedPanel)
    End Function


    Private Shared Function MenuFor(location As ManuscriptLocation) As List(Of String)
        Select Case location
            Case ManuscriptLocation.Published : Return New List(Of String) From {"Open", "View Route", "", "Delete..."}
            Case ManuscriptLocation.FileDrawer : Return New List(Of String) From {"Open", "View Route", "", "Restore to Pipeline", "", "Delete..."}
            Case Else : Return New List(Of String) From {"Open", "View Route", "", "Move to File Drawer...", "", "Delete..."}
        End Select
    End Function


    Private Shared Sub Pump(duration As TimeSpan)
        Dim watch As Diagnostics.Stopwatch = Diagnostics.Stopwatch.StartNew()
        While watch.Elapsed < duration
            Application.DoEvents()
            Threading.Thread.Sleep(5)
        End While
    End Sub


    Private Shared Sub PostKey(target As Control, key As Keys)
        PostMessage(target.Handle, &H100UI, New IntPtr(CInt(key)), New IntPtr(1))
    End Sub


    <DllImport("user32.dll")>
    Private Shared Function PostMessage(handle As IntPtr, message As UInteger, wParam As IntPtr, lParam As IntPtr) As Boolean
    End Function

End Class
