Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.Linq
Imports System.Windows.Forms
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline

' The main window's start and minimum size (#93, step 0). It opened at
' 1280x840 with a 960x680 minimum, unscaled and with no screen cap, so on
' a 1366x768 laptop the first window a new user saw was taller than the
' 720-pixel working area even at 100%. Both sizes now scale with the
' display, and the start size is capped to the working area as the
' dialogs' are. The content's own hand scaling is unchanged.
<TestClass>
<DoNotParallelize>
Public Class MainWindowSizeTests

    ' A 1366x768 laptop at 100%: the window fits the 720-pixel working
    ' area Windows 11's taskbar leaves, no smaller than its minimum, which
    ' is unchanged.
    <TestMethod>
    Public Sub FitsA1366x720WorkingAreaAt96Dpi()

        Dim area As New Rectangle(0, 0, 1366, 720)

        Dim sizes As (Start As Size, Minimum As Size) =
            Form1.StartSizes(area, 96)

        Assert.AreEqual(New Size(960, 680), sizes.Minimum, "The 96-DPI minimum is unchanged.")
        AssertInside(area, sizes.Start)
        AssertAtLeast(sizes.Minimum, sizes.Start)

    End Sub


    ' The certification VM: 1920x1080 at 150%, a 1008-pixel working area.
    ' The minimum scales by 1.5 to 1440 wide; its 1020 height is more
    ' than the screen has, so it is lowered to the screen.
    <TestMethod>
    Public Sub FitsA1920x1008WorkingAreaAt144Dpi()

        Dim area As New Rectangle(0, 0, 1920, 1008)

        Dim sizes As (Start As Size, Minimum As Size) =
            Form1.StartSizes(area, 144)

        AssertInside(area, sizes.Start)
        AssertAtLeast(sizes.Minimum, sizes.Start)
        Assert.AreEqual(1440, sizes.Minimum.Width, "The minimum width is the 96-DPI one scaled by 1.5.")
        Assert.IsTrue(sizes.Minimum.Height <= area.Height, "A minimum taller than the screen is lowered to it: " & sizes.Minimum.Height)
        Assert.IsTrue(sizes.Start.Width >= 1800, "The window is as wide as the screen allows, not its 96-DPI 1280: " & sizes.Start.Width)

    End Sub


    ' Where the window fits, nothing changes: 1280x840 and 960x680.
    <TestMethod>
    Public Sub KeepsItsSizesOnARoomyScreen()

        Dim sizes As (Start As Size, Minimum As Size) =
            Form1.StartSizes(New Rectangle(0, 0, 2560, 1400), 96)

        Assert.AreEqual(New Size(1280, 840), sizes.Start)
        Assert.AreEqual(New Size(960, 680), sizes.Minimum)

    End Sub


    ' The window the catalog builds opens at the computed size for one of
    ' this computer's screens, so BuildInterface applies the sizes and not
    ' the old literals. On a roomy screen both agree, so this cannot fail
    ' before the change there; the three tests above carry the proof. The
    ' catalog builds it on a temporary root, as it builds every view, so
    ' the window never reads the user's own data folder.
    <TestMethod>
    Public Sub TheBoardOpensAtTheComputedSize()

        Dim failures As List(Of String) =
            ViewCatalog.ShowEach(
                {("The Board", ViewCatalog.Dialogs(GetType(Form1)))},
                Sub(name, board)

                    Dim expected As String() =
                        Screen.AllScreens.
                            Select(Function(screen) Form1.StartSizes(screen.WorkingArea, board.DeviceDpi).Start.ToString()).
                            Distinct().
                            ToArray()

                    Assert.IsTrue(
                        expected.Contains(board.Size.ToString()),
                        "The Board opened at " & board.Size.ToString() & "; computed for this computer's screens: " & String.Join(", ", expected)
                    )

                End Sub
            )

        Assert.AreEqual(0, failures.Count, Environment.NewLine & String.Join(Environment.NewLine, failures))

    End Sub


    Private Shared Sub AssertInside(area As Rectangle, size As Size)
        Assert.IsTrue(
            size.Width <= area.Width AndAlso size.Height <= area.Height,
            size.ToString() & " is inside the " & area.Width & "x" & area.Height & " working area."
        )
    End Sub


    Private Shared Sub AssertAtLeast(minimum As Size, size As Size)
        Assert.IsTrue(
            size.Width >= minimum.Width AndAlso size.Height >= minimum.Height,
            size.ToString() & " is at or above the minimum " & minimum.ToString() & "."
        )
    End Sub

End Class
