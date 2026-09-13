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


        Public Shared ReadOnly Property ReadinessRequirementTitle As String
            Get
                Return "The short name of the thing the journal expects, such as Anonymous manuscript, Cover letter, or Data availability statement. Do not enter Required or Optional here; PaperRoute shows that automatically."
            End Get
        End Property


        Public Shared ReadOnly Property ReadinessCategory As String
            Get
                Return "A grouping label for related requirements, such as Manuscript, Editorial, Compliance, Files, or Figures. Keep the actual requirement name in Requirement title."
            End Get
        End Property


        Public Shared ReadOnly Property ReadinessInstructions As String
            Get
                Return "Optional detail about what the journal expects for this requirement, such as a word limit, anonymization rule, upload format, or journal-specific instruction."
            End Get
        End Property


        Public Shared ReadOnly Property ReadinessImportance As String
            Get
                Return "Checked means this item is required for the manuscript to be considered ready. Unchecked means optional. PaperRoute displays Required or Optional automatically."
            End Get
        End Property


        Public Shared ReadOnly Property ReadinessNotApplicable As String
            Get
                Return "Use Not Applicable only when this journal requirement genuinely does not apply to this manuscript. It counts as resolved for readiness, but it is not counted as completed."
            End Get
        End Property


        Public Shared ReadOnly Property ReadinessTemplateRefresh As String
            Get
                Return "Adds only requirements that were newly added to the reusable journal template. Existing manuscript snapshot text, status, notes, and completion history are not rewritten."
            End Get
        End Property

    End Class

End Namespace
