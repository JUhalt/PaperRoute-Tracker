Imports System
Imports System.Collections.Generic
Imports System.Globalization
Imports System.Linq
Imports System.Text
Imports System.Text.Json
Imports System.Text.RegularExpressions
Imports ManuscriptPipeline.Models

Namespace Services

    ' A comment the assistant found in a decision letter (#84), checked
    ' against the letter: Found means its text appears there word for word
    ' (ignoring spacing, quote marks, and dashes), and Text is then the
    ' letter's own text.
    Public NotInheritable Class LetterCommentCandidate

        Public Property ReviewerLabel As String = String.Empty

        Public Property Text As String = String.Empty

        Public Property Found As Boolean

        ' Where it is in the letter, when found.
        Public Property SourceStart As Integer = -1

        Public Property SourceLength As Integer

    End Class


    ' What the assistant read in a decision letter (#84): a proposal to
    ' check, never a change. Dates are checked here, and a deadline given
    ' as a number of days is worked out on this computer.
    Public NotInheritable Class DecisionLetterProposal

        ' Nothing when the letter doesn't state it plainly.
        Public Property Decision As EditorialDecision?

        ' The letter's sentence stating it, when found there.
        Public Property DecisionQuote As String = String.Empty

        Public Property DecisionDate As DateTime?

        Public Property RevisionDeadline As DateTime?

        Public Property DeadlineQuote As String = String.Empty

        ' "Stated in the letter" or "60 days after Sep 30, 2026".
        Public Property DeadlineBasis As String = String.Empty

        ' The period the letter gave, when the deadline was worked out from
        ' one, so it can follow a corrected decision date.
        Public Property DeadlineDays As Integer?

        ' A date the letter gave that is too far from today to use; no
        ' deadline is worked out then.
        Public Property LetterDateNotUsed As DateTime?

        ' The letter gave a deadline that wasn't worked out, because its
        ' date wasn't used.
        Public Property DeadlineNotUsed As Boolean

        Public Property Comments As New List(Of LetterCommentCandidate)()

        Public Property ProviderName As String = String.Empty

        Public Property Model As String = String.Empty

        Public Property SuggestedUtc As DateTime = DateTime.UtcNow

        ' The answer stopped at its length limit, so comments may be missing.
        Public Property Truncated As Boolean

    End Class


    ' A requirement the assistant proposed from a journal's instructions for
    ' authors (#95), checked against the text the researcher pasted.
    Public NotInheritable Class RequirementCandidate

        ' A short requirement, such as "Abstract of at most 250 words".
        Public Property Title As String = String.Empty

        ' One of the checklist's categories, or "" for General.
        Public Property Category As String = String.Empty

        Public Property IsRequired As Boolean = True

        ' The article type it is for, as the text names it, or "" for every
        ' type.
        Public Property AppliesTo As String = String.Empty

        ' The text's own words for it when found there; otherwise the quote
        ' the assistant gave, which is shown but never recorded as a source.
        Public Property Quote As String = String.Empty

        Public Property Found As Boolean

        Public Property SourceStart As Integer = -1

        Public Property SourceLength As Integer

        ' Every number in the requirement appears in its quote.
        Public Property NumbersMatch As Boolean = True

    End Class


    ' What the assistant read in a journal's instructions for authors (#95):
    ' proposals to check, never changes.
    Public NotInheritable Class AuthorInstructionsProposal

        Public Property Requirements As New List(Of RequirementCandidate)()

        Public Property ProviderName As String = String.Empty

        Public Property Model As String = String.Empty

        Public Property SuggestedUtc As DateTime = DateTime.UtcNow

        ' The answer stopped at its length limit, so requirements may be
        ' missing.
        Public Property Truncated As Boolean

    End Class


    ' The AI assistant's features (#84, #95): what each sends, in the same
    ' functions that build what the researcher sees before sending, and how
    ' each answer is read and checked.
    Public NotInheritable Class AssistantService

        Private Sub New()
        End Sub

        Public Const DecisionLetterFeature As String = "decision-letter"
        Public Const DraftResponseFeature As String = "draft-response"
        Public Const CoverLetterFeature As String = "cover-letter"
        Public Const AuthorInstructionsFeature As String = "author-instructions"

        Public Const MaximumLetterLength As Integer = 60000

        ' Kept short, so a model on this computer reads all of it and each
        ' answer stays reviewable: one section of the instructions at a time.
        Public Const MaximumInstructionsLength As Integer = 20000

        ' Test seam: the provider for the assistant as set up now.
        Friend Shared ProviderFactory As Func(Of IAssistantProvider) = Nothing


        ' The provider for the assistant as set up now, or Nothing when off.
        Public Shared Function CurrentProvider() As IAssistantProvider
            If ProviderFactory IsNot Nothing Then Return ProviderFactory()
            Dim connection As AssistantConnection = OnlineAccess.CurrentAssistant()
            If connection Is Nothing Then Return Nothing
            If connection.Provider = AssistantProvider.Claude Then Return New ClaudeAssistantProvider(connection.Model)
            Return New CompatibleAssistantProvider(connection.Endpoint, connection.Model)
        End Function


        Public Shared Function FeatureName(feature As String) As String
            Select Case feature
                Case DecisionLetterFeature : Return "Read Decision Letter"
                Case DraftResponseFeature : Return "Suggest a Starting Point"
                Case CoverLetterFeature : Return "Draft Cover Letter"
                Case AuthorInstructionsFeature : Return "Read Author Instructions"
                Case Else : Return feature
            End Select
        End Function


        ' Exactly what a request sends, for the researcher to read first.
        Public Shared Function WhatIsSent(request As AssistantRequest) As String
            Return "PaperRoute's instructions:" & Environment.NewLine & request.Instructions & Environment.NewLine & Environment.NewLine &
                "Your text:" & Environment.NewLine & request.Content &
                If(request.Schema IsNot Nothing, Environment.NewLine & Environment.NewLine & "PaperRoute also asks for the answer in a fixed format (JSON), which it checks before showing it.", String.Empty)
        End Function


        ' ---------------------------------------------------------------
        ' Decision letters
        ' ---------------------------------------------------------------

        Friend Const LetterInstructions As String =
            "You read a journal's decision letter for the researcher who received it, and answer with JSON only." & vbLf &
            "- decision: the editor's decision, one of accepted, minor_revision, major_revision, revise_and_resubmit, rejected_after_review, desk_rejected, rejected, or unclear. Use unclear unless the letter states the decision plainly. ""Reject and resubmit"" or a rejection that invites a new submission is revise_and_resubmit; ""accept pending minor revisions"" or a conditional acceptance is minor_revision; a rejection without peer review is desk_rejected; a rejection after peer review is rejected_after_review." & vbLf &
            "- decision_quote: the sentence that states the decision, copied exactly from the letter, or an empty string." & vbLf &
            "- decision_date: the date written on the letter as YYYY-MM-DD, or an empty string." & vbLf &
            "- deadline_date: a revision deadline written in the letter as a date, as YYYY-MM-DD, or an empty string." & vbLf &
            "- deadline_days: a revision deadline written as a period (for example ""within 60 days"", ""6 weeks"", ""3 months""), in days (a week is 7 days, a month 30), or 0." & vbLf &
            "- deadline_quote: the sentence that states the deadline, copied exactly from the letter, or an empty string." & vbLf &
            "- comments: every comment asking the authors to change, add, clarify, or answer something, from each reviewer and from the editor. Copy each comment's text exactly as it appears in the letter; do not paraphrase, shorten, correct, or translate it. Split a report into separate comments where the reviewer makes separate points, such as numbered items or separate paragraphs. Leave out greetings, signatures, submission instructions, and remarks that ask for nothing. For each, reviewer is the label the letter uses, such as ""Reviewer 1"", ""Referee 2"", ""Editor"", or ""Associate Editor""." & vbLf &
            "Never invent a comment, label, date, or deadline."

        Friend Const LetterSchemaJson As String =
            "{""type"":""object"",""properties"":{" &
            """decision"":{""type"":""string"",""enum"":[""accepted"",""minor_revision"",""major_revision"",""revise_and_resubmit"",""rejected_after_review"",""desk_rejected"",""rejected"",""unclear""]}," &
            """decision_quote"":{""type"":""string""}," &
            """decision_date"":{""type"":""string""}," &
            """deadline_date"":{""type"":""string""}," &
            """deadline_days"":{""type"":""integer""}," &
            """deadline_quote"":{""type"":""string""}," &
            """comments"":{""type"":""array"",""items"":{""type"":""object"",""properties"":{""reviewer"":{""type"":""string""},""text"":{""type"":""string""}},""required"":[""reviewer"",""text""],""additionalProperties"":false}}" &
            "},""required"":[""decision"",""decision_quote"",""decision_date"",""deadline_date"",""deadline_days"",""deadline_quote"",""comments""],""additionalProperties"":false}"


        Public Shared Function BuildLetterRequest(letter As String) As AssistantRequest
            Return New AssistantRequest With {
                .Feature = DecisionLetterFeature,
                .Instructions = LetterInstructions,
                .Content = If(letter, String.Empty).Trim(),
                .Schema = SchemaOf(LetterSchemaJson),
                .MaxTokens = 16000
            }
        End Function


        ' The answer, checked against the letter. today bounds the dates.
        Public Shared Function ReadLetterReply(reply As AssistantReply, letter As String, today As DateTime) As DecisionLetterProposal

            Dim proposal As New DecisionLetterProposal With {
                .ProviderName = If(reply?.ProviderName, String.Empty),
                .Model = If(reply?.Model, String.Empty),
                .Truncated = reply IsNot Nothing AndAlso reply.Truncated
            }
            Dim source As String = If(letter, String.Empty)

            ' An answer that stopped at its length limit ends mid-sentence, so
            ' it can't be read; say that, rather than calling it malformed.
            Dim document As JsonDocument
            Try
                document = ParseJsonAnswer(If(reply?.Text, String.Empty))
            Catch ex As AssistantException When proposal.Truncated
                Throw New AssistantException("The answer was cut short before it finished. Paste the letter in parts, such as one reviewer at a time. Nothing was changed.", ex)
            End Try

            Using document
                Dim root As JsonElement = document.RootElement

                proposal.Decision = DecisionFor(JsonFacts.RawText(root, "decision"))
                proposal.DecisionQuote = LetterTextOf(source, JsonFacts.RawText(root, "decision_quote"))

                ' A date far from today is not used, and no deadline is
                ' worked out from it: the researcher sets both.
                Dim letterDate As DateTime? = DateOf(JsonFacts.RawText(root, "decision_date"))
                If letterDate.HasValue AndAlso (letterDate.Value < today.Date.AddYears(-3) OrElse letterDate.Value > today.Date.AddDays(31)) Then
                    proposal.LetterDateNotUsed = letterDate
                    letterDate = Nothing
                End If
                proposal.DecisionDate = letterDate

                If Not proposal.LetterDateNotUsed.HasValue Then
                    Dim basis As DateTime = If(letterDate, today.Date)
                    Dim deadline As DateTime? = DateOf(JsonFacts.RawText(root, "deadline_date"))
                    If deadline.HasValue AndAlso (deadline.Value < basis OrElse deadline.Value > basis.AddYears(2)) Then deadline = Nothing
                    If deadline.HasValue Then
                        proposal.DeadlineBasis = "Stated in the letter"
                    Else
                        Dim days As Integer? = JsonFacts.Whole(root, "deadline_days")
                        If days.HasValue AndAlso days.Value >= 1 AndAlso days.Value <= 730 Then
                            deadline = basis.AddDays(days.Value)
                            proposal.DeadlineDays = days
                            proposal.DeadlineBasis = PeriodBasis(days.Value, basis, If(letterDate.HasValue, "the letter's date", "today"))
                        End If
                    End If
                    proposal.RevisionDeadline = deadline
                    If deadline.HasValue Then proposal.DeadlineQuote = LetterTextOf(source, JsonFacts.RawText(root, "deadline_quote"))
                Else
                    ' Only whether the letter gave a deadline, and its
                    ' sentence, so the researcher is asked to set one only
                    ' when there is one.
                    Dim givenDays As Integer? = JsonFacts.Whole(root, "deadline_days")
                    proposal.DeadlineNotUsed = DateOf(JsonFacts.RawText(root, "deadline_date")).HasValue OrElse
                        (givenDays.HasValue AndAlso givenDays.Value >= 1 AndAlso givenDays.Value <= 730)
                    If proposal.DeadlineNotUsed Then proposal.DeadlineQuote = LetterTextOf(source, JsonFacts.RawText(root, "deadline_quote"))
                End If

                Dim seen As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
                For Each item As JsonElement In JsonFacts.Items(root, "comments")
                    Dim text As String = JsonFacts.RawText(item, "text")
                    Dim label As String = Clean(JsonFacts.RawText(item, "reviewer"), 80)
                    If text.Length = 0 Then Continue For
                    Dim place = LocateExcerpt(source, text)
                    Dim candidate As New LetterCommentCandidate With {
                        .ReviewerLabel = If(label.Length > 0, label, "Reviewer"),
                        .Found = place.HasValue,
                        .SourceStart = If(place.HasValue, place.Value.Start, -1),
                        .SourceLength = If(place.HasValue, place.Value.Length, 0),
                        .Text = If(place.HasValue, source.Substring(place.Value.Start, place.Value.Length).Trim(), text.Trim())
                    }
                    If seen.Add(candidate.ReviewerLabel & "|" & Normalized(candidate.Text).Text) Then proposal.Comments.Add(candidate)
                Next
            End Using

            Return proposal

        End Function


        ' "60 days after Sep 15, 2026 (the letter's date)".
        Public Shared Function PeriodBasis(days As Integer, basis As DateTime, basisName As String) As String
            Return days.ToString("N0", CultureInfo.CurrentCulture) & If(days = 1, " day", " days") & " after " &
                basis.ToString("MMM d, yyyy", CultureInfo.CurrentCulture) & " (" & basisName & ")"
        End Function


        Public Shared Function DecisionFor(value As String) As EditorialDecision?
            Select Case If(value, String.Empty).Trim().ToLowerInvariant()
                Case "accepted" : Return EditorialDecision.Accepted
                Case "minor_revision" : Return EditorialDecision.MinorRevision
                Case "major_revision" : Return EditorialDecision.MajorRevision
                Case "revise_and_resubmit" : Return EditorialDecision.ReviseAndResubmit
                Case "rejected_after_review" : Return EditorialDecision.RejectedAfterReview
                Case "desk_rejected" : Return EditorialDecision.DeskRejected
                Case "rejected" : Return EditorialDecision.Rejected
                Case Else : Return Nothing
            End Select
        End Function


        ' Where an excerpt is in the letter, ignoring differences in spacing,
        ' letter case, quote marks, and dashes; Nothing when it isn't there.
        Public Shared Function LocateExcerpt(letter As String, excerpt As String) As (Start As Integer, Length As Integer)?
            Dim haystack = Normalized(If(letter, String.Empty))
            Dim needle As String = Normalized(If(excerpt, String.Empty)).Text.Trim()
            If needle.Length < 3 Then Return Nothing
            Dim index As Integer = haystack.Text.IndexOf(needle, StringComparison.OrdinalIgnoreCase)
            If index < 0 Then Return Nothing
            Dim start As Integer = haystack.Map(index)
            Dim finish As Integer = haystack.Map(index + needle.Length - 1) + 1
            Return (start, finish - start)
        End Function


        ' The letter's own sentence for a quote, or "" when it isn't there.
        Private Shared Function LetterTextOf(letter As String, quote As String) As String
            Dim place = LocateExcerpt(letter, quote)
            Return If(place.HasValue, letter.Substring(place.Value.Start, place.Value.Length).Trim(), String.Empty)
        End Function


        ' Text with spacing collapsed and quotes and dashes made plain, and
        ' for each of its characters the index it came from.
        Private Shared Function Normalized(text As String) As (Text As String, Map As List(Of Integer))
            Dim builder As New StringBuilder(text.Length)
            Dim map As New List(Of Integer)(text.Length)
            Dim spacePending As Boolean = False
            For index As Integer = 0 To text.Length - 1
                Dim c As Char = text(index)
                If Char.IsWhiteSpace(c) Then
                    spacePending = builder.Length > 0
                    Continue For
                End If
                If spacePending Then
                    builder.Append(" "c)
                    map.Add(index - 1)
                    spacePending = False
                End If
                Select Case c
                    Case ChrW(&H2018), ChrW(&H2019), ChrW(&H201A), ChrW(&H2032) : c = "'"c
                    Case ChrW(&H201C), ChrW(&H201D), ChrW(&H201E), ChrW(&H2033) : c = """"c
                    Case ChrW(&H2010), ChrW(&H2011), ChrW(&H2012), ChrW(&H2013), ChrW(&H2014), ChrW(&H2212) : c = "-"c
                End Select
                builder.Append(c)
                map.Add(index)
            Next
            Return (builder.ToString(), map)
        End Function


        ' ---------------------------------------------------------------
        ' A starting point for one response
        ' ---------------------------------------------------------------

        Friend Const ResponseInstructions As String =
            "You help a researcher start a response to one reviewer comment on their manuscript. Write a first draft of the reply to the reviewer, in the first person plural, polite and specific, for the researcher to rewrite." & vbLf &
            "Say a change was made only if the researcher's planned action says so. Where something is unknown, write a placeholder in square brackets, such as [describe the change] or [page and line], instead of inventing results, changes, page numbers, or citations." & vbLf &
            "Answer with the reply text only, without a heading or the comment itself."


        Public Shared Function BuildResponseRequest(reviewerLabel As String, comment As String, action As String) As AssistantRequest
            Dim content As New StringBuilder()
            content.Append("Reviewer: ").Append(If(String.IsNullOrWhiteSpace(reviewerLabel), "Reviewer", reviewerLabel.Trim())).Append(vbLf).Append(vbLf)
            content.Append("Comment:").Append(vbLf).Append(If(comment, String.Empty).Trim()).Append(vbLf).Append(vbLf)
            content.Append("Planned action (from the researcher):").Append(vbLf).Append(If(String.IsNullOrWhiteSpace(action), "(none yet)", action.Trim()))
            Return New AssistantRequest With {
                .Feature = DraftResponseFeature,
                .Instructions = ResponseInstructions,
                .Content = content.ToString(),
                .MaxTokens = 4000
            }
        End Function


        ' ---------------------------------------------------------------
        ' A starting point for a cover letter
        ' ---------------------------------------------------------------

        Friend Const CoverLetterInstructions As String =
            "You help a researcher start a cover letter for submitting their manuscript to a journal. Write a concise, formal letter to the editor of under 350 words, using only the details given." & vbLf &
            "Don't invent findings, numbers, co-authors, funding, or claims. Where the researcher must add something, write a placeholder in square brackets, such as [Editor's name] or [one sentence on the main finding], and end with [Your name] and [Your affiliation]." & vbLf &
            "Answer with the letter text only."


        Public Shared Function BuildCoverLetterRequest(title As String, journal As String, workType As String, keywords As IEnumerable(Of String), abstractText As String, Optional journalFacts As String = Nothing) As AssistantRequest
            Dim content As New StringBuilder()
            content.Append("Journal: ").Append(If(String.IsNullOrWhiteSpace(journal), "[not chosen yet]", journal.Trim())).Append(vbLf)
            If Not String.IsNullOrWhiteSpace(journalFacts) Then content.Append("About the journal: ").Append(journalFacts.Trim()).Append(vbLf)
            If Not String.IsNullOrWhiteSpace(workType) Then content.Append("Type of work: ").Append(workType.Trim()).Append(vbLf)
            content.Append("Title: ").Append(If(title, String.Empty).Trim()).Append(vbLf)
            Dim words As List(Of String) = If(keywords, Enumerable.Empty(Of String)()).Where(Function(item) Not String.IsNullOrWhiteSpace(item)).Select(Function(item) item.Trim()).ToList()
            If words.Count > 0 Then content.Append("Keywords: ").Append(String.Join(", ", words)).Append(vbLf)
            content.Append(vbLf).Append("Abstract:").Append(vbLf).Append(If(String.IsNullOrWhiteSpace(abstractText), "(none given)", abstractText.Trim()))
            Return New AssistantRequest With {
                .Feature = CoverLetterFeature,
                .Instructions = CoverLetterInstructions,
                .Content = content.ToString(),
                .MaxTokens = 4000
            }
        End Function


        ' ---------------------------------------------------------------
        ' A journal's instructions for authors (#95)
        ' ---------------------------------------------------------------

        ' The categories PaperRoute suggests for checklist requirements.
        Public Shared ReadOnly StandardCategories As String() = {"Manuscript", "Editorial", "Compliance", "Files", "Figures"}


        ' The categories a requirement may take: the standard ones, then any
        ' the journal's checklist already uses. Only the standard ones are
        ' sent; an answer is matched to the checklist's own on this computer.
        Public Shared Function CategoriesFor(existing As IEnumerable(Of String)) As List(Of String)
            Dim categories As New List(Of String)(StandardCategories)
            For Each name As String In If(existing, Enumerable.Empty(Of String)())
                Dim cleaned As String = Clean(name, 80)
                If cleaned.Length > 0 AndAlso Not categories.Contains(cleaned, StringComparer.OrdinalIgnoreCase) Then categories.Add(cleaned)
            Next
            Return categories
        End Function


        Friend Shared ReadOnly AuthorInstructionsPrompt As String =
            "You read the instructions for authors that a journal publishes, for a researcher preparing a submission, and answer with JSON only." & vbLf &
            "- requirements: each thing an author must prepare, include, or state when submitting, such as word, abstract, or figure limits; the format of the title page, abstract, or keywords; the reference style; required statements, such as data availability, conflicts of interest, funding, ethics approval, or author contributions; files and figures to upload; anonymizing the manuscript; and reporting guidelines. Leave out the journal's publishing terms, such as fees, licenses, or copyright, and anything about peer review or the editorial process." & vbLf &
            "- title: the requirement in a few plain words, such as ""Abstract of at most 250 words"". Keep every number exactly as the text gives it." & vbLf &
            "- category: one of " & String.Join(", ", StandardCategories) & "." & vbLf &
            "- required: true when the text requires it, false when the text calls it optional or recommended." & vbLf &
            "- applies_to: the article type the requirement is for, as the text names it, or an empty string when it is for every type." & vbLf &
            "- quote: the sentence or table text that states the requirement, copied exactly from the text; do not paraphrase, shorten, correct, or translate it." & vbLf &
            "If an article type is given, list the requirements for that type and for every type, and leave out those only for other types." & vbLf &
            "Never invent a requirement, number, or quote."

        Friend Const AuthorInstructionsSchemaJson As String =
            "{""type"":""object"",""properties"":{" &
            """requirements"":{""type"":""array"",""items"":{""type"":""object"",""properties"":{" &
            """title"":{""type"":""string""},""category"":{""type"":""string""},""required"":{""type"":""boolean""}," &
            """applies_to"":{""type"":""string""},""quote"":{""type"":""string""}}," &
            """required"":[""title"",""category"",""required"",""applies_to"",""quote""],""additionalProperties"":false}}" &
            "},""required"":[""requirements""],""additionalProperties"":false}"


        ' What Read Author Instructions sends: the pasted text, and the article
        ' type when one is given. Nothing from the library, not even the
        ' checklist's own category names.
        Public Shared Function BuildAuthorInstructionsRequest(text As String, articleType As String) As AssistantRequest
            Dim kind As String = Clean(articleType, 80)
            Dim content As String = If(text, String.Empty).Trim()
            If kind.Length > 0 Then content = "Article type: " & kind & vbLf & vbLf & content
            Return New AssistantRequest With {
                .Feature = AuthorInstructionsFeature,
                .Instructions = AuthorInstructionsPrompt,
                .Content = content,
                .Schema = SchemaOf(AuthorInstructionsSchemaJson),
                .MaxTokens = 8000
            }
        End Function


        ' The answer, each requirement checked against the pasted text: its
        ' quote word for word, and every number it gives in that quote.
        Public Shared Function ReadAuthorInstructionsReply(reply As AssistantReply, text As String, categories As IEnumerable(Of String)) As AuthorInstructionsProposal

            Dim proposal As New AuthorInstructionsProposal With {
                .ProviderName = If(reply?.ProviderName, String.Empty),
                .Model = If(reply?.Model, String.Empty),
                .Truncated = reply IsNot Nothing AndAlso reply.Truncated
            }
            Dim source As String = If(text, String.Empty)
            Dim allowed As List(Of String) = If(categories, StandardCategories).ToList()

            Dim document As JsonDocument
            Try
                document = ParseJsonAnswer(If(reply?.Text, String.Empty))
            Catch ex As AssistantException When proposal.Truncated
                Throw New AssistantException("The answer was cut short before it finished. Paste the instructions in parts, such as one section at a time. Nothing was changed.", ex)
            End Try

            Using document
                Dim seen As New HashSet(Of String)(StringComparer.Ordinal)
                For Each item As JsonElement In JsonFacts.Items(document.RootElement, "requirements")
                    Dim title As String = Clean(JsonFacts.RawText(item, "title"), 200)
                    If title.Length = 0 Then Continue For
                    Dim appliesTo As String = Clean(JsonFacts.RawText(item, "applies_to"), 80)
                    If ForEveryType(appliesTo) Then appliesTo = String.Empty
                    Dim quote As String = JsonFacts.RawText(item, "quote")
                    Dim place = LocateExcerpt(source, quote)
                    Dim candidate As New RequirementCandidate With {
                        .Title = title,
                        .Category = CategoryFor(JsonFacts.RawText(item, "category"), allowed),
                        .IsRequired = JsonFacts.Flag(item, "required").GetValueOrDefault(True),
                        .AppliesTo = appliesTo,
                        .Found = place.HasValue,
                        .SourceStart = If(place.HasValue, place.Value.Start, -1),
                        .SourceLength = If(place.HasValue, place.Value.Length, 0),
                        .Quote = If(place.HasValue, source.Substring(place.Value.Start, place.Value.Length).Trim(), Clean(quote, 600))
                    }
                    candidate.NumbersMatch = Not candidate.Found OrElse NumbersAppearIn(candidate.Title, candidate.Quote)
                    If seen.Add(RequirementKey(candidate.Title) & "|" & RequirementKey(candidate.AppliesTo)) Then proposal.Requirements.Add(candidate)
                Next
            End Using

            Return proposal

        End Function


        ' Two requirement titles that differ only in spacing, letter case,
        ' quote marks, or dashes are the same requirement.
        Public Shared Function SameRequirement(first As String, second As String) As Boolean
            Return String.Equals(RequirementKey(first), RequirementKey(second), StringComparison.Ordinal)
        End Function


        ' The instructions a requirement keeps: the text's own words, and the
        ' article type it is for. A requirement not found in the text keeps no
        ' quote, so nothing unverified is recorded as the journal's.
        Public Shared Function RequirementDescription(candidate As RequirementCandidate) As String
            If candidate Is Nothing Then Return String.Empty
            Dim lines As New List(Of String)()
            If candidate.Found AndAlso candidate.Quote.Length > 0 Then lines.Add(candidate.Quote)
            If candidate.AppliesTo.Length > 0 Then lines.Add("Applies to: " & candidate.AppliesTo & ".")
            Return String.Join(Environment.NewLine, lines)
        End Function


        ' Every number in the requirement appears in the quote, reading
        ' "8,000", "8 000", and "8000" alike, and "six" as 6.
        Public Shared Function NumbersAppearIn(requirement As String, quote As String) As Boolean
            Dim available As HashSet(Of String) = NumbersIn(quote, withParts:=True)
            Return NumbersIn(requirement, withParts:=False).All(Function(number) available.Contains(number))
        End Function


        Private Shared ReadOnly NumberWords As String() = {
            "zero", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten",
            "eleven", "twelve", "thirteen", "fourteen", "fifteen", "sixteen", "seventeen", "eighteen", "nineteen"
        }

        Private Shared ReadOnly TensWords As String() = {"twenty", "thirty", "forty", "fifty", "sixty", "seventy", "eighty", "ninety"}


        ' The numbers in a text: words such as "six" or "twenty-five" as
        ' digits, and a number grouped in thousands with one separator, such as
        ' "8,000" or "8 000", read whole. withParts also keeps each group, so a
        ' table row pasted as "3,000 150" still holds 150.
        Private Shared Function NumbersIn(text As String, withParts As Boolean) As HashSet(Of String)
            Dim tens As String = String.Join("|", TensWords)
            Dim units As String = String.Join("|", NumberWords.Skip(1).Take(9))
            Dim value As String = Regex.Replace(If(text, String.Empty), "\b(" & tens & ")[- ](" & units & ")\b",
                Function(found) (20 + 10 * Array.IndexOf(TensWords, found.Groups(1).Value.ToLowerInvariant()) +
                                 Array.IndexOf(NumberWords, found.Groups(2).Value.ToLowerInvariant())).ToString(CultureInfo.InvariantCulture),
                RegexOptions.IgnoreCase)
            value = Regex.Replace(value, "\b(" & String.Join("|", NumberWords) & "|" & tens & ")\b",
                Function(found)
                    Dim word As String = found.Value.ToLowerInvariant()
                    Dim small As Integer = Array.IndexOf(NumberWords, word)
                    Return If(small >= 0, small, 20 + 10 * Array.IndexOf(TensWords, word)).ToString(CultureInfo.InvariantCulture)
                End Function,
                RegexOptions.IgnoreCase)
            Dim joined As String = Regex.Replace(value, "(?<![\d.,])\d{1,3}(?<sep>[, \u00A0\u202F\u2009\u2007])\d{3}(?:\k<sep>\d{3})*(?!\d)",
                Function(found) Regex.Replace(found.Value, "\D", String.Empty))
            Dim numbers As New HashSet(Of String)(StringComparer.Ordinal)
            For Each found As Match In Regex.Matches(joined, "\d+(?:\.\d+)?")
                numbers.Add(found.Value)
            Next
            If withParts Then
                For Each found As Match In Regex.Matches(value, "\d+(?:\.\d+)?")
                    numbers.Add(found.Value)
                Next
            End If
            Return numbers
        End Function


        Private Shared Function RequirementKey(value As String) As String
            Return Normalized(Clean(value, 400)).Text.ToLowerInvariant()
        End Function


        Private Shared Function ForEveryType(appliesTo As String) As Boolean
            Select Case appliesTo.Trim().TrimEnd("."c).ToLowerInvariant()
                Case "all", "any", "every type", "all types", "all article types", "all articles", "all manuscripts", "all submissions", "every article", "any article"
                    Return True
                Case Else
                    Return False
            End Select
        End Function


        Private Shared Function CategoryFor(value As String, allowed As IEnumerable(Of String)) As String
            Dim name As String = Clean(value, 80)
            Dim match As String = allowed.FirstOrDefault(Function(category) String.Equals(category, name, StringComparison.OrdinalIgnoreCase))
            Return If(match, String.Empty)
        End Function


        ' A free-text answer, trimmed, without Markdown code fences.
        Public Shared Function TextOf(reply As AssistantReply) As String
            Dim text As String = If(reply?.Text, String.Empty).Trim()
            Dim fenced As Match = Regex.Match(text, "^```[a-zA-Z]*\s*\n([\s\S]*?)\n```\s*$")
            If fenced.Success Then text = fenced.Groups(1).Value.Trim()
            If text.Length = 0 Then Throw New AssistantException("The answer was empty. Nothing was changed.")
            Return text
        End Function


        ' Where a suggestion came from, for the record the researcher accepts.
        Public Shared Function SuggestionFor(feature As String, reply As AssistantReply, sourceText As String, suggestedUtc As DateTime) As AssistantSuggestion
            Return AssistantSuggestionService.Normalize(New AssistantSuggestion With {
                .Feature = feature,
                .Provider = If(reply?.ProviderName, String.Empty),
                .Model = If(reply?.Model, String.Empty),
                .SuggestedUtc = suggestedUtc,
                .SourceText = If(sourceText, String.Empty)
            })
        End Function


        ' ---------------------------------------------------------------
        ' Helpers
        ' ---------------------------------------------------------------

        Friend Shared Function SchemaOf(json As String) As Dictionary(Of String, JsonElement)
            Return JsonSerializer.Deserialize(Of Dictionary(Of String, JsonElement))(json)
        End Function


        ' The JSON object in an answer, even inside a Markdown code fence or
        ' after a sentence of preamble from a model that ignored the format.
        Friend Shared Function ParseJsonAnswer(text As String) As JsonDocument
            Dim trimmed As String = If(text, String.Empty).Trim()
            Dim start As Integer = trimmed.IndexOf("{"c)
            Dim finish As Integer = trimmed.LastIndexOf("}"c)
            If start < 0 OrElse finish <= start Then Throw New AssistantException("The answer wasn't in the expected format. Nothing was changed.")
            Try
                Dim document As JsonDocument = JsonDocument.Parse(trimmed.Substring(start, finish - start + 1))
                If document.RootElement.ValueKind <> JsonValueKind.Object Then
                    document.Dispose()
                    Throw New AssistantException("The answer wasn't in the expected format. Nothing was changed.")
                End If
                Return document
            Catch ex As JsonException
                Throw New AssistantException("The answer wasn't in the expected format. Nothing was changed.", ex)
            End Try
        End Function


        Private Shared Function DateOf(value As String) As DateTime?
            Dim parsed As DateTime
            If DateTime.TryParseExact(If(value, String.Empty).Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, parsed) Then Return parsed.Date
            Return Nothing
        End Function


        Private Shared Function Clean(value As String, length As Integer) As String
            Dim text As String = Regex.Replace(If(value, String.Empty), "\s+", " ").Trim()
            Return If(text.Length <= length, text, text.Substring(0, length))
        End Function

    End Class

End Namespace
