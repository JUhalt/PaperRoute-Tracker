Imports System
Imports System.Runtime.InteropServices
Imports System.Security.Cryptography
Imports System.Text
Imports System.Threading

Namespace Services

    ' One PaperRoute window per library (#77). Two windows on the same
    ' library each keep their own copy in memory, so a save in one could
    ' overwrite the other. A second launch instead asks the running window to
    ' come forward and exits before touching storage. The key is the
    ' library's folder, so the development profile and isolated test
    ' sessions never collide with an installed PaperRoute.
    Friend NotInheritable Class SingleInstanceService
        Implements IDisposable

        Private Const AnyProcess As Integer = -1

        <DllImport("user32.dll")>
        Private Shared Function AllowSetForegroundWindow(processId As Integer) As Boolean
        End Function

        Private ReadOnly _mutex As Mutex
        Private ReadOnly _activate As EventWaitHandle
        Private ReadOnly _stop As New ManualResetEvent(False)
        Private _listener As Thread

        Private Sub New(mutex As Mutex, activate As EventWaitHandle)
            _mutex = mutex
            _activate = activate
        End Sub


        Public Shared Function KeyFor(dataRoot As String) As String
            Dim normalized As String = If(dataRoot, String.Empty).Trim().TrimEnd("\"c, "/"c).ToUpperInvariant()
            Dim hash As Byte() = SHA256.HashData(Encoding.UTF8.GetBytes(normalized))
            Return "PaperRoute-" & Convert.ToHexString(hash, 0, 12)
        End Function


        ' The running instance for this library, or Nothing when another
        ' process already has it open.
        Public Shared Function TryStart(key As String) As SingleInstanceService

            Dim created As Boolean
            Dim mutex As New Mutex(True, "Local\" & key & "-instance", created)

            If Not created Then
                mutex.Dispose()
                Return Nothing
            End If

            Return New SingleInstanceService(mutex, New EventWaitHandle(False, EventResetMode.AutoReset, "Local\" & key & "-activate"))

        End Function


        ' Asks the running instance to come forward. Returns whether one was found.
        Public Shared Function SignalExisting(key As String) As Boolean

            Dim activate As EventWaitHandle = Nothing
            If Not EventWaitHandle.TryOpenExisting("Local\" & key & "-activate", activate) Then Return False

            Using activate
                ' Windows lets the running window take the foreground only
                ' when the launching process allows it.
                AllowSetForegroundWindow(AnyProcess)
                activate.Set()
            End Using
            Return True

        End Function


        ' Runs onActivate on a background thread each time another launch
        ' signals; the caller marshals to its window.
        Public Sub Listen(onActivate As Action)

            _listener = New Thread(
                Sub()
                    Dim signals As WaitHandle() = {_activate, _stop}
                    While WaitHandle.WaitAny(signals) = 0
                        Try
                            onActivate()
                        Catch ex As Exception When TypeOf ex Is InvalidOperationException OrElse TypeOf ex Is ObjectDisposedException
                            ' The window is closing; there is nothing to bring forward.
                        End Try
                    End While
                End Sub) With {.IsBackground = True, .Name = "PaperRoute single instance"}
            _listener.Start()

        End Sub


        ' Must run on the thread that started the instance, which owns the mutex.
        Public Sub Dispose() Implements IDisposable.Dispose
            _stop.Set()
            _listener?.Join(TimeSpan.FromSeconds(2))
            Try
                _mutex.ReleaseMutex()
            Catch ex As ApplicationException
                ' Not owned by this thread; closing the handle still releases it.
            End Try
            _mutex.Dispose()
            _activate.Dispose()
            _stop.Dispose()
        End Sub

    End Class

End Namespace
