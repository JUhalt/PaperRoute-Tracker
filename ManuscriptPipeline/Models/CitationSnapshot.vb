Imports System
Imports System.Collections.Generic

Namespace Models

    ' The researcher's own citations (#91), as OpenAlex counted them on one
    ' day, for the works they confirmed are theirs. Kept in data\citations.json;
    ' figures such as the h-index are computed from it, never stored.
    Public Class CitationSnapshot

        Public Property FormatVersion As Integer = 1

        Public Property Orcid As String = String.Empty

        ' "OpenAlex", or "Example" in the example library.
        Public Property Source As String = String.Empty

        Public Property RetrievedUtc As DateTime?

        ' Whether OpenAlex was also asked which works it links to the iD.
        Public Property IncludeOpenAlexLinked As Boolean

        ' Works the researcher left out: "doi:10.…" or "openalex:W…".
        Public Property Excluded As List(Of String) = New List(Of String)()

        Public Property Works As List(Of CitedWork) = New List(Of CitedWork)()

    End Class


    Public Class CitedWork

        ' "W…"
        Public Property OpenAlexId As String = String.Empty

        ' Lower-case, without https://doi.org/; may be blank.
        Public Property Doi As String = String.Empty

        Public Property Title As String = String.Empty

        ' OpenAlex's publication year.
        Public Property Year As Integer?

        Public Property Journal As String = String.Empty

        Public Property CitedByCount As Integer

        ' Citations by year, as OpenAlex counts them (from 2012).
        Public Property CountsByYear As List(Of YearCount) = New List(Of YearCount)()

        ' Field-weighted citation impact; 1.0 is average for similar work.
        Public Property Fwci As Double?

        ' 0 to 1, among works of the same type, year, and field.
        Public Property Percentile As Double?

        Public Property InTop1Percent As Boolean

        Public Property InTop10Percent As Boolean

        ' "orcid", "paperroute", or "openalex": where the work was found.
        Public Property FoundBy As String = String.Empty

    End Class


    Public Class YearCount

        Public Property Year As Integer

        Public Property Count As Integer

    End Class

End Namespace
