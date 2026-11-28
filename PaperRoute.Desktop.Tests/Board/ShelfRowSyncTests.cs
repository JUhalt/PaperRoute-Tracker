using System.Collections.Specialized;
using ManuscriptPipeline.Models;
using PaperRoute.ViewModels.Board;

namespace PaperRoute.Desktop.Tests.Board;

// The rows of a hidden shelf, without a window: SyncStaleRows brings rows
// that wait for the shelf to be shown to its cards exactly as showing the
// shelf does (ShelfViewModel.IsShown), and the width set before the shelf is
// realized rewraps its rows at once, so none wait. The Board's idle
// preparation of the hidden Published shelf (HiddenShelfPreparer) stands on
// both.
[TestClass]
public sealed class ShelfRowSyncTests : StorageCheckedTests
{
    // Ten Published papers, five of them "odd", and two Pipeline drafts.
    private static List<Manuscript> Library() =>
    [
        .. Enumerable.Range(1, 10).Select(number =>
            Papers.Paper($"Paper {number:00} {(number % 2 == 1 ? "odd" : "even")}", PaperStage.Published, ManuscriptLocation.Published)),
        Papers.Paper("Draft A"),
        Papers.Paper("Draft B"),
    ];

    [TestMethod]
    [DataRow("", "odd", DisplayName = "fewer cards while hidden")]
    [DataRow("odd", "", DisplayName = "more cards while hidden")]
    public void SyncStaleRowsBringsTheRowsToTheCardsAsShowingTheShelfDoes(string shownSearch, string hiddenSearch)
    {
        BoardHarness called = Stale(shownSearch, hiddenSearch);
        BoardHarness shown = Stale(shownSearch, hiddenSearch);
        Assert.IsTrue(called.Published.RowsAreStale, "The search while hidden leaves the rows waiting.");
        Assert.AreEqual(Shape(shown.Published), Shape(called.Published), "The twin Boards wait with the same rows.");
        List<CardRowViewModel> before = called.Published.Rows.ToList();

        called.Published.SyncStaleRows();
        shown.Board.SelectedShelfIndex = 1;

        Assert.IsFalse(called.Published.RowsAreStale, "SyncStaleRows leaves no row waiting.");
        Assert.IsFalse(shown.Published.RowsAreStale, "Showing the shelf leaves no row waiting.");
        Assert.AreEqual(Shape(shown.Published), Shape(called.Published), "SyncStaleRows gives the rows showing the shelf gives.");
        Assert.AreEqual(Expected(called.Published), Shape(called.Published), "The rows hold the cards in rows of the columns.");
        int kept = Math.Min(before.Count, called.Published.Rows.Count);
        Assert.IsTrue(called.Published.Rows.Take(kept).Select((row, index) => ReferenceEquals(row, before[index])).All(same => same),
            "The rows that stay are the same objects, refilled.");

        // Current rows: neither another call nor showing the shelf changes them.
        List<CardRowViewModel> current = called.Published.Rows.ToList();
        var changes = new List<NotifyCollectionChangedAction>();
        called.Published.Rows.CollectionChanged += (_, e) => changes.Add(e.Action);
        called.Published.SyncStaleRows();
        called.Board.SelectedShelfIndex = 1;
        Assert.IsEmpty(changes, "Rows already current are not changed again.");
        CollectionAssert.AreEqual(current, called.Published.Rows.ToList(), "The same row objects.");
    }

    [TestMethod]
    public void TheWidthSetBeforeTheShelfIsRealizedLeavesNoStaleRows()
    {
        var harness = new BoardHarness(Library());
        ShelfViewModel published = harness.Published;
        Assert.IsFalse(published.IsRealized, "Published was never shown.");
        Assert.AreEqual(1, published.Columns, "No width yet: one column.");

        published.SetWidth(1000);
        published.IsRealized = true;
        Assert.IsFalse(published.IsShown, "Still hidden.");
        Assert.IsFalse(published.RowsAreStale, "The width set first rewrapped the rows at once.");
        Assert.AreEqual(3, published.Columns);
        Assert.AreEqual(Expected(published), Shape(published), "The rows hold the cards in rows of three.");

        // The other order: realized first, the new width leaves the rows
        // waiting for the shelf to be shown.
        var other = new BoardHarness(Library());
        other.Published.IsRealized = true;
        other.Published.SetWidth(1000);
        Assert.IsTrue(other.Published.RowsAreStale, "Realized and hidden, a new width waits.");
        other.Published.SyncStaleRows();
        Assert.AreEqual(Shape(published), Shape(other.Published), "Brought current, the same rows.");
    }

    // A Board whose Published shelf was shown at 1000 px (three columns)
    // with one search, hidden, and searched again.
    private static BoardHarness Stale(string shownSearch, string hiddenSearch)
    {
        var harness = new BoardHarness(Library());
        harness.Published.SetWidth(1000);
        harness.Search(shownSearch);
        harness.Board.SelectedShelfIndex = 1;
        harness.Board.SelectedShelfIndex = 0;
        harness.Search(hiddenSearch);
        return harness;
    }

    // The rows as row index, card titles and the last mark.
    private static string Shape(ShelfViewModel shelf) =>
        string.Join(" | ", shelf.Rows.Select(row =>
            $"{row.Index}:{string.Join(",", row.Cards.Select(card => card.Title))}{(row.IsLast ? " last" : string.Empty)}"));

    // The shape the cards give in rows of the shelf's columns.
    private static string Expected(ShelfViewModel shelf)
    {
        int columns = Math.Max(1, shelf.Columns);
        List<CardViewModel> cards = shelf.Cards.ToList();
        int count = (cards.Count + columns - 1) / columns;
        return string.Join(" | ", Enumerable.Range(0, count).Select(index =>
            $"{index}:{string.Join(",", cards.Skip(index * columns).Take(columns).Select(card => card.Title))}{(index == count - 1 ? " last" : string.Empty)}"));
    }
}
