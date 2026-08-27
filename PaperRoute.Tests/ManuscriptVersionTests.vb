Imports System
Imports System.Collections.Generic
Imports System.IO
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

<TestClass>
Public Class ManuscriptVersionTests

    Private _root As String = String.Empty
    Private _dataDirectory As String = String.Empty
    Private _managedLibrary As String = String.Empty


    <TestInitialize>
    Public Sub Initialize()

        _root = CreateTemporaryRoot()

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
    Public Sub SaveAndLoad_RoundTripsVersionHistoryAndWorkflowLinks()

        Dim repository As New ManuscriptRepository(
            _dataDirectory,
            _managedLibrary
        )

        Dim manuscript As Manuscript =
            CreateRepresentativeLibrary()(0)

        Dim submissionId As Guid =
            manuscript.Submissions(0).Id

        Dim decisionId As Guid =
            manuscript.Submissions(0).Decisions(0).Id

        Dim submittedVersionId As Guid =
            Guid.NewGuid()

        Dim revisedVersionId As Guid =
            Guid.NewGuid()

        manuscript.Versions.Add(
            New ManuscriptVersion With {
                .Id = submittedVersionId,
                .CreatedDate = New DateTime(2026, 7, 1, 9, 30, 0),
                .Label = "Submitted manuscript",
                .Notes = "Version sent with the original submission.",
                .LocalFilePath = "C:\Research\Paper\submitted.docx",
                .IsManagedCopy = False,
                .SubmissionId = submissionId
            }
        )

        manuscript.Versions.Add(
            New ManuscriptVersion With {
                .Id = revisedVersionId,
                .CreatedDate = New DateTime(2026, 8, 15, 14, 0, 0),
                .Label = "Revision 1",
                .Notes = "Revision prepared after the major-revision decision.",
                .LocalFilePath = "C:\Research\Paper\revision-1.docx",
                .IsManagedCopy = False,
                .SubmissionId = submissionId,
                .DecisionId = decisionId,
                .RevisionRoundNumber = 1
            }
        )

        manuscript.CurrentVersionId =
            revisedVersionId

        repository.Save(
            New List(Of Manuscript) From {
                manuscript
            }
        )

        Dim loaded As List(Of Manuscript) =
            repository.Load()

        Assert.AreEqual(
            1,
            loaded.Count
        )

        Assert.AreEqual(
            2,
            loaded(0).Versions.Count
        )

        Assert.AreEqual(
            revisedVersionId,
            loaded(0).CurrentVersionId.Value
        )

        Assert.AreEqual(
            submittedVersionId,
            loaded(0).Versions(0).Id
        )

        Assert.AreEqual(
            submissionId,
            loaded(0).Versions(0).SubmissionId.Value
        )

        Assert.AreEqual(
            decisionId,
            loaded(0).Versions(1).DecisionId.Value
        )

        Assert.AreEqual(
            1,
            loaded(0).Versions(1).RevisionRoundNumber.Value
        )

        Assert.AreEqual(
            "Revision 1",
            loaded(0).Versions(1).Label
        )

    End Sub


    <TestMethod>
    Public Sub Clone_PreservesAndDeepCopiesVersionHistory()

        Dim manuscript As Manuscript =
            CreateRepresentativeLibrary()(0)

        Dim versionId As Guid =
            Guid.NewGuid()

        Dim submissionId As Guid =
            manuscript.Submissions(0).Id

        Dim decisionId As Guid =
            manuscript.Submissions(0).Decisions(0).Id

        manuscript.Versions.Add(
            New ManuscriptVersion With {
                .Id = versionId,
                .CreatedDate = New DateTime(2026, 8, 15, 14, 0, 0),
                .Label = "Revision 1",
                .Notes = "Original version note.",
                .LocalFilePath = "C:\Research\Paper\revision-1.docx",
                .IsManagedCopy = False,
                .SubmissionId = submissionId,
                .DecisionId = decisionId,
                .RevisionRoundNumber = 1
            }
        )

        manuscript.CurrentVersionId =
            versionId

        Dim clone As Manuscript =
            ManuscriptCloneService.CloneManuscript(
                manuscript
            )

        Assert.AreEqual(
            versionId,
            clone.CurrentVersionId.Value
        )

        Assert.AreEqual(
            1,
            clone.Versions.Count
        )

        Assert.AreNotSame(
            manuscript.Versions(0),
            clone.Versions(0)
        )

        Assert.AreEqual(
            decisionId,
            clone.Versions(0).DecisionId.Value
        )

        clone.Versions(0).Label =
            "Clone-only label"

        clone.Versions(0).Notes =
            "Clone-only note."

        Assert.AreEqual(
            "Revision 1",
            manuscript.Versions(0).Label
        )

        Assert.AreEqual(
            "Original version note.",
            manuscript.Versions(0).Notes
        )

    End Sub

End Class
