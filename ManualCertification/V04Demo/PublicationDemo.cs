using ManuscriptPipeline.Forms;
using ManuscriptPipeline.Models;
using ManuscriptPipeline.Services;

namespace PaperRoute.V04Demo;

// The publication check and Fill Blanks (#61) on fictional records, with
// synthetic answers instead of Crossref and ORCID. Nothing is saved.
internal static class PublicationDemo
{
    internal static Form Create(string surface)
    {
        var today = DateTime.Today;
        var library = new List<Manuscript>
        {
            Sample("DEMO Anchoring effects in clinical risk estimates: a preregistered replication", PaperStage.UnderReview, "Fictional Open Psychology", today.AddDays(-140)),
            Sample("DEMO Measurement invariance of a short grit scale across four countries", PaperStage.Accepted, "Fictional Assessment Quarterly", today.AddDays(-300)),
            Sample("DEMO Attention capture by salient distractors under working-memory load", PaperStage.Submitted, "Fictional Journal of Perception & Performance", today.AddDays(-20)),
            Sample("DEMO Pilot notes on reading fluency", PaperStage.Idea, "", today.AddDays(-5))
        };
        library[1].Metadata.Doi = "10.5555/demo.grit";
        var source = new SyntheticSource(library, today);

        if (surface == "fill")
        {
            library[1].Metadata.PublicationJournal = "Fictional Assessment Quarterly";
            return new FillBlanksForm(library, source, () => true) { Pause = TimeSpan.Zero };
        }
        return new PublicationCheckForm(library, null, "0000-0002-1825-0097", source, () => true, today) { Pause = TimeSpan.FromMilliseconds(400) };
    }

    private static Manuscript Sample(string title, PaperStage stage, string journal, DateTime submitted)
    {
        var manuscript = new Manuscript { Title = title, CurrentStage = stage, TargetJournal = journal, Location = ManuscriptLocation.Pipeline };
        if (stage >= PaperStage.Submitted)
            manuscript.Submissions.Add(new JournalSubmission { JournalName = journal, SubmittedDate = submitted });
        return manuscript;
    }

    private sealed class SyntheticSource(List<Manuscript> library, DateTime today) : IPublicationSource
    {
        public Task<CrossrefMetadataSuggestion?> LookupDoiAsync(string doi, CancellationToken cancellationToken) =>
            Task.FromResult<CrossrefMetadataSuggestion?>(doi == "10.5555/demo.grit"
                ? new CrossrefMetadataSuggestion
                {
                    Doi = doi, Title = library[1].Title.Replace("DEMO ", ""), Journal = "Fictional Assessment Quarterly",
                    Publisher = "Fictional Society Press", PublishedDate = today.AddDays(-9), Volume = "33", Issue = "4", Pages = "512-529",
                    Url = "https://example.org/fictional/grit", WorkType = "journal-article",
                    AbstractText = "A fictional abstract for the Fill Blanks preview."
                }
                : null);

        public Task<List<CrossrefMetadataSuggestion>> SearchTitleAsync(string title, CancellationToken cancellationToken) =>
            Task.FromResult(title == library[0].Title
                ? new List<CrossrefMetadataSuggestion>
                {
                    new() { Doi = "10.5555/demo.anchoring", Title = "Anchoring effects in clinical risk estimates: A preregistered replication",
                            Journal = "Fictional Open Psychology", PublishedDate = today.AddDays(-4), WorkType = "journal-article", Volume = "12", Pages = "e1043" }
                }
                : new List<CrossrefMetadataSuggestion>());

        public Task<List<OrcidWorkSuggestion>> OrcidWorksAsync(string orcid, CancellationToken cancellationToken) =>
            Task.FromResult(new List<OrcidWorkSuggestion>());
    }
}
