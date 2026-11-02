Imports System.Collections.Generic
Imports System.Drawing
Imports System.Linq
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

            ' Whether the hint was last shown. Its Visible reads False while
            ' a parent is hidden, as the manuscript page is while it loads
            ' the next record (#133), so it is no guide to what was set.
            Public Shown As Boolean
        End Class

        Private Shared ReadOnly Entries As New List(Of Entry)()
        Private Shared _idleHooked As Boolean

        ' How a hint's text is wrapped, both where it is measured
        ' (NeededHeight) and where it is drawn (HintLabel): centred, and
        ' broken only between words.
        Private Const HintFlags As TextFormatFlags =
            TextFormatFlags.WordBreak Or
            TextFormatFlags.HorizontalCenter Or
            TextFormatFlags.NoPrefix

        Private Sub New()
        End Sub

        ' The hint draws its text as NeededHeight measures it (#278). A
        ' Label lays its lines out with flags of its own, Windows'
        ' edit-control layout among them, which can break them where the
        ' measure does not: at 150% in the certification VM the empty
        ' Correspondence & Files list, given the height its hint measured,
        ' ended "...or drag files her", its last word cut at the right. Its
        ' text is wrapped at the width inside its padding, as measured, and
        ' centred in the height there, as a Label centres it.
        Private NotInheritable Class HintLabel
            Inherits Label

            Protected Overrides Sub OnPaint(e As PaintEventArgs)

                Dim face As Rectangle = TextArea(Me)
                Dim tall As Integer = TextSize(Text, Font, face.Width).Height
                Dim top As Integer = face.Top + Math.Max(0, face.Height \ 2 - tall \ 2)

                TextRenderer.DrawText(
                    e.Graphics,
                    Text,
                    Font,
                    Rectangle.FromLTRB(face.Left, top, face.Right, face.Bottom),
                    ForeColor,
                    HintFlags
                )

            End Sub

        End Class

        ' Where a hint's text goes: inside its padding.
        Private Shared Function TextArea(hint As Label) As Rectangle

            Return New Rectangle(
                hint.Padding.Left,
                hint.Padding.Top,
                hint.ClientSize.Width - hint.Padding.Horizontal,
                hint.ClientSize.Height - hint.Padding.Vertical
            )

        End Function

        ' The size the text takes wrapped at the width, as a hint draws it.
        Private Shared Function TextSize(text As String, font As Font, width As Integer) As Size

            Return TextRenderer.MeasureText(
                text,
                font,
                New Size(width, Integer.MaxValue),
                HintFlags
            )

        End Function

        Public Shared Sub Attach(target As Control, text As String)
            Attach(target, Function() text)
        End Sub

        ' The text is read each time the hint is shown, so it can describe a
        ' filter or the current state.
        Public Shared Sub Attach(target As Control, text As Func(Of String))

            If target Is Nothing OrElse text Is Nothing Then
                Return
            End If

            Dim hint As New HintLabel With {
                .AutoSize = False,
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

            ' A list not yet in any window shows its hint once it has one.
            AddHandler target.HandleCreated,
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

        ' Brings a list's hint up to date at once, as a form that loads
        ' another record into its lists does (#133).
        Public Shared Sub Refresh(target As Control)
            For Each entry As Entry In Entries.Where(Function(item) item.Target Is target).ToArray()
                Update(entry)
            Next
        End Sub

        ' The height a list's hint needs to be read whole at the list's
        ' width: its text, wrapped inside its padding as the hint draws it
        ' (#278), and the padding, so a window can give an empty list that
        ' much (#93). None while the list has items or no hint, or before it
        ' has a width to wrap to.
        Public Shared Function NeededHeight(target As Control) As Integer

            If target Is Nothing OrElse ItemCount(target) > 0 Then
                Return 0
            End If

            For Each entry As Entry In Entries.Where(Function(item) item.Target Is target)

                Dim width As Integer =
                    HintBounds(target).Width - entry.Hint.Padding.Horizontal

                If width <= 0 Then
                    Return 0
                End If

                Return TextSize(entry.Text(), entry.Hint.Font, width).Height +
                    entry.Hint.Padding.Vertical

            Next

            Return 0

        End Function

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

            ' Shown while its list is in no window yet, the hint would make
            ' its window on WinForms' parking window (#133); it waits until
            ' the list has a window or a form around it.
            If Not target.IsHandleCreated AndAlso target.FindForm() Is Nothing Then
                Return
            End If

            Dim empty As Boolean = ItemCount(target) = 0

            ' The hint keeps its list's colors while hidden too, so none is
            ' left from before the list was styled, or from a record the
            ' list showed before (#133).
            entry.Hint.BackColor = BackgroundOf(target)
            entry.Hint.ForeColor = UiTheme.SecondaryText()

            If empty Then
                Dim text As String = entry.Text()
                If entry.Hint.Text <> text Then entry.Hint.Text = text

                Dim bounds As Rectangle = HintBounds(target)
                If entry.Hint.Bounds <> bounds Then entry.Hint.Bounds = bounds
            End If

            If entry.Shown <> empty Then
                entry.Shown = empty
                entry.Hint.Visible = empty
            End If

        End Sub

        ' Where a list's hint lies: over the whole list, but for a table's
        ' column headings, which stay visible above it, and a table's frame.
        ' A table draws its frame inside its own client area, so a hint laid
        ' over all of it hid the frame on its left, right and bottom (#266).
        Private Shared Function HintBounds(target As Control) As Rectangle

            Dim frame As Integer = 0
            Dim top As Integer = 0
            Dim grid As DataGridView = TryCast(target, DataGridView)

            If grid IsNot Nothing Then
                ' The frame UiPolish draws is a pixel wide.
                If grid.BorderStyle <> BorderStyle.None Then frame = 1
                top = frame + If(grid.ColumnHeadersVisible, grid.ColumnHeadersHeight, 0)
            End If

            Return New Rectangle(
                frame,
                top,
                Math.Max(0, target.ClientSize.Width - 2 * frame),
                Math.Max(0, target.ClientSize.Height - top - frame)
            )

        End Function

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
