Imports System
Imports System.Collections.Generic
Imports System.Diagnostics
Imports System.Drawing
Imports System.IO
Imports System.Linq
Imports System.Runtime.InteropServices
Imports System.Threading
Imports System.Threading.Tasks
Imports System.Windows.Forms
Imports ManuscriptPipeline
Imports ManuscriptPipeline.Controls
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services
Imports Microsoft.VisualStudio.TestTools.UnitTesting

' A page switch's cross-fade (#142): with motion off nothing is added and
' no clock runs; with motion on, a picture of the page left is shown over
' the page area once the switch has returned and fades out on its own
' within 200 ms; the first key or click ends it and reaches the new page;
' the overlay takes no focus, no input and no place in the window's tree
' or in what a screen reader reads; a switch that outlasts the fade, or a
' picture not read in time, is a cut, and the switch never waits for the
' picture. Each test sets the seams it needs and puts them back.
<TestClass>
<DoNotParallelize>
Public Class MotionFadeTests

    Private _root As String = String.Empty


    <TestInitialize>
    Public Sub Initialize()
        _root = CreateTemporaryRoot()
        StorageEnvironment.RootOverride = Path.Combine(_root, "storage")
    End Sub


    <TestCleanup>
    Public Sub Cleanup()
        ResetSeams()
        StorageEnvironment.RootOverride = Nothing
        DeleteTemporaryRoot(_root)
    End Sub


    Friend Shared Sub ResetSeams()
        UiMotion.EnabledOverride = Nothing
        UiMotion.FadeMilliseconds = UiMotion.DefaultFadeMilliseconds
        UiMotion.HoverMilliseconds = UiMotion.DefaultHoverMilliseconds
        UiTheme.HighContrastOverride = Nothing
        PageFade.CaptureDelayMilliseconds = 0
        PageFade.AwaitPicture = False
        PageFade.ScaledOverride = Nothing
    End Sub


    ' Runs the work on a started board, the seams set as asked and put back.
    ' The test host is DPI-unaware, so above 100% Windows scales the board,
    ' and a switch there is a cut (AWindowWindowsScalesSwitchesAsACut): the
    ' fade is told the board is not scaled.
    Private Sub OnBoard(work As Action(Of LibraryBoard), Optional manuscripts As Integer = 60, Optional motion As Boolean = True, Optional awaitPicture As Boolean = True)

        StaRunner.Run(
            Sub()
                Try
                    UiMotion.EnabledOverride = motion
                    UiTheme.HighContrastOverride = False
                    PageFade.AwaitPicture = awaitPicture
                    PageFade.ScaledOverride = False
                    Using board As New LibraryBoard(LargeLibraryFixture.Create(Path.Combine(_root, "library"), manuscripts), Path.Combine(_root, "settings"))
                        board.Start()
                        board.WaitForIdleWork()
                        work(board)
                        board.Close()
                    End Using
                Finally
                    ResetSeams()
                End Try
            End Sub,
            TimeSpan.FromMinutes(5))

    End Sub


    ' With motion off a switch adds no overlay and starts no clock: the
    ' fade is never made, and no layered window is on the thread.
    <TestMethod>
    Public Sub WithMotionOffASwitchAddsNoOverlayAndStartsNoTimer()

        OnBoard(
            Sub(board)
                For Each page As Form1.WorkspacePage In {Form1.WorkspacePage.Library, Form1.WorkspacePage.Board, Form1.WorkspacePage.Journals, Form1.WorkspacePage.Deadlines, Form1.WorkspacePage.Board}
                    board.NavigateTo(page)
                    Assert.IsNull(board.PageFade, "No fade is made with motion off (" & page.ToString() & ").")
                    Assert.IsFalse(UiMotion.Animating, "No clock runs with motion off (" & page.ToString() & ").")
                    Assert.AreEqual(0, OverlaysOfThisThread().Count, "No overlay with motion off (" & page.ToString() & ").")
                    Application.DoEvents()
                Next
            End Sub,
            motion:=False)

    End Sub


    ' With motion on the overlay is there as each switch returns: over the
    ' page area exactly, owned by the window, a popup that is disabled,
    ' layered, transparent to the pointer, never activated and without a
    ' taskbar button, never a child; the clock and the input filter run.
    ' Left alone, it is hidden within 200 ms of the switch, and the clock
    ' and the filter have stopped. (The fade's frames run on the window's
    ' thread: a paint of the new page that holds the thread longer, as this
    ' machine's screen can hold one half a second, holds the overlay as
    ' long, and it is hidden on the first frame after.)
    <TestMethod>
    Public Sub WithMotionOnTheOverlayShowsAndIsGoneWithin200Ms()

        Dim report As New List(Of String)()
        Dim fades As Integer = 0

        OnBoard(
            Sub(board)
                ' Each page visited once first: a first visit that builds its
                ' page can take longer than the fade, and is then a cut.
                For Each page As Form1.WorkspacePage In {Form1.WorkspacePage.Deadlines, Form1.WorkspacePage.ImportExport, Form1.WorkspacePage.Board}
                    board.NavigateTo(page)
                    Application.DoEvents()
                    board.WaitForIdleWork()
                    Settle(board)
                Next

                For Each page As Form1.WorkspacePage In {Form1.WorkspacePage.Library, Form1.WorkspacePage.Board, Form1.WorkspacePage.Journals, Form1.WorkspacePage.Board,
                                                         Form1.WorkspacePage.Deadlines, Form1.WorkspacePage.Insights, Form1.WorkspacePage.ImportExport, Form1.WorkspacePage.Board}

                    board.NavigateTo(page)
                    Dim since As Stopwatch = Stopwatch.StartNew()
                    Dim fade As PageFade = board.PageFade
                    Dim name As String = "To " & page.ToString()

                    ' A switch that took the fade's length is a cut, as it should be.
                    If fade IsNot Nothing AndAlso fade.LastOutcome = "cut: the switch took longer than the fade" Then
                        Console.WriteLine(name & ": a cut, the switch took longer than the fade")
                        Continue For
                    End If

                    If fade Is Nothing OrElse fade.LastOutcome <> "faded" Then
                        report.Add(name & ": no fade (" & If(fade?.LastOutcome, "no fade made") & ")")
                        Continue For
                    End If

                    fades += 1

                    Dim overlay As IntPtr = fade.OverlayHandle
                    Dim bounds As Rectangle = WindowBounds(overlay)
                    Dim area As Rectangle = board.ContentHost.RectangleToScreen(board.ContentHost.ClientRectangle)
                    If Not fade.Showing Then report.Add(name & ": the overlay is not showing as the switch returns")
                    If bounds <> area Then report.Add(name & ": the overlay is at " & bounds.ToString() & ", the page area at " & area.ToString())
                    If GetWindow(overlay, GW_OWNER) <> board.Handle Then report.Add(name & ": the overlay is not owned by the window")
                    Dim style As Integer = GetWindowLong(overlay, GWL_STYLE)
                    Dim exStyle As Integer = GetWindowLong(overlay, GWL_EXSTYLE)
                    If (style And PageFade.WS_POPUP) = 0 OrElse (style And PageFade.WS_DISABLED) = 0 OrElse (style And PageFade.WS_CHILD) <> 0 Then
                        report.Add(name & ": the overlay's style is " & style.ToString("X8"))
                    End If
                    Dim wanted As Integer = PageFade.WS_EX_LAYERED Or PageFade.WS_EX_TRANSPARENT Or PageFade.WS_EX_NOACTIVATE Or PageFade.WS_EX_TOOLWINDOW
                    If (exStyle And wanted) <> wanted Then report.Add(name & ": the overlay's extended style is " & exStyle.ToString("X8"))
                    If Not UiMotion.Animating Then report.Add(name & ": the fade's clock is not running")
                    If Not fade.Filtering Then report.Add(name & ": the fade is not listening for input")

                    ' The fade's frames run on the window's thread, so a paint
                    ' that holds the thread holds the overlay too: the longest
                    ' time the thread processed no message is timed alongside.
                    Dim longestGap As Double = 0
                    Dim lastPass As Double = 0
                    While fade.Showing AndAlso since.ElapsedMilliseconds < 2000
                        Application.DoEvents()
                        Dim now As Double = since.Elapsed.TotalMilliseconds
                        longestGap = Math.Max(longestGap, now - lastPass)
                        lastPass = now
                        Thread.Sleep(1)
                    End While

                    Dim gone As Long = since.ElapsedMilliseconds
                    Console.WriteLine(name & ": the overlay was hidden " & gone.ToString() & " ms after the switch returned; the longest the thread was held meanwhile, " & longestGap.ToString("0") & " ms")
                    If fade.Showing OrElse (gone > 200 AndAlso gone > longestGap + 50) Then report.Add(name & ": the overlay was still showing " & gone.ToString() & " ms after the switch, the thread held at most " & longestGap.ToString("0") & " ms")
                    If fade.Filtering Then report.Add(name & ": the input filter is still on once the fade ended")
                    If UiMotion.Animating Then report.Add(name & ": the clock still runs once the fade ended")

                    If page = Form1.WorkspacePage.Insights Then board.WaitForInsights()
                    board.WaitForIdleWork()
                    Settle(board)
                Next
            End Sub)

        Assert.AreEqual(0, report.Count, String.Join(Environment.NewLine, report))
        Assert.IsTrue(fades >= 5, "Switches that faded: " & fades.ToString() & " of 8.")

    End Sub


    ' Ten switches make one overlay, kept hidden between fades.
    <TestMethod>
    Public Sub TheOverlayIsMadeOncePerWindow()

        OnBoard(
            Sub(board)
                For index As Integer = 1 To 5
                    board.NavigateTo(Form1.WorkspacePage.Journals)
                    Settle(board)
                    board.NavigateTo(Form1.WorkspacePage.Board)
                    Settle(board)
                Next
                Console.WriteLine("Faded: " & board.PageFade.FadesShown.ToString() & " of 10 switches.")
                Assert.IsTrue(board.PageFade.FadesShown >= 2, "Switches faded: " & board.PageFade.FadesShown.ToString())
                Assert.AreEqual(1, board.PageFade.OverlaysMade, "One overlay for the window.")
                Assert.AreEqual(1, OverlaysOfThisThread().Count, "One overlay window on the thread.")
            End Sub)

    End Sub


    ' A key pressed while the picture fades ends the fade there and then,
    ' and reaches the page shown: Ctrl+2 from the Board's search box puts
    ' the keyboard in the Library's filter (#213), and the "a" typed during
    ' the fade is in it.
    <TestMethod>
    Public Sub AKeyDuringTheFadeEndsItAndReachesTheNewPage()

        OnBoard(
            Sub(board)
                UiMotion.FadeMilliseconds = 10000
                Dim search As TextBox = ViewCatalog.Descendants(board).OfType(Of TextBox)().Single(Function(box) box.AccessibleName = "Search the board (Ctrl+F)")
                If Not search.Focus() Then board.ActiveControl = search
                Application.DoEvents()

                board.PressCommandKey(Keys.Control Or Keys.D2)
                Assert.AreEqual("faded", board.PageFade.LastOutcome)
                Assert.IsTrue(board.PageFade.Showing, "The picture shows over the Library.")

                Dim filter As TextBox = ViewCatalog.Descendants(board.PageView).OfType(Of TextBox)().Single(Function(box) box.AccessibleName = "Filter the Library")
                Assert.AreSame(filter, DeepestActiveControl(board), "The keyboard is in the Library's filter.")
                ' The key down only: the message loop makes its character.
                PostMessage(filter.Handle, WM_KEYDOWN, New IntPtr(CInt(Keys.A)), New IntPtr(1))
                Application.DoEvents()

                Assert.IsFalse(board.PageFade.Showing, "The key ended the fade.")
                Assert.IsFalse(board.PageFade.Filtering, "The fade no longer listens.")
                Assert.AreEqual("a", filter.Text, "The key reached the Library's filter.")
            End Sub)

    End Sub


    ' A click while the picture fades ends the fade there and then, and
    ' reaches the control under the pointer on the page shown. The control
    ' is the Library's Authors & Affiliations tab (a click on Import &
    ' Export's commands would open their dialogs). The overlay answers
    ' Windows' hit test over the tab as transparent. On the screen (the
    ' OnScreen test, with PAPERROUTE_ON_SCREEN_TESTS=1 on an unlocked
    ' desktop, the window put above the others and never activated;
    ' Inconclusive otherwise, or where Windows finds another window over
    ' the tab with no fade showing), Windows' hit test at the tab finds the
    ' tab with the overlay showing over it, and the click checks the tab,
    ' as a tab clicked does once Windows finds it under the pointer. (Off
    ' the screen the click is posted to the tab, so it reaches the tab
    ' whatever is over it.)
    <TestMethod>
    Public Sub AClickDuringTheFadeEndsItAndReachesTheControlUnderThePointer()

        ClickDuringTheFade(
            onScreen:=False
        )

    End Sub


    ' The click on the screen, where Windows' own hit test finds the tab.
    ' CI and the release candidate leave OnScreen tests out; certification
    ' runs them by hand on an unlocked desktop.
    <TestMethod>
    <TestCategory("OnScreen")>
    Public Sub AClickDuringTheFadeOnTheScreenReachesTheTabWindowsFinds()

        ClickDuringTheFade(
            onScreen:=True
        )

    End Sub


    Private Sub ClickDuringTheFade(onScreen As Boolean)

        If onScreen Then
            If Not String.Equals(Environment.GetEnvironmentVariable("PAPERROUTE_ON_SCREEN_TESTS"), "1", StringComparison.Ordinal) Then
                Assert.Inconclusive("Set PAPERROUTE_ON_SCREEN_TESTS=1 on an unlocked desktop to put the test window on the screen.")
            End If
            If Not DesktopUnlocked() Then
                Assert.Inconclusive("The desktop is locked: Windows finds no window under a point of it.")
            End If
        End If

        OnBoard(
            Sub(board)
                UiMotion.FadeMilliseconds = 10000
                If onScreen Then
                    Dim area As Rectangle = Screen.PrimaryScreen.WorkingArea
                    board.Location = New Point(area.Left + 20, area.Top + 20)
                    SetWindowPos(board.Handle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE Or SWP_NOSIZE Or SWP_NOACTIVATE)
                    Application.DoEvents()
                End If
                Dim findTab As Func(Of RadioButton) =
                    Function() ViewCatalog.Descendants(board.PageView).OfType(Of RadioButton)().Single(Function(item) item.Text = "Authors & Affiliations")

                board.NavigateTo(Form1.WorkspacePage.Library)
                Application.DoEvents()
                board.WaitForIdleWork()
                board.PageFade.End()
                Pump(80)

                ' On the screen, with no fade showing, Windows finds the tab at
                ' its middle; a window over it there, such as the lock screen,
                ' leaves nothing to hold.
                If onScreen Then
                    Dim first As RadioButton = findTab()
                    Dim middle As Point = first.PointToScreen(New Point(first.Width \ 2, first.Height \ 2))
                    Dim over As IntPtr = WindowFromPoint(middle)
                    If over <> first.Handle Then
                        Assert.Inconclusive("Another window is over the test window on the screen: " & WindowName(over) & ".")
                    End If
                End If

                board.NavigateTo(Form1.WorkspacePage.Board)
                board.WaitForIdleWork()
                board.PageFade.End()
                Pump(80)

                board.NavigateTo(Form1.WorkspacePage.Library)
                Assert.AreEqual("faded", board.PageFade.LastOutcome)
                Assert.IsTrue(board.PageFade.Showing, "The picture shows over the Library.")

                Dim tab As RadioButton = findTab()
                Dim downs As Integer = 0
                AddHandler tab.MouseDown, Sub(sender, e) downs += 1
                Dim inside As New Point(tab.Width \ 2, tab.Height \ 2)
                Dim at As Point = tab.PointToScreen(inside)
                Assert.IsTrue(WindowBounds(board.PageFade.OverlayHandle).Contains(at), "The overlay is over the tab.")
                Assert.AreEqual(New IntPtr(HTTRANSPARENT), SendMessage(board.PageFade.OverlayHandle, WM_NCHITTEST, IntPtr.Zero, MousePoint(at)),
                                "The overlay answers Windows' hit test over the tab as transparent.")
                If onScreen Then
                    Dim found As IntPtr = WindowFromPoint(at)
                    Assert.AreEqual(tab.Handle, found, "Windows' hit test at the tab, with the overlay showing over it, finds the tab, not " & WindowName(found) &
                                    If(found = board.PageFade.OverlayHandle, " (the overlay)", String.Empty))
                End If

                PostMessage(tab.Handle, WM_LBUTTONDOWN, New IntPtr(MK_LBUTTON), MousePoint(inside))
                PostMessage(tab.Handle, WM_LBUTTONUP, IntPtr.Zero, MousePoint(inside))
                Application.DoEvents()

                Assert.IsFalse(board.PageFade.Showing, "The click ended the fade.")
                Assert.AreEqual(1, downs, "The click reached the tab under the pointer.")
                If onScreen Then Assert.IsTrue(tab.Checked, "On the screen, the click checked the tab.")
            End Sub)

    End Sub


    ' The overlay takes nothing: Windows' focus, the active window and the
    ' window's own record of the focused control are the same before and
    ' during a fade; the overlay is disabled and no control; the window's
    ' controls are the same; and UI Automation, asked through Windows from
    ' another thread as a screen reader asks it, says it is neither a
    ' control nor content and takes no focus, so it is in the raw view
    ' only and the window's children in the control view are the same.
    ' The fade is held while UI Automation is asked, which on a busy host
    ' takes longer than a fade of 10 s (the questions took 11 to 16 s, and
    ' the fade ended on its own part way through them): its length is one
    ' no test outlasts, so only what the overlay took could end it.
    <TestMethod>
    Public Sub TheOverlayTakesNoFocusInputOrPlaceInTheTree()

        OnBoard(
            Sub(board)
                UiMotion.FadeMilliseconds = Integer.MaxValue
                Dim rail As Control = ViewCatalog.Descendants(board).OfType(Of RailButton)().First()
                If Not rail.Focus() Then board.ActiveControl = rail
                Application.DoEvents()

                ' A first switch makes the overlay. UI Automation is then
                ' asked about the window alone, as a screen reader has asked
                ' about it long before any fade: the process's first question
                ' sets UI Automation up, here and on the window's thread, and
                ' on a busy host that has made the next answer later than the
                ' 5 s or so UI Automation waits for one, after which it
                ' describes a window from its styles, through its MSAA proxy,
                ' as a focusable pane. The overlay, hidden, is in no view and
                ' is not asked, so the answer checked below is its first, as
                ' a screen reader's first question to it comes while it
                ' shows. That first question checks nothing and is let take
                ' 90 s: alongside two other test runs it once took more
                ' than 20 s. The third switch is the one held.
                board.NavigateTo(Form1.WorkspacePage.Journals)
                board.PageFade.End()
                Pump(80)
                Dim setup As Stopwatch = Stopwatch.StartNew()
                AskUiAutomation(board.Handle, IntPtr.Zero, 90000)
                Console.WriteLine("UI Automation's first question took " & setup.ElapsedMilliseconds.ToString() & " ms.")
                board.NavigateTo(Form1.WorkspacePage.Board)
                board.PageFade.End()
                Pump(80)

                Dim focus As IntPtr = GetFocus()
                Dim active As IntPtr = GetActiveWindow()
                Dim focused As Control = DeepestActiveControl(board)
                Dim forms As Integer = Application.OpenForms.Count

                ' The rail has the keyboard, so the switch itself moves nothing.
                board.NavigateTo(Form1.WorkspacePage.Journals)
                Dim fade As PageFade = board.PageFade
                Assert.IsTrue(fade.Showing, "The fade shows.")
                Application.DoEvents()

                Assert.AreEqual(focus, GetFocus(), "Windows' focus is where it was.")
                Assert.AreEqual(active, GetActiveWindow(), "The active window is the same.")
                Assert.AreSame(focused, DeepestActiveControl(board), "The window's focused control is the same.")
                Assert.IsFalse(IsWindowEnabled(fade.OverlayHandle), "The overlay is disabled.")
                Assert.IsNull(Control.FromHandle(fade.OverlayHandle), "The overlay is no control.")
                Assert.IsNull(Control.FromChildHandle(fade.OverlayHandle), "The overlay is in no control.")
                Assert.AreEqual(forms, Application.OpenForms.Count, "No form was opened.")

                ' UI Automation, asked through Windows as a screen reader asks
                ' it: the overlay's element is neither a control nor content
                ' and takes no focus, so the control view, the one a screen
                ' reader reads, passes over it. (A question asked while
                ' Windows did not show the window itself is asked again:
                ' AskUiAutomationOfShownWindow.)
                Dim showing As UiaReading = AskUiAutomationOfShownWindow(board, fade.OverlayHandle)
                Console.WriteLine("With the overlay showing: " & showing.ToString())
                Assert.AreEqual(Of Object)(False, showing.IsControlElement, "Not a control element.")
                Assert.AreEqual(Of Object)(False, showing.IsContentElement, "Not a content element.")
                Assert.AreEqual(Of Object)(False, showing.IsKeyboardFocusable, "Takes no keyboard focus.")
                Assert.AreNotEqual(fade.OverlayHandle, showing.ControlViewHandle, "The control view passes over the overlay.")
                Assert.AreEqual(board.Handle, showing.RawParentHandle, "In the raw view the overlay is the window's.")
                Dim overlayKey As String = fade.OverlayHandle.ToString("X") & " "
                Assert.IsTrue(showing.RawChildren.Any(Function(child) child.StartsWith(overlayKey, StringComparison.Ordinal)), "The window's children in the raw view hold the overlay.")
                Assert.IsFalse(showing.ControlChildren.Any(Function(child) child.StartsWith(overlayKey, StringComparison.Ordinal)), "The window's children in the control view do not.")

                ' The window's controls, and its children in UI Automation's
                ' control view, are the same with the overlay showing as once
                ' it has gone.
                Dim controls As List(Of Control) = ViewCatalog.Descendants(board).ToList()
                Assert.IsTrue(fade.Showing, "The fade still shows: nothing it took ended it.")
                fade.End()
                Application.DoEvents()
                Dim gone As UiaReading = AskUiAutomationOfShownWindow(board, IntPtr.Zero)
                CollectionAssert.AreEqual(controls, ViewCatalog.Descendants(board).ToList(), "The window's controls are the same.")
                CollectionAssert.AreEqual(gone.ControlChildren, showing.ControlChildren,
                                          "The window's children in the control view are the same. Showing: " & String.Join("; ", showing.ControlChildren) & ". Gone: " & String.Join("; ", gone.ControlChildren))
            End Sub)

    End Sub


    ' A switch that took the fade's length or longer shows none.
    <TestMethod>
    Public Sub ASwitchLongerThanTheFadeShowsNone()

        OnBoard(
            Sub(board)
                UiMotion.FadeMilliseconds = 1
                board.NavigateTo(Form1.WorkspacePage.Library)
                Assert.AreEqual("cut: the switch took longer than the fade", board.PageFade.LastOutcome)
                Assert.IsFalse(board.PageFade.Showing)
                Assert.AreEqual(0, board.PageFade.FadesShown)
                Assert.IsFalse(UiMotion.Animating)
            End Sub)

    End Sub


    ' A picture still being read when the switch returns is a cut, and the
    ' switch does not wait for it: with the read held 400 ms on the worker,
    ' each switch returns in a fraction of that, shows no fade, and the
    ' worker lets the picture go once it has read it.
    <TestMethod>
    Public Sub APictureNotReadInTimeIsACutAndTheSwitchNeverWaits()

        Dim report As New List(Of String)()

        OnBoard(
            Sub(board)
                PageFade.CaptureDelayMilliseconds = 400
                For Each page As Form1.WorkspacePage In {Form1.WorkspacePage.Journals, Form1.WorkspacePage.Board, Form1.WorkspacePage.Deadlines, Form1.WorkspacePage.Board}
                    Dim watch As Stopwatch = Stopwatch.StartNew()
                    board.NavigateTo(page)
                    Dim took As Double = watch.Elapsed.TotalMilliseconds
                    Console.WriteLine("To " & page.ToString() & ": " & took.ToString("0") & " ms, " & board.PageFade.LastOutcome & ", Begin " & board.PageFade.LastBeginMilliseconds.ToString("0.0") & " ms")
                    If board.PageFade.LastOutcome <> "cut: the picture was not ready" Then report.Add("To " & page.ToString() & ": " & board.PageFade.LastOutcome)
                    If took >= 400 Then report.Add("To " & page.ToString() & ": the switch took " & took.ToString("0") & " ms, as long as the read")
                    If board.PageFade.Showing Then report.Add("To " & page.ToString() & ": a fade shows")
                    Dim waited As Stopwatch = Stopwatch.StartNew()
                    While PageFade.PicturesInFlight > 0 AndAlso waited.ElapsedMilliseconds < 3000
                        Application.DoEvents()
                        Thread.Sleep(5)
                    End While
                    If PageFade.PicturesInFlight > 0 Then report.Add("To " & page.ToString() & ": the read never finished")
                Next
                Assert.AreEqual(0, board.PageFade.FadesShown)
            End Sub,
            awaitPicture:=False)

        Assert.AreEqual(0, report.Count, String.Join(Environment.NewLine, report))

    End Sub


    ' Under a contrast theme no switch fades, whatever the seam says.
    <TestMethod>
    Public Sub NothingFadesUnderAContrastTheme()

        OnBoard(
            Sub(board)
                UiTheme.HighContrastOverride = True
                For Each page As Form1.WorkspacePage In {Form1.WorkspacePage.Library, Form1.WorkspacePage.Board, Form1.WorkspacePage.Journals, Form1.WorkspacePage.Board}
                    board.NavigateTo(page)
                    Assert.IsTrue(board.PageFade Is Nothing OrElse board.PageFade.LastOutcome = "cut: motion is off", "To " & page.ToString() & ": " & board.PageFade?.LastOutcome)
                    Assert.IsFalse(board.PageFade IsNot Nothing AndAlso board.PageFade.Showing)
                    Assert.IsFalse(UiMotion.Animating)
                    Application.DoEvents()
                Next
            End Sub)

    End Sub


    ' A manuscript opens as a cut (its versions and details are built on
    ' the UI thread once it has painted, and a fade's frames would wait for
    ' them); leaving it fades.
    <TestMethod>
    Public Sub AManuscriptOpensAsACutAndLeavingItFades()

        OnBoard(
            Sub(board)
                board.Open(board.Library.First(Function(item) item.Location = ManuscriptLocation.Pipeline))
                Assert.AreEqual("cut: a manuscript opens as a cut", board.PageFade.LastOutcome)
                Assert.IsFalse(board.PageFade.Showing)
                Application.DoEvents()
                board.WaitForIdleWork()
                board.NavigateTo(Form1.WorkspacePage.Board)
                Assert.AreEqual("faded", board.PageFade.LastOutcome)
                board.PageFade.End()
            End Sub)

    End Sub


    ' Leaving the manuscript page after the Unsaved Changes question, saved
    ' or not, is a cut: the question is centred over the page, and the
    ' screen can still show it closing as the switch reads the page left.
    <TestMethod>
    Public Sub LeavingAfterTheUnsavedChangesQuestionIsACut()

        OnBoard(
            Sub(board)
                Dim subject As Manuscript = board.Library.First(Function(item) item.Location = ManuscriptLocation.Pipeline)
                For Each answer As DialogResult In {DialogResult.No, DialogResult.Yes}
                    board.Open(subject)
                    Application.DoEvents()
                    board.WaitForIdleWork()
                    Settle(board)
                    board.Field("Title").Text = subject.Title & " (" & answer.ToString() & ")"
                    Dim asked As Integer = 0
                    board.SetPrompt(Function(title)
                                        asked += 1
                                        Return answer
                                    End Function)
                    board.NavigateTo(Form1.WorkspacePage.Board)
                    Assert.AreEqual(1, asked, "Leaving asked about the change (" & answer.ToString() & ").")
                    Assert.AreEqual("Board", board.PageName)
                    Assert.AreEqual("cut: a question was just answered", board.PageFade.LastOutcome, answer.ToString())
                    Assert.IsFalse(board.PageFade.Showing)
                    board.SetPrompt(Nothing)
                    board.WaitForIdleWork()
                    Settle(board)
                Next
            End Sub)

    End Sub


    ' Leaving the manuscript page once Delete Manuscript's question has been
    ' answered Yes is a cut too: the manuscript is gone, so nothing asks
    ' about unsaved changes, but the question was centred over the page and
    ' the screen can still show it closing as the switch reads the page.
    <TestMethod>
    Public Sub LeavingAfterTheDeleteQuestionIsACut()

        OnBoard(
            Sub(board)
                UiMotion.FadeMilliseconds = 10000
                Dim subject As Manuscript = board.Library.First(Function(item) item.Location = ManuscriptLocation.Pipeline)
                board.Open(subject)
                Application.DoEvents()
                board.WaitForIdleWork()
                Settle(board)
                board.SetPrompt(Function(title)
                                    Assert.Fail("Nothing is unsaved, so nothing is asked.")
                                    Return DialogResult.Cancel
                                End Function)
                board.ConfirmDelete()
                Application.DoEvents()
                Assert.AreEqual("Board", board.PageName)
                Assert.IsFalse(board.Library.Contains(subject), "The manuscript is deleted.")
                Assert.AreEqual("cut: a question was just answered", board.PageFade.LastOutcome)
                Assert.IsFalse(board.PageFade.Showing)
                board.SetPrompt(Nothing)
            End Sub)

    End Sub


    ' A fade showing as the window is minimized is gone once the window is
    ' restored. Windows hides the overlay with the window, and would show
    ' it again as the window came back, after the window's Move and Resize
    ' had ended the fade: the picture of the page left would stay over the
    ' page shown, with no clock and no input filter left to hide it. The
    ' next switch fades as before.
    <TestMethod>
    Public Sub AFadeMinimizedWithTheWindowIsGoneOnceTheWindowIsRestored()

        OnBoard(
            Sub(board)
                UiMotion.FadeMilliseconds = 10000
                board.NavigateTo(Form1.WorkspacePage.Library)
                Assert.AreEqual("faded", board.PageFade.LastOutcome)
                Assert.IsTrue(board.PageFade.Showing, "The picture shows over the Library.")

                ShowWindow(board.Handle, SW_SHOWMINNOACTIVE)
                Application.DoEvents()
                Assert.AreEqual(FormWindowState.Minimized, board.WindowState, "The window is minimized.")
                Assert.IsFalse(board.PageFade.Showing, "Minimized, the picture is hidden.")
                Assert.IsFalse(board.PageFade.Fading, "Minimizing ended the fade.")

                ShowWindow(board.Handle, SW_SHOWNOACTIVATE)
                Application.DoEvents()
                Assert.AreEqual(FormWindowState.Normal, board.WindowState, "The window is restored.")
                Assert.IsFalse(board.PageFade.Showing, "Restored, the picture of the page left is not over the Library.")
                Assert.IsFalse(board.PageFade.Filtering, "Nothing listens for input.")

                Settle(board)
                board.NavigateTo(Form1.WorkspacePage.Board)
                Assert.AreEqual("faded", board.PageFade.LastOutcome, "The next switch fades.")
                Assert.IsTrue(board.PageFade.Showing, "The next switch's picture shows.")
                board.PageFade.End()
            End Sub)

    End Sub


    ' A window Windows scales for its screen switches as a cut. PaperRoute
    ' runs SystemAware, so on a monitor at another scale than the one at
    ' sign-in, or after the scale is changed, Windows stretches the window
    ' and gives it scaled coordinates, while the screen is read in its
    ' own: the picture would be of another area, at another size. The
    ' window is made in each DPI awareness: DPI-unaware, which Windows
    ' scales on any screen above 100%, and PaperRoute's own, which it
    ' scales on such a screen only. A window it does not scale fades. At
    ' 100% Windows scales no window, so there the unaware row proves only
    ' that, and says so, as the scaling sweep's cells not measured on a
    ' host do; the release candidate is built on a 125% screen, where it
    ' is a cut.
    <TestMethod>
    <DataRow("Unaware")>
    <DataRow("SystemAware")>
    Public Sub AWindowWindowsScalesSwitchesAsACut(awareness As String)

        Dim line As String = String.Empty
        Dim scaled As Boolean = False

        StaRunner.Run(
            Sub()
                Dim previous As IntPtr = SetThreadDpiAwarenessContext(If(awareness = "Unaware", DPI_AWARENESS_CONTEXT_UNAWARE, DPI_AWARENESS_CONTEXT_SYSTEM_AWARE))
                Try
                    UiMotion.EnabledOverride = True
                    UiTheme.HighContrastOverride = False
                    PageFade.AwaitPicture = True
                    UiMotion.FadeMilliseconds = 10000
                    Using board As New LibraryBoard(LargeLibraryFixture.Create(Path.Combine(_root, "library"), 60), Path.Combine(_root, "settings"))
                        board.Start()
                        board.WaitForIdleWork()
                        Dim windowDpi As Integer = GetDpiForWindow(board.Handle)
                        Dim screenDpi As Integer = MonitorDpi(board.Handle)
                        scaled = windowDpi <> screenDpi
                        board.NavigateTo(Form1.WorkspacePage.Library)
                        line = $"{awareness}: the window at {windowDpi} DPI (DeviceDpi {board.DeviceDpi}), its screen at {screenDpi}; {board.PageFade.LastOutcome}"
                        Console.WriteLine(line)
                        Assert.AreNotEqual(0, screenDpi, "Windows gives the screen's DPI. " & line)
                        If scaled Then
                            Assert.AreEqual("cut: Windows is scaling the window", board.PageFade.LastOutcome, line)
                            Assert.IsFalse(board.PageFade.Showing, line)
                        Else
                            Assert.AreEqual("faded", board.PageFade.LastOutcome, line)
                            board.PageFade.End()
                        End If
                        board.Close()
                    End Using
                Finally
                    ResetSeams()
                    SetThreadDpiAwarenessContext(previous)
                End Try
            End Sub)

        If awareness = "Unaware" AndAlso Not scaled Then
            Console.WriteLine("Not measured here: at 100% Windows scales no window.")
        End If

    End Sub


    ' Whether a window's DPI is its screen's, as the fade's scaling check
    ' asks it (PageFade.DpiIsTheScreens, #252), for a window made in each
    ' DPI awareness and asked on its own thread in that awareness. An
    ' unaware window is at 96 DPI on any screen, so on a screen above 100%
    ' its DPI is not its screen's: Windows stretches it. Windows tells an
    ' unaware thread 96 for that screen as well, so the answer is right
    ' only if the screen's DPI is asked as a per-monitor aware thread. A
    ' per-monitor aware window is at its screen's DPI on any screen.
    ' PaperRoute's own, SystemAware, is at the system DPI: its screen's
    ' unless the screen's scale has changed since sign-in, as on the first
    ' v0.9.2 candidate's host (system 96, screen 120). The test host is
    ' unaware, so a window at its own awareness is the unaware row's (a new
    ' thread starts at the process's awareness, and the line names it). At
    ' 100% an unaware window's DPI is its screen's, so there the unaware
    ' row proves only that, and says so. Each row also checks, at any
    ' scale, that the question puts the thread's awareness back: a UI
    ' thread left per-monitor aware would make every window it makes next
    ' per-monitor aware (the overlay, a dialog, a menu). The per-monitor
    ' aware row cannot tell; the other two can.
    <TestMethod>
    <DataRow("Unaware")>
    <DataRow("SystemAware")>
    <DataRow("PerMonitorAwareV2")>
    Public Sub AWindowsDpiIsTheScreensOnlyWhenWindowsDoesNotScaleIt(awareness As String)

        Dim line As String = String.Empty
        Dim measured As Boolean = True

        StaRunner.Run(
            Sub()
                Dim context As IntPtr =
                    If(awareness = "Unaware", DPI_AWARENESS_CONTEXT_UNAWARE,
                    If(awareness = "SystemAware", DPI_AWARENESS_CONTEXT_SYSTEM_AWARE, DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2))
                Dim previous As IntPtr = SetThreadDpiAwarenessContext(context)
                Assert.AreNotEqual(IntPtr.Zero, previous, "The thread is put in the " & awareness & " awareness.")
                Try
                    ' Made after the thread's awareness is set: a NativeWindow
                    ' makes its window in the awareness it was made in.
                    Dim window As New NativeWindow()
                    window.CreateHandle(New CreateParams With {.Style = PageFade.WS_POPUP, .X = 40, .Y = 40, .Width = 200, .Height = 100})
                    Try
                        Dim windowDpi As Integer = GetDpiForWindow(window.Handle)
                        Dim screenDpi As Integer = MonitorDpi(window.Handle)
                        Dim answer As Boolean = PageFade.DpiIsTheScreens(window.Handle)
                        Dim after As IntPtr = GetThreadDpiAwarenessContext()
                        line = $"{awareness} (the process {AwarenessName(previous)}): the window at {windowDpi} DPI, its screen at {screenDpi}; DpiIsTheScreens {answer}"
                        Console.WriteLine(line)
                        Assert.AreNotEqual(0, screenDpi, "Windows gives the screen's DPI. " & line)
                        Assert.IsTrue(AreDpiAwarenessContextsEqual(after, context), $"DpiIsTheScreens puts the thread's awareness back (the thread {AwarenessName(after)} after it). " & line)
                        Select Case awareness
                            Case "Unaware"
                                Assert.AreEqual(96, windowDpi, "An unaware window is at 96 DPI. " & line)
                                measured = screenDpi <> windowDpi
                                Assert.AreEqual(Not measured, answer, line)
                            Case "PerMonitorAwareV2"
                                Assert.AreEqual(screenDpi, windowDpi, "A per-monitor aware window is at its screen's DPI. " & line)
                                Assert.IsTrue(answer, line)
                            Case Else
                                Assert.AreEqual(windowDpi = screenDpi, answer, line)
                        End Select
                    Finally
                        window.DestroyHandle()
                    End Try
                Finally
                    SetThreadDpiAwarenessContext(previous)
                End Try
            End Sub)

        If Not measured Then
            Console.WriteLine("Not measured here: at 100% an unaware window's DPI is its screen's.")
        End If

    End Sub


    ' A switch begun while another of the window's thread's windows is over
    ' the page area, as a tooltip going as the switch begins, is a cut: the
    ' picture read off the screen would hold that window over the page
    ' shown. Once it has gone, the next switch fades. The fade is made
    ' longer than any switch here: at its own 150 ms, a switch on a busy
    ' host outlasted it and was a cut for that reason instead.
    <TestMethod>
    Public Sub AnotherWindowOverThePageAreaMakesTheSwitchACut()

        OnBoard(
            Sub(board)
                UiMotion.FadeMilliseconds = 10000
                board.NavigateTo(Form1.WorkspacePage.Library)
                board.PageFade.End()
                Pump(80)

                Dim area As Rectangle = board.ContentHost.RectangleToScreen(board.ContentHost.ClientRectangle)
                Dim cover As New NativeWindow()
                cover.CreateHandle(New CreateParams With {
                    .Style = PageFade.WS_POPUP Or WS_VISIBLE,
                    .ExStyle = PageFade.WS_EX_NOACTIVATE Or PageFade.WS_EX_TOOLWINDOW,
                    .X = area.X + 40, .Y = area.Y + 40, .Width = 120, .Height = 40,
                    .Parent = board.Handle
                })
                Try
                    board.NavigateTo(Form1.WorkspacePage.Board)
                    Assert.AreEqual("cut: another window is over the page area", board.PageFade.LastOutcome)
                    Assert.IsFalse(board.PageFade.Showing)
                Finally
                    cover.DestroyHandle()
                End Try

                Settle(board)
                board.NavigateTo(Form1.WorkspacePage.Library)
                Assert.AreEqual("faded", board.PageFade.LastOutcome)
                board.PageFade.End()
            End Sub)

    End Sub


    ' Whether the worker's read could hold the UI thread (#142, the plan's
    ' 4a): the read can wait on the screen, and the switch's own drawing
    ' (the rail's two buttons, painted as they are checked) might wait
    ' with it. On the 500 library, 50 switches with motion off and the same
    ' 50 with motion on and the pictures read for real, the two taking
    ' turns a page and back at a time, each switch followed by its paint
    ' and the same pause with the window processing its messages. Reported: each mode's times, and with motion on what
    ' showing the picture took, how long the reads took, how many waited
    ' 50 ms or more, and how many switches such a read held (the switch 50
    ' ms or more over the same switch's median with motion off). The test
    ' fails if more than two were held. Off the screen, where the window
    ' sits in every other test, a read of the screen has nothing to wait
    ' for; with PAPERROUTE_ON_SCREEN_TESTS=1 on an unlocked desktop the
    ' window is put on the screen, behind every other window, so the reads
    ' wait as a researcher's would.
    <TestMethod>
    Public Sub AReadInFlightHoldsNoSwitch()

        Dim lines As New List(Of String)()
        Dim held As Integer = 0
        Dim onScreen As Boolean = String.Equals(Environment.GetEnvironmentVariable("PAPERROUTE_ON_SCREEN_TESTS"), "1", StringComparison.Ordinal) AndAlso DesktopUnlocked()

        StaRunner.Run(
            Sub()
                Try
                    UiTheme.HighContrastOverride = False
                    PageFade.ScaledOverride = False
                    Using board As New LibraryBoard(LargeLibraryFixture.Create(Path.Combine(_root, "large"), 500), Path.Combine(_root, "settings"))
                        board.Start()
                        If onScreen Then
                            Dim area As Rectangle = Screen.PrimaryScreen.WorkingArea
                            board.Location = New Point(area.Left + 20, area.Top + 20)
                            SetWindowPos(board.Handle, HWND_BOTTOM, 0, 0, 0, 0, SWP_NOMOVE Or SWP_NOSIZE Or SWP_NOACTIVATE)
                        End If
                        lines.Add(If(onScreen, "On the screen, behind the other windows.", "Off the screen."))
                        board.WaitForBoardFill()
                        board.WaitForIdleWork()

                        Dim pages As Form1.WorkspacePage() = {Form1.WorkspacePage.Journals, Form1.WorkspacePage.Deadlines, Form1.WorkspacePage.Insights, Form1.WorkspacePage.ImportExport, Form1.WorkspacePage.Library}

                        ' Every switch, by where it went from and to, and how.
                        Dim switches As New List(Of (Key As String, Motion As Boolean, Took As Double, Read As Double, Place As Double, Begin As Double, Outcome As String))()

                        ' The two modes take turns, a page and back each, so both
                        ' meet the Board as its fill goes on, and the same host.
                        For round As Integer = 0 To 24
                            For Each motion As Boolean In {False, True}
                                UiMotion.EnabledOverride = motion
                                For Each page As Form1.WorkspacePage In {pages(round Mod pages.Length), Form1.WorkspacePage.Board}
                                    Dim key As String = board.PageName & " to " & page.ToString()
                                    Dim watch As Stopwatch = Stopwatch.StartNew()
                                    board.NavigateTo(page)
                                    Dim took As Double = watch.Elapsed.TotalMilliseconds
                                    Dim outcome As String = If(motion, board.PageFade.LastOutcome, String.Empty)
                                    Dim begin As Double = If(motion, board.PageFade.LastBeginMilliseconds, 0)
                                    board.PaintNow()

                                    ' The same pause after every switch, the fade's length
                                    ' and a little more, and any read let finish.
                                    Dim settle As Stopwatch = Stopwatch.StartNew()
                                    While settle.ElapsedMilliseconds < 250 OrElse (PageFade.PicturesInFlight > 0 AndAlso settle.ElapsedMilliseconds < 3000)
                                        Application.DoEvents()
                                        Thread.Sleep(1)
                                    End While
                                    If page = Form1.WorkspacePage.Insights Then board.WaitForInsights()

                                    switches.Add((key, motion, took, If(motion, PageFade.LastReadMilliseconds, 0), If(motion, PageFade.LastPlaceMilliseconds, 0), begin, outcome))
                                Next
                            Next
                        Next

                        For Each motion As Boolean In {False, True}
                            Dim mine = switches.Where(Function(item) item.Motion = motion).ToList()
                            Dim times As List(Of Double) = mine.Select(Function(item) item.Took).OrderBy(Function(value) value).ToList()
                            Dim stalls As Integer = times.Where(Function(value) value >= 50).Count()
                            Dim line As String = $"Motion {If(motion, "on", "off")}: {times.Count} switches, median {times(times.Count \ 2):0} ms, longest {times.Last():0} ms, {stalls} of 50 ms or more"
                            If motion Then
                                Dim begins As List(Of Double) = mine.Where(Function(item) item.Outcome = "faded").Select(Function(item) item.Begin).OrderBy(Function(value) value).ToList()
                                Dim reads As List(Of Double) = mine.Select(Function(item) item.Read).OrderBy(Function(value) value).ToList()
                                Dim places As List(Of Double) = mine.Select(Function(item) item.Place).OrderBy(Function(value) value).ToList()
                                Dim waited = mine.Where(Function(item) item.Read + item.Place >= 50).ToList()
                                For Each item In waited
                                    Dim usual As List(Of Double) = switches.Where(Function(other) Not other.Motion AndAlso other.Key = item.Key).Select(Function(other) other.Took).OrderBy(Function(value) value).ToList()
                                    If usual.Count > 0 AndAlso item.Took >= usual(usual.Count \ 2) + 50 Then held += 1
                                Next
                                line &= $"; outcomes {String.Join(", ", mine.GroupBy(Function(item) item.Outcome).Select(Function(group) group.Key & " " & group.Count().ToString()))}" &
                                        $"; showing the picture {If(begins.Count > 0, begins(begins.Count \ 2), 0):0.0} ms median, {If(begins.Count > 0, begins.Last(), 0):0.0} longest" &
                                        $"; reads {reads(reads.Count \ 2):0.0} ms median, {reads.Last():0.0} longest; putting the picture in the overlay {places(places.Count \ 2):0.0} ms median, {places.Last():0.0} longest" &
                                        $"; {waited.Count} pictures took 50 ms or more, {held} switches held by one"
                            End If
                            lines.Add(line)
                        Next

                        board.Close()
                    End Using
                Finally
                    ResetSeams()
                End Try
            End Sub,
            TimeSpan.FromMinutes(10))

        Console.WriteLine(String.Join(Environment.NewLine, lines))
        Assert.IsTrue(held <= 2, String.Join(Environment.NewLine, lines))

    End Sub


    ' The picture is the page left, as the screen showed it (#142, the
    ' plan's 4b): with the window on the screen, what the screen shows of
    ' the page area while the overlay is opaque, the new page painted
    ' under it, matches what it showed before the switch in at least 99%
    ' of its pixels, and once the fade has ended it does not. Only on an
    ' unlocked desktop, and only when PAPERROUTE_ON_SCREEN_TESTS is set,
    ' since the window is put on the screen for a moment; otherwise
    ' Inconclusive. CI and the release candidate leave OnScreen tests out;
    ' certification runs them by hand.
    <TestMethod>
    <TestCategory("OnScreen")>
    Public Sub ThePictureIsThePageLeft()

        If Not String.Equals(Environment.GetEnvironmentVariable("PAPERROUTE_ON_SCREEN_TESTS"), "1", StringComparison.Ordinal) Then
            Assert.Inconclusive("Set PAPERROUTE_ON_SCREEN_TESTS=1 on an unlocked desktop to put the test window on the screen.")
        End If

        If Not DesktopUnlocked() Then
            Assert.Inconclusive("The desktop is locked: the screen shows nothing to compare.")
        End If

        Dim lines As New List(Of String)()

        OnBoard(
            Sub(board)
                UiMotion.FadeMilliseconds = 10000
                Dim area As Rectangle = Screen.PrimaryScreen.WorkingArea
                board.Location = New Point(area.Left + 20, area.Top + 20)
                ' Above the other windows, as it would be in use, never activated.
                SetWindowPos(board.Handle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE Or SWP_NOSIZE Or SWP_NOACTIVATE)
                Application.DoEvents()
                board.NavigateTo(Form1.WorkspacePage.Journals)
                Application.DoEvents()
                board.PageFade.End()
                board.NavigateTo(Form1.WorkspacePage.Board)
                Application.DoEvents()
                board.PageFade.End()
                Pump(300)

                For Each page As Form1.WorkspacePage In {Form1.WorkspacePage.Journals, Form1.WorkspacePage.Board, Form1.WorkspacePage.ImportExport, Form1.WorkspacePage.Board}
                    Dim screenArea As Rectangle = board.ContentHost.RectangleToScreen(board.ContentHost.ClientRectangle)
                    Using before As Bitmap = FromScreen(screenArea)
                        board.NavigateTo(page)
                        Assert.AreEqual("faded", board.PageFade.LastOutcome)
                        board.PaintNow()
                        Thread.Sleep(100)
                        Using during As Bitmap = FromScreen(screenArea)
                            board.PageFade.End()
                            Pump(300)
                            Using after As Bitmap = FromScreen(screenArea)
                                Dim kept As Double = Matching(before, during)
                                Dim changed As Double = Matching(before, after)
                                lines.Add($"To {page}: {kept:P1} of the page area matched the page left under the opaque overlay, {changed:P1} once the fade ended")
                                ' A screen that shows the same before and after a
                                ' switch is not being drawn: a display asleep.
                                If changed >= 0.99 Then
                                    Assert.Inconclusive("The screen did not change with the page (the display may be off): " & lines.Last())
                                End If
                                Assert.IsTrue(kept >= 0.99, lines.Last())
                            End Using
                        End Using
                    End Using
                Next
            End Sub)

        Console.WriteLine(String.Join(Environment.NewLine, lines))

    End Sub


    ' ---------------------------------------------------------------
    ' Helpers
    ' ---------------------------------------------------------------

    Private Shared Sub Pump(milliseconds As Integer)
        Dim watch As Stopwatch = Stopwatch.StartNew()
        Do
            Application.DoEvents()
            Thread.Sleep(1)
        Loop While watch.ElapsedMilliseconds < milliseconds
    End Sub


    ' A switch begun while the screen may still show a fade is a cut: the
    ' fade still showing is let end, and then a few of Windows' frames go.
    Private Shared Sub Settle(board As LibraryBoard)
        Dim watch As Stopwatch = Stopwatch.StartNew()
        While board.PageFade IsNot Nothing AndAlso board.PageFade.Fading AndAlso watch.ElapsedMilliseconds < 2000
            Application.DoEvents()
            Thread.Sleep(1)
        End While
        Pump(80)
    End Sub


    ' What the screen shows of an area, layered windows such as the overlay
    ' included (CAPTUREBLT, which CopyFromScreen does not take).
    Private Shared Function FromScreen(area As Rectangle) As Bitmap
        Dim picture As New Bitmap(area.Width, area.Height)
        Using graphics As Graphics = Graphics.FromImage(picture)
            Dim target As IntPtr = graphics.GetHdc()
            Dim screen As IntPtr = GetDC(IntPtr.Zero)
            Try
                BitBlt(target, 0, 0, area.Width, area.Height, screen, area.X, area.Y, SRCCOPY Or CAPTUREBLT)
            Finally
                ReleaseDC(IntPtr.Zero, screen)
                graphics.ReleaseHdc(target)
            End Try
        End Using
        Return picture
    End Function


    ' The share of pixels two pictures of one size have alike.
    Private Shared Function Matching(first As Bitmap, second As Bitmap) As Double
        Dim same As Long = 0
        For y As Integer = 0 To first.Height - 1 Step 2
            For x As Integer = 0 To first.Width - 1 Step 2
                If first.GetPixel(x, y).ToArgb() = second.GetPixel(x, y).ToArgb() Then same += 1
            Next
        Next
        Return same / CDbl(((first.Width + 1) \ 2) * ((first.Height + 1) \ 2))
    End Function


    ' The DPI of the screen a window is on, as Windows has it, asked in
    ' the per-monitor awareness that is told it; 0 if Windows does not say.
    Private Shared Function MonitorDpi(window As IntPtr) As Integer
        Dim previous As IntPtr = SetThreadDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2)
        Try
            Dim x As UInteger = 0
            Dim y As UInteger = 0
            If GetDpiForMonitor(MonitorFromWindow(window, MONITOR_DEFAULTTONEAREST), MDT_EFFECTIVE_DPI, x, y) <> 0 Then Return 0
            Return CInt(x)
        Finally
            SetThreadDpiAwarenessContext(previous)
        End Try
    End Function


    ' An awareness context's awareness, by the name the tests give it.
    Private Shared Function AwarenessName(context As IntPtr) As String
        Select Case GetAwarenessFromDpiAwarenessContext(context)
            Case 0
                Return "Unaware"
            Case 1
                Return "SystemAware"
            Case 2
                Return "PerMonitorAware"
            Case Else
                Return "of an awareness Windows does not name"
        End Select
    End Function


    Private Shared Function DesktopUnlocked() As Boolean
        Dim desktop As IntPtr = OpenInputDesktop(0, False, DESKTOP_SWITCHDESKTOP)
        If desktop = IntPtr.Zero Then Return False
        Try
            Return SwitchDesktop(desktop)
        Finally
            CloseDesktop(desktop)
        End Try
    End Function


    ' What UI Automation told a client of the overlay and the window.
    Private NotInheritable Class UiaReading
        Public IsControlElement As Object
        Public IsContentElement As Object
        Public IsKeyboardFocusable As Object
        ' The window of the element the control view gives for the
        ' overlay's (itself if it is in that view, else the nearest one
        ' above it that is), and of its parent in the raw view.
        Public ControlViewHandle As IntPtr
        Public RawParentHandle As IntPtr
        ' The window's children in each view, by window and name.
        Public ControlChildren As New List(Of String)()
        Public RawChildren As New List(Of String)()
        ' Who described the overlay's element: its own provider, or, for a
        ' window that did not answer in time, Windows' MSAA proxy.
        Public Provider As String = String.Empty
        ' The questions before this one that found the window not shown.
        Public Missed As New List(Of String)()

        Public Overrides Function ToString() As String
            Return $"control element {IsControlElement}, content element {IsContentElement}, keyboard focusable {IsKeyboardFocusable}; " &
                   $"the control view gives {ControlViewHandle:X}, its raw parent {RawParentHandle:X}; " &
                   $"control children {String.Join(", ", ControlChildren)}; raw children {String.Join(", ", RawChildren)}; " &
                   $"described by {Provider}" &
                   If(Missed.Count = 0, String.Empty, $"; asked again after: {String.Join(" | ", Missed)}")
        End Function
    End Class


    ' Asks UI Automation as AskUiAutomation does, about a window Windows
    ' shows. A window Windows does not show at that moment, cloaked by the
    ' compositor (as the shell cloaks a window on another of its desktops)
    ' or without WS_VISIBLE, is in none of UI Automation's views, nor are
    ' its own child windows, and a window it owns is then the desktop's.
    ' Once in 22 runs on a busy host the overlay's raw parent was the
    ' desktop and the window's raw children held neither its own child
    ' window nor the overlay, as the window cloaked or without WS_VISIBLE
    ' on purpose reads every time (PaperRoute does neither to it: its
    ' RedrawHold holds parts of it, never the form). So a question that
    ' finds the window's own child windows missing from its raw view is
    ' asked again once Windows shows the window, three questions at most,
    ' each one missed kept with the reading; a window Windows does not
    ' show again within 30 s leaves the test Inconclusive.
    Private Shared Function AskUiAutomationOfShownWindow(window As Control, overlay As IntPtr) As UiaReading

        ' The child windows shown with the window (WS_VISIBLE their own,
        ' whatever the window's).
        Dim own As List(Of String) = window.Controls.Cast(Of Control)().
            Where(Function(child) child.IsHandleCreated AndAlso (GetWindowLong(child.Handle, GWL_STYLE) And WS_VISIBLE) <> 0).
            Select(Function(child) child.Handle.ToString("X") & " ").ToList()
        Dim missed As New List(Of String)()
        Dim reading As UiaReading = Nothing

        For question As Integer = 1 To 3
            reading = AskUiAutomation(window.Handle, overlay)
            If own.All(Function(key) reading.RawChildren.Any(Function(child) child.StartsWith(key, StringComparison.Ordinal))) Then
                Exit For
            End If
            missed.Add($"question {question} did not find the window's own child windows ({reading}); Windows then said visible {IsWindowVisible(window.Handle)}, cloaked {Cloaked(window.Handle)}")
            Dim waited As Stopwatch = Stopwatch.StartNew()
            While Not Shown(window.Handle) AndAlso waited.ElapsedMilliseconds < 30000
                Application.DoEvents()
                Thread.Sleep(10)
            End While
            If Not Shown(window.Handle) Then
                Assert.Inconclusive("Windows did not show the test window again within 30 s: " & String.Join(" | ", missed))
            End If
        Next

        reading.Missed = missed
        Return reading

    End Function


    Private Shared Function Shown(window As IntPtr) As Boolean
        Return IsWindowVisible(window) AndAlso Cloaked(window) = 0
    End Function


    ' How the compositor cloaks a window: 0 not at all.
    Private Shared Function Cloaked(window As IntPtr) As Integer
        Dim value As Integer = 0
        If DwmGetWindowAttribute(window, DWMWA_CLOAKED, value, Marshal.SizeOf(Of Integer)()) <> 0 Then Return 0
        Return value
    End Function


    ' Asks UI Automation, as a screen reader does from its own process,
    ' from a worker thread of the multithreaded apartment while this thread
    ' goes on processing its messages, since UI Automation asks the
    ' window's own thread (WM_GETOBJECT, and the overlay's provider and the
    ' window's are called there).
    Private Shared Function AskUiAutomation(window As IntPtr, overlay As IntPtr, Optional limit As Integer = 20000) As UiaReading

        Dim reading As New UiaReading()
        Dim failure As Exception = Nothing

        Dim worker As Task = Task.Run(
            Sub()
                Try
                    Dim automation As IUIAutomationClient = DirectCast(Activator.CreateInstance(Type.GetTypeFromCLSID(New Guid("ff48dba4-60ef-4201-aa87-54103eef594e"))), IUIAutomationClient)
                    Dim controlView As IUIAutomationTreeWalkerClient = automation.ControlViewWalker
                    Dim rawView As IUIAutomationTreeWalkerClient = automation.RawViewWalker

                    If overlay <> IntPtr.Zero Then
                        Dim element As IUIAutomationElementClient = automation.ElementFromHandle(overlay)
                        reading.IsControlElement = element.GetCurrentPropertyValue(PageFade.UIA_IsControlElementPropertyId)
                        reading.IsContentElement = element.GetCurrentPropertyValue(PageFade.UIA_IsContentElementPropertyId)
                        reading.IsKeyboardFocusable = element.GetCurrentPropertyValue(PageFade.UIA_IsKeyboardFocusablePropertyId)
                        reading.Provider = TryCast(element.GetCurrentPropertyValue(UIA_ProviderDescriptionPropertyId), String)
                        reading.ControlViewHandle = HandleOf(controlView.NormalizeElement(element))
                        reading.RawParentHandle = HandleOf(rawView.GetParentElement(element))
                    End If

                    Dim root As IUIAutomationElementClient = automation.ElementFromHandle(window)
                    reading.ControlChildren = ChildrenOf(controlView, root)
                    reading.RawChildren = ChildrenOf(rawView, root)
                Catch ex As Exception
                    failure = ex
                End Try
            End Sub)

        Dim waited As Stopwatch = Stopwatch.StartNew()
        While Not worker.IsCompleted AndAlso waited.ElapsedMilliseconds < limit
            Application.DoEvents()
            Thread.Sleep(1)
        End While

        Assert.IsTrue(worker.IsCompleted, $"UI Automation answered within {limit \ 1000} s.")
        If failure IsNot Nothing Then Assert.Fail("UI Automation: " & failure.ToString())
        Return reading

    End Function


    Private Shared Function HandleOf(element As IUIAutomationElementClient) As IntPtr
        If element Is Nothing Then Return IntPtr.Zero
        Dim value As Object = element.GetCurrentPropertyValue(UIA_NativeWindowHandlePropertyId)
        Return If(TypeOf value Is Integer, New IntPtr(CInt(value)), IntPtr.Zero)
    End Function


    Private Shared Function ChildrenOf(walker As IUIAutomationTreeWalkerClient, parent As IUIAutomationElementClient) As List(Of String)
        Dim found As New List(Of String)()
        Dim child As IUIAutomationElementClient = walker.GetFirstChildElement(parent)
        While child IsNot Nothing AndAlso found.Count < 100
            found.Add(HandleOf(child).ToString("X") & " '" & TryCast(child.GetCurrentPropertyValue(UIA_NamePropertyId), String) & "'")
            child = walker.GetNextSiblingElement(child)
        End While
        Return found
    End Function


    Private Const UIA_NamePropertyId As Integer = 30005
    Private Const UIA_NativeWindowHandlePropertyId As Integer = 30020
    Private Const UIA_ProviderDescriptionPropertyId As Integer = 30107


    ' The thread's top-level windows made as the fade's overlay is: layered,
    ' never activated, disabled. (A tooltip is layered too, but neither.)
    Private Shared Function OverlaysOfThisThread() As List(Of IntPtr)
        Dim found As New List(Of IntPtr)()
        Dim wanted As Integer = PageFade.WS_EX_LAYERED Or PageFade.WS_EX_NOACTIVATE
        EnumThreadWindows(GetCurrentThreadId(),
            Function(window, parameter)
                If (GetWindowLong(window, GWL_EXSTYLE) And wanted) = wanted AndAlso (GetWindowLong(window, GWL_STYLE) And PageFade.WS_DISABLED) <> 0 Then found.Add(window)
                Return True
            End Function,
            IntPtr.Zero)
        Return found
    End Function


    Private Shared Function WindowBounds(window As IntPtr) As Rectangle
        Dim rect As NativeRect
        GetWindowRect(window, rect)
        Return Rectangle.FromLTRB(rect.Left, rect.Top, rect.Right, rect.Bottom)
    End Function


    ' A window by its class, and its process when another's.
    Private Shared Function WindowName(window As IntPtr) As String
        Dim name As New Text.StringBuilder(256)
        GetClassName(window, name, name.Capacity)
        Dim process As Integer = 0
        GetWindowThreadProcessId(window, process)
        Return "a window of class " & name.ToString() & If(process = Environment.ProcessId, String.Empty, " in process " & process.ToString())
    End Function


    Private Shared Function MousePoint(at As Point) As IntPtr
        Return New IntPtr((at.Y << 16) Or (at.X And &HFFFF))
    End Function


    Private Const GWL_STYLE As Integer = -16
    Private Const GWL_EXSTYLE As Integer = -20
    Private Const WS_VISIBLE As Integer = &H10000000
    Private Const GW_OWNER As UInteger = 4
    Private Const WM_KEYDOWN As UInteger = &H100
    Private Const WM_CHAR As UInteger = &H102
    Private Const WM_LBUTTONDOWN As UInteger = &H201
    Private Const WM_LBUTTONUP As UInteger = &H202
    Private Const WM_NCHITTEST As UInteger = &H84
    Private Const HTTRANSPARENT As Integer = -1
    Private Const MK_LBUTTON As Integer = &H1
    Private Const DESKTOP_SWITCHDESKTOP As UInteger = &H100
    Private Shared ReadOnly HWND_TOPMOST As New IntPtr(-1)
    Private Shared ReadOnly HWND_BOTTOM As New IntPtr(1)
    Private Const SWP_NOSIZE As UInteger = &H1
    Private Const SWP_NOMOVE As UInteger = &H2
    Private Const SWP_NOACTIVATE As UInteger = &H10

    <DllImport("user32.dll")>
    Private Shared Function SetWindowPos(window As IntPtr, after As IntPtr, x As Integer, y As Integer, width As Integer, height As Integer, flags As UInteger) As Boolean
    End Function

    Private Const SW_SHOWNOACTIVATE As Integer = 4
    Private Const SW_SHOWMINNOACTIVE As Integer = 7

    <DllImport("user32.dll")>
    Private Shared Function ShowWindow(window As IntPtr, command As Integer) As Boolean
    End Function

    Private Shared ReadOnly DPI_AWARENESS_CONTEXT_UNAWARE As New IntPtr(-1)
    Private Shared ReadOnly DPI_AWARENESS_CONTEXT_SYSTEM_AWARE As New IntPtr(-2)
    Private Shared ReadOnly DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 As New IntPtr(-4)
    Private Const MONITOR_DEFAULTTONEAREST As UInteger = 2
    Private Const MDT_EFFECTIVE_DPI As Integer = 0

    <DllImport("user32.dll")>
    Private Shared Function SetThreadDpiAwarenessContext(context As IntPtr) As IntPtr
    End Function

    <DllImport("user32.dll")>
    Private Shared Function GetDpiForWindow(window As IntPtr) As Integer
    End Function

    <DllImport("user32.dll")>
    Private Shared Function GetAwarenessFromDpiAwarenessContext(context As IntPtr) As Integer
    End Function

    <DllImport("user32.dll")>
    Private Shared Function GetThreadDpiAwarenessContext() As IntPtr
    End Function

    <DllImport("user32.dll")>
    Private Shared Function AreDpiAwarenessContextsEqual(first As IntPtr, second As IntPtr) As Boolean
    End Function

    <DllImport("user32.dll")>
    Private Shared Function MonitorFromWindow(window As IntPtr, flags As UInteger) As IntPtr
    End Function

    <DllImport("shcore.dll")>
    Private Shared Function GetDpiForMonitor(monitor As IntPtr, kind As Integer, ByRef x As UInteger, ByRef y As UInteger) As Integer
    End Function

    Private Const SRCCOPY As Integer = &HCC0020
    Private Const CAPTUREBLT As Integer = &H40000000

    <DllImport("user32.dll")>
    Private Shared Function GetDC(window As IntPtr) As IntPtr
    End Function

    <DllImport("user32.dll")>
    Private Shared Function ReleaseDC(window As IntPtr, dc As IntPtr) As Integer
    End Function

    <DllImport("gdi32.dll")>
    Private Shared Function BitBlt(target As IntPtr, x As Integer, y As Integer, width As Integer, height As Integer, source As IntPtr, sourceX As Integer, sourceY As Integer, operation As Integer) As Boolean
    End Function

    <StructLayout(LayoutKind.Sequential)>
    Private Structure NativeRect
        Public Left As Integer
        Public Top As Integer
        Public Right As Integer
        Public Bottom As Integer
    End Structure

    Private Delegate Function EnumWindowsProc(window As IntPtr, parameter As IntPtr) As Boolean

    <DllImport("user32.dll")>
    Private Shared Function EnumThreadWindows(threadId As Integer, callback As EnumWindowsProc, parameter As IntPtr) As Boolean
    End Function

    <DllImport("kernel32.dll")>
    Private Shared Function GetCurrentThreadId() As Integer
    End Function

    <DllImport("user32.dll")>
    Private Shared Function GetWindowLong(window As IntPtr, index As Integer) As Integer
    End Function

    <DllImport("user32.dll")>
    Private Shared Function GetWindow(window As IntPtr, command As UInteger) As IntPtr
    End Function

    <DllImport("user32.dll")>
    Private Shared Function GetWindowRect(window As IntPtr, ByRef rect As NativeRect) As Boolean
    End Function

    <DllImport("user32.dll")>
    Private Shared Function IsWindowEnabled(window As IntPtr) As Boolean
    End Function

    <DllImport("user32.dll")>
    Private Shared Function IsWindowVisible(window As IntPtr) As Boolean
    End Function

    Private Const DWMWA_CLOAKED As Integer = 14

    <DllImport("dwmapi.dll")>
    Private Shared Function DwmGetWindowAttribute(window As IntPtr, attribute As Integer, ByRef value As Integer, size As Integer) As Integer
    End Function

    <DllImport("user32.dll")>
    Private Shared Function GetFocus() As IntPtr
    End Function

    <DllImport("user32.dll")>
    Private Shared Function GetActiveWindow() As IntPtr
    End Function

    <DllImport("user32.dll")>
    Private Shared Function PostMessage(window As IntPtr, message As UInteger, wParam As IntPtr, lParam As IntPtr) As Boolean
    End Function

    <DllImport("user32.dll")>
    Private Shared Function SendMessage(window As IntPtr, message As UInteger, wParam As IntPtr, lParam As IntPtr) As IntPtr
    End Function

    <DllImport("user32.dll")>
    Private Shared Function WindowFromPoint(point As Point) As IntPtr
    End Function

    <DllImport("user32.dll", CharSet:=CharSet.Unicode)>
    Private Shared Function GetClassName(window As IntPtr, name As Text.StringBuilder, capacity As Integer) As Integer
    End Function

    <DllImport("user32.dll")>
    Private Shared Function GetWindowThreadProcessId(window As IntPtr, ByRef process As Integer) As Integer
    End Function

    <DllImport("user32.dll")>
    Private Shared Function OpenInputDesktop(flags As UInteger, inherit As Boolean, access As UInteger) As IntPtr
    End Function

    <DllImport("user32.dll")>
    Private Shared Function SwitchDesktop(desktop As IntPtr) As Boolean
    End Function

    <DllImport("user32.dll")>
    Private Shared Function CloseDesktop(desktop As IntPtr) As Boolean
    End Function

End Class


' UI Automation's client interfaces, as UIAutomationClient declares them
' (CUIAutomation): only the members the tests call are named; each other
' member before them holds its place in the interface.
<ComImport>
<Guid("30cbe57d-d9d0-452a-ab13-7ac5ac4825ee")>
<InterfaceType(ComInterfaceType.InterfaceIsIUnknown)>
Friend Interface IUIAutomationClient
    Sub CompareElements()
    Sub CompareRuntimeIds()
    Sub GetRootElement()
    Function ElementFromHandle(window As IntPtr) As IUIAutomationElementClient
    Sub ElementFromPoint()
    Sub GetFocusedElement()
    Sub GetRootElementBuildCache()
    Sub ElementFromHandleBuildCache()
    Sub ElementFromPointBuildCache()
    Sub GetFocusedElementBuildCache()
    Sub CreateTreeWalker()
    ReadOnly Property ControlViewWalker As IUIAutomationTreeWalkerClient
    ReadOnly Property ContentViewWalker As IUIAutomationTreeWalkerClient
    ReadOnly Property RawViewWalker As IUIAutomationTreeWalkerClient
End Interface


<ComImport>
<Guid("d22108aa-8ac5-49a5-837b-37bbb3d7591e")>
<InterfaceType(ComInterfaceType.InterfaceIsIUnknown)>
Friend Interface IUIAutomationElementClient
    Sub SetFocus()
    Sub GetRuntimeId()
    Sub FindFirst()
    Sub FindAll()
    Sub FindFirstBuildCache()
    Sub FindAllBuildCache()
    Sub BuildUpdatedCache()
    Function GetCurrentPropertyValue(propertyId As Integer) As Object
End Interface


<ComImport>
<Guid("4042c624-389c-4afc-a630-9df854a541fc")>
<InterfaceType(ComInterfaceType.InterfaceIsIUnknown)>
Friend Interface IUIAutomationTreeWalkerClient
    Function GetParentElement(element As IUIAutomationElementClient) As IUIAutomationElementClient
    Function GetFirstChildElement(element As IUIAutomationElementClient) As IUIAutomationElementClient
    Function GetLastChildElement(element As IUIAutomationElementClient) As IUIAutomationElementClient
    Function GetNextSiblingElement(element As IUIAutomationElementClient) As IUIAutomationElementClient
    Function GetPreviousSiblingElement(element As IUIAutomationElementClient) As IUIAutomationElementClient
    Function NormalizeElement(element As IUIAutomationElementClient) As IUIAutomationElementClient
End Interface
