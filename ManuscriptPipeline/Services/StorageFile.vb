Imports System
Imports System.IO
Imports System.Threading

Namespace Services

    ' How PaperRoute opens its own storage files: manuscripts.json,
    ' authors.json and their safety backups. A file another program holds
    ' for a moment, such as a sync client or a backup tool, is tried again
    ' briefly and then reported as in use. That is not damage, so a loader
    ' stops there instead of turning to the .bak (#111).
    Friend NotInheritable Class StorageFile

        ' Five tries over about half a second: long enough for a sync
        ' client's pass, short enough not to feel like a hang at startup.
        Private Const OpenAttempts As Integer = 5
        Private Const RetryDelayMilliseconds As Integer = 100

        ' Win32 ERROR_SHARING_VIOLATION and ERROR_LOCK_VIOLATION: the low
        ' word of the HResult an IOException carries for them.
        Private Const SharingViolation As Integer = 32
        Private Const LockViolation As Integer = 33


        Private Sub New()
        End Sub


        Friend Shared Function OpenRead(
            filePath As String
        ) As Stream

            Return New FileStream(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize:=65536,
                options:=FileOptions.SequentialScan
            )

        End Function


        ' Opens filePath through openFile, trying again after a sharing
        ' violation or access denied. After the last try the file is
        ' reported in use; any other error is passed on as it is.
        Friend Shared Function OpenReadWithRetry(
            filePath As String,
            openFile As Func(Of String, Stream)
        ) As Stream

            Dim attempt As Integer =
                0

            Do

                attempt += 1

                Try

                    Return openFile(filePath)

                Catch ex As Exception When IsInUse(ex) AndAlso attempt < OpenAttempts

                    Thread.Sleep(
                        RetryDelayMilliseconds
                    )

                Catch ex As Exception When IsInUse(ex)

                    Throw New StorageFileInUseException(
                        filePath,
                        ex
                    )

                End Try

            Loop

        End Function


        ' True for the errors Windows raises when another program holds the
        ' file: a sharing or lock violation, or access denied. A missing
        ' file or folder is not one.
        Friend Shared Function IsInUse(
            ex As Exception
        ) As Boolean

            If TypeOf ex Is UnauthorizedAccessException Then
                Return True
            End If

            If TypeOf ex Is FileNotFoundException OrElse
               TypeOf ex Is DirectoryNotFoundException OrElse
               TypeOf ex IsNot IOException Then

                Return False

            End If

            Dim code As Integer =
                ex.HResult And &HFFFF

            Return code = SharingViolation OrElse
                   code = LockViolation

        End Function

    End Class


    ' A storage file held open by another program. Not damage: the loader
    ' stops here, and neither the file nor its backup is touched.
    Friend Class StorageFileInUseException
        Inherits IOException

        Public Sub New(
            filePath As String,
            inner As Exception
        )

            MyBase.New(
                "PaperRoute could not open " &
                Path.GetFileName(filePath) &
                " because the file is in use by another program. " &
                "Close the program that has it open, then start PaperRoute again. " &
                "Nothing has been changed.",
                inner
            )

        End Sub

    End Class

End Namespace
