Imports System
Imports System.Windows.Forms
Imports ManuscriptPipeline.Models

Namespace Forms

    Partial Public Class SubmissionPacketVaultForm

        ' Test seam: shows the export window instead of ShowDialog.
        Friend ExportPrompt As Action(Of PacketExportForm) = Nothing

        Friend ReadOnly Property ExportPacketButton As Button
            Get
                Return btnExportPacket
            End Get
        End Property


        Private Sub ExportPacketClicked(sender As Object, e As EventArgs)
            ExportSelectedPacket()
        End Sub


        ' Export Packet (#45) for the selected packet, as the vault shows it,
        ' including changes not saved yet. The export only reads: nothing here
        ' changes, so nothing is refreshed afterwards.
        Friend Function ExportSelectedPacket() As Boolean

            If _integrityBusy OrElse IsDisposed OrElse Disposing Then Return False
            Dim packet As SubmissionPacket = GetSelectedPacket()
            If packet Is Nothing Then Return False

            Using dialog As New PacketExportForm(_workingManuscript, packet, _authorLibrary)
                If ExportPrompt IsNot Nothing Then
                    ExportPrompt(dialog)
                Else
                    dialog.ShowDialog(Me)
                End If
            End Using
            Return True

        End Function

    End Class

End Namespace
