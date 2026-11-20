using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace PaperRoute.ViewModels.Board;

// An ObservableCollection a render brings to its new order (d1 3.2 item 8):
// the fewest removes, inserts and moves, so items that stay keep their
// views; above a threshold, one Reset instead.
public sealed class SyncedCollection<T> : ObservableCollection<T>
    where T : class
{
    // The number of changes above which a sync replaces the whole list with
    // one Reset notification. d1 3.2 sets 64 for a start; the 500-library
    // measurement (step 12) tunes it.
    public const int DefaultResetThreshold = 64;

    // Brings the collection to the order of wanted. Returns the number of
    // changes made (one notification each), or -1 when the list was replaced
    // with one Reset. Items are unique and compared by reference.
    public int SyncInto(IReadOnlyList<T> wanted, int resetThreshold = DefaultResetThreshold)
    {
        ArgumentNullException.ThrowIfNull(wanted);

        if (SameOrder(wanted))
        {
            return 0;
        }

        Plan plan = PlanFor(wanted);
        if (plan.Changes > resetThreshold)
        {
            ReplaceAll(wanted);
            return -1;
        }

        int performed = 0;

        // Removes first: everything that is not wanted.
        for (int index = Count - 1; index >= 0; index--)
        {
            if (!plan.Wanted.Contains(this[index]))
            {
                RemoveAt(index);
                performed++;
            }
        }

        // Then, in the wanted order, each item that is not kept in place is
        // moved, or inserted, just after the item before it. The kept ones
        // (the longest run already in order) are never touched, and each of
        // the others is placed once. By induction, the items placed so far
        // stand in wanted order, so at the end the list is the wanted list.
        for (int index = 0; index < wanted.Count; index++)
        {
            T item = wanted[index];
            if (plan.Kept.Contains(item))
            {
                continue;
            }

            int position = index == 0 ? 0 : IndexOfReference(wanted[index - 1]) + 1;
            int from = IndexOfReference(item);
            if (from < 0)
            {
                InsertItem(position, item);
                performed++;
            }
            else
            {
                int to = from < position ? position - 1 : position;
                if (from != to)
                {
                    MoveItem(from, to);
                    performed++;
                }
            }
        }

        return performed;
    }

    // The whole list replaced, with one Reset notification.
    public void ReplaceAll(IEnumerable<T> items)
    {
        CheckReentrancy();
        Items.Clear();
        foreach (T item in items)
        {
            Items.Add(item);
        }

        OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
        OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }

    private bool SameOrder(IReadOnlyList<T> wanted)
    {
        if (wanted.Count != Count)
        {
            return false;
        }

        for (int index = 0; index < Count; index++)
        {
            if (!ReferenceEquals(this[index], wanted[index]))
            {
                return false;
            }
        }

        return true;
    }

    private int IndexOfReference(T item)
    {
        for (int index = 0; index < Count; index++)
        {
            if (ReferenceEquals(this[index], item))
            {
                return index;
            }
        }

        return -1;
    }

    // Which items stay where they are: the longest run of present items whose
    // wanted positions already rise (patience sorting). Every other present
    // item is moved once, every absent one inserted, every unwanted one
    // removed.
    private Plan PlanFor(IReadOnlyList<T> wanted)
    {
        var wantedIndex = new Dictionary<T, int>(ReferenceEqualityComparer.Instance);
        for (int index = 0; index < wanted.Count; index++)
        {
            wantedIndex.TryAdd(wanted[index], index);
        }

        var present = new List<T>();
        int removes = 0;
        foreach (T item in this)
        {
            if (wantedIndex.ContainsKey(item))
            {
                present.Add(item);
            }
            else
            {
                removes++;
            }
        }

        // Longest increasing subsequence of the present items' wanted
        // positions, kept by the index of its last item at each length.
        var tails = new List<int>();
        var previous = new int[present.Count];
        for (int index = 0; index < present.Count; index++)
        {
            int value = wantedIndex[present[index]];
            int low = 0;
            int high = tails.Count;
            while (low < high)
            {
                int middle = (low + high) / 2;
                if (wantedIndex[present[tails[middle]]] < value)
                {
                    low = middle + 1;
                }
                else
                {
                    high = middle;
                }
            }

            previous[index] = low > 0 ? tails[low - 1] : -1;
            if (low == tails.Count)
            {
                tails.Add(index);
            }
            else
            {
                tails[low] = index;
            }
        }

        var kept = new HashSet<T>(ReferenceEqualityComparer.Instance);
        for (int index = tails.Count > 0 ? tails[^1] : -1; index >= 0; index = previous[index])
        {
            kept.Add(present[index]);
        }

        int moves = present.Count - kept.Count;
        int inserts = wanted.Count - present.Count;
        var wantedSet = new HashSet<T>(wantedIndex.Keys, ReferenceEqualityComparer.Instance);
        return new Plan(wantedSet, kept, removes + moves + inserts);
    }

    private sealed record Plan(HashSet<T> Wanted, HashSet<T> Kept, int Changes);
}
