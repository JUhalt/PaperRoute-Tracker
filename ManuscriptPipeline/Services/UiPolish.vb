Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.Text.RegularExpressions
Imports System.Windows.Forms
Imports ManuscriptPipeline.Controls

Namespace Services

    Public NotInheritable Class UiPolish

        Private Shared _installed As Boolean = False

        Private Shared ReadOnly _styledHandles As New HashSet(Of IntPtr)()


        Private Sub New()
        End Sub


        ' =====================================================
        ' Global installation
        ' =====================================================

        Public Shared Sub InstallGlobalDialogStyling()

            If _installed Then
                Return
            End If

            _installed = True

            AddHandler Application.Idle,
                AddressOf StyleOpenDialogs

        End Sub


        Private Shared Sub StyleOpenDialogs(
            sender As Object,
            e As EventArgs
        )

            For Each form As Form In Application.OpenForms

                ' Form1 has its own custom board styling.
                If String.Equals(
                    form.GetType().Name,
                    "Form1",
                    StringComparison.Ordinal
                ) Then

                    Continue For

                End If

                If _styledHandles.Contains(
                    form.Handle
                ) Then

                    Continue For

                End If

                ApplyDialog(
                    form
                )

                _styledHandles.Add(
                    form.Handle
                )

                AddHandler form.FormClosed,
                    AddressOf StyledFormClosed

            Next

        End Sub


        Private Shared Sub StyledFormClosed(
            sender As Object,
            e As FormClosedEventArgs
        )

            Dim form As Form =
                TryCast(
                    sender,
                    Form
                )

            If form Is Nothing Then
                Return
            End If

            _styledHandles.Remove(
                form.Handle
            )

        End Sub


        ' =====================================================
        ' Public styling entry point
        ' =====================================================

        Public Shared Sub ApplyDialog(
            form As Form
        )

            If form Is Nothing Then
                Return
            End If

            form.BackColor =
                UiTheme.BoardBackground()

            form.ForeColor =
                UiTheme.PrimaryText()

            ApplyToControlTree(
                form,
                TryCast(form.AcceptButton, Button)
            )

        End Sub


        ' =====================================================
        ' Recursive control styling
        ' =====================================================

        Private Shared Sub ApplyToControlTree(
            parent As Control,
            primaryButton As Button
        )

            For Each control As Control In parent.Controls

                ApplyControlStyle(
                    control,
                    primaryButton
                )

                If control.HasChildren Then

                    ApplyToControlTree(
                        control,
                        primaryButton
                    )

                End If

            Next

        End Sub


        Private Shared Sub ApplyControlStyle(
            control As Control,
            primaryButton As Button
        )

            ' Theme-painted buttons already follow the tokens.
            If TypeOf control Is ActionButton Then
                Return
            End If

            If TypeOf control Is Button Then

                Dim button As Button =
                    DirectCast(
                        control,
                        Button
                    )

                Dim isPrimary As Boolean =
                    control Is primaryButton

                StyleButton(
                    button,
                    isPrimary
                )

                ' A disabled filled button would show grey text on the accent.
                If isPrimary Then
                    AddHandler button.EnabledChanged,
                        Sub(sender, e)
                            StyleButton(button, button.Enabled)
                        End Sub
                    If Not button.Enabled Then
                        StyleButton(button, False)
                    End If
                End If

                Return

            End If


            If TypeOf control Is TextBox Then

                Dim textBox As TextBox =
                    DirectCast(
                        control,
                        TextBox
                    )

                textBox.BackColor =
                    UiTheme.CardBackground()

                textBox.ForeColor =
                    UiTheme.PrimaryText()

                textBox.BorderStyle =
                    BorderStyle.FixedSingle

                ThemedBorder.Attach(
                    textBox
                )

                Return

            End If


            If TypeOf control Is RichTextBox Then

                Dim richText As RichTextBox =
                    DirectCast(
                        control,
                        RichTextBox
                    )

                richText.BackColor =
                    UiTheme.CardBackground()

                richText.ForeColor =
                    UiTheme.PrimaryText()

                richText.BorderStyle =
                    BorderStyle.FixedSingle

                ThemedBorder.Attach(
                    richText
                )

                Return

            End If


            If TypeOf control Is ListBox Then

                Dim listBox As ListBox =
                    DirectCast(
                        control,
                        ListBox
                    )

                listBox.BackColor =
                    UiTheme.CardBackground()

                listBox.ForeColor =
                    UiTheme.PrimaryText()

                listBox.BorderStyle =
                    BorderStyle.FixedSingle

                ThemedBorder.Attach(
                    listBox
                )

                Return

            End If


            If TypeOf control Is ComboBox Then

                Dim comboBox As ComboBox =
                    DirectCast(
                        control,
                        ComboBox
                    )

                comboBox.BackColor =
                    UiTheme.CardBackground()

                comboBox.ForeColor =
                    UiTheme.PrimaryText()

                comboBox.FlatStyle =
                    FlatStyle.Flat

                Return

            End If


            If TypeOf control Is NumericUpDown Then

                Dim numeric As NumericUpDown =
                    DirectCast(
                        control,
                        NumericUpDown
                    )

                numeric.BackColor =
                    UiTheme.CardBackground()

                numeric.ForeColor =
                    UiTheme.PrimaryText()

                Return

            End If


            If TypeOf control Is DateTimePicker Then

                Dim picker As DateTimePicker =
                    DirectCast(
                        control,
                        DateTimePicker
                    )

                picker.CalendarMonthBackground =
                    UiTheme.CardBackground()

                picker.CalendarForeColor =
                    UiTheme.PrimaryText()

                Return

            End If


            If TypeOf control Is DataGridView Then

                Dim grid As DataGridView =
                    DirectCast(
                        control,
                        DataGridView
                    )

                grid.EnableHeadersVisualStyles = False
                grid.BackgroundColor = UiTheme.CardBackground()
                grid.GridColor = UiTheme.CardBorder()
                grid.DefaultCellStyle.BackColor = UiTheme.CardBackground()
                grid.DefaultCellStyle.ForeColor = UiTheme.PrimaryText()
                grid.DefaultCellStyle.SelectionBackColor = UiTheme.HoverBackground()
                grid.DefaultCellStyle.SelectionForeColor = UiTheme.PrimaryText()
                grid.ColumnHeadersDefaultCellStyle.BackColor = UiTheme.HeaderBackground()
                grid.ColumnHeadersDefaultCellStyle.ForeColor = UiTheme.PrimaryText()
                ' A header never looks selected because a cell below it is.
                grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = UiTheme.HeaderBackground()
                grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = UiTheme.PrimaryText()
                ' A header never looks selected because a cell below it is.
                grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = UiTheme.HeaderBackground()
                grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = UiTheme.PrimaryText()

                Return

            End If


            If TypeOf control Is CheckBox OrElse
               TypeOf control Is RadioButton Then

                control.ForeColor = UiTheme.PrimaryText()
                control.BackColor = SectionCard.SurfaceBehind(control)
                ShowLiteralAmpersands(DirectCast(control, ButtonBase))

                Return

            End If


            If TypeOf control Is LinkLabel Then

                Dim link As LinkLabel =
                    DirectCast(
                        control,
                        LinkLabel
                    )

                link.LinkColor = UiTheme.AccentColor()
                link.ActiveLinkColor = UiTheme.AccentSecondaryColor()
                link.VisitedLinkColor = UiTheme.AccentColor()

                Return

            End If


            ' Sections are cards; what they contain sits on the card.
            If TypeOf control Is SectionCard Then

                control.BackColor =
                    UiTheme.CardBackground()

                control.ForeColor =
                    UiTheme.PrimaryText()

                Return

            End If


            If TypeOf control Is GroupBox Then

                control.BackColor =
                    SectionCard.SurfaceBehind(control)

                control.ForeColor =
                    UiTheme.PrimaryText()

                Return

            End If


            ' A tab page continues the surface around its tabs.
            If TypeOf control Is TabPage Then

                Dim page As TabPage =
                    DirectCast(
                        control,
                        TabPage
                    )

                page.UseVisualStyleBackColor =
                    False

                page.BackColor =
                    If(page.Parent Is Nothing,
                       UiTheme.BoardBackground(),
                       SectionCard.SurfaceBehind(page.Parent))

                page.ForeColor =
                    UiTheme.PrimaryText()

                Return

            End If


            If TypeOf control Is TableLayoutPanel OrElse
               TypeOf control Is FlowLayoutPanel Then

                control.BackColor =
                    SectionCard.SurfaceBehind(control)

                Return

            End If


            If TypeOf control Is Label Then

                Dim label As Label =
                    DirectCast(
                        control,
                        Label
                    )

                If label.UseMnemonic AndAlso Not UsesAcceleratorSyntax(label.Text) Then
                    label.UseMnemonic = False
                End If

                If label.ForeColor =
                    SystemColors.GrayText Then

                    label.ForeColor =
                        UiTheme.SecondaryText()

                ElseIf label.ForeColor =
                       SystemColors.ControlText OrElse
                       label.ForeColor =
                       Color.Black OrElse
                       label.ForeColor =
                       Color.White Then

                    label.ForeColor =
                        UiTheme.PrimaryText()

                End If

            End If

        End Sub


        ' =====================================================
        ' Literal ampersands (#71)
        ' =====================================================

        ' A label or option shows "&" literally unless its text uses
        ' accelerator syntax ("&s" or "&&"). Names filled in later, such as
        ' "Memory & Cognition", keep their ampersand, while deliberate
        ' accelerators such as "Show &status" keep working.
        Private Shared ReadOnly AcceleratorSyntax As New Regex("&(&|[\p{L}\p{N}])")

        Friend Shared Function UsesAcceleratorSyntax(text As String) As Boolean
            Return Not String.IsNullOrEmpty(text) AndAlso AcceleratorSyntax.IsMatch(text)
        End Function

        Private Shared Sub ShowLiteralAmpersands(button As ButtonBase)
            If button.UseMnemonic AndAlso Not UsesAcceleratorSyntax(button.Text) Then
                button.UseMnemonic = False
            End If
        End Sub


        ' =====================================================
        ' Buttons
        ' =====================================================

        ' One filled primary action per dialog (its default button), red text
        ' for destructive actions, and quiet neutral outlines for the rest.
        Private Shared Sub StyleButton(
            button As Button,
            isPrimary As Boolean
        )

            button.FlatStyle =
                FlatStyle.Flat

            button.UseVisualStyleBackColor =
                False

            button.Cursor =
                Cursors.Hand

            button.FlatAppearance.BorderSize =
                1

            Dim text As String =
                button.Text.Trim().ToUpperInvariant()

            If isPrimary Then

                Dim accent As Color =
                    UiTheme.AccentColor()

                Dim shade As Color =
                    If(UiTheme.IsDark(), Color.White, Color.Black)

                button.BackColor = accent
                button.ForeColor = UiTheme.OnAccentText()
                button.FlatAppearance.BorderColor = accent
                button.FlatAppearance.MouseOverBackColor = UiTheme.Blend(accent, shade, 0.1F)
                button.FlatAppearance.MouseDownBackColor = UiTheme.Blend(accent, shade, 0.2F)

                Return

            End If

            button.BackColor =
                UiTheme.CardBackground()

            button.ForeColor =
                If(
                    text.Contains("DELETE") OrElse text.Contains("REMOVE"),
                    UiTheme.DangerColor(),
                    UiTheme.PrimaryText()
                )

            button.FlatAppearance.BorderColor =
                UiTheme.CardBorder()

            button.FlatAppearance.MouseOverBackColor =
                UiTheme.HoverBackground()

            button.FlatAppearance.MouseDownBackColor =
                UiTheme.HoverBackground()

        End Sub

    End Class

End Namespace