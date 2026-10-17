Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.IO
Imports System.Linq
Imports System.Reflection
Imports System.Windows.Forms
Imports ManuscriptPipeline
Imports ManuscriptPipeline.Controls
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services
Imports Microsoft.VisualStudio.TestTools.UnitTesting

' The Board's cards (#133): built and shown with their windows only where
' they are needed, every card offering everything a card always did.
<TestClass>
<DoNotParallelize>
Public Class BoardFillTests

    Private _root As String = String.Empty


    <TestInitialize>
    Public Sub Initialize()
        _root = CreateTemporaryRoot()
        StorageEnvironment.RootOverride = Path.Combine(_root, "storage")
    End Sub


    <TestCleanup>
    Public Sub Cleanup()
        StorageEnvironment.RootOverride = Nothing
        DeleteTemporaryRoot(_root)
    End Sub


    ' A change to one card's text rebuilds that card alone: every other
    ' card keeps its window and its place, and the shelf stays scrolled
    ' where it was. Before, every card left the shelf and came back, its
    ' windows moved off and on again, after a save that renamed one.
    <TestMethod>
    Public Sub ReplacingOneCardLeavesTheOthersParented()

        StaRunner.Run(
            Sub()
                Dim library As LargeLibraryFixture.SyntheticLibrary = Library120()

                Using board As New LibraryBoard(library, Path.Combine(_root, "settings"))

                    board.Start()
                    Dim pipeline As ManuscriptShelfPanel = Shelf(board, "pipelinePanel")
                    Dim cards As List(Of RoundedPanel) = BoardFillTests.Cards(pipeline)
                    Dim renamed As RoundedPanel = cards(cards.Count \ 2)

                    pipeline.AutoScrollPosition = New Point(0, 400)
                    Application.DoEvents()
                    Dim scrolled As Point = pipeline.AutoScrollPosition
                    Assert.IsTrue(scrolled.Y < 0, "The shelf is scrolled.")

                    Dim moved As New List(Of String)()
                    For Each card As RoundedPanel In cards.Where(Function(item) item IsNot renamed)
                        Dim name As String = card.AccessibleName
                        AddHandler card.ParentChanged, Sub(sender, e) moved.Add(name & " left the shelf")
                        AddHandler card.HandleDestroyed, Sub(sender, e) moved.Add(name & " lost its window")
                    Next

                    Dim subject As Manuscript = board.Library.First(Function(item) item.Title = renamed.AccessibleName)
                    subject.Title &= " (renamed)"
                    board.Render()
                    Application.DoEvents()

                    Assert.AreEqual(0, moved.Count, String.Join(Environment.NewLine, moved))
                    Assert.IsTrue(renamed.IsDisposed, "The renamed card is rebuilt.")
                    Assert.AreEqual(subject.Title, pipeline.Controls(cards.IndexOf(renamed)).AccessibleName, "Its new card takes its place.")
                    Assert.AreEqual(scrolled, pipeline.AutoScrollPosition, "The shelf stays where it was scrolled.")

                    board.Close()
                End Using
            End Sub,
            TimeSpan.FromMinutes(5))

    End Sub


    ' ---------------------------------------------------------------

    Private Function Library120() As LargeLibraryFixture.SyntheticLibrary
        Return LargeLibraryFixture.Create(Path.Combine(_root, "library"), 120)
    End Function


    Friend Shared Function Cards(shelf As Control) As List(Of RoundedPanel)
        Return shelf.Controls.OfType(Of RoundedPanel)().ToList()
    End Function


    Private Shared Function Shelf(board As LibraryBoard, name As String) As ManuscriptShelfPanel
        Return DirectCast(GetType(Form1).GetField(name, BindingFlags.Instance Or BindingFlags.NonPublic).GetValue(board), ManuscriptShelfPanel)
    End Function

End Class
