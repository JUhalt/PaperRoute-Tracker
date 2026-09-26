Imports System
Imports ManuscriptPipeline.Forms

Partial Public Class Form1

    Private Sub OpenPublicationExport(
        sender As Object,
        e As EventArgs
    )

        authorLibrary =
            authorRepository.Load()

        Using dialog As New PublicationExportForm(
            manuscripts,
            authorLibrary
        )

            dialog.ShowDialog(
                Me
            )

        End Using

    End Sub

End Class
