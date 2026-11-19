Imports System

Namespace Models

    Public Enum SubmissionWorkflowTarget
        Manuscript
        Readiness
        Packets
        Version
        Submission
        RecordSubmission
    End Enum

    ' Navigation only: IDs identify existing records and never manufacture events.
    Public Class SubmissionWorkflowRequest
        Public Property Target As SubmissionWorkflowTarget
        Public Property PacketId As Guid?
        Public Property ReadinessProfileId As Guid?
        Public Property VersionId As Guid?
        Public Property SubmissionId As Guid?
    End Class

End Namespace
