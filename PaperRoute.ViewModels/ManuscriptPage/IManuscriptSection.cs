namespace PaperRoute.ViewModels.ManuscriptPage;

// What Save does with a section's pending input (build 9's design, 2.2).
public enum PendingUse
{
    // Only input that is complete joins the record; what is incomplete stays
    // pending and is a problem (Problems).
    Save,

    // Everything as typed, for a recovery draft (R1).
    Draft,
}

// A check that stops Save, shown beside its field (D4). Field names the
// field for the view, which shows the message there and focuses the first;
// Message is the words shown and read.
public sealed record FieldProblem
{
    public FieldProblem(string field, string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(field);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        Field = field;
        Message = message;
    }

    public string Field { get; }

    public string Message { get; }
}

// One tab of the manuscript page (build 9's design, 2.2): Overview and
// Authors in unit 3, and later Versions, Submissions, Readiness and Route.
// The frame owns the working copy, the bar and Save; a section edits the
// copy only through its edit door, and its rows and editors hold ids, never
// objects inside Working, because Working is a new object graph after each
// load, discard and save (the design's three rules, 2.2).
public interface IManuscriptSection
{
    // "Overview", "Authors".
    string Key { get; }

    // The tab text.
    string Title { get; }

    // A new record, and again after Discard and after a save: rows are
    // rebuilt from the copy given, and every editor is closed.
    void Attach(WorkingCopy copy);

    // After the copy's Changed: rows reconciled by id.
    void Refresh();

    // Text not yet in the working copy, such as a half-typed tag or a new
    // link row with only a URL. An expanded editor on an existing row is not
    // pending, because it writes the working copy as edited (D11).
    bool HasPendingInput();

    // Pending input began or ended.
    event EventHandler? PendingChanged;

    // Puts pending input into target, inside the edit door. For Save, only
    // input that is complete joins, and what joined stops being pending; an
    // expanded editor whose row is complete closes (Save's step 3). For a
    // draft, everything as typed, and nothing on the page changes.
    void ApplyPending(Manuscript target, PendingUse use);

    // Drops pending input: after Discard, or a No on leaving.
    void ClearPending();

    // Inline checks that stop Save, incomplete pending input included.
    IReadOnlyList<FieldProblem> Problems();
}
