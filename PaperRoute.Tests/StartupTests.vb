Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.IO
Imports System.Linq
Imports System.Reflection
Imports System.Runtime.ExceptionServices
Imports System.Threading
Imports System.Windows.Forms
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline
Imports ManuscriptPipeline.Forms
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

' The window PaperRoute shows when it cannot start (#119), built from the
' three failures that used to end in a message box naming no file, and
' from the library file another program holds open (#111), whose message
' box named the file but offered none of the actions.
<TestClass>
<DoNotParallelize>
Public Class StartupTests

    Private _root As String = String.Empty


    <TestInitialize>
    Public Sub Initialize()

        _root =
            CreateTemporaryRoot()

        ' The Diagnostics report the window copies reads the settings and
        ' the schema marker and lists the logs and key folders: all under
        ' the temporary root, never the user's own profile.
        StorageEnvironment.RootOverride =
            _root

    End Sub


    <TestCleanup>
    Public Sub Cleanup()

        StorageEnvironment.RootOverride =
            Nothing

        DeleteTemporaryRoot(
            _root
        )

    End Sub


    <TestMethod>
    Public Sub BadSchema_WindowNamesTheSchemaFileAndOffersTheFourActions()

        Dim current As String =
            Path.Combine(
                _root,
                "current"
            )

        Dim schemaPath As String =
            StorageMigrationService.SchemaFilePath(
                current
            )

        Directory.CreateDirectory(
            Path.GetDirectoryName(
                schemaPath
            )
        )

        File.WriteAllText(
            schemaPath,
            "{""SchemaVersion"":999}"
        )

        Dim failure As Exception =
            Nothing

        Try

            StorageMigrationService.EnsureCurrentStorage(
                current,
                Path.Combine(_root, "legacy"),
                Path.Combine(_root, "library"),
                Path.Combine(_root, "legacy-library")
            )

        Catch ex As Exception

            failure =
                ex

        End Try

        Assert.IsNotNull(
            failure,
            "A schema newer than this build stops startup."
        )

        AssertWindow(
            StartupFailure.ForStorage(
                failure,
                schemaPath
            ),
            schemaPath
        )

    End Sub


    <TestMethod>
    Public Sub UnreadableLibrary_WindowNamesTheLibraryFileAndOffersTheFourActions()

        Dim dataDirectory As String =
            Path.Combine(
                _root,
                "data"
            )

        Directory.CreateDirectory(
            dataDirectory
        )

        File.WriteAllText(
            Path.Combine(
                dataDirectory,
                "manuscripts.json"
            ),
            "{ this is not a library"
        )

        Dim repository As New ManuscriptRepository(
            dataDirectory,
            Path.Combine(_root, "managed")
        )

        Dim failure As Exception =
            Nothing

        Try

            repository.Load()

        Catch ex As Exception

            failure =
                ex

        End Try

        Assert.IsNotNull(
            failure,
            "An unreadable library with no backup stops startup."
        )

        AssertWindow(
            StartupFailure.ForManuscriptLibrary(
                failure,
                repository.DataFilePath
            ),
            repository.DataFilePath
        )

    End Sub


    <TestMethod>
    Public Sub InvalidAuthorLibrary_WindowNamesTheAuthorFileAndOffersTheFourActions()

        Dim dataDirectory As String =
            Path.Combine(
                _root,
                "data"
            )

        Directory.CreateDirectory(
            dataDirectory
        )

        File.WriteAllText(
            Path.Combine(
                dataDirectory,
                "authors.json"
            ),
            "{""Authors"":"
        )

        Dim repository As New AuthorLibraryRepository(
            dataDirectory
        )

        Dim failure As Exception =
            Nothing

        Try

            repository.Load()

        Catch ex As Exception

            failure =
                ex

        End Try

        Assert.IsNotNull(
            failure,
            "An invalid author library with no backup stops startup."
        )

        AssertWindow(
            StartupFailure.ForAuthorLibrary(
                failure,
                repository.DataFilePath
            ),
            repository.DataFilePath
        )

    End Sub


    <TestMethod>
    Public Sub LibraryInUse_WindowNamesTheHeldFileAndOffersTheFourActions()

        Dim dataDirectory As String =
            Path.Combine(
                _root,
                "data"
            )

        Directory.CreateDirectory(
            dataDirectory
        )

        Dim dataFilePath As String =
            Path.Combine(
                dataDirectory,
                "manuscripts.json"
            )

        File.WriteAllText(
            dataFilePath,
            "[]"
        )

        ' Every open fails the way Windows fails when another program holds
        ' the file; no test waits on a real lock.
        Dim opener As New HeldFileOpener(
            dataFilePath,
            failures:=Integer.MaxValue
        )

        Dim repository As New ManuscriptRepository(
            dataDirectory,
            Path.Combine(_root, "managed"),
            openFile:=AddressOf opener.Open
        )

        Dim failure As StorageFileInUseException =
            Assert.ThrowsExactly(Of StorageFileInUseException)(
                Sub() repository.Load()
            )

        Dim inUse As StartupFailure =
            StartupFailure.ForLibraryInUse(
                failure,
                repository.DataFilePath
            )

        StringAssert.Contains(
            inUse.Heading,
            "another program",
            "The window says the file is in use, not damaged."
        )

        StringAssert.Contains(
            inUse.Explanation,
            "nothing was read, recovered or changed",
            "The window says the loader stopped before the file or its backup."
        )

        AssertWindow(
            inUse,
            repository.DataFilePath
        )

    End Sub


    ' The window offers Use the Previous Save only where the manuscript
    ' library or its storage failed and manuscripts.bak reads with the
    ' current rules, and Restore From Backup... wherever the library itself
    ' failed (#124). A file another program holds is not damage, so neither
    ' is offered for it, and the previous save is not offered for the
    ' author library, which it would not touch. Building the window changes
    ' nothing.
    <TestMethod>
    Public Sub RecoveryActions_AreOfferedExactlyWhereTheyCanPutTheLibraryRight()

        Dim repository As ManuscriptRepository =
            LibraryWithADamagedPrimary(
                "The previous save"
            )

        File.WriteAllText(
            StorageMigrationService.SchemaFilePath(),
            String.Empty
        )

        Dim cause As New InvalidDataException(
            "The test's failure."
        )

        Dim authorsPath As String =
            New AuthorLibraryRepository().DataFilePath

        Dim before As Dictionary(Of String, Byte()) =
            SnapshotData()

        CollectionAssert.AreEquivalent(
            {PreviousSaveAction, RestoreAction},
            RecoveryActions(StartupFailure.ForStorage(cause, StorageMigrationService.SchemaFilePath())),
            "Storage that failed beside a good previous save."
        )

        CollectionAssert.AreEquivalent(
            {PreviousSaveAction, RestoreAction},
            RecoveryActions(StartupFailure.ForManuscriptLibrary(cause, repository.DataFilePath)),
            "A manuscript library that failed beside a good previous save."
        )

        CollectionAssert.AreEquivalent(
            {RestoreAction},
            RecoveryActions(StartupFailure.ForAuthorLibrary(cause, authorsPath)),
            "The author library: a backup puts it back; the previous save would not."
        )

        CollectionAssert.AreEquivalent(
            Array.Empty(Of String)(),
            RecoveryActions(StartupFailure.ForLibraryInUse(New IOException("In use."), repository.DataFilePath)),
            "A file another program holds."
        )

        AssertSameData(
            before,
            "Offering changes nothing."
        )

        ' A previous save that does not read is not offered.
        File.WriteAllText(
            repository.BackupFilePath,
            "[null]"
        )

        CollectionAssert.AreEquivalent(
            {RestoreAction},
            RecoveryActions(StartupFailure.ForStorage(cause, StorageMigrationService.SchemaFilePath())),
            "Storage, beside a damaged previous save."
        )

        CollectionAssert.AreEquivalent(
            {RestoreAction},
            RecoveryActions(StartupFailure.ForManuscriptLibrary(cause, repository.DataFilePath)),
            "The manuscript library, beside a damaged previous save."
        )

    End Sub


    ' An empty schema.json beside a damaged primary and a good
    ' manuscripts.bak (#124): the storage step fails before any library
    ' file is read, and the window offers the previous save. Taking it says
    ' where the replaced file is kept and closes the window; the storage
    ' step runs once more, startup goes on, and the library opens.
    <TestMethod>
    Public Sub UsePreviousSave_ContinuesStartupWithALibraryThatOpens()

        Dim repository As ManuscriptRepository =
            LibraryWithADamagedPrimary(
                "The previous save"
            )

        File.WriteAllText(
            StorageMigrationService.SchemaFilePath(),
            String.Empty
        )

        Dim attempts As Integer = 0
        Dim messages As New List(Of String)()
        Dim prepared As Boolean = False

        RunOnStaThread(
            Sub()

                prepared =
                    ManuscriptPipeline.Program.PrepareStorage(
                        Sub()
                            attempts += 1
                            StorageMigrationService.EnsureCurrentStorage()
                        End Sub,
                        Function(failure) ClickRecovery(failure, PreviousSaveAction, messages)
                    )

            End Sub
        )

        Assert.IsTrue(
            prepared,
            "Startup goes on."
        )

        Assert.AreEqual(
            2,
            attempts,
            "The storage step ran once more after the recovery."
        )

        Dim kept As String =
            Directory.GetFiles(
                Path.Combine(
                    Path.GetDirectoryName(repository.DataFilePath),
                    "recovery"
                )
            ).Single()

        Assert.AreEqual(
            "{ damaged",
            File.ReadAllText(kept),
            "The damaged primary is kept."
        )

        StringAssert.Contains(
            messages.Single(),
            kept,
            "The window says where the replaced file is kept."
        )

        Assert.AreEqual(
            StorageMigrationService.CurrentSchemaVersion,
            StorageMigrationService.ReadSchemaVersion()
        )

        Dim reopened As New ManuscriptRepository()

        Assert.AreEqual(
            "The previous save",
            reopened.Load().Single().Title,
            "The library opens."
        )

        Assert.IsFalse(
            reopened.LastLoadRecoveredFromBackup,
            "As the primary, with no fallback needed."
        )

    End Sub


    ' Neither library file reads, so only a backup is offered (#124). The
    ' archive chosen in the window is restored with no library loaded; the
    ' window says so, names the copy of the files it replaced, and closes,
    ' and the library opens.
    <TestMethod>
    Public Sub RestoreFromBackup_EndsWithALibraryThatOpens()

        Dim backupPath As String =
            CreateBackupOfThreeManuscripts()

        Dim repository As ManuscriptRepository =
            LibraryWithADamagedPrimary(
                "Lost to both files"
            )

        File.WriteAllText(
            repository.BackupFilePath,
            "[null]"
        )

        Dim failure As Exception =
            Nothing

        Try
            repository.Load()
        Catch ex As Exception
            failure = ex
        End Try

        Assert.IsNotNull(
            failure,
            "Neither file reads, so the library does not load."
        )

        Dim messages As New List(Of String)()
        Dim recovered As Boolean = False

        RunOnStaThread(
            Sub()

                recovered =
                    ClickRecovery(
                        StartupFailure.ForManuscriptLibrary(failure, repository.DataFilePath),
                        RestoreAction,
                        messages,
                        backupPath
                    )

            End Sub
        )

        Assert.IsTrue(
            recovered
        )

        Dim emergencyCopy As String =
            Directory.GetFiles(
                Path.Combine(
                    Path.GetDirectoryName(repository.DataFilePath),
                    "backups"
                ),
                "PaperRoute_PreRestore_*.zip"
            ).Single()

        StringAssert.Contains(
            messages.Single(),
            "restored 3 manuscript(s)"
        )

        StringAssert.Contains(
            messages.Single(),
            emergencyCopy,
            "The window names the copy of the files it replaced."
        )

        StorageMigrationService.EnsureCurrentStorage()

        Assert.AreEqual(
            3,
            New ManuscriptRepository().Load().Count,
            "The library opens."
        )

    End Sub


    ' A recovery that fails shows the error, leaves the window open with
    ' nothing recovered, and changes nothing (#124).
    <TestMethod>
    Public Sub ARecoveryThatFails_SaysWhyAndChangesNothing()

        Dim repository As ManuscriptRepository =
            LibraryWithADamagedPrimary(
                "Lost to both files"
            )

        File.WriteAllText(
            repository.BackupFilePath,
            "[null]"
        )

        Dim notABackup As String =
            Path.Combine(
                _root,
                "not-a-backup.zip"
            )

        File.WriteAllText(
            notABackup,
            "This is not an archive."
        )

        Dim before As Dictionary(Of String, Byte()) =
            SnapshotData()

        Dim messages As New List(Of String)()
        Dim recovered As Boolean = True

        RunOnStaThread(
            Sub()

                recovered =
                    ClickRecovery(
                        StartupFailure.ForManuscriptLibrary(New InvalidDataException("The test's failure."), repository.DataFilePath),
                        RestoreAction,
                        messages,
                        notABackup
                    )

            End Sub
        )

        Assert.IsFalse(
            recovered,
            "Nothing was recovered, and the window stays open."
        )

        AssertSameData(
            before,
            "A failed restore changes nothing."
        )

        Dim expected As Exception =
            Nothing

        Try
            Call New StartupRecoveryService().RestoreFromBackup(notABackup)
        Catch ex As Exception
            expected = ex
        End Try

        Assert.IsNotNull(
            expected
        )

        Assert.AreEqual(
            expected.Message,
            messages.Single(),
            "The window shows the error's message."
        )

    End Sub


    ' A window closed without recovering ends startup, and the storage
    ' step is not run again (#124).
    <TestMethod>
    Public Sub AStorageWindowClosedWithoutRecovering_EndsStartup()

        Dim attempts As Integer = 0
        Dim shown As New List(Of StartupFailure)()

        Dim prepared As Boolean =
            ManuscriptPipeline.Program.PrepareStorage(
                Sub()
                    attempts += 1
                    Throw New InvalidDataException("The test's failure.")
                End Sub,
                Function(failure)
                    shown.Add(failure)
                    Return False
                End Function
            )

        Assert.IsFalse(
            prepared
        )

        Assert.AreEqual(
            1,
            attempts
        )

        Assert.AreEqual(
            "The test's failure.",
            shown.Single().Cause.Message
        )

    End Sub


    ' From the main window (#124): a start the window recovered hands over
    ' to a new session by a restart, as a restore does (#116), so this
    ' window neither goes on with the library it could not load nor opens
    ' a second window on the recovered one; a start it did not recover
    ' closes as before.
    <TestMethod>
    <DataRow(True)>
    <DataRow(False)>
    Public Sub AFailedLoad_RestartsWhenTheWindowRecoveredAndClosesWhenItDidNot(recovers As Boolean)

        Dim repository As ManuscriptRepository =
            LibraryWithADamagedPrimary(
                "Lost to both files"
            )

        File.WriteAllText(
            repository.BackupFilePath,
            "[null]"
        )

        RunOnStaThread(
            Sub()

                Using board As New FailedStart(recovers)

                    Assert.IsFalse(
                        CBool(board.CallPrivate("LoadManuscripts")),
                        "The library does not load."
                    )

                    Assert.IsTrue(
                        board.Failures.Single().OffersRestore,
                        "The window offers a backup."
                    )

                    board.CallPrivate("EndFailedStartup")

                    Assert.AreEqual(
                        recovers,
                        board.Restarted,
                        "A recovered start restarts."
                    )

                    Assert.AreEqual(
                        recovers,
                        CBool(board.FieldValue("handingOverAfterRestore")),
                        "The close a restart sends neither asks nor saves."
                    )

                    Assert.AreEqual(
                        Not recovers,
                        board.IsDisposed,
                        "Only a start that was not recovered closes the window itself."
                    )

                End Using

            End Sub
        )

    End Sub


    Private Const PreviousSaveAction As String =
        "Use the Previous Save"

    Private Const RestoreAction As String =
        "Restore From Backup..."


    ' The library under the test root, saved twice so manuscripts.bak holds
    ' it as well, then its primary damaged.
    Private Shared Function LibraryWithADamagedPrimary(
        title As String
    ) As ManuscriptRepository

        Dim repository As New ManuscriptRepository()

        Dim library As New List(Of Manuscript) From {
            New Manuscript With {.Title = title}
        }

        repository.Save(library)
        repository.Save(library)

        Assert.IsTrue(
            File.Exists(repository.BackupFilePath)
        )

        File.WriteAllText(
            repository.DataFilePath,
            "{ damaged"
        )

        Return repository

    End Function


    Private Function CreateBackupOfThreeManuscripts() As String

        Dim sourceData As String = Path.Combine(_root, "source-data")
        Dim sourceLibrary As String = Path.Combine(_root, "source-library")
        Dim sourceRepository As New ManuscriptRepository(sourceData, sourceLibrary)
        Dim sourceManuscripts As List(Of Manuscript) = CreateRepresentativeLibrary()
        sourceRepository.Save(sourceManuscripts)

        Dim backupPath As String = Path.Combine(_root, "backup.zip")
        Call New PortableBackupService(sourceLibrary).CreateBackup(backupPath, sourceManuscripts, sourceRepository)

        Return backupPath

    End Function


    ' The recovery actions a window offers for the failure. The window is
    ' built, not shown.
    Private Shared Function RecoveryActions(
        failure As StartupFailure
    ) As List(Of String)

        Dim actions As New List(Of String)()

        RunOnStaThread(
            Sub()

                Using window As New StartupFailureForm(failure)

                    actions.AddRange(
                        Descendants(window).
                            OfType(Of Button)().
                            Select(Function(button) button.Text).
                            Where(Function(text) text = PreviousSaveAction OrElse text = RestoreAction)
                    )

                End Using

            End Sub
        )

        Return actions

    End Function


    ' Shows the window off-screen, clicks the action, and returns whether
    ' the window recovered the library. The backup chosen and the messages
    ' shown are the test's; the window is closed either way.
    Private Shared Function ClickRecovery(
        failure As StartupFailure,
        action As String,
        messages As List(Of String),
        Optional backupPath As String = Nothing
    ) As Boolean

        Using window As New OffScreenStartupWindow(failure)

            window.messagePrompt =
                Sub(text, caption, icon) messages.Add(text)

            window.backupFilePrompt =
                Function() backupPath

            window.Show()
            Application.DoEvents()

            Descendants(window).
                OfType(Of Button)().
                Single(Function(button) button.Text = action).
                PerformClick()

            Application.DoEvents()

            Dim recovered As Boolean =
                window.Recovered

            Assert.AreEqual(
                recovered,
                window.IsDisposed OrElse Not window.Visible,
                "The window closes itself exactly when it recovered the library."
            )

            If Not window.IsDisposed Then
                window.Close()
            End If

            Return recovered

        End Using

    End Function


    Private Function SnapshotData() As Dictionary(Of String, Byte())

        Dim files As New Dictionary(Of String, Byte())(StringComparer.OrdinalIgnoreCase)

        For Each filePath As String In Directory.GetFiles(_root, "*", SearchOption.AllDirectories)
            files(filePath) = File.ReadAllBytes(filePath)
        Next

        Return files

    End Function


    Private Sub AssertSameData(
        expected As Dictionary(Of String, Byte()),
        message As String
    )

        Dim actual As Dictionary(Of String, Byte()) = SnapshotData()

        CollectionAssert.AreEquivalent(expected.Keys.ToList(), actual.Keys.ToList(), message)

        For Each pair As KeyValuePair(Of String, Byte()) In expected
            CollectionAssert.AreEqual(pair.Value, actual(pair.Key), message & " (" & Path.GetFileName(pair.Key) & ")")
        Next

    End Sub


    Private NotInheritable Class OffScreenStartupWindow
        Inherits StartupFailureForm

        Public Sub New(failure As StartupFailure)
            MyBase.New(failure)
            StartPosition = FormStartPosition.Manual
            Location = New Point(-20000, -20000)
            ShowInTaskbar = False
        End Sub

        Protected Overrides ReadOnly Property ShowWithoutActivation As Boolean
            Get
                Return True
            End Get
        End Property
    End Class


    ' The main window on the test root's library, never shown, with the
    ' startup window's answer and the restart recorded.
    Private NotInheritable Class FailedStart
        Inherits Form1

        Private ReadOnly _recovers As Boolean

        Public ReadOnly Failures As New List(Of StartupFailure)()
        Public Restarted As Boolean

        Public Sub New(recovers As Boolean)
            _recovers = recovers
        End Sub

        Protected Overrides Sub OnLoad(e As EventArgs)
        End Sub

        Protected Overrides Function ShowStartupFailure(failure As StartupFailure) As Boolean
            Failures.Add(failure)
            Return _recovers
        End Function

        Protected Overrides Sub RestartOnTheLibraryOnDisk()
            Restarted = True
        End Sub

        Public Function FieldValue(name As String) As Object
            Return GetType(Form1).GetField(name, BindingFlags.Instance Or BindingFlags.NonPublic).GetValue(Me)
        End Function

        Public Function CallPrivate(name As String, ParamArray args As Object()) As Object
            Return GetType(Form1).GetMethod(name, BindingFlags.Instance Or BindingFlags.NonPublic).Invoke(Me, args)
        End Function
    End Class


    ' The file that failed is on the window, Copy Details carries it with
    ' the Diagnostics report (built without a loaded library), and the four
    ' actions are there: Open Data Folder, Copy Details, the guide's
    ' Recovering your library section, and Close.
    Private Shared Sub AssertWindow(
        failure As StartupFailure,
        expectedPath As String
    )

        RunOnSta(
            Sub()

                Using window As New StartupFailureForm(
                    failure
                )

                    Dim controls As List(Of Control) =
                        Descendants(
                            window
                        ).ToList()

                    StringAssert.Contains(
                        window.DetailsText,
                        expectedPath,
                        "The window names the file that failed."
                    )

                    StringAssert.Contains(
                        window.DetailsText,
                        Path.GetDirectoryName(expectedPath),
                        "The window names its folder."
                    )

                    StringAssert.Contains(
                        window.DetailsText,
                        failure.Cause.Message,
                        "The window says what went wrong."
                    )

                    For Each action As String In {"Open Data Folder", "Copy Details", "Close"}

                        Assert.IsTrue(
                            controls.OfType(Of Button)().Any(
                                Function(button) button.Text = action
                            ),
                            "The window offers " & action & "."
                        )

                    Next

                    Assert.IsTrue(
                        controls.OfType(Of LinkLabel)().Any(
                            Function(link) link.Text.Contains(
                                "Recovering your library",
                                StringComparison.Ordinal
                            )
                        ),
                        "The window links to the guide's Recovering your library section."
                    )

                    Dim copied As String =
                        window.CopyText()

                    StringAssert.Contains(
                        copied,
                        expectedPath,
                        "Copy Details carries the file that failed."
                    )

                    StringAssert.Contains(
                        copied,
                        "PaperRoute Diagnostics",
                        "Copy Details carries the Diagnostics report, built without a loaded library."
                    )

                    StringAssert.Contains(
                        copied,
                        failure.Cause.GetType().Name,
                        "Copy Details carries the error."
                    )

                End Using

            End Sub
        )

    End Sub


    Private Shared Iterator Function Descendants(
        parent As Control
    ) As IEnumerable(Of Control)

        For Each child As Control In parent.Controls

            Yield child

            For Each descendant As Control In Descendants(child)
                Yield descendant
            Next

        Next

    End Function


    Private Shared Sub RunOnSta(
        action As Action
    )

        Dim failure As ExceptionDispatchInfo =
            Nothing

        Dim thread As New Thread(
            Sub()
                Try
                    action()
                Catch ex As Exception
                    failure = ExceptionDispatchInfo.Capture(ex)
                End Try
            End Sub
        )

        thread.SetApartmentState(
            ApartmentState.STA
        )

        thread.Start()
        thread.Join()

        failure?.Throw()

    End Sub

End Class
