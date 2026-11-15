Imports System
Imports System.Collections.Generic

Namespace Models

    Public Class OrcidProfileSuggestion

        Public Property Orcid As String =
            String.Empty

        Public Property GivenName As String =
            String.Empty

        Public Property FamilyName As String =
            String.Empty

        Public Property CreditName As String =
            String.Empty

        Public Property Biography As String =
            String.Empty

        Public Property Keywords As List(Of String) =
            New List(Of String)()

        Public Property ResearcherUrls As List(Of String) =
            New List(Of String)()

        Public Property Affiliations As List(Of OrcidAffiliationSuggestion) =
            New List(Of OrcidAffiliationSuggestion)()

        Public Property Works As List(Of OrcidWorkSuggestion) =
            New List(Of OrcidWorkSuggestion)()


        Public ReadOnly Property PublicName As String
            Get

                Dim parts As New List(Of String)()

                If Not String.IsNullOrWhiteSpace(GivenName) Then
                    parts.Add(GivenName.Trim())
                End If

                If Not String.IsNullOrWhiteSpace(FamilyName) Then
                    parts.Add(FamilyName.Trim())
                End If

                Return String.Join(" ", parts)

            End Get
        End Property

    End Class

End Namespace
