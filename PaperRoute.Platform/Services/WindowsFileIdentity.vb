Imports System
Imports System.Globalization
Imports System.IO
Imports System.Runtime.InteropServices
Imports System.Runtime.Versioning
Imports System.Security
Imports Microsoft.Win32.SafeHandles

Namespace Services

    ' Whether two paths reach the same file, the Windows way (core stage 2,
    ' step 11): the volume serial number and file index, which every path to
    ' one file shares, a mapped drive, a junction or the \\?\ form included.
    ' Moved unchanged from PacketExportService, which asks
    ' PlatformServices.FileIdentity before writing a packet's .zip, so packet
    ' export answers "is this one of the packet's own files" as in 1.0.x.
    <SupportedOSPlatform("windows")>
    Public NotInheritable Class WindowsFileIdentity
        Implements IFileIdentity

        ' The volume serial number and file index, which every path to one
        ' file shares; Nothing when the file isn't there or can't be opened.
        Public Function Identify(filePath As String) As String Implements IFileIdentity.Identify
            Try
                If String.IsNullOrWhiteSpace(filePath) OrElse Not File.Exists(filePath) Then Return Nothing
                Using handle As SafeFileHandle = File.OpenHandle(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite Or FileShare.Delete)
                    Dim information As ByHandleFileInformation
                    If Not GetFileInformationByHandle(handle, information) Then Return Nothing
                    Return IdentityText(information.VolumeSerialNumber, information.FileIndexHigh, information.FileIndexLow)
                End Using
            Catch ex As Exception When IsFileAccessException(ex)
                Return Nothing
            End Try
        End Function


        ' Nothing for a file index of 0, which some file servers report for
        ' every file: the identity is then unknown, and only the path
        ' comparison applies, so such files are never mistaken for each other.
        Public Shared Function IdentityText(volumeSerialNumber As UInteger, fileIndexHigh As UInteger, fileIndexLow As UInteger) As String
            If fileIndexHigh = 0UI AndAlso fileIndexLow = 0UI Then Return Nothing
            Return volumeSerialNumber.ToString("x8", CultureInfo.InvariantCulture) & ":" &
                fileIndexHigh.ToString("x8", CultureInfo.InvariantCulture) &
                fileIndexLow.ToString("x8", CultureInfo.InvariantCulture)
        End Function


        ' The same five types PacketExportService treats as a file it can't
        ' reach (its own copy is Private there).
        Private Shared Function IsFileAccessException(ex As Exception) As Boolean
            Return TypeOf ex Is IOException OrElse
                TypeOf ex Is UnauthorizedAccessException OrElse
                TypeOf ex Is SecurityException OrElse
                TypeOf ex Is ArgumentException OrElse
                TypeOf ex Is NotSupportedException
        End Function


        <StructLayout(LayoutKind.Sequential)>
        Private Structure ByHandleFileInformation
            Public FileAttributes As UInteger
            Public CreationTimeLow As UInteger
            Public CreationTimeHigh As UInteger
            Public LastAccessTimeLow As UInteger
            Public LastAccessTimeHigh As UInteger
            Public LastWriteTimeLow As UInteger
            Public LastWriteTimeHigh As UInteger
            Public VolumeSerialNumber As UInteger
            Public FileSizeHigh As UInteger
            Public FileSizeLow As UInteger
            Public NumberOfLinks As UInteger
            Public FileIndexHigh As UInteger
            Public FileIndexLow As UInteger
        End Structure


        <DllImport("kernel32.dll", SetLastError:=True)>
        Private Shared Function GetFileInformationByHandle(handle As SafeFileHandle, <Out> ByRef information As ByHandleFileInformation) As <MarshalAs(UnmanagedType.Bool)> Boolean
        End Function

    End Class

End Namespace
