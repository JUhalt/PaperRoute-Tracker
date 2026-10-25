Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.Linq
Imports System.Reflection
Imports System.Runtime.InteropServices
Imports System.Windows.Forms
Imports ManuscriptPipeline
Imports ManuscriptPipeline.Controls
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services
Imports Microsoft.VisualStudio.TestTools.UnitTesting

' The Insights route chart as a screen reader meets it (#193). Up and Down
' move the selection from route to route, and each move is announced as
' the route it lands on, the way the Deadlines list announces its rows
' (#133). The chart said the route in its description and told readers of
' the change with Control.AccessibilityNotifyClients, which raises nothing
' on .NET 10.0, so no move was heard.
<TestClass>
<DoNotParallelize>
Public Class RouteMapChartTests

    Private Const WM_KEYDOWN As Integer = &H100
    Private Const OBJID_CLIENT As Integer = -4
    Private Const STATE_SYSTEM_SELECTED As Integer = &H2
    Private Const STATE_SYSTEM_FOCUSED As Integer = &H4
    Private Const SELFLAG_TAKESELECTION As Integer = &H2

    Private Shared ReadOnly Today As New DateTime(2026, 9, 28)

    ' What each route is read as, shortest first: its title, its brief and
    ' its summary, the words the chart's description said.
    Private Shared ReadOnly RouteNames As String() = {
        "Still in review. 1 journal  ·  in progress. 30 days from first submission to today: 30 days with the journal.",
        "Quick note. 1 journal. 61 days from first submission to publication: 40 days with the journal, 21 days in production.",
        "Anchoring effects in clinical risk estimates. 2 journals  ·  2 revisions  ·  1 rejection. " &
            "264 days from first submission to publication: 105 days with the journals, 131 days with you, 28 days in production."
    }


    ' Tab onto the chart reads the selected route, and each Down and Up
    ' reads the route it lands on: an EVENT_OBJECT_FOCUS for that route's
    ' child, heard through an out-of-context WinEvent hook and resolved with
    ' AccessibleObjectFromEvent, as a screen reader resolves it, to the
    ' route's name. At either end nothing moves and nothing is said. The
    ' keys do what they did: the selection follows them, and Enter opens
    ' the selected route. Then what MSAA finds: one list named for its
    ' routes, each route an item of it, the one the keyboard is on both
    ' focused and selected, the route under a point, and a route chosen by
    ' the reader selected. Narrator's own reading is the Narrator pass's
    ' (#147).
    <TestMethod>
    Public Sub UpAndDownAnnounceTheRouteTheyLandOn()

        StaRunner.Run(
            Sub()

                Using host As New Form With {
                    .StartPosition = FormStartPosition.Manual,
                    .Location = New Point(-20000, -20000),
                    .ShowInTaskbar = False,
                    .ClientSize = New Size(900, 400)
                }

                    ' A field before the chart has the keyboard first, so Tab
                    ' brings it to the chart.
                    Dim before As New TextBox With {
                        .Bounds = New Rectangle(0, 0, 200, 24),
                        .TabIndex = 0
                    }

                    Dim chart As New RouteMapChart With {
                        .Bounds = New Rectangle(0, 40, 900, 100),
                        .BackColor = UiTheme.CardBackground(),
                        .TabIndex = 1,
                        .Routes = Routes()
                    }

                    Dim opened As New List(Of String)()
                    AddHandler chart.RouteOpened, Sub(sender, entry) opened.Add(entry.Manuscript.Title)

                    host.Controls.Add(before)
                    host.Controls.Add(chart)
                    host.Show()
                    Application.DoEvents()
                    Assert.IsTrue(before.Focus(), "The field takes the keyboard.")
                    Application.DoEvents()

                    ' Each move of the keyboard is announced as the route it
                    ' lands on.
                    Dim heard As New List(Of String)()
                    Dim selected As New List(Of Integer)()
                    Using hook As New DeadlineListTests.FocusHook(chart.Handle, heard)
                        Press(before, Keys.Tab)
                        Assert.IsTrue(chart.Focused, "Tab reaches the chart.")
                        For Each key As Keys In {Keys.Down, Keys.Down, Keys.Down, Keys.Up, Keys.Up, Keys.Up, Keys.Down}
                            Press(chart, key)
                            selected.Add(chart.SelectedIndex)
                        Next
                        Application.DoEvents()
                    End Using

                    CollectionAssert.AreEqual(
                        {RouteNames(0), RouteNames(1), RouteNames(2), RouteNames(1), RouteNames(0), RouteNames(1)},
                        heard,
                        "Heard: " & If(heard.Count = 0, "(nothing)", String.Join(" | ", heard)))

                    CollectionAssert.AreEqual({1, 2, 2, 1, 0, 0, 1}, selected, "Up and Down choose a route, and stop at either end.")
                    Press(chart, Keys.Enter)
                    CollectionAssert.AreEqual({"Quick note"}, opened, "Enter opens the selected route.")

                    ' Not answering UI Automation itself, the chart is read
                    ' through MSAA, whose events these are.
                    Dim supports As PropertyInfo = GetType(Control).GetProperty("SupportsUiaProviders", BindingFlags.Instance Or BindingFlags.NonPublic)
                    Assert.IsNotNull(supports, "Control.SupportsUiaProviders exists on this runtime.")
                    Assert.IsFalse(CBool(supports.GetValue(chart)), "The chart is read through MSAA.")

                    Dim root As Accessibility.IAccessible = FromWindow(chart.Handle)
                    Assert.AreEqual("Routes side by side, 3 routes. Press Enter to open the selected route.", CStr(root.accName(0)))
                    Assert.AreEqual(CInt(AccessibleRole.List), CInt(root.accRole(0)))
                    Assert.AreEqual(RouteNames.Length, CInt(root.accChildCount))
                    For index As Integer = 0 To RouteNames.Length - 1
                        Dim childId As Object = index + 1
                        Assert.AreEqual(RouteNames(index), CStr(root.accName(childId)), "Child " & childId.ToString())
                        Assert.AreEqual(CInt(AccessibleRole.ListItem), CInt(root.accRole(childId)), "Child " & childId.ToString())
                        Dim state As Integer = CInt(root.accState(childId))
                        Assert.AreEqual(index = 1, (state And STATE_SYSTEM_SELECTED) <> 0, "Child " & childId.ToString() & " is selected only if it is the selected route.")
                        Assert.AreEqual(index = 1, (state And STATE_SYSTEM_FOCUSED) <> 0, "Child " & childId.ToString() & " is focused only if the keyboard is on it.")
                    Next
                    Assert.AreEqual(RouteNames(1), ChildName(root, root.accFocus), "The focused child is the route the keyboard is on.")
                    Assert.AreEqual(RouteNames(1), ChildName(root, root.accSelection), "The selected child is the selected route.")

                    ' Pointing at a route reads it, and choosing one from the
                    ' reader selects it, as Down would.
                    Dim left, top, width, height As Integer
                    root.accLocation(left, top, width, height, 3)
                    Assert.AreEqual(RouteNames(2), ChildName(root, root.accHitTest(left + width \ 2, top + height \ 2)), "The route under the pointer.")
                    root.accSelect(SELFLAG_TAKESELECTION, 3)
                    Assert.AreEqual(2, chart.SelectedIndex, "A reader's choice selects the route.")

                    host.Close()

                End Using

            End Sub)

    End Sub


    ' Three routes, shortest first: one still under review, one published
    ' quickly, and the design mockup's 264 days.
    Private Shared Function Routes() As List(Of RouteMapEntry)
        Dim quick As New Manuscript With {.Title = "Quick note", .CurrentStage = PaperStage.Published, .Location = ManuscriptLocation.Published}
        quick.Submissions.Add(Submitted("Open Psychology", New DateTime(2026, 5, 1), (EditorialDecision.Accepted, New DateTime(2026, 6, 10))))
        quick.Metadata.PublishedDate = New DateTime(2026, 7, 1)
        Dim reviewing As New Manuscript With {.Title = "Still in review", .CurrentStage = PaperStage.UnderReview}
        reviewing.Submissions.Add(Submitted("Assessment", Today.AddDays(-30)))
        Return RouteMapService.Library({RouteMapServiceTests.Anchoring(), quick, reviewing}, Today, True).Routes
    End Function

    Private Shared Function Submitted(journal As String, submittedOn As DateTime, ParamArray decisions As (Decision As EditorialDecision, DecisionDate As DateTime)()) As JournalSubmission
        Dim submission As New JournalSubmission With {.JournalName = journal, .SubmittedDate = submittedOn}
        For Each decision In decisions
            submission.Decisions.Add(New EditorialDecisionEvent With {.Decision = decision.Decision, .DecisionDate = decision.DecisionDate})
        Next
        Return submission
    End Function

    ' A key as Windows delivers it: posted to the control's window, then
    ' through the message loop.
    Private Shared Sub Press(target As Control, key As Keys)
        PostMessage(target.Handle, WM_KEYDOWN, New IntPtr(CInt(key)), New IntPtr(1))
        Application.DoEvents()
    End Sub

    Private Shared Function FromWindow(handle As IntPtr) As Accessibility.IAccessible
        Dim iid As Guid = GetType(Accessibility.IAccessible).GUID
        Dim found As Object = Nothing
        Assert.AreEqual(0, AccessibleObjectFromWindow(handle, OBJID_CLIENT, iid, found), "The window answers MSAA.")
        Return DirectCast(found, Accessibility.IAccessible)
    End Function

    ' The name of what accFocus or accSelection gave: a child of its own,
    ' or a child's number, or nothing.
    Private Shared Function ChildName(root As Accessibility.IAccessible, found As Object) As String
        If found Is Nothing Then Return "(nothing)"
        If TypeOf found Is Accessibility.IAccessible Then Return CStr(DirectCast(found, Accessibility.IAccessible).accName(0))
        Return CStr(root.accName(found))
    End Function

    <DllImport("user32.dll")>
    Private Shared Function PostMessage(handle As IntPtr, message As Integer, wParam As IntPtr, lParam As IntPtr) As Boolean
    End Function

    <DllImport("oleacc.dll")>
    Private Shared Function AccessibleObjectFromWindow(window As IntPtr, objectId As Integer, ByRef iid As Guid, <MarshalAs(UnmanagedType.IUnknown)> ByRef found As Object) As Integer
    End Function

End Class
