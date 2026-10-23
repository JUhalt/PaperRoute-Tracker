Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.Windows.Forms
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline.Forms

' The display evidence tool's measure (#93, step 2's calibration): a
' catalog window inspected at this computer's own display, with nothing
' simulated, after it is fitted to the working area of the screen it
' opens on as the app fits it. ManualCertification/DisplayEvidence runs
' it on every top-level window at a real 150% display; here it runs on
' one, at whatever display the test host has.
<TestClass>
<DoNotParallelize>
Public Class DisplayEvidenceTests

    Public Property TestContext As TestContext


    ' A small window with Save and Cancel, which any screen holds whole.
    ' Its cell is filled: both buttons are judged and nothing says it was
    ' not measured. At 96 DPI, where the sweep simulates nothing, it must
    ' also agree with the sweep's cells for this window, which are "ok";
    ' at any other display whether it passes is that display's evidence,
    ' not this test's.
    <TestMethod>
    Public Sub AWindowIsMeasuredAtThisDisplay()

        Dim view As Type = GetType(AffiliationEditForm)
        Dim result As ScalingMeasurementTests.Cell = Nothing
        Dim dpi As Integer = 0
        Dim tooLarge As String = Nothing

        Dim failures As List(Of String) =
            ViewCatalog.ShowEach(
                {(view.Name, ViewCatalog.Dialogs(view))},
                Sub(name, shown)

                    result = ScalingMeasurementTests.MeasureAtThisDisplay(shown)
                    dpi = shown.DeviceDpi

                    Dim area As Rectangle = Screen.FromControl(shown).WorkingArea

                    If shown.Width > area.Width OrElse shown.Height > area.Height Then
                        tooLarge = shown.Size.ToString() & " in a working area of " & area.Size.ToString()
                    End If

                End Sub
            )

        Assert.AreEqual(0, failures.Count, Environment.NewLine & String.Join(Environment.NewLine, failures))
        Assert.IsNotNull(result, "The window was measured.")

        TestContext.WriteLine(view.Name & " at " & dpi & " DPI: " & result.Describe())

        Assert.IsNull(tooLarge, "The window is fitted to this screen's working area: " & tooLarge)
        Assert.IsNull(result.NotMeasuredHere, "The window is measured at this display: " & result.Describe())
        Assert.IsTrue(result.AcceptInside.HasValue, "The accept button is judged: " & result.Describe())
        Assert.IsTrue(result.CancelInside.HasValue, "The cancel button is judged: " & result.Describe())

        If dpi = 96 Then
            Assert.IsTrue(result.Passes, "At 96 DPI the window agrees with the sweep's cells: " & result.Describe())
        End If

    End Sub

End Class
