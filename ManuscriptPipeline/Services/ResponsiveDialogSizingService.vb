Imports System
Imports System.Drawing

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

    End Class

End Namespace
