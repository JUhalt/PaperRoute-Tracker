Imports System
Imports System.Collections.Generic

Namespace Models

    Public Class CrossrefMetadataSuggestion

        Public Property Doi As String = String.Empty

        Public Property Title As String = String.Empty

        Public Property Journal As String = String.Empty

        Public Property Publisher As String = String.Empty

        Public Property PublishedDate As DateTime? = Nothing

        Public Property Volume As String = String.Empty

        Public Property Issue As String = String.Empty

        Public Property Pages As String = String.Empty

        Public Property Url As String = String.Empty

        ' Crossref's work type, such as journal-article or posted-content.
        Public Property WorkType As String = String.Empty

        ' For a preprint, the DOIs of its published versions.
        Public Property PublishedVersionDois As List(Of String) =
            New List(Of String)()

        Public Property AbstractText As String = String.Empty

        Public Property Keywords As List(Of String) =
            New List(Of String)()

        Public Property Authors As List(Of CrossrefAuthorSuggestion) =
            New List(Of CrossrefAuthorSuggestion)()

    End Class

End Namespace
