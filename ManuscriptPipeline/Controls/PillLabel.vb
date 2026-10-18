Imports System
Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Windows.Forms
Imports ManuscriptPipeline.Services

Namespace Controls

    Public Class PillLabel
        Inherits Label

        Public Sub New()

            Me.AutoSize = False
            Me.TextAlign = ContentAlignment.MiddleCenter

        End Sub


        Protected Overrides Sub OnSizeChanged(
            e As EventArgs
        )

            MyBase.OnSizeChanged(e)

            UpdateShape()

        End Sub


        ' A stage's fill is the window color under a contrast theme, so
        ' the pill is outlined in its text color there (#191).
        Protected Overrides Sub OnPaint(
            e As PaintEventArgs
        )

            MyBase.OnPaint(e)

            RoundedShapes.OutlineUnderContrast(
                e.Graphics,
                New RectangleF(0, 0, Width, Height),
                Height / 2.0F,
                ForeColor
            )

        End Sub


        Private Sub UpdateShape()

            If Width <= 0 OrElse Height <= 0 Then
                Return
            End If

            Dim radius As Integer =
                Height \ 2

            Dim bounds As New Rectangle(
                0,
                0,
                Width,
                Height
            )

            Using path As GraphicsPath =
                CreateRoundedRectangle(
                    bounds,
                    radius
                )

                Dim newRegion As New Region(
                    path
                )

                Dim oldRegion As Region =
                    Me.Region

                Me.Region =
                    newRegion

                If oldRegion IsNot Nothing Then
                    oldRegion.Dispose()
                End If

            End Using

        End Sub


        Private Shared Function CreateRoundedRectangle(
            bounds As Rectangle,
            radius As Integer
        ) As GraphicsPath

            Dim path As New GraphicsPath()

            Dim diameter As Integer =
                radius * 2

            path.AddArc(
                bounds.Left,
                bounds.Top,
                diameter,
                diameter,
                180,
                90
            )

            path.AddArc(
                bounds.Right - diameter,
                bounds.Top,
                diameter,
                diameter,
                270,
                90
            )

            path.AddArc(
                bounds.Right - diameter,
                bounds.Bottom - diameter,
                diameter,
                diameter,
                0,
                90
            )

            path.AddArc(
                bounds.Left,
                bounds.Bottom - diameter,
                diameter,
                diameter,
                90,
                90
            )

            path.CloseFigure()

            Return path

        End Function

    End Class

End Namespace