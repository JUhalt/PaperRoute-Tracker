Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.Text.Json
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

<TestClass>
Public Class SubmissionReadinessValidationTests

    Private _root As String = String.Empty


    <TestInitialize>
    Public Sub Initialize()

        _root =
            CreateTemporaryRoot()

    End Sub


    <TestCleanup>
    Public Sub Cleanup()

        DeleteTemporaryRoot(
            _root
        )

    End Sub


    <TestMethod>
    Public Sub Repository_NullReadinessCollectionsNormalizeToEmpty()

        Dim dataDirectory As String =
            Path.Combine(
                _root,
                "data"
            )

        Directory.CreateDirectory(
            dataDirectory
        )

        Const json As String =
            "[{""Id"":""11111111-1111-1111-1111-111111111111"",""Title"":""Null collections"",""ReadinessProfiles"":null,""SubmissionPackets"":null,""History"":[],""Submissions"":[]}]"

        File.WriteAllText(
            Path.Combine(
                dataDirectory,
                "manuscripts.json"
            ),
            json
        )

        Dim repository As New ManuscriptRepository(
            dataDirectory,
            Path.Combine(
                _root,
                "managed"
            )
        )

        Dim loaded As List(Of Manuscript) =
            repository.Load()

        Assert.IsNotNull(
            loaded(0).ReadinessProfiles
        )

        Assert.IsNotNull(
            loaded(0).SubmissionPackets
        )

        Assert.AreEqual(
            0,
            loaded(0).ReadinessProfiles.Count
        )

        Assert.AreEqual(
            0,
            loaded(0).SubmissionPackets.Count
        )

    End Sub


    <TestMethod>
    Public Sub Repository_InvalidPacketVersionReferenceIsRejected()

        Dim manuscript As New Manuscript()

        manuscript.SubmissionPackets.Add(
            New SubmissionPacket With {
                .ManuscriptVersionId = Guid.NewGuid()
            }
        )

        AssertRepositoryRejects(
            manuscript
        )

    End Sub


    <TestMethod>
    Public Sub Repository_InvalidPacketSubmissionReferenceIsRejected()

        Dim manuscript As Manuscript =
            CreatePacketReadyManuscript()

        manuscript.SubmissionPackets(0).SubmissionId =
            Guid.NewGuid()

        AssertRepositoryRejects(
            manuscript
        )

    End Sub


    <TestMethod>
    Public Sub Repository_InvalidPacketReadinessReferenceIsRejected()

        Dim manuscript As Manuscript =
            CreatePacketReadyManuscript()

        manuscript.SubmissionPackets(0).ReadinessProfileId =
            Guid.NewGuid()

        AssertRepositoryRejects(
            manuscript
        )

    End Sub


    <TestMethod>
    Public Sub Repository_RevisionRoundWithoutSubmissionIsRejected()

        Dim manuscript As Manuscript =
            CreatePacketReadyManuscript()

        manuscript.SubmissionPackets(0).RevisionRoundNumber =
            1

        AssertRepositoryRejects(
            manuscript
        )

    End Sub


    <TestMethod>
    Public Sub Repository_InvalidSha256MetadataIsRejected()

        Dim manuscript As Manuscript =
            CreatePacketReadyManuscript()

        manuscript.SubmissionPackets(0).Files.Add(
            New SubmissionPacketFile With {
                .Role = SubmissionPacketFileRole.Manuscript,
                .Sha256 = "not-a-sha256"
            }
        )

        AssertRepositoryRejects(
            manuscript
        )

    End Sub


    <TestMethod>
    Public Sub Repository_NegativePacketFileSizeIsRejected()

        Dim manuscript As Manuscript =
            CreatePacketReadyManuscript()

        manuscript.SubmissionPackets(0).Files.Add(
            New SubmissionPacketFile With {
                .Role = SubmissionPacketFileRole.Manuscript,
                .FileSizeBytes = -1
            }
        )

        AssertRepositoryRejects(
            manuscript
        )

    End Sub


    <TestMethod>
    Public Sub AuthorLibrary_DuplicateChecklistTemplateIdentifiersAreRejected()

        Dim duplicateId As Guid =
            Guid.NewGuid()

        Dim journal As New JournalRecord With {
            .Name = "Duplicate template test"
        }

        journal.ReadinessChecklistTemplate.Add(
            New JournalChecklistTemplateItem With {
                .Id = duplicateId,
                .Title = "First"
            }
        )

        journal.ReadinessChecklistTemplate.Add(
            New JournalChecklistTemplateItem With {
                .Id = duplicateId,
                .Title = "Second"
            }
        )

        AssertAuthorLibraryRejects(
            journal
        )

    End Sub


    <TestMethod>
    Public Sub AuthorLibrary_NegativeChecklistSortOrderIsRejected()

        Dim journal As New JournalRecord With {
            .Name = "Negative sort test"
        }

        journal.ReadinessChecklistTemplate.Add(
            New JournalChecklistTemplateItem With {
                .Title = "Broken sort order",
                .SortOrder = -1
            }
        )

        AssertAuthorLibraryRejects(
            journal
        )

    End Sub


    Private Function CreatePacketReadyManuscript() As Manuscript

        Dim manuscript As New Manuscript()

        Dim version As New ManuscriptVersion With {
            .Label = "Packet version"
        }

        manuscript.Versions.Add(
            version
        )

        manuscript.SubmissionPackets.Add(
            New SubmissionPacket With {
                .ManuscriptVersionId = version.Id,
                .Label = "Packet"
            }
        )

        Return manuscript

    End Function


    Private Sub AssertRepositoryRejects(
        manuscript As Manuscript
    )

        Dim dataDirectory As String =
            Path.Combine(
                _root,
                Guid.NewGuid().ToString("N"),
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
            JsonSerializer.Serialize(
                New List(Of Manuscript) From {
                    manuscript
                },
                CreateJsonOptions()
            )
        )

        Dim repository As New ManuscriptRepository(
            dataDirectory,
            Path.Combine(
                _root,
                "managed-" &
                Guid.NewGuid().ToString("N")
            )
        )

        Assert.ThrowsExactly(Of InvalidDataException)(
            Sub()
                repository.Load()
            End Sub
        )

    End Sub


    Private Sub AssertAuthorLibraryRejects(
        journal As JournalRecord
    )

        Dim dataDirectory As String =
            Path.Combine(
                _root,
                "authors-" &
                Guid.NewGuid().ToString("N")
            )

        Directory.CreateDirectory(
            dataDirectory
        )

        Dim library As New AuthorLibraryData()

        library.Journals.Add(
            journal
        )

        File.WriteAllText(
            Path.Combine(
                dataDirectory,
                "authors.json"
            ),
            JsonSerializer.Serialize(
                library,
                CreateJsonOptions()
            )
        )

        Dim repository As New AuthorLibraryRepository(
            dataDirectory
        )

        Assert.ThrowsExactly(Of InvalidDataException)(
            Sub()
                repository.Load()
            End Sub
        )

    End Sub

End Class
