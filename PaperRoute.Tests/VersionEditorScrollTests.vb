Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.Linq
Imports System.Reflection
Imports System.Windows.Forms
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline
Imports ManuscriptPipeline.Forms
Imports ManuscriptPipeline.Services

' The version editor's rows need 786 pixels at 96 DPI, more than its body
' has on a 1366x768 screen (a 1366x720 working area at 100%), or at 150%
' on a 1920x1080 one (1920x1008), so the body scrolls (#93, step 3). Its
' table is docked to fill the body, and a panel leaves a child docked to
' fill it out of its scroll range: the body showed no scroll bar, and the
' notes and the make-current box could not be reached. Each is scrolled
' into view, and the table's last row reached, at 96 and 120 DPI on the
' smaller screen and at 144 DPI on the larger, with the display simulated
' as the measuring test simulates it, on its working areas at each DPI.
<TestClass>
<DoNotParallelize>
Public Class VersionEditorScrollTests

    Private Shared ReadOnly Cells As (Dpi As Integer, Screen As String, Area As Rectangle)() = {
        (96, "1366x720", New Rectangle(-30000, -30000, 1366, 720)),
        (120, "1366x708", New Rectangle(-30000, -30000, 1366, 708)),
        (144, "1920x1008", New Rectangle(-30000, -30000, 1920, 1008))
    }


    <TestMethod>
    Public Sub TheNotesAndTheMakeCurrentBoxScrollIntoView()

        Dim found As New List(Of String)()

        For Each cell As (Dpi As Integer, Screen As String, Area As Rectangle) In Cells

            Dim current As (Dpi As Integer, Screen As String, Area As Rectangle) = cell

            found.AddRange(
                ViewCatalog.ShowEach(
                    {("The version editor at " & current.Dpi & " DPI on " & current.Screen, ViewCatalog.Dialogs(GetType(ManuscriptVersionEditForm)))},
                    Sub(name, shown)

                        ' The window scales with Windows Forms (DialogScaling).
                        ScalingMeasurementTests.Simulate(shown, current.Dpi, True)
                        ResponsiveDialogSizingService.FitToWorkingArea(shown, current.Area)
                        shown.PerformLayout()
                        Application.DoEvents()

                        Dim body As Panel = shown.Controls.OfType(Of Panel)().Single(Function(panel) panel.AutoScroll)
                        Dim rows As Control = body.Controls.Cast(Of Control)().Single()

                        If rows.Height > body.ClientSize.Height AndAlso Not body.VerticalScroll.Visible Then
                            found.Add(name & ": the body is " & body.ClientSize.Height & " tall for rows " & rows.Height & " tall and shows no scroll bar")
                        End If

                        For Each field As String In {"txtNotes", "chkMakeCurrent"}

                            Dim target As Control =
                                DirectCast(GetType(ManuscriptVersionEditForm).GetField(field, BindingFlags.Instance Or BindingFlags.NonPublic).GetValue(shown), Control)

                            body.ScrollControlIntoView(target)
                            Application.DoEvents()

                            Dim seen As Rectangle =
                                Rectangle.Intersect(body.ClientRectangle, body.RectangleToClient(target.Parent.RectangleToScreen(target.Bounds)))

                            If seen.Size <> target.Size Then
                                found.Add(name & ": " & field & " shows " & seen.Height & " of " & target.Height & " pixels")
                            End If

                        Next

                        ' Scrolled to the end, the last row (the provenance line) is in view.
                        body.AutoScrollPosition = New Point(0, rows.Height)
                        Application.DoEvents()

                        Dim beyond As Integer = rows.Bottom - body.ClientSize.Height

                        If beyond > 0 Then
                            found.Add(name & ": scrolled to the end, the rows run " & beyond & " pixels past the body")
                        End If

                    End Sub
                )
            )

        Next

        Assert.AreEqual(0, found.Count, Environment.NewLine & String.Join(Environment.NewLine, found))

    End Sub

End Class
