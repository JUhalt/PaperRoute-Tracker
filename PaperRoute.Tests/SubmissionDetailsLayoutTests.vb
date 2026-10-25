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

        Dim screen As (Name As String, Area As Rectangle)() = ScalingMeasurementTests.HeldScreen(width, height)
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

        ' On the page's first open and on the kept page opened again (#133),
        ' whose details are built after the page has painted.
        Dim hosted As List(Of (Name As String, Build As Func(Of String, Form))) =
            ViewCatalog.Hosted.
                Where(Function(entry) entry.Name = "SubmissionDetailsForm inside the manuscript page" OrElse
                                      entry.Name = "The manuscript page opened a second time").
                Select(Function(entry) (entry.Name, entry.Build)).
                ToList()
        Assert.AreEqual(2, hosted.Count, "Both views are in the catalog.")

        failures =
            ViewCatalog.ShowEach(
                hosted,
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

    ' At the real 1366x708 working area at 125%, Submission Details gave its
    ' Correspondence & Files list no height and its Editorial History list
    ' one row, so the letters and files could not be seen (#93, step 4):
    ' its summary, notes and footer keep their pixel sizes while their text
    ' grows, and the tabs had what was left. With the text at the display's
    ' size, on a 1366x768 screen at 100% and 125% and a 1920x1080 one at
    ' 150%, every list and text box in the dialog shows two rows or more, in
    ' view or in reach of a scroll, and Close is in view: the measure finds
    ' nothing cut. A screen smaller than the one asked for (a 1024x768 build
    ' runner holds about 1043x787) can cut a label the asked screen holds
    ' whole, so there only the lists and Close are judged.
    <TestMethod>
    <DataRow(96, 1366, 720)>
    <DataRow(120, 1366, 708)>
    <DataRow(144, 1920, 1008)>
    Public Sub EveryListShowsTwoRowsAtTheDisplaysSize(dpi As Integer, width As Integer, height As Integer)

        Dim screen As (Name As String, Area As Rectangle)() = ScalingMeasurementTests.HeldScreen(width, height)

        Dim view As Type = GetType(SubmissionDetailsForm)
        Dim failures As List(Of String) =
            ViewCatalog.ShowEach(
                {(view.Name, ViewCatalog.Dialogs(view))},
                Sub(name, shown)

                    Dim cell As ScalingMeasurementTests.Cell =
                        ScalingMeasurementTests.Measure(shown, dpi, ScalingMeasurementTests.ScalingOf(view).ScalesWithWinForms, screen).Single().Result

                    Assert.IsNull(cell.NotMeasuredHere, "The dialog is measured on this computer: " & cell.Describe())
                    Assert.IsTrue(cell.AcceptInside.HasValue AndAlso cell.AcceptInside.Value, $"Close is in view at {dpi} DPI on {screen(0).Name}: " & cell.Describe())
                    Assert.IsFalse(
                        cell.Cut.Any(Function(cut) cut.EndsWith(" under two rows", StringComparison.Ordinal)),
                        $"Every list keeps two rows at {dpi} DPI on {screen(0).Name}: " & cell.Describe())

                    If screen(0).Area.Size = New Size(width, height) Then
                        Assert.IsTrue(cell.Passes, $"Submission Details at {dpi} DPI on {screen(0).Name}: " & cell.Describe())
                    End If

                End Sub
            )

        Assert.AreEqual(0, failures.Count, Environment.NewLine & String.Join(Environment.NewLine, failures))

    End Sub

    ' An empty list has room for its whole hint (#93). At 125% on a
    ' 1366x768 screen the empty Correspondence & Files list kept its two
    ' rows, and its hint, which says what belongs there and how to add the
    ' first item, needs four lines and its padding there: the list had room
    ' for one line of it. The real display reported the help above the list
    ' clipped too; that was the measure's misreading, which
    ' ScalingMeasurementTests.APageIsMeasuredWhereShowingItScrolled covers.
    ' With the text at the display's size, on a 1366x768 screen at 100% and
    ' 125% and a 1920x1080 one at 150%, the empty list's hint has the height
    ' its text needs at its width, the list is in reach of the body's
    ' scroll, every list keeps two rows, Close is in view and the measure
    ' finds nothing cut. A screen smaller than the one asked for (a 1024x768
    ' build runner holds about 1043x787) can cut what the asked screen holds
    ' whole, so there only the hint, the lists and Close are judged.
    ' Hosted, the detail has no body to scroll; its Correspondence & Files
    ' page is HostedTheCorrespondenceToolbarAndHintAreWhole's (#217).
    <TestMethod>
    <DataRow(96, 1366, 720)>
    <DataRow(120, 1366, 708)>
    <DataRow(144, 1920, 1008)>
    Public Sub AnEmptyListHasRoomForItsWholeHint(dpi As Integer, width As Integer, height As Integer)

        Dim screen As (Name As String, Area As Rectangle)() = ScalingMeasurementTests.HeldScreen(width, height)

        Dim view As Type = GetType(SubmissionDetailsForm)
        Dim failures As List(Of String) =
            ViewCatalog.ShowEach(
                {(view.Name, ViewCatalog.Dialogs(view))},
                Sub(name, shown)

                    Dim cell As ScalingMeasurementTests.Cell =
                        ScalingMeasurementTests.Measure(shown, dpi, ScalingMeasurementTests.ScalingOf(view).ScalesWithWinForms, screen).Single().Result

                    Dim at As String = $"At {dpi} DPI on {screen(0).Name}"
                    Dim list As ListBox = ShowCorrespondence(shown)
                    Dim hint As Label = list.Controls.OfType(Of Label)().Single()

                    Assert.IsTrue(hint.Visible, "The empty list shows its hint.")
                    AssertWholeHint(hint, at)

                    ' In reach of the body's scroll, all of it.
                    Dim body As Panel = shown.Controls.OfType(Of Panel)().Single(Function(panel) panel.AutoScroll)
                    Dim reach As Integer = Math.Max(body.DisplayRectangle.Height, body.ClientSize.Height)
                    Dim foot As Integer = body.RectangleToClient(list.Parent.RectangleToScreen(list.Bounds)).Bottom - body.DisplayRectangle.Y
                    Assert.IsTrue(foot <= reach, $"{at} the list's foot, {foot} pixels down the body, is in reach of its {reach}-pixel scroll.")

                    Assert.IsNull(cell.NotMeasuredHere, "The dialog is measured on this computer: " & cell.Describe())
                    Assert.IsTrue(cell.AcceptInside.HasValue AndAlso cell.AcceptInside.Value, $"{at} Close is in view: " & cell.Describe())
                    Assert.IsFalse(cell.Cut.Any(Function(cut) cut.EndsWith(" under two rows", StringComparison.Ordinal)), $"{at} every list keeps two rows: " & cell.Describe())

                    If screen(0).Area.Size = New Size(width, height) Then
                        Assert.IsTrue(cell.Passes, $"Submission Details {at}: " & cell.Describe())
                    End If

                End Sub
            )

        Assert.AreEqual(0, failures.Count, Environment.NewLine & String.Join(Environment.NewLine, failures))

    End Sub

    ' The Correspondence & Files page in front, and its list, which the
    ' sample leaves empty.
    Private Shared Function ShowCorrespondence(detail As Control) As ListBox

        Dim list As ListBox = Descendants(detail).OfType(Of ListBox)().Single(Function(item) item.AccessibleName = "Correspondence and files")
        Assert.AreEqual(0, list.Items.Count, "The sample submission has no letters or files.")

        Dim page As Control = list.Parent

        While Not TypeOf page Is TabPage
            page = page.Parent
        End While

        DirectCast(page.Parent, TabControl).SelectedTab = DirectCast(page, TabPage)
        Application.DoEvents()

        Return list

    End Function

    ' The hint's text, wrapped at its width inside its padding, and the
    ' padding fit its height.
    Private Shared Sub AssertWholeHint(hint As Label, at As String)

        Dim width As Integer = hint.ClientSize.Width - hint.Padding.Horizontal

        Dim needed As Integer =
            TextRenderer.MeasureText(hint.Text, hint.Font, New Size(width, Integer.MaxValue), TextFormatFlags.WordBreak Or TextFormatFlags.NoPrefix).Height +
            hint.Padding.Vertical

        Assert.IsTrue(hint.ClientSize.Height >= needed, $"{at} the hint has {hint.ClientSize.Height} of the {needed} pixels its text and padding need at {hint.ClientSize.Width} wide.")

    End Sub

    ' Hosted in the manuscript page, the detail is built as before and its
    ' lists and details boxes keep two rows. The page sits in the main
    ' window, which scales by hand, where a simulated display tells
    ' nothing, so it is measured at the main window's minimum size at this
    ' computer's display, which any screen holds, a 1024x768 build runner's
    ' included. A guard: it cannot fail on the code before the dialog's
    ' change, which left the hosted detail as it was.
    <TestMethod>
    Public Sub HostedTheDetailsListsKeepTwoRows()

        Dim hosted As ViewCatalog.NamedView =
            ViewCatalog.Hosted.Single(Function(entry) entry.Name = "SubmissionDetailsForm inside the manuscript page")

        Dim inDetail As String() = {"'Editorial history'", "'Selected decision details'", "'Correspondence and files'", "'Selected item details'"}

        Dim failures As List(Of String) =
            ViewCatalog.ShowEach(
                {(hosted.Name, hosted.Build)},
                Sub(name, host)

                    ' At the main window's minimum size, the smallest a
                    ' researcher can make it.
                    host.Size = host.MinimumSize
                    host.PerformLayout()
                    Application.DoEvents()

                    Dim submission As JournalSubmission =
                        DirectCast(GetType(SubmissionDetailsForm).GetField("_submission", BindingFlags.Instance Or BindingFlags.NonPublic).GetValue(Descendants(host).OfType(Of SubmissionDetailsForm)().Single()), JournalSubmission)
                    Descendants(host).OfType(Of EditManuscriptForm)().Single().ShowSubmission(submission.Id, showResponses:=False)
                    Application.DoEvents()

                    Assert.IsTrue(Descendants(host).OfType(Of SubmissionDetailsForm)().Single().Visible, "The detail is in view, so its lists are measured.")

                    Dim screen As (Name As String, Area As Rectangle)() = {("its own size", New Rectangle(New Point(-30000, -30000), host.Size))}

                    ' At 96 the measure simulates nothing.
                    Dim cell As ScalingMeasurementTests.Cell =
                        ScalingMeasurementTests.Measure(host, 96, False, screen).Single().Result

                    Dim squeezed As List(Of String) =
                        cell.Cut.Where(Function(cut) inDetail.Any(Function(part) cut.Contains(part, StringComparison.Ordinal))).ToList()

                    Assert.AreEqual(0, squeezed.Count, $"Hosted at the main window's minimum size {host.Size}, the detail's lists keep two rows: " & String.Join("; ", squeezed))

                End Sub
            )

        Assert.AreEqual(0, failures.Count, Environment.NewLine & String.Join(Environment.NewLine, failures))

    End Sub

    ' Hosted in the manuscript page at the main window's minimum size
    ' (960x680), the detail's Correspondence & Files page was 310x223: the
    ' toolbar's help ran past its row, "+ Add Item" wrapped below the other
    ' buttons and its row cut it by 16 pixels, and the empty list, 94x101,
    ' had 99 of the 317 pixels its hint needs at that width (#217). On the
    ' page's first open and on the kept page opened again, at the main
    ' window's minimum and default sizes, on a submission with no letters
    ' or files, every control of the toolbar is inside its row and the
    ' empty list's hint is whole, in view or in reach of a scroll. With an
    ' item that has a file and a source selected, which shows all seven
    ' buttons, every control of the toolbar is inside its row too, there
    ' and once the window is made narrow again, where the list lies above
    ' its box as on a page opened narrow. The main window scales by hand,
    ' so this is at this computer's display. On the code before the change
    ' both sizes failed: the empty list at the default size alone passed,
    ' and the selected item there had "+ Add Item" cut by 16 pixels.
    <TestMethod>
    <DataRow("minimum")>
    <DataRow("default")>
    Public Sub HostedTheCorrespondenceToolbarAndHintAreWhole(sizeName As String)

        Dim hosted As List(Of (Name As String, Build As Func(Of String, Form))) =
            ViewCatalog.Hosted.
                Where(Function(entry) entry.Name = "SubmissionDetailsForm inside the manuscript page" OrElse
                                      entry.Name = "The manuscript page opened a second time").
                Select(Function(entry) (entry.Name, entry.Build)).
                ToList()
        Assert.AreEqual(2, hosted.Count, "Both views are in the catalog.")

        Dim failures As List(Of String) =
            ViewCatalog.ShowEach(
                hosted,
                Sub(name, host)

                    ' The main window's minimum size, or the size it opens
                    ' at on a screen with room for it.
                    host.Size = If(sizeName = "minimum", host.MinimumSize, Form1.StartSizes(New Rectangle(0, 0, 3840, 2160), host.DeviceDpi).Start)
                    host.PerformLayout()
                    Application.DoEvents()

                    ' The page's Submissions section in front, on the submission.
                    Dim submission As JournalSubmission =
                        DirectCast(GetType(SubmissionDetailsForm).GetField("_submission", BindingFlags.Instance Or BindingFlags.NonPublic).GetValue(Descendants(host).OfType(Of SubmissionDetailsForm)().Single()), JournalSubmission)
                    Descendants(host).OfType(Of EditManuscriptForm)().Single().ShowSubmission(submission.Id, showResponses:=False)
                    Application.DoEvents()

                    Dim detail As SubmissionDetailsForm = Descendants(host).OfType(Of SubmissionDetailsForm)().Single()
                    Dim list As ListBox = ShowCorrespondence(detail)
                    Dim at As String = $"At the main window's {sizeName} size {host.Size}"
                    Dim problems As New List(Of String)()

                    Dim buttons As Control = Descendants(detail).OfType(Of Button)().Single(Function(button) button.Text = "+ Add Item").Parent

                    CheckToolbar(buttons, at, problems)

                    Dim hint As Label = list.Controls.OfType(Of Label)().Single()
                    Assert.IsTrue(hint.Visible, "The empty list shows its hint.")

                    Try
                        AssertWholeHint(hint, at)
                    Catch ex As AssertFailedException
                        problems.Add(ex.Message)
                    End Try

                    CheckInReach(list, detail, at, problems)

                    ' An item with a file and a source, selected, shows all
                    ' seven buttons, which wrap at both sizes. Its file is
                    ' under the view's temporary root and is never made, and
                    ' nothing opens it or the source. It is taken out again,
                    ' so the editor closes with nothing changed.
                    Dim shownSubmission As JournalSubmission =
                        DirectCast(GetType(SubmissionDetailsForm).GetField("_submission", BindingFlags.Instance Or BindingFlags.NonPublic).GetValue(detail), JournalSubmission)
                    Dim refresh As MethodInfo = GetType(SubmissionDetailsForm).GetMethod("RefreshCorrespondenceList", BindingFlags.Instance Or BindingFlags.NonPublic)
                    Dim letter As New CorrespondenceItem With {
                        .Type = CorrespondenceType.DecisionLetter,
                        .Title = "Decision letter",
                        .LocalFilePath = IO.Path.Combine(Services.StorageEnvironment.RootOverride, "Never made", "Decision letter.pdf"),
                        .SourceUrl = "https://example.invalid/letter"
                    }

                    shownSubmission.Correspondence.Add(letter)

                    Try

                        refresh.Invoke(detail, Nothing)
                        WaitForTheSaveBar(host)

                        list.SelectedIndex = 0
                        Application.DoEvents()

                        Dim selected As String = " with an item that has a file and a source selected,"
                        Assert.AreEqual(7, buttons.Controls.Cast(Of Control)().Count(Function(item) item.Visible), "The selected item shows all seven buttons.")
                        CheckToolbar(buttons, at & selected, problems)

                        ' With it selected, made wide from the minimum size and
                        ' narrow again, or narrow from the default size, the
                        ' toolbar stays whole and the page lays the list above
                        ' its box, as a page opened narrow does.
                        If sizeName = "minimum" Then

                            host.Size = Form1.StartSizes(New Rectangle(0, 0, 3840, 2160), host.DeviceDpi).Start
                            host.PerformLayout()
                            Application.DoEvents()

                            CheckToolbar(buttons, $"Made wide again, at the main window's default size {host.Size}{selected}", problems)

                        End If

                        host.Size = host.MinimumSize
                        host.PerformLayout()
                        Application.DoEvents()

                        Dim narrowed As String = $"Made narrow again, at the main window's minimum size {host.Size}{selected}"
                        Dim split As SplitContainer = DirectCast(list.Parent.Parent, SplitContainer)

                        If split.Orientation <> Orientation.Horizontal Then
                            problems.Add($"{narrowed} the list {list.Size} lies beside its box in the split {split.Size}, not above it")
                        End If

                        CheckToolbar(buttons, narrowed, problems)
                        CheckInReach(list, detail, narrowed, problems)

                    Finally

                        shownSubmission.Correspondence.Remove(letter)
                        refresh.Invoke(detail, Nothing)
                        Application.DoEvents()

                    End Try

                    Assert.AreEqual(0, problems.Count, Environment.NewLine & String.Join(Environment.NewLine, problems))

                End Sub
            )

        Assert.AreEqual(0, failures.Count, Environment.NewLine & String.Join(Environment.NewLine, failures))

    End Sub

    ' The Unsaved changes bar, which the main window shows on its next look
    ' once the record differs from its saved copy, as it does after a
    ' researcher adds an item; it takes height from the page, so the page
    ' is measured with it shown, not as the timer happens to fall.
    Private Shared Sub WaitForTheSaveBar(host As Form)

        Dim bar As Control = DirectCast(GetType(Form1).GetField("manuscriptSaveBar", BindingFlags.Instance Or BindingFlags.NonPublic).GetValue(host), Control)
        Dim waited As Diagnostics.Stopwatch = Diagnostics.Stopwatch.StartNew()

        While Not bar.Visible AndAlso waited.ElapsedMilliseconds < 10000
            Application.DoEvents()
            Thread.Sleep(10)
        End While

        Assert.IsTrue(bar.Visible, "The added item shows the Unsaved changes bar.")
        Application.DoEvents()

    End Sub

    ' The Correspondence & Files toolbar: its help in its first row, its
    ' buttons' row in its second, the toolbar in the page's first, and
    ' every button shown inside the buttons' row.
    Private Shared Sub CheckToolbar(buttons As Control, at As String, problems As List(Of String))

        Dim toolbar As TableLayoutPanel = DirectCast(buttons.Parent, TableLayoutPanel)

        CheckInsideRow(toolbar.GetControlFromPosition(0, 0), at, problems)
        CheckInsideRow(buttons, at, problems)
        CheckInsideRow(toolbar, at, problems)

        For Each button As Control In buttons.Controls.Cast(Of Control)().Where(Function(item) item.Visible)
            If Not buttons.ClientRectangle.Contains(button.Bounds) Then
                problems.Add($"{at} the button '{button.Text}' {button.Bounds} is outside its row {buttons.ClientRectangle}")
            End If
        Next

    End Sub

    ' A control is inside its cell of the table that holds it, and the cell
    ' inside the table's client area, or in reach of its scroll in a table
    ' that scrolls: at the main window's minimum size, with the Unsaved
    ' changes bar shown and seven buttons, the page is shorter than its
    ' toolbar and scrolls to it.
    Private Shared Sub CheckInsideRow(control As Control, at As String, problems As List(Of String))

        Dim table As TableLayoutPanel = DirectCast(control.Parent, TableLayoutPanel)
        Dim position As TableLayoutPanelCellPosition = table.GetPositionFromControl(control)
        Dim widths As Integer() = table.GetColumnWidths()
        Dim heights As Integer() = table.GetRowHeights()

        ' A table's display area is inside its padding.
        Dim cell As New Rectangle(
            table.DisplayRectangle.X + widths.Take(position.Column).Sum(),
            table.DisplayRectangle.Y + heights.Take(position.Row).Sum(),
            widths(position.Column),
            heights(position.Row))

        ' The scroll range is the display area and its padding.
        Dim area As Rectangle =
            If(table.AutoScroll,
               New Rectangle(
                   table.DisplayRectangle.X - table.Padding.Left,
                   table.DisplayRectangle.Y - table.Padding.Top,
                   table.DisplayRectangle.Width + table.Padding.Horizontal,
                   table.DisplayRectangle.Height + table.Padding.Vertical),
               table.ClientRectangle)

        Dim inside As Rectangle = Rectangle.Intersect(cell, area)

        If Not inside.Contains(control.Bounds) Then
            problems.Add($"{at} the {control.GetType().Name} '{control.Text}' {control.Bounds} is outside its row {inside} (row {position.Row}, the table's {If(table.AutoScroll, "scroll range", "client area")} {area})")
        End If

    End Sub

    ' A control is whole inside each container up to the given one, or in
    ' reach of a container's scroll; above a container that scrolls, that
    ' container is.
    Private Shared Sub CheckInReach(control As Control, upTo As Control, at As String, problems As List(Of String))

        Dim shown As Control = control

        While shown IsNot upTo

            Dim ancestor As Control = shown.Parent
            Dim scrolls As Boolean = TypeOf ancestor Is ScrollableControl AndAlso DirectCast(ancestor, ScrollableControl).AutoScroll
            Dim area As Rectangle = If(scrolls, ancestor.DisplayRectangle, ancestor.ClientRectangle)

            If Not area.Contains(shown.Bounds) Then
                problems.Add($"{at} the {shown.GetType().Name} '{shown.AccessibleName}' {shown.Bounds} is outside the {ancestor.GetType().Name}'s {If(scrolls, "scroll range", "client area")} {area}")
                Return
            End If

            shown = ancestor

        End While

    End Sub

    ' The dialog opened at its 780-pixel minimum height on a 1366x768
    ' screen, whose working area is 720 pixels tall at 100% and 708 at 125%
    ' with Windows 11's taskbar, centered as if it were shorter: its lowest
    ' rows and Close lay behind the taskbar and off the screen (#93, step
    ' 4). The measure and the display evidence tool fit each window to its
    ' screen themselves, so neither saw it. On those screens the dialog now
    ' opens inside the working area, its minimum lowered to fit, and where
    ' it has room it opens at 1040x880 with its minimum as it was.
    <TestMethod>
    Public Sub TheDialogOpensInsideItsScreensWorkingArea()

        Dim minimum As New Size(780, 780)

        For Each area As Rectangle In {New Rectangle(0, 0, 1366, 720), New Rectangle(0, 0, 1366, 708), New Rectangle(0, 0, 1920, 1008), New Rectangle(0, 0, 1024, 728)}

            Dim opening As (Bounds As Rectangle, Minimum As Size) =
                SubmissionDetailsForm.OpeningBounds(area, minimum)

            Assert.IsTrue(area.Contains(opening.Bounds), $"On {area.Size} the dialog opens inside the working area: {opening.Bounds}")
            Assert.IsTrue(opening.Bounds.Width >= opening.Minimum.Width AndAlso opening.Bounds.Height >= opening.Minimum.Height, $"On {area.Size} it opens at or above its minimum {opening.Minimum}: {opening.Bounds}")

        Next

        Dim roomy As (Bounds As Rectangle, Minimum As Size) =
            SubmissionDetailsForm.OpeningBounds(New Rectangle(0, 0, 1920, 1200), minimum)

        Assert.AreEqual(New Size(1040, 880), roomy.Bounds.Size, "With room, its size is as it was.")
        Assert.AreEqual(minimum, roomy.Minimum, "With room, its minimum is as it was.")

    End Sub

    ' Whole where a researcher can see it: a control inside a body that
    ' scrolls (the dialog's since #214) is judged once it is scrolled into
    ' view, as the measure judges what a scroll reaches. On a short screen
    ' the body can open scrolled, so a caption at its top starts above the
    ' visible area without being cut.
    Private Shared Sub AssertInsideAncestors(control As Control)
        Assert.IsTrue(control.Visible)
        Assert.IsTrue(control.Width > 0 AndAlso control.Height > 0)
        Dim scroller As Control = control.Parent
        While scroller IsNot Nothing
            Dim scrollable As ScrollableControl = TryCast(scroller, ScrollableControl)
            If scrollable IsNot Nothing AndAlso scrollable.AutoScroll Then
                scrollable.ScrollControlIntoView(control)
                Application.DoEvents()
            End If
            scroller = scroller.Parent
        End While
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
