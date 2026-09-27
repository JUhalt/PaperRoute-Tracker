Imports System
Imports System.Collections.Generic
Imports System.Linq
Imports ManuscriptPipeline.Models

Namespace Services

    ' Applies a reviewed title-page proposal to a manuscript. Authors and
    ' affiliations are matched to the reusable library with the same rules as
    ' bibliography and Crossref imports; unmatched ones become new library
    ' records. Abstract and keywords only fill metadata that is still empty.
    Public NotInheritable Class TitlePageApplyService

        Private Sub New()
        End Sub


        Public Shared Function FindLibraryAuthor(
            name As BibliographyAuthor,
            library As AuthorLibraryData
        ) As AuthorRecord

            If name Is Nothing OrElse
               library Is Nothing OrElse
               library.Authors Is Nothing Then
                Return Nothing
            End If

            Return BibliographyExchangeService.FindMatchingAuthor(name, library)

        End Function


        Public Shared Function Apply(
            proposal As TitlePageParseResult,
            manuscript As Manuscript,
            library As AuthorLibraryData
        ) As TitlePageApplyResult

            If proposal Is Nothing Then
                Throw New ArgumentNullException(NameOf(proposal))
            End If

            If manuscript Is Nothing Then
                Throw New ArgumentNullException(NameOf(manuscript))
            End If

            If library Is Nothing Then
                Throw New ArgumentNullException(NameOf(library))
            End If

            If library.Authors Is Nothing Then
                library.Authors = New List(Of AuthorRecord)()
            End If

            If library.Affiliations Is Nothing Then
                library.Affiliations = New List(Of AffiliationRecord)()
            End If

            If manuscript.Metadata Is Nothing Then
                manuscript.Metadata = New ManuscriptMetadata()
            End If

            Dim result As New TitlePageApplyResult()

            ApplyMetadata(proposal, manuscript.Metadata)

            For Each parsed As TitlePageAuthor In proposal.Authors
                If parsed Is Nothing OrElse
                   parsed.Name Is Nothing OrElse
                   String.IsNullOrWhiteSpace(parsed.Name.DisplayName) Then
                    Continue For
                End If

                Dim author As AuthorRecord = FindLibraryAuthor(parsed.Name, library)

                If author Is Nothing Then
                    author = New AuthorRecord With {
                        .GivenName = parsed.Name.GivenName.Trim(),
                        .MiddleName = parsed.Name.MiddleName.Trim(),
                        .FamilyName = parsed.Name.FamilyName.Trim(),
                        .Suffix = parsed.Name.Suffix.Trim(),
                        .DisplayNameOverride = parsed.Name.DisplayNameOverride.Trim()
                    }
                    library.Authors.Add(author)
                    result.AuthorsCreated += 1
                Else
                    result.AuthorsMatched += 1
                End If

                Dim authorId As Guid = author.Id

                If manuscript.Authors.Any(Function(link) link.AuthorId = authorId) Then
                    Continue For
                End If

                manuscript.Authors.Add(
                    New ManuscriptAuthor With {
                        .AuthorId = authorId,
                        .AffiliationIds = ResolveAffiliations(parsed.Affiliations, library, result),
                        .IsCorrespondingAuthor = parsed.IsCorrespondingAuthor
                    })
            Next

            Return result

        End Function


        Private Shared Sub ApplyMetadata(
            proposal As TitlePageParseResult,
            metadata As ManuscriptMetadata
        )

            Dim abstractText As String = If(proposal.AbstractText, String.Empty).Trim()

            If String.IsNullOrWhiteSpace(metadata.AbstractText) AndAlso abstractText.Length > 0 Then
                metadata.AbstractText = abstractText
            End If

            If metadata.Keywords Is Nothing Then
                metadata.Keywords = New List(Of String)()
            End If

            For Each keyword As String In proposal.Keywords
                Dim cleaned As String = If(keyword, String.Empty).Trim()

                If cleaned.Length > 0 AndAlso
                   Not metadata.Keywords.Contains(cleaned, StringComparer.OrdinalIgnoreCase) Then
                    metadata.Keywords.Add(cleaned)
                End If
            Next

        End Sub


        Private Shared Function ResolveAffiliations(
            names As IEnumerable(Of String),
            library As AuthorLibraryData,
            result As TitlePageApplyResult
        ) As List(Of Guid)

            Dim ids As New List(Of Guid)()

            For Each name As String In If(names, Enumerable.Empty(Of String)())
                Dim cleaned As String = BibliographyTextService.CollapseWhitespace(name)

                If cleaned.Length = 0 Then
                    Continue For
                End If

                Dim affiliation As AffiliationRecord =
                    CrossrefApplyService.FindMatchingAffiliation(cleaned, library)

                If affiliation Is Nothing Then
                    affiliation = New AffiliationRecord With {.Institution = cleaned}
                    library.Affiliations.Add(affiliation)
                    result.AffiliationsCreated += 1
                End If

                If Not ids.Contains(affiliation.Id) Then
                    ids.Add(affiliation.Id)
                End If
            Next

            Return ids

        End Function

    End Class

End Namespace
