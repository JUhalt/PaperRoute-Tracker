Imports System
Imports System.Runtime.Versioning

Namespace Services

    ' Everything the Windows app supplies to the core (core stage 2, design
    ' 3.3). One place, so the app, the tests and the certification tools
    ' register the same things. Each host calls Register once at start-up,
    ' before anything that could read a stored key (StartupRegistersThePlatformTests).
    <SupportedOSPlatform("windows")>
    Public NotInheritable Class WindowsPlatform

        Private Sub New()
        End Sub


        Public Shared Sub Register()
            PlatformServices.Secrets = New DpapiSecretProtector()
            ' The launcher and the file identity are added by the steps that
            ' bring their Windows implementations. The clock and the ids stay
            ' on their defaults: the system clock and Guid.NewGuid.
        End Sub

    End Class

End Namespace
