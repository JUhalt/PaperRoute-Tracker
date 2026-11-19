Imports System
Imports System.Collections.Generic

Namespace Models

    Public Class ManuscriptAuthor

        Public Property AuthorId As Guid

        Public Property AffiliationIds As List(Of Guid) =
            New List(Of Guid)()

        Public Property IsCorrespondingAuthor As Boolean =
            False

    End Class

End Namespace
