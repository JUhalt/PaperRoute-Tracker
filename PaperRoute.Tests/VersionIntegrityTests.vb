Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.Text.Json
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

<TestClass>
Public Class VersionIntegrityTests

    Private _root As String = String.Empty
    Private _dataDirectory As String = String.Empty
    Private _managedLibrary As String = String.Empty


    <TestInitialize>
    Public Sub Initialize()

        _root =
            CreateTemporaryRoot()

        _dataDirectory =
            Path.Combine(
                _root,
                "data"
            )

        _managedLibrary =
            Path.Combine(
                _root,
                "managed"
            )

    End Sub


    <TestCleanup>
    Public Sub Cleanup()

        DeleteTemporaryRoot(
            _root
        )

    End Sub


    <TestMethod>
    Public Sub Load_NormalizesNullVersionCollectionForOlderManuscript()

        Dim manuscript As New Manuscript With {
            .Title = "Older manuscript",
            .Versions = Nothing,
            .CurrentVersionId = Nothing
        }

        Dim loaded As List(Of Manuscript) =
            WriteAndLoad(
                manuscript
            )

        Assert.IsNotNull(
            loaded(0).Versions
        )

        Assert.AreEqual(
            0,
            loaded(0).Versions.Count
        )

        Assert.IsFalse(
            loaded(0).CurrentVersionId.HasValue
        )

    End Sub


    <TestMethod>
    Public Sub Load_NormalizesNullVersionTextFields()

        Dim versionId As Guid =
            Guid.NewGuid()

        Dim manuscript As New Manuscript()

        manuscript.Versions.Add(
            New ManuscriptVersion With {
                .Id = versionId,
                .Label = Nothing,
                .Notes = Nothing,
                .LocalFilePath = Nothing
            }
        )

        manuscript.CurrentVersionId =
            versionId

        Dim loaded As List(Of Manuscript) =
            WriteAndLoad(
                manuscript
            )

        Assert.AreEqual(
            String.Empty,
            loaded(0).Versions(0).Label
        )

        Assert.AreEqual(
            String.Empty,
            loaded(0).Versions(0).Notes
        )

        Assert.AreEqual(
            String.Empty,
            loaded(0).Versions(0).LocalFilePath
        )

    End Sub


    <TestMethod>
    Public Sub Load_RejectsNullVersionRecord()

        Dim manuscript As New Manuscript()

        manuscript.Versions.Add(
            Nothing
        )

        Assert.ThrowsExactly(Of InvalidDataException)(
            Sub()
                WriteAndLoad(
                    manuscript
                )
            End Sub
        )

    End Sub


    <TestMethod>
    Public Sub Load_RejectsEmptyVersionIdentifier()

        Dim manuscript As New Manuscript()

        manuscript.Versions.Add(
            New ManuscriptVersion With {
                .Id = Guid.Empty
            }
        )

        Assert.ThrowsExactly(Of InvalidDataException)(
            Sub()
                WriteAndLoad(
                    manuscript
                )
            End Sub
        )

    End Sub


    <TestMethod>
    Public Sub Load_RejectsDuplicateVersionIdentifiers()

        Dim duplicateId As Guid =
            Guid.NewGuid()

        Dim manuscript As New Manuscript()

        manuscript.Versions.Add(
            New ManuscriptVersion With {
                .Id = duplicateId,
                .Label = "First"
            }
        )

        manuscript.Versions.Add(
            New ManuscriptVersion With {
                .Id = duplicateId,
                .Label = "Second"
            }
        )

        Assert.ThrowsExactly(Of InvalidDataException)(
            Sub()
                WriteAndLoad(
                    manuscript
                )
            End Sub
        )

    End Sub


    <TestMethod>
    Public Sub Load_RejectsCurrentVersionThatIsNotInHistory()

        Dim manuscript As New Manuscript()

        manuscript.Versions.Add(
            New ManuscriptVersion With {
                .Id = Guid.NewGuid()
            }
        )

        manuscript.CurrentVersionId =
            Guid.NewGuid()

        Assert.ThrowsExactly(Of InvalidDataException)(
            Sub()
                WriteAndLoad(
                    manuscript
                )
            End Sub
        )

    End Sub


    <TestMethod>
    Public Sub Load_RejectsZeroRevisionRoundNumber()

        Dim versionId As Guid =
            Guid.NewGuid()

        Dim manuscript As New Manuscript()

        manuscript.Versions.Add(
            New ManuscriptVersion With {
                .Id = versionId,
                .RevisionRoundNumber = 0
            }
        )

        manuscript.CurrentVersionId =
            versionId

        Assert.ThrowsExactly(Of InvalidDataException)(
            Sub()
                WriteAndLoad(
                    manuscript
                )
            End Sub
        )

    End Sub


    <TestMethod>
    Public Sub Load_RejectsEmptySubmissionReference()

        Dim versionId As Guid =
            Guid.NewGuid()

        Dim manuscript As New Manuscript()

        manuscript.Versions.Add(
            New ManuscriptVersion With {
                .Id = versionId,
                .SubmissionId = Guid.Empty
            }
        )

        manuscript.CurrentVersionId =
            versionId

        Assert.ThrowsExactly(Of InvalidDataException)(
            Sub()
                WriteAndLoad(
                    manuscript
                )
            End Sub
        )

    End Sub


    <TestMethod>
    Public Sub Load_RejectsEmptyDecisionReference()

        Dim versionId As Guid =
            Guid.NewGuid()

        Dim manuscript As New Manuscript()

        manuscript.Versions.Add(
            New ManuscriptVersion With {
                .Id = versionId,
                .DecisionId = Guid.Empty
            }
        )

        manuscript.CurrentVersionId =
            versionId

        Assert.ThrowsExactly(Of InvalidDataException)(
            Sub()
                WriteAndLoad(
                    manuscript
                )
            End Sub
        )

    End Sub


    <TestMethod>
    Public Sub Load_AllowsUnresolvedHistoricalWorkflowReferences()

        Dim versionId As Guid =
            Guid.NewGuid()

        Dim unresolvedSubmissionId As Guid =
            Guid.NewGuid()

        Dim unresolvedDecisionId As Guid =
            Guid.NewGuid()

        Dim manuscript As New Manuscript()

        manuscript.Versions.Add(
            New ManuscriptVersion With {
                .Id = versionId,
                .Label = "Imported historical revision",
                .SubmissionId = unresolvedSubmissionId,
                .DecisionId = unresolvedDecisionId,
                .RevisionRoundNumber = 1
            }
        )

        manuscript.CurrentVersionId =
            versionId

        Dim loaded As List(Of Manuscript) =
            WriteAndLoad(
                manuscript
            )

        Assert.AreEqual(
            unresolvedSubmissionId,
            loaded(0).Versions(0).SubmissionId.Value
        )

        Assert.AreEqual(
            unresolvedDecisionId,
            loaded(0).Versions(0).DecisionId.Value
        )

    End Sub


    Private Function WriteAndLoad(
        manuscript As Manuscript
    ) As List(Of Manuscript)

        Directory.CreateDirectory(
            _dataDirectory
        )

        Dim dataFilePath As String =
            Path.Combine(
                _dataDirectory,
                "manuscripts.json"
            )

        File.WriteAllText(
            dataFilePath,
            JsonSerializer.Serialize(
                New List(Of Manuscript) From {
                    manuscript
                },
                CreateJsonOptions()
            )
        )

        Dim repository As New ManuscriptRepository(
            _dataDirectory,
            _managedLibrary
        )

        Return repository.Load()

    End Function

End Class
