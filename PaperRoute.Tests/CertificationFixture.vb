Imports System
Imports System.Collections.Generic
Imports System.Globalization
Imports System.IO
Imports System.Linq
Imports System.Security.Cryptography
Imports System.Text
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

' The standing certification fixture (#145): one small library, richer than
' any single defect needs, that release certification carries through its
' update and backup gates. Four ZZZ-CERT- manuscripts with journal
' shortlists, journal facts, citations.json, AI-suggestion provenance,
' packets with managed and linked files, reviewer responses and versions,
' as a 0.9.x (Schema 9) library on disk. Everything is fictional. Ids, dates
' and file contents are fixed, so the fixture written to the same folder is
' the same, byte for byte, every time. The responsiveness budgets' library
' (LargeLibraryFixture) is the large case; the two could share a generator.
Friend NotInheritable Class CertificationFixture

    Private Sub New()
    End Sub


    ' Every manuscript's title starts with this (ManualCertification's naming
    ' rule), so the fixture collector names them, and nothing else.
    Public Const TitlePrefix As String = "ZZZ-CERT-"

    ' Every 0.9 release writes Schema 9. The fixture stays there when a later
    ' build moves on, so its chained test then takes that step too.
    Public Const SchemaVersion As Integer = 9

    ' When the fixture's records were made, looked up and suggested.
    Private Shared ReadOnly RecordedUtc As New DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc)

    Private Const MethodsJournal As String = "Fictional Journal of Certification Methods"
    Private Const RouteJournal As String = "Fictional Review of Route Studies"
    Private Const LettersJournal As String = "Fictional Letters in Certification"

    Private Shared ReadOnly MethodsJournalId As Guid = Id(101)
    Private Shared ReadOnly RouteJournalId As Guid = Id(102)
    Private Shared ReadOnly LettersJournalId As Guid = Id(103)
    Private Shared ReadOnly CoverLetterItemId As Guid = Id(111)
    Private Shared ReadOnly DataStatementItemId As Guid = Id(112)
    Private Shared ReadOnly MeId As Guid = Id(121)
    Private Shared ReadOnly CoauthorId As Guid = Id(122)
    Private Shared ReadOnly UniversityId As Guid = Id(131)

    ' ORCID's own fictional test record.
    Private Const FictionalOrcid As String = "0000-0002-1825-0097"


    ' The fixture on disk, laid out as PaperRoute lays out a library, with
    ' what was written there.
    Friend NotInheritable Class CertificationLibrary

        ' Stands for %LocalAppData%\PaperRoute, with data\ inside.
        Public Property DataRoot As String = String.Empty
        Public Property DataDirectory As String = String.Empty

        ' Stands for Documents\PaperRoute Library.
        Public Property ManagedDirectory As String = String.Empty

        ' Files the library links to without copying them.
        Public Property LinkedDirectory As String = String.Empty

        Public Property Manuscripts As New List(Of Manuscript)()
        Public Property Library As New AuthorLibraryData()
        Public Property Citations As New CitationSnapshot()

    End Class


    ' The fixture's id number n: 92000000-0000-0000-0000-00000000000n.
    Public Shared Function Id(
        n As Integer
    ) As Guid

        Return New Guid("92000000-0000-0000-0000-" & n.ToString("000000000000", CultureInfo.InvariantCulture))

    End Function


    ' Writes the fixture under root: PaperRoute (the data folder), PaperRoute
    ' Library (the managed copies) and Linked files. Only into a new or empty
    ' folder, and never in or around PaperRoute's own folders.
    Public Shared Function Write(
        root As String
    ) As CertificationLibrary

        Dim folder As String = Path.GetFullPath(root)

        If OverlapsPaperRoutesFolders(folder) Then
            Throw New InvalidOperationException("The certification fixture is never written in or around PaperRoute's own folders: " & folder)
        End If

        If Directory.Exists(folder) AndAlso Directory.EnumerateFileSystemEntries(folder).Any() Then
            Throw New InvalidOperationException("The certification fixture is written only to a new or empty folder: " & folder)
        End If

        Dim fixture As New CertificationLibrary With {
            .DataRoot = Path.Combine(folder, ProductInfo.DataFolderName),
            .ManagedDirectory = Path.Combine(folder, ProductInfo.ManagedLibraryFolderName),
            .LinkedDirectory = Path.Combine(folder, "Linked files")
        }

        fixture.DataDirectory = Path.Combine(fixture.DataRoot, "data")
        Directory.CreateDirectory(fixture.DataDirectory)

        fixture.Library = CreateLibrary()
        fixture.Manuscripts.Add(CreateRevision(fixture))
        fixture.Manuscripts.Add(CreateRerouted(fixture))
        fixture.Manuscripts.Add(CreatePublished(fixture))
        fixture.Manuscripts.Add(CreateDraft(fixture))
        fixture.Citations = CreateCitations()

        ' Written by PaperRoute's own stores, as a 0.9.x library is: the
        ' models are unchanged since 0.9.1, and every calendar date here is a
        ' plain date, which 0.9.x also wrote without an offset.
        Call New AuthorLibraryRepository(fixture.DataDirectory).Save(fixture.Library)
        Call New ManuscriptRepository(fixture.DataDirectory, fixture.ManagedDirectory).Save(fixture.Manuscripts)
        Call New CitationStore(fixture.DataDirectory).Save(fixture.Citations)

        ' The marker a 0.9 release writes, at a fixed time.
        File.WriteAllText(
            StorageMigrationService.SchemaFilePath(fixture.DataRoot),
            "{" & vbCrLf &
            "  ""SchemaVersion"": " & SchemaVersion.ToString(CultureInfo.InvariantCulture) & "," & vbCrLf &
            "  ""UpdatedAtUtc"": ""2026-10-01T12:00:00.0000000Z""" & vbCrLf &
            "}")

        Return fixture

    End Function


    ' True when the folder is one of PaperRoute's own, current, development
    ' or legacy, is inside one, or holds one: written there, the fixture's
    ' data folder would be the user's library. Paths only.
    Friend Shared Function OverlapsPaperRoutesFolders(
        folder As String
    ) As Boolean

        Dim target As String = Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder))

        Dim names As String() = {
            ProductInfo.DataFolderName,
            ProductInfo.ManagedLibraryFolderName,
            ProductInfo.DevelopmentDataFolderName,
            ProductInfo.DevelopmentManagedLibraryFolderName,
            ProductInfo.LegacyDataFolderName,
            ProductInfo.LegacyManagedLibraryFolderName,
            ProductInfo.DevelopmentLegacyDataFolderName,
            ProductInfo.DevelopmentLegacyManagedLibraryFolderName
        }

        For Each parent As Environment.SpecialFolder In {Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolder.MyDocuments}

            For Each name As String In names

                Dim own As String = Path.Combine(Environment.GetFolderPath(parent), name)

                If IsSameOrInside(target, own) OrElse IsSameOrInside(own, target) Then
                    Return True
                End If

            Next

        Next

        Return False

    End Function


    Private Shared Function IsSameOrInside(
        inner As String,
        outer As String
    ) As Boolean

        Return String.Equals(inner, outer, StringComparison.OrdinalIgnoreCase) OrElse
               inner.StartsWith(outer & Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)

    End Function


    ' Three fictional journals: two with facts from the open indexes and
    ' facts entered by hand, one of those with a checklist whose second
    ' requirement began as an AI suggestion. Two authors, one affiliation.
    Private Shared Function CreateLibrary() As AuthorLibraryData

        Dim library As New AuthorLibraryData()

        library.Affiliations.Add(
            New AffiliationRecord With {
                .Id = UniversityId,
                .Institution = "Fictional University of Certification",
                .Department = "Department of Synthetic Studies",
                .City = "Exampleton",
                .Country = "Nowhere",
                .Notes = "Fictional."
            }
        )

        library.Authors.Add(
            New AuthorRecord With {
                .Id = MeId,
                .GivenName = "Cert",
                .FamilyName = "Author One",
                .Orcid = FictionalOrcid,
                .OrcidLastCheckedUtc = RecordedUtc,
                .Notes = "Fictional.",
                .IsMe = True
            }
        )

        library.Authors.Add(
            New AuthorRecord With {
                .Id = CoauthorId,
                .GivenName = "Cert",
                .FamilyName = "Author Two",
                .Notes = "Fictional."
            }
        )

        Dim methods As New JournalRecord With {
            .Id = MethodsJournalId,
            .Name = MethodsJournal,
            .Publisher = "Fictional Press",
            .HomepageUrl = "https://example.org/certification-methods",
            .SubmissionPortalUrl = "https://example.org/certification-methods/submit",
            .AimsScopeUrl = "https://example.org/certification-methods/scope",
            .AuthorInstructionsUrl = "https://example.org/certification-methods/instructions",
            .EditorialBoardUrl = "https://example.org/certification-methods/board",
            .Notes = "Fictional journal.",
            .IsFavorite = True,
            .Issns = New List(Of String) From {"0000-0019"},
            .OpenAlexId = "S0000000101",
            .DoajId = "00000000000000000000000000000101"
        }

        methods.FieldSources(JournalFactsService.PublisherField) =
            New FieldSource With {.Source = JournalFactCatalog.DoajSource, .Value = "Fictional Press", .RetrievedUtc = RecordedUtc}

        methods.Facts.Add(New JournalFact With {.Key = JournalFactCatalog.Apc, .Value = "USD 1,200", .Url = "https://example.org/certification-methods/fees", .Source = JournalFactCatalog.DoajSource, .SourceUpdatedUtc = RecordedUtc.AddDays(-30), .CheckedUtc = RecordedUtc})
        methods.Facts.Add(New JournalFact With {.Key = JournalFactCatalog.License, .Value = "CC BY 4.0", .Source = JournalFactCatalog.DoajSource, .CheckedUtc = RecordedUtc})
        methods.Facts.Add(New JournalFact With {.Key = JournalFactCatalog.MeanCitedness, .Value = "2.4", .Source = JournalFactCatalog.OpenAlexSource, .CheckedUtc = RecordedUtc})
        methods.Facts.Add(New JournalFact With {.Key = JournalFactCatalog.CiteScore, .Value = "3.1", .Source = "Scopus (Elsevier)", .Year = 2025, .CheckedUtc = RecordedUtc, .EnteredByYou = True})
        methods.Facts.Add(New JournalFact With {.Key = JournalFactCatalog.Other, .Label = "Median days to first decision", .Value = "41", .Source = "The journal's website", .Year = 2026, .CheckedUtc = RecordedUtc, .EnteredByYou = True})

        methods.ReadinessChecklistTemplate.Add(
            New JournalChecklistTemplateItem With {
                .Id = CoverLetterItemId,
                .Title = "Cover letter",
                .Category = "Submission files",
                .SortOrder = 1
            }
        )

        methods.ReadinessChecklistTemplate.Add(
            New JournalChecklistTemplateItem With {
                .Id = DataStatementItemId,
                .Title = "Data availability statement",
                .Description = "Say where the data can be found.",
                .Category = "Statements",
                .SortOrder = 2,
                .Suggestion = Suggested("author-instructions", "Fictional instructions: every article includes a data availability statement.")
            }
        )

        library.Journals.Add(methods)

        Dim route As New JournalRecord With {
            .Id = RouteJournalId,
            .Name = RouteJournal,
            .Publisher = "Fictional Open Publishing",
            .HomepageUrl = "https://example.org/route-studies",
            .Notes = "Fictional journal.",
            .IsShortlisted = True,
            .Issns = New List(Of String) From {"0000-0027"},
            .OpenAlexId = "S0000000102"
        }

        route.Facts.Add(New JournalFact With {.Key = JournalFactCatalog.DoajListing, .Value = "Listed", .Source = JournalFactCatalog.DoajSource, .CheckedUtc = RecordedUtc})
        route.Facts.Add(New JournalFact With {.Key = JournalFactCatalog.OpenAccess, .Value = "Yes", .Source = JournalFactCatalog.DoajSource, .CheckedUtc = RecordedUtc})
        route.Facts.Add(New JournalFact With {.Key = JournalFactCatalog.OpenAlexHIndex, .Value = "18", .Source = JournalFactCatalog.OpenAlexSource, .CheckedUtc = RecordedUtc})

        library.Journals.Add(route)

        library.Journals.Add(
            New JournalRecord With {
                .Id = LettersJournalId,
                .Name = LettersJournal,
                .Publisher = "Fictional Press",
                .Notes = "Fictional journal.",
                .Issns = New List(Of String) From {"0000-0035"}
            }
        )

        library.TagColors.Add(New TagColor With {.Tag = "certification", .Color = TagPalette.Teal})
        library.TagColors.Add(New TagColor With {.Tag = "replication", .Color = TagPalette.Violet})

        Return library

    End Function


    ' 01: a major revision under way. Responses to the reviewers, some begun
    ' as AI suggestions; the original submission's packet with managed and
    ' linked files; the revision draft kept as a linked version.
    Private Shared Function CreateRevision(
        fixture As CertificationLibrary
    ) As Manuscript

        Dim manuscript As New Manuscript With {
            .Id = Id(1),
            .Title = TitlePrefix & "v0.9.2-01 Major revision with responses",
            .CoAuthors = "Cert Author One; Cert Author Two",
            .TargetJournal = MethodsJournal,
            .TargetJournalId = MethodsJournalId,
            .ManuscriptUrl = "https://example.org/zzz-cert/01",
            .CurrentStage = PaperStage.Revision,
            .Location = ManuscriptLocation.Pipeline,
            .StageEnteredDate = New DateTime(2026, 9, 10),
            .RevisionDeadline = New DateTime(2026, 11, 30),
            .WorkType = WorkType.JournalArticle,
            .Tags = New List(Of String) From {"certification", "replication"}
        }

        manuscript.Authors.Add(New ManuscriptAuthor With {.AuthorId = MeId, .AffiliationIds = New List(Of Guid) From {UniversityId}, .IsCorrespondingAuthor = True})
        manuscript.Authors.Add(New ManuscriptAuthor With {.AuthorId = CoauthorId, .AffiliationIds = New List(Of Guid) From {UniversityId}})

        manuscript.Metadata.AbstractText = "A fictional abstract for the certification fixture."
        manuscript.Metadata.Keywords.AddRange({"certification", "synthetic data"})
        manuscript.Metadata.ExternalIdentifiers("osf") = "zzz-cert-01"

        manuscript.RelatedLinks.Add(New ManuscriptExternalLink With {.Id = Id(1001), .Label = "Preregistration", .Url = "https://example.org/zzz-cert/01/preregistration", .Notes = "Fictional."})
        manuscript.Reminders.Add(New ManuscriptReminder With {.Id = Id(1002), .DueDate = New DateTime(2026, 11, 20), .Title = "Send the revision", .Notes = "Fictional."})

        manuscript.History.Add(NewHistory(Id(1003), PaperStage.Draft, New DateTime(2026, 5, 1)))
        manuscript.History.Add(NewHistory(Id(1004), PaperStage.Submitted, New DateTime(2026, 6, 15)))
        manuscript.History.Add(NewHistory(Id(1005), PaperStage.Revision, New DateTime(2026, 9, 10)))

        Dim submission As JournalSubmission =
            NewSubmission(Id(1010), MethodsJournal, MethodsJournalId, "ZZZ-CERT-01-2026", New DateTime(2026, 6, 15))

        Dim decision As EditorialDecisionEvent =
            NewDecision(Id(1011), EditorialDecision.MajorRevision, New DateTime(2026, 9, 10), New DateTime(2026, 11, 30))

        decision.Suggestion = Suggested("decision-letter", "Fictional letter: we invite you to submit a major revision.")
        submission.Decisions.Add(decision)

        AddManagedLetter(fixture, manuscript, submission, Id(1012), CorrespondenceType.DecisionLetter, "Decision letter", decision.DecisionDate, "decision-letter.txt",
                         "ZZZ-CERT fictional decision letter.", "Reviewer 1: Please clarify the sampling plan.", "Reviewer 2: Revise the figure legend.")

        Dim comments As New CorrespondenceItem With {
            .Id = Id(1013),
            .RecordedAtUtc = RecordedUtc,
            .ItemDate = decision.DecisionDate,
            .Type = CorrespondenceType.ReviewerComments,
            .Title = "Reviewer comments, linked",
            .SourceUrl = "https://example.org/certification-methods/reviews/zzz-cert-01"
        }

        comments.LocalFilePath = WriteFile(fixture.LinkedDirectory, "reviewer-comments.txt", Text("ZZZ-CERT fictional reviewer comments, kept outside the library."))
        submission.Correspondence.Add(comments)

        submission.ReviewerResponses.Add(
            New ReviewerResponseItem With {
                .Id = Id(1014),
                .DecisionId = decision.Id,
                .RevisionRoundNumber = 1,
                .ReviewerLabel = "Reviewer 1",
                .CommentText = "Please clarify the sampling plan.",
                .ActionText = "Expand the Method section.",
                .ResponseText = "We now describe the sampling plan in full.",
                .ManuscriptLocation = "Method, p. 4",
                .Notes = "Fictional.",
                .Status = ReviewerResponseStatus.Addressed,
                .CreatedAtUtc = RecordedUtc,
                .LastModifiedAtUtc = RecordedUtc.AddDays(2),
                .CommentSuggestion = Suggested("decision-letter", "Reviewer 1: Please clarify the sampling plan."),
                .ResponseSuggestion = New AssistantSuggestion With {
                    .Feature = "draft-response",
                    .Provider = "OpenAI-compatible server",
                    .Model = "fictional-local-model",
                    .SuggestedUtc = RecordedUtc.AddDays(1),
                    .SourceText = "Please clarify the sampling plan."
                }
            }
        )

        submission.ReviewerResponses.Add(
            New ReviewerResponseItem With {
                .Id = Id(1015),
                .DecisionId = decision.Id,
                .RevisionRoundNumber = 1,
                .ReviewerLabel = "Reviewer 2",
                .CommentText = "Revise the figure legend.",
                .Status = ReviewerResponseStatus.InProgress,
                .CreatedAtUtc = RecordedUtc,
                .CommentSuggestion = Suggested("decision-letter", "Reviewer 2: Revise the figure legend.")
            }
        )

        submission.ReviewerResponses.Add(
            New ReviewerResponseItem With {
                .Id = Id(1016),
                .DecisionId = decision.Id,
                .RevisionRoundNumber = 1,
                .ReviewerLabel = "Editor",
                .CommentText = "Shorten the introduction.",
                .ActionText = "Cut it to 600 words.",
                .Status = ReviewerResponseStatus.Unresolved,
                .CreatedAtUtc = RecordedUtc
            }
        )

        manuscript.Submissions.Add(submission)

        Dim submitted As ManuscriptVersion =
            AddManagedVersion(fixture, manuscript, Id(1020), "Submitted manuscript", New DateTime(2026, 6, 14), submission.Id, "manuscript-v1.txt")

        submitted.RevisionRoundNumber = 1

        Dim draft As New ManuscriptVersion With {
            .Id = Id(1021),
            .RecordedAtUtc = RecordedUtc,
            .CreatedDate = New DateTime(2026, 9, 20),
            .Label = "Revision draft, linked",
            .Notes = "Kept where it is.",
            .SubmissionId = submission.Id,
            .DecisionId = decision.Id,
            .RevisionRoundNumber = 1
        }

        draft.LocalFilePath = WriteFile(fixture.LinkedDirectory, "manuscript-revision-draft.txt", Text("ZZZ-CERT fictional revision draft, kept outside the library."))
        manuscript.Versions.Add(draft)
        manuscript.CurrentVersionId = draft.Id

        Dim readiness As ManuscriptReadiness = NewReadiness(Id(1030), Id(1031), Id(1032))
        readiness.Items(0).Status = ReadinessItemStatus.Complete
        readiness.Items(0).UserNotes = "Sent with the submission."
        readiness.Items(0).CompletedAtUtc = RecordedUtc
        manuscript.ReadinessProfiles.Add(readiness)

        Dim packet As SubmissionPacket = NewPacket(Id(1040), "Original submission", submitted.Id, MethodsJournalId, MethodsJournal)
        packet.ReadinessProfileId = readiness.Id
        packet.SubmissionId = submission.Id
        packet.RevisionRoundNumber = 1

        AddManagedPacketFile(fixture, manuscript, packet, Id(1041), SubmissionPacketFileRole.Manuscript, "Manuscript", "manuscript.txt", Text("ZZZ-CERT fictional manuscript as sent."))
        AddManagedPacketFile(fixture, manuscript, packet, Id(1042), SubmissionPacketFileRole.CoverLetter, "Cover letter", "cover-letter.txt", Text("Dear Fictional Editor,", "Please consider this fictional manuscript."))
        AddManagedPacketFile(fixture, manuscript, packet, Id(1043), SubmissionPacketFileRole.Supplement, "Supplementary data", "supplement.bin", EveryByte())

        Dim figure As New SubmissionPacketFile With {
            .Id = Id(1044),
            .Role = SubmissionPacketFileRole.Figure,
            .Label = "Figure 1, linked",
            .StorageMode = SubmissionPacketFileStorageMode.LinkedExternal,
            .OriginalFileName = "figure-1.txt"
        }

        Attach(figure, WriteFile(fixture.LinkedDirectory, "figure-1.txt", Text("ZZZ-CERT fictional figure source, linked and never copied.")))
        packet.Files.Add(figure)

        packet.Files.Add(
            New SubmissionPacketFile With {
                .Id = Id(1045),
                .Role = SubmissionPacketFileRole.TitlePage,
                .Label = "Title page, entered in the portal",
                .Notes = "Fictional.",
                .StorageMode = SubmissionPacketFileStorageMode.MetadataOnly
            }
        )

        manuscript.SubmissionPackets.Add(packet)

        manuscript.JournalShortlist.Add(
            New JournalCandidate With {
                .Id = Id(1050),
                .JournalName = MethodsJournal,
                .JournalId = MethodsJournalId,
                .Status = CandidateStatus.Preferred,
                .Notes = "Fictional: the best fit.",
                .Checks = New List(Of String) From {"trust.known", "trust.review", "fit.scope"},
                .AddedDate = New DateTime(2026, 5, 20)
            }
        )

        manuscript.JournalShortlist.Add(
            New JournalCandidate With {
                .Id = Id(1051),
                .JournalName = RouteJournal,
                .JournalId = RouteJournalId,
                .Status = CandidateStatus.Backup,
                .Notes = "Fictional: found by keyword search.",
                .AddedDate = New DateTime(2026, 5, 21),
                .Evidence = New CandidateEvidence With {
                    .Source = JournalFactCatalog.OpenAlexSource,
                    .OpenAlexId = "S0000000102",
                    .Issns = New List(Of String) From {"0000-0027"},
                    .Publisher = "Fictional Open Publishing",
                    .Topics = New List(Of String) From {"Route studies", "Synthetic methods"},
                    .HomepageUrl = "https://example.org/route-studies",
                    .Keywords = New List(Of String) From {"certification", "synthetic data"},
                    .MatchAll = True,
                    .SinceDate = New DateTime(2021, 1, 1),
                    .MatchingArticles = 12,
                    .AllArticles = 340,
                    .Examples = New List(Of EvidenceExample) From {
                        New EvidenceExample With {.Title = "A fictional route study", .Year = 2025, .Doi = "10.5555/zzz-cert.example"}
                    },
                    .RetrievedUtc = RecordedUtc
                }
            }
        )

        Return manuscript

    End Function


    ' 02: desk rejected at one journal and under review at the next, with a
    ' packet for the second submission.
    Private Shared Function CreateRerouted(
        fixture As CertificationLibrary
    ) As Manuscript

        Dim manuscript As New Manuscript With {
            .Id = Id(2),
            .Title = TitlePrefix & "v0.9.2-02 Rejected and rerouted",
            .TargetJournal = RouteJournal,
            .TargetJournalId = RouteJournalId,
            .CurrentStage = PaperStage.UnderReview,
            .Location = ManuscriptLocation.Pipeline,
            .StageEnteredDate = New DateTime(2026, 4, 1),
            .WorkType = WorkType.JournalArticle,
            .Tags = New List(Of String) From {"certification"}
        }

        manuscript.Authors.Add(New ManuscriptAuthor With {.AuthorId = MeId, .IsCorrespondingAuthor = True})

        manuscript.History.Add(NewHistory(Id(2003), PaperStage.Draft, New DateTime(2026, 1, 15)))
        manuscript.History.Add(NewHistory(Id(2004), PaperStage.Submitted, New DateTime(2026, 3, 1)))
        manuscript.History.Add(NewHistory(Id(2005), PaperStage.UnderReview, New DateTime(2026, 4, 1)))

        Dim first As JournalSubmission =
            NewSubmission(Id(2010), LettersJournal, LettersJournalId, "ZZZ-CERT-02-A", New DateTime(2026, 3, 1))

        first.Decisions.Add(NewDecision(Id(2011), EditorialDecision.DeskRejected, New DateTime(2026, 3, 8), Nothing))

        AddManagedLetter(fixture, manuscript, first, Id(2012), CorrespondenceType.DecisionLetter, "Desk rejection", New DateTime(2026, 3, 8), "decision-letter.txt",
                         "ZZZ-CERT fictional desk rejection: outside the journal's scope.")

        manuscript.Submissions.Add(first)

        Dim second As JournalSubmission =
            NewSubmission(Id(2013), RouteJournal, RouteJournalId, "ZZZ-CERT-02-B", New DateTime(2026, 4, 1))

        second.FollowUpDate = New DateTime(2026, 10, 20)
        manuscript.Submissions.Add(second)

        AddManagedVersion(fixture, manuscript, Id(2020), "Sent to " & LettersJournal, New DateTime(2026, 2, 28), first.Id, "manuscript-v1.txt")

        Dim resent As ManuscriptVersion =
            AddManagedVersion(fixture, manuscript, Id(2021), "Sent to " & RouteJournal, New DateTime(2026, 3, 31), second.Id, "manuscript-v2.txt")

        manuscript.CurrentVersionId = resent.Id

        Dim packet As SubmissionPacket = NewPacket(Id(2040), "Second submission", resent.Id, RouteJournalId, RouteJournal)
        packet.SubmissionId = second.Id

        AddManagedPacketFile(fixture, manuscript, packet, Id(2041), SubmissionPacketFileRole.BlindedManuscript, "Blinded manuscript", "manuscript-blinded.txt", Text("ZZZ-CERT fictional blinded manuscript."))
        AddManagedPacketFile(fixture, manuscript, packet, Id(2042), SubmissionPacketFileRole.ReportingChecklist, "Reporting checklist", "reporting-checklist.txt", Text("ZZZ-CERT fictional reporting checklist."))

        manuscript.SubmissionPackets.Add(packet)

        manuscript.JournalShortlist.Add(
            New JournalCandidate With {
                .Id = Id(2050),
                .JournalName = LettersJournal,
                .JournalId = LettersJournalId,
                .Status = CandidateStatus.RuledOut,
                .Notes = "Fictional: desk rejected.",
                .AddedDate = New DateTime(2026, 2, 1)
            }
        )

        manuscript.JournalShortlist.Add(
            New JournalCandidate With {
                .Id = Id(2051),
                .JournalName = RouteJournal,
                .JournalId = RouteJournalId,
                .Status = CandidateStatus.Preferred,
                .Checks = New List(Of String) From {"fit.scope", "fit.audience"},
                .AddedDate = New DateTime(2026, 3, 10)
            }
        )

        Return manuscript

    End Function


    ' 03: published after a minor revision, with the confirmed publication
    ' match and the work's citations in citations.json.
    Private Shared Function CreatePublished(
        fixture As CertificationLibrary
    ) As Manuscript

        Dim manuscript As New Manuscript With {
            .Id = Id(3),
            .Title = TitlePrefix & "v0.9.2-03 Published",
            .TargetJournal = MethodsJournal,
            .TargetJournalId = MethodsJournalId,
            .CurrentStage = PaperStage.Published,
            .Location = ManuscriptLocation.Published,
            .StageEnteredDate = New DateTime(2026, 2, 1),
            .WorkType = WorkType.JournalArticle,
            .Tags = New List(Of String) From {"certification", "open science"}
        }

        manuscript.Authors.Add(New ManuscriptAuthor With {.AuthorId = CoauthorId, .AffiliationIds = New List(Of Guid) From {UniversityId}, .IsCorrespondingAuthor = True})
        manuscript.Authors.Add(New ManuscriptAuthor With {.AuthorId = MeId})

        With manuscript.Metadata
            .AbstractText = "A fictional published abstract."
            .Doi = "10.5555/zzz-cert.03"
            .PublicationJournal = MethodsJournal
            .PublishedDate = New DateTime(2026, 2, 1)
            .Volume = "12"
            .Issue = "3"
            .Pages = "101-118"
            .Publisher = "Fictional Press"
            .PublicationUrl = "https://example.org/certification-methods/12/3/101"
            .PreprintDoi = "10.5555/zzz-cert.03.preprint"
            .PreprintUrl = "https://example.org/preprints/zzz-cert-03"
        End With

        manuscript.History.Add(NewHistory(Id(3003), PaperStage.Submitted, New DateTime(2025, 9, 1)))
        manuscript.History.Add(NewHistory(Id(3004), PaperStage.Accepted, New DateTime(2026, 1, 10)))
        manuscript.History.Add(NewHistory(Id(3005), PaperStage.Published, New DateTime(2026, 2, 1)))

        Dim submission As JournalSubmission =
            NewSubmission(Id(3010), MethodsJournal, MethodsJournalId, "ZZZ-CERT-03-2025", New DateTime(2025, 9, 1))

        Dim minor As EditorialDecisionEvent =
            NewDecision(Id(3011), EditorialDecision.MinorRevision, New DateTime(2025, 11, 1), New DateTime(2025, 12, 1))

        Dim accepted As EditorialDecisionEvent =
            NewDecision(Id(3012), EditorialDecision.Accepted, New DateTime(2026, 1, 10), Nothing)

        submission.Decisions.Add(minor)
        submission.Decisions.Add(accepted)

        submission.ReviewerResponses.Add(
            New ReviewerResponseItem With {
                .Id = Id(3013),
                .DecisionId = minor.Id,
                .RevisionRoundNumber = 1,
                .ReviewerLabel = "Reviewer 1",
                .CommentText = "Report the effect sizes.",
                .ResponseText = "Added to Table 2.",
                .ManuscriptLocation = "Results, Table 2",
                .Status = ReviewerResponseStatus.Addressed,
                .CreatedAtUtc = RecordedUtc.AddDays(-300),
                .LastModifiedAtUtc = RecordedUtc.AddDays(-290)
            }
        )

        AddManagedLetter(fixture, manuscript, submission, Id(3014), CorrespondenceType.AcceptanceLetter, "Acceptance letter", accepted.DecisionDate, "acceptance-letter.txt",
                         "ZZZ-CERT fictional acceptance letter.")

        manuscript.Submissions.Add(submission)

        Dim acceptedVersion As ManuscriptVersion =
            AddManagedVersion(fixture, manuscript, Id(3020), "Accepted manuscript", New DateTime(2026, 1, 9), submission.Id, "manuscript-accepted.txt")

        acceptedVersion.DecisionId = minor.Id
        acceptedVersion.RevisionRoundNumber = 1
        manuscript.CurrentVersionId = acceptedVersion.Id

        manuscript.PublicationMatches.Add(
            New PublicationMatch With {
                .Id = Id(3030),
                .Doi = "10.5555/zzz-cert.03",
                .Title = manuscript.Title,
                .Journal = MethodsJournal,
                .PublishedDate = New DateTime(2026, 2, 1),
                .Url = "https://doi.org/10.5555/zzz-cert.03",
                .Publisher = "Fictional Press",
                .Volume = "12",
                .Issue = "3",
                .Pages = "101-118",
                .Source = PublicationMatchSource.Doi,
                .Status = PublicationMatchStatus.Confirmed,
                .FoundAtUtc = RecordedUtc.AddDays(-240),
                .ReviewedAtUtc = RecordedUtc.AddDays(-239)
            }
        )

        Return manuscript

    End Function


    ' 04: a draft with a shortlist and a packet made before any submission,
    ' for a journal whose checklist began partly as an AI suggestion.
    Private Shared Function CreateDraft(
        fixture As CertificationLibrary
    ) As Manuscript

        Dim manuscript As New Manuscript With {
            .Id = Id(4),
            .Title = TitlePrefix & "v0.9.2-04 Draft with a shortlist",
            .TargetJournal = MethodsJournal,
            .TargetJournalId = MethodsJournalId,
            .CurrentStage = PaperStage.Draft,
            .Location = ManuscriptLocation.Pipeline,
            .StageEnteredDate = New DateTime(2026, 8, 1),
            .WorkType = WorkType.ConferencePaper,
            .Tags = New List(Of String) From {"certification"}
        }

        manuscript.Authors.Add(New ManuscriptAuthor With {.AuthorId = MeId, .IsCorrespondingAuthor = True})
        manuscript.Authors.Add(New ManuscriptAuthor With {.AuthorId = CoauthorId})

        manuscript.Reminders.Add(New ManuscriptReminder With {.Id = Id(4002), .DueDate = New DateTime(2026, 9, 25), .Title = "Ask the coauthors for comments", .IsCompleted = True, .CompletedDate = New DateTime(2026, 9, 24)})

        manuscript.History.Add(NewHistory(Id(4003), PaperStage.Idea, New DateTime(2026, 7, 1)))
        manuscript.History.Add(NewHistory(Id(4004), PaperStage.Draft, New DateTime(2026, 8, 1)))

        Dim draft As ManuscriptVersion =
            AddManagedVersion(fixture, manuscript, Id(4020), "Draft 1", New DateTime(2026, 8, 15), Nothing, "draft-1.txt")

        manuscript.CurrentVersionId = draft.Id

        Dim readiness As ManuscriptReadiness = NewReadiness(Id(4030), Id(4031), Id(4032))
        manuscript.ReadinessProfiles.Add(readiness)

        Dim packet As SubmissionPacket = NewPacket(Id(4040), "Prepared for " & MethodsJournal, draft.Id, MethodsJournalId, MethodsJournal)
        packet.ReadinessProfileId = readiness.Id

        AddManagedPacketFile(fixture, manuscript, packet, Id(4041), SubmissionPacketFileRole.Manuscript, "Manuscript", "draft-1.txt", Text("ZZZ-CERT fictional draft, copied into the packet."))

        packet.Files.Add(
            New SubmissionPacketFile With {
                .Id = Id(4042),
                .Role = SubmissionPacketFileRole.DataAvailability,
                .Label = "Data availability statement, still to write",
                .StorageMode = SubmissionPacketFileStorageMode.MetadataOnly
            }
        )

        manuscript.SubmissionPackets.Add(packet)

        manuscript.JournalShortlist.Add(New JournalCandidate With {.Id = Id(4050), .JournalName = MethodsJournal, .JournalId = MethodsJournalId, .AddedDate = New DateTime(2026, 8, 2)})
        manuscript.JournalShortlist.Add(New JournalCandidate With {.Id = Id(4051), .JournalName = LettersJournal, .JournalId = LettersJournalId, .Notes = "Fictional: shorter papers.", .AddedDate = New DateTime(2026, 8, 3)})

        Return manuscript

    End Function


    ' The researcher's citations as OpenAlex counted them (#91): the
    ' published manuscript and one earlier article.
    Private Shared Function CreateCitations() As CitationSnapshot

        Dim snapshot As New CitationSnapshot With {
            .Orcid = FictionalOrcid,
            .Source = JournalFactCatalog.OpenAlexSource,
            .RetrievedUtc = RecordedUtc,
            .IncludeOpenAlexLinked = True,
            .Excluded = New List(Of String) From {"doi:10.5555/zzz-cert.not-mine"}
        }

        snapshot.Works.Add(
            New CitedWork With {
                .OpenAlexId = "W0000000301",
                .Doi = "10.5555/zzz-cert.03",
                .VersionDois = New List(Of String) From {"10.5555/zzz-cert.03.preprint"},
                .Title = TitlePrefix & "v0.9.2-03 Published",
                .Year = 2026,
                .Journal = MethodsJournal,
                .CitedByCount = 7,
                .CountsByYear = New List(Of YearCount) From {New YearCount With {.Year = 2026, .Count = 7}},
                .Fwci = 1.25,
                .Percentile = 0.71,
                .FoundBy = "paperroute"
            }
        )

        snapshot.Works.Add(
            New CitedWork With {
                .OpenAlexId = "W0000000302",
                .Doi = "10.5555/zzz-cert.earlier",
                .Title = "A fictional earlier article",
                .Year = 2024,
                .Journal = LettersJournal,
                .CitedByCount = 15,
                .CountsByYear = New List(Of YearCount) From {
                    New YearCount With {.Year = 2024, .Count = 3},
                    New YearCount With {.Year = 2025, .Count = 9},
                    New YearCount With {.Year = 2026, .Count = 3}
                },
                .Fwci = 2.5,
                .Percentile = 0.93,
                .InTop10Percent = True,
                .FoundBy = "orcid"
            }
        )

        Return snapshot

    End Function


    ' Where an accepted AI suggestion came from (#84, #95).
    Private Shared Function Suggested(
        feature As String,
        sourceText As String
    ) As AssistantSuggestion

        Return New AssistantSuggestion With {
            .Feature = feature,
            .Provider = "Claude",
            .Model = "claude-opus-5-5",
            .SuggestedUtc = RecordedUtc,
            .SourceText = sourceText
        }

    End Function


    Private Shared Function NewHistory(
        eventId As Guid,
        stage As PaperStage,
        eventDate As DateTime
    ) As HistoryEvent

        Return New HistoryEvent With {
            .Id = eventId,
            .RecordedAtUtc = RecordedUtc,
            .EventDate = eventDate,
            .Stage = stage,
            .Note = "Fictional."
        }

    End Function


    Private Shared Function NewSubmission(
        submissionId As Guid,
        journalName As String,
        journalId As Guid,
        manuscriptNumber As String,
        submittedDate As DateTime
    ) As JournalSubmission

        Return New JournalSubmission With {
            .Id = submissionId,
            .RecordedAtUtc = RecordedUtc,
            .JournalName = journalName,
            .JournalId = journalId,
            .ManuscriptNumber = manuscriptNumber,
            .SubmittedDate = submittedDate,
            .Notes = "Fictional submission.",
            .PortalUrl = "https://example.org/portal"
        }

    End Function


    Private Shared Function NewDecision(
        decisionId As Guid,
        decision As EditorialDecision,
        decisionDate As DateTime,
        revisionDeadline As DateTime?
    ) As EditorialDecisionEvent

        Return New EditorialDecisionEvent With {
            .Id = decisionId,
            .RecordedAtUtc = RecordedUtc,
            .DecisionDate = decisionDate,
            .Decision = decision,
            .RevisionDeadline = revisionDeadline,
            .Notes = "Fictional decision."
        }

    End Function


    ' Readiness for the methods journal, from its checklist: the cover
    ' letter, and the data statement that began as an AI suggestion.
    Private Shared Function NewReadiness(
        readinessId As Guid,
        coverLetterId As Guid,
        dataStatementId As Guid
    ) As ManuscriptReadiness

        Dim readiness As New ManuscriptReadiness With {
            .Id = readinessId,
            .JournalId = MethodsJournalId,
            .JournalName = MethodsJournal,
            .Notes = "Fictional.",
            .CreatedAtUtc = RecordedUtc
        }

        readiness.Items.Add(
            New ReadinessItemState With {
                .Id = coverLetterId,
                .TemplateItemId = CoverLetterItemId,
                .Title = "Cover letter",
                .Category = "Submission files",
                .SortOrder = 1
            }
        )

        readiness.Items.Add(
            New ReadinessItemState With {
                .Id = dataStatementId,
                .TemplateItemId = DataStatementItemId,
                .Title = "Data availability statement",
                .Description = "Say where the data can be found.",
                .Category = "Statements",
                .SortOrder = 2,
                .Suggestion = Suggested("author-instructions", "Fictional instructions: every article includes a data availability statement.")
            }
        )

        Return readiness

    End Function


    Private Shared Function NewPacket(
        packetId As Guid,
        label As String,
        versionId As Guid,
        journalId As Guid,
        journalName As String
    ) As SubmissionPacket

        Return New SubmissionPacket With {
            .Id = packetId,
            .JournalId = journalId,
            .JournalName = journalName,
            .ManuscriptVersionId = versionId,
            .Label = label,
            .Notes = "Fictional.",
            .CreatedAtUtc = RecordedUtc,
            .LastModifiedAtUtc = RecordedUtc
        }

    End Function


    ' A managed version, its copy where PaperRoute keeps it:
    ' <manuscript>\versions\<version>.
    Private Shared Function AddManagedVersion(
        fixture As CertificationLibrary,
        manuscript As Manuscript,
        versionId As Guid,
        label As String,
        createdDate As DateTime,
        submissionId As Guid?,
        fileName As String
    ) As ManuscriptVersion

        Dim version As New ManuscriptVersion With {
            .Id = versionId,
            .RecordedAtUtc = RecordedUtc,
            .CreatedDate = createdDate,
            .Label = label,
            .Notes = "Fictional.",
            .IsManagedCopy = True,
            .SubmissionId = submissionId
        }

        version.LocalFilePath =
            WriteFile(
                Path.Combine(fixture.ManagedDirectory, manuscript.Id.ToString("N"), "versions", versionId.ToString("N")),
                fileName,
                Text("ZZZ-CERT fictional manuscript: " & label & "."))

        manuscript.Versions.Add(version)
        Return version

    End Function


    ' A managed packet file, its copy where PaperRoute keeps it:
    ' <manuscript>\packets\<packet>\<file>.
    Private Shared Sub AddManagedPacketFile(
        fixture As CertificationLibrary,
        manuscript As Manuscript,
        packet As SubmissionPacket,
        fileId As Guid,
        role As SubmissionPacketFileRole,
        label As String,
        fileName As String,
        content As Byte()
    )

        Dim packetFile As New SubmissionPacketFile With {
            .Id = fileId,
            .Role = role,
            .Label = label,
            .StorageMode = SubmissionPacketFileStorageMode.ManagedCopy,
            .OriginalFileName = fileName
        }

        Attach(
            packetFile,
            WriteFile(
                Path.Combine(fixture.ManagedDirectory, manuscript.Id.ToString("N"), "packets", packet.Id.ToString("N"), fileId.ToString("N")),
                fileName,
                content))

        packet.Files.Add(packetFile)

    End Sub


    ' A managed letter, its copy where PaperRoute keeps it:
    ' <manuscript>\<submission>\<item>.
    Private Shared Sub AddManagedLetter(
        fixture As CertificationLibrary,
        manuscript As Manuscript,
        submission As JournalSubmission,
        itemId As Guid,
        itemType As CorrespondenceType,
        title As String,
        itemDate As DateTime,
        fileName As String,
        ParamArray lines As String()
    )

        Dim item As New CorrespondenceItem With {
            .Id = itemId,
            .RecordedAtUtc = RecordedUtc,
            .ItemDate = itemDate,
            .Type = itemType,
            .Title = title,
            .IsManagedCopy = True
        }

        item.LocalFilePath =
            WriteFile(
                Path.Combine(fixture.ManagedDirectory, manuscript.Id.ToString("N"), submission.Id.ToString("N"), itemId.ToString("N")),
                fileName,
                Text(lines))

        submission.Correspondence.Add(item)

    End Sub


    ' The integrity baseline a packet file carries for its copy on disk.
    Private Shared Sub Attach(
        packetFile As SubmissionPacketFile,
        filePath As String
    )

        Dim content As Byte() = File.ReadAllBytes(filePath)

        packetFile.LocalFilePath = filePath
        packetFile.Sha256 = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant()
        packetFile.FileSizeBytes = content.LongLength
        packetFile.LastWriteTimeUtc = RecordedUtc
        packetFile.HashComputedAtUtc = RecordedUtc

    End Sub


    ' Writes a file in place, where a save leaves it alone, at a fixed time.
    Private Shared Function WriteFile(
        folder As String,
        fileName As String,
        content As Byte()
    ) As String

        Directory.CreateDirectory(folder)

        Dim filePath As String = Path.Combine(folder, fileName)

        File.WriteAllBytes(filePath, content)
        File.SetLastWriteTimeUtc(filePath, RecordedUtc)

        Return filePath

    End Function


    ' A fictional text file: UTF-8, with Windows line ends.
    Private Shared Function Text(
        ParamArray lines As String()
    ) As Byte()

        Return New UTF8Encoding(False).GetBytes(String.Join(vbCrLf, lines) & vbCrLf)

    End Function


    ' Every byte value once, so a file comes through byte for byte and not
    ' only as text.
    Private Shared Function EveryByte() As Byte()

        Return Enumerable.Range(0, 256).Select(Function(value) CByte(value)).ToArray()

    End Function

End Class
