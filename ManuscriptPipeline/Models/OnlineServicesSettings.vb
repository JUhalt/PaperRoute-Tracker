Imports System.Collections.Generic

Namespace Models

    ' Which online services PaperRoute may contact (#86). Everything is on
    ' until the user turns it off; a service added in a later release starts
    ' on, as the Online services page explains. The AI assistant is the
    ' exception: it is off until the user turns it on (#84).
    Public Class OnlineServicesSettings

        ' Turns every service off, the update check included, while keeping
        ' each service's own choice for when it is turned off again.
        Public Property WorkOffline As Boolean = False

        ' Ids of the services the user switched off (OnlineServiceCatalog).
        ' Unknown ids are ignored.
        Public Property TurnedOff As List(Of String) = New List(Of String)()

        ' The optional AI assistant (#84), which is off until turned on.
        Public Property Assistant As AssistantSettings = New AssistantSettings()

    End Class

End Namespace
