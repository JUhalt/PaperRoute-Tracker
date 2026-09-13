Imports System
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

<TestClass>
Public Class SubmissionReadinessServiceTests

    <TestMethod>
    Public Sub CreateProfile_SnapshotsTemplateFieldsAndOrder()

        Dim manuscript As New Manuscript()
        Dim journal As New JournalRecord With {
            .Name = "Journal of Readiness"
        }

        Dim later As New JournalChecklistTemplateItem With {
            .Title = "Cover letter",
            .Description = "Tailor the letter.",
            .Category = "Editorial",
            .SortOrder = 20,
            .IsRequired = False
        }

        Dim earlier As New JournalChecklistTemplateItem With {
            .Title = "Anonymous manuscript",
            .Description = "Remove author identifiers.",
            .Category = "Manuscript",
            .SortOrder = 10,
            .IsRequired = True
        }

        journal.ReadinessChecklistTemplate.Add(later)
        journal.ReadinessChecklistTemplate.Add(earlier)

        Dim created As DateTime =
            New DateTime(2026, 8, 27, 20, 0, 0, DateTimeKind.Utc)

        Dim profile As ManuscriptReadiness =
            SubmissionReadinessService.CreateProfileFromJournal(
                manuscript,
                journal,
                created
            )

        Assert.AreEqual(journal.Id, profile.JournalId.Value)
        Assert.AreEqual(journal.Name, profile.JournalName)
        Assert.AreEqual(created, profile.CreatedAtUtc)
        Assert.AreEqual(2, profile.Items.Count)
        Assert.AreEqual(earlier.Id, profile.Items(0).TemplateItemId.Value)
        Assert.AreEqual("Anonymous manuscript", profile.Items(0).Title)
        Assert.IsTrue(profile.Items(0).IsRequired)
        Assert.AreEqual(
            ReadinessItemStatus.Unresolved,
            profile.Items(0).Status
        )
        Assert.AreEqual(later.Id, profile.Items(1).TemplateItemId.Value)

    End Sub

    <TestMethod>
    Public Sub CreateProfile_ReturnsExistingProfileForSameJournal()

        Dim manuscript As New Manuscript()
        Dim journal As New JournalRecord With {
            .Name = "One Profile Journal"
        }

        Dim first As ManuscriptReadiness =
            SubmissionReadinessService.CreateProfileFromJournal(
                manuscript,
                journal
            )

        Dim second As ManuscriptReadiness =
            SubmissionReadinessService.CreateProfileFromJournal(
                manuscript,
                journal
            )

        Assert.AreSame(first, second)
        Assert.AreEqual(1, manuscript.ReadinessProfiles.Count)

    End Sub

    <TestMethod>
    Public Sub AddMissingTemplateItems_PreservesExistingSnapshotAndStatus()

        Dim manuscript As New Manuscript()
        Dim journal As New JournalRecord With {
            .Name = "Growing Template Journal"
        }

        Dim originalTemplate As New JournalChecklistTemplateItem With {
            .Title = "Original title",
            .SortOrder = 10,
            .IsRequired = True
        }

        journal.ReadinessChecklistTemplate.Add(originalTemplate)

        Dim profile As ManuscriptReadiness =
            SubmissionReadinessService.CreateProfileFromJournal(
                manuscript,
                journal
            )

        SubmissionReadinessService.SetStatus(
            profile.Items(0),
            ReadinessItemStatus.Complete,
            New DateTime(2026, 8, 27, 20, 5, 0, DateTimeKind.Utc)
        )

        originalTemplate.Title =
            "Template title changed later"

        Dim newTemplate As New JournalChecklistTemplateItem With {
            .Title = "New requirement",
            .SortOrder = 20,
            .IsRequired = False
        }

        journal.ReadinessChecklistTemplate.Add(newTemplate)

        Dim added As Integer =
            SubmissionReadinessService.AddMissingTemplateItems(
                profile,
                journal,
                New DateTime(2026, 8, 27, 20, 10, 0, DateTimeKind.Utc)
            )

        Assert.AreEqual(1, added)
        Assert.AreEqual(2, profile.Items.Count)
        Assert.AreEqual("Original title", profile.Items(0).Title)
        Assert.AreEqual(
            ReadinessItemStatus.Complete,
            profile.Items(0).Status
        )
        Assert.AreEqual("New requirement", profile.Items(1).Title)

    End Sub

    <TestMethod>
    Public Sub SetStatus_TracksCompleteAndNotApplicableDistinctly()

        Dim item As New ReadinessItemState()

        Dim completed As DateTime =
            New DateTime(2026, 8, 27, 20, 15, 0, DateTimeKind.Utc)

        SubmissionReadinessService.SetStatus(
            item,
            ReadinessItemStatus.Complete,
            completed
        )

        Assert.AreEqual(
            ReadinessItemStatus.Complete,
            item.Status
        )
        Assert.AreEqual(completed, item.CompletedAtUtc.Value)

        Dim notApplicable As DateTime =
            completed.AddMinutes(5)

        SubmissionReadinessService.SetStatus(
            item,
            ReadinessItemStatus.NotApplicable,
            notApplicable
        )

        Assert.AreEqual(
            ReadinessItemStatus.NotApplicable,
            item.Status
        )
        Assert.IsFalse(item.CompletedAtUtc.HasValue)
        Assert.AreEqual(notApplicable, item.LastModifiedAtUtc.Value)

    End Sub

    <TestMethod>
    Public Sub Summary_SeparatesRequiredOptionalCompleteAndNotApplicable()

        Dim profile As New ManuscriptReadiness()

        profile.Items.Add(
            New ReadinessItemState With {
                .IsRequired = True,
                .Status = ReadinessItemStatus.Complete
            }
        )
        profile.Items.Add(
            New ReadinessItemState With {
                .IsRequired = True,
                .Status = ReadinessItemStatus.NotApplicable
            }
        )
        profile.Items.Add(
            New ReadinessItemState With {
                .IsRequired = True,
                .Status = ReadinessItemStatus.Unresolved
            }
        )
        profile.Items.Add(
            New ReadinessItemState With {
                .IsRequired = False,
                .Status = ReadinessItemStatus.Complete
            }
        )
        profile.Items.Add(
            New ReadinessItemState With {
                .IsRequired = False,
                .Status = ReadinessItemStatus.Unresolved
            }
        )

        Dim summary As ReadinessSummary =
            SubmissionReadinessService.GetSummary(profile)

        Assert.AreEqual(3, summary.RequiredTotal)
        Assert.AreEqual(2, summary.RequiredResolved)
        Assert.AreEqual(1, summary.RequiredComplete)
        Assert.AreEqual(1, summary.RequiredNotApplicable)
        Assert.AreEqual(1, summary.RequiredUnresolved)
        Assert.AreEqual(2, summary.OptionalTotal)
        Assert.AreEqual(1, summary.OptionalResolved)
        Assert.IsFalse(summary.IsReady)

    End Sub

    <TestMethod>
    Public Sub CreateProfile_DoesNotMutateLifecycleOrCreateSubmission()

        Dim manuscript As New Manuscript With {
            .CurrentStage = PaperStage.Draft,
            .Location = ManuscriptLocation.Pipeline
        }

        Dim historyCount As Integer = manuscript.History.Count
        Dim submissionCount As Integer = manuscript.Submissions.Count

        Dim journal As New JournalRecord With {
            .Name = "Advisory Only Journal"
        }

        SubmissionReadinessService.CreateProfileFromJournal(
            manuscript,
            journal
        )

        Assert.AreEqual(PaperStage.Draft, manuscript.CurrentStage)
        Assert.AreEqual(
            ManuscriptLocation.Pipeline,
            manuscript.Location
        )
        Assert.AreEqual(historyCount, manuscript.History.Count)
        Assert.AreEqual(submissionCount, manuscript.Submissions.Count)

    End Sub

    <TestMethod>
    Public Sub Rerouting_CanRetainSeparateProfilesForMultipleJournals()

        Dim manuscript As New Manuscript()
        Dim journalA As New JournalRecord With {.Name = "Journal A"}
        Dim journalB As New JournalRecord With {.Name = "Journal B"}

        Dim profileA As ManuscriptReadiness =
            SubmissionReadinessService.CreateProfileFromJournal(
                manuscript,
                journalA
            )

        Dim profileB As ManuscriptReadiness =
            SubmissionReadinessService.CreateProfileFromJournal(
                manuscript,
                journalB
            )

        Assert.AreEqual(2, manuscript.ReadinessProfiles.Count)
        Assert.AreNotEqual(profileA.Id, profileB.Id)
        Assert.AreEqual(journalA.Id, profileA.JournalId.Value)
        Assert.AreEqual(journalB.Id, profileB.JournalId.Value)

    End Sub

    <TestMethod>
    Public Sub RemoveProfile_RejectsProfileLinkedToSubmissionPacket()

        Dim manuscript As New Manuscript()
        Dim journal As New JournalRecord With {.Name = "Packet Journal"}

        Dim profile As ManuscriptReadiness =
            SubmissionReadinessService.CreateProfileFromJournal(
                manuscript,
                journal
            )

        manuscript.SubmissionPackets.Add(
            New SubmissionPacket With {
                .ReadinessProfileId = profile.Id,
                .ManuscriptVersionId = Guid.NewGuid()
            }
        )

        Assert.ThrowsExactly(Of InvalidOperationException)(
            Sub()
                SubmissionReadinessService.RemoveProfile(
                    manuscript,
                    profile.Id
                )
            End Sub
        )

        Assert.AreEqual(1, manuscript.ReadinessProfiles.Count)

    End Sub

    <TestMethod>
    Public Sub RemoveProfile_RemovesUnlinkedProfileWithoutLifecycleMutation()

        Dim manuscript As New Manuscript With {
            .CurrentStage = PaperStage.Revision
        }

        Dim journal As New JournalRecord With {
            .Name = "Disposable Readiness Profile"
        }

        Dim profile As ManuscriptReadiness =
            SubmissionReadinessService.CreateProfileFromJournal(
                manuscript,
                journal
            )

        Dim removed As Boolean =
            SubmissionReadinessService.RemoveProfile(
                manuscript,
                profile.Id
            )

        Assert.IsTrue(removed)
        Assert.AreEqual(0, manuscript.ReadinessProfiles.Count)
        Assert.AreEqual(PaperStage.Revision, manuscript.CurrentStage)
        Assert.AreEqual(0, manuscript.Submissions.Count)

    End Sub

End Class
