Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.Drawing.Imaging
Imports System.Linq
Imports System.Reflection
Imports System.Runtime.InteropServices
Imports System.Windows.Forms
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline.Forms
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

' The dialogs' polish the v0.9.2 certification found at 125% (#258, C3),
' each measured where it can be at 96, 120 and 144 DPI. The host is at 96,
' so 120 and 144 are simulated as the scaling sweep simulates a display
' (ScalingMeasurementTests.Simulate): the text grown, and the window
' scaled as Windows Forms scales it if its source asks for that. Each test
' failed on 80e1a7c, the cut candidate.
' LabelsAreWholeInAWindowBuiltAt96To144Dpi (#263, C5) instead builds each
' window as a display builds it (ScalingMeasurementTests.AtDisplayDpi),
' which Simulate cannot do faithfully for a window scaled by hand; it
' failed on 88a191e.
<TestClass>
<DoNotParallelize>
Public Class DialogPolishTests

    Private Shared ReadOnly Dpis As Integer() = {96, 120, 144}


    ' ---------------------------------------------------------------
    ' Combos (item 1)
    ' ---------------------------------------------------------------

    ' A drop-down list shows its chosen item whole or ends it in an
    ' ellipsis (#258, item 1). At 125% the packet's "Exact manuscript
    ' version" read "...and supplementa" and the readiness "Journal
    ' profile" read "Journal of Reproducible Research ar", cut mid-letter
    ' where Windows clips a box's text. Each combo whose item is wider than
    ' its box draws it as TextRenderer draws it with an end ellipsis; on
    ' 80e1a7c they drew nothing of their own. The names are C3's.
    <TestMethod>
    Public Sub PacketAndReadinessCombosShowTheirItemWholeOrEndInAnEllipsis()

        Dim judged As New HashSet(Of String)(StringComparer.Ordinal)
        Dim found As New List(Of String)()

        Dim failures As List(Of String) =
            AtEachDpi(
                ComboViews,
                Sub(name, shown, dpi)

                    For Each combo As ComboBox In ChosenItemCombos(shown)

                        Dim box As Rectangle = SelectionField(combo)
                        Dim text As String = combo.GetItemText(combo.SelectedItem)

                        If TextWidth(combo, text) <= box.Width Then
                            Continue For
                        End If

                        judged.Add(name & ": " & text)

                        ' Windows asks only an owner-drawn combo to draw its
                        ' item; one it draws itself cuts the text mid-letter.
                        If combo.DrawMode = DrawMode.Normal Then
                            found.Add($"{name} at {dpi} DPI: '{text}' is wider than its {box.Width}-pixel box, and Windows draws it.")
                        ElseIf Not DrawsAsTextRendererDoes(combo, text, box.Size) Then
                            found.Add($"{name} at {dpi} DPI: '{text}' is wider than its {box.Width}-pixel box and is not drawn ending in an ellipsis.")
                        End If

                    Next

                End Sub)

        Assert.AreEqual(0, failures.Count, Environment.NewLine & String.Join(Environment.NewLine, failures))
        Assert.AreEqual(0, found.Count, Environment.NewLine & String.Join(Environment.NewLine, found))
        Assert.IsTrue(judged.Count >= 3, "The packet's version, readiness and submission and the readiness profile are wider than their boxes: " & String.Join("; ", judged))

    End Sub


    ' A drop-down list's chosen item that fits its box is drawn whole, from
    ' the field's left edge as Windows draws it (#265). At 125% the packet's
    ' "Real journal submission" read "... Methods — Sep 9, 20...", which
    ' Windows drew whole on 80e1a7c: TextRenderer's padding moved the item
    ' 3 pixels right and ended it about 6 pixels early. Each owner-drawn
    ' item, in a box exactly as wide as its text, is drawn as TextRenderer
    ' draws the text there, whole; on aaea437 each ended in an ellipsis.
    <TestMethod>
    Public Sub ComboItemsThatFitTheirBoxAreDrawnWhole()

        Dim judged As Integer = 0
        Dim found As New List(Of String)()

        Dim failures As List(Of String) =
            AtEachDpi(
                ComboViews,
                Sub(name, shown, dpi)

                    For Each combo As ComboBox In ChosenItemCombos(shown).Where(Function(item) item.DrawMode <> DrawMode.Normal)

                        Dim text As String = combo.GetItemText(combo.SelectedItem)
                        Dim width As Integer = TextWidth(combo, text)

                        judged += 1

                        If Not DrawsAsTextRendererDoes(combo, text, New Size(width, SelectionField(combo).Height)) Then
                            found.Add($"{name} at {dpi} DPI: '{text}' is not drawn whole in a box as wide as its text, {width} pixels.")
                        End If

                    Next

                End Sub)

        Assert.AreEqual(0, failures.Count, Environment.NewLine & String.Join(Environment.NewLine, failures))
        Assert.AreEqual(0, found.Count, Environment.NewLine & String.Join(Environment.NewLine, found))
        Assert.IsTrue(judged >= 4 * Dpis.Length, "The packet's version, readiness and submission and the readiness profile at each DPI: " & judged.ToString())

    End Sub


    ' In Dark, a drop-down list's selected item is drawn white on the blue
    ' of Windows' own lists (#265). .NET's Dark colors pair the selection's
    ' blue, (40, 100, 180), with black text, about 3.6:1: C3 found the
    ' focused readiness profile drawn so, where on 80e1a7c, as in every
    ' list Windows draws itself, it was white on (0, 120, 215). Each
    ' owner-drawn item, drawn selected in its box in Dark, is drawn as
    ' TextRenderer draws its text white on that blue, inside the focus
    ' rectangle; on aaea437 it was black on (40, 100, 180).
    <TestMethod>
    Public Sub ComboSelectionsInDarkAreWhiteOnWindowsBlue()

        If SystemInformation.HighContrast Then
            Assert.Inconclusive("A contrast theme sets its own selection colors; these checks do not change OS settings.")
        End If

        If Not OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000) Then
            Assert.Inconclusive("Native WinForms dark mode requires Windows 11 or later.")
        End If

        Dim windowsBlue As Color = Color.FromArgb(0, 120, 215)
        Dim judged As Integer = 0
        Dim found As New List(Of String)()

        Dim failures As List(Of String) =
            AtEachDpi(
                ComboViews,
                Sub(name, shown, dpi)

                    For Each combo As ComboBox In ChosenItemCombos(shown).Where(Function(item) item.DrawMode <> DrawMode.Normal)

                        Dim text As String = combo.GetItemText(combo.SelectedItem)
                        Dim size As Size = SelectionField(combo).Size
                        judged += 1

                        Using drawn As Bitmap = DrawnItem(combo, size, DrawItemState.Selected Or DrawItemState.Focus Or DrawItemState.ComboBoxEdit), expected As New Bitmap(size.Width, size.Height, PixelFormat.Format32bppArgb)

                            Using g As Graphics = Graphics.FromImage(expected)
                                g.Clear(windowsBlue)
                                TextRenderer.DrawText(g, text, combo.Font, New Rectangle(Point.Empty, size), Color.White, TextFormatFlags.VerticalCenter Or TextFormatFlags.SingleLine Or TextFormatFlags.EndEllipsis Or TextFormatFlags.NoPrefix Or TextFormatFlags.NoPadding)
                            End Using

                            ' The focus rectangle is drawn on the box's edge.
                            If Not SamePixels(drawn, expected, inset:=2) Then
                                Dim fill As Color = drawn.GetPixel(2, 2)
                                found.Add($"{name} at {dpi} DPI: '{text}' is not drawn white on (0, 120, 215); its box is ({fill.R}, {fill.G}, {fill.B}).")
                            End If

                        End Using

                    Next

                End Sub,
                SystemColorMode.Dark)

        Assert.AreEqual(0, failures.Count, Environment.NewLine & String.Join(Environment.NewLine, failures))
        Assert.AreEqual(0, found.Count, Environment.NewLine & String.Join(Environment.NewLine, found))
        Assert.IsTrue(judged >= 4 * Dpis.Length, "The packet's version, readiness and submission and the readiness profile at each DPI: " & judged.ToString())

    End Sub


    ' The windows whose drop-down lists draw their own items, with C3's
    ' long names.
    Private Shared ReadOnly ComboViews As (Name As String, View As Type, Build As Func(Of Form))() = {
        ("SubmissionPacketEditForm, Edit", GetType(SubmissionPacketEditForm), Function() CType(PacketEditor(existing:=True), Form)),
        ("SubmissionPacketEditForm, New", GetType(SubmissionPacketEditForm), Function() CType(PacketEditor(existing:=False), Form)),
        ("ManuscriptReadinessForm", GetType(ManuscriptReadinessForm), Function() CType(New ManuscriptReadinessForm(LongNames(), New AuthorLibraryData()), Form))
    }


    ' The drop-down lists showing a chosen item.
    Private Shared Function ChosenItemCombos(shown As Form) As IEnumerable(Of ComboBox)
        Return ViewCatalog.Descendants(shown).OfType(Of ComboBox)().Where(Function(item) item.Visible AndAlso item.DropDownStyle = ComboBoxStyle.DropDownList AndAlso item.SelectedIndex >= 0)
    End Function


    ' How wide the text is, with no padding, as Windows draws a box's item.
    Private Shared Function TextWidth(combo As ComboBox, text As String) As Integer
        Return TextRenderer.MeasureText(text, combo.Font, New Size(Integer.MaxValue, Integer.MaxValue), TextFormatFlags.SingleLine Or TextFormatFlags.NoPrefix Or TextFormatFlags.NoPadding).Width
    End Function


    ' Whether the combo draws its item, in a box of the given size as
    ' Windows asks it to, with the pixels TextRenderer draws for the text
    ' from the box's left edge (#265), whole or ending in an ellipsis.
    ' OnDrawItem raises DrawItem whatever the combo's DrawMode, so a combo
    ' with no handler draws nothing here; whether Windows asks it to draw
    ' at all is the caller's check.
    Private Shared Function DrawsAsTextRendererDoes(combo As ComboBox, text As String, size As Size) As Boolean

        Using drawn As Bitmap = DrawnItem(combo, size, DrawItemState.ComboBoxEdit), expected As New Bitmap(size.Width, size.Height, PixelFormat.Format32bppArgb)

            Using g As Graphics = Graphics.FromImage(expected)
                g.Clear(combo.BackColor)
                TextRenderer.DrawText(g, text, combo.Font, New Rectangle(Point.Empty, size), combo.ForeColor, TextFormatFlags.VerticalCenter Or TextFormatFlags.SingleLine Or TextFormatFlags.EndEllipsis Or TextFormatFlags.NoPrefix Or TextFormatFlags.NoPadding)
            End Using

            Return SamePixels(drawn, expected)

        End Using

    End Function


    ' What the combo draws for its chosen item in a box of the given size
    ' and the given state, as Windows asks it to.
    Private Shared Function DrawnItem(combo As ComboBox, size As Size, state As DrawItemState) As Bitmap

        Dim drawn As New Bitmap(size.Width, size.Height, PixelFormat.Format32bppArgb)

        Using g As Graphics = Graphics.FromImage(drawn)
            g.Clear(combo.BackColor)
            Dim args As New DrawItemEventArgs(g, combo.Font, New Rectangle(Point.Empty, size), combo.SelectedIndex, state, combo.ForeColor, combo.BackColor)
            GetType(ComboBox).GetMethod("OnDrawItem", BindingFlags.Instance Or BindingFlags.NonPublic).Invoke(combo, New Object() {args})
        End Using

        Return drawn

    End Function


    ' Where Windows draws a drop-down list's chosen item, in its client
    ' coordinates.
    Private Shared Function SelectionField(combo As ComboBox) As Rectangle

        Dim info As New ComboBoxInfo With {.Size = Marshal.SizeOf(Of ComboBoxInfo)()}

        If Not GetComboBoxInfo(combo.Handle, info) Then
            Throw New InvalidOperationException("Windows did not describe the combo.")
        End If

        Return Rectangle.FromLTRB(info.Item.Left, info.Item.Top, info.Item.Right, info.Item.Bottom)

    End Function


    ' A manuscript with the long names of C3's fixture: its version, its
    ' readiness profile and its submission's journal, and a packet of all
    ' three.
    Private Shared Function LongNames() As Manuscript

        Const journal As String = "Journal of Reproducible Research and Interdisciplinary Methods"

        Dim manuscript As New Manuscript With {.Title = "Reproducible research across disciplines", .TargetJournal = journal, .CurrentStage = PaperStage.Submitted}
        Dim version As New ManuscriptVersion With {.Label = "Version 3 - revised methods, complete tables, and supplementary analysis", .CreatedDate = New DateTime(2026, 9, 10)}
        manuscript.Versions.Add(version)
        manuscript.CurrentVersionId = version.Id

        Dim readiness As New ManuscriptReadiness With {.JournalName = journal}
        readiness.Items.Add(New ReadinessItemState With {.Title = "Prepare a data availability statement", .SortOrder = 0})
        manuscript.ReadinessProfiles.Add(readiness)

        Dim submission As New JournalSubmission With {.JournalName = journal, .SubmittedDate = New DateTime(2026, 9, 9)}
        manuscript.Submissions.Add(submission)

        SubmissionPacketService.CreatePacket(manuscript, version.Id, "Revision package", String.Empty, readiness.Id, submission.Id)

        Return manuscript

    End Function


    Private Shared Function PacketEditor(existing As Boolean) As SubmissionPacketEditForm
        Dim manuscript As Manuscript = LongNames()
        Return New SubmissionPacketEditForm(manuscript, If(existing, manuscript.SubmissionPackets(0), Nothing))
    End Function


    ' ---------------------------------------------------------------
    ' Lists (item 2)
    ' ---------------------------------------------------------------

    ' A list scrolls sideways only when its columns cannot fit (#258, item
    ' 2). The lookup's list of what was found had 810 pixels of columns at
    ' 96 DPI and 803 of list once its vertical bar showed, so a horizontal
    ' bar covered part of its last row; the journals found ran a few pixels
    ' past a list made narrower by a smaller screen. Each list, its columns
    ' sized as LogicalToDeviceUnits sizes them at the display's DPI, the
    ' lookup's at its least height and the journals' list narrowed until
    ' its columns run 6 pixels past it, has no horizontal bar while all its
    ' columns but the last, and the last one's caption, fit beside each
    ' other. Below that the list scrolls as it did before #258 and its last
    ' column keeps the width the window built it with: the journals' list
    ' at its window's least width, where the fit cut "Yours" to its caption
    ' and "On your shortlist" to an ellipsis while the list scrolled anyway.
    <TestMethod>
    Public Sub LookupAndJournalListsScrollSidewaysOnlyWhenTheirColumnsCannotFit()

        Dim judged As New List(Of String)()
        Dim narrowed As New List(Of String)()
        Dim found As New List(Of String)()
        Dim failures As New List(Of String)()

        Dim suggestions = ViewCatalog.AllViewsAndStages().Single(Function(view) view.Name = "JournalSuggestionsForm, with the journals it found")

        For Each dpi As Integer In Dpis

            Dim at As Integer = dpi

            failures.AddRange(
                ViewCatalog.ShowEach(
                    {("JournalFactsForm, with what PLOS ONE's lookup found", CType(Function(root As String) CType(New JournalFactsForm(New JournalRecord With {.Name = "PLOS ONE", .Issns = New List(Of String) From {"1932-6203"}}, New JournalFactsTests.RecordedFacts(JournalFactsTests.PlosLookup())), Form), Func(Of String, Form)))},
                    Sub(name, shown)
                        Dim dialog As JournalFactsForm = DirectCast(shown, JournalFactsForm)
                        dialog.LookUpAsync().GetAwaiter().GetResult()
                        Application.DoEvents()
                        Dim list As ListView = dialog.ChangesList
                        AtTheDisplaysSize(shown, list, at)
                        shown.Height = shown.MinimumSize.Height
                        Application.DoEvents()
                        Assert.IsTrue(HasStyle(list, WS_VSCROLL), "At its least height the list has more rows than it shows.")
                        JudgeSidewaysBar(name & " at " & at & " DPI", list, judged, found)
                    End Sub))

            failures.AddRange(
                ViewCatalog.ShowEach(
                    {suggestions},
                    Sub(name, shown)
                        Dim list As ListView = DirectCast(shown, JournalSuggestionsForm).JournalsList
                        AtTheDisplaysSize(shown, list, at)
                        Dim columns As Integer = list.Columns.Cast(Of ColumnHeader)().Sum(Function(column) column.Width)
                        shown.Width -= list.ClientSize.Width - (columns - 6)
                        Application.DoEvents()
                        JudgeSidewaysBar(name & " at " & at & " DPI", list, judged, found)

                        shown.Width = shown.MinimumSize.Width
                        Application.DoEvents()
                        Dim yours As ColumnHeader = list.Columns(list.Columns.Count - 1)
                        Dim others As Integer = list.Columns.Cast(Of ColumnHeader)().Where(Function(column) column IsNot yours).Sum(Function(column) column.Width)
                        Assert.IsTrue(others > list.ClientSize.Width, $"At its least width the list's {others} pixels of columns before 'Yours' run past its {list.ClientSize.Width}.")
                        narrowed.Add(name & " at " & at & " DPI")
                        If yours.Width < list.LogicalToDeviceUnits(150) Then
                            found.Add($"{name} at {at} DPI, at its least width: 'Yours' is {yours.Width} pixels of the {list.LogicalToDeviceUnits(150)} it was built with, in a list that scrolls sideways: {HasStyle(list, WS_HSCROLL)}.")
                        End If
                    End Sub))

        Next

        Assert.AreEqual(0, failures.Count, Environment.NewLine & String.Join(Environment.NewLine, failures))
        Assert.AreEqual(0, found.Count, Environment.NewLine & String.Join(Environment.NewLine, found))
        Assert.AreEqual(2 * Dpis.Length, judged.Count, "Both lists were judged at each DPI: " & String.Join("; ", judged))
        Assert.AreEqual(Dpis.Length, narrowed.Count, "The journals' list was narrowed at each DPI: " & String.Join("; ", narrowed))

    End Sub


    ' The list's columns as LogicalToDeviceUnits gives them at the DPI,
    ' then the window simulated for the display. Windows Forms scales the
    ' window; it does not scale a list's columns, which the app sizes by
    ' hand.
    Private Shared Sub AtTheDisplaysSize(shown As Form, list As ListView, dpi As Integer)

        For Each column As ColumnHeader In list.Columns
            column.Width = column.Width * dpi \ 96
        Next

        ScalingMeasurementTests.Simulate(shown, dpi, True)

    End Sub


    ' A list whose columns fit, but for what its last one can give up down
    ' to its caption, has no horizontal bar.
    Private Shared Sub JudgeSidewaysBar(name As String, list As ListView, judged As List(Of String), found As List(Of String))

        Dim last As ColumnHeader = list.Columns(list.Columns.Count - 1)
        Dim others As Integer = list.Columns.Cast(Of ColumnHeader)().Where(Function(column) column IsNot last).Sum(Function(column) column.Width)
        Dim caption As Integer = TextRenderer.MeasureText(last.Text, list.Font).Width
        Dim line As String = $"{name}: {others} + {last.Width} pixels of columns in a list {list.ClientSize.Width} wide"

        If others + caption > list.ClientSize.Width Then
            Return
        End If

        judged.Add(line)

        If HasStyle(list, WS_HSCROLL) Then
            Dim info As New ScrollInfo With {.Size = Marshal.SizeOf(Of ScrollInfo)(), .Mask = &H17}
            GetScrollInfo(list.Handle, 0, info)
            found.Add(line & $" scroll sideways (range {info.Min} to {info.Max}, page {info.Page}, vertical bar {HasStyle(list, WS_VSCROLL)}).")
        End If

    End Sub


    ' ---------------------------------------------------------------
    ' Labels on their field's line (items 3 and 6)
    ' ---------------------------------------------------------------

    ' A field's label shares the field's centre line (#258, items 3 and 6).
    ' At 125% the packet file's "Local file" sat about 30 pixels below its
    ' box, its row as tall as its panel's default height; the version's
    ' Label, Version file and Current version were 11 or 12 pixels off,
    ' centred in rows the field sat at the top of, and the correspondence,
    ' submission and manuscript windows' labels 8 to 10 pixels below their
    ' fields. The middle of each label's first line of text is within 2
    ' pixels of its field's middle: the first box, list or date in the
    ' field's cell, else its first check box, else its first button, each
    ' of which centres its text. The text is drawn as the app draws it.
    <TestMethod>
    Public Sub FieldLabelsShareTheirFieldsCentreLine()

        Dim rows As New Dictionary(Of String, String()) From {
            {"AddCorrespondenceForm", {"Type", "Date", "Title", "Local file", "Source URL"}},
            {"AddSubmissionForm", {"Journal", "Journal manuscript ID", "Submitted", "Publisher portal URL", "Follow-up"}},
            {"AddManuscriptForm", {"Title", "Target journal", "Starting stage", "From a draft", "First deadline"}},
            {"ManuscriptVersionEditForm", {"Label", "Version file", "Current version"}},
            {"SubmissionPacketFileEditForm, Edit", {"Local file"}},
            {"SubmissionPacketFileEditForm, New", {"Local file"}}
        }

        Dim measured As New List(Of String)()
        Dim found As New List(Of String)()

        Dim failures As List(Of String) =
            AtEachDpi(
                LabelledWindows(),
                Sub(name, shown, dpi)

                    DrawTextAsTheAppDoes(shown)

                    For Each caption As String In rows(name)

                        Dim pair As (Label As Label, Field As Control) = LabelAndField(shown, caption)

                        If pair.Label Is Nothing OrElse pair.Field Is Nothing Then
                            found.Add($"{name}: no label '{caption}' with a field beside it.")
                            Continue For
                        End If

                        Dim labelCentre As Double = FirstLineCentreY(shown, pair.Label)
                        Dim fieldCentre As Double = CentreY(shown, pair.Field)
                        Dim line As String = $"{name} at {dpi} DPI: '{caption}' has its text's middle at {labelCentre}, its {pair.Field.GetType().Name} at {fieldCentre}"

                        measured.Add(line)

                        If Math.Abs(labelCentre - fieldCentre) > 2 Then
                            found.Add(line)
                        End If

                    Next

                End Sub)

        Assert.AreEqual(0, failures.Count, Environment.NewLine & String.Join(Environment.NewLine, failures))
        Assert.AreEqual(0, found.Count, Environment.NewLine & String.Join(Environment.NewLine, found))
        Assert.AreEqual(rows.Values.Sum(Function(captions) captions.Length) * Dpis.Length, measured.Count, "Every row was measured at each DPI.")

    End Sub


    ' A label is whole in a window built as a display builds it (#263).
    ' Windows Forms does not scale Add Manuscript, so its label column kept
    ' 145 pixels and its first rows 55 while its text grew: at 150% Target
    ' journal and Starting stage wrapped onto a second line, which their
    ' rows, with each label at its row's top (#258), cut. Each window
    ' LabelledWindows names is built as at each display
    ' (ScalingMeasurementTests.AtDisplayDpi) and draws its text as the app
    ' does, with GDI, which measures it at that display's DPI. Every label
    ' it shows has the width and height its text needs at its width, inside
    ' its parent, and so does the help beside a label. On 88a191e Add
    ' Manuscript's two labels were cut at 144 DPI, 48 pixels tall of the 56
    ' they need, and Record Journal Submission's help beside Journal
    ' manuscript ID showed 3 of its 19 pixels, as a real 150% display
    ' showed it. The sweep only reports such a window above 96 DPI, and
    ' does not judge help.
    <TestMethod>
    Public Sub LabelsAreWholeInAWindowBuiltAt96To144Dpi()

        Dim judged As New HashSet(Of String)(StringComparer.Ordinal)
        Dim found As New List(Of String)()
        Dim failures As New List(Of String)()

        For Each dpi As Integer In Dpis

            Dim at As Integer = dpi

            ScalingMeasurementTests.AtDisplayDpi(
                at,
                Sub()
                    failures.AddRange(
                        ViewCatalog.ShowEach(
                            LabelledWindows().Select(Function(view) (view.Name, CType(Function(root As String) view.Build(), Func(Of String, Form)))),
                            Sub(name, shown)

                                DrawTextAsTheAppDoes(shown)

                                For Each label As Label In ViewCatalog.Descendants(shown).OfType(Of Label)().Where(Function(item) item.Visible AndAlso item.Text.Length > 0)

                                    Dim needs As Size = label.GetPreferredSize(New Size(label.Width, 0))

                                    judged.Add(name & " at " & at & " DPI")

                                    If needs.Width > label.Width OrElse needs.Height > label.Height OrElse Not Holds(label.Parent, label) Then
                                        found.Add($"{name} at {at} DPI: '{label.Text}' is {label.Width}x{label.Height} at {label.Location} in {label.Parent.ClientSize}; its text needs {needs.Width}x{needs.Height}.")
                                    End If

                                Next

                                For Each help As ContextHelpControl In ViewCatalog.Descendants(shown).OfType(Of ContextHelpControl)().Where(Function(item) item.Visible AndAlso Not Holds(item.Parent, item))
                                    found.Add($"{name} at {at} DPI: '{help.AccessibleName}' is at {help.Bounds} in {help.Parent.ClientSize}.")
                                Next

                            End Sub))
                End Sub)

        Next

        Assert.AreEqual(0, failures.Count, Environment.NewLine & String.Join(Environment.NewLine, failures))
        Assert.AreEqual(0, found.Count, Environment.NewLine & String.Join(Environment.NewLine, found))
        Assert.AreEqual(LabelledWindows().Length * Dpis.Length, judged.Count, "Each window's labels were judged at each DPI.")

    End Sub


    ' Whether the parent shows all of the control, or brings it into view
    ' by scrolling.
    Private Shared Function Holds(parent As Control, control As Control) As Boolean
        Return Rectangle.Union(parent.ClientRectangle, parent.DisplayRectangle).Contains(control.Bounds)
    End Function


    ' The windows whose field labels #258 put on their fields' line.
    Private Shared Function LabelledWindows() As (Name As String, View As Type, Build As Func(Of Form))()

        Return {
            ("AddCorrespondenceForm", GetType(AddCorrespondenceForm), Function() CType(New AddCorrespondenceForm(), Form)),
            ("AddSubmissionForm", GetType(AddSubmissionForm), Function() CType(New AddSubmissionForm(), Form)),
            ("AddManuscriptForm", GetType(AddManuscriptForm), Function() CType(New AddManuscriptForm(), Form)),
            ("ManuscriptVersionEditForm", GetType(ManuscriptVersionEditForm), Function() ViewCatalog.Dialogs(GetType(ManuscriptVersionEditForm))(String.Empty)),
            ("SubmissionPacketFileEditForm, Edit", GetType(SubmissionPacketFileEditForm), Function() ViewCatalog.Dialogs(GetType(SubmissionPacketFileEditForm))(String.Empty)),
            ("SubmissionPacketFileEditForm, New", GetType(SubmissionPacketFileEditForm), Function() CType(New SubmissionPacketFileEditForm(LongNames().SubmissionPackets(0), Nothing), Form))
        }

    End Function


    ' The app draws text with GDI; a test host starts with GDI+, which
    ' sizes a label or a check box a few pixels taller.
    Private Shared Sub DrawTextAsTheAppDoes(shown As Form)

        For Each control As Control In ViewCatalog.Descendants(shown).Where(Function(item) TypeOf item Is Label OrElse TypeOf item Is ButtonBase)
            If TypeOf control Is Label Then
                DirectCast(control, Label).UseCompatibleTextRendering = False
            Else
                DirectCast(control, ButtonBase).UseCompatibleTextRendering = False
            End If
        Next

        shown.PerformLayout()
        Application.DoEvents()

    End Sub


    ' The label with the caption, alone or beside its help, in the first
    ' column of a table, and the field in the next column of its row.
    Private Shared Function LabelAndField(shown As Form, caption As String) As (Label As Label, Field As Control)

        For Each label As Label In ViewCatalog.Descendants(shown).OfType(Of Label)().Where(Function(item) item.Visible AndAlso item.Text = caption)

            ' A label with help sits in a row of its own in the table's cell.
            Dim cell As Control = If(TypeOf label.Parent Is FlowLayoutPanel AndAlso TypeOf label.Parent.Parent Is TableLayoutPanel, label.Parent, label)
            Dim table As TableLayoutPanel = TryCast(cell.Parent, TableLayoutPanel)

            If table Is Nothing Then
                Continue For
            End If

            Dim position As TableLayoutPanelCellPosition = table.GetPositionFromControl(cell)
            Dim beside As Control = table.GetControlFromPosition(position.Column + 1, position.Row)

            If beside Is Nothing Then
                Continue For
            End If

            Dim candidates As List(Of Control) = New Control() {beside}.Concat(ViewCatalog.Descendants(beside)).Where(Function(item) item.Visible).ToList()

            Dim field As Control =
                If(candidates.FirstOrDefault(Function(item) TypeOf item Is TextBoxBase OrElse TypeOf item Is ComboBox OrElse TypeOf item Is DateTimePicker OrElse TypeOf item Is NumericUpDown),
                   If(candidates.FirstOrDefault(Function(item) TypeOf item Is CheckBox),
                      candidates.FirstOrDefault(Function(item) TypeOf item Is Button)))

            Return (label, field)

        Next

        Return (Nothing, Nothing)

    End Function


    Private Shared Function CentreY(shown As Form, control As Control) As Double
        Dim bounds As Rectangle = shown.RectangleToClient(control.Parent.RectangleToScreen(control.Bounds))
        Return bounds.Top + bounds.Height / 2.0
    End Function


    ' A label draws its text from the top of its box, inside its padding:
    ' the middle of its first line, a label of more lines included.
    Private Shared Function FirstLineCentreY(shown As Form, label As Label) As Double
        Dim bounds As Rectangle = shown.RectangleToClient(label.Parent.RectangleToScreen(label.Bounds))
        Return bounds.Top + label.Padding.Top + TextRenderer.MeasureText("Ag", label.Font).Height / 2.0
    End Function


    ' ---------------------------------------------------------------
    ' The consent text (item 4)
    ' ---------------------------------------------------------------

    ' Before Sending's heading and footnote wrap at the window's width
    ' (#258, item 4). They are given a width to wrap at as the window
    ' resizes, but it was read from the table they sit in before the table
    ' was laid out to the new size: at 125% the window is scaled with its
    ' layout held, and they wrapped at about 290 pixels in a 1000-pixel
    ' window. Resized the same way at each DPI, each wraps at the table's
    ' width inside its padding.
    <TestMethod>
    Public Sub AssistantConsentTextWrapsAtTheWindowsWidth()

        Dim found As New List(Of String)()
        Dim judged As Integer = 0

        Dim failures As List(Of String) =
            AtEachDpi(
                {("AssistantConsentForm", GetType(AssistantConsentForm), Function() ViewCatalog.Dialogs(GetType(AssistantConsentForm))(String.Empty))},
                Sub(name, shown, dpi)

                    ' As Windows Forms resizes a window it scales: its layout held.
                    shown.SuspendLayout()
                    shown.Width += UiTheme.Px(200, dpi)
                    shown.ResumeLayout(True)
                    Application.DoEvents()

                    For Each label As Label In ViewCatalog.Descendants(shown).OfType(Of Label)().Where(Function(item) TypeOf item.Parent Is TableLayoutPanel)

                        Dim table As Control = label.Parent
                        Dim width As Integer = table.ClientSize.Width - table.Padding.Horizontal

                        judged += 1

                        If label.MaximumSize.Width <> width Then
                            found.Add($"{name} at {dpi} DPI: '{label.Text.Substring(0, 20)}…' wraps at {label.MaximumSize.Width} pixels in a table {width} wide inside its padding.")
                        End If

                    Next

                End Sub)

        Assert.AreEqual(0, failures.Count, Environment.NewLine & String.Join(Environment.NewLine, failures))
        Assert.AreEqual(0, found.Count, Environment.NewLine & String.Join(Environment.NewLine, found))
        Assert.AreEqual(2 * Dpis.Length, judged, "The heading and the footnote were judged at each DPI.")

    End Sub


    ' ---------------------------------------------------------------
    ' Read-only boxes (item 5)
    ' ---------------------------------------------------------------

    ' A read-only box opens with nothing selected (#258, item 5). The
    ' readiness notes' requirement and the diagnostics report took the
    ' keyboard as their windows opened, as a window's first box does, and
    ' a text box given the keyboard for the first time selects all its
    ' text unless a selection was set. Each, given the keyboard, selects
    ' nothing. AssistantConsentForm already set one.
    <TestMethod>
    Public Sub ReadinessItemNotesAndDiagnosticsOpenWithTheirReadOnlyBoxUnselected()

        Dim found As New List(Of String)()
        Dim judged As Integer = 0

        Dim failures As List(Of String) =
            ViewCatalog.ShowEach(
                {GetType(ReadinessItemNotesForm), GetType(DiagnosticsForm)}.Select(Function(view) (view.Name, ViewCatalog.Dialogs(view))),
                Sub(name, shown)

                    Dim box As TextBox = ViewCatalog.Descendants(shown).OfType(Of TextBox)().First(Function(item) item.ReadOnly AndAlso item.Visible)

                    Assert.IsTrue(box.Focus(), name & ": the box takes the keyboard.")
                    Application.DoEvents()

                    judged += 1

                    If box.SelectionLength <> 0 Then
                        found.Add($"{name}: '{box.AccessibleName}' opens with {box.SelectionLength} of its {box.TextLength} characters selected.")
                    End If

                End Sub)

        Assert.AreEqual(0, failures.Count, Environment.NewLine & String.Join(Environment.NewLine, failures))
        Assert.AreEqual(0, found.Count, Environment.NewLine & String.Join(Environment.NewLine, found))
        Assert.AreEqual(2, judged, "Both boxes were judged.")

    End Sub


    ' ---------------------------------------------------------------
    ' Bullets (item 7)
    ' ---------------------------------------------------------------

    ' Mark as Published's bullets hang their wrapped lines (#258, item 7).
    ' Each was one label, "•  " and the line, so a wrapped line started
    ' under the bullet. Each line is a label of its own, to the right of
    ' its bullet, so every line of it starts there.
    <TestMethod>
    Public Sub MarkPublishedBulletsHangTheirWrappedLines()

        Dim manuscript As Manuscript = CreateRepresentativeLibrary()(0)
        Dim match As New PublicationMatch With {.Doi = "10.5555/example.active", .Title = "Active Study", .Journal = "Journal of Example Studies", .PublishedDate = New DateTime(2026, 9, 1)}
        Dim today As New DateTime(2026, 10, 27)
        Dim lines As List(Of String) = PublicationMatchService.DescribeMarkPublished(manuscript, match, today).ToList()
        Dim found As New List(Of String)()

        Dim failures As List(Of String) =
            AtEachDpi(
                {("MarkPublishedForm", GetType(MarkPublishedForm), Function() CType(New MarkPublishedForm(manuscript, match, today), Form))},
                Sub(name, shown, dpi)

                    Dim labels As List(Of Label) = ViewCatalog.Descendants(shown).OfType(Of Label)().ToList()

                    For Each line As String In lines

                        Dim text As Label = labels.FirstOrDefault(Function(item) item.Text = line)
                        Dim bullet As Label = If(text Is Nothing, Nothing, labels.FirstOrDefault(Function(item) item.Text = "•" AndAlso item.Parent Is text.Parent AndAlso Math.Abs(item.Top - text.Top) <= item.Height))

                        If text Is Nothing OrElse bullet Is Nothing Then
                            found.Add($"{name} at {dpi} DPI: '{line}' is not a label of its own beside a bullet.")
                        ElseIf text.Left < bullet.Right Then
                            found.Add($"{name} at {dpi} DPI: '{line}' starts at {text.Left}, left of its bullet's edge at {bullet.Right}.")
                        End If

                    Next

                End Sub)

        Assert.AreEqual(0, failures.Count, Environment.NewLine & String.Join(Environment.NewLine, failures))
        Assert.AreEqual(0, found.Count, Environment.NewLine & String.Join(Environment.NewLine, found))
        Assert.IsTrue(lines.Count >= 2, "There are lines to judge.")

    End Sub


    ' ---------------------------------------------------------------
    ' Route View (item 8)
    ' ---------------------------------------------------------------

    ' Route View's help icon keeps a gap before the chip after it (#258,
    ' item 8). CURRENT STATE's "?" touched the CURRENT VERSION chip. Each
    ' help icon is at least 4 pixels (at 96 DPI) from what follows it on its
    ' line.
    '
    ' The chips' captions are a guard, which cannot fail before the change:
    ' C3 saw them sit high at 125%, 6 pixels above and 11 below, in boxes
    ' about 5 pixels taller than their text. Drawn here as the app draws
    ' text, with GDI, each chip is as tall as its caption and its padding,
    ' and its ink is within 2 pixels of the middle at each DPI; the captions
    ' are now centred in their boxes, which changes nothing in a box that
    ' tight, and the taller box C3 saw is left to its capture.
    <TestMethod>
    Public Sub RouteViewHelpIconsKeepAGapAndChipCaptionsSitInTheMiddle()

        Dim chips As String() = {"CURRENT STATE", "CURRENT VERSION"}
        Dim found As New List(Of String)()
        Dim measured As New List(Of String)()
        Dim icons As Integer = 0

        Dim failures As List(Of String) =
            AtEachDpi(
                {("ManuscriptRouteViewForm", GetType(ManuscriptRouteViewForm), Function() ViewCatalog.Dialogs(GetType(ManuscriptRouteViewForm))(String.Empty))},
                Sub(name, shown, dpi)

                    For Each help As ContextHelpControl In ViewCatalog.Descendants(shown).OfType(Of ContextHelpControl)().Where(Function(item) item.Visible AndAlso TypeOf item.Parent Is FlowLayoutPanel)

                        Dim siblings As Control.ControlCollection = help.Parent.Controls
                        Dim index As Integer = siblings.IndexOf(help)
                        Dim [next] As Control = If(index + 1 < siblings.Count, siblings(index + 1), Nothing)

                        If [next] Is Nothing OrElse Not [next].Visible OrElse [next].Top >= help.Bottom Then
                            Continue For
                        End If

                        icons += 1

                        If [next].Left - help.Right < UiTheme.Px(4, dpi) Then
                            found.Add($"{name} at {dpi} DPI: '{help.AccessibleName}' is {[next].Left - help.Right} pixels from '{[next].Text}'.")
                        End If

                    Next

                    For Each chip As Label In ViewCatalog.Descendants(shown).OfType(Of Label)().Where(Function(item) item.Visible AndAlso chips.Contains(item.Text))

                        Dim ink As (Above As Integer, Below As Integer) = InkGaps(chip)
                        Dim line As String = $"{name} at {dpi} DPI: '{chip.Text}', {chip.Height} pixels tall, has {ink.Above} above its caption and {ink.Below} below"

                        measured.Add(line)

                        If Math.Abs(ink.Above - ink.Below) > 2 Then
                            found.Add(line)
                        End If

                    Next

                End Sub)

        Assert.AreEqual(0, failures.Count, Environment.NewLine & String.Join(Environment.NewLine, failures))
        Assert.AreEqual(0, found.Count, Environment.NewLine & String.Join(Environment.NewLine, found))
        Assert.IsTrue(icons >= Dpis.Length, "A help icon with a chip after it was judged at each DPI.")
        Assert.IsTrue(measured.Count >= 2 * Dpis.Length, "Both chips were judged at each DPI: " & String.Join("; ", measured))

    End Sub


    ' The rows of a label's picture above and below its caption's ink: the
    ' pixels that differ clearly from its back color.
    Private Shared Function InkGaps(label As Label) As (Above As Integer, Below As Integer)

        ' The app draws text with GDI; a test host starts with GDI+.
        label.UseCompatibleTextRendering = False

        Using image As New Bitmap(label.Width, label.Height, PixelFormat.Format32bppArgb)

            label.DrawToBitmap(image, New Rectangle(Point.Empty, label.Size))

            Dim back As Color = label.BackColor
            Dim inked As New List(Of Integer)()

            For y As Integer = 0 To image.Height - 1
                For x As Integer = 0 To image.Width - 1
                    Dim pixel As Color = image.GetPixel(x, y)
                    If Math.Abs(CInt(pixel.R) - back.R) + Math.Abs(CInt(pixel.G) - back.G) + Math.Abs(CInt(pixel.B) - back.B) > 96 Then
                        inked.Add(y)
                        Exit For
                    End If
                Next
            Next

            If inked.Count = 0 Then
                Throw New InvalidOperationException("'" & label.Text & "' drew no caption.")
            End If

            Return (inked.Min(), image.Height - 1 - inked.Max())

        End Using

    End Function


    ' ---------------------------------------------------------------
    ' Wording (items 10 and 11)
    ' ---------------------------------------------------------------

    ' The title page's summary names a title it recognized (#258, item 10).
    ' Pasted text whose only recognized part is a title said "Nothing
    ' recognized yet. You can still type a title." while Title showed it.
    ' The summary now begins with the title; with nothing recognized it
    ' still says so.
    <TestMethod>
    Public Sub TitlePageImportSummaryNamesARecognizedTitle()

        StaRunner.Run(
            Sub()

                Using dialog As New TitlePageImportForm(New AuthorLibraryData(), "A preregistered replication of anchoring effects")
                    Assert.AreEqual("title", SummaryOf(dialog), "Only a title was recognized.")
                End Using

                Dim page As String = String.Join(vbLf, {
                    "A preregistered replication of anchoring effects in clinical risk estimates",
                    "",
                    "Dana Whitfield1, Rui Okafor1,2*, and Lena Marsh2",
                    "",
                    "1 Department of Psychology, Blue Mountain University",
                    "2 School of Nursing, Cascade State University"
                })

                Using dialog As New TitlePageImportForm(New AuthorLibraryData(), page)
                    StringAssert.StartsWith(SummaryOf(dialog), "title " & ChrW(&HB7) & " 3 authors", "The title comes first.")
                End Using

                Using dialog As New TitlePageImportForm(New AuthorLibraryData())
                    Assert.AreEqual("Paste text to see what PaperRoute finds.", SummaryOf(dialog), "Before any text, as before.")
                End Using

            End Sub)

    End Sub


    Private Shared Function SummaryOf(dialog As TitlePageImportForm) As String
        Return DirectCast(GetType(TitlePageImportForm).GetField("lblSummary", BindingFlags.Instance Or BindingFlags.NonPublic).GetValue(dialog), Label).Text
    End Function


    ' The reviewer response matrix says once that it has nothing to show
    ' (#258, item 11). With no comments, the list's hint said "No reviewer
    ' comments yet. Choose Add Comment... to record the first request." and
    ' the details box beside it "No reviewer comments yet. Choose Add
    ' Comment to begin."; so too with no decision and with a status that
    ' has no comments. The list's hint is the one sentence, and the details
    ' box, with no comment selected, is empty. Behind a status filter the
    ' hint still says to choose All statuses, as the details box did.
    <TestMethod>
    Public Sub ReviewerResponseMatrixSaysOnceThatItHasNothingToShow()

        Dim found As New List(Of String)()

        Dim noDecision As Func(Of String, Form) =
            Function(root)
                Dim manuscript As Manuscript = CreateRepresentativeLibrary()(0)
                manuscript.Submissions(0).Decisions.Clear()
                Return New ReviewerResponseMatrixForm(manuscript, manuscript.Submissions(0))
            End Function

        Dim filtered As Func(Of String, Form) =
            Function(root)
                Dim manuscript As Manuscript = CreateRepresentativeLibrary()(0)
                Dim submission As JournalSubmission = manuscript.Submissions(0)
                ReviewerResponseService.AddItem(submission, New ReviewerResponseItem With {.DecisionId = submission.Decisions(0).Id, .RevisionRoundNumber = 1, .ReviewerLabel = "Reviewer 1", .CommentText = "Clarify the sample.", .Status = ReviewerResponseStatus.Unresolved})
                Return New ReviewerResponseMatrixForm(manuscript, submission)
            End Function

        Dim failures As List(Of String) =
            ViewCatalog.ShowEach(
                {
                    ("With no comments", ViewCatalog.Dialogs(GetType(ReviewerResponseMatrixForm))),
                    ("With no decision", noDecision),
                    ("With no comments of the chosen status", filtered)
                },
                Sub(name, shown)

                    If name = "With no comments of the chosen status" Then
                        Dim filter As ComboBox = ViewCatalog.Descendants(shown).OfType(Of ComboBox)().Single(Function(item) item.AccessibleName = "Filter response status")
                        filter.SelectedIndex = filter.Items.Count - 1
                    End If

                    Application.DoEvents()

                    Dim list As ListBox = ViewCatalog.Descendants(shown).OfType(Of ListBox)().Single(Function(item) item.AccessibleName = "Reviewer comments")
                    Dim details As TextBox = ViewCatalog.Descendants(shown).OfType(Of TextBox)().Single(Function(item) item.AccessibleName = "Selected reviewer comment details")
                    Dim hint As Label = list.Controls.OfType(Of Label)().Single()

                    ' As the hints follow their lists once the app is idle.
                    ManuscriptPipeline.Controls.EmptyHint.Refresh(list)

                    Assert.AreEqual(0, list.Items.Count, name & ": the list is empty.")
                    Assert.IsTrue(hint.Visible AndAlso hint.Text.Length > 0, name & ": the list says why.")

                    If details.TextLength > 0 Then
                        found.Add($"{name}: the list says ""{hint.Text}"" and the details box ""{details.Text}"".")
                    End If

                    ' The one sentence keeps the way back to every comment
                    ' that the details box gave.
                    If name = "With no comments of the chosen status" AndAlso Not hint.Text.Contains("Choose All statuses") Then
                        found.Add($"{name}: the list says ""{hint.Text}"" and nothing says how to see every comment again.")
                    End If

                End Sub)

        Assert.AreEqual(0, failures.Count, Environment.NewLine & String.Join(Environment.NewLine, failures))
        Assert.AreEqual(0, found.Count, Environment.NewLine & String.Join(Environment.NewLine, found))

    End Sub


    ' ---------------------------------------------------------------
    ' Showing a view at each DPI
    ' ---------------------------------------------------------------

    ' Shows each view at each DPI, simulated for the display as its source
    ' asks (ScalingMeasurementTests.ScalingOf), in the color mode if one is
    ' given, and hands it to inspect; returns a line for each view that
    ' failed.
    Private Shared Function AtEachDpi(
        views As IEnumerable(Of (Name As String, View As Type, Build As Func(Of Form))),
        inspect As Action(Of String, Form, Integer),
        Optional mode As SystemColorMode? = Nothing
    ) As List(Of String)

        Dim failures As New List(Of String)()

        For Each dpi As Integer In Dpis

            Dim at As Integer = dpi

            For Each view In views

                Dim current As (Name As String, View As Type, Build As Func(Of Form)) = view
                Dim build As Func(Of String, Form) = Function(root) current.Build()

                failures.AddRange(
                    ViewCatalog.ShowEach(
                        {(current.Name & " at " & at & " DPI", build)},
                        Sub(name, shown)
                            ScalingMeasurementTests.Simulate(shown, at, ScalingMeasurementTests.ScalingOf(current.View).ScalesWithWinForms)
                            inspect(current.Name, shown, at)
                        End Sub,
                        mode))

            Next

        Next

        Return failures

    End Function


    ' Whether the two are the same, apart from a band of the given width
    ' around the edge.
    Private Shared Function SamePixels(first As Bitmap, second As Bitmap, Optional inset As Integer = 0) As Boolean

        If first.Size <> second.Size Then
            Return False
        End If

        For y As Integer = inset To first.Height - 1 - inset
            For x As Integer = inset To first.Width - 1 - inset
                If first.GetPixel(x, y).ToArgb() <> second.GetPixel(x, y).ToArgb() Then
                    Return False
                End If
            Next
        Next

        Return True

    End Function


    Private Shared Function HasStyle(control As Control, style As Integer) As Boolean
        Return (GetWindowLong(control.Handle, GWL_STYLE) And style) <> 0
    End Function


    Private Const GWL_STYLE As Integer = -16
    Private Const WS_HSCROLL As Integer = &H100000
    Private Const WS_VSCROLL As Integer = &H200000

    <StructLayout(LayoutKind.Sequential)>
    Private Structure NativeRect
        Public Left As Integer
        Public Top As Integer
        Public Right As Integer
        Public Bottom As Integer
    End Structure

    <StructLayout(LayoutKind.Sequential)>
    Private Structure ComboBoxInfo
        Public Size As Integer
        Public Item As NativeRect
        Public Button As NativeRect
        Public ButtonState As Integer
        Public Combo As IntPtr
        Public Edit As IntPtr
        Public List As IntPtr
    End Structure

    <DllImport("user32.dll", EntryPoint:="GetWindowLongW")>
    Private Shared Function GetWindowLong(handle As IntPtr, index As Integer) As Integer
    End Function

    <DllImport("user32.dll")>
    Private Shared Function GetComboBoxInfo(handle As IntPtr, ByRef info As ComboBoxInfo) As Boolean
    End Function

    <StructLayout(LayoutKind.Sequential)>
    Private Structure ScrollInfo
        Public Size As Integer
        Public Mask As Integer
        Public Min As Integer
        Public Max As Integer
        Public Page As Integer
        Public Pos As Integer
        Public TrackPos As Integer
    End Structure

    <DllImport("user32.dll")>
    Private Shared Function GetScrollInfo(handle As IntPtr, bar As Integer, ByRef info As ScrollInfo) As Boolean
    End Function

End Class
