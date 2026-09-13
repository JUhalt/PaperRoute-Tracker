Imports System
Imports System.Collections
Imports System.IO
Imports System.Linq
Imports System.Reflection
Imports System.Runtime.ExceptionServices
Imports System.Runtime.Loader
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline.Services

<TestClass>
Public Class IsolatedSessionStorageTests

    <TestMethod>
    Public Sub IsolatedSession_DefaultRepositoriesAndSettingsRoundTripUnderSessionRoot()

        Using session As New IsolatedStorageAssembly()
            session.Configure(session.Root)
            Dim currentData As String = Path.Combine(session.Root, "current-data")
            Dim managed As String = Path.Combine(session.Root, "managed-library")
            Assert.AreEqual(currentData, session.StoragePath("CurrentDataRoot"))
            Assert.AreEqual(Path.Combine(session.Root, "legacy-data"), session.StoragePath("LegacyDataRoot"))
            Assert.AreEqual(managed, session.StoragePath("CurrentManagedLibraryRoot"))
            Assert.AreEqual(Path.Combine(session.Root, "legacy-managed-library"), session.StoragePath("LegacyManagedLibraryRoot"))
            Assert.AreEqual("Disposable demo session", session.CallStatic("StorageEnvironment", "ProfileDisplayName"))

            Dim authors As Object = session.Create("Services.AuthorLibraryRepository")
            Dim library As Object = session.CallInstance(authors, "Load")
            Dim journal As Object = session.Create("Models.JournalRecord")
            journal.GetType().GetProperty("Name").SetValue(journal, "Synthetic session journal")
            DirectCast(library.GetType().GetProperty("Journals").GetValue(library), IList).Add(journal)
            session.CallInstance(authors, "Save", library)
            Dim reloadedLibrary As Object = session.CallInstance(authors, "Load")
            Dim journals As IList = DirectCast(reloadedLibrary.GetType().GetProperty("Journals").GetValue(reloadedLibrary), IList)
            Assert.AreEqual(1, journals.Count)
            Assert.AreEqual("Synthetic session journal", journals(0).GetType().GetProperty("Name").GetValue(journals(0)))
            Assert.AreEqual(Path.Combine(currentData, "data", "authors.json"),
                authors.GetType().GetProperty("DataFilePath").GetValue(authors))

            Dim repository As Object = session.Create("Services.ManuscriptRepository")
            Dim manuscripts As IList = DirectCast(session.CallInstance(repository, "Load"), IList)
            Dim manuscript As Object = session.Create("Models.Manuscript")
            manuscript.GetType().GetProperty("Title").SetValue(manuscript, "Disposable workflow manuscript")
            manuscripts.Add(manuscript)
            session.CallInstance(repository, "Save", manuscripts)
            Dim reloaded As IList = DirectCast(session.CallInstance(repository, "Load"), IList)
            Assert.AreEqual(1, reloaded.Count)
            Assert.AreEqual("Disposable workflow manuscript", reloaded(0).GetType().GetProperty("Title").GetValue(reloaded(0)))
            Assert.AreEqual(Path.Combine(currentData, "data", "manuscripts.json"),
                repository.GetType().GetProperty("DataFilePath").GetValue(repository))

            Dim settingsService As Object = session.Create("Services.AppSettingsService")
            Dim settings As Object = session.Create("Models.AppSettings")
            settings.GetType().GetProperty("RevisionWarningDays").SetValue(settings, 23)
            session.CallInstance(settingsService, "Save", settings)
            Dim reloadedSettings As Object = session.CallInstance(settingsService, "Load")
            Assert.AreEqual(23, CInt(reloadedSettings.GetType().GetProperty("RevisionWarningDays").GetValue(reloadedSettings)))
            Assert.IsTrue(File.Exists(Path.Combine(currentData, "settings.json")))

            Dim managedService As Object = session.Create("Services.ManagedLibraryService")
            Assert.AreEqual(managed, managedService.GetType().GetProperty("RootDirectory").GetValue(managedService))
            Assert.IsFalse(File.Exists(Path.Combine(currentData, "data", "schema.json")),
                "The isolated repositories must not trigger application startup migrations.")
        End Using

    End Sub

    <TestMethod>
    Public Sub UnconfiguredSession_KeepsNormalProfilePaths()

        Using session As New IsolatedStorageAssembly()
            Assert.AreEqual(StorageMigrationService.CurrentDataRoot(), session.StoragePath("CurrentDataRoot"))
            Assert.AreEqual(StorageMigrationService.LegacyDataRoot(), session.StoragePath("LegacyDataRoot"))
            Assert.AreEqual(StorageMigrationService.CurrentManagedLibraryRoot(), session.StoragePath("CurrentManagedLibraryRoot"))
            Assert.AreEqual(StorageMigrationService.LegacyManagedLibraryRoot(), session.StoragePath("LegacyManagedLibraryRoot"))
            Assert.IsFalse(Directory.Exists(session.Root))
        End Using

    End Sub

    <TestMethod>
    Public Sub ConfiguredSession_CannotBeReconfiguredEvenToTheSameRoot()

        Using session As New IsolatedStorageAssembly()
            session.Configure(session.Root)
            Assert.ThrowsExactly(Of InvalidOperationException)(Sub() session.Configure(session.Root))
            Dim anotherRoot As String = Path.Combine(Path.GetTempPath(), "PaperRoute-Rejected-" & Guid.NewGuid().ToString("N"))
            Assert.ThrowsExactly(Of InvalidOperationException)(Sub() session.Configure(anotherRoot))
            Assert.IsFalse(Directory.Exists(anotherRoot))
            Assert.AreEqual(Path.Combine(session.Root, "current-data"), session.StoragePath("CurrentDataRoot"))
        End Using

    End Sub

    <TestMethod>
    <DataRow("CurrentDataRoot")>
    <DataRow("LegacyDataRoot")>
    <DataRow("CurrentManagedLibraryRoot")>
    <DataRow("LegacyManagedLibraryRoot")>
    Public Sub ResolvingAnyNormalRoot_PreventsLaterSessionSwitch(resolver As String)

        Using session As New IsolatedStorageAssembly()
            session.StoragePath(resolver)
            Assert.ThrowsExactly(Of InvalidOperationException)(Sub() session.Configure(session.Root))
            Assert.IsFalse(Directory.Exists(session.Root))
        End Using

    End Sub

    <TestMethod>
    Public Sub ConfigureSession_RejectsExistingDirectoryWithoutChangingItsContents()

        Using session As New IsolatedStorageAssembly()
            Directory.CreateDirectory(session.Root)
            Dim sentinel As String = Path.Combine(session.Root, "keep.txt")
            File.WriteAllText(sentinel, "existing content")
            Assert.ThrowsExactly(Of ArgumentException)(Sub() session.Configure(session.Root))
            Assert.AreEqual("existing content", File.ReadAllText(sentinel))
        End Using

    End Sub

    <TestMethod>
    Public Sub ConfigureSession_RejectsNonUniqueOrNonTemporaryRoots()

        Using session As New IsolatedStorageAssembly()
            For Each invalidRoot As String In {
                "relative-session", Path.GetTempPath(),
                Path.Combine(session.Root, "nested"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "PaperRoute-Rejected-" & Guid.NewGuid().ToString("N"))
            }
                Assert.ThrowsExactly(Of ArgumentException)(Sub() session.Configure(invalidRoot))
                Assert.IsFalse(Directory.Exists(session.Root))
            Next
            ' Invalid arguments do not consume the one successful configuration.
            session.Configure(session.Root)
            Assert.IsTrue(Directory.Exists(session.Root))
        End Using

    End Sub

    ' A fresh collectible assembly context exercises the real process-wide seam
    ' without configuring or resetting the test host's ordinary application copy.
    Private NotInheritable Class IsolatedStorageAssembly
        Implements IDisposable

        Private ReadOnly _context As New AssemblyLoadContext("PaperRoute isolated storage " & Guid.NewGuid().ToString("N"), True)
        Private ReadOnly _assembly As Assembly = _context.LoadFromAssemblyPath(GetType(StorageEnvironment).Assembly.Location)
        Public ReadOnly Property Root As String = Path.Combine(Path.GetTempPath(), "PaperRoute-Isolation-Test-" & Guid.NewGuid().ToString("N"))

        Public Sub Configure(rootDirectory As String)
            CallStatic("StorageEnvironment", "ConfigureIsolatedSessionRoot", rootDirectory)
        End Sub

        Public Function StoragePath(methodName As String) As String
            Return CStr(CallStatic("StorageMigrationService", methodName))
        End Function

        Public Function Create(typeName As String) As Object
            Return Activator.CreateInstance(_assembly.GetType("ManuscriptPipeline." & typeName, True))
        End Function

        Public Function CallStatic(typeName As String, methodName As String, ParamArray arguments As Object()) As Object
            Dim method As MethodInfo = _assembly.GetType("ManuscriptPipeline.Services." & typeName, True).
                GetMethods(BindingFlags.Public Or BindingFlags.NonPublic Or BindingFlags.Static).
                Single(Function(item) item.Name = methodName AndAlso item.GetParameters().Length = arguments.Length)
            Return Invoke(method, Nothing, arguments)
        End Function

        Public Function CallInstance(target As Object, methodName As String, ParamArray arguments As Object()) As Object
            Return Invoke(target.GetType().GetMethod(methodName), target, arguments)
        End Function

        Private Shared Function Invoke(method As MethodInfo, target As Object, arguments As Object()) As Object
            Try
                Return method.Invoke(target, arguments)
            Catch ex As TargetInvocationException When ex.InnerException IsNot Nothing
                ExceptionDispatchInfo.Capture(ex.InnerException).Throw()
                Throw
            End Try
        End Function

        Public Sub Dispose() Implements IDisposable.Dispose
            _context.Unload()
            ' Only this test's validated, unique direct child of temp is removed.
            Dim normalized As String = Path.GetFullPath(Root)
            Dim temporaryRoot As String = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()))
            If String.Equals(Path.GetDirectoryName(normalized), temporaryRoot, StringComparison.OrdinalIgnoreCase) AndAlso
               Path.GetFileName(normalized).StartsWith("PaperRoute-Isolation-Test-", StringComparison.Ordinal) Then
                DeleteTemporaryRoot(normalized)
            End If
        End Sub

    End Class

End Class
