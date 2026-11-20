using CommunityToolkit.Mvvm.ComponentModel;

namespace PaperRoute.ViewModels.Spikes;

// The working-copy spike's editor (d1 7.2 item 4): one field, Title, bound two
// ways to the working clone of a RecordState, and HasUnsavedChanges, which the
// record state recomputes after every change of the field. It has no save, and
// nothing in it writes a file.
public sealed partial class WorkingCopyEditorViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _hasUnsavedChanges;

    public WorkingCopyEditorViewModel(RecordState record)
    {
        ArgumentNullException.ThrowIfNull(record);
        Record = record;
        _hasUnsavedChanges = record.HasUnsavedChanges();
    }

    public RecordState Record { get; }

    // The working clone's title, written as it is typed (the binding updates its
    // source on every change of the box's text). Unlike today's Save, which
    // takes txtTitle.Text.Trim(), the value is written as typed.
    public string Title
    {
        get => Record.Working.Title ?? string.Empty;
        set
        {
            value ??= string.Empty;
            if (string.Equals(value, Record.Working.Title ?? string.Empty, StringComparison.Ordinal))
            {
                return;
            }

            Record.Working.Title = value;
            OnPropertyChanged();
            HasUnsavedChanges = Record.HasUnsavedChanges();
        }
    }
}
