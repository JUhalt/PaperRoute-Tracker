Imports System
Imports System.Collections.Generic
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline.Services

' The files that ask before they open (#117), moved into the core (core
' stage 2, step 10). The list is PaperRoute 1.0.x's, written out here as
' literals: a type added or dropped changes which files ask, on Windows
' too, and must change this test on purpose.
<TestClass>
Public Class ProgramFileRuleTests

    Private Shared ReadOnly ReleasedTypes As String() = {
        ".exe", ".com", ".scr", ".pif", ".msi", ".msp",
        ".bat", ".cmd", ".ps1", ".psm1", ".vbs", ".vbe",
        ".js", ".jse", ".wsf", ".wsh", ".hta", ".reg",
        ".lnk", ".url", ".cpl", ".jar", ".appx", ".msix"
    }


    <TestMethod>
    Public Sub TheTypesAreExactlyTheTwentyFourOf101()

        Dim types As String() = ProgramFileRule.Extensions()

        Assert.AreEqual(24, types.Length)
        CollectionAssert.AreEquivalent(ReleasedTypes, types)

    End Sub


    <TestMethod>
    Public Sub EachTypeIsAProgramInAnyCaseAndWithTrailingDotsOrSpaces()

        For Each extension As String In ReleasedTypes

            Assert.IsTrue(ProgramFileRule.IsProgramOrScript("C:\PaperRoute Library\letter" & extension), extension)
            Assert.IsTrue(ProgramFileRule.IsProgramOrScript("C:\PaperRoute Library\letter" & extension.ToUpperInvariant()), extension)
            Assert.IsTrue(ProgramFileRule.IsProgramOrScript("C:\PaperRoute Library\letter" & extension & " ."), extension)

        Next

        For Each other As String In {".docx", ".pdf", ".html", ".txt", ".xlsx", ""}
            Assert.IsFalse(ProgramFileRule.IsProgramOrScript("C:\PaperRoute Library\letter" & other), other)
        Next

        Assert.IsFalse(ProgramFileRule.IsProgramOrScript(Nothing))
        Assert.IsFalse(ProgramFileRule.IsProgramOrScript("   "))

    End Sub


    <TestMethod>
    Public Sub OpenAsksBeforeAProgramAndNotBeforeADocument()

        Dim asked As New List(Of String)()
        Dim started As New List(Of String)()

        ProgramFileRule.Open("letter.exe", Function(candidate) Answer(asked, candidate, False), AddressOf started.Add)
        CollectionAssert.AreEqual({"letter.exe"}, asked)
        Assert.AreEqual(0, started.Count, "No leaves it closed.")

        ProgramFileRule.Open("letter.exe", Function(candidate) Answer(asked, candidate, True), AddressOf started.Add)
        CollectionAssert.AreEqual({"letter.exe"}, started, "Yes opens it.")

        asked.Clear()
        started.Clear()
        ProgramFileRule.Open("letter.docx", Function(candidate) Answer(asked, candidate, False), AddressOf started.Add)
        Assert.AreEqual(0, asked.Count, "A document opens without a question.")
        CollectionAssert.AreEqual({"letter.docx"}, started)

    End Sub


    Private Shared Function Answer(asked As List(Of String), filePath As String, yes As Boolean) As Boolean
        asked.Add(filePath)
        Return yes
    End Function

End Class
