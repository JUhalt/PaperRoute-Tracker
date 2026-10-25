Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.Linq
Imports System.Text
Imports System.Text.RegularExpressions
Imports Microsoft.VisualStudio.TestTools.UnitTesting

' Every delete or remove question defaults to No (#223), so one Enter
' removes nothing, and none says a file is deleted that PaperRoute in
' fact moves to the removed folder. A sweep of the app's source, in the
' style of DialogScalingTests: a message box is read where it is written,
' as the tests answer the boxes they meet without pressing Enter.
<TestClass>
Public Class DeleteQuestionTests


    ' The shared sentence a question that removes a managed file uses.
    Private Const SharedSentence As String =
        "ManagedLibraryService.MovesToRemovedOnSave"


    ' Every MessageBox.Show whose title literal starts with "Delete" or
    ' "Remove" passes MessageBoxDefaultButton.Button2. There are 12 today,
    ' among them Delete Manuscript Version? and Remove Reviewer Comment,
    ' which defaulted to No already.
    <TestMethod>
    Public Sub EveryDeleteOrRemoveQuestionDefaultsToNo()

        Dim questions As List(Of MessageBoxCall) =
            DeleteAndRemoveQuestions()

        Dim titles As String =
            String.Join(", ", questions.Select(Function(question) question.Title))

        Assert.IsTrue(questions.Count >= 12, "The delete and remove questions are found: " & titles)

        For Each title As String In {"Delete Manuscript Version?", "Remove Reviewer Comment", "Delete Journal Submission"}
            Assert.IsTrue(questions.Any(Function(question) question.Title = title), title & " is among them: " & titles)
        Next

        Dim yesByDefault As List(Of String) =
            questions.
                Where(Function(question) Not question.Arguments.Contains("MessageBoxDefaultButton.Button2")).
                Select(Function(question) question.File & ": " & question.Title).
                ToList()

        Assert.AreEqual(0, yesByDefault.Count, "These default to Yes: " & String.Join("; ", yesByDefault))

    End Sub


    ' No string literal in the app says a file is staged for deletion:
    ' what PaperRoute no longer needs is moved to the removed folder,
    ' never deleted (ManagedLibraryService.MoveToRemoved).
    <TestMethod>
    Public Sub NoStringSaysAMovedFileIsDeleted()

        Dim found As List(Of String) =
            SourceFiles().
                SelectMany(
                    Function(source)
                        Return StringLiterals(File.ReadAllText(source)).
                            Where(Function(literal) literal.Contains("physical deletion", StringComparison.OrdinalIgnoreCase) OrElse literal.Contains("staged for deletion", StringComparison.OrdinalIgnoreCase)).
                            Select(Function(literal) Path.GetFileName(source) & ": " & literal)
                    End Function).
                ToList()

        Assert.AreEqual(0, found.Count, String.Join(Environment.NewLine, found))

    End Sub


    ' The three questions that remove a file PaperRoute keeps, a packet's,
    ' a packet file's and a version's, say what happens to it with the
    ' one shared sentence.
    <TestMethod>
    Public Sub TheFileBearingQuestionsUseTheSharedSentence()

        Dim questions As List(Of MessageBoxCall) =
            DeleteAndRemoveQuestions()

        For Each title As String In {"Delete Submission Packet", "Remove Packet File", "Delete Manuscript Version?"}

            Dim question As MessageBoxCall =
                questions.Single(Function(item) item.Title = title)

            Assert.IsTrue(question.Method.Contains(SharedSentence, StringComparison.Ordinal), title & " (" & question.File & ") uses " & SharedSentence & ".")

        Next

    End Sub


    ' A MessageBox.Show call as written: its file, the title literal, its
    ' top-level arguments and the method it is in.
    Private NotInheritable Class MessageBoxCall
        Public File As String
        Public Title As String
        Public Arguments As List(Of String)
        Public Method As String
    End Class


    Private Shared Function DeleteAndRemoveQuestions() As List(Of MessageBoxCall)

        Return SourceFiles().
            SelectMany(AddressOf MessageBoxCalls).
            Where(Function(item) item.Title.StartsWith("Delete", StringComparison.Ordinal) OrElse item.Title.StartsWith("Remove", StringComparison.Ordinal)).
            ToList()

    End Function


    Private Shared Function SourceFiles() As List(Of String)

        Dim app As String =
            Path.Combine(AssistantCoreTests.RepositoryRoot(), "ManuscriptPipeline")

        Return Directory.GetFiles(app, "*.vb", SearchOption.AllDirectories).
            Where(Function(source) Not source.Contains("\obj\", StringComparison.OrdinalIgnoreCase) AndAlso Not source.Contains("\bin\", StringComparison.OrdinalIgnoreCase)).
            ToList()

    End Function


    ' Each call's title is the argument before its MessageBoxButtons, or
    ' its last argument when it names no buttons.
    Private Shared Iterator Function MessageBoxCalls(source As String) As IEnumerable(Of MessageBoxCall)

        Const Opening As String = "MessageBox.Show("

        Dim code As String =
            WithoutComments(File.ReadAllText(source))

        Dim at As Integer =
            code.IndexOf(Opening, StringComparison.Ordinal)

        While at >= 0

            Dim arguments As List(Of String) =
                TopLevelArguments(code, at + Opening.Length)

            Dim buttons As Integer =
                arguments.FindIndex(Function(argument) argument.StartsWith("MessageBoxButtons.", StringComparison.Ordinal))

            Dim title As String =
                If(buttons > 0, arguments(buttons - 1), If(arguments.Count >= 2, arguments.Last(), String.Empty))

            Yield New MessageBoxCall With {
                .File = Path.GetFileName(source),
                .Title = LeadingLiteral(title),
                .Arguments = arguments,
                .Method = EnclosingMethod(code, at)
            }

            at = code.IndexOf(Opening, at + Opening.Length, StringComparison.Ordinal)

        End While

    End Function


    ' The arguments from just after a call's opening parenthesis to its
    ' closing one, split at the commas outside parentheses, braces and
    ' string literals.
    Private Shared Function TopLevelArguments(code As String, start As Integer) As List(Of String)

        Dim arguments As New List(Of String)()
        Dim current As New StringBuilder()
        Dim depth As Integer = 0
        Dim inString As Boolean = False

        For i As Integer = start To code.Length - 1

            Dim c As Char = code(i)

            If inString Then
                inString = c <> """"c
            ElseIf c = """"c Then
                inString = True
            ElseIf c = "("c OrElse c = "{"c Then
                depth += 1
            ElseIf (c = ")"c OrElse c = "}"c) AndAlso depth > 0 Then
                depth -= 1
            ElseIf c = ")"c Then
                arguments.Add(current.ToString().Trim())
                Return arguments
            ElseIf c = ","c AndAlso depth = 0 Then
                arguments.Add(current.ToString().Trim())
                current.Clear()
                Continue For
            End If

            current.Append(c)

        Next

        Throw New InvalidDataException("A MessageBox.Show call is not closed.")

    End Function


    ' The text of the string literal an argument starts with, if it does.
    Private Shared Function LeadingLiteral(argument As String) As String

        Dim literal As Match =
            Regex.Match(argument, "^""((?:[^""]|"""")*)""")

        Return If(literal.Success, literal.Groups(1).Value.Replace("""""", """"), String.Empty)

    End Function


    ' From the last Sub or Function declared before a position to the
    ' first End Sub or End Function after it.
    Private Shared Function EnclosingMethod(code As String, at As Integer) As String

        Dim declarations As MatchCollection =
            Regex.Matches(code.Substring(0, at), "^[ \t]*(?:(?:Private|Public|Friend|Protected|Shared|Overrides|Overridable|Async|Iterator)\s+)*(?:Sub|Function)\s+\w+", RegexOptions.Multiline)

        Dim begin As Integer =
            If(declarations.Count = 0, 0, declarations(declarations.Count - 1).Index)

        Dim finish As Match =
            Regex.Match(code.Substring(at), "^[ \t]*End (?:Sub|Function)\b", RegexOptions.Multiline)

        Return code.Substring(begin, at - begin + If(finish.Success, finish.Index, code.Length - at))

    End Function


    Private Shared Function StringLiterals(code As String) As List(Of String)

        Return Regex.Matches(WithoutComments(code), """(?:[^""]|"""")*""").
            Select(Function(literal) literal.Value.Substring(1, literal.Length - 2).Replace("""""", """")).
            ToList()

    End Function


    ' The source with every comment blanked, so what a comment says is
    ' never read as code; string literals stay as they are.
    Private Shared Function WithoutComments(code As String) As String

        Dim result As New StringBuilder(code)
        Dim inString As Boolean = False
        Dim i As Integer = 0

        While i < code.Length

            Dim c As Char = code(i)

            If inString Then
                inString = c <> """"c
            ElseIf c = """"c Then
                inString = True
            ElseIf c = "'"c Then
                While i < code.Length AndAlso code(i) <> ControlChars.Cr AndAlso code(i) <> ControlChars.Lf
                    result(i) = " "c
                    i += 1
                End While
                Continue While
            End If

            i += 1

        End While

        Return result.ToString()

    End Function

End Class
