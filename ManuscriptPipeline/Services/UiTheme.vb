Imports System.Drawing
Imports ManuscriptPipeline.Models

Namespace Services

    Public NotInheritable Class UiTheme

        Private Sub New()
        End Sub


        Public Shared Function IsDark() As Boolean
            Return SystemColors.Window.GetBrightness() < 0.5F
        End Function


        Public Shared Function BoardBackground() As Color

            If IsDark() Then
                Return Color.FromArgb(24, 27, 32)
            End If

            Return Color.FromArgb(244, 247, 248)

        End Function


        Public Shared Function HeaderBackground() As Color

            If IsDark() Then
                Return Color.FromArgb(31, 35, 41)
            End If

            Return Color.White

        End Function


        Public Shared Function CardBackground() As Color

            If IsDark() Then
                Return Color.FromArgb(40, 44, 51)
            End If

            Return Color.White

        End Function


        Public Shared Function CardBorder() As Color

            If IsDark() Then
                Return Color.FromArgb(61, 68, 79)
            End If

            Return Color.FromArgb(218, 228, 231)

        End Function


        Public Shared Function HoverBackground() As Color

            If IsDark() Then
                Return Color.FromArgb(50, 56, 65)
            End If

            Return Color.FromArgb(235, 244, 245)

        End Function


        Public Shared Function PrimaryText() As Color
            Return SystemColors.ControlText
        End Function


        Public Shared Function SecondaryText() As Color

            If IsDark() Then
                Return Color.FromArgb(172, 181, 192)
            End If

            Return Color.FromArgb(91, 105, 119)

        End Function


        Public Shared Function BrandNavy() As Color
            Return Color.FromArgb(15, 23, 42)
        End Function


        Public Shared Function AccentColor() As Color

            If IsDark() Then
                Return Color.FromArgb(45, 212, 191)
            End If

            ' Keep normal-size action/link text legible on both white cards and
            ' the light hover background (at least 4.5:1 contrast).
            Return Color.FromArgb(15, 118, 110)

        End Function


        Public Shared Function AccentSecondaryColor() As Color

            If IsDark() Then
                Return Color.FromArgb(34, 211, 238)
            End If

            Return Color.FromArgb(14, 116, 144)

        End Function


        Public Shared Function AccentMutedBackground() As Color

            If IsDark() Then
                Return Color.FromArgb(20, 69, 68)
            End If

            Return Color.FromArgb(204, 251, 241)

        End Function


        Public Shared Function DangerColor() As Color

            If IsDark() Then
                ' Preserve readable destructive-action text on dark hover states.
                Return Color.FromArgb(248, 124, 124)
            End If

            Return Color.FromArgb(190, 35, 45)

        End Function


        Public Shared Function WarningColor() As Color

            If IsDark() Then
                Return Color.FromArgb(251, 191, 36)
            End If

            Return Color.FromArgb(180, 83, 9)

        End Function


        Public Shared Function SuccessColor() As Color

            If IsDark() Then
                Return Color.FromArgb(110, 231, 183)
            End If

            Return Color.FromArgb(22, 130, 80)

        End Function


        Public Shared Function StageBackground(
            stage As PaperStage
        ) As Color

            If IsDark() Then

                Select Case stage

                    Case PaperStage.Idea
                        Return Color.FromArgb(72, 54, 120)

                    Case PaperStage.Draft
                        Return Color.FromArgb(65, 68, 75)

                    Case PaperStage.Submitted
                        Return Color.FromArgb(30, 64, 110)

                    Case PaperStage.UnderReview
                        Return Color.FromArgb(17, 94, 89)

                    Case PaperStage.Revision
                        Return Color.FromArgb(92, 63, 21)

                    Case PaperStage.Accepted
                        Return Color.FromArgb(31, 76, 47)

                    Case PaperStage.InPress
                        Return Color.FromArgb(19, 78, 74)

                    Case PaperStage.Published
                        Return Color.FromArgb(17, 78, 60)

                End Select

            Else

                Select Case stage

                    Case PaperStage.Idea
                        Return Color.FromArgb(237, 233, 254)

                    Case PaperStage.Draft
                        Return Color.FromArgb(229, 231, 235)

                    Case PaperStage.Submitted
                        Return Color.FromArgb(219, 234, 254)

                    Case PaperStage.UnderReview
                        Return Color.FromArgb(204, 251, 241)

                    Case PaperStage.Revision
                        Return Color.FromArgb(254, 243, 199)

                    Case PaperStage.Accepted
                        Return Color.FromArgb(220, 252, 231)

                    Case PaperStage.InPress
                        Return Color.FromArgb(207, 250, 254)

                    Case PaperStage.Published
                        Return Color.FromArgb(209, 250, 229)

                End Select

            End If

            Return SystemColors.Control

        End Function


        Public Shared Function StageForeground(
            stage As PaperStage
        ) As Color

            If IsDark() Then

                Select Case stage

                    Case PaperStage.Idea
                        Return Color.FromArgb(221, 214, 254)

                    Case PaperStage.Draft
                        Return Color.FromArgb(229, 231, 235)

                    Case PaperStage.Submitted
                        Return Color.FromArgb(191, 219, 254)

                    Case PaperStage.UnderReview
                        Return Color.FromArgb(153, 246, 228)

                    Case PaperStage.Revision
                        Return Color.FromArgb(253, 230, 138)

                    Case PaperStage.Accepted
                        Return Color.FromArgb(187, 247, 208)

                    Case PaperStage.InPress
                        Return Color.FromArgb(165, 243, 252)

                    Case PaperStage.Published
                        Return Color.FromArgb(167, 243, 208)

                End Select

            Else

                Select Case stage

                    Case PaperStage.Idea
                        Return Color.FromArgb(91, 33, 182)

                    Case PaperStage.Draft
                        Return Color.FromArgb(55, 65, 81)

                    Case PaperStage.Submitted
                        Return Color.FromArgb(29, 78, 216)

                    Case PaperStage.UnderReview
                        Return Color.FromArgb(15, 118, 110)

                    Case PaperStage.Revision
                        Return Color.FromArgb(180, 83, 9)

                    Case PaperStage.Accepted
                        Return Color.FromArgb(21, 128, 61)

                    Case PaperStage.InPress
                        Return Color.FromArgb(14, 116, 144)

                    Case PaperStage.Published
                        Return Color.FromArgb(4, 120, 87)

                End Select

            End If

            Return SystemColors.ControlText

        End Function

    End Class

End Namespace
