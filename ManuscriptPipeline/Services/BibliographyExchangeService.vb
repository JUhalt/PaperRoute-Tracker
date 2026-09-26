Imports System
Imports System.Collections.Generic
Imports System.Linq
Imports ManuscriptPipeline.Models

Namespace Services

    Public NotInheritable Class BibliographyExchangeService

        Private Sub New()
        End Sub

        Public Shared Function DetectFormat(
            fileName As String,
            content As String
        ) As BibliographyFormat

            Dim extension As String =
                IO.Path.GetExtension(If(fileName, String.Empty))

            If String.Equals(
                extension,
                ".bib",
                StringComparison.OrdinalIgnoreCase
            ) Then
                Return BibliographyFormat.BibTeX
            End If

            If String.Equals(
                extension,
                ".ris",
                StringComparison.OrdinalIgnoreCase
            ) Then
                Return BibliographyFormat.Ris
            End If

            Dim sample As String =
                If(content, String.Empty).TrimStart()

            If sample.StartsWith("@", StringComparison.Ordinal) Then
                Return BibliographyFormat.BibTeX
            End If

            If sample.StartsWith(
                "TY  -",
                StringComparison.OrdinalIgnoreCase
            ) OrElse
               sample.Contains(
                   Environment.NewLine & "TY  -"
               ) Then
                Return BibliographyFormat.Ris
            End If

            Throw New InvalidOperationException(
                "PaperRoute could not determine whether this file is BibTeX or RIS."
            )
        End Function

        Public Shared Function Parse(
            fileName As String,
            content As String
        ) As BibliographyParseResult

            Dim format As BibliographyFormat =
                DetectFormat(fileName, content)

            Select Case format
                Case BibliographyFormat.BibTeX
                    Return BibTeXService.Parse(content)
                Case BibliographyFormat.Ris
                    Return RisService.Parse(content)
                Case Else
                    Throw New InvalidOperationException(
                        "Unsupported bibliography format."
                    )
            End Select
        End Function

        Public Shared Function Apply(
            records As IEnumerable(Of BibliographyRecord),
            manuscripts As List(Of Manuscript),
            authorLibrary As AuthorLibraryData,
            options As BibliographyImportOptions
        ) As BibliographyImportResult

            If records Is Nothing Then
                Throw New ArgumentNullException(NameOf(records))
            End If

            If manuscripts Is Nothing Then
                Throw New ArgumentNullException(NameOf(manuscripts))
            End If

            If authorLibrary Is Nothing Then
                Throw New ArgumentNullException(NameOf(authorLibrary))
            End If

            If options Is Nothing Then
                Throw New ArgumentNullException(NameOf(options))
            End If

            If authorLibrary.Authors Is Nothing Then
                authorLibrary.Authors = New List(Of AuthorRecord)()
            End If

            Dim result As New BibliographyImportResult()

            For Each record As BibliographyRecord In records
                If record Is Nothing Then
                    Continue For
                End If

                result.WarningCount += record.Warnings.Count

                Dim duplicateReason As String =
                    FindDuplicateReason(record, manuscripts)

                If Not String.IsNullOrWhiteSpace(duplicateReason) Then
                    result.DuplicateCount += 1
                    Continue For
                End If

                Dim manuscript As Manuscript =
                    CreateManuscript(
                        record,
                        authorLibrary,
                        options,
                        result
                    )

                manuscripts.Add(manuscript)
                result.ImportedCount += 1
            Next

            Return result
        End Function

        Public Shared Function FindDuplicateReason(
            record As BibliographyRecord,
            manuscripts As IEnumerable(Of Manuscript)
        ) As String

            If record Is Nothing OrElse manuscripts Is Nothing Then
                Return String.Empty
            End If

            Dim candidateDoi As String =
                DoiNormalizer.Normalize(record.Doi)

            If Not String.IsNullOrWhiteSpace(candidateDoi) Then
                For Each manuscript As Manuscript In manuscripts
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
                        candidateDoi,
                        StringComparison.OrdinalIgnoreCase
                    ) Then
                        Return "DOI already exists in PaperRoute."
                    End If
                Next
            End If

            Dim candidateTitle As String =
                BibliographyTextService.NormalizeName(record.Title)

            If String.IsNullOrWhiteSpace(candidateTitle) Then
                Return String.Empty
            End If

            For Each manuscript As Manuscript In manuscripts
                If manuscript Is Nothing Then
                    Continue For
                End If

                If String.Equals(
                    BibliographyTextService.NormalizeName(
                        manuscript.Title
                    ),
                    candidateTitle,
                    StringComparison.Ordinal
                ) Then
                    Return "Title already exists in PaperRoute."
                End If
            Next

            Return String.Empty
        End Function

        Private Shared Function CreateManuscript(
            record As BibliographyRecord,
            authorLibrary As AuthorLibraryData,
            options As BibliographyImportOptions,
            result As BibliographyImportResult
        ) As Manuscript

            Dim metadata As New ManuscriptMetadata With {
                .AbstractText = If(record.AbstractText, String.Empty).Trim(),
                .Keywords =
                    If(record.Keywords, New List(Of String)()).
                        Where(
                            Function(item)
                                Return Not String.IsNullOrWhiteSpace(item)
                            End Function
                        ).
                        Select(Function(item) item.Trim()).
                        Distinct(StringComparer.OrdinalIgnoreCase).
                        ToList(),
                .Doi = DoiNormalizer.Normalize(record.Doi),
                .PublicationJournal = If(record.Journal, String.Empty).Trim(),
                .PublishedDate = record.PublishedDate,
                .Volume = If(record.Volume, String.Empty).Trim(),
                .Issue = If(record.Issue, String.Empty).Trim(),
                .Pages = If(record.Pages, String.Empty).Trim(),
                .Publisher = If(record.Publisher, String.Empty).Trim(),
                .PublicationUrl = If(record.Url, String.Empty).Trim()
            }

            metadata.ExternalIdentifiers(
                "bibliography:source-format"
            ) =
                If(
                    record.SourceFormat = BibliographyFormat.BibTeX,
                    "bibtex",
                    "ris"
                )

            If record.SourceFormat = BibliographyFormat.BibTeX AndAlso
               Not String.IsNullOrWhiteSpace(record.SourceKey) Then
                metadata.ExternalIdentifiers(
                    "bibtex:citation-key"
                ) = record.SourceKey.Trim()
            End If

            If record.SourceFormat = BibliographyFormat.Ris AndAlso
               Not String.IsNullOrWhiteSpace(record.SourceType) Then
                metadata.ExternalIdentifiers(
                    "ris:type"
                ) = record.SourceType.Trim()
            End If

            Dim importAsPublished As Boolean =
                options.ImportPublishedRecordsAsPublished AndAlso
                record.LooksPublished

            Dim manuscript As New Manuscript With {
                .Title =
                    If(
                        String.IsNullOrWhiteSpace(record.Title),
                        "(Untitled imported record)",
                        record.Title.Trim()
                    ),
                .Metadata = metadata,
                .CurrentStage =
                    If(
                        importAsPublished,
                        PaperStage.Published,
                        PaperStage.Idea
                    ),
                .Location =
                    If(
                        importAsPublished,
                        ManuscriptLocation.Published,
                        ManuscriptLocation.Pipeline
                    ),
                .StageEnteredDate =
                    If(
                        importAsPublished AndAlso
                        record.PublishedDate.HasValue,
                        record.PublishedDate.Value,
                        DateTime.Now
                    )
            }

            For Each parsedAuthor As BibliographyAuthor In record.Authors
                If parsedAuthor Is Nothing OrElse
                   String.IsNullOrWhiteSpace(parsedAuthor.DisplayName) Then
                    Continue For
                End If

                Dim author As AuthorRecord =
                    FindMatchingAuthor(
                        parsedAuthor,
                        authorLibrary
                    )

                If author Is Nothing Then
                    author =
                        New AuthorRecord With {
                            .GivenName = parsedAuthor.GivenName.Trim(),
                            .MiddleName = parsedAuthor.MiddleName.Trim(),
                            .FamilyName = parsedAuthor.FamilyName.Trim(),
                            .Suffix = parsedAuthor.Suffix.Trim(),
                            .DisplayNameOverride = parsedAuthor.DisplayNameOverride.Trim()
                        }

                    authorLibrary.Authors.Add(author)
                    result.AuthorsCreated += 1
                End If

                If manuscript.Authors.Any(
                    Function(link)
                        Return link.AuthorId = author.Id
                    End Function
                ) Then
                    Continue For
                End If

                manuscript.Authors.Add(
                    New ManuscriptAuthor With {
                        .AuthorId = author.Id,
                        .AffiliationIds = New List(Of Guid)(),
                        .IsCorrespondingAuthor = False
                    }
                )
            Next

            Return manuscript
        End Function

        Friend Shared Function FindMatchingAuthor(
            parsedAuthor As BibliographyAuthor,
            library As AuthorLibraryData
        ) As AuthorRecord

            Dim candidateName As String =
                BibliographyTextService.NormalizeName(
                    parsedAuthor.DisplayName
                )

            If String.IsNullOrWhiteSpace(candidateName) Then
                Return Nothing
            End If

            Return library.Authors.
                FirstOrDefault(
                    Function(item)
                        Return item IsNot Nothing AndAlso
                            String.Equals(
                                BibliographyTextService.NormalizeName(
                                    item.DisplayName
                                ),
                                candidateName,
                                StringComparison.Ordinal
                            )
                End Function
            )
        End Function

    End Class

End Namespace
