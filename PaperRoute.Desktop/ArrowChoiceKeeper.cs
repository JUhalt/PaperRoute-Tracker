using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace PaperRoute.Desktop;

// Keeps the item the arrow keys reached when a box's list closes without a
// choice: by Esc, Alt+Up, F4 or a click away. Today's Windows drop-down list
// does this (r1 2.3 and 4.3, the C3 check "Esc keeps the item the arrows
// reached and shows it"); Avalonia 12.1.6's ComboBox does not. Its arrow keys
// in the open list only move the keyboard from item to item, its selection
// changes on Enter or a click, and Esc closes the list on the opening choice
// (BoardViewTests.TheStageBoxKeepsItsSelectionWhileItsListIsOpen).
//
// So the view, not the view model, hands the box the item reached as the list
// closes. The Board then sees what it saw from Windows: a choice that comes
// with the close, which the #338 rule renders once (d1 3.3; risk row of d1 9).
// A choice made in the list (Enter, a click on an item) is left alone.
internal sealed class ArrowChoiceKeeper
{
    private readonly ComboBox _box;
    private int _reached = -1;
    private bool _chosen;

    private ArrowChoiceKeeper(ComboBox box)
    {
        _box = box;
        box.DropDownOpened += (_, _) =>
        {
            _reached = -1;
            _chosen = false;
        };
        box.SelectionChanged += (_, _) =>
        {
            if (box.IsDropDownOpen)
            {
                _chosen = true;
            }
        };

        // The item that has the keyboard, as the arrows move it, and again as
        // a key comes to it (Esc among them): focus and key events from the
        // list's items route through the box, though the list is in a popup.
        box.AddHandler(InputElement.GotFocusEvent, (_, e) => NoteItem(e.Source), RoutingStrategies.Bubble, handledEventsToo: true);
        box.AddHandler(InputElement.KeyDownEvent, (_, e) => NoteItem(e.Source), RoutingStrategies.Tunnel, handledEventsToo: true);
        box.DropDownClosed += (_, _) => KeepReached();
    }

    public static void Attach(ComboBox box)
    {
        ArgumentNullException.ThrowIfNull(box);
        _ = new ArrowChoiceKeeper(box);
    }

    private void NoteItem(object? eventSource)
    {
        if (!_box.IsDropDownOpen || eventSource is not Control source)
        {
            return;
        }

        ComboBoxItem? item = source as ComboBoxItem ?? source.FindAncestorOfType<ComboBoxItem>();
        int index = item is null ? -1 : _box.IndexFromContainer(item);
        if (index >= 0)
        {
            _reached = index;
        }
    }

    private void KeepReached()
    {
        int reached = _reached;
        bool chosen = _chosen;
        _reached = -1;
        _chosen = false;
        if (!chosen && reached >= 0 && reached != _box.SelectedIndex)
        {
            _box.SelectedIndex = reached;
        }
    }
}
