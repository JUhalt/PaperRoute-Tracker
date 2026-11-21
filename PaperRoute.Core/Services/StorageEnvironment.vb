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

        ' For tests: every default root under a folder of their own, so a
        ' test that builds the Diagnostics report or loads settings never
        ' reads, lists or writes the user's profile. Set in TestInitialize
        ' and cleared in TestCleanup by classes that do not run in parallel.
        Friend Shared RootOverride As String = Nothing

        ' For tests (#135): how many times the default root answered, that
        ' is, neither an isolated session nor RootOverride stood in. The
        ' view catalog resets it before each view and expects zero after.
        Friend Shared DefaultRootUses As Integer = 0

        ' For tests (#395): what the system answers for a folder of the
        ' user's, and whether it is Windows, so a test can play a profile
        ' with no Documents folder without reading or making the real one.
        ' Set in a test and put back in its cleanup, like RootOverride.
        Friend Shared AskSystemFolder As Func(Of Environment.SpecialFolder, Environment.SpecialFolderOption, String) =
            AddressOf Environment.GetFolderPath
        Friend Shared RunsOnWindows As Func(Of Boolean) =
            AddressOf OperatingSystem.IsWindows


        Private Sub New()
        End Sub


        ' Where a folder of the user's is, such as Documents (#395). Linux
        ' and macOS answer with an empty string for a folder that does not
        ' exist yet, as ~/Documents on a fresh profile or a CI runner. There
        ' the path is asked for without that check, and the folder is made
        ' on first use, by the Directory.CreateDirectory that makes
        ' PaperRoute's own folder inside it. Windows answers as before: a
        ' redirected folder that is missing right now (OneDrive, a network
        ' share) still answers empty, and the callers still refuse it.
        Friend Shared Function UserFolder(folder As Environment.SpecialFolder) As String

            Dim answer As Environment.SpecialFolderOption =
                If(RunsOnWindows.Invoke(),
                   Environment.SpecialFolderOption.None,
                   Environment.SpecialFolderOption.DoNotVerify)

            Return AskSystemFolder.Invoke(folder, answer)

        End Function


        ' Called once, before any storage root is resolved: by the example
        ' library's own process (--example, #83), and by the certification
        ' harness and tests through InternalsVisibleTo. A normal launch never
        ' configures a session, so it always opens the user's own library.
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
                If Not String.Equals(Path.GetDirectoryName(root), temporaryRoot, PathComparison.Comparison) Then
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
                If RootOverride IsNot Nothing Then
                    Return Path.Combine(RootOverride, isolatedFolderName)
                End If
                DefaultRootUses += 1
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
