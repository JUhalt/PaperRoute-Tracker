Imports System
Imports System.Diagnostics
Imports System.IO

Namespace Services

    Public NotInheritable Class StorageEnvironment

        Private Const DevelopmentOverrideVariable As String =
            "PAPERROUTE_DEV_STORAGE"

        Private Shared ReadOnly SessionRootLock As New Object()
        Private Shared _isolatedSessionRoot As String
        Private Shared _storageRootResolved As Boolean


        Private Sub New()
        End Sub


        ' This is available only to the certification harness and tests through
        ' InternalsVisibleTo. Normal application startup never configures a session.
        Friend Shared Sub ConfigureIsolatedSessionRoot(rootDirectory As String)

            SyncLock SessionRootLock
                If _isolatedSessionRoot IsNot Nothing OrElse _storageRootResolved Then
                    Throw New InvalidOperationException(
                        "An isolated session must be configured once, before any storage root is resolved.")
                End If

                If String.IsNullOrWhiteSpace(rootDirectory) OrElse
                   Not Path.IsPathFullyQualified(rootDirectory) Then
                    Throw New ArgumentException(
                        "An absolute temporary session directory is required.", NameOf(rootDirectory))
                End If

                Dim root As String = Path.TrimEndingDirectorySeparator(Path.GetFullPath(rootDirectory))
                Dim temporaryRoot As String = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()))
                If Not String.Equals(Path.GetDirectoryName(root), temporaryRoot, StringComparison.OrdinalIgnoreCase) Then
                    Throw New ArgumentException(
                        "The session directory must be a new direct child of the operating system temporary directory.",
                        NameOf(rootDirectory))
                End If

                If Directory.Exists(root) OrElse File.Exists(root) Then
                    Throw New ArgumentException(
                        "The session directory already exists. Use a new unique temporary directory.", NameOf(rootDirectory))
                End If

                Directory.CreateDirectory(root)
                _isolatedSessionRoot = root
            End SyncLock

        End Sub


        Friend Shared ReadOnly Property IsIsolatedSession As Boolean
            Get
                SyncLock SessionRootLock
                    Return _isolatedSessionRoot IsNot Nothing
                End SyncLock
            End Get
        End Property


        Friend Shared Function ResolveStorageRoot(
            isolatedFolderName As String,
            normalRoot As Func(Of String)
        ) As String

            SyncLock SessionRootLock
                _storageRootResolved = True
                If _isolatedSessionRoot IsNot Nothing Then
                    Return Path.Combine(_isolatedSessionRoot, isolatedFolderName)
                End If
                Return normalRoot()
            End SyncLock

        End Function


        Public Shared Function IsDevelopmentProfile() As Boolean

            Return ShouldUseDevelopmentProfile(
                Debugger.IsAttached,
                Environment.GetEnvironmentVariable(
                    DevelopmentOverrideVariable
                )
            )

        End Function


        Friend Shared Function ShouldUseDevelopmentProfile(
            debuggerAttached As Boolean,
            overrideValue As String
        ) As Boolean

            If debuggerAttached Then
                Return True
            End If

            If String.IsNullOrWhiteSpace(overrideValue) Then
                Return False
            End If

            Select Case overrideValue.Trim().ToLowerInvariant()

                Case "1",
                     "true",
                     "yes",
                     "on",
                     "dev",
                     "development"

                    Return True

                Case Else
                    Return False

            End Select

        End Function


        Public Shared Function DataFolderName() As String

            If IsDevelopmentProfile() Then
                Return ProductInfo.DevelopmentDataFolderName
            End If

            Return ProductInfo.DataFolderName

        End Function


        Public Shared Function ManagedLibraryFolderName() As String

            If IsDevelopmentProfile() Then
                Return ProductInfo.DevelopmentManagedLibraryFolderName
            End If

            Return ProductInfo.ManagedLibraryFolderName

        End Function


        Public Shared Function LegacyDataFolderName() As String

            If IsDevelopmentProfile() Then
                Return ProductInfo.DevelopmentLegacyDataFolderName
            End If

            Return ProductInfo.LegacyDataFolderName

        End Function


        Public Shared Function LegacyManagedLibraryFolderName() As String

            If IsDevelopmentProfile() Then
                Return ProductInfo.DevelopmentLegacyManagedLibraryFolderName
            End If

            Return ProductInfo.LegacyManagedLibraryFolderName

        End Function


        Public Shared Function ProfileDisplayName() As String

            If IsIsolatedSession Then
                Return "Disposable demo session"
            End If

            If IsDevelopmentProfile() Then
                Return "Development (isolated)"
            End If

            Return "Standard"

        End Function

    End Class

End Namespace
