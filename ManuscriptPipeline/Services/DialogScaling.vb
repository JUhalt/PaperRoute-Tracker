Imports System
Imports System.Drawing
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


        ' After the last control: one layout pass, and the window kept inside
        ' its screen's working area when it opens, so its buttons are
        ' reachable on a small or a 150% display.
        Public Shared Sub Finish(
            dialog As Form
        )

            If dialog Is Nothing Then
                Throw New ArgumentNullException(NameOf(dialog))
            End If

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
