Imports System.Collections.Generic

Namespace Models

    Public Class BibliographyParseResult
        Public Property Format As BibliographyFormat
        Public Property Records As List(Of BibliographyRecord) = New List(Of BibliographyRecord)()
        Public Property FileWarnings As List(Of String) = New List(Of String)()
    End Class

End Namespace
