Imports System.ComponentModel
Imports System.Drawing
Imports System.Windows.Forms
Imports System.Windows.Forms.Layout

Namespace Controls

    ' A board shelf: manuscript cards in as many equal columns as fit the
    ' visible width, scrolling vertically. Cards are never placed beyond the
    ' client width, so the shelf has no horizontal scroll range (#37).
    Friend Class ManuscriptShelfPanel
        Inherits FlowLayoutPanel

        Private Shared ReadOnly GridEngine As New CardGridLayout()

        ' The narrowest a card may become before a column is removed.
        <DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
        Public Property MinimumCardWidth As Integer = 300

        <DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
        Public Property CardGap As Integer = 12

        Public Overrides ReadOnly Property LayoutEngine As LayoutEngine
            Get
                Return GridEngine
            End Get
        End Property

        Protected Overrides Sub OnLayout(e As LayoutEventArgs)
            MyBase.OnLayout(e)

            ' ScrollableControl calculates the scrollbar range before the layout
            ' engine moves the cards. Reconcile the range with the new bounds,
            ' including when a vertical scrollbar appears and narrows the columns
            ' or a suspended render replaces every card. Keep normal scrolling:
            ' no scrollbar is forcibly hidden.
            If AutoScroll Then
                MyBase.OnLayout(e)
                If DisplayRectangle.Width > ClientSize.Width Then
                    MyBase.OnLayout(e)
                End If
            End If
        End Sub

        Private NotInheritable Class CardGridLayout
            Inherits LayoutEngine

            Public Overrides Function Layout(container As Object, layoutEventArgs As LayoutEventArgs) As Boolean

                Dim shelf As ManuscriptShelfPanel = DirectCast(container, ManuscriptShelfPanel)
                ' DisplayRectangle is already inset by Padding and offset by the
                ' scroll position.
                Dim display As Rectangle = shelf.DisplayRectangle
                Dim left As Integer = display.X
                Dim y As Integer = display.Y
                Dim available As Integer = Math.Max(1, shelf.ClientSize.Width - shelf.Padding.Horizontal)
                Dim gap As Integer = Math.Max(0, shelf.CardGap)
                Dim columns As Integer = Math.Max(1, (available + gap) \ (Math.Max(1, shelf.MinimumCardWidth) + gap))
                Dim cellWidth As Integer = Math.Max(1, (available - (columns - 1) * gap) \ columns)

                Dim column As Integer = 0
                Dim rowHeight As Integer = 0

                For Each child As Control In shelf.Controls
                    Dim margin As Padding = child.Margin

                    If Not TypeOf child Is RoundedPanel Then
                        ' Messages such as an empty shelf take a full row.
                        If column > 0 Then
                            y += rowHeight + gap
                            column = 0
                            rowHeight = 0
                        End If
                        child.SetBounds(left + margin.Left, y + margin.Top, Math.Max(1, available - margin.Horizontal), child.Height)
                        y += child.Height + margin.Vertical + gap
                        Continue For
                    End If

                    Dim x As Integer = left + column * (cellWidth + gap)
                    child.SetBounds(x + margin.Left, y + margin.Top, Math.Max(1, cellWidth - margin.Horizontal), child.Height)
                    rowHeight = Math.Max(rowHeight, child.Height + margin.Vertical)
                    column += 1

                    If column = columns Then
                        y += rowHeight + gap
                        column = 0
                        rowHeight = 0
                    End If
                Next

                Return False

            End Function

        End Class

    End Class

End Namespace
