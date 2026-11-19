Imports System.Collections.Generic

Namespace Models

    Public Class AuthorLibraryData

        Public Property Authors As List(Of AuthorRecord) =
            New List(Of AuthorRecord)()

        Public Property Affiliations As List(Of AffiliationRecord) =
            New List(Of AffiliationRecord)()

        ' Reusable journals intentionally share the same local metadata
        ' library as authors and affiliations. This keeps v0.2 backward
        ' compatible with the established authors.json backup/restore path.
        Public Property Journals As List(Of JournalRecord) =
            New List(Of JournalRecord)()

        ' Colors chosen for tags (#64). A tag without one gets its color from
        ' its name.
        Public Property TagColors As List(Of TagColor) =
            New List(Of TagColor)()

    End Class

End Namespace
