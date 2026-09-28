Namespace Models

    ' A color the user chose for a tag. Tags without one get a color from
    ' their name, so the same tag always looks the same.
    Public Class TagColor

        Public Property Tag As String = String.Empty

        Public Property Color As TagPalette = TagPalette.Teal

    End Class


    Public Enum TagPalette
        Teal
        Blue
        Violet
        Rose
        Amber
        Green
        Slate
        Orange
    End Enum

End Namespace
