Imports System
Imports System.Collections.Generic
Imports System.IO
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

<TestClass>
Public Class JournalMetadataTests

    <TestMethod>
    Public Sub AuthorLibrary_RoundTripsReusableJournal()

        Dim root As String =
            CreateTempDirectory()

        Try

            Dim repository As New AuthorLibraryRepository(
                root
            )

            Dim library As New AuthorLibraryData()

            library.Journals.Add(
                New JournalRecord With {
                    .Name = "Journal of Examples",
                    .Publisher = "Example Press",
                    .HomepageUrl = "https://example.org/journal",
                    .SubmissionPortalUrl = "https://submit.example.org/",
                    .IsFavorite = True,
                    .IsShortlisted = True
                }
            )

            repository.Save(
                library
            )

            Dim loaded As AuthorLibraryData =
                repository.Load()

            Assert.AreEqual(
                1,
                loaded.Journals.Count
            )

            Assert.AreEqual(
                "Journal of Examples",
                loaded.Journals(0).Name
            )

            Assert.AreEqual(
                "https://submit.example.org/",
                loaded.Journals(0).SubmissionPortalUrl
            )

            Assert.IsTrue(
                loaded.Journals(0).IsFavorite
            )

            ' The Watch list mark keeps its stored name (#96).
            Assert.IsTrue(
                loaded.Journals(0).IsShortlisted
            )

            Assert.AreEqual(
                "★ [Watch list] Journal of Examples",
                loaded.Journals(0).DisplayName
            )

        Finally

            DeleteTempDirectory(
                root
            )

        End Try

    End Sub


    <TestMethod>
    Public Sub AuthorLibrary_DuplicateJournalIds_AreRejected()

        Dim root As String =
            CreateTempDirectory()

        Try

            Dim id As Guid =
                Guid.NewGuid()

            Dim library As New AuthorLibraryData()

            library.Journals.Add(
                New JournalRecord With {
                    .Id = id,
                    .Name = "First"
                }
            )

            library.Journals.Add(
                New JournalRecord With {
                    .Id = id,
                    .Name = "Second"
                }
            )

            Dim repository As New AuthorLibraryRepository(
                root
            )

            Assert.ThrowsExactly(Of InvalidDataException)(
                Sub()
                    repository.Save(
                        library
                    )
                End Sub
            )

        Finally

            DeleteTempDirectory(
                root
            )

        End Try

    End Sub


    <TestMethod>
    Public Sub UrlSafety_AcceptsHttpAndHttps()

        Assert.IsTrue(
            UrlSafetyService.IsSafeHttpUrl(
                "https://example.org/path"
            )
        )

        Assert.IsTrue(
            UrlSafetyService.IsSafeHttpUrl(
                "http://example.org/path"
            )
        )

    End Sub


    <TestMethod>
    Public Sub UrlSafety_RejectsNonWebSchemes()

        Assert.IsFalse(
            UrlSafetyService.IsSafeHttpUrl(
                "javascript:alert(1)"
            )
        )

        Assert.IsFalse(
            UrlSafetyService.IsSafeHttpUrl(
                "file:///C:/secret.txt"
            )
        )

    End Sub


    <TestMethod>
    Public Sub UrlSafety_BlankOptionalUrl_NormalizesToBlank()

        Assert.AreEqual(
            String.Empty,
            UrlSafetyService.NormalizeOptionalHttpUrl(
                "   ",
                "Test URL"
            )
        )

    End Sub


    <TestMethod>
    Public Sub CloneManuscript_PreservesJournalAndRelatedLinks()

        Dim journalId As Guid =
            Guid.NewGuid()

        Dim source As New Manuscript With {
            .Title = "Linked Paper",
            .TargetJournal = "Journal",
            .TargetJournalId = journalId,
            .ManuscriptUrl = "https://submit.example.org/manuscript/123"
        }

        source.RelatedLinks.Add(
            New ManuscriptExternalLink With {
                .Label = "OSF Project",
                .Url = "https://osf.io/example/",
                .Notes = "Project materials"
            }
        )

        Dim clone As Manuscript =
            ManuscriptCloneService.CloneManuscript(
                source
            )

        Assert.AreEqual(
            journalId,
            clone.TargetJournalId.Value
        )

        Assert.AreEqual(
            "https://submit.example.org/manuscript/123",
            clone.ManuscriptUrl
        )

        Assert.AreEqual(
            1,
            clone.RelatedLinks.Count
        )

        Assert.AreEqual(
            "OSF Project",
            clone.RelatedLinks(0).Label
        )

        Assert.AreNotSame(
            source.RelatedLinks(0),
            clone.RelatedLinks(0)
        )

    End Sub


    <TestMethod>
    Public Sub CloneSubmission_PreservesReusableJournalId()

        Dim journalId As Guid =
            Guid.NewGuid()

        Dim source As New JournalSubmission With {
            .JournalName = "Journal",
            .JournalId = journalId,
            .PortalUrl = "https://submit.example.org/"
        }

        Dim clone As JournalSubmission =
            ManuscriptCloneService.CloneSubmission(
                source
            )

        Assert.AreEqual(
            journalId,
            clone.JournalId.Value
        )

        Assert.AreEqual(
            source.PortalUrl,
            clone.PortalUrl
        )

    End Sub


    Private Shared Function CreateTempDirectory() As String

        Dim tempPath As String =
            Path.Combine(
                Path.GetTempPath(),
                "PaperRouteJournalTests_" &
                Guid.NewGuid().ToString("N")
            )

        Directory.CreateDirectory(
            tempPath
        )

        Return tempPath

    End Function


    Private Shared Sub DeleteTempDirectory(
        path As String
    )

        If String.IsNullOrWhiteSpace(
            path
        ) OrElse
           Not Directory.Exists(
               path
           ) Then

            Return

        End If

        Try

            Directory.Delete(
                path,
                True
            )

        Catch
        End Try

    End Sub

End Class
