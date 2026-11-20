Imports System
Imports System.IO
Imports System.Linq
Imports System.Reflection
Imports System.Xml.Linq
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline.Services

' The version a backup or packet manifest records, and the one the
' User-Agent sends, is PaperRoute's own: the app project's Version. The
' services read it from their own assembly, which is the app today; when
' they move into a library of their own (v1.1), a library built with
' another version fails here, compared with the app's project and with the
' app's assembly through a type that stays in the app.
<TestClass>
Public Class VersionSourceTests

    <TestMethod>
    Public Sub ManifestsAndTheUserAgentReportTheAppsVersion()

        Dim version As String =
            ProjectProperty("Version")

        Dim app As Assembly =
            GetType(Global.ManuscriptPipeline.Form1).Assembly

        Assert.AreEqual(
            "PaperRouteTracker",
            app.GetName().Name,
            "Form1 is in the app's assembly."
        )

        Assert.AreEqual(
            version,
            WithoutMetadata(app.GetCustomAttribute(Of AssemblyInformationalVersionAttribute)().InformationalVersion),
            "The app's assembly carries the project's Version."
        )

        Assert.AreEqual(
            version,
            PacketExportService.AppVersion(),
            "Backup and packet manifests record the app's version."
        )

        Assert.AreEqual(
            "PaperRoute-Tracker/" & version & " (+https://github.com/JUhalt/PaperRoute-Tracker)",
            OnlineAccess.UserAgent(),
            "The User-Agent names the app's version."
        )

    End Sub


    <TestMethod>
    Public Sub TheServicesAssemblyCarriesTheAppsVersionNumbers()

        Dim assemblyVersion As New Version(
            ProjectProperty("AssemblyVersion")
        )

        Dim fileVersion As String =
            ProjectProperty("FileVersion")

        Dim app As Assembly =
            GetType(Global.ManuscriptPipeline.Form1).Assembly

        ' The app, the core library and the platform library (core stage 2):
        ' every DLL the app ships carries the app's numbers.
        For Each owner As Assembly In {app, GetType(PacketExportService).Assembly, GetType(OnlineAccess).Assembly, GetType(Global.ManuscriptPipeline.Models.Manuscript).Assembly, GetType(WindowsPlatform).Assembly}

            Assert.AreEqual(
                assemblyVersion,
                owner.GetName().Version,
                owner.GetName().Name & " has the app's AssemblyVersion."
            )

            Assert.AreEqual(
                fileVersion,
                owner.GetCustomAttribute(Of AssemblyFileVersionAttribute)().Version,
                owner.GetName().Name & " has the app's FileVersion."
            )

        Next

    End Sub


    ' The app project's own property, where the release workflow reads it.
    Private Shared Function ProjectProperty(name As String) As String

        Dim project As XDocument =
            XDocument.Load(
                Path.Combine(
                    AssistantCoreTests.RepositoryRoot(),
                    "ManuscriptPipeline",
                    "ManuscriptPipeline.vbproj"
                )
            )

        Dim value As String =
            project.Descendants(name).Single().Value.Trim()

        Assert.IsFalse(
            String.IsNullOrWhiteSpace(value),
            "The app project sets " & name & "."
        )

        Return value

    End Function


    Private Shared Function WithoutMetadata(version As String) As String

        Dim metadata As Integer =
            version.IndexOf("+"c)

        If metadata >= 0 Then
            version = version.Substring(0, metadata)
        End If

        Return version.Trim()

    End Function

End Class
