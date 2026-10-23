Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.IO
Imports System.Linq
Imports System.Runtime.InteropServices
Imports System.Windows.Forms
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline.Controls
Imports ManuscriptPipeline.Forms
Imports ManuscriptPipeline.Services

' The windows with underline tabs (#93, step 1). The selected tab is drawn
' in bold, and at 150% Edit Journal's read "Readiness Chec…". The native
' control sizes each tab for its caption in the font it is handed, and
' Windows Forms hands a control that paints itself none when its window is
' made, so the tabs were measured in the stock system font, which does not
' follow the display's text; a later font change handed it the regular
' font. The control now hands it the bold font itself. At 10, 12.5 and 15
' points (the dialogs' font at 100%, 125% and 150%) every tab's bold
' caption, as drawn, is no wider than its tab, and the tab is as wide as
' the bold caption measured as the native control measures (GDI's text
' extent, narrower than the drawn text at small sizes) plus the control's
' side padding, so it is sized for the bold caption and not wide enough by
' chance. In this test host, at 96 DPI, the first check held before the
' change at all three sizes (at 15 points by six pixels); the second held
' at 10 points, where the stock font is the wider, and failed at 12.5 and
' 15 points.
<TestClass>
<DoNotParallelize>
Public Class UnderlineTabControlTests

    ' The five windows that use the control, each built by its catalog
    ' factory.
    Private Shared ReadOnly TabbedWindows As Type() = {
        GetType(AuthorLibraryForm), GetType(JournalEditForm), GetType(OrcidLookupForm), GetType(ReviewerResponseItemForm), GetType(SubmissionDetailsForm)
    }

    Private Shared ReadOnly Points As Single() = {10.0F, 12.5F, 15.0F}

    ' As the control draws its captions, without the ellipsis.
    Private Const CaptionFlags As TextFormatFlags =
        TextFormatFlags.SingleLine Or TextFormatFlags.NoPadding Or TextFormatFlags.HidePrefix


    <TestMethod>
    Public Sub EveryBoldCaptionFitsItsTabAt10To15Points()

        Dim found As New List(Of String)()
        Dim measured As Integer = 0

        Dim failures As List(Of String) =
            ViewCatalog.ShowEach(
                TabbedWindows.Select(Function(window) (window.Name, ViewCatalog.Dialogs(window))),
                Sub(name, shown)

                    Dim controls As List(Of UnderlineTabControl) =
                        ViewCatalog.Descendants(shown).OfType(Of UnderlineTabControl)().ToList()

                    Assert.IsTrue(controls.Count > 0, name & " has underline tabs.")

                    For Each tabs As UnderlineTabControl In controls
                        For Each size As Single In Points

                            tabs.Font = New Font(tabs.Font.FontFamily, size, FontStyle.Regular)
                            Application.DoEvents()

                            Using bold As New Font(tabs.Font, FontStyle.Bold)

                                Dim room As Integer = 2 * UiTheme.Px(UiTheme.SpaceMd, tabs.DeviceDpi)

                                For index As Integer = 0 To tabs.TabCount - 1

                                    measured += 1

                                    Dim caption As String = tabs.TabPages(index).Text
                                    Dim drawn As Integer = TextRenderer.MeasureText(caption, bold, New Size(Integer.MaxValue, Integer.MaxValue), CaptionFlags).Width
                                    Dim native As Integer = TextExtent(caption.Replace("&&", "&"), bold)
                                    Dim tab As Rectangle = tabs.GetTabRect(index)
                                    Dim where As String = $"{name} '{caption}' at {size}pt: bold caption {drawn} as drawn, {native} as the native control measures; tab {tab.Width} wide"

                                    ' Measured here on the screen's device context, GDI's
                                    ' extent is within a pixel of the native control's own.
                                    If drawn > tab.Width Then
                                        found.Add(where & ": the caption is cut.")
                                    ElseIf tab.Width < native + room - 1 Then
                                        found.Add(where & $": not sized for the bold caption with its {room} pixels of padding.")
                                    End If

                                    If tab.Right > tabs.ClientSize.Width Then
                                        found.Add(where & ": the tab runs past the control's right edge at " & tabs.ClientSize.Width & ".")
                                    End If

                                Next

                            End Using

                        Next
                    Next

                End Sub
            )

        Assert.AreEqual(0, failures.Count, Environment.NewLine & String.Join(Environment.NewLine, failures))
        Assert.AreEqual(0, found.Count, Environment.NewLine & String.Join(Environment.NewLine, found))
        Assert.IsTrue(measured >= 5 * 2 * Points.Length, "Every window has at least two tabs at each size: " & measured & " measured.")

    End Sub


    ' The list of five is complete: no other form makes the control.
    <TestMethod>
    Public Sub TheFiveWindowsAreTheOnesThatUseTheControl()

        Dim forms As String =
            Path.Combine(AssistantCoreTests.RepositoryRoot(), "ManuscriptPipeline", "Forms")

        Dim users As List(Of String) =
            Directory.GetFiles(forms, "*.vb").
                Where(Function(source) File.ReadAllText(source).Contains("New UnderlineTabControl", StringComparison.Ordinal)).
                Select(Function(source) Path.GetFileNameWithoutExtension(source)).
                OrderBy(Function(source) source, StringComparer.Ordinal).
                ToList()

        CollectionAssert.AreEqual(
            TabbedWindows.Select(Function(window) window.Name).OrderBy(Function(source) source, StringComparer.Ordinal).ToList(),
            users,
            "The forms that make the control: " & String.Join(", ", users)
        )

    End Sub


    ' The caption's width in GDI's own measure, with which the native tab
    ' control sizes its tabs.
    Private Shared Function TextExtent(text As String, font As Font) As Integer

        Dim dc As IntPtr = GetDC(IntPtr.Zero)
        Dim handle As IntPtr = font.ToHfont()
        Dim previous As IntPtr = SelectObject(dc, handle)

        Try
            Dim size As Size
            GetTextExtentPoint32W(dc, text, text.Length, size)
            Return size.Width
        Finally
            SelectObject(dc, previous)
            DeleteObject(handle)
            ReleaseDC(IntPtr.Zero, dc)
        End Try

    End Function


    <DllImport("user32.dll")>
    Private Shared Function GetDC(window As IntPtr) As IntPtr
    End Function

    <DllImport("user32.dll")>
    Private Shared Function ReleaseDC(window As IntPtr, dc As IntPtr) As Integer
    End Function

    <DllImport("gdi32.dll")>
    Private Shared Function SelectObject(dc As IntPtr, handle As IntPtr) As IntPtr
    End Function

    <DllImport("gdi32.dll")>
    Private Shared Function DeleteObject(handle As IntPtr) As Boolean
    End Function

    <DllImport("gdi32.dll", CharSet:=CharSet.Unicode)>
    Private Shared Function GetTextExtentPoint32W(dc As IntPtr, text As String, length As Integer, ByRef size As Size) As Boolean
    End Function

End Class
