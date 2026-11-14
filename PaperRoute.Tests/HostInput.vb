Imports System
Imports System.Runtime.InteropServices
Imports System.Windows.Forms

' The key state a posted key is handled in (#340, #318). The UI tests post
' keys to windows that are off the screen and never activated, so focus
' and the foreground window do not matter to them, but WinForms still
' reads this thread's key state as it handles the key: Control.ModifierKeys
' goes into the key's KeyData, and with Ctrl or Alt there a container
' ignores Tab and the arrow keys, while Shift turns Tab back; and a
' RadioButton reached by an arrow key checks itself only while
' Control.MouseButtons is None and Tab is up. Two such tests failed on
' release candidate builds while the PC was in use. How the person's
' input could reach a test thread's key state was not confirmed, so the
' key helpers set that state themselves just before each post. The table
' is the calling thread's own, and StaRunner gives every test a new
' thread, so nothing set here outlives the test.
Friend Module HostInput

    ' What Describe reads when nothing is held.
    Public Const AllReleased As String =
        "buttons None, modifiers None, Tab up"

    Private Const Down As Byte =
        &H80

    ' The mouse buttons, Tab, Shift, Ctrl, Alt and the Windows keys, each
    ' by its generic and its left and right virtual key.
    Private ReadOnly Released As Keys() = {
        Keys.LButton, Keys.RButton, Keys.MButton, Keys.XButton1, Keys.XButton2,
        Keys.Tab,
        Keys.ShiftKey, Keys.LShiftKey, Keys.RShiftKey,
        Keys.ControlKey, Keys.LControlKey, Keys.RControlKey,
        Keys.Menu, Keys.LMenu, Keys.RMenu,
        Keys.LWin, Keys.RWin
    }


    ' Releases every key in Released in this thread's key state, then holds
    ' the modifiers given (Shift, Control, Alt), as a key posted next
    ' should meet them. Other keys, Caps Lock among them, are left as they
    ' are: the helpers that call this post no letters.
    Public Sub HoldOnly(
        Optional held As Keys = Keys.None
    )

        Dim state(255) As Byte
        GetKeyboardState(state)

        For Each key As Keys In Released
            state(CInt(key)) = 0
        Next

        If (held And Keys.Shift) = Keys.Shift Then
            state(CInt(Keys.ShiftKey)) = Down
            state(CInt(Keys.LShiftKey)) = Down
        End If

        If (held And Keys.Control) = Keys.Control Then
            state(CInt(Keys.ControlKey)) = Down
            state(CInt(Keys.LControlKey)) = Down
        End If

        If (held And Keys.Alt) = Keys.Alt Then
            state(CInt(Keys.Menu)) = Down
            state(CInt(Keys.LMenu)) = Down
        End If

        SetKeyboardState(state)

    End Sub


    ' Marks the keys down in this thread's key state, as a button or key
    ' held on the host would read there. For the guard tests: the key
    ' helpers must release them before their post.
    Public Sub MarkDown(
        ParamArray keys As Keys()
    )

        Dim state(255) As Byte
        GetKeyboardState(state)

        For Each key As Keys In keys
            state(CInt(key)) = CByte(state(CInt(key)) Or Down)
        Next

        SetKeyboardState(state)

    End Sub


    ' Whether the key is down in this thread's key state.
    Public Function IsDown(
        key As Keys
    ) As Boolean

        Return GetKeyState(CInt(key)) < 0

    End Function


    ' The buttons, modifiers and Tab as WinForms reads them now on this
    ' thread, for a failure message.
    Public Function Describe() As String

        Return "buttons " & Control.MouseButtons.ToString() &
            ", modifiers " & Control.ModifierKeys.ToString() &
            ", Tab " & If(IsDown(Keys.Tab), "down", "up")

    End Function


    <DllImport("user32.dll")>
    Private Function GetKeyboardState(state As Byte()) As Boolean
    End Function

    <DllImport("user32.dll")>
    Private Function SetKeyboardState(state As Byte()) As Boolean
    End Function

    <DllImport("user32.dll")>
    Private Function GetKeyState(virtualKey As Integer) As Short
    End Function

End Module
