using ManuscriptPipeline.Models;
using PaperRoute.ViewModels.ManuscriptPage;

namespace PaperRoute.Desktop.Tests.ManuscriptPage;

// A section for the frame's tests, standing in for the Overview and Authors
// until they are built (O1 to O4, A1a). It holds the kinds of pending input
// unit 3 has: a typed tag (complete when not blank, as the tag box's text),
// and a new row that is not complete yet (a link with only a URL). It can
// hold an editor expanded on an existing, complete row. Its rows hold ids,
// never objects inside Working, and change the copy through the edit door.
// It counts what the frame asks of it.
internal sealed class StandInSection(string key = "Overview") : IManuscriptSection
{
    public const string IncompleteRowField = "NewLink";

    public const string IncompleteRowProblem = "Enter a label and a URL for the new link, or remove it.";

    private string _typedTag = string.Empty;
    private bool _incompleteRow;

    public event EventHandler? PendingChanged;

    public string Key { get; } = key;

    public string Title => Key;

    public WorkingCopy? Copy { get; private set; }

    public int Attaches { get; private set; }

    public int Refreshes { get; private set; }

    public int Clears { get; private set; }

    public List<PendingUse> Applied { get; } = [];

    // An editor is open on an existing row whose fields are complete.
    public bool EditorOpen { get; set; }

    // The tag box's text: pending input until it joins the copy.
    public string TypedTag
    {
        get => _typedTag;
        set
        {
            bool before = HasPendingInput();
            _typedTag = value;
            Notify(before);
        }
    }

    // A new row with only some of its fields: pending, and a problem at Save.
    public bool IncompleteRow
    {
        get => _incompleteRow;
        set
        {
            bool before = HasPendingInput();
            _incompleteRow = value;
            Notify(before);
        }
    }

    public void Attach(WorkingCopy copy)
    {
        Copy = copy;
        Attaches++;
        EditorOpen = false;
    }

    public void Refresh() => Refreshes++;

    public bool HasPendingInput() => _typedTag.Length > 0 || _incompleteRow;

    public void ApplyPending(Manuscript target, PendingUse use)
    {
        Applied.Add(use);
        if (use == PendingUse.Save && _typedTag.Trim().Length > 0)
        {
            target.Tags.Add(_typedTag.Trim());
            TypedTag = string.Empty;
        }

        if (use == PendingUse.Save)
        {
            EditorOpen = false;
        }
    }

    public void ClearPending()
    {
        Clears++;
        bool before = HasPendingInput();
        _typedTag = string.Empty;
        _incompleteRow = false;
        EditorOpen = false;
        Notify(before);
    }

    public IReadOnlyList<FieldProblem> Problems() =>
        _incompleteRow ? [new FieldProblem(IncompleteRowField, IncompleteRowProblem)] : [];

    // An edit from a row of one kind, which holds the id the copy knows the
    // item by and finds the item afresh in Working each time: a link's
    // label, a shortlisted journal's notes, an author's corresponding flag.
    public bool Change(RowKind kind, Guid id, string text) =>
        Copy?.Edit(working =>
        {
            switch (kind)
            {
                case RowKind.Link:
                    working.RelatedLinks.Single(link => link.Id == id).Label = text;
                    break;
                case RowKind.Shortlist:
                    working.JournalShortlist.Single(candidate => candidate.Id == id).Notes = text;
                    break;
                case RowKind.Author:
                    ManuscriptAuthor author = working.Authors.Single(item => item.AuthorId == id);
                    author.IsCorrespondingAuthor = !author.IsCorrespondingAuthor;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }) ?? false;

    private void Notify(bool before)
    {
        if (before != HasPendingInput())
        {
            PendingChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}

// The kinds of row unit 3 has, each with its key (build 9's design, 2.2,
// rule 1).
public enum RowKind
{
    Link,
    Shortlist,
    Author,
}
