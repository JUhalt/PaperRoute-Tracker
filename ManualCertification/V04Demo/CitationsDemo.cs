using ManuscriptPipeline.Forms;
using ManuscriptPipeline.Models;
using ManuscriptPipeline.Services;

namespace PaperRoute.V04Demo;

// Your Citations (#91) on the sample library: fictional figures saved as if
// from OpenAlex, for ORCID's fictional test researcher's iD. Nothing is
// looked up; the update window shows a recorded answer.
internal static class CitationsDemo
{
    private const string Orcid = "0000-0002-1825-0097";

    private static readonly (string Title, string Doi)[] PublishedDois =
    {
        ("DEMO Published 2", "10.5555/demo.retrieval"),
        ("DEMO Published 3", "10.5555/demo.sleep"),
        ("DEMO Published 4", "10.5555/demo.equivalence"),
        ("DEMO Published 5", "10.5555/demo.open-materials")
    };

    internal static void Prepare()
    {
        var repository = new ManuscriptRepository();
        var manuscripts = repository.Load();
        foreach (var (title, doi) in PublishedDois)
        {
            var manuscript = manuscripts.FirstOrDefault(item => item.Title.StartsWith(title, StringComparison.Ordinal));
            if (manuscript is not null)
            {
                manuscript.Metadata.Doi = doi;
                manuscript.Metadata.PublishedDate ??= manuscript.StageEnteredDate;
            }
        }
        repository.Save(manuscripts);

        var authors = new AuthorLibraryRepository();
        var library = authors.Load();
        library.Authors.Add(new AuthorRecord { GivenName = "Avery", FamilyName = "Demo", IsMe = true, Orcid = Orcid, Notes = "Fictional author." });
        authors.Save(library);

        new CitationStore().Save(Snapshot(DateTime.Today));
    }

    internal static CitationSnapshot Snapshot(DateTime today)
    {
        var year = today.Year;
        var snapshot = new CitationSnapshot { Orcid = Orcid, Source = JournalFactCatalog.OpenAlexSource, RetrievedUtc = today.ToUniversalTime() };
        snapshot.Works.AddRange(new[]
        {
            Work("W9000000002", "10.5555/demo.retrieval", "DEMO Retrieval practice in introductory statistics", year - 1, 1.62, 0.88, true, 4, 9),
            Work("W9000000003", "10.5555/demo.sleep", "DEMO Sleep and memory consolidation in older adults", year - 2, 1.05, 0.71, false, 3, 8, 6),
            Work("W9000000004", "10.5555/demo.equivalence", "DEMO A tutorial on equivalence testing", year - 3, 3.41, 0.97, true, 11, 24, 19, 7),
            Work("W9000000005", "10.5555/demo.open-materials", "DEMO Open materials in developmental science", year - 3, 0.74, 0.55, false, 1, 2, 3, 1),
            Work("W9000000011", "10.5555/demo.anchoring", "DEMO Anchoring in clinical risk estimates", year - 6, 1.21, 0.79, false, 2, 4, 5, 6, 5, 3, 1),
            Work("W9000000012", "10.5555/demo.invariance", "DEMO Measurement invariance of a short grit scale", year - 5, 0.93, 0.62, false, 1, 3, 4, 3, 2, 1),
            Work("W9000000013", "10.5555/demo.priming", "DEMO A null result on priming and choice", year - 7, 0.41, 0.33, false, 0, 1, 1, 0, 2, 1, 1, 0),
            Work("W9000000014", "10.5555/demo.review", "DEMO Feedback timing in learning: a review", year - 9, 1.37, 0.84, false, 3, 5, 6, 8, 9, 7, 6, 4, 2, 1)
        });
        return snapshot;
    }

    internal static Form Dialog()
    {
        var groups = new List<OrcidWorkGroup>
        {
            Group("10.5555/demo.retrieval", "DEMO Retrieval practice in introductory statistics"),
            Group("10.5555/demo.sleep", "DEMO Sleep and memory consolidation in older adults"),
            Group("10.5555/demo.equivalence", "DEMO A tutorial on equivalence testing"),
            Group("10.5555/demo.anchoring", "DEMO Anchoring in clinical risk estimates"),
            Group("10.5555/demo.review", "DEMO Feedback timing in learning: a review"),
            new OrcidWorkGroup { Title = "DEMO A book chapter without a DOI" }
        };
        var found = Snapshot(DateTime.Today).Works.ToDictionary(item => item.Doi);
        var linked = new List<CitedWork>
        {
            Work("W9000000021", "10.5555/demo.someone-else", "DEMO Attention and load in visual search", DateTime.Today.Year - 4, 2.1, 0.9, true, 8, 12, 10, 6)
        };
        var lookup = CitationsService.Assemble(Orcid, groups,
            new List<(string, string)> { ("10.5555/demo.open-materials", "DEMO Published 5: Open materials in developmental science") },
            found, linked, 9, Array.Empty<string>());
        var dialog = new CitationsUpdateForm(Orcid, new List<(string, string)> { ("10.5555/demo.open-materials", "DEMO Published 5") },
            null, new RecordedSource(lookup));
        dialog.Shown += async (_, _) => await dialog.LookUpAsync();
        return dialog;
    }

    private static OrcidWorkGroup Group(string doi, string title) =>
        new() { Dois = new List<string> { doi }, Title = title, WorkType = "journal-article" };

    private static CitedWork Work(string id, string doi, string title, int published, double fwci, double percentile, bool top10, params int[] perYear)
    {
        var work = new CitedWork
        {
            OpenAlexId = id, Doi = doi, Title = title, Year = published, Journal = "Fictional Journal",
            Fwci = fwci, Percentile = percentile, InTop10Percent = top10
        };
        var year = DateTime.Today.Year;
        for (var index = 0; index < perYear.Length; index++)
            if (perYear[index] > 0 && year - index >= published)
                work.CountsByYear.Add(new YearCount { Year = year - index, Count = perYear[index] });
        work.CitedByCount = work.CountsByYear.Sum(item => item.Count);
        return work;
    }

    private sealed class RecordedSource : ICitationSource
    {
        private readonly CitationLookup _lookup;

        public RecordedSource(CitationLookup lookup) => _lookup = lookup;

        public Task<CitationLookup> LookupAsync(string orcid, IEnumerable<(string Doi, string Title)> manuscriptDois, bool includeOpenAlexLinked,
            IEnumerable<string> excluded, IProgress<string> progress, CancellationToken cancellationToken) => Task.FromResult(_lookup);
    }

    internal static T? Find<T>(Control parent, Func<T, bool> match) where T : Control
    {
        foreach (Control child in parent.Controls)
        {
            if (child is T typed && match(typed)) return typed;
            var nested = Find(child, match);
            if (nested is not null) return nested;
        }
        return null;
    }
}
