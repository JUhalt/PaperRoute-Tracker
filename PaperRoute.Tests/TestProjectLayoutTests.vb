Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.Linq
Imports System.Text.RegularExpressions
Imports System.Xml.Linq
Imports Microsoft.VisualStudio.TestTools.UnitTesting

' Core stage 2 (v1.1) moves the tests that need no Windows from
' PaperRoute.Tests to PaperRoute.Core.Tests, so they run on Linux too. A
' test must still run exactly once on Windows: in one project, never in
' both, and never lost because a file in one project leans on a test class
' that went to the other.
<TestClass>
Public Class TestProjectLayoutTests

    Private Shared ReadOnly TestProjects As String() = {"PaperRoute.Tests", "PaperRoute.Core.Tests"}

    ' Test data shared by the test projects; it holds no tests.
    Private Const FixtureProject As String = "PaperRoute.TestFixtures"


    <TestMethod>
    Public Sub NoTestFileIsInBothTestProjects()

        Dim root As String = TestRepository.Root()
        Dim problems As New List(Of String)()

        ' The files each project compiles: its own folder's, and those its
        ' project file links in from elsewhere.
        Dim compiled As New Dictionary(Of String, List(Of String))(StringComparer.OrdinalIgnoreCase)

        For Each project As String In TestProjects

            Dim folder As String = Path.Combine(root, project)
            Dim files As List(Of String) = SourceFiles(folder)

            For Each linked As String In LinkedFiles(Path.Combine(folder, project & ".vbproj"))

                Dim code As String = File.ReadAllText(linked)
                If code.Contains("<TestMethod") OrElse code.Contains("<DataTestMethod") Then
                    problems.Add(project & " links " & Path.GetRelativePath(root, linked) & ", which holds test methods: they would run in both projects.")
                End If

                files.Add(linked)

            Next

            compiled(project) = files

        Next

        ' The test classes each project declares.
        Dim declared As New Dictionary(Of String, HashSet(Of String))(StringComparer.OrdinalIgnoreCase)

        For Each project As String In TestProjects
            declared(project) = New HashSet(Of String)(
                compiled(project).SelectMany(Function(source) TestClassesIn(File.ReadAllText(source))),
                StringComparer.OrdinalIgnoreCase)
            Assert.IsTrue(declared(project).Count > 0, project & " declares test classes.")
        Next

        For Each name As String In declared(TestProjects(0)).Intersect(declared(TestProjects(1)), StringComparer.OrdinalIgnoreCase)
            problems.Add("The test class " & name & " is declared in both test projects.")
        Next

        ' A file that names a test class only the other project has would stop
        ' its own project building, or tempt someone to copy the class.
        For Each project As String In TestProjects

            Dim other As String = TestProjects.Single(Function(item) Not String.Equals(item, project, StringComparison.OrdinalIgnoreCase))
            Dim othersOnly As List(Of String) =
                declared(other).Where(Function(name) Not declared(project).Contains(name)).ToList()

            For Each source As String In compiled(project)

                Dim code As String = WithoutComments(File.ReadAllText(source))

                For Each name As String In othersOnly
                    If Regex.IsMatch(code, "\b" & Regex.Escape(name) & "\b", RegexOptions.IgnoreCase) Then
                        problems.Add(Path.GetRelativePath(root, source) & " (in " & project & ") names " & name & ", a test class of " & other & ".")
                    End If
                Next

            Next

        Next

        ' The fixture project is data only.
        For Each source As String In SourceFiles(Path.Combine(root, FixtureProject))
            Dim code As String = File.ReadAllText(source)
            If code.Contains("<TestMethod") OrElse code.Contains("<DataTestMethod") OrElse TestClassesIn(code).Any() Then
                problems.Add(Path.GetRelativePath(root, source) & " holds tests; " & FixtureProject & " holds none.")
            End If
        Next

        Assert.AreEqual(0, problems.Count, String.Join(Environment.NewLine, problems))

    End Sub


    ' The .vb files under a project folder, as the SDK compiles them: not
    ' those under bin, obj or a test results folder.
    Private Shared Function SourceFiles(folder As String) As List(Of String)

        Assert.IsTrue(Directory.Exists(folder), folder & " exists.")

        Return Directory.GetFiles(folder, "*.vb", SearchOption.AllDirectories).
            Where(Function(source)
                      Dim parts As String() = Path.GetRelativePath(folder, source).Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                      Return Not parts.Any(Function(part) part.Equals("bin", StringComparison.OrdinalIgnoreCase) OrElse
                                                         part.Equals("obj", StringComparison.OrdinalIgnoreCase) OrElse
                                                         part.Equals("TestResults", StringComparison.OrdinalIgnoreCase))
                  End Function).
            ToList()

    End Function


    ' The .vb files a project file compiles from outside its own folder.
    Private Shared Function LinkedFiles(projectFile As String) As List(Of String)

        Assert.IsTrue(File.Exists(projectFile), projectFile & " exists.")

        Dim folder As String = Path.GetDirectoryName(projectFile)

        Return XDocument.Load(projectFile).Descendants().
            Where(Function(item) item.Name.LocalName = "Compile" AndAlso item.Attribute("Include") IsNot Nothing).
            Select(Function(item) Path.GetFullPath(Path.Combine(folder, item.Attribute("Include").Value.Replace("\"c, Path.DirectorySeparatorChar)))).
            Where(Function(source)
                      Assert.IsTrue(File.Exists(source), Path.GetFileName(projectFile) & " links " & source & ", which exists.")
                      Return Not source.StartsWith(folder & Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                  End Function).
            ToList()

    End Function


    ' The names of the classes in the code that carry <TestClass>.
    Private Shared Function TestClassesIn(code As String) As IEnumerable(Of String)

        Return Regex.Matches(WithoutComments(code), "<TestClass\b[^>]*>(?:\s|<[^>]*>|Public|Friend|NotInheritable|MustInherit|Partial)*\bClass\s+(\w+)", RegexOptions.IgnoreCase).
            Select(Function(found) found.Groups(1).Value).
            ToList()

    End Function


    ' The code without its whole-line comments, which may name a class that
    ' moved without using it.
    Private Shared Function WithoutComments(code As String) As String

        Return String.Join(
            vbLf,
            code.Split({vbCrLf, vbLf}, StringSplitOptions.None).
                Where(Function(line) Not line.TrimStart().StartsWith("'"c)))

    End Function

End Class
