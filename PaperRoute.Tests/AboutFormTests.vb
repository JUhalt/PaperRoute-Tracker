Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.Linq
Imports System.Windows.Forms
Imports ManuscriptPipeline.Controls
Imports ManuscriptPipeline.Forms
Imports Microsoft.VisualStudio.TestTools.UnitTesting

' About PaperRoute (#169): the logo was the program icon's 32-pixel frame
' stretched to 104 pixels (156 at 150%), so it looked blocky. It is drawn
' from the embedded 256-pixel logo now, and only ever scaled down.
<TestClass>
<DoNotParallelize>
Public Class AboutFormTests

    <TestMethod>
    Public Sub TheLogoIsNeverDrawnLargerThanItsSource()
        RunOnStaThread(
            Sub()
                Using dialog As New AboutForm()
                    ShowOffscreen(dialog)

                    ' At 150% the box is half as large again as it is here.
                    Dim pictures As List(Of PictureBox) = Descendants(dialog).OfType(Of PictureBox)().ToList()
                    For Each picture As PictureBox In pictures
                        Dim width As Integer = If(picture.Image Is Nothing, 0, picture.Image.Width)
                        Assert.IsTrue(
                            width >= picture.ClientSize.Width * 3 \ 2,
                            "An image " & width & " pixels wide is stretched to fill " & picture.ClientSize.Width & " pixels, " & picture.ClientSize.Width * 3 \ 2 & " at 150%.")
                    Next

                    Dim logos As List(Of RailLogo) = Descendants(dialog).OfType(Of RailLogo)().ToList()
                    Assert.AreEqual(1, pictures.Count + logos.Count, "About shows one logo.")

                    For Each logo As RailLogo In logos
                        Assert.IsTrue(RailLogo.Logo().Width >= logo.ClientSize.Width * 3 \ 2, "The logo's source is large enough for 150%.")
                        Assert.AreEqual(logo.ClientSize.Width, logo.ClientSize.Height, "The logo is square, so it is not distorted.")
                        Assert.AreEqual("PaperRoute logo", logo.AccessibleName)
                        Assert.AreSame(Cursors.Default, logo.Cursor, "About's logo is not something to click.")
                    Next

                    dialog.Close()
                End Using
            End Sub)
    End Sub

    ' The logo at a display's DPI (#255): at a real 125% it was 130x1
    ' pixels, a line. The window's grid laid it out as it was added, in the
    ' grid's first 200x100, and Windows Forms scaled that as the window
    ' finished building. Built as at each display, the logo is square, its
    ' 104 pixels at 96 DPI scaled, within a pixel. At 96 DPI it was whole
    ' before the fix too.
    <TestMethod>
    <DataRow(96)>
    <DataRow(120)>
    <DataRow(144)>
    Public Sub TheLogoIsSquareAtItsSizeAt96To144Dpi(dpi As Integer)
        Dim logo As Size = Size.Empty
        ScalingMeasurementTests.AtDisplayDpi(
            dpi,
            Sub()
                ViewCatalog.ShowAndClose(
                    "About at " & dpi & " DPI",
                    Function() New AboutForm(),
                    Sub(name, shown) logo = Descendants(shown).OfType(Of RailLogo)().Single().ClientSize)
            End Sub)

        Dim expected As Integer = CInt(Math.Round(104 * dpi / 96.0))
        Assert.IsTrue(
            Math.Abs(logo.Width - expected) <= 1 AndAlso Math.Abs(logo.Height - expected) <= 1,
            $"At {dpi} DPI the logo is {expected}x{expected}: {logo.Width}x{logo.Height}.")
    End Sub

    Private Shared Sub ShowOffscreen(form As Form)
        form.StartPosition = FormStartPosition.Manual
        form.Location = New Point(-20000, -20000)
        form.ShowInTaskbar = False
        form.Show()
        Application.DoEvents()
    End Sub

    Private Shared Iterator Function Descendants(parent As Control) As IEnumerable(Of Control)
        For Each child As Control In parent.Controls
            Yield child
            For Each descendant As Control In Descendants(child)
                Yield descendant
            Next
        Next
    End Function

    ' Its own sixty seconds, on the shared runner (#134).
    Private Shared Sub RunOnStaThread(action As Action)
        StaRunner.Run(action, TimeSpan.FromSeconds(60))
    End Sub

End Class