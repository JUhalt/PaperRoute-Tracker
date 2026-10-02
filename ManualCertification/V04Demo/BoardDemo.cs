using ManuscriptPipeline.Models;
using ManuscriptPipeline.Services;
using System.Reflection;
using System.Text.Json;

namespace PaperRoute.V04Demo;

internal static class BoardDemo
{
    internal static void RecordLayoutEvidence(ManuscriptPipeline.Form1 board, string sessionRoot)
    {
        // Harness-only diagnostics for native scaling and maximize/restore checks.
        // Delay until the resize settles; keep the last observations in this run's directory.
        var observations = new List<object>();
        var timer = new System.Windows.Forms.Timer { Interval = 300 };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            var shelves = new[] { "pipelinePanel", "publishedPanel", "fileDrawerPanel" }.Select(name =>
            {
                var shelf = (FlowLayoutPanel)typeof(ManuscriptPipeline.Form1)
                    .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(board)!;
                return new
                {
                    name, shelf.ClientSize, shelf.DisplayRectangle, shelf.AutoScrollPosition,
                    horizontal = shelf.HorizontalScroll.Visible, vertical = shelf.VerticalScroll.Visible,
                    cards = shelf.Controls.Cast<Control>().Select(card => new { card.Bounds, card.Margin }).ToArray()
                };
            }).ToArray();
            observations.Add(new { time = DateTime.UtcNow, board.DeviceDpi, board.WindowState, board.Bounds, board.ClientSize, shelves });
            if (observations.Count > 30) observations.RemoveAt(0);
            File.WriteAllText(Path.Combine(sessionRoot, "board-layout.json"),
                JsonSerializer.Serialize(observations, new JsonSerializerOptions { WriteIndented = true }));
        };
        board.Shown += (_, _) => timer.Start();
        board.SizeChanged += (_, _) => { timer.Stop(); timer.Start(); };
        board.FormClosed += (_, _) => timer.Dispose();
    }

    internal static void CreateSamples(string sessionRoot, bool empty = false)
    {
        // Verify every root before constructing any repository or the real board.
        if (!StorageEnvironment.IsIsolatedSession ||
            StorageMigrationService.CurrentDataRoot() != Path.Combine(sessionRoot, "current-data") ||
            StorageMigrationService.LegacyDataRoot() != Path.Combine(sessionRoot, "legacy-data") ||
            StorageMigrationService.CurrentManagedLibraryRoot() != Path.Combine(sessionRoot, "managed-library") ||
            StorageMigrationService.LegacyManagedLibraryRoot() != Path.Combine(sessionRoot, "legacy-managed-library"))
            throw new InvalidOperationException("All board demo storage roots must be isolated.");

        new AppSettingsService().Save(new AppSettings
        {
            CheckForUpdatesAutomatically = false,
            ReminderNotificationsEnabled = false
        });
        // Five fictional manuscripts per shelf with realistic variety: every
        // Needs Attention case, several stages, and multi-journal routes. The
        // first card on each shelf keeps a long title for layout stress.
        var today = DateTime.Today;
        const string LongTitle = ": Reproducible research across disciplines and journal-specific manuscript preparation";
        var manuscripts = new List<Manuscript>
        {
            Sample(ManuscriptLocation.Pipeline, PaperStage.Idea, "DEMO Pipeline 1" + LongTitle, "", today.AddDays(-3)),
            Sample(ManuscriptLocation.Pipeline, PaperStage.Submitted, "DEMO Pipeline 2: Measurement invariance of a short grit scale across four countries", "Fictional Assessment Quarterly", today.AddDays(-6),
                Submission("Fictional Assessment Quarterly", today.AddDays(-6))),
            Sample(ManuscriptLocation.Pipeline, PaperStage.UnderReview, "DEMO Pipeline 3: Attention capture by salient distractors under working-memory load", "Fictional Journal of Perception & Performance", today.AddDays(-104),
                Submission("Fictional Psychological Letters", today.AddDays(-150), (EditorialDecision.DeskRejected, today.AddDays(-143), null)),
                Submission("Fictional Journal of Perception & Performance", today.AddDays(-104))),
            Sample(ManuscriptLocation.Pipeline, PaperStage.Revision, "DEMO Pipeline 4: A preregistered replication of anchoring effects in clinical risk estimates", "Fictional Open Psychology", today.AddDays(-31),
                Submission("Fictional Psychological Letters", today.AddDays(-150), (EditorialDecision.DeskRejected, today.AddDays(-143), null)),
                Submission("Fictional Open Psychology", today.AddDays(-67), (EditorialDecision.MajorRevision, today.AddDays(-31), today.AddDays(9)))),
            Sample(ManuscriptLocation.Pipeline, PaperStage.Draft, "DEMO Pipeline 5: Teaching open science to nursing students, a mixed-methods evaluation", "Fictional Nurse Education Review", today.AddDays(-5),
                Submission("Fictional Nursing Methods", today.AddDays(-60), (EditorialDecision.RejectedAfterReview, today.AddDays(-5), null))),

            Sample(ManuscriptLocation.Published, PaperStage.Published, "DEMO Published 1" + LongTitle, "Fictional Journal of Reproducible Research and Interdisciplinary Methods", today.AddDays(-40),
                Submission("Fictional Journal of Reproducible Research and Interdisciplinary Methods", today.AddDays(-300), (EditorialDecision.Accepted, today.AddDays(-120), null))),
            Sample(ManuscriptLocation.Published, PaperStage.Published, "DEMO Published 2: Retrieval practice in introductory statistics", "Fictional Teaching of Psychology", today.AddDays(-200),
                Submission("Fictional Learning Science", today.AddDays(-520), (EditorialDecision.Rejected, today.AddDays(-480), null)),
                Submission("Fictional Teaching of Psychology", today.AddDays(-450), (EditorialDecision.Accepted, today.AddDays(-300), null))),
            Sample(ManuscriptLocation.Published, PaperStage.Published, "DEMO Published 3: Sleep and memory consolidation in older adults", "Fictional Aging & Cognition", today.AddDays(-400),
                Submission("Fictional Aging & Cognition", today.AddDays(-700), (EditorialDecision.Accepted, today.AddDays(-500), null))),
            Sample(ManuscriptLocation.Published, PaperStage.Published, "DEMO Published 4: A tutorial on equivalence testing", "Fictional Methods Review", today.AddDays(-600)),
            Sample(ManuscriptLocation.Published, PaperStage.Published, "DEMO Published 5: Open materials in developmental science", "Fictional Child Development Reports", today.AddDays(-800),
                Submission("Fictional Developmental Letters", today.AddDays(-1100), (EditorialDecision.DeskRejected, today.AddDays(-1090), null)),
                Submission("Fictional Infancy Studies", today.AddDays(-1050), (EditorialDecision.RejectedAfterReview, today.AddDays(-980), null)),
                Submission("Fictional Child Development Reports", today.AddDays(-950), (EditorialDecision.Accepted, today.AddDays(-850), null))),

            Sample(ManuscriptLocation.FileDrawer, PaperStage.Draft, "DEMO File Drawer 1" + LongTitle, "Fictional Journal of Reproducible Research and Interdisciplinary Methods", today.AddDays(-90),
                Submission("Fictional Journal A", today.AddDays(-400), (EditorialDecision.Rejected, today.AddDays(-360), null)),
                Submission("Fictional Journal B", today.AddDays(-330), (EditorialDecision.DeskRejected, today.AddDays(-320), null)),
                Submission("Fictional Journal C", today.AddDays(-300), (EditorialDecision.RejectedAfterReview, today.AddDays(-200), null))),
            Sample(ManuscriptLocation.FileDrawer, PaperStage.Draft, "DEMO File Drawer 2: A null result on priming and choice", "Fictional Social Cognition", today.AddDays(-180),
                Submission("Fictional Social Cognition", today.AddDays(-260), (EditorialDecision.Withdrawn, today.AddDays(-200), null))),
            Sample(ManuscriptLocation.FileDrawer, PaperStage.Idea, "DEMO File Drawer 3: Pilot notes on reading fluency", "", today.AddDays(-500)),
            Sample(ManuscriptLocation.FileDrawer, PaperStage.Draft, "DEMO File Drawer 4: Revisiting the ego-depletion paradigm", "Fictional Motivation Science", today.AddDays(-250),
                Submission("Fictional Motivation Science", today.AddDays(-320), (EditorialDecision.Rejected, today.AddDays(-280), null))),
            Sample(ManuscriptLocation.FileDrawer, PaperStage.Draft, "DEMO File Drawer 5: An unfinished scale-development project", "Fictional Assessment Quarterly", today.AddDays(-700))
        };
        // One fictional Journal Library record, linked from the Revision sample,
        // so the manuscript page shows its notes and checklist.
        var openPsychology = new JournalRecord
        {
            Name = "Fictional Open Psychology",
            Publisher = "Fictional Society Press",
            Notes = "Fictional sample notes: results sections must report exact p values and effect sizes with confidence intervals. The response letter is uploaded as a separate file.",
            ReadinessChecklistTemplate = new List<JournalChecklistTemplateItem>
            {
                new() { Title = "Data availability statement", SortOrder = 1 },
                new() { Title = "Preregistration link", SortOrder = 2 },
                new() { Title = "Response letter as a separate file", SortOrder = 3 }
            }
        };
        var revision = manuscripts.Single(item => item.CurrentStage == PaperStage.Revision);
        revision.TargetJournalId = openPsychology.Id;

        // Fictional reviewer comments on the major-revision decision, so the
        // Submissions tab shows the inline response matrix.
        var revisionSubmission = revision.Submissions.Last();
        var majorRevision = revisionSubmission.Decisions.Single();
        revisionSubmission.ReviewerResponses.AddRange(new[]
        {
            Response(majorRevision, "Reviewer 1", ReviewerResponseStatus.Unresolved,
                "Explain how the sample size was determined and report the smallest effect size of interest.", "", "Method, p. 7"),
            Response(majorRevision, "Reviewer 1", ReviewerResponseStatus.InProgress,
                "Report the anchoring effect separately for high- and low-numeracy clinicians.",
                "We now report the effect separately by numeracy group in Table 2 and Figure 3.", "Results, Table 2"),
            Response(majorRevision, "Reviewer 2", ReviewerResponseStatus.Addressed,
                "Add a data availability statement and link the preregistration.",
                "Added under Open Practices, with the preregistration link.", "Open Practices"),
            Response(majorRevision, "Editor", ReviewerResponseStatus.NotApplicable,
                "Consider a Bayesian reanalysis of Study 2.",
                "We explain why the preregistered analysis is retained.", "Study 2")
        });

        // Deadlines: follow-ups, reminders (one done), and an unsubmitted
        // packet whose checklist is still open.
        var submitted = manuscripts[1];
        submitted.Submissions.Single().FollowUpDate = today.AddDays(24);
        var underReview = manuscripts[2];
        underReview.Submissions.Last().FollowUpDate = today.AddDays(-4);
        underReview.PublicationMatches.Add(new PublicationMatch
        {
            Doi = "10.5555/demo.attention", Title = "Attention capture by salient distractors under working-memory load",
            Journal = "Fictional Journal of Perception & Performance", PublishedDate = today.AddDays(-2), Source = PublicationMatchSource.Title
        });
        var draft = manuscripts[4];
        draft.Reminders.Add(new ManuscriptReminder { Title = "Send the revised draft to coauthors", DueDate = today });
        draft.Reminders.Add(new ManuscriptReminder { Title = "Ask the librarian about the search strategy", DueDate = today.AddDays(5) });
        draft.Reminders.Add(new ManuscriptReminder { Title = "Book the ethics amendment", DueDate = today.AddDays(-6), IsCompleted = true, CompletedDate = today.AddDays(-2) });
        var readiness = new ManuscriptReadiness { JournalName = "Fictional Nurse Education Review" };
        readiness.Items.AddRange(new[]
        {
            new ReadinessItemState { Title = "Cover letter", IsRequired = true, Status = ReadinessItemStatus.Complete },
            new ReadinessItemState { Title = "Ethics statement", IsRequired = true, Status = ReadinessItemStatus.Complete },
            new ReadinessItemState { Title = "Data availability statement", IsRequired = true, Status = ReadinessItemStatus.Unresolved },
            new ReadinessItemState { Title = "Structured abstract", IsRequired = true, Status = ReadinessItemStatus.Unresolved }
        });
        draft.ReadinessProfiles.Add(readiness);
        var draftVersion = new ManuscriptVersion { Label = "Submission draft", CreatedDate = today.AddDays(-3), Notes = "Fictional version with no file." };
        draft.Versions.Add(draftVersion);
        draft.SubmissionPackets.Add(new SubmissionPacket { ReadinessProfileId = readiness.Id, JournalName = readiness.JournalName, Label = "Initial submission", ManuscriptVersionId = draftVersion.Id });
        manuscripts[0].Reminders.Add(new ManuscriptReminder { Title = "Outline the introduction", DueDate = today.AddDays(12) });
        // Types and tags (#64) on a few cards.
        manuscripts[0].Tags.Add("grant");
        manuscripts[3].WorkType = WorkType.JournalArticle;
        manuscripts[3].Tags.AddRange(new[] { "preregistered", "lab project" });
        manuscripts[4].WorkType = WorkType.JournalArticle;
        manuscripts[4].Tags.AddRange(new[] { "teaching", "mixed methods", "nursing" });
        // A journal shortlist (#65) whose next journal is offered after the rejection.
        manuscripts[4].JournalShortlist.AddRange(new[]
        {
            new JournalCandidate { JournalName = "Fictional Nursing Methods", Status = CandidateStatus.Preferred, Notes = "Methods focus; rejected after review",
                Checks = { "trust.known", "trust.publisher", "trust.review", "trust.indexed", "trust.fees", "trust.guidelines", "fit.scope", "fit.type" } },
            new JournalCandidate { JournalName = "Fictional Journal of Nursing Scholarship", Status = CandidateStatus.Preferred, Notes = "Publishes mixed-methods evaluations of teaching",
                Checks = { "trust.known", "trust.publisher", "trust.review", "trust.indexed", "trust.fees", "trust.guidelines", "trust.member", "fit.scope", "fit.type", "fit.audience", "fit.timeline" } },
            new JournalCandidate { JournalName = "Fictional Nurse Education Review", Status = CandidateStatus.Backup, Notes = "Broad readership; slower review",
                Checks = { "trust.known", "trust.review", "fit.scope", "fit.audience" } },
            new JournalCandidate { JournalName = "Fictional Rapid Health Letters", Status = CandidateStatus.RuledOut, Notes = "Unclear fees and peer review" }
        });
        manuscripts[5].WorkType = WorkType.JournalArticle;
        manuscripts[6].WorkType = WorkType.JournalArticle;
        manuscripts[8].WorkType = WorkType.ConferencePaper;

        var library = new AuthorLibraryData();
        library.Journals.Add(openPsychology);
        new AuthorLibraryRepository().Save(empty ? new AuthorLibraryData() : library);
        // --empty shows the first-run welcome instead of the sample shelves.
        new ManuscriptRepository().Save(empty ? new List<Manuscript>() : manuscripts);
    }

    private static Manuscript Sample(ManuscriptLocation location, PaperStage stage, string title, string journal,
        DateTime stageEntered, params JournalSubmission[] submissions)
    {
        var manuscript = new Manuscript
        {
            Title = title,
            Location = location,
            CurrentStage = stage,
            StageEnteredDate = stageEntered,
            TargetJournal = journal
        };
        manuscript.History.Add(new HistoryEvent
        {
            Stage = PaperStage.Idea,
            EventDate = stageEntered.AddDays(-30),
            Note = "Fictional sample for board and card checks."
        });
        if (stage != PaperStage.Idea)
            manuscript.History.Add(new HistoryEvent { Stage = stage, EventDate = stageEntered, Note = "Fictional stage change." });
        manuscript.Submissions.AddRange(submissions);
        return manuscript;
    }

    private static JournalSubmission Submission(string journal, DateTime submitted,
        params (EditorialDecision Decision, DateTime Date, DateTime? RevisionDeadline)[] decisions)
    {
        var submission = new JournalSubmission { JournalName = journal, SubmittedDate = submitted, ManuscriptNumber = "DEMO-" + submitted.ToString("yyMMdd") };
        foreach (var decision in decisions)
            submission.Decisions.Add(new EditorialDecisionEvent { Decision = decision.Decision, DecisionDate = decision.Date, RevisionDeadline = decision.RevisionDeadline });
        return submission;
    }

    private static ReviewerResponseItem Response(EditorialDecisionEvent decision, string reviewer, ReviewerResponseStatus status,
        string comment, string response, string location) => new()
    {
        DecisionId = decision.Id,
        RevisionRoundNumber = 1,
        ReviewerLabel = reviewer,
        Status = status,
        CommentText = comment,
        ResponseText = response,
        ManuscriptLocation = location
    };
}
