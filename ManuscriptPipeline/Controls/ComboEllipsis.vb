Imports System.Drawing
Imports System.Windows.Forms
Imports ManuscriptPipeline.Services

Namespace Controls

    ' A drop-down list's items end in an ellipsis when they are wider than
    ' the box (#258), rather than being cut mid-letter where Windows clips
    ' them: a long version label or journal name. Each item is drawn where
    ' Windows draws it, in the box's colors or the selection's.
    Friend NotInheritable Class ComboEllipsis

        Private Const Flags As TextFormatFlags =
            TextFormatFlags.VerticalCenter Or
            TextFormatFlags.SingleLine Or
            TextFormatFlags.EndEllipsis Or
            TextFormatFlags.NoPrefix

        ' The blue Windows' own drop-down lists select in, in Dark.
        Private Shared ReadOnly DarkSelection As Color =
            Color.FromArgb(0, 120, 215)


        Private Sub New()
        End Sub


        Public Shared Sub Attach(
            combo As ComboBox
        )

            combo.DrawMode =
                DrawMode.OwnerDrawFixed

            AddHandler combo.DrawItem,
                AddressOf DrawItem

        End Sub


        Private Shared Sub DrawItem(
            sender As Object,
            e As DrawItemEventArgs
        )

            Dim combo As ComboBox =
                DirectCast(sender, ComboBox)

            ' .NET's Dark colors pair the selection's blue with black text,
            ' about 3.6:1, so a selected item is drawn white on the blue of
            ' Windows' own lists, as these were before they drew their items
            ' (#265).
            Dim selectedInDark As Boolean =
                (e.State And DrawItemState.Selected) = DrawItemState.Selected AndAlso
                UiTheme.IsDark()

            Using fill As New SolidBrush(If(selectedInDark, DarkSelection, e.BackColor))
                e.Graphics.FillRectangle(fill, e.Bounds)
            End Using

            If e.Index >= 0 Then

                ' In the box itself, from the field's left edge, as Windows
                ' draws it: TextRenderer's padding moved the item 3 pixels
                ' right and ended it a few pixels early, which hid a
                ' submission's year at 125% (#265). The list keeps it.
                Dim inBox As Boolean =
                    (e.State And DrawItemState.ComboBoxEdit) = DrawItemState.ComboBoxEdit

                TextRenderer.DrawText(
                    e.Graphics,
                    combo.GetItemText(combo.Items(e.Index)),
                    e.Font,
                    e.Bounds,
                    If(selectedInDark, Color.White, e.ForeColor),
                    If(inBox, Flags Or TextFormatFlags.NoPadding, Flags)
                )

            End If

            e.DrawFocusRectangle()

        End Sub

    End Class

End Namespace
