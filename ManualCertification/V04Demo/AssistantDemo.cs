using ManuscriptPipeline.Forms;
using ManuscriptPipeline.Models;
using ManuscriptPipeline.Services;

namespace PaperRoute.V04Demo;

// The optional AI assistant's windows (#84, #95) with a canned answer in place of
// a model: nothing is sent anywhere, and no key is used. The assistant is
// shown as set up for Claude so the windows name a recipient.
internal static class AssistantDemo
{
    internal const string Letter =
        "Fictional Journal of Psychology\r\nSeptember 15, 2026\r\n\r\n" +
        "Dear Dr. Demo,\r\n\r\n" +
        "Thank you for submitting \"DEMO: anchoring effects in clinical risk estimates\" (FJP-2026-0142). It was reviewed by two experts. " +
        "Based on their comments, I would like to invite a major revision. Please submit your revised manuscript within 60 days.\r\n\r\n" +
        "Reviewer 1\r\n" +
        "1. The sample size justification is unclear; please report the power analysis.\r\n" +
        "2. Report the preregistered exclusion criteria in the main text.\r\n\r\n" +
        "Reviewer 2\r\n" +
        "The discussion should address how clinicians’ experience might moderate anchoring.\r\n" +
        "Please also compare the effect with published classroom studies.\r\n\r\n" +
        "Sincerely,\r\nRiley Placeholder, Editor";

    private const string LetterAnswer =
        "{\"decision\":\"major_revision\",\"decision_quote\":\"Based on their comments, I would like to invite a major revision.\",\"decision_date\":\"2026-09-15\"," +
        "\"deadline_date\":\"\",\"deadline_days\":60,\"deadline_quote\":\"Please submit your revised manuscript within 60 days.\",\"comments\":[" +
        "{\"reviewer\":\"Reviewer 1\",\"text\":\"The sample size justification is unclear; please report the power analysis.\"}," +
        "{\"reviewer\":\"Reviewer 1\",\"text\":\"Report the preregistered exclusion criteria in the main text.\"}," +
        "{\"reviewer\":\"Reviewer 2\",\"text\":\"The discussion should address how clinicians' experience might moderate anchoring.\"}," +
        "{\"reviewer\":\"Reviewer 2\",\"text\":\"Please also compare the effect with published classroom studies.\"}," +
        "{\"reviewer\":\"Reviewer 2\",\"text\":\"Add a figure showing the anchoring effect by years of experience.\"}]}";

    private const string ResponseAnswer =
        "We thank the reviewer for this suggestion. We now compare our effect with published classroom studies in the Discussion [page and line]. " +
        "[Describe how the effects compare, and cite the studies used.]";

    private const string CoverAnswer =
        "Dear [Editor's name],\n\nWe are pleased to submit our manuscript, \"DEMO: open materials in developmental science, a survey,\" for consideration as a journal article in Fictional Open Psychology.\n\n" +
        "[One sentence on the main finding.] The work addresses open science practices and should interest the journal's readers.\n\n" +
        "The manuscript is not under consideration elsewhere, and all authors approved this submission.\n\nSincerely,\n[Your name]\n[Your affiliation]";

    // A fictional journal's instructions for authors (#95), as pasted.
    internal const string Instructions =
        "Preparing your manuscript\r\n\r\n" +
        "Research articles should not exceed 8,000 words, including references. Brief reports are limited to 3,000 words.\r\n" +
        "Each manuscript must include a structured abstract of no more than 250 words.\r\n" +
        "Provide up to six keywords.\r\n" +
        "Remove all identifying information from the manuscript file, which is reviewed double-anonymously.\r\n" +
        "A data availability statement is required for all articles.\r\n" +
        "Authors are encouraged to follow the relevant reporting guideline, such as CONSORT or PRISMA.\r\n" +
        "Figures must be uploaded as separate TIFF or EPS files at 300 dpi or higher.";

    // One requirement with a number its sentence doesn't have, one already
    // in the checklist, and one whose quote isn't in the text.
    private const string InstructionsAnswer =
        "{\"requirements\":[" +
        "{\"title\":\"Main text of at most 8,000 words\",\"category\":\"Manuscript\",\"required\":true,\"applies_to\":\"Research articles\",\"quote\":\"Research articles should not exceed 8,000 words, including references.\"}," +
        "{\"title\":\"Structured abstract of at most 250 words\",\"category\":\"Manuscript\",\"required\":true,\"applies_to\":\"\",\"quote\":\"Each manuscript must include a structured abstract of no more than 250 words.\"}," +
        "{\"title\":\"Up to 5 keywords\",\"category\":\"Manuscript\",\"required\":true,\"applies_to\":\"\",\"quote\":\"Provide up to six keywords.\"}," +
        "{\"title\":\"Anonymized manuscript file\",\"category\":\"Editorial\",\"required\":true,\"applies_to\":\"\",\"quote\":\"Remove all identifying information from the manuscript file, which is reviewed double-anonymously.\"}," +
        "{\"title\":\"Data availability statement\",\"category\":\"Compliance\",\"required\":true,\"applies_to\":\"\",\"quote\":\"A data availability statement is required for all articles.\"}," +
        "{\"title\":\"Follow a reporting guideline\",\"category\":\"Compliance\",\"required\":false,\"applies_to\":\"\",\"quote\":\"Authors are encouraged to follow the relevant reporting guideline, such as CONSORT or PRISMA.\"}," +
        "{\"title\":\"One-page cover letter\",\"category\":\"Editorial\",\"required\":true,\"applies_to\":\"\",\"quote\":\"Include a one-page cover letter addressed to the editor.\"}," +
        "{\"title\":\"Figures as separate TIFF or EPS files\",\"category\":\"Figures\",\"required\":true,\"applies_to\":\"\",\"quote\":\"Figures must be uploaded as separate TIFF or EPS files at 300 dpi or higher.\"}]}";

    private const string InstructionsLink = "https://journals.example.org/fop/authors";

    // The assistant on for Claude, with a canned provider and no questions.
    internal static void Enable()
    {
        OnlineAccess.Configure(new OnlineServicesSettings
        {
            Assistant = new AssistantSettings { Enabled = true, Provider = AssistantProvider.Claude, ClaudeModel = "claude-opus-5-5" }
        });
        AssistantService.ProviderFactory = () => new CannedProvider();
    }

    internal static AppSettings Settings() => new()
    {
        OnlineServices = new OnlineServicesSettings
        {
            Assistant = new AssistantSettings
            {
                Enabled = true, Provider = AssistantProvider.Claude, ClaudeModel = "claude-opus-5-5",
                ConfirmedUses = { "decision-letter|https://api.anthropic.com:443" }
            }
        }
    };

    internal static Form Consent() =>
        new AssistantConsentForm(AssistantService.BuildLetterRequest(Letter), OnlineAccess.CurrentAssistant());

    internal static Form LetterDialog(bool read)
    {
        var dialog = new DecisionLetterForm(DecisionLetterMode.NewDecision, Submission(), Letter, new DateTime(2026, 10, 1));
        if (read) dialog.Shown += async (_, _) => await dialog.ReadLetterAsync();
        return dialog;
    }

    internal static Form DecisionDialog()
    {
        var reply = new AssistantReply { Text = LetterAnswer, ProviderName = "Claude", Model = "claude-opus-5-5" };
        var proposal = AssistantService.ReadLetterReply(reply, Letter, new DateTime(2026, 10, 1));
        var dialog = new AddDecisionForm();
        dialog.UseProposal(proposal, Letter,
            AssistantService.SuggestionFor(AssistantService.DecisionLetterFeature, reply, proposal.DecisionQuote, DateTime.UtcNow));
        return dialog;
    }

    internal static Form DraftDialog() =>
        new AssistantDraftForm(
            AssistantService.BuildResponseRequest("Reviewer 2", "Please also compare the effect with published classroom studies.", "Added a comparison paragraph to the Discussion."),
            "We thank the reviewer.");

    internal static Form CoverLetterDialog(bool drafted)
    {
        var dialog = new CoverLetterForm(
            "DEMO: open materials in developmental science, a survey", "Fictional Open Psychology", "Journal article",
            new[] { "open science", "developmental science", "survey" },
            "A fictional abstract: we surveyed researchers about sharing study materials and report how often, and why, materials are shared.",
            name => name == "Fictional Open Psychology" ? "Open access · CC BY · double-anonymous peer review" : string.Empty);
        if (drafted) dialog.Shown += async (_, _) => await dialog.DraftAsync();
        return dialog;
    }

    internal static Form AuthorInstructionsDialog(bool read)
    {
        var dialog = new AuthorInstructionsForm("Fictional Open Psychology", InstructionsLink,
            new[] { "Anonymized manuscript file" }, new[] { "Editorial" }, Instructions);
        if (read) dialog.Shown += async (_, _) => await dialog.ReadAsync();
        return dialog;
    }

    // A journal's Readiness Checklist after Read Author Instructions, with
    // an added requirement selected to show where it came from.
    internal static Form JournalChecklist()
    {
        var reply = new AssistantReply { Text = InstructionsAnswer, ProviderName = "Claude", Model = "claude-opus-5-5" };
        var found = AssistantService.ReadAuthorInstructionsReply(reply, Instructions, null);
        var journal = new JournalRecord { Name = "Fictional Open Psychology", Publisher = "Fictional Open Publishing", AuthorInstructionsUrl = InstructionsLink };
        journal.ReadinessChecklistTemplate.Add(new JournalChecklistTemplateItem { Title = "Anonymized manuscript file", Category = "Editorial", SortOrder = 10 });
        foreach (var candidate in found.Requirements.Where(item => item.Found && item.NumbersMatch && item.Title != "Anonymized manuscript file"))
        {
            journal.ReadinessChecklistTemplate.Add(new JournalChecklistTemplateItem
            {
                Title = candidate.Title,
                Category = candidate.Category,
                IsRequired = candidate.IsRequired,
                SortOrder = (journal.ReadinessChecklistTemplate.Count + 1) * 10,
                Description = AssistantService.RequirementDescription(candidate),
                Suggestion = AssistantService.SuggestionFor(AssistantService.AuthorInstructionsFeature, reply, candidate.Quote, DateTime.UtcNow)
            });
        }
        var editor = new JournalEditForm(journal);
        editor.Shown += (_, _) =>
        {
            var tabs = JournalFactsDemo.Find<TabControl>(editor);
            if (tabs != null) tabs.SelectedIndex = 1;
            editor.SelectChecklistItemForTest(1);
        };
        return editor;
    }

    private static JournalSubmission Submission()
    {
        var submission = new JournalSubmission { JournalName = "Fictional Journal of Psychology", SubmittedDate = new DateTime(2026, 6, 1) };
        return submission;
    }

    private sealed class CannedProvider : IAssistantProvider
    {
        public string ProviderName => "Claude";

        public string Model => "claude-opus-5-5";

        public Task<AssistantReply> CompleteAsync(AssistantRequest request, CancellationToken cancellationToken)
        {
            var text = request.Feature switch
            {
                AssistantService.DecisionLetterFeature => LetterAnswer,
                AssistantService.DraftResponseFeature => ResponseAnswer,
                AssistantService.AuthorInstructionsFeature => InstructionsAnswer,
                _ => CoverAnswer
            };
            return Task.FromResult(new AssistantReply { Text = text, ProviderName = ProviderName, Model = Model });
        }
    }
}
