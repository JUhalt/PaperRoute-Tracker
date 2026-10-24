Imports System
Imports System.Runtime.InteropServices
Imports System.Windows.Forms

Namespace Controls

    ' Keeping a control's windows where they belong (#133). WinForms makes a
    ' control's window as soon as the control is shown, and a form's even
    ' when it is only marked visible; with no window around it yet, the
    ' window is made on WinForms' parking window and moved into place
    ' later. Removing a control before disposing it moves its windows to
    ' the parking window again before they are destroyed. On a slow
    ' machine every window made, moved or destroyed costs milliseconds.
    Friend NotInheritable Class InPlace

        Private Sub New()
        End Sub


        ' Runs an action once a control's parent has a window: at once if it
        ' has one, otherwise when the control is placed in a parent that has
        ' one, or when its parent is given one. What the action shows or
        ' checks then makes its windows where they stay.
        Public Shared Sub WhenAttached(
            control As Control,
            action As Action
        )

            If control.Parent IsNot Nothing AndAlso control.Parent.IsHandleCreated Then
                action()
                Return
            End If

            Dim watched As Control = Nothing
            Dim parentChanged As EventHandler = Nothing
            Dim handleCreated As EventHandler = Nothing

            Dim finish As Action =
                Sub()
                    RemoveHandler control.ParentChanged, parentChanged
                    If watched IsNot Nothing Then RemoveHandler watched.HandleCreated, handleCreated
                    watched = Nothing
                    If Not control.IsDisposed Then action()
                End Sub

            handleCreated =
                Sub(sender, e)
                    finish()
                End Sub

            parentChanged =
                Sub(sender, e)
                    If watched IsNot Nothing Then RemoveHandler watched.HandleCreated, handleCreated
                    watched = control.Parent
                    If watched Is Nothing Then Return
                    If watched.IsHandleCreated Then
                        finish()
                    Else
                        AddHandler watched.HandleCreated, handleCreated
                    End If
                End Sub

            AddHandler control.ParentChanged, parentChanged

            If control.Parent IsNot Nothing Then
                parentChanged(control, EventArgs.Empty)
            End If

        End Sub


        ' Gives a control that starts hidden its own window as soon as its
        ' parent has one, so a sibling after it that is shown later keeps its
        ' place. WinForms moves a window made while no sibling before it has
        ' one to the front of its parent, ahead of them, so a page that shows
        ' parts as records need them would otherwise end up in another order
        ' than one built for the record it shows.
        Public Shared Sub HoldPlace(
            control As Control
        )

            WhenAttached(
                control,
                Sub()
                    If Not control.IsHandleCreated Then
                        Dim made As IntPtr = control.Handle
                    End If
                End Sub)

        End Sub


        ' Makes the windows of a control that is in the window but hidden, as
        ' showing it would make them, a few at a time: parent first, so each
        ' is made inside its parent, where it stays, and never inside a part
        ' made hidden (a section not shown, a part only some records show),
        ' which showing the control would not enter either. Each call makes
        ' windows for about the given time and returns whether every one is
        ' made; a later call makes the rest.
        Public Shared Function MakeWindows(
            root As Control,
            milliseconds As Integer
        ) As Boolean

            If root.IsDisposed OrElse root.Parent Is Nothing OrElse Not root.Parent.IsHandleCreated Then
                Return False
            End If

            Dim watch As Diagnostics.Stopwatch = Diagnostics.Stopwatch.StartNew()
            Dim pending As New Collections.Generic.Stack(Of Control)()
            pending.Push(root)

            While pending.Count > 0

                Dim control As Control = pending.Pop()

                If control.IsDisposed Then
                    Continue While
                End If

                If Not control.IsHandleCreated Then
                    If watch.ElapsedMilliseconds >= milliseconds Then
                        Return False
                    End If
                    Dim made As IntPtr = control.Handle
                End If

                ' The root is hidden itself; below it, a part made hidden
                ' keeps its own parts without windows until it shows.
                If control IsNot root AndAlso (GetWindowLong(control.Handle, GWL_STYLE) And WS_VISIBLE) = 0 Then
                    Continue While
                End If

                For index As Integer = control.Controls.Count - 1 To 0 Step -1
                    pending.Push(control.Controls(index))
                Next

            End While

            Return True

        End Function

        Private Const GWL_STYLE As Integer = -16
        Private Const WS_VISIBLE As Integer = &H10000000

        <DllImport("user32.dll")>
        Private Shared Function GetWindowLong(handle As IntPtr, index As Integer) As Integer
        End Function


        ' Disposes a control where it is: its windows are destroyed in place
        ' and it then leaves its parent. With focus inside it, it is removed
        ' first, as before, so focus moves on the way a removal moves it.
        Public Shared Sub Dispose(
            control As Control
        )

            Dim container As ContainerControl = TryCast(control.Parent?.GetContainerControl(), ContainerControl)
            Dim active As Control = container?.ActiveControl

            If active IsNot Nothing AndAlso (active Is control OrElse control.Contains(active)) Then
                control.Parent.Controls.Remove(control)
            End If

            control.Dispose()

        End Sub

    End Class


    ' Holds a window's drawing while a page switch changes what is under
    ' it, and asks for one paint of all of it once released (#133). Windows
    ' paints a window shown, resized or brought forward there and then,
    ' before the switch returns, and on this machine that drawing waited on
    ' the screen about half a second at times. With drawing held
    ' (WM_SETREDRAW) nothing under the window is painted until it is
    ' released; the paint asked for then is posted, so the switch returns
    ' first and the window paints once, whole.
    '
    ' Held, a window loses WS_VISIBLE, so it and everything in it read as
    ' not visible to Windows: Control.Focus refuses a control it cannot
    ' see. So a hold comes only after focus has moved out of what goes
    ' away, and on the page area or the shelves' host, never the form.
    ' (Select and SelectNextControl go by WinForms' own record of what is
    ' visible, and do move the keyboard to a control under a hold, which
    ' the Board's hide and return use, #213.) A window with no handle, or
    ' not visible, is left alone: releasing a hold on a hidden window
    ' would mark it visible.
    Friend NotInheritable Class RedrawHold
        Implements IDisposable

        Private Const WM_SETREDRAW As Integer = &HB
        Private Const RDW_INVALIDATE As UInteger = &H1
        Private Const RDW_ERASE As UInteger = &H4
        Private Const RDW_ALLCHILDREN As UInteger = &H80
        Private Const RDW_FRAME As UInteger = &H400

        Private _handle As IntPtr = IntPtr.Zero


        Public Sub New(
            control As Control
        )

            If control Is Nothing OrElse Not control.IsHandleCreated OrElse Not IsWindowVisible(control.Handle) Then
                Return
            End If

            _handle = control.Handle
            SendMessage(_handle, WM_SETREDRAW, IntPtr.Zero, IntPtr.Zero)

        End Sub


        Public Sub Dispose() Implements IDisposable.Dispose

            If _handle = IntPtr.Zero Then
                Return
            End If

            Dim handle As IntPtr = _handle
            _handle = IntPtr.Zero

            SendMessage(handle, WM_SETREDRAW, New IntPtr(1), IntPtr.Zero)
            RedrawWindow(handle, IntPtr.Zero, IntPtr.Zero, RDW_INVALIDATE Or RDW_ERASE Or RDW_FRAME Or RDW_ALLCHILDREN)

        End Sub


        <DllImport("user32.dll")>
        Private Shared Function SendMessage(handle As IntPtr, message As Integer, wParam As IntPtr, lParam As IntPtr) As IntPtr
        End Function

        <DllImport("user32.dll")>
        Private Shared Function IsWindowVisible(handle As IntPtr) As Boolean
        End Function

        <DllImport("user32.dll")>
        Private Shared Function RedrawWindow(handle As IntPtr, updateRect As IntPtr, updateRegion As IntPtr, flags As UInteger) As Boolean
        End Function

    End Class

End Namespace
