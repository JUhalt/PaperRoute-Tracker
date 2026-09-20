Imports System.Drawing
Imports System.Linq
Imports System.Reflection
Imports System.Runtime.ExceptionServices
Imports System.Threading
Imports System.Windows.Forms
Imports ManuscriptPipeline
Imports ManuscriptPipeline.Models
Imports Microsoft.VisualStudio.TestTools.UnitTesting

<TestClass>
<DoNotParallelize>
Public Class DashboardShelfLayoutTests

    <TestMethod>
    <DataRow(1.0F)>
    <DataRow(1.25F)>
    <DataRow(1.5F)>
    Public Sub MinimumWindowKeepsEveryShelfAndHeaderVisible(fontScale As Single)
        RunOnStaThread(
            Sub()
                Using board As New LayoutOnlyBoard()
                    board.Prepare(fontScale)
                    board.Show()
                    board.Size = board.MinimumSize
                    Application.DoEvents()
                    For Each header As Label In board.ShelfHeaders
                        AssertInsideAncestors(header, "Shelf header " & header.Text)
                    Next
                    For Each shelf As FlowLayoutPanel In board.Shelves
                        AssertInsideAncestors(shelf, "Shelf viewport")
                        Assert.IsTrue(shelf.ClientSize.Height >= 2 * SystemInformation.VerticalScrollBarArrowHeight + 8,
                            "Each shelf needs room for usable vertical scroll controls.")
                        Dim lastCard As Control = shelf.Controls(shelf.Controls.Count - 1)
                        For Each button As Button In Descendants(lastCard).OfType(Of Button)()
                            shelf.ScrollControlIntoView(button)
                            Application.DoEvents()
                            AssertInsideAncestors(button, "Scrolled card action " & button.Text)
                        Next
                    Next
                    board.Close()
                End Using
            End Sub)
    End Sub

    Private Shared Sub AssertInsideAncestors(control As Control, context As String)
        Dim rectangle As Rectangle = control.RectangleToScreen(control.ClientRectangle)
        Dim ancestor As Control = control.Parent
        While ancestor IsNot Nothing
            Dim viewport As Rectangle = ancestor.RectangleToScreen(ancestor.ClientRectangle)
            Assert.IsTrue(viewport.Contains(rectangle),
                $"{context}: {rectangle} exceeds {ancestor.GetType().Name} viewport {viewport}.")
            ancestor = ancestor.Parent
        End While
    End Sub

    <TestMethod>
    Public Sub ShelvesFitAfterMaximizingAndRestoringWindow()
        RunOnStaThread(
            Sub()
                Using board As New LayoutOnlyBoard()
                    board.Prepare(1.0F)
                    board.Opacity = 0
                    board.Show()
                    board.Size = board.MinimumSize
                    Application.DoEvents()
                    For cycle As Integer = 1 To 3
                        board.WindowState = FormWindowState.Maximized
                        Application.DoEvents()
                        board.WindowState = FormWindowState.Normal
                        Application.DoEvents()
                        For Each shelf As FlowLayoutPanel In board.Shelves
                            Assert.IsFalse(shelf.HorizontalScroll.Visible,
                                $"Restore cycle {cycle}: shelf {shelf.ClientSize}, display {shelf.DisplayRectangle}, first card {shelf.Controls(0).Bounds}.")
                        Next
                    Next
                    board.Close()
                End Using
            End Sub)
    End Sub

    <TestMethod>
    <DataRow(1.0F)>
    <DataRow(1.25F)>
    <DataRow(1.5F)>
    Public Sub ShelvesDoNotRetainHorizontalRangeThroughResizeCycles(fontScale As Single)
        ' Larger text exercises layout pressure. These are not native Windows DPI tests.
        RunOnStaThread(
            Sub()
                Using board As New LayoutOnlyBoard()
                    board.Prepare(fontScale)
                    board.Show()
                    For Each width As Integer In {900, 2200, 900, 1400, 900, 1180, 900}
                        board.Size = New Size(width, 900)
                        Application.DoEvents()
                        For Each shelf As FlowLayoutPanel In board.Shelves
                            Dim diagnostic = $"font scale {fontScale}, window width {width}, shelf {shelf.ClientSize}, display {shelf.DisplayRectangle}, scroll {shelf.AutoScrollPosition}"
                            Assert.IsFalse(shelf.HorizontalScroll.Visible, diagnostic)
                            Assert.IsTrue(shelf.VerticalScroll.Visible, "Populated shelves must scroll vertically: " & diagnostic)
                            Assert.AreEqual(0, shelf.AutoScrollPosition.X, diagnostic)
                            Assert.IsTrue(shelf.DisplayRectangle.Width <= shelf.ClientSize.Width, diagnostic)
                            For Each card As Control In shelf.Controls
                                Assert.IsTrue(card.Right + card.Margin.Right <= shelf.ClientSize.Width - shelf.Padding.Right,
                                    "Every card must fit the visible shelf width: " & diagnostic)
                                For Each button As Button In Descendants(card).OfType(Of Button)()
                                    Assert.IsTrue(button.Parent.ClientRectangle.Contains(button.Bounds),
                                        "Card action must fit without horizontal scrolling: " & button.Text & ", " & diagnostic)
                                    Assert.IsTrue(button.TabStop AndAlso button.Enabled, "Card actions remain keyboard eligible.")
                                Next
                                Dim route = card.Controls.OfType(Of Label)().Single(Function(label) label.Text.StartsWith("View route"))
                                Assert.IsTrue(card.ClientRectangle.Contains(route.Bounds), "View route must fit inside its card.")
                            Next
                            shelf.ScrollControlIntoView(shelf.Controls(shelf.Controls.Count - 1))
                            Application.DoEvents()
                            Assert.IsTrue(shelf.AutoScrollPosition.Y < 0, "Last card must remain reachable by vertical scrolling.")
                            Assert.AreEqual(0, shelf.AutoScrollPosition.X, diagnostic)
                        Next
                    Next
                    board.Close()
                End Using
            End Sub)
    End Sub

    <TestMethod>
    Public Sub ShelfRangesRecalculateWhenCardsAreReplacedWithEmptyRows()
        RunOnStaThread(
            Sub()
                Using board As New LayoutOnlyBoard()
                    board.Prepare(1.0F)
                    board.Show()
                    For Each sampleCount As Integer In {0, 5, 1, 0}
                        board.SetSamples(sampleCount)
                        For Each width As Integer In {900, 1400, 900}
                            board.Size = New Size(width, 900)
                            Application.DoEvents()
                            For Each shelf As FlowLayoutPanel In board.Shelves
                                Assert.IsFalse(shelf.HorizontalScroll.Visible,
                                    $"Replacing rows must clear stale horizontal extent: {sampleCount} cards, width {width}, display {shelf.DisplayRectangle}.")
                                Assert.AreEqual(0, shelf.AutoScrollPosition.X)
                                If sampleCount = 0 Then
                                    Assert.IsFalse(shelf.VerticalScroll.Visible,
                                        $"The empty shelf must clear obsolete vertical range: width {width}, shelf {shelf.ClientSize}, display {shelf.DisplayRectangle}, row {shelf.Controls(0).Bounds}.")
                                End If
                            Next
                        Next
                    Next
                    board.Close()
                End Using
            End Sub)
    End Sub

    Private Shared Iterator Function Descendants(parent As Control) As IEnumerable(Of Control)
        For Each child As Control In parent.Controls
            Yield child
            For Each descendant As Control In Descendants(child)
                Yield descendant
            Next
        Next
    End Function

    Private Class LayoutOnlyBoard
        Inherits Form1

        ' Skip the normal Load event, which reads repositories and checks updates.
        ' Repository constructors only resolve paths; this fixture never loads or saves.
        Protected Overrides Sub OnLoad(e As EventArgs)
        End Sub

        Protected Overrides ReadOnly Property ShowWithoutActivation As Boolean
            Get
                Return True
            End Get
        End Property

        Public Sub Prepare(fontScale As Single)
            InvokePrivate("BuildInterface")
            Font = New Font(Font.FontFamily, 10.0F * fontScale)
            SetSamples(5)
            StartPosition = FormStartPosition.Manual
            Location = New Point(-20000, -20000)
            ShowInTaskbar = False
        End Sub

        Public Sub SetSamples(count As Integer)
            Dim samples As New List(Of Manuscript)
            For Each location As ManuscriptLocation In [Enum].GetValues(Of ManuscriptLocation)()
                For index As Integer = 1 To count
                    samples.Add(New Manuscript With {
                        .Title = "Synthetic shelf layout manuscript with a long title " & index,
                        .Location = location,
                        .CurrentStage = If(location = ManuscriptLocation.Published, PaperStage.Published, PaperStage.Draft),
                        .TargetJournal = "A fictional journal with a long descriptive name"
                    })
                Next
            Next
            GetType(Form1).GetField("manuscripts", BindingFlags.Instance Or BindingFlags.NonPublic).SetValue(Me, samples)
            InvokePrivate("RenderManuscripts")
        End Sub

        Public ReadOnly Property Shelves As IEnumerable(Of FlowLayoutPanel)
            Get
                Return {"pipelinePanel", "publishedPanel", "fileDrawerPanel"}.Select(
                    Function(name) DirectCast(GetType(Form1).GetField(name, BindingFlags.Instance Or BindingFlags.NonPublic).GetValue(Me), FlowLayoutPanel))
            End Get
        End Property

        Public ReadOnly Property ShelfHeaders As IEnumerable(Of Label)
            Get
                Return {"lblPipelineHeader", "lblPublishedHeader", "lblFileDrawerHeader"}.Select(
                    Function(name) DirectCast(GetType(Form1).GetField(name, BindingFlags.Instance Or BindingFlags.NonPublic).GetValue(Me), Label))
            End Get
        End Property

        Private Sub InvokePrivate(name As String)
            GetType(Form1).GetMethod(name, BindingFlags.Instance Or BindingFlags.NonPublic).Invoke(Me, Nothing)
        End Sub
    End Class

    Private Shared Sub RunOnStaThread(action As Action)
        Dim failure As Exception = Nothing
        Dim thread As New Thread(
            Sub()
                Try
                    action()
                Catch ex As Exception
                    failure = ex
                End Try
            End Sub) With {.IsBackground = True}
        thread.SetApartmentState(ApartmentState.STA)
        thread.Start()
        Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(30)), "Shelf layout test timed out.")
        If failure IsNot Nothing Then ExceptionDispatchInfo.Capture(failure).Throw()
    End Sub
End Class
