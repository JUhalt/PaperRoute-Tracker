Imports System

' The core's clock (core stage 2, design 6.2). While PlatformServices.Clock
' is the system's, each member returns the very call it stands for:
' DateTime.Now (Kind Local), DateTime.UtcNow (Kind Utc) and DateTime.Today.
' A test clock gives the instant; this machine's zone gives the wall time
' (Kind Local), as DateTime.Now does. Stored as before: the library file
' drops the offset of every date but a ...Utc stamp (CalendarDateJson), and
' writers with plain options (settings, authors) keep the machine's offset.
Public NotInheritable Class CoreClock

    Private Sub New()
    End Sub


    Public Shared ReadOnly Property Now As DateTime
        Get
            Dim clock As TimeProvider = PlatformServices.Clock
            If clock Is TimeProvider.System Then Return DateTime.Now
            Return clock.GetUtcNow().UtcDateTime.ToLocalTime()
        End Get
    End Property


    Public Shared ReadOnly Property UtcNow As DateTime
        Get
            Dim clock As TimeProvider = PlatformServices.Clock
            If clock Is TimeProvider.System Then Return DateTime.UtcNow
            Return clock.GetUtcNow().UtcDateTime
        End Get
    End Property


    Public Shared ReadOnly Property Today As DateTime
        Get
            If PlatformServices.Clock Is TimeProvider.System Then Return DateTime.Today
            Return CoreClock.Now.Date
        End Get
    End Property

End Class
