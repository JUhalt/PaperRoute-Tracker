Imports System
Imports System.Collections.Generic
Imports System.Globalization
Imports System.Linq
Imports System.Net
Imports System.Net.Http
Imports System.Net.Http.Headers
Imports System.Text.Json
Imports System.Threading
Imports System.Threading.Tasks
Imports ManuscriptPipeline.Models

Namespace Services

    Public Class OrcidClient

        ' Every request goes through the Online services gate (#86), under the
        ' service that asked: ORCID import, or the publication check.
        Private ReadOnly SharedHttpClient As HttpClient


        Public Sub New(Optional serviceId As String = OnlineServiceCatalog.OrcidImport)

            SharedHttpClient = OnlineAccess.ClientFor(serviceId)

        End Sub


        Public Async Function LookupAsync(
            orcid As String,
            Optional cancellationToken As CancellationToken = Nothing
        ) As Task(Of OrcidProfileSuggestion)

            Dim normalized As String =
                OrcidIdentifierService.NormalizeAndValidate(
                    orcid
                )

            Dim requestUri As String =
                "https://orcid.org/" &
                normalized

            Using request As New HttpRequestMessage(
                HttpMethod.Get,
                requestUri
            )

                request.Headers.Accept.Clear()
                request.Headers.Accept.Add(
                    New MediaTypeWithQualityHeaderValue(
                        "application/vnd.orcid+json"
                    )
                )

                Using response As HttpResponseMessage =
                    Await SharedHttpClient.SendAsync(
                        request,
                        HttpCompletionOption.ResponseHeadersRead,
                        cancellationToken
                    )

                    If response.StatusCode =
                       HttpStatusCode.NotFound Then

                        Throw New InvalidOperationException(
                            "ORCID did not find a public record for that iD."
                        )

                    End If

                    If CInt(response.StatusCode) = 429 Then

                        Throw New InvalidOperationException(
                            "ORCID is temporarily rate-limiting requests. Please wait a moment and try again."
                        )

                    End If

                    If response.StatusCode =
                       HttpStatusCode.ServiceUnavailable Then

                        Throw New InvalidOperationException(
                            "ORCID is temporarily unavailable. Please try again later."
                        )

                    End If

                    If Not response.IsSuccessStatusCode Then

                        Throw New InvalidOperationException(
                            "ORCID returned HTTP " &
                            CInt(response.StatusCode).ToString() &
                            ". Please try again later."
                        )

                    End If

                    Dim json As String =
                        Await response.Content.ReadAsStringAsync(
                            cancellationToken
                        )

                    Dim suggestion As OrcidProfileSuggestion =
                        ParseRecordJson(
                            json
                        )

                    If String.IsNullOrWhiteSpace(
                        suggestion.Orcid
                    ) Then

                        suggestion.Orcid =
                            normalized

                    End If

                    If Not String.Equals(
                        OrcidIdentifierService.Normalize(
                            suggestion.Orcid
                        ),
                        normalized,
                        StringComparison.OrdinalIgnoreCase
                    ) Then

                        Throw New InvalidOperationException(
                            "ORCID returned a record identifier that did not match the requested iD."
                        )

                    End If

                    suggestion.Orcid =
                        normalized

                    Return suggestion

                End Using

            End Using

        End Function


        Friend Shared Function ParseRecordJson(
            json As String
        ) As OrcidProfileSuggestion

            If String.IsNullOrWhiteSpace(json) Then

                Throw New InvalidOperationException(
                    "ORCID returned an empty response."
                )

            End If

            Using document As JsonDocument =
                JsonDocument.Parse(
                    json
                )

                Dim root As JsonElement =
                    document.RootElement

                If root.ValueKind <>
                   JsonValueKind.Object Then

                    Throw New InvalidOperationException(
                        "ORCID returned an unexpected response."
                    )

                End If

                Dim suggestion As New OrcidProfileSuggestion()

                Dim identifier As JsonElement

                If root.TryGetProperty(
                    "orcid-identifier",
                    identifier
                ) AndAlso
                   identifier.ValueKind =
                    JsonValueKind.Object Then

                    suggestion.Orcid =
                        ReadString(
                            identifier,
                            "path"
                        )

                End If

                Dim person As JsonElement

                If root.TryGetProperty(
                    "person",
                    person
                ) AndAlso
                   person.ValueKind =
                    JsonValueKind.Object Then

                    ReadPerson(
                        person,
                        suggestion
                    )

                End If

                Dim activities As JsonElement

                If root.TryGetProperty(
                    "activities-summary",
                    activities
                ) AndAlso
                   activities.ValueKind =
                    JsonValueKind.Object Then

                    suggestion.Affiliations =
                        ReadEmployments(
                            activities
                        )

                    suggestion.Works =
                        ReadWorks(
                            activities
                        )

                End If

                Return suggestion

            End Using

        End Function


        Private Shared Sub ReadPerson(
            person As JsonElement,
            suggestion As OrcidProfileSuggestion
        )

            Dim name As JsonElement

            If person.TryGetProperty(
                "name",
                name
            ) AndAlso
               name.ValueKind =
                JsonValueKind.Object Then

                suggestion.GivenName =
                    ReadNestedValue(
                        name,
                        "given-names"
                    )

                suggestion.FamilyName =
                    ReadNestedValue(
                        name,
                        "family-name"
                    )

                suggestion.CreditName =
                    ReadNestedValue(
                        name,
                        "credit-name"
                    )

            End If

            Dim biography As JsonElement

            If person.TryGetProperty(
                "biography",
                biography
            ) AndAlso
               biography.ValueKind =
                JsonValueKind.Object Then

                suggestion.Biography =
                    ReadString(
                        biography,
                        "content"
                    )

            End If

            Dim keywordsContainer As JsonElement

            If person.TryGetProperty(
                "keywords",
                keywordsContainer
            ) AndAlso
               keywordsContainer.ValueKind =
                JsonValueKind.Object Then

                Dim keywords As JsonElement

                If keywordsContainer.TryGetProperty(
                    "keyword",
                    keywords
                ) AndAlso
                   keywords.ValueKind =
                    JsonValueKind.Array Then

                    For Each item As JsonElement In
                        keywords.EnumerateArray()

                        Dim content As String =
                            ReadString(
                                item,
                                "content"
                            )

                        If Not String.IsNullOrWhiteSpace(
                            content
                        ) AndAlso
                           Not suggestion.Keywords.Contains(
                               content,
                               StringComparer.OrdinalIgnoreCase
                           ) Then

                            suggestion.Keywords.Add(
                                content
                            )

                        End If

                    Next

                End If

            End If

            Dim urlsContainer As JsonElement

            If person.TryGetProperty(
                "researcher-urls",
                urlsContainer
            ) AndAlso
               urlsContainer.ValueKind =
                JsonValueKind.Object Then

                Dim urls As JsonElement

                If urlsContainer.TryGetProperty(
                    "researcher-url",
                    urls
                ) AndAlso
                   urls.ValueKind =
                    JsonValueKind.Array Then

                    For Each item As JsonElement In
                        urls.EnumerateArray()

                        Dim urlElement As JsonElement

                        If item.TryGetProperty(
                            "url",
                            urlElement
                        ) AndAlso
                           urlElement.ValueKind =
                            JsonValueKind.Object Then

                            Dim value As String =
                                ReadString(
                                    urlElement,
                                    "value"
                                )

                            If Not String.IsNullOrWhiteSpace(
                                value
                            ) Then

                                suggestion.ResearcherUrls.Add(
                                    value
                                )

                            End If

                        End If

                    Next

                End If

            End If

        End Sub


        Private Shared Function ReadEmployments(
            activities As JsonElement
        ) As List(Of OrcidAffiliationSuggestion)

            Dim results As New List(Of OrcidAffiliationSuggestion)()
            Dim employments As JsonElement

            If Not activities.TryGetProperty(
                "employments",
                employments
            ) OrElse
               employments.ValueKind <>
                JsonValueKind.Object Then

                Return results

            End If

            Dim groups As JsonElement

            If Not employments.TryGetProperty(
                "affiliation-group",
                groups
            ) OrElse
               groups.ValueKind <>
                JsonValueKind.Array Then

                Return results

            End If

            For Each group As JsonElement In
                groups.EnumerateArray()

                Dim summaries As JsonElement

                If Not group.TryGetProperty(
                    "summaries",
                    summaries
                ) OrElse
                   summaries.ValueKind <>
                    JsonValueKind.Array Then

                    Continue For

                End If

                For Each wrapper As JsonElement In
                    summaries.EnumerateArray()

                    Dim summary As JsonElement

                    If Not wrapper.TryGetProperty(
                        "employment-summary",
                        summary
                    ) OrElse
                       summary.ValueKind <>
                        JsonValueKind.Object Then

                        Continue For

                    End If

                    Dim affiliation As New OrcidAffiliationSuggestion With {
                        .Department = ReadString(summary, "department-name"),
                        .RoleTitle = ReadString(summary, "role-title"),
                        .StartDate = ReadPartialDate(summary, "start-date"),
                        .EndDate = ReadPartialDate(summary, "end-date")
                    }

                    Dim organization As JsonElement

                    If summary.TryGetProperty(
                        "organization",
                        organization
                    ) AndAlso
                       organization.ValueKind =
                        JsonValueKind.Object Then

                        affiliation.Institution =
                            ReadString(
                                organization,
                                "name"
                            )

                        Dim address As JsonElement

                        If organization.TryGetProperty(
                            "address",
                            address
                        ) AndAlso
                           address.ValueKind =
                            JsonValueKind.Object Then

                            affiliation.City =
                                ReadString(
                                    address,
                                    "city"
                                )

                            affiliation.Region =
                                ReadString(
                                    address,
                                    "region"
                                )

                            affiliation.Country =
                                ReadString(
                                    address,
                                    "country"
                                )

                        End If

                    End If

                    If Not String.IsNullOrWhiteSpace(
                        affiliation.Institution
                    ) Then

                        results.Add(
                            affiliation
                        )

                    End If

                Next

            Next

            Return results

        End Function


        Private Shared Function ReadWorks(
            activities As JsonElement
        ) As List(Of OrcidWorkSuggestion)

            Dim results As New List(Of OrcidWorkSuggestion)()
            Dim works As JsonElement

            If Not activities.TryGetProperty(
                "works",
                works
            ) OrElse
               works.ValueKind <>
                JsonValueKind.Object Then

                Return results

            End If

            Dim groups As JsonElement

            If Not works.TryGetProperty(
                "group",
                groups
            ) OrElse
               groups.ValueKind <>
                JsonValueKind.Array Then

                Return results

            End If

            For Each group As JsonElement In
                groups.EnumerateArray()

                Dim summaries As JsonElement

                If Not group.TryGetProperty(
                    "work-summary",
                    summaries
                ) OrElse
                   summaries.ValueKind <>
                    JsonValueKind.Array Then

                    Continue For

                End If

                Dim preferred As JsonElement
                Dim preferredIndex As Integer =
                    Integer.MinValue

                Dim found As Boolean =
                    False

                For Each summary As JsonElement In
                    summaries.EnumerateArray()

                    Dim displayIndexText As String =
                        ReadString(
                            summary,
                            "display-index"
                        )

                    Dim displayIndex As Integer =
                        0

                    Integer.TryParse(
                        displayIndexText,
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        displayIndex
                    )

                    If Not found OrElse
                       displayIndex > preferredIndex Then

                        preferred =
                            summary

                        preferredIndex =
                            displayIndex

                        found =
                            True

                    End If

                Next

                If Not found Then
                    Continue For
                End If

                Dim work As New OrcidWorkSuggestion()

                Dim putCode As Long
                Dim putCodeElement As JsonElement

                If preferred.TryGetProperty(
                    "put-code",
                    putCodeElement
                ) Then

                    If putCodeElement.ValueKind =
                       JsonValueKind.Number Then

                        putCodeElement.TryGetInt64(
                            putCode
                        )

                    ElseIf putCodeElement.ValueKind =
                           JsonValueKind.String Then

                        Long.TryParse(
                            putCodeElement.GetString(),
                            putCode
                        )

                    End If

                End If

                work.PutCode =
                    putCode

                work.WorkType =
                    ReadString(
                        preferred,
                        "type"
                    )

                work.PublishedDate =
                    ReadPartialDate(
                        preferred,
                        "publication-date"
                    )

                work.Doi =
                    ReadExternalIdentifier(
                        preferred,
                        "doi"
                    )

                work.JournalTitle =
                    ReadNestedValue(
                        preferred,
                        "journal-title"
                    )

                Dim titleContainer As JsonElement

                If preferred.TryGetProperty(
                    "title",
                    titleContainer
                ) AndAlso
                   titleContainer.ValueKind =
                    JsonValueKind.Object Then

                    work.Title =
                        ReadNestedValue(
                            titleContainer,
                            "title"
                        )

                End If

                If Not String.IsNullOrWhiteSpace(
                    work.Title
                ) Then

                    results.Add(
                        work
                    )

                End If

            Next

            Return results

        End Function


        Private Shared Function ReadExternalIdentifier(
            parent As JsonElement,
            requestedType As String
        ) As String

            Dim externalIds As JsonElement

            If Not parent.TryGetProperty(
                "external-ids",
                externalIds
            ) OrElse
               externalIds.ValueKind <>
                JsonValueKind.Object Then

                Return String.Empty

            End If

            Dim identifiers As JsonElement

            If Not externalIds.TryGetProperty(
                "external-id",
                identifiers
            ) OrElse
               identifiers.ValueKind <>
                JsonValueKind.Array Then

                Return String.Empty

            End If

            For Each identifier As JsonElement In
                identifiers.EnumerateArray()

                Dim typeValue As String =
                    ReadString(
                        identifier,
                        "external-id-type"
                    )

                If String.Equals(
                    typeValue,
                    requestedType,
                    StringComparison.OrdinalIgnoreCase
                ) Then

                    Return ReadString(
                        identifier,
                        "external-id-value"
                    )

                End If

            Next

            Return String.Empty

        End Function


        Private Shared Function ReadPartialDate(
            parent As JsonElement,
            propertyName As String
        ) As DateTime?

            Dim dateContainer As JsonElement

            If Not parent.TryGetProperty(
                propertyName,
                dateContainer
            ) OrElse
               dateContainer.ValueKind <>
                JsonValueKind.Object Then

                Return Nothing

            End If

            Dim yearText As String =
                ReadNestedValue(
                    dateContainer,
                    "year"
                )

            Dim monthText As String =
                ReadNestedValue(
                    dateContainer,
                    "month"
                )

            Dim dayText As String =
                ReadNestedValue(
                    dateContainer,
                    "day"
                )

            Dim year As Integer

            If Not Integer.TryParse(
                yearText,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                year
            ) OrElse
               year < 1 OrElse
               year > 9999 Then

                Return Nothing

            End If

            Dim month As Integer =
                1

            Dim day As Integer =
                1

            If Not String.IsNullOrWhiteSpace(
                monthText
            ) Then

                Integer.TryParse(
                    monthText,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    month
                )

            End If

            If month < 1 OrElse month > 12 Then
                month = 1
            End If

            If Not String.IsNullOrWhiteSpace(
                dayText
            ) Then

                Integer.TryParse(
                    dayText,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    day
                )

            End If

            Dim maxDay As Integer =
                DateTime.DaysInMonth(
                    year,
                    month
                )

            If day < 1 OrElse day > maxDay Then
                day = 1
            End If

            Return New DateTime(
                year,
                month,
                day
            )

        End Function


        Private Shared Function ReadNestedValue(
            parent As JsonElement,
            propertyName As String
        ) As String

            Dim nested As JsonElement

            If Not parent.TryGetProperty(
                propertyName,
                nested
            ) OrElse
               nested.ValueKind <>
                JsonValueKind.Object Then

                Return String.Empty

            End If

            Return ReadString(
                nested,
                "value"
            )

        End Function


        Private Shared Function ReadString(
            parent As JsonElement,
            propertyName As String
        ) As String

            Dim element As JsonElement

            If Not parent.TryGetProperty(
                propertyName,
                element
            ) Then

                Return String.Empty

            End If

            If element.ValueKind =
               JsonValueKind.String Then

                Return If(
                    element.GetString(),
                    String.Empty
                ).Trim()

            End If

            If element.ValueKind =
               JsonValueKind.Number Then

                Return element.GetRawText()

            End If

            Return String.Empty

        End Function

    End Class

End Namespace
