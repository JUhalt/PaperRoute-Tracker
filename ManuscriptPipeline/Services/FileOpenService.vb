Imports System
Imports System.Collections.Generic
Imports System.Diagnostics
Imports System.IO
Imports System.Windows.Forms

Namespace Services

    ' Opens a recorded file (a version, a packet file, a letter, a saved
    ' report) with the program Windows has for it. A restored backup or an
    ' edited manuscripts.json can put any path behind an Open button, so a
    ' file Windows would run rather than show, such as an .exe or a .ps1,
    ' is opened only after the researcher says so (#117).
    Public NotInheritable Class FileOpenService

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


        ' Opens filePath with its usual program, asking first when Windows
        ' would run it. Errors are the caller's to show.
        Public Shared Sub OpenRecordedFile(
            owner As IWin32Window,
            filePath As String
        )

            OpenRecordedFile(
                filePath,
                Function(candidate) AskToOpen(owner, candidate),
                AddressOf StartWithShell
            )

        End Sub


        ' The same with the question and the launch supplied, so a test can
        ' prove which files ask without a dialog or a process.
        Friend Shared Sub OpenRecordedFile(
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


        Private Shared Function AskToOpen(
            owner As IWin32Window,
            filePath As String
        ) As Boolean

            Return MessageBox.Show(
                owner,
                "This file is a program or script, not a document:" &
                Environment.NewLine &
                Environment.NewLine &
                filePath &
                Environment.NewLine &
                Environment.NewLine &
                "Windows will run it if you open it. Open it anyway?",
                "Open a Program?",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2
            ) = DialogResult.Yes

        End Function


        Private Shared Sub StartWithShell(
            filePath As String
        )

            Process.Start(
                New ProcessStartInfo With {
                    .FileName = filePath,
                    .UseShellExecute = True
                }
            )

        End Sub

    End Class

End Namespace
