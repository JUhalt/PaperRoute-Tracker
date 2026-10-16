Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.Linq
Imports System.Runtime.ExceptionServices
Imports System.Threading
Imports System.Windows.Forms
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline.Forms
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
