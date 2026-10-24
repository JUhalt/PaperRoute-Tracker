Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.Linq
Imports System.Windows.Forms

Namespace Services

    ' One scaling rule for a dialog (#93). Its sizes are written at 96 DPI
    ' and Windows Forms scales them for the display when the window opens:
    ' Begin before the first control, Finish after the last. A value Windows
    ' Forms scales (a size, a location, a margin, a padding, a minimum, a
    ' table's absolute column or row) is written as its 96-DPI literal and
    ' never scaled by hand as well, or it is scaled twice. Only what is
    ' drawn by hand, or set after the window has opened, is scaled from Dpi.
    Public NotInheritable Class DialogScaling

        Private Sub New()
        End Sub


        ' The 96-DPI base dimensions and the DPI scaling mode, with layout
        ' held until Finish, as the designer writes a form.
        Public Shared Sub Begin(
            dialog As Form
        )

            If dialog Is Nothing Then
                Throw New ArgumentNullException(NameOf(dialog))
            End If

            dialog.SuspendLayout()
            dialog.AutoScaleDimensions = New SizeF(96.0F, 96.0F)
            dialog.AutoScaleMode = AutoScaleMode.Dpi

        End Sub


        ' After the last control: the buttons sized to their captions held
        ' at the sizes they were given (HoldAtGivenSize), one layout pass,
        ' and the window kept inside its screen's working area when it
        ' opens, so its buttons are reachable on a small or a 150% display.
        Public Shared Sub Finish(
            dialog As Form
        )

            If dialog Is Nothing Then
                Throw New ArgumentNullException(NameOf(dialog))
            End If

            For Each button As Button In TextSizedButtons(dialog).ToList()
                HoldAtGivenSize(button)
            Next

            dialog.ResumeLayout(False)
            dialog.PerformLayout()

            AddHandler dialog.Load,
                AddressOf FitOnLoad

        End Sub


        Private Shared Sub FitOnLoad(
            sender As Object,
            e As EventArgs
        )

            ResponsiveDialogSizingService.FitToWorkingArea(
                DirectCast(sender, Form)
            )

        End Sub


        ' The buttons that size themselves to their captions and only grow,
        ' as a Button does unless told otherwise.
        Private Shared Iterator Function TextSizedButtons(
            parent As Control
        ) As IEnumerable(Of Button)

            For Each child As Control In parent.Controls

                Dim button As Button = TryCast(child, Button)

                If button IsNot Nothing AndAlso
                   button.AutoSize AndAlso
                   button.AutoSizeMode = AutoSizeMode.GrowOnly Then

                    Yield button

                End If

                For Each inner As Button In TextSizedButtons(child)
                    Yield inner
                Next

            Next

        End Function


        ' The dialog is built with its text at the display's size, so a
        ' button that sizes itself to its caption has already grown to the
        ' caption there, and one that only grows would keep that width
        ' when Windows Forms scales the dialog: at 150% it was 1.5 times
        ' its caption (#93). Turning AutoSize off gives it back the size it
        ' was given, written at 96 DPI (its default 75x23 if none); that
        ' becomes its minimum, which the scale scales, and it grows and
        ' shrinks from there, so it ends at the larger of that minimum and
        ' its caption. A minimum it already had, such as one shared to give
        ' buttons one width, is kept. Its parent waits meanwhile, so the
        ' size read is the one it was given.
        Private Shared Sub HoldAtGivenSize(
            button As Button
        )

            Dim parent As Control =
                button.Parent

            parent.SuspendLayout()

            button.AutoSize = False

            button.MinimumSize =
                New Size(
                    Math.Max(button.MinimumSize.Width, button.Width),
                    Math.Max(button.MinimumSize.Height, button.Height)
                )

            button.AutoSizeMode = AutoSizeMode.GrowAndShrink
            button.AutoSize = True

            parent.ResumeLayout(True)

        End Sub


        ' The DPI a control is shown at, for what is drawn by hand.
        Public Shared Function Dpi(
            control As Control
        ) As Integer

            If control Is Nothing Then
                Throw New ArgumentNullException(NameOf(control))
            End If

            Return control.DeviceDpi

        End Function

    End Class

End Namespace
