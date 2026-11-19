Imports System
Imports System.Collections.Generic

Namespace Models

    Public Class ManuscriptMetadata

        Public Property AbstractText As String = String.Empty

        Public Property Keywords As List(Of String) =
            New List(Of String)()

        Public Property Doi As String = String.Empty

        Public Property PublicationJournal As String = String.Empty

        Public Property PublishedDate As DateTime? = Nothing

        Public Property Volume As String = String.Empty

        Public Property Issue As String = String.Empty

        Public Property Pages As String = String.Empty

        Public Property Publisher As String = String.Empty

        Public Property PublicationUrl As String = String.Empty

        Public Property PreprintDoi As String = String.Empty

        Public Property PreprintUrl As String = String.Empty

        Public Property ExternalIdentifiers As Dictionary(Of String, String) =
            New Dictionary(Of String, String)()

    End Class

End Namespace
