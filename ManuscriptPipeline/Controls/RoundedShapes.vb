Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports ManuscriptPipeline.Services

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


        ' Under a contrast theme a filled chip or pill is the window color,
        ' so it keeps its shape with an outline in its text color (#191),
        ' one sharp pixel just inside the bounds. In Light and Dark nothing
        ' is drawn.
        Public Shared Sub OutlineUnderContrast(
            g As Graphics,
            bounds As RectangleF,
            radius As Single,
            ink As Color
        )

            If Not UiTheme.IsHighContrast() Then
                Return
            End If

            Dim smoothing As SmoothingMode = g.SmoothingMode
            Dim offset As PixelOffsetMode = g.PixelOffsetMode

            g.SmoothingMode = SmoothingMode.AntiAlias
            g.PixelOffsetMode = PixelOffsetMode.Half

            Using path As GraphicsPath = Create(New RectangleF(bounds.X + 0.5F, bounds.Y + 0.5F, bounds.Width - 1.0F, bounds.Height - 1.0F), radius - 0.5F),
                  pen As New Pen(ink)
                g.DrawPath(pen, path)
            End Using

            g.SmoothingMode = smoothing
            g.PixelOffsetMode = offset

        End Sub

    End Class

End Namespace
