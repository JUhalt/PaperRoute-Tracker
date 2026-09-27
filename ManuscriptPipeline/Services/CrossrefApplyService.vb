Imports System
Imports System.Collections.Generic
Imports System.Linq
Imports ManuscriptPipeline.Models

Namespace Services

    Public NotInheritable Class CrossrefApplyService

        Private Sub New()
        End Sub


        Public Shared Function Apply(
            suggestion As CrossrefMetadataSuggestion,
            manuscript As Manuscript,
            authorLibrary As AuthorLibraryData,
            options As CrossrefApplyOptions
        ) As CrossrefApplyResult

            If suggestion Is Nothing Then
                Throw New ArgumentNullException(NameOf(suggestion))
            End If

            If manuscript Is Nothing Then
                Throw New ArgumentNullException(NameOf(manuscript))
            End If

            If authorLibrary Is Nothing Then
                Throw New ArgumentNullException(NameOf(authorLibrary))
            End If

            If options Is Nothing Then
                Throw New ArgumentNullException(NameOf(options))
            End If

            If manuscript.Metadata Is Nothing Then
                manuscript.Metadata = New ManuscriptMetadata()
            End If

            If manuscript.Authors Is Nothing Then
                manuscript.Authors = New List(Of ManuscriptAuthor)()
            End If

            If authorLibrary.Authors Is Nothing Then
                authorLibrary.Authors = New List(Of AuthorRecord)()
            End If

            If authorLibrary.Affiliations Is Nothing Then
                authorLibrary.Affiliations = New List(Of AffiliationRecord)()
            End If

            Dim result As New CrossrefApplyResult()

            If options.ApplyDoi Then

                manuscript.Metadata.Doi =
                    DoiNormalizer.Normalize(
                        suggestion.Doi
                    )

            End If

            If options.ApplyTitle AndAlso
               Not String.IsNullOrWhiteSpace(
                    suggestion.Title
               ) Then

                manuscript.Title =
                    suggestion.Title.Trim()

            End If

            If options.ApplyPublicationDetails Then

                manuscript.Metadata.PublicationJournal =
                    PreferSuggested(
                        suggestion.Journal,
                        manuscript.Metadata.PublicationJournal
                    )

                manuscript.Metadata.Publisher =
                    PreferSuggested(
                        suggestion.Publisher,
                        manuscript.Metadata.Publisher
                    )

                If suggestion.PublishedDate.HasValue Then

                    manuscript.Metadata.PublishedDate =
                        suggestion.PublishedDate

                End If

                manuscript.Metadata.Volume =
                    PreferSuggested(
                        suggestion.Volume,
                        manuscript.Metadata.Volume
                    )

                manuscript.Metadata.Issue =
                    PreferSuggested(
                        suggestion.Issue,
                        manuscript.Metadata.Issue
                    )

                manuscript.Metadata.Pages =
                    PreferSuggested(
                        suggestion.Pages,
                        manuscript.Metadata.Pages
                    )

                manuscript.Metadata.PublicationUrl =
                    PreferSuggested(
                        suggestion.Url,
                        manuscript.Metadata.PublicationUrl
                    )

            End If

            If options.ApplyAbstractAndKeywords Then

                If Not String.IsNullOrWhiteSpace(
                    suggestion.AbstractText
                ) Then

                    manuscript.Metadata.AbstractText =
                        suggestion.AbstractText.Trim()

                End If

                Dim keywords As List(Of String) =
                    suggestion.Keywords.
                        Where(
                            Function(item)
                                Return Not String.IsNullOrWhiteSpace(item)
                            End Function
                        ).
                        Select(
                            Function(item)
                                Return item.Trim()
                            End Function
                        ).
                        Distinct(
                            StringComparer.OrdinalIgnoreCase
                        ).
                        ToList()

                If keywords.Count > 0 Then

                    manuscript.Metadata.Keywords =
                        keywords

                End If

            End If

            If suggestion.Keywords Is Nothing Then
                suggestion.Keywords = New List(Of String)()
            End If

            If suggestion.Authors Is Nothing Then
                suggestion.Authors = New List(Of CrossrefAuthorSuggestion)()
            End If

            If options.AddMissingAuthors Then

                ApplyAuthors(
                    suggestion,
                    manuscript,
                    authorLibrary,
                    result
                )

            End If

            RecordProvenance(
                manuscript.Metadata,
                suggestion
            )

            Return result

        End Function


        Private Shared Sub ApplyAuthors(
            suggestion As CrossrefMetadataSuggestion,
            manuscript As Manuscript,
            authorLibrary As AuthorLibraryData,
            result As CrossrefApplyResult
        )

            Dim assignedIds As New HashSet(Of Guid)(
                manuscript.Authors.
                    Select(
                        Function(item)
                            Return item.AuthorId
                        End Function
                    )
            )

            For Each suggestedAuthor As CrossrefAuthorSuggestion In
                suggestion.Authors

                Dim author As AuthorRecord =
                    FindMatchingAuthor(
                        suggestedAuthor,
                        authorLibrary
                    )

                If author Is Nothing Then

                    author =
                        New AuthorRecord With {
                            .GivenName = SafeTrim(suggestedAuthor.GivenName),
                            .FamilyName = SafeTrim(suggestedAuthor.FamilyName),
                            .Orcid = NormalizeOrcid(suggestedAuthor.Orcid)
                        }

                    authorLibrary.Authors.Add(
                        author
                    )

                    result.AuthorLibraryChanged =
                        True

                    result.AuthorsCreatedInLibrary +=
                        1

                End If

                If suggestedAuthor.Affiliations Is Nothing Then

                    suggestedAuthor.Affiliations =
                        New List(Of String)()

                End If

                Dim affiliationIds As New List(Of Guid)()

                For Each affiliationName As String In
                    suggestedAuthor.Affiliations

                    If String.IsNullOrWhiteSpace(
                        affiliationName
                    ) Then

                        Continue For

                    End If

                    Dim affiliation As AffiliationRecord =
                        FindMatchingAffiliation(
                            affiliationName,
                            authorLibrary
                        )

                    If affiliation Is Nothing Then

                        affiliation =
                            New AffiliationRecord With {
                                .Institution =
                                    affiliationName.Trim()
                            }

                        authorLibrary.Affiliations.Add(
                            affiliation
                        )

                        result.AuthorLibraryChanged =
                            True

                        result.AffiliationsCreatedInLibrary +=
                            1

                    End If

                    If Not affiliationIds.Contains(
                        affiliation.Id
                    ) Then

                        affiliationIds.Add(
                            affiliation.Id
                        )

                    End If

                Next

                Dim existingLink As ManuscriptAuthor =
                    manuscript.Authors.
                        FirstOrDefault(
                            Function(item)
                                Return item IsNot Nothing AndAlso
                                    item.AuthorId = author.Id
                            End Function
                        )

                If existingLink IsNot Nothing Then

                    If existingLink.AffiliationIds Is Nothing Then

                        existingLink.AffiliationIds =
                            New List(Of Guid)()

                    End If

                    For Each affiliationId As Guid In
                        affiliationIds

                        If Not existingLink.AffiliationIds.Contains(
                            affiliationId
                        ) Then

                            existingLink.AffiliationIds.Add(
                                affiliationId
                            )

                        End If

                    Next

                    Continue For

                End If

                manuscript.Authors.Add(
                    New ManuscriptAuthor With {
                        .AuthorId = author.Id,
                        .AffiliationIds = affiliationIds,
                        .IsCorrespondingAuthor = False
                    }
                )

                assignedIds.Add(
                    author.Id
                )

                result.AuthorsAddedToManuscript +=
                    1

            Next

        End Sub


        Private Shared Function FindMatchingAuthor(
            suggestion As CrossrefAuthorSuggestion,
            library As AuthorLibraryData
        ) As AuthorRecord

            Dim normalizedOrcid As String =
                NormalizeOrcid(
                    suggestion.Orcid
                )

            If Not String.IsNullOrWhiteSpace(
                normalizedOrcid
            ) Then

                Dim orcidMatch As AuthorRecord =
                    library.Authors.
                        FirstOrDefault(
                            Function(item)
                                Return String.Equals(
                                    NormalizeOrcid(item.Orcid),
                                    normalizedOrcid,
                                    StringComparison.OrdinalIgnoreCase
                                )
                            End Function
                        )

                If orcidMatch IsNot Nothing Then
                    Return orcidMatch
                End If

            End If

            Dim suggestedName As String =
                NormalizeName(
                    suggestion.DisplayName
                )

            If String.IsNullOrWhiteSpace(
                suggestedName
            ) Then

                Return Nothing

            End If

            Return library.Authors.
                FirstOrDefault(
                    Function(item)
                        Return String.Equals(
                            NormalizeName(item.DisplayName),
                            suggestedName,
                            StringComparison.OrdinalIgnoreCase
                        )
                    End Function
                )

        End Function


        Friend Shared Function FindMatchingAffiliation(
            affiliationName As String,
            library As AuthorLibraryData
        ) As AffiliationRecord

            Dim normalized As String =
                NormalizeName(
                    affiliationName
                )

            Return library.Affiliations.
                FirstOrDefault(
                    Function(item)
                        Return String.Equals(
                            NormalizeName(item.Institution),
                            normalized,
                            StringComparison.OrdinalIgnoreCase
                        ) OrElse
                               String.Equals(
                            NormalizeName(item.DisplayName),
                            normalized,
                            StringComparison.OrdinalIgnoreCase
                        )
                    End Function
                )

        End Function


        Friend Shared Function NormalizeOrcid(
            value As String
        ) As String

            If String.IsNullOrWhiteSpace(value) Then
                Return String.Empty
            End If

            Dim normalized As String =
                value.Trim()

            If normalized.StartsWith(
                "https://orcid.org/",
                StringComparison.OrdinalIgnoreCase
            ) Then

                normalized =
                    normalized.Substring(
                        "https://orcid.org/".Length
                    )

            ElseIf normalized.StartsWith(
                "http://orcid.org/",
                StringComparison.OrdinalIgnoreCase
            ) Then

                normalized =
                    normalized.Substring(
                        "http://orcid.org/".Length
                    )

            End If

            Return normalized.Trim()

        End Function


        Private Shared Function NormalizeName(
            value As String
        ) As String

            If String.IsNullOrWhiteSpace(value) Then
                Return String.Empty
            End If

            Return String.Join(
                " ",
                value.Trim().
                    Split(
                        New Char() {
                            " "c,
                            ControlChars.Tab
                        },
                        StringSplitOptions.RemoveEmptyEntries
                    )
            )

        End Function


        Private Shared Function PreferSuggested(
            suggestedValue As String,
            currentValue As String
        ) As String

            If String.IsNullOrWhiteSpace(
                suggestedValue
            ) Then

                Return SafeTrim(
                    currentValue
                )

            End If

            Return suggestedValue.Trim()

        End Function


        Private Shared Function SafeTrim(
            value As String
        ) As String

            Return If(
                value,
                String.Empty
            ).Trim()

        End Function


        Private Shared Sub RecordProvenance(
            metadata As ManuscriptMetadata,
            suggestion As CrossrefMetadataSuggestion
        )

            If metadata.ExternalIdentifiers Is Nothing Then

                metadata.ExternalIdentifiers =
                    New Dictionary(Of String, String)()

            End If

            metadata.ExternalIdentifiers(
                "crossref:source-doi"
            ) =
                DoiNormalizer.Normalize(
                    suggestion.Doi
                )

            metadata.ExternalIdentifiers(
                "crossref:last-applied-utc"
            ) =
                DateTime.UtcNow.ToString(
                    "O"
                )

        End Sub

    End Class

End Namespace
