Imports System.Collections.Generic
Imports System.ComponentModel
Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Windows.Forms
Imports ManuscriptPipeline.Services

Namespace Controls

    ' One dot per journal submission: filled grey for a closed submission, an
    ' accent ring for the current one, and a filled accent dot when accepted.
    ' The card's route text says the same thing in words.
    Friend Class RouteDots
        Inherits Control

        Private _dots As IReadOnlyList(Of RouteDotState) = New List(Of RouteDotState)()

        Public Sub New()
            SetStyle(
                ControlStyles.UserPaint Or
                ControlStyles.AllPaintingInWmPaint Or
                ControlStyles.OptimizedDoubleBuffer Or
                ControlStyles.ResizeRedraw,
                True)
            SetStyle(ControlStyles.Selectable, False)
            TabStop = False
            AccessibleRole = AccessibleRole.Graphic
        End Sub

        <DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
        Public Property Dots As IReadOnlyList(Of RouteDotState)
            Get
                Return _dots
            End Get
            Set(value As IReadOnlyList(Of RouteDotState))
                _dots = If(value, New List(Of RouteDotState)())
                Size = GetPreferredSize(Size.Empty)
                Invalidate()
            End Set
        End Property

        Private ReadOnly Property Diameter As Integer
            Get
                Return UiTheme.Px(10, DeviceDpi)
            End Get
        End Property

        Private ReadOnly Property Gap As Integer
            Get
                Return UiTheme.Px(4, DeviceDpi)
            End Get
        End Property

        Public Overrides Function GetPreferredSize(proposedSize As Size) As Size
            If _dots.Count = 0 Then Return New Size(0, Diameter)
            Return New Size(_dots.Count * Diameter + (_dots.Count - 1) * Gap + 1, Diameter + 1)
        End Function

        Protected Overrides Sub OnPaint(e As PaintEventArgs)

            Dim g As Graphics = e.Graphics
            g.Clear(If(Parent IsNot Nothing, Parent.BackColor, UiTheme.CardBackground()))
            g.SmoothingMode = SmoothingMode.AntiAlias

            Dim stroke As Single = Math.Max(1.5F, UiTheme.Px(2, DeviceDpi) * 0.9F)
            Dim top As Single = Math.Max(0.0F, (Height - Diameter) / 2.0F)

            For index As Integer = 0 To _dots.Count - 1
                Dim x As Single = index * (Diameter + Gap)
                Dim outer As New RectangleF(x + stroke / 2.0F, top + stroke / 2.0F, Diameter - stroke, Diameter - stroke)

                Select Case _dots(index)
                    Case RouteDotState.Current
                        Using fill As New SolidBrush(UiTheme.AccentMutedBackground())
                            g.FillEllipse(fill, outer)
                        End Using
                        Using ring As New Pen(UiTheme.AccentColor(), stroke)
                            g.DrawEllipse(ring, outer)
                        End Using
                    Case RouteDotState.Accepted
                        Using fill As New SolidBrush(UiTheme.AccentColor())
                            g.FillEllipse(fill, New RectangleF(x, top, Diameter, Diameter))
                        End Using
                    Case Else
                        Using fill As New SolidBrush(UiTheme.MutedText())
                            g.FillEllipse(fill, New RectangleF(x + 1.0F, top + 1.0F, Diameter - 2.0F, Diameter - 2.0F))
                        End Using
                End Select
            Next

        End Sub

    End Class

End Namespace
