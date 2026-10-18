Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.IO
Imports System.Linq
Imports System.Reflection
Imports System.Runtime.InteropServices
Imports System.Windows.Forms
Imports ManuscriptPipeline
Imports ManuscriptPipeline.Controls
Imports ManuscriptPipeline.Forms
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services
Imports Microsoft.VisualStudio.TestTools.UnitTesting

' The Deadlines page drawn by one list (#133): every row, button, heading
' and line in one window, kept between visits. The rows' actions, the keys
' that moved between them and pressed them, what a screen reader saw, the
' pointer, and the theme colors are each held to what the page's controls
' did before.
<TestClass>
<DoNotParallelize>
Public Class DeadlineListTests

    Private Const WM_KEYDOWN As Integer = &H100
    Private Const WM_KEYUP As Integer = &H101
    Private Const WM_CONTEXTMENU As Integer = &H7B
    Private Const WM_LBUTTONDOWN As Integer = &H201
    Private Const WM_LBUTTONUP As Integer = &H202
    Private Const WM_LBUTTONDBLCLK As Integer = &H203
    Private Const WM_RBUTTONDOWN As Integer = &H204
    Private Const WM_RBUTTONUP As Integer = &H205
    Private Const WM_UPDATEUISTATE As Integer = &H128
    Private Const UIS_CLEAR As Integer = 2
    Private Const UISF_HIDEFOCUS As Integer = 1
    Private Const OBJID_CLIENT As Integer = -4
    Private Const EVENT_OBJECT_FOCUS As UInteger = &H8005UI

    Private _root As String = String.Empty

    Public Property TestContext As TestContext


    <TestInitialize>
    Public Sub Initialize()
        _root = CreateTemporaryRoot()
        StorageEnvironment.RootOverride = _root
    End Sub


    <TestCleanup>
    Public Sub Cleanup()
        StorageEnvironment.RootOverride = Nothing
        DeleteTemporaryRoot(_root)
    End Sub


    ' ---------------------------------------------------------------
    ' The rows
    ' ---------------------------------------------------------------

    ' The buttons a row draws, where the list draws them, look exactly like
    ' the ActionButtons the row hosted (Secondary, the "⋯" Quiet, neither
    ' with a mnemonic, on the card): enabled, disabled, and each with the
    ' keyboard's focus ring, in Light, Dark and a contrast theme.
    <TestMethod>
    <DataRow(SystemColorMode.Classic, False)>
    <DataRow(SystemColorMode.Dark, False)>
    <DataRow(SystemColorMode.Classic, True)>
    Public Sub DeadlineDrawnButtonsMatchActionButton(mode As SystemColorMode, contrast As Boolean)

        Dim differences As New List(Of String)()

        StaRunner.Run(
            Sub()
                UiTheme.HighContrastOverride = contrast
                Try
                    Using host As New Form With {.StartPosition = FormStartPosition.Manual, .Location = New Point(-20000, -20000), .ShowInTaskbar = False,
                                                  .ClientSize = New Size(900, 260)}
                        Dim list As New DeadlineList With {.Bounds = New Rectangle(0, 0, 900, 120), .AccessibleName = "Deadlines"}
                        Dim surface As New Panel With {.Bounds = New Rectangle(0, 130, 900, 120), .BackColor = UiTheme.CardBackground()}
                        host.Controls.Add(list)
                        host.Controls.Add(surface)
                        Dim today As DateTime = DateTime.Today
                        Dim row As DeadlineRow = DrawnRow(New DeadlineItem With {.Kind = DeadlineKind.Reminder, .Group = DeadlineGroup.Later, .Title = "Send draft", .ManuscriptTitle = "Beta", .DueDate = today.AddDays(20)}, today)
                        list.SetEntries({DeadlineEntry.ForRow(row)})
                        host.Show()
                        Application.DoEvents()

                        ' A button for each part, at the size the list draws it.
                        Dim references As New List(Of ActionButton)()
                        Dim left As Integer = 10
                        For part As Integer = 0 To row.MorePart
                            Dim more As Boolean = part = row.MorePart
                            Dim reference As New ActionButton With {
                                .Text = If(more, "⋯", row.Buttons(part).Text),
                                .Role = If(more, ActionButtonRole.Quiet, ActionButtonRole.Secondary),
                                .UseMnemonic = False,
                                .Bounds = New Rectangle(New Point(left, 10), list.ButtonBounds(row, part).Size)
                            }
                            surface.Controls.Add(reference)
                            references.Add(reference)
                            left += reference.Width + 10
                        Next
                        Application.DoEvents()

                        ' The part of the list's picture where it drew a button,
                        ' against that button's reference as it is drawn now.
                        Dim compare As Action(Of String, Integer, Bitmap) =
                            Sub(state, part, drawn)
                                Using actual As Bitmap = drawn.Clone(list.ButtonBounds(row, part), drawn.PixelFormat), expected As Bitmap = Snapshot(references(part))
                                    Dim difference As String = FirstDifference(expected, actual)
                                    If difference IsNot Nothing Then differences.Add(state & ", " & references(part).Text & ": " & difference)
                                End Using
                            End Sub

                        For Each enabled As Boolean In {True, False}
                            list.Enabled = enabled
                            references.ForEach(Sub(reference) reference.Enabled = enabled)
                            Application.DoEvents()
                            Using drawn As Bitmap = Snapshot(list)
                                For part As Integer = 0 To row.MorePart
                                    compare(If(enabled, "enabled", "disabled"), part, drawn)
                                Next
                            End Using
                        Next
                        list.Enabled = True
                        references.ForEach(Sub(reference) reference.Enabled = True)

                        ' Tab from the row to each of its buttons in turn, drawn
                        ' with the focus ring; then the reference takes the focus.
                        Assert.IsTrue(list.Focus())
                        For part As Integer = 0 To row.MorePart
                            GetType(DeadlineList).GetMethod("ProcessDialogKey", BindingFlags.Instance Or BindingFlags.NonPublic).Invoke(list, New Object() {Keys.Tab})
                            Assert.AreEqual(part, list.FocusedPart, "Tab reaches each button in turn.")
                            ShowFocusCues(list)
                            Using drawn As Bitmap = Snapshot(list)
                                Assert.IsTrue(references(part).Focus())
                                ShowFocusCues(references(part))
                                compare("with the focus ring", part, drawn)
                            End Using
                            Assert.IsTrue(list.Focus())
                        Next

                        host.Close()
                    End Using
                Finally
                    UiTheme.HighContrastOverride = Nothing
                End Try
            End Sub, mode:=mode)

        Assert.AreEqual(0, differences.Count, String.Join(Environment.NewLine, differences))

    End Sub


    ' Every kind keeps its actions, in menu order, its buttons and its "⋯"
    ' exactly as the page's own code builds them.
    <TestMethod>
    Public Sub DeadlineRowsOfEveryKindKeepTheirActions()

        RunOnStaThread(
            Sub()
                Using board As New DeadlineBoard(_root)
                    Dim today As DateTime = DateTime.Today
                    Dim expected = {
                        (New DeadlineItem With {.Kind = DeadlineKind.Reminder, .Group = DeadlineGroup.Done, .Title = "Done", .DueDate = today, .CompletedDate = today},
                            {"Open Manuscript"}, New String() {}, True),
                        (New DeadlineItem With {.Kind = DeadlineKind.Revision, .Group = DeadlineGroup.NoDate, .Title = "Revision has no deadline"},
                            {"Set Deadline...", "Open"}, {"Set Deadline...", "Open"}, False),
                        (New DeadlineItem With {.Kind = DeadlineKind.Revision, .Group = DeadlineGroup.Overdue, .Title = "Revision due", .DueDate = today.AddDays(-1)},
                            {"Open", "Postpone..."}, {"Open", "Postpone..."}, False),
                        (New DeadlineItem With {.Kind = DeadlineKind.FollowUp, .Group = DeadlineGroup.Next7Days, .Title = "Follow up", .DueDate = today.AddDays(2)},
                            {"Open", "Postpone...", "Clear Follow-up..."}, {"Open", "Postpone..."}, True),
                        (New DeadlineItem With {.Kind = DeadlineKind.Reminder, .Group = DeadlineGroup.Today, .Title = "Reminder", .DueDate = today},
                            {"Done", "Postpone...", "Edit Reminder...", "Open Manuscript"}, {"Done", "Postpone..."}, True),
                        (New DeadlineItem With {.Kind = DeadlineKind.Publication, .Group = DeadlineGroup.NoDate, .Title = "May have been published"},
                            {"Review Match", "Mark Published...", "Ignore Match", "Open Manuscript"}, {"Review Match", "Mark Published..."}, True),
                        (New DeadlineItem With {.Kind = DeadlineKind.Preparation, .Group = DeadlineGroup.NoDate, .Title = "Packet", .ProgressDone = 1, .ProgressTotal = 3},
                            {"Open Readiness"}, {"Open Readiness"}, False)
                    }

                    For Each kind In expected
                        Dim row As DeadlineRow = board.CreateRow(kind.Item1, today)
                        Dim name As String = kind.Item1.Kind.ToString() & " in " & kind.Item1.Group.ToString()
                        CollectionAssert.AreEqual(kind.Item2, row.ActionTexts.ToList(), name & ": the menu")
                        CollectionAssert.AreEqual(kind.Item3, row.Buttons.Select(Function(button) button.Text).ToList(), name & ": the buttons")
                        Assert.AreEqual(kind.Item4, row.HasMore, name & ": the ⋯ button")
                        Assert.AreEqual(If(kind.Item3.Length = 0, Nothing, kind.Item3(0)), row.Primary?.Text, name & ": Enter's action")
                        If row.HasMore Then Assert.AreEqual("More actions for " & kind.Item1.Title, row.MoreName)
                    Next
                End Using
            End Sub)

    End Sub


    ' ---------------------------------------------------------------
    ' The keyboard
    ' ---------------------------------------------------------------

    ' Tab reaches each row, its buttons and its "⋯", then the Done line;
    ' Up and Down go from row to row; Left and Right go across the rows and
    ' the Done line; on a button the arrows stay among the row's buttons;
    ' Enter runs a row's first action or presses a button; Space presses a
    ' button when let go; the context-menu key opens the row's actions.
    <TestMethod>
    Public Sub DeadlinesKeyboardMovesTabsAndPressesAsTheRowControlsDid()

        RunOnStaThread(
            Sub()
                Using board As New DeadlineBoard(_root)
                    Dim library As Sample = Sample.Create()
                    board.Prepare(library.Manuscripts)
                    board.OpenDeadlines()
                    board.SetPrompts(Function(item) DateTime.Today.AddDays(20), Function(question) True)
                    Dim list As DeadlineList = board.List()

                    ' From the last chip, Tab enters the list at its first row.
                    Dim lastChip As FilterChip = ViewCatalog.Descendants(board).OfType(Of FilterChip)().Last(Function(chip) chip.Visible)
                    Assert.IsTrue(lastChip.Focus(), "The chip takes focus.")
                    Press(board, Keys.Tab)
                    Assert.IsTrue(list.Focused, "Tab after the chips reaches the list.")
                    AssertAt(list, "Book the room", DeadlineRow.ItemPart)

                    Press(board, Keys.Tab)
                    AssertAt(list, "Book the room", 0)
                    Press(board, Keys.Tab)
                    AssertAt(list, "Book the room", 1)
                    Press(board, Keys.Tab)
                    AssertAt(list, "Book the room", 2)
                    Press(board, Keys.Tab)
                    AssertAt(list, "Send draft to coauthors", DeadlineRow.ItemPart)

                    ShiftTab(list)
                    AssertAt(list, "Book the room", 2)

                    ' On a button, the arrows stay among the row's buttons, wrapping.
                    Press(board, Keys.Down)
                    AssertAt(list, "Book the room", 0)
                    Press(board, Keys.Up)
                    AssertAt(list, "Book the room", 2)
                    Press(board, Keys.Left)
                    AssertAt(list, "Book the room", 1)

                    ShiftTab(list)
                    ShiftTab(list)
                    AssertAt(list, "Book the room", DeadlineRow.ItemPart)

                    ' Up and Down go from row to row and stop at the ends.
                    Press(board, Keys.Up)
                    AssertAt(list, "Book the room", DeadlineRow.ItemPart)
                    Press(board, Keys.Down)
                    AssertAt(list, "Send draft to coauthors", DeadlineRow.ItemPart)
                    Press(board, Keys.Down)
                    AssertAt(list, "Follow up with Assessment", DeadlineRow.ItemPart)
                    Press(board, Keys.Down)
                    AssertAt(list, "Revision has no deadline", DeadlineRow.ItemPart)
                    Press(board, Keys.Down)
                    AssertAt(list, "Revision has no deadline", DeadlineRow.ItemPart)

                    ' Left and Right go across the rows and the Done line, wrapping.
                    Press(board, Keys.Right)
                    Assert.IsTrue(list.DoneLineFocused, "Right reaches the Done line.")
                    Press(board, Keys.Right)
                    AssertAt(list, "Book the room", DeadlineRow.ItemPart)
                    Press(board, Keys.Left)
                    Assert.IsTrue(list.DoneLineFocused, "Left wraps back to the Done line.")
                    Press(board, Keys.Down)
                    AssertAt(list, "Book the room", DeadlineRow.ItemPart)
                    Press(board, Keys.Up)
                    AssertAt(list, "Book the room", DeadlineRow.ItemPart)
                    Press(board, Keys.Left)
                    Assert.IsTrue(list.DoneLineFocused)

                    ' Enter on the Done line shows what was done; the last Tab stop
                    ' is then the done row's "⋯".
                    Press(board, Keys.Enter)
                    Assert.AreEqual(5, list.Rows.Count, "Enter on the Done line shows the done reminder.")
                    Assert.IsTrue(list.DoneLineFocused, "The keyboard stays on the Done line.")
                    Press(board, Keys.Tab)
                    AssertAt(list, "Upload preregistration", DeadlineRow.ItemPart)
                    Press(board, Keys.Enter)
                    Assert.AreEqual(0, board.SaveCount, "A done row has no first action.")
                    Press(board, Keys.Tab)
                    AssertAt(list, "Upload preregistration", list.FocusedRow.MorePart)
                    Press(board, Keys.Tab)
                    Assert.IsFalse(list.Focused, "Tab past the last stop leaves the list.")

                    ' Enter on a row runs its first action: Done, for a reminder.
                    list.Focus()
                    FocusRow(board, list, "Send draft to coauthors")
                    Press(board, Keys.Enter)
                    Assert.IsTrue(library.Today.IsCompleted, "Enter marks the reminder done.")
                    Assert.AreEqual(1, board.SaveCount)

                    ' Space presses a button when it is let go.
                    FocusRow(board, list, "Follow up with Assessment")
                    Press(board, Keys.Tab)
                    Press(board, Keys.Tab)
                    AssertAt(list, "Follow up with Assessment", 1)
                    Post(list, WM_KEYDOWN, CInt(Keys.Space), 1)
                    Application.DoEvents()
                    Assert.AreEqual(1, board.SaveCount, "Space presses on release, not on the key going down.")
                    Post(list, WM_KEYUP, CInt(Keys.Space), &HC0000001)
                    Application.DoEvents()
                    Assert.AreEqual(DateTime.Today.AddDays(20), library.FollowUp.FollowUpDate.Value, "Space on Postpone... moved the follow-up.")
                    Assert.AreEqual(2, board.SaveCount)

                    ' The context-menu key opens every action of the row.
                    FocusRow(board, list, "Book the room")
                    Using menu As New MenuWatch(list.Menu)
                        PostMessage(list.Handle, WM_CONTEXTMENU, list.Handle, New IntPtr(-1))
                        Application.DoEvents()
                        CollectionAssert.AreEqual({"Done", "Postpone...", "Edit Reminder...", "Open Manuscript"}, menu.Opened,
                                                  "The context-menu key opens the row's menu. " & menu.Events)
                    End Using
                    Assert.AreEqual(2, board.SaveCount, "Opening the menu saves nothing.")

                    board.Close()
                End Using
            End Sub)

    End Sub


    ' Refilling the list (a keystroke in the filter, a chip, a save that
    ' renders the library) keeps the keyboard on the row at the same place.
    <TestMethod>
    Public Sub DeadlinesKeepTheFocusedItemAcrossARefill()

        RunOnStaThread(
            Sub()
                Using board As New DeadlineBoard(_root)
                    Dim library As Sample = Sample.Create()
                    board.Prepare(library.Manuscripts)
                    board.OpenDeadlines()
                    board.SetPrompts(Function(item) DateTime.Today.AddDays(30), Function(question) True)
                    Dim list As DeadlineList = board.List()

                    Assert.IsTrue(list.Focus())
                    FocusRow(board, list, "Follow up with Assessment")

                    board.Filter().Text = "study"
                    Application.DoEvents()
                    Assert.IsTrue(list.Focused)
                    AssertAt(list, "Follow up with Assessment", DeadlineRow.ItemPart)

                    ' A chip that leaves fewer rows keeps the place, or the last row.
                    ClickChip(board, "Reminders")
                    Assert.AreEqual(2, list.Rows.Count)
                    Assert.AreEqual(1, list.Rows.ToList().IndexOf(list.FocusedRow), "The keyboard is on the last row left.")
                    ClickChip(board, "Reminders")
                    Assert.AreEqual(4, list.Rows.Count)
                    Assert.AreEqual(1, list.Rows.ToList().IndexOf(list.FocusedRow))

                    ' An action's save renders the library, which refills the list.
                    FocusRow(board, list, "Follow up with Assessment")
                    Press(board, Keys.Tab)
                    Press(board, Keys.Tab)
                    Press(board, Keys.Enter)
                    Assert.AreEqual(1, board.SaveCount)
                    Assert.AreEqual(DeadlineGroup.Later, list.Rows.Single(Function(row) row.Item.Kind = DeadlineKind.FollowUp).Item.Group)
                    Assert.IsTrue(list.Focused)
                    Assert.AreEqual(2, list.Rows.ToList().IndexOf(list.FocusedRow), "The keyboard is on the row at the same place.")
                    Assert.AreEqual(DeadlineRow.ItemPart, list.FocusedPart)

                    ' Away from the list, a refill leaves nothing chosen.
                    board.Filter().Focus()
                    board.Filter().Text = "s"
                    Application.DoEvents()
                    Assert.IsNull(list.FocusedRow)

                    board.Close()
                End Using
            End Sub)

    End Sub


    ' Moving the keyboard deep into a long list scrolls the row into view.
    <TestMethod>
    Public Sub DeadlinesFocusDeepInTheListScrollsIntoView()

        RunOnStaThread(
            Sub()
                Using board As New DeadlineBoard(_root)
                    board.Prepare(ManyReminders(60))
                    board.OpenDeadlines()
                    Dim list As DeadlineList = board.List()
                    Assert.AreEqual(60, list.Rows.Count)

                    Assert.IsTrue(list.Focus())
                    For index As Integer = 1 To 40
                        Post(list, WM_KEYDOWN, CInt(Keys.Down), 1)
                    Next
                    Application.DoEvents()

                    Assert.AreEqual(40, list.Rows.ToList().IndexOf(list.FocusedRow))
                    Assert.IsTrue(list.AutoScrollPosition.Y < 0, "The list scrolled.")
                    Dim bounds As Rectangle = list.ButtonBounds(list.FocusedRow, DeadlineRow.ItemPart)
                    Assert.IsTrue(list.ClientRectangle.Contains(bounds), "The row is in view: " & bounds.ToString() & " in " & list.ClientRectangle.ToString())

                    ' Back up to the top row brings the list back to the top.
                    For index As Integer = 1 To 40
                        Post(list, WM_KEYDOWN, CInt(Keys.Up), 1)
                    Next
                    Application.DoEvents()
                    Assert.AreEqual(0, list.Rows.ToList().IndexOf(list.FocusedRow))
                    Assert.IsTrue(list.ClientRectangle.Contains(list.ButtonBounds(list.FocusedRow, DeadlineRow.ItemPart)))

                    board.Close()
                End Using
            End Sub)

    End Sub


    ' ---------------------------------------------------------------
    ' A screen reader
    ' ---------------------------------------------------------------

    ' What MSAA, and so Narrator through UI Automation, finds: one list named
    ' Deadlines whose children, in reading order, are each heading, each row
    ' as a list item followed by its buttons, and the Done link; and a focus
    ' event for each item or button the keyboard reaches, which resolves to
    ' that child by its number.
    <TestMethod>
    Public Sub DeadlinesScreenReaderSeesItemsButtonsHeadingsAndFocus()

        RunOnStaThread(
            Sub()
                Using board As New DeadlineBoard(_root)
                    Dim library As Sample = Sample.Create()
                    board.Prepare(library.Manuscripts)
                    board.OpenDeadlines()
                    Dim list As DeadlineList = board.List()
                    Dim glance As DeadlineGlance = ViewCatalog.Descendants(board).OfType(Of DeadlineGlance)().Single()

                    ' Neither answers UI Automation itself, which would hide the
                    ' children below from it.
                    Dim supports As PropertyInfo = GetType(Control).GetProperty("SupportsUiaProviders", BindingFlags.Instance Or BindingFlags.NonPublic)
                    Assert.IsNotNull(supports, "Control.SupportsUiaProviders exists on this runtime.")
                    Assert.IsFalse(CBool(supports.GetValue(list)), "The list is read through MSAA.")
                    Assert.IsFalse(CBool(supports.GetValue(glance)), "The glance is read through MSAA.")

                    Dim root As Accessibility.IAccessible = FromWindow(list.Handle)
                    Assert.AreEqual("Deadlines", CStr(root.accName(0)))
                    Assert.AreEqual(CInt(AccessibleRole.List), CInt(root.accRole(0)))

                    Dim expected As New List(Of (Name As String, Role As AccessibleRole)) From {
                        ("OVERDUE  1", AccessibleRole.StaticText),
                        (RowName(list, "Book the room"), AccessibleRole.ListItem),
                        ("Done", AccessibleRole.PushButton),
                        ("Postpone...", AccessibleRole.PushButton),
                        ("More actions for Book the room", AccessibleRole.PushButton),
                        ("TODAY  1", AccessibleRole.StaticText),
                        (RowName(list, "Send draft to coauthors"), AccessibleRole.ListItem),
                        ("Done", AccessibleRole.PushButton),
                        ("Postpone...", AccessibleRole.PushButton),
                        ("More actions for Send draft to coauthors", AccessibleRole.PushButton),
                        ("NEXT 7 DAYS  1", AccessibleRole.StaticText),
                        (RowName(list, "Follow up with Assessment"), AccessibleRole.ListItem),
                        ("Open", AccessibleRole.PushButton),
                        ("Postpone...", AccessibleRole.PushButton),
                        ("More actions for Follow up with Assessment", AccessibleRole.PushButton),
                        ("NO DATE  1", AccessibleRole.StaticText),
                        (RowName(list, "Revision has no deadline"), AccessibleRole.ListItem),
                        ("Set Deadline...", AccessibleRole.PushButton),
                        ("Open", AccessibleRole.PushButton),
                        ("Show done items", AccessibleRole.Link)
                    }

                    Assert.AreEqual(expected.Count, CInt(root.accChildCount))
                    For index As Integer = 0 To expected.Count - 1
                        Dim childId As Object = index + 1
                        Assert.AreEqual(expected(index).Name, CStr(root.accName(childId)), "Child " & childId.ToString())
                        Assert.AreEqual(CInt(expected(index).Role), CInt(root.accRole(childId)), "Child " & childId.ToString())
                        Assert.IsNotNull(root.accChild(childId), "Child " & childId.ToString() & " is an object of its own.")
                    Next
                    StringAssert.Contains(RowName(list, "Book the room"), "Overdue")
                    Assert.AreEqual("Done", CStr(root.accDefaultAction(2)), "Enter's action is the item's default action.")
                    Assert.AreEqual("Press", CStr(root.accDefaultAction(3)))
                    Assert.AreEqual(RowName(list, "Book the room"), CStr(root.accDescription(3)), "A button is described by its row.")
                    StringAssert.StartsWith(CStr(root.accDescription(20)), "Done in the last 30 days: 1")

                    ' Pointing at a heading, or at the Done line's words before its
                    ' link, reads that line, as each was a window of its own.
                    Dim heading As Point = Center(Child(list, "OVERDUE  1", AccessibleRole.StaticText).Bounds)
                    Assert.AreEqual("OVERDUE  1", list.AccessibilityObject.HitTest(heading.X, heading.Y)?.Name, "The heading under the pointer.")
                    Dim link As Rectangle = Child(list, "Show done items", AccessibleRole.Link).Bounds
                    Assert.AreEqual("Show done items", list.AccessibilityObject.HitTest(link.Left - UiTheme.Px(20, list.DeviceDpi), link.Top + link.Height \ 2)?.Name,
                                    "The Done line's words under the pointer.")

                    ' The glance: a group with a line for each count.
                    Dim glanceRoot As Accessibility.IAccessible = FromWindow(glance.Handle)
                    Assert.AreEqual("At a glance", CStr(glanceRoot.accName(0)))
                    Assert.AreEqual(CInt(AccessibleRole.Grouping), CInt(glanceRoot.accRole(0)))
                    CollectionAssert.AreEqual({"1 overdue", "1 due today", "1 in the next 7 days", "1 without a date"},
                                              Enumerable.Range(1, CInt(glanceRoot.accChildCount)).Select(Function(id) CStr(glanceRoot.accName(id))).ToList())

                    ' Each move of the keyboard is announced as the child it lands on.
                    Dim heard As New List(Of String)()
                    Using hook As New FocusHook(list.Handle, heard)
                        Assert.IsTrue(list.Focus())
                        Application.DoEvents()
                        Press(board, Keys.Down)
                        Press(board, Keys.Tab)
                        Press(board, Keys.Down)
                        Application.DoEvents()
                    End Using
                    CollectionAssert.AreEqual(
                        {RowName(list, "Book the room"), RowName(list, "Send draft to coauthors"), "Done", "Postpone..."},
                        heard.Where(Function(name) name IsNot Nothing).ToList(),
                        "Heard: " & String.Join(" | ", heard))

                    ' The focused child is the one the keyboard is on.
                    Dim focused As Object = root.accFocus
                    Dim focusedName As String = If(TypeOf focused Is Accessibility.IAccessible, CStr(DirectCast(focused, Accessibility.IAccessible).accName(0)), CStr(root.accName(focused)))
                    Assert.AreEqual("Postpone...", focusedName)

                    board.Close()
                End Using
            End Sub)

    End Sub


    ' A screen reader's default action does what a click does: presses a
    ' button, runs an item's first action, opens the "⋯" menu, and shows or
    ' hides what was done. Each action saves once.
    <TestMethod>
    Public Sub DeadlinesDefaultActionsRunTheSameActions()

        RunOnStaThread(
            Sub()
                Using board As New DeadlineBoard(_root)
                    Dim library As Sample = Sample.Create()
                    board.Prepare(library.Manuscripts)
                    board.OpenDeadlines()
                    board.SetPrompts(Function(item) DateTime.Today.AddDays(12), Function(question) True)
                    Dim list As DeadlineList = board.List()

                    ' The third Postpone... in reading order is the follow-up's.
                    Child(list, "Postpone...", AccessibleRole.PushButton, 3).DoDefaultAction()
                    Application.DoEvents()
                    Assert.AreEqual(DateTime.Today.AddDays(12), library.FollowUp.FollowUpDate.Value, "The follow-up's Postpone... was pressed.")
                    Assert.AreEqual(1, board.SaveCount)

                    Child(list, RowName(list, "Send draft to coauthors"), AccessibleRole.ListItem).DoDefaultAction()
                    Application.DoEvents()
                    Assert.IsTrue(library.Today.IsCompleted, "The item's default action is its first action, Done.")
                    Assert.AreEqual(2, board.SaveCount)

                    Using menu As New MenuWatch(list.Menu)
                        Child(list, "More actions for Book the room", AccessibleRole.PushButton).DoDefaultAction()
                        Application.DoEvents()
                        CollectionAssert.AreEqual({"Done", "Postpone...", "Edit Reminder...", "Open Manuscript"}, menu.Opened,
                                                  "The ⋯ button's default action opens the menu. " & menu.Events)
                    End Using

                    Dim rows As Integer = list.Rows.Count
                    Child(list, "Show done items", AccessibleRole.Link).DoDefaultAction()
                    Application.DoEvents()
                    Assert.IsTrue(list.Rows.Count > rows, "The Done link shows what was done.")
                    Assert.IsNotNull(Child(list, "Hide done items", AccessibleRole.Link), "The link now hides them.")
                    Assert.AreEqual(2, board.SaveCount, "Opening the menu and showing done items save nothing.")

                    board.Close()
                End Using
            End Sub)

    End Sub


    ' ---------------------------------------------------------------
    ' The pointer
    ' ---------------------------------------------------------------

    ' A click on a drawn button runs its action once, even on a row the
    ' keyboard is not on; the "⋯" opens the menu; a double-click on a row
    ' runs its first action; a right-click chooses the row and opens its menu.
    <TestMethod>
    Public Sub DeadlinesMouseRunsTheRowsActions()

        RunOnStaThread(
            Sub()
                Using board As New DeadlineBoard(_root)
                    Dim library As Sample = Sample.Create()
                    board.Prepare(library.Manuscripts)
                    board.OpenDeadlines()
                    board.SetPrompts(Function(item) DateTime.Today.AddDays(9), Function(question) True)
                    Dim list As DeadlineList = board.List()

                    Dim gamma As DeadlineRow = list.Rows.Single(Function(row) row.Item.Kind = DeadlineKind.FollowUp)
                    Click(list, Center(list.ButtonBounds(gamma, 1)))
                    Assert.AreEqual(DateTime.Today.AddDays(9), library.FollowUp.FollowUpDate.Value, "A click on Postpone... postponed.")
                    Assert.AreEqual(1, board.SaveCount, "One click, one action.")

                    Dim beta As DeadlineRow = list.Rows.Single(Function(row) row.Item.Title = "Book the room")
                    Using menu As New MenuWatch(list.Menu)
                        Click(list, Center(list.ButtonBounds(beta, beta.MorePart)))
                        CollectionAssert.AreEqual(beta.ActionTexts.ToList(), menu.Opened, "The ⋯ button opens the menu. " & menu.Events)
                    End Using

                    ' The row's body: left of its text, clear of its buttons.
                    Dim body As Rectangle = list.ButtonBounds(beta, DeadlineRow.ItemPart)
                    Dim point As New Point(body.Left + 6, body.Top + body.Height \ 2)
                    Post(list, WM_LBUTTONDOWN, 1, Coordinates(point))
                    Post(list, WM_LBUTTONUP, 0, Coordinates(point))
                    Post(list, WM_LBUTTONDBLCLK, 1, Coordinates(point))
                    Post(list, WM_LBUTTONUP, 0, Coordinates(point))
                    Application.DoEvents()
                    Assert.IsTrue(library.Overdue.IsCompleted, "A double-click runs the row's first action, Done.")
                    Assert.AreEqual(2, board.SaveCount)

                    Dim delta As DeadlineRow = list.Rows.Single(Function(row) row.Item.Kind = DeadlineKind.Revision)
                    body = list.ButtonBounds(delta, DeadlineRow.ItemPart)
                    point = New Point(body.Left + 6, body.Top + body.Height \ 2)
                    Using menu As New MenuWatch(list.Menu)
                        Post(list, WM_RBUTTONDOWN, 2, Coordinates(point))
                        Application.DoEvents()
                        Assert.AreSame(delta, list.FocusedRow, "A right-click chooses the row.")
                        Post(list, WM_RBUTTONUP, 0, Coordinates(point))
                        Application.DoEvents()
                        CollectionAssert.AreEqual({"Set Deadline...", "Open"}, menu.Opened, "A right-click opens the row's menu. " & menu.Events)
                    End Using

                    ' The Done line's Show.
                    Dim rows As Integer = list.Rows.Count
                    Click(list, Center(list.DoneLinkBounds()))
                    Assert.IsTrue(list.Rows.Count > rows, "A click on Show shows what was done.")
                    Assert.AreEqual(2, board.SaveCount)

                    board.Close()
                End Using
            End Sub)

    End Sub


    ' A click near the edge of a button on a row the list's lower edge cuts
    ' off runs it once, whether the click focuses the list or moves the
    ' keyboard within it: the list scrolls only as far as the button needs,
    ' as focusing a button's own window did, so a button in view does not
    ' move between the press and the release.
    <TestMethod>
    Public Sub DeadlinesClickOnAPartlyHiddenRowIsNotLost()

        RunOnStaThread(
            Sub()
                Using board As New DeadlineBoard(_root)
                    Dim library As List(Of Manuscript) = ManyReminders(60)
                    board.Prepare(library)
                    board.OpenDeadlines()
                    board.SetPrompts(Function(item) DateTime.Today.AddDays(400), Function(question) True)
                    Dim list As DeadlineList = board.List()
                    Dim hidden As Integer = UiTheme.Px(10, list.DeviceDpi)
                    board.Filter().Focus()

                    For Each focused As Boolean In {False, True}
                        Dim label As String = If(focused, "With the list focused: ", "With the filter focused: ")
                        Assert.AreEqual(focused, list.Focused, label & "where the keyboard starts.")

                        ' The first row that reaches 10 pixels below the list's
                        ' edge, scrolled so that it does.
                        list.AutoScrollPosition = Point.Empty
                        Dim view As Integer = list.ClientSize.Height
                        Dim target As DeadlineRow = list.Rows.First(Function(row) list.ButtonBounds(row, DeadlineRow.ItemPart).Bottom > view + hidden)
                        Dim offset As Integer = list.ButtonBounds(target, DeadlineRow.ItemPart).Bottom - hidden - view
                        list.AutoScrollPosition = New Point(0, offset)
                        Application.DoEvents()
                        Assert.AreEqual(view + hidden, list.ButtonBounds(target, DeadlineRow.ItemPart).Bottom, label & "the row is cut off by 10 pixels.")
                        Dim postpone As Rectangle = list.ButtonBounds(target, 1)
                        Assert.AreEqual("Postpone...", target.Buttons(1).Text)
                        Assert.IsTrue(list.ClientRectangle.Contains(postpone), label & "its Postpone... is in view: " & postpone.ToString())

                        Dim saves As Integer = board.SaveCount
                        Dim reminder As ManuscriptReminder = library.SelectMany(Function(manuscript) manuscript.Reminders).Single(Function(candidate) candidate.Title = target.Item.Title)
                        Click(list, New Point(postpone.Left + postpone.Width \ 2, postpone.Bottom - 2))
                        Assert.AreEqual(saves + 1, board.SaveCount, label & "one click, one action.")
                        Assert.AreEqual(DateTime.Today.AddDays(400), reminder.DueDate.Date, label & "the click postponed the row it was on.")
                    Next

                    board.Close()
                End Using
            End Sub)

    End Sub


    ' ---------------------------------------------------------------
    ' Painting
    ' ---------------------------------------------------------------

    ' Every color is the theme's when the list is drawn: the overdue and
    ' today stripes, the focus border, the card and the kind badge, in
    ' Light, Dark and a contrast theme, where each is a system color. Rows
    ' are at least 64 pixels tall, taller with progress, 6 apart, at text
    ' sizes 1 and 1.5.
    <TestMethod>
    <DataRow(SystemColorMode.Classic, False)>
    <DataRow(SystemColorMode.Dark, False)>
    <DataRow(SystemColorMode.Classic, True)>
    Public Sub DeadlinesPaintFromTokensInEveryTheme(mode As SystemColorMode, contrast As Boolean)

        Dim failures As New List(Of String)()

        StaRunner.Run(
            Sub()
                UiTheme.HighContrastOverride = contrast
                Try
                    For Each scale As Single In {1.0F, 1.5F}
                        Using host As New Form With {.StartPosition = FormStartPosition.Manual, .Location = New Point(-20000, -20000), .ShowInTaskbar = False,
                                                      .ClientSize = New Size(900, 700), .Font = New Font(SystemFonts.MessageBoxFont.FontFamily, 9.0F * scale)}
                            Dim list As New DeadlineList With {.Dock = DockStyle.Fill, .AccessibleName = "Deadlines"}
                            host.Controls.Add(list)
                            Dim today As DateTime = DateTime.Today
                            Dim overdue As DeadlineRow = DrawnRow(New DeadlineItem With {.Kind = DeadlineKind.Revision, .Group = DeadlineGroup.Overdue, .Title = "Revision due", .ManuscriptTitle = "Alpha", .DueDate = today.AddDays(-2), .ProgressDone = 1, .ProgressTotal = 2}, today)
                            Dim dueToday As DeadlineRow = DrawnRow(New DeadlineItem With {.Kind = DeadlineKind.Reminder, .Group = DeadlineGroup.Today, .Title = "Send draft", .ManuscriptTitle = "Beta", .DueDate = today}, today)
                            Dim later As DeadlineRow = DrawnRow(New DeadlineItem With {.Kind = DeadlineKind.FollowUp, .Group = DeadlineGroup.Later, .Title = "Follow up", .ManuscriptTitle = "Gamma", .DueDate = today.AddDays(20)}, today)
                            list.SetEntries({DeadlineEntry.Heading("OVERDUE  1", DeadlineGroup.Overdue), DeadlineEntry.ForRow(overdue), DeadlineEntry.ForRow(dueToday), DeadlineEntry.ForRow(later)})
                            host.Show()
                            Application.DoEvents()
                            Assert.IsTrue(list.Focus())
                            Application.DoEvents()
                            Dim label As String = mode.ToString() & If(contrast, " contrast", String.Empty) & " at " & scale.ToString() & ": "

                            Using picture As New Bitmap(list.Width, list.Height)
                                list.DrawToBitmap(picture, New Rectangle(Point.Empty, list.Size))
                                Dim tokens As New List(Of (Name As String, Expected As Color, At As Point)) From {
                                    ("the board", UiTheme.BoardBackground(), New Point(2, 2)),
                                    ("the overdue stripe", UiTheme.DangerColor(), StripeAt(list, overdue)),
                                    ("the today stripe", UiTheme.WarningColor(), StripeAt(list, dueToday)),
                                    ("the focus border", UiTheme.AccentColor(), TopEdgeAt(list, overdue)),
                                    ("the card", UiTheme.CardBackground(), CardAt(list, later)),
                                    ("the reminder badge", UiTheme.StageBackground(PaperStage.Draft), BadgeAt(list, dueToday)),
                                    ("the follow-up badge", UiTheme.StageBackground(PaperStage.Submitted), BadgeAt(list, later))
                                }
                                For Each token In tokens
                                    Dim actual As Color = picture.GetPixel(token.At.X, token.At.Y)
                                    If actual.ToArgb() <> token.Expected.ToArgb() Then failures.Add(label & token.Name & " is " & actual.ToString() & " at " & token.At.ToString() & ", not " & token.Expected.ToString())
                                    If contrast AndAlso Not token.Expected.IsSystemColor Then failures.Add(label & token.Name & " is not a system color under a contrast theme.")
                                Next
                            End Using

                            Dim rows As List(Of DeadlineEntry) = list.Entries.Where(Function(entry) entry.Kind = DeadlineEntryKind.Row).ToList()
                            Dim minimum As Integer = UiTheme.Px(64, list.DeviceDpi)
                            If rows.Any(Function(entry) entry.Height < minimum) Then failures.Add(label & "a row is shorter than 64 pixels.")
                            If rows(0).Height <= rows(1).Height Then failures.Add(label & "a row with progress is no taller than one without.")
                            If rows(1).Top - (rows(0).Top + rows(0).Height) <> UiTheme.Px(6, list.DeviceDpi) Then failures.Add(label & "rows are not 6 pixels apart.")

                            host.Close()
                        End Using
                    Next
                Finally
                    UiTheme.HighContrastOverride = Nothing
                End Try
            End Sub, mode:=mode)

        Assert.AreEqual(0, failures.Count, String.Join(Environment.NewLine, failures))

    End Sub


    ' Under a Windows contrast theme the lists that draw their own items,
    ' buttons, headings and links use the user's colors, as what Windows
    ' draws does (#138): drawn with the seam on, in Classic and in Dark, the
    ' Deadlines list, its counts and Import & Export's list show no patch of
    ' a color of the Light or Dark palette; their surfaces are the window
    ' color; every button they draw or hold, the quiet "⋯" too, shows its
    ' edge; and the keyboard's place shows on a row, on each of its buttons
    ' and on the Done line's link, in the user's selection or link color.
    <TestMethod>
    <DataRow(SystemColorMode.Classic)>
    <DataRow(SystemColorMode.Dark)>
    Public Sub DrawnListsUseOnlyTheUsersColorsUnderAContrastTheme(mode As SystemColorMode)

        Dim failures As New List(Of String)()

        StaRunner.Run(
            Sub()
                Try
                    ' The palette's own colors in this mode: its tokens that
                    ' are not one of the user's colors once the theme is on.
                    UiTheme.HighContrastOverride = False
                    Dim palette As HashSet(Of Integer) = TokenColors(systemToo:=False)
                    UiTheme.HighContrastOverride = True
                    palette.ExceptWith(TokenColors(systemToo:=True))
                    Dim window As Color = SystemColors.Window

                    Using host As New Form With {.StartPosition = FormStartPosition.Manual, .Location = New Point(-20000, -20000), .ShowInTaskbar = False,
                                                  .ClientSize = New Size(900, 780), .BackColor = UiTheme.BoardBackground()}
                        Dim today As DateTime = DateTime.Today
                        Dim overdue As DeadlineRow = DrawnRow(New DeadlineItem With {.Kind = DeadlineKind.Revision, .Group = DeadlineGroup.Overdue, .Title = "Revision due", .ManuscriptTitle = "Alpha", .JournalName = "Fictional Journal", .DueDate = today.AddDays(-2), .ProgressDone = 1, .ProgressActive = 1, .ProgressTotal = 3}, today)
                        Dim dueToday As DeadlineRow = DrawnRow(New DeadlineItem With {.Kind = DeadlineKind.Reminder, .Group = DeadlineGroup.Today, .Title = "Send draft", .ManuscriptTitle = "Beta", .DueDate = today}, today)
                        Dim undated As DeadlineRow = DrawnRow(New DeadlineItem With {.Kind = DeadlineKind.Revision, .Group = DeadlineGroup.NoDate, .Title = "Revision has no deadline", .ManuscriptTitle = "Gamma"}, today)
                        Dim list As New DeadlineList With {.Bounds = New Rectangle(0, 0, 900, 470), .AccessibleName = "Deadlines"}
                        list.SetEntries({
                            DeadlineEntry.Heading("OVERDUE  1", DeadlineGroup.Overdue), DeadlineEntry.ForRow(overdue),
                            DeadlineEntry.Heading("TODAY  1", DeadlineGroup.Today), DeadlineEntry.ForRow(dueToday),
                            DeadlineEntry.Heading("NO DATE  1", DeadlineGroup.NoDate), DeadlineEntry.ForRow(undated),
                            DeadlineEntry.Message("Nothing matches this filter."),
                            DeadlineEntry.DoneToggle("Done in the last 30 days: 1  ·  Show", "Show", "Show done items")
                        })
                        Dim glance As New DeadlineGlance With {.AutoSize = False, .Bounds = New Rectangle(0, 480, 900, 110), .AccessibleName = "At a glance"}
                        glance.SetCounts(1, 1, 0, 1)
                        Dim commands As New CommandList With {.Bounds = New Rectangle(0, 600, 900, 170), .ButtonWidth = UiTheme.Px(180, host.DeviceDpi)}
                        commands.AddSection("BRING WORK IN")
                        Dim importButton As New ActionButton With {.Text = "Import Spreadsheet...", .UseMnemonic = False, .Width = UiTheme.Px(180, host.DeviceDpi), .Height = UiTheme.Px(34, host.DeviceDpi)}
                        commands.AddCommand(importButton, "A PaperRoute template, a legacy tracker, or any workbook through column mapping.")
                        host.Controls.AddRange(New Control() {list, glance, commands})
                        host.Show()
                        Application.DoEvents()

                        ' The list as tall as what it draws, at any text size,
                        ' so every row is in its picture; the others below it.
                        list.Height = list.AutoScrollMinSize.Height
                        glance.Top = list.Bottom + 10
                        commands.Top = glance.Bottom + 10
                        host.ClientSize = New Size(host.ClientSize.Width, commands.Bottom)
                        Application.DoEvents()
                        Assert.AreEqual(0, list.AutoScrollPosition.Y, "The list does not scroll.")

                        Dim label As String = mode.ToString() & ": "
                        Dim judge As Action(Of String, Control) =
                            Sub(what, drawn)
                                Using picture As Bitmap = Snapshot(drawn)
                                    For Each patch In PaletteColorsIn(picture, palette)
                                        failures.Add(label & what & " has " & patch.Count & " pixels of the palette's " & Color.FromArgb(patch.Argb).ToString() & ".")
                                    Next
                                End Using
                            End Sub

                        ' The surfaces, and every drawn button's edge.
                        If list.BackColor.ToArgb() <> window.ToArgb() OrElse Not list.BackColor.IsSystemColor Then failures.Add(label & "the list's surface is " & list.BackColor.ToString() & ".")
                        If commands.BackColor.ToArgb() <> window.ToArgb() OrElse Not commands.BackColor.IsSystemColor Then failures.Add(label & "Import & Export's surface is " & commands.BackColor.ToString() & ".")
                        judge("the list", list)
                        judge("the counts", glance)
                        judge("Import & Export's list", commands)
                        Using picture As Bitmap = Snapshot(list)
                            If picture.GetPixel(CardAt(list, undated).X, CardAt(list, undated).Y).ToArgb() <> window.ToArgb() Then failures.Add(label & "a row's card is not the window color.")
                            For Each row As DeadlineRow In {overdue, dueToday, undated}
                                For part As Integer = 0 To row.MorePart
                                    Dim edge As Double = EdgeContrast(picture, list.ButtonBounds(row, part), window)
                                    If edge < 3.0 Then failures.Add(label & row.Item.Title & "'s " & If(part = row.MorePart, "⋯", row.Buttons(part).Text) & " shows its edge at " & edge.ToString("F2") & ":1.")
                                Next
                            Next
                        End Using
                        Using picture As Bitmap = Snapshot(glance)
                            Dim tiles As AccessibleObject = glance.AccessibilityObject
                            For index As Integer = 0 To tiles.GetChildCount() - 1
                                Dim edge As Double = EdgeContrast(picture, glance.RectangleToClient(tiles.GetChild(index).Bounds), window)
                                If edge < 3.0 Then failures.Add(label & "count tile " & (index + 1) & " shows its edge at " & edge.ToString("F2") & ":1.")
                            Next
                        End Using
                        Using picture As Bitmap = Snapshot(commands)
                            Dim edge As Double = EdgeContrast(picture, importButton.Bounds, window)
                            If edge < 3.0 Then failures.Add(label & "Import Spreadsheet... shows its edge at " & edge.ToString("F2") & ":1.")
                        End Using

                        ' The keyboard on the first row, then on each of its
                        ' buttons, then on the Done line's link.
                        Dim dialogKey As MethodInfo = GetType(DeadlineList).GetMethod("ProcessDialogKey", BindingFlags.Instance Or BindingFlags.NonPublic)
                        Assert.IsTrue(list.Focus())
                        ShowFocusCues(list)
                        Assert.AreSame(overdue, list.FocusedRow)
                        Using picture As Bitmap = Snapshot(list)
                            Dim top As Point = TopEdgeAt(list, overdue)
                            If picture.GetPixel(top.X, top.Y).ToArgb() <> UiTheme.AccentColor().ToArgb() Then failures.Add(label & "the focused row's border is " & picture.GetPixel(top.X, top.Y).ToString() & ", not the selection color.")
                        End Using
                        For part As Integer = 0 To overdue.MorePart
                            dialogKey.Invoke(list, New Object() {Keys.Tab})
                            Application.DoEvents()
                            Dim bounds As Rectangle = list.ButtonBounds(overdue, part)
                            Using picture As Bitmap = Snapshot(list)
                                Dim ring As Integer = Enumerable.Range(bounds.Top, 4).Count(Function(y) picture.GetPixel(bounds.Left + bounds.Width \ 2, y).ToArgb() = UiTheme.AccentSecondaryColor().ToArgb())
                                If ring = 0 Then failures.Add(label & "the focus ring on " & If(part = overdue.MorePart, "⋯", overdue.Buttons(part).Text) & " is not drawn in the link color.")
                            End Using
                        Next
                        For tries As Integer = 0 To list.AccessibilityObject.GetChildCount()
                            If list.DoneLineFocused OrElse Not list.Focused Then Exit For
                            dialogKey.Invoke(list, New Object() {Keys.Tab})
                        Next
                        Application.DoEvents()
                        Assert.IsTrue(list.DoneLineFocused, "Tab reaches the Done line.")
                        Using picture As Bitmap = Snapshot(list)
                            Dim link As Rectangle = list.DoneLinkBounds()
                            Dim dots As Integer = Enumerable.Range(link.Top, link.Height).Count(Function(y) picture.GetPixel(link.Left - 1, y).ToArgb() = UiTheme.AccentColor().ToArgb())
                            If dots < link.Height \ 3 Then failures.Add(label & "the Done link's focus rectangle has " & dots & " of " & link.Height & " pixels in the selection color.")
                        End Using
                        judge("the list with the keyboard on the Done line", list)

                        Assert.IsTrue(importButton.Focus())
                        ShowFocusCues(importButton)
                        Using picture As Bitmap = Snapshot(commands)
                            Dim command As Rectangle = importButton.Bounds
                            Dim ring As Integer = Enumerable.Range(command.Top, 4).Count(Function(y) picture.GetPixel(command.Left + command.Width \ 2, y).ToArgb() = UiTheme.AccentSecondaryColor().ToArgb())
                            If ring = 0 Then failures.Add(label & "the focus ring on Import Spreadsheet... is not drawn in the link color.")
                        End Using

                        host.Close()
                    End Using
                Finally
                    UiTheme.HighContrastOverride = Nothing
                End Try
            End Sub, mode:=mode)

        Assert.AreEqual(0, failures.Count, String.Join(Environment.NewLine, failures))

    End Sub


    ' ---------------------------------------------------------------
    ' The page
    ' ---------------------------------------------------------------

    ' The page is built once: each visit shows the same page, with no filter
    ' text, the list at its top, and today's date even after midnight. Its
    ' list is made at its final size and is not laid out again by a
    ' keystroke. Nothing on the page saves: not a visit, a keystroke, a
    ' chip, the Done line, a leave or a return.
    <TestMethod>
    Public Sub DeadlinesPageIsKeptAndFreshOnEachVisit()

        RunOnStaThread(
            Sub()
                Using board As New DeadlineBoard(_root)
                    Dim library As List(Of Manuscript) = ManyReminders(40)
                    library(0).Reminders.Add(New ManuscriptReminder With {.Title = "Done lately", .DueDate = DateTime.Today.AddDays(-2), .IsCompleted = True, .CompletedDate = DateTime.Today.AddDays(-1)})
                    board.Prepare(library)

                    Dim created As Rectangle = Rectangle.Empty
                    Dim layouts As Integer = 0
                    Dim trail As String = String.Empty
                    Dim host As Control = board.ContentHost()
                    AddHandler host.ControlAdded,
                        Sub(sender, e)
                            Dim added As DeadlineList = ViewCatalog.Descendants(e.Control).OfType(Of DeadlineList)().SingleOrDefault()
                            If added Is Nothing Then Return
                            Assert.IsFalse(added.IsHandleCreated, "The page is put in the window before its windows are made.")
                            AddHandler added.HandleCreated, Sub(s, a) created = added.Bounds
                            AddHandler added.Layout,
                                Sub(s, a)
                                    layouts += 1
                                    trail &= a.AffectedProperty & "/" & added.IsHandleCreated & "/" & added.ClientSize.ToString() & " "
                                End Sub
                        End Sub

                    board.OpenDeadlines()
                    Dim page As Control = board.Page()
                    Dim list As DeadlineList = board.List()
                    Assert.AreEqual(list.Bounds, created, "The list's window is made at its final size.")
                    ' The list has no child controls, so laying it out only sets
                    ' its scroll range; it happens as the page takes its size and
                    ' again as the scroll bar appears with the window.
                    TestContext.WriteLine("First visit, the list's layouts: " & trail)

                    layouts = 0
                    board.Filter().Text = "Study"
                    Application.DoEvents()
                    Assert.AreEqual(0, layouts, "A keystroke that leaves the same rows does not lay the list out.")
                    Assert.AreEqual(40, list.Rows.Count)

                    ClickChip(board, "Reminders")
                    board.ClickDoneLine()
                    list.AutoScrollPosition = New Point(0, 500)
                    Assert.IsTrue(list.AutoScrollPosition.Y < 0)

                    board.NavigateTo(Form1.WorkspacePage.Board)
                    Assert.IsTrue(page.IsHandleCreated AndAlso Not page.IsDisposed, "Leaving keeps the page.")
                    Assert.IsFalse(page.Visible)

                    ' As if midnight had passed since the page was built.
                    board.SetPageDate(DateTime.Today.AddDays(-1))
                    board.NavigateTo(Form1.WorkspacePage.Deadlines)
                    Assert.AreSame(page, board.Page(), "The same page on the next visit.")
                    Assert.AreSame(list, board.List())
                    Assert.IsTrue(page.Visible)
                    Assert.AreEqual(String.Empty, board.Filter().Text, "The filter starts empty, as on a new page.")
                    Assert.AreEqual(0, list.AutoScrollPosition.Y, "The list starts at its top.")
                    Assert.IsNull(list.FocusedRow)
                    StringAssert.StartsWith(board.Subtitle(), DateTime.Today.ToString("dddd, MMMM d, yyyy", Globalization.CultureInfo.CurrentCulture))

                    Assert.AreEqual(0, board.SaveCount, "Nothing on the page saved.")

                    ' Kept in the window, the page goes with it, shown or hidden.
                    board.NavigateTo(Form1.WorkspacePage.Board)
                    board.Close()
                    Assert.IsTrue(page.IsDisposed, "Closing the window disposes the kept page.")
                End Using
            End Sub)

    End Sub


    ' A Windows contrast theme turned on while PaperRoute runs, through the
    ' HighContrastOverride seam: the kept pages were built on the old
    ' surface, so the hidden one is let go at once and the one showing as
    ' it is left. Each next visit builds its page anew, on the window
    ' color, and that page is kept again. Nothing saves.
    <TestMethod>
    Public Sub KeptPagesAreBuiltAnewAfterTheSystemColorsChange()

        RunOnStaThread(
            Sub()
                Try
                    UiTheme.HighContrastOverride = False
                    Using board As New DeadlineBoard(_root)
                        board.Prepare(ManyReminders(3))
                        board.OpenDeadlines()
                        Dim oldDeadlines As Control = board.Page()
                        Dim oldSurface As Integer = board.List().BackColor.ToArgb()
                        board.NavigateTo(Form1.WorkspacePage.ImportExport)
                        Application.DoEvents()
                        Dim oldImportExport As Control = board.ImportExportPage()
                        Assert.IsFalse(oldDeadlines.IsDisposed, "Leaving keeps the page.")
                        Assert.AreNotEqual(SystemColors.Window.ToArgb(), oldSurface, "The first list is on the board color.")

                        UiTheme.HighContrastOverride = True
                        board.ChangeSystemColors()
                        Assert.IsTrue(oldDeadlines.IsDisposed, "The hidden kept page is let go.")
                        Assert.IsFalse(board.ContentHost().Controls.Contains(oldDeadlines))
                        Assert.IsFalse(oldImportExport.IsDisposed, "The page showing stays while it shows.")
                        Assert.IsTrue(oldImportExport.Visible)

                        board.OpenDeadlines()
                        Assert.IsTrue(oldImportExport.IsDisposed, "The page that was showing is let go as it is left.")
                        Dim newDeadlines As Control = board.Page()
                        Assert.AreNotSame(oldDeadlines, newDeadlines, "The next visit builds the page anew.")
                        Assert.IsTrue(newDeadlines.Visible)
                        Assert.AreEqual(3, board.List().Rows.Count)
                        Assert.AreEqual(SystemColors.Window.ToArgb(), board.List().BackColor.ToArgb(), "The new list is on the window color.")

                        board.NavigateTo(Form1.WorkspacePage.ImportExport)
                        Application.DoEvents()
                        Dim newImportExport As Control = board.ImportExportPage()
                        Assert.AreNotSame(oldImportExport, newImportExport, "The next visit builds Import & Export anew.")
                        Assert.AreEqual(SystemColors.Window.ToArgb(), ViewCatalog.Descendants(newImportExport).OfType(Of CommandList)().Single().BackColor.ToArgb(),
                                        "The new Import & Export list is on the window color.")

                        ' Built in the new colors, both are kept again.
                        Assert.IsFalse(newDeadlines.IsDisposed, "Leaving keeps the new page.")
                        board.OpenDeadlines()
                        Assert.AreSame(newDeadlines, board.Page())
                        Assert.IsFalse(newImportExport.IsDisposed, "Leaving keeps the new Import & Export page.")
                        board.NavigateTo(Form1.WorkspacePage.ImportExport)
                        Application.DoEvents()
                        Assert.AreSame(newImportExport, board.ImportExportPage())

                        Assert.AreEqual(0, board.SaveCount, "Nothing saved.")
                        board.Close()
                    End Using
                Finally
                    UiTheme.HighContrastOverride = Nothing
                End Try
            End Sub)

    End Sub


    ' The page makes the same few windows however long the list is.
    <TestMethod>
    Public Sub DeadlinesPageHasAFixedWindowCount()

        Dim counts As New List(Of Integer)()

        RunOnStaThread(
            Sub()
                For Each size As Integer In {3, 60}
                    Using board As New DeadlineBoard(_root)
                        board.Prepare(ManyReminders(size))
                        board.OpenDeadlines()
                        Assert.AreEqual(size, board.List().Rows.Count)
                        Dim page As Control = board.Page()
                        counts.Add(1 + ViewCatalog.Descendants(page).Count(Function(control) control.IsHandleCreated))
                        board.Close()
                    End Using
                Next
            End Sub)

        Assert.AreEqual(counts(0), counts(1), "Windows for 3 and 60 rows: " & String.Join(", ", counts))
        Assert.IsTrue(counts(1) <= 17, "The page makes " & counts(1) & " windows.")

    End Sub


    ' The counts across the top ignore the filters.
    <TestMethod>
    Public Sub DeadlineGlanceCountsIgnoreTheFilters()

        RunOnStaThread(
            Sub()
                Using board As New DeadlineBoard(_root)
                    board.Prepare(Sample.Create().Manuscripts)
                    board.OpenDeadlines()
                    Dim glance As DeadlineGlance = ViewCatalog.Descendants(board).OfType(Of DeadlineGlance)().Single()
                    Dim before As List(Of Integer) = glance.Counts.ToList()
                    CollectionAssert.AreEqual({1, 1, 1, 1}, before)

                    ClickChip(board, "Follow-ups")
                    CollectionAssert.AreEqual(before, glance.Counts.ToList(), "A chip leaves the counts.")
                    board.Filter().Text = "Gamma"
                    Application.DoEvents()
                    CollectionAssert.AreEqual(before, glance.Counts.ToList(), "Filter text leaves the counts.")
                    Assert.AreEqual(1, board.List().Rows.Count)

                    Using picture As New Bitmap(glance.Width, glance.Height)
                        glance.DrawToBitmap(picture, New Rectangle(Point.Empty, glance.Size))
                        Assert.AreEqual(UiTheme.CardBackground().ToArgb(), picture.GetPixel(UiTheme.Px(6, glance.DeviceDpi), UiTheme.Px(4, glance.DeviceDpi)).ToArgb(), "A tile is a card.")
                    End Using

                    board.Close()
                End Using
            End Sub)

    End Sub


    ' ---------------------------------------------------------------
    ' Helpers
    ' ---------------------------------------------------------------

    ' Four manuscripts: a reminder overdue, one due today, a follow-up in
    ' three days, a revision without a deadline, and a reminder done
    ' yesterday.
    Private NotInheritable Class Sample

        Public Manuscripts As List(Of Manuscript)
        Public Overdue As ManuscriptReminder
        Public Today As ManuscriptReminder
        Public FollowUp As JournalSubmission

        Public Shared Function Create() As Sample

            Dim today As DateTime = DateTime.Today
            Dim alpha As Manuscript = Draft("Alpha study")
            Dim beta As Manuscript = Draft("Beta study")
            Dim gamma As Manuscript = Draft("Gamma study")
            Dim delta As Manuscript = Draft("Delta study")

            Dim result As New Sample()
            result.Today = New ManuscriptReminder With {.Title = "Send draft to coauthors", .DueDate = today}
            alpha.Reminders.Add(result.Today)
            alpha.Reminders.Add(New ManuscriptReminder With {.Title = "Upload preregistration", .DueDate = today.AddDays(-4), .IsCompleted = True, .CompletedDate = today.AddDays(-1)})
            result.Overdue = New ManuscriptReminder With {.Title = "Book the room", .DueDate = today.AddDays(-3)}
            beta.Reminders.Add(result.Overdue)

            gamma.CurrentStage = PaperStage.Submitted
            result.FollowUp = New JournalSubmission With {.JournalName = "Assessment", .SubmittedDate = today.AddDays(-30), .FollowUpDate = today.AddDays(3)}
            gamma.Submissions.Add(result.FollowUp)

            delta.CurrentStage = PaperStage.Revision
            Dim submission As New JournalSubmission With {.JournalName = "Second Journal", .SubmittedDate = today.AddDays(-90)}
            submission.Decisions.Add(New EditorialDecisionEvent With {.Decision = EditorialDecision.MajorRevision, .DecisionDate = today.AddDays(-20)})
            delta.Submissions.Add(submission)

            result.Manuscripts = New List(Of Manuscript) From {alpha, beta, gamma, delta}
            Return result

        End Function

    End Class

    Private Shared Function Draft(title As String) As Manuscript
        Return New Manuscript With {
            .Title = title,
            .Location = ManuscriptLocation.Pipeline,
            .CurrentStage = PaperStage.Draft,
            .TargetJournal = "A fictional journal"
        }
    End Function

    ' A manuscript each with a reminder, a day apart from tomorrow on.
    Private Shared Function ManyReminders(count As Integer) As List(Of Manuscript)
        Dim library As New List(Of Manuscript)()
        For index As Integer = 1 To count
            Dim manuscript As Manuscript = Draft("Study " & index.ToString("000", Globalization.CultureInfo.InvariantCulture))
            manuscript.Reminders.Add(New ManuscriptReminder With {.Title = "Reminder " & index.ToString(Globalization.CultureInfo.InvariantCulture), .DueDate = DateTime.Today.AddDays(index)})
            library.Add(manuscript)
        Next
        Return library
    End Function

    Private Shared Function DrawnRow(item As DeadlineItem, today As DateTime) As DeadlineRow
        Dim row As New DeadlineRow(item, today)
        row.AddAction("Open", Sub()
                              End Sub)
        row.AddAction("Postpone...", Sub()
                                     End Sub)
        row.AddAction("Edit...", Sub()
                                 End Sub, menuOnly:=True)
        row.FinishActions()
        Return row
    End Function

    Private Shared Function RowName(list As DeadlineList, title As String) As String
        Return list.Rows.Single(Function(row) row.Item.Title = title).AccessibleName
    End Function

    Private Shared Sub AssertAt(list As DeadlineList, title As String, part As Integer)
        Assert.IsNotNull(list.FocusedRow, "The keyboard is on a row; expected " & title)
        Assert.AreEqual(title, list.FocusedRow.Item.Title)
        Assert.AreEqual(part, list.FocusedPart, "On " & title)
    End Sub

    ' Puts the keyboard on a row, as Up and Down would.
    Private Shared Sub FocusRow(board As DeadlineBoard, list As DeadlineList, title As String)
        If Not list.Focused Then Assert.IsTrue(list.Focus())
        While list.FocusedPart <> DeadlineRow.ItemPart
            ShiftTab(list)
        End While
        Dim rows As List(Of DeadlineRow) = list.Rows.ToList()
        Dim target As Integer = rows.FindIndex(Function(row) row.Item.Title = title)
        Assert.IsTrue(target >= 0, title & " is listed.")
        While rows.IndexOf(list.FocusedRow) <> target
            Press(board, If(rows.IndexOf(list.FocusedRow) < target, Keys.Down, Keys.Up))
        End While
    End Sub

    Private Shared Function Child(list As DeadlineList, name As String, role As AccessibleRole, Optional occurrence As Integer = 1) As AccessibleObject
        Dim root As AccessibleObject = list.AccessibilityObject
        Return Enumerable.Range(0, root.GetChildCount()).Select(Function(index) root.GetChild(index)).
            Where(Function(candidate) candidate.Role = role AndAlso candidate.Name = name).
            Skip(occurrence - 1).FirstOrDefault()
    End Function

    Private Shared Sub ClickChip(board As DeadlineBoard, name As String)
        Dim chip As FilterChip = ViewCatalog.Descendants(board).OfType(Of FilterChip)().Single(Function(candidate) candidate.Text.StartsWith(name, StringComparison.Ordinal))
        GetType(Control).GetMethod("OnClick", BindingFlags.Instance Or BindingFlags.NonPublic).Invoke(chip, New Object() {EventArgs.Empty})
        Application.DoEvents()
    End Sub

    ' The key, posted to the focused control as Windows delivers it.
    Private Shared Sub Press(board As Form, key As Keys)
        Dim target As Control = board.ActiveControl
        While TypeOf target Is ContainerControl AndAlso DirectCast(target, ContainerControl).ActiveControl IsNot Nothing
            target = DirectCast(target, ContainerControl).ActiveControl
        End While
        Post(If(target, board), WM_KEYDOWN, CInt(key), 1)
        Application.DoEvents()
    End Sub

    ' Shift+Tab, which a posted key cannot carry: the dialog key, as the
    ' focused control receives it.
    Private Shared Sub ShiftTab(list As DeadlineList)
        GetType(DeadlineList).GetMethod("ProcessDialogKey", BindingFlags.Instance Or BindingFlags.NonPublic).
            Invoke(list, New Object() {Keys.Shift Or Keys.Tab})
        Application.DoEvents()
    End Sub

    Private Shared Sub Click(list As DeadlineList, point As Point)
        Post(list, WM_LBUTTONDOWN, 1, Coordinates(point))
        Post(list, WM_LBUTTONUP, 0, Coordinates(point))
        Application.DoEvents()
    End Sub

    Private Shared Function Center(bounds As Rectangle) As Point
        Return New Point(bounds.Left + bounds.Width \ 2, bounds.Top + bounds.Height \ 2)
    End Function

    Private Shared Function Coordinates(point As Point) As Integer
        Return (point.Y << 16) Or (point.X And &HFFFF)
    End Function

    Private Shared Sub Post(control As Control, message As Integer, wParam As Integer, lParam As Integer)
        PostMessage(control.Handle, message, New IntPtr(wParam), New IntPtr(lParam))
    End Sub

    ' Where the painting test samples: the stripe inside the left edge, the
    ' top edge's border, the card clear of text, and a corner of the badge.
    Private Shared Function StripeAt(list As DeadlineList, row As DeadlineRow) As Point
        Dim bounds As Rectangle = list.ButtonBounds(row, DeadlineRow.ItemPart)
        Return New Point(bounds.Left + 2, bounds.Top + bounds.Height \ 2)
    End Function

    Private Shared Function TopEdgeAt(list As DeadlineList, row As DeadlineRow) As Point
        Dim bounds As Rectangle = list.ButtonBounds(row, DeadlineRow.ItemPart)
        Return New Point(bounds.Left + bounds.Width \ 2, bounds.Top)
    End Function

    Private Shared Function CardAt(list As DeadlineList, row As DeadlineRow) As Point
        Dim bounds As Rectangle = list.ButtonBounds(row, DeadlineRow.ItemPart)
        Return New Point(bounds.Left + UiTheme.Px(6, list.DeviceDpi), bounds.Top + UiTheme.Px(4, list.DeviceDpi))
    End Function

    Private Shared Function BadgeAt(list As DeadlineList, row As DeadlineRow) As Point
        Dim dpi As Integer = list.DeviceDpi
        Dim bounds As Rectangle = list.ButtonBounds(row, DeadlineRow.ItemPart)
        Dim left As Integer = bounds.Left + UiTheme.Px(12, dpi) + UiTheme.Px(62, dpi) + UiTheme.Px(8, dpi)
        Dim top As Integer = bounds.Top + bounds.Height \ 2 - UiTheme.Px(28, dpi) \ 2
        Return New Point(left + UiTheme.Px(4, dpi), top + UiTheme.Px(4, dpi))
    End Function

    Private Shared Function Snapshot(control As Control) As Bitmap
        Dim picture As New Bitmap(control.Width, control.Height)
        control.DrawToBitmap(picture, New Rectangle(Point.Empty, control.Size))
        Return picture
    End Function

    ' Every token's color, a stage's badge and pill included, as the theme
    ' gives them now; the system colors among them only when asked for.
    Private Shared Function TokenColors(systemToo As Boolean) As HashSet(Of Integer)
        Dim colors As New List(Of Color)()
        For Each token As MethodInfo In GetType(UiTheme).GetMethods(BindingFlags.Public Or BindingFlags.Static).
            Where(Function(method) method.ReturnType = GetType(Color) AndAlso method.GetParameters().Length = 0)
            colors.Add(DirectCast(token.Invoke(Nothing, Nothing), Color))
        Next
        For Each stage As PaperStage In [Enum].GetValues(GetType(PaperStage))
            colors.Add(UiTheme.StageBackground(stage))
            colors.Add(UiTheme.StageForeground(stage))
        Next
        Return New HashSet(Of Integer)(colors.Where(Function(color) systemToo OrElse Not color.IsSystemColor).Select(Function(color) color.ToArgb()))
    End Function

    ' The palette's colors in a picture that cover at least four pixels
    ' each: smoothing makes single pixels of every shade between two
    ' colors, and one of them may match by chance; a fill, a line or a
    ' letter in a palette color makes many.
    Private Shared Function PaletteColorsIn(picture As Bitmap, palette As HashSet(Of Integer)) As List(Of (Argb As Integer, Count As Integer))
        Dim area As New Rectangle(Point.Empty, picture.Size)
        Dim data As Imaging.BitmapData = picture.LockBits(area, Imaging.ImageLockMode.ReadOnly, Imaging.PixelFormat.Format32bppArgb)
        Dim pixels(area.Width * area.Height - 1) As Integer
        Try
            Marshal.Copy(data.Scan0, pixels, 0, pixels.Length)
        Finally
            picture.UnlockBits(data)
        End Try
        Return pixels.Where(Function(pixel) palette.Contains(pixel)).
            GroupBy(Function(pixel) pixel).
            Where(Function(same) same.Count() >= 4).
            Select(Function(same) (Argb:=same.Key, Count:=same.Count())).
            ToList()
    End Function

    ' How far a drawn shape's edge stands out from the surface: at the
    ' middle of each side, the stronger of its two outermost pixels; the
    ' weakest of the four sides.
    Private Shared Function EdgeContrast(picture As Bitmap, bounds As Rectangle, surface As Color) As Double
        Dim middleX As Integer = bounds.Left + bounds.Width \ 2
        Dim middleY As Integer = bounds.Top + bounds.Height \ 2
        Dim sides As Point()() = {
            New Point() {New Point(middleX, bounds.Top), New Point(middleX, bounds.Top + 1)},
            New Point() {New Point(middleX, bounds.Bottom - 1), New Point(middleX, bounds.Bottom - 2)},
            New Point() {New Point(bounds.Left, middleY), New Point(bounds.Left + 1, middleY)},
            New Point() {New Point(bounds.Right - 1, middleY), New Point(bounds.Right - 2, middleY)}
        }
        Return sides.Min(Function(side) side.Max(Function(spot) ContrastRatio(picture.GetPixel(spot.X, spot.Y), surface)))
    End Function

    Private Shared Function ContrastRatio(foreground As Color, background As Color) As Double
        Dim first As Double = Luminance(foreground)
        Dim second As Double = Luminance(background)
        Return (Math.Max(first, second) + 0.05) / (Math.Min(first, second) + 0.05)
    End Function

    Private Shared Function Luminance(color As Color) As Double
        Return 0.2126 * LinearChannel(color.R) + 0.7152 * LinearChannel(color.G) + 0.0722 * LinearChannel(color.B)
    End Function

    Private Shared Function LinearChannel(channel As Byte) As Double
        Dim value As Double = channel / 255.0
        Return If(value <= 0.04045, value / 12.92, Math.Pow((value + 0.055) / 1.055, 2.4))
    End Function

    Private Shared Function FirstDifference(expected As Bitmap, actual As Bitmap) As String
        If expected.Size <> actual.Size Then Return "sizes differ"
        For y As Integer = 0 To expected.Height - 1
            For x As Integer = 0 To expected.Width - 1
                If expected.GetPixel(x, y).ToArgb() <> actual.GetPixel(x, y).ToArgb() Then
                    Return "pixel " & x & "," & y & " is " & actual.GetPixel(x, y).ToString() & ", not " & expected.GetPixel(x, y).ToString()
                End If
            Next
        Next
        Return Nothing
    End Function

    Private Shared Function FromWindow(handle As IntPtr) As Accessibility.IAccessible
        Dim iid As Guid = GetType(Accessibility.IAccessible).GUID
        Dim found As Object = Nothing
        Assert.AreEqual(0, AccessibleObjectFromWindow(handle, OBJID_CLIENT, iid, found), "The window answers MSAA.")
        Return DirectCast(found, Accessibility.IAccessible)
    End Function

    Private Shared Sub RunOnStaThread(action As Action)
        StaRunner.Run(action)
    End Sub

    ' What the list's menu held when it opened. A menu over a window that
    ' is not active, as a test's is, may close again as messages run; the
    ' watch closes it at the end in any case.
    Private NotInheritable Class MenuWatch
        Implements IDisposable

        Private ReadOnly _menu As ContextMenuStrip
        Public Opened As List(Of String) = Nothing
        Public Events As String = String.Empty

        Public Sub New(menu As ContextMenuStrip)
            _menu = menu
            AddHandler _menu.Opened, AddressOf OnOpened
            AddHandler _menu.Closed, AddressOf OnClosed
        End Sub

        Private Sub OnOpened(sender As Object, e As EventArgs)
            Events &= "opened "
            Opened = _menu.Items.Cast(Of ToolStripItem)().Select(Function(item) item.Text).ToList()
        End Sub

        Private Sub OnClosed(sender As Object, e As ToolStripDropDownClosedEventArgs)
            Events &= "closed(" & e.CloseReason.ToString() & ") "
        End Sub

        Public Sub Dispose() Implements IDisposable.Dispose
            RemoveHandler _menu.Opened, AddressOf OnOpened
            RemoveHandler _menu.Closed, AddressOf OnClosed
            _menu.Close()
            Application.DoEvents()
        End Sub

    End Class

    ' Focus cues on, as a key press turns them on: a window that has seen
    ' only the mouse, as a test's has, hides them.
    Private Shared Sub ShowFocusCues(control As Control)
        Post(control, WM_UPDATEUISTATE, (UISF_HIDEFOCUS << 16) Or UIS_CLEAR, 0)
        Application.DoEvents()
        Assert.IsTrue(CBool(GetType(Control).GetProperty("ShowFocusCues", BindingFlags.Instance Or BindingFlags.NonPublic).GetValue(control)),
                      control.GetType().Name & " shows focus cues.")
    End Sub

    ' Each EVENT_OBJECT_FOCUS raised for the window, read back the way a
    ' screen reader reads it: AccessibleObjectFromEvent, then the name.
    Private NotInheritable Class FocusHook
        Implements IDisposable

        Private ReadOnly _window As IntPtr
        Private ReadOnly _heard As List(Of String)
        Private ReadOnly _callback As WinEventProc
        Private ReadOnly _hook As IntPtr

        Public Sub New(window As IntPtr, heard As List(Of String))
            _window = window
            _heard = heard
            _callback = AddressOf OnEvent
            _hook = SetWinEventHook(EVENT_OBJECT_FOCUS, EVENT_OBJECT_FOCUS, IntPtr.Zero, _callback, CUInt(Environment.ProcessId), 0UI, 0UI)
            Assert.AreNotEqual(IntPtr.Zero, _hook, "The focus hook is set.")
        End Sub

        ' Windows' own event for the window as it takes focus names the list
        ' itself; only the list's events for its children are kept.
        Private Sub OnEvent(hook As IntPtr, eventType As UInteger, window As IntPtr, objectId As Integer, childId As Integer, thread As UInteger, time As UInteger)
            If window <> _window OrElse objectId <> OBJID_CLIENT OrElse childId = 0 Then Return
            Dim accessible As Accessibility.IAccessible = Nothing
            Dim child As Object = Nothing
            If AccessibleObjectFromEvent(window, objectId, childId, accessible, child) <> 0 OrElse accessible Is Nothing Then
                _heard.Add("(nothing for child " & childId & ")")
                Return
            End If
            _heard.Add(CStr(accessible.accName(child)))
        End Sub

        Public Sub Dispose() Implements IDisposable.Dispose
            UnhookWinEvent(_hook)
        End Sub

    End Class

    ' The real main window on in-memory samples, as ManuscriptPageTests
    ' builds it: Load is skipped, saves are counted rather than written, and
    ' anything that would read the author library reads the test's folder.
    Private NotInheritable Class DeadlineBoard
        Inherits Form1

        Private ReadOnly _root As String
        Public SaveCount As Integer

        Public Sub New(root As String)
            _root = root
        End Sub

        Protected Overrides Sub OnLoad(e As EventArgs)
        End Sub

        Protected Overrides ReadOnly Property ShowWithoutActivation As Boolean
            Get
                Return True
            End Get
        End Property

        Protected Overrides Function SaveManuscripts() As Boolean
            SaveCount += 1
            Return True
        End Function

        Protected Overrides Function LoadAuthorLibrary() As Boolean
            Return True
        End Function

        Protected Overrides Function CreateManuscriptEditor(manuscript As Manuscript) As EditManuscriptForm
            Dim authors As String = Path.Combine(_root, "authors")
            Directory.CreateDirectory(authors)
            Return New EditManuscriptForm(manuscript, DirectCast(GetField("manuscripts"), List(Of Manuscript)), New AuthorLibraryRepository(authors), pageMode:=True)
        End Function

        Protected Overrides Function CreateAuthorLibraryEditor() As Form
            Return New Form()
        End Function

        Public Sub Prepare(samples As List(Of Manuscript))
            CallPrivate("BuildInterface")
            SetField("manuscripts", samples)
            CallPrivate("RenderManuscripts")
            StartPosition = FormStartPosition.Manual
            Location = New Point(-20000, -20000)
            ShowInTaskbar = False
            Show()
            Application.DoEvents()
        End Sub

        Public Sub OpenDeadlines()
            NavigateTo(WorkspacePage.Deadlines)
            Application.DoEvents()
        End Sub

        Public Sub SetPrompts(datePrompt As Func(Of DeadlineItem, DateTime?), confirmPrompt As Func(Of String, Boolean))
            deadlineDatePrompt = datePrompt
            deadlineConfirmPrompt = confirmPrompt
        End Sub

        Public Function CreateRow(item As DeadlineItem, today As DateTime) As DeadlineRow
            Return DirectCast(CallPrivate("CreateDeadlineRow", item, today), DeadlineRow)
        End Function

        Public Function List() As DeadlineList
            Return DirectCast(GetField("deadlinesList"), DeadlineList)
        End Function

        Public Function Page() As Control
            Return DirectCast(GetField("deadlinesPage"), Control)
        End Function

        Public Function Filter() As TextBox
            Return DirectCast(GetField("deadlinesFilter"), TextBox)
        End Function

        Public Function Subtitle() As String
            Return DirectCast(GetField("deadlinesSubtitle"), Label).Text
        End Function

        Public Function ContentHost() As Control
            Return DirectCast(GetField("contentHost"), Control)
        End Function

        Public Function ImportExportPage() As Control
            Return DirectCast(GetField("importExportPage"), Control)
        End Function

        ' As Windows does when a contrast theme is turned on or off.
        Public Sub ChangeSystemColors()
            OnSystemColorsChanged(EventArgs.Empty)
            Application.DoEvents()
        End Sub

        Public Sub SetPageDate(value As DateTime)
            SetField("deadlinesPageDate", value)
        End Sub

        Public Sub ClickDoneLine()
            Dim root As AccessibleObject = List().AccessibilityObject
            Enumerable.Range(0, root.GetChildCount()).Select(Function(index) root.GetChild(index)).
                Single(Function(child) child.Role = AccessibleRole.Link).DoDefaultAction()
            Application.DoEvents()
        End Sub

        Private Function CallPrivate(name As String, ParamArray args As Object()) As Object
            Return GetType(Form1).GetMethod(name, BindingFlags.Instance Or BindingFlags.NonPublic).Invoke(Me, args)
        End Function

        Private Function GetField(name As String) As Object
            Return GetType(Form1).GetField(name, BindingFlags.Instance Or BindingFlags.NonPublic).GetValue(Me)
        End Function

        Private Sub SetField(name As String, value As Object)
            GetType(Form1).GetField(name, BindingFlags.Instance Or BindingFlags.NonPublic).SetValue(Me, value)
        End Sub

    End Class

    Private Delegate Sub WinEventProc(hook As IntPtr, eventType As UInteger, window As IntPtr, objectId As Integer, childId As Integer, thread As UInteger, time As UInteger)

    <DllImport("user32.dll")>
    Private Shared Function PostMessage(handle As IntPtr, message As Integer, wParam As IntPtr, lParam As IntPtr) As Boolean
    End Function

    <DllImport("user32.dll")>
    Private Shared Function SetWinEventHook(eventMin As UInteger, eventMax As UInteger, library As IntPtr, callback As WinEventProc, processId As UInteger, threadId As UInteger, flags As UInteger) As IntPtr
    End Function

    <DllImport("user32.dll")>
    Private Shared Function UnhookWinEvent(hook As IntPtr) As Boolean
    End Function

    <DllImport("oleacc.dll")>
    Private Shared Function AccessibleObjectFromWindow(window As IntPtr, objectId As Integer, ByRef iid As Guid, <MarshalAs(UnmanagedType.IUnknown)> ByRef found As Object) As Integer
    End Function

    <DllImport("oleacc.dll")>
    Private Shared Function AccessibleObjectFromEvent(window As IntPtr, objectId As Integer, childId As Integer, <MarshalAs(UnmanagedType.Interface)> ByRef accessible As Accessibility.IAccessible, ByRef child As Object) As Integer
    End Function

End Class
