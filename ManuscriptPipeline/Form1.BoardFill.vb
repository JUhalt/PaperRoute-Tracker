Imports System
Imports System.Collections.Generic
Imports System.Diagnostics
Imports System.Linq
Imports System.Windows.Forms
Imports ManuscriptPipeline.Controls
Imports ManuscriptPipeline.Models

' The Board fills in after its first screen (#133). At startup only the
' shelf shown is built, and only as many of its cards as fill the screen
' get their windows before the Board is interactive; the rest of that
' shelf shows a card at a time while the window has nothing else to do,
' and a shelf not shown yet is built the first time it is shown. Every
' card offers everything it always did once it shows; once the fill has
' passed them all the Board is the Board it always was. Making a card's
' windows was most of the time startup took on a large library.
Partial Public Class Form1

    ' Work that waits until the window has shown and has nothing else to
    ' do, a slice of a few milliseconds at a time between messages, and
    ' never while a save, backup or restore is running (#132).
    Private ReadOnly idleWork As New Queue(Of Action)()
    Private WithEvents idleWorkTimer As New Timer()
    Private idleWorkReady As Boolean = False

    ' The first slice waits a little after the window shows, so the first
    ' screen is drawn before any of the rest.
    Private Const IdleWorkFirstDelay As Integer = 100
    Private Const IdleWorkInterval As Integer = 15
    Private Const IdleWorkSlice As Long = 30

    ' Shelves not built yet, and how many of its leading cards a shelf
    ' still filling shows. A shelf in neither is the Board as it always was.
    Private ReadOnly unbuiltShelves As New HashSet(Of ManuscriptShelfPanel)()
    Private ReadOnly shelfRevealLimits As New Dictionary(Of ManuscriptShelfPanel, Integer)()

    ' A limit not measured yet: the first screen, once a card shows how
    ' tall cards are.
    Private Const FirstScreen As Integer = -1

    Private boardFillQueued As Boolean = False


    ' True while idle work waits to run; tests drain it.
    Friend ReadOnly Property IsIdleWorkPending As Boolean
        Get
            Return idleWork.Count > 0
        End Get
    End Property


    ' True while the shown shelf still hides cards it will show as the
    ' window idles.
    Friend ReadOnly Property IsBoardFillPending As Boolean
        Get
            Dim shelf As ManuscriptShelfPanel = ShownShelf()
            Return shelfRevealLimits.ContainsKey(shelf) AndAlso HidesCards(shelf)
        End Get
    End Property


    Private Sub QueueIdleWork(work As Action)

        idleWork.Enqueue(work)

        If idleWorkReady AndAlso Not idleWorkTimer.Enabled Then
            idleWorkTimer.Interval = IdleWorkInterval
            idleWorkTimer.Start()
        End If

    End Sub


    ' Idle work starts once the window has shown: posted when it becomes
    ' visible, it runs after the messages the first show queued, so no
    ' slice of it is part of showing the window.
    Protected Overrides Sub OnVisibleChanged(e As EventArgs)

        MyBase.OnVisibleChanged(e)

        If Visible AndAlso Not idleWorkReady AndAlso IsHandleCreated Then
            BeginInvoke(New Action(AddressOf StartIdleWork))
        End If

    End Sub


    Private Sub StartIdleWork()

        If idleWorkReady OrElse IsDisposed Then
            Return
        End If

        idleWorkReady = True

        If idleWork.Count > 0 Then
            idleWorkTimer.Interval = IdleWorkFirstDelay
            idleWorkTimer.Start()
        End If

    End Sub


    Private Sub IdleWorkTimer_Tick(sender As Object, e As EventArgs) Handles idleWorkTimer.Tick

        idleWorkTimer.Interval = IdleWorkInterval

        ' Library work reads the records the work would read (#132); the
        ' timer keeps going and the work waits for it to end.
        If libraryWorkInProgress OrElse Not startupComplete OrElse Not IsHandleCreated OrElse IsDisposed Then
            Return
        End If

        Dim slice As Stopwatch = Stopwatch.StartNew()

        While idleWork.Count > 0 AndAlso slice.ElapsedMilliseconds < IdleWorkSlice
            idleWork.Dequeue()()
        End While

        If idleWork.Count = 0 Then
            idleWorkTimer.Stop()
        End If

    End Sub


    ' Startup, before the first render: the shelves not shown wait to be
    ' built, and the one shown shows its first screen of cards, then the
    ' rest as the window idles.
    Private Sub DeferBoardFill()

        unbuiltShelves.Clear()
        shelfRevealLimits.Clear()

        Dim shown As ManuscriptShelfPanel = ShownShelf()

        For Each shelf As ManuscriptShelfPanel In {pipelinePanel, publishedPanel, fileDrawerPanel}
            If shelf IsNot shown Then
                unbuiltShelves.Add(shelf)
            End If
        Next

        shelfRevealLimits(shown) = FirstScreen
        QueueBoardFill()

    End Sub


    Private Function ShownShelf() As ManuscriptShelfPanel

        If tabPublished.Checked Then Return publishedPanel
        If tabFileDrawer.Checked Then Return fileDrawerPanel
        Return pipelinePanel

    End Function


    ' A shelf shown for the first time is built now, showing its first
    ' screen; the rest follows as the window idles.
    Private Sub BuildShelfIfUnbuilt(shelf As ManuscriptShelfPanel)

        If unbuiltShelves.Remove(shelf) Then
            shelfRevealLimits(shelf) = FirstScreen
            RenderManuscripts()
        End If

    End Sub


    ' Hides the cards of a shelf still filling past its limit, before they
    ' go on the shelf, so they get no windows until they show. A card
    ' shows again once the limit reaches it; with no limit, every card
    ' shows.
    Private Sub ApplyRevealLimit(shelf As ManuscriptShelfPanel, cards As List(Of Control))

        If Not shelfRevealLimits.ContainsKey(shelf) Then
            For Each card As Control In cards
                Dim record As CardRecord = TryCast(card.Tag, CardRecord)
                If record IsNot Nothing AndAlso record.HiddenByFill Then
                    record.HiddenByFill = False
                    card.Visible = True
                End If
            Next
            Return
        End If

        Dim limit As Integer = shelfRevealLimits(shelf)

        If limit = FirstScreen Then
            Dim first As Control = cards.FirstOrDefault(Function(card) TypeOf card.Tag Is CardRecord)
            If first Is Nothing Then Return
            limit = FirstScreenCount(shelf, first.Height)
            shelfRevealLimits(shelf) = limit
            If EndFillWhenComplete(shelf) Then
                ApplyRevealLimit(shelf, cards)
                Return
            End If
        End If

        Dim index As Integer = 0

        For Each card As Control In cards
            Dim record As CardRecord = TryCast(card.Tag, CardRecord)
            If record Is Nothing Then Continue For
            Dim hidden As Boolean = index >= limit
            If record.HiddenByFill <> hidden Then
                record.HiddenByFill = hidden
                card.Visible = Not hidden
            End If
            index += 1
        Next

    End Sub


    ' The cards that fill the shelf's view, and one row more.
    Private Function FirstScreenCount(shelf As ManuscriptShelfPanel, cardHeight As Integer) As Integer

        Dim columns As Integer = ShelfColumns(shelf)
        Dim height As Integer = ShelfView(shelf).Height
        Dim rows As Integer = If(height > 0, CInt(Math.Ceiling(height / CDbl(Math.Max(1, cardHeight + shelf.CardGap)))), 4)

        Return columns * (rows + 1)

    End Function


    ' The view a shelf fills. A shelf not shown yet keeps the size it was
    ' made at, as WinForms lays out no hidden control, so it is measured
    ' by the host it fills once shown (Dock = Fill): built as it is first
    ' shown, it shows its first screen at once.
    Private Shared Function ShelfView(shelf As ManuscriptShelfPanel) As Size

        If Not shelf.Visible AndAlso shelf.Parent IsNot Nothing Then
            Return shelf.Parent.DisplayRectangle.Size
        End If

        Return shelf.ClientSize

    End Function


    ' As many columns as CardGridLayout puts on the shelf at its width.
    Private Function ShelfColumns(shelf As ManuscriptShelfPanel) As Integer

        UpdateShelfGrid(shelf)

        Dim available As Integer = ShelfView(shelf).Width - shelf.Padding.Horizontal

        If available <= 0 Then
            Return 3
        End If

        Dim gap As Integer = Math.Max(0, shelf.CardGap)
        Return Math.Max(1, (available + gap) \ (Math.Max(1, shelf.MinimumCardWidth) + gap))

    End Function


    ' Queues the next card while the shown shelf hides cards; a render that
    ' hides more queues it again.
    Private Sub QueueBoardFill()

        Dim shelf As ManuscriptShelfPanel = ShownShelf()

        If boardFillQueued OrElse Not shelfRevealLimits.ContainsKey(shelf) OrElse Not HidesCards(shelf) Then
            Return
        End If

        boardFillQueued = True
        QueueIdleWork(AddressOf RevealNextCard)

    End Sub


    Private Shared Function HidesCards(shelf As ManuscriptShelfPanel) As Boolean
        Return shelf.Controls.Cast(Of Control)().Any(Function(card) TypeOf card.Tag Is CardRecord AndAlso DirectCast(card.Tag, CardRecord).HiddenByFill)
    End Function


    ' One idle step: the next card of the shown shelf. A slice runs as many
    ' as fit, so no step holds the window longer than one card does. While
    ' another page covers the Board it waits, since cards shown there would
    ' all make their windows at once when the Board comes back (ShowPage
    ' resumes).
    Private Sub RevealNextCard()

        boardFillQueued = False

        If Not boardPage.Visible OrElse currentPage <> WorkspacePage.Board Then
            Return
        End If

        Dim shelf As ManuscriptShelfPanel = ShownShelf()

        If Not shelfRevealLimits.ContainsKey(shelf) OrElse shelfRevealLimits(shelf) = FirstScreen Then
            Return
        End If

        RevealThrough(shelf, shelfRevealLimits(shelf) + 1)
        QueueBoardFill()

    End Sub


    ' Shows the shelf's cards up to count; the fill of the shelf ends once
    ' the count reaches every manuscript on it.
    Private Sub RevealThrough(shelf As ManuscriptShelfPanel, count As Integer)

        If count > shelfRevealLimits(shelf) Then
            shelfRevealLimits(shelf) = count
        End If

        EndFillWhenComplete(shelf)

        shelf.SuspendLayout()
        ApplyRevealLimit(shelf, shelf.Controls.Cast(Of Control)().ToList())
        shelf.ResumeLayout(True)

    End Sub


    ' True when the shelf's limit has reached every manuscript on it, and
    ' the shelf is the Board's own again.
    Private Function EndFillWhenComplete(shelf As ManuscriptShelfPanel) As Boolean

        Dim location As ManuscriptLocation = ShelfLocation(shelf)
        Dim total As Integer = manuscripts.Where(Function(item) item IsNot Nothing AndAlso item.Location = location).Count()

        If shelfRevealLimits(shelf) >= total Then
            shelfRevealLimits.Remove(shelf)
            Return True
        End If

        Return False

    End Function


    Private Function ShelfLocation(shelf As ManuscriptShelfPanel) As ManuscriptLocation

        If shelf Is publishedPanel Then Return ManuscriptLocation.Published
        If shelf Is fileDrawerPanel Then Return ManuscriptLocation.FileDrawer
        Return ManuscriptLocation.Pipeline

    End Function


    ' Focus entering a card shows the row after it, so Tab always reaches
    ' the next card while the shelf fills.
    Private Sub RevealPastCard(card As Control)

        Dim shelf As ManuscriptShelfPanel = TryCast(card.Parent, ManuscriptShelfPanel)

        If shelf Is Nothing OrElse Not shelfRevealLimits.ContainsKey(shelf) Then
            Return
        End If

        Dim columns As Integer = ShelfColumns(shelf)
        Dim index As Integer = shelf.Controls.GetChildIndex(card)
        Dim through As Integer = ((index \ columns) + 2) * columns

        If through > shelfRevealLimits(shelf) Then
            RevealThrough(shelf, through)
        End If

    End Sub

End Class
