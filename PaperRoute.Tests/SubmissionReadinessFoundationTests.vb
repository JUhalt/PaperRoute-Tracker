Imports System
Imports System.Collections.Generic
Imports System.IO
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

<TestClass>
Public Class SubmissionReadinessFoundationTests

    <TestMethod>
    Public Sub NewModels_DefaultToSafeEmptyState()

        Dim manuscript As New Manuscript()
        Dim journal As New JournalRecord()
        Dim packetFile As New SubmissionPacketFile()

        Assert.IsNotNull(manuscript.ReadinessProfiles)
        Assert.AreEqual(0, manuscript.ReadinessProfiles.Count)

        Assert.IsNotNull(manuscript.SubmissionPackets)
        Assert.AreEqual(0, manuscript.SubmissionPackets.Count)

        Assert.IsNotNull(journal.ReadinessChecklistTemplate)
        Assert.AreEqual(0, journal.ReadinessChecklistTemplate.Count)

        Assert.AreEqual(
            ReadinessItemStatus.Unresolved,
            New ReadinessItemState().Status
        )

        Assert.AreEqual(
            SubmissionPacketFileStorageMode.MetadataOnly,
            packetFile.StorageMode
        )

        Assert.AreEqual(
            SubmissionPacketFileRole.Other,
            packetFile.Role
        )

    End Sub


    <TestMethod>
    Public Sub ReadinessSnapshot_RemainsIndependentOfReusableJournalTemplate()

        Dim template As New JournalChecklistTemplateItem With {
            .Title = "Blind the manuscript",
            .Description = "Remove identifying author information.",
            .Category = "Manuscript",
            .SortOrder = 10,
            .IsRequired = True
        }

        Dim state As New ReadinessItemState With {
            .TemplateItemId = template.Id,
            .Title = template.Title,
            .Description = template.Description,
            .Category = template.Category,
            .SortOrder = template.SortOrder,
            .IsRequired = template.IsRequired,
            .Status = ReadinessItemStatus.Complete
        }

        template.Title =
            "Changed reusable template text"

        Assert.AreEqual(
            "Blind the manuscript",
            state.Title
        )

        Assert.AreEqual(
            ReadinessItemStatus.Complete,
            state.Status
        )

    End Sub


    <TestMethod>
    Public Sub Clone_DeepCopiesReadinessAndSubmissionPackets()

        Dim manuscript As New Manuscript With {
            .Title = "Readiness clone test"
        }

        Dim version As New ManuscriptVersion With {
            .Label = "Submission candidate",
            .CreatedDate = New DateTime(2026, 8, 27)
        }

        manuscript.Versions.Add(version)
        manuscript.CurrentVersionId = version.Id

        Dim readiness As New ManuscriptReadiness With {
            .JournalName = "Journal of Clone Testing",
            .Notes = "Original readiness notes"
        }

        readiness.Items.Add(
            New ReadinessItemState With {
                .Title = "Cover letter",
                .Status = ReadinessItemStatus.Complete,
                .UserNotes = "Original item notes"
            }
        )

        manuscript.ReadinessProfiles.Add(readiness)

        Dim packet As New SubmissionPacket With {
            .ReadinessProfileId = readiness.Id,
            .JournalName = readiness.JournalName,
            .ManuscriptVersionId = version.Id,
            .Label = "Round 1 packet",
            .RevisionRoundNumber = 1
        }

        packet.Files.Add(
            New SubmissionPacketFile With {
                .Role = SubmissionPacketFileRole.CoverLetter,
                .Label = "Cover letter",
                .StorageMode = SubmissionPacketFileStorageMode.LinkedExternal,
                .LocalFilePath = "C:\external\cover-letter.docx",
                .Sha256 = New String("a"c, 64)
            }
        )

        manuscript.SubmissionPackets.Add(packet)

        Dim clone As Manuscript =
            ManuscriptCloneService.CloneManuscript(
                manuscript
            )

        clone.ReadinessProfiles(0).Notes =
            "Clone-only readiness notes"

        clone.ReadinessProfiles(0).Items(0).UserNotes =
            "Clone-only item notes"

        clone.SubmissionPackets(0).Label =
            "Clone-only packet"

        clone.SubmissionPackets(0).Files(0).Label =
            "Clone-only file"

        Assert.AreEqual(
            "Original readiness notes",
            manuscript.ReadinessProfiles(0).Notes
        )

        Assert.AreEqual(
            "Original item notes",
            manuscript.ReadinessProfiles(0).Items(0).UserNotes
        )

        Assert.AreEqual(
            "Round 1 packet",
            manuscript.SubmissionPackets(0).Label
        )

        Assert.AreEqual(
            "Cover letter",
            manuscript.SubmissionPackets(0).Files(0).Label
        )

    End Sub


    <TestMethod>
    Public Sub Repository_RoundTripsReadinessPacketAndIntegrityMetadata()

        Dim root As String =
            CreateTemporaryRoot()

        Try

            Dim dataDirectory As String =
                Path.Combine(
                    root,
                    "data"
                )

            Dim managedDirectory As String =
                Path.Combine(
                    root,
                    "managed"
                )

            Dim repository As New ManuscriptRepository(
                dataDirectory,
                managedDirectory
            )

            Dim manuscript As New Manuscript With {
                .Title = "Submission readiness persistence"
            }

            Dim version As New ManuscriptVersion With {
                .Label = "Exact packet version",
                .CreatedDate = New DateTime(2026, 8, 27)
            }

            manuscript.Versions.Add(version)
            manuscript.CurrentVersionId = version.Id

            Dim submission As New JournalSubmission With {
                .JournalName = "Journal of Persistence",
                .SubmittedDate = New DateTime(2026, 8, 27)
            }

            manuscript.Submissions.Add(submission)

            Dim journalId As Guid =
                Guid.NewGuid()

            Dim readiness As New ManuscriptReadiness With {
                .JournalId = journalId,
                .JournalName = "Journal of Persistence",
                .Notes = "Ready when the checklist is satisfied."
            }

            Dim templateItemId As Guid =
                Guid.NewGuid()

            readiness.Items.Add(
                New ReadinessItemState With {
                    .TemplateItemId = templateItemId,
                    .Title = "Blinded manuscript",
                    .Description = "Remove identifying information.",
                    .Category = "Manuscript",
                    .SortOrder = 10,
                    .IsRequired = True,
                    .Status = ReadinessItemStatus.Complete,
                    .CompletedAtUtc = New DateTime(
                        2026,
                        8,
                        27,
                        15,
                        0,
                        0,
                        DateTimeKind.Utc
                    )
                }
            )

            manuscript.ReadinessProfiles.Add(readiness)

            Dim packet As New SubmissionPacket With {
                .ReadinessProfileId = readiness.Id,
                .JournalId = journalId,
                .JournalName = readiness.JournalName,
                .ManuscriptVersionId = version.Id,
                .SubmissionId = submission.Id,
                .RevisionRoundNumber = 2,
                .Label = "Revision 2 packet",
                .Notes = "Prepared packet metadata"
            }

            packet.Files.Add(
                New SubmissionPacketFile With {
                    .Role = SubmissionPacketFileRole.BlindedManuscript,
                    .Label = "Blinded manuscript",
                    .Notes = "External file intentionally need not exist.",
                    .LocalFilePath = Path.Combine(
                        root,
                        "missing-external-manuscript.docx"
                    ),
                    .StorageMode = SubmissionPacketFileStorageMode.LinkedExternal,
                    .OriginalFileName = "manuscript-r2.docx",
                    .Sha256 = New String("b"c, 64),
                    .FileSizeBytes = 123456,
                    .LastWriteTimeUtc = New DateTime(
                        2026,
                        8,
                        27,
                        14,
                        30,
                        0,
                        DateTimeKind.Utc
                    ),
                    .HashComputedAtUtc = New DateTime(
                        2026,
                        8,
                        27,
                        14,
                        31,
                        0,
                        DateTimeKind.Utc
                    )
                }
            )

            manuscript.SubmissionPackets.Add(packet)

            repository.Save(
                New List(Of Manuscript) From {
                    manuscript
                }
            )

            Dim loaded As List(Of Manuscript) =
                repository.Load()

            Assert.AreEqual(
                1,
                loaded(0).ReadinessProfiles.Count
            )

            Assert.AreEqual(
                ReadinessItemStatus.Complete,
                loaded(0).ReadinessProfiles(0).Items(0).Status
            )

            Assert.AreEqual(
                templateItemId,
                loaded(0).ReadinessProfiles(0).Items(0).TemplateItemId.Value
            )

            Assert.AreEqual(
                1,
                loaded(0).SubmissionPackets.Count
            )

            Dim loadedPacket As SubmissionPacket =
                loaded(0).SubmissionPackets(0)

            Assert.AreEqual(
                version.Id,
                loadedPacket.ManuscriptVersionId
            )

            Assert.AreEqual(
                submission.Id,
                loadedPacket.SubmissionId.Value
            )

            Assert.AreEqual(
                2,
                loadedPacket.RevisionRoundNumber.Value
            )

            Assert.AreEqual(
                SubmissionPacketFileStorageMode.LinkedExternal,
                loadedPacket.Files(0).StorageMode
            )

            Assert.AreEqual(
                New String("b"c, 64),
                loadedPacket.Files(0).Sha256
            )

            Assert.AreEqual(
                123456L,
                loadedPacket.Files(0).FileSizeBytes.Value
            )

        Finally

            DeleteTemporaryRoot(
                root
            )

        End Try

    End Sub


    <TestMethod>
    Public Sub AuthorLibrary_RoundTripsJournalReadinessTemplate()

        Dim root As String =
            CreateTemporaryRoot()

        Try

            Dim repository As New AuthorLibraryRepository(
                root
            )

            Dim journal As New JournalRecord With {
                .Name = "Journal of Readiness",
                .Publisher = "Example Publisher"
            }

            Dim templateId As Guid =
                Guid.NewGuid()

            journal.ReadinessChecklistTemplate.Add(
                New JournalChecklistTemplateItem With {
                    .Id = templateId,
                    .Title = "Cover letter",
                    .Description = "Prepare a journal-specific cover letter.",
                    .Category = "Editorial",
                    .SortOrder = 20,
                    .IsRequired = True
                }
            )

            Dim library As New AuthorLibraryData()

            library.Journals.Add(
                journal
            )

            repository.Save(
                library
            )

            Dim loaded As AuthorLibraryData =
                repository.Load()

            Assert.AreEqual(
                1,
                loaded.Journals.Count
            )

            Assert.AreEqual(
                1,
                loaded.Journals(0).ReadinessChecklistTemplate.Count
            )

            Assert.AreEqual(
                templateId,
                loaded.Journals(0).ReadinessChecklistTemplate(0).Id
            )

            Assert.AreEqual(
                "Cover letter",
                loaded.Journals(0).ReadinessChecklistTemplate(0).Title
            )

            Assert.IsTrue(
                loaded.Journals(0).ReadinessChecklistTemplate(0).IsRequired
            )

        Finally

            DeleteTemporaryRoot(
                root
            )

        End Try

    End Sub


    <TestMethod>
    Public Sub PacketPreparation_DoesNotCreateSubmissionOrChangeLifecycleState()

        Dim manuscript As New Manuscript With {
            .CurrentStage = PaperStage.Draft,
            .Location = ManuscriptLocation.Pipeline
        }

        Dim version As New ManuscriptVersion With {
            .Label = "Draft packet version"
        }

        manuscript.Versions.Add(
            version
        )

        Dim readiness As New ManuscriptReadiness With {
            .JournalName = "Journal Before Submission"
        }

        manuscript.ReadinessProfiles.Add(
            readiness
        )

        manuscript.SubmissionPackets.Add(
            New SubmissionPacket With {
                .ReadinessProfileId = readiness.Id,
                .JournalName = readiness.JournalName,
                .ManuscriptVersionId = version.Id,
                .Label = "Prepared, not submitted"
            }
        )

        Assert.AreEqual(
            PaperStage.Draft,
            manuscript.CurrentStage
        )

        Assert.AreEqual(
            ManuscriptLocation.Pipeline,
            manuscript.Location
        )

        Assert.AreEqual(
            0,
            manuscript.Submissions.Count
        )

    End Sub

End Class
