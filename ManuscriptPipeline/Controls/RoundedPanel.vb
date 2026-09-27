Imports System
Imports System.ComponentModel
Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Windows.Forms

Namespace Controls

    Public Class RoundedPanel
        Inherits Panel

        Private _cornerRadius As Integer = 14
        Private _borderColor As Color = SystemColors.ControlDark
        Private _borderThickness As Single = 1.0F


        Public Sub New()

            Me.DoubleBuffered = True
            Me.BorderStyle = BorderStyle.None

            ' Repaint the whole panel on resize; otherwise the border drawn at
            ' the previous width stays visible inside a widened card.
            SetStyle(ControlStyles.ResizeRedraw, True)

        End Sub


        <DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)>
        Public Property CornerRadius As Integer

            Get
                Return _cornerRadius
            End Get

            Set(value As Integer)

                _cornerRadius =
                    Math.Max(0, value)

                UpdateShape()
                Invalidate()

            End Set

        End Property


        <DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)>
        Public Property BorderColor As Color

            Get
                Return _borderColor
            End Get

            Set(value As Color)

                _borderColor = value
                Invalidate()

            End Set

        End Property


        <DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)>
        Public Property BorderThickness As Single

            Get
                Return _borderThickness
            End Get

            Set(value As Single)

                _borderThickness =
                    Math.Max(0.0F, value)

                Invalidate()

            End Set

        End Property


        Protected Overrides Sub OnResize(
            e As EventArgs
        )

            MyBase.OnResize(e)

            UpdateShape()

        End Sub


        Protected Overrides Sub OnPaint(
            e As PaintEventArgs
        )

            MyBase.OnPaint(e)

            If Width <= 2 OrElse Height <= 2 Then
                Return
            End If

            e.Graphics.SmoothingMode =
                SmoothingMode.AntiAlias

            Dim bounds As New Rectangle(
                0,
                0,
                Width - 1,
                Height - 1
            )

            Using path As GraphicsPath =
                CreateRoundedRectangle(
                    bounds,
                    _cornerRadius
                )

                Using pen As New Pen(
                    _borderColor,
                    _borderThickness
                )

                    e.Graphics.DrawPath(
                        pen,
                        path
                    )

                End Using

            End Using

        End Sub


        Private Sub UpdateShape()

            If Width <= 0 OrElse Height <= 0 Then
                Return
            End If

            Dim bounds As New Rectangle(
                0,
                0,
                Width,
                Height
            )

            Using path As GraphicsPath =
                CreateRoundedRectangle(
                    bounds,
                    _cornerRadius
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

            Dim safeRadius As Integer =
                Math.Max(
                    0,
                    Math.Min(
                        radius,
                        Math.Min(
                            bounds.Width,
                            bounds.Height
                        ) \ 2
                    )
                )

            If safeRadius = 0 Then

                path.AddRectangle(
                    bounds
                )

                Return path

            End If

            Dim diameter As Integer =
                safeRadius * 2

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