Imports System.Collections.Generic
Imports System.Drawing
Imports System.Windows.Forms
Imports ManuscriptPipeline.Services

Namespace Controls

    ' Guidance shown inside a list or table while it is empty: what belongs
    ' there and how to add the first item. It never adds data and disappears
    ' as soon as the first item appears.
    Friend NotInheritable Class EmptyHint

        Private NotInheritable Class Entry
            Public Target As Control
            Public Hint As Label
            Public Text As Func(Of String)
        End Class

        Private Shared ReadOnly Entries As New List(Of Entry)()
        Private Shared _idleHooked As Boolean

        Private Sub New()
        End Sub

        Public Shared Sub Attach(target As Control, text As String)
            Attach(target, Function() text)
        End Sub

        ' The text is read each time the hint is shown, so it can describe a
        ' filter or the current state.
        Public Shared Sub Attach(target As Control, text As Func(Of String))

            If target Is Nothing OrElse text Is Nothing Then
                Return
            End If

            Dim hint As New Label With {
                .AutoSize = False,
                .TextAlign = ContentAlignment.MiddleCenter,
                .UseMnemonic = False,
                .Padding = New Padding(16),
                .Visible = False,
                .AccessibleRole = AccessibleRole.StaticText
            }

            target.Controls.Add(hint)

            Dim entry As New Entry With {.Target = target, .Hint = hint, .Text = text}
            Entries.Add(entry)

            AddHandler target.Disposed,
                Sub(sender, e)
                    Entries.Remove(entry)
                End Sub

            AddHandler target.Resize,
                Sub(sender, e)
                    Update(entry)
                End Sub

            ' A table reports its rows, so its hint follows them at once.
            Dim grid As DataGridView = TryCast(target, DataGridView)
            If grid IsNot Nothing Then
                AddHandler grid.RowsAdded, Sub(sender, e) Update(entry)
                AddHandler grid.RowsRemoved, Sub(sender, e) Update(entry)
            End If

            If Not _idleHooked Then
                _idleHooked = True
                AddHandler Application.Idle, AddressOf SyncAll
            End If

            Update(entry)

        End Sub

        ' Lists change their items without events, so the hints follow them
        ' when the application is idle.
        Private Shared Sub SyncAll(sender As Object, e As EventArgs)
            For Each entry As Entry In Entries.ToArray()
                Update(entry)
            Next
        End Sub

        Private Shared Sub Update(entry As Entry)

            Dim target As Control = entry.Target

            If target.IsDisposed OrElse entry.Hint.IsDisposed Then
                Return
            End If

            Dim empty As Boolean = ItemCount(target) = 0

            If empty Then
                Dim text As String = entry.Text()
                If entry.Hint.Text <> text Then entry.Hint.Text = text
                entry.Hint.BackColor = BackgroundOf(target)
                entry.Hint.ForeColor = UiTheme.SecondaryText()

                ' A table keeps its column headings visible above the hint.
                Dim top As Integer = 0
                Dim grid As DataGridView = TryCast(target, DataGridView)
                If grid IsNot Nothing AndAlso grid.ColumnHeadersVisible Then
                    top = grid.ColumnHeadersHeight
                End If

                Dim bounds As New Rectangle(0, top, target.ClientSize.Width, Math.Max(0, target.ClientSize.Height - top))
                If entry.Hint.Bounds <> bounds Then entry.Hint.Bounds = bounds
            End If

            If entry.Hint.Visible <> empty Then
                entry.Hint.Visible = empty
            End If

        End Sub

        Private Shared Function ItemCount(target As Control) As Integer

            Dim list As ListBox = TryCast(target, ListBox)
            If list IsNot Nothing Then Return list.Items.Count

            Dim grid As DataGridView = TryCast(target, DataGridView)
            If grid IsNot Nothing Then Return grid.Rows.Count

            Dim view As ListView = TryCast(target, ListView)
            If view IsNot Nothing Then Return view.Items.Count

            Return 1

        End Function

        Private Shared Function BackgroundOf(target As Control) As Color

            Dim grid As DataGridView = TryCast(target, DataGridView)
            If grid IsNot Nothing Then Return grid.BackgroundColor

            Return target.BackColor

        End Function

    End Class

End Namespace
