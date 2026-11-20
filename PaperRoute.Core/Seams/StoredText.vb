' The line break of the files PaperRoute stores (core stage 2, design 5.1).
' CR LF is what Windows writes today, so a Windows user's files keep their
' bytes, and a library written on Linux or macOS is the same file. Exports
' (BibTeX, RIS, Markdown, HTML) are the user's files and keep the system's
' line break.
Public NotInheritable Class StoredText

    Public Const NewLine As String = vbCrLf


    Private Sub New()
    End Sub

End Class
