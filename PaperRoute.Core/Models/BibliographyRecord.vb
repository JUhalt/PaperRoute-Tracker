Imports System
Imports System.Collections.Generic

Namespace Models

    Public Class BibliographyRecord

        Public Property SourceFormat As BibliographyFormat
        Public Property SourceType As String = String.Empty
        Public Property SourceKey As String = String.Empty
        Public Property Title As String = String.Empty
        Public Property Authors As List(Of BibliographyAuthor) = New List(Of BibliographyAuthor)()
        Public Property Journal As String = String.Empty
        Public Property PublishedDate As DateTime? = Nothing
        Public Property Volume As String = String.Empty
        Public Property Issue As String = String.Empty
        Public Property Pages As String = String.Empty
        Public Property Publisher As String = String.Empty
        Public Property Doi As String = String.Empty
        Public Property Url As String = String.Empty
        Public Property AbstractText As String = String.Empty
        Public Property Keywords As List(Of String) = New List(Of String)()
        Public Property Warnings As List(Of String) = New List(Of String)()
        Public Property UnmappedFields As Dictionary(Of String, String) =
            New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)

        Public ReadOnly Property LooksPublished As Boolean
            Get
                If Not PublishedDate.HasValue Then
                    Return False
                End If

                Return (
                    Not String.IsNullOrWhiteSpace(Journal) OrElse
                    Not String.IsNullOrWhiteSpace(Doi) OrElse
                    Not String.IsNullOrWhiteSpace(Publisher)
                )
            End Get
        End Property

        Public ReadOnly Property DisplayName As String
            Get
                Dim yearText As String =
                    If(PublishedDate.HasValue, PublishedDate.Value.Year.ToString(), "n.d.")

                Dim result As String =
                    yearText & " — " &
                    If(String.IsNullOrWhiteSpace(Title), "(Untitled record)", Title.Trim())

                If Not String.IsNullOrWhiteSpace(Doi) Then
                    result &= "  [" & Doi.Trim() & "]"
                End If

                If Warnings.Count > 0 Then
                    result &= "  ⚠"
                End If

                Return result
            End Get
        End Property

        Public Overrides Function ToString() As String
            Return DisplayName
        End Function

    End Class

End Namespace
