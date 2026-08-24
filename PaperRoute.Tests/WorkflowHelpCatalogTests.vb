Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline.Services

<TestClass>
Public Class WorkflowHelpCatalogTests

    <TestMethod>
    Public Sub CurrentStateAndCurrentVersionHelpAreDistinct()

        Assert.IsFalse(
            String.IsNullOrWhiteSpace(
                WorkflowHelpCatalog.CurrentState
            )
        )

        Assert.IsFalse(
            String.IsNullOrWhiteSpace(
                WorkflowHelpCatalog.CurrentVersion
            )
        )

        Assert.AreNotEqual(
            WorkflowHelpCatalog.CurrentState,
            WorkflowHelpCatalog.CurrentVersion
        )

    End Sub


    <TestMethod>
    Public Sub JournalManuscriptIdHelpDistinguishesPublisherIdFromVersion()

        StringAssert.Contains(
            WorkflowHelpCatalog.JournalManuscriptId,
            "journal or publisher"
        )

        StringAssert.Contains(
            WorkflowHelpCatalog.JournalManuscriptId,
            "not a PaperRoute manuscript-version label"
        )

    End Sub

End Class
