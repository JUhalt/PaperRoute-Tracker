namespace PaperRoute.ViewModels.ManuscriptPage;

// The delta (build 9's design, 2.2): people, affiliations, journals and tag
// colours made on the manuscript page and not saved yet. The session's save
// writes them onto a fresh read of the author file (S1); Discard and a save
// that succeeded drop them (WorkingCopy). Today's
// counterpart is the author library's dirty flag and the ids it held on disk
// (EditManuscriptForm.vb:30-37).
public sealed class LibraryChanges
{
    public List<AuthorRecord> Authors { get; } = [];

    public List<AffiliationRecord> Affiliations { get; } = [];

    public List<JournalRecord> Journals { get; } = [];

    public List<TagColor> TagColors { get; } = [];

    public bool IsEmpty =>
        Authors.Count == 0 && Affiliations.Count == 0 && Journals.Count == 0 && TagColors.Count == 0;

    // Puts the delta into a library: each record whose id the library lacks
    // (the library's own record wins, today's draft rule,
    // EditManuscriptForm.vb:3030-3081), and each tag colour through
    // WorkTypeService.SetColor (:3083-3095).
    public void ApplyTo(AuthorLibraryData library)
    {
        ArgumentNullException.ThrowIfNull(library);
        library.Authors ??= [];
        library.Affiliations ??= [];
        library.Journals ??= [];
        AddMissing(library.Authors, Authors, author => author.Id);
        AddMissing(library.Affiliations, Affiliations, affiliation => affiliation.Id);
        AddMissing(library.Journals, Journals, journal => journal.Id);
        foreach (TagColor entry in TagColors.OfType<TagColor>())
        {
            WorkTypeService.SetColor(library, entry.Tag, entry.Color);
        }
    }

    // A save wrote the author file and then failed to write the manuscripts:
    // every record here is on disk now, so the next save has nothing to add
    // (build 9's design, 2.5 step 9; today RememberAuthorLibraryOnDisk,
    // EditManuscriptForm.vb:4653-4665). The record's own edits stay unsaved.
    public void MarkWritten()
    {
        Authors.Clear();
        Affiliations.Clear();
        Journals.Clear();
        TagColors.Clear();
    }

    private static void AddMissing<T>(List<T> into, IEnumerable<T> records, Func<T, Guid> id)
        where T : class
    {
        HashSet<Guid> present = into.OfType<T>().Select(id).ToHashSet();
        into.AddRange(records.OfType<T>().Where(record => id(record) != Guid.Empty && present.Add(id(record))));
    }
}
