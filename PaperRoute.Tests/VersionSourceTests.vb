Imports System
Imports System.IO
Imports System.Linq
Imports System.Reflection
Imports System.Xml.Linq
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline.Services
Imports Velopack

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


    ' Master carries a 1.1 pre-release number (the maintainer's decision of
    ' November 21, 2026), so an installed 1.0.x can update to a build of
    ' master. Velopack compares versions with its own SemanticVersion, the
    ' type of a release's Version, so the order is checked with it. v1.0.1
    ' was the newest release when master moved; a 1.0.x patch is made on
    ' release/v1.0.x and must never outrank master.
    <TestMethod>
    Public Sub TheVersionOrdersAboveEvery1_0ReleaseForTheUpdater()

        Dim text As String =
            ProjectProperty("Version")

        Dim version As SemanticVersion =
            SemanticVersion.Parse(text)

        Assert.IsTrue(
            version > SemanticVersion.Parse("1.0.1"),
            text & " must order above v1.0.1 for the updater."
        )

        Assert.IsTrue(
            version.Major > 1 OrElse version.Minor >= 1,
            text & " must order above every 1.0.x patch."
        )

        ' release.yml and Build-ReleaseCandidate.ps1 require AssemblyVersion
        ' and FileVersion to be the Version without its label, plus ".0";
        ' System.Version cannot read a label, so these carry none.
        Dim numeric As String =
            version.Major & "." & version.Minor & "." & version.Patch & ".0"

        Assert.AreEqual(numeric, ProjectProperty("AssemblyVersion"))
        Assert.AreEqual(numeric, ProjectProperty("FileVersion"))

        ' The core and platform projects write the same three nodes; the
        ' release workflow checks the core's, and nothing else checks the
        ' platform's Version.
        For Each library As String In {"PaperRoute.Core", "PaperRoute.Platform"}
            For Each name As String In {"Version", "AssemblyVersion", "FileVersion"}

                Assert.AreEqual(
                    ProjectProperty(name),
                    ProjectProperty(name, library),
                    library & ".vbproj has the app's " & name & "."
                )

            Next
        Next

    End Sub


    ' The app project's own property, where the release workflow reads it,
    ' or the same property of another project in the repository.
    Private Shared Function ProjectProperty(
        name As String,
        Optional projectName As String = "ManuscriptPipeline"
    ) As String

        Dim project As XDocument =
            XDocument.Load(
                Path.Combine(
                    TestRepository.Root(),
                    projectName,
                    projectName & ".vbproj"
                )
            )

        Dim value As String =
            project.Descendants(name).Single().Value.Trim()

        Assert.IsFalse(
            String.IsNullOrWhiteSpace(value),
            projectName & ".vbproj sets " & name & "."
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
