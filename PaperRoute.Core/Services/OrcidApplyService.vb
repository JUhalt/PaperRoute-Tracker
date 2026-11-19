Imports System
Imports System.Collections.Generic
Imports System.Linq
Imports ManuscriptPipeline.Models

Namespace Services

    Public NotInheritable Class OrcidApplyService

        Private Sub New()
        End Sub


        Public Shared Function Apply(
            author As AuthorRecord,
            library As AuthorLibraryData,
            manuscripts As List(Of Manuscript),
            suggestion As OrcidProfileSuggestion,
            options As OrcidApplyOptions
        ) As OrcidApplyResult

            If author Is Nothing Then
                Throw New ArgumentNullException(NameOf(author))
            End If

            If library Is Nothing Then
                Throw New ArgumentNullException(NameOf(library))
            End If

            If manuscripts Is Nothing Then
                Throw New ArgumentNullException(NameOf(manuscripts))
            End If

            If suggestion Is Nothing Then
                Throw New ArgumentNullException(NameOf(suggestion))
            End If

            If options Is Nothing Then
                Throw New ArgumentNullException(NameOf(options))
            End If

            Dim normalizedOrcid As String =
                OrcidIdentifierService.NormalizeAndValidate(
                    suggestion.Orcid
                )

            Dim result As New OrcidApplyResult()

            author.Orcid =
                normalizedOrcid

            author.OrcidLastCheckedUtc =
                DateTime.UtcNow

            result.AuthorUpdated =
                True

            If options.ApplyName Then

                If Not String.IsNullOrWhiteSpace(
                    suggestion.GivenName
                ) Then

                    author.GivenName =
                        suggestion.GivenName.Trim()

                End If

                If Not String.IsNullOrWhiteSpace(
                    suggestion.FamilyName
                ) Then

                    author.FamilyName =
                        suggestion.FamilyName.Trim()

                End If

            End If

            If options.ApplyCreditName AndAlso
               Not String.IsNullOrWhiteSpace(
                   suggestion.CreditName
               ) Then

                author.DisplayNameOverride =
                    suggestion.CreditName.Trim()

            End If

            If options.AddAffiliations Then

                For Each affiliation As OrcidAffiliationSuggestion In
                    suggestion.Affiliations

                    If affiliation Is Nothing OrElse
                       String.IsNullOrWhiteSpace(
                           affiliation.Institution
                       ) Then

                        Continue For

                    End If

                    If FindMatchingAffiliation(
                        library.Affiliations,
                        affiliation
                    ) Is Nothing Then

                        library.Affiliations.Add(
                            New AffiliationRecord With {
                                .Institution = affiliation.Institution.Trim(),
                                .Department = affiliation.Department.Trim(),
                                .City = affiliation.City.Trim(),
                                .Region = affiliation.Region.Trim(),
                                .Country = affiliation.Country.Trim(),
                                .Notes = BuildAffiliationNote(affiliation)
                            }
                        )

                        result.AffiliationsAdded +=
                            1

                    End If

                Next

            End If

            Dim selected As New HashSet(Of Long)(
                options.SelectedWorkPutCodes
            )

            For Each work As OrcidWorkSuggestion In
                suggestion.Works

                If work Is Nothing OrElse
                   Not selected.Contains(
                       work.PutCode
                   ) Then

                    Continue For

                End If

                If IsDuplicateWork(
                    manuscripts,
                    work
                ) Then

                    result.DuplicateWorksSkipped +=
                        1

                    Continue For

                End If

                manuscripts.Add(
                    CreateManuscriptFromWork(
                        author,
                        suggestion,
                        work,
                        options.ImportDatedWorksAsPublished
                    )
                )

                result.ManuscriptsImported +=
                    1

            Next

            Return result

        End Function


        Private Shared Function FindMatchingAffiliation(
            affiliations As IEnumerable(Of AffiliationRecord),
            candidate As OrcidAffiliationSuggestion
        ) As AffiliationRecord

            Return affiliations.FirstOrDefault(
                Function(existing)

                    If existing Is Nothing Then
                        Return False
                    End If

                    Return Same(
                        existing.Institution,
                        candidate.Institution
                    ) AndAlso
                    Same(
                        existing.Department,
                        candidate.Department
                    ) AndAlso
                    Same(
                        existing.City,
                        candidate.City
                    ) AndAlso
                    Same(
                        existing.Region,
                        candidate.Region
                    ) AndAlso
                    Same(
                        existing.Country,
                        candidate.Country
                    )

                End Function
            )

        End Function


        Private Shared Function Same(
            leftValue As String,
            rightValue As String
        ) As Boolean

            Return String.Equals(
                If(leftValue, String.Empty).Trim(),
                If(rightValue, String.Empty).Trim(),
                StringComparison.OrdinalIgnoreCase
            )

        End Function


        Private Shared Function BuildAffiliationNote(
            affiliation As OrcidAffiliationSuggestion
        ) As String

            Dim parts As New List(Of String) From {
                "Imported from public ORCID record."
            }

            If Not String.IsNullOrWhiteSpace(
                affiliation.RoleTitle
            ) Then

                parts.Add(
                    "Role: " &
                    affiliation.RoleTitle.Trim() &
                    "."
                )

            End If

            If affiliation.StartDate.HasValue Then

                parts.Add(
                    "Start: " &
                    affiliation.StartDate.Value.ToString(
                        "yyyy-MM-dd"
                    ) &
                    "."
                )

            End If

            If affiliation.EndDate.HasValue Then

                parts.Add(
                    "End: " &
                    affiliation.EndDate.Value.ToString(
                        "yyyy-MM-dd"
                    ) &
                    "."
                )

            End If

            Return String.Join(
                " ",
                parts
            )

        End Function


        Private Shared Function IsDuplicateWork(
            manuscripts As IEnumerable(Of Manuscript),
            work As OrcidWorkSuggestion
        ) As Boolean

            Dim normalizedDoi As String =
                DoiNormalizer.Normalize(
                    work.Doi
                )

            If Not String.IsNullOrWhiteSpace(
                normalizedDoi
            ) Then

                For Each manuscript As Manuscript In
                    manuscripts

                    If manuscript Is Nothing OrElse
                       manuscript.Metadata Is Nothing Then

                        Continue For

                    End If

                    Dim existingDoi As String =
                        DoiNormalizer.Normalize(
                            manuscript.Metadata.Doi
                        )

                    If String.Equals(
                        existingDoi,
                        normalizedDoi,
                        StringComparison.OrdinalIgnoreCase
                    ) Then

                        Return True

                    End If

                Next

            End If

            Dim candidateTitle As String =
                If(
                    work.Title,
                    String.Empty
                ).Trim()

            If String.IsNullOrWhiteSpace(
                candidateTitle
            ) Then

                Return False

            End If

            Return manuscripts.Any(
                Function(manuscript)
                    Return manuscript IsNot Nothing AndAlso
                        String.Equals(
                            If(
                                manuscript.Title,
                                String.Empty
                            ).Trim(),
                            candidateTitle,
                            StringComparison.CurrentCultureIgnoreCase
                        )
                End Function
            )

        End Function


        Private Shared Function CreateManuscriptFromWork(
            author As AuthorRecord,
            profile As OrcidProfileSuggestion,
            work As OrcidWorkSuggestion,
            importDatedWorkAsPublished As Boolean
        ) As Manuscript

            Dim doi As String =
                DoiNormalizer.Normalize(
                    work.Doi
                )

            Dim metadata As New ManuscriptMetadata With {
                .Doi = doi,
                .PublicationJournal = work.JournalTitle,
                .PublishedDate = work.PublishedDate,
                .PublicationUrl =
                    If(
                        String.IsNullOrWhiteSpace(doi),
                        String.Empty,
                        "https://doi.org/" & doi
                    )
            }

            metadata.ExternalIdentifiers(
                "orcid:source-author"
            ) =
                profile.Orcid

            metadata.ExternalIdentifiers(
                "orcid:work-put-code"
            ) =
                work.PutCode.ToString()

            If Not String.IsNullOrWhiteSpace(
                work.WorkType
            ) Then

                metadata.ExternalIdentifiers(
                    "orcid:work-type"
                ) =
                    work.WorkType

            End If

            Dim shouldImportAsPublished As Boolean =
                importDatedWorkAsPublished AndAlso
                work.PublishedDate.HasValue

            Dim manuscript As New Manuscript With {
                .WorkType = WorkTypeService.FromRegistry(work.WorkType),
                .Title = work.Title.Trim(),
                .Metadata = metadata,
                .CurrentStage =
                    If(
                        shouldImportAsPublished,
                        PaperStage.Published,
                        PaperStage.Idea
                    ),
                .Location =
                    If(
                        shouldImportAsPublished,
                        ManuscriptLocation.Published,
                        ManuscriptLocation.Pipeline
                    ),
                .StageEnteredDate =
                    If(
                        shouldImportAsPublished,
                        work.PublishedDate.Value,
                        DateTime.Now
                    )
            }

            manuscript.Authors.Add(
                New ManuscriptAuthor With {
                    .AuthorId = author.Id,
                    .AffiliationIds = New List(Of Guid)(),
                    .IsCorrespondingAuthor = False
                }
            )

            Return manuscript

        End Function

    End Class

End Namespace
