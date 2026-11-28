using System.Diagnostics.CodeAnalysis;

namespace PaperRoute.ViewModels.ManuscriptPage;

// Why a save stopped before it wrote anything (build 9's design, 2.5).
public enum SaveRefusal
{
    // Nothing stopped it: the attempt holds a candidate.
    None,

    // The title is blank (EditManuscriptForm.vb:4537-4553).
    TitleRequired,

    // The held stage needs a submission or a decision first
    // (EditManuscriptForm.vb:4568-4586); the stage stays held (D3).
    NeedsStep,
}

// What WorkingCopy.BuildCandidate gives Save: the record to write, or why
// nothing is written. Building it never changes the working copy.
public sealed class SaveAttempt
{
    private SaveAttempt(Manuscript? candidate, SaveRefusal refusal, string requirement)
    {
        Candidate = candidate;
        Refusal = refusal;
        Requirement = requirement;
    }

    // A fresh clone of the working copy with Save's rules applied; null when
    // the save was refused.
    public Manuscript? Candidate { get; }

    public SaveRefusal Refusal { get; }

    // For NeedsStep, what the held stage needs, in the core's words
    // (ManuscriptStagePolicyService.RequirementDescription), such as
    // "an active Journal Submission record"; otherwise empty.
    public string Requirement { get; }

    [MemberNotNullWhen(true, nameof(Candidate))]
    public bool IsReady => Candidate is not null;

    internal static SaveAttempt Ready(Manuscript candidate) =>
        new(candidate ?? throw new ArgumentNullException(nameof(candidate)), SaveRefusal.None, string.Empty);

    internal static SaveAttempt TitleRequired() => new(null, SaveRefusal.TitleRequired, string.Empty);

    internal static SaveAttempt NeedsStep(string requirement) => new(null, SaveRefusal.NeedsStep, requirement);
}
