Imports System
Imports System.Collections.Generic
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

<TestClass>
Public Class ManuscriptCloneTests

    <TestMethod>
    Public Sub Clone_PreservesAndDeepCopiesSchema2Metadata()

        Dim source As New Manuscript()

        source.Metadata.AbstractText =
            "Original abstract"

        source.Metadata.Keywords.Add(
            "original"
        )

        source.Metadata.Doi =
            "10.1000/example"

        source.Metadata.ExternalIdentifiers(
            "custom"
        ) = "ABC"

        Dim clone As Manuscript =
            ManuscriptCloneService.CloneManuscript(
                source
            )

        Assert.AreEqual(
            "Original abstract",
            clone.Metadata.AbstractText
        )

        Assert.AreEqual(
            "10.1000/example",
            clone.Metadata.Doi
        )

        clone.Metadata.AbstractText =
            "Changed"

        clone.Metadata.Keywords.Add(
            "clone-only"
        )

        clone.Metadata.ExternalIdentifiers(
            "custom"
        ) = "XYZ"

        Assert.AreEqual(
            "Original abstract",
            source.Metadata.AbstractText
        )

        Assert.AreEqual(
            1,
            source.Metadata.Keywords.Count
        )

        Assert.AreEqual(
            "ABC",
            source.Metadata.ExternalIdentifiers(
                "custom"
            )
        )

    End Sub


    <TestMethod>
    Public Sub Clone_PreservesAndDeepCopiesStructuredAuthors()

        Dim authorId As Guid =
            Guid.NewGuid()

        Dim affiliationId As Guid =
            Guid.NewGuid()

        Dim source As New Manuscript()

        source.Authors.Add(
            New ManuscriptAuthor With {
                .AuthorId = authorId,
                .AffiliationIds =
                    New List(Of Guid) From {
                        affiliationId
                    },
                .IsCorrespondingAuthor = True
            }
        )

        Dim clone As Manuscript =
            ManuscriptCloneService.CloneManuscript(
                source
            )

        Assert.AreEqual(
            1,
            clone.Authors.Count
        )

        Assert.AreEqual(
            authorId,
            clone.Authors(0).AuthorId
        )

        Assert.IsTrue(
            clone.Authors(0).IsCorrespondingAuthor
        )

        clone.Authors(0).AffiliationIds.Clear()

        Assert.AreEqual(
            1,
            source.Authors(0).AffiliationIds.Count
        )

    End Sub


    <TestMethod>
    Public Sub Clone_DoesNotMutateSourceSubmissionCollections()

        Dim source As Manuscript =
            CreateRepresentativeLibrary()(0)

        Dim clone As Manuscript =
            ManuscriptCloneService.CloneManuscript(
                source
            )

        clone.Submissions(0).Notes =
            "Clone-only change"

        clone.Submissions(0).Decisions.Clear()

        Assert.AreEqual(
            "Round one submission.",
            source.Submissions(0).Notes
        )

        Assert.AreEqual(
            1,
            source.Submissions(0).Decisions.Count
        )

    End Sub

End Class
