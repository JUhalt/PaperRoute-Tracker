Imports System.Drawing
Imports System.Drawing.Drawing2D

Namespace Controls

    Friend NotInheritable Class RoundedShapes

        Private Sub New()
        End Sub

        ' A rounded rectangle path. The radius is clamped so short controls
        ' become pills rather than drawing inverted arcs.
        Public Shared Function Create(
            bounds As RectangleF,
            radius As Single
        ) As GraphicsPath

            Dim path As New GraphicsPath()
            Dim r As Single = Math.Max(0.0F, Math.Min(radius, Math.Min(bounds.Width, bounds.Height) / 2.0F))

            If r <= 0.0F Then
                path.AddRectangle(bounds)
                Return path
            End If

            Dim d As Single = r * 2.0F
            path.AddArc(bounds.Left, bounds.Top, d, d, 180, 90)
            path.AddArc(bounds.Right - d, bounds.Top, d, d, 270, 90)
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90)
            path.AddArc(bounds.Left, bounds.Bottom - d, d, d, 90, 90)
            path.CloseFigure()

            Return path

        End Function

    End Class

End Namespace
