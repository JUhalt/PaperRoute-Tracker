Imports System.Windows.Forms

Namespace Controls

    ' A drop-down list's items end in an ellipsis when they are wider than
    ' the box (#258), rather than being cut mid-letter where Windows clips
    ' them: a long version label or journal name. Each item is drawn in the
    ' colors Windows gives it, the box's own or the selection's.
    Friend NotInheritable Class ComboEllipsis

        Private Const Flags As TextFormatFlags =
            TextFormatFlags.VerticalCenter Or
            TextFormatFlags.SingleLine Or
            TextFormatFlags.EndEllipsis Or
            TextFormatFlags.NoPrefix


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

            e.DrawBackground()

            If e.Index >= 0 Then

                TextRenderer.DrawText(
                    e.Graphics,
                    combo.GetItemText(combo.Items(e.Index)),
                    e.Font,
                    e.Bounds,
                    e.ForeColor,
                    Flags
                )

            End If

            e.DrawFocusRectangle()

        End Sub

    End Class

End Namespace
