Imports System

' How a link or a file is opened with the system's own program (core stage
' 2, design 3.4). The host registers its implementation in
' PlatformServices.Launcher: the Windows shell, from core stage 2's step 10.
Public Interface ILauncher

    ' Opens an http(s) address in the browser. The caller has checked it
    ' (UrlSafetyService.IsSafeHttpUrl); the text is passed exactly as given,
    ' never through Uri, which could add a slash or change escaping.
    Sub OpenLink(address As String)

    ' Opens a document with its usual program. The caller has asked first
    ' when the file is a program or script (ProgramFileRule).
    Sub OpenFile(filePath As String)

End Interface


' The default until a host registers one: nothing is opened, and the caller
' hears why.
Friend NotInheritable Class UnavailableLauncher
    Implements ILauncher

    Public Shared ReadOnly Instance As New UnavailableLauncher()


    Private Sub New()
    End Sub


    Public Sub OpenLink(address As String) Implements ILauncher.OpenLink
        Throw New InvalidOperationException("PaperRoute can't open links or files here.")
    End Sub


    Public Sub OpenFile(filePath As String) Implements ILauncher.OpenFile
        Throw New InvalidOperationException("PaperRoute can't open links or files here.")
    End Sub

End Class
