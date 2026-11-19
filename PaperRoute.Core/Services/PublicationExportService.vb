Imports System
Imports System.Collections.Generic
Imports System.Linq
Imports System.Net
Imports System.Text
Imports ManuscriptPipeline.Models

Namespace Services

    Public NotInheritable Class PublicationExportService

        Private Sub New()
        End Sub


        Public Shared Function SelectByScope(
            manuscripts As IEnumerable(Of Manuscript),
            scope As PublicationExportScope
        ) As List(Of Manuscript)

            If manuscripts Is Nothing Then
                Throw New ArgumentNullException(NameOf(manuscripts))
            End If

            Dim selected As IEnumerable(Of Manuscript) =
                manuscripts.Where(
                    Function(item)
                        Return item IsNot Nothing
                    End Function
                )

            Select Case scope

                Case PublicationExportScope.PublishedOnly

                    selected =
                        selected.Where(
                            Function(item)
                                Return item.CurrentStage = PaperStage.Published OrElse
                                    item.Location = ManuscriptLocation.Published
                            End Function
                        )

                Case PublicationExportScope.AcceptedAndPublished

                    selected =
                        selected.Where(
                            Function(item)
                                Return item.CurrentStage = PaperStage.Accepted OrElse
                                    item.CurrentStage = PaperStage.InPress OrElse
                                    item.CurrentStage = PaperStage.Published OrElse
                                    item.Location = ManuscriptLocation.Published
                            End Function
                        )

                Case PublicationExportScope.AllManuscripts
                    ' No additional filtering.

            End Select

            Return selected.
                OrderByDescending(
                    Function(item)
                        Return PublicationYear(item)
                    End Function
                ).
                ThenBy(
                    Function(item)
                        Return item.Title
                    End Function,
                    StringComparer.CurrentCultureIgnoreCase
                ).
                ToList()

        End Function


        Public Shared Function Export(
            manuscripts As IEnumerable(Of Manuscript),
            authorLibrary As AuthorLibraryData,
            format As PublicationExportFormat,
            style As PublicationExportStyle,
            Optional groupByType As Boolean = False
        ) As String

            If manuscripts Is Nothing Then
                Throw New ArgumentNullException(NameOf(manuscripts))
            End If

            If authorLibrary Is Nothing Then
                Throw New ArgumentNullException(NameOf(authorLibrary))
            End If

            Dim items As List(Of Manuscript) =
                manuscripts.
                    Where(
                        Function(item)
                            Return item IsNot Nothing
                        End Function
                    ).
                    OrderByDescending(
                        Function(item)
                            Return PublicationYear(item)
                        End Function
                    ).
                    ThenBy(
                        Function(item)
                            Return item.Title
                        End Function,
                        StringComparer.CurrentCultureIgnoreCase
                    ).
                    ToList()

            If groupByType Then
                Return ExportGrouped(items, authorLibrary, format, style)
            End If

            Select Case format

                Case PublicationExportFormat.PlainText

                    Return ExportPlainText(
                        items,
                        authorLibrary,
                        style
                    )

                Case PublicationExportFormat.Markdown

                    Return ExportMarkdown(
                        items,
                        authorLibrary,
                        style
                    )

                Case PublicationExportFormat.Html

                    Return ExportHtml(
                        items,
                        authorLibrary,
                        style
                    )

                Case Else

                    Throw New ArgumentOutOfRangeException(
                        NameOf(format)
                    )

            End Select

        End Function


        ' One section per kind of work (#64), in a fixed order; work without
        ' a type, or of another type, comes last as "Other work".
        Public Shared Function GroupHeading(type As WorkType) As String
            Select Case type
                Case WorkType.JournalArticle : Return "Journal articles"
                Case WorkType.Preprint : Return "Preprints"
                Case WorkType.ConferencePaper : Return "Conference papers"
                Case WorkType.ConferenceAbstract : Return "Conference abstracts"
                Case WorkType.Poster : Return "Posters"
                Case WorkType.BookChapter : Return "Book chapters"
                Case WorkType.Thesis : Return "Theses and dissertations"
                Case Else : Return "Other work"
            End Select
        End Function


        Private Shared Function ExportGrouped(
            items As List(Of Manuscript),
            authorLibrary As AuthorLibraryData,
            format As PublicationExportFormat,
            style As PublicationExportStyle
        ) As String

            Dim sections = items.
                GroupBy(Function(item) If(item.WorkType = WorkType.Unspecified, WorkType.Other, item.WorkType)).
                OrderBy(Function(group) CInt(group.Key)).ToList()

            Dim builder As New StringBuilder()

            Select Case format

                Case PublicationExportFormat.PlainText
                    If style = PublicationExportStyle.CvSection Then builder.AppendLine("PUBLICATIONS").AppendLine()
                    For Each section In sections
                        builder.AppendLine(GroupHeading(section.Key).ToUpperInvariant()).AppendLine()
                        builder.AppendLine(ExportPlainText(section.ToList(), authorLibrary, PublicationExportStyle.PublicationList)).AppendLine()
                    Next
                    Return builder.ToString().TrimEnd()

                Case PublicationExportFormat.Markdown
                    If style = PublicationExportStyle.CvSection Then builder.AppendLine("## Publications").AppendLine()
                    For Each section In sections
                        builder.AppendLine("### " & GroupHeading(section.Key)).AppendLine()
                        builder.AppendLine(ExportMarkdown(section.ToList(), authorLibrary, PublicationExportStyle.PublicationList)).AppendLine()
                    Next
                    Return builder.ToString().TrimEnd()

                Case PublicationExportFormat.Html
                    builder.AppendLine("<!doctype html>")
                    builder.AppendLine("<html><head><meta charset=""utf-8""><title>PaperRoute Publications</title></head><body>")
                    If style = PublicationExportStyle.CvSection Then builder.AppendLine("<h2>Publications</h2>")
                    For Each section In sections
                        builder.AppendLine("<h3>" & WebUtility.HtmlEncode(GroupHeading(section.Key)) & "</h3>")
                        builder.AppendLine("<ul>")
                        For Each manuscript As Manuscript In section
                            builder.Append("<li>").Append(WebUtility.HtmlEncode(FormatCitation(manuscript, authorLibrary))).AppendLine("</li>")
                        Next
                        builder.AppendLine("</ul>")
                    Next
                    builder.AppendLine("</body></html>")
                    Return builder.ToString()

                Case Else
                    Throw New ArgumentOutOfRangeException(NameOf(format))

            End Select

        End Function


        Public Shared Function FormatCitation(
            manuscript As Manuscript,
            authorLibrary As AuthorLibraryData
        ) As String

            If manuscript Is Nothing Then
                Throw New ArgumentNullException(NameOf(manuscript))
            End If

            If authorLibrary Is Nothing Then
                Throw New ArgumentNullException(NameOf(authorLibrary))
            End If

            Dim parts As New List(Of String)()

            Dim authors As String =
                FormatAuthors(
                    manuscript,
                    authorLibrary
                )

            If Not String.IsNullOrWhiteSpace(authors) Then
                parts.Add(authors)
            End If

            Dim yearText As String =
                If(
                    manuscript.Metadata IsNot Nothing AndAlso
                    manuscript.Metadata.PublishedDate.HasValue,
                    manuscript.Metadata.PublishedDate.Value.Year.ToString(),
                    "n.d."
                )

            parts.Add(
                "(" &
                yearText &
                ")."
            )

            Dim title As String =
                If(
                    manuscript.Title,
                    String.Empty
                ).Trim()

            If String.IsNullOrWhiteSpace(title) Then
                title = "(Untitled manuscript)"
            End If

            parts.Add(
                title &
                "."
            )

            Dim publicationText As String =
                FormatPublicationDetails(
                    manuscript
                )

            If Not String.IsNullOrWhiteSpace(
                publicationText
            ) Then

                parts.Add(
                    publicationText
                )

            ElseIf manuscript.Metadata IsNot Nothing AndAlso
                   (
                       Not String.IsNullOrWhiteSpace(
                           manuscript.Metadata.PreprintDoi
                       ) OrElse
                       Not String.IsNullOrWhiteSpace(
                           manuscript.Metadata.PreprintUrl
                       )
                   ) Then

                parts.Add(
                    "[Preprint]."
                )

            End If

            Dim doi As String =
                String.Empty

            If manuscript.Metadata IsNot Nothing Then
                doi =
                    DoiNormalizer.Normalize(
                        manuscript.Metadata.Doi
                    )
            End If

            If Not String.IsNullOrWhiteSpace(doi) Then

                parts.Add(
                    "https://doi.org/" &
                    doi
                )

            ElseIf manuscript.Metadata IsNot Nothing AndAlso
                   Not String.IsNullOrWhiteSpace(
                       manuscript.Metadata.PublicationUrl
                   ) Then

                parts.Add(
                    manuscript.Metadata.PublicationUrl.Trim()
                )

            ElseIf manuscript.Metadata IsNot Nothing Then

                Dim preprintDoi As String =
                    DoiNormalizer.Normalize(
                        manuscript.Metadata.PreprintDoi
                    )

                If Not String.IsNullOrWhiteSpace(
                    preprintDoi
                ) Then

                    parts.Add(
                        "Preprint: https://doi.org/" &
                        preprintDoi
                    )

                ElseIf Not String.IsNullOrWhiteSpace(
                    manuscript.Metadata.PreprintUrl
                ) Then

                    parts.Add(
                        "Preprint: " &
                        manuscript.Metadata.PreprintUrl.Trim()
                    )

                End If

            End If

            Return String.Join(
                " ",
                parts
            ).Trim()

        End Function


        Private Shared Function ExportPlainText(
            manuscripts As List(Of Manuscript),
            authorLibrary As AuthorLibraryData,
            style As PublicationExportStyle
        ) As String

            Dim builder As New StringBuilder()

            If style =
               PublicationExportStyle.CvSection Then

                builder.AppendLine(
                    "PUBLICATIONS"
                )

                builder.AppendLine()

            End If

            For Each manuscript As Manuscript In manuscripts

                builder.AppendLine(
                    FormatCitation(
                        manuscript,
                        authorLibrary
                    )
                )

                builder.AppendLine()

            Next

            Return builder.ToString().TrimEnd()

        End Function


        Private Shared Function ExportMarkdown(
            manuscripts As List(Of Manuscript),
            authorLibrary As AuthorLibraryData,
            style As PublicationExportStyle
        ) As String

            Dim builder As New StringBuilder()

            If style =
               PublicationExportStyle.CvSection Then

                builder.AppendLine(
                    "## Publications"
                )

                builder.AppendLine()

            End If

            For Each manuscript As Manuscript In manuscripts

                builder.Append(
                    "- "
                )

                builder.AppendLine(
                    EscapeMarkdown(
                        FormatCitation(
                            manuscript,
                            authorLibrary
                        )
                    )
                )

            Next

            Return builder.ToString().TrimEnd()

        End Function


        Private Shared Function ExportHtml(
            manuscripts As List(Of Manuscript),
            authorLibrary As AuthorLibraryData,
            style As PublicationExportStyle
        ) As String

            Dim builder As New StringBuilder()

            builder.AppendLine(
                "<!doctype html>"
            )

            builder.AppendLine(
                "<html><head><meta charset=""utf-8""><title>PaperRoute Publications</title></head><body>"
            )

            If style =
               PublicationExportStyle.CvSection Then

                builder.AppendLine(
                    "<h2>Publications</h2>"
                )

            End If

            builder.AppendLine(
                "<ul>"
            )

            For Each manuscript As Manuscript In manuscripts

                builder.Append(
                    "<li>"
                )

                builder.Append(
                    WebUtility.HtmlEncode(
                        FormatCitation(
                            manuscript,
                            authorLibrary
                        )
                    )
                )

                builder.AppendLine(
                    "</li>"
                )

            Next

            builder.AppendLine(
                "</ul>"
            )

            builder.AppendLine(
                "</body></html>"
            )

            Return builder.ToString()

        End Function


        Private Shared Function FormatAuthors(
            manuscript As Manuscript,
            authorLibrary As AuthorLibraryData
        ) As String

            Dim authorNames As New List(Of String)()

            If manuscript.Authors IsNot Nothing Then

                For Each link As ManuscriptAuthor In
                    manuscript.Authors

                    If link Is Nothing Then
                        Continue For
                    End If

                    Dim author As AuthorRecord =
                        authorLibrary.Authors.
                            FirstOrDefault(
                                Function(item)
                                    Return item IsNot Nothing AndAlso
                                        item.Id = link.AuthorId
                                End Function
                            )

                    If author Is Nothing Then
                        Continue For
                    End If

                    authorNames.Add(
                        FormatAuthor(
                            author
                        )
                    )

                Next

            End If

            If authorNames.Count = 0 AndAlso
               Not String.IsNullOrWhiteSpace(
                   manuscript.CoAuthors
               ) Then

                Return manuscript.CoAuthors.Trim()

            End If

            If authorNames.Count = 0 Then
                Return String.Empty
            End If

            If authorNames.Count = 1 Then
                Return authorNames(0)
            End If

            If authorNames.Count = 2 Then

                Return authorNames(0) &
                    ", & " &
                    authorNames(1)

            End If

            Return String.Join(
                ", ",
                authorNames.Take(
                    authorNames.Count - 1
                )
            ) &
                ", & " &
                authorNames(authorNames.Count - 1)

        End Function


        Private Shared Function FormatAuthor(
            author As AuthorRecord
        ) As String

            If String.IsNullOrWhiteSpace(
                author.FamilyName
            ) Then

                Return author.DisplayName

            End If

            Dim result As String =
                author.FamilyName.Trim()

            Dim initials As New List(Of String)()

            AddInitials(
                initials,
                author.GivenName
            )

            AddInitials(
                initials,
                author.MiddleName
            )

            If initials.Count > 0 Then

                result &=
                    ", " &
                    String.Join(
                        " ",
                        initials
                    )

            End If

            If Not String.IsNullOrWhiteSpace(
                author.Suffix
            ) Then

                result &=
                    ", " &
                    author.Suffix.Trim()

            End If

            Return result

        End Function


        Private Shared Sub AddInitials(
            initials As List(Of String),
            value As String
        )

            If String.IsNullOrWhiteSpace(value) Then
                Return
            End If

            For Each part As String In
                value.Split(
                    New Char() {
                        " "c,
                        "-"c
                    },
                    StringSplitOptions.RemoveEmptyEntries
                )

                If part.Length > 0 Then

                    initials.Add(
                        Char.ToUpperInvariant(
                            part(0)
                        ) &
                        "."
                    )

                End If

            Next

        End Sub


        Private Shared Function FormatPublicationDetails(
            manuscript As Manuscript
        ) As String

            If manuscript.Metadata Is Nothing Then
                Return String.Empty
            End If

            Dim outlet As String =
                If(
                    manuscript.Metadata.PublicationJournal,
                    String.Empty
                ).Trim()

            If String.IsNullOrWhiteSpace(
                outlet
            ) Then

                Select Case manuscript.CurrentStage

                    Case PaperStage.Accepted,
                         PaperStage.InPress,
                         PaperStage.Published

                        outlet =
                            If(
                                manuscript.TargetJournal,
                                String.Empty
                            ).Trim()

                End Select

            End If

            If String.IsNullOrWhiteSpace(outlet) Then

                Dim publisher As String =
                    If(
                        manuscript.Metadata.Publisher,
                        String.Empty
                    ).Trim()

                If String.IsNullOrWhiteSpace(
                    publisher
                ) Then

                    Return String.Empty

                End If

                Return publisher &
                    "."

            End If

            Dim result As String =
                outlet

            If Not String.IsNullOrWhiteSpace(
                manuscript.Metadata.Volume
            ) Then

                result &=
                    ", " &
                    If(
                        manuscript.Metadata.Volume,
                        String.Empty
                    ).Trim()

                If Not String.IsNullOrWhiteSpace(
                    manuscript.Metadata.Issue
                ) Then

                    result &=
                        "(" &
                        If(
                            manuscript.Metadata.Issue,
                            String.Empty
                        ).Trim() &
                        ")"

                End If

            ElseIf Not String.IsNullOrWhiteSpace(
                manuscript.Metadata.Issue
            ) Then

                result &=
                    ", (" &
                    If(
                        manuscript.Metadata.Issue,
                        String.Empty
                    ).Trim() &
                    ")"

            End If

            If Not String.IsNullOrWhiteSpace(
                manuscript.Metadata.Pages
            ) Then

                result &=
                    ", " &
                    If(
                        manuscript.Metadata.Pages,
                        String.Empty
                    ).Trim()

            End If

            Return result &
                "."

        End Function


        Private Shared Function PublicationYear(
            manuscript As Manuscript
        ) As Integer

            If manuscript IsNot Nothing AndAlso
               manuscript.Metadata IsNot Nothing AndAlso
               manuscript.Metadata.PublishedDate.HasValue Then

                Return manuscript.Metadata.PublishedDate.Value.Year

            End If

            Return 0

        End Function


        Private Shared Function EscapeMarkdown(
            value As String
        ) As String

            If value Is Nothing Then
                Return String.Empty
            End If

            Return value.
                Replace(
                    "\",
                    "\\"
                ).
                Replace(
                    "*",
                    "\*"
                ).
                Replace(
                    "_",
                    "\_"
                ).
                Replace(
                    "[",
                    "\["
                ).
                Replace(
                    "]",
                    "\]"
                )

        End Function

    End Class

End Namespace
