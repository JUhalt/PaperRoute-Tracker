using ManuscriptPipeline.Forms;
using ManuscriptPipeline.Models;
using ManuscriptPipeline.Services;

namespace PaperRoute.V04Demo;

// Find Journals (#88) on recorded OpenAlex answers (the test fixtures), so
// the window can be checked without any network request.
internal static class JournalSuggestionsDemo
{
    private static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(Path.GetDirectoryName(JournalFactsDemo.FixtureFolder())!, "JournalSuggestions", name));

    private static JournalSuggestionsResult Recorded(JournalSuggestionRequest request)
    {
        var groups = JournalSuggestionService.ParseGroups(Fixture("journals_and.json"));
        var totals = JournalSuggestionService.ParseGroups(Fixture("totals.json"));
        var details = JournalSuggestionService.ParseSourceList(Fixture("details.json"));
        var examples = JournalSuggestionService.ParseExamples(Fixture("examples.json"));
        var result = new JournalSuggestionsResult { Request = request, RetrievedUtc = DateTime.UtcNow };
        foreach (var group in groups)
        {
            var source = details.FirstOrDefault(item => item.Id == group.Id);
            var journal = new JournalSuggestion
            {
                OpenAlexId = group.Id,
                Name = source?.DisplayName ?? group.Name,
                Publisher = source?.Publisher ?? "",
                Issns = source?.Issns ?? new List<string>(),
                IsOa = source?.IsOa,
                IsInDoaj = source?.IsInDoaj,
                ApcPrices = source?.ApcPrices ?? new List<ListedPrice>(),
                MatchingArticles = group.Count,
                AllArticles = totals.Where(item => item.Id == group.Id).Select(item => (long?)item.Count).FirstOrDefault(),
                Examples = examples.Where(item => item.SourceId == group.Id).Select(item => item.Example).Take(3).ToList(),
                ExamplesLoaded = true
            };
            result.Journals.Add(journal);
        }
        result.TotalMatching = groups.Sum(item => item.Count);
        return result;
    }

    internal static Form Dialog(bool search)
    {
        var dialog = new JournalSuggestionsForm(
            "Anchoring effects in clinical risk estimates: a preregistered replication",
            new[] { "clinical judgment" },
            new RecordedSource(),
            journal => journal.OpenAlexId == "S9692511",
            journal => journal.OpenAlexId == "S196734849" ? "Submitted 1×, last 2024" : "");
        if (search)
        {
            dialog.Shown += async (_, _) =>
            {
                dialog.KeywordsList.SetItemChecked(1, true);
                await dialog.SearchAsync();
            };
        }
        return dialog;
    }

    private sealed class RecordedSource : IJournalSuggestionsSource
    {
        public Task<JournalSuggestionsResult> SearchAsync(JournalSuggestionRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(Recorded(request));

        public Task<List<EvidenceExample>> ExamplesAsync(JournalSuggestionRequest request, string openAlexId, CancellationToken cancellationToken) =>
            Task.FromResult(new List<EvidenceExample>());
    }
}
