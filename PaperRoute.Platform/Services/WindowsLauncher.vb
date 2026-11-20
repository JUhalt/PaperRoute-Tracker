Imports System
Imports System.Diagnostics
Imports System.Runtime.Versioning

Namespace Services

    ' Opens links and files with the Windows shell (core stage 2, step 10).
    ' The one place the core's links and the app's recorded files reach the
    ' shell: the address or path itself as the file name, UseShellExecute,
    ' no verb and no arguments, the ProcessStartInfo that
    ' UrlSafetyService.OpenInBrowser and FileOpenService.StartWithShell built
    ' in PaperRoute 1.0.x. The callers check first: OpenInBrowser refuses
    ' anything but http and https, and FileOpenService asks before a program
    ' or script (ProgramFileRule).
    <SupportedOSPlatform("windows")>
    Public NotInheritable Class WindowsLauncher
        Implements ILauncher

        Public Sub OpenLink(address As String) Implements ILauncher.OpenLink
            StartWithShell(address)
        End Sub


        Public Sub OpenFile(filePath As String) Implements ILauncher.OpenFile
            StartWithShell(filePath)
        End Sub


        Private Shared Sub StartWithShell(target As String)
            Process.Start(ShellStartInfo(target))
        End Sub


        ' What the shell is given, apart from the launch, so a test can pin it
        ' without starting a process (WindowsLauncherTests).
        Friend Shared Function ShellStartInfo(target As String) As ProcessStartInfo
            Return New ProcessStartInfo With {
                .FileName = target,
                .UseShellExecute = True
            }
        End Function

    End Class

End Namespace
