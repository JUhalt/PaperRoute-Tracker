Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.Drawing.Imaging
Imports System.Runtime.InteropServices
Imports System.Text.RegularExpressions
Imports System.Windows.Forms
Imports ManuscriptPipeline.Controls

Namespace Services

    Public NotInheritable Class UiPolish

        Private Shared _installed As Boolean = False

        Private Shared ReadOnly _styledHandles As New HashSet(Of IntPtr)()


        Private Sub New()
        End Sub


        ' =====================================================
        ' Global installation
        ' =====================================================

        Public Shared Sub InstallGlobalDialogStyling()

            If _installed Then
                Return
            End If

            _installed = True

            AddHandler Application.Idle,
                AddressOf StyleOpenDialogs

        End Sub


        Private Shared Sub StyleOpenDialogs(
            sender As Object,
            e As EventArgs
        )

            For Each form As Form In Application.OpenForms

                ' Form1 has its own custom board styling.
                If String.Equals(
                    form.GetType().Name,
                    "Form1",
                    StringComparison.Ordinal
                ) Then

                    Continue For

                End If

                If _styledHandles.Contains(
                    form.Handle
                ) Then

                    Continue For

                End If

                ApplyDialog(
                    form
                )

                _styledHandles.Add(
                    form.Handle
                )

                AddHandler form.FormClosed,
                    AddressOf StyledFormClosed

            Next

        End Sub


        Private Shared Sub StyledFormClosed(
            sender As Object,
            e As FormClosedEventArgs
        )

            Dim form As Form =
                TryCast(
                    sender,
                    Form
                )

            If form Is Nothing Then
                Return
            End If

            _styledHandles.Remove(
                form.Handle
            )

        End Sub


        ' =====================================================
        ' Public styling entry point
        ' =====================================================

        Public Shared Sub ApplyDialog(
            form As Form
        )

            If form Is Nothing Then
                Return
            End If

            form.BackColor =
                UiTheme.BoardBackground()

            form.ForeColor =
                UiTheme.PrimaryText()

            ApplyToControlTree(
                form,
                TryCast(form.AcceptButton, Button)
            )

        End Sub


        ' =====================================================
        ' Recursive control styling
        ' =====================================================

        Private Shared Sub ApplyToControlTree(
            parent As Control,
            primaryButton As Button
        )

            For Each control As Control In parent.Controls

                ApplyControlStyle(
                    control,
                    primaryButton
                )

                If control.HasChildren Then

                    ApplyToControlTree(
                        control,
                        primaryButton
                    )

                End If

            Next

        End Sub


        Private Shared Sub ApplyControlStyle(
            control As Control,
            primaryButton As Button
        )

            ' Theme-painted buttons already follow the tokens.
            If TypeOf control Is ActionButton Then
                Return
            End If

            If TypeOf control Is Button Then

                Dim button As Button =
                    DirectCast(
                        control,
                        Button
                    )

                Dim isPrimary As Boolean =
                    control Is primaryButton

                StyleButton(
                    button,
                    isPrimary
                )

                ' A disabled filled button would show grey text on the accent.
                If isPrimary Then
                    AddHandler button.EnabledChanged,
                        Sub(sender, e)
                            StyleButton(button, button.Enabled)
                        End Sub
                    If Not button.Enabled Then
                        StyleButton(button, False)
                    End If
                End If

                MuteDisabledCaption(button)

                Return

            End If


            If TypeOf control Is TextBox Then

                Dim textBox As TextBox =
                    DirectCast(
                        control,
                        TextBox
                    )

                textBox.BackColor =
                    UiTheme.CardBackground()

                textBox.ForeColor =
                    UiTheme.PrimaryText()

                textBox.BorderStyle =
                    BorderStyle.FixedSingle

                ThemedBorder.Attach(
                    textBox
                )

                Return

            End If


            If TypeOf control Is RichTextBox Then

                Dim richText As RichTextBox =
                    DirectCast(
                        control,
                        RichTextBox
                    )

                richText.BackColor =
                    UiTheme.CardBackground()

                richText.ForeColor =
                    UiTheme.PrimaryText()

                richText.BorderStyle =
                    BorderStyle.FixedSingle

                ThemedBorder.Attach(
                    richText
                )

                Return

            End If


            If TypeOf control Is ListBox Then

                Dim listBox As ListBox =
                    DirectCast(
                        control,
                        ListBox
                    )

                listBox.BackColor =
                    UiTheme.CardBackground()

                listBox.ForeColor =
                    UiTheme.PrimaryText()

                listBox.BorderStyle =
                    BorderStyle.FixedSingle

                ThemedBorder.Attach(
                    listBox
                )

                Return

            End If


            If TypeOf control Is ComboBox Then

                Dim comboBox As ComboBox =
                    DirectCast(
                        control,
                        ComboBox
                    )

                comboBox.BackColor =
                    UiTheme.CardBackground()

                comboBox.ForeColor =
                    UiTheme.PrimaryText()

                comboBox.FlatStyle =
                    FlatStyle.Flat

                Return

            End If


            If TypeOf control Is NumericUpDown Then

                Dim numeric As NumericUpDown =
                    DirectCast(
                        control,
                        NumericUpDown
                    )

                numeric.BackColor =
                    UiTheme.CardBackground()

                numeric.ForeColor =
                    UiTheme.PrimaryText()

                Return

            End If


            If TypeOf control Is DateTimePicker Then

                Dim picker As DateTimePicker =
                    DirectCast(
                        control,
                        DateTimePicker
                    )

                picker.CalendarMonthBackground =
                    UiTheme.CardBackground()

                picker.CalendarForeColor =
                    UiTheme.PrimaryText()

                Return

            End If


            If TypeOf control Is DataGridView Then

                Dim grid As DataGridView =
                    DirectCast(
                        control,
                        DataGridView
                    )

                grid.EnableHeadersVisualStyles = False
                grid.BackgroundColor = UiTheme.CardBackground()
                grid.GridColor = UiTheme.CardBorder()
                grid.DefaultCellStyle.BackColor = UiTheme.CardBackground()
                grid.DefaultCellStyle.ForeColor = UiTheme.PrimaryText()
                grid.DefaultCellStyle.SelectionBackColor = UiTheme.HoverBackground()
                grid.DefaultCellStyle.SelectionForeColor = UiTheme.PrimaryText()
                grid.ColumnHeadersDefaultCellStyle.BackColor = UiTheme.HeaderBackground()
                grid.ColumnHeadersDefaultCellStyle.ForeColor = UiTheme.PrimaryText()
                ' A header never looks selected because a cell below it is.
                grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = UiTheme.HeaderBackground()
                grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = UiTheme.PrimaryText()
                ' A header never looks selected because a cell below it is.
                grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = UiTheme.HeaderBackground()
                grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = UiTheme.PrimaryText()

                Return

            End If


            If TypeOf control Is CheckBox OrElse
               TypeOf control Is RadioButton Then

                control.ForeColor = UiTheme.PrimaryText()
                control.BackColor = SectionCard.SurfaceBehind(control)
                ShowLiteralAmpersands(DirectCast(control, ButtonBase))
                MuteDisabledCaption(DirectCast(control, ButtonBase))

                Return

            End If


            If TypeOf control Is LinkLabel Then

                Dim link As LinkLabel =
                    DirectCast(
                        control,
                        LinkLabel
                    )

                link.LinkColor = UiTheme.AccentColor()
                link.ActiveLinkColor = UiTheme.AccentSecondaryColor()
                link.VisitedLinkColor = UiTheme.AccentColor()

                Return

            End If


            ' Sections are cards; what they contain sits on the card.
            If TypeOf control Is SectionCard Then

                control.BackColor =
                    UiTheme.CardBackground()

                control.ForeColor =
                    UiTheme.PrimaryText()

                Return

            End If


            If TypeOf control Is GroupBox Then

                control.BackColor =
                    SectionCard.SurfaceBehind(control)

                control.ForeColor =
                    UiTheme.PrimaryText()

                Return

            End If


            ' A tab page continues the surface around its tabs.
            If TypeOf control Is TabPage Then

                Dim page As TabPage =
                    DirectCast(
                        control,
                        TabPage
                    )

                page.UseVisualStyleBackColor =
                    False

                page.BackColor =
                    If(page.Parent Is Nothing,
                       UiTheme.BoardBackground(),
                       SectionCard.SurfaceBehind(page.Parent))

                page.ForeColor =
                    UiTheme.PrimaryText()

                Return

            End If


            If TypeOf control Is TableLayoutPanel OrElse
               TypeOf control Is FlowLayoutPanel Then

                control.BackColor =
                    SectionCard.SurfaceBehind(control)

                Return

            End If


            If TypeOf control Is Label Then

                Dim label As Label =
                    DirectCast(
                        control,
                        Label
                    )

                If label.UseMnemonic AndAlso Not UsesAcceleratorSyntax(label.Text) Then
                    label.UseMnemonic = False
                End If

                If label.ForeColor =
                    SystemColors.GrayText Then

                    label.ForeColor =
                        UiTheme.SecondaryText()

                ElseIf label.ForeColor =
                       SystemColors.ControlText OrElse
                       label.ForeColor =
                       Color.Black OrElse
                       label.ForeColor =
                       Color.White Then

                    label.ForeColor =
                        UiTheme.PrimaryText()

                End If

            End If

        End Sub


        ' =====================================================
        ' Literal ampersands (#71)
        ' =====================================================

        ' A label or option shows "&" literally unless its text uses
        ' accelerator syntax ("&s" or "&&"). Names filled in later, such as
        ' "Memory & Cognition", keep their ampersand, while deliberate
        ' accelerators such as "Show &status" keep working.
        Private Shared ReadOnly AcceleratorSyntax As New Regex("&(&|[\p{L}\p{N}])")

        Friend Shared Function UsesAcceleratorSyntax(text As String) As Boolean
            Return Not String.IsNullOrEmpty(text) AndAlso AcceleratorSyntax.IsMatch(text)
        End Function

        Private Shared Sub ShowLiteralAmpersands(button As ButtonBase)
            If button.UseMnemonic AndAlso Not UsesAcceleratorSyntax(button.Text) Then
                button.UseMnemonic = False
            End If
        End Sub


        ' =====================================================
        ' Buttons
        ' =====================================================

        ' One filled primary action per dialog (its default button), red text
        ' for destructive actions, and quiet neutral outlines for the rest.
        Private Shared Sub StyleButton(
            button As Button,
            isPrimary As Boolean
        )

            button.FlatStyle =
                FlatStyle.Flat

            button.UseVisualStyleBackColor =
                False

            button.Cursor =
                Cursors.Hand

            button.FlatAppearance.BorderSize =
                1

            Dim text As String =
                button.Text.Trim().ToUpperInvariant()

            If isPrimary Then

                Dim accent As Color =
                    UiTheme.AccentColor()

                Dim shade As Color =
                    If(UiTheme.IsDark(), Color.White, Color.Black)

                button.BackColor = accent
                button.ForeColor = UiTheme.OnAccentText()
                button.FlatAppearance.BorderColor = accent
                button.FlatAppearance.MouseOverBackColor = UiTheme.Blend(accent, shade, 0.1F)
                button.FlatAppearance.MouseDownBackColor = UiTheme.Blend(accent, shade, 0.2F)

                Return

            End If

            button.BackColor =
                UiTheme.CardBackground()

            ' WinForms' dark buttons draw an enabled caption in this grey
            ' when no text color is set. Setting the same grey changes
            ' nothing while the button is enabled, and makes WinForms darken
            ' its caption when disabled as it does every other one, so that
            ' it is muted with them (see Disabled captions). High contrast
            ' has no dark buttons and keeps the system's text color.
            Dim plainText As Color =
                If(
                    Application.IsDarkModeEnabled,
                    Color.FromArgb(240, 240, 240),
                    UiTheme.PrimaryText()
                )

            button.ForeColor =
                If(
                    text.Contains("DELETE") OrElse text.Contains("REMOVE"),
                    UiTheme.DangerColor(),
                    plainText
                )

            button.FlatAppearance.BorderColor =
                UiTheme.CardBorder()

            button.FlatAppearance.MouseOverBackColor =
                UiTheme.HoverBackground()

            button.FlatAppearance.MouseDownBackColor =
                UiTheme.HoverBackground()

        End Sub


        ' =====================================================
        ' Disabled captions
        ' =====================================================

        ' WinForms draws the caption of a disabled check box, option, or
        ' button in a darkened copy of the control's own back color: a
        ' readable grey on a light surface, near-black on a dark one. In
        ' Dark, once the control has painted, its caption is colored again
        ' in the muted text color, pixel for pixel, so it stays exactly
        ' where the control put it. In Light nothing is done.

        ' Set while a control paints into a picture to find its caption.
        <ThreadStatic>
        Private Shared _readingCaption As Boolean

        Private Shared Sub MuteDisabledCaption(button As ButtonBase)

            ' A dialog is styled more than once; one handler is enough.
            RemoveHandler button.Paint,
                AddressOf PaintMutedCaption

            AddHandler button.Paint,
                AddressOf PaintMutedCaption

        End Sub


        ' Friend so a test can paint a control with and without it.
        Friend Shared Sub PaintMutedCaption(
            sender As Object,
            e As PaintEventArgs
        )

            Dim button As ButtonBase =
                TryCast(
                    sender,
                    ButtonBase
                )

            If button Is Nothing OrElse
               button.Enabled OrElse
               _readingCaption OrElse
               Not UiTheme.IsDark() Then

                Return

            End If

            Try

                Dim surface As Color = button.BackColor
                Dim bounds As New Rectangle(Point.Empty, button.Size)

                ' A see-through control has no color to tell a caption from.
                If surface.A < 255 Then
                    Return
                End If

                Using picture As New Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb)

                    _readingCaption = True
                    Try
                        button.DrawToBitmap(picture, bounds)
                    Finally
                        _readingCaption = False
                    End Try

                    ' A button's rounded frame blends into the color behind
                    ' it, which can be as dark as a caption: it is left alone.
                    Dim within As Rectangle = bounds

                    If TypeOf button Is Button Then
                        Dim frame As Integer = UiTheme.Px(UiTheme.SpaceXs, button.DeviceDpi)
                        within.Inflate(-frame, -frame)

                        ' WinForms fills a button in a grey of its own when
                        ' the button's back color is also its parent's, as
                        ' on a card, yet still darkens the caption from the
                        ' back color: the surface is what fills the button.
                        surface = CommonestColor(picture, within)
                    End If

                    KeepMutedCaption(picture, within, ControlPaint.Dark(button.BackColor), surface, UiTheme.MutedText())

                    e.Graphics.DrawImage(picture, bounds)

                End Using

            Catch ex As Exception
                ' The caption stays as WinForms drew it; a paint never fails.
            End Try

        End Sub


        ' The color of most pixels in an area of a picture.
        Private Shared Function CommonestColor(
            picture As Bitmap,
            within As Rectangle
        ) As Color

            Dim area As New Rectangle(Point.Empty, picture.Size)
            Dim data As BitmapData = picture.LockBits(area, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb)

            Try

                Dim pixels(area.Width * area.Height - 1) As Integer
                Marshal.Copy(data.Scan0, pixels, 0, pixels.Length)

                Dim counts As New Dictionary(Of Integer, Integer)()
                Dim commonest As Integer = 0
                Dim most As Integer = 0

                within.Intersect(area)

                For y As Integer = within.Top To within.Bottom - 1
                    For x As Integer = within.Left To within.Right - 1

                        Dim pixel As Integer = pixels(y * area.Width + x)
                        Dim count As Integer = 0

                        counts.TryGetValue(pixel, count)
                        counts(pixel) = count + 1

                        If count + 1 > most Then
                            most = count + 1
                            commonest = pixel
                        End If

                    Next
                Next

                Return Color.FromArgb(commonest)

            Finally
                picture.UnlockBits(data)
            End Try

        End Function


        ' Leaves only the caption in the picture, in the muted color. A pixel
        ' between the color WinForms drew the caption in and the surface
        ' behind it is caption: its smoothed edges are blends of the two, and
        ' keep their share of each. Every other pixel is cleared, and so is
        ' the box around whatever else was drawn within the given area: the
        ' box or tick of a check box or option can be as dark as a caption.
        Private Shared Sub KeepMutedCaption(
            picture As Bitmap,
            within As Rectangle,
            drawn As Color,
            surface As Color,
            muted As Color
        )

            Dim area As New Rectangle(Point.Empty, picture.Size)
            Dim data As BitmapData = picture.LockBits(area, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb)

            Try

                Dim pixels(area.Width * area.Height - 1) As Integer
                Marshal.Copy(data.Scan0, pixels, 0, pixels.Length)

                Dim background As Integer = surface.ToArgb() And &HFFFFFF
                Dim other As Rectangle = Rectangle.Empty

                For y As Integer = 0 To area.Height - 1
                    For x As Integer = 0 To area.Width - 1

                        Dim index As Integer = y * area.Width + x
                        Dim original As Integer = pixels(index)

                        pixels(index) = 0

                        If Not within.Contains(x, y) OrElse (original And &HFFFFFF) = background Then
                            Continue For
                        End If

                        pixels(index) = MutedPixel(Color.FromArgb(original), drawn, surface, muted)

                        If pixels(index) = 0 Then
                            Dim spot As New Rectangle(x, y, 1, 1)
                            other = If(other.IsEmpty, spot, Rectangle.Union(other, spot))
                        End If

                    Next
                Next

                For y As Integer = other.Top To other.Bottom - 1
                    For x As Integer = other.Left To other.Right - 1
                        pixels(y * area.Width + x) = 0
                    Next
                Next

                Marshal.Copy(pixels, 0, data.Scan0, pixels.Length)

            Finally
                picture.UnlockBits(data)
            End Try

        End Sub


        ' A caption pixel in the muted color, or a clear pixel for any other.
        Private Shared Function MutedPixel(
            pixel As Color,
            drawn As Color,
            surface As Color,
            muted As Color
        ) As Integer

            Dim red As Integer = MutedChannel(pixel.R, drawn.R, surface.R, muted.R)
            Dim green As Integer = MutedChannel(pixel.G, drawn.G, surface.G, muted.G)
            Dim blue As Integer = MutedChannel(pixel.B, drawn.B, surface.B, muted.B)

            If red < 0 OrElse green < 0 OrElse blue < 0 Then
                Return 0
            End If

            Return Color.FromArgb(red, green, blue).ToArgb()

        End Function


        ' One channel of a caption pixel, as far from the surface toward the
        ' muted color as it was toward the drawn color; -1 when it is not
        ' between the drawn color and the surface.
        Private Shared Function MutedChannel(
            value As Integer,
            drawn As Integer,
            surface As Integer,
            muted As Integer
        ) As Integer

            If drawn >= surface OrElse value < drawn OrElse value > surface Then
                Return -1
            End If

            Return surface + CInt(Math.Round((surface - value) * (muted - surface) / (surface - drawn)))

        End Function

    End Class

End Namespace