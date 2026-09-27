Imports System.ComponentModel
Imports System.Drawing
Imports System.Windows.Forms
Imports ManuscriptPipeline.Services

Namespace Controls

    ' A bar with one group at the left edge and one at the right. When the
    ' window is too narrow for both on one line, the right group takes its
    ' own right-aligned line (above or below the left group) instead of being
    ' clipped or overlapping.
    Friend Class SplitBar
        Inherits Panel

        Private ReadOnly _left As Control
        Private ReadOnly _right As Control

        Public Sub New(left As Control, right As Control)
            _left = left
            _right = right
            AutoSize = True
            AutoSizeMode = AutoSizeMode.GrowAndShrink
            DoubleBuffered = True
            SetStyle(ControlStyles.ResizeRedraw, True)
            Controls.Add(left)
            Controls.Add(right)
        End Sub

        ' Center both groups on their line (page headers) rather than sitting
        ' them on the bottom line (the shelf tabs).
        <DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
        Public Property CenterVertically As Boolean

        ' When wrapping, put the right group on the line above the left group.
        <DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
        Public Property WrapRightAbove As Boolean

        ' A hairline along the bottom edge.
        <DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
        Public Property ShowBaseline As Boolean

        Private ReadOnly Property Gap As Integer
            Get
                Return UiTheme.Px(16, DeviceDpi)
            End Get
        End Property

        Private Function Fits(width As Integer) As Boolean
            Return _left.PreferredSize.Width + Gap + _right.PreferredSize.Width <= width - Padding.Horizontal
        End Function

        Private Function MeasureWidth(proposed As Size) As Integer
            If proposed.Width > 0 AndAlso proposed.Width < Integer.MaxValue / 2 Then Return proposed.Width
            If Parent IsNot Nothing AndAlso Dock <> DockStyle.None Then Return Parent.DisplayRectangle.Width - Margin.Horizontal
            Return Width
        End Function

        Public Overrides Function GetPreferredSize(proposedSize As Size) As Size
            Dim width As Integer = MeasureWidth(proposedSize)
            Dim leftHeight As Integer = _left.PreferredSize.Height
            Dim rightHeight As Integer = _right.PreferredSize.Height
            Dim height As Integer =
                If(Fits(width),
                   Math.Max(leftHeight, rightHeight),
                   leftHeight + rightHeight)
            Return New Size(width, height + Padding.Vertical)
        End Function

        Protected Overrides Sub OnLayout(e As LayoutEventArgs)

            Dim leftSize As Size = _left.PreferredSize
            Dim rightSize As Size = _right.PreferredSize
            Dim top As Integer = Padding.Top
            Dim bottom As Integer = ClientSize.Height - Padding.Bottom
            Dim right As Integer = ClientSize.Width - Padding.Right
            Dim rightX As Integer = Math.Max(Padding.Left, right - rightSize.Width)

            If Fits(ClientSize.Width) Then
                If CenterVertically Then
                    Dim line As Integer = Math.Max(leftSize.Height, rightSize.Height)
                    _left.SetBounds(Padding.Left, top + (line - leftSize.Height) \ 2, leftSize.Width, leftSize.Height)
                    _right.SetBounds(rightX, top + (line - rightSize.Height) \ 2, rightSize.Width, rightSize.Height)
                Else
                    _left.SetBounds(Padding.Left, bottom - leftSize.Height, leftSize.Width, leftSize.Height)
                    _right.SetBounds(rightX, bottom - rightSize.Height, rightSize.Width, rightSize.Height)
                End If
            ElseIf WrapRightAbove Then
                _right.SetBounds(rightX, top, rightSize.Width, rightSize.Height)
                _left.SetBounds(Padding.Left, top + rightSize.Height, leftSize.Width, leftSize.Height)
            Else
                _left.SetBounds(Padding.Left, top, leftSize.Width, leftSize.Height)
                _right.SetBounds(rightX, top + leftSize.Height, rightSize.Width, rightSize.Height)
            End If

        End Sub

        Protected Overrides Sub OnResize(eventargs As EventArgs)
            MyBase.OnResize(eventargs)
            ' The arrangement, and so the height, depends on the width.
            If Parent IsNot Nothing Then Parent.PerformLayout(Me, "Bounds")
        End Sub

        Protected Overrides Sub OnPaint(e As PaintEventArgs)
            MyBase.OnPaint(e)
            If ShowBaseline Then
                Using line As New Pen(UiTheme.CardBorder())
                    e.Graphics.DrawLine(line, 0, Height - 1, Width, Height - 1)
                End Using
            End If
        End Sub

    End Class

End Namespace
