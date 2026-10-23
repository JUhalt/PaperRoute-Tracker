Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.Linq
Imports System.Windows.Forms
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline.Controls
Imports ManuscriptPipeline.Forms
Imports ManuscriptPipeline.Models

' Accessible names (#137): every input a screen reader lands on says what
' it is, and so does every button whose face has no letters. The sweep
' runs over the view catalog, each view on every one of its tab pages in
' turn, and reads AccessibilityObject.Name, which is what Narrator reads:
' the control's AccessibleName, or else the label WinForms finds before
' it in tab order. WinForms stops looking at the first tab stop, so a
' help control or a check box between a label and its field leaves the
' field without a name, and the label it does find can be an empty
' list's hint or a value rather than the field's caption.
<TestClass>
<DoNotParallelize>
Public Class AccessibleNameTests

    ' Every visible text, combo, list, grid, number and date control in
    ' every view has a name, and none is named by a link, a button or a
    ' sentence that happens to come before it; every button with no
    ' letters on its face has an AccessibleName, and a ? help button's
    ' says what it helps with, not just "Help".
    <TestMethod>
    Public Sub EveryInputAndEveryButtonWithNoLettersHasAName()

        Dim reports As New List(Of String)()

        Dim failures As List(Of String) =
            ViewCatalog.ShowEach(
                ViewCatalog.AllViews(),
                Sub(name, shown)
                    reports.AddRange(
                        Unnamed(shown).Select(Function(line) name & ": " & line)
                    )
                End Sub
            )

        Assert.AreEqual(
            0,
            failures.Count,
            Environment.NewLine & String.Join(Environment.NewLine, failures)
        )

        ' A view hosted in the main window is reported under each name the
        ' catalog shows it by, so the count can name one control twice.
        Assert.AreEqual(
            0,
            reports.Count,
            reports.Count & " reports:" & Environment.NewLine &
            String.Join(Environment.NewLine, reports)
        )

    End Sub


    ' The sweep has teeth. A guard: it checks the sweep, not the app, so
    ' it cannot fail before the change. A field after a label is named by
    ' it; one after a help control, one after a check box, one after a
    ' link and one after a sentence are reported, as are a button whose
    ' face is an ellipsis and a help control named only "Help"; the same
    ' button with an AccessibleName is not.
    <TestMethod>
    Public Sub TheSweepReportsWhatWinFormsLeavesUnnamed()

        Dim reported As List(Of String) =
            Nothing

        StaRunner.Run(
            Sub()

                Using probe As New Form With {
                    .StartPosition = FormStartPosition.Manual,
                    .Location = New Point(-20000, -20000),
                    .ShowInTaskbar = False
                }

                    Dim layout As New FlowLayoutPanel With {
                        .Dock = DockStyle.Fill,
                        .FlowDirection = FlowDirection.TopDown
                    }

                    layout.Controls.Add(New Label With {.Text = "Named field"})
                    layout.Controls.Add(New TextBox With {.Name = "named"})
                    layout.Controls.Add(New Label With {.Text = "Helped field"})
                    layout.Controls.Add(New ContextHelpControl("Help for the field.") With {.Name = "bareHelp"})
                    layout.Controls.Add(New TextBox With {.Name = "afterHelp"})
                    layout.Controls.Add(New CheckBox With {.Text = "Use a date"})
                    layout.Controls.Add(New DateTimePicker With {.Name = "afterCheck"})
                    layout.Controls.Add(New LinkLabel With {.Text = "Show all"})
                    layout.Controls.Add(New ListBox With {.Name = "afterLink"})
                    layout.Controls.Add(New Label With {.Text = "Nothing here yet."})
                    layout.Controls.Add(New TextBox With {.Name = "afterSentence", .Multiline = True})
                    layout.Controls.Add(New Button With {.Text = "...", .Name = "ellipsis"})
                    layout.Controls.Add(New Button With {.Text = "...", .Name = "namedEllipsis", .AccessibleName = "Choose a file"})

                    ' A drawn list whose row has a button with no letters.
                    Dim arrowRow As New DeadlineRow(New DeadlineItem With {.Title = "Arrow", .Kind = DeadlineKind.Reminder, .Group = DeadlineGroup.Today, .DueDate = DateTime.Today}, DateTime.Today)
                    arrowRow.AddAction("→", Sub()
                                            End Sub)
                    arrowRow.FinishActions()
                    Dim drawn As New DeadlineList With {.Name = "drawnArrow", .Size = New Size(400, 120), .AccessibleName = "Deadlines"}
                    drawn.SetEntries({DeadlineEntry.ForRow(arrowRow)})
                    layout.Controls.Add(drawn)

                    probe.Controls.Add(layout)
                    probe.Show()
                    Application.DoEvents()

                    reported = Unnamed(probe)

                    probe.Close()

                End Using

            End Sub
        )

        Dim names As String() =
            {"bareHelp", "afterHelp", "afterCheck", "afterLink", "afterSentence", "ellipsis", "drawnArrow"}

        Assert.AreEqual(
            names.Length,
            reported.Count,
            String.Join(Environment.NewLine, reported)
        )

        For Each name As String In names
            Assert.IsTrue(
                reported.Any(Function(line) line.Contains("'" & name & "'", StringComparison.Ordinal)),
                name & " is reported: " & String.Join(" | ", reported)
            )
        Next

    End Sub


    ' The shared label-with-help names the field for its label and the help
    ' control for the label it helps with, so the sweep finds nothing to
    ' report beside it.
    <TestMethod>
    Public Sub ALabelWithHelpNamesItsFieldAndItsHelp()

        Dim reported As List(Of String) =
            Nothing

        Dim fieldName As String =
            Nothing

        Dim helpName As String =
            Nothing

        StaRunner.Run(
            Sub()

                Using probe As New Form With {
                    .StartPosition = FormStartPosition.Manual,
                    .Location = New Point(-20000, -20000),
                    .ShowInTaskbar = False
                }

                    Dim layout As New FlowLayoutPanel With {
                        .Dock = DockStyle.Fill,
                        .FlowDirection = FlowDirection.TopDown
                    }

                    Dim field As New TextBox()

                    Dim caption As Control =
                        ContextHelpControl.LabelWithHelp(
                            New Label With {.Text = "Journal manuscript ID", .AutoSize = True},
                            "The ID the journal gave the submission.",
                            7,
                            field
                        )

                    layout.Controls.Add(caption)
                    layout.Controls.Add(field)
                    probe.Controls.Add(layout)
                    probe.Show()
                    Application.DoEvents()

                    reported = Unnamed(probe)
                    fieldName = field.AccessibilityObject.Name
                    helpName = ViewCatalog.Descendants(caption).OfType(Of ContextHelpControl)().Single().AccessibilityObject.Name

                    probe.Close()

                End Using

            End Sub
        )

        Assert.AreEqual(
            "Journal manuscript ID",
            fieldName
        )

        Assert.AreEqual(
            "Help: Journal manuscript ID",
            helpName
        )

        Assert.AreEqual(
            0,
            reported.Count,
            String.Join(Environment.NewLine, reported)
        )

    End Sub


    ' One line for each control in the form that a screen reader would
    ' announce without a name, or by a link's, a button's or a sentence's
    ' words. The form's tab pages are shown one after another, so controls
    ' on a page that is not in front are read as well, and the first is put
    ' back.
    Friend Shared Function Unnamed(
        form As Form
    ) As List(Of String)

        Dim lines As New List(Of String)()
        Dim seen As New HashSet(Of Control)()

        Inspect(form, form, seen, lines)

        For Each tabs As TabControl In ViewCatalog.Descendants(form).OfType(Of TabControl)().ToList()

            Dim front As TabPage =
                tabs.SelectedTab

            For Each page As TabPage In tabs.TabPages.Cast(Of TabPage)().ToList()

                Reveal(page)
                Inspect(form, page, seen, lines)

            Next

            If front IsNot Nothing Then
                Reveal(front)
            End If

        Next

        Return lines

    End Function


    Private Shared Sub Inspect(
        form As Form,
        container As Control,
        seen As HashSet(Of Control),
        lines As List(Of String)
    )

        Dim actions As New HashSet(Of String)(
            ViewCatalog.Descendants(form).
                Where(Function(control) TypeOf control Is LinkLabel OrElse TypeOf control Is ButtonBase).
                Select(Function(control) control.Text.Trim()).
                Where(Function(text) text.Length > 0),
            StringComparer.Ordinal
        )

        For Each control As Control In ViewCatalog.Descendants(container)

            If Not control.Visible OrElse seen.Contains(control) Then
                Continue For
            End If

            If IsInput(control) Then

                seen.Add(control)

                Dim name As String =
                    If(control.AccessibilityObject.Name, String.Empty).Trim()

                If name.Length = 0 OrElse IsDefaultName(control, name) Then
                    lines.Add(Describe(control) & " has no name.")
                ElseIf control.AccessibleName Is Nothing AndAlso actions.Contains(name) Then
                    lines.Add(Describe(control) & " is named """ & name & """ by the link or button before it.")
                ElseIf control.AccessibleName Is Nothing AndAlso Not IsCaption(name) Then
                    lines.Add(Describe(control) & " is named """ & Shorten(name) & """ by text before it that is not its caption.")
                End If

            ElseIf IsButton(control) AndAlso Not control.Text.Any(Function(character) Char.IsLetter(character)) Then

                seen.Add(control)

                If String.IsNullOrWhiteSpace(control.AccessibleName) Then
                    lines.Add(Describe(control) & " shows """ & control.Text & """ and has no AccessibleName.")
                ElseIf TypeOf control Is ContextHelpControl AndAlso String.Equals(control.AccessibleName, "Help", StringComparison.Ordinal) Then
                    lines.Add(Describe(control) & " is named only ""Help"", not for what it helps with.")
                End If

            ElseIf DrawsItsChildren(control) Then

                seen.Add(control)
                InspectDrawn(control, lines)

            End If

        Next

    End Sub


    ' A control that draws its items, buttons and text rather than hosting a
    ' control for each (#133). A screen reader reads them as its accessible
    ' children, so the same rules hold for them.
    Private Shared Function DrawsItsChildren(
        control As Control
    ) As Boolean

        Return TypeOf control Is DeadlineList OrElse
            TypeOf control Is DeadlineGlance OrElse
            TypeOf control Is CommandList OrElse
            TypeOf control Is JournalShortlistList

    End Function


    ' Every drawn button is named with letters, and every drawn item, text
    ' or link has a name.
    Private Shared Sub InspectDrawn(
        control As Control,
        lines As List(Of String)
    )

        Dim accessible As AccessibleObject =
            control.AccessibilityObject

        For index As Integer = 0 To accessible.GetChildCount() - 1

            Dim child As AccessibleObject =
                accessible.GetChild(index)

            If child Is Nothing Then
                lines.Add(Describe(control) & " has no child " & index & ".")
                Continue For
            End If

            Dim name As String =
                If(child.Name, String.Empty).Trim()

            Select Case child.Role

                Case AccessibleRole.PushButton
                    If Not name.Any(Function(character) Char.IsLetter(character)) Then
                        lines.Add(Describe(control) & " draws a button named """ & name & """, with no letters.")
                    End If

                Case AccessibleRole.ListItem, AccessibleRole.StaticText, AccessibleRole.Link
                    If name.Length = 0 Then
                        lines.Add(Describe(control) & " draws a " & child.Role.ToString() & " with no name.")
                    End If

            End Select

        Next

    End Sub


    ' The text, combo, list, grid, number and date controls. The text box
    ' inside a number box is the number box's own, and is not counted.
    Private Shared Function IsInput(
        control As Control
    ) As Boolean

        If TypeOf control Is TextBoxBase Then
            Return Not TypeOf control.Parent Is UpDownBase
        End If

        Return TypeOf control Is ComboBox OrElse
            TypeOf control Is ListBox OrElse
            TypeOf control Is ListView OrElse
            TypeOf control Is TreeView OrElse
            TypeOf control Is DataGridView OrElse
            TypeOf control Is UpDownBase OrElse
            TypeOf control Is DateTimePicker OrElse
            TypeOf control Is MonthCalendar

    End Function


    ' A Button, check box or radio button, or a control drawn as one.
    Private Shared Function IsButton(
        control As Control
    ) As Boolean

        Return TypeOf control Is ButtonBase OrElse
            control.AccessibleRole = AccessibleRole.PushButton

    End Function


    ' A caption is a few words. The label WinForms finds before a field
    ' can instead be a sentence, such as an empty list's hint, an intro or
    ' a count ("0 reusable journals."), or a value, such as a portal's
    ' address; none of them says what the field is.
    Private Shared Function IsCaption(
        name As String
    ) As Boolean

        Return name.Length <= 60 AndAlso
            Not name.EndsWith("."c) AndAlso
            Not name.Contains("://", StringComparison.Ordinal)

    End Function


    ' WinForms names an unnamed grid after its class, which tells a
    ' listener nothing.
    Private Shared Function IsDefaultName(
        control As Control,
        name As String
    ) As Boolean

        Dim type As Type =
            control.GetType()

        While type IsNot Nothing

            If String.Equals(name, type.Name, StringComparison.OrdinalIgnoreCase) Then
                Return True
            End If

            type = type.BaseType

        End While

        Return False

    End Function


    ' Brings the page to the front, and every page it sits on.
    Private Shared Sub Reveal(
        page As TabPage
    )

        Dim pages As New List(Of TabPage)()
        Dim current As Control = page

        While current IsNot Nothing
            If TypeOf current Is TabPage Then
                pages.Insert(0, DirectCast(current, TabPage))
            End If
            current = current.Parent
        End While

        For Each outer As TabPage In pages
            Dim tabs As TabControl = TryCast(outer.Parent, TabControl)
            If tabs IsNot Nothing AndAlso tabs.SelectedTab IsNot outer Then
                tabs.SelectedTab = outer
            End If
        Next

        Application.DoEvents()

    End Sub


    ' The control's form, kind, what it shows, and where it sits, so each
    ' report can be found in the code.
    Private Shared Function Describe(
        control As Control
    ) As String

        Dim owner As Form =
            control.FindForm()

        Dim shown As String =
            If(TypeOf control Is TextBox, DirectCast(control, TextBox).PlaceholderText, String.Empty)

        If String.IsNullOrWhiteSpace(shown) AndAlso Not TypeOf control Is TextBoxBase AndAlso Not TypeOf control Is ListControl Then
            shown = control.Text
        End If

        Dim page As TabPage =
            Nothing

        Dim parent As Control =
            control.Parent

        While parent IsNot Nothing AndAlso page Is Nothing
            page = TryCast(parent, TabPage)
            parent = parent.Parent
        End While

        Return If(owner Is Nothing, "(no form)", owner.GetType().Name) & " " &
            control.GetType().Name &
            If(String.IsNullOrEmpty(control.Name), String.Empty, " '" & control.Name & "'") &
            If(String.IsNullOrWhiteSpace(shown), String.Empty, " """ & Shorten(shown) & """") &
            If(page Is Nothing, String.Empty, " on tab """ & page.Text & """") &
            " after " & Before(control)

    End Function


    ' The text of the nearest label or button before the control in its
    ' container, as a clue to which field it is.
    Private Shared Function Before(
        control As Control
    ) As String

        Dim container As Control =
            control.Parent

        While container IsNot Nothing

            Dim earlier As Control =
                ViewCatalog.Descendants(container).
                    TakeWhile(Function(item) item IsNot control AndAlso Not item.Contains(control)).
                    LastOrDefault(Function(item) (TypeOf item Is Label OrElse TypeOf item Is ButtonBase) AndAlso item.Text.Trim().Length > 0)

            If earlier IsNot Nothing Then
                Return """" & Shorten(earlier.Text) & """"
            End If

            container = container.Parent

        End While

        Return "nothing"

    End Function


    Private Shared Function Shorten(
        text As String
    ) As String

        Dim line As String =
            text.Replace(Environment.NewLine, " ").Trim()

        Return If(line.Length > 40, line.Substring(0, 37) & "...", line)

    End Function

End Class
