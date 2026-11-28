Imports System
Imports System.Collections
Imports System.Collections.Generic
Imports System.Globalization
Imports System.Linq
Imports System.Reflection
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


    ' What the clone leaves out on purpose, by path, each with its reason. A
    ' path named here is still filled, and the walk checks every entry: it
    ' names one whose value the clone keeps now, one whose list or dictionary
    ' the clone leaves nothing instead of empty, and one no property has (a
    ' rename or a removal), so the list cannot go stale.
    Private Shared ReadOnly LeftOutOnPurpose As New Dictionary(Of String, String)(StringComparer.Ordinal) From {
        {"Manuscript.PublicationMatches",
         "the clone leaves a manuscript's publication matches empty. No window that edits a clone edits them: " &
         "the publication check changes the library's own records. Today's manuscript window copies its fields " &
         "back into the library's record and leaves the matches as the library holds them, and the new manuscript " &
         "page gives what it saves the record's matches in a list of its own (WorkingCopy.BuildCandidate, build 9)."}
    }


    ' Every settable property of a manuscript, nested ones included, survives
    ' the clone (#464). The record is filled by reflection, each property with
    ' a value of its own that a new object does not have, so a property added
    ' to the model later is covered without anyone remembering it, and one the
    ' clone forgets, or copies from another property, is named by its path.
    <TestMethod>
    Public Sub EverySettablePropertySurvivesTheClone()

        Dim source As Manuscript =
            DirectCast(New CloneProbe().Fill(GetType(Manuscript), "Manuscript"), Manuscript)

        Dim clone As Manuscript =
            ManuscriptCloneService.CloneManuscript(source)

        Dim lost As List(Of String) =
            FindCloneLosses(source, clone, LeftOutOnPurpose)

        Assert.AreEqual(
            0,
            lost.Count,
            "The clone loses what a person entered, at every save of a page that edits a clone. " &
            "Copy each of these in ManuscriptCloneService, or name it in LeftOutOnPurpose with its reason " &
            "(or mend the list, where a line names an entry of it):" &
            Environment.NewLine & String.Join(Environment.NewLine, lost)
        )

    End Sub


    ' The walk's check of the exemptions themselves: an entry whose value the
    ' clone copies is named as stale, the way the reviewer's probe added
    ' CoAuthors, which the clone keeps.
    <TestMethod>
    Public Sub AnExemptionTheCloneKeepsIsNamed()

        Dim source As Manuscript =
            DirectCast(New CloneProbe().Fill(GetType(Manuscript), "Manuscript"), Manuscript)

        Dim leftOut As New Dictionary(Of String, String)(LeftOutOnPurpose, StringComparer.Ordinal) From {
            {"Manuscript.CoAuthors", "an exemption the test adds."}
        }

        Dim lost As List(Of String) =
            FindCloneLosses(source, ManuscriptCloneService.CloneManuscript(source), leftOut)

        Assert.AreEqual(1, lost.Count, String.Join(Environment.NewLine, lost))
        StringAssert.StartsWith(
            lost(0),
            "Manuscript.CoAuthors: listed in LeftOutOnPurpose, but the clone keeps it now.")
        StringAssert.Contains(lost(0), "an exemption the test adds.")

        Dim keeping As Manuscript = ManuscriptCloneService.CloneManuscript(source)
        keeping.PublicationMatches = source.PublicationMatches

        lost = FindCloneLosses(source, keeping, LeftOutOnPurpose)

        Assert.AreEqual(1, lost.Count, String.Join(Environment.NewLine, lost))
        StringAssert.StartsWith(
            lost(0),
            "Manuscript.PublicationMatches: listed in LeftOutOnPurpose, but the clone keeps it now.")

    End Sub


    ' An entry no property has, after a rename or a removal, is named too.
    <TestMethod>
    Public Sub AnExemptionNoPropertyHasIsNamed()

        Dim source As Manuscript =
            DirectCast(New CloneProbe().Fill(GetType(Manuscript), "Manuscript"), Manuscript)

        Dim leftOut As New Dictionary(Of String, String)(LeftOutOnPurpose, StringComparer.Ordinal) From {
            {"Manuscript.NoSuchProperty", "an exemption the test adds."}
        }

        Dim lost As List(Of String) =
            FindCloneLosses(source, ManuscriptCloneService.CloneManuscript(source), leftOut)

        Assert.AreEqual(1, lost.Count, String.Join(Environment.NewLine, lost))
        StringAssert.StartsWith(
            lost(0),
            "Manuscript.NoSuchProperty: listed in LeftOutOnPurpose, but no property has this path.")

    End Sub


    ' A list the clone leaves out must be empty: a clone that leaves it
    ' nothing is named, where reading its count would have thrown.
    <TestMethod>
    Public Sub AListLeftOutAsNothingIsNamed()

        Dim source As Manuscript =
            DirectCast(New CloneProbe().Fill(GetType(Manuscript), "Manuscript"), Manuscript)

        Dim clone As Manuscript = ManuscriptCloneService.CloneManuscript(source)
        clone.PublicationMatches = Nothing

        Dim lost As List(Of String) =
            FindCloneLosses(source, clone, LeftOutOnPurpose)

        Assert.AreEqual(1, lost.Count, String.Join(Environment.NewLine, lost))
        StringAssert.StartsWith(
            lost(0),
            "Manuscript.PublicationMatches: listed in LeftOutOnPurpose, and the clone leaves it nothing")

    End Sub


    ' Walks what the probe filled and what the clone made side by side, and
    ' returns a line for every value the clone does not have and for every
    ' entry of leftOut that has gone stale.
    Private Shared Function FindCloneLosses(
        source As Manuscript,
        clone As Manuscript,
        leftOut As IReadOnlyDictionary(Of String, String)
    ) As List(Of String)

        Dim walk As New CloneWalk(leftOut)
        walk.Compare(GetType(Manuscript), source, clone, "Manuscript", walk.Lost)

        For Each path As String In leftOut.Keys.OrderBy(Function(item) item, StringComparer.Ordinal)
            If Not walk.Visited.Contains(path) Then
                walk.Lost.Add(
                    $"{path}: listed in LeftOutOnPurpose, but no property has this path. " &
                    $"Mend or remove the entry. Its reason was: {leftOut(path)}")
            End If
        Next

        Return walk.Lost

    End Function


    Private NotInheritable Class CloneWalk

        Private ReadOnly _leftOut As IReadOnlyDictionary(Of String, String)

        Public ReadOnly Lost As New List(Of String)()
        Public ReadOnly Visited As New HashSet(Of String)(StringComparer.Ordinal)

        Public Sub New(leftOut As IReadOnlyDictionary(Of String, String))
            _leftOut = leftOut
        End Sub


        ' Adds to differences a line for every value the clone does not have.
        ' A path in leftOut is checked the other way round: the clone must
        ' not have its value, and what is wrong with an entry goes to Lost.
        Public Sub Compare(
            declared As Type,
            expected As Object,
            actual As Object,
            path As String,
            differences As List(Of String)
        )

            If _leftOut.ContainsKey(path) Then
                Visited.Add(path)
                If actual Is Nothing AndAlso (TypeOf expected Is IList OrElse TypeOf expected Is IDictionary) Then
                    Lost.Add(
                        $"{path}: listed in LeftOutOnPurpose, and the clone leaves it nothing; " &
                        "what the clone leaves out of a list or a dictionary must be left empty.")
                    Return
                End If
                Dim kept As New List(Of String)()
                CompareValues(declared, expected, actual, path, kept)
                If kept.Count = 0 Then
                    Lost.Add(
                        $"{path}: listed in LeftOutOnPurpose, but the clone keeps it now. Take it out of the " &
                        $"list, and revisit the save paths built on the old rule. Its reason was: {_leftOut(path)}")
                End If
                Return
            End If

            CompareValues(declared, expected, actual, path, differences)

        End Sub


        Private Sub CompareValues(
            declared As Type,
            expected As Object,
            actual As Object,
            path As String,
            lost As List(Of String)
        )

            If expected Is Nothing OrElse actual Is Nothing Then
                If expected IsNot Nothing OrElse actual IsNot Nothing Then
                    lost.Add($"{path}: {DescribeForClone(expected)}, and the clone has {DescribeForClone(actual)}")
                End If
                Return
            End If

            Dim target As Type = If(Nullable.GetUnderlyingType(declared), declared)

            If IsCloneScalar(target) Then
                If Not expected.Equals(actual) Then
                    lost.Add($"{path}: {DescribeForClone(expected)}, and the clone has {DescribeForClone(actual)}")
                End If
                Return
            End If

            Dim expectedMap As IDictionary = TryCast(expected, IDictionary)
            If expectedMap IsNot Nothing Then
                Dim actualMap As IDictionary = DirectCast(actual, IDictionary)
                Dim valueType As Type = target.GetGenericArguments()(1)
                For Each key As Object In expectedMap.Keys
                    If actualMap.Contains(key) Then
                        Compare(valueType, expectedMap(key), actualMap(key), $"{path}[{DescribeForClone(key)}]", lost)
                    Else
                        lost.Add($"{path}[{DescribeForClone(key)}]: missing from the clone")
                    End If
                Next
                If actualMap.Count <> expectedMap.Count Then
                    lost.Add($"{path}: {expectedMap.Count} entries, and the clone has {actualMap.Count}")
                End If
                Return
            End If

            Dim expectedList As IList = TryCast(expected, IList)
            If expectedList IsNot Nothing Then
                Dim actualList As IList = DirectCast(actual, IList)
                Dim itemType As Type = target.GetGenericArguments()(0)
                If actualList.Count <> expectedList.Count Then
                    lost.Add($"{path}: {expectedList.Count} items, and the clone has {actualList.Count}")
                    Return
                End If
                For index As Integer = 0 To expectedList.Count - 1
                    Compare(itemType, expectedList(index), actualList(index), $"{path}[{index}]", lost)
                Next
                Return
            End If

            For Each item As PropertyInfo In SettableForClone(target)
                Compare(item.PropertyType, item.GetValue(expected), item.GetValue(actual), $"{path}.{item.Name}", lost)
            Next

        End Sub

    End Class


    ' The public properties a caller can set, in a fixed order.
    Private Shared Function SettableForClone(target As Type) As IEnumerable(Of PropertyInfo)

        Return target.GetProperties(BindingFlags.Public Or BindingFlags.Instance).
            Where(Function(item) item.GetSetMethod() IsNot Nothing AndAlso item.GetIndexParameters().Length = 0).
            OrderBy(Function(item) item.Name, StringComparer.Ordinal)

    End Function


    Private Shared Function IsCloneScalar(target As Type) As Boolean

        Return target.IsPrimitive OrElse target.IsEnum OrElse target Is GetType(String) OrElse
            target Is GetType(Guid) OrElse target Is GetType(DateTime) OrElse target Is GetType(Decimal)

    End Function


    Private Shared Function DescribeForClone(value As Object) As String

        If value Is Nothing Then Return "nothing"
        If TypeOf value Is String Then Return """" & CStr(value) & """"
        If TypeOf value Is DateTime Then Return DirectCast(value, DateTime).ToString("o", CultureInfo.InvariantCulture)
        Dim formattable As IFormattable = TryCast(value, IFormattable)
        If formattable IsNot Nothing Then Return formattable.ToString(Nothing, CultureInfo.InvariantCulture)
        Return value.ToString()

    End Function


    ' Fills a model object by reflection: every settable property gets a value
    ' no other property has and a new object does not have, every list two
    ' items and every dictionary two entries, filled the same way. A type it
    ' does not know stops the test, so a new kind of property cannot pass
    ' unchecked.
    Private NotInheritable Class CloneProbe

        Private _next As Integer

        Public Function Fill(target As Type, path As String) As Object

            Dim filled As Object = Activator.CreateInstance(target)
            Dim fresh As Object = Activator.CreateInstance(target)

            For Each item As PropertyInfo In SettableForClone(target)
                item.SetValue(filled, MakeValue(item.PropertyType, $"{path}.{item.Name}", item.GetValue(fresh)))
            Next

            Return filled

        End Function


        Private Function MakeValue(declared As Type, path As String, freshValue As Object) As Object

            Dim target As Type = If(Nullable.GetUnderlyingType(declared), declared)
            _next += 1

            If IsCloneScalar(target) Then
                Dim value As Object = MakeScalar(target, path, freshValue)
                If value.Equals(freshValue) Then
                    Throw New InvalidOperationException($"The probe gave {path} the value a new object has; give it another.")
                End If
                Return value
            End If

            If target.IsGenericType AndAlso target.GetGenericTypeDefinition() Is GetType(List(Of )) Then
                Dim list As IList = DirectCast(Activator.CreateInstance(target), IList)
                For index As Integer = 0 To 1
                    list.Add(MakeValue(target.GetGenericArguments()(0), $"{path}[{index}]", Nothing))
                Next
                Return list
            End If

            If target.IsGenericType AndAlso target.GetGenericTypeDefinition() Is GetType(Dictionary(Of ,)) Then
                Dim map As IDictionary = DirectCast(Activator.CreateInstance(target), IDictionary)
                For index As Integer = 0 To 1
                    map.Add(
                        MakeValue(target.GetGenericArguments()(0), $"{path} key {index}", Nothing),
                        MakeValue(target.GetGenericArguments()(1), $"{path}[{index}]", Nothing))
                Next
                Return map
            End If

            If target.IsClass AndAlso target.Namespace = GetType(Manuscript).Namespace AndAlso
               target.GetConstructor(Type.EmptyTypes) IsNot Nothing Then
                Return Fill(target, path)
            End If

            Throw New InvalidOperationException(
                $"The probe cannot fill {path} ({target.FullName}). Teach it the type, so the clone's test covers the property.")

        End Function


        Private Function MakeScalar(target As Type, path As String, freshValue As Object) As Object

            If target Is GetType(String) Then Return $"{path} #{_next}"
            If target Is GetType(Guid) Then Return New Guid(_next, 0S, 0S, New Byte() {&HC1, &HE, 0, 0, 0, 0, 0, 0})
            If target Is GetType(Integer) Then Return 100 + _next
            If target Is GetType(Long) Then Return 100000L + _next
            If target Is GetType(Decimal) Then Return 1000D + _next
            If target Is GetType(DateTime) Then Return New DateTime(2001, 2, 3, 4, 5, 6).AddDays(_next)
            If target Is GetType(Boolean) Then Return freshValue Is Nothing OrElse Not CBool(freshValue)

            If target.IsEnum Then
                Dim values As Array = [Enum].GetValues(target)
                For offset As Integer = 0 To values.Length - 1
                    Dim candidate As Object = values.GetValue((_next + offset) Mod values.Length)
                    If Not candidate.Equals(freshValue) Then Return candidate
                Next
            End If

            Throw New InvalidOperationException(
                $"The probe cannot fill {path} ({target.FullName}). Teach it the type, so the clone's test covers the property.")

        End Function

    End Class

End Class
