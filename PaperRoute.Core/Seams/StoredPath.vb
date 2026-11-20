Imports System
Imports System.IO

' A path read from a library or a backup, which may have been written on
' another system: "C:\Users\a\PaperRoute Library\...\letter.docx" from
' Windows, or "/home/a/PaperRoute Library/.../letter.docx" from Linux.
Public NotInheritable Class StoredPath

    Private Sub New()
    End Sub

    ' The stored path's file name. On Windows this is Path.GetFileName,
    ' the call restore made before (core stage 2, step 4), which already
    ' cuts at "\" and at "/". Elsewhere "\" is an ordinary character to
    ' Path.GetFileName, so the name is cut after the last "\" or "/".
    Public Shared Function FileName(stored As String) As String
        If OperatingSystem.IsWindows() Then Return Path.GetFileName(stored)
        Return LastSegment(stored)
    End Function


    ' The text after the last "\" or "/", or all of it when it has neither.
    Friend Shared Function LastSegment(stored As String) As String
        If stored Is Nothing Then Return Nothing
        Dim separator As Integer = Math.Max(stored.LastIndexOf("\"c), stored.LastIndexOf("/"c))
        Return If(separator >= 0, stored.Substring(separator + 1), stored)
    End Function

End Class
