Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.Linq
Imports System.Windows.Forms

Namespace Services

    ' One scaling rule for a dialog (#93). Its sizes are written at 96 DPI
    ' and Windows Forms scales them for the display when the window opens:
    ' Begin before the first control, Finish after the last. A value Windows
    ' Forms scales (a size, a location, a margin, a padding, a minimum, a
    ' table's absolute column or row) is written as its 96-DPI literal and
    ' never scaled by hand as well, or it is scaled twice. Three things are
    ' scaled from Dpi: what is drawn by hand, what is set after the window
    ' has opened, and what is set while it is built on a property Windows
    ' Forms does not scale with the window, such as a list view's column
    ' widths: Edit Journal's facts columns are written
    ' UiTheme.Px(n, DialogScaling.Dpi(lvFacts)), as a column of 200 stays
    ' 200 when its window is scaled.
    Public NotInheritable Class DialogScaling

        Private Sub New()
        End Sub


        ' The 96-DPI base dimensions and the DPI scaling mode, with layout
        ' held until Finish, as the designer writes a form.
        Public Shared Sub Begin(
            dialog As Form
        )

            If dialog Is Nothing Then
                Throw New ArgumentNullException(NameOf(dialog))
            End If

            dialog.SuspendLayout()
            dialog.AutoScaleDimensions = New SizeF(96.0F, 96.0F)
            dialog.AutoScaleMode = AutoScaleMode.Dpi

        End Sub


        ' After the last control: the buttons sized to their captions held
        ' at the sizes they were given (HoldAtGivenSize), one layout pass,
        ' and the window kept inside its screen's working area when it
        ' opens, so its buttons are reachable on a small or a 150% display.
        Public Shared Sub Finish(
            dialog As Form
        )

            If dialog Is Nothing Then
                Throw New ArgumentNullException(NameOf(dialog))
            End If

            For Each button As Button In TextSizedButtons(dialog).ToList()
                HoldAtGivenSize(button)
            Next

            dialog.ResumeLayout(False)
            dialog.PerformLayout()

            AddHandler dialog.Load,
                AddressOf FitOnLoad

        End Sub


        Private Shared Sub FitOnLoad(
            sender As Object,
            e As EventArgs
        )

            ResponsiveDialogSizingService.FitToWorkingArea(
                DirectCast(sender, Form)
            )

        End Sub


        ' The buttons that size themselves to their captions and only grow,
        ' as a Button does unless told otherwise.
        Private Shared Iterator Function TextSizedButtons(
            parent As Control
        ) As IEnumerable(Of Button)

            For Each child As Control In parent.Controls

                Dim button As Button = TryCast(child, Button)

                If button IsNot Nothing AndAlso
                   button.AutoSize AndAlso
                   button.AutoSizeMode = AutoSizeMode.GrowOnly Then

                    Yield button

                End If

                For Each inner As Button In TextSizedButtons(child)
                    Yield inner
                Next

            Next

        End Function


        ' The dialog is built with its text at the display's size, so a
        ' button that sizes itself to its caption has already grown to the
        ' caption there, and one that only grows would keep that width
        ' when Windows Forms scales the dialog: at 150% it was 1.5 times
        ' its caption (#93). Turning AutoSize off gives it back the size it
        ' was given, written at 96 DPI (its default 75x23 if none); that
        ' becomes its minimum, which the scale scales, and it grows and
        ' shrinks from there, so it ends at the larger of that minimum and
        ' its caption. A minimum it already had, such as one shared to give
        ' buttons one width, is kept. Its parent waits meanwhile, so the
        ' size read is the one it was given, and is not laid out after:
        ' that layout reached the panels above it, which have no size until
        ' the window lays out, and at 125% About's logo, squeezed by it, was
        ' scaled to a 1-pixel line (#255). The window lays it out once
        ' scaled.
        Private Shared Sub HoldAtGivenSize(
            button As Button
        )

            Dim parent As Control =
                button.Parent

            parent.SuspendLayout()

            button.AutoSize = False

            button.MinimumSize =
                New Size(
                    Math.Max(button.MinimumSize.Width, button.Width),
                    Math.Max(button.MinimumSize.Height, button.Height)
                )

            button.AutoSizeMode = AutoSizeMode.GrowAndShrink
            button.AutoSize = True

            parent.ResumeLayout(False)

        End Sub


        ' The DPI a control is shown at, for the three things above.
        Public Shared Function Dpi(
            control As Control
        ) As Integer

            If control Is Nothing Then
                Throw New ArgumentNullException(NameOf(control))
            End If

            Return control.DeviceDpi

        End Function


        ' Lets a checked list scroll sideways to the end of its widest row,
        ' as wide as the list draws it: the check box and the gaps beside it
        ' at the list's DPI, then the text with TextRenderer's padding
        ' (#313). Windows Forms' own extent counts a 13-pixel check box until
        ' the list has drawn one, 20 pixels at 150%, and measures "&" as a
        ' mnemonic, so it stopped short of a row's end: a pixel short of C3's
        ' affiliation at a real 150%, and 8 or more short of one with an "&"
        ' at any DPI where it scrolls. Call it once the rows are in, and
        ' again when the list's font changes.
        Public Shared Sub FitCheckedListExtent(
            list As CheckedListBox
        )

            If list Is Nothing Then
                Throw New ArgumentNullException(NameOf(list))
            End If

            Dim listDpi As Integer =
                Dpi(list)

            Dim gap As Integer =
                CInt(Math.Round(listDpi / 96.0))

            Dim glyph As Integer =
                CInt(Math.Round(13 * listDpi / 96.0))

            Dim widest As Integer = 0

            For Each item As Object In list.Items

                widest =
                    Math.Max(
                        widest,
                        TextRenderer.MeasureText(
                            list.GetItemText(item),
                            list.Font,
                            New Size(Integer.MaxValue, Integer.MaxValue),
                            TextFormatFlags.NoPrefix Or TextFormatFlags.ExpandTabs
                        ).Width
                    )

            Next

            list.HorizontalScrollbar = True

            ' Never 0, which hands the extent back to Windows Forms.
            list.HorizontalExtent =
                Math.Max(1, glyph + 2 * gap + widest)

        End Sub

    End Class

End Namespace
