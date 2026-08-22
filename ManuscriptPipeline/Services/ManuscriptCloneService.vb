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

            If source.History IsNot Nothing Then
                For Each historyEvent As HistoryEvent In source.History

                    clone.History.Add(
                        New HistoryEvent With {
                            .Id = historyEvent.Id,
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


        Public Shared Function CloneSubmission(
            source As JournalSubmission
        ) As JournalSubmission

            If source Is Nothing Then
                Throw New ArgumentNullException(NameOf(source))
            End If

            Dim clone As New JournalSubmission With {
                .Id = source.Id,
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
                            .DecisionDate = decisionEvent.DecisionDate,
                            .Decision = decisionEvent.Decision,
                            .RevisionDeadline = decisionEvent.RevisionDeadline,
                            .Notes = decisionEvent.Notes
                        }
                    )
                Next
            End If

            If source.Correspondence IsNot Nothing Then
                For Each item As CorrespondenceItem In source.Correspondence
                    clone.Correspondence.Add(
                        New CorrespondenceItem With {
                            .Id = item.Id,
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

    End Class

End Namespace
