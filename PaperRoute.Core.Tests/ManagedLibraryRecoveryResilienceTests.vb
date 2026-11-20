Imports System
Imports System.Collections.Generic
Imports System.IO
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

<TestClass>
Public Class ManagedLibraryRecoveryResilienceTests

    Private _root As String = String.Empty


    <TestInitialize>
    Public Sub Initialize()

        _root =
            Path.Combine(
                Path.GetTempPath(),
                "PaperRoute-Recovery-" &
                Guid.NewGuid().ToString("N")
            )

        Directory.CreateDirectory(
            _root
        )

    End Sub


    <TestCleanup>
    Public Sub Cleanup()

        Try

            If Directory.Exists(
                _root
            ) Then

                Directory.Delete(
                    _root,
                    True
                )

            End If

        Catch
            ' Best-effort test cleanup only.
        End Try

    End Sub


    <TestMethod>
    Public Sub Load_AccessDeniedManagedStagingDoesNotBlockValidLibrary()

        AssertManagedRecoveryFailureIsNonfatal(
            New UnauthorizedAccessException(
                "Synthetic staging access denied."
            )
        )

    End Sub


    <TestMethod>
    Public Sub Load_MissingOrBrokenManagedStagingDoesNotBlockValidLibrary()

        AssertManagedRecoveryFailureIsNonfatal(
            New DirectoryNotFoundException(
                "Synthetic staged directory disappeared."
            )
        )

    End Sub


    Private Sub AssertManagedRecoveryFailureIsNonfatal(
        failure As Exception
    )

        Dim dataDirectory As String =
            Path.Combine(
                _root,
                "data"
            )

        Dim managedDirectory As String =
            Path.Combine(
                _root,
                "managed"
            )

        Dim seedRepository As New ManuscriptRepository(
            dataDirectory,
            managedDirectory
        )

        Dim manuscript As New Manuscript With {
            .Title = "Recovery resilience"
        }

        seedRepository.Save(
            New List(Of Manuscript) From {
                manuscript
            }
        )

        Dim faultingManagedLibrary As New FaultingManagedLibraryService(
            managedDirectory,
            failure
        )

        Dim repository As New ManuscriptRepository(
            dataDirectory,
            managedDirectory,
            faultingManagedLibrary
        )

        Dim loaded As List(Of Manuscript) =
            repository.Load()

        Assert.AreEqual(
            1,
            loaded.Count
        )

        Assert.AreEqual(
            "Recovery resilience",
            loaded(0).Title
        )

        Assert.IsFalse(
            String.IsNullOrWhiteSpace(
                repository.LastManagedLibraryRecoveryWarning
            )
        )

        StringAssert.Contains(
            repository.LastManagedLibraryRecoveryWarning,
            failure.Message
        )

    End Sub


    Private NotInheritable Class FaultingManagedLibraryService
        Inherits ManagedLibraryService

        Private ReadOnly _failure As Exception


        Public Sub New(
            rootDirectory As String,
            failure As Exception
        )

            MyBase.New(
                rootDirectory
            )

            _failure =
                failure

        End Sub


        Friend Overrides Sub RecoverStagedVersionDeletions(
            manuscripts As IEnumerable(Of Manuscript)
        )

            Throw _failure

        End Sub

    End Class

End Class
