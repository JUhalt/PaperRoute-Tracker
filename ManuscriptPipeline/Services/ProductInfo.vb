Namespace Services

    Public NotInheritable Class ProductInfo

        Private Sub New()
        End Sub

        Public Const DisplayName As String = "PaperRoute"
        Public Const ProductName As String = "PaperRoute Tracker"
        Public Const Tagline As String = "Track • Submit • Publish"
        Public Const Description As String = "Local-first, privacy-focused app for tracking academic manuscripts from draft through submission, revision, and publication."

        ' Current PaperRoute storage names.
        Public Const DataFolderName As String = "PaperRoute"
        Public Const ManagedLibraryFolderName As String = "PaperRoute Library"

        ' Visual Studio / debugger launches use an isolated development
        ' profile so schema experiments cannot touch the stable library.
        Public Const DevelopmentDataFolderName As String = "PaperRoute-Dev"
        Public Const DevelopmentManagedLibraryFolderName As String = "PaperRoute Dev Library"

        ' Legacy names are retained so the one-time migration can copy from
        ' pre-rebrand builds without deleting the user's rollback copy.
        Public Const LegacyDataFolderName As String = "ManuscriptPipeline"
        Public Const LegacyManagedLibraryFolderName As String = "ManuscriptPipeline Library"

        ' Development-profile legacy roots are deliberately separate from
        ' the user's production legacy roots as well.
        Public Const DevelopmentLegacyDataFolderName As String = "ManuscriptPipeline-Dev"
        Public Const DevelopmentLegacyManagedLibraryFolderName As String = "ManuscriptPipeline Dev Library"

    End Class

End Namespace
