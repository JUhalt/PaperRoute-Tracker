Imports System

' What the host (the Windows Forms app, a second interface, a test project)
' supplies before the core is used (core stage 2, design 3.2). Each has a
' default that is safe on every system: no key store, no launcher, no file
' identity, the system clock and Guid.NewGuid. Set once, at start-up, on the
' main thread; read from anywhere. The clock and the ids are left on their
' defaults by every host; only tests change them.
Public NotInheritable Class PlatformServices

    Private Sub New()
    End Sub


    Private Shared _secrets As ISecretProtector = UnavailableSecretProtector.Instance
    Private Shared _launcher As ILauncher = UnavailableLauncher.Instance
    Private Shared _fileIdentity As IFileIdentity = NoFileIdentity.Instance
    Private Shared _clock As TimeProvider = TimeProvider.System
    Private Shared _newId As Func(Of Guid) = AddressOf Guid.NewGuid


    Public Shared Property Secrets As ISecretProtector
        Get
            Return _secrets
        End Get
        Set(value As ISecretProtector)
            If value Is Nothing Then Throw New ArgumentNullException(NameOf(value))
            _secrets = value
        End Set
    End Property


    Public Shared Property Launcher As ILauncher
        Get
            Return _launcher
        End Get
        Set(value As ILauncher)
            If value Is Nothing Then Throw New ArgumentNullException(NameOf(value))
            _launcher = value
        End Set
    End Property


    Public Shared Property FileIdentity As IFileIdentity
        Get
            Return _fileIdentity
        End Get
        Set(value As IFileIdentity)
            If value Is Nothing Then Throw New ArgumentNullException(NameOf(value))
            _fileIdentity = value
        End Set
    End Property


    ' Read through CoreClock, which returns DateTime.Now and DateTime.UtcNow
    ' themselves while this is TimeProvider.System.
    Public Shared Property Clock As TimeProvider
        Get
            Return _clock
        End Get
        Set(value As TimeProvider)
            If value Is Nothing Then Throw New ArgumentNullException(NameOf(value))
            _clock = value
        End Set
    End Property


    ' Read through CoreIds.NewId: Visual Basic reads PlatformServices.NewId()
    ' as this property, not as a call of the delegate.
    Public Shared Property NewId As Func(Of Guid)
        Get
            Return _newId
        End Get
        Set(value As Func(Of Guid))
            If value Is Nothing Then Throw New ArgumentNullException(NameOf(value))
            _newId = value
        End Set
    End Property


    ' For tests: the clock, the ids and the file identity back to their
    ' defaults, and the given key protector and launcher (the suite's own)
    ' set again, in one call, so no test class can forget one of them.
    Friend Shared Sub ResetForTests(secrets As ISecretProtector, launcher As ILauncher)

        If secrets Is Nothing Then Throw New ArgumentNullException(NameOf(secrets))
        If launcher Is Nothing Then Throw New ArgumentNullException(NameOf(launcher))

        _secrets = secrets
        _launcher = launcher
        _fileIdentity = NoFileIdentity.Instance
        _clock = TimeProvider.System
        _newId = AddressOf Guid.NewGuid

    End Sub

End Class
