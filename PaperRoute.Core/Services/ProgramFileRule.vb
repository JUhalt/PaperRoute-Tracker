Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.Linq

Namespace Services

    ' Which recorded files are programs or scripts, so the researcher is
    ' asked before one is opened (#117). A restored backup or an edited
    ' manuscripts.json can put any path behind an Open button. Moved from the
    ' app's FileOpenService (core stage 2, step 10) unchanged: the same types,
    ' character for character, and the same trimming. The app's
    ' FileOpenService forwards here and keeps its question.
    Public NotInheritable Class ProgramFileRule

        ' Types Windows runs as a program or script.
        Private Shared ReadOnly ProgramExtensions As New HashSet(Of String)(
            {
                ".exe", ".com", ".scr", ".pif", ".msi", ".msp",
                ".bat", ".cmd", ".ps1", ".psm1", ".vbs", ".vbe",
                ".js", ".jse", ".wsf", ".wsh", ".hta", ".reg",
                ".lnk", ".url", ".cpl", ".jar", ".appx", ".msix"
            },
            StringComparer.OrdinalIgnoreCase
        )


        Private Sub New()
        End Sub


        Public Shared Function IsProgramOrScript(
            filePath As String
        ) As Boolean

            If String.IsNullOrWhiteSpace(filePath) Then
                Return False
            End If

            ' Windows ignores trailing dots and spaces in a name.
            Return ProgramExtensions.Contains(
                Path.GetExtension(
                    filePath.TrimEnd(" "c, "."c)
                )
            )

        End Function


        ' Opens filePath with start, first asking confirm when the file is a
        ' program or script; a No leaves it closed. Errors are the caller's
        ' to show.
        Public Shared Sub Open(
            filePath As String,
            confirm As Func(Of String, Boolean),
            start As Action(Of String)
        )

            If IsProgramOrScript(filePath) AndAlso
               Not confirm(filePath) Then

                Return

            End If

            start(filePath)

        End Sub


        ' A copy of the types, for the test that pins them (ProgramFileRuleTests).
        Friend Shared Function Extensions() As String()
            Return ProgramExtensions.ToArray()
        End Function

    End Class

End Namespace
