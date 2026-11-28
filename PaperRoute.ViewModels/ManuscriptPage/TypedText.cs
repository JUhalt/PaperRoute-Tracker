using CommunityToolkit.Mvvm.ComponentModel;

namespace PaperRoute.ViewModels.ManuscriptPage;

// The three fields that write the working copy as they are typed (build 9's
// design, 2.4).
public enum TypedField
{
    Title,
    TargetJournal,
    FileDrawerReason,
}

// A box's text over the working copy: the #358 outcome with no shown-value
// bookkeeping (build 9's design, 2.4). Today the title, target journal and
// File Drawer reason wait in their boxes until Save, and a step rewrites a
// box only if it changed that field (EditManuscriptForm.vb:54-62). Here the
// box and the working copy are one state, as #368 asks:
// - What is typed writes the working copy at once, trimmed, through the edit
//   door, and only when its trimmed text differs from the copy's, so spaces
//   at the ends are never a change (EditManuscriptForm.vb:593-594, 606-607).
//   The box keeps the text as typed, so a space being typed is not eaten.
// - Text typed back to the value the last load, discard or save left writes
//   that value as it was (WorkingCopy.Loaded), so a title stored with spaces
//   at its ends is not unsaved after a character is typed and removed. That
//   value, not the original record's: a load's baseline is taken after the
//   reconciliation, which can rewrite the target journal, and a save's from
//   the record as written.
// - After every change to the working copy, the box rereads it when the
//   copy's trimmed value differs from the box's trimmed text: a step that
//   changed the field wins, and a step that left it keeps what was typed.
//   After a load, a discard or a save the box shows the record as it is.
public sealed class TypedText : ObservableObject
{
    private readonly WorkingCopy _copy;
    private int _token;
    private string _text;

    public TypedText(WorkingCopy copy, TypedField field)
    {
        ArgumentNullException.ThrowIfNull(copy);
        _copy = copy;
        Field = field;
        _token = copy.Token;
        _text = Read(copy.Working);
        copy.Changed += OnCopyChanged;
    }

    public TypedField Field { get; }

    // The box's text, as typed.
    public string Text
    {
        get => _text;
        set
        {
            if (!SetProperty(ref _text, value ?? string.Empty))
            {
                return;
            }

            string typed = _text.Trim();
            if (string.Equals(typed, Read(_copy.Working).Trim(), StringComparison.Ordinal))
            {
                return;
            }

            string loaded = _copy.Loaded[Field];
            string written = string.Equals(typed, loaded.Trim(), StringComparison.Ordinal) ? loaded : typed;
            _copy.Edit(working => Write(working, written));
        }
    }

    private void OnCopyChanged(object? sender, EventArgs e)
    {
        string stored = Read(_copy.Working);
        if (_token != _copy.Token)
        {
            _token = _copy.Token;
            SetProperty(ref _text, stored, nameof(Text));
        }
        else if (!string.Equals(stored.Trim(), _text.Trim(), StringComparison.Ordinal))
        {
            SetProperty(ref _text, stored, nameof(Text));
        }
    }

    private string Read(Manuscript record) => Field switch
    {
        TypedField.Title => record.Title,
        TypedField.TargetJournal => record.TargetJournal,
        TypedField.FileDrawerReason => record.FileDrawerReason,
        _ => throw new InvalidOperationException($"No typed field {Field}."),
    } ?? string.Empty;

    private void Write(Manuscript record, string value)
    {
        switch (Field)
        {
            case TypedField.Title:
                record.Title = value;
                break;
            case TypedField.TargetJournal:
                record.TargetJournal = value;
                break;
            case TypedField.FileDrawerReason:
                record.FileDrawerReason = value;
                break;
            default:
                throw new InvalidOperationException($"No typed field {Field}.");
        }
    }
}
