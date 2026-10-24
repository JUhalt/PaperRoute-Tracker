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
    ' picture in a window of its own, hidden over the page area; the UI
    ' thread never waits for either, which on this machine have waited on
    ' the screen for half a second. Once the switch has returned, that
    ' window, if the picture is in it, is shown and faded out over
    ' UiMotion.FadeMilliseconds on UiMotion's Windows Forms clock. A
    ' picture not ready, a switch that took the fade's length or longer,
    ' a switch just after a fade or with another of the thread's windows
    ' over the page area, or motion off, is a cut, as before.
    '
    ' The window never takes part: disabled, never activated, transparent
    ' to the pointer, and in no view a screen reader reads (its UI
    ' Automation element is in the raw view only); it is not a control, so
    ' nothing in the window's tree, its Tab order or its focus changes. The
    ' first key, click or wheel ends the fade there and then and goes on
    ' to the page; so do the next switch and the window moving, sizing,
    ' hiding or changing colors. Made on the first switch that could fade,
    ' it is kept hidden between fades and destroyed with the window.
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

        ' How long the screen may go on showing a fade once it is hidden: a
        ' few of Windows' frames.
        Private Const SettleMilliseconds As Double = 50

        ' Test seams: how long the worker waits before it reads, and whether
        ' Begin waits for the picture (functional tests only; the budget
        ' never does).
        Friend Shared CaptureDelayMilliseconds As Integer = 0
        Friend Shared AwaitPicture As Boolean = False


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

            ' The overlay is made, and placed over the page area, on the
            ' window's thread, hidden: the worker puts the picture in it
            ' without moving or sizing it, which would have Windows ask this
            ' thread, busy with the switch, about the move. A window Windows
            ' cannot make (out of window handles) is a cut: the fade never
            ' stops the switch.
            Try

                If _overlay Is Nothing OrElse _overlay.Handle = IntPtr.Zero Then
                    _overlay = New Overlay(_owner)
                    OverlaysMade += 1
                End If

                Dim picture As New Picture With {.Screen = screen}
                Dim overlay As IntPtr = _overlay.Handle

                If WindowBounds(overlay) <> screen Then
                    SetWindowPos(overlay, IntPtr.Zero, screen.X, screen.Y, screen.Width, screen.Height, SWP_NOZORDER Or SWP_NOOWNERZORDER Or SWP_NOACTIVATE Or SWP_DEFERERASE Or SWP_NOREDRAW)
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
        ' if the worker has put the picture in it, shows over the page area
        ' and starts to fade. Its cost is part of the switch's.
        Friend Sub Begin(switchMilliseconds As Double, switched As Boolean)

            Dim watch As Stopwatch = Stopwatch.StartNew()
            Dim picture As Picture = _picture
            _picture = Nothing

            If picture Is Nothing Then
                Return
            End If

            Try

                If AwaitPicture Then
                    Dim waited As Stopwatch = Stopwatch.StartNew()
                    While Volatile.Read(picture.State) = Taking AndAlso waited.ElapsedMilliseconds < 2000
                        Thread.Sleep(1)
                    End While
                End If

                ' A picture still being read is let go; the worker finishes
                ' with the hidden overlay and frees what it made.
                Dim taken As Boolean = Interlocked.CompareExchange(picture.State, Abandoned, Taking) <> Taking

                If Not taken Then
                    LastOutcome = "cut: the picture was not ready"
                    Return
                End If

                Dim bounds As Rectangle = _area.RectangleToScreen(_area.ClientRectangle)

                If Not switched Then
                    LastOutcome = "cut: the switch did not finish"
                ElseIf Not picture.Placed Then
                    LastOutcome = "cut: the picture could not be read"
                ElseIf switchMilliseconds >= UiMotion.FadeMilliseconds Then
                    LastOutcome = "cut: the switch took longer than the fade"
                ElseIf Not UiMotion.Enabled OrElse Not CanShow() OrElse bounds <> picture.Screen OrElse OverlayHandle = IntPtr.Zero Then
                    LastOutcome = "cut: the window changed"
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
                [End]()
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
                    [End]()
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
        ' switches, done on the window's thread), so it is done here too,
        ' and the switch only shows the overlay. Nothing of Windows Forms is
        ' touched here, and what is made here is freed here.
        Private Shared Sub Read(picture As Picture, source As Rectangle, overlay As IntPtr)

            Dim bitmap As IntPtr = IntPtr.Zero
            Dim placed As Boolean = False
            Dim started As Long = Stopwatch.GetTimestamp()
            Dim readAt As Long = 0

            Try

                If CaptureDelayMilliseconds > 0 Then
                    Thread.Sleep(CaptureDelayMilliseconds)
                End If

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
                                            Dim origin As New NativePoint()
                                            Dim blend As BlendFunction = Opacity(255)
                                            placed = UpdateLayeredWindowPicture(overlay, surface, IntPtr.Zero, IntPtr.Zero, memory, origin, 0, blend, ULW_ALPHA)
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
                Dim finished As Long = Stopwatch.GetTimestamp()
                Volatile.Write(_lastReadTicks, If(readAt = 0, finished, readAt) - started)
                Volatile.Write(_lastPlaceTicks, If(readAt = 0, 0, finished - readAt))
                picture.Placed = placed
                Interlocked.CompareExchange(picture.State, Ready, Taking)
                Interlocked.Decrement(_inFlight)
            End Try

        End Sub


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

        ' The picture, where the window already is and at its size.
        <DllImport("user32.dll", EntryPoint:="UpdateLayeredWindow")>
        Private Shared Function UpdateLayeredWindowPicture(window As IntPtr, screen As IntPtr, position As IntPtr, size As IntPtr, source As IntPtr, ByRef origin As NativePoint, key As Integer, ByRef blend As BlendFunction, flags As Integer) As Boolean
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
