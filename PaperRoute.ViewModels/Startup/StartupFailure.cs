namespace PaperRoute.ViewModels.Startup;

// The four ways PaperRoute fails to start, as today's StartupFailureForm.vb
// names them (start-up design 3.2 rows 20 and 21, build 6 F1).
public enum StartupFailureKind
{
    // The storage step failed (Program.vb:162-166), before any window.
    Storage,

    // manuscripts.json and its previous save could not be read while loading.
    ManuscriptLibrary,

    // A library file another program holds open (#111): not damage.
    LibraryInUse,

    // authors.json could not be read, at start or mid-session.
    AuthorLibrary,
}

// What stopped PaperRoute from starting: one line saying what failed, the
// explanation, the file being read when it failed, and the error; and what
// the failure window may offer to put right. The words and offers are
// today's (StartupFailureForm.vb:17-135; StartupFailureTextsMatchTodays
// compares them). Only the storage kind is raised before any window
// (Program.vb:162-166); the other three come from the load (Form1.vb:1412-1453,
// 1506-1541), which builds them with IsInUse deciding between in use and
// damaged.
public sealed class StartupFailure
{
    private StartupFailure(
        StartupFailureKind kind,
        string heading,
        string explanation,
        string? filePath,
        Exception cause,
        bool offersPreviousSave,
        bool offersRestore)
    {
        ArgumentNullException.ThrowIfNull(cause);
        Kind = kind;
        Heading = heading;
        Explanation = explanation;
        FilePath = filePath ?? string.Empty;
        Cause = cause;
        OffersPreviousSave = offersPreviousSave;
        OffersRestore = offersRestore;
    }

    public StartupFailureKind Kind { get; }

    public string Heading { get; }

    public string Explanation { get; }

    public string FilePath { get; }

    public Exception Cause { get; }

    // What the window may offer to put right (#124), before the window's own
    // checks: the previous save is manuscripts.bak, so it is offered only
    // where the manuscript library or the storage it sits in failed; a
    // backup archive also puts back the author library. A file another
    // program holds is not damage, so neither is offered for it.
    public bool OffersPreviousSave { get; }

    public bool OffersRestore { get; }

    public string FolderPath => Path.GetDirectoryName(FilePath) ?? string.Empty;

    public static StartupFailure ForStorage(Exception cause, string? schemaFilePath) => new(
        StartupFailureKind.Storage,
        "PaperRoute could not prepare its local storage.",
        "The storage format could not be verified, so PaperRoute closed rather than open or rewrite data in an unknown format. Existing files were left in place wherever possible.",
        schemaFilePath,
        cause,
        offersPreviousSave: true,
        offersRestore: true);

    public static StartupFailure ForManuscriptLibrary(Exception cause, string? dataFilePath) => new(
        StartupFailureKind.ManuscriptLibrary,
        "PaperRoute could not read your manuscript library.",
        "The library file could not be read, and no safety backup could be recovered. PaperRoute closed rather than continue with an empty library. Your files were not overwritten.",
        dataFilePath,
        cause,
        offersPreviousSave: true,
        offersRestore: true);

    // A library file another program holds open (#111): not damage, so the
    // loader stopped before reading it or turning to its backup.
    public static StartupFailure ForLibraryInUse(Exception cause, string? dataFilePath) => new(
        StartupFailureKind.LibraryInUse,
        "PaperRoute could not open a library file that another program is using.",
        "This is not a sign of damage. PaperRoute stopped before reading the file or turning to its backup, so nothing was read, recovered or changed. Close the program that has the file open, then start PaperRoute again.",
        dataFilePath,
        cause,
        offersPreviousSave: false,
        offersRestore: false);

    // The author library is also read mid-session, where a restore would
    // replace the library under a window that goes on with the old one in
    // memory, so once start-up is over the load builds this without the offer.
    public static StartupFailure ForAuthorLibrary(Exception cause, string? dataFilePath, bool offersRestore = true) => new(
        StartupFailureKind.AuthorLibrary,
        "PaperRoute could not read the reusable author library.",
        "PaperRoute closed rather than continue with uncertain author metadata. Your files were not changed.",
        dataFilePath,
        cause,
        offersPreviousSave: false,
        offersRestore: offersRestore);

    // The in-use test of today's load (Form1.vb:1412, 1506): the core raises
    // its own in-use exception, before reading the file or turning to its
    // backup, when another program holds a library file. Such a failure is
    // LibraryInUse; any other is the library's own kind.
    public static bool IsInUse(Exception cause) => cause is StorageFileInUseException;
}
