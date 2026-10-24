Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.Linq
Imports System.Reflection
Imports System.Runtime.ExceptionServices
Imports System.Threading
Imports System.Windows.Forms
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline
Imports ManuscriptPipeline.Forms
Imports ManuscriptPipeline.Models

<TestClass>
<DoNotParallelize>
Public Class SubmissionDetailsLayoutTests

    <TestMethod>
    <DataRow("minimum", False)>
    <DataRow("minimum", True)>
    <DataRow("default", False)>
    <DataRow("default", True)>
    Public Sub Summary_AllFiveRowsAndWorkflowFooterRemainVisible(sizeName As String, hasPortal As Boolean)
        Dim submission As New JournalSubmission With {
            .JournalName = String.Join(" ", Enumerable.Repeat("A journal with a very long historical name", 12)),
            .ManuscriptNumber = "JOURNAL-2026-SUBMISSION-123456789",
            .SubmittedDate = New DateTime(2026, 9, 12),
            .FollowUpDate = New DateTime(2026, 10, 12),
            .PortalUrl = If(hasPortal,
                "https://journal.example.invalid/submissions/" & New String("x"c, 300), String.Empty),
            .Notes = "Disposable summary layout fixture. No portal is opened by this test."
        }
        For index As Integer = 0 To 2
            submission.Decisions.Add(New EditorialDecisionEvent With {
                .Decision = EditorialDecision.MajorRevision,
                .DecisionDate = submission.SubmittedDate.AddDays(index + 1),
                .Notes = "Decision details remain readable below the complete submission summary." &
                    Environment.NewLine & "The author can review notes and scroll through further instructions."
            })
        Next
        Dim manuscript As New Manuscript With {.Title = "Submission summary layout fixture"}
        manuscript.Submissions.Add(submission)

        RunOnStaThread(
            Sub()
                Using dialog As New NonactivatingSubmissionDetailsForm(manuscript, submission)
                    dialog.ShowInTaskbar = False
                    dialog.Opacity = 0
                    dialog.StartPosition = FormStartPosition.Manual
                    dialog.Location = New Point(-20000, -20000)
                    dialog.Show()
                    Application.DoEvents()
                    ' Keep OnShown's real default size; it accounts for the actual
                    ' working area. Move back offscreen after its centering logic.
                    dialog.Location = New Point(-20000, -20000)
                    If sizeName = "minimum" Then dialog.Size = dialog.MinimumSize
                    dialog.PerformLayout()
                    Application.DoEvents()

                    Dim group As GroupBox = Descendants(dialog).OfType(Of GroupBox)().Single(
                        Function(item) item.Text = "Submission")
                    Dim summary As TableLayoutPanel = group.Controls.OfType(Of TableLayoutPanel)().Single()
                    Dim expectedLabels As String() = {"Journal", "Journal manuscript ID", "Submitted", "Follow-up", "Publisher portal"}
                    Assert.AreEqual(expectedLabels.Length, summary.RowCount)
                    Assert.IsTrue(summary.ClientSize.Height >= summary.GetRowHeights().Sum(),
                        "The summary must reserve enough height for all five rows, including the portal.")
                    For index As Integer = 0 To expectedLabels.Length - 1
                        Dim label As Control = summary.GetControlFromPosition(0, index)
                        Assert.AreEqual(expectedLabels(index), label.Text)
                        AssertInsideAncestors(label)
                        AssertInsideAncestors(summary.GetControlFromPosition(1, index))
                    Next

                    Dim journalValue As Label = DirectCast(summary.GetControlFromPosition(1, 0), Label)
                    Assert.IsFalse(journalValue.AutoSize, "Long journal names must remain bounded by the fixed summary rows.")
                    Assert.IsTrue(journalValue.AutoEllipsis)
                    Dim portalPanel As Control = summary.GetControlFromPosition(1, 4)
                    Dim portalLabel As Label = portalPanel.Controls.OfType(Of Label)().Single()
                    Assert.AreEqual(If(hasPortal, submission.PortalUrl, "Not recorded"), portalLabel.Text)
                    AssertInsideAncestors(portalLabel)
                    Dim portalButton As Button = portalPanel.Controls.OfType(Of Button)().Single()
                    Assert.AreEqual(hasPortal, portalButton.Visible)
                    If hasPortal Then AssertInsideAncestors(portalButton)

                    Dim closeButton As Control = DirectCast(dialog.AcceptButton, Control)
                    Dim packetsButton As Button = Descendants(dialog).OfType(Of Button)().Single(
                        Function(item) item.AccessibleName = "View packets for this submission")
                    AssertInsideAncestors(closeButton)
                    AssertInsideAncestors(packetsButton)
                    Dim responsesButton As Button = Descendants(dialog).OfType(Of Button)().Single(
                        Function(item) item.AccessibleName = "Reviewer responses for this submission")
                    AssertInsideAncestors(responsesButton)
                    Dim summaryBottom As Integer = dialog.RectangleToClient(group.Parent.RectangleToScreen(group.Bounds)).Bottom
                    Dim footerTop As Integer = dialog.RectangleToClient(closeButton.Parent.RectangleToScreen(closeButton.Bounds)).Top
                    Assert.IsTrue(summaryBottom < footerTop)

                    Dim decisionTab As TabPage = Descendants(dialog).OfType(Of TabPage)().Single(
                        Function(item) item.Text = "Editorial History")
                    Dim decisionList As ListBox = Descendants(decisionTab).OfType(Of ListBox)().Single()
                    Dim decisionDetail As TextBox = Descendants(decisionTab).OfType(Of TextBox)().Single()
                    decisionList.SelectedIndex = 0
                    Application.DoEvents()
                    Assert.AreEqual(3, decisionList.Items.Count)
                    Assert.IsTrue(decisionList.ClientSize.Height >= decisionList.ItemHeight * 3,
                        "The complete summary must still leave at least three editorial-history rows visible.")
                    Assert.IsTrue(decisionDetail.ClientSize.Height >= decisionDetail.Font.Height * 2,
                        "The selected editorial decision needs at least two readable text lines.")
                    Assert.IsTrue(decisionDetail.Text.Contains("Decision details remain readable"))
                    AssertInsideAncestors(decisionList)
                    AssertInsideAncestors(decisionDetail)
                    dialog.Close()
                End Using
            End Sub)
    End Sub

    ' At a real 150% display the portal address, docked to fill the
    ' summary's fifth row, lost its lowest 2 pixels: the summary's row kept
    ' room for the card's title as the window's font measured it while the
    ' window was built, and the card keeps more. And the caption "Journal
    ' manuscript ID" wrapped in its fixed 195-pixel column and its row cut
    ' the second line (#93, step 4). With the text at the size of a 125%
    ' and a 150% display, on their screens, the summary holds its five
    ' rows, each caption has its width on one line, and the portal row,
    ' the address with its button, is whole in the dialog. In the
    ' manuscript page the portal is a button in the detail's header and no
    ' address is shown. That presentation sits in the main window, which
    ' scales by hand, where a simulated display tells nothing, so it is
    ' checked at the main window's own minimum size at this computer's
    ' display, which any screen holds, a 1024x768 build runner's included:
    ' the button is whole there (a guard: it was whole before).
    <TestMethod>
    <DataRow(144, 1920, 1008)>
    <DataRow(120, 1366, 708)>
    Public Sub TheSummaryIsWholeAtTheDisplaysSize(dpi As Integer, width As Integer, height As Integer)

        ' A screen larger than this computer's can hold is measured at the
        ' largest it can hold, which leaves the window less room, not more.
        Dim track As Size = SystemInformation.MaxWindowTrackSize
        Dim held As New Size(Math.Min(width, track.Width - 1), Math.Min(height, track.Height - 1))
        Dim screen As (Name As String, Area As Rectangle)() = {(held.Width & "x" & held.Height, New Rectangle(New Point(-30000, -30000), held))}
        Dim address As String = Nothing

        Dim view As Type = GetType(SubmissionDetailsForm)
        Dim failures As List(Of String) =
            ViewCatalog.ShowEach(
                {(view.Name, ViewCatalog.Dialogs(view))},
                Sub(name, shown)

                    Dim cell As ScalingMeasurementTests.Cell =
                        ScalingMeasurementTests.Measure(shown, dpi, ScalingMeasurementTests.ScalingOf(view).ScalesWithWinForms, screen).Single().Result

                    Dim group As GroupBox = Descendants(shown).OfType(Of GroupBox)().Single(Function(item) item.Text = "Submission")
                    Dim summary As TableLayoutPanel = group.Controls.OfType(Of TableLayoutPanel)().Single()
                    Assert.IsTrue(summary.ClientSize.Height >= summary.GetRowHeights().Sum(),
                        $"At {dpi} DPI the summary holds its five rows: {summary.ClientSize.Height} of {summary.GetRowHeights().Sum()} pixels.")

                    For row As Integer = 0 To summary.RowCount - 1
                        Dim caption As Control = summary.GetControlFromPosition(0, row)
                        Dim oneLine As Integer = caption.GetPreferredSize(Size.Empty).Width
                        Assert.IsTrue(caption.Width >= oneLine, $"At {dpi} DPI the caption '{caption.Text}' has its width on one line: {caption.Width} of {oneLine} pixels.")
                        AssertInsideAncestors(caption)
                    Next

                    Dim portalPanel As Control = summary.GetControlFromPosition(1, 4)
                    Dim portalLabel As Label = portalPanel.Controls.OfType(Of Label)().Single()
                    address = portalLabel.Text
                    StringAssert.StartsWith(address, "https://", "The sample records a portal.")
                    AssertInsideAncestors(portalLabel)
                    AssertInsideAncestors(portalPanel.Controls.OfType(Of Button)().Single())

                    Assert.IsFalse(
                        cell.Cut.Any(Function(cut) cut.Contains("portal", StringComparison.OrdinalIgnoreCase) OrElse cut.Contains("https://", StringComparison.Ordinal)),
                        $"Nothing in the portal row is cut at {dpi} DPI on {screen(0).Name}: " & cell.Describe())

                End Sub
            )

        Assert.AreEqual(0, failures.Count, Environment.NewLine & String.Join(Environment.NewLine, failures))

        Dim hosted As ViewCatalog.NamedView =
            ViewCatalog.Hosted.Single(Function(entry) entry.Name = "SubmissionDetailsForm inside the manuscript page")

        failures =
            ViewCatalog.ShowEach(
                {(hosted.Name, hosted.Build)},
                Sub(name, host)

                    ' At the main window's minimum size, the narrowest a
                    ' researcher can make it.
                    host.Size = host.MinimumSize
                    host.PerformLayout()
                    Application.DoEvents()

                    ' The page's Submissions section in front, on the submission.
                    Dim submission As JournalSubmission =
                        DirectCast(GetType(SubmissionDetailsForm).GetField("_submission", BindingFlags.Instance Or BindingFlags.NonPublic).GetValue(Descendants(host).OfType(Of SubmissionDetailsForm)().Single()), JournalSubmission)
                    Descendants(host).OfType(Of EditManuscriptForm)().Single().ShowSubmission(submission.Id, showResponses:=False)
                    Application.DoEvents()

                    Dim detail As SubmissionDetailsForm = Descendants(host).OfType(Of SubmissionDetailsForm)().Single()
                    Dim portal As Button = Descendants(detail).OfType(Of Button)().Single(Function(button) button.AccessibleName = "Open the publisher portal")
                    AssertInsideAncestors(portal)
                    Assert.IsFalse(Descendants(detail).OfType(Of Label)().Any(Function(label) label.Text = address), "Hosted, the detail shows no address to cut.")

                End Sub
            )

        Assert.AreEqual(0, failures.Count, Environment.NewLine & String.Join(Environment.NewLine, failures))

    End Sub

    Private Shared Sub AssertInsideAncestors(control As Control)
        Assert.IsTrue(control.Visible)
        Assert.IsTrue(control.Width > 0 AndAlso control.Height > 0)
        Dim screenBounds As Rectangle = control.Parent.RectangleToScreen(control.Bounds)
        Dim ancestor As Control = control.Parent
        While ancestor IsNot Nothing
            Assert.IsTrue(ancestor.ClientRectangle.Contains(ancestor.RectangleToClient(screenBounds)),
                control.GetType().Name & " is clipped by " & ancestor.GetType().Name &
                "; child=" & ancestor.RectangleToClient(screenBounds).ToString() &
                "; available=" & ancestor.ClientRectangle.ToString())
            ancestor = ancestor.Parent
        End While
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
        StaRunner.Run(action)
    End Sub

    Private Class NonactivatingSubmissionDetailsForm
        Inherits SubmissionDetailsForm

        Public Sub New(manuscript As Manuscript, submission As JournalSubmission)
            MyBase.New(manuscript, submission, True)
        End Sub

        Protected Overrides ReadOnly Property ShowWithoutActivation As Boolean
            Get
                Return True
            End Get
        End Property
    End Class

End Class
