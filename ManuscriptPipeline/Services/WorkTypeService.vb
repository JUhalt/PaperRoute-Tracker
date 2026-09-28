Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.IO
Imports System.Linq
Imports System.Text.RegularExpressions
Imports ManuscriptPipeline.Models

Namespace Services

    ' Work types and tags (#64): names, the tag palette, normalization, and
    ' how imported records map to a type. A type is set only when the user or
    ' the source says so; nothing is inferred.
    Public NotInheritable Class WorkTypeService

        Public Const MaximumTagLength As Integer = 40

        Private Sub New()
        End Sub


        Public Shared Function DisplayName(type As WorkType) As String
            Select Case type
                Case WorkType.JournalArticle : Return "Journal article"
                Case WorkType.Preprint : Return "Preprint"
                Case WorkType.ConferencePaper : Return "Conference paper"
                Case WorkType.ConferenceAbstract : Return "Conference abstract"
                Case WorkType.Poster : Return "Poster"
                Case WorkType.BookChapter : Return "Book chapter"
                Case WorkType.Thesis : Return "Thesis or dissertation"
                Case WorkType.Other : Return "Other"
                Case Else : Return "Not specified"
            End Select
        End Function


        ' A type from its display name or enum name, ignoring case and spaces.
        ' Unspecified when the text names no type.
        Public Shared Function Parse(text As String) As WorkType
            Dim key As String = Regex.Replace(If(text, String.Empty), "[\s_-]", String.Empty)
            If key.Length = 0 Then Return WorkType.Unspecified
            For Each type As WorkType In [Enum].GetValues(GetType(WorkType))
                If String.Equals(key, type.ToString(), StringComparison.OrdinalIgnoreCase) OrElse
                   String.Equals(key, Regex.Replace(DisplayName(type), "\s", String.Empty), StringComparison.OrdinalIgnoreCase) Then
                    Return type
                End If
            Next
            Select Case key.ToLowerInvariant()
                Case "article", "journal" : Return WorkType.JournalArticle
                Case "thesis", "dissertation" : Return WorkType.Thesis
                Case "chapter" : Return WorkType.BookChapter
                Case "abstract" : Return WorkType.ConferenceAbstract
            End Select
            Return WorkType.Unspecified
        End Function


        ' Trimmed, single-spaced, and at most MaximumTagLength characters.
        ' Empty when nothing usable remains.
        Public Shared Function NormalizeTag(tag As String) As String
            Dim value As String = Regex.Replace(If(tag, String.Empty).Trim().TrimStart("#"c).Trim(), "\s+", " ")
            If value.Length > MaximumTagLength Then value = value.Substring(0, MaximumTagLength).TrimEnd()
            Return value
        End Function


        ' Adds a tag unless the manuscript already has it in any capitalization.
        ' Returns whether it was added.
        Public Shared Function AddTag(manuscript As Manuscript, tag As String) As Boolean
            Dim value As String = NormalizeTag(tag)
            If value.Length = 0 Then Return False
            If manuscript.Tags Is Nothing Then manuscript.Tags = New List(Of String)()
            If manuscript.Tags.Contains(value, StringComparer.CurrentCultureIgnoreCase) Then Return False
            manuscript.Tags.Add(value)
            Return True
        End Function


        Public Shared Function HasTag(manuscript As Manuscript, tag As String) As Boolean
            Return manuscript?.Tags IsNot Nothing AndAlso manuscript.Tags.Contains(NormalizeTag(tag), StringComparer.CurrentCultureIgnoreCase)
        End Function


        ' Every tag in the library, most used first, for suggestions and filters.
        Public Shared Function AllTags(manuscripts As IEnumerable(Of Manuscript)) As List(Of String)
            Return manuscripts.Where(Function(item) item?.Tags IsNot Nothing).
                SelectMany(Function(item) item.Tags).
                GroupBy(Function(tag) tag, StringComparer.CurrentCultureIgnoreCase).
                OrderByDescending(Function(group) group.Count()).
                ThenBy(Function(group) group.Key, StringComparer.CurrentCultureIgnoreCase).
                Select(Function(group) group.First()).ToList()
        End Function


        Public Shared Sub NormalizeAndValidateManuscript(manuscript As Manuscript)

            If manuscript Is Nothing Then Throw New ArgumentNullException(NameOf(manuscript))
            If Not [Enum].IsDefined(manuscript.WorkType) Then
                Throw New InvalidDataException("The manuscript library contains an unsupported work type.")
            End If

            Dim tags As New List(Of String)()
            For Each tag As String In If(manuscript.Tags, New List(Of String)())
                Dim value As String = NormalizeTag(tag)
                If value.Length > 0 AndAlso Not tags.Contains(value, StringComparer.CurrentCultureIgnoreCase) Then tags.Add(value)
            Next
            manuscript.Tags = tags

        End Sub


        Public Shared Sub NormalizeTagColors(library As AuthorLibraryData)

            Dim colors As New List(Of TagColor)()
            For Each entry As TagColor In If(library.TagColors, New List(Of TagColor)())
                If entry Is Nothing OrElse Not [Enum].IsDefined(entry.Color) Then Continue For
                Dim tag As String = NormalizeTag(entry.Tag)
                If tag.Length = 0 Then Continue For
                colors.RemoveAll(Function(existing) String.Equals(existing.Tag, tag, StringComparison.CurrentCultureIgnoreCase))
                colors.Add(New TagColor With {.Tag = tag, .Color = entry.Color})
            Next
            library.TagColors = colors

        End Sub


        ' The chosen color, or one from the tag's name that never changes
        ' between runs or computers.
        Public Shared Function ColorOf(tag As String, library As AuthorLibraryData) As TagPalette

            Dim value As String = NormalizeTag(tag)
            Dim chosen As TagColor = If(library?.TagColors, New List(Of TagColor)()).
                FirstOrDefault(Function(entry) entry IsNot Nothing AndAlso String.Equals(entry.Tag, value, StringComparison.CurrentCultureIgnoreCase))
            If chosen IsNot Nothing Then Return chosen.Color

            Dim hash As UInteger = 2166136261UI
            For Each character As Char In value.ToUpperInvariant()
                hash = CUInt((CULng(hash Xor AscW(character)) * 16777619UL) And &HFFFFFFFFUL)
            Next
            Return CType(CInt(hash Mod CUInt([Enum].GetValues(GetType(TagPalette)).Length)), TagPalette)

        End Function


        Public Shared Sub SetColor(library As AuthorLibraryData, tag As String, color As TagPalette)
            Dim value As String = NormalizeTag(tag)
            If value.Length = 0 Then Return
            If library.TagColors Is Nothing Then library.TagColors = New List(Of TagColor)()
            library.TagColors.RemoveAll(Function(entry) entry Is Nothing OrElse String.Equals(entry.Tag, value, StringComparison.CurrentCultureIgnoreCase))
            library.TagColors.Add(New TagColor With {.Tag = value, .Color = color})
        End Sub


        ' Background and text colors with at least 4.5:1 contrast in both themes.
        Public Shared Function Background(color As TagPalette, dark As Boolean) As Color
            Select Case color
                Case TagPalette.Blue : Return If(dark, Drawing.Color.FromArgb(30, 58, 138), Drawing.Color.FromArgb(219, 234, 254))
                Case TagPalette.Violet : Return If(dark, Drawing.Color.FromArgb(76, 29, 149), Drawing.Color.FromArgb(237, 233, 254))
                Case TagPalette.Rose : Return If(dark, Drawing.Color.FromArgb(136, 19, 55), Drawing.Color.FromArgb(255, 228, 230))
                Case TagPalette.Amber : Return If(dark, Drawing.Color.FromArgb(120, 53, 15), Drawing.Color.FromArgb(254, 243, 199))
                Case TagPalette.Green : Return If(dark, Drawing.Color.FromArgb(20, 83, 45), Drawing.Color.FromArgb(220, 252, 231))
                Case TagPalette.Slate : Return If(dark, Drawing.Color.FromArgb(51, 65, 85), Drawing.Color.FromArgb(226, 232, 240))
                Case TagPalette.Orange : Return If(dark, Drawing.Color.FromArgb(124, 45, 18), Drawing.Color.FromArgb(255, 237, 213))
                Case Else : Return If(dark, Drawing.Color.FromArgb(19, 78, 74), Drawing.Color.FromArgb(204, 251, 241))
            End Select
        End Function


        Public Shared Function Foreground(color As TagPalette, dark As Boolean) As Color
            Select Case color
                Case TagPalette.Blue : Return If(dark, Drawing.Color.FromArgb(191, 219, 254), Drawing.Color.FromArgb(30, 64, 175))
                Case TagPalette.Violet : Return If(dark, Drawing.Color.FromArgb(221, 214, 254), Drawing.Color.FromArgb(91, 33, 182))
                Case TagPalette.Rose : Return If(dark, Drawing.Color.FromArgb(254, 205, 211), Drawing.Color.FromArgb(159, 18, 57))
                Case TagPalette.Amber : Return If(dark, Drawing.Color.FromArgb(253, 230, 138), Drawing.Color.FromArgb(146, 64, 14))
                Case TagPalette.Green : Return If(dark, Drawing.Color.FromArgb(187, 247, 208), Drawing.Color.FromArgb(22, 101, 52))
                Case TagPalette.Slate : Return If(dark, Drawing.Color.FromArgb(226, 232, 240), Drawing.Color.FromArgb(51, 65, 85))
                Case TagPalette.Orange : Return If(dark, Drawing.Color.FromArgb(254, 215, 170), Drawing.Color.FromArgb(154, 52, 18))
                Case Else : Return If(dark, Drawing.Color.FromArgb(153, 246, 228), Drawing.Color.FromArgb(17, 94, 89))
            End Select
        End Function


        ' ---------------------------------------------------------------
        ' Imports say what a record is; these map their words to a type.
        ' ---------------------------------------------------------------

        Public Shared Function FromBibTeX(entryType As String) As WorkType
            Select Case If(entryType, String.Empty).Trim().ToLowerInvariant()
                Case "article" : Return WorkType.JournalArticle
                Case "inproceedings", "conference", "proceedings" : Return WorkType.ConferencePaper
                Case "incollection", "inbook" : Return WorkType.BookChapter
                Case "phdthesis", "mastersthesis", "thesis" : Return WorkType.Thesis
                Case "unpublished", "misc", "" : Return WorkType.Unspecified
                Case Else : Return WorkType.Other
            End Select
        End Function


        Public Shared Function FromRis(ty As String) As WorkType
            Select Case If(ty, String.Empty).Trim().ToUpperInvariant()
                Case "JOUR", "JFULL", "EJOUR", "MGZN" : Return WorkType.JournalArticle
                Case "CPAPER", "CONF" : Return WorkType.ConferencePaper
                Case "ABST" : Return WorkType.ConferenceAbstract
                Case "CHAP", "ECHAP" : Return WorkType.BookChapter
                Case "THES" : Return WorkType.Thesis
                Case "UNPB", "GEN", "" : Return WorkType.Unspecified
                Case Else : Return WorkType.Other
            End Select
        End Function


        ' Export: a known type names its entry; otherwise the caller's
        ' fallback, as before types existed.
        Public Shared Function ToBibTeX(type As WorkType, fallback As String) As String
            Select Case type
                Case WorkType.JournalArticle : Return "article"
                Case WorkType.ConferencePaper, WorkType.ConferenceAbstract : Return "inproceedings"
                Case WorkType.BookChapter : Return "incollection"
                Case WorkType.Thesis : Return "phdthesis"
                Case WorkType.Preprint, WorkType.Poster, WorkType.Other : Return "misc"
                Case Else : Return fallback
            End Select
        End Function


        Public Shared Function ToRis(type As WorkType, fallback As String) As String
            Select Case type
                Case WorkType.JournalArticle : Return "JOUR"
                Case WorkType.Preprint : Return "UNPB"
                Case WorkType.ConferencePaper : Return "CPAPER"
                Case WorkType.ConferenceAbstract : Return "ABST"
                Case WorkType.Poster : Return "CONF"
                Case WorkType.BookChapter : Return "CHAP"
                Case WorkType.Thesis : Return "THES"
                Case WorkType.Other : Return "GEN"
                Case Else : Return fallback
            End Select
        End Function


        ' ORCID and Crossref both use lower-case hyphenated names.
        Public Shared Function FromRegistry(type As String) As WorkType
            Select Case If(type, String.Empty).Trim().ToLowerInvariant().Replace("_", "-")
                Case "journal-article" : Return WorkType.JournalArticle
                Case "preprint", "posted-content" : Return WorkType.Preprint
                Case "conference-paper", "proceedings-article" : Return WorkType.ConferencePaper
                Case "conference-abstract" : Return WorkType.ConferenceAbstract
                Case "conference-poster", "poster" : Return WorkType.Poster
                Case "book-chapter" : Return WorkType.BookChapter
                Case "dissertation-thesis", "dissertation", "thesis" : Return WorkType.Thesis
                Case "" : Return WorkType.Unspecified
                Case Else : Return WorkType.Other
            End Select
        End Function

    End Class

End Namespace
