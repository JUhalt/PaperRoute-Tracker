Imports System
Imports System.Collections.Concurrent
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

        Dim failures As List(Of String) =
            ViewCatalog.ShowEach(
                {(view.Name, ViewCatalog.Dialogs(view))},
                Sub(name, shown)

                    result = ScalingMeasurementTests.MeasureAtThisDisplay(shown)
                    dpi = shown.DeviceDpi

                End Sub
            )

        Assert.AreEqual(0, failures.Count, Environment.NewLine & String.Join(Environment.NewLine, failures))
        Assert.IsNotNull(result, "The window was measured.")

        TestContext.WriteLine(view.Name & " at " & dpi & " DPI: " & result.Describe())

        Assert.IsNull(result.NotMeasuredHere, "The window is measured at this display: " & result.Describe())
        Assert.IsTrue(result.AcceptInside.HasValue, "The accept button is judged: " & result.Describe())
        Assert.IsTrue(result.CancelInside.HasValue, "The cancel button is judged: " & result.Describe())

        If dpi = 96 Then
            Assert.IsTrue(result.Passes, "At 96 DPI the window agrees with the sweep's cells: " & result.Describe())
        End If

    End Sub


    ' What the tool's guard rests on (Program.vb, Guarded). Each window
    ' opens on a runner thread of its own, where Windows Forms catches an
    ' error in one of the window's handlers and, with no handler of its own
    ' on that thread, shows its error dialog and waits. The factory runs on
    ' that same thread before the window exists, so a handler it attaches
    ' there is handed the error instead, with no dialog, and inspect fails
    ' the window by name. The tool itself is out of this project's reach,
    ' so this is a guard that cannot fail on the unchanged code: it fails if
    ' ShowEach ever builds a window on another thread than the one it runs
    ' on, and without the handler the dialog holds the window until the
    ' runner's timeout.
    <TestMethod>
    Public Sub AnErrorInAWindowsHandlerFailsThatWindowByName()

        Dim faults As New ConcurrentDictionary(Of String, Exception)()
        Dim inspected As Boolean = False

        Dim build As Func(Of String, Form) =
            Function(root)

                AddHandler Application.ThreadException,
                    Sub(sender, e) faults.TryAdd("Faulty", e.Exception)

                Dim window As New Form With {
                    .Size = New Size(300, 200)
                }

                AddHandler window.Shown,
                    Sub(sender, e) Throw New InvalidOperationException("Shown failed.")

                Return window

            End Function

        Dim failures As List(Of String) =
            ViewCatalog.ShowEach(
                {("Faulty", build)},
                Sub(name, shown)

                    inspected = True

                    Dim fault As Exception = Nothing

                    If faults.TryRemove(name, fault) Then
                        Throw New InvalidOperationException("a handler failed: " & fault.ToString(), fault)
                    End If

                End Sub
            )

        Assert.IsTrue(inspected, "The window was inspected: " & String.Join(" | ", failures))
        Assert.AreEqual(1, failures.Count, String.Join(" | ", failures))
        StringAssert.StartsWith(failures(0), "Faulty: a handler failed: System.InvalidOperationException: Shown failed.")
        Assert.IsTrue(faults.IsEmpty, "No error was left unreported.")

    End Sub

End Class
