Imports System
Imports System.Collections.Generic
Imports System.Linq
Imports System.Reflection
Imports System.Windows.Forms
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline.Forms
Imports ManuscriptPipeline.Models
Imports ManuscriptPipeline.Services

<TestClass>
Public Class PublicationExportTests

    <TestMethod>
    Public Sub SelectByScope_PublishedOnly_ExcludesEarlierStages()

        Dim items As New List(Of Manuscript) From {
            New Manuscript With {
                .Title = "Idea",
                .CurrentStage = PaperStage.Idea
            },
            New Manuscript With {
                .Title = "Published",
                .CurrentStage = PaperStage.Published,
                .Location = ManuscriptLocation.Published
            }
        }

        Dim selected As List(Of Manuscript) =
            PublicationExportService.SelectByScope(
                items,
                PublicationExportScope.PublishedOnly
            )

        Assert.AreEqual(
            1,
            selected.Count
        )

        Assert.AreEqual(
            "Published",
            selected(0).Title
        )

    End Sub


    <TestMethod>
    Public Sub SelectByScope_AcceptedAndPublished_IncludesAcceptedInPressAndPublished()

        Dim items As New List(Of Manuscript) From {
            New Manuscript With {
                .Title = "Review",
                .CurrentStage = PaperStage.UnderReview
            },
            New Manuscript With {
                .Title = "Accepted",
                .CurrentStage = PaperStage.Accepted
            },
            New Manuscript With {
                .Title = "In Press",
                .CurrentStage = PaperStage.InPress
            },
            New Manuscript With {
                .Title = "Published",
                .CurrentStage = PaperStage.Published,
                .Location = ManuscriptLocation.Published
            }
        }

        Dim selected As List(Of Manuscript) =
            PublicationExportService.SelectByScope(
                items,
                PublicationExportScope.AcceptedAndPublished
            )

        Assert.AreEqual(
            3,
            selected.Count
        )

    End Sub


    <TestMethod>
    Public Sub FormatCitation_UsesStructuredAuthorsInManuscriptOrder()

        Dim first As New AuthorRecord With {
            .GivenName = "Jane",
            .FamilyName = "Smith"
        }

        Dim second As New AuthorRecord With {
            .GivenName = "John",
            .MiddleName = "Paul",
            .FamilyName = "Doe"
        }

        Dim library As New AuthorLibraryData()

        library.Authors.Add(first)
        library.Authors.Add(second)

        Dim manuscript As New Manuscript With {
            .Title = "A Paper"
        }

        manuscript.Authors.Add(
            New ManuscriptAuthor With {
                .AuthorId = first.Id
            }
        )

        manuscript.Authors.Add(
            New ManuscriptAuthor With {
                .AuthorId = second.Id
            }
        )

        manuscript.Metadata.PublishedDate =
            New DateTime(
                2026,
                1,
                1
            )

        Dim citation As String =
            PublicationExportService.FormatCitation(
                manuscript,
                library
            )

        Assert.IsTrue(
            citation.StartsWith(
                "Smith, J., & Doe, J. P. (2026).",
                StringComparison.Ordinal
            ),
            citation
        )

    End Sub


    <TestMethod>
    Public Sub FormatCitation_IncludesJournalVolumeIssuePagesAndDoi()

        Dim manuscript As New Manuscript With {
            .Title = "Metadata Rich Paper"
        }

        manuscript.Metadata.PublishedDate =
            New DateTime(
                2025,
                5,
                4
            )

        manuscript.Metadata.PublicationJournal =
            "Journal of Examples"

        manuscript.Metadata.Volume =
            "12"

        manuscript.Metadata.Issue =
            "3"

        manuscript.Metadata.Pages =
            "44-58"

        manuscript.Metadata.Doi =
            "https://doi.org/10.1234/example"

        Dim citation As String =
            PublicationExportService.FormatCitation(
                manuscript,
                New AuthorLibraryData()
            )

        StringAssert.Contains(
            citation,
            "Journal of Examples, 12(3), 44-58."
        )

        StringAssert.Contains(
            citation,
            "https://doi.org/10.1234/example"
        )

    End Sub


    <TestMethod>
    Public Sub FormatCitation_WithoutDoi_RemainsUseful()

        Dim manuscript As New Manuscript With {
            .Title = "No DOI Paper"
        }

        manuscript.Metadata.PublishedDate =
            New DateTime(
                2024,
                1,
                1
            )

        manuscript.Metadata.PublicationJournal =
            "Journal"

        Dim citation As String =
            PublicationExportService.FormatCitation(
                manuscript,
                New AuthorLibraryData()
            )

        StringAssert.Contains(
            citation,
            "(2024)."
        )

        StringAssert.Contains(
            citation,
            "No DOI Paper."
        )

        StringAssert.Contains(
            citation,
            "Journal."
        )

    End Sub


    <TestMethod>
    Public Sub FormatCitation_PreprintFallback_IsIncluded()

        Dim manuscript As New Manuscript With {
            .Title = "Preprint Paper"
        }

        manuscript.Metadata.PreprintDoi =
            "10.5555/preprint"

        Dim citation As String =
            PublicationExportService.FormatCitation(
                manuscript,
                New AuthorLibraryData()
            )

        StringAssert.Contains(
            citation,
            "[Preprint]."
        )

        StringAssert.Contains(
            citation,
            "Preprint: https://doi.org/10.5555/preprint"
        )

    End Sub


    <TestMethod>
    Public Sub Export_Markdown_CreatesEditableList()

        Dim manuscript As New Manuscript With {
            .Title = "Markdown Paper"
        }

        Dim output As String =
            PublicationExportService.Export(
                New List(Of Manuscript) From {
                    manuscript
                },
                New AuthorLibraryData(),
                PublicationExportFormat.Markdown,
                PublicationExportStyle.CvSection
            )

        StringAssert.Contains(
            output,
            "## Publications"
        )

        StringAssert.Contains(
            output,
            "- "
        )

        StringAssert.Contains(
            output,
            "Markdown Paper"
        )

    End Sub


    <TestMethod>
    Public Sub Export_Html_EscapesUntrustedText()

        Dim manuscript As New Manuscript With {
            .Title = "A <B> & C"
        }

        Dim output As String =
            PublicationExportService.Export(
                New List(Of Manuscript) From {
                    manuscript
                },
                New AuthorLibraryData(),
                PublicationExportFormat.Html,
                PublicationExportStyle.PublicationList
            )

        StringAssert.Contains(
            output,
            "A &lt;B&gt; &amp; C"
        )

        Assert.IsFalse(
            output.Contains(
                "A <B> & C",
                StringComparison.Ordinal
            )
        )

    End Sub


    <TestMethod>
    Public Sub Export_DoesNotMutateManuscriptData()

        Dim manuscript As New Manuscript With {
            .Title = "Original Title",
            .CurrentStage = PaperStage.Accepted
        }

        Dim originalStage As PaperStage =
            manuscript.CurrentStage

        Dim originalTitle As String =
            manuscript.Title

        PublicationExportService.Export(
            New List(Of Manuscript) From {
                manuscript
            },
            New AuthorLibraryData(),
            PublicationExportFormat.PlainText,
            PublicationExportStyle.CvSection
        )

        Assert.AreEqual(
            originalTitle,
            manuscript.Title
        )

        Assert.AreEqual(
            originalStage,
            manuscript.CurrentStage
        )

    End Sub


    <TestMethod>
    Public Sub FormatCitation_PublisherOnlyRecord_IncludesPublisher()

        Dim manuscript As New Manuscript With {
            .Title = "A Book"
        }

        manuscript.Metadata.PublishedDate =
            New DateTime(
                2020,
                1,
                1
            )

        manuscript.Metadata.Publisher =
            "Example University Press"

        Dim citation As String =
            PublicationExportService.FormatCitation(
                manuscript,
                New AuthorLibraryData()
            )

        StringAssert.Contains(
            citation,
            "Example University Press."
        )

    End Sub


    <TestMethod>
    Public Sub FormatCitation_AcceptedRecord_CanUseTargetJournalAsOutlet()

        Dim manuscript As New Manuscript With {
            .Title = "Accepted Paper",
            .CurrentStage = PaperStage.Accepted,
            .TargetJournal = "Journal of Future Work"
        }

        Dim citation As String =
            PublicationExportService.FormatCitation(
                manuscript,
                New AuthorLibraryData()
            )

        StringAssert.Contains(
            citation,
            "Journal of Future Work."
        )

    End Sub


    ' The preview is rebuilt once per change, not once per checked item
    ' (#232). Every ItemCheck queued a full export of the whole selection,
    ' so a scope change, Select None or Select All queued one for each
    ' manuscript and froze the dialog on a large record. Each now rebuilds
    ' the preview at most twice, once directly and once from the refresh it
    ' queued, and the preview is still the export of what is checked.
    <TestMethod>
    Public Sub Preview_IsRebuiltOncePerChangeNotOncePerCheckedItem()

        Dim manuscripts As New List(Of Manuscript)()

        For number As Integer = 1 To 40

            Dim published As New Manuscript With {
                .Title = "Published Study " & number,
                .CurrentStage = PaperStage.Published,
                .Location = ManuscriptLocation.Published
            }

            published.Metadata.PublishedDate =
                New DateTime(
                    1980 + number,
                    1,
                    1
                )

            published.Metadata.PublicationJournal =
                "Journal of Examples"

            manuscripts.Add(
                published
            )

        Next

        For number As Integer = 1 To 5

            manuscripts.Add(
                New Manuscript With {
                    .Title = "Study in Progress " & number,
                    .CurrentStage = PaperStage.UnderReview
                }
            )

        Next

        Dim authors As New AuthorLibraryData()

        Dim everything As List(Of Manuscript) =
            PublicationExportService.SelectByScope(
                manuscripts,
                PublicationExportScope.AllManuscripts
            )

        ' Not the defaults, so the preview is read in the format, style and
        ' grouping chosen.
        Dim expected As Func(Of List(Of Manuscript), String) =
            Function(selected)
                Return PublicationExportService.Export(
                    selected,
                    authors,
                    PublicationExportFormat.Markdown,
                    PublicationExportStyle.PublicationList,
                    True
                )
            End Function

        StaRunner.Run(
            Sub()

                Using dialog As New PublicationExportForm(
                    manuscripts,
                    authors
                )

                    ViewCatalog.ShowOffscreen(
                        dialog
                    )

                    Dim scope As ComboBox =
                        ExportField(Of ComboBox)(dialog, "cmbScope")

                    Dim style As ComboBox =
                        ExportField(Of ComboBox)(dialog, "cmbStyle")

                    Dim format As ComboBox =
                        ExportField(Of ComboBox)(dialog, "cmbFormat")

                    Dim items As CheckedListBox =
                        ExportField(Of CheckedListBox)(dialog, "lstItems")

                    Dim preview As TextBox =
                        ExportField(Of TextBox)(dialog, "txtPreview")

                    format.SelectedIndex =
                        format.FindStringExact("Markdown")

                    style.SelectedIndex =
                        style.FindStringExact("Publication list")

                    ExportField(Of CheckBox)(dialog, "chkGroupByType").Checked =
                        True

                    Application.DoEvents()

                    AssertRebuiltAtMostTwice(
                        dialog,
                        "Switching the scope to All manuscripts",
                        Sub()
                            scope.SelectedIndex =
                                scope.FindStringExact("All manuscripts")
                        End Sub
                    )

                    Assert.AreEqual(
                        everything.Count,
                        items.CheckedItems.Count
                    )

                    Assert.AreEqual(
                        expected(everything),
                        preview.Text
                    )

                    AssertRebuiltAtMostTwice(
                        dialog,
                        "Select None",
                        Sub() ExportButton(dialog, "Select None").PerformClick()
                    )

                    Assert.AreEqual(
                        0,
                        items.CheckedItems.Count
                    )

                    Assert.AreEqual(
                        expected(New List(Of Manuscript)()),
                        preview.Text
                    )

                    AssertRebuiltAtMostTwice(
                        dialog,
                        "Select All",
                        Sub() ExportButton(dialog, "Select All").PerformClick()
                    )

                    Assert.AreEqual(
                        everything.Count,
                        items.CheckedItems.Count
                    )

                    Assert.AreEqual(
                        expected(everything),
                        preview.Text
                    )

                    ' A guard that could not fail before the change: one
                    ' checkbox, checked as a click checks it, still updates
                    ' the preview once its queued refresh runs.
                    Dim before As Integer =
                        dialog.PreviewCount

                    items.SetItemChecked(
                        3,
                        False
                    )

                    Application.DoEvents()

                    Assert.AreEqual(
                        1,
                        dialog.PreviewCount - before
                    )

                    Assert.AreEqual(
                        expected(
                            everything.Where(
                                Function(item, index) index <> 3
                            ).ToList()
                        ),
                        preview.Text
                    )

                    dialog.Close()

                End Using

            End Sub
        )

    End Sub


    ' Runs one action and the messages it queued, and fails with the count
    ' if the preview was rebuilt more than twice.
    Private Shared Sub AssertRebuiltAtMostTwice(
        dialog As PublicationExportForm,
        actionName As String,
        perform As Action
    )

        Dim before As Integer =
            dialog.PreviewCount

        perform()

        Application.DoEvents()

        Dim rebuilt As Integer =
            dialog.PreviewCount - before

        Assert.IsTrue(
            rebuilt <= 2,
            actionName & " rebuilt the preview " & rebuilt & " times."
        )

    End Sub


    Private Shared Function ExportField(Of T)(
        dialog As PublicationExportForm,
        name As String
    ) As T

        Return DirectCast(
            GetType(PublicationExportForm).
                GetField(
                    name,
                    BindingFlags.Instance Or
                    BindingFlags.NonPublic
                ).
                GetValue(dialog),
            T
        )

    End Function


    Private Shared Function ExportButton(
        dialog As PublicationExportForm,
        text As String
    ) As Button

        Return Descendants(dialog).
            OfType(Of Button)().
            Single(
                Function(button) button.Text = text
            )

    End Function


    Private Shared Iterator Function Descendants(
        parent As Control
    ) As IEnumerable(Of Control)

        For Each child As Control In parent.Controls

            Yield child

            For Each descendant As Control In Descendants(child)
                Yield descendant
            Next

        Next

    End Function

End Class
