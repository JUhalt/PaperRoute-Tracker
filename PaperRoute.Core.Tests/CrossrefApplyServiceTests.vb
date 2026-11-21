Imports System
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

<TestClass>
Public Class CrossrefApplyServiceTests

    <TestMethod>
    Public Sub Apply_DoesNotChangeLifecycleFields()

        Dim manuscript As New Manuscript With {
            .Title = "Working title",
            .TargetJournal = "Target Journal",
            .CurrentStage = PaperStage.Submitted,
            .Location = ManuscriptLocation.Pipeline
        }

        Dim suggestion As New CrossrefMetadataSuggestion With {
            .Doi = "10.1234/example",
            .Title = "Published title",
            .Journal = "Published Journal"
        }

        Dim options As New CrossrefApplyOptions With {
            .ApplyDoi = True,
            .ApplyTitle = True,
            .ApplyPublicationDetails = True
        }

        CrossrefApplyService.Apply(
            suggestion,
            manuscript,
            New AuthorLibraryData(),
            options
        )

        Assert.AreEqual(
            PaperStage.Submitted,
            manuscript.CurrentStage
        )

        Assert.AreEqual(
            ManuscriptLocation.Pipeline,
            manuscript.Location
        )

        Assert.AreEqual(
            "Target Journal",
            manuscript.TargetJournal
        )

    End Sub


    <TestMethod>
    Public Sub Apply_OnlyChangesSelectedMetadata()

        Dim manuscript As New Manuscript With {
            .Title = "Keep this title"
        }

        manuscript.Metadata.PublicationJournal =
            "Keep this journal"

        Dim suggestion As New CrossrefMetadataSuggestion With {
            .Doi = "https://doi.org/10.1234/example",
            .Title = "Replacement title",
            .Journal = "Replacement journal"
        }

        Dim options As New CrossrefApplyOptions With {
            .ApplyDoi = True,
            .ApplyTitle = False,
            .ApplyPublicationDetails = False
        }

        CrossrefApplyService.Apply(
            suggestion,
            manuscript,
            New AuthorLibraryData(),
            options
        )

        Assert.AreEqual(
            "10.1234/example",
            manuscript.Metadata.Doi
        )

        Assert.AreEqual(
            "Keep this title",
            manuscript.Title
        )

        Assert.AreEqual(
            "Keep this journal",
            manuscript.Metadata.PublicationJournal
        )

    End Sub


    <TestMethod>
    Public Sub Apply_RecordsCrossrefProvenance()

        Dim manuscript As New Manuscript()
        Dim suggestion As New CrossrefMetadataSuggestion With {
            .Doi = "10.1234/example"
        }

        CrossrefApplyService.Apply(
            suggestion,
            manuscript,
            New AuthorLibraryData(),
            New CrossrefApplyOptions()
        )

        Assert.AreEqual(
            "10.1234/example",
            manuscript.Metadata.ExternalIdentifiers(
                "crossref:source-doi"
            )
        )

        Assert.IsTrue(
            manuscript.Metadata.ExternalIdentifiers.ContainsKey(
                "crossref:last-applied-utc"
            )
        )

    End Sub


    <TestMethod>
    Public Sub ApplyAuthors_MatchesExistingAuthorByOrcid()

        Dim existing As New AuthorRecord With {
            .GivenName = "Jane",
            .FamilyName = "Smith",
            .Orcid = "0000-0001-2345-6789"
        }

        Dim library As New AuthorLibraryData()

        library.Authors.Add(
            existing
        )

        Dim suggestedAuthor As New CrossrefAuthorSuggestion With {
            .GivenName = "J.",
            .FamilyName = "Smith",
            .Orcid = "https://orcid.org/0000-0001-2345-6789"
        }

        Dim suggestion As New CrossrefMetadataSuggestion With {
            .Doi = "10.1234/example"
        }

        suggestion.Authors.Add(
            suggestedAuthor
        )

        Dim manuscript As New Manuscript()

        Dim result As CrossrefApplyResult =
            CrossrefApplyService.Apply(
                suggestion,
                manuscript,
                library,
                New CrossrefApplyOptions With {
                    .AddMissingAuthors = True
                }
            )

        Assert.AreEqual(
            1,
            library.Authors.Count
        )

        Assert.AreEqual(
            existing.Id,
            manuscript.Authors(0).AuthorId
        )

        Assert.AreEqual(
            0,
            result.AuthorsCreatedInLibrary
        )

    End Sub


    <TestMethod>
    Public Sub ApplyAuthors_CreatesNewAuthorAndAffiliation()

        Dim suggestedAuthor As New CrossrefAuthorSuggestion With {
            .GivenName = "Alex",
            .FamilyName = "Jones"
        }

        suggestedAuthor.Affiliations.Add(
            "Example University"
        )

        Dim suggestion As New CrossrefMetadataSuggestion With {
            .Doi = "10.1234/example"
        }

        suggestion.Authors.Add(
            suggestedAuthor
        )

        Dim library As New AuthorLibraryData()
        Dim manuscript As New Manuscript()

        Dim result As CrossrefApplyResult =
            CrossrefApplyService.Apply(
                suggestion,
                manuscript,
                library,
                New CrossrefApplyOptions With {
                    .AddMissingAuthors = True
                }
            )

        Assert.AreEqual(
            1,
            library.Authors.Count
        )

        Assert.AreEqual(
            1,
            library.Affiliations.Count
        )

        Assert.AreEqual(
            1,
            manuscript.Authors.Count
        )

        Assert.AreEqual(
            1,
            result.AuthorsCreatedInLibrary
        )

        Assert.AreEqual(
            1,
            result.AffiliationsCreatedInLibrary
        )

        Assert.IsTrue(
            result.AuthorLibraryChanged
        )

    End Sub


    <TestMethod>
    Public Sub ApplyAuthors_DoesNotDuplicateAlreadyAssignedAuthor()

        Dim existing As New AuthorRecord With {
            .GivenName = "Jane",
            .FamilyName = "Smith"
        }

        Dim library As New AuthorLibraryData()

        library.Authors.Add(
            existing
        )

        Dim manuscript As New Manuscript()

        manuscript.Authors.Add(
            New ManuscriptAuthor With {
                .AuthorId = existing.Id
            }
        )

        Dim suggestion As New CrossrefMetadataSuggestion With {
            .Doi = "10.1234/example"
        }

        suggestion.Authors.Add(
            New CrossrefAuthorSuggestion With {
                .GivenName = "Jane",
                .FamilyName = "Smith"
            }
        )

        Dim result As CrossrefApplyResult =
            CrossrefApplyService.Apply(
                suggestion,
                manuscript,
                library,
                New CrossrefApplyOptions With {
                    .AddMissingAuthors = True
                }
            )

        Assert.AreEqual(
            1,
            manuscript.Authors.Count
        )

        Assert.AreEqual(
            0,
            result.AuthorsAddedToManuscript
        )

    End Sub


    <TestMethod>
    Public Sub ApplyPublicationDetails_DoesNotEraseMissingCrossrefFields()

        Dim manuscript As New Manuscript()

        manuscript.Metadata.Volume =
            "Existing Volume"

        Dim suggestion As New CrossrefMetadataSuggestion With {
            .Doi = "10.1234/example",
            .Journal = "Updated Journal",
            .Volume = String.Empty
        }

        CrossrefApplyService.Apply(
            suggestion,
            manuscript,
            New AuthorLibraryData(),
            New CrossrefApplyOptions With {
                .ApplyPublicationDetails = True
            }
        )

        Assert.AreEqual(
            "Updated Journal",
            manuscript.Metadata.PublicationJournal
        )

        Assert.AreEqual(
            "Existing Volume",
            manuscript.Metadata.Volume
        )

    End Sub


    <TestMethod>
    Public Sub ApplyAuthors_EnrichesAffiliationsForAlreadyAssignedAuthor()

        Dim existing As New AuthorRecord With {
            .GivenName = "Jane",
            .FamilyName = "Smith"
        }

        Dim library As New AuthorLibraryData()
        library.Authors.Add(existing)

        Dim manuscript As New Manuscript()

        manuscript.Authors.Add(
            New ManuscriptAuthor With {
                .AuthorId = existing.Id
            }
        )

        Dim suggestedAuthor As New CrossrefAuthorSuggestion With {
            .GivenName = "Jane",
            .FamilyName = "Smith"
        }

        suggestedAuthor.Affiliations.Add(
            "Department of Psychology, Example University"
        )

        Dim suggestion As New CrossrefMetadataSuggestion With {
            .Doi = "10.1234/example"
        }

        suggestion.Authors.Add(
            suggestedAuthor
        )

        Dim result As CrossrefApplyResult =
            CrossrefApplyService.Apply(
                suggestion,
                manuscript,
                library,
                New CrossrefApplyOptions With {
                    .AddMissingAuthors = True
                }
            )

        Assert.AreEqual(
            1,
            manuscript.Authors.Count
        )

        Assert.AreEqual(
            1,
            library.Affiliations.Count
        )

        Assert.AreEqual(
            1,
            manuscript.Authors(0).AffiliationIds.Count
        )

        Assert.AreEqual(
            library.Affiliations(0).Id,
            manuscript.Authors(0).AffiliationIds(0)
        )

        Assert.AreEqual(
            0,
            result.AuthorsAddedToManuscript
        )

    End Sub

End Class
