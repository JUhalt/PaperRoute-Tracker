Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.IO
Imports System.Linq
Imports System.Runtime.ExceptionServices
Imports System.Threading
Imports System.Windows.Forms
Imports ManuscriptPipeline.Forms
Imports Microsoft.VisualStudio.TestTools.UnitTesting

' The plain error dialog (#121): what happened, whether there are unsaved
' changes, and two ways out, neither of which forces a close.
<TestClass>
<DoNotParallelize>
Public Class UnexpectedErrorFormTests

    Private Shared ReadOnly Failure As New InvalidOperationException("The route map could not be drawn.")

    <TestMethod>
    Public Sub SaysWhatHappenedAndOffersKeepWorkingOrClose()
        RunOnStaThread(
            Sub()
                Using dialog As New UnexpectedErrorForm(Failure, True, Path.Combine(Path.GetTempPath(), "PaperRoute-ErrorTests-logs"))
                    ShowOffscreen(dialog)

                    Dim labels As List(Of String) = Descendants(dialog).OfType(Of Label)().Select(Function(label) label.Text).ToList()
                    CollectionAssert.Contains(labels, "PaperRoute ran into a problem")
                    CollectionAssert.Contains(labels, "The route map could not be drawn.")
                    CollectionAssert.Contains(labels, "There are unsaved changes. Keep Working returns to the window so you can save them.")
                    CollectionAssert.AreEqual(
                        {"Copy Details", "Open Log Folder", "Keep Working", "Close PaperRoute"},
                        Descendants(dialog).OfType(Of Button)().Select(Function(button) button.Text).ToList())

                    Assert.AreSame(Of IButtonControl)(ButtonNamed(dialog, "Keep Working"), dialog.AcceptButton, "Enter keeps working.")
                    Assert.AreSame(Of IButtonControl)(ButtonNamed(dialog, "Keep Working"), dialog.CancelButton, "Escape keeps working.")
                    Assert.AreEqual(DialogResult.Cancel, ButtonNamed(dialog, "Keep Working").DialogResult)
                    Assert.AreEqual(DialogResult.Abort, ButtonNamed(dialog, "Close PaperRoute").DialogResult, "Close PaperRoute is told apart, and the window does the closing.")

                    dialog.Close()
                End Using

                Using dialog As New UnexpectedErrorForm(Failure, False, Path.Combine(Path.GetTempPath(), "PaperRoute-ErrorTests-logs"))
                    ShowOffscreen(dialog)
                    CollectionAssert.Contains(Descendants(dialog).OfType(Of Label)().Select(Function(label) label.Text).ToList(), "Nothing is waiting to be saved.")
                    dialog.Close()
                End Using
            End Sub)
    End Sub

    <TestMethod>
    Public Sub DetailsNameTheVersionAndTheError()
        Dim details As String = UnexpectedErrorForm.Details(Failure)
        StringAssert.StartsWith(details, "PaperRoute Tracker ")
        StringAssert.Contains(details, "InvalidOperationException")
        StringAssert.Contains(details, "The route map could not be drawn.")

        ' The folder the crash report (#120) writes to.
        Assert.AreEqual("logs", Path.GetFileName(UnexpectedErrorForm.LogFolder()))
    End Sub

    Private Shared Function ButtonNamed(form As Form, text As String) As Button
        Return Descendants(form).OfType(Of Button)().Single(Function(button) button.Text = text)
    End Function

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
