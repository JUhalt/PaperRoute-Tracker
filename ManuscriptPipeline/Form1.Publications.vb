Imports System
Imports System.Collections.Generic
Imports System.Globalization
Imports System.Linq
Imports System.Windows.Forms
Imports ManuscriptPipeline.Forms
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

' The publication check and Fill Blanks (#61): only when the user asks,
' from Import & Export or a possible publication on the Deadlines page.
Partial Public Class Form1

    ' Tests answer with synthetic records instead of Crossref and ORCID.
    Friend publicationSource As IPublicationSource = Nothing
    Friend markPublishedPrompt As Func(Of Manuscript, PublicationMatch, Boolean) = Nothing

    Private Function CurrentPublicationSource(Optional serviceId As String = OnlineServiceCatalog.PublicationCheck) As IPublicationSource
        If publicationSource IsNot Nothing Then Return publicationSource
        Return New OnlinePublicationSource(serviceId)
    End Function


    ' The ORCID iD of the author marked "This is me", if any.
    Private Function OwnOrcid() As String
        Dim self As AuthorRecord = If(authorLibrary?.Authors, New List(Of AuthorRecord)()).
            FirstOrDefault(Function(author) author IsNot Nothing AndAlso author.IsMe AndAlso Not String.IsNullOrWhiteSpace(author.Orcid))
        Return If(self?.Orcid, String.Empty)
    End Function


    Private Sub CheckForPublications(sender As Object, e As EventArgs)
        CheckForPublications(Nothing)
    End Sub


    Friend Sub CheckForPublications(manuscriptIds As IEnumerable(Of Guid))

        Using dialog As New PublicationCheckForm(manuscripts, manuscriptIds, OwnOrcid(), CurrentPublicationSource(), AddressOf SaveManuscripts, DateTime.Today)
            dialog.ConfirmMarkPublished = markPublishedPrompt
            dialog.ShowDialog(Me)
            If dialog.Changed Then
                RenderManuscripts()
            End If
        End Using

    End Sub


    Private Sub FillBlanksFromCrossref(sender As Object, e As EventArgs)

        Using dialog As New FillBlanksForm(manuscripts, CurrentPublicationSource(OnlineServiceCatalog.Crossref), AddressOf SaveManuscripts)
            If dialog.ShowDialog(Me) = DialogResult.OK AndAlso dialog.FilledCount > 0 Then
                RenderManuscripts()
                lblStatus.Text = "Filled " & dialog.FilledCount.ToString(CultureInfo.CurrentCulture) &
                                 If(dialog.FilledCount = 1, " empty field", " empty fields") & " from Crossref."
            End If
        End Using

    End Sub


    Private Function FindPublicationMatch(item As DeadlineItem, ByRef manuscript As Manuscript) As PublicationMatch
        manuscript = FindManuscript(item.ManuscriptId)
        If manuscript Is Nothing OrElse Not item.PublicationMatchId.HasValue OrElse manuscript.PublicationMatches Is Nothing Then Return Nothing
        Dim id As Guid = item.PublicationMatchId.Value
        Return manuscript.PublicationMatches.FirstOrDefault(Function(match) match IsNot Nothing AndAlso match.Id = id)
    End Function


    Private Sub ReviewDeadlineMatch(item As DeadlineItem)
        Dim manuscript As Manuscript = Nothing
        Dim match As PublicationMatch = FindPublicationMatch(item, manuscript)
        If match IsNot Nothing Then PublicationActions.OpenMatch(Me, match)
    End Sub


    Private Sub MarkDeadlinePublished(item As DeadlineItem)

        Dim manuscript As Manuscript = Nothing
        Dim match As PublicationMatch = FindPublicationMatch(item, manuscript)
        If match Is Nothing Then Return

        Dim confirmed As Boolean
        If markPublishedPrompt IsNot Nothing Then
            confirmed = markPublishedPrompt(manuscript, match)
        Else
            Using dialog As New MarkPublishedForm(manuscript, match, DateTime.Today)
                confirmed = dialog.ShowDialog(Me) = DialogResult.OK
            End Using
        End If
        If Not confirmed Then Return

        Dim target As Manuscript = manuscript
        If PublicationActions.Commit(manuscripts, target, Sub() PublicationMatchService.MarkPublished(target, match, DateTime.Today), AddressOf SaveManuscripts) Then
            RenderManuscripts()
            lblStatus.Text = "Marked """ & ReminderService.SafeManuscriptTitle(target) & """ published."
        Else
            RenderManuscripts()
        End If

    End Sub


    Private Sub IgnoreDeadlineMatch(item As DeadlineItem)

        Dim manuscript As Manuscript = Nothing
        Dim match As PublicationMatch = FindPublicationMatch(item, manuscript)
        If match Is Nothing Then Return

        If PublicationActions.Commit(manuscripts, manuscript, Sub() PublicationMatchService.Ignore(match), AddressOf SaveManuscripts) Then
            lblStatus.Text = "Ignored the possible publication. Later checks will not show it again."
        End If
        RenderManuscripts()

    End Sub

End Class
