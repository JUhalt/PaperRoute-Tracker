Namespace Services

    Public NotInheritable Class WorkflowHelpCatalog

        Private Sub New()
        End Sub


        Public Shared ReadOnly Property CurrentState As String
            Get
                Return "Where the manuscript is in the publication workflow right now, such as Submitted, Revision, Accepted, or Draft after a rejection/withdrawal."
            End Get
        End Property


        Public Shared ReadOnly Property CurrentVersion As String
            Get
                Return "Which manuscript file or snapshot you currently consider the active working version. This is separate from the manuscript's lifecycle state."
            End Get
        End Property


        Public Shared ReadOnly Property JournalManuscriptId As String
            Get
                Return "The tracking identifier assigned by the journal or publisher after submission (for example, JCS-2026-1234). This is not a PaperRoute manuscript-version label."
            End Get
        End Property


        Public Shared ReadOnly Property VersionDate As String
            Get
                Return "When this manuscript version existed in the research workflow. PaperRoute separately records when the version entry was added or edited, so later metadata changes do not move it through history."
            End Get
        End Property


        Public Shared ReadOnly Property SubmissionAssociation As String
            Get
                Return "Choose a journal submission when this exact manuscript version was the file sent with that submission."
            End Get
        End Property


        Public Shared ReadOnly Property DecisionAssociation As String
            Get
                Return "Choose an editorial decision when this version was created in response to that decision. A submitted manuscript version can correctly have no decision association."
            End Get
        End Property


        Public Shared ReadOnly Property RevisionRound As String
            Get
                Return "Use a round number for revised manuscript versions created while responding to editorial or reviewer feedback within the same journal submission attempt."
            End Get
        End Property

    End Class

End Namespace
