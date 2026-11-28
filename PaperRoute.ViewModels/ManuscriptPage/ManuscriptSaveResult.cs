namespace PaperRoute.ViewModels.ManuscriptPage;

public enum ManuscriptSaveOutcome
{
    // Written: the store has committed the candidate to the session's record.
    Saved,

    // Not tried, and nothing written: other library work ran, or the library
    // cannot be saved.
    Refused,

    // Tried, and the manuscripts were not written.
    Failed,
}

// What IManuscriptStore gives for a save, a retry or a delete (build 9's
// design, 2.2): Saved, Refused, or Failed with the message to show and
// whether the author file was written before the failure (2.5, session step
// 6), so the page's own records are on disk and the next save has none to
// add.
public sealed record ManuscriptSaveResult
{
    private ManuscriptSaveResult(ManuscriptSaveOutcome outcome, string message, bool authorLibraryWritten)
    {
        Outcome = outcome;
        Message = message;
        AuthorLibraryWritten = authorLibraryWritten;
    }

    public static ManuscriptSaveResult Saved { get; } = new(ManuscriptSaveOutcome.Saved, string.Empty, authorLibraryWritten: false);

    public static ManuscriptSaveResult Refused { get; } = new(ManuscriptSaveOutcome.Refused, string.Empty, authorLibraryWritten: false);

    public ManuscriptSaveOutcome Outcome { get; }

    // Empty unless Failed.
    public string Message { get; }

    public bool AuthorLibraryWritten { get; }

    public static ManuscriptSaveResult Failed(string message, bool authorLibraryWritten)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        return new(ManuscriptSaveOutcome.Failed, message, authorLibraryWritten);
    }
}
