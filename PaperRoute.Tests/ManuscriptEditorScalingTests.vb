Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.IO
Imports System.Linq
Imports System.Reflection
Imports System.Windows.Forms
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline.Forms
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

' The manuscript editor's Revision deadline row at 125% and 150% (#93).
' At a real 150% display the editor shown on its own reported the row's
' date and its Edit... button clipped: the button was 75x42, the height
' its caption asks for at that text size, and with its margin it and the
' date ran past the foot of the row. On October 23 it was 115x37, held
' to its row. The editor used to lay out as each part went in, so the
' button first sized itself inside its row and kept that height; since
' it lays out once, after it is built (#133), the button sizes itself to
' its caption first and keeps that in the shorter row. The sweep builds
' a window at 96 DPI and grows its text after it has laid out, so it saw
' the button held to the row. Here the editor is built as a real display
' builds it: its layouts held, its text at the display's size before
' they are let go, then the record loaded and everything laid out once.
<TestClass>
<DoNotParallelize>
Public Class ManuscriptEditorScalingTests

    Public Property TestContext As TestContext


    ' At a simulated 150% and 125%, the date and the button are inside
    ' the row, and inside the panel that holds them, and the measure
    ' finds neither clipped. The 125% row passed on the unchanged editor
    ' too, where the caption's height fits the row.
    <TestMethod>
    <DataRow(144)>
    <DataRow(120)>
    Public Sub TheRevisionDeadlineRowHoldsItsDateAndButton(dpi As Integer)

        Dim found As New List(Of String)()
        Dim measured As String = Nothing

        Dim failures As List(Of String) =
            ViewCatalog.ShowEach(
                {("EditManuscriptForm built at " & dpi & " DPI", Function(root) BuiltAt(root, dpi))},
                Sub(name, shown)

                    measured = CheckRow(shown, ScalingMeasurementTests.MeasureAtThisDisplay(shown), found)

                End Sub
            )

        Assert.AreEqual(0, failures.Count, Environment.NewLine & String.Join(Environment.NewLine, failures))

        TestContext.WriteLine(measured)

        Assert.AreEqual(0, found.Count, $"At {dpi} DPI: " & measured & Environment.NewLine & String.Join(Environment.NewLine, found))

    End Sub


    ' The editor scales by hand, which a simulated display does not
    ' reach, so it is checked at this computer's own display too, built
    ' as the app builds it. At a 96-DPI host this is a guard: the row held
    ' its date and button there before the change too.
    <TestMethod>
    Public Sub TheRevisionDeadlineRowHoldsItsDateAndButtonAtThisDisplay()

        Dim found As New List(Of String)()
        Dim measured As String = Nothing
        Dim view As Type = GetType(EditManuscriptForm)

        Dim failures As List(Of String) =
            ViewCatalog.ShowEach(
                {(view.Name, ViewCatalog.Dialogs(view))},
                Sub(name, shown)

                    measured = $"At {shown.DeviceDpi} DPI: " & CheckRow(shown, ScalingMeasurementTests.MeasureAtThisDisplay(shown), found)

                End Sub
            )

        Assert.AreEqual(0, failures.Count, Environment.NewLine & String.Join(Environment.NewLine, failures))

        TestContext.WriteLine(measured)

        Assert.AreEqual(0, found.Count, measured & Environment.NewLine & String.Join(Environment.NewLine, found))

    End Sub


    ' The row on the kept manuscript page opened again (#133): the page
    ' left holds its layout from letting go of its record to the next open,
    ' which lays the row out from the last record's shape, at the main
    ' window's size and after the window was made as small as it goes on
    ' the Board. At this computer's display, since the main window scales
    ' by hand.
    <TestMethod>
    Public Sub TheRevisionDeadlineRowHoldsOnTheKeptPageOpenedAgain()

        Dim found As New List(Of String)()
        Dim measured As New List(Of String)()
        Dim again As ViewCatalog.NamedView = ViewCatalog.Hosted.Single(Function(entry) entry.Name = "The manuscript page opened a second time")

        Dim failures As List(Of String) =
            ViewCatalog.ShowEach(
                {(again.Name, again.Build),
                 ("The manuscript page opened again after the window was made small on the Board", Function(root As String) ViewCatalog.ManuscriptPageAgainAfterShrinking())},
                Sub(name, host)

                    Dim editor As EditManuscriptForm = ViewCatalog.Descendants(host).OfType(Of EditManuscriptForm)().Single()
                    Dim cell As ScalingMeasurementTests.Cell =
                        ScalingMeasurementTests.Measure(host, 96, False, {("its own size", New Rectangle(New Point(-30000, -30000), host.Size))}).Single().Result
                    measured.Add(name & " at " & host.Size.ToString() & ": " & CheckRow(editor, cell, found))

                End Sub
            )

        Assert.AreEqual(0, failures.Count, Environment.NewLine & String.Join(Environment.NewLine, failures))

        TestContext.WriteLine(String.Join(Environment.NewLine, measured))

        Assert.AreEqual(0, found.Count, String.Join(Environment.NewLine, measured) & Environment.NewLine & String.Join(Environment.NewLine, found))

    End Sub


    ' The editor shown on its own, as the catalog builds it, but with its
    ' text at the DPI's size while its layouts are held: an editor built
    ' for a record later holds them until it is told to let go.
    Private Shared Function BuiltAt(root As String, dpi As Integer) As Form

        Dim manuscript As Manuscript = CreateRepresentativeLibrary()(0)
        Dim editor As New EditManuscriptForm(Nothing, {manuscript}, New AuthorLibraryRepository(Path.Combine(root, "authors")), pageMode:=True)

        ScalingMeasurementTests.GrowText(editor, dpi)
        editor.LoadRecord(manuscript, {manuscript})
        editor.LetGoOfLayouts(Integer.MaxValue)

        Return editor

    End Function


    ' Adds a line for the date or the button outside its row, outside the
    ' panel that holds them, or reported cut by the measure; returns what
    ' was measured.
    Private Shared Function CheckRow(shown As Form, cell As ScalingMeasurementTests.Cell, found As List(Of String)) As String

        Dim fields As BindingFlags = BindingFlags.Instance Or BindingFlags.NonPublic
        Dim deadline As Label = DirectCast(GetType(EditManuscriptForm).GetField("lblRevisionDeadlineValue", fields).GetValue(shown), Label)
        Dim button As Button = DirectCast(GetType(EditManuscriptForm).GetField("btnRevisionDeadline", fields).GetValue(shown), Button)

        Assert.AreEqual("Edit...", button.Text, "The sample's latest decision has a revision deadline, so the row shows a date.")

        Dim panel As Control = deadline.Parent
        Dim details As TableLayoutPanel = DirectCast(panel.Parent, TableLayoutPanel)
        Dim heights As Integer() = details.GetRowHeights()
        Dim index As Integer = details.GetRow(panel)
        Dim row As New Rectangle(0, heights.Take(index).Sum(), details.ClientSize.Width, heights(index))

        For Each control As Control In {CType(deadline, Control), button}

            Dim inRow As Rectangle = details.RectangleToClient(panel.RectangleToScreen(control.Bounds))

            If Not row.Contains(inRow) Then
                found.Add($"'{control.Text}' {inRow} is outside its row {row}")
            End If

            If Not panel.ClientRectangle.Contains(control.Bounds) Then
                found.Add($"'{control.Text}' {control.Bounds} is outside its panel {panel.ClientRectangle}")
            End If

        Next

        found.AddRange(cell.Cut.Where(Function(cut) cut.Contains("'" & deadline.Text & "'", StringComparison.Ordinal) OrElse cut.Contains("'" & button.Text & "'", StringComparison.Ordinal)))

        Return $"the row {row}, the date {deadline.Bounds} and the button {button.Bounds} in a panel {panel.ClientRectangle}; the window: {cell.Describe()}"

    End Function

End Class
