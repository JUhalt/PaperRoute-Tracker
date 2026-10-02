Imports System
Imports System.Drawing
Imports System.Windows.Forms

Namespace Services

    Public NotInheritable Class ResponsiveDialogSizingService

        Private Sub New()
        End Sub


        Public Shared Function CalculateInitialSize(
            workingArea As Rectangle,
            desiredSize As Size,
            minimumSize As Size,
            Optional edgeMargin As Integer = 72
        ) As Size

            If edgeMargin < 0 Then
                Throw New ArgumentOutOfRangeException(NameOf(edgeMargin))
            End If

            Dim availableWidth As Integer =
                Math.Max(
                    1,
                    workingArea.Width - edgeMargin
                )

            Dim availableHeight As Integer =
                Math.Max(
                    1,
                    workingArea.Height - edgeMargin
                )

            Dim targetWidth As Integer =
                Math.Min(
                    desiredSize.Width,
                    availableWidth
                )

            Dim targetHeight As Integer =
                Math.Min(
                    desiredSize.Height,
                    availableHeight
                )

            If availableWidth >= minimumSize.Width Then
                targetWidth =
                    Math.Max(
                        minimumSize.Width,
                        targetWidth
                    )
            End If

            If availableHeight >= minimumSize.Height Then
                targetHeight =
                    Math.Max(
                        minimumSize.Height,
                        targetHeight
                    )
            End If

            Return New Size(
                Math.Max(1, targetWidth),
                Math.Max(1, targetHeight)
            )

        End Function


        Public Shared Function CalculateCenteredLocation(
            workingArea As Rectangle,
            dialogSize As Size
        ) As Point

            Return New Point(
                workingArea.Left +
                    Math.Max(
                        0,
                        (workingArea.Width - dialogSize.Width) \ 2
                    ),
                workingArea.Top +
                    Math.Max(
                        0,
                        (workingArea.Height - dialogSize.Height) \ 2
                    )
            )

        End Function


        ' A size no larger than the working area.
        Public Shared Function FitSize(
            workingArea As Rectangle,
            size As Size
        ) As Size

            Return New Size(
                Math.Max(1, Math.Min(size.Width, workingArea.Width)),
                Math.Max(1, Math.Min(size.Height, workingArea.Height))
            )

        End Function


        ' Keeps a window that would open larger than its screen's working
        ' area (a tall window at 150% on a small display) inside it, so its
        ' buttons can't sit behind the taskbar. A window that fits is left
        ' where it is.
        Public Shared Sub FitToWorkingArea(dialog As Form)

            If dialog Is Nothing Then
                Throw New ArgumentNullException(NameOf(dialog))
            End If

            Dim area As Rectangle =
                Screen.FromControl(If(dialog.Owner, dialog)).WorkingArea

            ' A minimum larger than this screen (scaled for another display)
            ' would hold the window at a size that can't fit.
            Dim minimum As Size = dialog.MinimumSize

            If minimum.Width > area.Width OrElse minimum.Height > area.Height Then
                dialog.MinimumSize = FitSize(area, minimum)
            End If

            Dim fitted As Size =
                FitSize(area, dialog.Size)

            If fitted = dialog.Size Then
                Return
            End If

            dialog.Size = fitted
            dialog.Location = CalculateCenteredLocation(area, dialog.Size)

        End Sub

    End Class

End Namespace
