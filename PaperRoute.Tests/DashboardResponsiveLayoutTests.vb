Imports System.Collections.Generic
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline.Services

<TestClass>
Public Class DashboardResponsiveLayoutTests

    <TestMethod>
    Public Sub WideViewportUsesAvailableCardWidth()

        Assert.AreEqual(
            838,
            DashboardResponsiveLayoutService.CalculateCardWidth(
                900,
                34,
                16
            )
        )

    End Sub


    <TestMethod>
    Public Sub NarrowViewportFallsBackToSafeMinimumCardWidth()

        Assert.AreEqual(
            DashboardResponsiveLayoutService.MinimumCardWidth,
            DashboardResponsiveLayoutService.CalculateCardWidth(
                330,
                34,
                16
            )
        )

    End Sub


    <TestMethod>
    Public Sub ActionButtonsStayOnOneRowWhenTheyFit()

        Assert.AreEqual(
            1,
            DashboardResponsiveLayoutService.CalculateActionRowCount(
                430,
                New Integer() {
                    88,
                    175,
                    88
                },
                10
            )
        )

    End Sub


    <TestMethod>
    Public Sub ActionButtonsWrapPredictablyWhenNarrow()

        Assert.AreEqual(
            2,
            DashboardResponsiveLayoutService.CalculateActionRowCount(
                330,
                New Integer() {
                    88,
                    175,
                    88
                },
                10
            )
        )

    End Sub


    <TestMethod>
    Public Sub ActionAreaHeightAccountsForWrappedRows()

        Assert.AreEqual(
            76,
            DashboardResponsiveLayoutService.CalculateActionAreaHeight(
                330,
                New Integer() {
                    88,
                    175,
                    88
                },
                10,
                34,
                8
            )
        )

    End Sub

End Class
