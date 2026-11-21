Imports System.Collections.Generic
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

<TestClass>
Public Class StageSummaryTests

    <TestMethod>
    Public Sub CountByStage_ReturnsZeroForMissingStages()

        Dim manuscripts As New List(Of Manuscript) From {
            New Manuscript With {
                .CurrentStage = PaperStage.Draft
            }
        }

        Dim counts =
            ManuscriptStageSummaryService.CountByStage(
                manuscripts
            )

        Assert.AreEqual(1, counts(PaperStage.Draft))
        Assert.AreEqual(0, counts(PaperStage.Submitted))
        Assert.AreEqual(0, counts(PaperStage.Published))

    End Sub


    <TestMethod>
    Public Sub CountByStage_CountsEveryManuscriptAcrossLocations()

        Dim manuscripts As New List(Of Manuscript) From {
            New Manuscript With {
                .CurrentStage = PaperStage.Draft,
                .Location = ManuscriptLocation.Pipeline
            },
            New Manuscript With {
                .CurrentStage = PaperStage.Draft,
                .Location = ManuscriptLocation.FileDrawer
            },
            New Manuscript With {
                .CurrentStage = PaperStage.Published,
                .Location = ManuscriptLocation.Published
            }
        }

        Dim counts =
            ManuscriptStageSummaryService.CountByStage(
                manuscripts
            )

        Assert.AreEqual(2, counts(PaperStage.Draft))
        Assert.AreEqual(1, counts(PaperStage.Published))

    End Sub

End Class
