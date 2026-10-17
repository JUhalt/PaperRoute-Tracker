Imports System
Imports System.ComponentModel
Imports System.Windows.Forms

Namespace Controls

    ' A Board card whose context menu is built when it is first needed:
    ' when the card is given its window, so a right-click or the menu key
    ' finds it on the card and on each of its parts, or when code asks for
    ' it first. Building a menu for each of 500 cards was most of the time
    ' it took to build the Board (#133). The card and every part share the
    ' one menu, which goes with the card.
    Friend NotInheritable Class CardPanel
        Inherits RoundedPanel

        Private ReadOnly _buildMenu As Func(Of ContextMenuStrip)
        Private _menu As ContextMenuStrip = Nothing


        Public Sub New(
            buildMenu As Func(Of ContextMenuStrip)
        )

            _buildMenu = buildMenu

        End Sub


        <DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
        Public Overrides Property ContextMenuStrip As ContextMenuStrip

            Get
                EnsureMenu()
                Return MyBase.ContextMenuStrip
            End Get

            Set(value As ContextMenuStrip)
                MyBase.ContextMenuStrip = value
            End Set

        End Property


        ' True once the menu has been built.
        Friend ReadOnly Property HasMenu As Boolean
            Get
                Return _menu IsNot Nothing
            End Get
        End Property


        Protected Overrides Sub OnHandleCreated(
            e As EventArgs
        )

            ' Parts get their windows after the card, so each finds the menu
            ' in place when it is right-clicked.
            EnsureMenu()
            MyBase.OnHandleCreated(e)

        End Sub


        Private Sub EnsureMenu()

            If _menu IsNot Nothing OrElse _buildMenu Is Nothing OrElse IsDisposed OrElse Disposing Then
                Return
            End If

            _menu = _buildMenu()
            MyBase.ContextMenuStrip = _menu

            For Each part As Control In Controls
                part.ContextMenuStrip = _menu
            Next

        End Sub


        Protected Overrides Sub Dispose(
            disposing As Boolean
        )

            Dim menu As ContextMenuStrip = _menu
            _menu = Nothing

            MyBase.Dispose(disposing)

            If disposing Then
                menu?.Dispose()
            End If

        End Sub

    End Class

End Namespace
