using ManuscriptPipeline.Models;
using PaperRoute.ViewModels.ManuscriptPage;

namespace PaperRoute.Desktop.Tests.ManuscriptPage;

// The delta's own rules (build 9's design, tests L-1 on). L-1 comes forward
// from W2 into W1, because W1's working copy already puts the delta into the
// library it shows (LibraryChanges.ApplyTo). Every record is built in memory.
[TestClass]
public sealed class LibraryChangesTests : Board.StorageCheckedTests
{
    private static readonly Guid Ada = Guid.Parse("4d1f4b0e-0000-4000-8000-000000000001");
    private static readonly Guid Grace = Guid.Parse("4d1f4b0e-0000-4000-8000-000000000002");
    private static readonly Guid University = Guid.Parse("4d1f4b0e-0000-4000-8000-000000000003");
    private static readonly Guid Institute = Guid.Parse("4d1f4b0e-0000-4000-8000-000000000004");
    private static readonly Guid Tests = Guid.Parse("4d1f4b0e-0000-4000-8000-000000000005");
    private static readonly Guid Steps = Guid.Parse("4d1f4b0e-0000-4000-8000-000000000006");

    // L-1: the library's own record wins, each id joins once, and a record
    // with no id does not join.
    [TestMethod]
    public void ApplyToAddsOnlyRecordsTheLibraryLacks()
    {
        var library = new AuthorLibraryData
        {
            Authors = [new AuthorRecord { Id = Ada, GivenName = "Ada", FamilyName = "Lovelace" }],
            Affiliations = [new AffiliationRecord { Id = University, Institution = "Example University" }],
            Journals = [new JournalRecord { Id = Tests, Name = "Journal of Tests" }],
        };
        AuthorRecord adaOnDisk = library.Authors[0];
        AffiliationRecord universityOnDisk = library.Affiliations[0];
        JournalRecord testsOnDisk = library.Journals[0];

        var delta = new LibraryChanges();
        delta.Authors.Add(new AuthorRecord { Id = Ada, GivenName = "Ada", FamilyName = "Byron" });
        delta.Authors.Add(new AuthorRecord { Id = Grace, GivenName = "Grace", FamilyName = "Hopper" });
        delta.Authors.Add(new AuthorRecord { Id = Grace, GivenName = "Grace", FamilyName = "Murray" });
        delta.Authors.Add(new AuthorRecord { Id = Guid.Empty, GivenName = "No", FamilyName = "Id" });
        delta.Affiliations.Add(new AffiliationRecord { Id = University, Institution = "Another University" });
        delta.Affiliations.Add(new AffiliationRecord { Id = Institute, Institution = "Example Institute" });
        delta.Journals.Add(new JournalRecord { Id = Tests, Name = "Another Journal" });
        delta.Journals.Add(new JournalRecord { Id = Steps, Name = "Journal of Steps" });

        delta.ApplyTo(library);

        CollectionAssert.AreEqual(new[] { Ada, Grace }, library.Authors.Select(author => author.Id).ToArray(), "Each person joins once, only where the library lacks the id, and never with no id.");
        Assert.AreSame(adaOnDisk, library.Authors[0], "The delta's record replaced the library's own.");
        Assert.AreEqual("Hopper", library.Authors[1].FamilyName, "The first of two records with one id joins.");
        CollectionAssert.AreEqual(new[] { University, Institute }, library.Affiliations.Select(affiliation => affiliation.Id).ToArray());
        Assert.AreSame(universityOnDisk, library.Affiliations[0], "The delta's affiliation replaced the library's own.");
        CollectionAssert.AreEqual(new[] { Tests, Steps }, library.Journals.Select(journal => journal.Id).ToArray());
        Assert.AreSame(testsOnDisk, library.Journals[0], "The delta's journal replaced the library's own.");

        delta.ApplyTo(library);
        Assert.HasCount(2, library.Authors, "Putting the delta in again added a person twice.");
        Assert.HasCount(2, library.Affiliations, "Putting the delta in again added an affiliation twice.");
        Assert.HasCount(2, library.Journals, "Putting the delta in again added a journal twice.");
    }
}
