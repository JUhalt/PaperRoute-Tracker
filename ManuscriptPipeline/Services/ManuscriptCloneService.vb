Imports System
Imports System.Collections.Generic
Imports ManuscriptPipeline.Models

Namespace Services

    Public NotInheritable Class ManuscriptCloneService

        Private Sub New()
        End Sub


        Public Shared Function CloneManuscript(
            source As Manuscript
        ) As Manuscript

            If source Is Nothing Then
                Throw New ArgumentNullException(NameOf(source))
            End If

            Dim clone As New Manuscript With {
                .Id = source.Id,
                .Title = source.Title,
                .CoAuthors = source.CoAuthors,
                .TargetJournal = source.TargetJournal,
                .TargetJournalId = source.TargetJournalId,
                .ManuscriptUrl = source.ManuscriptUrl,
                .Metadata = CloneMetadata(source.Metadata),
                .CurrentVersionId = source.CurrentVersionId,
                .CurrentStage = source.CurrentStage,
                .Location = source.Location,
                .StageEnteredDate = source.StageEnteredDate,
                .RevisionDeadline = source.RevisionDeadline,
                .FileDrawerDate = source.FileDrawerDate,
                .FileDrawerReason = source.FileDrawerReason
            }

            If source.Authors IsNot Nothing Then
                For Each authorLink As ManuscriptAuthor In source.Authors
                    clone.Authors.Add(
                        CloneManuscriptAuthor(authorLink)
                    )
                Next
            End If

            If source.RelatedLinks IsNot Nothing Then
                For Each item As ManuscriptExternalLink In source.RelatedLinks
                    clone.RelatedLinks.Add(
                        CloneExternalLink(item)
                    )
                Next
            End If

            If source.Reminders IsNot Nothing Then
                For Each reminder As ManuscriptReminder In source.Reminders

                    If reminder Is Nothing Then
                        Continue For
                    End If

                    clone.Reminders.Add(
                        CloneReminder(reminder)
                    )

                Next
            End If

            If source.Versions IsNot Nothing Then
                For Each version As ManuscriptVersion In source.Versions

                    If version Is Nothing Then
                        Continue For
                    End If

                    clone.Versions.Add(
                        CloneVersion(version)
                    )

                Next
            End If

            If source.ReadinessProfiles IsNot Nothing Then
                For Each readiness As ManuscriptReadiness In source.ReadinessProfiles

                    If readiness Is Nothing Then
                        Continue For
                    End If

                    clone.ReadinessProfiles.Add(
                        CloneReadiness(readiness)
                    )

                Next
            End If

            If source.SubmissionPackets IsNot Nothing Then
                For Each packet As SubmissionPacket In source.SubmissionPackets

                    If packet Is Nothing Then
                        Continue For
                    End If

                    clone.SubmissionPackets.Add(
                        CloneSubmissionPacket(packet)
                    )

                Next
            End If

            If source.History IsNot Nothing Then
                For Each historyEvent As HistoryEvent In source.History

                    clone.History.Add(
                        New HistoryEvent With {
                            .Id = historyEvent.Id,
                            .RecordedAtUtc = historyEvent.RecordedAtUtc,
                            .LastModifiedAtUtc = historyEvent.LastModifiedAtUtc,
                            .EventDate = historyEvent.EventDate,
                            .Stage = historyEvent.Stage,
                            .Note = historyEvent.Note
                        }
                    )

                Next
            End If

            If source.Submissions IsNot Nothing Then
                For Each submission As JournalSubmission In source.Submissions
                    clone.Submissions.Add(
                        CloneSubmission(submission)
                    )
                Next
            End If

            Return clone

        End Function


        Public Shared Function CloneMetadata(
            source As ManuscriptMetadata
        ) As ManuscriptMetadata

            If source Is Nothing Then
                Return New ManuscriptMetadata()
            End If

            Dim clone As New ManuscriptMetadata With {
                .AbstractText = source.AbstractText,
                .Doi = source.Doi,
                .PublicationJournal = source.PublicationJournal,
                .PublishedDate = source.PublishedDate,
                .Volume = source.Volume,
                .Issue = source.Issue,
                .Pages = source.Pages,
                .Publisher = source.Publisher,
                .PublicationUrl = source.PublicationUrl,
                .PreprintDoi = source.PreprintDoi,
                .PreprintUrl = source.PreprintUrl
            }

            If source.Keywords IsNot Nothing Then
                clone.Keywords =
                    New List(Of String)(
                        source.Keywords
                    )
            End If

            If source.ExternalIdentifiers IsNot Nothing Then
                clone.ExternalIdentifiers =
                    New Dictionary(Of String, String)(
                        source.ExternalIdentifiers,
                        StringComparer.OrdinalIgnoreCase
                    )
            End If

            Return clone

        End Function


        Public Shared Function CloneManuscriptAuthor(
            source As ManuscriptAuthor
        ) As ManuscriptAuthor

            If source Is Nothing Then
                Throw New ArgumentNullException(NameOf(source))
            End If

            Dim clone As New ManuscriptAuthor With {
                .AuthorId = source.AuthorId,
                .IsCorrespondingAuthor = source.IsCorrespondingAuthor
            }

            If source.AffiliationIds IsNot Nothing Then
                clone.AffiliationIds =
                    New List(Of Guid)(
                        source.AffiliationIds
                    )
            End If

            Return clone

        End Function


        Public Shared Function CloneExternalLink(
            source As ManuscriptExternalLink
        ) As ManuscriptExternalLink

            If source Is Nothing Then
                Throw New ArgumentNullException(NameOf(source))
            End If

            Return New ManuscriptExternalLink With {
                .Id = source.Id,
                .Label = source.Label,
                .Url = source.Url,
                .Notes = source.Notes
            }

        End Function


        Public Shared Function CloneReminder(
            source As ManuscriptReminder
        ) As ManuscriptReminder

            If source Is Nothing Then
                Throw New ArgumentNullException(NameOf(source))
            End If

            Return New ManuscriptReminder With {
                .Id = source.Id,
                .DueDate = source.DueDate,
                .Title = source.Title,
                .Notes = source.Notes,
                .IsCompleted = source.IsCompleted,
                .CompletedDate = source.CompletedDate
            }

        End Function


        Public Shared Function CloneVersion(
            source As ManuscriptVersion
        ) As ManuscriptVersion

            If source Is Nothing Then
                Throw New ArgumentNullException(NameOf(source))
            End If

            Return New ManuscriptVersion With {
                .Id = source.Id,
                .RecordedAtUtc = source.RecordedAtUtc,
                .LastModifiedAtUtc = source.LastModifiedAtUtc,
                .CreatedDate = source.CreatedDate,
                .Label = source.Label,
                .Notes = source.Notes,
                .LocalFilePath = source.LocalFilePath,
                .IsManagedCopy = source.IsManagedCopy,
                .SubmissionId = source.SubmissionId,
                .DecisionId = source.DecisionId,
                .RevisionRoundNumber = source.RevisionRoundNumber
            }

        End Function


        Public Shared Function CloneReadiness(
            source As ManuscriptReadiness
        ) As ManuscriptReadiness

            If source Is Nothing Then
                Throw New ArgumentNullException(NameOf(source))
            End If

            Dim clone As New ManuscriptReadiness With {
                .Id = source.Id,
                .JournalId = source.JournalId,
                .JournalName = source.JournalName,
                .Notes = source.Notes,
                .CreatedAtUtc = source.CreatedAtUtc,
                .LastModifiedAtUtc = source.LastModifiedAtUtc
            }

            If source.Items IsNot Nothing Then
                For Each item As ReadinessItemState In source.Items

                    If item Is Nothing Then
                        Continue For
                    End If

                    clone.Items.Add(
                        CloneReadinessItemState(item)
                    )

                Next
            End If

            Return clone

        End Function


        Public Shared Function CloneReadinessItemState(
            source As ReadinessItemState
        ) As ReadinessItemState

            If source Is Nothing Then
                Throw New ArgumentNullException(NameOf(source))
            End If

            Return New ReadinessItemState With {
                .Id = source.Id,
                .TemplateItemId = source.TemplateItemId,
                .Title = source.Title,
                .Description = source.Description,
                .Category = source.Category,
                .SortOrder = source.SortOrder,
                .IsRequired = source.IsRequired,
                .Status = source.Status,
                .UserNotes = source.UserNotes,
                .CompletedAtUtc = source.CompletedAtUtc,
                .LastModifiedAtUtc = source.LastModifiedAtUtc
            }

        End Function


        Public Shared Function CloneSubmissionPacket(
            source As SubmissionPacket
        ) As SubmissionPacket

            If source Is Nothing Then
                Throw New ArgumentNullException(NameOf(source))
            End If

            Dim clone As New SubmissionPacket With {
                .Id = source.Id,
                .ReadinessProfileId = source.ReadinessProfileId,
                .JournalId = source.JournalId,
                .JournalName = source.JournalName,
                .ManuscriptVersionId = source.ManuscriptVersionId,
                .SubmissionId = source.SubmissionId,
                .RevisionRoundNumber = source.RevisionRoundNumber,
                .Label = source.Label,
                .Notes = source.Notes,
                .CreatedAtUtc = source.CreatedAtUtc,
                .LastModifiedAtUtc = source.LastModifiedAtUtc
            }

            If source.Files IsNot Nothing Then
                For Each item As SubmissionPacketFile In source.Files

                    If item Is Nothing Then
                        Continue For
                    End If

                    clone.Files.Add(
                        CloneSubmissionPacketFile(item)
                    )

                Next
            End If

            Return clone

        End Function


        Public Shared Function CloneSubmissionPacketFile(
            source As SubmissionPacketFile
        ) As SubmissionPacketFile

            If source Is Nothing Then
                Throw New ArgumentNullException(NameOf(source))
            End If

            Return New SubmissionPacketFile With {
                .Id = source.Id,
                .Role = source.Role,
                .Label = source.Label,
                .Notes = source.Notes,
                .LocalFilePath = source.LocalFilePath,
                .StorageMode = source.StorageMode,
                .OriginalFileName = source.OriginalFileName,
                .Sha256 = source.Sha256,
                .FileSizeBytes = source.FileSizeBytes,
                .LastWriteTimeUtc = source.LastWriteTimeUtc,
                .HashComputedAtUtc = source.HashComputedAtUtc
            }

        End Function


        Public Shared Function CloneSubmission(
            source As JournalSubmission
        ) As JournalSubmission

            If source Is Nothing Then
                Throw New ArgumentNullException(NameOf(source))
            End If

            Dim clone As New JournalSubmission With {
                .Id = source.Id,
                .RecordedAtUtc = source.RecordedAtUtc,
                .LastModifiedAtUtc = source.LastModifiedAtUtc,
                .JournalName = source.JournalName,
                .JournalId = source.JournalId,
                .ManuscriptNumber = source.ManuscriptNumber,
                .SubmittedDate = source.SubmittedDate,
                .FollowUpDate = source.FollowUpDate,
                .Notes = source.Notes,
                .PortalUrl = source.PortalUrl
            }

            If source.Decisions IsNot Nothing Then
                For Each decisionEvent As EditorialDecisionEvent In source.Decisions
                    clone.Decisions.Add(
                        New EditorialDecisionEvent With {
                            .Id = decisionEvent.Id,
                            .RecordedAtUtc = decisionEvent.RecordedAtUtc,
                            .LastModifiedAtUtc = decisionEvent.LastModifiedAtUtc,
                            .DecisionDate = decisionEvent.DecisionDate,
                            .Decision = decisionEvent.Decision,
                            .RevisionDeadline = decisionEvent.RevisionDeadline,
                            .Notes = decisionEvent.Notes
                        }
                    )
                Next
            End If

            If source.ReviewerResponses IsNot Nothing Then
                For Each item As ReviewerResponseItem In source.ReviewerResponses
                    clone.ReviewerResponses.Add(CloneReviewerResponse(item))
                Next
            End If

            If source.Correspondence IsNot Nothing Then
                For Each item As CorrespondenceItem In source.Correspondence
                    clone.Correspondence.Add(
                        New CorrespondenceItem With {
                            .Id = item.Id,
                            .RecordedAtUtc = item.RecordedAtUtc,
                            .LastModifiedAtUtc = item.LastModifiedAtUtc,
                            .ItemDate = item.ItemDate,
                            .Type = item.Type,
                            .Title = item.Title,
                            .Notes = item.Notes,
                            .LocalFilePath = item.LocalFilePath,
                            .SourceUrl = item.SourceUrl,
                            .IsManagedCopy = item.IsManagedCopy
                        }
                    )
                Next
            End If

            Return clone

        End Function

        Public Shared Function CloneReviewerResponse(
            source As ReviewerResponseItem
        ) As ReviewerResponseItem

            If source Is Nothing Then
                Throw New ArgumentNullException(NameOf(source))
            End If

            Return New ReviewerResponseItem With {
                .Id = source.Id,
                .DecisionId = source.DecisionId,
                .RevisionRoundNumber = source.RevisionRoundNumber,
                .ReviewerLabel = source.ReviewerLabel,
                .CommentText = source.CommentText,
                .ActionText = source.ActionText,
                .ResponseText = source.ResponseText,
                .ManuscriptLocation = source.ManuscriptLocation,
                .Notes = source.Notes,
                .Status = source.Status,
                .CreatedAtUtc = source.CreatedAtUtc,
                .LastModifiedAtUtc = source.LastModifiedAtUtc
            }

        End Function

    End Class

End Namespace
