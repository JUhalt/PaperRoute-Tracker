using ManuscriptPipeline.Forms;
using ManuscriptPipeline.Models;

namespace PaperRoute.V04Demo;

// One route drawn to scale (#82) on a fictional manuscript: a desk
// rejection, rerouting, two revision rounds with recorded returns to
// review, acceptance, and publication. Nothing is saved.
internal static class RouteMapDemo
{
    internal static Form Create()
    {
        var manuscript = new Manuscript
        {
            Title = "DEMO A preregistered replication of anchoring effects in clinical risk estimates",
            CurrentStage = PaperStage.Published,
            Location = ManuscriptLocation.Published,
            TargetJournal = "Fictional Open Psychology"
        };
        manuscript.Submissions.Add(Submission("Fictional Psychological Letters", new DateTime(2026, 1, 5),
            (EditorialDecision.DeskRejected, new DateTime(2026, 1, 12))));
        manuscript.Submissions.Add(Submission("Fictional Open Psychology", new DateTime(2026, 3, 1),
            (EditorialDecision.MajorRevision, new DateTime(2026, 4, 2)),
            (EditorialDecision.MinorRevision, new DateTime(2026, 7, 22)),
            (EditorialDecision.Accepted, new DateTime(2026, 8, 29))));
        manuscript.History.Add(new HistoryEvent { Stage = PaperStage.UnderReview, EventDate = new DateTime(2026, 6, 10), Note = "Resubmitted after major revision" });
        manuscript.History.Add(new HistoryEvent { Stage = PaperStage.UnderReview, EventDate = new DateTime(2026, 8, 5), Note = "Resubmitted after minor revision" });
        manuscript.Metadata.PublishedDate = new DateTime(2026, 9, 26);
        manuscript.Metadata.PublicationJournal = "Fictional Open Psychology";
        return new ManuscriptRouteViewForm(manuscript);
    }

    private static JournalSubmission Submission(string journal, DateTime submitted, params (EditorialDecision Decision, DateTime Date)[] decisions)
    {
        var submission = new JournalSubmission { JournalName = journal, SubmittedDate = submitted };
        foreach (var decision in decisions)
            submission.Decisions.Add(new EditorialDecisionEvent { Decision = decision.Decision, DecisionDate = decision.Date });
        return submission;
    }
}
