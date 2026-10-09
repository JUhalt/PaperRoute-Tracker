Imports System
Imports System.Collections.Generic
Imports System.Diagnostics
Imports System.Drawing
Imports System.Globalization
Imports System.Linq
Imports System.Text.Json
Imports System.Windows.Forms
Imports ManuscriptPipeline.Controls
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

Namespace Forms

    ' Confirms Mark Published (#61) by listing exactly what will change.
    Friend Class MarkPublishedForm
        Inherits Form

        Public Sub New(manuscript As Manuscript, match As PublicationMatch, today As DateTime)

            Text = "Mark as Published"
            FormBorderStyle = FormBorderStyle.FixedDialog
            MaximizeBox = False
            MinimizeBox = False
            ShowInTaskbar = False
            StartPosition = FormStartPosition.CenterParent
            AutoSize = True
            AutoSizeMode = AutoSizeMode.GrowAndShrink
            AutoScaleMode = AutoScaleMode.Dpi
            Font = New Font("Segoe UI", 9.0F)

            Dim root As New TableLayoutPanel With {
                .AutoSize = True,
                .AutoSizeMode = AutoSizeMode.GrowAndShrink,
                .ColumnCount = 1,
                .Padding = New Padding(18, 16, 18, 14)
            }

            root.Controls.Add(New Label With {
                .Text = "Mark """ & ReminderService.SafeManuscriptTitle(manuscript) & """ as published?",
                .AutoSize = True,
                .MaximumSize = New Size(460, 0),
                .UseMnemonic = False,
                .Font = New Font(Font, FontStyle.Bold),
                .Margin = New Padding(0, 0, 0, 10)
            })

            root.Controls.Add(New Label With {
                .Text = PublicationActions.Describe(match),
                .AutoSize = True,
                .MaximumSize = New Size(460, 0),
                .UseMnemonic = False,
                .Margin = New Padding(0, 0, 0, 12)
            })

            root.Controls.Add(New Label With {
                .Text = "PaperRoute will:",
                .AutoSize = True,
                .UseMnemonic = False,
                .Margin = New Padding(0, 0, 0, 4)
            })

            For Each line As String In PublicationMatchService.DescribeMarkPublished(manuscript, match, today)
                root.Controls.Add(New Label With {
                    .Text = "•  " & line,
                    .AutoSize = True,
                    .MaximumSize = New Size(460, 0),
                    .UseMnemonic = False,
                    .Margin = New Padding(8, 0, 0, 4)
                })
            Next

            root.Controls.Add(New Label With {
                .Text = "Recorded submissions and decisions are left as they are.",
                .AutoSize = True,
                .MaximumSize = New Size(460, 0),
                .UseMnemonic = False,
                .ForeColor = SystemColors.GrayText,
                .Margin = New Padding(0, 8, 0, 14)
            })

            Dim buttons As New FlowLayoutPanel With {
                .AutoSize = True,
                .AutoSizeMode = AutoSizeMode.GrowAndShrink,
                .FlowDirection = FlowDirection.RightToLeft,
                .WrapContents = False,
                .Dock = DockStyle.Fill,
                .Margin = New Padding(0)
            }
            Dim btnMark As New ActionButton With {
                .Text = "Mark Published",
                .Role = ActionButtonRole.Primary,
                .Height = 34,
                .Width = 140,
                .DialogResult = DialogResult.OK
            }
            Dim btnCancel As New ActionButton With {.Text = "Cancel", .Height = 34, .Width = 90, .DialogResult = DialogResult.Cancel, .Margin = New Padding(0, 0, 8, 0)}
            buttons.Controls.Add(btnMark)
            buttons.Controls.Add(btnCancel)
            root.Controls.Add(buttons)

            Controls.Add(root)
            AcceptButton = btnMark
            CancelButton = btnCancel
            UiPolish.ApplyDialog(Me)

        End Sub

    End Class


    ' Shared by the Publication Check window and the Deadlines page.
    Friend NotInheritable Class PublicationActions

        Private Sub New()
        End Sub


        ' "Title" · Journal · date · DOI
        Public Shared Function Describe(match As PublicationMatch) As String
            Dim parts As New List(Of String)()
            If Not String.IsNullOrWhiteSpace(match.Title) Then parts.Add("""" & match.Title.Trim() & """")
            If Not String.IsNullOrWhiteSpace(match.Journal) Then parts.Add(match.Journal.Trim())
            If match.PublishedDate.HasValue Then parts.Add(match.PublishedDate.Value.ToString("MMM d, yyyy", CultureInfo.CurrentCulture))
            If Not String.IsNullOrWhiteSpace(match.Doi) Then parts.Add("DOI " & match.Doi)
            Return String.Join("  ·  ", parts)
        End Function


        Public Shared Function SourceText(match As PublicationMatch) As String
            Select Case match.Source
                Case PublicationMatchSource.Doi : Return "The manuscript's DOI now resolves to this article."
                Case PublicationMatchSource.Preprint : Return "The manuscript's preprint links to this published version."
                Case PublicationMatchSource.Orcid : Return "The ORCID record lists a work with the same title."
                Case Else : Return "Crossref lists a work with the same title."
            End Select
        End Function


        ' The DOI's landing page, or the work's own web address.
        Public Shared Function MatchUri(match As PublicationMatch) As Uri
            Dim target As Uri = Nothing
            Dim address As String = If(String.IsNullOrWhiteSpace(match.Doi), match.Url, "https://doi.org/" & match.Doi)
            If Not Uri.TryCreate(address, UriKind.Absolute, target) Then Return Nothing
            If target.Scheme <> Uri.UriSchemeHttps AndAlso target.Scheme <> Uri.UriSchemeHttp Then Return Nothing
            Return target
        End Function


        Public Shared Sub OpenMatch(owner As IWin32Window, match As PublicationMatch)
            Dim target As Uri = MatchUri(match)
            If target Is Nothing Then
                MessageBox.Show(owner, "This match has no DOI or web address to open.", "Review Match", MessageBoxButtons.OK, MessageBoxIcon.Information)
                Return
            End If
            Try
                UrlSafetyService.OpenInBrowser(target.AbsoluteUri)
            Catch ex As Exception
                MessageBox.Show(owner, "PaperRoute could not open the web page." & Environment.NewLine & ex.Message,
                    "Review Match", MessageBoxButtons.OK, MessageBoxIcon.Information)
            End Try
        End Sub


        ' Makes a change and saves. If saving fails, the manuscript goes back
        ' to exactly what it was.
        Public Shared Function Commit(library As IList(Of Manuscript), manuscript As Manuscript, change As Action, save As Func(Of Boolean)) As Boolean

            Dim index As Integer = library.IndexOf(manuscript)
            Dim snapshot As String = JsonSerializer.Serialize(manuscript)

            change()

            If save() Then Return True

            If index >= 0 Then library(index) = JsonSerializer.Deserialize(Of Manuscript)(snapshot)
            Return False

        End Function

    End Class

End Namespace
