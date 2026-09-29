Imports System.Collections.Generic

Namespace Models

    ' Which online services PaperRoute may contact (#86). Everything is on
    ' until the user turns it off; a service added in a later release starts
    ' on, as the Online services page explains.
    Public Class OnlineServicesSettings

        ' Turns every service off, the update check included, while keeping
        ' each service's own choice for when it is turned off again.
        Public Property WorkOffline As Boolean = False

        ' Ids of the services the user switched off (OnlineServiceCatalog).
        ' Unknown ids are ignored.
        Public Property TurnedOff As List(Of String) = New List(Of String)()

    End Class

End Namespace
