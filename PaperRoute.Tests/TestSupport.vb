Imports System
Imports Microsoft.VisualStudio.TestTools.UnitTesting

' The test helpers that need Windows Forms. Those that do not are in
' CoreTestSupport.vb, which PaperRoute.Core.Tests shares (core stage 2).
Friend Module TestSupport

    ' Runs a window test on the shared STA runner (#134), which rethrows
    ' what it threw. A modal box would hold the thread; after ninety
    ' seconds the runner closes what that thread left open and fails the
    ' test, so the run reports it instead.
    Public Sub RunOnStaThread(
        action As Action
    )

        StaRunner.Run(
            action,
            TimeSpan.FromSeconds(90)
        )

    End Sub


    ' The Insights figures are worked out off the UI thread (#132): pumps
    ' the window's messages until they are shown, and fails if they are
    ' still being worked out at the limit.
    Public Sub WaitForInsights(
        window As ManuscriptPipeline.Form1,
        limit As TimeSpan
    )

        Dim waited As System.Diagnostics.Stopwatch =
            System.Diagnostics.Stopwatch.StartNew()

        While window.InsightsCalculating AndAlso
              waited.Elapsed < limit

            System.Windows.Forms.Application.DoEvents()
            System.Threading.Thread.Sleep(1)

        End While

        System.Windows.Forms.Application.DoEvents()

        Assert.IsFalse(
            window.InsightsCalculating,
            "Insights finished calculating."
        )

    End Sub


    ' Processes the window's messages while work it does as it idles is
    ' pending, and fails if it still is after a minute.
    Public Sub PumpWhile(
        pending As Func(Of Boolean),
        failure As String
    )

        Dim waited As System.Diagnostics.Stopwatch =
            System.Diagnostics.Stopwatch.StartNew()

        While pending() AndAlso waited.Elapsed < TimeSpan.FromSeconds(60)
            System.Windows.Forms.Application.DoEvents()
            System.Threading.Thread.Sleep(1)
        End While

        Assert.IsFalse(pending(), failure)

    End Sub


    ' The control focus is in: the active control of each container
    ' down from this one.
    Public Function DeepestActiveControl(
        container As System.Windows.Forms.ContainerControl
    ) As System.Windows.Forms.Control

        Dim active As System.Windows.Forms.Control =
            container.ActiveControl

        While TypeOf active Is System.Windows.Forms.ContainerControl AndAlso
              DirectCast(active, System.Windows.Forms.ContainerControl).ActiveControl IsNot Nothing

            active = DirectCast(active, System.Windows.Forms.ContainerControl).ActiveControl

        End While

        Return active

    End Function

End Module
