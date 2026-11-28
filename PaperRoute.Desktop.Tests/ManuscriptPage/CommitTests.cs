using System.Reflection;
using System.Text.Json;
using ManuscriptPipeline.Models;
using PaperRoute.ViewModels.ManuscriptPage;

namespace PaperRoute.Desktop.Tests.ManuscriptPage;

// What a save that succeeded copies into the session's own record (build 9's
// design, 2.2; test C-10). Every record is built in memory.
[TestClass]
public sealed class CommitTests : Board.StorageCheckedTests
{
    private static readonly DateTime Day = new(2026, 3, 2);

    // C-10: every settable Manuscript property is copied or named with its
    // reason, never both; a property added to the model fails here until it
    // is put on one list. Each copied property arrives as it was written,
    // in objects of the record's own; each one left out stays as it was.
    [TestMethod]
    public void TheCommitCopiesEveryPropertyButTheNamedOnes()
    {
        PropertyInfo[] settable = typeof(Manuscript).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.SetMethod is { IsPublic: true })
            .ToArray();
        IReadOnlyList<string> copied = ManuscriptCommit.Copied;
        IReadOnlyDictionary<string, string> leftOut = ManuscriptCommit.LeftOut;

        string[] neither = [.. settable.Select(property => property.Name).Where(name => !copied.Contains(name) && !leftOut.ContainsKey(name)).Order(StringComparer.Ordinal)];
        Assert.IsEmpty(neither,
            "ManuscriptCommit neither copies nor names these Manuscript properties, so a save would drop them from the session's record. " +
            "Copy each, or name it in LeftOut with why it is not copied: " + string.Join(", ", neither));
        string[] both = [.. copied.Where(leftOut.ContainsKey)];
        Assert.IsEmpty(both, "Copied and also named as left out: " + string.Join(", ", both));
        string[] twice = [.. copied.GroupBy(name => name, StringComparer.Ordinal).Where(group => group.Count() > 1).Select(group => group.Key)];
        Assert.IsEmpty(twice, "Copied twice: " + string.Join(", ", twice));
        var names = settable.Select(property => property.Name).ToHashSet(StringComparer.Ordinal);
        string[] stale = [.. copied.Concat(leftOut.Keys).Where(name => !names.Contains(name))];
        Assert.IsEmpty(stale, "ManuscriptCommit names properties Manuscript does not have: " + string.Join(", ", stale));
        foreach ((string name, string reason) in leftOut)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(reason), $"{name} is left out with no reason.");
        }

        Manuscript from = EveryPropertySet();
        var to = new Manuscript();
        var fresh = new Manuscript();
        var before = settable.ToDictionary(property => property.Name, property => property.GetValue(to));

        ManuscriptCommit.CopyInto(from, to);

        foreach (PropertyInfo property in settable)
        {
            object? written = property.GetValue(from);
            object? committed = property.GetValue(to);
            if (leftOut.ContainsKey(property.Name))
            {
                Assert.AreSame(before[property.Name], committed, $"{property.Name} is named as left out but was changed.");
                continue;
            }

            Assert.AreNotEqual(Json(property.GetValue(fresh)), Json(written), $"This test does not set {property.Name}, so it cannot show that it is copied.");
            Assert.AreEqual(Json(written), Json(committed), $"{property.Name} was not copied as it was written.");
            if (written is not null && written is not string && !property.PropertyType.IsValueType)
            {
                Assert.AreNotSame(written, committed, $"{property.Name} is shared with the written record, so a later change to one reaches the other.");
            }
        }
    }

    private static string Json(object? value) => JsonSerializer.Serialize(value);

    // A record with every property away from a new Manuscript's value.
    private static Manuscript EveryPropertySet()
    {
        var submission = new JournalSubmission { JournalName = "Journal of Tests", SubmittedDate = Day.AddDays(-60), ManuscriptNumber = "JT-1" };
        submission.Decisions.Add(new EditorialDecisionEvent { Decision = EditorialDecision.MajorRevision, DecisionDate = Day.AddDays(-30) });
        var version = new ManuscriptVersion { Label = "Revision 1", CreatedDate = Day, LocalFilePath = "revision-1.docx", SubmissionId = submission.Id };
        return new Manuscript
        {
            Id = Guid.Parse("4d1f4b0e-0000-4000-8000-000000000010"),
            Title = "A Study of Tests",
            CoAuthors = "Ada Lovelace",
            Authors = [new ManuscriptAuthor { AuthorId = Guid.Parse("4d1f4b0e-0000-4000-8000-000000000001"), IsCorrespondingAuthor = true }],
            TargetJournal = "Journal of Tests",
            TargetJournalId = Guid.Parse("4d1f4b0e-0000-4000-8000-000000000007"),
            ManuscriptUrl = "https://example.org/manuscript",
            Metadata = new ManuscriptMetadata { AbstractText = "Tests, studied.", PreprintDoi = "10.1000/preprint" },
            RelatedLinks = [new ManuscriptExternalLink { Label = "Data", Url = "https://example.org/data" }],
            Reminders = [new ManuscriptReminder { Title = "Send the revision", DueDate = Day.AddDays(20) }],
            Versions = [version],
            CurrentVersionId = version.Id,
            ReadinessProfiles = [new ManuscriptReadiness { JournalName = "Journal of Tests", Notes = "Word limit checked" }],
            SubmissionPackets = [new SubmissionPacket { JournalName = "Journal of Tests", Label = "First submission", ManuscriptVersionId = version.Id }],
            CurrentStage = PaperStage.Revision,
            Location = ManuscriptLocation.FileDrawer,
            StageEnteredDate = Day,
            RevisionDeadline = Day.AddDays(30),
            FileDrawerDate = Day.AddDays(5),
            FileDrawerReason = "Waiting for the second data set",
            History = [new HistoryEvent { EventDate = Day, Stage = PaperStage.Revision, Note = "Stage changed from UnderReview to Revision." }],
            Submissions = [submission],
            WorkType = WorkType.JournalArticle,
            Tags = ["methods"],
            JournalShortlist = [new JournalCandidate { JournalName = "Journal of Second Chances" }],
            PublicationMatches = [new PublicationMatch { Doi = "10.1000/one", Title = "A Study of Tests" }],
        };
    }
}
