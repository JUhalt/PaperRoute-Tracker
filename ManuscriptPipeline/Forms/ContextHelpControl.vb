Imports System
Imports System.Drawing
Imports System.Windows.Forms
Imports ManuscriptPipeline.Controls
Imports ManuscriptPipeline.Services

Namespace Forms

    Friend NotInheritable Class ContextHelpControl
        Inherits Control

        Private ReadOnly _helpText As String
        Private ReadOnly _toolTip As New ToolTip()
        Private ReadOnly _dismissTimer As New Timer()

        Private _clickHelpVisible As Boolean = False

        ' The pixels to the em of a "?" its circle holds whole, for each
        ' pixel of the circle's diameter: the 10-point text the control was
        ' drawn for, 13 1/3 pixels to the em at 96 DPI, in its 16-pixel
        ' circle.
        Private Const GlyphEmPerDiameter As Single = 5.0F / 6.0F


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

            Me.Size = New Size(19, 19)
            Me.MinimumSize = New Size(19, 19)
            Me.MaximumSize = New Size(19, 19)
            Me.Margin = New Padding(5, 0, 0, 0)
            Me.Cursor = Cursors.Help
            Me.TabStop = True
            Me.AccessibleRole = AccessibleRole.PushButton
            Me.AccessibleName = "Help"
            Me.AccessibleDescription = _helpText

            _toolTip.InitialDelay = 350
            _toolTip.ReshowDelay = 100
            _toolTip.AutoPopDelay = 6000
            _toolTip.SetToolTip(Me, _helpText)

            _dismissTimer.Interval = 750

            AddHandler _dismissTimer.Tick,
                AddressOf DismissTimerTick

        End Sub


        ' A field's label with its help beside it (#137). The help control
        ' is a tab stop between the label and the field, and WinForms stops
        ' looking for a field's label at the first tab stop, so the field
        ' is named for its label here, and the help control says whose help
        ' it is. A check box names itself, so none is passed for one.
        Public Shared Function LabelWithHelp(
            label As Label,
            helpText As String,
            topPadding As Integer,
            Optional field As Control = Nothing
        ) As Control

            Dim host As New FlowLayoutPanel With {
                .Dock = DockStyle.Fill,
                .AutoSize = True,
                .AutoSizeMode = AutoSizeMode.GrowAndShrink,
                .FlowDirection = FlowDirection.LeftToRight,
                .WrapContents = False,
                .Margin = New Padding(0),
                .Padding = New Padding(0, topPadding, 0, 0)
            }

            host.Controls.Add(
                label
            )

            host.Controls.Add(
                New ContextHelpControl(
                    helpText
                ) With {
                    .AccessibleName = "Help: " & label.Text
                }
            )

            If field IsNot Nothing Then
                field.AccessibleName = label.Text
            End If

            Return host

        End Function


        Protected Overrides Sub OnPaint(
            e As PaintEventArgs
        )

            MyBase.OnPaint(e)

            e.Graphics.SmoothingMode =
                System.Drawing.Drawing2D.SmoothingMode.AntiAlias

            Dim diameter As Single =
                Math.Min(ClientSize.Width, ClientSize.Height) - 3.0F

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
                e.Graphics.DrawEllipse(pen, bounds)
            End Using

            ' The "?" in the control's font, unless that is larger than the
            ' circle holds whole (#313). Windows Forms scales the control
            ' with a window it scales, circle and text together. A window it
            ' does not scale, such as Submission Readiness, keeps the control
            ' at 19 pixels while its text grows with the display: at 150%
            ' the 10-point "?" was 20 pixels to the em, its line taller than
            ' the 16-pixel circle, and TextRenderer, given a line taller than
            ' its box, draws it from the box's top and cuts it at the box's
            ' foot, so only the hook showed, and at 125% all but the dot.
            ' There the "?" is drawn as at 96 DPI. GDI draws a font in whole
            ' pixels to the em, so the two are compared in whole pixels: at
            ' 96 DPI the 10-point "?" is the 13 the circle holds, and is
            ' drawn in the control's font as before.
            Dim holds As Single =
                diameter * GlyphEmPerDiameter

            Dim em As Single =
                Me.Font.SizeInPoints * DialogScaling.Dpi(Me) / 72.0F

            Using held As Font =
                If(
                    CInt(em) > CInt(holds),
                    New Font(
                        Me.Font.FontFamily,
                        holds,
                        Me.Font.Style,
                        GraphicsUnit.Pixel
                    ),
                    Nothing
                )

                TextRenderer.DrawText(
                    e.Graphics,
                    "?",
                    If(held, Me.Font),
                    Rectangle.Round(bounds),
                    UiTheme.SecondaryText(),
                    TextFormatFlags.HorizontalCenter Or
                    TextFormatFlags.VerticalCenter Or
                    TextFormatFlags.NoPadding
                )

            End Using

            If Focused Then
                ControlPaint.DrawFocusRectangle(
                    e.Graphics,
                    ClientRectangle
                )
            End If

        End Sub


        ' A screen reader presses it as a click does, and the help shows
        ' (#139).
        Protected Overrides Function CreateAccessibilityInstance() As AccessibleObject

            Return New PressableAccessibleObject(
                Me,
                Sub() OnClick(EventArgs.Empty)
            )

        End Function


        Protected Overrides Sub OnClick(
            e As EventArgs
        )

            MyBase.OnClick(e)
            Focus()

            If _clickHelpVisible Then
                HideHelp()
                Return
            End If

            ShowHelp(fromClick:=True)

        End Sub


        Protected Overrides Sub OnMouseEnter(
            e As EventArgs
        )

            _dismissTimer.Stop()
            MyBase.OnMouseEnter(e)

        End Sub


        Protected Overrides Sub OnMouseLeave(
            e As EventArgs
        )

            MyBase.OnMouseLeave(e)

            If _clickHelpVisible Then
                _dismissTimer.Stop()
                _dismissTimer.Start()
            End If

        End Sub


        Protected Overrides Sub OnEnter(
            e As EventArgs
        )

            MyBase.OnEnter(e)

            ' Focus alone should not open a tooltip. This prevents a help
            ' control that happens to receive initial dialog focus from
            ' covering the first field before the user asks for help.
            ' Keyboard users can still press Space or F1; in a dialog, Enter
            ' presses the default button before it reaches this control.
            Invalidate()

        End Sub


        Protected Overrides Sub OnLeave(
            e As EventArgs
        )

            HideHelp()
            MyBase.OnLeave(e)
            Invalidate()

        End Sub


        Protected Overrides Sub OnKeyDown(
            e As KeyEventArgs
        )

            MyBase.OnKeyDown(e)

            If e.KeyCode = Keys.Enter OrElse
               e.KeyCode = Keys.Space OrElse
               e.KeyCode = Keys.F1 Then

                If _clickHelpVisible Then
                    HideHelp()
                Else
                    ShowHelp(fromClick:=True)
                End If

                e.Handled = True
                e.SuppressKeyPress = True

            End If

        End Sub


        Private Sub ShowHelp(
            fromClick As Boolean
        )

            If String.IsNullOrWhiteSpace(_helpText) Then
                Return
            End If

            _dismissTimer.Stop()
            _clickHelpVisible = fromClick

            _toolTip.Show(
                _helpText,
                Me,
                New Point(Width + 4, Height),
                8000
            )

        End Sub


        Private Sub HideHelp()

            _dismissTimer.Stop()
            _clickHelpVisible = False
            _toolTip.Hide(Me)

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

            MyBase.Dispose(disposing)

        End Sub

    End Class

End Namespace
