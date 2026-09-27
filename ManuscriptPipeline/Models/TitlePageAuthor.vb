Imports System.Collections.Generic

Namespace Models

    ' One author proposed from a pasted title page. Nothing here is linked to
    ' the reusable author library until the user reviews and applies it.
    Public Class TitlePageAuthor

        Public Property Name As BibliographyAuthor = New BibliographyAuthor()

        ' Affiliation and note markers exactly as written, e.g. "1", "2", "*".
        Public Property Markers As List(Of String) = New List(Of String)()

        Public Property Affiliations As List(Of String) = New List(Of String)()

        Public Property IsCorrespondingAuthor As Boolean

    End Class

End Namespace
