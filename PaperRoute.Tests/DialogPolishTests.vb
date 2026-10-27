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
                {
                    ("SubmissionPacketEditForm, Edit", GetType(SubmissionPacketEditForm), Function() CType(PacketEditor(existing:=True), Form)),
                    ("SubmissionPacketEditForm, New", GetType(SubmissionPacketEditForm), Function() CType(PacketEditor(existing:=False), Form)),
                    ("ManuscriptReadinessForm", GetType(ManuscriptReadinessForm), Function() CType(New ManuscriptReadinessForm(LongNames(), New AuthorLibraryData()), Form))
                },
                Sub(name, shown, dpi)

                    For Each combo As ComboBox In ViewCatalog.Descendants(shown).OfType(Of ComboBox)().Where(Function(item) item.Visible AndAlso item.DropDownStyle = ComboBoxStyle.DropDownList AndAlso item.SelectedIndex >= 0)

                        Dim box As Rectangle = SelectionField(combo)
                        Dim text As String = combo.GetItemText(combo.SelectedItem)

                        If TextRenderer.MeasureText(text, combo.Font, New Size(Integer.MaxValue, Integer.MaxValue), TextFormatFlags.SingleLine Or TextFormatFlags.NoPrefix).Width <= box.Width Then
                            Continue For
                        End If

                        judged.Add(name & ": " & text)

                        If Not DrawsEndingInAnEllipsis(combo, text, box.Size) Then
                            found.Add($"{name} at {dpi} DPI: '{text}' is wider than its {box.Width}-pixel box and is not drawn ending in an ellipsis.")
                        End If

                    Next

                End Sub)

        Assert.AreEqual(0, failures.Count, Environment.NewLine & String.Join(Environment.NewLine, failures))
        Assert.AreEqual(0, found.Count, Environment.NewLine & String.Join(Environment.NewLine, found))
        Assert.IsTrue(judged.Count >= 3, "The packet's version, readiness and submission and the readiness profile are wider than their boxes: " & String.Join("; ", judged))

    End Sub


    ' Whether the combo draws its item, in a box of the given size as
    ' Windows asks it to, with the pixels TextRenderer draws for the text
    ' ending in an ellipsis. A combo Windows draws draws nothing here.
    Private Shared Function DrawsEndingInAnEllipsis(combo As ComboBox, text As String, size As Size) As Boolean

        Dim bounds As New Rectangle(Point.Empty, size)

        Using drawn As New Bitmap(size.Width, size.Height, PixelFormat.Format32bppArgb), expected As New Bitmap(size.Width, size.Height, PixelFormat.Format32bppArgb)

            Using g As Graphics = Graphics.FromImage(drawn)
                g.Clear(combo.BackColor)
                Dim args As New DrawItemEventArgs(g, combo.Font, bounds, combo.SelectedIndex, DrawItemState.ComboBoxEdit, combo.ForeColor, combo.BackColor)
                GetType(ComboBox).GetMethod("OnDrawItem", BindingFlags.Instance Or BindingFlags.NonPublic).Invoke(combo, New Object() {args})
            End Using

            Using g As Graphics = Graphics.FromImage(expected)
                g.Clear(combo.BackColor)
                TextRenderer.DrawText(g, text, combo.Font, bounds, combo.ForeColor, TextFormatFlags.VerticalCenter Or TextFormatFlags.SingleLine Or TextFormatFlags.EndEllipsis Or TextFormatFlags.NoPrefix)
            End Using

            Return SamePixels(drawn, expected)

        End Using

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
    ' other.
    <TestMethod>
    Public Sub LookupAndJournalListsScrollSidewaysOnlyWhenTheirColumnsCannotFit()

        Dim judged As New List(Of String)()
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
                    End Sub))

        Next

        Assert.AreEqual(0, failures.Count, Environment.NewLine & String.Join(Environment.NewLine, failures))
        Assert.AreEqual(0, found.Count, Environment.NewLine & String.Join(Environment.NewLine, found))
        Assert.AreEqual(2 * Dpis.Length, judged.Count, "Both lists were judged at each DPI: " & String.Join("; ", judged))

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
    ' Showing a view at each DPI
    ' ---------------------------------------------------------------

    ' Shows each view at each DPI, simulated for the display as its source
    ' asks (ScalingMeasurementTests.ScalingOf), and hands it to inspect;
    ' returns a line for each view that failed.
    Private Shared Function AtEachDpi(
        views As IEnumerable(Of (Name As String, View As Type, Build As Func(Of Form))),
        inspect As Action(Of String, Form, Integer)
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
                        End Sub))

            Next

        Next

        Return failures

    End Function


    Private Shared Function SamePixels(first As Bitmap, second As Bitmap) As Boolean

        If first.Size <> second.Size Then
            Return False
        End If

        For y As Integer = 0 To first.Height - 1
            For x As Integer = 0 To first.Width - 1
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
