Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.Linq
Imports System.Windows.Forms
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline.Forms

' Edit Journal at the screens 1.0 certifies (#93, step 4). At a real 150%
' display (a 1920x1008 working area) it opened at its 96-DPI size while
' its text grew, and the List status row, with Favorite and Watch list,
' and the notes slid under the Save and Cancel bar with no way to scroll
' to them. Measured as the sweep measures a window, at 150% on that
' screen and at 125% on a 1366x768 one (a 1366x708 working area), every
' row of every tab is in view or reached by scrolling, and Save and
' Cancel are in view.
<TestClass>
<DoNotParallelize>
Public Class JournalEditScalingTests

    ' The 125% row passed on the unchanged window too, which kept its
    ' 96-DPI sizes there; it holds the window to the shorter screen now
    ' that its sizes grow with the display. A screen larger than this
    ' computer's can hold is measured at the largest it can: a smaller
    ' screen leaves the rows less room, never more. The third row is the
    ' window narrowed to its scaled minimum width at 125%, which a
    ' 1024x768 build runner holds too. At that width Save and Cancel ran
    ' off the right edge while the window's one column kept the tabs'
    ' scaled width.
    <TestMethod>
    <DataRow(144, 1920, 1008)>
    <DataRow(120, 1366, 708)>
    <DataRow(120, 875, 708)>
    Public Sub EveryRowIsReachedAndSaveAndCancelAreInView(dpi As Integer, width As Integer, height As Integer)

        Dim view As Type = GetType(JournalEditForm)
        Dim scales As Boolean = ScalingMeasurementTests.ScalingOf(view).ScalesWithWinForms
        Dim screen As (Name As String, Area As Rectangle)() = ScalingMeasurementTests.HeldScreen(width, height)
        Dim result As ScalingMeasurementTests.Cell = Nothing

        Dim failures As List(Of String) =
            ViewCatalog.ShowEach(
                {(view.Name, ViewCatalog.Dialogs(view))},
                Sub(name, shown)
                    result = ScalingMeasurementTests.Measure(shown, dpi, scales, screen).Single().Result
                End Sub
            )

        Assert.AreEqual(0, failures.Count, Environment.NewLine & String.Join(Environment.NewLine, failures))
        Assert.IsNull(result.NotMeasuredHere, "The window is measured on this computer: " & result.Describe())
        Assert.IsTrue(result.AcceptInside.HasValue AndAlso result.CancelInside.HasValue, "Save and Cancel are judged: " & result.Describe())
        Assert.IsTrue(result.Passes, $"Edit Journal at {dpi} DPI on {screen(0).Name}: " & result.Describe())

    End Sub

End Class
