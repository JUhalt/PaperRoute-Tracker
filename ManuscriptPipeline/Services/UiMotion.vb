Imports System
Imports System.Collections.Generic
Imports System.Diagnostics
Imports System.Runtime.InteropServices
Imports System.Threading
Imports System.Windows.Forms

Namespace Services

    ' Micro-motion (#142): whether anything moves, and the one clock it
    ' moves by. Windows' "Animation effects" setting (Settings >
    ' Accessibility > Visual effects) is read once, and again after Windows
    ' says a setting changed (Form1's WM_SETTINGCHANGE); with it off, or
    ' under a Windows contrast theme, every state changes at once and a
    ' page switch is a cut, as before.
    Friend NotInheritable Class UiMotion

        Private Sub New()
        End Sub


        ' Test seam: motion on or off in place of Windows' setting. A test
        ' that sets it puts it back (Nothing) when it ends.
        Friend Shared EnabledOverride As Boolean? = Nothing

        ' How long a page's cross-fade and a hover's ease last, in
        ' milliseconds. Tests may shorten or lengthen them and put them back.
        Friend Const DefaultFadeMilliseconds As Integer = 150
        Friend Const DefaultHoverMilliseconds As Integer = 100
        Friend Shared FadeMilliseconds As Integer = DefaultFadeMilliseconds
        Friend Shared HoverMilliseconds As Integer = DefaultHoverMilliseconds

        ' Windows' setting as last read: unknown until it is asked for, and
        ' again once Windows says a setting changed.
        Private Const SettingUnknown As Integer = 0
        Private Const SettingOn As Integer = 1
        Private Const SettingOff As Integer = 2
        Private Shared _setting As Integer = SettingUnknown
        Private Shared _settingReads As Integer = 0


        ' Whether anything moves now. A contrast theme always wins, so a
        ' theme turned on while PaperRoute runs stops the motion at once;
        ' Animation effects turned off does too, once Windows has said so.
        Public Shared ReadOnly Property Enabled As Boolean
            Get
                Return Not UiTheme.IsHighContrast() AndAlso If(EnabledOverride, Setting())
            End Get
        End Property


        ' Windows says a setting changed: the next step asks it again, so a
        ' fade or an ease under way stops on its next frame.
        Friend Shared Sub SettingChanged()
            Volatile.Write(_setting, SettingUnknown)
        End Sub


        ' How many times Windows' setting has been read this session: for
        ' the test that it is read once until Windows says it changed.
        Friend Shared ReadOnly Property SettingReads As Integer
            Get
                Return Volatile.Read(_settingReads)
            End Get
        End Property


        Private Shared Function Setting() As Boolean

            Dim known As Integer = Volatile.Read(_setting)

            If known = SettingUnknown Then
                known = If(ReadSetting(), SettingOn, SettingOff)
                Volatile.Write(_setting, known)
            End If

            Return known = SettingOn

        End Function


        Private Shared Function ReadSetting() As Boolean

            Interlocked.Increment(_settingReads)

            Dim animate As Integer = 1

            ' Unread, it is Windows' default: on.
            If Not SystemParametersInfo(SPI_GETCLIENTAREAANIMATION, 0, animate, 0) Then
                Return True
            End If

            Return animate <> 0

        End Function


        ' The ease every motion uses: quick at first, settling at the end.
        Friend Shared Function EaseOut(t As Double) As Double

            Dim clamped As Double = Math.Max(0.0, Math.Min(1.0, t))
            Dim rest As Double = 1.0 - clamped
            Return 1.0 - rest * rest * rest

        End Function


        ' ---------------------------------------------------------------
        ' The clock
        ' ---------------------------------------------------------------

        ' One Windows Forms timer per UI thread runs every step of motion:
        ' started by the first step, stopped once none is left. A step
        ' returns whether it goes on. Windows Forms timers tick about every
        ' 16 ms at an interval of 15 or less, so each step works out where
        ' it is from its own stopwatch, never from the ticks it has seen.
        <ThreadStatic>
        Private Shared _clock As System.Windows.Forms.Timer

        <ThreadStatic>
        Private Shared _steps As List(Of Func(Of Boolean))

        Private Const ClockInterval As Integer = 15


        Friend Shared Sub Run(stepOnce As Func(Of Boolean))

            If _steps Is Nothing Then
                _steps = New List(Of Func(Of Boolean))()
            End If

            If Not _steps.Contains(stepOnce) Then
                _steps.Add(stepOnce)
            End If

            If _clock Is Nothing Then
                _clock = New System.Windows.Forms.Timer With {.Interval = ClockInterval}
                AddHandler _clock.Tick, AddressOf Tick
            End If

            If Not _clock.Enabled Then
                _clock.Start()
            End If

        End Sub


        Friend Shared Sub Halt(stepOnce As Func(Of Boolean))

            If _steps Is Nothing Then
                Return
            End If

            _steps.Remove(stepOnce)

            If _steps.Count = 0 Then
                _clock?.Stop()
            End If

        End Sub


        ' Whether the clock runs on this thread.
        Friend Shared ReadOnly Property Animating As Boolean
            Get
                Return _clock IsNot Nothing AndAlso _clock.Enabled
            End Get
        End Property


        Private Shared Sub Tick(sender As Object, e As EventArgs)

            For Each stepOnce As Func(Of Boolean) In _steps.ToArray()
                If Not _steps.Contains(stepOnce) Then
                    Continue For
                End If
                If Not stepOnce() Then
                    _steps.Remove(stepOnce)
                End If
            Next

            If _steps.Count = 0 Then
                _clock.Stop()
            End If

        End Sub


        Private Const SPI_GETCLIENTAREAANIMATION As UInteger = &H1042

        <DllImport("user32.dll", SetLastError:=True)>
        Private Shared Function SystemParametersInfo(action As UInteger, param As UInteger, ByRef value As Integer, winIni As UInteger) As Boolean
        End Function

    End Class


    ' A control's hover, eased between rest (0) and hover (1) over
    ' UiMotion.HoverMilliseconds on UiMotion's clock (#142). Made on the
    ' control's first hover, so the hundreds of cards on the Board cost
    ' nothing until one is pointed at. Each step does only what the owner
    ' asked, by default invalidating the owner and nothing else. With
    ' motion off, under a contrast theme (looked at again on every step),
    ' or with the owner hidden, gone or without its window, it is at its
    ' end at once.
    Friend NotInheritable Class HoverTween

        Private ReadOnly _owner As Control
        Private ReadOnly _changed As Action
        Private ReadOnly _step As Func(Of Boolean)
        Private ReadOnly _watch As New Stopwatch()
        Private _from As Single = 0.0F
        Private _target As Single = 0.0F
        Private _amount As Single = 0.0F


        Public Sub New(owner As Control, Optional changed As Action = Nothing)
            _owner = owner
            _changed = If(changed, New Action(Sub() owner.Invalidate()))
            _step = AddressOf StepOnce
        End Sub


        ' Where the hover is: 0 at rest, 1 hovered.
        Public ReadOnly Property Amount As Single
            Get
                Return _amount
            End Get
        End Property


        ' Eases toward hovered or toward rest. Asked for where it is
        ' already headed, it does nothing.
        Public Sub MoveTo(hovered As Boolean)

            Dim target As Single = If(hovered, 1.0F, 0.0F)

            If target = _target AndAlso (_watch.IsRunning OrElse _amount = target) Then
                Return
            End If

            _target = target

            If Not CanMove() Then
                SnapTo(hovered)
                Return
            End If

            _from = _amount
            _watch.Restart()
            UiMotion.Run(_step)

        End Sub


        ' Goes to hovered or to rest at once, as the keyboard and a press do.
        Public Sub SnapTo(hovered As Boolean)

            Dim target As Single = If(hovered, 1.0F, 0.0F)

            _watch.Reset()
            UiMotion.Halt(_step)
            _target = target
            Arrive(target)

        End Sub


        Private Function CanMove() As Boolean
            Return UiMotion.Enabled AndAlso Not _owner.IsDisposed AndAlso _owner.IsHandleCreated AndAlso _owner.Visible
        End Function


        Private Sub Arrive(value As Single)
            If _amount <> value Then
                _amount = value
                If Not _owner.IsDisposed Then _changed()
            End If
        End Sub


        Private Function StepOnce() As Boolean

            If Not _watch.IsRunning Then
                Return False
            End If

            If Not CanMove() Then
                _watch.Reset()
                Arrive(_target)
                Return False
            End If

            Dim t As Double = _watch.Elapsed.TotalMilliseconds / Math.Max(1, UiMotion.HoverMilliseconds)

            If t >= 1.0 Then
                _watch.Reset()
                Arrive(_target)
                Return False
            End If

            Arrive(CSng(_from + (_target - _from) * UiMotion.EaseOut(t)))
            Return True

        End Function

    End Class

End Namespace
