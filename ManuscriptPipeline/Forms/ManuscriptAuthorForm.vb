Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.Linq
Imports System.Windows.Forms
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

Namespace Forms

    Public Class ManuscriptAuthorForm
        Inherits Form

        Private ReadOnly _library As AuthorLibraryData
        Private ReadOnly _source As ManuscriptAuthor
        Private ReadOnly _excludedAuthorIds As HashSet(Of Guid)

        Private ReadOnly cboAuthor As New ComboBox()
        Private ReadOnly lstAffiliations As New CheckedListBox()
        Private ReadOnly chkCorresponding As New CheckBox()

        Private _result As ManuscriptAuthor


        Public ReadOnly Property Result As ManuscriptAuthor
            Get
                Return _result
            End Get
        End Property


        Public Sub New(
            library As AuthorLibraryData,
            source As ManuscriptAuthor,
            excludedAuthorIds As IEnumerable(Of Guid)
        )

            _library =
                If(
                    library,
                    New AuthorLibraryData()
                )

            _source =
                source

            _excludedAuthorIds =
                New HashSet(Of Guid)(
                    If(
                        excludedAuthorIds,
                        Enumerable.Empty(Of Guid)()
                    )
                )

            BuildInterface()
            UiPolish.ApplyDialog(Me)
            LoadData()

        End Sub


        Private Sub BuildInterface()

            Me.Text =
                If(
                    _source Is Nothing,
                    "Add Manuscript Author",
                    "Edit Manuscript Author"
                )

            Me.StartPosition =
                FormStartPosition.CenterParent

            Me.Size =
                New Size(
                    720,
                    560
                )

            Me.MinimumSize =
                New Size(
                    620,
                    500
                )

            Me.Font =
                New Font(
                    "Segoe UI",
                    10.0F
                )

            Me.AutoScaleMode =
                AutoScaleMode.Dpi

            Dim root As New TableLayoutPanel With {
                .Dock = DockStyle.Fill,
                .ColumnCount = 2,
                .RowCount = 5,
                .Padding = New Padding(20)
            }

            root.ColumnStyles.Add(
                New ColumnStyle(
                    SizeType.Absolute,
                    175
                )
            )

            root.ColumnStyles.Add(
                New ColumnStyle(
                    SizeType.Percent,
                    100
                )
            )

            root.RowStyles.Add(
                New RowStyle(
                    SizeType.AutoSize
                )
            )

            root.RowStyles.Add(
                New RowStyle(
                    SizeType.AutoSize
                )
            )

            root.RowStyles.Add(
                New RowStyle(
                    SizeType.Percent,
                    100
                )
            )

            root.RowStyles.Add(
                New RowStyle(
                    SizeType.AutoSize
                )
            )

            root.RowStyles.Add(
                New RowStyle(
                    SizeType.AutoSize
                )
            )

            cboAuthor.Dock =
                DockStyle.Fill

            cboAuthor.DropDownStyle =
                ComboBoxStyle.DropDownList

            lstAffiliations.Dock =
                DockStyle.Fill

            lstAffiliations.CheckOnClick =
                True

            chkCorresponding.Text =
                "Corresponding author"

            chkCorresponding.AutoSize =
                True

            chkCorresponding.Anchor =
                AnchorStyles.Left

            Dim hint As New Label With {
                .AutoSize = True,
                .MaximumSize = New Size(480, 0),
                .Text =
                    "Author order is set on the manuscript's Authors tab. " &
                    "Affiliations are manuscript-specific assignments."
            }

            root.Controls.Add(
                CreateLabel("Author"),
                0,
                0
            )

            root.Controls.Add(
                cboAuthor,
                1,
                0
            )

            root.Controls.Add(
                CreateLabel("Affiliations"),
                0,
                1
            )

            root.Controls.Add(
                hint,
                1,
                1
            )

            root.Controls.Add(
                lstAffiliations,
                1,
                2
            )

            root.Controls.Add(
                CreateLabel("Role"),
                0,
                3
            )

            root.Controls.Add(
                chkCorresponding,
                1,
                3
            )

            Dim buttons As New FlowLayoutPanel With {
                .Dock = DockStyle.Fill,
                .AutoSize = True,
                .FlowDirection = FlowDirection.RightToLeft,
                .WrapContents = False,
                .Padding = New Padding(0, 10, 0, 0)
            }

            Dim btnSave As New Button With {
                .Text = "Save",
                .AutoSize = True,
                .Height = 36
            }

            Dim btnCancel As New Button With {
                .Text = "Cancel",
                .AutoSize = True,
                .Height = 36,
                .DialogResult = DialogResult.Cancel
            }

            AddHandler btnSave.Click,
                AddressOf SaveLink

            buttons.Controls.Add(btnSave)
            buttons.Controls.Add(btnCancel)

            root.Controls.Add(
                buttons,
                0,
                4
            )

            root.SetColumnSpan(
                buttons,
                2
            )

            Me.AcceptButton =
                btnSave

            Me.CancelButton =
                btnCancel

            Me.Controls.Add(
                root
            )

        End Sub


        Private Function CreateLabel(
            text As String
        ) As Label

            Return New Label With {
                .Text = text,
                .AutoSize = True,
                .Anchor = AnchorStyles.Left,
                .Font = New Font(Me.Font, FontStyle.Bold),
                .Margin = New Padding(3, 9, 3, 9)
            }

        End Function


        Private Sub LoadData()

            For Each author As AuthorRecord In
                _library.Authors.
                    OrderBy(
                        Function(item)
                            Return item.DisplayName
                        End Function,
                        StringComparer.CurrentCultureIgnoreCase
                    )

                If _source Is Nothing AndAlso
                   _excludedAuthorIds.Contains(
                       author.Id
                   ) Then

                    Continue For

                End If

                If _source IsNot Nothing AndAlso
                   author.Id <> _source.AuthorId AndAlso
                   _excludedAuthorIds.Contains(
                       author.Id
                   ) Then

                    Continue For

                End If

                cboAuthor.Items.Add(
                    author
                )

            Next

            For Each affiliation As AffiliationRecord In
                _library.Affiliations.
                    OrderBy(
                        Function(item)
                            Return item.DisplayName
                        End Function,
                        StringComparer.CurrentCultureIgnoreCase
                    )

                lstAffiliations.Items.Add(
                    affiliation,
                    False
                )

            Next

            If _source Is Nothing Then

                If cboAuthor.Items.Count > 0 Then

                    cboAuthor.SelectedIndex =
                        0

                End If

                Return

            End If

            For i As Integer = 0 To cboAuthor.Items.Count - 1

                Dim author As AuthorRecord =
                    TryCast(
                        cboAuthor.Items(i),
                        AuthorRecord
                    )

                If author IsNot Nothing AndAlso
                   author.Id =
                   _source.AuthorId Then

                    cboAuthor.SelectedIndex =
                        i

                    Exit For

                End If

            Next

            If _source.AffiliationIds IsNot Nothing Then

                For i As Integer = 0 To lstAffiliations.Items.Count - 1

                    Dim affiliation As AffiliationRecord =
                        TryCast(
                            lstAffiliations.Items(i),
                            AffiliationRecord
                        )

                    If affiliation IsNot Nothing AndAlso
                       _source.AffiliationIds.Contains(
                           affiliation.Id
                       ) Then

                        lstAffiliations.SetItemChecked(
                            i,
                            True
                        )

                    End If

                Next

            End If

            chkCorresponding.Checked =
                _source.IsCorrespondingAuthor

        End Sub


        Private Sub SaveLink(
            sender As Object,
            e As EventArgs
        )

            Dim selectedAuthor As AuthorRecord =
                TryCast(
                    cboAuthor.SelectedItem,
                    AuthorRecord
                )

            If selectedAuthor Is Nothing Then

                MessageBox.Show(
                    Me,
                    "Add an author to the reusable author library first.",
                    "Author Required",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                )

                Return

            End If

            Dim link As New ManuscriptAuthor With {
                .AuthorId = selectedAuthor.Id,
                .IsCorrespondingAuthor = chkCorresponding.Checked
            }

            For Each checkedItem As Object In
                lstAffiliations.CheckedItems

                Dim affiliation As AffiliationRecord =
                    TryCast(
                        checkedItem,
                        AffiliationRecord
                    )

                If affiliation IsNot Nothing Then

                    link.AffiliationIds.Add(
                        affiliation.Id
                    )

                End If

            Next

            _result =
                link

            Me.DialogResult =
                DialogResult.OK

        End Sub

    End Class

End Namespace
