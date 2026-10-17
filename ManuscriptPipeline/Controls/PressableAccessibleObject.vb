Imports System
Imports System.Windows.Forms

Namespace Controls

    ' What a screen reader sees of a control PaperRoute draws as a button
    ' (#139): its default action, which Narrator's Invoke calls, presses it
    ' as a click does. A control drawn this way otherwise has a button's
    ' role and nothing to press.
    Friend NotInheritable Class PressableAccessibleObject
        Inherits Control.ControlAccessibleObject

        Private ReadOnly _press As Action


        Public Sub New(
            owner As Control,
            press As Action
        )

            MyBase.New(owner)

            _press =
                press

        End Sub


        Public Overrides ReadOnly Property DefaultAction As String
            Get
                Return "Press"
            End Get
        End Property


        ' A disabled control is not pressed, as a click would not press it.
        Public Overrides Sub DoDefaultAction()

            If Owner IsNot Nothing AndAlso Owner.Enabled Then
                _press()
            End If

        End Sub

    End Class

End Namespace
