Imports System
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

<TestClass>
Public Class StageClockServiceTests

    Private Shared ReadOnly Today As New DateTime(2026, 9, 26)

    <TestMethod>
    Public Sub RecordedStageChange_ShowsDaysInStage()

        Dim manuscript As Manuscript = Sample(ManuscriptLocation.Pipeline, New DateTime(2026, 8, 23), historyCount:=2)

        Assert.AreEqual("34 days", StageClockService.Describe(manuscript, Today))
        manuscript.StageEnteredDate = New DateTime(2026, 9, 25, 16, 30, 0)
        Assert.AreEqual("1 day", StageClockService.Describe(manuscript, Today))
        manuscript.StageEnteredDate = Today.AddHours(9)
        Assert.AreEqual("since today", StageClockService.Describe(manuscript, Today))

    End Sub

    <TestMethod>
    Public Sub WithoutARecordedStageChange_SaysWhenTheRecordWasAdded()

        Dim manuscript As Manuscript = Sample(ManuscriptLocation.Pipeline, New DateTime(2026, 9, 14), historyCount:=1)

        Assert.AreEqual("added 12 days ago", StageClockService.Describe(manuscript, Today))
        manuscript.History.Clear()
        Assert.AreEqual("added 12 days ago", StageClockService.Describe(manuscript, Today))
        manuscript.StageEnteredDate = Today
        Assert.AreEqual("added today", StageClockService.Describe(manuscript, Today))

    End Sub

    <TestMethod>
    Public Sub PublishedFiledAndFutureDates_ShowNothing()

        Assert.AreEqual(String.Empty, StageClockService.Describe(Sample(ManuscriptLocation.Published, Today.AddDays(-30), 2), Today))
        Assert.AreEqual(String.Empty, StageClockService.Describe(Sample(ManuscriptLocation.FileDrawer, Today.AddDays(-30), 2), Today))
        Assert.AreEqual(String.Empty, StageClockService.Describe(Sample(ManuscriptLocation.Pipeline, Today.AddDays(3), 2), Today))
        Assert.AreEqual(String.Empty, StageClockService.Describe(Nothing, Today))

    End Sub

    Private Shared Function Sample(location As ManuscriptLocation, entered As DateTime, historyCount As Integer) As Manuscript

        Dim manuscript As New Manuscript With {
            .Title = "Synthetic stage clock manuscript",
            .Location = location,
            .StageEnteredDate = entered
        }

        For index As Integer = 1 To historyCount
            manuscript.History.Add(New HistoryEvent With {.EventDate = entered, .Stage = PaperStage.Draft})
        Next

        Return manuscript

    End Function

End Class
