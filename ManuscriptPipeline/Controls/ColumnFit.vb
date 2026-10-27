Imports System.Linq
Imports System.Windows.Forms

Namespace Controls

    ' A details list's last column takes the width its other columns leave
    ' (#258), so the list never scrolls sideways for a few pixels, as it did
    ' once its vertical bar showed or a smaller screen narrowed its window.
    ' The column never gets narrower than its caption; below that the list
    ' scrolls.
    Friend NotInheritable Class ColumnFit

        Private Sub New()
        End Sub


        Public Shared Sub Attach(
            list As ListView
        )

            ' A list's client area changes as its bars come and go, too. Once
            ' Windows has sized the list: a column changed while it does is
            ' given the width, but the list keeps the bar it measured before.
            AddHandler list.ClientSizeChanged,
                Sub(sender, e)
                    If list.IsHandleCreated Then
                        list.BeginInvoke(New Action(Sub() Fit(list)))
                    Else
                        Fit(list)
                    End If
                End Sub

            Fit(list)

        End Sub


        Private Shared Sub Fit(
            list As ListView
        )

            If list.IsDisposed OrElse list.View <> View.Details OrElse list.Columns.Count = 0 Then
                Return
            End If

            Dim last As ColumnHeader =
                list.Columns(list.Columns.Count - 1)

            Dim others As Integer =
                list.Columns.Cast(Of ColumnHeader)().
                    Where(Function(column) column IsNot last).
                    Sum(Function(column) column.Width)

            Dim caption As Integer =
                TextRenderer.MeasureText(last.Text, list.Font).Width +
                list.LogicalToDeviceUnits(12)

            Dim width As Integer =
                Math.Max(caption, list.ClientSize.Width - others)

            If last.Width <> width Then
                last.Width = width
            End If

        End Sub

    End Class

End Namespace
