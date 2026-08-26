Imports System
Imports System.Collections.Generic

Namespace Services

    Public NotInheritable Class DashboardResponsiveLayoutService

        Public Const MinimumCardWidth As Integer = 360

        Private Sub New()
        End Sub


        Public Shared Function CalculateCardWidth(
            viewportWidth As Integer,
            horizontalPadding As Integer,
            scrollbarWidth As Integer
        ) As Integer

            Dim available As Integer =
                viewportWidth -
                Math.Max(
                    0,
                    horizontalPadding
                ) -
                Math.Max(
                    0,
                    scrollbarWidth
                ) -
                12

            Return Math.Max(
                MinimumCardWidth,
                available
            )

        End Function


        Public Shared Function CalculateActionRowCount(
            availableWidth As Integer,
            buttonWidths As IEnumerable(Of Integer),
            horizontalSpacing As Integer
        ) As Integer

            If buttonWidths Is Nothing Then
                Return 0
            End If

            Dim safeWidth As Integer =
                Math.Max(
                    1,
                    availableWidth
                )

            Dim spacing As Integer =
                Math.Max(
                    0,
                    horizontalSpacing
                )

            Dim rows As Integer = 0
            Dim currentWidth As Integer = 0
            Dim hasButton As Boolean = False

            For Each rawWidth As Integer In buttonWidths

                Dim buttonWidth As Integer =
                    Math.Max(
                        1,
                        rawWidth
                    )

                If Not hasButton Then

                    rows = 1
                    currentWidth = buttonWidth
                    hasButton = True
                    Continue For

                End If

                Dim candidateWidth As Integer =
                    currentWidth +
                    spacing +
                    buttonWidth

                If candidateWidth <=
                   safeWidth Then

                    currentWidth =
                        candidateWidth

                Else

                    rows += 1
                    currentWidth =
                        buttonWidth

                End If

            Next

            Return rows

        End Function


        Public Shared Function CalculateActionAreaHeight(
            availableWidth As Integer,
            buttonWidths As IEnumerable(Of Integer),
            horizontalSpacing As Integer,
            rowHeight As Integer,
            verticalSpacing As Integer
        ) As Integer

            Dim rows As Integer =
                CalculateActionRowCount(
                    availableWidth,
                    buttonWidths,
                    horizontalSpacing
                )

            If rows = 0 Then
                Return 0
            End If

            Dim safeRowHeight As Integer =
                Math.Max(
                    1,
                    rowHeight
                )

            Dim safeVerticalSpacing As Integer =
                Math.Max(
                    0,
                    verticalSpacing
                )

            Return (
                rows *
                safeRowHeight
            ) + (
                Math.Max(
                    0,
                    rows - 1
                ) *
                safeVerticalSpacing
            )

        End Function

    End Class

End Namespace
