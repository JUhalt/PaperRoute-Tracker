Imports System
Imports System.Collections.Generic
Imports System.Linq
Imports ManuscriptPipeline.Models

Namespace Services

    Public Enum JournalFactGroup
        Publishing
        OpenMetric
        EnteredMetric
    End Enum


    ' What one kind of journal fact means, and where a metric is published.
    Public NotInheritable Class JournalFactDefinition

        Public Sub New(key As String, label As String, group As JournalFactGroup, Optional definition As String = "", Optional whereUrl As String = "", Optional defaultSource As String = "")
            Me.Key = key
            Me.Label = label
            Me.Group = group
            Me.Definition = definition
            Me.WhereUrl = whereUrl
            Me.DefaultSource = defaultSource
        End Sub

        Public ReadOnly Property Key As String
        Public ReadOnly Property Label As String
        Public ReadOnly Property Group As JournalFactGroup
        Public ReadOnly Property Definition As String
        Public ReadOnly Property WhereUrl As String
        Public ReadOnly Property DefaultSource As String

    End Class


    ' The facts and metrics a journal record can hold (#87). Keys are stored
    ' in authors.json and never change. Metrics are shown by name, source,
    ' and year, and never combined into one score.
    Public NotInheritable Class JournalFactCatalog

        Private Sub New()
        End Sub

        ' From the open indexes.
        Public Const DoajListing As String = "doaj-listing"
        Public Const OpenAccess As String = "open-access"
        Public Const Apc As String = "apc"
        Public Const License As String = "license"
        Public Const Copyright As String = "copyright"
        Public Const Review As String = "review"
        Public Const Weeks As String = "weeks"
        Public Const Plagiarism As String = "plagiarism"
        Public Const Topics As String = "topics"
        Public Const Sharing As String = "sharing-url"
        Public Const MeanCitedness As String = "openalex-2yr-mean-citedness"
        Public Const OpenAlexHIndex As String = "openalex-h-index"
        Public Const OpenAlexI10Index As String = "openalex-i10-index"

        ' Entered by the researcher.
        Public Const Jif As String = "jif"
        Public Const Jif5 As String = "jif-5yr"
        Public Const CiteScore As String = "citescore"
        Public Const Sjr As String = "sjr"
        Public Const Snip As String = "snip"
        Public Const H5Index As String = "h5-index"
        Public Const AcceptanceRate As String = "acceptance-rate"
        Public Const DecisionTime As String = "decision-time"
        Public Const Other As String = "other"

        Public Const DoajSource As String = "DOAJ"
        Public Const OpenAlexSource As String = "OpenAlex"
        Public Const ExampleSource As String = "Example"

        Public Const DoraUrl As String = "https://sfdora.org/read/"

        Public Const DoraNote As String =
            "Journal metrics describe a journal as a whole, not the quality of any single article or the work of any author. PaperRoute never combines them into one score."

        Public Const Attribution As String = "Data from DOAJ and OpenAlex, both public domain (CC0)."

        Public Const OpenAlexCitation As String =
            "Priem, J., Piwowar, H., & Orr, R. (2022). OpenAlex: A fully-open index of scholarly works, authors, venues, institutions, and concepts. ArXiv. https://arxiv.org/abs/2205.01833"

        Public Const SharingSearchBase As String = "https://openpolicyfinder.jisc.ac.uk/search?search="

        ' Older than this, a checked fact is marked as possibly out of date.
        Public Const FreshDays As Integer = 365


        ' In the order the Journals page shows them.
        Public Shared ReadOnly Property Definitions As IReadOnlyList(Of JournalFactDefinition) = {
            New JournalFactDefinition(DoajListing, "DOAJ listing", JournalFactGroup.Publishing),
            New JournalFactDefinition(OpenAccess, "Open access", JournalFactGroup.Publishing),
            New JournalFactDefinition(Apc, "Publication fee", JournalFactGroup.Publishing),
            New JournalFactDefinition(License, "License", JournalFactGroup.Publishing),
            New JournalFactDefinition(Copyright, "Copyright", JournalFactGroup.Publishing),
            New JournalFactDefinition(Review, "Peer review", JournalFactGroup.Publishing),
            New JournalFactDefinition(Weeks, "Time to publication", JournalFactGroup.Publishing),
            New JournalFactDefinition(Plagiarism, "Plagiarism screening", JournalFactGroup.Publishing),
            New JournalFactDefinition(Topics, "Main topics", JournalFactGroup.Publishing),
            New JournalFactDefinition(Sharing, "Sharing policy", JournalFactGroup.Publishing),
            New JournalFactDefinition(MeanCitedness, "2-year mean citedness", JournalFactGroup.OpenMetric,
                "Citations received last year by works the journal published in the two years before, divided by the number of those works, counted in OpenAlex. Similar in idea to the Journal Impact Factor, but not the same number.",
                "https://help.openalex.org/data/common-attributes/", OpenAlexSource),
            New JournalFactDefinition(OpenAlexHIndex, "h-index (all years)", JournalFactGroup.OpenMetric,
                "The largest number h such that h of the journal's works in OpenAlex have been cited at least h times each.",
                "https://help.openalex.org/data/common-attributes/", OpenAlexSource),
            New JournalFactDefinition(OpenAlexI10Index, "i10-index (all years)", JournalFactGroup.OpenMetric,
                "The number of the journal's works in OpenAlex cited at least 10 times.",
                "https://help.openalex.org/data/common-attributes/", OpenAlexSource),
            New JournalFactDefinition(Jif, "Journal Impact Factor", JournalFactGroup.EnteredMetric,
                "Citations in one year to what the journal published in the two years before, divided by the number of scholarly items it published in those two years. From Clarivate's Journal Citation Reports.",
                "https://jcr.clarivate.com/", "Clarivate Journal Citation Reports"),
            New JournalFactDefinition(Jif5, "5-year Journal Impact Factor", JournalFactGroup.EnteredMetric,
                "The same idea as the Journal Impact Factor over five years: citations in one year to items from the five years before, divided by the items published in them.",
                "https://jcr.clarivate.com/", "Clarivate Journal Citation Reports"),
            New JournalFactDefinition(CiteScore, "CiteScore", JournalFactGroup.EnteredMetric,
                "Citations over four years to the journal's documents published in those same four years, divided by the number of those documents. From Elsevier's Scopus; not adjusted for field.",
                "https://www.scopus.com/sources", "Scopus (Elsevier)"),
            New JournalFactDefinition(Sjr, "SJR (SCImago Journal Rank)", JournalFactGroup.EnteredMetric,
                "Average weighted citations in one year to documents from the three years before, where a citation from a highly ranked journal counts for more. From SCImago, using Scopus data.",
                "https://www.scimagojr.com/", "SCImago Journal & Country Rank"),
            New JournalFactDefinition(Snip, "SNIP", JournalFactGroup.EnteredMetric,
                "Citations in one year per paper the journal published in the three years before, with each citation weighted down when the citing paper has a long reference list, for fairer comparison across fields. From CWTS, Leiden University.",
                "https://www.journalindicators.com/indicators", "CWTS Journal Indicators"),
            New JournalFactDefinition(H5Index, "h5-index", JournalFactGroup.EnteredMetric,
                "The largest number h such that h articles the journal published in the last five complete years have at least h citations each. From Google Scholar Metrics.",
                "https://scholar.google.com/citations?view_op=top_venues", "Google Scholar Metrics"),
            New JournalFactDefinition(AcceptanceRate, "Acceptance rate", JournalFactGroup.EnteredMetric,
                "The share of submissions the journal accepts, as the journal or its publisher reports it. There is no standard definition, so note what it counts.",
                "", "The journal's website"),
            New JournalFactDefinition(DecisionTime, "Time to first decision", JournalFactGroup.EnteredMetric,
                "How long the journal takes to reach a first decision, as it reports it. Your own times are on the Insights page.",
                "", "The journal's website"),
            New JournalFactDefinition(Other, "Other metric", JournalFactGroup.EnteredMetric,
                "Any other metric, with its name, source, and year.",
                "", "")
        }


        Public Shared Function Find(key As String) As JournalFactDefinition
            Return Definitions.FirstOrDefault(Function(item) String.Equals(item.Key, key, StringComparison.Ordinal))
        End Function


        Public Shared ReadOnly Property EnteredMetrics As IReadOnlyList(Of JournalFactDefinition) =
            Definitions.Where(Function(item) item.Group = JournalFactGroup.EnteredMetric).ToList()


        ' The fact's name as shown: the catalog label, or the name the
        ' researcher gave an "other" metric. Unknown keys keep their key.
        Public Shared Function LabelOf(fact As JournalFact) As String
            If fact Is Nothing Then Return String.Empty
            If String.Equals(fact.Key, Other, StringComparison.Ordinal) AndAlso Not String.IsNullOrWhiteSpace(fact.Label) Then Return fact.Label.Trim()
            Dim definition As JournalFactDefinition = Find(fact.Key)
            Return If(definition?.Label, fact.Key)
        End Function


        Public Shared Function GroupOf(fact As JournalFact) As JournalFactGroup
            If fact Is Nothing Then Return JournalFactGroup.Publishing
            If fact.EnteredByYou Then Return JournalFactGroup.EnteredMetric
            Dim definition As JournalFactDefinition = Find(fact.Key)
            Return If(definition Is Nothing, JournalFactGroup.Publishing, definition.Group)
        End Function


        ' The journal's sharing policy page: DOAJ's Open Policy Finder link
        ' when it gave one, else an Open Policy Finder search by ISSN. Opened
        ' in the browser only; PaperRoute never calls Open Policy Finder.
        Public Shared Function SharingPolicyUrl(record As JournalRecord) As String
            If record Is Nothing Then Return String.Empty
            Dim fact As JournalFact = If(record.Facts, New List(Of JournalFact)()).
                FirstOrDefault(Function(item) String.Equals(item?.Key, Sharing, StringComparison.Ordinal) AndAlso IsOpenPolicyFinderRecord(item.Url))
            If fact IsNot Nothing Then Return fact.Url
            Dim issn As String = IssnService.NormalizeList(record.Issns).FirstOrDefault()
            Return If(issn Is Nothing, String.Empty, SharingSearchBase & issn)
        End Function


        ' https://openpolicyfinder.jisc.ac.uk/id/publication/17599 or
        ' /publication/17599, and nothing else.
        Public Shared Function IsOpenPolicyFinderRecord(url As String) As Boolean
            Dim target As Uri = Nothing
            If String.IsNullOrWhiteSpace(url) OrElse Not Uri.TryCreate(url.Trim(), UriKind.Absolute, target) Then Return False
            If Not String.Equals(target.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) Then Return False
            If Not String.Equals(target.Host, "openpolicyfinder.jisc.ac.uk", StringComparison.OrdinalIgnoreCase) Then Return False
            Return Text.RegularExpressions.Regex.IsMatch(target.AbsolutePath, "^/(id/)?publication/\d+$")
        End Function

    End Class

End Namespace
