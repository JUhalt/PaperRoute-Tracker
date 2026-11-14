Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.Linq
Imports System.Runtime.InteropServices
Imports System.Windows.Forms
Imports ManuscriptPipeline
Imports ManuscriptPipeline.Controls
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services
Imports Microsoft.VisualStudio.TestTools.UnitTesting

' The Board's stage and sort filters (#338). A choice made in an open list
' renders the Board once, as the list closes, not at each arrow step; a
' step on a closed box renders at once, as before. Each such render, and
' Clear, holds the shelves' drawing as a shelf switch does, so the cards
' are painted once, after it. What the Board shows does not change.
'
' A render is told by the window's attention snapshots, which every render
' makes anew (LibraryBoard.RenderMark), and a held render by the two
' WM_SETREDRAW it sends the shelves' host. The open list is the window's
' own seam (LibraryBoard.StepOpenList): a dropped list takes the mouse,
' which a person at the PC may be using.
<TestClass>
<DoNotParallelize>
Public Class BoardFilterTests

    Private Const WM_SETREDRAW As Integer = &HB
    Private Const WM_KEYDOWN As UInteger = &H100UI
    Private Const WM_KEYUP As UInteger = &H101UI

    Private Const AllStages As Integer = 0
    Private Const Draft As Integer = 2
    Private Const Submitted As Integer = 3

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


    ' Idea, Draft, Submitted with the arrow keys, then Enter: on v1.0.0
    ' three renders, one at each step. Now none until the list closes, then
    ' one, held, showing the Submitted Pipeline manuscripts as a render with
    ' Submitted chosen shows them.
    <TestMethod>
    Public Sub SteppingTheOpenStageListRendersOnceWhenItCloses()

        StaRunner.Run(
            Sub()
                Using board As LibraryBoard = StartBoard()

                    Dim shelves As Control = board.Part(Of Panel)("shelfHost")
                    Dim all As List(Of Manuscript) = board.ShelfManuscripts("pipelinePanel")
                    Dim mark As Object = board.RenderMark

                    Using watch As New SentMessageWatch()
                        board.StepOpenList("cboStageFilter", 1, Draft, Submitted)
                        board.CloseList("cboStageFilter")
                        Assert.AreSame(mark, board.RenderMark, "Nothing is rendered while the list is open, nor as it closes.")
                        CollectionAssert.AreEqual(all, board.ShelfManuscripts("pipelinePanel"), "The shelf still shows All stages.")
                        Application.DoEvents()
                        Assert.AreNotSame(mark, board.RenderMark, "The Board rendered once the close was handled.")
                        Assert.AreEqual(2, watch.Count(shelves, WM_SETREDRAW), "One render, holding the shelves' drawing.")
                    End Using

                    AssertShows(board, Submitted)
                    board.Close()
                End Using
            End Sub,
            TimeSpan.FromMinutes(3))

    End Sub


    ' Esc, or a click away, closes the list. Whether Windows hands back the
    ' choice the list opened with, before the close or after it, or keeps
    ' the last step, the Board ends on the box's choice, and renders only
    ' when that is not what it shows. A mouse pick whose choice comes after
    ' the close renders once.
    <TestMethod>
    Public Sub AListClosedWithoutAChoiceSettlesOnTheBoxWithoutAStrayRender()

        StaRunner.Run(
            Sub()
                Using board As LibraryBoard = StartBoard()

                    Dim box As ComboBox = board.Part(Of ComboBox)("cboStageFilter")
                    Dim all As List(Of Manuscript) = board.ShelfManuscripts("pipelinePanel")

                    ' The choice handed back while the list is open.
                    Assert.AreEqual(0, Renders(board, Sub() board.StepOpenList("cboStageFilter", 1, Draft, AllStages), Sub() board.CloseList("cboStageFilter")),
                                    "Steps handed back before the close render nothing.")
                    CollectionAssert.AreEqual(all, board.ShelfManuscripts("pipelinePanel"), "The shelf shows All stages, as the box does.")

                    ' The choice handed back after the close.
                    Assert.AreEqual(0, Renders(board,
                                               Sub() board.StepOpenList("cboStageFilter", 1, Draft),
                                               Sub()
                                                   board.CloseList("cboStageFilter")
                                                   box.SelectedIndex = AllStages
                                               End Sub),
                                    "Steps handed back after the close render nothing.")
                    CollectionAssert.AreEqual(all, board.ShelfManuscripts("pipelinePanel"), "The shelf shows All stages, as the box does.")

                    ' The last step kept.
                    Assert.AreEqual(1, Renders(board, Sub() board.StepOpenList("cboStageFilter", 1, Draft), Sub() board.CloseList("cboStageFilter")),
                                    "A step kept as the list closes renders once.")
                    AssertShows(board, Draft)

                    ' A pick whose choice comes after the close.
                    Assert.AreEqual(1, Renders(board,
                                               Sub() board.StepOpenList("cboStageFilter"),
                                               Sub()
                                                   board.CloseList("cboStageFilter")
                                                   box.SelectedIndex = Submitted
                                               End Sub),
                                    "A choice that comes after the close renders once.")
                    AssertShows(board, Submitted)

                    board.Close()
                End Using
            End Sub,
            TimeSpan.FromMinutes(3))

    End Sub


    ' An arrow key or the mouse wheel on the closed box renders at once, as
    ' on v1.0.0, holding the shelves' drawing: no delay is added.
    <TestMethod>
    Public Sub AStepOnAClosedStageBoxRendersAtOnce()

        StaRunner.Run(
            Sub()
                Using board As LibraryBoard = StartBoard()

                    Dim shelves As Control = board.Part(Of Panel)("shelfHost")

                    For Each stage As Integer In {Draft, AllStages}
                        Dim mark As Object = board.RenderMark
                        Using watch As New SentMessageWatch()
                            board.ChooseStage(stage)
                            Assert.AreNotSame(mark, board.RenderMark, "The step rendered before any message was processed.")
                            Assert.AreEqual(2, watch.Count(shelves, WM_SETREDRAW), "One render, holding the shelves' drawing.")
                        End Using
                        AssertShows(board, stage)
                    Next

                    board.Close()
                End Using
            End Sub,
            TimeSpan.FromMinutes(3))

    End Sub


    ' The sort box shares the stage filter's handler and behaves the same.
    <TestMethod>
    Public Sub TheSortBoxRendersOnceWhenItsListClosesAndAtOnceWhenClosed()

        StaRunner.Run(
            Sub()
                Using board As LibraryBoard = StartBoard()

                    Dim shelves As Control = board.Part(Of Panel)("shelfHost")
                    Dim sort As ComboBox = board.Part(Of ComboBox)("cboBoardSort")
                    Dim current As List(Of Manuscript) = board.ShelfManuscripts("pipelinePanel")
                    Dim mark As Object = board.RenderMark

                    ' Title A-Z, then Title Z-A, in the open list.
                    Using watch As New SentMessageWatch()
                        board.StepOpenList("cboBoardSort", 1, 2)
                        board.CloseList("cboBoardSort")
                        Assert.AreSame(mark, board.RenderMark, "Nothing is rendered while the sort list is open, nor as it closes.")
                        CollectionAssert.AreEqual(current, board.ShelfManuscripts("pipelinePanel"), "The shelf keeps its order until the close is handled.")
                        Application.DoEvents()
                        Assert.AreNotSame(mark, board.RenderMark, "The Board rendered once the close was handled.")
                        Assert.AreEqual(2, watch.Count(shelves, WM_SETREDRAW), "One render, holding the shelves' drawing.")
                    End Using
                    board.WaitForBoardFill()
                    Dim zToA As List(Of Manuscript) = board.ShelfManuscripts("pipelinePanel")
                    CollectionAssert.AreEqual(current.OrderByDescending(Function(item) item.Title, StringComparer.CurrentCultureIgnoreCase).ToList(), zToA.ToList(),
                                              "The shelf is sorted Title Z-A.")
                    board.Render()
                    board.WaitForBoardFill()
                    CollectionAssert.AreEqual(zToA, board.ShelfManuscripts("pipelinePanel"), "The shelf shows what a render with Title Z-A chosen shows.")

                    ' Title A-Z on the closed box.
                    mark = board.RenderMark
                    sort.SelectedIndex = 1
                    Assert.AreNotSame(mark, board.RenderMark, "A step on the closed sort box renders at once.")
                    board.WaitForBoardFill()
                    CollectionAssert.AreEqual(Enumerable.Reverse(zToA).ToList(), board.ShelfManuscripts("pipelinePanel"), "The shelf is sorted Title A-Z.")

                    board.Close()
                End Using
            End Sub,
            TimeSpan.FromMinutes(3))

    End Sub


    ' Nothing inside the shelves is painted, framed or erased while a
    ' filter change or Clear runs: on v1.0.0 each card put on the shown
    ' shelf was erased there and then. The shelves are drawn once the
    ' window processes its messages.
    <TestMethod>
    Public Sub AFilterChangePaintsNothingSynchronously()

        Dim report As New List(Of String)()

        StaRunner.Run(
            Sub()
                Using board As LibraryBoard = StartBoard()

                    Dim shelves As Control = board.Part(Of Panel)("shelfHost")
                    Dim clear As Button = board.Part(Of Button)("btnClearBoardFilters")

                    Dim watchChange As Action(Of String, Action) =
                        Sub(name, change)
                            Using watch As New PaintWatch(shelves)
                                change()
                                report.AddRange(watch.Sent().Select(Function(message) name & ": " & message).Take(10))
                            End Using
                            Dim painted As Boolean = False
                            Dim handler As PaintEventHandler = Sub(sender, e) painted = True
                            Dim parts As List(Of Control) = {shelves}.Concat(ViewCatalog.Descendants(shelves)).Where(Function(control) control.Visible).ToList()
                            For Each part As Control In parts
                                AddHandler part.Paint, handler
                            Next
                            Application.DoEvents()
                            For Each part As Control In parts
                                RemoveHandler part.Paint, handler
                            Next
                            If Not painted Then report.Add(name & ": nothing was drawn once the window processed its messages")
                            board.WaitForBoardFill()
                            Application.DoEvents()
                        End Sub

                    watchChange("All stages to Submitted", Sub() board.ChooseStage(Submitted))
                    watchChange("Submitted to All stages", Sub() board.ChooseStage(AllStages))
                    watchChange("All stages to Draft", Sub() board.ChooseStage(Draft))
                    watchChange("Clear", Sub() clear.PerformClick())

                    Assert.AreEqual(AllStages, board.Part(Of ComboBox)("cboStageFilter").SelectedIndex, "Clear chose All stages.")
                    board.Close()
                End Using
            End Sub,
            TimeSpan.FromMinutes(3))

        Assert.AreEqual(0, report.Count, "Drawn while a filter change ran:" & Environment.NewLine & String.Join(Environment.NewLine, report))

    End Sub


    ' Clear pressed with the keyboard disables itself as its render ends,
    ' and WinForms moves the keyboard on, to the first card's title, which
    ' the render under the shelves' hold must not refuse (RedrawHold). On
    ' v1.0.0 the title had it, in WinForms and in Windows; so it does now.
    <TestMethod>
    Public Sub ClearPressedWithTheKeyboardLeavesFocusOnTheFirstCard()

        StaRunner.Run(
            Sub()
                Using board As LibraryBoard = StartBoard()

                    Dim clear As Button = board.Part(Of Button)("btnClearBoardFilters")
                    board.ChooseStage(Submitted)
                    board.WaitForBoardFill()
                    Application.DoEvents()
                    Assert.IsTrue(clear.Enabled, "Clear is enabled with a filter on.")

                    If Not clear.Focus() Then board.ActiveControl = clear
                    Application.DoEvents()
                    Assert.AreSame(clear, DeepestActiveControl(board), "Clear has the keyboard.")

                    Dim shelves As Control = board.Part(Of Panel)("shelfHost")
                    Using watch As New SentMessageWatch()
                        PressSpace(clear)
                        Assert.AreEqual(2, watch.Count(shelves, WM_SETREDRAW), "Clear's render held the shelves' drawing while the keyboard moved into them.")
                    End Using
                    board.WaitForBoardFill()
                    Application.DoEvents()

                    Assert.AreEqual(AllStages, board.Part(Of ComboBox)("cboStageFilter").SelectedIndex, "Clear chose All stages.")
                    Assert.IsFalse(clear.Enabled, "Clear disabled itself.")
                    Dim firstCard As RoundedPanel = board.Shelf("pipelinePanel").Controls.OfType(Of RoundedPanel)().First()
                    Dim title As LinkLabel = firstCard.Controls.OfType(Of LinkLabel)().First()
                    Assert.AreSame(title, DeepestActiveControl(board), "WinForms has the keyboard on the first card's title, not on " & Describe(DeepestActiveControl(board)) & ".")
                    Assert.AreEqual(title.Handle, GetFocus(), "Windows has the keyboard on the first card's title, not on " & Describe(Control.FromChildHandle(GetFocus())) & ".")

                    board.Close()
                End Using
            End Sub,
            TimeSpan.FromMinutes(3))

    End Sub


    Private Function StartBoard() As LibraryBoard
        Dim board As New LibraryBoard(LargeLibraryFixture.Create(Path.Combine(_root, "library"), 120), Path.Combine(_root, "settings"))
        board.Start()
        board.WaitForIdleWork()
        Return board
    End Function


    ' How many held renders the steps and the close gave once the window
    ' processed its messages; none before then.
    Private Shared Function Renders(board As LibraryBoard, steps As Action, close As Action) As Integer
        Dim shelves As Control = board.Part(Of Panel)("shelfHost")
        Dim mark As Object = board.RenderMark
        Dim count As Integer
        Using watch As New SentMessageWatch()
            steps()
            close()
            Assert.AreSame(mark, board.RenderMark, "Nothing is rendered before the close is handled.")
            Application.DoEvents()
            count = watch.Count(shelves, WM_SETREDRAW) \ 2
            Assert.AreEqual(count > 0, mark IsNot board.RenderMark, "A render, and only a render, holds the shelves' drawing.")
        End Using
        board.WaitForBoardFill()
        Return count
    End Function


    ' The Pipeline shelf shows the manuscripts of the stage chosen, in the
    ' library's order, and the box shows that stage.
    Private Shared Sub AssertShows(board As LibraryBoard, stage As Integer)
        board.WaitForBoardFill()
        Assert.AreEqual(stage, board.Part(Of ComboBox)("cboStageFilter").SelectedIndex, "The stage filter's choice.")
        Dim expected As List(Of Manuscript) =
            board.Library.Where(Function(item) item.Location = ManuscriptLocation.Pipeline AndAlso
                                               (stage = AllStages OrElse item.CurrentStage = StageAt(stage))).ToList()
        Assert.IsTrue(expected.Count > 0, "The library has Pipeline manuscripts at the stage chosen.")
        CollectionAssert.AreEqual(expected, board.ShelfManuscripts("pipelinePanel"), "The Pipeline shelf's cards for the stage chosen.")
    End Sub


    Private Shared Function StageAt(index As Integer) As PaperStage
        Select Case index
            Case Draft
                Return PaperStage.Draft
            Case Submitted
                Return PaperStage.Submitted
            Case Else
                Return PaperStage.Idea
        End Select
    End Function


    ' Space on the control, pressed and let go, with the buttons, Tab and
    ' modifiers released in this thread's key state first (HostInput, #340).
    Private Shared Sub PressSpace(target As Control)
        HostInput.HoldOnly()
        PostMessage(target.Handle, WM_KEYDOWN, New IntPtr(CInt(Keys.Space)), New IntPtr(1))
        PostMessage(target.Handle, WM_KEYUP, New IntPtr(CInt(Keys.Space)), New IntPtr(1))
        Application.DoEvents()
    End Sub


    ' A control by its type and name, and the card it is on, for a failure
    ' message.
    Private Shared Function Describe(control As Control) As String
        If control Is Nothing Then Return "nothing"
        Dim text As String = control.GetType().Name & " '" & If(String.IsNullOrEmpty(control.AccessibleName), control.Text, control.AccessibleName) & "'"
        Dim card As Control = control
        While card IsNot Nothing AndAlso TypeOf card IsNot RoundedPanel
            card = card.Parent
        End While
        If card IsNot Nothing AndAlso card.Parent IsNot Nothing Then
            text &= " on card " & card.Parent.Controls.GetChildIndex(card).ToString() & " of its shelf"
        End If
        Return text
    End Function


    <DllImport("user32.dll")>
    Private Shared Function PostMessage(handle As IntPtr, message As UInteger, wParam As IntPtr, lParam As IntPtr) As Boolean
    End Function

    <DllImport("user32.dll")>
    Private Shared Function GetFocus() As IntPtr
    End Function

End Class
