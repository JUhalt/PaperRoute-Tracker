Imports System.Windows.Forms

Namespace Controls

    Friend Class ManuscriptShelfPanel
        Inherits FlowLayoutPanel

        Protected Overrides Sub OnLayout(e As LayoutEventArgs)
            MyBase.OnLayout(e)

            ' FlowLayoutPanel refreshes its cached content bounds after
            ' ScrollableControl has calculated the scrollbar range. Reconcile
            ' the range with those new bounds, including when a suspended render
            ' replaces every card or a resize narrows the existing rows.
            ' Keep normal scrolling: no scrollbar is forcibly hidden.
            If AutoScroll Then
                MyBase.OnLayout(e)
            End If
        End Sub
    End Class

End Namespace
