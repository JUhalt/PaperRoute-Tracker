namespace PaperRoute.ViewModels.Session;

// What a library save ended in (shell design 2.6 rule 1).
public enum SaveOutcomeKind
{
    // The library was written. Warning names a managed folder a file in use
    // kept in place (ManuscriptRepository.LastSaveWarning), or is empty.
    Saved,

    // Nothing was written, and nothing failed: the library may not be saved
    // (a snapshot, or a kind not allowed to), it has not been read, or other
    // library work runs.
    Refused,

    // The save threw; Message is the exception's message.
    Failed,
}

public sealed record SaveOutcome(SaveOutcomeKind Kind, string Warning, string Message)
{
    public static SaveOutcome Refused { get; } = new(SaveOutcomeKind.Refused, string.Empty, string.Empty);

    public static SaveOutcome Saved(string? warning) => new(SaveOutcomeKind.Saved, warning ?? string.Empty, string.Empty);

    public static SaveOutcome Failed(string message) => new(SaveOutcomeKind.Failed, string.Empty, message);
}
