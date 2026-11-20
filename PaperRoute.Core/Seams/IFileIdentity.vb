Imports System

' Whether two paths reach the same file (core stage 2, design 3.4). The host
' registers its implementation in PlatformServices.FileIdentity: the Windows
' file index, from core stage 2's step 11.
Public Interface IFileIdentity

    ' A text that every path to the same file shares, or Nothing when it is
    ' unknown; the caller then compares paths only.
    Function Identify(filePath As String) As String

End Interface


' The default until a host registers one, and the answer on Linux in v1.1:
' the identity is never known, so callers compare paths only.
Friend NotInheritable Class NoFileIdentity
    Implements IFileIdentity

    Public Shared ReadOnly Instance As New NoFileIdentity()


    Private Sub New()
    End Sub


    Public Function Identify(filePath As String) As String Implements IFileIdentity.Identify
        Return Nothing
    End Function

End Class
