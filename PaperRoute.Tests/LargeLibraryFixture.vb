Imports System
Imports System.Collections.Generic
Imports System.Globalization
Imports System.IO
Imports System.Linq
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

' A synthetic library large enough to time PaperRoute against its
' responsiveness budgets (#131): about 500 manuscripts with submissions,
' decisions, reviewer responses, correspondence, versions, reminders,
' shortlists, tags and packets, backed by about 2,000 managed folders under
' a temporary root. Everything is fictional. A run with the same size is the
' same library every time, so before and after measurements compare like
' with like. The example library (ExampleLibraryService) is the small case.
Friend NotInheritable Class LargeLibraryFixture

    Private Sub New()
    End Sub


    ' Dates are placed around this day rather than around today, so the
    ' library does not drift as the calendar moves.
    Public Shared ReadOnly Today As New DateTime(2026, 10, 16)

    Private Const Seed As Integer = 20261016

    Private Shared ReadOnly Topics As String() = {
        "anchoring effects in clinical risk estimates",
        "retrieval practice in introductory statistics",
        "sleep and memory consolidation in older adults",
        "measurement invariance of a short grit scale",
        "open materials in developmental science",
        "feedback timing in classroom learning",
        "working memory training and transfer",
        "attention lapses during long lectures",
        "reading fluency in bilingual children",
        "self-control and course persistence",
        "metacognitive judgments of learning",
        "handwriting and note quality",
        "spacing effects in vocabulary learning",
        "test anxiety and performance",
        "collaborative recall in small groups"
    }

    Private Shared ReadOnly Variants As String() = {
        "a preregistered replication",
        "a survey",
        "a multilevel analysis",
        "a longitudinal study",
        "a registered report",
        "a meta-analysis",
        "a field experiment",
        "a null result"
    }

    Private Shared ReadOnly TagPool As String() = {
        "replication", "teaching", "open science", "methods", "clinical", "developmental", "longitudinal", "collaboration"
    }

    Private Shared ReadOnly ReminderTitles As String() = {
        "Draft the cover letter", "Check the author guidelines", "Send the proofs back", "Update the preprint", "Ask the coauthors for comments"
    }

    Private Shared ReadOnly Checks As String() = {
        "trust.known", "trust.publisher", "trust.review", "trust.indexed", "trust.fees", "trust.guidelines", "fit.scope", "fit.type", "fit.audience"
    }


    ' The library on disk, with the repositories that read it.
    Friend NotInheritable Class SyntheticLibrary

        Public Property Name As String = String.Empty
        Public Property ManagedDirectory As String = String.Empty
        Public Property Repository As ManuscriptRepository
        Public Property AuthorRepository As AuthorLibraryRepository
        Public Property Manuscripts As New List(Of Manuscript)()

        ' Leaf folders under the managed root that hold a file.
        Public Property ManagedFolderCount As Integer

    End Class


    ' Writes the example library (the small case) under root.
    Public Shared Function WriteExample(
        root As String
    ) As SyntheticLibrary

        Dim example = ExampleLibraryService.Create(Today)

        Return Write(
            "example",
            root,
            example.Manuscripts,
            example.Library
        )

    End Function


    ' Writes a library of manuscriptCount manuscripts under root.
    Public Shared Function Create(
        root As String,
        manuscriptCount As Integer
    ) As SyntheticLibrary

        Dim random As New Random(Seed)
        Dim library As AuthorLibraryData = CreateAuthorLibrary(random)
        Dim managedDirectory As String = Path.Combine(root, "library")
        Dim manuscripts As New List(Of Manuscript)()
        Dim folders As Integer = 0

        For index As Integer = 1 To manuscriptCount

            manuscripts.Add(
                CreateManuscript(
                    index,
                    random,
                    library,
                    managedDirectory,
                    folders
                )
            )

        Next

        Dim result As SyntheticLibrary =
            Write(
                manuscriptCount.ToString(CultureInfo.InvariantCulture),
                root,
                manuscripts,
                library
            )

        result.ManagedFolderCount = folders
        Return result

    End Function


    Private Shared Function Write(
        name As String,
        root As String,
        manuscripts As List(Of Manuscript),
        library As AuthorLibraryData
    ) As SyntheticLibrary

        Dim dataDirectory As String = Path.Combine(root, "data")
        Dim managedDirectory As String = Path.Combine(root, "library")

        Directory.CreateDirectory(dataDirectory)
        Directory.CreateDirectory(managedDirectory)

        Dim repository As New ManuscriptRepository(dataDirectory, managedDirectory)
        Dim authorRepository As New AuthorLibraryRepository(dataDirectory)

        authorRepository.Save(library)
        repository.Save(manuscripts)

        Return New SyntheticLibrary With {
            .Name = name,
            .ManagedDirectory = managedDirectory,
            .Repository = repository,
            .AuthorRepository = authorRepository,
            .Manuscripts = manuscripts
        }

    End Function


    ' Twelve fictional journals, forty authors and eight affiliations: enough
    ' that the Journals page, the shortlists and the author search have
    ' something to show.
    Private Shared Function CreateAuthorLibrary(
        random As Random
    ) As AuthorLibraryData

        Dim library As New AuthorLibraryData()

        For index As Integer = 1 To 8
            library.Affiliations.Add(
                New AffiliationRecord With {
                    .Institution = "Synthetic University " & index.ToString(CultureInfo.InvariantCulture),
                    .Department = "Department of Psychology",
                    .City = "Exampleton"
                }
            )
        Next

        For index As Integer = 1 To 40
            library.Authors.Add(
                New AuthorRecord With {
                    .GivenName = "Author",
                    .FamilyName = "Synthetic " & index.ToString("00", CultureInfo.InvariantCulture),
                    .IsMe = index = 1,
                    .Notes = "Fictional author."
                }
            )
        Next

        For index As Integer = 1 To 12

            Dim journal As New JournalRecord With {
                .Name = "Fictional Journal " & index.ToString("00", CultureInfo.InvariantCulture),
                .Publisher = If(index Mod 3 = 0, "Fictional Open Publishing", "Fictional Press"),
                .IsFavorite = index = 1,
                .IsShortlisted = index = 2,
                .Notes = "Fictional journal."
            }

            ' Two journals carry a checklist, as journals a researcher
            ' submits to often do.
            If index <= 2 Then
                journal.ReadinessChecklistTemplate.AddRange({
                    New JournalChecklistTemplateItem With {.Title = "Cover letter", .SortOrder = 1},
                    New JournalChecklistTemplateItem With {.Title = "Data availability statement", .SortOrder = 2},
                    New JournalChecklistTemplateItem With {.Title = "Word count under 5,000", .SortOrder = 3}
                })
            End If

            library.Journals.Add(journal)

        Next

        Return library

    End Function


    Private Shared Function CreateManuscript(
        index As Integer,
        random As Random,
        library As AuthorLibraryData,
        managedDirectory As String,
        ByRef folders As Integer
    ) As Manuscript

        Dim journals As List(Of JournalRecord) = library.Journals
        Dim roll As Integer = random.Next(100)

        ' About 55% published, 35% in the pipeline, 10% in the File Drawer.
        Dim location As ManuscriptLocation =
            If(roll < 55, ManuscriptLocation.Published, If(roll < 90, ManuscriptLocation.Pipeline, ManuscriptLocation.FileDrawer))

        Dim stage As PaperStage = PaperStage.Draft

        If location = ManuscriptLocation.Published Then
            stage = PaperStage.Published
        ElseIf location = ManuscriptLocation.Pipeline Then
            stage = PipelineStage(random)
        End If

        Dim stageEntered As DateTime = Today.AddDays(-random.Next(1, 400))
        Dim target As JournalRecord = If(random.Next(100) < 8, Nothing, journals(random.Next(journals.Count)))

        Dim manuscript As New Manuscript With {
            .Title = "Synthetic " & index.ToString("000", CultureInfo.InvariantCulture) & ": " &
                     Topics(random.Next(Topics.Length)) & ", " & Variants(random.Next(Variants.Length)),
            .CurrentStage = stage,
            .Location = location,
            .StageEnteredDate = stageEntered,
            .TargetJournal = If(target?.Name, String.Empty),
            .TargetJournalId = If(target Is Nothing, CType(Nothing, Guid?), target.Id),
            .WorkType = If(random.Next(100) < 80, WorkType.JournalArticle, If(random.Next(2) = 0, WorkType.Preprint, WorkType.ConferencePaper))
        }

        Dim authorCount As Integer = random.Next(1, 5)

        For position As Integer = 0 To authorCount - 1
            Dim author As AuthorRecord = library.Authors(random.Next(library.Authors.Count))
            If manuscript.Authors.Any(Function(item) item.AuthorId = author.Id) Then Continue For
            manuscript.Authors.Add(New ManuscriptAuthor With {.AuthorId = author.Id, .IsCorrespondingAuthor = position = 0})
        Next

        For Each tag As String In TagPool.OrderBy(Function(item) random.Next()).Take(random.Next(0, 4))
            manuscript.Tags.Add(tag)
        Next

        manuscript.Metadata.Keywords.AddRange({"synthetic", "psychology"})
        manuscript.Metadata.AbstractText = "A fictional abstract for the synthetic library."

        manuscript.History.Add(New HistoryEvent With {.Stage = PaperStage.Idea, .EventDate = stageEntered.AddDays(-30), .Note = "Synthetic."})
        If stage <> PaperStage.Idea Then
            manuscript.History.Add(New HistoryEvent With {.Stage = stage, .EventDate = stageEntered, .Note = "Synthetic."})
        End If

        AddSubmissions(manuscript, random, journals, stageEntered, managedDirectory, folders)
        AddVersions(manuscript, random, managedDirectory, folders)
        AddPacket(manuscript, random, managedDirectory, folders)

        If random.Next(100) < 30 Then
            manuscript.Reminders.Add(
                New ManuscriptReminder With {
                    .Title = ReminderTitles(random.Next(ReminderTitles.Length)),
                    .DueDate = Today.AddDays(random.Next(-10, 31))
                }
            )
        End If

        If location = ManuscriptLocation.Pipeline AndAlso stage <= PaperStage.Revision Then
            For Each journal As JournalRecord In journals.OrderBy(Function(item) random.Next()).Take(random.Next(1, 4))
                manuscript.JournalShortlist.Add(
                    New JournalCandidate With {
                        .JournalName = journal.Name,
                        .JournalId = journal.Id,
                        .Status = CType(random.Next(3), CandidateStatus),
                        .Notes = "Synthetic shortlist entry.",
                        .Checks = Checks.Take(random.Next(0, Checks.Length + 1)).ToList(),
                        .AddedDate = Today.AddDays(-random.Next(0, 60))
                    }
                )
            Next
        End If

        ' Some pipeline manuscripts are being prepared for a journal with a
        ' checklist, which Deadlines lists as unfinished preparation.
        If location = ManuscriptLocation.Pipeline AndAlso target IsNot Nothing AndAlso
           target.ReadinessChecklistTemplate.Count > 0 AndAlso random.Next(100) < 50 Then

            Dim readiness As New ManuscriptReadiness With {.JournalId = target.Id, .JournalName = target.Name}

            For Each item As JournalChecklistTemplateItem In target.ReadinessChecklistTemplate
                readiness.Items.Add(
                    New ReadinessItemState With {
                        .TemplateItemId = item.Id,
                        .Title = item.Title,
                        .SortOrder = item.SortOrder,
                        .Status = If(random.Next(2) = 0, ReadinessItemStatus.Complete, ReadinessItemStatus.Unresolved)
                    }
                )
            Next

            manuscript.ReadinessProfiles.Add(readiness)

        End If

        If location = ManuscriptLocation.Published Then
            manuscript.Metadata.Doi = "10.5555/synthetic." & index.ToString("000", CultureInfo.InvariantCulture)
            manuscript.Metadata.PublicationJournal = If(manuscript.Submissions.LastOrDefault()?.JournalName, manuscript.TargetJournal)
            manuscript.Metadata.PublishedDate = stageEntered
        ElseIf location = ManuscriptLocation.FileDrawer Then
            manuscript.FileDrawerDate = stageEntered
            manuscript.FileDrawerReason = "Set aside after several rejections."
        End If

        Return manuscript

    End Function


    Private Shared Function PipelineStage(
        random As Random
    ) As PaperStage

        Dim roll As Integer = random.Next(100)

        If roll < 5 Then Return PaperStage.Idea
        If roll < 25 Then Return PaperStage.Draft
        If roll < 40 Then Return PaperStage.Submitted
        If roll < 70 Then Return PaperStage.UnderReview
        If roll < 90 Then Return PaperStage.Revision
        If roll < 95 Then Return PaperStage.Accepted
        Return PaperStage.InPress

    End Function


    ' Earlier submissions end in a rejection; the current one depends on the
    ' stage. Every decision gets a managed decision letter.
    Private Shared Sub AddSubmissions(
        manuscript As Manuscript,
        random As Random,
        journals As List(Of JournalRecord),
        stageEntered As DateTime,
        managedDirectory As String,
        ByRef folders As Integer
    )

        Dim earlier As Integer

        Select Case manuscript.Location
            Case ManuscriptLocation.Published
                earlier = random.Next(0, 3)
            Case ManuscriptLocation.FileDrawer
                earlier = random.Next(2, 4)
            Case Else
                earlier = random.Next(0, 2)
        End Select

        ' Room for the earlier rounds before the current submission.
        Dim submitted As DateTime =
            stageEntered.AddDays(-If(manuscript.Location = ManuscriptLocation.Published, random.Next(200, 900), random.Next(60, 700)))

        For round As Integer = 1 To earlier
            Dim journal As JournalRecord = journals(random.Next(journals.Count))
            Dim rejection As EditorialDecision =
                If(random.Next(3) = 0, EditorialDecision.DeskRejected, EditorialDecision.RejectedAfterReview)
            Dim decided As DateTime = submitted.AddDays(If(rejection = EditorialDecision.DeskRejected, random.Next(3, 14), random.Next(40, 120)))
            Dim submission As JournalSubmission = CreateSubmission(journal, submitted, random)
            submission.Decisions.Add(New EditorialDecisionEvent With {.Decision = rejection, .DecisionDate = decided, .Notes = "Synthetic decision."})
            AddDecisionLetter(manuscript, submission, decided, managedDirectory, folders)
            manuscript.Submissions.Add(submission)
            submitted = decided.AddDays(random.Next(14, 60))
        Next

        ' The current submission.
        Dim current As JournalSubmission = Nothing

        Select Case manuscript.Location

            Case ManuscriptLocation.Published
                Dim journal As JournalRecord = journals(random.Next(journals.Count))
                Dim accepted As DateTime = submitted.AddDays(random.Next(160, 300))
                If accepted >= stageEntered Then accepted = stageEntered.AddDays(-10)
                current = CreateSubmission(journal, submitted, random)
                If random.Next(100) < 60 Then
                    Dim revision As DateTime = submitted.AddDays(random.Next(40, 100))
                    Dim major As New EditorialDecisionEvent With {.Decision = EditorialDecision.MajorRevision, .DecisionDate = revision, .RevisionDeadline = revision.AddDays(60), .Notes = "Synthetic decision."}
                    current.Decisions.Add(major)
                    AddResponses(current, major, random, allAddressed:=True)
                    AddDecisionLetter(manuscript, current, revision, managedDirectory, folders)
                    manuscript.History.Add(New HistoryEvent With {.Stage = PaperStage.UnderReview, .EventDate = revision.AddDays(45), .Note = "Resubmitted after revision."})
                End If
                current.Decisions.Add(New EditorialDecisionEvent With {.Decision = EditorialDecision.Accepted, .DecisionDate = accepted, .Notes = "Synthetic decision."})
                AddDecisionLetter(manuscript, current, accepted, managedDirectory, folders)
                manuscript.TargetJournal = journal.Name
                manuscript.TargetJournalId = journal.Id

            Case ManuscriptLocation.Pipeline

                Select Case manuscript.CurrentStage

                    Case PaperStage.Submitted, PaperStage.UnderReview
                        current = CreateSubmission(JournalOrAny(manuscript, journals, random), stageEntered, random)
                        If random.Next(100) < 30 Then current.FollowUpDate = Today.AddDays(random.Next(-20, 40))

                    Case PaperStage.Revision
                        current = CreateSubmission(JournalOrAny(manuscript, journals, random), stageEntered.AddDays(-random.Next(40, 120)), random)
                        Dim decision As New EditorialDecisionEvent With {
                            .Decision = If(random.Next(2) = 0, EditorialDecision.MajorRevision, EditorialDecision.MinorRevision),
                            .DecisionDate = stageEntered,
                            .RevisionDeadline = Today.AddDays(random.Next(-15, 60)),
                            .Notes = "Synthetic decision."
                        }
                        current.Decisions.Add(decision)
                        manuscript.RevisionDeadline = decision.RevisionDeadline
                        AddResponses(current, decision, random, allAddressed:=False)
                        AddDecisionLetter(manuscript, current, stageEntered, managedDirectory, folders)

                    Case PaperStage.Accepted, PaperStage.InPress
                        current = CreateSubmission(JournalOrAny(manuscript, journals, random), stageEntered.AddDays(-random.Next(90, 200)), random)
                        current.Decisions.Add(New EditorialDecisionEvent With {.Decision = EditorialDecision.Accepted, .DecisionDate = stageEntered, .Notes = "Synthetic decision."})
                        AddDecisionLetter(manuscript, current, stageEntered, managedDirectory, folders)

                End Select

        End Select

        If current IsNot Nothing Then
            manuscript.Submissions.Add(current)
        End If

    End Sub


    Private Shared Function JournalOrAny(
        manuscript As Manuscript,
        journals As List(Of JournalRecord),
        random As Random
    ) As JournalRecord

        If manuscript.TargetJournalId.HasValue Then
            Return journals.First(Function(item) item.Id = manuscript.TargetJournalId.Value)
        End If

        Return journals(random.Next(journals.Count))

    End Function


    Private Shared Function CreateSubmission(
        journal As JournalRecord,
        submittedOn As DateTime,
        random As Random
    ) As JournalSubmission

        Return New JournalSubmission With {
            .JournalName = journal.Name,
            .JournalId = journal.Id,
            .SubmittedDate = submittedOn,
            .ManuscriptNumber = "SYN-" & random.Next(1000, 9999).ToString(CultureInfo.InvariantCulture),
            .PortalUrl = "https://example.org/portal",
            .Notes = "Synthetic submission."
        }

    End Function


    Private Shared Sub AddResponses(
        submission As JournalSubmission,
        decision As EditorialDecisionEvent,
        random As Random,
        allAddressed As Boolean
    )

        For index As Integer = 1 To random.Next(2, 5)
            submission.ReviewerResponses.Add(
                New ReviewerResponseItem With {
                    .DecisionId = decision.Id,
                    .RevisionRoundNumber = 1,
                    .ReviewerLabel = If(index = 1, "Editor", "Reviewer " & (index - 1).ToString(CultureInfo.InvariantCulture)),
                    .Status = If(allAddressed, ReviewerResponseStatus.Addressed, CType(random.Next(4), ReviewerResponseStatus)),
                    .CommentText = "Synthetic reviewer comment " & index.ToString(CultureInfo.InvariantCulture) & ".",
                    .ResponseText = If(allAddressed OrElse random.Next(2) = 0, "Synthetic response.", String.Empty),
                    .ManuscriptLocation = "Method"
                }
            )
        Next

    End Sub


    ' One managed decision letter per decision, written in place under
    ' <manuscript>\<submission>\<item>, where a save leaves it alone.
    Private Shared Sub AddDecisionLetter(
        manuscript As Manuscript,
        submission As JournalSubmission,
        itemDate As DateTime,
        managedDirectory As String,
        ByRef folders As Integer
    )

        Dim item As New CorrespondenceItem With {
            .ItemDate = itemDate,
            .Type = CorrespondenceType.DecisionLetter,
            .Title = "Decision letter",
            .IsManagedCopy = True
        }

        item.LocalFilePath =
            WriteManagedFile(
                Path.Combine(managedDirectory, manuscript.Id.ToString("N"), submission.Id.ToString("N"), item.Id.ToString("N")),
                "decision-letter.txt",
                folders
            )

        submission.Correspondence.Add(item)

    End Sub


    ' Two managed versions for every manuscript past an idea: the first draft
    ' and the one sent to the latest journal.
    Private Shared Sub AddVersions(
        manuscript As Manuscript,
        random As Random,
        managedDirectory As String,
        ByRef folders As Integer
    )

        If manuscript.CurrentStage = PaperStage.Idea Then Return

        Dim latest As JournalSubmission = manuscript.Submissions.LastOrDefault()

        Dim draft As New ManuscriptVersion With {
            .Label = "Draft 1",
            .CreatedDate = manuscript.StageEnteredDate.AddDays(-random.Next(30, 200)),
            .IsManagedCopy = True,
            .Notes = "Synthetic."
        }

        draft.LocalFilePath =
            WriteManagedFile(
                Path.Combine(managedDirectory, manuscript.Id.ToString("N"), "versions", draft.Id.ToString("N")),
                "manuscript-draft.txt",
                folders
            )

        manuscript.Versions.Add(draft)

        Dim sent As New ManuscriptVersion With {
            .Label = If(latest Is Nothing, "Draft 2", "Submitted to " & latest.JournalName),
            .CreatedDate = If(latest Is Nothing, draft.CreatedDate.AddDays(20), latest.SubmittedDate),
            .IsManagedCopy = True,
            .SubmissionId = latest?.Id,
            .Notes = "Synthetic."
        }

        sent.LocalFilePath =
            WriteManagedFile(
                Path.Combine(managedDirectory, manuscript.Id.ToString("N"), "versions", sent.Id.ToString("N")),
                "manuscript-submitted.txt",
                folders
            )

        manuscript.Versions.Add(sent)
        manuscript.CurrentVersionId = sent.Id

    End Sub


    ' The packet sent with the latest submission: one managed copy and two
    ' records only, for about 70% of the manuscripts that have a submission.
    Private Shared Sub AddPacket(
        manuscript As Manuscript,
        random As Random,
        managedDirectory As String,
        ByRef folders As Integer
    )

        Dim latest As JournalSubmission = manuscript.Submissions.LastOrDefault()

        If latest Is Nothing OrElse manuscript.Versions.Count = 0 OrElse random.Next(100) >= 70 Then Return

        Dim created As DateTime = DateTime.SpecifyKind(latest.SubmittedDate.AddDays(-1), DateTimeKind.Utc)

        Dim packet As SubmissionPacket =
            SubmissionPacketService.CreatePacket(
                manuscript,
                manuscript.Versions.Last().Id,
                "Submission to " & latest.JournalName,
                "Synthetic packet.",
                submissionId:=latest.Id,
                createdAtUtc:=created
            )

        packet.JournalName = latest.JournalName
        packet.JournalId = latest.JournalId

        Dim managedFile As New SubmissionPacketFile With {
            .Role = SubmissionPacketFileRole.Manuscript,
            .Label = "Anonymized manuscript",
            .StorageMode = SubmissionPacketFileStorageMode.ManagedCopy,
            .OriginalFileName = "manuscript-anonymized.txt"
        }

        managedFile.LocalFilePath =
            WriteManagedFile(
                Path.Combine(managedDirectory, manuscript.Id.ToString("N"), "packets", packet.Id.ToString("N"), managedFile.Id.ToString("N")),
                managedFile.OriginalFileName,
                folders
            )

        packet.Files.Add(managedFile)

        For Each entry In {(SubmissionPacketFileRole.TitlePage, "Title page"), (SubmissionPacketFileRole.CoverLetter, "Cover letter")}
            SubmissionPacketService.AddFile(packet, entry.Item1, entry.Item2, String.Empty, String.Empty, SubmissionPacketFileStorageMode.MetadataOnly, created)
        Next

    End Sub


    Private Shared Function WriteManagedFile(
        folder As String,
        fileName As String,
        ByRef folders As Integer
    ) As String

        Directory.CreateDirectory(folder)

        Dim filePath As String = Path.Combine(folder, fileName)
        File.WriteAllText(filePath, "Synthetic managed file.")
        folders += 1
        Return filePath

    End Function

End Class
