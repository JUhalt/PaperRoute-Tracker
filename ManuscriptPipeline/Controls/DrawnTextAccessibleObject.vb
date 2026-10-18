Imports System.Drawing
Imports System.Windows.Forms

Namespace Controls

    ' A line of text a control draws rather than hosting a Label for (#133):
    ' a heading, a description or a count. A screen reader reads it as
    ' static text named by what it shows, at the place it is drawn, among
    ' its owner's children by its number there.
    Friend NotInheritable Class DrawnTextAccessibleObject
        Inherits AccessibleObject

        Private ReadOnly _owner As Control
        Private ReadOnly _index As Integer
        Private ReadOnly _text As Func(Of String)
        Private ReadOnly _bounds As Func(Of Rectangle)

        ' The text and where it is drawn, in the owner's client coordinates,
        ' are read each time they are asked for.
        Public Sub New(owner As Control, index As Integer, text As Func(Of String), bounds As Func(Of Rectangle))
            _owner = owner
            _index = index
            _text = text
            _bounds = bounds
        End Sub

        Public Overrides Property Name As String
            Get
                Return _text()
            End Get
            Set(value As String)
                ' Named by what it shows.
            End Set
        End Property

        Public Overrides ReadOnly Property Role As AccessibleRole
            Get
                Return AccessibleRole.StaticText
            End Get
        End Property

        Public Overrides ReadOnly Property State As AccessibleStates
            Get
                Dim states As AccessibleStates = AccessibleStates.ReadOnly
                If Not _owner.ClientRectangle.IntersectsWith(_bounds()) Then states = states Or AccessibleStates.Offscreen
                Return states
            End Get
        End Property

        Public Overrides ReadOnly Property Bounds As Rectangle
            Get
                If Not _owner.IsHandleCreated Then Return Rectangle.Empty
                Return _owner.RectangleToScreen(_bounds())
            End Get
        End Property

        Public Overrides ReadOnly Property Parent As AccessibleObject
            Get
                Return _owner.AccessibilityObject
            End Get
        End Property

        Public Overrides Function GetChildCount() As Integer
            Return 0
        End Function

        Public Overrides Function Navigate(navdir As AccessibleNavigation) As AccessibleObject
            Select Case navdir
                Case AccessibleNavigation.Next, AccessibleNavigation.Right, AccessibleNavigation.Down
                    Return _owner.AccessibilityObject.GetChild(_index + 1)
                Case AccessibleNavigation.Previous, AccessibleNavigation.Left, AccessibleNavigation.Up
                    Return _owner.AccessibilityObject.GetChild(_index - 1)
            End Select
            Return Nothing
        End Function

    End Class

End Namespace
