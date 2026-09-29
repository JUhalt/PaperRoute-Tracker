Imports System.Drawing
Imports ManuscriptPipeline.Models

Namespace Services

    Public NotInheritable Class UiTheme

        Private Sub New()
        End Sub


        ' Spacing and shape tokens, in logical pixels at 96 DPI. Scale them
        ' with Px for the control's actual DPI.
        Public Const SpaceXs As Integer = 4
        Public Const SpaceSm As Integer = 8
        Public Const SpaceMd As Integer = 12
        Public Const SpaceLg As Integer = 16
        Public Const SpaceXl As Integer = 24
        Public Const CardRadius As Integer = 10
        Public Const ControlRadius As Integer = 8


        Public Shared Function Px(
            logicalPixels As Integer,
            deviceDpi As Integer
        ) As Integer

            Return CInt(Math.Round(logicalPixels * Math.Max(96, deviceDpi) / 96.0))

        End Function


        Public Shared Function IsDark() As Boolean
            Return SystemColors.Window.GetBrightness() < 0.5F
        End Function


        ' Dividers inside a card, one step quieter than CardBorder.
        Public Shared Function SubtleBorder() As Color

            If IsDark() Then
                Return Color.FromArgb(50, 56, 65)
            End If

            Return Color.FromArgb(231, 238, 240)

        End Function


        ' Tertiary text: counts, hints, and metadata beside secondary text.
        ' Still 4.5:1 on both the board and card backgrounds.
        Public Shared Function MutedText() As Color

            If IsDark() Then
                Return Color.FromArgb(138, 149, 161)
            End If

            Return Color.FromArgb(100, 113, 125)

        End Function


        ' Text and icons drawn on a filled accent (primary) surface.
        Public Shared Function OnAccentText() As Color

            If IsDark() Then
                Return Color.FromArgb(7, 32, 29)
            End If

            Return Color.White

        End Function


        Public Shared Function WarningMutedBackground() As Color

            If IsDark() Then
                Return Color.FromArgb(92, 63, 21)
            End If

            Return Color.FromArgb(254, 243, 199)

        End Function


        Public Shared Function DangerMutedBackground() As Color

            If IsDark() Then
                Return Color.FromArgb(91, 33, 38)
            End If

            Return Color.FromArgb(254, 226, 226)

        End Function


        ' Mixes a color toward another by the given fraction (0 to 1), for
        ' hover and pressed states of filled surfaces.
        Public Shared Function Blend(
            baseColor As Color,
            toward As Color,
            amount As Single
        ) As Color

            Dim t As Single = Math.Max(0.0F, Math.Min(1.0F, amount))

            ' Color channels are Bytes; widen before subtracting.
            Return Color.FromArgb(
                baseColor.A,
                CInt(CInt(baseColor.R) + (CInt(toward.R) - CInt(baseColor.R)) * t),
                CInt(CInt(baseColor.G) + (CInt(toward.G) - CInt(baseColor.G)) * t),
                CInt(CInt(baseColor.B) + (CInt(toward.B) - CInt(baseColor.B)) * t))

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

            ' At least 4.5:1 on white, the hover background, and its own
            ' muted background.
            Return Color.FromArgb(21, 121, 74)

        End Function


        ' A quiet green behind SuccessColor, such as the rail's Online status.
        Public Shared Function SuccessMutedBackground() As Color

            If IsDark() Then
                Return Color.FromArgb(21, 59, 43)
            End If

            Return Color.FromArgb(220, 247, 232)

        End Function


        ' A calm state rather than a warning, such as Working offline. The
        ' blues match the Submitted stage badge.
        Public Shared Function InfoColor() As Color

            If IsDark() Then
                Return Color.FromArgb(147, 197, 253)
            End If

            Return Color.FromArgb(29, 78, 216)

        End Function


        Public Shared Function InfoMutedBackground() As Color

            If IsDark() Then
                Return Color.FromArgb(30, 58, 100)
            End If

            Return Color.FromArgb(219, 234, 254)

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
