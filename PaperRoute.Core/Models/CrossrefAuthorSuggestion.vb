Imports System.Collections.Generic

Namespace Models

    Public Class CrossrefAuthorSuggestion

        Public Property GivenName As String = String.Empty

        Public Property FamilyName As String = String.Empty

        Public Property Orcid As String = String.Empty

        Public Property Affiliations As List(Of String) =
            New List(Of String)()


        Public ReadOnly Property DisplayName As String
            Get

                Return String.Join(
                    " ",
                    New String() {
                        GivenName,
                        FamilyName
                    }.
                    Where(
                        Function(part)
                            Return Not String.IsNullOrWhiteSpace(part)
                        End Function
                    )
                ).Trim()

            End Get
        End Property

    End Class

End Namespace
