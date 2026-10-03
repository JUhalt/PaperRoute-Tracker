Imports System
Imports System.Collections
Imports System.Collections.Generic
Imports System.Drawing
Imports System.IO
Imports System.Linq
Imports System.Reflection
Imports System.Runtime.ExceptionServices
Imports System.Runtime.Loader
Imports System.Security.Cryptography
Imports System.Threading
Imports System.Windows.Forms
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

    ' #83 and #96, end to end: the example's own process, as Program.Main
    ' runs it for --example, in a fresh copy of PaperRoute. The window opens
    ' on the seeded example, a manuscript and a setting are changed and
    ' saved, and Return to My Library closes it. The researcher's profile,
    ' the four folders an ordinary launch on this computer uses, must be
    ' byte-identical afterwards. Only paths, sizes, times, and hashes are
    ' compared; no file's contents are kept or shown.
    <TestMethod>
    <DoNotParallelize>
    Public Sub TheExampleLeavesTheResearchersProfileByteIdentical()

        Dim profile As String() = {
            StorageMigrationService.CurrentDataRoot(),
            StorageMigrationService.LegacyDataRoot(),
            StorageMigrationService.CurrentManagedLibraryRoot(),
            StorageMigrationService.LegacyManagedLibraryRoot()
        }

        ' A running PaperRoute could save while the profile is compared.
        Dim running As Mutex = Nothing
        If Mutex.TryOpenExisting("Local\" & SingleInstanceService.KeyFor(profile(0)) & "-instance", running) Then
            running.Dispose()
            Assert.Inconclusive("PaperRoute has this profile open. Close it and run the test again.")
        End If

        Dim before As List(Of String) = SnapshotProfile(profile)
        Const EditedTitle As String = "Example: pilot notes on reading fluency, edited in the example"

        RunOnStaThread(
            Sub()
                Using session As New IsolatedStorageAssembly()

                    ' First, before any folder is resolved, as in Program.Main.
                    session.CallStatic("ExampleLibraryService", "StartSession")
                    Dim currentData As String = session.StoragePath("CurrentDataRoot")
                    Dim root As String = Path.GetDirectoryName(currentData)
                    Dim window As Form = Nothing

                    Try
                        StringAssert.StartsWith(Path.GetFileName(root), "PaperRoute-Example-")
                        For Each resolver As String In {"CurrentDataRoot", "LegacyDataRoot", "CurrentManagedLibraryRoot", "LegacyManagedLibraryRoot"}
                            StringAssert.StartsWith(session.StoragePath(resolver), root & Path.DirectorySeparatorChar)
                        Next

                        session.CallStatic("StorageMigrationService", "EnsureCurrentStorage")
                        Dim offline As Object = session.Create("Models.OnlineServicesSettings")
                        offline.GetType().GetProperty("WorkOffline").SetValue(offline, True)
                        session.CallStatic("ExampleLibraryService", "Seed", DateTime.Today, offline)

                        window = DirectCast(session.Create("Form1"), Form)
                        ' Closing with unsaved changes would ask; record it instead.
                        Dim asked As Boolean = False
                        SetField(window, "unsavedChangesPrompt",
                            New Func(Of String, DialogResult)(
                                Function(title)
                                    asked = True
                                    Return DialogResult.No
                                End Function))
                        ShowOffscreen(window)
                        Assert.IsFalse(window.IsDisposed, "The example opens.")
                        Assert.AreEqual("PaperRoute Tracker - Example Library", window.Text)
                        Assert.IsTrue(Descendants(window).OfType(Of Button)().Any(Function(button) button.Text = "Return to My Library"), "The banner is shown.")

                        ' Edit a manuscript and save it.
                        Dim library As IList = DirectCast(GetField(window, "manuscripts"), IList)
                        Dim idea As Object = library.Cast(Of Object)().Single(
                            Function(item) CStr(item.GetType().GetProperty("Title").GetValue(item)) = "Example: pilot notes on reading fluency")
                        CallMember(window, "OpenManuscript", idea, Nothing)
                        Application.DoEvents()
                        Dim editor As Control = DirectCast(GetField(window, "manuscriptEditor"), Control)
                        Descendants(editor).OfType(Of TextBox)().Single(Function(box) box.AccessibleName = "Title").Text = EditedTitle
                        Assert.IsTrue(CBool(CallMember(editor, "HasUnsavedChanges")))
                        Assert.IsTrue(CBool(CallMember(window, "SaveManuscriptPage")), "The example saves.")
                        Dim saved As String = File.ReadAllText(Path.Combine(currentData, "data", "manuscripts.json"))
                        StringAssert.Contains(saved, EditedTitle, "The edit is saved in the example's own folder.")
                        StringAssert.Contains(saved, "Submission to Fictional Open Psychology", "The sample packet passes the save checks.")

                        ' Change a setting, which is saved at once.
                        CallMember(window, "ApplyNavigationCollapsed", True, True)
                        Dim settings As Object = session.CallInstance(session.Create("Services.AppSettingsService"), "Load")
                        Assert.IsTrue(CBool(settings.GetType().GetProperty("NavigationCollapsed").GetValue(settings)),
                                      "The setting is saved in the example's own folder.")

                        ' Help inside the example doesn't offer the example.
                        Using guide As Form = DirectCast(CallMember(window, "CreateUserGuide"), Form)
                            Dim footer As List(Of String) = Descendants(guide).OfType(Of Button)().Select(Function(button) button.Text).ToList()
                            CollectionAssert.Contains(footer, "Open Guide on GitHub")
                            CollectionAssert.DoesNotContain(footer, "Explore an Example Library...")
                        End Using

                        Descendants(window).OfType(Of Button)().Single(Function(button) button.Text = "Return to My Library").PerformClick()
                        Application.DoEvents()
                        Assert.IsTrue(window.IsDisposed, "Return to My Library closes the example.")
                        Assert.IsFalse(asked, "Nothing unsaved was left to ask about.")

                        ' As Program.Main does once the window has closed. Antivirus
                        ' may still be reading a file just written to Temp.
                        For attempt As Integer = 1 To 20
                            session.CallStatic("ExampleLibraryService", "EndSession")
                            If Not Directory.Exists(root) Then Exit For
                            Thread.Sleep(250)
                        Next
                        Assert.IsFalse(Directory.Exists(root), "The example is discarded when its window closes.")

                    Finally
                        If window IsNot Nothing AndAlso Not window.IsDisposed Then window.Dispose()
                        ' Only the example's own folder, a direct child of Temp.
                        Dim temporaryRoot As String = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()))
                        If Directory.Exists(root) AndAlso
                           String.Equals(Path.GetDirectoryName(Path.GetFullPath(root)), temporaryRoot, StringComparison.OrdinalIgnoreCase) AndAlso
                           Path.GetFileName(root).StartsWith("PaperRoute-Example-", StringComparison.Ordinal) Then
                            DeleteTemporaryRoot(root)
                        End If
                    End Try

                End Using
            End Sub)

        Dim after As List(Of String) = SnapshotProfile(profile)
        Dim changes As String = String.Join("; ",
            before.Except(after).Select(Function(entry) "was " & entry).
            Concat(after.Except(before).Select(Function(entry) "now " & entry)))
        Assert.AreEqual(String.Empty, changes, "The researcher's profile is byte-identical.")
        Assert.AreEqual(profile(0), StorageMigrationService.CurrentDataRoot())
        Assert.IsFalse(ExampleLibraryService.IsActive, "The test host never became the example.")

    End Sub

    ' Every root, folder, and file of the profile: paths, sizes, times, and
    ' SHA-256 hashes, never contents. A file kept only in the cloud (such as
    ' OneDrive's Documents) is not hashed, so nothing is downloaded.
    Private Shared Function SnapshotProfile(roots As IEnumerable(Of String)) As List(Of String)

        Const RecallOnOpen As Integer = &H40000
        Const RecallOnDataAccess As Integer = &H400000
        Dim cloudOnly As FileAttributes = FileAttributes.Offline Or CType(RecallOnOpen Or RecallOnDataAccess, FileAttributes)
        Dim entries As New List(Of String)()

        For Each profileRoot As String In roots
            If Not Directory.Exists(profileRoot) Then
                entries.Add(profileRoot & " | none")
                Continue For
            End If
            For Each folderPath As String In {profileRoot}.Concat(Directory.EnumerateDirectories(profileRoot, "*", SearchOption.AllDirectories))
                entries.Add(String.Join(" | ", folderPath, "folder", Directory.GetLastWriteTimeUtc(folderPath).Ticks))
            Next
            For Each filePath As String In Directory.EnumerateFiles(profileRoot, "*", SearchOption.AllDirectories)
                Dim info As New FileInfo(filePath)
                Dim hash As String = "in the cloud, not hashed"
                If (info.Attributes And cloudOnly) = 0 Then
                    Using stream As New FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite Or FileShare.Delete)
                        hash = Convert.ToHexString(SHA256.HashData(stream))
                    End Using
                End If
                entries.Add(String.Join(" | ", filePath, info.Length, info.LastWriteTimeUtc.Ticks, hash))
            Next
        Next

        entries.Sort(StringComparer.Ordinal)
        Return entries

    End Function

    ' The fresh copy's types are not this assembly's, so its window is
    ' driven through Form, Control, and reflection.
    Private Shared Function CallMember(target As Object, methodName As String, ParamArray arguments As Object()) As Object
        Dim method As MethodInfo = target.GetType().GetMethod(methodName, BindingFlags.Instance Or BindingFlags.Public Or BindingFlags.NonPublic)
        Try
            Return method.Invoke(target, arguments)
        Catch ex As TargetInvocationException When ex.InnerException IsNot Nothing
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw()
            Throw
        End Try
    End Function

    Private Shared Function GetField(target As Object, fieldName As String) As Object
        Return target.GetType().GetField(fieldName, BindingFlags.Instance Or BindingFlags.Public Or BindingFlags.NonPublic).GetValue(target)
    End Function

    Private Shared Sub SetField(target As Object, fieldName As String, value As Object)
        target.GetType().GetField(fieldName, BindingFlags.Instance Or BindingFlags.Public Or BindingFlags.NonPublic).SetValue(target, value)
    End Sub

    ' Children report Visible only while their form is shown.
    Private Shared Sub ShowOffscreen(form As Form)
        form.StartPosition = FormStartPosition.Manual
        form.Location = New Point(-20000, -20000)
        form.ShowInTaskbar = False
        form.Show()
        form.Location = New Point(-20000, -20000)
        Application.DoEvents()
    End Sub

    Private Shared Iterator Function Descendants(parent As Control) As IEnumerable(Of Control)
        For Each child As Control In parent.Controls
            Yield child
            For Each descendant As Control In Descendants(child)
                Yield descendant
            Next
        Next
    End Function

    Private Shared Sub RunOnStaThread(action As Action)
        Dim failure As Exception = Nothing
        Dim worker As New Thread(
            Sub()
                Try
                    action()
                Catch ex As Exception
                    failure = ex
                End Try
            End Sub) With {.IsBackground = True}
        worker.SetApartmentState(ApartmentState.STA)
        worker.Start()
        If Not worker.Join(TimeSpan.FromSeconds(90)) Then
            Assert.Fail("The example window test timed out; a message box may be open on its thread.")
        End If
        If failure IsNot Nothing Then ExceptionDispatchInfo.Capture(failure).Throw()
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
