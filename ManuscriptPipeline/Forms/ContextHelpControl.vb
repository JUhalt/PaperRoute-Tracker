Imports System
Imports System.Drawing
Imports System.Windows.Forms
Imports ManuscriptPipeline.Services

Namespace Forms

    Friend NotInheritable Class ContextHelpControl
        Inherits Control

        Private ReadOnly _helpText As String
        Private ReadOnly _toolTip As New ToolTip()
        Private ReadOnly _dismissTimer As New Timer()

        Private _clickHelpVisible As Boolean = False


        Public Sub New(
            helpText As String
        )

            _helpText =
                If(
                    helpText,
                    String.Empty
                ).Trim()

            SetStyle(
                ControlStyles.AllPaintingInWmPaint Or
                ControlStyles.OptimizedDoubleBuffer Or
                ControlStyles.UserPaint Or
                ControlStyles.ResizeRedraw,
                True
            )

            Me.Size =
                New Size(19, 19)

            Me.MinimumSize =
                New Size(19, 19)

            Me.MaximumSize =
                New Size(19, 19)

            Me.Margin =
                New Padding(5, 0, 0, 0)

            Me.Cursor =
                Cursors.Help

            Me.TabStop =
                True

            Me.AccessibleRole =
                AccessibleRole.PushButton

            Me.AccessibleName =
                "Help"

            Me.AccessibleDescription =
                _helpText

            _toolTip.InitialDelay =
                350

            _toolTip.ReshowDelay =
                100

            _toolTip.AutoPopDelay =
                6000

            _toolTip.SetToolTip(
                Me,
                _helpText
            )

            _dismissTimer.Interval =
                750

            AddHandler _dismissTimer.Tick,
                AddressOf DismissTimerTick

        End Sub


        Protected Overrides Sub OnPaint(
            e As PaintEventArgs
        )

            MyBase.OnPaint(
                e
            )

            e.Graphics.SmoothingMode =
                System.Drawing.Drawing2D.SmoothingMode.AntiAlias

            Dim diameter As Single =
                Math.Min(
                    ClientSize.Width,
                    ClientSize.Height
                ) - 3.0F

            Dim bounds As New RectangleF(
                1.5F,
                1.5F,
                diameter,
                diameter
            )

            Using pen As New Pen(
                UiTheme.SecondaryText(),
                1.4F
            )

                e.Graphics.DrawEllipse(
                    pen,
                    bounds
                )

            End Using

            TextRenderer.DrawText(
                e.Graphics,
                "?",
                Me.Font,
                Rectangle.Round(bounds),
                UiTheme.SecondaryText(),
                TextFormatFlags.HorizontalCenter Or
                TextFormatFlags.VerticalCenter Or
                TextFormatFlags.NoPadding
            )

            If Focused Then

                ControlPaint.DrawFocusRectangle(
                    e.Graphics,
                    ClientRectangle
                )

            End If

        End Sub


        Protected Overrides Sub OnClick(
            e As EventArgs
        )

            MyBase.OnClick(
                e
            )

            Focus()

            If _clickHelpVisible Then

                HideHelp()
                Return

            End If

            ShowHelp(
                fromClick:=True
            )

        End Sub


        Protected Overrides Sub OnMouseEnter(
            e As EventArgs
        )

            _dismissTimer.Stop()

            MyBase.OnMouseEnter(
                e
            )

        End Sub


        Protected Overrides Sub OnMouseLeave(
            e As EventArgs
        )

            MyBase.OnMouseLeave(
                e
            )

            If _clickHelpVisible Then

                _dismissTimer.Stop()
                _dismissTimer.Start()

            End If

        End Sub


        Protected Overrides Sub OnEnter(
            e As EventArgs
        )

            MyBase.OnEnter(
                e
            )

            ShowHelp(
                fromClick:=False
            )

            Invalidate()

        End Sub


        Protected Overrides Sub OnLeave(
            e As EventArgs
        )

            HideHelp()

            MyBase.OnLeave(
                e
            )

            Invalidate()

        End Sub


        Protected Overrides Sub OnKeyDown(
            e As KeyEventArgs
        )

            MyBase.OnKeyDown(
                e
            )

            If e.KeyCode =
               Keys.Enter OrElse
               e.KeyCode =
               Keys.Space OrElse
               e.KeyCode =
               Keys.F1 Then

                If _clickHelpVisible Then

                    HideHelp()

                Else

                    ShowHelp(
                        fromClick:=True
                    )

                End If

                e.Handled =
                    True

                e.SuppressKeyPress =
                    True

            End If

        End Sub


        Private Sub ShowHelp(
            fromClick As Boolean
        )

            If String.IsNullOrWhiteSpace(
                _helpText
            ) Then

                Return

            End If

            _dismissTimer.Stop()

            _clickHelpVisible =
                fromClick

            _toolTip.Show(
                _helpText,
                Me,
                New Point(
                    Width + 4,
                    Height
                ),
                8000
            )

        End Sub


        Private Sub HideHelp()

            _dismissTimer.Stop()

            _clickHelpVisible =
                False

            _toolTip.Hide(
                Me
            )

        End Sub


        Private Sub DismissTimerTick(
            sender As Object,
            e As EventArgs
        )

            HideHelp()

        End Sub


        Protected Overrides Sub Dispose(
            disposing As Boolean
        )

            If disposing Then

                RemoveHandler _dismissTimer.Tick,
                    AddressOf DismissTimerTick

                _dismissTimer.Dispose()
                _toolTip.Dispose()

            End If

            MyBase.Dispose(
                disposing
            )

        End Sub

    End Class

End Namespace
