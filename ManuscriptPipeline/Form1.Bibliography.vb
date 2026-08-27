Imports System
Imports System.IO
Imports System.Windows.Forms
Imports ManuscriptPipeline.Forms
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

Partial Public Class Form1

    Private Sub ImportBibliography(
        sender As Object,
        e As EventArgs
    )
        Using picker As New OpenFileDialog With {
            .Title = "Import BibTeX or RIS",
            .Filter =
                "Bibliography files (*.bib;*.ris)|*.bib;*.ris|" &
                "BibTeX (*.bib)|*.bib|" &
                "RIS (*.ris)|*.ris|" &
                "All files (*.*)|*.*",
            .CheckFileExists = True,
            .Multiselect = False
        }

            If picker.ShowDialog(Me) <> DialogResult.OK Then
                Return
            End If

            Try
                Dim content As String =
                    File.ReadAllText(picker.FileName)

                Dim parsed As BibliographyParseResult =
                    BibliographyExchangeService.Parse(
                        picker.FileName,
                        content
                    )

                If parsed.Records.Count = 0 Then
                    MessageBox.Show(
                        Me,
                        String.Join(
                            Environment.NewLine,
                            parsed.FileWarnings
                        ),
                        "No Bibliography Records Found",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information
                    )
                    Return
                End If

                Using dialog As New BibliographyImportForm(
                    parsed,
                    manuscripts
                )
                    If dialog.ShowDialog(Me) <> DialogResult.OK Then
                        Return
                    End If

                    repository.CreatePreImportBackup()

                    Dim existingManuscriptIds As New System.Collections.Generic.HashSet(Of Guid)()

                    For Each existingManuscript As Manuscript In manuscripts

                        If existingManuscript IsNot Nothing Then

                            existingManuscriptIds.Add(
                                existingManuscript.Id
                            )

                        End If

                    Next

                    Dim result As BibliographyImportResult =
                        BibliographyExchangeService.Apply(
                            dialog.SelectedRecords,
                            manuscripts,
                            authorLibrary,
                            New BibliographyImportOptions With {
                                .ImportPublishedRecordsAsPublished =
                                    dialog.ImportPublishedRecordsAsPublished
                            }
                        )

                    Dim importedAtUtc As DateTime =
                        DateTime.UtcNow

                    For Each importedManuscript As Manuscript In manuscripts

                        If importedManuscript Is Nothing OrElse
                           existingManuscriptIds.Contains(
                               importedManuscript.Id
                           ) Then

                            Continue For

                        End If

                        ChronologyProvenanceService.StampImportedManuscript(
                            importedManuscript,
                            importedAtUtc
                        )

                    Next

                    ' Save reusable people first. If the manuscript save then
                    ' fails, the worst case is an unused reusable author record;
                    ' PaperRoute never persists a manuscript that references an
                    ' author record that failed to save.
                    authorRepository.Save(authorLibrary)
                    repository.Save(manuscripts)
                    RenderManuscripts()

                    MessageBox.Show(
                        Me,
                        "Bibliography import complete." &
                        Environment.NewLine &
                        Environment.NewLine &
                        "Imported: " &
                        result.ImportedCount.ToString() &
                        Environment.NewLine &
                        "Duplicates skipped: " &
                        result.DuplicateCount.ToString() &
                        Environment.NewLine &
                        "Reusable authors created: " &
                        result.AuthorsCreated.ToString() &
                        Environment.NewLine &
                        "Record warnings reviewed: " &
                        result.WarningCount.ToString(),
                        "Import Complete",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information
                    )
                End Using

            Catch ex As Exception
                MessageBox.Show(
                    Me,
                    "PaperRoute could not import the bibliography file." &
                    Environment.NewLine &
                    Environment.NewLine &
                    ex.Message,
                    "Bibliography Import Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                )
            End Try
        End Using
    End Sub

    Private Sub ExportBibTeX(
        sender As Object,
        e As EventArgs
    )
        ExportBibliography(BibliographyFormat.BibTeX)
    End Sub

    Private Sub ExportRis(
        sender As Object,
        e As EventArgs
    )
        ExportBibliography(BibliographyFormat.Ris)
    End Sub

    Private Sub ExportBibliography(
        format As BibliographyFormat
    )
        Dim formatName As String =
            If(
                format = BibliographyFormat.BibTeX,
                "BibTeX",
                "RIS"
            )

        Using selection As New BibliographyExportSelectionForm(
            manuscripts,
            formatName
        )
            If selection.ShowDialog(Me) <> DialogResult.OK Then
                Return
            End If

            Using picker As New SaveFileDialog With {
                .Title = "Export " & formatName,
                .Filter =
                    If(
                        format = BibliographyFormat.BibTeX,
                        "BibTeX (*.bib)|*.bib",
                        "RIS (*.ris)|*.ris"
                    ),
                .DefaultExt =
                    If(
                        format = BibliographyFormat.BibTeX,
                        "bib",
                        "ris"
                    ),
                .AddExtension = True,
                .OverwritePrompt = True
            }
                If picker.ShowDialog(Me) <> DialogResult.OK Then
                    Return
                End If

                Try
                    Dim content As String

                    If format = BibliographyFormat.BibTeX Then
                        content =
                            BibTeXService.Export(
                                selection.SelectedManuscripts,
                                authorLibrary
                            )
                    Else
                        content =
                            RisService.Export(
                                selection.SelectedManuscripts,
                                authorLibrary
                            )
                    End If

                    File.WriteAllText(
                        picker.FileName,
                        content
                    )

                    MessageBox.Show(
                        Me,
                        formatName &
                        " export complete." &
                        Environment.NewLine &
                        Environment.NewLine &
                        selection.SelectedManuscripts.Count.ToString() &
                        " manuscript(s) exported.",
                        "Export Complete",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information
                    )
                Catch ex As Exception
                    MessageBox.Show(
                        Me,
                        "PaperRoute could not export the bibliography." &
                        Environment.NewLine &
                        Environment.NewLine &
                        ex.Message,
                        "Bibliography Export Error",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error
                    )
                End Try
            End Using
        End Using
    End Sub

End Class
