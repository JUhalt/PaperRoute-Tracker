Imports System
Imports System.Windows.Forms

Namespace Services

    ' Opens a recorded file (a version, a packet file, a letter, a saved
    ' report) with the program Windows has for it. A restored backup or an
    ' edited manuscripts.json can put any path behind an Open button, so a
    ' file Windows would run rather than show, such as an .exe or a .ps1,
    ' is opened only after the researcher says so (#117).
    '
    ' The rule for which files ask is the core's ProgramFileRule, and the
    ' launch is the registered launcher (the Windows shell, WindowsLauncher);
    ' the question stays here (core stage 2, step 10).
    Public NotInheritable Class FileOpenService

        Private Sub New()
        End Sub


        Public Shared Function IsProgramOrScript(
            filePath As String
        ) As Boolean

            Return ProgramFileRule.IsProgramOrScript(filePath)

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
                Sub(target) PlatformServices.Launcher.OpenFile(target)
            )

        End Sub


        ' The same with the question and the launch supplied, so a test can
        ' prove which files ask without a dialog or a process.
        Friend Shared Sub OpenRecordedFile(
            filePath As String,
            confirm As Func(Of String, Boolean),
            start As Action(Of String)
        )

            ProgramFileRule.Open(filePath, confirm, start)

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

    End Class

End Namespace
