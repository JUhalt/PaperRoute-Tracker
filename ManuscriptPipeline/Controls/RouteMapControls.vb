Imports System
Imports System.Collections.Generic
Imports System.ComponentModel
Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Globalization
Imports System.Linq
Imports System.Windows.Forms
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

Namespace Controls

    ' Colors and wording shared by the route map views (#82). The three
    ' kinds differ in lightness as well as hue, and white text on each fill
    ' meets 4.5:1.
    Friend NotInheritable Class RouteMapStyle

        Private Sub New()
        End Sub

        Public Shared Function Fill(kind As RouteSegmentKind) As Color
            Select Case kind
                Case RouteSegmentKind.Journal : Return Color.FromArgb(15, 118, 110)
                Case RouteSegmentKind.Author : Return Color.FromArgb(180, 83, 9)
                Case RouteSegmentKind.Production : Return If(UiTheme.IsDark(), Color.FromArgb(100, 116, 139), Color.FromArgb(71, 85, 105))
                Case Else : Return If(UiTheme.IsDark(), Color.FromArgb(92, 112, 118), Color.FromArgb(160, 176, 181))
            End Select
        End Function

        Public Shared Sub FillSegment(g As Graphics, bounds As RectangleF, kind As RouteSegmentKind, background As Color)
            If kind = RouteSegmentKind.NotRecorded Then
                Using hatch As New HatchBrush(HatchStyle.WideUpwardDiagonal, Fill(kind), background), edge As New Pen(Fill(kind))
                    g.FillRectangle(hatch, bounds)
                    g.DrawRectangle(edge, bounds.X, bounds.Y, Math.Max(0, bounds.Width - 1), Math.Max(0, bounds.Height - 1))
                End Using
            Else
                Using brush As New SolidBrush(Fill(kind))
                    g.FillRectangle(brush, bounds)
                End Using
            End If
        End Sub

        Public Shared Function MarkerColor(marker As RouteMarker) As Color
            Select Case marker.Kind
                Case RouteMarkerKind.Published : Return Color.FromArgb(15, 118, 110)
                Case RouteMarkerKind.Decision
                    Select Case RouteAnalyticsService.OutcomeOf(marker.Decision)
                        Case SubmissionOutcome.Accepted : Return Color.FromArgb(21, 128, 61)
                        Case SubmissionOutcome.Open : Return Color.FromArgb(180, 83, 9)
                        Case SubmissionOutcome.Withdrawn : Return Color.FromArgb(71, 85, 105)
                        Case Else : Return Color.FromArgb(185, 28, 28)
                    End Select
                Case Else : Return Color.FromArgb(29, 78, 216)
            End Select
        End Function

        ' "105 days with the journals", one per kind on the route.
        Public Shared Function DayTotals(map As RouteMap) As List(Of (Kind As RouteSegmentKind, Text As String))
            Dim result As New List(Of (Kind As RouteSegmentKind, Text As String))()
            For Each kind As RouteSegmentKind In [Enum].GetValues(GetType(RouteSegmentKind))
                Dim days As Integer = map.DaysOf(kind)
                If days > 0 Then result.Add((kind, days.ToString(CultureInfo.CurrentCulture) & If(days = 1, " day ", " days ") &
                                                   If(kind = RouteSegmentKind.Journal AndAlso map.Journals.Count > 1, "with the journals", RouteMapService.KindName(kind))))
            Next
            Return result
        End Function

        Public Shared Function Summary(map As RouteMap) As String
            If map.IsEmpty Then Return "No submissions are recorded yet, so there is no route to draw."
            Dim ending As String = If(map.Ongoing, "today", If(map.Markers.Any(Function(item) item.Kind = RouteMarkerKind.Published), "publication", "the last decision"))
            Return map.TotalDays.ToString(CultureInfo.CurrentCulture) & " days from first submission to " & ending & ": " &
                   String.Join(", ", DayTotals(map).Select(Function(item) item.Text)) & "."
        End Function

        Public Shared Function ShortDate(value As DateTime) As String
            Return value.ToString("MMM d", CultureInfo.CurrentCulture)
        End Function

    End Class


    ' Color swatches with their meaning, wrapping to the width it is given.
    Friend Class RouteMapLegend
        Inherits Control

        Private _items As New List(Of (Kind As RouteSegmentKind, Text As String))()

        Public Sub New()
            SetStyle(ControlStyles.UserPaint Or ControlStyles.AllPaintingInWmPaint Or ControlStyles.OptimizedDoubleBuffer Or ControlStyles.ResizeRedraw, True)
            AccessibleRole = AccessibleRole.StaticText
        End Sub

        <DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
        Public Property Items As List(Of (Kind As RouteSegmentKind, Text As String))
            Get
                Return _items
            End Get
            Set(value As List(Of (Kind As RouteSegmentKind, Text As String)))
                _items = If(value, New List(Of (Kind As RouteSegmentKind, Text As String))())
                AccessibleName = String.Join(", ", _items.Select(Function(item) item.Text))
                FitHeight()
                Invalidate()
            End Set
        End Property

        Private Function Place() As List(Of Rectangle)
            Dim unit As Integer = UiTheme.Px(1, DeviceDpi)
            Dim line As Integer = TextRenderer.MeasureText("Ag", Font).Height
            Dim result As New List(Of Rectangle)()
            Dim x As Integer = 0
            Dim y As Integer = 0
            For Each item In _items
                Dim width As Integer = 18 * unit + TextRenderer.MeasureText(item.Text, Font, Size.Empty, TextFormatFlags.NoPrefix Or TextFormatFlags.NoPadding).Width
                If x > 0 AndAlso x + width > Me.Width Then
                    x = 0
                    y += line + 4 * unit
                End If
                result.Add(New Rectangle(x, y, width, line))
                x += width + 20 * unit
            Next
            Return result
        End Function

        Private Sub FitHeight()
            Dim places As List(Of Rectangle) = Place()
            Dim wanted As Integer = If(places.Count = 0, 0, places.Max(Function(item) item.Bottom))
            If Height <> wanted Then Height = wanted
        End Sub

        Protected Overrides Sub OnResize(e As EventArgs)
            MyBase.OnResize(e)
            FitHeight()
        End Sub

        Protected Overrides Sub OnFontChanged(e As EventArgs)
            MyBase.OnFontChanged(e)
            FitHeight()
        End Sub

        Protected Overrides Sub OnPaint(e As PaintEventArgs)
            e.Graphics.Clear(BackColor)
            Dim unit As Integer = UiTheme.Px(1, DeviceDpi)
            Dim places As List(Of Rectangle) = Place()
            For index As Integer = 0 To _items.Count - 1
                Dim place As Rectangle = places(index)
                Dim swatch As Integer = 12 * unit
                RouteMapStyle.FillSegment(e.Graphics, New RectangleF(place.X, place.Y + (place.Height - swatch) \ 2, swatch, swatch), _items(index).Kind, BackColor)
                TextRenderer.DrawText(e.Graphics, _items(index).Text, Font, New Point(place.X + 18 * unit, place.Y), UiTheme.PrimaryText(), TextFormatFlags.NoPrefix Or TextFormatFlags.NoPadding)
            Next
        End Sub

    End Class


    ' One manuscript's route drawn to scale (#82): journals above, the
    ' colored route with numbered events on it, months below, and the
    ' numbered key underneath. Its height follows its width.
    Friend Class RouteMapBar
        Inherits Control

        Private _map As New RouteMap()
        Private _steps As New List(Of RouteStep)()

        Public Sub New()
            SetStyle(ControlStyles.UserPaint Or ControlStyles.AllPaintingInWmPaint Or ControlStyles.OptimizedDoubleBuffer Or ControlStyles.ResizeRedraw, True)
            AccessibleRole = AccessibleRole.Graphic
        End Sub

        <DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
        Public Property Map As RouteMap
            Get
                Return _map
            End Get
            Set(value As RouteMap)
                _map = If(value, New RouteMap())
                _steps = RouteMapService.Steps(_map)
                AccessibleName = "Route drawn to scale. " & RouteMapStyle.Summary(_map)
                AccessibleDescription = String.Join(". ", _steps.Select(Function(item) item.Number.ToString(CultureInfo.CurrentCulture) & ": " & item.Title & ", " &
                                                                                       RouteMapStyle.ShortDate(item.Marker.MarkerDate) & ", " & item.Note))
                FitHeight()
                Invalidate()
            End Set
        End Property

        Public ReadOnly Property Steps As List(Of RouteStep)
            Get
                Return _steps
            End Get
        End Property

        ' Where each part sits, for painting and for the height.
        Private Structure Frame
            Public Line As Integer
            Public Bar As Rectangle
            Public AxisTop As Integer
            Public KeyTop As Integer
            Public KeyColumns As Integer
            Public KeyRow As Integer
            Public NoteTop As Integer
            Public NoteHeight As Integer
            Public Bottom As Integer
        End Structure

        Private Function Measure(width As Integer) As Frame
            Dim unit As Integer = UiTheme.Px(1, DeviceDpi)
            Dim frame As New Frame With {.Line = TextRenderer.MeasureText("Ag", Font).Height}
            If _map.IsEmpty Then
                frame.Bottom = frame.Line * 2
                Return frame
            End If
            Dim inset As Integer = 10 * unit
            frame.Bar = New Rectangle(inset, frame.Line + 16 * unit, Math.Max(20, width - inset * 2), 30 * unit)
            frame.AxisTop = frame.Bar.Bottom + 8 * unit
            frame.KeyTop = frame.AxisTop + frame.Line + 16 * unit
            frame.KeyColumns = If(width >= 620 * unit, 2, 1)
            frame.KeyRow = frame.Line + 8 * unit
            Dim keyRows As Integer = CInt(Math.Ceiling(_steps.Count / CDbl(frame.KeyColumns)))
            frame.NoteTop = frame.KeyTop + keyRows * frame.KeyRow + 4 * unit
            If _map.Segments.Any(Function(item) item.Kind = RouteSegmentKind.NotRecorded) Then
                frame.NoteHeight = TextRenderer.MeasureText(HatchNote, Font, New Size(Math.Max(20, width), 0), TextFormatFlags.WordBreak Or TextFormatFlags.NoPrefix).Height
            End If
            frame.Bottom = frame.NoteTop + frame.NoteHeight
            Return frame
        End Function

        Private Const HatchNote As String =
            "A hatched stretch is time the record does not assign, such as a revision whose resubmission date was never entered. It is shown as not recorded, never estimated."

        Private Sub FitHeight()
            Dim wanted As Integer = Measure(Width).Bottom
            If Height <> wanted Then Height = wanted
        End Sub

        Protected Overrides Sub OnResize(e As EventArgs)
            MyBase.OnResize(e)
            FitHeight()
        End Sub

        Protected Overrides Sub OnFontChanged(e As EventArgs)
            MyBase.OnFontChanged(e)
            FitHeight()
        End Sub

        Private Function X(day As DateTime, bar As Rectangle) As Single
            Dim total As Double = Math.Max(1, _map.TotalDays)
            Return CSng(bar.Left + bar.Width * (day.Date - _map.Start.Date).TotalDays / total)
        End Function

        Protected Overrides Sub OnPaint(e As PaintEventArgs)

            Dim g As Graphics = e.Graphics
            g.Clear(BackColor)
            Dim frame As Frame = Measure(Width)
            If _map.IsEmpty Then
                TextRenderer.DrawText(g, RouteMapStyle.Summary(_map), Font, ClientRectangle, UiTheme.SecondaryText(), TextFormatFlags.VerticalCenter Or TextFormatFlags.Left Or TextFormatFlags.WordBreak)
                Return
            End If

            Dim unit As Integer = UiTheme.Px(1, DeviceDpi)
            Dim bar As Rectangle = frame.Bar
            Dim flags As TextFormatFlags = TextFormatFlags.NoPrefix Or TextFormatFlags.SingleLine Or TextFormatFlags.NoPadding

            ' Who held the route: each journal over its stretch, then the publisher.
            Using bold As New Font(Font, FontStyle.Bold)
                Dim holders As New List(Of (Name As String, Left As Single, Right As Single))()
                For Each journal As String In _map.Journals
                    Dim spans As List(Of RouteSegment) = _map.Segments.Where(Function(item) item.JournalName = journal).ToList()
                    holders.Add((journal, X(spans.First().Start, bar), X(spans.Last().Finish, bar)))
                Next
                Dim production As List(Of RouteSegment) = _map.Segments.Where(Function(item) item.Kind = RouteSegmentKind.Production).ToList()
                If production.Count > 0 Then holders.Add(("Publisher", X(production.First().Start, bar), X(production.Last().Finish, bar)))

                Dim lastRight As Single = -1
                For index As Integer = 0 To holders.Count - 1
                    Dim holder = holders(index)
                    Dim left As Single = Math.Max(holder.Left, lastRight + 10 * unit)
                    Dim limit As Single = If(index < holders.Count - 1, Math.Max(holder.Right, holders(index + 1).Left) - 6 * unit, Width)
                    Dim room As Integer = CInt(Math.Max(0, Math.Min(limit, Width) - left))
                    If room < 24 * unit Then Continue For
                    Dim text As New Rectangle(CInt(left), 0, room, frame.Line)
                    TextRenderer.DrawText(g, holder.Name, bold, text, UiTheme.PrimaryText(), flags Or TextFormatFlags.EndEllipsis)
                    Using tick As New Pen(UiTheme.CardBorder(), unit)
                        g.DrawLine(tick, holder.Left, frame.Line + 3 * unit, Math.Max(holder.Left + 1, holder.Right - 2 * unit), frame.Line + 3 * unit)
                    End Using
                    lastRight = left + Math.Min(room, TextRenderer.MeasureText(holder.Name, bold, Size.Empty, flags).Width)
                Next
            End Using

            ' The route.
            For Each segment As RouteSegment In _map.Segments
                Dim left As Single = X(segment.Start, bar)
                Dim right As Single = X(segment.Finish, bar)
                Dim bounds As New RectangleF(left, bar.Top, Math.Max(1, right - left - unit), bar.Height)
                RouteMapStyle.FillSegment(g, bounds, segment.Kind, BackColor)
                If segment.Kind <> RouteSegmentKind.NotRecorded Then
                    Dim label As String = segment.Days.ToString(CultureInfo.CurrentCulture) & If(segment.Days = 1, " day", " days")
                    If TextRenderer.MeasureText(label, Font).Width + 6 * unit > bounds.Width Then label = segment.Days.ToString(CultureInfo.CurrentCulture) & "d"
                    If TextRenderer.MeasureText(label, Font).Width + 2 * unit <= bounds.Width Then
                        TextRenderer.DrawText(g, label, Font, Rectangle.Round(bounds), Color.White,
                            TextFormatFlags.HorizontalCenter Or TextFormatFlags.VerticalCenter Or flags)
                    End If
                End If
                If segment.Ongoing Then
                    Using pen As New Pen(RouteMapStyle.Fill(segment.Kind), 3 * unit) With {.DashStyle = DashStyle.Dot}
                        Dim middle As Single = bar.Top + bar.Height / 2.0F
                        g.DrawLine(pen, Math.Min(bounds.Right + 3 * unit, Width - 16 * unit), middle, Math.Min(bounds.Right + 16 * unit, Width - 2 * unit), middle)
                    End Using
                End If
            Next

            ' Numbered events on the route's top edge.
            g.SmoothingMode = SmoothingMode.AntiAlias
            Dim diameter As Integer = 20 * unit
            Using small As New Font(Font.FontFamily, Math.Max(7.0F, Font.SizeInPoints - 1.5F), FontStyle.Bold)
                For Each item As RouteStep In _steps
                    Dim center As Single = Math.Min(Math.Max(X(item.Marker.MarkerDate, bar), bar.Left), bar.Right)
                    Dim circle As New RectangleF(center - diameter / 2.0F, bar.Top - diameter / 2.0F, diameter, diameter)
                    DrawNumber(g, circle, item, small, unit)
                Next

                ' Months along the bottom, or years on a long route.
                g.SmoothingMode = SmoothingMode.None
                Using border As New Pen(UiTheme.CardBorder(), unit)
                    g.DrawLine(border, bar.Left, frame.AxisTop - 3 * unit, bar.Right, frame.AxisTop - 3 * unit)
                End Using
                Dim yearly As Boolean = _map.TotalDays > 730
                Dim tick As New DateTime(_map.Start.Year, If(yearly, 1, _map.Start.Month), 1)
                Dim lastLabel As Single = -1000
                Dim first As Boolean = True
                While tick <= _map.Finish
                    If tick >= _map.Start.Date.AddDays(-3) OrElse first Then
                        Dim position As Single = X(If(tick < _map.Start, _map.Start, tick), bar)
                        Dim text As String = If(yearly, tick.ToString("yyyy", CultureInfo.CurrentCulture),
                                                If(first OrElse tick.Month = 1, tick.ToString("MMM yyyy", CultureInfo.CurrentCulture), tick.ToString("MMM", CultureInfo.CurrentCulture)))
                        Dim width As Integer = TextRenderer.MeasureText(text, Font, Size.Empty, flags).Width
                        position = Math.Max(bar.Left, position)
                        If position >= lastLabel + 8 * unit AndAlso position + width <= Me.Width Then
                            Using mark As New Pen(UiTheme.CardBorder(), unit)
                                g.DrawLine(mark, position, frame.AxisTop - 6 * unit, position, frame.AxisTop - 3 * unit)
                            End Using
                            TextRenderer.DrawText(g, text, Font, New Point(CInt(position), frame.AxisTop), UiTheme.SecondaryText(), flags)
                            lastLabel = position + width
                            first = False
                        End If
                    End If
                    tick = If(yearly, tick.AddYears(1), tick.AddMonths(1))
                End While

                ' The key.
                g.SmoothingMode = SmoothingMode.AntiAlias
                Dim columnWidth As Integer = Width \ frame.KeyColumns
                Dim rows As Integer = Math.Max(1, CInt(Math.Ceiling(_steps.Count / CDbl(frame.KeyColumns))))
                Using bold As New Font(Font, FontStyle.Bold)
                    For index As Integer = 0 To _steps.Count - 1
                        Dim item As RouteStep = _steps(index)
                        Dim left As Integer = (index \ rows) * columnWidth
                        Dim top As Integer = frame.KeyTop + (index Mod rows) * frame.KeyRow
                        Dim keyDiameter As Integer = 18 * unit
                        DrawNumber(g, New RectangleF(left, top + (frame.Line - keyDiameter) / 2.0F, keyDiameter, keyDiameter), item, small, unit)
                        Dim textLeft As Integer = left + keyDiameter + 8 * unit
                        Dim title As String = item.Title & ", " & RouteMapStyle.ShortDate(item.Marker.MarkerDate)
                        Dim room As Integer = Math.Max(0, left + columnWidth - 12 * unit - textLeft)
                        Dim titleWidth As Integer = Math.Min(room, TextRenderer.MeasureText(title, bold, Size.Empty, flags).Width)
                        TextRenderer.DrawText(g, title, bold, New Rectangle(textLeft, top, titleWidth, frame.Line), UiTheme.PrimaryText(), flags Or TextFormatFlags.EndEllipsis)
                        Dim noteLeft As Integer = textLeft + titleWidth + 8 * unit
                        If noteLeft < left + columnWidth - 40 * unit Then
                            TextRenderer.DrawText(g, item.Note, Font, New Rectangle(noteLeft, top, left + columnWidth - 12 * unit - noteLeft, frame.Line), UiTheme.SecondaryText(), flags Or TextFormatFlags.EndEllipsis)
                        End If
                    Next
                End Using
            End Using

            If frame.NoteHeight > 0 Then
                TextRenderer.DrawText(g, HatchNote, Font, New Rectangle(0, frame.NoteTop, Width, frame.NoteHeight), UiTheme.SecondaryText(), TextFormatFlags.WordBreak Or TextFormatFlags.NoPrefix)
            End If

        End Sub

        Private Sub DrawNumber(g As Graphics, circle As RectangleF, item As RouteStep, numberFont As Font, unit As Integer)
            Using fill As New SolidBrush(RouteMapStyle.MarkerColor(item.Marker)), ring As New Pen(BackColor, 2 * unit)
                g.FillEllipse(fill, circle)
                g.DrawEllipse(ring, circle)
            End Using
            TextRenderer.DrawText(g, item.Number.ToString(CultureInfo.CurrentCulture), numberFont, Rectangle.Round(circle), Color.White,
                TextFormatFlags.HorizontalCenter Or TextFormatFlags.VerticalCenter Or TextFormatFlags.NoPadding Or TextFormatFlags.NoPrefix)
        End Sub

    End Class


    ' Every route side by side from day 0 on one scale (#82). Up and Down
    ' choose a route; Enter or a click opens it.
    Friend Class RouteMapChart
        Inherits Control

        Private _routes As New List(Of RouteMapEntry)()
        Private _selected As Integer = -1
        Private _hot As Integer = -1

        Public Event RouteOpened As EventHandler(Of RouteMapEntry)

        Public Sub New()
            SetStyle(ControlStyles.UserPaint Or ControlStyles.AllPaintingInWmPaint Or ControlStyles.OptimizedDoubleBuffer Or ControlStyles.ResizeRedraw Or ControlStyles.Selectable, True)
            TabStop = True
            AccessibleRole = AccessibleRole.List
        End Sub

        <DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
        Public Property Routes As List(Of RouteMapEntry)
            Get
                Return _routes
            End Get
            Set(value As List(Of RouteMapEntry))
                _routes = If(value, New List(Of RouteMapEntry)())
                _selected = If(_routes.Count > 0, 0, -1)
                AccessibleName = "Routes side by side, " & _routes.Count.ToString(CultureInfo.CurrentCulture) & If(_routes.Count = 1, " route", " routes") & ". Press Enter to open the selected route."
                DescribeSelection()
                FitHeight()
                Invalidate()
            End Set
        End Property

        <DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
        Public Property SelectedIndex As Integer
            Get
                Return _selected
            End Get
            Set(value As Integer)
                _selected = Math.Max(-1, Math.Min(_routes.Count - 1, value))
                DescribeSelection()
                Invalidate()
            End Set
        End Property

        Private ReadOnly Property Line As Integer
            Get
                Return TextRenderer.MeasureText("Ag", Font).Height
            End Get
        End Property

        Private ReadOnly Property RowHeight As Integer
            Get
                Return Line * 2 + UiTheme.Px(14, DeviceDpi)
            End Get
        End Property

        Private ReadOnly Property HeaderHeight As Integer
            Get
                Return Line + UiTheme.Px(10, DeviceDpi)
            End Get
        End Property

        Private Sub FitHeight()
            Dim wanted As Integer = HeaderHeight + Math.Max(1, _routes.Count) * RowHeight + UiTheme.Px(4, DeviceDpi)
            If Height <> wanted Then Height = wanted
        End Sub

        Protected Overrides Sub OnFontChanged(e As EventArgs)
            MyBase.OnFontChanged(e)
            FitHeight()
        End Sub

        Protected Overrides Sub OnDpiChangedAfterParent(e As EventArgs)
            MyBase.OnDpiChangedAfterParent(e)
            FitHeight()
        End Sub

        Private Function Columns() As (TitleWidth As Integer, Bar As Rectangle, TotalWidth As Integer)
            Dim dpi As Integer = DeviceDpi
            Dim titleWidth As Integer = Math.Min(UiTheme.Px(300, dpi), Width \ 3)
            Dim totalWidth As Integer
            Using bold As New Font(Font, FontStyle.Bold)
                totalWidth = TextRenderer.MeasureText("0000+", bold).Width + UiTheme.Px(8, dpi)
            End Using
            Dim left As Integer = titleWidth + UiTheme.Px(16, dpi)
            Return (titleWidth, New Rectangle(left, HeaderHeight, Math.Max(10, Width - left - totalWidth - UiTheme.Px(24, dpi)), 0), totalWidth)
        End Function

        ' Days on the shared scale, rounded up to whole steps.
        Private Function ScaleDays() As Integer
            Dim longest As Integer = If(_routes.Count = 0, 0, _routes.Max(Function(item) item.Map.TotalDays))
            Dim stepDays As Integer = StepFor(longest)
            Return Math.Max(stepDays, CInt(Math.Ceiling(longest / CDbl(stepDays))) * stepDays)
        End Function

        Private Shared Function StepFor(days As Integer) As Integer
            Return If(days > 1440, 360, If(days > 720, 180, If(days > 360, 120, 60)))
        End Function

        Protected Overrides Sub OnPaint(e As PaintEventArgs)

            Dim g As Graphics = e.Graphics
            g.Clear(BackColor)
            Dim dpi As Integer = DeviceDpi
            Dim unit As Integer = UiTheme.Px(1, dpi)
            Dim flags As TextFormatFlags = TextFormatFlags.NoPrefix Or TextFormatFlags.SingleLine Or TextFormatFlags.NoPadding

            If _routes.Count = 0 Then
                TextRenderer.DrawText(g, "No routes to draw yet.", Font, New Rectangle(0, HeaderHeight, Width, RowHeight), UiTheme.SecondaryText(),
                    TextFormatFlags.Left Or TextFormatFlags.VerticalCenter Or TextFormatFlags.NoPrefix)
                Return
            End If

            Dim layout = Columns()
            Dim scale As Integer = ScaleDays()
            Dim stepDays As Integer = StepFor(scale - 1)
            Dim bodyBottom As Integer = HeaderHeight + _routes.Count * RowHeight

            Using bold As New Font(Font, FontStyle.Bold)

                TextRenderer.DrawText(g, "Manuscript", bold, New Point(UiTheme.Px(8, dpi), 0), UiTheme.SecondaryText(), flags)
                TextRenderer.DrawText(g, "Days", bold, New Rectangle(Width - layout.TotalWidth - UiTheme.Px(8, dpi), 0, layout.TotalWidth, Line), UiTheme.SecondaryText(), flags Or TextFormatFlags.Right)

                ' The shared day scale.
                For day As Integer = 0 To scale Step stepDays
                    Dim x As Integer = layout.Bar.Left + CInt(layout.Bar.Width * day / CDbl(scale))
                    Dim text As String = day.ToString(CultureInfo.CurrentCulture)
                    Dim textWidth As Integer = TextRenderer.MeasureText(text, Font, Size.Empty, flags).Width
                    TextRenderer.DrawText(g, text, Font, New Point(Math.Max(layout.Bar.Left, x - textWidth \ 2), 0), UiTheme.SecondaryText(), flags)
                    Using grid As New Pen(UiTheme.SubtleBorder(), unit)
                        g.DrawLine(grid, x, HeaderHeight - UiTheme.Px(3, dpi), x, bodyBottom)
                    End Using
                Next

                For index As Integer = 0 To _routes.Count - 1
                    Dim entry As RouteMapEntry = _routes(index)
                    Dim top As Integer = HeaderHeight + index * RowHeight
                    Dim rowBackground As Color = BackColor

                    If index = _selected AndAlso Focused Then
                        rowBackground = UiTheme.AccentMutedBackground()
                    ElseIf index = _hot Then
                        rowBackground = UiTheme.HeaderBackground()
                    End If
                    If rowBackground <> BackColor Then
                        Using highlight As New SolidBrush(rowBackground)
                            g.FillRectangle(highlight, 0, top, Width, RowHeight)
                        End Using
                        Using grid As New Pen(UiTheme.SubtleBorder(), unit)
                            For day As Integer = 0 To scale Step stepDays
                                Dim x As Integer = layout.Bar.Left + CInt(layout.Bar.Width * day / CDbl(scale))
                                g.DrawLine(grid, x, top, x, top + RowHeight)
                            Next
                        End Using
                    End If
                    If index = _selected AndAlso Focused Then
                        Using accent As New SolidBrush(UiTheme.AccentColor())
                            g.FillRectangle(accent, 0, top, 3 * unit, RowHeight)
                        End Using
                    End If

                    Dim textTop As Integer = top + (RowHeight - Line * 2) \ 2
                    TextRenderer.DrawText(g, ReminderService.SafeManuscriptTitle(entry.Manuscript), bold, New Rectangle(UiTheme.Px(8, dpi), textTop, layout.TitleWidth - UiTheme.Px(8, dpi), Line),
                        UiTheme.PrimaryText(), flags Or TextFormatFlags.EndEllipsis)
                    TextRenderer.DrawText(g, RouteMapService.Brief(entry.Map), Font, New Rectangle(UiTheme.Px(8, dpi), textTop + Line, layout.TitleWidth - UiTheme.Px(8, dpi), Line),
                        UiTheme.SecondaryText(), flags Or TextFormatFlags.EndEllipsis)

                    Dim barHeight As Integer = UiTheme.Px(18, dpi)
                    Dim barTop As Integer = top + (RowHeight - barHeight) \ 2
                    For Each segment As RouteSegment In entry.Map.Segments
                        Dim startDay As Double = (segment.Start.Date - entry.Map.Start.Date).TotalDays
                        Dim left As Single = CSng(layout.Bar.Left + layout.Bar.Width * startDay / scale)
                        Dim width As Single = CSng(layout.Bar.Width * segment.Days / CDbl(scale))
                        RouteMapStyle.FillSegment(g, New RectangleF(left, barTop, Math.Max(1, width - 1), barHeight), segment.Kind, rowBackground)
                        If segment.Ongoing Then
                            Using pen As New Pen(RouteMapStyle.Fill(segment.Kind), 3 * unit) With {.DashStyle = DashStyle.Dot}
                                g.DrawLine(pen, left + width + 2 * unit, barTop + barHeight / 2.0F, left + width + 14 * unit, barTop + barHeight / 2.0F)
                            End Using
                        End If
                    Next

                    TextRenderer.DrawText(g, entry.Map.TotalDays.ToString(CultureInfo.CurrentCulture) & If(entry.Map.Ongoing, "+", String.Empty), bold,
                        New Rectangle(Width - layout.TotalWidth - UiTheme.Px(8, dpi), top, layout.TotalWidth, RowHeight), UiTheme.PrimaryText(),
                        TextFormatFlags.Right Or TextFormatFlags.VerticalCenter Or flags)

                    Using divider As New Pen(UiTheme.SubtleBorder(), unit)
                        g.DrawLine(divider, 0, top + RowHeight - 1, Width, top + RowHeight - 1)
                    End Using
                Next

            End Using

        End Sub

        Private Function RowAt(y As Integer) As Integer
            If y < HeaderHeight Then Return -1
            Dim index As Integer = (y - HeaderHeight) \ RowHeight
            Return If(index < _routes.Count, index, -1)
        End Function

        Protected Overrides Sub OnMouseMove(e As MouseEventArgs)
            MyBase.OnMouseMove(e)
            Dim index As Integer = RowAt(e.Y)
            If index <> _hot Then
                _hot = index
                Cursor = If(index >= 0, Cursors.Hand, Cursors.Default)
                Invalidate()
            End If
        End Sub

        Protected Overrides Sub OnMouseLeave(e As EventArgs)
            MyBase.OnMouseLeave(e)
            _hot = -1
            Invalidate()
        End Sub

        Protected Overrides Sub OnMouseClick(e As MouseEventArgs)
            MyBase.OnMouseClick(e)
            Dim index As Integer = RowAt(e.Y)
            If index < 0 Then Return
            Focus()
            SelectedIndex = index
            RaiseEvent RouteOpened(Me, _routes(index))
        End Sub

        Protected Overrides Function IsInputKey(keyData As Keys) As Boolean
            Return keyData = Keys.Up OrElse keyData = Keys.Down OrElse keyData = Keys.Enter OrElse MyBase.IsInputKey(keyData)
        End Function

        Protected Overrides Sub OnKeyDown(e As KeyEventArgs)
            MyBase.OnKeyDown(e)
            If _routes.Count = 0 Then Return
            Select Case e.KeyCode
                Case Keys.Up : SelectedIndex = Math.Max(0, _selected - 1)
                Case Keys.Down : SelectedIndex = Math.Min(_routes.Count - 1, _selected + 1)
                Case Keys.Enter
                    If _selected >= 0 Then RaiseEvent RouteOpened(Me, _routes(_selected))
                Case Else : Return
            End Select
            e.Handled = True
        End Sub

        Private Sub DescribeSelection()
            If _selected < 0 OrElse _selected >= _routes.Count Then
                AccessibleDescription = Nothing
                Return
            End If
            Dim entry As RouteMapEntry = _routes(_selected)
            AccessibleDescription = ReminderService.SafeManuscriptTitle(entry.Manuscript) & ". " & RouteMapService.Brief(entry.Map) & ". " & RouteMapStyle.Summary(entry.Map)
            If Focused Then AccessibilityNotifyClients(AccessibleEvents.DescriptionChange, -1)
        End Sub

        Protected Overrides Sub OnGotFocus(e As EventArgs)
            MyBase.OnGotFocus(e)
            Invalidate()
        End Sub

        Protected Overrides Sub OnLostFocus(e As EventArgs)
            MyBase.OnLostFocus(e)
            Invalidate()
        End Sub

    End Class

End Namespace
