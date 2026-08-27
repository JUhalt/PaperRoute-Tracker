Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline.Services

<TestClass>
Public Class ReleaseNotesPresentationTests

    <TestMethod>
    Public Sub MarkdownHeadingsLoseHashSyntaxAndRemainVisuallyDistinct()

        Dim result As String =
            ReleaseNotesPresentationService.ToDisplayText(
                "# What's new" & vbLf &
                "## Route"
            )

        StringAssert.Contains(
            result,
            "What's new"
        )

        StringAssert.Contains(
            result,
            "Route"
        )

        Assert.IsFalse(
            result.Contains(
                "#"
            )
        )

        StringAssert.Contains(
            result,
            "───"
        )

    End Sub


    <TestMethod>
    Public Sub MarkdownBulletsBecomeReadableBullets()

        Dim result As String =
            ReleaseNotesPresentationService.ToDisplayText(
                "- Added Route View" & vbLf &
                "* Added Version History"
            )

        StringAssert.Contains(
            result,
            "• Added Route View"
        )

        StringAssert.Contains(
            result,
            "• Added Version History"
        )

    End Sub


    <TestMethod>
    Public Sub WrappedParagraphLinesAreJoined()

        Dim result As String =
            ReleaseNotesPresentationService.ToDisplayText(
                "PaperRoute now tracks manuscript" & vbLf &
                "versions through the publication workflow."
            )

        Assert.AreEqual(
            "PaperRoute now tracks manuscript versions through the publication workflow.",
            result
        )

    End Sub


    <TestMethod>
    Public Sub InlineMarkdownIsCleanedWithoutDroppingLinkDestination()

        Dim result As String =
            ReleaseNotesPresentationService.ToDisplayText(
                "**Read** the [release page](https://example.test/release)."
            )

        Assert.AreEqual(
            "Read the release page (https://example.test/release).",
            result
        )

    End Sub


    <TestMethod>
    Public Sub BlankReleaseNotesReturnFriendlyFallback()

        Assert.AreEqual(
            "No release notes were included with this update.",
            ReleaseNotesPresentationService.ToDisplayText(
                "   "
            )
        )

    End Sub

End Class
