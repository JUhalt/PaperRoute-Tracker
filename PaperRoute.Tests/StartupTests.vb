Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.Globalization
Imports System.IO
Imports System.Linq
Imports System.Reflection
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

        ' A library from a newer PaperRoute is not damage: updating
        ' PaperRoute is the fix, and a restore would replace the library.
        ' Before the change a backup was offered here.
        File.WriteAllText(
            StorageMigrationService.SchemaFilePath(),
            "{""SchemaVersion"":10}"
        )

        CollectionAssert.AreEquivalent(
            Array.Empty(Of String)(),
            RecoveryActions(StartupFailure.ForStorage(cause, StorageMigrationService.SchemaFilePath())),
            "Storage written by a newer PaperRoute."
        )

    End Sub


    ' Use the Previous Save over a manuscripts.json that reads would only
    ' roll the library back, setting the newest save aside as unreadable,
    ' and would not mend a failure elsewhere, here an empty schema.json. It
    ' is not offered, and asked for anyway it changes nothing (#124). Before
    ' the change it was offered here.
    <TestMethod>
    Public Sub AReadableLibrary_IsNotOfferedThePreviousSave()

        Dim repository As New ManuscriptRepository()

        repository.Save(
            New List(Of Manuscript) From {New Manuscript With {.Title = "The previous save"}}
        )

        repository.Save(
            New List(Of Manuscript) From {New Manuscript With {.Title = "The newest save"}}
        )

        Assert.IsTrue(
            repository.HasValidBackup(),
            "The previous save reads."
        )

        File.WriteAllText(
            StorageMigrationService.SchemaFilePath(),
            String.Empty
        )

        Dim before As Dictionary(Of String, Byte()) =
            SnapshotData()

        CollectionAssert.AreEquivalent(
            {RestoreAction},
            RecoveryActions(StartupFailure.ForStorage(New InvalidDataException("The test's failure."), StorageMigrationService.SchemaFilePath())),
            "Only a backup is offered beside a library file that reads."
        )

        Dim refused As InvalidOperationException =
            Assert.ThrowsExactly(Of InvalidOperationException)(
                Sub() Call New StartupRecoveryService().UsePreviousSave()
            )

        StringAssert.Contains(
            refused.Message,
            "can be read"
        )

        AssertSameData(
            before,
            "manuscripts.json and everything beside it are as they were."
        )

        Assert.AreEqual(
            "The newest save",
            New ManuscriptRepository().Load().Single().Title
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
    ' archive chosen in the window is counted and asked about first, then
    ' restored with no library loaded; the window says so, names the copy
    ' of the files it replaced, and closes, and the library opens.
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
        Dim questions As New List(Of String)()
        Dim recovered As Boolean = False

        RunOnStaThread(
            Sub()

                recovered =
                    ClickRecovery(
                        StartupFailure.ForManuscriptLibrary(failure, repository.DataFilePath),
                        RestoreAction,
                        messages,
                        backupPath,
                        questions
                    )

            End Sub
        )

        Assert.IsTrue(
            recovered
        )

        StringAssert.Contains(
            questions.Single(),
            "Manuscripts: 3",
            "The question says what the archive holds."
        )

        StringAssert.Contains(
            questions.Single(),
            "backups folder",
            "And that the files it replaces are zipped first."
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
    ' nothing recovered, and changes nothing (#124). An archive that is not
    ' a backup fails its check before anything is asked.
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
        Dim questions As New List(Of String)()
        Dim recovered As Boolean = True

        RunOnStaThread(
            Sub()

                recovered =
                    ClickRecovery(
                        StartupFailure.ForManuscriptLibrary(New InvalidDataException("The test's failure."), repository.DataFilePath),
                        RestoreAction,
                        messages,
                        notABackup,
                        questions
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

        Assert.AreEqual(
            0,
            questions.Count,
            "An archive that fails its check is not asked about."
        )

        Dim expected As Exception =
            Nothing

        Try
            Call New StartupRecoveryService().InspectBackup(notABackup)
        Catch ex As Exception
            expected = ex
        End Try

        Assert.IsNotNull(
            expected
        )

        StringAssert.StartsWith(
            messages.Single(),
            "This backup could not be validated. Nothing was changed."
        )

        StringAssert.Contains(
            messages.Single(),
            expected.Message,
            "The window shows the error's message."
        )

    End Sub


    ' Restore From Backup... says what the archive holds and asks first, as
    ' Settings > Restore Backup... does (#124); No changes nothing and
    ' leaves the window open. Before the change choosing the archive
    ' replaced the library at once.
    <TestMethod>
    Public Sub RestoreFromBackup_AsksFirstAndNoChangesNothing()

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

        Dim before As Dictionary(Of String, Byte()) =
            SnapshotData()

        Dim messages As New List(Of String)()
        Dim questions As New List(Of String)()
        Dim recovered As Boolean = True

        RunOnStaThread(
            Sub()

                recovered =
                    ClickRecovery(
                        StartupFailure.ForManuscriptLibrary(New InvalidDataException("The test's failure."), repository.DataFilePath),
                        RestoreAction,
                        messages,
                        backupPath,
                        questions,
                        confirms:=False
                    )

            End Sub
        )

        Assert.IsFalse(
            recovered,
            "No leaves the window open with nothing recovered."
        )

        StringAssert.Contains(
            questions.Single(),
            "Manuscripts: 3"
        )

        Assert.AreEqual(
            0,
            messages.Count,
            "There is nothing to report."
        )

        AssertSameData(
            before,
            "No changes nothing."
        )

    End Sub


    ' The window's question shows the backup's date and the PaperRoute that
    ' made it, as its backup.json records them, as Settings > Restore
    ' Backup... does (#115). Before the change it showed only the counts.
    <TestMethod>
    Public Sub RestoreFromBackup_AsksWithTheBackupsDateAndVersion()

        Dim backupPath As String =
            CreateBackupOfThreeManuscripts()

        SetBackupManifest(
            backupPath,
            BackupManifestJson(
                StorageMigrationService.CurrentSchemaVersion,
                createdUtc:="2026-10-12T15:30:00Z",
                appVersion:="0.9.2"
            )
        )

        Dim repository As ManuscriptRepository =
            LibraryWithADamagedPrimary(
                "Lost to both files"
            )

        File.WriteAllText(
            repository.BackupFilePath,
            "[null]"
        )

        Dim questions As New List(Of String)()

        RunOnStaThread(
            Sub()

                ClickRecovery(
                    StartupFailure.ForManuscriptLibrary(New InvalidDataException("The test's failure."), repository.DataFilePath),
                    RestoreAction,
                    New List(Of String)(),
                    backupPath,
                    questions,
                    confirms:=False
                )

            End Sub
        )

        Dim created As String =
            New DateTime(2026, 10, 12, 15, 30, 0, DateTimeKind.Utc).
                ToLocalTime().
                ToString("MMM d, yyyy h:mm tt", CultureInfo.CurrentCulture)

        StringAssert.Contains(
            questions.Single(),
            "Created: " & created
        )

        StringAssert.Contains(
            questions.Single(),
            "PaperRoute version: 0.9.2"
        )

        StringAssert.Contains(
            questions.Single(),
            "Manuscripts: 3"
        )

    End Sub


    ' A backup from a newer storage schema fails the window's check, before
    ' anything is asked or changed, with the Update PaperRoute message
    ' (#115). Before the change the window asked, and Yes replaced the
    ' library with it.
    <TestMethod>
    Public Sub ANewerBackup_IsRefusedBeforeAnythingIsAskedOrChanged()

        Dim backupPath As String =
            CreateBackupOfThreeManuscripts()

        SetBackupManifest(
            backupPath,
            BackupManifestJson(
                StorageMigrationService.CurrentSchemaVersion + 1
            )
        )

        Dim repository As ManuscriptRepository =
            LibraryWithADamagedPrimary(
                "Lost to both files"
            )

        File.WriteAllText(
            repository.BackupFilePath,
            "[null]"
        )

        Dim before As Dictionary(Of String, Byte()) =
            SnapshotTree(_root)

        Dim messages As New List(Of String)()
        Dim questions As New List(Of String)()
        Dim recovered As Boolean = True

        RunOnStaThread(
            Sub()

                recovered =
                    ClickRecovery(
                        StartupFailure.ForManuscriptLibrary(New InvalidDataException("The test's failure."), repository.DataFilePath),
                        RestoreAction,
                        messages,
                        backupPath,
                        questions
                    )

            End Sub
        )

        Assert.IsFalse(
            recovered,
            "Nothing was recovered, and the window stays open."
        )

        Assert.AreEqual(
            0,
            questions.Count,
            "A newer backup is not asked about."
        )

        StringAssert.StartsWith(
            messages.Single(),
            "This backup could not be validated. Nothing was changed."
        )

        StringAssert.Contains(
            messages.Single(),
            "Update PaperRoute"
        )

        AssertSameTree(
            before,
            _root,
            "A newer backup changes nothing"
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


    ' An authors.json another program holds is not damage (#111), as for
    ' the manuscript library: the window offers nothing to put right.
    ' Before the change it was shown as a damaged author library, with
    ' Restore From Backup....
    <TestMethod>
    Public Sub AnAuthorLibraryInUse_OffersNothingToPutRight()

        Dim authorsPath As String =
            New AuthorLibraryRepository().DataFilePath

        Directory.CreateDirectory(
            Path.GetDirectoryName(authorsPath)
        )

        File.WriteAllText(
            authorsPath,
            "{""Authors"":[]}"
        )

        Dim opener As New HeldFileOpener(
            authorsPath,
            failures:=Integer.MaxValue
        )

        RunOnStaThread(
            Sub()

                Using board As New FailedStart(recovers:=True)

                    board.SetField(
                        "authorRepository",
                        New AuthorLibraryRepository(
                            Path.GetDirectoryName(authorsPath),
                            openFile:=AddressOf opener.Open
                        )
                    )

                    Assert.IsFalse(
                        CBool(board.CallPrivate("LoadAuthorLibrary")),
                        "The author library does not load."
                    )

                    Dim failure As StartupFailure =
                        board.Failures.Single()

                    StringAssert.Contains(
                        failure.Heading,
                        "another program"
                    )

                    Assert.AreEqual(
                        authorsPath,
                        failure.FilePath
                    )

                    Assert.IsFalse(
                        failure.OffersRestore OrElse failure.OffersPreviousSave,
                        "Nothing is offered for a file in use."
                    )

                    Assert.IsFalse(
                        CBool(board.FieldValue("startupRecovered"))
                    )

                End Using

            End Sub
        )

    End Sub


    ' The author library is read again mid-session: after every page save,
    ' on leaving a hosted editor, after Add Manuscript. A restore there would
    ' replace the library under a session that goes on with the old one in
    ' memory, so once startup is over its failure offers nothing, and
    ' nothing acts on it (#124). While starting, a backup is still offered.
    ' Before the change Restore From Backup... was offered mid-session too.
    <TestMethod>
    Public Sub AnAuthorLibraryFailureAfterStartup_OffersNoRecovery()

        Dim authorsPath As String =
            New AuthorLibraryRepository().DataFilePath

        Directory.CreateDirectory(
            Path.GetDirectoryName(authorsPath)
        )

        File.WriteAllText(
            authorsPath,
            "{""Authors"":"
        )

        RunOnStaThread(
            Sub()

                Using starting As New FailedStart(recovers:=True), started As New FailedStart(recovers:=True)

                    Assert.IsFalse(
                        CBool(starting.CallPrivate("LoadAuthorLibrary"))
                    )

                    Assert.IsTrue(
                        starting.Failures.Single().OffersRestore,
                        "While starting, a backup is offered."
                    )

                    Assert.IsTrue(
                        CBool(starting.FieldValue("startupRecovered")),
                        "And a recovery there ends startup with a restart."
                    )

                    started.SetField(
                        "startupComplete",
                        True
                    )

                    Assert.IsFalse(
                        CBool(started.CallPrivate("LoadAuthorLibrary"))
                    )

                    Dim failure As StartupFailure =
                        started.Failures.Single()

                    Assert.IsFalse(
                        failure.OffersRestore OrElse failure.OffersPreviousSave,
                        "Mid-session the window offers nothing to put right."
                    )

                    Assert.IsFalse(
                        CBool(started.FieldValue("startupRecovered")),
                        "Nothing acts on the window's answer."
                    )

                    Assert.IsFalse(
                        CBool(started.FieldValue("handingOverAfterRestore"))
                    )

                    Assert.IsFalse(
                        started.Restarted
                    )

                End Using

            End Sub
        )

    End Sub


    ' The window must open whatever failed (#119). Building its recovery
    ' fails where the Documents folder cannot be found, which may be why
    ' startup failed: the window then opens with neither recovery action.
    ' Before the change that failure escaped the constructor, and no window
    ' opened.
    <TestMethod>
    Public Sub ARecoveryThatCannotBeBuilt_StillOpensTheWindow()

        Dim buttons As New List(Of String)()

        Dim cannotBeBuilt As Func(Of StartupRecoveryService) =
            Function() As StartupRecoveryService
                Throw New InvalidOperationException("The user's Documents folder could not be located.")
            End Function

        RunOnStaThread(
            Sub()

                Using window As New StartupFailureForm(
                    StartupFailure.ForStorage(New InvalidDataException("The test's failure."), StorageMigrationService.SchemaFilePath()),
                    cannotBeBuilt
                )

                    buttons.AddRange(
                        Descendants(window).
                            OfType(Of Button)().
                            Select(Function(button) button.Text)
                    )

                End Using

            End Sub
        )

        CollectionAssert.Contains(buttons, "Close")
        CollectionAssert.Contains(buttons, "Copy Details")
        CollectionAssert.DoesNotContain(buttons, PreviousSaveAction)
        CollectionAssert.DoesNotContain(buttons, RestoreAction)

    End Sub


    ' Guard: the harnesses skip Form1_Load, so its wiring is checked in the
    ' source, as the recovery draft's offer is. Both load failures post
    ' EndFailedStartup and neither closes the window itself; that half
    ' passed before this change and fails only if either goes back to
    ' Close(). Startup is marked complete once both libraries have loaded,
    ' before anything else reads them, so a later failure offers nothing.
    <TestMethod>
    Public Sub Form1Load_EndsAFailedLoadThroughEndFailedStartupAndThenMarksStartupComplete()

        Dim source As String =
            File.ReadAllText(
                Path.Combine(
                    AssistantCoreTests.RepositoryRoot(),
                    "ManuscriptPipeline",
                    "Form1.vb"
                )
            )

        Dim loading As Integer =
            source.IndexOf("Private Sub Form1_Load(", StringComparison.Ordinal)

        Dim rendering As Integer =
            source.IndexOf("RenderManuscripts()", Math.Max(loading, 0), StringComparison.Ordinal)

        Assert.IsTrue(
            loading >= 0 AndAlso rendering > loading,
            "Form1_Load renders the board."
        )

        Dim startup As String =
            source.Substring(
                loading,
                rendering - loading
            )

        Dim posts As New List(Of Integer)()
        Dim found As Integer = startup.IndexOf("EndFailedStartup()", StringComparison.Ordinal)

        While found >= 0
            posts.Add(found)
            found = startup.IndexOf("EndFailedStartup()", found + 1, StringComparison.Ordinal)
        End While

        Assert.AreEqual(
            2,
            posts.Count,
            "Both load failures end through EndFailedStartup."
        )

        Assert.IsFalse(
            startup.Contains("Close()", StringComparison.Ordinal),
            "Neither closes the window itself."
        )

        Assert.IsTrue(
            startup.IndexOf("startupComplete = True", StringComparison.Ordinal) > posts.Last(),
            "Startup is marked complete after both loads."
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
    ' the window recovered the library. The backup chosen, the answer to
    ' the restore's question, and the messages shown are the test's; the
    ' questions asked are kept when the test passes a list. The window is
    ' closed either way.
    Private Shared Function ClickRecovery(
        failure As StartupFailure,
        action As String,
        messages As List(Of String),
        Optional backupPath As String = Nothing,
        Optional questions As List(Of String) = Nothing,
        Optional confirms As Boolean = True
    ) As Boolean

        Using window As New OffScreenStartupWindow(failure)

            window.messagePrompt =
                Sub(text, caption, icon) messages.Add(text)

            window.backupFilePrompt =
                Function() backupPath

            window.restoreConfirmPrompt =
                Function(question)
                    questions?.Add(question)
                    Return confirms
                End Function

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

        Public Sub SetField(name As String, value As Object)
            GetType(Form1).GetField(name, BindingFlags.Instance Or BindingFlags.NonPublic).SetValue(Me, value)
        End Sub

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

        RunOnStaThread(
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

End Class
