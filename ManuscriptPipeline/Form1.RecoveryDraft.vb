Imports System
Imports System.IO
Imports System.Windows.Forms
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

' A recovery draft of the open manuscript page (#123). When PaperRoute
' closes without being able to ask about unsaved changes, the page's
' working copy is written to data\recovery; the next start offers to open
' the manuscript with it as unsaved changes, or to discard it. Nothing here
' changes manuscripts.json: that still takes Save.
Partial Public Class Form1

    ' Asks whether to open a recovered draft; Yes opens it, anything else
    ' discards it. Tests replace it.
    Friend recoveryDraftPrompt As Func(Of String, DialogResult) = Nothing

    ' The recovered copy the page being built opens with, if any.
    Private pendingRecoveredCopy As Manuscript = Nothing


    Private Function RecoveryDrafts() As RecoveryDraftStore

        Return New RecoveryDraftStore(
            Path.GetDirectoryName(repository.DataFilePath)
        )

    End Function


    ' Writes the open page's working copy when there is no one to ask:
    ' Windows is shutting down, or an error is ending the session. True
    ' when a draft was written. A failure here has no one to tell and must
    ' not hold up a shutdown, so nothing is shown.
    Friend Function WriteRecoveryDraft(
        reason As String
    ) As Boolean

        If currentPage <> WorkspacePage.Manuscript OrElse
           manuscriptEditor Is Nothing OrElse
           manuscriptEditor.IsDisposed OrElse
           Not manuscriptEditor.HasUnsavedChanges() Then
            Return False
        End If

        Try

            RecoveryDrafts().Write(
                New RecoveryDraft With {
                    .ManuscriptId = currentManuscriptId,
                    .WrittenAtUtc = DateTime.UtcNow,
                    .Reason = If(reason, String.Empty),
                    .Manuscript = manuscriptEditor.RecoveryCopy()
                }
            )

            Return True

        Catch ex As Exception

            Return False

        End Try

    End Function


    ' At startup, a draft for a manuscript still in the library is offered
    ' once. Yes opens the page with the draft as unsaved changes; the file
    ' goes either way, since the draft now lives in the page or was
    ' discarded. A draft whose manuscript is gone is left where it is.
    Private Sub OfferRecoveryDraft()

        Dim store As RecoveryDraftStore = RecoveryDrafts()
        Dim draft As RecoveryDraft = store.Read()

        If draft Is Nothing Then
            Return
        End If

        Dim saved As Manuscript = FindManuscript(draft.ManuscriptId)

        If saved Is Nothing Then
            Return
        End If

        Dim choice As DialogResult =
            If(recoveryDraftPrompt IsNot Nothing,
               recoveryDraftPrompt(saved.Title),
               MessageBox.Show(
                   Me,
                   "PaperRoute closed before your changes to '" & saved.Title & "' were saved." &
                   Environment.NewLine &
                   Environment.NewLine &
                   "Open the manuscript with those changes? Choose No to discard them.",
                   "Recovered Changes",
                   MessageBoxButtons.YesNo,
                   MessageBoxIcon.Warning))

        If choice = DialogResult.Yes Then

            ' The page's working copy stands in for this saved manuscript.
            draft.Manuscript.Id = saved.Id
            pendingRecoveredCopy = draft.Manuscript

            Try
                OpenManuscript(saved)
            Finally
                pendingRecoveredCopy = Nothing
            End Try

        End If

        Try
            store.Delete()
        Catch ex As Exception When TypeOf ex Is IOException OrElse TypeOf ex Is UnauthorizedAccessException
            ' Offered again at the next start; nothing is lost.
        End Try

    End Sub

End Class
