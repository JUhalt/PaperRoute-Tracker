Imports System
Imports System.Collections.Generic
Imports System.Diagnostics
Imports System.IO
Imports System.Linq
Imports ManuscriptPipeline.Models

Namespace Services

    ' The example library for teaching and first-time exploration (#83): a
    ' fictional research group's work, opened in a separate PaperRoute
    ' window whose storage is a new temporary folder. The user's own library
    ' is never opened, and the example is discarded when its window closes.
    Public NotInheritable Class ExampleLibraryService

        Private Sub New()
        End Sub

        Public Const Argument As String = "--example"
        Public Const WorkOfflineArgument As String = "--work-offline"
        Public Const ServicesOffArgument As String = "--services-off="

        Private Shared _sessionRoot As String


        Public Shared ReadOnly Property IsActive As Boolean
            Get
                Return _sessionRoot IsNot Nothing
            End Get
        End Property


        Public Shared Function IsExampleLaunch(args As String()) As Boolean
            Return args IsNot Nothing AndAlso args.Any(Function(item) String.Equals(item, Argument, StringComparison.OrdinalIgnoreCase))
        End Function


        ' Opens the example in a new PaperRoute process, which keeps the
        ' user's Online services choices (#86).
        Public Shared Sub Launch(Optional online As OnlineServicesSettings = Nothing)
            Dim start As New ProcessStartInfo(Environment.ProcessPath) With {.UseShellExecute = False}
            start.ArgumentList.Add(Argument)
            For Each item As String In OnlineArguments(online)
                start.ArgumentList.Add(item)
            Next
            Process.Start(start)?.Dispose()
        End Sub


        Friend Shared Function OnlineArguments(online As OnlineServicesSettings) As List(Of String)
            Dim result As New List(Of String)()
            If online Is Nothing Then Return result
            If online.WorkOffline Then result.Add(WorkOfflineArgument)
            Dim off As List(Of String) = If(online.TurnedOff, New List(Of String)()).Where(AddressOf IsServiceId).ToList()
            If off.Count > 0 Then result.Add(ServicesOffArgument & String.Join(",", off))
            Return result
        End Function


        Friend Shared Function OnlineSettingsFrom(args As String()) As OnlineServicesSettings
            Dim settings As New OnlineServicesSettings()
            For Each item As String In If(args, Array.Empty(Of String)())
                If String.Equals(item, WorkOfflineArgument, StringComparison.OrdinalIgnoreCase) Then settings.WorkOffline = True
                If item IsNot Nothing AndAlso item.StartsWith(ServicesOffArgument, StringComparison.OrdinalIgnoreCase) Then
                    settings.TurnedOff.AddRange(item.Substring(ServicesOffArgument.Length).Split(","c).Where(AddressOf IsServiceId))
                End If
            Next
            Return settings
        End Function


        Private Shared Function IsServiceId(value As String) As Boolean
            Return Not String.IsNullOrEmpty(value) AndAlso value.Length <= 40 AndAlso
                value.All(Function(character) (character >= "a"c AndAlso character <= "z"c) OrElse (character >= "0"c AndAlso character <= "9"c) OrElse character = "-"c)
        End Function


        ' Must run before any storage root is resolved: every PaperRoute
        ' folder, settings included, then lives in the session folder.
        Friend Shared Sub StartSession()
            Dim root As String = Path.Combine(Path.GetTempPath(), "PaperRoute-Example-" & Guid.NewGuid().ToString("N"))
            StorageEnvironment.ConfigureIsolatedSessionRoot(root)
            _sessionRoot = root
        End Sub


        ' Writes the example, and the Online services choices it was opened
        ' with, into the session's fresh storage.
        Friend Shared Sub Seed(today As DateTime, Optional online As OnlineServicesSettings = Nothing)
            If Not IsActive Then Throw New InvalidOperationException("The example library is written only into its own session.")
            Dim example = Create(today)
            Dim authors As New AuthorLibraryRepository()
            authors.Save(example.Library)
            Dim manuscripts As New ManuscriptRepository()
            manuscripts.Save(example.Manuscripts)
            Dim citations As New CitationStore()
            citations.Save(CreateCitations(today, example.Manuscripts))
            Dim settings As New AppSettingsService()
            settings.Save(New AppSettings With {.OnlineServices = If(online, New OnlineServicesSettings())})
        End Sub


        Friend Shared Sub EndSession()
            If _sessionRoot Is Nothing Then Return
            Try
                If Directory.Exists(_sessionRoot) Then Directory.Delete(_sessionRoot, True)
            Catch ex As Exception When TypeOf ex Is IOException OrElse TypeOf ex Is UnauthorizedAccessException
                ' A later run of Windows' temporary-file cleanup removes it.
            End Try
        End Sub


        ' The fictional Example Lab: every route a new researcher needs to
        ' see, with dates relative to today so Deadlines always has overdue,
        ' today, soon, and later items.
        Public Shared Function Create(today As DateTime) As (Manuscripts As List(Of Manuscript), Library As AuthorLibraryData)

            Dim day As DateTime = today.Date
            Dim library As New AuthorLibraryData()
            Dim lab As New AffiliationRecord With {.Institution = "Example University", .Department = "Department of Psychology", .City = "Exampleton"}
            library.Affiliations.Add(lab)
            Dim lead As New AuthorRecord With {.GivenName = "Avery", .FamilyName = "Example", .IsMe = True, .Notes = "Fictional author."}
            Dim student As New AuthorRecord With {.GivenName = "Jordan", .FamilyName = "Sample", .Notes = "Fictional author."}
            Dim coauthor As New AuthorRecord With {.GivenName = "Riley", .FamilyName = "Placeholder", .Notes = "Fictional author."}
            library.Authors.AddRange({lead, student, coauthor})

            Dim letters As New JournalRecord With {.Name = "Fictional Psychological Letters", .Publisher = "Fictional Press", .Notes = "Fictional journal. Short reports; desk decisions within about a week."}
            letters.ReadinessChecklistTemplate.AddRange({
                New JournalChecklistTemplateItem With {.Title = "Cover letter", .SortOrder = 1},
                New JournalChecklistTemplateItem With {.Title = "Word count under 5,000", .SortOrder = 2},
                New JournalChecklistTemplateItem With {.Title = "Data availability statement", .SortOrder = 3}
            })
            Dim openPsychology As New JournalRecord With {.Name = "Fictional Open Psychology", .Publisher = "Fictional Open Publishing", .IsShortlisted = True, .Notes = "Fictional open-access journal."}
            Dim methods As New JournalRecord With {.Name = "Fictional Journal of Research Methods", .Publisher = "Fictional Press", .Notes = "Fictional journal."}
            Dim assessment As New JournalRecord With {.Name = "Fictional Assessment Quarterly", .Publisher = "Fictional Society Publications", .Notes = "Fictional journal."}
            library.Journals.AddRange({letters, openPsychology, methods, assessment})

            ' Fictional facts and metrics (#87), so the Journals page shows what
            ' a looked-up journal looks like. They name no real index.
            Dim checkedUtc As DateTime = DateTime.SpecifyKind(day.AddDays(-12), DateTimeKind.Utc)
            Dim fact As Func(Of String, String, JournalFact) =
                Function(key, value) New JournalFact With {.Key = key, .Value = value, .Source = JournalFactCatalog.ExampleSource, .CheckedUtc = checkedUtc}
            openPsychology.AimsScopeUrl = "https://example.org/fictional-open-psychology/aims"
            openPsychology.AuthorInstructionsUrl = "https://example.org/fictional-open-psychology/authors"
            openPsychology.EditorialBoardUrl = "https://example.org/fictional-open-psychology/board"
            openPsychology.Facts.AddRange({
                fact(JournalFactCatalog.OpenAccess, "Fully open access"),
                fact(JournalFactCatalog.Apc, "No publication fee"),
                fact(JournalFactCatalog.License, "CC BY"),
                fact(JournalFactCatalog.Copyright, "Authors keep copyright"),
                fact(JournalFactCatalog.Review, "Double anonymous peer review"),
                fact(JournalFactCatalog.Weeks, "About 14 weeks from submission to publication"),
                New JournalFact With {.Key = JournalFactCatalog.CiteScore, .Value = "3.1", .Year = day.Year - 1, .Source = JournalFactCatalog.ExampleSource, .CheckedUtc = checkedUtc, .EnteredByYou = True},
                New JournalFact With {.Key = JournalFactCatalog.AcceptanceRate, .Value = "About 30%", .Year = day.Year - 1, .Source = JournalFactCatalog.ExampleSource, .CheckedUtc = checkedUtc, .EnteredByYou = True}
            })
            methods.Facts.Add(fact(JournalFactCatalog.Review, "Double anonymous peer review"))
            assessment.Facts.Add(fact(JournalFactCatalog.Review, "Single anonymous peer review"))
            letters.Facts.AddRange({
                fact(JournalFactCatalog.OpenAccess, "Not fully open access (subscription or hybrid)"),
                fact(JournalFactCatalog.Review, "Single anonymous peer review"),
                New JournalFact With {.Key = JournalFactCatalog.DecisionTime, .Value = "About 1 week for a desk decision", .Year = day.Year - 1, .Source = JournalFactCatalog.ExampleSource, .CheckedUtc = checkedUtc, .EnteredByYou = True}
            })

            Dim manuscripts As New List(Of Manuscript)()

            ' 1. Published: a desk rejection, rerouting, two revision rounds with
            ' recorded returns to review, acceptance, and publication.
            Dim published As Manuscript = Paper("Example: anchoring effects in clinical risk estimates, a preregistered replication",
                                                PaperStage.Published, ManuscriptLocation.Published, openPsychology, day.AddDays(-156), lead, student)
            published.WorkType = WorkType.JournalArticle
            published.Tags.Add("replication")
            Dim start As DateTime = day.AddDays(-420)
            published.Submissions.Add(Submitted(letters, start, (EditorialDecision.DeskRejected, start.AddDays(7), Nothing)))
            Dim second As JournalSubmission = Submitted(openPsychology, start.AddDays(55),
                (EditorialDecision.MajorRevision, start.AddDays(87), CType(start.AddDays(147), DateTime?)),
                (EditorialDecision.MinorRevision, start.AddDays(198), CType(start.AddDays(228), DateTime?)),
                (EditorialDecision.Accepted, start.AddDays(236), Nothing))
            Dim major As EditorialDecisionEvent = second.Decisions(0)
            second.ReviewerResponses.AddRange({
                Response(major, "Reviewer 1", ReviewerResponseStatus.Addressed, "Report the preregistered exclusion criteria in the main text.", "Moved the criteria from the supplement into the Method.", "Method, Participants"),
                Response(major, "Reviewer 1", ReviewerResponseStatus.Addressed, "Add an equivalence test for the null effect.", "Added TOST equivalence tests with the preregistered bounds.", "Results"),
                Response(major, "Reviewer 2", ReviewerResponseStatus.Addressed, "Discuss how clinical experience might moderate anchoring.", "Added a paragraph and an exploratory analysis by years of practice.", "Discussion"),
                Response(major, "Editor", ReviewerResponseStatus.Addressed, "Shorten the introduction by about a third.", "Cut the introduction from 1,600 to 1,050 words.", "Introduction")
            })
            published.Submissions.Add(second)
            published.History.Add(New HistoryEvent With {.Stage = PaperStage.UnderReview, .EventDate = start.AddDays(156), .Note = "Resubmitted after major revision."})
            published.History.Add(New HistoryEvent With {.Stage = PaperStage.UnderReview, .EventDate = start.AddDays(212), .Note = "Resubmitted after minor revision."})
            published.Versions.Add(New ManuscriptVersion With {.Label = "Submitted to Fictional Open Psychology", .CreatedDate = start.AddDays(55), .SubmissionId = second.Id, .Notes = "Metadata only; fictional."})
            published.Versions.Add(New ManuscriptVersion With {.Label = "Revision 1", .CreatedDate = start.AddDays(156), .SubmissionId = second.Id, .DecisionId = major.Id, .RevisionRoundNumber = 1, .Notes = "Metadata only; fictional."})
            published.Metadata.Doi = "10.5555/example.anchoring"
            published.Metadata.PublicationJournal = openPsychology.Name
            published.Metadata.PublishedDate = start.AddDays(264)
            published.Metadata.AbstractText = "A fictional abstract for the example library."
            manuscripts.Add(published)

            ' 2. In revision: reviewer comments at several stages, with a
            ' revision deadline ahead.
            Dim revising As Manuscript = Paper("Example: retrieval practice in an introductory statistics course",
                                               PaperStage.Revision, ManuscriptLocation.Pipeline, methods, day.AddDays(-20), lead, coauthor)
            revising.WorkType = WorkType.JournalArticle
            revising.Tags.Add("teaching")
            revising.RevisionDeadline = day.AddDays(10)
            Dim revisionSubmission As JournalSubmission = Submitted(methods, day.AddDays(-80), (EditorialDecision.MajorRevision, day.AddDays(-20), CType(day.AddDays(10), DateTime?)))
            Dim request As EditorialDecisionEvent = revisionSubmission.Decisions(0)
            revisionSubmission.ReviewerResponses.AddRange({
                Response(request, "Reviewer 1", ReviewerResponseStatus.Addressed, "Clarify how quiz attendance was recorded.", "Added the attendance procedure and a table of completion rates.", "Method"),
                Response(request, "Reviewer 1", ReviewerResponseStatus.InProgress, "Model the nesting of students within sections.", "Drafting a multilevel model; results pending.", "Results"),
                Response(request, "Reviewer 2", ReviewerResponseStatus.Unresolved, "Compare the effect with published classroom studies.", String.Empty, "Discussion"),
                Response(request, "Editor", ReviewerResponseStatus.NotApplicable, "Consider a registered report for the follow-up.", "Noted for the next study; not part of this manuscript.", String.Empty)
            })
            revising.Submissions.Add(revisionSubmission)
            revising.Versions.Add(New ManuscriptVersion With {.Label = "Submitted to Fictional Journal of Research Methods", .CreatedDate = day.AddDays(-80), .SubmissionId = revisionSubmission.Id, .Notes = "Metadata only; fictional."})
            manuscripts.Add(revising)

            ' 3. Under review for a long time, with a follow-up now overdue.
            Dim waiting As Manuscript = Paper("Example: sleep and memory consolidation in older adults",
                                              PaperStage.UnderReview, ManuscriptLocation.Pipeline, assessment, day.AddDays(-104), student, lead)
            waiting.WorkType = WorkType.JournalArticle
            Dim longReview As JournalSubmission = Submitted(assessment, day.AddDays(-104))
            longReview.FollowUpDate = day.AddDays(-4)
            waiting.Submissions.Add(longReview)
            manuscripts.Add(waiting)

            ' 4. Rejected after review last week; the shortlist offers the
            ' next journal.
            Dim rerouting As Manuscript = Paper("Example: measurement invariance of a short grit scale across four countries",
                                                PaperStage.Draft, ManuscriptLocation.Pipeline, methods, day.AddDays(-6), lead, student, coauthor)
            rerouting.WorkType = WorkType.JournalArticle
            rerouting.Submissions.Add(Submitted(methods, day.AddDays(-70), (EditorialDecision.RejectedAfterReview, day.AddDays(-6), Nothing)))
            rerouting.JournalShortlist.AddRange({
                New JournalCandidate With {.JournalName = methods.Name, .JournalId = methods.Id, .Status = CandidateStatus.Preferred, .Notes = "Methods focus; rejected after review.",
                                           .Checks = New List(Of String) From {"trust.known", "trust.publisher", "trust.review", "trust.guidelines", "fit.scope", "fit.type"}},
                New JournalCandidate With {.JournalName = assessment.Name, .JournalId = assessment.Id, .Status = CandidateStatus.Preferred, .Notes = "Publishes invariance studies; reviewers asked for exactly this.",
                                           .Checks = New List(Of String) From {"trust.known", "trust.publisher", "trust.review", "trust.indexed", "trust.fees", "trust.guidelines", "fit.scope", "fit.type", "fit.audience"}},
                New JournalCandidate With {.JournalName = openPsychology.Name, .JournalId = openPsychology.Id, .Status = CandidateStatus.Backup, .Notes = "Broad readership; open access.",
                                           .Checks = New List(Of String) From {"trust.known", "trust.fees", "fit.fees"},
                                           .Evidence = New CandidateEvidence With {
                                               .Source = JournalFactCatalog.ExampleSource,
                                               .Keywords = New List(Of String) From {"measurement invariance", "grit"},
                                               .MatchAll = True,
                                               .SinceDate = day.AddYears(-5),
                                               .MatchingArticles = 6,
                                               .AllArticles = 1840,
                                               .Examples = New List(Of EvidenceExample) From {
                                                   New EvidenceExample With {.Title = "Example: invariance of a brief self-control scale across age groups", .Year = day.Year - 1, .Doi = "10.5555/example.invariance"},
                                                   New EvidenceExample With {.Title = "Example: grit and course persistence in first-year students", .Year = day.Year - 2, .Doi = "10.5555/example.grit"}
                                               },
                                               .RetrievedUtc = DateTime.SpecifyKind(day.AddDays(-30), DateTimeKind.Utc)
                                           }}
            })
            manuscripts.Add(rerouting)

            ' 5. A preprint being prepared for a journal, with a reminder due
            ' today.
            Dim preprint As Manuscript = Paper("Example: open materials in developmental science, a survey",
                                               PaperStage.Draft, ManuscriptLocation.Pipeline, openPsychology, day.AddDays(-12), coauthor, lead)
            preprint.WorkType = WorkType.Preprint
            preprint.Tags.Add("open science")
            preprint.Metadata.PreprintDoi = "10.5555/example.preprint"
            preprint.Metadata.PreprintUrl = "https://example.org/preprints/open-materials"
            preprint.Reminders.Add(New ManuscriptReminder With {.Title = "Draft the cover letter", .DueDate = day})
            manuscripts.Add(preprint)

            ' 6. The File Drawer: three rejections, then set aside.
            Dim filed As Manuscript = Paper("Example: a null result on priming and choice",
                                            PaperStage.Draft, ManuscriptLocation.FileDrawer, Nothing, day.AddDays(-60), student)
            filed.Submissions.Add(Submitted(letters, day.AddDays(-400), (EditorialDecision.DeskRejected, day.AddDays(-393), Nothing)))
            filed.Submissions.Add(Submitted(methods, day.AddDays(-360), (EditorialDecision.RejectedAfterReview, day.AddDays(-280), Nothing)))
            filed.Submissions.Add(Submitted(assessment, day.AddDays(-250), (EditorialDecision.Rejected, day.AddDays(-190), Nothing)))
            filed.FileDrawerDate = day.AddDays(-60)
            filed.FileDrawerReason = "Three rejections; revisit with a larger sample."
            manuscripts.Add(filed)

            ' 7. An idea, with a reminder later this week.
            Dim idea As Manuscript = Paper("Example: pilot notes on reading fluency", PaperStage.Idea, ManuscriptLocation.Pipeline, Nothing, day.AddDays(-3), lead)
            idea.Reminders.Add(New ManuscriptReminder With {.Title = "Outline the pilot study", .DueDate = day.AddDays(3)})
            manuscripts.Add(idea)

            Return (manuscripts, library)

        End Function


        ' Fictional Your Citations figures (#91) for the example library:
        ' the published example manuscript and five other works, with
        ' 10.5555 example DOIs. Updating is off in the example.
        Public Shared Function CreateCitations(today As DateTime, manuscripts As IEnumerable(Of Manuscript)) As CitationSnapshot

            Dim year As Integer = today.Year
            Dim anchoring As Manuscript = If(manuscripts, Enumerable.Empty(Of Manuscript)()).
                FirstOrDefault(Function(item) item?.Metadata IsNot Nothing AndAlso item.Metadata.Doi = "10.5555/example.anchoring")
            Dim anchoringYear As Integer = If(anchoring?.Metadata.PublishedDate?.Year, year)

            Dim work As Func(Of String, String, String, Integer, Integer(), Double?, Double?, CitedWork) =
                Function(doi, title, journal, published, perYear, fwci, percentile)
                    Dim item As New CitedWork With {
                        .Doi = doi, .Title = title, .Journal = journal, .Year = published,
                        .Fwci = fwci, .Percentile = percentile, .InTop10Percent = percentile.HasValue AndAlso percentile.Value >= 0.9,
                        .FoundBy = "Example"
                    }
                    ' perYear lists this year's citations first, then earlier years.
                    For index As Integer = 0 To perYear.Length - 1
                        If perYear(index) > 0 AndAlso year - index >= published Then item.CountsByYear.Add(New YearCount With {.Year = year - index, .Count = perYear(index)})
                    Next
                    item.CitedByCount = item.CountsByYear.Sum(Function(entry) entry.Count)
                    Return item
                End Function

            Dim snapshot As New CitationSnapshot With {.Source = JournalFactCatalog.ExampleSource, .RetrievedUtc = today.ToUniversalTime()}
            snapshot.Works.AddRange({
                work("10.5555/example.anchoring", "Example: anchoring effects in clinical risk estimates, a preregistered replication", "Fictional Open Psychology", anchoringYear, {3, 4}, 1.8, 0.86),
                work("10.5555/example.habits", "Example: study habits and exam performance across two semesters", "Fictional Journal of Research Methods", year - 6, {4, 7, 9, 8, 6, 5, 3}, 1.4, 0.81),
                work("10.5555/example.measurement", "Example: a short measure of study planning", "Fictional Assessment Quarterly", year - 5, {3, 5, 6, 4, 2, 1}, 1.1, 0.72),
                work("10.5555/example.replication", "Example: a registered replication of the testing effect", "Fictional Psychological Letters", year - 4, {2, 3, 3, 2, 1}, 0.9, 0.61),
                work("10.5555/example.review", "Example: feedback timing in learning, a narrative review", "Fictional Journal of Research Methods", year - 8, {1, 2, 2, 3, 4, 5, 6, 4, 2}, 1.2, 0.77),
                work("10.5555/example.commentary", "Example: a commentary on open materials", "Fictional Psychological Letters", year - 3, {0, 1, 0, 1}, Nothing, Nothing)
            })
            Return snapshot

        End Function


        Private Shared Function Paper(title As String, stage As PaperStage, location As ManuscriptLocation, journal As JournalRecord,
                                      stageEntered As DateTime, ParamArray authors As AuthorRecord()) As Manuscript
            Dim manuscript As New Manuscript With {
                .Title = title,
                .CurrentStage = stage,
                .Location = location,
                .StageEnteredDate = stageEntered,
                .TargetJournal = If(journal?.Name, String.Empty),
                .TargetJournalId = If(journal Is Nothing, CType(Nothing, Guid?), journal.Id)
            }
            For index As Integer = 0 To authors.Length - 1
                manuscript.Authors.Add(New ManuscriptAuthor With {.AuthorId = authors(index).Id, .IsCorrespondingAuthor = index = 0})
            Next
            manuscript.History.Add(New HistoryEvent With {.Stage = PaperStage.Idea, .EventDate = stageEntered.AddDays(-30), .Note = "Fictional example."})
            If stage <> PaperStage.Idea Then manuscript.History.Add(New HistoryEvent With {.Stage = stage, .EventDate = stageEntered, .Note = "Fictional example."})
            Return manuscript
        End Function


        Private Shared Function Submitted(journal As JournalRecord, submittedOn As DateTime,
                                          ParamArray decisions As (Decision As EditorialDecision, DecisionDate As DateTime, Deadline As DateTime?)()) As JournalSubmission
            Dim submission As New JournalSubmission With {
                .JournalName = journal.Name,
                .JournalId = journal.Id,
                .SubmittedDate = submittedOn,
                .ManuscriptNumber = "EX-" & submittedOn.ToString("yyMMdd", Globalization.CultureInfo.InvariantCulture)
            }
            For Each decision In decisions
                submission.Decisions.Add(New EditorialDecisionEvent With {.Decision = decision.Decision, .DecisionDate = decision.DecisionDate, .RevisionDeadline = decision.Deadline})
            Next
            Return submission
        End Function


        Private Shared Function Response(decision As EditorialDecisionEvent, reviewer As String, status As ReviewerResponseStatus,
                                         comment As String, answer As String, location As String) As ReviewerResponseItem
            Return New ReviewerResponseItem With {
                .DecisionId = decision.Id,
                .RevisionRoundNumber = 1,
                .ReviewerLabel = reviewer,
                .Status = status,
                .CommentText = comment,
                .ResponseText = answer,
                .ManuscriptLocation = location
            }
        End Function

    End Class

End Namespace
