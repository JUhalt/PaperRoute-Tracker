Imports System
Imports System.Drawing
Imports System.IO
Imports System.Linq
Imports System.Windows.Forms
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline.Services

' The v0.9 dialogs on a high-scale or small display: each scales with the
' display and stays inside the screen's working area, so its buttons never
' sit behind the taskbar. A check at 150% found Find Journals and Before
' Sending did not.
<TestClass>
Public Class DialogFitTests

    ' The fixed-size dialogs added in v0.9.
    Private Shared ReadOnly V09Dialogs As String() = {
        "AssistantConsentForm", "AssistantDraftForm", "AuthorInstructionsForm", "CitationsUpdateForm", "CoverLetterForm", "DecisionLetterForm",
        "JournalCandidateForm", "JournalFactsForm", "JournalSuggestionsForm"
    }

    <TestMethod>
    Public Sub EveryV09DialogScalesAndFitsTheScreen()
        Dim folder As String = Path.Combine(AssistantCoreTests.RepositoryRoot(), "ManuscriptPipeline", "Forms")
        For Each name As String In V09Dialogs
            Dim code As String = String.Join(Environment.NewLine,
                File.ReadAllLines(Path.Combine(folder, name & ".vb")).Where(Function(line) Not line.TrimStart().StartsWith("'"c)))
            StringAssert.Contains(code, "AutoScaleDimensions = New SizeF(96.0F, 96.0F)", name & " scales its sizes from 96 DPI.")
            StringAssert.Contains(code, "ResponsiveDialogSizingService.FitToWorkingArea(Me)", name & " keeps itself inside the screen.")
        Next
    End Sub

    <TestMethod>
    Public Sub AWindowLargerThanTheScreenIsShrunkAndCenteredInIt()
        Dim area As New Rectangle(100, 50, 700, 500)
        Using dialog As New Form With {.StartPosition = FormStartPosition.Manual, .MinimumSize = New Size(760, 620), .Size = New Size(900, 700)}
            ResponsiveDialogSizingService.FitToWorkingArea(dialog, area)
            Assert.IsTrue(area.Contains(dialog.Bounds), "Inside the working area: " & dialog.Bounds.ToString())
            Assert.AreEqual(New Size(700, 500), dialog.Size, "As large as the area allows.")
            Assert.IsTrue(dialog.MinimumSize.Width <= 700 AndAlso dialog.MinimumSize.Height <= 500, "A minimum too large for this screen is lowered.")
        End Using

        Using fits As New Form With {.StartPosition = FormStartPosition.Manual, .Location = New Point(120, 60), .Size = New Size(400, 300)}
            ResponsiveDialogSizingService.FitToWorkingArea(fits, area)
            Assert.AreEqual(New Rectangle(120, 60, 400, 300), fits.Bounds, "A window that fits is left as it is.")
        End Using
    End Sub

    ' At 150% a button's minimum (scaled from 96x34) is larger than its
    ' text; the row must be laid out at that size. Look Up Journal Facts
    ' drew Save over Cancel and cut both off when it wasn't.
    <TestMethod>
    Public Sub ButtonsAreLaidOutAtTheSizeTheyAreDrawn()
        Dim failure As Exception = Nothing
        Dim thread As New Threading.Thread(
            Sub()
                Try
                    Using host As New Form With {.StartPosition = FormStartPosition.Manual, .Location = New Point(-20000, -20000), .ShowInTaskbar = False, .ClientSize = New Size(600, 200)}
                        Dim row As New FlowLayoutPanel With {.AutoSize = True, .FlowDirection = FlowDirection.RightToLeft, .WrapContents = False, .Dock = DockStyle.Top}
                        Dim save As New ManuscriptPipeline.Controls.ActionButton With {.Text = "Save", .AutoSize = True, .MinimumSize = New Size(144, 51)}
                        Dim cancel As New ManuscriptPipeline.Controls.ActionButton With {.Text = "Cancel", .AutoSize = True, .MinimumSize = New Size(144, 51), .Margin = New Padding(0, 0, 12, 0)}
                        row.Controls.Add(save)
                        row.Controls.Add(cancel)
                        host.Controls.Add(row)
                        host.Show()
                        Application.DoEvents()
                        row.PerformLayout()

                        Assert.IsTrue(save.GetPreferredSize(Size.Empty).Width >= 144 AndAlso save.GetPreferredSize(Size.Empty).Height >= 51)
                        Assert.IsFalse(save.Bounds.IntersectsWith(cancel.Bounds), "Save " & save.Bounds.ToString() & " overlaps Cancel " & cancel.Bounds.ToString())
                        For Each button As Control In {save, cancel}
                            Assert.IsTrue(row.ClientRectangle.Contains(button.Bounds), button.Text & " " & button.Bounds.ToString() & " fits its row " & row.ClientRectangle.ToString())
                        Next
                        host.Close()
                    End Using
                Catch ex As Exception
                    failure = ex
                End Try
            End Sub) With {.IsBackground = True}
        thread.SetApartmentState(Threading.ApartmentState.STA)
        thread.Start()
        Assert.IsTrue(thread.Join(TimeSpan.FromMinutes(2)), "Timed out.")
        If failure IsNot Nothing Then Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw()
    End Sub

End Class
