Imports System

' The core's new identifiers (core stage 2, design 6.2): exactly
' Guid.NewGuid() unless a test set another source in PlatformServices.NewId.
Public NotInheritable Class CoreIds

    Private Sub New()
    End Sub


    Public Shared Function NewId() As Guid
        Return PlatformServices.NewId.Invoke()
    End Function

End Class
