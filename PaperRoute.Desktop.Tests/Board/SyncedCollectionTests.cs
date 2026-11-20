using System.Collections.Specialized;
using PaperRoute.ViewModels.Board;

namespace PaperRoute.Desktop.Tests.Board;

// The minimal SyncInto of d1 3.2 item 8, which stands in for Form1.vb
// FillShelf (:3083-3128: cards already in place are left alone, the others
// moved into place): the fewest removes, inserts and moves, and one Reset
// above the threshold.
[TestClass]
public sealed class SyncedCollectionTests : StorageCheckedTests
{
    private sealed class Item(string name)
    {
        public override string ToString() => name;
    }

    [TestMethod]
    public void OneItemMovedFromTheFrontIsOneMove()
    {
        Item a = new("a"), b = new("b"), c = new("c"), d = new("d");
        SyncedCollection<Item> list = [a, b, c, d];
        List<NotifyCollectionChangedAction> events = Record(list);

        int changes = list.SyncInto([b, c, d, a]);

        Assert.AreEqual(1, changes);
        CollectionAssert.AreEqual(new[] { NotifyCollectionChangedAction.Move }, events);
        CollectionAssert.AreEqual(new[] { b, c, d, a }, list.ToArray());
    }

    [TestMethod]
    public void RemovesInsertsAndMovesReachTheWantedOrder()
    {
        Item[] items = Enumerable.Range(0, 12).Select(index => new Item(index.ToString(System.Globalization.CultureInfo.InvariantCulture))).ToArray();
        var random = new Random(338);
        for (int round = 0; round < 200; round++)
        {
            SyncedCollection<Item> list = [.. items.Where(_ => random.Next(3) > 0).OrderBy(_ => random.Next())];
            Item[] wanted = items.Where(_ => random.Next(3) > 0).OrderBy(_ => random.Next()).ToArray();
            List<Item> before = [.. list];
            List<NotifyCollectionChangedAction> events = Record(list);

            int changes = list.SyncInto(wanted);

            CollectionAssert.AreEqual(wanted, list.ToArray(), $"Round {round}.");
            Assert.AreEqual(changes, events.Count, $"Round {round}: one notification per change.");
            Assert.IsFalse(events.Contains(NotifyCollectionChangedAction.Reset));
            int removes = before.Count(item => !wanted.Contains(item));
            int inserts = wanted.Count(item => !before.Contains(item));
            Assert.IsGreaterThanOrEqualTo(removes + inserts, changes, $"Round {round}.");
        }
    }

    [TestMethod]
    public void TheSameOrderChangesNothingAndManyChangesAreOneReset()
    {
        Item[] items = Enumerable.Range(0, 100).Select(index => new Item(index.ToString(System.Globalization.CultureInfo.InvariantCulture))).ToArray();
        SyncedCollection<Item> list = [.. items];
        List<NotifyCollectionChangedAction> events = Record(list);

        Assert.AreEqual(0, list.SyncInto(items));
        Assert.IsEmpty(events);

        Item[] reversed = items.Reverse().ToArray();
        Assert.AreEqual(-1, list.SyncInto(reversed), "99 moves is over the threshold of 64.");
        CollectionAssert.AreEqual(new[] { NotifyCollectionChangedAction.Reset }, events);
        CollectionAssert.AreEqual(reversed, list.ToArray());
    }

    private static List<NotifyCollectionChangedAction> Record(SyncedCollection<Item> list)
    {
        var events = new List<NotifyCollectionChangedAction>();
        list.CollectionChanged += (_, e) => events.Add(e.Action);
        return events;
    }
}
