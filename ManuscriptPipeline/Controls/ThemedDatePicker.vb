Imports System.Drawing
Imports System.Drawing.Imaging
Imports System.Linq
Imports System.Runtime.InteropServices
Imports System.Windows.Forms
Imports ManuscriptPipeline.Services

Namespace Controls

    ' A date field that follows the theme in Dark (#106). Windows draws a
    ' date picker's field from its Light theme whatever the window's colors:
    ' a white field with dark text. In Dark this picker has Windows draw the
    ' field into a picture and paints the picture recolored (see Recolor),
    ' with the field in the card color and its text in the primary text
    ' color. Typing, the arrow keys and the calendar are Windows' own. In
    ' Light nothing is changed.
    Friend Class ThemedDatePicker
        Inherits DateTimePicker

        Private Const WM_ERASEBKGND As Integer = &H14
        Private Const WM_PAINT As Integer = &HF
        Private Const WM_PRINTCLIENT As Integer = &H318
        Private Const PRF_CLIENT As Integer = &H4
        Private Const PRF_ERASEBKGND As Integer = &H8
        Private Const COLOR_HIGHLIGHT As Integer = 13

        ' Set while Windows draws the field into the picture.
        Private _drawing As Boolean


        Protected Overrides Sub WndProc(ByRef m As Message)

            If UiTheme.IsDark() AndAlso Not _drawing Then

                Select Case m.Msg

                    ' The recolored picture covers the whole field, so the
                    ' white background is never painted first.
                    Case WM_ERASEBKGND
                        m.Result = New IntPtr(1)
                        Return

                    Case WM_PAINT
                        PaintRecolored(m.WParam)
                        m.Result = IntPtr.Zero
                        Return

                    Case WM_PRINTCLIENT
                        DrawRecolored(m.WParam)
                        m.Result = IntPtr.Zero
                        Return

                End Select

            End If

            MyBase.WndProc(m)

        End Sub


        ' A paint message may name the device context to draw on.
        Private Sub PaintRecolored(
            dc As IntPtr
        )

            If dc <> IntPtr.Zero Then
                DrawRecolored(dc)
                Return
            End If

            Dim paint As New PAINTSTRUCT()
            Dim screen As IntPtr = BeginPaint(Handle, paint)

            Try
                DrawRecolored(screen)
            Finally
                EndPaint(Handle, paint)
            End Try

        End Sub


        Private Sub DrawRecolored(
            dc As IntPtr
        )

            Dim size As Size = ClientSize

            If dc = IntPtr.Zero OrElse size.Width <= 0 OrElse size.Height <= 0 Then
                Return
            End If

            Try

                Using picture As New Bitmap(size.Width, size.Height, PixelFormat.Format32bppRgb)

                    Using g As Graphics = Graphics.FromImage(picture)

                        g.Clear(Color.White)
                        Dim pictureDc As IntPtr = g.GetHdc()

                        Try
                            DrawAsWindowsDoes(pictureDc)
                        Finally
                            g.ReleaseHdc(pictureDc)
                        End Try

                    End Using

                    Recolor(picture, ColorTranslator.FromWin32(GetSysColor(COLOR_HIGHLIGHT)))

                    Using target As Graphics = Graphics.FromHdc(dc)
                        target.DrawImageUnscaled(picture, 0, 0)
                    End Using

                End Using

            Catch ex As Exception
                ' A paint never fails: Windows' own field shows instead.
                DrawAsWindowsDoes(dc)
            End Try

        End Sub


        Private Sub DrawAsWindowsDoes(
            dc As IntPtr
        )

            _drawing = True

            Try
                Dim print As Message = Message.Create(Handle, WM_PRINTCLIENT, dc, New IntPtr(PRF_CLIENT Or PRF_ERASEBKGND))
                DefWndProc(print)
            Finally
                _drawing = False
            End Try

        End Sub


        ' Recolors the picture of a field Windows drew. The field's own color,
        ' the commonest, becomes the card color and black the ink, each
        ' pixel as far from one toward the other as it was, so a field that
        ' is off is the card color too and text keeps its smoothing (its
        ' colored ClearType edges become grey ones). Two things keep
        ' Windows' colors: colored lines, such as the frame of a field with
        ' the focus, which are colored pixels in a run of alike ones across
        ' half the field or down most of it, longer than any stroke of its
        ' text; and the highlight on the part of the date being edited, the
        ' box around the pixels in the highlight color that are not lines.
        ' A field Windows drew dark is left as it is.
        Private Shared Sub Recolor(
            picture As Bitmap,
            highlight As Color
        )

            Dim area As New Rectangle(Point.Empty, picture.Size)
            Dim data As BitmapData = picture.LockBits(area, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb)

            Try

                Dim pixels(area.Width * area.Height - 1) As Integer
                Marshal.Copy(data.Scan0, pixels, 0, pixels.Length)

                Dim surface As Color = Color.FromArgb(pixels.GroupBy(Function(pixel) pixel).OrderByDescending(Function(group) group.Count()).First().Key)

                If surface.GetBrightness() < 0.5F Then
                    Return
                End If

                Dim kept As Boolean() = ColoredLines(pixels, area.Width, area.Height)
                Dim marked As Integer = highlight.ToArgb() And &HFFFFFF
                Dim card As Color = UiTheme.CardBackground()
                Dim ink As Color = UiTheme.PrimaryText()
                Dim edited As Rectangle = Rectangle.Empty

                For index As Integer = 0 To pixels.Length - 1
                    If Not kept(index) AndAlso (pixels(index) And &HFFFFFF) = marked Then
                        Dim spot As New Rectangle(index Mod area.Width, index \ area.Width, 1, 1)
                        edited = If(edited.IsEmpty, spot, Rectangle.Union(edited, spot))
                    End If
                Next

                For y As Integer = 0 To area.Height - 1

                    Dim row As Integer = y * area.Width

                    For x As Integer = 0 To area.Width - 1

                        If kept(row + x) OrElse edited.Contains(x, y) Then
                            Continue For
                        End If

                        Dim pixel As Color = Color.FromArgb(pixels(row + x))
                        Dim share As Double = (ShareOf(pixel.R, surface.R) + ShareOf(pixel.G, surface.G) + ShareOf(pixel.B, surface.B)) / 3.0

                        pixels(row + x) = Color.FromArgb(
                            CInt(Math.Round(card.R + (CInt(ink.R) - CInt(card.R)) * share)),
                            CInt(Math.Round(card.G + (CInt(ink.G) - CInt(card.G)) * share)),
                            CInt(Math.Round(card.B + (CInt(ink.B) - CInt(card.B)) * share))).ToArgb()

                    Next

                Next

                Marshal.Copy(pixels, 0, data.Scan0, pixels.Length)

            Finally
                picture.UnlockBits(data)
            End Try

        End Sub


        ' How far a channel is from the field's color toward black: none for
        ' the field's color or anything lighter, all for black.
        Private Shared Function ShareOf(
            value As Byte,
            surface As Byte
        ) As Double

            If surface = 0 Then
                Return 1.0
            End If

            Return Math.Max(0.0, Math.Min(1.0, (CInt(surface) - CInt(value)) / CDbl(surface)))

        End Function


        ' The colored pixels in a run of the same color across half the
        ' picture or down four fifths of it: lines Windows drew, not the
        ' colored edges of text.
        Private Shared Function ColoredLines(
            pixels As Integer(),
            width As Integer,
            height As Integer
        ) As Boolean()

            Const Colored As Integer = 64

            Dim kept(pixels.Length - 1) As Boolean

            For index As Integer = 0 To pixels.Length - 1

                Dim pixel As Color = Color.FromArgb(pixels(index))
                Dim chroma As Integer = Math.Max(pixel.R, Math.Max(pixel.G, pixel.B)) - Math.Min(pixel.R, Math.Min(pixel.G, pixel.B))

                If chroma < Colored Then
                    Continue For
                End If

                Dim x As Integer = index Mod width
                Dim y As Integer = index \ width
                Dim across As Integer = 1
                Dim down As Integer = 1

                Dim left As Integer = x - 1
                While left >= 0 AndAlso pixels(y * width + left) = pixels(index)
                    across += 1
                    left -= 1
                End While

                Dim right As Integer = x + 1
                While right < width AndAlso pixels(y * width + right) = pixels(index)
                    across += 1
                    right += 1
                End While

                Dim up As Integer = y - 1
                While up >= 0 AndAlso pixels(up * width + x) = pixels(index)
                    down += 1
                    up -= 1
                End While

                Dim below As Integer = y + 1
                While below < height AndAlso pixels(below * width + x) = pixels(index)
                    down += 1
                    below += 1
                End While

                kept(index) = across * 2 >= width OrElse down * 5 >= height * 4

            Next

            Return kept

        End Function


        <StructLayout(LayoutKind.Sequential)>
        Private Structure PAINTSTRUCT
            Public hdc As IntPtr
            Public fErase As Integer
            Public rcLeft As Integer
            Public rcTop As Integer
            Public rcRight As Integer
            Public rcBottom As Integer
            Public fRestore As Integer
            Public fIncUpdate As Integer
            <MarshalAs(UnmanagedType.ByValArray, SizeConst:=32)>
            Public rgbReserved As Byte()
        End Structure

        <DllImport("user32.dll")>
        Private Shared Function BeginPaint(window As IntPtr, ByRef paint As PAINTSTRUCT) As IntPtr
        End Function

        <DllImport("user32.dll")>
        Private Shared Function EndPaint(window As IntPtr, ByRef paint As PAINTSTRUCT) As Boolean
        End Function

        <DllImport("user32.dll")>
        Private Shared Function GetSysColor(index As Integer) As Integer
        End Function

    End Class

End Namespace
