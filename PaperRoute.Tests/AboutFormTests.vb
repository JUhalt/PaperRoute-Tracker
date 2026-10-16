Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.Linq
Imports System.Runtime.ExceptionServices
Imports System.Threading
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

    Private Shared Sub RunOnStaThread(action As Action)
        Dim failure As ExceptionDispatchInfo = Nothing
        Dim thread As New Thread(
            Sub()
                Try
                    action()
                Catch ex As Exception
                    failure = ExceptionDispatchInfo.Capture(ex)
                End Try
            End Sub) With {.IsBackground = True}
        thread.SetApartmentState(ApartmentState.STA)
        thread.Start()
        Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(60)), "The dialog test timed out.")
        failure?.Throw()
    End Sub

End Class