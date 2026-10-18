Imports System
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

End Namespace
