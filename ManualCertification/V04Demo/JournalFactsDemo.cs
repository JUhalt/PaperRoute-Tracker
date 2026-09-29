using ManuscriptPipeline.Forms;
using ManuscriptPipeline.Models;
using ManuscriptPipeline.Services;

namespace PaperRoute.V04Demo;

// Journal facts (#87) on recorded DOAJ and OpenAlex answers (the test
// fixtures), so the Journals page and the lookup preview can be checked
// without any network request.
internal static class JournalFactsDemo
{
    internal static string FixtureFolder()
    {
        var folder = new DirectoryInfo(AppContext.BaseDirectory);
        while (folder != null && !File.Exists(Path.Combine(folder.FullName, "ManuscriptPipeline.slnx")))
            folder = folder.Parent;
        if (folder == null) throw new InvalidOperationException("The repository folder was not found.");
        return Path.Combine(folder.FullName, "PaperRoute.Tests", "Fixtures", "JournalFacts");
    }

    private static string Fixture(string name) => File.ReadAllText(Path.Combine(FixtureFolder(), name));

    internal static JournalFactsLookup PlosOne() => new()
    {
        CheckedUtc = DateTime.UtcNow,
        Issns = new List<string> { "1932-6203" },
        Doaj = DoajClient.ParseSearch(Fixture("doaj_plos_one.json"), "1932-6203"),
        DoajChecked = true,
        OpenAlex = OpenAlexSourceClient.ParseSource(Fixture("openalex_plos_one.json")),
        OpenAlexChecked = true
    };

    internal static JournalFactsLookup PsychologicalScience() => new()
    {
        CheckedUtc = DateTime.UtcNow,
        Issns = new List<string> { "0956-7976", "1467-9280" },
        Doaj = null,
        DoajChecked = true,
        OpenAlex = OpenAlexSourceClient.ParseSource(Fixture("openalex_psych_science.json")),
        OpenAlexChecked = true
    };

    // Adds three journals to the session's library: one looked up in both
    // indexes, one only in OpenAlex, and one not looked up yet. Returns the
    // first one's id.
    internal static Guid AddJournals()
    {
        var repository = new AuthorLibraryRepository();
        var library = repository.Load();

        var plos = new JournalRecord { Name = "PLOS ONE", Issns = { "1932-6203" }, IsShortlisted = true };
        JournalFactsService.Apply(plos, JournalFactsService.Plan(plos, PlosOne()));
        plos.Facts.Add(new JournalFact
        {
            Key = JournalFactCatalog.AcceptanceRate, Value = "DEMO 31%", Year = DateTime.Today.Year - 1,
            Source = "DEMO entry, not a real figure", CheckedUtc = DateTime.UtcNow, EnteredByYou = true
        });

        var science = new JournalRecord { Name = "Psychological Science", Issns = { "0956-7976" } };
        JournalFactsService.Apply(science, JournalFactsService.Plan(science, PsychologicalScience()));

        var memory = new JournalRecord { Name = "Memory & Cognition", Publisher = "Springer Science+Business Media" };

        library.Journals.AddRange(new[] { plos, science, memory });
        repository.Save(library);
        return plos.Id;
    }

    // The lookup dialog on a journal whose publisher the researcher typed.
    internal static Form LookupDialog()
    {
        var record = new JournalRecord { Name = "PLOS ONE", Publisher = "PLOS", Issns = { "1932-6203" } };
        var dialog = new JournalFactsForm(record, new Recorded(PlosOne()));
        dialog.Shown += async (_, _) => await dialog.LookUpAsync();
        return dialog;
    }

    private sealed class Recorded : IJournalFactsSource
    {
        private readonly JournalFactsLookup _lookup;
        public Recorded(JournalFactsLookup lookup) => _lookup = lookup;

        public Task<JournalFactsLookup> LookupAsync(IEnumerable<string> issns, string openAlexId, CancellationToken cancellationToken) =>
            Task.FromResult(_lookup);

        public Task<List<OpenAlexSourceMatch>> SearchAsync(string name, CancellationToken cancellationToken) =>
            Task.FromResult(OpenAlexSourceClient.ParseSearch(Fixture("openalex_search.json")));
    }

    internal static T? Find<T>(Control parent) where T : Control
    {
        foreach (Control child in parent.Controls)
        {
            if (child is T match) return match;
            var nested = Find<T>(child);
            if (nested != null) return nested;
        }
        return null;
    }
}
