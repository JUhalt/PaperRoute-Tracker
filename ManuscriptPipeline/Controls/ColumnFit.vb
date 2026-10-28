Imports System.Collections.Generic
Imports System.Linq
Imports System.Windows.Forms

Namespace Controls

    ' A details list's last column takes the width its other columns leave
    ' (#258), so the list never scrolls sideways for a few pixels, as it did
    ' once its vertical bar showed or a smaller screen narrowed its window.
    ' The column never gets narrower than its caption; below that it keeps
    ' its own width and the list scrolls.
    Friend NotInheritable Class ColumnFit

        Private Sub New()
        End Sub


        Public Shared Sub Attach(
            list As ListView
        )

            ' The last column's own width, kept when Fit first sees it: a
            ' list may be attached before its columns are added.
            Dim designed As New Dictionary(Of ColumnHeader, Integer)()

            ' A list's client area changes as its bars come and go, too. Once
            ' Windows has sized the list: a column changed while it does is
            ' given the width, but the list keeps the bar it measured before.
            AddHandler list.ClientSizeChanged,
                Sub(sender, e)
                    If list.IsHandleCreated Then
                        list.BeginInvoke(New Action(Sub() Fit(list, designed)))
                    Else
                        Fit(list, designed)
                    End If
                End Sub

            Fit(list, designed)

        End Sub


        Private Shared Sub Fit(
            list As ListView,
            designed As Dictionary(Of ColumnHeader, Integer)
        )

            If list.IsDisposed OrElse list.View <> View.Details OrElse list.Columns.Count = 0 Then
                Return
            End If

            Dim last As ColumnHeader =
                list.Columns(list.Columns.Count - 1)

            If Not designed.ContainsKey(last) Then
                designed.Add(last, last.Width)
            End If

            Dim others As Integer =
                list.Columns.Cast(Of ColumnHeader)().
                    Where(Function(column) column IsNot last).
                    Sum(Function(column) column.Width)

            Dim caption As Integer =
                TextRenderer.MeasureText(last.Text, list.Font).Width +
                list.LogicalToDeviceUnits(12)

            Dim fitted As Integer =
                list.ClientSize.Width - others

            ' Narrower only where that ends the sideways bar. A list that
            ' scrolls anyway shows its last column whole, as before #258.
            Dim width As Integer =
                If(fitted >= caption, fitted, designed(last))

            If last.Width <> width Then
                last.Width = width
            End If

        End Sub

    End Class

End Namespace
