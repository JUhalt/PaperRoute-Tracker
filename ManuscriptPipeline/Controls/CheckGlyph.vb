Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Runtime.CompilerServices
Imports System.Runtime.InteropServices
Imports System.Windows.Forms
Imports ManuscriptPipeline.Services

Namespace Controls

    ' The check boxes of lists and grids, drawn from the theme's tokens in
    ' Dark (#106). Windows draws them from its Light theme whatever the
    ' window's colors: a near-white square on a dark row. A standalone check
    ' box and a checked list box already get Windows' dark check box. In
    ' Light nothing is changed, and Windows draws them as before.
    Friend NotInheritable Class CheckGlyph

        ' Windows 11's box at 96 DPI: 13 pixels, slightly rounded.
        Private Const BoxSide As Integer = 13
        Private Const Corner As Single = 2.5F

        Private Const LVM_GETIMAGELIST As Integer = &H1002
        Private Const LVSIL_STATE As Integer = 2


        Private Sub New()
        End Sub


        ' The box, centered in the bounds: the card color inside a secondary
        ' outline, or the accent with a tick when checked. A disabled box is
        ' in the muted color.
        Public Shared Sub Draw(
            g As Graphics,
            bounds As Rectangle,
            checked As Boolean,
            enabled As Boolean,
            dpi As Integer
        )

            Dim scale As Single = Math.Max(96, dpi) / 96.0F
            Dim side As Integer = UiTheme.Px(BoxSide, dpi)
            Dim left As Integer = bounds.Left + (bounds.Width - side) \ 2
            Dim top As Integer = bounds.Top + (bounds.Height - side) \ 2
            ' On whole pixels, so the outline's straight edges are solid.
            Dim box As New RectangleF(left, top, side - 1.0F, side - 1.0F)
            Dim mark As Color = If(enabled, UiTheme.AccentColor(), UiTheme.MutedText())
            Dim outline As Color = If(enabled, UiTheme.SecondaryText(), UiTheme.MutedText())

            Dim smoothing As SmoothingMode = g.SmoothingMode
            g.SmoothingMode = SmoothingMode.AntiAlias

            Using path As GraphicsPath = RoundedShapes.Create(box, Corner * scale)

                Using fill As New SolidBrush(If(checked, mark, UiTheme.CardBackground())),
                      edge As New Pen(If(checked, mark, outline))
                    g.FillPath(fill, path)
                    g.DrawPath(edge, path)
                End Using

            End Using

            If checked Then
                Using tick As New Pen(UiTheme.OnAccentText(), 1.6F * scale) With {
                    .StartCap = LineCap.Round,
                    .EndCap = LineCap.Round,
                    .LineJoin = LineJoin.Round
                }
                    g.DrawLines(tick, {
                        New PointF(left + 3.2F * scale, top + 6.7F * scale),
                        New PointF(left + 5.6F * scale, top + 9.1F * scale),
                        New PointF(left + 9.8F * scale, top + 4.0F * scale)
                    })
                End Using
            End If

            g.SmoothingMode = smoothing

        End Sub


        ' A list's check boxes are the two state images Windows makes for it,
        ' clear and checked. In Dark they are drawn over here, in place, each
        ' time Windows makes them: when check boxes are turned on, when the
        ' theme changes, and when the list moves to a display with another
        ' scale. The images stay Windows' own, so nothing here owns or frees
        ' them.
        Public Shared Sub Attach(
            list As ListView
        )

            If list Is Nothing OrElse Not list.CheckBoxes OrElse Not UiTheme.IsDark() Then
                Return
            End If

            ' A dialog is styled more than once; one handler is enough.
            RemoveHandler list.HandleCreated,
                AddressOf ListCreated

            AddHandler list.HandleCreated,
                AddressOf ListCreated

            If list.IsHandleCreated Then
                Watch(list)
                PaintStateImages(list)
            End If

        End Sub


        ' A grid's check-box cells are painted here in Dark, over the cell's
        ' own background, border, selection and focus.
        Public Shared Sub Attach(
            grid As DataGridView
        )

            If grid Is Nothing OrElse Not UiTheme.IsDark() Then
                Return
            End If

            RemoveHandler grid.CellPainting,
                AddressOf PaintGridCell

            AddHandler grid.CellPainting,
                AddressOf PaintGridCell

        End Sub


        ' Windows turns check boxes on, and so makes the images, after this
        ' event, so the new window is watched from here.
        Private Shared Sub ListCreated(
            sender As Object,
            e As EventArgs
        )

            Watch(DirectCast(sender, ListView))

        End Sub


        Private Shared Sub Watch(
            list As ListView
        )

            Dim watcher As StateImageWatcher = Nothing

            If Watchers.TryGetValue(list, watcher) AndAlso watcher.Handle = list.Handle Then
                Return
            End If

            Watchers.AddOrUpdate(list, New StateImageWatcher(list))

        End Sub


        ' One watcher for each list's current window; a window's watcher lets
        ' go of it when the window is destroyed.
        Private Shared ReadOnly Watchers As New ConditionalWeakTable(Of ListView, StateImageWatcher)()


        ' Draws a list's check boxes again after each message that makes
        ' Windows draw them: a change of extended style, which is how check
        ' boxes are turned on, a change of theme, and a change of scale.
        Private NotInheritable Class StateImageWatcher
            Inherits NativeWindow

            Private Const WM_THEMECHANGED As Integer = &H31A
            Private Const WM_DPICHANGED_AFTERPARENT As Integer = &H2E3
            Private Const LVM_SETEXTENDEDLISTVIEWSTYLE As Integer = &H1036

            Private ReadOnly _list As ListView

            Public Sub New(list As ListView)
                _list = list
                AssignHandle(list.Handle)
            End Sub

            Protected Overrides Sub WndProc(ByRef m As Message)

                MyBase.WndProc(m)

                Select Case m.Msg
                    Case WM_THEMECHANGED, WM_DPICHANGED_AFTERPARENT, LVM_SETEXTENDEDLISTVIEWSTYLE
                        PaintStateImages(_list)
                End Select

            End Sub

        End Class


        Private Shared Sub PaintStateImages(
            list As ListView
        )

            If list.IsDisposed OrElse Not list.IsHandleCreated OrElse Not list.CheckBoxes OrElse Not UiTheme.IsDark() Then
                Return
            End If

            Try

                Dim calls As ImageListCalls = ImageListCalls.Find(list)
                Dim images As IntPtr = SendMessage(list.Handle, LVM_GETIMAGELIST, New IntPtr(LVSIL_STATE), IntPtr.Zero)
                Dim width As Integer = 0
                Dim height As Integer = 0

                If calls Is Nothing OrElse
                   images = IntPtr.Zero OrElse
                   calls.GetImageCount(images) < 2 OrElse
                   Not calls.GetIconSize(images, width, height) Then

                    Return

                End If

                For index As Integer = 0 To 1

                    Using image As New Bitmap(width, height, Imaging.PixelFormat.Format32bppArgb)

                        ' A list draws its check boxes alike whether or not it is on.
                        Using g As Graphics = Graphics.FromImage(image)
                            Draw(g, New Rectangle(0, 0, width, height), index = 1, True, list.DeviceDpi)
                        End Using

                        Dim icon As IntPtr = image.GetHicon()

                        Try
                            calls.ReplaceIcon(images, index, icon)
                        Finally
                            DestroyIcon(icon)
                        End Try

                    End Using

                Next

                list.Invalidate()

            Catch ex As Exception
                ' Windows' own check boxes stay; a list never fails to show.
            End Try

        End Sub


        ' The image-list functions of the common controls that made a list.
        ' A process can hold two versions of the common controls, and only
        ' the one that made a list's images can change them. The list makes
        ' its column header itself, so the header's window class belongs to
        ' the same version: the functions come from the module that holds
        ' the class's window procedure.
        Private NotInheritable Class ImageListCalls

            Public Delegate Function GetImageCountCall(images As IntPtr) As Integer
            Public Delegate Function GetIconSizeCall(images As IntPtr, ByRef width As Integer, ByRef height As Integer) As Boolean
            Public Delegate Function ReplaceIconCall(images As IntPtr, index As Integer, icon As IntPtr) As Integer

            Private Const LVM_GETHEADER As Integer = &H101F
            Private Const GCLP_WNDPROC As Integer = -24
            Private Const FROM_ADDRESS_UNCHANGED_REFCOUNT As Integer = &H6

            Public GetImageCount As GetImageCountCall
            Public GetIconSize As GetIconSizeCall
            Public ReplaceIcon As ReplaceIconCall


            ' Nothing when they cannot be found, as for a list without a
            ' header; then Windows' own check boxes stay.
            Public Shared Function Find(list As ListView) As ImageListCalls

                Dim header As IntPtr = SendMessage(list.Handle, LVM_GETHEADER, IntPtr.Zero, IntPtr.Zero)
                Dim controls As IntPtr = IntPtr.Zero

                If header = IntPtr.Zero Then
                    Return Nothing
                End If

                Dim procedure As IntPtr = If(IntPtr.Size = 8, GetClassLongPtr(header, GCLP_WNDPROC), GetClassLong(header, GCLP_WNDPROC))

                If procedure = IntPtr.Zero OrElse Not GetModuleHandleEx(FROM_ADDRESS_UNCHANGED_REFCOUNT, procedure, controls) Then
                    Return Nothing
                End If

                Dim count As IntPtr = GetProcAddress(controls, "ImageList_GetImageCount")
                Dim size As IntPtr = GetProcAddress(controls, "ImageList_GetIconSize")
                Dim replace As IntPtr = GetProcAddress(controls, "ImageList_ReplaceIcon")

                If count = IntPtr.Zero OrElse size = IntPtr.Zero OrElse replace = IntPtr.Zero Then
                    Return Nothing
                End If

                Return New ImageListCalls With {
                    .GetImageCount = Marshal.GetDelegateForFunctionPointer(Of GetImageCountCall)(count),
                    .GetIconSize = Marshal.GetDelegateForFunctionPointer(Of GetIconSizeCall)(size),
                    .ReplaceIcon = Marshal.GetDelegateForFunctionPointer(Of ReplaceIconCall)(replace)
                }

            End Function


            <DllImport("user32.dll", EntryPoint:="GetClassLongPtrW")>
            Private Shared Function GetClassLongPtr(window As IntPtr, index As Integer) As IntPtr
            End Function

            <DllImport("user32.dll", EntryPoint:="GetClassLongW")>
            Private Shared Function GetClassLong(window As IntPtr, index As Integer) As IntPtr
            End Function

            <DllImport("kernel32.dll", EntryPoint:="GetModuleHandleExW")>
            Private Shared Function GetModuleHandleEx(flags As Integer, address As IntPtr, ByRef [module] As IntPtr) As Boolean
            End Function

            <DllImport("kernel32.dll", CharSet:=CharSet.Ansi, BestFitMapping:=False)>
            Private Shared Function GetProcAddress([module] As IntPtr, name As String) As IntPtr
            End Function

        End Class


        Private Shared Sub PaintGridCell(
            sender As Object,
            e As DataGridViewCellPaintingEventArgs
        )

            Dim grid As DataGridView = DirectCast(sender, DataGridView)

            If e.RowIndex < 0 OrElse e.ColumnIndex < 0 OrElse
               Not UiTheme.IsDark() OrElse
               Not TypeOf grid.Columns(e.ColumnIndex) Is DataGridViewCheckBoxColumn Then

                Return

            End If

            e.Paint(e.ClipBounds, DataGridViewPaintParts.All And Not DataGridViewPaintParts.ContentForeground)

            ' The box sits in the cell inside its right and bottom grid lines.
            Dim cell As New Rectangle(e.CellBounds.X, e.CellBounds.Y, e.CellBounds.Width - 1, e.CellBounds.Height - 1)
            Dim value As Object = e.FormattedValue
            Dim checked As Boolean =
                (TypeOf value Is Boolean AndAlso DirectCast(value, Boolean)) OrElse
                (TypeOf value Is CheckState AndAlso DirectCast(value, CheckState) = CheckState.Checked)

            Draw(e.Graphics, cell, checked, grid.Enabled, grid.DeviceDpi)
            e.Handled = True

        End Sub


        <DllImport("user32.dll")>
        Private Shared Function SendMessage(handle As IntPtr, message As Integer, wParam As IntPtr, lParam As IntPtr) As IntPtr
        End Function

        <DllImport("user32.dll")>
        Private Shared Function DestroyIcon(icon As IntPtr) As Boolean
        End Function

    End Class

End Namespace
