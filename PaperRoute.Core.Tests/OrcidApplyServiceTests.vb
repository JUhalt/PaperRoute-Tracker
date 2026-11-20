Imports System
Imports System.Collections.Generic
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

<TestClass>
Public Class OrcidApplyServiceTests

    Private Function CreateProfile() As OrcidProfileSuggestion

        Dim profile As New OrcidProfileSuggestion With {
            .Orcid = "0000-0002-1825-0097",
            .GivenName = "Remote",
            .FamilyName = "Researcher",
            .CreditName = "R. Researcher"
        }

        profile.Affiliations.Add(
            New OrcidAffiliationSuggestion With {
                .Institution = "Example University",
                .Department = "Psychology",
                .City = "Hartford",
                .Region = "CT",
                .Country = "US"
            }
        )

        profile.Works.Add(
            New OrcidWorkSuggestion With {
                .PutCode = 42,
                .Title = "Imported ORCID Work",
                .WorkType = "JOURNAL_ARTICLE",
                .Doi = "10.1234/imported",
                .PublishedDate = New DateTime(2025, 1, 1)
            }
        )

        Return profile

    End Function


    <TestMethod>
    Public Sub Apply_UnselectedName_DoesNotOverwriteLocalName()

        Dim author As New AuthorRecord With {
            .GivenName = "Local",
            .FamilyName = "Author"
        }

        Dim library As New AuthorLibraryData()
        library.Authors.Add(author)

        OrcidApplyService.Apply(
            author,
            library,
            New List(Of Manuscript)(),
            CreateProfile(),
            New OrcidApplyOptions()
        )

        Assert.AreEqual(
            "Local",
            author.GivenName
        )

        Assert.AreEqual(
            "Author",
            author.FamilyName
        )

        Assert.AreEqual(
            "0000-0002-1825-0097",
            author.Orcid
        )

        Assert.IsTrue(
            author.OrcidLastCheckedUtc.HasValue
        )

    End Sub


    <TestMethod>
    Public Sub Apply_SelectedNameAndCreditName_AreApplied()

        Dim author As New AuthorRecord With {
            .GivenName = "Local",
            .FamilyName = "Author"
        }

        Dim library As New AuthorLibraryData()
        library.Authors.Add(author)

        OrcidApplyService.Apply(
            author,
            library,
            New List(Of Manuscript)(),
            CreateProfile(),
            New OrcidApplyOptions With {
                .ApplyName = True,
                .ApplyCreditName = True
            }
        )

        Assert.AreEqual(
            "Remote",
            author.GivenName
        )

        Assert.AreEqual(
            "Researcher",
            author.FamilyName
        )

        Assert.AreEqual(
            "R. Researcher",
            author.DisplayNameOverride
        )

    End Sub


    <TestMethod>
    Public Sub Apply_Affiliations_AddsOnlyMissingReusableRecord()

        Dim author As New AuthorRecord()
        Dim library As New AuthorLibraryData()
        library.Authors.Add(author)

        Dim options As New OrcidApplyOptions With {
            .AddAffiliations = True
        }

        Dim profile As OrcidProfileSuggestion =
            CreateProfile()

        OrcidApplyService.Apply(
            author,
            library,
            New List(Of Manuscript)(),
            profile,
            options
        )

        OrcidApplyService.Apply(
            author,
            library,
            New List(Of Manuscript)(),
            profile,
            options
        )

        Assert.AreEqual(
            1,
            library.Affiliations.Count
        )

    End Sub


    <TestMethod>
    Public Sub Apply_SelectedWork_ImportsIdeaWithoutPublishing()

        Dim author As New AuthorRecord()
        Dim library As New AuthorLibraryData()
        library.Authors.Add(author)

        Dim manuscripts As New List(Of Manuscript)()

        Dim result As OrcidApplyResult =
            OrcidApplyService.Apply(
                author,
                library,
                manuscripts,
                CreateProfile(),
                New OrcidApplyOptions With {
                    .SelectedWorkPutCodes =
                        New List(Of Long) From {
                            42
                        }
                }
            )

        Assert.AreEqual(
            1,
            result.ManuscriptsImported
        )

        Assert.AreEqual(
            1,
            manuscripts.Count
        )

        Assert.AreEqual(
            PaperStage.Idea,
            manuscripts(0).CurrentStage
        )

        Assert.AreEqual(
            ManuscriptLocation.Pipeline,
            manuscripts(0).Location
        )

        Assert.AreEqual(
            "10.1234/imported",
            manuscripts(0).Metadata.Doi
        )

        Assert.AreEqual(
            author.Id,
            manuscripts(0).Authors(0).AuthorId
        )

    End Sub


    <TestMethod>
    Public Sub Apply_DuplicateDoi_IsSkipped()

        Dim author As New AuthorRecord()
        Dim library As New AuthorLibraryData()
        library.Authors.Add(author)

        Dim manuscripts As New List(Of Manuscript) From {
            New Manuscript With {
                .Title = "Already Here",
                .Metadata =
                    New ManuscriptMetadata With {
                        .Doi = "https://doi.org/10.1234/imported"
                    }
            }
        }

        Dim result As OrcidApplyResult =
            OrcidApplyService.Apply(
                author,
                library,
                manuscripts,
                CreateProfile(),
                New OrcidApplyOptions With {
                    .SelectedWorkPutCodes =
                        New List(Of Long) From {
                            42
                        }
                }
            )

        Assert.AreEqual(
            0,
            result.ManuscriptsImported
        )

        Assert.AreEqual(
            1,
            result.DuplicateWorksSkipped
        )

        Assert.AreEqual(
            1,
            manuscripts.Count
        )

    End Sub


    <TestMethod>
    Public Sub Apply_SelectedDatedWork_CanExplicitlyImportAsPublished()

        Dim author As New AuthorRecord()
        Dim library As New AuthorLibraryData()
        library.Authors.Add(author)

        Dim manuscripts As New List(Of Manuscript)()

        Dim result As OrcidApplyResult =
            OrcidApplyService.Apply(
                author,
                library,
                manuscripts,
                CreateProfile(),
                New OrcidApplyOptions With {
                    .ImportDatedWorksAsPublished = True,
                    .SelectedWorkPutCodes =
                        New List(Of Long) From {
                            42
                        }
                }
            )

        Assert.AreEqual(
            1,
            result.ManuscriptsImported
        )

        Assert.AreEqual(
            PaperStage.Published,
            manuscripts(0).CurrentStage
        )

        Assert.AreEqual(
            ManuscriptLocation.Published,
            manuscripts(0).Location
        )

        Assert.AreEqual(
            New DateTime(2025, 1, 1),
            manuscripts(0).StageEnteredDate
        )

    End Sub

End Class
