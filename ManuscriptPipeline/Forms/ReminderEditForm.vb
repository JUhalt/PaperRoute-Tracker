Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.Linq
Imports System.Windows.Forms
Imports ManuscriptPipeline.Controls
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

Namespace Forms

    Public Class ReminderEditForm
        Inherits Form

        Private ReadOnly _manuscripts As List(Of Manuscript)
        Private ReadOnly _existingManuscript As Manuscript
        Private ReadOnly _existingReminder As ManuscriptReminder

        Private ReadOnly cboManuscript As New ComboBox()
        Private ReadOnly dtpDueDate As New ThemedDatePicker()
        Private ReadOnly txtTitle As New TextBox()
        Private ReadOnly txtNotes As New TextBox()

        Private _selectedManuscriptId As Guid
        Private _result As ManuscriptReminder


        Public ReadOnly Property SelectedManuscriptId As Guid
            Get
                Return _selectedManuscriptId
            End Get
        End Property


        Public ReadOnly Property Result As ManuscriptReminder
            Get
                Return _result
            End Get
        End Property


        Public Sub New(
            manuscripts As IEnumerable(Of Manuscript),
            Optional existingManuscript As Manuscript = Nothing,
            Optional existingReminder As ManuscriptReminder = Nothing
        )

            _manuscripts =
                If(
                    manuscripts,
                    Enumerable.Empty(Of Manuscript)()
                ).
                Where(
                    Function(item)
                        Return item IsNot Nothing
                    End Function
                ).
                OrderBy(
                    Function(item)
                        Return item.Title
                    End Function,
                    StringComparer.CurrentCultureIgnoreCase
                ).
                ToList()

            _existingManuscript =
                existingManuscript

            _existingReminder =
                existingReminder

            BuildInterface()
            LoadValues()
            UiPolish.ApplyDialog(Me)

        End Sub


        Private Sub BuildInterface()

            Me.Text =
                If(
                    _existingReminder Is Nothing,
                    "Add Reminder",
                    "Edit Reminder"
                )

            Me.StartPosition =
                FormStartPosition.CenterParent

            Me.Size =
                New Size(
                    720,
                    520
                )

            Me.MinimumSize =
                New Size(
                    600,
                    460
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
                    150
                )
            )

            root.ColumnStyles.Add(
                New ColumnStyle(
                    SizeType.Percent,
                    100
                )
            )

            root.RowStyles.Add(New RowStyle(SizeType.AutoSize))
            root.RowStyles.Add(New RowStyle(SizeType.AutoSize))
            root.RowStyles.Add(New RowStyle(SizeType.AutoSize))
            root.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
            root.RowStyles.Add(New RowStyle(SizeType.AutoSize))

            cboManuscript.DropDownStyle =
                ComboBoxStyle.DropDownList

            cboManuscript.Dock =
                DockStyle.Fill

            For Each manuscript As Manuscript In _manuscripts

                cboManuscript.Items.Add(
                    New ManuscriptOption(
                        manuscript
                    )
                )

            Next

            dtpDueDate.Format =
                DateTimePickerFormat.Short

            dtpDueDate.Width =
                180

            dtpDueDate.Value =
                DateTime.Today.AddDays(7)

            ' A date picker takes no name from the label before it (#137).
            dtpDueDate.AccessibleName =
                "Due date"

            txtTitle.Dock =
                DockStyle.Fill

            txtNotes.Dock =
                DockStyle.Fill

            txtNotes.Multiline =
                True

            txtNotes.ScrollBars =
                ScrollBars.Vertical

            root.Controls.Add(
                CreateFieldLabel(
                    "Manuscript"
                ),
                0,
                0
            )

            root.Controls.Add(
                cboManuscript,
                1,
                0
            )

            root.Controls.Add(
                CreateFieldLabel(
                    "Due date"
                ),
                0,
                1
            )

            root.Controls.Add(
                dtpDueDate,
                1,
                1
            )

            root.Controls.Add(
                CreateFieldLabel(
                    "Reminder"
                ),
                0,
                2
            )

            root.Controls.Add(
                txtTitle,
                1,
                2
            )

            root.Controls.Add(
                CreateFieldLabel(
                    "Notes"
                ),
                0,
                3
            )

            root.Controls.Add(
                txtNotes,
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
                .Text =
                    If(
                        _existingReminder Is Nothing,
                        "Add Reminder",
                        "Save Changes"
                    ),
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
                AddressOf SaveReminder

            buttons.Controls.Add(
                btnSave
            )

            buttons.Controls.Add(
                btnCancel
            )

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


        Private Function CreateFieldLabel(
            text As String
        ) As Label

            Return New Label With {
                .Text = text,
                .AutoSize = True,
                .Anchor = AnchorStyles.Left,
                .Font =
                    New Font(
                        Me.Font,
                        FontStyle.Bold
                    ),
                .Margin =
                    New Padding(
                        3,
                        9,
                        3,
                        9
                    )
            }

        End Function


        Private Sub LoadValues()

            If _manuscripts.Count > 0 Then
                cboManuscript.SelectedIndex = 0
            End If

            If _existingManuscript IsNot Nothing Then

                For index As Integer = 0 To cboManuscript.Items.Count - 1

                    Dim optionItem As ManuscriptOption =
                        TryCast(
                            cboManuscript.Items(index),
                            ManuscriptOption
                        )

                    If optionItem IsNot Nothing AndAlso
                       optionItem.Manuscript.Id =
                           _existingManuscript.Id Then

                        cboManuscript.SelectedIndex =
                            index

                        Exit For

                    End If

                Next

            End If

            If _existingReminder IsNot Nothing Then

                dtpDueDate.Value =
                    SafePickerDate(
                        _existingReminder.DueDate
                    )

                txtTitle.Text =
                    _existingReminder.Title

                txtNotes.Text =
                    _existingReminder.Notes

                cboManuscript.Enabled =
                    False

            End If

        End Sub


        Private Sub SaveReminder(
            sender As Object,
            e As EventArgs
        )

            Dim selected As ManuscriptOption =
                TryCast(
                    cboManuscript.SelectedItem,
                    ManuscriptOption
                )

            If selected Is Nothing Then

                MessageBox.Show(
                    Me,
                    "Select a manuscript for this reminder.",
                    "Manuscript Required",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                )

                Return

            End If

            If String.IsNullOrWhiteSpace(
                txtTitle.Text
            ) Then

                MessageBox.Show(
                    Me,
                    "Enter a short reminder description.",
                    "Reminder Required",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                )

                txtTitle.Focus()

                Return

            End If

            _selectedManuscriptId =
                selected.Manuscript.Id

            _result =
                New ManuscriptReminder With {
                    .Id =
                        If(
                            _existingReminder Is Nothing,
                            Guid.NewGuid(),
                            _existingReminder.Id
                        ),
                    .DueDate = dtpDueDate.Value.Date,
                    .Title = txtTitle.Text.Trim(),
                    .Notes = txtNotes.Text.Trim(),
                    .IsCompleted =
                        If(
                            _existingReminder Is Nothing,
                            False,
                            _existingReminder.IsCompleted
                        ),
                    .CompletedDate =
                        If(
                            _existingReminder Is Nothing,
                            Nothing,
                            _existingReminder.CompletedDate
                        )
                }

            Me.DialogResult =
                DialogResult.OK

        End Sub


        Private Shared Function SafePickerDate(
            value As DateTime
        ) As DateTime

            If value < DateTimePicker.MinimumDateTime Then
                Return DateTime.Today
            End If

            If value > DateTimePicker.MaximumDateTime Then
                Return DateTime.Today
            End If

            Return value.Date

        End Function


        Private Class ManuscriptOption

            Public ReadOnly Property Manuscript As Manuscript


            Public Sub New(
                manuscript As Manuscript
            )

                Me.Manuscript =
                    manuscript

            End Sub


            Public Overrides Function ToString() As String

                If Manuscript Is Nothing OrElse
                   String.IsNullOrWhiteSpace(
                       Manuscript.Title
                   ) Then

                    Return "(Untitled manuscript)"

                End If

                Return Manuscript.Title.Trim()

            End Function

        End Class

    End Class

End Namespace
