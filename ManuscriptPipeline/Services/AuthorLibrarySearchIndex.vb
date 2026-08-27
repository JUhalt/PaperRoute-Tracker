Imports System
Imports System.Collections.Generic
Imports ManuscriptPipeline.Models

Namespace Services

    Public NotInheritable Class AuthorLibrarySearchIndex

        Private ReadOnly _authorsById As New Dictionary(
            Of Guid,
            AuthorRecord
        )()

        Private ReadOnly _affiliationsById As New Dictionary(
            Of Guid,
            AffiliationRecord
        )()


        Public Sub New(
            library As AuthorLibraryData
        )

            If library Is Nothing Then
                Return
            End If

            If library.Authors IsNot Nothing Then

                For Each author As AuthorRecord In
                    library.Authors

                    If author Is Nothing OrElse
                       author.Id = Guid.Empty Then

                        Continue For

                    End If

                    _authorsById(author.Id) =
                        author

                Next

            End If

            If library.Affiliations IsNot Nothing Then

                For Each affiliation As AffiliationRecord In
                    library.Affiliations

                    If affiliation Is Nothing OrElse
                       affiliation.Id = Guid.Empty Then

                        Continue For

                    End If

                    _affiliationsById(affiliation.Id) =
                        affiliation

                Next

            End If

        End Sub


        Public ReadOnly Property AuthorCount As Integer
            Get
                Return _authorsById.Count
            End Get
        End Property


        Public ReadOnly Property AffiliationCount As Integer
            Get
                Return _affiliationsById.Count
            End Get
        End Property


        Public Function BuildSearchText(
            manuscript As Manuscript
        ) As String

            If manuscript Is Nothing OrElse
               manuscript.Authors Is Nothing OrElse
               manuscript.Authors.Count = 0 Then

                Return String.Empty

            End If

            Dim parts As New List(Of String)()

            For Each authorLink As ManuscriptAuthor In
                manuscript.Authors

                If authorLink Is Nothing Then
                    Continue For
                End If

                Dim author As AuthorRecord =
                    Nothing

                If _authorsById.TryGetValue(
                    authorLink.AuthorId,
                    author
                ) Then

                    parts.Add(
                        author.DisplayName
                    )

                    If Not String.IsNullOrWhiteSpace(
                        author.Orcid
                    ) Then

                        parts.Add(
                            author.Orcid.Trim()
                        )

                    End If

                End If

                If authorLink.AffiliationIds Is Nothing Then
                    Continue For
                End If

                For Each affiliationId As Guid In
                    authorLink.AffiliationIds

                    Dim affiliation As AffiliationRecord =
                        Nothing

                    If _affiliationsById.TryGetValue(
                        affiliationId,
                        affiliation
                    ) Then

                        parts.Add(
                            affiliation.DisplayName
                        )

                    End If

                Next

            Next

            Return String.Join(
                " ",
                parts
            )

        End Function

    End Class

End Namespace
