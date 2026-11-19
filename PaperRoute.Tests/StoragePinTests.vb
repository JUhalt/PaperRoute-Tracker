Imports System
Imports System.IO
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports ManuscriptPipeline.Services

' Where a Windows user's library is looked for, held with literal names so
' that core stage 2 (v1.1) cannot move it (design risk R1). Each test reads
' the default roots with no RootOverride and creates nothing.
<TestClass>
<DoNotParallelize>
Public Class StoragePinTests

    Private _rootOverride As String
    Private _defaultRootUses As Integer


    <TestInitialize>
    Public Sub Initialize()

        _rootOverride = StorageEnvironment.RootOverride
        _defaultRootUses = StorageEnvironment.DefaultRootUses
        StorageEnvironment.RootOverride = Nothing

    End Sub


    ' Reading a default root counts in DefaultRootUses, which the view
    ' catalog expects to stay where it was.
    <TestCleanup>
    Public Sub Cleanup()

        StorageEnvironment.RootOverride = _rootOverride
        StorageEnvironment.DefaultRootUses = _defaultRootUses

    End Sub


    ' Under a debugger or with PAPERROUTE_DEV_STORAGE set, the development
    ' profile answers instead, so the test holds that profile's name then.
    <TestMethod>
    Public Sub DefaultDataRootIsLocalAppDataPaperRoute()

        Dim folderName As String =
            If(StorageEnvironment.IsDevelopmentProfile(), "PaperRoute-Dev", "PaperRoute")

        Assert.AreEqual(
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                folderName
            ),
            StorageMigrationService.CurrentDataRoot()
        )

    End Sub


    <TestMethod>
    Public Sub DefaultManagedRootIsDocumentsPaperRouteLibrary()

        Dim folderName As String =
            If(StorageEnvironment.IsDevelopmentProfile(), "PaperRoute Dev Library", "PaperRoute Library")

        Assert.AreEqual(
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                folderName
            ),
            StorageMigrationService.CurrentManagedLibraryRoot()
        )

    End Sub


    <TestMethod>
    Public Sub DevelopmentNamesAreUnchanged()

        Assert.AreEqual("PaperRoute-Dev", ProductInfo.DevelopmentDataFolderName)
        Assert.AreEqual("PaperRoute Dev Library", ProductInfo.DevelopmentManagedLibraryFolderName)

        ' The one-time migration still reads the pre-rebrand folders.
        Assert.AreEqual("ManuscriptPipeline", ProductInfo.LegacyDataFolderName)
        Assert.AreEqual("ManuscriptPipeline Library", ProductInfo.LegacyManagedLibraryFolderName)
        Assert.AreEqual("ManuscriptPipeline-Dev", ProductInfo.DevelopmentLegacyDataFolderName)
        Assert.AreEqual("ManuscriptPipeline Dev Library", ProductInfo.DevelopmentLegacyManagedLibraryFolderName)

    End Sub

End Class
