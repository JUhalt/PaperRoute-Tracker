Imports System
Imports System.Diagnostics
Imports System.Drawing
Imports System.Runtime.InteropServices
Imports System.Threading
Imports System.Windows.Forms
Imports ManuscriptPipeline.Services

Namespace Controls

    ' A page switch's cross-fade (#142). As a switch begins, a worker
    ' reads the page area off the screen, as it shows it, and puts the
    ' picture in a window of its own, hidden over the page area; neither
    ' is done on the UI thread, since on this machine either has waited on
    ' the screen for half a second. Once the switch has returned, unless
    ' it is a cut whatever the picture does, it waits at most a moment for
    ' the picture (PictureWaitMilliseconds, #268), the page left still on
    ' the screen, and that window, if the picture is in it, is shown and
    ' faded out over UiMotion.FadeMilliseconds on UiMotion's Windows Forms
    ' clock. A picture not ready by then, a switch that took the fade's
    ' length or longer, a switch just after a fade or with another of the
    ' thread's windows over the page area, a window Windows scales for its
    ' screen, or motion off, is a cut, as before.
    '
    ' The window never takes part: disabled, never activated, transparent
    ' to the pointer, and in no view a screen reader reads (its UI
    ' Automation element is in the raw view only); it is not a control, so
    ' nothing in the window's tree, its Tab order or its focus changes. The
    ' first key, click or wheel ends the fade there and then and goes on
    ' to the page; so do the next switch and the window moving, sizing,
    ' hiding or changing colors. Made on the first switch that could fade,
    ' it is kept hidden between fades and destroyed with the window.
    '
    ' A switch raises the focus event of the control it gives the keyboard
    ' as it moves it, before the picture shows over that control; and
    ' Narrator, which reads the event once the window's thread answers it,
    ' after the switch, read nothing of the page shown while the picture
    ' covered it (#276). So once the fade ends on its own, at an input or
    ' for the page to show more, with nothing over the page, the focus
    ' event of the control in the page area with the keyboard, if it took
    ' it since the switch began, is raised again (Finish). Nothing waits
    ' for it.
    Friend NotInheritable Class PageFade
        Implements IMessageFilter

        Private ReadOnly _owner As Form
        Private ReadOnly _area As Control
        Private ReadOnly _step As Func(Of Boolean)
        Private ReadOnly _watch As New Stopwatch()
        Private _overlay As Overlay = Nothing
        Private _picture As Picture = Nothing
        Private _filtering As Boolean = False
        Private _lastHidden As Long = 0
        Private Shared _inFlight As Integer = 0

        ' The layered size last given to the overlay on the window's thread
        ' (GiveLayeredSize); none for an overlay just made.
        Private _layeredSize As Size = Size.Empty

        ' Windows' focus as the switch began (#276).
        Private _focusBefore As IntPtr = IntPtr.Zero

        ' How long the screen may go on showing a fade once it is hidden: a
        ' few of Windows' frames.
        Private Const SettleMilliseconds As Double = 50

        ' How long a switch, once it has returned, waits for the picture
        ' before it is a cut: about 60 ms, the maintainer's choice on
        ' November 2, 2026 (#268). On a laptop at 125% the screen took 20 to
        ' 76 ms to read and a switch 7 to 27, so a switch that waited for
        ' nothing was a cut every time. Nothing paints until the switch has
        ' returned, so the page left stays on the screen while it waits. A
        ' picture ready within it shows there and then; one not ready is a
        ' cut once Windows ends the wait, on a tick of its timer (15.6 ms),
        ' so 60 to about 75 ms in.
        Friend Const PictureWaitMilliseconds As Integer = 60

        ' Set by the worker as each read ends, so that the switch waiting
        ' for its picture goes on at once: waited for in sleeps of a
        ' millisecond, each lasting to a tick of Windows' timer, a picture
        ' was seen up to 15.6 ms after it was ready (#268).
        Private Shared ReadOnly _pictureDone As New AutoResetEvent(False)

        ' Test seams: how long the worker waits before it reads, and whether
        ' Begin waits for the picture until it is ready, up to 2 s, in place
        ' of PictureWaitMilliseconds (functional tests only; the budget
        ' never does).
        Friend Shared CaptureDelayMilliseconds As Integer = 0
        Friend Shared AwaitPicture As Boolean = False

        ' Test seam: whether Windows scales the window, in place of what
        ' Windows says (Nothing takes Windows' answer; it is asked either
        ' way, so a timed switch pays for asking). The tests' host is
        ' DPI-unaware, so on a screen above 100% Windows scales every window
        ' a test makes; a test of what a fade does says it does not.
        Friend Shared ScaledOverride As Boolean? = Nothing

        ' Test seam: the DPI of the window's screen, in place of what Windows
        ' says (Nothing takes Windows' answer; it is asked either way). A
        ' window at the system DPI on a screen at another, as on the first
        ' v0.9.2 candidate's host (#252), cannot be made on demand, so a
        ' test gives the window's screen another DPI than the window's.
        Friend Shared ScreenDpiOverride As Integer? = Nothing


        Public Sub New(owner As Form, area As Control)

            _owner = owner
            _area = area
            _step = AddressOf StepOnce

            AddHandler owner.Move, AddressOf OwnerChanged
            AddHandler owner.Resize, AddressOf OwnerChanged
            AddHandler owner.Deactivate, AddressOf OwnerChanged
            AddHandler owner.VisibleChanged, AddressOf OwnerChanged
            AddHandler owner.DpiChanged, AddressOf OwnerChanged
            AddHandler owner.SystemColorsChanged, AddressOf OwnerChanged
            AddHandler owner.HandleDestroyed, AddressOf OwnerDestroyed

        End Sub


        ' ---------------------------------------------------------------
        ' What the last switch did, for the tests and the budget rows
        ' ---------------------------------------------------------------

        Friend Property LastOutcome As String = String.Empty
        Friend Property LastBeginMilliseconds As Double = 0
        Friend Property FadesShown As Integer = 0
        Friend Property OverlaysMade As Integer = 0
        Friend Property LayeredSizesGiven As Integer = 0

        ' How long giving the overlay its layered size took on the window's
        ' thread in the last switch, within the switch's time (Capture); 0
        ' if it was not given.
        Friend Property LastSizeMilliseconds As Double = 0

        Friend ReadOnly Property Fading As Boolean
            Get
                Return _watch.IsRunning
            End Get
        End Property

        Friend ReadOnly Property Filtering As Boolean
            Get
                Return _filtering
            End Get
        End Property

        Friend ReadOnly Property OverlayHandle As IntPtr
            Get
                Return If(_overlay Is Nothing, IntPtr.Zero, _overlay.Handle)
            End Get
        End Property

        Friend ReadOnly Property Showing As Boolean
            Get
                Return OverlayHandle <> IntPtr.Zero AndAlso IsWindowVisible(OverlayHandle)
            End Get
        End Property

        Friend Shared ReadOnly Property PicturesInFlight As Integer
            Get
                Return Volatile.Read(_inFlight)
            End Get
        End Property

        ' How long the last read, and putting its picture in the overlay,
        ' took on the worker, ready in time or not.
        Private Shared _lastReadTicks As Long = 0
        Private Shared _lastPlaceTicks As Long = 0

        Friend Shared ReadOnly Property LastReadMilliseconds As Double
            Get
                Return Volatile.Read(_lastReadTicks) * 1000.0 / Stopwatch.Frequency
            End Get
        End Property

        Friend Shared ReadOnly Property LastPlaceMilliseconds As Double
            Get
                Return Volatile.Read(_lastPlaceTicks) * 1000.0 / Stopwatch.Frequency
            End Get
        End Property

        ' The DPI awareness the worker's thread last read the screen in: the
        ' overlay's, for the test of it (#268).
        Friend Shared Property LastReadAwareness As IntPtr = IntPtr.Zero


        ' ---------------------------------------------------------------
        ' A switch
        ' ---------------------------------------------------------------

        ' As a switch begins, once any question about the page left has been
        ' answered: a fade still showing ends, and unless motion is off, the
        ' window cannot show a fade, or a picture is still being read, a
        ' worker starts reading the page area. Returns at once.
        Friend Sub Capture(allowed As Boolean, Optional reason As String = Nothing)

            [End]()
            Abandon()
            LastBeginMilliseconds = 0
            LastSizeMilliseconds = 0
            _focusBefore = GetFocus()

            If Not allowed Then
                LastOutcome = "cut: " & If(reason, "not faded")
                Return
            End If

            If Not UiMotion.Enabled Then
                LastOutcome = "cut: motion is off"
                Return
            End If

            If Not CanShow() Then
                LastOutcome = "cut: the window shows no page area"
                Return
            End If

            If PicturesInFlight > 0 Then
                LastOutcome = "cut: a picture was still being read"
                Return
            End If

            ' A fade that has only just been hidden may still be on the
            ' screen, and in the picture.
            If _lastHidden <> 0 AndAlso (Stopwatch.GetTimestamp() - _lastHidden) * 1000.0 / Stopwatch.Frequency < SettleMilliseconds Then
                LastOutcome = "cut: the last fade had only just ended"
                Return
            End If

            Dim screen As Rectangle = _area.RectangleToScreen(_area.ClientRectangle)

            ' So may another of the thread's windows over the page area, such
            ' as a tooltip going as the switch begins.
            If AnotherWindowOver(screen) Then
                LastOutcome = "cut: another window is over the page area"
                Return
            End If

            ' A window Windows scales for a screen of another DPI than the one
            ' it was made for (a scale changed since sign-in, a second
            ' monitor) is placed in scaled coordinates, while the screen is
            ' read in its own: the picture would be of another area, at
            ' another size. Windows is asked even when a test answers.
            Dim scaled As Boolean = ScaledByWindows(screen)

            If If(ScaledOverride, scaled) Then
                LastOutcome = "cut: Windows is scaling the window"
                Return
            End If

            ' The overlay is made, placed over the page area and given its
            ' layered size on the window's thread, hidden: the worker puts
            ' the picture in it at the place and size it has already, which
            ' Windows does without this thread. A place or size it has not
            ' would have Windows ask this thread, busy with the switch, about
            ' the move (35 to 150 ms when tried for #268). A window Windows
            ' cannot make (out of window handles), or cannot size, is a cut:
            ' the fade never stops the switch.
            Try

                If _overlay Is Nothing OrElse _overlay.Handle = IntPtr.Zero Then
                    _overlay = New Overlay(_owner)
                    _layeredSize = Size.Empty
                    OverlaysMade += 1
                End If

                Dim picture As New Picture With {.Screen = screen}
                Dim overlay As IntPtr = _overlay.Handle

                If WindowBounds(overlay) <> screen Then
                    SetWindowPos(overlay, IntPtr.Zero, screen.X, screen.Y, screen.Width, screen.Height, SWP_NOZORDER Or SWP_NOOWNERZORDER Or SWP_NOACTIVATE Or SWP_DEFERERASE Or SWP_NOREDRAW)
                End If

                ' Windows shows a picture put in a layered window only at a
                ' layered size given it, which SetWindowPos does not give:
                ' put in with no size, as until #268, every picture was taken
                ' and none shown, so no switch had faded since at least
                ' v0.9.2. So the overlay is given its size here, with a fully
                ' transparent picture, when it is made and when the page
                ' area's size changes. That can wait on the screen as the
                ' worker's calls can, inside the switch's time, so it is
                ' timed for the budget rows (LastSizeMilliseconds). Not given
                ' it, the worker would pass a size the overlay has not, and
                ' Windows would ask this thread, waiting in Begin, about it.
                If _layeredSize <> screen.Size Then
                    Dim sizing As Long = Stopwatch.GetTimestamp()
                    Dim sized As Boolean = GiveLayeredSize(overlay, screen)
                    LastSizeMilliseconds = (Stopwatch.GetTimestamp() - sizing) * 1000.0 / Stopwatch.Frequency
                    If Not sized Then
                        _layeredSize = Size.Empty
                        LastOutcome = "cut: the overlay could not be sized"
                        Return
                    End If
                    _layeredSize = screen.Size
                    LayeredSizesGiven += 1
                End If

                Interlocked.Increment(_inFlight)
                Try
                    ThreadPool.UnsafeQueueUserWorkItem(Sub(state) Read(picture, screen, overlay), Nothing)
                Catch
                    Interlocked.Decrement(_inFlight)
                    Throw
                End Try
                _picture = picture

            Catch ex As Exception When TypeOf ex Is ComponentModel.Win32Exception OrElse TypeOf ex Is OutOfMemoryException
                _picture = Nothing
                LastOutcome = "cut: the overlay could not be made"
            End Try

        End Sub


        ' Once the switch has returned, with how long it took: the overlay,
        ' once the worker has put the picture in it, within a moment at
        ' most, shows over the page area and starts to fade. A switch that
        ' is a cut whatever the picture does (it did not finish, as when it
        ' threw, it took the fade's length or longer, or the window changed)
        ' is one at once, and does not wait for it. Its cost, the wait
        ' included, is part of the switch's.
        Friend Sub Begin(switchMilliseconds As Double, switched As Boolean)

            Dim watch As Stopwatch = Stopwatch.StartNew()
            Dim picture As Picture = _picture
            _picture = Nothing

            If picture Is Nothing Then
                Return
            End If

            Try

                ' What makes the switch a cut whatever the picture does is
                ' asked before the wait, so that such a switch waits for
                ' nothing: none of it can change during the wait, in which
                ' this thread handles no message.
                Dim cut As String = Nothing

                If Not switched Then
                    cut = "cut: the switch did not finish"
                ElseIf switchMilliseconds >= UiMotion.FadeMilliseconds Then
                    cut = "cut: the switch took longer than the fade"
                ElseIf Not UiMotion.Enabled OrElse Not CanShow() OrElse _area.RectangleToScreen(_area.ClientRectangle) <> picture.Screen OrElse OverlayHandle = IntPtr.Zero Then
                    cut = "cut: the window changed"
                End If

                ' An earlier read, let go, may have left the event set: the
                ' picture is then still being read, and the wait goes on for
                ' what is left of the moment.
                If cut Is Nothing Then
                    Dim wait As Integer = If(AwaitPicture, 2000, PictureWaitMilliseconds)
                    Dim left As Long = wait - watch.ElapsedMilliseconds
                    While left > 0 AndAlso Volatile.Read(picture.State) = Taking
                        WaitForSingleObject(_pictureDone.SafeWaitHandle, CUInt(left))
                        left = wait - watch.ElapsedMilliseconds
                    End While
                End If

                ' A picture still being read is let go; the worker finishes
                ' with the hidden overlay and frees what it made.
                Dim taken As Boolean = Interlocked.CompareExchange(picture.State, Abandoned, Taking) <> Taking

                If cut IsNot Nothing Then
                    LastOutcome = cut
                ElseIf Not taken Then
                    LastOutcome = "cut: the picture was not ready"
                ElseIf Not picture.Placed Then
                    LastOutcome = "cut: the picture could not be read"
                Else
                    Show()
                    FadesShown += 1
                    LastOutcome = "faded"
                End If

            Finally
                LastBeginMilliseconds = watch.Elapsed.TotalMilliseconds
            End Try

        End Sub


        ' Ends a fade showing: the picture goes at once.
        Friend Sub [End]()

            If _watch.IsRunning Then
                _watch.Reset()
                UiMotion.Halt(_step)
            End If

            If _filtering Then
                _filtering = False
                Application.RemoveMessageFilter(Me)
            End If

            If OverlayHandle <> IntPtr.Zero AndAlso IsWindowVisible(OverlayHandle) Then
                SetWindowPos(OverlayHandle, IntPtr.Zero, 0, 0, 0, 0, SWP_HIDEWINDOW Or Quietly)
                _lastHidden = Stopwatch.GetTimestamp()
            End If

        End Sub


        ' The fade ends on its own, at a key, a click or the wheel, or for
        ' the page shown to show more (Insights' figures): the picture goes,
        ' and the control in the page area with the keyboard, if it took it
        ' since the switch began, has its focus event raised again, as
        ' Windows raised it under the picture (#276): the control the switch
        ' gave it, or one given it once the switch had returned (Ctrl+F's
        ' search box). At an input, which can move the keyboard itself (a
        ' Tab, a click, the next switch), the event is raised once the input
        ' has been handled, and only if that control still has the keyboard
        ' and no picture shows: a control the input gave the keyboard has
        ' its own event, with nothing over the page. Not as the next switch
        ' begins, nor as the window moves, sizes, hides or goes to the
        ' background (End).
        Friend Sub Finish(Optional atAnInput As Boolean = False)

            Dim covered As IntPtr = If(Showing, FocusTakenInTheArea(), IntPtr.Zero)

            [End]()

            If covered = IntPtr.Zero Then
                Return
            End If

            If Not atAnInput Then
                TellFocus(covered)
            ElseIf _owner.IsHandleCreated Then
                _owner.BeginInvoke(New Action(Sub() TellFocus(covered)))
            End If

        End Sub


        ' The window with the keyboard, if it is in the page area and took the
        ' keyboard since the switch began; otherwise none.
        Private Function FocusTakenInTheArea() As IntPtr

            Dim focus As IntPtr = GetFocus()

            If focus = IntPtr.Zero OrElse focus = _focusBefore OrElse Not _area.IsHandleCreated OrElse
               Not (focus = _area.Handle OrElse IsChild(_area.Handle, focus)) Then
                Return IntPtr.Zero
            End If

            Return focus

        End Function


        ' A window's focus event raised again, if it still has the keyboard
        ' and no picture shows over it.
        Private Sub TellFocus(window As IntPtr)

            If window = GetFocus() AndAlso Not Showing Then
                NotifyWinEvent(EVENT_OBJECT_FOCUS, window, OBJID_CLIENT, CHILDID_SELF)
            End If

        End Sub


        ' A picture whose switch never began is let go.
        Private Sub Abandon()

            Dim picture As Picture = _picture
            _picture = Nothing

            If picture IsNot Nothing Then
                Interlocked.CompareExchange(picture.State, Abandoned, Taking)
            End If

        End Sub


        Private Function CanShow() As Boolean
            Return _owner.IsHandleCreated AndAlso _owner.Visible AndAlso _owner.WindowState <> FormWindowState.Minimized AndAlso
                _area.IsHandleCreated AndAlso _area.Visible AndAlso _area.ClientSize.Width > 0 AndAlso _area.ClientSize.Height > 0
        End Function


        ' Whether Windows scales the window: its DPI is not its screen's, or
        ' the area's corners, in the window's coordinates, are other points
        ' on the screen (the corners alone missed a window at 96 DPI on a
        ' screen at 120, #252). Asked of Windows whatever this thread's DPI
        ' awareness; a DPI or a point it cannot give counts as scaled.
        Private Function ScaledByWindows(area As Rectangle) As Boolean

            Dim owner As IntPtr = _owner.Handle

            If Not DpiIsTheScreens(owner) Then
                Return True
            End If

            For Each corner As Point In {area.Location, New Point(area.Right - 1, area.Bottom - 1)}
                Dim converted As New NativePoint With {.X = corner.X, .Y = corner.Y}
                If Not LogicalToPhysicalPointForPerMonitorDPI(owner, converted) OrElse converted.X <> corner.X OrElse converted.Y <> corner.Y Then
                    Return True
                End If
            Next

            Return False

        End Function


        ' Whether the window's DPI is the effective DPI of the screen it is
        ' on. Windows gives a screen's DPI as the asking thread sees it, so it
        ' is asked as a per-monitor aware thread; this one would be told its
        ' own DPI. False if Windows cannot say. Friend for the test of it;
        ' a test may give the screen's DPI (ScreenDpiOverride).
        Friend Shared Function DpiIsTheScreens(window As IntPtr) As Boolean

            Try

                Dim windowDpi As Integer = GetDpiForWindow(window)

                If windowDpi = 0 Then
                    Return False
                End If

                Dim previous As IntPtr = SetThreadDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2)

                If previous = IntPtr.Zero Then
                    Return False
                End If

                Dim screenX As UInteger = 0
                Dim screenY As UInteger = 0
                Dim result As Integer

                Try
                    result = GetDpiForMonitor(MonitorFromWindow(window, MONITOR_DEFAULTTONEAREST), MDT_EFFECTIVE_DPI, screenX, screenY)
                Finally
                    SetThreadDpiAwarenessContext(previous)
                End Try

                Dim screenDpi As Long = If(ScreenDpiOverride.HasValue, CLng(ScreenDpiOverride.Value), CLng(screenX))

                Return result = 0 AndAlso screenDpi = windowDpi

            Catch ex As Exception When TypeOf ex Is EntryPointNotFoundException OrElse TypeOf ex Is DllNotFoundException
                Return False
            End Try

        End Function


        ' Whether a window of this thread's other than the owner and the
        ' overlay shows over the area: a tooltip, a menu, a window of its own.
        Private Function AnotherWindowOver(area As Rectangle) As Boolean

            Dim found As Boolean = False
            Dim owner As IntPtr = _owner.Handle
            Dim overlay As IntPtr = OverlayHandle

            EnumThreadWindows(GetCurrentThreadId(),
                Function(window, parameter)
                    If window <> owner AndAlso window <> overlay AndAlso IsWindowVisible(window) AndAlso WindowBounds(window).IntersectsWith(area) Then
                        found = True
                        Return False
                    End If
                    Return True
                End Function,
                IntPtr.Zero)

            Return found

        End Function


        ' The overlay, holding the picture opaque over the page area, shown;
        ' then the fade's clock.
        Private Sub Show()

            SetWindowPos(_overlay.Handle, IntPtr.Zero, 0, 0, 0, 0, SWP_SHOWWINDOW Or Quietly)

            _watch.Restart()

            If Not _filtering Then
                _filtering = True
                Application.AddMessageFilter(Me)
            End If

            UiMotion.Run(_step)

        End Sub


        ' A frame: the picture's opacity from the fade's own stopwatch. The
        ' fade ends at its length, or as soon as the picture would be drawn
        ' fully transparent, which the ease reaches at seven eighths of it.
        Private Function StepOnce() As Boolean

            If Not _watch.IsRunning OrElse OverlayHandle = IntPtr.Zero Then
                Return False
            End If

            Dim elapsed As Double = _watch.Elapsed.TotalMilliseconds

            Dim alpha As Double = Math.Round(255.0 * (1.0 - UiMotion.EaseOut(elapsed / UiMotion.FadeMilliseconds)))

            If elapsed >= UiMotion.FadeMilliseconds OrElse alpha <= 0.0 OrElse Not UiMotion.Enabled Then
                Finish()
                Return False
            End If

            Dim blend As BlendFunction = Opacity(CByte(Math.Min(255.0, alpha)))
            UpdateLayeredWindowAlpha(_overlay.Handle, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 0, blend, ULW_ALPHA)
            Return True

        End Function


        ' A key, a button, the wheel or a touch anywhere in the thread's
        ' windows ends the fade and goes on to where it was going.
        Public Function PreFilterMessage(ByRef m As Message) As Boolean Implements IMessageFilter.PreFilterMessage

            Select Case m.Msg
                Case WM_KEYDOWN, WM_SYSKEYDOWN,
                     WM_LBUTTONDOWN, WM_RBUTTONDOWN, WM_MBUTTONDOWN, WM_XBUTTONDOWN,
                     WM_NCLBUTTONDOWN, WM_NCRBUTTONDOWN, WM_NCMBUTTONDOWN, WM_NCXBUTTONDOWN,
                     WM_MOUSEWHEEL, WM_MOUSEHWHEEL, WM_POINTERDOWN
                    Finish(atAnInput:=True)
            End Select

            Return False

        End Function


        Private Sub OwnerChanged(sender As Object, e As EventArgs)
            [End]()
        End Sub


        Private Sub OwnerDestroyed(sender As Object, e As EventArgs)
            [End]()
            Abandon()
            If _overlay IsNot Nothing AndAlso _overlay.Handle <> IntPtr.Zero Then
                _overlay.DestroyHandle()
            End If
            _overlay = Nothing
        End Sub


        Private Shared Function Opacity(alpha As Byte) As BlendFunction
            Return New BlendFunction With {.BlendOp = AC_SRC_OVER, .BlendFlags = 0, .SourceConstantAlpha = alpha, .AlphaFormat = 0}
        End Function


        ' The hidden overlay given its place and layered size, on the
        ' window's thread, with a picture of that size drawn fully
        ' transparent (Capture). False if Windows cannot.
        Private Shared Function GiveLayeredSize(overlay As IntPtr, area As Rectangle) As Boolean

            Dim surface As IntPtr = GetDC(IntPtr.Zero)

            If surface = IntPtr.Zero Then
                Return False
            End If

            Try
                Dim memory As IntPtr = CreateCompatibleDC(surface)
                If memory = IntPtr.Zero Then Return False
                Try
                    Dim header As New BitmapInfoHeader With {
                        .Size = Marshal.SizeOf(Of BitmapInfoHeader)(),
                        .Width = area.Width,
                        .Height = -area.Height,
                        .Planes = 1,
                        .BitCount = 32,
                        .Compression = 0
                    }
                    Dim bits As IntPtr = IntPtr.Zero
                    Dim bitmap As IntPtr = CreateDIBSection(memory, header, 0, bits, IntPtr.Zero, 0)
                    If bitmap = IntPtr.Zero Then Return False
                    Try
                        Dim previous As IntPtr = SelectObject(memory, bitmap)
                        Try
                            Dim place As New NativePoint With {.X = area.X, .Y = area.Y}
                            Dim extent As New NativePoint With {.X = area.Width, .Y = area.Height}
                            Dim origin As New NativePoint()
                            Dim blend As BlendFunction = Opacity(0)
                            Return UpdateLayeredWindowPicture(overlay, surface, place, extent, memory, origin, 0, blend, ULW_ALPHA)
                        Finally
                            SelectObject(memory, previous)
                        End Try
                    Finally
                        DeleteObject(bitmap)
                    End Try
                Finally
                    DeleteDC(memory)
                End Try
            Finally
                ReleaseDC(IntPtr.Zero, surface)
            End Try

        End Function


        ' ---------------------------------------------------------------
        ' The worker's read
        ' ---------------------------------------------------------------

        Private Const Taking As Integer = 0
        Private Const Ready As Integer = 1
        Private Const Abandoned As Integer = 2

        ' A picture of the page area for one switch: read and put in the
        ' overlay by a worker, then shown by the switch, or let go.
        Private NotInheritable Class Picture
            Public State As Integer = Taking
            Public Placed As Boolean = False
            Public Screen As Rectangle
        End Class


        ' Reads the page area off the screen, as it shows now, and puts it in
        ' the hidden overlay, opaque, where the page area is: the switch
        ' holds the page area's drawing, so the screen goes on showing the
        ' page left until the switch has returned. The screen's device
        ' context, not the window's: while a read of the window's own waited
        ' on the screen, every change the switch made to the window's child
        ' windows waited for it too (5 to 15 of 50 switches on the 500
        ' library held 450 ms and more by reads that long); the screen's is
        ' no device context of the window's. Putting the picture in the
        ' overlay can wait on the screen as the read can (once 522 ms of 50
        ' switches, done on the window's thread), so it is done here too, at
        ' the layered size the window's thread gave the hidden overlay with
        ' a transparent picture as it was made or the page area's size
        ' changed (Capture, LastSizeMilliseconds, #268); the switch then
        ' only shows the overlay. Nothing of Windows Forms is touched here,
        ' and what is made here is freed here.
        Private Shared Sub Read(picture As Picture, source As Rectangle, overlay As IntPtr)

            Dim bitmap As IntPtr = IntPtr.Zero
            Dim placed As Boolean = False
            Dim started As Long = Stopwatch.GetTimestamp()
            Dim readAt As Long = 0
            Dim awareness As IntPtr = IntPtr.Zero

            Try

                ' The screen is read, and the picture put in, in the
                ' overlay's DPI awareness, the one the page area was measured
                ' in. Every thread of PaperRoute's has it already; a pool
                ' thread of the tests' host, which is DPI-unaware, read a
                ' screen Windows scaled for it, at another place and size
                ' (#268). The thread is put back as it came (Finally).
                awareness = SetThreadDpiAwarenessContext(GetWindowDpiAwarenessContext(overlay))

                If CaptureDelayMilliseconds > 0 Then
                    Thread.Sleep(CaptureDelayMilliseconds)
                End If

                LastReadAwareness = GetThreadDpiAwarenessContext()
                Dim surface As IntPtr = GetDC(IntPtr.Zero)

                If surface <> IntPtr.Zero Then
                    Try
                        Dim memory As IntPtr = CreateCompatibleDC(surface)
                        If memory <> IntPtr.Zero Then
                            Try
                                Dim header As New BitmapInfoHeader With {
                                    .Size = Marshal.SizeOf(Of BitmapInfoHeader)(),
                                    .Width = source.Width,
                                    .Height = -source.Height,
                                    .Planes = 1,
                                    .BitCount = 32,
                                    .Compression = 0
                                }
                                Dim bits As IntPtr = IntPtr.Zero
                                bitmap = CreateDIBSection(memory, header, 0, bits, IntPtr.Zero, 0)
                                If bitmap <> IntPtr.Zero Then
                                    Dim previous As IntPtr = SelectObject(memory, bitmap)
                                    Try
                                        If BitBlt(memory, 0, 0, source.Width, source.Height, surface, source.X, source.Y, SRCCOPY) Then
                                            readAt = Stopwatch.GetTimestamp()
                                            ' At the place and layered size the
                                            ' window's thread gave the overlay
                                            ' (Capture): given no size, Windows
                                            ' showed none of the picture (#268).
                                            Dim place As New NativePoint With {.X = source.X, .Y = source.Y}
                                            Dim extent As New NativePoint With {.X = source.Width, .Y = source.Height}
                                            Dim origin As New NativePoint()
                                            Dim blend As BlendFunction = Opacity(255)
                                            placed = UpdateLayeredWindowPicture(overlay, surface, place, extent, memory, origin, 0, blend, ULW_ALPHA)
                                        End If
                                    Finally
                                        SelectObject(memory, previous)
                                    End Try
                                End If
                            Finally
                                DeleteDC(memory)
                            End Try
                        End If
                    Finally
                        ReleaseDC(IntPtr.Zero, surface)
                    End Try
                End If

            Catch ex As Exception
                ' A picture that cannot be read or placed is a cut.
                placed = False
            Finally
                If bitmap <> IntPtr.Zero Then DeleteObject(bitmap)
                If awareness <> IntPtr.Zero Then SetThreadDpiAwarenessContext(awareness)
                Dim finished As Long = Stopwatch.GetTimestamp()
                Volatile.Write(_lastReadTicks, If(readAt = 0, finished, readAt) - started)
                Volatile.Write(_lastPlaceTicks, If(readAt = 0, 0, finished - readAt))
                picture.Placed = placed
                Interlocked.CompareExchange(picture.State, Ready, Taking)
                _pictureDone.Set()
                Interlocked.Decrement(_inFlight)
            End Try

        End Sub


        ' The worker's read of an area into a window, run on the calling
        ' thread: Friend for the test that it puts the thread's DPI
        ' awareness back. Whether the picture was put in.
        Friend Shared Function ReadHere(source As Rectangle, overlay As IntPtr) As Boolean

            Dim picture As New Picture With {.Screen = source}
            Interlocked.Increment(_inFlight)
            Read(picture, source, overlay)
            Return picture.Placed

        End Function


        ' ---------------------------------------------------------------
        ' The overlay
        ' ---------------------------------------------------------------

        ' A popup owned by the window: disabled, layered, transparent to the
        ' pointer, never activated, with no taskbar button and no title.
        Private NotInheritable Class Overlay
            Inherits NativeWindow

            Private _provider As OverlayProvider = Nothing

            Public Sub New(owner As Form)
                CreateHandle(New CreateParams With {
                    .Caption = String.Empty,
                    .Style = WS_POPUP Or WS_DISABLED,
                    .ExStyle = WS_EX_LAYERED Or WS_EX_TRANSPARENT Or WS_EX_NOACTIVATE Or WS_EX_TOOLWINDOW,
                    .Parent = owner.Handle
                })
            End Sub

            Protected Overrides Sub WndProc(ByRef m As Message)

                Select Case m.Msg

                    Case WM_NCHITTEST
                        m.Result = New IntPtr(HTTRANSPARENT)
                        Return

                    Case WM_MOUSEACTIVATE
                        m.Result = New IntPtr(MA_NOACTIVATE)
                        Return

                    ' Windows hides an owned popup showing as the window is
                    ' minimized, and shows it again as the window is restored,
                    ' after the window's own Move and Resize have ended the
                    ' fade: the picture would come back over the page, with no
                    ' clock and no filter left to hide it. Only the fade shows
                    ' the overlay (SetWindowPos), so that show is not made.
                    Case WM_SHOWWINDOW
                        If m.WParam <> IntPtr.Zero AndAlso m.LParam = New IntPtr(SW_PARENTOPENING) Then
                            Return
                        End If

                    ' UI Automation's own element for the window, in the raw
                    ' view only.
                    Case WM_GETOBJECT
                        If LowInt(m.LParam) = UiaRootObjectId Then
                            If _provider Is Nothing Then _provider = New OverlayProvider(Handle)
                            m.Result = UiaReturnRawElementProvider(Handle, m.WParam, m.LParam, _provider)
                            Return
                        End If

                    Case WM_DESTROY
                        If _provider IsNot Nothing Then
                            UiaReturnRawElementProvider(Handle, IntPtr.Zero, IntPtr.Zero, Nothing)
                            _provider = Nothing
                        End If

                End Select

                MyBase.WndProc(m)

            End Sub

            Private Shared Function LowInt(value As IntPtr) As Integer
                Return BitConverter.ToInt32(BitConverter.GetBytes(value.ToInt64()), 0)
            End Function

        End Class


        ' The overlay as UI Automation sees it: an element of the raw view,
        ' neither a control nor content, which takes no keyboard focus. The
        ' rest of what UI Automation knows of the window comes from Windows'
        ' own element for it.
        Private NotInheritable Class OverlayProvider
            Implements IRawElementProviderSimple

            Private ReadOnly _window As IntPtr

            Public Sub New(window As IntPtr)
                _window = window
            End Sub

            Public ReadOnly Property ProviderOptions As Integer Implements IRawElementProviderSimple.ProviderOptions
                Get
                    Return ServerSideProvider
                End Get
            End Property

            Public Function GetPatternProvider(patternId As Integer) As Object Implements IRawElementProviderSimple.GetPatternProvider
                Return Nothing
            End Function

            Public Function GetPropertyValue(propertyId As Integer) As Object Implements IRawElementProviderSimple.GetPropertyValue
                Select Case propertyId
                    Case UIA_IsControlElementPropertyId, UIA_IsContentElementPropertyId, UIA_IsKeyboardFocusablePropertyId
                        Return False
                    Case Else
                        Return Nothing
                End Select
            End Function

            Public ReadOnly Property HostRawElementProvider As IRawElementProviderSimple Implements IRawElementProviderSimple.HostRawElementProvider
                Get
                    Dim host As IRawElementProviderSimple = Nothing
                    If _window <> IntPtr.Zero AndAlso UiaHostProviderFromHwnd(_window, host) = 0 Then
                        Return host
                    End If
                    Return Nothing
                End Get
            End Property

        End Class


        ' ---------------------------------------------------------------
        ' Windows
        ' ---------------------------------------------------------------

        Friend Const UIA_IsKeyboardFocusablePropertyId As Integer = 30009
        Friend Const UIA_IsControlElementPropertyId As Integer = 30016
        Friend Const UIA_IsContentElementPropertyId As Integer = 30017
        Private Const UiaRootObjectId As Integer = -25
        Private Const ServerSideProvider As Integer = 2

        Friend Const WS_POPUP As Integer = &H80000000
        Friend Const WS_DISABLED As Integer = &H8000000
        Friend Const WS_CHILD As Integer = &H40000000
        Friend Const WS_EX_LAYERED As Integer = &H80000
        Friend Const WS_EX_TRANSPARENT As Integer = &H20
        Friend Const WS_EX_NOACTIVATE As Integer = &H8000000
        Friend Const WS_EX_TOOLWINDOW As Integer = &H80

        Private Const WM_DESTROY As Integer = &H2
        Private Const WM_SHOWWINDOW As Integer = &H18
        Private Const SW_PARENTOPENING As Integer = 3
        Private Const WM_NCHITTEST As Integer = &H84
        Private Const WM_MOUSEACTIVATE As Integer = &H21
        Private Const WM_GETOBJECT As Integer = &H3D
        Private Const WM_KEYDOWN As Integer = &H100
        Private Const WM_SYSKEYDOWN As Integer = &H104
        Private Const WM_LBUTTONDOWN As Integer = &H201
        Private Const WM_RBUTTONDOWN As Integer = &H204
        Private Const WM_MBUTTONDOWN As Integer = &H207
        Private Const WM_XBUTTONDOWN As Integer = &H20B
        Private Const WM_NCLBUTTONDOWN As Integer = &HA1
        Private Const WM_NCRBUTTONDOWN As Integer = &HA4
        Private Const WM_NCMBUTTONDOWN As Integer = &HA7
        Private Const WM_NCXBUTTONDOWN As Integer = &HAB
        Private Const WM_MOUSEWHEEL As Integer = &H20A
        Private Const WM_MOUSEHWHEEL As Integer = &H20E
        Private Const WM_POINTERDOWN As Integer = &H246
        Private Const HTTRANSPARENT As Integer = -1
        Private Const MA_NOACTIVATE As Integer = 3
        Private Const SWP_NOSIZE As UInteger = &H1
        Private Const SWP_NOMOVE As UInteger = &H2
        Private Const SWP_NOZORDER As UInteger = &H4
        Private Const SWP_NOREDRAW As UInteger = &H8
        Private Const SWP_NOACTIVATE As UInteger = &H10
        Private Const SWP_SHOWWINDOW As UInteger = &H40
        Private Const SWP_HIDEWINDOW As UInteger = &H80
        Private Const SWP_NOOWNERZORDER As UInteger = &H200
        Private Const SWP_DEFERERASE As UInteger = &H2000

        ' Shown or hidden where it is, never activated, and without Windows
        ' erasing and framing the windows of the thread there and then: shown
        ' with ShowWindow, the overlay had every window of the page just
        ' shown, waiting for its paint, sent WM_NCPAINT and WM_ERASEBKGND
        ' inside the switch (PageSwitchTests.SwitchesPaintNothingSynchronously).
        Private Const Quietly As UInteger = SWP_NOMOVE Or SWP_NOSIZE Or SWP_NOZORDER Or SWP_NOOWNERZORDER Or SWP_NOACTIVATE Or SWP_DEFERERASE
        Private Const SRCCOPY As Integer = &HCC0020
        Private Const ULW_ALPHA As Integer = &H2
        Private Const AC_SRC_OVER As Byte = 0

        <StructLayout(LayoutKind.Sequential)>
        Private Structure NativePoint
            Public X As Integer
            Public Y As Integer
        End Structure

        <StructLayout(LayoutKind.Sequential)>
        Private Structure NativeRect
            Public Left As Integer
            Public Top As Integer
            Public Right As Integer
            Public Bottom As Integer
        End Structure

        <StructLayout(LayoutKind.Sequential, Pack:=1)>
        Private Structure BlendFunction
            Public BlendOp As Byte
            Public BlendFlags As Byte
            Public SourceConstantAlpha As Byte
            Public AlphaFormat As Byte
        End Structure

        <StructLayout(LayoutKind.Sequential)>
        Private Structure BitmapInfoHeader
            Public Size As Integer
            Public Width As Integer
            Public Height As Integer
            Public Planes As Short
            Public BitCount As Short
            Public Compression As Integer
            Public SizeImage As Integer
            Public XPelsPerMeter As Integer
            Public YPelsPerMeter As Integer
            Public ClrUsed As Integer
            Public ClrImportant As Integer
        End Structure

        <DllImport("user32.dll")>
        Private Shared Function GetDC(window As IntPtr) As IntPtr
        End Function

        <DllImport("user32.dll")>
        Private Shared Function ReleaseDC(window As IntPtr, dc As IntPtr) As Integer
        End Function

        <DllImport("gdi32.dll")>
        Private Shared Function CreateCompatibleDC(dc As IntPtr) As IntPtr
        End Function

        <DllImport("gdi32.dll")>
        Private Shared Function DeleteDC(dc As IntPtr) As Boolean
        End Function

        <DllImport("gdi32.dll")>
        Private Shared Function CreateDIBSection(dc As IntPtr, ByRef header As BitmapInfoHeader, usage As UInteger, ByRef bits As IntPtr, section As IntPtr, offset As UInteger) As IntPtr
        End Function

        <DllImport("gdi32.dll")>
        Private Shared Function SelectObject(dc As IntPtr, item As IntPtr) As IntPtr
        End Function

        <DllImport("gdi32.dll")>
        Private Shared Function DeleteObject(item As IntPtr) As Boolean
        End Function

        <DllImport("gdi32.dll")>
        Private Shared Function BitBlt(target As IntPtr, x As Integer, y As Integer, width As Integer, height As Integer, source As IntPtr, sourceX As Integer, sourceY As Integer, operation As Integer) As Boolean
        End Function

        ' A picture, at a place and a size.
        <DllImport("user32.dll", EntryPoint:="UpdateLayeredWindow")>
        Private Shared Function UpdateLayeredWindowPicture(window As IntPtr, screen As IntPtr, ByRef position As NativePoint, ByRef size As NativePoint, source As IntPtr, ByRef origin As NativePoint, key As Integer, ByRef blend As BlendFunction, flags As Integer) As Boolean
        End Function

        <DllImport("user32.dll")>
        Private Shared Function GetWindowRect(window As IntPtr, ByRef rect As NativeRect) As Boolean
        End Function

        Private Shared Function WindowBounds(window As IntPtr) As Rectangle
            Dim rect As NativeRect
            If Not GetWindowRect(window, rect) Then Return Rectangle.Empty
            Return Rectangle.FromLTRB(rect.Left, rect.Top, rect.Right, rect.Bottom)
        End Function

        <DllImport("user32.dll", EntryPoint:="UpdateLayeredWindow")>
        Private Shared Function UpdateLayeredWindowAlpha(window As IntPtr, screen As IntPtr, position As IntPtr, size As IntPtr, source As IntPtr, origin As IntPtr, key As Integer, ByRef blend As BlendFunction, flags As Integer) As Boolean
        End Function

        <DllImport("user32.dll")>
        Private Shared Function SetWindowPos(window As IntPtr, after As IntPtr, x As Integer, y As Integer, width As Integer, height As Integer, flags As UInteger) As Boolean
        End Function

        <DllImport("user32.dll")>
        Private Shared Function IsWindowVisible(window As IntPtr) As Boolean
        End Function

        Private Const EVENT_OBJECT_FOCUS As UInteger = &H8005UI
        Private Const OBJID_CLIENT As Integer = &HFFFFFFFC
        Private Const CHILDID_SELF As Integer = 0

        <DllImport("user32.dll")>
        Private Shared Function GetFocus() As IntPtr
        End Function

        <DllImport("user32.dll")>
        Private Shared Function IsChild(parent As IntPtr, window As IntPtr) As Boolean
        End Function

        <DllImport("user32.dll")>
        Private Shared Sub NotifyWinEvent(eventType As UInteger, window As IntPtr, objectId As Integer, childId As Integer)
        End Sub

        <DllImport("user32.dll")>
        Private Shared Function LogicalToPhysicalPointForPerMonitorDPI(window As IntPtr, ByRef point As NativePoint) As Boolean
        End Function

        Private Shared ReadOnly DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 As New IntPtr(-4)
        Private Const MONITOR_DEFAULTTONEAREST As UInteger = 2
        Private Const MDT_EFFECTIVE_DPI As Integer = 0

        <DllImport("user32.dll")>
        Private Shared Function GetDpiForWindow(window As IntPtr) As Integer
        End Function

        <DllImport("user32.dll")>
        Private Shared Function SetThreadDpiAwarenessContext(context As IntPtr) As IntPtr
        End Function

        <DllImport("user32.dll")>
        Private Shared Function GetWindowDpiAwarenessContext(window As IntPtr) As IntPtr
        End Function

        <DllImport("user32.dll")>
        Private Shared Function GetThreadDpiAwarenessContext() As IntPtr
        End Function

        ' Waits on a handle and handles no message meanwhile: a wait of
        ' .NET's on the window's thread, a single-threaded apartment's,
        ' handles some, which could reenter the switch.
        <DllImport("kernel32.dll")>
        Private Shared Function WaitForSingleObject(handle As Microsoft.Win32.SafeHandles.SafeWaitHandle, milliseconds As UInteger) As UInteger
        End Function

        <DllImport("user32.dll")>
        Private Shared Function MonitorFromWindow(window As IntPtr, flags As UInteger) As IntPtr
        End Function

        <DllImport("Shcore.dll")>
        Private Shared Function GetDpiForMonitor(monitor As IntPtr, kind As Integer, ByRef dpiX As UInteger, ByRef dpiY As UInteger) As Integer
        End Function

        Private Delegate Function EnumWindowsProc(window As IntPtr, parameter As IntPtr) As Boolean

        <DllImport("user32.dll")>
        Private Shared Function EnumThreadWindows(threadId As Integer, callback As EnumWindowsProc, parameter As IntPtr) As Boolean
        End Function

        <DllImport("kernel32.dll")>
        Private Shared Function GetCurrentThreadId() As Integer
        End Function

        <DllImport("UIAutomationCore.dll")>
        Private Shared Function UiaReturnRawElementProvider(window As IntPtr, wParam As IntPtr, lParam As IntPtr, element As IRawElementProviderSimple) As IntPtr
        End Function

        <DllImport("UIAutomationCore.dll")>
        Private Shared Function UiaHostProviderFromHwnd(window As IntPtr, ByRef provider As IRawElementProviderSimple) As Integer
        End Function

    End Class


    ' UI Automation's provider interface, as UIAutomationCore declares it.
    <ComImport>
    <Guid("d6dd68d1-86fd-4332-8666-9abedea2d24c")>
    <InterfaceType(ComInterfaceType.InterfaceIsIUnknown)>
    Friend Interface IRawElementProviderSimple
        ReadOnly Property ProviderOptions As Integer
        Function GetPatternProvider(patternId As Integer) As <MarshalAs(UnmanagedType.IUnknown)> Object
        Function GetPropertyValue(propertyId As Integer) As Object
        ReadOnly Property HostRawElementProvider As IRawElementProviderSimple
    End Interface

End Namespace
