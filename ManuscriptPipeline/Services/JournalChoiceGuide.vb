Imports System
Imports System.Collections.Generic
Imports System.Globalization
Imports System.Linq
Imports ManuscriptPipeline.Models

Namespace Services

    Public NotInheritable Class JournalCheck

        Public Sub New(id As String, text As String)
            Me.Id = id
            Me.Text = text
        End Sub

        Public ReadOnly Property Id As String

        Public ReadOnly Property Text As String

    End Class


    ' The questions for choosing a journal (#89), answered per shortlisted
    ' journal. The trust questions are adapted from the Think. Check. Submit.
    ' checklist for journals (CC BY 4.0); the fit questions are PaperRoute's
    ' own. Ids are stored, so they never change once released.
    Public NotInheritable Class JournalChoiceGuide

        Private Sub New()
        End Sub

        Public Const TrustHeading As String = "Is it a trusted journal?"

        Public Const FitHeading As String = "Is it a good fit for this manuscript?"

        Public Const Attribution As String =
            "Trust questions adapted from the Think. Check. Submit. checklist for journals, a cross-industry initiative (thinkchecksubmit.org), licensed CC BY 4.0."

        Public Const SourceUrl As String = "https://thinkchecksubmit.org/journals/"

        Public Shared ReadOnly Property TrustChecks As IReadOnlyList(Of JournalCheck) = {
            New JournalCheck("trust.known", "You or your colleagues know the journal and have read its articles"),
            New JournalCheck("trust.publisher", "You can easily identify and contact the publisher"),
            New JournalCheck("trust.review", "The journal is clear about the type of peer review it uses"),
            New JournalCheck("trust.indexed", "Its articles are indexed and archived in services you use"),
            New JournalCheck("trust.fees", "It is clear what fees will be charged, and for what"),
            New JournalCheck("trust.guidelines", "It gives clear guidelines for authors"),
            New JournalCheck("trust.member", "The publisher belongs to recognized industry initiatives, such as COPE, DOAJ, or OASPA")
        }

        Public Shared ReadOnly Property FitChecks As IReadOnlyList(Of JournalCheck) = {
            New JournalCheck("fit.scope", "Its aims and scope cover this work"),
            New JournalCheck("fit.type", "It publishes this type of article, within its length limits"),
            New JournalCheck("fit.audience", "It reaches the readers you want to reach"),
            New JournalCheck("fit.fees", "Its open-access options and fees work for you and any funder requirement"),
            New JournalCheck("fit.sharing", "Its preprint and sharing policy suits your plans"),
            New JournalCheck("fit.timeline", "Its time to decision and publication suits your timeline")
        }

        ' "5 of 7 trust checks · 4 of 6 fit checks", or empty before any is
        ' answered.
        Public Shared Function Summary(candidate As JournalCandidate) As String
            Dim yes As HashSet(Of String) = Answered(candidate)
            If yes.Count = 0 Then Return String.Empty
            Dim trust As Integer = TrustChecks.Where(Function(item) yes.Contains(item.Id)).Count()
            Dim fit As Integer = FitChecks.Where(Function(item) yes.Contains(item.Id)).Count()
            Return trust.ToString(CultureInfo.CurrentCulture) & " of " & TrustChecks.Count.ToString(CultureInfo.CurrentCulture) & " trust checks  ·  " &
                   fit.ToString(CultureInfo.CurrentCulture) & " of " & FitChecks.Count.ToString(CultureInfo.CurrentCulture) & " fit checks"
        End Function

        Public Shared Function Answered(candidate As JournalCandidate) As HashSet(Of String)
            Return New HashSet(Of String)(If(candidate?.Checks, New List(Of String)()), StringComparer.Ordinal)
        End Function

    End Class

End Namespace
