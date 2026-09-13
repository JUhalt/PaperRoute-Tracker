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


    <TestMethod>
    Public Sub ReadinessTemplateHelpSeparatesTitleCategoryAndImportance()

        StringAssert.Contains(
            WorkflowHelpCatalog.ReadinessRequirementTitle,
            "Do not enter Required or Optional"
        )

        StringAssert.Contains(
            WorkflowHelpCatalog.ReadinessCategory,
            "grouping label"
        )

        StringAssert.Contains(
            WorkflowHelpCatalog.ReadinessImportance,
            "Unchecked means optional"
        )

    End Sub


    <TestMethod>
    Public Sub ReadinessStateHelpExplainsNotApplicableAndAdditiveRefresh()

        StringAssert.Contains(
            WorkflowHelpCatalog.ReadinessNotApplicable,
            "resolved for readiness"
        )

        StringAssert.Contains(
            WorkflowHelpCatalog.ReadinessNotApplicable,
            "not counted as completed"
        )

        StringAssert.Contains(
            WorkflowHelpCatalog.ReadinessTemplateRefresh,
            "Adds only requirements"
        )

        StringAssert.Contains(
            WorkflowHelpCatalog.ReadinessTemplateRefresh,
            "are not rewritten"
        )

    End Sub

End Class
