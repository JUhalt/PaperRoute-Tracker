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
' shelf shows a card at a time while the window has nothing else to do;
' the shelves not shown yet are put together once it has filled, and one
' shown before then is built the first time it is shown. Every card
' offers everything it always did once it shows; once the fill has
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

    ' The shelves not shown yet are put together once the shown shelf has
    ' filled, while the Board shows and the window idles (#133): the shelf
    ' being put together and how far it has got, and the shelves put
    ' together whose first screen still waits for its windows.
    Private shelfPreparationQueued As Boolean = False
    Private shelfInPreparation As ShelfPreparation = Nothing
    Private ReadOnly shelvesAwaitingWindows As New List(Of ManuscriptShelfPanel)()
    Private shelfCardInProgress As Control = Nothing

    Private NotInheritable Class ShelfPreparation
        Public Shelf As ManuscriptShelfPanel
        Public Entries As List(Of (Manuscript As Manuscript, Slot As (Id As Guid, Occurrence As Integer)))
        Public Position As Integer
        Public Placed As Integer
        Public Limit As Integer = FirstScreen
        Public AllOnShelf As Boolean
    End Class


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
        ' timer keeps going and the work waits for it to end. It waits for
        ' a page's cross-fade too (#142), at most the fade's length: a step
        ' of it would hold the fade's frames, and the picture of the page
        ' left would sit over the page shown until the step was done.
        If libraryWorkInProgress OrElse Not startupComplete OrElse Not IsHandleCreated OrElse IsDisposed OrElse
           (pageFade IsNot Nothing AndAlso pageFade.Fading) Then
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
        shelfInPreparation = Nothing
        shelvesAwaitingWindows.Clear()
        shelfCardInProgress = Nothing

        Dim shown As ManuscriptShelfPanel = ShownShelf()

        For Each shelf As ManuscriptShelfPanel In {pipelinePanel, publishedPanel, fileDrawerPanel}
            If shelf IsNot shown Then
                unbuiltShelves.Add(shelf)
            End If
        Next

        shelfRevealLimits(shown) = FirstScreen
        QueueBoardFill()

        ' Beside the fill, a step at a time: the manuscript page, then
        ' Journals, Insights and the Library.
        QueueManuscriptPagePreparation()
        QueueKeptPagesPreparation()

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
    ' hides more queues it again. Once the shown shelf has filled, the
    ' shelves not shown yet are prepared.
    Private Sub QueueBoardFill()

        Dim shelf As ManuscriptShelfPanel = ShownShelf()

        If boardFillQueued Then
            Return
        End If

        If Not shelfRevealLimits.ContainsKey(shelf) Then
            QueueShelfPreparation()
            Return
        End If

        If Not HidesCards(shelf) Then
            Return
        End If

        boardFillQueued = True
        QueueIdleWork(AddressOf RevealNextCard)

    End Sub


    ' The shelves not shown yet are put together once the shown shelf has
    ' filled (#133), a step at a time while the Board shows and the window
    ' idles, and never behind another page, as the fill waits there too:
    ' each shelf's cards, about a slice's worth a step, in the order the
    ' shelf shows them, each past its first screen hidden before it goes
    ' on the shelf; then the shelf rendered as a shelf shown for the first
    ' time is (BuildShelfIfUnbuilt); then, laid out hidden at the size it
    ' shows at, its first screen's windows, a card a step. The first
    ' switch to it then shows cards whose windows are made, and the fill
    ' goes on there. A shelf switched to before it is ready is built there
    ' and then, as before.
    Private Sub QueueShelfPreparation()

        If shelfPreparationQueued OrElse
           (shelfInPreparation Is Nothing AndAlso unbuiltShelves.Count = 0 AndAlso shelvesAwaitingWindows.Count = 0) Then
            Return
        End If

        shelfPreparationQueued = True
        QueueIdleWork(AddressOf PrepareShelvesStep)

    End Sub


    Private Sub PrepareShelvesStep()

        shelfPreparationQueued = False

        ' Behind another page, or while the shown shelf fills, it waits:
        ' the Board coming back, or the fill ending, queues it again.
        If IsDisposed OrElse Not boardPage.Visible OrElse currentPage <> WorkspacePage.Board OrElse shelfRevealLimits.ContainsKey(ShownShelf()) Then
            Return
        End If

        If shelfInPreparation IsNot Nothing AndAlso
           (shelfInPreparation.Shelf.IsDisposed OrElse Not unbuiltShelves.Contains(shelfInPreparation.Shelf)) Then
            shelfInPreparation = Nothing
        End If

        If shelfInPreparation Is Nothing Then
            Dim unbuilt As ManuscriptShelfPanel =
                {pipelinePanel, publishedPanel, fileDrawerPanel}.FirstOrDefault(Function(candidate) unbuiltShelves.Contains(candidate))
            If unbuilt IsNot Nothing Then
                shelfInPreparation = StartShelfPreparation(unbuilt)
            End If
        End If

        If shelfInPreparation IsNot Nothing Then
            Dim preparation As ShelfPreparation = shelfInPreparation
            If Not preparation.AllOnShelf Then
                preparation.AllOnShelf = PutCardsOnShelf(preparation, IdleWorkSlice)
            Else
                ' Every card is on the shelf: in a step of its own, the shelf
                ' is rendered as one shown for the first time is, which puts
                ' right whatever changed since the preparation began, and is
                ' laid out at the size it shows at: a hidden shelf takes no
                ' part in its host's layout.
                shelfInPreparation = Nothing
                If preparation.Shelf.Parent IsNot Nothing Then
                    preparation.Shelf.Bounds = preparation.Shelf.Parent.DisplayRectangle
                End If
                BuildShelfIfUnbuilt(preparation.Shelf)
                If Not shelvesAwaitingWindows.Contains(preparation.Shelf) Then
                    shelvesAwaitingWindows.Add(preparation.Shelf)
                End If
            End If
            QueueShelfPreparation()
            Return
        End If

        MakeShelfWindowsStep()
        QueueShelfPreparation()

    End Sub


    ' The manuscripts a shelf shows, in its order, with the slot each
    ' card is kept in, as RenderManuscripts would put them on it now.
    Private Function StartShelfPreparation(shelf As ManuscriptShelfPanel) As ShelfPreparation

        Dim location As ManuscriptLocation = ShelfLocation(shelf)
        Dim seen As New Dictionary(Of Guid, Integer)()
        Dim entries As New List(Of (Manuscript As Manuscript, Slot As (Id As Guid, Occurrence As Integer)))()

        For Each manuscript As Manuscript In GetVisibleManuscripts()
            Dim slot As (Id As Guid, Occurrence As Integer) = CardSlot(manuscript, seen)
            If manuscript.Location = location Then
                entries.Add((manuscript, slot))
            End If
        Next

        Return New ShelfPreparation With {.Shelf = shelf, .Entries = entries}

    End Function


    ' Puts the next cards on a shelf not shown yet, for about the given
    ' time; True once every card is on it. A card past the first screen is
    ' hidden before it goes on, so it gets no window until the fill
    ' reaches it. What changed since the preparation began (a save, a
    ' search) is put right when the shelf is rendered next.
    Private Function PutCardsOnShelf(preparation As ShelfPreparation, milliseconds As Long) As Boolean

        Dim shelf As ManuscriptShelfPanel = preparation.Shelf
        Dim location As ManuscriptLocation = ShelfLocation(shelf)
        Dim watch As Stopwatch = Stopwatch.StartNew()

        shelf.SuspendLayout()

        Try

            While preparation.Position < preparation.Entries.Count

                If watch.ElapsedMilliseconds >= milliseconds Then
                    Return False
                End If

                Dim entry As (Manuscript As Manuscript, Slot As (Id As Guid, Occurrence As Integer)) = preparation.Entries(preparation.Position)
                preparation.Position += 1

                If entry.Manuscript Is Nothing OrElse entry.Manuscript.Location <> location Then
                    Continue While
                End If

                Dim card As RoundedPanel = CardFor(entry.Manuscript, entry.Slot, shelf)
                Dim record As CardRecord = DirectCast(card.Tag, CardRecord)
                Dim index As Integer = preparation.Placed
                preparation.Placed += 1

                If preparation.Limit = FirstScreen Then
                    preparation.Limit = FirstScreenCount(shelf, card.Height)
                End If

                ' As ApplyRevealLimit hides and shows a card.
                Dim hidden As Boolean = index >= preparation.Limit
                If record.HiddenByFill <> hidden Then
                    record.HiddenByFill = hidden
                    card.Visible = Not hidden
                End If

                If card.Parent IsNot shelf Then
                    shelf.Controls.Add(card)
                End If

                If shelf.Controls.GetChildIndex(card) <> index Then
                    shelf.Controls.SetChildIndex(card, index)
                End If

            End While

            Return True

        Finally
            shelf.ResumeLayout(False)
        End Try

    End Function


    ' One card of a prepared shelf's first screen gets its windows, made
    ' inside the hidden shelf, where they stay; the shelf is first given
    ' the size it shows at, since a hidden shelf takes no part in its
    ' host's layout, and its own window. A card's menu is built as its
    ' window is made (CardPanel).
    Private Sub MakeShelfWindowsStep()

        While shelvesAwaitingWindows.Count > 0

            Dim shelf As ManuscriptShelfPanel = shelvesAwaitingWindows(0)

            If shelf.IsDisposed OrElse shelf.Visible OrElse unbuiltShelves.Contains(shelf) OrElse
               shelf.Parent Is Nothing OrElse Not shelf.Parent.IsHandleCreated Then
                shelvesAwaitingWindows.RemoveAt(0)
                Continue While
            End If

            ' The shelf's own window, in a step of its own; the shelf lays
            ' itself out again as it is made, its scroll bar with it.
            If Not shelf.IsHandleCreated Then
                shelf.Bounds = shelf.Parent.DisplayRectangle
                Dim made As IntPtr = shelf.Handle
                Return
            End If

            ' A card a step; one not finished in its slice goes on next step.
            Dim waiting As Control = shelfCardInProgress

            If waiting Is Nothing OrElse waiting.IsDisposed OrElse waiting.Parent IsNot shelf OrElse
               (TypeOf waiting.Tag Is CardRecord AndAlso DirectCast(waiting.Tag, CardRecord).HiddenByFill) Then
                waiting = shelf.Controls.Cast(Of Control)().FirstOrDefault(
                    Function(card) Not card.IsHandleCreated AndAlso
                                   Not (TypeOf card.Tag Is CardRecord AndAlso DirectCast(card.Tag, CardRecord).HiddenByFill))
            End If

            If waiting Is Nothing Then
                shelfCardInProgress = Nothing
                shelvesAwaitingWindows.RemoveAt(0)
                Continue While
            End If

            shelfCardInProgress = If(InPlace.MakeWindows(waiting, CInt(IdleWorkSlice)), Nothing, waiting)
            Return

        End While

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


    ' A shelf still filling shows at least the cards that fill its view and
    ' one row more: a shelf prepared hidden measured its first screen by
    ' the window as it was then.
    Private Sub RevealFirstScreen(shelf As ManuscriptShelfPanel)

        If Not shelfRevealLimits.ContainsKey(shelf) OrElse shelfRevealLimits(shelf) = FirstScreen Then
            Return
        End If

        Dim first As Control = shelf.Controls.Cast(Of Control)().FirstOrDefault(Function(card) TypeOf card.Tag Is CardRecord)

        If first Is Nothing Then
            Return
        End If

        Dim screen As Integer = FirstScreenCount(shelf, first.Height)

        If screen > shelfRevealLimits(shelf) Then
            RevealThrough(shelf, screen)
        End If

    End Sub


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
